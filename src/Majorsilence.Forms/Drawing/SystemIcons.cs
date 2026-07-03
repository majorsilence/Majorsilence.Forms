using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// The system icons. Cross-platform replacement for System.Drawing.SystemIcons — there are no OS
    /// system icons to load uniformly across platforms, so each icon is a simple drawn placeholder
    /// (a colored disc keyed to the icon's meaning). Cached per icon.
    /// </summary>
    public static class SystemIcons
    {
        private static readonly object gate = new ();

        private static Icon? application, asterisk, error, exclamation, hand, information, question, shield, warning, winLogo;

        /// <summary>Gets the application icon.</summary>
        public static Icon Application => application ??= Make (new SKColor (0x55, 0x55, 0x55));

        /// <summary>Gets the asterisk (information) icon.</summary>
        public static Icon Asterisk => asterisk ??= Make (new SKColor (0x1E, 0x90, 0xFF));

        /// <summary>Gets the error icon.</summary>
        public static Icon Error => error ??= Make (new SKColor (0xD3, 0x2F, 0x2F));

        /// <summary>Gets the exclamation (warning) icon.</summary>
        public static Icon Exclamation => exclamation ??= Make (new SKColor (0xFF, 0xA0, 0x00));

        /// <summary>Gets the hand (error) icon.</summary>
        public static Icon Hand => hand ??= Make (new SKColor (0xD3, 0x2F, 0x2F));

        /// <summary>Gets the information icon.</summary>
        public static Icon Information => information ??= Make (new SKColor (0x1E, 0x90, 0xFF));

        /// <summary>Gets the question icon.</summary>
        public static Icon Question => question ??= Make (new SKColor (0x19, 0x76, 0xD2));

        /// <summary>Gets the shield icon.</summary>
        public static Icon Shield => shield ??= Make (new SKColor (0x2E, 0x7D, 0x32));

        /// <summary>Gets the warning icon.</summary>
        public static Icon Warning => warning ??= Make (new SKColor (0xFF, 0xA0, 0x00));

        /// <summary>Gets the Windows logo icon.</summary>
        public static Icon WinLogo => winLogo ??= Make (new SKColor (0x00, 0x78, 0xD4));

        private static Icon Make (SKColor color)
        {
            lock (gate)
            {
                var bitmap = new SKBitmap (32, 32, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas (bitmap);
                canvas.Clear (SKColors.Transparent);
                using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
                canvas.DrawCircle (16, 16, 14, paint);
                return new Icon (bitmap);
            }
        }
    }
}
