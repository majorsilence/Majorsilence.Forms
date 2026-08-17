using System.Drawing;
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Screen coordinates mean the same thing to every window: a control's client origin on screen is where
/// the platform actually puts it, not where the window's own <see cref="WindowBase.Location"/> is.
/// </summary>
/// <remarks>
/// Those two differ by the chrome the platform draws above the client area — a native title bar, ~32px.
/// Measuring from the window's Location made <see cref="Control.MousePosition"/> that far off from true
/// screen coordinates: self-consistent for the window the pointer was over, and wrong for every other
/// window converting it. A drag overlay hit-testing its drop guides against the cursor is exactly that
/// case — the guides tested a title bar's height above where they had been drawn, so dropping on one
/// never registered and a document could not be docked by hand.
///
/// Headless has no chrome, which is why this went unseen; <see cref="HeadlessRenderer.ChromeOffset"/>
/// simulates a platform that has some.
/// </remarks>
[Collection ("Headless")]
public class ScreenCoordinateAgreementTests
{
    private static readonly Size TitleBar = new (0, 32);

    private static void WithChrome (System.Action body)
    {
        HeadlessRenderer.Use ();
        HeadlessRenderer.ChromeOffset = TitleBar;

        try { body (); } finally { HeadlessRenderer.ChromeOffset = Size.Empty; }
    }

    [Fact]
    public void PointToScreen_measures_from_the_client_origin_not_the_window_location ()
    {
        WithChrome (() => {
            using var form = new Form { Width = 300, Height = 200 };
            form.Location = new Point (100, 50);
            form.Show ();

            var origin = form.ContentControl!.PointToScreen (Point.Empty);

            Assert.Equal (new Point (100, 50 + TitleBar.Height), origin);
        });
    }

    [Fact]
    public void The_tracked_cursor_position_is_in_true_screen_coordinates ()
    {
        WithChrome (() => {
            using var form = new Form { Width = 400, Height = 300 };
            form.Location = new Point (10, 20);
            form.Show ();
            HeadlessRenderer.CapturePng (form, 400, 300);

            HeadlessRenderer.MouseMove (form, 60, 70);   // logical client coordinates

            // Screen = the client origin (chrome included) plus the logical offset in desktop pixels.
            // Spelled out rather than hard-coded so this still means something under a simulated HiDPI
            // display, where the same logical point lands twice as far from the origin.
            var scale = form.DesktopScaling;
            var expected = new Point (
                10 + (int)System.Math.Round (60 * scale),
                20 + TitleBar.Height + (int)System.Math.Round (70 * scale));

            Assert.Equal (expected, Cursor.Position);
        });
    }

    [Fact]
    public void A_second_window_resolves_the_cursor_into_its_own_client_space ()
    {
        // The drag-overlay case: one window reports where the pointer is, a different one converts it.
        WithChrome (() => {
            using var pointerWindow = new Form { Width = 400, Height = 300 };
            pointerWindow.Location = new Point (0, 0);
            pointerWindow.Show ();

            using var overlay = new Form { Width = 400, Height = 300 };
            overlay.Location = new Point (0, 0);
            overlay.Show ();

            HeadlessRenderer.CapturePng (pointerWindow, 400, 300);
            HeadlessRenderer.MouseMove (pointerWindow, 120, 90);

            var inOverlay = overlay.ContentControl!.PointToClient (Cursor.Position);

            // Both windows sit at the same place, so the overlay must see the same client point.
            Assert.Equal (new Point (120, 90), inOverlay);
        });
    }

    [Fact]
    public void PointToClient_stays_the_inverse_of_PointToScreen ()
    {
        WithChrome (() => {
            using var form = new Form { Width = 400, Height = 300 };
            form.Location = new Point (75, 40);
            form.Show ();

            var adapter = form.ContentControl!;
            var local = new Point (33, 44);

            Assert.Equal (local, adapter.PointToClient (adapter.PointToScreen (local)));
        });
    }

    [Fact]
    public void Without_chrome_nothing_moves ()
    {
        // The headless default, and every existing measurement taken through it.
        HeadlessRenderer.Use ();

        using var form = new Form { Width = 300, Height = 200 };
        form.Location = new Point (100, 50);
        form.Show ();

        Assert.Equal (new Point (100, 50), form.ContentControl!.PointToScreen (Point.Empty));
    }
}
