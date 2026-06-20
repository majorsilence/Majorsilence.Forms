using System.Drawing;

namespace Continuum.Forms.Backends
{
    /// <summary>
    /// Per-window abstraction provided by an <see cref="IPlatformBackend"/>. Owns the native window
    /// (geometry, lifecycle, decorations, present surface). This is the "pull" side — the operations
    /// <see cref="WindowBase"/> invokes on its window.
    ///
    /// The "push" side (native input → Continuum.Forms, and paint requests) is delivered by the backend
    /// calling the owning window's neutral methods directly: <c>WindowBase.HandlePointerPressed/…</c>,
    /// <c>HandleKeyDown/…</c>, <c>HandleTextInput</c>, <c>RenderFrame</c> and the <c>OnBackend…</c>
    /// lifecycle callbacks — none of which expose any platform types. So no events live on this seam.
    /// </summary>
    public interface IWindowBackend
    {
        // ── Geometry ─────────────────────────────────────────────────────────────
        /// <summary>Gets or sets the window's top-left position, in screen pixels.</summary>
        Point Location { get; set; }
        /// <summary>Gets or sets the window's size, in logical pixels.</summary>
        Size Size { get; set; }
        /// <summary>Gets the client size, in logical pixels.</summary>
        Size ClientSize { get; }
        /// <summary>Gets the device scale factor (e.g. 2.0 on Retina).</summary>
        double Scaling { get; }

        // ── Lifecycle ────────────────────────────────────────────────────────────
        /// <summary>Shows the window.</summary>
        void Show ();
        /// <summary>Shows the window modally over the given parent backend, blocking until closed.</summary>
        void ShowDialog (IWindowBackend? owner);
        /// <summary>Hides the window without destroying it.</summary>
        void Hide ();
        /// <summary>Closes (destroys) the window.</summary>
        void Close ();
        /// <summary>Brings the window to the front and activates it.</summary>
        void Activate ();

        // ── Appearance / behaviour ───────────────────────────────────────────────
        /// <summary>Sets the window title.</summary>
        string Title { set; }
        /// <summary>Gets or sets whether the window is always on top.</summary>
        bool Topmost { get; set; }
        /// <summary>Switches between native (system) decorations and borderless self-drawn chrome.</summary>
        void SetSystemDecorations (bool useSystemDecorations);
        /// <summary>Sets the mouse cursor (a backend-neutral <see cref="CursorType"/>), or the default when null.</summary>
        void SetCursor (CursorType cursor);
        /// <summary>Sets the window icon from PNG bytes, or clears it when null.</summary>
        void SetIcon (byte[]? iconPng);
        /// <summary>Sets the minimum window size in logical pixels (empty = no minimum).</summary>
        Size MinimumSize { set; }
        /// <summary>Sets the maximum window size in logical pixels (empty = no maximum).</summary>
        Size MaximumSize { set; }
        /// <summary>Gets or sets whether the window can be resized by the user.</summary>
        bool CanResize { get; set; }
        /// <summary>Gets or sets whether the window appears in the taskbar.</summary>
        bool ShowInTaskbar { get; set; }
        /// <summary>Gets or sets the window opacity (0..1).</summary>
        double Opacity { get; set; }
        /// <summary>Gets or sets the window state (normal/minimized/maximized).</summary>
        FormWindowState WindowState { get; set; }
        /// <summary>Gets or sets whether the window accepts input (used to disable a modal dialog's parent).</summary>
        bool Enabled { get; set; }

        // ── Coordinate conversion ────────────────────────────────────────────────
        /// <summary>Converts a screen point to client coordinates.</summary>
        Point PointToClient (Point screen);
        /// <summary>Converts a client point to screen coordinates.</summary>
        Point PointToScreen (Point client);

        // ── Drag operations (custom chrome) ──────────────────────────────────────
        /// <summary>Begins an interactive window move drag.</summary>
        void BeginMoveDrag ();
        /// <summary>Begins an interactive window resize drag from the given edge.</summary>
        void BeginResizeDrag (WindowEdge edge);

        // ── Rendering ────────────────────────────────────────────────────────────
        /// <summary>Marks the window as needing a repaint.</summary>
        void Invalidate ();

        // ── File/folder pickers (owned by this window) ───────────────────────────
        /// <summary>Shows an open-file picker; returns the chosen full paths (empty if cancelled).</summary>
        System.Threading.Tasks.Task<string[]> ShowOpenFileDialog (OpenFileRequest request);
        /// <summary>Shows a save-file picker; returns the chosen full path, or null if cancelled.</summary>
        System.Threading.Tasks.Task<string?> ShowSaveFileDialog (SaveFileRequest request);
        /// <summary>Shows a folder picker; returns the chosen full path, or null if cancelled.</summary>
        System.Threading.Tasks.Task<string?> ShowOpenFolderDialog (FolderDialogRequest request);
    }

    /// <summary>Identifies a window edge/corner for an interactive resize drag. Backend-neutral.</summary>
    public enum WindowEdge
    {
        /// <summary>The north (top) edge.</summary>
        North,
        /// <summary>The north-east corner.</summary>
        NorthEast,
        /// <summary>The east (right) edge.</summary>
        East,
        /// <summary>The south-east corner.</summary>
        SouthEast,
        /// <summary>The south (bottom) edge.</summary>
        South,
        /// <summary>The south-west corner.</summary>
        SouthWest,
        /// <summary>The west (left) edge.</summary>
        West,
        /// <summary>The north-west corner.</summary>
        NorthWest
    }
}
