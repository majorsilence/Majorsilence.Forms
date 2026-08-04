using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a cell in a DataGridView control.
    /// </summary>
    public partial class DataGridViewCell
    {
        /// <summary>The owning column (Telerik GridViewCellInfo.ColumnInfo naming).</summary>
        public DataGridViewColumn? ColumnInfo { get; internal set; }

        /// <summary>The editing-control type for this cell. Mirrors WinForms DataGridViewCell.EditType.</summary>
        public virtual Type? EditType => null;

        /// <summary>The value type of the cell. Mirrors WinForms DataGridViewCell.ValueType.</summary>
        public virtual Type? ValueType => null;

        /// <summary>The default value for a new row's cell. Mirrors WinForms.</summary>
        public virtual object? DefaultNewRowValue => null;

        /// <summary>Initializes the hosted editing control. Mirrors WinForms.</summary>
        public virtual void InitializeEditingControl (int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
        }

        private object? value;
        private DataGridViewRow? owner;

        // Default style used as the base parent for all cell Style instances.
        internal static readonly ControlStyle DefaultCellStyleInternal = new ControlStyle (null,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.ForegroundColor = Theme.ForegroundColor;
            });

        /// <summary>
        /// Initializes a new instance of the DataGridViewCell class.
        /// </summary>
        public DataGridViewCell ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the DataGridViewCell class with the specified value.
        /// </summary>
        public DataGridViewCell (object? value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the bounding rectangle of the cell.
        /// </summary>
        internal Rectangle Bounds { get; set; }

        /// <summary>
        /// Gets the column index of this cell.
        /// </summary>
        public int ColumnIndex => owner?.Cells.IndexOf (this) ?? -1;

        /// <summary>
        /// Gets the DataGridView that contains this cell.
        /// </summary>
        public DataGridView? DataGridView => owner?.DataGridView;

        /// <summary>
        /// Gets the row that contains this cell.
        /// </summary>
        public DataGridViewRow? OwningRow => owner;

        /// <summary>
        /// Gets the row index of this cell.
        /// </summary>
        public int RowIndex => owner?.Index ?? -1;

        /// <summary>
        /// Gets or sets whether this cell is selected.
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>Gets whether this cell is currently being edited. Mirrors WinForms DataGridViewCell.IsInEditMode.</summary>
        public bool IsInEditMode => DataGridView?.IsCellInEditMode (RowIndex, ColumnIndex) ?? false;

        /// <summary>
        /// Gets the current value of the cell including any uncommitted edit. Mirrors WinForms
        /// DataGridViewCell.EditedFormattedValue; falls back to <see cref="Value"/> when the cell
        /// is not being edited.
        /// </summary>
        public object? EditedFormattedValue => IsInEditMode ? DataGridView?.CurrentEditValue ?? Value : Value;

        /// <summary>
        /// Gets or sets the style for this cell.
        /// </summary>
        public ControlStyle Style {
            get => cell_style;
            set {
                cell_style = value;
                // Records that a style was set on this cell rather than inherited, which is what
                // HasStyle reports; the initial value is not an assignment.
                has_explicit_style = true;
            }
        }

        private ControlStyle cell_style = new ControlStyle (DefaultCellStyleInternal);
        private bool has_explicit_style;

        /// <summary>
        /// Gets or sets an object that contains data to associate with the cell.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Gets or sets the value of this cell.
        /// </summary>
        public object? Value {
            get => value;
            set {
                if (!Equals (this.value, value)) {
                    this.value = value;
                    owner?.DataGridView?.Invalidate ();
                }
            }
        }

        /// <summary>Gets the formatted (display) value of this cell.</summary>
        public object? FormattedValue => FormattedTextOverride ?? value?.ToString ();

        /// <summary>
        /// An optional display-text override set by a formatting pass (e.g. RadGridView's CellFormatting
        /// or a column FormatString). When set, the renderer draws this instead of the raw value. Reset
        /// each paint by the formatting hook so it never goes stale.
        /// </summary>
        internal string? FormattedTextOverride { get; set; }

        /// <summary>Gets or sets whether this cell is read-only.</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Gets or sets the tooltip text for this cell.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the error message text for this cell. Stub in Majorsilence.Forms.</summary>
        public string ErrorText { get; set; } = string.Empty;

        /// <summary>Gets or sets whether this cell is visible. Stub in Majorsilence.Forms.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Gets the column that contains this cell.</summary>
        public DataGridViewColumn? OwningColumn {
            get {
                var colIndex = ColumnIndex;
                var dgv = DataGridView;
                if (dgv is null || colIndex < 0 || colIndex >= dgv.Columns.Count) return null;
                return dgv.Columns[colIndex];
            }
        }

        /// <summary>
        /// Gets the style actually used to display this cell -- the WinForms style cascade, applied from
        /// lowest to highest precedence: the grid's <see cref="DataGridView.DefaultCellStyle"/>, the
        /// owning column's <see cref="DataGridViewColumn.DefaultCellStyle"/>, the grid's
        /// <see cref="DataGridView.RowsDefaultCellStyle"/>, its
        /// <see cref="DataGridView.AlternatingRowsDefaultCellStyle"/> (odd rows only), the owning row's
        /// <see cref="DataGridViewRow.DefaultCellStyle"/> and finally this cell's own
        /// <see cref="Style"/>. Computed fresh on each call.
        /// </summary>
        public DataGridViewCellStyle InheritedStyle {
            get {
                var grid = DataGridView;
                var result = new DataGridViewCellStyle ();

                if (grid is not null)
                    result.ApplyStyle (grid.DefaultCellStyle.ToDataGridViewCellStyle ());

                if (OwningColumn is { } column)
                    result.ApplyStyle (column.DefaultCellStyle);

                if (grid is not null) {
                    result.ApplyStyle (grid.RowsDefaultCellStyle.ToDataGridViewCellStyle ());

                    // Alternating-row default style only participates on odd rows, as in WinForms.
                    if (RowIndex % 2 == 1)
                        result.ApplyStyle (grid.AlternatingRowsDefaultCellStyle.ToDataGridViewCellStyle ());
                }

                if (owner is not null)
                    result.ApplyStyle (owner.DefaultCellStyle);

                result.ApplyStyle (Style.ToDataGridViewCellStyle ());
                return result;
            }
        }

        /// <summary>
        /// Gets the state of this cell, combining its own state with the state inherited from its row
        /// and column (WinForms DataGridViewCell.InheritedState).
        /// </summary>
        public DataGridViewElementStates InheritedState {
            get {
                var state = DataGridViewElementStates.None;
                var row = owner;
                var column = OwningColumn;

                if (Visible && (row?.Visible ?? true) && (column?.Visible ?? true))
                    state |= DataGridViewElementStates.Visible;

                if (ReadOnly || (row?.ReadOnly ?? false) || (column?.ReadOnly ?? false) || (DataGridView?.ReadOnly ?? false))
                    state |= DataGridViewElementStates.ReadOnly;

                if (Selected || (row?.Selected ?? false))
                    state |= DataGridViewElementStates.Selected;

                if (row?.Frozen ?? false)
                    state |= DataGridViewElementStates.Frozen;

                if (column?.Frozen ?? false)
                    state |= DataGridViewElementStates.Frozen;

                if (!Bounds.IsEmpty)
                    state |= DataGridViewElementStates.Displayed;

                if ((row?.Resizable ?? DataGridViewTriState.NotSet) != DataGridViewTriState.False
                    && (DataGridView?.AllowUserToResizeRows ?? true))
                    state |= DataGridViewElementStates.Resizable;

                return state;
            }
        }

        /// <summary>
        /// Creates an exact copy of this cell (WinForms DataGridViewCell.Clone). The clone is of the same
        /// runtime type and is unowned -- add it to a row's cell collection to attach it.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2072",
            Justification = "Cell types are concrete public types with public parameterless constructors; cloning mirrors WinForms.")]
        public virtual object Clone ()
        {
            var clone = (DataGridViewCell)Activator.CreateInstance (GetType ())!;
            CopyStateTo (clone);
            return clone;
        }

        /// <summary>
        /// Copies this cell's own (non-ownership) state onto <paramref name="target"/>. Derived cell types
        /// override to carry their extra members across a <see cref="Clone"/>.
        /// </summary>
        protected virtual void CopyStateTo (DataGridViewCell target)
        {
            ArgumentNullException.ThrowIfNull (target);

            target.value = value;
            target.Tag = Tag;
            target.ReadOnly = ReadOnly;
            target.Selected = Selected;
            target.ToolTipText = ToolTipText;
            target.ErrorText = ErrorText;
            target.Visible = Visible;
            target.Style.BackgroundColor = Style.BackgroundColor;
            target.Style.ForegroundColor = Style.ForegroundColor;
            target.Style.Font = Style.Font;
            target.Style.FontSize = Style.FontSize;
            target.Style.Alignment = Style.Alignment;
        }

        /// <summary>
        /// Sets the owning row.
        /// </summary>
        internal void SetOwner (DataGridViewRow? row) => owner = row;
    }
}
