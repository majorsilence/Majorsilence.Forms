using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// An explicit <see cref="WindowBase.PerformLayout"/> or <see cref="WindowBase.ResumeLayout(bool)"/>
/// raises the window's own <see cref="WindowBase.OnLayout"/>, whether or not it has been shown yet.
/// </summary>
/// <remarks>
/// The root adapter forwards its layout pass to the window only once the window is on screen — adding
/// controls during a Form subclass's construction otherwise runs that subclass's OnLayout override before
/// its constructor has, which is a real crash. An explicit request from the consumer is a different thing
/// and must arrive regardless: a window that decides its own visibility in OnLayout can never become
/// visible otherwise. DockPanelSuite's FloatWindow does exactly that — it sets
/// <c>Visible = VisibleNestedPanes.Count > 0</c> there and constructs itself inside
/// SuspendLayout/ResumeLayout so the ResumeLayout runs it. Unraised, a document dragged out to float
/// landed in a window that was never shown, which reads as the document vanishing.
/// </remarks>
public class WindowLayoutRequestTests
{
    private sealed class CountingForm : Form
    {
        public int Layouts;

        protected internal override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);
            Layouts++;
        }
    }

    [Fact]
    public void PerformLayout_raises_OnLayout_on_a_window_that_was_never_shown ()
    {
        using var form = new CountingForm ();

        form.PerformLayout ();

        Assert.True (form.Layouts > 0, "an explicit PerformLayout must reach the window's OnLayout");
    }

    [Fact]
    public void ResumeLayout_raises_OnLayout_on_a_window_that_was_never_shown ()
    {
        using var form = new CountingForm ();

        form.SuspendLayout ();
        form.ResumeLayout ();

        Assert.True (form.Layouts > 0, "ResumeLayout must reach the window's OnLayout");
    }

    [Fact]
    public void ResumeLayout_without_performing_layout_raises_nothing ()
    {
        using var form = new CountingForm ();

        form.SuspendLayout ();
        form.ResumeLayout (performLayout: false);

        Assert.Equal (0, form.Layouts);
    }

    [Fact]
    public void A_window_that_becomes_visible_from_OnLayout_ends_up_visible ()
    {
        // The FloatWindow shape, end to end.
        using var form = new VisibilityFromLayoutForm ();

        form.SuspendLayout ();
        form.HasContent = true;
        form.ResumeLayout ();

        Assert.True (form.Visible);
    }

    private sealed class VisibilityFromLayoutForm : Form
    {
        public bool HasContent;

        protected internal override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);
            Visible = HasContent;
        }
    }

    [Fact]
    public void An_OnLayout_that_lays_out_again_does_not_recurse_forever ()
    {
        using var form = new RelayoutingForm ();

        form.PerformLayout ();

        Assert.True (form.Layouts is > 0 and < 10, $"expected a bounded number of layouts, got {form.Layouts}");
    }

    private sealed class RelayoutingForm : Form
    {
        public int Layouts;

        protected internal override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            if (++Layouts < 100)
                PerformLayout ();
        }
    }
}
