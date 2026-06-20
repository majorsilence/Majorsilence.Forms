using System;
using SkiaSharp;

namespace Continuum.Drawing.Imaging
{
    /// <summary>
    /// Specifies the file format of an image. Cross-platform replacement for
    /// <c>System.Drawing.Imaging.ImageFormat</c>.
    /// </summary>
    public sealed class ImageFormat
    {
        internal ImageFormat (string name, SKEncodedImageFormat skFormat)
        {
            Name = name;
            SKFormat = skFormat;
        }

        /// <summary>Gets the name of this image format.</summary>
        public string Name { get; }

        internal SKEncodedImageFormat SKFormat { get; }

        /// <summary>Gets the bitmap (BMP) image format.</summary>
        public static ImageFormat Bmp { get; } = new ImageFormat ("Bmp", SKEncodedImageFormat.Bmp);

        /// <summary>Gets the PNG image format.</summary>
        public static ImageFormat Png { get; } = new ImageFormat ("Png", SKEncodedImageFormat.Png);

        /// <summary>Gets the JPEG image format.</summary>
        public static ImageFormat Jpeg { get; } = new ImageFormat ("Jpeg", SKEncodedImageFormat.Jpeg);

        /// <summary>Gets the GIF image format.</summary>
        public static ImageFormat Gif { get; } = new ImageFormat ("Gif", SKEncodedImageFormat.Gif);

        /// <summary>Gets the W3C PNG image format (alias of <see cref="Png"/>).</summary>
        public static ImageFormat MemoryBmp { get; } = new ImageFormat ("MemoryBmp", SKEncodedImageFormat.Bmp);

        /// <summary>Gets the Windows icon image format (encoded as PNG).</summary>
        public static ImageFormat Icon { get; } = new ImageFormat ("Icon", SKEncodedImageFormat.Ico);

        /// <summary>Gets the TIFF image format.</summary>
        public static ImageFormat Tiff { get; } = new ImageFormat ("Tiff", SKEncodedImageFormat.Png);

        /// <summary>Gets the WMF image format (encoded as PNG).</summary>
        public static ImageFormat Wmf { get; } = new ImageFormat ("Wmf", SKEncodedImageFormat.Png);

        /// <summary>Gets the EMF image format (encoded as PNG).</summary>
        public static ImageFormat Emf { get; } = new ImageFormat ("Emf", SKEncodedImageFormat.Png);

        /// <summary>Gets the EXIF image format (encoded as JPEG).</summary>
        public static ImageFormat Exif { get; } = new ImageFormat ("Exif", SKEncodedImageFormat.Jpeg);

        internal SKEncodedImageFormat ToSKEncodedImageFormat () => SKFormat;

        internal static ImageFormat FromFileName (string filename)
        {
            var ext = System.IO.Path.GetExtension (filename)?.ToLowerInvariant ();
            return ext switch {
                ".jpg" or ".jpeg" => Jpeg,
                ".gif" => Gif,
                ".bmp" => Bmp,
                ".ico" => Icon,
                ".tif" or ".tiff" => Tiff,
                _ => Png
            };
        }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>
    /// Specifies the pixel format of an image. Continuum.Drawing always stores 32bpp ARGB; the other
    /// members are provided for source compatibility.
    /// </summary>
    public enum PixelFormat
    {
        /// <summary>The pixel format is undefined.</summary>
        Undefined = 0,
        /// <summary>16 bits per pixel, 555 RGB.</summary>
        Format16bppRgb555 = 135173,
        /// <summary>16 bits per pixel, 565 RGB.</summary>
        Format16bppRgb565 = 135174,
        /// <summary>24 bits per pixel, RGB.</summary>
        Format24bppRgb = 137224,
        /// <summary>32 bits per pixel, RGB.</summary>
        Format32bppRgb = 139273,
        /// <summary>32 bits per pixel, ARGB.</summary>
        Format32bppArgb = 2498570,
        /// <summary>32 bits per pixel, premultiplied ARGB.</summary>
        Format32bppPArgb = 925707,
        /// <summary>8 bits per pixel, indexed.</summary>
        Format8bppIndexed = 198659
    }

    /// <summary>Specifies the access mode used when locking bitmap bits. Stub in Continuum.Drawing.</summary>
    public enum ImageLockMode
    {
        /// <summary>Read-only access.</summary>
        ReadOnly = 1,
        /// <summary>Write-only access.</summary>
        WriteOnly = 2,
        /// <summary>Read and write access.</summary>
        ReadWrite = 3,
        /// <summary>The buffer is user-allocated.</summary>
        UserInputBuffer = 4
    }
}
