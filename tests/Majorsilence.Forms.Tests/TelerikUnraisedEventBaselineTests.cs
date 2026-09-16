using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// UnraisedEventBaselineTests, pointed at Majorsilence.Forms.Telerik, in its own baseline file so a
// core regression and a Telerik regression stay distinguishable.
//
// The scan is stricter than the core one in the two ways this layer needs.
//
// Trap 3 from issue #91: the core gate calls an event raised as soon as some method other than the
// add/remove pair reads the backing field, which a `protected virtual OnXxx` containing `Xxx?.Invoke`
// always does -- it never asks whether anything reaches the raiser. Three RadTreeView events are
// exactly that shape, and the core scan would have reported all three as working.
//
// Second, most of RadGridView's events are written out by hand over a private `_name` delegate field.
// Those have no compiler-generated field named after the event, and their accessors are not empty, so
// before this gate they were examined by neither event scan -- a hole big enough to hide the layer's
// most-subscribed events in. Recovering the store from the adder brings them under the gate; they all
// turn out to be genuinely raised, which is the answer that was worth having and is the reason the
// grep-derived list in #176 names seven events that this test says are fine.
//
// Regenerate with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1 -- the same switch as core, so one run
// rewrites both files.
public class TelerikUnraisedEventBaselineTests
{
    [Fact]
    public void NoNewUnraisedEvents ()
    {
        var assembly = typeof (Majorsilence.Forms.Telerik.RadGridView).Assembly.Location;
        var actual = StubSurfaceScanner.ScanUnraisedEventsDeep (assembly, out var examined);
        var baselinePath = StubSurfaceScanner.LocateBaseline (StubSurfaceScanner.TelerikUnraisedEventBaselineFileName);

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE") == "1") {
            var subscribable = StubSurfaceScanner.CountSubscribableEvents (assembly);
            var singleHop = StubSurfaceScanner.ScanUnraisedEvents (assembly);

            StubSurfaceScanner.WriteBaseline (baselinePath, [
                "# Field-backed events in Majorsilence.Forms.Telerik that nothing live ever raises -- a handler",
                "# really is stored, and never called. Separate from UnraisedEventBaseline.txt so a core",
                "# regression and a Telerik regression stay distinguishable. See",
                "# TelerikUnraisedEventBaselineTests and issue #176 (step 1).",
                "#",
                "# The entries below ARE the count; the denominators are not recorded here (see the core",
                "# baseline for why; dated snapshots live in docs/behaviour-gap-plan.md). Of the events a",
                "# consumer can subscribe to, the rest are",
                "# either inert -- see TelerikInertEventBaseline.txt -- or aliases forwarding to another event).",
                "#",
                "# NOT comparable with the core file's number: this is a stricter scan, in two ways.",
                "#",
                "#  * Trap 3 from #91 -- an `OnXxx` raiser holding `Xxx?.Invoke` that nothing ever calls. The",
                "#    core scan looks for the field read and stops; it does not ask whether any call path",
                "#    reaches the raiser. Over this assembly it finds fewer than this one does, and the",
                "#    difference is precisely that shape: RadTreeView.NodeFormatting, NodeCheckedChanged and",
                "#    NodeCheckedChanging each have a correct `protected internal virtual OnNodeXxx` that no",
                "#    code in the assembly calls.",
                "#  * Events written by hand over a private `_name` delegate field. The core scan only",
                "#    recognises the compiler-generated backing field, which carries the event's own name, so",
                "#    this layer's hand-written pairs -- most of RadGridView's grid events -- were examined by",
                "#    no gate at all: not inert, not field-like. The store is recovered from the adder instead",
                "#    (it has to load the field to combine into it), which brings them under the gate. All of",
                "#    them turn out to be raised, which is why seven events the #176 grep lists as dead are",
                "#    absent from this file: ChildViewExpanding, CommandCellClick, CurrentColumnChanged,",
                "#    CurrentRowChanged, RowFormatting, SelectedPageChanged and ViewCellFormatting are wired.",
                "#",
                "# Not covered: an event raised only from code that runs but achieves nothing (that needs",
                "# value-level dataflow, not reachability), and an event whose accessors forward to another",
                "# type's event -- an alias, where the aliased event is the one examined.",
                "#",
                "# Shrinking this list is the goal, and it is usually a one-line fix: for most entries the",
                "# trigger point already exists and simply is not called.",
                "# Regenerate with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1.",
            ], actual);
            return;
        }

        var baseline = StubSurfaceScanner.ReadBaseline (baselinePath);

        var added = actual.Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New Telerik event(s) that are declared and never raised by anything that runs. Raise them where\n" +
            "Telerik does, or -- if the omission is deliberate -- record it in COMPATIBILITY_MATRIX.md and\n" +
            "regenerate this baseline with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1:\n  "
            + string.Join ("\n  ", added));

        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (raised now, or renamed). Regenerate\n" +
            "the baseline with MAJORSILENCE_WRITE_UNRAISED_EVENT_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
