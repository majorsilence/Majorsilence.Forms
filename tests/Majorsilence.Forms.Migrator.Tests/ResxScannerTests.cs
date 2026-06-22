using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class ResxScannerTests
{
    [Fact]
    public void Flags_typed_designer_resource ()
    {
        var xml = """<root><data name="b.Size" type="System.Drawing.Size, System.Drawing"><value>75, 23</value></data></root>""";
        var result = ResxScanner.Scan (xml);
        Assert.True (result.NeedsReview);
        Assert.Equal (1, result.DesignerResourceCount);
    }

    [Fact]
    public void Flags_binary_serialized_object ()
    {
        var xml = """<root><data name="i" mimetype="application/x-microsoft.net.object.binary.base64"><value>AAAB</value></data></root>""";
        var result = ResxScanner.Scan (xml);
        Assert.True (result.NeedsReview);
        Assert.Equal (1, result.BinaryResourceCount);
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
    }
}
