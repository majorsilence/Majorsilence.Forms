using System.Data;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadGridView's DataBindingComplete and CustomFiltering, as Telerik raises them. Measured on Telerik
// UI.for.WinForms 2026.2.520 with a three-row DataTable.
[Collection ("Headless")]
public class TelerikGridBindingEventTests
{
    private static DataTable Table ()
    {
        var table = new DataTable ();
        table.Columns.Add ("Name", typeof (string));
        table.Columns.Add ("Amount", typeof (int));
        table.Rows.Add ("Bravo", 2);
        table.Rows.Add ("Alpha", 1);
        table.Rows.Add ("Charlie", 3);
        return table;
    }

    private static RadGridView Grid (out Form form)
    {
        HeadlessRenderer.Use ();
        var grid = new RadGridView { Width = 360, Height = 200 };
        form = new Form { Width = 460, Height = 300 };
        form.Controls.Add (grid);
        form.Show ();
        return grid;
    }

    // Telerik: one Reset when DataSource is set; nothing for a row added to the bound table; nothing when the
    // source is cleared. The core grid, like upstream WinForms', also follows each list change.
    [Fact]
    public void DataBindingComplete_follows_the_bind_only ()
    {
        var grid = Grid (out var form);

        using (form) {
            var seen = new List<string> ();
            void Handler (object sender, GridViewBindingCompleteEventArgs e) => seen.Add (e.ListChangedType.ToString ());
            grid.DataBindingComplete += Handler;

            var table = Table ();
            grid.DataSource = table;
            Assert.Equal (new[] { "Reset" }, seen);

            table.Rows.Add ("Delta", 4);
            Assert.Equal (new[] { "Reset" }, seen);

            grid.DataSource = null;
            Assert.Equal (new[] { "Reset" }, seen);

            grid.DataSource = Table ();
            Assert.Equal (new[] { "Reset", "Reset" }, seen);
        }
    }

    // Telerik: a handler that only sets e.Visible decides the row -- Handled is true unless the handler clears it --
    // and e.Row is the row's GridViewDataRowInfo, its cells read by name.
    [Fact]
    public void A_CustomFiltering_handler_that_sets_Visible_hides_the_row ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.DataSource = Table ();
            grid.EnableFiltering = true;
            grid.EnableCustomFiltering = true;

            var handled = new List<bool> ();
            var clear = false;
            grid.CustomFiltering += (_, e) => {
                handled.Add (e.Handled);
                e.Visible = (string?) e.Row!.Cells["Name"].Value != "Alpha";

                if (clear)
                    e.Handled = false;
            };

            grid.RebuildView ();
            Assert.All (handled, h => Assert.True (h));
            Assert.Equal (new object?[] { "Bravo", "Charlie" }, grid.Rows.Select (r => r.Cells["Name"].Value));

            // Handled cleared: the descriptors decide, and with none every row stays.
            clear = true;
            grid.RebuildView ();
            Assert.Equal (3, grid.Rows.Count);
        }
    }
}
