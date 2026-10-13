using System;
using System.IO;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing.Common.Tests
{
    // Image.Save encodes straight from the bitmap's pixels instead of copying them into an SKImage first (performance
    // plan F8). The bytes written must not change: this compares them with the old route, SKImage.FromBitmap then
    // Encode, falling back to PNG for a container Skia cannot write.
    public class ImageSaveParityTests
    {
        public static TheoryData<PixelFormat, string> Cases ()
        {
            var data = new TheoryData<PixelFormat, string> ();
            foreach (var pixelFormat in new[] { PixelFormat.Format32bppArgb, PixelFormat.Format32bppPArgb, PixelFormat.Format32bppRgb, PixelFormat.Format24bppRgb, PixelFormat.Format16bppRgb565 })
                foreach (var format in new[] { "Png", "Jpeg", "Webp", "Bmp", "Gif", "Tiff" })
                    data.Add (pixelFormat, format);
            return data;
        }

        [Theory]
        [MemberData (nameof (Cases))]
        public void Save_writes_the_bytes_the_snapshot_route_wrote (PixelFormat pixelFormat, string formatName)
        {
            var format = (ImageFormat)typeof (ImageFormat).GetProperty (formatName)!.GetValue (null)!;
            using var bitmap = new Bitmap (97, 61, pixelFormat);
            // Graphics lives in the Forms assembly; drawing on the backing bitmap directly gives the same pixels to encode.
            using (var canvas = new SKCanvas (bitmap.GetSKBitmap ()!)) {
                canvas.Clear (new SKColor (10, 200, 30, 120));
                using var paint = new SKPaint { Color = new SKColor (250, 20, 90, 200), IsAntialias = true };
                canvas.DrawOval (new SKRect (5, 7, 65, 47), paint);
                using var line = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, IsAntialias = true };
                canvas.DrawLine (0, 0, 96, 60, line);
            }

            foreach (var quality in new[] { 100, 75, 10 }) {
                using var actual = new MemoryStream ();
                bitmap.Save (actual, new ImageCodecInfo { Format = format }, Quality (quality));

                using var image = SKImage.FromBitmap (bitmap.GetSKBitmap ()!);
                using var expected = image.Encode (format.ToSKEncodedImageFormat (), quality) ?? image.Encode (SKEncodedImageFormat.Png, quality);

                Assert.True (expected.ToArray ().AsSpan ().SequenceEqual (actual.ToArray ()), $"{pixelFormat} {formatName} quality {quality}");
            }
        }

        private static EncoderParameters Quality (long quality)
        {
            var parameters = new EncoderParameters (1);
            parameters.Param[0] = new EncoderParameter (Encoder.Quality, quality);
            return parameters;
        }
    }
}
