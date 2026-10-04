using System;
using System.Runtime.InteropServices;

namespace Majorsilence.Forms.Terminal
{
    /// <summary>
    /// The terminal's size in pixels as the pty reports it (<c>TIOCGWINSZ</c>), which gives the cell size
    /// without a round trip and from terminals that refuse the <c>ESC[16t</c> query: xterm leaves window
    /// operations off by default, yet fills in the pixel fields.
    /// </summary>
    internal static class TerminalWindowSize
    {
        [StructLayout (LayoutKind.Sequential)]
        private struct WinSize
        {
            public ushort Rows, Cols, XPixel, YPixel;
        }

        // Linux's request number. macOS differs, and its arm64 ABI passes the variadic ioctl's argument
        // differently from a fixed-argument P/Invoke, so only Linux asks; elsewhere the query reply is the source.
        private const ulong TiocgwinszLinux = 0x5413;

        [DllImport ("libc", SetLastError = true)]
        private static extern int ioctl (int fd, ulong request, out WinSize size);

        /// <summary>Gets the cell size in pixels from the pty, or null when the terminal does not say (zero pixel fields) or this platform cannot ask.</summary>
        public static (int W, int H)? TryGetCellPixels ()
        {
            if (!OperatingSystem.IsLinux ())
                return null;

            try {
                // Standard output is the terminal we are drawing on.
                return ioctl (1, TiocgwinszLinux, out var size) == 0
                    ? CellFromWinSize (size.Cols, size.Rows, size.XPixel, size.YPixel)
                    : null;
            } catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) {
                return null;
            }
        }

        /// <summary>Divides the terminal's pixel size by its cell grid; null when any part is missing, since a terminal that does not know its pixel size reports zeros.</summary>
        internal static (int W, int H)? CellFromWinSize (int cols, int rows, int xPixel, int yPixel)
        {
            if (cols <= 0 || rows <= 0 || xPixel <= 0 || yPixel <= 0)
                return null;

            var w = xPixel / cols;
            var h = yPixel / rows;
            return w > 0 && h > 0 ? (w, h) : null;
        }
    }
}
