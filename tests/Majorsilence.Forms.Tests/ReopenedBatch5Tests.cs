using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the reopened entries -- three unrelated properties that happen to be cheap, each a single
    // read in a place that already draws the thing:
    //
    //   ListView.LabelWrap   LST-64  a tile caption always wrapped
    //   Label.FlatStyle      SMP-09  a flat label drew a standard border
    //
    // ShowImageMargin was wired here too and then REVERTED: see TSM-45. It works at scaling 1 and
    // cannot be demonstrated at scaling 2, because the drop-down's text indent is a device-converted
    // constant added to a logical edge (TSM-41) -- at scale 2 the gutter becomes 56 logical units and
    // pushes the caption off the menu. Fixing that is a units decision for the whole strip-renderer
    // family, not something to slip into a wiring batch.
    //
    // None of them changes a default: each property's default value produces exactly the drawing that
    // was there before, and only the non-default value was unreachable.
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
