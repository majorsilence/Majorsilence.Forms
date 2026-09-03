using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.16 (findings TSM-03 P0, TSM-08, and the menu-lifecycle half of TSM-30).
    //
    // TSM-03 is a coordinate-space bug with a canonical victim: contextMenuStrip1.Show (button1,
    // new Point (0, button1.Height)) -- a menu under a button -- opened at the top-left of the
    // SCREEN, because the overload treated a client point as a screen point. Every internal caller
    // pre-converted with PointToScreen to compensate, which is what showed the API was wrong.
    //
    // TSM-08 is a facade bypass. ItemAdded/ItemRemoved/ItemClicked were wired inside the
    // ToolStripItemCollection facade, but Menu.Items and MenuDropDown.Items re-expose the underlying
    // collection directly, so a MenuStrip or ContextMenuStrip never went through it: the very common
    // one-handler pattern contextMenuStrip.ItemClicked += ... never fired.
    [Collection ("Headless")]
    public class StripFacadeAndCoordinatesTests
    {
        // ---------------- TSM-03: Show takes client coordinates

        [Fact]
        public void Showing_a_context_menu_treats_the_point_as_client_coordinates ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            var child = new Panel { Left = 100, Top = 100, Width = 80, Height = 24 };
            form.Controls.Add (child);
            form.Show ();

            try {
                using var menu = new ContextMenuStrip ();
                menu.Items.Add (new ToolStripMenuItem { Text = "Cut" });

                var client = new Point (0, child.Height);
                menu.Show (child, client);

                // The drop-down-under-a-button idiom: the menu's top-left is the child's own
                // bottom-left in screen space, not the screen's origin.
                Assert.Equal (child.PointToScreen (client), MenuLocation (menu));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_screen_space_overload_is_unchanged ()
        {
            // GUARD, not proof: this overload was already right and is untouched. It is here because
            // the obvious way to "fix" TSM-03 is to convert in ShowCore, which would have broken this
            // one instead -- the two overloads mean different things.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            form.Show ();

            try {
                using var menu = new ContextMenuStrip ();
                menu.Items.Add (new ToolStripMenuItem { Text = "Cut" });

                var screen = new Point (222, 111);
                menu.Show (screen);

                Assert.Equal (screen, MenuLocation (menu));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void The_alignment_overload_offsets_in_the_same_space ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            var child = new Panel { Left = 60, Top = 40, Width = 80, Height = 24 };
            form.Controls.Add (child);
            form.Show ();

            try {
                using var menu = new ContextMenu ();
                menu.Items.Add (new MenuItem { Text = "Cut" });

                var client = new Point (10, 10);
                menu.Show (child, client, LeftRightAlignment.Right);

                Assert.Equal (child.PointToScreen (client), MenuLocation (menu));
            } finally {
                form.Close ();
            }
        }

        private static Point MenuLocation (ContextMenu menu)
        {
            // The popup window the drop-down lives in is what carries the screen position.
            var popup = menu.FindWindow ();

            Assert.NotNull (popup);

            return popup!.Location;
        }

        // ---------------- TSM-08: the events fire on menus, not just on a plain ToolStrip

        [Fact]
        public void Adding_an_item_to_a_context_menu_raises_ItemAdded ()
        {
            HeadlessRenderer.Use ();
            using var menu = new ContextMenuStrip ();
            var added = 0;
            ToolStripItem? reported = null;
            menu.ItemAdded += (_, e) => { added++; reported = e.Item; };

            var item = new ToolStripMenuItem { Text = "Cut" };
            menu.Items.Add (item);

            Assert.Equal (1, added);
            Assert.Same (item, reported);
        }

        [Fact]
        public void Removing_an_item_from_a_context_menu_raises_ItemRemoved ()
        {
            HeadlessRenderer.Use ();
            using var menu = new ContextMenuStrip ();
            var item = new ToolStripMenuItem { Text = "Cut" };
            menu.Items.Add (item);
            var removed = 0;
            menu.ItemRemoved += (_, _) => removed++;

            menu.Items.Remove (item);

            Assert.Equal (1, removed);
        }

        [Fact]
        public void A_menu_strip_raises_ItemClicked_for_the_item_that_was_clicked ()
        {
            // The one-handler pattern: menuStrip.ItemClicked += (s, e) => switch (e.ClickedItem.Name).
            HeadlessRenderer.Use ();
            using var strip = new MenuStrip ();
            var first = new ToolStripMenuItem { Text = "File" };
            var second = new ToolStripMenuItem { Text = "Edit" };
            strip.Items.Add (first);
            strip.Items.Add (second);
            ToolStripItem? clicked = null;
            var count = 0;
            strip.ItemClicked += (_, e) => { count++; clicked = e.ClickedItem; };

            second.PerformClick ();

            Assert.Equal (1, count);
            Assert.Same (second, clicked);
        }

        [Fact]
        public void A_context_menu_raises_ItemClicked_when_a_click_is_routed_to_an_item ()
        {
            HeadlessRenderer.Use ();
            var item = new ToolStripMenuItem { Text = "Cut" };
            using var dropdown = new ClickableDropDown { Width = 200, Height = 60 };
            dropdown.Items.Add (item);
            PaintSurface.Render (dropdown, 1f).Dispose ();     // items lay out on paint

            var count = 0;
            dropdown.ItemClicked += (_, _) => count++;

            dropdown.ClickAt (new Point (item.Bounds.Left + item.Bounds.Width / 2,
                                         item.Bounds.Top + item.Bounds.Height / 2));

            Assert.Equal (1, count);
        }

        [Fact]
        public void An_item_taken_out_and_put_back_still_reports_one_click ()
        {
            // The relay is attached per add, so a re-added item would report twice if the previous
            // subscription were not removed. A lambda could not have been removed at all.
            HeadlessRenderer.Use ();
            using var strip = new MenuStrip ();
            var item = new ToolStripMenuItem { Text = "File" };
            strip.Items.Add (item);
            strip.Items.Remove (item);
            strip.Items.Add (item);
            var count = 0;
            strip.ItemClicked += (_, _) => count++;

            item.PerformClick ();

            Assert.Equal (1, count);
        }

        [Fact]
        public void A_plain_ToolStrip_still_raises_its_events ()
        {
            // Not a guard, as it turns out: the notifications moved OUT of the facade this path used,
            // so neutralizing the new plumbing breaks this test too. It is the regression half of the
            // change -- the path that already worked has to keep working -- and it discriminates.
            HeadlessRenderer.Use ();
            using var strip = new ToolStrip ();
            var added = 0;
            var clicked = 0;
            strip.ItemAdded += (_, _) => added++;
            strip.ItemClicked += (_, _) => clicked++;

            var item = new ToolStripButton { Text = "Save" };
            strip.Items.Add (item);
            item.PerformClick ();

            Assert.Equal (1, added);
            Assert.Equal (1, clicked);
        }

        // ---------------- TSM-30: the menu lifecycle events

        [Fact]
        public void Showing_a_context_menu_raises_Popup_before_Opening ()
        {
            // Popup is the legacy hook for enabling items just before display, so it has to run before
            // anything can cancel the open.
            // Anchored on a child control, not the form: Form does not derive from Control here.
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            var host = new Panel { Left = 10, Top = 10, Width = 100, Height = 40 };
            form.Controls.Add (host);
            form.Show ();

            try {
                using var menu = new ContextMenu ();
                menu.Items.Add (new MenuItem { Text = "Cut" });
                var order = string.Empty;
                menu.Popup += (_, _) => order += "P";
                menu.Opening += (_, _) => order += "O";

                menu.Show (host, new Point (10, 10));

                Assert.Equal ("PO", order);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Dismissing_a_context_menu_raises_Collapse ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            var host = new Panel { Left = 10, Top = 10, Width = 100, Height = 40 };
            form.Controls.Add (host);
            form.Show ();

            try {
                using var menu = new ContextMenu ();
                menu.Items.Add (new MenuItem { Text = "Cut" });
                var collapsed = 0;
                menu.Collapse += (_, _) => collapsed++;

                menu.Show (host, new Point (10, 10));
                menu.Deactivate ();

                Assert.Equal (1, collapsed);

                // And not again for a menu that is already closed.
                menu.Deactivate ();

                Assert.Equal (1, collapsed);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Opening_a_submenu_raises_the_items_Popup ()
        {
            HeadlessRenderer.Use ();
            using var strip = new MenuStrip { Width = 200, Height = 24 };
            using var form = new Form { Width = 400, Height = 300 };
            form.Controls.Add (strip);
            form.Show ();

            try {
                var file = new ToolStripMenuItem { Text = "File" };
                file.DropDownItems.Add (new ToolStripMenuItem { Text = "Open" });
                strip.Items.Add (file);
                var popped = 0;
                file.Popup += (_, _) => popped++;

                file.ShowDropDown ();

                Assert.Equal (1, popped);
            } finally {
                form.Close ();
            }
        }

        private sealed class ClickableDropDown : MenuDropDown
        {
            internal void ClickAt (Point location)
                => OnMouseClick (new MouseEventArgs (MouseButtons.Left, 1, location.X, location.Y, 0));
        }
    }
}
