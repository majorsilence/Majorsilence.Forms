using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// WinForms commits a tab change on mouse DOWN. TabStrip used to select in OnMouseClick, but
    /// Control.RaiseClick raises OnClick before OnMouseClick — so a Click handler observed the tab the
    /// user had just left. Migrated code reads SelectedTab inside Click to decide what to load, so it
    /// acted on the wrong tab.
    /// </summary>
    public class TabStripSelectionTests
    {
        private static (Form form, TabControl tabs) Build ()
        {
            var form = new Form { Size = new Size (400, 300) };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add (new TabPage { Text = "First" });
            tabs.TabPages.Add (new TabPage { Text = "Second" });
            tabs.TabPages.Add (new TabPage { Text = "Third" });
            form.Controls.Add (tabs);
            HeadlessRenderer.CapturePng (form, 400, 300);
            return (form, tabs);
        }

        // A point inside the header of the tab at <paramref name="index"/>, in form coordinates.
        private static Point Header (TabControl tabs, int index)
        {
            var rect = tabs.GetTabRect (index);
            return new Point (tabs.Left + rect.Left + (rect.Width / 2),
                              tabs.Top + (tabs.TabStrip.Height / 2));
        }

        [Fact]
        public void Selection_commits_on_mouse_down_before_the_button_is_released ()
        {
            var (form, tabs) = Build ();
            var target = Header (tabs, 1);

            HeadlessRenderer.MouseDown (form, target.X, target.Y);

            // No MouseUp yet: the change must already have happened.
            Assert.Equal (1, tabs.SelectedIndex);
            Assert.Equal ("Second", tabs.SelectedTab?.Text);
        }

        [Fact]
        public void SelectedIndexChanged_fires_on_the_press_not_the_release ()
        {
            var (form, tabs) = Build ();
            var fired = 0;
            tabs.SelectedIndexChanged += (_, _) => fired++;
            var target = Header (tabs, 2);

            HeadlessRenderer.MouseDown (form, target.X, target.Y);
            Assert.Equal (1, fired);

            HeadlessRenderer.MouseUp (form, target.X, target.Y);
            Assert.Equal (1, fired);   // the release must not raise it a second time
        }

        [Fact]
        public void A_full_click_lands_on_the_clicked_tab ()
        {
            // The OnMouseClick path is kept as a fallback for input delivered without a preceding
            // press; it must be idempotent rather than selecting something else.
            var (form, tabs) = Build ();
            var target = Header (tabs, 2);

            HeadlessRenderer.Click (form, target.X, target.Y);

            Assert.Equal (2, tabs.SelectedIndex);
        }

        [Fact]
        public void Clicking_the_already_selected_tab_changes_nothing ()
        {
            var (form, tabs) = Build ();
            var changed = 0;
            tabs.SelectedIndexChanged += (_, _) => changed++;
            var target = Header (tabs, 0);

            HeadlessRenderer.Click (form, target.X, target.Y);

            Assert.Equal (0, tabs.SelectedIndex);
            Assert.Equal (0, changed);
        }

        [Fact]
        public void A_disabled_tab_header_is_not_selected ()
        {
            // The Enabled check moved along with the selection logic; make sure it came too. It is the
            // strip ITEM's Enabled that gates selection -- disabling a TabPage disables its contents,
            // and WinForms still lets you select that tab.
            var (form, tabs) = Build ();
            tabs.TabStrip.Tabs[1].Enabled = false;
            var target = Header (tabs, 1);

            HeadlessRenderer.Click (form, target.X, target.Y);

            Assert.Equal (0, tabs.SelectedIndex);
        }

        [Fact]
        public void Right_mouse_down_alone_does_not_change_the_selection ()
        {
            // Only the left button commits on press; a right press is for context menus.
            var (form, tabs) = Build ();
            var target = Header (tabs, 1);

            HeadlessRenderer.MouseDown (form, target.X, target.Y, MouseButtons.Right);

            Assert.Equal (0, tabs.SelectedIndex);
        }
    }
}
