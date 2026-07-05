using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Compat stand-in for Telerik's RadPropertyStore (dock layout persistence).</summary>
    public class RadPropertyStore
    {
    }

    /// <summary>Compat stand-in for Telerik's RadGridViewElement (root visual element of a grid).</summary>
    public class RadGridViewElement
    {
        /// <summary>Initializes a new element for the given grid.</summary>
        public RadGridViewElement (RadGridView owner) { Owner = owner; }

        /// <summary>The grid this element belongs to.</summary>
        public RadGridView Owner { get; }
    }

    /// <summary>Provides data for cell value push/pull events. Mirrors Telerik's GridViewCellValueEventArgs.</summary>
    public class GridViewCellValueEventArgs : EventArgs
    {
        /// <summary>Initializes the args for a row/column and value.</summary>
        public GridViewCellValueEventArgs (int rowIndex, int columnIndex, object? value)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            Value = value;
        }

        /// <summary>The row index of the cell.</summary>
        public int RowIndex { get; }

        /// <summary>The column index of the cell.</summary>
        public int ColumnIndex { get; }

        /// <summary>The cell value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for grid collection change notifications. Mirrors Telerik's shape.</summary>
    public class GridViewCollectionChangedEventArgs : EventArgs
    {
        /// <summary>Initializes the args.</summary>
        public GridViewCollectionChangedEventArgs (object? newItems = null) { NewItems = newItems; }

        /// <summary>The items involved in the change.</summary>
        public object? NewItems { get; }
    }

    /// <summary>Compat stand-in for Telerik's GridViewListSource (the grid's list data source seam).</summary>
    public class GridViewListSource
    {
        /// <summary>Initializes the source for a grid.</summary>
        public GridViewListSource (RadGridView owner) { Owner = owner; }

        /// <summary>The owning grid.</summary>
        public RadGridView Owner { get; }
    }

    /// <summary>Compat stand-in for Telerik's PropertyGridItem.</summary>
    public class PropertyGridItem
    {
        /// <summary>The property name shown for the item.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The property label shown for the item.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>The item value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for property-grid editor initialization. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemEditorInitializedEventArgs : EventArgs
    {
        /// <summary>The item whose editor was initialized.</summary>
        public PropertyGridItem? Item { get; set; }

        /// <summary>The editor instance.</summary>
        public object? Editor { get; set; }
    }

    /// <summary>Provides data for property-grid item formatting. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemFormattingEventArgs : EventArgs
    {
        /// <summary>The item being formatted.</summary>
        public PropertyGridItem? Item { get; set; }
    }


    /// <summary>Provides data for element render callbacks. Mirrors Telerik's RenderElementEventArgs.</summary>
    public class RenderElementEventArgs : EventArgs
    {
        /// <summary>The element being rendered.</summary>
        public object? Element { get; set; }
    }

    /// <summary>Provides data for custom filtering. Mirrors Telerik's GridViewCustomFilteringEventArgs.</summary>
    public class GridViewCustomFilteringEventArgs : EventArgs
    {
        /// <summary>The row being evaluated.</summary>
        public object? Row { get; set; }

        /// <summary>Whether the row is visible under the filter.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Whether the event was handled by the custom filter.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>Provides data for cell validation completion. Mirrors Telerik's CellValidatedEventArgs.</summary>
    public class CellValidatedEventArgs : EventArgs
    {
        /// <summary>The row index of the validated cell.</summary>
        public int RowIndex { get; set; }

        /// <summary>The column index of the validated cell.</summary>
        public int ColumnIndex { get; set; }

        /// <summary>The validated value.</summary>
        public object? Value { get; set; }
    }
}

namespace Majorsilence.Forms.Telerik.Data
{
    /// <summary>
    /// Relative-qualification seam: legacy code written under Imports Telerik.WinControls(.UI)
    /// references Data.PositionChangedEventArgs; with Majorsilence.Forms.Telerik imported this
    /// nested namespace satisfies the same relative name.
    /// </summary>
    public class PositionChangedEventArgs : Majorsilence.Forms.Telerik.PositionChangedEventArgs
    {
        /// <summary>Initializes the args for a position.</summary>
        public PositionChangedEventArgs (int position) : base (position) { }
    }
}

namespace Telerik.Collections.Generic
{
    /// <summary>
    /// Compat stand-in for Telerik's Index collection, declared under Telerik's own namespace
    /// because call sites reference it fully qualified (an unqualified Index would collide with
    /// System.Index) and VB cannot alias open generic types.
    /// </summary>
    public class Index<T> : System.Collections.ObjectModel.Collection<T>
    {
    }
}
