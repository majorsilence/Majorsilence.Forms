using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Upstream's TabControl.Controls and TabPages are one collection, so taking a page out through Controls
    // takes its tab out too. Measured on WinForms (.NET 10) with pages A, B and C: Controls.Remove (B) left
    // 2 pages, B unparented; RemoveByKey ("c") left 1; adding B back gave 2; Controls.Clear () left none.
    [Collection ("Headless")]
    public class TabControlControlsRemoveTests
    {
        private static (Form form, TabControl tabs, TabPage a, TabPage b, TabPage c) Tabs ()
        {
            HeadlessRenderer.Use ();
            var tabs = new TabControl ();
            TabPage a = new TabPage ("A") { Name = "a" }, b = new TabPage ("B") { Name = "b" }, c = new TabPage ("C") { Name = "c" };
            tabs.TabPages.Add (a);
            tabs.TabPages.Add (b);
            tabs.TabPages.Add (c);
            var form = new Form ();
            form.Controls.Add (tabs);
            return (form, tabs, a, b, c);
        }

        [Fact]
        public void Removing_a_page_from_Controls_removes_its_tab ()
        {
            var (form, tabs, a, b, c) = Tabs ();

            using (form) {
                b.Visible = false;
                tabs.Controls.Remove (b);

                Assert.Equal (new[] { a, c }, tabs.TabPages);
                Assert.Null (b.Parent);
                Assert.Equal (0, tabs.SelectedIndex);

                // And it comes back as a tab when it is added back.
                tabs.Controls.Add (b);
                Assert.Equal (new[] { a, c, b }, tabs.TabPages);
            }
        }

        [Fact]
        public void Removing_a_page_by_key_from_Controls_removes_its_tab ()
        {
            var (form, tabs, a, b, _) = Tabs ();

            using (form) {
                tabs.Controls.RemoveByKey ("c");

                Assert.Equal (new[] { a, b }, tabs.TabPages);
            }
        }

        [Fact]
        public void Clearing_Controls_removes_every_tab ()
        {
            var (form, tabs, _, _, _) = Tabs ();

            using (form) {
                tabs.Controls.Clear ();

                Assert.Empty (tabs.TabPages);
                Assert.Equal (-1, tabs.SelectedIndex);
            }
        }
    }
}
