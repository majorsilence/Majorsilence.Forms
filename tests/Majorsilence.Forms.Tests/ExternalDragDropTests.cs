using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Drags that come from outside the application (files from the desktop's file manager, text from
/// another app): the backend feeds the window's external-drag entry points, and the control under the
/// pointer sees the same DragEnter / DragOver / DragDrop / DragLeave as an in-process DoDragDrop.
/// </summary>
[Collection ("Headless")]
public class ExternalDragDropTests
{
    private static Point WindowPoint (Control control, int x, int y)
    {
        var p = new Point (x, y);

        for (var c = control; c.Parent is not null; c = c.Parent)
            p.Offset (c.Left, c.Top);

        return new Point (control.LogicalToDeviceUnits (p.X), control.LogicalToDeviceUnits (p.Y));
    }

    private static (Form form, Panel plain, Panel target) Scene ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (400, 300) };
        var plain = new Panel { Bounds = new Rectangle (10, 10, 100, 100) };
        var target = new Panel { Bounds = new Rectangle (200, 10, 150, 150), AllowDrop = true };
        form.Controls.Add (plain);
        form.Controls.Add (target);
        form.Show ();
        return (form, plain, target);
    }

    private static DataObject Files (params string[] paths)
    {
        var data = new DataObject ();
        data.SetData (DataFormats.FileDrop.Name, paths);
        return data;
    }

    [Fact]
    public void Files_dropped_from_outside_reach_the_target_as_FileDrop ()
    {
        var (form, _, target) = Scene ();
        using var _form = form;
        var entered = 0;
        string[]? dropped = null;
        target.DragEnter += (_, e) => {
            entered++;
            if (e.Data!.GetDataPresent (DataFormats.FileDrop.Name))
                e.Effect = DragDropEffects.Copy;
        };
        target.DragDrop += (_, e) => dropped = e.Data!.GetData (DataFormats.FileDrop.Name) as string[];

        var data = Files ("/tmp/parcels.shp", "/tmp/roads.shp");
        var at = WindowPoint (target, 20, 20);

        var over = form.HandleExternalDragOver (data, DragDropEffects.Copy | DragDropEffects.Link, at.X, at.Y, Keys.None);
        Assert.Equal (DragDropEffects.Copy, over);

        var result = form.HandleExternalDrop (data, DragDropEffects.Copy | DragDropEffects.Link, at.X, at.Y, Keys.None);

        Assert.Equal (DragDropEffects.Copy, result);
        Assert.Equal (1, entered);
        Assert.Equal (new[] { "/tmp/parcels.shp", "/tmp/roads.shp" }, dropped);
        Assert.Null (DragDropSession.Active);
    }

    [Fact]
    public void A_control_without_AllowDrop_refuses_the_drag ()
    {
        var (form, plain, target) = Scene ();
        using var _form = form;
        var dropped = 0;
        target.DragDrop += (_, _) => dropped++;
        var data = Files ("/tmp/a.txt");
        var at = WindowPoint (plain, 20, 20);

        Assert.Equal (DragDropEffects.None, form.HandleExternalDragOver (data, DragDropEffects.Copy, at.X, at.Y, Keys.None));
        Assert.Equal (DragDropEffects.None, form.HandleExternalDrop (data, DragDropEffects.Copy, at.X, at.Y, Keys.None));
        Assert.Equal (0, dropped);
    }

    [Fact]
    public void Leaving_the_window_raises_DragLeave_and_ends_the_drag ()
    {
        var (form, _, target) = Scene ();
        using var _form = form;
        var left = 0;
        target.DragEnter += (_, e) => e.Effect = DragDropEffects.Copy;
        target.DragLeave += (_, _) => left++;
        var at = WindowPoint (target, 20, 20);

        form.HandleExternalDragOver (Files ("/tmp/a.txt"), DragDropEffects.Copy, at.X, at.Y, Keys.None);
        form.HandleExternalDragLeave ();

        Assert.Equal (1, left);
        Assert.Null (DragDropSession.Active);
    }

    [Fact]
    public void Moving_between_targets_raises_leave_and_enter ()
    {
        var (form, plain, target) = Scene ();
        using var _form = form;
        plain.AllowDrop = true;
        var events = new List<string> ();
        plain.DragEnter += (_, e) => { events.Add ("plain enter"); e.Effect = DragDropEffects.Copy; };
        plain.DragLeave += (_, _) => events.Add ("plain leave");
        target.DragEnter += (_, e) => { events.Add ("target enter"); e.Effect = DragDropEffects.Copy; };
        target.DragOver += (_, _) => events.Add ("target over");
        var data = Files ("/tmp/a.txt");

        var a = WindowPoint (plain, 20, 20);
        var b = WindowPoint (target, 20, 20);
        var c = WindowPoint (target, 30, 30);
        form.HandleExternalDragOver (data, DragDropEffects.Copy, a.X, a.Y, Keys.None);
        form.HandleExternalDragOver (data, DragDropEffects.Copy, b.X, b.Y, Keys.None);
        form.HandleExternalDragOver (data, DragDropEffects.Copy, c.X, c.Y, Keys.None);
        form.HandleExternalDragLeave ();

        Assert.Equal (new[] { "plain enter", "plain leave", "target enter", "target over" }, events);
    }

    [Fact]
    public void A_drop_without_a_drag_over_first_still_reaches_the_target ()
    {
        var (form, _, target) = Scene ();
        using var _form = form;
        string? text = null;
        target.DragEnter += (_, e) => e.Effect = DragDropEffects.Copy;
        target.DragDrop += (_, e) => text = e.Data!.GetData (DataFormats.Text.Name) as string;
        var data = new DataObject ();
        data.SetData (DataFormats.Text.Name, "from another app");
        var at = WindowPoint (target, 20, 20);

        Assert.Equal (DragDropEffects.Copy, form.HandleExternalDrop (data, DragDropEffects.Copy, at.X, at.Y, Keys.None));
        Assert.Equal ("from another app", text);
    }
}
