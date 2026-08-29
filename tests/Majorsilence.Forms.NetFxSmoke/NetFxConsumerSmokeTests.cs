using System;
using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Proves the netstandard2.0 build of Majorsilence.Forms loads and runs on classic .NET Framework
// (net472). These paths are all backend-free: no Majorsilence.Forms.Backends.Platform.Backend is
// set here (there is no .NET Framework backend), so anything requiring a window/render loop is out
// of scope for this smoke test.
public class NetFxConsumerSmokeTests
{
    [Fact]
    public void Assembly_targets_netstandard2_0_on_this_runtime ()
    {
        // .NET Framework has no closer asset than lib/netstandard2.0, so this is the row under test.
        var tfm = typeof (Control).Assembly
            .GetCustomAttributes (typeof (System.Runtime.Versioning.TargetFrameworkAttribute), false);
        Assert.NotEmpty (tfm);
        Assert.Contains (".NETStandard,Version=v2.0",
            ((System.Runtime.Versioning.TargetFrameworkAttribute) tfm[0]).FrameworkName);
    }

    [Fact]
    public void Value_types_and_enums_are_usable ()
    {
        var p = new Padding (3, 4, 5, 6);
        Assert.Equal (8, p.Horizontal);   // left + right
        Assert.Equal (10, p.Vertical);    // top + bottom
        Assert.Equal (DockStyle.Fill, (DockStyle) 5);
    }

    [Fact]
    public void ComponentResourceManager_reads_typed_primitives_from_xml ()
    {
        var resx = Resx ("""
            <data name="b.Size" type="System.Drawing.Size, System.Drawing"><value>120, 30</value></data>
            <data name="b.Location" type="System.Drawing.Point, System.Drawing"><value>5, 7</value></data>
            <data name="b.BackColor" type="System.Drawing.Color, System.Drawing"><value>255, 0, 0</value></data>
            """);
        var mgr = ComponentResourceManager.FromXml (resx);

        Assert.Equal (new Size (120, 30), mgr.GetObject ("b.Size"));
        Assert.Equal (new Point (5, 7), mgr.GetObject ("b.Location"));
        Assert.Equal (Color.FromArgb (255, 0, 0), mgr.GetObject ("b.BackColor"));
    }

    [Fact]
    public void ComponentResourceManager_decodes_a_bytearray_image_via_skiasharp ()
    {
        const string onePixelPng =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        var resx = Resx ($"""<data name="b.Image" type="System.Drawing.Bitmap, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"><value>{onePixelPng}</value></data>""");
        var mgr = ComponentResourceManager.FromXml (resx);

        var image = Assert.IsAssignableFrom<Majorsilence.Forms.Drawing.Image> (mgr.GetObject ("b.Image"));
        Assert.Equal (1, image.Width);
        Assert.Equal (1, image.Height);
    }

    [Fact]
    public void Compiled_resources_resolve_the_embedded_WinForms_and_Drawing_shims ()
    {
        // The netstandard2.0-only branch under test: ComponentResourceManager registers an
        // AppDomain.AssemblyResolve handler (AssemblyLoadContext.Resolving on the .NET TFMs) that
        // hands back the embedded "System.Windows.Forms" / "System.Drawing.Common" shim assemblies so
        // a compiled .resx that names System.Windows.Forms.DockStyle / System.Drawing.Bitmap still
        // reads. Without the fallback this throws and every docked control / image comes back null.
        var mgr = new ComponentResourceManager (typeof (Fixtures.CompiledResourceFixture));

        var dock = mgr.GetObject ("button1.Dock");
        Assert.NotNull (dock);
        Assert.Equal ("Fill", dock!.ToString ());
        Assert.Equal (5, (int) dock);

        var image = Assert.IsAssignableFrom<Majorsilence.Forms.Drawing.Image> (mgr.GetObject ("fixtureBitmapBytes"));
        Assert.True (image.Width > 0);
    }

    private static string Resx (string body) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="version"><value>2.0</value></resheader>
          {body}
        </root>
        """;
}
