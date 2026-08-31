using System.ComponentModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents the base class for windows, like Form and PopupWindow.
    /// </summary>
    public abstract partial class WindowBase : Component, IBindableComponent
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
            _softKeyboardObserver = new SoftKeyboardObserver (this);
        }

        // Watches the focus choke-point and drives the backend's on-screen keyboard. One event
        // subscription; the backend call is a no-op on desktop, so this costs effectively nothing there.
        private SoftKeyboardObserver? _softKeyboardObserver;

        // ── Lifecycle callbacks (the platform backend invokes these; no platform types involved) ──
        /// <summary>Called by the backend after the window is closed.</summary>
        // Set for the duration of OnBackendClosed. Several paths drive that method -- a programmatic
        // Close, the window's own close button, MDI removal -- and since a non-modal form now disposes
        // itself at the end of the sequence, and disposing tears the window down, the sequence can
        // re-enter itself. Without this guard a single close raised Closed, FormClosed and
        // HandleDestroyed twice and notified ApplicationContext twice.
        private bool in_backend_closed;

        internal void OnBackendClosed ()
        {
            if (in_backend_closed)
                return;

            in_backend_closed = true;

            try {
                RunBackendClosed ();
            } finally {
                in_backend_closed = false;
            }
        }

        private void RunBackendClosed ()
        {
            // Captured before CompleteClose clears it.
            var wasModal = (this as Form)?.IsModalDialog ?? false;

            // A window takes what it owns with it. Before its own FormClosed, so an owner's cleanup
            // handler sees its tool windows already gone rather than half-closed.
            (this as Form)?.CloseOwnedForms ();

            // A form closed by its own close button never goes through Close(), so this is the only
            // place the bookkeeping Close() does can happen for it: leaving it out kept the form in
            // OpenForms for the life of the process and, for a modal one, left ShowDialog awaiting a
            // result that never arrived.
            if (this is Form closed)
                Application.OpenForms.Remove (closed);

            // A closed window is not visible. `visible` was cleared only by Hide() and Dispose(), so
            // after Close() a form still reported Visible == true -- which broke the common
            // "if (!find.Visible) find.Show (); else find.Activate ();" guard, and made a re-shown form
            // skip EnsureShownBookkeeping entirely so it never re-joined OpenForms.
            if (visible) {
                visible = false;
                OnVisibleChanged (EventArgs.Empty);
            }

            OnClosed (EventArgs.Empty);

            // WinForms raises FormClosed after the form has closed, for every close path -- programmatic
            // Close(), the window's close button, MDI child removal -- not just dialogs. Fire it once here
            // (FormClosing already fired before the close via OnClosing/OnBackendClosing) so ordinary forms
            // get FormClosed too, in FormClosing-then-FormClosed order.
            (this as Form)?.RaiseFormClosed ();

            // After FormClosed, so ShowDialog returns to its caller only once the form is fully closed.
            (this as Form)?.CompleteClose ();

            // Last of all: the handle is what a form's window ultimately is, so its destruction is the
            // final notification a form sends. Code that tracks the set of live forms keys on this rather
            // than on Closed, because it is the point after which the form can safely be forgotten.
            // Routed through Form.DestroyHandle (which itself calls OnHandleDestroyed) so an override of
            // that WinForms-standard hook still runs; other WindowBase subtypes have no such override
            // point, so they still just raise the event directly.
            if (this is Form form)
                form.RaiseDestroyHandle ();
            else
                OnHandleDestroyed (EventArgs.Empty);

            // The handle is gone, so the window is back to its pre-show state: WinForms models the
            // HANDLE as the unit of lifetime and destroys it on every close, which is what makes Load,
            // Shown and FormClosed fire again on the next show. These flags modelled the INSTANCE as
            // shown-once, so a dialog kept as a field and reopened raised neither Load nor Shown, and
            // its second close raised no FormClosed -- a reused Options dialog showed stale data
            // because the Load handler that repopulates it never ran again.
            shown = false;
            (this as Form)?.ResetLifecycleForReshow ();

            // A non-modal form is disposed by its own close, as upstream's WmClose does; a modal one is
            // not, because its caller still has to read DialogResult and may show it again. Without
            // this the designer's `components` -- Timers, BindingSources, ToolTips -- were never
            // disposed, so a Timer started by a closed form went on ticking against it, and Disposed
            // handlers used for cleanup never ran.
            //
            // Decided here rather than in CompleteClose because Form.Close calls CompleteClose a
            // second time after the backend sequence has already cleared dialog_task -- so asking
            // "was this modal?" there answered no for a dialog and disposed it out from under the
            // caller about to read its result. `wasModal` is captured at the top of this method,
            // before anything clears it.
            if (!wasModal && this is Form { IsDisposed: false } closing)
                closing.Dispose ();
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
            OnGotFocus (EventArgs.Empty);
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

        /// <summary>
        /// Raises the Move event. <c>Move</c> is an alias of <c>LocationChanged</c> here, so this
        /// forwards rather than raising a second time -- same shape as <see cref="OnResize"/>.
        /// </summary>
        /// <remarks>
        /// Present because <c>Form</c> derives from <c>Control</c> in WinForms and so inherits its
        /// overridable hooks; WindowBase is not a Control, so each one ported code overrides has to be
        /// declared here explicitly.
        /// </remarks>
        protected virtual void OnMove (EventArgs e) => OnLocationChanged (e);

        /// <summary>
        /// Gets the height, in pixels, of one line of text in the window's current font.
        /// </summary>
        /// <remarks>
        /// WinForms puts this on Control, which Form inherits; WindowBase is not a Control, so it is
        /// declared here as well. Ported layout arithmetic uses it as a scale-aware unit (a resize
        /// gripper sized <c>FontHeight / 3</c>, for instance).
        /// </remarks>
        protected int FontHeight => Font?.Height ?? Majorsilence.Forms.SystemFonts.DefaultFont.Height;

        /// <summary>Raised when the window gains focus.</summary>
        public event EventHandler? GotFocus;

        /// <summary>Raised when the window loses focus.</summary>
        public event EventHandler? LostFocus;

        /// <summary>Raises the GotFocus event.</summary>
        /// <remarks>
        /// A top-level window has no focus state separate from activation on these backends, so this
        /// rides on activation -- which is also when WinForms raises it for a Form in practice. Code
        /// that pauses on focus loss (a media player dimming when you switch away, say) behaves the
        /// same; code that distinguishes activation from focus does not.
        /// </remarks>
        protected virtual void OnGotFocus (EventArgs e) => GotFocus?.Invoke (this, e);

        /// <summary>Raises the LostFocus event.</summary>
        /// <inheritdoc cref="OnGotFocus"/>
        protected virtual void OnLostFocus (EventArgs e) => LostFocus?.Invoke (this, e);

        /// <summary>Called by the backend when the window is deactivated.</summary>
        internal void OnBackendDeactivated ()
        {
            IsActive = false;

            // Don't dismiss synchronously: showing a popup deactivates its parent (and a submenu
            // deactivates its parent popup). See Application.ScheduleClosePopupsOnDeactivate.
            Application.ScheduleClosePopupsOnDeactivate ();
            OnDeactivate (EventArgs.Empty);
            OnLostFocus (EventArgs.Empty);
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
            var wasDisposed = IsDisposed;

            Disposing = true;
            IsDisposed = true;

            if (disposing) {
                _softKeyboardObserver?.Dispose ();
                _softKeyboardObserver = null;

                if (this is Form f)
                    Application.OpenForms.Remove (f);

                if (Application.ActivePopupWindow == this)
                    Application.ActivePopupWindow = null;

                // WinForms destroys the window handle when a form is disposed, so the window leaves the
                // screen whether or not anything called Close first -- and popups are routinely
                // dismissed by disposing them. Without this the backend window stayed up with nothing
                // painting into it, leaving a blank rectangle on screen after the popup had gone.
                //
                // FormClosing is deliberately not raised: disposing is not closing, and WinForms does
                // not raise it either. _closingHandled suppresses the backend's own closing callback
                // for the same reason Close() does.
                // The window is gone, so it is no longer visible -- WinForms clears this when the
                // handle is destroyed, and the paint pipeline reads it. Set before the backend call so
                // it holds even for a backend whose Close is a no-op.
                visible = false;
                shown = false;

                if (!wasDisposed && Backend is not null) {
                    _closingHandled = true;

                    try {
                        Backend.Close ();
                    } catch (Exception) {
                        // A backend window that is already gone is the expected case here, not a
                        // failure -- and throwing out of Dispose would strand the rest of the teardown.
                    } finally {
                        _closingHandled = false;
                    }
                }
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

        /// <summary>
        /// The control standing in for the window's CLIENT surface — what application code puts
        /// controls on, and what the window-level members that describe the client area forward to.
        /// </summary>
        /// <remarks>
        /// The root <c>adapter</c> spans the whole window, chrome included. On a <see cref="Form"/>
        /// that chrome is a title bar this library draws itself, so the two differ, and it is the
        /// client half that <c>Padding</c>, the ambient properties, the context menu and the forwarded
        /// control events mean. Everything genuinely about the window as a whole — bounds, painting,
        /// the input entry point, the focus root — stays on <c>adapter</c>.
        /// </remarks>
        internal virtual Control ContentRoot => adapter;

        /// <summary>
        /// <see cref="ContentRoot"/> as a <see cref="ScrollableControl"/>, for the <c>AutoScroll</c>
        /// surface.
        /// </summary>
        internal ScrollableControl ContentScrollRoot => ContentRoot as ScrollableControl ?? adapter;

        /// <summary>Gets the collection of controls contained by the window.</summary>
        /// <remarks>
        /// Virtual because <see cref="Form"/> separates client from non-client: the title bar it draws
        /// must not appear in — or take space from — the collection application code sees.
        /// </remarks>
        public virtual Control.ControlCollection Controls => adapter.Controls;

        /// <summary>Gets or sets the window's default font. Mirrors WinForms Form.Font; forwarded to
        /// the root control adapter so child controls inherit it.</summary>
        public virtual Majorsilence.Forms.Drawing.Font? Font {
            get => adapter?.Font;
            set {
                if (adapter is null || value is null)
                    return;

                adapter.Font = value;
                OnFontChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets or sets the cursor shown over the window. Mirrors WinForms Form.Cursor.</summary>
        public Cursor? Cursor {
            get => current_cursor;
            set {
                current_cursor = value;

                if (override_cursor is null)
                    Backend?.SetCursor (value?.CursorType ?? Backends.CursorType.Arrow);
            }
        }

        private Cursor? override_cursor;

        /// <summary>
        /// Gets or sets a cursor that takes priority over <see cref="Cursor"/> while it is set, without
        /// disturbing the configured value underneath. Mirrors <c>Control.OverrideCursor</c> -- see its
        /// remarks for why a control (or, here, a window's own content) needs this rather than writing
        /// and restoring <see cref="Cursor"/> itself.
        /// </summary>
        protected Cursor? OverrideCursor {
            get => override_cursor;
            set {
                override_cursor = value;
                Backend?.SetCursor ((value ?? current_cursor)?.CursorType ?? Backends.CursorType.Arrow);
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

            SyncAdapterBounds (logicalW, logicalH);

            // A shaped window paints only inside its region. The region is in logical units and the
            // canvas in physical ones, so the boundary path is scaled across rather than clipped raw.
            var clipped = false;

            if (Region is { } shape) {
                var skRegion = shape.GetSKRegion ();

                canvas.Save ();
                clipped = true;

                if (skRegion.IsEmpty) {
                    // An empty region means "paint nothing" -- how a drag overlay is built before it has
                    // any guides to show. Clipping to the empty PATH would not do it: Skia treats an
                    // empty path as no clip at all, so the window painted in full.
                    canvas.ClipRect (SkiaSharp.SKRect.Empty);
                } else {
                    using var path = skRegion.GetBoundaryPath ();
                    path.Transform (SkiaSharp.SKMatrix.CreateScale ((float)scaling, (float)scaling));
                    canvas.ClipPath (path, SkiaSharp.SKClipOperation.Intersect, antialias: true);
                }
            }

            var e = new PaintEventArgs (skInfo, canvas, scaling);

            OnPaintBackground (e);
            canvas.DrawBorder (new System.Drawing.Rectangle (0, 0, physW, physH), CurrentStyle);
            OnPaint (e);

            // WinForms' Form derives from Control, so a Form.Paint handler runs immediately after
            // OnPaint and before the child controls are drawn. WindowBase is not a Control, so the
            // Paint event it declares has to be raised by hand here -- mirroring Control.RaisePaint,
            // which invokes Paint straight after OnPaint. Without this, `form.Paint += handler`
            // compiles and silently never fires.
            //
            // Drawing here survives: the client area's own background pass below is a no-op, because
            // Control.OnPaintBackground returns early for ControlAdapter, so nothing repaints over
            // this before the children go down on top.
            Paint?.Invoke (this, e);

            // Clip canvas to the inner client area (excludes borders).
            canvas.ClipRect (new SkiaSharp.SKRect (
                physBorderLeft, physBorderTop,
                physW - physBorderRight + 1, physH - physBorderBottom + 1));

            adapter.RaisePaintBackground (e);
            adapter.RaisePaint (e);

            if (clipped)
                canvas.Restore ();

            canvas.Flush ();
        }

        /// <summary>
        /// Gives the root adapter the window's own client bounds and lays the child controls out against
        /// them, ahead of anything painting.
        /// </summary>
        /// <remarks>
        /// <see cref="RenderFrame"/> sizes the adapter from the surface it is handed, and for a long time
        /// that was the only thing that sized it at all: until the first frame was painted the adapter
        /// stayed 0x0, so every docked or anchored child laid out before then measured against nothing.
        /// A <c>Load</c> handler reading a docked panel's Width got 0, an explicit <c>PerformLayout</c>
        /// produced a layout the first paint then silently redid, and headless code -- which never paints
        /// -- could not lay a window out at all without reaching in and sizing the adapter itself.
        /// WinForms has no such window: a form's client rectangle is real as soon as it has a size, well
        /// before anything is drawn.
        /// </remarks>
        internal void SyncAdapterBounds ()
        {
            // A form hosted inside another window (MDI child, panel host) draws through its host's
            // RenderFrame at whatever size the host allots it, which is not this window's client size --
            // there the hosting paint pass is the authority and this stands aside.
            if (IsFrameHosted)
                return;

            SyncAdapterBounds (Backend.ClientSize.Width, Backend.ClientSize.Height);
        }

        // Both callers share the resize bookkeeping so the events fire exactly once per real size change,
        // whichever path notices it first.
        private void SyncAdapterBounds (int logicalW, int logicalH)
        {
            var border = CurrentStyle.Border;
            var borderLeft = border.Left.GetWidth ();
            var borderTop = border.Top.GetWidth ();

            if (adapter.Left == borderLeft && adapter.Top == borderTop &&
                adapter.Width == logicalW && adapter.Height == logicalH)
                return;

            adapter.SetBounds (borderLeft, borderTop, logicalW, logicalH);
            adapter.PerformLayout ();

            // The adapter's pass places the chrome and the client area; the controls the application
            // added live one level further in and need their own. A no-op when the two are the same.
            if (!ReferenceEquals (ContentRoot, adapter))
                ContentRoot.PerformLayout ();
            OnClientLayoutChanged ();
            OnResize (EventArgs.Empty);
        }

        // Whether this window's content is drawn inside another window rather than into a surface of its
        // own. Overridden by Form, which can be an MDI child or panel-hosted.
        internal virtual bool IsFrameHosted => false;

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
        /// <remarks>
        /// The client region, so on a <see cref="Form"/> drawing its own caption this excludes it —
        /// the same region <see cref="Form.ClientSize"/> reports and the one a child at (0, 0) sits in.
        /// </remarks>
        public System.Drawing.Rectangle ClientRectangle =>
            new System.Drawing.Rectangle (System.Drawing.Point.Empty, ClientAreaSize);

        // Backend.ClientSize spans the whole window; Form narrows it to exclude the caption it draws.
        private protected virtual System.Drawing.Size ClientAreaSize => Backend.ClientSize;

        /// <summary>
        /// The device safe-area insets in logical pixels — the strips along the window edges covered by
        /// a status bar, camera notch, rounded corner or home indicator. <see cref="Padding.Empty"/> on
        /// desktop and in the browser; non-empty only on Android/iOS, where the backend pushes it in via
        /// <see cref="HandleSafeAreaChanged"/>. <see cref="Form"/> deflates its client layout by this so
        /// docked and anchored controls stay clear of the unsafe strips.
        /// </summary>
        public Padding SafeArea { get; private set; }

        // ── Neutral single-view push handlers (backend-specific; called only by a backend that has a
        // device safe area or an on-screen keyboard -- the Avalonia single-view host. Same pattern as
        // the gesture handlers below: not part of the IWindowBackend/IPlatformBackend seam, so a backend
        // that never calls them just never insets, with nothing to implement) ──

        /// <summary>Called by the backend when the device safe-area insets change (rotation, keyboard, …).</summary>
        internal void HandleSafeAreaChanged (Padding safeAreaLogical)
        {
            if (SafeArea == safeAreaLogical)
                return;

            SafeArea = safeAreaLogical;
            OnSafeAreaChanged ();
            SyncAdapterBounds ();
        }

        /// <summary>Lets <see cref="Form"/> react to a safe-area change before the relayout.</summary>
        private protected virtual void OnSafeAreaChanged () { }

        /// <summary>
        /// Called by the backend when the on-screen keyboard opens or closes. <paramref name="occludedRectLogical"/>
        /// is the window-relative rectangle the keyboard now covers, or <see cref="System.Drawing.Rectangle.Empty"/>
        /// when it closed. Scrolls the focused control clear of the keyboard.
        /// </summary>
        internal void HandleInputPaneChanged (System.Drawing.Rectangle occludedRectLogical)
        {
            var focused = (this as Form)?.ActiveControl ?? adapter.SelectedControl;
            if (focused is null)
                return;

            if (occludedRectLogical.IsEmpty)
                focused.ScrollControlIntoView (null);
            else
                focused.ScrollControlIntoView (focused, occludedRectLogical.Height);
        }

        /// <summary>
        /// The caret rectangle of the focused text control in logical window coordinates, or null when
        /// no text control is focused. A single-view backend uses it to place the on-screen keyboard's
        /// suggestion strip and to keep the caret visible above the keyboard.
        /// </summary>
        internal System.Drawing.Rectangle? TryGetCaretRectangleLogical ()
        {
            var focused = (this as Form)?.ActiveControl ?? adapter.SelectedControl;
            if (focused is not TextBox tb)
                return null;

            // Accumulate logical offsets up to the adapter -- Control.PointToScreen's non-top branch
            // without the desktop-scale conversion, since we want logical window coordinates.
            var p = tb.GetPositionFromCharIndex (tb.SelectionStart);
            for (Control? c = tb; c is not null and not ControlAdapter; c = c.Parent)
                p.Offset (c.Bounds.Location);

            return new System.Drawing.Rectangle (p.X, p.Y, 1, System.Math.Min (tb.Height, 22));
        }

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

            // Hiding a modal dialog ends it. Upstream's modal loop tests CheckCloseDialog, which exits
            // as soon as the form is not Visible and hands back Cancel. Here the loop only watched
            // dialog_task, so a dialog written as `this.Hide ()` -- older code, and wizards that hide
            // themselves before opening the next window -- left ShowDialog pumping forever with the
            // owner still disabled, which presents as a hung application.
            if (this is Form { dialog_task: not null } dialog) {
                if (dialog.DialogResult == DialogResult.None)
                    dialog.DialogResult = DialogResult.Cancel;

                dialog.CompleteClose ();
            }
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

        // Validate lives with the rest of the validation members below. It used to `return true` here
        // without raising anything, so a form that gated a Save button on Validate () always saved.

        /// <summary>Executes the specified delegate asynchronously on the window's UI thread.</summary>
        public void BeginInvoke (Action action)
        {
            Guard.ThrowIfNull (action);
            Majorsilence.Forms.Backends.Platform.Backend.Post (action);
        }

        /// <summary>Executes the specified delegate asynchronously on the window's UI thread with the given arguments. Mirrors WinForms Control.BeginInvoke(Delegate, Object[]).</summary>
        public void BeginInvoke (Delegate method, params object?[]? args)
        {
            Guard.ThrowIfNull (method);
            Majorsilence.Forms.Backends.Platform.Backend.Post (() => method.DynamicInvoke (args));
        }

        /// <summary>Executes the specified delegate synchronously on the window's UI thread.</summary>
        public void Invoke (Action action)
        {
            Guard.ThrowIfNull (action);
            Majorsilence.Forms.Backends.Platform.Backend.Invoke (action);
        }

        /// <summary>Executes the specified delegate synchronously on the window's UI thread with the given arguments and returns its result. Mirrors WinForms Control.Invoke(Delegate, Object[]).</summary>
        public object? Invoke (Delegate method, params object?[]? args)
        {
            Guard.ThrowIfNull (method);
            object? result = null;
            Majorsilence.Forms.Backends.Platform.Backend.Invoke (() => result = method.DynamicInvoke (args));
            return result;
        }

        /// <summary>
        /// Executes the specified delegate synchronously on the window's UI thread and returns its
        /// result, typed. Mirrors <see cref="Control.Invoke{T}(Func{T})"/> -- Form isn't a Control here,
        /// so it needs its own copy; ported code that overrides a Form/window-hierarchy method and calls
        /// <c>Invoke(() => someTypedExpression)</c> otherwise silently binds to the void-returning
        /// <see cref="Invoke(Action)"/> overload instead (the lambda converts to either), discarding the
        /// value rather than failing to compile.
        /// </summary>
        public T Invoke<T> (Func<T> func)
        {
            Guard.ThrowIfNull (func);
            T result = default!;
            Majorsilence.Forms.Backends.Platform.Backend.Invoke (() => result = func ());
            return result;
        }

        /// <summary>
        /// Gets an opaque nonzero token standing in for the native window handle. WinForms code
        /// reads Handle to force handle creation before Invoke; the compat window has no HWND.
        /// </summary>
        public IntPtr Handle => (IntPtr)(GetHashCode () | 1);

        /// <summary>Gets or sets the image drawn behind the window's controls.</summary>
        /// <remarks>
        /// Forwarded to the root adapter, which fills the window's client area and already knows how to
        /// paint a background image. Previously the layout below was stored and ignored, so a form with
        /// a background image simply showed none.
        /// </remarks>
        public virtual Majorsilence.Forms.Drawing.Image? BackgroundImage {
            get => adapter.BackgroundImage;
            set => adapter.BackgroundImage = value;
        }

        /// <summary>Gets or sets how <see cref="BackgroundImage"/> is laid out.</summary>
        public virtual ImageLayout BackgroundImageLayout {
            get => adapter.BackgroundImageLayout;
            set => adapter.BackgroundImageLayout = value;
        }

        /// <summary>Gets or sets user data associated with the window. Mirrors WinForms Control.Tag.</summary>
        public object? Tag { get; set; }

        /// <summary>Raised when the window surface is painted. Declared for WinForms source compat; the compat window paints through its root adapter and does not raise this yet.</summary>
#pragma warning disable CS0067
        public event PaintEventHandler? Paint;
#pragma warning restore CS0067

        /// <summary>
        /// Gets or sets the context menu shown when the window itself is right-clicked.
        /// </summary>
        /// <remarks>
        /// Forwarded to the root adapter, which is both the right object and what makes this WORK: the
        /// adapter is the window's client surface, so a right-click on the window's background lands on it,
        /// and <see cref="Control.RaiseClick"/> already opens a control's own context menu there. This
        /// used to be a stored value nothing read, so a form with a ContextMenuStrip assigned in the
        /// designer showed nothing when right-clicked while its child controls' menus worked -- which
        /// reads as the form's menu being broken rather than absent.
        /// </remarks>
        public ContextMenuStrip? ContextMenuStrip {
            get => ContentRoot.ContextMenuStrip;
            set => ContentRoot.ContextMenuStrip = value;
        }

        /// <summary>Gets or sets the legacy context menu shown when the window is right-clicked.</summary>
        /// <inheritdoc cref="ContextMenuStrip" path="/remarks"/>
        public virtual ContextMenu? ContextMenu {
            get => ContentRoot.ContextMenu;
            set => ContentRoot.ContextMenu = value;
        }

        /// <summary>Raised when <see cref="ContextMenu"/> changes.</summary>
        public event EventHandler? ContextMenuChanged {
            add => ContentRoot.ContextMenuChanged += value;
            remove => ContentRoot.ContextMenuChanged -= value;
        }

        /// <summary>Raised when <see cref="ContextMenuStrip"/> changes.</summary>
        public event EventHandler? ContextMenuStripChanged {
            add => ContentRoot.ContextMenuStripChanged += value;
            remove => ContentRoot.ContextMenuStripChanged -= value;
        }

        /// <summary>Gets or sets the input method editor mode for the window.</summary>
        /// <remarks>Forwarded to the root adapter so the window's children inherit it through the same
        /// chain they inherit a parent control's.</remarks>
        public ImeMode ImeMode {
            get => ContentRoot.ImeMode;
            set => ContentRoot.ImeMode = value;
        }

        /// <summary>Gets the default IME mode for this window type, used by <see cref="ResetImeMode"/>.</summary>
        protected virtual ImeMode DefaultImeMode => ImeMode.NoControl;

        /// <summary>Raised when <see cref="ImeMode"/> changes.</summary>
        public event EventHandler? ImeModeChanged {
            add => ContentRoot.ImeModeChanged += value;
            remove => ContentRoot.ImeModeChanged -= value;
        }

        /// <summary>Resets <see cref="ImeMode"/> to its default. Part of the designer Reset* pattern.</summary>
        public void ResetImeMode () => ImeMode = DefaultImeMode;

        // ── The rest of the designer Reset* pattern ──────────────────────────────
        // Every designer file emits these, and each one has to clear the SAME storage the corresponding
        // property writes, or "reset" leaves the explicit value in place and the property keeps reporting
        // it. That is why these are not one-liners forwarded blindly: BackColor and ForeColor live on the
        // window's own ControlStyle, Cursor in its own field, while Font and RightToLeft belong to the
        // root adapter.

        /// <summary>Clears any explicitly-set background colour so the window resolves it from the theme again.</summary>
        public virtual void ResetBackColor ()
        {
            if (Style.BackgroundColor is null)
                return;

            Style.BackgroundColor = null;
            Invalidate ();
        }

        /// <summary>Clears any explicitly-set foreground colour so the window resolves it from the theme again.</summary>
        public virtual void ResetForeColor ()
        {
            if (Style.ForegroundColor is null)
                return;

            Style.ForegroundColor = null;
            Invalidate ();
        }

        /// <summary>Clears any explicitly-set cursor, so the window shows the default arrow again.</summary>
        public virtual void ResetCursor () => Cursor = null;

        /// <summary>Clears any explicitly-set font, so the window and its children resolve it ambiently.</summary>
        public virtual void ResetFont () => adapter.ResetFont ();

        /// <summary>Resets the window's reading order so it follows the system default again.</summary>
        public virtual void ResetRightToLeft () => RightToLeft = RightToLeft.Inherit;

        /// <summary>Gets the default font a window and its children use. Matches <see cref="Control.DefaultFont"/>.</summary>
        public static Majorsilence.Forms.Drawing.Font DefaultFont => Control.DefaultFont;

        /// <summary>Gets the default foreground colour of a window. Matches <see cref="Control.DefaultForeColor"/>.</summary>
        public static System.Drawing.Color DefaultForeColor => Control.DefaultForeColor;

        /// <summary>Gets the company name from the application's assembly metadata.</summary>
        public string CompanyName => Application.CompanyName ?? string.Empty;

        /// <summary>Gets the product name from the application's assembly metadata.</summary>
        public string ProductName => Application.ProductName ?? string.Empty;

        /// <summary>Gets the product version from the application's assembly metadata.</summary>
        public string ProductVersion => Application.ProductVersion ?? string.Empty;

        // ── Validation and tab handling ──────────────────────────────────────────

        /// <summary>Raised while the window is validating, so a handler can cancel it.</summary>
        /// <remarks>Forwarded to the root adapter, as <see cref="Validated"/> is -- the pair has to come
        /// from the same object or a handler can see one without the other. It was previously a
        /// discarding stub on Form, so handlers attached to it were thrown away.</remarks>
        public event System.ComponentModel.CancelEventHandler? Validating {
            add => adapter.Validating += value;
            remove => adapter.Validating -= value;
        }

        /// <summary>Runs the window's validation cycle, returning false when a handler cancelled it.</summary>
        public bool Validate () => adapter.Validate ();

        /// <summary>Runs the window's validation cycle, returning false when a handler cancelled it.</summary>
        public bool Validate (bool checkAutoValidate) => adapter.Validate (checkAutoValidate);

        /// <summary>Moves focus to the next or previous control that can take it.</summary>
        /// <remarks>The hook a form overrides to take over Tab handling; it really moves focus rather
        /// than reporting false, so an override that calls base gets the standard behaviour.</remarks>
        protected virtual bool ProcessTabKey (bool forward)
            => adapter.SelectNextControl (adapter.SelectedControl, forward,
                tabStopOnly: true, nested: true, wrap: true);

        /// <summary>Suspends redrawing and layout while a batch of changes is applied.</summary>
        public void BeginUpdate () => SuspendLayout ();

        /// <summary>Resumes redrawing and layout after <see cref="BeginUpdate"/>, and repaints once.</summary>
        public void EndUpdate ()
        {
            ResumeLayout (false);
            Invalidate ();
        }

        /// <summary>Gets or sets the unscaled location of the window. Mirrors WinForms Form.Location.</summary>
        public System.Drawing.Point Location {
            get => Backend.Location;
            set {
                if (Backend.Location == value)
                    return;
                Backend.Location = value;

                // Through OnMove, which forwards to OnLocationChanged -- mirroring how the size setter
                // goes through OnResize. Raising OnLocationChanged directly would leave an override of
                // OnMove unreachable.
                OnMove (EventArgs.Empty);
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
        internal void OnBackendMoved () => OnMove (EventArgs.Empty);

        /// <summary>Raised when the window's client size changes. Mirrors WinForms Form.SizeChanged.
        /// Raised from the layout pipeline whenever the client area takes a new size.</summary>
        public event EventHandler? SizeChanged;

        /// <summary>Raises the SizeChanged event.</summary>
        protected virtual void OnSizeChanged (EventArgs e)
        {
            // A window that asked for ResizeRedraw wants its WHOLE surface repainted on every resize, not
            // just the strip the OS uncovered -- which is what a form drawing its own border and caption
            // needs, or the old border stays drawn along the edge that moved. Mirrors what Control.cs does
            // for the ControlStyles.ResizeRedraw flag.
            if (ResizeRedraw)
                Invalidate ();

            SizeChanged?.Invoke (this, e);

            // The window's client area always changes with its size here (there is no style change that
            // resizes the frame alone), so the two notifications are raised together -- code that docks a
            // companion window against a form's client edge (a ribbon's floating windows, say) subscribes
            // to this one.
            ClientSizeChanged?.Invoke (this, e);
        }

        /// <summary>Raised when the size of the window's client area changes. Mirrors Control.ClientSizeChanged.</summary>
        public event EventHandler? ClientSizeChanged;

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

        // Offers input to Application's message filters before it reaches a control, the way a Win32
        // message loop does. A filter returning true consumes the input.
        private bool Filtered (int msg, System.IntPtr wParam, System.IntPtr lParam)
        {
            var m = new Message { HWnd = Handle, Msg = msg, WParam = wParam, LParam = lParam };
            return Application.FilterMessage (ref m);
        }

        /// <summary>
        /// Where this window's CLIENT (0, 0) sits on the desktop, in screen pixels.
        /// </summary>
        /// <remarks>
        /// Not the window's <see cref="Location"/>: on a window with a native title bar the two differ by
        /// the height of that bar. Measured on a real window, the backend put client (0,0) at (1115, 62)
        /// while the window sat at (1115, 30).
        ///
        /// Everything that crosses between a control and the desktop goes through here, so the whole
        /// application agrees on one screen space. Using the window's own Location instead made
        /// Control.MousePosition a title bar's worth off from true screen coordinates -- self-consistent
        /// for the window the pointer was over, and wrong for every other window converting it. A drag
        /// overlay hit-testing its drop guides against the cursor is exactly that case: the guides tested
        /// ~32px above where they were drawn, so dropping on one never registered and a document could
        /// not be docked by hand.
        ///
        /// Backends without chrome (headless) answer with the window location, which is what this did
        /// before, so their coordinates are unchanged at any scale.
        /// </remarks>
        internal System.Drawing.Point ClientOriginOnScreen {
            get {
                try {
                    return Backend.PointToScreen (System.Drawing.Point.Empty);
                } catch (System.Exception) {
                    return Location;   // no platform window yet
                }
            }
        }

        // Keeps Cursor.Position/Control.MousePosition current.
        //
        // Converted through the ROOT ADAPTER, not through the window backend. The two disagree on where
        // the origin is: measured on a real window, the backend put client (0,0) at screen (1115, 62)
        // while the adapter put it at (1115, 30) -- the window's own Location. The 32px gap is the
        // title bar, because Avalonia's PointToScreen measures from the client area and
        // Control.PointToScreen measures from the window. Small, but a tab strip is ~21px tall, so
        // every hit test against Control.MousePosition landed clean outside it and matched nothing.
        //
        // Control.PointToScreen also consumes exactly the coordinates these handlers deliver, and is
        // the same path the controls reading Control.MousePosition use to convert it back with
        // PointToClient -- so the round trip is exact by construction rather than by coincidence.
        private void TrackCursorPosition (int logicalX, int logicalY)
        {
            try {
                Cursor.TrackPosition (adapter.PointToScreen (new System.Drawing.Point (logicalX, logicalY)));
            } catch (System.Exception) {
                // A backend that cannot map coordinates yet (no platform window) must not break input
                // routing; the stale value is better than a thrown pointer event.
            }
        }

        // The backends deliver device pixels. Everything public -- Bounds, MouseEventArgs, the lParam a
        // message filter reads, PointToClient/PointToScreen -- is in logical units, so the conversion
        // belongs here, once, at the boundary. Identity at scaling 1, which is why input that was routed
        // in device space worked until a scaled display was simulated: a control then hit-tested device
        // coordinates against its logical Bounds and matched the wrong child, or none.
        private int DeviceToLogical (int value)
        {
            var scaling = Scaling;
            return scaling is <= 0 or 1 ? value : (int)System.Math.Round (value / scaling);
        }

        /// <summary>
        /// Whether the capture holder sits in a form that is <em>hosted inside this window</em> — its own
        /// WindowBase, so this window's dispatch will not reach it, yet visually part of this window and
        /// so entitled to this window's pointer input.
        /// </summary>
        /// <remarks>
        /// Deliberately narrower than "any other window". Capture in WinForms is per-thread and does hold
        /// across unrelated top-level windows, but honouring that here would let one window's unreleased
        /// capture swallow input to every other window in the process — a much worse failure than the one
        /// being fixed, and one nothing in a hosted-form drag needs.
        /// </remarks>
        private bool HostsInAnotherWindow (Control holder)
        {
            var root = holder.RootControl;

            // Walk out of each hosted form into the control that hosts it, up to this window's own tree.
            for (var hops = 0; hops < 32; hops++) {
                if (ReferenceEquals (root, adapter))
                    return hops > 0;   // hops == 0 means it is already ours; normal dispatch handles it.

                if (root is not ControlAdapter { ParentForm: Form hosted } || hosted.HostingControl is not { } frame)
                    return false;

                root = frame.RootControl;
            }

            return false;
        }

        /// <summary>
        /// Hands a mouse event to the control holding the capture when that control lives in a
        /// <em>different</em> window, and reports whether it did.
        /// </summary>
        /// <remarks>
        /// Capture belongs to the pointer, not to a window: while a control holds it, every mouse event
        /// is that control's, even when the pointer is somewhere else entirely. This window's own
        /// dispatch already routes to a holder inside its own tree, so only the cross-window case is
        /// handled here.
        ///
        /// That case is how WinForms code takes over a drag one of its children began — the child
        /// captures on mouse-down, then the library hands capture to a form. In a docking library that
        /// form is the *document* being dragged, which is a separate window hosted inside the main one.
        /// Left unrouted, the main window went on hit-testing as usual and delivered every move straight
        /// back to the tab strip, which restarted the drag on each one: ~30 full-screen drag-outline
        /// windows stacked up over the screen, and the app looked hung.
        /// </remarks>
        private bool RoutedToCaptureHolder (MouseButtons button, int clicks, Keys keys,
                                            System.Action<Control, MouseEventArgs> raise)
        {
            var holder = Control.CaptureHolder;

            if (holder is null || !HostsInAnotherWindow (holder))
                return false;

            System.Drawing.Point local;
            try {
                // Cursor.Position was just updated from this event, and is in screen units.
                local = holder.PointToClient (Cursor.Position);
            } catch (System.Exception) {
                return false;   // can't map (no platform window yet) -- fall back to normal dispatch.
            }

            raise (holder, new MouseEventArgs (button, clicks, local.X, local.Y,
                                               System.Drawing.Point.Empty, keyData: keys));
            return true;
        }

        internal void HandlePointerPressed (MouseButtons button, int x, int y, Keys keys)
        {
            int lx = DeviceToLogical (x), ly = DeviceToLogical (y);

            TrackCursorPosition (lx, ly);

            if (Filtered (WindowMessages.ButtonDownMessage (button), System.IntPtr.Zero, WindowMessages.MakeMouseLParam (lx, ly)))
                return;

            if (RoutedToCaptureHolder (button, 1, keys, static (c, e) => c.RaiseMouseDown (e)))
                return;

            // A press can be the first pointer event a window sees (click-through onto an inactive
            // window), so it counts as an entry too.
            TrackPointerInside ();

            if (Resizeable && HandleMouseDown (x, y))
                return;

            var ev = new MouseEventArgs (button, 1, lx, ly, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseDown (ev);
        }

        internal void HandlePointerReleased (MouseButtons button, int x, int y, Keys keys)
        {
            int lx = DeviceToLogical (x), ly = DeviceToLogical (y);

            TrackCursorPosition (lx, ly);

            if (Filtered (WindowMessages.ButtonUpMessage (button), System.IntPtr.Zero, WindowMessages.MakeMouseLParam (lx, ly)))
                return;

            // Mouse-up before Click, matching WinForms (WmMouseUp releases the capture and raises
            // MouseUp, then raises Click). Raising Click first is a trap: a Click handler that opens a
            // modal dialog blocks in the nested loop, so the MouseUp that would have dropped this
            // control's mouse capture never runs -- and every pointer release inside the modal is
            // then routed straight back to the still-captured control, re-firing its Click. Found
            // running ReportDesigner: clicking the report-warnings button opened its dialog, and
            // clicking OK on that dialog opened another copy instead of closing it.
            if (RoutedToCaptureHolder (button, 1, keys, static (c, e) => { c.RaiseMouseUp (e); c.RaiseClick (e); }))
                return;

            var ev = BuildMouseClickArgs (button, new System.Drawing.Point (lx, ly), keys);

            if (ev.Clicks > 1)
                adapter.RaiseDoubleClick (ev);

            adapter.RaiseMouseUp (ev);
            adapter.RaiseClick (ev);
        }

        internal void HandlePointerMoved (MouseButtons buttons, int x, int y, Keys keys)
        {
            int lx = DeviceToLogical (x), ly = DeviceToLogical (y);

            TrackCursorPosition (lx, ly);

            if (Filtered (WindowMessages.WM_MOUSEMOVE, System.IntPtr.Zero, WindowMessages.MakeMouseLParam (lx, ly)))
                return;

            if (RoutedToCaptureHolder (buttons, 0, keys, static (c, e) => c.RaiseMouseMove (e)))
                return;

            // Raise MouseEnter before the resize-border shortcut below returns: the window chrome is
            // part of the window, so entering over a border edge is still an entry.
            TrackPointerInside ();

            if (Resizeable && HandleMouseMove (x, y))
                return;

            var ev = new MouseEventArgs (buttons, 0, lx, ly, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseMove (ev);
        }

        internal void HandlePointerWheel (MouseButtons buttons, int x, int y, System.Drawing.Point delta, Keys keys)
        {
            // Convert device pixels to logical units here, once, like the other pointer handlers --
            // otherwise on a scaled display the wheel event hit-tests device coordinates against
            // logical Bounds and reaches the wrong child, or none.
            int lx = DeviceToLogical (x), ly = DeviceToLogical (y);
            TrackPointerInside ();

            var ev = new MouseEventArgs (buttons, 0, lx, ly, delta, keyData: keys);
            adapter.RaiseMouseWheel (ev);

            // WinForms delivers the wheel to the window itself as well, which is how a form scrolls or
            // zooms a view it owns without every child having to forward. Declared here because Form
            // does not derive from Control and so inherits nothing from it.
            OnMouseWheel (ev);
        }

        /// <summary>Raised when the window's Text changes. Mirrors Control.TextChanged.</summary>
        public event EventHandler? TextChanged;

        /// <summary>Raises the <see cref="TextChanged"/> event.</summary>
        protected virtual void OnTextChanged (EventArgs e) => TextChanged?.Invoke (this, e);

        /// <summary>Raised when the window lays out its children. Mirrors Control.Layout.</summary>
        public event LayoutEventHandler? Layout {
            add => adapter.Layout += value;
            remove => adapter.Layout -= value;
        }

        // ── Control events a WinForms Form inherits ───────────────────────────────
        // Form is not a Control here, so none of these come for free, and `form.MouseClick += ...` on
        // migrated code simply did not compile. Each forwards to the root ControlAdapter, which IS the
        // window's client surface -- the same shape as DoubleClick and Layout above. Members that have no
        // meaning for a top-level window (Dock, Anchor, TabIndex and friends) are deliberately still
        // absent; see ControlWindowParityBaseline.txt, which records that split.
        //
        // MouseClick itself is declared on Form instead (with an OnMouseClick hook), not here -- see
        // the comment above Form.MouseClick.

        /// <summary>Raised when the window's client area is double-clicked. Mirrors <c>Control.MouseDoubleClick</c>; forwards to the root control adapter.</summary>
        public event MouseEventHandler? MouseDoubleClick {
            add => adapter.MouseDoubleClick += value;
            remove => adapter.MouseDoubleClick -= value;
        }

        /// <summary>Raised when the mouse rests over the window's client area. Mirrors <c>Control.MouseHover</c>; forwards to the root control adapter.</summary>
        public event EventHandler? MouseHover {
            add => adapter.MouseHover += value;
            remove => adapter.MouseHover -= value;
        }

        /// <summary>Raised when the window's client area gains or loses mouse capture. Mirrors <c>Control.MouseCaptureChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? MouseCaptureChanged {
            add => adapter.MouseCaptureChanged += value;
            remove => adapter.MouseCaptureChanged -= value;
        }

        /// <summary>Raised when the background colour changes. Mirrors <c>Control.BackColorChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? BackColorChanged {
            add => ContentRoot.BackColorChanged += value;
            remove => ContentRoot.BackColorChanged -= value;
        }

        /// <summary>Raised when the foreground colour changes. Mirrors <c>Control.ForeColorChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? ForeColorChanged {
            add => ContentRoot.ForeColorChanged += value;
            remove => ContentRoot.ForeColorChanged -= value;
        }

        /// <summary>Raised when the cursor changes. Mirrors <c>Control.CursorChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? CursorChanged {
            add => adapter.CursorChanged += value;
            remove => adapter.CursorChanged -= value;
        }

        /// <summary>Raised when the padding changes. Mirrors <c>Control.PaddingChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? PaddingChanged {
            add => ContentRoot.PaddingChanged += value;
            remove => ContentRoot.PaddingChanged -= value;
        }

        /// <summary>Raised when the RightToLeft value changes. Mirrors <c>Control.RightToLeftChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? RightToLeftChanged {
            add => adapter.RightToLeftChanged += value;
            remove => adapter.RightToLeftChanged -= value;
        }

        /// <summary>Raised when the system colours change. Mirrors <c>Control.SystemColorsChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? SystemColorsChanged {
            add => adapter.SystemColorsChanged += value;
            remove => adapter.SystemColorsChanged -= value;
        }

        /// <summary>Raised when the binding context changes. Mirrors <c>Control.BindingContextChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? BindingContextChanged {
            add => adapter.BindingContextChanged += value;
            remove => adapter.BindingContextChanged -= value;
        }

        /// <summary>Raised when the CausesValidation value changes. Mirrors <c>Control.CausesValidationChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? CausesValidationChanged {
            add => adapter.CausesValidationChanged += value;
            remove => adapter.CausesValidationChanged -= value;
        }

        /// <summary>Raised when the background image changes. Mirrors <c>Control.BackgroundImageChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? BackgroundImageChanged {
            add => adapter.BackgroundImageChanged += value;
            remove => adapter.BackgroundImageChanged -= value;
        }

        /// <summary>Raised when the background image layout changes. Mirrors <c>Control.BackgroundImageLayoutChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? BackgroundImageLayoutChanged {
            add => adapter.BackgroundImageLayoutChanged += value;
            remove => adapter.BackgroundImageLayoutChanged -= value;
        }

        /// <summary>Raised when the region changes. Mirrors <c>Control.RegionChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? RegionChanged {
            add => adapter.RegionChanged += value;
            remove => adapter.RegionChanged -= value;
        }

        /// <summary>Raised when the control style changes. Mirrors <c>Control.StyleChanged</c>; forwards to the root control adapter.</summary>
        public event EventHandler? StyleChanged {
            add => adapter.StyleChanged += value;
            remove => adapter.StyleChanged -= value;
        }

        /// <summary>Raised when a drag operation leaves the window. Mirrors <c>Control.DragLeave</c>; forwards to the root control adapter.</summary>
        public event EventHandler? DragLeave {
            add => adapter.DragLeave += value;
            remove => adapter.DragLeave -= value;
        }

        /// <summary>Raised when the window's client area is entered. Mirrors <c>Control.Enter</c>; forwards to the root control adapter.</summary>
        public event EventHandler? Enter {
            add => adapter.Enter += value;
            remove => adapter.Enter -= value;
        }

        /// <summary>Raised when validation of the client area finishes. Mirrors <c>Control.Validated</c>; forwards to the root control adapter.</summary>
        public event EventHandler? Validated {
            add => adapter.Validated += value;
            remove => adapter.Validated -= value;
        }

        /// <summary>Raised when part of the window is invalidated. Mirrors <c>Control.Invalidated</c>; forwards to the root control adapter.</summary>
        public event InvalidateEventHandler? Invalidated {
            add => adapter.Invalidated += value;
            remove => adapter.Invalidated -= value;
        }

        /// <summary>Raised when a drag operation is asked whether to continue. Mirrors <c>Control.QueryContinueDrag</c>; forwards to the root control adapter.</summary>
        public event QueryContinueDragEventHandler? QueryContinueDrag {
            add => adapter.QueryContinueDrag += value;
            remove => adapter.QueryContinueDrag -= value;
        }

        /// <summary>Raised when the focus or keyboard UI cues change. Mirrors <c>Control.ChangeUICues</c>; forwards to the root control adapter.</summary>
        public event UICuesEventHandler? ChangeUICues {
            add => adapter.ChangeUICues += value;
            remove => adapter.ChangeUICues -= value;
        }

        /// <summary>Raised when a control is added to the window. Mirrors <c>Control.ControlAdded</c>; forwards to the root control adapter.</summary>
        public event ControlEventHandler? ControlAdded {
            add => ContentRoot.ControlAdded += value;
            remove => ContentRoot.ControlAdded -= value;
        }

        /// <summary>Raised when a control is removed from the window. Mirrors <c>Control.ControlRemoved</c>; forwards to the root control adapter.</summary>
        public event ControlEventHandler? ControlRemoved {
            add => ContentRoot.ControlRemoved += value;
            remove => ContentRoot.ControlRemoved -= value;
        }

        /// <summary>Raised when a key is previewed before being processed. Mirrors <c>Control.PreviewKeyDown</c>; forwards to the root control adapter.</summary>
        public event PreviewKeyDownEventHandler? PreviewKeyDown {
            add => adapter.PreviewKeyDown += value;
            remove => adapter.PreviewKeyDown -= value;
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

        // Backends deliver device pixels here, exactly like HandlePointerPressed and friends; the
        // routing below hit-tests against logical Bounds and the Translate* helpers subtract logical
        // offsets, so the point (and, for a scroll/swipe, the movement and velocity) is converted at
        // this boundary once. Identity at scaling 1, which is why routing device coordinates against
        // logical Bounds went unnoticed until Android (RenderScaling ~2.6) exercised these paths.

        internal void HandleLongPress (int x, int y)
            => adapter.RaiseLongPress (new LongPressEventArgs (DeviceToLogical (x), DeviceToLogical (y)));

        internal void HandlePinch (int x, int y, double scale, double angle, double angleDelta)
            => adapter.RaisePinch (new PinchGestureEventArgs (DeviceToLogical (x), DeviceToLogical (y), scale, angle, angleDelta));

        internal void HandleSwipe (int x, int y, double velocityX, double velocityY, SwipeDirection direction)
            => adapter.RaiseSwipe (new SwipeGestureEventArgs (
                DeviceToLogical (x), DeviceToLogical (y),
                velocityX / DeviceScaleOrOne, velocityY / DeviceScaleOrOne, direction));

        internal void HandleScrollGesture (int x, int y, int deltaX, int deltaY)
            => adapter.RaiseScrollGesture (new ScrollGestureEventArgs (
                DeviceToLogical (x), DeviceToLogical (y),
                new System.Drawing.Point (DeviceToLogical (deltaX), DeviceToLogical (deltaY))));

        private double DeviceScaleOrOne => Scaling is <= 0 or 1 ? 1 : Scaling;

        // WinForms parity: without KeyPreview a form's own key events fire only when no child
        // control has focus; keys otherwise go straight to the focused control. With KeyPreview
        // the form sees (and may handle) the key before the focused control does.
        private bool FormSeesKeyFirst => this is not Form form || form.KeyPreview || adapter.SelectedControl is null;

        /// <summary>
        /// Runs the keyboard pre-processing chain for a key-down, starting at the focused control and
        /// bubbling outward to this window.
        /// </summary>
        /// <returns>True when the key was consumed and no key event should follow.</returns>
        /// <remarks>
        /// The chain begins at the deepest focused control rather than at the window, because
        /// <c>IsInputKey</c> is that control's decision to make: a multiline text box claims Enter, a
        /// grid claims the arrows, and only a key nobody claims becomes a dialog key. Starting at the
        /// window instead is what made <see cref="Form.AcceptButton"/> swallow Enter everywhere.
        /// </remarks>
        private bool PreProcessKey (Keys keys)
        {
            // The adapter is the root control, and it forwards into this window at the end of its own
            // chain -- so starting there covers the no-focus case as well.
            var start = adapter.SelectedControl ?? adapter;
            return start.PreProcessKeyMessage (keys);
        }

        /// <summary>Routes a key-down. Returns true if handled (the backend should suppress further native processing).</summary>
        internal bool HandleKeyDown (Keys keys)
        {
            // wParam is the virtual-key code on Windows, which is what Keys already encodes.
            if (Filtered (WindowMessages.WM_KEYDOWN, (System.IntPtr)(int)(keys & Keys.KeyCode), System.IntPtr.Zero))
                return true;

            var kd_e = new KeyEventArgs (keys);

            // The WinForms pre-processing chain, in WinForms' order: ProcessCmdKey (shortcuts win over
            // everything) -> IsInputKey (the focused control claiming the key for itself) ->
            // ProcessDialogKey (Tab/Enter/Escape/arrows). It starts at the focused control and bubbles
            // outward to this window, so AcceptButton and CancelButton are reached last -- which is the
            // whole point. Running them first, as this method used to, meant Enter in a multiline text
            // box submitted the dialog and Tab could never reach a control that wanted it.
            if (PreProcessKey (keys))
                return true;

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
            if (Filtered (WindowMessages.WM_KEYUP, (System.IntPtr)(int)(keys & Keys.KeyCode), System.IntPtr.Zero))
                return true;

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

            // WM_CHAR carries one character; report the first, matching how a filter would see a run
            // of native WM_CHARs begin.
            if (Filtered (WindowMessages.WM_CHAR, (System.IntPtr)text[0], System.IntPtr.Zero))
                return true;

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

        /// <summary>
        /// Raised when the user starts dragging the window by its caption, before the window begins to
        /// move. Setting <see cref="CaptionDragStartingEventArgs.Cancel"/> stops the move, leaving the
        /// gesture to the application.
        /// </summary>
        /// <remarks>
        /// The portable stand-in for intercepting <c>WM_NCLBUTTONDOWN</c> over <c>HTCAPTION</c>, which is
        /// how WinForms code takes over a title-bar drag — a docking library does it so dragging a
        /// floating window re-docks it instead of moving it around the desktop. There are no non-client
        /// messages here, so without this the gesture was unreachable.
        ///
        /// Only raised for a caption this library draws. A window using the operating system's title bar
        /// (<see cref="Form.UseSystemDecorations"/>, the default on macOS) never sees the press at all —
        /// the OS moves the window itself — so a window that wants this must own its caption.
        /// </remarks>
        public event EventHandler<CaptionDragStartingEventArgs>? CaptionDragStarting;

        /// <summary>Raises <see cref="CaptionDragStarting"/>.</summary>
        protected virtual void OnCaptionDragStarting (CaptionDragStartingEventArgs e)
            => CaptionDragStarting?.Invoke (this, e);

        // Returns true when a handler claimed the gesture, in which case the window must not move.
        internal bool RaiseCaptionDragStarting (System.Drawing.Point location)
        {
            var e = new CaptionDragStartingEventArgs (location);
            OnCaptionDragStarting (e);
            return e.Cancel;
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

        /// <summary>
        /// Notifies the window of Windows messages. Declared so a form that overrides it compiles; never
        /// called, because there is no Win32 message pump here. Present on both Control and WindowBase
        /// because a Form does not inherit from Control -- and a form is the usual thing to override it on.
        /// </summary>
        protected virtual void OnNotifyMessage (Message m) { }

        /// <summary>Gets the pointer position in screen coordinates. Mirrors <see cref="Control.MousePosition"/>.</summary>
        public static System.Drawing.Point MousePosition => Control.MousePosition;

        /// <summary>Gets whether the window can receive focus. Mirrors <see cref="Control.CanFocus"/>.</summary>
        public bool CanFocus => Visible && Enabled;

        /// <summary>Reapplies the window's styles. Mirrors <see cref="Control.UpdateStyles"/>; a no-op here,
        /// as there is no window-style bitmask to push to a handle.</summary>
        public void UpdateStyles () { }

        /// <summary>Starts a drag-and-drop operation. Mirrors <see cref="Control.DoDragDrop(object, DragDropEffects)"/>; forwards to
        /// the root adapter, and so returns None until the backend grows a drag source.</summary>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects)
            => adapter.DoDragDrop (data, allowedEffects);

        /// <summary>Raised while a drag is over this window, to set the cursor. Forwards to the root adapter.</summary>
        public event GiveFeedbackEventHandler? GiveFeedback {
            add => adapter.GiveFeedback += value;
            remove => adapter.GiveFeedback -= value;
        }

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
        /// <remarks>
        /// The display's factor times <see cref="Application.UiScale"/>. This is the single point the
        /// whole UI scales from -- <c>Control.DeviceDpi</c> is derived from it and every
        /// <c>LogicalToDeviceUnits</c> conversion follows.
        /// </remarks>
        public double Scaling => Backend.Scaling * Application.UiScale;

        /// <summary>Gets the current scale factor of the desktop.</summary>
        /// <remarks>
        /// The real display factor, deliberately WITHOUT <see cref="Application.UiScale"/>: the desktop
        /// does not zoom just because this app does. <c>Control.PointToScreen</c> converts control
        /// coordinates to desktop ones through <c>DesktopScaling / Scaling</c>, so keeping this
        /// unzoomed makes that ratio undo the zoom exactly.
        /// </remarks>
        public double DesktopScaling => Backend.Scaling;

        internal void SetCursor (Cursor cursor) => current_cursor = cursor;

        internal virtual void SetWindowStartupLocation (WindowBase? owner = null) { }

        /// <summary>
        /// The window that actually presents this one on screen. Normally itself; a <see cref="Form"/>
        /// hosted in someone else's control tree (an MDI child, or one placed via Controls.Add) returns
        /// the top-level window it is hosted in.
        /// </summary>
        /// <remarks>
        /// A hosted form's own <see cref="Backend"/> is constructed but never shown -- its content is
        /// rendered into the host's frame instead. Operating on that unused backend is not the no-op it
        /// looks like: <c>Backend.Activate ()</c> maps to the platform's "make key and order front",
        /// which puts an empty window on screen. Anything reaching for a window to enable, activate or
        /// measure must go through here rather than <c>Backend</c> directly.
        /// </remarks>
        internal virtual WindowBase PresentationWindow => this;

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

        /// <summary>
        /// Shows the window modally with no owner to disable — the first dialog of an application,
        /// before any other window exists.
        /// </summary>
        /// <remarks>
        /// There is no owner to disable, but the window must still be shown and the caller must still
        /// block. This path used to fall back to a plain <c>Show ()</c> returning
        /// <see cref="DialogResult.OK"/>, which broke the near-universal
        /// <c>if (new LoginForm ().ShowDialog () != DialogResult.OK) return;</c> shape before
        /// <c>Application.Run</c>: it saw OK instantly with nothing filled in and carried on into the
        /// main form, leaving the login window floating.
        /// </remarks>
        internal void ShowDialogOwnerless ()
        {
            SetWindowStartupLocation (null);
            Backend.Show ();
            EnsureShownBookkeeping ();
        }

        internal void ShowDialog (WindowBase parent)
        {
            // Disable and measure against the window presenting the parent: when the parent is an MDI
            // child it has no window of its own, and its unrealized backend reports a meaningless
            // position (so CenterParent placed the dialog somewhere arbitrary) while leaving the real
            // window it lives in still accepting input.
            var parentWindow = parent.PresentationWindow;

            SetWindowStartupLocation (parentWindow);
            DisableWindowsForModalLoop ();
            Backend.ShowActivated = ShowsActivated;

            // Owned show (NOT a nested native modal loop -- that is RunModalLoop's job). The backend
            // establishes the native owner link so the window manager keeps this dialog above its
            // parent and raises it together with the parent; a plain Show() left it a detached
            // top-level that alt-tab could bury behind the main window with no way back to it.
            Backend.ShowDialog (parentWindow.Backend);
            EnsureShownBookkeeping ();
        }

        // The windows this dialog disabled, so CompleteClose can restore exactly those and leave alone
        // any that were already disabled for another reason.
        private List<WindowBase>? _disabledForModal;

        /// <summary>
        /// Disables every other top-level window for the duration of a modal dialog.
        /// </summary>
        /// <remarks>
        /// Only the dialog's own owner used to be disabled, so with two forms open a dialog raised from
        /// A left B fully interactive — B could open a second dialog, close A out from under the modal
        /// loop, or call <c>Application.Exit</c> mid-modal. Typical in an MDI shell with floating tool
        /// windows. Upstream disables the whole thread's window set
        /// (<c>Application.ThreadContext.DisableWindowsForModalLoop</c>).
        /// </remarks>
        private void DisableWindowsForModalLoop ()
        {
            _disabledForModal = [];

            foreach (var form in Application.OpenForms.Cast<Form> ().ToArray ()) {
                var window = form.PresentationWindow;

                // Not this dialog, and not a window already disabled by an outer modal loop -- nested
                // dialogs must not re-enable each other's windows on the way out.
                if (ReferenceEquals (window, this) || !window.Backend.Enabled)
                    continue;

                window.Backend.Enabled = false;
                _disabledForModal.Add (window);
            }
        }

        /// <summary>Re-enables the windows this dialog disabled.</summary>
        internal void RestoreWindowsAfterModalLoop ()
        {
            if (_disabledForModal is null)
                return;

            foreach (var window in _disabledForModal)
                window.Backend.Enabled = true;

            _disabledForModal = null;
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

            // A real client rectangle before Load, as in WinForms -- where the handle (and with it the
            // client size) exists by the time Load is raised, so a handler that reads a docked child's
            // Width gets its settled value. Nothing has painted yet at this point.
            SyncAdapterBounds ();

            // The handle exists BEFORE Load, as it does upstream: OnCreateControl runs after
            // CreateHandle and is what raises OnLoad (Form.cs). This used to run after EnsureLoaded,
            // so IsHandleCreated was false throughout Load -- and the common guard
            // `if (!IsHandleCreated) return;` in a refresh routine shared with a timer silently skipped
            // the whole handler. It also meant an OnHandleCreated override and a HandleCreated
            // subscriber fired at two different moments.
            //
            // MarkHandleCreated is what sets `shown`, so the first-show test has to be taken before it
            // rather than read off the flag afterwards.
            var firstShow = !shown;

            if (firstShow) {
                MarkHandleCreated ();

                // The HandleCreated EVENT is a different object from the OnHandleCreated method: the
                // event is forwarded to the root adapter and raised by Control.CreateControl, while the
                // method is raised by MarkHandleCreated above. Creating the adapter here keeps the two
                // together -- an override and a subscription used to fire at two different moments,
                // with the event landing after Load.
                adapter.CreateControl ();
            }

            EnsureLoaded ();            // WinForms raises Load around the window's first display.

            // Assume active the moment we ask the backend to show one of our own windows, rather than
            // waiting for its real Activated event (which, empirically, can arrive either before or
            // after this call returns depending on the platform) -- see IsActive's doc comment. The
            // real event still fires and reconfirms this when it eventually arrives. A window shown
            // without activation is the exception: it never becomes active, so assuming it did would
            // make the window it appeared over look deactivated to the app.
            if (activated)
                IsActive = true;

            if (firstShow) {
                OnShown (EventArgs.Empty);

                // The pass above could not reach this window's own OnLayout: the adapter forwards its
                // layout only once `shown` is set (see ControlAdapter.OnLayout, which explains why).
                // Now that it is, run the pass the first painted frame used to be responsible for.
                adapter.PerformLayout ();
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
                SetVisibleCore (value);
            }
        }

        /// <summary>Shows or hides the window.</summary>
        /// <remarks>
        /// The choke point every visibility change routes through, as in WinForms -- which is the entire
        /// value of the member: a popup that suppresses being shown until it has content overrides this,
        /// and an override only intercepts anything if <see cref="Visible"/> actually goes through it.
        /// </remarks>
        protected virtual void SetVisibleCore (bool value)
        {
            if (value)
                Show ();
            else
                Hide ();
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
        public void ResumeLayout (bool performLayout = true)
        {
            if (performLayout)
                SyncAdapterBounds ();

            adapter.ResumeLayout (performLayout);

            if (performLayout)
                RaiseLayoutForExplicitRequest ();
        }

        /// <summary>Forces the window's controls to apply layout logic.</summary>
        public void PerformLayout ()
        {
            // Before the pass, not after: laying out against a stale adapter is what made an explicit
            // PerformLayout on a not-yet-painted window produce nothing usable.
            SyncAdapterBounds ();
            adapter.PerformLayout ();

            if (!ReferenceEquals (ContentRoot, adapter))
                ContentRoot.PerformLayout ();

            RaiseLayoutForExplicitRequest ();
        }

        /// <summary>Forces the window's controls to apply layout logic, naming what changed.</summary>
        /// <remarks>The overload designer-generated and container code actually calls.</remarks>
        public void PerformLayout (Control? affectedControl, string? affectedProperty)
        {
            SyncAdapterBounds ();
            adapter.PerformLayout (affectedControl, affectedProperty);

            if (!ReferenceEquals (ContentRoot, adapter))
                ContentRoot.PerformLayout (affectedControl, affectedProperty);
            RaiseLayoutForExplicitRequest ();
        }

        // ── Control state and geometry a WinForms Form inherits ──────────────────
        // Form is not a Control here, so none of this comes for free. Each forwards to the root
        // ControlAdapter, which IS the window's client surface, so the answers are about the same
        // rectangle a WinForms Form would answer about. See ControlWindowParityBaseline.txt for the
        // members deliberately still absent and why.

        /// <summary>Gets whether the window has been created. Mirrors <c>Control.Created</c>.</summary>
        /// <remarks>Answers from the same flag as <see cref="IsHandleCreated"/> -- there is no handle
        /// here, and "has been shown" is the closest true statement.</remarks>
        public bool Created => shown;

        /// <summary>Forces the creation of the window's client surface. Mirrors <c>Control.CreateControl</c>.</summary>
        public void CreateControl ()
        {
            adapter.CreateControl ();
            OnCreateControl ();
        }

        /// <summary>Called when the window's client surface is created.</summary>
        protected virtual void OnCreateControl () { }

        /// <summary>Gets whether the given control is a child or deeper descendant of this window.</summary>
        public bool Contains (Control control) => ContentRoot.Contains (control);

        /// <summary>Gets whether the window has any child controls.</summary>
        public bool HasChildren => ContentRoot.HasChildren;

        /// <summary>Gets the size the window's contents would like to be.</summary>
        public System.Drawing.Size PreferredSize => adapter.PreferredSize;

        /// <summary>Gets the size the window's contents would like to be within the given bounds.</summary>
        public virtual System.Drawing.Size GetPreferredSize (System.Drawing.Size proposedSize)
            => adapter.GetPreferredSize (proposedSize);

        /// <summary>Gets the innermost container control of the window's contents.</summary>
        public IContainerControl? GetContainerControl () => adapter.GetContainerControl ();

        /// <summary>Gets the value of the specified <see cref="ControlStyles"/> flag.</summary>
        /// <remarks>The counterpart of <see cref="SetStyle"/>, which already forwarded here.</remarks>
        public bool GetStyle (ControlStyles flag) => adapter.GetStyle (flag);

        /// <summary>Gets or sets whether the wait cursor is shown for the window and its contents.</summary>
        public bool UseWaitCursor {
            get => adapter.UseWaitCursor;
            set => adapter.UseWaitCursor = value;
        }

        /// <summary>Converts a logical DPI value to the window's device DPI.</summary>
        public int LogicalToDeviceUnits (int value) => adapter.LogicalToDeviceUnits (value);

        /// <summary>Converts a device DPI value to logical units.</summary>
        public int DeviceToLogicalUnits (int value) => adapter.DeviceToLogicalUnits (value);

        /// <summary>Scales a bitmap to the window's device DPI.</summary>
        public void ScaleBitmapLogicalToDevice (ref Majorsilence.Forms.Drawing.Bitmap logicalBitmap)
            => adapter.ScaleBitmapLogicalToDevice (ref logicalBitmap);

        /// <summary>Invalidates a region of the window, optionally including its children.</summary>
        public void Invalidate (System.Drawing.Rectangle rectangle, bool invalidateChildren) => Invalidate ();

        /// <summary>Invalidates a region of the window, optionally including its children.</summary>
        public void Invalidate (Majorsilence.Forms.Drawing.Region region, bool invalidateChildren) => Invalidate ();

        /// <summary>Gets the container this window is a component of. Always null, as on Control.</summary>
        public new System.ComponentModel.IContainer? Container => null;

        /// <summary>Gets whether the window is in design mode. Always false, as on Control.</summary>
        public new bool DesignMode => false;

        // ── Data binding ─────────────────────────────────────────────────────────
        // NOTE ON WHAT THIS ACTUALLY DOES: binding is a COMPILE-compatibility surface in this library,
        // not a working facility -- `Binding.WriteValue` is an empty stub, so no binding moves a value in
        // either direction yet. These members exist so migrated code that sets up bindings on a Form
        // compiles and runs; they are wired to the correct objects so that implementing Binding later
        // makes them work rather than making them wrong.

        /// <summary>Gets the data bindings for the window's own properties.</summary>
        /// <remarks>
        /// Bound to the WINDOW, not to its root adapter, because that is what the collection is for:
        /// `form.DataBindings.Add ("Text", source, "Title")` is a statement about the window's title.
        /// Handing back the adapter's collection would compile and quietly bind the adapter's Text --
        /// a different property that nothing displays.
        /// </remarks>
        public ControlBindingsCollection DataBindings => data_bindings ??= new ControlBindingsCollection (this);

        private ControlBindingsCollection? data_bindings;

        /// <summary>Re-reads every bound property of the window from its data source.</summary>
        public void ResetBindings ()
        {
            foreach (var binding in DataBindings)
                binding.WriteValue ();
        }

        /// <summary>Gets or sets an arbitrary object shared with the window's children for binding.</summary>
        /// <remarks>
        /// Forwards to the root adapter, and here that IS the right object: the value's purpose is to be
        /// inherited by descendants, and a child with none of its own reads its parent's -- a chain that
        /// terminates at the adapter. Setting it on the adapter is therefore what makes every control on
        /// the window see it.
        /// </remarks>
        public virtual object? DataContext {
            get => adapter.DataContext;
            set => adapter.DataContext = value;
        }

        /// <summary>Raised when <see cref="DataContext"/> changes.</summary>
        public event EventHandler? DataContextChanged {
            add => adapter.DataContextChanged += value;
            remove => adapter.DataContextChanged -= value;
        }

        // The window's binding context. Form declares a public, non-nullable BindingContext of its own
        // (as WinForms does on Form), so the interface member routes through this hook instead of a second
        // property that would shadow it and then disagree with it.
        internal virtual BindingContext? BindingContextCore {
            get => binding_context ??= new BindingContext ();
            set => binding_context = value;
        }

        private BindingContext? binding_context;

        BindingContext? IBindableComponent.BindingContext {
            get => BindingContextCore;
            set => BindingContextCore = value;
        }

        // ── Accessibility ────────────────────────────────────────────────────────
        // Window-owned rather than forwarded to the adapter: a screen reader addresses the WINDOW, and
        // these describe it. Like Control's, they are currently a described surface rather than a live one
        // -- nothing here is published to a platform accessibility API yet -- so they store and return what
        // they are told, which is what lets migrated code that sets them compile and keep its intent.

        /// <summary>Gets the accessible object that represents this window.</summary>
        public AccessibleObject AccessibilityObject => accessibility_object ??= CreateAccessibilityInstance ();

        private AccessibleObject? accessibility_object;

        /// <summary>Creates the accessible object for this window.</summary>
        protected virtual AccessibleObject CreateAccessibilityInstance () => new AccessibleObject ();

        /// <summary>Notifies accessibility clients of a change. A no-op, as on Control.</summary>
        public void AccessibilityNotifyClients (AccessibleEvents accEvent, int childID) { }

        /// <summary>Gets or sets the description of the window's default action.</summary>
        public string? AccessibleDefaultActionDescription { get; set; }

        /// <summary>Gets or sets the accessible role of the window.</summary>
        public AccessibleRole AccessibleRole { get; set; } = AccessibleRole.Default;

        /// <summary>Gets or sets whether the window is visible to accessibility clients.</summary>
        public bool IsAccessible { get; set; } = true;

        /// <summary>Raised when an accessibility client requests help for the window.</summary>
        /// <remarks>Never raised, as on Control: there is no accessibility client to ask. Present because
        /// designer code binds it.</remarks>
        public event QueryAccessibilityHelpEventHandler? QueryAccessibilityHelp { add { } remove { } }

        // The adapter forwards its layout pass to this window only once the window has been shown (see
        // ControlAdapter.OnLayout, which explains why). An explicit PerformLayout/ResumeLayout from the
        // consumer is a different thing entirely and has to reach the window's own OnLayout whether it is
        // on screen yet or not: a window that decides its visibility there -- DockPanelSuite's
        // FloatWindow sets `Visible = VisibleNestedPanes.Count > 0` in OnLayout, and constructs itself
        // inside SuspendLayout/ResumeLayout precisely so that runs -- can never become visible otherwise.
        // Left unraised, a document dragged out to float went into a window that was never shown, which
        // read as the document simply vanishing.
        private void RaiseLayoutForExplicitRequest ()
        {
            // Already forwarded by the adapter's own pass, and a re-entrant raise would let an OnLayout
            // that triggers layout recurse without end.
            if (shown || raising_layout)
                return;

            raising_layout = true;

            try {
                RaiseLayout (new LayoutEventArgs (adapter, null));
            } finally {
                raising_layout = false;
            }
        }

        private bool raising_layout;

        // The rest of this block is the same story as SuspendLayout/ResumeLayout above: members a
        // WinForms Form inherits from Control, which a Majorsilence.Forms Form cannot because it is not
        // one. Each forwards to the root adapter, which IS a Control and does host the children, so the
        // answers are the real ones rather than placeholders.

        /// <summary>
        /// Gets the <see cref="Control"/> that hosts this window's children.
        /// </summary>
        /// <remarks>
        /// In WinForms the Form *is* that control, so code passes a Form wherever a Control is wanted --
        /// as a drag source, as a parent to reparent onto, as the thing a Parent-walk expects to reach.
        /// A Majorsilence.Forms Form is not a Control, so this names the control it delegates to and
        /// gives such code somewhere real to point. <see cref="Control.FindForm"/> maps back the other
        /// way.
        /// </remarks>
        public Control ContentControl => adapter;

        /// <summary>Gets whether the window or one of its children currently has input focus.</summary>
        public bool ContainsFocus => adapter.ContainsFocus;

        /// <summary>Gets or sets whether the window has captured the mouse.</summary>
        public bool Capture {
            get => adapter.Capture;
            set => adapter.Capture = value;
        }

        /// <summary>Occurs when the window's handle is created.</summary>
        public event EventHandler? HandleCreated {
            add => adapter.HandleCreated += value;
            remove => adapter.HandleCreated -= value;
        }

        /// <summary>Occurs when the window's parent changes.</summary>
        public event EventHandler? ParentChanged {
            add => adapter.ParentChanged += value;
            remove => adapter.ParentChanged -= value;
        }

        /// <summary>Converts a screen rectangle to window client coordinates.</summary>
        public System.Drawing.Rectangle RectangleToClient (System.Drawing.Rectangle rect)
            => adapter.RectangleToClient (rect);

        /// <summary>Moves focus to the next control in the tab order.</summary>
        public bool SelectNextControl (Control? start, bool forward, bool tabStopOnly, bool nested, bool wrap)
            => adapter.SelectNextControl (start, forward, tabStopOnly, nested, wrap);

        /// <summary>
        /// Called when the window lays its controls out.
        /// </summary>
        /// <remarks>
        /// In WinForms a Form is a Control and inherits this from it. Here a Form is not a Control --
        /// its children live on the root ControlAdapter -- so the adapter forwards its own layout pass
        /// up to the window. Without it a Form could not override OnLayout at all, which is how a
        /// window that positions its children itself (a docking host, a splitter frame) does that work.
        ///
        /// This deliberately does not raise <see cref="Layout"/>: that event is forwarded straight to
        /// the adapter's own, which the adapter has already raised by the time it calls this. Raising it
        /// here too would deliver every layout to subscribers twice.
        /// </remarks>
        protected internal virtual void OnLayout (LayoutEventArgs e) { }

        // Called by the root adapter when it lays out, so the window's own OnLayout override runs as
        // part of the same pass rather than needing a separate trigger.
        internal void RaiseLayout (LayoutEventArgs e) => OnLayout (e);

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
            get => adapter is null ? Padding.Empty : ContentRoot.Padding;
            set { if (adapter is not null) ContentRoot.Padding = value; }
        }

        /// <summary>
        /// Gets or sets the window region. Stored for source parity, matching
        /// <see cref="Control.Region"/> (also stored) — Majorsilence.Forms does not clip a window to
        /// a non-rectangular region yet.
        /// </summary>
        /// <remarks>
        /// A window with a region is SHAPED: it paints only inside the region, and the rest of it reads
        /// through to whatever is behind. Both halves are needed — clipping alone would only expose the
        /// window's own opaque backdrop — so the backend is told to stop filling that backdrop (see
        /// <see cref="Backends.IWindowBackend.SetShaped"/>) and <see cref="RenderFrame"/> clips to the
        /// region.
        ///
        /// This is how a drag overlay draws just its guides: a full-screen, input-transparent window
        /// whose region is a handful of small shapes. Stored and never read, it produced the opposite —
        /// a screen-sized opaque rectangle over everything for the duration of a drag.
        /// </remarks>
        public Majorsilence.Forms.Drawing.Region? Region {
            get => region;
            set {
                if (ReferenceEquals (region, value))
                    return;

                region = value;

                Backend.SetShaped (value is not null);
                Invalidate ();
            }
        }

        private Majorsilence.Forms.Drawing.Region? region;

        /// <summary>
        /// Gets or sets the reading order of the window. Forwarded to the root control adapter, which
        /// is the parent of every child control, so children left on
        /// <see cref="Majorsilence.Forms.RightToLeft.Inherit"/> resolve through this the same way they
        /// resolve through a parent Control in WinForms.
        /// </summary>
        public virtual RightToLeft RightToLeft {
            get => adapter is null ? RightToLeft.No : adapter.RightToLeft;
            set { if (adapter is not null) adapter.RightToLeft = value; }
        }

        /// <summary>
        /// Gets or sets whether the window shows scrollbars when its children don't fit. Forwarded to
        /// the root control adapter (a <see cref="ScrollableControl"/>), so this really scrolls.
        /// </summary>
        public bool AutoScroll {
            get => adapter is not null && ContentScrollRoot.AutoScroll;
            set { if (adapter is not null) ContentScrollRoot.AutoScroll = value; }
        }

        /// <summary>Gets or sets the auto-scroll margin. Forwarded to the root control adapter.</summary>
        public System.Drawing.Size AutoScrollMargin {
            get => adapter is null ? System.Drawing.Size.Empty : ContentScrollRoot.AutoScrollMargin;
            set { if (adapter is not null) ContentScrollRoot.AutoScrollMargin = value; }
        }

        /// <summary>Gets or sets the minimum size of the auto-scroll area. Forwarded to the root control adapter.</summary>
        public System.Drawing.Size AutoScrollMinSize {
            get => adapter is null ? System.Drawing.Size.Empty : ContentScrollRoot.AutoScrollMinSize;
            set { if (adapter is not null) ContentScrollRoot.AutoScrollMinSize = value; }
        }

        /// <summary>Gets or sets the current scroll position. Forwarded to the root control adapter.</summary>
        public System.Drawing.Point AutoScrollPosition {
            get => adapter is null ? System.Drawing.Point.Empty : ContentScrollRoot.AutoScrollPosition;
            set { if (adapter is not null) ContentScrollRoot.AutoScrollPosition = value; }
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
