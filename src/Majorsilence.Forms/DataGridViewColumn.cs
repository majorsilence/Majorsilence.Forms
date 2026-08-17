using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a column in a DataGridView control.
    /// </summary>
    public partial class DataGridViewColumn : IDisposable
    {
        private string header_text = string.Empty;
        private int width = 100;
        private DataGridView? owner;
        private DataGridViewCellStyle default_cell_style = new DataGridViewCellStyle ();

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumn class.
        /// </summary>
        public DataGridViewColumn ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumn class with the specified header text.
        /// </summary>
        public DataGridViewColumn (string headerText)
        {
            header_text = headerText;
        }

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumn class with the specified cell template.
        /// WinForms compatibility.
        /// </summary>
        public DataGridViewColumn (DataGridViewCell cellTemplate)
        {
            CellTemplate = cellTemplate;
        }

        /// <summary>Releases resources used by the column. WinForms parity (DataGridViewBand is IDisposable); the compat column holds no unmanaged state.</summary>
        public void Dispose ()
        {
            Dispose (true);
            RaiseDisposed ();
            GC.SuppressFinalize (this);
        }

        /// <summary>Releases resources used by the column.</summary>
        protected virtual void Dispose (bool disposing) { }

        /// <summary>
        /// Gets or sets the name used to identify this column.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data source property name for this column.
        /// </summary>
        public string DataPropertyName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data type of the values in this column's cells. WinForms compatibility —
        /// used to drive default formatting; null when unbound/unknown.
        /// </summary>
        public Type? ValueType { get; set; }

        /// <summary>Telerik-style alias of DataPropertyName (GridViewDataColumn.FieldName).</summary>
        public string FieldName {
            get => DataPropertyName;
            set => DataPropertyName = value;
        }

        /// <summary>Gets or sets the value format string (Telerik GridViewColumn.FormatString).</summary>
        public string FormatString { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the column supports filtering. Stored for Telerik compat.</summary>
        public bool AllowFiltering { get; set; } = true;

        /// <summary>Telerik-style alias of <see cref="ValueType"/> (GridViewDataColumn.DataType).</summary>
        public Type? DataType {
            get => ValueType;
            set => ValueType = value;
        }

        /// <summary>
        /// Gets or sets whether this column is bound to a data source. WinForms compatibility stub.
        /// </summary>
        public bool IsDataBound { get; set; }

        /// <summary>
        /// Gets or sets whether cells in this column are read-only.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// Gets or sets the tooltip text for this column.
        /// </summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the default cell style for this column.
        /// </summary>
        public virtual DataGridViewCellStyle DefaultCellStyle {
            get => default_cell_style;
            set => default_cell_style = value ?? new DataGridViewCellStyle ();
        }

        /// <summary>
        /// Gets or sets whether the column is resizable.
        /// </summary>
        public DataGridViewTriState Resizable { get; set; } = DataGridViewTriState.NotSet;

        /// <summary>
        /// Gets or sets the sort mode for this column.
        /// </summary>
        public DataGridViewColumnSortMode SortMode { get; set; } = DataGridViewColumnSortMode.Automatic;

        /// <summary>
        /// Gets the bounding rectangle of the column header.
        /// </summary>
        internal Rectangle HeaderBounds { get; set; }

        /// <summary>
        /// Gets the header cell for this column.
        /// </summary>
        /// <summary>The header cell for this column.</summary>
        /// <remarks>
        /// Settable because grids replace it with their own type -- a filterable grid swaps in a
        /// header cell that paints a funnel and handles its clicks. Never null: assigning null
        /// restores a plain header rather than leaving the column without one.
        /// </remarks>
        public DataGridViewColumnHeaderCell HeaderCell {
            get {
                // Linked on read rather than only on write: the field initializer below cannot reference
                // `this`, and every constructor would otherwise have to remember to do it.
                header_cell.owning_column = this;
                return header_cell;
            }
            set {
                header_cell = value ?? new DataGridViewColumnHeaderCell ();
                header_cell.owning_column = this;
            }
        }

        private DataGridViewColumnHeaderCell header_cell = new DataGridViewColumnHeaderCell ();

        /// <summary>
        /// Gets or sets the header text for this column.
        /// </summary>
        public string HeaderText {
            get => header_text;
            set {
                if (header_text != value) {
                    header_text = value;
                    owner?.Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets the index of this column in the DataGridView.
        /// </summary>
        public int Index => owner?.Columns.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets or sets the minimum width, in pixels, of the column.
        /// </summary>
        public int MinimumWidth { get; set; } = 30;

        /// <summary>
        /// Gets the DataGridView control that contains this column.
        /// </summary>
        public DataGridView? DataGridView => owner;

        /// <summary>
        /// Gets or sets a value indicating whether the column is sortable.
        /// </summary>
        public bool Sortable { get; set; } = true;

        /// <summary>
        /// Gets or sets the sort order for this column.
        /// </summary>
        public SortOrder SortOrder { get; set; } = SortOrder.None;

        /// <summary>
        /// Gets or sets an object that contains data to associate with the column.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets whether the column is visible.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Telerik-style alias of <see cref="Visible"/> (GridViewColumn.IsVisible).</summary>
        public bool IsVisible {
            get => Visible;
            set => Visible = value;
        }

        /// <summary>Gets or sets the auto-size mode. Stub in Majorsilence.Forms.</summary>
        /// <remarks>NotSet is the default upstream, and it has to be: NotSet is what makes the column
        /// fall back to the grid's AutoSizeColumnsMode. Defaulting to None meant a column never
        /// inherited, so setting AutoSizeColumnsMode on the grid did nothing at all.</remarks>
        public DataGridViewAutoSizeColumnMode AutoSizeMode { get; set; } = DataGridViewAutoSizeColumnMode.NotSet;

        /// <summary>Gets or sets the relative fill weight for fill-mode auto-sizing. Stub.</summary>
        public float FillWeight { get; set; } = 100f;

        /// <summary>Gets or sets whether the column is frozen to the left (does not scroll horizontally).</summary>
        public bool Frozen { get; set; }

        /// <summary>
        /// Whether the column is pinned to the right edge (does not scroll horizontally). Telerik-only
        /// concept, set via <c>GridViewColumn.PinPosition = PinnedColumnPosition.Right</c>.
        /// </summary>
        internal bool PinnedRight { get; set; }

        /// <summary>Gets or sets the width of the column divider. Stub in Majorsilence.Forms.</summary>
        public int DividerWidth { get; set; }

        /// <summary>Gets or sets the template used to create new cells. Stub in Majorsilence.Forms.</summary>
        public virtual DataGridViewCell? CellTemplate { get; set; }

        /// <summary>Gets or sets the display order of the column. Stub in Majorsilence.Forms.</summary>
        public int DisplayIndex {
            get => Index;
            set { /* ordering not implemented */ }
        }

        /// <summary>Gets or sets the column cell content alignment.</summary>
        public ContentAlignment DefaultCellStyleAlignment { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>Gets or sets the alignment of the column header text.</summary>
        public ContentAlignment HeaderAlignment { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>
        /// When true, the renderer draws a check-box glyph instead of text for this column's cells.
        /// Default false; check-box column types (including the Telerik-compat GridViewCheckBoxColumn) override.
        /// </summary>
        protected internal virtual bool DisplaysAsCheckBox => false;

        /// <summary>
        /// Gets or sets the width, in pixels, of the column.
        /// </summary>
        public int Width {
            get => width;
            set {
                value = Math.Max (value, MinimumWidth);

                if (width != value) {
                    width = value;
                    owner?.OnColumnsChanged ();
                }
            }
        }

        /// <summary>
        /// Gets the style used for this column's cells: the grid's
        /// <see cref="DataGridView.DefaultCellStyle"/> overlaid with this column's
        /// <see cref="DefaultCellStyle"/>. Mirrors WinForms DataGridViewColumn.InheritedStyle.
        /// </summary>
        public DataGridViewCellStyle InheritedStyle {
            get {
                var result = new DataGridViewCellStyle ();

                if (owner is not null)
                    result.ApplyStyle (owner.DefaultCellStyle.ToDataGridViewCellStyle ());

                result.ApplyStyle (DefaultCellStyle);
                return result;
            }
        }

        /// <summary>Gets the state of this column (WinForms DataGridViewColumn.State / InheritedState).</summary>
        public DataGridViewElementStates State {
            get {
                var state = DataGridViewElementStates.None;

                if (Visible)
                    state |= DataGridViewElementStates.Visible;
                if (ReadOnly || (owner?.ReadOnly ?? false))
                    state |= DataGridViewElementStates.ReadOnly;
                if (Frozen)
                    state |= DataGridViewElementStates.Frozen;
                if (!HeaderBounds.IsEmpty)
                    state |= DataGridViewElementStates.Displayed;
                if (Resizable != DataGridViewTriState.False && (owner?.AllowUserToResizeColumns ?? true))
                    state |= DataGridViewElementStates.Resizable;
                if (Resizable != DataGridViewTriState.NotSet)
                    state |= DataGridViewElementStates.ResizableSet;

                return state;
            }
        }

        /// <summary>Gets the state of this column, including state inherited from the grid. Mirrors WinForms.</summary>
        public DataGridViewElementStates InheritedState => State;

        /// <summary>
        /// Creates an exact copy of this column (WinForms DataGridViewColumn.Clone). The clone is of the
        /// same runtime type and is unowned -- add it to a grid's column collection to attach it.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072",
            Justification = "Column types are concrete public types with public parameterless constructors; cloning mirrors WinForms.")]
        public virtual object Clone ()
        {
            var clone = (DataGridViewColumn)Activator.CreateInstance (GetType ())!;
            CopyStateTo (clone);
            return clone;
        }

        /// <summary>
        /// Copies this column's own (non-ownership) state onto <paramref name="target"/>. Derived column
        /// types override to carry their extra members across a <see cref="Clone"/>.
        /// </summary>
        protected virtual void CopyStateTo (DataGridViewColumn target)
        {
            ArgumentNullException.ThrowIfNull (target);

            target.header_text = header_text;
            target.width = width;
            target.Name = Name;
            target.DataPropertyName = DataPropertyName;
            target.ValueType = ValueType;
            target.FormatString = FormatString;
            target.AllowFiltering = AllowFiltering;
            target.IsDataBound = IsDataBound;
            target.ReadOnly = ReadOnly;
            target.ToolTipText = ToolTipText;
            target.DefaultCellStyle = DefaultCellStyle.Clone ();
            target.Resizable = Resizable;
            target.SortMode = SortMode;
            target.MinimumWidth = MinimumWidth;
            target.Sortable = Sortable;
            target.SortOrder = SortOrder;
            target.Tag = Tag;
            target.Visible = Visible;
            target.AutoSizeMode = AutoSizeMode;
            target.FillWeight = FillWeight;
            target.Frozen = Frozen;
            target.PinnedRight = PinnedRight;
            target.DividerWidth = DividerWidth;
            target.CellTemplate = CellTemplate;
            target.DefaultCellStyleAlignment = DefaultCellStyleAlignment;
            target.HeaderAlignment = HeaderAlignment;
        }

        /// <summary>
        /// Sets the owning DataGridView.
        /// </summary>
        internal void SetOwner (DataGridView? dataGridView) => owner = dataGridView;
    }

    /// <summary>
    /// Specifies the appearance of a control.
    /// </summary>
    public enum FlatStyle
    {
        /// <summary>Flat appearance.</summary>
        Flat,
        /// <summary>Popup appearance.</summary>
        Popup,
        /// <summary>Standard (3D) appearance.</summary>
        Standard,
        /// <summary>Uses the system default.</summary>
        System
    }

    /// <summary>
    /// Specifies the sort order for a column.
    /// </summary>
    public enum SortOrder
    {
        /// <summary>
        /// No sort order.
        /// </summary>
        None,
        /// <summary>
        /// Items are sorted in ascending order.
        /// </summary>
        Ascending,
        /// <summary>
        /// Items are sorted in descending order.
        /// </summary>
        Descending
    }
}
