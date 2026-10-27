using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A MessageBox or FileDialog shown with no owner belongs to the thread's active window, as upstream's GetActiveWindow
    // gives it -- not the first form opened, and not ActiveForm, which follows the foreground. Measured on WinForms
    // (.NET 10), with another application in front: forms A then B shown, the box goes to B; A activated again, to A;
    // B closed, to A.
    [Collection ("Headless")]
    public class DialogDefaultOwnerTests
    {
        // The owner a MessageBox shown with no owner is given, the box closed again.
        private static Form? BoxOwner ()
        {
            _ = MessageBox.ShowAsync ("x", "owned");
            var shown = Application.OpenForms.OfType<MessageBoxForm> ().Last ();
            var owner = shown.Owner;
            shown.Close ();
            return owner;
        }

        private static Form Shown (string text)
        {
            var form = new Form { Text = text, ClientSize = new Size (200, 100) };
            form.Show ();
            return form;
        }

        [Fact]
        public void A_box_with_no_owner_goes_to_the_last_form_shown_even_without_the_foreground ()
        {
            HeadlessRenderer.Use ();
            using var a = Shown ("A");
            using var b = Shown ("B");

            // The platform refused B the foreground: another application kept it.
            b.OnBackendDeactivated ();
            Assert.Null (Form.ActiveForm);

            Assert.Same (b, BoxOwner ());
        }

        [Fact]
        public void Activating_a_form_makes_it_the_owner_and_closing_falls_back ()
        {
            HeadlessRenderer.Use ();
            using var a = Shown ("A");
            var b = Shown ("B");

            a.Activate ();
            Assert.Same (a, BoxOwner ());

            b.Activate ();
            Assert.Same (b, BoxOwner ());

            b.Close ();
            Assert.Same (a, BoxOwner ());
        }
    }
}
