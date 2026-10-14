using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Copies an <c>SKBitmap</c> by reading its pixels into a new bitmap of the same <c>SKImageInfo</c>.
    /// </summary>
    /// <remarks>
    /// <c>SKBitmap.Copy</c> is slow and not exact. It took 18.8 ms for a 1600x1200 bitmap where reading the pixels
    /// takes 0.57 ms, and 8.2 us for a 16x16 icon against 1.9 us; every DrawImage, Image.Clone, Icon.ToBitmap and
    /// ImageList read made one (performance plan F19). It also converts through premultiplied colour, so an
    /// unpremultiplied pixel with no alpha lost its colour, and F16 pixels changed; a GDI+ clone keeps them.
    /// The copy here is the source's bytes, row for row, in the same colour type, alpha type and colour space
    /// (BitmapCopyTests). A bitmap with no readable pixels still goes through <c>SKBitmap.Copy</c>.
    /// </remarks>
    internal static class BitmapCopy
    {
        internal static SKBitmap? Of (SKBitmap? source)
        {
            if (source is null)
                return null;

            using (var pixels = source.PeekPixels ()) {
                if (pixels is not null) {
                    var copy = new SKBitmap ();
                    if (copy.TryAllocPixels (source.Info) && pixels.ReadPixels (copy.Info, copy.GetPixels (), copy.RowBytes))
                        return copy;
                    copy.Dispose ();
                }
            }

            return source.Copy ();
        }
    }
}
