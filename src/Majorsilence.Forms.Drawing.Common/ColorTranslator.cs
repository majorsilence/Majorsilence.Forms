using System;
using System.Drawing;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Translates colors to and from HTML color notation. Cross-platform replacement for
    /// <c>System.Drawing.ColorTranslator</c> (which is Windows-only in System.Drawing.Common).
    /// </summary>
    public static class ColorTranslator
    {
        /// <summary>
        /// Translates an HTML color string to a <see cref="Color"/>, as <c>System.Drawing.ColorTranslator.FromHtml</c> does.
        /// </summary>
        /// <remarks>
        /// Upstream's rules, measured on .NET 10: null, empty or blank gives <see cref="Color.Empty"/>; <c>#RGB</c> and
        /// <c>#RRGGBB</c> are hex; <c>LightGrey</c> is <see cref="Color.LightGray"/>; a CSS system-colour keyword
        /// (<c>buttonface</c>, <c>infotext</c>, ...) gives that system colour; anything else goes through the colour
        /// converter -- a known colour name in any case, an integer (decimal, or hex after <c>#</c> or <c>0x</c>, so
        /// <c>#AARRGGBB</c> carries its alpha), or two to four comma-separated channels. An invalid hex digit is a
        /// <see cref="FormatException"/>; anything else unrecognised an <see cref="ArgumentException"/>.
        /// This threw on an empty string -- the value a blank colour column holds -- read <c>RRGGBB</c> without its
        /// <c>#</c> as hex where upstream reads a decimal number, and knew no system-colour keywords.
        /// </remarks>
        public static Color FromHtml (string html)
        {
            if (string.IsNullOrEmpty (html))
                return Color.Empty;

            if (html[0] == '#' && (html.Length == 7 || html.Length == 4)) {
                if (html.Length == 7)
                    return Color.FromArgb (
                        Convert.ToInt32 (html.Substring (1, 2), 16),
                        Convert.ToInt32 (html.Substring (3, 2), 16),
                        Convert.ToInt32 (html.Substring (5, 2), 16));

                return Color.FromArgb (
                    Convert.ToInt32 (new string (html[1], 2), 16),
                    Convert.ToInt32 (new string (html[2], 2), 16),
                    Convert.ToInt32 (new string (html[3], 2), 16));
            }

            if (string.Equals (html, "LightGrey", StringComparison.OrdinalIgnoreCase))
                return Color.LightGray;

            var system = SystemColorFromKeyword (html.ToLowerInvariant ());

            if (system is { } keyword)
                return Color.FromName (keyword);

            return ConvertFromString (html);
        }

        // The colour converter's string form: a known name, an integer, or comma-separated channels.
        private static Color ConvertFromString (string html)
        {
            var text = html.Trim ();

            if (text.Length == 0)
                return Color.Empty;

            var named = Color.FromName (text);

#if NETSTANDARD2_0
            // No IsKnownColor here: an unknown name comes back with every channel 0, which no known colour has
            // (Transparent is 0x00FFFFFF).
            if (named.ToArgb () != 0)
#else
            if (named.IsKnownColor)
#endif
                return named;

            var parts = text.Split (',');

            if (parts.Length == 1)
                return Color.FromArgb (ParseInt (text, html));

            var values = new int[parts.Length];

            for (var i = 0; i < parts.Length; i++)
                values[i] = ParseInt (parts[i].Trim (), html);

            return values.Length switch {
                3 => Color.FromArgb (values[0], values[1], values[2]),
                4 => Color.FromArgb (values[0], values[1], values[2], values[3]),
                _ => throw new ArgumentException ($"{html} is not a valid value for Int32.", nameof (html)),
            };
        }

        private static int ParseInt (string text, string html)
        {
            try {
#if NETSTANDARD2_0
                if (text.StartsWith ("#", StringComparison.Ordinal))
#else
                if (text.StartsWith ('#'))
#endif
                    return Convert.ToInt32 (text.Substring (1), 16);

                if (text.StartsWith ("0x", StringComparison.OrdinalIgnoreCase) || text.StartsWith ("&h", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32 (text.Substring (2), 16);

                return int.Parse (text, System.Globalization.CultureInfo.CurrentCulture);
            } catch (Exception ex) when (ex is FormatException or OverflowException) {
                throw new ArgumentException ($"{html} is not a valid value for Int32.", nameof (html), ex);
            }
        }

        // The CSS2 system-colour keywords FromHtml reads, as System.Drawing's ColorTranslator maps them.
        private static string? SystemColorFromKeyword (string keyword) => keyword switch {
            "activeborder" => "ActiveBorder",
            "activecaption" => "ActiveCaption",
            "appworkspace" => "AppWorkspace",
            "background" => "Desktop",
            "buttonface" => "Control",
            "buttonhighlight" => "ControlLightLight",
            "buttonshadow" => "ControlDark",
            "buttontext" => "ControlText",
            "captiontext" => "ActiveCaptionText",
            "graytext" => "GrayText",
            "highlight" => "Highlight",
            "highlighttext" => "HighlightText",
            "inactiveborder" => "InactiveBorder",
            "inactivecaption" => "InactiveCaption",
            "inactivecaptiontext" => "InactiveCaptionText",
            "infobackground" => "Info",
            "infotext" => "InfoText",
            "menu" => "Menu",
            "menutext" => "MenuText",
            "scrollbar" => "ScrollBar",
            "threeddarkshadow" => "ControlDarkDark",
            "threedface" => "Control",
            "threedhighlight" => "ControlLight",
            "threedlightshadow" => "ControlLightLight",
            "window" => "Window",
            "windowframe" => "WindowFrame",
            "windowtext" => "WindowText",
            _ => null,
        };

        /// <summary>
        /// Translates a <see cref="Color"/> to an HTML color string: the CSS system-colour keyword for a
        /// system color, the name for any other known color, otherwise <c>#RRGGBB</c>.
        /// </summary>
        /// <remarks>
        /// GFX-40: this always produced hex, and an 8-digit <c>#AARRGGBB</c> form for translucent colours
        /// that HTML does not have. It follows <c>System.Drawing.ColorTranslator.ToHtml</c>'s three
        /// branches now -- including its quirks, which callers comparing output depend on: a system
        /// colour with no CSS keyword (<c>ButtonFace</c>, <c>ButtonHighlight</c>, <c>ButtonShadow</c>)
        /// gives an empty string, <c>LightGray</c> is spelled <c>LightGrey</c>, and alpha is dropped.
        /// </remarks>
        public static string ToHtml (Color color)
        {
            if (color.IsEmpty)
                return string.Empty;

            // By name rather than KnownColor, which netstandard2.0 does not have; a system colour's name
            // is its KnownColor member's name. netstandard2.0's Color cannot report system-colour identity
            // at all, so that build falls through to the colour's name.
#if !NETSTANDARD2_0
            if (color.IsSystemColor)
                return SystemColorKeyword (color.Name);
#endif

            if (color.IsNamedColor)
                return color == Color.LightGray ? "LightGrey" : color.Name;

            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // The CSS2 system-colour keywords, as System.Drawing's ColorTranslator maps them.
        private static string SystemColorKeyword (string name) => name switch {
            "ActiveBorder" => "activeborder",
            "GradientActiveCaption" or "ActiveCaption" => "activecaption",
            "AppWorkspace" => "appworkspace",
            "Desktop" => "background",
            "Control" or "ControlLight" => "buttonface",
            "ControlDark" => "buttonshadow",
            "ControlText" => "buttontext",
            "ActiveCaptionText" => "captiontext",
            "GrayText" => "graytext",
            "HotTrack" or "Highlight" => "highlight",
            "MenuHighlight" or "HighlightText" => "highlighttext",
            "InactiveBorder" => "inactiveborder",
            "GradientInactiveCaption" or "InactiveCaption" => "inactivecaption",
            "InactiveCaptionText" => "inactivecaptiontext",
            "Info" => "infobackground",
            "InfoText" => "infotext",
            "MenuBar" or "Menu" => "menu",
            "MenuText" => "menutext",
            "ScrollBar" => "scrollbar",
            "ControlDarkDark" => "threeddarkshadow",
            "ControlLightLight" => "buttonhighlight",
            "Window" => "window",
            "WindowFrame" => "windowframe",
            "WindowText" => "windowtext",
            _ => string.Empty,
        };
    
        /// <summary>
        /// Converts a color to a Win32 COLORREF, which packs the channels as 0x00BBGGRR.
        /// </summary>
        /// <remarks>
        /// Despite the name this is meaningful cross-platform: it is a plain byte-order convention, and
        /// migrated code round-trips colors through it (persisted settings, interop structs) on every OS.
        /// Alpha is dropped, as COLORREF has no alpha channel.
        /// </remarks>
        public static int ToWin32 (System.Drawing.Color color) => color.R | (color.G << 8) | (color.B << 16);

        /// <summary>Converts a Win32 COLORREF (0x00BBGGRR) to a color.</summary>
        public static System.Drawing.Color FromWin32 (int value)
            => System.Drawing.Color.FromArgb (value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF);

        /// <summary>Converts a color to an OLE color value, which uses the same 0x00BBGGRR packing.</summary>
        public static int ToOle (System.Drawing.Color color) => ToWin32 (color);

        /// <summary>Converts an OLE color value to a color.</summary>
        public static System.Drawing.Color FromOle (int value) => FromWin32 (value);
}
}
