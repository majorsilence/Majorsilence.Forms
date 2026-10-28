using System.Data;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A column sized to its content by its AutoSizeMode -- its own, or the grid's AutoSizeColumnsMode -- stays at the
    // width its content needs, as upstream keeps re-fitting it. Measured on WinForms (.NET 10): in a DisplayedCells
    // grid whose columns were set to 70, 225, 100 and 100 after binding, they came out 72, 242, 110 and 103 (each the
    // width of its content), and setting one to 50 once the grid was shown left it at 110.
    [Collection ("Headless")]
    public class DataGridViewContentAutoSizeTests
    {
        private static (Form form, DataGridView grid) Grid (DataGridViewAutoSizeColumnsMode mode)
        {
            HeadlessRenderer.Use ();
            var form = new Form { ClientSize = new Size (700, 450) };
            var grid = new DataGridView { Location = new Point (12, 19), Size = new Size (612, 388), AutoSizeColumnsMode = mode, AllowUserToAddRows = false };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Columns.Add ("Account");
            table.Columns.Add ("A rather long column caption");
            table.Rows.Add ("BMO1", "01-31000-151-000-000", "x");
            table.Rows.Add ("CAP1", "60-31000-151", "y");
            grid.DataSource = table;
            form.Show ();
            return (form, grid);
        }

        [Fact]
        public void A_DisplayedCells_column_keeps_the_width_of_its_content ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeColumnsMode.DisplayedCells);

            using (form) {
                var account = grid.Columns["Account"]!;
                var fitted = account.GetPreferredWidth (DataGridViewAutoSizeColumnMode.DisplayedCells, fixedHeight: true);

                account.Width = 100;
                Assert.Equal (fitted, account.Width);
                Assert.True (fitted > 100, $"the account number needs more than 100px, measured {fitted}");

                account.Width = 50;
                Assert.Equal (fitted, account.Width);
            }
        }

        [Fact]
        public void An_ExceptHeader_mode_ignores_the_caption_and_DisplayedCells_counts_it ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader);

            using (form) {
                var captioned = grid.Columns["A rather long column caption"]!;
                var except = captioned.Width;

                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                var with_header = captioned.Width;

                // The one-letter cells are far narrower than the caption: only the mode that counts the header is wide.
                Assert.True (with_header > except + 50, $"DisplayedCells {with_header} should be much wider than AllCellsExceptHeader {except}");
            }
        }

        [Fact]
        public void A_None_column_keeps_the_width_it_is_given ()
        {
            var (form, grid) = Grid (DataGridViewAutoSizeColumnsMode.None);

            using (form) {
                grid.Columns["Account"]!.Width = 60;
                Assert.Equal (60, grid.Columns["Account"]!.Width);
            }
        }
    }
}
