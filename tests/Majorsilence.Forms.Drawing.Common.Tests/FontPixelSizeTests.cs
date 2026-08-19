using Majorsilence.Forms.Drawing;
using Xunit;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // Font.Size is expressed in Font.Unit -- Point by default, as in GDI+ -- but SkiaSharp measures text in
    // pixels. Handing the raw number to Skia treated points as pixels, so every piece of text in the library
    // rendered about a quarter too small, and so did every dimension derived from text metrics: a Krypton
    // ribbon's tab strip came out 19px instead of ~25 and its button captions overlapped their images.
    public class FontPixelSizeTests
    {
        [Fact]
        public void A_point_sized_font_reports_a_larger_line_height_than_its_point_size ()
        {
            // 9pt is 12px at 96 DPI before line spacing, so a real face must exceed the point size.
            var font = new Font ("Arial", 9f);

            Assert.True (font.GetHeight () > 9f,
                $"line height {font.GetHeight ()} is not greater than the 9pt em size -- points are being treated as pixels");
        }

        [Theory]
        [InlineData (GraphicsUnit.Point, 72f, 96f)]     // 72pt = 1 inch = 96px
        [InlineData (GraphicsUnit.Pixel, 96f, 96f)]     // already pixels
        [InlineData (GraphicsUnit.Inch, 1f, 96f)]
        [InlineData (GraphicsUnit.Millimeter, 25.4f, 96f)]
        [InlineData (GraphicsUnit.Document, 300f, 96f)]
        public void The_unit_decides_the_pixel_size (GraphicsUnit unit, float size, float expectedPixels)
        {
            var font = new Font ("Arial", size, FontStyle.Regular, unit);

            // Compared through the public surface: a font whose pixel size is P must measure the same line
            // height as a Pixel-unit font of size P, since that is the number Skia is given either way.
            var equivalent = new Font ("Arial", expectedPixels, FontStyle.Regular, GraphicsUnit.Pixel);

            Assert.Equal (equivalent.GetHeight (), font.GetHeight (), precision: 2);
        }

        [Fact]
        public void Doubling_the_point_size_doubles_the_line_height ()
        {
            var small = new Font ("Arial", 10f);
            var large = new Font ("Arial", 20f);

            Assert.Equal (small.GetHeight () * 2f, large.GetHeight (), precision: 1);
        }
    }
}
