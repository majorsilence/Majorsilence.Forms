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
        /// Translates an HTML color string to a <see cref="Color"/>.
        /// Supports #RGB, #RRGGBB, #AARRGGBB, and named colors.
        /// </summary>
        public static Color FromHtml (string html)
        {
            if (string.IsNullOrEmpty (html))
                throw new ArgumentException ("Invalid HTML color code", nameof (html));

            var s = html.StartsWith ('#') ? html.Substring (1) : html;

            try
            {
                if (s.Length == 3)
                    return Color.FromArgb (
                        Convert.ToInt32 (new string (s[0], 2), 16),
                        Convert.ToInt32 (new string (s[1], 2), 16),
                        Convert.ToInt32 (new string (s[2], 2), 16));

                if (s.Length == 6)
                    return Color.FromArgb (
                        Convert.ToInt32 (s.Substring (0, 2), 16),
                        Convert.ToInt32 (s.Substring (2, 2), 16),
                        Convert.ToInt32 (s.Substring (4, 2), 16));

                if (s.Length == 8)
                    return Color.FromArgb (
                        Convert.ToInt32 (s.Substring (0, 2), 16),
                        Convert.ToInt32 (s.Substring (2, 2), 16),
                        Convert.ToInt32 (s.Substring (4, 2), 16),
                        Convert.ToInt32 (s.Substring (6, 2), 16));
            }
            catch (FormatException) { }

            return Color.FromName (html);
        }

        /// <summary>Translates a <see cref="Color"/> to an HTML color string (#RRGGBB or #AARRGGBB).</summary>
        public static string ToHtml (Color color)
        {
            if (color == Color.Empty)
                return string.Empty;
            if (color.A < 255)
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    
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
