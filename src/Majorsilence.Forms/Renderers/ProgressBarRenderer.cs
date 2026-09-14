using System;
using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a ProgressBar.
    /// </summary>
    public class ProgressBarRenderer : Renderer<ProgressBar>
    {
        // Classic comctl32 block metrics: the chunk is a little taller than it is wide, with a one-pixel
        // gap. Kept in logical units and scaled, so the segmentation survives a high-DPI display rather
        // than collapsing into a solid bar.
        private const int BlockWidth = 6;
        private const int BlockGap = 2;

        // The marquee block covers about a third of the track, which is what Windows draws.
        private const float MarqueeBlockFraction = 0.3f;

        /// <inheritdoc/>
        protected override void Render (ProgressBar control, PaintEventArgs e)
        {
            var client_area = control.PaddedClientRectangle;
            var fill = control.Enabled ? Theme.AccentColor2 : Theme.ForegroundDisabledColor;

            // SMP-26: Marquee is indeterminate -- it says "working", not "this far through", so it is
            // driven by the animation phase and not by Value. Drawn from Value like everything else, a
            // marquee bar sat permanently empty and the app looked hung.
            if (control.Style == ProgressBarStyle.Marquee) {
                RenderMarquee (control, e, client_area, fill);
                return;
            }

            // Prevent divide by zero
            if (control.Maximum == control.Minimum)
                return;

            var percent = (float)(control.Value - control.Minimum) / (control.Maximum - control.Minimum);
            var filled_pixels = (int)(percent * client_area.Width);

            if (filled_pixels <= 0)
                return;

            if (control.Style == ProgressBarStyle.Continuous) {
                e.Canvas.FillRectangle (client_area.X, client_area.Y, filled_pixels, client_area.Height, fill);
                return;
            }

            RenderBlocks (control, e, client_area, filled_pixels, fill);
        }

        // Blocks -- the WinForms default -- is a row of discrete chunks, not a solid fill. Drawing it
        // solid is why a Blocks bar and a Continuous one were indistinguishable.
        private static void RenderBlocks (ProgressBar control, PaintEventArgs e, Rectangle clientArea, int filledPixels, SkiaSharp.SKColor fill)
        {
            var block = control.LogicalToDeviceUnits (BlockWidth);
            var gap = control.LogicalToDeviceUnits (BlockGap);
            var stride = block + gap;

            if (stride <= 0)
                return;

            for (var x = clientArea.X; x < clientArea.X + filledPixels; x += stride) {
                // The last block is clipped to the filled extent rather than overhanging it, so the bar
                // still reads as exactly the right length.
                var width = Math.Min (block, clientArea.X + filledPixels - x);

                if (width > 0)
                    e.Canvas.FillRectangle (x, clientArea.Y, width, clientArea.Height, fill);
            }
        }

        private static void RenderMarquee (ProgressBar control, PaintEventArgs e, Rectangle clientArea, SkiaSharp.SKColor fill)
        {
            var block_width = Math.Max (1, (int)(clientArea.Width * MarqueeBlockFraction));

            // The block's left edge travels the width of the track and wraps, so at every phase --
            // phase zero included -- some of it is over the bar. Starting it fully off the left edge
            // instead would leave the bar looking empty exactly when it is meant to say "working".
            var left = clientArea.X + (int)(control.MarqueePosition * clientArea.Width);

            // Clipped at the right edge: the block narrows as it leaves rather than overhanging.
            var visible_left = left;
            var visible_right = Math.Min (left + block_width, clientArea.Right);

            if (visible_right > visible_left)
                e.Canvas.FillRectangle (visible_left, clientArea.Y, visible_right - visible_left, clientArea.Height, fill);
        }
    }
}
