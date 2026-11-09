using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A dialog is never owned by a window that is not on screen. Avalonia refuses one ("Cannot show window with
    // non-visible owner"), and the throw came out of ShowDialog after every other window had been disabled: the
    // dialog never appeared and the application stopped taking input. An application that keeps its first form
    // hidden (a login or launcher form) hit it on every ShowDialog () with no owner, which took the first open form.
    [Collection ("Headless")]
    public class DialogOwnerVisibilityTests
    {
        private static Form Shown (string text)
        {
            var form = new Form { Text = text, ClientSize = new Size (200, 100) };
            form.Show ();
            return form;
        }

        private static Form Hidden (string text)
        {
            var form = Shown (text);
            form.Hide ();
            return form;
        }

        private static IWindowBackend? NativeOwner (Form dialog) => ((HeadlessWindowHost) dialog.Backend).LastDialogOwner;

        [Fact]
        public async Task A_dialog_with_no_owner_is_not_owned_by_a_hidden_form ()
        {
            HeadlessRenderer.Use ();
            using var hidden = Hidden ("hidden");
            using var main = Shown ("main");

            using var dialog = new Form ();
            var task = dialog.ShowDialogAsync ();

            Assert.Same (main, dialog.Owner);
            Assert.Same (main.Backend, NativeOwner (dialog));

            dialog.DialogResult = DialogResult.OK;
            Assert.Equal (DialogResult.OK, await task);
        }

        [Fact]
        public async Task A_dialog_with_no_owner_goes_to_the_active_form_as_a_message_box_does ()
        {
            // Upstream's ShowDialog () takes GetActiveWindow, as MessageBox.Show does (DialogDefaultOwnerTests). It
            // took the first form opened, which put the dialog over a window the user was not looking at.
            HeadlessRenderer.Use ();
            using var a = Shown ("A");
            using var b = Shown ("B");
            b.Activate ();

            using var dialog = new Form ();
            var task = dialog.ShowDialogAsync ();

            Assert.Same (b, dialog.Owner);

            dialog.DialogResult = DialogResult.OK;
            await task;
        }

        [Fact]
        public async Task A_dialog_given_a_hidden_owner_is_still_shown_modally ()
        {
            HeadlessRenderer.Use ();
            using var hidden = Hidden ("hidden");
            using var main = Shown ("main");

            using var dialog = new Form ();
            var task = dialog.ShowDialogAsync (hidden);

            Assert.True (dialog.Visible);
            Assert.True (dialog.Modal);
            Assert.Same (hidden, dialog.Owner);
            Assert.Null (NativeOwner (dialog));
            Assert.False (main.Backend.Enabled);

            dialog.DialogResult = DialogResult.OK;
            Assert.Equal (DialogResult.OK, await task);
            Assert.True (main.Backend.Enabled);
        }

        [Fact]
        public void A_dialog_that_fails_to_show_leaves_the_application_usable ()
        {
            HeadlessRenderer.Use ();
            using var main = Shown ("main");

            using var failing = new Form ();
            failing.Load += (_, _) => throw new InvalidOperationException ("load failed");

            Assert.Throws<InvalidOperationException> (() => { _ = failing.ShowDialogAsync (main); });

            Assert.True (main.Backend.Enabled);
            Assert.Null (Application.ActiveModalForm);
            Assert.False (failing.Modal);
        }

        [Fact]
        public void ShowDialog_with_a_hidden_first_form_shows_and_returns_the_result ()
        {
            // The shape it was found in: a hidden first form, a shown main window, and a dialog opened with
            // ShowDialog () from a button on it.
            HeadlessRenderer.Use ();
            using var hidden = Hidden ("hidden");
            using var main = Shown ("main");

            using var dialog = new Form ();
            Platform.Backend.Post (() => dialog.DialogResult = DialogResult.OK);

            var result = DialogResult.None;
            Exception? failure = null;
            var thread = new Thread (() => {
                try { result = dialog.ShowDialog (); } catch (Exception ex) { failure = ex; }
            }) { IsBackground = true };
            thread.Start ();

            Assert.True (thread.Join (TimeSpan.FromSeconds (10)), "ShowDialog never returned");
            Assert.Null (failure);
            Assert.Equal (DialogResult.OK, result);
            Assert.True (main.Backend.Enabled);
        }
    }
}
