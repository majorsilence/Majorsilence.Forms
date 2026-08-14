using System.Drawing;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a PictureBox.
    /// </summary>
    public class PictureBoxRenderer : Renderer<PictureBox>
    {
        /// <inheritdoc/>
        protected override void Render (PictureBox control, PaintEventArgs e)
        {
            if (control.SKImage != null) {
                var client = control.PaddedClientRectangle;

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
                    case PictureBoxSizeMode.CenterImage:
                        e.Canvas.DrawBitmap (control.SKImage, (client.Width / 2) - (control.SKImage.Width / 2), (client.Height / 2) - (control.SKImage.Height / 2), !control.Enabled);
                        break;
                    case PictureBoxSizeMode.Zoom:
                        Size image_size;

                        if (((float)control.SKImage.Width / control.SKImage.Height) >= ((float)client.Width / client.Height))
                            image_size = new Size (client.Width, (control.SKImage.Height * client.Width) / control.SKImage.Width);
                        else
                            image_size = new Size ((control.SKImage.Width * client.Height) / control.SKImage.Height, client.Height);

                        e.Canvas.DrawBitmap (control.SKImage, new Rectangle ((client.Width / 2) - (image_size.Width / 2), (client.Height / 2) - (image_size.Height / 2), image_size.Width, image_size.Height), !control.Enabled);
                        break;
                }
            } else if (control.IsErrored) {
                var client = control.PaddedClientRectangle;

                e.Canvas.DrawLine (client.Left, client.Top, client.Right, client.Bottom, Theme.WarningHighlightColor, control.LogicalToDeviceUnits (2));
                e.Canvas.DrawLine (client.Left, client.Bottom, client.Right, client.Top, Theme.WarningHighlightColor, control.LogicalToDeviceUnits (2));
            }
        }
    }
}
