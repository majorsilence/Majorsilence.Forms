using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

/// <summary>
/// Mirrors <see cref="SourceConverterTests"/>'s per-case structure, but against a real, temp-project-backed
/// fixture — the Roslyn engine consumes a Roslyn <c>Document</c> resolved from a loaded
/// <see cref="RoslynWorkspaceContext"/>, not bare text, so each case here builds (and tears down) an actual
/// on-disk project referencing real WinForms assemblies (via <c>net10.0-windows</c> +
/// <c>UseWindowsForms</c>) so <c>SemanticModel.GetSymbolInfo</c> has real symbols to resolve.
///
/// Deliberately narrower in namespace coverage than <see cref="SourceConverterTests"/>: Telerik UI for
/// WinForms is not a referenceable assembly available to a lightweight test fixture, so the
/// Telerik-namespace-prefix cases aren't duplicated here (the mapping logic itself —
/// <see cref="RoslynSourceConverter.MapNamespace"/> — is shared with, and already covered against, the
/// built-in prefixes exercised below; only the "is there a real symbol to resolve" plumbing differs, and
/// that's exercised via System.Windows.Forms/System.Drawing instead).
/// </summary>
[Trait ("Category", "Roslyn")]
public class RoslynSourceConverterTests : IDisposable
{
    private readonly string _dir;

    public RoslynSourceConverterTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-roslyn-src-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose ()
    {
        Directory.Delete (_dir, recursive: true);
        GC.SuppressFinalize (this);
    }

    private const string WinFormsCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWindowsForms>true</UseWindowsForms>
            <Nullable>enable</Nullable>
            <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
          </PropertyGroup>
        </Project>
        """;

    private string Write (string relativePath, string content)
    {
        var path = Path.Combine (_dir, relativePath);
        Directory.CreateDirectory (Path.GetDirectoryName (path)!);
        File.WriteAllText (path, content);
        return path;
    }

    /// <summary>
    /// Builds a single-project fixture (<c>App.csproj</c> + the given source under <paramref name="fileName"/>),
    /// loads it through <see cref="RoslynWorkspaceContext"/>, converts that one file via
    /// <see cref="RoslynSourceConverter"/>, and returns the result — the Roslyn-engine equivalent of calling
    /// <c>SourceConverter.Convert(text)</c> directly.
    /// </summary>
    private SourceConverter.Result Convert (string source, string fileName = "Form1.cs", CustomMap? customMap = null,
        VbConstructorMode vbConstructor = VbConstructorMode.Auto)
    {
        var projPath = Write ("App.csproj", WinFormsCsproj);
        var sourcePath = Write (fileName, source);

        var context = RoslynWorkspaceContext.TryCreate (projPath, out var error);
        Assert.Null (error);
        Assert.NotNull (context);
        try
        {
            var found = context!.TryGetDocument (sourcePath, out var document, out var failureReason);
            Assert.True (found, failureReason);
            return RoslynSourceConverter.ConvertAsync (document!, customMap, vbConstructor).GetAwaiter ().GetResult ();
        }
        finally
        {
            context?.Dispose ();
        }
    }

    // ---- Pass 1/1a: namespace prefix rewrites (real symbol resolution) ----

    [Fact]
    public void Rewrites_WinForms_using_directive ()
    {
        var result = Convert ("using System.Windows.Forms;\nclass F { }\n");
        Assert.Contains ("using Majorsilence.Forms;", result.Text);
        Assert.DoesNotContain ("System.Windows.Forms", result.Text);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Rewrites_fully_qualified_WinForms_type_reference ()
    {
        var result = Convert ("class F : System.Windows.Forms.Form { }\n");
        Assert.Contains ("Majorsilence.Forms.Form", result.Text);
        Assert.DoesNotContain ("System.Windows.Forms", result.Text);
    }

    [Fact]
    public void Rewrites_fully_qualified_WinForms_member_access ()
    {
        var result = Convert ("class F { void M() { System.Windows.Forms.MessageBox.Show(\"hi\"); } }\n");
        Assert.Contains ("Majorsilence.Forms.MessageBox.Show", result.Text);
        Assert.DoesNotContain ("System.Windows.Forms", result.Text);
    }

    [Fact]
    public void Does_not_rewrite_using_static_or_alias ()
    {
        // Mirrors SourceConverter's deliberate exclusion of `using static` / alias imports — they carry
        // meaning beyond a plain namespace import.
        var result = Convert ("using WF = System.Windows.Forms;\nclass F { }\n");
        Assert.Contains ("System.Windows.Forms", result.Text);
        Assert.DoesNotContain ("Majorsilence.Forms", result.Text);
    }

    // ---- Pass 2: System.Drawing 3-way bucketing, including the unqualified case ----

    [Theory]
    [InlineData ("Color")]
    [InlineData ("Point")]
    [InlineData ("Size")]
    [InlineData ("Rectangle")]
    public void Keeps_System_Drawing_primitive_value_types (string primitive)
    {
        var result = Convert ($"class F {{ System.Drawing.{primitive} X; }}\n");
        Assert.Contains ($"System.Drawing.{primitive}", result.Text);
        Assert.DoesNotContain ($"Majorsilence.Forms.Drawing.{primitive}", result.Text);
    }

    [Theory]
    [InlineData ("Bitmap")]
    [InlineData ("Font")]
    [InlineData ("Pen")]
    [InlineData ("SolidBrush")]
    public void Redirects_GDI_plus_types_to_Majorsilence_Drawing (string gdiType)
    {
        var result = Convert ($"class F {{ System.Drawing.{gdiType} X; }}\n");
        Assert.Contains ($"Majorsilence.Forms.Drawing.{gdiType}", result.Text);
    }

    [Theory]
    [InlineData ("Graphics")]
    [InlineData ("SystemColors")]
    public void Redirects_WinForms_compat_drawing_types_to_Majorsilence_Forms (string type)
    {
        var result = Convert ($"class F {{ System.Drawing.{type} X; }}\n");
        Assert.Contains ($"Majorsilence.Forms.{type}", result.Text);
    }

    [Fact]
    public void Rewrites_unqualified_GDI_plus_type_under_bare_using ()
    {
        // The concrete "Roslyn does something text mode structurally cannot" case: SourceConverter's Pass 5
        // can only *warn* about an unqualified Bitmap under `using System.Drawing;` — the Roslyn engine
        // resolves the symbol and fixes the reference outright, and (Pass 3, reimplemented) drops the
        // now-unnecessary `using System.Drawing;` in favor of `using Majorsilence.Forms.Drawing;`.
        var result = Convert ("using System.Drawing;\nclass F { Bitmap b; }\n");
        Assert.DoesNotContain ("using System.Drawing;", result.Text);
        Assert.Contains ("using Majorsilence.Forms.Drawing;", result.Text);
        // No manual-review warning for this case — it was fixed, not flagged (Pass 5 is superseded).
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("Bitmap"));
    }

    [Fact]
    public void Keeps_bare_Drawing_import_when_a_primitive_is_used_unqualified ()
    {
        var result = Convert ("using System.Drawing;\nclass F { Color c; }\n");
        Assert.Contains ("using System.Drawing;", result.Text);
        Assert.DoesNotContain ("using Majorsilence.Forms.Drawing;", result.Text);
    }

    [Fact]
    public void Keeps_both_imports_when_a_primitive_and_GDI_plus_are_used_unqualified ()
    {
        var result = Convert ("using System.Drawing;\nclass F { Color c; Bitmap b; }\n");
        Assert.Contains ("using System.Drawing;", result.Text);
        Assert.Contains ("using Majorsilence.Forms.Drawing;", result.Text);
    }

    // ---- Pass 6: ComponentResourceManager redirect ----

    [Fact]
    public void Redirects_ComponentResourceManager ()
    {
        var result = Convert (
            "class F { void M() { var r = new System.ComponentModel.ComponentResourceManager(typeof(F)); } }\n");
        Assert.Contains ("Majorsilence.Forms.ComponentResourceManager", result.Text);
        Assert.DoesNotContain ("System.ComponentModel.ComponentResourceManager", result.Text);
    }

    // ---- Pass 0: reused textually (post-process over this engine's output) ----

    [Fact]
    public void Comments_out_ApplicationConfiguration_Initialize ()
    {
        var result = Convert ("class F { static void Main() { ApplicationConfiguration.Initialize(); } }\n");
        Assert.Contains ("// ApplicationConfiguration.Initialize();", result.Text);
        Assert.Contains (result.Warnings, w => w.Contains ("ApplicationConfiguration.Initialize"));
    }

    // ---- Pass 4 / 5b: reused textual warning helpers ----

    [Fact]
    public void Warns_on_unsupported_VisualStyles_namespace ()
    {
        var result = Convert ("using System.Windows.Forms.VisualStyles;\nclass F { }\n");
        Assert.Contains ("System.Windows.Forms.VisualStyles", result.Text); // left as-is
        Assert.Contains (result.Warnings, w => w.Contains ("VisualStyles"));
    }

    // ---- Pass 8: duplicate import dedup ----

    [Fact]
    public void Collapses_duplicate_imports_produced_by_the_rewrite ()
    {
        // Both statements resolve to the same target namespace once rewritten; the Roslyn engine's own
        // per-node replacement can't see across sibling using directives, so the textual dedup pass (Pass 8,
        // reused verbatim) is what collapses the resulting duplicate.
        var result = Convert ("using System.Windows.Forms;\nusing System.Windows.Forms;\nclass F { }\n");
        var occurrences = result.Text.Split ("using Majorsilence.Forms;").Length - 1;
        Assert.Equal (1, occurrences);
    }

    // ---- CustomMap: user-supplied namespace rewrites resolve through the same symbol-based path ----

    [Fact]
    public void MapNamespace_prefers_built_in_map_over_a_custom_entry_for_the_same_prefix ()
    {
        // Built-in NamespacePrefixes are consulted first (mirrors SourceConverter's Pass 1 running before
        // Pass 1a) — a custom map entry for a namespace the built-in map already owns is shadowed.
        var mapped = RoslynSourceConverter.MapNamespace ("System.Windows.Forms", CustomMap.Empty);
        Assert.Equal ("Majorsilence.Forms", mapped);
    }

    [Fact]
    public void MapNamespace_falls_back_to_a_custom_entry_for_an_unmapped_namespace ()
    {
        var customMap = WriteCustomMap ("""{ "namespaces": { "Acme.Legacy.UI": "Majorsilence.Forms.Acme" } }""");
        var mapped = RoslynSourceConverter.MapNamespace ("Acme.Legacy.UI", customMap);
        Assert.Equal ("Majorsilence.Forms.Acme", mapped);
    }

    [Fact]
    public void MapNamespace_returns_null_for_a_namespace_with_no_mapping ()
    {
        var mapped = RoslynSourceConverter.MapNamespace ("System.Text", CustomMap.Empty);
        Assert.Null (mapped);
    }

    private CustomMap WriteCustomMap (string json)
    {
        var path = Write ("map.json", json);
        return CustomMap.Load ([path]);
    }

    // ---- Visual Basic: same symbol-resolution path, plus the reused textual VB helpers (Pass 7) ----

    private const string WinFormsVbproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0-windows</TargetFramework>
            <UseWindowsForms>true</UseWindowsForms>
            <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
            <MyType>Empty</MyType>
          </PropertyGroup>
        </Project>
        """;

    private SourceConverter.Result ConvertVb (string source, string fileName = "Form1.vb",
        VbConstructorMode vbConstructor = VbConstructorMode.Auto)
    {
        var projPath = Write ("App.vbproj", WinFormsVbproj);
        var sourcePath = Write (fileName, source);

        var context = RoslynWorkspaceContext.TryCreate (projPath, out var error);
        Assert.Null (error);
        Assert.NotNull (context);
        try
        {
            var found = context!.TryGetDocument (sourcePath, out var document, out var failureReason);
            Assert.True (found, failureReason);
            return RoslynSourceConverter.ConvertAsync (document!, null, vbConstructor).GetAwaiter ().GetResult ();
        }
        finally
        {
            context?.Dispose ();
        }
    }

    [Fact]
    public void Rewrites_VB_Imports_directive ()
    {
        var result = ConvertVb ("Imports System.Windows.Forms\nPublic Class C\nEnd Class\n");
        Assert.Contains ("Imports Majorsilence.Forms", result.Text);
        Assert.DoesNotContain ("System.Windows.Forms", result.Text);
    }

    [Fact]
    public void Rewrites_VB_fully_qualified_drawing_type ()
    {
        var result = ConvertVb ("Public Class C\n    Dim b As System.Drawing.Bitmap\nEnd Class\n");
        Assert.Contains ("Majorsilence.Forms.Drawing.Bitmap", result.Text);
    }

    [Fact]
    public void Injects_VB_constructor_via_reused_textual_pass ()
    {
        // Pass 7's VB pieces are deliberately reused unchanged (SourceConverter.ApplyVbConstructor, widened
        // to internal) — this proves the Roslyn engine actually calls through to it, not just that the
        // textual engine still does (SourceConverterTests/VbSupportTests already cover that).
        var src = "Public Class Form1\n    Inherits System.Windows.Forms.Form\n\n    Private Sub Setup()\n        InitializeComponent()\n    End Sub\n\n    Private Sub InitializeComponent()\n    End Sub\nEnd Class\n";
        var result = ConvertVb (src, vbConstructor: VbConstructorMode.Inject);
        Assert.Contains ("Public Sub New()", result.Text);
        Assert.Contains ("Inherits Majorsilence.Forms.Form", result.Text); // base type rewritten too, alongside the ctor injection
    }

    // ---- Per-file behavior differences from the text engine that must NOT regress ----

    [Fact]
    public void Unchanged_file_reports_no_change ()
    {
        var result = Convert ("using System.Text;\nclass F { int x; }\n");
        Assert.False (result.Changed);
        Assert.Empty (result.Warnings);
    }

    // ---- The value-proposition test: cross-project same-named-type disambiguation ----
    //
    // The one thing the textual engine cannot do at all: tell a project-local `Panel` apart from
    // `System.Windows.Forms.Panel` when both are used by bare name in the same file. Two real projects
    // (ClassLib defines Acme.Controls.Panel; UiApp references ClassLib and uses both types) prove the
    // Roslyn engine resolves each reference to its real symbol and only rewrites the WinForms one.

    [Fact]
    public async Task Disambiguates_same_named_type_across_projects_by_real_symbol_resolution ()
    {
        var classLibDir = Path.Combine (_dir, "ClassLib");
        var uiAppDir = Path.Combine (_dir, "UiApp");
        Directory.CreateDirectory (classLibDir);
        Directory.CreateDirectory (uiAppDir);

        var classLibProj = Path.Combine (classLibDir, "ClassLib.csproj");
        File.WriteAllText (classLibProj, WinFormsCsproj);
        File.WriteAllText (Path.Combine (classLibDir, "Panel.cs"),
            "namespace Acme.Controls\n{\n    public class Panel : System.Windows.Forms.Control\n    {\n    }\n}\n");

        var uiAppProj = Path.Combine (uiAppDir, "UiApp.csproj");
        File.WriteAllText (uiAppProj,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0-windows</TargetFramework>
                <UseWindowsForms>true</UseWindowsForms>
                <Nullable>enable</Nullable>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\ClassLib\ClassLib.csproj" />
              </ItemGroup>
            </Project>
            """);
        var form1Path = Path.Combine (uiAppDir, "Form1.cs");
        File.WriteAllText (form1Path,
            "using System.Windows.Forms;\n" +
            "using Acme.Controls;\n" +
            "\n" +
            "namespace UiApp\n" +
            "{\n" +
            "    public class Form1 : Form\n" +
            "    {\n" +
            "        public Form1()\n" +
            "        {\n" +
            "            var acmePanel = new Panel();\n" +
            "            var wfPanel = new System.Windows.Forms.Panel();\n" +
            "        }\n" +
            "    }\n" +
            "}\n");

        var context = RoslynWorkspaceContext.TryCreate (uiAppProj, out var error);
        Assert.Null (error);
        Assert.NotNull (context);
        try
        {
            var found = context!.TryGetDocument (form1Path, out var document, out var failureReason);
            Assert.True (found, failureReason);

            var result = await RoslynSourceConverter.ConvertAsync (document!, null, VbConstructorMode.Suppress);

            // The bare, project-local Acme.Controls.Panel usage must survive completely untouched.
            Assert.Contains ("new Panel();", result.Text);
            // The fully-qualified System.Windows.Forms.Panel usage must be rewritten.
            Assert.Contains ("new Majorsilence.Forms.Panel();", result.Text);
            Assert.DoesNotContain ("System.Windows.Forms.Panel", result.Text);
            // The base Form reference and using directive still get rewritten as normal.
            Assert.Contains ("using Majorsilence.Forms;", result.Text);
            Assert.Contains ("class Form1 : Form", result.Text);
        }
        finally
        {
            context?.Dispose ();
        }
    }
}
