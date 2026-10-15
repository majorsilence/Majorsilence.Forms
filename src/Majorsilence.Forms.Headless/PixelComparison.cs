using System;
using SkiaSharp;

namespace Majorsilence.Forms.Headless
{
    /// <summary>
    /// Compares two renders pixel by pixel, with a per-channel tolerance for anti-aliasing noise: the check
    /// behind a golden-image test, independent of any test framework.
    /// </summary>
    /// <example>
    /// <code>
    /// var result = PixelComparison.Compare (File.ReadAllBytes (baselinePath), HeadlessRenderer.CapturePng (form, 800, 600));
    /// if (!result.IsWithin (0.0001)) {
    ///     File.WriteAllBytes ("diff.png", result.CreateDiffPng ());
    ///     Assert.Fail (result.ToString ());
    /// }
    /// </code>
    /// </example>
    public sealed class PixelComparison
    {
        // RGBA, unpremultiplied, row by row; null when the sizes differ.
        private readonly byte[]? expected, actual;
        private readonly int tolerance;

        private PixelComparison (SKBitmap expected, SKBitmap actual, int tolerance)
        {
            ExpectedSize = new System.Drawing.Size (expected.Width, expected.Height);
            ActualSize = new System.Drawing.Size (actual.Width, actual.Height);
            this.tolerance = tolerance;

            if (ExpectedSize != ActualSize)
                return;

            this.expected = expected.GetPixelSpan ().ToArray ();
            this.actual = actual.GetPixelSpan ().ToArray ();
            DifferingPixels = Count ();
        }

        /// <summary>The expected image's size.</summary>
        public System.Drawing.Size ExpectedSize { get; }

        /// <summary>The actual image's size.</summary>
        public System.Drawing.Size ActualSize { get; }

        /// <summary>Whether the two images are the same size; when they are not, no pixels were compared.</summary>
        public bool SizesMatch => ExpectedSize == ActualSize;

        /// <summary>The pixels with a channel (red, green, blue or alpha) further apart than the tolerance.</summary>
        public int DifferingPixels { get; }

        /// <summary>The pixels compared: every pixel of the image when the sizes match, otherwise none.</summary>
        public int TotalPixels => SizesMatch ? ExpectedSize.Width * ExpectedSize.Height : 0;

        /// <summary>
        /// Compares two PNG (or any format SkiaSharp decodes) images.
        /// </summary>
        /// <param name="expectedPng">The baseline.</param>
        /// <param name="actualPng">The render under test, typically from <see cref="HeadlessRenderer.CapturePng"/>.</param>
        /// <param name="channelTolerance">How far apart (0-255) a channel may be and still count as the same.
        /// The default, 8, absorbs anti-aliasing differences between runs and Skia builds without hiding a
        /// change of colour.</param>
        public static PixelComparison Compare (byte[] expectedPng, byte[] actualPng, int channelTolerance = 8)
        {
            ArgumentNullException.ThrowIfNull (expectedPng);
            ArgumentNullException.ThrowIfNull (actualPng);
            if (channelTolerance is < 0 or > 255) throw new ArgumentOutOfRangeException (nameof (channelTolerance));

            using var expected = Decode (expectedPng, nameof (expectedPng));
            using var actual = Decode (actualPng, nameof (actualPng));
            return new PixelComparison (expected, actual, channelTolerance);
        }

        /// <summary>
        /// Whether the sizes match and no more than <paramref name="maxDifferingFraction"/> of the pixels differ.
        /// </summary>
        /// <param name="maxDifferingFraction">The share of pixels allowed to differ, 0 to 1. Keep it small: a
        /// one-character change to a form's title alters a few hundred pixels of a 1000x700 render.</param>
        public bool IsWithin (double maxDifferingFraction)
            => SizesMatch && DifferingPixels <= (long) Math.Floor (TotalPixels * maxDifferingFraction);

        /// <summary>
        /// A PNG of the actual image, dimmed, with each differing pixel in solid red: what to attach to a
        /// failed test. Null when the sizes differ.
        /// </summary>
        public byte[]? CreateDiffPng ()
        {
            if (expected is null || actual is null)
                return null;

            using var diff = new SKBitmap (new SKImageInfo (ActualSize.Width, ActualSize.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            ReadOnlySpan<byte> e = expected, a = actual;
            var d = diff.GetPixelSpan ();

            for (var i = 0; i < a.Length; i += 4) {
                if (Differs (e, a, i)) {
                    d[i] = 255; d[i + 1] = 0; d[i + 2] = 0; d[i + 3] = 255;
                } else {
                    d[i] = (byte) (a[i] / 4 + 191); d[i + 1] = (byte) (a[i + 1] / 4 + 191); d[i + 2] = (byte) (a[i + 2] / 4 + 191); d[i + 3] = 255;
                }
            }

            using var image = SKImage.FromBitmap (diff);
            using var data = image.Encode (SKEncodedImageFormat.Png, 100);
            return data.ToArray ();
        }

        /// <inheritdoc/>
        public override string ToString ()
            => SizesMatch
                ? $"{DifferingPixels} of {TotalPixels} pixels differ by more than {tolerance} in a channel"
                : $"sizes differ: expected {ExpectedSize.Width}x{ExpectedSize.Height}, actual {ActualSize.Width}x{ActualSize.Height}";

        private int Count ()
        {
            ReadOnlySpan<byte> e = expected, a = actual;
            var count = 0;
            for (var i = 0; i < a.Length; i += 4)
                if (Differs (e, a, i))
                    count++;
            return count;
        }

        private bool Differs (ReadOnlySpan<byte> e, ReadOnlySpan<byte> a, int i)
            => Math.Abs (e[i] - a[i]) > tolerance || Math.Abs (e[i + 1] - a[i + 1]) > tolerance
            || Math.Abs (e[i + 2] - a[i + 2]) > tolerance || Math.Abs (e[i + 3] - a[i + 3]) > tolerance;

        // One pixel layout for both, whatever each file was encoded as.
        private static SKBitmap Decode (byte[] png, string name)
        {
            using var decoded = SKBitmap.Decode (png) ?? throw new ArgumentException ("Not an image SkiaSharp can decode.", name);
            var bitmap = new SKBitmap (new SKImageInfo (decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            if (!decoded.CopyTo (bitmap, SKColorType.Rgba8888))
                using (var canvas = new SKCanvas (bitmap))
                    canvas.DrawBitmap (decoded, 0, 0);
            return bitmap;
        }
    }
}
