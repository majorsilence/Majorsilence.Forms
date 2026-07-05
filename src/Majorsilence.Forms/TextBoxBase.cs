namespace Majorsilence.Forms
{
    /// <summary>
    /// WinForms compatibility: the common base of TextBox-family controls. Real WinForms code
    /// frequently types parameters as TextBoxBase; Text comes from Control, the selection members
    /// are implemented by the concrete controls.
    /// </summary>
    public abstract class TextBoxBase : ScrollControl
    {
        /// <summary>Gets or sets the start of the selected text.</summary>
        public abstract int SelectionStart { get; set; }

        /// <summary>Gets or sets the length of the selected text.</summary>
        public abstract int SelectionLength { get; set; }
    }
}
