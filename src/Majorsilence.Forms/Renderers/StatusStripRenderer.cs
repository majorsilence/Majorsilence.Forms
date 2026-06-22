using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a StatusStrip.
    /// </summary>
    public class StatusStripRenderer : Renderer<StatusStrip>
    {
        /// <inheritdoc/>
        protected override void Render (StatusStrip control, PaintEventArgs e)
        {
            var x = control.PaddedClientRectangle.X;
            var y = control.PaddedClientRectangle.Y;
            var height = control.PaddedClientRectangle.Height;
            var font_size = control.LogicalToDeviceUnits (Theme.FontSize - 1);

            foreach (var item in control.Items) {
                if (!item.Visible)
                    continue;

                var item_width = item.Size.Width > 0 ? item.Size.Width : 120;

                if (item is ToolStripProgressBar pb) {
                    var range = pb.Maximum - pb.Minimum;

                    if (range > 0) {
                        var fill_width = (int)((float)(pb.Value - pb.Minimum) / range * item_width);
                        e.Canvas.FillRectangle (x, y, fill_width, height, Theme.AccentColor2);
                    }

                    e.Canvas.DrawRectangle (x, y, item_width, height, Theme.BorderLowColor);
                } else if (!string.IsNullOrEmpty (item.Text)) {
                    var bounds = new Rectangle (x + 4, y, item_width, height);
                    e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, bounds, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
                }

                x += item_width + 4;

                if (x >= control.ClientRectangle.Right)
                    break;
            }
        }
    }
}
