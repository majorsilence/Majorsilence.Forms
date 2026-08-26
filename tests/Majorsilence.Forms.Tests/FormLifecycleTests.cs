using System;
using System.Linq;
using System.Threading;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The window lifecycle: a form is correct every time it is used, not just the first.
//
// _loadFired, _formClosedFired, `shown`, `visible` and dialog_result were set once and never cleared,
// which modelled a form as shown-once. WinForms models the HANDLE as the unit of lifetime and destroys
// it on every close, so Load, Shown and FormClosed fire on each cycle. See findings FRM-02 (P0),
// FRM-03, FRM-04, FRM-07, FRM-12, FRM-13, FRM-16, and FRM-01/SVC-01 for the ownerless dialog.
public class FormLifecycleTests
{
    private static Form ShowForm ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new System.Drawing.Size (300, 200) };
        form.Show ();
        return form;
    }

    // Runs a modal show on a background thread so a regression fails rather than hanging the suite.
    private static DialogResult RunModalWithTimeout (Func<DialogResult> show)
    {
        var result = DialogResult.None;
        Exception? failure = null;

        var thread = new Thread (() => {
            try { result = show (); } catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };

        thread.Start ();

        Assert.True (thread.Join (TimeSpan.FromSeconds (10)),
            "ShowDialog never returned: nothing ended the modal loop");

        if (failure is not null)
            throw failure;

        return result;
    }

    // ── Reuse ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_and_FormClosed_fire_on_every_show_close_cycle ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new System.Drawing.Size (300, 200) };

        var loads = 0;
        var closes = 0;
        form.Load += (_, _) => loads++;
        form.FormClosed += (_, _) => closes++;

        form.Show ();
        form.Close ();
        form.Show ();
        form.Close ();

        Assert.Equal (2, loads);
        Assert.Equal (2, closes);
    }

    [Fact]
    public void Visible_is_false_after_Close ()
    {
        using var form = ShowForm ();

        Assert.True (form.Visible);

        form.Close ();

        Assert.False (form.Visible);
        Assert.DoesNotContain (form, Application.OpenForms.Cast<Form> ());
    }

    [Fact]
    public void A_closed_form_can_be_shown_again_and_rejoins_OpenForms ()
    {
        using var form = ShowForm ();
        form.Close ();

        form.Show ();

        Assert.True (form.Visible);
        Assert.Contains (form, Application.OpenForms.Cast<Form> ());

        form.Close ();
    }

    [Fact]
    public void IsHandleCreated_is_true_during_Load ()
    {
        // `if (!IsHandleCreated) return;` is a common guard in a refresh routine shared with a timer.
        // The handle used to be marked created after Load, so that guard skipped the whole handler.
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new System.Drawing.Size (300, 200) };

        bool? handleDuringLoad = null;
        form.Load += (_, _) => handleDuringLoad = form.IsHandleCreated;

        form.Show ();

        Assert.True (handleDuringLoad, "the handle must exist by the time Load is raised");
        form.Close ();
    }

    [Fact]
    public void The_show_sequence_is_HandleCreated_then_Load_then_Shown ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new System.Drawing.Size (300, 200) };
        using var recorder = EventRecorder.For (form, "Load", "Shown");

        var order = new System.Collections.Generic.List<string> ();
        form.HandleCreated += (_, _) => order.Add ("HandleCreated");
        form.Load += (_, _) => order.Add ("Load");
        form.Shown += (_, _) => order.Add ("Shown");

        form.Show ();

        Assert.Equal (["HandleCreated", "Load", "Shown"], order);
        form.Close ();
    }

    // ── Dispose on close ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Closing_a_non_modal_form_disposes_it ()
    {
        // Upstream's WmClose disposes a non-modal form, which is what runs the designer's
        // Dispose(bool) override and with it `components` -- Timers, BindingSources, ToolTips.
        var form = ShowForm ();
        var disposed = 0;
        form.Disposed += (_, _) => disposed++;

        form.Close ();

        Assert.True (form.IsDisposed);
        Assert.Equal (1, disposed);
    }

    [Fact]
    public void Closing_raises_Closed_and_FormClosed_exactly_once ()
    {
        // Disposing from the close path re-enters the close sequence; without a guard a single close
        // raised everything twice.
        var form = ShowForm ();
        var closed = 0;
        var formClosed = 0;
        form.Closed += (_, _) => closed++;
        form.FormClosed += (_, _) => formClosed++;

        form.Close ();

        Assert.Equal (1, closed);
        Assert.Equal (1, formClosed);
    }

    // ── Modal ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ShowDialog_is_modal_with_no_owner_and_waits_for_a_result ()
    {
        // The universal "login before Application.Run" shape:
        //     if (new LoginForm ().ShowDialog () != DialogResult.OK) return;
        // used to see OK instantly with nothing filled in.
        HeadlessRenderer.Use ();
        using var dialog = new Form { Size = new System.Drawing.Size (200, 120) };

        var opened = false;
        dialog.Shown += (_, _) => opened = true;

        Platform.Backend.Post (() => dialog.DialogResult = DialogResult.Yes);

        var result = RunModalWithTimeout (dialog.ShowDialog);

        Assert.True (opened, "the dialog must actually be shown");
        Assert.Equal (DialogResult.Yes, result);
    }

    [Fact]
    public void A_reused_dialog_does_not_return_its_previous_result ()
    {
        // FRM-02: a dialog kept as a field -- Find, Options -- handed back last time's answer without
        // appearing at all.
        HeadlessRenderer.Use ();
        using var dialog = new Form { Size = new System.Drawing.Size (200, 120) };

        Platform.Backend.Post (() => dialog.DialogResult = DialogResult.Yes);
        Assert.Equal (DialogResult.Yes, RunModalWithTimeout (dialog.ShowDialog));

        var shownAgain = false;
        dialog.Shown += (_, _) => shownAgain = true;

        Platform.Backend.Post (() => dialog.DialogResult = DialogResult.No);
        var second = RunModalWithTimeout (dialog.ShowDialog);

        Assert.True (shownAgain, "the second ShowDialog must actually show the form again");
        Assert.Equal (DialogResult.No, second);
    }

    [Fact]
    public void Modal_is_true_only_while_the_dialog_is_running ()
    {
        HeadlessRenderer.Use ();
        using var dialog = new Form { Size = new System.Drawing.Size (200, 120) };

        bool? modalDuringLoad = null;
        dialog.Load += (_, _) => modalDuringLoad = dialog.Modal;

        Platform.Backend.Post (() => dialog.DialogResult = DialogResult.OK);
        RunModalWithTimeout (dialog.ShowDialog);

        Assert.True (modalDuringLoad, "`if (Modal) DialogResult = OK; else Close ();` depends on this");
        Assert.False (dialog.Modal);
    }

    [Fact]
    public void Hiding_a_modal_dialog_ends_it_with_Cancel ()
    {
        // FRM-13: a dialog written as `this.Hide ()` left ShowDialog pumping forever with the owner
        // disabled, which presents as a hung application.
        HeadlessRenderer.Use ();
        using var dialog = new Form { Size = new System.Drawing.Size (200, 120) };

        Platform.Backend.Post (dialog.Hide);

        Assert.Equal (DialogResult.Cancel, RunModalWithTimeout (dialog.ShowDialog));
    }

    [Fact]
    public void A_modal_dialog_is_not_disposed_by_its_own_close ()
    {
        // The caller still has to read DialogResult, and may show it again.
        HeadlessRenderer.Use ();
        using var dialog = new Form { Size = new System.Drawing.Size (200, 120) };

        Platform.Backend.Post (() => dialog.DialogResult = DialogResult.OK);
        RunModalWithTimeout (dialog.ShowDialog);

        Assert.False (dialog.IsDisposed);
        Assert.Equal (DialogResult.OK, dialog.DialogResult);
    }
}
