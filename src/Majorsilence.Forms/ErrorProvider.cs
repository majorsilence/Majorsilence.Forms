using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Provides a user interface for indicating validation errors on a form: an error icon beside
    /// each control that has one.
    /// </summary>
    /// <remarks>
    /// The icon is painted by the errored control's PARENT, through
    /// <c>Control.PaintAdorners</c> -- a layer that runs after the container's children, so the icon
    /// sits on top of them. Upstream uses a separate <c>ErrorWindow</c> per control, which this
    /// framework has no child windows for.
    /// <para>
    /// This class summary used to say that Majorsilence.Forms "does not render error icons natively"
    /// and that the text is "shown in the control's ToolTip text if a ToolTip is set" -- the first was
    /// true and the second never was: <see cref="SetError"/> only wrote to a dictionary that nothing
    /// but <see cref="GetError"/> read. <c>errorProvider1.SetError (txtName, "Required")</c> in a
    /// <c>Validating</c> handler, the canonical WinForms validation affordance, produced no feedback of
    /// any kind, so a form refused to submit with nothing on screen explaining why (finding
    /// <c>SMP-51</c>, P0).
    /// </para>
    /// <para>
    /// Not implemented: blinking (<see cref="BlinkStyle"/>/<see cref="BlinkRate"/> are honoured as
    /// state but no timer runs), a custom <see cref="Icon"/> -- the built-in glyph is always drawn --
    /// and the hover tooltip. Each is additive and none of them is what made this a P0.
    /// </para>
    /// </remarks>
    public partial class ErrorProvider : Component, ISupportInitialize
    {
        // Real WinForms ErrorProvider implements ISupportInitialize, so designer code brackets it with
        // ((ISupportInitialize)errorProvider).BeginInit()/EndInit() -- explicit no-ops so that
        // unconditional cast succeeds instead of throwing InvalidCastException (found opening
        // frmMaintainCustomer, which has several ErrorProviders).
        void ISupportInitialize.BeginInit () { }
        void ISupportInitialize.EndInit () { }

        private readonly Dictionary<Control, string> _errors = new ();
        private readonly Dictionary<Control, ErrorIconAlignment> _iconAlignments = new ();
        private readonly Dictionary<Control, int> _iconPaddings = new ();
        private int _blinkRate = 250;
        private ErrorBlinkStyle _blinkStyle = ErrorBlinkStyle.BlinkIfDifferentError;
        private bool _rightToLeft;

        /// <summary>Initializes a new instance of ErrorProvider.</summary>
        public ErrorProvider () { }

        /// <summary>Initializes a new instance of ErrorProvider and adds it to the specified container.</summary>
        public ErrorProvider (IContainer container)
        {
            Guard.ThrowIfNull (container);

            container.Add (this);
        }

        /// <summary>
        /// Gets or sets the rate in milliseconds at which the error icon blinks. Stub in Majorsilence.Forms.
        /// Setting the rate to zero forces <see cref="BlinkStyle"/> to <see cref="ErrorBlinkStyle.NeverBlink"/>.
        /// </summary>
        public int BlinkRate {
            get => _blinkRate;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), $"Value '{value}' must be greater than or equal to 0.");

                _blinkRate = value;

                // If the blinkRate is zero, then set blinkStyle to NeverBlink to match WinForms.
                if (_blinkRate == 0)
                    _blinkStyle = ErrorBlinkStyle.NeverBlink;
            }
        }

        /// <summary>Gets or sets the blink style for the error icon. Stub in Majorsilence.Forms.</summary>
        public ErrorBlinkStyle BlinkStyle {
            get {
                // If the blink rate is zero the icon can never blink.
                if (_blinkRate == 0)
                    return ErrorBlinkStyle.NeverBlink;

                return _blinkStyle;
            }
            set {
                if (value < ErrorBlinkStyle.BlinkIfDifferentError || value > ErrorBlinkStyle.NeverBlink)
                    throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (ErrorBlinkStyle));

                _blinkStyle = value;
            }
        }

        /// <summary>
        /// Gets or sets the container (a Form or a control such as a UserControl) to watch. Stub in
        /// Majorsilence.Forms. Typed as <see cref="Component"/> because Form and Control sit on separate
        /// inheritance branches here (unlike WinForms, where both derive from ContainerControl), so a
        /// single common base is needed to accept either as the assignment target.
        /// </summary>
        public Component? ContainerControl { get; set; }

        /// <summary>Gets or sets the icon displayed next to a control with an error. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Forms.Drawing.Icon? Icon { get; set; }

        /// <summary>Gets a value indicating whether the error provider currently has errors for any control.</summary>
        public bool HasErrors => _errors.Count > 0;

        /// <summary>Gets or sets user-defined data associated with this error provider.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets a value indicating whether the component is laid out right-to-left.</summary>
        public bool RightToLeft {
            get => _rightToLeft;
            set {
                if (_rightToLeft == value)
                    return;

                _rightToLeft = value;
                OnRightToLeftChanged (EventArgs.Empty);
            }
        }

        /// <summary>Occurs when the <see cref="RightToLeft"/> property changes.</summary>
        public event EventHandler? RightToLeftChanged;

        /// <summary>Raises the <see cref="RightToLeftChanged"/> event.</summary>
        protected virtual void OnRightToLeftChanged (EventArgs e) => RightToLeftChanged?.Invoke (this, e);

        /// <summary>Sets the error description string for the specified control.</summary>
        /// <remarks>An empty or null description clears the error, as upstream.</remarks>
        public void SetError (Control control, string value)
        {
            Guard.ThrowIfNull (control);

            if (string.IsNullOrEmpty (value))
                _errors.Remove (control);
            else
                _errors[control] = value;

            Attach (control);
            Repaint (control);
        }

        /// <summary>Clears all error descriptions.</summary>
        public void Clear ()
        {
            var affected = _errors.Keys.ToArray ();

            _errors.Clear ();

            foreach (var control in affected)
                Repaint (control);
        }

        // Every container this provider has hooked, so a second error on the same parent does not
        // subscribe twice and the icons can be found again when the parent repaints.
        private readonly HashSet<Control> _hooked = new ();

        private void Attach (Control control)
        {
            // The PARENT paints the icon, because the icon sits outside the control's own bounds.
            if (control.Parent is not { } parent || !_hooked.Add (parent))
                return;

            parent.PaintAdorners += PaintErrorIcons;
        }

        private void Repaint (Control control) => control.Parent?.Invalidate ();

        // Draws an icon for every errored child of the container being painted. Iterating the
        // container's own children rather than the error dictionary keeps a control that has been
        // removed from the form from drawing anything.
        private void PaintErrorIcons (object? sender, PaintEventArgs e)
        {
            if (sender is not Control parent)
                return;

            foreach (var child in parent.Controls) {
                if (!child.Visible || !_errors.ContainsKey (child))
                    continue;

                DrawIcon (e, IconBounds (e, child));
            }
        }

        /// <summary>The icon's rectangle, in the device pixels the parent's canvas uses.</summary>
        /// <remarks>Child <c>Bounds</c> are logical while the paint canvas is device-scaled, so the
        /// geometry is converted here rather than in the drawing helper.</remarks>
        private Rectangle IconBounds (PaintEventArgs e, Control control)
        {
            var size = e.LogicalToDeviceUnits (IconSize);
            var padding = e.LogicalToDeviceUnits (GetIconPadding (control));
            var bounds = new Rectangle (
                e.LogicalToDeviceUnits (control.Left),
                e.LogicalToDeviceUnits (control.Top),
                e.LogicalToDeviceUnits (control.Width),
                e.LogicalToDeviceUnits (control.Height));

            var alignment = GetIconAlignment (control);
            var left = alignment switch {
                ErrorIconAlignment.TopLeft or ErrorIconAlignment.MiddleLeft or ErrorIconAlignment.BottomLeft
                    => bounds.Left - size - padding,
                _ => bounds.Right + padding,
            };
            var top = alignment switch {
                ErrorIconAlignment.TopLeft or ErrorIconAlignment.TopRight => bounds.Top,
                ErrorIconAlignment.BottomLeft or ErrorIconAlignment.BottomRight => bounds.Bottom - size,
                _ => bounds.Top + ((bounds.Height - size) / 2),
            };

            return new Rectangle (left, top, size, size);
        }

        /// <summary>The icon's logical edge length. Upstream's error bitmap is 16x16.</summary>
        internal const int IconSize = 16;

        // A filled circle with an exclamation mark, drawn from primitives rather than shipped as an
        // image: the framework has no resource pipeline for one, and a glyph that scales with the
        // display beats a bitmap that does not.
        private static void DrawIcon (PaintEventArgs e, Rectangle bounds)
        {
            var radius = bounds.Width / 2;
            var centre_x = bounds.Left + radius;
            var centre_y = bounds.Top + radius;

            e.Canvas.FillCircle (centre_x, centre_y, radius, ErrorIconColor);

            // The bar and the dot of the "!", sized off the icon so they hold at any scale.
            var bar_width = Math.Max (1, bounds.Width / 8);
            var bar_top = bounds.Top + (bounds.Height / 4);
            var bar_height = bounds.Height / 3;

            e.Canvas.FillRectangle (
                new Rectangle (centre_x - (bar_width / 2), bar_top, bar_width, bar_height),
                ErrorIconGlyphColor);

            e.Canvas.FillRectangle (
                new Rectangle (centre_x - (bar_width / 2), bar_top + bar_height + Math.Max (1, bounds.Height / 12), bar_width, bar_width),
                ErrorIconGlyphColor);
        }

        internal static readonly SKColor ErrorIconColor = new SKColor (0xC4, 0x22, 0x1E);
        internal static readonly SKColor ErrorIconGlyphColor = new SKColor (0xFF, 0xFF, 0xFF);

        /// <summary>Gets the error description string for the specified control.</summary>
        public string GetError (Control control)
        {
            Guard.ThrowIfNull (control);

            return _errors.TryGetValue (control, out var msg) ? msg : string.Empty;
        }

        /// <summary>Sets the icon alignment for the specified control. Stub in Majorsilence.Forms.</summary>
        public void SetIconAlignment (Control control, ErrorIconAlignment value)
        {
            Guard.ThrowIfNull (control);

            if (value < ErrorIconAlignment.TopLeft || value > ErrorIconAlignment.BottomRight)
                throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (ErrorIconAlignment));

            _iconAlignments[control] = value;
        }

        /// <summary>Gets the icon alignment for the specified control. Stub in Majorsilence.Forms.</summary>
        public ErrorIconAlignment GetIconAlignment (Control control)
        {
            Guard.ThrowIfNull (control);

            return _iconAlignments.TryGetValue (control, out var alignment) ? alignment : ErrorIconAlignment.MiddleRight;
        }

        /// <summary>Sets the icon padding for the specified control. Stub in Majorsilence.Forms.</summary>
        public void SetIconPadding (Control control, int padding)
        {
            Guard.ThrowIfNull (control);

            _iconPaddings[control] = padding;
        }

        /// <summary>Gets the icon padding for the specified control. Stub in Majorsilence.Forms.</summary>
        public int GetIconPadding (Control control)
        {
            Guard.ThrowIfNull (control);

            return _iconPaddings.TryGetValue (control, out var padding) ? padding : 0;
        }

        /// <summary>Gets or sets the data source for automatic validation. Stub in Majorsilence.Forms.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the data member for automatic validation. Stub in Majorsilence.Forms.</summary>
        public string DataMember { get; set; } = string.Empty;
    }

    /// <summary>Specifies the alignment of an error icon in relation to the control with an error.</summary>
    public enum ErrorIconAlignment
    {
        /// <summary>The icon appears to the left of the top of the control.</summary>
        TopLeft,
        /// <summary>The icon appears to the right of the top of the control.</summary>
        TopRight,
        /// <summary>The icon appears to the left of the middle of the control.</summary>
        MiddleLeft,
        /// <summary>The icon appears to the right of the middle of the control.</summary>
        MiddleRight,
        /// <summary>The icon appears to the left of the bottom of the control.</summary>
        BottomLeft,
        /// <summary>The icon appears to the right of the bottom of the control.</summary>
        BottomRight
    }

    /// <summary>
    /// Specifies when the error icon blinks to alert the user to an error condition.
    /// </summary>
    public enum ErrorBlinkStyle
    {
        /// <summary>Blinks when the error is first displayed or when the description changes.</summary>
        BlinkIfDifferentError,
        /// <summary>Blinks continuously.</summary>
        AlwaysBlink,
        /// <summary>Never blinks.</summary>
        NeverBlink
    }
}
