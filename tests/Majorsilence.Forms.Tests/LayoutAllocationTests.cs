using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Anchored layout allocated 340 bytes a child a pass (performance plan F16): a HybridDictionary of pending bounds
    // made for each pass, a Concat iterator each time a container's children were asked for, and a WeakReference in
    // every LayoutEventArgs. These pin what replaced them.
    [Collection ("Headless")]
    public class LayoutAllocationTests
    {
        // Upstream holds the affected component weakly because a control caches layout args while its layout is
        // suspended, and those would otherwise keep a removed child alive. Args are now weak only once cached.
        [Fact]
        public void Cached_layout_args_do_not_keep_a_removed_child_alive ()
        {
            HeadlessRenderer.Use ();
            using var parent = new Panel { Width = 200, Height = 200 };
            var child = Track (parent, out var weak);

            parent.Controls.Remove (child);
            child = null!;
            GC.Collect ();
            GC.WaitForPendingFinalizers ();
            GC.Collect ();

            Assert.False (weak.TryGetTarget (out _));
            parent.ResumeLayout ();
        }

        [MethodImpl (MethodImplOptions.NoInlining)]
        private static Panel Track (Panel parent, out WeakReference<Panel> weak)
        {
            var child = new Panel ();
            parent.Controls.Add (child);
            parent.SuspendLayout ();
            // Caches args naming the child on the suspended parent.
            ((Layout.IArrangedElement)parent).PerformLayout (child, "Bounds");
            weak = new WeakReference<Panel> (child);
            return child;
        }

        [Fact]
        public void Args_passed_to_a_layout_handler_name_the_affected_control ()
        {
            HeadlessRenderer.Use ();
            using var parent = new Panel { Width = 200, Height = 200 };
            var child = new Panel { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            parent.Controls.Add (child);
            Control? affected = null;
            parent.Layout += (_, e) => affected = e.AffectedControl;

            child.Width += 10;

            Assert.Same (child, affected);
        }

        // A container's children, as layout sees them: explicit then implicit, live, and counted and indexed without
        // enumerating.
        [Fact]
        public void A_container_s_children_are_one_live_list ()
        {
            HeadlessRenderer.Use ();
            using var panel = new Panel ();
            var children = ((Layout.IArrangedElement)panel).Children;
            var implicitCount = children.Count ();

            var a = new Button ();
            var b = new Label ();
            panel.Controls.Add (a);
            panel.Controls.Add (b);

            var list = Assert.IsAssignableFrom<System.Collections.Generic.IList<Control>> (children);
            Assert.Equal (implicitCount + 2, list.Count);
            Assert.Same (a, list[0]);
            Assert.Same (b, list[1]);
            Assert.Equal (children.ToList (), Enumerable.Range (0, list.Count).Select (i => list[i]).ToList ());
            Assert.Equal (1, list.IndexOf (b));
            Assert.Throws<NotSupportedException> (() => list.Add (new Button ()));
            Assert.Throws<ArgumentOutOfRangeException> (() => list[list.Count]);
        }

        [Fact]
        public void An_anchored_layout_pass_allocates_little_per_child ()
        {
            HeadlessRenderer.Use ();
            using var container = new Panel { Width = 2000, Height = 2000 };
            container.SuspendLayout ();
            for (var i = 0; i < 500; i++)
                container.Controls.Add (new Panel { Left = i % 200, Top = i / 200 * 20, Width = 100, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right });
            container.ResumeLayout (false);

            // Until the layout code is promoted to optimised JIT code (a couple of hundred passes) objects that never
            // leave a method are still made on the heap, about 900 bytes a child more; the steady state is measured.
            for (var i = 0; i < 400; i++) {
                container.Width = container.Width == 2000 ? 2010 : 2000;
                container.PerformLayout ();
            }

            var before = GC.GetAllocatedBytesForCurrentThread ();
            container.Width = container.Width == 2000 ? 2010 : 2000;
            container.PerformLayout ();
            var perChild = (GC.GetAllocatedBytesForCurrentThread () - before) / 500;

            // A LayoutEventArgs per resized child, and the weak reference a suspended parent's cached args need; it
            // was 340 bytes a child with a dictionary made each pass.
            Assert.InRange (perChild, 0, 160);
        }
    }
}
