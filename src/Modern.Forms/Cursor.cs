using Avalonia.Input;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a mouse cursor.
    /// </summary>
    public class Cursor : IDisposable
    {
        internal Avalonia.Input.Cursor cursor;
        private bool _disposed;

        internal Cursor (StandardCursorType type)
        {
            cursor = new Avalonia.Input.Cursor (type);
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (_disposed) return;
            _disposed = true;
            cursor.Dispose ();
            GC.SuppressFinalize (this);
        }

        /// <summary>
        /// The default cursor provided by the operating system.
        /// </summary>
        public static Cursor Default => Cursors.Arrow;

        /// <summary>Gets or sets the current mouse cursor. Stub in Modern.Forms.</summary>
        public static Cursor? Current { get; set; }

        /// <summary>Hides the cursor. Stub in Modern.Forms.</summary>
        public static void Hide () { }

        /// <summary>Shows the cursor. Stub in Modern.Forms.</summary>
        public static void Show () { }

        /// <summary>Gets or sets the cursor's position in screen coordinates. Stub in Modern.Forms.</summary>
        public static System.Drawing.Point Position { get; set; }

        /// <summary>Gets or sets whether the cursor is clipped to a rectangle. Stub in Modern.Forms.</summary>
        public static System.Drawing.Rectangle Clip { get; set; }
    }
}
