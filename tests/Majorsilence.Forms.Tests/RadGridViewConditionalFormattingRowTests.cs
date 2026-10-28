using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // ApplyToRow and ApplyOnSelectedRows are two settings on Telerik's ConditionalFormattingObject. Measured on
    // Telerik (UI for WinForms 2026.2.520) with a rule on column K matching K = 1 in rows 0 and 1 of three, row 0
    // selected:
    //  - ApplyToRow (false by default; the constructor's last argument) colors a matching row: its K cell takes
    //    CellBackColor and its other cells RowBackColor. Without it only the K cell is colored. With it but no row
    //    colors, the other cells keep their own.
    //  - ApplyOnSelectedRows (true by default) lets a selected row show the rule's colors; when false the selected
    //    row keeps the selection's, and the next matching row still shows the rule's.
    [Collection ("Headless")]
    public class RadGridViewConditionalFormattingRowTests
    {
        private static readonly SKColor Lime = new (0, 255, 0);
        private static readonly SKColor Yellow = new (255, 255, 0);

        private sealed class Rendered : System.IDisposable
        {
            private readonly Form _form;
            private readonly SKBitmap _bitmap;
            private readonly RadGridView _grid;
            private readonly float _scale;

            public Rendered (ConditionalFormattingObject? rule)
            {
                HeadlessRenderer.Use ();
                _form = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size (300, 200) };
                _grid = new RadGridView { Location = new Point (10, 10), Size = new Size (280, 180), ShowGroupPanel = false, ReadOnly = true };
                _form.Controls.Add (_grid);

                var table = new DataTable ();
                table.Columns.Add ("K");
                table.Columns.Add ("V");
                table.Rows.Add ("1", "a");
                table.Rows.Add ("1", "b");
                table.Rows.Add ("2", "c");
                _grid.DataSource = table;
                _grid.AllowAddNewRow = false;
                _grid.Columns["K"]!.Width = 70;
                _grid.Columns["V"]!.Width = 80;
                if (rule is not null)
                    ((GridViewColumn) _grid.Columns["K"]!).ConditionalFormattingObjectList.Add (rule);

                _form.Show ();
                _grid.Rows[0].IsSelected = true;

                _bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (_form, 300, 200));
                _scale = _bitmap.Width / 300f;
            }

            // A pixel inside a cell, clear of its text, its borders and the current cell's outline.
            public SKColor At (int row, string column)
            {
                var cell = _grid.GetCellDisplayRectangle (_grid.ColumnIndexByName (column), row, cutOverflow: false);
                var x = 10 + cell.Right - 10;
                var y = 10 + cell.Top + cell.Height / 2;
                return _bitmap.GetPixel ((int) (x * _scale), (int) (y * _scale));
            }

            public void Dispose ()
            {
                _bitmap.Dispose ();
                _form.Dispose ();
            }
        }

        private static ConditionalFormattingObject Rule (bool applyToRow, bool onSelected = true, bool rowColor = true)
            => new ("r", ConditionTypes.Equal, "1", "", applyToRow) {
                CellBackColor = System.Drawing.Color.Lime,
                RowBackColor = rowColor ? System.Drawing.Color.Yellow : System.Drawing.Color.Empty,
                ApplyOnSelectedRows = onSelected,
            };

        [Fact]
        public void A_rule_applies_to_its_cell_and_selected_rows_by_default ()
        {
            var rule = new ConditionalFormattingObject ();
            Assert.False (rule.ApplyToRow);
            Assert.True (rule.ApplyOnSelectedRows);

            var row_rule = new ConditionalFormattingObject ("r", ConditionTypes.Equal, "1", "", true);
            Assert.True (row_rule.ApplyToRow);
            Assert.True (row_rule.ApplyOnSelectedRows);
        }

        [Fact]
        public void A_row_rule_gives_its_cell_the_cell_color_and_the_rest_of_the_row_the_row_color ()
        {
            using var grid = new Rendered (Rule (applyToRow: true));

            Assert.Equal (Lime, grid.At (1, "K"));
            Assert.Equal (Yellow, grid.At (1, "V"));
            Assert.NotEqual (Lime, grid.At (2, "K"));
            Assert.NotEqual (Yellow, grid.At (2, "V"));
        }

        [Fact]
        public void A_cell_rule_colors_only_its_cell ()
        {
            using var grid = new Rendered (Rule (applyToRow: false));

            Assert.Equal (Lime, grid.At (1, "K"));
            Assert.Equal (grid.At (2, "V"), grid.At (1, "V"));
        }

        [Fact]
        public void A_row_rule_without_row_colors_leaves_the_rest_of_the_row ()
        {
            using var grid = new Rendered (Rule (applyToRow: true, rowColor: false));

            Assert.Equal (Lime, grid.At (1, "K"));
            Assert.Equal (grid.At (2, "V"), grid.At (1, "V"));
        }

        [Fact]
        public void A_selected_row_shows_the_rule_unless_it_says_not_to ()
        {
            using var plain = new Rendered (null);
            var selection = plain.At (0, "V");

            using (var shown = new Rendered (Rule (applyToRow: true))) {
                Assert.Equal (Lime, shown.At (0, "K"));
                Assert.Equal (Yellow, shown.At (0, "V"));
            }

            using var kept = new Rendered (Rule (applyToRow: true, onSelected: false));
            Assert.Equal (selection, kept.At (0, "V"));
            Assert.Equal (plain.At (0, "K"), kept.At (0, "K"));
            Assert.Equal (Lime, kept.At (1, "K"));
            Assert.Equal (Yellow, kept.At (1, "V"));
        }
    }
}
