using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadDock.SerializableAutoHideContainer and MdiChildrenDockType, and DockWindowPlaceholder as the dock
// window it is in Telerik -- the members designer code for a dock uses that the compat dock did not have.
// Behaviour as measured on UI.for.WinForms 2026.2.520.
[Collection ("Headless")]
public class TelerikDockAutoHideSerializationTests
{
    // A dock built the way InitializeComponent builds one: windows in a strip, the recorded groups, then EndInit.
    private static RadDock DesignerDock (out Form form, out ToolWindow search, out ToolWindow results, System.Action<RadDock, ToolWindow, ToolWindow> record)
    {
        HeadlessRenderer.Use ();

        form = new Form { Size = new Size (500, 400) };
        var dock = new RadDock { Bounds = new Rectangle (0, 0, 480, 360) };
        var strip = new ToolTabStrip ();
        search = new ToolWindow ("Search") { Name = "twSearch" };
        results = new ToolWindow ("Results") { Name = "twResults" };

        ((ISupportInitialize) dock).BeginInit ();
        dock.Controls.Add (strip);
        strip.Controls.Add (search);
        strip.Controls.Add (results);
        record (dock, search, results);
        form.Controls.Add (dock);
        ((ISupportInitialize) dock).EndInit ();

        form.Show ();
        return dock;
    }

    [Fact]
    public void A_recorded_group_auto_hides_the_windows_its_placeholders_name_when_initialization_ends ()
    {
        AutoHideGroup? group = null;
        var dock = DesignerDock (out var form, out var search, out var results, (d, s, r) => {
            group = new AutoHideGroup ();
            group.Windows.Add (new DockWindowPlaceholder (s));
            d.SerializableAutoHideContainer.BottomAutoHideGroups.Add (group);

            // Until EndInit the group is only recorded.
            Assert.Equal (DockState.Docked, s.DockState);
        });

        using (form) {
            Assert.Equal (DockState.AutoHide, search.DockState);
            Assert.Equal (DockState.Docked, search.PreviousDockState);
            Assert.Equal (DockState.Docked, results.DockState);

            // Applied once: the container is emptied, the group itself keeps its windows.
            Assert.Empty (dock.SerializableAutoHideContainer.BottomAutoHideGroups);
            Assert.Single (group!.Windows);
        }
    }

    [Fact]
    public void Every_side_is_applied_a_window_can_be_named_directly_and_an_unknown_name_is_passed_over ()
    {
        var dock = DesignerDock (out var form, out var search, out var results, (d, s, r) => {
            d.SerializableAutoHideContainer.LeftAutoHideGroups.Add (new AutoHideGroup (new DockWindowPlaceholder ("nowhere")));
            d.SerializableAutoHideContainer.TopAutoHideGroups.Add (new AutoHideGroup (r));
            d.SerializableAutoHideContainer.RightAutoHideGroups.Add (new AutoHideGroup ());
            d.SerializableAutoHideContainer.BottomAutoHideGroups.Add (new AutoHideGroup (new DockWindowPlaceholder ("twSearch")));
        });

        using (form) {
            Assert.Equal (DockState.AutoHide, search.DockState);
            Assert.Equal (DockState.AutoHide, results.DockState);

            var container = dock.SerializableAutoHideContainer;
            Assert.Empty (container.LeftAutoHideGroups);
            Assert.Empty (container.TopAutoHideGroups);
            Assert.Empty (container.RightAutoHideGroups);
            Assert.Empty (container.BottomAutoHideGroups);
        }
    }

    [Fact]
    public void A_group_added_after_initialization_waits_for_the_next_EndInit ()
    {
        var dock = DesignerDock (out var form, out var search, out _, (_, _, _) => { });

        using (form) {
            dock.SerializableAutoHideContainer.BottomAutoHideGroups.Add (new AutoHideGroup (new DockWindowPlaceholder (search)));
            Assert.Equal (DockState.Docked, search.DockState);

            ((ISupportInitialize) dock).BeginInit ();
            ((ISupportInitialize) dock).EndInit ();
            Assert.Equal (DockState.AutoHide, search.DockState);
        }
    }

    [Fact]
    public void An_empty_group_changes_nothing ()
    {
        var dock = DesignerDock (out var form, out var search, out var results, (d, _, _) =>
            d.SerializableAutoHideContainer.BottomAutoHideGroups.Add (new AutoHideGroup ()));

        using (form) {
            Assert.Equal (DockState.Docked, search.DockState);
            Assert.Equal (DockState.Docked, results.DockState);
            Assert.Empty (dock.SerializableAutoHideContainer.BottomAutoHideGroups);
        }
    }

    [Fact]
    public void A_placeholder_is_a_dock_window_whose_name_and_caption_are_its_own ()
    {
        var window = new ToolWindow ("Search") { Name = "twSearch" };
        using var placeholder = new DockWindowPlaceholder (window);

        Assert.IsAssignableFrom<DockWindow> (placeholder);
        Assert.Equal ("twSearch", placeholder.DockWindowName);
        Assert.Equal ("Search", placeholder.DockWindowText);
        Assert.Equal (string.Empty, placeholder.Name);
        Assert.Equal (string.Empty, placeholder.Text);

        // The designer's lines for one.
        placeholder.Text = "DockWindowPlaceholder1";
        placeholder.Name = "DockWindowPlaceholder1";
        placeholder.PreviousDockState = DockState.Docked;
        placeholder.Size = new Size (200, 200);

        Assert.Equal ("Search", placeholder.DockWindowText);
        Assert.Equal ("twSearch", placeholder.DockWindowName);

        placeholder.DockWindowText = "Find";
        Assert.Equal ("DockWindowPlaceholder1", placeholder.Text);
    }

    [Fact]
    public void MDI_children_are_hosted_as_documents_by_default ()
    {
        using var dock = new RadDock ();

        Assert.Equal (DockType.Document, dock.MdiChildrenDockType);

        dock.MdiChildrenDockType = DockType.ToolWindow;
        Assert.Equal (DockType.ToolWindow, dock.MdiChildrenDockType);
    }
}
