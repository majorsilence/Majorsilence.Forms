using System;
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
// Regenerate with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1.
public class UnraisedEventBaselineTests
{
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

        var baseline = StubSurfaceScanner.ReadBaseline (baselinePath);

        var added = actual.Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New event(s) that are declared and never raised. Raise them where upstream does, or -- if\n" +
            "the omission is deliberate -- record it in COMPATIBILITY_MATRIX.md and regenerate this\n" +
            "baseline with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", added));

        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (raised now, or renamed). Regenerate\n" +
            "the baseline with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
