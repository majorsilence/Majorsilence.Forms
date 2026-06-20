using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Continuum.Forms;
using SkiaSharp;

namespace Continuum.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat data grid. Backed by <see cref="Continuum.Forms.DataGridView"/>. Provides the most
    /// common RadGridView surface: <see cref="MasterTemplate"/>/<see cref="TableViewDefinition"/>
    /// boilerplate, the GridView* column types, and Telerik-style row/cell access via
    /// <see cref="GridViewRowInfo"/>/<see cref="GridViewCellInfo"/> wrappers over the underlying rows.
    /// </summary>
    public class RadGridView : DataGridView
    {
        /// <summary>Initializes a new instance of the RadGridView class.</summary>
        public RadGridView ()
        {
            MasterTemplate = new GridViewTemplate (this);

            // Forward the real (raised) base events to their Telerik-shaped equivalents so migrated
            // grid handlers actually run. CellClick also drives CommandCellClick (Telerik's button-
            // column command pattern dispatches by ColumnInfo.Name), and selection changes drive both
            // SelectionChanged and CurrentRowChanged.
            base.CellClick += (_, e) => {
                var args = BuildCellArgs (e.ColumnIndex, e.RowIndex);
                _cellClick?.Invoke (this, args);
                _commandCellClick?.Invoke (this, args);
            };
            base.CellValueChanged += (_, e) => {
                var args = BuildCellArgs (e.ColumnIndex, e.RowIndex);
                _cellValueChanged?.Invoke (this, args);
                _valueChanged?.Invoke (this, args);
            };
            base.CellEndEdit += (_, e) => _cellEndEdit?.Invoke (this, BuildCellArgs (e.ColumnIndex, e.RowIndex));
            base.CellBeginEdit += (_, e) => _cellBeginEdit?.Invoke (this, new GridViewCellCancelEventArgs { RowIndex = e.RowIndex, ColumnIndex = e.ColumnIndex, Row = RowAt (e.RowIndex) });
            base.SelectionChanged += (_, e) => {
                _selectionChanged?.Invoke (this, e);
                _currentRowChanged?.Invoke (this, e);
            };
            // Base CellDoubleClick is not raised; derive it from the control's double-click + current cell.
            DoubleClick += (_, _) => {
                var cell = base.CurrentCell;
                if (cell is not null)
                    _cellDoubleClick?.Invoke (this, BuildCellArgs (cell.ColumnIndex, cell.RowIndex));
            };
        }

        /// <summary>Gets the master template (Telerik configuration façade over this grid).</summary>
        public GridViewTemplate MasterTemplate { get; }

        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        /// <summary>Gets the root element of the grid (stub).</summary>
        public RadElement GridViewElement { get; } = new RadElement ();

        /// <summary>Gets the root element of the grid (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets the current row as a Telerik <see cref="GridViewRowInfo"/>, or null.</summary>
        public new GridViewRowInfo? CurrentRow {
            get {
                var row = base.CurrentRow;
                return row is null ? null : new GridViewRowInfo (row);
            }
        }

        /// <summary>Gets the rows, accessible Telerik-style (indexer/enumeration yield <see cref="GridViewRowInfo"/>) while still supporting Count/Add/Clear/Remove.</summary>
        public new GridViewRowInfoCollection Rows => new GridViewRowInfoCollection (base.Rows);

        /// <summary>Gets the number of rows.</summary>
        public int RowCount => base.Rows.Count;

        // ── Telerik config surface (forwarded to MasterTemplate so grid.X and grid.MasterTemplate.X agree) ──

        /// <summary>Gets or sets whether a new-row entry is shown.</summary>
        public bool AllowAddNewRow { get => MasterTemplate.AllowAddNewRow; set => MasterTemplate.AllowAddNewRow = value; }
        /// <summary>Gets or sets whether rows can be deleted.</summary>
        public bool AllowDeleteRow { get => MasterTemplate.AllowDeleteRow; set => MasterTemplate.AllowDeleteRow = value; }
        /// <summary>Gets or sets whether rows can be edited.</summary>
        public bool AllowEditRow { get => MasterTemplate.AllowEditRow; set => MasterTemplate.AllowEditRow = value; }
        /// <summary>Gets or sets whether columns can be reordered.</summary>
        public bool AllowColumnReorder { get => MasterTemplate.AllowColumnReorder; set => MasterTemplate.AllowColumnReorder = value; }
        /// <summary>Gets or sets whether the column chooser is allowed.</summary>
        public bool AllowColumnChooser { get => MasterTemplate.AllowColumnChooser; set => MasterTemplate.AllowColumnChooser = value; }
        /// <summary>Gets or sets whether multiple rows can be selected.</summary>
        public bool MultiSelect { get => MasterTemplate.MultiSelect; set => MasterTemplate.MultiSelect = value; }
        /// <summary>Gets or sets whether filtering is enabled.</summary>
        public bool EnableFiltering { get => MasterTemplate.EnableFiltering; set => MasterTemplate.EnableFiltering = value; }
        /// <summary>Gets or sets whether sorting is enabled. When false, header-click sorting is disabled for every column.</summary>
        public bool EnableSorting {
            get => MasterTemplate.EnableSorting;
            set { MasterTemplate.EnableSorting = value; ApplyEnableSorting (); }
        }

        // When sorting is disabled grid-wide, force every column non-sortable (the base only sorts
        // columns whose Sortable flag is true). Applied on set and whenever the column set changes.
        private void ApplyEnableSorting ()
        {
            // MasterTemplate may be null if a base-ctor column change fires before our ctor body runs.
            if (MasterTemplate is null || MasterTemplate.EnableSorting)
                return;

            foreach (DataGridViewColumn column in base.Columns)
                column.Sortable = false;
        }

        /// <inheritdoc/>
        internal override void OnColumnsChanged ()
        {
            base.OnColumnsChanged ();
            ApplyEnableSorting ();
        }
        /// <summary>Gets or sets whether grouped data auto-expands.</summary>
        public bool AutoExpandGroups { get => MasterTemplate.AutoExpandGroups; set => MasterTemplate.AutoExpandGroups = value; }
        /// <summary>Gets or sets whether the group panel is shown. Stub.</summary>
        public bool ShowGroupPanel { get; set; }
        /// <summary>Gets or sets whether grouping is enabled. Stub.</summary>
        public bool EnableGrouping { get; set; }
        /// <summary>Gets or sets whether alternating rows are colored. Stub.</summary>
        public bool EnableAlternatingRowColor { get; set; }

        /// <summary>Auto-sizes every visible column to fit its header and (formatted) cell content.</summary>
        public void BestFitColumns ()
        {
            const int padding = 18;
            var fontSize = Theme.ItemFontSize;

            foreach (DataGridViewColumn column in base.Columns) {
                if (!column.Visible)
                    continue;

                var width = (int)TextMeasurer.MeasureText (column.HeaderText ?? string.Empty, Theme.UIFontBold, fontSize).Width;
                var columnIndex = column.Index;

                // Check-box columns render a fixed-size glyph, so size to the header only.
                if (!column.DisplaysAsCheckBox && columnIndex >= 0) {
                    foreach (var row in base.Rows) {
                        if (columnIndex >= row.Cells.Count)
                            continue;

                        var text = ComputeFormattedText (row.Cells[columnIndex], column);
                        var w = (int)TextMeasurer.MeasureText (text, Theme.UIFont, fontSize).Width;

                        if (w > width)
                            width = w;
                    }
                }

                column.Width = Math.Max (column.MinimumWidth, width + padding);
            }

            Invalidate ();
        }

        /// <summary>Begins a batch update. Stub.</summary>
        public void BeginUpdate () { }
        /// <summary>Ends a batch update. Stub.</summary>
        public void EndUpdate () => Invalidate ();

        // ── Events (forwarded from the base grid; see ctor) ──

        private EventHandler<GridViewCellEventArgs>? _cellClick, _commandCellClick, _cellDoubleClick, _cellValueChanged, _valueChanged, _cellEndEdit;
        private EventHandler<GridViewCellCancelEventArgs>? _cellBeginEdit;
        private EventHandler? _selectionChanged, _currentRowChanged;

        /// <summary>Raised when a cell is clicked.</summary>
        public new event EventHandler<GridViewCellEventArgs>? CellClick { add => _cellClick += value; remove => _cellClick -= value; }
        /// <summary>Raised on a command-cell (button column) click. Fires alongside <see cref="CellClick"/>.</summary>
        public event EventHandler<GridViewCellEventArgs>? CommandCellClick { add => _commandCellClick += value; remove => _commandCellClick -= value; }
        /// <summary>Raised when a cell is double-clicked.</summary>
        public new event EventHandler<GridViewCellEventArgs>? CellDoubleClick { add => _cellDoubleClick += value; remove => _cellDoubleClick -= value; }
        /// <summary>Raised when a cell value changes.</summary>
        public new event EventHandler<GridViewCellEventArgs>? CellValueChanged { add => _cellValueChanged += value; remove => _cellValueChanged -= value; }
        /// <summary>Raised when a cell value changes (Telerik alias).</summary>
        public event EventHandler<GridViewCellEventArgs>? ValueChanged { add => _valueChanged += value; remove => _valueChanged -= value; }
        /// <summary>Raised when a cell edit completes.</summary>
        public new event EventHandler<GridViewCellEventArgs>? CellEndEdit { add => _cellEndEdit += value; remove => _cellEndEdit -= value; }
        /// <summary>Raised before a cell enters edit mode.</summary>
        public new event EventHandler<GridViewCellCancelEventArgs>? CellBeginEdit { add => _cellBeginEdit += value; remove => _cellBeginEdit -= value; }
        /// <summary>Raised when the selection changes.</summary>
        public new event EventHandler? SelectionChanged { add => _selectionChanged += value; remove => _selectionChanged -= value; }
        /// <summary>Raised when the current row changes (fires alongside <see cref="SelectionChanged"/>).</summary>
        public event EventHandler? CurrentRowChanged { add => _currentRowChanged += value; remove => _currentRowChanged -= value; }

        private EventHandler<GridViewCellFormattingEventArgs>? _cellFormatting, _viewCellFormatting;
        private EventHandler<GridViewRowFormattingEventArgs>? _rowFormatting;

        /// <summary>Raised when a cell is being formatted. Set <c>e.CellElement.BackColor</c>/<c>ForeColor</c> to color the cell.</summary>
        public new event EventHandler<GridViewCellFormattingEventArgs>? CellFormatting { add => _cellFormatting += value; remove => _cellFormatting -= value; }
        /// <summary>Raised when a view cell is being formatted (alias for <see cref="CellFormatting"/>).</summary>
        public event EventHandler<GridViewCellFormattingEventArgs>? ViewCellFormatting { add => _viewCellFormatting += value; remove => _viewCellFormatting -= value; }
        /// <summary>Raised when a row is being formatted. Set <c>e.RowElement.BackColor</c> (with <c>DrawFill=true</c>) to color the row.</summary>
        public event EventHandler<GridViewRowFormattingEventArgs>? RowFormatting { add => _rowFormatting += value; remove => _rowFormatting -= value; }
        private EventHandler<ContextMenuOpeningEventArgs>? _contextMenuOpening;

        /// <summary>
        /// Raised on right-click before the context menu is shown. Handlers populate
        /// <c>e.ContextMenu.Items</c> (with <see cref="RadMenuItem"/>s); the populated menu is then shown.
        /// </summary>
        public event EventHandler<ContextMenuOpeningEventArgs>? ContextMenuOpening { add => _contextMenuOpening += value; remove => _contextMenuOpening -= value; }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            // Telerik raises ContextMenuOpening on right-click so handlers can build the menu; show it.
            if (e.Button == MouseButtons.Right && _contextMenuOpening is not null) {
                var args = new ContextMenuOpeningEventArgs { RowElement = RowElementUnder (e.Location) };
                _contextMenuOpening.Invoke (this, args);

                if (args.ContextMenu.Items.Count > 0) {
                    var menu = new ContextMenu ();
                    foreach (var item in args.ContextMenu.Items)
                        if (item is MenuItem menuItem)
                            menu.Items.Add (menuItem);

                    if (menu.Items.Count > 0) {
                        menu.Show (this, PointToScreen (e.Location));
                        return;
                    }
                }
            }

            base.OnClick (e);
        }

        // Best-effort hit-test: returns a row element for the row whose cell contains the point, or null.
        private GridViewRowElement? RowElementUnder (Point location)
        {
            foreach (var row in base.Rows)
                foreach (DataGridViewCell cell in row.Cells)
                    if (cell.Bounds.Contains (location))
                        return new GridViewRowElement { RowInfo = new GridViewRowInfo (row) };

            return null;
        }

        /// <inheritdoc/>
        protected internal override void RaiseRowFormatting (DataGridViewRow row, int rowIndex)
        {
            // Clear the per-cell colors and text overrides we manage so conditional formatting doesn't
            // leave stale values from a previous frame; alternating-row striping (drawn at row level)
            // still shows through.
            foreach (DataGridViewCell cell in row.Cells) {
                cell.Style.BackgroundColor = null;
                cell.Style.ForegroundColor = null;
                cell.FormattedTextOverride = null;
            }

            if (_rowFormatting is null)
                return;

            var element = new GridViewRowElement { RowInfo = new GridViewRowInfo (row) };
            _rowFormatting.Invoke (this, new GridViewRowFormattingEventArgs { RowElement = element, Row = element.RowInfo });

            if (element.BackColor != Color.Empty) {
                var sk = ToSK (element.BackColor);
                foreach (DataGridViewCell cell in row.Cells)
                    cell.Style.BackgroundColor = sk;
            }

            if (element.ForeColor != Color.Empty) {
                var sk = ToSK (element.ForeColor);
                foreach (DataGridViewCell cell in row.Cells)
                    cell.Style.ForegroundColor = sk;
            }
        }

        /// <inheritdoc/>
        protected internal override void RaiseCellFormatting (DataGridViewRow row, int rowIndex, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= row.Cells.Count)
                return;

            var cell = row.Cells[columnIndex];
            var column = columnIndex < base.Columns.Count ? base.Columns[columnIndex] : null;

            var originalText = cell.Value?.ToString () ?? string.Empty;
            var displayText = ComputeFormattedText (cell, column);

            // Raise the Telerik formatting events (handlers may further change Text / colors).
            if (_cellFormatting is not null || _viewCellFormatting is not null) {
                var element = new GridViewCellElement {
                    Value = cell.Value,
                    Text = displayText,
                    RowIndex = rowIndex,
                    ColumnInfo = column,
                    RowInfo = new GridViewRowInfo (row)
                };

                var args = new GridViewCellFormattingEventArgs {
                    CellElement = element, RowIndex = rowIndex, ColumnIndex = columnIndex,
                    Value = cell.Value, Row = element.RowInfo, Column = column
                };

                _cellFormatting?.Invoke (this, args);
                _viewCellFormatting?.Invoke (this, args);

                displayText = element.Text;

                // Cell formatting overrides any row-level color for this specific cell.
                if (element.BackColor != Color.Empty)
                    cell.Style.BackgroundColor = ToSK (element.BackColor);
                if (element.ForeColor != Color.Empty)
                    cell.Style.ForegroundColor = ToSK (element.ForeColor);
            }

            // Only override the displayed text when formatting actually changed it.
            if (!string.Equals (displayText, originalText, StringComparison.Ordinal))
                cell.FormattedTextOverride = displayText;
        }

        // Computes a cell's display text: a combo column resolves the stored value to its display text
        // (foreign-key → name); otherwise the column's FormatString is applied; else the raw value.
        private static string ComputeFormattedText (DataGridViewCell cell, DataGridViewColumn? column)
        {
            if (column is GridViewComboBoxColumn combo && combo.LookupDisplay (cell.Value) is string comboDisplay)
                return comboDisplay;
            if (column is GridViewColumn gvc && !string.IsNullOrEmpty (gvc.FormatString) && cell.Value is not null)
                return FormatValue (gvc.FormatString, cell.Value);
            return cell.Value?.ToString () ?? string.Empty;
        }

        // Applies a Telerik/.NET format string, supporting both composite ("{0:C2}") and bare ("C2") forms.
        private static string FormatValue (string format, object value)
        {
            try {
                return format.Contains ("{0")
                    ? string.Format (format, value)
                    : string.Format ("{0:" + format + "}", value);
            } catch {
                return value?.ToString () ?? string.Empty;
            }
        }

        private static SKColor ToSK (Color c) => new SKColor (c.R, c.G, c.B, c.A);

        // Builds a Telerik cell-event-args from row/column indices, reading the underlying grid.
        private GridViewCellEventArgs BuildCellArgs (int columnIndex, int rowIndex)
        {
            object? value = null;
            var rowInfo = RowAt (rowIndex);

            if (rowInfo is not null && columnIndex >= 0 && columnIndex < rowInfo.DataRow.Cells.Count)
                value = rowInfo.DataRow.Cells[columnIndex].Value;

            return new GridViewCellEventArgs {
                ColumnIndex = columnIndex,
                RowIndex = rowIndex,
                Value = value,
                Row = rowInfo,
                Column = columnIndex >= 0 && columnIndex < base.Columns.Count ? base.Columns[columnIndex] : null
            };
        }

        // Wraps the base row at the given index, or null if out of range.
        private GridViewRowInfo? RowAt (int rowIndex)
            => rowIndex >= 0 && rowIndex < base.Rows.Count ? new GridViewRowInfo (base.Rows[rowIndex]) : null;
    }

    /// <summary>
    /// Telerik-compat grid configuration façade. In Telerik this drives columns/rows/grouping; here
    /// the column/data members forward to the owning <see cref="RadGridView"/> and the rest are stubs.
    /// </summary>
    public class GridViewTemplate
    {
        private readonly RadGridView _grid;

        internal GridViewTemplate (RadGridView grid) => _grid = grid;

        /// <summary>Gets the columns of the grid.</summary>
        public DataGridViewColumnCollection Columns => _grid.Columns;
        /// <summary>Gets or sets the view definition (assigning a <see cref="TableViewDefinition"/> is a no-op).</summary>
        public object? ViewDefinition { get; set; }
        /// <summary>Gets or sets whether a new-row entry is shown.</summary>
        public bool AllowAddNewRow { get; set; } = true;
        /// <summary>Gets or sets whether rows can be deleted.</summary>
        public bool AllowDeleteRow { get; set; } = true;
        /// <summary>Gets or sets whether rows can be edited.</summary>
        public bool AllowEditRow { get; set; } = true;
        /// <summary>Gets or sets whether columns can be reordered.</summary>
        public bool AllowColumnReorder { get; set; } = true;
        /// <summary>Gets or sets whether the column chooser is allowed.</summary>
        public bool AllowColumnChooser { get; set; }
        /// <summary>Gets or sets whether filtering is enabled.</summary>
        public bool EnableFiltering { get; set; }
        /// <summary>Gets or sets whether sorting is enabled.</summary>
        public bool EnableSorting { get; set; } = true;
        /// <summary>Gets or sets whether grouped data auto-expands.</summary>
        public bool AutoExpandGroups { get; set; }
        /// <summary>Gets or sets whether multiple rows can be selected.</summary>
        public bool MultiSelect { get; set; }
        /// <summary>Gets or sets whether the grid is read-only.</summary>
        public bool ReadOnly { get; set; }
        /// <summary>Gets or sets the auto-size columns mode. Stub.</summary>
        public object? AutoSizeColumnsMode { get; set; }
        /// <summary>Gets or sets whether paging is enabled.</summary>
        public bool EnablePaging { get; set; }
        /// <summary>Gets or sets the page size.</summary>
        public int PageSize { get; set; }
        /// <summary>Gets the sort descriptors. Stub list.</summary>
        public List<object> SortDescriptors { get; } = new ();
        /// <summary>Gets the group descriptors. Stub list.</summary>
        public List<object> GroupDescriptors { get; } = new ();
        /// <summary>Gets the summary rows shown at the bottom. Stub list.</summary>
        public List<object> SummaryRowsBottom { get; } = new ();
        /// <summary>Gets the summary rows shown at the top. Stub list.</summary>
        public List<object> SummaryRowsTop { get; } = new ();

        /// <summary>Auto-sizes columns. Stub.</summary>
        public void BestFitColumns () => _grid.BestFitColumns ();
        /// <summary>Refreshes the grid.</summary>
        public void Refresh () => _grid.Invalidate ();
        /// <summary>Expands all groups. Stub.</summary>
        public void ExpandAllGroups () { }
        /// <summary>Collapses all groups. Stub.</summary>
        public void CollapseAllGroups () { }
    }

    /// <summary>Telerik-compat view definition. Assignable to <see cref="GridViewTemplate.ViewDefinition"/> as a no-op.</summary>
    public class TableViewDefinition { }

    // ── Column types ──────────────────────────────────────────────────────────

    /// <summary>Base Telerik-compat grid column. Adds the Telerik column member names on top of <see cref="DataGridViewColumn"/>.</summary>
    public class GridViewColumn : DataGridViewColumn
    {
        /// <summary>Gets or sets whether the column is visible (Telerik alias for <see cref="DataGridViewColumn.Visible"/>).</summary>
        public bool IsVisible {
            get => Visible;
            set => Visible = value;
        }
        /// <summary>Gets or sets the bound field name (Telerik alias for <see cref="DataGridViewColumn.DataPropertyName"/>).</summary>
        public string FieldName {
            get => DataPropertyName;
            set => DataPropertyName = value;
        }
        /// <summary>Gets or sets the column name used by data binding (Telerik alias for <see cref="DataGridViewColumn.Name"/>).</summary>
        public string ColumnName {
            get => Name;
            set => Name = value;
        }
        /// <summary>Gets or sets the .NET format string applied to values. Stub.</summary>
        public string FormatString { get; set; } = string.Empty;
        /// <summary>Gets or sets the cell text alignment (forwards to the rendered cell alignment).</summary>
        public ContentAlignment TextAlignment {
            get => DefaultCellStyleAlignment;
            set => DefaultCellStyleAlignment = value;
        }
        /// <summary>Gets or sets the header text alignment (forwards to the rendered header alignment).</summary>
        public ContentAlignment HeaderTextAlignment {
            get => HeaderAlignment;
            set => HeaderAlignment = value;
        }
        /// <summary>Gets or sets whether the column appears in the column chooser. Stub.</summary>
        public bool VisibleInColumnChooser { get; set; } = true;
        /// <summary>Gets or sets the pinned position. Stub.</summary>
        public object? PinPosition { get; set; }
        /// <summary>Gets or sets whether text wraps. Stub.</summary>
        public bool WrapText { get; set; }
        /// <summary>Gets or sets whether reordering is allowed. Stub.</summary>
        public bool AllowReorder { get; set; } = true;
        /// <summary>Gets or sets whether filtering is allowed. Stub.</summary>
        public bool AllowFiltering { get; set; } = true;
        /// <summary>Gets or sets whether this column can be sorted (forwards to the base sortable flag).</summary>
        public bool AllowSort {
            get => Sortable;
            set => Sortable = value;
        }
        /// <summary>Gets or sets the column data type. Stub.</summary>
        public Type? DataType { get; set; }
        /// <summary>Gets the conditional-formatting object list. Stub.</summary>
        public List<object> ConditionalFormattingObjectList { get; } = new ();

        /// <summary>Auto-sizes this column to its content. Stub.</summary>
        public void BestFit () { }
        /// <summary>Sets the display ordinal. Stub maps to <see cref="DataGridViewColumn.DisplayIndex"/>.</summary>
        public void SetOrdinal (int ordinal) => DisplayIndex = ordinal;
    }

    /// <summary>Telerik-compat data column base.</summary>
    public class GridViewDataColumn : GridViewColumn { }

    /// <summary>Telerik-compat text column.</summary>
    public class GridViewTextBoxColumn : GridViewDataColumn
    {
        /// <summary>Initializes a new instance.</summary>
        public GridViewTextBoxColumn () { }
        /// <summary>Initializes a new instance bound to the specified field.</summary>
        public GridViewTextBoxColumn (string fieldName) { FieldName = fieldName; Name = fieldName; }
        /// <summary>Gets or sets the maximum input length. Stub.</summary>
        public int MaxInputLength { get; set; }
    }

    /// <summary>Telerik-compat combo-box column.</summary>
    public class GridViewComboBoxColumn : GridViewDataColumn
    {
        /// <summary>Initializes a new instance.</summary>
        public GridViewComboBoxColumn () { }
        /// <summary>Initializes a new instance bound to the specified field.</summary>
        public GridViewComboBoxColumn (string fieldName) { FieldName = fieldName; Name = fieldName; }
        /// <summary>Gets or sets the data source for the drop-down.</summary>
        public object? DataSource { get; set; }
        /// <summary>Gets or sets the display member.</summary>
        public string DisplayMember { get; set; } = string.Empty;
        /// <summary>Gets or sets the value member.</summary>
        public string ValueMember { get; set; } = string.Empty;
        /// <summary>Gets or sets whether the column was auto-generated. Stub.</summary>
        public bool IsAutoGenerated { get; set; }

        private object? _cachedSource;
        private Dictionary<string, string>? _lookup;

        /// <summary>
        /// Resolves a stored cell value to its display text using <see cref="DataSource"/>,
        /// <see cref="ValueMember"/> and <see cref="DisplayMember"/> (the foreign-key-to-name lookup a
        /// Telerik combo column performs). Returns null if no mapping applies. Result is cached per
        /// DataSource reference.
        /// </summary>
        internal string? LookupDisplay (object? value)
        {
            if (value is null || DataSource is null || string.IsNullOrEmpty (ValueMember) || string.IsNullOrEmpty (DisplayMember))
                return null;

            if (!ReferenceEquals (_cachedSource, DataSource) || _lookup is null) {
                _lookup = BuildLookup ();
                _cachedSource = DataSource;
            }

            return _lookup is not null && _lookup.TryGetValue (value.ToString () ?? string.Empty, out var display) ? display : null;
        }

        private Dictionary<string, string>? BuildLookup ()
        {
            var items = AsEnumerable (DataSource);
            if (items is null)
                return null;

            var map = new Dictionary<string, string> ();

            foreach (var item in items) {
                if (item is null)
                    continue;

                var key = GetMemberValue (item, ValueMember)?.ToString ();
                var display = GetMemberValue (item, DisplayMember)?.ToString ();

                if (key is not null && display is not null)
                    map[key] = display;
            }

            return map;
        }

        // Normalizes the common DataSource shapes (DataTable / DataView / IListSource / IEnumerable) to items.
        private static IEnumerable? AsEnumerable (object? source) => source switch {
            System.Data.DataTable t => t.Rows.Cast<System.Data.DataRow> (),
            System.Data.DataView v => v.Cast<System.Data.DataRowView> (),
            System.ComponentModel.IListSource ls => ls.GetList (),
            IEnumerable e => e,
            _ => null
        };

        // Reads a named member, supporting DataRow/DataRowView columns and CLR properties.
        private static object? GetMemberValue (object item, string member)
        {
            try {
                switch (item) {
                    case System.Data.DataRowView drv:
                        return drv.Row.Table.Columns.Contains (member) ? drv[member] : null;
                    case System.Data.DataRow dr:
                        return dr.Table.Columns.Contains (member) ? dr[member] : null;
                    default:
                        return item.GetType ().GetProperty (member)?.GetValue (item);
                }
            } catch {
                return null;
            }
        }
    }

    /// <summary>Telerik-compat check-box column.</summary>
    public class GridViewCheckBoxColumn : GridViewDataColumn
    {
        /// <summary>Initializes a new instance.</summary>
        public GridViewCheckBoxColumn () { }
        /// <summary>Initializes a new instance bound to the specified field.</summary>
        public GridViewCheckBoxColumn (string fieldName) { FieldName = fieldName; Name = fieldName; }

        /// <inheritdoc/>
        protected internal override bool DisplaysAsCheckBox => true;
    }

    /// <summary>Telerik-compat decimal column.</summary>
    public class GridViewDecimalColumn : GridViewDataColumn
    {
        /// <summary>Initializes a new instance.</summary>
        public GridViewDecimalColumn () { }
        /// <summary>Initializes a new instance bound to the specified field.</summary>
        public GridViewDecimalColumn (string fieldName) { FieldName = fieldName; Name = fieldName; }
        /// <summary>Gets or sets the number of decimal places. Stub.</summary>
        public int DecimalPlaces { get; set; } = 2;
        /// <summary>Gets or sets the maximum value. Stub.</summary>
        public decimal Maximum { get; set; } = decimal.MaxValue;
        /// <summary>Gets or sets the minimum value. Stub.</summary>
        public decimal Minimum { get; set; } = decimal.MinValue;
    }

    /// <summary>Telerik-compat date/time column.</summary>
    public class GridViewDateTimeColumn : GridViewDataColumn
    {
        /// <summary>Initializes a new instance.</summary>
        public GridViewDateTimeColumn () { }
        /// <summary>Initializes a new instance bound to the specified field.</summary>
        public GridViewDateTimeColumn (string fieldName) { FieldName = fieldName; Name = fieldName; }
    }

    // ── Row / cell wrappers ────────────────────────────────────────────────────

    /// <summary>
    /// Telerik-compat row collection over the underlying <see cref="DataGridViewRowCollection"/>.
    /// The indexer and enumerator yield <see cref="GridViewRowInfo"/>, while Count/Add/Clear/Remove
    /// forward to the real grid rows.
    /// </summary>
    public class GridViewRowInfoCollection : IEnumerable<GridViewRowInfo>
    {
        private readonly DataGridViewRowCollection _rows;

        internal GridViewRowInfoCollection (DataGridViewRowCollection rows) => _rows = rows;

        /// <summary>Gets the number of rows.</summary>
        public int Count => _rows.Count;

        /// <summary>Gets the row at the specified index as a <see cref="GridViewRowInfo"/>.</summary>
        public GridViewRowInfo this[int index] => new GridViewRowInfo (_rows[index]);

        /// <summary>Adds a row with the specified cell values.</summary>
        public void Add (params object[] values) => _rows.Add (values);

        /// <summary>Adds the specified row.</summary>
        public void Add (DataGridViewRow row) => _rows.Add (row);

        /// <summary>Removes the specified row.</summary>
        public void Remove (GridViewRowInfo row) => _rows.Remove (row.DataRow);

        /// <summary>Removes the row at the specified index.</summary>
        public void RemoveAt (int index) => _rows.RemoveAt (index);

        /// <summary>Removes all rows.</summary>
        public void Clear () => _rows.Clear ();

        /// <inheritdoc/>
        public IEnumerator<GridViewRowInfo> GetEnumerator ()
        {
            foreach (var row in _rows)
                yield return new GridViewRowInfo (row);
        }

        IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>Telerik-compat row info. Wraps a <see cref="DataGridViewRow"/>.</summary>
    public class GridViewRowInfo
    {
        private readonly DataGridViewRow _row;

        internal GridViewRowInfo (DataGridViewRow row) => _row = row;

        /// <summary>Gets the underlying row.</summary>
        public DataGridViewRow DataRow => _row;
        /// <summary>Gets the cells of the row, accessible by column name or index.</summary>
        public GridViewCellCollection Cells => new GridViewCellCollection (_row);
        /// <summary>Gets the row index.</summary>
        public int Index => _row.Index;
        /// <summary>Gets or sets whether the row is selected.</summary>
        public bool IsSelected {
            get => _row.Selected;
            set => _row.Selected = value;
        }
        /// <summary>Gets or sets whether the row is selected.</summary>
        public bool Selected {
            get => _row.Selected;
            set => _row.Selected = value;
        }
        /// <summary>Gets the bound data item.</summary>
        public object? DataBoundItem => _row.DataBoundItem;
        /// <summary>Gets or sets whether the row is read-only.</summary>
        public bool ReadOnly {
            get => _row.ReadOnly;
            set => _row.ReadOnly = value;
        }
        /// <summary>Gets whether this is the new-row placeholder.</summary>
        public bool IsNewRow => _row.IsNewRow;
        /// <summary>Gets or sets the row tag.</summary>
        public object? Tag {
            get => _row.Tag;
            set => _row.Tag = value;
        }

        /// <summary>Deletes this row from the grid.</summary>
        public void Delete () => _row.DataGridView?.Rows.Remove (_row);
    }

    /// <summary>Telerik-compat cell collection over a <see cref="DataGridViewRow"/>.</summary>
    public class GridViewCellCollection
    {
        private readonly DataGridViewRow _row;

        internal GridViewCellCollection (DataGridViewRow row) => _row = row;

        /// <summary>Gets the cell for the specified column name.</summary>
        public GridViewCellInfo this[string columnName] {
            get {
                var cell = _row.Cells[columnName] ?? _row.Cells.Add (null);
                return new GridViewCellInfo (cell);
            }
        }

        /// <summary>Gets the cell at the specified index.</summary>
        public GridViewCellInfo this[int index] => new GridViewCellInfo (_row.Cells[index]);

        /// <summary>Enumerates the cells.</summary>
        public IEnumerator<GridViewCellInfo> GetEnumerator ()
        {
            foreach (DataGridViewCell cell in _row.Cells)
                yield return new GridViewCellInfo (cell);
        }
    }

    /// <summary>Telerik-compat cell info. Wraps a <see cref="DataGridViewCell"/>.</summary>
    public class GridViewCellInfo
    {
        private readonly DataGridViewCell _cell;

        internal GridViewCellInfo (DataGridViewCell cell) => _cell = cell;

        /// <summary>Gets or sets the cell value.</summary>
        public object? Value {
            get => _cell.Value;
            set => _cell.Value = value;
        }
        /// <summary>Gets the owning column.</summary>
        public DataGridViewColumn? ColumnInfo => _cell.OwningColumn;
        /// <summary>Gets the cell style (Telerik-compat style object).</summary>
        public RadCellStyle Style { get; } = new RadCellStyle ();
        /// <summary>Gets or sets whether the cell is read-only.</summary>
        public bool ReadOnly {
            get => _cell.ReadOnly;
            set => _cell.ReadOnly = value;
        }
        /// <summary>Gets or sets whether the cell is selected.</summary>
        public bool Selected {
            get => _cell.Selected;
            set => _cell.Selected = value;
        }
    }
}
