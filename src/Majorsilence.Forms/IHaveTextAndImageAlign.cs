using SkiaSharp;

namespace Majorsilence.Forms;

// Used by TextImageLayoutEngine to lay out text and image
interface IHaveTextAndImageAlign
{
    ContentAlignment ImageAlign { get; set; }
    ContentAlignment TextAlign { get; set; }
    TextImageRelation TextImageRelation { get; set; }
    Majorsilence.Forms.Drawing.Image? Image { get; set; }
    SKBitmap? ImageSK { get; }
    ImageList? ImageList { get; set; }
    int ImageIndex { get; set; }
    string ImageKey { get; set; }
    // Was a default interface member (=> false); every implementer (Button, CheckBox, RadioButton,
    // Label) already provides it, and DIM isn't supported on the netstandard2.0 runtime.
    bool Multiline { get; }
}

// Was IHaveTextAndImageAlign.GetImage, a default interface member. Moved to an extension method so the
// interface carries no method bodies -- the netstandard2.0 runtime (.NET Framework consumers) has no
// default-interface-implementation support.
static class HaveTextAndImageAlignExtensions
{
    public static SKBitmap? GetImage (this IHaveTextAndImageAlign? self)
    {
        if (self is null)
            return null;

        if (self.ImageSK is not null)
            return self.ImageSK;

        if (self.ImageList is null)
            return null;

        if (self.ImageIndex >= 0)
            return self.ImageList.Images[self.ImageIndex];

        if (self.ImageKey.Length > 0)
            return self.ImageList.Images[self.ImageKey];

        return null;
    }
}
