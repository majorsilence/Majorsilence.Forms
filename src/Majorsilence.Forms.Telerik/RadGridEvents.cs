using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat cell style: a cell's own appearance (<c>row.Cells[i].Style</c>), or a formatting
    /// handler's (<c>e.CellElement.Style</c>).
    /// </summary>
    /// <remarks>
    /// As in Telerik, <see cref="BackColor"/> is painted only once <see cref="CustomizeFill"/> is set;
    /// <see cref="ForeColor"/>, <see cref="Font"/> and <see cref="Alignment"/> apply as soon as they are set.
    /// A cell's style is kept per cell, so the <c>Cells[i]</c> wrapper can be fetched again and still
    /// return the same one (W6 mechanisms, #176: it was a new, discarded object on every access).
    /// Cell formatting handlers run after it, so they override it for that paint.
    /// </remarks>
    public class RadCellStyle
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridViewCell, RadCellStyle> cell_styles = new ();

        private DataGridViewCell? owner;
        private Color back_color = Color.Empty;
        private Color fore_color = Color.Empty;
        private ContentAlignment alignment = ContentAlignment.MiddleLeft;
        private bool alignment_set;
        private Majorsilence.Forms.Drawing.Font? font;
        private bool font_applied;
        private bool customize_fill;

        internal static RadCellStyle For (DataGridViewCell cell) => cell_styles.GetValue (cell, c => new RadCellStyle { owner = c });

        internal static RadCellStyle? Existing (DataGridViewCell cell) => cell_styles.TryGetValue (cell, out var style) ? style : null;

        /// <summary>Gets or sets the fill (background) color. Painted once <see cref="CustomizeFill"/> is true.</summary>
        public Color BackColor { get => back_color; set { back_color = value; Changed (); } }
        /// <summary>Gets or sets the text color.</summary>
        public Color ForeColor { get => fore_color; set { fore_color = value; Changed (); } }
        /// <summary>Gets or sets the text alignment.</summary>
        public ContentAlignment Alignment { get => alignment; set { alignment = value; alignment_set = true; Changed (); } }
        /// <summary>Gets or sets the font.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get => font; set { font = value; Changed (); } }
        /// <summary>Gets or sets whether the fill is customized -- whether <see cref="BackColor"/> is painted.</summary>
        public bool CustomizeFill { get => customize_fill; set { customize_fill = value; Changed (); } }
        /// <summary>Gets or sets the gradient style. Stored: fills are always solid.</summary>
        public object? GradientStyle { get; set; }

        /// <summary>Resets the style to defaults.</summary>
        public void Reset ()
        {
            back_color = Color.Empty;
            fore_color = Color.Empty;
            alignment = ContentAlignment.MiddleLeft;
            alignment_set = false;
            font = null;
            customize_fill = false;
            Changed ();
        }

        private void Changed () => owner?.DataGridView?.Invalidate ();

        // Called for every paint of the cell, after the per-frame colour reset and conditional formatting.
        internal void ApplyTo (DataGridViewCell cell)
        {
            if (customize_fill && back_color != Color.Empty)
                cell.Style.BackgroundColor = new SkiaSharp.SKColor (back_color.R, back_color.G, back_color.B, back_color.A);
            if (fore_color != Color.Empty)
                cell.Style.ForegroundColor = new SkiaSharp.SKColor (fore_color.R, fore_color.G, fore_color.B, fore_color.A);
            if (alignment_set)
                cell.Style.Alignment = (DataGridViewContentAlignment) (int) alignment;

            if (font is not null) {
                cell.Style.Font = TypefaceCache.Resolve (font);
                cell.Style.FontSize = (int) System.Math.Round (font.SizeInPoints * 96f / 72f);   // pixels, as Control.Font does
                font_applied = true;
            } else if (font_applied) {
                cell.Style.Font = null;
                cell.Style.FontSize = null;
                font_applied = false;
            }
        }
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
        /// <summary>Gets or sets the owning column info (Telerik-typed).</summary>
        public GridViewDataColumn? ColumnInfo { get; set; }
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

    /// <summary>Provides data for group-summary evaluation. Mirrors Telerik's shape.</summary>
    public class GroupSummaryEvaluationEventArgs : EventArgs
    {
        /// <summary>The summary item being evaluated.</summary>
        public GridViewSummaryItem SummaryItem { get; set; } = new GridViewSummaryItem ();

        /// <summary>The group being summarized, or null for the grand total.</summary>
        public DataGroup? Group { get; set; }

        /// <summary>The computed summary value.</summary>
        public object? Value { get; set; }

        /// <summary>The display format string for the summary cell.</summary>
        public string FormatString { get; set; } = string.Empty;
    }

    /// <summary>Provides data for group expand/collapse. Mirrors Telerik's shape.</summary>
    public class GroupExpandingEventArgs : EventArgs
    {
        /// <summary>The group being expanded or collapsed.</summary>
        public DataGroup? DataGroup { get; set; }

        /// <summary>Set true to cancel the expand/collapse.</summary>
        public bool Cancel { get; set; }
    }

    /// <summary>Provides data for row validation before commit. Mirrors Telerik's shape.</summary>
    public class RowValidatingEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>The row being validated.</summary>
        public GridViewRowInfo? Row { get; set; }
    }

    /// <summary>Provides data for row-scoped grid events (e.g. DefaultValuesNeeded). Mirrors Telerik's shape.</summary>
    public class GridViewRowEventArgs : System.EventArgs
    {
        /// <summary>The row involved.</summary>
        public GridViewRowInfo? Row { get; set; }
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
        // The grid this element belongs to; null for one application code built itself.
        internal RadGridView? Grid { get; set; }

        /// <summary>Gets or sets the height of the column header row.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): it is the grid's <see cref="DataGridView.ColumnHeadersHeight"/>.</remarks>
        public int TableHeaderHeight {
            get => Grid?.ColumnHeadersHeight ?? table_header_height;
            set {
                table_header_height = value;

                if (Grid is { } grid)
                    grid.ColumnHeadersHeight = value;
            }
        }

        private int table_header_height = 28;

        /// <summary>Gets or sets the colour of every other row, shown when the grid's <c>EnableAlternatingRowColor</c> is on.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): it is the grid's alternating-row background.
        /// Empty leaves the theme's alternating colour.</remarks>
        public Color AlternatingRowColor {
            get => alternating_row_color;
            set {
                alternating_row_color = value;

                if (Grid is { } grid) {
                    grid.AlternatingRowsDefaultCellStyle.BackgroundColor = value.IsEmpty
                        ? null
                        : new SkiaSharp.SKColor (value.R, value.G, value.B, value.A);
                    grid.Invalidate ();
                }
            }
        }

        private Color alternating_row_color = Color.Empty;

        /// <summary>Gets or sets the height of every data row.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): the grid's row template takes it, and the rows
        /// already in the grid are resized to it. Zero or less leaves the rows as they are.</remarks>
        public int RowHeight {
            get => row_height;
            set {
                row_height = value;

                if (value > 0 && Grid is { } grid)
                    grid.ApplyRowHeight (value);
            }
        }

        private int row_height;
        /// <summary>Gets the owning view element (the grid's root element).</summary>
        public RadElement? ViewElement { get; set; }

        /// <summary>Refreshes the table element for the given notify action. No-op — the compat grid repaints as a whole.</summary>
        public void Update (GridUINotifyAction action) { }

        /// <summary>Telerik compat: rebuilds/refreshes the view. No-op — the compat grid repaints as a whole.</summary>
        public void UpdateView () { }

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
        /// <summary>Gets or sets the affected column.</summary>
        public GridViewColumn? Column { get; set; }
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
