using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// In WinForms the tab headers belong to the TabControl, so clicking one raises TabControl.Click.
    /// Here they live in an implicit child strip, which used to swallow the click -- and migrated code
    /// commonly loads a tab's contents from `Handles someTab.Click`.
    /// </summary>
    public class TabControlClickTests
    {
        private static (Form form, TabControl tabs) Build ()
        {
            var form = new Form { Size = new Size (400, 300) };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add (new TabPage { Text = "First" });
            tabs.TabPages.Add (new TabPage { Text = "Second" });
            form.Controls.Add (tabs);
            HeadlessRenderer.CapturePng (form, 400, 300);
            return (form, tabs);
        }

        [Fact]
        public void Clicking_a_tab_header_raises_Click_on_the_TabControl ()
        {
            var (form, tabs) = Build ();
            var clicks = 0;
            tabs.Click += (_, _) => clicks++;

            // Middle of the header strip, where the tab captions are drawn.
            HeadlessRenderer.Click (form, tabs.Left + 30, tabs.Top + (tabs.TabStrip.Height / 2));

            Assert.True (clicks > 0, "TabControl.Click did not fire for a click on the tab header");
        }

        [Fact]
        public void Clicking_a_tab_header_also_raises_MouseClick ()
        {
            var (form, tabs) = Build ();
            var clicks = 0;
            tabs.MouseClick += (_, _) => clicks++;

            HeadlessRenderer.Click (form, tabs.Left + 30, tabs.Top + (tabs.TabStrip.Height / 2));

            Assert.True (clicks > 0, "TabControl.MouseClick did not fire for a click on the tab header");
        }

        [Fact]
        public void Selecting_a_different_tab_still_raises_SelectedIndexChanged ()
        {
            // The pre-existing notification must keep working alongside the forwarded click.
            var (form, tabs) = Build ();
            var changed = 0;
            tabs.SelectedIndexChanged += (_, _) => changed++;

            tabs.SelectedIndex = 1;

            Assert.Equal (1, tabs.SelectedIndex);
            Assert.True (changed > 0, "SelectedIndexChanged did not fire");
        }

        [Fact]
        public void Selection_is_already_applied_when_Click_fires ()
        {
            // WinForms updates the selection before raising Click, and migrated handlers read
            // SelectedTab inside Click to decide what to load. If Click arrived first, every handler
            // would act on the tab the user just left.
            var (form, tabs) = Build ();
            string? seen = null;
            tabs.Click += (_, _) => seen = tabs.SelectedTab?.Text;

            var second = tabs.TabPages[1];
            var x = tabs.Left + tabs.GetTabRect (1).Left + 5;
            HeadlessRenderer.Click (form, x, tabs.Top + (tabs.TabStrip.Height / 2));

            Assert.Equal (second.Text, seen);
        }

    }
}
