using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;
namespace Majorsilence.Forms.Tests;
[Collection ("Headless")]
public class ZzTmpMn {
    private static string Ink (string text) {
        HeadlessRenderer.Use ();
        Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
        try {
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (200, 60) };
            var b = new Button { Text = text, Location = new Point (10, 10), Size = new Size (97, 28), Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f), UseVisualStyleBackColor = true };
            form.Controls.Add (b); form.Show ();
            using var bmp = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 200, 60));
            var s = bmp.Width / 200f; int minx = 999, maxx = -1, miny = 999, maxy = -1;
            for (var x = (int) (12 * s); x < (int) (105 * s); x++) for (var y = (int) (12 * s); y < (int) (36 * s); y++) { var p = bmp.GetPixel (x, y); if (p.Red + p.Green + p.Blue < 300) { minx = System.Math.Min (minx, x); maxx = System.Math.Max (maxx, x); miny = System.Math.Min (miny, y); maxy = System.Math.Max (maxy, y); } }
            return $"'{text}': ink x {minx}-{maxx} y {miny}-{maxy} textWidth={TextRenderer.MeasureText (text.Replace ("&", ""), b.Font).Width}";
        } finally { Application.SetDefaultFont (null!); }
    }
    [Fact] public void Diag () => Assert.Fail (Ink ("Cancel &Add") + " || " + Ink ("Cancel Add"));
}
