using Microsoft.UI.Input;
using Modern.Forms.Backends;
using Windows.System;

namespace Modern.Forms.Uno
{
    /// <summary>Translates Uno/WinUI input types into the neutral Modern.Forms enums.</summary>
    internal static class UnoKeyInterop
    {
        public static MouseButtons ToButton (PointerPointProperties props)
        {
            var buttons = MouseButtons.None;
            if (props.IsLeftButtonPressed) buttons |= MouseButtons.Left;
            if (props.IsRightButtonPressed) buttons |= MouseButtons.Right;
            if (props.IsMiddleButtonPressed) buttons |= MouseButtons.Middle;
            return buttons;
        }

        public static Keys ToKeys (VirtualKey key)
        {
            // Letters A–Z and digits 0–9 map directly (VirtualKey values match ASCII / Keys here).
            if (key >= VirtualKey.A && key <= VirtualKey.Z)
                return Keys.A + (key - VirtualKey.A);
            if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
                return Keys.D0 + (key - VirtualKey.Number0);

            return key switch {
                VirtualKey.Back => Keys.Back,
                VirtualKey.Tab => Keys.Tab,
                VirtualKey.Enter => Keys.Return,
                VirtualKey.Escape => Keys.Escape,
                VirtualKey.Space => Keys.Space,
                VirtualKey.Left => Keys.Left,
                VirtualKey.Up => Keys.Up,
                VirtualKey.Right => Keys.Right,
                VirtualKey.Down => Keys.Down,
                VirtualKey.Delete => Keys.Delete,
                VirtualKey.Home => Keys.Home,
                VirtualKey.End => Keys.End,
                VirtualKey.PageUp => Keys.PageUp,
                VirtualKey.PageDown => Keys.PageDown,
                VirtualKey.Shift => Keys.ShiftKey,
                VirtualKey.Control => Keys.ControlKey,
                VirtualKey.Menu => Keys.Menu,
                _ => Keys.None
            };
        }

        public static InputSystemCursorShape ToCursorShape (CursorType cursor) => cursor switch {
            CursorType.Hand => InputSystemCursorShape.Hand,
            CursorType.Ibeam => InputSystemCursorShape.IBeam,
            CursorType.Wait => InputSystemCursorShape.Wait,
            CursorType.AppStarting => InputSystemCursorShape.AppStarting,
            CursorType.Cross => InputSystemCursorShape.Cross,
            CursorType.Help => InputSystemCursorShape.Help,
            CursorType.No => InputSystemCursorShape.UniversalNo,
            CursorType.UpArrow => InputSystemCursorShape.UpArrow,
            CursorType.SizeAll => InputSystemCursorShape.SizeAll,
            CursorType.SizeNorthSouth or CursorType.TopSide or CursorType.BottomSide => InputSystemCursorShape.SizeNorthSouth,
            CursorType.SizeWestEast or CursorType.LeftSide or CursorType.RightSide => InputSystemCursorShape.SizeWestEast,
            CursorType.TopLeftCorner or CursorType.BottomRightCorner => InputSystemCursorShape.SizeNorthwestSoutheast,
            CursorType.TopRightCorner or CursorType.BottomLeftCorner => InputSystemCursorShape.SizeNortheastSouthwest,
            _ => InputSystemCursorShape.Arrow
        };
    }
}
