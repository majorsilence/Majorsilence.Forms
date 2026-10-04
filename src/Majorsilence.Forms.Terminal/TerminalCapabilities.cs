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
            if (term.Contains ("256color", StringComparison.OrdinalIgnoreCase))
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
    }
}
