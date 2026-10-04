namespace Majorsilence.Forms.Terminal
{
    /// <summary>How a frame reaches the terminal.</summary>
    public enum TerminalGraphicsMode
    {
        /// <summary>
        /// ANSI half-blocks (<c>▄</c>): one pixel per column and two per row, in any terminal that has colour.
        /// The most basic fallback: it needs only one glyph. See <see cref="Blocks"/> for the sharper default.
        /// </summary>
        HalfBlock,
        /// <summary>
        /// The Kitty graphics protocol: the frame is sent as a real image at the terminal's full pixel
        /// resolution. Supported by kitty, WezTerm and Ghostty, among others.
        /// </summary>
        Kitty,
        /// <summary>
        /// Sixel graphics: a real image at the terminal's full pixel resolution, in at most 256 colours,
        /// repainting only the changed region. Supported by foot, mlterm, iTerm2, WezTerm, xterm (with
        /// <c>-ti vt340</c>) and others.
        /// </summary>
        Sixel,
        /// <summary>
        /// Unicode block elements at 2x4 pixels per cell: four times the detail of <see cref="HalfBlock"/>, from the
        /// quadrant and quarter-block glyphs. The default for a terminal with no graphics protocol, since text is
        /// readable at scale 1 on a large terminal. Needs a font that has those glyphs, which nearly every
        /// terminal font does; where one does not, use <see cref="HalfBlock"/>.
        /// </summary>
        Blocks,
    }
}
