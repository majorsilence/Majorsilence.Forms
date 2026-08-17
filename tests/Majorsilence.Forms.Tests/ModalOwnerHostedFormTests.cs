using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Regression: a dialog opened from an MDI child left a blank duplicate of that child on screen the
    // moment it was dismissed. An MDI child's own backend window is constructed but never shown -- its
    // content renders into the MDI frame -- and the modal bookkeeping reached for dialog_parent.Backend
    // directly. Backend.Activate() maps to the platform "make key and order front", so re-activating the
    // opener after the dialog closed displayed that empty window. The modal path has to resolve the
    // window actually presenting a hosted form instead.
    public class ModalOwnerHostedFormTests
    {
        private static Form MdiContainer ()
        {
            HeadlessRenderer.Use ();
            var parent = new Form { IsMdiContainer = true };
            HeadlessRenderer.CapturePng (parent, 900, 700);
            return parent;
        }

        [Fact]
        public void An_mdi_child_presents_through_its_container_not_its_own_window ()
        {
            var parent = MdiContainer ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Show ();

            Assert.Same (parent, child.PresentationWindow);
            // The container owns a real window, so it presents itself.
            Assert.Same (parent, parent.PresentationWindow);
        }

        [Fact]
        public void A_top_level_form_presents_itself ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form ();

            Assert.Same (form, form.PresentationWindow);
        }

        [Fact]
        public void A_form_hosted_in_a_control_tree_presents_through_its_host ()
        {
            HeadlessRenderer.Use ();
            var host = new Form { ClientSize = new Size (500, 400) };
            var hosted = new Form { ClientSize = new Size (200, 150) };
            host.Controls.Add (hosted);
            HeadlessRenderer.CapturePng (host, 500, 400);

            Assert.Same (host, hosted.PresentationWindow);
        }

        [Fact]
        public void Dismissing_a_dialog_owned_by_an_mdi_child_leaves_the_child_hosted ()
        {
            var parent = MdiContainer ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Show ();

            var dialog = new Form { ClientSize = new Size (200, 120) };
            dialog.ShowDialogAsync (child);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close ();

            // The child must still be a hosted frame in the MDI client, never promoted to a window of
            // its own -- the blank duplicate was exactly that promotion.
            Assert.Single (parent.MdiClientControl!.Controls.OfType<MdiChildWindow> ());
            Assert.Same (parent, child.PresentationWindow);

            // And the container it lives in is usable again once the dialog is gone.
            Assert.True (parent.Backend.Enabled);
        }

        [Fact]
        public void Dismissing_a_dialog_re_enables_the_container_that_was_disabled ()
        {
            var parent = MdiContainer ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200) };
            child.Show ();

            var dialog = new Form { ClientSize = new Size (200, 120) };
            dialog.ShowDialogAsync (child);

            // Modality has to bite on the real window; disabling the child's unrealized backend left the
            // MDI container fully clickable behind the dialog.
            Assert.False (parent.Backend.Enabled);

            dialog.DialogResult = DialogResult.OK;
            dialog.Close ();

            Assert.True (parent.Backend.Enabled);
        }
    }
}
