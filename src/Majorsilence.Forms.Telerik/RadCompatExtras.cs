using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Compat stand-in for Telerik's RadPropertyStore (dock layout persistence).</summary>
    public class RadPropertyStore
    {
        /// <summary>The stored items.</summary>
        public System.Collections.Generic.List<PropertyStoreItem> Items { get; } = new ();

        /// <summary>Adds an item to the store.</summary>
        public void Add (PropertyStoreItem item) => Items.Add (item);
    }

    /// <summary>Compat stand-in for Telerik's PropertyStoreItem.</summary>
    public class PropertyStoreItem
    {
        /// <summary>Initializes an item for a typed property value.</summary>
        public PropertyStoreItem (Type type, string name, object? value)
        {
            Type = type;
            Name = name;
            Value = value;
        }

        /// <summary>Initializes an item for a typed property value with a default.</summary>
        public PropertyStoreItem (Type type, string name, object? value, object? defaultValue)
            : this (type, name, value)
        {
            DefaultValue = defaultValue;
        }

        /// <summary>The property type.</summary>
        public Type Type { get; }

        /// <summary>The property name.</summary>
        public string Name { get; }

        /// <summary>Telerik alias of <see cref="Name"/>.</summary>
        public string PropertyName => Name;

        /// <summary>The display label shown for the property.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>The attributes applied to the property (Telerik uses these for category/editor hints).</summary>
        public System.Collections.Generic.List<object> Attributes { get; } = new ();

        /// <summary>The property value.</summary>
        public object? Value { get; set; }

        /// <summary>The default value.</summary>
        public object? DefaultValue { get; set; }
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

        /// <summary>The validated column.</summary>
        public Majorsilence.Forms.DataGridViewColumn? Column { get; set; }

        /// <summary>The validated row.</summary>
        public GridViewRowInfo? Row { get; set; }

        /// <summary>The validated value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for RadPropertyGrid item edit completion. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemEditedEventArgs : EventArgs
    {
        /// <summary>The edited item.</summary>
        public PropertyGridItem? Item { get; set; }
    }

    /// <summary>Provides data for RadPropertyGrid editor selection. Mirrors Telerik's shape.</summary>
    public class PropertyGridEditorRequiredEventArgs : EventArgs
    {
        /// <summary>The item needing an editor.</summary>
        public PropertyGridItem? Item { get; set; }

        /// <summary>The editor type to use.</summary>
        public Type? EditorType { get; set; }

        /// <summary>The editor instance to use.</summary>
        public object? Editor { get; set; }
    }

    /// <summary>Provides data for RadPropertyGrid item value changes. Mirrors Telerik's shape.</summary>
    public class PropertyGridItemValueChangedEventArgs : EventArgs
    {
        /// <summary>The item whose value changed.</summary>
        public PropertyGridItem? Item { get; set; }
    }

    /// <summary>Compat stand-in for Telerik's text-box property editor.</summary>
    public class PropertyGridTextBoxEditor
    {
    }

    /// <summary>Compat stand-in for Telerik's DataGroup (grid grouping node).</summary>
    public class DataGroup
    {
        /// <summary>The group key.</summary>
        public object? Key { get; set; }
    }

    /// <summary>Provides data for RadDock tab-strip creation. Mirrors Telerik's shape.</summary>
    public class DockTabStripNeededEventArgs : EventArgs
    {
        /// <summary>The strip to use (assign to supply one).</summary>
        public object? Strip { get; set; }
    }

    /// <summary>Compat stand-in for the grid group-panel field element.</summary>
    public class GroupFieldElement : RadElement
    {
        /// <summary>The field name shown by the element.</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>The display text of the element.</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Compat stand-in for Telerik's RadControl base (sites type variables as RadControl).</summary>
    public class RadControl : Majorsilence.Forms.Control
    {
    }

    /// <summary>Compat stand-in for the drop-down calendar of a RadDateTimePicker.</summary>
    public class RadDateTimePickerCalendar : RadElement
    {
        /// <summary>Whether the time picker panel is shown.</summary>
        public bool ShowTimePicker { get; set; }
    }

    /// <summary>Compat stand-in for the grid header cell element.</summary>
    public class GridHeaderCellElement : RadElement
    {
    }

    /// <summary>Compat stand-in for the grid data cell element.</summary>
    public class GridDataCellElement : RadElement
    {
    }

    /// <summary>Compat stand-in for the calendar table element of a date picker popup.</summary>
    public class CalendarTableElement : RadElement
    {
    }

    /// <summary>Compat stand-in for Telerik's filter operation context.</summary>
    public class FilterOperationContext
    {
        /// <summary>The field being filtered.</summary>
        public string FieldName { get; set; } = string.Empty;
    }

    /// <summary>Compat stand-in for the grid's paging panel element.</summary>
    public class PagingPanelElement : RadElement
    {
        /// <summary>First-page button element (stub).</summary>
        public CommandBarButton FirstButton { get; } = new CommandBarButton ();
        /// <summary>Previous-page button element (stub).</summary>
        public CommandBarButton PreviousButton { get; } = new CommandBarButton ();
        /// <summary>Fast-back button element (stub).</summary>
        public CommandBarButton FastBackButton { get; } = new CommandBarButton ();
        /// <summary>Fast-forward button element (stub).</summary>
        public CommandBarButton FastForwardButton { get; } = new CommandBarButton ();
        /// <summary>Next-page button element (stub).</summary>
        public CommandBarButton NextButton { get; } = new CommandBarButton ();
        /// <summary>Last-page button element (stub).</summary>
        public CommandBarButton LastButton { get; } = new CommandBarButton ();
    }

    /// <summary>Provides data for grid filter-popup creation. Mirrors Telerik's shape.</summary>
    public class FilterPopupRequiredEventArgs : EventArgs
    {
        /// <summary>The column the popup is for.</summary>
        public Majorsilence.Forms.DataGridViewColumn? Column { get; set; }

        /// <summary>The popup to show (assign to supply one).</summary>
        public object? FilterPopup { get; set; }
    }

    /// <summary>Provides cancellable data for RadPageView page changes. Mirrors Telerik's shape.</summary>
    public class RadPageViewCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>The page involved.</summary>
        public object? Page { get; set; }
    }

    /// <summary>Provides data for dock-window events. Mirrors Telerik's shape.</summary>
    public class DockWindowEventArgs : EventArgs
    {
        /// <summary>The dock window involved.</summary>
        public object? DockWindow { get; set; }
    }
}

namespace Majorsilence.Forms.Telerik.Primitives
{
    /// <summary>Relative-qualification seam for Telerik's Primitives.FillPrimitive.</summary>
    public class FillPrimitive : Majorsilence.Forms.Telerik.RadElement
    {
        // BackColor is inherited from RadElement.

        /// <summary>The gradient style (stored for compat).</summary>
        public object? GradientStyle { get; set; }
    }

    /// <summary>Relative-qualification seam for Telerik's Primitives.BorderPrimitive.</summary>
    public class BorderPrimitive : Majorsilence.Forms.Telerik.RadElement
    {
        /// <summary>The border color.</summary>
        public new System.Drawing.Color ForeColor { get; set; }
    }
}

namespace Majorsilence.Forms.Telerik.UI
{
    /// <summary>Relative-qualification seam: legacy code written under Imports Telerik.WinControls
    /// references UI.RowFormattingEventArgs / UI.GridViewCellEventArgs.</summary>
    public class RowFormattingEventArgs : Majorsilence.Forms.Telerik.RowFormattingEventArgs { }

    /// <summary>Relative-qualification seam for UI.GridViewCellEventArgs.</summary>
    public class GridViewCellEventArgs : Majorsilence.Forms.Telerik.GridViewCellEventArgs { }
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
