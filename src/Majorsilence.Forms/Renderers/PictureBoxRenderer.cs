using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a PictureBox.
    /// </summary>
    public class PictureBoxRenderer : Renderer<PictureBox>
    {
        // The client rectangle inset by the padding, both in device pixels. Control.PaddedClientRectangle
        // insets the device client rectangle by the LOGICAL padding, so at scale 2 a padded image sat
        // half as far in as it should (RC-8) -- found by the padded-zoom regression test at scale 2.
        private static Rectangle PaddedDeviceRectangle (PictureBox control)
        {
            var client = control.ClientRectangle;
            var padding = control.LogicalToDeviceUnits (control.Padding);

            return new Rectangle (client.X + padding.Left, client.Y + padding.Top,
                Math.Max (0, client.Width - padding.Horizontal), Math.Max (0, client.Height - padding.Vertical));
        }

        /// <inheritdoc/>
        protected override void Render (PictureBox control, PaintEventArgs e)
        {
            if (control.SKImage != null) {
                var client = PaddedDeviceRectangle (control);

                switch (control.SizeMode) {
                    // AutoSize draws the image at its natural size, exactly as Normal does -- the
                    // difference between them is that AutoSize also resizes the control to match (see
                    // PictureBox.UpdateSize), not how the image is painted. Commented out, this arm fell
                    // through a switch with no default and the image was never drawn at all: a docking
                    // library's drop guides are AutoSize picture boxes, so all that appeared where the
                    // guides should be was the bare background of the window carrying them.
                    case PictureBoxSizeMode.AutoSize:
                    case PictureBoxSizeMode.Normal:
                        e.Canvas.DrawBitmap (control.SKImage, new Rectangle (0, 0, control.SKImage.Width, control.SKImage.Height), !control.Enabled);
                        break;
                    case PictureBoxSizeMode.StretchImage:
                        e.Canvas.DrawBitmap (control.SKImage, client, !control.Enabled);
                        break;
                    // CenterImage and Zoom centre within the PADDED client rectangle, so its origin is
                    // part of the position; measuring from 0 instead pushed a padded image up and to the
                    // left by the padding.
                    case PictureBoxSizeMode.CenterImage:
                        e.Canvas.DrawBitmap (control.SKImage, client.X + (client.Width / 2) - (control.SKImage.Width / 2), client.Y + (client.Height / 2) - (control.SKImage.Height / 2), !control.Enabled);
                        break;
                    case PictureBoxSizeMode.Zoom:
                        Size image_size;

                        if (((float)control.SKImage.Width / control.SKImage.Height) >= ((float)client.Width / client.Height))
                            image_size = new Size (client.Width, (control.SKImage.Height * client.Width) / control.SKImage.Width);
                        else
                            image_size = new Size ((control.SKImage.Width * client.Height) / control.SKImage.Height, client.Height);

                        e.Canvas.DrawBitmap (control.SKImage, new Rectangle (client.X + (client.Width / 2) - (image_size.Width / 2), client.Y + (client.Height / 2) - (image_size.Height / 2), image_size.Width, image_size.Height), !control.Enabled);
                        break;
                }
            } else if (control.IsErrored) {
                var client = PaddedDeviceRectangle (control);

                e.Canvas.DrawLine (client.Left, client.Top, client.Right, client.Bottom, Theme.WarningHighlightColor, control.LogicalToDeviceUnits (2));
                e.Canvas.DrawLine (client.Left, client.Bottom, client.Right, client.Top, Theme.WarningHighlightColor, control.LogicalToDeviceUnits (2));
            }
        }
    }
}
