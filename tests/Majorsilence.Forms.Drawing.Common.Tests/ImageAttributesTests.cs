using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;
// See GraphicsPathTests for why these are aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class ImageAttributesTests
{
    private static SKColor Filter(ImageAttributes attributes, SKColor source)
    {
        using var src = new SKBitmap(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
        src.SetPixel(0, 0, source);

        using var adjusted = attributes.ApplyPixelAdjustments(src);
        using var dst = new SKBitmap(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Transparent);

        using var filter = attributes.ToSKColorFilter();
        using var paint = filter is null ? null : new SKPaint { ColorFilter = filter };
        canvas.DrawBitmap(adjusted ?? src, new SKPoint(0, 0), paint);
        canvas.Flush();

        return dst.GetPixel(0, 0);
    }

    [Fact]
    public void ColorMatrix_DefaultsToIdentity()
    {
        var matrix = new ColorMatrix();

        Assert.Equal(1f, matrix.Matrix00);
        Assert.Equal(1f, matrix.Matrix11);
        Assert.Equal(1f, matrix.Matrix22);
        Assert.Equal(1f, matrix.Matrix33);
        Assert.Equal(1f, matrix.Matrix44);
        Assert.Equal(0f, matrix.Matrix01);
        Assert.Equal(0f, matrix.Matrix40);
    }

    [Fact]
    public void ColorMatrix_IndexerAndNamedPropertiesAgree()
    {
        var matrix = new ColorMatrix();

        matrix[2, 3] = 0.75f;
        Assert.Equal(0.75f, matrix.Matrix23);

        matrix.Matrix40 = -0.25f;
        Assert.Equal(-0.25f, matrix[4, 0]);
    }

    [Fact]
    public void ColorMatrix_JaggedArrayConstructor_CopiesEveryElement()
    {
        var matrix = new ColorMatrix(new[]
        {
            new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
            new[] { 1f, 0f, 0f, 0f, 0f },
            new[] { 0f, 1f, 0f, 0f, 0f },
            new[] { 0f, 0f, 1f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
        });

        Assert.Equal(0.3f, matrix[0, 2]);
        Assert.Equal(1f, matrix[4, 3]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void ColorMatrix_Indexer_RejectsOutOfRange(int row, int column)
    {
        var matrix = new ColorMatrix();
        Assert.Throws<ArgumentOutOfRangeException>(() => matrix[row, column]);
    }

    [Fact]
    public void SetColorMatrix_ChannelSwap_MovesGreenIntoRed()
    {
        // GDI+ multiplies [R G B A 1] by the matrix, so column 0 produces the output red channel.
        var matrix = new ColorMatrix();
        matrix.Matrix00 = 0f;   // red no longer contributes to red
        matrix.Matrix10 = 1f;   // green does

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);

        var result = Filter(attributes, new SKColor(128, 64, 32, 255));

        Assert.Equal((byte)64, result.Red);
        Assert.Equal((byte)64, result.Green);
        Assert.Equal((byte)32, result.Blue);
    }

    [Fact]
    public void SetColorMatrix_TranslationRow_IsInterpretedAsZeroToOne()
    {
        // Matrix40 is a +0.1 bias on red: 128 + 25.5 => 153/154, not 128 (which is what a
        // 0..255-scaled convention would produce).
        var matrix = new ColorMatrix { Matrix40 = 0.1f };

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);

        var result = Filter(attributes, new SKColor(128, 64, 32, 255));

        Assert.InRange(result.Red, (byte)152, (byte)155);
        Assert.Equal((byte)64, result.Green);
    }

    [Fact]
    public void SetColorMatrix_AlphaScale_HalvesOpacity()
    {
        var matrix = new ColorMatrix { Matrix33 = 0.5f };

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

        var result = Filter(attributes, new SKColor(200, 200, 200, 255));

        Assert.InRange(result.Alpha, (byte)126, (byte)130);
    }

    [Fact]
    public void SetColorMatrix_Grayscale_ProducesEqualChannels()
    {
        // The classic luminance grayscale matrix.
        var matrix = new ColorMatrix(new[]
        {
            new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
            new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
            new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f },
        });

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(matrix);

        var result = Filter(attributes, new SKColor(255, 0, 0, 255));

        Assert.Equal(result.Red, result.Green);
        Assert.Equal(result.Green, result.Blue);
        Assert.InRange(result.Red, (byte)74, (byte)78); // 0.299 * 255
    }

    [Fact]
    public void ClearColorMatrix_RestoresIdentity()
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix00 = 0f });
        attributes.ClearColorMatrix();

        Assert.True(attributes.IsEmpty);
        Assert.Null(attributes.ToSKColorFilter());
    }

    [Fact]
    public void SetColorKey_MakesMatchingPixelsTransparent()
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorKey(Color.FromArgb(250, 0, 250), Color.FromArgb(255, 5, 255));

        var keyed = Filter(attributes, new SKColor(255, 0, 255, 255));
        Assert.Equal((byte)0, keyed.Alpha);

        var untouched = Filter(attributes, new SKColor(10, 20, 30, 255));
        Assert.Equal((byte)255, untouched.Alpha);
        Assert.Equal((byte)10, untouched.Red);
    }

    [Fact]
    public void ClearColorKey_StopsKeying()
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorKey(Color.Magenta, Color.Magenta);
        attributes.ClearColorKey();

        var result = Filter(attributes, new SKColor(255, 0, 255, 255));
        Assert.Equal((byte)255, result.Alpha);
    }

    [Fact]
    public void SetRemapTable_ReplacesExactColors()
    {
        using var attributes = new ImageAttributes();
        attributes.SetRemapTable(new[]
        {
            new ColorMap { OldColor = Color.FromArgb(255, 10, 20, 30), NewColor = Color.FromArgb(255, 200, 100, 50) },
        });

        var remapped = Filter(attributes, new SKColor(10, 20, 30, 255));
        Assert.Equal((byte)200, remapped.Red);
        Assert.Equal((byte)100, remapped.Green);
        Assert.Equal((byte)50, remapped.Blue);

        var untouched = Filter(attributes, new SKColor(11, 20, 30, 255));
        Assert.Equal((byte)11, untouched.Red);
    }

    [Fact]
    public void SetGamma_BrightensMidtones()
    {
        using var attributes = new ImageAttributes();
        attributes.SetGamma(2.2f);

        var result = Filter(attributes, new SKColor(128, 128, 128, 255));

        // pow(128/255, 1/2.2) * 255 == ~186
        Assert.InRange(result.Red, (byte)180, (byte)192);
        Assert.Equal(result.Red, result.Blue);
    }

    [Fact]
    public void SetGamma_ThenClearGamma_LeavesPixelsAlone()
    {
        using var attributes = new ImageAttributes();
        attributes.SetGamma(2.2f);
        attributes.ClearGamma();

        Assert.Null(attributes.ToSKColorFilter());
    }

    [Fact]
    public void GammaAndColorMatrix_Compose()
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix00 = 0.5f });
        attributes.SetGamma(2.2f);

        using var filter = attributes.ToSKColorFilter();
        Assert.NotNull(filter);
    }

    [Fact]
    public void NewImageAttributes_IsEmptyAndProducesNoFilter()
    {
        using var attributes = new ImageAttributes();

        Assert.True(attributes.IsEmpty);
        Assert.False(attributes.HasPixelAdjustments);
        Assert.Null(attributes.ToSKColorFilter());
        Assert.Null(attributes.ApplyPixelAdjustments(new SKBitmap(1, 1)));
    }

    [Fact]
    public void Clone_CopiesEveryAdjustment()
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix00 = 0f, Matrix10 = 1f });
        attributes.SetGamma(1.8f);
        attributes.SetColorKey(Color.Magenta, Color.Magenta);
        attributes.SetWrapMode(Majorsilence.Forms.Drawing.Drawing2D.WrapMode.TileFlipX, Color.Red);

        var clone = (ImageAttributes)attributes.Clone();

        Assert.Equal(Majorsilence.Forms.Drawing.Drawing2D.WrapMode.TileFlipX, clone.WrapMode);
        Assert.Equal(Color.Red, clone.ClampColor);
        Assert.False(clone.IsEmpty);

        var original = Filter(attributes, new SKColor(128, 64, 32, 255));
        var copied = Filter(clone, new SKColor(128, 64, 32, 255));
        Assert.Equal(original, copied);
    }

    [Fact]
    public void SetColorMatrices_KeepsGrayMatrixAvailable()
    {
        var color = new ColorMatrix { Matrix00 = 0.5f };
        var gray = new ColorMatrix { Matrix11 = 0.25f };

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrices(color, gray, ColorMatrixFlag.AltGrays);

        Assert.Same(gray, attributes.GrayMatrix);
    }

    [Fact]
    public void SetColorMatrix_NullMatrix_Throws()
    {
        using var attributes = new ImageAttributes();
        Assert.Throws<ArgumentNullException>(() => attributes.SetColorMatrix(null!));
    }
}
