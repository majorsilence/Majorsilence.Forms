using System.Linq;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the stored-only sweep -- the ToolStrip family slice. 88 baseline entries across the
    // family; five had a consumer sitting right next to them:
    //
    //   ShowShortcutKeys + ShortcutKeyDisplayString  the DISPLAY half of a shortcut system that has
    //                                                really worked since W1.3 -- the accelerator
    //                                                fired and was invisible in the menu
    //   ToolStripDropDownButton.ShowDropDownArrow    the arrow was drawn whenever the item had a
    //                                                submenu, whatever the flag said
    //   ToolStripStatusLabel.Spring                  every status item took a fixed width, so a
    //                                                "spring" label did not reach the right edge
    //   ToolStripItem.Alignment                      items were laid out left to right in
    //                                                declaration order, whatever the property said
    //
    // This is the register's own systemic finding made concrete: "renderers keyed on concrete control
    // type read only Text/ImageSK/Enabled/Hovered/HasItems" (TSM-06/07/12/16/23/37).
    [Collection ("Headless")]
    public class ToolStripStoredOnlyTests
    {
        // ---------------- the shortcut display half

        [Fact]
        public void A_shortcut_key_produces_menu_text ()
        {
            var item = new ToolStripMenuItem ("Save") { ShortcutKeys = Keys.Control | Keys.S };

            Assert.Equal ("Ctrl+S", item.ShortcutDisplayText);
        }

        [Fact]
        public void An_explicit_display_string_wins ()
        {
            // The whole point of ShortcutKeyDisplayString: "Del" where the key is Keys.Delete, or a
            // localised caption. Without it the property is decoration.
            var item = new ToolStripMenuItem ("Delete") {
                ShortcutKeys = Keys.Delete,
                ShortcutKeyDisplayString = "Del"
            };

            Assert.Equal ("Del", item.ShortcutDisplayText);
        }

        [Fact]
        public void ShowShortcutKeys_false_shows_nothing ()
        {
            // The half that only a read property can do: the accelerator still works, it is just not
            // advertised. Both sources are suppressed, not only the formatted one.
            var keyed = new ToolStripMenuItem ("Save") { ShortcutKeys = Keys.Control | Keys.S, ShowShortcutKeys = false };
            var explicitly = new ToolStripMenuItem ("Delete") { ShortcutKeyDisplayString = "Del", ShowShortcutKeys = false };

            Assert.Equal (string.Empty, keyed.ShortcutDisplayText);
            Assert.Equal (string.Empty, explicitly.ShortcutDisplayText);
        }

        [Fact]
        public void An_item_with_no_shortcut_shows_nothing ()
        {
            // GUARD: a default ToolStripMenuItem has ShowShortcutKeys = true, so an implementation
            // that formatted Keys.None unconditionally would put "None" beside every menu caption.
            var item = new ToolStripMenuItem ("Open");

            Assert.Equal (string.Empty, item.ShortcutDisplayText);
        }

        [Fact]
        public void The_shortcut_is_painted_in_the_drop_down ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var strip = new MenuStrip ();
            var file = new ToolStripMenuItem ("File");
            var save = new ToolStripMenuItem ("Save") { ShortcutKeys = Keys.Control | Keys.S };
            file.DropDownItems.Add (save);
            strip.Items.Add (file);
            form.Controls.Add (strip);
            form.Show ();

            try {
                file.ShowDropDown ();
                Majorsilence.Forms.Backends.Platform.Backend.DoEvents ();

                var dropdown = file.OpenDropDown;
                Assert.NotNull (dropdown);

                // The item's whole box, for the same reason as ArrowZone: MenuDropDownRenderer also
                // offsets a LOGICAL bounds edge by a DEVICE-converted constant, so a sub-rectangle
                // derived from those constants stops matching where the text lands once the scale is
                // not 1. The caption does not change between the two renders, so only the shortcut can.
                var gutter = Device (dropdown!, save.Bounds);

                var withShortcut = Zone (dropdown!, gutter);

                save.ShowShortcutKeys = false;

                Assert.NotEqual (withShortcut, Zone (dropdown!, gutter));
            } finally {
                Application.ActivePopupWindow?.Close ();
                form.Close ();
            }
        }

        // ---------------- ShowDropDownArrow

        [Fact]
        public void ShowDropDownArrow_false_suppresses_the_arrow ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 200 };
            var strip = new ToolStrip { Width = 300, Height = 30 };
            var button = new ToolStripDropDownButton { Text = "More" };
            button.DropDownItems.Add (new ToolStripMenuItem ("One"));
            strip.Items.Add (button);
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();   // lays the items out; Bounds is 0,0,0,0 until then

            try {
                var arrow = ArrowZone (strip, button);
                var shown = Zone (strip, arrow);

                button.ShowDropDownArrow = false;

                Assert.NotEqual (shown, Zone (strip, arrow));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_plain_item_with_children_still_draws_its_arrow ()
        {
            // GUARD: only ToolStripDropDownButton carries the flag. Suppressing the arrow for every
            // item type would take it off the menu bar and the split buttons too.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 200 };
            var strip = new ToolStrip { Width = 300, Height = 30 };
            var item = new ToolStripMenuItem ("More");
            item.DropDownItems.Add (new ToolStripMenuItem ("One"));
            strip.Items.Add (item);
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            try {
                // Compared against the same item with its submenu removed: only the arrow can differ,
                // and it must still be drawn because the flag belongs to ToolStripDropDownButton alone.
                var withChildren = Zone (strip, ArrowZone (strip, item));

                item.DropDownItems.Clear ();
                strip.PerformLayout ();

                Assert.NotEqual (withChildren, Zone (strip, ArrowZone (strip, item)));
            } finally {
                form.Close ();
            }
        }

        // The item's whole box, in DEVICE pixels because that is what the bitmap is in.
        //
        // Not a computed arrow sub-rectangle: ToolBarRenderer positions the arrow by subtracting a
        // DEVICE-converted constant from a LOGICAL bounds edge, so the glyph drifts left as the scale
        // rises and any rectangle derived from those constants misses it at scale 2. That mixing is a
        // real defect of the W6.3 class and is recorded separately; this test is about whether the
        // property is read, so it asks the question that does not depend on where the glyph lands.
        private static Rectangle ArrowZone (Control owner, MenuItem item) => Device (owner, item.Bounds);

        private static Rectangle Device (Control owner, Rectangle logical)
            => new (owner.LogicalToDeviceUnits (logical.X), owner.LogicalToDeviceUnits (logical.Y),
                    owner.LogicalToDeviceUnits (logical.Width), owner.LogicalToDeviceUnits (logical.Height));

        // The pixels of `area`, as a string that can be compared between two renders.
        //
        // Deliberately NOT "is there ink here": a strip paints its own border and background across
        // the whole control, so any fixed background guess makes the answer depend on which chrome
        // pixel the sample happened to land on -- and that lands differently at every scale. Comparing
        // the SAME region across two renders needs no such guess and asks the question the test
        // actually cares about: did changing this property change what gets drawn here.
        private static string Zone (Control control, Rectangle area)
        {
            using var bitmap = PaintSurface.Render (control);
            var builder = new System.Text.StringBuilder ();

            for (var y = System.Math.Max (0, area.Top); y < area.Bottom && y < bitmap.Height; y++)
                for (var x = System.Math.Max (0, area.Left); x < area.Right && x < bitmap.Width; x++)
                    builder.Append (bitmap.GetPixel (x, y).ToString ()).Append (';');

            return builder.ToString ();
        }

        // ---------------- Spring

        private static StatusStrip Status (out Form form, out ToolStripStatusLabel left, out ToolStripStatusLabel right)
        {
            HeadlessRenderer.Use ();

            var strip = new StatusStrip { Width = 400, Height = 24 };
            left = new ToolStripStatusLabel { Text = "Ready" };
            right = new ToolStripStatusLabel { Text = "OK" };
            strip.Items.Add (left);
            strip.Items.Add (right);

            form = new Form { Width = 500, Height = 200 };
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            return strip;
        }

        [Fact]
        public void A_spring_label_takes_the_width_the_others_leave ()
        {
            using var strip = Status (out var form, out var left, out var right);

            try {
                var before = left.Bounds.Width;

                left.Spring = true;
                PaintSurface.Render (strip).Dispose ();

                Assert.True (left.Bounds.Width > before,
                    $"spring label did not grow: {before} -> {left.Bounds.Width}");
                Assert.True (right.Bounds.Right <= strip.Width);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_spring_label_pushes_its_neighbour_to_the_edge ()
        {
            // What Spring is actually for: the right-hand status labels stay pinned to the right edge
            // as the window resizes. Asserted as a relationship, not a pixel count.
            using var strip = Status (out var form, out var left, out var right);

            try {
                var before = right.Bounds.Right;

                left.Spring = true;
                PaintSurface.Render (strip).Dispose ();

                Assert.True (right.Bounds.Right > before,
                    $"neighbour did not move right: {before} -> {right.Bounds.Right}");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void With_no_spring_label_the_layout_is_unchanged ()
        {
            // GUARD: the whole arithmetic is behind a spring count, so a status strip that never sets
            // it -- which is every one that exists today -- lays out exactly as before.
            using var strip = Status (out var form, out var left, out var right);

            try {
                // Relationships, not absolutes: the strip has a padded client rectangle, so the first
                // item does not start at 0 and asserting that it did was testing the padding.
                Assert.Equal (left.Bounds.Right + 4, right.Bounds.Left);
                Assert.Equal (left.Bounds.Width, right.Bounds.Width);
            } finally {
                form.Close ();
            }
        }

        // ---------------- Alignment

        [Fact]
        public void A_right_aligned_item_is_pinned_to_the_trailing_edge ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 500, Height = 200 };
            var strip = new ToolStrip { Width = 400, Height = 30 };
            var first = new ToolStripButton { Text = "Open" };
            var help = new ToolStripButton { Text = "Help" };
            strip.Items.Add (first);
            strip.Items.Add (help);
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            try {
                var before = help.Bounds.Left;

                help.Alignment = ToolStripItemAlignment.Right;
                strip.PerformLayout ();
                PaintSurface.Render (strip).Dispose ();

                Assert.True (help.Bounds.Left > before, $"not moved right: {before} -> {help.Bounds.Left}");
                Assert.Equal (strip.DeviceToLogicalUnits (strip.ClientRectangle.Right), help.Bounds.Right);

                // The left-aligned item keeps its place.
                Assert.Equal (0, first.Bounds.Left);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Right_aligned_items_keep_their_relative_order ()
        {
            // GUARD: walking the trailing items forwards from the edge instead of backwards reverses
            // them, which looks right with one item and is wrong with two.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 500, Height = 200 };
            var strip = new ToolStrip { Width = 400, Height = 30 };
            var a = new ToolStripButton { Text = "Settings", Alignment = ToolStripItemAlignment.Right };
            var b = new ToolStripButton { Text = "Help", Alignment = ToolStripItemAlignment.Right };
            strip.Items.Add (a);
            strip.Items.Add (b);
            form.Controls.Add (strip);
            form.Show ();
            PaintSurface.Render (strip).Dispose ();

            try {
                Assert.True (a.Bounds.Left < b.Bounds.Left, "declaration order not preserved at the trailing edge");
                Assert.Equal (strip.DeviceToLogicalUnits (strip.ClientRectangle.Right), b.Bounds.Right);
            } finally {
                form.Close ();
            }
        }
    }
}
