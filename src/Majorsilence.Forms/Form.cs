using System.ComponentModel;
using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a top-level window to display to the user.
    /// </summary>
    public partial class Form : WindowBase, IWin32Window
    {
        // If the border is only 1 pixel it's too hard to resize, so we may steal some pixels from the client area
        private const int MINIMUM_RESIZE_PIXELS = 4;

        private WindowBase? dialog_parent;
        private DialogResult dialog_result = DialogResult.None;
        internal TaskCompletionSource<DialogResult>? dialog_task;
        private System.Drawing.Size minimum_size;
        private System.Drawing.Size maximum_size;

        private bool show_focus_cues;
        private string text = string.Empty;
        private bool use_system_decorations;
        private bool extends_content_into_title_bar;
        private Form? mdi_parent;

        // MDI state. On a container form, MdiClientControl is the client area hosting children. On a child
        // form, MdiHost is the frame hosting it inside its parent's client (and the child has no on-screen
        // OS window). Both are null for an ordinary top-level form.
        internal MdiClient? MdiClientControl;
        internal MdiChildWindow? MdiHost;

        // Set when this form was added to an ordinary control tree via Controls.Add(Form) -- the
        // "form.TopLevel = false; panel.Controls.Add (form)" idiom. Like MdiHost it means the form owns
        // no on-screen OS window and is composited by its frame instead, so the same properties that
        // branch on MdiHost below branch on this too.
        internal FormHost? PanelHost;

        // True while this form is drawn inside another control tree rather than its own OS window.
        internal override bool IsFrameHosted => MdiHost != null || PanelHost != null;

        /// <summary>
        /// Initializes a new instance of the Form class.
        /// </summary>
        public Form ()
        {
            InitWindow (Majorsilence.Forms.Backends.Platform.Backend.CreateWindow (this, isPopup: false));

            TitleBar = Controls.AddImplicitControl (new FormTitleBar ());

            Resizeable = true;
            Backend.SetSystemDecorations (false);

            // The native-close (Closing) hook is delivered via WindowBase.OnBackendClosing → OnClosing.

            // Windows/Linux draw fully custom chrome. macOS uses the NATIVE title bar (traffic lights,
            // rounded corners, shadow). A form that wants to paint into the title bar opts in with
            // ExtendsContentIntoTitleBar = true (Avalonia 12 full-size content view) — see RadTabbedForm.
            if (OperatingSystem.IsMacOS ())
                UseSystemDecorations = true;

            Backend.Size = DefaultSize;

            // Forward the internal adapter's own mouse events as public Form-level events --
            // WindowBase routes all mouse input through `adapter` (a Control) via
            // adapter.RaiseMouseDown/RaiseMouseMove, which already raise the adapter's own
            // MouseDown/MouseMove/Leave; Form itself just didn't expose them. Needed for
            // top-level windows that track the mouse over their own surface directly (e.g.
            // borderless popup pickers), the same way ported WinForms code commonly does on Form.
            adapter.Click += (s, e) => OnClick (e);
            adapter.MouseDown += (s, e) => OnMouseDown (e);
            adapter.MouseUp += (s, e) => OnMouseUp (e);
            adapter.MouseMove += (s, e) => OnMouseMove (e);
            adapter.MouseClick += (s, e) => OnMouseClick (e);
            adapter.MouseLeave += (s, e) => Leave?.Invoke (this, e);
        }

        /// <summary>
        /// Raised when the form's own surface is clicked. Inherited from Control on a WinForms Form;
        /// declared here because Form derives from WindowBase instead, and designer code attaches a
        /// form-level click handler routinely (a splash screen dismissing itself, say).
        /// </summary>
        public event EventHandler? Click;

        /// <summary>Raises the Click event.</summary>
        protected virtual void OnClick (EventArgs e) => Click?.Invoke (this, e);

        // Control declares OnMouseDown/OnMouseUp/OnMouseMove as protected virtuals and WinForms code
        // overrides them on a Form as a matter of course; Form derives from WindowBase here, so they
        // have to be declared alongside the events rather than inherited.

        /// <summary>Raises the MouseDown event.</summary>
        protected virtual void OnMouseDown (MouseEventArgs e) => MouseDown?.Invoke (this, e);

        /// <summary>Raises the MouseUp event.</summary>
        protected virtual void OnMouseUp (MouseEventArgs e) => MouseUp?.Invoke (this, e);

        /// <summary>Raises the MouseMove event.</summary>
        protected virtual void OnMouseMove (MouseEventArgs e) => MouseMove?.Invoke (this, e);

        /// <summary>Raises the MouseClick event.</summary>
        protected virtual void OnMouseClick (MouseEventArgs e) => MouseClick?.Invoke (this, e);

        /// <summary>Raised when a mouse button is pressed over the form's own surface.</summary>
        public event MouseEventHandler? MouseDown;

        /// <summary>Raised when a mouse button is released over the form's own surface.</summary>
        public event MouseEventHandler? MouseUp;

        /// <summary>Raised when the mouse moves over the form's own surface.</summary>
        public event MouseEventHandler? MouseMove;

        /// <summary>
        /// Raised when the form's own surface is clicked. Declared here (with an <c>On</c> hook,
        /// like <see cref="Click"/> and the other mouse events above) rather than left in WindowBase's
        /// generic event-forwarding block, because ported WinForms code overrides
        /// <c>OnMouseClick</c> on a Form as a matter of course -- that only works if the method is a
        /// real protected virtual declared here, not just an add/remove pair forwarding to the adapter.
        /// </summary>
        public event MouseEventHandler? MouseClick;

        /// <summary>Raised when the mouse leaves the form's own surface.</summary>
        public event EventHandler? Leave;

        /// <summary>Gets or sets whether the form causes validation to be performed on any controls that require validation when it receives focus. Matches Control.CausesValidation.</summary>
        public bool CausesValidation { get; set; } = true;

        // Validating is on WindowBase now, forwarded to the root adapter alongside Validated so the pair
        // cannot come from different objects. It used to be a discarding stub here (`add { } remove { }`),
        // so a handler attached to it was thrown away.

        /// <summary>Attempts to set focus to the form. Matches Control.Focus's shape (returns whether the focus request succeeded).</summary>
        public bool Focus ()
        {
            // A frame-hosted form owns no OS window, and must not be given one here. The backend's
            // Activate orders the native window on screen WITHOUT going through Show, so the window
            // appears while Avalonia's IsVisible stays false -- which also makes a later Hide a no-op,
            // so the window cannot be taken back down. That left a full-size stray window beside the
            // host every time something focused a hosted form. Focusing a hosted form means selecting
            // the frame that composites it, inside the tree that hosts it.
            if (HostFrame is { } frame) {
                frame.Select ();
                return true;
            }

            // Nor when the form owns no shown window for another reason: it was told it is not top-level,
            // or it simply is not visible. Activate() on the platform ORDERS THE WINDOW ON SCREEN without
            // going through Show, so IsVisible stays false and a later Hide is a no-op -- the window can
            // then never be taken back down. A docking library detaches a form (Parent = null) and focuses
            // it mid-re-dock, which is exactly this case, and left a blank window stranded over the
            // application.
            if (!visible || !TopLevel)
                return true;

            Backend.Activate ();
            return true;
        }

        // The control standing in for this form while it is hosted in someone else's tree, or null when
        // the form is a real top-level window. Panel hosting and MDI hosting both go through a frame.
        private Control? HostFrame => (Control?)PanelHost ?? MdiHost;

        /// <summary>
        /// The control this form is hosted in, when it has been put inside another control rather than
        /// shown as its own top-level window (see <see cref="Control.ControlCollection.Add(Form)"/>).
        /// Null for an ordinary top-level form.
        /// </summary>
        internal Control? HostingControl => HostFrame;

        /// <inheritdoc/>
        internal override WindowBase PresentationWindow {
            get {
                // Walk out through the host chain (a hosted form can itself be hosted) to the form that
                // owns a real window. FindForm climbs the parent chain, so this terminates.
                var host = HostFrame?.FindForm ();

                return host is not null && host != this ? host.PresentationWindow : this;
            }
        }

        /// <summary>Gets or sets the button that is activated when Enter is pressed.</summary>
        /// <remarks>
        /// Typed <see cref="IButtonControl"/>, as WinForms types it — the point of the interface is that a
        /// form's default button need not be a <see cref="Button"/> at all. A control library that
        /// re-declares the property to narrow or hide it (<c>public new IButtonControl AcceptButton</c>)
        /// could not compile against a <see cref="Button"/>-typed one.
        /// </remarks>
        public IButtonControl? AcceptButton { get; set; }

        /// <summary>Gets or sets whether the form can be maximized.</summary>
        public bool AllowMaximize {
            get => TitleBar.AllowMaximize;
            set => TitleBar.AllowMaximize = value;
        }

        /// <summary>Gets or sets whether the form can be minimized.</summary>
        public bool AllowMinimize {
            get => TitleBar.AllowMinimize;
            set => TitleBar.AllowMinimize = value;
        }

        /// <summary>Gets or sets the button that is activated when Escape is pressed.</summary>
        /// <inheritdoc cref="AcceptButton" path="/remarks"/>
        public IButtonControl? CancelButton { get; set; }

        /// <summary>Gets or sets whether the form receives key events before child controls.</summary>
        public bool KeyPreview { get; set; }

        /// <summary>Begins dragging the window to move it.</summary>
        public void BeginMoveDrag () => Backend.BeginMoveDrag ();

        /// <summary>Gets or sets the bounds of the Window.</summary>
        public new System.Drawing.Rectangle Bounds {
            get => new System.Drawing.Rectangle (Location, Size);
            set {
                Location = value.Location;
                Size = value.Size;
            }
        }

        /// <inheritdoc/>
        public override void Close ()
        {
            // An MDI child has no OS window to close — remove its frame from the parent's client instead.
            if (MdiHost != null) {
                var args = new CancelEventArgs ();
                OnClosing (args);
                if (args.Cancel)
                    return;

                var host = MdiHost;
                Application.OpenForms.Remove (this);
                host.Client.RemoveChild (host);   // clears MdiHost
                OnBackendClosed ();               // raises Closed + FormClosed (once)
                return;
            }

            // Same for a panel-hosted form: closing it means taking its frame out of the control tree
            // it was added to. The dashboard idiom depends on this -- "close the old page, add the new
            // one" is how those apps swap content, and leaving the frame parented would stack pages on
            // top of each other.
            if (PanelHost != null) {
                var args = new CancelEventArgs ();
                OnClosing (args);
                if (args.Cancel)
                    return;

                var host = PanelHost;
                Application.OpenForms.Remove (this);
                host.Parent?.Controls.Remove (host);   // clears PanelHost via FormHost.DetachChild
                PanelHost = null;
                OnBackendClosed ();                    // raises Closed + FormClosed (once)
                return;
            }

            base.Close ();

            // If close was cancelled by OnClosing, don't proceed with dialog cleanup
            if (Application.OpenForms.Contains (this))
                return;

            CompleteClose ();
        }

        /// <summary>
        /// Finishes a close that has actually gone through: hands the result back to
        /// <see cref="ShowDialog()"/> and re-enables the window that opened this one.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Close"/> because a close started by the window's own close button
        /// never calls Close at all -- the backend raises its Closed callback directly -- so
        /// <c>OnBackendClosed</c> has to reach this too. While it did not, dismissing a modal dialog
        /// with its close button made the window disappear while ShowDialog never returned and its
        /// owner stayed disabled: the whole app was left unusable and could not be shut down.
        /// Idempotent, because Close and the backend callback both run during a programmatic close.
        /// </remarks>
        internal void CompleteClose ()
        {
            if (dialog_parent is not null) {
                // Re-enable and raise the window that presents the opener, not the opener's own backend:
                // an MDI child's backend is an unshown window, and activating it made a blank duplicate
                // of that form appear on screen every time a dialog it opened was dismissed.
                var parentWindow = dialog_parent.PresentationWindow;

                parentWindow.Backend.Enabled = true;
                parentWindow.Backend.Activate ();

                // Activation still belongs to the opener, so hand it back within the MDI client too --
                // otherwise whichever child was active before the dialog keeps the caption highlight.
                if (dialog_parent is Form { MdiParent: { } mdiParent } child)
                    mdiParent.ActivateMdiChild (child);

                dialog_parent = null;
            }

            if (dialog_task is not null) {
                var task = dialog_task;
                dialog_task = null;

                // Dismissing a dialog without setting a result is a cancel, as in WinForms.
                task.SetResult (dialog_result == DialogResult.None ? DialogResult.Cancel : dialog_result);
            }
        }

        // Lets WindowBase run the closing sequence on a Form it holds a reference to.
        internal void RaiseClosing (CancelEventArgs e) => OnClosing (e);

        /// <summary>Raised before the form is closed, allowing close to be programatically canceled.</summary>
        public event CancelEventHandler? Closing;

        /// <summary>Raised before the form is closed (WinForms compatibility alias for Closing).</summary>
        public event FormClosingEventHandler? FormClosing;

        /// <summary>Raised after the form is closed.</summary>
        public event FormClosedEventHandler? FormClosed;

        private bool _formClosedFired;

        // Raises FormClosed exactly once, regardless of how many close callbacks reach it (programmatic
        // Close, close button, MDI removal can each drive OnBackendClosed). Called from OnBackendClosed.
        /// <summary>Raises the <see cref="FormClosed"/> event.</summary>
        /// <remarks>The overridable WinForms routes the event through, so a form that cleans up on
        /// close overrides this rather than subscribing to itself. On the real close path here too.</remarks>
        protected virtual void OnFormClosed (FormClosedEventArgs e) => FormClosed?.Invoke (this, e);

        internal void RaiseFormClosed ()
        {
            if (_formClosedFired)
                return;

            _formClosedFired = true;
            OnFormClosed (new FormClosedEventArgs ());
        }


        /// <summary>Raised when the form is first shown (WinForms compatibility alias; raised together with Shown).</summary>
        public event EventHandler? Load;

        /// <summary>Raised when the user begins to resize the form.</summary>
        /// <remarks>No backend reports the start of a user resize drag yet, so this does not fire on its
        /// own; it is a real event rather than a discard so that ported code which overrides
        /// <see cref="OnResizeBegin"/> compiles and runs once a backend can raise it.</remarks>
        public event EventHandler? ResizeBegin;

        /// <summary>Raises the <see cref="ResizeBegin"/> event.</summary>
        protected virtual void OnResizeBegin (EventArgs e) => ResizeBegin?.Invoke (this, e);

        /// <summary>Raised when the user finishes resizing the form. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? ResizeEnd;

        /// <summary>
        /// Raises the ResizeEnd event. Nothing in Majorsilence.Forms detects the end of a user resize
        /// drag yet, so this never fires on its own -- it exists so ported code that overrides it
        /// compiles, and so a backend that can report drag-end has somewhere to raise it.
        /// </summary>
        protected virtual void OnResizeEnd (EventArgs e) => ResizeEnd?.Invoke (this, e);

        /// <summary>
        /// Gets the window's default padding. Mirrors Control.DefaultPadding, which WinForms forms
        /// override to reserve space for their own chrome.
        /// </summary>
        protected virtual Padding DefaultPadding => Padding.Empty;

        /// <summary>Raised when the form is activated by the backend.</summary>
        public new event EventHandler? Activated {
            add => base.Activated += value;
            remove => base.Activated -= value;
        }

        /// <summary>Raised when the form is deactivated by the backend.</summary>
        public event EventHandler? Deactivate {
            add => base.Deactivated += value;
            remove => base.Deactivated -= value;
        }

        /// <summary>Raised on the MDI container when one of its child forms is activated.</summary>
        public event EventHandler? MdiChildActivate {
            add => mdi_child_activate += value;
            remove => mdi_child_activate -= value;
        }

        /// <summary>Raised when the DPI the form is displayed at changes.</summary>
        /// <remarks>
        /// Typed with the args WinForms uses, so a handler can read the old and new DPI -- the two numbers
        /// a form needs to rescale anything it sized itself. Declared and raisable but not raised: the
        /// backend does not notify this layer when a window moves between monitors of different scale.
        /// Its accessors used to be empty, which additionally meant handlers were silently discarded.
        /// </remarks>
        public event EventHandler<DpiChangedEventArgs>? DpiChanged;

        /// <summary>Raises the <see cref="DpiChanged"/> event.</summary>
        protected virtual void OnDpiChanged (DpiChangedEventArgs e) => DpiChanged?.Invoke (this, e);

        /// <summary>Raised when the input language changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<InputLanguageChangedEventArgs>? InputLanguageChanged { add { } remove { } }

        /// <summary>Raised when the input language is changing. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<InputLanguageChangingEventArgs>? InputLanguageChanging { add { } remove { } }

        /// <summary>Raised when the form is first displayed to the user.</summary>
        public new event EventHandler? Shown {
            add => base.Shown += value;
            remove => base.Shown -= value;
        }

        private bool _loadFired;

        // WinForms raises Load once, during the show sequence, BEFORE the form is displayed -- distinct
        // from Shown (which fires after first display). EnsureLoaded is called from WindowBase.Show/
        // ShowDialog (and the MDI-hosted path) just before the backend shows the window.
        internal override void EnsureLoaded ()
        {
            if (_loadFired)
                return;

            _loadFired = true;
            OnLoad (EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected override void OnShown (EventArgs e)
        {
            // Load is raised earlier (EnsureLoaded, before the window is shown); Shown fires after display.
            base.OnShown (e);
        }

        /// <inheritdoc/>
        protected override void OnClientLayoutChanged ()
        {
            base.OnClientLayoutChanged ();
            UpdateCaptionRegions ();
        }

        // Publishes the draggable title-bar region to the backend (declarative window-drag for backends
        // that can't begin a drag from code, e.g. Uno). Nothing to declare under system decorations —
        // the OS owns the title bar then.
        private void UpdateCaptionRegions ()
        {
            if (use_system_decorations || !TitleBar.Visible) {
                Backend.SetCaptionRegions (System.Array.Empty<System.Drawing.Rectangle> ());
                return;
            }

            // Logical, window-relative: the title-bar strip inside the border, minus the caption buttons
            // (close/maximize/minimize stay client area so their clicks reach Majorsilence.Forms). The
            // buttons sit on the right on Windows/Linux and on the left on macOS (traffic lights), so
            // shift the draggable region past them on whichever side they occupy.
            var border = CurrentStyle.Border;
            var top = border.Top.GetWidth ();
            var buttons = TitleBar.CaptionButtonsWidth;
            var left = border.Left.GetWidth () + (TitleBar.CaptionButtonsOnLeft ? buttons : 0);
            var width = Backend.ClientSize.Width - border.Left.GetWidth () - border.Right.GetWidth () - buttons;
            var height = TitleBar.Height;

            if (width <= 0 || height <= 0) {
                Backend.SetCaptionRegions (System.Array.Empty<System.Drawing.Rectangle> ());
                return;
            }

            Backend.SetCaptionRegions (new[] { new System.Drawing.Rectangle (left, top, width, height) });
        }

        /// <inheritdoc/>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size (1080, 720);

        /// <summary>Gets the default style for all forms.</summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.BackgroundColor;
                style.Border.Color = Theme.AccentColor2;
                style.Border.Width = 1;
            });

        /// <summary>Gets or sets the dialog result for the form.</summary>
        public DialogResult DialogResult {
            get => dialog_result;
            set {
                dialog_result = value;

                if (dialog_result != DialogResult.None && dialog_parent is not null)
                    Close ();
            }
        }

        /// <summary>Gets the next control in tab order.</summary>
        public Control? GetNextControl (Control? start, bool forward = true) => adapter.GetNextControl (start, forward);

        private Majorsilence.Forms.Drawing.Icon? _formIcon;

        /// <summary>
        /// Gets or sets the icon for the form. Accepts <see cref="Majorsilence.Forms.Drawing.Icon"/> for WinForms compatibility.
        /// </summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Icon? Icon {
            get => _formIcon;
            set {
                _formIcon = value;
                if (value is null)
                    Image = null;
                else
                    Image = value.ToBitmap ();
            }
        }
#pragma warning restore CA1416

        /// <summary>Gets or sets the image shown in the form's title bar.</summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Image? Image {
            get => TitleBar.Image;
            set {
                TitleBar.Image = value;

                if (value is null) {
                    Backend.SetIcon (null);
                } else {
                    using var sk = value.ToSKBitmap ();
                    if (sk is not null) {
                        using var ms = new System.IO.MemoryStream ();
                        sk.Encode (ms, SKEncodedImageFormat.Png, 100);
                        Backend.SetIcon (ms.ToArray ());
                    }
                }
            }
        }
#pragma warning restore CA1416

        /// <summary>
        /// Gets or sets the unscaled location of the control. For an MDI child this is its position within
        /// the parent's MDI client area, for a panel-hosted form the position of its frame within that
        /// panel; otherwise it's the window's screen position.
        /// </summary>
        public new System.Drawing.Point Location {
            get {
                if (MdiHost != null)
                    return new System.Drawing.Point (MdiHost.Left, MdiHost.Top);
                if (PanelHost != null)
                    return new System.Drawing.Point (PanelHost.Left, PanelHost.Top);
                return Backend.Location;
            }
            set {
                // Compared through the getter so the check covers all three hosting cases uniformly.
                if (Location == value)
                    return;

                if (MdiHost != null)
                    MdiHost.Client.MoveChild (MdiHost, value.X, value.Y);
                else if (PanelHost != null)
                    PanelHost.Location = value;
                else
                    Backend.Location = value;

                // WinForms raises Move/LocationChanged for a programmatic Location assignment, not only
                // for an OS-driven move. This setter shadows WindowBase.Location (`new`), so without
                // this the notification was lost for every Form -- code repositioning a satellite
                // window (a drop shadow, a tool window) from Move never ran.
                OnMove (EventArgs.Empty);
            }
        }

        private WindowElement GetElementAtLocation (int x, int y)
        {
            var left = false;
            var right = false;

            if (x < Math.Max (Style.Border.Left.GetWidth (), MINIMUM_RESIZE_PIXELS))
                left = true;
            else if (x >= ScaledSize.Width - Math.Max (Style.Border.Right.GetWidth (), MINIMUM_RESIZE_PIXELS))
                right = true;

            if (y < Math.Max (Style.Border.Top.GetWidth (), MINIMUM_RESIZE_PIXELS))
                return left ? WindowElement.TopLeftCorner : right ? WindowElement.TopRightCorner : WindowElement.TopBorder;
            else if (y >= ScaledSize.Height - Math.Max (Style.Border.Bottom.GetWidth (), MINIMUM_RESIZE_PIXELS))
                return left ? WindowElement.BottomLeftCorner : right ? WindowElement.BottomRightCorner : WindowElement.BottomBorder;

            return left ? WindowElement.LeftBorder : right ? WindowElement.RightBorder : WindowElement.Client;
        }

        internal override bool HandleMouseDown (int x, int y)
        {
            var element = GetElementAtLocation (x, y);

            switch (element) {
                case WindowElement.TopBorder:         Backend.BeginResizeDrag (Backends.WindowEdge.North);     return true;
                case WindowElement.RightBorder:       Backend.BeginResizeDrag (Backends.WindowEdge.East);      return true;
                case WindowElement.BottomBorder:      Backend.BeginResizeDrag (Backends.WindowEdge.South);     return true;
                case WindowElement.LeftBorder:        Backend.BeginResizeDrag (Backends.WindowEdge.West);      return true;
                case WindowElement.TopLeftCorner:     Backend.BeginResizeDrag (Backends.WindowEdge.NorthWest); return true;
                case WindowElement.TopRightCorner:    Backend.BeginResizeDrag (Backends.WindowEdge.NorthEast); return true;
                case WindowElement.BottomLeftCorner:  Backend.BeginResizeDrag (Backends.WindowEdge.SouthWest); return true;
                case WindowElement.BottomRightCorner: Backend.BeginResizeDrag (Backends.WindowEdge.SouthEast); return true;
            }

            return false;
        }

        internal override bool HandleMouseMove (int x, int y)
        {
            var element = GetElementAtLocation (x, y);

            switch (element) {
                case WindowElement.TopBorder:         Backend.SetCursor (Cursors.TopSide.CursorType);         return true;
                case WindowElement.RightBorder:       Backend.SetCursor (Cursors.RightSide.CursorType);       return true;
                case WindowElement.BottomBorder:      Backend.SetCursor (Cursors.BottomSide.CursorType);      return true;
                case WindowElement.LeftBorder:        Backend.SetCursor (Cursors.LeftSide.CursorType);        return true;
                case WindowElement.TopLeftCorner:     Backend.SetCursor (Cursors.TopLeftCorner.CursorType);   return true;
                case WindowElement.TopRightCorner:    Backend.SetCursor (Cursors.TopRightCorner.CursorType);  return true;
                case WindowElement.BottomLeftCorner:  Backend.SetCursor (Cursors.BottomLeftCorner.CursorType); return true;
                case WindowElement.BottomRightCorner: Backend.SetCursor (Cursors.BottomRightCorner.CursorType); return true;
            }

            return base.HandleMouseMove (x, y);
        }

        /// <summary>Gets or sets the maximum size of the Window.</summary>
        public System.Drawing.Size MaximumSize {
            get => maximum_size;
            set {
                if (maximum_size != value) {
                    maximum_size = value;

                    if (!minimum_size.IsEmpty && !maximum_size.IsEmpty)
                        minimum_size = new System.Drawing.Size (Math.Min (minimum_size.Width, maximum_size.Width), Math.Min (minimum_size.Height, maximum_size.Height));

                    ApplyMinMaxSize ();

                    var size = Size;
                    if (!value.IsEmpty && (size.Width > value.Width || size.Height > value.Height))
                        Size = new System.Drawing.Size (Math.Min (size.Width, value.Width), Math.Min (size.Height, value.Height));

                    OnMaximumSizeChanged (EventArgs.Empty);
                }
            }
        }

        /// <summary>Gets or sets the minimum size of the Window.</summary>
        public System.Drawing.Size MinimumSize {
            get => minimum_size;
            set {
                if (minimum_size != value) {
                    minimum_size = value;

                    if (!minimum_size.IsEmpty && !maximum_size.IsEmpty)
                        maximum_size = new System.Drawing.Size (Math.Max (minimum_size.Width, maximum_size.Width), Math.Max (minimum_size.Height, maximum_size.Height));

                    ApplyMinMaxSize ();

                    var size = Size;
                    if (size.Width < value.Width || size.Height < value.Height)
                        Size = new System.Drawing.Size (Math.Max (size.Width, value.Width), Math.Max (size.Height, value.Height));

                    OnMinimumSizeChanged (EventArgs.Empty);
                }
            }
        }

        private void ApplyMinMaxSize ()
        {
            Backend.MinimumSize = minimum_size;
            Backend.MaximumSize = maximum_size;
        }

        /// <summary>Gets or sets the name of the form.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Raises the Closing event.</summary>
        /// <remarks>
        /// Protected, as in WinForms. <see cref="WindowBase"/> reaches it through
        /// <see cref="RaiseClosing"/>, since a base class cannot call a protected member through a
        /// derived-typed reference.
        /// </remarks>
        protected virtual void OnClosing (CancelEventArgs e)
        {
            Closing?.Invoke (this, e);

            var form_closing_args = new FormClosingEventArgs { Cancel = e.Cancel };
            OnFormClosing (form_closing_args);

            if (form_closing_args.Cancel)
                e.Cancel = true;
        }

        /// <summary>Raises the <see cref="FormClosing"/> event.</summary>
        /// <remarks>
        /// The hook a form overrides to veto its own close -- "you have unsaved changes, really quit?" is
        /// written this way as often as with a handler, and it has to be reachable for that code to compile.
        /// Routed through by <see cref="OnClosing"/>, so an override sees every close the event does.
        /// </remarks>
        protected virtual void OnFormClosing (FormClosingEventArgs e) => FormClosing?.Invoke (this, e);

        /// <summary>
        /// Destroys the handle associated with this form. In real WinForms, inherited from Control and
        /// called once as the window is torn down, immediately before <c>OnHandleDestroyed</c>/
        /// <c>HandleDestroyed</c> fire -- ported code commonly overrides it to release window-lifetime
        /// resources at that exact point. Form derives from WindowBase rather than Control here, so it
        /// has to be declared afresh; <see cref="WindowBase.OnBackendClosed"/> calls it (in place of
        /// raising <c>OnHandleDestroyed</c> directly) for every Form, right where WinForms' own
        /// DestroyHandle would have fired it.
        /// </summary>
        protected virtual void DestroyHandle () => OnHandleDestroyed (EventArgs.Empty);

        /// <summary>
        /// Invokes <see cref="DestroyHandle"/> from <see cref="WindowBase.OnBackendClosed"/> -- a base
        /// class cannot call a protected member through a derived-typed reference (same reason
        /// <see cref="RaiseClosing"/> exists alongside <see cref="OnClosing"/>).
        /// </summary>
        internal void RaiseDestroyHandle () => DestroyHandle ();

        /// <summary>
        /// Picks the form a modal dialog should be owned by: the first open form that is not the dialog
        /// itself and actually owns a window.
        /// </summary>
        /// <remarks>
        /// See <see cref="Application.ModalOwnerCandidates"/> for why frame-hosted forms are excluded.
        /// Factored out of ShowDialog so it is testable without entering the modal loop, where a
        /// regression would hang the test run instead of failing it.
        /// </remarks>
        internal static Form? FindModalOwner (Form dialog) =>
            Application.ModalOwnerCandidates.FirstOrDefault (f => f != dialog);

        /// <summary>Displays the window modally using the first open form as the parent.</summary>
        public DialogResult ShowDialog ()
        {
            var parent = FindModalOwner (this);

            if (parent == null) {
                Show ();
                return DialogResult.OK;
            }

            return ShowDialog (parent);
        }

        /// <summary>Shows the form as a modal dialog with the specified owner window. Stub — ignores owner parameter.</summary>
        public DialogResult ShowDialog (IWin32Window owner) => ShowDialog ();

        /// <summary>Shows the form, ignoring the owner parameter (Majorsilence.Forms has no Win32 parenting).</summary>
        public void Show (IWin32Window owner) => Show ();

        /// <summary>Implements IWin32Window: returns IntPtr.Zero (Majorsilence.Forms has no Win32 handle).</summary>
        IntPtr IWin32Window.Handle => IntPtr.Zero;

        // Blocks the current call while the backend runs a nested message loop so the modal
        // dialog can receive and handle input events without deadlocking the UI thread.
        internal static T RunModal<T> (Task<T> modalTask)
        {
            Backends.Platform.Backend.RunModalLoop (modalTask);
            return modalTask.GetAwaiter ().GetResult ();
        }

        /// <summary>Called when the theme changes.</summary>
        protected internal virtual void OnThemeChanged (EventArgs e)
        {
            foreach (var control in Controls.GetAllControls ())
                control.OnThemeChanged (e);

            // Repaint the window (backends that only paint on demand, e.g. Uno, won't otherwise refresh).
            Invalidate ();
        }

        internal override void SetWindowStartupLocation (WindowBase? owner = null)
        {
            var scaling = Scaling;

            // Window size in device pixels (screen geometry is reported in device pixels).
            var width = (int) (Backend.ClientSize.Width * scaling);
            var height = (int) (Backend.ClientSize.Height * scaling);

            if (StartPosition == FormStartPosition.CenterScreen) {
                var ownerPos = owner is not null ? owner.Backend.Location : Backend.Location;
                var screen = Screen.FromPoint (ownerPos);

                if (screen != null) {
                    var wa = screen.WorkingArea;
                    var position = new System.Drawing.Point (
                        wa.X + (wa.Width - width) / 2,
                        wa.Y + (wa.Height - height) / 2);

                    // Ensure we don't position the titlebar offscreen
                    position.X = Math.Max (position.X, wa.X);
                    position.Y = Math.Max (position.Y, wa.Y);

                    Location = position;
                }
            } else if (StartPosition == FormStartPosition.CenterParent) {
                if (owner != null) {
                    var ownerPos = owner.Backend.Location;
                    var ownerWidth = (int) (owner.Backend.ClientSize.Width * scaling);
                    var ownerHeight = (int) (owner.Backend.ClientSize.Height * scaling);

                    var x = ownerPos.X + (ownerWidth - width) / 2;
                    var y = ownerPos.Y + (ownerHeight - height) / 2;
                    Location = new System.Drawing.Point (x, y);
                }
            }
        }

        /// <summary>Displays the window modally with the given owner and blocks until closed.
        /// Mirrors WinForms Form.ShowDialog(owner); the modal loop keeps the UI pumped.</summary>
        public DialogResult ShowDialog (Form parent)
        {
            var result = RunModal (ShowDialogAsync (parent));
            // FormClosed is raised once from OnBackendClosed during the dialog's close, before this returns.
            return result;
        }

        /// <summary>Displays the window to the user modally, preventing interaction with other windows until closed.</summary>
        public Task<DialogResult> ShowDialogAsync (Form parent)
        {
            dialog_task = new TaskCompletionSource<DialogResult> ();

            if (dialog_result != DialogResult.None) {
                dialog_task.SetResult (dialog_result);
                return dialog_task.Task;
            }

            dialog_parent = parent;

            // Call the base window-show-modally helper, NOT this Form.ShowDialog(Form) overload.
            base.ShowDialog (parent);

            return dialog_task.Task;
        }

        /// <summary>Gets a value indicating a focus rectangle should be drawn on the selected control.</summary>
        public bool ShowFocusCues {
            get => show_focus_cues;
            internal set {
                if (show_focus_cues != value) {
                    show_focus_cues = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the unscaled size of the window. For an MDI child this is the size of its content
        /// area inside the host frame, for a panel-hosted form the size of its frame; otherwise it's the
        /// window's client size.
        /// </summary>
        public new System.Drawing.Size Size {
            get {
                if (MdiHost != null)
                    return MdiHost.ContentSize;
                if (PanelHost != null)
                    return PanelHost.Size;
                return Backend.ClientSize;
            }
            set {
                // Clamp instead of throwing. WinForms hands the size to SetWindowPos, which treats a
                // negative extent as zero, so laying a window out to a negative size is something
                // WinForms code does and survives -- a docking pane whose available area has collapsed
                // computes exactly that while the user drags a splitter past its neighbour. The Avalonia
                // backend rejects it with ArgumentException, which crashed the app mid-layout.
                value = new System.Drawing.Size (Math.Max (0, value.Width), Math.Max (0, value.Height));

                // Writing an unchanged size is not free: it is a round trip to the window server, and a
                // drag that recomputes geometry per mouse-move sets the same value over and over. Measured
                // on a float-window drag, 61 of 85 size writes were no-ops -- enough platform traffic to
                // make the drag visibly lag behind the cursor until the mouse stopped.
                if (value == Size)
                    return;

                if (MdiHost != null)
                    MdiHost.SetContentSize (value);
                else if (PanelHost != null)
                    PanelHost.Size = value;
                else
                    Backend.Size = value;
            }
        }

        /// <summary>Gets or sets the width of the window, in pixels. Equivalent to Size.Width.</summary>
        public int Width {
            get => Size.Width;
            set => Size = new System.Drawing.Size (value, Size.Height);
        }

        /// <summary>Gets or sets the height of the window, in pixels. Equivalent to Size.Height.</summary>
        public int Height {
            get => Size.Height;
            set => Size = new System.Drawing.Size (Size.Width, value);
        }

        /// <summary>Gets the currently active form (the most recently focused open form).</summary>
        public static Form? ActiveForm => Application.OpenForms.LastOrDefault ();

        /// <summary>Gets or sets the client area size (equivalent to Size for Majorsilence.Forms).</summary>
        public System.Drawing.Size ClientSize {
            get => Size;
            set => SetClientSizeCore (value.Width, value.Height);
        }

        /// <summary>
        /// Performs the work of setting the client size. The override point WinForms code reaches for
        /// when a custom-chrome form needs to intercept a ClientSize assignment (e.g. to size itself
        /// off the raw value in design mode, bypassing whatever border math a real Win32 client-area
        /// distinction would otherwise apply). Majorsilence.Forms treats ClientSize as Size, so the
        /// base implementation just forwards; a Windows-only override that adjusts for a non-client
        /// border still compiles and runs here, it just has no border to adjust for.
        /// </summary>
        protected virtual void SetClientSizeCore (int x, int y) => Size = new System.Drawing.Size (x, y);

        /// <summary>Gets or sets the automatic scaling mode.</summary>
        /// <remarks>
        /// Stored and returned, but it does not drive layout: the platform backend owns DPI scaling,
        /// so there is no designer-time-to-runtime font/DPI rescale to perform. Every WinForms designer
        /// file assigns this, hence the round-trip rather than throwing.
        /// </remarks>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Font;

        /// <inheritdoc cref="AutoScaleMode"/>
        public System.Drawing.SizeF AutoScaleDimensions { get; set; }

        /// <summary>Gets or sets how the form performs implicit validation when focus leaves a child control.</summary>
        public AutoValidate AutoValidate { get; set; } = AutoValidate.EnablePreventFocusChange;

        /// <summary>Validates all selectable child controls, returning false if any handler cancelled.</summary>
        /// <remarks>
        /// This used to `return true` without validating anything -- while the
        /// <see cref="ValidateChildren(ValidationConstraints)"/> overload right next to it was real. The
        /// parameterless one is the one nearly everybody calls, so the working overload was the one nobody
        /// reached. Both now run the same walk.
        /// </remarks>
        public bool ValidateChildren () => ValidateChildren (ValidationConstraints.Selectable);

        private BindingContext? binding_context;

        /// <summary>
        /// Gets or sets the BindingContext for the form. Mirrors WinForms Form.BindingContext:
        /// binding managers are cached per (dataSource, dataMember) pair so all lookups on the form
        /// share position state.
        /// </summary>
        public BindingContext BindingContext {
            get => binding_context ??= new BindingContext ();
            set => binding_context = value;
        }

        /// <inheritdoc/>
        /// <remarks>Routed to this form's own <see cref="BindingContext"/> so the public property and the
        /// <c>IBindableComponent</c> one cannot drift apart.</remarks>
        internal override BindingContext? BindingContextCore {
            get => BindingContext;
            set => BindingContext = value ?? new BindingContext ();
        }

        /// <summary>Gets or sets the border style of the form.</summary>
        /// <remarks>
        /// <para>
        /// <see cref="FormBorderStyle.None"/> means no caption and no border at all. An app that wants
        /// to draw its own title bar sets it and then paints the caption itself, so honouring it has to
        /// suppress <em>both</em> chromes: the OS decorations, and the <see cref="TitleBar"/> this
        /// library draws in their place. Suppressing only one leaves the window wearing two title bars.
        /// </para>
        /// <para>
        /// The fixed styles keep a caption but drop the resize grip; <see cref="UseSystemDecorations"/>
        /// still chooses <em>whose</em> caption that is.
        /// </para>
        /// </remarks>
        public FormBorderStyle FormBorderStyle {
            get => form_border_style;
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (form_border_style == value)
                    return;

                form_border_style = value;

                Backend.SetSystemDecorations (use_system_decorations && !IsBorderless);
                Style.Border.Width = IsBorderless || use_system_decorations ? 0 : 1;
                Resizeable = value is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;
                UpdateTitleBarChrome ();
            }
        }
        private FormBorderStyle form_border_style = FormBorderStyle.Sizable;

        // Whether the form asked for no chrome whatsoever.
        private bool IsBorderless => form_border_style == FormBorderStyle.None;

        /// <summary>Gets or sets whether a maximize button appears in the title bar.</summary>
        public bool MaximizeBox {
            get => Backend.CanResize;
            set => Backend.CanResize = value;
        }

        /// <summary>Gets or sets whether a minimize button appears in the title bar.</summary>
        public bool MinimizeBox { get; set; } = true;

        /// <summary>Gets or sets whether the form is displayed in the taskbar.</summary>
        public bool ShowInTaskbar {
            get => Backend.ShowInTaskbar;
            set => Backend.ShowInTaskbar = value;
        }

        /// <summary>Gets or sets whether the form is displayed on top of all other windows.</summary>
        public bool TopMost {
            get => Backend.Topmost;
            set => Backend.Topmost = value;
        }

        /// <summary>Gets or sets the size-grip style for the form (stub).</summary>
        public SizeGripStyle SizeGripStyle {
            get => size_grip_style;
            set {
                SourceGenerated.EnumValidator.Validate (value);
                size_grip_style = value;
            }
        }
        private SizeGripStyle size_grip_style = SizeGripStyle.Auto;

        /// <summary>Gets or sets the form opacity (0.0 = transparent, 1.0 = opaque). Values are clamped to the range [0, 1].</summary>
        public double Opacity {
            get => Backend.Opacity;
            set {
                if (value > 1.0)
                    value = 1.0;
                else if (value < 0.0)
                    value = 0.0;

                Backend.Opacity = value;
            }
        }

        /// <summary>Gets or sets the color treated as transparent. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Color TransparencyKey { get; set; } = System.Drawing.Color.Empty;

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Clears the form's title. Part of the designer Reset* pattern.</summary>
        /// <remarks>Declared here rather than on <see cref="WindowBase"/> because <see cref="Text"/> is,
        /// so this is the only place that can clear the same storage the property writes.</remarks>
        public virtual void ResetText () => Text = string.Empty;

        /// <summary>Gets or sets the text for the form title bar.</summary>
        public virtual string Text {
            get => text;
            set {
                if (text != value) {
                    text = value;
                    Backend.Title = text;
                    TitleBar.Text = text;
                }
            }
        }

        /// <summary>Gets the title bar for the form.</summary>
        public FormTitleBar TitleBar { get; }

        /// <summary>
        /// Gets or sets whether the form should use the operating system's title bar and decorations.
        /// Must be changed before the form is shown for the first time.
        /// </summary>
        public bool UseSystemDecorations {
            get => use_system_decorations;
            set {
                if (shown)
                    throw new InvalidOperationException ($"Cannot change {nameof (UseSystemDecorations)} once a Form has been shown.");

                if (use_system_decorations != value) {
                    use_system_decorations = value;
                    Style.Border.Width = IsBorderless || use_system_decorations ? 0 : 1;
                    Backend.SetSystemDecorations (value && !IsBorderless);
                    UpdateTitleBarChrome ();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the form's content (and its <see cref="TitleBar"/>) is extended up into
        /// the native OS title bar, so the application can paint into it while the OS keeps drawing the
        /// native caption buttons, rounded corners and window shadow. Only has an effect together with
        /// <see cref="UseSystemDecorations"/> (the platform must provide a native title bar — macOS).
        /// On macOS this is enabled by default. Must be changed before the form is shown.
        /// </summary>
        public bool ExtendsContentIntoTitleBar {
            get => extends_content_into_title_bar;
            set {
                if (shown)
                    throw new InvalidOperationException ($"Cannot change {nameof (ExtendsContentIntoTitleBar)} once a Form has been shown.");

                if (extends_content_into_title_bar != value) {
                    extends_content_into_title_bar = value;
                    UpdateTitleBarChrome ();
                }
            }
        }

        // Reconciles the title bar's visibility/overlay mode and the backend's title-bar extension with
        // the current UseSystemDecorations + ExtendsContentIntoTitleBar combination.
        private void UpdateTitleBarChrome ()
        {
            var extend = use_system_decorations && extends_content_into_title_bar && !IsBorderless;

            // The custom title bar is shown for fully-custom chrome, or when merged into a native bar --
            // but never on a borderless form, which asked for no caption from anyone.
            TitleBar.Visible = !IsBorderless && (!use_system_decorations || extend);
            // In the merged case the OS draws the caption buttons, so the title bar runs in overlay mode.
            TitleBar.NativeOverlay = extend;

            Backend.SetExtendClientIntoTitleBar (extend, TitleBar.PreferredHeight);
        }

        /// <summary>Gets or sets the state of the form (normal/minimized/maximized).</summary>
        public FormWindowState WindowState {
            get => Backend.WindowState;
            set {
                SourceGenerated.EnumValidator.Validate (value);
                Backend.WindowState = value;
            }
        }

        /// <summary>Gets or sets the active control on the form.</summary>
        public Control? ActiveControl {
            get => adapter.GetNextControl (null, true);
            set => value?.Select ();
        }

        /// <summary>
        /// Gets or sets whether the form is an MDI container. Setting this true creates the client area
        /// that hosts MDI child forms (Majorsilence.Forms emulates MDI by hosting children inside the
        /// parent's client rather than as native OS windows).
        /// </summary>
        public bool IsMdiContainer {
            get => MdiClientControl != null;
            set {
                if (value == (MdiClientControl != null))
                    return;

                if (value) {
                    MdiClientControl = new MdiClient { Owner = this };
                    // Appended, i.e. left at the BACK of the z-order, which is where WinForms puts it
                    // and where it has to stay: index 0 is the front, and the front is painted last, so
                    // a front MDI client covers every sibling -- the menu, the toolbar, and any Fill'd
                    // panel (a docking host's whole UI) drawn beneath it.
                    //
                    // It gets the leftover space regardless of that position, because
                    // DockAndAnchorLayout defers the MDI client to the end of the dock pass rather than
                    // taking it in z-order.
                    Controls.Add (MdiClientControl);
                } else if (MdiClientControl != null) {
                    Controls.Remove (MdiClientControl);
                    MdiClientControl = null;
                }
            }
        }

        /// <summary>Gets or sets whether the form accepts data dragged onto it. Stub in
        /// Majorsilence.Forms -- matches Control.AllowDrop/DragEnter/DragDrop, which are also
        /// stubs (DoDragDrop always returns DragDropEffects.None, and the drag events never
        /// fire); provided so ported code compiles against Form the same as it does Control.</summary>
        public bool AllowDrop { get; set; }

        // Real handler storage rather than `{ add { } remove { } }`, which discarded the handler
        // outright: `form.DragEnter += h` looked wired up and h was thrown away, so an override of
        // OnDragEnter could never be reached even once a backend does raise these. Nothing raises them
        // yet (there is no OS drag source -- DoDragDrop returns None), so they are still "declared and
        // never fired", which is the documented stub shape; the difference is that the handler and the
        // overridable hook now exist to be called.
        private DragEventHandler? drag_enter;
        private DragEventHandler? drag_over;
        private DragEventHandler? drag_drop;

        /// <summary>Raised when a drag-and-drop operation enters the form. Never fires yet — see <see cref="AllowDrop"/>.</summary>
        public event DragEventHandler? DragEnter {
            add => drag_enter += value;
            remove => drag_enter -= value;
        }

        /// <summary>Raised as a drag-and-drop operation moves over the form. Never fires yet — see <see cref="AllowDrop"/>.</summary>
        public event DragEventHandler? DragOver {
            add => drag_over += value;
            remove => drag_over -= value;
        }

        /// <summary>Raised when a drag-and-drop operation completes over the form. Never fires yet — see <see cref="AllowDrop"/>.</summary>
        public event DragEventHandler? DragDrop {
            add => drag_drop += value;
            remove => drag_drop -= value;
        }

        /// <summary>Raises the <see cref="DragEnter"/> event.</summary>
        /// <remarks>
        /// Declared here because WinForms' Form inherits it from Control and ported code overrides it;
        /// WindowBase is not a Control. Matches Control.OnDragEnter.
        /// </remarks>
        protected virtual void OnDragEnter (DragEventArgs e) => drag_enter?.Invoke (this, e);

        /// <summary>Raises the <see cref="DragOver"/> event.</summary>
        /// <inheritdoc cref="OnDragEnter"/>
        protected virtual void OnDragOver (DragEventArgs e) => drag_over?.Invoke (this, e);

        /// <summary>Raises the <see cref="DragDrop"/> event.</summary>
        /// <inheritdoc cref="OnDragEnter"/>
        protected virtual void OnDragDrop (DragEventArgs e) => drag_drop?.Invoke (this, e);

        /// <summary>
        /// Gets or sets the MDI parent. Set this (and call <see cref="WindowBase.Show"/>) to host this form
        /// as a child inside <paramref name="value"/>'s MDI client area instead of as a top-level window.
        /// </summary>
        public Form? MdiParent {
            get => mdi_parent;
            set => mdi_parent = value;
        }

        /// <summary>Gets the MDI child forms hosted by this container, in creation order.</summary>
        public Form[] MdiChildren => MdiClientControl?.ChildForms.ToArray () ?? [];

        /// <summary>Gets whether this form is hosted as the child of an MDI container.</summary>
        public bool IsMdiChild => mdi_parent != null;

        /// <summary>
        /// Gets the form this form is parented to. Mirrors WinForms Control.ParentForm as it applies
        /// to a Form: the MDI parent when hosted as an MDI child, otherwise null (top-level window).
        /// </summary>
        public Form? ParentForm => mdi_parent;

        private Control? parent;

        /// <summary>
        /// Gets or sets the control this form is hosted in. Mirrors WinForms Control.Parent as it
        /// applies to a Form: while hosted as an MDI child the parent is the container's
        /// <see cref="MdiClient"/> (exactly as in WinForms, where an MDI child's Parent is the
        /// MdiClient control), and a top-level window reports null.
        /// </summary>
        /// <remarks>
        /// Assigning this really hosts the form, by the same route as
        /// <c>parent.Controls.Add (form)</c> -- the form is wrapped in a frame and composited into that
        /// control tree instead of owning a top-level window. Assigning null takes it back out.
        ///
        /// It used to only store the value. That made <c>form.Parent = panel</c> a silent no-op: the
        /// form stayed a separate top-level window, so an app that hosts forms inside a container by
        /// assigning Parent -- which is how WinForms code does it, and how a docking library puts a
        /// document into a pane -- got a stray empty window per form and nothing in the container.
        /// </remarks>
        public Control? Parent {
            // A panel-hosted form reports the control its frame was added to, not the frame itself --
            // the frame is an implementation detail, and WinForms code reaches for Parent to add
            // siblings alongside the hosted form ("Parent.Controls.Add (otherForm)").
            get => MdiHost?.Client ?? PanelHost?.Parent ?? parent;
            set {
                parent = value;

                // An MDI child's parent is owned by MdiParent, not settable through here -- WinForms
                // reports the MdiClient and ignores writes just the same.
                if (MdiHost != null)
                    return;

                if (value is null) {
                    // Detach without closing: taking a hosted form out of the tree is not the same as
                    // disposing it, and callers move forms between containers.
                    if (PanelHost is { } host) {
                        host.Parent?.Controls.Remove (host);
                        PanelHost = null;
                    }

                    return;
                }

                // Add handles the already-hosted case as a move, so this is safe to re-assign.
                if (PanelHost?.Parent != value)
                    value.Controls.Add (this);
            }
        }

        /// <summary>
        /// Gets or sets the preferred rounding of the window's corners. Stored for source parity with
        /// the WinForms property; corner rounding is decided by the platform backend (or the OS, under
        /// <see cref="UseSystemDecorations"/>) and this value is not applied to it.
        /// </summary>
        public FormCornerPreference FormCornerPreference {
            get => form_corner_preference;
            set {
                SourceGenerated.EnumValidator.Validate (value);
                form_corner_preference = value;
            }
        }
        private FormCornerPreference form_corner_preference = FormCornerPreference.Default;

        /// <summary>Gets or sets the bounds the form uses when maximized. Stored but not enforced in Majorsilence.Forms.</summary>
        public System.Drawing.Rectangle MaximizedBounds { get; set; }

        /// <summary>Gets or sets the base size used for autoscaling. Legacy WinForms designer property; stored no-op.</summary>
        public System.Drawing.Size AutoScaleBaseSize { get; set; }

        /// <summary>
        /// Gets the Win32 creation parameters. WinForms compatibility for the classic
        /// remove-close-button override pattern; the compat window ignores the values.
        /// </summary>
        protected virtual CreateParams CreateParams => new CreateParams ();

        /// <summary>
        /// Gets whether the window is shown without taking focus from whatever is currently active.
        /// Overridden to <see langword="true"/> by overlay windows — a drag preview, a translucent
        /// highlight, a notification — which must appear without stealing the caret from the form the
        /// user is working in. Honoured by <see cref="WindowBase.Show"/>.
        /// </summary>
        protected virtual bool ShowWithoutActivation => false;

        internal override bool ShowsActivated => !ShowWithoutActivation;

        /// <summary>Gets the active MDI child form, or null.</summary>
        public Form? ActiveMdiChild => MdiClientControl?.ActiveChild;

        /// <summary>Activates (brings to front and focuses) the specified MDI child form.</summary>
        public void ActivateMdiChild (Form? form) => MdiClientControl?.Activate (form);

        /// <summary>Arranges the MDI child forms in the given layout (cascade, tile, or arrange icons).</summary>
        public void LayoutMdi (MdiLayout value) => MdiClientControl?.LayoutMdi (value);

        // ── MDI internals ─────────────────────────────────────────────────────────

        /// <summary>Raises <see cref="MdiChildActivate"/> on the container.</summary>
        internal void RaiseMdiChildActivate () => mdi_child_activate?.Invoke (this, EventArgs.Empty);
        private EventHandler? mdi_child_activate;

        /// <summary>Lets a child react to its MDI frame being resized (re-lays out its client area).</summary>
        internal void RaiseMdiResize () => OnClientLayoutChanged ();

        // The designer-set client size, used to size a child's frame when it's first hosted. We read
        // Backend.Size (the size set via the Size setter) rather than Backend.ClientSize: a hosted child
        // never owns a realized OS window, so its backend's client size is unreliable before Show — on
        // some platforms it reports a default/monitor-sized value, which would make the child far too
        // wide. Backend.Size reflects exactly what was assigned. Falls back to a sensible default when
        // the form never set one. The MdiClient additionally clamps the frame to the parent's bounds.
        internal System.Drawing.Size InitialMdiContentSize {
            get {
                var s = Backend.Size;
                return s.Width > 0 && s.Height > 0 ? s : new System.Drawing.Size (300, 200);
            }
        }

        // Configures this form for being hosted as an MDI child: no self-drawn title bar/border (the frame
        // draws them) and no window-edge resize routing (the frame handles resize).
        private void PrepareAsMdiChild () => PrepareAsHostedChild ();

        // Configures this form for being drawn inside a frame in someone else's control tree: no
        // self-drawn title bar or border (the host owns any chrome there is) and no window-edge resize
        // routing. Shared by MDI children and Controls.Add(Form) hosting.
        internal void PrepareAsHostedChild ()
        {
            Resizeable = false;
            TitleBar.Visible = false;
            Style.Border.Width = 0;
        }

        // Takes down the OS window this form may already have shown, on becoming frame-hosted.
        //
        // Deliberately not Hide (): Hide would set visible = false and raise VisibleChanged, but the form
        // is not becoming invisible -- it is about to be painted inside its host, and callers that set
        // Visible = true before parenting expect it to still read true afterwards.
        //
        // Unconditional, and not guarded on `shown`: that flag records whether the Shown *event* has
        // been raised, which is not the same as owning a window right now. EnsureShownBookkeeping
        // returns early once `visible` is true, so a form shown again while already visible ends up
        // with a live OS window and shown == false -- exactly the case a guard here would skip, leaving
        // the window stranded on screen beside its host. Hiding a window that was never shown is
        // harmless.
        internal void HideOwnWindowForHosting () => Backend.Hide ();

        internal override bool TryShowHosted ()
        {
            // Told it is not top-level and not yet parented: it owns no OS window, so becoming visible is
            // bookkeeping until something hosts it. WinForms behaves the same -- a non-top-level form
            // with no parent simply is not on screen.
            if (!TopLevel && !IsFrameHosted) {
                visible = true;
                EnsureLoaded ();

                if (!shown) {
                    shown = true;
                    OnShown (EventArgs.Empty);
                }

                return true;
            }

            // Already sitting in a control tree via Controls.Add (form): Show() must not create an OS
            // window, it just makes the frame visible. Checked first because the frame is what the
            // caller actually parented the form into -- an MdiParent assignment left over from earlier
            // does not override where it currently lives.
            if (PanelHost is { } frame) {
                frame.Visible = true;
                visible = true;
                Application.OpenForms.Add (this);

                EnsureLoaded ();        // Load before the form is shown, matching WinForms.

                if (!shown) {
                    shown = true;
                    OnShown (EventArgs.Empty);
                }

                frame.Invalidate ();
                return true;
            }

            if (mdi_parent?.MdiClientControl is not { } client)
                return false;

            PrepareAsMdiChild ();
            client.AddChild (this);
            visible = true;
            Application.OpenForms.Add (this);

            EnsureLoaded ();            // Load before the child is shown, matching WinForms.

            if (!shown) {
                shown = true;
                OnShown (EventArgs.Empty);
            }

            return true;
        }

        /// <inheritdoc/>
        public override void Invalidate ()
        {
            // A hosted form has no surface of its own; repainting means dirtying the frame that
            // composites it, so the request travels up that control tree instead.
            if (MdiHost != null)
                MdiHost.Invalidate ();
            else if (PanelHost != null)
                PanelHost.Invalidate ();
            else
                base.Invalidate ();
        }

        /// <summary>Gets or sets the form that owns this form.</summary>
        public Form? Owner { get; set; }

        private List<Form>? _ownedForms;

        /// <summary>Gets the array of forms that are owned by this form.</summary>
        public Form[] OwnedForms => _ownedForms?.ToArray () ?? [];

        /// <summary>Adds an owned form to this form.</summary>
        public void AddOwnedForm (Form form)
        {
            _ownedForms ??= [];
            if (!_ownedForms.Contains (form))
                _ownedForms.Add (form);
            form.Owner = this;
        }

        /// <summary>Removes an owned form from this form.</summary>
        public void RemoveOwnedForm (Form form)
        {
            _ownedForms?.Remove (form);
            if (form.Owner == this)
                form.Owner = null;
        }

        /// <summary>Gets or sets the MenuStrip that is the main menu for the form.</summary>
        public MenuStrip? MainMenuStrip { get; set; }

        /// <summary>Gets or sets whether the form is a top-level window.</summary>
        /// <remarks>
        /// Setting this false is how WinForms code says "stop owning an OS window" before parenting a
        /// form into a control tree — the <c>form.TopLevel = false; panel.Controls.Add (form)</c> idiom,
        /// and what a docking library does on every dock-state change. Stored and never acted on, the
        /// form kept its own window: re-docking a floated document left its old window behind as a large
        /// blank rectangle over the application.
        /// </remarks>
        public bool TopLevel {
            get => top_level;
            set {
                if (top_level == value)
                    return;

                top_level = value;

                if (!value)
                    Backend.Hide ();        // composited by whatever hosts it from here on
                else if (visible && !IsFrameHosted)
                    Backend.Show ();
            }
        }

        private bool top_level = true;

        /// <summary>Gets or sets the start position of the form when it is first shown.</summary>
        public new FormStartPosition StartPosition {
            get => start_position;
            set {
                SourceGenerated.EnumValidator.Validate (value);
                start_position = value;
            }
        }
        private FormStartPosition start_position = FormStartPosition.WindowsDefaultLocation;

        /// <summary>Gets or sets the desktop bounds of the form.</summary>
        public System.Drawing.Rectangle DesktopBounds {
            get => new System.Drawing.Rectangle (Location.X, Location.Y, Size.Width, Size.Height);
            set { Location = new System.Drawing.Point (value.X, value.Y); Size = new System.Drawing.Size (value.Width, value.Height); }
        }

        /// <summary>Gets or sets the desktop location of the form.</summary>
        public System.Drawing.Point DesktopLocation {
            get => new System.Drawing.Point (Location.X, Location.Y);
            set => Location = value;
        }

        /// <summary>Activates the form and gives it focus.</summary>
        /// <remarks>
        /// Was an empty stub, so <c>form.Activate ()</c> silently did nothing -- the WinForms idiom for
        /// bringing an already-open window to the user rather than opening a second one. Routed through
        /// the same path as <see cref="Focus"/>, which keeps a hosted form from acquiring a stray OS
        /// window of its own.
        /// </remarks>
        public void Activate () => Focus ();

        /// <summary>Activates the form. Mirrors WinForms Control.Select as it applies to a Form.</summary>
        public void Select () => BringToFront ();

        /// <summary>Centers the form in its parent or on screen.</summary>
        public void CenterToScreen ()
        {
            if (StartPosition != FormStartPosition.Manual)
                StartPosition = FormStartPosition.CenterScreen;
        }

        /// <summary>Centers the form within its owner form, or on the screen if there is no owner.</summary>
        public void CenterToParent ()
        {
            if (Owner != null) {
                var ob = Owner.Bounds;
                var b = Bounds;
                Location = new System.Drawing.Point (ob.Left + (ob.Width - b.Width) / 2, ob.Top + (ob.Height - b.Height) / 2);
            } else {
                CenterToScreen ();
            }
        }

        /// <summary>Brings the form to the front of the z-order.</summary>
        public void BringToFront ()
        {
            // For a hosted form "front" means front of the frame's sibling z-order, not of the desktop.
            if (MdiHost != null)
                MdiHost.Client.Activate (this);
            else if (PanelHost != null)
                PanelHost.BringToFront ();
            else if (visible && TopLevel)
                Backend.Activate ();   // see Focus(): activating strands a window the form does not own
        }

        /// <summary>Sends the form to the back of the z-order.</summary>
        /// <remarks>
        /// The counterpart of <see cref="BringToFront"/>, whose absence was an asymmetry rather than a
        /// decision: a form could be raised but not lowered. Hosted forms move within their frame's
        /// sibling order; a top-level window has no cross-application "send to back" the backend
        /// exposes, so that case is a no-op rather than a pretence.
        /// </remarks>
        public void SendToBack ()
        {
            if (PanelHost != null)
                PanelHost.SendToBack ();
        }

        /// <summary>Gets the bounds of the form when it is not minimized or maximized.</summary>
        public System.Drawing.Rectangle RestoreBounds => Bounds;

        /// <summary>Gets or sets whether the form is displayed in the Windows taskbar.</summary>
        public bool ControlBox { get; set; } = true;

        /// <summary>Gets or sets the help button visibility in the title bar. Stub in Majorsilence.Forms.</summary>
        public bool HelpButton { get; set; }

        /// <summary>Gets or sets whether to display the icon in the title bar. Stub in Majorsilence.Forms.</summary>
        public bool ShowIcon { get; set; } = true;

        /// <summary>Raises the Load event.</summary>
        /// <remarks>
        /// <c>protected virtual</c>, as in WinForms. It was public and non-virtual, so the standard
        /// <c>protected override void OnLoad</c> a ported form is built around failed to compile (CS0506)
        /// — the derived form could only reach its own startup work by subscribing to its own event.
        /// </remarks>
        protected virtual void OnLoad (EventArgs e) => Load?.Invoke (this, e);

        /// <summary>Gets whether the form is displayed as a modal dialog.</summary>
        public bool Modal { get; private set; }

        /// <summary>Gets or sets the description of the form as an accessible object. Stub in Majorsilence.Forms.</summary>
        public string? AccessibleDescription { get; set; }

        /// <summary>Gets or sets the name of the form as an accessible object. Stub in Majorsilence.Forms.</summary>
        public string? AccessibleName { get; set; }


        /// <summary>Returns the currently focused control within the form, or null.</summary>
        public Control? GetFocusedControl () => Controls.GetAllControls ().FirstOrDefault (c => c.Focused);

        /// <summary>Sets the form position to the specified screen coordinates.</summary>
        public void SetDesktopBounds (int x, int y, int width, int height) { Location = new System.Drawing.Point (x, y); Size = new System.Drawing.Size (width, height); }

        /// <summary>Sets the location of the form in screen coordinates.</summary>
        public void SetDesktopLocation (int x, int y) { Location = new System.Drawing.Point (x, y); }

        private enum WindowElement
        {
            Client,
            TopBorder,
            RightBorder,
            BottomBorder,
            LeftBorder,
            TopLeftCorner,
            TopRightCorner,
            BottomLeftCorner,
            BottomRightCorner
        }
    }
}
