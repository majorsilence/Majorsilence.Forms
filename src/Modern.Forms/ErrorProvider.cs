using System.Collections.Generic;
using System.ComponentModel;
using SkiaSharp;

namespace Modern.Forms
{
    /// <summary>
    /// WinForms compatibility: provides a user interface for indicating validation errors on a form.
    /// Modern.Forms does not render error icons natively; the error text is stored for
    /// programmatic access and shown in the control's ToolTip text if a ToolTip is set.
    /// </summary>
    public class ErrorProvider : Component
    {
        private readonly Dictionary<Control, string> _errors = new ();

        /// <summary>Initializes a new instance of ErrorProvider.</summary>
        public ErrorProvider () { }

        /// <summary>Initializes a new instance of ErrorProvider and adds it to the specified container.</summary>
        public ErrorProvider (IContainer container) { container.Add (this); }

        /// <summary>Gets or sets the rate in milliseconds at which the error icon blinks. Stub in Modern.Forms.</summary>
        public int BlinkRate { get; set; } = 250;

        /// <summary>Gets or sets the blink style for the error icon. Stub in Modern.Forms.</summary>
        public ErrorBlinkStyle BlinkStyle { get; set; } = ErrorBlinkStyle.BlinkIfDifferentError;

        /// <summary>Gets or sets the container control (form) to watch. Stub in Modern.Forms.</summary>
        public Form? ContainerControl { get; set; }

        /// <summary>Gets or sets the icon displayed next to a control with an error. Stub in Modern.Forms.</summary>
        public Modern.Drawing.Icon? Icon { get; set; }

        /// <summary>Sets the error description string for the specified control.</summary>
        public void SetError (Control control, string value)
        {
            if (string.IsNullOrEmpty (value))
                _errors.Remove (control);
            else
                _errors[control] = value;
        }

        /// <summary>Gets the error description string for the specified control.</summary>
        public string GetError (Control control)
        {
            return _errors.TryGetValue (control, out var msg) ? msg : string.Empty;
        }

        /// <summary>Clears all error descriptions.</summary>
        public void Clear () => _errors.Clear ();

        /// <summary>Sets the icon alignment for the specified control. Stub in Modern.Forms.</summary>
        public void SetIconAlignment (Control control, ErrorIconAlignment value) { }

        /// <summary>Gets the icon alignment for the specified control. Stub in Modern.Forms.</summary>
        public ErrorIconAlignment GetIconAlignment (Control control) => ErrorIconAlignment.MiddleRight;

        /// <summary>Sets the icon padding for the specified control. Stub in Modern.Forms.</summary>
        public void SetIconPadding (Control control, int padding) { }

        /// <summary>Gets the icon padding for the specified control. Stub in Modern.Forms.</summary>
        public int GetIconPadding (Control control) => 0;

        /// <summary>Gets or sets the data source for automatic validation. Stub in Modern.Forms.</summary>
        public object? DataSource { get; set; }

        /// <summary>Gets or sets the data member for automatic validation. Stub in Modern.Forms.</summary>
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
