using System.Data;
using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A column inserted into a grid takes its index as its display position, and the columns displayed at or after
    // it move along one, as upstream's. Measured (.NET 10, Windows 11) on a DataGridView bound to A, B, C, D:
    // inserting X at 1 gave A:0, X:1, B:2, C:3, D:4; removing it, A:0, B:1, C:2, D:3; after D was displayed at 1,
    // inserting Y at 1 gave A:0, Y:1, D:2, B:3, C:4. Here the new column kept its index as its display position while
    // the others kept theirs, so two shared a position and drew in collection order: a Telerik layout loaded after
    // inserting command columns showed Department before Supervisor, the reverse of the layout.
    public class DataGridViewInsertDisplayIndexTests
    {
        private static string Display (DataGridView grid)
            => string.Join (",", grid.Columns.Cast<DataGridViewColumn> ().Select (c => $"{c.Name}:{c.DisplayIndex}"));

        private static DataTable Table (params string[] columns)
        {
            var table = new DataTable ();
            foreach (var c in columns)
                table.Columns.Add (c);
            table.Rows.Add (columns.Select (c => (object) (c + "1")).ToArray ());
            return table;
        }

        [Fact]
        public void An_inserted_column_takes_its_index_as_its_display_position ()
        {
            using var grid = new DataGridView { DataSource = Table ("A", "B", "C", "D") };

            grid.Columns.Insert (1, new DataGridViewTextBoxColumn { Name = "X" });
            Assert.Equal ("A:0,X:1,B:2,C:3,D:4", Display (grid));

            grid.Columns.Remove ("X");
            Assert.Equal ("A:0,B:1,C:2,D:3", Display (grid));
        }

        [Fact]
        public void An_inserted_column_moves_along_the_columns_displayed_from_its_position ()
        {
            using var grid = new DataGridView { DataSource = Table ("A", "B", "C", "D") };
            grid.Columns["D"]!.DisplayIndex = 1;

            grid.Columns.Insert (1, new DataGridViewTextBoxColumn { Name = "Y" });

            Assert.Equal ("A:0,Y:1,B:3,C:4,D:2", Display (grid));
        }

        [Fact]
        public void A_layout_loaded_after_inserting_pinned_columns_is_displayed_in_its_order ()
        {
            using var grid = new RadGridView { DataSource = Table ("Id", "Name", "Dept", "Supervisor", "Status") };
            var buttons = new[] { "b1", "b2", "b3" };
            for (var i = 0; i < buttons.Length; i++)
                grid.Columns.Insert (i + 1, new GridViewCommandColumn { Name = buttons[i], PinPosition = PinnedColumnPosition.Right });

            static string Column (string name, string type = "GridViewTextBoxColumn")
                => $"<Telerik.WinControls.UI.{type} FieldName=\"{name}\" Name=\"{name}\" IsVisible=\"True\" HeaderText=\"{name}\" />";
            grid.LoadLayoutFromString ("<RadGridView><MasterTemplate><Columns>" + Column ("Id")
                + string.Concat (buttons.Select (b => Column (b, "GridViewCommandColumn")))
                + Column ("Name") + Column ("Supervisor") + Column ("Dept") + Column ("Status") + "</Columns></MasterTemplate></RadGridView>");

            var displayed = ((DataGridView) grid).Columns.Cast<DataGridViewColumn> ().OrderBy (c => c.DisplayIndex).Select (c => c.Name);
            Assert.Equal (new[] { "Id", "b1", "b2", "b3", "Name", "Supervisor", "Dept", "Status" }, displayed);
        }
    }
}
