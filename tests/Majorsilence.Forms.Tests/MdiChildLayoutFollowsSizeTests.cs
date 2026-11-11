using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Issue #682: an MDI child laid its controls out in the area it had when shown and kept it -- a
    // Dock = Fill control stayed at its first size through maximize, an MDI parent resize and a
    // ClientSize assignment, while ClientSize reported the new size.
    public class MdiChildLayoutFollowsSizeTests
    {
        private static (Form parent, Form child, Panel fill) ShowChild ()
        {
            var parent = new Form { IsMdiContainer = true, Size = new Size (1200, 800) };
            parent.Show ();

            var child = new Form { MdiParent = parent, ClientSize = new Size (600, 400) };
            var fill = new Panel { Dock = DockStyle.Fill };
            child.Controls.Add (fill);
            child.Show ();
            Application.DoEvents ();
            return (parent, child, fill);
        }

        [Fact]
        public void Fill_control_follows_the_child_when_maximized ()
        {
            var (parent, child, fill) = ShowChild ();
            using var _ = parent;

            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents ();

            Assert.NotEqual (new Size (600, 400), child.ClientSize);
            Assert.Equal (child.ClientSize, child.DisplayRectangle.Size);
            Assert.Equal (child.ClientSize, fill.Size);
        }

        [Fact]
        public void Fill_control_follows_a_maximized_child_when_the_parent_resizes ()
        {
            var (parent, child, fill) = ShowChild ();
            using var _ = parent;
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents ();
            var maximized = child.ClientSize;

            parent.Size = new Size (1000, 700);
            Application.DoEvents ();

            Assert.NotEqual (maximized, child.ClientSize);
            Assert.Equal (child.ClientSize, child.DisplayRectangle.Size);
            Assert.Equal (child.ClientSize, fill.Size);
        }

        [Fact]
        public void Fill_control_follows_ClientSize_in_the_normal_state ()
        {
            var (parent, child, fill) = ShowChild ();
            using var _ = parent;

            child.ClientSize = new Size (500, 300);
            Application.DoEvents ();

            Assert.Equal (new Size (500, 300), child.ClientSize);
            Assert.Equal (child.ClientSize, child.DisplayRectangle.Size);
            Assert.Equal (child.ClientSize, fill.Size);
        }

        [Fact]
        public void Fill_control_follows_restore_after_maximize ()
        {
            var (parent, child, fill) = ShowChild ();
            using var _ = parent;
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents ();

            child.WindowState = FormWindowState.Normal;
            Application.DoEvents ();

            Assert.Equal (child.ClientSize, child.DisplayRectangle.Size);
            Assert.Equal (child.ClientSize, fill.Size);
        }

        [Fact]
        public void Maximized_child_raises_Resize_when_the_parent_resizes ()
        {
            var (parent, child, _) = ShowChild ();
            using var _p = parent;
            child.WindowState = FormWindowState.Maximized;
            Application.DoEvents ();
            var resized = 0;
            child.Resize += (_, _) => resized++;

            parent.Size = new Size (1000, 700);
            Application.DoEvents ();

            Assert.True (resized > 0);
        }

        [Fact]
        public void Fill_control_of_a_panel_hosted_form_follows_the_panel ()
        {
            using var outer = new Form { ClientSize = new Size (800, 600) };
            var host = new Panel { Bounds = new Rectangle (0, 0, 400, 300) };
            outer.Controls.Add (host);
            outer.Show ();

            var inner = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
            var fill = new Panel { Dock = DockStyle.Fill };
            inner.Controls.Add (fill);
            host.Controls.Add (inner);
            inner.Show ();
            Application.DoEvents ();
            var before = fill.Size;

            host.Size = new Size (500, 350);
            Application.DoEvents ();

            Assert.Equal (new Size (400, 300), before);
            Assert.Equal (new Size (500, 350), fill.Size);
        }
    }
}
