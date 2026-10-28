using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// ContextMenu.Show (Form, Point): upstream a Form is a Control, so a form's own mouse handler shows its
// context menu with Show (Me, e.Location). The point is in the form's client coordinates -- below the
// caption -- and the menu opens there through the same Opening/Opened path as the Control overload.
[Collection ("Headless")]
public class ContextMenuShowOnFormTests
{
    private static Point MenuLocation (ContextMenu menu)
    {
        var popup = menu.FindWindow ();

        Assert.NotNull (popup);

        return popup!.Location;
    }

    [Fact]
    public void The_point_is_in_the_form_s_client_coordinates ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        form.Show ();

        try {
            using var menu = new ContextMenuStrip ();
            menu.Items.Add (new ToolStripMenuItem { Text = "Cut" });

            var opened = 0;
            menu.Opened += (_, _) => opened++;

            var client = new Point (30, 40);
            menu.Show (form, client);

            Assert.Equal (1, opened);
            Assert.Equal (form.ContentRoot.PointToScreen (client), MenuLocation (menu));
            Assert.Same (form.ContentRoot, menu.SourceControl);

            // Below the caption: the client origin is not the window origin.
            Assert.NotEqual (form.ContentRoot.PointToScreen (Point.Empty), form.adapter.PointToScreen (Point.Empty));

            menu.Close ();
            menu.Show (form, client.X, client.Y);
            Assert.Equal (form.ContentRoot.PointToScreen (client), MenuLocation (menu));
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void A_cancelled_Opening_keeps_it_closed ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 300 };
        form.Show ();

        try {
            using var menu = new ContextMenuStrip ();
            menu.Items.Add (new ToolStripMenuItem { Text = "Cut" });
            menu.Opening += (_, e) => e.Cancel = true;

            var opened = 0;
            menu.Opened += (_, _) => opened++;

            menu.Show (form, new Point (10, 10));

            Assert.Equal (0, opened);
        } finally {
            form.Close ();
        }
    }
}
