using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: ForeColor is AMBIENT, exactly as BackColor is (see AmbientBackColorTests).
    // Setting it once on a container is the normal way to colour a themed strip -- every caption
    // inside picks it up. Only BackColor resolved that way here, so a dark panel that set
    // ForeColor = White got black captions on its own dark background, i.e. invisible ones.
    public class AmbientForeColorTests
    {
        [Fact]
        public void Child_inherits_parent_ForeColor_when_unset ()
        {
            using var panel = new Panel { ForeColor = System.Drawing.Color.White };
            var button = new Button ();
            var label = new Label ();
            panel.Controls.Add (button);
            panel.Controls.Add (label);

            Assert.Equal (System.Drawing.Color.White.ToArgb (), button.ForeColor.ToArgb ());
            Assert.Equal (System.Drawing.Color.White.ToArgb (), label.ForeColor.ToArgb ());
        }

        [Fact]
        public void Explicit_ForeColor_still_wins ()
        {
            using var panel = new Panel { ForeColor = System.Drawing.Color.White };
            var label = new Label { ForeColor = System.Drawing.Color.Red };
            panel.Controls.Add (label);

            Assert.Equal (System.Drawing.Color.Red.ToArgb (), label.ForeColor.ToArgb ());
        }

        [Fact]
        public void Ambient_resolution_walks_nested_parents ()
        {
            using var outer = new Panel { ForeColor = System.Drawing.Color.FromArgb (10, 200, 30) };
            var inner = new Panel ();
            var label = new Label ();
            outer.Controls.Add (inner);
            inner.Controls.Add (label);

            Assert.Equal (outer.ForeColor.ToArgb (), label.ForeColor.ToArgb ());
        }

        // The property agreeing is not the point -- what is painted is. Drawing used to read the
        // style chain directly while the property read the ambient chain, so the two could disagree
        // and only the pixels told the truth.
        [Fact]
        public void Inherited_ForeColor_is_the_colour_actually_painted ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var panel = new Panel {
                Left = 0, Top = 0, Width = 280, Height = 160,
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.White,
            };
            var label = new Label { Left = 10, Top = 10, Width = 200, Height = 30, Text = "Caption" };
            panel.Controls.Add (label);
            form.Controls.Add (panel);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap) buffer.Invoke (label, null)!;

            var lightPixels = 0;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++) {
                    var c = bitmap.GetPixel (x, y);
                    if (c.Alpha > 0 && c.Red > 200 && c.Green > 200 && c.Blue > 200)
                        lightPixels++;
                }

            Assert.True (lightPixels > 0, "caption should be painted in the panel's white ForeColor, not the default dark one");
        }
    }
}
