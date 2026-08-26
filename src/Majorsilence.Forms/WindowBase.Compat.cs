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

        // ── The window end of the keyboard pre-processing chain ──────────────────────────────────
        //
        // These are the last link: ControlAdapter forwards into them once the focused control and its
        // parent chain have all declined the key (see ControlAdapter's Process* overrides). They used
        // to be `=> false` with no caller, which is why `override ProcessCmdKey` on a ported Form never
        // ran. Form overrides ProcessDialogKey for AcceptButton/CancelButton and ProcessKeyPreview for
        // KeyPreview.

        /// <summary>
        /// Whether the window consumes <paramref name="keyData"/> itself rather than letting it be
        /// treated as a navigation key.
        /// </summary>
        protected virtual bool IsInputKey (Keys keyData) => false;

        /// <summary>
        /// Whether the window consumes <paramref name="charCode"/> as text rather than as a mnemonic.
        /// </summary>
        protected virtual bool IsInputChar (char charCode) => false;

        /// <summary>
        /// Processes a command key before any key event is raised — the override point for keyboard
        /// shortcuts. This is the end of the chain, so there is nothing above to bubble to.
        /// </summary>
        protected virtual bool ProcessCmdKey (ref Message msg, Keys keyData) => false;

        /// <summary>
        /// Processes a dialog key — Enter, Escape and friends. Reached only after the focused control
        /// and every container above it declined the key. <see cref="Form"/> overrides this for
        /// <see cref="Form.AcceptButton"/> and <see cref="Form.CancelButton"/>.
        /// </summary>
        protected virtual bool ProcessDialogKey (Keys keyData) => false;

        /// <summary>Processes a dialog character — an Alt+letter mnemonic.</summary>
        protected virtual bool ProcessDialogChar (char charCode) => false;

        /// <summary>Processes a keyboard message. Returns true if the message was handled.</summary>
        protected virtual bool ProcessKeyMessage (ref Message m) => false;

        /// <summary>
        /// Gives the window first refusal on a keyboard message. <see cref="Form"/> overrides this to
        /// honour <see cref="Form.KeyPreview"/>.
        /// </summary>
        protected virtual bool ProcessKeyPreview (ref Message m) => false;

        /// <summary>
        /// Performs the mnemonic operation (Alt+key) for the window — offers the character to every
        /// child until one claims it.
        /// </summary>
        protected virtual bool ProcessMnemonic (char charCode) => false;

        // The adapter is a Control and these are protected on WindowBase, so the cross-over needs an
        // internal bridge rather than a direct call.
        internal bool RaiseProcessCmdKey (ref Message msg, Keys keyData) => ProcessCmdKey (ref msg, keyData);

        internal bool RaiseProcessDialogKey (Keys keyData) => ProcessDialogKey (keyData);

        internal bool RaiseProcessDialogChar (char charCode) => ProcessDialogChar (charCode);

        internal bool RaiseProcessKeyPreview (ref Message m) => ProcessKeyPreview (ref m);

        internal bool RaiseProcessMnemonic (char charCode) => ProcessMnemonic (charCode);
    }
}
