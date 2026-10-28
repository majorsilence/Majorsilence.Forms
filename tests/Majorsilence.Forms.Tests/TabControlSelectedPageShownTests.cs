using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's TabControl shows the selected page and hides the rest when its handle is created, whatever the pages'
    // Visible said before. Measured on WinForms (.NET 10): pages A, B and C, added and then set Visible False, False and
    // True (as a designer file can carry them), come up True, False, False with A selected; a selected page hidden at
    // runtime stays hidden until it is selected again.
    [Collection ("Headless")]
    public class TabControlSelectedPageShownTests
    {
        [Fact]
        public void The_selected_page_is_shown_when_the_control_is_created ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { ClientSize = new Size (500, 400) };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage a = new TabPage ("A"), b = new TabPage ("B"), c = new TabPage ("C");
            tabs.SuspendLayout ();
            tabs.Controls.Add (a);
            tabs.Controls.Add (b);
            tabs.Controls.Add (c);
            tabs.SelectedIndex = 0;
            a.Visible = false;
            b.Visible = false;
            c.Visible = true;
            tabs.ResumeLayout (false);
            form.Controls.Add (tabs);

            form.Show ();

            Assert.Same (a, tabs.SelectedTab);
            Assert.Equal ((true, false, false), (a.Visible, b.Visible, c.Visible));

            // Hidden at runtime it stays hidden, and selecting it again shows it.
            a.Visible = false;
            Assert.False (a.Visible);
            tabs.SelectedIndex = 1;
            tabs.SelectedIndex = 0;
            Assert.True (a.Visible);
        }
    }
}
