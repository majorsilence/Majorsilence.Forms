using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Assigning a window the size or location it already has does not reach the platform.
/// </summary>
/// <remarks>
/// Not a micro-optimisation: every write is a round trip to the window server, and code that recomputes
/// geometry per mouse-move sets the same value over and over. Measured on a float-window drag in
/// DockPanelSuite, 61 of 85 size writes were no-ops — enough platform traffic that the drag outline
/// visibly lagged the cursor until the mouse stopped moving, which is worse the faster the pointer
/// reports (a trackpad emits far more events than a scripted drag).
/// </remarks>
[Collection ("Headless")]
public class WindowGeometryWriteTests
{
    [Fact]
    public void Re_assigning_the_same_size_changes_nothing ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 300, Height = 200 };
        var before = form.Size;

        form.Size = before;

        Assert.Equal (before, form.Size);
    }

    [Fact]
    public void A_different_size_is_applied ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 300, Height = 200 };

        form.Size = new Size (321, 234);

        Assert.Equal (new Size (321, 234), form.Size);
    }

    [Fact]
    public void Re_assigning_the_same_location_changes_nothing ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 300, Height = 200 };
        form.Location = new Point (40, 50);

        var moves = 0;
        form.Move += (_, _) => moves++;

        form.Location = new Point (40, 50);

        Assert.Equal (0, moves);
    }

    [Fact]
    public void Setting_Bounds_to_the_current_value_raises_nothing ()
    {
        // The shape a drag takes: recompute the whole rectangle every mouse-move and assign it.
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 300, Height = 200 };
        form.Location = new Point (10, 20);

        var moves = 0;
        var resizes = 0;
        form.Move += (_, _) => moves++;
        form.Resize += (_, _) => resizes++;

        var current = form.Bounds;
        for (var i = 0; i < 20; i++)
            form.Bounds = current;

        Assert.Equal (0, moves);
        Assert.Equal (0, resizes);
    }

    [Fact]
    public void A_changed_bounds_still_moves_and_resizes ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 300, Height = 200 };
        form.Location = new Point (10, 20);

        form.Bounds = new Rectangle (60, 70, 400, 250);

        Assert.Equal (new Point (60, 70), form.Location);
        Assert.Equal (new Size (400, 250), form.Size);
    }
}
