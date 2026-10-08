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
                //
                // Upstream's ButtonBaseAdapter also adds TextFormatFlags.TextBoxControl, which drops a
                // last line that would only be partly visible, and then centres what is left. Without
                // it a caption that wraps on a one-line-high button ("Save Default" on a 26px button)
                // top-aligned both lines and showed the second one sliced in half.
                DrawCaption (control, layout.TextBounds, e);
        }

        private static void DrawCaption (Button control, System.Drawing.Rectangle bounds, PaintEventArgs e)
        {
            var alignment = control.TextAlign;
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());
            var wrapped = TextMeasurer.MeasureText (control.Text, font, font_size, new System.Drawing.Size (bounds.Width, int.MaxValue)).Height;
            var line = TextMeasurer.MeasureText ("X", font, font_size).Height;
            var whole_lines = line > 0 ? (int)(bounds.Height / line) : 0;

            // Only when the wrapped caption overflows and at least one whole line fits: narrow the box to
            // those whole lines, centred where the alignment asks, and let the clip hide the rest. A
            // button shorter than a single line keeps the ordinary (top-clamped) path.
            if (wrapped > bounds.Height && whole_lines >= 1) {
                var visible = (int)(whole_lines * line);
                var top = TextMeasurer.GetVerticalAlign (alignment) switch {
                    SkiaSharp.SKTextAlign.Left => bounds.Top,
                    SkiaSharp.SKTextAlign.Right => bounds.Bottom - visible,
                    _ => bounds.Top + ((bounds.Height - visible) / 2),
                };

                bounds = new System.Drawing.Rectangle (bounds.X, top, bounds.Width, visible);
                alignment = ToTopRow (alignment);
            }

            // Upstream's themed button keeps its text colour on hover; the theme's hover style turns it
            // white for the accent-filled face, which the upstream face (Button.PaintsUpstreamLook) is not.
            if (control.PaintsUpstreamLook) {
                var colour = !control.Enabled ? Theme.ForegroundDisabledColor
                    : control.Style.ForegroundColor ?? control.Parent?.GetEffectiveForegroundColor () ?? Theme.ForegroundColor;

                e.Canvas.DrawMnemonicText (control.Text, font, font_size, bounds, colour, alignment, null, control.AutoEllipsis, control.ShowKeyboardCues);
                return;
            }

            e.Canvas.DrawMnemonicText (control.Text, bounds, control, alignment, maxLines: null, ellipsis: control.AutoEllipsis);
        }

        private static ContentAlignment ToTopRow (ContentAlignment alignment) => alignment switch {
            ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => ContentAlignment.TopLeft,
            ContentAlignment.MiddleRight or ContentAlignment.BottomRight => ContentAlignment.TopRight,
            ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => ContentAlignment.TopCenter,
            _ => alignment,
        };
    }
}
