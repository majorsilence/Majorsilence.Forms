using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Compat stand-in for Telerik's RadPropertyStore: a hand-built set of properties.</summary>
    /// <remarks>
    /// Real as of W6 mechanisms (#176): assigned as a property grid's <c>SelectedObject</c>, the store
    /// describes each <see cref="PropertyStoreItem"/> as a property -- its <see cref="PropertyStoreItem.Label"/>
    /// (or name) as the display name, its <see cref="PropertyStoreItem.Category"/>,
    /// <see cref="PropertyStoreItem.Description"/> and <see cref="PropertyStoreItem.ReadOnly"/> flag --
    /// so the grid shows and edits them, and an edit is written back to
    /// <see cref="PropertyStoreItem.Value"/>. <see cref="PropertyStoreItem.DefaultValue"/> is what a
    /// reset returns to.
    /// </remarks>
    public class RadPropertyStore : ICustomTypeDescriptor
    {
        /// <summary>The stored items.</summary>
        public System.Collections.Generic.List<PropertyStoreItem> Items { get; } = new ();

        /// <summary>Adds an item to the store.</summary>
        public void Add (PropertyStoreItem item) => Items.Add (item);

        /// <summary>Returns the stored items as an array. Mirrors Telerik.</summary>
        public PropertyStoreItem[] ToArray () => Items.ToArray ();

        /// <summary>Gets the item with the given name, or null.</summary>
        public PropertyStoreItem? this[string name]
            => Items.Find (item => string.Equals (item.Name, name, StringComparison.OrdinalIgnoreCase));

#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties ()
            => new (Items.ConvertAll (item => (PropertyDescriptor) new StoreItemDescriptor (item)).ToArray ());

#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties (Attribute[]? attributes)
            => ((ICustomTypeDescriptor) this).GetProperties ();

        AttributeCollection ICustomTypeDescriptor.GetAttributes () => AttributeCollection.Empty;
        string? ICustomTypeDescriptor.GetClassName () => nameof (RadPropertyStore);
        string? ICustomTypeDescriptor.GetComponentName () => null;
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        TypeConverter ICustomTypeDescriptor.GetConverter () => new TypeConverter ();
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent () => null;
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty () => null;
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        object? ICustomTypeDescriptor.GetEditor (Type editorBaseType) => null;
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents () => EventDescriptorCollection.Empty;
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode ("A property store is described through TypeDescriptor, which trimming cannot see through.")]
#endif
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents (Attribute[]? attributes) => EventDescriptorCollection.Empty;
        object? ICustomTypeDescriptor.GetPropertyOwner (PropertyDescriptor? pd) => this;

        // One store item, as a property of the store.
        private sealed class StoreItemDescriptor : PropertyDescriptor
        {
            private readonly PropertyStoreItem item;

            internal StoreItemDescriptor (PropertyStoreItem item) : base (item.Name, Describe (item)) => this.item = item;

            private static Attribute[] Describe (PropertyStoreItem item)
            {
                var attributes = new System.Collections.Generic.List<Attribute> ();

                if (item.Label.Length > 0)
                    attributes.Add (new DisplayNameAttribute (item.Label));
                if (item.Category.Length > 0)
                    attributes.Add (new CategoryAttribute (item.Category));
                if (item.Description.Length > 0)
                    attributes.Add (new DescriptionAttribute (item.Description));
                if (item.ReadOnly)
                    attributes.Add (ReadOnlyAttribute.Yes);

                foreach (var extra in item.Attributes)
                    if (extra is Attribute attribute)
                        attributes.Add (attribute);

                return attributes.ToArray ();
            }

            public override Type ComponentType => typeof (RadPropertyStore);
            public override bool IsReadOnly => item.ReadOnly;
            public override Type PropertyType => item.Type;
            public override object? GetValue (object? component) => item.Value;
            public override void SetValue (object? component, object? value) => item.Value = value;
            public override bool CanResetValue (object component) => !item.ReadOnly && !Equals (item.Value, item.DefaultValue);
            public override void ResetValue (object component) => item.Value = item.DefaultValue;
            public override bool ShouldSerializeValue (object component) => !Equals (item.Value, item.DefaultValue);
        }
    }

    /// <summary>Compat stand-in for Telerik's PropertyStoreItem.</summary>
    public class PropertyStoreItem
    {
        /// <summary>Initializes an item for a typed property value.</summary>
        public PropertyStoreItem (Type type, string name, object? value)
        {
            Type = type;
            Name = name;
            Value = value;
        }

        /// <summary>Initializes an item with a description and category (Telerik designer shape).</summary>
        public PropertyStoreItem (Type type, string name, object? value, string description, string category)
            : this (type, name, value)
        {
            Description = description;
            Category = category;
        }

        /// <summary>Initializes an item with a description, category, and read-only flag (Telerik designer shape).</summary>
        public PropertyStoreItem (Type type, string name, object? value, string description, string category, bool isReadOnly)
            : this (type, name, value, description, category)
        {
            ReadOnly = isReadOnly;
        }

        /// <summary>Initializes an item for a typed property value with a default.</summary>
        public PropertyStoreItem (Type type, string name, object? value, object? defaultValue)
            : this (type, name, value)
        {
            DefaultValue = defaultValue;
        }

        /// <summary>The property type.</summary>
        public Type Type { get; }

        /// <summary>The property name.</summary>
        public string Name { get; }

        /// <summary>Telerik alias of <see cref="Name"/>.</summary>
        public string PropertyName => Name;

        /// <summary>The display label shown for the property.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>The attributes applied to the property (Telerik uses these for category/editor hints).</summary>
        public System.Collections.Generic.List<object> Attributes { get; } = new ();

        /// <summary>The property value.</summary>
        public object? Value { get; set; }

        /// <summary>The default value.</summary>
        public object? DefaultValue { get; set; }

        /// <summary>The property description (tooltip/help text).</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>The category the property is grouped under.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Whether the property is read-only.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Telerik alias of <see cref="Type"/>.</summary>
        public Type PropertyType => Type;
    }

    /// <summary>Compat stand-in for Telerik's RadGridViewElement (root visual element of a grid).</summary>
    public class RadGridViewElement
    {
        /// <summary>Initializes a new element for the given grid.</summary>
        public RadGridViewElement (RadGridView owner) { Owner = owner; }

        /// <summary>The grid this element belongs to.</summary>
        public RadGridView Owner { get; }
    }

    /// <summary>Provides data for cell value push/pull events. Mirrors Telerik's GridViewCellValueEventArgs.</summary>
    public class GridViewCellValueEventArgs : EventArgs
    {
        /// <summary>Initializes the args for a row/column and value.</summary>
        public GridViewCellValueEventArgs (int rowIndex, int columnIndex, object? value)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            Value = value;
        }

        /// <summary>The row index of the cell.</summary>
        public int RowIndex { get; }

        /// <summary>The column index of the cell.</summary>
        public int ColumnIndex { get; }

        /// <summary>The cell value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for grid collection change notifications. Mirrors Telerik's shape.</summary>
    public class GridViewCollectionChangedEventArgs : EventArgs
    {
        /// <summary>Initializes the args.</summary>
        public GridViewCollectionChangedEventArgs (object? newItems = null) { NewItems = newItems; }

        /// <summary>The items involved in the change.</summary>
        public object? NewItems { get; }
    }

    /// <summary>Compat stand-in for Telerik's GridViewListSource (the grid's list data source seam).</summary>
    public class GridViewListSource
    {
        /// <summary>Initializes the source for a grid.</summary>
        public GridViewListSource (RadGridView owner) { Owner = owner; }

        /// <summary>The owning grid.</summary>
        public RadGridView Owner { get; }
    }

    /// <summary>
    /// Compat stand-in for Telerik's PropertyGridItem: one property of the object a
    /// <see cref="RadPropertyGrid"/> inspects. Derives from the core <see cref="Majorsilence.Forms.GridItem"/>
    /// so WinForms migration code can TryCast a PropertyGrid's SelectedGridItem to it.
    /// </summary>
    /// <remarks>
    /// Live as of W6 mechanisms (#176) for an item the grid built: <see cref="Value"/> reads and writes
    /// the inspected property, <see cref="Label"/> is the text the row shows, <see cref="Visible"/> and
    /// <see cref="ReadOnly"/> hide the row and lock it, and <see cref="FormattedValue"/>,
    /// <see cref="PropertyType"/> and <see cref="Category"/> answer from the property's descriptor. An
    /// item built by application code with <c>new</c> is detached and simply stores what it is given.
    /// </remarks>
    public class PropertyGridItem : Majorsilence.Forms.GridItem
    {
        /// <summary>Initializes a detached item.</summary>
        public PropertyGridItem () { }

        internal PropertyGridItem (RadPropertyGrid grid, Majorsilence.Forms.GridItem entry)
        {
            Grid = grid;
            Entry = entry;
            base.Name = entry.Name;
            label = entry.Label;
            original_value = entry.Value;
        }

        // The grid and the core entry this item speaks for; null for a detached item.
        internal RadPropertyGrid? Grid { get; }
        internal Majorsilence.Forms.GridItem? Entry { get; }

        private string? label;
        private object? detached_value;
        private object? original_value;
        private object? formatted_value;
        private Type? property_type;
        private string? category;
        private bool visible = true;
        private bool read_only;

        /// <summary>The property name shown for the item.</summary>
        public new string Name {
            get => base.Name;
            set => base.Name = value ?? string.Empty;
        }

        /// <summary>The text the row shows for the property; defaults to its display name.</summary>
        public new string Label {
            get => label ?? string.Empty;
            set {
                label = value ?? string.Empty;
                Grid?.Invalidate ();
            }
        }

        /// <summary>The current value of the property; setting it writes the inspected object.</summary>
        /// <remarks>A write the property's setter refuses leaves the value alone, as an edit in the
        /// grid does. <see cref="RadPropertyGrid.ItemValueChanged"/> is raised for a write that
        /// takes.</remarks>
        public new object? Value {
            get => Entry is { } entry ? entry.Value : detached_value;
            set {
                if (Grid is { } grid && Entry is { } entry)
                    grid.WriteValue (this, entry, value);
                else
                    detached_value = value;
            }
        }

        /// <summary>The value the property had when the grid built this item.</summary>
        public object? OriginalValue {
            get => original_value;
            set => original_value = value;
        }

        /// <summary>The value as the row shows it, through the property's type converter.</summary>
        public object? FormattedValue {
            get => Entry is { } entry ? Majorsilence.Forms.PropertyGrid.ValueTextOf (entry) : formatted_value;
            set => formatted_value = value;
        }

        /// <summary>The declared type of the property.</summary>
        public Type? PropertyType {
            get => Entry?.PropertyDescriptor?.PropertyType ?? property_type;
            set => property_type = value;
        }

        /// <summary>The category the property is grouped under.</summary>
        public string? Category {
            get => Entry?.PropertyDescriptor is { } descriptor ? descriptor.Category : category;
            set => category = value;
        }

        /// <summary>The property's description, shown in the grid's help pane.</summary>
        public string Description => Entry?.PropertyDescriptor?.Description ?? string.Empty;

        /// <summary>Gets or sets whether the row is shown.</summary>
        public bool Visible {
            get => visible;
            set {
                if (visible == value)
                    return;

                visible = value;
                Grid?.RefreshRows ();
            }
        }

        /// <summary>Gets or sets whether the value can be edited, on top of the property's own read-only flag.</summary>
        public bool ReadOnly {
            get => read_only || Entry?.PropertyDescriptor?.IsReadOnly == true;
            set {
                read_only = value;
                Grid?.Invalidate ();
            }
        }

        /// <summary>User data associated with the item (settable hide of the base member for source compat).</summary>
        /// <remarks>Stored, and legitimately so: it is the application's own data.</remarks>
        public new object? Tag { get; set; }

        /// <summary>The validation error message for the item (empty when valid).</summary>
        /// <remarks>Stored: the grid has no per-row error glyph to show it with. Recorded rather than
        /// drawn so a handler that sets it and reads it back still works.</remarks>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>The image key shown next to the item.</summary>
        /// <remarks>Stored: the grid's rows carry no image column.</remarks>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>The custom attributes attached to the property.</summary>
        public List<Attribute> Attributes { get; } = new ();

        /// <summary>Selects this item in its grid.</summary>
        public override void Select ()
        {
            if (Grid is { } grid && Entry is { } entry)
                grid.SelectEntry (entry);
        }
    }

    /// <summary>Compat stand-in for Telerik's PropertyGridGroupItem (a category header row). Derives from PropertyGridItem so grid-item casts succeed.</summary>
    public class PropertyGridGroupItem : PropertyGridItem
    {
        /// <summary>Initializes a detached group.</summary>
        public PropertyGridGroupItem () { }

        internal PropertyGridGroupItem (RadPropertyGrid grid, Majorsilence.Forms.GridItem entry) : base (grid, entry) { }

        /// <summary>The items in this group.</summary>
        public new PropertyGridItemCollection GridItems { get; } = new ();

        /// <summary>Gets or sets whether the group shows its items.</summary>
        public new bool Expanded {
            get => Entry?.Expanded ?? base.Expanded;
            set {
                if (Entry is { } entry)
                    entry.Expanded = value;
                else
                    base.Expanded = value;
            }
        }

        /// <summary>Shows the group's items.</summary>
        public void Expand () => Expanded = true;

        /// <summary>Hides the group's items.</summary>
        public void Collapse () => Expanded = false;
    }

    /// <summary>Compat stand-in for the property-grid item visual element.</summary>
    public class PropertyGridItemElement : RadElement
    {
        /// <summary>Gets or sets the item shown by the element.</summary>
        public PropertyGridItem? Data { get; set; }

        /// <summary>Gets the label text element.</summary>
        public LightVisualElement TextElement { get; } = new LightVisualElement ();

        /// <summary>Gets the value text element.</summary>
        public LightVisualElement ValueElement { get; } = new LightVisualElement ();
    }

    /// <summary>Compat stand-in for the property-grid group visual element.</summary>
    public class PropertyGridGroupElement : RadElement
    {
        /// <summary>Gets or sets the group item shown by the element.</summary>
        public object? Data { get; set; }

        /// <summary>Gets the label text element.</summary>
        public LightVisualElement TextElement { get; } = new LightVisualElement ();
    }

    /// <summary>Compat stand-in for the property-grid expander visual element.</summary>
    public class PropertyGridExpanderElement : RadElement
    {
        /// <summary>Gets the element bounds in control coordinates (empty stub).</summary>
        public System.Drawing.Rectangle ControlBoundingRectangle => System.Drawing.Rectangle.Empty;
    }

    /// <summary>Compat stand-in for the property-grid spin (numeric) editor.</summary>
    public class PropertyGridSpinEditor
    {
        /// <summary>Gets the editor's visual element.</summary>
        public RadElement EditorElement { get; } = new RadElement ();

        /// <summary>Gets or sets the editor's current value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for property-grid value validation. Mirrors Telerik's shape.</summary>
    public class PropertyValidatingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>The proposed new value.</summary>
        public object? NewValue { get; set; }

        /// <summary>The previous value.</summary>
        public object? OldValue { get; set; }

        /// <summary>The item being validated.</summary>
        public PropertyGridItem? Item { get; set; }
    }

    /// <summary>Provides data for property-grid editor initialization. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemEditorInitializedEventArgs : EventArgs
    {
        /// <summary>The item whose editor was initialized.</summary>
        public PropertyGridItem? Item { get; set; }

        /// <summary>The editor instance.</summary>
        public object? Editor { get; set; }
    }

    /// <summary>Provides data for property-grid item formatting. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemFormattingEventArgs : EventArgs
    {
        /// <summary>The item being formatted.</summary>
        public PropertyGridItem? Item { get; set; }

        /// <summary>The visual element being formatted.</summary>
        public PropertyGridItemElement VisualElement { get; set; } = new PropertyGridItemElement ();
    }


    /// <summary>Provides data for element render callbacks. Mirrors Telerik's RenderElementEventArgs.</summary>
    public class RenderElementEventArgs : EventArgs
    {
        /// <summary>The element being rendered.</summary>
        public object? Element { get; set; }
    }

    /// <summary>Provides data for custom filtering. Mirrors Telerik's GridViewCustomFilteringEventArgs.</summary>
    public class GridViewCustomFilteringEventArgs : EventArgs
    {
        /// <summary>The row being evaluated.</summary>
        public object? Row { get; set; }

        /// <summary>Whether the row is visible under the filter.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Whether the event was handled by the custom filter.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>Provides data for cell validation completion. Mirrors Telerik's CellValidatedEventArgs.</summary>
    public class CellValidatedEventArgs : EventArgs
    {
        /// <summary>The row index of the validated cell.</summary>
        public int RowIndex { get; set; }

        /// <summary>The column index of the validated cell.</summary>
        public int ColumnIndex { get; set; }

        /// <summary>The validated column.</summary>
        public Majorsilence.Forms.DataGridViewColumn? Column { get; set; }

        /// <summary>The validated row.</summary>
        public GridViewRowInfo? Row { get; set; }

        /// <summary>The validated value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for RadPropertyGrid item edit completion. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemEditedEventArgs : EventArgs
    {
        /// <summary>The edited item.</summary>
        public PropertyGridItem? Item { get; set; }
    }

    /// <summary>Provides data for RadPropertyGrid editor selection. Mirrors Telerik's shape.</summary>
    public class PropertyGridEditorRequiredEventArgs : EventArgs
    {
        /// <summary>The item needing an editor.</summary>
        public PropertyGridItem? Item { get; set; }

        /// <summary>The editor type to use.</summary>
#if NET
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers (System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
        public Type? EditorType { get; set; }

        /// <summary>The editor instance to use.</summary>
        public object? Editor { get; set; }
    }

    /// <summary>Provides data for RadPropertyGrid item value changes. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemValueChangedEventArgs : EventArgs
    {
        /// <summary>The item whose value changed.</summary>
        public PropertyGridItem? Item { get; set; }
    }

    /// <summary>Compat stand-in for Telerik's RadTextBoxItem (the text portion of editor elements).</summary>
    public class RadTextBoxItem : RadElement
    {
        /// <summary>Gets or sets the text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the text alignment.</summary>
        public ContentAlignment Alignment { get; set; } = ContentAlignment.MiddleLeft;
    }

    /// <summary>Compat stand-in for a text-box editor's visual element.</summary>
    public class BaseTextBoxEditorElement : RadElement
    {
        /// <summary>Gets the hosted text-box item.</summary>
        public RadTextBoxItem TextBoxItem { get; } = new RadTextBoxItem ();
    }

    /// <summary>Compat stand-in for a spin editor's visual element.</summary>
    public class BaseSpinEditorElement : BaseTextBoxEditorElement { }

    /// <summary>Compat stand-in for a drop-down-list editor's visual element.</summary>
    public class BaseDropDownListEditorElement : RadElement
    {
        /// <summary>Gets or sets the bound list source.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the member shown for each item.</summary>
        public string DisplayMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the member used as each item's value.</summary>
        public string ValueMember { get; set; } = string.Empty;

        /// <summary>Gets or sets the selected value.</summary>
        public object? SelectedValue { get; set; }

        /// <summary>Gets or sets the selected index.</summary>
        public int SelectedIndex { get; set; } = -1;

        /// <summary>Gets or sets the editor text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the drop-down style.</summary>
        public RadDropDownStyle DropDownStyle { get; set; } = RadDropDownStyle.DropDownList;

        /// <summary>Raised when the selected value changes. Never raised by the stub editor.</summary>
#pragma warning disable CS0067
        public event EventHandler? SelectedValueChanged;
#pragma warning restore CS0067
    }

    /// <summary>Compat stand-in for a date-time editor's visual element.</summary>
    public class BaseDateTimeEditorElement : RadElement
    {
        /// <summary>Gets or sets the display format string.</summary>
        public string CustomFormat { get; set; } = string.Empty;

        /// <summary>Gets or sets the date format mode.</summary>
        public object? Format { get; set; }

        /// <summary>Gets or sets the editor value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Compat stand-in for a browse (file/folder) editor's visual element.</summary>
    public class RadBrowseEditorElement : RadElement
    {
        /// <summary>Gets or sets which browse dialog the editor opens.</summary>
        public BrowseEditorDialogType DialogType { get; set; } = BrowseEditorDialogType.OpenFileDialog;

        /// <summary>Gets or sets the editor value (the chosen path).</summary>
        public object? Value { get; set; }
    }

    /// <summary>Specifies the dialog a browse editor opens. Compat for Telerik.</summary>
    public enum BrowseEditorDialogType
    {
        /// <summary>An open-file dialog.</summary>
        OpenFileDialog = 0,
        /// <summary>A folder-browser dialog.</summary>
        FolderBrowseDialog = 1
    }

    /// <summary>Compat stand-in for a checkbox item element inside the property grid.</summary>
    public class PropertyGridCheckBoxItemElement : PropertyGridItemElement
    {
        /// <summary>Gets or sets the checked value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Compat stand-in for Telerik's text-box property editor.</summary>
    public class PropertyGridTextBoxEditor
    {
        /// <summary>Gets the editor's visual element.</summary>
        public RadElement EditorElement { get; } = new BaseTextBoxEditorElement ();
    }

    /// <summary>Compat stand-in for Telerik's drop-down-list property editor.</summary>
    public class PropertyGridDropDownListEditor
    {
        /// <summary>Gets the editor's visual element.</summary>
        public RadElement EditorElement { get; } = new BaseDropDownListEditorElement ();

        /// <summary>Gets or sets the drop-down style (forwards to the element).</summary>
        public RadDropDownStyle DropDownStyle {
            get => ((BaseDropDownListEditorElement)EditorElement).DropDownStyle;
            set => ((BaseDropDownListEditorElement)EditorElement).DropDownStyle = value;
        }
    }

    /// <summary>Compat stand-in for Telerik's date-time property editor.</summary>
    public class PropertyGridDateTimeEditor
    {
        /// <summary>Gets the editor's visual element.</summary>
        public RadElement EditorElement { get; } = new BaseDateTimeEditorElement ();
    }

    /// <summary>Compat stand-in for Telerik's browse (file) property editor.</summary>
    public class PropertyGridBrowseEditor
    {
        /// <summary>Gets the editor's visual element.</summary>
        public RadElement EditorElement { get; } = new RadBrowseEditorElement ();
    }

    /// <summary>Compat stand-in for Telerik's DataGroup (grid grouping node).</summary>
    public class DataGroup
    {
        // Set when the grid projects this group, so Expand/Collapse can reach the collapse state the
        // grid actually paints from rather than flipping a local bool nobody reads.
        internal IGridGroupOwner? Owner { get; set; }

        /// <summary>The group key.</summary>
        public object? Key { get; set; }

        /// <summary>
        /// The rows in this group -- the leaf rows beneath it, including those in nested groups.
        /// </summary>
        public System.Collections.Generic.List<GridViewRowInfo> Items { get; } = new ();

        /// <summary>
        /// The groups nested directly inside this one, empty for a leaf group.
        /// </summary>
        /// <remarks>
        /// The grid has always grouped to any depth (<c>GroupDescriptors</c> is a list), and
        /// <see cref="DataGroup"/> was flat -- so multi-level grouping could not be described at all,
        /// even once the projection existed.
        /// </remarks>
        public System.Collections.Generic.List<DataGroup> Groups { get; } = new ();

        /// <summary>How deep this group sits: 0 for a top-level group.</summary>
        public int Level { get; internal set; }

        /// <summary>The value every row in this group shares, as displayed.</summary>
        public string HeaderText { get; internal set; } = string.Empty;

        /// <summary>Gets the number of rows in the group.</summary>
        public int ItemCount => Items.Count;

        /// <summary>Gets the row at the specified index within the group.</summary>
        public GridViewRowInfo this[int index] => Items[index];

        /// <summary>Gets or sets whether the group is expanded on screen.</summary>
        public bool IsExpanded {
            get => Owner is null ? _expanded : Owner.IsGroupExpanded (this);
            set {
                _expanded = value;

                Owner?.SetGroupExpanded (this, value);
            }
        }

        private bool _expanded = true;

        /// <summary>Expands the group.</summary>
        public void Expand () => IsExpanded = true;

        /// <summary>Collapses the group.</summary>
        public void Collapse () => IsExpanded = false;
    }

    /// <summary>
    /// How a <see cref="DataGroup"/> reaches the grid that produced it, so its expand state is the
    /// grid's rather than a copy.
    /// </summary>
    internal interface IGridGroupOwner
    {
        /// <summary>Whether the grid currently shows this group expanded.</summary>
        bool IsGroupExpanded (DataGroup group);

        /// <summary>Expands or collapses the group on the grid and repaints.</summary>
        void SetGroupExpanded (DataGroup group, bool expanded);
    }

    /// <summary>Provides data for RadDock tab-strip creation. Mirrors Telerik's shape.</summary>
    public class DockTabStripNeededEventArgs : EventArgs
    {
        /// <summary>The strip to use (assign to supply one).</summary>
        public object? Strip { get; set; }
    }

    /// <summary>Provides data for a dock's selected-tab change. Mirrors Telerik's shape.</summary>
    public class SelectedTabChangedEventArgs : EventArgs
    {
        /// <summary>The previously selected dock window.</summary>
        public DockWindowBase? OldWindow { get; set; }

        /// <summary>The newly selected dock window.</summary>
        public DockWindowBase? NewWindow { get; set; }
    }

    /// <summary>Provides data for the docking context-menu display. Mirrors Telerik's shape.</summary>
    public class ContextMenuDisplayingEventArgs : EventArgs
    {
        /// <summary>The menu items about to be shown; handlers append their own entries.</summary>
        public List<object> MenuItems { get; } = new ();

        /// <summary>The dock window the menu applies to.</summary>
        public DockWindowBase? DockWindow { get; set; }
    }

    /// <summary>
    /// Compat stand-in for Telerik's RadCheckBoxEditor (the in-place editor of checkbox grid
    /// cells). Legacy handlers TryCast the event sender to this; the compat grid uses its own
    /// editing controls, so the cast yields null and such handlers no-op.
    /// </summary>
    public class RadCheckBoxEditor : IInputEditor
    {
        /// <summary>Gets or sets the editor's current value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Compat stand-in for Telerik's IInputEditor (the typed surface of RadGridView.ActiveEditor).</summary>
    public interface IInputEditor
    {
        /// <summary>Gets or sets the editor's current value.</summary>
        object? Value { get; set; }
    }

    /// <summary>
    /// Compat stand-in for Telerik's docking context-menu service (obtained through
    /// <c>RadDock.GetService(Of ContextMenuService)()</c>). The compat dock shows no built-in
    /// context menus, so the event is declared but never raised.
    /// </summary>
    public class ContextMenuService
    {
        /// <summary>Raised before the docking context menu is shown. Never raised by the compat dock.</summary>
#pragma warning disable CS0067
        public event EventHandler<ContextMenuDisplayingEventArgs>? ContextMenuDisplaying;
#pragma warning restore CS0067
    }

    /// <summary>Compat stand-in for the grid group-panel field element.</summary>
    public class GroupFieldElement : RadElement
    {
        /// <summary>The field name shown by the element.</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>The display text of the element.</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Compat stand-in for Telerik's RadControl base (sites type variables as RadControl).</summary>
    public class RadControl : Majorsilence.Forms.Control, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }

    /// <summary>Compat stand-in for the drop-down calendar of a RadDateTimePicker.</summary>
    public class RadDateTimePickerCalendar : RadElement
    {
        /// <summary>Whether the time picker panel is shown.</summary>
        public bool ShowTimePicker { get; set; }
    }

    /// <summary>
    /// Compat stand-in for the grid header cell element. Derives from
    /// <see cref="Majorsilence.Forms.Telerik.GridViewCellElement"/> (Telerik parity) so formatting
    /// handlers that narrow a cell-formatting event's CellElement with
    /// <c>TypeOf e.CellElement Is GridHeaderCellElement</c> compile.
    /// </summary>
    public class GridHeaderCellElement : GridViewCellElement
    {
    }

    /// <summary>Compat stand-in for the grid data cell element.</summary>
    public class GridDataCellElement : RadElement
    {
    }

    /// <summary>Compat stand-in for the calendar table element of a date picker popup.</summary>
    public class CalendarTableElement : RadElement
    {
    }

    /// <summary>Compat stand-in for Telerik's filter operation context.</summary>
    public class FilterOperationContext
    {
        /// <summary>The field being filtered.</summary>
        public string FieldName { get; set; } = string.Empty;
    }

    /// <summary>Compat stand-in for the grid's paging panel element.</summary>
    public class PagingPanelElement : RadElement
    {
        /// <summary>First-page button element (stub).</summary>
        public CommandBarButton FirstButton { get; } = new CommandBarButton ();
        /// <summary>Previous-page button element (stub).</summary>
        public CommandBarButton PreviousButton { get; } = new CommandBarButton ();
        /// <summary>Fast-back button element (stub).</summary>
        public CommandBarButton FastBackButton { get; } = new CommandBarButton ();
        /// <summary>Fast-forward button element (stub).</summary>
        public CommandBarButton FastForwardButton { get; } = new CommandBarButton ();
        /// <summary>Next-page button element (stub).</summary>
        public CommandBarButton NextButton { get; } = new CommandBarButton ();
        /// <summary>Last-page button element (stub).</summary>
        public CommandBarButton LastButton { get; } = new CommandBarButton ();
    }

    /// <summary>Provides data for grid filter-popup creation. Mirrors Telerik's shape.</summary>
    public class FilterPopupRequiredEventArgs : EventArgs
    {
        /// <summary>The column the popup is for.</summary>
        public Majorsilence.Forms.DataGridViewColumn? Column { get; set; }

        /// <summary>The popup to show (assign to supply one).</summary>
        public object? FilterPopup { get; set; }
    }

    /// <summary>Provides cancellable data for RadPageView page changes. Mirrors Telerik's shape.</summary>
    public class RadPageViewCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>The page involved.</summary>
        public RadPageViewPage? Page { get; set; }
    }

    /// <summary>Provides data for dock-window events. Mirrors Telerik's shape.</summary>
    public class DockWindowEventArgs : EventArgs
    {
        /// <summary>The dock window involved.</summary>
        public object? DockWindow { get; set; }
    }
}

namespace Majorsilence.Forms.Telerik.Primitives
{
    /// <summary>Relative-qualification seam for Telerik's Primitives.FillPrimitive.</summary>
    public class FillPrimitive : Majorsilence.Forms.Telerik.RadElement
    {
        // BackColor is inherited from RadElement.

        /// <summary>The gradient style (stored for compat).</summary>
        public object? GradientStyle { get; set; }
    }

    /// <summary>Relative-qualification seam for Telerik's Primitives.BorderPrimitive.</summary>
    public class BorderPrimitive : Majorsilence.Forms.Telerik.RadElement
    {
        /// <summary>The border color.</summary>
        public new System.Drawing.Color ForeColor { get; set; }
    }
}

namespace Majorsilence.Forms.Telerik.UI
{
    /// <summary>Relative-qualification seam: legacy code written under Imports Telerik.WinControls
    /// references UI.RowFormattingEventArgs / UI.GridViewCellEventArgs.</summary>
    public class RowFormattingEventArgs : Majorsilence.Forms.Telerik.RowFormattingEventArgs { }

    /// <summary>Relative-qualification seam for UI.GridViewCellEventArgs.</summary>
    public class GridViewCellEventArgs : Majorsilence.Forms.Telerik.GridViewCellEventArgs { }
}

namespace Majorsilence.Forms.Telerik.Data
{
    /// <summary>
    /// Relative-qualification seam: legacy code written under Imports Telerik.WinControls(.UI)
    /// references Data.PositionChangedEventArgs; with Majorsilence.Forms.Telerik imported this
    /// nested namespace satisfies the same relative name.
    /// </summary>
    public class PositionChangedEventArgs : Majorsilence.Forms.Telerik.PositionChangedEventArgs
    {
        /// <summary>Initializes the args for a position.</summary>
        public PositionChangedEventArgs (int position) : base (position) { }
    }
}

namespace Telerik.Collections.Generic
{
    /// <summary>
    /// Compat stand-in for Telerik's Index collection, declared under Telerik's own namespace
    /// because call sites reference it fully qualified (an unqualified Index would collide with
    /// System.Index) and VB cannot alias open generic types.
    /// </summary>
    public class Index<T> : System.Collections.ObjectModel.Collection<T>
    {
    }
}
