using System.Drawing;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// A System.Windows.Forms control that embeds a Majorsilence.Forms scene inside an existing
    /// WinForms application. Drop it onto any WinForms form or container (or use
    /// <c>myMfControl.ToWinFormsControl()</c>) and assign <see cref="Content"/> a Majorsilence.Forms
    /// control tree; it renders through SkiaSharp into this control and forwards WinForms
    /// mouse/keyboard input into the Majorsilence.Forms pipeline. No top-level OS window is created.
    ///
    /// Popups opened from the embedded content (combo dropdowns, menus, tooltips) are real borderless
    /// OS windows created by <see cref="WinFormsPlatformBackend"/>, which the presenter installs as
    /// the active backend automatically if none is configured.
    ///
    /// This is the WinForms counterpart of the Avalonia and Uno <c>MajorsilenceFormsPresenter</c>
    /// classes — the migration path that lets a WinForms app (or a WinForms control library's
    /// consumers) adopt Majorsilence.Forms one control at a time, then switch to the Avalonia or Uno
    /// host once everything is ported.
    /// </summary>
    public class MajorsilenceFormsPresenter : WF.Control, IWindowBackend, INativeControlHostBackend
    {
        private readonly SkiaHostControl _skia;
        private readonly MF.HostedSurface _host;
        private readonly System.Collections.Generic.Dictionary<MF.NativeControlHost, WF.Control> _overlays = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="MajorsilenceFormsPresenter"/> class. Installs
        /// a <see cref="WinFormsPlatformBackend"/> as the active Majorsilence.Forms backend if none is
        /// configured yet.
        /// </summary>
        public MajorsilenceFormsPresenter ()
        {
            // Ensure the WinForms backend is the active platform backend so popups/timers opened by the
            // embedded content are WinForms windows on the host's own message pump.
            // NOTE: read the backend defensively — the getter auto-resolves to the Avalonia backend by
            // reflection, which throws in a WinForms-only app where that assembly isn't referenced.
            WinFormsPlatformBackend? backend = null;
            try { backend = Platform.Backend as WinFormsPlatformBackend; } catch { /* no backend configured yet */ }

            if (backend is null && !TryGetForeignBackend ()) {
                backend = new WinFormsPlatformBackend ();
                Platform.Backend = backend;
            }
            backend?.Initialize ();

            _skia = new SkiaHostControl (() => _host, () => Scaling) {
                Dock = WF.DockStyle.Fill,
            };
            Controls.Add (_skia);

            _host = new MF.HostedSurface (this);

            // The embedded scene is transparent where it paints nothing; clear to this control's
            // (ambient, parent-inherited) background so it blends into the host form.
            _skia.ClearColor = ToSKColor (BackColor);
            BackColorChanged += (_, _) => { _skia.ClearColor = ToSKColor (BackColor); _skia.Invalidate (); };
        }

        // True when a non-WinForms backend (e.g. Avalonia via WindowsFormsInterop) is already active;
        // the presenter then leaves it alone rather than replacing it mid-app.
        private static bool TryGetForeignBackend ()
        {
            try { return Platform.Backend is not null; }
            catch { return false; }
        }

        private static SkiaSharp.SKColor ToSKColor (Color color) => new (color.R, color.G, color.B, color.A);

        /// <summary>
        /// Gets or sets the root Majorsilence.Forms control hosted by this presenter. Setting it docks
        /// the control to fill the presenter.
        /// </summary>
        [System.ComponentModel.Browsable (false)]
        [System.ComponentModel.DesignerSerializationVisibility (System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public MF.Control? Content {
            get => _host.Content;
            set => _host.Content = value;
        }

        /// <summary>Gets the underlying hosted surface (advanced scenarios: multiple roots, events).</summary>
        [System.ComponentModel.Browsable (false)]
        [System.ComponentModel.DesignerSerializationVisibility (System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public MF.HostedSurface Surface => _host;

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing)
                _host.Dispose ();
            base.Dispose (disposing);
        }

        /// <inheritdoc/>
        protected override void OnResize (EventArgs e)
        {
            base.OnResize (e);
            _skia.Invalidate ();   // RenderFrame re-lays-out the scene from the new surface size
        }

        // ── IWindowBackend ───────────────────────────────────────────────────────
        // The presenter is the embedded scene's "window": screen coordinates are physical pixels (so
        // popups — real OS windows — land where the scene asked), client coordinates are logical.

        Point IWindowBackend.Location {
            get {
                try { return IsHandleCreated ? PointToScreen (Point.Empty) : Point.Empty; }
                catch { return Point.Empty; }
            }
            set { /* position is owned by the host layout */ }
        }

        Size IWindowBackend.Size {
            get {
                var s = Scaling;
                return new Size ((int) Math.Round (ClientSize.Width / s), (int) Math.Round (ClientSize.Height / s));
            }
            set { /* size is owned by the host layout */ }
        }

        Size IWindowBackend.ClientSize => ((IWindowBackend) this).Size;

        /// <summary>Gets the device scale factor of the monitor this presenter is on.</summary>
        public double Scaling => DeviceDpi / 96.0;

        void IWindowBackend.Show () { /* shown by the host control tree */ }
        void IWindowBackend.ShowDialog (IWindowBackend? owner) { /* embedded surfaces are not shown modally */ }
        void IWindowBackend.Hide () => Visible = false;
        void IWindowBackend.Close () { /* lifetime owned by the host */ }
        void IWindowBackend.Activate () => _skia.Focus ();
        bool IWindowBackend.ShowActivated { get; set; } = true;

        string IWindowBackend.Title { set { } }
        bool IWindowBackend.Topmost { get; set; }
        void IWindowBackend.SetSystemDecorations (bool useSystemDecorations) { }
        void IWindowBackend.SetCursor (CursorType cursor) => _skia.Cursor = WinFormsKeyInterop.ToCursor (cursor);
        void IWindowBackend.SetIcon (byte[]? iconPng) { }
        Size IWindowBackend.MinimumSize { set { } }
        Size IWindowBackend.MaximumSize { set { } }
        bool IWindowBackend.CanResize { get; set; }
        bool IWindowBackend.ShowInTaskbar { get; set; }

        double IWindowBackend.Opacity { get; set; } = 1.0;

        MF.FormWindowState IWindowBackend.WindowState { get; set; } = MF.FormWindowState.Normal;

        bool IWindowBackend.Enabled {
            get => Enabled;
            set => Enabled = value;
        }

        IntPtr IWindowBackend.TryGetPlatformHandle () => IsHandleCreated ? Handle : IntPtr.Zero;

        Point IWindowBackend.PointToClient (Point screen)
        {
            try {
                if (!IsHandleCreated)
                    return screen;
                var p = PointToClient (screen);
                var s = Scaling;
                return new Point ((int) Math.Round (p.X / s), (int) Math.Round (p.Y / s));
            } catch {
                return screen;
            }
        }

        Point IWindowBackend.PointToScreen (Point client)
        {
            try {
                if (!IsHandleCreated)
                    return client;
                var s = Scaling;
                return PointToScreen (new Point ((int) Math.Round (client.X * s), (int) Math.Round (client.Y * s)));
            } catch {
                return client;
            }
        }

        void IWindowBackend.BeginMoveDrag () { }
        void IWindowBackend.BeginResizeDrag (WindowEdge edge) { }

        void IWindowBackend.Invalidate ()
        {
            if (_skia.IsHandleCreated && _skia.InvokeRequired) {
                try { _skia.BeginInvoke (new Action (_skia.Invalidate)); } catch { }
                return;
            }
            _skia.Invalidate ();
        }

        // ── INativeControlHostBackend (real WinForms controls inside the embedded scene) ──────────

        void INativeControlHostBackend.AttachNativeControl (MF.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not WF.Control control)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, control))
                Controls.Remove (existing);

            _overlays[host] = control;
            if (!Controls.Contains (control)) {
                Controls.Add (control);
                control.BringToFront ();
            }
        }

        void INativeControlHostBackend.UpdateNativeControl (MF.NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible)
            => NativeOverlay.Update (_overlays, host, logicalBounds, clipBounds, visible, Scaling);

        void INativeControlHostBackend.DetachNativeControl (MF.NativeControlHost host)
        {
            if (_overlays.Remove (host, out var control))
                Controls.Remove (control);
        }

        // ── File/folder pickers (native WinForms dialogs, owned by the host form) ─────────────────

        Task<string[]> IWindowBackend.ShowOpenFileDialog (OpenFileRequest request)
            => Task.FromResult (WinFormsDialogs.ShowOpenFileDialog (FindForm (), request));

        Task<string?> IWindowBackend.ShowSaveFileDialog (SaveFileRequest request)
            => Task.FromResult (WinFormsDialogs.ShowSaveFileDialog (FindForm (), request));

        Task<string?> IWindowBackend.ShowOpenFolderDialog (FolderDialogRequest request)
            => Task.FromResult (WinFormsDialogs.ShowOpenFolderDialog (FindForm (), request));
    }
}
