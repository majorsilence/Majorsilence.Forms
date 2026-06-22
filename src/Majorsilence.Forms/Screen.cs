using System.Drawing;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a display device on which controls and forms can be drawn.
    /// Backed by a backend-neutral <see cref="ScreenInfo"/> snapshot.
    /// </summary>
    public class Screen
    {
        private readonly ScreenInfo _screen;

        internal Screen (ScreenInfo screen)
        {
            _screen = screen;
        }

        /// <summary>Gets the name of the screen device.</summary>
        public string DeviceName => _screen.DeviceName;

        /// <summary>Gets the bounds of the screen in device pixels.</summary>
        public Rectangle Bounds => _screen.Bounds;

        /// <summary>Gets the working area of the screen (excluding taskbar).</summary>
        public Rectangle WorkingArea => _screen.WorkingArea;

        /// <summary>Gets whether this is the primary screen.</summary>
        public bool Primary => _screen.IsPrimary;

        /// <summary>Gets the pixel depth (always 32 for modern displays).</summary>
        public int BitsPerPixel => 32;

        /// <summary>Gets all available screens.</summary>
        public static Screen[] AllScreens
            => Platform.Backend.GetScreens ().Select (s => new Screen (s)).ToArray ();

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
