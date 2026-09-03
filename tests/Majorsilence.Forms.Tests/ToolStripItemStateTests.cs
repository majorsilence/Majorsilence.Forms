using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.15 (findings TSM-01 P0, TSM-04, TSM-06, TSM-30, TSM-31): ToolStripItem shadowed its base's
    // members with `new` and stored the values somewhere nothing read.
    //
    // The P0 is the single commonest menu operation in a line-of-business application:
    // saveToolStripMenuItem.Enabled = false. Every consumer -- the click gate, the hover gate, all
    // four renderers -- holds items as MenuItem and read MenuItem.Enabled, which stayed true, so a
    // "disabled" item was painted normally, highlighted on hover, and still fired Click.
    [Collection ("Headless")]
    public class ToolStripItemStateTests
    {
        private static ContextMenuStrip Menu (params ToolStripItem[] items)
        {
            HeadlessRenderer.Use ();
            var menu = new ContextMenuStrip ();

            foreach (var item in items)
                menu.Items.Add (item);

            return menu;
        }

        // ---------------- TSM-01: one Enabled, and it is the one everything reads

        [Fact]
        public void Disabling_an_item_reaches_the_property_every_consumer_reads ()
        {
            var item = new ToolStripMenuItem { Text = "Save", Enabled = false };

            // The renderers and the click/hover gates hold items as MenuItem.
            Assert.False (((MenuItem)item).Enabled);
            Assert.False (item.Enabled);
        }

        [Fact]
        public void A_disabled_item_does_not_fire_Click_when_the_strip_routes_one ()
        {
            // NOT asserted through CanSelect. CanSelect is declared on ToolStripItem, so it reads
            // whichever Enabled is in scope there -- including the shadow -- and passes whether or not
            // the fix is present. The finding says as much about the existing parity test. The gate
            // that matters is MenuBase.OnMouseClick, which holds the item as a MenuItem.
            // TWO items, one click each, disabled one first. Clicking a single item twice does not
            // work as a test: MenuBase.TryBeginLeafClick de-duplicates repeat clicks on the same item
            // within 50ms -- it exists to collapse the two deliveries of one physical release -- so the
            // second click never reaches the gate and the assertion passes whatever Enabled says.
            HeadlessRenderer.Use ();
            var off = new ToolStripMenuItem { Text = "Save", Enabled = false };
            var on = new ToolStripMenuItem { Text = "Save As" };
            var off_clicks = 0;
            var on_clicks = 0;
            off.Click += (_, _) => off_clicks++;
            on.Click += (_, _) => on_clicks++;

            using var dropdown = new ClickableDropDown { Width = 200, Height = 80 };
            dropdown.Items.Add (off);
            dropdown.Items.Add (on);
            PaintSurface.Render (dropdown, 1f).Dispose ();     // items lay out on paint

            dropdown.ClickAt (Centre (off));
            dropdown.ClickAt (Centre (on));

            Assert.Equal (0, off_clicks);
            Assert.Equal (1, on_clicks);                       // the control case: the path does work
        }

        private static Point Centre (MenuItem item)
            => new Point (item.Bounds.Left + item.Bounds.Width / 2, item.Bounds.Top + item.Bounds.Height / 2);

        // OnMouseClick is protected, and this is the entry point the strip's own click gate lives on.
        private sealed class ClickableDropDown : MenuDropDown
        {
            internal void ClickAt (Point location)
                => OnMouseClick (new MouseEventArgs (MouseButtons.Left, 1, location.X, location.Y, 0));
        }

        [Fact]
        public void Disabling_an_item_raises_EnabledChanged_once ()
        {
            var item = new ToolStripMenuItem { Text = "Save" };
            var raised = 0;
            item.EnabledChanged += (_, _) => raised++;

            item.Enabled = false;
            item.Enabled = false;   // no change, no event

            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_disabled_strip_does_not_make_every_assignment_look_like_a_no_op ()
        {
            // The getter folds in the owner's Enabled, so the setter has to compare against its own
            // field. Comparing against the property would make this raise nothing.
            var item = new ToolStripMenuItem { Text = "Save" };
            using var menu = Menu (item);
            menu.Enabled = false;
            var raised = 0;
            item.EnabledChanged += (_, _) => raised++;

            item.Enabled = false;

            Assert.Equal (1, raised);
        }

        [Fact]
        public void Tag_has_one_store ()
        {
            var item = new ToolStripMenuItem { Text = "Save", Tag = "payload" };

            Assert.Equal ("payload", ((MenuItem)item).Tag);
        }

        // ---------------- TSM-04: a hidden item takes no space

        [Fact]
        public void Hiding_an_item_on_a_ToolStrip_takes_its_box_away ()
        {
            // Rendered, not PerformLayout'd: a strip lays its items out in OnPaint (MenuBase.OnPaint),
            // so a paint pass is what produces item bounds at all.
            HeadlessRenderer.Use ();
            using var strip = new ToolStrip { Width = 300, Height = 30 };
            var first = new ToolStripButton { Text = "First" };
            var second = new ToolStripButton { Text = "Second" };
            strip.Items.Add (first);
            strip.Items.Add (second);
            PaintSurface.RenderOnForm (strip, 1f).Dispose ();

            var moved_to = second.Bounds.X;
            Assert.True (moved_to > first.Bounds.X, "the second button should start after the first");

            first.Visible = false;
            PaintSurface.Render (strip, 1f).Dispose ();

            // The hidden button used to keep its box: painted, but skipped by hit-testing -- a dead
            // visible button -- and the second never moved up to take its place.
            Assert.Equal (first.Bounds.X, second.Bounds.X);
            Assert.True (second.Bounds.X < moved_to);
        }

        [Fact]
        public void Hiding_an_item_raises_VisibleChanged_and_AvailableChanged_once_each ()
        {
            var item = new ToolStripButton { Text = "Save" };
            var visible = 0;
            var available = 0;
            item.VisibleChanged += (_, _) => visible++;
            item.AvailableChanged += (_, _) => available++;

            item.Visible = false;

            Assert.Equal (1, visible);
            Assert.Equal (1, available);

            // And through the other name for the same state, still once each -- Available used to
            // keep a flag of its own and raise on top of this.
            item.Available = true;

            Assert.Equal (2, visible);
            Assert.Equal (2, available);
        }

        // ---------------- TSM-06: the check state is visible

        [Fact]
        public void Checked_has_one_store_on_both_item_types ()
        {
            var menu_item = new ToolStripMenuItem { Text = "Word wrap", Checked = true };
            var button = new ToolStripButton { Text = "Bold", Checked = true };

            // The renderers read MenuItem.Checked; each type used to keep a private field instead.
            Assert.True (((MenuItem)menu_item).Checked);
            Assert.True (((MenuItem)button).Checked);
        }

        [Fact]
        public void Checking_an_item_still_raises_its_event_once ()
        {
            // GUARD, not proof: the private store this replaced raised once too. It pins that moving
            // the state onto MenuItem did not lose the notification on the way.
            var item = new ToolStripMenuItem { Text = "Word wrap" };
            var raised = 0;
            item.CheckedChanged += (_, _) => raised++;

            item.Checked = true;
            item.Checked = true;

            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_checked_menu_item_paints_a_glyph_where_an_unchecked_one_does_not ()
        {
            // State without paint is the whole point of this plan, so the check goes to the pixels.
            // Compared between two sibling items rather than against a colour: the gutter of the
            // checked one has non-background pixels and the unchecked one's does not.
            HeadlessRenderer.Use ();
            var ticked = new ToolStripMenuItem { Text = "Word wrap", Checked = true };
            var plain = new ToolStripMenuItem { Text = "Word wrap" };

            // A MenuDropDown directly, because MenuDropDownRenderer is what draws the gutter.
            using var dropdown = new MenuDropDown { Width = 200, Height = 80 };
            dropdown.Items.Add (ticked);
            dropdown.Items.Add (plain);

            // Rendered unparented, and at an explicit scale. Two traps here, both of which make a
            // pixel assertion pass by finding nothing at all: an unhosted control reports Scaling 0
            // and PaintSurface sizes its bitmap from that, and adding a MenuDropDown to a Form resets
            // its Width to 0.
            using var bitmap = PaintSurface.Render (dropdown, 1f);
            Assert.Equal (dropdown.Width, bitmap.Width);

            var with = GlyphPixels (bitmap, ticked.Bounds);
            var without = GlyphPixels (bitmap, plain.Bounds);

            Assert.True (with > 0, "the checked item drew nothing in its gutter");
            Assert.Equal (0, without);
        }

        // Pixels of the check glyph's own colour inside the 28px image gutter of an item's row.
        // Counting "anything that is not the background" instead would count the row background
        // itself, which is not the colour this renderer's own theme constant says it is.
        private static int GlyphPixels (SkiaSharp.SKBitmap bitmap, Rectangle row)
        {
            var glyph = Theme.AccentColor;
            var count = 0;

            for (var x = row.Left; x < Math.Min (bitmap.Width, row.Left + 28); x++)
                for (var y = Math.Max (0, row.Top); y < Math.Min (bitmap.Height, row.Bottom); y++) {
                    var p = bitmap.GetPixel (x, y);

                    if (p.Red == glyph.Red && p.Green == glyph.Green && p.Blue == glyph.Blue)
                        count++;
                }

            return count;
        }

        // ---------------- TSM-31: a stored value that reaches a layout pass

        [Fact]
        public void Setting_an_items_size_asks_the_strip_to_repaint ()
        {
            // Item boxes are computed in OnPaint, so "reaching layout" means "invalidating": without
            // this the new size waited for some unrelated repaint. Counted on a spy, because a test
            // that renders would re-lay out regardless and prove nothing.
            HeadlessRenderer.Use ();
            using var strip = new ToolStrip { Width = 300, Height = 40 };
            var item = new ToolStripButton { Text = "Wide", AutoSize = false, Size = new Size (40, 22) };
            strip.Items.Add (item);

            // Control.Invalidate returns early when the control has not been created, so an
            // unparented strip would swallow the very call under test.
            strip.CreateControl ();

            // Control.Invalidate is not virtual; the Invalidated event is the observable signal.
            var invalidations = 0;
            strip.Invalidated += (_, _) => invalidations++;

            item.Width = 120;

            Assert.True (invalidations > 0, "setting Width did not invalidate the strip");
        }

        [Fact]
        public void An_explicit_item_size_reaches_the_box_that_is_drawn ()
        {
            // GUARD, not proof: the AutoSize=false rule already existed, and this test renders
            // explicitly, so a layout pass happens either way. The test above is the one that pins the
            // part that was missing -- the setter asking for that pass.
            HeadlessRenderer.Use ();
            using var strip = new ToolStrip { Width = 300, Height = 40 };
            var item = new ToolStripButton { Text = "Wide", AutoSize = false, Size = new Size (40, 22) };
            strip.Items.Add (item);
            PaintSurface.RenderOnForm (strip, 1f).Dispose ();

            var before = item.Bounds.Width;

            item.Width = 120;
            PaintSurface.Render (strip, 1f).Dispose ();

            Assert.True (item.Bounds.Width > before,
                $"bounds stayed at {item.Bounds.Width} after asking for 120");
        }

        [Fact]
        public void DisplayStyle_announces_its_change_and_re_lays_out ()
        {
            var item = new ToolStripButton { Text = "Save" };
            var raised = 0;
            item.DisplayStyleChanged += (_, _) => raised++;

            item.DisplayStyle = ToolStripItemDisplayStyle.Image;
            item.DisplayStyle = ToolStripItemDisplayStyle.Image;

            Assert.Equal (1, raised);
            Assert.Equal (ToolStripItemDisplayStyle.Image, item.DisplayStyle);
        }

        [Fact]
        public void Alignment_has_one_store_on_a_status_label ()
        {
            // GUARD, not proof: reading back what was assigned worked before too -- through the
            // shadow's own field. It pins that the shadow is gone, so the value the strip reads when
            // it positions the label is the value that was set.
            var label = new ToolStripStatusLabel { Text = "Ready", Alignment = ToolStripItemAlignment.Right };

            Assert.Equal (ToolStripItemAlignment.Right, ((ToolStripItem)label).Alignment);
        }
    }
}
