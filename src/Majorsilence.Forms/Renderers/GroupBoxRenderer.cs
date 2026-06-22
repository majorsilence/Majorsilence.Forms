using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a GroupBox.
    /// </summary>
    public class GroupBoxRenderer : Renderer<GroupBox>
    {
        /// <inheritdoc/>
        protected override void Render (GroupBox control, PaintEventArgs e)
        {
            var bounds = control.ClientRectangle;
            var font_size = control.LogicalToDeviceUnits (Theme.FontSize);
            var title_height = font_size + 4;
            var border_top = title_height / 2;

            // Border rect starts below the midpoint of the title text
            var border = new Rectangle (bounds.X + 1, bounds.Y + border_top, bounds.Width - 2, bounds.Height - border_top - 1);
            e.Canvas.DrawRectangle (border, Theme.BorderLowColor);

            if (!string.IsNullOrEmpty (control.Text)) {
                var text_size = TextMeasurer.MeasureText (control.Text, Theme.UIFont, font_size);
                var text_width = (int)text_size.Width + 6;
                var text_x = bounds.X + 10;

                // Clear border behind title
                e.Canvas.FillRectangle (text_x - 2, bounds.Y, text_width + 4, title_height, control.Style.BackgroundColor ?? Theme.BackgroundColor);

                var text_bounds = new Rectangle (text_x, bounds.Y, text_width, title_height);
                e.Canvas.DrawText (control.Text, Theme.UIFont, font_size, text_bounds, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
            }
        }
    }
}
