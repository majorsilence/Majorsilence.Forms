using Gdk;
using MF = Majorsilence.Forms;
using CursorType = Majorsilence.Forms.Backends.CursorType;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Translates GDK input (key values, modifier masks, gesture buttons, scroll deltas) into the
    /// neutral Majorsilence.Forms enums. <see cref="MF.Keys"/> and <see cref="MF.MouseButtons"/> are the
    /// upstream WinForms enums verbatim (Windows Virtual-Key codes / values); GDK key values are a
    /// different numbering (X11 keysyms), so they go through <see cref="ToKeys"/>.
    /// </summary>
    internal static class Gtk4KeyInterop
    {
        // GDK / X11 keysym constants (the ones Majorsilence.Forms actually routes on).
        private const uint GDK_BackSpace = 0xff08, GDK_Tab = 0xff09, GDK_Return = 0xff0d, GDK_Escape = 0xff1b;
        private const uint GDK_Home = 0xff50, GDK_Left = 0xff51, GDK_Up = 0xff52, GDK_Right = 0xff53, GDK_Down = 0xff54;
        private const uint GDK_PageUp = 0xff55, GDK_PageDown = 0xff56, GDK_End = 0xff57, GDK_Insert = 0xff63;
        private const uint GDK_KP_Enter = 0xff8d, GDK_KP_0 = 0xffb0, GDK_KP_9 = 0xffb9;
        private const uint GDK_F1 = 0xffbe, GDK_F24 = 0xffd5;
        private const uint GDK_Delete = 0xffff;
        private const uint GDK_Shift_L = 0xffe1, GDK_Shift_R = 0xffe2, GDK_Control_L = 0xffe3, GDK_Control_R = 0xffe4;
        private const uint GDK_Caps_Lock = 0xffe5, GDK_Alt_L = 0xffe9, GDK_Alt_R = 0xffea;
        private const uint GDK_Menu = 0xff67;

        /// <summary>Converts a GDK key value to the Majorsilence.Forms Virtual-Key value, modifiers ORed on.</summary>
        internal static MF.Keys ToKeys (uint keyval, ModifierType state)
        {
            var key = ToKeysCore (keyval);
            return AddModifiers (key, state);
        }

        private static MF.Keys ToKeysCore (uint keyval)
        {
            // ASCII letters: keysym == the character; Keys.A..Keys.Z are 0x41..0x5A.
            if (keyval >= 'a' && keyval <= 'z') return (MF.Keys) (keyval - 'a' + (uint) MF.Keys.A);
            if (keyval >= 'A' && keyval <= 'Z') return (MF.Keys) keyval;
            if (keyval >= '0' && keyval <= '9') return (MF.Keys) keyval;           // Keys.D0..D9 == '0'..'9'
            if (keyval == ' ') return MF.Keys.Space;

            if (keyval >= GDK_KP_0 && keyval <= GDK_KP_9)
                return (MF.Keys) (keyval - GDK_KP_0 + (uint) MF.Keys.NumPad0);
            if (keyval >= GDK_F1 && keyval <= GDK_F24)
                return (MF.Keys) (keyval - GDK_F1 + (uint) MF.Keys.F1);

            return keyval switch {
                GDK_BackSpace => MF.Keys.Back,
                GDK_Tab => MF.Keys.Tab,
                GDK_Return or GDK_KP_Enter => MF.Keys.Return,
                GDK_Escape => MF.Keys.Escape,
                GDK_Home => MF.Keys.Home,
                GDK_Left => MF.Keys.Left,
                GDK_Up => MF.Keys.Up,
                GDK_Right => MF.Keys.Right,
                GDK_Down => MF.Keys.Down,
                GDK_PageUp => MF.Keys.PageUp,
                GDK_PageDown => MF.Keys.PageDown,
                GDK_End => MF.Keys.End,
                GDK_Insert => MF.Keys.Insert,
                GDK_Delete => MF.Keys.Delete,
                GDK_Shift_L or GDK_Shift_R => MF.Keys.ShiftKey,
                GDK_Control_L or GDK_Control_R => MF.Keys.ControlKey,
                GDK_Alt_L or GDK_Alt_R => MF.Keys.Menu,
                GDK_Caps_Lock => MF.Keys.CapsLock,
                GDK_Menu => MF.Keys.Apps,
                _ => MF.Keys.None,
            };
        }

        /// <summary>ORs the GDK modifier mask onto a <see cref="MF.Keys"/> value.</summary>
        internal static MF.Keys AddModifiers (MF.Keys key, ModifierType state)
        {
            if ((state & ModifierType.ShiftMask) != 0) key |= MF.Keys.Shift;
            if ((state & ModifierType.ControlMask) != 0) key |= MF.Keys.Control;
            if ((state & ModifierType.AltMask) != 0) key |= MF.Keys.Alt;
            return key;
        }

        /// <summary>The modifier flags currently held, as <see cref="MF.Keys"/>.</summary>
        internal static MF.Keys ModifierKeys (ModifierType state) => AddModifiers (MF.Keys.None, state);

        /// <summary>Maps a GDK/GTK gesture button number (1=left, 2=middle, 3=right, 8/9=X) to <see cref="MF.MouseButtons"/>.</summary>
        internal static MF.MouseButtons ToButton (uint button) => button switch {
            1 => MF.MouseButtons.Left,
            2 => MF.MouseButtons.Middle,
            3 => MF.MouseButtons.Right,
            8 => MF.MouseButtons.XButton1,
            9 => MF.MouseButtons.XButton2,
            _ => MF.MouseButtons.None,
        };

        /// <summary>The mouse buttons currently held, from a GDK modifier mask.</summary>
        internal static MF.MouseButtons ButtonsFromState (ModifierType state)
        {
            var b = MF.MouseButtons.None;
            if ((state & ModifierType.Button1Mask) != 0) b |= MF.MouseButtons.Left;
            if ((state & ModifierType.Button2Mask) != 0) b |= MF.MouseButtons.Middle;
            if ((state & ModifierType.Button3Mask) != 0) b |= MF.MouseButtons.Right;
            return b;
        }

        /// <summary>The CSS/GTK cursor name for a backend-neutral <see cref="CursorType"/>.</summary>
        internal static string ToCursorName (CursorType cursor) => cursor switch {
            CursorType.AppStarting or CursorType.Wait => "wait",
            CursorType.Cross => "crosshair",
            CursorType.Hand => "pointer",
            CursorType.Help => "help",
            CursorType.Ibeam => "text",
            CursorType.No => "not-allowed",
            CursorType.UpArrow => "n-resize",
            CursorType.SizeAll or CursorType.DragMove => "move",
            CursorType.SizeNorthSouth or CursorType.TopSide or CursorType.BottomSide => "ns-resize",
            CursorType.SizeWestEast or CursorType.LeftSide or CursorType.RightSide => "ew-resize",
            CursorType.TopLeftCorner or CursorType.BottomRightCorner => "nwse-resize",
            CursorType.TopRightCorner or CursorType.BottomLeftCorner => "nesw-resize",
            CursorType.DragCopy => "copy",
            CursorType.DragLink => "alias",
            _ => "default",
        };

        /// <summary>Maps a backend-neutral resize edge to the GDK surface edge.</summary>
        internal static SurfaceEdge ToSurfaceEdge (Majorsilence.Forms.Backends.WindowEdge edge) => edge switch {
            Majorsilence.Forms.Backends.WindowEdge.North => SurfaceEdge.North,
            Majorsilence.Forms.Backends.WindowEdge.NorthEast => SurfaceEdge.NorthEast,
            Majorsilence.Forms.Backends.WindowEdge.East => SurfaceEdge.East,
            Majorsilence.Forms.Backends.WindowEdge.SouthEast => SurfaceEdge.SouthEast,
            Majorsilence.Forms.Backends.WindowEdge.South => SurfaceEdge.South,
            Majorsilence.Forms.Backends.WindowEdge.SouthWest => SurfaceEdge.SouthWest,
            Majorsilence.Forms.Backends.WindowEdge.West => SurfaceEdge.West,
            _ => SurfaceEdge.NorthWest,
        };
    }
}
