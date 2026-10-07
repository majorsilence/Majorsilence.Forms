using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// UserControl.Load existed as an event that nothing ever raised, so a ported WinForms UserControl
// compiled and then silently skipped its Load handler -- usually the one that fills it with data.
//
// CTL-05: it then fired too EARLY -- on Controls.Add into a form that had not been shown, i.e. in the
// middle of InitializeComponent. Upstream ControlCollection.Add creates a child only when the owner is
// already created, and CreateControl skips a hidden control, so Load waits for the form to be shown
// and a UserControl on a hidden page waits until the page is shown (Control.ControlCollection.cs,
// Control.cs CreateControl (bool)).
[Collection ("Headless")]
public class UserControlLoadTests
{
    public UserControlLoadTests () => HeadlessRenderer.Use ();

    private sealed class CountingUserControl : UserControl
    {
        public int LoadCount;
        public int HandleCreatedCount;

        public CountingUserControl ()
        {
            Load += (_, _) => LoadCount++;
            HandleCreated += (_, _) => HandleCreatedCount++;
        }
    }

    [Fact]
    public void Load_waits_until_the_form_is_shown ()
    {
        var control = new CountingUserControl ();
        using var form = new Form ();

        form.Controls.Add (control);

        Assert.Equal (0, control.LoadCount);
        Assert.Equal (0, control.HandleCreatedCount);
        Assert.False (control.IsHandleCreated);

        form.Show ();

        Assert.Equal (1, control.LoadCount);
        Assert.Equal (1, control.HandleCreatedCount);
        Assert.True (control.IsHandleCreated);
    }

    [Fact]
    public void Load_fires_on_Add_to_a_form_that_is_already_shown ()
    {
        using var form = new Form ();
        form.Show ();
        var control = new CountingUserControl ();

        form.Controls.Add (control);

        Assert.Equal (1, control.LoadCount);
    }

    [Fact]
    public void Load_fires_only_once ()
    {
        var control = new CountingUserControl ();
        using var form = new Form ();
        form.Show ();

        form.Controls.Add (control);
        control.CreateControl ();
        form.Controls.Remove (control);
        form.Controls.Add (control);

        Assert.Equal (1, control.LoadCount);
    }

    [Fact]
    public void Load_fires_for_a_nested_user_control ()
    {
        var inner = new CountingUserControl ();
        var outer = new UserControl ();
        outer.Controls.Add (inner);

        using var form = new Form ();
        form.Controls.Add (outer);
        form.Show ();

        Assert.Equal (1, inner.LoadCount);
    }

    [Fact]
    public void A_hidden_control_is_created_when_it_is_first_shown ()
    {
        using var form = new Form ();
        var page = new Panel { Visible = false };
        var control = new CountingUserControl ();
        page.Controls.Add (control);
        form.Controls.Add (page);
        form.Show ();

        Assert.Equal (0, control.LoadCount);   // the unselected-TabPage case: lazy, as upstream
        Assert.False (page.Created);

        page.Visible = true;

        Assert.Equal (1, control.LoadCount);
        Assert.True (page.Created);
    }

    [Fact]
    public void A_control_added_while_its_created_parent_is_hidden_is_created_when_the_parent_is_shown ()
    {
        using var form = new Form ();
        var page = new Panel ();
        form.Controls.Add (page);
        form.Show ();
        page.Visible = false;

        var control = new CountingUserControl ();
        page.Controls.Add (control);
        Assert.Equal (0, control.LoadCount);

        page.Visible = true;

        Assert.Equal (1, control.LoadCount);
    }

    // A form with no OS window of its own -- an MDI child, or a form added to a Controls collection --
    // is shown through a separate path; its controls must go live there too.
    [Fact]
    public void Load_fires_for_a_user_control_on_an_MDI_child ()
    {
        using var parent = new Form { IsMdiContainer = true };
        parent.Show ();
        var child = new Form { MdiParent = parent };
        var control = new CountingUserControl ();
        child.Controls.Add (control);

        child.Show ();

        Assert.Equal (1, control.LoadCount);
        Assert.True (control.Created);
    }

    [Fact]
    public void Load_fires_for_a_user_control_on_a_form_hosted_in_a_panel ()
    {
        using var host = new Form ();
        var panel = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add (panel);
        host.Show ();
        var hosted = new Form { TopLevel = false };
        var control = new CountingUserControl ();
        hosted.Controls.Add (control);
        panel.Controls.Add (hosted);

        hosted.Show ();

        Assert.Equal (1, control.LoadCount);
    }
}
