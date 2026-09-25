using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// W6 mechanisms, twelfth chunk: the PropertyGrid grew the pieces two dozen of its properties were
// waiting on -- a real GridItem tree, the help and commands panes, the toolbar and property tabs,
// BrowsableAttributes filtering, and in-place value editing (which is what PropertyValueChanged
// needed to exist at all).
[Collection ("Headless")]
public class W6PropertyGridTests
{
    private sealed class Settings
    {
        [Category ("Appearance"), Description ("The caption shown in the title bar.")]
        public string Title { get; set; } = "Untitled";

        [Category ("Appearance")]
        public bool Visible { get; set; } = true;

        [Category ("Behaviour")]
        public int Retries { get; set; } = 3;

        [Category ("Behaviour"), ReadOnly (true)]
        public string Identifier { get; set; } = "fixed";

        [Browsable (false)]
        public string Hidden { get; set; } = "never shown";
    }

    private static PropertyGrid Grid (out Form form, object? selected = null, bool show = true)
    {
        HeadlessRenderer.Use ();
        form = new Form { Size = new Size (460, 420) };
        var grid = new PropertyGrid { Bounds = new Rectangle (0, 0, 380, 360) };
        form.Controls.Add (grid);

        if (show)
            form.Show ();

        grid.SelectedObject = selected ?? new Settings ();
        return grid;
    }

    private static void ClickRow (PropertyGrid grid, int index, bool value = false)
    {
        // RowBounds is device, the mouse logical (RC-8).
        var row = grid.DeviceToLogicalUnits (grid.RowBounds (index));
        var x = value ? row.Left + (row.Width * 3 / 4) : row.Left + 10;
        grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, x, row.Top + (row.Height / 2), 0));
    }

    // ── the item tree ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_grid_builds_a_GridItem_tree_of_categories_and_properties ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Equal (2, grid.Roots.Count);
            Assert.Equal (new[] { "Appearance", "Behaviour" }, grid.Roots.Select (r => r.Label).ToArray ());
            Assert.All (grid.Roots, r => Assert.Equal (GridItemType.Category, r.GridItemType));

            var appearance = grid.Roots[0];
            Assert.Equal (new[] { "Title", "Visible" }, appearance.GridItems.Select (i => i.Label).ToArray ());
            Assert.All (appearance.GridItems, i => Assert.Same (appearance, i.Parent));
            Assert.All (appearance.GridItems, i => Assert.NotNull (i.PropertyDescriptor));

            // The item carries the live value, and Browsable(false) is left out.
            Assert.Equal ("Untitled", appearance.GridItems[0].Value);
            Assert.DoesNotContain (grid.VisibleRows, i => i.Label == "Hidden");
        }
    }

    [Fact]
    public void A_collapsed_category_hides_its_children ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Equal (6, grid.VisibleRows.Count);   // two categories plus four properties

            grid.Roots[0].Expanded = false;
            Assert.Equal (4, grid.VisibleRows.Count);
            Assert.DoesNotContain (grid.VisibleRows, i => i.Label == "Title");

            grid.CollapseAllGridItems ();
            Assert.Equal (2, grid.VisibleRows.Count);

            grid.ExpandAllGridItems ();
            Assert.Equal (6, grid.VisibleRows.Count);

            // Clicking the expander box toggles the category rather than selecting it.
            var box = grid.DeviceToLogicalUnits (grid.ExpanderBounds (0));
            grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, box.Left + (box.Width / 2), box.Top + (box.Height / 2), 0));

            Assert.False (grid.Roots[0].Expanded);
            Assert.Equal (4, grid.VisibleRows.Count);
        }
    }

    [Fact]
    public void SelectedGridItem_follows_the_click_and_can_be_set ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Null (grid.SelectedGridItem);

            ClickRow (grid, 1);
            Assert.Equal ("Title", grid.SelectedGridItem!.Label);

            // Selecting a child of a collapsed category opens its parents so it can be seen.
            var retries = grid.Roots[1].GridItems.First (i => i.Label == "Retries");
            grid.Roots[1].Expanded = false;
            retries.Select ();

            Assert.Same (retries, grid.SelectedGridItem);
            Assert.True (grid.Roots[1].Expanded);
            Assert.Contains (retries, grid.VisibleRows);
        }
    }

    // ── the help pane ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_help_pane_shows_the_selected_property_description ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.False (grid.HelpBounds.IsEmpty);
            Assert.Equal ((string.Empty, string.Empty), grid.HelpText);

            ClickRow (grid, 1);
            Assert.Equal (("Title", "The caption shown in the title bar."), grid.HelpText);

            // The pane is painted in HelpBackColor and takes room from the view.
            grid.HelpBackColor = Color.FromArgb (12, 210, 40);
            using (var bitmap = PaintSurface.Render (grid))
                Assert.True (CountIn (bitmap, grid.HelpBounds, new SKColor (12, 210, 40)) > 0, "the help pane was not painted");

            var with_help = grid.ViewBounds.Height;
            grid.HelpVisible = false;

            Assert.True (grid.HelpBounds.IsEmpty);
            Assert.True (grid.ViewBounds.Height > with_help, "the view should reclaim the help pane's room");
        }
    }

    // ── the toolbar and tabs ────────────────────────────────────────────────────────────────────────

    private sealed class NameOnlyTab : PropertyTab
    {
        public override string TabName => "Name only";

        public override PropertyDescriptorCollection GetProperties (object component)
            => new PropertyDescriptorCollection (TypeDescriptor.GetProperties (component)
                .Cast<PropertyDescriptor> ().Where (p => p.Name == "Title").ToArray ());
    }

    [Fact]
    public void The_toolbar_is_real_and_its_sort_buttons_drive_PropertySort ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.True (grid.ToolbarVisible);
            Assert.True (grid.Toolbar.Visible);
            Assert.True (grid.ViewBounds.Top >= grid.Toolbar.ScaledHeight);

            var categorised = (ToolStripButton) grid.Toolbar.Items[0];
            var alphabetical = (ToolStripButton) grid.Toolbar.Items[1];
            Assert.True (categorised.Checked);
            Assert.False (alphabetical.Checked);

            alphabetical.PerformClick ();
            Assert.Equal (PropertySort.Alphabetical, grid.PropertySort);
            Assert.True (alphabetical.Checked);

            // Alphabetical has no category rows, so every visible row is a property.
            Assert.All (grid.VisibleRows, i => Assert.Equal (GridItemType.Property, i.GridItemType));

            // LargeButtons sizes the strip.
            var small = grid.Toolbar.Height;
            grid.LargeButtons = true;
            Assert.True (grid.Toolbar.Height > small);
            Assert.Equal (new Size (24, 24), grid.Toolbar.ImageScalingSize);

            grid.ToolbarVisible = false;
            Assert.False (grid.Toolbar.Visible);
        }
    }

    [Fact]
    public void A_property_tab_supplies_the_properties_and_switching_raises_PropertyTabChanged ()
    {
        var grid = Grid (out var form);

        using (form) {
            var changes = new List<PropertyTabChangedEventArgs> ();
            grid.PropertyTabChanged += (_, e) => changes.Add (e);

            grid.PropertyTabs.AddTabType (typeof (NameOnlyTab));
            grid.RefreshTabs (PropertyTabScope.Global);

            // The tab has a button on the toolbar; clicking it selects the tab.
            var button = grid.Toolbar.Items.Cast<ToolStripItem> ().OfType<ToolStripButton> ().First (b => b.Text == "Name only");
            button.PerformClick ();

            var change = Assert.Single (changes);
            Assert.Null (change.OldTab);
            Assert.IsType<NameOnlyTab> (change.NewTab);
            Assert.Same (change.NewTab, grid.SelectedTab);
            Assert.True (button.Checked);

            // And the tab decides what the grid shows.
            Assert.Equal (new[] { "Appearance", "Title" }, grid.VisibleRows.Select (i => i.Label).ToArray ());
        }
    }

    // ── the commands pane ───────────────────────────────────────────────────────────────────────────

    // A component sited so that it offers an IMenuCommandService, which is where upstream's property
    // grid finds the verbs its commands pane shows.
    private sealed class HostedComponent : Component
    {
        internal int Invoked;

        public HostedComponent ()
        {
            var verbs = new DesignerVerbCollection {
                new DesignerVerb ("Reset it", (_, _) => Invoked++),
                new DesignerVerb ("Disabled one", (_, _) => { }) { Enabled = false },
            };

            Site = new StubSite (this, new StubCommands (verbs));
        }

        public string Caption { get; set; } = "hello";

        private sealed class StubSite (IComponent owner, IMenuCommandService commands) : ISite
        {
            public IComponent Component => owner;
            public IContainer? Container => null;
            public bool DesignMode => true;
            public string? Name { get; set; }

            public object? GetService (Type serviceType) => serviceType == typeof (IMenuCommandService) ? commands : null;
        }

        private sealed class StubCommands (DesignerVerbCollection verbs) : IMenuCommandService
        {
            public DesignerVerbCollection Verbs => verbs;

            public void AddCommand (MenuCommand command) { }
            public void AddVerb (DesignerVerb verb) => verbs.Add (verb);
            public MenuCommand? FindCommand (CommandID commandID) => null;
            public bool GlobalInvoke (CommandID commandID) => false;
            public void RemoveCommand (MenuCommand command) { }
            public void RemoveVerb (DesignerVerb verb) => verbs.Remove (verb);
            public void ShowContextMenu (CommandID menuID, int x, int y) { }
        }
    }

    [Fact]
    public void The_commands_pane_shows_the_designer_verbs_and_invokes_one ()
    {
        using var plain = new PropertyGrid { Size = new Size (300, 300), SelectedObject = new Settings () };

        // No designer host, so no commands -- which is what upstream shows too.
        Assert.False (plain.CanShowCommands);
        Assert.False (plain.CommandsVisible);
        Assert.True (plain.CommandsBounds.IsEmpty);

        var component = new HostedComponent ();
        var grid = Grid (out var form, component);

        using (form) {
            Assert.True (grid.CanShowCommands);
            Assert.True (grid.CommandsVisible);
            Assert.False (grid.CommandsBounds.IsEmpty);

            grid.CommandsBackColor = Color.FromArgb (14, 190, 60);
            using (var bitmap = PaintSurface.Render (grid))
                Assert.True (CountIn (bitmap, grid.CommandsBounds, new SKColor (14, 190, 60)) > 0, "the commands pane was not painted");

            // Clicking the first verb's row invokes it; the disabled one does nothing.
            var first = grid.DeviceToLogicalUnits (grid.CommandBounds (0));
            grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, first.Left + 5, first.Top + (first.Height / 2), 0));
            Assert.Equal (1, component.Invoked);

            var second = grid.DeviceToLogicalUnits (grid.CommandBounds (1));
            grid.RaiseMouseDown (new MouseEventArgs (MouseButtons.Left, 1, second.Left + 5, second.Top + (second.Height / 2), 0));
            Assert.Equal (1, component.Invoked);

            // Turning the pane off hides it and gives the room back.
            grid.CommandsVisibleIfAvailable = false;
            Assert.False (grid.CommandsVisible);
            Assert.True (grid.CommandsBounds.IsEmpty);
        }
    }

    // ── BrowsableAttributes ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BrowsableAttributes_filters_the_listed_properties ()
    {
        var grid = Grid (out var form);

        using (form) {
            Assert.Contains (grid.VisibleRows, i => i.Label == "Title");
            Assert.Contains (grid.VisibleRows, i => i.Label == "Identifier");

            // Only the read-only properties.
            grid.BrowsableAttributes = new AttributeCollection (new ReadOnlyAttribute (true));

            Assert.Equal (new[] { "Identifier" }, grid.VisibleRows.Where (i => i.GridItemType == GridItemType.Property).Select (i => i.Label).ToArray ());

            // And back: a default-valued attribute matches a property that does not declare it.
            grid.BrowsableAttributes = new AttributeCollection (new ReadOnlyAttribute (false));

            Assert.Contains (grid.VisibleRows, i => i.Label == "Title");
            Assert.DoesNotContain (grid.VisibleRows, i => i.Label == "Identifier");
        }
    }

    // ── editing ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Clicking_a_value_edits_it_and_a_commit_raises_PropertyValueChanged ()
    {
        var target = new Settings ();
        var grid = Grid (out var form, target);

        using (form) {
            var changes = new List<PropertyValueChangedEventArgs> ();
            grid.PropertyValueChanged += (_, e) => changes.Add (e);

            // The name column selects; the value column opens an editor.
            ClickRow (grid, 1);
            Assert.Null (grid.EditingControl);

            ClickRow (grid, 1, value: true);
            var editor = Assert.IsType<TextBox> (grid.EditingControl);
            Assert.Equal ("Untitled", editor.Text);

            editor.Text = "Renamed";
            grid.EndEdit (commit: true);

            Assert.Equal ("Renamed", target.Title);
            Assert.Null (grid.EditingControl);

            var change = Assert.Single (changes);
            Assert.Equal ("Untitled", change.OldValue);
            Assert.Equal ("Title", change.ChangedItem.Label);
            Assert.Equal ("Renamed", change.ChangedItem.Value);
        }
    }

    [Fact]
    public void A_boolean_edits_in_a_drop_down_and_a_read_only_property_does_not_edit ()
    {
        var target = new Settings ();
        var grid = Grid (out var form, target);

        using (form) {
            var visible = grid.VisibleRows.Select ((item, index) => (item, index)).First (r => r.item.Label == "Visible").index;
            var identifier = grid.VisibleRows.Select ((item, index) => (item, index)).First (r => r.item.Label == "Identifier").index;

            Assert.True (grid.IsEditable (visible));
            Assert.False (grid.IsEditable (identifier));

            ClickRow (grid, visible, value: true);
            var combo = Assert.IsType<ComboBox> (grid.EditingControl);
            Assert.Equal (new object[] { "False", "True" }, combo.Items.Cast<object> ().ToArray ());
            Assert.Equal ("True", combo.SelectedItem);

            // Choosing commits, as a drop-down does.
            combo.SelectedItem = "False";
            Assert.False (target.Visible);

            grid.EndEdit (commit: false);

            // A read-only property refuses to open an editor at all.
            ClickRow (grid, identifier, value: true);
            Assert.Null (grid.EditingControl);
        }
    }

    [Fact]
    public void A_value_the_converter_refuses_leaves_the_property_alone ()
    {
        var target = new Settings ();
        var grid = Grid (out var form, target);

        using (form) {
            var changes = 0;
            grid.PropertyValueChanged += (_, _) => changes++;

            var retries = grid.VisibleRows.Select ((item, index) => (item, index)).First (r => r.item.Label == "Retries").index;

            ClickRow (grid, retries, value: true);
            var editor = Assert.IsType<TextBox> (grid.EditingControl);

            editor.Text = "not a number";
            grid.EndEdit (commit: true);

            Assert.Equal (3, target.Retries);
            Assert.Equal (0, changes);
        }
    }

    // ── colours ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_view_border_category_splitter_and_disabled_colours_are_painted ()
    {
        var grid = Grid (out var form);

        using (form) {
            grid.ViewBorderColor = Color.FromArgb (200, 10, 10);
            grid.CategorySplitterColor = Color.FromArgb (10, 10, 200);
            grid.DisabledItemForeColor = Color.FromArgb (10, 190, 190);

            using var bitmap = PaintSurface.Render (grid);

            Assert.True (CountIn (bitmap, grid.ViewBounds, new SKColor (200, 10, 10)) > 0, "no view border");
            Assert.True (CountIn (bitmap, grid.ViewBounds, new SKColor (10, 10, 200)) > 0, "no category splitter");

            // The read-only property's row is drawn in the disabled colour.
            var identifier = grid.VisibleRows.Select ((item, index) => (item, index)).First (r => r.item.Label == "Identifier").index;
            Assert.True (CountIn (bitmap, grid.RowBounds (identifier), new SKColor (10, 190, 190)) > 0, "read-only row not in DisabledItemForeColor");
        }
    }

    private static int CountIn (SKBitmap bitmap, Rectangle area, SKColor colour)
    {
        var count = 0;

        for (var y = Math.Max (0, area.Top); y < Math.Min (bitmap.Height, area.Bottom); y++)
            for (var x = Math.Max (0, area.Left); x < Math.Min (bitmap.Width, area.Right); x++) {
                var p = bitmap.GetPixel (x, y);

                if (p.Red == colour.Red && p.Green == colour.Green && p.Blue == colour.Blue)
                    count++;
            }

        return count;
    }
}
