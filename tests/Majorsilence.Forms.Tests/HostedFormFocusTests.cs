using Majorsilence.Forms;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Focusing a frame-hosted form must stay inside the control tree that hosts it, never activate an OS
/// window of its own.
/// </summary>
/// <remarks>
/// The backend's Activate orders the native window on screen WITHOUT going through Show, so the window
/// appeared while the framework still believed it was hidden -- which also made a later Hide a no-op,
/// leaving a full-size stray window beside the host that nothing could take back down. A docking
/// library focuses a document as it docks it, so this happened on every document.
/// </remarks>
public class HostedFormFocusTests
{
    private static (Form Host, Panel Panel, Form Child) BuildHosted ()
    {
        var host = new Form ();
        var panel = new Panel ();
        host.Controls.Add (panel);

        var child = new Form ();
        child.Parent = panel;
        child.Show ();          // A docking library focuses a document that is on screen, not a hidden one.

        // Disposing the host disposes the panel and the hosted child with it, which takes the child out
        // of Application.OpenForms -- that collection is global, and a leftover form there changes what
        // later tests see.
        return (host, panel, child);
    }

    [Fact]
    public void Focus_on_a_hosted_form_selects_its_frame ()
    {
        var (host, _, child) = BuildHosted ();
        using (host) {
            child.Focus ();

            Assert.NotNull (child.PanelHost);
            Assert.True (child.PanelHost!.Selected);
        }
    }

    [Fact]
    public void Focus_on_a_hosted_form_leaves_it_hosted ()
    {
        var (host, panel, child) = BuildHosted ();
        using (host) {
            child.Focus ();

            // Still composited into the panel -- not promoted back to a top-level window.
            Assert.True (panel.Controls.Contains (child));
            Assert.Same (panel, child.Parent);
        }
    }

    [Fact]
    public void Focus_reports_success_for_a_hosted_form ()
    {
        var (host, _, child) = BuildHosted ();
        using (host)
            Assert.True (child.Focus ());
    }

    [Fact]
    public void Activate_is_no_longer_a_no_op ()
    {
        // It was an empty body, so form.Activate () silently did nothing -- the WinForms idiom for
        // bringing an already-open window to the user.
        var (host, _, child) = BuildHosted ();
        using (host) {
            child.Activate ();

            Assert.True (child.PanelHost!.Selected);
        }
    }

    [Fact]
    public void Focus_on_a_top_level_form_still_succeeds ()
    {
        using var form = new Form ();
        Assert.True (form.Focus ());
    }

    [Fact]
    public void Unhosting_then_focusing_goes_back_to_the_window_path ()
    {
        var (host, _, child) = BuildHosted ();
        using (host) {
            child.Parent = null;

            Assert.Null (child.PanelHost);
            Assert.True (child.Focus ());     // top-level path, must not throw

            // Closing matters: an unhosted, shown form stays in the global Application.OpenForms and
            // would then be offered as a modal owner to every later test.
            child.Close ();
        }
    }
}
