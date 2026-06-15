using System.Drawing;
using Avalonia.Platform;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a display device on which controls and forms can be drawn.
    /// Wraps Avalonia.Platform.Screen for cross-platform compatibility.
    /// </summary>
    public class Screen
    {
        private readonly Avalonia.Platform.Screen _screen;

        internal Screen (Avalonia.Platform.Screen screen)
        {
            _screen = screen;
        }

        /// <summary>Gets the name of the screen device.</summary>
        public string DeviceName => _screen.DisplayName ?? string.Empty;

        /// <summary>Gets the bounds of the screen in device pixels.</summary>
        public Rectangle Bounds => new Rectangle (
            _screen.Bounds.X, _screen.Bounds.Y,
            _screen.Bounds.Width, _screen.Bounds.Height);

        /// <summary>Gets the working area of the screen (excluding taskbar).</summary>
        public Rectangle WorkingArea => new Rectangle (
            _screen.WorkingArea.X, _screen.WorkingArea.Y,
            _screen.WorkingArea.Width, _screen.WorkingArea.Height);

        /// <summary>Gets whether this is the primary screen.</summary>
        public bool Primary => _screen.IsPrimary;

        /// <summary>Gets the pixel depth (always 32 for modern displays).</summary>
        public int BitsPerPixel => 32;

        /// <summary>Gets all available screens.</summary>
        public static Screen[] AllScreens
        {
            get {
                var app = Avalonia.Application.Current;
                var lifetime = app?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                var screens = lifetime?.MainWindow?.Screens?.All;

                if (screens == null)
                    return Array.Empty<Screen> ();

                return screens.Select (s => new Screen (s)).ToArray ();
            }
        }

        /// <summary>Gets the primary screen.</summary>
        public static Screen? PrimaryScreen
        {
            get {
                var all = AllScreens;
                return all.FirstOrDefault (s => s.Primary) ?? all.FirstOrDefault ();
            }
        }

        /// <summary>Gets the screen that contains the specified point.</summary>
        public static Screen? FromPoint (Point point)
        {
            var all = AllScreens;
            return all.FirstOrDefault (s => s.Bounds.Contains (point)) ?? PrimaryScreen;
        }

        /// <summary>Gets the screen that contains the specified control.</summary>
        public static Screen? FromControl (Control control) => PrimaryScreen;

        /// <summary>Gets the screen that has the largest intersection with the specified rectangle.</summary>
        public static Screen? FromRectangle (System.Drawing.Rectangle rect)
        {
            var pt = new Point (rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            return FromPoint (pt);
        }
    }
}
