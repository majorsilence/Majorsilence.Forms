using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadPropertyGrid members Telerik code uses that the compat grid did not have: PropertyGridItem.IsModified
// and Level, RadPropertyGrid.EnableGrouping, enumerating a RadPropertyStore, the PropertyGridTableElement
// EditorRequired is raised from, and a PropertyGridSpinEditor's DecimalPlaces.
[Collection ("Headless")]
public class TelerikPropertyGridMembersTests
{
    private sealed class Sample
    {
        [Category ("Behaviour"), DefaultValue (3)]
        public int Retries { get; set; } = 3;

        [Category ("Money")]
        public decimal Rate { get; set; } = 1.5m;

        [Category ("Appearance"), DefaultValue ("Untitled")]
        public string Title { get; set; } = "Untitled";
    }

    private static RadPropertyGrid Grid (out Form form, object selected)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (460, 420) };
        var grid = new RadPropertyGrid { Bounds = new Rectangle (0, 0, 380, 360) };
        form.Controls.Add (grid);
        form.Show ();
        grid.SelectedObject = selected;
        return grid;
    }

    private static void ClickValue (PropertyGrid grid, string name)
    {
        var index = -1;

        for (var i = 0; i < grid.VisibleRows.Count; i++)
            if (grid.VisibleRows[i].Name == name)
                index = i;

        var row = grid.DeviceToLogicalUnits (grid.RowBounds (index));
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, row.Left + (row.Width * 3 / 4), row.Top + (row.Height / 2), 0));
    }

    [Fact]
    public void IsModified_is_whether_the_value_differs_from_its_default ()
    {
        var sample = new Sample ();
        var grid = Grid (out var form, sample);

        using (form) {
            var retries = grid.Items["Retries"]!;
            Assert.False (retries.IsModified);

            retries.Value = 5;
            Assert.True (retries.IsModified);

            sample.Retries = 3;
            Assert.False (retries.IsModified);

            // A detached item has no property to ask.
            Assert.False (new PropertyGridItem ().IsModified);
        }
    }

    [Fact]
    public void IsModified_of_a_store_item_is_against_its_default_value ()
    {
        var store = new RadPropertyStore ();
        var item = new PropertyStoreItem (typeof (string), "Colour", "Red", "Red");
        store.Add (item);

        var grid = Grid (out var form, store);

        using (form) {
            Assert.False (grid.Items["Colour"]!.IsModified);

            item.Value = "Blue";
            Assert.True (grid.Items["Colour"]!.IsModified);
        }
    }

    [Fact]
    public void A_store_enumerates_its_items_in_order ()
    {
        var store = new RadPropertyStore ();
        store.Add (new PropertyStoreItem (typeof (int), "First", 1));
        store.Add (new PropertyStoreItem (typeof (int), "Second", 2));

        Assert.Equal (2, store.Count);
        Assert.Equal (new[] { "First", "Second" }, store.Select (i => i.PropertyName));
    }

    private sealed class Boxed
    {
        [Category ("Layout")]
        public System.Drawing.Size Extent { get; set; } = new System.Drawing.Size (3, 4);
    }

    [Fact]
    public void Level_counts_the_properties_above_an_item ()
    {
        var grid = Grid (out var form, new Boxed ());

        using (form) {
            grid.PropertySort = PropertySort.Categorized;

            var extent = grid.Items["Extent"]!;
            Assert.Equal (0, extent.Level);

            // A part of an expandable property is one level down; the category above both does not count.
            var width = extent.Entry!.GridItems.Cast<Majorsilence.Forms.GridItem> ().Single (g => g.Label == "Width");
            Assert.Equal (1, new PropertyGridItem (grid, width).Level);
        }
    }

    [Fact]
    public void Level_does_not_count_the_category_group ()
    {
        var grid = Grid (out var form, new Sample ());

        using (form) {
            grid.PropertySort = PropertySort.Categorized;

            Assert.All (grid.Items, item => Assert.Equal (0, item.Level));
            Assert.Equal (0, new PropertyGridItem ().Level);
        }
    }

    [Fact]
    public void EnableGrouping_groups_the_items_by_category ()
    {
        var grid = Grid (out var form, new Sample ());

        using (form) {
            grid.PropertySort = PropertySort.Alphabetical;
            Assert.False (grid.EnableGrouping);
            Assert.Empty (grid.Groups);

            grid.EnableGrouping = true;
            Assert.Equal (PropertySort.CategorizedAlphabetical, grid.PropertySort);
            Assert.Equal (new[] { "Appearance", "Behaviour", "Money" }, grid.Groups.Select (g => g.Name).OrderBy (n => n));

            grid.EnableGrouping = false;
            Assert.Equal (PropertySort.Alphabetical, grid.PropertySort);
            Assert.Empty (grid.Groups);
        }
    }

    [Fact]
    public void EditorRequired_comes_from_the_table_element_and_a_spin_editor_keeps_its_decimal_places ()
    {
        var sample = new Sample ();
        var grid = Grid (out var form, sample);

        using (form) {
            object? sender_seen = null;
            PropertyGridSpinEditor? supplied = null;
            object? initialized = null;

            grid.EditorRequired += (sender, e) => {
                sender_seen = sender;

                if (sender is PropertyGridTableElement && e.Item!.PropertyType == typeof (decimal)) {
                    supplied = new PropertyGridSpinEditor ();
                    ((BaseSpinEditorElement) supplied.EditorElement).DecimalPlaces = 3;
                    e.Editor = supplied;
                }
            };
            grid.EditorInitialized += (_, e) => initialized = e.Editor;

            ClickValue (grid, "Rate");

            var table = Assert.IsType<PropertyGridTableElement> (sender_seen);
            Assert.Same (grid, table.PropertyGrid);
            Assert.Same (grid.PropertyGridElement.PropertyTableElement, table);

            var spin = Assert.IsType<NumericUpDown> (grid.EditingControl);
            Assert.Equal (3, spin.DecimalPlaces);
            Assert.Equal (1.5m, spin.Value);
            Assert.Same (supplied, initialized);

            spin.Value = 2.125m;
            grid.EndEdit (commit: true);
            Assert.Equal (2.125m, sample.Rate);
        }
    }
}
