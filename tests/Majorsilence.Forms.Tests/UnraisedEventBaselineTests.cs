using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The quieter sibling of InertEventBaselineTests. Here the event is a real field-like event -- the
// compiler generated a backing field and a working add/remove pair, so a handler really is stored --
// but nothing in the assembly ever reads that field to invoke it. Subscribing works; the event just
// never happens.
//
// In source these are the `#pragma warning disable CS0067` sites: the compiler noticed, and the
// warning was suppressed instead of the event wired. The audit counted 89 of them
// (docs/behaviour-gap-plan.md, RC-5) and found that in most cases the trigger point already exists --
// a property setter that stores without notifying, an OnMouseDown that routes without raising.
//
// This gate finds them from IL rather than from the pragma, so an event that is unraised for any other
// reason is caught too, and moving the pragma around cannot hide one.
//
// A deliberate exclusion: events with hand-written accessors have no backing field and are not
// examined here. The empty-accessor ones are InertEventBaselineTests' job; the rest forward somewhere
// real.
//
// REACHABILITY (2026-09-21). "Nothing reads the field" was too weak, and it missed the single most
// common shape in this assembly: `protected virtual void OnFoo (EventArgs e) => Foo?.Invoke (...)`
// reads the field, so every event with a conventional raiser counted as raised -- however unreachable
// that raiser was. Upstream calls those from message handling; this layer has no pump, so a raiser
// nothing calls is an event that never fires. A reader now has to be reachable itself, which took the
// baseline from 109 entries to 215. Control.ClientSizeChanged is the clearest of the 106: its raise is
// still in the file, commented out.
//
// The check is ONE HOP -- a raiser called only by other dead code still counts as reachable. That errs
// toward "raised", which keeps working events out of the baseline, the same direction IsCalled already
// errs for virtual accessors. Full transitive reachability is the follow-up.
//
// Regenerate with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1.
public class UnraisedEventBaselineTests
{
    // The reachability rule, pinned at both ends. Without the first assertion the gate silently
    // reverts to its old blindness -- and that blindness is invisible, because the baseline it
    // produces is smaller and a smaller baseline looks like progress.
    [Fact]
    public void AnEventWhoseOnlyReaderIsAnUncalledRaiserCountsAsUnraised ()
    {
        var scanned = StubSurfaceScanner.ScanUnraisedEvents (typeof (Control).Assembly.Location);

        // Control.ChangeUICues: OnChangeUICues is declared, reads the field, and is called by nothing
        // in the assembly. Control.ClientSizeChanged is the same with its raise commented out.
        Assert.Contains ("Majorsilence.Forms.Control.ChangeUICues", scanned.Select (Name));
        Assert.Contains ("Majorsilence.Forms.Control.ClientSizeChanged", scanned.Select (Name));

        // And the other direction, which is what stops the rule being "flag everything with a raiser":
        // ListView.GroupTaskLinkClick has the same shape and its raiser IS called (from the cell-click
        // path, wired in DGV-43's batch), so it must NOT be listed.
        Assert.DoesNotContain ("Majorsilence.Forms.ListView.GroupTaskLinkClick", scanned.Select (Name));
    }

    // The baselines tell the reader to annotate a deliberately-inert entry rather than delete it, and
    // regeneration used to throw every annotation away -- which is why the files contained none.
    [Fact]
    public void RegeneratingABaselineKeepsHandWrittenNotes ()
    {
        var path = Path.Combine (Path.GetTempPath (), $"baseline-notes-{Guid.NewGuid ():N}.txt");

        try {
            File.WriteAllLines (path, [
                "# header",
                "Some.Type.Alpha    -- deliberate: no portable meaning",
                "Some.Type.Beta",
            ]);

            // Beta keeps none, Alpha keeps its note, and Gamma -- new this run -- gets none.
            StubSurfaceScanner.WriteBaseline (path, ["# header"], [
                "Some.Type.Alpha",
                "Some.Type.Beta",
                "Some.Type.Gamma",
            ]);

            var written = File.ReadAllLines (path);

            Assert.Contains ("Some.Type.Alpha    -- deliberate: no portable meaning", written);
            Assert.Contains ("Some.Type.Beta", written);
            Assert.Contains ("Some.Type.Gamma", written);
        } finally {
            File.Delete (path);
        }
    }

    // A note the scanner generates is derived from the current IL, so it wins over a stale hand note
    // about the same entry -- otherwise a carried-over annotation could contradict the scan.
    [Fact]
    public void AGeneratedNoteWinsOverACarriedOverOne ()
    {
        var path = Path.Combine (Path.GetTempPath (), $"baseline-notes-{Guid.NewGuid ():N}.txt");

        try {
            File.WriteAllLines (path, ["# header", "Some.Type.Alpha    -- an older, hand-written reason"]);

            StubSurfaceScanner.WriteBaseline (path, ["# header"],
                ["Some.Type.Alpha" + StubSurfaceScanner.WrittenMarker]);

            Assert.Contains ("Some.Type.Alpha" + StubSurfaceScanner.WrittenMarker, File.ReadAllLines (path));
            Assert.DoesNotContain ("an older, hand-written reason", string.Join ("\n", File.ReadAllLines (path)));
        } finally {
            File.Delete (path);
        }
    }

    private static string Name (string line)
        => line.Split (" --", StringSplitOptions.None)[0].Trim ();

    [Fact]
    public void NoNewUnraisedEvents ()
    {
        var actual = StubSurfaceScanner.ScanUnraisedEvents (typeof (Control).Assembly.Location);
        var baselinePath = StubSurfaceScanner.LocateBaseline (StubSurfaceScanner.UnraisedEventBaselineFileName);

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE") == "1") {
            StubSurfaceScanner.WriteBaseline (baselinePath, [
                "# Field-backed events in Majorsilence.Forms that nothing ever raises -- a handler is stored",
                "# and never called. In source these are the `#pragma warning disable CS0067` sites; this",
                "# baseline finds them from IL, so the pragma cannot hide one. See",
                "# UnraisedEventBaselineTests and docs/behaviour-gap-plan.md (RC-5).",
                "#",
                "# Shrinking this list is the goal, and it is usually a one-line fix: the audit found that",
                "# most of these have a trigger point that already exists in the framework.",
                "# Regenerate with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1.",
            ], actual);
            return;
        }

        // Notes are stripped before comparing, as StoredOnlyPropertyBaselineTests already does. This
        // gate compared raw lines, so the moment anyone followed the file's own instruction to
        // annotate an entry, that entry read as removed AND re-added -- the convention was broken in
        // three separate places at once: the header invited notes, WriteBaseline threw them away, and
        // this comparison could not read them.
        var baseline = StubSurfaceScanner.ReadBaseline (baselinePath).Select (Name).ToList ();

        var added = actual.Select (Name).Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual.Select (Name)).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New event(s) that are declared and never raised. Raise them where upstream does, or -- if\n" +
            "the omission is deliberate -- record it in COMPATIBILITY_MATRIX.md and regenerate this\n" +
            "baseline with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", added));

        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (raised now, or renamed). Regenerate\n" +
            "the baseline with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
