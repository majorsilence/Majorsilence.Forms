using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

/// <summary>
/// Covers <c>--dual-build</c>: the top-of-file WinForms/Drawing import becomes an
/// <c>#if MAJORSILENCE_FORMS</c> conditional instead of being rewritten unconditionally, and
/// <see cref="ProjectConverter"/> propagates the symbol into <c>DefineConstants</c> — see
/// <see cref="ConditionalImports"/>. Text-engine and project-file cases live here; the Roslyn-engine
/// counterpart of the source-level cases lives in <see cref="RoslynSourceConverterTests"/> for parity with
/// how the rest of the suite splits text vs. roslyn coverage.
/// </summary>
public class DualBuildTests
{
    [Fact]
    public void Text_engine_wraps_bare_WinForms_import_in_conditional ()
    {
        var result = SourceConverter.Convert ("using System.Windows.Forms;\n", dualBuild: true);
        Assert.Contains ("#if MAJORSILENCE_FORMS", result.Text);
        Assert.Contains ("using Majorsilence.Forms;", result.Text);
        Assert.Contains ("#else", result.Text);
        Assert.Contains ("using System.Windows.Forms;", result.Text);
        Assert.Contains ("#endif", result.Text);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Text_engine_dual_build_off_still_rewrites_unconditionally ()
    {
        // Regression guard: the default (dualBuild: false, the parameter's default) must be byte-for-byte
        // what it was before --dual-build existed.
        var result = SourceConverter.Convert ("using System.Windows.Forms;\n");
        Assert.Equal ("using Majorsilence.Forms;\n", result.Text);
        Assert.DoesNotContain ("#if", result.Text);
    }

    [Fact]
    public void Text_engine_still_rewrites_fully_qualified_references_unconditionally_under_dual_build ()
    {
        // Only the plain import line is conditional; other rewritten references in the body are not — see
        // ConditionalImports' class doc for why that's the deliberate scope.
        var result = SourceConverter.Convert ("System.Windows.Forms.MessageBox.Show(\"hi\");", dualBuild: true);
        Assert.Contains ("Majorsilence.Forms.MessageBox.Show", result.Text);
        Assert.DoesNotContain ("#if", result.Text);
    }

    [Fact]
    public void Text_engine_wraps_Drawing_companion_import_when_GDI_plus_type_used_unqualified ()
    {
        var src = "using System.Drawing;\nvar b = new Bitmap(10, 10);\n";
        var result = SourceConverter.Convert (src, dualBuild: true);
        Assert.Contains ("using System.Drawing;", result.Text);
        Assert.Contains ("#if MAJORSILENCE_FORMS", result.Text);
        Assert.Contains ("using Majorsilence.Forms.Drawing;", result.Text);
        Assert.Contains ("#endif", result.Text);
        // No #else branch for the Drawing companion: System.Drawing already covers WinForms-mode.
        Assert.DoesNotContain ("#else", result.Text);
    }

    [Fact]
    public void Text_engine_keeps_System_Drawing_unconditionally_when_only_GDI_plus_used_under_dual_build ()
    {
        // Without dual-build, RewriteDrawingImports would replace this line entirely (System.Drawing isn't
        // "needed" once nothing under it is used as a primitive). Under dual-build it must stay, because
        // real-WinForms-mode still needs it for Bitmap.
        var src = "using System.Drawing;\nvar b = new Bitmap(10, 10);\n";
        var dualBuild = SourceConverter.Convert (src, dualBuild: true);
        var normal = SourceConverter.Convert (src);

        Assert.Contains ("using System.Drawing;", dualBuild.Text);
        Assert.DoesNotContain ("using System.Drawing;", normal.Text); // sanity: confirms the premise above.
    }

    [Fact]
    public void Text_engine_VB_dual_build_falls_back_to_normal_conversion_with_a_warning ()
    {
        var result = SourceConverter.Convert ("Imports System.Windows.Forms\n",
            language: SourceLanguage.VisualBasic, dualBuild: true);
        Assert.DoesNotContain ("#If", result.Text);
        Assert.Contains ("Imports Majorsilence.Forms", result.Text);
        Assert.Contains (result.Warnings, w => w.Contains ("not supported for Visual Basic", System.StringComparison.Ordinal));
    }

    [Fact]
    public void ProjectConverter_adds_DefineConstants_for_MAJORSILENCE_FORMS_under_dual_build ()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net8.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """;
        var options = new MigrationOptions { Input = "x", DualBuild = true };
        var result = ProjectConverter.Convert (xml, options, ".");

        Assert.Contains ("MAJORSILENCE_FORMS", result.Xml);
        Assert.Contains ("DefineConstants", result.Xml);
        Assert.True (result.Changed);
    }

    [Fact]
    public void ProjectConverter_keeps_UseWindowsForms_and_windows_TFM_under_dual_build ()
    {
        // The whole point: the project must still build as real WinForms, so the SDK opt-ins that make that
        // possible are left alone under --dual-build, unlike the normal (full-migration) conversion.
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net8.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """;
        var options = new MigrationOptions { Input = "x", DualBuild = true };
        var result = ProjectConverter.Convert (xml, options, ".");

        Assert.Contains ("<UseWindowsForms>true</UseWindowsForms>", result.Xml);
        Assert.Contains ("net8.0-windows", result.Xml);
    }

    [Fact]
    public void ProjectConverter_still_adds_Majorsilence_references_under_dual_build ()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net8.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """;
        var options = new MigrationOptions { Input = "x", DualBuild = true, Backend = Backend.Avalonia };
        var result = ProjectConverter.Convert (xml, options, ".");

        Assert.Contains ("Majorsilence.Forms", result.Xml);
        Assert.Contains ("Majorsilence.Forms.Avalonia", result.Xml);
    }

    [Fact]
    public void ProjectConverter_VB_dual_build_falls_back_to_full_conversion_with_a_warning ()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>WinExe</OutputType>
                <TargetFramework>net8.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
              </PropertyGroup>
            </Project>
            """;
        var options = new MigrationOptions { Input = "x", DualBuild = true };
        var result = ProjectConverter.Convert (xml, options, ".", isVisualBasic: true);

        // Falls back to the normal, full conversion: UseWindowsForms/the -windows TFM are stripped same as
        // without --dual-build, and no DefineConstants propagation is added.
        Assert.DoesNotContain ("UseWindowsForms", result.Xml);
        Assert.DoesNotContain ("net8.0-windows", result.Xml);
        Assert.DoesNotContain ("MAJORSILENCE_FORMS", result.Xml);
        Assert.Contains (result.Warnings, w => w.Contains ("not supported for Visual Basic", System.StringComparison.Ordinal));
    }

    [Fact]
    [Trait ("Category", "Roslyn")]
    public void Roslyn_engine_wraps_bare_WinForms_import_in_conditional ()
    {
        using var helper = new RoslynSourceConverterTests ();
        var result = helper.Convert ("using System.Windows.Forms;\nclass F { }\n", dualBuild: true);
        Assert.Contains ("#if MAJORSILENCE_FORMS", result.Text);
        Assert.Contains ("using Majorsilence.Forms;", result.Text);
        Assert.Contains ("#else", result.Text);
        Assert.Contains ("using System.Windows.Forms;", result.Text);
    }
}
