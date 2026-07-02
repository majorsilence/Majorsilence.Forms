using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Xml.Linq;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat data grid. Backed by <see cref="Majorsilence.Forms.DataGridView"/>. Provides the most
    /// common RadGridView surface: <see cref="MasterTemplate"/>/<see cref="TableViewDefinition"/>
    /// boilerplate, the GridView* column types, and Telerik-style row/cell access via
    /// <see cref="GridViewRowInfo"/>/<see cref="GridViewCellInfo"/> wrappers over the underlying rows.
    ///
    /// In addition to the data surface, the control implements the interactive end-user features of
    /// RadGridView: in-grid column <b>filtering</b> (funnel popup with a distinct-value checklist and a
    /// condition), header-click <b>sorting</b>, <b>grouping</b> (right-click "Group by", drag a header
    /// into the group panel, expand/collapse groups), <b>drag-to-reorder</b> columns, and
    /// <see cref="SaveLayout(string)"/>/<see cref="LoadLayout(string)"/> persistence to Telerik-shaped XML.
    /// </summary>
    public class RadGridView : DataGridView
    {
        // The full, untransformed set of data rows. base.Rows holds the *display* projection
        // (filtered + sorted + grouped, with injected group-header rows); _master is the source of truth.
        private readonly List<DataGridViewRow> _master = new ();
        // Guards re-entrancy: true while we (not the user) are rewriting base.Rows.
        private bool _applyingView;
        private bool _suspendRebuild;
        // Guards re-entrancy while ApplyAutoSizeColumns rewrites column widths.
        private bool _applyingAutoSize;
        // Group path keys (see BuildGroupKey) that the user has collapsed.
        private readonly HashSet<string> _collapsed = new (StringComparer.Ordinal);
        // Group keys already encountered (so that, when AutoExpandGroups is off, only *new* groups default collapsed).
        private readonly HashSet<string> _seenGroupKeys = new (StringComparer.Ordinal);
        // Master rows (by reference) whose child/detail view is currently expanded.
        private readonly HashSet<DataGridViewRow> _expandedMasters = new (ReferenceEqualityComparer.Instance);

        // ── Interaction state (read by RadGridViewRenderer) ──
        private int _headerDragColumn = -1;
        private Point _dragStart;
        internal bool DragActive { get; private set; }
        internal int DragColumn => _headerDragColumn;
        internal Point DragLocation { get; private set; }
        internal int DragTargetColumn { get; private set; } = -1;
        internal bool DragOverGroupPanel { get; private set; }

        // Inline filter-row editor (one active at a time, hosted over the focused filter cell).
        private TextBox? _filterEditBox;
        private int _filterEditColumn = -1;

        // Layout rectangles published by the renderer for hit-testing.
        internal readonly List<GroupPillLayout> GroupPillLayouts = new ();
        internal readonly Dictionary<int, Rectangle> FilterGlyphRects = new ();

        /// <summary>Logical height of the group panel band shown above the column headers.</summary>
        internal const int GroupPanelLogicalHeight = 34;

        static RadGridView ()
        {
            // RadGridView gets its own renderer (group panel, filter glyphs, group-header rows). The
            // renderer derives from DataGridViewRenderer but re-declares its Type as RadGridView.
            RenderManager.SetRenderer<RadGridView> (new RadGridViewRenderer ());
        }

        /// <summary>Initializes a new instance of the RadGridView class.</summary>
        public RadGridView ()
        {
            MasterTemplate = new MasterGridViewTemplate (this);
            TableElement = new GridTableElement { ViewElement = RootElement };

            // Descriptor changes (add/remove/clear) rebuild the displayed view.
            SortDescriptors.Changed = () => { SyncSortGlyphs (); RebuildView (); };
            GroupDescriptors.Changed = () => { _seenGroupKeys.Clear (); RefreshLayout (); RebuildView (); };
            FilterDescriptors.Changed = RebuildView;
            SummaryRowsTop.Changed = RebuildView;
            SummaryRowsBottom.Changed = RebuildView;
            GroupSummaryItems.Changed = RebuildView;

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
                // ValueChanging first (Telerik's pre-validation hook): a handler may reject the new
                // value outright, same revert-to-old-value behavior as a CellValidating cancel.
                if (RaiseValueChanging (e.ColumnIndex, e.RowIndex))
                    return;

                // Validation next: a handler may reject the new value (revert to the captured old value).
                if (RaiseCellValidating (e.ColumnIndex, e.RowIndex))
                    return;

                // Enforce GridViewDecimalColumn constraints (min/max/decimal places) on the new value.
                NormalizeDecimalCell (e.ColumnIndex, e.RowIndex);
                NormalizeTextCell (e.ColumnIndex, e.RowIndex);

                var args = BuildCellArgs (e.ColumnIndex, e.RowIndex);
                _cellValueChanged?.Invoke (this, args);
                _valueChanged?.Invoke (this, args);

                // A value change in a grouped/filtered/sorted column must re-flow the view.
                if (HasViewTransform)
                    RebuildView ();
            };
            base.CellEndEdit += (_, e) => _cellEndEdit?.Invoke (this, BuildCellArgs (e.ColumnIndex, e.RowIndex));
            base.CellBeginEdit += (_, e) => {
                _editOldValue = CellValueAt (e.RowIndex, e.ColumnIndex);
                _cellBeginEdit?.Invoke (this, new GridViewCellCancelEventArgs { RowIndex = e.RowIndex, ColumnIndex = e.ColumnIndex, Row = RowAt (e.RowIndex) });
            };
            base.SelectionChanged += (_, e) => {
                _selectionChanged?.Invoke (this, e);
                RaiseCurrentRowOrColumnChanged ();
            };
            // Base CellDoubleClick is not raised; derive it from the control's double-click + current cell.
            DoubleClick += (_, _) => {
                var cell = base.CurrentCell;
                if (cell is not null)
                    _cellDoubleClick?.Invoke (this, BuildCellArgs (cell.ColumnIndex, cell.RowIndex));
            };
        }

        /// <summary>Gets the master template (Telerik configuration façade over this grid).</summary>
        public MasterGridViewTemplate MasterTemplate { get; }

        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        /// <summary>Gets the root element of the grid (stub).</summary>
        public RadElement GridViewElement { get; } = new RadElement ();

        /// <summary>Gets the root element of the grid (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets the table (view) element shared by every row of this grid. Passed to <see cref="RowFormatting"/> handlers as <c>e.RowElement.TableElement</c>.</summary>
        public GridTableElement TableElement { get; }

        /// <summary>Gets or sets how the grid is split into panes. No-op stub; setting it invalidates the grid.</summary>
        public RadGridViewSplitMode SplitMode {
            get => _splitMode;
            set { _splitMode = value; Invalidate (); }
        }
        private RadGridViewSplitMode _splitMode = RadGridViewSplitMode.None;

        /// <summary>Gets the current row as a Telerik <see cref="GridViewRowInfo"/>, or null (group-header rows return null).</summary>
        public new GridViewRowInfo? CurrentRow {
            get {
                var row = base.CurrentRow;
                return row is null || IsStructuralRow (row) ? null : new GridViewDataRowInfo (row);
            }
        }

        /// <summary>Gets the data rows, accessible Telerik-style (indexer/enumeration yield <see cref="GridViewRowInfo"/>). Injected group-header rows are not included.</summary>
        public new GridViewRowInfoCollection Rows => new GridViewRowInfoCollection (base.Rows);

        /// <summary>Gets the number of data rows (excludes injected group-header rows).</summary>
        public new int RowCount {
            get {
                var count = 0;
                foreach (var row in base.Rows)
                    if (!IsStructuralRow (row))
                        count++;
                return count;
            }
        }

        // ── Telerik config surface (forwarded to MasterTemplate so grid.X and grid.MasterTemplate.X agree) ──

        /// <summary>Gets or sets whether the "add new row" placeholder is shown.</summary>
        public bool AllowAddNewRow { get => MasterTemplate.AllowAddNewRow; set { MasterTemplate.AllowAddNewRow = value; RebuildView (); Invalidate (); } }
        /// <summary>Gets or sets whether rows can be deleted (via the Delete key).</summary>
        public bool AllowDeleteRow { get => MasterTemplate.AllowDeleteRow; set => MasterTemplate.AllowDeleteRow = value; }
        /// <summary>Gets or sets whether rows can be edited. Setting false makes the grid read-only.</summary>
        public bool AllowEditRow { get => MasterTemplate.AllowEditRow; set { MasterTemplate.AllowEditRow = value; ApplyEditability (); } }

        // Editing is allowed only when not read-only (grid- or template-level) and AllowEditRow is set.
        internal bool CanEditCells => AllowEditRow && !MasterTemplate.ReadOnly && !base.ReadOnly;

        // Reflects the Telerik read-only flags onto the base grid so all edit paths (F2, text, etc.) honor them.
        internal void ApplyEditability () => base.ReadOnly = MasterTemplate.ReadOnly || !MasterTemplate.AllowEditRow;
        /// <summary>Gets or sets whether columns can be reordered by dragging their headers.</summary>
        public bool AllowColumnReorder { get => MasterTemplate.AllowColumnReorder; set => MasterTemplate.AllowColumnReorder = value; }
        /// <summary>Gets or sets whether the column chooser is allowed.</summary>
        public bool AllowColumnChooser { get => MasterTemplate.AllowColumnChooser; set => MasterTemplate.AllowColumnChooser = value; }
        /// <summary>Gets or sets whether multiple rows can be selected.</summary>
        public new bool MultiSelect { get => MasterTemplate.MultiSelect; set { MasterTemplate.MultiSelect = value; base.MultiSelect = value; } }

        /// <summary>Gets or sets whether the in-grid column filtering UI (funnel popups) is enabled.</summary>
        public bool EnableFiltering {
            get => MasterTemplate.EnableFiltering;
            set { MasterTemplate.EnableFiltering = value; Invalidate (); }
        }

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
            ApplyAutoSizeColumns ();
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore (x, y, width, height, specified);
            ApplyAutoSizeColumns ();
        }

        /// <summary>
        /// Sizes visible columns to fill the viewport (by <see cref="DataGridViewColumn.FillWeight"/>) when
        /// <see cref="GridViewTemplate.AutoSizeColumnsMode"/> is <see cref="GridViewAutoSizeColumnsMode.Fill"/>.
        /// Re-entrancy-guarded because setting a column width re-raises the column-changed notification.
        /// </summary>
        internal void ApplyAutoSizeColumns ()
        {
            if (_applyingAutoSize || MasterTemplate is null || MasterTemplate.AutoSizeColumnsMode != GridViewAutoSizeColumnsMode.Fill)
                return;

            var visible = base.Columns.Where (c => c.Visible).ToList ();
            if (visible.Count == 0)
                return;

            var content = GetContentArea ();
            var availableDevice = content.Width - (RowHeadersVisible ? ScaledRowHeadersWidth : 0);
            var available = DeviceToLogicalUnits (availableDevice);
            if (available <= 0)
                return;

            var totalWeight = visible.Sum (c => c.FillWeight <= 0 ? 1f : c.FillWeight);
            if (totalWeight <= 0)
                return;

            _applyingAutoSize = true;
            try {
                var used = 0;
                for (var i = 0; i < visible.Count; i++) {
                    var column = visible[i];
                    int width;
                    if (i == visible.Count - 1) {
                        width = Math.Max (column.MinimumWidth, available - used);
                    } else {
                        var weight = column.FillWeight <= 0 ? 1f : column.FillWeight;
                        width = Math.Max (column.MinimumWidth, (int)(available * (weight / totalWeight)));
                        used += width;
                    }
                    column.Width = width;
                }
            } finally {
                _applyingAutoSize = false;
            }

            Invalidate ();
        }

        // Clamps a GridViewDecimalColumn cell's value to [Minimum, Maximum] and rounds to DecimalPlaces.
        private void NormalizeDecimalCell (int columnIndex, int rowIndex)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count || base.Columns[columnIndex] is not GridViewDecimalColumn dc)
                return;
            if (rowIndex < 0 || rowIndex >= base.Rows.Count)
                return;

            var row = base.Rows[rowIndex];
            if (IsStructuralRow (row) || columnIndex >= row.Cells.Count)
                return;

            var cell = row.Cells[columnIndex];
            if (cell.Value is null)
                return;

            if (!decimal.TryParse (cell.Value.ToString (),
                    NumberStyles.Any | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var value))
                return;

            var clamped = ClampDecimal (dc, value);
            if (clamped != value)
                cell.Value = clamped;
        }

        // Enforces GridViewTextBoxColumn.MaxInputLength by truncating an over-long value.
        private void NormalizeTextCell (int columnIndex, int rowIndex)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count
                || base.Columns[columnIndex] is not GridViewTextBoxColumn tc || tc.MaxInputLength <= 0)
                return;
            if (rowIndex < 0 || rowIndex >= base.Rows.Count)
                return;

            var row = base.Rows[rowIndex];
            if (IsStructuralRow (row) || columnIndex >= row.Cells.Count)
                return;

            var cell = row.Cells[columnIndex];
            if (cell.Value?.ToString () is string text && text.Length > tc.MaxInputLength)
                cell.Value = text.Substring (0, tc.MaxInputLength);
        }

        /// <summary>Clamps a value to the column's [Minimum, Maximum] range and rounds to its DecimalPlaces.</summary>
        internal static decimal ClampDecimal (GridViewDecimalColumn column, decimal value)
        {
            if (value < column.Minimum)
                value = column.Minimum;
            if (value > column.Maximum)
                value = column.Maximum;

            var places = Math.Min (Math.Max (0, column.DecimalPlaces), 28);
            return Math.Round (value, places, MidpointRounding.AwayFromZero);
        }

        /// <summary>Gets or sets whether grouped data auto-expands. Stub retained for compatibility.</summary>
        public bool AutoExpandGroups { get => MasterTemplate.AutoExpandGroups; set => MasterTemplate.AutoExpandGroups = value; }

        private bool _showGroupPanel;
        /// <summary>Gets or sets whether the group panel (drag a column header here to group) is shown.</summary>
        public bool ShowGroupPanel {
            get => _showGroupPanel;
            set {
                if (_showGroupPanel == value)
                    return;
                _showGroupPanel = value;
                RefreshLayout ();
            }
        }

        private bool _enableGrouping;
        /// <summary>Gets or sets whether grouping (group panel, right-click "group by", drag-to-group) is enabled.</summary>
        public bool EnableGrouping {
            get => _enableGrouping;
            set {
                _enableGrouping = value;
                if (!value)
                    ClearGrouping ();
            }
        }

        private int _currentPage;
        /// <summary>Gets or sets whether paging is enabled. When on (with a positive <see cref="PageSize"/>), only the current page is shown.</summary>
        public bool EnablePaging {
            get => MasterTemplate.EnablePaging;
            set { MasterTemplate.EnablePaging = value; _currentPage = 0; RebuildView (); Invalidate (); }
        }
        /// <summary>Gets or sets the number of rows per page.</summary>
        public int PageSize {
            get => MasterTemplate.PageSize;
            set { MasterTemplate.PageSize = Math.Max (0, value); _currentPage = 0; RebuildView (); Invalidate (); }
        }
        /// <summary>Whether paging is currently in effect (enabled, positive page size, and not grouping).</summary>
        internal bool PagingActive => MasterTemplate.EnablePaging && MasterTemplate.PageSize > 0 && GroupDescriptors.Count == 0;
        /// <summary>Gets the number of pages given the current filter/search and page size.</summary>
        public int PageCount => PagingActive ? Math.Max (1, (FilteredMaster ().Count + PageSize - 1) / PageSize) : 1;
        /// <summary>Gets or sets the current (0-based) page index.</summary>
        public int CurrentPageIndex { get => _currentPage; set => GoToPage (value); }
        /// <summary>Navigates to the specified (0-based) page, clamped to the valid range.</summary>
        public void GoToPage (int index)
        {
            var clamped = Math.Max (0, Math.Min (index, PageCount - 1));
            if (clamped == _currentPage)
                return;
            _currentPage = clamped;
            RebuildView ();
            Invalidate ();
        }
        /// <summary>Moves to the next page.</summary>
        public void NextPage () => GoToPage (_currentPage + 1);
        /// <summary>Moves to the previous page.</summary>
        public void PrevPage () => GoToPage (_currentPage - 1);

        private bool _enableAlternatingRowColor;
        /// <summary>Gets or sets whether alternating rows are colored. Defaults to false (Telerik default).</summary>
        public bool EnableAlternatingRowColor {
            get => _enableAlternatingRowColor;
            set { _enableAlternatingRowColor = value; Invalidate (); }
        }

        /// <inheritdoc/>
        protected internal override bool AlternatingRowColorsEnabled => _enableAlternatingRowColor;

        // ── Descriptors (also surfaced on MasterTemplate) ──

        /// <summary>Gets the sort descriptors. Changing the collection re-sorts the view.</summary>
        public GridDescriptorCollection<SortDescriptor> SortDescriptors { get; } = new ();
        /// <summary>Gets the group descriptors. Changing the collection re-groups the view.</summary>
        public GridDescriptorCollection<GroupDescriptor> GroupDescriptors { get; } = new ();
        /// <summary>Gets the filter descriptors. Changing the collection re-filters the view.</summary>
        public GridDescriptorCollection<FilterDescriptor> FilterDescriptors { get; } = new ();
        /// <summary>Gets the summary rows shown above the data (grand totals). Changing the collection rebuilds the view.</summary>
        public GridDescriptorCollection<GridViewSummaryRowItem> SummaryRowsTop { get; } = new ();
        /// <summary>Gets the summary rows shown below the data (grand totals). Changing the collection rebuilds the view.</summary>
        public GridDescriptorCollection<GridViewSummaryRowItem> SummaryRowsBottom { get; } = new ();
        /// <summary>Gets the aggregate items shown as a footer under each (leaf) group when grouping is active.</summary>
        public GridDescriptorCollection<GridViewSummaryItem> GroupSummaryItems { get; } = new ();

        /// <inheritdoc/>
        protected override int ContentTopOffset => ShowGroupPanel ? LogicalToDeviceUnits (GroupPanelLogicalHeight) : 0;

        /// <summary>Logical height of the inline filter row.</summary>
        internal const int FilterRowLogicalHeight = 26;

        private bool _showFilterRow;
        /// <summary>Gets or sets whether the inline filter row (a live per-column quick filter) is shown below the headers.</summary>
        public bool ShowFilterRow {
            get => _showFilterRow;
            set {
                if (_showFilterRow == value)
                    return;
                _showFilterRow = value;
                if (!value)
                    CloseFilterEditor ();
                RefreshLayout ();
            }
        }

        /// <summary>Device-pixel height of the filter-row band (0 when hidden). Used by the renderer.</summary>
        internal int FilterRowBandHeight => ShowFilterRow ? LogicalToDeviceUnits (FilterRowLogicalHeight) : 0;

        /// <inheritdoc/>
        protected internal override int HeaderExtraHeight => FilterRowBandHeight;

        // ── Master-detail ──

        private Func<DataGridViewRow, GridChildView?>? _childViewProvider;
        /// <summary>
        /// Gets or sets the provider that supplies a child (detail) view for a master row, enabling
        /// master-detail. When set, each data row shows an expander; expanding injects a drawn child table.
        /// Return null for rows that have no detail.
        /// </summary>
        public Func<DataGridViewRow, GridChildView?>? ChildViewProvider {
            get => _childViewProvider;
            set { _childViewProvider = value; RebuildView (); Invalidate (); }
        }

        /// <summary>Whether a child-view provider is configured (master-detail enabled).</summary>
        internal bool HasChildView => _childViewProvider is not null;

        /// <summary>Returns whether the master row's detail view is currently expanded.</summary>
        internal bool IsRowExpanded (DataGridViewRow row) => _expandedMasters.Contains (row);

        private EventHandler<ChildViewExpandingEventArgs>? _childViewExpanding;
        /// <summary>
        /// Raised before a master row's child view expands. Handlers may set <c>e.Cancel</c> to veto, or
        /// lazily populate the data the <see cref="ChildViewProvider"/> will return for this row.
        /// </summary>
        public event EventHandler<ChildViewExpandingEventArgs>? ChildViewExpanding { add => _childViewExpanding += value; remove => _childViewExpanding -= value; }

        /// <summary>Expands the master row's child/detail view (raises <see cref="ChildViewExpanding"/>).</summary>
        public void ExpandRow (DataGridViewRow row)
        {
            if (row is null || _expandedMasters.Contains (row))
                return;

            if (_childViewExpanding is not null) {
                var args = new ChildViewExpandingEventArgs { Row = new GridViewDataRowInfo (row) };
                _childViewExpanding.Invoke (this, args);
                if (args.Cancel)
                    return;
            }

            _expandedMasters.Add (row);
            RebuildView ();
        }

        /// <summary>Collapses the master row's child/detail view.</summary>
        public void CollapseRow (DataGridViewRow row)
        {
            if (row is not null && _expandedMasters.Remove (row))
                RebuildView ();
        }

        private void ToggleMasterRow (DataGridViewRow row)
        {
            if (_expandedMasters.Contains (row))
                CollapseRow (row);
            else
                ExpandRow (row);
        }

        /// <summary>Appends a new, empty data row (the action behind the "add new row" placeholder) and returns it.</summary>
        public GridViewRowInfo? AddNewRow ()
        {
            if (!ShowNewRow)
                return null;

            var row = new DataGridViewRow ();
            foreach (DataGridViewColumn _ in base.Columns)
                row.Cells.Add (string.Empty);

            _master.Add (row);
            RebuildView ();
            Invalidate ();
            return new GridViewDataRowInfo (row);
        }

        private string _searchText = string.Empty;
        /// <summary>
        /// Gets or sets the quick-search text. When non-empty, only rows where some visible column's
        /// display text contains it (case-insensitive) are shown — the Telerik search-row behavior,
        /// combined with any column filters.
        /// </summary>
        public string SearchText {
            get => _searchText;
            set {
                value ??= string.Empty;
                if (_searchText == value)
                    return;
                _searchText = value;
                RebuildView ();
                Invalidate ();
            }
        }

        /// <summary>Sets <see cref="SearchText"/> (Telerik-style search entry point).</summary>
        public void Search (string text) => SearchText = text;

        /// <summary>True when the displayed rows differ from the raw master (any filter, search, sort, group, or summary applied).</summary>
        internal bool HasViewTransform =>
            GroupDescriptors.Count > 0 || SortDescriptors.Count > 0 || FilterDescriptors.Any (f => f.IsActive)
            || SummaryRowsTop.Count > 0 || SummaryRowsBottom.Count > 0 || !string.IsNullOrEmpty (_searchText)
            || _expandedMasters.Count > 0 || ShowNewRow || PagingActive;

        /// <summary>Returns whether the row is an injected group-header row.</summary>
        internal static bool IsGroupRow (DataGridViewRow row) => row.Tag is GridGroupRow;

        /// <summary>Returns whether the row is an injected summary (aggregate) row.</summary>
        internal static bool IsSummaryRow (DataGridViewRow row) => row.Tag is GridSummaryRow;

        /// <summary>Returns whether the row is an injected structural row (group header or summary) rather than data.</summary>
        internal static bool IsStructuralRow (DataGridViewRow row) => row.Tag is GridGroupRow || row.Tag is GridSummaryRow || row.Tag is GridDetailRow || row.Tag is GridNewRow;

        /// <summary>Whether the "add new row" placeholder should be shown (master-detail style, at the end).</summary>
        internal bool ShowNewRow => MasterTemplate is not null && MasterTemplate.AllowAddNewRow && MasterTemplate.AllowEditRow
            && !MasterTemplate.ReadOnly && !base.ReadOnly && base.Columns.Count > 0;

        /// <summary>Returns whether the column currently has an active filter (used by the renderer to highlight the funnel).</summary>
        internal bool ColumnHasActiveFilter (DataGridViewColumn column)
            => FilterDescriptors.Any (f => f.IsActive && NameMatches (f.PropertyName, column));

        /// <summary>Device-pixel height of the group panel band (0 when hidden). Used by the renderer.</summary>
        internal int GroupPanelBandHeight => ShowGroupPanel ? LogicalToDeviceUnits (GroupPanelLogicalHeight) : 0;

        // ── Public grouping / filtering API ──

        /// <summary>Groups the grid by the named column (appended as the innermost group level).</summary>
        public void GroupByColumn (string columnName, ListSortDirection direction = ListSortDirection.Ascending)
        {
            if (string.IsNullOrEmpty (columnName) || GroupDescriptors.Any (g => NameMatches (g.PropertyName, columnName)))
                return;

            // Honor a column opting out of grouping (Telerik GridViewColumn.AllowGroup).
            if (ColumnByName (columnName) is { } column && !ColumnAllowsGrouping (column))
                return;

            GroupDescriptors.Add (new GroupDescriptor (columnName, direction));
        }

        /// <summary>Returns whether the column permits the end user to filter it.</summary>
        internal static bool ColumnAllowsFiltering (DataGridViewColumn column)
            => column is not GridViewColumn g || g.AllowFiltering;

        /// <summary>Returns whether the column permits the end user to group by it.</summary>
        internal static bool ColumnAllowsGrouping (DataGridViewColumn column)
            => column is not GridViewColumn g || g.AllowGroup;

        /// <summary>Returns whether the column permits the end user to reorder it.</summary>
        internal static bool ColumnAllowsReorder (DataGridViewColumn column)
            => column is not GridViewColumn g || g.AllowReorder;

        /// <summary>Removes the grouping for the named column.</summary>
        public void UngroupColumn (string columnName)
        {
            var existing = GroupDescriptors.Find (g => NameMatches (g.PropertyName, columnName));
            if (existing is not null)
                GroupDescriptors.Remove (existing);
        }

        /// <summary>Removes all grouping.</summary>
        public void ClearGrouping ()
        {
            if (GroupDescriptors.Count == 0)
                return;
            _collapsed.Clear ();
            _seenGroupKeys.Clear ();
            GroupDescriptors.Clear ();
        }

        /// <summary>Clears the filter for the named column.</summary>
        public void ClearColumnFilter (string columnName)
        {
            var existing = FilterDescriptors.Find (f => NameMatches (f.PropertyName, columnName));
            if (existing is not null)
                FilterDescriptors.Remove (existing);
        }

        /// <summary>Expands every group.</summary>
        public void ExpandAllGroups ()
        {
            if (_collapsed.Count == 0)
                return;
            _collapsed.Clear ();
            RebuildView ();
        }

        /// <summary>Collapses every group.</summary>
        public void CollapseAllGroups ()
        {
            if (GroupDescriptors.Count == 0)
                return;

            // Build the full (all-expanded) projection to enumerate every group key, then collapse them.
            var all = BuildDisplayRows (respectCollapse: false);
            foreach (var row in all)
                if (row.Tag is GridGroupRow g)
                    _collapsed.Add (g.Key);

            RebuildView ();
        }

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
                        if (IsStructuralRow (row) || columnIndex >= row.Cells.Count)
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

        /// <summary>Begins a batch update. Suspends view rebuilds until <see cref="EndUpdate"/>.</summary>
        public new void BeginUpdate () => _suspendRebuild = true;
        /// <summary>Ends a batch update and rebuilds the view.</summary>
        public new void EndUpdate () { _suspendRebuild = false; RebuildView (); Invalidate (); }

        // ── View pipeline ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        internal override void OnRowsChanged ()
        {
            base.OnRowsChanged ();

            // _master is null only if a base-ctor row change races our field initializers.
            if (_applyingView || _master is null)
                return;

            // The user (or data binding) mutated base.Rows. Recapture the master data rows and re-flow.
            SyncMasterFromDisplay ();

            if (HasViewTransform)
                RebuildView ();
        }

        // Captures the current data display rows (excluding injected structural rows) as the new master.
        private void SyncMasterFromDisplay ()
        {
            _master.Clear ();
            foreach (var row in base.Rows)
                if (!IsStructuralRow (row))
                    _master.Add (row);
        }

        // Rebuilds base.Rows from the master set by applying filters, sorting, grouping, and summary rows.
        internal void RebuildView ()
        {
            if (_suspendRebuild || _master is null)
                return;

            var display = HasViewTransform ? BuildDisplayRows (respectCollapse: true) : new List<DataGridViewRow> (_master);

            // Master-detail: inject a child (detail) row after each expanded master row.
            if (HasChildView && _expandedMasters.Count > 0)
                display = InjectDetailRows (display);

            // Grand summary rows are computed over the filtered data set and bracket the display rows.
            if (SummaryRowsTop.Count > 0 || SummaryRowsBottom.Count > 0) {
                var data = FilteredMaster ();
                var combined = new List<DataGridViewRow> ();
                foreach (var summary in SummaryRowsTop)
                    combined.Add (CreateSummaryRow (summary, data));
                combined.AddRange (display);
                foreach (var summary in SummaryRowsBottom)
                    combined.Add (CreateSummaryRow (summary, data));
                display = combined;
            }

            // "Click here to add a new row" placeholder at the very end.
            if (ShowNewRow)
                display.Add (new DataGridViewRow { Tag = new GridNewRow () });

            _applyingView = true;
            try {
                base.Rows.ReplaceAll (display);
            } finally {
                _applyingView = false;
            }
        }

        // Interleaves a detail row after each expanded master row (single hierarchy level).
        private List<DataGridViewRow> InjectDetailRows (List<DataGridViewRow> display)
        {
            var result = new List<DataGridViewRow> (display.Count);

            foreach (var row in display) {
                result.Add (row);

                if (IsStructuralRow (row) || !_expandedMasters.Contains (row))
                    continue;

                var child = _childViewProvider! (row);
                if (child is null)
                    continue;

                result.Add (new DataGridViewRow {
                    Tag = new GridDetailRow { Child = child },
                    Height = ComputeDetailHeight (child)
                });
            }

            return result;
        }

        // Logical height of a detail row: child header + child rows + padding (bounded).
        private static int ComputeDetailHeight (GridChildView child)
        {
            const int childHeaderHeight = 22, childRowHeight = 22, padding = 10;
            var rows = Math.Max (1, child.Rows.Count);
            return Math.Min (320, childHeaderHeight + rows * childRowHeight + padding);
        }

        // The master data rows after applying any active column filters and the quick-search text.
        private List<DataGridViewRow> FilteredMaster ()
        {
            IEnumerable<DataGridViewRow> rows = _master;

            var activeFilters = FilterDescriptors.Where (f => f.IsActive).ToList ();
            if (activeFilters.Count > 0)
                rows = rows.Where (r => PassesFilters (r, activeFilters));

            if (!string.IsNullOrEmpty (_searchText))
                rows = rows.Where (MatchesSearch);

            return rows.ToList ();
        }

        // True if any visible column's display text contains the quick-search text.
        private bool MatchesSearch (DataGridViewRow row)
        {
            for (var i = 0; i < base.Columns.Count; i++) {
                if (!base.Columns[i].Visible)
                    continue;
                if (GetCellDisplay (row, i).Contains (_searchText, StringComparison.CurrentCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        // Produces the displayed row list: filtered, sorted, with group-header rows interleaved.
        private List<DataGridViewRow> BuildDisplayRows (bool respectCollapse)
        {
            var list = FilteredMaster ();

            // Sort (group keys first so groups are contiguous, then explicit sort descriptors). Stable.
            if (GroupDescriptors.Count > 0 || SortDescriptors.Count > 0)
                list = StableSort (list);

            // Paging: slice to the current page (paging is disabled while grouping is active).
            if (PagingActive) {
                var pages = Math.Max (1, (list.Count + PageSize - 1) / PageSize);
                if (_currentPage >= pages)
                    _currentPage = pages - 1;
                if (_currentPage < 0)
                    _currentPage = 0;
                list = list.Skip (_currentPage * PageSize).Take (PageSize).ToList ();
            }

            if (GroupDescriptors.Count == 0)
                return list;

            var result = new List<DataGridViewRow> ();
            BuildGroupLevel (list, 0, string.Empty, respectCollapse, result);
            return result;
        }

        // Builds a summary (aggregate) row: a structural row carrying per-column computed display text.
        private DataGridViewRow CreateSummaryRow (IEnumerable<GridViewSummaryItem> items, List<DataGridViewRow> data, int level = 0)
        {
            var values = new Dictionary<int, string> ();

            foreach (var item in items) {
                var colIndex = ColumnIndexByName (item.Name);
                if (colIndex < 0)
                    continue;
                values[colIndex] = ComputeAggregate (item, colIndex, data);
            }

            return new DataGridViewRow { Tag = new GridSummaryRow { Values = values, Level = level } };
        }

        // Computes one aggregate over the column's values across the supplied rows, then formats it.
        private string ComputeAggregate (GridViewSummaryItem item, int colIndex, List<DataGridViewRow> data)
        {
            object? result = item.Aggregate switch {
                GridAggregateFunction.Count => data.Count,
                GridAggregateFunction.First => data.Count > 0 ? GetCellDisplay (data[0], colIndex) : string.Empty,
                GridAggregateFunction.Last => data.Count > 0 ? GetCellDisplay (data[^1], colIndex) : string.Empty,
                GridAggregateFunction.Sum => Numbers (data, colIndex).DefaultIfEmpty (0).Sum (),
                GridAggregateFunction.Average => Numbers (data, colIndex) is var n && n.Count > 0 ? n.Average () : (object?)null,
                GridAggregateFunction.Min => Numbers (data, colIndex) is var n2 && n2.Count > 0 ? n2.Min () : (object?)null,
                GridAggregateFunction.Max => Numbers (data, colIndex) is var n3 && n3.Count > 0 ? n3.Max () : (object?)null,
                _ => null
            };

            if (result is null)
                return string.Empty;

            return string.IsNullOrEmpty (item.FormatString) ? result.ToString () ?? string.Empty : FormatValue (item.FormatString, result);
        }

        // Numeric cell values for a column (raw value when numeric, else parsed from display text).
        private List<double> Numbers (List<DataGridViewRow> data, int colIndex)
        {
            var result = new List<double> ();
            foreach (var row in data) {
                if (colIndex >= row.Cells.Count)
                    continue;
                var raw = row.Cells[colIndex].Value;
                if (raw is IConvertible && raw is not string && raw is not bool) {
                    try { result.Add (Convert.ToDouble (raw, System.Globalization.CultureInfo.CurrentCulture)); continue; } catch { }
                }
                if (double.TryParse (GetCellDisplay (row, colIndex),
                        System.Globalization.NumberStyles.Any | System.Globalization.NumberStyles.AllowCurrencySymbol,
                        System.Globalization.CultureInfo.CurrentCulture, out var n))
                    result.Add (n);
            }
            return result;
        }

        private bool PassesFilters (DataGridViewRow row, List<FilterDescriptor> filters)
        {
            foreach (var filter in filters) {
                var idx = ColumnIndexByName (filter.PropertyName);
                if (idx < 0)
                    continue;
                if (!filter.Matches (GetCellDisplay (row, idx)))
                    return false;
            }
            return true;
        }

        private List<DataGridViewRow> StableSort (List<DataGridViewRow> rows)
        {
            var indexed = rows.Select ((r, i) => (r, i)).ToList ();
            indexed.Sort ((a, b) => {
                var cmp = CompareRows (a.r, b.r);
                return cmp != 0 ? cmp : a.i.CompareTo (b.i);
            });
            return indexed.Select (t => t.r).ToList ();
        }

        private int CompareRows (DataGridViewRow a, DataGridViewRow b)
        {
            foreach (var g in GroupDescriptors) {
                var idx = ColumnIndexByName (g.PropertyName);
                if (idx < 0)
                    continue;
                var cmp = FilterDescriptor.Compare (GetCellDisplay (a, idx), GetCellDisplay (b, idx));
                if (g.Direction == ListSortDirection.Descending)
                    cmp = -cmp;
                if (cmp != 0)
                    return cmp;
            }

            foreach (var s in SortDescriptors) {
                var idx = ColumnIndexByName (s.PropertyName);
                if (idx < 0)
                    continue;
                var cmp = FilterDescriptor.Compare (GetCellDisplay (a, idx), GetCellDisplay (b, idx));
                if (s.Direction == ListSortDirection.Descending)
                    cmp = -cmp;
                if (cmp != 0)
                    return cmp;
            }

            return 0;
        }

        // Recursively emits group-header rows and (when expanded) their children.
        private void BuildGroupLevel (List<DataGridViewRow> rows, int level, string parentKey, bool respectCollapse, List<DataGridViewRow> result)
        {
            if (level >= GroupDescriptors.Count) {
                result.AddRange (rows);
                return;
            }

            var descriptor = GroupDescriptors[level];
            var colIndex = ColumnIndexByName (descriptor.PropertyName);
            var header = ColumnHeaderByName (descriptor.PropertyName);

            // rows are pre-sorted by the group keys, so equal values are contiguous.
            var i = 0;
            while (i < rows.Count) {
                var value = GetCellDisplay (rows[i], colIndex);
                var members = new List<DataGridViewRow> ();

                while (i < rows.Count && string.Equals (GetCellDisplay (rows[i], colIndex), value, StringComparison.CurrentCultureIgnoreCase)) {
                    members.Add (rows[i]);
                    i++;
                }

                var key = BuildGroupKey (parentKey, level, value);

                // When AutoExpandGroups is off, a group collapses by default the first time it appears.
                if (respectCollapse && !MasterTemplate.AutoExpandGroups && _seenGroupKeys.Add (key))
                    _collapsed.Add (key);

                var collapsed = respectCollapse && _collapsed.Contains (key);

                var groupRow = new DataGridViewRow {
                    Tag = new GridGroupRow {
                        Field = descriptor.PropertyName,
                        HeaderText = header,
                        Value = value,
                        Count = members.Count,
                        Level = level,
                        Key = key,
                        Collapsed = collapsed
                    }
                };
                result.Add (groupRow);

                if (!collapsed) {
                    BuildGroupLevel (members, level + 1, key, respectCollapse, result);

                    // Group footer: aggregate over the leaf group's rows.
                    if (level == GroupDescriptors.Count - 1 && GroupSummaryItems.Count > 0)
                        result.Add (CreateSummaryRow (GroupSummaryItems, members, level + 1));
                }
            }
        }

        private static string BuildGroupKey (string parentKey, int level, string value)
            => $"{parentKey}/{level}:{value}";

        // ── Name / value helpers ──

        internal int ColumnIndexByName (string name)
        {
            for (var i = 0; i < base.Columns.Count; i++)
                if (NameMatches (name, base.Columns[i]))
                    return i;
            return -1;
        }

        private DataGridViewColumn? ColumnByName (string name)
        {
            var idx = ColumnIndexByName (name);
            return idx >= 0 ? base.Columns[idx] : null;
        }

        private string ColumnHeaderByName (string name)
        {
            var col = ColumnByName (name);
            return col is null ? name : (string.IsNullOrEmpty (col.HeaderText) ? col.Name : col.HeaderText);
        }

        private static bool NameMatches (string name, DataGridViewColumn column)
            => string.Equals (column.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals (column.DataPropertyName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals (column.HeaderText, name, StringComparison.OrdinalIgnoreCase);

        private static bool NameMatches (string a, string b) => string.Equals (a, b, StringComparison.OrdinalIgnoreCase);

        // The display text used for filtering/sorting/grouping (combo FK→name, format strings applied).
        internal string GetCellDisplay (DataGridViewRow row, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= row.Cells.Count)
                return string.Empty;
            var column = columnIndex < base.Columns.Count ? base.Columns[columnIndex] : null;
            return ComputeFormattedText (row.Cells[columnIndex], column);
        }

        // Distinct display values for a column (used to populate the filter popup checklist).
        internal List<string> DistinctValues (int columnIndex)
            => _master.Select (r => GetCellDisplay (r, columnIndex))
                      .Distinct (StringComparer.CurrentCultureIgnoreCase)
                      .OrderBy (v => v, StringComparer.CurrentCultureIgnoreCase)
                      .ToList ();

        private void RefreshLayout ()
        {
            // ContentTopOffset / column set changed: recompute scroll metrics and repaint.
            OnColumnsChanged ();
            RebuildView ();
            Invalidate ();
        }

        // ── Sorting ──

        // Cycles a column's sort: none → ascending → descending → none. When additive (Shift-click) the
        // column joins the existing multi-column sort instead of replacing it.
        private void ToggleSort (int columnIndex, bool additive = false)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count)
                return;

            var column = base.Columns[columnIndex];
            if (!EnableSorting || !column.Sortable)
                return;

            var name = !string.IsNullOrEmpty (column.Name) ? column.Name : column.HeaderText;
            var existing = SortDescriptors.Find (s => NameMatches (s.PropertyName, column));
            var previous = existing?.Direction;

            _suspendRebuild = true;

            if (!additive)
                SortDescriptors.Clear ();
            else if (existing is not null)
                SortDescriptors.Remove (existing);

            if (previous is null)
                SortDescriptors.Add (new SortDescriptor (name, ListSortDirection.Ascending));
            else if (previous == ListSortDirection.Ascending)
                SortDescriptors.Add (new SortDescriptor (name, ListSortDirection.Descending));
            // else: was descending → cycle back to unsorted (leave it removed)

            _suspendRebuild = false;
            SyncSortGlyphs ();
            RebuildView ();
        }

        // Keeps each column's SortOrder (drawn as the header glyph) in step with the sort descriptors.
        private void SyncSortGlyphs ()
        {
            foreach (DataGridViewColumn column in base.Columns)
                column.SortOrder = SortOrder.None;

            foreach (var s in SortDescriptors) {
                var col = ColumnByName (s.PropertyName);
                if (col is not null)
                    col.SortOrder = s.Direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
            }
        }

        // ── Column reorder ──

        private void MoveColumn (int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= base.Columns.Count || to >= base.Columns.Count)
                return;

            var column = base.Columns[from];

            _suspendRebuild = true;
            _applyingView = true;   // suppress master resync while we shuffle cells
            try {
                base.Columns.RemoveAt (from);
                var target = to > from ? to - 1 : to;
                base.Columns.Insert (target, column);

                foreach (var row in _master) {
                    if (from >= row.Cells.Count)
                        continue;
                    var cell = row.Cells[from];
                    row.Cells.RemoveAt (from);
                    row.Cells.Insert (Math.Min (target, row.Cells.Count), cell);
                }
            } finally {
                _applyingView = false;
                _suspendRebuild = false;
            }

            RebuildView ();
            Invalidate ();
        }

        // ── Mouse interaction ──

        private const int DragThreshold = 4;

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            if (Enabled && e.Button == MouseButtons.Left) {
                var content = GetContentArea ();

                // 1) Group panel band (above the header).
                if (ShowGroupPanel && e.Location.Y < content.Top) {
                    HandleGroupPanelMouseDown (e.Location);
                    return;
                }

                // 2) Column header row.
                if (ColumnHeadersVisible) {
                    var headerRect = new Rectangle (content.Left, content.Top, content.Width, ScaledHeaderHeight);

                    if (headerRect.Contains (e.Location)) {
                        // Resize zones still belong to the base grid.
                        if (AllowUserToResizeColumns && NearColumnEdge (e.Location)) {
                            base.OnMouseDown (e);
                            return;
                        }

                        var col = GetColumnAtLocation (e.Location);
                        if (col >= 0) {
                            if (EnableFiltering && FilterGlyphRects.TryGetValue (col, out var glyph) && glyph.Contains (e.Location)) {
                                ShowFilterPopup (col);
                                return;
                            }

                            // Defer: a plain click sorts on mouse-up; a drag groups/reorders.
                            _headerDragColumn = col;
                            _dragStart = e.Location;
                            DragActive = false;
                        }
                        return;
                    }
                }

                // 2b) Filter row band (below the header): open the inline filter editor for the column.
                if (ShowFilterRow) {
                    var bandTop = content.Top + (ColumnHeadersVisible ? ScaledHeaderHeight : 0);
                    if (e.Location.Y >= bandTop && e.Location.Y < bandTop + FilterRowBandHeight) {
                        var fcol = GetColumnAtLocation (e.Location);
                        if (fcol >= 0)
                            OpenFilterEditor (fcol);
                        return;
                    }
                }

                var rowIndex = GetRowAtLocation (e.Location);

                // 2c) Master-detail expander zone (left edge of a data row).
                if (HasChildView && rowIndex >= 0 && rowIndex < base.Rows.Count && !IsStructuralRow (base.Rows[rowIndex])) {
                    var zoneLeft = content.Left + (RowHeadersVisible ? ScaledRowHeadersWidth : 0);
                    if (e.Location.X >= zoneLeft && e.Location.X <= zoneLeft + LogicalToDeviceUnits (18)) {
                        ToggleMasterRow (base.Rows[rowIndex]);
                        return;
                    }
                }

                // 3) Structural row: toggle a group header, add a row from the new-row placeholder, else swallow.
                if (rowIndex >= 0 && rowIndex < base.Rows.Count && IsStructuralRow (base.Rows[rowIndex])) {
                    if (IsGroupRow (base.Rows[rowIndex]))
                        ToggleGroupRow (base.Rows[rowIndex]);
                    else if (base.Rows[rowIndex].Tag is GridNewRow)
                        AddNewRow ();
                    return;
                }
            }

            base.OnMouseDown (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            if (_headerDragColumn >= 0 && e.Button == MouseButtons.Left) {
                if (!DragActive && (Math.Abs (e.Location.X - _dragStart.X) > LogicalToDeviceUnits (DragThreshold)
                                 || Math.Abs (e.Location.Y - _dragStart.Y) > LogicalToDeviceUnits (DragThreshold)))
                    DragActive = true;

                if (DragActive) {
                    DragLocation = e.Location;
                    var content = GetContentArea ();
                    DragOverGroupPanel = ShowGroupPanel && EnableGrouping && e.Location.Y < content.Top;
                    DragTargetColumn = DragOverGroupPanel ? -1 : GetColumnAtLocation (e.Location);
                    Invalidate ();
                    return;
                }
            }

            base.OnMouseMove (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            if (_headerDragColumn >= 0) {
                var col = _headerDragColumn;
                _headerDragColumn = -1;

                if (DragActive) {
                    DragActive = false;

                    if (DragOverGroupPanel) {
                        var column = col < base.Columns.Count ? base.Columns[col] : null;
                        if (column is not null)
                            GroupByColumn (!string.IsNullOrEmpty (column.Name) ? column.Name : column.HeaderText);
                    } else if (AllowColumnReorder && DragTargetColumn >= 0 && DragTargetColumn != col
                               && col < base.Columns.Count && ColumnAllowsReorder (base.Columns[col])) {
                        MoveColumn (col, DragTargetColumn);
                    }

                    DragTargetColumn = -1;
                    DragOverGroupPanel = false;
                    Invalidate ();
                } else {
                    ToggleSort (col, additive: e.Shift);   // plain header click; Shift = multi-column sort
                }
                return;
            }

            base.OnMouseUp (e);
        }

        // True when the point is within a few pixels of a visible column's right edge (resize handle).
        private bool NearColumnEdge (Point location)
        {
            var zone = LogicalToDeviceUnits (4);
            foreach (DataGridViewColumn column in base.Columns) {
                if (!column.Visible || column.HeaderBounds.IsEmpty)
                    continue;
                if (Math.Abs (location.X - column.HeaderBounds.Right) <= zone)
                    return true;
            }
            return false;
        }

        // ── Inline filter row ──

        /// <summary>Returns the current quick-filter (Contains) text shown in a column's filter-row cell.</summary>
        internal string CurrentColumnFilterText (int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count)
                return string.Empty;
            var column = base.Columns[columnIndex];
            var existing = FilterDescriptors.Find (f => f.Operator == FilterOperator.Contains && NameMatches (f.PropertyName, column));
            return existing?.Value?.ToString () ?? string.Empty;
        }

        /// <summary>Index of the column whose filter cell is currently being edited, or -1.</summary>
        internal int FilterEditColumn => _filterEditColumn;

        private void OpenFilterEditor (int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count || FindWindow () is null)
                return;

            CloseFilterEditor ();

            _filterEditColumn = columnIndex;
            _filterEditBox = new TextBox { Text = CurrentColumnFilterText (columnIndex) };
            _filterEditBox.Style.Border.Width = 0;
            _filterEditBox.TextChanged += FilterEditBox_TextChanged;
            _filterEditBox.KeyDown += FilterEditBox_KeyDown;

            Controls.Add (_filterEditBox);
            PositionFilterEditor ();
            _filterEditBox.Select ();
            Invalidate ();
        }

        private void CloseFilterEditor ()
        {
            if (_filterEditBox is null)
                return;

            _filterEditBox.TextChanged -= FilterEditBox_TextChanged;
            _filterEditBox.KeyDown -= FilterEditBox_KeyDown;
            Controls.Remove (_filterEditBox);
            _filterEditBox.Dispose ();
            _filterEditBox = null;
            _filterEditColumn = -1;
            Invalidate ();
        }

        // Positions the editor over its filter cell (re-run on paint so it tracks scrolling/resize).
        private void PositionFilterEditor ()
        {
            if (_filterEditBox is null || _filterEditColumn < 0 || _filterEditColumn >= base.Columns.Count)
                return;

            var content = GetContentArea ();
            var x = GetColumnDeviceLeft (_filterEditColumn);
            var y = content.Top + (ColumnHeadersVisible ? ScaledHeaderHeight : 0);
            var w = LogicalToDeviceUnits (base.Columns[_filterEditColumn].Width);

            _filterEditBox.Left = DeviceToLogicalUnits (x) + 1;
            _filterEditBox.Top = DeviceToLogicalUnits (y) + 1;
            _filterEditBox.Width = Math.Max (1, DeviceToLogicalUnits (w) - 2);
            _filterEditBox.Height = Math.Max (1, DeviceToLogicalUnits (FilterRowBandHeight) - 2);
        }

        private void FilterEditBox_TextChanged (object? sender, EventArgs e)
        {
            if (_filterEditColumn >= 0)
                ApplyFilterRowText (_filterEditColumn, _filterEditBox?.Text ?? string.Empty);
        }

        private void FilterEditBox_KeyDown (object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) {
                CloseFilterEditor ();
                e.Handled = true;
            } else if (e.KeyCode == Keys.Escape) {
                var col = _filterEditColumn;
                CloseFilterEditor ();
                if (col >= 0)
                    ApplyFilterRowText (col, string.Empty);   // clear the column's quick filter
                e.Handled = true;
            }
        }

        // Sets (or clears) a column's Contains filter from the filter-row text, live.
        private void ApplyFilterRowText (int columnIndex, string text)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count)
                return;

            var column = base.Columns[columnIndex];
            var name = !string.IsNullOrEmpty (column.Name) ? column.Name : column.HeaderText;
            var existing = FilterDescriptors.Find (f => f.Operator == FilterOperator.Contains && NameMatches (f.PropertyName, column));

            _suspendRebuild = true;
            if (existing is not null)
                FilterDescriptors.Remove (existing);
            if (!string.IsNullOrEmpty (text))
                FilterDescriptors.Add (new FilterDescriptor (name, FilterOperator.Contains, text));
            _suspendRebuild = false;

            RebuildView ();
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            // Keep the inline filter editor aligned with its cell as the grid scrolls/resizes.
            if (_filterEditBox is not null)
                PositionFilterEditor ();
        }

        private void ToggleGroupRow (DataGridViewRow row)
        {
            if (row.Tag is not GridGroupRow info)
                return;

            if (!_collapsed.Remove (info.Key))
                _collapsed.Add (info.Key);

            RebuildView ();
        }

        private void HandleGroupPanelMouseDown (Point location)
        {
            foreach (var pill in GroupPillLayouts) {
                if (pill.CloseBounds.Contains (location)) {
                    UngroupColumn (pill.Descriptor.PropertyName);
                    return;
                }
                if (pill.Bounds.Contains (location)) {
                    // Toggle this group's direction.
                    pill.Descriptor.Direction = pill.Descriptor.Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending : ListSortDirection.Ascending;
                    RebuildView ();
                    return;
                }
            }
        }

        private void ShowFilterPopup (int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count)
                return;

            var column = base.Columns[columnIndex];
            var name = !string.IsNullOrEmpty (column.Name) ? column.Name : column.HeaderText;
            var current = FilterDescriptors.Find (f => NameMatches (f.PropertyName, column));

            // Anchor under the funnel glyph (fall back to the header's bottom-left).
            var anchor = FilterGlyphRects.TryGetValue (columnIndex, out var glyph)
                ? new Point (glyph.Left, column.HeaderBounds.Bottom)
                : new Point (column.HeaderBounds.Left, column.HeaderBounds.Bottom);

            RadGridFilterPopup.Show (this, name, DistinctValues (columnIndex), current,
                PointToScreen (anchor), descriptor => {
                    if (current is not null)
                        FilterDescriptors.Remove (current);
                    if (descriptor is not null) {
                        descriptor.PropertyName = name;
                        FilterDescriptors.Add (descriptor);
                    } else {
                        RebuildView ();
                    }
                    Invalidate ();
                });
        }

        // ── Layout persistence (Telerik-shaped XML) ──

        /// <summary>Serializes the column layout, sort/group/filter descriptors to a Telerik-shaped XML string.</summary>
        public string SaveLayoutToString ()
        {
            var doc = new XElement ("RadGridView",
                new XElement ("Columns",
                    base.Columns.Select ((c, i) => new XElement ("Column",
                        new XAttribute ("Name", c.Name ?? string.Empty),
                        new XAttribute ("HeaderText", c.HeaderText ?? string.Empty),
                        new XAttribute ("Width", c.Width),
                        new XAttribute ("Index", i),
                        new XAttribute ("IsVisible", c.Visible),
                        new XAttribute ("SortOrder", c.SortOrder)))),
                new XElement ("SortDescriptors",
                    SortDescriptors.Select (s => new XElement ("SortDescriptor",
                        new XAttribute ("PropertyName", s.PropertyName),
                        new XAttribute ("Direction", s.Direction)))),
                new XElement ("GroupDescriptors",
                    GroupDescriptors.Select (g => new XElement ("GroupDescriptor",
                        new XAttribute ("PropertyName", g.PropertyName),
                        new XAttribute ("Direction", g.Direction)))),
                new XElement ("FilterDescriptors",
                    FilterDescriptors.Select (f => new XElement ("FilterDescriptor",
                        new XAttribute ("PropertyName", f.PropertyName),
                        new XAttribute ("Operator", f.Operator),
                        new XAttribute ("Value", f.Value?.ToString () ?? string.Empty),
                        f.SelectedValues is null ? null : new XElement ("SelectedValues",
                            f.SelectedValues.Select (v => new XElement ("Value", v)))))));

            return new XDocument (doc).ToString ();
        }

        /// <summary>Saves the layout (see <see cref="SaveLayoutToString"/>) to the specified file.</summary>
        public void SaveLayout (string fileName) => File.WriteAllText (fileName, SaveLayoutToString ());

        /// <summary>Loads layout XML previously produced by <see cref="SaveLayoutToString"/>.</summary>
        public void LoadLayoutFromString (string xml)
        {
            if (string.IsNullOrWhiteSpace (xml))
                return;

            XElement root;
            try {
                root = XDocument.Parse (xml).Root!;
            } catch {
                return;
            }
            if (root is null)
                return;

            _suspendRebuild = true;
            try {
                // Columns: width / visibility / order.
                var cols = root.Element ("Columns")?.Elements ("Column").ToList () ?? new List<XElement> ();
                foreach (var ce in cols) {
                    var col = ColumnByName ((string?)ce.Attribute ("Name") ?? string.Empty);
                    if (col is null)
                        continue;
                    if (int.TryParse ((string?)ce.Attribute ("Width"), out var w))
                        col.Width = w;
                    if (bool.TryParse ((string?)ce.Attribute ("IsVisible"), out var vis))
                        col.Visible = vis;
                }

                // Re-order columns to match the saved Index order.
                var order = cols.OrderBy (ce => (int?)ce.Attribute ("Index") ?? 0)
                                .Select (ce => (string?)ce.Attribute ("Name") ?? string.Empty)
                                .ToList ();
                ApplyColumnOrder (order);

                // Descriptors.
                SortDescriptors.Clear ();
                foreach (var se in root.Element ("SortDescriptors")?.Elements ("SortDescriptor") ?? [])
                    SortDescriptors.Add (new SortDescriptor (
                        (string?)se.Attribute ("PropertyName") ?? string.Empty,
                        ParseDirection ((string?)se.Attribute ("Direction"))));

                GroupDescriptors.Clear ();
                foreach (var ge in root.Element ("GroupDescriptors")?.Elements ("GroupDescriptor") ?? [])
                    GroupDescriptors.Add (new GroupDescriptor (
                        (string?)ge.Attribute ("PropertyName") ?? string.Empty,
                        ParseDirection ((string?)ge.Attribute ("Direction"))));

                FilterDescriptors.Clear ();
                foreach (var fe in root.Element ("FilterDescriptors")?.Elements ("FilterDescriptor") ?? []) {
                    var descriptor = new FilterDescriptor {
                        PropertyName = (string?)fe.Attribute ("PropertyName") ?? string.Empty,
                        Operator = Enum.TryParse<FilterOperator> ((string?)fe.Attribute ("Operator"), out var op) ? op : FilterOperator.None,
                        Value = (string?)fe.Attribute ("Value")
                    };
                    var values = fe.Element ("SelectedValues");
                    if (values is not null)
                        descriptor.SelectedValues = new HashSet<string> (
                            values.Elements ("Value").Select (v => v.Value), StringComparer.CurrentCultureIgnoreCase);
                    FilterDescriptors.Add (descriptor);
                }
            } finally {
                _suspendRebuild = false;
            }

            SyncSortGlyphs ();
            RefreshLayout ();
        }

        /// <summary>Loads layout XML from the specified file.</summary>
        public void LoadLayout (string fileName)
        {
            if (File.Exists (fileName))
                LoadLayoutFromString (File.ReadAllText (fileName));
        }

        private static ListSortDirection ParseDirection (string? value)
            => string.Equals (value, "Descending", StringComparison.OrdinalIgnoreCase)
                ? ListSortDirection.Descending : ListSortDirection.Ascending;

        private void ApplyColumnOrder (List<string> namesInOrder)
        {
            for (var desired = 0; desired < namesInOrder.Count; desired++) {
                var current = ColumnIndexByName (namesInOrder[desired]);
                if (current >= 0 && current != desired)
                    MoveColumn (current, desired);
            }
        }

        // ── CSV export ──

        /// <summary>
        /// Exports the grid to CSV — visible columns and their formatted display text, in the current
        /// view order (filter/search/sort/group applied; group-header and summary rows excluded).
        /// </summary>
        public string ExportToCsv (bool includeHeaders = true)
        {
            var cols = new List<DataGridViewColumn> ();
            foreach (DataGridViewColumn c in base.Columns)
                if (c.Visible)
                    cols.Add (c);

            var lines = new List<string> ();

            if (includeHeaders)
                lines.Add (string.Join (",", cols.Select (c => CsvEscape (string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText))));

            foreach (var row in base.Rows) {
                if (IsStructuralRow (row))
                    continue;
                lines.Add (string.Join (",", cols.Select (c => CsvEscape (GetCellDisplay (row, c.Index)))));
            }

            return string.Join ("\r\n", lines);
        }

        /// <summary>Exports the grid to a CSV file (see <see cref="ExportToCsv(bool)"/>).</summary>
        public void ExportToCsv (string fileName, bool includeHeaders = true)
            => File.WriteAllText (fileName, ExportToCsv (includeHeaders));

        /// <summary>
        /// Exports the current view to an Open XML (.xlsx) workbook (visible columns, formatted text,
        /// current view order; structural rows excluded). No spreadsheet library is required.
        /// </summary>
        public byte[] ExportToXlsxBytes (string sheetName = "Sheet1", bool includeHeaders = true)
        {
            var cols = new List<DataGridViewColumn> ();
            foreach (DataGridViewColumn c in base.Columns)
                if (c.Visible)
                    cols.Add (c);

            List<string>? headers = null;
            if (includeHeaders) {
                headers = new List<string> ();
                foreach (var c in cols)
                    headers.Add (string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText);
            }

            var rows = new List<IReadOnlyList<RadGridXlsxExport.Cell>> ();
            foreach (var row in base.Rows) {
                if (IsStructuralRow (row))
                    continue;
                var values = new List<RadGridXlsxExport.Cell> (cols.Count);
                foreach (var c in cols) {
                    var raw = c.Index < row.Cells.Count ? row.Cells[c.Index].Value : null;
                    // Numeric values export as real numbers (Excel right-aligns + supports math); others as text.
                    if (raw is not null and not string and not bool && raw is IConvertible) {
                        try {
                            var n = Convert.ToDouble (raw, CultureInfo.InvariantCulture);
                            values.Add (new RadGridXlsxExport.Cell (n.ToString ("R", CultureInfo.InvariantCulture), true));
                            continue;
                        } catch { }
                    }
                    values.Add (new RadGridXlsxExport.Cell (GetCellDisplay (row, c.Index), false));
                }
                rows.Add (values);
            }

            return RadGridXlsxExport.Build (sheetName, headers, rows);
        }

        /// <summary>Exports the current view to an .xlsx file (see <see cref="ExportToXlsxBytes"/>).</summary>
        public void ExportToXlsx (string fileName, string sheetName = "Sheet1", bool includeHeaders = true)
            => File.WriteAllBytes (fileName, ExportToXlsxBytes (sheetName, includeHeaders));

        // Quotes a CSV field when it contains a comma, quote, or line break (doubling embedded quotes).
        private static string CsvEscape (string value)
        {
            value ??= string.Empty;
            if (value.IndexOfAny (anyOf) >= 0)
                return "\"" + value.Replace ("\"", "\"\"") + "\"";
            return value;
        }

        // ── Clipboard copy ──

        /// <summary>Copies the selected rows to the clipboard as tab-separated text (honors <see cref="DataGridView.ClipboardCopyMode"/>).</summary>
        public void CopySelectionToClipboard ()
        {
            var text = BuildClipboardText ();
            if (!string.IsNullOrEmpty (text))
                Clipboard.SetText (text);
        }

        // Builds the tab-separated clipboard text for the selected (non-structural) rows.
        internal string BuildClipboardText ()
        {
            if (ClipboardCopyMode == DataGridViewClipboardCopyMode.Disable)
                return string.Empty;

            var cols = new List<DataGridViewColumn> ();
            foreach (DataGridViewColumn c in base.Columns)
                if (c.Visible)
                    cols.Add (c);

            var selected = new List<DataGridViewRow> ();
            foreach (var r in base.Rows)
                if (!IsStructuralRow (r) && r.Selected)
                    selected.Add (r);

            if (selected.Count == 0)
                return string.Empty;

            var includeHeaders = ClipboardCopyMode != DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            var lines = new List<string> ();

            if (includeHeaders)
                lines.Add (string.Join ("\t", cols.Select (c => TabSanitize (string.IsNullOrEmpty (c.HeaderText) ? c.Name : c.HeaderText))));

            foreach (var r in selected)
                lines.Add (string.Join ("\t", cols.Select (c => TabSanitize (GetCellDisplay (r, c.Index)))));

            return string.Join ("\r\n", lines);
        }

        // Replaces tab/newline characters so a field stays on its own TSV column/row.
        private static string TabSanitize (string value)
            => (value ?? string.Empty).Replace ('\t', ' ').Replace ('\r', ' ').Replace ('\n', ' ');

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C) {
                CopySelectionToClipboard ();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Delete && AllowDeleteRow && !MasterTemplate.ReadOnly) {
                DeleteSelectedRows ();
                e.Handled = true;
                return;
            }

            base.OnKeyDown (e);
        }

        // Removes the selected data rows (skips structural rows). Used by the Delete key.
        private void DeleteSelectedRows ()
        {
            var toRemove = new List<DataGridViewRow> ();
            foreach (var row in base.Rows)
                if (!IsStructuralRow (row) && row.Selected)
                    toRemove.Add (row);

            if (toRemove.Count == 0)
                return;

            // Remove from the master set; OnRowsChanged-driven resync + RebuildView refresh the view.
            _suspendRebuild = true;
            foreach (var row in toRemove)
                _master.Remove (row);
            _suspendRebuild = false;
            RebuildView ();
            Invalidate ();
        }

        // ── Events (forwarded from the base grid; see ctor) ──

        private EventHandler<GridViewCellEventArgs>? _cellClick, _commandCellClick, _cellDoubleClick, _cellValueChanged, _valueChanged, _cellEndEdit;
        private EventHandler<GridViewCellCancelEventArgs>? _cellBeginEdit;
        private EventHandler? _selectionChanged;
        private EventHandler<CurrentRowChangedEventArgs>? _currentRowChanged;
        private EventHandler<CurrentColumnChangedEventArgs>? _currentColumnChanged;
        // Tracked so RaiseCurrentRowOrColumnChanged only fires when the row/column actually changed
        // (not on every SelectionChanged, e.g. a same-row multi-select toggle).
        private int _lastCurrentRowIndex = -1, _lastCurrentColumnIndex = -1;

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

        private EventHandler<GridViewCellValidatingEventArgs>? _cellValidating;
        private EventHandler<ValueChangingEventArgs>? _valueChanging;
        // The value captured when editing began, used to revert a rejected (CellValidating/ValueChanging-cancelled) edit.
        private object? _editOldValue;
        /// <summary>
        /// Raised after a cell value changes. Set <c>e.Cancel</c> to reject the new value (it reverts to the
        /// prior value); set the row's <c>ErrorText</c> in the handler to show an error indicator.
        /// </summary>
        public new event EventHandler<GridViewCellValidatingEventArgs>? CellValidating { add => _cellValidating += value; remove => _cellValidating -= value; }
        /// <summary>
        /// Raised when a cell value is about to change, before <see cref="CellValidating"/>. Set <c>e.Cancel</c>
        /// to reject the new value (it reverts to the prior value).
        /// </summary>
        public event EventHandler<ValueChangingEventArgs>? ValueChanging { add => _valueChanging += value; remove => _valueChanging -= value; }
        /// <summary>Raised when the selection changes.</summary>
        public new event EventHandler? SelectionChanged { add => _selectionChanged += value; remove => _selectionChanged -= value; }
        /// <summary>Raised when the current row actually changes (not on every selection change; see <see cref="SelectionChanged"/> for that).</summary>
        public event EventHandler<CurrentRowChangedEventArgs>? CurrentRowChanged { add => _currentRowChanged += value; remove => _currentRowChanged -= value; }
        /// <summary>Raised when the current column actually changes.</summary>
        public event EventHandler<CurrentColumnChangedEventArgs>? CurrentColumnChanged { add => _currentColumnChanged += value; remove => _currentColumnChanged -= value; }

        // Raises CurrentRowChanged/CurrentColumnChanged when the underlying current cell's row/column index
        // actually moved since the last call (SelectionChanged fires more often, e.g. multi-select toggles).
        private void RaiseCurrentRowOrColumnChanged ()
        {
            var cell = base.CurrentCell;
            var rowIndex = cell?.RowIndex ?? -1;
            var columnIndex = cell?.ColumnIndex ?? -1;

            if (rowIndex != _lastCurrentRowIndex) {
                var oldRow = RowAt (_lastCurrentRowIndex);
                var newRow = RowAt (rowIndex);
                _lastCurrentRowIndex = rowIndex;
                _currentRowChanged?.Invoke (this, new CurrentRowChangedEventArgs (oldRow, newRow));
            }

            if (columnIndex != _lastCurrentColumnIndex) {
                var oldColumn = _lastCurrentColumnIndex >= 0 && _lastCurrentColumnIndex < base.Columns.Count ? base.Columns[_lastCurrentColumnIndex] : null;
                var newColumn = columnIndex >= 0 && columnIndex < base.Columns.Count ? base.Columns[columnIndex] : null;
                _lastCurrentColumnIndex = columnIndex;
                _currentColumnChanged?.Invoke (this, new CurrentColumnChangedEventArgs (oldColumn, newColumn));
            }
        }

        // Raises ValueChanging; if a handler cancels, reverts the cell to the captured old value. Returns true if rejected.
        private bool RaiseValueChanging (int columnIndex, int rowIndex)
        {
            if (_valueChanging is null || rowIndex < 0 || rowIndex >= base.Rows.Count)
                return false;

            var row = base.Rows[rowIndex];
            if (IsStructuralRow (row) || columnIndex < 0 || columnIndex >= row.Cells.Count)
                return false;

            var args = new ValueChangingEventArgs { OldValue = _editOldValue, NewValue = row.Cells[columnIndex].Value };
            _valueChanging.Invoke (this, args);

            if (!args.Cancel)
                return false;

            row.Cells[columnIndex].Value = _editOldValue;   // reject: revert
            Invalidate ();
            return true;
        }

        private EventHandler<GridViewCellFormattingEventArgs>? _cellFormatting, _viewCellFormatting;
        private EventHandler<GridViewRowFormattingEventArgs>? _rowFormatting;

        /// <summary>Raised when a cell is being formatted. Set <c>e.CellElement.BackColor</c>/<c>ForeColor</c> to color the cell.</summary>
        public new event EventHandler<GridViewCellFormattingEventArgs>? CellFormatting { add => _cellFormatting += value; remove => _cellFormatting -= value; }
        /// <summary>Raised when a view cell is being formatted (alias for <see cref="CellFormatting"/>).</summary>
        public event EventHandler<GridViewCellFormattingEventArgs>? ViewCellFormatting { add => _viewCellFormatting += value; remove => _viewCellFormatting -= value; }
        /// <summary>Raised when a row is being formatted. Set <c>e.RowElement.BackColor</c> (with <c>DrawFill=true</c>) to color the row.</summary>
        public event EventHandler<GridViewRowFormattingEventArgs>? RowFormatting { add => _rowFormatting += value; remove => _rowFormatting -= value; }
        private EventHandler<ContextMenuOpeningEventArgs>? _contextMenuOpening;
        private static readonly char[] anyOf = [',', '"', '\r', '\n'];

        /// <summary>
        /// Raised on right-click before the context menu is shown. Handlers populate
        /// <c>e.ContextMenu.Items</c> (with <see cref="RadMenuItem"/>s); the populated menu is then shown.
        /// </summary>
        public event EventHandler<ContextMenuOpeningEventArgs>? ContextMenuOpening { add => _contextMenuOpening += value; remove => _contextMenuOpening -= value; }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) {
                var content = GetContentArea ();
                var onHeader = ColumnHeadersVisible
                    && new Rectangle (content.Left, content.Top, content.Width, ScaledHeaderHeight).Contains (e.Location);

                // Built-in header menu (group / sort / filter / best-fit), unless the consumer drives the menu.
                if (onHeader && _contextMenuOpening is null) {
                    ShowHeaderContextMenu (GetColumnAtLocation (e.Location), e.Location);
                    return;
                }

                // Telerik raises ContextMenuOpening on right-click so handlers can build the menu; show it.
                if (_contextMenuOpening is not null) {
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
            }

            base.OnClick (e);
        }

        private void ShowHeaderContextMenu (int columnIndex, Point location)
        {
            if (columnIndex < 0 || columnIndex >= base.Columns.Count)
                return;

            var column = base.Columns[columnIndex];
            var name = !string.IsNullOrEmpty (column.Name) ? column.Name : column.HeaderText;
            var menu = new ContextMenu ();

            if (EnableSorting && column.Sortable) {
                menu.Items.Add (new MenuItem ("Sort Ascending", (SKBitmap?)null, (_, _) => SetSort (name, ListSortDirection.Ascending)));
                menu.Items.Add (new MenuItem ("Sort Descending", (SKBitmap?)null, (_, _) => SetSort (name, ListSortDirection.Descending)));
                menu.Items.Add (new MenuItem ("Clear Sorting", (SKBitmap?)null, (_, _) => { SortDescriptors.Clear (); SyncSortGlyphs (); }));
            }

            if (EnableGrouping && ColumnAllowsGrouping (column)) {
                var isGrouped = GroupDescriptors.Any (g => NameMatches (g.PropertyName, name));
                menu.Items.Add (new MenuItem (isGrouped ? "Ungroup This Column" : "Group By This Column", (SKBitmap?)null,
                    (_, _) => { if (isGrouped) UngroupColumn (name); else GroupByColumn (name); }));
                if (GroupDescriptors.Count > 0)
                    menu.Items.Add (new MenuItem ("Clear Grouping", (SKBitmap?)null, (_, _) => ClearGrouping ()));
            }

            if (EnableFiltering && ColumnAllowsFiltering (column)) {
                menu.Items.Add (new MenuItem ("Filter…", (SKBitmap?)null, (_, _) => ShowFilterPopup (columnIndex)));
                if (FilterDescriptors.Any (f => NameMatches (f.PropertyName, name)))
                    menu.Items.Add (new MenuItem ("Clear Filter", (SKBitmap?)null, (_, _) => ClearColumnFilter (name)));
            }

            // Pin submenu-style entries (the API exists via PinPosition; expose it in the UI).
            if (column.Frozen || column.PinnedRight)
                menu.Items.Add (new MenuItem ("Unpin Column", (SKBitmap?)null, (_, _) => SetColumnPin (column, PinnedColumnPosition.None)));
            if (!column.Frozen)
                menu.Items.Add (new MenuItem ("Pin Left", (SKBitmap?)null, (_, _) => SetColumnPin (column, PinnedColumnPosition.Left)));
            if (!column.PinnedRight)
                menu.Items.Add (new MenuItem ("Pin Right", (SKBitmap?)null, (_, _) => SetColumnPin (column, PinnedColumnPosition.Right)));

            menu.Items.Add (new MenuItem ("Best Fit Columns", (SKBitmap?)null, (_, _) => BestFitColumns ()));

            if (AllowColumnChooser)
                menu.Items.Add (new MenuItem ("Column Chooser…", (SKBitmap?)null, (_, _) => RadGridColumnChooser.Show (this, PointToScreen (location))));

            if (menu.Items.Count > 0)
                menu.Show (this, PointToScreen (location));
        }

        // Pins/unpins a column (via PinPosition for GridViewColumn, else the base Frozen/PinnedRight flags).
        private void SetColumnPin (DataGridViewColumn column, PinnedColumnPosition position)
        {
            if (column is GridViewColumn gvc) {
                gvc.PinPosition = position;
            } else {
                column.Frozen = position == PinnedColumnPosition.Left;
                column.PinnedRight = position == PinnedColumnPosition.Right;
            }
            OnColumnsChanged ();
            Invalidate ();
        }

        /// <summary>Shows the column chooser popup, letting the user toggle column visibility.</summary>
        public void ShowColumnChooser ()
        {
            if (FindWindow () is null)
                return;
            RadGridColumnChooser.Show (this, PointToScreen (new Point (0, ScaledHeaderHeight)));
        }

        private void SetSort (string name, ListSortDirection direction)
        {
            _suspendRebuild = true;
            SortDescriptors.Clear ();
            SortDescriptors.Add (new SortDescriptor (name, direction));
            _suspendRebuild = false;
            SyncSortGlyphs ();
            RebuildView ();
        }

        // Best-effort hit-test: returns a row element for the row whose cell contains the point, or null.
        private GridViewRowElement? RowElementUnder (Point location)
        {
            foreach (var row in base.Rows) {
                if (IsStructuralRow (row))
                    continue;
                foreach (DataGridViewCell cell in row.Cells)
                    if (cell.Bounds.Contains (location))
                        return new GridViewRowElement { RowInfo = new GridViewDataRowInfo (row) };
            }

            return null;
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            // Don't begin editing on a structural row; toggle a group header, swallow a summary row.
            var rowIndex = GetRowAtLocation (e.Location);
            if (rowIndex >= 0 && rowIndex < base.Rows.Count && IsStructuralRow (base.Rows[rowIndex])) {
                if (IsGroupRow (base.Rows[rowIndex]))
                    ToggleGroupRow (base.Rows[rowIndex]);
                return;
            }

            // Combo / date columns open a dedicated in-place editor instead of the text editor.
            var colIndex = GetColumnAtLocation (e.Location);
            if (rowIndex >= 0 && colIndex >= 0 && colIndex < base.Columns.Count) {
                if (base.Columns[colIndex] is GridViewComboBoxColumn) {
                    ShowComboEditor (rowIndex, colIndex);
                    return;
                }
                if (base.Columns[colIndex] is GridViewDateTimeColumn) {
                    ShowDateEditor (rowIndex, colIndex);
                    return;
                }
            }

            base.OnDoubleClick (e);
        }

        // Opens the in-place calendar editor for a GridViewDateTimeColumn cell.
        private void ShowDateEditor (int rowIndex, int colIndex)
        {
            if (ReadOnly || FindWindow () is null || rowIndex < 0 || rowIndex >= base.Rows.Count)
                return;

            var row = base.Rows[rowIndex];
            if (IsStructuralRow (row) || colIndex >= row.Cells.Count)
                return;

            var cell = row.Cells[colIndex];
            if (cell.ReadOnly)
                return;

            var current = TryGetCellDate (cell.Value, out var dt) ? dt : (DateTime?)null;
            var anchor = new Point (cell.Bounds.Left, cell.Bounds.Bottom);
            RadGridDateEditor.Show (this, current, PointToScreen (anchor), picked => {
                cell.Value = picked;
                OnCellValueChanged (new DataGridViewCellEditEventArgs (rowIndex, colIndex));
                Invalidate ();
            });
        }

        /// <summary>Extracts a <see cref="DateTime"/> from a cell value (already a DateTime, or parseable text).</summary>
        internal static bool TryGetCellDate (object? value, out DateTime date)
        {
            if (value is DateTime dt) {
                date = dt;
                return true;
            }
            return DateTime.TryParse (value?.ToString (), CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
        }

        // Opens the in-place combo drop-down for a GridViewComboBoxColumn cell.
        private void ShowComboEditor (int rowIndex, int colIndex)
        {
            if (ReadOnly || FindWindow () is null)
                return;
            if (colIndex < 0 || colIndex >= base.Columns.Count || base.Columns[colIndex] is not GridViewComboBoxColumn combo)
                return;
            if (rowIndex < 0 || rowIndex >= base.Rows.Count)
                return;

            var row = base.Rows[rowIndex];
            if (IsStructuralRow (row) || colIndex >= row.Cells.Count)
                return;

            var cell = row.Cells[colIndex];
            if (cell.ReadOnly)
                return;

            var items = combo.GetEditorItems ();
            if (items.Count == 0)
                return;

            var anchor = new Point (cell.Bounds.Left, cell.Bounds.Bottom);
            RadGridComboEditor.Show (this, items, combo.LookupDisplay (cell.Value), PointToScreen (anchor), cell.Bounds.Width, picked => {
                cell.Value = picked;
                OnCellValueChanged (new DataGridViewCellEditEventArgs (rowIndex, colIndex));
                Invalidate ();
            });
        }

        /// <inheritdoc/>
        protected internal override void RaiseRowFormatting (DataGridViewRow row, int rowIndex)
        {
            // Structural rows (group headers / summaries) are drawn by the renderer; skip user formatting.
            if (IsStructuralRow (row))
                return;

            // Clear the per-cell colors and text overrides we manage so conditional formatting doesn't
            // leave stale values from a previous frame; alternating-row striping (drawn at row level)
            // still shows through.
            foreach (DataGridViewCell cell in row.Cells) {
                cell.Style.BackgroundColor = null;
                cell.Style.ForegroundColor = null;
                cell.FormattedTextOverride = null;
            }

            // Row-level conditional formatting (rules flagged ApplyToRow).
            ApplyRowConditionalFormatting (row);

            if (_rowFormatting is null)
                return;

            var element = new GridViewRowElement { RowInfo = new GridViewDataRowInfo (row), TableElement = TableElement };
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
            if (IsStructuralRow (row) || columnIndex < 0 || columnIndex >= row.Cells.Count)
                return;

            var cell = row.Cells[columnIndex];
            var column = columnIndex < base.Columns.Count ? base.Columns[columnIndex] : null;

            var originalText = cell.Value?.ToString () ?? string.Empty;
            var displayText = ComputeFormattedText (cell, column);

            // Cell-level conditional formatting (rules not flagged ApplyToRow). User events override below.
            ApplyCellConditionalFormatting (cell, column, displayText);

            // Raise the Telerik formatting events (handlers may further change Text / colors).
            if (_cellFormatting is not null || _viewCellFormatting is not null) {
                // Financial does e.g. TryCast(e.CellElement, GridCommandCellElement) — construct the
                // concrete element type by column kind so that cast succeeds for the columns it applies to.
                GridViewCellElement element = column switch {
                    GridViewCommandColumn or DataGridViewButtonColumn => new GridCommandCellElement (),
                    GridViewDateTimeColumn => new GridDateTimeCellElement (),
                    _ => new GridCellElement ()
                };
                element.Value = cell.Value;
                element.Text = displayText;
                element.RowIndex = rowIndex;
                element.ColumnIndex = columnIndex;
                element.ColumnInfo = column;
                element.RowInfo = new GridViewDataRowInfo (row);

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

        // Applies a column's non-row conditional-formatting rules to a single cell.
        private static void ApplyCellConditionalFormatting (DataGridViewCell cell, DataGridViewColumn? column, string displayText)
        {
            if (column is not GridViewColumn gvc || gvc.ConditionalFormattingObjectList.Count == 0)
                return;

            foreach (var rule in gvc.ConditionalFormattingObjectList) {
                if (rule.ApplyToRow || !rule.Matches (displayText))
                    continue;
                if (rule.CellBackColor != Color.Empty)
                    cell.Style.BackgroundColor = ToSK (rule.CellBackColor);
                if (rule.CellForeColor != Color.Empty)
                    cell.Style.ForegroundColor = ToSK (rule.CellForeColor);
            }
        }

        // Applies every column's ApplyToRow conditional-formatting rules across the whole row.
        private void ApplyRowConditionalFormatting (DataGridViewRow row)
        {
            for (var i = 0; i < base.Columns.Count; i++) {
                if (base.Columns[i] is not GridViewColumn gvc || gvc.ConditionalFormattingObjectList.Count == 0 || i >= row.Cells.Count)
                    continue;

                var text = GetCellDisplay (row, i);

                foreach (var rule in gvc.ConditionalFormattingObjectList) {
                    if (!rule.ApplyToRow || !rule.Matches (text))
                        continue;

                    var back = rule.RowBackColor != Color.Empty ? rule.RowBackColor : rule.CellBackColor;
                    var fore = rule.RowForeColor != Color.Empty ? rule.RowForeColor : rule.CellForeColor;

                    if (back != Color.Empty) {
                        var sk = ToSK (back);
                        foreach (DataGridViewCell c in row.Cells)
                            c.Style.BackgroundColor = sk;
                    }
                    if (fore != Color.Empty) {
                        var sk = ToSK (fore);
                        foreach (DataGridViewCell c in row.Cells)
                            c.Style.ForegroundColor = sk;
                    }
                }
            }
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

        // Wraps the base row at the given index, or null if out of range / a structural row.
        private GridViewDataRowInfo? RowAt (int rowIndex)
            => rowIndex >= 0 && rowIndex < base.Rows.Count && !IsStructuralRow (base.Rows[rowIndex])
                ? new GridViewDataRowInfo (base.Rows[rowIndex]) : null;

        // Reads a cell value by row/column index (null if out of range).
        private object? CellValueAt (int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || rowIndex >= base.Rows.Count)
                return null;
            var row = base.Rows[rowIndex];
            return columnIndex >= 0 && columnIndex < row.Cells.Count ? row.Cells[columnIndex].Value : null;
        }

        // Raises CellValidating; if a handler cancels, reverts the cell to the captured old value. Returns true if rejected.
        private bool RaiseCellValidating (int columnIndex, int rowIndex)
        {
            if (_cellValidating is null || rowIndex < 0 || rowIndex >= base.Rows.Count)
                return false;

            var row = base.Rows[rowIndex];
            if (IsStructuralRow (row) || columnIndex < 0 || columnIndex >= row.Cells.Count)
                return false;

            var args = new GridViewCellValidatingEventArgs {
                RowIndex = rowIndex,
                ColumnIndex = columnIndex,
                Value = row.Cells[columnIndex].Value,
                OldValue = _editOldValue,
                Row = new GridViewDataRowInfo (row),
                Column = columnIndex < base.Columns.Count ? base.Columns[columnIndex] : null
            };
            _cellValidating.Invoke (this, args);

            if (!args.Cancel)
                return false;

            row.Cells[columnIndex].Value = _editOldValue;   // reject: revert
            Invalidate ();
            return true;
        }
    }

    /// <summary>Describes an injected group-header row (stored on <see cref="DataGridViewRow.Tag"/>).</summary>
    internal sealed class GridGroupRow
    {
        public string Field = string.Empty;
        public string HeaderText = string.Empty;
        public string Value = string.Empty;
        public int Count;
        public int Level;
        public string Key = string.Empty;
        public bool Collapsed;
    }

    /// <summary>Describes an injected summary (aggregate) row (stored on <see cref="DataGridViewRow.Tag"/>).</summary>
    internal sealed class GridSummaryRow
    {
        // Display text per column index (columns without a summary item are blank).
        public Dictionary<int, string> Values = new ();
        // Indent level (0 for grand totals; group footers carry their group depth).
        public int Level;
    }

    /// <summary>Describes an injected master-detail child row (stored on <see cref="DataGridViewRow.Tag"/>).</summary>
    internal sealed class GridDetailRow
    {
        public GridChildView Child = null!;
    }

    /// <summary>Marks the injected "click here to add a new row" placeholder (stored on <see cref="DataGridViewRow.Tag"/>).</summary>
    internal sealed class GridNewRow { }

    /// <summary>Layout of a single group-panel "pill", published by the renderer for hit-testing.</summary>
    internal sealed class GroupPillLayout
    {
        public GroupDescriptor Descriptor = null!;
        public Rectangle Bounds;
        public Rectangle CloseBounds;
    }

    /// <summary>
    /// Telerik-compat grid configuration façade. The column/data members forward to the owning
    /// <see cref="RadGridView"/> when attached; Financial's <c>VirtualMasterGridViewTemplate</c> pattern
    /// (inheriting a template class and constructing it standalone, before it's ever attached to a grid)
    /// requires this base to also support a detached instance — see the protected constructor and
    /// <see cref="Attach"/>.
    /// </summary>
    public class GridViewTemplate
    {
        // Not readonly: detached instances (see the protected ctor) attach to a grid later via Attach().
        private RadGridView? _grid;

        // Local backing used only while detached; once attached, the grid's own collections/values are
        // the source of truth (and these become unreachable dead storage, which is fine for a compat shim).
        // Columns has no detached fallback (Columns throws instead, per the compat-layer design) since a
        // standalone DataGridViewColumnCollection needs an owning grid to construct.
        private GridDescriptorCollection<SortDescriptor>? _detachedSort;
        private GridDescriptorCollection<GroupDescriptor>? _detachedGroup;
        private GridDescriptorCollection<FilterDescriptor>? _detachedFilter;
        private GridDescriptorCollection<GridViewSummaryRowItem>? _detachedSummaryBottom;
        private GridDescriptorCollection<GridViewSummaryRowItem>? _detachedSummaryTop;

        internal GridViewTemplate (RadGridView grid) => _grid = grid;

        /// <summary>Initializes a detached template, not yet associated with a <see cref="RadGridView"/>. Used by subclasses constructed standalone (e.g. before being assigned to a grid's designer-generated template).</summary>
        protected GridViewTemplate () { }

        /// <summary>Associates this (previously detached) template with a grid. Subsequent member access forwards to the grid.</summary>
        internal void Attach (RadGridView grid) => _grid = grid;

        /// <summary>Gets the columns of the grid. Throws if the template is not yet attached to a <see cref="RadGridView"/>.</summary>
        public DataGridViewColumnCollection Columns => _grid?.Columns ?? throw new InvalidOperationException ("GridViewTemplate is not attached to a RadGridView");
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
        public bool AutoExpandGroups { get; set; } = true;
        /// <summary>Gets or sets whether multiple rows can be selected.</summary>
        public bool MultiSelect { get; set; }
        private bool _readOnly;
        /// <summary>Gets or sets whether the grid is read-only.</summary>
        public bool ReadOnly { get => _readOnly; set { _readOnly = value; _grid?.ApplyEditability (); } }
        private GridViewAutoSizeColumnsMode _autoSizeColumnsMode = GridViewAutoSizeColumnsMode.None;
        /// <summary>Gets or sets the auto-size columns mode. <see cref="GridViewAutoSizeColumnsMode.Fill"/> sizes visible columns to fill the viewport.</summary>
        public GridViewAutoSizeColumnsMode AutoSizeColumnsMode {
            get => _autoSizeColumnsMode;
            set { _autoSizeColumnsMode = value; _grid?.ApplyAutoSizeColumns (); }
        }
        /// <summary>Gets or sets whether paging is enabled.</summary>
        public bool EnablePaging { get; set; }
        /// <summary>Gets or sets the page size.</summary>
        public int PageSize { get; set; }
        /// <summary>Gets the sort descriptors (the grid's own collection when attached; a standalone one otherwise).</summary>
        public GridDescriptorCollection<SortDescriptor> SortDescriptors => _grid?.SortDescriptors ?? (_detachedSort ??= new ());
        /// <summary>Gets the group descriptors (the grid's own collection when attached; a standalone one otherwise).</summary>
        public GridDescriptorCollection<GroupDescriptor> GroupDescriptors => _grid?.GroupDescriptors ?? (_detachedGroup ??= new ());
        /// <summary>Gets the filter descriptors (the grid's own collection when attached; a standalone one otherwise).</summary>
        public GridDescriptorCollection<FilterDescriptor> FilterDescriptors => _grid?.FilterDescriptors ?? (_detachedFilter ??= new ());
        /// <summary>Gets the summary rows shown below the data (the grid's own collection when attached; a standalone one otherwise).</summary>
        public GridDescriptorCollection<GridViewSummaryRowItem> SummaryRowsBottom => _grid?.SummaryRowsBottom ?? (_detachedSummaryBottom ??= new ());
        /// <summary>Gets the summary rows shown above the data (the grid's own collection when attached; a standalone one otherwise).</summary>
        public GridDescriptorCollection<GridViewSummaryRowItem> SummaryRowsTop => _grid?.SummaryRowsTop ?? (_detachedSummaryTop ??= new ());

        /// <summary>Auto-sizes columns. No-op when detached.</summary>
        public void BestFitColumns () => _grid?.BestFitColumns ();
        /// <summary>Refreshes the grid. No-op when detached.</summary>
        public void Refresh () => _grid?.Invalidate ();
        /// <summary>Expands all groups. No-op when detached.</summary>
        public void ExpandAllGroups () => _grid?.ExpandAllGroups ();
        /// <summary>Collapses all groups. No-op when detached.</summary>
        public void CollapseAllGroups () => _grid?.CollapseAllGroups ();
    }

    /// <summary>
    /// Telerik-compat master (top-level) grid template. Financial subclasses this directly
    /// (<c>VirtualMasterGridViewTemplate : MasterGridViewTemplate</c>), constructing an instance before
    /// it is attached to a <see cref="RadGridView"/> — hence the public parameterless constructor
    /// alongside the internal attached one used by <see cref="RadGridView"/> itself.
    /// </summary>
    public class MasterGridViewTemplate : GridViewTemplate
    {
        /// <summary>Initializes a new, detached instance. Attach it to a grid via the grid's designer-generated template assignment.</summary>
        public MasterGridViewTemplate () : base () { }

        internal MasterGridViewTemplate (RadGridView grid) : base (grid) { }

        /// <summary>Gets or sets whether (and how) the vertical auto-hide scrollbar is shown. Stub.</summary>
        public ScrollState HorizontalScrollState { get; set; } = ScrollState.AlwaysShow;
        /// <summary>Gets or sets whether copy/paste is allowed. Stub.</summary>
        public CopyPasteMode AllowCopyPaste { get; set; } = CopyPasteMode.All;
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
        private PinnedColumnPosition _pinPosition;
        /// <summary>Gets or sets the pinned position. <see cref="PinnedColumnPosition.Left"/> freezes the column to the left; <see cref="PinnedColumnPosition.Right"/> pins it to the right.</summary>
        public PinnedColumnPosition PinPosition {
            get => _pinPosition;
            set {
                _pinPosition = value;
                Frozen = value == PinnedColumnPosition.Left;
                PinnedRight = value == PinnedColumnPosition.Right;
            }
        }
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
        /// <summary>Gets or sets whether this column can be grouped. Stub.</summary>
        public bool AllowGroup { get; set; } = true;
        /// <summary>Gets or sets the column data type. Stub.</summary>
        public Type? DataType { get; set; }
        /// <summary>Gets the conditional-formatting rules applied to this column's cells.</summary>
        public List<ConditionalFormattingObject> ConditionalFormattingObjectList { get; } = new ();

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

        /// <summary>
        /// Returns the (underlying value, display text) pairs from the drop-down's DataSource, used to
        /// populate the in-place combo editor.
        /// </summary>
        internal List<(object? Value, string Display)> GetEditorItems ()
        {
            var result = new List<(object?, string)> ();
            var items = AsEnumerable (DataSource);
            if (items is null || string.IsNullOrEmpty (ValueMember) || string.IsNullOrEmpty (DisplayMember))
                return result;

            foreach (var item in items) {
                if (item is null)
                    continue;
                var value = GetMemberValue (item, ValueMember);
                var display = GetMemberValue (item, DisplayMember)?.ToString ();
                if (display is not null)
                    result.Add ((value, display));
            }

            return result;
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
        // The CLR-property path reflects over the bound item's runtime type, which trimming
        // cannot analyze statically; the consumer is responsible for preserving bound types.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "Data binding reflects over the user-supplied item type, which cannot be statically annotated.")]
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

    /// <summary>Telerik-compat command (button) column. Cells format as a <see cref="GridCommandCellElement"/>.</summary>
    public class GridViewCommandColumn : GridViewDataColumn
    {
        /// <summary>Initializes a new instance.</summary>
        public GridViewCommandColumn () { }
        /// <summary>Initializes a new instance bound to the specified field.</summary>
        public GridViewCommandColumn (string fieldName) { FieldName = fieldName; Name = fieldName; }
        /// <summary>Gets or sets whether the column's default text is used regardless of the bound value. Stub.</summary>
        public bool UseDefaultText { get; set; }
        /// <summary>Gets or sets the default button text shown when <see cref="UseDefaultText"/> is set (or the bound value is empty).</summary>
        public string DefaultText { get; set; } = string.Empty;
    }

    // ── Row / cell wrappers ────────────────────────────────────────────────────

    /// <summary>
    /// Telerik-compat row collection over the underlying <see cref="DataGridViewRowCollection"/>.
    /// The indexer and enumerator yield <see cref="GridViewRowInfo"/> for data rows only (injected
    /// group-header rows are skipped), while Count/Add/Clear/Remove operate on the real grid rows.
    /// </summary>
    public class GridViewRowInfoCollection : IEnumerable<GridViewRowInfo>
    {
        private readonly DataGridViewRowCollection _rows;

        internal GridViewRowInfoCollection (DataGridViewRowCollection rows) => _rows = rows;

        // Underlying data rows (group-header rows excluded), in display order.
        private List<DataGridViewRow> DataRows {
            get {
                var list = new List<DataGridViewRow> ();
                foreach (var row in _rows)
                    if (!RadGridView.IsStructuralRow (row))
                        list.Add (row);
                return list;
            }
        }

        /// <summary>Gets the number of data rows.</summary>
        public int Count => DataRows.Count;

        /// <summary>Gets the data row at the specified index as a <see cref="GridViewRowInfo"/>.</summary>
        public GridViewRowInfo this[int index] => new GridViewDataRowInfo (DataRows[index]);

        /// <summary>Adds a row with the specified cell values.</summary>
        public void Add (params object[] values) => _rows.Add (values);

        /// <summary>Adds the specified row.</summary>
        public void Add (DataGridViewRow row) => _rows.Add (row);

        /// <summary>Removes the specified row.</summary>
        public void Remove (GridViewRowInfo row) => _rows.Remove (row.DataRow);

        /// <summary>Removes the data row at the specified index.</summary>
        public void RemoveAt (int index) => _rows.Remove (DataRows[index]);

        /// <summary>Removes all rows.</summary>
        public void Clear () => _rows.Clear ();

        /// <inheritdoc/>
        public IEnumerator<GridViewRowInfo> GetEnumerator ()
        {
            foreach (var row in DataRows)
                yield return new GridViewDataRowInfo (row);
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

    /// <summary>
    /// Telerik-compat data row info — the concrete type constructed for ordinary (non-group-header) rows,
    /// so Financial's <c>TryCast(row, GridViewDataRowInfo)</c> succeeds.
    /// </summary>
    public class GridViewDataRowInfo : GridViewRowInfo
    {
        internal GridViewDataRowInfo (DataGridViewRow row) : base (row) { }
    }

    /// <summary>
    /// Telerik-compat group-header row info — the concrete type constructed for injected group-header rows.
    /// </summary>
    public class GridViewGroupRowInfo : GridViewRowInfo
    {
        internal GridViewGroupRowInfo (DataGridViewRow row) : base (row) { }

        /// <summary>Gets the group header text (e.g. "Field: Value (Count)").</summary>
        public string HeaderText => DataRow.Tag is GridGroupRow g ? g.HeaderText : string.Empty;
    }

    /// <summary>Telerik-compat cell collection over a <see cref="DataGridViewRow"/>.</summary>
    public class GridViewCellCollection
    {
        private readonly DataGridViewRow _row;

        internal GridViewCellCollection (DataGridViewRow row) => _row = row;

        /// <summary>Gets the cell for the specified column name.</summary>
        public GridViewCellInfo this[string columnName] {
            get {
                var existing = _row.Cells[columnName];
                if (existing is not null)
                    return new GridViewCellInfo (existing);

                // The cell doesn't exist yet. Pad the row with empty cells up to the column's index so
                // cells stay aligned with columns regardless of the order they're first accessed in.
                var dgv = _row.DataGridView;
                var colIndex = -1;
                if (dgv is not null) {
                    for (var i = 0; i < dgv.Columns.Count; i++) {
                        var col = dgv.Columns[i];
                        if (string.Equals (col.Name, columnName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals (col.DataPropertyName, columnName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals (col.HeaderText, columnName, StringComparison.OrdinalIgnoreCase)) {
                            colIndex = i;
                            break;
                        }
                    }
                }

                if (colIndex < 0)
                    colIndex = _row.Cells.Count;

                while (_row.Cells.Count <= colIndex)
                    _row.Cells.Add (string.Empty);

                return new GridViewCellInfo (_row.Cells[colIndex]);
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
