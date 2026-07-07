using System.ComponentModel;
using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a top-level window to display to the user.
    /// </summary>
    public class Form : WindowBase, IWin32Window
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
            adapter.MouseDown += (s, e) => MouseDown?.Invoke (this, e);
            adapter.MouseMove += (s, e) => MouseMove?.Invoke (this, e);
            adapter.MouseLeave += (s, e) => Leave?.Invoke (this, e);
        }

        /// <summary>Raised when a mouse button is pressed over the form's own surface.</summary>
        public event EventHandler<MouseEventArgs>? MouseDown;

        /// <summary>Raised when the mouse moves over the form's own surface.</summary>
        public event EventHandler<MouseEventArgs>? MouseMove;

        /// <summary>Raised when the mouse leaves the form's own surface.</summary>
        public event EventHandler? Leave;

        /// <summary>Gets or sets whether the form causes validation to be performed on any controls that require validation when it receives focus. Matches Control.CausesValidation.</summary>
        public bool CausesValidation { get; set; } = true;

        /// <summary>
        /// Raised when the form is validating. Stub in Majorsilence.Forms -- matches Control.
        /// Validating's shape so ported code compiles, but there is no automatic focus-change
        /// validation pipeline to fire it yet (add/remove bodies avoid a "never used" warning
        /// for an event this type never raises itself).
        /// </summary>
        public event System.ComponentModel.CancelEventHandler? Validating { add { } remove { } }

        /// <summary>Attempts to set focus to the form. Matches Control.Focus's shape (returns whether the focus request succeeded).</summary>
        public bool Focus ()
        {
            Backend.Activate ();
            return true;
        }

        /// <summary>Gets or sets the button that is activated when Enter is pressed.</summary>
        public Button? AcceptButton { get; set; }

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
        public Button? CancelButton { get; set; }

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
                OnBackendClosed ();               // raises Closed
                FormClosed?.Invoke (this, new FormClosedEventArgs ());
                return;
            }

            base.Close ();

            // If close was cancelled by OnClosing, don't proceed with dialog cleanup
            if (Application.OpenForms.Contains (this))
                return;

            if (dialog_parent is not null) {
                dialog_parent.Backend.Enabled = true;
                dialog_parent.Backend.Activate ();
                dialog_parent = null;
            }

            if (dialog_task is not null) {
                var task = dialog_task;
                dialog_task = null;
                task.SetResult (dialog_result);
            }
        }

        /// <summary>Raised before the form is closed, allowing close to be programatically canceled.</summary>
        public event EventHandler<CancelEventArgs>? Closing;

        /// <summary>Raised before the form is closed (WinForms compatibility alias for Closing).</summary>
        public event EventHandler<FormClosingEventArgs>? FormClosing;

        /// <summary>Raised after the form is closed.</summary>
        public event FormClosedEventHandler? FormClosed;


        /// <summary>Raised when the form is first shown (WinForms compatibility alias; raised together with Shown).</summary>
        public event EventHandler? Load;

        /// <summary>Raised when the user begins to resize the form. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? ResizeBegin { add { } remove { } }

        /// <summary>Raised when the user finishes resizing the form. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? ResizeEnd { add { } remove { } }

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

        /// <summary>Raised when the DPI setting for the form changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler? DpiChanged { add { } remove { } }

        /// <summary>Raised when the input language changes. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<InputLanguageChangedEventArgs>? InputLanguageChanged { add { } remove { } }

        /// <summary>Raised when the input language is changing. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<InputLanguageChangingEventArgs>? InputLanguageChanging { add { } remove { } }

        /// <summary>Raised when the form is first displayed to the user.</summary>
        public new event EventHandler? Shown {
            add => base.Shown += value;
            remove => base.Shown -= value;
        }

        /// <inheritdoc/>
        protected override void OnShown (EventArgs e)
        {
            Load?.Invoke (this, e);
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
        /// the parent's MDI client area; otherwise it's the window's screen position.
        /// </summary>
        public new System.Drawing.Point Location {
            get => MdiHost != null ? new System.Drawing.Point (MdiHost.Left, MdiHost.Top) : Backend.Location;
            set {
                if (MdiHost != null)
                    MdiHost.Client.MoveChild (MdiHost, value.X, value.Y);
                else if (Backend.Location != value)
                    Backend.Location = value;
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
        public virtual void OnClosing (CancelEventArgs e)
        {
            Closing?.Invoke (this, e);

            var form_closing_args = new FormClosingEventArgs { Cancel = e.Cancel };
            FormClosing?.Invoke (this, form_closing_args);

            if (form_closing_args.Cancel)
                e.Cancel = true;
        }

        /// <summary>Displays the window modally using the first open form as the parent.</summary>
        public DialogResult ShowDialog ()
        {
            var parent = Application.OpenForms.FirstOrDefault (f => f != this);

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
            FormClosed?.Invoke (this, new FormClosedEventArgs ());
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
        /// area inside the host frame; otherwise it's the window's client size.
        /// </summary>
        public new System.Drawing.Size Size {
            get => MdiHost != null ? MdiHost.ContentSize : Backend.ClientSize;
            set {
                if (MdiHost != null)
                    MdiHost.SetContentSize (value);
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
            set => Size = value;
        }

        /// <summary>Gets or sets the automatic scaling mode. No-op in Majorsilence.Forms.</summary>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Font;

        /// <summary>Gets or sets the auto-scale dimensions. No-op in Majorsilence.Forms.</summary>
        public System.Drawing.SizeF AutoScaleDimensions { get; set; }

        /// <summary>Gets or sets how the form performs implicit validation when focus leaves a child control.</summary>
        public AutoValidate AutoValidate { get; set; } = AutoValidate.EnablePreventFocusChange;

        /// <summary>Validates all selectable child controls. Always returns true (stub).</summary>
        public bool ValidateChildren () => true;

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

        /// <summary>Gets or sets the border style of the form (stub — actual decoration is controlled by UseSystemDecorations).</summary>
        public FormBorderStyle FormBorderStyle {
            get => form_border_style;
            set {
                SourceGenerated.EnumValidator.Validate (value);
                form_border_style = value;
            }
        }
        private FormBorderStyle form_border_style = FormBorderStyle.Sizable;

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

        /// <summary>Gets or sets the text for the form title bar.</summary>
        public string Text {
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
                    Style.Border.Width = use_system_decorations ? 0 : 1;
                    Backend.SetSystemDecorations (value);
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
            var extend = use_system_decorations && extends_content_into_title_bar;

            // The custom title bar is shown for fully-custom chrome, or when merged into a native bar.
            TitleBar.Visible = !use_system_decorations || extend;
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
                    Controls.Add (MdiClientControl);
                    // Dock layout processes z-order front-to-back (index 0 first; see
                    // DockAndAnchorLayout.LayoutDockedControls), and Controls.Add appends at the
                    // back. Left there, the MDI client's Dock=Fill gets computed against the whole
                    // display rectangle before any sibling menu/toolbar/status strip -- typically
                    // already present from InitializeComponent -- has claimed its own slice, so it
                    // visually covers them entirely. BringToFront moves it to the front (index 0),
                    // making it dock-process last and correctly receive only the leftover space.
                    MdiClientControl.BringToFront ();
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

        /// <summary>Raised when a drag-and-drop operation enters the form. Stub in Majorsilence.Forms (never fires).</summary>
        public event EventHandler<DragEventArgs>? DragEnter { add { } remove { } }

        /// <summary>Raised when a drag-and-drop operation completes over the form. Stub in Majorsilence.Forms (never fires).</summary>
        public event EventHandler<DragEventArgs>? DragDrop { add { } remove { } }

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

        /// <summary>Gets or sets the bounds the form uses when maximized. Stored but not enforced in Majorsilence.Forms.</summary>
        public System.Drawing.Rectangle MaximizedBounds { get; set; }

        /// <summary>Gets or sets the base size used for autoscaling. Legacy WinForms designer property; stored no-op.</summary>
        public System.Drawing.Size AutoScaleBaseSize { get; set; }

        /// <summary>
        /// Gets the Win32 creation parameters. WinForms compatibility for the classic
        /// remove-close-button override pattern; the compat window ignores the values.
        /// </summary>
        protected virtual CreateParams CreateParams => new CreateParams ();

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
        private void PrepareAsMdiChild ()
        {
            Resizeable = false;
            TitleBar.Visible = false;
            Style.Border.Width = 0;
        }

        internal override bool TryShowHosted ()
        {
            if (mdi_parent?.MdiClientControl is not { } client)
                return false;

            PrepareAsMdiChild ();
            client.AddChild (this);
            visible = true;
            Application.OpenForms.Add (this);

            if (!shown) {
                shown = true;
                OnShown (EventArgs.Empty);
            }

            return true;
        }

        /// <inheritdoc/>
        public override void Invalidate ()
        {
            if (MdiHost != null)
                MdiHost.Invalidate ();
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
        public bool TopLevel { get; set; } = true;

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

        /// <summary>Activates the form and gives it focus. No-op stub in Majorsilence.Forms.</summary>
        public void Activate () { }

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
        public void BringToFront () => Backend.Activate ();

        /// <summary>Gets the bounds of the form when it is not minimized or maximized.</summary>
        public System.Drawing.Rectangle RestoreBounds => Bounds;

        /// <summary>Gets or sets whether the form is displayed in the Windows taskbar.</summary>
        public bool ControlBox { get; set; } = true;

        /// <summary>Gets or sets the help button visibility in the title bar. Stub in Majorsilence.Forms.</summary>
        public bool HelpButton { get; set; }

        /// <summary>Gets or sets whether to display the icon in the title bar. Stub in Majorsilence.Forms.</summary>
        public bool ShowIcon { get; set; } = true;

        /// <summary>Raises the Load event on next show. Stub in Majorsilence.Forms.</summary>
        public void OnLoad (EventArgs e) => Load?.Invoke (this, e);

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
