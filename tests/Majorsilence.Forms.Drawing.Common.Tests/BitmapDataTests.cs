using System.Runtime.InteropServices;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Imaging;
// See GraphicsPathTests for why these are aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class BitmapDataTests
{
    private static Bitmap MakeBitmap(int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, Color.FromArgb(255, (x * 17) % 256, (y * 31) % 256, 128));
        return bitmap;
    }

    [Fact]
    public void LockBits_ReportsRegionGeometry()
    {
        using var bitmap = MakeBitmap(10, 8);

        var data = bitmap.LockBits(new Rectangle(0, 0, 10, 8), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            Assert.Equal(10, data.Width);
            Assert.Equal(8, data.Height);
            Assert.Equal(PixelFormat.Format32bppArgb, data.PixelFormat);
            Assert.Equal(40, data.Stride); // 10 px * 4 bytes, already 4-byte aligned
            Assert.NotEqual(IntPtr.Zero, data.Scan0);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [Fact]
    public void LockBits_Format32bppArgb_LaysOutBgraBytes()
    {
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));

        var data = bitmap.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[4];
            Marshal.Copy(data.Scan0, bytes, 0, 4);

            Assert.Equal(30, bytes[0]);  // B
            Assert.Equal(20, bytes[1]);  // G
            Assert.Equal(10, bytes[2]);  // R
            Assert.Equal(255, bytes[3]); // A
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [Fact]
    public void LockBits_Format24bppRgb_PadsStrideToFourBytes()
    {
        using var bitmap = MakeBitmap(3, 2);

        var data = bitmap.LockBits(new Rectangle(0, 0, 3, 2), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            Assert.Equal(PixelFormat.Format24bppRgb, data.PixelFormat);
            Assert.Equal(12, data.Stride); // 3 px * 3 bytes = 9, rounded up to 12
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [Fact]
    public void LockBits_NarrowFormat_WidensToThirtyTwoBppArgb()
    {
        using var bitmap = MakeBitmap(4, 4);

        var data = bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.ReadOnly, PixelFormat.Format16bppRgb565);
        try
        {
            Assert.Equal(PixelFormat.Format32bppArgb, data.PixelFormat);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [Fact]
    public void LockBits_RoundTripsPixelsUnchanged()
    {
        using var bitmap = MakeBitmap(8, 6);
        var before = bitmap.GetPixel(5, 3);

        var data = bitmap.LockBits(new Rectangle(0, 0, 8, 6), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        bitmap.UnlockBits(data);

        Assert.Equal(before, bitmap.GetPixel(5, 3));
    }

    [Fact]
    public void UnlockBits_WriteMode_WritesBufferBackIntoTheBitmap()
    {
        using var bitmap = new Bitmap(4, 4);

        var data = bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buffer = new byte[data.Stride * data.Height];
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 30;      // B
            buffer[i + 1] = 20;  // G
            buffer[i + 2] = 10;  // R
            buffer[i + 3] = 255; // A
        }
        Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        bitmap.UnlockBits(data);

        Assert.Equal(Color.FromArgb(255, 10, 20, 30), bitmap.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 10, 20, 30), bitmap.GetPixel(3, 3));
    }

    [Fact]
    public void UnlockBits_ReadOnly_DiscardsEdits()
    {
        using var bitmap = new Bitmap(2, 2);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 1, 2, 3));

        var data = bitmap.LockBits(new Rectangle(0, 0, 2, 2), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        Marshal.WriteByte(data.Scan0, 0, 99);
        bitmap.UnlockBits(data);

        Assert.Equal(Color.FromArgb(255, 1, 2, 3), bitmap.GetPixel(0, 0));
    }

    [Fact]
    public void LockBits_SubRectangle_MapsOnlyThatRegion()
    {
        using var bitmap = new Bitmap(4, 4);

        var data = bitmap.LockBits(new Rectangle(1, 1, 2, 2), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buffer = new byte[data.Stride * data.Height];
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 255;     // B
            buffer[i + 3] = 255; // A
        }
        Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        bitmap.UnlockBits(data);

        Assert.Equal(Color.FromArgb(255, 0, 0, 255), bitmap.GetPixel(1, 1));
        Assert.Equal(Color.FromArgb(255, 0, 0, 255), bitmap.GetPixel(2, 2));
        Assert.Equal(0, bitmap.GetPixel(0, 0).A); // outside the locked region
        Assert.Equal(0, bitmap.GetPixel(3, 3).A);
    }

    [Fact]
    public void LockBits_PremultipliedFormat_RoundTripsTranslucentPixels()
    {
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetPixel(0, 0, Color.FromArgb(128, 200, 100, 50));

        var data = bitmap.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            var bytes = new byte[4];
            Marshal.Copy(data.Scan0, bytes, 0, 4);

            Assert.Equal(128, bytes[3]);
            Assert.InRange(bytes[2], (byte)98, (byte)102);  // 200 * 128/255
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var after = bitmap.GetPixel(0, 0);
        Assert.Equal(128, after.A);
        Assert.InRange(after.R, 195, 205);
    }

    [Fact]
    public void LockBits_Twice_Throws()
    {
        using var bitmap = MakeBitmap(4, 4);
        var data = bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb));
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [Fact]
    public void LockBits_ThenUnlock_AllowsLockingAgain()
    {
        using var bitmap = MakeBitmap(4, 4);

        var first = bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        bitmap.UnlockBits(first);

        var second = bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        bitmap.UnlockBits(second);
    }

    [Fact]
    public void LockBits_RectangleOutsideBitmap_Throws()
    {
        using var bitmap = MakeBitmap(4, 4);
        Assert.Throws<ArgumentException>(
            () => bitmap.LockBits(new Rectangle(10, 10, 4, 4), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb));
    }

    [Fact]
    public void UnlockBits_ForeignBitmapData_Throws()
    {
        using var bitmap = MakeBitmap(4, 4);
        Assert.Throws<ArgumentException>(() => bitmap.UnlockBits(new BitmapData()));
    }

    [Fact]
    public void Dispose_WithOutstandingLock_DoesNotThrow()
    {
        var bitmap = MakeBitmap(4, 4);
        bitmap.LockBits(new Rectangle(0, 0, 4, 4), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        bitmap.Dispose();
    }
}
