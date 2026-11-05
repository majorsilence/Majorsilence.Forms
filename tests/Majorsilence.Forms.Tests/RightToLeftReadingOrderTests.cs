using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported app's control reads in its RightToLeft order, as upstream's does (DT_RTLREADING), whatever its first
    // letter. Measured on WinForms (.NET 10) with 8.25pt labels (probe wfrtl): with RightToLeft.Yes ":Cheque Total" shows
    // "Cheque Total:" at the right edge -- the colon is a neutral before the first letter, so it takes the paragraph's
    // direction and goes to the right -- and its ink is column for column TextRenderer's "Cheque Total:" right aligned.
    // "-5.00" shows "5.00-" and "#1 Fee" "Fee #1". With RightToLeft.No a caption beginning in Hebrew is still a
    // left-to-right paragraph, its colon at the left. Here the first strong letter decided: the RightToLeft label showed
    // ":Cheque Total", and ":שלום" put its colon at the right.
    [Collection ("Headless")]
    public class RightToLeftReadingOrderTests
    {
        [Theory]
        [InlineData (":Cheque Total", "Cheque Total:")]
        [InlineData ("-5.00", "5.00-")]
        [InlineData ("#1 Fee", "Fee #1")]
        public void A_right_to_left_label_shows_its_caption_reordered_at_the_right (string caption, string shown)
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            try {
                var rtl = Render (caption, RightToLeft.Yes, ContentAlignment.TopLeft);
                var ltr = Render (shown, RightToLeft.No, ContentAlignment.TopRight);

                AssertSamePixels (ltr, rtl);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_left_to_right_label_keeps_a_leading_neutral_at_the_left_of_a_right_to_left_word ()
        {
            HeadlessRenderer.Use ();
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f));
            try {
                // ":שלום" read left to right is the colon, then the word: what "שלום:" read right to left shows at the left.
                var ltr = Render (":שלום", RightToLeft.No, ContentAlignment.TopLeft);
                var rtl = Render ("שלום:", RightToLeft.Yes, ContentAlignment.TopRight);

                AssertSamePixels (rtl, ltr);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // An app that never chose a font keeps the first-strong-letter order: the theme's look is unchanged.
        [Fact]
        public void Without_a_default_font_the_first_letter_decides_the_order ()
        {
            HeadlessRenderer.Use ();

            var rtl = Render (":Cheque Total", RightToLeft.Yes, ContentAlignment.TopLeft);
            var literal = Render (":Cheque Total", RightToLeft.No, ContentAlignment.TopRight);

            AssertSamePixels (literal, rtl);
        }

        private static SKBitmap Render (string caption, RightToLeft order, ContentAlignment align)
        {
            using var form = new Form { Size = new Size (300, 100) };
            var label = new Label {
                Left = 10, Top = 10, Width = 200, Height = 20, AutoSize = false,
                Text = caption, RightToLeft = order, TextAlign = align,
                Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f),
                BackColor = Color.White, ForeColor = Color.Black,
            };
            form.Controls.Add (label);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = (SKBitmap) typeof (Control).GetMethod ("GetBackBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke (label, null)!;

            return buffer.Copy ();
        }

        private static void AssertSamePixels (SKBitmap expected, SKBitmap actual)
        {
            Assert.Equal (expected.Width, actual.Width);
            Assert.Equal (expected.Height, actual.Height);

            var inked = 0;

            for (var y = 0; y < expected.Height; y++) {
                for (var x = 0; x < expected.Width; x++) {
                    var e = expected.GetPixel (x, y);
                    var a = actual.GetPixel (x, y);
                    Assert.True (e == a, $"pixel ({x}, {y}): expected {e}, got {a}");

                    if (e.Red < 128)
                        inked++;
                }
            }

            Assert.True (inked > 10, "the caption drew no ink");
        }
    }
}
