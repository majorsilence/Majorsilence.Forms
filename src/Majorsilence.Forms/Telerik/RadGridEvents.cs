using System;
using System.Collections.Generic;
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
        public Majorsilence.Drawing.Font? Font { get; set; }
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
        /// <summary>Gets or sets whether the element draws a border.</summary>
        public bool DrawBorder { get; set; }
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
        /// <summary>Gets or sets the owning column info.</summary>
        public DataGridViewColumn? ColumnInfo { get; set; }
        /// <summary>Gets or sets the owning row info.</summary>
        public GridViewRowInfo? RowInfo { get; set; }
        /// <summary>Gets the cell style.</summary>
        public RadCellStyle Style { get; } = new RadCellStyle ();
    }

    /// <summary>Telerik-compat row visual element, exposed by the RowFormatting event.</summary>
    public class GridViewRowElement : RadElement
    {
        /// <summary>Gets or sets the owning row info.</summary>
        public GridViewRowInfo? RowInfo { get; set; }
        /// <summary>Gets or sets whether the element draws its fill.</summary>
        public bool DrawFill { get; set; }
        /// <summary>Gets or sets whether the element draws a border.</summary>
        public bool DrawBorder { get; set; }
        /// <summary>Gets or sets the number of gradient colors.</summary>
        public int NumberOfColors { get; set; } = 1;
        /// <summary>Gets or sets the gradient style. Stub.</summary>
        public object? GradientStyle { get; set; }
        /// <summary>Gets or sets the font.</summary>
        public Majorsilence.Drawing.Font? Font { get; set; }
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

    /// <summary>Provides data for the Telerik grid CellFormatting / ViewCellFormatting events.</summary>
    public class GridViewCellFormattingEventArgs : EventArgs
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

    /// <summary>Provides data for the Telerik grid RowFormatting event.</summary>
    public class GridViewRowFormattingEventArgs : EventArgs
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

    /// <summary>Provides data for the Telerik grid CellValidating event.</summary>
    public class GridViewCellValidatingEventArgs : EventArgs
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
        public RadContextMenuStub ContextMenu { get; } = new RadContextMenuStub ();
        /// <summary>Gets or sets the provider element that triggered the menu.</summary>
        public RadElement? ContextMenuProvider { get; set; }
        /// <summary>Gets or sets the row element under the cursor.</summary>
        public GridViewRowElement? RowElement { get; set; }
    }

    /// <summary>Minimal Telerik-compat context menu used by <see cref="ContextMenuOpeningEventArgs"/>.</summary>
    public class RadContextMenuStub
    {
        /// <summary>Gets the menu items.</summary>
        public List<object> Items { get; } = new ();
    }
}
