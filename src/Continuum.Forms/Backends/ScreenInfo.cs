using System.Drawing;

namespace Continuum.Forms.Backends
{
    /// <summary>
    /// A backend-neutral snapshot of a display device, returned by
    /// <see cref="IPlatformBackend.GetScreens"/>. Coordinates are in device pixels.
    /// </summary>
    public readonly struct ScreenInfo
    {
        /// <summary>Initializes a new <see cref="ScreenInfo"/>.</summary>
        public ScreenInfo (string deviceName, Rectangle bounds, Rectangle workingArea, bool isPrimary)
        {
            DeviceName = deviceName;
            Bounds = bounds;
            WorkingArea = workingArea;
            IsPrimary = isPrimary;
        }

        /// <summary>The display device name.</summary>
        public string DeviceName { get; }

        /// <summary>The full bounds of the display, in device pixels.</summary>
        public Rectangle Bounds { get; }

        /// <summary>The usable working area (excluding taskbars/docks), in device pixels.</summary>
        public Rectangle WorkingArea { get; }

        /// <summary>Whether this is the primary display.</summary>
        public bool IsPrimary { get; }
    }
}
