using System.Drawing;
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
                // GDI label text carries a small horizontal bearing inset (~2px at classic UI font
                // sizes): the first glyph never starts flush at the control edge. Designer layouts
                // rely on it -- a label placed at a slightly negative X still shows its first glyph
                // fully -- so translate the aligned bounds the same way (toward the text's anchored
                // edge; centered text is unaffected).
                var text_bounds = layout.TextBounds;
                var inset = e.LogicalToDeviceUnits (2);
                switch (control.TextAlign) {
                    case ContentAlignment.TopLeft:
                    case ContentAlignment.MiddleLeft:
                    case ContentAlignment.BottomLeft:
                        text_bounds.X += inset;
                        break;
                    case ContentAlignment.TopRight:
                    case ContentAlignment.MiddleRight:
                    case ContentAlignment.BottomRight:
                        text_bounds.X -= inset;
                        break;
                }

                if (control.UseMnemonic)
                    e.Canvas.DrawMnemonicText (control.Text, text_bounds, control, control.TextAlign, maxLines: control.Multiline ? null : 1, ellipsis: control.AutoEllipsis);
                else
                    e.Canvas.DrawText (control.Text, text_bounds, control, control.TextAlign, maxLines: control.Multiline ? null : 1, ellipsis: control.AutoEllipsis);
            }
        }
    }
}
