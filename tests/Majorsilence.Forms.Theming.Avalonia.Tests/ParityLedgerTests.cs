using System;
using System.IO;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Theming.Avalonia;
using Xunit;

namespace Majorsilence.Forms.Theming.Avalonia.Tests
{
    // The parity ledger (docs/theming-parity-plan.md, phase 0): every (selector, part, state, property)
    // row and the level it has, committed. A row that gets worse fails the test; one that gets better
    // makes the baseline stale, which fails too until it is rewritten. Either way a change in what
    // Avalonia can express is a recorded act, like the other *Baseline.txt files.
    // Regenerate with MAJORSILENCE_WRITE_THEMING_PARITY_LEDGER=1.
    public class ParityLedgerTests
    {
        private static string Locate ()
        {
            var dir = new DirectoryInfo (AppContext.BaseDirectory);

            while (dir is not null) {
                var candidate = Path.Combine (dir.FullName, "tests", "Majorsilence.Forms.Theming.Avalonia.Tests", "ParityLedger.txt");
                if (File.Exists (candidate) || Directory.Exists (Path.GetDirectoryName (candidate)!))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException ("tests/Majorsilence.Forms.Theming.Avalonia.Tests not found above " + AppContext.BaseDirectory);
        }

        internal static string Render ()
        {
            var sb = new StringBuilder ();
            var rows = AvaloniaThemeSupport.Entries
                .Select (e => (Key: $"{e.Selector}{(e.Part is null ? "" : "::" + e.Part)}{(e.State == Majorsilence.Forms.ThemeCssState.Normal ? "" : ":" + e.State)} {e.Property}", e.Level))
                .OrderBy (r => r.Key, StringComparer.Ordinal)
                .ToList ();

            sb.AppendLine ("# Avalonia theming parity ledger. Rewrite with MAJORSILENCE_WRITE_THEMING_PARITY_LEDGER=1.");
            sb.AppendLine ($"# native={rows.Count (r => r.Level == AvaloniaThemeSupportLevel.Native)} approximate={rows.Count (r => r.Level == AvaloniaThemeSupportLevel.Approximate)} unsupported={rows.Count (r => r.Level == AvaloniaThemeSupportLevel.Unsupported)}");
            foreach (var (key, level) in rows)
                sb.Append (key).Append (" = ").Append (level).Append ('\n');

            return sb.ToString ();
        }

        [Fact]
        public void Ledger_MatchesTheMatrix_AndNoRowGotWorse ()
        {
            var path = Locate ();
            var current = Render ();

            if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_THEMING_PARITY_LEDGER") == "1") {
                File.WriteAllText (path, current);
                return;
            }

            Assert.True (File.Exists (path), "ParityLedger.txt is missing; write it with MAJORSILENCE_WRITE_THEMING_PARITY_LEDGER=1.");

            static System.Collections.Generic.Dictionary<string, string> Parse (string text)
                => text.Replace ("\r\n", "\n").Split ('\n')
                    .Where (l => l.Length > 0 && l[0] != '#')
                    .Select (l => l.Split (" = "))
                    .ToDictionary (p => p[0], p => p[1]);

            var before = Parse (File.ReadAllText (path));
            var after = Parse (current);
            int Rank (string level) => level switch { "Native" => 2, "Approximate" => 1, _ => 0 };

            var worse = after.Where (a => before.TryGetValue (a.Key, out var b) && Rank (a.Value) < Rank (b)).Select (a => $"{a.Key}: {before[a.Key]} -> {a.Value}").ToList ();
            var removed = before.Keys.Except (after.Keys).Select (k => $"{k}: row removed").ToList ();
            Assert.True (worse.Count == 0 && removed.Count == 0, "Rows got worse or disappeared:\n" + string.Join ("\n", worse.Concat (removed)));

            var changed = after.Where (a => !before.TryGetValue (a.Key, out var b) || b != a.Value).Select (a => a.Key).ToList ();
            Assert.True (changed.Count == 0, $"{changed.Count} row(s) changed or were added; rewrite the ledger with MAJORSILENCE_WRITE_THEMING_PARITY_LEDGER=1. First: {string.Join (", ", changed.Take (5))}");
        }
    }
}
