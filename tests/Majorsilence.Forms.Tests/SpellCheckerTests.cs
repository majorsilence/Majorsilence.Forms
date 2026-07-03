using Majorsilence.Forms.SpellCheck;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class SpellCheckerTests
    {
        // --- Tokenizer -----------------------------------------------------------------------------

        [Fact]
        public void Tokenize_SplitsOnWhitespaceAndPunctuation ()
        {
            var tokens = WordTokenizer.Tokenize ("Hello, world!").Select (t => t.Text).ToArray ();
            Assert.Equal (new[] { "Hello", "world" }, tokens);
        }

        [Fact]
        public void Tokenize_KeepsInternalApostrophe ()
        {
            var tokens = WordTokenizer.Tokenize ("don't stop").Select (t => t.Text).ToArray ();
            Assert.Equal (new[] { "don't", "stop" }, tokens);
        }

        [Fact]
        public void Tokenize_KeepsInternalHyphen ()
        {
            var tokens = WordTokenizer.Tokenize ("a well-known fact").Select (t => t.Text).ToArray ();
            Assert.Equal (new[] { "a", "well-known", "fact" }, tokens);
        }

        [Fact]
        public void Tokenize_SkipsTokensContainingDigits ()
        {
            var tokens = WordTokenizer.Tokenize ("room 42a is ready").Select (t => t.Text).ToArray ();
            Assert.Equal (new[] { "room", "is", "ready" }, tokens);
        }

        [Fact]
        public void Tokenize_SkipsUrls ()
        {
            var tokens = WordTokenizer.Tokenize ("visit https://example.com today").Select (t => t.Text).ToArray ();
            Assert.DoesNotContain ("https", tokens);
            Assert.Contains ("visit", tokens);
            Assert.Contains ("today", tokens);
        }

        [Fact]
        public void Tokenize_SkipsEmailAddresses ()
        {
            var tokens = WordTokenizer.Tokenize ("email me at foo@bar.com please").Select (t => t.Text).ToArray ();
            Assert.DoesNotContain ("foo", tokens);
            Assert.Contains ("please", tokens);
        }

        [Fact]
        public void Tokenize_SkipsAllCapsAcronyms ()
        {
            var tokens = WordTokenizer.Tokenize ("the NASA mission").Select (t => t.Text).ToArray ();
            Assert.Equal (new[] { "the", "mission" }, tokens);
        }

        [Fact]
        public void Tokenize_SingleUppercaseLetterIsNotTreatedAsAcronym ()
        {
            // A lone "I" is a valid (correctly spelled) word, not a two-letter-minimum acronym.
            var tokens = WordTokenizer.Tokenize ("I am here").Select (t => t.Text).ToArray ();
            Assert.Contains ("I", tokens);
        }

        [Fact]
        public void Tokenize_FirstWordIsSentenceStart ()
        {
            var tokens = WordTokenizer.Tokenize ("Hello world. Second sentence.").ToArray ();
            Assert.True (tokens[0].IsSentenceStart);
            Assert.False (tokens[1].IsSentenceStart);
            Assert.True (tokens.Single (t => t.Text == "Second").IsSentenceStart);
        }

        [Fact]
        public void Tokenize_ReturnsCorrectCharacterOffsets ()
        {
            var tokens = WordTokenizer.Tokenize ("foo bar").ToArray ();
            Assert.Equal (0, tokens[0].Start);
            Assert.Equal (3, tokens[0].End);
            Assert.Equal (4, tokens[1].Start);
            Assert.Equal (7, tokens[1].End);
        }

        [Fact]
        public void Tokenize_EmptyString_ReturnsNoTokens ()
        {
            Assert.Empty (WordTokenizer.Tokenize (string.Empty));
        }

        // --- Case rules ------------------------------------------------------------------------------

        [Theory]
        [InlineData ("the")]
        [InlineData ("running")]
        [InlineData ("cities")]
        [InlineData ("unhappy")]
        [InlineData ("receive")]
        [InlineData ("happiness")]
        public void IsMisspelled_KnownGoodLowercaseWords_ReturnsFalse (string word)
        {
            var checker = new SpellChecker ();
            Assert.False (checker.IsMisspelled (word));
        }

        [Theory]
        [InlineData ("teh")]
        [InlineData ("recieve")]
        [InlineData ("wierd")]
        [InlineData ("occured")]
        [InlineData ("seperate")]
        [InlineData ("definately")]
        [InlineData ("xzqvthisisnotaword")]
        public void IsMisspelled_KnownBadWords_ReturnsTrue (string word)
        {
            var checker = new SpellChecker ();
            Assert.True (checker.IsMisspelled (word));
        }

        [Fact]
        public void IsMisspelled_ExactCaseProperNoun_ReturnsFalse ()
        {
            var checker = new SpellChecker ();
            Assert.False (checker.IsMisspelled ("London"));
        }

        [Fact]
        public void IsMisspelled_MidSentenceCapitalizedOrdinaryWord_ReturnsFalse ()
        {
            // "Running" typed with a stray capital mid-sentence should still validate against the
            // dictionary's "running" entry via the lowercase-match rule.
            var checker = new SpellChecker ();
            Assert.False (checker.IsMisspelled ("Running", isSentenceStart: false));
        }

        [Fact]
        public void IsMisspelled_SentenceStartCapitalizedWord_ReturnsFalse ()
        {
            var checker = new SpellChecker ();
            Assert.False (checker.IsMisspelled ("The", isSentenceStart: true));
        }

        [Fact]
        public void IsMisspelled_EmptyString_ReturnsFalse ()
        {
            var checker = new SpellChecker ();
            Assert.False (checker.IsMisspelled (string.Empty));
        }

        [Fact]
        public void IsMisspelled_UserAddedWord_ReturnsFalse ()
        {
            var checker = new SpellChecker ();
            const string madeUpWord = "zzflibbertytest";

            Assert.True (checker.IsMisspelled (madeUpWord));

            checker.AddToDictionary (madeUpWord);

            Assert.False (checker.IsMisspelled (madeUpWord));
        }

        // --- Suggestions -----------------------------------------------------------------------------

        [Fact]
        public void GetSuggestions_ClassicTypo_TehSuggestsThe ()
        {
            var checker = new SpellChecker ();
            var suggestions = checker.GetSuggestions ("teh");
            Assert.Contains ("the", suggestions);
        }

        [Fact]
        public void GetSuggestions_ClassicTypo_RecieveSuggestsReceive ()
        {
            var checker = new SpellChecker ();
            var suggestions = checker.GetSuggestions ("recieve");
            Assert.Contains ("receive", suggestions);
        }

        [Fact]
        public void GetSuggestions_PreservesCapitalization ()
        {
            var checker = new SpellChecker ();
            var suggestions = checker.GetSuggestions ("Teh");
            Assert.Contains ("The", suggestions);
        }

        [Fact]
        public void GetSuggestions_CapsAtEightResults ()
        {
            var checker = new SpellChecker ();
            // A short, very ambiguous fragment tends to generate many dictionary hits.
            var suggestions = checker.GetSuggestions ("ba");
            Assert.True (suggestions.Count <= 8);
        }

        [Fact]
        public void GetSuggestions_EmptyString_ReturnsEmpty ()
        {
            var checker = new SpellChecker ();
            Assert.Empty (checker.GetSuggestions (string.Empty));
        }

        [Fact]
        public void GetSuggestions_DoesNotSuggestTheWordItself ()
        {
            var checker = new SpellChecker ();
            var suggestions = checker.GetSuggestions ("the");
            Assert.DoesNotContain ("the", suggestions);
        }

        // --- Dictionary loading ------------------------------------------------------------------------

        [Fact]
        public void Dictionary_LoadsPlausibleWordCount ()
        {
            // Sanity bound on the embedded, pre-expanded en_US word list (see third-party-licenses.md).
            var checker = new SpellChecker ();
            Assert.False (checker.IsMisspelled ("test")); // forces the dictionary to load
            Assert.InRange (SpellingDictionaryTestHook.Count, 100_000, 200_000);
        }
    }

    // Exposes the internal SpellingDictionary.Count for the plausible-range sanity test above without
    // widening SpellingDictionary's own (intentionally minimal) internal surface.
    internal static class SpellingDictionaryTestHook
    {
        internal static int Count => SpellingDictionary.Count;
    }
}
