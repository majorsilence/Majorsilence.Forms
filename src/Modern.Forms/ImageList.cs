using System.ComponentModel;
using SkiaSharp;

namespace Modern.Forms;

/// <summary>
/// Represents a collection of images that can be used by controls.
/// </summary>
public class ImageList : Component
{
    private static readonly SKSize s_defaultImageSize = new (32, 32);

    /// <summary>
    /// Initializes a new instance of the ImageList class.
    /// </summary>
    public ImageList ()
    {
        Images = new (s_defaultImageSize);
    }

    /// <summary>
    /// Initializes a new instance of the ImageList class and adds it to the specified container.
    /// </summary>
    public ImageList (System.ComponentModel.IContainer container)
    {
        Images = new (s_defaultImageSize);
        container.Add (this);
    }

    /// <summary>
    /// Gets the collection of images in the ImageList.
    /// </summary>
    public ImageCollection Images { get; }

    /// <summary>
    /// Gets or sets the size of the images in the ImageList. Note this cannot be set once images have been added.
    /// </summary>
    public System.Drawing.Size ImageSize {
        get { var s = Images.ImageSize; return new System.Drawing.Size ((int)s.Width, (int)s.Height); }
        set => Images.SetImageSize (new SKSize (value.Width, value.Height));
    }

    /// <summary>Gets or sets the color depth used by the image list. Stored but not enforced in Modern.Forms.</summary>
    public ColorDepth ColorDepth { get; set; } = ColorDepth.Depth32Bit;

    /// <summary>Draws the image at the specified index at the given point.</summary>
    public void Draw (Graphics g, System.Drawing.Point pt, int index) => Draw (g, pt.X, pt.Y, index);

    /// <summary>Draws the image at the specified index at the given coordinates.</summary>
    public void Draw (Graphics g, int x, int y, int index)
    {
        if (index >= 0 && index < Images.Count)
            g.DrawImage (Images[index], x, y);
    }

    /// <summary>Draws the image at the specified index scaled to the given size.</summary>
    public void Draw (Graphics g, int x, int y, int width, int height, int index)
    {
        if (index >= 0 && index < Images.Count)
            g.DrawImage (Images[index], new System.Drawing.Rectangle (x, y, width, height));
    }

    /// <inheritdoc/>
    protected override void Dispose (bool disposing)
    {
        if (disposing)
            Images.Dispose ();

        base.Dispose (disposing);
    }
}
