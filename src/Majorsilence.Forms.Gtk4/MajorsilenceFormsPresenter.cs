using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using MF = Majorsilence.Forms;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Embeds a Majorsilence.Forms scene inside an existing GTK 4 application. Assign <see cref="Content"/>
    /// a Majorsilence.Forms control tree and place <see cref="Widget"/> into any GTK container; the scene
    /// renders through SkiaSharp into that widget and GTK input is forwarded into the Majorsilence.Forms
    /// pipeline. No top-level OS window is created.
    ///
    /// Installs a <see cref="Gtk4PlatformBackend"/> as the active Majorsilence.Forms backend if none is
    /// configured yet. Popups opened from the embedded content (combo dropdowns, menus) are real
    /// borderless <c>Gtk.Window</c>s created by that backend.
    ///
    /// The GTK 4 counterpart of the Avalonia / Uno / WPF / WinForms <c>MajorsilenceFormsPresenter</c>
    /// classes. It exposes <see cref="Widget"/> rather than deriving from a GTK widget: gir.core's
    /// GObject subclassing needs an extra integration package and a type-registration call, and nothing
    /// here requires it.
    /// </summary>
    public sealed class MajorsilenceFormsPresenter : IWindowBackend, INativeControlHostBackend, IDisposable
    {
        private readonly Gtk4SkiaSurface _surface;
        private readonly global::Gtk.Overlay _overlay;
        private readonly MF.HostedSurface _host;
        private readonly Dictionary<MF.NativeControlHost, Gtk4NativeOverlay.Entry> _overlays = new ();
        private bool _disposed;
        private bool _enabled = true;

        /// <summary>
        /// Initializes a new presenter. Installs a <see cref="Gtk4PlatformBackend"/> if no backend is
        /// configured, then initializes GTK.
        /// </summary>
        public MajorsilenceFormsPresenter ()
        {
            if (Platform.ConfiguredBackend is null)
                Platform.Backend = new Gtk4PlatformBackend ();

            Platform.Backend.Initialize ();

            _surface = new Gtk4SkiaSurface (() => _host);
            _overlay = global::Gtk.Overlay.New ();
            _overlay.SetChild (_surface.Widget);
            _host = new MF.HostedSurface (this);

            _overlay.OnUnrealize += (_, _) => Dispose ();
        }

        /// <summary>The GTK widget to place in your visual tree (a <c>Gtk.Overlay</c> wrapping the Skia surface).</summary>
        public global::Gtk.Widget Widget => _overlay;

        /// <summary>Gets or sets the root Majorsilence.Forms control hosted by this presenter.</summary>
        public MF.Control? Content {
            get => _host.Content;
            set => _host.Content = value;
        }

        /// <summary>Gets the underlying hosted surface (advanced scenarios: multiple roots, events).</summary>
        public MF.HostedSurface Surface => _host;

        /// <summary>Detaches the embedded scene from the application (idempotent; also runs on unrealize).</summary>
        public void Dispose ()
        {
            if (_disposed)
                return;
            _disposed = true;
            _host.Dispose ();
        }

        // ── IWindowBackend ───────────────────────────────────────────────────────
        // The presenter is the embedded scene's "window". GTK 4 gives an app no absolute screen
        // position for a widget, so the coordinate conversions treat the widget's client origin as
        // the screen origin — the same limitation the standalone Gtk4 window host has.

        double IWindowBackend.Scaling => _surface.Scaling;

        Point IWindowBackend.Location { get => Point.Empty; set { /* owned by the host layout */ } }

        Size IWindowBackend.Size {
            get => _surface.Widget.GetWidth () > 0
                ? new Size (_surface.Widget.GetWidth (), _surface.Widget.GetHeight ())
                : _surface.ClientLogical;
            set { /* owned by the host layout */ }
        }

        Size IWindowBackend.ClientSize => ((IWindowBackend) this).Size;

        void IWindowBackend.Show () { }
        void IWindowBackend.ShowDialog (IWindowBackend? owner) { }
        void IWindowBackend.Hide () => _surface.Widget.SetVisible (false);
        void IWindowBackend.Close () { }
        void IWindowBackend.Activate () => _surface.Focus ();
        bool IWindowBackend.ShowActivated { get; set; } = true;

        string IWindowBackend.Title { set { } }
        bool IWindowBackend.Topmost { get; set; }
        void IWindowBackend.SetSystemDecorations (bool useSystemDecorations) { }
        void IWindowBackend.SetCursor (CursorType cursor) => _surface.Widget.SetCursorFromName (Gtk4KeyInterop.ToCursorName (cursor));
        void IWindowBackend.SetIcon (byte[]? iconPng) { }
        Size IWindowBackend.MinimumSize { set { } }
        Size IWindowBackend.MaximumSize { set { } }
        bool IWindowBackend.CanResize { get; set; }
        bool IWindowBackend.ShowInTaskbar { get; set; }
        double IWindowBackend.Opacity { get; set; } = 1.0;
        MF.FormWindowState IWindowBackend.WindowState { get; set; } = MF.FormWindowState.Normal;

        bool IWindowBackend.Enabled {
            get => _enabled;
            set { _enabled = value; _surface.Widget.SetSensitive (value); }
        }

        Point IWindowBackend.PointToClient (Point screen) => screen;
        Point IWindowBackend.PointToScreen (Point client) => client;

        void IWindowBackend.BeginMoveDrag () { }
        void IWindowBackend.BeginResizeDrag (WindowEdge edge) { }

        void IWindowBackend.Invalidate () => _surface.RequestRender ();

        // ── INativeControlHostBackend ────────────────────────────────────────────

        void INativeControlHostBackend.AttachNativeControl (MF.NativeControlHost host, object nativeControl)
            => Gtk4NativeOverlay.Attach (_overlay, _overlays, host, nativeControl);

        void INativeControlHostBackend.UpdateNativeControl (MF.NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible)
            => Gtk4NativeOverlay.Update (_overlay, _overlays, host, logicalBounds, clipBounds, visible);

        void INativeControlHostBackend.DetachNativeControl (MF.NativeControlHost host)
            => Gtk4NativeOverlay.Detach (_overlay, _overlays, host);

        Task<string[]> IWindowBackend.ShowOpenFileDialog (OpenFileRequest request) => Task.FromResult (Array.Empty<string> ());
        Task<string?> IWindowBackend.ShowSaveFileDialog (SaveFileRequest request) => Task.FromResult<string?> (null);
        Task<string?> IWindowBackend.ShowOpenFolderDialog (FolderDialogRequest request) => Task.FromResult<string?> (null);
    }
}
