using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms detaches a control from its parent when it is disposed, and a great deal of ported code
    // relies on it: the standard way to swap a page or a panel is to dispose the old control and add
    // the new one, never removing the old one explicitly. Majorsilence.Forms used to leave the disposed
    // control parented, so it stayed in the collection and -- being docked -- went on filling its
    // container. Found in a control library's demo shell, where the first page opened was the only one
    // that ever showed: every later navigation added its page behind the dead one.
    public class ControlDisposeUnparentsTests
    {
        [Fact]
        public void Disposing_a_child_removes_it_from_its_parent ()
        {
            using var parent = new Panel ();
            var child = new Panel ();
            parent.Controls.Add (child);

            child.Dispose ();

            Assert.Empty (parent.Controls);
            Assert.DoesNotContain (child, parent.Controls);
        }

        [Fact]
        public void Disposing_a_child_clears_its_Parent ()
        {
            using var parent = new Panel ();
            var child = new Panel ();
            parent.Controls.Add (child);

            child.Dispose ();

            Assert.Null (child.Parent);
        }

        [Fact]
        public void Disposing_a_docked_page_lets_the_next_one_take_over ()
        {
            // The exact navigation shape that failed: dispose the old page, add the new one.
            using var host = new Panel { Bounds = new Rectangle (0, 0, 400, 300) };

            var first = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add (first);
            first.Dispose ();

            var second = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add (second);

            Assert.Single (host.Controls);
            Assert.Same (second, host.Controls[0]);
        }

        [Fact]
        public void Disposing_a_parent_still_disposes_its_children ()
        {
            var parent = new Panel ();
            var first = new Panel ();
            var second = new Panel ();
            parent.Controls.Add (first);
            parent.Controls.Add (second);

            // Children detach themselves as they go, so the disposal walk has to iterate a snapshot --
            // otherwise the second child is skipped.
            parent.Dispose ();

            Assert.True (first.IsDisposed);
            Assert.True (second.IsDisposed);
        }

        [Fact]
        public void Disposing_a_control_with_implicit_chrome_does_not_throw ()
        {
            // Implicit chrome lives in a separate list that Remove does not touch and is owned by the
            // parent, so it must not try to detach itself on the way down.
            var updown = new NumericUpDown ();
            updown.Dispose ();

            Assert.True (updown.IsDisposed);
        }

        [Fact]
        public void Disposing_an_unparented_control_is_still_fine ()
        {
            var orphan = new Panel ();
            orphan.Dispose ();

            Assert.True (orphan.IsDisposed);
            Assert.Null (orphan.Parent);
        }
    }
}
