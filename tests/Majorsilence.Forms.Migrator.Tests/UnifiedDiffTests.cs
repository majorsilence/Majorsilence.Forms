using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class UnifiedDiffTests
{
    [Fact]
    public void Emits_file_headers ()
    {
        var diff = UnifiedDiff.Build ("a\n", "b\n", "Form1.cs");
        Assert.Contains ("--- a/Form1.cs", diff);
        Assert.Contains ("+++ b/Form1.cs", diff);
    }

    [Fact]
    public void Marks_replaced_line_with_minus_and_plus ()
    {
        var diff = UnifiedDiff.Build (
            "line1\nusing System.Windows.Forms;\nline3\n",
            "line1\nusing Majorsilence.Forms;\nline3\n",
            "F.cs");
        Assert.Contains ("-using System.Windows.Forms;", diff);
        Assert.Contains ("+using Majorsilence.Forms;", diff);
    }

    [Fact]
    public void Keeps_unchanged_context_lines_with_space_prefix ()
    {
        var diff = UnifiedDiff.Build ("keep\nold\n", "keep\nnew\n", "F.cs");
        Assert.Contains (" keep", diff);
    }

    [Fact]
    public void Has_a_hunk_header ()
    {
        var diff = UnifiedDiff.Build ("a\n", "b\n", "F.cs");
        Assert.Contains ("@@ -", diff);
    }

    [Fact]
    public void Pure_insertion_is_marked_with_plus ()
    {
        var diff = UnifiedDiff.Build ("a\nb\n", "a\nINSERTED\nb\n", "F.cs");
        Assert.Contains ("+INSERTED", diff);

        // No deletion lines: ignore the '---' file header and the '@@' hunk header, then assert
        // nothing in the body starts with a single '-'.
        var bodyLines = diff.Split ('\n')
            .Where (l => !l.StartsWith ("---", System.StringComparison.Ordinal)
                      && !l.StartsWith ("@@", System.StringComparison.Ordinal));
        Assert.DoesNotContain (bodyLines, l => l.StartsWith ('-'));
    }

    [Fact]
    public void Separate_changes_produce_separate_hunks ()
    {
        var oldText = string.Join ("\n", Enumerable.Range (1, 20).Select (i => i == 2 ? "X" : i == 18 ? "Y" : $"line{i}"));
        var newText = string.Join ("\n", Enumerable.Range (1, 20).Select (i => i == 2 ? "Xc" : i == 18 ? "Yc" : $"line{i}"));
        var diff = UnifiedDiff.Build (oldText, newText, "F.cs");
        var hunkCount = diff.Split ('\n').Count (l => l.StartsWith ("@@", System.StringComparison.Ordinal));
        Assert.Equal (2, hunkCount); // far-apart edits don't merge into one hunk
    }
}
