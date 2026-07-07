using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises the cross-platform ComponentResourceManager that lets migrated WinForms designer code
// (resources.GetObject/GetString/ApplyResources) read a .resx without System.Drawing/BinaryFormatter.
public class ComponentResourceManagerTests
{
    // A 1x1 transparent PNG — the kind of payload a WinForms .resx stores as bytearray.base64.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [Fact]
    public void Reads_plain_string_entry ()
    {
        var resx = Resx("""<data name="$this.Text" xml:space="preserve"><value>Hello</value></data>""");
        var mgr = ComponentResourceManager.FromXml(resx);
        Assert.Equal("Hello", mgr.GetString("$this.Text"));
    }

    [Fact]
    public void Reads_typed_primitive_entries ()
    {
        var resx = Resx("""
            <data name="b.Size" type="System.Drawing.Size, System.Drawing"><value>120, 30</value></data>
            <data name="b.Location" type="System.Drawing.Point, System.Drawing"><value>5, 7</value></data>
            <data name="b.Visible" type="System.Boolean, mscorlib"><value>False</value></data>
            <data name="b.TabIndex" type="System.Int32, mscorlib"><value>4</value></data>
            <data name="b.BackColor" type="System.Drawing.Color, System.Drawing"><value>255, 0, 0</value></data>
            """);
        var mgr = ComponentResourceManager.FromXml(resx);

        Assert.Equal(new Size(120, 30), mgr.GetObject("b.Size"));
        Assert.Equal(new Point(5, 7), mgr.GetObject("b.Location"));
        Assert.Equal(false, mgr.GetObject("b.Visible"));
        Assert.Equal(4, mgr.GetObject("b.TabIndex"));
        Assert.Equal(Color.FromArgb(255, 0, 0), mgr.GetObject("b.BackColor"));
    }

    [Fact]
    public void Decodes_bytearray_image_to_Majorsilence_image ()
    {
        var resx = Resx($"""<data name="b.Image" type="System.Drawing.Bitmap, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"><value>{OnePixelPng}</value></data>""");
        var mgr = ComponentResourceManager.FromXml(resx);

        var image = mgr.GetObject("b.Image");
        var bitmap = Assert.IsAssignableFrom<Majorsilence.Forms.Drawing.Image>(image);
        Assert.Equal(1, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
    }

    [Fact]
    public void Returns_null_for_BinaryFormatter_payload ()
    {
        var resx = Resx("""<data name="b.Icon" mimetype="application/x-microsoft.net.object.binary.base64"><value>AAEAAAD/////</value></data>""");
        var mgr = ComponentResourceManager.FromXml(resx);
        Assert.Null(mgr.GetObject("b.Icon"));
    }

    [Fact]
    public void Missing_entry_returns_null ()
    {
        var mgr = ComponentResourceManager.FromXml(Resx(""));
        Assert.Null(mgr.GetObject("nope"));
        Assert.Null(mgr.GetString("nope"));
    }

    [Fact]
    public void ApplyResources_sets_matching_properties_by_reflection ()
    {
        var resx = Resx("""
            <data name="ctrl.Text" xml:space="preserve"><value>Click me</value></data>
            <data name="ctrl.TabIndex" type="System.Int32, mscorlib"><value>3</value></data>
            <data name="ctrl.Visible" type="System.Boolean, mscorlib"><value>False</value></data>
            <data name="other.Text" xml:space="preserve"><value>ignored</value></data>
            """);
        var mgr = ComponentResourceManager.FromXml(resx);

        var target = new FakeControl();
        mgr.ApplyResources(target, "ctrl");

        Assert.Equal("Click me", target.Text);
        Assert.Equal(3, target.TabIndex);
        Assert.False(target.Visible);
    }

    [Fact]
    public void Empty_manager_from_unknown_type_does_not_throw ()
    {
        // The (Type) ctor must degrade to an empty manager when no .resx is found, so designer code runs.
        var mgr = new ComponentResourceManager(typeof(ComponentResourceManagerTests));
        Assert.Null(mgr.GetObject("anything"));
    }

    [Fact]
    public void Reads_standard_SDK_compiled_resources_binary ()
    {
        // Regression: every migrated .Forms project's `resources.ApplyResources(...)` calls were
        // silently no-ops, because a normal `<EmbeddedResource Include="Foo.resx">` project item
        // compiles to a "<Namespace>.Foo.resources" binary manifest resource, not a raw XML ".resx"
        // one -- and the (Type) constructor only ever looked for the latter. Found via a real
        // ReportDesigner.Forms startup rendering as a completely blank window (no menu/toolbar/
        // panels), because none of their Size/Location/Dock/Text ever got applied.
        // Fixtures/CompiledResourceFixture.resx is compiled exactly like a real ported designer
        // .resx (GenerateResourceUsePreserializedResources=true, same as every .Forms csproj).
        var mgr = new ComponentResourceManager(typeof(Fixtures.CompiledResourceFixture));

        Assert.Equal("Fixture Form", mgr.GetString("$this.Text"));
        Assert.Equal(new System.Drawing.Size(75, 23), mgr.GetObject("button1.Size"));
        Assert.Equal(new System.Drawing.Point(10, 12), mgr.GetObject("button1.Location"));
        Assert.Equal(0, mgr.GetObject("button1.TabIndex"));
        Assert.Equal(false, mgr.GetObject("button1.Visible"));

        // button1.Dock is typed as System.Windows.Forms.DockStyle in the resx -- an assembly this
        // framework deliberately never references. RegisterWinFormsEnumResolver bridges it to a
        // same-valued stand-in type (Majorsilence.Forms.WinFormsEnumShims) rather than skipping it,
        // so the raw value comes back as *a* DockStyle.Fill, just not Majorsilence.Forms' own one --
        // ApplyResources (below) is what bridges it the rest of the way to the real property type.
        var dock = mgr.GetObject("button1.Dock");
        Assert.NotNull(dock);
        Assert.Equal("Fill", dock!.ToString());
        Assert.Equal(5, (int)dock);
    }

    [Fact]
    public void ApplyResources_from_compiled_resources_binary_sets_matching_properties ()
    {
        var mgr = new ComponentResourceManager(typeof(Fixtures.CompiledResourceFixture));
        var target = new FakeControl();

        mgr.ApplyResources(target, "button1");

        Assert.Equal(0, target.TabIndex);
        Assert.False(target.Visible);
        // Regression: button1.Dock resolves via the shim as System.Windows.Forms.DockStyle.Fill,
        // a different CLR type than Majorsilence.Forms.DockStyle -- ApplyResources must still bridge
        // it across by underlying value (see TryConvert), not reject it as a type mismatch.
        Assert.Equal(Majorsilence.Forms.DockStyle.Fill, target.Dock);
    }

    private sealed class FakeControl
    {
        public string Text { get; set; } = "";
        public int TabIndex { get; set; }
        public bool Visible { get; set; } = true;
        public Majorsilence.Forms.DockStyle Dock { get; set; } = Majorsilence.Forms.DockStyle.None;
    }

    private static string Resx(string body) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          {body}
        </root>
        """;
}
