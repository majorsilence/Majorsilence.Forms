using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.2, the stored-only sweep (RC-7) -- the group-header slice (LST-62). LST-46 built the group
    // bands and wired Header, Subtitle and HeaderAlignment; five members of the family were left
    // stored and unread, and all five are things a group header is supposed to show:
    //
    //   TitleImageIndex / TitleImageKey   an icon beside the header, from ListView.GroupImageList
    //   TaskLink                          a link at the right of the header
    //   Footer / FooterAlignment          a line under the group's items
    //
    // All five reached this sweep only because the framework-written marker was fixed -- every one is
    // an initialised auto-property, so the scan called them outbound state and told a sweep to skip
    // them. See StoredOnlyPropertyBaselineTests.
    //
    // TaskLink also raises GroupTaskLinkClick, which was declared and raised from nowhere. A link that
    // draws and does nothing is the worse half of that pair, so the two are wired together and share
    // one rectangle (ListView.GroupTaskLinkBounds) rather than each computing its own.
    [Collection ("Headless")]
    public class ListViewGroupHeaderTests
    {
        private static ListView Grouped (out Form form, out ListViewGroup group)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 320, Height = 240, View = View.Details, ShowGroups = true };

            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });

            group = new ListViewGroup ("g", "Group one");
            view.Groups.Add (group);

            foreach (var text in new[] { "one", "two" }) {
                var item = new ListViewItem (text) { Group = group };

                view.Items.Add (item);
            }

            form = new Form { Width = 420, Height = 340 };
            form.Controls.Add (view);
            form.Show ();
            PaintSurface.Render (view).Dispose ();

            return view;
        }

        // A footer is a second band, so it changes how many LINES the list has -- and the scrollbar
        // reads that count. A footer drawn without being counted would sit under the last row and be
        // unreachable, which is why this asserts the count rather than only the paint.
        [Fact]
        public void A_footer_adds_a_band_and_the_line_count_follows ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                var before = view.LineCount;
                Assert.Single (view.GroupBands);

                group.Footer = "2 items";
                view.RefreshGroups ();
                PaintSurface.Render (view).Dispose ();

                Assert.Equal (before + 1, view.LineCount);
                Assert.Equal (2, view.GroupBands.Count);

                var footer = view.GroupBands.Single (b => b.IsFooter);
                var header = view.GroupBands.Single (b => !b.IsFooter);

                // Under the items, not under the header -- the placement is the whole difference
                // between FooterAlignment and HeaderAlignment meaning separate things.
                Assert.True (footer.DeviceBounds.Top > header.DeviceBounds.Top);

                foreach (var item in view.Items.Cast<ListViewItem> ())
                    Assert.True (footer.DeviceBounds.Top >= item.DeviceBounds.Bottom);
            }
        }

        // A collapsed group has no items on screen, so a footer under them would be a line under
        // nothing -- and, worse, a line the scroll arithmetic counted.
        [Fact]
        public void A_collapsed_group_shows_no_footer ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                group.Footer = "2 items";
                view.RefreshGroups ();
                PaintSurface.Render (view).Dispose ();

                Assert.Equal (2, view.GroupBands.Count);

                group.CollapsedState = ListViewGroupCollapsedState.Collapsed;
                view.RefreshGroups ();
                PaintSurface.Render (view).Dispose ();

                Assert.Single (view.GroupBands);
                Assert.DoesNotContain (view.GroupBands, b => b.IsFooter);

                // The invariant that matters, and the one the band count alone does not test: the
                // scrollbar reads LineCount, the rows come from LayoutRowsGrouped, and the two count
                // bands through different code. A GroupBandCount that still counted the collapsed
                // group's footer would leave the list one line longer than it draws -- a blank line
                // you can scroll to. The layout's own `continue` hides that from the band count, so it
                // has to be asserted here.
                Assert.Equal (view.Items.Count + view.GroupBands.Count, view.LineCount);
            }
        }

        [Fact]
        public void The_footer_text_is_drawn ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                group.Footer = "2 items";
                view.RefreshGroups ();

                var band = BandInk (view, footer: true);

                group.Footer = " ";
                view.RefreshGroups ();

                Assert.True (band > BandInk (view, footer: true), "The footer band drew no text.");
            }
        }

        [Fact]
        public void FooterAlignment_moves_the_text ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                group.Footer = "2 items";
                group.FooterAlignment = HorizontalAlignment.Left;
                view.RefreshGroups ();
                var left = BandCentroid (view, footer: true);

                group.FooterAlignment = HorizontalAlignment.Right;
                view.RefreshGroups ();
                var right = BandCentroid (view, footer: true);

                Assert.True (right > left, $"Right-aligned footer centroid {right} is not right of {left}.");
            }
        }

        [Fact]
        public void TitleImageKey_puts_an_icon_in_the_header ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                var bare = BandInk (view, footer: false);

                view.GroupImageList = Images ("badge");
                group.TitleImageKey = "badge";
                view.RefreshGroups ();

                Assert.True (BandInk (view, footer: false) > bare, "No icon appeared in the header band.");
            }
        }

        // The index is the other half of the same pair and resolves on its own -- a key-only
        // implementation would leave designer code that used the index drawing nothing.
        [Fact]
        public void TitleImageIndex_works_without_a_key ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                var bare = BandInk (view, footer: false);

                view.GroupImageList = Images ("badge");
                group.TitleImageIndex = 0;
                view.RefreshGroups ();

                Assert.True (BandInk (view, footer: false) > bare, "No icon appeared for TitleImageIndex.");
            }
        }

        [Fact]
        public void A_task_link_is_drawn_at_the_right_of_the_header ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                var bare = BandInk (view, footer: false);

                group.TaskLink = "Show all";
                view.RefreshGroups ();

                Assert.True (BandInk (view, footer: false) > bare, "The task link drew nothing.");

                PaintSurface.Render (view).Dispose ();

                var band = view.GroupBands.Single (b => !b.IsFooter);
                var link = view.GroupTaskLinkBounds (group, band.DeviceBounds);

                Assert.True (link.Right <= band.DeviceBounds.Right);
                Assert.True (link.Left > band.DeviceBounds.Left + band.DeviceBounds.Width / 2);
            }
        }

        [Fact]
        public void Clicking_the_task_link_raises_GroupTaskLinkClick ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                group.TaskLink = "Show all";
                view.RefreshGroups ();
                PaintSurface.Render (view).Dispose ();

                var raised = -1;
                view.GroupTaskLinkClick += (_, e) => raised = e.GroupIndex;

                var band = view.GroupBands.Single (b => !b.IsFooter);
                var link = view.GroupTaskLinkBounds (group, band.DeviceBounds);

                // DriveClick takes a LOGICAL point; the band and link rectangles are device.
                view.DriveClick (new Point (
                    view.DeviceToLogicalUnits (link.Left + link.Width / 2),
                    view.DeviceToLogicalUnits (link.Top + link.Height / 2)));

                Assert.Equal (0, raised);
            }
        }

        // The header band is not one big hit target: a click beside the link is still a click on the
        // header, and must not fire the link's event.
        [Fact]
        public void Clicking_elsewhere_in_the_header_does_not_raise_it ()
        {
            var view = Grouped (out var form, out var group);

            using (form) {
                group.TaskLink = "Show all";
                view.RefreshGroups ();
                PaintSurface.Render (view).Dispose ();

                var raised = false;
                view.GroupTaskLinkClick += (_, _) => raised = true;

                var band = view.GroupBands.Single (b => !b.IsFooter);

                view.DriveClick (new Point (
                    view.DeviceToLogicalUnits (band.DeviceBounds.Left + 2),
                    view.DeviceToLogicalUnits (band.DeviceBounds.Top + band.DeviceBounds.Height / 2)));

                Assert.False (raised);
            }
        }

        private static ImageList Images (string key)
        {
            var list = new ImageList { ImageSize = new Size (12, 12) };
            var bitmap = new SkiaSharp.SKBitmap (12, 12);

            using (var canvas = new SkiaSharp.SKCanvas (bitmap))
                canvas.Clear (new SkiaSharp.SKColor (255, 0, 0));

            list.Images.Add (key, bitmap);

            return list;
        }

        // Paint FIRST, then read the bands: group_bands is filled by the layout pass OnPaint runs, so
        // anything that inspects it straight after RefreshGroups is looking at the previous frame.
        private static SkiaSharp.SKBitmap Band (ListView view, bool footer, out Rectangle bounds)
        {
            var bitmap = PaintSurface.Render (view);

            bounds = view.GroupBands.Single (b => b.IsFooter == footer).DeviceBounds;

            return bitmap;
        }

        private static int BandInk (ListView view, bool footer)
        {
            using var bitmap = Band (view, footer, out var bounds);

            var background = bitmap.GetPixel (bounds.Right - 2, bounds.Bottom - 2);
            var ink = 0;

            for (var y = bounds.Top; y < bounds.Bottom - 1 && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        ink++;

            return ink;
        }

        // The mean x of the band's ink, which moves when the text's alignment does.
        private static double BandCentroid (ListView view, bool footer)
        {
            using var bitmap = Band (view, footer, out var bounds);

            var background = bitmap.GetPixel (bounds.Right - 2, bounds.Bottom - 2);
            double total = 0;
            var count = 0;

            for (var y = bounds.Top; y < bounds.Bottom - 1 && y < bitmap.Height; y++)
                for (var x = bounds.Left; x < bounds.Right && x < bitmap.Width; x++)
                    if (bitmap.GetPixel (x, y) != background) {
                        total += x;
                        count++;
                    }

            Assert.True (count > 0, "The band drew nothing to measure.");

            return total / count;
        }
    }
}
