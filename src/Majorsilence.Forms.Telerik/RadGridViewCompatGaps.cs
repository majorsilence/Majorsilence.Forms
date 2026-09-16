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
        /// <summary>Telerik compat: enables custom sorting via the SortChanging event. Stored.</summary>
        public bool EnableCustomSorting { get; set; }
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
        /// <summary>Telerik compat: the column-chooser sort order. Stored.</summary>
        public ListSortDirection ColumnChooserSortOrder { get; set; } = ListSortDirection.Ascending;
        /// <summary>Telerik compat: the row/cell selection mode. Stored. (Intentionally hides the base
        /// DataGridView.SelectionMode -- RadGridView exposes Telerik's GridViewSelectionMode instead.)</summary>
        public new GridViewSelectionMode SelectionMode { get; set; } = GridViewSelectionMode.FullRowSelect;

        /// <summary>Telerik compat: the vertical auto-hide scrollbar state (forwards to the master template).</summary>
        public ScrollState VerticalScrollState {
            get => MasterTemplate.VerticalScrollState;
            set => MasterTemplate.VerticalScrollState = value;
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
        /// Raised before a header-click sort is applied, when <see cref="EnableCustomSorting"/> is set.
        /// Set <see cref="SortChangingEventArgs.Cancel"/> to sort the data yourself instead.
        /// </summary>
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

        // Still never raised; present so designer/handler code (AddHandler / Handles) compiles.
#pragma warning disable CS0067
        /// <summary>Raised when a cell editor is required. Stub (never raised).</summary>
        public event EventHandler<GridViewCellCancelEventArgs>? EditorRequired;
#pragma warning restore CS0067
    }

    public partial class MasterGridViewTemplate
    {
        /// <summary>Telerik compat: the vertical auto-hide scrollbar state. Stub.</summary>
        public ScrollState VerticalScrollState { get; set; } = ScrollState.AlwaysShow;
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

    /// <summary>Provides data for the RadGridView SortChanging event. Compat stub.</summary>
    public class SortChangingEventArgs : EventArgs
    {
        /// <summary>Gets or sets whether to cancel the sort.</summary>
        public bool Cancel { get; set; }

        /// <summary>The index of the column whose sort is changing.</summary>
        public int ColumnIndex { get; set; } = -1;

        /// <summary>The name the sort descriptor is (or would be) keyed by.</summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>The column's sort direction before this change, or null if it was unsorted.</summary>
        public ListSortDirection? OldDirection { get; set; }

        /// <summary>The direction it is about to take, or null if the click cycles back to unsorted.</summary>
        public ListSortDirection? NewDirection { get; set; }
    }

    /// <summary>Provides data for a Telerik grid collection-changing event. Compat stub.</summary>
    public class GridViewCollectionChangingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Gets or sets the changed item.</summary>
        public object? NewItem { get; set; }
    }
}
