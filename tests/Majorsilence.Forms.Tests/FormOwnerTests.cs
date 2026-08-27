using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The owner graph, which did not exist: Owner was a bare auto-property so OwnedForms stayed empty,
// Show(owner)/ShowDialog(owner) discarded their argument outright, a modal dialog disabled only its
// own owner, and CloseReason was hard-coded UserClosing everywhere. Findings FRM-14, FRM-15, FRM-30.
[Collection ("Headless")]
public class FormOwnerTests
{
    private static Form NewForm ()
    {
        HeadlessRenderer.Use ();
        return new Form { Size = new Size (240, 160) };
    }

    // ── Owner / OwnedForms ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Setting_Owner_adds_the_form_to_the_owners_OwnedForms ()
    {
        using var owner = NewForm ();
        using var child = NewForm ();

        child.Owner = owner;

        Assert.Same (owner, child.Owner);
        Assert.Contains (child, owner.OwnedForms);
    }

    [Fact]
    public void Re_owning_a_form_removes_it_from_the_previous_owner ()
    {
        using var first = NewForm ();
        using var second = NewForm ();
        using var child = NewForm ();

        child.Owner = first;
        child.Owner = second;

        Assert.DoesNotContain (child, first.OwnedForms);
        Assert.Contains (child, second.OwnedForms);
    }

    [Fact]
    public void AddOwnedForm_and_RemoveOwnedForm_keep_both_sides_in_step ()
    {
        using var owner = NewForm ();
        using var child = NewForm ();

        owner.AddOwnedForm (child);
        Assert.Same (owner, child.Owner);
        Assert.Single (owner.OwnedForms);

        owner.RemoveOwnedForm (child);
        Assert.Null (child.Owner);
        Assert.Empty (owner.OwnedForms);
    }

    [Fact]
    public void A_form_cannot_own_itself_or_form_a_cycle ()
    {
        using var a = NewForm ();
        using var b = NewForm ();

        Assert.Throws<ArgumentException> (() => a.Owner = a);

        b.Owner = a;
        Assert.Throws<ArgumentException> (() => a.Owner = b);
    }

    [Fact]
    public void Show_with_an_owner_records_the_ownership ()
    {
        using var owner = NewForm ();
        owner.Show ();
        using var tool = NewForm ();

        tool.Show (owner);

        Assert.Same (owner, tool.Owner);
        Assert.Contains (tool, owner.OwnedForms);

        tool.Close ();
        owner.Close ();
    }

    [Fact]
    public void An_owner_takes_its_owned_forms_with_it_when_it_closes ()
    {
        var owner = NewForm ();
        owner.Show ();
        var tool = NewForm ();
        tool.Show (owner);

        var reason = CloseReason.None;
        tool.FormClosed += (_, e) => reason = e.CloseReason;

        owner.Close ();

        Assert.False (tool.Visible, "an owned tool window must not outlive its owner");
        Assert.Equal (CloseReason.FormOwnerClosing, reason);
    }

    [Fact]
    public void A_control_as_owner_resolves_to_the_form_hosting_it ()
    {
        using var owner = NewForm ();
        var button = new Button { Text = "Open", Size = new Size (60, 24) };
        owner.Controls.Add (button);
        owner.Show ();

        using var tool = NewForm ();
        tool.Show (button);

        Assert.Same (owner, tool.Owner);

        tool.Close ();
        owner.Close ();
    }

    // ── Modal disables every window ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_modal_dialog_disables_every_other_window_not_just_its_owner ()
    {
        // With two forms open, a dialog raised from A used to leave B fully interactive -- B could
        // open a second dialog, close A, or exit the application from under the modal loop.
        using var a = NewForm ();
        using var b = NewForm ();
        a.Show ();
        b.Show ();

        using var dialog = NewForm ();

        bool? bEnabledDuring = null;

        Majorsilence.Forms.Backends.Platform.Backend.Post (() => {
            bEnabledDuring = b.Backend.Enabled;
            dialog.DialogResult = DialogResult.OK;
        });

        RunModalWithTimeout (() => dialog.ShowDialog (a));

        Assert.False (bEnabledDuring, "every top-level window is disabled for the duration of a modal dialog");
        Assert.True (b.Backend.Enabled, "and re-enabled afterwards");
        Assert.True (a.Backend.Enabled);

        a.Close ();
        b.Close ();
    }

    [Fact]
    public void ShowDialog_with_an_owner_records_the_ownership ()
    {
        using var owner = NewForm ();
        owner.Show ();
        using var dialog = NewForm ();

        Majorsilence.Forms.Backends.Platform.Backend.Post (() => dialog.DialogResult = DialogResult.OK);
        RunModalWithTimeout (() => dialog.ShowDialog (owner));

        Assert.Same (owner, dialog.Owner);

        owner.Close ();
    }

    // ── CloseReason ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_ordinary_Close_reports_UserClosing ()
    {
        var form = NewForm ();
        form.Show ();

        var reason = CloseReason.None;
        form.FormClosing += (_, e) => reason = e.CloseReason;

        form.Close ();

        Assert.Equal (CloseReason.UserClosing, reason);
    }

    [Fact]
    public void Application_Exit_reports_ApplicationExitCall ()
    {
        // The reason the "minimise to tray unless we are really exiting" pattern needs this. Cancelled
        // so the suite's other forms survive -- see ApplicationLifecycleTests for why.
        using var form = NewForm ();
        form.Show ();

        var reason = CloseReason.None;
        form.FormClosing += (_, e) => { reason = e.CloseReason; e.Cancel = true; };

        Application.Exit ();

        Assert.Equal (CloseReason.ApplicationExitCall, reason);
        Assert.True (form.Visible);

        form.Close ();
    }

    private static DialogResult RunModalWithTimeout (Func<DialogResult> show)
    {
        var result = DialogResult.None;
        Exception? failure = null;

        var thread = new System.Threading.Thread (() => {
            try { result = show (); } catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };

        thread.Start ();
        Assert.True (thread.Join (TimeSpan.FromSeconds (10)), "ShowDialog never returned");

        if (failure is not null)
            throw failure;

        return result;
    }
}
