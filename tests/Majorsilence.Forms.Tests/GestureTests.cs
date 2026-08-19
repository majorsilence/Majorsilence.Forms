using System.Drawing;
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
public class GestureTests
{
    [Fact]
    public void HandleLongPress_OpensContextMenu ()
    {
        var form = new Form ();
        form.Show ();
        var top = form.TitleBar.Height;

        var menu = new ContextMenu ();
        var button = new Button { ContextMenu = menu, Left = 10, Top = top + 10, Width = 100, Height = 30 };
        form.Controls.Add (button);

        form.HandleLongPress (30, top + 20);   // inside the button's bounds

        Assert.True (menu.Visible);

        form.Close ();
    }

    [Fact]
    public void HandleLongPress_DoesNotOpenContextMenu_WhenNoneSet ()
    {
        var form = new Form ();
        form.Show ();
        var top = form.TitleBar.Height;

        var button = new Button { Left = 10, Top = top + 10, Width = 100, Height = 30 };
        form.Controls.Add (button);

        // No ContextMenu set -- must not throw and must not show anything.
        form.HandleLongPress (30, top + 20);

        form.Close ();
    }

    [Fact]
    public void HandlePinch_RaisesOnHitTestedControl_WithScaleAndAngle ()
    {
        var form = new Form ();
        form.Show ();
        var top = form.TitleBar.Height;

        var target = new Panel { Left = 10, Top = top + 10, Width = 100, Height = 100 };
        form.Controls.Add (target);

        PinchGestureEventArgs? received = null;
        target.Pinch += (_, e) => received = e;

        form.HandlePinch (40, top + 40, 1.5, 30, 5);

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
        form.Show ();
        var top = form.TitleBar.Height;

        var target = new Panel { Left = 10, Top = top + 10, Width = 100, Height = 100 };
        form.Controls.Add (target);

        SwipeGestureEventArgs? received = null;
        target.Swipe += (_, e) => received = e;

        form.HandleSwipe (40, top + 40, 0, -500, SwipeDirection.Up);

        Assert.NotNull (received);
        Assert.Equal (SwipeDirection.Up, received!.Direction);
        Assert.Equal (-500, received.VelocityY);

        form.Close ();
    }

    [Fact]
    public void HandleScrollGesture_PansNearestScrollableAncestor ()
    {
        var form = new Form ();
        form.Show ();
        var top = form.TitleBar.Height;

        var panel = new Panel {
            Left = 0, Top = top, Width = 100, Height = 100,
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
        form.HandleScrollGesture (20, top + 20, 0, -40);

        Assert.NotEqual (before, panel.AutoScrollPosition);
        Assert.Equal (40, panel.VerticalScrollProperties.Value);

        form.Close ();
    }

    [Fact]
    public void ExistingMouseClickPipeline_IsUnaffectedByGestureAdditions ()
    {
        var form = new Form ();
        form.Show ();
        var top = form.TitleBar.Height;

        var clicked = false;
        var button = new Button { Left = 10, Top = top + 10, Width = 100, Height = 30 };
        button.Click += (_, _) => clicked = true;
        form.Controls.Add (button);

        form.HandlePointerPressed (MouseButtons.Left, 30, top + 20, Keys.None);
        form.HandlePointerReleased (MouseButtons.Left, 30, top + 20, Keys.None);

        Assert.True (clicked);

        form.Close ();
    }
}
