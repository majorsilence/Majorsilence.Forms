using System.Runtime.CompilerServices;
using SkiaSharp;

namespace Majorsilence.Forms.Gtk4
{
    /// <summary>
    /// Builds the GDK cursor for a cursor loaded from .cur/.ico data (SVC-38): a texture cursor with the
    /// file's hotspot, falling back to the default cursor where the display cannot show textures. The core
    /// hands the same <see cref="SKBitmap"/> back each time that cursor is shown, so the cursor is cached
    /// against it.
    /// </summary>
    internal static class Gtk4CustomCursor
    {
        private static readonly ConditionalWeakTable<SKBitmap, Gdk.Cursor> cursors = new ();

        internal static Gdk.Cursor From (SKBitmap image, System.Drawing.Point hotSpot)
        {
            if (cursors.TryGetValue (image, out var cached))
                return cached;

            using var png = image.Encode (SKEncodedImageFormat.Png, 100);
            using var bytes = GLib.Bytes.New (png.ToArray ());
            var texture = Gdk.Texture.NewFromBytes (bytes);
            var cursor = Gdk.Cursor.NewFromTexture (texture, hotSpot.X, hotSpot.Y, Gdk.Cursor.NewFromName ("default", null));

            cursors.AddOrUpdate (image, cursor);
            return cursor;
        }
    }
}
