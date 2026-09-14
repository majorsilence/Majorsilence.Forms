using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Renders a <see cref="DomainUpDown"/>: its selected item's text, and the spin buttons.
    /// </summary>
    /// <remarks>
    /// SMP-37. Without a renderer of its own, <c>RenderManager</c> walked up the base chain and painted
    /// a DomainUpDown with <see cref="NumericUpDownRenderer"/> -- which draws a <c>Value</c> this control
    /// does not have, so it showed the literal text <c>0</c> whatever its items held.
    /// </remarks>
    public class DomainUpDownRenderer : Renderer<DomainUpDown>
    {
        /// <inheritdoc/>
        protected override void Render (DomainUpDown control, PaintEventArgs e)
        {
            var client = control.ClientRectangle;
            var button_width = control.LogicalToDeviceUnits (18);
            var buttons_left = control.UpDownAlign == LeftRightAlignment.Left;
            var strip_left = buttons_left ? client.X : client.Right - button_width;
            var inc_area = new Rectangle (strip_left, client.Y, button_width, client.Height / 2);
            var dec_area = new Rectangle (strip_left, client.Y + client.Height / 2, button_width, client.Height - client.Height / 2);

            var font = control.GetEffectiveFont ();
            var font_size = control.LogicalToDeviceUnits (control.GetEffectiveFontSize ());
            var foreground = control.Enabled ? control.GetEffectiveForegroundColor () : Theme.ForegroundDisabledColor;

            var text_area = buttons_left
                ? new Rectangle (inc_area.Right + 3, client.Y, client.Width - button_width - 3, client.Height)
                : new Rectangle (client.X + 3, client.Y, client.Width - button_width - 3, client.Height);

            var alignment = control.TextAlign switch {
                HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft,
            };

            e.Canvas.DrawText (control.DisplayText, font, font_size, text_area, foreground, alignment, maxLines: 1);

            var separator_x = buttons_left ? inc_area.Right : inc_area.Left;
            e.Canvas.DrawLine (separator_x, client.Y, separator_x, client.Bottom, Theme.BorderLowColor);

            e.Canvas.FillRectangle (inc_area, Theme.ControlMidColor);
            var inc_center = inc_area.GetCenter ();
            ControlPaint.DrawArrowGlyph (e, new Rectangle (inc_center.X - 4, inc_center.Y - 3, 8, 6), foreground, ArrowDirection.Up);

            e.Canvas.FillRectangle (dec_area, Theme.ControlMidColor);
            var dec_center = dec_area.GetCenter ();
            ControlPaint.DrawArrowGlyph (e, new Rectangle (dec_center.X - 4, dec_center.Y - 2, 8, 6), foreground, ArrowDirection.Down);

            e.Canvas.DrawLine (inc_area.Left, inc_area.Bottom, inc_area.Right, inc_area.Bottom, Theme.BorderLowColor);
        }
    }
}
