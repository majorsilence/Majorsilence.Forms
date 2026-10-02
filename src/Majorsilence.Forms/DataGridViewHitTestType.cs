namespace Majorsilence.Forms
{
    /// <summary>Specifies the part of the <see cref="DataGridView"/> identified by a hit test.</summary>
    /// <remarks>
    /// At namespace scope, as upstream declares it (<c>DataGridViewHitTestType.cs</c>). It was nested in
    /// <see cref="DataGridView"/>, so <c>hit.Type == DataGridViewHitTestType.ColumnHeader</c> compiled only
    /// inside a <see cref="DataGridView"/> subclass (DGV-35). Values match upstream's.
    /// </remarks>
    public enum DataGridViewHitTestType
    {
        /// <summary>The point is not part of the grid.</summary>
        None = 0,
        /// <summary>The point is over a cell.</summary>
        Cell = 1,
        /// <summary>The point is over a column header.</summary>
        ColumnHeader = 2,
        /// <summary>The point is over a row header.</summary>
        RowHeader = 3,
        /// <summary>The point is over the top-left header.</summary>
        TopLeftHeader = 4,
        /// <summary>The point is over the horizontal scroll bar.</summary>
        HorizontalScrollBar = 5,
        /// <summary>The point is over the vertical scroll bar.</summary>
        VerticalScrollBar = 6
    }
}
