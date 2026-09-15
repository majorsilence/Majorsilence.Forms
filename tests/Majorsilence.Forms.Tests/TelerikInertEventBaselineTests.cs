using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// InertEventBaselineTests, pointed at Majorsilence.Forms.Telerik, in its own baseline file so a core
// regression and a Telerik regression stay distinguishable.
//
// This is the one of the three Telerik gates that asks exactly what its core twin asks, and it needs
// no reachability work to be honest: an `add { }` accessor cannot store the delegate, so no amount of
// code elsewhere could raise it. The subscription is dropped on the floor at the moment it is made.
//
// Read it together with TelerikUnraisedEventBaseline.txt -- the two files are the set a consumer can
// subscribe to and never hear from, and issue #176 is about shrinking their union.
//
// Regenerate with MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1 -- the same switch as core, so one run
// rewrites both files.
public class TelerikInertEventBaselineTests
{
    [Fact]
    public void NoNewInertEvents ()
    {
        var assembly = typeof (Majorsilence.Forms.Telerik.RadGridView).Assembly.Location;
        var actual = StubSurfaceScanner.ScanInertEvents (assembly);
        var baselinePath = StubSurfaceScanner.LocateBaseline (StubSurfaceScanner.TelerikInertEventBaselineFileName);

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_INERT_EVENT_BASELINE") == "1") {
            var subscribable = StubSurfaceScanner.CountSubscribableEvents (assembly);
            var unraised = StubSurfaceScanner.ScanUnraisedEventsDeep (assembly, out _);

            StubSurfaceScanner.WriteBaseline (baselinePath, [
                "# Events in Majorsilence.Forms.Telerik declared `add { } remove { }` -- they compile, accept",
                "# a handler and discard it, so the subscription silently never fires. Separate from",
                "# InertEventBaseline.txt so a core regression and a Telerik regression stay distinguishable.",
                "# See TelerikInertEventBaselineTests and issue #176 (step 1).",
                "#",
                $"# {actual.Count} of {subscribable} events a consumer can subscribe to.",
                "#",
                "# None of the #91 false-clean traps apply here, and that is a property of the shape rather",
                "# than of the scan: an empty accessor never stores the delegate, so no reachable raiser could",
                "# exist to be found. Unlike its two companions this gate asks exactly what the core one asks.",
                "#",
                $"# With the {unraised.Count} in TelerikUnraisedEventBaseline.txt that is {actual.Count + unraised.Count} of {subscribable} events that a",
                "# consumer can wire a handler to and never hear from. Shrinking the union is issue #176 step 2;",
                "# the highest-value entries there are the per-row and per-cell formatting events, which is how",
                "# Telerik LOB code colours its grids.",
                "# Regenerate with MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1.",
            ], actual);
            return;
        }

        var baseline = StubSurfaceScanner.ReadBaseline (baselinePath);

        var added = actual.Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New inert Telerik event(s) -- declared `add { } remove { }`, so any handler is discarded. Raise\n" +
            "the event at its natural trigger point, or -- if the no-op is deliberate -- record it in\n" +
            "COMPATIBILITY_MATRIX.md and regenerate this baseline with\n" +
            "MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", added));

        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (wired up or renamed). Regenerate\n" +
            "the baseline with MAJORSILENCE_WRITE_INERT_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
