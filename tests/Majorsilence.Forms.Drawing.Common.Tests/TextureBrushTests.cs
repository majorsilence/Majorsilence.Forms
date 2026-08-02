using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Drawing2D;
using SkiaSharp;
// See GraphicsPathTests for why this is aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Covers the TextureBrush surface added in Phase 1 of docs/gdi-gap-plan.md. Before it, this type was a
/// shell — the constructor and WrapMode were its entire public API, with Image, the transform family and
/// Clone all absent — despite being listed as "Implemented" in the compatibility matrix.
///
/// The transform assertions render actual pixels rather than just round-tripping the matrix, because a
/// stored-but-unapplied transform is exactly the failure mode this phase exists to eliminate.
/// </summary>
public class TextureBrushTests
{
    // A 2x2 texture with four distinct, easily identified colors.
    private static Bitmap MakeTexture ()
    {
        var bmp = new Bitmap (2, 2);
        bmp.SetPixel (0, 0, Color.Red);
        bmp.SetPixel (1, 0, Color.Lime);
        bmp.SetPixel (0, 1, Color.Blue);
        bmp.SetPixel (1, 1, Color.Yellow);
        return bmp;
    }

    // Fills a 4x4 surface with the brush and returns the resulting pixels.
    private static SKColor[,] Render (TextureBrush brush)
    {
        using var surface = new SKBitmap (4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas (surface))
        using (var paint = brush.CreatePaint ())
        {
            canvas.Clear (SKColors.Black);
            canvas.DrawRect (new SKRect (0, 0, 4, 4), paint);
        }

        var pixels = new SKColor[4, 4];
        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                pixels[x, y] = surface.GetPixel (x, y);
        return pixels;
    }

    [Fact]
    public void Image_property_returns_the_source_image ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);

        Assert.Same (texture, brush.Image);
    }

    [Fact]
    public void Transform_defaults_to_identity_and_returns_a_copy ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);

        Assert.True (brush.Transform.IsIdentity);

        // The getter must hand back a copy: mutating it must not affect the brush.
        var snapshot = brush.Transform;
        snapshot.Translate (10f, 10f);
        Assert.True (brush.Transform.IsIdentity);
    }

    [Fact]
    public void Transform_setter_stores_a_copy ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);
        using var matrix = new Matrix ();
        matrix.Translate (5f, 0f);

        brush.Transform = matrix;
        matrix.Translate (100f, 0f);     // mutating the source afterwards must not leak in

        Assert.Equal (5f, brush.Transform.Elements[4], 3);
    }

    [Fact]
    public void Transform_setter_accepts_null_as_reset ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);
        brush.TranslateTransform (7f, 7f);

        brush.Transform = null!;

        Assert.True (brush.Transform.IsIdentity);
    }

    [Fact]
    public void Untransformed_brush_tiles_the_texture_from_the_origin ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);

        var pixels = Render (brush);

        // The 2x2 texture repeats, so (0,0) and (2,2) are both the texture's top-left pixel.
        Assert.Equal (SKColors.Red, pixels[0, 0]);
        Assert.Equal (SKColors.Lime, pixels[1, 0]);
        Assert.Equal (SKColors.Blue, pixels[0, 1]);
        Assert.Equal (SKColors.Red, pixels[2, 2]);
    }

    [Fact]
    public void TranslateTransform_actually_shifts_the_rendered_texture ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);

        var before = Render (brush);
        brush.TranslateTransform (1f, 0f);
        var after = Render (brush);

        // Shifting right by one texel moves the green texel from x=1 to x=0.
        Assert.Equal (SKColors.Red, before[0, 0]);
        Assert.Equal (SKColors.Lime, after[0, 0]);
        Assert.Equal (SKColors.Red, after[1, 0]);
    }

    [Fact]
    public void ScaleTransform_actually_stretches_the_rendered_texture ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);
        brush.ScaleTransform (2f, 2f);

        var pixels = Render (brush);

        // Each texel now covers 2x2 device pixels, so the whole top-left quadrant is the red texel.
        Assert.Equal (SKColors.Red, pixels[0, 0]);
        Assert.Equal (SKColors.Red, pixels[1, 0]);
        Assert.Equal (SKColors.Red, pixels[0, 1]);
        Assert.Equal (SKColors.Red, pixels[1, 1]);
        Assert.Equal (SKColors.Lime, pixels[2, 0]);
    }

    [Fact]
    public void ResetTransform_restores_the_untransformed_rendering ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);

        var original = Render (brush);
        brush.TranslateTransform (1f, 1f);
        brush.ResetTransform ();
        var restored = Render (brush);

        Assert.True (brush.Transform.IsIdentity);
        Assert.Equal (original[0, 0], restored[0, 0]);
        Assert.Equal (original[1, 0], restored[1, 0]);
    }

    [Fact]
    public void MultiplyTransform_composes_with_the_existing_transform ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture);
        brush.TranslateTransform (1f, 0f);

        using var second = new Matrix ();
        second.Translate (1f, 0f);
        brush.MultiplyTransform (second);

        // Two single-texel shifts on a 2-wide texture land back where it started.
        Assert.Equal (2f, brush.Transform.Elements[4], 3);
        Assert.Equal (SKColors.Red, Render (brush)[0, 0]);
    }

    [Fact]
    public void Clone_copies_image_wrap_mode_and_transform ()
    {
        using var texture = MakeTexture ();
        using var brush = new TextureBrush (texture, WrapMode.Clamp);
        brush.TranslateTransform (1f, 0f);

        using var clone = brush.Clone ();

        Assert.Same (texture, clone.Image);
        Assert.Equal (WrapMode.Clamp, clone.WrapMode);
        Assert.Equal (1f, clone.Transform.Elements[4], 3);

        // ...and is genuinely independent of the original.
        clone.TranslateTransform (5f, 0f);
        Assert.Equal (1f, brush.Transform.Elements[4], 3);
    }
}
