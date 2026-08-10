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
        internal void OnBackendClosed ()
        {
            OnClosed (EventArgs.Empty);

            // WinForms raises FormClosed after the form has closed, for every close path -- programmatic
            // Close(), the window's close button, MDI child removal -- not just dialogs. Fire it once here
            // (FormClosing already fired before the close via OnClosing/OnBackendClosing) so ordinary forms
            // get FormClosed too, in FormClosing-then-FormClosed order.
            (this as Form)?.RaiseFormClosed ();
        }

        // Set while a programmatic Close() is running so the backend's own closing callback doesn't
        // re-raise Closing/FormClosing: WinForms raises FormClosing exactly once per close.
        private bool _closingHandled;

        /// <summary>Called by the backend when the window is about to close. Returns true to cancel.</summary>
        internal bool OnBackendClosing ()
        {
            // A programmatic Close() already ran the closing sequence (and would have returned early if it
            // was cancelled), so don't fire it again when Backend.Close() calls back into here.
            if (_closingHandled)
                return false;

            if (this is Form f) {
                var args = new System.ComponentModel.CancelEventArgs ();
                f.RaiseClosing (args);
                return args.Cancel;
            }

            return false;
        }

        /// <summary>
        /// Gets whether the backend currently considers this window active. Tracked directly (rather
        /// than inferred from event ordering) because a brand-new popup's real Activated notification
        /// was empirically observed arriving BEFORE its parent's Deactivated on at least one real
        /// desktop (Linux/Mutter/XWayland) -- the opposite of what a naive "deactivate now, activate
        /// later" assumption expects. See <see cref="Application.ScheduleClosePopupsOnDeactivate"/>,
        /// which reads this directly instead of comparing an activation counter against a snapshot.
        /// </summary>
        internal bool IsActive { get; private set; }

        /// <summary>Called by the backend when the window is activated.</summary>
        internal void OnBackendActivated ()
        {
            IsActive = true;
            OnActivated (EventArgs.Empty);
        }

        /// <summary>Raises the Activated event.</summary>
        protected virtual void OnActivated (EventArgs e) => Activated?.Invoke (this, e);

        /// <summary>Raises the Deactivate event.</summary>
        protected virtual void OnDeactivate (EventArgs e) => Deactivated?.Invoke (this, e);

        /// <summary>Raises the Closed event.</summary>
        protected virtual void OnClosed (EventArgs e) => Closed?.Invoke (this, e);

        /// <summary>
        /// Raises the Resize event. <c>Resize</c> is an alias of <c>SizeChanged</c> here, so this
        /// forwards rather than raising a second time.
        /// </summary>
        protected virtual void OnResize (EventArgs e) => OnSizeChanged (e);

        /// <summary>Called by the backend when the window is deactivated.</summary>
        internal void OnBackendDeactivated ()
        {
            IsActive = false;

            // Don't dismiss synchronously: showing a popup deactivates its parent (and a submenu
            // deactivates its parent popup). See Application.ScheduleClosePopupsOnDeactivate.
            Application.ScheduleClosePopupsOnDeactivate ();
            OnDeactivate (EventArgs.Empty);
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

                f.RaiseClosing (args);

                if (args.Cancel)
                    return;

                Application.OpenForms.Remove (f);
            }

            // Closing already ran above; suppress the backend's re-entrant OnBackendClosing so
            // FormClosing isn't raised a second time (WinForms raises it once per close).
            _closingHandled = true;
            try {
                Backend.Close ();
            } finally {
                _closingHandled = false;
            }
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
                OnEnabledChanged (EventArgs.Empty);
            }
        }

        /// <summary>Raises the EnabledChanged event.</summary>
        /// <remarks>
        /// Control declares this as a protected virtual and ported window code overrides it (to repaint
        /// disabled chrome, typically); it is declared here because a window is not a Control.
        /// </remarks>
        protected virtual void OnEnabledChanged (EventArgs e) => EnabledChanged?.Invoke (this, e);

        /// <summary>
        /// Sets the window's bounds, honouring which components <paramref name="specified"/> selects.
        /// Mirrors Control.SetBoundsCore, the single choke-point WinForms code overrides to constrain
        /// or snap a window's geometry.
        /// </summary>
        protected virtual void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            var location = Location;
            var size = Size;

            var newX = specified.HasFlag (BoundsSpecified.X) ? x : location.X;
            var newY = specified.HasFlag (BoundsSpecified.Y) ? y : location.Y;
            var newWidth = specified.HasFlag (BoundsSpecified.Width) ? width : size.Width;
            var newHeight = specified.HasFlag (BoundsSpecified.Height) ? height : size.Height;

            if (newX != location.X || newY != location.Y)
                Location = new System.Drawing.Point (newX, newY);

            if (newWidth != size.Width || newHeight != size.Height)
                Backend.Size = new System.Drawing.Size (newWidth, newHeight);
        }

        /// <summary>Raised when <see cref="Enabled"/> changes. Mirrors WinForms Control.EnabledChanged (modal dialogs toggle their owner through this property).</summary>
        public event EventHandler? EnabledChanged;

        /// <summary>Raised when the window is closed.</summary>
        public event EventHandler? Closed;

        /// <summary>Gets the collection of controls contained by the window.</summary>
        public Control.ControlCollection Controls => adapter.Controls;

        /// <summary>Gets or sets the window's default font. Mirrors WinForms Form.Font; forwarded to
        /// the root control adapter so child controls inherit it.</summary>
        public virtual Majorsilence.Forms.Drawing.Font? Font {
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
                OnResize (EventArgs.Empty);
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

        /// <summary>
        /// Gets the client area of the window, as WinForms' <c>Control.ClientRectangle</c> does: the size
        /// of the client area at the origin. A form paints and hit-tests against this constantly, and it
        /// is inherited for free on a WinForms Form; here Form derives from this type rather than from
        /// Control, so it has to be declared.
        /// </summary>
        public System.Drawing.Rectangle ClientRectangle =>
            new System.Drawing.Rectangle (System.Drawing.Point.Empty, Backend.ClientSize);

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

            // A frame-hosted form has no OS window to hide -- hiding it means hiding the frame that
            // composites it. Calling Backend.Hide would be a no-op at best on a window that was never
            // shown, leaving the form still painted inside its host. The popup bookkeeping below is
            // shared: it tracks a window that is going away regardless of how it was displayed.
            if (this is Form { PanelHost: { } frame })
                frame.Visible = false;
            else
                Backend.Hide ();

            if (Application.ActivePopupWindow == this)
                Application.ActivePopupWindow = null;

            OnVisibleChanged (EventArgs.Empty);
        }

        /// <summary>Marks the entire window as needing to be redrawn.</summary>
        public virtual void Invalidate ()
        {
            // The backend schedules the repaint via Avalonia's InvalidateVisual, which must run on the
            // UI thread. WinForms code frequently invalidates from an async continuation that -- absent a
            // UI SynchronizationContext -- resumes on a thread-pool thread (e.g. a grid rebound after
            // awaiting its data). Calling the backend directly from there would no-op, leaving freshly
            // loaded content unpainted until the next input event. Marshal to the UI thread when needed;
            // Post is fire-and-forget, so this never blocks (and cannot deadlock sync-over-async code).
            if (Majorsilence.Forms.Backends.Platform.Backend.CheckAccess ())
                Backend.Invalidate ();
            else
                Majorsilence.Forms.Backends.Platform.Backend.Post (Backend.Invalidate);
        }

        /// <summary>Marks the specified portion of the window as needing to be redrawn.</summary>
        public void Invalidate (System.Drawing.Rectangle rectangle) => Invalidate ();

        /// <summary>Marks the window as needing to be redrawn. Mirrors WinForms Invalidate(bool);
        /// children repaint with the window here regardless.</summary>
        public void Invalidate (bool invalidateChildren) => Invalidate ();

        /// <summary>Forces the window to repaint. Mirrors WinForms Control.Refresh.</summary>
        public void Refresh () => Invalidate ();

        /// <summary>Validates the last invalidated control. Always true — the compat window has no implicit validation pipeline. Mirrors WinForms ContainerControl.Validate.</summary>
        public bool Validate () => true;

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

        /// <summary>Gets or sets user data associated with the window. Mirrors WinForms Control.Tag.</summary>
        public object? Tag { get; set; }

        /// <summary>Raised when the window surface is painted. Declared for WinForms source compat; the compat window paints through its root adapter and does not raise this yet.</summary>
#pragma warning disable CS0067
        public event PaintEventHandler? Paint;
#pragma warning restore CS0067

        /// <summary>
        /// Gets or sets the context menu shown when the window itself is right-clicked. Stored for
        /// designer compat; the compat window does not surface it yet (controls' own menus work).
        /// </summary>
        public ContextMenuStrip? ContextMenuStrip { get; set; }

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
            // A press can be the first pointer event a window sees (click-through onto an inactive
            // window), so it counts as an entry too.
            TrackPointerInside ();

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
            // Raise MouseEnter before the resize-border shortcut below returns: the window chrome is
            // part of the window, so entering over a border edge is still an entry.
            TrackPointerInside ();

            if (Resizeable && HandleMouseMove (x, y))
                return;

            var ev = new MouseEventArgs (buttons, 0, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseMove (ev);
        }

        internal void HandlePointerWheel (MouseButtons buttons, int x, int y, System.Drawing.Point delta, Keys keys)
        {
            TrackPointerInside ();

            var ev = new MouseEventArgs (buttons, 0, x, y, delta, keyData: keys);
            adapter.RaiseMouseWheel (ev);

            // WinForms delivers the wheel to the window itself as well, which is how a form scrolls or
            // zooms a view it owns without every child having to forward. Declared here because Form
            // does not derive from Control and so inherits nothing from it.
            OnMouseWheel (ev);
        }

        /// <summary>Raises the <see cref="MouseWheel"/> event.</summary>
        protected virtual void OnMouseWheel (MouseEventArgs e) => MouseWheel?.Invoke (this, e);

        /// <summary>Raised when the mouse wheel turns over this window.</summary>
        public event MouseEventHandler? MouseWheel;

        internal void HandlePointerExited (MouseButtons buttons, int x, int y, Keys keys)
        {
            var ev = new MouseEventArgs (buttons, 0, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseLeave (ev);

            // After the children have been told, raise the window's own MouseLeave (WinForms leaves the
            // innermost control first and unwinds outwards).
            TrackPointerOutside ();
        }

        // ── Neutral gesture handlers (backend-specific; called only by backends that detect these,
        // e.g. the Avalonia backend's attached GestureRecognizers -- see AvaloniaGestureWiring. Not
        // part of IWindowBackend/IPlatformBackend, so a backend that doesn't call these simply never
        // raises gesture events, with no interface to implement and no effect on its own behavior) ──

        internal void HandleLongPress (int x, int y)
            => adapter.RaiseLongPress (new LongPressEventArgs (x, y));

        internal void HandlePinch (int x, int y, double scale, double angle, double angleDelta)
            => adapter.RaisePinch (new PinchGestureEventArgs (x, y, scale, angle, angleDelta));

        internal void HandleSwipe (int x, int y, double velocityX, double velocityY, SwipeDirection direction)
            => adapter.RaiseSwipe (new SwipeGestureEventArgs (x, y, velocityX, velocityY, direction));

        internal void HandleScrollGesture (int x, int y, int deltaX, int deltaY)
            => adapter.RaiseScrollGesture (new ScrollGestureEventArgs (x, y, new System.Drawing.Point (deltaX, deltaY)));

        // WinForms parity: without KeyPreview a form's own key events fire only when no child
        // control has focus; keys otherwise go straight to the focused control. With KeyPreview
        // the form sees (and may handle) the key before the focused control does.
        private bool FormSeesKeyFirst => this is not Form form || form.KeyPreview || adapter.SelectedControl is null;

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
            }

            if (FormSeesKeyFirst) {
                OnKeyDown (kd_e);

                if (kd_e.Handled)
                    return true;
            }

            if (TryRouteToActiveMdiChild (child => child.HandleKeyDown (keys), out var mdiHandled))
                return mdiHandled;

            adapter.RaiseKeyDown (kd_e);
            return kd_e.Handled;
        }

        /// <summary>
        /// An MDI child never owns an on-screen window — its frame composites the child's content into
        /// the container's, and <see cref="MdiChildWindow"/> forwards pointer input inward. Keyboard has
        /// to make the same trip: the container is the only window the backend delivers key events to,
        /// so without this the active child's focused control never hears a keystroke.
        ///
        /// Only when the container itself has nothing focused. A container is free to own focusable
        /// chrome of its own (a toolbar's text box, say), and while that holds focus the keys are its
        /// own — matching WinForms, where focus is genuinely in one place or the other.
        /// </summary>
        private bool TryRouteToActiveMdiChild (Func<Form, bool> dispatch, out bool handled)
        {
            handled = false;

            if (this is not Form { ActiveMdiChild: { } child } || ReferenceEquals (child, this))
                return false;

            if (adapter.SelectedControl is not null)
                return false;

            handled = dispatch (child);
            return true;
        }

        /// <summary>Routes a key-up. Returns true if handled.</summary>
        internal bool HandleKeyUp (Keys keys)
        {
            var ku_e = new KeyEventArgs (keys);

            if (FormSeesKeyFirst) {
                OnKeyUp (ku_e);

                if (ku_e.Handled)
                    return true;
            }

            if (TryRouteToActiveMdiChild (child => child.HandleKeyUp (keys), out var mdiHandled))
                return mdiHandled;

            adapter.RaiseKeyUp (ku_e);
            return ku_e.Handled;
        }

        /// <summary>Routes text input. Returns true if handled.</summary>
        internal bool HandleTextInput (string text)
        {
            if (string.IsNullOrEmpty (text))
                return false;

            var kp_e = new KeyPressEventArgs (text, Keys.None);

            if (FormSeesKeyFirst) {
                OnKeyPress (kp_e);

                if (kp_e.Handled)
                    return true;
            }

            if (TryRouteToActiveMdiChild (child => child.HandleTextInput (text), out var mdiHandled))
                return mdiHandled;

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

        /// <summary>Raised when <see cref="Visible"/> changes. Mirrors WinForms Control.VisibleChanged.</summary>
        public event EventHandler? VisibleChanged;

        /// <summary>Raises the VisibleChanged event and propagates it to the window's children.</summary>
        protected virtual void OnVisibleChanged (EventArgs e)
        {
            adapter.RaiseParentVisibleChanged (e);
            VisibleChanged?.Invoke (this, e);
        }

        /// <summary>
        /// Raised when the window moves. A WinForms alias of <see cref="LocationChanged"/>, which is
        /// what ported code that repositions satellite windows (drop shadows, tool windows) hooks.
        /// </summary>
        public event EventHandler? Move {
            add => LocationChanged += value;
            remove => LocationChanged -= value;
        }

        /// <summary>
        /// Forces the window to repaint any invalidated regions immediately. Majorsilence.Forms repaints
        /// on the backend's own tick rather than synchronously, so this is an <see cref="Invalidate()"/>
        /// -- the paint happens on the next tick instead of before this call returns.
        /// </summary>
        public void Update () => Invalidate ();

        /// <summary>Sets the specified <see cref="ControlStyles"/> flag on the window's root adapter.</summary>
        /// <remarks>
        /// Control declares this and ported window code calls it in its constructor (opting into
        /// double-buffering and user paint, typically). A window is not a Control here, so it forwards
        /// to the adapter that actually hosts the control tree.
        /// </remarks>
        public void SetStyle (ControlStyles flag, bool value) => adapter.SetStyle (flag, value);

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

        /// <summary>
        /// Converts a rectangle from client to screen coordinates. The companion to
        /// <see cref="PointToScreen"/>, and the shape a form measuring its own chrome uses
        /// (<c>RectangleToScreen (ClientRectangle)</c>). Inherited from Control on a WinForms Form;
        /// declared here because Form derives from this type instead.
        /// </summary>
        public System.Drawing.Rectangle RectangleToScreen (System.Drawing.Rectangle rect)
        {
            var origin = PointToScreen (System.Drawing.Point.Empty);
            return new System.Drawing.Rectangle (rect.X + origin.X, rect.Y + origin.Y, rect.Width, rect.Height);
        }

        /// <summary>Gets or sets whether the window is resizable.</summary>
        public bool Resizeable { get; set; }

        // Two more members a WinForms Form gets by inheriting Control, which this one cannot. Both read
        // exactly as they do on Control -- ModifierKeys is static state shared by the whole app, and the
        // cursor default is the arrow -- so they simply forward rather than duplicating anything.

        /// <summary>Gets the modifier keys currently held down. Mirrors <see cref="Control.ModifierKeys"/>.</summary>
        public static Keys ModifierKeys => Control.ModifierKeys;

        /// <summary>Gets the cursor used when none is set. Mirrors <see cref="Control.DefaultCursor"/>.</summary>
        protected virtual Cursor DefaultCursor => Cursor.Default;

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

        // Whether Show() should also activate this window. Form overrides it from the WinForms-shaped
        // ShowWithoutActivation hook; see that property.
        internal virtual bool ShowsActivated => true;

        /// <summary>Displays the window to the user.</summary>
        public void Show ()
        {
            if (TryShowHosted ())
                return;

            SetWindowStartupLocation ();
            Backend.ShowActivated = ShowsActivated;
            Backend.Show ();
            EnsureShownBookkeeping (activated: ShowsActivated);
        }

        internal void ShowDialog (WindowBase parent)
        {
            SetWindowStartupLocation (parent);
            parent.Backend.Enabled = false;
            Backend.Show ();
            EnsureShownBookkeeping ();
        }

        // Runs the WinForms-compat "window just became visible" bookkeeping exactly once: Load/Shown
        // events, Application.OpenForms membership, and the initial IsActive assumption. Called by
        // Show()/ShowDialog() above, and also by a host app's own native window becoming visible when
        // a Form is handed out via AvaloniaHostInterop.ToAvaloniaWindow/UnoHostInterop.ToUnoWindow
        // instead of being shown through Form.Show() -- so behaviour is identical regardless of which
        // side actually triggered the native show. Guarded by `visible` so calling it more than once
        // (e.g. a host window's Opened/Activated firing repeatedly) is harmless.
        internal void EnsureShownBookkeeping (bool activated = true)
        {
            if (visible)
                return;

            visible = true;
            OnVisibleChanged (EventArgs.Empty);

            // Join OpenForms BEFORE Load is raised. Form.ShowDialog and MessageBox.Show pick their
            // modal owner out of Application.OpenForms and fall back to a non-blocking Show() when it
            // is empty, so registering afterwards made every dialog opened from a Load handler -- the
            // standard WinForms "prompt for missing configuration on startup" shape -- silently
            // non-modal on the first form: it flashed up and the handler ran straight on with nothing
            // filled in.
            if (this is Form f)
                Application.OpenForms.Add (f);

            EnsureLoaded ();            // WinForms raises Load around the window's first display.

            // Assume active the moment we ask the backend to show one of our own windows, rather than
            // waiting for its real Activated event (which, empirically, can arrive either before or
            // after this call returns depending on the platform) -- see IsActive's doc comment. The
            // real event still fires and reconfirms this when it eventually arrives. A window shown
            // without activation is the exception: it never becomes active, so assuming it did would
            // make the window it appeared over look deactivated to the app.
            if (activated)
                IsActive = true;

            if (!shown) {
                shown = true;
                OnShown (EventArgs.Empty);
            }
        }

        // Raises the one-time Load event before the window is first displayed. WinForms fires Load during
        // the show sequence, before the form is painted/shown -- not coupled to Shown (which fires after
        // first display). Overridden by Form; a no-op for non-Form windows.
        internal virtual void EnsureLoaded () { }

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
        public virtual System.Drawing.Color BackColor {
            get => Style.BackgroundColor?.ToDrawingColor () ?? Style.GetBackgroundColor ().ToDrawingColor ();
            set {
                Style.BackgroundColor = value.ToSKColor ();
                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the foreground (text) color of the window. Convenience wrapper over
        /// <see cref="ControlStyle.ForegroundColor"/>, mirroring <see cref="Control.ForeColor"/>.
        /// </summary>
        public virtual System.Drawing.Color ForeColor {
            get => Style.ForegroundColor?.ToDrawingColor () ?? Style.GetForegroundColor ().ToDrawingColor ();
            set {
                Style.ForegroundColor = value.ToSKColor ();
                Invalidate ();
            }
        }

        /// <summary>Gets the default background color of a window. Matches <see cref="Control.DefaultBackColor"/>.</summary>
        public static System.Drawing.Color DefaultBackColor => SystemColors.Control;

        /// <summary>Gets or sets whether the window automatically sizes itself to fit its content. Stub.</summary>
        public bool AutoSize { get; set; }

        // ── Control-parity surface ───────────────────────────────────────────────
        // Form sits on a separate inheritance branch from Control (see the layout/handle/color
        // shims above), so plain Control members have to exist here to be reachable from a Form.
        // Members that have a meaningful equivalent forward to the root ControlAdapter -- which IS
        // a Control (a ScrollableControl) and already hosts the window's children -- so they behave
        // rather than merely compile. The rest are stored properties per the documented stub policy:
        // Anchor/Dock/TabIndex describe how a control is placed inside a *parent*, and a top-level
        // window has none, so they do nothing meaningful even in real WinForms.

        /// <summary>
        /// Gets or sets the edges of the container the window is anchored to. Stored for designer and
        /// source parity; a top-level window has no layout parent to anchor against.
        /// </summary>
        public AnchorStyles Anchor { get; set; } = AnchorStyles.Top | AnchorStyles.Left;

        /// <summary>
        /// Gets or sets which container edge the window is docked to. Stored for designer and source
        /// parity; a top-level window has no layout parent to dock into.
        /// </summary>
        public DockStyle Dock { get; set; } = DockStyle.None;

        /// <summary>
        /// Gets or sets the tab order of the window within its container. Stored for designer and
        /// source parity; a top-level window is not part of a parent's tab order (use the child
        /// controls' own <see cref="Control.TabIndex"/> for tabbing inside the window).
        /// </summary>
        public int TabIndex { get; set; }

        /// <summary>
        /// Gets or sets the padding inside the window's client area. Forwarded to the root control
        /// adapter, whose <see cref="Control.DisplayRectangle"/> it deflates — so, as in WinForms,
        /// docked and anchored child controls really are inset by it.
        /// </summary>
        public Padding Padding {
            get => adapter is null ? Padding.Empty : adapter.Padding;
            set { if (adapter is not null) adapter.Padding = value; }
        }

        /// <summary>
        /// Gets or sets the window region. Stored for source parity, matching
        /// <see cref="Control.Region"/> (also stored) — Majorsilence.Forms does not clip a window to
        /// a non-rectangular region yet.
        /// </summary>
        public Majorsilence.Forms.Drawing.Region? Region { get; set; }

        /// <summary>
        /// Gets or sets the reading order of the window. Forwarded to the root control adapter, which
        /// is the parent of every child control, so children left on
        /// <see cref="Majorsilence.Forms.RightToLeft.Inherit"/> resolve through this the same way they
        /// resolve through a parent Control in WinForms.
        /// </summary>
        public RightToLeft RightToLeft {
            get => adapter is null ? RightToLeft.No : adapter.RightToLeft;
            set { if (adapter is not null) adapter.RightToLeft = value; }
        }

        /// <summary>
        /// Gets or sets whether the window shows scrollbars when its children don't fit. Forwarded to
        /// the root control adapter (a <see cref="ScrollableControl"/>), so this really scrolls.
        /// </summary>
        public bool AutoScroll {
            get => adapter is not null && adapter.AutoScroll;
            set { if (adapter is not null) adapter.AutoScroll = value; }
        }

        /// <summary>Gets or sets the auto-scroll margin. Forwarded to the root control adapter.</summary>
        public System.Drawing.Size AutoScrollMargin {
            get => adapter is null ? System.Drawing.Size.Empty : adapter.AutoScrollMargin;
            set { if (adapter is not null) adapter.AutoScrollMargin = value; }
        }

        /// <summary>Gets or sets the minimum size of the auto-scroll area. Forwarded to the root control adapter.</summary>
        public System.Drawing.Size AutoScrollMinSize {
            get => adapter is null ? System.Drawing.Size.Empty : adapter.AutoScrollMinSize;
            set { if (adapter is not null) adapter.AutoScrollMinSize = value; }
        }

        /// <summary>Gets or sets the current scroll position. Forwarded to the root control adapter.</summary>
        public System.Drawing.Point AutoScrollPosition {
            get => adapter is null ? System.Drawing.Point.Empty : adapter.AutoScrollPosition;
            set { if (adapter is not null) adapter.AutoScrollPosition = value; }
        }

        /// <summary>Sets the auto-scroll margin. Mirrors WinForms ScrollableControl.SetAutoScrollMargin.</summary>
        public void SetAutoScrollMargin (int x, int y) => AutoScrollMargin = new System.Drawing.Size (x, y);

        // ── Window-level mouse enter/leave ───────────────────────────────────────
        // Real, not a stub: every backend already reports pointer exit into HandlePointerExited, and
        // any pointer press/move/wheel arriving means the pointer is over this window. There is no
        // matching "pointer entered" backend callback, so entry is inferred from the first pointer
        // event that arrives while the pointer is recorded as outside -- the same edge-triggered
        // state machine Control uses for its children (see Control.RaiseMouseMove).

        private bool mouse_inside;

        /// <summary>
        /// Raised when the mouse pointer enters the window. Mirrors WinForms Control.MouseEnter as it
        /// applies to a top-level window: it tracks the pointer entering the window's whole surface
        /// (chrome included), so it fires once per entry and not again when the pointer crosses
        /// between the window's child controls.
        /// </summary>
        public event EventHandler? MouseEnter;

        /// <summary>
        /// Raised when the mouse pointer leaves the window. Mirrors WinForms Control.MouseLeave as it
        /// applies to a top-level window (see <see cref="MouseEnter"/> for the tracking scope).
        /// </summary>
        public event EventHandler? MouseLeave;

        /// <summary>Gets whether the mouse pointer is currently over the window.</summary>
        internal bool IsMouseOver => mouse_inside;

        /// <summary>Raises the <see cref="MouseEnter"/> event.</summary>
        protected virtual void OnMouseEnter (EventArgs e) => MouseEnter?.Invoke (this, e);

        /// <summary>Raises the <see cref="MouseLeave"/> event.</summary>
        protected virtual void OnMouseLeave (EventArgs e) => MouseLeave?.Invoke (this, e);

        // Records the pointer as being over the window, raising MouseEnter on the outside→inside edge.
        private void TrackPointerInside ()
        {
            if (mouse_inside)
                return;

            mouse_inside = true;
            OnMouseEnter (EventArgs.Empty);
        }

        // Records the pointer as having left the window, raising MouseLeave on the inside→outside edge.
        private void TrackPointerOutside ()
        {
            if (!mouse_inside)
                return;

            mouse_inside = false;
            OnMouseLeave (EventArgs.Empty);
        }
    }
}
