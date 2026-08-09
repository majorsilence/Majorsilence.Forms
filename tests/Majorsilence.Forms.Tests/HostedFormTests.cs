using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Covers the WinForms "MDI-lite" dashboard idiom -- form.TopLevel = false; form.Dock = Fill;
// panel.Controls.Add (form) -- which is how a large share of WinForms apps swap pages inside a shell.
// A Form is not a Control here, so hosting goes through FormHost; these tests pin the behaviour the
// idiom depends on rather than that implementation detail.
public sealed class HostedFormTests : IDisposable
{
    private readonly List<Form> opened = new ();

    /// <summary>
    /// Closes every form this fixture showed. Showing a hosted form registers it in
    /// Application.OpenForms, which is process-wide: ShowDialog and MessageBox pick their modal owner
    /// out of it and only fall back to a non-blocking Show when it is empty. A form left open here
    /// therefore becomes some other test's modal owner and hangs the whole run.
    /// </summary>
    public void Dispose ()
    {
        foreach (var form in opened) {
            form.Close ();

            // One test cancels closing on purpose, so Close alone cannot be trusted to have
            // deregistered the form.
            Application.OpenForms.Remove (form);
        }

        opened.Clear ();
    }

    // A dashboard's body panel lives on a shell form, and layout only runs for a control that is in a
    // live tree -- an unparented panel never docks its children. Deliberately does not Show the shell
    // (see PaintSurface).
    private static Panel BodyPanel ()
    {
        var shell = new Form { Width = 400, Height = 300 };
        var panel = new Panel { Width = 300, Height = 200, BackColor = Color.White };
        shell.Controls.Add (panel);

        return panel;
    }

    private Form HostedForm (Panel panel, Color? fill = null)
    {
        var form = new Form { Width = 200, Height = 100 };

        if (fill is { } color)
            form.Controls.Add (new Panel { Dock = DockStyle.Fill, BackColor = color });

        form.TopLevel = false;
        form.Dock = DockStyle.Fill;
        panel.Controls.Add (form);
        opened.Add (form);

        return form;
    }

    [Fact]
    public void Form_can_be_added_to_a_control_collection ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        Assert.True (panel.Controls.Contains (form));
        Assert.Equal (1, panel.Controls.Count);
    }

    [Fact]
    public void Hosted_form_reports_the_panel_as_its_parent ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        // Not the frame -- app code uses Parent to add siblings next to the hosted form.
        Assert.Same (panel, form.Parent);
    }

    [Fact]
    public void Docked_hosted_form_fills_the_panel ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        // Add leaves the frame as invisible as the form was, and dock layout skips invisible controls
        // -- exactly as in WinForms. The idiom's Show() is what puts it on screen and sizes it.
        form.Show ();
        panel.PerformLayout ();

        Assert.Equal (new Size (300, 200), form.Size);
    }

    [Fact]
    public void Hosted_form_is_not_laid_out_until_it_is_shown ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        panel.PerformLayout ();

        Assert.Equal (Size.Empty, form.Size);
    }

    [Fact]
    public void Hosted_form_content_paints_into_the_panel ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel, fill: Color.Red);

        form.Show ();
        panel.PerformLayout ();

        using var bitmap = PaintSurface.RenderOnForm (panel);

        // The hosted form's own child fills it, so the middle of the panel must be that child's colour.
        var pixel = bitmap.GetPixel (150, 100);
        Assert.Equal (Color.Red.R, pixel.Red);
        Assert.Equal (Color.Red.G, pixel.Green);
        Assert.Equal (Color.Red.B, pixel.Blue);
    }

    [Fact]
    public void Showing_a_hosted_form_raises_Load_before_Shown ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        var order = new List<string> ();
        form.Load += (_, _) => order.Add ("load");
        form.Shown += (_, _) => order.Add ("shown");

        form.Show ();

        Assert.Equal (new[] { "load", "shown" }, order);
    }

    [Fact]
    public void Closing_a_hosted_form_removes_it_from_the_panel ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);
        form.Show ();

        var closed = 0;
        form.FormClosed += (_, _) => closed++;

        form.Close ();

        // The dashboard idiom closes the outgoing page before adding the next one; if the frame stayed
        // parented the pages would stack up.
        Assert.False (panel.Controls.Contains (form));
        Assert.Equal (0, panel.Controls.Count);
        Assert.Equal (1, closed);
    }

    [Fact]
    public void Closing_a_hosted_form_can_be_cancelled ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);
        form.Show ();

        form.FormClosing += (_, e) => e.Cancel = true;
        form.Close ();

        Assert.True (panel.Controls.Contains (form));
    }

    [Fact]
    public void Removing_a_hosted_form_detaches_it ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        Assert.True (panel.Controls.Remove (form));
        Assert.False (panel.Controls.Contains (form));
        Assert.Null (form.Parent);
    }

    [Fact]
    public void Clearing_the_collection_detaches_a_hosted_form ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        panel.Controls.Clear ();

        Assert.Null (form.Parent);
    }

    [Fact]
    public void Adding_a_hosted_form_to_a_second_panel_moves_it ()
    {
        var first = BodyPanel ();
        var second = BodyPanel ();
        var form = HostedForm (first);

        second.Controls.Add (form);

        Assert.False (first.Controls.Contains (form));
        Assert.True (second.Controls.Contains (form));
        Assert.Same (second, form.Parent);
    }

    [Fact]
    public void A_hosted_form_is_never_chosen_as_a_modal_owner ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);
        form.Show ();

        // Showing a hosted form registers it in Application.OpenForms, as WinForms does. It owns no OS
        // window though, so ShowDialog picking it as owner disables a backend that was never realized
        // and then blocks forever -- which is exactly how this surfaced: an unrelated MessageBox test
        // 3000 tests downstream hung the whole run. Asserted through FindModalOwner rather than
        // ShowDialog so a regression fails here instead of hanging.
        Assert.Contains (form, Application.OpenForms);
        Assert.Null (Form.FindModalOwner (new Form ()));
    }

    [Fact]
    public void Hosted_form_does_not_draw_its_own_title_bar ()
    {
        var panel = BodyPanel ();
        var form = HostedForm (panel);

        // The host owns whatever chrome there is, and for a panel-hosted form that is none.
        Assert.False (form.TitleBar.Visible);
    }
}
