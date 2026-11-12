using System;
using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Two details of a RadGridView, both measured on Telerik UI for WinForms 2026.2.520
    // (probes/wfgridhints):
    //
    //   the empty group panel's hint   TextAlignment MiddleCenter -- its ink sits 339px from the left of
    //                                  an 898px panel and 340 from the right. It was drawn hard left here.
    //   the sort glyph                 an 8 x 4 triangle one pixel below the top of the header cell and
    //                                  centred across it, in #15428B, in the same place whatever the
    //                                  column's width. It was a 6 x 6 triangle against the right edge,
    //                                  with 16px of the caption's room given up to it.
    [Collection ("Headless")]
    public class RadGridViewHintAndSortGlyphTests
    {
        private static readonly SKColor SortGlyph = new (0x15, 0x42, 0x8B);

        private static (Form Form, RadGridView Grid) Grid (string themeName = "")
        {
            HeadlessRenderer.Use ();

            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 300) };
            var grid = new RadGridView {
                Location = new Point (0, 0), Size = new Size (600, 300),
                AllowAddNewRow = false, ShowGroupPanel = true, ThemeName = themeName,
            };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Asset Description");
            table.Columns.Add ("Qty");
            for (var i = 0; i < 4; i++)
                table.Rows.Add ($"Item {i}", i.ToString ());
            grid.DataSource = table;
            grid.Columns[0]!.Width = 260;
            grid.Columns[1]!.Width = 54;

            form.Show ();
            return (form, grid);
        }

        private static SKBitmap Capture (Form form) => SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 600, 300));

        // The box the pixels close to a colour occupy, or Rectangle.Empty when there are none.
        private static Rectangle BoxOf (SKBitmap bitmap, SKColor colour, Rectangle within, int tolerance = 24)
        {
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;

            for (var y = Math.Max (within.Top, 0); y < Math.Min (within.Bottom, bitmap.Height); y++)
                for (var x = Math.Max (within.Left, 0); x < Math.Min (within.Right, bitmap.Width); x++) {
                    var p = bitmap.GetPixel (x, y);

                    if (Math.Abs (p.Red - colour.Red) > tolerance
                        || Math.Abs (p.Green - colour.Green) > tolerance
                        || Math.Abs (p.Blue - colour.Blue) > tolerance)
                        continue;

                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }

            return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB (left, top, right + 1, bottom + 1);
        }

        [Fact]
        public void The_empty_group_panels_hint_is_centred ()
        {
            var (form, grid) = Grid ();

            using (form) {
                var bitmap = Capture (form);
                var client = grid.DeviceClientRectangle;
                var band = new Rectangle (client.Left, client.Top, client.Width, grid.GroupPanelBandHeight);

                var hint = BoxOf (bitmap, Theme.ForegroundDisabledColor, band);
                Assert.False (hint.IsEmpty, "the hint should be drawn");

                var leftGap = hint.Left - band.Left;
                var rightGap = band.Right - hint.Right;

                // Centred to within a pixel or two of rounding, as Telerik's 339 against 340.
                Assert.True (Math.Abs (leftGap - rightGap) <= grid.LogicalToDeviceUnits (3),
                    $"hint {hint} in band {band}: left gap {leftGap}, right gap {rightGap}");
            }
        }

        [Fact]
        public void The_sort_glyph_is_centred_at_the_top_of_the_header ()
        {
            var (form, grid) = Grid ();

            using (form) {
                grid.SortDescriptors.Add (new SortDescriptor (grid.Columns[0]!.Name, System.ComponentModel.ListSortDirection.Ascending));
                var bitmap = Capture (form);

                var glyph = BoxOf (bitmap, SortGlyph, grid.DeviceClientRectangle);
                Assert.False (glyph.IsEmpty, "the sort glyph should be drawn in Telerik's #15428B");

                var left = grid.GetColumnDeviceLeft (0);
                var centre = left + grid.LogicalToDeviceUnits (260) / 2;

                Assert.True (Math.Abs ((glyph.Left + glyph.Right) / 2 - centre) <= grid.LogicalToDeviceUnits (2),
                    $"glyph {glyph} should be centred on {centre}");
                Assert.InRange (glyph.Width, grid.LogicalToDeviceUnits (6), grid.LogicalToDeviceUnits (10));
                Assert.InRange (glyph.Height, grid.LogicalToDeviceUnits (3), grid.LogicalToDeviceUnits (6));
            }
        }

        // The same place whatever the column's width: Telerik centres it on the cell, so a narrow column's
        // glyph is not pushed against an edge.
        [Fact]
        public void A_narrow_columns_sort_glyph_is_centred_on_that_column ()
        {
            var (form, grid) = Grid ();

            using (form) {
                grid.SortDescriptors.Add (new SortDescriptor (grid.Columns[1]!.Name, System.ComponentModel.ListSortDirection.Descending));
                var bitmap = Capture (form);

                var glyph = BoxOf (bitmap, SortGlyph, grid.DeviceClientRectangle);
                Assert.False (glyph.IsEmpty, "the sort glyph should be drawn");

                var centre = grid.GetColumnDeviceLeft (1) + grid.LogicalToDeviceUnits (54) / 2;

                Assert.True (Math.Abs ((glyph.Left + glyph.Right) / 2 - centre) <= grid.LogicalToDeviceUnits (2),
                    $"glyph {glyph} should be centred on {centre}");
            }
        }

        // A named theme colours the glyph with its own header foreground rather than Telerik's navy.
        [Fact]
        public void A_named_theme_colours_the_glyph_with_its_header_foreground ()
        {
            try {
                var (form, grid) = Grid ("Fluent");

                using (form) {
                    grid.SortDescriptors.Add (new SortDescriptor (grid.Columns[0]!.Name, System.ComponentModel.ListSortDirection.Ascending));
                    var bitmap = Capture (form);

                    var glyph = BoxOf (bitmap, grid.ThemePalette!.HeaderForeground, grid.DeviceClientRectangle);
                    Assert.False (glyph.IsEmpty, "the glyph should take the theme's header foreground");
                }
            } finally {
                ThemeResolutionService.ApplicationThemeName = string.Empty;
                ThemeResolutionService.ApplicationAccentColor = Color.Empty;
            }
        }
    }
}
