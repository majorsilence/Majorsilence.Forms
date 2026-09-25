using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Renders a <see cref="PrintPreviewControl"/>: the pages its document produced, laid out in the
    /// control's <see cref="PrintPreviewControl.Rows"/> by <see cref="PrintPreviewControl.Columns"/>
    /// grid from <see cref="PrintPreviewControl.StartPage"/>, each on a white sheet with a thin border
    /// (W6 mechanisms — the control used to paint nothing at all).
    /// </summary>
    public class PrintPreviewRenderer : Renderer<PrintPreviewControl>
    {
        /// <inheritdoc/>
        protected override void Render (PrintPreviewControl control, PaintEventArgs e)
        {
            var pages = control.Pages;

            if (pages.Count == 0)
                return;

            e.Canvas.Save ();
            e.Canvas.Clip (control.ClientRectangle);

            // UseAntiAlias picks the sampling the page bitmap is scaled with: smooth, or the nearest
            // pixel, which is what a preview at a small zoom looks like without it.
            using var paint = new SKPaint { IsAntialias = control.UseAntiAlias };

            for (var i = 0; i < control.Rows * control.Columns; i++) {
                var bounds = control.PageBoundsAt (i);

                if (bounds.IsEmpty)
                    break;

                e.Canvas.FillRectangle (bounds, SKColors.White);

                if (pages[control.StartPage + i].Image.GetSKBitmap () is { } bitmap)
                    e.Canvas.DrawBitmap (bitmap, SKRect.Create (bounds.Left, bounds.Top, bounds.Width, bounds.Height), paint);

                e.Canvas.DrawRectangle (bounds, Theme.BorderMidColor);
            }

            e.Canvas.Restore ();
        }
    }
}
