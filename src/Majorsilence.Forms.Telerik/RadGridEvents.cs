using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat cell style used in formatting handlers. Settable no-op stub.</summary>
    public class RadCellStyle
    {
        /// <summary>Gets or sets the fill (background) color.</summary>
        public Color BackColor { get; set; } = Color.Empty;
        /// <summary>Gets or sets the text color.</summary>
        public Color ForeColor { get; set; } = Color.Empty;
        /// <summary>Gets or sets the text alignment.</summary>
        public ContentAlignment Alignment { get; set; } = ContentAlignment.MiddleLeft;
        /// <summary>Gets or sets the font.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }
        /// <summary>Gets or sets whether the fill is customized.</summary>
        public bool CustomizeFill { get; set; }
        /// <summary>Gets or sets the gradient style. Stub.</summary>
        public object? GradientStyle { get; set; }
        /// <summary>Resets the style to defaults. Stub.</summary>
        public void Reset () { }
    }

    /// <summary>Telerik-compat cell visual element, exposed by formatting/create-cell events.</summary>
    public class GridViewCellElement : RadElement
    {
        /// <summary>Gets or sets the displayed text.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Gets or sets the cell value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets whether the element draws its fill.</summary>
        public bool DrawFill { get; set; }
        // DrawBorder is inherited from RadElement.
        /// <summary>Gets or sets the number of gradient colors.</summary>
        public int NumberOfColors { get; set; } = 1;
        /// <summary>Gets or sets the gradient style. Stub.</summary>
        public object? GradientStyle { get; set; }
        /// <summary>Gets or sets whether text wraps.</summary>
        public bool TextWrap { get; set; }
        /// <summary>Gets or sets whether drawing is clipped.</summary>
        public bool ClipDrawing { get; set; }
        /// <summary>Gets or sets the text alignment.</summary>
        public ContentAlignment TextAlignment { get; set; } = ContentAlignment.MiddleLeft;
        /// <summary>Gets or sets the row index of the cell.</summary>
        public int RowIndex { get; set; }
        /// <summary>Gets or sets the column index of the cell.</summary>
        public int ColumnIndex { get; set; } = -1;
        /// <summary>Gets or sets the owning column info.</summary>
        public DataGridViewColumn? ColumnInfo { get; set; }
        /// <summary>Gets or sets the owning row info.</summary>
        public GridViewRowInfo? RowInfo { get; set; }
        /// <summary>Gets the cell style.</summary>
        public RadCellStyle Style { get; } = new RadCellStyle ();
    }

    /// <summary>Telerik-compat cell visual element for a data (non-command, non-date) cell.</summary>
    public class GridCellElement : GridViewCellElement { }

    /// <summary>Telerik-compat cell visual element for a <see cref="GridViewDateTimeColumn"/> cell.</summary>
    public class GridDateTimeCellElement : GridCellElement { }

    /// <summary>Telerik-compat cell visual element for a command (button) column cell.</summary>
    public class GridCommandCellElement : GridCellElement
    {
        /// <summary>Gets the button element hosted by this command cell.</summary>
        public RadButtonElement CommandButton { get; } = new RadButtonElement ();
    }

    /// <summary>Telerik-compat base row visual element (Telerik's GridRowElement).</summary>
    public class GridRowElement : RadElement
    {
        /// <summary>Gets or sets the owning row info.</summary>
        public GridViewRowInfo? RowInfo { get; set; }
    }

    /// <summary>Telerik-compat row visual element, exposed by the RowFormatting event.</summary>
    public class GridViewRowElement : GridRowElement
    {
        /// <summary>Gets or sets whether the element draws its fill.</summary>
        public bool DrawFill { get; set; }
        // DrawBorder is inherited from RadElement.
        /// <summary>Gets or sets the number of gradient colors.</summary>
        public int NumberOfColors { get; set; } = 1;
        /// <summary>Gets or sets the gradient style. Stub.</summary>
        public object? GradientStyle { get; set; }
        /// <summary>Gets or sets the font.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }
        /// <summary>Gets or sets the table element that owns this row (the grid's shared <see cref="GridTableElement"/>).</summary>
        public GridTableElement? TableElement { get; set; }
    }

    /// <summary>Specifies which aspect of the grid changed for a table-element update. Compat for Telerik <c>GridUINotifyAction</c>.</summary>
    public enum GridUINotifyAction
    {
        /// <summary>The data changed.</summary>
        DataChanged = 0,
        /// <summary>The element state changed.</summary>
        StateChanged = 1,
        /// <summary>The layout changed.</summary>
        LayoutChanged = 2,
        /// <summary>Everything should be reset.</summary>
        Reset = 3
    }

    /// <summary>Telerik-compat table (view) visual element shared by all rows of a <see cref="RadGridView"/>.</summary>
    public class GridTableElement : RadElement
    {
        /// <summary>Gets or sets the header row height. Stored for Telerik compat.</summary>
        public int TableHeaderHeight { get; set; } = 28;

        /// <summary>Gets or sets the color used for alternating row striping. Stub.</summary>
        public Color AlternatingRowColor { get; set; } = Color.Empty;
        /// <summary>Gets or sets the row height. Stub.</summary>
        public int RowHeight { get; set; }
        /// <summary>Gets the owning view element (the grid's root element).</summary>
        public RadElement? ViewElement { get; set; }

        /// <summary>Refreshes the table element for the given notify action. No-op — the compat grid repaints as a whole.</summary>
        public void Update (GridUINotifyAction action) { }

        /// <summary>Scrolls the grid so the given row is visible. Stub — the compat grid manages its own scrolling.</summary>
        public void ScrollToRow (GridViewRowInfo row) { }

        /// <summary>Scrolls the grid so the given row index is visible. Stub — the compat grid manages its own scrolling.</summary>
        public void ScrollToRow (int rowIndex) { }

        /// <summary>Gets the visible row elements. Stub: empty — the compat grid does not expose per-row visual elements.</summary>
        public IEnumerable<GridRowElement> VisualRows => Array.Empty<GridRowElement> ();
    }

    /// <summary>Provides data for Telerik grid cell events (CellClick, CellDoubleClick, etc.).</summary>
    public class GridViewCellEventArgs : EventArgs
    {
        /// <summary>Gets or sets the row index.</summary>
        public int RowIndex { get; set; } = -1;
        /// <summary>Gets or sets the column index.</summary>
        public int ColumnIndex { get; set; } = -1;
        /// <summary>Gets or sets the cell value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets the affected row.</summary>
        public GridViewRowInfo? Row { get; set; }
        /// <summary>Gets or sets the affected column.</summary>
        public DataGridViewColumn? Column { get; set; }
        /// <summary>Gets or sets the cell element.</summary>
        public GridViewCellElement? CellElement { get; set; }
        /// <summary>Gets or sets the cell bounds.</summary>
        public Rectangle CellBounds { get; set; }
    }

    /// <summary>
    /// Provides data for the Telerik grid CellFormatting / ViewCellFormatting events. This is the base
    /// class also used by the real Telerik <c>CellFormattingEventArgs</c> name; <see cref="GridViewCellFormattingEventArgs"/>
    /// is an empty subclass kept for the <c>RadGridView.CellFormatting</c> event's original type, so
    /// handlers written against either name bind (VB <c>AddressOf</c> widening is legal under Option Strict On).
    /// </summary>
    public class CellFormattingEventArgs : EventArgs
    {
        /// <summary>Gets or sets the cell element being formatted.</summary>
        public GridViewCellElement CellElement { get; set; } = new GridViewCellElement ();
        /// <summary>Gets or sets the row index.</summary>
        public int RowIndex { get; set; } = -1;
        /// <summary>Gets or sets the column index.</summary>
        public int ColumnIndex { get; set; } = -1;
        /// <summary>Gets or sets the affected row.</summary>
        public GridViewRowInfo? Row { get; set; }
        /// <summary>Gets or sets the affected column.</summary>
        public DataGridViewColumn? Column { get; set; }
        /// <summary>Gets or sets the cell value.</summary>
        public object? Value { get; set; }
    }

    /// <summary>Provides data for the Telerik grid CellFormatting / ViewCellFormatting events (the type <see cref="RadGridView.CellFormatting"/> is declared with).</summary>
    public class GridViewCellFormattingEventArgs : CellFormattingEventArgs { }

    /// <summary>
    /// Provides data for the Telerik grid RowFormatting event. This is the base class also used by the
    /// real Telerik <c>RowFormattingEventArgs</c> name; <see cref="GridViewRowFormattingEventArgs"/> is an
    /// empty subclass kept for the <c>RadGridView.RowFormatting</c> event's original type, so handlers
    /// written against either name bind (VB <c>AddressOf</c> widening is legal under Option Strict On).
    /// </summary>
    public class RowFormattingEventArgs : EventArgs
    {
        /// <summary>Gets or sets the row element being formatted.</summary>
        public GridViewRowElement RowElement { get; set; } = new GridViewRowElement ();
        /// <summary>Gets or sets the affected row.</summary>
        public GridViewRowInfo? Row { get; set; }
        /// <summary>Gets or sets the row value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets the cell element, if applicable.</summary>
        public GridViewCellElement? CellElement { get; set; }
    }

    /// <summary>Provides data for the Telerik grid RowFormatting event (the type <see cref="RadGridView.RowFormatting"/> is declared with).</summary>
    public class GridViewRowFormattingEventArgs : RowFormattingEventArgs { }

    /// <summary>Provides data for the Telerik grid CurrentRowChanged event.</summary>
    public class CurrentRowChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public CurrentRowChangedEventArgs (GridViewRowInfo? oldRow, GridViewRowInfo? currentRow)
        {
            OldRow = oldRow;
            CurrentRow = currentRow;
        }

        /// <summary>Gets the previously current row, or null.</summary>
        public GridViewRowInfo? OldRow { get; }
        /// <summary>Gets the newly current row, or null.</summary>
        public GridViewRowInfo? CurrentRow { get; }
    }

    /// <summary>Provides data for the Telerik grid CurrentColumnChanged event.</summary>
    public class CurrentColumnChangedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public CurrentColumnChangedEventArgs (DataGridViewColumn? oldColumn, DataGridViewColumn? newColumn)
        {
            OldColumn = oldColumn;
            NewColumn = newColumn;
        }

        /// <summary>Gets the previously current column, or null.</summary>
        public DataGridViewColumn? OldColumn { get; }
        /// <summary>Gets the newly current column, or null.</summary>
        public DataGridViewColumn? NewColumn { get; }
    }

    /// <summary>Provides data for the Telerik grid ValueChanging event.</summary>
    public class ValueChangingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Gets or sets the previous cell value.</summary>
        public object? OldValue { get; set; }
        /// <summary>Gets or sets the new (proposed) cell value.</summary>
        public object? NewValue { get; set; }
    }

    /// <summary>Provides data for the Telerik grid CellValidating event.</summary>
    public class CellValidatingEventArgs : EventArgs
    {
        /// <summary>Gets or sets the row index.</summary>
        public int RowIndex { get; set; } = -1;
        /// <summary>Gets or sets the column index.</summary>
        public int ColumnIndex { get; set; } = -1;
        /// <summary>Gets or sets whether to cancel the edit.</summary>
        public bool Cancel { get; set; }
        /// <summary>Gets or sets the formatted value being validated.</summary>
        public object? FormattedValue { get; set; }
        /// <summary>Gets or sets the proposed value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets the previous value.</summary>
        public object? OldValue { get; set; }
        /// <summary>Gets or sets the affected row.</summary>
        public GridViewRowInfo? Row { get; set; }
        /// <summary>Gets or sets the affected column.</summary>
        public DataGridViewColumn? Column { get; set; }
    }

    /// <summary>Provides data for the Telerik grid CellBeginEdit event.</summary>
    public class GridViewCellCancelEventArgs : EventArgs
    {
        /// <summary>Gets or sets the row index.</summary>
        public int RowIndex { get; set; } = -1;
        /// <summary>Gets or sets the column index.</summary>
        public int ColumnIndex { get; set; } = -1;
        /// <summary>Gets or sets whether to cancel.</summary>
        public bool Cancel { get; set; }
        /// <summary>Gets or sets the affected row.</summary>
        public GridViewRowInfo? Row { get; set; }
    }

    /// <summary>Provides data for the Telerik grid CreateCell event.</summary>
    public class GridViewCreateCellEventArgs : EventArgs
    {
        /// <summary>Gets or sets the row index.</summary>
        public int RowIndex { get; set; } = -1;
        /// <summary>Gets or sets the cell element to use.</summary>
        public GridViewCellElement? CellElement { get; set; }
        /// <summary>Gets or sets the column.</summary>
        public DataGridViewColumn? Column { get; set; }
        /// <summary>Gets or sets the affected row.</summary>
        public GridViewRowInfo? Row { get; set; }
        /// <summary>Gets or sets the cell type to create.</summary>
        public Type? CellType { get; set; }
    }

    /// <summary>Provides data for the RadGridView ChildViewExpanding event (master-detail).</summary>
    public class ChildViewExpandingEventArgs : EventArgs
    {
        /// <summary>Gets or sets the master row whose child view is expanding.</summary>
        public GridViewRowInfo? Row { get; set; }
        /// <summary>Gets or sets whether to cancel the expansion.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>Provides data for the Telerik grid ContextMenuOpening event.</summary>
    public class ContextMenuOpeningEventArgs : EventArgs
    {
        /// <summary>Gets the context menu being opened.</summary>
        public RadContextMenu ContextMenu { get; } = new RadContextMenu ();
        /// <summary>Gets or sets the provider element that triggered the menu.</summary>
        public RadElement? ContextMenuProvider { get; set; }
        /// <summary>Gets or sets the row element under the cursor.</summary>
        public GridViewRowElement? RowElement { get; set; }
    }

    /// <summary>
    /// Obsolete alias kept for source compatibility with earlier versions of this compat layer.
    /// The type has been promoted (and renamed) to <see cref="RadContextMenu"/> in RadMisc.cs.
    /// </summary>
    [Obsolete ("Use RadContextMenu instead.")]
    public class RadContextMenuStub : RadContextMenu { }
}
