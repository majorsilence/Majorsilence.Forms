using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #342: the DataGridView behaviour-gap findings still open on 2026-10-02 -- DGV-04 (the Add overloads'
    // return types and DataBindingComplete), DGV-12 (the CurrentCell setter), DGV-24 (HeaderText is
    // HeaderCell.Value), DGV-28 (content clicks), DGV-34 (the scrolling API), DGV-35 (HitTest), DGV-36
    // (FormattedValue), DGV-38's last event (RowDirtyStateNeeded) and DGV-40 (Rows.CollectionChanged).
    [Collection ("Headless")]
    public sealed class DataGridViewGapClosureTests : IDisposable
    {
        private readonly double original_ui_scale = Application.UiScale;

        public void Dispose () => Application.UiScale = original_ui_scale;

        private sealed class DrivenGrid : DataGridView
        {
            internal void Down (Point p) => OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));

            internal void Up (Point p) => OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));

            internal void ClickAt (Point p)
            {
                Down (p);
                Up (p);
            }

            // LOGICAL, as MouseEventArgs carry them; the geometry is device (RC-8).
            internal Rectangle LogicalCell (int rowIndex, int columnIndex) => DeviceToLogicalUnits (GetCellBounds (rowIndex, columnIndex));
        }

        private static DrivenGrid Grid (out Form form, int rows = 3, int columns = 2, int width = 400, int height = 220, int columnWidth = 80)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = width + 120, Height = height + 120 };
            var grid = new DrivenGrid { Width = width, Height = height };

            for (var c = 0; c < columns; c++)
                grid.Columns.Add (new DataGridViewColumn { HeaderText = $"C{c}", Name = $"C{c}", Width = columnWidth });

            for (var r = 0; r < rows; r++) {
                var row = new DataGridViewRow ();

                for (var c = 0; c < columns; c++)
                    row.Cells.Add (new DataGridViewCell { Value = $"r{r}c{c}" });

                grid.Rows.Add (row);
            }

            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            return grid;
        }

        private sealed class Person
        {
            public string Name { get; set; } = string.Empty;
        }

        // ---------------- DGV-04

        [Fact]
        public void Rows_Add_values_returns_the_new_rows_index ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", "A");

            int first = grid.Rows.Add ("x");
            int second = grid.Rows.Add ("y");

            Assert.Equal (0, first);
            Assert.Equal (1, second);
            Assert.Equal ("y", grid.Rows[second].Cells[0].Value);
        }

        [Fact]
        public void Columns_Add_name_and_header_returns_the_new_columns_index ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add (new DataGridViewColumn { Name = "First" });

            int index = grid.Columns.Add ("Id", "ID");

            Assert.Equal (1, index);
            Assert.Equal ("Id", grid.Columns[index].Name);
        }

        [Fact]
        public void DataBindingComplete_follows_a_bind_and_each_list_change_with_its_type ()
        {
            using var grid = new DataGridView ();
            var seen = new List<ListChangedType> ();
            grid.DataBindingComplete += (_, e) => seen.Add (e.ListChangedType);

            var people = new BindingList<Person> { new Person { Name = "Ann" } };
            grid.DataSource = people;
            people.Add (new Person { Name = "Bob" });
            people.RemoveAt (0);

            Assert.Equal (new[] { ListChangedType.Reset, ListChangedType.ItemAdded, ListChangedType.ItemDeleted }, seen);

            // Unbinding is not a completed binding: upstream raises only while a data connection exists.
            seen.Clear ();
            grid.DataSource = null;
            Assert.Empty (seen);
        }

        // ---------------- DGV-12

        [Fact]
        public void Assigning_CurrentCell_moves_once ()
        {
            var grid = Grid (out var form);

            using (form) {
                grid.CurrentCell = grid[0, 0];
                var selection_changes = 0;
                var current_changes = 0;
                var row_validating = 0;
                grid.SelectionChanged += (_, _) => selection_changes++;
                grid.CurrentCellChanged += (_, _) => current_changes++;
                grid.RowValidating += (_, _) => row_validating++;

                grid.CurrentCell = grid[1, 2];

                Assert.Equal (new Point (1, 2), grid.CurrentCellAddress);
                Assert.Equal (1, selection_changes);
                Assert.Equal (1, current_changes);
                Assert.Equal (1, row_validating);
            }
        }

        [Fact]
        public void Assigning_null_to_CurrentCell_clears_it_and_the_selection ()
        {
            var grid = Grid (out var form);

            using (form) {
                grid.CurrentCell = grid[1, 1];
                Assert.NotEmpty (grid.SelectedRows);

                grid.CurrentCell = null;

                Assert.Null (grid.CurrentCell);
                Assert.Equal (new Point (-1, -1), grid.CurrentCellAddress);
                Assert.Empty (grid.SelectedRows);
                Assert.Empty (grid.SelectedCells);
            }
        }

        [Fact]
        public void Assigning_CurrentCell_scrolls_it_into_view ()
        {
            var grid = Grid (out var form, rows: 100);

            using (form) {
                grid.CurrentCell = grid[0, 80];

                var top = grid.FirstDisplayedScrollingRowIndex;
                Assert.InRange (80, top, top + grid.DisplayedRowCount (false) - 1);
            }
        }

        // ---------------- DGV-34

        [Fact]
        public void ScrollIntoView_brings_a_row_below_the_view_to_the_bottom_edge ()
        {
            var grid = Grid (out var form, rows: 100);

            using (form) {
                Assert.Equal (0, grid.FirstDisplayedScrollingRowIndex);

                grid.ScrollIntoView (0, 50);

                // The smallest scroll: row 50 becomes the LAST fully displayed row, not the first.
                Assert.Equal (50, grid.FirstDisplayedScrollingRowIndex + grid.DisplayedRowCount (false) - 1);

                // Already visible: no further scroll.
                var top = grid.FirstDisplayedScrollingRowIndex;
                grid.ScrollIntoView (0, top + 1);
                Assert.Equal (top, grid.FirstDisplayedScrollingRowIndex);

                // Above the view: it becomes the first row.
                grid.ScrollIntoView (0, 3);
                Assert.Equal (3, grid.FirstDisplayedScrollingRowIndex);
            }
        }

        [Fact]
        public void ScrollIntoView_scrolls_a_column_past_the_right_edge_into_view ()
        {
            var grid = Grid (out var form, rows: 3, columns: 12, columnWidth: 90);

            using (form) {
                Assert.Equal (0, grid.HorizontalScrollingOffset);

                grid.ScrollIntoView (11, -1);

                Assert.True (grid.HorizontalScrollingOffset > 0);

                // Device on both sides: the column must end inside the content area, and exactly at its
                // edge -- the smallest scroll that shows it whole.
                var cell = grid.GetCellBounds (0, 11);
                Assert.False (cell.IsEmpty);
                Assert.Equal (grid.GetContentArea ().Right, cell.Right);
            }
        }

        [Fact]
        public void The_horizontal_range_follows_a_scale_change_made_after_the_columns_were_added ()
        {
            // The bar's range is device pixels. Computed at one scale and used at another, it covered
            // half the columns' width at 2x, so the last column could never be scrolled to.
            var grid = Grid (out var form, rows: 3, columns: 12, columnWidth: 90);

            using (form) {
                Application.UiScale = original_ui_scale * 2;

                grid.ScrollIntoView (11, -1);

                var cell = grid.GetCellBounds (0, 11);
                Assert.False (cell.IsEmpty);
                Assert.Equal (grid.GetContentArea ().Right, cell.Right);
            }
        }

        [Fact]
        public void Scroll_is_raised_with_the_old_and_new_positions ()
        {
            var grid = Grid (out var form, rows: 100, columns: 12, columnWidth: 90);

            using (form) {
                var seen = new List<(ScrollOrientation Orientation, int Old, int New)> ();
                grid.Scroll += (_, e) => seen.Add ((e.ScrollOrientation, e.OldValue, e.NewValue));

                grid.FirstDisplayedScrollingRowIndex = 10;
                grid.HorizontalScrollingOffset = 40;

                Assert.Equal (2, seen.Count);
                Assert.Equal ((ScrollOrientation.VerticalScroll, 0, 10), seen[0]);
                Assert.Equal ((ScrollOrientation.HorizontalScroll, 0, 40), seen[1]);
            }
        }

        [Fact]
        public void DisplayedRowCount_counts_from_the_first_displayed_row ()
        {
            var grid = Grid (out var form, rows: 60);

            using (form) {
                // Tall rows at the top: counted from row 0 the answer is small, from row 30 it is not.
                for (var i = 0; i < 10; i++)
                    grid.Rows[i].Height = 60;

                PaintSurface.Render (grid).Dispose ();
                var at_top = grid.DisplayedRowCount (false);

                grid.FirstDisplayedScrollingRowIndex = 30;

                Assert.True (grid.DisplayedRowCount (false) > at_top,
                    $"Counted {grid.DisplayedRowCount (false)} rows from row 30, {at_top} from row 0.");
            }
        }

        [Fact]
        public void DisplayedRowCount_includes_a_partial_row_only_when_asked ()
        {
            var grid = Grid (out var form, rows: 60);

            using (form) {
                // Make the last row a partial one whatever the platform's header height: the rows are
                // all one height, so a remainder in the available height IS a partial row.
                var available = grid.GetContentArea ().Height - grid.RowsTopOffset;
                var row_height = grid.LogicalToDeviceUnits (grid.Rows[0].Height);

                if (available % row_height == 0)
                    grid.Height += 7;

                PaintSurface.Render (grid).Dispose ();

                Assert.Equal (grid.DisplayedRowCount (false) + 1, grid.DisplayedRowCount (true));
            }
        }

        [Fact]
        public void FirstDisplayedCell_follows_the_scroll_and_assigning_it_scrolls_without_moving_the_current_cell ()
        {
            var grid = Grid (out var form, rows: 100);

            using (form) {
                grid.CurrentCell = grid[0, 0];
                grid.FirstDisplayedScrollingRowIndex = 20;

                Assert.Same (grid[0, 20], grid.FirstDisplayedCell);

                grid.FirstDisplayedCell = grid[1, 40];

                Assert.Equal (40, grid.FirstDisplayedScrollingRowIndex);
                Assert.Equal (new Point (0, 0), grid.CurrentCellAddress);
            }
        }

        // ---------------- DGV-35

        [Fact]
        public void HitTest_reports_headers_cells_and_the_scroll_bar ()
        {
            var grid = Grid (out var form, rows: 100);

            using (form) {
                grid.RowHeadersVisible = true;
                PaintSurface.Render (grid).Dispose ();

                var area = grid.DeviceToLogicalUnits (grid.GetContentArea ());
                var header = grid.DeviceToLogicalUnits (grid.GetCellBounds (-1, 1));
                var cell = grid.LogicalCell (2, 1);
                var row_header_x = area.Left + grid.RowHeadersWidth / 2;

                var column_header = grid.HitTest (header.Left + header.Width / 2, header.Top + header.Height / 2);
                Assert.Equal (DataGridViewHitTestType.ColumnHeader, column_header.Type);
                Assert.Equal (1, column_header.ColumnIndex);
                Assert.Equal (-1, column_header.RowIndex);
                Assert.Equal (header.Left, column_header.ColumnX);

                var row_header = grid.HitTest (row_header_x, cell.Top + cell.Height / 2);
                Assert.Equal (DataGridViewHitTestType.RowHeader, row_header.Type);
                Assert.Equal (-1, row_header.ColumnIndex);
                Assert.Equal (2, row_header.RowIndex);
                Assert.Equal (cell.Top, row_header.RowY);

                Assert.Equal (DataGridViewHitTestType.TopLeftHeader, grid.HitTest (row_header_x, header.Top + header.Height / 2).Type);

                var hit_cell = grid.HitTest (cell.Left + cell.Width / 2, cell.Top + cell.Height / 2);
                Assert.Equal (DataGridViewHitTestType.Cell, hit_cell.Type);
                Assert.Equal ((1, 2), (hit_cell.ColumnIndex, hit_cell.RowIndex));

                var bar = grid.Controls.OfType<VScrollBar> ().Single ();
                Assert.True (bar.Visible);
                Assert.Equal (DataGridViewHitTestType.VerticalScrollBar,
                    grid.HitTest (bar.Left + bar.Width / 2, bar.Top + bar.Height / 2).Type);
            }
        }

        // ---------------- DGV-24

        [Fact]
        public void HeaderText_and_HeaderCell_Value_are_one_value ()
        {
            var column = new DataGridViewColumn { HeaderText = "Qty" };

            Assert.Equal ("Qty", column.HeaderCell.Value);

            column.HeaderCell.Value = "Quantity";

            Assert.Equal ("Quantity", column.HeaderText);
        }

        [Fact]
        public void A_header_cells_value_is_what_the_column_header_paints ()
        {
            var grid = Grid (out var form);

            using (form) {
                grid.Columns[1].HeaderText = string.Empty;
                using var before = PaintSurface.Render (grid);

                grid.Columns[1].HeaderCell.Value = "WWWW";
                using var after = PaintSurface.Render (grid);

                var header = grid.GetCellBounds (-1, 1);
                var changed = ChangedRegion (before, after);

                Assert.False (changed.IsEmpty, "Setting HeaderCell.Value changed nothing on screen.");
                Assert.True (header.Contains (changed), $"The change {changed} is outside the header {header}.");
            }
        }

        [Fact]
        public void A_rows_header_cell_value_is_painted_in_its_header ()
        {
            // The row-number idiom: row.HeaderCell.Value = (i + 1).ToString ().
            var grid = Grid (out var form);

            using (form) {
                grid.RowHeadersVisible = true;
                grid.RowHeadersWidth = 60;
                using var before = PaintSurface.Render (grid);

                grid.Rows[2].HeaderCell.Value = "88";
                using var after = PaintSurface.Render (grid);

                var row = grid.GetCellBounds (2, 0);
                var header = new Rectangle (grid.GetContentArea ().Left, row.Top, row.Left - grid.GetContentArea ().Left, row.Height);
                var changed = ChangedRegion (before, after);

                Assert.False (changed.IsEmpty, "Setting the row's HeaderCell.Value painted nothing.");
                Assert.True (header.Contains (changed), $"The change {changed} is outside row 2's header {header}.");
            }
        }

        // ---------------- DGV-36

        [Fact]
        public void FormattedValue_goes_through_the_cells_format ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("Amount", "Amount");
            grid.Columns[0].DefaultCellStyle.Format = "N2";
            grid.Columns[0].DefaultCellStyle.FormatProvider = CultureInfo.InvariantCulture;
            grid.Rows.Add (1234.5m);

            Assert.Equal ("1,234.50", grid[0, 0]!.FormattedValue);
        }

        [Fact]
        public void FormattedValue_shows_NullValue_for_a_null ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("Amount", "Amount");
            grid.Columns[0].DefaultCellStyle.NullValue = "(none)";
            grid.Rows.Add (new object?[] { null }!);

            Assert.Equal ("(none)", grid[0, 0]!.FormattedValue);
        }

        [Fact]
        public void FormattedValue_and_the_clipboard_see_what_CellFormatting_produced ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("Code", "Code");
            grid.Rows.Add ("a");
            grid.CellFormatting += (_, e) => {
                // A handler that reads the cell's own FormattedValue must not recurse.
                _ = grid[e.ColumnIndex, e.RowIndex]!.FormattedValue;
                e.Value = "Alpha";
                e.FormattingApplied = true;
            };

            Assert.Equal ("Alpha", grid[0, 0]!.FormattedValue);

            grid.SelectedRowIndex = 0;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            var text = (string)grid.GetClipboardContent ()!.GetData (DataFormats.Text.Name)!;
            Assert.Equal ($"Alpha{Environment.NewLine}", text);
        }

        // ---------------- DGV-40

        [Fact]
        public void Rows_CollectionChanged_reports_adds_removes_and_clears ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", "A");
            var seen = new List<(CollectionChangeAction Action, object? Element)> ();
            grid.Rows.CollectionChanged += (_, e) => seen.Add ((e.Action, e.Element));

            var index = grid.Rows.Add ("x");
            var row = grid.Rows[index];
            grid.Rows.RemoveAt (index);
            grid.Rows.Add ("y");
            grid.Rows.Clear ();

            Assert.Equal (4, seen.Count);
            Assert.Equal ((CollectionChangeAction.Add, (object?)row), seen[0]);
            Assert.Equal ((CollectionChangeAction.Remove, (object?)row), seen[1]);
            Assert.Equal (CollectionChangeAction.Add, seen[2].Action);
            Assert.Equal ((CollectionChangeAction.Refresh, (object?)null), seen[3]);
        }

        // ---------------- DGV-38

        [Fact]
        public void IsCurrentRowDirty_asks_RowDirtyStateNeeded_in_virtual_mode_only ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add ("a", "A");
            grid.RowCount = 2;
            var asked = 0;
            grid.RowDirtyStateNeeded += (_, e) => {
                asked++;
                e.Response = true;
            };

            Assert.False (grid.IsCurrentRowDirty);
            Assert.Equal (0, asked);

            grid.VirtualMode = true;

            Assert.True (grid.IsCurrentRowDirty);
            Assert.Equal (1, asked);
        }

        // ---------------- DGV-28

        [Fact]
        public void A_click_beside_a_cells_text_is_a_cell_click_but_not_a_content_click ()
        {
            var grid = Grid (out var form, columnWidth: 200);

            using (form) {
                var clicks = 0;
                var content_clicks = 0;
                grid.CellClick += (_, _) => clicks++;
                grid.CellContentClick += (_, _) => content_clicks++;

                var cell = grid.LogicalCell (1, 0);
                var content = grid[0, 1]!.ContentBounds;

                // Relative to the cell, and the short text ends well before the wide cell does.
                Assert.InRange (content.Left, 0, 10);
                Assert.True (content.Right < cell.Width / 2, $"Content {content} in a cell {cell.Width} wide.");

                grid.ClickAt (new Point (cell.Right - 10, cell.Top + cell.Height / 2));
                Assert.Equal ((1, 0), (clicks, content_clicks));

                grid.ClickAt (new Point (cell.Left + content.Left + content.Width / 2, cell.Top + content.Top + content.Height / 2));
                Assert.Equal ((2, 1), (clicks, content_clicks));
            }
        }

        [Fact]
        public void A_check_box_cell_toggles_from_its_glyph_only ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            var grid = new DrivenGrid { Width = 300, Height = 160 };
            grid.Columns.Add (new DataGridViewCheckBoxColumn { Name = "Done", Width = 160 });
            grid.Rows.Add (false);
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.Render (grid).Dispose ();

            var cell = grid.LogicalCell (0, 0);
            var glyph = grid[0, 0]!.ContentBounds;

            // Beside the glyph: a click on the cell, not on the box.
            grid.ClickAt (new Point (cell.Left + 3, cell.Top + cell.Height / 2));
            Assert.Equal (false, grid[0, 0]!.Value);

            grid.ClickAt (new Point (cell.Left + glyph.Left + glyph.Width / 2, cell.Top + glyph.Top + glyph.Height / 2));
            Assert.Equal (true, grid[0, 0]!.Value);
        }

        // The bounding box of the pixels that differ between two renders, in device pixels.
        private static Rectangle ChangedRegion (SKBitmap before, SKBitmap after)
        {
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;

            for (var y = 0; y < Math.Min (before.Height, after.Height); y++) {
                for (var x = 0; x < Math.Min (before.Width, after.Width); x++) {
                    if (before.GetPixel (x, y) == after.GetPixel (x, y))
                        continue;

                    left = Math.Min (left, x);
                    top = Math.Min (top, y);
                    right = Math.Max (right, x);
                    bottom = Math.Max (bottom, y);
                }
            }

            return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB (left, top, right + 1, bottom + 1);
        }
    }
}
