using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2 — the remaining big types (Form, Control, WindowBase, ToolTip: 47 entries between them).
    //
    // Most of what is left there is not a stub nobody wired. Two categories account for it, and the
    // tests below pin the one entry that WAS a real gap plus the boundary of each category:
    //
    //   Form.ControlBox      a real gap -- a form that asked for no control box still got minimise,
    //                        maximise and close. Closed here.
    //   Form.Modal,          OUTBOUND STATE: the framework WRITES these for the application to read,
    //   WindowBase.Disposing so nothing in the assembly reads the getter and the scan flags them. The
    //                        mirror image of the outbound *EventArgs data carriers, and just as
    //                        legitimately inert. Pinned so they are not "fixed" by a later sweep.
    [Collection ("Headless")]
    public class W62RemainingTypesTests
    {
        // ---------------- Form.ControlBox

        // Whether this configuration draws its own caption buttons at all. With the OS chrome the bar
        // is hidden entirely, and with a native overlay the OS draws the traffic lights -- in both the
        // managed buttons are invisible regardless, so nothing ControlBox does is observable. That is
        // the default in two of the four gates, which is how it was found: the assertions would have
        // been vacuous rather than wrong.
        private static bool ManagedCaption (Form form)
            => form.TitleBar.Visible && !form.TitleBar.NativeOverlay;

        private static Form Shown ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { Width = 300, Height = 200 };
            form.Show ();

            return form;
        }

        [Fact]
        public void A_form_shows_its_caption_buttons_by_default ()
        {
            // PREMISE: without it, "no buttons" is also what a form with no title bar looks like.
            //
            // Skipped when the title bar is a NATIVE OVERLAY -- the OS draws the traffic lights there
            // and every managed caption button is hidden regardless, so nothing this property does is
            // observable. That is the default configuration in one of the four gates, which is how it
            // was found; the assertions below would have been vacuous rather than wrong.
            using var form = Shown ();

            try {
                if (!ManagedCaption (form))
                    return;

                Assert.True (form.ControlBox);
                Assert.True (form.TitleBar.AllowClose);
                Assert.True (form.TitleBar.AllowMinimize);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ControlBox_false_takes_the_whole_cluster_away ()
        {
            using var form = Shown ();

            try {
                if (!ManagedCaption (form))
                    return;

                form.ControlBox = false;

                Assert.False (form.TitleBar.AllowClose);
                Assert.False (form.TitleBar.AllowMinimize);
                Assert.False (form.TitleBar.AllowMaximize);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Turning_it_back_on_restores_what_the_other_two_said ()
        {
            // GUARD: ControlBox is a master switch, not an override. Restoring all three would turn on
            // a maximise button the form had explicitly refused.
            using var form = Shown ();

            try {
                if (!ManagedCaption (form))
                    return;

                form.MaximizeBox = false;
                form.ControlBox = false;
                form.ControlBox = true;

                Assert.True (form.TitleBar.AllowClose);
                Assert.False (form.TitleBar.AllowMaximize);
            } finally {
                form.Close ();
            }
        }

        // ---------------- outbound state, which must NOT be "wired"

        [Fact]
        public void Modal_reports_whether_the_form_was_shown_as_a_dialog ()
        {
            // Form.Modal is written by the dialog path (Form.cs:1086) and cleared on close (:447). The
            // stored-only scan flags it because nothing READS the getter in this assembly -- the reader
            // is application code, which is the whole point. Upstream's Modal is read-only for the same
            // reason.
            //
            // Pinned so a later sweep does not "fix" a property that already works.
            using var form = Shown ();

            try {
                Assert.False (form.Modal);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Disposing_reports_the_teardown_window ()
        {
            // The same category on WindowBase: written at the top of Dispose and cleared at the end
            // (WindowBase.cs:304, :347), for an application's handlers to check. Never read here.
            var form = new Form { Width = 200, Height = 150 };

            Assert.False (form.Disposing);

            var during = false;
            form.Disposed += (_, _) => during = form.Disposing;

            form.Dispose ();

            Assert.True (during, "Disposing should be true while the teardown handlers run");
        }
    }
}
