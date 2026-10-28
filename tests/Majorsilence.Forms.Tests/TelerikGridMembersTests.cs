using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using System.ComponentModel;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadGridView members Telerik code uses that the compat grid did not have: a cell style's DrawFill and
// NumberOfColors, MasterView.Rows (the template's rows), and the GridComboBoxCellElement a combo box
// column's cells are formatted through.
[Collection ("Headless")]
public class TelerikGridMembersTests
{
    private static readonly Color Magenta = Color.FromArgb (255, 0, 255);

    private static RadGridView Grid (out Form form)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 360, Height = 200 };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 120 });
        grid.Columns.Add (new GridViewComboBoxColumn ("Choice") { HeaderText = "Choice", Width = 120 });
        grid.Rows.Add ();
        grid.Rows.Add ();
        grid.Rows[0].Cells["Name"].Value = "Alice";
        grid.Rows[1].Cells["Name"].Value = string.Empty;

        form = new Form { Width = 460, Height = 300 };
        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.Render (grid).Dispose ();
        return grid;
    }

    // The second row's first cell: not current, and empty, so the probe sees only its fill.
    private static SKColor CellFill (RadGridView grid)
    {
        using var bitmap = PaintSurface.Render (grid);
        var cell = ((DataGridView) grid).GetCellBounds (1, 0);
        var inset = grid.LogicalToDeviceUnits (3);
        return bitmap.GetPixel (cell.Left + inset, cell.Top + inset);
    }

    private static bool IsMagenta (SKColor c) => c.Red == 255 && c.Green == 0 && c.Blue == 255;

    [Fact]
    public void DrawFill_off_leaves_a_customized_cell_its_own_background ()
    {
        var grid = Grid (out var form);

        using (form) {
            var style = grid.Rows[1].Cells[0].Style;

            Assert.True (style.DrawFill);
            Assert.Equal (1, style.NumberOfColors);

            style.CustomizeFill = true;
            style.BackColor = Magenta;
            Assert.True (IsMagenta (CellFill (grid)));

            style.DrawFill = false;
            Assert.False (IsMagenta (CellFill (grid)), "DrawFill = false still drew the fill.");

            style.DrawFill = true;
            style.NumberOfColors = 1;
            Assert.True (IsMagenta (CellFill (grid)));

            // Reset turns the fill back on, along with clearing the rest.
            style.DrawFill = false;
            style.Reset ();
            Assert.True (style.DrawFill);
        }
    }

    [Fact]
    public void MasterView_Rows_are_the_grids_rows ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Equal (2, grid.MasterView.Rows.Count);
            Assert.Equal ("Alice", grid.MasterView.Rows[0].Cells["Name"].Value);

            // The view's rows, in the order shown: sorted, the empty name comes first (measured on Telerik 2026.2.520,
            // MasterView.Rows[0] follows the sort).
            grid.SortDescriptors.Add (new SortDescriptor ("Name", ListSortDirection.Ascending));
            Assert.Equal ("", grid.MasterView.Rows[0].Cells["Name"].Value);
            Assert.Equal ("Alice", grid.MasterView.Rows[1].Cells["Name"].Value);
            Assert.Throws<InvalidOperationException> (() => new MasterGridViewTemplate ().Rows);
        }
    }

    [Fact]
    public void A_combo_box_columns_cells_are_formatted_through_a_GridComboBoxCellElement ()
    {
        var grid = Grid (out var form);
        // A data row's combo cell: the new row, which has every cell, is not formatted.
        grid.Rows[0].Cells["Choice"].Value = "A";

        using (form) {
            var kinds = new Dictionary<string, Type> ();
            (int Row, int Column)? picked = null;

            grid.CellFormatting += (_, e) => {
                kinds[e.CellElement.ColumnInfo!.Name] = e.CellElement.GetType ();

                if (e.CellElement is GridComboBoxCellElement combo && picked is null) {
                    combo.Text = "Picked";
                    picked = (e.RowIndex, e.ColumnIndex);
                }
            };

            PaintSurface.Render (grid).Dispose ();

            Assert.Equal (typeof (GridCellElement), kinds["Name"]);
            Assert.Equal (typeof (GridComboBoxCellElement), kinds["Choice"]);
            Assert.NotNull (picked);
            Assert.Equal ("Picked", ((DataGridView) grid).Rows[picked.Value.Row].Cells[picked.Value.Column].FormattedTextOverride);
        }
    }

    // Telerik's SortChanging hands its handlers a GridViewCollectionChangingEventArgs, and a handler declared
    // with that type -- as Telerik code declares it -- has to bind to the event and be able to veto.
    [Fact]
    public void A_SortChanging_handler_on_Telerik_s_argument_type_binds_and_can_veto ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.EnableCustomSorting = true;

            GridViewCollectionChangingEventArgs? seen = null;
            void Veto (object? sender, GridViewCollectionChangingEventArgs e)
            {
                seen = e;
                e.Cancel = true;
            }

            grid.SortChanging += Veto;
            grid.ToggleSort (grid.Columns["Name"]!.Index);

            var args = Assert.IsType<SortChangingEventArgs> (seen);
            Assert.Equal ("Name", args.PropertyName);
            Assert.Empty (grid.SortDescriptors);
        }
    }

    // Measured on Telerik UI.for.WinForms 2026.2.520: every change to SortDescriptors raises SortChanging, with or
    // without EnableCustomSorting, as a GridViewCollectionChangingEventArgs naming the change; Cancel leaves it out.
    [Fact]
    public void Each_change_to_the_sort_descriptors_raises_SortChanging_and_can_be_cancelled ()
    {
        var grid = Grid (out var form);

        using (form) {
            var seen = new List<string> ();
            var cancel = false;
            grid.SortChanging += (_, e) => {
                seen.Add (e.Action + ":" + (e.NewItems?.Count ?? 0));
                e.Cancel = cancel;
            };

            grid.SortDescriptors.Add (new SortDescriptor ("Name", ListSortDirection.Ascending));
            Assert.Equal (new[] { "Add:1" }, seen);
            Assert.Equal ("", grid.Rows[0].Cells["Name"].Value);   // empty sorts first: the sort was applied

            grid.SortDescriptors.Clear ();
            Assert.Equal (new[] { "Add:1", "Reset:0" }, seen);
            Assert.Empty (grid.SortDescriptors);

            cancel = true;
            grid.SortDescriptors.Add (new SortDescriptor ("Name", ListSortDirection.Descending));
            Assert.Empty (grid.SortDescriptors);
            Assert.Equal ("Alice", grid.Rows[0].Cells["Name"].Value);

            // A header click is one change, announced once, and a cancelled one leaves the sort alone.
            seen.Clear ();
            grid.ToggleSort (grid.Columns["Name"]!.Index);
            Assert.Single (seen);
            Assert.Empty (grid.SortDescriptors);
        }
    }

    private static RadGridView Letters (out Form form, params string[] names)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 360, Height = 260 };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 120 });

        foreach (var name in names) {
            grid.Rows.Add ();
            grid.Rows[grid.Rows.Count - 1].Cells["Name"].Value = name;
        }

        form = new Form { Width = 460, Height = 360 };
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    private static string Order (RadGridView grid) => string.Join (",", grid.Rows.Select (r => r.Cells["Name"].Value));

    // Measured on Telerik 2026.2.520 with Bravo, Alpha, Charlie sorted by Name: no EnableCustomSorting, no event and
    // Alpha,Bravo,Charlie; a handler's SortResult orders the rows; Handled defaults to true, so an empty handler
    // leaves every pair equal and the rows in their own order; Handled = false is the grid's own comparison.
    [Fact]
    public void CustomSorting_orders_the_rows_when_EnableCustomSorting_is_set ()
    {
        var grid = Letters (out var form, "Bravo", "Alpha", "Charlie");

        using (form) {
            var mode = "reverse";
            var calls = 0;
            grid.CustomSorting += (_, e) => {
                calls++;
                Assert.True (e.Handled);
                Assert.Equal (0, e.SortResult);

                if (mode == "reverse")
                    e.SortResult = -string.CompareOrdinal ((string?) e.Row1.Cells["Name"].Value, (string?) e.Row2.Cells["Name"].Value);
                else if (mode == "unhandled")
                    e.Handled = false;
            };

            grid.SortDescriptors.Add (new SortDescriptor ("Name", ListSortDirection.Ascending));
            Assert.Equal ("Alpha,Bravo,Charlie", Order (grid));
            Assert.Equal (0, calls);

            grid.EnableCustomSorting = true;
            grid.RebuildView ();
            Assert.Equal ("Charlie,Bravo,Alpha", Order (grid));
            Assert.True (calls > 0);

            mode = "unhandled";
            grid.RebuildView ();
            Assert.Equal ("Alpha,Bravo,Charlie", Order (grid));

            mode = "empty";
            grid.RebuildView ();
            Assert.Equal ("Bravo,Alpha,Charlie", Order (grid));
        }
    }
}
