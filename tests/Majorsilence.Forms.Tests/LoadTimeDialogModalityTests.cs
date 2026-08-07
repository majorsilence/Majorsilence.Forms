using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Regression: Form.ShowDialog and MessageBox.Show choose their modal owner out of
// Application.OpenForms and fall back to a NON-blocking Show() when it is empty. EnsureShownBookkeeping
// used to register the form only after raising Load, so on the first form of an application every
// dialog opened from a Load handler -- the standard WinForms "prompt for missing configuration on
// startup" shape -- silently flashed up non-modally and the handler ran straight on with nothing
// filled in. Found in a migrated media player: its startup prompt for the mplayer path never blocked,
// so the path stayed empty and the next line dereferenced a null player.
public class LoadTimeDialogModalityTests
{
    [Fact]
    public void Form_is_in_OpenForms_by_the_time_Load_is_raised()
    {
        using var form = new Form();
        var registeredDuringLoad = false;

        form.Load += (_, _) => registeredDuringLoad = Application.OpenForms.Contains(form);

        form.Show();

        Assert.True(registeredDuringLoad,
            "a dialog opened from Load can only be modal if its owner is already an open form");
    }

    [Fact]
    public void A_dialog_opened_from_Load_finds_an_owner_to_be_modal_against()
    {
        using var form = new Form();

        // Application.OpenForms is process-wide, so anything another test still has open would be
        // picked as the owner ahead of this form and fail the assertion for the wrong reason. Ignore
        // whatever was already open and reproduce the owner search over just this test's own form.
        var preexisting = Application.OpenForms.Cast<Form>().ToArray();
        Form? Candidates() => Application.OpenForms.Cast<Form>().Except(preexisting).FirstOrDefault(f => f != form)
            ?? Application.OpenForms.Cast<Form>().Except(preexisting).FirstOrDefault();

        Form? ownerSeenByDialog = null;

        form.Load += (_, _) => ownerSeenByDialog = Candidates();

        form.Show();

        Assert.Same(form, ownerSeenByDialog);
    }

    [Fact]
    public void Shown_still_runs_after_Load()
    {
        using var form = new Form();
        var order = "";

        form.Load += (_, _) => order += "L";
        form.Shown += (_, _) => order += "S";

        form.Show();

        Assert.Equal("LS", order);
    }
}
