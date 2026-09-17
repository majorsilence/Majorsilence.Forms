using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

/// <summary>
/// Covers <c>--shims</c>: the namespaces are left exactly as written for
/// <c>Majorsilence.Forms.WinFormsShims.Compat</c>'s generated surface to resolve, while the
/// project-file half of the migration happens as usual. The inverse of the normal conversion, so the
/// tests are mostly "asserts nothing changed" — which is the whole contract.
/// </summary>
public class ShimsTests
{
    private const string WinFormsProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net8.0-windows</TargetFramework>
            <UseWindowsForms>true</UseWindowsForms>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public void References_the_generator_package_instead_of_the_plain_runtime_one ()
    {
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.Contains ("Majorsilence.Forms.WinFormsShims.Compat", result.Xml);
        // The generator package carries Majorsilence.Forms as a dependency, so referencing it again
        // here would be redundant. The backend still has to be explicit.
        Assert.Contains ("Majorsilence.Forms.Avalonia", result.Xml);
        Assert.DoesNotContain ("\"Majorsilence.Forms\"", result.Xml);
    }

    [Fact]
    public void Default_still_references_the_plain_runtime_package ()
    {
        // Regression guard: every existing caller that doesn't pass Shims must be unaffected.
        var options = new MigrationOptions { Input = "x" };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.Contains ("\"Majorsilence.Forms\"", result.Xml);
        Assert.DoesNotContain ("WinFormsShims", result.Xml);
    }

    [Fact]
    public void Project_file_migration_still_happens ()
    {
        // The point of --shims is to skip the SOURCE rewrite, not the project one: without this the
        // project would still be asking for real WinForms on a -windows TFM.
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.DoesNotContain ("<UseWindowsForms>true</UseWindowsForms>", result.Xml);
        Assert.Contains ("net8.0", result.Xml);
        Assert.DoesNotContain ("net8.0-windows", result.Xml);
    }

    [Fact]
    public void Warns_that_a_VB_project_gets_nothing_from_a_CSharp_only_generator ()
    {
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".", isVisualBasic: true);

        Assert.Contains (result.Warnings, w => w.Contains ("--shims") && w.Contains ("C#-only"));
    }

    [Fact]
    public void No_warning_for_a_CSharp_project ()
    {
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.DoesNotContain (result.Warnings, w => w.Contains ("--shims"));
    }
}
