using System.Collections;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W5.6 (findings LST-01 P0, LST-12, LST-17, LST-18, LST-19): the ListView was not a list view.
    // View was stored-only, so every item rendered as a 70px large-icon tile whatever the mode -- and
    // View.Details, the overwhelmingly common shape in a LOB app, showed column 0 only, with no header
    // and every subitem invisible. Selection, the seven documented events, sorting and scrolling were
    // each dead in their own way.
    [Collection ("Headless")]
    public class ListViewDetailsViewTests
    {
        private static ListView DetailsList (int rows = 3, params string[] columns)
        {
            HeadlessRenderer.Use ();

            var view = new ListView { View = View.Details, Width = 400, Height = 200 };

            foreach (var column in columns.Length > 0 ? columns : new[] { "Name", "Size" })
                view.Columns.Add (column);

            for (var i = 0; i < rows; i++) {
                var item = view.Items.Add ($"row{i}");
                item.SubItems.Add ($"sub{i}");
            }

            return view;
        }

        // ── LST-01: Details is a real view ──────────────────────────────────────────────────────

        [Fact]
        public void Details_lays_items_out_as_rows_not_tiles ()
        {
            using var view = DetailsList (rows: 3);
            view.LayoutItems ();

            var first = view.Items[0].Bounds;
            var second = view.Items[1].Bounds;

            // Rows: full width, stacked, one row-height apart. Tiles were 70x70 side by side, so the
            // second item used to sit to the RIGHT of the first.
            Assert.Equal (first.Left, second.Left);
            Assert.True (second.Top > first.Top, $"rows should stack; got {first} then {second}");
            Assert.True (first.Width > view.ScaledTileSize,
                $"a Details row spans the list, not a tile; got width {first.Width}");
            Assert.Equal (view.ScaledRowHeight, first.Height);
        }

        [Fact]
        public void Details_gives_each_subitem_a_cell_in_its_column ()
        {
            using var view = DetailsList (rows: 2);
            view.LayoutItems ();

            var cell = view.Items[0].SubItems[1].Bounds;

            // SubItem.Bounds was documented as "Stub: always Rectangle.Empty".
            Assert.NotEqual (Rectangle.Empty, cell);
            Assert.True (cell.X >= view.ScaledColumnWidth (view.Columns[0]),
                $"the second column starts after the first; got {cell} vs column 0 width {view.ScaledColumnWidth (view.Columns[0])}");
            Assert.Equal (view.Items[0].Bounds.Top, cell.Top);
        }

        [Fact]
        public void Details_draws_the_header_band_and_the_subitem_text_where_only_Details_puts_them ()
        {
            // The one assertion that proves the SUBITEM DATA reaches the screen. Both halves are chosen
            // so that TILE rendering cannot satisfy them, which the first version of this test did not
            // manage: "some ink in the second column's x-range" is true of tiles too, because tiles are
            // laid across the full width. So: only two items (tiles would occupy the left ~150px and
            // nothing beyond), a wide first column (250px), and a header colour check -- tiles draw no
            // header, so that pixel stays the list's own background.
            using var form = new Form { Size = new Size (500, 300) };
            form.UseSystemDecorations = false;
            var view = DetailsList (rows: 2);
            view.Left = 0;
            view.Top = 0;
            view.Columns[0].Width = 250;
            view.Columns[1].Width = 100;
            form.Controls.Add (view);

            form.Show ();
            HeadlessRenderer.CapturePng (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var bitmap = (SkiaSharp.SKBitmap)buffer.Invoke (view, null)!;

            var header_height = view.ScaledHeaderHeight;
            var column0 = view.ScaledColumnWidth (view.Columns[0]);

            Assert.True (header_height > 0, "a Details list with Clickable headers has a header band");

            // The header band is filled with the header colour, which differs from the list background.
            var header_pixel = bitmap.GetPixel (column0 / 2, header_height / 2);
            var body_pixel = bitmap.GetPixel (column0 / 2, header_height + view.ScaledRowHeight * 3);

            Assert.NotEqual (body_pixel, header_pixel);
            Assert.Equal (Theme.ControlMidColor, header_pixel);

            // Subitem text, in the second column, beside the first row -- past where any tile reaches.
            var subitem_ink = 0;

            for (var x = column0 + 2; x < System.Math.Min (bitmap.Width, column0 + view.ScaledColumnWidth (view.Columns[1])); x++)
                for (var y = header_height; y < System.Math.Min (bitmap.Height, header_height + view.ScaledRowHeight * 2); y++) {
                    var pixel = bitmap.GetPixel (x, y);

                    if (pixel.Alpha > 0 && pixel != body_pixel)
                        subitem_ink++;
                }

            Assert.True (subitem_ink > 0,
                $"the second column's subitem text should be drawn past x={column0}; found none");
        }

        [Fact]
        public void A_column_width_of_minus_two_measures_the_header_instead_of_going_negative ()
        {
            using var view = DetailsList (rows: 2, "A much longer column caption", "B");

            // -1 (fit content) and -2 (fit header) are WinForms' autosize sentinels and were stored
            // verbatim, so a designer's `Width = -2` produced a column two pixels wide, negatively.
            view.Columns[0].Width = -2;
            var header_sized = view.ScaledColumnWidth (view.Columns[0]);

            view.Columns[0].Width = -1;
            var content_sized = view.ScaledColumnWidth (view.Columns[0]);

            Assert.True (header_sized > 0, $"a -2 column should measure its header; got {header_sized}");
            Assert.True (content_sized > 0, $"a -1 column should measure its content; got {content_sized}");
        }

        [Fact]
        public void The_List_view_is_rows_and_LargeIcon_is_tiles ()
        {
            using var rows = DetailsList (rows: 4);
            rows.View = View.List;
            rows.LayoutItems ();

            Assert.Equal (rows.Items[0].Bounds.Left, rows.Items[1].Bounds.Left);
            Assert.Equal (rows.ScaledRowHeight, rows.Items[0].Bounds.Height);

            using var tiles = DetailsList (rows: 4);
            tiles.View = View.LargeIcon;
            tiles.LayoutItems ();

            // Tiles run across before wrapping, so the second one is to the right.
            Assert.True (tiles.Items[1].Bounds.Left > tiles.Items[0].Bounds.Left);
            Assert.Equal (tiles.ScaledTileSize, tiles.Items[0].Bounds.Height);
        }

        // ── LST-19: scrolling ───────────────────────────────────────────────────────────────────

        [Fact]
        public void EnsureVisible_scrolls_the_last_item_into_view ()
        {
            using var view = DetailsList (rows: 100);
            view.Height = 100;

            view.EnsureVisible (99);
            view.LayoutItems ();

            Assert.True (view.TopIndex > 0, "the view should have scrolled");
            Assert.True (view.Items[99].Bounds.Bottom <= view.ItemArea.Bottom + view.ScaledRowHeight,
                $"item 99 should be in view; got {view.Items[99].Bounds} in {view.ItemArea}");
            Assert.NotSame (view.Items[0], view.TopItem);
        }

        // A guard rather than a proof: nothing scrolling is also what the unfixed control did, so this
        // cannot fail against it. It is here to catch a future EnsureVisible that scrolls when it should
        // not -- an over-eager fix, which is the failure the other direction.
        [Fact]
        public void A_list_that_fits_does_not_scroll ()
        {
            using var view = DetailsList (rows: 2);
            view.Height = 400;

            view.EnsureVisible (1);

            Assert.Equal (0, view.TopIndex);
            Assert.Same (view.Items[0], view.TopItem);
        }

        [Fact]
        public void CountPerPage_counts_rows_in_the_current_view ()
        {
            using var view = DetailsList (rows: 50);
            view.Height = 200;

            // Anchored to the row height the control actually lays out with, not to a floor: the old
            // formula divided the height by a constant 70px tile whatever the view, and "more than one"
            // is true of that too (200/70 = 2), so a floor proved nothing.
            var rows_that_fit = view.ItemArea.Height / view.ScaledRowHeight;

            Assert.InRange (view.CountPerPage, rows_that_fit - 1, rows_that_fit + 1);
        }

        // ── LST-17: selection ───────────────────────────────────────────────────────────────────

        [Fact]
        public void Setting_Selected_on_an_item_announces_it ()
        {
            using var view = DetailsList (rows: 3);
            var changed = 0;
            var reported = 0;
            view.SelectedIndexChanged += (_, _) => changed++;
            view.ItemSelectionChanged += (_, _) => reported++;

            view.Items[1].Selected = true;

            Assert.Equal (1, changed);
            Assert.Equal (1, reported);
            Assert.Single (view.SelectedItems.Cast<ListViewItem> ());
        }

        [Fact]
        public void Single_select_deselects_the_previous_item ()
        {
            using var view = DetailsList (rows: 3);
            view.MultiSelect = false;

            view.Items[0].Selected = true;
            view.Items[2].Selected = true;

            // With MultiSelect off, several items assigned Selected = true all used to stick.
            Assert.Single (view.SelectedItems.Cast<ListViewItem> ());
            Assert.True (view.Items[2].Selected);
            Assert.False (view.Items[0].Selected);
        }

        [Fact]
        public void MultiSelect_allows_more_than_one_and_announces_each ()
        {
            using var view = DetailsList (rows: 3);
            var changed = 0;
            view.SelectedIndexChanged += (_, _) => changed++;

            view.Items[0].Selected = true;
            view.Items[2].Selected = true;

            // The count alone is true of a silent auto-property too -- both assignments stick either
            // way -- so the announcement is what this asserts.
            Assert.Equal (2, view.SelectedItems.Cast<ListViewItem> ().Count ());
            Assert.Equal (2, changed);
        }

        // ── LST-18: the seven dead events ───────────────────────────────────────────────────────

        [Fact]
        public void ItemCheck_can_veto_a_check ()
        {
            using var view = DetailsList (rows: 2);
            view.CheckBoxes = true;

            view.ItemCheck += (_, e) => e.NewValue = CheckState.Unchecked;

            view.Items[0].Checked = true;

            Assert.False (view.Items[0].Checked);
        }

        [Fact]
        public void ItemChecked_reports_a_check_that_went_through ()
        {
            using var view = DetailsList (rows: 2);
            view.CheckBoxes = true;

            ListViewItem? reported = null;
            view.ItemChecked += (_, e) => reported = e.Item;

            view.Items[1].Checked = true;

            Assert.Same (view.Items[1], reported);
            Assert.True (view.Items[1].Checked);
        }

        [Fact]
        public void The_dropped_events_keep_their_handlers ()
        {
            // Every one of these was `add { } remove { } }`: += compiled and discarded the delegate, so
            // the subscription could not even be observed. Raising them is covered by the tests above
            // and below; this pins that a handler survives being attached at all.
            using var view = DetailsList (rows: 1);

            var events = typeof (ListView).GetEvents ()
                .Where (e => e.Name is "ItemCheck" or "ItemChecked" or "ItemActivate" or "ItemDrag"
                    or "BeforeLabelEdit" or "AfterLabelEdit" or "ColumnClick")
                .ToList ();

            Assert.Equal (7, events.Count);

            foreach (var declared in events) {
                var field = typeof (ListView).GetField (declared.Name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                Assert.NotNull (field);   // a real field-backed event, not a discarding accessor pair
            }
        }

        // ── LST-12: sorting ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ListViewItemSorter_sorts_on_assignment ()
        {
            using var view = DetailsList (rows: 0);
            view.Items.Add ("b");
            view.Items.Add ("a");

            view.ListViewItemSorter = new TextSorter ();

            // The canonical ColumnClick handler: set a comparer, call Sort. Sort was `Invalidate ()`.
            Assert.Equal ("a", view.Items[0].Text);
            Assert.Equal ("b", view.Items[1].Text);
        }

        [Fact]
        public void Sorting_descending_sorts_by_text ()
        {
            using var view = DetailsList (rows: 0);
            view.Items.Add ("a");
            view.Items.Add ("c");
            view.Items.Add ("b");

            view.Sorting = SortOrder.Descending;

            Assert.Equal (new[] { "c", "b", "a" }, view.Items.Cast<ListViewItem> ().Select (i => i.Text));
        }

        private sealed class TextSorter : IComparer
        {
            public int Compare (object? x, object? y)
                => string.Compare ((x as ListViewItem)?.Text, (y as ListViewItem)?.Text,
                    System.StringComparison.Ordinal);
        }
    }
}
