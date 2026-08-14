using System;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

/// <summary>
/// Where an ambiguity alias (<c>using SystemFonts = Majorsilence.Forms.SystemFonts;</c>) is placed.
/// </summary>
/// <remarks>
/// It is anchored on the file's imports, and a C# <c>using (disposable)</c> <i>statement</i> reads exactly
/// like an import to a line-based matcher: same keyword, no '=' to give it away. Anchoring on one put an
/// alias directive inside a method body, which does not compile — found migrating a component library whose
/// files import through global usings, so a using-statement was the only import-shaped line in the file.
/// </remarks>
public class AliasAnchorTests
{
    // The shape that broke: no plain imports (they are global), aliases already added by earlier passes,
    // and a using-statement in the middle of a method.
    private const string GlobalUsingsFile = """
        using Timer = Majorsilence.Forms.Timer;

        namespace Krypton.Toolkit;

        internal class ViewDrawBadge
        {
            private void Draw ()
            {
                using (topLeftPen)
                {
                    var f = SystemFonts.DefaultFont;
                }
            }
        }
        """;

    [Fact]
    public void An_alias_is_not_placed_inside_a_method_body ()
    {
        var result = SourceConverter.Convert (GlobalUsingsFile);

        var alias = result.Text.Split ('\n').Single (l => l.Contains ("using SystemFonts ="));

        Assert.StartsWith ("using SystemFonts =", alias);          // column 0: file scope, not indented
        Assert.DoesNotContain ("using (topLeftPen)\n    using SystemFonts", result.Text.Replace ("\r", ""));
    }

    [Fact]
    public void The_alias_goes_above_the_namespace_when_the_file_has_only_aliases ()
    {
        var result = SourceConverter.Convert (GlobalUsingsFile);

        var lines = result.Text.Replace ("\r", "").Split ('\n').Select (l => l.Trim ()).ToList ();

        Assert.True (lines.FindIndex (l => l.StartsWith ("using SystemFonts =", StringComparison.Ordinal)) <
                     lines.FindIndex (l => l.StartsWith ("namespace ", StringComparison.Ordinal)),
                     "the alias must precede the namespace declaration");
    }

    [Fact]
    public void A_file_with_no_imports_at_all_still_gets_its_alias ()
    {
        var src = """
            namespace Sample;

            internal class Painter
            {
                public void Paint () => _ = SystemBrushes.Control;
            }
            """;

        var result = SourceConverter.Convert (src);

        Assert.Contains ("using SystemBrushes = Majorsilence.Forms.SystemBrushes;", result.Text);
        Assert.True (result.Text.IndexOf ("using SystemBrushes", StringComparison.Ordinal)
                     < result.Text.IndexOf ("namespace Sample", StringComparison.Ordinal));
    }

    [Fact]
    public void The_alias_still_follows_a_kept_System_Drawing_import ()
    {
        // The original, unchanged behaviour: with a real import present, that is the anchor.
        var src = """
            using System.Drawing;

            namespace Sample;

            internal class Painter
            {
                private Color _c;
                public void Paint () => _ = SystemBrushes.Control;
            }
            """;

        var result = SourceConverter.Convert (src);
        var lines = result.Text.Replace ("\r", "").Split ('\n').ToList ();

        Assert.Equal (lines.FindIndex (l => l.StartsWith ("using System.Drawing;", StringComparison.Ordinal)) + 1,
                      lines.FindIndex (l => l.StartsWith ("using SystemBrushes =", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_using_statement_is_never_treated_as_an_import ()
    {
        // Guards the matcher directly: the statement must not attract anything after it.
        var src = """
            namespace Sample;

            internal class Painter
            {
                public void Paint ()
                {
                    using (pen)
                    {
                        _ = SystemPens.Control;
                    }
                }
            }
            """;

        var result = SourceConverter.Convert (src);

        Assert.DoesNotContain ("        using SystemPens", result.Text);
        Assert.Contains ("using SystemPens = Majorsilence.Forms.SystemPens;", result.Text);
    }
}
