using Majorsilence.Forms.Drawing;
// See GraphicsPathTests for why these are aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;

namespace Majorsilence.Forms.Drawing.Common.Tests;

// Image.MakeTransparent used to be a no-op stub, so the colour-key idiom that sprite sheets predating
// per-pixel alpha rely on silently did nothing -- the key colour stayed opaque and drew as a solid
// block behind the sprite.
public class MakeTransparentTests
{
    [Fact]
    public void MakeTransparent_WithColor_ClearsMatchingPixelsOnly()
    {
        using var bitmap = new Bitmap(2, 1);
        bitmap.SetPixel(0, 0, Color.White);
        bitmap.SetPixel(1, 0, Color.FromArgb(255, 200, 40, 40));

        bitmap.MakeTransparent(Color.White);

        Assert.Equal(0, bitmap.GetPixel(0, 0).A);

        var kept = bitmap.GetPixel(1, 0);
        Assert.Equal(255, kept.A);
        Assert.Equal(200, kept.R);
        Assert.Equal(40, kept.G);
        Assert.Equal(40, kept.B);
    }

    [Fact]
    public void MakeTransparent_WithColor_LeavesNearMissesOpaque()
    {
        // An off-by-one channel must not be keyed out; the match is exact, not nearest.
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 254, 255, 255));

        bitmap.MakeTransparent(Color.White);

        Assert.Equal(255, bitmap.GetPixel(0, 0).A);
    }

    [Fact]
    public void MakeTransparent_Parameterless_KeysOffBottomLeftPixel()
    {
        // GDI+ reads the key from the bottom-left pixel, not the top-left.
        using var bitmap = new Bitmap(2, 2);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));
        bitmap.SetPixel(1, 0, Color.FromArgb(255, 10, 20, 30));
        bitmap.SetPixel(0, 1, Color.Magenta);
        bitmap.SetPixel(1, 1, Color.Magenta);

        bitmap.MakeTransparent();

        Assert.Equal(0, bitmap.GetPixel(0, 1).A);
        Assert.Equal(0, bitmap.GetPixel(1, 1).A);
        Assert.Equal(255, bitmap.GetPixel(0, 0).A);
    }

    [Fact]
    public void MakeTransparent_Parameterless_DoesNothingWhenKeyPixelIsNotOpaque()
    {
        // GDI+ bails out rather than keying off a translucent pixel.
        using var bitmap = new Bitmap(1, 2);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));
        bitmap.SetPixel(0, 1, Color.FromArgb(128, 10, 20, 30));

        bitmap.MakeTransparent();

        Assert.Equal(255, bitmap.GetPixel(0, 0).A);
        Assert.Equal(128, bitmap.GetPixel(0, 1).A);
    }
}
