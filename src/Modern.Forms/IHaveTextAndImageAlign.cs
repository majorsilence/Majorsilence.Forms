using SkiaSharp;

namespace Modern.Forms;

// Used by TextImageLayoutEngine to lay out text and image
interface IHaveTextAndImageAlign
{
    ContentAlignment ImageAlign { get; set; }
    ContentAlignment TextAlign { get; set; }
    TextImageRelation TextImageRelation { get; set; }
    Modern.Drawing.Image? Image { get; set; }
    SKBitmap? ImageSK { get; }
    ImageList? ImageList { get; set; }
    int ImageIndex { get; set; }
    string ImageKey { get; set; }
    bool Multiline => false;

    public SKBitmap? GetImage ()
    {
        if (ImageSK is not null)
            return ImageSK;

        if (ImageList is null)
            return null;

        if (ImageIndex >= 0)
            return ImageList.Images[ImageIndex];

        if (ImageKey.Length > 0)
            return ImageList.Images[ImageKey];

        return null;
    }
}
