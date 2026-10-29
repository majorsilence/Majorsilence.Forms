using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Cell-element and editor members of RadGridView that Telerik code uses: the CommandCellClick sender, a
// cell element's Dispose, TableElement.Invalidate, Data.GroupDescriptor, GridGroupContentCellElement, and
// the RadDropDownListEditor a combo cell is edited with. Expectations measured on UI.for.WinForms 2026.2.520.
[Collection ("Headless")]
public class TelerikGridCellMembersTests
{
    private sealed record Dept (int Id, string Name);

    private sealed class DrivenGrid : RadGridView
    {
        internal void ClickAt (Point p)
        {
            OnMouseDown (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));
            OnMouseUp (new MouseEventArgs (MouseButtons.Left, 1, p.X, p.Y, Point.Empty));
        }

        internal void DoubleClickAt (Point p) => OnDoubleClick (new MouseEventArgs (MouseButtons.Left, 2, p.X, p.Y, Point.Empty));

        // Logical points, as MouseEventArgs carry them.
        internal Point CellCentre (int rowIndex, int columnIndex)
        {
            var b = GetCellBounds (rowIndex, columnIndex);
            return DeviceToLogicalUnits (new Point (b.Left + b.Width / 2, b.Top + b.Height / 2));
        }
    }

    private static DrivenGrid Grid (out Form form, bool combo = false)
    {
        HeadlessRenderer.Use ();

        var grid = new DrivenGrid { Width = 420, Height = 220, AllowAddNewRow = false };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 120 });
        grid.Columns.Add (new GridViewCommandColumn ("Action") { HeaderText = "Action", Width = 120 });

        if (combo)
            grid.Columns.Add (new GridViewComboBoxColumn ("Dept") { HeaderText = "Dept", Width = 120, DataSource = new[] { new Dept (1, "Roads"), new Dept (2, "Parks") }, ValueMember = "Id", DisplayMember = "Name" });

        grid.Rows.Add ();
        grid.Rows.Add ();
        grid.Rows[0].Cells["Name"].Value = "Alice";
        grid.Rows[1].Cells["Name"].Value = "Bob";

        if (combo) {
            grid.Rows[0].Cells["Dept"].Value = 1;
            grid.Rows[1].Cells["Dept"].Value = 2;
        }

        form = new Form { Width = 520, Height = 320 };
        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.Render (grid).Dispose ();
        return grid;
    }

    [Fact]
    public void CommandCellClick_comes_from_the_command_cell_and_only_from_it ()
    {
        var grid = Grid (out var form);

        using (form) {
            var senders = new System.Collections.Generic.List<object?> ();
            GridViewCellEventArgs? last = null;
            var cell_clicks = 0;

            grid.CommandCellClick += (sender, e) => { senders.Add (sender); last = e; };
            grid.CellClick += (_, _) => cell_clicks++;

            grid.ClickAt (grid.CellCentre (1, 1));

            var element = Assert.IsType<GridCommandCellElement> (Assert.Single (senders));
            Assert.Equal ("Action", element.ColumnInfo!.Name);
            Assert.Equal ("Bob", element.RowInfo!.Cells["Name"].Value);
            Assert.Equal ("Action", last!.Column!.Name);
            Assert.Same (element, last.CellElement);
            Assert.Equal (1, cell_clicks);

            // A text cell raises CellClick only.
            grid.ClickAt (grid.CellCentre (0, 0));
            Assert.Single (senders);
            Assert.Equal (2, cell_clicks);
        }
    }

    [Fact]
    public void A_disposed_command_cell_lets_go_of_its_cell_and_the_next_click_gets_a_new_one ()
    {
        var grid = Grid (out var form);

        using (form) {
            GridCommandCellElement? first = null, second = null;

            grid.CommandCellClick += (sender, _) => {
                var cell = (GridCommandCellElement) sender!;

                if (first is null) {
                    first = cell;
                    Assert.False (cell.IsDisposed);
                    cell.Dispose ();
                } else {
                    second = cell;
                }
            };

            grid.ClickAt (grid.CellCentre (0, 1));
            Assert.True (first!.IsDisposed);
            Assert.Null (first.ColumnInfo);
            Assert.Null (first.RowInfo);
            first.Dispose ();

            grid.ClickAt (grid.CellCentre (0, 1));
            Assert.NotSame (first, second);
            Assert.False (second!.IsDisposed);
            Assert.Equal ("Action", second.ColumnInfo!.Name);
        }
    }

    [Fact]
    public void TableElement_Invalidate_repaints_the_grid ()
    {
        var grid = Grid (out var form);

        using (form) {
            var invalidated = 0;
            grid.Invalidated += (_, _) => invalidated++;

            grid.TableElement.Invalidate ();
            Assert.Equal (1, invalidated);

            grid.TableElement.Invalidate (true);
            Assert.Equal (2, invalidated);
        }

        new GridTableElement ().Invalidate ();
    }

    [Fact]
    public void A_Data_GroupDescriptor_groups_the_grid ()
    {
        var grid = Grid (out var form);

        using (form) {
            var descriptor = new Majorsilence.Forms.Telerik.Data.GroupDescriptor ();
            descriptor.GroupNames.Add ("Name", ListSortDirection.Ascending);
            grid.GroupDescriptors.Add (descriptor);

            Assert.Equal (2, ((DataGridView) grid).Rows.Cast<DataGridViewRow> ().Count (RadGridView.IsGroupRow));
        }
    }

    [Fact]
    public void A_group_content_cell_belongs_to_its_column_and_row ()
    {
        var grid = Grid (out var form);

        using (form) {
            var column = grid.Columns["Name"]!;
            var row = new GridRowElement { RowInfo = grid.Rows[0] };
            var cell = new GridGroupContentCellElement (column, row);

            Assert.Same (column, cell.ColumnInfo);
            Assert.Same (grid.Rows[0].DataRow, cell.RowInfo!.DataRow);
            Assert.IsAssignableFrom<GridCellElement> (cell);
        }
    }

    [Fact]
    public void A_combo_cell_is_edited_with_the_grids_drop_down_list_editor ()
    {
        var grid = Grid (out var form, combo: true);

        using (form) {
            Assert.Null (grid.ActiveEditor);

            var seen = new System.Collections.Generic.List<IInputEditor?> ();
            grid.CellBeginEdit += (_, e) => {
                seen.Add (grid.ActiveEditor);

                if (grid.ActiveEditor is RadDropDownListEditor editor) {
                    Assert.Equal (grid.Rows[e.RowIndex].Cells["Dept"].Value, editor.Value);
                    editor.DropDownStyle = RadDropDownStyle.DropDownList;
                    editor.DropDownSizingMode = SizingMode.UpDownAndRightBottom;
                }
            };

            grid.DoubleClickAt (grid.CellCentre (0, 2));

            var first = Assert.IsType<RadDropDownListEditor> (Assert.Single (seen));
            Assert.Same (first, grid.ActiveEditor);
            Assert.Equal (RadDropDownStyle.DropDownList, ((RadDropDownListEditorElement) first.EditorElement).DropDownStyle);
            Assert.Equal (SizingMode.UpDownAndRightBottom, ((RadDropDownListEditorElement) first.EditorElement).DropDownSizingMode);

            // Picking a value ends the edit: no editor is active once the drop-down closes.
            Assert.NotNull (grid.OpenComboList);
            grid.OpenComboList!.SelectedIndex = 1;
            Assert.Equal (2, grid.Rows[0].Cells["Dept"].Value);
            Assert.Null (grid.ActiveEditor);
            Assert.Null (grid.OpenComboList);

            // The grid keeps the one editor, with what a handler set on it.
            grid.DoubleClickAt (grid.CellCentre (1, 2));
            Assert.Same (first, seen[1]);
            Assert.Equal (SizingMode.UpDownAndRightBottom, ((RadDropDownListEditor) seen[1]!).DropDownSizingMode);
        }
    }

    [Fact]
    public void The_drop_down_opens_on_the_editors_value ()
    {
        var grid = Grid (out var form, combo: true);

        using (form) {
            grid.CellBeginEdit += (_, _) => ((RadDropDownListEditor) grid.ActiveEditor!).Value = 2;

            grid.DoubleClickAt (grid.CellCentre (0, 2));

            Assert.Equal (1, grid.OpenComboList!.SelectedIndex);
        }
    }

    [Fact]
    public void A_refused_combo_edit_opens_no_drop_down_and_leaves_no_editor ()
    {
        var grid = Grid (out var form, combo: true);

        using (form) {
            grid.CellBeginEdit += (_, e) => e.Cancel = true;

            grid.DoubleClickAt (grid.CellCentre (0, 2));

            Assert.Null (grid.ActiveEditor);
            Assert.Null (grid.OpenComboList);
            Assert.Equal (1, grid.Rows[0].Cells["Dept"].Value);
        }
    }

    [Fact]
    public void A_new_drop_down_list_editor_starts_editable_and_not_resizable ()
    {
        var editor = new RadDropDownListEditor ();

        Assert.Equal (RadDropDownStyle.DropDown, editor.DropDownStyle);
        Assert.Equal (SizingMode.None, editor.DropDownSizingMode);
        Assert.Equal (3, (int) SizingMode.UpDownAndRightBottom);
    }
}
