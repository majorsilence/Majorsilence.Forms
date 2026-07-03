namespace Majorsilence.Forms.SpellCheck
{
    /// <summary>Represents a single word-like token found in a body of text, and its position within it.</summary>
    internal readonly struct WordToken
    {
        /// <summary>Initializes a new instance of the <see cref="WordToken"/> struct.</summary>
        internal WordToken (string text, int start, bool isSentenceStart)
        {
            Text = text;
            Start = start;
            IsSentenceStart = isSentenceStart;
        }

        /// <summary>Gets the token's text.</summary>
        internal string Text { get; }

        /// <summary>Gets the index of the first character of the token within the source text.</summary>
        internal int Start { get; }

        /// <summary>Gets the index one past the last character of the token within the source text.</summary>
        internal int End => Start + Text.Length;

        /// <summary>Gets whether this token is the first word of a sentence (used for title-case leniency).</summary>
        internal bool IsSentenceStart { get; }
    }

    /// <summary>
    /// Splits free-form text into spell-checkable word tokens: runs of letters that may contain internal
    /// apostrophes or hyphens (so "don't", "well-known" tokenize as single words), skipping numbers, URLs,
    /// email addresses, and ALL-CAPS tokens (acronyms) entirely.
    /// </summary>
    internal static class WordTokenizer
    {
        // Characters allowed inside a URL/email "noise run" once one is detected - wider than the normal
        // word charset because a URL/email needs to swallow its dots, slashes, colons, and @ sign so the
        // whole thing is skipped as one unit instead of splitting into separate (falsely-misspelled) words.
        private const string UrlExtraChars = ".:/@?&=#%_~";

        /// <summary>Tokenizes <paramref name="text"/>, yielding one <see cref="WordToken"/> per checkable word.</summary>
        internal static IEnumerable<WordToken> Tokenize (string text)
        {
            if (string.IsNullOrEmpty (text))
                yield break;

            var i = 0;
            var atSentenceStart = true;

            while (i < text.Length) {
                var c = text[i];

                if (char.IsWhiteSpace (c)) {
                    i++;
                    continue;
                }

                if (!char.IsLetter (c) && !char.IsDigit (c)) {
                    // Sentence-ending punctuation flips the "expect a capital" flag for the next word.
                    if (c == '.' || c == '!' || c == '?')
                        atSentenceStart = true;

                    i++;
                    continue;
                }

                var start = i;

                // Scan ahead using the wider URL/email charset first, so a "://" or "@" found anywhere in
                // the run causes the *entire* run (domain, path, etc.) to be consumed and skipped as one
                // token, rather than falling through to per-word tokenization and splitting on the dots.
                var wideEnd = i;
                while (wideEnd < text.Length && (char.IsLetterOrDigit (text[wideEnd]) || UrlExtraChars.Contains (text[wideEnd])))
                    wideEnd++;

                var wideRun = text[start..wideEnd];

                if (wideRun.Contains ("://", StringComparison.Ordinal) || IsEmailLike (wideRun)) {
                    i = wideEnd;
                    atSentenceStart = false;
                    continue;
                }

                // Not URL/email-shaped: fall back to normal word tokenization - letters, plus embedded
                // apostrophes/hyphens that are immediately followed by another letter (so trailing
                // punctuation like a quote or a hyphen used as a dash is not swallowed into the token).
                var sawDigit = false;

                while (i < text.Length) {
                    var ch = text[i];

                    if (char.IsLetter (ch)) {
                        i++;
                        continue;
                    }

                    if (char.IsDigit (ch)) {
                        sawDigit = true;
                        i++;
                        continue;
                    }

                    if ((ch == '\'' || ch == '’' || ch == '-') &&
                        i + 1 < text.Length && (char.IsLetterOrDigit (text[i + 1]))) {
                        i++;
                        continue;
                    }

                    break;
                }

                var word = text[start..i];

                // Skip pure noise: contains digits (part numbers, dates), or every letter in the token is
                // uppercase and it has more than one letter (an acronym like "NASA").
                var skip = sawDigit || IsAllCaps (word);

                if (!skip)
                    yield return new WordToken (word, start, atSentenceStart);

                atSentenceStart = false;
            }
        }

        // A wide run is email-like if it contains exactly one '@' with at least one character on each
        // side and a '.' somewhere after the '@' (a minimal "user@host.tld" shape).
        private static bool IsEmailLike (string run)
        {
            var at = run.IndexOf ('@');

            if (at <= 0 || at == run.Length - 1)
                return false;

            return run.IndexOf ('.', at + 1) > at;
        }

        private static bool IsAllCaps (string word)
        {
            var letterCount = 0;
            var upperCount = 0;

            foreach (var c in word) {
                if (!char.IsLetter (c))
                    continue;

                letterCount++;

                if (char.IsUpper (c))
                    upperCount++;
            }

            return letterCount > 1 && letterCount == upperCount;
        }
    }
}
