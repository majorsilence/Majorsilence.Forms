using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The designer Reset* pattern. Every designer file emits these, and each one has to clear the SAME
    // storage its property writes -- otherwise "reset" leaves the explicit value in place and the property
    // keeps reporting it, which is why these are checked by asserting the value actually changes back
    // rather than merely that the method exists. The window's storage is not uniform: BackColor and
    // ForeColor live on its own ControlStyle, Cursor in its own field, Font and RightToLeft on the root
    // adapter, and Text on Form.
    public class WindowResetPatternTests
    {
        [Fact]
        public void ResetBackColor_clears_an_explicit_colour ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var original = form.BackColor;
            form.BackColor = Color.Firebrick;

            Assert.Equal (Color.Firebrick.ToArgb (), form.BackColor.ToArgb ());

            form.ResetBackColor ();

            Assert.Equal (original.ToArgb (), form.BackColor.ToArgb ());
        }

        [Fact]
        public void ResetForeColor_clears_an_explicit_colour ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var original = form.ForeColor;
            form.ForeColor = Color.Firebrick;

            Assert.Equal (Color.Firebrick.ToArgb (), form.ForeColor.ToArgb ());

            form.ResetForeColor ();

            Assert.Equal (original.ToArgb (), form.ForeColor.ToArgb ());
        }

        [Fact]
        public void ResetCursor_clears_an_explicit_cursor ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Cursor = Cursors.WaitCursor };

            form.ResetCursor ();

            Assert.Null (form.Cursor);
        }

        [Fact]
        public void ResetFont_clears_an_explicit_font_on_the_window_and_its_children ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var child = new Panel ();
            form.Controls.Add (child);

            var inherited = child.Font.Name;
            form.Font = new Majorsilence.Forms.Drawing.Font ("Courier New", 14f);

            Assert.Equal ("Courier New", child.Font.Name);   // children resolve through the window

            form.ResetFont ();

            Assert.Equal (inherited, child.Font.Name);
        }

        [Fact]
        public void ResetRightToLeft_returns_the_window_and_its_children_to_the_default ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var child = new Panel ();   // left on Inherit, so it reads through the window
            form.Controls.Add (child);

            form.RightToLeft = RightToLeft.Yes;

            Assert.Equal (RightToLeft.Yes, child.RightToLeft);

            form.ResetRightToLeft ();

            // The getter RESOLVES Inherit through the parent chain, and a window has no parent -- so it
            // reports the ambient default rather than the word Inherit, exactly as WinForms does. What
            // matters is that the explicit value is gone and children follow the default again.
            Assert.Equal (RightToLeft.No, form.RightToLeft);
            Assert.Equal (RightToLeft.No, child.RightToLeft);
        }

        [Fact]
        public void ResetText_clears_the_title ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Text = "Report" };

            form.ResetText ();

            Assert.Equal (string.Empty, form.Text);
        }

        [Fact]
        public void The_assembly_metadata_properties_agree_with_a_controls ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var control = new Panel ();

            // Same source (the application's assembly metadata), so a window and a control cannot report
            // different products.
            Assert.Equal (control.CompanyName, form.CompanyName);
            Assert.Equal (control.ProductName, form.ProductName);
            Assert.Equal (control.ProductVersion, form.ProductVersion);
        }

        [Fact]
        public void The_window_defaults_match_a_controls ()
        {
            HeadlessRenderer.Use ();

            Assert.Equal (Control.DefaultForeColor.ToArgb (), WindowBase.DefaultForeColor.ToArgb ());
            Assert.Equal (Control.DefaultFont.Name, WindowBase.DefaultFont.Name);
        }
    }
}
