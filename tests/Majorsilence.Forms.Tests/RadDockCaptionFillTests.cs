using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // What a docked tool strip's caption is filled with. Measured on Telerik UI for WinForms 2026.2.520
    // (probes/wfdockcaption), down a 24 high caption clear of its text:
    //
    //   y0        #DCEAFD
    //   y1..y22   #B7D5FA -> #ABCAF0
    //   y23       #DCEAFD
    //
    // It was filled flat with Theme.ControlMidColor, a grey, so a docked panel's caption had none of
    // Telerik's tint. The caption only exists in ported mode, so every test here sets the application font.
    [Collection ("Headless")]
    public class RadDockCaptionFillTests
    {
        private static readonly SKColor Edge = new (0xDC, 0xEA, 0xFD);
        private static readonly SKColor FaceTop = new (0xB7, 0xD5, 0xFA);
        private static readonly SKColor FaceBottom = new (0xAB, 0xCA, 0xF0);

        private static SKBitmap BackBuffer (Control control)
        {
            var method = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SKBitmap) method.Invoke (control, null)!;
        }

        // A dock with one tool window docked on the left, painted, with the caption's band in the strip's
        // own device pixels and an x clear of both the caption's text and its buttons.
        private static (Form Form, ToolTabStrip Strip, Rectangle Band, int X) Docked ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (700, 400) };
            var dock = new RadDock { Location = new Point (0, 0), Size = new Size (700, 400) };
            form.Controls.Add (dock);
            dock.DockWindow (new DocumentWindow { Name = "doc", Text = "Documents" }, DockPosition.Fill);

            var tool = new ToolWindow { Name = "twResults", Text = "TCA Results" };
            dock.DockWindow (tool, DockPosition.Left);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var strip = (ToolTabStrip) tool.Parent!;
            var area = DockStrip.CaptionArea (strip)!.Value;

            Rectangle Device (Rectangle r) => new (
                strip.LogicalToDeviceUnits (r.Left), strip.LogicalToDeviceUnits (r.Top),
                strip.LogicalToDeviceUnits (r.Width), strip.LogicalToDeviceUnits (r.Height));

            var band = Device (area);

            // The caption's text starts 8 in from the left and its buttons end 19 from the right; halfway
            // along is clear of both for a caption this short.
            return (form, strip, band, band.Left + band.Width / 2);
        }

        [Fact]
        public void A_docked_caption_has_Telerik_s_pale_line_along_its_top_and_bottom ()
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

            try {
                var (form, strip, band, x) = Docked ();

                using (form) {
                    var bitmap = BackBuffer (strip);

                    Assert.Equal (Edge, bitmap.GetPixel (x, band.Top));
                    Assert.Equal (Edge, bitmap.GetPixel (x, band.Bottom - 1));
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        [Fact]
        public void A_docked_caption_is_filled_with_Telerik_s_graded_blue ()
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));

            try {
                var (form, strip, band, x) = Docked ();

                using (form) {
                    var bitmap = BackBuffer (strip);
                    var px = strip.LogicalToDeviceUnits (1);

                    var top = bitmap.GetPixel (x, band.Top + px);
                    var bottom = bitmap.GetPixel (x, band.Bottom - 1 - px);

                    Assert.True (Near (top, FaceTop), $"top of the caption was {top}, expected about {FaceTop}");
                    Assert.True (Near (bottom, FaceBottom), $"bottom of the caption was {bottom}, expected about {FaceBottom}");

                    // Graded, not flat: the bottom is the darker end.
                    Assert.True (top.Blue > bottom.Blue, $"{top} should be lighter than {bottom}");
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static bool Near (SKColor a, SKColor b, int tolerance = 6)
            => Math.Abs (a.Red - b.Red) <= tolerance
            && Math.Abs (a.Green - b.Green) <= tolerance
            && Math.Abs (a.Blue - b.Blue) <= tolerance;
    }
}
