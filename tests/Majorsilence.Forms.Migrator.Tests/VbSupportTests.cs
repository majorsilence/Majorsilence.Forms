using Majorsilence.Forms.Migrator;
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
    public void Adds_companion_VB_drawing_import_directly_after ()
    {
        var result = SourceConverter.Convert ("Imports System.Drawing\nPublic Class C\nEnd Class\n", language: SourceLanguage.VisualBasic);
        Assert.Contains ("Imports System.Drawing\nImports Majorsilence.Drawing", result.Text.Replace ("\r\n", "\n"));
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

    [Fact]
    public void Warns_when_VB_form_lacks_constructor ()
    {
        var src = "Public Class Form1\n  Private Sub Setup()\n    InitializeComponent()\n  End Sub\nEnd Class";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.Contains (result.Warnings, w => w.Contains ("Public Sub New"));
    }

    [Fact]
    public void Does_not_warn_when_VB_form_has_constructor ()
    {
        var src = "Public Class Form1\n  Public Sub New()\n    InitializeComponent()\n  End Sub\nEnd Class";
        var result = SourceConverter.Convert (src, language: SourceLanguage.VisualBasic);
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("Public Sub New"));
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
}
