using System;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // BitmapCopy replaces SKBitmap.Copy wherever an image is copied (performance plan F19): Image.Clone, the
    // Bitmap (Image) constructor, Icon.ToBitmap/Clone, ImageList reads, DrawImage. The copy must be the source's
    // bytes in the same SKImageInfo, and independent of it.
    public class BitmapCopyTests
    {
        [Theory]
        [InlineData (SKColorType.Bgra8888, SKAlphaType.Premul)]
        [InlineData (SKColorType.Bgra8888, SKAlphaType.Unpremul)]
        [InlineData (SKColorType.Rgba8888, SKAlphaType.Unpremul)]
        [InlineData (SKColorType.Rgba8888, SKAlphaType.Opaque)]
        [InlineData (SKColorType.Rgb565, SKAlphaType.Opaque)]
        [InlineData (SKColorType.Gray8, SKAlphaType.Opaque)]
        [InlineData (SKColorType.Alpha8, SKAlphaType.Premul)]
        [InlineData (SKColorType.RgbaF16, SKAlphaType.Premul)]
        public void A_copy_is_the_source_s_bytes_in_the_same_format (SKColorType colorType, SKAlphaType alphaType)
        {
            using var source = new SKBitmap ();
            Assert.True (source.TryAllocPixels (new SKImageInfo (37, 23, colorType, alphaType)));
            var bytes = new byte[source.ByteCount];
            new Random (1).NextBytes (bytes);
            Marshal.Copy (bytes, 0, source.GetPixels (), bytes.Length);

            using var copy = BitmapCopy.Of (source)!;

            Assert.Equal (source.Info, copy.Info);
            Assert.True (source.Bytes.AsSpan ().SequenceEqual (copy.Bytes));
            Assert.NotEqual (source.GetPixels (), copy.GetPixels ());
            Assert.False (copy.IsImmutable);
        }

        [Fact]
        public void Changing_the_copy_leaves_the_source_alone ()
        {
            using var source = new SKBitmap (8, 8);
            source.Erase (SKColors.Coral);

            using var copy = BitmapCopy.Of (source)!;
            copy.Erase (SKColors.Navy);

            Assert.Equal (SKColors.Coral, source.GetPixel (3, 3));
        }

        [Fact]
        public void No_bitmap_copies_to_nothing () => Assert.Null (BitmapCopy.Of (null));
    }
}
