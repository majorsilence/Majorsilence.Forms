using System;
using System.Collections.Generic;
using System.Data;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A bind makes the first row current, and Telerik's RadGridView ANNOUNCES it: forms fill a detail
    // pane from CurrentRowChanged or SelectionChanged, and on Telerik that pane is filled the moment the
    // grid is bound. Here the base grid seeded the current cell silently, so only DataBindingComplete was
    // raised and the pane stayed empty until the user clicked a row.
    //
    // Measured on UI for WinForms 2026.2.520, RadGridView on a shown form:
    //
    //   bind 2 rows               CurrentRowChanged (old null, new row 0), CurrentCellChanged,
    //                             SelectionChanged (1 selected), DataBindingComplete
    //   rebind a new 2-row table  CurrentCellChanged, CurrentRowChanged (old null, new row 0),
    //   (row 2 was current)       CurrentCellChanged, SelectionChanged, DataBindingComplete -- row 0 current
    //   bind 0 rows,              CurrentRowChanged (old null, new null), CurrentCellChanged,
    //   AllowAddNewRow = false    DataBindingComplete (no SelectionChanged)
    //   bind 0 rows, new row on   CurrentRowChanged, CurrentCellChanged, SelectionChanged (0 selected),
    //                             DataBindingComplete
    //   the same DataSource again nothing
    //   rows added to the table   no DataBindingComplete; each added row becomes current:
    //                             CurrentRowChanged (old previous, new added), CurrentCellChanged, SelectionChanged
    //   grid on an unshown form   nothing when bound; the bind's four events when the form is shown
    //   grid never parented       nothing at all
    [Collection ("Headless")]
    public class RadGridViewBindNotificationTests
    {
        private static DataTable Table (int rows)
        {
            var table = new DataTable ("T");
            table.Columns.Add ("Id", typeof (int));
            table.Columns.Add ("Comment");

            for (var i = 0; i < rows; i++)
                table.Rows.Add (i + 1, "c" + (i + 1));

            return table;
        }

        private static string Row (GridViewRowInfo? row) => row is null ? "null" : "row" + row.Index;

        private static List<string> Record (RadGridView grid)
        {
            var log = new List<string> ();
            grid.CurrentRowChanged += (_, e) => log.Add ($"CurrentRowChanged {Row (e.OldRow)}->{Row (e.CurrentRow)}");
            grid.CurrentCellChanged += (_, _) => log.Add ("CurrentCellChanged");
            grid.SelectionChanged += (_, _) => log.Add ($"SelectionChanged {grid.SelectedRows.Count}");
            grid.DataBindingComplete += (_, _) => log.Add ($"DataBindingComplete {Row (grid.CurrentRow)}");
            return log;
        }

        private static (Form Form, RadGridView Grid) Shown (bool allowAddNewRow = false)
        {
            HeadlessRenderer.Use ();
            var form = new Form { Width = 600, Height = 400 };
            var grid = new RadGridView { Dock = DockStyle.Fill, AllowAddNewRow = allowAddNewRow };
            form.Controls.Add (grid);
            form.Show ();
            return (form, grid);
        }

        [Fact]
        public void Binding_rows_announces_the_first_row_before_DataBindingComplete ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var log = Record (grid);

            grid.DataSource = Table (2);

            Assert.Equal (new[] {
                "CurrentRowChanged null->row0",
                "CurrentCellChanged",
                "SelectionChanged 1",
                "DataBindingComplete row0",
            }, log);
        }

        [Fact]
        public void A_detail_pane_filled_from_SelectionChanged_is_filled_by_the_bind ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var notes = new TextBox ();
            form.Controls.Add (notes);
            grid.SelectionChanged += (_, _) => notes.Text = grid.CurrentRow?.Cells["Comment"].Value?.ToString () ?? "";

            grid.DataSource = Table (2);

            Assert.Equal ("c1", notes.Text);
        }

        [Fact]
        public void Rebinding_lands_on_the_first_row_and_announces_it ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            grid.DataSource = Table (3);
            grid.CurrentRow = grid.Rows[2];
            var log = Record (grid);

            grid.DataSource = Table (3);

            Assert.Equal (new[] {
                "CurrentCellChanged",
                "CurrentRowChanged null->row0",
                "CurrentCellChanged",
                "SelectionChanged 1",
                "DataBindingComplete row0",
            }, log);
            Assert.Equal (0, grid.CurrentRow!.Index);
        }

        [Fact]
        public void Binding_no_rows_after_rows_announces_that_no_row_is_current ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            grid.DataSource = Table (2);
            var log = Record (grid);

            grid.DataSource = Table (0);

            Assert.Equal (new[] {
                "CurrentRowChanged null->null",
                "CurrentCellChanged",
                "DataBindingComplete null",
            }, log);
        }

        [Fact]
        public void Binding_no_rows_with_the_new_row_shown_announces_it_unselected ()
        {
            var (form, grid) = Shown (allowAddNewRow: true);
            using var _ = form;
            var log = Record (grid);

            grid.DataSource = Table (0);

            // Upstream's CurrentRow is the GridViewNewRowInfo here; this grid has no such type, so the
            // placeholder reads as null. The events and the empty selection are what is pinned.
            Assert.Equal (new[] {
                "CurrentRowChanged null->null",
                "CurrentCellChanged",
                "SelectionChanged 0",
                "DataBindingComplete null",
            }, log);
        }

        [Fact]
        public void Assigning_the_same_DataSource_again_raises_nothing ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var table = Table (2);
            grid.DataSource = table;
            var log = Record (grid);

            grid.DataSource = table;

            Assert.Empty (log);
        }

        [Fact]
        public void A_row_added_to_the_bound_table_becomes_current_without_DataBindingComplete ()
        {
            var (form, grid) = Shown ();
            using var _ = form;
            var table = Table (2);
            grid.DataSource = table;
            var log = Record (grid);

            table.Rows.Add (3, "c3");

            Assert.Equal (new[] {
                "CurrentRowChanged row0->row2",
                "CurrentCellChanged",
                "SelectionChanged 1",
            }, log);
            Assert.Equal (2, grid.CurrentRow!.Index);
        }

        [Fact]
        public void A_grid_bound_before_its_form_is_shown_announces_the_bind_when_shown ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 600, Height = 400 };
            var grid = new RadGridView { Dock = DockStyle.Fill, AllowAddNewRow = false };
            form.Controls.Add (grid);
            var log = Record (grid);

            grid.DataSource = Table (2);
            Assert.Empty (log);

            form.Show ();

            Assert.Equal (new[] {
                "CurrentRowChanged null->row0",
                "CurrentCellChanged",
                "SelectionChanged 1",
                "DataBindingComplete row0",
            }, log);
        }

        [Fact]
        public void A_grid_bound_in_Load_announces_the_bind_there ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 600, Height = 400 };
            var grid = new RadGridView { Dock = DockStyle.Fill, AllowAddNewRow = false };
            form.Controls.Add (grid);
            var log = Record (grid);
            var during_load = 0;
            form.Load += (_, _) => { grid.DataSource = Table (2); during_load = log.Count; };

            form.Show ();

            Assert.Equal (4, during_load);
            Assert.Equal (4, log.Count);
        }

        [Fact]
        public void A_grid_never_parented_raises_nothing_on_bind ()
        {
            HeadlessRenderer.Use ();
            using var grid = new RadGridView ();
            var log = Record (grid);

            grid.DataSource = Table (2);

            Assert.Empty (log);
        }
    }
}
