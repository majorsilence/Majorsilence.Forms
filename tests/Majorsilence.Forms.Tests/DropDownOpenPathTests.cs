using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // ToolStripDropDownItem raises DropDownOpening/DropDownOpened and honours a cancelled Opening, but it
    // declared ShowDropDown with `new` rather than `override`. Every caller that actually opens a menu --
    // MenuItem's Selected setter, MenuDropDown's selection tracking, ToolStripDropDown's Visible setter --
    // holds the item as a MenuItem, so they all bound to the base method: opening a menu by clicking it
    // raised nothing, and only code calling ShowDropDown on the derived type by hand saw the events.
    public class DropDownOpenPathTests
    {
        private static (Form form, MenuStrip strip, ToolStripMenuItem item) BuildMenu ()
        {
            HeadlessRenderer.Use ();

            var form = new Form { ClientSize = new Size (300, 200) };
            var strip = new MenuStrip ();
            var item = new ToolStripMenuItem { Text = "File" };
            item.DropDownItems.Add (new ToolStripMenuItem { Text = "Open" });
            strip.Items.Add (item);
            form.Controls.Add (strip);
            form.Show ();

            return (form, strip, item);
        }

        [Fact]
        public void Opening_a_menu_the_way_the_strip_does_raises_both_events ()
        {
            var (form, _, item) = BuildMenu ();

            using (form) {
                var opening = 0;
                var opened = 0;
                item.DropDownOpening += (_, _) => opening++;
                item.DropDownOpened += (_, _) => opened++;

                // What a click does: the strip selects the item, and the Selected setter opens it. Holding
                // the item as a MenuItem is the point -- that is the binding the strip itself has.
                ((MenuItem)item).Selected = true;

                Assert.Equal (1, opening);
                Assert.Equal (1, opened);
            }
        }

        [Fact]
        public void Closing_a_menu_the_way_the_strip_does_raises_DropDownClosed ()
        {
            var (form, _, item) = BuildMenu ();

            using (form) {
                var closed = 0;
                ((MenuItem)item).Selected = true;
                item.DropDownClosed += (_, _) => closed++;

                ((MenuItem)item).Selected = false;

                Assert.Equal (1, closed);
            }
        }

        [Fact]
        public void A_cancelled_Opening_really_abandons_a_click_driven_open ()
        {
            var (form, _, item) = BuildMenu ();

            using (form) {
                // Opening is the cancellable point in WinForms, and a drop-down that populates itself
                // lazily cancels when it has nothing to show -- so the open has to be abandoned, not
                // merely notified.
                item.DropDown.Opening += (_, e) => e.Cancel = true;

                ((MenuItem)item).Selected = true;

                Assert.False (item.IsDropDownOpened);
            }
        }
    }
}
