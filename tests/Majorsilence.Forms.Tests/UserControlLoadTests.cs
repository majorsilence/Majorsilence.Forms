using Xunit;

namespace Majorsilence.Forms.Tests;

// UserControl.Load existed as an event that nothing ever raised, so a ported WinForms UserControl
// compiled and then silently skipped its Load handler -- usually the one that fills it with data.
public class UserControlLoadTests
{
    private sealed class CountingUserControl : UserControl
    {
        public int LoadCount;

        public CountingUserControl ()
        {
            Load += (_, _) => LoadCount++;
        }
    }

    [Fact]
    public void Load_fires_when_the_control_goes_live ()
    {
        var control = new CountingUserControl ();
        var form = new Form ();

        form.Controls.Add (control);

        Assert.Equal (1, control.LoadCount);
    }

    [Fact]
    public void Load_fires_only_once ()
    {
        var control = new CountingUserControl ();
        var form = new Form ();

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

        var form = new Form ();
        form.Controls.Add (outer);

        Assert.Equal (1, inner.LoadCount);
    }
}
