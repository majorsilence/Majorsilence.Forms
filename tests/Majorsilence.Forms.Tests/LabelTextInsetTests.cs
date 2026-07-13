using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GDI parity: label text carries a small horizontal bearing inset -- the first glyph never
    // starts flush at the control's left edge. Designer layouts rely on it: a label placed at a
    // slightly negative X still shows its first glyph fully. Without the inset, such labels
    // rendered with the first character shaved.
    public class LabelTextInsetTests
    {
        [Fact]
        public void Left_aligned_label_text_does_not_start_flush_at_the_edge ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var panel = new Panel { Left = 0, Top = 0, Width = 280, Height = 160, BackColor = System.Drawing.Color.White };
            var label = new Label {
                Left = 10, Top = 10, Width = 200, Height = 20,
                Text = "DocType",
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.Black,
            };
            panel.Controls.Add (label);
            form.Controls.Add (panel);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            // Inspect the label's own back buffer: the leftmost text pixel must sit at least the
            // 2px bearing inset from the edge.
            var mi = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bmp = (SkiaSharp.SKBitmap) mi.Invoke (label, null)!;

            var firstInkColumn = -1;
            for (var x = 0; x < bmp.Width && firstInkColumn < 0; x++)
                for (var y = 0; y < bmp.Height; y++) {
                    var c = bmp.GetPixel (x, y);
                    if (c.Alpha > 0 && c.Red < 128 && c.Green < 128 && c.Blue < 128) {
                        firstInkColumn = x;
                        break;
                    }
                }

            Assert.True (firstInkColumn >= 2,
                $"label text must start at least 2px in from the left edge (first ink column: {firstInkColumn})");
        }
    }
}
