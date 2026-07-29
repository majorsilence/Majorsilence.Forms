using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;
using Xunit;
using ColorMatrix = Majorsilence.Forms.Drawing.Imaging.ColorMatrix;
using ImageAttributes = Majorsilence.Forms.Drawing.Imaging.ImageAttributes;
using Pen = Majorsilence.Forms.Drawing.Pen;
using SolidBrush = Majorsilence.Forms.Drawing.SolidBrush;

namespace Majorsilence.Forms.Tests;

public class GraphicsContainerTests
{
    [Fact]
    public void Save_ReturnsANonNullState ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (50, 50);
        using var g = Graphics.FromImage (target);

        var state = g.Save ();
        Assert.NotNull (state);
        g.Restore (state);
    }

    [Fact]
    public void Save_Restore_RewindsTheTransform ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (50, 50);
        using var g = Graphics.FromImage (target);

        var state = g.Save ();
        g.TranslateTransform (25, 25);
        g.Restore (state);

        g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (0, 0, 10, 10));

        Assert.Equal ((byte)255, target.GetPixel (5, 5).R);
        Assert.Equal ((byte)0, target.GetPixel (5, 5).G);
        Assert.Equal ((byte)0, target.GetPixel (30, 30).A); // nothing was drawn at the translated origin
    }

    [Fact]
    public void Restore_WithNullState_DoesNotThrow ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);

        g.Save ();
        g.Restore (null!);
    }

    [Fact]
    public void BeginContainer_EndContainer_RewindsTheTransform ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (50, 50);
        using var g = Graphics.FromImage (target);

        var container = g.BeginContainer ();
        g.TranslateTransform (25, 25);
        g.EndContainer (container);

        g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (0, 0, 10, 10));

        Assert.Equal ((byte)255, target.GetPixel (5, 5).R);
        Assert.Equal ((byte)0, target.GetPixel (30, 30).A);
    }

    [Fact]
    public void Containers_NestWithSaveRestore ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (60, 60);
        using var g = Graphics.FromImage (target);

        var outer = g.BeginContainer ();
        g.TranslateTransform (10, 10);
        var inner = g.Save ();
        g.TranslateTransform (10, 10);
        g.Restore (inner);

        // Back to the outer container's transform: (10, 10).
        g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (0, 0, 5, 5));
        Assert.Equal ((byte)255, target.GetPixel (12, 12).R);

        g.EndContainer (outer);
        g.FillRectangle (new SolidBrush (Color.Lime), new Rectangle (0, 0, 5, 5));
        Assert.Equal ((byte)255, target.GetPixel (2, 2).G);
    }

    [Fact]
    public void BeginContainer_WithRectangles_MapsSourceOntoDestination ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (100, 100);
        using var g = Graphics.FromImage (target);

        // A 10x10 source space stretched onto a 50x50 destination at (50, 50).
        var container = g.BeginContainer (
            new Rectangle (50, 50, 50, 50),
            new Rectangle (0, 0, 10, 10),
            Majorsilence.Forms.Drawing.GraphicsUnit.Pixel);

        g.FillRectangle (new SolidBrush (Color.Red), new Rectangle (0, 0, 10, 10));
        g.EndContainer (container);

        Assert.Equal ((byte)255, target.GetPixel (75, 75).R);
        Assert.Equal ((byte)0, target.GetPixel (25, 25).A); // clipped to the destination rectangle
    }

    [Fact]
    public void EndContainer_WithNullContainer_DoesNotThrow ()
    {
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);

        g.BeginContainer ();
        g.EndContainer (null!);
    }
}

public class GraphicsDrawImageAttributesTests
{
    private static Majorsilence.Forms.Drawing.Bitmap MakeSource (Color color)
    {
        var bitmap = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
                bitmap.SetPixel (x, y, color);
        return bitmap;
    }

    [Fact]
    public void DrawImage_WithNullAttributes_DrawsUnchanged ()
    {
        using var source = MakeSource (Color.FromArgb (255, 200, 100, 50));
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);

        g.DrawImage (source, new Rectangle (0, 0, 10, 10), 0f, 0f, 10f, 10f,
            Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, null);

        Assert.Equal ((byte)200, target.GetPixel (5, 5).R);
        Assert.Equal ((byte)100, target.GetPixel (5, 5).G);
    }

    [Fact]
    public void DrawImage_WithColorMatrix_RemapsChannels ()
    {
        using var source = MakeSource (Color.FromArgb (255, 200, 100, 50));
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);

        // Zero out red; keep green and blue.
        var matrix = new ColorMatrix { Matrix00 = 0f };
        using var attributes = new ImageAttributes ();
        attributes.SetColorMatrix (matrix);

        g.DrawImage (source, new Rectangle (0, 0, 10, 10), 0f, 0f, 10f, 10f,
            Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, attributes);

        var pixel = target.GetPixel (5, 5);
        Assert.Equal ((byte)0, pixel.R);
        Assert.Equal ((byte)100, pixel.G);
        Assert.Equal ((byte)50, pixel.B);
    }

    [Fact]
    public void DrawImage_WithAlphaMatrix_DrawsTranslucently ()
    {
        using var source = MakeSource (Color.FromArgb (255, 0, 0, 0));
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);
        g.Clear (Color.White);

        var matrix = new ColorMatrix { Matrix33 = 0.5f };
        using var attributes = new ImageAttributes ();
        attributes.SetColorMatrix (matrix);

        g.DrawImage (source, new Rectangle (0, 0, 10, 10), 0f, 0f, 10f, 10f,
            Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, attributes);

        // Black at 50% over white is mid-gray, not black.
        var pixel = target.GetPixel (5, 5);
        Assert.InRange (pixel.R, (byte)120, (byte)136);
        Assert.Equal (pixel.R, pixel.B);
    }

    [Fact]
    public void DrawImage_WithColorKey_SkipsTheKeyedColor ()
    {
        using var source = MakeSource (Color.FromArgb (255, 255, 0, 255));
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);
        g.Clear (Color.White);

        using var attributes = new ImageAttributes ();
        attributes.SetColorKey (Color.Magenta, Color.Magenta);

        g.DrawImage (source, new Rectangle (0, 0, 10, 10), 0f, 0f, 10f, 10f,
            Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, attributes);

        var pixel = target.GetPixel (5, 5);
        Assert.Equal ((byte)255, pixel.R);
        Assert.Equal ((byte)255, pixel.G); // still white — the magenta never landed
        Assert.Equal ((byte)255, pixel.B);
    }

    [Fact]
    public void DrawImage_SourceRectangleOverload_DrawsTheRequestedRegion ()
    {
        using var source = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
                source.SetPixel (x, y, x < 5 ? Color.Red : Color.Blue);

        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);

        g.DrawImage (source, new Rectangle (0, 0, 10, 10), new Rectangle (5, 0, 5, 10),
            Majorsilence.Forms.Drawing.GraphicsUnit.Pixel, null);

        // Only the blue half of the source was requested, stretched over the whole target.
        Assert.Equal ((byte)255, target.GetPixel (2, 5).B);
        Assert.Equal ((byte)0, target.GetPixel (2, 5).R);
    }

    [Fact]
    public void DrawImage_WholeImageOverload_Works ()
    {
        using var source = MakeSource (Color.FromArgb (255, 10, 20, 30));
        using var target = new Majorsilence.Forms.Drawing.Bitmap (10, 10);
        using var g = Graphics.FromImage (target);

        using var attributes = new ImageAttributes ();
        g.DrawImage (source, new Rectangle (0, 0, 10, 10), attributes);

        Assert.Equal ((byte)10, target.GetPixel (5, 5).R);
    }
}

public class SystemBrushesPensTests
{
    [Fact]
    public void SystemBrushes_ReturnsTheSameCachedInstance ()
    {
        Assert.Same (SystemBrushes.Control, SystemBrushes.Control);
        Assert.Same (SystemBrushes.WindowText, SystemBrushes.WindowText);
    }

    [Fact]
    public void SystemPens_ReturnsTheSameCachedInstance ()
    {
        Assert.Same (SystemPens.Control, SystemPens.Control);
        Assert.Same (SystemPens.Highlight, SystemPens.Highlight);
    }

    [Fact]
    public void SystemBrushes_UseTheMatchingSystemColor ()
    {
        Assert.Equal (SystemColors.Highlight, SystemBrushes.Highlight.Color);
        Assert.Equal (SystemColors.InfoText, SystemBrushes.InfoText.Color);
        Assert.Equal (SystemColors.AppWorkspace, SystemBrushes.AppWorkspace.Color);
    }

    [Fact]
    public void SystemPens_UseTheMatchingSystemColor ()
    {
        Assert.Equal (SystemColors.ControlDarkDark, SystemPens.ControlDarkDark.Color);
        Assert.Equal (SystemColors.MenuText, SystemPens.MenuText.Color);
        Assert.Equal (1f, SystemPens.MenuText.Width);
    }

    [Fact]
    public void FromSystemColor_CachesByColor ()
    {
        var brush = SystemBrushes.FromSystemColor (SystemColors.Desktop);
        Assert.Same (brush, SystemBrushes.FromSystemColor (SystemColors.Desktop));

        var pen = SystemPens.FromSystemColor (SystemColors.Desktop);
        Assert.Same (pen, SystemPens.FromSystemColor (SystemColors.Desktop));
    }

    [Fact]
    public void EverySystemColorHasABrushAndAPen ()
    {
        var colorNames = typeof (SystemColors)
            .GetProperties (System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select (p => p.Name)
            .ToArray ();

        Assert.NotEmpty (colorNames);
        foreach (var name in colorNames) {
            Assert.NotNull (typeof (SystemBrushes).GetProperty (name));
            Assert.NotNull (typeof (SystemPens).GetProperty (name));
        }
    }
}
