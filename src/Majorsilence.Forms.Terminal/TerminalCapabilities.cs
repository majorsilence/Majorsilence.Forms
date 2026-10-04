using System;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>What the attached terminal can show, read from its environment.</summary>
    internal static class TerminalCapabilities
    {
        public static TerminalColorMode DetectColorMode (Func<string, string?> getEnv)
        {
            var colorTerm = getEnv ("COLORTERM");
            if (colorTerm is not null && (colorTerm.Contains ("truecolor", StringComparison.OrdinalIgnoreCase)
                                          || colorTerm.Contains ("24bit", StringComparison.OrdinalIgnoreCase)))
                return TerminalColorMode.TrueColor;

            var term = getEnv ("TERM") ?? string.Empty;
            // Plain "xterm" is what real xterm sets (and what many ssh sessions pass on), and every xterm built
            // this century has 256 colours, though terminfo's "xterm" entry says 8. Found by running in real
            // xterm, where trusting terminfo left a blue title bar cyan.
            if (term.Contains ("256color", StringComparison.OrdinalIgnoreCase) || term == "xterm")
                return TerminalColorMode.Ansi256;

            // Windows Terminal and the modern conhost set neither variable but are truecolor.
            if (getEnv ("WT_SESSION") is not null)
                return TerminalColorMode.TrueColor;

            return TerminalColorMode.Ansi16;
        }

        /// <summary>The mode <c>MF_TERMINAL_GRAPHICS</c> forces, or null when it is unset or not a mode name. A forced mode is never second-guessed by the terminal query.</summary>
        public static TerminalGraphicsMode? ExplicitGraphicsMode (Func<string, string?> getEnv)
            => getEnv ("MF_TERMINAL_GRAPHICS")?.Trim ().ToLowerInvariant () switch {
                "halfblock" => TerminalGraphicsMode.HalfBlock,
                "kitty" => TerminalGraphicsMode.Kitty,
                "sixel" => TerminalGraphicsMode.Sixel,
                _ => null,
            };

        /// <summary>
        /// Picks how to show frames before the terminal has been asked (and when it cannot be: no input). Graphics are used only where the environment positively identifies a
        /// terminal that supports them: guessing wrong prints escape-sequence garbage, while half-blocks
        /// work everywhere. With input available the backend then asks the terminal (<see cref="TerminalProbe"/>)
        /// and the answer replaces this guess.
        /// </summary>
        public static TerminalGraphicsMode DetectGraphicsMode (Func<string, string?> getEnv)
        {
            if (ExplicitGraphicsMode (getEnv) is { } forced)
                return forced;

            // A multiplexer swallows or mangles graphics escapes unless it is configured to pass them through.
            if (getEnv ("TMUX") is not null || getEnv ("STY") is not null)
                return TerminalGraphicsMode.HalfBlock;

            var term = getEnv ("TERM") ?? string.Empty;
            var program = getEnv ("TERM_PROGRAM") ?? string.Empty;

            if (getEnv ("KITTY_WINDOW_ID") is not null
                || term == "xterm-kitty"
                || term.Contains ("ghostty", StringComparison.OrdinalIgnoreCase)
                || program is "ghostty" or "WezTerm")
                return TerminalGraphicsMode.Kitty;

            if (term.Contains ("foot", StringComparison.OrdinalIgnoreCase)
                || term.Contains ("mlterm", StringComparison.OrdinalIgnoreCase)
                || term.Contains ("contour", StringComparison.OrdinalIgnoreCase)
                || term.Contains ("sixel", StringComparison.OrdinalIgnoreCase)
                || program == "iTerm.app")
                return TerminalGraphicsMode.Sixel;

            return TerminalGraphicsMode.HalfBlock;
        }

        /// <summary>The largest Sixel image <c>MF_TERMINAL_SIXEL_MAX</c> (<c>WxH</c> in pixels) says the terminal draws, or null when unset or malformed.</summary>
        public static (int W, int H)? ExplicitSixelLimit (Func<string, string?> getEnv)
        {
            var parts = getEnv ("MF_TERMINAL_SIXEL_MAX")?.Trim ().Split ('x', 'X');
            return parts is { Length: 2 } && int.TryParse (parts[0], out var w) && int.TryParse (parts[1], out var h) && w > 0 && h > 0
                ? (w, h)
                : null;
        }

        /// <summary>xterm's default <c>maxGraphicsSize</c>, which it applies silently by cutting the image off.</summary>
        internal static readonly (int W, int H) XTermDefaultSixelLimit = (1000, 1000);
    }
}
