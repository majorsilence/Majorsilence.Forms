using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// A <see cref="PictureBox"/> with <see cref="PictureBoxSizeMode.AutoSize"/> takes the size of its image.
/// </summary>
/// <remarks>
/// It used to only ask its parent to lay out, which is a no-op for a control that is neither docked nor
/// anchored — so the box stayed at its 100x50 default however big the image was. WinForms sizes the
/// control itself here, and does not require Control.AutoSize to be set.
///
/// A docking library's drop guides are built exactly this way: one PictureBox per guide, sized by its own
/// artwork. Left at the default, the guides were hit-tested against a box smaller than the cluster drawn
/// on screen, and the hot-spot lookup indexed that artwork at coordinates that did not correspond to it,
/// so dropping on a lobe mostly missed.
/// </remarks>
public class PictureBoxAutoSizeTests
{
    private static SKBitmap Image (int width, int height) => new (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

    [Fact]
    public void AutoSize_takes_the_size_of_the_image ()
    {
        var box = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };

        box.SetSKImage (Image (108, 108));

        Assert.Equal (new Size (108, 108), box.Size);
    }

    [Fact]
    public void Switching_to_AutoSize_resizes_an_image_already_set ()
    {
        var box = new PictureBox ();
        box.SetSKImage (Image (64, 32));

        Assert.Equal (new Size (100, 50), box.Size);   // the default, until the mode says otherwise

        box.SizeMode = PictureBoxSizeMode.AutoSize;

        Assert.Equal (new Size (64, 32), box.Size);
    }

    [Fact]
    public void A_new_image_resizes_the_box_again ()
    {
        var box = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
        box.SetSKImage (Image (40, 40));

        box.SetSKImage (Image (90, 20));

        Assert.Equal (new Size (90, 20), box.Size);
    }

    [Fact]
    public void The_other_size_modes_leave_the_box_alone ()
    {
        foreach (var mode in new[] {
                     PictureBoxSizeMode.Normal, PictureBoxSizeMode.StretchImage,
                     PictureBoxSizeMode.CenterImage, PictureBoxSizeMode.Zoom }) {
            var box = new PictureBox { Width = 200, Height = 150, SizeMode = mode };

            box.SetSKImage (Image (33, 44));

            Assert.Equal (new Size (200, 150), box.Size);
        }
    }

    [Fact]
    public void The_preferred_size_is_the_image_under_AutoSize ()
    {
        // What a layout engine asks, and the answer it needs to give the guides their designed size.
        var box = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
        box.SetSKImage (Image (108, 108));

        Assert.Equal (new Size (108, 108), box.GetPreferredSize (Size.Empty));
    }

    [Fact]
    public void An_AutoSize_picture_box_actually_draws_its_image ()
    {
        // The renderer's AutoSize arm was commented out of a switch with no default, so the image was
        // never painted -- a docking library's drop guides are AutoSize picture boxes, and all that showed
        // where the guides belonged was the bare background of the window carrying them.
        HeadlessRenderer.Use ();

        // Borderless, so the client area starts at the window's own origin on every platform: Windows
        // and Linux draw the library's title bar over the top 34 logical pixels of a decorated form,
        // which is where the box -- and the pixel sampled below -- would otherwise be.
        using var form = new Form {
            Width = 80, Height = 80, BackColor = System.Drawing.Color.White,
            FormBorderStyle = FormBorderStyle.None,
        };
        var box = new PictureBox { Left = 0, Top = 0, SizeMode = PictureBoxSizeMode.AutoSize };
        form.Controls.Add (box);

        var bitmap = new SKBitmap (40, 40, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas (bitmap))
            canvas.Clear (SKColors.Lime);
        box.SetSKImage (bitmap);

        using var rendered = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 80, 80));
        var pixel = rendered.GetPixel (20, 20);

        Assert.Equal (SKColors.Lime.Green, pixel.Green);
        Assert.Equal (SKColors.Lime.Red, pixel.Red);
    }
}
