using System.Reflection;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // A RadGridView's headers are Telerik's, with their filter buttons, under the upstream look as without it. In an app
    // that chose its font they were laid out as the WinForms grid's DataGridViewColumnHeaderCell: the caption ran on
    // across the funnel, a "VendCode" header reading "VendCo" funnel "de".
    [Collection ("Headless")]
    public class RadGridViewUpstreamHeaderTests
    {
        private static (SKBitmap Painted, System.Drawing.Rectangle Funnel) Header (string caption)
        {
            HeadlessRenderer.Use ();
            using var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new System.Drawing.Size (300, 120) };
            var grid = new RadGridView { Location = new System.Drawing.Point (0, 0), Size = new System.Drawing.Size (300, 120), ShowGroupPanel = false, EnableFiltering = true };
            grid.MasterTemplate.ShowFilteringRow = false;
            grid.MasterTemplate.ShowHeaderCellButtons = true;
            grid.Columns.Add (new GridViewTextBoxColumn { Name = "a", HeaderText = caption, Width = 100 });
            form.Controls.Add (grid);
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var painted = ((SKBitmap) buffer.Invoke (grid, null)!).Copy ();
            return (painted, grid.FilterGlyphRects[0]);
        }

        [Fact]
        public void A_long_caption_stops_short_of_the_filter_button ()
        {
            Application.SetDefaultFont (new DFont ("Microsoft Sans Serif", 12f));

            try {
                var (blank, funnel) = Header (string.Empty);
                var (captioned, at) = Header ("WWWWWWWWWWWW");

                Assert.Equal (funnel, at);

                // The funnel and a pixel either side of it are as they are under no caption at all.
                var differ = 0;
                for (var y = funnel.Top; y < funnel.Bottom; y++)
                    for (var x = funnel.Left - 1; x <= funnel.Right; x++)
                        if (blank.GetPixel (x, y) != captioned.GetPixel (x, y))
                            differ++;

                Assert.Equal (0, differ);
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
