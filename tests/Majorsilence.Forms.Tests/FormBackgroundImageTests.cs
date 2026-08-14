using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="WindowBase.BackgroundImage"/> is painted across the window's client area.
/// </summary>
/// <remarks>
/// Form.BackgroundImage forwards to the root adapter, and the adapter's background pass returned early
/// before drawing it — the window paints its own colour and border, so the adapter skipped the lot,
/// image included. That made the property stored-and-never-drawn: a splash screen built the usual way,
/// a borderless form whose entire content is BackgroundImage, came up as a blank white rectangle sitting
/// over the application for as long as it was shown.
/// </remarks>
[Collection ("Headless")]
public class FormBackgroundImageTests
{
    private const int Size = 60;

    private static Majorsilence.Forms.Drawing.Image SolidImage (SKColor colour)
    {
        var bitmap = new SKBitmap (Size, Size, SKColorType.Bgra8888, SKAlphaType.Premul);

        using (var canvas = new SKCanvas (bitmap))
            canvas.Clear (colour);

        Majorsilence.Forms.Drawing.Image? image = bitmap;   // implicit SKBitmap -> Image
        return image!;
    }

    private static SKBitmap Render (bool withImage)
    {
        HeadlessRenderer.Use ();

        // Borderless: the splash screen this exists for, and the only way the sampled pixel means the
        // same thing on every platform. Windows and Linux paint the library's own title bar over the
        // top 34 logical pixels of a decorated form, which on a 60x60 window covers the centre.
        using var form = new Form {
            Width = Size, Height = Size, BackColor = Color.White,
            FormBorderStyle = FormBorderStyle.None,
        };

        if (withImage)
            form.BackgroundImage = SolidImage (SKColors.Lime);

        return SKBitmap.Decode (HeadlessRenderer.CapturePng (form, Size, Size));
    }

    [Fact]
    public void The_background_image_is_drawn ()
    {
        using var bitmap = Render (withImage: true);

        var centre = bitmap.GetPixel (Size / 2, Size / 2);

        Assert.Equal (SKColors.Lime.Red, centre.Red);
        Assert.Equal (SKColors.Lime.Green, centre.Green);
        Assert.Equal (SKColors.Lime.Blue, centre.Blue);
    }

    [Fact]
    public void Without_an_image_the_window_keeps_its_own_background ()
    {
        using var bitmap = Render (withImage: false);

        Assert.NotEqual (SKColors.Lime, bitmap.GetPixel (Size / 2, Size / 2));
    }

    [Fact]
    public void The_image_reads_back_as_assigned ()
    {
        using var form = new Form { Width = Size, Height = Size };
        var image = SolidImage (SKColors.Lime);

        form.BackgroundImage = image;

        Assert.Same (image, form.BackgroundImage);
    }

    [Fact]
    public void Clearing_the_image_stops_it_being_drawn ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form {
            Width = Size, Height = Size, BackColor = Color.White,
            FormBorderStyle = FormBorderStyle.None,
        };
        form.BackgroundImage = SolidImage (SKColors.Lime);
        HeadlessRenderer.CapturePng (form, Size, Size);

        form.BackgroundImage = null;

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, Size, Size));

        Assert.NotEqual (SKColors.Lime, bitmap.GetPixel (Size / 2, Size / 2));
    }
}
