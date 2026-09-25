using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // DGV-26: the combo-box column showed the raw value and edited as free text.
    //
    // The universal lookup column -- a CustomerId cell that displays the customer's NAME -- rendered the
    // id, because DataSource/DisplayMember/ValueMember/Items were stored and read by nothing. Editing
    // one offered a TextBox, so a user could type anything into a column whose whole purpose is a fixed
    // list, and DataGridViewComboBoxEditingControl existed and was never constructed.
    [Collection ("Headless")]
    public class DataGridViewComboBoxColumnTests
    {
        private sealed class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private sealed class Order
        {
            public int CustomerId { get; set; }
        }

        private static readonly List<Customer> Customers = new () {
            new () { Id = 1, Name = "Ada" },
            new () { Id = 2, Name = "Grace" },
            new () { Id = 3, Name = "Katherine" },
        };

        // A grid with one lookup column, showing an id and expected to render a name.
        private static DataGridView LookupGrid (out Form form, object? value = null)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 520, Height = 340 };
            var grid = new DataGridView { Width = 400, Height = 200 };
            grid.Columns.Add (new DataGridViewComboBoxColumn {
                HeaderText = "Customer",
                Name = "Customer",
                Width = 160,
                DataSource = Customers,
                ValueMember = nameof (Customer.Id),
                DisplayMember = nameof (Customer.Name),
            });
            var row = new DataGridViewRow ();
            row.Cells.Add (new DataGridViewCell { Value = value ?? 2 });
            grid.Rows.Add (row);
            form.Controls.Add (grid);
            form.Show ();
            PaintSurface.RenderOnForm (grid).Dispose ();

            return grid;
        }

        // ---------------- the display lookup

        [Fact]
        public void A_lookup_column_shows_the_display_member_not_the_value ()
        {
            // The finding's own test: cell value 2 should read "Grace".
            var grid = LookupGrid (out var form);
            using var _form = form;

            try {
                string? formatted = null;
                grid.CellPainting += (_, e) => {
                    if (e.RowIndex == 0)
                        formatted = e.FormattedValue?.ToString ();
                };

                PaintSurface.RenderOnForm (grid).Dispose ();

                Assert.Equal ("Grace", formatted);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_cells_FormattedValue_is_the_display_member ()
        {
            var grid = LookupGrid (out var form);
            using var _form = form;

            try {
                Assert.Equal ("Grace", grid.Rows[0].Cells[0].FormattedValue);
                // The stored value is untouched -- a lookup column displays a name and stores an id.
                Assert.Equal (2, grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_unmatched_value_falls_back_to_the_value_itself ()
        {
            // Upstream raises DataError here. Until that exists, showing the id beats showing nothing:
            // a blank cell where a name should be is the harder bug to diagnose. Recorded as a
            // deliberate deviation, not an oversight.
            var grid = LookupGrid (out var form, value: 99);
            using var _form = form;

            try {
                Assert.Equal ("99", grid.Rows[0].Cells[0].FormattedValue);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_statically_populated_column_looks_up_through_its_Items ()
        {
            // Items rather than DataSource, with the same ValueMember/DisplayMember pair. Written with
            // OBJECT items deliberately: with plain strings the value and its display text are the same
            // string, so the test could not tell a real lookup from the fall-back-to-the-value path.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DataGridView { Width = 400, Height = 200 };
            var column = new DataGridViewComboBoxColumn {
                HeaderText = "Customer",
                Width = 120,
                ValueMember = nameof (Customer.Id),
                DisplayMember = nameof (Customer.Name),
            };
            column.Items.AddRange (Customers);
            grid.Columns.Add (column);
            var row = new DataGridViewRow ();
            row.Cells.Add (new DataGridViewCell { Value = 3 });
            grid.Rows.Add (row);
            form.Controls.Add (grid);
            form.Show ();

            try {
                Assert.Equal ("Katherine", grid.Rows[0].Cells[0].FormattedValue);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_column_of_plain_items_shows_the_item_itself ()
        {
            // GUARD, not proof: with no member to look up by, the value IS the item and the fallback is
            // the correct answer. It pins that the lookup did not break the simple case.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DataGridView { Width = 400, Height = 200 };
            var column = new DataGridViewComboBoxColumn { HeaderText = "Size", Width = 120 };
            column.Items.AddRange (new object[] { "Small", "Medium", "Large" });
            grid.Columns.Add (column);
            var row = new DataGridViewRow ();
            row.Cells.Add (new DataGridViewCell { Value = "Medium" });
            grid.Rows.Add (row);
            form.Controls.Add (grid);
            form.Show ();

            try {
                Assert.Equal ("Medium", grid.Rows[0].Cells[0].FormattedValue);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_plain_column_is_unaffected_by_the_lookup ()
        {
            // GUARD, not proof: only combo columns look anything up. It pins that adding the lookup did
            // not change what an ordinary cell displays.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DataGridView { Width = 400, Height = 200 };
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "Id", Width = 120 });
            var row = new DataGridViewRow ();
            row.Cells.Add (new DataGridViewCell { Value = 2 });
            grid.Rows.Add (row);
            form.Controls.Add (grid);
            form.Show ();

            try {
                Assert.Equal ("2", grid.Rows[0].Cells[0].FormattedValue);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_CellFormatting_handler_still_wins ()
        {
            // GUARD, not proof: the handler path was always consulted first. It pins that the lookup
            // did not displace it.
            var grid = LookupGrid (out var form);
            using var _form = form;

            try {
                grid.CellFormatting += (_, e) => { e.Value = "overridden"; e.FormattingApplied = true; };
                string? formatted = null;
                grid.CellPainting += (_, e) => { if (e.RowIndex == 0) formatted = e.FormattedValue?.ToString (); };

                PaintSurface.RenderOnForm (grid).Dispose ();

                Assert.Equal ("overridden", formatted);
            } finally {
                form.Close ();
            }
        }

        // ---------------- the editor

        [Fact]
        public void Editing_a_lookup_column_hosts_a_combo_box ()
        {
            // BeginEdit always made a TextBox, so a column whose whole purpose is a fixed list let the
            // user type anything.
            var grid = LookupGrid (out var form);
            using var _form = form;

            try {
                grid.MoveCurrentCell (0, 0);

                Assert.True (grid.BeginEdit (true));

                var editor = Assert.IsType<DataGridViewComboBoxEditingControl> (grid.EditingControl);
                Assert.Same (grid, editor.EditingControlDataGridView);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_editor_opens_on_the_cells_current_value ()
        {
            var grid = LookupGrid (out var form);
            using var _form = form;

            try {
                grid.MoveCurrentCell (0, 0);
                grid.BeginEdit (true);

                var editor = (DataGridViewComboBoxEditingControl)grid.EditingControl!;

                Assert.Equal (2, editor.SelectedValue);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Committing_the_editor_stores_the_value_member_not_the_text ()
        {
            // The point of a lookup column: the user picks "Katherine" and the cell stores 3.
            var grid = LookupGrid (out var form);
            using var _form = form;

            try {
                grid.MoveCurrentCell (0, 0);
                grid.BeginEdit (true);
                var editor = (DataGridViewComboBoxEditingControl)grid.EditingControl!;

                editor.SelectedValue = 3;
                grid.EndEdit ();

                Assert.Equal (3, grid.Rows[0].Cells[0].Value);
                Assert.Equal ("Katherine", grid.Rows[0].Cells[0].FormattedValue);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Committing_a_lookup_edit_writes_through_to_the_bound_object ()
        {
            HeadlessRenderer.Use ();
            var orders = new BindingList<Order> { new () { CustomerId = 1 } };
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DataGridView { Width = 400, Height = 200, AutoGenerateColumns = false };
            grid.Columns.Add (new DataGridViewComboBoxColumn {
                HeaderText = "Customer",
                DataPropertyName = nameof (Order.CustomerId),
                Width = 160,
                DataSource = Customers,
                ValueMember = nameof (Customer.Id),
                DisplayMember = nameof (Customer.Name),
            });
            grid.DataSource = orders;
            form.Controls.Add (grid);
            form.Show ();

            try {
                PaintSurface.RenderOnForm (grid).Dispose ();
                grid.MoveCurrentCell (0, 0);
                grid.BeginEdit (true);
                var editor = (DataGridViewComboBoxEditingControl)grid.EditingControl!;

                editor.SelectedValue = 3;
                grid.EndEdit ();

                Assert.Equal (3, orders[0].CustomerId);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_plain_column_still_edits_in_a_text_box ()
        {
            // GUARD, not proof: the text box was the only editor before. It pins that adding a second
            // editor type did not change what an ordinary column gets.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 520, Height = 340 };
            var grid = new DataGridView { Width = 400, Height = 200 };
            grid.Columns.Add (new DataGridViewColumn { HeaderText = "Name", Width = 120 });
            grid.Rows.Add ("typed");
            form.Controls.Add (grid);
            form.Show ();

            try {
                PaintSurface.RenderOnForm (grid).Dispose ();
                grid.MoveCurrentCell (0, 0);
                grid.BeginEdit (true);

                // A DataGridViewTextBoxEditingControl, which IS a TextBox, as upstream (W6 mechanisms).
                var editor = Assert.IsAssignableFrom<TextBox> (grid.EditingControl);
                editor.Text = "changed";
                grid.EndEdit ();

                Assert.Equal ("changed", grid.Rows[0].Cells[0].Value);
            } finally {
                form.Close ();
            }
        }
    }
}
