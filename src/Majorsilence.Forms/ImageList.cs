using System;
using System.Collections.Generic;
using System.ComponentModel;
using SkiaSharp;

namespace Majorsilence.Forms;

/// <summary>
/// Represents a collection of images that can be used by controls.
/// </summary>
public partial class ImageList : Component
{
    // WinForms default ImageList.ImageSize is 16x16.
    private static readonly SKSize s_defaultImageSize = new (16, 16);

    /// <summary>
    /// Initializes a new instance of the ImageList class.
    /// </summary>
    public ImageList ()
    {
        Images = new (s_defaultImageSize) { Transform = ApplyTransparentColor };
    }

    /// <summary>
    /// Initializes a new instance of the ImageList class and adds it to the specified container.
    /// </summary>
    public ImageList (System.ComponentModel.IContainer container)
    {
        Images = new (s_defaultImageSize) { Transform = ApplyTransparentColor };
        container.Add (this);
    }

    // The colour key is applied as an image is added, which is when upstream applies it too: a
    // change to TransparentColor affects images added afterwards.
    private SKBitmap ApplyTransparentColor (SKBitmap bitmap)
    {
        var key = TransparentColor;

        if (key.IsEmpty || key.A == 0)
            return bitmap;

        var keyed = ColorKeyedBitmaps.Apply (bitmap, key.ToSKColor ());

        // Apply returns a cached copy tied to the source; the collection owns what it stores.
        return ReferenceEquals (keyed, bitmap) ? bitmap : keyed.Copy ();
    }

    /// <summary>
    /// Gets the collection of images in the ImageList.
    /// </summary>
    public ImageCollection Images { get; }

    // Upstream ImageList.s_maxImageWidth / s_maxImageHeight.
    private const int MaxImageDimension = 256;

    /// <summary>
    /// Gets or sets the size of the images in the ImageList. Changing it once images are present
    /// resizes every image to the new size, as upstream re-renders them.
    /// </summary>
    /// <exception cref="ArgumentException">The value is <see cref="System.Drawing.Size.Empty"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside 1..256.</exception>
    public System.Drawing.Size ImageSize {
        get { var s = Images.ImageSize; return new System.Drawing.Size ((int)s.Width, (int)s.Height); }
        set {
            // Upstream's validation and exception types (Controls/ImageList/ImageList.cs, ImageSize).
            if (value.IsEmpty)
                throw new ArgumentException ("Value of 'Size.Empty' is not valid for 'ImageSize'.", nameof (value));

            if (value.Width <= 0 || value.Width > MaxImageDimension)
                throw new ArgumentOutOfRangeException (nameof (value), value, $"Value of '{value.Width}' is not valid for 'ImageSize.Width'. 'ImageSize.Width' must be between 1 and {MaxImageDimension}.");

            if (value.Height <= 0 || value.Height > MaxImageDimension)
                throw new ArgumentOutOfRangeException (nameof (value), value, $"Value of '{value.Height}' is not valid for 'ImageSize.Height'. 'ImageSize.Height' must be between 1 and {MaxImageDimension}.");

            Images.SetImageSize (new SKSize (value.Width, value.Height));
        }
    }

    /// <summary>Gets or sets the color depth used by the image list.</summary>
    /// <remarks>Stored, not applied, deliberately: every image is held as a 32-bit Skia bitmap, and
    /// reducing the palette to emulate a comctl32 image list's lower depths would only lose colour.</remarks>
    public ColorDepth ColorDepth { get; set; } = ColorDepth.Depth32Bit;

    /// <summary>Gets or sets the color to treat as transparent.</summary>
    /// <remarks>Real as of W6 mechanisms: every pixel of this colour in an image added afterwards is
    /// made transparent, as upstream keys images on add. The default, <c>Transparent</c>, keys nothing.</remarks>
    public System.Drawing.Color TransparentColor { get; set; } = System.Drawing.Color.Transparent;

    /// <summary>
    /// Gets or sets the image stream used to (de)serialize the image list (e.g. from a .resx resource).
    /// When the streamer carries frames decoded from a WinForms <c>ImageStream</c> resource (see
    /// <see cref="ComponentResourceManager"/>), assigning it populates <see cref="Images"/>.
    /// </summary>
    public ImageListStreamer? ImageStream {
        get => _imageStream;
        set {
            _imageStream = value;
            ApplyStreamer (value);
        }
    }

    private ImageListStreamer? _imageStream;

    private void ApplyStreamer (ImageListStreamer? streamer)
    {
        if (streamer is null || streamer.Frames.Count == 0)
            return;

        Images.Clear ();

        // Adopt the streamer's native frame size so frames aren't resized to the 16x16 default.
        if (streamer.FrameSize is { Width: > 0, Height: > 0 } size)
            Images.SetImageSize (new SKSize (size.Width, size.Height));

        foreach (var frame in streamer.Frames)
            Images.Add (frame);
    }

    /// <summary>Draws the image at the specified index at the given point.</summary>
    public void Draw (Graphics g, System.Drawing.Point pt, int index) => Draw (g, pt.X, pt.Y, index);

    /// <summary>Draws the image at the specified index at the given coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an image's index.</exception>
    public void Draw (Graphics g, int x, int y, int index)
    {
        ThrowIfBadIndex (index);
        g.DrawImage (Images.GetBitmap (index), x, y);
    }

    /// <summary>Draws the image at the specified index scaled to the given size.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an image's index.</exception>
    public void Draw (Graphics g, int x, int y, int width, int height, int index)
    {
        ThrowIfBadIndex (index);
        g.DrawImage (Images.GetBitmap (index), new System.Drawing.Rectangle (x, y, width, height));
    }

    // Upstream ImageList.Draw throws for a bad index (Controls/ImageList/ImageList.cs). Painting nothing
    // instead hid a stale index, which is a bug in the caller (SMP-56).
    private void ThrowIfBadIndex (int index)
    {
        if (index < 0 || index >= Images.Count)
            throw new ArgumentOutOfRangeException (nameof (index), index, $"Index '{index}' is out of range for an image list of {Images.Count} images.");
    }

    /// <inheritdoc/>
    protected override void Dispose (bool disposing)
    {
        if (disposing)
            Images.Dispose ();

        base.Dispose (disposing);
    }
}

/// <summary>
/// WinForms compatibility: represents the serialized image-stream of an <see cref="ImageList"/>,
/// as produced by designer-generated <c>resources.GetObject("imageList.ImageStream")</c> calls.
/// <see cref="ComponentResourceManager"/> decodes the WinForms <c>ImageStream</c> resource into the
/// <see cref="Frames"/> here, which <see cref="ImageList.ImageStream"/> then adopts.
/// </summary>
public sealed partial class ImageListStreamer
{
    /// <summary>Initializes a new, empty instance of the ImageListStreamer class.</summary>
    public ImageListStreamer () => Frames = Array.Empty<SKBitmap> ();

    /// <summary>Initializes a streamer carrying frames decoded from a WinForms ImageStream resource.</summary>
    internal ImageListStreamer (IReadOnlyList<SKBitmap> frames, System.Drawing.Size frameSize)
    {
        Frames = frames;
        FrameSize = frameSize;
    }

    /// <summary>The decoded frames (empty when the streamer was constructed without a resource).</summary>
    internal IReadOnlyList<SKBitmap> Frames { get; }

    /// <summary>The native frame size of the image list, or empty when unknown.</summary>
    internal System.Drawing.Size FrameSize { get; }
}
