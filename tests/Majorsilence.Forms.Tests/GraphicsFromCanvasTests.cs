using System;
using System.IO;
using System.Text;
using SkiaSharp;
using Xunit;
using MFD = Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms.Tests;

// Graphics.FromCanvas lets a caller draw with the GDI-style API onto a canvas it made itself: an
// SKSvgCanvas, a PDF page, a picture recorder. Before it existed the only way in was the internal
// Graphics (SKCanvas, Control) constructor, which Majorsilence Reporting's SVG export reached by
// reflection.
public class GraphicsFromCanvasTests
{
    [Fact]
    public void Drawing_lands_on_the_callers_canvas ()
    {
        using var bitmap = new SKBitmap (20, 20);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        using (var g = Graphics.FromCanvas (canvas))
            g.FillRectangle (MFD.Brushes.Red, 5, 5, 10, 10);

        Assert.Equal (SKColors.Red, bitmap.GetPixel (10, 10));
        Assert.Equal (SKColors.White, bitmap.GetPixel (1, 1));
    }

    [Fact]
    public void Disposing_the_graphics_leaves_the_canvas_usable ()
    {
        using var bitmap = new SKBitmap (20, 20);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);
        int depth = canvas.SaveCount;

        using (var g = Graphics.FromCanvas (canvas)) {
            g.SetClip (new System.Drawing.Rectangle (0, 0, 5, 5));
            g.FillRectangle (MFD.Brushes.Red, 0, 0, 20, 20);
        }

        // The clip was undone, and the canvas was not disposed, so it can still be drawn on everywhere.
        Assert.Equal (depth, canvas.SaveCount);
        canvas.DrawRect (new SKRect (10, 10, 20, 20), new SKPaint { Color = SKColors.Blue });
        Assert.Equal (SKColors.Blue, bitmap.GetPixel (15, 15));
        Assert.Equal (SKColors.White, bitmap.GetPixel (8, 8));
    }

    [Fact]
    public void A_transform_set_through_the_graphics_stays_on_the_canvas_after_dispose ()
    {
        // As FromCanvas documents: the clip is undone on dispose, the matrix is not, so a caller who
        // reuses the canvas saves and restores around the drawing.
        using var bitmap = new SKBitmap (20, 20);
        using var canvas = new SKCanvas (bitmap);

        using (var g = Graphics.FromCanvas (canvas))
            g.TranslateTransform (7, 0);
        Assert.Equal (7f, canvas.TotalMatrix.TransX);

        int saved = canvas.Save ();
        using (var g = Graphics.FromCanvas (canvas))
            g.TranslateTransform (5, 0);
        canvas.RestoreToCount (saved);
        Assert.Equal (7f, canvas.TotalMatrix.TransX);
    }

    [Fact]
    public void The_canvas_matrix_is_the_origin_and_transform_starts_at_identity ()
    {
        using var bitmap = new SKBitmap (40, 40);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);
        canvas.Translate (20, 20);

        using (var g = Graphics.FromCanvas (canvas)) {
            Assert.True (g.Transform.IsIdentity);
            g.FillRectangle (MFD.Brushes.Red, 0, 0, 5, 5);
        }

        Assert.Equal (SKColors.Red, bitmap.GetPixel (22, 22));
        Assert.Equal (SKColors.White, bitmap.GetPixel (2, 2));
    }

    [Fact]
    public void The_resolution_sets_dpi_and_the_page_unit_scale ()
    {
        using var bitmap = new SKBitmap (200, 200);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        using (var g = Graphics.FromCanvas (canvas, 144f)) {
            Assert.Equal (144f, g.DpiX);
            Assert.Equal (144f, g.DpiY);

            // Half an inch at 144 units to the inch is 72 units.
            g.PageUnit = MFD.GraphicsUnit.Inch;
            g.FillRectangle (MFD.Brushes.Red, 0f, 0f, 0.5f, 0.5f);
        }

        Assert.Equal (SKColors.Red, bitmap.GetPixel (70, 70));
        Assert.Equal (SKColors.White, bitmap.GetPixel (74, 74));
    }

    [Fact]
    public void The_default_resolution_is_96 ()
    {
        using var bitmap = new SKBitmap (4, 4);
        using var canvas = new SKCanvas (bitmap);
        using var g = Graphics.FromCanvas (canvas);

        Assert.Equal (96f, g.DpiX);
    }

    [Theory]
    [InlineData (0f)]
    [InlineData (-1f)]
    [InlineData (float.NaN)]
    [InlineData (float.PositiveInfinity)]
    public void A_resolution_that_is_not_positive_and_finite_is_refused (float dpi)
    {
        using var bitmap = new SKBitmap (4, 4);
        using var canvas = new SKCanvas (bitmap);

        Assert.Throws<ArgumentOutOfRangeException> (() => Graphics.FromCanvas (canvas, dpi));
    }

    [Fact]
    public void A_null_canvas_is_refused ()
    {
        Assert.Throws<ArgumentNullException> (() => Graphics.FromCanvas (null!));
    }

    [Fact]
    public void Shapes_drawn_onto_an_svg_canvas_come_out_as_vectors ()
    {
        using var stream = new MemoryStream ();
        using (var wstream = new SKManagedWStream (stream))
        using (var canvas = SKSvgCanvas.Create (new SKRect (0, 0, 100, 100), wstream))
        using (var g = Graphics.FromCanvas (canvas))
            g.FillRectangle (MFD.Brushes.Red, 10, 10, 30, 30);

        string svg = Encoding.UTF8.GetString (stream.ToArray ());
        Assert.Contains ("<rect", svg);
        Assert.DoesNotContain ("<image", svg);
    }
}
