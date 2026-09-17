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

    // --- the polymorphic-base alias: the one source edit --shims makes ------------------------------

    [Fact]
    public void Aliases_Control_when_used_as_a_variable_type ()
    {
        var src = """
            Imports System.Windows.Forms

            Public Class Probe
                Public Sub Clear(pnl As Panel)
                    For Each ctl As Control In pnl.Controls
                        ctl.Enabled = False
                    Next
                End Sub
            End Class
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.VisualBasic, out var inherited);

        Assert.Contains ("Imports Control = Majorsilence.Forms.Control", result);
        Assert.Empty (inherited);
        // Everything else is untouched — that is the point of --shims.
        Assert.Contains ("For Each ctl As Control In pnl.Controls", result);
        Assert.Contains ("Imports System.Windows.Forms", result);
    }

    [Fact]
    public void Leaves_Form_alone ()
    {
        // Form is NOT aliased: a migrated app's forms derive from the compat Form in their own source,
        // so polymorphic storage already works, and aliasing would retarget every `Inherits Form`.
        var src = """
            Imports System.Windows.Forms

            Public Class Probe
                Public Sub Show(owner As Form)
                End Sub
            End Class
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.VisualBasic, out _);

        Assert.DoesNotContain ("Imports Form =", result);
    }

    [Fact]
    public void Does_not_alias_a_base_the_file_inherits_by_its_bare_name ()
    {
        // Aliasing here would move the class onto the real base and lose the compat event/enum
        // shadowing its Designer code is written against.
        var src = """
            Imports System.Windows.Forms

            Public Class MyCanvas
                Inherits Control

                Private Sub Use(other As Control)
                End Sub
            End Class
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.VisualBasic, out var inherited);

        Assert.DoesNotContain ("Imports Control =", result);
        Assert.Contains ("Control", inherited);
    }

    [Fact]
    public void A_qualified_Inherits_does_not_block_the_alias ()
    {
        // `Inherits System.Windows.Forms.Control` keeps naming the compat type whatever the bare name
        // means, so there is nothing to protect and the alias is safe.
        var src = """
            Imports System.Windows.Forms

            Public Class MyCanvas
                Inherits System.Windows.Forms.Control

                Private Sub Use(other As Control)
                End Sub
            End Class
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.VisualBasic, out var inherited);

        Assert.Contains ("Imports Control = Majorsilence.Forms.Control", result);
        Assert.Empty (inherited);
    }

    [Fact]
    public void Does_not_alias_a_name_the_file_declares_itself ()
    {
        var src = """
            Imports System.Windows.Forms

            Public Class Control
            End Class
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.VisualBasic, out _);

        Assert.DoesNotContain ("Imports Control =", result);
    }

    [Fact]
    public void Does_not_add_a_duplicate_alias_on_a_second_run ()
    {
        var src = """
            Imports System.Windows.Forms
            Imports Control = Majorsilence.Forms.Control

            Public Class Probe
                Private Sub Use(c As Control)
                End Sub
            End Class
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.VisualBasic, out _);

        Assert.Equal (src, result);
    }

    [Fact]
    public void Emits_a_CSharp_alias_for_a_CSharp_file ()
    {
        var src = """
            using System.Windows.Forms;

            public class Probe
            {
                void Use(Control c) { }
            }
            """;

        var result = SourceConverter.AddPolymorphicBaseAliases (src, SourceLanguage.CSharp, out _);

        Assert.Contains ("using Control = global::Majorsilence.Forms.Control;", result);
    }
}
