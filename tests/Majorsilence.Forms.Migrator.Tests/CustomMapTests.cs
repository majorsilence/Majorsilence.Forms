using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class CustomMapTests : IDisposable
{
    private readonly string _dir;

    public CustomMapTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-map-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose () => Directory.Delete (_dir, recursive: true);

    private string WriteJson (string name, string content)
    {
        var path = Path.Combine (_dir, name);
        File.WriteAllText (path, content);
        return path;
    }

    [Fact]
    public void Empty_when_no_files ()
    {
        Assert.Empty (CustomMap.Load (Array.Empty<string> ()).Namespaces);
    }

    [Fact]
    public void Loads_namespace_mappings ()
    {
        var path = WriteJson ("m.json", """{ "namespaces": { "Telerik.WinControls.UI": "Majorsilence.Forms.Telerik" } }""");
        var map = CustomMap.Load (new[] { path });
        Assert.Contains (map.Namespaces, m => m.From == "Telerik.WinControls.UI" && m.To == "Majorsilence.Forms.Telerik");
    }

    [Fact]
    public void Orders_mappings_longest_prefix_first ()
    {
        var path = WriteJson ("m.json", """{ "namespaces": { "A.B": "X", "A.B.C": "Y" } }""");
        var map = CustomMap.Load (new[] { path });
        Assert.Equal ("A.B.C", map.Namespaces[0].From); // longer first
    }

    [Fact]
    public void Later_files_override_earlier ()
    {
        var a = WriteJson ("a.json", """{ "namespaces": { "N": "First" } }""");
        var b = WriteJson ("b.json", """{ "namespaces": { "N": "Second" } }""");
        var map = CustomMap.Load (new[] { a, b });
        Assert.Equal ("Second", map.Namespaces.Single (m => m.From == "N").To);
    }

    [Fact]
    public void Missing_file_throws_FileNotFound ()
    {
        Assert.Throws<FileNotFoundException> (() => CustomMap.Load (new[] { Path.Combine (_dir, "nope.json") }));
    }

    [Fact]
    public void Invalid_json_throws_Format ()
    {
        var path = WriteJson ("bad.json", "{ not json");
        Assert.Throws<FormatException> (() => CustomMap.Load (new[] { path }));
    }

    [Fact]
    public void SourceConverter_applies_custom_mapping ()
    {
        var path = WriteJson ("m.json", """{ "namespaces": { "Telerik.WinControls.UI": "Majorsilence.Forms.Telerik" } }""");
        var map = CustomMap.Load (new[] { path });
        var result = SourceConverter.Convert ("using Telerik.WinControls.UI;", map);
        Assert.Contains ("using Majorsilence.Forms.Telerik;", result.Text);
    }
}
