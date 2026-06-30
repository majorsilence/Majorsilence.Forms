using System;
using System.Drawing;

namespace Majorsilence.Drawing
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
    }
}
