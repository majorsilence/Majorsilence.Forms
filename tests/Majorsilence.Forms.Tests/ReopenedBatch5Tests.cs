using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the reopened entries -- three unrelated properties that happen to be cheap, each a single
    // read in a place that already draws the thing:
    //
    //   ListView.LabelWrap                  LST-64  a tile caption always wrapped
    //   Label.FlatStyle                     SMP-09  a flat label drew a standard border
    //   ContextMenuStrip.ShowImageMargin    TSM-45  the icon gutter was always reserved
    //   ToolStripDropDownMenu.ShowImageMargin       (the same state, declared twice)
    //
    // ShowImageMargin was wired here, reverted, and wired again. The revert is on the record in
    // TSM-45: it works at scaling 1 and could not be demonstrated at scaling 2, because the caption's
    // indent is device while the item box it was measured against was logical. Fixing TSM-41 -- the
    // renderers now paint at `MenuItem.DeviceBounds` -- is what made it expressible.
    [Collection ("Headless")]
    public class ReopenedBatch5Tests
    {
        // ---------------- ListView.LabelWrap (LST-64)

        // A caption long enough to need two lines at tile width, so capping it at one is visible.
        private static ListView Tiles (out Form form, out ListViewItem item)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 300, Height = 200, View = View.LargeIcon };

            item = new ListViewItem ("a rather long caption that has to wrap");
            view.Items.Add (item);

            form = new Form { Width = 400, Height = 300 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        [Fact]
        public void LabelWrap_off_caps_a_tile_caption_at_one_line ()
        {
            var view = Tiles (out var form, out var item);

            using (form) {
                Assert.True (view.LabelWrap);
                var wrapped = ItemInk (view, item);

                view.LabelWrap = false;

                Assert.True (wrapped > ItemInk (view, item),
                    "Turning LabelWrap off drew as much text as wrapping did.");
            }
        }

        // ---------------- ShowImageMargin (TSM-45)

        [Fact]
        public void A_context_menu_without_an_image_margin_starts_its_text_further_left ()
        {
            var menu = new ContextMenuStrip ();
            menu.Items.Add (new ToolStripMenuItem ("Paste"));

            Assert.True (menu.ShowImageMargin);
            var indented = TextStart (menu);

            menu.ShowImageMargin = false;

            Assert.True (TextStart (menu) < indented,
                "Collapsing the image margin did not move the caption left.");
        }

        // The same state is declared on two types; a renderer reading only one would leave the other
        // inert, which is the shape this sweep keeps finding.
        [Fact]
        public void A_drop_down_menu_honours_its_own_ShowImageMargin ()
        {
            var menu = new ToolStripDropDownMenu ();
            menu.Items.Add (new ToolStripMenuItem ("Paste"));

            var indented = TextStart (menu);

            menu.ShowImageMargin = false;

            Assert.True (TextStart (menu) < indented);
        }

        // The x of the first ink on the caption's row.
        private static int TextStart (MenuDropDown menu)
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 400, Height = 300 };
            var host = new Panel { Left = 10, Top = 10, Width = 100, Height = 40 };
            form.Controls.Add (host);
            form.Show ();

            menu.Show (host, new Point (10, 10));

            try {
                using var bitmap = PaintSurface.Render (menu);
                var item = menu.Items[0].DeviceBounds;

                // Only the item's own row band, and only past the popup's frame: the drop-down draws a
                // border down its left edge, so a whole-bitmap scan finds that every time and the
                // answer never moves. The gutter is empty here -- no item has an image -- so the first
                // ink inside the band is the caption. DeviceBounds, because the bitmap is device
                // (TSM-41).
                // Sampled from INSIDE the item, just under its top-left corner and above the
                // vertically-centred caption -- not from the bitmap's corner, which is outside the
                // item and a different colour, so every column of the item then reads as ink and the
                // measurement saturates. (Exactly the mistake ToolStripLabelLinkTests made.)
                var background = bitmap.GetPixel (System.Math.Max (item.Left, 0) + 1, System.Math.Max (item.Top, 0) + 1);
                var from = System.Math.Max (item.Left, 0) + menu.LogicalToDeviceUnits (4);
                var top = System.Math.Max (item.Top, 0);
                var bottom = System.Math.Min (item.Bottom, bitmap.Height);

                for (var x = from; x < bitmap.Width; x++)
                    for (var y = top; y < bottom; y++)
                        if (bitmap.GetPixel (x, y) != background)
                            return x;

                return int.MaxValue;
            } finally {
                Application.ClosePopups ();
                form.Close ();
            }
        }

        // ---------------- Label.FlatStyle (SMP-09)

        [Fact]
        public void A_flat_label_draws_a_single_line_border ()
        {
            HeadlessRenderer.Use ();

            var label = new Label { Text = "Total", Width = 120, Height = 30, BorderStyle = BorderStyle.Fixed3D };

            using var form = new Form { Width = 300, Height = 200 };
            form.Controls.Add (label);
            form.Show ();

            Assert.Equal (2, label.Style.Border.GetWidth ());

            label.FlatStyle = FlatStyle.Flat;

            Assert.Equal (1, label.Style.Border.GetWidth ());
        }

        // Popup is flat here: a Label tracks no hover for this purpose, so upstream's "flat until the
        // pointer is over it" has no second state to switch to. Recorded, not approximated.
        [Fact]
        public void Popup_is_flat_on_a_label ()
        {
            HeadlessRenderer.Use ();

            var label = new Label { BorderStyle = BorderStyle.Fixed3D, FlatStyle = FlatStyle.Popup };

            Assert.Equal (1, label.Style.Border.GetWidth ());
        }

        // FlatStyle must not invent a border where BorderStyle asked for none -- the two properties
        // answer different questions and only one of them decides whether there is a border at all.
        [Fact]
        public void FlatStyle_does_not_give_a_borderless_label_a_border ()
        {
            HeadlessRenderer.Use ();

            var label = new Label { Text = "Total" };

            Assert.Equal (BorderStyle.None, label.BorderStyle);

            label.FlatStyle = FlatStyle.Flat;

            Assert.Equal (0, label.Style.Border.GetWidth ());
        }

        // Setting the two in either order has to land in the same place; the first version of this
        // applied the border only from BorderStyle's setter, so a label whose FlatStyle was assigned
        // afterwards kept the standard width.
        [Fact]
        public void The_two_properties_commute ()
        {
            HeadlessRenderer.Use ();

            var border_first = new Label ();
            border_first.BorderStyle = BorderStyle.Fixed3D;
            border_first.FlatStyle = FlatStyle.Flat;

            var flat_first = new Label ();
            flat_first.FlatStyle = FlatStyle.Flat;
            flat_first.BorderStyle = BorderStyle.Fixed3D;

            Assert.Equal (border_first.Style.Border.GetWidth (), flat_first.Style.Border.GetWidth ());
            Assert.Equal (1, flat_first.Style.Border.GetWidth ());
        }

        // ListViewItem.DeviceBounds is DEVICE, matching the bitmap.
        private static int ItemInk (ListView view, ListViewItem item)
        {
            using var bitmap = PaintSurface.Render (view);
            var bounds = item.DeviceBounds;
            var background = bitmap.GetPixel (bounds.Left + 1, bounds.Top + 1);
            var ink = 0;

            for (var y = bounds.Top; y < bounds.Bottom && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }
    }
}
