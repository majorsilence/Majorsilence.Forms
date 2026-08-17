using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// A <see cref="ToolStripItem"/> draws the image its <see cref="ToolStripItem.ImageIndex"/> or
/// <see cref="ToolStripItem.ImageKey"/> names in the owning strip's <see cref="ToolStrip.ImageList"/>.
/// </summary>
/// <remarks>
/// ImageIndex, ImageKey and ToolStrip.ImageList were all stored-and-never-read stubs, so a designer
/// toolbar — which is built entirely out of an ImageList plus per-button indices — rendered with every
/// button blank. ToolStrip.ImageList was additionally a second field hiding
/// <see cref="ToolBar.ImageList"/>, so the two disagreed about which list had been assigned.
/// </remarks>
public class ToolStripItemImageListTests
{
    private static ImageList TwoColourList ()
    {
        var list = new ImageList ();
        list.Images.Add ("red", Filled (SKColors.Red));
        list.Images.Add ("blue", Filled (SKColors.Blue));
        return list;
    }

    private static SKBitmap Filled (SKColor colour)
    {
        var bitmap = new SKBitmap (16, 16, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas (bitmap))
            canvas.Clear (colour);
        return bitmap;
    }

    [Fact]
    public void ImageIndex_resolves_against_the_owning_strips_ImageList ()
    {
        var strip = new ToolStrip { ImageList = TwoColourList () };
        var item = new ToolStripButton ();
        strip.Items.Add (item);

        item.ImageIndex = 1;

        Assert.NotNull (item.Image);
        Assert.Equal (SKColors.Blue, item.ImageSK!.GetPixel (0, 0));
    }

    [Fact]
    public void ImageKey_resolves_against_the_owning_strips_ImageList ()
    {
        var strip = new ToolStrip { ImageList = TwoColourList () };
        var item = new ToolStripButton ();
        strip.Items.Add (item);

        item.ImageKey = "red";

        Assert.Equal (SKColors.Red, item.ImageSK!.GetPixel (0, 0));
    }

    [Fact]
    public void The_index_still_resolves_when_it_was_assigned_before_the_ImageList_was ()
    {
        // The order designer code actually uses: indices are set while the list is still empty, and
        // ImageStream fills it in afterwards. Anything resolved eagerly would be null forever.
        var strip = new ToolStrip ();
        var item = new ToolStripButton ();
        strip.Items.Add (item);
        item.ImageIndex = 0;

        Assert.Null (item.ImageSK);

        strip.ImageList = TwoColourList ();

        Assert.Equal (SKColors.Red, item.ImageSK!.GetPixel (0, 0));
    }

    [Fact]
    public void The_index_still_resolves_when_the_item_is_added_to_the_strip_afterwards ()
    {
        var strip = new ToolStrip { ImageList = TwoColourList () };
        var item = new ToolStripButton { ImageIndex = 1 };

        Assert.Null (item.ImageSK);

        strip.Items.Add (item);

        Assert.Equal (SKColors.Blue, item.ImageSK!.GetPixel (0, 0));
    }

    [Fact]
    public void An_assigned_image_wins_over_the_ImageList ()
    {
        var strip = new ToolStrip { ImageList = TwoColourList () };
        var item = new ToolStripButton { ImageIndex = 0 };
        strip.Items.Add (item);

        item.Image = Filled (SKColors.Green);

        Assert.Equal (SKColors.Green, item.ImageSK!.GetPixel (0, 0));
    }

    [Fact]
    public void Setting_the_index_clears_the_key_and_the_other_way_round ()
    {
        var item = new ToolStripButton { ImageKey = "red" };

        item.ImageIndex = 1;
        Assert.Equal (string.Empty, item.ImageKey);

        item.ImageKey = "red";
        Assert.Equal (-1, item.ImageIndex);
    }

    [Fact]
    public void An_out_of_range_index_resolves_to_no_image_rather_than_throwing ()
    {
        var strip = new ToolStrip { ImageList = TwoColourList () };
        var item = new ToolStripButton ();
        strip.Items.Add (item);

        item.ImageIndex = 99;

        Assert.Null (item.ImageSK);
        Assert.Null (item.Image);
    }

    [Fact]
    public void Reading_Image_twice_hands_back_the_same_instance ()
    {
        // Callers compare and dispose what this returns, so it must not wrap the bitmap afresh per read.
        var strip = new ToolStrip { ImageList = TwoColourList () };
        var item = new ToolStripButton ();
        strip.Items.Add (item);
        item.ImageIndex = 0;

        Assert.Same (item.Image, item.Image);
    }

    [Fact]
    public void ToolStrip_and_ToolBar_agree_on_which_ImageList_was_assigned ()
    {
        // Designer code assigns through the ToolStrip-typed field it declared; image lookup reads the
        // ToolBar one. As separate storage those two silently disagreed.
        var strip = new ToolStrip ();
        var list = TwoColourList ();

        strip.ImageList = list;

        Assert.Same (list, ((ToolBar)strip).ImageList);
    }
}
