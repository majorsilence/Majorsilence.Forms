using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class CentralPackageManagementTests : IDisposable
{
    private readonly string _dir;

    public CentralPackageManagementTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-cpm-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose ()
    {
        Directory.Delete (_dir, recursive: true);
        GC.SuppressFinalize (this);
    }

    [Fact]
    public void Find_walks_up_to_an_ancestor_props_file ()
    {
        var props = Path.Combine (_dir, "Directory.Packages.props");
        File.WriteAllText (props, "<Project />");
        var nested = Path.Combine (_dir, "src", "App");
        Directory.CreateDirectory (nested);

        Assert.Equal (props, CentralPackageManagement.Find (nested));
    }

    [Fact]
    public void Find_returns_null_when_no_props_file_exists ()
    {
        Assert.Null (CentralPackageManagement.Find (_dir));
    }

    [Fact]
    public void IsEnabled_is_true_by_the_files_mere_presence ()
    {
        Assert.True (CentralPackageManagement.IsEnabled ("<Project><ItemGroup /></Project>"));
    }

    [Fact]
    public void IsEnabled_honours_an_explicit_false ()
    {
        var xml = "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>";
        Assert.False (CentralPackageManagement.IsEnabled (xml));
    }

    [Fact]
    public void EnsureVersions_appends_missing_entries ()
    {
        var xml = "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"SkiaSharp\" Version=\"3.0.0\" />\n  </ItemGroup>\n</Project>";

        var (updated, changed) = CentralPackageManagement.EnsureVersions (
            xml, new[] { ("Majorsilence.Forms", "0.3.0"), ("Majorsilence.Forms.Avalonia", "0.3.0") });

        Assert.True (changed);
        Assert.Contains ("<PackageVersion Include=\"Majorsilence.Forms\" Version=\"0.3.0\" />", updated);
        Assert.Contains ("<PackageVersion Include=\"Majorsilence.Forms.Avalonia\" Version=\"0.3.0\" />", updated);
    }

    [Fact]
    public void EnsureVersions_leaves_existing_entries_untouched ()
    {
        var xml = "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"Majorsilence.Forms\" Version=\"9.9.9\" />\n  </ItemGroup>\n</Project>";

        var (updated, changed) = CentralPackageManagement.EnsureVersions (
            xml, new[] { ("Majorsilence.Forms", "0.3.0") });

        Assert.False (changed);
        Assert.Contains ("Version=\"9.9.9\"", updated);
        Assert.DoesNotContain ("0.3.0", updated);
    }
}
