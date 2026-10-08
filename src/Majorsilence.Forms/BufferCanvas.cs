using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The canvas a control or hosted form paints its back buffer through.
    /// </summary>
    /// <remarks>
    /// Text is drawn with subpixel edging, but Skia only renders subpixel (LCD) text into a surface that
    /// declares its pixel geometry: a canvas over a bare bitmap has none, so every glyph fell back to
    /// grey antialiasing and read lighter and softer than upstream's ClearType -- ReportDesigner's menu
    /// "File" had a third of WinForms' solid stem pixels. On Windows, once the app has chosen a font
    /// (Application.SetDefaultFont, as a ported WinForms app does), buffers declare the usual RGB
    /// horizontal stripe so text matches. Apps that have not keep the theme's look.
    /// </remarks>
    internal readonly struct BufferCanvas : System.IDisposable
    {
        private readonly SKSurface? surface;
        private readonly SKCanvas? owned;

        private BufferCanvas (SKSurface? surface, SKCanvas? owned)
        {
            this.surface = surface;
            this.owned = owned;
        }

        /// <summary>Whether back buffers get subpixel text.</summary>
        internal static bool UsesSubpixelText => OperatingSystemCompat.IsWindows () && SystemFonts.HasDefaultFontOverride;

        /// <summary>Opens a canvas over <paramref name="bitmap"/>'s pixels.</summary>
        internal static BufferCanvas Open (SKBitmap bitmap)
        {
            if (UsesSubpixelText) {
                var surface = SKSurface.Create (bitmap.Info, bitmap.GetPixels (), bitmap.RowBytes,
                    new SKSurfaceProperties (SKPixelGeometry.RgbHorizontal));

                if (surface is not null)
                    return new BufferCanvas (surface, null);
            }

            return new BufferCanvas (null, new SKCanvas (bitmap));
        }

        /// <summary>The canvas to draw with.</summary>
        internal SKCanvas Canvas => surface?.Canvas ?? owned!;

        /// <inheritdoc/>
        public void Dispose ()
        {
            surface?.Dispose ();
            owned?.Dispose ();
        }
    }
}
