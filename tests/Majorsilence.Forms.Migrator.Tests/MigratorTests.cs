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
    public void Strips_windows_suffix_in_an_imported_custom_props_file ()
    {
        Write ("build/common.props", "<Project>\n  <PropertyGroup>\n    <TargetFramework>net8.0-windows</TargetFramework>\n  </PropertyGroup>\n</Project>");
        Write ("App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <Import Project="build\common.props" />
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """);
        var outDir = Path.Combine (_dir, "out");

        var exit = new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        Assert.Equal (0, exit);
        var props = File.ReadAllText (Path.Combine (outDir, "build", "common.props"));
        Assert.Contains ("<TargetFramework>net8.0</TargetFramework>", props);
        Assert.DoesNotContain ("net8.0-windows", props);
    }

    [Fact]
    public void In_place_project_keeps_its_version_and_drops_windows ()
    {
        var proj = Write ("App.csproj", WinFormsCsproj);

        new Migrator (new MigrationOptions { Input = _dir, NoReport = true }).Run ();

        var converted = File.ReadAllText (proj);
        Assert.Contains ("<TargetFramework>net8.0</TargetFramework>", converted);
        Assert.DoesNotContain ("net8.0-windows", converted);
    }

    [Fact]
    public void Removes_Telerik_package_end_to_end ()
    {
        Write ("App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>WinExe</OutputType><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup><PackageReference Include="Telerik.UI.for.WinForms" Version="2024.1.1" /></ItemGroup>
            </Project>
            """);
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        Assert.DoesNotContain ("Telerik", File.ReadAllText (Path.Combine (outDir, "App.csproj")));
    }

    [Fact]
    public void Map_file_removePackages_drops_a_custom_package ()
    {
        var map = Write ("map.json", """{ "removePackages": [ "Acme.WinForms.*" ] }""");
        Write ("App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>WinExe</OutputType><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup><PackageReference Include="Acme.WinForms.Grid" Version="1.0.0" /></ItemGroup>
            </Project>
            """);
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true, MapFiles = new[] { map } }).Run ();

        Assert.DoesNotContain ("Acme.WinForms", File.ReadAllText (Path.Combine (outDir, "App.csproj")));
    }

    [Fact]
    public void Does_not_inject_constructor_when_a_sibling_partial_already_has_one ()
    {
        // Code-behind already defines the constructor; the designer holds InitializeComponent.
        Write ("Form1.vb", "Imports System.Windows.Forms\nPublic Class Form1\n  Public Sub New()\n    InitializeComponent()\n  End Sub\nEnd Class\n");
        Write ("Form1.Designer.vb", "Partial Class Form1\n  Inherits System.Windows.Forms.Form\n  Private Sub InitializeComponent()\n  End Sub\nEnd Class\n");
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        // Nothing injected anywhere — and certainly not a duplicate in the designer file.
        var designer = File.ReadAllText (Path.Combine (outDir, "Form1.Designer.vb"));
        Assert.DoesNotContain ("Sub New", designer);
        Assert.DoesNotContain ("[majorsilence-migrate]", designer);
        var codeBehind = File.ReadAllText (Path.Combine (outDir, "Form1.vb"));
        Assert.Equal (1, CountOccurrences (codeBehind, "Sub New"));
    }

    [Fact]
    public void Injects_constructor_into_the_code_behind_not_the_designer ()
    {
        // Neither partial has a constructor; the form uses InitializeComponent (in the designer).
        Write ("Form1.vb", "Imports System.Windows.Forms\nPublic Class Form1\nEnd Class\n");
        Write ("Form1.Designer.vb", "Partial Class Form1\n  Inherits System.Windows.Forms.Form\n  Private Sub InitializeComponent()\n  End Sub\nEnd Class\n");
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        var codeBehind = File.ReadAllText (Path.Combine (outDir, "Form1.vb"));
        Assert.Contains ("Public Sub New()", codeBehind);
        Assert.Contains ("[majorsilence-migrate]", codeBehind);

        var designer = File.ReadAllText (Path.Combine (outDir, "Form1.Designer.vb"));
        Assert.DoesNotContain ("Public Sub New", designer);
    }

    private static int CountOccurrences (string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf (needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }

    [Fact]
    public void Does_not_reference_Majorsilence_for_a_non_winforms_project ()
    {
        // A windows-targeted library with no WinForms opt-in and no WinForms code.
        Write ("Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0-windows</TargetFramework></PropertyGroup>
            </Project>
            """);
        Write ("Service.cs", "namespace Lib { public class Service { public int Add (int a, int b) => a + b; } }");
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        var csproj = File.ReadAllText (Path.Combine (outDir, "Lib.csproj"));
        Assert.DoesNotContain ("Majorsilence.Forms", csproj);     // no UI dependency forced on it
        Assert.Contains ("<TargetFramework>net8.0</TargetFramework>", csproj); // but still converted
    }

    [Fact]
    public void References_Majorsilence_when_source_uses_winforms_without_the_opt_in ()
    {
        // No <UseWindowsForms>, but the code clearly uses WinForms.
        Write ("App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>WinExe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        Write ("Form1.cs", "using System.Windows.Forms;\npublic class Form1 : Form { }");
        var outDir = Path.Combine (_dir, "out");

        new Migrator (new MigrationOptions { Input = _dir, Output = outDir, NoReport = true }).Run ();

        Assert.Contains ("Majorsilence.Forms", File.ReadAllText (Path.Combine (outDir, "App.csproj")));
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
