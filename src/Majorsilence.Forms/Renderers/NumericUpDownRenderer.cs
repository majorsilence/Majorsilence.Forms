using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a NumericUpDown.
    /// </summary>
    public class NumericUpDownRenderer : Renderer<NumericUpDown>
    {
        /// <inheritdoc/>
        protected override void Render (NumericUpDown control, PaintEventArgs e)
        {
            var client = control.ClientRectangle;
            var inc_area = control.GetIncrementArea ();
            var dec_area = control.GetDecrementArea ();

            // SMP-33: the control's OWN font, size and colour -- these were Theme.UIFont,
            // Theme.FontSize and Theme.ForegroundColor, so setting Font on a NumericUpDown had no
            // effect at all and the control did not scale with the rest of the form.
            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());
            var foreground = control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;

            // The text sits on whichever side the buttons are not (SMP-33): UpDownAlign was stored and
            // the button strip was always at the right-hand edge.
            var buttons_left = control.UpDownAlign == LeftRightAlignment.Left;
            var text_area = buttons_left
                ? new Rectangle (inc_area.Right + 3, client.Y, client.Width - inc_area.Width - 3, client.Height)
                : new Rectangle (client.X + 3, client.Y, client.Width - inc_area.Width - 3, client.Height);

            // And in whichever alignment TextAlign asks for -- right-aligned numeric columns were all
            // drawn left-aligned.
            var alignment = control.TextAlign switch {
                HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft,
            };

            // DisplayText, not Value.ToString: it is what the user is typing while they are typing, and
            // the Hexadecimal/ThousandsSeparator-aware formatting of Value otherwise (SMP-32, SMP-33).
            e.Canvas.DrawText (control.DisplayText, font, font_size, text_area, foreground, alignment, maxLines: 1);

            DrawCaret (control, e, text_area, font, font_size, foreground);

            // Button separator, on the buttons' own inner edge.
            var separator_x = buttons_left ? inc_area.Right : inc_area.Left;
            e.Canvas.DrawLine (separator_x, client.Y, separator_x, client.Bottom, Theme.BorderLowColor);

            // Up button
            var inc_color = control.IncrementAreaHot ? Theme.ControlHighlightMidColor : Theme.ControlMidColor;
            e.Canvas.FillRectangle (inc_area, inc_color);
            var inc_center = inc_area.GetCenter ();
            ControlPaint.DrawArrowGlyph (e, new Rectangle (inc_center.X - 4, inc_center.Y - 3, 8, 6), foreground, ArrowDirection.Up);

            // Down button
            var dec_color = control.DecrementAreaHot ? Theme.ControlHighlightMidColor : Theme.ControlMidColor;
            e.Canvas.FillRectangle (dec_area, dec_color);
            var dec_center = dec_area.GetCenter ();
            ControlPaint.DrawArrowGlyph (e, new Rectangle (dec_center.X - 4, dec_center.Y - 2, 8, 6), foreground, ArrowDirection.Down);

            // Divider between up/down
            e.Canvas.DrawLine (inc_area.Left, inc_area.Bottom, inc_area.Right, inc_area.Bottom, Theme.BorderLowColor);
        }

        // A one-pixel caret at the typing position, so a control the user can now type into shows where
        // the next character will land. Only while focused and only while there is typed text -- an
        // unfocused spin box showing a caret would read as focused.
        private static void DrawCaret (NumericUpDown control, PaintEventArgs e, Rectangle textArea,
            SkiaSharp.SKTypeface font, int fontSize, SkiaSharp.SKColor color)
        {
            var position = control.CaretPosition;

            if (position < 0)
                return;

            var before = control.DisplayText.Substring (0, System.Math.Min (position, control.DisplayText.Length));
            var offset = (int)TextMeasurer.MeasureText (before, font, fontSize).Width;
            var x = textArea.Left + offset;
            var inset = e.LogicalToDeviceUnits (3);

            e.Canvas.DrawLine (x, textArea.Top + inset, x, textArea.Bottom - inset, color);
        }
    }
}
