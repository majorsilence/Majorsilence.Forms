using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a Label.
    /// </summary>
    public class LabelRenderer : Renderer<Label>
    {
        /// <inheritdoc/>
        protected override void Render (Label control, PaintEventArgs e)
        {
            var layout = TextImageLayoutEngine.Layout (control);

            // Draw the image
            if ((control as IHaveTextAndImageAlign).GetImage () is SKBitmap image)
                e.Canvas.DrawBitmap (image, layout.ImageBounds, !control.Enabled);

            // Draw the text. A Label interprets the '&' mnemonic prefix only when UseMnemonic is set
            // (the default); with it off the text -- including any ampersand -- is drawn literally.
            if (control.Text.HasValue ()) {
                if (control.UseMnemonic)
                    e.Canvas.DrawMnemonicText (control.Text, layout.TextBounds, control, control.TextAlign, maxLines: control.Multiline ? null : 1, ellipsis: control.AutoEllipsis);
                else
                    e.Canvas.DrawText (control.Text, layout.TextBounds, control, control.TextAlign, maxLines: control.Multiline ? null : 1, ellipsis: control.AutoEllipsis);
            }
        }
    }
}
