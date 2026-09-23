using System.Collections.Generic;
﻿using System.Drawing;

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
        private string name = string.Empty;

        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public string Name {
            get => name;
            set {
                if (EqualityComparer<string>.Default.Equals (name, value))
                    return;

                name = value;
                DataGridView?.NotifyColumnNameChanged (this);
            }
        }

        /// <summary>
        /// Gets or sets the data source property name for this column.
        /// </summary>
        private string data_property_name = string.Empty;

        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public string DataPropertyName {
            get => data_property_name;
            set {
                if (EqualityComparer<string>.Default.Equals (data_property_name, value))
                    return;

                data_property_name = value;
                DataGridView?.NotifyColumnDataPropertyNameChanged (this);
            }
        }

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
        private bool read_only;

        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public bool ReadOnly {
            get => read_only;
            set {
                if (EqualityComparer<bool>.Default.Equals (read_only, value))
                    return;

                read_only = value;
                DataGridView?.NotifyColumnStateChanged (this, DataGridViewElementStates.ReadOnly);
            }
        }

        /// <summary>
        /// Gets or sets the tooltip text for this column.
        /// </summary>
        private string tool_tip_text = string.Empty;

        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public string ToolTipText {
            get => tool_tip_text;
            set {
                if (EqualityComparer<string>.Default.Equals (tool_tip_text, value))
                    return;

                tool_tip_text = value;
                DataGridView?.NotifyColumnToolTipTextChanged (this);
            }
        }

        /// <summary>
        /// Gets or sets the default cell style for this column.
        /// </summary>
        public virtual DataGridViewCellStyle DefaultCellStyle {
            get {
                // Attached on every read, not once: the column may join its grid after the style was
                // handed out, and the attachment is two field writes (CellStyleContentChanged, W6).
                default_cell_style.Attach (DataGridView, DataGridViewCellStyleScopes.Column);
                return default_cell_style;
            }
            set {
                default_cell_style = value ?? new DataGridViewCellStyle ();
                default_cell_style.Attach (DataGridView, DataGridViewCellStyleScopes.Column);
                DataGridView?.NotifyColumnDefaultCellStyleChanged (this);
            }
        }

        /// <summary>
        /// Gets or sets whether the column is resizable.
        /// </summary>
        private DataGridViewTriState resizable = DataGridViewTriState.NotSet;

        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public DataGridViewTriState Resizable {
            get => resizable;
            set {
                if (EqualityComparer<DataGridViewTriState>.Default.Equals (resizable, value))
                    return;

                resizable = value;
                DataGridView?.NotifyColumnStateChanged (this, DataGridViewElementStates.Resizable);
            }
        }

        /// <summary>
        /// Gets or sets the sort mode for this column.
        /// </summary>
        public DataGridViewColumnSortMode SortMode {
            get => sort_mode;
            set {
                if (sort_mode == value)
                    return;

                sort_mode = value;
                owner?.RaiseColumnSortModeChanged (this);
            }
        }

        private DataGridViewColumnSortMode sort_mode = DataGridViewColumnSortMode.Automatic;

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
                DataGridView?.NotifyColumnHeaderCellChanged (this);
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
        private int minimum_width = 5;

        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public int MinimumWidth {
            get => minimum_width;
            set {
                if (EqualityComparer<int>.Default.Equals (minimum_width, value))
                    return;

                minimum_width = value;
                DataGridView?.NotifyColumnMinimumWidthChanged (this);
            }
        }

        /// <summary>
        /// Gets the DataGridView control that contains this column.
        /// </summary>
        public DataGridView? DataGridView => owner;

        /// <summary>
        /// Gets or sets a value indicating whether the column is sortable.
        /// </summary>
        public bool Sortable { get; set; } = true;

        /// <summary>
        /// Gets or sets the direction of the sort glyph on this column's header. This IS
        /// <see cref="DataGridViewColumnHeaderCell.SortGlyphDirection"/> under this library's older
        /// name: two fields drifted apart, and the renderer drew the one the grid did not set (DGV-16).
        /// </summary>
        public SortOrder SortOrder {
            get => HeaderCell.SortGlyphDirection;
            set => HeaderCell.SortGlyphDirection = value;
        }

        /// <summary>
        /// Gets or sets an object that contains data to associate with the column.
        /// </summary>
        public object? Tag { get; set; }

        private bool visible = true;

        /// <summary>Gets or sets whether the column is visible.</summary>
        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public bool Visible {
            get => visible;
            set {
                if (EqualityComparer<bool>.Default.Equals (visible, value))
                    return;

                visible = value;
                DataGridView?.NotifyColumnStateChanged (this, DataGridViewElementStates.Visible);
            }
        }

        /// <summary>Telerik-style alias of <see cref="Visible"/> (GridViewColumn.IsVisible).</summary>
        public bool IsVisible {
            get => Visible;
            set => Visible = value;
        }

        /// <summary>Gets or sets the auto-size mode. Stub in Majorsilence.Forms.</summary>
        /// <remarks>NotSet is the default upstream, and it has to be: NotSet is what makes the column
        /// fall back to the grid's AutoSizeColumnsMode. Defaulting to None meant a column never
        /// inherited, so setting AutoSizeColumnsMode on the grid did nothing at all.</remarks>
        public DataGridViewAutoSizeColumnMode AutoSizeMode {
            get => auto_size_mode;
            set {
                if (auto_size_mode == value)
                    return;

                var previous_mode = auto_size_mode;
                auto_size_mode = value;
                owner?.OnColumnsChanged ();      // Fill is applied by the layout pass (DGV-18)
                owner?.RaiseAutoSizeColumnModeChanged (this, previous_mode);
            }
        }

        private DataGridViewAutoSizeColumnMode auto_size_mode = DataGridViewAutoSizeColumnMode.NotSet;

        /// <summary>Gets or sets the relative fill weight for fill-mode auto-sizing. Stub.</summary>
        public float FillWeight {
            get => fill_weight;
            set {
                if (fill_weight == value)
                    return;

                fill_weight = value;
                owner?.OnColumnsChanged ();
            }
        }

        private float fill_weight = 100f;

        private bool frozen;

        /// <summary>Gets or sets whether the column is frozen to the left (does not scroll horizontally).</summary>
        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public bool Frozen {
            get => frozen;
            set {
                if (EqualityComparer<bool>.Default.Equals (frozen, value))
                    return;

                frozen = value;
                DataGridView?.NotifyColumnStateChanged (this, DataGridViewElementStates.Frozen);
            }
        }

        /// <summary>
        /// Whether the column is pinned to the right edge (does not scroll horizontally). Telerik-only
        /// concept, set via <c>GridViewColumn.PinPosition = PinnedColumnPosition.Right</c>.
        /// </summary>
        internal bool PinnedRight { get; set; }

        private int divider_width;

        /// <summary>Gets or sets the width of the column divider.</summary>
        /// <remarks>Notifies the owning grid on change, as upstream's setter does (W6.1, DGV-45).</remarks>
        public int DividerWidth {
            get => divider_width;
            set {
                if (EqualityComparer<int>.Default.Equals (divider_width, value))
                    return;

                divider_width = value;
                DataGridView?.NotifyColumnDividerWidthChanged (this);
            }
        }

        /// <summary>Gets or sets the template used to create new cells. Stub in Majorsilence.Forms.</summary>
        public virtual DataGridViewCell? CellTemplate { get; set; }

        /// <summary>Gets or sets the display order of the column. Stub in Majorsilence.Forms.</summary>
        public int DisplayIndex {
            get => Index;
            set { /* ordering not implemented */ }
        }

        /// <summary>
        /// Gets or sets the alignment of this column's cells. This IS
        /// <see cref="DataGridViewCellStyle.Alignment"/> on <see cref="DefaultCellStyle"/> under this
        /// library's older name -- the renderer read this one and the public style was ignored, so
        /// <c>Columns["Amount"].DefaultCellStyle.Alignment = MiddleRight</c>, the single most common
        /// column customisation, left numbers left-aligned (DGV-21). The two enums share their values.
        /// </summary>
        public ContentAlignment DefaultCellStyleAlignment {
            get => DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? ContentAlignment.MiddleLeft
                : (ContentAlignment)(int)DefaultCellStyle.Alignment;
            set => DefaultCellStyle.Alignment = (DataGridViewContentAlignment)(int)value;
        }

        /// <summary>Gets or sets the alignment of the column header text.</summary>
        public ContentAlignment HeaderAlignment { get; set; } = ContentAlignment.MiddleLeft;

        /// <summary>
        /// When true, the renderer draws a check-box glyph instead of text for this column's cells.
        /// Default false; check-box column types (including the Telerik-compat GridViewCheckBoxColumn) override.
        /// </summary>
        protected internal virtual bool DisplaysAsCheckBox => false;

        /// <summary>
        /// When true, the renderer draws the cell's image instead of text. Default false; image column
        /// types override.
        /// </summary>
        /// <remarks>
        /// The companion to <see cref="DisplaysAsCheckBox"/>, and it exists for the same reason: the
        /// renderer used to select the image path on the concrete DataGridViewImageColumn type, so an
        /// image column that had to derive from something else -- a Telerik-compat column, which must be
        /// a GridViewDataColumn to belong to RadGridView's Columns collection -- could not render as one.
        /// </remarks>
        protected internal virtual bool DisplaysAsImage => false;

        /// <summary>
        /// The column-level fallback image, used when a cell has no image of its own. Null unless an
        /// image column type supplies one.
        /// </summary>
        protected internal virtual Majorsilence.Forms.Drawing.Image? ColumnImage => null;

        // The layout pass sets Fill widths through here rather than the setter, whose OnColumnsChanged
        // would call back into the layout that is running. MinimumWidth is enforced here, once.
        internal void SetWidthFromLayout (int value) => width = Math.Max (value, MinimumWidth);

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
                    owner?.RaiseColumnWidthChanged (this);
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

        /// <summary>
        /// Gets or sets whether this column is selected. Setting it selects or deselects the column in
        /// the owning grid, which repaints and raises <see cref="DataGridView.SelectionChanged"/>; with
        /// <see cref="DataGridView.MultiSelect"/> off, selecting this column deselects everything else.
        /// A column that belongs to no grid simply stores the value.
        /// </summary>
        public bool Selected {
            get => selected;
            set {
                if (owner is { } grid)
                    grid.SetColumnSelected (this, value);
                else
                    SetSelectedCore (value, 0);
            }
        }

        private bool selected;

        // See DataGridViewRow.SetSelectedCore: the flag and its recency stamp move together.
        internal void SetSelectedCore (bool value, long order)
        {
            selected = value;
            selection_order = order;
        }

        // When this column was most recently selected. See DataGridViewRow.SelectionOrder.
        internal long SelectionOrder => selection_order;

        private long selection_order;

        /// <summary>Gets the state of this column (WinForms DataGridViewColumn.State / InheritedState).</summary>
        public DataGridViewElementStates State {
            get {
                var state = DataGridViewElementStates.None;

                if (Visible)
                    state |= DataGridViewElementStates.Visible;
                if (Selected)
                    state |= DataGridViewElementStates.Selected;
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
            Guard.ThrowIfNull (target);

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
