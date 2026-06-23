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
    public void Retargets_framework ()
    {
        var result = ProjectConverter.Convert (WinFormsCsproj, Options (), ".");
        Assert.Contains ("<TargetFramework>net10.0</TargetFramework>", result.Xml);
        Assert.DoesNotContain ("net8.0-windows", result.Xml);
    }

    [Fact]
    public void Normalizes_TargetFrameworks_plural_to_singular ()
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
        Assert.Contains ("<TargetFramework>net10.0</TargetFramework>", result.Xml);
        Assert.DoesNotContain ("TargetFrameworks", result.Xml);
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
