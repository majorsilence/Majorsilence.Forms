using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A toolkit theme's grid colours, for a grid whose look comes from a named theme rather than from this
    /// library's theme tokens (a Telerik-compatible grid with a <c>ThemeName</c>). The renderer reads it between
    /// the grid's own styles, which still win, and the type-level defaults (<see cref="DataGridView.DefaultColumnHeaderStyle"/>
    /// and the rest), which it replaces.
    /// </summary>
    internal sealed class DataGridViewThemePalette
    {
        /// <summary>The fill behind the rows, below the last row and right of the last column.</summary>
        public SKColor Background { get; init; }

        /// <summary>The cells' text.</summary>
        public SKColor Foreground { get; init; }

        /// <summary>The column header band's fill.</summary>
        public SKColor HeaderBackground { get; init; }

        /// <summary>The column headers' text.</summary>
        public SKColor HeaderForeground { get; init; }

        /// <summary>The grid lines: under each row, under the header band and right of the row header column.</summary>
        public SKColor GridLine { get; init; }

        /// <summary>Whether the cells and column headers are divided by vertical lines as well.</summary>
        public bool VerticalGridLines { get; init; }

        /// <summary>Every second row's fill, when the grid alternates its rows.</summary>
        public SKColor AlternatingRow { get; init; }

        /// <summary>The fill of the row under the pointer.</summary>
        public SKColor HoverRow { get; init; }

        /// <summary>The selected rows' fill.</summary>
        public SKColor SelectionBackground { get; init; }

        /// <summary>The selected rows' text.</summary>
        public SKColor SelectionForeground { get; init; }

        /// <summary>The outline drawn inside the current cell, or null for none.</summary>
        public SKColor? CurrentCellBorder { get; init; }

        /// <summary>The current cell's outline width in logical pixels.</summary>
        public int CurrentCellBorderWidth { get; init; } = 2;
    }
}
