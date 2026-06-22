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
            var button_width = control.ButtonWidth;
            var font_size = control.LogicalToDeviceUnits (Theme.FontSize);
            var text_area = new Rectangle (client.X + 3, client.Y, client.Width - button_width - 3, client.Height);
            var inc_area = control.GetIncrementArea ();
            var dec_area = control.GetDecrementArea ();

            // Value text
            var format = control.DecimalPlaces > 0
                ? "F" + control.DecimalPlaces
                : "F0";
            e.Canvas.DrawText (control.Value.ToString (format), Theme.UIFont, font_size, text_area, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);

            // Button separator
            e.Canvas.DrawLine (inc_area.Left, client.Y, inc_area.Left, client.Bottom, Theme.BorderLowColor);

            // Up button
            var inc_color = control.IncrementAreaHot ? Theme.ControlHighlightMidColor : Theme.ControlMidColor;
            e.Canvas.FillRectangle (inc_area, inc_color);
            var inc_center = inc_area.GetCenter ();
            ControlPaint.DrawArrowGlyph (e, new Rectangle (inc_center.X - 4, inc_center.Y - 3, 8, 6), Theme.ForegroundColor, ArrowDirection.Up);

            // Down button
            var dec_color = control.DecrementAreaHot ? Theme.ControlHighlightMidColor : Theme.ControlMidColor;
            e.Canvas.FillRectangle (dec_area, dec_color);
            var dec_center = dec_area.GetCenter ();
            ControlPaint.DrawArrowGlyph (e, new Rectangle (dec_center.X - 4, dec_center.Y - 2, 8, 6), Theme.ForegroundColor, ArrowDirection.Down);

            // Divider between up/down
            e.Canvas.DrawLine (inc_area.Left, inc_area.Bottom, inc_area.Right, inc_area.Bottom, Theme.BorderLowColor);
        }
    }
}
