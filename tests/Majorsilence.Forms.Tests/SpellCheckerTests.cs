using Majorsilence.Forms.Headless;
using Majorsilence.Forms.SpellCheck;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class SpellCheckerTests
    {
        // --- Right-click suggestion menu (regression: MouseUp-vs-MouseDown ordering) ------------------

        // Finds a control-relative x inside the TextBox whose GetSpellCheckCharIndexFromPosition falls
        // within [start, end) - i.e. a pixel that hit-tests into the given word - by scanning left to
        // right. Uses the same production hit-testing path the bug itself goes through, rather than
        // duplicating Skia's text-layout math with guessed pixel offsets.
        private static int FindXForCharRange (TextBox textBox, int start, int end)
        {
            for (var x = 0; x < textBox.Width; x++) {
                var index = textBox.GetSpellCheckCharIndexFromPosition (new System.Drawing.Point (x, textBox.Height / 2));

                if (index >= start && index < end)
                    return x;
            }

            throw new InvalidOperationException ($"Could not find an x position that hit-tests inside char range [{start}, {end}).");
        }

        private static (Form form, TextBox textBox) CreateSpellCheckedForm (string text)
        {
            // UseSystemDecorations gives a clean client area on every platform: without it the custom
            // FormTitleBar (drawn in-client on Windows/Linux) overlaps a control at the top and would
            // intercept the click meant for the TextBox (see HeadlessBackendTests.TextInput_ReachesFocusedTextBox).
            var form = new Form { UseSystemDecorations = true };
            var textBox = new TextBox {
                Text = text,
                // Inset from every edge: Form.HandleMouseDown treats a MINIMUM_RESIZE_PIXELS-wide (4px)
                // band along each window edge as a resize handle and swallows MouseDown there before it
                // ever reaches the control tree (MouseUp/Click have no such gate) - see Form.cs
                // GetElementAtLocation. Left/Top = 20 keeps the whole TextBox well clear of that band.
                Left = 20,
                Top = 20,
                Width = 400,
                Height = 25,
            };

            form.Controls.Add (textBox);
            TextBoxSpellCheck.SetSpellChecker (textBox, new SpellChecker ());
            form.Show ();

            // Force a layout/paint pass so the text block is measured and hit-testing has real geometry
            // to work with (mirrors HeadlessRenderer.CapturePng usage before reading Bounds elsewhere).
            HeadlessRenderer.Use ();
            HeadlessRenderer.CapturePng (form, 500, 100);

            return (form, textBox);
        }

        private static void RightClickAt (Form form, TextBox textBox, int controlRelativeX)
        {
            var formX = textBox.Left + controlRelativeX;
            var formY = textBox.Top + textBox.Height / 2;

            // Drives the real input pipeline (WindowBase.HandlePointerPressed / HandlePointerReleased),
            // which is what actually orders MouseDown -> Click (ContextMenu.Show -> Opening) -> MouseUp -
            // the exact sequence the bug depends on.
            HeadlessRenderer.Click (form, formX, formY, MouseButtons.Right);
        }

        [Fact]
        public void RightClick_FirstEverClick_PopulatesSuggestionsForClickedWord ()
        {
            // "recieve" (misspelled) is the very first word; this is the FIRST right-click ever made
            // against this TextBox, so LastClickedCharIndex starts at -1. Before the fix (subscribing on
            // MouseUp), BuildContextMenuItems would run before MouseUp ever set the index, leaving the
            // menu empty on this very first interaction.
            var (form, textBox) = CreateSpellCheckedForm ("recieve the package");

            var ranges = TextBoxSpellCheck.GetMisspelledRanges (textBox);
            var recieve = Assert.Single (ranges, r => r.Word == "recieve");

            var x = FindXForCharRange (textBox, recieve.Start, recieve.End);
            RightClickAt (form, textBox, x);

            // The click handler must have captured a char index inside "recieve" by the time the menu
            // was built (proves the fix: MouseDown, not MouseUp, is what populates LastClickedCharIndex).
            var lastClicked = TextBoxSpellCheck.GetLastClickedCharIndexForTest (textBox);
            Assert.InRange (lastClicked, recieve.Start, recieve.End - 1);

            var suggestionTexts = textBox.ContextMenu!.Items.OfType<MenuItem> ().Select (i => i.Text).ToArray ();
            Assert.Contains ("receive", suggestionTexts);

            form.Close ();
        }

        [Fact]
        public void RightClick_SecondClickAtDifferentWord_ShowsThatWordsSuggestions_NotThePreviousWords ()
        {
            // Two distinct misspelled words. After right-clicking "recieve" then "wierd", the menu built
            // for the second click must reflect "wierd" - not still show "recieve"'s suggestions (the
            // off-by-one-click symptom).
            var (form, textBox) = CreateSpellCheckedForm ("recieve the wierd package");

            var ranges = TextBoxSpellCheck.GetMisspelledRanges (textBox);
            var recieve = Assert.Single (ranges, r => r.Word == "recieve");
            var wierd = Assert.Single (ranges, r => r.Word == "wierd");

            RightClickAt (form, textBox, FindXForCharRange (textBox, recieve.Start, recieve.End));

            var firstMenuTexts = textBox.ContextMenu!.Items.OfType<MenuItem> ().Select (i => i.Text).ToArray ();
            Assert.Contains ("receive", firstMenuTexts);

            RightClickAt (form, textBox, FindXForCharRange (textBox, wierd.Start, wierd.End));

            var secondMenuTexts = textBox.ContextMenu!.Items.OfType<MenuItem> ().Select (i => i.Text).ToArray ();
            Assert.Contains ("weird", secondMenuTexts);
            Assert.DoesNotContain ("receive", secondMenuTexts);

            form.Close ();
        }

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
