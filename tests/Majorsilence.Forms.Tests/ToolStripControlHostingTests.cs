using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // ToolStripControlHost held a Control reference and forwarded properties to it, but never parented the
    // control to anything or gave it a position -- so the control was never displayed. What showed instead
    // was the item's Text, drawn by the strip's renderer, which reads as a stray label rather than as an
    // unhosted control. This is the whole contract of the type, and it covers ToolStripTextBox and
    // ToolStripComboBox too.
    public class ToolStripControlHostingTests
    {
        [Fact]
        public void The_hosted_control_becomes_a_child_of_the_strip_and_takes_the_items_bounds ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (400, 200) };
            var strip = new StatusStrip ();
            var hosted = new Button { Size = new Size (90, 20) };
            strip.Items.Add (new ToolStripControlHost (hosted));
            form.Controls.Add (strip);

            form.Show ();
            HeadlessRenderer.CapturePng (form);   // items are laid out inside the strip's paint

            Assert.Same (strip, hosted.Parent);
            Assert.Contains (hosted, strip.Controls.Cast<Control> ());
            Assert.Equal (strip.Items[0].Bounds, hosted.Bounds);
        }

        [Fact]
        public void The_strip_does_not_also_draw_the_items_text_under_the_hosted_control ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (400, 200) };
            var strip = new StatusStrip ();

            // A hosted control with a TRANSPARENT background is what exposed this: the renderer's text
            // showed straight through it, so a hosted slider had "kryptonSlider1" printed across its track.
            var hosted = new Panel { Size = new Size (140, 20), BackColor = Color.Transparent };
            var host = new ToolStripControlHost (hosted) { Text = "kryptonSlider1" };
            strip.Items.Add (host);
            form.Controls.Add (strip);

            form.Show ();
            var png = HeadlessRenderer.CapturePng (form);

            using var bitmap = SkiaSharp.SKBitmap.Decode (png);
            var bounds = host.Bounds;
            var inkFound = false;

            // Any dark pixel inside the item's rectangle would be rendered glyphs: neither the strip's
            // background nor a transparent panel puts anything dark there.
            for (var y = bounds.Top; y < bounds.Bottom && !inkFound; y++)
                for (var x = bounds.Left; x < bounds.Right; x++) {
                    var c = bitmap.GetPixel (x, y);

                    if (c.Red < 100 && c.Green < 100 && c.Blue < 100) {
                        inkFound = true;
                        break;
                    }
                }

            Assert.False (inkFound, "The strip drew the host item's Text inside the hosted control's area.");
        }

        // Regression: the strip laid a hosted editor out from MenuItem.GetPreferredSize, which measures
        // the item's Text -- empty for a control host -- so a ToolStripTextBox given a 250px width in the
        // designer collapsed to roughly its padding. ReportDesigner's "fx" expression bar showed as a
        // sliver a few characters wide.
        [Fact]
        public void A_hosted_editor_keeps_the_width_it_was_given ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (600, 200) };
            var strip = new ToolStrip ();
            var fx = new ToolStripTextBox { Size = new Size (250, 25) };
            strip.Items.Add (new ToolStripLabel { Text = "fx" });
            strip.Items.Add (fx);
            form.Controls.Add (strip);

            form.Show ();
            HeadlessRenderer.CapturePng (form);

            Assert.Equal (250, fx.Bounds.Width);
            Assert.Equal (250, fx.TextBox.Width);
        }
    }
}
