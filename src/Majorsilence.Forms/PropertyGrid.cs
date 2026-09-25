using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Displays the properties of an object, as WinForms' <c>PropertyGrid</c> does.
    /// </summary>
    /// <remarks>
    /// <para>Until W6 mechanisms this painted a flat list of name/value rows and nothing else: the help
    /// pane, the commands pane, the toolbar, the property tabs and the <see cref="GridItem"/> tree were
    /// all declared and all inert, which is why two dozen of this type's properties sat in the
    /// stored-only baseline. The grid now builds a real item tree, lays the panes out around the view,
    /// and edits values in place.</para>
    /// <para>The layout, top to bottom: the toolbar (<see cref="ToolbarVisible"/>), the view, the
    /// commands pane (<see cref="CommandsVisible"/>) and the help pane (<see cref="HelpVisible"/>).</para>
    /// </remarks>
    public partial class PropertyGrid : ScrollableControl
    {
        private object? _selected_object;
        private readonly List<GridItem> _rows = [];
        private GridItem? _selected_item;
        private PropertyTab? _selected_tab;
        private const int ROW_HEIGHT = 22;
        private const int NAME_COL_RATIO_PCT = 40;
        private const int HELP_HEIGHT = 56;
        private const int COMMANDS_ROW_HEIGHT = 18;

        /// <summary>Initializes a new instance of the <see cref="PropertyGrid"/> class.</summary>
        public PropertyGrid ()
        {
            toolbar = Controls.AddImplicitControl (new ToolStrip {
                Dock = DockStyle.Top,
                Visible = toolbar_visible,
                GripStyle = ToolStripGripStyle.Hidden,
            });

            BuildToolbar ();
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (300, 400);

        /// <summary>The default style for a PropertyGrid.</summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlLowColor;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets the object whose properties are shown.</summary>
        public object? SelectedObject {
            get => _selected_object;
            set {
                if (ReferenceEquals (_selected_object, value))
                    return;

                _selected_object = value;
                RebuildEntries ();
                Invalidate ();
                SelectedObjectsChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the objects whose properties are shown; only the first is used.</summary>
        public object[]? SelectedObjects {
            get => _selected_object == null ? null : new[] { _selected_object };
            set => SelectedObject = value?.Length > 0 ? value[0] : null;
        }

        /// <summary>Rebuilds the grid from the selected object.</summary>
        public new void Refresh ()
        {
            RebuildEntries ();
            Invalidate ();
        }

        /// <summary>Expands every category.</summary>
        /// <remarks>Real as of W6 mechanisms: the categories are <see cref="GridItem"/>s with children,
        /// and <see cref="GridItem.Expanded"/> is what the view walks.</remarks>
        public void ExpandAllGridItems () => SetAllExpanded (true);

        /// <summary>Collapses every category.</summary>
        /// <remarks>See <see cref="ExpandAllGridItems"/>.</remarks>
        public void CollapseAllGridItems () => SetAllExpanded (false);

        private void SetAllExpanded (bool expanded)
        {
            foreach (var root in Roots)
                if (root.GridItems.Count > 0)
                    root.Expanded = expanded;

            RebuildRows ();
            Invalidate ();
        }

        /// <summary>Gets or sets the background of the value view.</summary>
        public Color ViewBackColor { get; set; } = SystemColors.Window;

        /// <summary>Gets or sets the foreground of the value view.</summary>
        public Color ViewForeColor { get; set; } = SystemColors.WindowText;

        /// <summary>Gets or sets the background of the help pane.</summary>
        public Color HelpBackColor { get; set; } = SystemColors.Control;

        /// <summary>Gets or sets the foreground of the help pane.</summary>
        public Color HelpForeColor { get; set; } = SystemColors.ControlText;

        /// <summary>Gets or sets the colour of the grid lines.</summary>
        public Color LineColor { get; set; } = SystemColors.InactiveBorder;

        /// <summary>Gets or sets whether the help pane is shown under the view.</summary>
        /// <remarks>Real as of W6 mechanisms: the pane shows the selected property's display name and
        /// its <c>Description</c>, in <see cref="HelpBackColor"/>/<see cref="HelpForeColor"/> behind a
        /// <see cref="HelpBorderColor"/> border.</remarks>
        public bool HelpVisible {
            get => help_visible;
            set {
                if (help_visible == value)
                    return;

                help_visible = value;
                UpdateScrollExtent ();
                Invalidate ();
            }
        }

        private bool help_visible = true;

        /// <summary>Gets or sets whether the sort/tab toolbar is shown above the view.</summary>
        /// <remarks>Real as of W6 mechanisms: a real <see cref="ToolStrip"/> carrying the categorised
        /// and alphabetical sort buttons and one button per <see cref="PropertyTabs"/> entry.</remarks>
        public bool ToolbarVisible {
            get => toolbar_visible;
            set {
                if (toolbar_visible == value)
                    return;

                toolbar_visible = value;
                toolbar.Visible = value;
                UpdateScrollExtent ();
                Invalidate ();
            }
        }

        private bool toolbar_visible = true;

        private PropertySort property_sort = PropertySort.CategorizedAlphabetical;

        /// <summary>Gets or sets how the properties are ordered and grouped.</summary>
        public PropertySort PropertySort {
            get => property_sort;
            set {
                if (property_sort == value)
                    return;

                property_sort = value;
                RebuildEntries ();
                ApplyToolbarState ();
                Invalidate ();
                OnPropertySortChanged (EventArgs.Empty);
            }
        }

        /// <summary>Raised when the selected grid item changes.</summary>
        public event EventHandler? SelectedGridItemChanged;

        /// <summary>Raised when the selected objects change.</summary>
        public event EventHandler? SelectedObjectsChanged;

        /// <summary>Raised when a property's value is changed through the grid.</summary>
        /// <remarks>Real as of W6 mechanisms: the grid edits values in place, and a committed edit
        /// raises this with the item and the value it held before.</remarks>
        public event PropertyValueChangedEventHandler? PropertyValueChanged;

        /// <summary>Raises <see cref="PropertyValueChanged"/>.</summary>
        protected virtual void OnPropertyValueChanged (PropertyValueChangedEventArgs e)
            => PropertyValueChanged?.Invoke (this, e);

        /// <summary>Gets the selected grid item.</summary>
        /// <remarks>Real as of W6 mechanisms: the grid builds a <see cref="GridItem"/> tree and this is
        /// the one the selection is on, category rows included.</remarks>
        public GridItem? SelectedGridItem {
            get => _selected_item;
            set {
                if (value is null || ReferenceEquals (_selected_item, value))
                    return;

                // Selecting a collapsed item's child opens its parents, as upstream does, so the
                // selection is always something the user can see.
                for (var parent = value.Parent; parent is not null; parent = parent.Parent)
                    parent.Expanded = true;

                RebuildRows ();
                _selected_item = value;
                EndEdit (commit: true);
                Invalidate ();
                SelectedGridItemChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets whether the commands pane is shown when there are commands to show.</summary>
        public bool CommandsVisibleIfAvailable {
            get => commands_visible_if_available;
            set {
                if (commands_visible_if_available == value)
                    return;

                commands_visible_if_available = value;
                UpdateScrollExtent ();
                Invalidate ();
            }
        }

        private bool commands_visible_if_available = true;

        // ── the item tree ───────────────────────────────────────────────────────────────────────────

        /// <summary>The top-level items: the categories, or the properties when they are not grouped.</summary>
        internal IReadOnlyList<GridItem> Roots => roots;

        private readonly List<GridItem> roots = [];

        /// <summary>The rows the view currently shows, in order (a collapsed category hides its children).</summary>
        internal IReadOnlyList<GridItem> VisibleRows => _rows;

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid uses reflection at runtime; trimming is not supported for this control.")]
        private void RebuildEntries ()
        {
            roots.Clear ();
            _rows.Clear ();
            _selected_item = null;
            EndEdit (commit: false);

            if (_selected_object == null) {
                UpdateScrollExtent ();
                return;
            }

            // The selected tab decides which properties are shown, as upstream's does; with no tab the
            // type's own descriptors are used.
            var descriptors = SelectedTab is { } tab
                ? tab.GetProperties (_selected_object).Cast<PropertyDescriptor> ()
                : TypeDescriptor.GetProperties (_selected_object).Cast<PropertyDescriptor> ();

            var props = descriptors.Where (p => p.IsBrowsable).Where (MatchesBrowsableAttributes);

            if (PropertySort is PropertySort.Alphabetical or PropertySort.CategorizedAlphabetical)
                props = props.OrderBy (p => p.Name, StringComparer.Ordinal);

            var categorised = PropertySort is PropertySort.CategorizedAlphabetical or PropertySort.Categorized;

            if (categorised)
                props = props.OrderBy (p => p.Category == "Misc" ? "zzz" : p.Category ?? "zzz", StringComparer.Ordinal)
                             .ThenBy (p => p.Name, StringComparer.Ordinal);

            GridItem? category = null;

            foreach (var prop in props) {
                var parent = (GridItem?) null;

                if (categorised) {
                    var name = prop.Category ?? "Misc";

                    if (category is null || category.Name != name) {
                        category = new PropertyGridEntry (this) { Name = name, Label = name, Expanded = true };
                        roots.Add (category);
                    }

                    parent = category;
                }

                var item = new PropertyGridEntry (this) {
                    Name = prop.Name,
                    Label = prop.DisplayName,
                    PropertyDescriptor = prop,
                    Parent = parent,
                    Value = ReadValue (prop),
                };

                if (parent is null)
                    roots.Add (item);
                else
                    parent.GridItems.Add (item);
            }

            RebuildRows ();
        }

        // BrowsableAttributes (W6 mechanisms): a property is shown only when it carries every attribute
        // in the collection, which is how upstream filters (an attribute with its default value also
        // matches a property that does not declare it at all).
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072", Justification = "PropertyGrid filters by attribute at runtime; trimming is not supported for this control.")]
        private bool MatchesBrowsableAttributes (PropertyDescriptor property)
        {
            if (BrowsableAttributes is not { Count: > 0 } wanted)
                return true;

            foreach (Attribute attribute in wanted) {
                var found = property.Attributes[attribute.GetType ()];

                if (found is null ? !attribute.IsDefaultAttribute () : !found.Equals (attribute))
                    return false;
            }

            return true;
        }

        private object? ReadValue (PropertyDescriptor property)
        {
            try {
                return _selected_object is null ? null : property.GetValue (_selected_object);
            } catch {
                return null;
            }
        }

        // The visible rows: every root, and a category's children only while it is expanded.
        private void RebuildRows ()
        {
            _rows.Clear ();

            foreach (var root in roots) {
                _rows.Add (root);

                if (root.Expanded)
                    _rows.AddRange (root.GridItems);
            }

            UpdateScrollExtent ();
        }

        internal void NotifyExpandedChanged ()
        {
            RebuildRows ();
            Invalidate ();
        }

        /// <summary>The text shown in an item's value column.</summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid converts values at runtime; trimming is not supported for this control.")]
        internal static string ValueTextOf (GridItem item)
        {
            if (item.PropertyDescriptor is null)
                return string.Empty;

            try {
                return item.Value is null ? "(null)" : item.PropertyDescriptor.Converter.ConvertToString (item.Value) ?? string.Empty;
            } catch {
                return item.Value?.ToString () ?? "(error)";
            }
        }

        // ── layout ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The device rectangle the property rows are drawn in.</summary>
        internal Rectangle ViewBounds {
            get {
                var client = ClientRectangle;
                var top = client.Top + (ToolbarVisible ? toolbar.ScaledHeight : 0);
                var bottom = client.Bottom - ScaledHelpHeight - ScaledCommandsHeight;
                return new Rectangle (client.Left, top, client.Width, Math.Max (0, bottom - top));
            }
        }

        /// <summary>The device rectangle of the commands pane; empty when it is not shown.</summary>
        internal Rectangle CommandsBounds {
            get {
                var height = ScaledCommandsHeight;

                if (height == 0)
                    return Rectangle.Empty;

                var client = ClientRectangle;
                return new Rectangle (client.Left, client.Bottom - ScaledHelpHeight - height, client.Width, height);
            }
        }

        /// <summary>The device rectangle of the help pane; empty when it is not shown.</summary>
        internal Rectangle HelpBounds {
            get {
                var height = ScaledHelpHeight;

                if (height == 0)
                    return Rectangle.Empty;

                var client = ClientRectangle;
                return new Rectangle (client.Left, client.Bottom - height, client.Width, height);
            }
        }

        private int ScaledHelpHeight => HelpVisible ? LogicalToDeviceUnits (HELP_HEIGHT) : 0;

        private int ScaledCommandsHeight
            => CommandsVisible ? LogicalToDeviceUnits ((Verbs.Count * COMMANDS_ROW_HEIGHT) + 8) : 0;

        private int ScaledRowHeight => LogicalToDeviceUnits (ROW_HEIGHT);

        private void UpdateScrollExtent ()
            => AutoScrollMinSize = new Size (0, DeviceToLogicalUnits (_rows.Count * ScaledRowHeight));

        /// <summary>The device rectangle of the row at <paramref name="index"/> in the view.</summary>
        internal Rectangle RowBounds (int index)
        {
            var view = ViewBounds;
            var height = ScaledRowHeight;
            return new Rectangle (view.Left, view.Top + LogicalToDeviceUnits (AutoScrollPosition.Y) + (index * height), view.Width, height);
        }

        /// <summary>The width of the name column, in device pixels.</summary>
        internal int ScaledNameColumnWidth => ViewBounds.Width * NAME_COL_RATIO_PCT / 100;

        // ── mouse ───────────────────────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            // The pointer is logical and the row geometry device (RC-8).
            var device = LogicalToDeviceUnits (e.Location);

            if (!ViewBounds.Contains (device)) {
                RunCommandAt (device);
                return;
            }

            for (var i = 0; i < _rows.Count; i++) {
                if (!RowBounds (i).Contains (device))
                    continue;

                var item = _rows[i];

                // The expander box of a category toggles it rather than selecting it.
                if (item.GridItems.Count > 0 && ExpanderBounds (i).Contains (device)) {
                    item.Expanded = !item.Expanded;
                    NotifyExpandedChanged ();
                    return;
                }

                if (!ReferenceEquals (item, _selected_item)) {
                    EndEdit (commit: true);
                    _selected_item = item;
                    Invalidate ();
                    SelectedGridItemChanged?.Invoke (this, EventArgs.Empty);
                }

                // A click in the value column of a writable property starts an edit (W6 mechanisms).
                if (device.X > ViewBounds.Left + ScaledNameColumnWidth)
                    BeginEdit (i);

                return;
            }
        }

        /// <summary>The device box of a category's expander glyph.</summary>
        internal Rectangle ExpanderBounds (int index)
        {
            var row = RowBounds (index);
            var side = LogicalToDeviceUnits (9);
            return new Rectangle (row.Left + LogicalToDeviceUnits (3), row.Top + ((row.Height - side) / 2), side, side);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);
            RenderManager.Render (this, e);
        }

        /// <summary>
        /// The concrete <see cref="GridItem"/> the grid builds its tree from (W6 mechanisms).
        /// </summary>
        /// <remarks>
        /// <see cref="GridItem"/> is abstract, as upstream's is, so it needs a concrete entry type --
        /// upstream's is <c>PropertyGridInternal.GridEntry</c>. Expanding or selecting one tells the
        /// owning grid, which is what makes <see cref="GridItem.Expanded"/> and
        /// <see cref="GridItem.Select"/> mean something.
        /// </remarks>
        internal sealed class PropertyGridEntry : GridItem
        {
            internal PropertyGridEntry (PropertyGrid owner) => Owner = owner;

            internal PropertyGrid Owner { get; }

            /// <inheritdoc/>
            public override GridItemType GridItemType
                => PropertyDescriptor is null ? GridItemType.Category : GridItemType.Property;

            /// <inheritdoc/>
            public override bool Expanded {
                get => base.Expanded;
                set {
                    if (base.Expanded == value)
                        return;

                    base.Expanded = value;
                    Owner.NotifyExpandedChanged ();
                }
            }

            /// <inheritdoc/>
            public override void Select () => Owner.SelectedGridItem = this;

            // The value the grid last read, kept so a committed edit can report the old one.
            internal object? CurrentValue { get; set; }
        }
    }

    /// <summary>
    /// Represents a single row in a <see cref="PropertyGrid"/>.
    /// </summary>
    /// <remarks>
    /// Abstract, as <c>System.Windows.Forms.GridItem</c> is: the grid builds its tree out of its own
    /// concrete entries. Until W6 mechanisms it built no tree at all and this type was never
    /// constructed, which is why every member below sat in the stored-only baseline.
    /// </remarks>
    public abstract partial class GridItem
    {
        /// <summary>Gets the current value of the property this item represents.</summary>
        public object? Value { get; internal set; }

        /// <summary>Gets the PropertyDescriptor describing the property this item represents.</summary>
        public System.ComponentModel.PropertyDescriptor? PropertyDescriptor { get; init; }

        /// <summary>Gets the label (property name) of this item.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Gets or sets the item's name. WinForms/Telerik compatibility.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets user data associated with the item. WinForms/Telerik compatibility.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the parent item in the grid's item tree, or null if this is a root item.</summary>
        public GridItem? Parent { get; init; }

        /// <summary>Gets the child items of this item.</summary>
        public GridItemCollection GridItems { get; init; } = new ();

        /// <summary>Gets or sets whether this item is expanded in the grid.</summary>
        /// <remarks>Real as of W6 mechanisms: a collapsed category hides its children from the view.</remarks>
        public virtual bool Expanded { get; set; }

        /// <summary>Selects this item in its owning PropertyGrid.</summary>
        /// <remarks>Real as of W6 mechanisms for the entries a grid builds.</remarks>
        public virtual void Select () { }
    }

    /// <summary>
    /// A collection of GridItem objects, matching System.Windows.Forms.GridItemCollection's shape.
    /// </summary>
    public sealed partial class GridItemCollection : System.Collections.Generic.List<GridItem>
    {
    }
}
