using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the tool strips WinForms' default
    // ToolStripProfessionalRenderer draws under Windows 11, from its colour table there (ProfessionalColorTable, as
    // measured): a toolbar shaded down from near-white to Control, its 1px edging and rounded corner pixels, and the
    // ToolStripPanel round it shaded left to right across its parent. The theme's flat Control stood for all of these.
    // Drawn in device pixels, as upstream's GDI+ brushes and one-pixel pens are; the gradients' 12px widths scale with
    // the DPI, as upstream's ICON_WELL_GRADIENT_WIDTH does.
    internal static class ToolStripUpstreamLook
    {
        internal static readonly SKColor GradientBegin = new SKColor (0xFC, 0xFC, 0xFC);         // ToolStripGradientBegin
        internal static readonly SKColor GradientMiddle = new SKColor (0xF8, 0xF8, 0xF8);        // ToolStripGradientMiddle
        internal static readonly SKColor GradientEnd = new SKColor (0xF0, 0xF0, 0xF0);           // ToolStripGradientEnd
        internal static readonly SKColor BorderColour = new SKColor (0xF2, 0xF2, 0xF2);                // ToolStripBorder
        internal static readonly SKColor OverflowBegin = new SKColor (0xF5, 0xF5, 0xF5);         // OverflowButtonGradientBegin
        internal static readonly SKColor OverflowMiddle = new SKColor (0xF2, 0xF2, 0xF2);        // OverflowButtonGradientMiddle
        internal static readonly SKColor OverflowEnd = new SKColor (0xA0, 0xA0, 0xA0);           // OverflowButtonGradientEnd
        internal static readonly SKColor PanelBegin = new SKColor (0xF0, 0xF0, 0xF0);            // ToolStripPanelGradientBegin
        internal static readonly SKColor PanelEnd = new SKColor (0xFC, 0xFC, 0xFC);              // ToolStripPanelGradientEnd

        private const int IconWellGradientWidth = 12;

        // Upstream's ToolStripRenderer.ShouldPaintBackground: no BackColor or BackgroundImage of the control's own.
        internal static bool ShouldPaintBackground (Control control)
            => control.Style.BackgroundColor is null && control.BackgroundImage is null;

        // OnRenderToolStripBackground: the toolbar's FillWithDoubleGradient, down a horizontal strip, across a vertical one.
        internal static void Background (SKCanvas canvas, ToolStrip strip, Rectangle bounds)
        {
            var width = strip.LogicalToDeviceUnits (IconWellGradientWidth);
            FillWithDoubleGradient (canvas, GradientBegin, GradientMiddle, GradientEnd, bounds, width, width, vertical: strip.Orientation == Orientation.Horizontal);
        }

        // OnRenderToolStripBorder for a plain toolbar: a shadow line along the bottom (the right of a vertical strip), and
        // with rounded edges a corner pixel, the 1px edging down the far side and the corner pixels RenderToolStripCurve sets.
        internal static void Border (SKCanvas canvas, ToolStrip strip, Rectangle bounds, bool roundedEdges)
        {
            using var paint = new SKPaint { Color = BorderColour };
            int w = bounds.Width, h = bounds.Height;
            var horizontal = strip.Orientation == Orientation.Horizontal;

            canvas.Save ();
            canvas.Translate (bounds.Left, bounds.Top);

            if (horizontal)
                canvas.DrawRect (SKRect.Create (0, h - 1, w, 1), paint);
            else
                canvas.DrawRect (SKRect.Create (w - 1, 0, 1, h), paint);

            if (roundedEdges) {
                // One pixel of corner rounding at the bottom right: GDI+'s line from (w-2, h-2) to (w-1, h-3).
                canvas.DrawRect (SKRect.Create (w - 2, h - 2, 1, 1), paint);
                canvas.DrawRect (SKRect.Create (w - 1, h - 3, 1, 1), paint);

                // The overflow button paints its own effects over the border; without one, the strip is edged.
                if (!strip.OverflowButton.Visible) {
                    var edging = horizontal ? new Rectangle (w - 1, 3, 1, h - 3) : new Rectangle (3, h - 1, w - 3, h - 1);
                    var width = strip.LogicalToDeviceUnits (IconWellGradientWidth);
                    FillWithDoubleGradient (canvas, OverflowBegin, OverflowMiddle, OverflowEnd, edging, width, width, vertical: true);
                    Curve (canvas, strip, w, h);
                }

                ClearTransparentRegion (canvas, strip, w, h);
            }

            canvas.Restore ();
        }

        // GetTransparentRegion: the corner pixels upstream leaves to the parent, so a toolbar with rounded edges shows the
        // panel through them. Cleared here, the strip's surface lets the panel under it show when it is composited.
        private static void ClearTransparentRegion (SKCanvas canvas, ToolStrip strip, int w, int h)
        {
            if (strip.Parent is null)
                return;

            var corners = new System.Collections.Generic.List<SKRect> {
                SKRect.Create (0, 0, 1, 1),
                SKRect.Create (0, h - 1, 2, 1), SKRect.Create (0, h - 2, 1, 2),
                SKRect.Create (w - 2, h - 1, 2, 1), SKRect.Create (w - 1, h - 2, 1, 2),
            };

            if (strip.OverflowButton.Visible) {
                corners.Add (SKRect.Create (w - 2, 0, 1, 1));
                corners.Add (SKRect.Create (w - 1, 0, 1, 2));
            } else {
                corners.Add (SKRect.Create (w - 3, 0, 2, 1));
                corners.Add (SKRect.Create (w - 1, 0, 1, 3));
            }

            using var clear = new SKPaint { BlendMode = SKBlendMode.Clear };

            foreach (var corner in corners)
                canvas.DrawRect (corner, clear);
        }

        // RenderToolStripCurve: two shadow pixels at each top corner in the middle colour, and two at the bottom left in
        // the end colour, each only where it would not cover the strip's display rectangle (its items).
        private static void Curve (SKCanvas canvas, ToolStrip strip, int w, int h)
        {
            // Upstream's ToolStrip.DisplayRectangle leaves out the grip, so the corner pixels beside it are painted.
            var logical = strip.DisplayRectangle;

            if (strip.GripStyle == ToolStripGripStyle.Visible && strip.Orientation == Orientation.Horizontal) {
                var grip = strip.GripRectangle.Right + strip.GripMargin.Right;
                logical = Rectangle.FromLTRB (System.Math.Max (logical.Left, grip), logical.Top, logical.Right, logical.Bottom);
            }

            var display = strip.LogicalToDeviceUnits (logical);

            using var paint = new SKPaint { Color = GradientMiddle };

            foreach (var (x, y) in new[] { (1, 0), (0, 1), (w - 3, 0), (w - 2, 1) })
                if (!display.IntersectsWith (new Rectangle (x, y, 1, 1)))
                    canvas.DrawRect (SKRect.Create (x, y, 1, 1), paint);

            paint.Color = GradientEnd;

            if (!display.Contains (new Point (1, h - 2)))
                canvas.DrawRect (SKRect.Create (1, h - 2, 1, 1), paint);

            if (!display.IntersectsWith (new Rectangle (0, h - 3, 1, 1)))
                canvas.DrawRect (SKRect.Create (0, h - 3, 1, 1), paint);
        }

        // OnRenderGrip on a horizontal strip: from GRIP_PADDING (4px, scaled) down, one square every 4 pixels -- a GripLight 2x2
        // square at the grip's middle, then a ControlText one a pixel up and to the left over it.
        internal static void Grip (SKCanvas canvas, ToolStrip strip, Rectangle grip)
        {
            var padding = strip.LogicalToDeviceUnits (4);
            var count = (grip.Height - padding * 2) / 4;

            if (count <= 0)
                return;

            var x = grip.Left + grip.Width / 2;
            using var light = new SKPaint { Color = SKColors.White };                              // GripLight
            using var dark = new SKPaint { Color = SystemColors.ControlText.ToSKColor () };

            for (var i = 0; i < count; i++) {
                var y = grip.Top + padding + 1 + 4 * i;
                canvas.DrawRect (SKRect.Create (x, y, 2, 2), light);
                canvas.DrawRect (SKRect.Create (x - 1, y - 1, 2, 2), dark);
            }
        }

        // OnRenderToolStripPanelBackground: RenderBackgroundGradient, the panel showing its slice of one gradient run left
        // to right across its parent (a panel with none runs it across itself).
        internal static void PanelBackground (SKCanvas canvas, ToolStripPanel panel)
        {
            float left, width;

            if (panel.Parent is { } parent) {
                left = -panel.ScaledLeft;
                width = parent.ScaledWidth;
            } else {
                left = 0;
                width = panel.ScaledWidth;
            }

            var (begin, end) = panel.RightToLeft == RightToLeft.Yes ? (PanelEnd, PanelBegin) : (PanelBegin, PanelEnd);

            if (width <= 0 || panel.ScaledWidth <= 0 || panel.ScaledHeight <= 0)
                return;

            using var shader = SKShader.CreateLinearGradient (new SKPoint (left, 0), new SKPoint (left + width, 0), new[] { begin, end }, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect (SKRect.Create (0, 0, panel.ScaledWidth, panel.ScaledHeight), paint);
        }

        // Upstream's FillWithDoubleGradient: the middle colour throughout, the begin colour shaded into it over the first
        // `first` pixels, and the middle shaded into the end colour over the last `second`. A rectangle too short for both
        // gradients gets one, begin to end, across all of it.
        private static void FillWithDoubleGradient (SKCanvas canvas, SKColor begin, SKColor middle, SKColor end, Rectangle bounds, int first, int second, bool vertical)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var length = vertical ? bounds.Height : bounds.Width;

            if (length <= first + second) {
                Gradient (canvas, bounds, begin, end, vertical);
                return;
            }

            using (var paint = new SKPaint { Color = middle })
                canvas.DrawRect (SKRect.Create (bounds.Left, bounds.Top, bounds.Width, bounds.Height), paint);

            // The first gradient over the first `first` pixels.
            var head = vertical ? new Rectangle (bounds.Left, bounds.Top, bounds.Width, first) : new Rectangle (bounds.Left, bounds.Top, first, bounds.Height);
            Gradient (canvas, head, begin, middle, vertical);

            // The second gradient is laid out over `second + 1` pixels at the end and painted over all but its first.
            var tail = vertical
                ? new Rectangle (bounds.Left, bounds.Bottom - (second + 1), bounds.Width, second + 1)
                : new Rectangle (bounds.Right - (second + 1), bounds.Top, second + 1, bounds.Height);
            var painted = vertical
                ? new Rectangle (tail.Left, tail.Top + 1, tail.Width, tail.Height - 1)
                : new Rectangle (tail.Left + 1, tail.Top, tail.Width - 1, tail.Height);
            Gradient (canvas, tail, middle, end, vertical, painted);
        }

        // A linear gradient laid out over `layout`, painted over `paintRect` (all of it by default).
        private static void Gradient (SKCanvas canvas, Rectangle layout, SKColor from, SKColor to, bool vertical, Rectangle? paintRect = null)
        {
            var end = vertical ? new SKPoint (layout.Left, layout.Bottom) : new SKPoint (layout.Right, layout.Top);
            using var shader = SKShader.CreateLinearGradient (new SKPoint (layout.Left, layout.Top), end, new[] { from, to }, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader };
            var r = paintRect ?? layout;
            canvas.DrawRect (SKRect.Create (r.Left, r.Top, r.Width, r.Height), paint);
        }
    }
}
