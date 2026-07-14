using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GDI parity: the classic radio/checkbox glyph is 13px with a ~5px gap before the text.
    // Designer AutoSize widths are frozen from those metrics (e.g. a "Vertical" radio serialized
    // at 60x17), so a larger glyph box eats into the text area and clips the last characters.
    public class GlyphMetricsParityTests
    {
        [Fact]
        public void Radio_and_checkbox_glyph_metrics_match_classic_widths ()
        {
            var radio = new RadioButtonRenderer ();
            var check = new CheckBoxRenderer ();

            Assert.Equal (13, radio.GlyphSize);
            Assert.Equal (5, radio.GlyphTextPadding);
            Assert.Equal (13, check.GlyphSize);
            Assert.Equal (5, check.GlyphTextPadding);
        }

        [Fact]
        public void Designer_sized_radio_text_is_not_clipped ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Size = new System.Drawing.Size (300, 200) };
            var panel = new Panel { Left = 0, Top = 0, Width = 280, Height = 160, BackColor = System.Drawing.Color.White };
            // Classic-designer-frozen AutoSize bounds for an 8.25pt "Vertical" radio: 60x17.
            var radio = new RadioButton {
                Left = 10, Top = 10, Width = 60, Height = 17,
                Text = "Vertical",
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.Black,
            };
            panel.Controls.Add (radio);
            form.Controls.Add (panel);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            // The text must fit the designer width: some ink has to appear in the LAST quarter of
            // the control (the trailing "al" of "Vertical"), which the oversized glyph box pushed
            // out of bounds before the fix.
            var mi = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bmp = (SkiaSharp.SKBitmap) mi.Invoke (radio, null)!;

            var lastInkColumn = -1;
            for (var x = bmp.Width - 1; x >= 0 && lastInkColumn < 0; x--)
                for (var y = 0; y < bmp.Height; y++) {
                    var c = bmp.GetPixel (x, y);
                    if (c.Alpha > 0 && c.Red < 128 && c.Green < 128 && c.Blue < 128) {
                        lastInkColumn = x;
                        break;
                    }
                }

            Assert.True (lastInkColumn >= 40,
                $"trailing text ink expected past x=40 of the 60px control (last ink column: {lastInkColumn})");
            Assert.True (lastInkColumn <= 58,
                $"text must not run to the very edge (last ink column: {lastInkColumn})");
        }
    }
}
