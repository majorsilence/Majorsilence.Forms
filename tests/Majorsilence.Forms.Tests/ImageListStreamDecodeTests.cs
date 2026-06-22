using Xunit;

namespace Majorsilence.Forms.Tests;

// Validates recovery of WinForms ImageListStreamer resources (binary.base64) without BinaryFormatter,
// end-to-end against a real production payload (see ImageListStreamFixture):
// NRBF unwrap -> MSFt RLE -> comctl32 IL header -> BMP strip + mask -> per-frame bitmaps.
public class ImageListStreamDecodeTests
{
    private static string ResxWithImageStream () => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="imgList.ImageStream" mimetype="application/x-microsoft.net.object.binary.base64">
            <value>{ImageListStreamFixture.Base64}</value>
          </data>
        </root>
        """;

    [Fact]
    public void GetObject_decodes_ImageListStreamer_with_all_frames ()
    {
        var mgr = ComponentResourceManager.FromXml (ResxWithImageStream ());

        var streamer = Assert.IsType<ImageListStreamer> (mgr.GetObject ("imgList.ImageStream"));
        Assert.Equal (ImageListStreamFixture.FrameCount, streamer.Frames.Count);
        Assert.Equal (ImageListStreamFixture.FrameWidth, streamer.FrameSize.Width);
        Assert.Equal (ImageListStreamFixture.FrameHeight, streamer.FrameSize.Height);
        Assert.All (streamer.Frames, f =>
        {
            Assert.Equal (ImageListStreamFixture.FrameWidth, f.Width);
            Assert.Equal (ImageListStreamFixture.FrameHeight, f.Height);
        });
    }

    [Fact]
    public void Assigning_ImageStream_populates_the_ImageList ()
    {
        var mgr = ComponentResourceManager.FromXml (ResxWithImageStream ());
        var streamer = (ImageListStreamer) mgr.GetObject ("imgList.ImageStream")!;

        // The designer pattern: imageList1.ImageStream = (ImageListStreamer) resources.GetObject(...).
        using var imageList = new ImageList { ImageStream = streamer };

        Assert.Equal (ImageListStreamFixture.FrameCount, imageList.Images.Count);
        Assert.Equal (ImageListStreamFixture.FrameWidth, imageList.ImageSize.Width);
        Assert.Equal (ImageListStreamFixture.FrameHeight, imageList.ImageSize.Height);
    }

    [Fact]
    public void Malformed_binary_blob_returns_null_not_throws ()
    {
        var resx = """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="x" mimetype="application/x-microsoft.net.object.binary.base64"><value>bm90LXJlYWwtbnJiZg==</value></data>
            </root>
            """;
        var mgr = ComponentResourceManager.FromXml (resx);
        Assert.Null (mgr.GetObject ("x"));
    }
}
