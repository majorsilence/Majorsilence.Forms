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
        /// <summary>Telerik compat: enables hot-tracking of rows/cells under the pointer. Stored.</summary>
        public bool EnableHotTracking { get; set; } = true;
        /// <summary>Telerik compat: use scrollbars in hierarchy (master-detail) views. Stored.</summary>
        public bool UseScrollbarsInHierarchy { get; set; }
        /// <summary>Telerik compat: whether columns auto-size to fill the viewport. Stored.</summary>
        public bool AllowAutoSizeColumns { get; set; }
        /// <summary>Telerik compat: whether the user can resize columns. Stored.</summary>
        public bool AllowColumnResize { get; set; } = true;
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

        /// <summary>Telerik compat: the grouped rows. Stub: the compat grid does not expose group rows yet.</summary>
        public IEnumerable<DataGroup> Groups => Array.Empty<DataGroup> ();

        /// <summary>Telerik compat: the master view template (alias of <see cref="MasterTemplate"/>).</summary>
        public MasterGridViewTemplate MasterView => MasterTemplate;

        // Never raised by the compat grid; present so designer/handler code (AddHandler / Handles) compiles.
#pragma warning disable CS0067
        /// <summary>Raised before the current row changes. Stub (never raised).</summary>
        public event EventHandler<CurrentRowChangingEventArgs>? CurrentRowChanging;
        /// <summary>Raised before a sort operation. Stub (never raised).</summary>
        public event EventHandler<SortChangingEventArgs>? SortChanging;
        /// <summary>Raised for custom filtering of a row. Stub (never raised).</summary>
        public event EventHandler<GridViewCustomFilteringEventArgs>? CustomFiltering;
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
    }

    /// <summary>Provides data for a Telerik grid collection-changing event. Compat stub.</summary>
    public class GridViewCollectionChangingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Gets or sets the changed item.</summary>
        public object? NewItem { get; set; }
    }
}
