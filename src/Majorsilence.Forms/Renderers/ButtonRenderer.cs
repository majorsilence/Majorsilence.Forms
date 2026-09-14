using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a Button.
    /// </summary>
    public class ButtonRenderer : Renderer<Button>, IRenderTextAndImage
    {
        /// <inheritdoc/>
        public int ImageTextMargin { get; } = 4;

        /// <inheritdoc/>
        protected override void Render (Button control, PaintEventArgs e)
        {
            var layout = TextImageLayoutEngine.Layout (control);

            // Draw the image
            if ((control as IHaveTextAndImageAlign).GetImage () is SKBitmap image)
                e.Canvas.DrawBitmap (image, layout.ImageBounds, !control.Enabled);

            // Draw the focus rectangle
            if (control.Selected && control.ShowFocusCues)
                e.Canvas.DrawFocusRectangle (layout.Focus, 0);

            // Draw the text (a Button always interprets the '&' mnemonic prefix).
            if (control.Text.HasValue ())
                // SMP-13: upstream ORs TextFormatFlags.WordBreak unconditionally for the button
                // family (ControlPaint.CreateTextFormatFlags, Rendering/ControlPaint.cs:2640-2652), so
                // a tall button with a two-word caption wraps. Pinned to one line, "Export Selected" on
                // a 60x60 button came out clipped to a single ellipsised line.
                e.Canvas.DrawMnemonicText (control.Text, layout.TextBounds, control, control.TextAlign, maxLines: null, ellipsis: control.AutoEllipsis);
        }
    }
}
