using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using SkiaSharp;

namespace Majorsilence.Forms;

/// <summary>
/// Builds the Avalonia cursor for a cursor loaded from .cur/.ico data (SVC-38). The core hands the same
/// <see cref="SKBitmap"/> back every time that cursor is shown -- on every mouse move over its control --
/// so the native cursor is made once per image and cached against it.
/// </summary>
internal static class AvaloniaCustomCursor
{
    private static readonly ConditionalWeakTable<SKBitmap, Avalonia.Input.Cursor> cursors = new ();

    internal static Avalonia.Input.Cursor From (SKBitmap image, System.Drawing.Point hotSpot)
    {
        if (cursors.TryGetValue (image, out var cached))
            return cached;

        using var png = image.Encode (SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream (png.ToArray ());
        var cursor = new Avalonia.Input.Cursor (new Avalonia.Media.Imaging.Bitmap (stream), new PixelPoint (hotSpot.X, hotSpot.Y));

        cursors.AddOrUpdate (image, cursor);
        return cursor;
    }
}
