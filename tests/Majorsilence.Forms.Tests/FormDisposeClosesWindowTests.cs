using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms destroys a form's window handle when the form is disposed, so the window leaves the
    // screen whether or not anything called Close first. Majorsilence.Forms detached the form from
    // Application.OpenForms and left the backend window up, with nothing painting into it -- so a popup
    // dismissed by disposing it (which is how popups are normally dismissed) left a blank rectangle on
    // screen that the user could not get rid of.
    public class FormDisposeClosesWindowTests
    {
        [Fact]
        public void Disposing_a_shown_form_marks_it_not_visible ()
        {
            var form = new Form ();
            form.Show ();
            Assert.True (form.Visible);

            form.Dispose ();

            Assert.False (form.Visible);
            Assert.True (form.IsDisposed);
        }

        [Fact]
        public void Disposing_a_shown_form_removes_it_from_OpenForms ()
        {
            var form = new Form ();
            form.Show ();
            var before = Application.OpenForms.Count;

            form.Dispose ();

            Assert.Equal (before - 1, Application.OpenForms.Count);
            Assert.DoesNotContain (form, Application.OpenForms.Cast<Form> ());
        }

        [Fact]
        public void Disposing_does_not_raise_FormClosing ()
        {
            // Disposing is not closing: WinForms destroys the handle without running the closing
            // pipeline, and code that cancels FormClosing must not be able to veto a Dispose.
            var form = new Form ();
            var closingRaised = false;
            form.FormClosing += (_, _) => closingRaised = true;
            form.Show ();

            form.Dispose ();

            Assert.False (closingRaised);
        }

        [Fact]
        public void Disposing_twice_is_harmless ()
        {
            var form = new Form ();
            form.Show ();

            form.Dispose ();
            form.Dispose ();

            Assert.True (form.IsDisposed);
            Assert.False (form.Visible);
        }

        [Fact]
        public void Disposing_a_form_that_was_never_shown_is_harmless ()
        {
            var form = new Form ();

            form.Dispose ();

            Assert.True (form.IsDisposed);
            Assert.False (form.Visible);
        }
    }
}
