using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Assigning <see cref="Form.Parent"/> hosts the form inside that control, the same as
/// <c>parent.Controls.Add (form)</c>.
/// </summary>
/// <remarks>
/// The setter used to only store the value, which made <c>form.Parent = panel</c> a silent no-op: the
/// form stayed a separate top-level window, so hosting forms by assigning Parent -- how WinForms code
/// does it, and how a docking library puts a document into a pane -- produced a stray empty window per
/// form and nothing inside the container.
/// </remarks>
public class FormHostingTests
{
    [Fact]
    public void Assigning_Parent_hosts_the_form_in_that_control ()
    {
        using var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Parent = panel;

        Assert.True (panel.Controls.Contains (child));
    }

    [Fact]
    public void Parent_reads_back_as_the_control_it_was_assigned ()
    {
        using var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Parent = panel;

        // Not the internal frame: callers use Parent to add siblings alongside the hosted form.
        Assert.Same (panel, child.Parent);
    }

    [Fact]
    public void Assigning_Parent_null_takes_the_form_back_out ()
    {
        using var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Parent = panel;
        child.Parent = null;

        Assert.False (panel.Controls.Contains (child));
        Assert.Null (child.Parent);
    }

    [Fact]
    public void Re_assigning_Parent_moves_the_form_rather_than_hosting_it_twice ()
    {
        using var host = new Form ();
        var first = new Panel ();
        var second = new Panel ();
        host.Controls.Add (first);
        host.Controls.Add (second);

        var child = new Form ();
        child.Parent = first;
        child.Parent = second;

        Assert.False (first.Controls.Contains (child));
        Assert.True (second.Controls.Contains (child));
    }

    [Fact]
    public void Assigning_the_same_parent_twice_is_a_no_op ()
    {
        using var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Parent = panel;
        var count = panel.Controls.Count;
        child.Parent = panel;

        Assert.Equal (count, panel.Controls.Count);
    }

    [Fact]
    public void A_form_shown_before_being_parented_does_not_leave_its_window_up ()
    {
        // WinForms code sets Visible and Parent as separate steps, in either order, because a WinForms
        // Form given a Parent stops being a top-level window. Showing first used to leave a stray empty
        // OS window beside the host.
        using var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Show ();
        child.Parent = panel;

        Assert.True (panel.Controls.Contains (child));

        // Still visible -- it is painted inside the host now, not hidden.
        Assert.True (child.Visible);

        child.Close ();     // Application.OpenForms is global; do not leave it in there.
    }

    [Fact]
    public void Hosting_a_form_that_was_never_shown_still_works ()
    {
        using var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Parent = panel;

        Assert.True (panel.Controls.Contains (child));
    }
}
