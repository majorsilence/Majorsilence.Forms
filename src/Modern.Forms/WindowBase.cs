using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Modern.Forms
{
    /// <summary>
    /// Represents the base class for windows, like Form and PopupWindow.
    /// </summary>
    public abstract partial class WindowBase : Component
    {
        private const int DOUBLE_CLICK_TIME = 500;
        private const int DOUBLE_CLICK_MOVEMENT = 4;

        internal ModernFormsWindowHost AvWindow = null!;
        internal ControlAdapter adapter = null!;

        private DateTime last_click_time;
        private System.Drawing.Point last_click_point;
        private Cursor? current_cursor;
        internal bool shown;

        /// <summary>
        /// Initializes the Avalonia platform. Subclasses must call <see cref="InitWindow"/> before
        /// accessing any window or adapter members.
        /// </summary>
        protected WindowBase ()
        {
            AvaloniaBootstrap.EnsureInitialized ();
        }

        /// <summary>
        /// Completes window initialisation. Must be called in subclass constructors before accessing
        /// Controls, adapter, or any window property.
        /// </summary>
        internal void InitWindow (ModernFormsWindowHost avWindow)
        {
            AvWindow = avWindow;
            adapter = new ControlAdapter (this);

            AvWindow.Closed += (s, e) => Closed?.Invoke (this, EventArgs.Empty);
            AvWindow.Deactivated += (s, e) => {
                Application.ClosePopups ();
                Deactivated?.Invoke (this, EventArgs.Empty);
            };
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

            AvWindow.Close ();
        }

        /// <summary>Raised when the window is closed.</summary>
        public event EventHandler? Closed;

        /// <summary>Gets the collection of controls contained by the window.</summary>
        public Control.ControlCollection Controls => adapter.Controls;

        /// <summary>Gets the current style of this window instance.</summary>
        public virtual ControlStyle CurrentStyle => Style;

        /// <summary>Raised when the window is deactivated.</summary>
        public event EventHandler? Deactivated;

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
            (int)AvWindow.ClientSize.Width - CurrentStyle.Border.Right.GetWidth () - CurrentStyle.Border.Left.GetWidth (),
            (int)AvWindow.ClientSize.Height - CurrentStyle.Border.Top.GetWidth () - CurrentStyle.Border.Bottom.GetWidth ());

        internal virtual bool HandleMouseDown (int x, int y) => false;

        internal virtual bool HandleMouseMove (int x, int y)
        {
            AvWindow.Cursor = current_cursor?.cursor ?? Cursors.Arrow.cursor;
            return false;
        }

        /// <summary>Hides the window without destroying it.</summary>
        public void Hide ()
        {
            Visible = false;
            AvWindow.Hide ();

            if (Application.ActivePopupWindow == this)
                Application.ActivePopupWindow = null;

            OnVisibleChanged (EventArgs.Empty);
        }

        /// <summary>Marks the entire window as needing to be redrawn.</summary>
        public void Invalidate () => AvWindow.IsDirty = true;

        /// <summary>Marks the specified portion of the window as needing to be redrawn.</summary>
        public void Invalidate (System.Drawing.Rectangle rectangle) => Invalidate ();

        /// <summary>Executes the specified delegate asynchronously on the window's UI thread.</summary>
        public void BeginInvoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);
            Dispatcher.UIThread.Post (action);
        }

        /// <summary>Executes the specified delegate synchronously on the window's UI thread.</summary>
        public void Invoke (Action action)
        {
            ArgumentNullException.ThrowIfNull (action);

            if (Dispatcher.UIThread.CheckAccess ()) {
                action ();
                return;
            }

            Dispatcher.UIThread.InvokeAsync (action).GetAwaiter ().GetResult ();
        }

        /// <summary>Gets the unscaled location of the window.</summary>
        public System.Drawing.Point Location => new System.Drawing.Point (AvWindow.Position.X, AvWindow.Position.Y);

        /// <summary>Raised when the MaximumSize property is changed.</summary>
        public event EventHandler? MaximumSizeChanged;

        /// <summary>Raised when the MinimumSize property is changed.</summary>
        public event EventHandler? MinimumSizeChanged;

        // ── Avalonia input event handlers (called from ModernFormsWindowHost) ─────────────────────

        internal void OnAvaloniaPointerPressed (Avalonia.Input.PointerPressedEventArgs e)
        {
            var pos = e.GetPosition (AvWindow);
            var x = (int)(pos.X * Scaling);
            var y = (int)(pos.Y * Scaling);

            var props = e.GetCurrentPoint (AvWindow).Properties;
            var button = AvaloniaKeyInterop.PressedButton (props.PointerUpdateKind);
            var keys = AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers);

            if (Resizeable && HandleMouseDown (x, y))
                return;

            var ev = new MouseEventArgs (button, 1, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseDown (ev);
        }

        internal void OnAvaloniaPointerReleased (Avalonia.Input.PointerReleasedEventArgs e)
        {
            var pos = e.GetPosition (AvWindow);
            var x = (int)(pos.X * Scaling);
            var y = (int)(pos.Y * Scaling);

            var props = e.GetCurrentPoint (AvWindow).Properties;
            var button = AvaloniaKeyInterop.ReleasedButton (props.PointerUpdateKind);
            var keys = AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers);
            var point = new System.Drawing.Point (x, y);

            var ev = BuildMouseClickArgs (button, point, keys);

            if (ev.Clicks > 1)
                adapter.RaiseDoubleClick (ev);

            adapter.RaiseClick (ev);
            adapter.RaiseMouseUp (ev);
        }

        internal void OnAvaloniaPointerMoved (Avalonia.Input.PointerEventArgs e)
        {
            var pos = e.GetPosition (AvWindow);
            var x = (int)(pos.X * Scaling);
            var y = (int)(pos.Y * Scaling);

            var props = e.GetCurrentPoint (AvWindow).Properties;
            var buttons = AvaloniaKeyInterop.ToMouseButtons (props);
            var keys = AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers);

            if (Resizeable && HandleMouseMove (x, y))
                return;

            var ev = new MouseEventArgs (buttons, 0, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseMove (ev);
        }

        internal void OnAvaloniaPointerWheel (Avalonia.Input.PointerWheelEventArgs e)
        {
            var pos = e.GetPosition (AvWindow);
            var x = (int)(pos.X * Scaling);
            var y = (int)(pos.Y * Scaling);

            var props = e.GetCurrentPoint (AvWindow).Properties;
            var buttons = AvaloniaKeyInterop.ToMouseButtons (props);
            var keys = AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers);

            var delta = new System.Drawing.Point ((int)e.Delta.X, (int)e.Delta.Y);
            var ev = new MouseEventArgs (buttons, 0, x, y, delta, keyData: keys);
            adapter.RaiseMouseWheel (ev);
        }

        internal void OnAvaloniaPointerExited (Avalonia.Input.PointerEventArgs e)
        {
            var pos = e.GetPosition (AvWindow);
            var x = (int)(pos.X * Scaling);
            var y = (int)(pos.Y * Scaling);

            var props = e.GetCurrentPoint (AvWindow).Properties;
            var buttons = AvaloniaKeyInterop.ToMouseButtons (props);
            var keys = AvaloniaKeyInterop.ModifiersOnly (e.KeyModifiers);

            var ev = new MouseEventArgs (buttons, 0, x, y, System.Drawing.Point.Empty, keyData: keys);
            adapter.RaiseMouseLeave (ev);
        }

        internal void OnAvaloniaKeyDown (Avalonia.Input.KeyEventArgs e)
        {
            var keys = AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers);
            var kd_e = new KeyEventArgs (keys);

            // Form-level shortcuts: AcceptButton / CancelButton / modal Escape
            if (this is Form form) {
                var baseKey = keys & Keys.KeyCode;

                if (baseKey == Keys.Return && form.AcceptButton != null) {
                    form.AcceptButton.PerformClick ();
                    e.Handled = true;
                    return;
                }

                if (baseKey == Keys.Escape) {
                    if (form.CancelButton != null) {
                        form.CancelButton.PerformClick ();
                        e.Handled = true;
                        return;
                    }

                    if (form.dialog_task is not null) {
                        form.DialogResult = DialogResult.Cancel;
                        e.Handled = true;
                        return;
                    }
                }

                // KeyPreview: let the form see the key first
                if (form.KeyPreview) {
                    OnKeyDown (kd_e);
                    if (kd_e.Handled) { e.Handled = true; return; }
                    adapter.RaiseKeyDown (kd_e);
                    if (kd_e.Handled) e.Handled = true;
                    return;
                }
            }

            OnKeyDown (kd_e);

            if (kd_e.Handled) {
                e.Handled = true;
                return;
            }

            adapter.RaiseKeyDown (kd_e);

            if (kd_e.Handled)
                e.Handled = true;
        }

        internal void OnAvaloniaKeyUp (Avalonia.Input.KeyEventArgs e)
        {
            var keys = AvaloniaKeyInterop.AddModifiers (AvaloniaKeyInterop.ToFormsKey (e.Key), e.KeyModifiers);
            var ku_e = new KeyEventArgs (keys);

            OnKeyUp (ku_e);

            if (ku_e.Handled) {
                e.Handled = true;
                return;
            }

            adapter.RaiseKeyUp (ku_e);

            if (ku_e.Handled)
                e.Handled = true;
        }

        internal void OnAvaloniaTextInput (Avalonia.Input.TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty (e.Text))
                return;

            var kp_e = new KeyPressEventArgs (e.Text, Keys.None);

            OnKeyPress (kp_e);

            if (kp_e.Handled) {
                e.Handled = true;
                return;
            }

            adapter.RaiseKeyPress (kp_e);

            if (kp_e.Handled)
                e.Handled = true;
        }

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
        public System.Drawing.Point PointToClient (System.Drawing.Point point)
        {
            var pt = AvWindow.PointToClient (new PixelPoint (point.X, point.Y));
            return new System.Drawing.Point ((int)pt.X, (int)pt.Y);
        }

        /// <summary>Converts a point from window coordinates to screen coordinates.</summary>
        public System.Drawing.Point PointToScreen (System.Drawing.Point point)
        {
            var pt = AvWindow.PointToScreen (new Avalonia.Point (point.X, point.Y));
            return new System.Drawing.Point (pt.X, pt.Y);
        }

        /// <summary>Gets or sets whether the window is resizable.</summary>
        public bool Resizeable { get; set; }

        private System.Drawing.Size ScaledClientSize => new System.Drawing.Size (
            (int)(AvWindow.ClientSize.Width * Scaling),
            (int)(AvWindow.ClientSize.Height * Scaling));

        /// <summary>Gets the scaled bounds of the form not including borders.</summary>
        public System.Drawing.Rectangle ScaledDisplayRectangle => new System.Drawing.Rectangle (
            CurrentStyle.Border.Left.GetWidth (),
            CurrentStyle.Border.Top.GetWidth (),
            ScaledClientSize.Width - CurrentStyle.Border.Right.GetWidth () - CurrentStyle.Border.Left.GetWidth (),
            ScaledClientSize.Height - CurrentStyle.Border.Top.GetWidth () - CurrentStyle.Border.Bottom.GetWidth ());

        /// <summary>Gets or sets the scaled size of the window.</summary>
        public System.Drawing.Size ScaledSize => ScaledClientSize;

        /// <summary>Gets the current scale factor of the window.</summary>
        public double Scaling => AvWindow.RenderScaling;

        /// <summary>Gets the current scale factor of the desktop.</summary>
        public double DesktopScaling => AvWindow.RenderScaling;

        internal Avalonia.Controls.Screens Screens => AvWindow.Screens;

        internal void SetCursor (Cursor cursor) => current_cursor = cursor;

        internal virtual void SetWindowStartupLocation (Avalonia.Controls.Window? owner = null) { }

        /// <summary>Displays the window to the user.</summary>
        public void Show ()
        {
            Visible = true;
            OnVisibleChanged (EventArgs.Empty);

            SetWindowStartupLocation ();
            AvWindow.Show ();

            if (this is Form f)
                Application.OpenForms.Add (f);

            if (!shown) {
                shown = true;
                OnShown (EventArgs.Empty);
            }
        }

        internal void ShowDialog (Avalonia.Controls.Window parentAvWindow)
        {
            Visible = true;
            OnVisibleChanged (EventArgs.Empty);

            SetWindowStartupLocation (parentAvWindow);
            parentAvWindow.IsEnabled = false;
            AvWindow.Show ();

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
            (int)AvWindow.ClientSize.Width,
            (int)AvWindow.ClientSize.Height);

        /// <summary>Gets or sets the startup location of the window.</summary>
        public FormStartPosition StartPosition { get; set; } = FormStartPosition.CenterScreen;

        /// <summary>Gets the ControlStyle properties for this instance of the window.</summary>
        public virtual ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets whether the window is displayed to the user.</summary>
        public bool Visible { get; private set; }
    }
}
