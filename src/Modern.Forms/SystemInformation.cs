using System.Drawing;

namespace Modern.Forms
{
    /// <summary>
    /// WinForms compatibility: provides system-level information about the user's computer.
    /// Values are approximations; for exact data use <see cref="Screen"/> or Avalonia's display APIs.
    /// </summary>
    public static class SystemInformation
    {
        /// <summary>Gets the height of the horizontal scroll bar in pixels.</summary>
        public static int HorizontalScrollBarHeight => 17;

        /// <summary>Gets the width of the vertical scroll bar in pixels.</summary>
        public static int VerticalScrollBarWidth => 17;

        /// <summary>Gets the height of the horizontal scroll bar thumb.</summary>
        public static int HorizontalScrollBarThumbWidth => 18;

        /// <summary>Gets the height of the vertical scroll bar thumb.</summary>
        public static int VerticalScrollBarThumbHeight => 18;

        /// <summary>Gets the bounds of the primary screen's working area (excludes taskbar).</summary>
        public static Rectangle WorkingArea =>
            Screen.PrimaryScreen?.WorkingArea ?? new Rectangle (0, 0, 1920, 1080);

        /// <summary>Gets the size of the primary monitor.</summary>
        public static Size PrimaryMonitorSize =>
            Screen.PrimaryScreen is Screen s ? new Size (s.Bounds.Width, s.Bounds.Height) : new Size (1920, 1080);

        /// <summary>Gets the height of a single-line menu bar in pixels.</summary>
        public static int MenuHeight => 24;

        /// <summary>Gets the default height of a caption bar (title bar).</summary>
        public static int CaptionHeight => 30;

        /// <summary>Gets the double-click time in milliseconds.</summary>
        public static int DoubleClickTime => 500;

        /// <summary>Gets the double-click rectangle size (area in which a second click is a double-click).</summary>
        public static Size DoubleClickSize => new Size (4, 4);

        /// <summary>Gets the size of a small icon.</summary>
        public static Size SmallIconSize => new Size (16, 16);

        /// <summary>Gets the size of a large icon.</summary>
        public static Size IconSize => new Size (32, 32);

        /// <summary>Gets whether the mouse is present. Always true in Modern.Forms.</summary>
        public static bool MousePresent => true;

        /// <summary>Gets the number of mouse buttons.</summary>
        public static int MouseButtons => 3;

        /// <summary>Gets whether a drag operation has started, based on the drag threshold.</summary>
        public static Size DragSize => new Size (4, 4);

        /// <summary>Gets the number of display monitors.</summary>
        public static int MonitorCount => Screen.AllScreens.Length;

        /// <summary>Gets the number of lines to scroll when the mouse wheel is rotated.</summary>
        public static int MouseWheelScrollLines => 3;

        /// <summary>Gets the screen size of the default cursor in pixels.</summary>
        public static Size CursorSize => new Size (32, 32);

        /// <summary>Gets the width of the border drawn around a window border in pixels.</summary>
        public static int BorderSize => 1;

        /// <summary>Gets whether the operating system is a network-enabled version.</summary>
        public static bool Network => true;

        /// <summary>Gets whether the user has swapped the meaning of the left and right mouse buttons.</summary>
        public static bool MouseButtonsSwapped => false;

        /// <summary>Gets whether a slow machine flag is set. Always false in Modern.Forms.</summary>
        public static bool SlowMachine => false;

        /// <summary>Gets whether the computer is running on battery power.</summary>
        public static PowerStatus PowerStatus => new PowerStatus ();

        /// <summary>Gets the height of the border for a window without sizing capabilities.</summary>
        public static int FixedFrameBorderSize => 3;

        /// <summary>Gets the thickness of the frame border around a sizable window.</summary>
        public static int FrameBorderSize => 4;

        /// <summary>Gets the thickness of the sizing border around a sizable window.</summary>
        public static int SizingBorderWidth => 4;

        /// <summary>Gets the bounds of the virtual screen (combined area of all monitors). Stub in Modern.Forms.</summary>
        public static System.Drawing.Rectangle VirtualScreen => new System.Drawing.Rectangle (0, 0, PrimaryMonitorSize.Width, PrimaryMonitorSize.Height);

        /// <summary>Gets the current screen orientation. Stub in Modern.Forms.</summary>
        public static ScreenOrientation ScreenOrientation => ScreenOrientation.Angle0;

        /// <summary>Gets the minimum width of a window in pixels.</summary>
        public static int MinimumWindowSize => 112;

        /// <summary>Gets the maximum number of elements in a single menu.</summary>
        public static int MaxWindowTrackSize => int.MaxValue;
    }

    /// <summary>
    /// WinForms compatibility stub for PowerStatus information.
    /// </summary>
    public class PowerStatus
    {
        /// <summary>Gets the current battery charge status. Always High in Modern.Forms.</summary>
        public BatteryChargeStatus BatteryChargeStatus => BatteryChargeStatus.High;

        /// <summary>Gets the approximate amount of full battery charge remaining as a percentage.</summary>
        public float BatteryLifePercent => 1.0f;

        /// <summary>Gets the reported remaining battery lifetime in seconds. Returns -1 (unknown).</summary>
        public int BatteryLifeRemaining => -1;

        /// <summary>Gets the reported full battery lifetime in seconds. Returns -1 (unknown).</summary>
        public int BatteryFullLifetime => -1;

        /// <summary>Gets the current power line status. Always Online in Modern.Forms.</summary>
        public PowerLineStatus PowerLineStatus => PowerLineStatus.Online;
    }

    /// <summary>Specifies the battery charge status.</summary>
    public enum BatteryChargeStatus
    {
        /// <summary>Indicates a high level of battery charge.</summary>
        High = 1,
        /// <summary>Indicates a low level of battery charge.</summary>
        Low = 2,
        /// <summary>Indicates a critically low level of battery charge.</summary>
        Critical = 4,
        /// <summary>Indicates the battery is charging.</summary>
        Charging = 8,
        /// <summary>Indicates no system battery is present.</summary>
        NoSystemBattery = 128,
        /// <summary>Indicates an unknown battery charge status.</summary>
        Unknown = 255
    }

    /// <summary>Specifies the screen orientation.</summary>
    public enum ScreenOrientation
    {
        /// <summary>0 degrees (landscape).</summary>
        Angle0,
        /// <summary>90 degrees (portrait).</summary>
        Angle90,
        /// <summary>180 degrees (landscape flipped).</summary>
        Angle180,
        /// <summary>270 degrees (portrait flipped).</summary>
        Angle270
    }

    /// <summary>Specifies the power line status.</summary>
    public enum PowerLineStatus
    {
        /// <summary>The system is not connected to AC power.</summary>
        Offline = 0,
        /// <summary>The system is connected to AC power.</summary>
        Online = 1,
        /// <summary>The power line status is unknown.</summary>
        Unknown = 255
    }
}
