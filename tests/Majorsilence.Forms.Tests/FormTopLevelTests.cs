using Majorsilence.Forms;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="Form.TopLevel"/> decides whether a form owns an OS window. Setting it false gives the
/// window up; the form is then composited by whatever hosts it.
/// </summary>
/// <remarks>
/// It was stored and never acted on. That breaks the <c>form.TopLevel = false; panel.Controls.Add (form)</c>
/// idiom every WinForms app uses to put a form inside a control — and a docking library sets it on every
/// dock-state change, so re-docking a floated document left the old window behind as a large blank
/// rectangle sitting over the application.
/// </remarks>
[Collection ("Headless")]
public class FormTopLevelTests
{
    [Fact]
    public void A_form_owns_a_window_by_default ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form ();
        form.Show ();

        Assert.True (form.TopLevel);
        Assert.True (HeadlessRenderer.OwnsShownWindow (form));
    }

    [Fact]
    public void Clearing_TopLevel_gives_up_the_window ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form ();
        form.Show ();

        form.TopLevel = false;

        Assert.False (HeadlessRenderer.OwnsShownWindow (form));
    }

    [Fact]
    public void A_form_that_is_not_top_level_does_not_raise_a_window_when_shown ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { TopLevel = false };

        form.Show ();

        Assert.False (HeadlessRenderer.OwnsShownWindow (form));
        Assert.True (form.Visible);       // visible in the bookkeeping sense, awaiting a host
    }

    [Fact]
    public void Restoring_TopLevel_puts_the_window_back ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form ();
        form.Show ();
        form.TopLevel = false;

        form.TopLevel = true;

        Assert.True (HeadlessRenderer.OwnsShownWindow (form));
    }

    [Fact]
    public void The_TopLevel_false_then_host_idiom_ends_with_no_window_of_its_own ()
    {
        // The shape WinForms code actually writes, and what a docking library does when it re-docks.
        HeadlessRenderer.Use ();

        using var host = new Form { Width = 400, Height = 300 };
        var panel = new Panel { Width = 300, Height = 200 };
        host.Controls.Add (panel);
        host.Show ();

        var child = new Form ();
        child.Show ();
        child.TopLevel = false;
        panel.Controls.Add (child);

        Assert.False (HeadlessRenderer.OwnsShownWindow (child));
        Assert.True (panel.Controls.Contains (child));

        child.Close ();
    }

    [Fact]
    public void Focusing_a_form_that_is_not_top_level_does_not_raise_a_window ()
    {
        // Activate/Focus orders a window on screen even when it was never shown, and the platform's later
        // Hide is then a no-op -- so the window is stranded. A docking library detaches a form and focuses
        // it mid-re-dock, which left a blank window over the application.
        HeadlessRenderer.Use ();

        using var form = new Form { TopLevel = false };
        form.Show ();

        form.Focus ();

        Assert.False (HeadlessRenderer.OwnsShownWindow (form));
    }

    [Fact]
    public void Focusing_an_invisible_form_does_not_raise_a_window ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form ();

        form.Focus ();

        Assert.False (HeadlessRenderer.OwnsShownWindow (form));
    }

    [Fact]
    public void BringToFront_does_not_raise_a_window_the_form_does_not_own ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form { TopLevel = false };
        form.Show ();

        form.BringToFront ();

        Assert.False (HeadlessRenderer.OwnsShownWindow (form));
    }

    [Fact]
    public void Focusing_a_normal_visible_form_still_activates_it ()
    {
        HeadlessRenderer.Use ();

        using var form = new Form ();
        form.Show ();

        form.Focus ();

        Assert.True (HeadlessRenderer.OwnsShownWindow (form));
    }
}
