using System.Drawing;
using System.Linq;
using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// An MDI container also hosts ordinary docked children -- a menu, a toolbar, a status bar and a
/// Fill'd panel. The MDI client has to end up behind all of them and take only the space they leave.
/// </summary>
/// <remarks>
/// Index 0 is the front of the z-order and the front is painted last, so an MDI client moved to the
/// front covers every sibling: a docking host's entire UI rendered underneath an opaque MDI client and
/// the window looked empty. WinForms leaves the client at the back and instead defers it to the end of
/// the dock pass, which is how it gets the leftover space without being in front.
/// </remarks>
public class MdiContainerZOrderTests
{
    private static Form BuildMdiHost ()
    {
        // The order DockSample's designer uses: children added first, IsMdiContainer set afterwards.
        var form = new Form { Width = 800, Height = 600 };

        var fill = new Panel { Dock = DockStyle.Fill, Name = "fill" };
        var top = new Panel { Dock = DockStyle.Top, Height = 24, Name = "top" };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 22, Name = "bottom" };

        form.Controls.Add (fill);
        form.Controls.Add (top);
        form.Controls.Add (bottom);
        form.IsMdiContainer = true;

        return form;
    }

    private static Control MdiClientOf (Form form)
        => form.Controls.Cast<Control> ().Single (c => c is MdiClient);

    [Fact]
    public void The_MDI_client_stays_at_the_back_of_the_z_order ()
    {
        using var form = BuildMdiHost ();
        var children = form.Controls.Cast<Control> ().ToList ();

        // Back of the z-order == last index. Anything nearer 0 paints over the siblings.
        Assert.Same (MdiClientOf (form), children[^1]);
    }

    [Fact]
    public void The_MDI_client_is_painted_before_its_siblings ()
    {
        using var form = BuildMdiHost ();
        var order = form.Controls.GetControlsPaintOrder ().ToList ();

        Assert.Same (MdiClientOf (form), order[0]);
    }

    [Fact]
    public void Docked_siblings_still_get_their_own_slices ()
    {
        using var form = BuildMdiHost ();
        form.PerformLayout ();

        var top = form.Controls.Cast<Control> ().Single (c => c.Name == "top");
        var bottom = form.Controls.Cast<Control> ().Single (c => c.Name == "bottom");

        Assert.Equal (24, top.Height);
        Assert.Equal (22, bottom.Height);
    }

    [Fact]
    public void The_MDI_client_gets_the_leftover_space_not_the_whole_area ()
    {
        // The point of deferring it: even though it sits at the back, the top/bottom strips have
        // already claimed their bands by the time it is placed.
        using var form = BuildMdiHost ();
        form.PerformLayout ();

        var client = MdiClientOf (form);
        var fill = form.Controls.Cast<Control> ().Single (c => c.Name == "fill");

        Assert.Equal (fill.Bounds, client.Bounds);
        Assert.True (client.Top >= 24, $"MDI client top {client.Top} should sit below the 24px top strip");
    }

    [Fact]
    public void Turning_the_container_off_removes_the_client ()
    {
        using var form = BuildMdiHost ();
        form.IsMdiContainer = false;

        Assert.DoesNotContain (form.Controls.Cast<Control> (), c => c is MdiClient);
    }
}
