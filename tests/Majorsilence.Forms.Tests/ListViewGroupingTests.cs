using System.Linq;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // LST-46: ListView.Groups was a collection nothing read. The renderer had no group code and the
    // layout did not group, so an application that built groups got an ungrouped flat list with no
    // error -- every item present, in insertion order, reading as "grouping did nothing" rather than
    // as a failure.
    //
    // This was the largest single cause in the whole stored-only baseline: 14 of ListView's 20 entries
    // (ShowGroups, GroupImageList and all 12 ListViewGroup members) plus ListViewItem.Group were that
    // one fact. Which is the point worth taking from the W6.2 triage -- the baseline's entry count is
    // not a count of defects.
    //
    // A header band is one row tall. That is a real simplification against upstream, whose bands are
    // taller, and it is deliberate: it keeps top_index, ScaledLineHeight, VisibleLineCount and the
    // scrollbar working unchanged, because every line is still one row and there are simply more of
    // them. Variable line heights are a separate job.
    [Collection ("Headless")]
    public class ListViewGroupingTests
    {
        private static ListView Built (out Form form)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { Width = 300, Height = 260, View = View.Details };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });

            foreach (var text in new[] { "alpha", "bravo", "charlie", "delta" })
                view.Items.Add (new ListViewItem (text));

            form = new Form { Width = 400, Height = 360 };
            form.Controls.Add (view);
            form.Show ();

            return view;
        }

        private static ListView Grouped (out Form form, out ListViewGroup first, out ListViewGroup second)
        {
            var view = Built (out form);

            first = new ListViewGroup ("g1", "First");
            second = new ListViewGroup ("g2", "Second");
            view.Groups.Add (first);
            view.Groups.Add (second);

            view.Items[0].Group = first;
            view.Items[1].Group = second;
            view.Items[2].Group = first;

            PaintSurface.Render (view).Dispose ();

            return view;
        }

        // ---------------- layout

        [Fact]
        public void Each_group_gets_a_header_band ()
        {
            using var view = Grouped (out var form, out var first, out var second);

            try {
                Assert.Equal (2, view.GroupBands.Count);
                Assert.Same (first, view.GroupBands[0].Group);
                Assert.Same (second, view.GroupBands[1].Group);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Items_are_laid_out_under_their_own_group ()
        {
            // The whole of the defect: before this the items sat in insertion order with no band above
            // any of them, so "alpha, bravo, charlie" appeared in that order under nothing.
            using var view = Grouped (out var form, out var first, out var second);

            try {
                var band1 = view.GroupBands[0].DeviceBounds;
                var band2 = view.GroupBands[1].DeviceBounds;

                // alpha and charlie are in the first group, bravo in the second.
                Assert.InRange (view.Items[0].DeviceBounds.Top, band1.Bottom, band2.Top);
                Assert.InRange (view.Items[2].DeviceBounds.Top, band1.Bottom, band2.Top);
                Assert.True (view.Items[1].DeviceBounds.Top >= band2.Bottom);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_ungrouped_item_still_appears ()
        {
            // GUARD, and the more dangerous failure of the two: filtering to grouped items would make
            // assigning groups to SOME items silently hide the rest. "delta" has no group.
            using var view = Grouped (out var form, out _, out _);

            try {
                var delta = view.Items[3];

                Assert.Null (delta.Group);
                Assert.NotEqual (Rectangle.Empty, delta.DeviceBounds);
                Assert.True (delta.DeviceBounds.Top > view.GroupBands[1].DeviceBounds.Top);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_collapsed_group_hides_its_items_but_keeps_its_band ()
        {
            using var view = Grouped (out var form, out var first, out _);

            try {
                first.CollapsedState = ListViewGroupCollapsedState.Collapsed;
                PaintSurface.Render (view).Dispose ();

                Assert.Equal (2, view.GroupBands.Count);
                Assert.Equal (Rectangle.Empty, view.Items[0].DeviceBounds);
                Assert.Equal (Rectangle.Empty, view.Items[2].DeviceBounds);

                // The second group's item is unaffected.
                Assert.NotEqual (Rectangle.Empty, view.Items[1].DeviceBounds);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_collapsed_item_cannot_be_hit_tested ()
        {
            // Why the collapsed items are laid out at Rectangle.Empty rather than left where they
            // were: an empty rectangle contains no point, so every hit-test skips them without having
            // to learn about collapse separately.
            using var view = Grouped (out var form, out var first, out _);

            try {
                // The point alpha occupies while the group is open. Aiming anywhere else would pass
                // whether or not collapse was honoured, because nothing is there in either case.
                var alpha = view.Items[0].Bounds;
                var point = new Point (alpha.Left + 4, alpha.Top + alpha.Height / 2);

                Assert.Same (view.Items[0], view.GetItemAt (point.X, point.Y));

                first.CollapsedState = ListViewGroupCollapsedState.Collapsed;
                PaintSurface.Render (view).Dispose ();

                Assert.NotSame (view.Items[0], view.GetItemAt (point.X, point.Y));
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ShowGroups_false_lays_the_list_out_flat ()
        {
            using var view = Grouped (out var form, out _, out _);

            try {
                view.ShowGroups = false;
                PaintSurface.Render (view).Dispose ();

                Assert.Empty (view.GroupBands);
                Assert.Equal (view.Items[0].DeviceBounds.Height, view.Items[1].DeviceBounds.Top - view.Items[0].DeviceBounds.Top);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_list_with_no_groups_is_untouched ()
        {
            // GUARD: every grouping path is behind IsGrouped, so a list that never mentions Groups --
            // which is every list that exists today -- lays out exactly as it did.
            using var view = Built (out var form);

            try {
                PaintSurface.Render (view).Dispose ();

                Assert.Empty (view.GroupBands);

                var step = view.Items[1].DeviceBounds.Top - view.Items[0].DeviceBounds.Top;

                Assert.Equal (step, view.Items[2].DeviceBounds.Top - view.Items[1].DeviceBounds.Top);
                Assert.Equal (step, view.Items[3].DeviceBounds.Top - view.Items[2].DeviceBounds.Top);
            } finally {
                form.Close ();
            }
        }

        // ---------------- the model

        [Fact]
        public void Assigning_a_group_maintains_both_sides ()
        {
            using var view = Built (out var form);

            try {
                var group = new ListViewGroup ("g", "Group");
                view.Groups.Add (group);

                view.Items[0].Group = group;

                Assert.Contains (view.Items[0], group.Items);
                Assert.Same (group, view.Items[0].Group);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Reassigning_a_group_removes_the_item_from_the_old_one ()
        {
            // Without this an item accumulates in every group it was ever put in, and group.Items --
            // which upstream treats as the authoritative membership -- disagrees with item.Group.
            using var view = Built (out var form);

            try {
                var a = new ListViewGroup ("a", "A");
                var b = new ListViewGroup ("b", "B");
                view.Groups.AddRange (a, b);

                view.Items[0].Group = a;
                view.Items[0].Group = b;

                Assert.DoesNotContain (view.Items[0], a.Items);
                Assert.Contains (view.Items[0], b.Items);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_group_added_to_a_control_knows_which_control ()
        {
            // ListViewGroup.ListView had an `internal set` nothing ever called, which is what made the
            // property read as wired while sitting on the stored-only baseline.
            using var view = Built (out var form);

            try {
                var group = new ListViewGroup ("g", "Group");

                Assert.Null (group.ListView);

                view.Groups.Add (group);

                Assert.Same (view, group.ListView);

                view.Groups.Remove (group);

                Assert.Null (group.ListView);
            } finally {
                form.Close ();
            }
        }

        // ---------------- painting

        // Whether any pixel in `area` differs from the control's background.
        //
        // Rendered at the control's OWN scale, not a hardcoded 1: `area` comes from DeviceBounds, which
        // are in device pixels, so a 1x bitmap samples the wrong place the moment the backend reports
        // anything else. Caught by the MF_HEADLESS_SCALE=2 gate, which is the third time that gate has
        // caught exactly this in a pixel test.
        private static bool HasInk (ListView view, Rectangle area)
        {
            using var bitmap = PaintSurface.Render (view);

            var background = bitmap.GetPixel (view.ItemArea.Left + 1, view.ItemArea.Bottom - 2);

            for (var y = area.Top; y < area.Bottom; y++)
                for (var x = area.Left; x < area.Right; x++)
                    if (bitmap.GetPixel (x, y) != background)
                        return true;

            return false;
        }

        [Fact]
        public void The_header_text_is_painted_on_the_side_the_alignment_asks_for ()
        {
            // Not "the band has ink" -- the band always has ink, because it draws its own rule. This
            // compares WHICH SIDE the caption lands on, which only the alignment can move.
            using var view = Grouped (out var form, out var first, out _);

            try {
                var band = view.GroupBands[0].DeviceBounds;
                var left = new Rectangle (band.Left, band.Top, band.Width / 3, band.Height - 2);
                var right = new Rectangle (band.Right - band.Width / 3, band.Top, band.Width / 3, band.Height - 2);

                first.HeaderAlignment = HorizontalAlignment.Left;
                Assert.True (HasInk (view, left));
                Assert.False (HasInk (view, right));

                first.HeaderAlignment = HorizontalAlignment.Right;
                Assert.False (HasInk (view, left));
                Assert.True (HasInk (view, right));
            } finally {
                form.Close ();
            }
        }

        // ---------------- scrolling

        [Fact]
        public void The_scroll_range_counts_the_bands ()
        {
            // A grouped list whose scroll range counted only its items could not reach its last row,
            // by exactly the number of groups.
            using var view = Built (out var form);

            try {
                var ungrouped = view.LineCount;

                var group = new ListViewGroup ("g", "Group");
                view.Groups.Add (group);
                view.Items[0].Group = group;
                PaintSurface.Render (view).Dispose ();

                Assert.Equal (ungrouped + 1, view.LineCount);

                var second = new ListViewGroup ("g2", "Second");
                view.Groups.Add (second);
                view.Items[1].Group = second;
                PaintSurface.Render (view).Dispose ();

                Assert.Equal (ungrouped + 2, view.LineCount);
            } finally {
                form.Close ();
            }
        }
    }
}
