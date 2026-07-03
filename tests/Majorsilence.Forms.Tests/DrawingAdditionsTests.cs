using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Covers the GDI+ surface added to Majorsilence.Forms.Drawing: named Brushes/Pens, ColorConverter,
    // SystemIcons, TextureBrush, PathGradientBrush, and BufferedGraphics.
    public class DrawingAdditionsTests
    {
        [Fact]
        public void Brushes_named_color_is_a_SolidBrush_of_that_color ()
        {
            var brush = Assert.IsType<SolidBrush> (Brushes.Red);
            Assert.Equal (System.Drawing.Color.Red.ToArgb (), brush.Color.ToArgb ());
        }

        [Fact]
        public void Brushes_returns_a_cached_instance ()
        {
            Assert.Same (Brushes.CornflowerBlue, Brushes.CornflowerBlue);
        }

        [Fact]
        public void Pens_named_color_is_a_Pen_of_that_color ()
        {
            Assert.Equal (System.Drawing.Color.Blue.ToArgb (), Pens.Blue.Color.ToArgb ());
            Assert.Same (Pens.Blue, Pens.Blue);
        }

        [Fact]
        public void ColorConverter_round_trips_named_color ()
        {
            var converter = new ColorConverter ();
            var text = converter.ConvertTo (null, null, System.Drawing.Color.Red, typeof (string));
            Assert.Equal ("Red", text);
            var back = (System.Drawing.Color) converter.ConvertFrom (null, null, "Red")!;
            Assert.Equal (System.Drawing.Color.Red.ToArgb (), back.ToArgb ());
        }

        [Theory]
        [InlineData ("#FF0000", 255, 0, 0)]
        [InlineData ("0, 128, 255", 0, 128, 255)]
        public void ColorConverter_parses_hex_and_components (string input, int r, int g, int b)
        {
            var color = (System.Drawing.Color) new ColorConverter ().ConvertFrom (null, null, input)!;
            Assert.Equal (System.Drawing.Color.FromArgb (r, g, b).ToArgb (), color.ToArgb ());
        }

        [Fact]
        public void SystemIcons_are_not_null ()
        {
            Assert.NotNull (SystemIcons.Error);
            Assert.NotNull (SystemIcons.Warning);
            Assert.NotNull (SystemIcons.Information);
        }

        [Fact]
        public void TextureBrush_constructs_from_a_bitmap ()
        {
            using var bmp = new Bitmap (8, 8);
            var brush = new TextureBrush (bmp, WrapMode.Tile);
            Assert.Equal (WrapMode.Tile, brush.WrapMode);
        }

        [Fact]
        public void PathGradientBrush_centers_on_the_polygon_bounds ()
        {
            var brush = new PathGradientBrush (new[]
            {
                new System.Drawing.PointF (0, 0),
                new System.Drawing.PointF (10, 0),
                new System.Drawing.PointF (10, 10),
            });
            Assert.Equal (5f, brush.CenterPoint.X);
            Assert.Equal (5f, brush.CenterPoint.Y);
        }

        [Fact]
        public void BufferedGraphics_allocates_and_renders ()
        {
            using var target = new Bitmap (20, 20);
            using var targetGfx = Graphics.FromImage (target);
            using var buffered = BufferedGraphicsManager.Current.Allocate (targetGfx, new System.Drawing.Rectangle (0, 0, 20, 20));
            Assert.NotNull (buffered.Graphics);
            buffered.Render (); // should not throw
        }

        [Fact]
        public void ImageAnimator_reports_no_animation ()
        {
            using var bmp = new Bitmap (4, 4);
            Assert.False (ImageAnimator.CanAnimate (bmp));
        }
    }
}
