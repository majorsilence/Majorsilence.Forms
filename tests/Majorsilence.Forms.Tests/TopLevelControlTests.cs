using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // SetTopLevel(true) + Visible = true is how WinForms shows a Control as a floating window --
    // ToolStripDropDown does it to itself, and Krypton's entire popup layer (context menus, tooltips,
    // the ribbon's app menu) shows popups this way via ShowWindow(Handle, ...). Here a visible top-level
    // control is hosted in a PopupWindow; these pin that contract. Clicking the ribbon's File tab used
    // to FailFast on Krypton's `popup.IsHandleCreated` assert because nothing implemented this.
    public class TopLevelControlTests
    {
        private sealed class ProbeControl : Control
        {
            public void MakeTopLevel () => SetTopLevel (true);
            public void MakeChildLevel () => SetTopLevel (false);
            public bool TopLevelForTest => GetTopLevel ();
        }

        private static Form OpenOwnerForm ()
        {
            var form = new Form { Width = 300, Height = 200 };
            form.Show ();
            return form;
        }

        [Fact]
        public void A_visible_top_level_control_is_created_and_hosted ()
        {
            using var owner = OpenOwnerForm ();

            using var popup = new ProbeControl ();
            popup.SetBounds (50, 60, 200, 120);
            popup.MakeTopLevel ();
            popup.Visible = true;

            // Krypton asserts IsHandleCreated the moment the popup is shown, so this must hold
            // synchronously -- not after a paint or an event-loop tick.
            Assert.True (popup.IsHandleCreated);
            Assert.True (popup.Created);

            // Hosted: it acquired a parent (the host window's root), and sits at the host's origin
            // with the host taking over the screen position.
            Assert.NotNull (popup.Parent);
            Assert.Equal (Point.Empty, popup.Location);
        }

        [Fact]
        public void Hiding_the_control_hides_it_without_disposing ()
        {
            using var owner = OpenOwnerForm ();

            using var popup = new ProbeControl ();
            popup.SetBounds (50, 60, 200, 120);
            popup.MakeTopLevel ();
            popup.Visible = true;
            popup.Visible = false;

            Assert.False (popup.Visible);
            Assert.False (popup.IsDisposed);

            // And it can come back: re-showing reuses the machinery rather than throwing.
            popup.SetBounds (10, 10, 200, 120);
            popup.Visible = true;
            Assert.True (popup.Visible);
        }

        [Fact]
        public void SetTopLevel_throws_for_a_parented_control ()
        {
            using var owner = OpenOwnerForm ();
            using var child = new ProbeControl ();
            owner.Controls.Add (child);

            Assert.Throws<System.InvalidOperationException> (child.MakeTopLevel);
        }

        [Fact]
        public void Disposing_the_control_tears_the_host_down ()
        {
            using var owner = OpenOwnerForm ();

            var popup = new ProbeControl ();
            popup.SetBounds (50, 60, 200, 120);
            popup.MakeTopLevel ();
            popup.Visible = true;

            var host = popup.Parent;
            Assert.NotNull (host);

            // Krypton dismisses a popup by disposing it (VisualPopupManager.EndAllTracking), so
            // disposal must take the window down and detach the control.
            popup.Dispose ();
            Assert.Empty (host!.Controls);
        }

        [Fact]
        public void Returning_to_child_level_releases_the_host ()
        {
            using var owner = OpenOwnerForm ();

            using var popup = new ProbeControl ();
            popup.SetBounds (50, 60, 200, 120);
            popup.MakeTopLevel ();
            popup.Visible = true;

            popup.MakeChildLevel ();

            Assert.False (popup.TopLevelForTest);
            Assert.Null (popup.Parent);
        }
    }
}
