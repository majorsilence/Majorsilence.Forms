using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class MigratorTests : IDisposable
{
    private readonly string _dir;

    public MigratorTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-mig-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose ()
    {
        Directory.Delete (_dir, recursive: true);
        GC.SuppressFinalize (this);
    }

    private string Write (string name, string content)
    {
        var path = Path.Combine (_dir, name);
        Directory.CreateDirectory (Path.GetDirectoryName (path)!);
        File.WriteAllText (path, content);
        return path;
    }

    [Fact]
    public void Converts_single_source_file_to_output ()
    {
        var input = Write ("One.cs", "using System.Windows.Forms;\nclass F : Form { }\n");
        var outDir = Path.Combine (_dir, "out");

        var exit = new Migrator (new MigrationOptions { Input = input, Output = outDir, NoReport = true }).Run ();

        Assert.Equal (0, exit);
        var converted = File.ReadAllText (Path.Combine (outDir, "One.cs"));
        Assert.Contains ("using Majorsilence.Forms;", converted);
    }

    [Fact]
    public void Dry_run_writes_nothing ()
    {
        var input = Write ("Two.cs", "using System.Windows.Forms;\n");
        var original = File.ReadAllText (input);

        new Migrator (new MigrationOptions { Input = _dir, DryRun = true, NoReport = true }).Run ();

        Assert.Equal (original, File.ReadAllText (input)); // untouched
        Assert.False (File.Exists (input + ".bak"));
    }

    [Fact]
    public void In_place_conversion_leaves_a_backup ()
    {
        var input = Write ("Three.cs", "using System.Windows.Forms;\n");

        new Migrator (new MigrationOptions { Input = _dir, NoReport = true }).Run ();

        Assert.Contains ("using Majorsilence.Forms;", File.ReadAllText (input));
        Assert.True (File.Exists (input + ".bak"));
        Assert.Contains ("using System.Windows.Forms;", File.ReadAllText (input + ".bak"));
    }

    [Fact]
    public void No_backup_converts_in_place_without_a_bak_file ()
    {
        var input = Write ("Three.cs", "using System.Windows.Forms;\n");

        new Migrator (new MigrationOptions { Input = _dir, NoBackup = true, NoReport = true }).Run ();

        Assert.Contains ("using Majorsilence.Forms;", File.ReadAllText (input));
        Assert.False (File.Exists (input + ".bak"));
    }

    [Fact]
    public void Strict_mode_returns_nonzero_on_warnings ()
    {
        // Metafile has no Majorsilence equivalent → a manual-review warning.
        Write ("Four.cs", "class C { System.Drawing.Metafile b; }");

        var exit = new Migrator (new MigrationOptions { Input = _dir, DryRun = true, NoReport = true, Strict = true }).Run ();

        Assert.NotEqual (0, exit);
    }

    private const string WinFormsCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net8.0-windows</TargetFramework>
            <UseWindowsForms>true</UseWindowsForms>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public void Central_package_management_pins_versions_in_props_and_omits_inline_versions ()
    {
        Write ("Directory.Packages.props", "<Project>\n  <PropertyGroup>\n    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>\n  </PropertyGroup>\n  <ItemGroup />\n</Project>");
        Write ("App.csproj", WinFormsCsproj);
        var outDir = Path.Combine (_dir, "out");

        var exit = new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        Assert.Equal (0, exit);

        var csproj = File.ReadAllText (Path.Combine (outDir, "App.csproj"));
        Assert.Contains ("<PackageReference Include=\"Majorsilence.Forms\" />", csproj);
        Assert.DoesNotContain ("Version=", csproj);

        var props = File.ReadAllText (Path.Combine (outDir, "Directory.Packages.props"));
        Assert.Contains ("<PackageVersion Include=\"Majorsilence.Forms\" Version=\"0.3.0\" />", props);
        Assert.Contains ("<PackageVersion Include=\"Majorsilence.Forms.Avalonia\" Version=\"0.3.0\" />", props);
    }

    [Fact]
    public void Without_central_management_versions_stay_inline ()
    {
        Write ("App.csproj", WinFormsCsproj);
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        var csproj = File.ReadAllText (Path.Combine (outDir, "App.csproj"));
        Assert.Contains ("Version=\"0.3.0\"", csproj);
        Assert.False (File.Exists (Path.Combine (outDir, "Directory.Packages.props")));
    }

    [Fact]
    public void Writes_report_by_default ()
    {
        Write ("Five.cs", "using System.Windows.Forms;\n");
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir }).Run ();

        Assert.True (File.Exists (Path.Combine (outDir, "migration-report.md")));
    }
}
