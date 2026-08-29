using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // The types the WinForms surface still names but this layer did not declare
    // (docs/winforms-gap-plan.md).
    //
    // ListBindingHelper is the one with teeth: it is the static resolver that turns a data source and
    // a member name into the list and item properties a binding actually reads, and every binding
    // path here does that work inline today. Implementing it puts the rule in one place, and it is
    // real -- it walks an IListSource, unwraps an IEnumerable to its element type, and follows a
    // dotted data member through PropertyDescriptors.
    //
    // The selected-cell collections are snapshots, as they are upstream: WinForms documents them as
    // "a copy", so Clear and Insert throw rather than pretending an edit reaches the grid.

    /// <summary>Resolves a data source and member into the list and properties a binding reads.</summary>
    public static class ListBindingHelper
    {
        /// <summary>Returns the list behind the given data source.</summary>
        public static object? GetList (object? list)
            => list is IListSource source ? source.GetList () : list;

        /// <summary>Returns the list reached by following the given member from the data source.</summary>
        [RequiresUnreferencedCode ("The data member is followed by reflection, as it is upstream.")]
        public static object? GetList (object? dataSource, string? dataMember)
        {
            var list = GetList (dataSource);

            if (string.IsNullOrEmpty (dataMember))
                return list;

            // The member is read off the current item rather than off the list: "Orders" on a list of
            // customers means the current customer's orders, which is what a master/detail binding is.
            var current = list is IList { Count: > 0 } items ? items[0] : list;

            return current is null ? null : GetProperty (current, dataMember)?.GetValue (current);
        }

        /// <summary>Returns the type of the items in the given list.</summary>
        [RequiresUnreferencedCode ("The list's interfaces are inspected by reflection, as they are upstream.")]
        public static Type GetListItemType (object? list)
        {
            var resolved = GetList (list);

            if (resolved is null)
                return typeof (object);

            var type = resolved.GetType ();

            if (type.IsArray)
                return type.GetElementType () ?? typeof (object);

            // A typed enumerable names its element type; an untyped IList can only be sampled.
#pragma warning disable IL2075
            var enumerable = type.GetInterfaces ()
                .FirstOrDefault (i => i.IsGenericType && i.GetGenericTypeDefinition () == typeof (IEnumerable<>));

#pragma warning restore IL2075

            if (enumerable is not null)
                return enumerable.GetGenericArguments ()[0];

            return resolved is IList { Count: > 0 } items && items[0] is { } first
                ? first.GetType ()
                : typeof (object);
        }

        /// <inheritdoc cref="GetListItemType(object)"/>
        [RequiresUnreferencedCode ("The list's interfaces are inspected by reflection, as they are upstream.")]
        public static Type GetListItemType (object? dataSource, string? dataMember)
            => GetListItemType (GetList (dataSource, dataMember));

        /// <summary>Returns the properties of the items in the given list.</summary>
        [RequiresUnreferencedCode ("The item type's properties are discovered by reflection, as they are upstream.")]
        public static PropertyDescriptorCollection GetListItemProperties (object? list)
            => TypeDescriptor.GetProperties (GetListItemType (list));

        /// <inheritdoc cref="GetListItemProperties(object)"/>
        [RequiresUnreferencedCode ("The item type's properties are discovered by reflection, as they are upstream.")]
        public static PropertyDescriptorCollection GetListItemProperties (object? list, PropertyDescriptor[]? listAccessors)
        {
            if (listAccessors is not { Length: > 0 })
                return GetListItemProperties (list);

            // Each accessor steps one level down a master/detail chain, so the properties returned are
            // those of the innermost list.
            var type = listAccessors[^1].PropertyType;
            return TypeDescriptor.GetProperties (type);
        }

        /// <inheritdoc cref="GetListItemProperties(object)"/>
        [RequiresUnreferencedCode ("The item type's properties are discovered by reflection, as they are upstream.")]
        public static PropertyDescriptorCollection GetListItemProperties (object? dataSource, string? dataMember,
            PropertyDescriptor[]? listAccessors)
            => GetListItemProperties (GetList (dataSource, dataMember), listAccessors);

        /// <summary>Returns the name a bound list reports for itself.</summary>
        [RequiresUnreferencedCode ("The item type is discovered by reflection, as it is upstream.")]
        public static string GetListName (object? list, PropertyDescriptor[]? listAccessors)
        {
            if (list is ITypedList typed)
                return typed.GetListName (listAccessors);

            return listAccessors is { Length: > 0 }
                ? listAccessors[^1].Name
                : GetListItemType (list).Name;
        }

        [RequiresUnreferencedCode ("The member is looked up by reflection, as it is upstream.")]
        private static System.Reflection.PropertyInfo? GetProperty (object instance, string member)
        {
            // A data member may be dotted -- "Customer.Orders" -- so each segment is followed in turn.
            System.Reflection.PropertyInfo? property = null;
            var current = instance;

            foreach (var segment in member.Split ('.')) {
                if (current is null)
                    return null;

                property = current.GetType ().GetProperty (segment,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                if (property is null)
                    return null;

                current = property.GetValue (current);
            }

            return property;
        }
    }

    /// <summary>The colours and font an owner-drawn item was told to use.</summary>
    public class OwnerDrawPropertyBag : MarshalByRefObject
    {
        /// <summary>Gets or sets the background colour.</summary>
        public Color BackColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the foreground colour.</summary>
        public Color ForeColor { get; set; } = Color.Empty;

        /// <summary>Gets or sets the font.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }

        /// <summary>Returns whether nothing has been set.</summary>
        public bool IsEmpty () => BackColor.IsEmpty && ForeColor.IsEmpty && Font is null;

        /// <summary>Returns a copy of the given bag.</summary>
        public static OwnerDrawPropertyBag Copy (OwnerDrawPropertyBag value)
        {
            Guard.ThrowIfNull (value);

            return new OwnerDrawPropertyBag {
                BackColor = value.BackColor,
                ForeColor = value.ForeColor,
                Font = value.Font,
            };
        }
    }

    /// <summary>A snapshot of a <see cref="DataGridView"/>'s selection.</summary>
    /// <remarks>
    /// Upstream documents these as copies, and the mutating members exist only because the type
    /// implements <c>IList</c>. They throw here for the same reason they do there: an edit would look
    /// as though it had changed the grid's selection when it had changed a detached list.
    /// </remarks>
    public class DataGridViewSelectedCellCollection : BaseCollection
    {
        private readonly ArrayList cells = [];

        internal DataGridViewSelectedCellCollection (IEnumerable<DataGridViewCell> selection)
            => cells.AddRange (selection.ToArray ());

        /// <inheritdoc/>
        protected override ArrayList List => cells;

        /// <summary>Gets the cell at the given index.</summary>
        public DataGridViewCell this[int index] => (DataGridViewCell)cells[index]!;

        /// <summary>Returns whether the given cell is in this snapshot.</summary>
        public bool Contains (DataGridViewCell dataGridViewCell) => cells.Contains (dataGridViewCell);

        /// <summary>Copies this snapshot into an array.</summary>
        public void CopyTo (DataGridViewCell[] array, int index) => cells.CopyTo (array, index);

        /// <summary>Not supported: this collection is a snapshot.</summary>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Clear () => throw new NotSupportedException ();

        /// <summary>Not supported: this collection is a snapshot.</summary>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Insert (int index, DataGridViewCell dataGridViewCell) => throw new NotSupportedException ();
    }

    /// <inheritdoc cref="DataGridViewSelectedCellCollection"/>
    public class DataGridViewSelectedRowCollection : BaseCollection
    {
        private readonly ArrayList rows = [];

        internal DataGridViewSelectedRowCollection (IEnumerable<DataGridViewRow> selection)
            => rows.AddRange (selection.ToArray ());

        /// <inheritdoc/>
        protected override ArrayList List => rows;

        /// <summary>Gets the row at the given index.</summary>
        public DataGridViewRow this[int index] => (DataGridViewRow)rows[index]!;

        /// <summary>Returns whether the given row is in this snapshot.</summary>
        public bool Contains (DataGridViewRow dataGridViewRow) => rows.Contains (dataGridViewRow);

        /// <summary>Copies this snapshot into an array.</summary>
        public void CopyTo (DataGridViewRow[] array, int index) => rows.CopyTo (array, index);

        /// <summary>Not supported: this collection is a snapshot.</summary>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Clear () => throw new NotSupportedException ();

        /// <summary>Not supported: this collection is a snapshot.</summary>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Insert (int index, DataGridViewRow dataGridViewRow) => throw new NotSupportedException ();
    }

    /// <inheritdoc cref="DataGridViewSelectedCellCollection"/>
    public class DataGridViewSelectedColumnCollection : BaseCollection
    {
        private readonly ArrayList columns = [];

        internal DataGridViewSelectedColumnCollection (IEnumerable<DataGridViewColumn> selection)
            => columns.AddRange (selection.ToArray ());

        /// <inheritdoc/>
        protected override ArrayList List => columns;

        /// <summary>Gets the column at the given index.</summary>
        public DataGridViewColumn this[int index] => (DataGridViewColumn)columns[index]!;

        /// <summary>Returns whether the given column is in this snapshot.</summary>
        public bool Contains (DataGridViewColumn dataGridViewColumn) => columns.Contains (dataGridViewColumn);

        /// <summary>Copies this snapshot into an array.</summary>
        public void CopyTo (DataGridViewColumn[] array, int index) => columns.CopyTo (array, index);

        /// <summary>Not supported: this collection is a snapshot.</summary>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Clear () => throw new NotSupportedException ();

        /// <summary>Not supported: this collection is a snapshot.</summary>
        [EditorBrowsable (EditorBrowsableState.Never)]
        public void Insert (int index, DataGridViewColumn dataGridViewColumn) => throw new NotSupportedException ();
    }

    /// <summary>The cell above the row headers and left of the column headers.</summary>
    public class DataGridViewTopLeftHeaderCell : DataGridViewColumnHeaderCell
    {
        /// <inheritdoc/>
        public override string ToString () => "DataGridViewTopLeftHeaderCell";
    }

    /// <summary>A cell that draws a button.</summary>
    public class DataGridViewButtonCell : DataGridViewCell
    {
        /// <summary>Gets or sets whether the cell's value is used as the button's caption.</summary>
        public bool UseColumnTextForButtonValue { get; set; }

        /// <summary>Gets or sets the flat-style appearance of the button.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <inheritdoc/>
        public override Type ValueType => typeof (object);
    }

    /// <summary>A cell that draws a hyperlink.</summary>
    public partial class DataGridViewLinkCell : DataGridViewCell
    {
        /// <summary>Gets or sets the colour of the link.</summary>
        public Color LinkColor { get; set; } = SystemColors.HotTrack;

        /// <summary>Gets or sets the colour of a link being clicked.</summary>
        public Color ActiveLinkColor { get; set; } = SystemColors.HotTrack;

        /// <summary>Gets or sets the colour of a link that has been followed.</summary>
        public Color VisitedLinkColor { get; set; } = SystemColors.HotTrack;

        /// <summary>Gets or sets whether the link has been followed.</summary>
        public bool LinkVisited { get; set; }

        /// <summary>Gets or sets when the link is underlined.</summary>
        public LinkBehavior LinkBehavior { get; set; } = LinkBehavior.SystemDefault;

        /// <summary>Gets or sets whether the cell's value is used as the link text.</summary>
        public bool UseColumnTextForLinkValue { get; set; }

        /// <inheritdoc/>
        public override Type ValueType => typeof (object);
    }

    /// <summary>The text box a <see cref="DataGridView"/> hosts while a text cell is edited.</summary>
    public partial class DataGridViewTextBoxEditingControl : TextBox
    {
        /// <summary>Gets or sets the grid this control is editing in.</summary>
        public DataGridView? EditingControlDataGridView { get; set; }

        /// <summary>Gets or sets the row being edited.</summary>
        public int EditingControlRowIndex { get; set; } = -1;

        /// <summary>Gets or sets whether the user has changed the value.</summary>
        public bool EditingControlValueChanged { get; set; }

        /// <summary>Gets or sets the value being edited, in its formatted form.</summary>
        public object? EditingControlFormattedValue {
            get => Text;
            set => Text = value?.ToString () ?? string.Empty;
        }

        /// <summary>Returns the value being edited.</summary>
        public object? GetEditingControlFormattedValue (DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

        /// <summary>Prepares the control for editing.</summary>
        public void PrepareEditingControlForEdit (bool selectAll)
        {
            if (selectAll)
                SelectAll ();
        }
    }

    /// <summary>The combo box a <see cref="DataGridView"/> hosts while a combo cell is edited.</summary>
    public partial class DataGridViewComboBoxEditingControl : ComboBox
    {
        /// <summary>Gets or sets the grid this control is editing in.</summary>
        public DataGridView? EditingControlDataGridView { get; set; }

        /// <summary>Gets or sets the row being edited.</summary>
        public int EditingControlRowIndex { get; set; } = -1;

        /// <summary>Gets or sets whether the user has changed the value.</summary>
        public bool EditingControlValueChanged { get; set; }

        /// <summary>Gets or sets the value being edited, in its formatted form.</summary>
        public object? EditingControlFormattedValue {
            get => Text;
            set => Text = value?.ToString () ?? string.Empty;
        }

        /// <summary>Returns the value being edited.</summary>
        public object? GetEditingControlFormattedValue (DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

        /// <summary>Prepares the control for editing.</summary>
        public void PrepareEditingControlForEdit (bool selectAll) { }
    }

    /// <summary>One of a <see cref="SplitContainer"/>'s two panels.</summary>
    /// <remarks>WinForms exposes the panels as this type so a designer can restrict what may be set on
    /// them — Dock, Anchor and the rest are hidden. The restriction is a design-time concern, so this
    /// is a Panel that names itself after its container.</remarks>
    public class SplitterPanel : Panel
    {
        /// <summary>Initializes a new instance of the <see cref="SplitterPanel"/> class.</summary>
        public SplitterPanel (SplitContainer owner) => Owner = owner;

        /// <summary>Gets the container this panel belongs to.</summary>
        public SplitContainer Owner { get; }

    }

    /// <summary>The panel a <see cref="ToolStripContainer"/> hosts its content in.</summary>
    public class ToolStripContentPanel : Panel
    {
        /// <summary>Gets or sets the renderer used to paint the panel.</summary>
        public ToolStripRenderer? Renderer {
            get => renderer;
            set {
                if (ReferenceEquals (renderer, value))
                    return;

                renderer = value;
                RendererChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        private ToolStripRenderer? renderer;

        /// <summary>Gets or sets whether the panel uses its own renderer or the manager's.</summary>
        public ToolStripRenderMode RenderMode { get; set; } = ToolStripRenderMode.ManagerRenderMode;

        /// <summary>Raised when <see cref="Renderer"/> changes.</summary>
        public event EventHandler? RendererChanged;

        /// <summary>Raised when the panel is loaded.</summary>
#pragma warning disable CS0067
        public event EventHandler? Load;
#pragma warning restore CS0067
    }

    /// <summary>The drop-down that holds a strip's overflowed items.</summary>
    public class ToolStripOverflow : ToolStripDropDown
    {
        /// <summary>Initializes a new instance of the <see cref="ToolStripOverflow"/> class.</summary>
        public ToolStripOverflow (ToolStripItem parentItem) => OwnerItem = parentItem;
    }

    /// <summary>A drop-down laid out as a menu, with a margin for check marks and images.</summary>
    public class ToolStripDropDownMenu : ToolStripDropDown
    {
        /// <summary>Gets or sets whether space is reserved for check marks.</summary>
        public bool ShowCheckMargin { get; set; }

        /// <summary>Gets or sets whether space is reserved for images.</summary>
        public bool ShowImageMargin { get; set; } = true;

        /// <summary>Gets or sets how the items are laid out.</summary>
        public new ToolStripLayoutStyle LayoutStyle { get; set; } = ToolStripLayoutStyle.VerticalStackWithOverflow;
    }

    /// <summary>Exposes a <see cref="ToolStripDropDownItem"/> to accessibility clients.</summary>
    public class ToolStripDropDownItemAccessibleObject : AccessibleObject
    {
        private readonly ToolStripDropDownItem item;

        /// <summary>Initializes a new instance of the <see cref="ToolStripDropDownItemAccessibleObject"/> class.</summary>
        public ToolStripDropDownItemAccessibleObject (ToolStripDropDownItem item) => this.item = item;

        /// <summary>Gets the name reported to assistive technology.</summary>
        public override string? Name => item.AccessibleName ?? item.Text;

        /// <summary>Gets the role reported to assistive technology.</summary>
        public override AccessibleRole Role => AccessibleRole.MenuItem;

        /// <summary>Gets how many items the drop-down holds.</summary>
        public override int GetChildCount () => item.HasDropDownItems ? item.DropDownItems.Count : 0;
    }

    /// <summary>The dialog shown for an unhandled exception on the UI thread.</summary>
    public class ThreadExceptionDialog : Form
    {
        /// <summary>Initializes a new instance of the <see cref="ThreadExceptionDialog"/> class.</summary>
        public ThreadExceptionDialog (Exception t)
        {
            Guard.ThrowIfNull (t);

            Exception = t;
            Text = "Unhandled exception";
        }

        /// <summary>Gets the exception the dialog describes.</summary>
        public Exception Exception { get; }
    }

    /// <summary>Supplies the colours a professional-styled ToolStrip renderer paints with.</summary>
    public class ProfessionalColorTable
    {
        /// <summary>Gets the background of a checked item.</summary>
        public virtual Color ButtonCheckedHighlight => SystemColors.Highlight;

        /// <summary>Gets the background of an item under the pointer.</summary>
        public virtual Color ButtonSelectedHighlight => SystemColors.ControlLight;

        /// <summary>Gets the background of a pressed item.</summary>
        public virtual Color ButtonPressedHighlight => SystemColors.ControlDark;

        /// <summary>Gets the background of a menu strip.</summary>
        public virtual Color MenuStripGradientBegin => SystemColors.Control;

        /// <summary>Gets the far end of a menu strip's gradient.</summary>
        public virtual Color MenuStripGradientEnd => SystemColors.Control;

        /// <summary>Gets the border drawn around a menu.</summary>
        public virtual Color MenuBorder => SystemColors.ControlDark;

        /// <summary>Gets the background of a tool strip.</summary>
        public virtual Color ToolStripGradientBegin => SystemColors.Control;

        /// <summary>Gets the middle of a tool strip's gradient.</summary>
        public virtual Color ToolStripGradientMiddle => SystemColors.Control;

        /// <summary>Gets the far end of a tool strip's gradient.</summary>
        public virtual Color ToolStripGradientEnd => SystemColors.Control;

        /// <summary>Gets the border drawn around a tool strip.</summary>
        public virtual Color ToolStripBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour of a separator's dark line.</summary>
        public virtual Color SeparatorDark => SystemColors.ControlDark;

        /// <summary>Gets the colour of a separator's light line.</summary>
        public virtual Color SeparatorLight => SystemColors.ControlLightLight;

        // The remaining 45. Every one is answered from a SystemColors entry chosen by category --
        // borders and pressed states from ControlDark, checked and selected from ControlLight, the
        // rest from Control -- so the table stays coherent when the theme changes instead of being
        // 45 independent guesses. A caller that wants the Office look overrides the ones it cares
        // about, which is what the type is virtual for.

        /// <summary>Gets the colour used for the button checked gradient begin.</summary>
        public virtual Color ButtonCheckedGradientBegin => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the button checked gradient end.</summary>
        public virtual Color ButtonCheckedGradientEnd => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the button checked gradient middle.</summary>
        public virtual Color ButtonCheckedGradientMiddle => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the button checked highlight border.</summary>
        public virtual Color ButtonCheckedHighlightBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button pressed border.</summary>
        public virtual Color ButtonPressedBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button pressed gradient begin.</summary>
        public virtual Color ButtonPressedGradientBegin => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button pressed gradient end.</summary>
        public virtual Color ButtonPressedGradientEnd => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button pressed gradient middle.</summary>
        public virtual Color ButtonPressedGradientMiddle => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button pressed highlight border.</summary>
        public virtual Color ButtonPressedHighlightBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button selected border.</summary>
        public virtual Color ButtonSelectedBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the button selected gradient begin.</summary>
        public virtual Color ButtonSelectedGradientBegin => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the button selected gradient end.</summary>
        public virtual Color ButtonSelectedGradientEnd => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the button selected gradient middle.</summary>
        public virtual Color ButtonSelectedGradientMiddle => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the button selected highlight border.</summary>
        public virtual Color ButtonSelectedHighlightBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the check background.</summary>
        public virtual Color CheckBackground => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the check pressed background.</summary>
        public virtual Color CheckPressedBackground => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the check selected background.</summary>
        public virtual Color CheckSelectedBackground => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the grip dark.</summary>
        public virtual Color GripDark => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the grip light.</summary>
        public virtual Color GripLight => SystemColors.ControlLightLight;

        /// <summary>Gets the colour used for the image margin gradient begin.</summary>
        public virtual Color ImageMarginGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the image margin gradient end.</summary>
        public virtual Color ImageMarginGradientEnd => SystemColors.Control;

        /// <summary>Gets the colour used for the image margin gradient middle.</summary>
        public virtual Color ImageMarginGradientMiddle => SystemColors.Control;

        /// <summary>Gets the colour used for the image margin revealed gradient begin.</summary>
        public virtual Color ImageMarginRevealedGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the image margin revealed gradient end.</summary>
        public virtual Color ImageMarginRevealedGradientEnd => SystemColors.Control;

        /// <summary>Gets the colour used for the image margin revealed gradient middle.</summary>
        public virtual Color ImageMarginRevealedGradientMiddle => SystemColors.Control;

        /// <summary>Gets the colour used for the menu item border.</summary>
        public virtual Color MenuItemBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the menu item pressed gradient begin.</summary>
        public virtual Color MenuItemPressedGradientBegin => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the menu item pressed gradient end.</summary>
        public virtual Color MenuItemPressedGradientEnd => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the menu item pressed gradient middle.</summary>
        public virtual Color MenuItemPressedGradientMiddle => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the menu item selected.</summary>
        public virtual Color MenuItemSelected => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the menu item selected gradient begin.</summary>
        public virtual Color MenuItemSelectedGradientBegin => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the menu item selected gradient end.</summary>
        public virtual Color MenuItemSelectedGradientEnd => SystemColors.ControlLight;

        /// <summary>Gets the colour used for the overflow button gradient begin.</summary>
        public virtual Color OverflowButtonGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the overflow button gradient end.</summary>
        public virtual Color OverflowButtonGradientEnd => SystemColors.Control;

        /// <summary>Gets the colour used for the overflow button gradient middle.</summary>
        public virtual Color OverflowButtonGradientMiddle => SystemColors.Control;

        /// <summary>Gets the colour used for the rafting container gradient begin.</summary>
        public virtual Color RaftingContainerGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the rafting container gradient end.</summary>
        public virtual Color RaftingContainerGradientEnd => SystemColors.Control;

        /// <summary>Gets the colour used for the status strip border.</summary>
        public virtual Color StatusStripBorder => SystemColors.ControlDark;

        /// <summary>Gets the colour used for the status strip gradient begin.</summary>
        public virtual Color StatusStripGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the status strip gradient end.</summary>
        public virtual Color StatusStripGradientEnd => SystemColors.Control;

        /// <summary>Gets the colour used for the tool strip content panel gradient begin.</summary>
        public virtual Color ToolStripContentPanelGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the tool strip content panel gradient end.</summary>
        public virtual Color ToolStripContentPanelGradientEnd => SystemColors.Control;

        /// <summary>Gets the colour used for the tool strip drop down background.</summary>
        public virtual Color ToolStripDropDownBackground => SystemColors.Window;

        /// <summary>Gets the colour used for the tool strip panel gradient begin.</summary>
        public virtual Color ToolStripPanelGradientBegin => SystemColors.Control;

        /// <summary>Gets the colour used for the tool strip panel gradient end.</summary>
        public virtual Color ToolStripPanelGradientEnd => SystemColors.Control;

        /// <summary>Gets or sets whether the table follows the system colours.</summary>
        public bool UseSystemColors { get; set; } = true;
    }

    /// <summary>The shared <see cref="ProfessionalColorTable"/>.</summary>
    public static class ProfessionalColors
    {
        private static readonly ProfessionalColorTable Table = new ();

        /// <inheritdoc cref="ProfessionalColorTable.ButtonCheckedHighlight"/>
        public static Color ButtonCheckedHighlight => Table.ButtonCheckedHighlight;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonSelectedHighlight"/>
        public static Color ButtonSelectedHighlight => Table.ButtonSelectedHighlight;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonPressedHighlight"/>
        public static Color ButtonPressedHighlight => Table.ButtonPressedHighlight;

        /// <inheritdoc cref="ProfessionalColorTable.MenuStripGradientBegin"/>
        public static Color MenuStripGradientBegin => Table.MenuStripGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.MenuStripGradientEnd"/>
        public static Color MenuStripGradientEnd => Table.MenuStripGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.MenuBorder"/>
        public static Color MenuBorder => Table.MenuBorder;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripGradientBegin"/>
        public static Color ToolStripGradientBegin => Table.ToolStripGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripGradientMiddle"/>
        public static Color ToolStripGradientMiddle => Table.ToolStripGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripGradientEnd"/>
        public static Color ToolStripGradientEnd => Table.ToolStripGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripBorder"/>
        public static Color ToolStripBorder => Table.ToolStripBorder;

        /// <inheritdoc cref="ProfessionalColorTable.SeparatorDark"/>
        public static Color SeparatorDark => Table.SeparatorDark;

        /// <inheritdoc cref="ProfessionalColorTable.SeparatorLight"/>
        public static Color SeparatorLight => Table.SeparatorLight;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonCheckedGradientBegin"/>
        public static Color ButtonCheckedGradientBegin => Table.ButtonCheckedGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonCheckedGradientEnd"/>
        public static Color ButtonCheckedGradientEnd => Table.ButtonCheckedGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonCheckedGradientMiddle"/>
        public static Color ButtonCheckedGradientMiddle => Table.ButtonCheckedGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonCheckedHighlightBorder"/>
        public static Color ButtonCheckedHighlightBorder => Table.ButtonCheckedHighlightBorder;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonPressedBorder"/>
        public static Color ButtonPressedBorder => Table.ButtonPressedBorder;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonPressedGradientBegin"/>
        public static Color ButtonPressedGradientBegin => Table.ButtonPressedGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonPressedGradientEnd"/>
        public static Color ButtonPressedGradientEnd => Table.ButtonPressedGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonPressedGradientMiddle"/>
        public static Color ButtonPressedGradientMiddle => Table.ButtonPressedGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonPressedHighlightBorder"/>
        public static Color ButtonPressedHighlightBorder => Table.ButtonPressedHighlightBorder;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonSelectedBorder"/>
        public static Color ButtonSelectedBorder => Table.ButtonSelectedBorder;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonSelectedGradientBegin"/>
        public static Color ButtonSelectedGradientBegin => Table.ButtonSelectedGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonSelectedGradientEnd"/>
        public static Color ButtonSelectedGradientEnd => Table.ButtonSelectedGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonSelectedGradientMiddle"/>
        public static Color ButtonSelectedGradientMiddle => Table.ButtonSelectedGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.ButtonSelectedHighlightBorder"/>
        public static Color ButtonSelectedHighlightBorder => Table.ButtonSelectedHighlightBorder;

        /// <inheritdoc cref="ProfessionalColorTable.CheckBackground"/>
        public static Color CheckBackground => Table.CheckBackground;

        /// <inheritdoc cref="ProfessionalColorTable.CheckPressedBackground"/>
        public static Color CheckPressedBackground => Table.CheckPressedBackground;

        /// <inheritdoc cref="ProfessionalColorTable.CheckSelectedBackground"/>
        public static Color CheckSelectedBackground => Table.CheckSelectedBackground;

        /// <inheritdoc cref="ProfessionalColorTable.GripDark"/>
        public static Color GripDark => Table.GripDark;

        /// <inheritdoc cref="ProfessionalColorTable.GripLight"/>
        public static Color GripLight => Table.GripLight;

        /// <inheritdoc cref="ProfessionalColorTable.ImageMarginGradientBegin"/>
        public static Color ImageMarginGradientBegin => Table.ImageMarginGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ImageMarginGradientEnd"/>
        public static Color ImageMarginGradientEnd => Table.ImageMarginGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ImageMarginGradientMiddle"/>
        public static Color ImageMarginGradientMiddle => Table.ImageMarginGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.ImageMarginRevealedGradientBegin"/>
        public static Color ImageMarginRevealedGradientBegin => Table.ImageMarginRevealedGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ImageMarginRevealedGradientEnd"/>
        public static Color ImageMarginRevealedGradientEnd => Table.ImageMarginRevealedGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ImageMarginRevealedGradientMiddle"/>
        public static Color ImageMarginRevealedGradientMiddle => Table.ImageMarginRevealedGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemBorder"/>
        public static Color MenuItemBorder => Table.MenuItemBorder;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemPressedGradientBegin"/>
        public static Color MenuItemPressedGradientBegin => Table.MenuItemPressedGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemPressedGradientEnd"/>
        public static Color MenuItemPressedGradientEnd => Table.MenuItemPressedGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemPressedGradientMiddle"/>
        public static Color MenuItemPressedGradientMiddle => Table.MenuItemPressedGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemSelected"/>
        public static Color MenuItemSelected => Table.MenuItemSelected;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemSelectedGradientBegin"/>
        public static Color MenuItemSelectedGradientBegin => Table.MenuItemSelectedGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.MenuItemSelectedGradientEnd"/>
        public static Color MenuItemSelectedGradientEnd => Table.MenuItemSelectedGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.OverflowButtonGradientBegin"/>
        public static Color OverflowButtonGradientBegin => Table.OverflowButtonGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.OverflowButtonGradientEnd"/>
        public static Color OverflowButtonGradientEnd => Table.OverflowButtonGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.OverflowButtonGradientMiddle"/>
        public static Color OverflowButtonGradientMiddle => Table.OverflowButtonGradientMiddle;

        /// <inheritdoc cref="ProfessionalColorTable.RaftingContainerGradientBegin"/>
        public static Color RaftingContainerGradientBegin => Table.RaftingContainerGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.RaftingContainerGradientEnd"/>
        public static Color RaftingContainerGradientEnd => Table.RaftingContainerGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.StatusStripBorder"/>
        public static Color StatusStripBorder => Table.StatusStripBorder;

        /// <inheritdoc cref="ProfessionalColorTable.StatusStripGradientBegin"/>
        public static Color StatusStripGradientBegin => Table.StatusStripGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.StatusStripGradientEnd"/>
        public static Color StatusStripGradientEnd => Table.StatusStripGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripContentPanelGradientBegin"/>
        public static Color ToolStripContentPanelGradientBegin => Table.ToolStripContentPanelGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripContentPanelGradientEnd"/>
        public static Color ToolStripContentPanelGradientEnd => Table.ToolStripContentPanelGradientEnd;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripDropDownBackground"/>
        public static Color ToolStripDropDownBackground => Table.ToolStripDropDownBackground;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripPanelGradientBegin"/>
        public static Color ToolStripPanelGradientBegin => Table.ToolStripPanelGradientBegin;

        /// <inheritdoc cref="ProfessionalColorTable.ToolStripPanelGradientEnd"/>
        public static Color ToolStripPanelGradientEnd => Table.ToolStripPanelGradientEnd;
    }

    /// <summary>A binding manager for a single object rather than a list.</summary>
    public partial class PropertyManager : BindingManagerBase
    {
        /// <summary>Initializes a new instance of the <see cref="PropertyManager"/> class.</summary>
        public PropertyManager () : base (null) { }

        /// <summary>Gets or sets the object being managed.</summary>
        /// <remarks>Also what <see cref="BindingManagerBase.Current"/> reports for a property manager --
        /// see the override in BindingRuntime.cs. It used to report through this property alone, because
        /// Current was not virtual and a second, conflicting answer would have been worse.</remarks>
        public object? DataSource { get; set; }
    }

    /// <summary>A component that can be data-bound but is not a control.</summary>
    public partial class BindableComponent : Component, IBindableComponent
    {
        private BindingContext? binding_context;
        private ControlBindingsCollection? data_bindings;

        /// <summary>Gets or sets the binding context for this component.</summary>
        public BindingContext? BindingContext {
            get => binding_context ??= new BindingContext ();
            set => binding_context = value;
        }

        /// <summary>Gets the data bindings for this component.</summary>
        public ControlBindingsCollection DataBindings => data_bindings ??= new ControlBindingsCollection (null!);
    }

    /// <summary>Marks a control as dockable by the designer.</summary>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed partial class DockingAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="DockingAttribute"/> class.</summary>
        public DockingAttribute () => DockingBehavior = DockingBehavior.Never;

        /// <inheritdoc cref="DockingAttribute()"/>
        public DockingAttribute (DockingBehavior dockingBehavior) => DockingBehavior = dockingBehavior;

        /// <summary>Gets how the designer docks the control.</summary>
        public DockingBehavior DockingBehavior { get; }
    }

    /// <summary>Names the image list property a control's image index refers to.</summary>
    [AttributeUsage (AttributeTargets.Property)]
    public sealed class RelatedImageListAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="RelatedImageListAttribute"/> class.</summary>
        public RelatedImageListAttribute (string? relatedImageList) => RelatedImageList = relatedImageList;

        /// <summary>Gets the name of the related image list property.</summary>
        public string? RelatedImageList { get; }
    }

    /// <summary>Marks a DataGridView column type as visible in the designer.</summary>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed partial class DataGridViewColumnDesignTimeVisibleAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="DataGridViewColumnDesignTimeVisibleAttribute"/> class.</summary>
        public DataGridViewColumnDesignTimeVisibleAttribute () => Visible = true;

        /// <inheritdoc cref="DataGridViewColumnDesignTimeVisibleAttribute()"/>
        public DataGridViewColumnDesignTimeVisibleAttribute (bool visible) => Visible = visible;

        /// <summary>Gets whether the column type is offered in the designer.</summary>
        public bool Visible { get; }

        /// <summary>The default, which is visible.</summary>
        public static readonly DataGridViewColumnDesignTimeVisibleAttribute Default = new ();
    }

    /// <summary>Creates the table styles for a <see cref="DataGrid"/>'s data source.</summary>
    public static class GridTablesFactory
    {
        /// <summary>Returns a table style per table in the given source.</summary>
        /// <remarks>Empty: building styles from a source means walking a DataSet's tables and
        /// relations, which this layer has no model for. A caller gets nothing rather than a style
        /// describing columns that do not exist.</remarks>
        public static DataGridTableStyle[] CreateGridTables (DataGridTableStyle gridTable, object dataSource,
            string dataMember, BindingContext listManager) => [];
    }

    /// <summary>Lets a component editor page tell its site what it did.</summary>
    public interface IComponentEditorPageSite
    {
        /// <summary>Gets the control the page draws into.</summary>
        Control GetControl ();

        /// <summary>Tells the site the page's contents changed.</summary>
        void SetDirty ();
    }

    /// <summary>Lets a grid tell a column style that editing began or ended.</summary>
    public interface IDataGridColumnStyleEditingNotificationService
    {
        /// <summary>Called when the column's cell begins editing.</summary>
        void ColumnStartedEditing (Control editingControl);
    }

    /// <summary>The editing operations a <see cref="DataGrid"/> offers a column style.</summary>
    public interface IDataGridEditingService
    {
        /// <summary>Begins editing the given cell.</summary>
        bool BeginEdit (DataGridColumnStyle gridColumn, int rowNumber);

        /// <summary>Ends editing the given cell.</summary>
        bool EndEdit (DataGridColumnStyle gridColumn, int rowNumber, bool shouldAbort);
    }

    /// <summary>Reports which version of a feature is present.</summary>
    public interface IFeatureSupport
    {
        /// <summary>Returns the version of the given feature, or null when it is absent.</summary>
        Version? GetVersionPresent (object feature);

        /// <summary>Returns whether the given feature is present.</summary>
        bool IsPresent (object feature);

        /// <inheritdoc cref="IsPresent(object)"/>
        bool IsPresent (object feature, Version minimumVersion);
    }

    /// <summary>The synchronization context a Majorsilence.Forms UI thread installs.</summary>
    /// <remarks>Posting and sending go through the active backend, which is what marshals to the UI
    /// thread here -- so an <c>await</c> in an event handler resumes on the UI thread exactly as it
    /// does under WinForms.</remarks>
    public class WindowsFormsSynchronizationContext : System.Threading.SynchronizationContext
    {
        /// <summary>Initializes a new instance of the <see cref="WindowsFormsSynchronizationContext"/> class.</summary>
        public WindowsFormsSynchronizationContext () { }

        /// <summary>Gets or sets whether the context is installed automatically on a UI thread.</summary>
        public static bool AutoInstall { get; set; } = true;

        /// <inheritdoc/>
        public override void Post (System.Threading.SendOrPostCallback d, object? state)
        {
            Guard.ThrowIfNull (d);
            Backends.Platform.Backend.Post (() => d (state));
        }

        /// <inheritdoc/>
        public override void Send (System.Threading.SendOrPostCallback d, object? state)
        {
            Guard.ThrowIfNull (d);
            Backends.Platform.Backend.Invoke (() => d (state));
        }

        /// <inheritdoc/>
        public override System.Threading.SynchronizationContext CreateCopy () => new WindowsFormsSynchronizationContext ();

        /// <summary>Removes the context installed on the current thread.</summary>
        public static void Uninstall () => SetSynchronizationContext (null);

        /// <summary>Releases the resources held by this context.</summary>
        /// <remarks>Nothing to release: posting goes through the backend, which owns the message
        /// loop and outlives any context wrapping it.</remarks>
        public void Dispose () { }
    }

    /// <summary>Extension methods for reading typed values out of a data object.</summary>
    public static class DataObjectExtensions
    {
        /// <summary>Returns the stored value when it is of the requested type.</summary>
        public static bool TryGetData<T> (this IDataObject dataObject, out T? data)
        {
            Guard.ThrowIfNull (dataObject);

            if (dataObject.GetData (typeof (T).FullName ?? typeof (T).Name) is T stored) {
                data = stored;
                return true;
            }

            data = default;
            return false;
        }

        /// <inheritdoc cref="TryGetData{T}(IDataObject,out T)"/>
        public static bool TryGetData<T> (this IDataObject dataObject, string format, out T? data)
        {
            Guard.ThrowIfNull (dataObject);

            if (dataObject.GetData (format) is T stored) {
                data = stored;
                return true;
            }

            data = default;
            return false;
        }

        /// <inheritdoc cref="TryGetData{T}(IDataObject,out T)"/>
        public static bool TryGetData<T> (this IDataObject dataObject, string format, bool autoConvert, out T? data)
            => dataObject.TryGetData (format, out data);

        /// <inheritdoc cref="TryGetData{T}(IDataObject,out T)"/>
        /// <remarks>The resolver maps a stored type name to the type to deserialise as. Nothing here
        /// serialises by type name -- values are stored as objects -- so it is never consulted.</remarks>
        public static bool TryGetData<T> (this IDataObject dataObject, string format,
            Func<System.Reflection.Metadata.TypeName, Type> resolver, bool autoConvert, out T? data)
            => dataObject.TryGetData (format, out data);
    }
}

namespace Majorsilence.Forms.Layout
{
    /// <summary>The read-only collection the layout engine walks.</summary>
    public class ArrangedElementCollection : System.Collections.IList
    {
        private readonly System.Collections.ArrayList items = [];

        /// <summary>Gets the number of elements.</summary>
        public virtual int Count => items.Count;

        /// <summary>Gets whether the collection is read-only.</summary>
        public virtual bool IsReadOnly => false;

        /// <summary>Gets whether the collection is a fixed size.</summary>
        public virtual bool IsFixedSize => false;

        /// <summary>Gets whether access is synchronised.</summary>
        public bool IsSynchronized => false;

        /// <summary>Gets the object used to synchronise access.</summary>
        public object SyncRoot => this;

        /// <summary>Gets or sets the element at the given index.</summary>
        public object? this[int index] {
            get => items[index];
            set => items[index] = value;
        }

        /// <summary>Adds an element.</summary>
        public int Add (object? value) => items.Add (value);

        /// <summary>Removes every element.</summary>
        public virtual void Clear () => items.Clear ();

        /// <summary>Returns whether the given element is present.</summary>
        public bool Contains (object? value) => items.Contains (value);

        /// <summary>Copies the collection into an array.</summary>
        public virtual void CopyTo (Array array, int index) => items.CopyTo (array, index);

        /// <summary>Returns the index of the given element, or -1.</summary>
        public int IndexOf (object? value) => items.IndexOf (value);

        /// <summary>Inserts an element at the given index.</summary>
        public void Insert (int index, object? value) => items.Insert (index, value);

        /// <summary>Removes the given element.</summary>
        public void Remove (object? value) => items.Remove (value);

        /// <summary>Removes the element at the given index.</summary>
        public void RemoveAt (int index) => items.RemoveAt (index);

        /// <inheritdoc/>
        public virtual System.Collections.IEnumerator GetEnumerator () => items.GetEnumerator ();
    }
}
