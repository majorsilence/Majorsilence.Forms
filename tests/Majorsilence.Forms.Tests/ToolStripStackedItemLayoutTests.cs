using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// A strip item with its image above (or below) its text is laid out as upstream's
// ButtonBaseAdapter.LayoutOptions.LayoutTextAndImage lays it out: in the item less its 2px border, the
// image and text halves split and stretched to the field's edges, each aligned in its own half, and
// the text pulled up against an image above it.
public class ToolStripStackedItemLayoutTests
{
    // Measured from WinForms (.NET 10): a 150 x 80 ToolStripButton at y = 1 in an 83px strip, its 24px
    // image above a Microsoft Sans Serif 12pt caption (20px), ImageAlign MiddleCenter. The field is
    // the item's 2px inside its border: y 3, 76 tall.
    [Theory]
    [InlineData (ContentAlignment.MiddleCenter, 11)]
    [InlineData (ContentAlignment.TopCenter, 11)]
    [InlineData (ContentAlignment.BottomCenter, 19)]
    public void An_image_above_text_is_placed_where_upstream_places_it (ContentAlignment text_align, int image_top)
    {
        var field = new Rectangle (2, 3, 146, 76);
        var (image, text) = ToolBarRenderer.StackedLayout (field, new Size (24, 24), new Size (70, 20),
            ContentAlignment.MiddleCenter, text_align, TextImageRelation.ImageAboveText);

        Assert.Equal (new Rectangle (63, image_top, 24, 24), image);
        // The caption starts where the image ends, as tall as it measured.
        Assert.Equal (image.Bottom, text.Top);
        Assert.Equal (20, text.Height);
    }

    [Fact]
    public void A_toolbar_button_draws_its_image_in_the_upper_part_of_the_button ()
    {
        try {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            var magenta = Color.FromArgb (255, 0, 255);
            using var image = new Majorsilence.Forms.Drawing.Bitmap (24, 24);
            for (var x = 0; x < 24; x++)
                for (var y = 0; y < 24; y++)
                    image.SetPixel (x, y, magenta);

            // 83px, as upstream sizes a strip round an 80px button (its margin is 1 above, 2 below).
            var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top, AutoSize = false, Height = 83 };
            var button = new ToolStripButton {
                Image = image, Text = "Inventory",
                ImageScaling = ToolStripItemImageScaling.None, TextImageRelation = TextImageRelation.ImageAboveText,
                ImageAlign = ContentAlignment.MiddleCenter, TextAlign = ContentAlignment.MiddleCenter,
            };
            strip.Items.Add (button);
            button.AutoSize = false;
            button.Size = new Size (150, 80);
            using var form = new Form { ClientSize = new Size (400, 200) };
            form.Controls.Add (strip);
            strip.PerformLayout ();
            using var bitmap = PaintSurface.Render (strip);

            var top = -1;
            for (var y = 0; y < bitmap.Height && top < 0; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Red > 240 && c.Green < 20 && c.Blue > 240) { top = y; break; }
                }

            // Where upstream's layout puts it for the caption as this backend measures it: the image's half of
            // the field runs from the field's top to the bottom of the image in the centred pair, and the
            // image is centred in that half.
            var (typeface, size) = StripItemFont.Resolve (button, strip);
            var caption = (int) System.Math.Ceiling (TextMeasurer.MeasureText (button.Text, typeface, strip.LogicalToDeviceUnits (size)).Height);
            var bounds = button.DeviceBounds;
            var field_top = bounds.Top + 2;
            var field_height = bounds.Height - 4;
            var pair_top = field_top + (field_height - 24 - caption) / 2;
            var expected = field_top + (pair_top - field_top) / 2;

            Assert.Equal (expected, top);

            // Centring image and caption as one block, as the strip used to, puts the image lower.
            var centred = bounds.Top + (bounds.Height - (24 + strip.LogicalToDeviceUnits (4) + caption)) / 2;
            Assert.True (top < centred, $"image top {top} should be above the centred stack's {centred}");
        } finally {
            Application.SetDefaultFont (null!);
        }
    }
}
