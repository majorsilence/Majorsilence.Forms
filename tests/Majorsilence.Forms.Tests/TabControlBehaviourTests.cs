using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.23 -- TabControl. Covers the four layout findings this control carried:
    //
    //   LAY-12  TabPage.Enabled was a `new` auto-property with its own backing field, so the page and
    //           its Control face disagreed and the page's children stayed interactive.
    //   LAY-13  the four selection events fired in the order Deselecting, Selecting, Deselected,
    //           SelectedIndexChanged, Selected -- Windows uses Deselecting, Deselected (both while the
    //           outgoing tab is still current), then Selecting, Selected, SelectedIndexChanged.
    //   LAY-14  TabControl.ImageList and TabPage.ImageIndex/ImageKey were three stores nothing read.
    //   LAY-15  Alignment / ItemSize / SizeMode / Padding were stored-only, and GetTabRect answered in
    //           the strip's coordinates rather than the control's.
    //
    // The assertions are relationships between measurements -- an imaged tab against the same tab
    // without an image, a bottom-aligned strip against the page it sits below -- rather than the pixel
    // numbers a particular font happens to produce.
    public class TabControlBehaviourTests
    {
        // A parentless TabControl positions its docked strip in its own layout pass, but the strip
        // measures and wraps its tabs in the strip's. Run the outer pass, the strip's, then the outer
        // one again so a strip that resized itself (a side alignment sets its own width) has moved the
        // pages before anything is measured.
        private static void Layout (TabControl control)
        {
            control.PerformLayout ();
            control.TabStrip.PerformLayout ();
            control.PerformLayout ();
        }

        private static SKBitmap Swatch (SKColor color, int size = 16)
        {
            var bitmap = new SKBitmap (size, size);

            using (var canvas = new SKCanvas (bitmap))
                canvas.Clear (color);

            return bitmap;
        }

        // Distinctive enough that nothing the theme paints can be mistaken for it.
        private static readonly SKColor icon_color = new SKColor (0xFF, 0x00, 0xFF);

        private static bool IsIcon (SKColor color) => color.Red > 200 && color.Blue > 200 && color.Green < 60;

        // ---------------------------------------------------------------- LAY-12: TabPage.Enabled

        [Fact]
        public void Disabling_a_page_disables_it_through_its_Control_face_too ()
        {
            using var control = new TabControl ();
            using var page = new TabPage ("One");
            control.TabPages.Add (page);

            page.Enabled = false;

            // The whole defect: `new bool Enabled { get; set; }` forked the state, so the same page
            // answered false through TabPage and true through Control -- and anything that consults
            // Enabled to decide whether to route input consults the Control one.
            Assert.False (page.Enabled);
            Assert.False (((Control)page).Enabled);

            page.Enabled = true;

            Assert.True (page.Enabled);
            Assert.True (((Control)page).Enabled);
        }

        [Fact]
        public void Disabling_a_page_disables_the_controls_on_it ()
        {
            using var control = new TabControl ();
            using var page = new TabPage ("One");
            var child = new Button { Text = "Save" };
            page.Controls.Add (child);
            control.TabPages.Add (page);

            Assert.True (child.Enabled);

            page.Enabled = false;

            // Control.Enabled is ambient, which is the entire point of routing through the base: locking
            // a wizard step by disabling its page has to lock the controls on that step.
            Assert.False (child.Enabled);

            page.Enabled = true;

            Assert.True (child.Enabled);
        }

        [Fact]
        public void Disabling_a_page_raises_EnabledChanged_on_its_children ()
        {
            using var control = new TabControl ();
            using var page = new TabPage ("One");
            var child = new Button ();
            page.Controls.Add (child);
            control.TabPages.Add (page);

            var changed = 0;
            child.EnabledChanged += (_, _) => changed++;

            page.Enabled = false;

            // A store into a private field cannot notify anything; going through Control.Enabled walks
            // the subtree and tells each child its effective value moved.
            Assert.Equal (1, changed);
        }

        [Fact]
        public void Disabling_a_page_leaves_its_tab_header_selectable ()
        {
            // GUARD, not proof: no previous version could fail this, because TabPage.Enabled was inert.
            // It is here to pin the correction to LAY-12's suggested fix -- see the note in
            // docs/behaviour-gap/layout.md. Upstream's TabPage.Enabled is Control.Enabled and nothing
            // more; the native tab control has no notion of a disabled tab, so the header stays
            // clickable and ungreyed and only the page's contents go dead. Linking TabStripItem.Enabled
            // (which the finding proposes) would make the tab unselectable, which Windows does not do.
            using var control = new TabControl ();
            using var page1 = new TabPage ("One");
            using var page2 = new TabPage ("Two");
            control.TabPages.Add (page1);
            control.TabPages.Add (page2);

            page2.Enabled = false;
            control.SelectedIndex = 1;

            Assert.Same (page2, control.SelectedTab);
            Assert.True (page2.TabStripItem.Enabled);
        }

        // ------------------------------------------------- LAY-13: the four selection events' order

        private static (TabControl control, TabPage first, TabPage second, List<string> log) BuildLogged ()
        {
            var control = new TabControl ();
            var first = new TabPage ("One");
            var second = new TabPage ("Two");
            control.TabPages.Add (first);
            control.TabPages.Add (second);

            // The four events (and SelectedIndexChanged) are suppressed until the handle exists, so that
            // tabs added during InitializeComponent do not run a half-constructed form's handlers.
            control.CreateControl ();

            var log = new List<string> ();
            control.Deselecting += (_, _) => log.Add ("Deselecting");
            control.Deselected += (_, _) => log.Add ("Deselected");
            control.Selecting += (_, _) => log.Add ("Selecting");
            control.Selected += (_, _) => log.Add ("Selected");
            control.SelectedIndexChanged += (_, _) => log.Add ("SelectedIndexChanged");

            return (control, first, second, log);
        }

        [Fact]
        public void Selection_events_fire_in_the_Windows_order ()
        {
            var (control, _, _, log) = BuildLogged ();

            using (control) {
                control.SelectedIndex = 1;

                // Two reorderings in one sequence: Deselected moves up in front of Selecting (upstream
                // raises both halves of the deselect from TCN_SELCHANGING, before the control moves),
                // and Selected moves in front of SelectedIndexChanged (upstream raises them in that
                // order from TCN_SELCHANGE).
                Assert.Equal (new[] { "Deselecting", "Deselected", "Selecting", "Selected", "SelectedIndexChanged" }, log);
            }
        }

        [Fact]
        public void Deselect_events_see_the_outgoing_page_and_select_events_the_incoming_one ()
        {
            var (control, first, second, _) = BuildLogged ();

            using (control) {
                var observed = new List<(string phase, TabPage? current, TabPage? argument)> ();

                control.Deselecting += (_, e) => observed.Add (("Deselecting", control.SelectedTab, e.TabPage));
                control.Deselected += (_, e) => observed.Add (("Deselected", control.SelectedTab, e.TabPage));
                control.Selecting += (_, e) => observed.Add (("Selecting", control.SelectedTab, e.TabPage));
                control.Selected += (_, e) => observed.Add (("Selected", control.SelectedTab, e.TabPage));

                control.SelectedIndex = 1;

                // The reason the order matters. A Deselecting/Deselected handler exists to save the state
                // of the tab being left, and it reads SelectedTab to find out which tab that is; both
                // used to run after the strip had already moved, so it was handed the tab being entered.
                Assert.Equal (("Deselecting", first, first), observed[0]);
                Assert.Equal (("Deselected", first, first), observed[1]);

                // Selecting/Selected are the other side of the pair: upstream raises them once the
                // control has moved, so these do see the incoming page.
                Assert.Equal (("Selecting", second, second), observed[2]);
                Assert.Equal (("Selected", second, second), observed[3]);
            }
        }

        [Fact]
        public void Cancelling_Selecting_reverts_the_selection_and_stops_the_sequence ()
        {
            var (control, first, _, log) = BuildLogged ();

            using (control) {
                control.Selecting += (_, e) => e.Cancel = true;

                control.SelectedIndex = 1;

                // The deselect half has already happened and is not undone (upstream's TCN_SELCHANGING
                // ran and returned "allowed"); what stops is everything after Selecting.
                Assert.Equal (new[] { "Deselecting", "Deselected", "Selecting" }, log);
                Assert.Equal (0, control.SelectedIndex);
                Assert.Same (first, control.SelectedTab);
            }
        }

        [Fact]
        public void Cancelling_Deselecting_keeps_the_selection_and_suppresses_Deselected ()
        {
            // GUARD, not proof: the old handler also bailed out before Deselected when Deselecting
            // cancelled, so no previous version could fail this. It pins the half of the new two-phase
            // shape that is easy to lose -- the veto now happens before the strip moves at all, so a
            // cancelled Deselecting has to leave no trace rather than move and move back.
            var (control, first, _, log) = BuildLogged ();

            using (control) {
                control.Deselecting += (_, e) => e.Cancel = true;

                control.SelectedIndex = 1;

                Assert.Equal (new[] { "Deselecting" }, log);
                Assert.Equal (0, control.SelectedIndex);
                Assert.Same (first, control.SelectedTab);
            }
        }

        // ----------------------------------------- LAY-14: ImageList + TabPage.ImageIndex/ImageKey

        [Fact]
        public void A_tab_with_an_image_is_wider_than_the_same_tab_without_one ()
        {
            using var swatch = Swatch (icon_color);
            using var images = new ImageList ();
            images.Images.Add ("icon", swatch);

            using var plain = new TabControl { Width = 400, Height = 300 };
            plain.TabPages.Add (new TabPage ("One"));
            Layout (plain);

            using var imaged = new TabControl { Width = 400, Height = 300, ImageList = images };
            imaged.TabPages.Add (new TabPage ("One") { ImageIndex = 0 });
            Layout (imaged);

            var plain_rect = plain.GetTabRect (0);
            var imaged_rect = imaged.GetTabRect (0);

            // Same caption, same font, same padding: the whole difference is the icon and the gap
            // beside it. Asserted as an exact difference rather than "wider", so a change that widened
            // the tab by the wrong amount (or by the padding twice) still fails.
            Assert.True (plain_rect.Width > 0, $"the text-only tab must have been laid out, was {plain_rect}");
            Assert.Equal (plain_rect.Width + images.ImageSize.Width + TabStripItem.IMAGE_TEXT_GAP, imaged_rect.Width);
        }

        [Fact]
        public void The_tab_image_is_painted_inside_the_tab ()
        {
            using var swatch = Swatch (icon_color);
            using var images = new ImageList ();
            images.Images.Add ("icon", swatch);

            using var control = new TabControl { Width = 400, Height = 300, ImageList = images };
            control.TabPages.Add (new TabPage ("One") { ImageIndex = 0 });
            Layout (control);

            using var bitmap = PaintSurface.Render (control.TabStrip, 1f);

            // A 0x0 surface would make every pixel assertion below vacuously true.
            Assert.Equal (control.TabStrip.Width, bitmap.Width);
            Assert.True (bitmap.Height > 0);

            var tab = control.GetTabRect (0);
            var painted = 0;
            var outside = 0;

            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++) {
                    if (!IsIcon (bitmap.GetPixel (x, y)))
                        continue;

                    painted++;

                    if (!tab.Contains (x, y))
                        outside++;
                }

            // The whole icon reaches the surface, and all of it lands within the tab that owns it.
            Assert.Equal (images.ImageSize.Width * images.ImageSize.Height, painted);
            Assert.Equal (0, outside);
        }

        [Fact]
        public void ImageKey_resolves_through_the_ImageList_by_name ()
        {
            using var first = Swatch (new SKColor (0x10, 0x20, 0x30));
            using var second = Swatch (icon_color);
            using var images = new ImageList ();
            images.Images.Add ("other", first);
            images.Images.Add ("wanted", second);

            using var control = new TabControl { Width = 400, Height = 300, ImageList = images };
            var page = new TabPage ("One") { ImageKey = "wanted" };
            control.TabPages.Add (page);
            Layout (control);

            // Named lookup, not "the first image": the key has to pick out its own entry.
            using var bitmap = PaintSurface.Render (control.TabStrip, 1f);
            Assert.True (bitmap.Width > 0);

            var painted = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (IsIcon (bitmap.GetPixel (x, y)))
                        painted++;

            Assert.Equal (images.ImageSize.Width * images.ImageSize.Height, painted);
        }

        [Fact]
        public void The_ImageList_can_arrive_after_the_pages_do ()
        {
            using var swatch = Swatch (icon_color);
            using var images = new ImageList ();
            images.Images.Add ("icon", swatch);

            using var control = new TabControl { Width = 400, Height = 300 };
            control.TabPages.Add (new TabPage ("One") { ImageIndex = 0 });
            Layout (control);

            var before = control.GetTabRect (0).Width;

            // Designer files assign the ImageList after AddRange as often as before it, so neither order
            // may lose the icons; the width moving is the observable proof the image resolved.
            control.ImageList = images;
            Layout (control);

            Assert.Equal (before + images.ImageSize.Width + TabStripItem.IMAGE_TEXT_GAP, control.GetTabRect (0).Width);

            // And clearing it puts the tab back to its text-only width rather than leaving a stale icon.
            control.ImageList = null;
            Layout (control);

            Assert.Equal (before, control.GetTabRect (0).Width);
        }

        [Fact]
        public void An_ImageIndex_past_the_end_of_the_ImageList_leaves_the_tab_text_only ()
        {
            // GUARD, not proof: no previous version could fail this. Before W5.23 no ImageIndex of any
            // value reached the tab, so an out-of-range one was indistinguishable from a valid one --
            // both showed nothing. It guards the range check that the new resolution path needs and that
            // an "index into the list" implementation is easy to write without: images[7] on a
            // one-image list throws, and it would throw from inside a layout pass.
            using var swatch = Swatch (icon_color);
            using var images = new ImageList ();
            images.Images.Add ("icon", swatch);

            using var plain = new TabControl { Width = 400, Height = 300, ImageList = images };
            plain.TabPages.Add (new TabPage ("One"));
            Layout (plain);

            using var control = new TabControl { Width = 400, Height = 300, ImageList = images };
            control.TabPages.Add (new TabPage ("One") { ImageIndex = 7 });
            Layout (control);

            // An out-of-range index is a no-image tab, not a throw and not the last image.
            Assert.Equal (plain.GetTabRect (0).Width, control.GetTabRect (0).Width);
        }

        // ------------------------------- LAY-15: Alignment / ItemSize / SizeMode / Padding wiring

        [Fact]
        public void Alignment_Bottom_puts_the_tabs_below_the_page ()
        {
            using var control = new TabControl { Width = 400, Height = 300 };
            var page = new TabPage ("One");
            control.TabPages.Add (page);
            Layout (control);

            // Baseline: the default keeps the header above the page.
            Assert.True (control.GetTabRect (0).Bottom <= page.Bounds.Top,
                $"top-aligned tab {control.GetTabRect (0)} should sit above the page {page.Bounds}");

            control.Alignment = TabAlignment.Bottom;
            Layout (control);

            // The relationship, not a pixel row: the header band and the page swap places, and
            // GetTabRect reports in the control's coordinates so the two are comparable at all.
            Assert.True (control.GetTabRect (0).Top >= page.Bounds.Bottom,
                $"bottom-aligned tab {control.GetTabRect (0)} should sit below the page {page.Bounds}");
        }

        [Fact]
        public void Alignment_Left_stacks_the_tabs_in_a_column_beside_the_page ()
        {
            using var control = new TabControl { Width = 400, Height = 300 };
            var first = new TabPage ("One");
            control.TabPages.Add (first);
            control.TabPages.Add (new TabPage ("Two"));

            control.Alignment = TabAlignment.Left;
            Layout (control);

            var top = control.GetTabRect (0);
            var below = control.GetTabRect (1);

            // One column: same left edge, second tab under the first, and the page starts to the right
            // of the whole column.
            Assert.Equal (top.Left, below.Left);
            Assert.Equal (top.Width, below.Width);
            Assert.True (below.Top >= top.Bottom, $"tab 1 {below} should sit under tab 0 {top}");
            Assert.True (first.Bounds.Left >= top.Right, $"page {first.Bounds} should start right of the tab column {top}");
        }

        [Fact]
        public void SizeMode_Fixed_gives_every_tab_the_ItemSize_width ()
        {
            using var control = new TabControl { Width = 400, Height = 300 };
            control.TabPages.Add (new TabPage ("A"));
            control.TabPages.Add (new TabPage ("A much longer caption"));
            Layout (control);

            // Captions this different must produce different widths first, or "all equal" below proves
            // nothing about SizeMode.
            Assert.NotEqual (control.GetTabRect (0).Width, control.GetTabRect (1).Width);

            control.ItemSize = new Size (120, 0);
            control.SizeMode = TabSizeMode.Fixed;
            Layout (control);

            Assert.Equal (120, control.GetTabRect (0).Width);
            Assert.Equal (120, control.GetTabRect (1).Width);

            // Laid end to end, so the second tab starts where the first ends.
            Assert.Equal (control.GetTabRect (0).Right, control.GetTabRect (1).Left);
        }

        [Fact]
        public void ItemSize_Height_sets_the_height_of_the_header_band ()
        {
            using var control = new TabControl { Width = 400, Height = 300 };
            var page = new TabPage ("One");
            control.TabPages.Add (page);
            Layout (control);

            var default_height = control.GetTabRect (0).Height;
            Assert.True (default_height > 0);

            control.ItemSize = new Size (0, default_height + 20);
            Layout (control);

            // The tab grows, the strip grows with it, and the page is pushed down by the same amount --
            // a tab height the band did not follow would clip the tabs.
            Assert.Equal (default_height + 20, control.GetTabRect (0).Height);
            Assert.Equal (control.GetTabRect (0).Bottom, page.Bounds.Top);
        }

        [Fact]
        public void SizeMode_FillToRight_stretches_a_row_of_tabs_to_the_strip_width ()
        {
            using var control = new TabControl { Width = 400, Height = 300 };
            control.TabPages.Add (new TabPage ("A"));
            control.TabPages.Add (new TabPage ("A much longer caption"));
            Layout (control);

            var natural = control.GetTabRect (0).Width + control.GetTabRect (1).Width;
            Assert.True (natural < control.Width, "the two tabs must leave slack for FillToRight to hand out");

            control.SizeMode = TabSizeMode.FillToRight;
            Layout (control);

            var stretched = control.GetTabRect (0).Width + control.GetTabRect (1).Width;

            // The row ends exactly on the strip's edge, which is what makes the mode worth having: the
            // rounding remainder has to land somewhere rather than leaving a gap.
            Assert.Equal (control.TabStrip.ClientRectangle.Width, stretched);
            Assert.Equal (control.GetTabRect (0).Right, control.GetTabRect (1).Left);
            Assert.True (stretched > natural);
        }

        [Fact]
        public void Padding_insets_every_tab_in_both_directions ()
        {
            using var control = new TabControl { Width = 400, Height = 300 };
            control.TabPages.Add (new TabPage ("One"));
            Layout (control);

            var natural = control.GetTabRect (0);

            control.Padding = new Point (9, 5);
            Layout (control);

            var padded = control.GetTabRect (0);

            // Both components of the Point are consumed, each on both sides of the tab -- the shape
            // upstream's TabControl.Padding has.
            Assert.Equal (natural.Width + 18, padded.Width);
            Assert.Equal (natural.Height + 10, padded.Height);
        }
    }
}
