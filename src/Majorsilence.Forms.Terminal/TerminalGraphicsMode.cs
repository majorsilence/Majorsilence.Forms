namespace Majorsilence.Forms.Terminal
{
    /// <summary>How a frame reaches the terminal.</summary>
    public enum TerminalGraphicsMode
    {
        /// <summary>
        /// ANSI half-blocks (<c>▄</c>): one pixel per column and two per row, in any terminal that has colour.
        /// The universal fallback.
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
    }
}
