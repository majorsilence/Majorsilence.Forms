using System.Drawing;
using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a display device on which controls and forms can be drawn.
    /// Backed by a backend-neutral <see cref="ScreenInfo"/> snapshot.
    /// </summary>
    public partial class Screen
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

        /// <summary>Gets the screen that contains the largest part of the specified control.</summary>
        /// <remarks>
        /// Upstream asks for the monitor nearest the control's window (<c>MonitorFromWindow</c>,
        /// Screen.cs). This answered the primary screen whatever the control, so a dialog sized against
        /// <c>Screen.FromControl (this).WorkingArea</c> on a second monitor opened on the first.
        /// </remarks>
        public static Screen? FromControl (Control control)
        {
            var origin = control.PointToScreen (Point.Empty);
            var far = control.PointToScreen (new Point (control.Width, control.Height));

            return FromRectangle (Rectangle.FromLTRB (origin.X, origin.Y, far.X, far.Y));
        }

        /// <summary>Gets the screen showing the largest part of the given window.</summary>
        /// <remarks>Overload for <see cref="WindowBase"/>: a Form is not a Control here, and asking which
        /// screen a window sits on -- to size a dialog against the working area -- is the common case.
        /// A form drawn inside another answers with the window presenting it.</remarks>
        public static Screen? FromControl (WindowBase window)
            => FromRectangle (window.PresentationWindow.ScreenBounds);

        /// <summary>Gets the screen that has the largest intersection with the specified rectangle.</summary>
        /// <remarks>
        /// As upstream's <c>MonitorFromRect</c> with <c>MONITOR_DEFAULTTONEAREST</c>: the screen sharing
        /// the most area with the rectangle; one touching none of them goes to the screen holding its
        /// centre, or the primary screen.
        /// </remarks>
        public static Screen? FromRectangle (System.Drawing.Rectangle rect)
        {
            Screen? best = null;
            long best_area = 0;

            foreach (var screen in AllScreens) {
                var overlap = Rectangle.Intersect (screen.Bounds, rect);
                long area = (long) overlap.Width * overlap.Height;

                if (area > best_area) {
                    best = screen;
                    best_area = area;
                }
            }

            return best ?? FromPoint (new Point (rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
        }
    }
}
