using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // In ported mode (Application.SetDefaultFont) an MDI form's background image shows on the workspace behind its
    // children, as upstream's does. Measured on WinForms (.NET 10): the MdiClient, which fills the form, reports the
    // form's BackgroundImage while it has none of its own and draws it on the workspace colour (AppWorkspace, the form's
    // BackColor is not taken); the form's BackgroundImageLayout is the one used even when the client was given an image
    // and a layout of its own; Paint on the MdiClient found in the form's Controls is raised over it. Here the client
    // fills the form, so the form's image was never seen.
    [Collection ("Headless")]
    public class MdiClientBackgroundImageTests
    {
        private const int ImageWidth = 40;
        private const int ImageHeight = 20;

        // Blue top-left quarter, red elsewhere: where the quarter lands tells the layout apart.
        private static Majorsilence.Forms.Drawing.Image TwoToneImage ()
        {
            var bitmap = new SKBitmap (ImageWidth, ImageHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas (bitmap)) {
                canvas.Clear (SKColors.Red);
                using var blue = new SKPaint { Color = SKColors.Blue };
                canvas.DrawRect (0, 0, ImageWidth / 2, ImageHeight / 2, blue);
            }

            Majorsilence.Forms.Drawing.Image? image = bitmap;
            return image!;
        }

        private static Form MdiForm ()
        {
            HeadlessRenderer.Use ();
            // Borderless, so the client starts at the capture's corner on every platform.
            return new Form { IsMdiContainer = true, FormBorderStyle = FormBorderStyle.None, BackColor = Color.DarkGray };
        }

        private static SKBitmap Capture (Form form) => SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 400, 300));

        private static void Ported (Action test)
        {
            Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12f));
            try {
                test ();
            } finally {
                Application.SetDefaultFont (null!);
            }
        }

        private static SKColor Workspace => SystemColors.AppWorkspace.ToSKColor ();

        [Fact]
        public void The_forms_image_is_centred_on_the_workspace ()
        {
            Ported (() => {
                using var form = MdiForm ();
                var image = TwoToneImage ();
                form.BackgroundImage = image;
                form.BackgroundImageLayout = ImageLayout.Center;
                var client = form.Controls.OfType<MdiClient> ().Single ();

                Assert.Same (image, client.BackgroundImage);
                Assert.Equal (ImageLayout.Center, client.BackgroundImageLayout);

                using var bitmap = Capture (form);
                // The image is drawn at its own pixel size, centred in the client.
                var left = (bitmap.Width - ImageWidth) / 2;
                var top = (bitmap.Height - ImageHeight) / 2;

                Assert.Equal (SKColors.Blue, bitmap.GetPixel (left + 2, top + 2));
                Assert.Equal (SKColors.Red, bitmap.GetPixel (left + ImageWidth - 3, top + ImageHeight - 3));
                // Around it, the workspace colour -- not the form's BackColor.
                Assert.Equal (Workspace, bitmap.GetPixel (left - 5, top - 5));
                Assert.Equal (Workspace, bitmap.GetPixel (5, 5));
            });
        }

        [Fact]
        public void The_forms_layout_wins_over_the_clients_own ()
        {
            Ported (() => {
                using var form = MdiForm ();
                form.BackgroundImage = TwoToneImage ();
                form.BackgroundImageLayout = ImageLayout.None;
                var client = form.Controls.OfType<MdiClient> ().Single ();
                client.BackgroundImageLayout = ImageLayout.Center;

                Assert.Equal (ImageLayout.None, client.BackgroundImageLayout);

                using var bitmap = Capture (form);
                Assert.Equal (SKColors.Blue, bitmap.GetPixel (2, 2));
                Assert.Equal (SKColors.Red, bitmap.GetPixel (ImageWidth - 3, ImageHeight - 3));
                Assert.Equal (Workspace, bitmap.GetPixel (ImageWidth + 5, ImageHeight + 5));

                // Changing the form's layout repaints the client.
                form.Show ();
                var invalidated = false;
                client.Invalidated += (_, _) => invalidated = true;
                form.BackgroundImageLayout = ImageLayout.Tile;
                Assert.True (invalidated);
                Assert.Equal (ImageLayout.Tile, client.BackgroundImageLayout);
                using var tiled = Capture (form);
                Assert.Equal (SKColors.Blue, tiled.GetPixel (ImageWidth + 2, ImageHeight + 2));
            });
        }

        [Fact]
        public void The_forms_image_tiles_across_the_workspace ()
        {
            Ported (() => {
                using var form = MdiForm ();
                form.BackgroundImage = TwoToneImage ();
                form.BackgroundImageLayout = ImageLayout.Tile;

                using var bitmap = Capture (form);
                Assert.Equal (SKColors.Blue, bitmap.GetPixel (2, 2));
                Assert.Equal (SKColors.Blue, bitmap.GetPixel (ImageWidth + 2, ImageHeight + 2));
                Assert.Equal (SKColors.Red, bitmap.GetPixel (2 * ImageWidth - 3, 2 * ImageHeight - 3));
            });
        }

        [Fact]
        public void The_clients_own_image_is_drawn_in_the_forms_layout ()
        {
            Ported (() => {
                using var form = MdiForm ();
                form.BackgroundImageLayout = ImageLayout.None;
                var client = form.Controls.OfType<MdiClient> ().Single ();
                var own = TwoToneImage ();
                client.BackgroundImage = own;
                client.BackgroundImageLayout = ImageLayout.Center;

                Assert.Same (own, client.BackgroundImage);
                Assert.Equal (ImageLayout.None, client.BackgroundImageLayout);

                using var bitmap = Capture (form);
                Assert.Equal (SKColors.Blue, bitmap.GetPixel (2, 2));
            });
        }

        [Fact]
        public void A_paint_handler_on_the_client_draws_over_the_image ()
        {
            Ported (() => {
                using var form = MdiForm ();
                form.BackgroundImage = TwoToneImage ();
                form.BackgroundImageLayout = ImageLayout.None;
                var client = form.Controls.OfType<MdiClient> ().Single ();
                var paints = 0;
                client.Paint += (_, e) => {
                    paints++;
                    using var lime = new Majorsilence.Forms.Drawing.SolidBrush (Color.Lime);
                    e.Graphics.FillRectangle (lime, 0, 0, 4, 4);
                };

                using var bitmap = Capture (form);
                Assert.True (paints > 0);
                Assert.Equal (SKColors.Lime, bitmap.GetPixel (1, 1));
                Assert.Equal (SKColors.Red, bitmap.GetPixel (ImageWidth - 3, ImageHeight - 3));
            });
        }

        // An app that never chose a font keeps the bare workspace: the form's image stays the form's.
        [Fact]
        public void Without_a_chosen_font_the_workspace_stays_bare ()
        {
            using var form = MdiForm ();
            form.BackgroundImage = TwoToneImage ();
            form.BackgroundImageLayout = ImageLayout.Center;
            var client = form.Controls.OfType<MdiClient> ().Single ();

            Assert.Null (client.BackgroundImage);

            using var bitmap = Capture (form);
            Assert.Equal (Workspace, bitmap.GetPixel (bitmap.Width / 2, bitmap.Height / 2));
        }
    }
}
