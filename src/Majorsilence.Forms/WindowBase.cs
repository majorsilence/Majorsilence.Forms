using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents the base class for windows, like Form and PopupWindow.
    /// </summary>
    public abstract partial class WindowBase : Component
    {
        private const int DOUBLE_CLICK_TIME = 500;
        private const int DOUBLE_CLICK_MOVEMENT = 4;

        // The window's platform backend (the Avalonia host today; a Uno host in future). WindowBase
        // performs all of its window operations through this seam so it stays fully backend-neutral.
        internal Majorsilence.Forms.Backends.IWindowBackend Backend = null!;
        internal ControlAdapter adapter = null!;

        private DateTime last_click_time;
        private System.Drawing.Point last_click_point;
        private Cursor? current_cursor;
        internal bool shown;

        // True when this window is embedded inside another UI toolkit (see HostedSurface) rather than
        // owning a top-level OS window. Used to suppress top-level-only behaviour (chrome, etc.).
        internal bool IsHosted;

        /// <summary>
        /// Initializes the platform backend. Subclasses must call <see cref="InitWindow"/> before
        /// accessing any window or adapter members.
        /// </summary>
        protected WindowBase ()
        {
            Majorsilence.Forms.Backends.Platform.Backend.Initialize ();
        }

        /// <summary>
        /// Completes window initialisation. Must be called in subclass constructors before accessing
        /// Controls, adapter, or any window property.
        /// </summary>
        internal void InitWindow (Majorsilence.Forms.Backends.IWindowBackend backend)
        {
            Backend = backend;
            adapter = new ControlAdapter (this);
        }

        // ── Lifecycle callbacks (the platform backend invokes these; no platform types involved) ──
        /// <summary>Called by the backend after the window is closed.</summary>
        internal void OnBackendClosed () => Closed?.Invoke (this, EventArgs.Empty);

        /// <summary>Called by the backend when the window is about to close. Returns true to cancel.</summary>
        internal bool OnBackendClosing ()
        {
            if (this is Form f) {
                var args = new System.ComponentModel.CancelEventArgs ();
                f.OnClosing (args);
                return args.Cancel;
            }

            return false;
        }

        /// <summary>Called by the backend when the window is activated.</summary>
        internal void OnBackendActivated () => Activated?.Invoke (this, EventArgs.Empty);

        /// <summary>Called by the backend when the window is deactivated.</summary>
        internal void OnBackendDeactivated ()
        {
            // Showing a popup deactivates its parent window; that must NOT dismiss the popup we are
            // opening. The flag is set only for the duration of PopupWindow.Show.
            if (Application.SuppressPopupDismiss)
                return;

            Application.ClosePopups ();
            Deactivated?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>Gets the bounds of the Window.</summary>
        public System.Drawing.Rectangle Bounds => new System.Drawing.Rectangle (Location, Size);

        private MouseEventArgs BuildMouseClickArgs (MouseButtons buttons, System.Drawing.Point point, Keys keyData)
        {
            var click_count = 1;

            if (DateTime.Now.Subtract (last_click_time).TotalMilliseconds < DOUBLE_CLICK_TIME && PointInDoubleClickRange (point))
                click_count = 2;

            var e = new MouseEventArgs (buttons, click_count, point.X, point.Y, System.Drawing.Point.Empty, keyData: keyData);

            last_click_time = click_count > 1 ? DateTime.MinValue : DateTime.Now;
            last_click_point = click_count > 1 ? System.Drawing.Point.Empty : point;

            return e;
        }

        /// <summary>Closes and destroys the window.</summary>
        public virtual void Close ()
        {
            if (this is Form f) {
                var args = new System.ComponentModel.CancelEventArgs ();

                f.OnClosing (args);

                if (args.Cancel)
                    return;

                Application.OpenForms.Remove (f);
            }

            Backend.Close ();
        }

        /// <summary>
        /// Releases the window's resources. Disposing a window also detaches it from global window
        /// state (Application.OpenForms and the active-popup tracking) so a window that is disposed
        /// without an explicit <see cref="Close"/> — e.g. a <c>using</c>-scoped form — does not leak
        /// into that shared state. Mirrors WinForms, where disposing a Form removes it from
        /// Application.OpenForms.
        /// </summary>
        protected override void Dispose (bool disposing)
        {
            Disposing = true;
            IsDisposed = true;

            if (disposing) {
                if (this is Form f)
                    Application.OpenForms.Remove (f);

                if (Application.ActivePopupWindow == this)
                    Application.ActivePopupWindow = null;
            }

            base.Dispose (disposing);

            Disposing = false;
        }

        /// <summary>Gets whether the window has been disposed. Mirrors WinForms Control.IsDisposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Gets whether the window is currently executing its dispose logic. Mirrors WinForms Form.Disposing.</summary>
        public bool Disposing { get; private set; }

        /// <summary>
        /// Gets whether the caller must marshal to the UI thread to interact with this window.
        /// Mirrors WinForms Control.InvokeRequired (see the matching member on <see cref="Control"/>).
        /// </summary>
        public bool InvokeRequired => !Majorsilence.Forms.Backends.Platform.Backend.CheckAccess ();

        private bool enabled = true;

        /// <summary>
        /// Gets or sets whether the window accepts input. Mirrors WinForms Form.Enabled; delegates to
        /// the platform backend (the same seam modal dialogs use to disable their owner).
        /// </summary>
        public bool Enabled {
            get => enabled;
            set {
                if (enabled == value)
                    return;
                enabled = value;
                if (Backend is not null)
                    Backend.Enabled = value;
                EnabledChanged?.Invoke (this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when <see cref="Enabled"/> changes. Mirrors WinForms Control.EnabledChanged (modal dialogs toggle their owner through this property).</summary>
        public event EventHandler? EnabledChanged;

        /// <summary>Raised when the window is closed.</summary>
        public event EventHandler? Closed;

        /// <summary>Gets the collection of controls contained by the window.</summary>
        public Control.ControlCollection Controls => adapter.Controls;

        /// <summary>Gets or sets the window's default font. Mirrors WinForms Form.Font; forwarded to
        /// the root control adapter so child controls inherit it.</summary>
        public Majorsilence.Forms.Drawing.Font? Font {
            get => adapter?.Font;
            set { if (adapter is not null && value is not null) adapter.Font = value; }
        }

        /// <summary>Gets or sets the cursor shown over the window. Mirrors WinForms Form.Cursor.</summary>
        public Cursor? Cursor {
            get => current_cursor;
            set {
                current_cursor = value;
                Backend?.SetCursor (value?.CursorType ?? Backends.CursorType.Arrow);
            }
        }

        /// <summary>WinForms compatibility. Majorsilence.Forms always renders double-buffered; the
        /// value is stored but has no effect.</summary>
        public bool DoubleBuffered { get; set; } = true;

        /// <summary>WinForms compatibility: the window's outer margin. Stored for designer parity;
        /// top-level windows have no layout parent to consume it.</summary>
        public Padding Margin { get; set; } = new Padding (3);

        /// <summary>Raised when the window's client area is double-clicked. Mirrors WinForms
        /// Form.DoubleClick; forwards to the root control adapter.</summary>
        public event EventHandler? DoubleClick {
            add => adapter.DoubleClick += value;
            remove => adapter.DoubleClick -= value;
        }

        /// <summary>Gets the current style of this window instance.</summary>
        public virtual ControlStyle CurrentStyle => Style;

        /// <summary>
        /// Renders one frame of the Majorsilence.Forms scene into the supplied SkiaSharp canvas. This is the
        /// backend-neutral paint pipeline: a platform backend creates/locks a surface at the physical
        /// pixel size and calls this. <paramref name="physW"/>/<paramref name="physH"/> are physical
        /// pixels; <paramref name="scaling"/> is the device scale factor.
        /// </summary>
        internal void RenderFrame (SkiaSharp.SKCanvas canvas, int physW, int physH, double scaling)
        {
            var skInfo = new SkiaSharp.SKImageInfo (physW, physH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

            // Adapter and border widths are in LOGICAL pixels; canvas draws in PHYSICAL pixels.
            var logicalW = (int)Math.Round (physW / scaling);
            var logicalH = (int)Math.Round (physH / scaling);

            var border = CurrentStyle.Border;
            var borderLeft = border.Left.GetWidth ();
            var borderTop = border.Top.GetWidth ();
            var physBorderLeft = (int)(borderLeft * scaling);
            var physBorderTop = (int)(borderTop * scaling);
            var physBorderRight = (int)(border.Right.GetWidth () * scaling);
            var physBorderBottom = (int)(border.Bottom.GetWidth () * scaling);

            if (adapter.Left != borderLeft || adapter.Top != borderTop ||
                adapter.Width != logicalW || adapter.Height != logicalH) {
                adapter.SetBounds (borderLeft, borderTop, logicalW, logicalH);
                adapter.PerformLayout ();
                OnClientLayoutChanged ();
                OnSizeChanged (EventArgs.Empty);
            }

            var e = new PaintEventArgs (skInfo, canvas, scaling);

            OnPaintBackground (e);
            canvas.DrawBorder (new System.Drawing.Rectangle (0, 0, physW, physH), CurrentStyle);
            OnPaint (e);

            // Clip canvas to the inner client area (excludes borders).
            canvas.ClipRect (new SkiaSharp.SKRect (
                physBorderLeft, physBorderTop,
                physW - physBorderRight + 1, physH - physBorderBottom + 1));

            adapter.RaisePaintBackground (e);
            adapter.RaisePaint (e);

            canvas.Flush ();
        }

        /// <summary>Raised when the window is deactivated.</summary>
        public event EventHandler? Deactivated;

        /// <summary>Raised when the window becomes the active window.</summary>
        public event EventHandler? Activated;

        /// <summary>Gets the default size of the window.</summary>
        protected virtual System.Drawing.Size DefaultSize => new System.Drawing.Size (100, 100);

        /// <summary>Gets the default style for all windows of this type.</summary>
        public static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.BackgroundColor;
            });

        /// <summary>Gets the unscaled bounds of the form not including borders.</summary>
        public System.Drawing.Rectangle DisplayRectangle => new System.Drawing.Rectangle (
            CurrentStyle.Border.Left.GetWidth (),
            CurrentStyle.Border.Top.GetWidth (),
            Backend.ClientSize.Width - CurrentStyle.Border.Right.GetWidth () - CurrentStyle.Border.Left.GetWidth (),
            Backend.ClientSize.Height - CurrentStyle.Border.Top.GetWidth () - CurrentStyle.Border.Bottom.GetWidth ());

        internal virtual bool HandleMouseDown (int x, int y) => false;

        internal virtual bool HandleMouseMove (int x, int y)
        {
            Backend.SetCursor (current_cursor?.CursorType ?? Backends.CursorType.Arrow);
            return false;
        }

        /// <summary>Hides the window without destroying it.</summary>
        public void Hide ()
        {
            visible = false;
            Backend.Hide ();

            if (Application.ActivePopupWindow == this)
                Application.ActivePopupWindow = null;

            OnVisibleChanged (EventArgs.Empty);
        }

        /// <summary>Marks the entire window as needing to be redrawn.</summary>
        public virtual void Invalidate () => Backend.Invalidate ();

        /// <summary>Marks the specified portion of the window as needing to be redrawn.</summary>
        public void Invalidate (System.Drawing.Rectangle rectangle) => Invalidate ();

        /// <summary>Marks the window as needing to be redrawn. Mirrors WinForms Invalidate(bool);
        /// children repaint with the window here regardless.</summary>
        public void Invalidate (bool invalidateChildren) => Invalidate ();

        /// <summary>Executes the specified delegate asynchronously on the window's UI thread.</summary>
        public void BeginInvoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            Majorsilence.Forms.Backends.Platform.Backend.Post (action);
        }

        /// <summary>Executes the specified delegate asynchronously on the window's UI thread with the given arguments. Mirrors WinForms Control.BeginInvoke(Delegate, Object[]).</summary>
        public void BeginInvoke (Delegate method, params object?[]? args)
        {
            ArgumentNullException.ThrowIfNull (method);
            Majorsilence.Forms.Backends.Platform.Backend.Post (() => method.DynamicInvoke (args));
        }

        /// <summary>Executes the specified delegate synchronously on the window's UI thread.</summary>
        public void Invoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            Majorsilence.Forms.Backends.Platform.Backend.Invoke (action);
        }

        /// <summary>Executes the specified delegate synchronously on the window's UI thread with the given arguments and returns its result. Mirrors WinForms Control.Invoke(Delegate, Object[]).</summary>
        public object? Invoke (Delegate method, params object?[]? args)
        {
            ArgumentNullException.ThrowIfNull (method);
            object? result = null;
            Majorsilence.Forms.Backends.Platform.Backend.Invoke (() => result = method.DynamicInvoke (args));
            return result;
        }

        /// <summary>
        /// Gets an opaque nonzero token standing in for the native window handle. WinForms code
        /// reads Handle to force handle creation before Invoke; the compat window has no HWND.
        /// </summary>
        public IntPtr Handle => (IntPtr)(GetHashCode () | 1);

        /// <summary>Gets or sets how the window's background image is laid out. Stored for designer compat (the compat window does not draw a background image yet).</summary>
        public ImageLayout BackgroundImageLayout { get; set; } = ImageLayout.Tile;

        /// <summary>Gets or sets the unscaled location of the window. Mirrors WinForms Form.Location.</summary>
        public System.Drawing.Point Location {
            get => Backend.Location;
            set {
                if (Backend.Location == value)
                    return;
                Backend.Location = value;
                OnLocationChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the x-coordinate of the window's left edge. Mirrors WinForms Form.Left.</summary>
        public int Left {
            get => Location.X;
            set => Location = new System.Drawing.Point (value, Location.Y);
        }

        /// <summary>Gets or sets the y-coordinate of the window's top edge. Mirrors WinForms Form.Top.</summary>
        public int Top {
            get => Location.Y;
            set => Location = new System.Drawing.Point (Location.X, value);
        }

        /// <summary>Raised when the window's location changes. Mirrors WinForms Form.LocationChanged.
        /// Raised for programmatic moves; backend-driven moves raise it via OnBackendMoved.</summary>
        public event EventHandler? LocationChanged;

        /// <summary>Raises the LocationChanged event.</summary>
        protected virtual void OnLocationChanged (EventArgs e) => LocationChanged?.Invoke (this, e);

        /// <summary>Called by the backend when the OS window is moved.</summary>
        internal void OnBackendMoved () => OnLocationChanged (EventArgs.Empty);

        /// <summary>Raised when the window's client size changes. Mirrors WinForms Form.SizeChanged.
        /// Raised from the layout pipeline whenever the client area takes a new size.</summary>
        public event EventHandler? SizeChanged;

        /// <summary>Raises the SizeChanged event.</summary>
        protected virtual void OnSizeChanged (EventArgs e) => SizeChanged?.Invoke (this, e);

        /// <summary>Raised when the window is resized. Mirrors WinForms Form.Resize (alias of SizeChanged).</summary>
        public event EventHandler? Resize {
            add => SizeChanged += value;
            remove => SizeChanged -= value;
        }

        /// <summary>
        /// Gets the native OS window handle (HWND on Windows), or <see cref="System.IntPtr.Zero"/> if the
        /// backend can't provide one. Used by platform accessibility bridges to attach to the host window.
        /// </summary>
        public System.IntPtr PlatformHandle => Backend.TryGetPlatformHandle ();

        /// <summary>Raised when the MaximumSize property is changed.</summary>
        public event EventHandler? MaximumSizeChanged;

        /// <summary>Raised when the MinimumSize property is changed.</summary>
        public event EventHandler? MinimumSizeChanged;

        // ── Neutral input handlers (the platform backend translates native input and calls these) ──

        internal void HandlePointerPressed (MouseButtons button, int x, int y, Keys keys)
        {
            if (Resizeable && HandleMouseDown (x, y))
                return;

            var ev = new MouseEventArgs (button, 1, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseDown (ev);
        }

        internal void HandlePointerReleased (MouseButtons button, int x, int y, Keys keys)
        {
            var ev = BuildMouseClickArgs (button, new System.Drawing.Point (x, y), keys);

            if (ev.Clicks > 1)
                adapter.RaiseDoubleClick (ev);

            adapter.RaiseClick (ev);
            adapter.RaiseMouseUp (ev);
        }

        internal void HandlePointerMoved (MouseButtons buttons, int x, int y, Keys keys)
        {
            if (Resizeable && HandleMouseMove (x, y))
                return;

            var ev = new MouseEventArgs (buttons, 0, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseMove (ev);
        }

        internal void HandlePointerWheel (MouseButtons buttons, int x, int y, System.Drawing.Point delta, Keys keys)
        {
            var ev = new MouseEventArgs (buttons, 0, x, y, delta, keyData: keys);
            adapter.RaiseMouseWheel (ev);
        }

        internal void HandlePointerExited (MouseButtons buttons, int x, int y, Keys keys)
        {
            var ev = new MouseEventArgs (buttons, 0, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseLeave (ev);
        }

        /// <summary>Routes a key-down. Returns true if handled (the backend should suppress further native processing).</summary>
        internal bool HandleKeyDown (Keys keys)
        {
            var kd_e = new KeyEventArgs (keys);

            // Form-level shortcuts: AcceptButton / CancelButton / modal Escape
            if (this is Form form) {
                var baseKey = keys & Keys.KeyCode;

                if (baseKey == Keys.Return && form.AcceptButton != null) {
                    form.AcceptButton.PerformClick ();
                    return true;
                }

                if (baseKey == Keys.Escape) {
                    if (form.CancelButton != null) {
                        form.CancelButton.PerformClick ();
                        return true;
                    }

                    if (form.dialog_task is not null) {
                        form.DialogResult = DialogResult.Cancel;
                        return true;
                    }
                }

                // KeyPreview: let the form see the key first
                if (form.KeyPreview) {
                    OnKeyDown (kd_e);
                    if (kd_e.Handled)
                        return true;
                    adapter.RaiseKeyDown (kd_e);
                    return kd_e.Handled;
                }
            }

            OnKeyDown (kd_e);

            if (kd_e.Handled)
                return true;

            adapter.RaiseKeyDown (kd_e);
            return kd_e.Handled;
        }

        /// <summary>Routes a key-up. Returns true if handled.</summary>
        internal bool HandleKeyUp (Keys keys)
        {
            var ku_e = new KeyEventArgs (keys);

            OnKeyUp (ku_e);

            if (ku_e.Handled)
                return true;

            adapter.RaiseKeyUp (ku_e);
            return ku_e.Handled;
        }

        /// <summary>Routes text input. Returns true if handled.</summary>
        internal bool HandleTextInput (string text)
        {
            if (string.IsNullOrEmpty (text))
                return false;

            var kp_e = new KeyPressEventArgs (text, Keys.None);

            OnKeyPress (kp_e);

            if (kp_e.Handled)
                return true;

            adapter.RaiseKeyPress (kp_e);
            return kp_e.Handled;
        }

        /// <summary>Called after the client area is (re)laid out due to a size change. Override to react to resizes.</summary>
        protected virtual void OnClientLayoutChanged () { }

        /// <summary>Raises the MaximumSizeChanged event.</summary>
        protected virtual void OnMaximumSizeChanged (EventArgs e) => MaximumSizeChanged?.Invoke (this, e);

        /// <summary>Raises the MinimumSizeChanged event.</summary>
        protected virtual void OnMinimumSizeChanged (EventArgs e) => MinimumSizeChanged?.Invoke (this, e);

        /// <summary>Paints the Form.</summary>
        protected internal virtual void OnPaint (PaintEventArgs e) { }

        /// <summary>Paints the Form's background.</summary>
        protected internal virtual void OnPaintBackground (PaintEventArgs e)
        {
            e.Canvas.DrawBackground (Bounds, CurrentStyle);
        }

        /// <summary>Raises the Shown event.</summary>
        protected virtual void OnShown (EventArgs e) => Shown?.Invoke (this, e);

        private void OnVisibleChanged (EventArgs e)
        {
            adapter.RaiseParentVisibleChanged (e);
        }

        private bool PointInDoubleClickRange (System.Drawing.Point point)
        {
            if (Math.Abs (point.X - last_click_point.X) > DOUBLE_CLICK_MOVEMENT)
                return false;

            return Math.Abs (point.Y - last_click_point.Y) <= DOUBLE_CLICK_MOVEMENT;
        }

        /// <summary>Converts a point from screen coordinates to window coordinates.</summary>
        public System.Drawing.Point PointToClient (System.Drawing.Point point) => Backend.PointToClient (point);

        /// <summary>Converts a point from window coordinates to screen coordinates.</summary>
        public System.Drawing.Point PointToScreen (System.Drawing.Point point) => Backend.PointToScreen (point);

        /// <summary>Gets or sets whether the window is resizable.</summary>
        public bool Resizeable { get; set; }

        private System.Drawing.Size ScaledClientSize => new System.Drawing.Size (
            (int)(Backend.ClientSize.Width * Scaling),
            (int)(Backend.ClientSize.Height * Scaling));

        /// <summary>Gets the scaled bounds of the form not including borders.</summary>
        public System.Drawing.Rectangle ScaledDisplayRectangle => new System.Drawing.Rectangle (
            CurrentStyle.Border.Left.GetWidth (),
            CurrentStyle.Border.Top.GetWidth (),
            ScaledClientSize.Width - CurrentStyle.Border.Right.GetWidth () - CurrentStyle.Border.Left.GetWidth (),
            ScaledClientSize.Height - CurrentStyle.Border.Top.GetWidth () - CurrentStyle.Border.Bottom.GetWidth ());

        /// <summary>Gets or sets the scaled size of the window.</summary>
        public System.Drawing.Size ScaledSize => ScaledClientSize;

        /// <summary>Gets the current scale factor of the window.</summary>
        public double Scaling => Backend.Scaling;

        /// <summary>Gets the current scale factor of the desktop.</summary>
        public double DesktopScaling => Backend.Scaling;

        internal void SetCursor (Cursor cursor) => current_cursor = cursor;

        internal virtual void SetWindowStartupLocation (WindowBase? owner = null) { }

        // Lets a subclass (Form) divert Show() into being hosted inside another window — an MDI child is
        // placed in its parent's MDI client area rather than getting its own top-level OS window.
        internal virtual bool TryShowHosted () => false;

        /// <summary>Displays the window to the user.</summary>
        public void Show ()
        {
            if (TryShowHosted ())
                return;

            visible = true;
            OnVisibleChanged (EventArgs.Empty);

            SetWindowStartupLocation ();
            Backend.Show ();

            if (this is Form f)
                Application.OpenForms.Add (f);

            if (!shown) {
                shown = true;
                OnShown (EventArgs.Empty);
            }
        }

        internal void ShowDialog (WindowBase parent)
        {
            visible = true;
            OnVisibleChanged (EventArgs.Empty);

            SetWindowStartupLocation (parent);
            parent.Backend.Enabled = false;
            Backend.Show ();

            if (this is Form f)
                Application.OpenForms.Add (f);

            if (!shown) {
                shown = true;
                OnShown (EventArgs.Empty);
            }
        }

        /// <summary>Raised when the window is shown.</summary>
        public event EventHandler? Shown;

        /// <summary>Gets the unscaled size of the window.</summary>
        public System.Drawing.Size Size => new System.Drawing.Size (
            Backend.ClientSize.Width,
            Backend.ClientSize.Height);

        /// <summary>Gets or sets the startup location of the window.</summary>
        public FormStartPosition StartPosition { get; set; } = FormStartPosition.CenterScreen;

        /// <summary>Gets the ControlStyle properties for this instance of the window.</summary>
        public virtual ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets whether the window is displayed to the user.</summary>
        internal bool visible;

        /// <summary>Gets or sets whether the window is displayed. Setting mirrors WinForms semantics:
        /// true shows the window, false hides it.</summary>
        public bool Visible {
            get => visible;
            set {
                if (visible == value)
                    return;
                if (value)
                    Show ();
                else
                    Hide ();
            }
        }

        // ── WinForms layout/handle/color compatibility ───────────────────────────
        // Form sits on a separate inheritance branch from Control (Form : WindowBase, not
        // Form : ContainerControl as in WinForms), so it does not inherit Control's layout
        // members. These shims forward to the root ControlAdapter — which IS a Control and
        // already hosts the window's children — so migrated WinForms code that calls
        // SuspendLayout/ResumeLayout/PerformLayout on a Form compiles and behaves correctly.

        /// <summary>Temporarily suspends the layout logic for the window's controls.</summary>
        public void SuspendLayout () => adapter.SuspendLayout ();

        /// <summary>Resumes normal layout logic, optionally forcing an immediate layout.</summary>
        public void ResumeLayout (bool performLayout = true) => adapter.ResumeLayout (performLayout);

        /// <summary>Forces the window's controls to apply layout logic.</summary>
        public void PerformLayout () => adapter.PerformLayout ();

        /// <summary>
        /// Gets whether the window's backing handle has been created. In Majorsilence.Forms the
        /// platform handle exists once the window has been shown; migrated code uses this to guard
        /// cross-thread Invoke/BeginInvoke calls.
        /// </summary>
        public bool IsHandleCreated => shown;

        /// <summary>
        /// Gets or sets the background color of the window. Convenience wrapper over
        /// <see cref="ControlStyle.BackgroundColor"/>, mirroring <see cref="Control.BackColor"/>.
        /// </summary>
        public System.Drawing.Color BackColor {
            get => Style.BackgroundColor?.ToDrawingColor () ?? Style.GetBackgroundColor ().ToDrawingColor ();
            set {
                Style.BackgroundColor = value.ToSKColor ();
                Invalidate ();
            }
        }
    }
}
