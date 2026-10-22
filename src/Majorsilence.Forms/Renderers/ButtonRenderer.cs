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
            // At its own size, clipped to the field: a 23x21 glyph on a 25x25 button shows its middle 17x17, as upstream.
            if ((control as IHaveTextAndImageAlign).GetImage () is SKBitmap image) {
                var clip = layout.ImageClip;
                e.Canvas.Save ();
                e.Canvas.ClipRect (new SKRect (clip.Left, clip.Top, clip.Right, clip.Bottom));
                e.Canvas.DrawBitmap (image, layout.ImageBounds, !control.Enabled);
                e.Canvas.Restore ();
            }

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
            var alignment = TextImageLayoutEngine.RtlTranslate (control, control.TextAlign);
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());

            if (control is { PaintsUpstreamLook: true, FlatStyle: FlatStyle.System })
                bounds = NativeCaptionBox (control.Text, font, font_size, bounds, alignment);
            else
                (bounds, alignment) = SkiaTextExtensions.WholeLineBox (control.Text, font, font_size, bounds, alignment);

            // Upstream's themed button keeps its text colour on hover; the theme's hover style turns it
            // white for the accent-filled face, which the upstream face (Button.PaintsUpstreamLook) is not.
            if (control.PaintsUpstreamLook) {
                var colour = !control.Enabled ? control.DisabledForeColor
                    : control.Style.ForegroundColor ?? control.Parent?.GetEffectiveForegroundColor () ?? Theme.ForegroundColor;

                e.Canvas.DrawMnemonicText (control.Text, font, font_size, bounds, colour, alignment, null, control.AutoEllipsis, control.ShowKeyboardCues);
                return;
            }

            e.Canvas.DrawMnemonicText (control.Text, bounds, control, alignment, maxLines: null, ellipsis: control.AutoEllipsis);
        }

        // A FlatStyle.System button is the native control, which draws every line a caption wraps to and places the
        // block as its alignment says, the button clipping what overflows -- a 75 x 23 "Show Emails" shows the bottom
        // of "Show" over the top of "Emails". It dropped the line that did not fit whole, as the themed (Standard)
        // adapter's TextBoxControl flag does, and showed "Show" alone.
        private static System.Drawing.Rectangle NativeCaptionBox (string text, SKTypeface font, int fontSize, System.Drawing.Rectangle bounds, ContentAlignment alignment)
        {
            var height = (int) System.Math.Ceiling (TextMeasurer.MeasureText (Mnemonics.Strip (text), font, fontSize, new System.Drawing.Size (bounds.Width, int.MaxValue)).Height);

            if (height <= bounds.Height)
                return bounds;

            var top = TextMeasurer.GetVerticalAlign (alignment) switch {
                SKTextAlign.Left => bounds.Top,
                SKTextAlign.Right => bounds.Bottom - height,
                _ => bounds.Top + (bounds.Height - height) / 2,
            };

            return new System.Drawing.Rectangle (bounds.X, top, bounds.Width, height);
        }

    }
}
