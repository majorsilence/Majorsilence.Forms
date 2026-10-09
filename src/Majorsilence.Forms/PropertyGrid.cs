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
        private object[] _selected_objects = [];
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
        /// <remarks>With several <see cref="SelectedObjects"/> this is the first of them, as upstream's
        /// is; assigning it selects that one object alone.</remarks>
        public object? SelectedObject {
            get => _selected_object;
            set => SelectedObjects = value is null ? [] : [value];
        }

        /// <summary>Gets or sets the objects whose properties are shown and edited together.</summary>
        /// <remarks>
        /// SMP-59: this kept only the first object, so "select five shapes, set FillColor once" edited
        /// one shape, and reading the property back gave an array of length 1. As upstream does
        /// (Controls/PropertyGrid/PropertyGrid.cs, SelectedObjects; MultiSelectRootGridEntry), the grid
        /// now lists only the properties every object has -- same name and type, and not
        /// <c>[MergableProperty (false)]</c> -- shows a value only where all the objects agree, and
        /// writes a committed edit to every object. The getter returns a copy, empty rather than null
        /// when nothing is selected.
        /// </remarks>
        /// <exception cref="ArgumentException">An element of the array is null.</exception>
        public object[]? SelectedObjects {
            get => [.. _selected_objects];
            set {
                var objects = value ?? [];

                for (var i = 0; i < objects.Length; i++) {
                    if (objects[i] is null)
                        throw new ArgumentException ($"Item {i} of the array is null.", nameof (value));
                }

                if (objects.Length == _selected_objects.Length && objects.Zip (_selected_objects, ReferenceEquals).All (same => same))
                    return;

                _selected_objects = [.. objects];
                _selected_object = objects.Length > 0 ? objects[0] : null;
                RebuildEntries ();
                Invalidate ();
                SelectedObjectsChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        // The descriptors the grid shows for one object: the selected tab's, or the type's own.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid uses reflection at runtime; trimming is not supported for this control.")]
        private PropertyDescriptorCollection DescriptorsOf (object component)
            => SelectedTab is { } tab ? tab.GetProperties (component) : TypeDescriptor.GetProperties (component);

        // The descriptor of the same-named, same-typed property on another selected object, or null.
        // Each object needs its own: a reflection descriptor only accepts components of its own type.
        internal PropertyDescriptor? CounterpartOf (object component, PropertyDescriptor property)
        {
            if (ReferenceEquals (component, _selected_object))
                return property;

            return DescriptorsOf (component).Find (property.Name, ignoreCase: false) is { } other
                   && other.PropertyType == property.PropertyType
                ? other
                : null;
        }

        // Upstream's property merger: shown for a multiple selection only when every object has it and
        // it allows merging.
        private bool IsCommonToSelection (PropertyDescriptor property)
        {
            if (_selected_objects.Length < 2)
                return true;

            if (property.Attributes[typeof (MergablePropertyAttribute)] is MergablePropertyAttribute { AllowMerge: false })
                return false;

            return _selected_objects.All (o => CounterpartOf (o, property) is not null);
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
            var descriptors = DescriptorsOf (_selected_object).Cast<PropertyDescriptor> ();

            var props = descriptors.Where (p => p.IsBrowsable).Where (MatchesBrowsableAttributes).Where (IsCommonToSelection);

            // By the label the row shows, ignoring case, as upstream's grid compares them: ordinal by
            // Name put "BodyColumnSpacing" before "BodyColumns" and sorted by names the user never sees.
            if (PropertySort is PropertySort.Alphabetical or PropertySort.CategorizedAlphabetical)
                props = props.OrderBy (p => p.DisplayName, LabelOrder);

            var categorised = PropertySort is PropertySort.CategorizedAlphabetical or PropertySort.Categorized;

            if (categorised)
                props = props.OrderBy (p => p.Category == "Misc" ? "zzz" : p.Category ?? "zzz", LabelOrder)
                             .ThenBy (p => p.DisplayName, LabelOrder);

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
                    ValuesDiffer = ValuesDiffer (prop),
                };

                if (parent is null)
                    roots.Add (item);
                else
                    parent.GridItems.Add (item);

                AddSubProperties (item, prop, item.Value, depth: 1);
            }

            OnEntriesRebuilt ();
            RebuildRows ();
        }

        // How rows are ordered by label: case-insensitively in the current culture, as upstream's grid.
        private static StringComparer LabelOrder => StringComparer.CurrentCultureIgnoreCase;

        // A property whose type converter exposes properties of its own (ExpandableObjectConverter: a
        // margin's sides, a point's X and Y) gets them as child rows, collapsed until expanded, as
        // upstream's grid does. They were never built, so such a value could only be edited as one
        // string -- ReportDesigner's Page Margins, Page Header and Page Footer had no expander at all.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid reads properties through TypeDescriptor at runtime; trimming is not supported for this control.")]
        private void AddSubProperties (PropertyGridEntry owner, PropertyDescriptor property, object? value, int depth)
        {
            // One selected object only: a child row edits that object's value, and there is no single
            // value to expand when the selection disagrees. The depth stops a type that contains itself.
            if (value is null || owner.ValuesDiffer || _selected_objects.Length > 1 || depth > 4)
                return;

            PropertyDescriptorCollection? subs;

            try {
                if (!property.Converter.GetPropertiesSupported ())
                    return;

                subs = property.Converter.GetProperties (value);
            } catch {
                return;
            }

            if (subs is null)
                return;

            foreach (var sub in subs.Cast<PropertyDescriptor> ().Where (p => p.IsBrowsable).OrderBy (p => p.DisplayName, LabelOrder)) {
                object? sub_value;

                try {
                    sub_value = sub.GetValue (value);
                } catch {
                    sub_value = null;
                }

                var child = new PropertyGridEntry (this) {
                    Name = sub.Name,
                    Label = sub.DisplayName,
                    PropertyDescriptor = sub,
                    Parent = owner,
                    Value = sub_value,
                    Component = value,
                };

                owner.GridItems.Add (child);
                AddSubProperties (child, sub, sub_value, depth + 1);
            }
        }

        /// <summary>
        /// Writes a child row's edited parent value back up the chain: a parent the converter rebuilds
        /// from its parts (a struct, an immutable value) is recreated, and every parent is set on its
        /// own owner -- so the selected object's setter runs, as it does for a top-level edit.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "PropertyGrid edits through TypeDescriptor at runtime; trimming is not supported for this control.")]
        private void PropagateToParent (PropertyGridEntry child)
        {
            if (child.Parent is not PropertyGridEntry { PropertyDescriptor: { } parent_property } parent || child.Component is null)
                return;

            var parent_value = child.Component;

            if (parent_property.Converter.GetCreateInstanceSupported ()) {
                var parts = new System.Collections.Hashtable ();

                foreach (var sibling in parent.GridItems.OfType<PropertyGridEntry> ())
                    if (sibling.PropertyDescriptor is { } sibling_property)
                        parts[sibling_property.Name] = sibling_property.GetValue (parent_value);

                parent_value = parent_property.Converter.CreateInstance (parts) ?? parent_value;

                foreach (var sibling in parent.GridItems.OfType<PropertyGridEntry> ())
                    sibling.Component = parent_value;
            }

            if (parent.Component is { } owner)
                parent_property.SetValue (owner, parent_value);
            else
                foreach (var target in _selected_objects)
                    CounterpartOf (target, parent_property)?.SetValue (target, parent_value);

            parent.Value = parent.Component is { } read_from ? parent_property.GetValue (read_from) : ReadValue (parent_property);
            parent.CurrentValue = parent.Value;

            PropagateToParent (parent);
        }

        // ── extension points for a derived grid (the Telerik layer's RadPropertyGrid) ───────────────

        /// <summary>Called once the item tree has been rebuilt from the selected object.</summary>
        internal virtual void OnEntriesRebuilt () { }

        /// <summary>Whether a row is shown; a hidden property row takes no space in the view.</summary>
        internal virtual bool IsRowVisible (GridItem item) => true;

        /// <summary>Whether an otherwise writable item may be edited in place.</summary>
        internal virtual bool AllowsEdit (GridItem item) => true;

        /// <summary>An editor to use in place of the grid's own, or null for the default.</summary>
        internal virtual Control? CreateEditor (GridItem item) => null;

        /// <summary>Called once an editor has been created and seeded for an item.</summary>
        internal virtual void OnEditorCreated (GridItem item, Control editor) { }

        /// <summary>Called after an editor has closed, committed or not.</summary>
        internal virtual void OnEditEnded (GridItem item) { }

        /// <summary>How a row is drawn: its label, value text and colours (empty for the defaults).</summary>
        internal virtual RowFormat FormatRow (GridItem item, string label, string value)
            => new RowFormat (label, value, Color.Empty, Color.Empty);

        /// <summary>The label, value text and colours one row is painted with.</summary>
        internal readonly struct RowFormat
        {
            internal RowFormat (string label, string value, Color foreColor, Color backColor)
            {
                Label = label;
                Value = value;
                ForeColor = foreColor;
                BackColor = backColor;
            }

            internal string Label { get; }
            internal string Value { get; }
            internal Color ForeColor { get; }
            internal Color BackColor { get; }
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

        // With several objects selected, the value is shown only when they all hold the same one;
        // otherwise the cell is blank, as upstream's multi-select entry leaves it.
        private object? ReadValue (PropertyDescriptor property)
        {
            try {
                if (_selected_object is null)
                    return null;

                var first = property.GetValue (_selected_object);

                foreach (var other in _selected_objects.Skip (1)) {
                    if (!Equals (CounterpartOf (other, property)?.GetValue (other), first))
                        return null;
                }

                return first;
            } catch {
                return null;
            }
        }

        // Whether the selected objects disagree about a property's value, so the cell shows nothing.
        internal bool ValuesDiffer (PropertyDescriptor property)
        {
            if (_selected_objects.Length < 2)
                return false;

            try {
                var first = property.GetValue (_selected_object);
                return _selected_objects.Skip (1).Any (o => !Equals (CounterpartOf (o, property)?.GetValue (o), first));
            } catch {
                return false;
            }
        }

        // The visible rows: every root, and a category's children only while it is expanded.
        private void RebuildRows ()
        {
            _rows.Clear ();

            foreach (var root in roots) {
                if (root.GridItemType != GridItemType.Category && !IsRowVisible (root))
                    continue;

                _rows.Add (root);
                AddExpandedChildren (root);
            }

            UpdateScrollExtent ();
        }

        // An expanded row's children, and theirs in turn: a category's properties, a property's parts.
        private void AddExpandedChildren (GridItem item)
        {
            if (!item.Expanded)
                return;

            foreach (var child in item.GridItems.Where (IsRowVisible)) {
                _rows.Add (child);
                AddExpandedChildren (child);
            }
        }

        /// <summary>How many properties a row sits under (0 for a property of the object itself).</summary>
        internal static int PropertyDepth (GridItem item)
        {
            var depth = 0;

            for (var parent = item.Parent; parent is not null; parent = parent.Parent)
                if (parent.GridItemType == GridItemType.Property)
                    depth++;

            return depth;
        }

        /// <summary>
        /// The logical indent of a property row's name. Rows under a category, or in a grid with an
        /// expandable row, leave the left margin to the expanders; each level of parts goes 10px further.
        /// </summary>
        internal int NameIndent (GridItem item)
        {
            var margin = item.Parent is not null || roots.Any (r => r.GridItems.Count > 0) ? 16 : 2;
            return margin + 10 * PropertyDepth (item);
        }

        /// <summary>Re-reads which rows are shown, after a derived grid changes an item's visibility.</summary>
        internal void RefreshRows ()
        {
            RebuildRows ();
            Invalidate ();
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

            if (item is PropertyGridEntry { ValuesDiffer: true })
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
                var client = DeviceClientRectangle;
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

                var client = DeviceClientRectangle;
                return new Rectangle (client.Left, client.Bottom - ScaledHelpHeight - height, client.Width, height);
            }
        }

        /// <summary>The device rectangle of the help pane; empty when it is not shown.</summary>
        internal Rectangle HelpBounds {
            get {
                var height = ScaledHelpHeight;

                if (height == 0)
                    return Rectangle.Empty;

                var client = DeviceClientRectangle;
                return new Rectangle (client.Left, client.Bottom - height, client.Width, height);
            }
        }

        private int ScaledHelpHeight => HelpVisible ? LogicalToDeviceUnits (HELP_HEIGHT) : 0;

        private int ScaledCommandsHeight
            => CommandsVisible ? LogicalToDeviceUnits ((Verbs.Count * COMMANDS_ROW_HEIGHT) + 8) : 0;

        private int ScaledRowHeight => LogicalToDeviceUnits (RowHeight);

        // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets
        // upstream's rows: the grid's own font, and a row as tall as that font plus 3 (19px for Segoe UI
        // 9pt). The theme's fixed 22px rows in 10px type showed fewer, harder to read properties.
        private bool UsesUpstreamRows => ControlPaint.UsesUpstreamGlyphs;

        /// <summary>The logical height of a row.</summary>
        internal int RowHeight => UsesUpstreamRows ? Font.Height + 3 : ROW_HEIGHT;

        /// <summary>The typeface rows are drawn in.</summary>
        internal SkiaSharp.SKTypeface RowTypeface => UsesUpstreamRows ? GetEffectiveFont () : Theme.UIFont;

        /// <summary>The logical pixel size rows are drawn at.</summary>
        internal int RowFontSize => UsesUpstreamRows ? GetEffectiveFontSize () : 10;

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
        /// <remarks>Half the view under upstream's rows, as WinForms' default label ratio splits it; the
        /// theme's 40% cut ReportDesigner's longer names (BodyColumnSpacing) short.</remarks>
        internal int ScaledNameColumnWidth => ViewBounds.Width * (UsesUpstreamRows ? 50 : NAME_COL_RATIO_PCT) / 100;

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

            // A category's sits in the margin; an expandable property's just before its name.
            var item = index >= 0 && index < _rows.Count ? _rows[index] : null;
            var left = item is null || item.GridItemType == GridItemType.Category ? 3 : NameIndent (item) - 13;

            return new Rectangle (row.Left + LogicalToDeviceUnits (left), row.Top + ((row.Height - side) / 2), side, side);
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

            // The selected objects held different values when the grid last read this one.
            internal bool ValuesDiffer { get; set; }

            // The object a part's descriptor reads and writes: its parent row's value. Null for a
            // property of the selected objects themselves.
            internal object? Component { get; set; }
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
