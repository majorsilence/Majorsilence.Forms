using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: a GroupBox's DisplayRectangle -- what Dock=Fill children fill -- starts
    // BELOW the caption band (font height + Padding.Top). Without the inset, a docked grid slides
    // up under the caption and the caption text paints over the grid's header row.
    public class GroupBoxDisplayRectangleTests
    {
        [Fact]
        public void DisplayRectangle_starts_below_the_caption_band ()
        {
            using var box = new GroupBox { Width = 300, Height = 200, Text = "Details" };

            var display = box.DisplayRectangle;

            Assert.True (display.Y >= box.CaptionHeight,
                $"display top {display.Y} must clear the caption band ({box.CaptionHeight}px)");
            Assert.True (display.Height <= 200 - box.CaptionHeight);
        }

        [Fact]
        public void Caption_inset_grows_with_the_font ()
        {
            using var small = new GroupBox { Width = 300, Height = 200, Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 8.25f) };
            using var large = new GroupBox { Width = 300, Height = 200, Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f) };

            Assert.True (large.DisplayRectangle.Y > small.DisplayRectangle.Y,
                "a larger caption font must reserve a taller caption band");
        }

        [Fact]
        public void Docked_child_fills_below_the_caption ()
        {
            using var box = new GroupBox { Width = 300, Height = 200, Text = "Details", Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f) };
            var child = new Panel { Dock = DockStyle.Fill };
            box.Controls.Add (child);

            box.PerformLayout ();

            Assert.True (child.Top >= box.CaptionHeight,
                $"docked child top {child.Top} must sit below the caption band ({box.CaptionHeight}px)");
            Assert.True (child.Bottom <= box.Height);
        }
    }
}
