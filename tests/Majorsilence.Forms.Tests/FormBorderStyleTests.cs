using Xunit;

namespace Majorsilence.Forms.Tests
{
    // FormBorderStyle used to store a value nothing read, so an app that set None to draw its own
    // title bar got that title bar *plus* a caption from the framework -- two of them, stacked.
    public class FormBorderStyleTests
    {
        [Fact]
        public void Defaults_to_Sizable ()
        {
            using var form = new Form ();

            Assert.Equal (FormBorderStyle.Sizable, form.FormBorderStyle);
        }

        // The point of the fix: neither chrome survives. TitleBar is what this library draws when the
        // OS is not drawing one, so a borderless form has to suppress it too.
        [Fact]
        public void None_hides_the_frameworks_own_title_bar ()
        {
            using var form = new Form { FormBorderStyle = FormBorderStyle.None };

            Assert.False (form.TitleBar.Visible);
        }

        [Fact]
        public void None_removes_the_forms_painted_border ()
        {
            using var form = new Form { FormBorderStyle = FormBorderStyle.None };

            Assert.Equal (0, form.Style.Border.GetWidth ());
        }

        [Theory]
        [InlineData (FormBorderStyle.Sizable, true)]
        [InlineData (FormBorderStyle.SizableToolWindow, true)]
        [InlineData (FormBorderStyle.FixedSingle, false)]
        [InlineData (FormBorderStyle.Fixed3D, false)]
        [InlineData (FormBorderStyle.FixedDialog, false)]
        [InlineData (FormBorderStyle.FixedToolWindow, false)]
        [InlineData (FormBorderStyle.None, false)]
        public void Only_the_sizable_styles_can_be_resized (FormBorderStyle style, bool resizeable)
        {
            using var form = new Form { FormBorderStyle = style };

            Assert.Equal (resizeable, form.Resizeable);
        }

        // Setting it back has to restore the caption, or a form that toggles chrome at runtime
        // (full-screen mode, a kiosk switch) would never get its title bar back.
        [Fact]
        public void Leaving_None_restores_the_chrome ()
        {
            using var form = new Form { UseSystemDecorations = false };

            form.FormBorderStyle = FormBorderStyle.None;
            Assert.False (form.TitleBar.Visible);

            form.FormBorderStyle = FormBorderStyle.Sizable;
            Assert.True (form.TitleBar.Visible);
        }

        // UseSystemDecorations chooses whose caption is drawn; None means nobody's, so the two
        // settings must not fight over the title bar.
        [Fact]
        public void None_wins_over_UseSystemDecorations ()
        {
            using var form = new Form { FormBorderStyle = FormBorderStyle.None };

            form.UseSystemDecorations = true;

            Assert.False (form.TitleBar.Visible);
        }
    }
}
