using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class ResxScannerTests
{
    [Fact]
    public void Typed_designer_value_is_consumable_not_a_blocker ()
    {
        // A typed primitive (Size/Point/Color/…) loads via Majorsilence.Forms.ComponentResourceManager,
        // so it is consumable at runtime and must not be flagged for manual review.
        var xml = """<root><data name="b.Size" type="System.Drawing.Size, System.Drawing"><value>75, 23</value></data></root>""";
        var result = ResxScanner.Scan (xml);
        Assert.False (result.NeedsReview);
        Assert.True (result.HasConsumableResources);
        Assert.Equal (1, result.DesignerResourceCount);
    }

    [Fact]
    public void Bytearray_image_is_consumable_not_a_blocker ()
    {
        var xml = """<root><data name="b.Image" type="System.Drawing.Bitmap, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"><value>iVBORw0K</value></data></root>""";
        var result = ResxScanner.Scan (xml);
        Assert.False (result.NeedsReview);
        Assert.True (result.HasConsumableResources);
        Assert.Equal (1, result.ByteArrayImageCount);
    }

    // A binary.base64 blob whose decoded NRBF names a recoverable image type is consumable, not a blocker.
    [Theory]
    [InlineData ("System.Drawing.Bitmap")]
    [InlineData ("System.Drawing.Icon")]
    [InlineData ("System.Windows.Forms.ImageListStreamer")]
    public void Recoverable_image_blob_is_consumable_not_a_blocker (string typeMarker)
    {
        var blob = System.Convert.ToBase64String (
            System.Text.Encoding.UTF8.GetBytes ($"....{typeMarker}....payload...."));
        var xml = $"""<root><data name="b.Image" mimetype="application/x-microsoft.net.object.binary.base64"><value>{blob}</value></data></root>""";

        var result = ResxScanner.Scan (xml);
        Assert.False (result.NeedsReview);
        Assert.True (result.HasConsumableResources);
        Assert.Equal (1, result.RecoverableImageBlobCount);
    }

    [Fact]
    public void Binary_blob_of_unsupported_type_still_needs_review ()
    {
        var blob = System.Convert.ToBase64String (
            System.Text.Encoding.UTF8.GetBytes ("....System.Data.SqlTypes.SqlInt32....not an image...."));
        var xml = $"""<root><data name="p.Value" mimetype="application/x-microsoft.net.object.binary.base64"><value>{blob}</value></data></root>""";

        var result = ResxScanner.Scan (xml);
        Assert.True (result.NeedsReview);
        Assert.Equal (1, result.BinaryResourceCount);
    }

    [Fact]
    public void Scrubbed_placeholder_blob_is_not_flagged ()
    {
        // Sanitized corpora replace the payload with a stub; it's neither consumable nor a blocker.
        var xml = """<root><data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64"><value>[base64 mime encoded serialized .NET Framework object]</value></data></root>""";
        var result = ResxScanner.Scan (xml);
        Assert.False (result.NeedsReview);
        Assert.Equal (1, result.PlaceholderBlobCount);
    }

    [Fact]
    public void Plain_string_table_passes_clean ()
    {
        var xml = """
            <root>
              <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0</value></resheader>
              <data name="Greeting" xml:space="preserve"><value>Hello</value></data>
            </root>
            """;
        var result = ResxScanner.Scan (xml);
        Assert.False (result.NeedsReview);
        Assert.False (result.HasConsumableResources);
        Assert.Equal (1, result.StringCount);
    }
}
