namespace Majorsilence.Forms.Terminal
{
    /// <summary>What kind of thing the user did, as decoded from the terminal's input stream.</summary>
    internal enum TerminalInputKind
    {
        /// <summary>A key press. <see cref="TerminalInput.Key"/> is set (possibly <see cref="Keys.None"/> for a symbol with no WinForms key) and <see cref="TerminalInput.Text"/> carries the character it types, if any.</summary>
        Key,
        /// <summary>Text with no key behind it: a bracketed paste.</summary>
        Text,
        /// <summary>A mouse button went down.</summary>
        MouseDown,
        /// <summary>A mouse button went up.</summary>
        MouseUp,
        /// <summary>The pointer moved, with <see cref="TerminalInput.Button"/> held if any.</summary>
        MouseMove,
        /// <summary>The wheel turned; <see cref="TerminalInput.WheelX"/>/<see cref="TerminalInput.WheelY"/> are in WinForms units (120 per notch, positive up/right).</summary>
        Wheel,
        /// <summary>The terminal answered a cell-size query: <see cref="TerminalInput.Col"/> is the cell width and <see cref="TerminalInput.Row"/> its height, in pixels.</summary>
        CellSize,
        /// <summary>The terminal answered a primary device attributes query (<c>ESC[c</c>); <see cref="TerminalInput.Text"/> holds the attribute list, e.g. <c>62;4;22</c>, where 4 means Sixel. Terminals answer queries in order, so this arrives after the replies to anything asked before it.</summary>
        DeviceAttributes,
        /// <summary>The terminal answered a Kitty keyboard protocol query; <see cref="TerminalInput.Col"/> holds the flags currently in force.</summary>
        KeyboardFlags,
        /// <summary>The terminal answered a Kitty graphics command; <see cref="TerminalInput.Col"/> is the image id and <see cref="TerminalInput.Text"/> is <c>OK</c> or the error.</summary>
        GraphicsReply,
    }

    /// <summary>What a key event is. Only the Kitty keyboard protocol reports repeats and releases; a legacy terminal reports presses alone.</summary>
    internal enum KeyEventKind
    {
        /// <summary>The key went down.</summary>
        Press,
        /// <summary>The key is held and repeating.</summary>
        Repeat,
        /// <summary>The key came up.</summary>
        Release,
    }

    /// <summary>One decoded input event. Mouse positions are zero-based, in character cells unless <see cref="InPixels"/>.</summary>
    internal readonly record struct TerminalInput (
        TerminalInputKind Kind,
        Keys Key = Keys.None,
        string? Text = null,
        MouseButtons Button = MouseButtons.None,
        int Col = 0,
        int Row = 0,
        int WheelX = 0,
        int WheelY = 0,
        bool InPixels = false,
        KeyEventKind Event = KeyEventKind.Press);
}
