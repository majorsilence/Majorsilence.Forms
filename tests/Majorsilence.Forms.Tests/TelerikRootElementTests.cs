using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream, RootElement is declared on RadControl, so every Rad control has one and the designer
    // emits `SomeControl.RootElement.ControlBounds = ...` for any of them it re-serializes. Here the
    // compat layer backs different Rad types with different Majorsilence.Forms bases -- Panel, Control,
    // Form, SplitContainer -- so there is no single base to declare it on, and it was being added a
    // type at a time. Fourteen of twenty-two were missing it, which is not a decision anyone made; it
    // is what ad-hoc accretion looks like.
    //
    // The cost is asymmetric, which is why this is a gate rather than a style note. A RootElement that
    // nothing calls is a few bytes. A missing one is BC30456 at build time, in a designer file the
    // application did not write and cannot easily change -- the consuming project had to conditionally
    // compile the line out to build at all.
    public class TelerikRootElementTests
    {
        // Backed by a control here, but RadElement-derived ITEMS upstream rather than RadControls, so
        // they have no RootElement of their own and inventing one would be surface this layer does not
        // owe anyone. LayoutControlItem and LayoutControlGroup are hosted inside RadLayoutControl;
        // RadPageViewPage derives from RadPageViewElement. Named rather than pattern-matched: an
        // exemption should have to be argued, and the argument should be visible here.
        private static readonly string[] NotControlsUpstream =
            ["LayoutControlItem", "LayoutControlGroup", "RadPageViewPage"];

        public static TheoryData<Type> RadControlTypes ()
        {
            var data = new TheoryData<Type> ();

            foreach (var type in typeof (Majorsilence.Forms.Telerik.RadControl).Assembly.GetTypes ()) {
                if (!type.IsPublic || type.IsAbstract || !IsBackedByAControl (type))
                    continue;
                if (NotControlsUpstream.Contains (type.Name))
                    continue;

                data.Add (type);
            }

            return data;
        }

        private static bool IsBackedByAControl (Type type)
        {
            for (var t = type; t is not null; t = t.BaseType) {
                if (t == typeof (Majorsilence.Forms.Control) || t == typeof (Majorsilence.Forms.WindowBase))
                    return true;
            }

            return false;
        }

        [Theory]
        [MemberData (nameof (RadControlTypes))]
        public void Every_Rad_control_exposes_a_RootElement (Type type)
        {
            var property = type.GetProperty ("RootElement", BindingFlags.Public | BindingFlags.Instance);

            Assert.True (property is not null,
                $"{type.Name} has no RootElement. A designer that re-serializes a form using it will emit "
                + "RootElement.ControlBounds and the consuming project will not compile.");
            Assert.Equal (typeof (Majorsilence.Forms.Telerik.RadElement), property!.PropertyType);
        }

        // The member the designer actually assigns through it, so the property being present is not
        // enough on its own.
        [Fact]
        public void RootElement_carries_the_bounds_a_designer_assigns ()
        {
            var panel = new Majorsilence.Forms.Telerik.RadScrollablePanel ();

            panel.RootElement.ControlBounds = new System.Drawing.Rectangle (0, 0, 200, 100);

            Assert.Equal (new System.Drawing.Rectangle (0, 0, 200, 100), panel.RootElement.ControlBounds);
        }
    }
}
