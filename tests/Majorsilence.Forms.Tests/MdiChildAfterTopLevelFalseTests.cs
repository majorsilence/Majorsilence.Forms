using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A form made non-top-level first and then given an MdiParent is an MDI child, as upstream's. Measured (.NET 10,
    // Windows 11): child.TopLevel = false, then StartPosition = CenterScreen, MdiParent = parent, TopMost = true,
    // SendToBack, BringToFront and Show -- a common "add to main panel" helper -- gave Parent MdiClient, IsMdiChild,
    // one MdiChildren entry, and the child visible in the MDI area. Here it was shown with no host at all, so the
    // form opened nowhere on screen.
    [Collection ("Headless")]
    public class MdiChildAfterTopLevelFalseTests
    {
        private static (Form parent, Form child) Opened (bool topLevelFirst)
        {
            HeadlessRenderer.Use ();
            var parent = new Form { IsMdiContainer = true, ClientSize = new Size (1200, 800) };
            parent.Controls.Add (new TreeView { Dock = DockStyle.Left, Width = 260 });
            parent.Show ();

            var child = new Form { Size = new Size (600, 400) };
            if (topLevelFirst)
                child.TopLevel = false;
            child.StartPosition = FormStartPosition.CenterScreen;
            child.MdiParent = parent;
            child.TopMost = true;
            child.SendToBack ();
            child.BringToFront ();
            child.Show ();
            return (parent, child);
        }

        [Fact]
        public void A_form_made_non_top_level_before_its_MdiParent_is_an_MDI_child ()
        {
            var (parent, child) = Opened (topLevelFirst: true);
            using (parent) {
                Assert.True (child.IsMdiChild);
                Assert.IsType<MdiClient> (child.Parent);
                Assert.Equal (new[] { child }, parent.MdiChildren);
                Assert.True (child.Visible);
            }
        }

        [Fact]
        public void It_is_hosted_where_the_same_child_set_up_the_usual_way_is ()
        {
            var (usual_parent, usual) = Opened (topLevelFirst: false);
            var (parent, child) = Opened (topLevelFirst: true);
            using (usual_parent)
            using (parent) {
                Assert.Equal (usual.Bounds, child.Bounds);
            }
        }

        [Fact]
        public void A_non_top_level_form_with_no_parent_is_still_not_hosted ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { TopLevel = false };

            form.Show ();

            Assert.Null (form.Parent);
            Assert.False (form.IsMdiChild);
        }
    }
}
