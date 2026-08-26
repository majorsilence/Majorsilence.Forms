using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The headline number of the behavioural audit, and the one worth watching over time: a third of this
// assembly's public settable auto-properties store a value that nothing in the assembly ever reads
// back.
//
// The shape that makes this dangerous is not simple inertness -- it is that the WinForms name is often
// the decoy. The renderer reads a private twin while the WinForms-named property sits beside it
// collecting values nobody consumes: column.DefaultCellStyleAlignment vs DefaultCellStyle.Alignment,
// ShowDropdownGlyph vs ShowPlusMinus, INDENT_SIZE vs Indent, ScrollbarAlwaysVisible vs
// ScrollAlwaysVisible. Migrated code sets the WinForms name, which is precisely the one that does
// nothing, and the only symptom is that the setting appears to be ignored.
//
// See docs/behaviour-gap-plan.md (RC-7) and docs/behaviour-gap/stored-only-properties.txt, the source
// scan this gate replaces.
//
// IMPORTANT -- this baseline is a list of facts, not a to-do list. Plenty of entries are legitimately
// inert and should stay: `Tag` is application storage by definition, and the Win32 shell extras
// (FileDialog.ClientGuid and friends) have no portable meaning. When an entry is deliberate, say so
// beside it in the file rather than deleting it.
//
// SCOPE -- and the thing not to misread. This gate answers "does anything in the assembly read this
// value", which is a strictly narrower question than "does this property do anything". Absence from
// the baseline is NOT a certificate that the property works. Three real examples, all of them audit
// findings that this gate deliberately does not catch:
//
//   * ListView.View is read -- by ListViewParity.cs:599, choosing between the large and small image
//     list. The renderer still ignores it, so every item draws as a large-icon tile (LST-01).
//   * TextBox.WordWrap and CharacterCasing are read -- by ToolStripTextBox's forwarding accessors,
//     which are themselves consumed by nothing that draws (TXT-11, TXT-12).
//   * TextBox.AcceptsReturn is read -- inside a key handler that nothing calls, because the whole
//     ProcessDialogKey chain has no dispatcher (TXT-09, RC-1).
//
// Each is "read by code that is itself inert", which needs transitive reachability rather than a
// single-hop scan. Catching those is a worthwhile follow-up; until then the finding files in
// docs/behaviour-gap/ are where that class lives, and this gate is the mechanical floor beneath them.
//
// Also auto-properties only: a property with hand-written accessors may compute, forward or validate,
// so "its backing field" is not a well-defined question -- another reason the real figure is higher
// than this gate can see.
//
// Regenerate with MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1.
public class StoredOnlyPropertyBaselineTests
{
    [Fact]
    public void NoNewStoredOnlyProperties ()
    {
        var actual = StubSurfaceScanner.ScanStoredOnlyProperties (typeof (Control).Assembly.Location, out var examined);
        var baselinePath = StubSurfaceScanner.LocateBaseline (StubSurfaceScanner.StoredOnlyPropertyBaselineFileName);

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_STORED_ONLY_BASELINE") == "1") {
            StubSurfaceScanner.WriteBaseline (baselinePath, [
                "# Public settable auto-properties in Majorsilence.Forms that nothing in the assembly ever",
                "# reads the value of -- set by application code, readable back, consumed by no code here.",
                "# A property counts as consumed if its getter is called OR its backing field is read",
                "# somewhere other than that getter. See StoredOnlyPropertyBaselineTests and",
                "# docs/behaviour-gap-plan.md (RC-7).",
                "#",
                $"# {actual.Count} of {examined} public settable auto-properties "
                    + $"({(examined == 0 ? 0 : 100 * actual.Count / examined)}%).",
                "#",
                "# This is a list of facts, not a to-do list. Some entries are legitimately inert (`Tag` is",
                "# app storage; the Win32 shell extras have no portable meaning) -- when that is the case,",
                "# say so beside the entry rather than removing it.",
                "#",
                "# Absence from this file does NOT mean the property works: a property read only by code",
                "# that is itself inert counts as consumed here. ListView.View, TextBox.WordWrap and",
                "# TextBox.AcceptsReturn are all absent and all broken -- see StoredOnlyPropertyBaselineTests",
                "# for why, and docs/behaviour-gap/ for that class of finding.",
                "#",
                "# Auto-properties only: a property with hand-written accessors is invisible here, so the",
                "# real figure is higher.",
                "# Regenerate with MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1.",
            ], actual);
            return;
        }

        var baseline = StubSurfaceScanner.ReadBaseline (baselinePath)
            // Entries may carry a trailing "-- reason" note explaining a deliberate one.
            .Select (l => l.Split (" --", StringSplitOptions.None)[0].Trim ())
            .ToList ();

        var added = actual.Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New stored-only propert(ies) -- settable, readable, and consumed by nothing. Wire each to the\n" +
            "code that should honour it, or -- if it is deliberately inert -- regenerate this baseline with\n" +
            "MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1 and note the reason beside the entry:\n  "
            + string.Join ("\n  ", added));

        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (wired up or renamed). Regenerate the\n" +
            "baseline with MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
