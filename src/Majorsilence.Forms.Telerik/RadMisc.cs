using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat status strip. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadStatusStrip : Control, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the strip (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the items hosted in the status strip.</summary>
        public List<object> Items { get; } = new ();
        /// <summary>Sets whether the last item springs to fill remaining space. Stub.</summary>
        public void SetSpring (bool spring) { }
    }

    /// <summary>Telerik-compat command bar. Backed by <see cref="Majorsilence.Forms.Control"/>.</summary>
    public class RadCommandBar : Control, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the bar (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
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
    public class RadMenu : Menu, ISupportInitializeCompat 
    {
        /// <summary>Gets the root element of the menu (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

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
        /// <summary>Initializes a new instance.</summary>
        public RadContextMenu () { }

        /// <summary>Initializes a new instance owned by the specified container (WinForms designer overload; the container is not used).</summary>
        public RadContextMenu (System.ComponentModel.IContainer container) { }

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

        /// <summary>Shows the owning context menu relative to the specified control.</summary>
        public void Show (Control control, Point location) => owner.Show (control, location);

        /// <summary>Shows the owning context menu at the given offset from the specified control.</summary>
        public void Show (Control control, int x, int y) => owner.Show (control, new Point (x, y));

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
        private static readonly Dictionary<Control, (RadContextMenu Menu, MouseEventHandler Handler)> _menus = new ();

        /// <summary>Associates the specified <see cref="RadContextMenu"/> with a control (null clears the association).</summary>
        public static void SetRadContextMenu (Control control, RadContextMenu? menu)
        {
            if (_menus.Remove (control, out var existing))
                control.MouseUp -= existing.Handler;

            if (menu is null)
                return;

            MouseEventHandler handler = (_, e) => {
                if (e.Button == MouseButtons.Right)
                    menu.Show (control, e.Location);
            };

            _menus[control] = (menu, handler);
            control.MouseUp += handler;
        }

        /// <summary>Gets the <see cref="RadContextMenu"/> associated with the control, or null.</summary>
        public static RadContextMenu? GetRadContextMenu (Control control) => _menus.TryGetValue (control, out var entry) ? entry.Menu : null;
    }

    /// <summary>Keyed collection of property-grid items (indexable by Name).</summary>
    public class PropertyGridItemCollection : List<PropertyGridItem>
    {
        /// <summary>Gets the item with the specified property name, or null.</summary>
        public PropertyGridItem? this[string name]
            => Find (i => string.Equals (i.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Keyed collection of property-grid group items (indexable by group name).</summary>
    public class PropertyGridGroupItemCollection : List<PropertyGridGroupItem>
    {
        /// <summary>Gets the group with the specified name; a detached group when absent (so chained calls are safe).</summary>
        public PropertyGridGroupItem this[string name]
            => Find (g => string.Equals (g.Name, name, StringComparison.OrdinalIgnoreCase)) ?? new PropertyGridGroupItem { Name = name };
    }

    /// <summary>Telerik-compat property grid, over the core <see cref="PropertyGrid"/>.</summary>
    /// <remarks>
    /// Live as of W6 mechanisms (#176). <see cref="Items"/> and <see cref="Groups"/> are built from the
    /// rows the core grid shows for <see cref="PropertyGrid.SelectedObject"/>, and rebuilt whenever it
    /// rebuilds them; the seven events fire at the core grid's own moments -- <see cref="ItemFormatting"/>
    /// as each row is painted, <see cref="EditorRequired"/> and <see cref="EditorInitialized"/> around
    /// the editor an edit opens, <see cref="Edited"/> as it closes, <see cref="ItemValueChanged"/> when a
    /// value is written, and <see cref="ItemMouseClick"/> and <see cref="ContextMenuOpening"/> on a click
    /// over a row. A <see cref="RadPropertyStore"/> assigned as the selected object is shown as its items.
    /// </remarks>
    public class RadPropertyGrid : PropertyGrid, ISupportInitializeCompat
    {
        // PropertySort, ToolbarVisible and the painting of the rows are inherited from the core grid.

        /// <summary>Gets the element tree root (stub; hit testing returns null).</summary>
        public RadElement ElementTree { get; } = new RadElement ();

        /// <summary>Gets or sets the selected item.</summary>
        /// <remarks>Settable, as Telerik's is, and the same selection as the core grid's: the item the
        /// user clicked reads back here, and assigning an item of <see cref="Items"/> selects its row.</remarks>
        public new Majorsilence.Forms.GridItem? SelectedGridItem {
            get => base.SelectedGridItem is { } entry && wrappers.TryGetValue (entry, out var item) ? item : base.SelectedGridItem;
            set {
                if (value is PropertyGridItem { Entry: { } entry })
                    base.SelectedGridItem = entry;
                else if (value is not null)
                    base.SelectedGridItem = value;
            }
        }

        /// <summary>Gets the property items, indexable by property name.</summary>
        public PropertyGridItemCollection Items { get; } = new ();

        /// <summary>Gets the property groups, indexable by group name.</summary>
        public PropertyGridGroupItemCollection Groups { get; } = new ();

        /// <summary>Gets or sets the sort order.</summary>
        /// <remarks>Stored: ordering is <see cref="PropertyGrid.PropertySort"/>'s, which the toolbar
        /// drives. Telerik's descriptor-based sort is not modelled.</remarks>
        public object? SortOrder { get; set; }

        /// <summary>Gets or sets whether sorting is enabled.</summary>
        /// <remarks>Stored; see <see cref="SortOrder"/>.</remarks>
        public bool EnableSorting { get; set; } = true;

        /// <summary>Gets the sort descriptors. Stub list.</summary>
        public List<object> SortDescriptors { get; } = new ();

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets whether the grid refuses every edit.</summary>
        /// <remarks>Real as of W6 mechanisms: a click in a value cell opens no editor, and the rows
        /// still show their values.</remarks>
        public bool ReadOnly {
            get => read_only;
            set {
                if (read_only == value)
                    return;

                read_only = value;

                if (value)
                    EndEdit (commit: false);

                Invalidate ();
            }
        }

        private bool read_only;

        /// <summary>Opens the editor on the selected item, when it can be edited.</summary>
        public void BeginEdit ()
        {
            if (base.SelectedGridItem is not { } entry)
                return;

            for (var i = 0; i < VisibleRows.Count; i++) {
                if (ReferenceEquals (VisibleRows[i], entry)) {
                    BeginEdit (i);
                    return;
                }
            }
        }

        /// <summary>Raised as each property row is painted, so a handler can restyle or relabel it.</summary>
        /// <remarks>The handler sees the row through <c>e.VisualElement</c>: its
        /// <c>TextElement.Text</c> is the label drawn and its <c>ValueElement.Text</c> the value, and
        /// its <c>ForeColor</c>/<c>BackColor</c> colour the row. Raised on every paint, as Telerik
        /// raises it whenever an element is updated.</remarks>
        public event EventHandler<PropertyGridItemFormattingEventArgs>? ItemFormatting;

        /// <summary>Raised after an item's editor closes.</summary>
        public event EventHandler<PropertyGridItemEditedEventArgs>? Edited;

        /// <summary>Raised once an item's editor has been created and seeded with its value.</summary>
        public event EventHandler<PropertyGridItemEditorInitializedEventArgs>? EditorInitialized;

        /// <summary>Raised before an item's editor is created, so a handler can supply its own.</summary>
        /// <remarks>A <see cref="Control"/> assigned to <c>e.Editor</c>, or a control type assigned to
        /// <c>e.EditorType</c>, is the editor the grid opens; it commits its <c>Text</c> (a
        /// <see cref="NumericUpDown"/> its value, a <see cref="CheckBox"/> its check) through the
        /// property's converter. Telerik's own editor element types are not modelled, so an editor of
        /// any other kind leaves the grid's built-in one.</remarks>
        public event EventHandler<PropertyGridEditorRequiredEventArgs>? EditorRequired;

        /// <summary>Raised after an item's value is written, by an edit or through <see cref="PropertyGridItem.Value"/>.</summary>
        public event EventHandler<PropertyGridItemValueChangedEventArgs>? ItemValueChanged;

        /// <summary>Raised when a property row is clicked; the sender is the grid, and the clicked item is <see cref="SelectedGridItem"/>.</summary>
        public event EventHandler? ItemMouseClick;

        /// <summary>Raised on a right-click over a row, before any context menu opens.</summary>
        public event EventHandler? ContextMenuOpening;

        // ── the item model ──────────────────────────────────────────────────────────────────────────

        private readonly Dictionary<Majorsilence.Forms.GridItem, PropertyGridItem> wrappers = new ();

        internal override void OnEntriesRebuilt ()
        {
            wrappers.Clear ();
            Items.Clear ();
            Groups.Clear ();

            foreach (var root in Roots) {
                if (root.GridItemType == GridItemType.Category) {
                    var group = new PropertyGridGroupItem (this, root);
                    wrappers[root] = group;
                    Groups.Add (group);

                    foreach (var child in root.GridItems) {
                        var item = new PropertyGridItem (this, child);
                        wrappers[child] = item;
                        group.GridItems.Add (item);
                        Items.Add (item);
                    }
                } else {
                    var item = new PropertyGridItem (this, root);
                    wrappers[root] = item;
                    Items.Add (item);
                }
            }
        }

        private PropertyGridItem? WrapperOf (Majorsilence.Forms.GridItem entry)
            => wrappers.TryGetValue (entry, out var item) ? item : null;

        internal override bool IsRowVisible (Majorsilence.Forms.GridItem item) => WrapperOf (item)?.Visible ?? true;

        internal override bool AllowsEdit (Majorsilence.Forms.GridItem item)
            => !read_only && WrapperOf (item) is not { ReadOnly: true };

        internal void SelectEntry (Majorsilence.Forms.GridItem entry) => base.SelectedGridItem = entry;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "The property grid writes through TypeDescriptor at runtime, as the core grid does.")]
        internal void WriteValue (PropertyGridItem item, Majorsilence.Forms.GridItem entry, object? value)
        {
            if (SelectedObject is not { } target || entry.PropertyDescriptor is not { } property || property.IsReadOnly)
                return;

            try {
                property.SetValue (target, value);
                entry.Value = property.GetValue (target);
            } catch (Exception ex) when (ex is ArgumentException or InvalidCastException or NotSupportedException
                                            or System.Reflection.TargetInvocationException) {
                return;
            }

            Invalidate ();
            ItemValueChanged?.Invoke (this, new PropertyGridItemValueChangedEventArgs { Item = item });
        }

        /// <inheritdoc/>
        protected override void OnPropertyValueChanged (PropertyValueChangedEventArgs e)
        {
            base.OnPropertyValueChanged (e);

            if (e.ChangedItem is { } entry && WrapperOf (entry) is { } item)
                ItemValueChanged?.Invoke (this, new PropertyGridItemValueChangedEventArgs { Item = item });
        }

        // ── formatting ──────────────────────────────────────────────────────────────────────────────

        internal override RowFormat FormatRow (Majorsilence.Forms.GridItem item, string label, string value)
        {
            var wrapper = WrapperOf (item);
            var shown = wrapper?.Label is { Length: > 0 } relabelled ? relabelled : label;

            if (ItemFormatting is null || wrapper is null)
                return new RowFormat (shown, value, Color.Empty, Color.Empty);

            var element = new PropertyGridItemElement { Data = wrapper };
            element.TextElement.Text = shown;
            element.ValueElement.Text = value;

            ItemFormatting (this, new PropertyGridItemFormattingEventArgs { Item = wrapper, VisualElement = element });

            // Either the element's own colours or its label's; the label's is the one Telerik samples use.
            var fore = !element.TextElement.ForeColor.IsEmpty ? element.TextElement.ForeColor : element.ForeColor;
            var back = !element.BackColor.IsEmpty ? element.BackColor : element.TextElement.BackColor;

            return new RowFormat (element.TextElement.Text, element.ValueElement.Text, fore, back);
        }

        // ── editing ─────────────────────────────────────────────────────────────────────────────────

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "An editor type named by an EditorRequired handler is created by reflection, as Telerik creates it.")]
        internal override Control? CreateEditor (Majorsilence.Forms.GridItem item)
        {
            if (EditorRequired is null || WrapperOf (item) is not { } wrapper)
                return null;

            var args = new PropertyGridEditorRequiredEventArgs { Item = wrapper, EditorType = null };
            EditorRequired (this, args);

            if (args.Editor is Control supplied)
                return Seed (supplied, item);

            if (args.EditorType is { } type && typeof (Control).IsAssignableFrom (type) && type.GetConstructor (Type.EmptyTypes) is not null)
                return Seed ((Control) Activator.CreateInstance (type)!, item);

            return null;
        }

        // The item's current value in the editor, in the form that editor holds it.
        private static Control Seed (Control editor, Majorsilence.Forms.GridItem item)
        {
            switch (editor) {
            case NumericUpDown number when item.Value is IConvertible convertible:
                try {
                    number.Value = Math.Min (number.Maximum, Math.Max (number.Minimum, convertible.ToDecimal (System.Globalization.CultureInfo.InvariantCulture)));
                } catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) {
                }

                break;
            case CheckBox check when item.Value is bool on:
                check.Checked = on;
                break;
            default:
                editor.Text = PropertyGrid.ValueTextOf (item);
                break;
            }

            return editor;
        }

        internal override void OnEditorCreated (Majorsilence.Forms.GridItem item, Control editor)
        {
            if (WrapperOf (item) is { } wrapper)
                EditorInitialized?.Invoke (this, new PropertyGridItemEditorInitializedEventArgs { Item = wrapper, Editor = editor });
        }

        internal override void OnEditEnded (Majorsilence.Forms.GridItem item)
        {
            if (WrapperOf (item) is { } wrapper)
                Edited?.Invoke (this, new PropertyGridItemEditedEventArgs { Item = wrapper });
        }

        // ── clicks ──────────────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            // The core grid moves the selection and may open an editor; the Telerik events follow it,
            // so a handler reading SelectedGridItem finds the row that was clicked.
            base.OnMouseDown (e);

            if (RowAt (e.Location) is not { } entry || entry.GridItemType == GridItemType.Category)
                return;

            if (e.Button == MouseButtons.Right) {
                if (!ReferenceEquals (base.SelectedGridItem, entry))
                    base.SelectedGridItem = entry;

                ContextMenuOpening?.Invoke (this, EventArgs.Empty);
                return;
            }

            ItemMouseClick?.Invoke (this, EventArgs.Empty);
        }

        // The row under a logical point; the rows are laid out in device pixels (RC-8).
        private Majorsilence.Forms.GridItem? RowAt (Point logical)
        {
            var device = LogicalToDeviceUnits (logical);

            for (var i = 0; i < VisibleRows.Count; i++)
                if (RowBounds (i).Contains (device))
                    return VisibleRows[i];

            return null;
        }
    }

    /// <summary>Telerik-compat layout control. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadLayoutControl : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets the layout items.</summary>
        public List<LayoutControlItem> Items { get; } = new ();
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat layout item. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class LayoutControlItem : Panel, ISupportInitializeCompat { }

    /// <summary>Telerik-compat layout group. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class LayoutControlGroup : Panel, ISupportInitializeCompat { }
}

namespace Majorsilence.Forms.Telerik.Themes
{
    /// <summary>Telerik-compat visual theme component. No-op stub -- the compat controls have no theme engine.</summary>
    public class Office2007BlackTheme : System.ComponentModel.Component { }
}
