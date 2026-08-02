using System.Reflection;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Imaging;
using SkiaSharp;
// See GraphicsPathTests for why this is aliased instead of `using System.Drawing;`.
using Color = System.Drawing.Color;

namespace Majorsilence.Forms.Drawing.Common.Tests;

/// <summary>
/// Covers the imaging metadata and multi-frame surface added in Phase 4 of docs/gdi-gap-plan.md:
/// <c>FrameDimension</c>, <c>PropertyItem</c>, <c>ColorPalette</c>, and the <c>Image</c> members around
/// them — plus <c>ImageAnimator</c>, which was a documented no-op until frame decoding existed to back it.
///
/// The frame tests build a real multi-frame GIF at runtime and decode it back, rather than asserting
/// that a member merely exists.
/// </summary>
public class ImageMetadataAndFrameTests
{
    /// <summary>
    /// Encodes a two-frame animated GIF: frame 0 solid red, frame 1 solid blue. SkiaSharp cannot write
    /// multi-frame GIFs, so the file is assembled by hand from the GIF89a structure.
    /// </summary>
    private static byte[] TwoFrameGif ()
    {
        var bytes = new List<byte> ();
        void Add (params int[] values) => bytes.AddRange (values.Select (v => (byte)v));

        Add (0x47, 0x49, 0x46, 0x38, 0x39, 0x61);          // "GIF89a"
        Add (1, 0, 1, 0);                                   // logical screen 1x1
        Add (0x80, 0, 0);                                   // global color table, 2 entries
        Add (0xFF, 0x00, 0x00);                             // index 0: red
        Add (0x00, 0x00, 0xFF);                             // index 1: blue

        // NETSCAPE2.0 looping extension — what marks the file as an animation.
        Add (0x21, 0xFF, 0x0B);
        bytes.AddRange ("NETSCAPE2.0"u8.ToArray ());
        Add (0x03, 0x01, 0x00, 0x00, 0x00);

        foreach (var colorIndex in new[] { 0, 1 }) {
            Add (0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00);   // graphic control, 100ms
            Add (0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0);                  // image descriptor 1x1
            Add (0x02, 0x02);                                        // LZW min code size 2, 2 data bytes
            Add (colorIndex == 0 ? 0x44 : 0x4C, 0x01);                // clear + index + end, packed LSB-first
            Add (0x00);                                              // block terminator
        }

        Add (0x3B);                                          // trailer
        return [.. bytes];
    }

    private static byte[] SingleFramePng ()
    {
        using var bitmap = new SKBitmap (4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var image = SKImage.FromBitmap (bitmap);
        using var data = image.Encode (SKEncodedImageFormat.Png, 100);
        return data.ToArray ();
    }

    // ---- Frames ----

    [Fact]
    public void A_single_frame_image_reports_one_frame_and_the_page_dimension ()
    {
        using var image = Image.FromBytes (SingleFramePng ());

        Assert.Equal (1, image.GetFrameCount (FrameDimension.Time));
        Assert.Equal ([FrameDimension.Page.Guid], image.FrameDimensionsList);
        Assert.False (ImageAnimator.CanAnimate (image));
    }

    [Fact]
    public void A_multi_frame_gif_reports_its_frames_and_can_animate ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());

        Assert.True (image.GetFrameCount (FrameDimension.Time) > 1,
            "the hand-built GIF should decode as more than one frame");
        Assert.Equal ([FrameDimension.Time.Guid], image.FrameDimensionsList);
        Assert.True (ImageAnimator.CanAnimate (image));
    }

    [Fact]
    public void SelectActiveFrame_decodes_a_different_frame ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());
        Assert.True (image.GetFrameCount (FrameDimension.Time) > 1);

        var bitmap = (Bitmap)image;
        image.SelectActiveFrame (FrameDimension.Time, 0);
        var first = bitmap.GetPixel (0, 0);

        var selected = image.SelectActiveFrame (FrameDimension.Time, 1);
        var second = bitmap.GetPixel (0, 0);

        Assert.Equal (1, selected);
        Assert.NotEqual (first, second);   // the two frames are different colors
    }

    [Fact]
    public void SelectActiveFrame_ignores_an_out_of_range_index ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());
        var bitmap = (Bitmap)image;
        image.SelectActiveFrame (FrameDimension.Time, 1);
        var before = bitmap.GetPixel (0, 0);

        image.SelectActiveFrame (FrameDimension.Time, 99);

        Assert.Equal (before, bitmap.GetPixel (0, 0));
    }

    [Fact]
    public void ImageAnimator_advances_frames_and_raises_its_handler ()
    {
        using var image = Image.FromBytes (TwoFrameGif ());
        Assert.True (ImageAnimator.CanAnimate (image));

        var raised = 0;
        EventHandler handler = (_, _) => raised++;
        ImageAnimator.Animate (image, handler);

        var bitmap = (Bitmap)image;
        var before = bitmap.GetPixel (0, 0);
        ImageAnimator.UpdateFrames (image);

        Assert.Equal (1, raised);
        Assert.NotEqual (before, bitmap.GetPixel (0, 0));

        ImageAnimator.StopAnimate (image, handler);
        ImageAnimator.UpdateFrames (image);
        Assert.Equal (1, raised);   // no longer animating
    }

    [Fact]
    public void ImageAnimator_ignores_a_single_frame_image ()
    {
        using var image = Image.FromBytes (SingleFramePng ());
        var raised = 0;
        EventHandler handler = (_, _) => raised++;

        ImageAnimator.Animate (image, handler);
        ImageAnimator.UpdateFrames ();

        Assert.Equal (0, raised);
    }

    // ---- Metadata ----

    [Fact]
    public void Property_items_round_trip_and_can_be_removed ()
    {
        using var image = Image.FromBytes (SingleFramePng ());
        Assert.Empty (image.PropertyItems);

        image.SetPropertyItem (PropertyItem.Create (0x010E, 2, "hello"u8.ToArray ()));

        Assert.Single (image.PropertyItems);
        Assert.Equal ([0x010E], image.PropertyIdList);
        Assert.Equal ("hello"u8.ToArray (), image.GetPropertyItem (0x010E).Value!);
        Assert.Equal (5, image.GetPropertyItem (0x010E).Len);

        image.RemovePropertyItem (0x010E);
        Assert.Empty (image.PropertyItems);
    }

    [Fact]
    public void SetPropertyItem_replaces_an_existing_id_rather_than_duplicating_it ()
    {
        using var image = Image.FromBytes (SingleFramePng ());
        image.SetPropertyItem (PropertyItem.Create (42, 2, [1]));
        image.SetPropertyItem (PropertyItem.Create (42, 2, [2]));

        Assert.Single (image.PropertyItems);
        Assert.Equal ([(byte)2], image.GetPropertyItem (42).Value!);
    }

    [Fact]
    public void GetPropertyItem_throws_for_an_id_that_is_not_present ()
        => Assert.Throws<ArgumentException> (() => {
            using var image = Image.FromBytes (SingleFramePng ());
            image.GetPropertyItem (0x1234);
        });

    [Fact]
    public void Exif_tags_are_read_from_a_jpeg ()
    {
        // A minimal JPEG carrying an APP1/EXIF block with one tag (Orientation = 6).
        var jpeg = BuildJpegWithOrientation (6);
        using var image = Image.FromBytes (jpeg);

        var orientation = image.PropertyItems.FirstOrDefault (p => p.Id == 0x0112);
        Assert.NotNull (orientation);
        Assert.Equal (6, BitConverter.ToUInt16 (orientation!.Value!, 0));
    }

    // Assembles a JPEG whose APP1 segment holds a little-endian TIFF header with a single
    // Orientation (0x0112) SHORT tag. The image data itself is not valid, which is fine: the EXIF
    // reader walks the segment chain and never decodes pixels.
    private static byte[] BuildJpegWithOrientation (ushort orientation)
    {
        var tiff = new List<byte> ();
        tiff.AddRange ("II"u8.ToArray ());                       // little-endian
        tiff.AddRange (BitConverter.GetBytes ((ushort)42));
        tiff.AddRange (BitConverter.GetBytes (8u));              // first IFD at offset 8
        tiff.AddRange (BitConverter.GetBytes ((ushort)1));       // one entry
        tiff.AddRange (BitConverter.GetBytes ((ushort)0x0112));  // Orientation
        tiff.AddRange (BitConverter.GetBytes ((ushort)3));       // SHORT
        tiff.AddRange (BitConverter.GetBytes (1u));              // one component
        tiff.AddRange (BitConverter.GetBytes ((uint)orientation)); // inline value
        tiff.AddRange (BitConverter.GetBytes (0u));              // no next IFD

        var app1 = new List<byte> ();
        app1.AddRange ("Exif"u8.ToArray ());
        app1.AddRange ([0, 0]);
        app1.AddRange (tiff);

        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
        var length = app1.Count + 2;
        jpeg.Add ((byte)(length >> 8));
        jpeg.Add ((byte)(length & 0xFF));
        jpeg.AddRange (app1);
        jpeg.AddRange ([0xFF, 0xD9]);
        return [.. jpeg];
    }

    // ---- Palette, bounds and pixel-format helpers ----

    [Fact]
    public void Palette_round_trips ()
    {
        using var image = Image.FromBytes (SingleFramePng ());
        Assert.NotNull (image.Palette);
        Assert.Empty (image.Palette.Entries);

        var palette = (ColorPalette)typeof (ColorPalette)
            .GetConstructor (BindingFlags.NonPublic | BindingFlags.Instance, [typeof (Color[])])!
            .Invoke ([new[] { Color.Red, Color.Blue }]);
        image.Palette = palette;

        Assert.Equal (Color.Red, image.Palette.Entries[0]);
    }

    [Fact]
    public void GetBounds_reports_pixel_bounds ()
    {
        using var image = Image.FromBytes (SingleFramePng ());
        var unit = GraphicsUnit.Inch;

        var bounds = image.GetBounds (ref unit);

        Assert.Equal (GraphicsUnit.Pixel, unit);
        Assert.Equal (new System.Drawing.RectangleF (0, 0, 4, 4), bounds);
    }

    [Theory]
    [InlineData (PixelFormat.Format32bppArgb, 32)]
    [InlineData (PixelFormat.Format24bppRgb, 24)]
    [InlineData (PixelFormat.Format8bppIndexed, 8)]
    [InlineData (PixelFormat.Format1bppIndexed, 1)]
    public void GetPixelFormatSize_reads_the_depth_out_of_the_enum_encoding (PixelFormat format, int expected)
        => Assert.Equal (expected, Image.GetPixelFormatSize (format));

    [Fact]
    public void PixelFormat_predicates_match_the_format_flags ()
    {
        Assert.True (Image.IsAlphaPixelFormat (PixelFormat.Format32bppArgb));
        Assert.False (Image.IsAlphaPixelFormat (PixelFormat.Format24bppRgb));
        Assert.True (Image.IsCanonicalPixelFormat (PixelFormat.Format32bppArgb));
        Assert.True (Image.IsExtendedPixelFormat (PixelFormat.Format64bppArgb));
        Assert.False (Image.IsExtendedPixelFormat (PixelFormat.Format32bppArgb));
    }

    // ---- Codec and encoder metadata ----

    [Fact]
    public void Image_codecs_are_fully_described ()
    {
        var encoders = ImageCodecInfo.GetImageEncoders ();

        Assert.All (encoders, codec => {
            Assert.NotEmpty (codec.CodecName);
            Assert.NotEmpty (codec.FilenameExtension);
            Assert.NotEmpty (codec.MimeType);
            Assert.NotEqual (Guid.Empty, codec.FormatID);
            Assert.NotNull (codec.SignaturePatterns);
        });

        var png = encoders.Single (c => c.MimeType == "image/png");
        Assert.Equal (ImageFormat.Png.Guid, png.FormatID);
        Assert.Equal ([0x89, 0x50, 0x4E, 0x47], png.SignaturePatterns![0]);
    }

    [Fact]
    public void Decoders_are_a_superset_of_encoders_not_the_same_list ()
    {
        // Skia decodes formats it will not write, so returning the encoder list would be wrong.
        var encoders = ImageCodecInfo.GetImageEncoders ();
        var decoders = ImageCodecInfo.GetImageDecoders ();

        Assert.True (decoders.Length > encoders.Length);
        Assert.Contains (decoders, c => c.MimeType == "image/x-icon");
        Assert.DoesNotContain (encoders, c => c.MimeType == "image/x-icon");
    }

    [Fact]
    public void ImageFormat_guids_match_the_GDI_plus_values ()
    {
        // Persisted/designer data compares against these, so they are API.
        Assert.Equal (new Guid ("b96b3caf-0728-11d3-9d7b-0000f81ef32e"), ImageFormat.Png.Guid);
        Assert.Equal (new Guid ("b96b3cae-0728-11d3-9d7b-0000f81ef32e"), ImageFormat.Jpeg.Guid);
        Assert.Equal (new Guid ("b96b3cb0-0728-11d3-9d7b-0000f81ef32e"), ImageFormat.Gif.Guid);
    }

    [Fact]
    public void Encoder_guids_match_the_GDI_plus_values ()
    {
        Assert.Equal (new Guid ("1d5be4b5-fa4a-452d-9cdd-5db35105e7eb"), Encoder.Quality.Guid);
        Assert.Equal (new Guid ("e09d739d-ccd4-44ee-8eba-3fbf8be4fc58"), Encoder.Compression.Guid);
        Assert.Equal (new Guid ("66087055-ad66-4c7c-9a18-38a2310b8337"), Encoder.ColorDepth.Guid);
    }

    [Theory]
    [InlineData (50L, EncoderParameterValueType.ValueTypeLong)]
    [InlineData ("text", EncoderParameterValueType.ValueTypeAscii)]
    public void EncoderParameter_infers_its_value_type (object value, EncoderParameterValueType expected)
    {
        using var parameter = new EncoderParameter (Encoder.Quality, value);

        Assert.Equal (expected, parameter.ValueType);
        Assert.Equal (expected, parameter.Type);
        Assert.Equal (1, parameter.NumberOfValues);
    }
}
