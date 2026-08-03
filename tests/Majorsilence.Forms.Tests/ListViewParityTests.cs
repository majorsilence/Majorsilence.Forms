using Xunit;

using Point = System.Drawing.Point;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the ListView parity pass (docs/winforms-gap-plan.md).
    ///
    /// Weighted towards the members that had the wrong shape rather than the ones that were simply
    /// absent: <c>SelectedItems</c> was an <c>IEnumerable</c> so <c>.Count</c> did not compile,
    /// <c>TopItem</c> was a <c>bool</c>, and <c>HitTest</c> returned an <c>int</c>. Those are the
    /// changes a regression would silently undo.
    /// </summary>
    public class ListViewParityTests
    {
        private static ListView WithItems (params string[] texts)
        {
            var listView = new ListView ();

            foreach (var text in texts)
                listView.Items.Add (new ListViewItem (text));

            return listView;
        }

        [Fact]
        public void SelectedItems_is_a_collection_with_a_count ()
        {
            // The line this exists for: `if (listView.SelectedItems.Count > 0)`.
            using var listView = WithItems ("a", "b", "c");
            listView.Items[1].Selected = true;

            Assert.Equal (1, listView.SelectedItems.Count);
            Assert.Same (listView.Items[1], listView.SelectedItems[0]);
            Assert.True (listView.SelectedItems.Contains (listView.Items[1]));
            Assert.False (listView.SelectedItems.Contains (listView.Items[0]));
        }

        [Fact]
        public void The_selection_views_are_live_not_snapshots ()
        {
            using var listView = WithItems ("a", "b");
            var selected = listView.SelectedItems;

            Assert.Equal (0, selected.Count);

            listView.Items[0].Selected = true;

            Assert.Equal (1, selected.Count);      // the same instance sees the change
        }

        [Fact]
        public void Clearing_the_selection_view_deselects_the_items ()
        {
            using var listView = WithItems ("a", "b", "c");
            listView.Items[0].Selected = true;
            listView.Items[2].Selected = true;

            listView.SelectedItems.Clear ();

            Assert.Equal (0, listView.SelectedItems.Count);
            Assert.All (listView.Items, item => Assert.False (item.Selected));
        }

        [Fact]
        public void SelectedIndices_reports_positions_and_can_select_by_index ()
        {
            using var listView = WithItems ("a", "b", "c");

            listView.SelectedIndices.Add (2);

            Assert.Equal ([2], listView.SelectedIndices);
            Assert.True (listView.SelectedIndices.Contains (2));

            listView.SelectedIndices.Remove (2);
            Assert.Equal (0, listView.SelectedIndices.Count);
        }

        [Fact]
        public void CheckedItems_and_CheckedIndices_track_the_check_state ()
        {
            using var listView = WithItems ("a", "b", "c");
            listView.Items[1].Checked = true;

            Assert.Equal (1, listView.CheckedItems.Count);
            Assert.Equal ([1], listView.CheckedIndices);

            listView.CheckedItems.Clear ();

            Assert.Equal (0, listView.CheckedItems.Count);
        }

        [Fact]
        public void TopItem_is_an_item_not_a_bool ()
        {
            using var listView = WithItems ("a", "b");

            Assert.Same (listView.Items[0], listView.TopItem);

            using var empty = new ListView ();
            Assert.Null (empty.TopItem);
        }

        [Fact]
        public void HitTest_reports_what_is_under_the_point ()
        {
            using var listView = WithItems ("a", "b");
            listView.Items[0].SetBounds (0, 0, 100, 20);
            listView.Items[1].SetBounds (0, 20, 100, 20);

            var hit = listView.HitTest (10, 25);

            Assert.Same (listView.Items[1], hit.Item);
            Assert.NotEqual (ListViewHitTestLocations.None, hit.Location);

            var miss = listView.HitTest (new Point (500, 500));

            Assert.Null (miss.Item);
            Assert.Equal (ListViewHitTestLocations.None, miss.Location);
        }

        [Fact]
        public void Clear_removes_items_and_columns ()
        {
            using var listView = WithItems ("a", "b");
            listView.Columns.Add ("Name");

            listView.Clear ();

            Assert.Equal (0, listView.Items.Count);
            Assert.Equal (0, listView.Columns.Count);
        }

        [Fact]
        public void AutoResizeColumn_widens_a_column_to_its_header ()
        {
            using var listView = new ListView ();
            var column = listView.Columns.Add ("A much longer header than the default width");
            column.Width = 5;

            listView.AutoResizeColumn (0, ColumnHeaderAutoResizeStyle.HeaderSize);

            Assert.True (column.Width > 5);
        }

        [Fact]
        public void AutoResizeColumn_leaves_the_width_alone_for_None ()
        {
            using var listView = new ListView ();
            var column = listView.Columns.Add ("Name");
            column.Width = 42;

            listView.AutoResizeColumn (0, ColumnHeaderAutoResizeStyle.None);

            Assert.Equal (42, column.Width);
        }

        [Fact]
        public void AutoResizeColumn_rejects_an_index_that_is_not_a_column ()
        {
            using var listView = new ListView ();

            Assert.Throws<System.ArgumentOutOfRangeException> (
                () => listView.AutoResizeColumn (0, ColumnHeaderAutoResizeStyle.HeaderSize));
        }

        [Fact]
        public void FindNearestItem_picks_the_closest_item_in_the_asked_direction ()
        {
            using var listView = WithItems ("left", "right", "far right");
            listView.Items[0].SetBounds (0, 0, 20, 20);
            listView.Items[1].SetBounds (40, 0, 20, 20);
            listView.Items[2].SetBounds (200, 0, 20, 20);

            Assert.Same (listView.Items[1], listView.FindNearestItem (SearchDirectionHint.Right, 10, 10));
            Assert.Same (listView.Items[0], listView.FindNearestItem (SearchDirectionHint.Left, 50, 10));
            Assert.Null (listView.FindNearestItem (SearchDirectionHint.Left, 0, 10));
        }

        [Fact]
        public void RedrawItems_is_a_method_and_validates_its_range ()
        {
            // It reads like an event; the reference assembly says otherwise.
            using var listView = WithItems ("a", "b", "c");

            listView.RedrawItems (0, 2, invalidateOnly: true);

            Assert.Throws<System.ArgumentOutOfRangeException> (() => listView.RedrawItems (-1, 1, true));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => listView.RedrawItems (2, 1, true));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => listView.RedrawItems (0, 9, true));
        }

        [Fact]
        public void GetSubItemAt_maps_a_point_to_a_column ()
        {
            using var listView = new ListView ();
            listView.Columns.Add ("First", 50);
            listView.Columns.Add ("Second", 50);

            var item = new ListViewItem ("one");
            item.SubItems.Add ("two");
            listView.Items.Add (item);
            item.SetBounds (0, 0, 100, 20);

            // Column i is SubItems[i], including column 0, which is the item's own text.
            Assert.Equal ("one", item.GetSubItemAt (10, 10)?.Text);
            Assert.Equal ("two", item.GetSubItemAt (60, 10)?.Text);
            Assert.Null (item.GetSubItemAt (500, 10));
        }

        [Fact]
        public void GetBounds_splits_the_row_into_icon_and_label ()
        {
            var item = new ListViewItem ("one");
            item.SetBounds (0, 0, 100, 20);

            var icon = item.GetBounds (ItemBoundsPortion.Icon);
            var label = item.GetBounds (ItemBoundsPortion.Label);

            Assert.Equal (20, icon.Width);                  // the leading square
            Assert.Equal (20, label.X);                     // the label starts where the icon ends
            Assert.Equal (80, label.Width);
            Assert.Equal (item.Bounds, item.GetBounds (ItemBoundsPortion.Entire));
        }

        [Fact]
        public void Focused_tracks_the_controls_focused_item ()
        {
            using var listView = WithItems ("a", "b");

            listView.Items[1].Focused = true;

            Assert.Same (listView.Items[1], listView.FocusedItem);
            Assert.True (listView.Items[1].Focused);
            Assert.False (listView.Items[0].Focused);

            listView.Items[1].Focused = false;
            Assert.Null (listView.FocusedItem);
        }

        [Fact]
        public void An_unparented_item_cannot_be_focused ()
        {
            var orphan = new ListViewItem ("a") { Focused = true };

            Assert.False (orphan.Focused);
        }

        [Fact]
        public void ImageList_follows_the_view_mode ()
        {
            using var listView = WithItems ("a");
            listView.LargeImageList = new ImageList ();
            listView.SmallImageList = new ImageList ();

            listView.View = View.LargeIcon;
            Assert.Same (listView.LargeImageList, listView.Items[0].ImageList);

            listView.View = View.Details;
            Assert.Same (listView.SmallImageList, listView.Items[0].ImageList);
        }

        [Fact]
        public void SubItems_zero_is_the_items_own_text ()
        {
            // The WinForms contract, and the reason GetSubItemAt can map column i to SubItems[i].
            // Text used to live in a separate field with SubItems starting at column 1, so migrated
            // code reading item.SubItems[1].Text was reading the third column.
            var item = new ListViewItem (["first", "second"]);

            Assert.Equal (2, item.SubItems.Count);
            Assert.Equal ("first", item.SubItems[0].Text);
            Assert.Equal ("second", item.SubItems[1].Text);

            item.Text = "renamed";
            Assert.Equal ("renamed", item.SubItems[0].Text);

            item.SubItems[0].Text = "again";
            Assert.Equal ("again", item.Text);
        }

        [Fact]
        public void ListViewGroupCollection_AddRange_adds_every_group ()
        {
            using var listView = new ListView ();

            listView.Groups.AddRange (new ListViewGroup ("one"), new ListViewGroup ("two"));

            Assert.Equal (2, listView.Groups.Count);
        }
    }
}
