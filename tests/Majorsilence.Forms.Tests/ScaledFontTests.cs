using Xunit;
using RectangleF = System.Drawing.RectangleF;
using CharacterRange = Majorsilence.Forms.Drawing.CharacterRange;
using MSStringFormat = Majorsilence.Forms.Drawing.StringFormat;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// On a surface that is not 96 DPI text is measured and drawn at a scaled size. That font is now kept with the
    /// <see cref="Font"/> rather than built, and disposed, by every call. These tests check it behaves the same
    /// from call to call and after other work in between.
    /// </summary>
    public class ScaledFontTests
    {
        private static float Width (Graphics g, Font font, string text)
        {
            var format = new MSStringFormat ();
            format.SetMeasurableCharacterRanges (new[] { new CharacterRange (0, text.Length) });
            var regions = g.MeasureCharacterRanges (text, font, new RectangleF (0, 0, float.MaxValue, float.MaxValue), format);
            return regions[0].GetBounds (g).Width;
        }

        private static Graphics At (float dpi)
        {
            var bitmap = new Bitmap (600, 200);
            bitmap.SetResolution (dpi, dpi);
            return Graphics.FromImage (bitmap);
        }

        [Fact]
        public void Repeated_measures_on_a_300_dpi_surface_agree ()
        {
            using var g = At (300f);
            using var font = new Font ("Arial", 12f);

            var first = Width (g, font, "Quarterly results");
            for (var i = 0; i < 5; i++)
                Assert.Equal (first, Width (g, font, "Quarterly results"));
        }

        [Fact]
        public void A_higher_resolution_measures_wider_and_a_lower_one_narrower ()
        {
            using var font = new Font ("Arial", 12f);
            using var g96 = At (96f);
            using var g300 = At (300f);
            using var g72 = At (72f);

            var w96 = Width (g96, font, "Quarterly results");
            var w300 = Width (g300, font, "Quarterly results");
            var w72 = Width (g72, font, "Quarterly results");

            Assert.True (w300 > w96 * 2.5f && w300 < w96 * 3.7f, $"300 dpi {w300} vs 96 dpi {w96}");
            Assert.True (w72 < w96, $"72 dpi {w72} vs 96 dpi {w96}");

            // going back to an earlier resolution gives the earlier answer: the kept font follows the surface
            Assert.Equal (w96, Width (g96, font, "Quarterly results"));
            Assert.Equal (w300, Width (g300, font, "Quarterly results"));
        }

        [Fact]
        public void The_font_still_draws_after_measuring_at_another_resolution ()
        {
            using var font = new Font ("Arial", 12f);
            using var g300 = At (300f);
            using var g96 = At (96f);

            Width (g300, font, "abc");
            g96.Clear (System.Drawing.Color.White);
            g96.DrawString ("abc", font, Majorsilence.Forms.Drawing.Brushes.Black, 5f, 5f);   // must not throw on a disposed font
            Width (g300, font, "abc");
        }
    }
}
