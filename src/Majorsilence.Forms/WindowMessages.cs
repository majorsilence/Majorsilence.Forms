namespace Majorsilence.Forms
{
    /// <summary>
    /// The Win32 message ids reported to <see cref="IMessageFilter"/> implementations.
    /// </summary>
    /// <remarks>
    /// Majorsilence.Forms does not run a Win32 message loop, but ported filters switch on these
    /// constants, so the synthesised <see cref="Message"/> handed to a filter carries the id the same
    /// input would have had on Windows. Only input messages are raised — there is no equivalent of the
    /// window-management or painting messages.
    /// </remarks>
    public static class WindowMessages
    {
        /// <summary>A key was pressed.</summary>
        public const int WM_KEYDOWN = 0x0100;

        /// <summary>A key was released.</summary>
        public const int WM_KEYUP = 0x0101;

        /// <summary>A character was typed.</summary>
        public const int WM_CHAR = 0x0102;

        /// <summary>A key was pressed with Alt held. Recognised by the pre-processing chain so an
        /// <c>Alt</c>-modified shortcut reaches <c>ProcessCmdKey</c> the way it does upstream.</summary>
        public const int WM_SYSKEYDOWN = 0x0104;

        /// <summary>A character was typed with Alt held — how a mnemonic arrives upstream.</summary>
        public const int WM_SYSCHAR = 0x0106;

        /// <summary>The pointer moved.</summary>
        public const int WM_MOUSEMOVE = 0x0200;

        /// <summary>The left button went down.</summary>
        public const int WM_LBUTTONDOWN = 0x0201;

        /// <summary>The left button went up.</summary>
        public const int WM_LBUTTONUP = 0x0202;

        /// <summary>The right button went down.</summary>
        public const int WM_RBUTTONDOWN = 0x0204;

        /// <summary>The right button went up.</summary>
        public const int WM_RBUTTONUP = 0x0205;

        /// <summary>The middle button went down.</summary>
        public const int WM_MBUTTONDOWN = 0x0207;

        /// <summary>The middle button went up.</summary>
        public const int WM_MBUTTONUP = 0x0208;

        // Packs a point the way MAKELPARAM does for the mouse messages, so a filter that unpacks
        // lParam with the usual LOWORD/HIWORD arithmetic reads the coordinates it expects.
        internal static System.IntPtr MakeMouseLParam (int x, int y)
            => (System.IntPtr)((y << 16) | (x & 0xFFFF));

        internal static int ButtonDownMessage (MouseButtons button) => button switch {
            MouseButtons.Right => WM_RBUTTONDOWN,
            MouseButtons.Middle => WM_MBUTTONDOWN,
            _ => WM_LBUTTONDOWN,
        };

        internal static int ButtonUpMessage (MouseButtons button) => button switch {
            MouseButtons.Right => WM_RBUTTONUP,
            MouseButtons.Middle => WM_MBUTTONUP,
            _ => WM_LBUTTONUP,
        };
    }
}
