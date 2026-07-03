using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class VbSupportTests
{
    private static MigrationOptions Options () => new () { Input = "x" };

    // ---- VB source (.vb) ----

    [Fact]
    public void Rewrites_VB_Imports_directive ()
    {
        var result = SourceConverter.Convert ("Imports System.Windows.Forms\n", language: SourceLanguage.VisualBasic);
        Assert.Contains ("Imports Majorsilence.Forms", result.Text);
    }

    [Fact]
    public void Removes_unused_VB_drawing_import ()
    {
        var result = SourceConverter.Convert ("Imports System.Drawing\nPublic Class C\nEnd Class\n", language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain ("Imports System.Drawing", result.Text);
    }

    [Fact]
    public void Keeps_VB_drawing_import_when_a_primitive_is_used ()
    {
        var result = SourceConverter.Convert ("Imports System.Drawing\nPublic Class C\n  Dim p As Point\nEnd Class\n", language: SourceLanguage.VisualBasic);
        Assert.Contains ("Imports System.Drawing", result.Text);
    }

    [Fact]
    public void Replaces_VB_drawing_import_with_companion_for_GDI_plus ()
    {
        var result = SourceConverter.Convert ("Imports System.Drawing\nPublic Class C\n  Dim b As Bitmap\nEnd Class\n", language: SourceLanguage.VisualBasic);
        var text = result.Text.Replace ("\r\n", "\n");
        Assert.DoesNotContain ("Imports System.Drawing\n", text);
        Assert.Contains ("Imports Majorsilence.Forms.Drawing", text);
    }

    [Fact]
    public void Warns_on_My_namespace ()
    {
        var result = SourceConverter.Convert ("x = My.Settings.Greeting", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Warns_on_ComputerInfo ()
    {
        var result = SourceConverter.Convert ("Dim c As New ComputerInfo()", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("ComputerInfo"));
    }

    // ---- My.Application.Info / My.Resources / My.Computer.Name: implemented, no longer warn ----

    [Theory]
    [InlineData ("x = My.Application.Info.Title")]
    [InlineData ("x = My.Application.Info.AssemblyName")]
    [InlineData ("x = My.Application.Info.Version.ToString")]
    [InlineData ("x = My.Application.Info.Copyright")]
    [InlineData ("x = My.Application.Info.CompanyName")]
    [InlineData ("x = My.Application.Info.Description")]
    [InlineData ("x = My.Application.Info.ProductName")]
    public void Does_not_warn_on_My_Application_Info (string src)
    {
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("My.*"));
    }

    [Theory]
    [InlineData ("btn.Image = CType(My.Resources.btnSearch, Majorsilence.Forms.Drawing.Image)")]
    [InlineData ("myWriter.Write(My.Resources.t4_2011)")]
    [InlineData ("x = My.Resources.Resources.SomeName")]
    public void Does_not_warn_on_My_Resources (string src)
    {
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Does_not_warn_on_My_Computer_Name ()
    {
        var result = SourceConverter.Convert ("If My.Computer.Name.ToUpper = \"X\" Then", language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("My.*"));
    }

    // ---- Regression: every OTHER My.* member must still warn exactly as before ----

    [Fact]
    public void Still_warns_on_My_Application_Log ()
    {
        var result = SourceConverter.Convert ("My.Application.Log.WriteEntry(\"x\")", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_Application_Shutdown_event ()
    {
        var result = SourceConverter.Convert ("AddHandler My.Application.Shutdown, AddressOf X", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_bare_My_Application ()
    {
        var result = SourceConverter.Convert ("Dim a = My.Application", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_Computer_Registry ()
    {
        var result = SourceConverter.Convert ("x = My.Computer.Registry.LocalMachine", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_Computer_Clipboard ()
    {
        var result = SourceConverter.Convert ("x = My.Computer.Clipboard.GetText()", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_Computer_Info ()
    {
        var result = SourceConverter.Convert ("x = My.Computer.Info.OSFullName", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_bare_My_Computer ()
    {
        var result = SourceConverter.Convert ("Dim c = My.Computer", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_Forms ()
    {
        var result = SourceConverter.Convert ("My.Forms.Form1.Show()", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_Settings ()
    {
        var result = SourceConverter.Convert ("x = My.Settings.SomeSetting", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Still_warns_on_My_User ()
    {
        var result = SourceConverter.Convert ("x = My.User.Name", language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Mixed_file_warns_only_for_the_unimplemented_member ()
    {
        // My.Application.Info doesn't warrant a warning, but My.Application.Log in the same file still does.
        var src = "x = My.Application.Info.Title\nMy.Application.Log.WriteEntry(\"x\")";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Injects_constructor_when_VB_form_lacks_one ()
    {
        var src = "Public Class Form1\n  Private Sub Setup()\n    InitializeComponent()\n  End Sub\nEnd Class";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.True (result.Changed);
        Assert.Contains ("Public Sub New()", result.Text);
        Assert.Contains ("InitializeComponent()", result.Text);
        // It's an applied fix, not a manual-review item.
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("Public Sub New"));
    }

    [Fact]
    public void Injects_constructor_after_Inherits_line ()
    {
        var src = "Partial Class Form1\n    Inherits Majorsilence.Forms.Form\n    Private Sub InitializeComponent()\n    End Sub\nEnd Class\n";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        var text = result.Text.Replace ("\r\n", "\n");
        // Constructor must sit *after* Inherits (VB requires Inherits to be the first statement).
        Assert.True (text.IndexOf ("Inherits", System.StringComparison.Ordinal)
                     < text.IndexOf ("Public Sub New()", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_inject_when_VB_form_already_has_constructor ()
    {
        var src = "Public Class Form1\n  Public Sub New()\n    InitializeComponent()\n  End Sub\nEnd Class";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.False (result.Changed);
        Assert.DoesNotContain ("[majorsilence-migrate]", result.Text);
    }

    [Fact]
    public void Does_not_inject_constructor_for_non_form_VB_files ()
    {
        // No InitializeComponent => not a form => nothing injected.
        var src = "Public Class Helper\n  Public Sub Work()\n  End Sub\nEnd Class";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain ("Public Sub New", result.Text);
    }

    [Fact]
    public void Redirects_ComponentResourceManager_to_Majorsilence ()
    {
        var src = "var r = new System.ComponentModel.ComponentResourceManager(typeof(Form1));";
        var result = SourceConverter.Convert (src);
        Assert.Contains ("Majorsilence.Forms.ComponentResourceManager", result.Text);
        Assert.DoesNotContain ("System.ComponentModel.ComponentResourceManager", result.Text);
    }

    [Fact]
    public void My_warning_is_not_raised_for_CSharp ()
    {
        // "My.Settings" is not valid C#, but guard anyway: the VB pass must not run for C#.
        var result = SourceConverter.Convert ("var x = My.Settings;", language: SourceLanguage.CSharp);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("My.*"));
    }

    [Fact]
    public void Maps_qualified_ContentAlignment_to_Majorsilence_Forms ()
    {
        var result = SourceConverter.Convert ("System.Drawing.ContentAlignment a;");
        Assert.Contains ("Majorsilence.Forms.ContentAlignment", result.Text);
    }

    // ---- VB project (.vbproj) ----

    private const string VbProj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net8.0-windows</TargetFramework>
            <UseWindowsForms>true</UseWindowsForms>
            <MyType>WindowsForms</MyType>
          </PropertyGroup>
          <ItemGroup>
            <Import Include="System.Windows.Forms" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void Forces_MyType_Empty ()
    {
        var result = ProjectConverter.Convert (VbProj, Options (), ".", isVisualBasic: true);
        Assert.Contains ("<MyType>Empty</MyType>", result.Xml);
        Assert.DoesNotContain ("WindowsForms", result.Xml);
    }

    [Fact]
    public void Adds_MyType_Empty_when_absent ()
    {
        var xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (xml, Options (), ".", isVisualBasic: true);
        Assert.Contains ("<MyType>Empty</MyType>", result.Xml);
    }

    [Fact]
    public void Removes_My_Project_designer_files ()
    {
        var result = ProjectConverter.Convert (VbProj, Options (), ".", isVisualBasic: true);
        Assert.Contains ("My Project", result.Xml);
        Assert.Contains ("Compile", result.Xml);
        Assert.Contains ("Remove=", result.Xml);
    }

    [Fact]
    public void Excludes_My_Project_Resources_resx_from_default_EmbeddedResource_glob ()
    {
        // My Project\Resources.resx now backs a generated My.Resources.vb (see MyResourcesGenerator)
        // that embeds its content as a string literal — the SDK's default EmbeddedResource glob must not
        // also try to compile the same .resx into a .resources binary (which fails outright on
        // non-string entries without System.Resources.Extensions).
        var result = ProjectConverter.Convert (VbProj, Options (), ".", isVisualBasic: true);
        Assert.Contains ("EmbeddedResource", result.Xml);
        Assert.Contains ("Resources.resx", result.Xml);
    }

    [Fact]
    public void Rewrites_project_level_VB_import ()
    {
        var result = ProjectConverter.Convert (VbProj, Options (), ".", isVisualBasic: true);
        Assert.Contains ("<Import Include=\"Majorsilence.Forms\"", result.Xml);
    }

    [Fact]
    public void Warns_about_VB_entry_point ()
    {
        var result = ProjectConverter.Convert (VbProj, Options (), ".", isVisualBasic: true);
        Assert.Contains (result.Warnings, w => w.Contains ("entry point"));
    }

    [Fact]
    public void CSharp_project_gets_no_MyType ()
    {
        var result = ProjectConverter.Convert (VbProj, Options (), ".", isVisualBasic: false);
        Assert.DoesNotContain ("MyType>Empty", result.Xml);
    }

    [Fact]
    public void Library_VB_project_gets_no_entry_point_warning ()
    {
        // A class library has no entry point to lose, so MyType=Empty doesn't warrant the warning.
        var lib = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><OutputType>Library</OutputType><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (lib, Options (), ".", isVisualBasic: true);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("entry point"));
    }

    [Fact]
    public void VB_project_without_OutputType_gets_no_entry_point_warning ()
    {
        // Absent <OutputType> defaults to a library under the SDK.
        var proj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
            </Project>
            """;
        var result = ProjectConverter.Convert (proj, Options (), ".", isVisualBasic: true);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("entry point"));
    }

    // Typed-DataSet (.xsd) designer files only touch System.ComponentModel.Design via HelpKeywordAttribute,
    // which is available cross-platform — they are pure System.Data and must not be flagged.
    [Fact]
    public void HelpKeywordAttribute_alone_is_not_flagged ()
    {
        var src = "<Global.System.ComponentModel.Design.HelpKeywordAttribute(\"vs.data.DataSet\")> _\nPartial Public Class ds\nEnd Class";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("System.ComponentModel.Design"));
    }

    [Fact]
    public void Other_ComponentModel_Design_type_is_still_flagged ()
    {
        var src = "Dim d As System.ComponentModel.Design.IDesigner";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("System.ComponentModel.Design"));
    }
}
