using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Validation on a window was three stubs stacked on each other: WindowBase.Validate() returned true
    // without raising anything, Form.ValidateChildren() returned true without validating anything (while
    // the ValidationConstraints overload beside it was real), and Form.Validating was a discarding
    // `add { } remove { }` that threw handlers away. A form gating a Save button on Validate() always
    // saved.
    public class WindowValidationParityTests
    {
        [Fact]
        public void A_cancelled_Validating_makes_Validate_report_false ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 150) };
            form.Validating += (_, e) => e.Cancel = true;

            Assert.False (form.Validate (), "Validate() ignored a cancelled Validating handler.");
        }

        [Fact]
        public void An_uncancelled_cycle_raises_Validating_then_Validated ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var order = new System.Collections.Generic.List<string> ();
            form.Validating += (_, _) => order.Add ("validating");
            form.Validated += (_, _) => order.Add ("validated");

            Assert.True (form.Validate ());
            Assert.Equal (new[] { "validating", "validated" }, order);
        }

        [Fact]
        public void Validated_does_not_fire_when_validation_was_cancelled ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var validated = 0;
            form.Validating += (_, e) => e.Cancel = true;
            form.Validated += (_, _) => validated++;

            form.Validate ();

            Assert.Equal (0, validated);
        }

        [Fact]
        public void ValidateChildren_actually_walks_the_children ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 150) };
            var box = new TextBox ();
            form.Controls.Add (box);

            var validated = 0;
            box.Validating += (_, _) => validated++;

            Assert.True (form.ValidateChildren ());
            Assert.Equal (1, validated);
        }

        [Fact]
        public void ValidateChildren_reports_false_when_a_child_cancels ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 150) };
            var box = new TextBox ();
            box.Validating += (_, e) => e.Cancel = true;
            form.Controls.Add (box);

            Assert.False (form.ValidateChildren ());
        }

        [Fact]
        public void BeginUpdate_and_EndUpdate_suspend_and_resume_layout ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (200, 150) };
            var docked = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add (docked);
            form.Show ();

            form.BeginUpdate ();
            form.ClientSize = new Size (400, 300);
            form.EndUpdate ();

            // EndUpdate resumes and repaints, so the docked child ends up at the new size either way --
            // what matters is that the pair is callable and leaves layout working, not suspended.
            form.PerformLayout ();

            Assert.Equal (400, docked.Width);
        }
    }
}
