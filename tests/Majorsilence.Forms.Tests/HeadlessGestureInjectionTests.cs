using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// HeadlessRenderer could inject mouse, wheel, key and text input but no gesture: WindowBase.HandleSwipe,
// HandlePinch, HandleLongPress and HandleScrollGesture are internal, so an application could not test its
// own Swipe/Pinch/LongPress/ScrollGesture handling on the headless backend. Found by Recital's page-turn
// swipe (FRAMEWORK_GAPS GAP-006). Every assertion is in logical units, so these hold at MF_HEADLESS_SCALE=2.
[Collection ("Headless")]
public sealed class HeadlessGestureInjectionTests
{
    private static (Form form, Panel target, Point at) Setup ()
    {
        HeadlessRenderer.Use ();
        var form = new Form ();
        var target = new Panel { Location = new Point (50, 40), Size = new Size (200, 150) };
        form.Controls.Add (target);
        HeadlessRenderer.CapturePng (form, 400, 300);
        return (form, target, WindowPoint.In (target, new Point (30, 20)));
    }

    [Fact]
    public void Swipe_reaches_the_control_under_the_point ()
    {
        var (form, target, at) = Setup ();
        using var _ = form;
        SwipeGestureEventArgs? seen = null;
        target.Swipe += (_, e) => seen = e;

        HeadlessRenderer.Swipe (form, at.X, at.Y, SwipeDirection.Left, velocityX: -900);

        Assert.NotNull (seen);
        Assert.Equal (SwipeDirection.Left, seen.Direction);
        Assert.Equal (-900, seen.VelocityX, 3);
    }

    [Fact]
    public void Pinch_reaches_the_control_under_the_point ()
    {
        var (form, target, at) = Setup ();
        using var _ = form;
        PinchGestureEventArgs? seen = null;
        target.Pinch += (_, e) => seen = e;

        HeadlessRenderer.Pinch (form, at.X, at.Y, 1.5);

        Assert.NotNull (seen);
        Assert.Equal (1.5, seen.Scale, 3);
    }

    [Fact]
    public void LongPress_reaches_the_control_under_the_point ()
    {
        var (form, target, at) = Setup ();
        using var _ = form;
        var pressed = 0;
        target.LongPress += (_, _) => pressed++;

        HeadlessRenderer.LongPress (form, at.X, at.Y);

        Assert.Equal (1, pressed);
    }

    [Fact]
    public void ScrollGesture_reaches_the_control_with_its_logical_movement ()
    {
        var (form, target, at) = Setup ();
        using var _ = form;
        ScrollGestureEventArgs? seen = null;
        target.ScrollGesture += (_, e) => seen = e;

        HeadlessRenderer.ScrollGesture (form, at.X, at.Y, 0, 25);

        Assert.NotNull (seen);
        Assert.Equal (new Point (0, 25), seen.Delta);
    }

    [Fact]
    public void A_gesture_outside_the_control_does_not_reach_it ()
    {
        var (form, target, _) = Setup ();
        using var _ = form;
        var swiped = 0;
        target.Swipe += (_, _) => swiped++;

        var outside = WindowPoint.In (target, new Point (target.Width + 20, 20));
        HeadlessRenderer.Swipe (form, outside.X, outside.Y, SwipeDirection.Left);

        Assert.Equal (0, swiped);
    }
}
