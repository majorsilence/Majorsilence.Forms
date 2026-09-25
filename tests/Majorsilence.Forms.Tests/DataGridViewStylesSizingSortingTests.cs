using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.4 (findings DGV-16, DGV-17, DGV-18, DGV-19, DGV-21, DGV-22) and #94.
    //
    // The renderer read private twins of public properties, so the public ones did nothing:
    // Columns["Amount"].DefaultCellStyle.Alignment = MiddleRight left numbers left-aligned, GridColor and
    // BackgroundColor were stored and never read, SortedColumn/SortOrder had setters nothing assigned,
    // AutoSizeColumnsMode.Fill left a blank band down the right, and RowTemplate -- the designer's way
    // to set row height -- had no readers.
    [Collection ("Headless")]
    public class DataGridViewStylesSizingSortingTests
    {
        private sealed class ClickableGrid : DataGridView
        {
            internal void ClickAt (int x, int y)
                => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, x, y, Point.Empty));
        }

        private static ClickableGrid Grid (out Form form, int rows = 3, int columns = 2, int width = 400)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = width + 120, Height = 340 };
            var grid = new ClickableGrid { Width = width, Height = 200 };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Name = $"C{c}", Width = 80 });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = r * 10 + c });

                grid.Rows.Add (row);
            }

            form.Controls.Add (grid);
            form.Show ();
            return grid;
        }

        private static int CountIn (SKBitmap bitmap, Rectangle area, SKColor colour)
        {
            var count = 0;

            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++)
                for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue)
                        count++;
                }

            return count;
        }

        private static SKColor SK (Color c) => new SKColor (c.R, c.G, c.B, c.A);

        // At the scale the control really paints at (RenderOnForm's default), so the bitmap is in the
        // same device-pixel space as GetCellBounds. The grid is hosted, so its Scaling is real -- the
        // 0x0-bitmap trap the explicit 1f elsewhere guards against is for UNHOSTED controls.
        private static SKBitmap Render (DataGridView grid) => PaintSurface.RenderOnForm (grid);

        // The mean x of the pixels that are not the background, or null when there are none.
        //
        // Ink is defined RELATIVE to the cell's own background, sampled from its top-left corner, rather
        // than by an absolute darkness threshold: the threshold version passed on macOS and Linux and
        // found nothing on Windows CI, because how a rasteriser antialiases a glyph is its own business.
        // What is portable is that text differs from what it sits on.
        private static double? InkCentreX (SKBitmap bitmap, Rectangle area)
        {
            if (area.Left < 0 || area.Top < 0 || area.Right > bitmap.Width || area.Bottom > bitmap.Height)
                return null;

            var background = bitmap.GetPixel (area.Left + 1, area.Top + 1);
            long sum = 0;
            var count = 0;

            for (var x = area.Left; x < area.Right; x++)
                for (var y = area.Top; y < area.Bottom; y++) {
                    var p = bitmap.GetPixel (x, y);
                    var difference = Math.Abs (p.Red - background.Red) + Math.Abs (p.Green - background.Green) + Math.Abs (p.Blue - background.Blue);

                    if (difference > 60) {
                        sum += x;
                        count++;
                    }
                }

            return count == 0 ? null : (double)sum / count;
        }

        // ---------------- DGV-16: a sort is recorded, glyphed and announced

        [Fact]
        public void Sorting_records_the_column_and_direction_and_raises_Sorted ()
        {
            // The finding's own test. The standard toggle reads these two and always saw null/None.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var sorted = 0;
                grid.Sorted += (_, _) => sorted++;

                grid.Sort (grid.Columns[1], ListSortDirection.Descending);

                Assert.Same (grid.Columns[1], grid.SortedColumn);
                Assert.Equal (SortOrder.Descending, grid.SortOrder);
                Assert.Equal (SortOrder.Descending, grid.Columns[1].HeaderCell.SortGlyphDirection);
                Assert.Equal (SortOrder.None, grid.Columns[0].HeaderCell.SortGlyphDirection);
                Assert.Equal (1, sorted);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_header_click_toggles_from_the_recorded_sort ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.SortByColumn (0, SortOrder.Ascending);
                var seen_in_handler = SortOrder.None;
                grid.ColumnHeaderMouseClick += (_, _) => seen_in_handler = grid.SortOrder;

                ClickHeader (grid, 0);

                Assert.Equal (SortOrder.Descending, grid.SortOrder);
                // DGV-16's ordering trap: the handler must see the NEW order, not the previous one.
                Assert.Equal (SortOrder.Descending, seen_in_handler);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Column_SortOrder_and_the_header_glyph_are_the_same_thing ()
        {
            // Two fields drifted apart: the header click set one and the renderer drew it; the public
            // HeaderCell.SortGlyphDirection was stored and never drawn.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].HeaderCell.SortGlyphDirection = SortOrder.Ascending;
                Assert.Equal (SortOrder.Ascending, grid.Columns[0].SortOrder);

                grid.Columns[0].SortOrder = SortOrder.Descending;
                Assert.Equal (SortOrder.Descending, grid.Columns[0].HeaderCell.SortGlyphDirection);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void SortCompare_supplies_the_ordering_when_handled ()
        {
            // It was add { } remove { }: a natural-order or numeric-string comparer was silently ignored.
            var grid = Grid (out var form, rows: 0);
            using var _form = form;

            try {
                foreach (var v in new[] { "item10", "item2", "item1" })
                    grid.Rows.Add (v, 0);

                grid.SortCompare += (_, e) => {
                    // Natural order: compare the trailing numbers.
                    var a = int.Parse (e.CellValue1!.ToString ()!.Substring (4));
                    var b = int.Parse (e.CellValue2!.ToString ()!.Substring (4));
                    e.SortResult = a.CompareTo (b);
                    e.Cancel = true;      // Handled, on this CancelEventArgs-based type
                };

                grid.SortByColumn (0, SortOrder.Ascending);

                Assert.Equal (new[] { "item1", "item2", "item10" }, grid.Rows.Select (r => r.Cells[0].Value).ToArray ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Without_a_SortCompare_handler_the_grid_sorts_by_value ()
        {
            // GUARD, not proof: value sorting worked before. It pins that adding the hook did not stop
            // the default comparison from running when nothing is attached.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.SortByColumn (0, SortOrder.Descending);

                Assert.Equal (new object[] { 20, 10, 0 }, grid.Rows.Select (r => r.Cells[0].Value).ToArray ());
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Sorting_moves_row_objects_so_their_state_travels_with_them ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                var last = grid.Rows[2];
                last.Tag = "tagged";
                last.Selected = true;

                grid.SortByColumn (0, SortOrder.Descending);

                Assert.Same (last, grid.Rows[0]);
                Assert.Equal ("tagged", grid.Rows[0].Tag);
                Assert.True (grid.Rows[0].Selected);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-17: SortMode gates the header click; bound lists sort themselves

        [Fact]
        public void A_Programmatic_column_is_not_sorted_by_a_header_click ()
        {
            // That mode exists so the app sorts in its own click handler; sorting here too sorted twice.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].SortMode = DataGridViewColumnSortMode.Programmatic;
                var before = grid.Rows.Select (r => r.Cells[0].Value).ToArray ();
                var clicks = 0;
                grid.ColumnHeaderMouseClick += (_, _) => clicks++;

                ClickHeader (grid, 0);

                Assert.Equal (before, grid.Rows.Select (r => r.Cells[0].Value).ToArray ());
                Assert.Null (grid.SortedColumn);
                Assert.Equal (1, clicks);          // the click is still reported -- that is where the app sorts
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_Automatic_column_is_sorted_by_a_header_click ()
        {
            // GUARD, not proof: header-click sorting worked before, through Sortable. It pins that the
            // SortMode gate did not close the door on the default.
            var grid = Grid (out var form);
            using var _form = form;

            try {
                ClickHeader (grid, 0);

                Assert.Same (grid.Columns[0], grid.SortedColumn);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_bound_grids_sort_survives_the_next_change_to_the_list ()
        {
            // The rows were reordered while the DataView kept source order, so the next ListChanged --
            // any edit -- snapped the grid back. The list is asked to sort itself instead.
            HeadlessRenderer.Use ();
            var table = new DataTable ();
            table.Columns.Add ("name", typeof (string));
            table.Columns.Add ("qty", typeof (int));
            table.Rows.Add ("b", 2);
            table.Rows.Add ("c", 3);
            table.Rows.Add ("a", 1);
            using var grid = new DataGridView { Width = 400, Height = 200, DataSource = table };

            grid.Sort (grid.Columns["name"]!, ListSortDirection.Ascending);
            Assert.Equal (new object[] { "a", "b", "c" }, grid.Rows.Select (r => r.Cells[0].Value).ToArray ());

            table.Rows[0]["qty"] = 99;          // an edit: raises ListChanged

            Assert.Equal (new object[] { "a", "b", "c" }, grid.Rows.Select (r => r.Cells[0].Value).ToArray ());
            Assert.Contains ("name", table.DefaultView.Sort);      // DataView writes it as "[name]"
        }

        // ---------------- DGV-18: Fill and measured resizing

        [Fact]
        public void Fill_distributes_the_content_width_by_FillWeight ()
        {
            // The finding's own test. Fill is what most designer-built grids use, and it left 100-px
            // columns and a blank band down the right.
            var grid = Grid (out var form, width: 400);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.Columns[0].FillWeight = 100;
                grid.Columns[1].FillWeight = 300;

                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();     // a layout pass

                var total = grid.Columns[0].Width + grid.Columns[1].Width;
                var content = grid.DeviceToLogicalUnits (grid.GetContentArea ().Width);

                Assert.Equal (content, total);
                Assert.Equal (3, grid.Columns[1].Width / grid.Columns[0].Width);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Fill_columns_share_only_what_the_fixed_columns_leave ()
        {
            var grid = Grid (out var form, columns: 3, width: 400);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.Columns[0].Width = 100;                                   // fixed
                grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();

                var content = grid.DeviceToLogicalUnits (grid.GetContentArea ().Width);

                Assert.Equal (100, grid.Columns[0].Width);
                Assert.Equal (content - 100, grid.Columns[1].Width + grid.Columns[2].Width);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Fill_follows_the_grid_when_it_is_resized ()
        {
            var grid = Grid (out var form, width: 400);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();
                var narrow = grid.Columns[0].Width + grid.Columns[1].Width;

                grid.Width = 600;
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();

                Assert.True (grid.Columns[0].Width + grid.Columns[1].Width > narrow);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_fill_column_never_drops_below_its_MinimumWidth ()
        {
            var grid = Grid (out var form, columns: 3, width: 200);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.Columns[0].Width = 180;                                   // hogs the space
                grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();

                Assert.True (grid.Columns[1].Width >= grid.Columns[1].MinimumWidth);
                Assert.True (grid.Columns[2].Width >= grid.Columns[2].MinimumWidth);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void AutoResizeColumns_measures_the_content ()
        {
            // `=> Invalidate ()`, while GetPreferredWidth sat unused beside it.
            var grid = Grid (out var form, rows: 0, columns: 2);
            using var _form = form;

            try {
                grid.Rows.Add ("x", "a considerably longer value that needs the room");
                var short_before = grid.Columns[0].Width;

                grid.AutoResizeColumns (DataGridViewAutoSizeColumnsMode.AllCells);

                // Asserted as a relationship: the long column grew past the short one, whatever the font.
                Assert.True (grid.Columns[1].Width > grid.Columns[0].Width,
                    $"long {grid.Columns[1].Width} should exceed short {grid.Columns[0].Width}");
                Assert.True (grid.Columns[1].Width > short_before);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void AutoResizeRows_grows_a_row_to_fit_its_content ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Rows[0].Height = 12;    // squashed below the text
                var squashed = grid.Rows[0].Height;

                grid.AutoResizeRows ();

                Assert.True (grid.Rows[0].Height > squashed);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-19: rows come from RowTemplate

        [Fact]
        public void RowTemplate_Height_applies_to_rows_added_by_hand ()
        {
            // The finding's own test: `RowTemplate.Height = 32` is what the designer emits, and it did
            // nothing.
            var grid = Grid (out var form, rows: 0);
            using var _form = form;

            try {
                grid.RowTemplate.Height = 40;

                grid.Rows.Add ();
                grid.Rows.Add ("a", "b");

                Assert.Equal (40, grid.Rows[0].Height);
                Assert.Equal (40, grid.Rows[1].Height);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void RowTemplate_applies_to_bound_rows ()
        {
            HeadlessRenderer.Use ();
            var list = new BindingList<Item> { new () { Name = "a" } };
            using var grid = new DataGridView { Width = 400, Height = 200 };
            grid.RowTemplate.Height = 37;
            grid.RowTemplate.DefaultCellStyle.BackColor = Color.Honeydew;

            grid.DataSource = list;
            list.Add (new Item { Name = "b" });          // and the incremental path too

            Assert.All (grid.Rows, r => Assert.Equal (37, r.Height));
            Assert.All (grid.Rows, r => Assert.Equal (Color.Honeydew, r.DefaultCellStyle.BackColor));
        }

        [Fact]
        public void RowHeight_is_RowTemplate_Height_under_its_older_name ()
        {
            // GUARD, not proof: this library's RowHeight always set new rows' height. It pins that
            // aliasing it to the template kept it working -- and that the two cannot disagree.
            var grid = Grid (out var form, rows: 0);
            using var _form = form;

            try {
                grid.RowHeight = 33;
                Assert.Equal (33, grid.RowTemplate.Height);

                grid.RowTemplate.Height = 29;
                Assert.Equal (29, grid.RowHeight);

                grid.Rows.Add ();
                Assert.Equal (29, grid.Rows[0].Height);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-21: the style cascade reaches the paint

        [Fact]
        public void A_columns_DefaultCellStyle_Alignment_moves_the_text ()
        {
            // The single most common column customisation. The renderer read the non-WinForms
            // DefaultCellStyleAlignment, so the public style left numbers left-aligned.
            var grid = Grid (out var form, rows: 1, columns: 1, width: 300);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.Columns[0].Width = 200;
                grid.Rows[0].Cells[0].Value = "1";
                grid.DefaultCellStyle.ForeColor = Color.Black;

                using var left = Render (grid);
                // The cell's INTERIOR: its border lines are "not the background" too, and being at both
                // edges they drag the mean to the middle wherever the text actually sits.
                var cell = Inner (grid.GetCellBounds (0, 0));
                var centre_before = InkCentreX (left, cell);
                Assert.True (centre_before is not null, "baseline: the cell should contain text");

                grid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                using var right = Render (grid);
                var centre_after = InkCentreX (right, cell);

                // Asserted as a relationship, not a region: the ink's centre moves right by at least a
                // third of the cell. "Some ink in the right third and none in the left" held on macOS and
                // Linux and failed on Windows CI, whose font puts the glyph somewhere else; where the ink
                // ENDS UP is the font's business, that it MOVED is the layout's. The numbers are in the
                // message so the next platform difference explains itself.
                Assert.True (centre_after is not null, "right-aligned: the cell should still contain text");
                Assert.True (centre_after > centre_before + cell.Width / 3,
                    $"right-aligning should move the ink right by a third of the cell: centre {centre_before} -> {centre_after} in a {cell.Width}-px cell");
            } finally {
                form.Close ();
            }
        }

        // A grid type of its own, so the capturing renderer below is registered for it and not for every
        // other DataGridView test in the run.
        private sealed class CascadeGrid : DataGridView { }

        // Records the ControlStyle each cell is drawn with -- the cascade the renderer resolved, which
        // is the link DGV-21 is about. Every RenderCell path funnels through the paintParts overload.
        private sealed class CascadeRenderer : DataGridViewRenderer
        {
            public override Type Type => typeof (CascadeGrid);

            public readonly Dictionary<int, ControlStyle?> StyleByColumn = new ();

            protected override void RenderCell (DataGridView control, DataGridViewColumn column, string value, int rowIndex,
                int columnIndex, Rectangle bounds, ControlStyle? cellStyle, PaintEventArgs e, DataGridViewPaintParts paintParts)
            {
                if (rowIndex == 0)
                    StyleByColumn[columnIndex] = cellStyle;

                base.RenderCell (control, column, value, rowIndex, columnIndex, bounds, cellStyle, e, paintParts);
            }
        }

        [Fact]
        public void The_resolved_cascade_is_what_the_renderer_is_handed ()
        {
            // The pixel test above proves the text MOVES; this proves the renderer was HANDED the
            // cascaded style, with no dependency on how a platform rasterises a glyph. Asserted on the
            // ControlStyle RenderCell receives, not on CellPainting.CellStyle -- that already carried
            // InheritedStyle before this change, so asserting it would prove nothing new.
            HeadlessRenderer.Use ();
            var renderer = new CascadeRenderer ();
            RenderManager.SetRenderer<CascadeGrid> (renderer);

            using var form = new Form { Width = 520, Height = 340 };
            var grid = new CascadeGrid { Width = 400, Height = 200 };
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "A", Width = 80 });
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "B", Width = 80 });
            grid.Rows.Add ("x", "y");
            grid.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns[1].DefaultCellStyle.BackColor = Color.Salmon;
            form.Controls.Add (grid);
            form.Show ();

            try {
                PaintSurface.RenderOnForm (grid).Dispose ();

                Assert.Equal (DataGridViewContentAlignment.MiddleRight, renderer.StyleByColumn[1]?.Alignment);
                // ToArgb, not the Color: a named colour and the ARGB it resolves to are equal in value
                // and not equal as Color instances.
                Assert.Equal (Color.Salmon.ToArgb (), renderer.StyleByColumn[1]?.BackColor.ToArgb ());
                Assert.NotEqual (DataGridViewContentAlignment.MiddleRight, renderer.StyleByColumn[0]?.Alignment);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void DefaultCellStyleAlignment_and_DefaultCellStyle_Alignment_are_the_same_thing ()
        {
            var grid = Grid (out var form);
            using var _form = form;

            try {
                grid.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.BottomCenter;
                Assert.Equal (ContentAlignment.BottomCenter, grid.Columns[0].DefaultCellStyleAlignment);

                grid.Columns[0].DefaultCellStyleAlignment = ContentAlignment.TopRight;
                Assert.Equal (DataGridViewContentAlignment.TopRight, grid.Columns[0].DefaultCellStyle.Alignment);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_columns_DefaultCellStyle_BackColor_is_painted_without_a_handler ()
        {
            // It only took effect through a CellFormatting handler's style.
            var grid = Grid (out var form, rows: 1, columns: 2);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.ClearSelection ();
                grid.Columns[1].DefaultCellStyle.BackColor = Color.Salmon;

                using var bitmap = PaintSurface.RenderOnForm (grid, 1f);
                var salmon = SK (Color.Salmon);

                Assert.True (CountIn (bitmap, Inner (grid.GetCellBounds (0, 1)), salmon) > 0, "column back colour not painted");
                Assert.Equal (0, CountIn (bitmap, Inner (grid.GetCellBounds (0, 0)), salmon));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void SelectionBackColor_from_the_style_paints_the_selected_row ()
        {
            // The selection colour was a fixed theme colour; DefaultCellStyle.SelectionBackColor -- in
            // nearly every styled app -- was ignored.
            var grid = Grid (out var form, rows: 2, columns: 1);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.DefaultCellStyle.SelectionBackColor = Color.Gold;
                grid.ClearSelection ();
                grid.Rows[1].Selected = true;

                using var bitmap = PaintSurface.RenderOnForm (grid, 1f);
                var gold = SK (Color.Gold);

                Assert.True (CountIn (bitmap, Inner (grid.GetCellBounds (1, 0)), gold) > 0, "selection colour not painted");
                Assert.Equal (0, CountIn (bitmap, Inner (grid.GetCellBounds (0, 0)), gold));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_cells_own_style_still_wins_over_the_columns ()
        {
            // GUARD, not proof: cell.Style was what the renderer read before, so it always won. It pins
            // that folding the cascade in kept the cell at the top of it.
            var grid = Grid (out var form, rows: 1, columns: 1);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.ClearSelection ();
                grid.Columns[0].DefaultCellStyle.BackColor = Color.Salmon;
                grid.Rows[0].Cells[0].Style.BackColor = Color.Aquamarine;

                using var bitmap = PaintSurface.RenderOnForm (grid, 1f);

                Assert.True (CountIn (bitmap, Inner (grid.GetCellBounds (0, 0)), SK (Color.Aquamarine)) > 0);
                Assert.Equal (0, CountIn (bitmap, Inner (grid.GetCellBounds (0, 0)), SK (Color.Salmon)));
            } finally {
                form.Close ();
            }
        }

        // ---------------- DGV-22: GridColor and BackgroundColor are read

        [Fact]
        public void GridColor_colours_the_grid_lines ()
        {
            var grid = Grid (out var form, rows: 2, columns: 2);
            using var _form = form;

            try {
                grid.RowHeadersVisible = false;
                grid.GridColor = Color.Red;

                using var bitmap = PaintSurface.RenderOnForm (grid, 1f);
                var cell = grid.GetCellBounds (0, 0);

                // The bottom edge of the first row is a grid line.
                var bottom_edge = new Rectangle (cell.Left + 2, cell.Bottom - 1, cell.Width - 4, 1);
                Assert.True (CountIn (bitmap, bottom_edge, SK (Color.Red)) > 0, "grid line not in GridColor");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void BackgroundColor_fills_the_area_below_the_last_row ()
        {
            var grid = Grid (out var form, rows: 1, columns: 1);
            using var _form = form;

            try {
                grid.BackgroundColor = Color.MediumPurple;

                // Rendered at the control's own scale: GetCellBounds/GetContentArea are DEVICE pixels,
                // and a bitmap rendered at 1f under MF_HEADLESS_SCALE=2 is half their size, so probes
                // taken from them fall off it (the coordinate-space trap W6.3 catalogues).
                using var bitmap = Render (grid);
                var last = grid.GetCellBounds (0, 0);
                var content = grid.GetContentArea ();
                var below = new Rectangle (content.Left + 2, last.Bottom + 2, content.Width - 4, Math.Max (1, content.Bottom - last.Bottom - 4));

                Assert.True (CountIn (bitmap, below, SK (Color.MediumPurple)) > below.Width * below.Height / 2,
                    "the band below the last row should be BackgroundColor");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void With_no_BackgroundColor_the_band_below_the_rows_keeps_the_theme ()
        {
            // GUARD, not proof, and a recorded deviation: upstream defaults BackgroundColor to
            // AppWorkspace. Here the default stays Empty so an unthemed grid looks as it did; the fill
            // happens only when the app sets a colour.
            var grid = Grid (out var form, rows: 1, columns: 1);
            using var _form = form;

            try {
                Assert.True (grid.BackgroundColor.IsEmpty);
            } finally {
                form.Close ();
            }
        }

        // ---------------- #94: the scroll range counts visible rows

        [Fact]
        public void Hiding_rows_shrinks_the_scroll_range ()
        {
            // With Row.Visible real (W5.2a), counting Rows.Count let the thumb travel past the last
            // visible row into blank space.
            var grid = Grid (out var form, rows: 20, columns: 1);
            using var _form = form;

            try {
                grid.Height = 120;
                PaintSurface.RenderOnForm (grid, 1f).Dispose ();
                var all_visible = grid.VerticalScrollMaximum;
                Assert.True (all_visible > 0, "the grid should need to scroll");

                for (var i = 10; i < 20; i++)
                    grid.Rows[i].Visible = false;

                PaintSurface.RenderOnForm (grid, 1f).Dispose ();

                Assert.True (grid.VerticalScrollMaximum < all_visible,
                    $"hiding half the rows should shrink the range: {grid.VerticalScrollMaximum} vs {all_visible}");
            } finally {
                form.Close ();
            }
        }

        // ---------------- helpers

        private static Rectangle Inner (Rectangle r) => new Rectangle (r.Left + 3, r.Top + 3, Math.Max (1, r.Width - 6), Math.Max (1, r.Height - 6));

        private static void ClickHeader (ClickableGrid grid, int columnIndex)
        {
            PaintSurface.RenderOnForm (grid, 1f).Dispose ();
            var content = grid.GetContentArea ();
            var x = grid.GetColumnDeviceLeft (columnIndex) + grid.LogicalToDeviceUnits (grid.Columns[columnIndex].Width) / 2;
            var y = content.Top + grid.ScaledHeaderHeight / 2;
            // x/y are device; the mouse event is logical (RC-8).
            grid.ClickAt (grid.DeviceToLogicalUnits (x), grid.DeviceToLogicalUnits (y));
        }

        private sealed class Item
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
