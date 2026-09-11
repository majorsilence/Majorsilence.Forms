using System.Drawing;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>
    /// The DWM title-bar seams: caption / caption-text / frame-border colours (Windows 11) and the
    /// dark-mode caption flag (Windows 10 1809+). All best-effort — an unsupported attribute on an
    /// older Windows returns a failure HRESULT, which is deliberately ignored: the theme still
    /// applies to the client area.
    /// </summary>
    internal static class Dwm
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport ("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmSetWindowAttribute (IntPtr hwnd, int attribute, ref int value, int size);

        private static int ToColorRef (Color c) => c.R | (c.G << 8) | (c.B << 16);

        public static void ApplyTitleBar (IntPtr hwnd, Color caption, Color captionText, Color? border, bool dark)
        {
            if (hwnd == IntPtr.Zero)
                return;

            var darkFlag = dark ? 1 : 0;
            _ = DwmSetWindowAttribute (hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkFlag, sizeof (int));

            var captionRef = ToColorRef (caption);
            _ = DwmSetWindowAttribute (hwnd, DWMWA_CAPTION_COLOR, ref captionRef, sizeof (int));

            var textRef = ToColorRef (captionText);
            _ = DwmSetWindowAttribute (hwnd, DWMWA_TEXT_COLOR, ref textRef, sizeof (int));

            if (border is { } borderColor) {
                var borderRef = ToColorRef (borderColor);
                _ = DwmSetWindowAttribute (hwnd, DWMWA_BORDER_COLOR, ref borderRef, sizeof (int));
            }
        }
    }
}
