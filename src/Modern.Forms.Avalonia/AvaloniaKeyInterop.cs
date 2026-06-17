using Avalonia.Input;
using AvaloniaPointerUpdateKind = Avalonia.Input.PointerUpdateKind;

namespace Modern.Forms;

/// <summary>
/// Maps Avalonia 12 Key enum values to Modern.Forms Keys (Windows Virtual Key codes).
/// Avalonia 12 uses WPF-style Key enum values which differ from VK codes.
/// </summary>
internal static class AvaloniaKeyInterop
{
    // Precomputed lookup table (index = Avalonia Key value, value = Forms VK code).
    // Avalonia Key values up to 175; initialised lazily below.
    private static readonly int[] _keyMap = BuildKeyMap ();

    private static int[] BuildKeyMap ()
    {
        var map = new int[256];

        // Letters: A(44)=0x41 … Z(69)=0x5A
        for (int i = 0; i < 26; i++)
            map[44 + i] = 0x41 + i;

        // Digits: D0(34)=0x30 … D9(43)=0x39
        for (int i = 0; i < 10; i++)
            map[34 + i] = 0x30 + i;

        // NumPad: NumPad0(74)=0x60 … NumPad9(83)=0x69
        for (int i = 0; i < 10; i++)
            map[74 + i] = 0x60 + i;

        // Function keys: F1(90)=0x70 … F24(113)=0x87
        for (int i = 0; i < 24; i++)
            map[90 + i] = 0x70 + i;

        // Control keys
        map[(int)Key.None]        = 0x00;
        map[(int)Key.Cancel]      = 0x03;
        map[(int)Key.Back]        = 0x08;
        map[(int)Key.Tab]         = 0x09;
        map[(int)Key.LineFeed]    = 0x0A;
        map[(int)Key.Clear]       = 0x0C;
        map[(int)Key.Return]      = 0x0D;
        map[(int)Key.Pause]       = 0x13;
        map[(int)Key.CapsLock]    = 0x14;
        map[(int)Key.Escape]      = 0x1B;
        map[(int)Key.Space]       = 0x20;
        map[(int)Key.PageUp]      = 0x21;
        map[(int)Key.PageDown]    = 0x22;
        map[(int)Key.End]         = 0x23;
        map[(int)Key.Home]        = 0x24;
        map[(int)Key.Left]        = 0x25;
        map[(int)Key.Up]          = 0x26;
        map[(int)Key.Right]       = 0x27;
        map[(int)Key.Down]        = 0x28;
        map[(int)Key.Select]      = 0x29;
        map[(int)Key.Print]       = 0x2A;
        map[(int)Key.Execute]     = 0x2B;
        map[(int)Key.PrintScreen] = 0x2C;
        map[(int)Key.Insert]      = 0x2D;
        map[(int)Key.Delete]      = 0x2E;
        map[(int)Key.Help]        = 0x2F;
        map[(int)Key.LWin]        = 0x5B;
        map[(int)Key.RWin]        = 0x5C;
        map[(int)Key.Apps]        = 0x5D;
        map[(int)Key.Sleep]       = 0x5F;
        map[(int)Key.Multiply]    = 0x6A;
        map[(int)Key.Add]         = 0x6B;
        map[(int)Key.Separator]   = 0x6C;
        map[(int)Key.Subtract]    = 0x6D;
        map[(int)Key.Decimal]     = 0x6E;
        map[(int)Key.Divide]      = 0x6F;
        map[(int)Key.NumLock]     = 0x90;
        map[(int)Key.Scroll]      = 0x91;
        map[(int)Key.LeftShift]   = 0xA0;
        map[(int)Key.RightShift]  = 0xA1;
        map[(int)Key.LeftCtrl]    = 0xA2;
        map[(int)Key.RightCtrl]   = 0xA3;
        map[(int)Key.LeftAlt]     = 0xA4;
        map[(int)Key.RightAlt]    = 0xA5;

        // Browser/Media keys
        map[(int)Key.BrowserBack]        = 0xA6;
        map[(int)Key.BrowserForward]     = 0xA7;
        map[(int)Key.BrowserRefresh]     = 0xA8;
        map[(int)Key.BrowserStop]        = 0xA9;
        map[(int)Key.BrowserSearch]      = 0xAA;
        map[(int)Key.BrowserFavorites]   = 0xAB;
        map[(int)Key.BrowserHome]        = 0xAC;
        map[(int)Key.VolumeMute]         = 0xAD;
        map[(int)Key.VolumeDown]         = 0xAE;
        map[(int)Key.VolumeUp]           = 0xAF;
        map[(int)Key.MediaNextTrack]     = 0xB0;
        map[(int)Key.MediaPreviousTrack] = 0xB1;
        map[(int)Key.MediaStop]          = 0xB2;
        map[(int)Key.MediaPlayPause]     = 0xB3;
        map[(int)Key.LaunchMail]         = 0xB4;
        map[(int)Key.SelectMedia]        = 0xB5;
        map[(int)Key.LaunchApplication1] = 0xB6;
        map[(int)Key.LaunchApplication2] = 0xB7;
        map[(int)Key.OemSemicolon]       = 0xBA;
        map[(int)Key.OemPlus]            = 0xBB;
        map[(int)Key.OemComma]           = 0xBC;
        map[(int)Key.OemMinus]           = 0xBD;
        map[(int)Key.OemPeriod]          = 0xBE;
        map[(int)Key.OemQuestion]        = 0xBF;
        map[(int)Key.Oem3]               = 0xC0;
        map[(int)Key.Oem4]               = 0xDB;
        map[(int)Key.OemPipe]            = 0xDC;
        map[(int)Key.OemCloseBrackets]   = 0xDD;
        map[(int)Key.OemQuotes]          = 0xDE;
        map[(int)Key.Oem8]               = 0xDF;
        map[(int)Key.OemBackslash]       = 0xE2;

        return map;
    }

    /// <summary>
    /// Converts an Avalonia <see cref="Key"/> to a Modern.Forms <see cref="Keys"/> value.
    /// </summary>
    internal static Keys ToFormsKey (Key key)
    {
        var idx = (int)key;

        if (idx >= 0 && idx < _keyMap.Length)
            return (Keys)_keyMap[idx];

        return Keys.None;
    }

    /// <summary>
    /// Adds modifier flags from Avalonia <see cref="KeyModifiers"/> to a <see cref="Keys"/> value.
    /// </summary>
    internal static Keys AddModifiers (Keys key, KeyModifiers mods)
    {
        if ((mods & KeyModifiers.Alt) != 0)     key |= Keys.Alt;
        if ((mods & KeyModifiers.Control) != 0) key |= Keys.Control;
        if ((mods & KeyModifiers.Shift) != 0)   key |= Keys.Shift;

        return key;
    }

    /// <summary>
    /// Builds a <see cref="Keys"/> value containing only the current modifier flags.
    /// </summary>
    internal static Keys ModifiersOnly (KeyModifiers mods)
        => AddModifiers (Keys.None, mods);

    /// <summary>
    /// Maps Avalonia pointer button state to <see cref="MouseButtons"/>.
    /// </summary>
    internal static MouseButtons ToMouseButtons (Avalonia.Input.PointerPointProperties props)
    {
        var buttons = MouseButtons.None;

        if (props.IsLeftButtonPressed)   buttons |= MouseButtons.Left;
        if (props.IsRightButtonPressed)  buttons |= MouseButtons.Right;
        if (props.IsMiddleButtonPressed) buttons |= MouseButtons.Middle;
        if (props.IsXButton1Pressed)     buttons |= MouseButtons.XButton1;
        if (props.IsXButton2Pressed)     buttons |= MouseButtons.XButton2;

        return buttons;
    }

    /// <summary>
    /// Converts the primary pointer button from an Avalonia pointer-released event.
    /// </summary>
    internal static MouseButtons ReleasedButton (AvaloniaPointerUpdateKind kind)
    {
        return kind switch {
            AvaloniaPointerUpdateKind.LeftButtonReleased   => MouseButtons.Left,
            AvaloniaPointerUpdateKind.RightButtonReleased  => MouseButtons.Right,
            AvaloniaPointerUpdateKind.MiddleButtonReleased => MouseButtons.Middle,
            AvaloniaPointerUpdateKind.XButton1Released     => MouseButtons.XButton1,
            AvaloniaPointerUpdateKind.XButton2Released     => MouseButtons.XButton2,
            _                                               => MouseButtons.None
        };
    }

    /// <summary>
    /// Converts the primary pointer button from an Avalonia pointer-pressed event.
    /// </summary>
    internal static MouseButtons PressedButton (AvaloniaPointerUpdateKind kind)
    {
        return kind switch {
            AvaloniaPointerUpdateKind.LeftButtonPressed   => MouseButtons.Left,
            AvaloniaPointerUpdateKind.RightButtonPressed  => MouseButtons.Right,
            AvaloniaPointerUpdateKind.MiddleButtonPressed => MouseButtons.Middle,
            AvaloniaPointerUpdateKind.XButton1Pressed     => MouseButtons.XButton1,
            AvaloniaPointerUpdateKind.XButton2Pressed     => MouseButtons.XButton2,
            _                                              => MouseButtons.None
        };
    }
}
