using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class SourceConverterTests
{
    [Fact]
    public void Rewrites_WinForms_using_directive ()
    {
        var result = SourceConverter.Convert ("using System.Windows.Forms;\n");
        Assert.Contains ("using Majorsilence.Forms;", result.Text);
        Assert.DoesNotContain ("System.Windows.Forms", result.Text);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Rewrites_fully_qualified_WinForms_reference ()
    {
        var result = SourceConverter.Convert ("System.Windows.Forms.MessageBox.Show(\"hi\");");
        Assert.Contains ("Majorsilence.Forms.MessageBox.Show", result.Text);
    }

    [Theory]
    [InlineData ("Color")]
    [InlineData ("Point")]
    [InlineData ("Size")]
    [InlineData ("Rectangle")]
    [InlineData ("PointF")]
    [InlineData ("SizeF")]
    [InlineData ("RectangleF")]
    public void Keeps_System_Drawing_primitive_value_types (string primitive)
    {
        var src = $"var x = new System.Drawing.{primitive}();";
        var result = SourceConverter.Convert (src);
        Assert.Contains ($"System.Drawing.{primitive}", result.Text);
        Assert.DoesNotContain ($"Majorsilence.Drawing.{primitive}", result.Text);
    }

    [Theory]
    [InlineData ("Bitmap")]
    [InlineData ("Font")]
    [InlineData ("Pen")]
    [InlineData ("SolidBrush")]
    [InlineData ("Icon")]
    [InlineData ("Brushes")]
    [InlineData ("Pens")]
    [InlineData ("TextureBrush")]
    [InlineData ("PathGradientBrush")]
    [InlineData ("SystemIcons")]
    public void Redirects_GDI_plus_types_to_Majorsilence_Drawing (string gdiType)
    {
        var result = SourceConverter.Convert ($"System.Drawing.{gdiType} x;");
        Assert.Contains ($"Majorsilence.Drawing.{gdiType}", result.Text);
    }

    [Fact]
    public void Warns_on_unmapped_GDI_plus_type_and_leaves_it ()
    {
        var result = SourceConverter.Convert ("System.Drawing.Metafile b;");
        Assert.Contains ("System.Drawing.Metafile", result.Text); // left as-is
        Assert.Contains (result.Warnings, w => w.Contains ("System.Drawing.Metafile"));
    }

    [Theory]
    [InlineData ("Graphics")]
    [InlineData ("ContentAlignment")]
    [InlineData ("SystemColors")]
    [InlineData ("SystemFonts")]
    [InlineData ("ColorTranslator")]
    public void Redirects_WinForms_compat_drawing_types_to_Majorsilence_Forms (string type)
    {
        var result = SourceConverter.Convert ($"System.Drawing.{type} x;");
        Assert.Contains ($"Majorsilence.Forms.{type}", result.Text);
        Assert.DoesNotContain (result.Warnings, w => w.Contains (type));
    }

    [Fact]
    public void Rewrites_drawing_sub_namespaces ()
    {
        var result = SourceConverter.Convert ("using System.Drawing.Drawing2D;\nvar m = new System.Drawing.Drawing2D.Matrix();");
        Assert.Contains ("using Majorsilence.Drawing.Drawing2D;", result.Text);
        Assert.Contains ("Majorsilence.Drawing.Drawing2D.Matrix", result.Text);
    }

    [Fact]
    public void Maps_drawing_Printing_to_Majorsilence_Forms_Printing ()
    {
        var result = SourceConverter.Convert ("using System.Drawing.Printing;");
        Assert.Contains ("using Majorsilence.Forms.Printing;", result.Text);
    }

    [Fact]
    public void Removes_unused_bare_Drawing_import ()
    {
        // Nothing from System.Drawing is used here, so the import is dropped entirely.
        var result = SourceConverter.Convert ("using System.Drawing;\nclass C { }\n");
        Assert.DoesNotContain ("using System.Drawing;", result.Text);
        Assert.DoesNotContain ("using Majorsilence.Drawing;", result.Text);
    }

    [Fact]
    public void Keeps_bare_Drawing_import_when_a_primitive_is_used ()
    {
        var result = SourceConverter.Convert ("using System.Drawing;\nColor c;\n");
        Assert.Contains ("using System.Drawing;", result.Text); // Color resolves from System.Drawing
    }

    [Fact]
    public void Replaces_bare_Drawing_import_with_companion_when_only_GDI_plus_is_used ()
    {
        // Bitmap moves to Majorsilence.Drawing and no primitive is used, so the import is swapped.
        var result = SourceConverter.Convert ("using System.Drawing;\nBitmap b;\n");
        Assert.DoesNotContain ("using System.Drawing;", result.Text);
        Assert.Contains ("using Majorsilence.Drawing;", result.Text);
    }

    [Fact]
    public void Keeps_both_imports_when_a_primitive_and_GDI_plus_are_used ()
    {
        var result = SourceConverter.Convert ("using System.Drawing;\nColor c; Bitmap b;\n");
        Assert.Contains ("using System.Drawing;", result.Text);
        Assert.Contains ("using Majorsilence.Drawing;", result.Text);
    }

    [Fact]
    public void Drawing_import_rewrite_is_idempotent ()
    {
        var once = SourceConverter.Convert ("using System.Drawing;\nBitmap b;\n").Text;
        var twice = SourceConverter.Convert (once).Text;
        Assert.Equal (once, twice);
        Assert.Equal (1, CountOccurrences (twice, "using Majorsilence.Drawing;"));
    }

    [Fact]
    public void Comments_out_ApplicationConfiguration_Initialize ()
    {
        var result = SourceConverter.Convert ("ApplicationConfiguration.Initialize();");
        Assert.Contains ("// ApplicationConfiguration.Initialize();", result.Text);
        Assert.Contains (result.Warnings, w => w.Contains ("ApplicationConfiguration.Initialize"));
    }

    [Fact]
    public void Warns_on_unsupported_VisualStyles_namespace ()
    {
        var result = SourceConverter.Convert ("using System.Windows.Forms.VisualStyles;");
        Assert.Contains ("System.Windows.Forms.VisualStyles", result.Text); // left as-is
        Assert.Contains (result.Warnings, w => w.Contains ("VisualStyles"));
    }

    [Fact]
    public void Unsupported_subnamespace_warns_once_not_as_a_leaf_type ()
    {
        // System.Drawing.Design is a namespace, not a type — it must not also be reported as a missing
        // "System.Drawing.Design" leaf type by the drawing-type pass.
        var result = SourceConverter.Convert ("System.Drawing.Design.UITypeEditor e;");
        var hits = result.Warnings.Count (w => w.Contains ("System.Drawing.Design"));
        Assert.Equal (1, hits); // exactly one — was two before the dedup fix
        // The lone warning is the namespace one, not the misleading "leaf type" form.
        Assert.DoesNotContain (result.Warnings, w => w.StartsWith ("'System.Drawing.Design' has no", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_rewrite_unrelated_namespace_suffix ()
    {
        var result = SourceConverter.Convert ("using MyApp.System.Drawing.Extensions;");
        Assert.Contains ("MyApp.System.Drawing.Extensions", result.Text);
        Assert.False (result.Changed);
    }

    [Fact]
    public void Warns_on_unqualified_unmapped_GDI_type_under_drawing_import ()
    {
        var result = SourceConverter.Convert ("using System.Drawing;\nImageAttributes a;");
        Assert.Contains (result.Warnings, w => w.Contains ("ImageAttributes"));
    }

    [Fact]
    public void Does_not_warn_on_unmapped_type_without_drawing_import ()
    {
        // No `using System.Drawing;` — `ImageAttributes` is almost certainly an unrelated identifier.
        var result = SourceConverter.Convert ("var ImageAttributes = 1;");
        Assert.DoesNotContain (result.Warnings, w => w.Contains ("ImageAttributes"));
    }

    [Fact]
    public void Unchanged_file_reports_no_change ()
    {
        var result = SourceConverter.Convert ("using System.Text;\nvar x = 1;");
        Assert.False (result.Changed);
        Assert.Empty (result.Warnings);
    }

    [Theory]
    [InlineData ("Telerik.WinControls.UI")]
    [InlineData ("Telerik.WinControls.Enumerations")]
    public void Rewrites_Telerik_using_directive (string ns)
    {
        var result = SourceConverter.Convert ($"using {ns};\n");
        Assert.Contains ("using Majorsilence.Forms.Telerik;", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
        Assert.True (result.Changed);
    }

    [Fact]
    public void Rewrites_fully_qualified_Telerik_reference ()
    {
        var result = SourceConverter.Convert ("Telerik.WinControls.UI.RadGridView grid;");
        Assert.Contains ("Majorsilence.Forms.Telerik.RadGridView", result.Text);
        Assert.DoesNotContain ("Telerik.WinControls", result.Text);
    }

    [Fact]
    public void Collapses_duplicate_Telerik_usings_to_one ()
    {
        // Both Telerik namespaces map to Majorsilence.Forms.Telerik; the result must not contain the
        // duplicate `using` that would otherwise trigger CS0105.
        var src = "using Telerik.WinControls.UI;\nusing Telerik.WinControls.Enumerations;\n";
        var result = SourceConverter.Convert (src);
        Assert.Equal (1, CountOccurrences (result.Text, "using Majorsilence.Forms.Telerik;"));
    }

    [Fact]
    public void Dedup_keeps_distinct_imports ()
    {
        var src = "using System.Text;\nusing System.Linq;\n";
        var result = SourceConverter.Convert (src);
        Assert.Contains ("using System.Text;", result.Text);
        Assert.Contains ("using System.Linq;", result.Text);
    }

    private static int CountOccurrences (string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf (needle, i, System.StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
