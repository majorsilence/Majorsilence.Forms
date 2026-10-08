using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Majorsilence.Forms.Backends;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a mouse cursor.
    /// </summary>
    public partial class Cursor : IDisposable
    {
        // The standard shape, or for a cursor built from .cur/.ico data an id at CustomTypeBase and above.
        // Every path that shows a cursor hands WindowBase.ApplyBackendCursor this value alone, so a custom
        // cursor travels as its id and is resolved to its image there (see CustomImage).
        internal readonly CursorType CursorType;

        // Synthetic handles: HandleBase + CursorType, so each standard shape and each loaded cursor has its
        // own, and a cursor rebuilt from one (Cursor (IntPtr)) finds the same shape and image. Upstream
        // compares cursors by HCURSOR; these compare the same way.
        private const long HandleBase = 0x4D460000;
        private const int CustomTypeBase = 0x10000;

        private static readonly Dictionary<CursorType, WeakReference<Cursor>> custom_cursors = new ();
        private static int next_custom_id;

        private IntPtr handle;

        // The decoded frame of a cursor loaded from data. Shared, not copied, by a cursor rebuilt from its
        // handle; only the loading cursor owns it, as only upstream's loading cursor frees its HCURSOR.
        private SKBitmap? image;
        private readonly bool owns_image;
        private readonly Point hot_spot;

        internal Cursor (CursorType type)
        {
            CursorType = type;
            handle = HandleOf (type);
        }

        /// <summary>Creates a cursor from a cursor handle.</summary>
        /// <remarks>
        /// A handle from another <see cref="Cursor"/>'s <see cref="Handle"/> gives that cursor back: the
        /// same shape, the same loaded image, and equal to it, as upstream wraps the same HCURSOR. Any other
        /// handle is kept (so the cursor compares by it) but shows the arrow -- there are no HCURSORs here
        /// to draw from.
        ///
        /// Upstream throws <see cref="ArgumentException"/> for a zero handle. This answers the default
        /// cursor instead, because the handles this library hands out for images are zero
        /// (<c>Bitmap.GetHicon</c>, <c>Icon.Handle</c>), and the drag-cursor idiom
        /// <c>new Cursor (bitmap.GetHicon ())</c> would then crash where it shows the arrow today.
        /// </remarks>
        public Cursor (IntPtr handle)
        {
            if (handle == IntPtr.Zero) {
                CursorType = CursorType.Arrow;
                this.handle = HandleOf (CursorType.Arrow);
                return;
            }

            this.handle = handle;
            var offset = (long)handle - HandleBase;

            if (offset >= 0 && offset <= (long)CursorType.None) {
                CursorType = (CursorType)offset;
            } else if (offset >= CustomTypeBase && offset <= int.MaxValue && Owner ((CursorType)offset) is { } owner) {
                CursorType = owner.CursorType;
                image = owner.image;
                hot_spot = owner.hot_spot;
            } else {
                CursorType = CursorType.Arrow;
            }
        }

        /// <summary>Creates a cursor from a .cur or .ico file.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is null.</exception>
        /// <exception cref="FileNotFoundException">The file does not exist.</exception>
        /// <exception cref="ArgumentException">The file is not a cursor or an icon.</exception>
        /// <inheritdoc cref="Cursor(Stream)" path="/remarks"/>
        public Cursor (string fileName)
            : this (File.ReadAllBytes (fileName), nameof (fileName))
        {
        }

        /// <summary>Creates a cursor from a .cur or .ico resource embedded in <paramref name="type"/>'s assembly.</summary>
        /// <param name="type">The type whose namespace scopes <paramref name="resource"/>.</param>
        /// <param name="resource">The resource name.</param>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is null, or the resource does not
        /// exist (upstream passes the missing stream on).</exception>
        /// <exception cref="ArgumentException">The resource is not a cursor or an icon.</exception>
        public Cursor (Type type, string resource)
            : this (ResourceStream (type, resource)!)
        {
        }

        /// <summary>Creates a cursor from .cur or .ico data.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <exception cref="ArgumentException">The data is not a cursor or an icon.</exception>
        /// <remarks>
        /// As upstream, a seekable stream is read from its start. The frame closest to the system cursor
        /// size (<see cref="SystemInformation.CursorSize"/>) is used, with the hotspot its directory entry
        /// gives; an icon tracks the pointer at its centre. Each window backend shows the image through its
        /// own toolkit; the browser, mobile, Uno and terminal backends have no bitmap cursor and show the
        /// arrow. Animated cursors (.ani) are a different container and are rejected, as they are upstream.
        /// </remarks>
        public Cursor (Stream stream)
            : this (ReadAll (stream), nameof (stream))
        {
        }

        private Cursor (byte[] data, string paramName)
        {
            (image, hot_spot) = CursorFile.Decode (data, paramName);
            owns_image = true;

            lock (custom_cursors) {
                foreach (var dead in custom_cursors.Where (c => !c.Value.TryGetTarget (out _)).Select (c => c.Key).ToList ())
                    custom_cursors.Remove (dead);

                CursorType = (CursorType)(CustomTypeBase + next_custom_id++);
                custom_cursors[CursorType] = new WeakReference<Cursor> (this);
            }

            handle = HandleOf (CursorType);
        }

        private static Stream? ResourceStream (Type type, string resource)
        {
            Guard.ThrowIfNull (type);
            return type.Module.Assembly.GetManifestResourceStream (type, resource);
        }

        private static byte[] ReadAll (Stream stream)
        {
            Guard.ThrowIfNull (stream);

            // Upstream: there is no guarantee the stream is at its start.
            if (stream.CanSeek)
                stream.Position = 0;

            using var buffer = new MemoryStream ();
            stream.CopyTo (buffer);
            return buffer.ToArray ();
        }

        private static IntPtr HandleOf (CursorType type) => new IntPtr (HandleBase + (int)type);

        private static Cursor? Owner (CursorType type)
        {
            lock (custom_cursors)
                return custom_cursors.TryGetValue (type, out var owner) && owner.TryGetTarget (out var cursor) && cursor.image is not null ? cursor : null;
        }

        // Whether a CursorType is a loaded cursor's id rather than a standard shape.
        internal static bool IsCustom (CursorType type) => (int)type >= CustomTypeBase;

        // The image and hotspot a custom cursor id stands for, or null once the loading cursor is disposed
        // (or collected) -- upstream's HCURSOR is destroyed then too, and the window shows the arrow.
        internal static SKBitmap? CustomImage (CursorType type, out Point hotSpot)
        {
            var owner = IsCustom (type) ? Owner (type) : null;
            hotSpot = owner?.hot_spot ?? Point.Empty;
            return owner?.image;
        }

        // The loaded image, for tests and for drawing.
        internal SKBitmap? Image => image;

        /// <summary>Gets or sets arbitrary data associated with this cursor.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the point within the cursor image that tracks the pointer.</summary>
        /// <remarks>The loaded frame's hotspot for a cursor built from data; (0, 0) for the standard
        /// cursors, whose images belong to the platform.</remarks>
        public Point HotSpot => hot_spot;

        /// <summary>Gets the size of the cursor, the system cursor size.</summary>
        /// <remarks>As upstream, which answers <see cref="SystemInformation.CursorSize"/> whatever the image.</remarks>
        public Size Size => SystemInformation.CursorSize;

        /// <summary>Gets the handle for the cursor.</summary>
        /// <exception cref="ObjectDisposedException">The cursor was loaded from data and has been disposed.</exception>
        /// <remarks>
        /// Not a Win32 HCURSOR -- the backends set the pointer through their own API -- but a handle in the
        /// sense upstream's equality uses: each standard shape and each loaded cursor has its own, two
        /// cursors are equal exactly when their handles are, and <see cref="Cursor(IntPtr)"/> turns one back
        /// into the cursor it came from. Do not pass it to Win32.
        /// </remarks>
        public IntPtr Handle {
            get {
#if NETSTANDARD2_0
                if (handle == IntPtr.Zero)
                    throw new ObjectDisposedException (GetType ().FullName);
#else
                ObjectDisposedException.ThrowIf (handle == IntPtr.Zero, this);
#endif

                return handle;
            }
        }

        /// <summary>Gets a handle to the cursor.</summary>
        /// <remarks>Upstream copies the HCURSOR. There is nothing to copy here, so this answers
        /// <see cref="Handle"/>; a cursor built from it is the same cursor.</remarks>
        public IntPtr CopyHandle () => Handle;

        /// <summary>Draws the cursor image inside the given rectangle.</summary>
        /// <remarks>The backends own the pointer image and do not hand back a bitmap for it, so there
        /// is nothing to draw; a caller compositing a drag image should draw its own.</remarks>
        public void Draw (Graphics g, Rectangle targetRect) { }

        /// <inheritdoc cref="Draw"/>
        public void DrawStretched (Graphics g, Rectangle targetRect) { }

        /// <inheritdoc/>
        /// <remarks>
        /// Disposing a cursor loaded from data releases its image, as upstream destroys its HCURSOR: a window
        /// still set to it shows the arrow from then on. The standard cursors are shared and are not
        /// affected.
        /// </remarks>
        public void Dispose ()
        {
            if (owns_image && image is not null) {
                lock (custom_cursors)
                    custom_cursors.Remove (CursorType);

                // Not disposed: a backend may still be showing a native cursor made from it, and the
                // collector reclaims it with the last reference.
                image = null;
                handle = IntPtr.Zero;
            }

            GC.SuppressFinalize (this);
        }

        /// <inheritdoc/>
        public override bool Equals (object? obj)
            => obj is Cursor other && other.handle == handle;

        /// <inheritdoc/>
        public override int GetHashCode () => handle.GetHashCode ();

        /// <summary>Determines whether two cursors are the same cursor: the same standard shape, or the same
        /// loaded cursor.</summary>
        public static bool operator == (Cursor? left, Cursor? right)
        {
            if (ReferenceEquals (left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.handle == right.handle;
        }

        /// <summary>Determines whether two cursors are different cursors.</summary>
        public static bool operator != (Cursor? left, Cursor? right) => !(left == right);

        /// <inheritdoc/>
        /// <remarks>As upstream: the standard cursors name their shape; any other cursor gives the type name.</remarks>
        public override string ToString () => IsCustom (CursorType) || handle != HandleOf (CursorType)
            ? $"[Cursor: {base.ToString ()}]"
            : $"[Cursor: {CursorType}]";

        /// <summary>
        /// The default cursor provided by the operating system.
        /// </summary>
        public static Cursor Default => Cursors.Arrow;

        /// <summary>Gets or sets the cursor being shown.</summary>
        /// <remarks>
        /// As upstream (<c>Cursor.Current</c> calls <c>SetCursor</c>): setting it shows the cursor on every
        /// open window now, which is the WinForms busy-cursor idiom --
        /// <c>Cursor.Current = Cursors.WaitCursor; ...long work...; Cursor.Current = Cursors.Default;</c> --
        /// and the next mouse move puts back the cursor of the control under the pointer, as
        /// <c>WM_SETCURSOR</c> does. The getter answers the cursor shown last (SVC-12: it was stored only).
        /// </remarks>
        public static Cursor? Current {
            // Null after `Current = null`, which upstream turns into no cursor at all.
            get => current_set ? current : Cursors.Default;
            set {
                current = value;
                current_set = true;

                foreach (var form in Application.OpenForms.Cast<Form> ().ToList ())
                    form.ShowCursor (value ?? Cursors.Default);
            }
        }

        private static Cursor? current;
        private static bool current_set;

        // The window put a control's cursor back on a mouse move: that is what is shown now.
        internal static void Track (Cursor? shown)
        {
            current = shown ?? Cursors.Default;
            current_set = true;
        }

        // Upstream's Hide/Show call ShowCursor (false/true) (Input/Cursor.cs), which moves a display
        // counter: the pointer is hidden while it is below zero, so the calls must be balanced and
        // two Hides need two Shows. This is that counter, kept process-wide.
        private static int display_count;

        // Whether Hide has outweighed Show: the windows then hand the backend CursorType.None in place
        // of whatever cursor they would show (SVC-38).
        internal static bool IsHidden => display_count < 0;

        /// <summary>Hides the cursor.</summary>
        /// <remarks>
        /// As upstream, calls are counted: the cursor stays hidden until <see cref="Show"/> has been called
        /// as many times as <see cref="Hide"/>. The pointer is hidden over this application's windows
        /// only; a backend that has no hidden cursor (Uno) keeps showing the arrow.
        /// </remarks>
        public static void Hide ()
        {
            display_count--;

            if (display_count == -1)
                ReapplyToOpenForms ();
        }

        /// <summary>Shows the cursor, undoing one call to <see cref="Hide"/>.</summary>
        public static void Show ()
        {
            display_count++;

            if (display_count == 0)
                ReapplyToOpenForms ();
        }

        private static void ReapplyToOpenForms ()
        {
            foreach (var form in Application.OpenForms.Cast<Form> ().ToList ())
                form.ReapplyCursor ();
        }

        /// <summary>Gets or sets the cursor's position in screen coordinates.</summary>
        /// <remarks>
        /// Tracked from the pointer events the windows receive, converted to screen coordinates. It used
        /// to be a plain stored property that nothing ever assigned, so it always read (0, 0) -- and
        /// <see cref="Control.MousePosition"/> reads through to here. Any control that hit-tests the
        /// pointer without being handed a MouseEventArgs -- the WinForms
        /// <c>HitTest (PointToClient (Control.MousePosition))</c> idiom, which is how a tab strip works
        /// out which tab was clicked -- therefore tested the top-left corner of the screen and found
        /// nothing, so clicking a tab did nothing at all.
        ///
        /// Setting it still only stores: warping the pointer is a platform capability the backends do
        /// not expose, and the next real pointer event overwrites the stored value.
        /// </remarks>
        public static System.Drawing.Point Position { get; set; }

        // Called from the window pointer handlers, with coordinates already in screen space.
        internal static void TrackPosition (System.Drawing.Point screenPosition) => Position = screenPosition;

        /// <summary>Gets or sets whether the cursor is clipped to a rectangle. Stub in Majorsilence.Forms.</summary>
        public static System.Drawing.Rectangle Clip { get; set; }
    }
}
