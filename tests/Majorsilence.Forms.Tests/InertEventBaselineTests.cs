using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// An event written `add { } remove { }` is the worst shape a compatibility stub can take. It compiles,
// it accepts a subscription, it hands back nothing, and it silently drops the delegate -- so the
// handler an application registered is never called and there is no symptom anywhere to grep for. It
// is strictly worse than a missing event, which at least fails at compile time where it is cheap to
// find.
//
// NoOpStubBaselineTests pins the same defect for methods and says in its own remarks why events were
// left out: "Property and event accessors are excluded -- an inert event is a separate (and much
// larger) category than a method that quietly discards its arguments." This is that category.
//
// The behavioural audit (docs/behaviour-gap-plan.md) counted 84 of them, concentrated exactly where
// LOB code subscribes: DataGridView, ListView, TreeView, NotifyIcon, WebBrowser. As with the stub
// baseline, this does not demand they all be implemented -- it pins the set so that adding one is a
// conscious act, and so the number can be watched down rather than drifting up.
//
// Regenerate with MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1.
public class InertEventBaselineTests
{
    [Fact]
    public void NoNewInertEvents ()
    {
        var actual = StubSurfaceScanner.ScanInertEvents (typeof (Control).Assembly.Location);
        var baselinePath = StubSurfaceScanner.LocateBaseline (StubSurfaceScanner.InertEventBaselineFileName);

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_INERT_EVENT_BASELINE") == "1") {
            StubSurfaceScanner.WriteBaseline (baselinePath, [
                "# Events in Majorsilence.Forms declared `add { } remove { }` -- they compile, accept a",
                "# handler and discard it, so the subscription silently never fires. See",
                "# InertEventBaselineTests and docs/behaviour-gap-plan.md (RC-5).",
                "#",
                "# Shrinking this list is the goal. Wiring one up means raising it at the trigger point that",
                "# usually already exists (a property setter, OnMouseDown, OnKeyUp, EndEdit, ListChanged).",
                "# Regenerate with MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1.",
            ], actual);
            return;
        }

        var baseline = StubSurfaceScanner.ReadBaseline (baselinePath);

        var added = actual.Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New inert event(s) -- declared `add { } remove { }`, so any handler is discarded. Raise the\n" +
            "event at its natural trigger point, or -- if the no-op is deliberate -- record it in\n" +
            "COMPATIBILITY_MATRIX.md and regenerate this baseline with\n" +
            "MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", added));

        // Not a failure in itself, but the baseline should shrink as events are wired rather than drift.
        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (wired up or renamed). Regenerate\n" +
            "the baseline with MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
