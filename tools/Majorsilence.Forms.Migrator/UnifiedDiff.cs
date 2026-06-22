using System.Text;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// A small line-based unified-diff generator so <c>--diff</c> can show reviewers exactly what a
/// conversion changes, not just which files. Uses a standard longest-common-subsequence edit script
/// grouped into hunks with a few lines of surrounding context.
/// </summary>
internal static class UnifiedDiff
{
    public static string Build(string oldText, string newText, string path, int context = 3)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);
        var ops = Diff(oldLines, newLines);

        // Nothing changed (shouldn't happen — callers gate on Changed — but stay safe).
        if (ops.All(o => o.Kind == EditKind.Equal))
            return "";

        var sb = new StringBuilder();
        sb.Append("--- a/").Append(path).Append('\n');
        sb.Append("+++ b/").Append(path).Append('\n');

        foreach (var hunk in GroupIntoHunks(ops, context))
            AppendHunk(sb, hunk);

        return sb.ToString();
    }

    private enum EditKind { Equal, Delete, Insert }

    private readonly record struct Edit(EditKind Kind, string Line);

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    // Classic LCS dynamic-programming table, then backtrack into an edit script.
    private static List<Edit> Diff(string[] a, string[] b)
    {
        var lcs = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ops = new List<Edit>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                ops.Add(new(EditKind.Equal, a[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                ops.Add(new(EditKind.Delete, a[x]));
                x++;
            }
            else
            {
                ops.Add(new(EditKind.Insert, b[y]));
                y++;
            }
        }
        while (x < a.Length) ops.Add(new(EditKind.Delete, a[x++]));
        while (y < b.Length) ops.Add(new(EditKind.Insert, b[y++]));
        return ops;
    }

    private sealed class Hunk
    {
        public int OldStart, OldCount, NewStart, NewCount;
        public readonly List<Edit> Lines = new();
    }

    private static IEnumerable<Hunk> GroupIntoHunks(List<Edit> ops, int context)
    {
        // Indexes of changed ops; each hunk covers changes plus `context` equal lines on each side,
        // merging adjacent groups whose context windows overlap.
        var changed = new List<int>();
        for (var i = 0; i < ops.Count; i++)
            if (ops[i].Kind != EditKind.Equal)
                changed.Add(i);
        if (changed.Count == 0)
            yield break;

        var ranges = new List<(int Start, int End)>();
        var rStart = Math.Max(0, changed[0] - context);
        var rEnd = Math.Min(ops.Count - 1, changed[0] + context);
        for (var k = 1; k < changed.Count; k++)
        {
            var s = Math.Max(0, changed[k] - context);
            if (s <= rEnd + 1)
                rEnd = Math.Min(ops.Count - 1, changed[k] + context);
            else
            {
                ranges.Add((rStart, rEnd));
                rStart = s;
                rEnd = Math.Min(ops.Count - 1, changed[k] + context);
            }
        }
        ranges.Add((rStart, rEnd));

        foreach (var (start, end) in ranges)
        {
            var hunk = new Hunk();
            // 1-based line numbers; count Equal/Delete toward old, Equal/Insert toward new.
            var oldLineNo = 1 + ops.Take(start).Count(o => o.Kind != EditKind.Insert);
            var newLineNo = 1 + ops.Take(start).Count(o => o.Kind != EditKind.Delete);
            hunk.OldStart = oldLineNo;
            hunk.NewStart = newLineNo;
            for (var i = start; i <= end; i++)
            {
                hunk.Lines.Add(ops[i]);
                if (ops[i].Kind != EditKind.Insert) hunk.OldCount++;
                if (ops[i].Kind != EditKind.Delete) hunk.NewCount++;
            }
            yield return hunk;
        }
    }

    private static void AppendHunk(StringBuilder sb, Hunk hunk)
    {
        sb.Append("@@ -").Append(hunk.OldStart).Append(',').Append(hunk.OldCount)
          .Append(" +").Append(hunk.NewStart).Append(',').Append(hunk.NewCount).Append(" @@\n");
        foreach (var edit in hunk.Lines)
        {
            var prefix = edit.Kind switch { EditKind.Delete => '-', EditKind.Insert => '+', _ => ' ' };
            sb.Append(prefix).Append(edit.Line).Append('\n');
        }
    }
}
