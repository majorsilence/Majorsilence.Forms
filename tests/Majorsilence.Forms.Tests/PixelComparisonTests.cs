using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The headless backend renders to PNG, but comparing a render against a committed baseline with a
// tolerance for anti-aliasing was left to every application; Recital wrote its own (FRAMEWORK_GAPS
// GAP-002). PixelComparison is that check, independent of any test framework.
[Collection ("Headless")]
public sealed class PixelComparisonTests
{
    private static byte[] Png (int width, int height, Action<SKCanvas> draw)
    {
        using var bitmap = new SKBitmap (width, height);
        using (var canvas = new SKCanvas (bitmap)) {
            canvas.Clear (SKColors.White);
            draw (canvas);
        }
        using var image = SKImage.FromBitmap (bitmap);
        using var data = image.Encode (SKEncodedImageFormat.Png, 100);
        return data.ToArray ();
    }

    private static readonly byte[] blank = Png (20, 10, _ => { });

    [Fact]
    public void Identical_images_match ()
    {
        var result = PixelComparison.Compare (blank, Png (20, 10, _ => { }));

        Assert.True (result.SizesMatch);
        Assert.Equal (0, result.DifferingPixels);
        Assert.Equal (200, result.TotalPixels);
        Assert.True (result.IsWithin (0));
    }

    [Fact]
    public void Each_changed_pixel_is_counted ()
    {
        var changed = Png (20, 10, c => {
            using var red = new SKPaint { Color = SKColors.Red };
            c.DrawRect (0, 0, 3, 2, red);
        });

        var result = PixelComparison.Compare (blank, changed);

        Assert.Equal (6, result.DifferingPixels);
        Assert.False (result.IsWithin (0.02));
        Assert.True (result.IsWithin (0.03));
    }

    [Fact]
    public void A_change_within_the_tolerance_is_not_a_difference ()
    {
        var nearly = Png (20, 10, c => c.Clear (new SKColor (250, 250, 250)));

        Assert.Equal (0, PixelComparison.Compare (blank, nearly, channelTolerance: 8).DifferingPixels);
        Assert.Equal (200, PixelComparison.Compare (blank, nearly, channelTolerance: 4).DifferingPixels);
    }

    [Fact]
    public void Different_sizes_never_match ()
    {
        var result = PixelComparison.Compare (blank, Png (21, 10, _ => { }));

        Assert.False (result.SizesMatch);
        Assert.False (result.IsWithin (1));
        Assert.Null (result.CreateDiffPng ());
        Assert.Contains ("20x10", result.ToString ());
    }

    [Fact]
    public void The_diff_image_marks_the_differing_pixels_red ()
    {
        var changed = Png (20, 10, c => {
            using var black = new SKPaint { Color = SKColors.Black };
            c.DrawRect (5, 5, 1, 1, black);
        });

        using var diff = SKBitmap.Decode (PixelComparison.Compare (blank, changed).CreateDiffPng ());

        Assert.Equal (SKColors.Red, diff.GetPixel (5, 5));
        Assert.NotEqual (SKColors.Red, diff.GetPixel (0, 0));
    }

    [Fact]
    public void A_headless_render_matches_itself ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Text = "Baseline" };
        var first = HeadlessRenderer.CapturePng (form, 300, 200);
        var second = HeadlessRenderer.CapturePng (form, 300, 200);

        Assert.True (PixelComparison.Compare (first, second).IsWithin (0));
    }
}
