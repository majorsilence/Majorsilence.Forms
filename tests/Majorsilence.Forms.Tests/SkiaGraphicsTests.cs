using System.Drawing;
using Majorsilence.Forms.Drawing;
using SkiaSharp;
using Xunit;
using Font = Majorsilence.Forms.Drawing.Font;
using Pen = Majorsilence.Forms.Drawing.Pen;
using SolidBrush = Majorsilence.Forms.Drawing.SolidBrush;

namespace Majorsilence.Forms.Tests;

public class SkiaGraphicsTests
{
    [Fact]
    public void FillRectangle_FillsPixels ()
    {
        using var bitmap = new SKBitmap (100, 100);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        var g = new SkiaGraphics (canvas);
        g.FillRectangle (new SolidBrush (Color.Red), new RectangleF (10, 10, 80, 80));

        var center = bitmap.GetPixel (50, 50);
        Assert.Equal ((byte)255, center.Red);
        Assert.Equal ((byte)0, center.Green);
        Assert.Equal ((byte)0, center.Blue);

        // A corner outside the rectangle stays white.
        var corner = bitmap.GetPixel (2, 2);
        Assert.Equal ((byte)255, corner.Green);
        Assert.Equal ((byte)255, corner.Blue);
    }

    [Fact]
    public void DrawLine_DrawsPixels ()
    {
        using var bitmap = new SKBitmap (100, 100);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        var g = new SkiaGraphics (canvas);
        g.DrawLine (new Pen (Color.Blue, 4f), 0, 50, 100, 50);

        var on_line = bitmap.GetPixel (50, 50);
        Assert.True (on_line.Blue > on_line.Red, "Pixel on the blue line should be predominantly blue.");
    }

    [Fact]
    public void TranslateTransform_OffsetsDrawing ()
    {
        using var bitmap = new SKBitmap (100, 100);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        var g = new SkiaGraphics (canvas);
        g.TranslateTransform (40, 40);
        g.FillRectangle (new SolidBrush (Color.Green), new RectangleF (0, 0, 10, 10));

        // The rectangle was drawn at (0,0) but translated to (40,40).
        Assert.Equal ((byte)128, bitmap.GetPixel (45, 45).Green);   // Color.Green is (0,128,0)
        Assert.Equal ((byte)255, bitmap.GetPixel (5, 5).Green);     // origin area remains white
    }

    [Fact]
    public void Save_Restore_RestoresTransform ()
    {
        using var bitmap = new SKBitmap (100, 100);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        var g = new SkiaGraphics (canvas);
        var state = g.Save ();
        g.TranslateTransform (50, 50);
        g.Restore (state);

        // After restore, drawing at (0,0) is not offset.
        g.FillRectangle (new SolidBrush (Color.Black), new RectangleF (0, 0, 10, 10));
        Assert.Equal ((byte)0, bitmap.GetPixel (5, 5).Red);
    }

    [Fact]
    public void MeasureString_ReturnsPositiveSize ()
    {
        using var bitmap = new SKBitmap (10, 10);
        using var canvas = new SKCanvas (bitmap);
        var g = new SkiaGraphics (canvas);

        using var font = new Font ("Arial", 12f);
        var size = g.MeasureString ("Hello", font);

        Assert.True (size.Width > 0);
        Assert.True (size.Height > 0);
    }

    [Fact]
    public void DrawString_RendersInkPixels ()
    {
        using var bitmap = new SKBitmap (200, 60);
        using var canvas = new SKCanvas (bitmap);
        canvas.Clear (SKColors.White);

        var g = new SkiaGraphics (canvas);
        using var font = new Font ("Arial", 24f, bold: true);
        g.DrawString ("ABC", font, new SolidBrush (Color.Black), new PointF (5, 5));

        // At least some non-white ink should have been produced.
        var ink = 0;
        for (var x = 0; x < bitmap.Width; x++)
            for (var y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel (x, y).Red < 200)
                    ink++;

        Assert.True (ink > 0, "DrawString should produce visible ink.");
    }
}
