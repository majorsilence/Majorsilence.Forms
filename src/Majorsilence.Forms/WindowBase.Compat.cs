namespace Majorsilence.Forms
{
    // WinForms-compatibility surface for WindowBase (and therefore Form).
    // Control exposes the same key/message-processing override points in
    // Control.Compat.cs, but Form derives from WindowBase (not Control), so the
    // surface is mirrored here to let forms override ProcessCmdKey and friends.
    public abstract partial class WindowBase
    {
        /// <summary>
        /// Processes Windows messages. Override to intercept messages. Stub in Majorsilence.Forms — does nothing.
        /// </summary>
        protected virtual void WndProc (ref Message m) { }

        /// <summary>
        /// Invokes the default Windows procedure for the window. Stub in Majorsilence.Forms — does nothing.
        /// </summary>
        protected void DefWndProc (ref Message m) { }

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
        /// Performs the mnemonic operation (Alt+key) for the window. Returns true if handled. Majorsilence.Forms stub.
        /// </summary>
        protected virtual bool ProcessMnemonic (char charCode) => false;
    }
}
