using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) lays a column header out as
    // DataGridViewColumnHeaderCell does, measured from WinForms: a sortable header keeps 19px for its sort glyph, a header
    // is GDI's line height a line plus 8 (28px in Microsoft Sans Serif 12pt), and a caption too wide for its header is
    // cut at the header's edge. A Microsoft Sans Serif 12pt "Hold Back" header wraps below a 107px sortable column
    // upstream; here it fitted down to 85, so a grid's headers came out a line short, and "Approve" ran into the next one.
    [Collection ("Headless")]
    public class DataGridViewUpstreamHeaderTests
    {
        [Fact]
        public void Only_a_text_column_is_sortable_by_default ()
        {
            Assert.Equal (DataGridViewColumnSortMode.Automatic, new DataGridViewTextBoxColumn ().SortMode);
            Assert.Equal (DataGridViewColumnSortMode.NotSortable, new DataGridViewCheckBoxColumn ().SortMode);
            Assert.Equal (DataGridViewColumnSortMode.NotSortable, new DataGridViewComboBoxColumn ().SortMode);
            Assert.Equal (DataGridViewColumnSortMode.NotSortable, new DataGridViewButtonColumn ().SortMode);
            Assert.Equal (DataGridViewColumnSortMode.NotSortable, new DataGridViewImageColumn ().SortMode);
            Assert.Equal (DataGridViewColumnSortMode.NotSortable, new DataGridViewLinkColumn ().SortMode);

            var assigned = new DataGridViewCheckBoxColumn { SortMode = DataGridViewColumnSortMode.Programmatic };
            Assert.Equal (DataGridViewColumnSortMode.Programmatic, assigned.SortMode);
        }

        private static (Form form, DataGridView grid) Grid (params DataGridViewColumn[] columns)
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (Control.DefaultFont.Name, 9f));
            HeadlessRenderer.Use ();
            var form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (600, 200), Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f) };
            var grid = new DataGridView { Location = new Point (0, 0), Size = new Size (600, 200), ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize };
            grid.Columns.AddRange (columns);
            form.Controls.Add (grid);
            form.Show ();
            return (form, grid);
        }

        // A width "Hold Back" fits on one line in a header without the glyph's room, and does not with it.
        [Fact]
        public void A_sortable_header_wraps_where_one_without_a_sort_glyph_does_not ()
        {
            try {
                var column = new DataGridViewTextBoxColumn { HeaderText = "Hold Back" };
                var (form, grid) = Grid (column);

                using (form) {
                    // Upstream's header: a 1px border, 2px margins either side, and the caption with TextRenderer's padding (a
                    // sixth of the line height either side) -- and with room for it all, 4px to spare.
                    var font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f);
                    var (typeface, size) = grid.HeaderFallbackFont ();
                    var text = (int) Math.Ceiling (TextMeasurer.MeasureText ("Hold Back", typeface, size).Width);
                    var gdi_padding = 2 * (int) Math.Ceiling (font.Height / 6.0);
                    column.Width = text + gdi_padding + 1 + 2 * 2 + 4;
                    var line = TextLineHeight.Of (font);

                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    Assert.Equal (line + 8, grid.EffectiveHeaderHeight);

                    column.SortMode = DataGridViewColumnSortMode.Automatic;
                    Assert.Equal (2 * line + 8, grid.EffectiveHeaderHeight);

                    if (OperatingSystem.IsWindows ())
                        Assert.Equal (20, line);   // WinForms' headers: 28px, and 48 wrapped
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        // The caption of a header too narrow for it is cut at its edge: the empty header beside it shows no ink.
        [Fact]
        public void A_caption_too_wide_for_its_header_is_cut_at_its_edge ()
        {
            try {
                var narrow = new DataGridViewCheckBoxColumn { HeaderText = "Approve", Width = 40 };
                var next = new DataGridViewTextBoxColumn { HeaderText = "", Width = 80 };
                var (form, grid) = Grid (narrow, next);

                using (form) {
                    using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 600, 200));
                    var scale = bitmap.Width / 600f;
                    var bounds = next.HeaderBounds;
                    var ink = 0;

                    for (var x = bounds.Left + (int) (2 * scale); x < bounds.Right - (int) (2 * scale); x++)
                        for (var y = bounds.Top + (int) (2 * scale); y < bounds.Bottom - (int) (2 * scale); y++) {
                            var p = bitmap.GetPixel (x, y);
                            if (p.Red + p.Green + p.Blue < 300)
                                ink++;
                        }

                    Assert.Equal (0, ink);
                }
            } finally {
                Application.SetDefaultFont (null!);
            }
        }
    }
}
