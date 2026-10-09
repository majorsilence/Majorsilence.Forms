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
