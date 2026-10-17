using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;
using DStyle = Majorsilence.Forms.Drawing.FontStyle;

namespace Majorsilence.Forms.Tests
{
    [Collection ("Headless")]
    public class FontDecorationTests
    {
        // The dark pixels a label paints for its caption in the given style.
        private static int Ink (DStyle style)
        {
            using var deterministic = HeadlessRenderer.UseDeterministic ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 100) };
            var label = new Label { Location = new Point (10, 10), Size = new Size (200, 30), Text = "Frequency Period" };
            label.Font = new DFont (label.Font.Name, 12f, style);
            form.Controls.Add (label);
            form.Show ();

            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 100));
            var dark = 0;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y) is var c && c.Red < 100 && c.Green < 100 && c.Blue < 100)
                        dark++;

            return dark;
        }

        [Theory]
        [InlineData (DStyle.Underline)]
        [InlineData (DStyle.Strikeout)]
        public void A_controls_underlined_or_struck_out_font_is_drawn_so (DStyle style)
        {
            // As upstream draws a control's text in such a font. The typeface carries only the family, weight
            // and slant, so a Label in an underlined font (a designer's column captions) drew plain.
            var plain = Ink (DStyle.Regular);
            var decorated = Ink (style);

            // A line under (or through) the whole caption: at least most of its width in extra ink.
            Assert.True (decorated - plain > 60, $"plain {plain} px, {style} {decorated} px");
        }
    }
}
