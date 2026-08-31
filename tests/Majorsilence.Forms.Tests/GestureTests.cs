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
