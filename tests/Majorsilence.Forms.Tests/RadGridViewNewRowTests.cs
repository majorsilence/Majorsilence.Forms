using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik's grid adds rows through its own "Click here to add a new row" row only, and shows none with
    // AllowAddNewRow off. The WinForms grid underneath also laid out its own new row, a blank "*" row after the
    // data (with an unticked box under a check box column), whatever AllowAddNewRow or ReadOnly said.
    public class RadGridViewNewRowTests
    {
        public class Approval
        {
            public bool Approved { get; set; }
            public string PoNumber { get; set; } = string.Empty;
        }

        [Fact]
        public void A_grid_with_AllowAddNewRow_off_has_no_new_row ()
        {
            using var grid = new RadGridView { DataSource = new List<Approval> () };
            grid.AllowAddNewRow = false;

            var engine = (DataGridView) grid;
            Assert.Equal (-1, engine.NewRowIndex);
            Assert.Empty (engine.Rows.Cast<DataGridViewRow> ());
        }

        [Fact]
        public void A_read_only_grid_has_no_new_row ()
        {
            using var grid = new RadGridView { ReadOnly = true, DataSource = new List<Approval> { new () { PoNumber = "1" } } };

            var engine = (DataGridView) grid;
            Assert.Equal (-1, engine.NewRowIndex);
            Assert.Single (engine.Rows.Cast<DataGridViewRow> ());
        }

        [Fact]
        public void An_editable_grid_has_only_Teleriks_new_row ()
        {
            using var grid = new RadGridView { DataSource = new List<Approval> { new () { PoNumber = "1" } } };

            var engine = (DataGridView) grid;
            Assert.Equal (-1, engine.NewRowIndex);
            Assert.Equal (2, engine.Rows.Count);
            Assert.Equal (1, grid.RowCount);
        }
    }
}
