using Majorsilence.Forms.Backends;
using WF = System.Windows.Forms;
using MF = Majorsilence.Forms;

namespace Majorsilence.Forms.WinForms
{
    /// <summary>
    /// Translates System.Windows.Forms input types to their Majorsilence.Forms equivalents.
    /// Majorsilence.Forms' <see cref="MF.Keys"/> and <see cref="MF.MouseButtons"/> are the upstream
    /// WinForms enums verbatim (same names, same numeric values), so those conversions are plain
    /// integer casts rather than lookup tables.
    /// </summary>
    internal static class WinFormsKeyInterop
    {
        /// <summary>Converts WinForms key data (key code + modifiers) to Majorsilence.Forms keys.</summary>
        internal static MF.Keys ToKeys (WF.Keys keyData) => (MF.Keys) (int) keyData;

        /// <summary>Converts a WinForms mouse button to the Majorsilence.Forms equivalent.</summary>
        internal static MF.MouseButtons ToButton (WF.MouseButtons button) => (MF.MouseButtons) (int) button;

        /// <summary>The Majorsilence.Forms modifier keys currently held down, from the WinForms keyboard state.</summary>
        internal static MF.Keys CurrentModifiers () => (MF.Keys) (int) WF.Control.ModifierKeys;

        /// <summary>Maps a backend-neutral cursor to the corresponding WinForms cursor.</summary>
        internal static WF.Cursor ToCursor (CursorType cursor) => cursor switch {
            CursorType.AppStarting => WF.Cursors.AppStarting,
            CursorType.Cross => WF.Cursors.Cross,
            CursorType.Hand => WF.Cursors.Hand,
            CursorType.Help => WF.Cursors.Help,
            CursorType.Ibeam => WF.Cursors.IBeam,
            CursorType.No => WF.Cursors.No,
            CursorType.UpArrow => WF.Cursors.UpArrow,
            CursorType.Wait => WF.Cursors.WaitCursor,
            CursorType.SizeAll or CursorType.DragMove => WF.Cursors.SizeAll,
            CursorType.SizeNorthSouth or CursorType.TopSide or CursorType.BottomSide => WF.Cursors.SizeNS,
            CursorType.SizeWestEast or CursorType.LeftSide or CursorType.RightSide => WF.Cursors.SizeWE,
            CursorType.TopLeftCorner or CursorType.BottomRightCorner => WF.Cursors.SizeNWSE,
            CursorType.TopRightCorner or CursorType.BottomLeftCorner => WF.Cursors.SizeNESW,
            // WinForms has no dedicated drag-copy/drag-link cursors outside an OLE drag loop.
            CursorType.DragCopy or CursorType.DragLink => WF.Cursors.Default,
            _ => WF.Cursors.Default,
        };

        /// <summary>
        /// Converts a Win32 wheel delta (±120 per notch) to the small "notch count" Majorsilence.Forms'
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
