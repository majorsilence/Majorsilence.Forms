using System;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The TaskDialog family. ShowDialog needs a message loop, so what is covered here is the page
    // model it composes from: the controls, their binding to the page, and the radio-button
    // exclusivity a composed dialog depends on.
    public class TaskDialogParityTests
    {
        [Fact]
        public void A_page_starts_unbound ()
        {
            var page = new TaskDialogPage ();

            Assert.Null (page.BoundDialog);
        }

        [Fact]
        public void Standard_buttons_carry_their_text ()
        {
            Assert.Equal ("OK", TaskDialogButton.OK.Text);
            Assert.Equal ("Cancel", TaskDialogButton.Cancel.Text);
            Assert.Equal ("Abort", TaskDialogButton.Abort.Text);
            Assert.Equal ("Retry", TaskDialogButton.Retry.Text);
        }

        [Fact]
        public void Adding_a_control_to_a_page_binds_it ()
        {
            var page = new TaskDialogPage ();
            var button = new TaskDialogButton ("Go");

            page.Buttons.Add (button);

            Assert.Same (page, button.BoundPage);
        }

        [Fact]
        public void Checking_a_radio_button_clears_its_siblings ()
        {
            var page = new TaskDialogPage ();
            var first = new TaskDialogRadioButton ("First");
            var second = new TaskDialogRadioButton ("Second");

            page.RadioButtons.Add (first);
            page.RadioButtons.Add (second);

            first.Checked = true;
            Assert.True (first.Checked);
            Assert.False (second.Checked);

            second.Checked = true;
            Assert.False (first.Checked);
            Assert.True (second.Checked);
        }

        [Fact]
        public void An_unbound_radio_button_has_no_siblings_to_clear ()
        {
            var loose = new TaskDialogRadioButton ("Loose") { Checked = true };

            Assert.True (loose.Checked);
        }

        [Fact]
        public void A_button_raises_click_when_performed ()
        {
            var button = new TaskDialogButton ("Go");
            var clicks = 0;

            button.Click += (_, _) => clicks++;
            button.PerformClick ();

            Assert.Equal (1, clicks);
        }

        [Fact]
        public void A_verification_check_box_tracks_its_state ()
        {
            var verification = new TaskDialogVerificationCheckBox ("Do not ask again");
            var changes = 0;

            verification.CheckedChanged += (_, _) => changes++;
            verification.Checked = true;

            Assert.True (verification.Checked);
            Assert.Equal (1, changes);

            verification.Checked = true;
            Assert.Equal (1, changes);
        }

        [Fact]
        public void A_page_holds_its_text_and_heading ()
        {
            var page = new TaskDialogPage {
                Caption = "Export",
                Heading = "Export finished",
                Text = "Seven files were written.",
            };

            Assert.Equal ("Export", page.Caption);
            Assert.Equal ("Export finished", page.Heading);
            Assert.Equal ("Seven files were written.", page.Text);
        }
    }
}
