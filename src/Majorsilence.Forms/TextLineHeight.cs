using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The height upstream gives a line of text in a font: GDI's TEXTMETRIC.tmHeight, which is what a
    /// ListBox's ItemHeight and TextRenderer's line height come from.
    /// </summary>
    /// <remarks>
    /// GDI takes it from the font's hinted per-size metrics (the VDMX table), so it does not follow from
    /// the outline: Segoe UI 9pt is 15px where its scaled ascent and descent round to 16, while Microsoft
    /// Sans Serif 8.25pt is 13 where they round to 12. Skia does not apply those tables, so on Windows the
    /// value is asked of GDI itself; elsewhere there is no GDI to match and the rounded metrics stand in.
    /// </remarks>
    internal static class TextLineHeight
    {
        private static readonly ConcurrentDictionary<(string Family, int Pixels, bool Bold, bool Italic), int> cache = new ();
        private static readonly ConcurrentDictionary<(string Family, int Pixels, bool Bold, bool Italic), int> tab_cache = new ();

        /// <summary>The line height, in pixels at 96 DPI, of <paramref name="font"/>.</summary>
        internal static int Of (Majorsilence.Forms.Drawing.Font font)
        {
            var pixels = (int) System.Math.Round (font.SizeInPoints * 96f / 72f);
            return cache.GetOrAdd ((font.Name, pixels, font.Bold, font.Italic), key => Measure (key.Family, key.Pixels, key.Bold, key.Italic, font));
        }

        /// <summary>
        /// The advance, in pixels at 96 DPI, GDI gives a tab in <paramref name="font"/> when it does not expand
        /// tabs, which is how TextRenderer measures one: a drop-down menu reserves it between every caption and
        /// its shortcut column.
        /// </summary>
        /// <remarks>
        /// It is 1px in Microsoft Sans Serif and 1 or 2 in Segoe UI. Skia maps a tab to the font's missing-glyph
        /// box (4 to 10px), so it cannot stand in; elsewhere there is no GDI and 1 does.
        /// </remarks>
        internal static int TabAdvanceOf (Majorsilence.Forms.Drawing.Font font)
        {
            var pixels = (int) System.Math.Round (font.SizeInPoints * 96f / 72f);
            return tab_cache.GetOrAdd ((font.Name, pixels, font.Bold, font.Italic),
                key => OperatingSystemCompat.IsWindows () && TryGdi (key.Family, key.Pixels, key.Bold, key.Italic, out _, out var tab) ? tab : 1);
        }

        private static int Measure (string family, int pixels, bool bold, bool italic, Majorsilence.Forms.Drawing.Font font)
        {
            if (OperatingSystemCompat.IsWindows () && TryGdi (family, pixels, bold, italic, out var height, out _))
                return height;

            using var sk = new SkiaSharp.SKFont (TypefaceCache.Resolve (font), pixels);
            sk.GetFontMetrics (out var metrics);
            return (int) (System.Math.Round (-metrics.Ascent) + System.Math.Round (metrics.Descent));
        }

        private static bool TryGdi (string family, int pixels, bool bold, bool italic, out int height, out int tab)
        {
            height = 0;
            tab = 0;

            try {
                var dc = CreateCompatibleDC (System.IntPtr.Zero);
                if (dc == System.IntPtr.Zero)
                    return false;

                try {
                    // A negative height asks for the em height in pixels, which is what a point size is.
                    var hfont = CreateFontW (-pixels, 0, 0, 0, bold ? 700 : 400, italic ? 1u : 0u, 0, 0, 1, 0, 0, 0, 0, family);
                    if (hfont == System.IntPtr.Zero)
                        return false;

                    try {
                        var old = SelectObject (dc, hfont);
                        var ok = GetTextMetricsW (dc, out var tm);
                        var tab_ok = GetTextExtentPoint32W (dc, "	", 1, out var tab_size);
                        SelectObject (dc, old);

                        if (!ok || tm.tmHeight <= 0 || !tab_ok)
                            return false;

                        height = tm.tmHeight;
                        tab = tab_size.cx;
                        return true;
                    } finally {
                        DeleteObject (hfont);
                    }
                } finally {
                    DeleteDC (dc);
                }
            } catch (System.Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) {
                return false;
            }
        }

        [StructLayout (LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TEXTMETRICW
        {
            public int tmHeight, tmAscent, tmDescent, tmInternalLeading, tmExternalLeading, tmAveCharWidth,
                tmMaxCharWidth, tmWeight, tmOverhang, tmDigitizedAspectX, tmDigitizedAspectY;
            public char tmFirstChar, tmLastChar, tmDefaultChar, tmBreakChar;
            public byte tmItalic, tmUnderlined, tmStruckOut, tmPitchAndFamily, tmCharSet;
        }

        [DllImport ("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern System.IntPtr CreateFontW (int height, int width, int escapement, int orientation, int weight,
            uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision,
            uint quality, uint pitchAndFamily, string face);

        [DllImport ("gdi32.dll")]
        private static extern System.IntPtr CreateCompatibleDC (System.IntPtr dc);

        [DllImport ("gdi32.dll")]
        private static extern bool DeleteDC (System.IntPtr dc);

        [DllImport ("gdi32.dll")]
        private static extern System.IntPtr SelectObject (System.IntPtr dc, System.IntPtr obj);

        [DllImport ("gdi32.dll")]
        private static extern bool DeleteObject (System.IntPtr obj);

        [DllImport ("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetTextMetricsW (System.IntPtr dc, out TEXTMETRICW metrics);

        [StructLayout (LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx, cy;
        }

        [DllImport ("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetTextExtentPoint32W (System.IntPtr dc, string text, int length, out SIZE size);
    }
}
