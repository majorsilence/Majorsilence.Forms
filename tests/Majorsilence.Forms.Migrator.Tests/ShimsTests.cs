using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

/// <summary>
/// Covers <c>--shims</c>: the namespaces are left exactly as written for the generated
/// System.Windows.Forms build (<c>Majorsilence.Forms.WinFormsShims.Compat</c>) to resolve, while the
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
    public void References_the_generated_builds_instead_of_the_plain_runtime_ones ()
    {
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.Contains ("\"Majorsilence.Forms.WinFormsShims.Compat\"", result.Xml);
        Assert.Contains ("\"Majorsilence.Forms.WinFormsShims.Compat.Avalonia\"", result.Xml);

        // The generated builds are standalone copies, not add-ons: both Form types in one project would
        // be a second, unrelated System.Windows.Forms-shaped API beside the first.
        Assert.DoesNotContain ("\"Majorsilence.Forms\"", result.Xml);
        Assert.DoesNotContain ("\"Majorsilence.Forms.Avalonia\"", result.Xml);
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
    public void A_VB_project_gets_the_same_references_and_no_warning ()
    {
        // The earlier shim was a C#-only Roslyn generator, so a .vbproj got nothing from it and the
        // migrator warned. The generated build is an ordinary assembly: VB just references it.
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".", isVisualBasic: true);

        Assert.DoesNotContain (result.Warnings, w => w.Contains ("--shims"));
        Assert.Contains ("\"Majorsilence.Forms.WinFormsShims.Compat\"", result.Xml);
    }

    [Fact]
    public void Warns_when_asked_for_a_backend_that_has_no_generated_build ()
    {
        var options = new MigrationOptions { Input = "x", Shims = true, Backend = Backend.Uno };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.Contains (result.Warnings, w => w.Contains ("--shims") && w.Contains ("Uno"));
        Assert.DoesNotContain ("Majorsilence.Forms.Uno", result.Xml);
    }

    [Fact]
    public void No_warning_for_a_CSharp_project ()
    {
        var options = new MigrationOptions { Input = "x", Shims = true };
        var result = ProjectConverter.Convert (WinFormsProject, options, ".");

        Assert.DoesNotContain (result.Warnings, w => w.Contains ("--shims"));
    }
}
