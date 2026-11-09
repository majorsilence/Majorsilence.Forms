using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Closing a modeless form disposes it, and upstream refuses to show a disposed form: Show, Visible = true and
    // ShowDialog throw ObjectDisposedException. Here they showed it again. An MDI child came back into its parent
    // as an empty frame, which is what a handler that closes its form and then sets Visible = true in a Finally
    // block (an "always restore the window" pattern) left on screen -- and the form's data was already gone, so
    // the next use of it failed somewhere unrelated.
    [Collection ("Headless")]
    public class ShowDisposedFormTests
    {
        private static (Form Parent, Form Child) MdiPair ()
        {
            HeadlessRenderer.Use ();
            var parent = new Form { IsMdiContainer = true, ClientSize = new Size (800, 600) };
            parent.Show ();
            var child = new Form { MdiParent = parent, ClientSize = new Size (300, 200), Text = "Child" };
            child.Show ();
            return (parent, child);
        }

        [Fact]
        public void Setting_Visible_on_a_closed_MDI_child_throws_and_does_not_bring_it_back ()
        {
            var (parent, child) = MdiPair ();
            using var _p = parent;

            child.Close ();
            Assert.True (child.IsDisposed);

            Assert.Throws<ObjectDisposedException> (() => child.Visible = true);

            Assert.Empty (parent.MdiChildren);
            Assert.DoesNotContain (parent.Controls.OfType<MdiClient> ().Single ().Controls.OfType<Control> (),
                c => c is MdiChildWindow);
            Assert.False (child.Visible);
        }

        [Fact]
        public void A_handler_that_closes_its_MDI_child_and_shows_it_in_Finally_gets_ObjectDisposedException ()
        {
            var (parent, child) = MdiPair ();
            using var _p = parent;

            void Issue ()
            {
                try {
                    child.Close ();
                } finally {
                    child.Visible = true;
                }
            }

            Assert.Throws<ObjectDisposedException> (Issue);
            Assert.Empty (parent.MdiChildren);
        }

        [Fact]
        public void Showing_a_closed_top_level_form_throws ()
        {
            HeadlessRenderer.Use ();
            var form = new Form ();
            form.Show ();
            form.Close ();
            Assert.True (form.IsDisposed);

            Assert.Throws<ObjectDisposedException> (() => form.Show ());
            Assert.Throws<ObjectDisposedException> (() => form.Visible = true);
            Assert.False (form.Visible);
            Assert.DoesNotContain (form, Application.OpenForms.Cast<Form> ());
        }

        [Fact]
        public void Showing_a_disposed_form_as_a_dialog_throws_and_leaves_no_modal_state ()
        {
            HeadlessRenderer.Use ();
            using var owner = new Form ();
            owner.Show ();
            var dialog = new Form ();
            dialog.Dispose ();

            // Queued before the call so that, were the dialog shown, its modal loop would run this and return
            // rather than wait for a close that never comes; the test then fails instead of hanging.
            Platform.Backend.Post (() => { if (dialog.Visible) dialog.DialogResult = DialogResult.Cancel; });
            try {
                Assert.Throws<ObjectDisposedException> (() => dialog.ShowDialog (owner));
            } finally {
                Platform.Backend.DoEvents ();
            }
            // Refused before any task exists, as ShowDialog is, so nothing is left to await.
            Assert.Throws<ObjectDisposedException> (() => { _ = dialog.ShowDialogAsync (owner); });

            Assert.False (dialog.Modal);
            Assert.True (owner.Enabled);
            Assert.DoesNotContain (dialog, Application.ModalStack);
        }
    }
}
