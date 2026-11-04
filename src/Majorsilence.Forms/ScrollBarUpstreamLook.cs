using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the scroll bar
    // Windows 11 draws for WinForms, measured from it: a plain SystemColors.Control track with no arrow
    // boxes, and a thin rounded thumb -- 2px wide while the pointer is elsewhere, wider with small arrows
    // while it is over the bar. The geometry (arrow areas at each end, the thumb between them) is the
    // classic one; only the drawing changes. The theme's boxed arrows and broad thumb read as a different
    // control beside WinForms'. Apps that have not chosen a font keep the theme's scroll bar.
    public abstract partial class ScrollBar
    {
        internal static readonly SKColor UpstreamTrack = new SKColor (0xF0, 0xF0, 0xF0);
        internal static readonly SKColor UpstreamThumb = new SKColor (0x85, 0x85, 0x85);

        // The one-pixel line upstream draws along the bar's leading edge: the left column of a vertical bar,
        // the top row of a horizontal one. The thumb is centred on the track beside it.
        internal static readonly SKColor UpstreamEdge = SKColors.White;

        // Upstream's bar, measured under Windows 11 (probes/wfscrollw): SystemInformation.VerticalScrollBarWidth,
        // HorizontalScrollBarHeight and both arrow lengths are 17 at 96 DPI and 34 at 192, and so is the bar
        // every ListBox, TreeView, multiline TextBox, AutoScroll Panel and DataGridView shows (Telerik's grid's
        // too). The theme's bar is 15: a ported form's lists were 2px wider inside than upstream's.
        internal const int UpstreamThickness = 17;

        private const int ThemeThickness = 15;

        // The shortest thumb upstream draws. It hit-tests a shorter one (8px, as here) on a long list.
        internal const int UpstreamDrawnThumbMinimum = 17;

        /// <summary>The width of a vertical bar and the height of a horizontal one, in logical pixels.</summary>
        /// <remarks>Read when a bar is created (its default size) and when a ScrollableControl lays its bars out.</remarks>
        internal static int DefaultThickness => ControlPaint.UsesUpstreamGlyphs ? UpstreamThickness : ThemeThickness;

        /// <summary>Whether this scroll bar is drawn as upstream's Windows 11 one.</summary>
        internal bool PaintsUpstreamLook => ControlPaint.UsesUpstreamGlyphs && !TouchStyle;

        /// <inheritdoc/>
        protected override void OnMouseEnter (System.EventArgs e)
        {
            base.OnMouseEnter (e);

            // The thumb widens and the arrows appear under the pointer.
            if (PaintsUpstreamLook)
                Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (System.EventArgs e)
        {
            base.OnMouseLeave (e);

            if (PaintsUpstreamLook)
                Invalidate ();
        }
    }
}
