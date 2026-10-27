using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // AutoSizeRowsMode sizes the rows, as upstream's does. It was stored and raised its event, and the rows never moved: a
    // Fill grid with DefaultCellStyle.WrapMode True and four ~130-character descriptions showed every row one line tall
    // with the text cut off. Measured on WinForms (.NET 10), a 1334x450 Fill grid bound to DataTable (ID, Type,
    // Description), Microsoft Sans Serif 12, RowTemplate.Height 25:
    //   - every non-None mode sizes the rows on bind: the cell modes to the wrapped text (63, three 20px lines plus 3), the
    //     header modes to the empty row header (17); None leaves them at 25. Short text gives 23 (24 unwrapped), below the
    //     template; only MinimumHeight floors it.
    //   - the grid at 900 makes the rows 83, at 1800 43; a fixed Description column at 200 makes them 123, at 1000 23.
    //   - a value change re-sizes its row before CellValueChanged; an added row is sized as it arrives.
    //   - a Height assignment under a mode is ignored, raising nothing, and given back at None with the heights the rows
    //     had when the mode was switched on; RowHeightChanged for each row comes before AutoSizeRowsModeChanged.
    //   - the Displayed* modes size the rows on screen: of 40 bound rows, rows 0-7; the rest keep the first row's height
    //     until they are scrolled into view and painted. The All* modes size every row.
    // Heights depend on the font, so the tests assert what the measurements establish relative to a one-line row.
    [Collection ("Headless")]
    public class DataGridViewAutoSizeRowsModeTests
    {
        private const string Long1 = "The annual levy for the northern ward includes a sewer surcharge, a water meter rental fee and a one-time frontage charge for paving.";
        private const string Long2 = "Penalty and interest applied to the arrears balance carried forward from the previous billing period, calculated at the posted rate.";

        private static DataTable Table (params string[] descriptions)
        {
            var table = new DataTable ();
            table.Columns.Add ("ID", typeof (int));
            table.Columns.Add ("Type", typeof (string));
            table.Columns.Add ("Description", typeof (string));

            for (var i = 0; i < descriptions.Length; i++)
                table.Rows.Add (i + 1, "T" + i, descriptions[i]);

            return table;
        }

        private static (Form form, DataGridView grid) Grid (DataGridViewAutoSizeRowsMode mode, bool wrap = true, Action<DataGridView>? before = null)
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new Size (1900, 600) };
            var grid = new DataGridView {
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Size = new Size (1334, 450),
                AllowUserToAddRows = false,
            };
            grid.DefaultCellStyle.WrapMode = wrap ? DataGridViewTriState.True : DataGridViewTriState.False;
            grid.AutoSizeRowsMode = mode;
            before?.Invoke (grid);
            form.Controls.Add (grid);
            form.Show ();
            return (form, grid);
        }

        // The height of a row whose text is one line, under the same grid: the yardstick for "N lines tall".
        private static int OneLineHeight (DataGridView grid) => grid.Rows[grid.Rows.Count - 1].Height;

        [Theory]
        [InlineData (DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders)]
        [InlineData (DataGridViewAutoSizeRowsMode.AllCells)]
        [InlineData (DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders)]
        [InlineData (DataGridViewAutoSizeRowsMode.DisplayedCells)]
        public void A_cell_mode_sizes_bound_rows_to_their_wrapped_text (DataGridViewAutoSizeRowsMode mode)
        {
            var (form, grid) = Grid (mode);

            using (form) {
                grid.DataSource = Table (Long1, Long2, Long1, "x");
                var line = OneLineHeight (grid);

                // At least two lines: upstream wraps these to three in a 430px column.
                for (var i = 0; i < 3; i++)
                    Assert.True (grid.Rows[i].Height >= 2 * line - 3, $"row {i} is {grid.Rows[i].Height}px, one line is {line}px");

                // And the short row came out below the template, as upstream's 23 against 25 does.
                Assert.True (line < grid.RowTemplate.Height + 2, $"one line is {line}px, the template {grid.RowTemplate.Height}px");
            }
        }

        [Fact]
        public void None_leaves_the_rows_at_the_template_height ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.None);

            using (form) {
                grid.DataSource = Table (Long1, Long2, "x");

                Assert.All (grid.Rows.Cast<DataGridViewRow> (), r => Assert.Equal (grid.RowTemplate.Height, r.Height));
            }
        }

        [Fact]
        public void Rows_grow_when_their_column_narrows_and_shrink_back_when_it_widens ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);

            using (form) {
                grid.DataSource = Table (Long1 + " " + Long2, Long2, "x");
                var line = OneLineHeight (grid);
                var at_1334 = grid.Rows[0].Height;

                grid.Width = 900;
                var at_900 = grid.Rows[0].Height;
                Assert.True (at_900 > at_1334, $"900: {at_900}, 1334: {at_1334}");

                grid.Width = 1800;
                Assert.True (grid.Rows[0].Height < at_1334, $"1800: {grid.Rows[0].Height}, 1334: {at_1334}");

                // A column that is not Fill: wide enough for the text on one line, the row is one line again.
                var description = grid.Columns["Description"]!;
                description.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                description.Width = 200;
                Assert.True (grid.Rows[0].Height > at_900, $"200px column: {grid.Rows[0].Height}");

                description.Width = 1000;
                Assert.Equal (line, grid.Rows[1].Height);
            }
        }

        [Fact]
        public void A_header_mode_ignores_the_cell_text ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.AllHeaders);

            using (form) {
                grid.DataSource = Table (Long1, "x");

                Assert.Equal (grid.Rows[1].Height, grid.Rows[0].Height);
                Assert.True (grid.Rows[0].Height < grid.RowTemplate.Height, $"{grid.Rows[0].Height} against the template's {grid.RowTemplate.Height}");
            }
        }

        [Fact]
        public void The_header_modes_need_visible_row_headers ()
        {
            HeadlessRenderer.Use ();
            using var hidden = new DataGridView { RowHeadersVisible = false };
            Assert.Throws<InvalidOperationException> (() => hidden.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllHeaders);

            using var sized = new DataGridView { AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedHeaders };
            Assert.Throws<InvalidOperationException> (() => sized.RowHeadersVisible = false);
        }

        [Fact]
        public void A_value_change_resizes_its_row_before_CellValueChanged ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders);

            using (form) {
                var table = Table (Long1, Long2, "x");
                grid.DataSource = table;
                var line = OneLineHeight (grid);
                var log = new List<string> ();
                grid.RowHeightChanged += (_, e) => log.Add ($"height {e.Row.Index}");
                grid.CellValueChanged += (_, e) => log.Add ($"value {e.RowIndex}");

                grid.Rows[1].Cells[2].Value = "y";

                Assert.Equal (line, grid.Rows[1].Height);
                Assert.Equal (new[] { "height 1", "value 1" }, log);

                // From the source: the bound row follows the table.
                table.Rows[2]["Description"] = Long2;
                Assert.True (grid.Rows[2].Height >= 2 * line - 3, $"row 2 is {grid.Rows[2].Height}px");
            }
        }

        [Fact]
        public void An_added_row_is_sized_as_it_arrives ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.AllCells);

            using (form) {
                var table = Table ("x");
                grid.DataSource = table;
                var line = OneLineHeight (grid);

                table.Rows.Add (2, "N", Long1);
                Assert.True (grid.Rows[1].Height >= 2 * line - 3, $"bound: {grid.Rows[1].Height}px against {line}px");
            }

            var (unbound_form, unbound) = Grid (DataGridViewAutoSizeRowsMode.AllCells);

            using (unbound_form) {
                unbound.Columns.Add ("a", "A");
                unbound.Columns.Add ("b", "B");
                unbound.Rows.Add ("1", "x");
                unbound.Rows.Add ("2", Long1 + " " + Long2);

                Assert.True (unbound.Rows[1].Height > unbound.Rows[0].Height, $"unbound: {unbound.Rows[1].Height}px against {unbound.Rows[0].Height}px");
            }
        }

        [Fact]
        public void Unwrapped_text_keeps_every_row_one_line ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.AllCells);

            using (form) {
                grid.DataSource = Table (Long1, Long2, "x");
                var wrapped = grid.Rows[0].Height;
                Assert.True (wrapped > grid.Rows[2].Height, $"wrapped: {wrapped}px against {grid.Rows[2].Height}px");

                // Upstream re-sizes on the style change; here the rows on screen are re-sized as they are painted.
                grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                HeadlessRenderer.CapturePng (form);

                Assert.True (grid.Rows[0].Height < wrapped, $"unwrapped: {grid.Rows[0].Height}px, wrapped {wrapped}px");
                Assert.Equal (grid.Rows[2].Height, grid.Rows[0].Height);
                Assert.Equal (grid.Rows[2].Height, grid.Rows[1].Height);
            }
        }

        [Fact]
        public void Rows_go_below_the_template_but_not_below_MinimumHeight ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders, before: g => g.RowTemplate.Height = 40);

            using (form) {
                grid.DataSource = Table ("x", "y");
                Assert.True (grid.Rows[0].Height < 40, $"{grid.Rows[0].Height}");
            }

            var (floored_form, floored) = Grid (DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders, before: g => {
                g.RowTemplate.Height = 40;
                g.RowTemplate.MinimumHeight = 30;
            });

            using (floored_form) {
                floored.DataSource = Table ("x", "y");
                Assert.True (floored.Rows[0].Height >= 30, $"{floored.Rows[0].Height}");
                Assert.True (floored.Rows[0].Height < 40, $"{floored.Rows[0].Height}");
            }
        }

        [Fact]
        public void Height_is_ignored_under_a_mode_and_the_old_heights_come_back_at_None ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.None);

            using (form) {
                grid.DataSource = Table (Long1, Long2, "x");
                var template = grid.RowTemplate.Height;
                grid.Rows[1].Height = 50;

                var log = new List<string> ();
                grid.RowHeightChanged += (_, e) => log.Add ($"height {e.Row.Index}");
                grid.AutoSizeRowsModeChanged += (_, e) => log.Add ($"mode {e.PreviousModeAutoSized}");

                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                Assert.Equal (new[] { "height 0", "height 1", "height 2", "mode False" }, log);
                log.Clear ();

                var sized = grid.Rows[0].Height;
                grid.Rows[0].Height = 10;
                Assert.Equal (sized, grid.Rows[0].Height);
                Assert.Empty (log);

                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                Assert.Equal (10, grid.Rows[0].Height);
                Assert.Equal (50, grid.Rows[1].Height);
                Assert.Equal (template, grid.Rows[2].Height);
                Assert.Equal ("mode True", log.Last ());
            }
        }

        [Fact]
        public void A_Displayed_mode_sizes_the_rows_on_screen_and_more_as_they_scroll_in ()
        {
            var descriptions = Enumerable.Range (0, 40).Select (i => i == 0 ? "x" : Long1).ToArray ();
            var (form, grid) = Grid (DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders);

            using (form) {
                grid.DataSource = Table (descriptions);
                var line = grid.Rows[0].Height;

                Assert.True (grid.Rows[1].Height >= 2 * line - 3, $"row 1 is {grid.Rows[1].Height}px");
                // Off the screen, a row keeps the first row's height, as upstream's copies of it do.
                Assert.Equal (line, grid.Rows[35].Height);

                grid.FirstDisplayedScrollingRowIndex = 30;
                HeadlessRenderer.CapturePng (form);

                Assert.True (grid.Rows[grid.FirstDisplayedScrollingRowIndex].Height >= 2 * line - 3,
                    $"row {grid.FirstDisplayedScrollingRowIndex} is {grid.Rows[grid.FirstDisplayedScrollingRowIndex].Height}px once painted");
            }

            var (all_form, all) = Grid (DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);

            using (all_form) {
                all.DataSource = Table (descriptions);
                Assert.True (all.Rows[35].Height >= 2 * all.Rows[0].Height - 3, $"All*: row 35 is {all.Rows[35].Height}px");
            }
        }
    }
}
