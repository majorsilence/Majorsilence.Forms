using System;
using WI = System.Windows.Input;
using MF = Majorsilence.Forms;
using CursorType = Majorsilence.Forms.Backends.CursorType;

namespace Majorsilence.Forms.Wpf
{
    // Every WPF input type is written WI-prefixed: this namespace is nested under Majorsilence.Forms,
    // which has its own MouseEventArgs / KeyEventArgs / Cursor / Cursors (the WinForms-compat surface),
    // and enclosing-namespace members beat using-alias directives in C# name lookup.

    /// <summary>
    /// Translates WPF (<c>System.Windows.Input</c>) input types to their Majorsilence.Forms equivalents.
    /// Majorsilence.Forms' <see cref="MF.Keys"/> and <see cref="MF.MouseButtons"/> are the upstream
    /// WinForms enums verbatim (Windows Virtual-Key codes / values), but WPF's <c>Key</c> enum is a
    /// different numbering, so key codes go through a lookup table. WPF's <c>Key</c> enum happens to be
    /// value-identical to Avalonia 12's, so this table mirrors AvaloniaKeyInterop.
    /// </summary>
    internal static class WpfKeyInterop
    {
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

            map[(int) WI.Key.None]        = 0x00;
            map[(int) WI.Key.Cancel]      = 0x03;
            map[(int) WI.Key.Back]        = 0x08;
            map[(int) WI.Key.Tab]         = 0x09;
            map[(int) WI.Key.LineFeed]    = 0x0A;
            map[(int) WI.Key.Clear]       = 0x0C;
            map[(int) WI.Key.Return]      = 0x0D;   // == Key.Enter
            map[(int) WI.Key.Pause]       = 0x13;
            map[(int) WI.Key.Capital]     = 0x14;   // == Key.CapsLock
            map[(int) WI.Key.Escape]      = 0x1B;
            map[(int) WI.Key.Space]       = 0x20;
            map[(int) WI.Key.Prior]       = 0x21;   // == Key.PageUp
            map[(int) WI.Key.Next]        = 0x22;   // == Key.PageDown
            map[(int) WI.Key.End]         = 0x23;
            map[(int) WI.Key.Home]        = 0x24;
            map[(int) WI.Key.Left]        = 0x25;
            map[(int) WI.Key.Up]          = 0x26;
            map[(int) WI.Key.Right]       = 0x27;
            map[(int) WI.Key.Down]        = 0x28;
            map[(int) WI.Key.Select]      = 0x29;
            map[(int) WI.Key.Print]       = 0x2A;
            map[(int) WI.Key.Execute]     = 0x2B;
            map[(int) WI.Key.Snapshot]    = 0x2C;   // == Key.PrintScreen
            map[(int) WI.Key.Insert]      = 0x2D;
            map[(int) WI.Key.Delete]      = 0x2E;
            map[(int) WI.Key.Help]        = 0x2F;
            map[(int) WI.Key.LWin]        = 0x5B;
            map[(int) WI.Key.RWin]        = 0x5C;
            map[(int) WI.Key.Apps]        = 0x5D;
            map[(int) WI.Key.Sleep]       = 0x5F;
            map[(int) WI.Key.Multiply]    = 0x6A;
            map[(int) WI.Key.Add]         = 0x6B;
            map[(int) WI.Key.Separator]   = 0x6C;
            map[(int) WI.Key.Subtract]    = 0x6D;
            map[(int) WI.Key.Decimal]     = 0x6E;
            map[(int) WI.Key.Divide]      = 0x6F;
            map[(int) WI.Key.NumLock]     = 0x90;
            map[(int) WI.Key.Scroll]      = 0x91;
            map[(int) WI.Key.LeftShift]   = 0xA0;
            map[(int) WI.Key.RightShift]  = 0xA1;
            map[(int) WI.Key.LeftCtrl]    = 0xA2;
            map[(int) WI.Key.RightCtrl]   = 0xA3;
            map[(int) WI.Key.LeftAlt]     = 0xA4;
            map[(int) WI.Key.RightAlt]    = 0xA5;

            map[(int) WI.Key.BrowserBack]        = 0xA6;
            map[(int) WI.Key.BrowserForward]     = 0xA7;
            map[(int) WI.Key.BrowserRefresh]     = 0xA8;
            map[(int) WI.Key.BrowserStop]        = 0xA9;
            map[(int) WI.Key.BrowserSearch]      = 0xAA;
            map[(int) WI.Key.BrowserFavorites]   = 0xAB;
            map[(int) WI.Key.BrowserHome]        = 0xAC;
            map[(int) WI.Key.VolumeMute]         = 0xAD;
            map[(int) WI.Key.VolumeDown]         = 0xAE;
            map[(int) WI.Key.VolumeUp]           = 0xAF;
            map[(int) WI.Key.MediaNextTrack]     = 0xB0;
            map[(int) WI.Key.MediaPreviousTrack] = 0xB1;
            map[(int) WI.Key.MediaStop]          = 0xB2;
            map[(int) WI.Key.MediaPlayPause]     = 0xB3;
            map[(int) WI.Key.LaunchMail]         = 0xB4;
            map[(int) WI.Key.SelectMedia]        = 0xB5;
            map[(int) WI.Key.LaunchApplication1] = 0xB6;
            map[(int) WI.Key.LaunchApplication2] = 0xB7;
            map[(int) WI.Key.OemSemicolon]       = 0xBA;
            map[(int) WI.Key.OemPlus]            = 0xBB;
            map[(int) WI.Key.OemComma]           = 0xBC;
            map[(int) WI.Key.OemMinus]           = 0xBD;
            map[(int) WI.Key.OemPeriod]          = 0xBE;
            map[(int) WI.Key.OemQuestion]        = 0xBF;
            map[(int) WI.Key.OemTilde]           = 0xC0;
            map[(int) WI.Key.OemOpenBrackets]    = 0xDB;
            map[(int) WI.Key.OemPipe]            = 0xDC;
            map[(int) WI.Key.OemCloseBrackets]   = 0xDD;
            map[(int) WI.Key.OemQuotes]          = 0xDE;
            map[(int) WI.Key.Oem8]               = 0xDF;
            map[(int) WI.Key.OemBackslash]       = 0xE2;

            return map;
        }

        /// <summary>Converts a WPF <c>Key</c> to the Majorsilence.Forms Virtual-Key value.</summary>
        internal static MF.Keys ToFormsKey (WI.Key key)
        {
            var idx = (int) key;
            return idx >= 0 && idx < _keyMap.Length ? (MF.Keys) _keyMap[idx] : MF.Keys.None;
        }

        /// <summary>ORs the current modifier flags onto a <see cref="MF.Keys"/> value.</summary>
        internal static MF.Keys AddModifiers (MF.Keys key, WI.ModifierKeys mods)
        {
            if ((mods & WI.ModifierKeys.Alt) != 0)     key |= MF.Keys.Alt;
            if ((mods & WI.ModifierKeys.Control) != 0) key |= MF.Keys.Control;
            if ((mods & WI.ModifierKeys.Shift) != 0)   key |= MF.Keys.Shift;
            return key;
        }

        /// <summary>The Majorsilence.Forms modifier flags currently held down.</summary>
        internal static MF.Keys CurrentModifiers () => AddModifiers (MF.Keys.None, WI.Keyboard.Modifiers);

        /// <summary>Maps the pressed mouse buttons from a WPF mouse event to <see cref="MF.MouseButtons"/>.</summary>
        internal static MF.MouseButtons CurrentButtons (WI.MouseEventArgs e)
        {
            var b = MF.MouseButtons.None;
            if (e.LeftButton == WI.MouseButtonState.Pressed)   b |= MF.MouseButtons.Left;
            if (e.RightButton == WI.MouseButtonState.Pressed)  b |= MF.MouseButtons.Right;
            if (e.MiddleButton == WI.MouseButtonState.Pressed) b |= MF.MouseButtons.Middle;
            if (e.XButton1 == WI.MouseButtonState.Pressed)     b |= MF.MouseButtons.XButton1;
            if (e.XButton2 == WI.MouseButtonState.Pressed)     b |= MF.MouseButtons.XButton2;
            return b;
        }

        /// <summary>Converts a single WPF <c>MouseButton</c> to <see cref="MF.MouseButtons"/>.</summary>
        internal static MF.MouseButtons ToButton (WI.MouseButton button) => button switch {
            WI.MouseButton.Left     => MF.MouseButtons.Left,
            WI.MouseButton.Right    => MF.MouseButtons.Right,
            WI.MouseButton.Middle   => MF.MouseButtons.Middle,
            WI.MouseButton.XButton1 => MF.MouseButtons.XButton1,
            WI.MouseButton.XButton2 => MF.MouseButtons.XButton2,
            _                       => MF.MouseButtons.None,
        };

        /// <summary>Maps a backend-neutral cursor to the corresponding WPF cursor.</summary>
        internal static WI.Cursor ToCursor (CursorType cursor) => cursor switch {
            CursorType.AppStarting => WI.Cursors.AppStarting,
            CursorType.Cross => WI.Cursors.Cross,
            CursorType.Hand => WI.Cursors.Hand,
            CursorType.Help => WI.Cursors.Help,
            CursorType.Ibeam => WI.Cursors.IBeam,
            CursorType.No => WI.Cursors.No,
            CursorType.UpArrow => WI.Cursors.UpArrow,
            CursorType.Wait => WI.Cursors.Wait,
            CursorType.SizeAll or CursorType.DragMove => WI.Cursors.SizeAll,
            CursorType.SizeNorthSouth or CursorType.TopSide or CursorType.BottomSide => WI.Cursors.SizeNS,
            CursorType.SizeWestEast or CursorType.LeftSide or CursorType.RightSide => WI.Cursors.SizeWE,
            CursorType.TopLeftCorner or CursorType.BottomRightCorner => WI.Cursors.SizeNWSE,
            CursorType.TopRightCorner or CursorType.BottomLeftCorner => WI.Cursors.SizeNESW,
            _ => WI.Cursors.Arrow,
        };

        /// <summary>
        /// Converts a WPF wheel delta (±120 per notch) to the small "notch count" Majorsilence.Forms'
        /// scrollbars expect, preserving direction for fractional (precision-touchpad) deltas.
        /// </summary>
        internal static int NotchesFromWheelDelta (int rawDelta)
        {
            const int WheelDeltaPerNotch = 120;
            if (rawDelta == 0)
                return 0;
            var notches = (int) Math.Round (rawDelta / (double) WheelDeltaPerNotch, MidpointRounding.AwayFromZero);
            return notches != 0 ? notches : Math.Sign (rawDelta);
        }
    }
}
