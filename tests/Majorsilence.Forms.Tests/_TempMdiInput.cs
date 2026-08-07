using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

public class _TempMdiInput
{
    [Fact]
    public void PlainForm_TextBox_receives_typing ()
    {
        var form = new Form ();
        var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill };
        form.Controls.Add (tb);
        HeadlessRenderer.CapturePng (form, 400, 300);

        tb.Select ();
        HeadlessRenderer.TextInput (form, "hi");

        Assert.Equal ("hi", tb.Text);
    }

    [Fact]
    public void MdiChild_TextBox_receives_typing ()
    {
        var parent = new Form { IsMdiContainer = true };
        HeadlessRenderer.CapturePng (parent, 600, 400);

        var child = new Form ();
        var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill };
        child.Controls.Add (tb);
        child.MdiParent = parent;
        child.Show ();

        HeadlessRenderer.CapturePng (parent, 600, 400);

        tb.Select ();

        HeadlessRenderer.TextInput (parent, "hi");
        HeadlessRenderer.KeyDown (parent, Keys.Back);

        Assert.Equal ("h", tb.Text);
    }
}
