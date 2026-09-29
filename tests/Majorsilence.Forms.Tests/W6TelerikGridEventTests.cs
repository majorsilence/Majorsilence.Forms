using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twenty-second chunk (#176): three of the five Telerik events that still never fired
// -- RadGridView.EditorRequired and DefaultValuesNeeded, and the drop-down editor element's
// SelectedValueChanged. The other two, CreateCell and PageCollapsed, have no moment to fire here and
// are annotated in the baseline.
[Collection ("Headless")]
public class W6TelerikGridEventTests
{
    private static RadGridView Grid (out Form form)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 300, Height = 160 };
        grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 150 });
        grid.Rows.Add ();
        grid.Rows[0].Cells["Name"].Value = "Alice";

        form = new Form { Width = 400, Height = 250 };
        form.Controls.Add (grid);
        form.Show ();
        PaintSurface.Render (grid).Dispose ();
        return grid;
    }

    [Fact]
    public void EditorRequired_follows_CellBeginEdit_and_can_refuse_the_editor ()
    {
        var grid = Grid (out var form);

        using (form) {
            var order = new List<string> ();
            grid.CellBeginEdit += (_, _) => order.Add ("begin");
            grid.EditorRequired += (_, e) => { order.Add ("editor:" + e.ColumnIndex); e.Cancel = true; };

            grid.BeginEdit (0, 0);

            Assert.Equal (new[] { "begin", "editor:0" }, order);
            Assert.False (grid.IsCurrentCellInEditMode);
        }
    }

    [Fact]
    public void EditorRequired_is_not_asked_when_CellBeginEdit_refused_and_lets_the_edit_through_otherwise ()
    {
        var grid = Grid (out var form);

        using (form) {
            var asked = 0;
            var refuse = true;
            grid.CellBeginEdit += (_, e) => e.Cancel = refuse;
            grid.EditorRequired += (_, _) => asked++;

            grid.BeginEdit (0, 0);
            Assert.Equal (0, asked);

            refuse = false;
            grid.BeginEdit (0, 0);
            Assert.Equal (1, asked);
            Assert.True (grid.IsCurrentCellInEditMode);
        }
    }

    [Fact]
    public void DefaultValuesNeeded_fills_a_row_added_through_the_new_row ()
    {
        var grid = Grid (out var form);

        using (form) {
            var asked = new List<GridViewRowInfo?> ();
            grid.DefaultValuesNeeded += (_, e) => { asked.Add (e.Row); e.Row!.Cells["Name"].Value = "(new)"; };

            var added = grid.AddNewRow ();

            Assert.NotNull (added);
            Assert.Single (asked);
            Assert.Same (added!.DataRow, asked[0]!.DataRow);
            Assert.Equal ("(new)", added.Cells["Name"].Value);
            Assert.Equal (2, grid.Rows.Count);
        }
    }

    [Fact]
    public void The_drop_down_editor_element_announces_a_new_value ()
    {
        var editor = new PropertyGridDropDownListEditor ();
        var element = (BaseDropDownListEditorElement) editor.EditorElement;
        var changes = 0;
        element.SelectedValueChanged += (_, _) => changes++;

        element.SelectedValue = 5;
        element.SelectedValue = 5;   // the same value is not a change
        element.SelectedValue = 6;

        Assert.Equal (2, changes);
        Assert.Equal (6, element.SelectedValue);
    }
}
