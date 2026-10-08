using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the tab control
    // WinForms draws under the Windows 11 theme, measured from it: tabs sized to their caption and
    // TabControl.Padding (6, 3 by default), starting 2px in; a pale bordered box per tab with the selected
    // one raised and open into the page; and the page inside a bordered frame, at upstream's
    // DisplayRectangle (4, row + 4, width - 8, height - row - 8). The theme's flat underlined captions and
    // edge-to-edge page put every control on a tab page 4px left of where the designer placed it.
    // Top-aligned, normal-appearance, non-owner-drawn tabs only. Apps that have not chosen a font keep
    // the theme's tabs.
    public partial class TabControl
    {
        internal static readonly SKColor UpstreamTabBorder = new SKColor (0xE5, 0xE5, 0xE5);
        internal static readonly SKColor UpstreamTabFill = new SKColor (0xF3, 0xF3, 0xF3);
        internal static readonly SKColor UpstreamSelectedTabFill = new SKColor (0xF9, 0xF9, 0xF9);

        // Where the tabs start, and how far the page is inset from the frame's edges.
        internal const int UpstreamTabOrigin = 2;
        internal const int UpstreamPageInset = 4;

        /// <summary>Whether this tab control lays out and paints as upstream's themed one.</summary>
        internal bool UsesUpstreamTabs
            => ControlPaint.UsesUpstreamGlyphs
               && Appearance == TabAppearance.Normal
               && Alignment == TabAlignment.Top
               && !IsOwnerDrawn
               && TabStripVisible;

        /// <summary>The tab padding in effect: as set, else upstream's (6, 3) default under its look.</summary>
        internal System.Drawing.Point EffectiveTabPadding
            => !tab_padding_set && UsesUpstreamTabs ? new System.Drawing.Point (6, 3) : tab_padding;

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            // A page fills the space under the strip (Dock = Fill) unless upstream's inset applies, which
            // the dock pass cannot express: it would inset the strip too. Undocked, the page is placed here.
            var upstream = UsesUpstreamTabs;

            foreach (var page in TabPages) {
                var dock = upstream ? DockStyle.None : DockStyle.Fill;
                if (page.Dock != dock)
                    page.Dock = dock;
            }

            base.OnLayout (e);

            if (!upstream)
                return;

            // Upstream's DisplayRectangle: the page is inset from the frame on three sides and starts
            // below the strip, which already includes the tabs' 2px origin and the frame's top edge.
            var top = TabStrip.Height;
            var bounds = new System.Drawing.Rectangle (UpstreamPageInset, top,
                System.Math.Max (0, Width - 2 * UpstreamPageInset),
                System.Math.Max (0, Height - top - UpstreamPageInset));

            foreach (var page in TabPages)
                if (page.Bounds != bounds)
                    page.Bounds = bounds;
        }

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            base.OnPaintBackground (e);

            if (!UsesUpstreamTabs)
                return;

            using var device = e.DeviceSpace ();

            // The frame around the page: its top edge runs along the bottom of the tabs, two pixels up
            // from where the page starts, and a light band shows between it and the page.
            var scale = (float) e.Scaling;
            var top = (TabStrip.Height - UpstreamTabOrigin) * scale;
            var frame = new SKRect (0.5f * scale, top + 0.5f * scale, ScaledWidth - 0.5f * scale, ScaledHeight - 0.5f * scale);

            using var paint = new SKPaint { Color = UpstreamSelectedTabFill };
            e.Canvas.DrawRect (frame, paint);

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = scale;
            paint.Color = UpstreamTabBorder;
            e.Canvas.DrawRect (frame, paint);
        }
    }
}
