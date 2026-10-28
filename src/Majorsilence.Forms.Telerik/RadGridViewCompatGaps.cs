using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Majorsilence.Forms.Telerik
{
    // ── RadGridView member gaps surfaced migrating the heavily-Telerik TownSuite D10 projects
    //    (LibCM / Lib_TCA / LibBudgeting). Stub-style like the rest of the RadGridView compat: the
    //    values are stored (or forwarded to the master template) so designer/handler code compiles and
    //    runs; the compat grid does not act on them. Split into this companion file to keep RadGridView.cs
    //    manageable (the classes are declared partial there).
    public partial class RadGridView
    {
        /// <summary>Gets or sets whether the grid asks <see cref="CustomSorting"/> to order its rows while it is sorted.</summary>
        public bool EnableCustomSorting { get; set; }

        /// <summary>
        /// Raised, with <see cref="EnableCustomSorting"/> set, for each pair of rows the sort compares. A handler sets
        /// <see cref="GridViewCustomSortingEventArgs.SortResult"/>; one that clears <see cref="GridViewCustomSortingEventArgs.Handled"/>
        /// leaves the pair to the grid's own comparison. Rows that compare equal keep their order.
        /// </summary>
        public event EventHandler<GridViewCustomSortingEventArgs>? CustomSorting;

        // The custom comparison of two rows, or null when the application leaves them to the sort descriptors.
        internal int? CompareCustom (DataGridViewRow a, DataGridViewRow b)
        {
            if (!EnableCustomSorting || CustomSorting is null)
                return null;

            var args = new GridViewCustomSortingEventArgs (new GridViewDataRowInfo (a), new GridViewDataRowInfo (b));
            CustomSorting (this, args);
            return args.Handled ? args.SortResult : null;
        }
        /// <summary>Telerik compat: enables custom filtering via the CustomFiltering event. Stored.</summary>
        public bool EnableCustomFiltering { get; set; }
        /// <summary>Whether rows highlight under the pointer.</summary>
        /// <remarks>
        /// The engine hot-tracks (<c>DataGridView.HoveredRowIndex</c>, painted by the renderer this
        /// grid inherits), so this was stored and read by nothing while the behaviour happened anyway.
        /// It defaults to <c>true</c>, which is why nobody noticed: only setting it to <c>false</c>
        /// was a silent no-op.
        /// </remarks>
        public bool EnableHotTracking { get; set; } = true;

        /// <summary>
        /// Telerik compat: use scrollbars in hierarchy (master-detail) views. **Stored and inert, by
        /// necessity** -- see the remarks.
        /// </summary>
        /// <remarks>
        /// There is nothing to gate. A child view here is not a nested grid: it is a
        /// <c>GridChildView</c> (columns and rows of strings) painted directly by
        /// <c>RadGridViewRenderer.RenderDetailRow</c> into a clipped band whose height is capped at
        /// 320px, with no child control and no scroll region. Honouring this flag means building a
        /// hosted, scrollable child grid -- a feature, not a flag -- so it is recorded here as a known
        /// gap rather than wired to something that does not exist. (The 320px cap silently drops rows
        /// past roughly the thirteenth, which is its own defect.)
        /// </remarks>
        public bool UseScrollbarsInHierarchy { get; set; }

        /// <summary>Whether columns auto-size to fill the viewport.</summary>
        /// <remarks>
        /// A computed forward over <c>MasterTemplate.AutoSizeColumnsMode</c>, which
        /// already drives the real weight-proportional fill. This was not merely inert, it was
        /// REDUNDANT: a second property for a setting that already worked, silently disagreeing with it.
        /// </remarks>
        public bool AllowAutoSizeColumns {
            get => MasterTemplate.AutoSizeColumnsMode == GridViewAutoSizeColumnsMode.Fill;
            set => MasterTemplate.AutoSizeColumnsMode = value
                ? GridViewAutoSizeColumnsMode.Fill
                : GridViewAutoSizeColumnsMode.None;
        }

        /// <summary>Whether the user can resize columns by dragging a header edge.</summary>
        /// <remarks>
        /// Forwards to the engine's <c>AllowUserToResizeColumns</c>, which the grid already consults on
        /// the drag path. Same shape as <see cref="EnableHotTracking"/>: the <c>true</c> default matched
        /// reality, so only <c>false</c> was a silent no-op.
        /// </remarks>
        public bool AllowColumnResize {
            get => AllowUserToResizeColumns;
            set => AllowUserToResizeColumns = value;
        }
        /// <summary>Gets or sets the order the column chooser lists the columns in: their own order (<see cref="RadSortOrder.None"/>,
        /// the default, as Telerik's) or by caption.</summary>
        public RadSortOrder ColumnChooserSortOrder { get; set; } = RadSortOrder.None;

        /// <summary>Gets or sets whether the user can drag a row to another place in the grid. Off by default, as Telerik's.</summary>
        /// <remarks>Rows the grid holds itself move: not a bound grid's, whose order is its data source's, and not while the
        /// rows are sorted, grouped or filtered, when the position a row is dropped at is not one it can keep. The row
        /// dragged takes the place of the row it is dropped on.</remarks>
        public bool AllowRowReorder { get; set; }

        /// <summary>Puts the current cell into edit mode, as Telerik's parameterless BeginEdit does.</summary>
        /// <returns>Whether an editor opened: false for a read-only cell, or with no current cell.</returns>
        public bool BeginEdit () => base.BeginEdit (true);
        /// <summary>Gets or sets whether a click selects the whole row or the one cell.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): it sets the grid's own selection mode. Hides the base
        /// DataGridView.SelectionMode, as Telerik's GridViewSelectionMode does.</remarks>
        public new GridViewSelectionMode SelectionMode {
            get => base.SelectionMode == DataGridViewSelectionMode.CellSelect ? GridViewSelectionMode.CellSelect : GridViewSelectionMode.FullRowSelect;
            set => base.SelectionMode = value == GridViewSelectionMode.CellSelect ? DataGridViewSelectionMode.CellSelect : DataGridViewSelectionMode.FullRowSelect;
        }

        /// <summary>Gets or sets when the vertical scroll bar shows (forwards to the master template).</summary>
        public ScrollState VerticalScrollState {
            get => MasterTemplate.VerticalScrollState;
            set => MasterTemplate.VerticalScrollState = value;
        }

        /// <summary>Gets or sets when the horizontal scroll bar shows (forwards to the master template).</summary>
        public ScrollState HorizontalScrollState {
            get => MasterTemplate.HorizontalScrollState;
            set => MasterTemplate.HorizontalScrollState = value;
        }

        // The scroll bars the template's states allow: each shows when needed unless its state is AlwaysHide.
        internal void ApplyScrollStates ()
        {
            var bars = ScrollBars.None;

            if (MasterTemplate.HorizontalScrollState != ScrollState.AlwaysHide)
                bars |= ScrollBars.Horizontal;

            if (MasterTemplate.VerticalScrollState != ScrollState.AlwaysHide)
                bars |= ScrollBars.Vertical;

            ScrollBars = bars;
        }

        /// <summary>The grid's groups, as a tree: top-level groups, each with its nested groups.</summary>
        /// <remarks>
        /// <para>
        /// This returned <c>Array.Empty</c> while grouping was advertised as working -- and grouping
        /// genuinely does work: <c>GroupByColumn</c>, the drag-to-group panel, multi-level descriptors,
        /// per-group footers and collapse state are all real and tested. It was only the object model
        /// exposing them that was missing, so every consumer walking <c>Groups</c> to count, label or
        /// collapse them silently saw nothing at all.
        /// </para>
        /// <para>
        /// Projected on demand from the same filtered and sorted rows the display is built from, so it
        /// cannot describe a different grouping from the one on screen. Each group's
        /// <see cref="DataGroup.IsExpanded"/> reads and writes the grid's own collapse state rather
        /// than a copy.
        /// </para>
        /// </remarks>
        public IEnumerable<DataGroup> Groups => BuildDataGroups ();

        /// <summary>Telerik compat: the master view template (alias of <see cref="MasterTemplate"/>).</summary>
        public MasterGridViewTemplate MasterView => MasterTemplate;

        /// <summary>
        /// Raised before the sort changes: before each change to <see cref="SortDescriptors"/>, and once for a header click.
        /// Set <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> to leave the sort as it is.
        /// </summary>
        /// <remarks>Raised whether or not <see cref="EnableCustomSorting"/> is set, as Telerik's is (measured on
        /// UI.for.WinForms 2026.2.520: SortDescriptors.Add, SortDescriptors.Clear and a column's SortOrder each raise it once).</remarks>
        public event EventHandler<SortChangingEventArgs>? SortChanging;

        /// <summary>Raises the <see cref="SortChanging"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnSortChanging (SortChangingEventArgs e) => SortChanging?.Invoke (this, e);

        /// <summary>
        /// Raised once per row when <see cref="EnableCustomFiltering"/> is set, so the application can
        /// decide that row's visibility. Set <see cref="GridViewCustomFilteringEventArgs.Handled"/>
        /// along with <c>Visible</c>; leaving it unset keeps the filter descriptors' own answer.
        /// </summary>
        public event EventHandler<GridViewCustomFilteringEventArgs>? CustomFiltering;

        /// <summary>Raises the <see cref="CustomFiltering"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnCustomFiltering (GridViewCustomFilteringEventArgs e) => CustomFiltering?.Invoke (this, e);

        /// <summary>
        /// Raised when the current row is about to change, after everything else that could stop the
        /// move has agreed. Set <c>Cancel</c> to keep the current row.
        /// </summary>
        /// <remarks>
        /// The unsaved-changes guard — "you have unsaved edits, stay on this row?" — is what this
        /// event is for, and it never fired, so the guard was bypassed and the move always went
        /// through. A cancelled event and an unwired one look identical from the handler's side, which
        /// is why nothing surfaced it.
        /// </remarks>
        public event EventHandler<CurrentRowChangingEventArgs>? CurrentRowChanging;

        /// <summary>Raises the <see cref="CurrentRowChanging"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnCurrentRowChanging (CurrentRowChangingEventArgs e) => CurrentRowChanging?.Invoke (this, e);

        /// <inheritdoc/>
        protected internal override bool RaiseCurrentRowChanging (int oldRowIndex, int newRowIndex)
        {
            if (!base.RaiseCurrentRowChanging (oldRowIndex, newRowIndex))
                return false;

            if (CurrentRowChanging is null)
                return true;

            var e = new CurrentRowChangingEventArgs (RowAt (newRowIndex), RowAt (oldRowIndex));

            OnCurrentRowChanging (e);

            return !e.Cancel;
        }

        /// <summary>Raised as a cell edit goes ahead, before its editor opens.</summary>
        /// <remarks>Raised as of W6 mechanisms (#176), after <c>CellBeginEdit</c> and only when that was
        /// not cancelled. Telerik's args carry the editor type to use; these carry the cell and a
        /// <c>Cancel</c>, which keeps the cell out of edit mode -- the grid always edits with its own
        /// editor.</remarks>
        public event EventHandler<GridViewCellCancelEventArgs>? EditorRequired;
    }

    public partial class MasterGridViewTemplate
    {
        /// <summary>Gets the grid's rows, in the order shown -- the rows of <see cref="RadGridView.MasterView"/>.</summary>
        /// <exception cref="InvalidOperationException">The template is not attached to a grid.</exception>
        public GridViewRowInfoCollection Rows => Grid?.Rows ?? throw new InvalidOperationException ("The template is not attached to a grid.");

        /// <summary>Gets or sets when the vertical scroll bar shows. AutoHide by default, as Telerik's.</summary>
        /// <remarks>AlwaysHide takes the bar away; AutoHide and AlwaysShow both show it when the rows overflow (the grid has no
        /// bar shown over content that fits).</remarks>
        public ScrollState VerticalScrollState {
            get => vertical_scroll_state;
            set {
                vertical_scroll_state = value;
                Grid?.ApplyScrollStates ();
            }
        }

        private ScrollState vertical_scroll_state = ScrollState.AutoHide;

        /// <summary>Gets the grand summary rows the grid shows, at its top and bottom.</summary>
        public IEnumerable<GridViewSummaryRowInfo> SummaryRows {
            get {
                if (Grid is not { } grid)
                    yield break;

                foreach (DataGridViewRow row in ((DataGridView) grid).Rows)
                    if (row.Tag is GridSummaryRow { Level: 0 })
                        yield return new GridViewSummaryRowInfo (row);
            }
        }
        /// <summary>Telerik compat: whether the totals (summary) row is shown. Stored.</summary>
        public bool ShowTotals { get; set; }
    }

    /// <summary>Compat for Telerik.WinControls.UI.GridViewSelectionMode.</summary>
    public enum GridViewSelectionMode
    {
        /// <summary>Individual cells are selected.</summary>
        CellSelect = 0,
        /// <summary>Whole rows are selected.</summary>
        FullRowSelect = 1
    }

    /// <summary>Provides data for the RadGridView CurrentRowChanging event. Compat stub.</summary>
    public class CurrentRowChangingEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public CurrentRowChangingEventArgs () { }
        /// <summary>Initializes a new instance with the new and old rows.</summary>
        public CurrentRowChangingEventArgs (GridViewRowInfo? newRow, GridViewRowInfo? oldRow) { NewRow = newRow; OldRow = oldRow; }
        /// <summary>Gets the row becoming current.</summary>
        public GridViewRowInfo? NewRow { get; set; }
        /// <summary>Gets the row that was current.</summary>
        public GridViewRowInfo? OldRow { get; set; }
        /// <summary>Gets or sets whether to cancel the change.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>Provides data for the RadGridView SortChanging event.</summary>
    /// <remarks>A <see cref="GridViewCollectionChangingEventArgs"/>, as the arguments of Telerik's SortChanging are, so a
    /// handler written against Telerik's signature binds; <c>Cancel</c> is the base's.</remarks>
    public class SortChangingEventArgs : GridViewCollectionChangingEventArgs
    {

        /// <summary>The index of the column whose sort is changing.</summary>
        public int ColumnIndex { get; set; } = -1;

        /// <summary>The name the sort descriptor is (or would be) keyed by.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>The column's sort direction before this change, or null if it was unsorted.</summary>
        public ListSortDirection? OldDirection { get; set; }

        /// <summary>The direction it is about to take, or null if the click cycles back to unsorted.</summary>
        public ListSortDirection? NewDirection { get; set; }
    }

    /// <summary>Provides data for a Telerik grid collection-changing event.</summary>
    public class GridViewCollectionChangingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Gets or sets the changed item.</summary>
        public object? NewItem { get; set; }

        /// <summary>Gets what is about to happen to the collection.</summary>
        public System.Collections.Specialized.NotifyCollectionChangedAction Action { get; internal set; }

        /// <summary>Gets the items about to be added, or null.</summary>
        public System.Collections.IList? NewItems { get; internal set; }

        /// <summary>Gets the items about to be removed, or null.</summary>
        public System.Collections.IList? OldItems { get; internal set; }
    }

    /// <summary>Provides data for the RadGridView CustomSorting event: how <see cref="Row1"/> orders against <see cref="Row2"/>.</summary>
    public class GridViewCustomSortingEventArgs : EventArgs
    {
        internal GridViewCustomSortingEventArgs (GridViewRowInfo row1, GridViewRowInfo row2)
        {
            Row1 = row1;
            Row2 = row2;
        }

        /// <summary>Gets the first row compared.</summary>
        public GridViewRowInfo Row1 { get; }

        /// <summary>Gets the second row compared.</summary>
        public GridViewRowInfo Row2 { get; }

        /// <summary>Gets or sets the comparison: negative when <see cref="Row1"/> comes first, positive when <see cref="Row2"/> does, 0 when they are equal.</summary>
        public int SortResult { get; set; }

        /// <summary>Gets or sets whether <see cref="SortResult"/> is the answer. True by default, as Telerik's; false uses the grid's own comparison.</summary>
        public bool Handled { get; set; } = true;
    }
}
