using System;
using System.Drawing;
using Majorsilence.Forms.Backends;
using SkiaSharp;

namespace Majorsilence.Forms
{
#if NETSTANDARD2_0
    // Designer-generated InitializeComponent brackets some controls (notably every Telerik Rad control,
    // via Majorsilence.Forms.Telerik.ISupportInitializeCompat) with ((ISupportInitialize)c).BeginInit()
    // / EndInit(). On net8.0/net10.0 those no-ops come from a default interface member on
    // ISupportInitializeCompat; the netstandard2.0 runtime has no DIM support, so Control supplies the
    // no-op implementation here instead. Explicit implementation keeps BeginInit/EndInit off Control's
    // public surface, and this whole block is netstandard2.0-only so the .NET TFMs' API is unchanged.
    public partial class Control : System.ComponentModel.ISupportInitialize
    {
        void System.ComponentModel.ISupportInitialize.BeginInit () { }
        void System.ComponentModel.ISupportInitialize.EndInit () { }
    }
#endif

    // WinForms-compatibility surface for Control: BackgroundImage, Invoke/BeginInvoke.
    public partial class Control
    {
        private Majorsilence.Forms.Drawing.Image? background_image;
        private ImageLayout background_image_layout = ImageLayout.Tile;
        private ControlStyles control_styles = ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer;

        /// <summary>
        /// Gets the combination of modifier keys (Ctrl/Shift/Alt) currently held down, as captured
        /// from the most recent input event. WinForms compatibility for code that reads the static
        /// Control.ModifierKeys (e.g. detecting Ctrl during a mouse-wheel zoom).
        /// </summary>
        public static Keys ModifierKeys { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating the control is double-buffered. Majorsilence.Forms always renders
        /// each control into its own off-screen surface, so this is effectively always true.
        /// </summary>
        protected virtual bool DoubleBuffered { get; set; } = true;

        /// <summary>
        /// Forces the control to invalidate and immediately repaint.
        /// </summary>
        /// <remarks>Upstream: <c>Invalidate (true); Update ();</c> (Control.cs Refresh) -- see
        /// <see cref="Update"/> for what "immediately" can mean here.</remarks>
        public virtual void Refresh ()
        {
            Invalidate (true);
            Update ();
        }

        /// <summary>
        /// Sets input focus to the control. Returns true if focus was successfully set.
        /// </summary>
        /// <remarks>
        /// Only a control that <see cref="CanFocus"/> -- one whose handle exists -- takes focus, as
        /// upstream's <c>FocusInternal</c> (Control.cs) calls <c>SetFocus</c> only then. A control on a
        /// form that has not been shown is not created yet, so this returns false there; use
        /// <see cref="Select ()"/> or <c>ActiveControl</c> to choose the control that gets focus on show.
        /// </remarks>
        public bool Focus ()
        {
            if (CanFocus)
                Select ();

            return Focused;
        }

        /// <summary>
        /// Computes the location of the specified screen point into client coordinates.
        /// </summary>
        public Point PointToClient (Point point)
        {
            var origin = PointToScreen (Point.Empty);
            var delta = new Point (point.X - origin.X, point.Y - origin.Y);

            // Undo the desktop/window ratio PointToScreen applied on the way out, or this is not its
            // inverse. The ratio is 1 whenever the window scale and the desktop scale agree, which was
            // always true until Application.UiScale could zoom one without the other -- so subtracting
            // the origin was enough and this asymmetry stayed hidden. Under a zoom it is not: a point
            // 20px into a control came back as 10px, which is exactly the kind of error that makes a
            // hit test miss.
            var window = FindWindow ();

            if (window is null)
                return delta;

            var scale = window.DesktopScaling;

            return scale is 0 or 1
                ? delta
                : new Point ((int)Math.Round (delta.X / scale), (int)Math.Round (delta.Y / scale));
        }

        /// <summary>Converts a Rectangle from client to screen coordinates.</summary>
        /// <remarks>
        /// The size converts as well as the origin. <see cref="PointToScreen"/> multiplies logical
        /// coordinates by the desktop factor, so pairing a converted origin with an unconverted
        /// <c>Width</c>/<c>Height</c> yields a rectangle scale-times too small -- invisible at scaling 1
        /// and wrong on every HiDPI display, in exactly the "is this point inside that control" test
        /// this method exists to answer.
        /// </remarks>
        public Rectangle RectangleToScreen (Rectangle rect)
        {
            var origin = PointToScreen (rect.Location);
            var scale = FindWindow ()?.DesktopScaling ?? 1;

            return scale is 0 or 1
                ? new Rectangle (origin, rect.Size)
                : new Rectangle (origin,
                    new Size ((int)Math.Round (rect.Width * scale), (int)Math.Round (rect.Height * scale)));
        }

        /// <summary>Converts a Rectangle from screen to client coordinates.</summary>
        /// <remarks>The inverse of <see cref="RectangleToScreen"/>, size included.</remarks>
        public Rectangle RectangleToClient (Rectangle rect)
        {
            var origin = PointToClient (rect.Location);
            var scale = FindWindow ()?.DesktopScaling ?? 1;

            return scale is 0 or 1
                ? new Rectangle (origin, rect.Size)
                : new Rectangle (origin,
                    new Size ((int)Math.Round (rect.Width / scale), (int)Math.Round (rect.Height / scale)));
        }

        /// <summary>Raises the GotFocus event on behalf of another control.</summary>
        /// <remarks>
        /// WinForms provides this so a composite control can surface the focus events of a child it
        /// wraps as its own -- a themed text box hosting a real text box, for instance.
        /// </remarks>
        protected void InvokeGotFocus (Control? toInvoke, EventArgs e) => toInvoke?.RaiseEnter ();

        /// <summary>Raises the LostFocus event on behalf of another control.</summary>
        /// <inheritdoc cref="InvokeGotFocus"/>
        protected void InvokeLostFocus (Control? toInvoke, EventArgs e) => toInvoke?.RaiseLeave ();

        /// <summary>
        /// Sets the specified <see cref="ControlStyles"/> flag.
        /// </summary>
        public void SetStyle (ControlStyles flag, bool value)
        {
            if (value)
                control_styles |= flag;
            else
                control_styles &= ~flag;
        }

        /// <summary>
        /// Returns whether the specified <see cref="ControlStyles"/> flag is set.
        /// </summary>
        public bool GetStyle (ControlStyles flag) => (control_styles & flag) == flag;

        /// <summary>
        /// Notifies the control of Windows messages. Declared so a form that overrides it compiles;
        /// never called, because there is no Win32 message pump here (the same documented non-goal as
        /// WndProc and the IMessageFilter surface).
        /// </summary>
        protected virtual void OnNotifyMessage (Message m) { }

        /// <summary>
        /// Gets or sets whether the control redraws itself whenever it is resized. The named shorthand
        /// for <see cref="ControlStyles.ResizeRedraw"/>, which is how a custom-painted control asks for
        /// a full repaint instead of only the newly-exposed strip; owner-drawn controls set it in their
        /// constructor, so it has to exist for them to compile.
        /// </summary>
        protected bool ResizeRedraw {
            get => GetStyle (ControlStyles.ResizeRedraw);
            set => SetStyle (ControlStyles.ResizeRedraw, value);
        }

        /// <summary>
        /// Gets or sets the background image displayed in the control.
        /// Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
        public virtual Majorsilence.Forms.Drawing.Image? BackgroundImage {
            get => background_image;
            set {
                if (background_image != value) {
                    background_image = value;
                    OnBackgroundImageChanged (EventArgs.Empty);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the layout used to position the <see cref="BackgroundImage"/>.
        /// </summary>
        public virtual ImageLayout BackgroundImageLayout {
            get => background_image_layout;
            set {
                if (background_image_layout != value) {
                    background_image_layout = value;
                    OnBackgroundImageLayoutChanged (EventArgs.Empty);
                    Invalidate ();
                }
            }
        }

        private protected void PaintBackgroundImage (PaintEventArgs e)
        {
            var image = background_image;

            if (image is null)
                return;

#pragma warning disable CA1416
            using var skBmp = image.ToSKBitmap ();
#pragma warning restore CA1416

            if (skBmp is null)
                return;

            var width = ScaledBounds.Width;
            var height = ScaledBounds.Height;

            switch (background_image_layout) {
                case ImageLayout.None: {
                        e.Canvas.DrawBitmap (skBmp, new SKRect (0, 0, skBmp.Width, skBmp.Height));
                        break;
                    }
                case ImageLayout.Center: {
                        var x = (width - skBmp.Width) / 2f;
                        var y = (height - skBmp.Height) / 2f;
                        e.Canvas.DrawBitmap (skBmp, new SKRect (x, y, x + skBmp.Width, y + skBmp.Height));
                        break;
                    }
                case ImageLayout.Stretch: {
                        e.Canvas.DrawBitmap (skBmp, new SKRect (0, 0, width, height));
                        break;
                    }
                case ImageLayout.Zoom: {
                        var scale = Math.Min ((float)width / skBmp.Width, (float)height / skBmp.Height);
                        var w = skBmp.Width * scale;
                        var h = skBmp.Height * scale;
                        var x = (width - w) / 2f;
                        var y = (height - h) / 2f;
                        e.Canvas.DrawBitmap (skBmp, new SKRect (x, y, x + w, y + h));
                        break;
                    }
                case ImageLayout.Tile:
                default: {
                        for (var y = 0; y < height; y += skBmp.Height)
                            for (var x = 0; x < width; x += skBmp.Width)
                                e.Canvas.DrawBitmap (skBmp, new SKRect (x, y, x + skBmp.Width, y + skBmp.Height));
                        break;
                    }
            }
        }

        /// <summary>
        /// Executes the specified delegate asynchronously on the thread that owns the control.
        /// </summary>
        public void BeginInvoke (Action action)
        {
            Guard.ThrowIfNull (action);
            Platform.Backend.Post (action);
        }

        /// <summary>
        /// Executes the specified delegate asynchronously on the thread that owns the control.
        /// WinForms compat overload accepting Delegate.
        /// </summary>
        /// <returns>
        /// A result that completes when the delegate has run; pass it to <see cref="EndInvoke"/> for the
        /// delegate's return value.
        /// </returns>
        public IAsyncResult BeginInvoke (Delegate method) => BeginInvoke (method, Array.Empty<object?> ());

        /// <summary>
        /// Executes the specified delegate asynchronously with args on the thread that owns the control.
        /// </summary>
        /// <returns>
        /// A result that completes when the delegate has run; pass it to <see cref="EndInvoke"/> for the
        /// delegate's return value.
        /// </returns>
        /// <remarks>
        /// The result used to be a Task that was never started (CTL-19), so <c>IsCompleted</c> stayed
        /// false and <c>AsyncWaitHandle.WaitOne ()</c> hung forever. Upstream returns a ThreadMethodEntry
        /// that completes when the callback runs and carries its return value or exception
        /// (Control.cs MarshaledInvoke / InvokeMarshaledCallback); a TaskCompletionSource is that here.
        /// </remarks>
        public IAsyncResult BeginInvoke (Delegate method, params object?[] args)
        {
            Guard.ThrowIfNull (method);

            var completion = new System.Threading.Tasks.TaskCompletionSource<object?> (
                System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

            Platform.Backend.Post (() => {
                try {
                    completion.SetResult (InvokeDelegate (method, args));
                } catch (Exception ex) {
                    // Kept for EndInvoke, and still thrown into the loop, which reports it through
                    // Application.ThreadException as upstream reports an asynchronous callback's failure.
                    completion.SetException (ex);
                    throw;
                }
            });

            return completion.Task;
        }

        // Calls the common delegate shapes directly, so their exceptions are not wrapped in a
        // TargetInvocationException the way DynamicInvoke wraps them.
        private static object? InvokeDelegate (Delegate method, object?[]? args)
        {
            switch (method) {
                case Action action when args is null || args.Length == 0:
                    action ();
                    return null;
                case MethodInvoker invoker when args is null || args.Length == 0:
                    invoker ();
                    return null;
            }

            try {
                return method.DynamicInvoke (args);
            } catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture (ex.InnerException).Throw ();
                throw;   // unreachable: Throw () always throws
            }
        }

        /// <summary>
        /// Waits for the asynchronous call started by <see cref="BeginInvoke(Delegate, object[])"/> and
        /// returns the delegate's return value, rethrowing its exception if it threw.
        /// </summary>
        /// <remarks>
        /// Called on the UI thread before the call has run, it runs pending posted work until it has --
        /// upstream EndInvoke likewise runs the marshaled callbacks itself on the owning thread rather
        /// than deadlock waiting for them (Control.cs EndInvoke).
        /// </remarks>
        public object? EndInvoke (IAsyncResult asyncResult)
        {
            Guard.ThrowIfNull (asyncResult);

            if (asyncResult is not System.Threading.Tasks.Task<object?> task)
                throw new ArgumentException ("The IAsyncResult was not returned by BeginInvoke on a Control.", nameof (asyncResult));

            if (!task.IsCompleted && Platform.Backend.CheckAccess ()) {
                while (!task.IsCompleted) {
                    Application.DoEvents ();

                    if (!task.IsCompleted)
                        ((IAsyncResult) task).AsyncWaitHandle.WaitOne (1);
                }
            }

            return task.GetAwaiter ().GetResult ();
        }

        /// <summary>
        /// Executes the specified delegate synchronously on the thread that owns the control.
        /// </summary>
        public void Invoke (Action action)
        {
            Guard.ThrowIfNull (action);
            Platform.Backend.Invoke (action);
        }

        /// <summary>
        /// Executes the specified Delegate synchronously on the UI thread and returns the result.
        /// WinForms compat overload.
        /// </summary>
        /// <remarks>
        /// No dedicated <c>Invoke(MethodInvoker method)</c> overload exists alongside
        /// <see cref="Invoke(Action)"/> -- <c>Action</c> and <c>MethodInvoker</c> are both bare
        /// <c>void()</c> delegates, so having both made every <c>Invoke(() =&gt; ...)</c> call in
        /// migrated code, by far the most common shape, ambiguous (CS0121). An explicitly-typed
        /// <c>MethodInvoker</c> (<c>Invoke(new MethodInvoker(Method))</c>, or a designer-generated
        /// <c>Invoke((MethodInvoker) (...))</c>) still works, routed through here.
        /// </remarks>
        public object? Invoke (Delegate method)
        {
            if (method is Action a) { Invoke (a); return null; }
            if (method is MethodInvoker mi) { Invoke (mi); return null; }
            return Platform.Backend.Invoke (() => method.DynamicInvoke ());
        }

        /// <summary>
        /// Executes the specified Delegate with arguments synchronously on the UI thread.
        /// </summary>
        public object? Invoke (Delegate method, params object?[] args)
        {
            return Platform.Backend.Invoke (() => method.DynamicInvoke (args));
        }

        /// <summary>
        /// Executes the specified delegate synchronously and returns its result.
        /// </summary>
        public T Invoke<T> (Func<T> func)
        {
            Guard.ThrowIfNull (func);
            return Platform.Backend.Invoke (func);
        }

        /// <summary>Gets or sets whether a wait cursor is shown for this control and its children.</summary>
        /// <remarks>
        /// As upstream (<c>Control.UseWaitCursor</c>): it sets the state <see cref="Cursor"/> reads, and every
        /// child takes the same value, so a whole form or panel goes busy at once. The cursor changes at
        /// once if the pointer is over the control (SVC-13: it was stored and read by nothing).
        /// </remarks>
        public bool UseWaitCursor {
            get => GetState (States.UseWaitCursor);
            set {
                if (GetState (States.UseWaitCursor) == value)
                    return;

                SetState (States.UseWaitCursor, value);

                foreach (Control child in Controls)
                    child.UseWaitCursor = value;

                FindForm ()?.RefreshHoverCursor ();
            }
        }

        /// <summary>Gets whether the control has a handle (this library's equivalent: it has gone live).</summary>
        /// <remarks>
        /// Set when <see cref="CreateHandle"/> runs -- from <see cref="CreateControl"/>, or earlier by
        /// reading <see cref="Handle"/> -- and <c>HandleCreated</c> is raised, rather than the constant true
        /// it used to be. The constant was not harmless: WinForms code standardly writes
        /// <c>if (IsHandleCreated)</c> as its "am I fully initialized yet?" guard inside layout paths, and
        /// answering true inside a constructor sent such code into members its constructor had not
        /// assigned yet.
        /// </remarks>
        public bool IsHandleCreated => GetState (States.HandleCreated);

        /// <summary>Always returns false in Majorsilence.Forms — right-to-left mirroring is not supported.</summary>
        public bool IsMirrored => false;

        /// <summary>Gets the control's window handle: always <see cref="IntPtr.Zero"/>, after creating the handle.</summary>
        /// <remarks>
        /// There is no per-control OS window, and a control handle is never faked (see
        /// docs/native-interop.md: a made-up value passed on to native code can hit someone else's
        /// window). Reading it still has upstream's side effect -- the getter creates the handle when
        /// there is none (Control.cs Handle) -- because <c>_ = control.Handle;</c> is the WinForms idiom
        /// for making <see cref="IsHandleCreated"/> true and raising <c>HandleCreated</c> early, typically
        /// so a later <c>BeginInvoke</c> guarded by it runs (CTL-20).
        /// </remarks>
        public IntPtr Handle {
            get {
                if (!IsHandleCreated)
                    CreateHandle ();

                return IntPtr.Zero;
            }
        }

        /// <summary>Creates the control's handle: marks it created and raises <c>HandleCreated</c>.</summary>
        /// <remarks>
        /// Does not create the control (<see cref="Created"/> and <c>OnCreateControl</c> come from
        /// <see cref="CreateControl"/>), as upstream's CreateHandle does not. An override that does not
        /// call the base gets no <c>HandleCreated</c>, as upstream.
        /// </remarks>
        protected virtual void CreateHandle ()
        {
            if (IsHandleCreated || GetState (States.Disposed))
                return;

            SetState (States.HandleCreated, true);
            OnHandleCreated (EventArgs.Empty);
        }

        /// <summary>Reapplies the control's styles: raises <see cref="StyleChanged"/> and repaints.</summary>
        /// <remarks>Real as of W6 mechanisms. The <see cref="ControlStyles"/> flags take effect the
        /// moment they are set here, so what remains of upstream's handle-level restyle is its two
        /// observable outcomes -- the event and a repaint.</remarks>
        public void UpdateStyles ()
        {
            OnStyleChanged (EventArgs.Empty);
            Invalidate ();
        }

        /// <summary>Gets whether this control or one of its descendents has keyboard focus.</summary>
        public bool ContainsFocus => Focused || Controls.Any (c => c.ContainsFocus);

        /// <summary>Gets whether the control can receive focus.</summary>
        /// <remarks>
        /// Requires a created handle, as upstream's <c>CanFocus</c> (Control.cs:
        /// <c>IsHandleCreated &amp;&amp; IsWindowVisible &amp;&amp; IsWindowEnabled</c>). Without it an
        /// unparented control -- which is <see cref="Visible"/>, as upstream -- could take focus that
        /// no window tracks (CTL-14).
        /// </remarks>
        public bool CanFocus => IsHandleCreated && Visible && Enabled && CanSelect;

        /// <summary>Gets whether the caller must use Invoke to call the control (always false on UI thread).</summary>
        public bool InvokeRequired => !Platform.Backend.CheckAccess ();

        private RightToLeft right_to_left = RightToLeft.Inherit;

        /// <summary>
        /// Gets or sets the reading order of the control. Like WinForms, the stored value defaults to
        /// <see cref="RightToLeft.Inherit"/> and the getter resolves it through the parent chain, so a
        /// control with no ancestor setting one still reads <see cref="RightToLeft.No"/>. Majorsilence.Forms
        /// does not mirror rendering yet; the value drives the <c>RtlTranslate*</c> helpers.
        /// </summary>
        public virtual RightToLeft RightToLeft {
            get => right_to_left != RightToLeft.Inherit
                ? right_to_left
                : Parent?.RightToLeft ?? RightToLeft.No;
            set {
                if (right_to_left == value)
                    return;

                // Compare what the property actually reports, not the stored value: switching a
                // child from an explicit Yes to Inherit under a Yes parent changes nothing observable.
                var old_resolved = RightToLeft;

                right_to_left = value;

                if (old_resolved != RightToLeft)
                    OnRightToLeftChanged (EventArgs.Empty);
            }
        }

        /// <summary>
        /// Suspends drawing until <see cref="EndUpdate"/> is called.
        /// In Majorsilence.Forms, equivalent to SuspendLayout().
        /// </summary>
        public void BeginUpdate () => SuspendLayout ();

        /// <summary>
        /// Resumes drawing after a call to <see cref="BeginUpdate"/>.
        /// In Majorsilence.Forms, equivalent to ResumeLayout() with a repaint.
        /// </summary>
        public void EndUpdate ()
        {
            ResumeLayout ();
            Invalidate ();
        }

        /// <summary>Gets or sets the accessible name of the control. Stub in Majorsilence.Forms.</summary>
        public string? AccessibleName { get; set; }

        /// <summary>Gets or sets the default action description for the control's accessibility object. Stub in Majorsilence.Forms.</summary>
        public string? AccessibleDefaultActionDescription { get; set; }

        /// <summary>Gets or sets the accessible description of the control. Stub in Majorsilence.Forms.</summary>
        public string? AccessibleDescription { get; set; }

        /// <summary>Gets or sets whether the control accepts data the user drags onto it.</summary>
        /// <remarks>Read by the drag-and-drop session as of W6 mechanisms: the deepest control under the
        /// pointer that has this set is the target of <see cref="DragEnter"/>, <see cref="DragOver"/>,
        /// <see cref="DragLeave"/> and <see cref="DragDrop"/>.</remarks>
        public virtual bool AllowDrop { get; set; }

        /// <summary>Causes all validation in the control hierarchy to occur. Always returns true in Majorsilence.Forms.</summary>
        public bool Validate () => Validate (checkAutoValidate: false);

        // Whether ValidateChildren recurses into this control's children. Upstream is
        // GetStyle (ControlStyles.ContainerControl), which ScrollableControl and GroupBox set and
        // TabControl overrides to true (Control.cs ShouldPerformContainerValidation). Here only
        // UserControl carries the style, and setting it on Panel would also make every panel a
        // focus-managing container, so the upstream holders are named instead. SplitContainer is a
        // ContainerControl upstream and a plain Control here.
        internal virtual bool ShouldPerformContainerValidation ()
            => GetStyle (ControlStyles.ContainerControl)
                || this is ScrollableControl or GroupBox or TabControl or SplitContainer;

        /// <summary>Runs this control's validation cycle, returning false when a handler cancelled it.</summary>
        /// <remarks>
        /// Both of these used to return true without raising anything, which made
        /// <c>ValidateChildren</c> a loop that did nothing. They now run the same Validating then
        /// Validated sequence that focus loss runs, and report the handler's Cancel.
        /// </remarks>
        public bool Validate (bool checkAutoValidate)
        {
            if (!CausesValidation)
                return true;

            var e = new System.ComponentModel.CancelEventArgs ();
            OnValidating (e);

            if (e.Cancel)
                return false;

            OnValidated (EventArgs.Empty);
            return true;
        }

        /// <summary>Gets or sets whether user input in the control causes validation to occur.</summary>
        public bool CausesValidation {
            get => GetState (States.CausesValidation);
            set {
                if (GetState (States.CausesValidation) == value)
                    return;

                SetState (States.CausesValidation, value);
                OnCausesValidationChanged (EventArgs.Empty);
            }
        }

        private ImeMode ime_mode = ImeMode.NoControl;

        /// <summary>Gets or sets the Input Method Editor (IME) mode. Stored and notified; Majorsilence.Forms
        /// does not drive a platform IME from it.</summary>
        public ImeMode ImeMode {
            get => ime_mode;
            set {
                if (ime_mode == value)
                    return;

                ime_mode = value;
                OnImeModeChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets the default IME mode for this control type, used by <see cref="ResetImeMode"/>.</summary>
        protected virtual ImeMode DefaultImeMode => ImeMode.NoControl;

        /// <summary>Begins a drag-and-drop operation. Stub in Majorsilence.Forms — always returns None.</summary>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects)
        {
            // Real as of W6 mechanisms: an in-process session (DragDropSession) takes over the window's
            // pointer events and this blocks in the nested loop until the drop, the cancel or Escape,
            // returning the effect the target chose -- upstream's contract, without an OLE source.
            var session = DragDropSession.Begin (this, data, allowedEffects);

            return Form.RunModal (session.Completion);
        }

        /// <summary>Paints the control's invalidated area now, before returning.</summary>
        /// <remarks>
        /// Upstream's UpdateWindow raises Paint synchronously (Control.cs Update). This used to be an
        /// <see cref="Invalidate()"/> (CTL-21), so a Paint handler ran only on the backend's next frame
        /// and anything reading the control's pixels straight after -- <c>label.Text = ...;
        /// label.Refresh ();</c> then <see cref="DrawToBitmap"/> -- saw the old ones. Now a dirty,
        /// created control paints into its surface here, raising <c>Paint</c> before this returns, and
        /// the window is asked to present it (<see cref="Backends.IWindowBackend.PresentNow"/>). The
        /// WinForms and WPF hosts put it on screen at once; Avalonia, GTK 4 and Uno render from their
        /// own frame loop, so there a UI thread blocked in a loop shows it only once it returns.
        /// </remarks>
        public void Update ()
        {
            if (this is ControlAdapter || !Created || !GetState (States.IsDirty) || !Visible || Width <= 0 || Height <= 0)
                return;

            var window = FindWindow ();

            if (window is null)
                return;

            var size = ScaledSize;
            var info = new SKImageInfo (size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using (var canvas = new SKCanvas (GetBackBuffer ())) {
                var args = new PaintEventArgs (info, canvas, Scaling);

                RaisePaintBackground (args);
                RaisePaint (args);

                canvas.Flush ();
            }

            // Every ancestor composites this surface into its own, and would otherwise reuse a copy
            // taken before this paint: a dirty descendant is what made them repaint until now
            // (NeedsPaint), so they are marked in its place.
            for (var p = parent; p is not null; p = p.parent)
                p.SetState (States.IsDirty, true);

            // Upstream's UpdateWindow ends with the pixels on screen; the composite above only reaches
            // it through the window's next frame unless the backend can present now (CTL-21).
            window.Backend?.PresentNow ();
        }

        /// <summary>Scales the control and its children by the specified horizontal and vertical scaling factors.</summary>
        public void Scale (float dx, float dy) => Scale (new System.Drawing.SizeF (dx, dy));

        /// <summary>
        /// Scrolls the nearest auto-scrolling ancestor so <paramref name="activeControl"/> is visible.
        /// Pass null to mean "this control".
        /// </summary>
        public void ScrollControlIntoView (Control? activeControl) => ScrollControlIntoView (activeControl ?? this, 0);

        /// <summary>
        /// Scrolls the nearest auto-scrolling ancestor so <paramref name="target"/> clears the visible
        /// region, keeping <paramref name="bottomInset"/> logical pixels free at the bottom (used to lift
        /// a focused field above an on-screen keyboard).
        /// </summary>
        internal void ScrollControlIntoView (Control target, int bottomInset)
        {
            if (target is null)
                return;

            // Nearest ancestor that actually scrolls.
            ScrollableControl? scroller = null;
            for (Control? c = target.Parent; c is not null; c = c.Parent) {
                if (c is ScrollableControl { AutoScroll: true } sc) {
                    scroller = sc;
                    break;
                }
            }
            if (scroller is null)
                return;

            // target's position in the scroller's client area as it is drawn now. A scrolled ScrollableControl moves its
            // children by the scroll offset (as WinForms does), so each Top already includes it: this is where the
            // field is on screen, not where it sits in the content.
            var top = 0;
            var left = 0;
            for (Control? c = target; c is not null && !ReferenceEquals (c, scroller); c = c.Parent) {
                top += c.Top;
                left += c.Left;
            }

            // LAY-30: AutoScrollMargin is the gap upstream keeps clear around the control it scrolls
            // to, so the field does not end up flush against the edge of the viewport.
            var margin = scroller.AutoScrollMargin;

            // In LOGICAL units, because everything else here is: Bounds, AutoScrollMargin and the
            // accumulated child offsets all are, while ClientRectangle is in DEVICE pixels. Comparing
            // the two directly made this scale-dependent -- on a 2x display it read the viewport as
            // twice its logical height, decided the control already fitted, and scrolled too little
            // or not at all. Caught by the MF_HEADLESS_SCALE=2 gate, not by review.
            var viewport = scroller.DeviceToLogicalUnits (scroller.DeviceClientRectangle.Size);
            var viewportBottom = viewport.Height - bottomInset;
            var current = scroller.AutoScrollPosition;      // WinForms-style negative offset
            var offsetY = -current.Y;
            var offsetX = -current.X;

            // Taking the offset off a second time read a visible field as further above the viewport the further the
            // panel was scrolled, so focusing it scrolled the content back up -- and a tap whose press focused a
            // control released over a different one.
            var visibleTop = top;
            var visibleBottom = visibleTop + target.Height;

            if (visibleBottom + margin.Height > viewportBottom)
                offsetY += visibleBottom + margin.Height - viewportBottom;
            else if (visibleTop - margin.Height < 0)
                offsetY += visibleTop - margin.Height;

            // The horizontal axis was simply not handled: a wide form scrolled sideways left the
            // focused field off to the right however far down the panel had scrolled to reach it.
            var visibleLeft = left;
            var visibleRight = visibleLeft + target.Width;

            if (visibleRight + margin.Width > viewport.Width)
                offsetX += visibleRight + margin.Width - viewport.Width;
            else if (visibleLeft - margin.Width < 0)
                offsetX += visibleLeft - margin.Width;

            offsetY = System.Math.Max (0, offsetY);
            offsetX = System.Math.Max (0, offsetX);

            if (offsetY != -current.Y || offsetX != -current.X)
                scroller.AutoScrollPosition = new System.Drawing.Point (offsetX, offsetY);
        }

        /// <summary>Returns the child control at the specified client coordinates, or null.
        /// WinForms z-order: index 0 is topmost, so the first match wins.</summary>
        public Control? GetChildAtPoint (System.Drawing.Point pt)
            => Controls.GetAllControls ().FirstOrDefault (c => c.Visible && c.Bounds.Contains (pt)
                && c.RegionContains (new System.Drawing.Point (pt.X - c.Left, pt.Y - c.Top)));

        /// <summary>Gets the current mouse cursor position in screen coordinates (alias for Cursor.Position).</summary>
        public static System.Drawing.Point MousePosition => Cursor.Position;

        /// <summary>Resets the Text property to its default value (empty string).</summary>
        public virtual void ResetText () => Text = string.Empty;

        /// <summary>Resets the Font property to null (theme default).</summary>
        public virtual void ResetFont () => Font = null;


        /// <summary>Gets or sets the accessible role. Stub in Majorsilence.Forms.</summary>
        public AccessibleRole AccessibleRole { get; set; } = AccessibleRole.Default;

        /// <summary>Gets or sets the ContextMenuStrip associated with this control (WinForms compat alias for ContextMenu).</summary>
        public virtual ContextMenuStrip? ContextMenuStrip {
            get => ContextMenu as ContextMenuStrip;
            set => ContextMenu = value;
        }

        private ControlBindingsCollection? _dataBindings;

        /// <summary>Gets the data bindings for this control. Bindings are live: they read the data source into the bound property and write edits back.</summary>
        /// <remarks>
        /// Binding finds members by name at run time, so a trimmed or NativeAOT app has to root the bound control property, its
        /// <c>&lt;Property&gt;Changed</c> event and the data source's property (see "Binding and trimming" in docs/backends.md).
        /// </remarks>
        public ControlBindingsCollection DataBindings => _dataBindings ??= new ControlBindingsCollection (this);

        private BindingContext? binding_context;

        // The context an UNPARENTED control without one of its own hands out. Upstream answers null
        // there (Control.cs BindingContextInternal) and binds nothing until the control is parented;
        // this library binds as soon as a binding is added, so an unparented control needs somewhere
        // to resolve its managers. It is deliberately NOT the control's own context (BND-15): storing
        // it as one made every control built in designer order -- DataBindings.Add, THEN
        // Controls.Add -- keep a private context forever, so two boxes bound to one table never moved
        // together and neither followed the form. Once the control has a parent this is ignored, and
        // the bindings that used it move to the inherited context (see UpdateBindings).
        private BindingContext? implicit_binding_context;

        /// <summary>
        /// Gets or sets the BindingContext for the control. Mirrors WinForms Control.BindingContext:
        /// inherited from the parent chain when not set locally, so controls on one form share
        /// binding-manager position state.
        /// </summary>
        /// <remarks>
        /// A control with neither a context of its own nor a parent answers with a provisional context
        /// rather than upstream's null, so a binding added before the control is parented works at
        /// once. The provisional context is not the control's own: parenting the control moves its
        /// bindings into the parent's context, as upstream's <c>UpdateBindings</c> does.
        /// </remarks>
        public virtual BindingContext BindingContext {
            get => binding_context ?? Parent?.BindingContext ?? (implicit_binding_context ??= new BindingContext ());
            set {
                if (ReferenceEquals (binding_context, value))
                    return;

                binding_context = value;
                OnBindingContextChanged (EventArgs.Empty);
            }
        }

        // IBindableComponent declares the property nullable; this control's own getter never returns
        // null (it creates a context on demand), which is the stronger guarantee, so the interface is
        // satisfied explicitly rather than weakening the public member.
        BindingContext? IBindableComponent.BindingContext {
            get => BindingContext;
            set => BindingContext = value ?? new BindingContext ();
        }

        // Whether BindingContext answers with a real context rather than the provisional one.
        internal bool HasInheritedOrOwnBindingContext => binding_context is not null || Parent is not null;

        // Upstream's Control.UpdateBindings (Control.cs, called from OnBindingContextChanged): every
        // binding moves to the manager the control's CURRENT context holds for its source. Only an
        // own or inherited context counts -- a control just taken off its parent keeps the managers it
        // had rather than snapping back to a fresh provisional context and showing the first row.
        internal virtual void UpdateBindings ()
        {
            if (!HasInheritedOrOwnBindingContext)
                return;

            if (_dataBindings is not { Count: > 0 } bindings)
                return;

            var context = BindingContext;

            foreach (var binding in bindings.ToArray ())
                BindingContext.UpdateBinding (context, binding);
        }

        // The not-yet-created half of a parent change. Upstream only re-homes once a control is
        // created, because only then does it bind at all; here a binding is live from the moment it is
        // added, so designer-order code (bind, then parent, then show) has to re-home at parenting
        // time. Without raising BindingContextChanged: upstream does not raise it for an uncreated
        // control either (Control.cs AssignParent), and CreateControl raises it later.
        internal void UpdateBindingsInTree ()
        {
            UpdateBindings ();

            foreach (var child in Controls.GetAllControls ().ToArray ()) {
                if (child.binding_context is null)
                    child.UpdateBindingsInTree ();
            }
        }

        /// <summary>Gets the Form that the control is on, if any.</summary>
        public Form? ParentForm => FindForm ();

        /// <summary>Gets the top-level control in the parent chain of this control.</summary>
        /// <remarks>
        /// Upstream returns the first control in the chain for which <see cref="GetTopLevel"/> is true
        /// (Control.cs TopLevelControl). This walked to the root unconditionally, so a control shown
        /// with <see cref="SetTopLevel"/> -- hosted in a popup window here -- answered with the popup's
        /// internal root instead of itself (CTL-25). A Form is not a <see cref="Control"/> in this
        /// library, so for a control on a form the chain still ends at the form's root control.
        /// </remarks>
        public Control? TopLevelControl {
            get {
                var ctrl = (Control)this;

                while (!ctrl.GetTopLevel () && ctrl.Parent is { } up)
                    ctrl = up;

                return ctrl;
            }
        }

        /// <summary>Gets the container of the component.</summary>
        public new System.ComponentModel.IContainer? Container => null;

        private bool _isDisposed;

        /// <summary>Gets whether this control has been disposed.</summary>
        public bool IsDisposed => _isDisposed;

        /// <summary>Gets or sets tab order index. Maps to TabIndex.</summary>
        public int TabOrder {
            get => TabIndex;
            set => TabIndex = value;
        }

        private AccessibleObject? _accessibilityObject;

        /// <summary>Gets the AccessibleObject assigned to the control.</summary>
        public AccessibleObject AccessibilityObject => _accessibilityObject ??= BindAccessibilityObject (CreateAccessibilityInstance ());

        // Ties the object to this control so RaiseLiveRegionChanged knows whose text to announce.
        private AccessibleObject BindAccessibilityObject (AccessibleObject accessible)
        {
            accessible.LiveOwner ??= this;
            return accessible;
        }

        /// <summary>Creates the accessibility object for this control. Override to return a custom implementation.</summary>
        protected virtual AccessibleObject CreateAccessibilityInstance () => new AccessibleObject ();

        /// <summary>
        /// Processes Windows messages. Override to intercept messages. Stub in Majorsilence.Forms — does nothing.
        /// </summary>
        protected virtual void WndProc (ref Message m) { }

        /// <summary>
        /// Invokes the default Windows procedure for the control. Stub in Majorsilence.Forms — does nothing.
        /// </summary>
        protected void DefWndProc (ref Message m) { }

        /// <summary>Gets the creation parameters for the control. Returns a stub in Majorsilence.Forms.</summary>
        protected virtual CreateParams CreateParams => new CreateParams ();

#pragma warning disable CA1416
        /// <summary>Paints this control, and the children it contains, into <paramref name="bitmap"/>.</summary>
        /// <remarks>
        /// <para>Real as of W6 mechanisms: it used to do nothing, so the standard "screenshot a
        /// control" idiom produced a blank image. The control paints through the same pipeline a real
        /// paint uses -- background, then itself, then its children -- into the region of the bitmap
        /// <paramref name="targetBounds"/> names, clipped to the bitmap and to the control's own size
        /// as upstream clips it. The control does not have to be on a shown form.</para>
        /// <para><paramref name="targetBounds"/> is in the bitmap's own pixels, which are DEVICE
        /// pixels: the paint pipeline places a control's children by their device bounds, so the
        /// canvas has to carry the control's <c>Scaling</c> for them to land in the right place. A
        /// bitmap of <c>ScaledWidth</c> by <c>ScaledHeight</c> therefore holds exactly the control.
        /// At scale 1, which is every ordinary display, that is the same as its <c>Size</c>.</para>
        /// </remarks>
        public void DrawToBitmap (Majorsilence.Forms.Drawing.Bitmap bitmap, Rectangle targetBounds)
        {
            Guard.ThrowIfNull (bitmap);

            if (bitmap.GetSKBitmap () is not { } surface)
                return;

            // Upstream clips the requested rectangle to the control and to the bitmap, and draws
            // nothing when what is left is empty. Both are device pixels here (see the remarks).
            var wanted = Rectangle.Intersect (targetBounds, new Rectangle (0, 0, ScaledWidth, ScaledHeight));
            wanted = Rectangle.Intersect (wanted, new Rectangle (0, 0, bitmap.Width, bitmap.Height));

            if (wanted.Width <= 0 || wanted.Height <= 0)
                return;

            var scaling = (float) Scaling;
            var info = new SkiaSharp.SKImageInfo (surface.Width, surface.Height, SkiaSharp.SKImageInfo.PlatformColorType, SkiaSharp.SKAlphaType.Premul);

            using var canvas = new SkiaSharp.SKCanvas (surface);
            var args = new PaintEventArgs (info, canvas, scaling);

            canvas.Save ();
            canvas.ClipRect (SkiaSharp.SKRect.Create (wanted.Left, wanted.Top, wanted.Width, wanted.Height));
            canvas.Translate (targetBounds.Left, targetBounds.Top);

            RaisePaintBackground (args);
            RaisePaint (args);

            canvas.Restore ();
            canvas.Flush ();
        }
#pragma warning restore CA1416

        // ── The keyboard pre-processing chain ────────────────────────────────────────────────────
        //
        // These used to be `=> false` with no caller anywhere, so `override ProcessCmdKey` -- one of
        // the most common customisations in a WinForms codebase -- compiled and silently never ran.
        // They are now the real chain, driven by PreProcessKeyMessage below and reached from
        // WindowBase.HandleKeyDown.
        //
        // Each bubbles to Parent exactly as upstream does (Control.cs: `_parent?.ProcessCmdKey(...)`),
        // so a container can claim a key on behalf of its children. ControlAdapter -- the root control
        // standing in for the window -- continues the walk into WindowBase/Form, which is where
        // AcceptButton, CancelButton and menu shortcuts live.

        /// <summary>
        /// Whether the control consumes <paramref name="keyData"/> itself rather than letting it be
        /// treated as a navigation key. A control that returns true here gets the key as
        /// <see cref="Control.KeyDown"/> and dialog processing is skipped for it.
        /// </summary>
        protected virtual bool IsInputKey (Keys keyData) => false;

        /// <summary>
        /// Whether the control consumes <paramref name="charCode"/> as text rather than as a dialog
        /// character (a mnemonic).
        /// </summary>
        protected virtual bool IsInputChar (char charCode) => false;

        /// <summary>
        /// Processes a command key before any key event is raised — the override point for keyboard
        /// shortcuts. Bubbles to <see cref="Control.Parent"/>, so the outermost container sees it last.
        /// </summary>
        protected virtual bool ProcessCmdKey (ref Message msg, Keys keyData)
        {
            var parent = Parent;
            return parent is not null && parent.ProcessCmdKey (ref msg, keyData);
        }

        /// <summary>
        /// Processes a dialog key — Tab, Enter, Escape, the arrows. Runs only after the focused control
        /// has declined the key via <see cref="IsInputKey"/>. Bubbles to <see cref="Control.Parent"/>.
        /// </summary>
        protected virtual bool ProcessDialogKey (Keys keyData) => Parent?.ProcessDialogKey (keyData) ?? false;

        /// <summary>
        /// Processes a dialog character — an Alt+letter mnemonic. Bubbles to <see cref="Control.Parent"/>.
        /// </summary>
        protected virtual bool ProcessDialogChar (char charCode) => Parent?.ProcessDialogChar (charCode) ?? false;

        /// <summary>Processes a keyboard message. Returns true if the message was handled.</summary>
        protected virtual bool ProcessKeyMessage (ref Message m)
        {
            var parent = Parent;
            return parent is not null && parent.ProcessKeyPreview (ref m);
        }

        /// <summary>
        /// Gives an ancestor first refusal on a keyboard message. Bubbles to <see cref="Control.Parent"/>;
        /// <see cref="Form.KeyPreview"/> is what makes the window act on it.
        /// </summary>
        protected virtual bool ProcessKeyPreview (ref Message m)
        {
            var parent = Parent;
            return parent is not null && parent.ProcessKeyPreview (ref m);
        }

        /// <summary>
        /// Performs the mnemonic operation (Alt+key) for the control. The default implementation does
        /// nothing and returns false; controls with a caption override it to click or take focus.
        /// </summary>
        protected virtual bool ProcessMnemonic (char charCode) => false;

        // ProcessMnemonic is protected, and the mnemonic walk in KeyboardShortcuts visits arbitrary
        // controls rather than descendants of one, so it needs an internal way in.
        internal bool RaiseProcessMnemonic (char charCode) => ProcessMnemonic (charCode);

        /// <summary>
        /// Runs the pre-processing chain for a key-down, in WinForms' order, starting at this control:
        /// <see cref="ProcessCmdKey"/> first (so a shortcut wins over everything), then
        /// <see cref="IsInputKey"/> (the control claiming the key for itself), and only if it declines,
        /// <see cref="ProcessDialogKey"/>.
        /// </summary>
        /// <returns>
        /// True when the key was consumed and no <see cref="Control.KeyDown"/> should follow.
        /// </returns>
        /// <remarks>
        /// Mirrors <c>Control.PreProcessMessage</c>'s WM_KEYDOWN branch. The order is the contract:
        /// putting the dialog keys first is what made Enter in a multiline text box submit the dialog.
        /// </remarks>
        internal bool PreProcessKeyMessage (Keys keyData)
        {
            var msg = new Message {
                Msg = WindowMessages.WM_KEYDOWN,
                WParam = (IntPtr) (int) (keyData & Keys.KeyCode),
            };

            if (ProcessCmdKey (ref msg, keyData))
                return true;

            // Alt+letter is a mnemonic. Upstream this arrives as a separate WM_SYSCHAR and goes to
            // ProcessDialogChar; the backends here deliver one Alt-modified key-down and no character,
            // so the access key is resolved from the key code instead. Ahead of IsInputKey because
            // TextBoxBase (like upstream) declines every Alt-modified key, and Alt+F must still open
            // the File menu while a text box has focus.
            if ((keyData & Keys.Alt) == Keys.Alt) {
                var character = MnemonicCharacterOf (keyData);

                if (character != '\0' && PreProcessKeyChar (character))
                    return true;
            }

            if (IsInputKey (keyData))
                return false;

            return ProcessDialogKey (keyData);
        }

        // The printable character an Alt-modified key stands for. Letters and digits only: those are
        // what an ampersand can mark in a caption.
        internal static char MnemonicCharacterOf (Keys keyData)
        {
            var code = keyData & Keys.KeyCode;

            if (code is >= Keys.A and <= Keys.Z)
                return (char) ('a' + (code - Keys.A));

            if (code is >= Keys.D0 and <= Keys.D9)
                return (char) ('0' + (code - Keys.D0));

            return '\0';
        }

        /// <summary>
        /// Runs the pre-processing chain for a character: a control that does not claim the character
        /// as text gets it offered as a dialog character (a mnemonic).
        /// </summary>
        internal bool PreProcessKeyChar (char charCode)
            => !IsInputChar (charCode) && ProcessDialogChar (charCode);

        /// <summary>Gets whether the control's Size includes scrollbar sizes. Stub in Majorsilence.Forms.</summary>
        public bool HScroll { get; set; }

        /// <summary>Gets whether the control's Size includes scrollbar sizes. Stub in Majorsilence.Forms.</summary>
        public bool VScroll { get; set; }

        /// <summary>Processes a tab key. Returns true if the key was processed. Majorsilence.Forms stub.</summary>
        protected virtual bool ProcessTabKey (bool forward) => false;

        /// <summary>Tells accessibility clients that something about this control changed.</summary>
        /// <remarks>Real as of W6 mechanisms: an <see cref="Automation.AutomationObserver"/> watching
        /// the control's window hears it -- a <c>Focus</c> event as <c>FocusChanged</c>, and a value,
        /// name, state or selection change as <c>ValueChanged</c> -- which is how a control that draws
        /// its own content announces a change the observer cannot see through <c>TextChanged</c>.
        /// Other events have no observer counterpart and go unheard. There is no OS accessibility
        /// bridge here, so the <paramref name="childID"/> is not forwarded.</remarks>
        public void AccessibilityNotifyClients (AccessibleEvents accEvent, int childID)
            => Automation.AutomationObserver.Notify (this, accEvent);

        // Notifies on change; the event was declared and raised by nothing (W6.1).
        private Majorsilence.Forms.Drawing.Region? region;

        /// <summary>Gets or sets the region, in the control's own coordinates, it paints and is hit in.</summary>
        /// <remarks>
        /// Upstream applies it with SetWindowRgn, which clips both painting and hit-testing
        /// (Control.cs Region). It used to be stored only (CTL-23), so a rounded or elliptical button
        /// built from a GraphicsPath drew as a rectangle and took clicks in its corners. Now the parent
        /// clips the control's surface to it when compositing (<see cref="PaintChildren"/>), and a point
        /// outside it falls through to whatever is underneath.
        /// </remarks>
        public Majorsilence.Forms.Drawing.Region? Region {
            get => region;
            set {
                if (ReferenceEquals (region, value))
                    return;

                region = value;
                OnRegionChanged (EventArgs.Empty);
                Invalidate ();
            }
        }

        // Whether a point in this control's own coordinates is inside its Region (everywhere, without one).
        internal bool RegionContains (Point local) => region is null || region.IsVisible (local);

        /// <summary>Gets whether this control is currently in design mode. Always false in Majorsilence.Forms.</summary>
        public new bool DesignMode => false;

        /// <summary>Gets the site associated with this component.</summary>
        /// <remarks>
        /// Real WinForms declares <c>Control.Site</c> as an <c>override</c> of the virtual
        /// <c>Component.Site</c> (not a hide) specifically so a derived control can override it again —
        /// this was <c>new</c> here, which broke that chain and made every such override (a control that
        /// needs to react to being sited, e.g. to suppress runtime-only behavior in the designer) a
        /// compile error (CS0506) instead of the no-op override real WinForms code expects to write.
        /// </remarks>
        public override System.ComponentModel.ISite? Site { get; set; }

    }

    /// <summary>Specifies the Input Method Editor mode.</summary>
    public enum ImeMode
    {
        /// <summary>IME not controlled by the application.</summary>
        NoControl = 0,
        /// <summary>IME turned on.</summary>
        On = 1,
        /// <summary>IME turned off.</summary>
        Off = 2,
        /// <summary>IME disabled.</summary>
        Disable = 3,
        /// <summary>IME closed.</summary>
        Close = 11,
        /// <summary>IME in hiragana input mode.</summary>
        Hiragana = 4,
        /// <summary>IME in katakana input mode.</summary>
        Katakana = 5,
        /// <summary>IME in half-width katakana input mode.</summary>
        KatakanaHalf = 6,
        /// <summary>IME in alphanumeric mode.</summary>
        Alpha = 8,
        /// <summary>IME in half-width alphanumeric mode.</summary>
        AlphaFull = 7,
        /// <summary>IME in hangul mode.</summary>
        Hangul = 10,
        /// <summary>IME in half-width hangul mode.</summary>
        HangulFull = 9,
        /// <summary>IME in half-width native mode.</summary>
        OnHalf = 12,
        /// <summary>Inherits from parent control.</summary>
        Inherit = -1,
    }

    /// <summary>
    /// Conversion helpers between <see cref="System.Drawing.Color"/> and <see cref="SKColor"/>.
    /// </summary>
    public static class ColorCompatExtensions
    {
        /// <summary>Converts a <see cref="System.Drawing.Color"/> to an <see cref="SKColor"/>.</summary>
        public static SKColor ToSKColor (this Color color) => new SKColor (color.R, color.G, color.B, color.A);

        /// <summary>Converts an <see cref="SKColor"/> to a <see cref="System.Drawing.Color"/>.</summary>
        public static Color ToDrawingColor (this SKColor color) => Color.FromArgb (color.Alpha, color.Red, color.Green, color.Blue);
    }

    /// <summary>Extension methods for converting <see cref="Majorsilence.Forms.Drawing.Image"/> to SkiaSharp bitmaps. Fully cross-platform (Skia-backed, no GDI+).</summary>
    public static class BitmapCompatExtensions
    {
        /// <summary>Returns a copy of the SkiaSharp bitmap backing the given <see cref="Majorsilence.Forms.Drawing.Bitmap"/>.</summary>
        public static SkiaSharp.SKBitmap? ToSKBitmap (this Majorsilence.Forms.Drawing.Bitmap bitmap)
            => bitmap?.GetSKBitmap ()?.Copy ();

        /// <summary>Returns a copy of the SkiaSharp bitmap backing the given <see cref="Majorsilence.Forms.Drawing.Image"/>.</summary>
        public static SkiaSharp.SKBitmap? ToSKBitmap (this Majorsilence.Forms.Drawing.Image image)
            => image?.GetSKBitmap ()?.Copy ();
    }
}
