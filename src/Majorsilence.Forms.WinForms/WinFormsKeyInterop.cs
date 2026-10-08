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

        // Whether this backend has hidden the pointer with WF.Cursor.Hide. One flag for the process, not
        // one per window: every window is handed CursorType.None while MF.Cursor.Hide is in effect, so
        // at most one WF.Cursor.Hide is ever outstanding and the first visible cursor undoes it.
        private static bool cursor_hidden;

        /// <summary>
        /// Shows <paramref name="cursor"/> over <paramref name="target"/>. WinForms has no hidden cursor
        /// object, so <see cref="CursorType.None"/> goes through <c>WF.Cursor.Hide</c>/<c>Show</c> (the
        /// ShowCursor counter upstream's <c>Cursor.Hide</c> uses), kept balanced by a single flag.
        /// </summary>
        internal static void ApplyCursor (WF.Control target, CursorType cursor)
        {
            var hide = cursor == CursorType.None;

            if (hide != cursor_hidden) {
                if (hide)
                    WF.Cursor.Hide ();
                else
                    WF.Cursor.Show ();

                cursor_hidden = hide;
            }

            if (!hide)
                target.Cursor = ToCursor (cursor);
        }

        // One native cursor per loaded image: the core hands the same SKBitmap back each time that cursor
        // is shown, which is every mouse move over its control.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SkiaSharp.SKBitmap, WF.Cursor> custom_cursors
            = new System.Runtime.CompilerServices.ConditionalWeakTable<SkiaSharp.SKBitmap, WF.Cursor> ();

        /// <summary>
        /// Shows a cursor loaded from .cur/.ico data over <paramref name="target"/> (SVC-38), through the real
        /// <c>WF.Cursor (Stream)</c>: the image is written back as a one-frame .cur with its hotspot. Undoes
        /// this backend's <c>WF.Cursor.Hide</c> first, as any visible cursor does in <see cref="ApplyCursor"/>.
        /// </summary>
        internal static void ApplyCustomCursor (WF.Control target, SkiaSharp.SKBitmap image, System.Drawing.Point hotSpot)
        {
            if (cursor_hidden) {
                WF.Cursor.Show ();
                cursor_hidden = false;
            }

            target.Cursor = custom_cursors.GetValue (image, i => new WF.Cursor (new System.IO.MemoryStream (CursorFile.Encode (i, hotSpot))));
        }

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
