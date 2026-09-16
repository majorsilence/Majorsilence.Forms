using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// StoredOnlyPropertyBaselineTests, pointed at Majorsilence.Forms.Telerik -- the assembly with the
// highest stored-only density in the repo and, until now, no instrumentation at all. Its own file
// rather than entries merged into the core baseline, so a core regression and a Telerik regression are
// never confused for each other.
//
// The scan behind it is not the core one. Issue #91 recorded four ways a stored-only baseline reads
// clean over code that does nothing, and this layer is built out of two of them by construction: a
// RadGridView member is usually a hand-written forwarder onto MasterTemplate, so the template's
// auto-property is "read" -- by a getter whose own callers are nobody. ScanStoredOnlyPropertiesDeep
// therefore walks a liveness closure and an escape closure instead of asking one hop; the trap-by-trap
// account is in the baseline file's header, and the mechanism is on StubSurfaceScanner's
// reachability half.
//
// The two scans are kept side by side on purpose. The strict one can only ever find more (every
// reason to call a property consumed still applies, each now has to survive the closures), so the
// difference between them is the size of the blind spot, and the header records both numbers.
//
// Regenerate with MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1 -- the same switch as core, so one run
// rewrites both files.
public class TelerikStoredOnlyPropertyBaselineTests
{
    [Fact]
    public void NoNewStoredOnlyProperties ()
    {
        var assembly = typeof (Majorsilence.Forms.Telerik.RadGridView).Assembly.Location;
        var actual = StubSurfaceScanner.ScanStoredOnlyPropertiesDeep (assembly, out var examined);
        var baselinePath = StubSurfaceScanner.LocateBaseline (StubSurfaceScanner.TelerikStoredOnlyPropertyBaselineFileName);

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_STORED_ONLY_BASELINE") == "1") {
            var singleHop = StubSurfaceScanner.ScanStoredOnlyProperties (assembly, out _);

            StubSurfaceScanner.WriteBaseline (baselinePath, [
                "# Public settable auto-properties in Majorsilence.Forms.Telerik whose stored value never",
                "# reaches live code that does anything with it. Separate from StoredOnlyPropertyBaseline.txt",
                "# on purpose: a core regression and a Telerik regression have to stay distinguishable.",
                "# See TelerikStoredOnlyPropertyBaselineTests and issue #176 (step 1).",
                "#",
                "# The entries below ARE the count; the denominator is not recorded here (see the core",
                "# baseline for why); dated snapshots live in docs/behaviour-gap-plan.md.",
                "#",
                "# NOT comparable with the core file's number: this is a stricter scan. The core gate asks",
                "# one question -- is this backing field loaded anywhere other than its own getter -- and on",
                "# this layer that reads clean over code that does nothing, because nearly every RadGridView",
                "# member is a hand-written forwarder onto MasterTemplate and the template's auto-property is",
                "# duly \"read\", by a getter whose own callers are nobody. Run the core scan over this",
                "# assembly and it finds fewer than the two closures below do. The difference is",
                "# forwarding chains that end in mid-air, e.g. RadElement.ControlBounds ->",
                "# LightVisualElement.ControlBoundingRectangle -> nothing.",
                "#",
                "# The four false-clean traps recorded in #91, and where this scan stands on each:",
                "#",
                "#  1. A ring of stub properties reading each other. ELIMINATED. \"Does this getter's value",
                "#     escape\" is the least fixed point from false, so a cycle never gains a reason to flip.",
                "#  2. A property read only by inert code. MOSTLY ELIMINATED. A reader that is empty-bodied,",
                "#     or that no live call path reaches, does not count. What remains is a reader that runs",
                "#     and is itself pointless -- one whose only effect is to store into another stored-only",
                "#     property, say. That needs value-level dataflow rather than reachability, and is not",
                "#     done here.",
                "#  3. An OnXxx raiser nothing calls. ELIMINATED, though it bites the event baseline rather",
                "#     than this one. Liveness is seeded from public members of public types, overrides,",
                "#     explicit interface implementations and static constructors; a protected virtual that",
                "#     introduces a new slot is deliberately not a seed, so a raiser nobody calls is dead.",
                "#  4. A reader that hands the value straight back out. ELIMINATED for the getter shape --",
                "#     the one this layer is made of -- because a property getter is treated as a relay and",
                "#     the question defers to its callers. NOT eliminated for the copy shape: a Clone or",
                "#     CopyFrom that reads the field into a new instance still counts as a consumer. The one",
                "#     such method here (FilterDescriptor.Clone) goes through MemberwiseClone, which loads no",
                "#     field, so nothing is hidden behind it today. An override of ToString also counts as a",
                "#     consumer, deliberately -- for a list item that is what gets displayed.",
                "#",
                "# Two blind spots shared with the core gate, stated so the number is not read as a ceiling:",
                "# auto-properties only (a property with hand-written accessors has no well-defined backing",
                "# field), and in-assembly reads only (the consumer reading the value back is not a read).",
                "#",
                "# This is a list of facts, not a to-do list. Some entries are legitimately inert -- `Tag` is",
                "# application storage, and an event-args property is set here precisely so a handler outside",
                "# this assembly can read it. When that is the case, say so beside the entry with a trailing",
                "# \" -- reason\" rather than deleting it.",
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
            "New stored-only Telerik propert(ies) -- settable, readable, and reaching no live code that\n" +
            "acts on them. Wire each to the code that should honour it, or -- if it is deliberately inert --\n" +
            "regenerate this baseline with MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1 and note the reason\n" +
            "beside the entry:\n  " + string.Join ("\n  ", added));

        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (wired up or renamed). Regenerate the\n" +
            "baseline with MAJORSILENCE_WRITE_STORED_ONLY_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }
}
