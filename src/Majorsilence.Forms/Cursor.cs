using Majorsilence.Forms.Backends;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a mouse cursor.
    /// </summary>
    public partial class Cursor : IDisposable
    {
        internal readonly CursorType CursorType;

        internal Cursor (CursorType type)
        {
            CursorType = type;
        }

        /// <summary>Creates a cursor from a Win32 icon/cursor handle.</summary>
        /// <remarks>
        /// Accepted for source compatibility -- code that builds a drag cursor from a bitmap goes through
        /// <c>GetHicon</c> and this constructor -- but there are no HWNDs or HICONs here, so the handle is
        /// not read and the cursor behaves as the default. Documented as a stub rather than throwing: a
        /// drag that shows the standard pointer is a cosmetic loss, a crash is not.
        /// </remarks>
        public Cursor (IntPtr handle) : this (CursorType.Arrow) { }

        /// <inheritdoc/>
        public void Dispose ()
        {
            // Cursors are backend-neutral value descriptors in core; the native cursor (if any)
            // is owned and cached by the backend, so there is nothing to release here.
            GC.SuppressFinalize (this);
        }

        /// <inheritdoc/>
        public override bool Equals (object? obj)
            => obj is Cursor other && other.CursorType == CursorType;

        /// <inheritdoc/>
        public override int GetHashCode () => (int)CursorType;

        /// <summary>Determines whether two cursors represent the same cursor type.</summary>
        public static bool operator == (Cursor? left, Cursor? right)
        {
            if (ReferenceEquals (left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.CursorType == right.CursorType;
        }

        /// <summary>Determines whether two cursors represent different cursor types.</summary>
        public static bool operator != (Cursor? left, Cursor? right) => !(left == right);

        /// <inheritdoc/>
        public override string ToString () => $"[Cursor: {CursorType}]";

        /// <summary>
        /// The default cursor provided by the operating system.
        /// </summary>
        public static Cursor Default => Cursors.Arrow;

        /// <summary>Gets or sets the current mouse cursor. Stub in Majorsilence.Forms.</summary>
        public static Cursor? Current { get; set; }

        /// <summary>Hides the cursor. Stub in Majorsilence.Forms.</summary>
        public static void Hide () { }

        /// <summary>Shows the cursor. Stub in Majorsilence.Forms.</summary>
        public static void Show () { }

        /// <summary>Gets or sets the cursor's position in screen coordinates. Stub in Majorsilence.Forms.</summary>
        public static System.Drawing.Point Position { get; set; }

        /// <summary>Gets or sets whether the cursor is clipped to a rectangle. Stub in Majorsilence.Forms.</summary>
        public static System.Drawing.Rectangle Clip { get; set; }
    }
}
