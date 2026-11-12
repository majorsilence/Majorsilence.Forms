using System;
using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // How the pager marks the page you are on.
    //
    // Measured on Telerik UI for WinForms 2026.2.520 under the default theme (probes/wfpagerfill), down a
    // 24px tall button clear of its digit: a #FFBE6A border, a #FEF5C3 highlight inside it, an upper face
    // from #FFCD91 to #FFBD71, a fold at #FFB862/#FFB243, a lower face from #FFB748 to #FEDB68, and black
    // text. The button was filled from DataGridView.DefaultSelectionStyle instead -- SystemColors.Highlight,
    // white on blue -- so every migrated application's pager marked the current page in the system colour.
    [Collection ("Headless")]
    public class RadGridViewPagerCurrentPageTests
    {
        private static readonly SKColor SystemHighlight = SystemColors.Highlight.ToSKColor ();

        private static (Form Form, RadGridView Grid) Grid (string themeName = "")
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (400, 300) };
            var grid = new RadGridView {
                Location = new Point (0, 0), Size = new Size (400, 300), AllowAddNewRow = false,
                EnablePaging = true, PageSize = 4, ThemeName = themeName,
            };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("A");
            for (var i = 0; i < 24; i++)
                table.Rows.Add ("");
            grid.DataSource = table;

            form.Show ();
            return (form, grid);
        }

        private static SKBitmap Capture (Form form) => SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));

        // The button of the page the grid is on.
        private static Rectangle CurrentPage (RadGridView grid)
            => grid.LayoutPagingPanel ().Pages.Single (p => p.Page == grid.CurrentPageIndex).Bounds;

        // A pixel down the left of the button, clear of the digit centred in it. The fraction walks the
        // button's height, so the same point is sampled at either MF_HEADLESS_SCALE.
        private static SKColor At (SKBitmap bitmap, RadGridView grid, Rectangle button, double fraction)
            => bitmap.GetPixel (
                button.Left + grid.LogicalToDeviceUnits (3),
                button.Top + (int) Math.Round (button.Height * fraction));

        [Fact]
        public void The_current_page_is_not_marked_in_the_system_highlight ()
        {
            var (form, grid) = Grid ();

            using (form) {
                var bitmap = Capture (form);
                var button = CurrentPage (grid);

                for (var y = button.Top; y < button.Bottom; y++)
                    for (var x = button.Left; x < button.Right; x++)
                        Assert.NotEqual (SystemHighlight, bitmap.GetPixel (x, y));
            }
        }

        [Fact]
        public void The_current_page_is_marked_in_Telerik_amber ()
        {
            var (form, grid) = Grid ();

            using (form) {
                var bitmap = Capture (form);
                var face = At (bitmap, grid, CurrentPage (grid), 0.25);

                // Everything across the measured face is a saturated amber: red at full, green in the middle,
                // blue low. The system highlight (0,120,215) is none of those.
                Assert.True (face.Red >= 0xF0, $"red {face.Red:X2}");
                Assert.InRange (face.Green, 0xA8, 0xD5);
                Assert.InRange (face.Blue, 0x30, 0x9A);
            }
        }

        // The two faces meet at a fold about 58% down, which is what makes the button look raised. A flat
        // fill has the same colour all the way down.
        [Fact]
        public void The_current_page_button_has_two_faces_meeting_at_a_fold ()
        {
            var (form, grid) = Grid ();

            using (form) {
                var bitmap = Capture (form);
                var button = CurrentPage (grid);

                var upper = At (bitmap, grid, button, 0.2);
                var fold = At (bitmap, grid, button, 0.55);
                var lower = At (bitmap, grid, button, 0.9);

                Assert.True (upper.Blue > fold.Blue, $"upper {upper} should be lighter than the fold {fold}");
                Assert.True (lower.Blue > fold.Blue, $"lower {lower} should be lighter than the fold {fold}");
            }
        }

        // A named theme is unaffected: it fills the button flat in its own accent, as it did before.
        [Fact]
        public void A_named_theme_still_marks_the_current_page_in_its_accent ()
        {
            try {
                var (form, grid) = Grid ("Fluent");

                using (form) {
                    var bitmap = Capture (form);
                    var accent = grid.TelerikTheme!.Accent;

                    Assert.Equal (accent, At (bitmap, grid, CurrentPage (grid), 0.5));
                }
            } finally {
                ThemeResolutionService.ApplicationThemeName = string.Empty;
                ThemeResolutionService.ApplicationAccentColor = Color.Empty;
            }
        }
    }
}
