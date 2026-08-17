using Majorsilence.Forms.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GDI+ decides how "&Cancel" renders from the StringFormat's HotkeyPrefix, and Krypton's AccurateText
    // sets it on every piece of button, tab and menu text it draws. Ignoring it drew a literal ampersand
    // on every dialog button in the Krypton suites, and sized each button for a character that was never
    // going to appear -- so both the measure and the draw path are pinned here.
    public class HotkeyPrefixTests
    {
        private static Graphics Surface (out Bitmap bitmap)
        {
            bitmap = new Bitmap (200, 60);
            return Graphics.FromImage (bitmap);
        }

        [Theory]
        [InlineData (Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Show)]
        [InlineData (Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Hide)]
        public void MeasureString_excludes_the_prefix (Majorsilence.Forms.Drawing.Text.HotkeyPrefix prefix)
        {
            using var bitmap = new Bitmap (200, 60);
            using var g = Graphics.FromImage (bitmap);
            var font = new Font ("Arial", 12f);

            var format = new StringFormat { HotkeyPrefix = prefix };

            Assert.Equal (
                g.MeasureString ("Cancel", font, format).Width,
                g.MeasureString ("C&ancel", font, format).Width,
                precision: 2);
        }

        [Fact]
        public void MeasureString_keeps_the_prefix_when_processing_is_off ()
        {
            using var bitmap = new Bitmap (200, 60);
            using var g = Graphics.FromImage (bitmap);
            var font = new Font ("Arial", 12f);

            var format = new StringFormat { HotkeyPrefix = Majorsilence.Forms.Drawing.Text.HotkeyPrefix.None };

            Assert.True (g.MeasureString ("C&ancel", font, format).Width
                > g.MeasureString ("Cancel", font, format).Width);
        }

        [Fact]
        public void MeasureString_collapses_a_doubled_prefix_to_one_ampersand ()
        {
            using var bitmap = new Bitmap (200, 60);
            using var g = Graphics.FromImage (bitmap);
            var font = new Font ("Arial", 12f);

            var show = new StringFormat { HotkeyPrefix = Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Show };
            var off = new StringFormat { HotkeyPrefix = Majorsilence.Forms.Drawing.Text.HotkeyPrefix.None };

            // "A&&B" is an escaped ampersand: it renders as the three characters "A&B", so it measures the
            // same as that literal string -- and WIDER than "A&B", which renders as "AB" with B underlined.
            Assert.Equal (
                g.MeasureString ("A&B", font, off).Width,
                g.MeasureString ("A&&B", font, show).Width,
                precision: 2);

            Assert.True (g.MeasureString ("A&&B", font, show).Width
                > g.MeasureString ("A&B", font, show).Width);
        }

        [Fact]
        public void Show_underlines_the_mnemonic_and_Hide_does_not ()
        {
            // The underline is the only difference between the two modes, so comparing the rendered pixels
            // is the test: same text, same position, one extra rule.
            static int InkedPixels (Majorsilence.Forms.Drawing.Text.HotkeyPrefix prefix)
            {
                using var bitmap = new Bitmap (200, 60);
                using var g = Graphics.FromImage (bitmap);
                g.Clear (System.Drawing.Color.White);

                using var brush = new SolidBrush (System.Drawing.Color.Black);
                g.DrawString ("C&ancel", new Font ("Arial", 12f), brush,
                    new System.Drawing.RectangleF (0, 0, 200, 60),
                    new StringFormat { HotkeyPrefix = prefix });

                var inked = 0;
                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++)
                        if (bitmap.GetPixel (x, y).R < 128)
                            inked++;

                return inked;
            }

            Assert.True (InkedPixels (Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Show) > InkedPixels (Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Hide));
        }

        // TextRenderer is the OTHER path, and the one that actually mattered: Krypton draws all
        // solid-coloured text through TextRenderer.DrawText, and reaches it with no prefix flag set at all.
        // Processing prefixes is the default there -- NoPrefix opts out -- so an unflagged call must strip.
        [Fact]
        public void TextRenderer_measures_without_the_prefix_by_default ()
        {
            var font = new Font ("Arial", 12f);

            Assert.Equal (
                TextRenderer.MeasureText ("Show", font, new System.Drawing.Size (500, 100), TextFormatFlags.Left).Width,
                TextRenderer.MeasureText ("S&how", font, new System.Drawing.Size (500, 100), TextFormatFlags.Left).Width);
        }

        [Fact]
        public void TextRenderer_keeps_the_prefix_when_NoPrefix_is_set ()
        {
            var font = new Font ("Arial", 12f);
            var area = new System.Drawing.Size (500, 100);

            Assert.True (
                TextRenderer.MeasureText ("S&how", font, area, TextFormatFlags.NoPrefix).Width
                > TextRenderer.MeasureText ("Show", font, area, TextFormatFlags.NoPrefix).Width);
        }

        [Theory]
        [InlineData (TextFormatFlags.Left, true)]         // default: underline drawn
        [InlineData (TextFormatFlags.HidePrefix, false)]  // stripped, no cue
        public void TextRenderer_draws_the_accelerator_cue_only_when_asked (TextFormatFlags flags, bool expectUnderline)
        {
            static int InkedPixels (TextFormatFlags f)
            {
                using var bitmap = new Bitmap (200, 60);
                using var g = Graphics.FromImage (bitmap);
                g.Clear (System.Drawing.Color.White);

                TextRenderer.DrawText (g, "S&how", new Font ("Arial", 12f),
                    new System.Drawing.Rectangle (0, 0, 200, 60), System.Drawing.Color.Black, f);

                var inked = 0;
                for (var x = 0; x < bitmap.Width; x++)
                    for (var y = 0; y < bitmap.Height; y++)
                        if (bitmap.GetPixel (x, y).R < 128)
                            inked++;

                return inked;
            }

            var withCue = InkedPixels (flags);
            var stripped = InkedPixels (TextFormatFlags.HidePrefix);

            if (expectUnderline)
                Assert.True (withCue > stripped);
            else
                Assert.Equal (stripped, withCue);
        }

        [Fact]
        public void Draw_does_not_throw_for_a_trailing_prefix ()
        {
            // "&" with nothing after it names no mnemonic; the index must not run off the end.
            using var bitmap = new Bitmap (200, 60);
            using var g = Graphics.FromImage (bitmap);
            using var brush = new SolidBrush (System.Drawing.Color.Black);

            g.DrawString ("Cancel&", new Font ("Arial", 12f), brush,
                new System.Drawing.RectangleF (0, 0, 200, 60),
                new StringFormat { HotkeyPrefix = Majorsilence.Forms.Drawing.Text.HotkeyPrefix.Show });
        }
    }
}
