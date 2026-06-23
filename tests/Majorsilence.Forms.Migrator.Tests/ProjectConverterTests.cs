using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class ProjectConverterTests
{
    private static MigrationOptions Options (Backend backend = Backend.Avalonia, ReferenceMode mode = ReferenceMode.Package) =>
        new () { Input = "x", Backend = backend, ReferenceMode = mode };

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
    public void Removes_UseWindowsForms ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (), ".");
        Assert.DoesNotContain ("UseWindowsForms", result.Xml);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Strips_windows_suffix_preserving_the_version ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (), ".");
        Assert.Contains ("<TargetFramework>net8.0</TargetFramework>", result.Xml);
        Assert.DoesNotContain ("net8.0-windows", result.Xml);
    }

    [Fact]
    public void Strips_windows_platform_version_suffix ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.Contains ("<TargetFramework>net10.0</TargetFramework>", result.Xml);
        Assert.DoesNotContain ("windows", result.Xml);
    }

    [Fact]
    public void Strips_windows_in_each_TargetFrameworks_entry_keeping_it_plural ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0-windows;net48</TargetFrameworks>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.Contains ("<TargetFrameworks>net8.0;net48</TargetFrameworks>", result.Xml);
        Assert.DoesNotContain ("net8.0-windows", result.Xml);
    }

    [Fact]
    public void Explicit_tfm_forces_the_exact_framework ()
    {
        var options = new MigrationOptions { Input = "x", TargetFramework = "net10.0" };
        var result = ProjectConverter.Convert (WinFormsCsproj, options, ".");
        Assert.Contains ("<TargetFramework>net10.0</TargetFramework>", result.Xml);
        Assert.DoesNotContain ("net8.0", result.Xml);
    }

    [Fact]
    public void Adds_package_references_for_core_and_backend ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (Backend.Avalonia), ".");
        Assert.Contains ("<PackageReference Include=\"Majorsilence.Forms\"", result.Xml);
        Assert.Contains ("<PackageReference Include=\"Majorsilence.Forms.Avalonia\"", result.Xml);
    }

    [Fact]
    public void Backend_selection_changes_the_backend_reference ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (Backend.Uno), ".");
        Assert.Contains ("Majorsilence.Forms.Uno", result.Xml);
        Assert.DoesNotContain ("Majorsilence.Forms.Avalonia", result.Xml);
    }

    [Fact]
    public void Project_reference_mode_emits_ProjectReference ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (mode: ReferenceMode.Project), ".");
        Assert.Contains ("<ProjectReference", result.Xml);
        Assert.Contains ("Majorsilence.Forms.csproj", result.Xml);
    }

    [Fact]
    public void Removes_WindowsDesktop_framework_reference ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup><FrameworkReference Include="Microsoft.WindowsDesktop.App" /></ItemGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.DoesNotContain ("WindowsDesktop", result.Xml);
    }

    [Fact]
    public void Does_not_duplicate_existing_references ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup><PackageReference Include="Majorsilence.Forms" Version="0.3.0" /></ItemGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.Equal (1, CountOccurrences (result.Xml, "Include=\"Majorsilence.Forms\""));
    }

    [Fact]
    public void Central_package_management_omits_the_version_attribute ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (), ".", centralPackageManagement: true);
        Assert.Contains ("<PackageReference Include=\"Majorsilence.Forms\" />", result.Xml);
        Assert.Contains ("<PackageReference Include=\"Majorsilence.Forms.Avalonia\" />", result.Xml);
        // No inline Version anywhere — it belongs in Directory.Packages.props.
        Assert.DoesNotContain ("Version=", result.Xml);
    }

    [Fact]
    public void Central_package_management_reports_added_packages ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (Backend.Uno), ".", centralPackageManagement: true);
        Assert.Equal (new[] { "Majorsilence.Forms", "Majorsilence.Forms.Uno" }, result.AddedPackages);
    }

    [Fact]
    public void Non_central_management_still_pins_the_version_inline ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (), ".");
        Assert.Contains ("Version=\"0.3.0\"", result.Xml);
    }

    [Fact]
    public void Removes_Telerik_WinForms_package_reference ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Telerik.UI.for.WinForms.AllControls" Version="2024.1.1" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.DoesNotContain ("Telerik.UI.for.WinForms", result.Xml);
        // An unrelated package is left in place.
        Assert.Contains ("Newtonsoft.Json", result.Xml);
    }

    [Fact]
    public void Removes_central_PackageVersion_for_a_WinForms_package ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Telerik.UI.for.WinForms" Version="2024.1.1" />
              </ItemGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.DoesNotContain ("Telerik.UI.for.WinForms", result.Xml);
    }

    [Fact]
    public void Custom_remove_pattern_drops_a_matching_package ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
              <ItemGroup><PackageReference Include="Acme.WinForms.Grid" Version="1.0.0" /></ItemGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".",
            removePackagePatterns: new[] { "Acme.WinForms.*" });
        Assert.DoesNotContain ("Acme.WinForms.Grid", result.Xml);
    }

    [Fact]
    public void Does_not_add_references_when_not_a_winforms_project ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net8.0-windows</TargetFramework></PropertyGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".", addMajorsilenceReferences: false);
        Assert.DoesNotContain ("Majorsilence.Forms", result.Xml);
        // Other transforms still apply — the -windows suffix is dropped.
        Assert.Contains ("<TargetFramework>net8.0</TargetFramework>", result.Xml);
    }

    [Fact]
    public void Skips_legacy_non_sdk_project_with_warning ()
    {
        var xml = """<Project ToolsVersion="4.0"><PropertyGroup /></Project>""";
        var result = ProjectConverter.Convert (xml, Options (), ".");
        Assert.False (result.Changed);
        Assert.Contains (result.Warnings, w => w.Contains ("SDK", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Malformed_xml_is_reported_not_thrown ()
    {
        var result = ProjectConverter.Convert ("<Project><not closed", Options (), ".");
        Assert.False (result.Changed);
        Assert.NotEmpty (result.Warnings);
    }

    private static int CountOccurrences (string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf (needle, i, System.StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
