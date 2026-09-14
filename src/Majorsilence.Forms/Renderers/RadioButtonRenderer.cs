using Majorsilence.Forms.Layout;
using SkiaSharp;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a RadioButton.
    /// </summary>
    public class RadioButtonRenderer : Renderer<RadioButton>, IRenderGlyph, IRenderTextAndImage
    {
        // GDI parity: the classic radio glyph is 13px with a ~5px gap before the text. Designer
        // AutoSize widths are frozen from those metrics, so a larger glyph box eats into the text
        // area and clips the last characters of designer-sized radio buttons.
        /// <inheritdoc/>
        public int GlyphSize { get; } = 13;

        /// <inheritdoc/>
        public int GlyphTextPadding { get; } = 5;

        /// <inheritdoc/>
        public int ImageTextMargin { get; } = 4;

        /// <inheritdoc/>
        protected override void Render (RadioButton control, PaintEventArgs e)
        {
            var layout = TextImageLayoutEngine.Layout (control);

            // SMP-03: Appearance.Button draws the control as a toggle button -- no glyph, and the
            // latched state carried by the background instead (see ButtonBase.ApplyFlatAppearance).
            // A row of these is the segmented control the idiom exists for.
            if (control.Appearance != Appearance.Button)
            ControlPaint.DrawRadioButton (e, layout.GlyphBounds.GetCenter (), control.Checked ? CheckState.Checked : CheckState.Unchecked, !control.Enabled);

            // Draw the image
            if ((control as IHaveTextAndImageAlign).GetImage () is SKBitmap image)
                e.Canvas.DrawBitmap (image, layout.ImageBounds, !control.Enabled);

            // Draw the focus rectangle
            if (control.Selected && control.ShowFocusCues)
                e.Canvas.DrawFocusRectangle (layout.Focus, 0);

            // Draw the text (a RadioButton always interprets the '&' mnemonic prefix).
            if (control.Text.HasValue ())
                // SMP-13: see ButtonRenderer -- upstream wraps the whole button family's captions,
                // and multi-line checkbox/radio labels are common on consent and option forms.
                e.Canvas.DrawMnemonicText (control.Text, layout.TextBounds, control, control.TextAlign, maxLines: null, ellipsis: control.AutoEllipsis);
        }
    }
}
