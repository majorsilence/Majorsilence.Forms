using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using SkiaSharp;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a top-level window to display to the user.
    /// </summary>
    public class Form : WindowBase, ICloseable
    {
        // If the border is only 1 pixel it's too hard to resize, so we may steal some pixels from the client area
        private const int MINIMUM_RESIZE_PIXELS = 4;

        private Avalonia.Controls.Window? dialog_parent;
        private DialogResult dialog_result = DialogResult.None;
        internal TaskCompletionSource<DialogResult>? dialog_task;
        private System.Drawing.Size minimum_size;
        private System.Drawing.Size maximum_size;

        private bool show_focus_cues;
        private string text = string.Empty;
        private bool use_system_decorations;

        /// <summary>
        /// Initializes a new instance of the Form class.
        /// </summary>
        public Form ()
        {
            var host = new ModernFormsWindowHost (this);
            InitWindow (host);

            TitleBar = Controls.AddImplicitControl (new FormTitleBar ());

            Resizeable = true;
            host.SystemDecorations = WindowDecorations.None;
            host.ExtendClientAreaToDecorationsHint = true;

            host.Closing += (s, e) => {
                var args = new CancelEventArgs ();
                OnClosing (args);
                e.Cancel = args.Cancel;
            };

            // On macOS defer to the native window chrome (traffic-light buttons, native drag/resize).
            if (OperatingSystem.IsMacOS ())
                UseSystemDecorations = true;

            host.Width = DefaultSize.Width;
            host.Height = DefaultSize.Height;
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
        public void BeginMoveDrag () => AvWindow.StartMoveDrag ();

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
            base.Close ();

            // If close was cancelled by OnClosing, don't proceed with dialog cleanup
            if (Application.OpenForms.Contains (this))
                return;

            if (dialog_parent is not null) {
                dialog_parent.IsEnabled = true;
                dialog_parent.Activate ();
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
        public event EventHandler<FormClosedEventArgs>? FormClosed;

        /// <summary>Raised when the form is first shown (WinForms compatibility alias; raised together with Shown).</summary>
        public event EventHandler? Load;

        /// <inheritdoc/>
        protected override void OnShown (EventArgs e)
        {
            Load?.Invoke (this, e);
            base.OnShown (e);
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

        /// <summary>Gets or sets the icon for the form.</summary>
        public SKBitmap? Image {
            get => TitleBar.Image;
            set {
                TitleBar.Image = value;

                if (value is null) {
                    AvWindow.Icon = null;
                } else {
                    using var ms = new System.IO.MemoryStream ();
                    value.Encode (ms, SKEncodedImageFormat.Png, 100);
                    ms.Seek (0, System.IO.SeekOrigin.Begin);
                    AvWindow.Icon = new Avalonia.Controls.WindowIcon (ms);
                }
            }
        }

        /// <summary>Gets or sets the unscaled location of the control.</summary>
        public new System.Drawing.Point Location {
            get => new System.Drawing.Point (AvWindow.Position.X, AvWindow.Position.Y);
            set {
                if (new System.Drawing.Point (AvWindow.Position.X, AvWindow.Position.Y) != value)
                    AvWindow.Position = new PixelPoint (value.X, value.Y);
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
                case WindowElement.TopBorder:       AvWindow.StartResizeDrag (WindowEdge.North);     return true;
                case WindowElement.RightBorder:     AvWindow.StartResizeDrag (WindowEdge.East);      return true;
                case WindowElement.BottomBorder:    AvWindow.StartResizeDrag (WindowEdge.South);     return true;
                case WindowElement.LeftBorder:      AvWindow.StartResizeDrag (WindowEdge.West);      return true;
                case WindowElement.TopLeftCorner:   AvWindow.StartResizeDrag (WindowEdge.NorthWest); return true;
                case WindowElement.TopRightCorner:  AvWindow.StartResizeDrag (WindowEdge.NorthEast); return true;
                case WindowElement.BottomLeftCorner:  AvWindow.StartResizeDrag (WindowEdge.SouthWest); return true;
                case WindowElement.BottomRightCorner: AvWindow.StartResizeDrag (WindowEdge.SouthEast); return true;
            }

            return false;
        }

        internal override bool HandleMouseMove (int x, int y)
        {
            var element = GetElementAtLocation (x, y);

            switch (element) {
                case WindowElement.TopBorder:         AvWindow.Cursor = Cursors.TopSide.cursor;         return true;
                case WindowElement.RightBorder:       AvWindow.Cursor = Cursors.RightSide.cursor;       return true;
                case WindowElement.BottomBorder:      AvWindow.Cursor = Cursors.BottomSide.cursor;      return true;
                case WindowElement.LeftBorder:        AvWindow.Cursor = Cursors.LeftSide.cursor;        return true;
                case WindowElement.TopLeftCorner:     AvWindow.Cursor = Cursors.TopLeftCorner.cursor;   return true;
                case WindowElement.TopRightCorner:    AvWindow.Cursor = Cursors.TopRightCorner.cursor;  return true;
                case WindowElement.BottomLeftCorner:  AvWindow.Cursor = Cursors.BottomLeftCorner.cursor; return true;
                case WindowElement.BottomRightCorner: AvWindow.Cursor = Cursors.BottomRightCorner.cursor; return true;
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
            AvWindow.MinWidth  = minimum_size.IsEmpty ? 0 : minimum_size.Width;
            AvWindow.MinHeight = minimum_size.IsEmpty ? 0 : minimum_size.Height;
            AvWindow.MaxWidth  = maximum_size.IsEmpty ? double.PositiveInfinity : maximum_size.Width;
            AvWindow.MaxHeight = maximum_size.IsEmpty ? double.PositiveInfinity : maximum_size.Height;
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

            var result = RunModal (ShowDialog (parent));
            FormClosed?.Invoke (this, new FormClosedEventArgs ());
            return result;
        }

        // Blocks the current call while running a nested Avalonia dispatcher frame so the
        // modal dialog can receive and handle input events without deadlocking the UI thread.
        internal static T RunModal<T> (Task<T> modalTask)
        {
            var frame = new DispatcherFrame ();
            modalTask.ContinueWith (_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame (frame);
            return modalTask.GetAwaiter ().GetResult ();
        }

        /// <summary>Called when the theme changes.</summary>
        protected internal virtual void OnThemeChanged (EventArgs e)
        {
            foreach (var control in Controls.GetAllControls ())
                control.OnThemeChanged (e);
        }

        internal override void SetWindowStartupLocation (Avalonia.Controls.Window? owner = null)
        {
            var scaling = Scaling;

            var rect = new PixelRect (
                PixelPoint.Origin,
                PixelSize.FromSize (AvWindow.ClientSize, scaling));

            if (StartPosition == FormStartPosition.CenterScreen) {
                var ownerPos = owner is not null ? owner.Position : AvWindow.Position;
                var screen = Screens.ScreenFromPoint (ownerPos);

                if (screen != null) {
                    var wa = screen.WorkingArea;
                    var position = new System.Drawing.Point (
                        wa.X + (wa.Width - rect.Width) / 2,
                        wa.Y + (wa.Height - rect.Height) / 2);

                    // Ensure we don't position the titlebar offscreen
                    position.X = Math.Max (position.X, wa.X);
                    position.Y = Math.Max (position.Y, wa.Y);

                    Location = position;
                }
            } else if (StartPosition == FormStartPosition.CenterParent) {
                if (owner != null) {
                    var ownerRect = new PixelRect (
                        owner.Position,
                        PixelSize.FromSize (owner.ClientSize, scaling));

                    var x = ownerRect.X + (ownerRect.Width - rect.Width) / 2;
                    var y = ownerRect.Y + (ownerRect.Height - rect.Height) / 2;
                    Location = new System.Drawing.Point (x, y);
                }
            }
        }

        /// <summary>Displays the window to the user modally, preventing interaction with other windows until closed.</summary>
        public Task<DialogResult> ShowDialog (Form parent)
        {
            dialog_task = new TaskCompletionSource<DialogResult> ();

            if (dialog_result != DialogResult.None) {
                dialog_task.SetResult (dialog_result);
                return dialog_task.Task;
            }

            dialog_parent = parent.AvWindow;

            ShowDialog (parent.AvWindow);

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

        /// <summary>Gets or sets the unscaled size of the window.</summary>
        public new System.Drawing.Size Size {
            get => new System.Drawing.Size ((int)AvWindow.ClientSize.Width, (int)AvWindow.ClientSize.Height);
            set {
                AvWindow.Width = value.Width;
                AvWindow.Height = value.Height;
            }
        }

        /// <summary>Gets the currently active form (the most recently focused open form).</summary>
        public static Form? ActiveForm => Application.OpenForms.LastOrDefault ();

        /// <summary>Gets or sets the client area size (equivalent to Size for Modern.Forms).</summary>
        public System.Drawing.Size ClientSize {
            get => Size;
            set => Size = value;
        }

        /// <summary>Gets or sets the automatic scaling mode. No-op in Modern.Forms.</summary>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Font;

        /// <summary>Gets or sets the auto-scale dimensions. No-op in Modern.Forms.</summary>
        public System.Drawing.SizeF AutoScaleDimensions { get; set; }

        /// <summary>Gets or sets the binding context. No-op in Modern.Forms.</summary>
        public object? BindingContext { get; set; }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets the text for the form title bar.</summary>
        public string Text {
            get => text;
            set {
                if (text != value) {
                    text = value;
                    AvWindow.Title = text;
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
                    TitleBar.Visible = !use_system_decorations;
                    Style.Border.Width = use_system_decorations ? 0 : 1;
                    AvWindow.SystemDecorations = value ? WindowDecorations.Full : WindowDecorations.None;
                    AvWindow.ExtendClientAreaToDecorationsHint = !value;
                }
            }
        }

        /// <summary>Gets or sets the state of the form (normal/minimized/maximized).</summary>
        public FormWindowState WindowState {
            get => (FormWindowState)(int)AvWindow.WindowState;
            set => AvWindow.WindowState = (Avalonia.Controls.WindowState)(int)value;
        }

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
