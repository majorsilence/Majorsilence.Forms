using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Colour-key transparency (W6 mechanisms): <see cref="ImageList.TransparentColor"/> and
    /// <see cref="ToolStripItem.ImageTransparentColor"/> name a colour that reads as transparent. The
    /// keyed copy is built once per source bitmap and colour and kept beside the source.
    /// </summary>
    internal static class ColorKeyedBitmaps
    {
        private static readonly ConditionalWeakTable<SKBitmap, Entry> cache = new ();

        private sealed class Entry
        {
            internal SKColor Key;
            internal SKBitmap? Keyed;
        }

        /// <summary>The bitmap with every pixel of <paramref name="key"/> made transparent; the source itself when the key is transparent.</summary>
        internal static SKBitmap Apply (SKBitmap source, SKColor key)
        {
            if (key.Alpha == 0)
                return source;

            var entry = cache.GetValue (source, _ => new Entry ());

            if (entry.Keyed is { } keyed && entry.Key == key)
                return keyed;

            entry.Keyed?.Dispose ();
            entry.Key = key;
            entry.Keyed = Build (source, key);
            return entry.Keyed;
        }

        private static SKBitmap Build (SKBitmap source, SKColor key)
        {
            var result = new SKBitmap (source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

            for (var y = 0; y < source.Height; y++)
                for (var x = 0; x < source.Width; x++) {
                    var pixel = source.GetPixel (x, y);
                    result.SetPixel (x, y, pixel.Red == key.Red && pixel.Green == key.Green && pixel.Blue == key.Blue ? SKColors.Transparent : pixel);
                }

            return result;
        }
    }
}
