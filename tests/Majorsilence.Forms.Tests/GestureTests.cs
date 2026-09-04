using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises the backend-agnostic gesture pipeline: WindowBase.HandleLongPress/HandlePinch/
// HandleSwipe/HandleScrollGesture -> Control.RaiseXxx (hit-testing) -> Control.OnXxx (default
// behavior + the public event). None of this needs a real Avalonia gesture recognizer -- the same
// approach HeadlessBackendTests uses for HandlePointerPressed etc. Real end-to-end verification
// (an actual touch gesture driving AvaloniaGestureWiring) can only be done by hand on a device.
//
// Every target here is added directly under the Form, which also hosts an implicit, docked
// TitleBar above it (see MenuClickReproTests) -- an absolutely-positioned sibling (Anchor
// Top/Left, no Dock) is not shifted down to clear it, same as real WinForms dock/absolute
// composition. So each test shows the form first, reads the real TitleBar.Height, and offsets
// both the target's Top and the hit-tested Y by it instead of assuming the client area starts at
// window-relative Y=0.
//
// Each form is closed in a finally block: a failed assert must not leak a shown form into
// Application.OpenForms, where an unrelated later test (e.g. one picking a modal owner) could
// pick it up and fail for a completely different reason -- turning one red test into two.
public class GestureTests
{
    [Fact]
    public void HandleLongPress_OpensContextMenu ()
    {
        // Explicitly sized, like the other gesture tests: a default-size form leaves little client
        // area once the caption has taken its strip, and less still at MF_HEADLESS_SCALE=2.
        var form = new Form { Size = new Size (300, 200) };
        try {
            form.Show ();
            var menu = new ContextMenu ();
            var button = new Button { ContextMenu = menu, Left = 10, Top = 10, Width = 100, Height = 30 };
            form.Controls.Add (button);

            // WindowBase.HandleLongPress (like the other Handle* gesture entry points) takes device
            // pixels and converts them to logical at the boundary, exactly as HandlePointerPressed
            // does -- so routing against logical Bounds holds at MF_HEADLESS_SCALE=2 too.
            var pressAt = WindowPoint.DeviceIn (button, 20, 15);
            form.HandleLongPress (pressAt.X, pressAt.Y);

            Assert.True (menu.Visible);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void HandleLongPress_DoesNotOpenContextMenu_WhenNoneSet ()
    {
        var form = new Form ();
        try {
            form.Show ();
            var button = new Button { Left = 10, Top = 10, Width = 100, Height = 30 };
            form.Controls.Add (button);

            // No ContextMenu set -- must not throw and must not show anything.
            var pressAt = WindowPoint.DeviceIn (button, 20, 15);
            form.HandleLongPress (pressAt.X, pressAt.Y);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void HandlePinch_RaisesOnHitTestedControl_WithScaleAndAngle ()
    {
        var form = new Form ();
        try {
            form.Show ();
            var target = new Panel { Left = 10, Top = 10, Width = 100, Height = 100 };
            form.Controls.Add (target);

            PinchGestureEventArgs? received = null;
            target.Pinch += (_, e) => received = e;

            var pinchAt = WindowPoint.DeviceIn (target, 30, 30);
            form.HandlePinch (pinchAt.X, pinchAt.Y, 1.5, 30, 5);

            Assert.NotNull (received);
            Assert.Equal (1.5, received!.Scale);
            Assert.Equal (30, received.Angle);
            Assert.Equal (5, received.AngleDelta);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void HandleSwipe_RaisesOnHitTestedControl_WithDirection ()
    {
        var form = new Form ();
        try {
            form.Show ();
            var target = new Panel { Left = 10, Top = 10, Width = 100, Height = 100 };
            form.Controls.Add (target);

            SwipeGestureEventArgs? received = null;
            target.Swipe += (_, e) => received = e;

            var swipeAt = WindowPoint.DeviceIn (target, 30, 30);
            form.HandleSwipe (swipeAt.X, swipeAt.Y, 0, -500, SwipeDirection.Up);

            Assert.NotNull (received);
            Assert.Equal (SwipeDirection.Up, received!.Direction);
            // Velocity, like the point, is device pixels/sec converted to logical at the boundary.
            Assert.Equal (-500 / target.Scaling, received.VelocityY);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void HandleScrollGesture_PansNearestScrollableAncestor ()
    {
        var form = new Form ();
        try {
            form.Show ();
            var panel = new Panel {
                Left = 0, Top = 0, Width = 100, Height = 100,
                AutoScroll = true, AutoScrollMinSize = new Size (400, 1000)
            };
            var label = new Label { Left = 0, Top = 0, Width = 400, Height = 1000 };
            panel.Controls.Add (label);
            form.Controls.Add (panel);
            panel.PerformLayout ();

            var before = panel.AutoScrollPosition;

            // A drag starting over the label (a plain Control, not itself scrollable) must still pan
            // its nearest ScrollableControl ancestor (the Panel) -- this is the whole point of
            // RaiseScrollGesture's ancestor walk, not just the exact hit-tested leaf. Content follows
            // the finger, so an upward drag (negative deltaY) reveals content further down (increases
            // scroll magnitude); starting from the top, a downward drag would just clamp back to 0.
            // The delta, like the point, is device pixels converted to logical at the boundary, so the
            // panned distance is scale-relative: a 40px-device drag is DeviceToLogicalUnits(40) logical.
            var scrollAt = WindowPoint.DeviceIn (panel, 20, 20);
            form.HandleScrollGesture (scrollAt.X, scrollAt.Y, 0, -40);

            Assert.NotEqual (before, panel.AutoScrollPosition);
            Assert.Equal (panel.DeviceToLogicalUnits (40), panel.VerticalScrollProperties.Value);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void HandleScrollGesture_ScrollsTreeView_ThatOwnsItsOwnScrollbar ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var tree = new TreeView { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                tree.Nodes.Add ($"Node {i}");
            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);   // force a layout pass so the scrollbar shows

            ScrollGestureEventArgs? seen = null;
            tree.ScrollGesture += (_, e) => seen = e;

            Assert.Equal ("Node 0", tree.LayoutedItems[0].Text);

            // An upward drag (content follows the finger) reveals nodes further down. Delta is device
            // pixels; make it several rows' worth so the sub-row remainder is not what is under test.
            var scrollAt = WindowPoint.DeviceIn (tree, 20, 20);
            form.HandleScrollGesture (scrollAt.X, scrollAt.Y, 0, tree.LogicalToDeviceUnits (-400));

            Assert.NotNull (seen);
            HeadlessRenderer.CapturePng (form, 300, 200);   // repaint so LayoutedItems reflects the new scroll offset
            Assert.NotEqual ("Node 0", tree.LayoutedItems[0].Text);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void HandleScrollGesture_ScrollsListBox_ThatOwnsItsOwnScrollbar ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var list = new ListBox { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                list.Items.Add ($"Item {i}");
            form.Controls.Add (list);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);

            Assert.Equal (0, list.FirstVisibleIndex);

            var scrollAt = WindowPoint.DeviceIn (list, 20, 20);
            form.HandleScrollGesture (scrollAt.X, scrollAt.Y, 0, list.LogicalToDeviceUnits (-400));

            Assert.True (list.FirstVisibleIndex > 0);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_TreeView_TracksTheDragSubRow_ThenRollsOverAWholeRow ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var tree = new TreeView { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                tree.Nodes.Add ($"Node {i}");
            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);

            var rowH = tree.ScaledItemHeight;
            var quarterRow = -rowH / 4;
            var at = WindowPoint.DeviceIn (tree, 20, 20);

            // A drag of a quarter of a row: too small to advance top_index, but the rendered stack
            // must still shift up by that many pixels -- item[0] is Node 0 with a negative Bounds.Y.
            form.HandleScrollGesture (at.X, at.Y, 0, quarterRow);
            HeadlessRenderer.CapturePng (form, 300, 200);
            Assert.Equal ("Node 0", tree.LayoutedItems[0].Text);
            Assert.True (tree.LayoutedItems[0].Bounds.Y < 0, "sub-row drag should push item[0] above the client top");

            // Four more quarter-row drags -> 1.25 rows travelled: the top is now anchored to Node 1,
            // proving the sub-row remainder rolls over rather than being dropped between events.
            for (var i = 0; i < 4; i++)
                form.HandleScrollGesture (at.X, at.Y, 0, quarterRow);
            HeadlessRenderer.CapturePng (form, 300, 200);
            Assert.Equal ("Node 1", tree.LayoutedItems[0].Text);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_ListBox_TracksTheDragSubRow_AndAThumbScrollSnapsFlush ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var list = new ListBox { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                list.Items.Add ($"Item {i}");
            form.Controls.Add (list);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);

            var rowH = list.ScaledItemHeight;
            var at = WindowPoint.DeviceIn (list, 20, 20);

            // Sub-row drag: FirstVisibleIndex unchanged, but item 0's rectangle is lifted above the top.
            form.HandleScrollGesture (at.X, at.Y, 0, -rowH / 4);
            Assert.Equal (0, list.FirstVisibleIndex);
            Assert.True (list.GetItemRectangle (0).Y < list.ClientRectangle.Top,
                "sub-row drag should lift item 0 above the client top");

            // A whole-row programmatic scroll clears the sub-row remainder -- the anchored row sits flush.
            list.FirstVisibleIndex = 4;
            Assert.Equal (list.ClientRectangle.Top, list.GetItemRectangle (4).Y);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_ListBox_FastPathMatchesAFullRepaint_ForASubRowShift ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var list = new ListBox { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                list.Items.Add ($"Item {i}");
            form.Controls.Add (list);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);   // populates the first real back buffer

            var rowH = list.ScaledItemHeight;
            var at = WindowPoint.DeviceIn (list, 20, 20);

            // A few small drags that never cross a row boundary: ScrollByDevicePixels.TryFastScrollBlit
            // repaints these by shifting the existing back buffer and patching only the exposed strip,
            // instead of a full re-render (text shaping included) -- it must still leave ListBox looking
            // exactly like a normal full render would have.
            for (var i = 0; i < 3; i++)
                form.HandleScrollGesture (at.X, at.Y, 0, -rowH / 5);
            var fastPathPng = HeadlessRenderer.CapturePng (form, 300, 200);

            // Force a full re-render at that identical scroll position (Invalidate () does not touch
            // top_index/_scrollOffsetPx) and compare pixel-for-pixel against the fast path's output.
            list.Invalidate ();
            var fullRenderPng = HeadlessRenderer.CapturePng (form, 300, 200);

            if (!fastPathPng.AsSpan ().SequenceEqual (fullRenderPng)) {
                using var a = SkiaSharp.SKBitmap.Decode (fastPathPng);
                using var b = SkiaSharp.SKBitmap.Decode (fullRenderPng);
                var count = 0;
                var maxDelta = 0;
                var worst = "";
                for (var y = 0; y < a.Height; y++)
                for (var x = 0; x < a.Width; x++) {
                    var pa = a.GetPixel (x, y);
                    var pb = b.GetPixel (x, y);
                    if (pa == pb)
                        continue;
                    count++;
                    var delta = Math.Max (Math.Max (Math.Abs (pa.Red - pb.Red), Math.Abs (pa.Green - pb.Green)),
                        Math.Max (Math.Abs (pa.Blue - pb.Blue), Math.Abs (pa.Alpha - pb.Alpha)));
                    if (delta > maxDelta) { maxDelta = delta; worst = $"({x},{y}): fast={pa} full={pb}"; }
                }
                throw new Xunit.Sdk.XunitException ($"{count} differing pixels, max per-channel delta={maxDelta}, worst {worst}");
            }

            Assert.Equal (fullRenderPng, fastPathPng);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_ListBox_CrossingARow_StillFallsBackToAFullRepaint ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var list = new ListBox { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                list.Items.Add ($"Item {i}");
            form.Controls.Add (list);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);

            var rowH = list.ScaledItemHeight;
            var at = WindowPoint.DeviceIn (list, 20, 20);

            // A drag past a whole row moves the scrollbar thumb, which the fast path deliberately does
            // not try to keep in sync -- ScrollByDevicePixels must fall back to Invalidate () here, not
            // leave a stale (shifted-but-uncommitted) frame with a thumb that disagrees with top_index.
            form.HandleScrollGesture (at.X, at.Y, 0, -(rowH + rowH / 2));

            Assert.True (list.FirstVisibleIndex > 0);
            Assert.True (list.NeedsPaint, "crossing a row must still request a normal full repaint");
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_TreeView_FastPathMatchesAFullRepaint_ForASubRowShift ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var tree = new TreeView { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                tree.Nodes.Add ($"Node {i}");
            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);   // populates the first real back buffer

            var rowH = tree.ScaledItemHeight;
            var at = WindowPoint.DeviceIn (tree, 20, 20);

            // A few small drags that never cross a row boundary: ScrollByDevicePixels.TryFastScrollBlit
            // repaints these by shifting the existing back buffer and patching only the exposed strip,
            // instead of a full LayoutItems() + re-render -- it must still leave the TreeView looking
            // exactly like a normal full render would have.
            for (var i = 0; i < 3; i++)
                form.HandleScrollGesture (at.X, at.Y, 0, -rowH / 5);
            var fastPathPng = HeadlessRenderer.CapturePng (form, 300, 200);

            // Force a full re-render at that identical scroll position (Invalidate () does not touch
            // top_index/_scrollOffsetPx) and compare pixel-for-pixel against the fast path's output.
            tree.Invalidate ();
            var fullRenderPng = HeadlessRenderer.CapturePng (form, 300, 200);

            if (!fastPathPng.AsSpan ().SequenceEqual (fullRenderPng)) {
                using var a = SkiaSharp.SKBitmap.Decode (fastPathPng);
                using var b = SkiaSharp.SKBitmap.Decode (fullRenderPng);
                var count = 0;
                var maxDelta = 0;
                var worst = "";
                for (var y = 0; y < a.Height; y++)
                for (var x = 0; x < a.Width; x++) {
                    var pa = a.GetPixel (x, y);
                    var pb = b.GetPixel (x, y);
                    if (pa == pb)
                        continue;
                    count++;
                    var delta = Math.Max (Math.Max (Math.Abs (pa.Red - pb.Red), Math.Abs (pa.Green - pb.Green)),
                        Math.Max (Math.Abs (pa.Blue - pb.Blue), Math.Abs (pa.Alpha - pb.Alpha)));
                    if (delta > maxDelta) { maxDelta = delta; worst = $"({x},{y}): fast={pa} full={pb}"; }
                }
                throw new Xunit.Sdk.XunitException ($"{count} differing pixels, max per-channel delta={maxDelta}, worst {worst}");
            }

            Assert.Equal (fullRenderPng, fastPathPng);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_TreeView_CrossingARow_StillFallsBackToAFullRepaint ()
    {
        var form = new Form { Size = new Size (300, 200) };
        try {
            var tree = new TreeView { Left = 0, Top = 0, Width = 160, Height = 120 };
            for (var i = 0; i < 60; i++)
                tree.Nodes.Add ($"Node {i}");
            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 200);

            var rowH = tree.ScaledItemHeight;
            var at = WindowPoint.DeviceIn (tree, 20, 20);

            // A drag past a whole row moves the scrollbar thumb, which the fast path deliberately does
            // not try to keep in sync -- ScrollByDevicePixels must fall back to Invalidate () here, not
            // leave a stale (shifted-but-uncommitted) frame with a thumb that disagrees with top_index.
            form.HandleScrollGesture (at.X, at.Y, 0, -(rowH + rowH / 2));

            Assert.True (tree.NeedsPaint, "crossing a row must still request a normal full repaint");
            HeadlessRenderer.CapturePng (form, 300, 200);   // repaint so LayoutedItems reflects the new scroll offset
            Assert.NotEqual ("Node 0", tree.LayoutedItems[0].Text);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_TreeView_ManySmallDrags_KeepScrollbarInLockstepWithTopIndex ()
    {
        // Shaped like the ControlGallery nav TreeView: many more flat top-level nodes than fit on
        // screen at once (real gallery has ~40 samples in a nav panel that only shows ~10 rows) --
        // this is the ratio that actually exercises a multi-row scroll range, unlike a panel tall
        // enough to show nearly everything at once.
        var form = new Form { Size = new Size (300, 240) };
        try {
            var tree = new TreeView { Left = 0, Top = 0, Width = 160, Height = 240 };
            for (var i = 0; i < 40; i++)
                tree.Nodes.Add ($"Node {i}");
            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 240);   // force a layout pass so the scrollbar shows

            var vsbField = tree.GetType ().GetField ("vscrollbar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var vsb = vsbField.GetValue (tree)!;
            var valueProp = vsb.GetType ().GetProperty ("Value")!;
            var topIndexField = tree.GetType ().GetField ("top_index", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var at = WindowPoint.DeviceIn (tree, 20, 20);
            // Many small drags with no intervening repaint, like the events a real touch fling
            // delivers between frames -- top_index and the scrollbar's Value must never disagree,
            // even mid-fling, or the thumb visibly lags/freezes relative to content that has moved on.
            for (var i = 0; i < 40; i++) {
                form.HandleScrollGesture (at.X, at.Y, 0, -30);
                Assert.Equal (topIndexField.GetValue (tree), valueProp.GetValue (vsb));
            }

            Assert.True ((int) topIndexField.GetValue (tree)! > 0);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_TreeView_ScrollingToTheEnd_ParksTheThumbAtTheEndOfTheTrack ()
    {
        // Regression test for a real device report ("it scrolls but the scroll bar doesn't move"):
        // with a viewport tall enough to show several rows at once (LargeChange > 1), setting
        // vscrollbar.Maximum to the last valid top_index directly -- instead of the conventional
        // itemCount - 1 -- left ScrollBar.EffectiveMaximum (what actually positions the thumb) far
        // short of the real end, so the thumb reached the end of the track, and froze there, long
        // before top_index/Value did. This must not regress: EffectiveMaximum has to equal the last
        // top_index a scroll can reach, and Value must actually get there.
        var form = new Form { Size = new Size (300, 240) };
        try {
            var tree = new TreeView { Left = 0, Top = 0, Width = 160, Height = 240 };
            for (var i = 0; i < 40; i++)
                tree.Nodes.Add ($"Node {i}");
            form.Controls.Add (tree);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 240);

            var vsbField = tree.GetType ().GetField ("vscrollbar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var vsb = (VerticalScrollBar) vsbField.GetValue (tree)!;

            // A viewport this shaped genuinely has LargeChange > 1 -- if it didn't, the old bug
            // (Maximum used where EffectiveMaximum was needed) would happen to be invisible here too.
            Assert.True (vsb.LargeChange > 1);
            Assert.True (vsb.EffectiveMaximum < vsb.Maximum, "test setup should exercise LargeChange > 1");

            var at = WindowPoint.DeviceIn (tree, 20, 20);
            for (var i = 0; i < 60; i++)
                form.HandleScrollGesture (at.X, at.Y, 0, -30);   // drag far past the end of the content

            Assert.Equal (vsb.EffectiveMaximum, vsb.Value);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ScrollGesture_ListBox_ScrollingToTheEnd_ParksTheThumbAtTheEndOfTheTrack ()
    {
        var form = new Form { Size = new Size (300, 240) };
        try {
            var list = new ListBox { Left = 0, Top = 0, Width = 160, Height = 240 };
            for (var i = 0; i < 40; i++)
                list.Items.Add ($"Item {i}");
            form.Controls.Add (list);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 300, 240);

            var vsbField = list.GetType ().GetField ("vscrollbar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var vsb = (VerticalScrollBar) vsbField.GetValue (list)!;

            Assert.True (vsb.LargeChange > 1);
            Assert.True (vsb.EffectiveMaximum < vsb.Maximum, "test setup should exercise LargeChange > 1");

            var at = WindowPoint.DeviceIn (list, 20, 20);
            for (var i = 0; i < 60; i++)
                form.HandleScrollGesture (at.X, at.Y, 0, -30);

            Assert.Equal (vsb.EffectiveMaximum, vsb.Value);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void ExistingMouseClickPipeline_IsUnaffectedByGestureAdditions ()
    {
        var form = new Form ();
        try {
            form.Show ();
            var clicked = false;
            var button = new Button { Left = 10, Top = 10, Width = 100, Height = 30 };
            button.Click += (_, _) => clicked = true;
            form.Controls.Add (button);

            // Unlike the Handle* gesture entry points above, HandlePointerPressed/Released take DEVICE
            // pixels (a real backend multiplies by its render scaling before calling them) -- see
            // HeadlessRenderer.ToDevice and MenuClickReproTests. HeadlessRenderer.MouseDown/MouseUp
            // convert from the logical coordinates used everywhere else in this file, so this still
            // lands on the button under a HiDPI-simulated run (MF_HEADLESS_SCALE=2) instead of at half
            // the intended position.
            var pressAt = WindowPoint.In (button, 20, 15);
            HeadlessInput.MouseDown (form, pressAt);
            HeadlessInput.MouseUp (form, pressAt);

            Assert.True (clicked);
        } finally {
            form.Close ();
        }
    }
}
