using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;
using DFont = Majorsilence.Forms.Drawing.Font;

namespace Majorsilence.Forms.Tests
{
    // A paged RadGridView with no rows has no page buttons. Measured on Telerik (UI for WinForms 2026.2.520), a 900px grid in
    // Segoe UI 12pt whose source had columns and no rows: first, previous and "..." where they always are, then at once a
    // separator, the second "...", next and last (at 102, 131 and 163 in the panel), the rest of the strip empty, and the
    // second "...", next and last drawn enabled. Here it showed a "1" button stretched across the strip, with next and last
    // disabled at its far end.
    [Collection ("Headless")]
    public class RadGridViewPagingPanelWithoutRowsTests
    {
        private static (Form Form, RadGridView Grid) Grid (int rows)
        {
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (900, 400) };
            var grid = new RadGridView { Location = new Point (0, 0), Size = new Size (900, 400), Font = new DFont ("Segoe UI", 12f), ShowGroupPanel = false };
            form.Controls.Add (grid);
            grid.MasterTemplate.EnablePaging = true;

            var table = new DataTable ();
            table.Columns.Add ("Code");
            for (var i = 0; i < rows; i++)
                table.Rows.Add ("R" + i);
            grid.DataSource = table;

            form.Show ();
            return (form, grid);
        }

        [Fact]
        public void No_rows_no_page_buttons_and_the_rest_follows_on ()
        {
            var (form, grid) = Grid (rows: 0);

            using (form) {
                var layout = grid.LayoutPagingPanel ();
                int X (Rectangle r) => grid.DeviceToLogicalUnits (r.Left - layout.Band.Left);
                int Right (Rectangle r) => grid.DeviceToLogicalUnits (r.Right - layout.Band.Left);

                Assert.Empty (layout.Pages);

                // 3px, a 2px separator and 3px between buttons, as everywhere in the strip.
                Assert.Equal (Right (layout.FastBack) + 16, X (layout.FastForward));
                Assert.Equal (Right (layout.FastForward) + 8, X (layout.Next));
                Assert.Equal (Right (layout.Next) + 8, X (layout.Last));
                Assert.Equal (6, layout.Separators.Count);

                if (System.OperatingSystem.IsWindows ()) {
                    Assert.Equal (102, X (layout.FastForward));
                    Assert.Equal (131, X (layout.Next));
                    Assert.Equal (163, X (layout.Last));
                }
            }
        }

        [Fact]
        public void A_page_of_rows_keeps_its_page_button_and_the_buttons_at_the_end ()
        {
            var (form, grid) = Grid (rows: 1);

            using (form) {
                var layout = grid.LayoutPagingPanel ();

                Assert.Single (layout.Pages);
                Assert.Equal (layout.PageLabel.Left - grid.LogicalToDeviceUnits (24), layout.Last.Left);
            }
        }

        // The darkest pixel in a part of the panel.
        private static int Darkest (Form form, RadGridView grid, Rectangle device)
        {
            using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 900, 400));
            var scale = bitmap.Width / 900f;
            var logical = grid.DeviceToLogicalUnits (device);
            var darkest = int.MaxValue;

            for (var x = logical.Left; x < logical.Right; x++)
                for (var y = logical.Top; y < logical.Bottom; y++) {
                    var c = bitmap.GetPixel ((int) (x * scale), (int) (y * scale));
                    darkest = System.Math.Min (darkest, c.Red + c.Green + c.Blue);
                }

            return darkest;
        }

        [Fact]
        public void No_rows_shows_next_and_last_enabled ()
        {
            int Next (int rows)
            {
                var (form, grid) = Grid (rows);
                using (form)
                    return Darkest (form, grid, grid.LayoutPagingPanel ().Next);
            }

            // One page: next and last cannot move, so they are greyed. No rows: Telerik draws them enabled.
            Assert.True (Next (0) < Next (1), $"next with no rows {Next (0)}, with a page {Next (1)}");
        }
    }
}
