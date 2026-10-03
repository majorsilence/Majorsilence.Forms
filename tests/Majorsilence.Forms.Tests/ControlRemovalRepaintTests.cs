using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Removing a child has to repaint the space it occupied. A parent paints into a cached back buffer that only an Invalidate () rebuilds, and
    // taking a child out of Controls rebuilt nothing, so the removed control's pixels stayed on screen -- and in every later frame -- until
    // something else happened to invalidate the parent (#370). WinForms invalidates the parent when a child leaves it.
    [Collection ("Headless")]
    public class ControlRemovalRepaintTests
    {
        private static readonly Color ChildColour = Color.FromArgb (255, 255, 0, 0);

        // A white form holding a white panel holding a red 60x40 panel; (50, 50) is inside the red one.
        private static (Form Form, Panel Parent, Panel Child) Build ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (300, 200), FormBorderStyle = FormBorderStyle.None, BackColor = Color.White };
            var parent = new Panel { Location = new Point (10, 10), Size = new Size (200, 100), BackColor = Color.White };
            var child = new Panel { Location = new Point (20, 20), Size = new Size (60, 40), BackColor = ChildColour };
            parent.Controls.Add (child);
            form.Controls.Add (parent);
            form.Show ();
            return (form, parent, child);
        }

        private static SKColor Sample (Form form)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
            var scale = bitmap.Width / 300f;
            return bitmap.GetPixel ((int)(50 * scale), (int)(50 * scale));
        }

        [Fact]
        public void Remove_repaints_where_the_child_was ()
        {
            var (form, parent, child) = Build ();
            using var _ = form;
            Assert.Equal (new SKColor (255, 0, 0), Sample (form));

            parent.Controls.Remove (child);

            Assert.Equal (SKColors.White, Sample (form));
        }

        [Fact]
        public void RemoveAt_repaints_where_the_child_was ()
        {
            var (form, parent, _) = Build ();
            using var _ = form;
            Assert.Equal (new SKColor (255, 0, 0), Sample (form));

            parent.Controls.RemoveAt (0);

            Assert.Equal (SKColors.White, Sample (form));
        }

        [Fact]
        public void Clear_repaints_where_the_children_were ()
        {
            var (form, parent, _) = Build ();
            using var _ = form;
            Assert.Equal (new SKColor (255, 0, 0), Sample (form));

            parent.Controls.Clear ();

            Assert.Equal (SKColors.White, Sample (form));
        }

        [Fact]
        public void Disposing_a_parented_child_repaints_where_it_was ()
        {
            var (form, _, child) = Build ();
            using var _ = form;
            Assert.Equal (new SKColor (255, 0, 0), Sample (form));

            child.Dispose ();

            Assert.Equal (SKColors.White, Sample (form));
        }
    }
}
