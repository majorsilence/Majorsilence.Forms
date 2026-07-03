using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class MyResourcesGeneratorTests
{
    private static string Resx (string dataElements) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <root>
        {dataElements}
        </root>
        """;

    // ---- path gating: only the VB "My Project\Resources.resx" is a My.Resources source ----

    [Theory]
    [InlineData (@"C:\proj\My Project\Resources.resx", true)]
    [InlineData (@"C:\proj\my project\Resources.resx", true)] // case-insensitive folder match
    [InlineData (@"C:\proj\Form1.resx", false)]
    [InlineData (@"C:\proj\My Project\Settings.resx", false)]
    [InlineData (@"C:\proj\Properties\Resources.resx", false)] // C#-style folder, not VB's "My Project"
    public void IsMyResourcesResx_matches_only_the_VB_My_Project_Resources_file (string path, bool expected)
    {
        Assert.Equal (expected, MyResourcesGenerator.IsMyResourcesResx (path));
    }

    // ---- classification: matches real Financial usage (image vs. byte array vs. string) ----

    [Fact]
    public void Classifies_Bitmap_bytearray_entry_as_Image ()
    {
        // The real shape for btnSearch: inline bytearray.base64 payload typed as System.Drawing.Bitmap.
        var resx = Resx("""<data name="btnSearch" type="System.Drawing.Bitmap, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"><value>AAAA</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);

        var r = Assert.Single (resources);
        Assert.Equal ("btnSearch", r.RawName);
        Assert.Equal ("btnSearch", r.PropertyName);
        Assert.Equal (MyResourcesGenerator.ResourceKind.Image, r.Kind);
    }

    [Fact]
    public void Classifies_ResXFileRef_to_Bitmap_as_Image ()
    {
        // The real shape for a linked file resource (e.g. "Completed"): type is ResXFileRef, and the
        // actual CLR type rides in the ';'-delimited second segment of <value>.
        var resx = Resx("""<data name="Completed" type="System.Resources.ResXFileRef, System.Windows.Forms"><value>..\Resources\Completed.png;System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);

        var r = Assert.Single (resources);
        Assert.Equal (MyResourcesGenerator.ResourceKind.Image, r.Kind);
    }

    [Fact]
    public void Classifies_ResXFileRef_to_ByteArray_as_ByteArray ()
    {
        // The real shape for t4_2011 (used with myWriter.Write(...) — needs Byte(), not a string).
        var resx = Resx("""<data name="t4_2011" type="System.Resources.ResXFileRef, System.Windows.Forms"><value>..\Resources\t4-2011.zip;System.Byte[], mscorlib, Version=4.0.0.0</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);

        var r = Assert.Single (resources);
        Assert.Equal ("t4_2011", r.PropertyName);
        Assert.Equal (MyResourcesGenerator.ResourceKind.ByteArray, r.Kind);
    }

    [Fact]
    public void Classifies_untyped_entry_as_StringOrOther ()
    {
        var resx = Resx("""<data name="Greeting" xml:space="preserve"><value>Hello</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);

        var r = Assert.Single (resources);
        Assert.Equal (MyResourcesGenerator.ResourceKind.StringOrOther, r.Kind);
    }

    // ---- identifier sanitization: must match real StronglyTypedResourceBuilder output exactly ----

    [Theory]
    [InlineData ("btnSearch", "btnSearch")]
    [InlineData ("number of assert on maintains ", "number_of_assert_on_maintains_")]
    [InlineData ("_15x15Collapse", "_15x15Collapse")]
    [InlineData ("_exit", "_exit")]
    [InlineData ("small_icons_Dashboard24x24", "small_icons_Dashboard24x24")]
    public void Sanitizes_resource_names_to_valid_VB_identifiers (string rawName, string expectedIdentifier)
    {
        var resx = Resx($"""<data name="{rawName}" xml:space="preserve"><value>x</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);

        var r = Assert.Single (resources);
        Assert.Equal (rawName, r.RawName);
        Assert.Equal (expectedIdentifier, r.PropertyName);
    }

    // ---- generated source shape ----

    [Fact]
    public void Generate_emits_a_Friend_Module_under_My_Resources_namespace ()
    {
        var resx = Resx("""<data name="Greeting" xml:space="preserve"><value>Hello</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);
        var generated = MyResourcesGenerator.Generate (resources, resx);

        Assert.Contains ("Namespace My.Resources", generated);
        Assert.Contains ("Friend Module Resources", generated);
        Assert.Contains ("Friend ReadOnly Property Greeting() As String", generated);
        Assert.Contains ("Option Strict On", generated);
    }

    [Fact]
    public void Generate_emits_Majorsilence_Drawing_Image_for_image_resources ()
    {
        var resx = Resx("""<data name="btnSearch" type="System.Drawing.Bitmap, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"><value>AAAA</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);
        var generated = MyResourcesGenerator.Generate (resources, resx);

        Assert.Contains ("Friend ReadOnly Property btnSearch() As Majorsilence.Forms.Drawing.Image", generated);
    }

    [Fact]
    public void Generate_emits_Byte_array_for_bytearray_resources ()
    {
        var resx = Resx("""<data name="t4_2011" type="System.Resources.ResXFileRef, System.Windows.Forms"><value>..\Resources\t4-2011.zip;System.Byte[], mscorlib</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);
        var generated = MyResourcesGenerator.Generate (resources, resx);

        Assert.Contains ("Friend ReadOnly Property t4_2011() As Byte()", generated);
    }

    [Fact]
    public void Generate_is_idempotent_for_the_same_resx ()
    {
        var resx = Resx("""<data name="Greeting" xml:space="preserve"><value>Hello</value></data>""");
        var resources = MyResourcesGenerator.ParseResources (resx);

        var first = MyResourcesGenerator.Generate (resources, resx);
        var second = MyResourcesGenerator.Generate (resources, resx);

        Assert.Equal (first, second);
    }

    [Fact]
    public void Generate_handles_resx_XML_with_embedded_quotes_and_newlines ()
    {
        // The resx XML is embedded as a VB string literal; entries with quotes/newlines in their raw
        // value must not break the generated literal.
        var resx = Resx("<data name=\"Quoted\" xml:space=\"preserve\"><value>Say &quot;hi&quot;\non two lines</value></data>");
        var resources = MyResourcesGenerator.ParseResources (resx);
        var generated = MyResourcesGenerator.Generate (resources, resx);

        // The generated source itself must be free of unescaped raw newlines inside a string literal —
        // spot check: every quoted chunk line either ends the literal or continues with " & vbCrLf &".
        Assert.Contains ("vbCrLf", generated);
        Assert.Contains ("Friend ReadOnly Property Quoted() As String", generated);
    }
}
