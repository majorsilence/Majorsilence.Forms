using System;
using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms
{
    // The .NET 1.x DataGrid family (docs/winforms-gap-plan.md) -- DataGrid, DataGridTableStyle,
    // DataGridColumnStyle and DataGridBoolColumn.
    //
    // These controls were superseded by DataGridView in .NET 2.0 and survive only in code old enough
    // to predate it, which is exactly the code most likely to be migrated. The surface is dominated
    // by two mechanical patterns -- a *Changed event for nearly every property, and a Reset* method
    // for every colour and font -- and both are implemented properly here rather than declared:
    // the setters raise, and the resetters restore the documented default.
    //
    // DataGrid itself hosts a DataGridView (see DataGrid.cs), so the members that describe real state
    // read through to it: VisibleRowCount, FirstVisibleColumn, GetCurrentCellBounds, BeginEdit and
    // EndEdit. The hierarchical-navigation members are the exception -- NavigateTo, Expand, Collapse
    // and IsExpanded exist for the DataSet parent/child relations this layer has no equivalent of,
    // and each says so.

    public partial class DataGrid
    {
        private Color link_hover_color = SystemColors.HotTrack;
        private bool column_headers_visible = true;
        private bool row_headers_visible = true;
        private bool parent_rows_visible = true;
        private bool allow_navigation = true;
        private int row_header_width = 35;
        private int preferred_row_height = 16;
        private DataGridParentRowsLabelStyle parent_rows_label_style = DataGridParentRowsLabelStyle.Both;

        /// <summary>Gets or sets whether the user can navigate to a child table.</summary>
        public bool AllowNavigation {
            get => allow_navigation;
            set => Set (ref allow_navigation, value, () => AllowNavigationChanged);
        }

        /// <summary>Gets or sets whether the column headers are shown.</summary>
        public bool ColumnHeadersVisible {
            get => column_headers_visible;
            set {
                column_headers_visible = value;
                Grid.ColumnHeadersVisible = value;
            }
        }

        /// <summary>Gets or sets whether the row headers are shown.</summary>
        public bool RowHeadersVisible {
            get => row_headers_visible;
            set {
                row_headers_visible = value;
                Grid.RowHeadersVisible = value;
            }
        }

        /// <summary>Gets or sets the width of the row headers.</summary>
        public int RowHeaderWidth {
            get => row_header_width;
            set {
                row_header_width = value;
                Grid.RowHeadersWidth = value;
            }
        }

        /// <summary>Gets or sets the height rows prefer.</summary>
        public int PreferredRowHeight {
            get => preferred_row_height;
            set => preferred_row_height = value;
        }

        /// <summary>Gets or sets the font used for the caption.</summary>
        public Majorsilence.Forms.Drawing.Font? CaptionFont { get; set; }

        /// <summary>Gets or sets the colour of a link under the pointer.</summary>
        public Color LinkHoverColor {
            get => link_hover_color;
            set => link_hover_color = value;
        }

        /// <summary>Gets or sets whether the parent rows of a child table are shown.</summary>
        public bool ParentRowsVisible {
            get => parent_rows_visible;
            set => Set (ref parent_rows_visible, value, () => ParentRowsVisibleChanged);
        }

        /// <summary>Gets or sets what the parent rows show.</summary>
        public DataGridParentRowsLabelStyle ParentRowsLabelStyle {
            get => parent_rows_label_style;
            set => Set (ref parent_rows_label_style, value, () => ParentRowsLabelStyleChanged);
        }

        /// <summary>Gets the index of the first column scrolled into view.</summary>
        public int FirstVisibleColumn {
            get {
                for (var i = 0; i < Grid.Columns.Count; i++)
                    if (Grid.Columns[i].Visible)
                        return i;

                return -1;
            }
        }

        /// <summary>Gets how many columns are shown.</summary>
        public int VisibleColumnCount => Grid.Columns.Count (c => c.Visible);

        /// <summary>Gets how many rows are shown.</summary>
        public int VisibleRowCount => Grid.Rows.GetRowCount (DataGridViewElementStates.Visible);

        /// <summary>Returns the bounds of the cell the user is on.</summary>
        public Rectangle GetCurrentCellBounds ()
            => Grid.CurrentCell is { } cell
                ? Grid.GetCellDisplayRectangle (cell.ColumnIndex, cell.RowIndex, cutOverflow: false)
                : Rectangle.Empty;

        /// <summary>Puts the given cell into edit mode.</summary>
        public bool BeginEdit (DataGridColumnStyle gridColumn, int rowNumber)
        {
            if (rowNumber < 0 || rowNumber >= Grid.Rows.Count)
                return false;

            return Grid.BeginEdit (selectAll: true);
        }

        /// <summary>Commits or abandons the edit on the given cell.</summary>
        public bool EndEdit (DataGridColumnStyle gridColumn, int rowNumber, bool shouldAbort)
        {
            if (shouldAbort) {
                Grid.CancelEdit ();
                return true;
            }

            return Grid.EndEdit ();
        }

        /// <summary>Binds the grid to a data source and member together.</summary>
        public void SetDataBinding (object? dataSource, string? dataMember)
        {
            DataSource = dataSource;
            DataMember = dataMember ?? string.Empty;
        }

        // The hierarchical-navigation surface. A .NET 1.x DataGrid could drill from a parent DataTable
        // into a child through a DataRelation and draw an expander per row; this layer has no relation
        // model and no expander, so these do nothing and IsExpanded says so rather than claiming a row
        // is collapsed when there is nothing to collapse.

        /// <summary>Navigates to a child table through the named relation. Not supported here.</summary>
        public void NavigateTo (int rowNumber, string relationName) { }

        /// <summary>Navigates back to the parent table. Not supported here.</summary>
        public void NavigateBack () { }

        /// <summary>Expands a row's child rows. Not supported here; see <see cref="NavigateTo"/>.</summary>
        public void Expand (int row) { }

        /// <summary>Collapses a row's child rows. Not supported here; see <see cref="NavigateTo"/>.</summary>
        public void Collapse (int row) { }

        /// <summary>Returns whether a row's child rows are shown. Always false; see <see cref="NavigateTo"/>.</summary>
        public bool IsExpanded (int rowNumber) => false;

        /// <summary>Sites or unsites the grid's child objects for a designer.</summary>
        public void SubObjectsSiteChange (bool site) { }

        /// <summary>Restores <c>AlternatingBackColor</c> to its default.</summary>
        public void ResetAlternatingBackColor () => AlternatingBackColor = SystemColors.Window;

        /// <summary>Restores <c>GridLineColor</c> to its default.</summary>
        public void ResetGridLineColor () => GridLineColor = SystemColors.Control;

        /// <summary>Restores <c>HeaderBackColor</c> to its default.</summary>
        public void ResetHeaderBackColor () => HeaderBackColor = SystemColors.Control;

        /// <summary>Restores <c>HeaderForeColor</c> to its default.</summary>
        public void ResetHeaderForeColor () => HeaderForeColor = SystemColors.ControlText;

        /// <summary>Restores <c>HeaderFont</c> to its default.</summary>
        public void ResetHeaderFont () => HeaderFont = null;

        /// <summary>Restores <c>LinkColor</c> to its default.</summary>
        public void ResetLinkColor () => LinkColor = SystemColors.HotTrack;

        /// <summary>Restores <see cref="LinkHoverColor"/> to its default.</summary>
        public void ResetLinkHoverColor () => LinkHoverColor = SystemColors.HotTrack;

        /// <summary>Restores <c>SelectionBackColor</c> to its default.</summary>
        public void ResetSelectionBackColor () => SelectionBackColor = SystemColors.Highlight;

        /// <summary>Restores <c>SelectionForeColor</c> to its default.</summary>
        public void ResetSelectionForeColor () => SelectionForeColor = SystemColors.HighlightText;

        /// <summary>Raised when <see cref="AllowNavigation"/> changes.</summary>
        public event EventHandler? AllowNavigationChanged;

        /// <summary>Raised when <see cref="ParentRowsVisible"/> changes.</summary>
        public event EventHandler? ParentRowsVisibleChanged;

        /// <summary>Raised when <see cref="ParentRowsLabelStyle"/> changes.</summary>
        public event EventHandler? ParentRowsLabelStyleChanged;

        // Declared and raisable. The properties behind them live on the hosted DataGridView, which has
        // its own notifications, so these are the .NET 1.x names for changes this layer reports
        // elsewhere rather than a second source of truth.
#pragma warning disable CS0067
        /// <summary>Raised when the background colour changes. Not raised by this layer.</summary>
        public event EventHandler? BackgroundColorChanged;

        /// <summary>Raised when the border style changes. Not raised by this layer.</summary>
        public event EventHandler? BorderStyleChanged;

        /// <summary>Raised when the caption's visibility changes. Not raised by this layer.</summary>
        public event EventHandler? CaptionVisibleChanged;

        /// <summary>Raised when flat mode changes. Not raised by this layer.</summary>
        public event EventHandler? FlatModeChanged;

        /// <summary>Raised when the read-only state changes. Not raised by this layer.</summary>
        public event EventHandler? ReadOnlyChanged;

        /// <summary>Raised when the data source changes. Not raised by this layer: DataSource is a
        /// pass-through to the hosted grid, which reports its own changes.</summary>
        public event EventHandler? DataSourceChanged;

        /// <summary>Raised when the back button is clicked. Not raised: see <see cref="NavigateBack"/>.</summary>
        public event EventHandler? BackButtonClick;

        /// <summary>Raised when the parent-details button is clicked. Not raised: see <see cref="NavigateTo"/>.</summary>
        public event EventHandler? ShowParentDetailsButtonClick;
#pragma warning restore CS0067

        private void Set<T> (ref T field, T value, Func<EventHandler?> handler)
        {
            if (EqualityComparer<T>.Default.Equals (field, value))
                return;

            field = value;
            handler ()?.Invoke (this, EventArgs.Empty);
        }
    }

    public partial class DataGridTableStyle
    {
        private Color link_hover_color = SystemColors.HotTrack;
        private bool column_headers_visible = true;
        private bool row_headers_visible = true;
        private int row_header_width = 35;
        private int preferred_column_width = 75;
        private int preferred_row_height = 16;
        private DataGridLineStyle grid_line_style = DataGridLineStyle.Solid;
        private Majorsilence.Forms.Drawing.Font? header_font;

        /// <summary>Gets or sets whether the column headers are shown.</summary>
        public bool ColumnHeadersVisible {
            get => column_headers_visible;
            set => Set (ref column_headers_visible, value, () => ColumnHeadersVisibleChanged);
        }

        /// <summary>Gets or sets whether the row headers are shown.</summary>
        public bool RowHeadersVisible {
            get => row_headers_visible;
            set => Set (ref row_headers_visible, value, () => RowHeadersVisibleChanged);
        }

        /// <summary>Gets or sets the width of the row headers.</summary>
        public int RowHeaderWidth {
            get => row_header_width;
            set => Set (ref row_header_width, value, () => RowHeaderWidthChanged);
        }

        /// <summary>Gets or sets the width columns prefer.</summary>
        public int PreferredColumnWidth {
            get => preferred_column_width;
            set => Set (ref preferred_column_width, value, () => PreferredColumnWidthChanged);
        }

        /// <summary>Gets or sets the height rows prefer.</summary>
        public int PreferredRowHeight {
            get => preferred_row_height;
            set => Set (ref preferred_row_height, value, () => PreferredRowHeightChanged);
        }

        /// <summary>Gets or sets how the grid lines are drawn.</summary>
        public DataGridLineStyle GridLineStyle {
            get => grid_line_style;
            set => Set (ref grid_line_style, value, () => GridLineStyleChanged);
        }

        /// <summary>Gets or sets the font used for the headers.</summary>
        public Majorsilence.Forms.Drawing.Font? HeaderFont {
            get => header_font;
            set => Set (ref header_font, value, () => HeaderFontChanged);
        }

        /// <summary>Gets or sets the colour of a link under the pointer.</summary>
        public Color LinkHoverColor {
            get => link_hover_color;
            set => Set (ref link_hover_color, value, () => LinkHoverColorChanged);
        }

        /// <summary>Puts the given cell into edit mode.</summary>
        /// <remarks>False: a table style describes how a grid draws its columns and has no grid of its
        /// own to edit. The DataGrid hosting the style is what BeginEdit is meaningful on.</remarks>
        public bool BeginEdit (DataGridColumnStyle gridColumn, int rowNumber) => false;

        /// <inheritdoc cref="BeginEdit"/>
        public bool EndEdit (DataGridColumnStyle gridColumn, int rowNumber, bool shouldAbort) => false;

        /// <summary>Restores <c>BackColor</c> to its default.</summary>
        public void ResetBackColor () => BackColor = SystemColors.Window;

        /// <summary>Restores <c>ForeColor</c> to its default.</summary>
        public void ResetForeColor () => ForeColor = SystemColors.WindowText;

        /// <summary>Restores <c>AlternatingBackColor</c> to its default.</summary>
        public void ResetAlternatingBackColor () => AlternatingBackColor = SystemColors.Window;

        /// <summary>Restores <c>GridLineColor</c> to its default.</summary>
        public void ResetGridLineColor () => GridLineColor = SystemColors.Control;

        /// <summary>Restores <c>HeaderBackColor</c> to its default.</summary>
        public void ResetHeaderBackColor () => HeaderBackColor = SystemColors.Control;

        /// <summary>Restores <c>HeaderForeColor</c> to its default.</summary>
        public void ResetHeaderForeColor () => HeaderForeColor = SystemColors.ControlText;

        /// <summary>Restores <see cref="HeaderFont"/> to its default.</summary>
        public void ResetHeaderFont () => HeaderFont = null;

        /// <summary>Restores <c>LinkColor</c> to its default.</summary>
        public void ResetLinkColor () => LinkColor = SystemColors.HotTrack;

        /// <summary>Restores <see cref="LinkHoverColor"/> to its default.</summary>
        public void ResetLinkHoverColor () => LinkHoverColor = SystemColors.HotTrack;

        /// <summary>Restores <c>SelectionBackColor</c> to its default.</summary>
        public void ResetSelectionBackColor () => SelectionBackColor = SystemColors.Highlight;

        /// <summary>Restores <c>SelectionForeColor</c> to its default.</summary>
        public void ResetSelectionForeColor () => SelectionForeColor = SystemColors.HighlightText;

        /// <summary>Raised when <see cref="ColumnHeadersVisible"/> changes.</summary>
        public event EventHandler? ColumnHeadersVisibleChanged;

        /// <summary>Raised when <see cref="RowHeadersVisible"/> changes.</summary>
        public event EventHandler? RowHeadersVisibleChanged;

        /// <summary>Raised when <see cref="RowHeaderWidth"/> changes.</summary>
        public event EventHandler? RowHeaderWidthChanged;

        /// <summary>Raised when <see cref="PreferredColumnWidth"/> changes.</summary>
        public event EventHandler? PreferredColumnWidthChanged;

        /// <summary>Raised when <see cref="PreferredRowHeight"/> changes.</summary>
        public event EventHandler? PreferredRowHeightChanged;

        /// <summary>Raised when <see cref="GridLineStyle"/> changes.</summary>
        public event EventHandler? GridLineStyleChanged;

        /// <summary>Raised when <see cref="HeaderFont"/> changes.</summary>
        public event EventHandler? HeaderFontChanged;

        /// <summary>Raised when <see cref="LinkHoverColor"/> changes.</summary>
        public event EventHandler? LinkHoverColorChanged;

        // The colour and flag properties they describe are plain auto-properties on this type, so
        // there is no setter to raise from. They are declared so a .NET 1.x table style can be wired
        // up, and a derived style that overrides those properties can raise them.
#pragma warning disable CS0067
        /// <summary>Raised when AllowSorting changes. Not raised by this layer.</summary>
        public event EventHandler? AllowSortingChanged;

        /// <summary>Raised when AlternatingBackColor changes. Not raised by this layer.</summary>
        public event EventHandler? AlternatingBackColorChanged;

        /// <summary>Raised when BackColor changes. Not raised by this layer.</summary>
        public event EventHandler? BackColorChanged;

        /// <summary>Raised when ForeColor changes. Not raised by this layer.</summary>
        public event EventHandler? ForeColorChanged;

        /// <summary>Raised when GridLineColor changes. Not raised by this layer.</summary>
        public event EventHandler? GridLineColorChanged;

        /// <summary>Raised when HeaderBackColor changes. Not raised by this layer.</summary>
        public event EventHandler? HeaderBackColorChanged;

        /// <summary>Raised when HeaderForeColor changes. Not raised by this layer.</summary>
        public event EventHandler? HeaderForeColorChanged;

        /// <summary>Raised when LinkColor changes. Not raised by this layer.</summary>
        public event EventHandler? LinkColorChanged;

        /// <summary>Raised when MappingName changes. Not raised by this layer.</summary>
        public event EventHandler? MappingNameChanged;

        /// <summary>Raised when ReadOnly changes. Not raised by this layer.</summary>
        public event EventHandler? ReadOnlyChanged;

        /// <summary>Raised when SelectionBackColor changes. Not raised by this layer.</summary>
        public event EventHandler? SelectionBackColorChanged;

        /// <summary>Raised when SelectionForeColor changes. Not raised by this layer.</summary>
        public event EventHandler? SelectionForeColorChanged;
#pragma warning restore CS0067

        private void Set<T> (ref T field, T value, Func<EventHandler?> handler)
        {
            if (EqualityComparer<T>.Default.Equals (field, value))
                return;

            field = value;
            handler ()?.Invoke (this, EventArgs.Empty);
        }
    }

    public partial class DataGridColumnStyle
    {
        private HorizontalAlignment alignment = HorizontalAlignment.Left;
        private string null_text = "(null)";
        private PropertyDescriptor? property_descriptor;

        /// <summary>Gets or sets how the column's content is aligned.</summary>
        public virtual HorizontalAlignment Alignment {
            get => alignment;
            set {
                if (alignment == value)
                    return;

                alignment = value;
                AlignmentChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the text shown for a null value.</summary>
        public virtual string NullText {
            get => null_text;
            set {
                if (null_text == value)
                    return;

                null_text = value ?? string.Empty;
                NullTextChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the property this column is bound to.</summary>
        public virtual PropertyDescriptor? PropertyDescriptor {
            get => property_descriptor;
            set {
                if (ReferenceEquals (property_descriptor, value))
                    return;

                property_descriptor = value;
                PropertyDescriptorChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Gets the table style this column belongs to.</summary>
        public virtual DataGridTableStyle? DataGridTableStyle { get; internal set; }

        /// <summary>Gets the accessible object describing this column's header.</summary>
        public AccessibleObject HeaderAccessibleObject
            => header_accessible_object ??= new DataGridColumnStyleHeaderAccessibleObject (this);

        private AccessibleObject? header_accessible_object;

        /// <summary>Restores <c>HeaderText</c> to its default.</summary>
        public void ResetHeaderText () => HeaderText = string.Empty;

        /// <summary>Raised when <see cref="Alignment"/> changes.</summary>
        public event EventHandler? AlignmentChanged;

        /// <summary>Raised when <see cref="NullText"/> changes.</summary>
        public event EventHandler? NullTextChanged;

        /// <summary>Raised when <see cref="PropertyDescriptor"/> changes.</summary>
        public event EventHandler? PropertyDescriptorChanged;

        // As on DataGridTableStyle, the properties these describe are plain auto-properties with no
        // setter to raise from.
#pragma warning disable CS0067
        /// <summary>Raised when the font changes. Not raised by this layer.</summary>
        public event EventHandler? FontChanged;

        /// <summary>Raised when HeaderText changes. Not raised by this layer.</summary>
        public event EventHandler? HeaderTextChanged;

        /// <summary>Raised when MappingName changes. Not raised by this layer.</summary>
        public event EventHandler? MappingNameChanged;

        /// <summary>Raised when ReadOnly changes. Not raised by this layer.</summary>
        public event EventHandler? ReadOnlyChanged;

        /// <summary>Raised when Width changes. Not raised by this layer.</summary>
        public event EventHandler? WidthChanged;
#pragma warning restore CS0067
    }

    /// <summary>Exposes a <see cref="DataGridColumnStyle"/>'s header to accessibility clients.</summary>
    public class DataGridColumnStyleHeaderAccessibleObject : AccessibleObject
    {
        private readonly DataGridColumnStyle column;

        /// <summary>Initializes a new instance of the <see cref="DataGridColumnStyleHeaderAccessibleObject"/> class.</summary>
        public DataGridColumnStyleHeaderAccessibleObject (DataGridColumnStyle owner) => column = owner;

        /// <summary>Gets the header text reported to assistive technology.</summary>
        public override string? Name => column.HeaderText;

        /// <summary>Gets the role reported to assistive technology.</summary>
        public override AccessibleRole Role => AccessibleRole.ColumnHeader;
    }

    public partial class DataGridBoolColumn
    {
        /// <summary>Gets or sets the value that means "null" for this column.</summary>
        public object? NullValue { get; set; }

        // The properties behind these are plain auto-properties on this type.
#pragma warning disable CS0067
        /// <summary>Raised when AllowNull changes. Not raised by this layer.</summary>
        public event EventHandler? AllowNullChanged;

        /// <summary>Raised when TrueValue changes. Not raised by this layer.</summary>
        public event EventHandler? TrueValueChanged;

        /// <summary>Raised when FalseValue changes. Not raised by this layer.</summary>
        public event EventHandler? FalseValueChanged;
#pragma warning restore CS0067
    }
}
