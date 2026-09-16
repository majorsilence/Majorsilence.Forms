using System;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.3, the coordinate-space audit (RC-8).
    //
    // The rule this file pins: every public hit-test and rectangle-returning member is in LOGICAL
    // units -- the space `Bounds` and `MouseEventArgs` are in -- so that the idiom an application
    // actually writes is right:
    //
    //     private void OnMouseDown (object sender, MouseEventArgs e)
    //         => Hit (list.GetItemRectangle (i).Contains (e.Location));
    //
    // Device pixels belong to painting and to the laid-out item bounds behind those members, and the
    // conversion happens once, at the public boundary. Five members were on the wrong side of it, and
    // every one of them is invisible at scale 1 -- which is why this file forces a scale rather than
    // relying on the MF_HEADLESS_SCALE=2 gate to be the only thing that ever sees them.
    //
    // Application.UiScale multiplies into the window's scale factor (see UiScaleTests), so these run
    // at an effective 2x in every gate configuration.
    [Collection ("Headless")]
    public sealed class CoordinateSpaceTests : IDisposable
    {
        private readonly double original = Application.UiScale;

        public CoordinateSpaceTests () => HeadlessRenderer.Use ();

        public void Dispose () => Application.UiScale = original;

        // A form has to exist and be shown for DeviceDpi to pick the scale up: it reads
        // FindWindow ()?.Scaling, and an unparented control has no window.
        private static Form Scaled (double scale, params Control[] children)
        {
            Application.UiScale = scale;

            var form = new Form { Width = 500, Height = 400 };

            foreach (var child in children)
                form.Controls.Add (child);

            form.Show ();

            return form;
        }

        // ---------------- ListBox

        [Fact]
        public void ListBox_GetItemRectangle_is_in_logical_units ()
        {
            using var list = new ListBox { Width = 200, Height = 200, ItemHeight = 20 };
            list.Items.AddRange (new object[] { "a", "b", "c", "d" });
            using var form = Scaled (2, list);

            try {
                // Logical both sides: ItemHeight is the unscaled property the application set.
                Assert.Equal (list.ItemHeight, list.GetItemRectangle (1).Height);
                Assert.Equal (list.ItemHeight, list.GetItemRectangle (1).Top - list.GetItemRectangle (0).Top);

                // And the device-space accessor behind it really is scaled, or the assertion above
                // would hold for the wrong reason.
                Assert.Equal (list.ScaledItemHeight, list.GetItemRectangleDevice (1).Height);
                Assert.True (list.ScaledItemHeight > list.ItemHeight, "the fixture must actually be scaled");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ListBox_GetItemRectangle_agrees_with_the_hit_test ()
        {
            // The whole point of the rule: the rectangle a caller gets back must contain the point
            // the hit-test resolves to the same item, both being logical.
            using var list = new ListBox { Width = 200, Height = 200, ItemHeight = 20 };
            list.Items.AddRange (new object[] { "a", "b", "c", "d" });
            using var form = Scaled (2, list);

            try {
                var rect = list.GetItemRectangle (2);
                var centre = new Point (rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

                Assert.Equal (2, list.GetIndexAtLocation (centre));
            } finally {
                form.Close ();
            }
        }

        // ---------------- ListView

        [Fact]
        public void ListView_HitTest_takes_a_logical_point ()
        {
            // `listView.HitTest (e.X, e.Y)` is the only thing this method is for, and e is logical
            // while the item bounds are laid out in device pixels. Compared directly it picked the
            // item at index x scale -- a click on the second row reported the fourth.
            using var view = new ListView { Width = 300, Height = 200, View = View.Details };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 200 });
            view.Items.Add (new ListViewItem ("first"));
            view.Items.Add (new ListViewItem ("second"));
            view.Items.Add (new ListViewItem ("third"));
            using var form = Scaled (2, view);

            try {
                // A paint pass, because the item bounds this hit-test walks are laid out then -- they
                // are all zero until something renders.
                PaintSurface.Render (view).Dispose ();

                // The second row's centre, in logical units, taken from that row's own laid-out
                // bounds -- Details view puts a column-header band above the first row, so counting
                // rows down from the top of the control would be measuring the wrong thing.
                //
                // No conversion here any more: ListViewItem.Bounds answers in logical units as of
                // LAY-38, so this reads the same space the hit-test takes. DeviceBounds is the scaled
                // one, and the assertion below pins that the two really do differ -- without it this
                // test would pass on a control that was not scaled at all.
                var bounds = view.Items[1].Bounds;
                var point = new Point (bounds.Left + 5, bounds.Top + bounds.Height / 2);

                Assert.Same (view.Items[1], view.HitTest (point).Item);
                Assert.True (view.Items[1].DeviceBounds.Top > bounds.Top, "the fixture must actually be scaled");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ListViewItem_Bounds_is_in_logical_units ()
        {
            // LAY-38: the item rectangles are laid out against ScaledRowHeight, so they were device
            // pixels on a public member -- `listView.Items[i].Bounds.Contains (e.Location)`, with a
            // logical MouseEventArgs point, was wrong by the display scale.
            using var view = Details ();
            using var form = Scaled (2, view);

            try {
                PaintSurface.Render (view).Dispose ();

                var logical = view.Items[1].Bounds;
                var device = view.Items[1].DeviceBounds;

                Assert.Equal (view.DeviceToLogicalUnits (device), logical);
                Assert.True (device.Top > logical.Top, "the fixture must actually be scaled");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void ListView_GetItemRect_agrees_with_the_hit_test ()
        {
            // The contract in one assertion: the rectangle a caller gets back must contain the point
            // the hit-test resolves to that same item.
            using var view = Details ();
            using var form = Scaled (2, view);

            try {
                PaintSurface.Render (view).Dispose ();

                var rect = view.GetItemRect (1);
                var centre = new Point (rect.Left + 5, rect.Top + rect.Height / 2);

                Assert.Same (view.Items[1], view.HitTest (centre).Item);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void GetSubItemAt_walks_the_columns_in_one_space ()
        {
            // It stepped LOGICAL column widths across a DEVICE rectangle, so the columns it walked were
            // the wrong size relative to the rectangle it started from -- the second column's own
            // rectangle did not resolve to the second column.
            using var view = Details ();
            using var form = Scaled (2, view);

            try {
                PaintSurface.Render (view).Dispose ();

                var second = view.Items[1].SubItems[1].Bounds;
                var inside = new Point (second.Left + second.Width / 2, second.Top + second.Height / 2);

                Assert.Same (view.Items[1].SubItems[1], view.Items[1].GetSubItemAt (inside.X, inside.Y));
            } finally {
                form.Close ();
            }
        }

        private static ListView Details ()
        {
            var view = new ListView { Width = 300, Height = 200, View = View.Details };
            view.Columns.Add (new ColumnHeader { Text = "Name", Width = 100 });
            view.Columns.Add (new ColumnHeader { Text = "Value", Width = 100 });
            view.Items.Add (new ListViewItem (new[] { "first", "1" }));
            view.Items.Add (new ListViewItem (new[] { "second", "2" }));
            view.Items.Add (new ListViewItem (new[] { "third", "3" }));

            return view;
        }

        [Fact]
        public void ListView_click_selects_the_item_under_the_pointer_when_scaled ()
        {
            using var view = Details ();
            using var form = Scaled (2, view);

            try {
                PaintSurface.Render (view).Dispose ();

                var bounds = view.Items[1].Bounds;   // logical, as of LAY-38
                var point = new Point (bounds.Left + 30, bounds.Top + bounds.Height / 2);

                view.DriveClick (point);

                Assert.Same (view.Items[1], view.SelectedItems.Count > 0 ? view.SelectedItems[0] : null);
            } finally {
                form.Close ();
            }
        }

        // ---------------- TreeView

        // There is deliberately no test here for the PlusMinus/Label split. The indent it keys off is
        // `item.Bounds.Left`, and a laid-out node's bounds span the whole row from x ~= 1 whatever its
        // depth -- so the threshold is ~0 and every point in the control classifies as Label already.
        // The device-to-logical conversion on that line is still right (it is the same quantity in the
        // same space as the point it is compared with), it is simply not observable until the indent is
        // real. Recorded as a finding rather than pinned by a test that would pass either way.

        [Fact]
        public void TreeView_HitTest_rejects_a_point_outside_the_control ()
        {
            // The bounds guard tested a logical point against a device-pixel ClientRectangle, so at
            // 2x it accepted points well past the control's right and bottom edges.
            using var tree = new TreeView { Width = 250, Height = 200 };
            tree.Nodes.Add ("root");
            using var form = Scaled (2, tree);

            try {
                PaintSurface.Render (tree).Dispose ();

                // Past the control's logical right edge but still inside the DEVICE rectangle, which
                // at 2x is twice as wide -- the only band where the guard's space matters.
                var outside = new Point (tree.Width + 10, 10);

                Assert.True (outside.X < tree.ClientRectangle.Width, "the point must be inside the device rectangle");
                Assert.Equal (TreeViewHitTestLocations.None, tree.HitTest (outside).Location);
            } finally {
                form.Close ();
            }
        }

        // ---------------- DataGridView

        [Fact]
        public void DataGridView_cutOverflow_actually_cuts ()
        {
            // The rectangle was converted to logical and then clipped against a device-pixel
            // ClientRectangle: at 2x the clip was twice the size of the control, so the flag that
            // exists to keep the rectangle inside the grid did nothing.
            using var grid = new DataGridView { Width = 200, Height = 120 };
            grid.Columns.Add (new DataGridViewTextBoxColumn { Width = 150 });
            grid.Columns.Add (new DataGridViewTextBoxColumn { Width = 150 });
            grid.Rows.Add ();
            using var form = Scaled (2, grid);

            try {
                var clipped = grid.GetCellDisplayRectangle (1, 0, cutOverflow: true);
                var client = grid.DeviceToLogicalUnits (grid.ClientRectangle);

                Assert.True (clipped.Right <= client.Right,
                    $"a cut rectangle must not run past the control ({clipped.Right} vs {client.Right})");
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void DataGridView_column_and_row_rectangles_are_one_space ()
        {
            // Each was built half from logical sizing properties (Columns[i].Width, Rows[i].Height)
            // and half from the device-pixel ClientRectangle -- one rectangle, two spaces, so a column
            // came back the right width and twice the height.
            using var grid = new DataGridView { Width = 200, Height = 120 };
            grid.Columns.Add (new DataGridViewTextBoxColumn { Width = 60 });
            grid.Rows.Add ();
            using var form = Scaled (2, grid);

            try {
                var client = grid.DeviceToLogicalUnits (grid.ClientRectangle);

                Assert.Equal (60, grid.GetColumnDisplayRectangle (0, false).Width);
                Assert.Equal (client.Height, grid.GetColumnDisplayRectangle (0, false).Height);
                Assert.Equal (client.Width, grid.GetRowDisplayRectangle (0, false).Width);
                Assert.Equal (grid.Rows[0].Height, grid.GetRowDisplayRectangle (0, false).Height);
            } finally {
                form.Close ();
            }
        }

        // There is no "nothing changed at scale 1" test here. It was written and then removed: it set
        // Application.UiScale = 1, which is not scale 1 under the MF_HEADLESS_SCALE=2 gate (the two
        // multiply), so it failed in the one configuration it was least about. The claim is covered far
        // better by the rest of the suite -- ~4800 tests written against scale 1, none of which moved.

    }
}
