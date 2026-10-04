namespace Majorsilence.Forms.Terminal
{
    /// <summary>Settings for <see cref="TerminalApplication.Use(TerminalOptions)"/>.</summary>
    public sealed class TerminalOptions
    {
        /// <summary>
        /// Gets or sets the colour mode, or <c>null</c> (the default) to detect it from <c>COLORTERM</c> and
        /// <c>TERM</c>.
        /// </summary>
        public TerminalColorMode? ColorMode { get; set; }

        /// <summary>
        /// Gets or sets how frames reach the terminal, or <c>null</c> (the default) to find out: the terminal is
        /// asked what it supports (Kitty graphics, then Sixel, else half-blocks), with the environment's guess
        /// (<c>TERM</c>, <c>TERM_PROGRAM</c>, half-blocks inside tmux or screen) used until it answers and when
        /// there is no input to read an answer from. A mode set here, or by the <c>MF_TERMINAL_GRAPHICS</c>
        /// environment variable (<c>halfblock</c>, <c>kitty</c>, <c>sixel</c>), is used as given and never
        /// overridden by the terminal's answer.
        /// </summary>
        public TerminalGraphicsMode? GraphicsMode { get; set; }

        /// <summary>
        /// Gets or sets the device scale of the rendered window. In half-block mode the terminal supplies only
        /// one pixel per column and two per row, so an 800×600 form's 13px text is unreadable at 1.0; the Kitty and Sixel
        /// modes render at the terminal's real pixel size, where 1.0 is right. Below 1 the form lays
        /// out on a larger logical canvas than the pixels it is squeezed into (0.5 doubles the logical size);
        /// text stays legible only if the terminal is large. Default 1.
        /// </summary>
        public double Scaling { get; set; } = 1.0;

        /// <summary>Gets or sets whether to switch to the alternate screen, so the shell's scrollback is restored on exit. Default true.</summary>
        public bool UseAlternateScreen { get; set; } = true;
    }
}
