using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises the backend-agnostic gesture pipeline: WindowBase.HandleLongPress/HandlePinch/
// HandleSwipe/HandleScrollGesture -> Control.RaiseXxx (hit-testing) -> Control.OnXxx (default
// behavior + the public event). None of this needs a real Avalonia gesture recognizer -- the same
// approach HeadlessBackendTests uses for HandlePointerPressed etc. Real end-to-end verification
// (an actual touch gesture driving AvaloniaGestureWiring) can only be done by hand on a device.
public class GestureTests
{
    [Fact]
    public void HandleLongPress_OpensContextMenu ()
    {
        var form = new Form ();
        var menu = new ContextMenu ();
        var button = new Button { ContextMenu = menu, Left = 10, Top = 10, Width = 100, Height = 30 };
        form.Controls.Add (button);
        form.Show ();

        form.HandleLongPress (30, 20);   // inside the button's bounds

        Assert.True (menu.Visible);

        form.Close ();
    }

    [Fact]
    public void HandleLongPress_DoesNotOpenContextMenu_WhenNoneSet ()
    {
        var form = new Form ();
        var button = new Button { Left = 10, Top = 10, Width = 100, Height = 30 };
        form.Controls.Add (button);
        form.Show ();

        // No ContextMenu set -- must not throw and must not show anything.
        form.HandleLongPress (30, 20);

        form.Close ();
    }

    [Fact]
    public void HandlePinch_RaisesOnHitTestedControl_WithScaleAndAngle ()
    {
        var form = new Form ();
        var target = new Panel { Left = 10, Top = 10, Width = 100, Height = 100 };
        form.Controls.Add (target);
        form.Show ();

        PinchGestureEventArgs? received = null;
        target.Pinch += (_, e) => received = e;

        form.HandlePinch (40, 40, 1.5, 30, 5);

        Assert.NotNull (received);
        Assert.Equal (1.5, received!.Scale);
        Assert.Equal (30, received.Angle);
        Assert.Equal (5, received.AngleDelta);

        form.Close ();
    }

    [Fact]
    public void HandleSwipe_RaisesOnHitTestedControl_WithDirection ()
    {
        var form = new Form ();
        var target = new Panel { Left = 10, Top = 10, Width = 100, Height = 100 };
        form.Controls.Add (target);
        form.Show ();

        SwipeGestureEventArgs? received = null;
        target.Swipe += (_, e) => received = e;

        form.HandleSwipe (40, 40, 0, -500, SwipeDirection.Up);

        Assert.NotNull (received);
        Assert.Equal (SwipeDirection.Up, received!.Direction);
        Assert.Equal (-500, received.VelocityY);

        form.Close ();
    }

    [Fact]
    public void HandleScrollGesture_PansNearestScrollableAncestor ()
    {
        var form = new Form ();
        var panel = new Panel {
            Left = 0, Top = 0, Width = 100, Height = 100,
            AutoScroll = true, AutoScrollMinSize = new Size (400, 1000)
        };
        var label = new Label { Left = 0, Top = 0, Width = 400, Height = 1000 };
        panel.Controls.Add (label);
        form.Controls.Add (panel);
        form.Show ();
        panel.PerformLayout ();

        var before = panel.AutoScrollPosition;

        // A drag starting over the label (a plain Control, not itself scrollable) must still pan
        // its nearest ScrollableControl ancestor (the Panel) -- this is the whole point of
        // RaiseScrollGesture's ancestor walk, not just the exact hit-tested leaf. Content follows
        // the finger, so an upward drag (negative deltaY) reveals content further down (increases
        // scroll magnitude); starting from the top, a downward drag would just clamp back to 0.
        form.HandleScrollGesture (20, 20, 0, -40);

        Assert.NotEqual (before, panel.AutoScrollPosition);
        Assert.Equal (40, panel.VerticalScrollProperties.Value);

        form.Close ();
    }

    [Fact]
    public void ExistingMouseClickPipeline_IsUnaffectedByGestureAdditions ()
    {
        var form = new Form ();
        var clicked = false;
        var button = new Button { Left = 10, Top = 10, Width = 100, Height = 30 };
        button.Click += (_, _) => clicked = true;
        form.Controls.Add (button);
        form.Show ();

        form.HandlePointerPressed (MouseButtons.Left, 30, 20, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, 30, 20, Keys.None);

        Assert.True (clicked);

        form.Close ();
    }
}
