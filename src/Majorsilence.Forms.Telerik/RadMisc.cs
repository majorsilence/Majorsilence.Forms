using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat status strip. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadStatusStrip : Control
    {
        /// <summary>Gets the items hosted in the status strip.</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Sets whether the last item springs to fill remaining space. Stub.</summary>
        public void SetSpring (bool spring) { }
    }

    /// <summary>Telerik-compat command bar. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadCommandBar : Control
    {
        /// <summary>Gets the command-bar rows.</summary>
        public List<CommandBarRowElement> Rows { get; } = new ();
    }

    /// <summary>Telerik-compat command-bar row.</summary>
    public class CommandBarRowElement
    {
        /// <summary>Gets the strips in this row.</summary>
        public List<CommandBarStripElement> Strips { get; } = new ();
    }

    /// <summary>Telerik-compat command-bar strip.</summary>
    public class CommandBarStripElement
    {
        /// <summary>Gets the items in this strip.</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the text.</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Telerik-compat menu. Backed by <see cref="Majorsilence.Forms.Menu"/>.</summary>
    public class RadMenu : Menu { }

    /// <summary>Telerik-compat menu item. Backed by <see cref="Majorsilence.Forms.MenuItem"/>.</summary>
    public class RadMenuItem : MenuItem
    {
        /// <summary>Initializes a new instance.</summary>
        public RadMenuItem () { }
        /// <summary>Initializes a new instance with the specified text.</summary>
        public RadMenuItem (string text) : base (text) { }
        /// <summary>Initializes a new instance with the specified text and tag (Telerik (text, data) ctor).</summary>
        public RadMenuItem (string text, object? tag) : base (text) { Tag = tag; }

        /// <summary>Gets or sets whether clicking toggles the checked state. Mirrors Telerik.</summary>
        public bool CheckOnClick { get; set; }

        /// <summary>Telerik-style alias of <see cref="MenuItem.Checked"/>.</summary>
        public bool IsChecked {
            get => Checked;
            set => Checked = value;
        }
    }

    /// <summary>Telerik-compat menu separator.</summary>
    public class RadMenuSeparatorItem : MenuSeparatorItem { }

    /// <summary>Base for Telerik-compat command bar items (buttons, separators, etc. hosted in a <see cref="RadCommandBar"/> strip).</summary>
    public class RadCommandBarBaseItem : RadItem
    {
        /// <summary>Gets or sets whether the item's image is drawn. Stored for Telerik compat.</summary>
        public bool DrawImage { get; set; } = true;

        /// <summary>Gets or sets the display name shown in the command bar customization UI. Stub.</summary>
        public string DisplayName { get; set; } = string.Empty;
        /// <summary>Gets or sets whether the item appears in the "more items" overflow menu. Stub.</summary>
        public bool VisibleInOverflowMenu { get; set; } = true;
    }

    /// <summary>Telerik-compat command-bar button.</summary>
    public class CommandBarButton : RadCommandBarBaseItem
    {
        /// <summary>Initializes a new instance of the CommandBarButton class.</summary>
        public CommandBarButton () { }
        /// <summary>Initializes a new instance of the CommandBarButton class with the specified text.</summary>
        public CommandBarButton (string text) => Text = text;

        /// <summary>Gets or sets the button image.</summary>
        public Majorsilence.Forms.Drawing.Image? Image { get; set; }
        /// <summary>Gets or sets whether the button's text is drawn. Stub.</summary>
        public bool DrawText { get; set; } = true;
    }

    /// <summary>
    /// Telerik-compat context menu. Backs <see cref="ContextMenuOpeningEventArgs"/>'s
    /// <c>ContextMenu</c> property and is settable on any control via
    /// <see cref="RadContextMenuManager"/>. <see cref="Show(Control, Point)"/> builds and shows a real
    /// <see cref="Majorsilence.Forms.ContextMenu"/> from <see cref="Items"/>, the same way
    /// <c>RadGridView.OnClick</c> builds its context menu.
    /// </summary>
    public class RadContextMenu
    {
        /// <summary>Gets the menu items. Populate with <see cref="RadMenuItem"/>s (or other <see cref="Majorsilence.Forms.MenuItem"/>s).</summary>
        public List<object> Items { get; } = new ();

        /// <summary>Raised before the menu is shown, so handlers can populate <see cref="Items"/> lazily.</summary>
        public event EventHandler? DropDownOpening;

        /// <summary>Raises <see cref="DropDownOpening"/>, builds a menu from <see cref="Items"/>, and shows it relative to the specified control.</summary>
        public void Show (Control control, Point location)
        {
            DropDownOpening?.Invoke (this, EventArgs.Empty);

            if (Items.Count == 0)
                return;

            var menu = new ContextMenu ();
            foreach (var item in Items)
                if (item is MenuItem menuItem)
                    menu.Items.Add (menuItem);

            if (menu.Items.Count > 0)
                menu.Show (control, location);
        }

        /// <summary>Raises <see cref="DropDownOpening"/>, builds a menu from <see cref="Items"/>, and shows it at the specified screen point.</summary>
        public void Show (Point location) => Show (location.X, location.Y);

        private RadDropDownMenu? drop_down;

        /// <summary>Gets the drop-down facade of this menu (Telerik's RadContextMenu.DropDown surface: Show/Hide/Visible).</summary>
        public RadDropDownMenu DropDown => drop_down ??= new RadDropDownMenu (this);

        /// <summary>Raises <see cref="DropDownOpening"/>, builds a menu from <see cref="Items"/>, and shows it at the specified screen coordinates.</summary>
        public void Show (int x, int y)
        {
            DropDownOpening?.Invoke (this, EventArgs.Empty);

            if (Items.Count == 0)
                return;

            var menu = new ContextMenu ();
            foreach (var item in Items)
                if (item is MenuItem menuItem)
                    menu.Items.Add (menuItem);

            if (menu.Items.Count > 0)
                menu.Show (x, y);
        }
    }

    /// <summary>
    /// The drop-down facade of a <see cref="RadContextMenu"/> (Telerik's RadDropDownMenu as reached
    /// through <c>RadContextMenu.DropDown</c>). Show delegates to the owning menu; the popup manages
    /// its own dismissal, so <see cref="Visible"/> reports false and <see cref="Hide"/> is a no-op.
    /// </summary>
    public class RadDropDownMenu
    {
        private readonly RadContextMenu owner;

        internal RadDropDownMenu (RadContextMenu owner) => this.owner = owner;

        /// <summary>Gets whether the drop-down is currently shown. Always false — the popup dismisses itself.</summary>
        public bool Visible => false;

        /// <summary>Shows the owning context menu at the specified screen point.</summary>
        public void Show (Point location) => owner.Show (location);

        /// <summary>Hides the drop-down. No-op — the popup dismisses itself.</summary>
        public void Hide () { }
    }

    /// <summary>
    /// Telerik-compat static manager associating a <see cref="RadContextMenu"/> with a control, mirroring
    /// <c>RadContextMenuManager.SetRadContextMenu</c>/<c>GetRadContextMenu</c>. Hooks the control's
    /// right mouse button up to show the associated menu.
    /// </summary>
    public static class RadContextMenuManager
    {
        // Each entry keeps the menu alongside the exact hooked delegate instance, so a later Set(control, null)
        // (or replacing the menu) can unhook the correct handler (delegate -= requires the same instance).
        private static readonly Dictionary<Control, (RadContextMenu Menu, EventHandler<MouseEventArgs> Handler)> _menus = new ();

        /// <summary>Associates the specified <see cref="RadContextMenu"/> with a control (null clears the association).</summary>
        public static void SetRadContextMenu (Control control, RadContextMenu? menu)
        {
            if (_menus.Remove (control, out var existing))
                control.MouseUp -= existing.Handler;

            if (menu is null)
                return;

            EventHandler<MouseEventArgs> handler = (_, e) => {
                if (e.Button == MouseButtons.Right)
                    menu.Show (control, e.Location);
            };

            _menus[control] = (menu, handler);
            control.MouseUp += handler;
        }

        /// <summary>Gets the <see cref="RadContextMenu"/> associated with the control, or null.</summary>
        public static RadContextMenu? GetRadContextMenu (Control control) => _menus.TryGetValue (control, out var entry) ? entry.Menu : null;
    }

    /// <summary>Telerik-compat property grid. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadPropertyGrid : PropertyGrid
    {
        // SelectedObject, PropertySort, ToolbarVisible, SelectedGridItem and the rendering of the
        // inspected object's properties are inherited from Majorsilence.Forms.PropertyGrid.

        /// <summary>Gets the property items. Stub list (Telerik exposes individual property items).</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Gets the property groups. Stub list.</summary>
        public List<object> Groups { get; } = new ();
        /// <summary>Gets or sets the sort order. Stub.</summary>
        public object? SortOrder { get; set; }
        /// <summary>Gets or sets whether sorting is enabled. Stub.</summary>
        public bool EnableSorting { get; set; } = true;
        /// <summary>Gets the sort descriptors. Stub list.</summary>
        public List<object> SortDescriptors { get; } = new ();
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Begins editing the selected item. Stub.</summary>
        public void BeginEdit () { }

        /// <summary>Raised when an item is being formatted. Stub (never raised yet).</summary>
        public event EventHandler<PropertyGridItemFormattingEventArgs>? ItemFormatting { add { } remove { } }
        /// <summary>Raised when an item has been edited. Stub (never raised yet).</summary>
        public event EventHandler<PropertyGridItemEditedEventArgs>? Edited { add { } remove { } }
        /// <summary>Raised when an editor is initialized. Stub (never raised yet).</summary>
        public event EventHandler<PropertyGridItemEditorInitializedEventArgs>? EditorInitialized { add { } remove { } }
        /// <summary>Raised when an editor is required. Stub (never raised yet).</summary>
        public event EventHandler<PropertyGridEditorRequiredEventArgs>? EditorRequired { add { } remove { } }
        /// <summary>Raised when an item's value changes. Stub (never raised yet).</summary>
        public event EventHandler<PropertyGridItemValueChangedEventArgs>? ItemValueChanged { add { } remove { } }
        /// <summary>Raised on item mouse-click. Stub.</summary>
        public event EventHandler? ItemMouseClick { add { } remove { } }
        /// <summary>Raised when the context menu is opening. Stub.</summary>
        public event EventHandler? ContextMenuOpening { add { } remove { } }
    }

    /// <summary>Telerik-compat layout control. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadLayoutControl : Panel
    {
        /// <summary>Gets the layout items.</summary>
        public List<LayoutControlItem> Items { get; } = new ();
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat layout item. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class LayoutControlItem : Panel { }

    /// <summary>Telerik-compat layout group. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class LayoutControlGroup : Panel { }
}
