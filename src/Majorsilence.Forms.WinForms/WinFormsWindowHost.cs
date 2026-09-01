using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// An <see cref="IWindowBackend"/> that presents a Majorsilence.Forms window through a real
    /// <see cref="WF.Form"/> filled by an SKControl: its <c>PaintSurface</c> calls
    /// <c>WindowBase.RenderFrame</c>, and WinForms mouse/keyboard events are translated into the
    /// neutral <c>WindowBase.Handle*</c> path (see <see cref="SkiaHostControl"/>).
    ///
    /// Popups (menus, combo dropdowns, tooltips) are borderless, non-activating tool windows —
    /// real OS windows, exactly like the Avalonia backend's popups.
    /// </summary>
    internal sealed class WinFormsWindowHost : IWindowBackend, INativeControlHostBackend, IDisposable
    {
        private readonly MF.WindowBase _owner;
        private readonly bool _isPopup;
        private readonly HostForm _form;
        private readonly SkiaHostControl _skia;
        private readonly System.Collections.Generic.Dictionary<MF.NativeControlHost, WF.Control> _overlays = new ();

        private Size _size = new (800, 600);
        private bool _systemDecorations;
        private bool _canResize = true;
        private bool _closedDelivered;

        // Exposes the wrapped native form to WinFormsHostInterop.ToWinFormsForm, for host apps that
        // want to show/use it directly as a native WinForms form rather than through Form.Show().
        internal WF.Form NativeForm => _form;

        // Font warmup runs once — subsequent calls are instant (caches are already populated).
        private static bool _fontWarmedUp;

        private static void EnsureFontsWarmedUp ()
        {
            if (_fontWarmedUp) return;
            _fontWarmedUp = true;
            MF.Theme.WarmupFonts ();
        }

        public WinFormsWindowHost (MF.WindowBase owner, bool isPopup)
        {
            EnsureFontsWarmedUp ();
            _owner = owner;
            _isPopup = isPopup;
            _systemDecorations = !isPopup;

            _form = new HostForm (isPopup) {
                StartPosition = WF.FormStartPosition.Manual,
                ShowInTaskbar = !isPopup,
                TopMost = isPopup,
            };

            _skia = new SkiaHostControl (() => _owner, () => Scaling) {
                Dock = WF.DockStyle.Fill,
            };
            _form.Controls.Add (_skia);

            ApplyDecorations ();
            ApplyClientSize ();
            WireLifecycle ();
        }

        private void WireLifecycle ()
        {
            _form.Activated += (_, _) => _owner.OnBackendActivated ();
            _form.Deactivate += (_, _) => _owner.OnBackendDeactivated ();
            _form.Move += (_, _) => _owner.OnBackendMoved ();
            _form.Resize += (_, _) => _skia.Invalidate ();
            _form.DpiChanged += (_, _) => { ApplyClientSize (); _skia.Invalidate (); };

            _form.FormClosing += (_, e) => {
                if (_owner.OnBackendClosing ())   // true == cancel
                    e.Cancel = true;
            };
            _form.FormClosed += (_, _) => {
                if (_closedDelivered)
                    return;
                _closedDelivered = true;
                _owner.OnBackendClosed ();
            };
        }

        // ── Geometry ─────────────────────────────────────────────────────────────
        // WinForms under PerMonitorV2 works in physical device pixels; the seam's Size/ClientSize are
        // logical, its Location/PointTo* screen coordinates are physical. Scaling converts between.

        public Point Location {
            get => _form.Location;
            set => _form.Location = value;
        }

        public Size Size {
            get {
                if (!_form.IsHandleCreated)
                    return _size;
                var s = Scaling;
                var client = _form.ClientSize;
                return new Size ((int) Math.Round (client.Width / s), (int) Math.Round (client.Height / s));
            }
            set {
                _size = value;
                ApplyClientSize ();
            }
        }

        public Size ClientSize => Size;

        public double Scaling => _form.DeviceDpi / 96.0;

        private void ApplyClientSize ()
        {
            var s = Scaling;
            _form.ClientSize = new Size ((int) Math.Round (_size.Width * s), (int) Math.Round (_size.Height * s));
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public void Show ()
        {
            _form.ShowWithoutActivationFlag = _isPopup || !ShowActivated;
            _form.Show ();
            if (_isPopup)
                _owner.OnBackendActivated ();
        }

        public void ShowDialog (IWindowBackend? owner)
        {
            // Majorsilence.Forms' own modal machinery (parent-disable + RunModalLoop) lives above the
            // seam, so the backend only needs to show the window with the right owner for z-order.
            if (owner is WinFormsWindowHost host && host._form.IsHandleCreated)
                _form.Show (host._form);
            else
                _form.Show ();
        }

        public void Hide () => _form.Hide ();

        public void Close ()
        {
            if (_form.IsHandleCreated || _form.Visible) {
                _form.Close ();
                return;
            }

            // Never shown: WF.Form.Close on an unshown form doesn't raise FormClosing/FormClosed,
            // so deliver the neutral lifecycle by hand.
            if (_owner.OnBackendClosing ())
                return;
            if (!_closedDelivered) {
                _closedDelivered = true;
                _owner.OnBackendClosed ();
            }
            _form.Dispose ();
        }

        public void Activate ()
        {
            if (!_form.Visible)
                _form.Show ();
            _form.Activate ();
        }

        public bool ShowActivated { get; set; } = true;

        // ── Appearance / behaviour ───────────────────────────────────────────────

        public string Title { set => _form.Text = value ?? string.Empty; }

        public bool Topmost {
            get => _form.TopMost;
            set => _form.TopMost = value;
        }

        public void SetSystemDecorations (bool useSystemDecorations)
        {
            _systemDecorations = useSystemDecorations;
            ApplyDecorations ();
        }

        private void ApplyDecorations ()
        {
            if (_isPopup) {
                _form.FormBorderStyle = WF.FormBorderStyle.None;
                return;
            }

            _form.FormBorderStyle = _systemDecorations
                ? (_canResize ? WF.FormBorderStyle.Sizable : WF.FormBorderStyle.FixedSingle)
                : WF.FormBorderStyle.None;
            _form.MaximizeBox = _canResize;
        }

        public void SetCursor (CursorType cursor) => _skia.Cursor = WinFormsKeyInterop.ToCursor (cursor);

        public void SetIcon (byte[]? iconPng)
        {
            if (_isPopup)
                return;

            if (iconPng is null || iconPng.Length == 0) {
                _form.Icon = null;
                return;
            }

            try {
                using var stream = new System.IO.MemoryStream (iconPng);
                using var bitmap = new System.Drawing.Bitmap (stream);
                var hIcon = bitmap.GetHicon ();
                try {
                    using var icon = System.Drawing.Icon.FromHandle (hIcon);
                    _form.Icon = (System.Drawing.Icon) icon.Clone ();   // own our copy; free the GDI handle below
                } finally {
                    _ = DestroyIcon (hIcon);
                }
            } catch {
                // Icon is best-effort.
            }
        }

        [DllImport ("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon (IntPtr hIcon);

        public Size MinimumSize {
            set {
                var s = Scaling;
                _form.MinimumSize = value.IsEmpty ? Size.Empty : new Size ((int) (value.Width * s), (int) (value.Height * s));
            }
        }

        public Size MaximumSize {
            set {
                var s = Scaling;
                _form.MaximumSize = value.IsEmpty ? Size.Empty : new Size ((int) (value.Width * s), (int) (value.Height * s));
            }
        }

        public bool CanResize {
            get => _canResize;
            set {
                _canResize = value;
                ApplyDecorations ();
            }
        }

        public bool ShowInTaskbar {
            get => _form.ShowInTaskbar;
            set => _form.ShowInTaskbar = value;
        }

        public double Opacity {
            get => _form.Opacity;
            set => _form.Opacity = value;
        }

        // MF.FormWindowState and WF.FormWindowState are the same enum values (Normal/Minimized/Maximized).
        public MF.FormWindowState WindowState {
            get => (MF.FormWindowState) (int) _form.WindowState;
            set => _form.WindowState = (WF.FormWindowState) (int) value;
        }

        public bool Enabled {
            get => _form.Enabled;
            set => _form.Enabled = value;
        }

        public IntPtr TryGetPlatformHandle () => _form.IsHandleCreated ? _form.Handle : IntPtr.Zero;

        // Optional IWindowBackend members with no meaning for the plain-WinForms host (the OS draws the
        // caption, there is a hardware keyboard, no window shaping). Default interface members on the
        // net8.0/net10.0 core; plain interface members on the netstandard2.0 core the net48 build
        // consumes, so they are implemented here for all TFMs.
        public void SetCaptionRegions (System.Collections.Generic.IReadOnlyList<Rectangle> captionRects) { }
        public void SetShaped (bool shaped) { }
        public void SetTextInputActive (bool active, TextInputKind kind) { }
        public void SetExtendClientIntoTitleBar (bool extend, int titleBarHeight) { }
        public bool IsSingleView => false;

        // ── Coordinate conversion ────────────────────────────────────────────────
        // Screen coordinates are physical pixels; client coordinates are logical.

        public Point PointToClient (Point screen)
        {
            if (!_skia.IsHandleCreated)
                return screen;
            var p = _skia.PointToClient (screen);
            var s = Scaling;
            return new Point ((int) Math.Round (p.X / s), (int) Math.Round (p.Y / s));
        }

        public Point PointToScreen (Point client)
        {
            if (!_skia.IsHandleCreated)
                return client;
            var s = Scaling;
            return _skia.PointToScreen (new Point ((int) Math.Round (client.X * s), (int) Math.Round (client.Y * s)));
        }

        // ── Drag (custom chrome) ─────────────────────────────────────────────────
        // The classic Win32 borderless-window trick: release the mouse capture the press gave us and
        // hand the drag to the window manager as a non-client hit.

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2;

        public void BeginMoveDrag () => BeginNonClientDrag (HTCAPTION);

        public void BeginResizeDrag (WindowEdge edge) => BeginNonClientDrag (edge switch {
            WindowEdge.West => 10,        // HTLEFT
            WindowEdge.East => 11,        // HTRIGHT
            WindowEdge.North => 12,       // HTTOP
            WindowEdge.NorthWest => 13,   // HTTOPLEFT
            WindowEdge.NorthEast => 14,   // HTTOPRIGHT
            WindowEdge.South => 15,       // HTBOTTOM
            WindowEdge.SouthWest => 16,   // HTBOTTOMLEFT
            WindowEdge.SouthEast => 17,   // HTBOTTOMRIGHT
            _ => HTCAPTION,
        });

        private void BeginNonClientDrag (int hitTest)
        {
            if (!_form.IsHandleCreated)
                return;
            _ = ReleaseCapture ();
            _ = SendMessage (_form.Handle, WM_NCLBUTTONDOWN, hitTest, 0);
        }

        [DllImport ("user32.dll")]
        private static extern bool ReleaseCapture ();

        [DllImport ("user32.dll")]
        private static extern IntPtr SendMessage (IntPtr hWnd, int msg, int wParam, int lParam);

        // ── Rendering ────────────────────────────────────────────────────────────

        public void Invalidate ()
        {
            if (_skia.IsHandleCreated && _skia.InvokeRequired) {
                try { _skia.BeginInvoke (new Action (_skia.Invalidate)); } catch { }
                return;
            }
            _skia.Invalidate ();
        }

        // ── INativeControlHostBackend (real WinForms controls inside the Majorsilence scene) ──────

        void INativeControlHostBackend.AttachNativeControl (MF.NativeControlHost host, object nativeControl)
        {
            if (nativeControl is not WF.Control control)
                return;

            if (_overlays.TryGetValue (host, out var existing) && !ReferenceEquals (existing, control))
                _form.Controls.Remove (existing);

            _overlays[host] = control;
            if (!_form.Controls.Contains (control)) {
                _form.Controls.Add (control);
                control.BringToFront ();
            }
        }

        void INativeControlHostBackend.UpdateNativeControl (MF.NativeControlHost host, Rectangle logicalBounds, Rectangle clipBounds, bool visible)
            => NativeOverlay.Update (_overlays, host, logicalBounds, clipBounds, visible, Scaling);

        void INativeControlHostBackend.DetachNativeControl (MF.NativeControlHost host)
        {
            if (_overlays.Remove (host, out var control))
                _form.Controls.Remove (control);
        }

        /// <summary>Releases the native form (which disposes its child Skia control with it).</summary>
        public void Dispose () => _form.Dispose ();

        // ── File/folder pickers (native WinForms dialogs) ────────────────────────

        public Task<string[]> ShowOpenFileDialog (OpenFileRequest request)
            => Task.FromResult (WinFormsDialogs.ShowOpenFileDialog (_form, request));

        public Task<string?> ShowSaveFileDialog (SaveFileRequest request)
            => Task.FromResult (WinFormsDialogs.ShowSaveFileDialog (_form, request));

        public Task<string?> ShowOpenFolderDialog (FolderDialogRequest request)
            => Task.FromResult (WinFormsDialogs.ShowOpenFolderDialog (_form, request));

        /// <summary>
        /// The native window: a plain <see cref="WF.Form"/> plus the two behaviours a host needs to
        /// control from the seam — show-without-activation, and (for popups) never activating at all
        /// (WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW, like every toolkit's menu/dropdown windows).
        /// </summary>
        private sealed class HostForm : WF.Form
        {
            private readonly bool _isPopup;

            internal HostForm (bool isPopup) => _isPopup = isPopup;

            [System.ComponentModel.Browsable (false)]
            [System.ComponentModel.DesignerSerializationVisibility (System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            internal bool ShowWithoutActivationFlag { get; set; }

            protected override bool ShowWithoutActivation => ShowWithoutActivationFlag;

            protected override WF.CreateParams CreateParams {
                get {
                    var cp = base.CreateParams;
                    if (_isPopup) {
                        const int WS_EX_TOOLWINDOW = 0x0000_0080;
                        const int WS_EX_NOACTIVATE = 0x0800_0000;
                        cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                    }
                    return cp;
                }
            }
        }
    }
}
