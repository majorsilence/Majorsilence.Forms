namespace Continuum.Forms
{
    /// <summary>
    ///  Provides data for the KeyDown or KeyUp event.
    /// </summary>
    public class KeyEventArgs : EventArgs
    {
        private bool _suppressKeyPress;

        /// <summary>
        ///  Initializes a new instance of the KeyEventArgs class.
        /// </summary>
        public KeyEventArgs (Keys keyData)
        {
            KeyData = keyData;

            // Keep the static Control.ModifierKeys current for WinForms-compatible callers.
            Continuum.Forms.Control.ModifierKeys = keyData & Keys.Modifiers;
        }

        /// <summary>Gets a value indicating whether the ALT key was pressed.</summary>
        public virtual bool Alt => (KeyData & Keys.Alt) == Keys.Alt;

        /// <summary>Gets a value indicating whether the CTRL key was pressed.</summary>
        public bool Control => (KeyData & Keys.Control) == Keys.Control;

        /// <summary>Gets or sets a value indicating whether the event was handled.</summary>
        public bool Handled { get; set; }

        /// <summary>Gets the keyboard code for a KeyDown or KeyUp event.</summary>
        public Keys KeyCode {
            get {
                var keyGenerated = KeyData & Keys.KeyCode;

                if (!Enum.IsDefined (typeof (Keys), (int)keyGenerated))
                    return Keys.None;

                return keyGenerated;
            }
        }

        /// <summary>Gets the keyboard value for a KeyDown or KeyUp event.</summary>
        public int KeyValue => (int)(KeyData & Keys.KeyCode);

        /// <summary>Gets the key data for a KeyDown or KeyUp event.</summary>
        public Keys KeyData { get; }

        /// <summary>Gets the modifier flags for a KeyDown or KeyUp event.</summary>
        public Keys Modifiers => KeyData & Keys.Modifiers;

        /// <summary>Gets a value indicating whether the SHIFT key was pressed.</summary>
        public virtual bool Shift => (KeyData & Keys.Shift) == Keys.Shift;

        /// <summary>Gets or sets a value indicating the key press should be suppressed.</summary>
        public bool SuppressKeyPress {
            get => _suppressKeyPress;
            set {
                _suppressKeyPress = value;
                Handled = value;
            }
        }

    }
}
