namespace Majorsilence.Forms.Terminal
{
    /// <summary>How many colours the terminal can show, which decides the SGR sequences the encoder emits.</summary>
    public enum TerminalColorMode
    {
        /// <summary>24-bit colour: <c>ESC[38;2;R;G;Bm</c>. Used when <c>COLORTERM</c> says <c>truecolor</c> or <c>24bit</c>.</summary>
        TrueColor,
        /// <summary>The xterm 256-colour palette: <c>ESC[38;5;Nm</c>.</summary>
        Ansi256,
        /// <summary>The 16 standard colours: <c>ESC[30-37m</c> / <c>ESC[90-97m</c>.</summary>
        Ansi16,
    }
}
