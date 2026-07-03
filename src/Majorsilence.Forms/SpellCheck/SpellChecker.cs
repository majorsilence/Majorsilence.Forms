namespace Majorsilence.Forms.SpellCheck
{
    /// <summary>
    /// A dependency-free, in-process spell checker backed by a pre-expanded Hunspell/SCOWL en_US word
    /// list (<see cref="SpellingDictionary"/>). Provides tokenization, misspelling detection with
    /// case-sensitivity rules appropriate for free-form prose, and edit-distance suggestions.
    /// </summary>
    /// <remarks>
    /// This is the engine behind the <c>Majorsilence.Forms.Telerik.RadSpellChecker</c> compat surface and
    /// the TextBox squiggle-underline rendering. It has no dependency on any UI type so it can be unit
    /// tested and reused independently of a specific control.
    /// </remarks>
    public sealed class SpellChecker
    {
        private const int MaxSuggestions = 8;
        private const int ShortWordLength = 5;

        /// <summary>
        /// Adds a word to the session-local "Add to Dictionary" list shared by all <see cref="SpellChecker"/>
        /// instances in this process. Not persisted across process runs.
        /// </summary>
        public void AddToDictionary (string word)
        {
            if (string.IsNullOrWhiteSpace (word))
                return;

            SpellingDictionary.UserWords.Add (word);
        }

        /// <summary>Tokenizes <paramref name="text"/> into the words this checker would consider for spelling.</summary>
        internal static IEnumerable<WordToken> Tokenize (string text) => WordTokenizer.Tokenize (text);

        /// <summary>
        /// Returns true if <paramref name="word"/> is not found in the dictionary (misspelled). Numbers,
        /// URLs, e-mail addresses, and ALL-CAPS tokens should be filtered out by the tokenizer before
        /// reaching this method; calling it directly on such a token returns whatever the raw lookup finds.
        /// </summary>
        /// <param name="word">The word to check.</param>
        /// <param name="isSentenceStart">
        /// When true, a leading-capital form of a dictionary word is accepted (e.g. "The" at the start of
        /// a sentence matches the dictionary entry "the") in addition to an exact or all-lowercase match.
        /// </param>
        public bool IsMisspelled (string word, bool isSentenceStart = false)
        {
            if (string.IsNullOrEmpty (word))
                return false;

            // 1. Exact match (handles proper nouns like "London" and words that are only ever
            //    capitalized, plus words already in their canonical lowercase form).
            if (SpellingDictionary.Contains (word, allowLowercaseMatch: false))
                return false;

            // 2. All-lowercase match: catches ordinary words typed with unexpected casing, e.g. "Running"
            //    typed mid-sentence, or a fully-lowercased comparison of a properly-cased word.
            if (SpellingDictionary.ContainsLowercase (word.ToLowerInvariant ()))
                return false;

            // 3. Title-case-at-sentence-start rule: "The" is valid at the start of a sentence purely
            //    because it capitalizes a dictionary word ("the") - already covered by rule 2 since
            //    ToLowerInvariant("The") == "the". This rule instead protects the inverse: a word that is
            //    ONLY in the dictionary in Titlecase (e.g. a name), referenced without extra help needed;
            //    exact match (rule 1) already covers it. Kept as an explicit branch for clarity and so a
            //    future stricter mode (e.g. one that removes rule 2) still special-cases sentence starts.
            if (isSentenceStart && word.Length > 0 && char.IsUpper (word[0])) {
                var lowerFirst = char.ToLowerInvariant (word[0]) + word[1..];
                if (SpellingDictionary.Contains (lowerFirst, allowLowercaseMatch: true))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns up to 8 suggested replacements for a misspelled word, ordered by edit distance then
        /// alphabetically. Candidates are generated at edit-distance 1 (insertions, deletions,
        /// substitutions, and transpositions); for words of 5 characters or fewer the search is extended
        /// to edit-distance 2, since a short word needing a two-step correction (e.g. "hlepr" -&gt; "help")
        /// is common and edit-distance-1 alone would miss it.
        /// </summary>
        public IReadOnlyList<string> GetSuggestions (string word)
        {
            if (string.IsNullOrEmpty (word))
                return [];

            var candidates = new SortedSet<string> (StringComparer.Ordinal);
            var lower = word.ToLowerInvariant ();

            CollectValidEdits (lower, candidates);

            if (word.Length <= ShortWordLength) {
                // Distance-2: apply the distance-1 generator again to every distance-1 candidate string
                // (valid or not), then filter. This is the standard "generate then filter" approach used
                // by simple spell checkers (e.g. Peter Norvig's), traded for suggestion latency rather
                // than a full weighted edit-distance search - acceptable at this word length.
                var distance1All = new HashSet<string> (StringComparer.Ordinal);
                GenerateEdits (lower, distance1All);

                foreach (var candidate1 in distance1All)
                    CollectValidEdits (candidate1, candidates);
            }

            // Preserve the capitalization style of the original word for the suggestions (e.g. "Teh" ->
            // "The" rather than "the") when the input started with an uppercase letter.
            var capitalizeResult = word.Length > 0 && char.IsUpper (word[0]);

            return candidates
                .Where (c => !string.Equals (c, lower, StringComparison.Ordinal))
                .OrderBy (c => DamerauLevenshteinDistance (lower, c))
                // Among same-distance candidates, prefer ones that are anagrams of the original word (same
                // letters, different order) - the strongest available signal that a transposition typo
                // ("teh" -> "the") is the more likely correction than an unrelated substitution/insertion
                // match of the same edit distance ("teh" -> "ten"). Without word-frequency data this
                // (plus the length-closeness tie-break below) is the best available proxy for relevance.
                .ThenBy (c => IsAnagram (lower, c) ? 0 : 1)
                .ThenBy (c => Math.Abs (c.Length - lower.Length))
                .ThenBy (c => c, StringComparer.Ordinal)
                .Take (MaxSuggestions)
                .Select (c => capitalizeResult ? char.ToUpperInvariant (c[0]) + c[1..] : c)
                .ToList ();
        }

        // True if `a` and `b` contain exactly the same multiset of characters (same length required).
        private static bool IsAnagram (string a, string b)
        {
            if (a.Length != b.Length)
                return false;

            var chars = a.ToCharArray ();
            Array.Sort (chars);
            var otherChars = b.ToCharArray ();
            Array.Sort (otherChars);

            return chars.AsSpan ().SequenceEqual (otherChars);
        }

        // Generates every edit-distance-1 variant of `word` and adds the ones found in the dictionary to `results`.
        private static void CollectValidEdits (string word, SortedSet<string> results)
        {
            var edits = new HashSet<string> (StringComparer.Ordinal);
            GenerateEdits (word, edits);

            foreach (var edit in edits) {
                if (SpellingDictionary.ContainsLowercase (edit))
                    results.Add (edit);
            }
        }

        // Standard single-edit-distance candidate generation: deletions, transpositions, substitutions,
        // and insertions, restricted to lowercase a-z (the dictionary's word forms are ASCII/Latin text;
        // this keeps the candidate set - already O(54 * len) - from ballooning for accented input).
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyz'";

        private static void GenerateEdits (string word, HashSet<string> results)
        {
            for (var i = 0; i < word.Length; i++) {
                // Deletion
                results.Add (word[..i] + word[(i + 1)..]);

                // Transposition
                if (i + 1 < word.Length)
                    results.Add (word[..i] + word[i + 1] + word[i] + word[(i + 2)..]);

                // Substitution
                foreach (var c in Alphabet)
                    results.Add (word[..i] + c + word[(i + 1)..]);
            }

            // Insertion
            for (var i = 0; i <= word.Length; i++) {
                foreach (var c in Alphabet)
                    results.Add (word[..i] + c + word[i..]);
            }
        }

        // Optimal string alignment (Damerau-Levenshtein restricted to one transposition per substring)
        // distance, used only to rank an already-filtered (small) candidate set. Adjacent-transposition
        // typos ("teh" -> "the") must score as a single edit here to match how GenerateEdits treats them,
        // or a genuine distance-1 transposition candidate would be outranked by unrelated distance-1
        // substitution/insertion words and fall off the suggestion list's cap.
        private static int DamerauLevenshteinDistance (string a, string b)
        {
            var lenA = a.Length;
            var lenB = b.Length;
            var d = new int[lenA + 1, lenB + 1];

            for (var i = 0; i <= lenA; i++)
                d[i, 0] = i;

            for (var j = 0; j <= lenB; j++)
                d[0, j] = j;

            for (var i = 1; i <= lenA; i++) {
                for (var j = 1; j <= lenB; j++) {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min (
                        Math.Min (d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        d[i, j] = Math.Min (d[i, j], d[i - 2, j - 2] + 1);
                }
            }

            return d[lenA, lenB];
        }
    }
}
