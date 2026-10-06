namespace Majorsilence.Forms
{
    /// <summary>
    /// How a <see cref="StackPanel"/> places something across its stacking axis: a child within the column,
    /// or the column within a panel that is wider than <see cref="StackPanel.MaximumContentWidth"/>.
    /// </summary>
    public enum StackAlignment
    {
        /// <summary>Fill the available width (or height, for a horizontal stack). For a column inside a wider panel this places it at the start.</summary>
        Stretch = 0,

        /// <summary>At the start edge, at the child's own size.</summary>
        Start = 1,

        /// <summary>In the middle, at the child's own size.</summary>
        Center = 2,

        /// <summary>At the end edge, at the child's own size.</summary>
        End = 3,
    }
}
