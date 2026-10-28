using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadDock.Contains (string), measured on Telerik UI.for.WinForms 2026.2.520: true for a docked window's Name, false
// for its caption or an unknown name, and still true once the window is closed to hidden.
[Collection ("Headless")]
public class TelerikDockContainsTests
{
    [Fact]
    public void Contains_finds_a_dock_window_by_its_name ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new Size (560, 440) };
        var dock = new RadDock { Bounds = new Rectangle (0, 0, 480, 360) };
        form.Controls.Add (dock);
        form.Show ();

        Assert.False (dock.Contains ("Priorpanel"));

        var window = new ToolWindow ("Prior") { Name = "Priorpanel" };
        dock.DockWindow (window, DockPosition.Left);

        Assert.True (dock.Contains ("Priorpanel"));
        Assert.False (dock.Contains ("Prior"));
        Assert.False (dock.Contains ("priorpanel"));
        Assert.False (dock.Contains ("Nope"));

        window.Close ();
        Assert.Equal (DockWindowCloseAction.Hide, window.CloseAction);
        Assert.True (dock.Contains ("Priorpanel"));

        dock.DockWindow (new DocumentWindow ("Future") { Name = "Futurepanel" });
        Assert.True (dock.Contains ("Futurepanel"));
    }
}
