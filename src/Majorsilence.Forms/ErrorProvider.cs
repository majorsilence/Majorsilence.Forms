using System;
using System.Collections.Generic;
using System.ComponentModel;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: provides a user interface for indicating validation errors on a form.
    /// Majorsilence.Forms does not render error icons natively; the error text is stored for
    /// programmatic access and shown in the control's ToolTip text if a ToolTip is set.
    /// </summary>
    public class ErrorProvider : Component
    {
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
            if (container is null)
                throw new ArgumentNullException (nameof (container));

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

        /// <summary>Gets or sets the container control (form) to watch. Stub in Majorsilence.Forms.</summary>
        public Form? ContainerControl { get; set; }

        /// <summary>Gets or sets the icon displayed next to a control with an error. Stub in Majorsilence.Forms.</summary>
        public Majorsilence.Drawing.Icon? Icon { get; set; }

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
        public void SetError (Control control, string value)
        {
            if (control is null)
                throw new ArgumentNullException (nameof (control));

            if (string.IsNullOrEmpty (value))
                _errors.Remove (control);
            else
                _errors[control] = value;
        }

        /// <summary>Gets the error description string for the specified control.</summary>
        public string GetError (Control control)
        {
            if (control is null)
                throw new ArgumentNullException (nameof (control));

            return _errors.TryGetValue (control, out var msg) ? msg : string.Empty;
        }

        /// <summary>Clears all error descriptions.</summary>
        public void Clear () => _errors.Clear ();

        /// <summary>Sets the icon alignment for the specified control. Stub in Majorsilence.Forms.</summary>
        public void SetIconAlignment (Control control, ErrorIconAlignment value)
        {
            if (control is null)
                throw new ArgumentNullException (nameof (control));

            if (value < ErrorIconAlignment.TopLeft || value > ErrorIconAlignment.BottomRight)
                throw new InvalidEnumArgumentException (nameof (value), (int)value, typeof (ErrorIconAlignment));

            _iconAlignments[control] = value;
        }

        /// <summary>Gets the icon alignment for the specified control. Stub in Majorsilence.Forms.</summary>
        public ErrorIconAlignment GetIconAlignment (Control control)
        {
            if (control is null)
                throw new ArgumentNullException (nameof (control));

            return _iconAlignments.TryGetValue (control, out var alignment) ? alignment : ErrorIconAlignment.MiddleRight;
        }

        /// <summary>Sets the icon padding for the specified control. Stub in Majorsilence.Forms.</summary>
        public void SetIconPadding (Control control, int padding)
        {
            if (control is null)
                throw new ArgumentNullException (nameof (control));

            _iconPaddings[control] = padding;
        }

        /// <summary>Gets the icon padding for the specified control. Stub in Majorsilence.Forms.</summary>
        public int GetIconPadding (Control control)
        {
            if (control is null)
                throw new ArgumentNullException (nameof (control));

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
