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
            var font_size = control.LogicalToDeviceUnits (Theme.FontSize - 1);

            // Positions come from StatusStrip.LayoutItems (run by MenuBase.OnPaint just before this), so
            // the painted item and the region MenuBase hit-tests for clicks are the same rectangle.
            foreach (var item in control.Items) {
                if (!item.Visible)
                    continue;

                var item_bounds = item.Bounds;

                // An item hosting a real control draws nothing of its own -- the control is a child of the
                // strip and paints itself over this rectangle. Drawing the item's Text as well shows
                // through any hosted control with a transparent background: a hosted slider came out with
                // the words "kryptonSlider1" printed across its track.
                if (item is ToolStripControlHost) {
                    // Fall through to the right-edge check below, as every other item does.
                } else if (item is ToolStripProgressBar pb) {
                    var range = pb.Maximum - pb.Minimum;

                    if (range > 0) {
                        var fill_width = (int)((float)(pb.Value - pb.Minimum) / range * item_bounds.Width);
                        e.Canvas.FillRectangle (item_bounds.X, item_bounds.Y, fill_width, item_bounds.Height, Theme.AccentColor2);
                    }

                    e.Canvas.DrawRectangle (item_bounds.X, item_bounds.Y, item_bounds.Width, item_bounds.Height, Theme.BorderLowColor);
                } else if (!string.IsNullOrEmpty (item.Text)) {
                    var text_bounds = new Rectangle (item_bounds.X + 4, item_bounds.Y, item_bounds.Width, item_bounds.Height);
                    e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, text_bounds, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
                }

                // Stop once we've run off the right-hand edge of the bar.
                if (item_bounds.Right + StatusStrip.ItemSpacing >= control.ClientRectangle.Right)
                    break;
            }
        }
    }
}
