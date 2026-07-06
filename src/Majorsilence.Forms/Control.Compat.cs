using System;
using System.Drawing;
using Majorsilence.Forms.Backends;
using SkiaSharp;

namespace Majorsilence.Forms
{
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
        public bool DoubleBuffered { get; set; } = true;

        /// <summary>
        /// Forces the control to invalidate and immediately repaint.
        /// </summary>
        public void Refresh () => Invalidate ();

        /// <summary>
        /// Sets input focus to the control. Returns true if focus was successfully set.
        /// </summary>
        public bool Focus ()
        {
            Select ();
            return Focused;
        }

        /// <summary>
        /// Computes the location of the specified screen point into client coordinates.
        /// </summary>
        public Point PointToClient (Point point)
        {
            var origin = PointToScreen (Point.Empty);
            return new Point (point.X - origin.X, point.Y - origin.Y);
        }

        /// <summary>Converts a Rectangle from client to screen coordinates.</summary>
        public Rectangle RectangleToScreen (Rectangle rect)
        {
            var origin = PointToScreen (Point.Empty);
            return new Rectangle (rect.X + origin.X, rect.Y + origin.Y, rect.Width, rect.Height);
        }

        /// <summary>Converts a Rectangle from screen to client coordinates.</summary>
        public Rectangle RectangleToClient (Rectangle rect)
        {
            var origin = PointToScreen (Point.Empty);
            return new Rectangle (rect.X - origin.X, rect.Y - origin.Y, rect.Width, rect.Height);
        }

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
        /// Gets or sets the background image displayed in the control.
        /// Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
        public Majorsilence.Forms.Drawing.Image? BackgroundImage {
            get => background_image;
            set {
                if (background_image != value) {
                    background_image = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the layout used to position the <see cref="BackgroundImage"/>.
        /// </summary>
        public ImageLayout BackgroundImageLayout {
            get => background_image_layout;
            set {
                if (background_image_layout != value) {
                    background_image_layout = value;
                    Invalidate ();
                }
            }
        }

        private void PaintBackgroundImage (PaintEventArgs e)
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
            ArgumentNullException.ThrowIfNull (action);
            Platform.Backend.Post (action);
        }

        /// <summary>
        /// Executes the specified delegate asynchronously on the thread that owns the control.
        /// WinForms compat overload accepting Delegate.
        /// </summary>
        public IAsyncResult BeginInvoke (Delegate method)
        {
            if (method is Action a) BeginInvoke (a);
            else if (method is MethodInvoker mi) BeginInvoke ((Action)(() => mi ()));
            else Platform.Backend.Post (() => method.DynamicInvoke ());
            return new System.Threading.Tasks.Task (() => { });
        }

        /// <summary>
        /// Executes the specified delegate asynchronously with args on the thread that owns the control.
        /// </summary>
        public IAsyncResult BeginInvoke (Delegate method, params object?[] args)
        {
            Platform.Backend.Post (() => method.DynamicInvoke (args));
            return new System.Threading.Tasks.Task (() => { });
        }

        /// <summary>Waits for a pending asynchronous call to complete. Stub in Majorsilence.Forms.</summary>
        public object? EndInvoke (IAsyncResult asyncResult) => null;

        /// <summary>
        /// Executes the specified delegate synchronously on the thread that owns the control.
        /// </summary>
        public void Invoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            Platform.Backend.Invoke (action);
        }

        /// <summary>
        /// Executes the specified MethodInvoker delegate synchronously on the thread that owns the control.
        /// </summary>
        public void Invoke (MethodInvoker method) => Invoke ((Action)(() => method ()));

        /// <summary>
        /// Executes the specified Delegate synchronously on the UI thread and returns the result.
        /// WinForms compat overload.
        /// </summary>
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
            ArgumentNullException.ThrowIfNull (func);
            return Platform.Backend.Invoke (func);
        }

        /// <summary>Gets or sets whether a wait cursor is shown for this control and its children.</summary>
        public bool UseWaitCursor { get; set; }

        /// <summary>Always returns true in Majorsilence.Forms — the control is always created.</summary>
        public bool IsHandleCreated => true;

        /// <summary>Always returns false in Majorsilence.Forms — right-to-left mirroring is not supported.</summary>
        public bool IsMirrored => false;

        /// <summary>Returns a platform handle (always IntPtr.Zero in Majorsilence.Forms).</summary>
        public IntPtr Handle => IntPtr.Zero;

        /// <summary>Forces the creation of the control handle. Stub in Majorsilence.Forms — handle is always ready.</summary>
        public void CreateHandle () { }

        /// <summary>Applies updated ControlStyles flags. Stub in Majorsilence.Forms — styles are applied immediately.</summary>
        public void UpdateStyles () { }

        /// <summary>Gets whether this control or one of its descendents has keyboard focus.</summary>
        public bool ContainsFocus => Focused || Controls.Any (c => c.ContainsFocus);

        /// <summary>Gets whether the control can receive focus.</summary>
        public bool CanFocus => Visible && Enabled && CanSelect;

        /// <summary>Gets whether the caller must use Invoke to call the control (always false on UI thread).</summary>
        public bool InvokeRequired => !Platform.Backend.CheckAccess ();

        /// <summary>Gets or sets the right-to-left layout mode. Stub in Majorsilence.Forms.</summary>
        public RightToLeft RightToLeft { get; set; } = RightToLeft.No;

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

        /// <summary>Gets or sets whether the control accepts drag-and-drop data. Stub in Majorsilence.Forms.</summary>
        public bool AllowDrop { get; set; }

        /// <summary>Causes all validation in the control hierarchy to occur. Always returns true in Majorsilence.Forms.</summary>
        public bool Validate () => true;

        /// <summary>Causes all validation in the control hierarchy to occur. Always returns true in Majorsilence.Forms.</summary>
        public bool Validate (bool checkAutoValidate) => true;

        /// <summary>Gets or sets whether user input in the control causes validation to occur. Stub in Majorsilence.Forms.</summary>
        public bool CausesValidation { get; set; } = true;

        /// <summary>Gets or sets the Input Method Editor (IME) mode. Stub in Majorsilence.Forms.</summary>
        public ImeMode ImeMode { get; set; } = ImeMode.NoControl;

        /// <summary>Begins a drag-and-drop operation. Stub in Majorsilence.Forms — always returns None.</summary>
        public DragDropEffects DoDragDrop (object data, DragDropEffects allowedEffects) => DragDropEffects.None;

        /// <summary>Forces the control and its children to repaint.</summary>
        public void Update () => Invalidate ();

        /// <summary>Scales the control by the specified horizontal and vertical scaling factors. Stub in Majorsilence.Forms.</summary>
        public void Scale (float dx, float dy) { }

        /// <summary>Initiates scrolling the display of the control by the specified number of pixels.</summary>
        public void ScrollControlIntoView (Control? activeControl) { }

        /// <summary>Returns the child control at the specified client coordinates, or null.</summary>
        public Control? GetChildAtPoint (System.Drawing.Point pt)
            => Controls.GetAllControls ().LastOrDefault (c => c.Visible && c.Bounds.Contains (pt));

        /// <summary>Gets the current mouse cursor position in screen coordinates (alias for Cursor.Position).</summary>
        public static System.Drawing.Point MousePosition => Cursor.Position;

        /// <summary>Resets the Text property to its default value (empty string).</summary>
        public virtual void ResetText () => Text = string.Empty;

        /// <summary>Resets the Font property to null (theme default).</summary>
        public virtual void ResetFont () => Font = null;


        /// <summary>Gets or sets the accessible role. Stub in Majorsilence.Forms.</summary>
        public AccessibleRole AccessibleRole { get; set; } = AccessibleRole.Default;

        /// <summary>Gets or sets the ContextMenuStrip associated with this control (WinForms compat alias for ContextMenu).</summary>
        public ContextMenuStrip? ContextMenuStrip {
            get => ContextMenu as ContextMenuStrip;
            set => ContextMenu = value;
        }

        private ControlBindingsCollection? _dataBindings;

        /// <summary>Gets the data bindings for this control. Stub in Majorsilence.Forms — bindings are not evaluated.</summary>
        public ControlBindingsCollection DataBindings => _dataBindings ??= new ControlBindingsCollection (this);

        private BindingContext? binding_context;

        /// <summary>
        /// Gets or sets the BindingContext for the control. Mirrors WinForms Control.BindingContext:
        /// inherited from the parent chain when not set locally, so controls on one form share
        /// binding-manager position state.
        /// </summary>
        public virtual BindingContext BindingContext {
            get => binding_context ?? Parent?.BindingContext ?? (binding_context = new BindingContext ());
            set => binding_context = value;
        }

        /// <summary>Gets the Form that the control is on, if any.</summary>
        public Form? ParentForm => FindForm ();

        /// <summary>Gets the top-level control in the parent chain of this control.</summary>
        public Control? TopLevelControl {
            get {
                var ctrl = (Control)this;
                while (ctrl.Parent != null) ctrl = ctrl.Parent;
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
        public AccessibleObject AccessibilityObject => _accessibilityObject ??= CreateAccessibilityInstance ();

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

        /// <summary>
        /// Renders the control and its children to a Majorsilence.Forms.Drawing.Bitmap.
        /// Stub in Majorsilence.Forms — creates an empty bitmap of the control's size.
        /// </summary>
#pragma warning disable CA1416
        public void DrawToBitmap (Majorsilence.Forms.Drawing.Bitmap bitmap, Rectangle targetBounds)
        {
        }
#pragma warning restore CA1416

        /// <summary>
        /// Determines whether a key is an input key (versus a navigation key processed before KeyDown).
        /// Override to accept additional keys. Majorsilence.Forms stub — returns false.
        /// </summary>
        protected virtual bool IsInputKey (Keys keyData) => false;

        /// <summary>
        /// Determines whether a character is an input character. Majorsilence.Forms stub — returns false.
        /// </summary>
        protected virtual bool IsInputChar (char charCode) => false;

        /// <summary>
        /// Processes a command key. Override in a derived class to intercept keyboard shortcuts before key events are raised.
        /// Returns true if the key was processed. Majorsilence.Forms stub — passes through to the base implementation.
        /// </summary>
        protected virtual bool ProcessCmdKey (ref Message msg, Keys keyData) => false;

        /// <summary>
        /// Processes a dialog key. Override to handle keys like Enter/Escape in dialogs.
        /// Returns true if the key was handled. Majorsilence.Forms stub.
        /// </summary>
        protected virtual bool ProcessDialogKey (Keys keyData) => false;

        /// <summary>Processes a keyboard message. Returns true if the message was handled. Stub in Majorsilence.Forms.</summary>
        protected virtual bool ProcessKeyMessage (ref Message m) => false;

        /// <summary>
        /// Previews a keyboard message. Returns true if the message was handled. Majorsilence.Forms stub.
        /// </summary>
        protected virtual bool ProcessKeyPreview (ref Message m) => false;

        /// <summary>
        /// Performs the mnemonic operation (Alt+key) for the control. Returns true if handled. Majorsilence.Forms stub.
        /// </summary>
        protected virtual bool ProcessMnemonic (char charCode) => false;

        /// <summary>Gets whether the control's Size includes scrollbar sizes. Stub in Majorsilence.Forms.</summary>
        public bool HScroll { get; set; }

        /// <summary>Gets whether the control's Size includes scrollbar sizes. Stub in Majorsilence.Forms.</summary>
        public bool VScroll { get; set; }

        /// <summary>Processes a tab key. Returns true if the key was processed. Majorsilence.Forms stub.</summary>
        protected virtual bool ProcessTabKey (bool forward) => false;

        /// <summary>Notifies the accessibility client application of a specified event. Stub in Majorsilence.Forms.</summary>
        public void AccessibilityNotifyClients (AccessibleEvents accEvent, int childID) { }

        /// <summary>Gets or sets the window region associated with the control. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Region? Region { get; set; }

        /// <summary>Gets whether this control is currently in design mode. Always false in Majorsilence.Forms.</summary>
        public new bool DesignMode => false;

        /// <summary>Gets the site associated with this component. Stub in Majorsilence.Forms.</summary>
        public new System.ComponentModel.ISite? Site { get; set; }

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
        Close = 4,
        /// <summary>IME in hiragana input mode.</summary>
        Hiragana = 5,
        /// <summary>IME in katakana input mode.</summary>
        Katakana = 6,
        /// <summary>IME in half-width katakana input mode.</summary>
        KatakanaHalf = 7,
        /// <summary>IME in alphanumeric mode.</summary>
        Alpha = 8,
        /// <summary>IME in half-width alphanumeric mode.</summary>
        AlphaFull = 9,
        /// <summary>IME in hangul mode.</summary>
        Hangul = 10,
        /// <summary>IME in half-width hangul mode.</summary>
        HangulFull = 11,
        /// <summary>Inherits from parent control.</summary>
        Inherit = -1
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
