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
            if (control.LaysOutAsUpstream) {
                RenderUpstreamItems (control, e);
                return;
            }

            var font_size = control.LogicalToDeviceUnits (Theme.FontSize - 1);

            // Positions come from StatusStrip.LayoutItems (run by MenuBase.OnPaint just before this), so
            // the painted item and the region MenuBase hit-tests for clicks are the same rectangle.
            foreach (var item in control.Items) {
                if (!item.Visible)
                    continue;

                var item_bounds = item.DeviceBounds;

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
                } else if (!string.IsNullOrEmpty (item.Text) && item is not ToolStripItem { DisplaysText: false }) {
                    // DisplayStyle Image or None hides the caption here as on any strip (TSM-07).
                    var text_bounds = new Rectangle (item_bounds.X + 4, item_bounds.Y, item_bounds.Width, item_bounds.Height);
                    e.Canvas.DrawText (item.Text, Theme.UIFont, font_size, text_bounds, Theme.ForegroundColor, ContentAlignment.MiddleLeft, maxLines: 1);
                }

                // A status label's border: BorderStyle turns it on, BorderSides picks the edges. Every
                // 3D style draws as the one-pixel line this renderer uses everywhere (W6.2 sweep).
                if (item is ToolStripStatusLabel { BorderStyle: not Border3DStyle.None, BorderSides: not ToolStripStatusLabelBorderSides.None } label)
                    RenderLabelBorder (e, item_bounds, label.BorderSides);

                // Stop once we've run off the right-hand edge of the bar.
                if (item_bounds.Right + StatusStrip.ItemSpacing >= control.DeviceClientRectangle.Right)
                    break;
            }
        }

        // A ported app's status strip (StatusStripUpstreamLayout.cs): each item in the box laid out for it, a label's text in
        // the font it was measured in, placed by its TextAlign inside GDI's padding, and its image before the text.
        // Items upstream does not show (Placement None) are not drawn.
        private static void RenderUpstreamItems (StatusStrip control, PaintEventArgs e)
        {
            foreach (var item in control.Items) {
                if (!item.Visible || item is ToolStripItem { Placement: ToolStripItemPlacement.None })
                    continue;

                var bounds = item.DeviceBounds;

                if (item is ToolStripControlHost) {
                    // The hosted control paints itself.
                } else if (item is ToolStripProgressBar pb) {
                    var range = pb.Maximum - pb.Minimum;

                    if (range > 0) {
                        var fill_width = (int)((float)(pb.Value - pb.Minimum) / range * bounds.Width);
                        e.Canvas.FillRectangle (bounds.X, bounds.Y, fill_width, bounds.Height, Theme.AccentColor2);
                    }

                    e.Canvas.DrawRectangle (bounds.X, bounds.Y, bounds.Width, bounds.Height, Theme.BorderLowColor);
                } else if (item is not ToolStripSeparator) {
                    RenderUpstreamContent (control, e, item, bounds);
                }

                if (item is ToolStripStatusLabel { BorderStyle: not Border3DStyle.None, BorderSides: not ToolStripStatusLabelBorderSides.None } label)
                    RenderLabelBorder (e, bounds, label.BorderSides);
            }
        }

        private static void RenderUpstreamContent (StatusStrip control, PaintEventArgs e, MenuItem item, Rectangle bounds)
        {
            var strip_item = item as ToolStripItem;
            var image = strip_item is { DisplaysImage: false } ? null : item.ImageSK;
            var text = strip_item is { DisplaysText: false } ? null : item.Text;
            var font = StripItemFont.ResolveFont (item, control) ?? SystemFonts.MenuFont;
            var align = Layout.TextImageLayoutEngine.RtlTranslate (control, strip_item?.TextAlign ?? ContentAlignment.MiddleCenter);

            // The content box: inside a bordered label's 2px edge, and the padding the item was given.
            var inset = item is ToolStripStatusLabel { BorderSides: not ToolStripStatusLabelBorderSides.None } ? 2 : 0;
            var padding = item.AssignedPadding ?? Padding.Empty;
            var content = new Rectangle (
                bounds.X + control.LogicalToDeviceUnits (inset + padding.Left),
                bounds.Y + control.LogicalToDeviceUnits (inset + padding.Top),
                System.Math.Max (0, bounds.Width - control.LogicalToDeviceUnits (2 * inset + padding.Horizontal)),
                System.Math.Max (0, bounds.Height - control.LogicalToDeviceUnits (2 * inset + padding.Vertical)));

            var text_width = string.IsNullOrEmpty (text) ? 0 : control.LogicalToDeviceUnits (TextRenderer.MeasureText (text, font).Width);
            var image_size = image is null ? Size.Empty
                           : strip_item is { ImageScaling: ToolStripItemImageScaling.None } ? new Size (image.Width, image.Height)
                           : new Size (control.LogicalToDeviceUnits (control.ImageScalingSize.Width), control.LogicalToDeviceUnits (control.ImageScalingSize.Height));

            // The image and the text side by side, placed together by the alignment's column.
            var block = System.Math.Min (content.Width, image_size.Width + text_width);
            var left = align switch {
                ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => content.X,
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => content.Right - block,
                _ => content.X + (content.Width - block) / 2
            };

            if (image is not null) {
                var image_rect = new Rectangle (left, content.Y + (content.Height - image_size.Height) / 2, image_size.Width, image_size.Height);
                e.Canvas.DrawBitmap (image, image_rect, !item.Enabled);
                left += image_size.Width;
            }

            if (text_width == 0)
                return;

            // TextRenderer draws the text a sixth of the line height in from the left of its measured box.
            var overhang = control.LogicalToDeviceUnits ((int) System.Math.Ceiling (font.Height / 6f));
            var text_rect = new Rectangle (left + overhang, content.Y, System.Math.Max (0, System.Math.Min (text_width, content.Right - left) - overhang), content.Height);
            var vertical = align switch {
                ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => ContentAlignment.TopLeft,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => ContentAlignment.BottomLeft,
                _ => ContentAlignment.MiddleLeft
            };
            var colour = item.Enabled ? Theme.ForegroundColor : Theme.ForegroundDisabledColor;

            e.Canvas.DrawText (text!, TypefaceCache.Resolve (font), control.LogicalToDeviceUnits ((int) System.Math.Round (font.PixelSize)), text_rect, colour, vertical, maxLines: 1);
        }

        private static void RenderLabelBorder (PaintEventArgs e, Rectangle bounds, ToolStripStatusLabelBorderSides sides)
        {
            var colour = Theme.BorderLowColor;

            if (sides.HasFlag (ToolStripStatusLabelBorderSides.Left))
                e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1, colour);
            if (sides.HasFlag (ToolStripStatusLabelBorderSides.Top))
                e.Canvas.DrawLine (bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top, colour);
            if (sides.HasFlag (ToolStripStatusLabelBorderSides.Right))
                e.Canvas.DrawLine (bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1, colour);
            if (sides.HasFlag (ToolStripStatusLabelBorderSides.Bottom))
                e.Canvas.DrawLine (bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1, colour);
        }
    }
}
