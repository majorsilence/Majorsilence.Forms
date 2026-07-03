using System.IO.Compression;

namespace Majorsilence.Forms.SpellCheck
{
    /// <summary>
    /// Lazily-loaded, process-wide en_US word list backing <see cref="SpellChecker"/>. The word list is a
    /// pre-expanded (all Hunspell affix rules already applied), deflate-compressed, sorted flat file
    /// embedded as a resource — see <c>src/Majorsilence.Forms/SpellCheck/en_US.words.gz</c> and
    /// <c>third-party-licenses.md</c> for provenance and licensing.
    /// </summary>
    internal static class SpellingDictionary
    {
        private const string ResourceName = "Majorsilence.Forms.SpellCheck.en_US.words.gz";

        // Case-sensitive storage of every word form exactly as it appears in the source dictionary
        // (so proper nouns like "London" keep their capital, while "america" - absent from the
        // dictionary in lowercase form - does not silently validate).
        private static HashSet<string>? _originalCase;

        // Lowercase projection of every word in _originalCase, used for the common case of validating
        // ordinary lowercase (or start-of-sentence-capitalized) prose without requiring an exact case match.
        private static HashSet<string>? _lowercase;

        private static readonly object _lock = new ();

        /// <summary>Session-local words the user has explicitly added via "Add to Dictionary". Not persisted.</summary>
        internal static readonly HashSet<string> UserWords = new (StringComparer.Ordinal);

        private static void EnsureLoaded ()
        {
            if (_originalCase != null)
                return;

            lock (_lock) {
                if (_originalCase != null)
                    return;

                var original = new HashSet<string> (StringComparer.Ordinal);
                var lower = new HashSet<string> (StringComparer.Ordinal);

                var assembly = typeof (SpellingDictionary).Assembly;
                using var resourceStream = assembly.GetManifestResourceStream (ResourceName);

                if (resourceStream != null) {
                    using var gzip = new GZipStream (resourceStream, CompressionMode.Decompress);
                    using var reader = new StreamReader (gzip, System.Text.Encoding.UTF8);

                    string? line;
                    while ((line = reader.ReadLine ()) != null) {
                        if (line.Length == 0)
                            continue;

                        original.Add (line);
                        lower.Add (line.ToLowerInvariant ());
                    }
                }

                _lowercase = lower;
                _originalCase = original;
            }
        }

        /// <summary>
        /// Returns true if <paramref name="word"/> is a recognized dictionary word or a user-added word,
        /// per the case rules in <see cref="SpellChecker.IsMisspelled(string, bool)"/>.
        /// </summary>
        internal static bool Contains (string word, bool allowLowercaseMatch)
        {
            EnsureLoaded ();

            if (UserWords.Contains (word))
                return true;

            if (_originalCase!.Contains (word))
                return true;

            if (allowLowercaseMatch && _lowercase!.Contains (word.ToLowerInvariant ()))
                return true;

            return false;
        }

        /// <summary>Returns true if <paramref name="lowercaseWord"/> (already lowercased by the caller) is a recognized dictionary word.</summary>
        internal static bool ContainsLowercase (string lowercaseWord)
        {
            EnsureLoaded ();

            return _lowercase!.Contains (lowercaseWord) || UserWords.Contains (lowercaseWord);
        }

        /// <summary>Gets the total number of distinct word forms loaded (diagnostic / test use).</summary>
        internal static int Count {
            get {
                EnsureLoaded ();
                return _originalCase!.Count;
            }
        }
    }
}
