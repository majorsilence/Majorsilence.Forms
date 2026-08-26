using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The focus-change sequence: Leave up the leaving chain, validation between the two controls, Enter
// down the entering chain, then the focus notifications.
//
// Before this, Control.Select () raised the ENTERING control's Enter/GotFocus and only then told the
// adapter, whose setter deselected the leaving one -- so a mouse click produced B.Enter, B.GotFocus,
// A.Leave, A.LostFocus while Tab produced the opposite order, in the same application. Validation ran
// inside OnLostFocus, after focus had already moved, so e.Cancel had nothing left to prevent. See
// findings EVT-02 (P0), CTL-01, CTL-08 and EVT-05.
public class FocusSequenceTests
{
    private static Form ShowForm ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (400, 300) };
        form.Show ();
        return form;
    }

    private static TextBox AddBox (Control parent, int tabIndex)
    {
        var box = NewBox (tabIndex);
        parent.Controls.Add (box);
        return box;
    }

    // A Form is not a Control here (Form : WindowBase : Component), so its Controls collection is
    // reached through a separate overload rather than the shared one.
    private static TextBox AddBox (Form parent, int tabIndex)
    {
        var box = NewBox (tabIndex);
        parent.Controls.Add (box);
        return box;
    }

    // Laid out down the form rather than stacked at (0, 0): the mouse-driven tests click a box by its
    // own coordinates, and overlapping boxes make that click land on whichever happens to be on top.
    private static TextBox NewBox (int tabIndex)
        => new () {
            TabIndex = tabIndex,
            Size = new Size (80, 20),
            Location = new Point (10, 10 + (tabIndex * 40)),
        };

    [Fact]
    public void The_leaving_control_is_heard_before_the_entering_one ()
    {
        using var form = ShowForm ();
        var a = AddBox (form, 0);
        var b = AddBox (form, 1);
        a.Select ();

        using var recorder = EventRecorder.For (a, "Leave", "Validating", "Validated", "LostFocus");
        recorder.Also (b, "b", "Enter", "GotFocus");

        b.Select ();

        recorder.AssertSequence (
            "Leave", "Validating", "Validated", "LostFocus", "b.Enter", "b.GotFocus");
    }

    [Fact]
    public void A_mouse_driven_focus_change_uses_the_same_order_as_Tab ()
    {
        // The specific inconsistency EVT-02 describes: two paths into focus, two different orders.
        using var form = ShowForm ();
        var a = AddBox (form, 0);
        var b = AddBox (form, 1);

        a.Select ();
        using var byTab = EventRecorder.For (a, "Leave");
        byTab.Also (b, "b", "Enter");
        b.Select ();
        var tabOrder = byTab.Entries;

        a.Select ();
        using var byMouse = EventRecorder.For (a, "Leave");
        byMouse.Also (b, "b", "Enter");
        HeadlessRenderer.Click (form, b.Left + 5, b.Top + 5);

        Assert.Equal (tabOrder, byMouse.Entries);
        Assert.Equal (["Leave", "b.Enter"], byMouse.Entries);
    }

    // ── Validation ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancelling_Validating_keeps_focus_on_the_invalid_control ()
    {
        // The canonical WinForms idiom, and the reason validation had to move out of OnLostFocus.
        using var form = ShowForm ();
        var a = AddBox (form, 0);
        var b = AddBox (form, 1);
        a.Select ();

        a.Validating += (_, e) => e.Cancel = true;

        b.Select ();

        Assert.True (a.Focused, "focus should have stayed on the control that failed validation");
        Assert.False (b.Focused);
    }

    [Fact]
    public void Cancelling_Validating_suppresses_Validated_and_the_entering_controls_Enter ()
    {
        using var form = ShowForm ();
        var a = AddBox (form, 0);
        var b = AddBox (form, 1);
        a.Select ();

        a.Validating += (_, e) => e.Cancel = true;

        using var recorder = EventRecorder.For (a, "Validating", "Validated");
        recorder.Also (b, "b", "Enter", "GotFocus");

        b.Select ();

        recorder.AssertSequence ("Validating");
    }

    [Fact]
    public void An_entering_control_with_CausesValidation_false_skips_validation_entirely ()
    {
        // A Cancel/Help button is the standard case: you must be able to leave an invalid field to
        // press it.
        using var form = ShowForm ();
        var a = AddBox (form, 0);
        var cancel = new Button { Text = "Cancel", CausesValidation = false, Size = new Size (80, 24) };
        form.Controls.Add (cancel);
        a.Select ();

        var validated = 0;
        a.Validating += (_, e) => { validated++; e.Cancel = true; };

        cancel.Select ();

        Assert.Equal (0, validated);
        Assert.True (cancel.Focused, "a control that does not cause validation must still take focus");
    }

    [Fact]
    public void AutoValidate_Disable_turns_the_validation_cycle_off ()
    {
        using var form = ShowForm ();
        form.AutoValidate = AutoValidate.Disable;
        var a = AddBox (form, 0);
        var b = AddBox (form, 1);
        a.Select ();

        var ran = 0;
        a.Validating += (_, e) => { ran++; e.Cancel = true; };

        b.Select ();

        Assert.Equal (0, ran);
        Assert.True (b.Focused);
    }

    [Fact]
    public void AutoValidate_EnableAllowFocusChange_still_validates_but_lets_focus_go ()
    {
        using var form = ShowForm ();
        form.AutoValidate = AutoValidate.EnableAllowFocusChange;
        var a = AddBox (form, 0);
        var b = AddBox (form, 1);
        a.Select ();

        var ran = 0;
        a.Validating += (_, e) => { ran++; e.Cancel = true; };

        b.Select ();

        Assert.Equal (1, ran);
        Assert.True (b.Focused, "AllowFocusChange means a cancel is reported but not obeyed");
    }

    // ── The ancestor walk ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_container_hears_focus_leaving_it ()
    {
        // EVT-05: only the leaf control used to hear anything, so a UserControl-based editor panel got
        // no notification when focus left it.
        using var form = ShowForm ();
        var panel = new Panel { Size = new Size (200, 100) };
        form.Controls.Add (panel);
        var inside = AddBox (panel, 0);
        var outside = AddBox (form, 1);
        inside.Select ();

        using var recorder = EventRecorder.For (panel, "Leave");

        outside.Select ();

        Assert.Equal (1, recorder.Count ("Leave"));
    }

    [Fact]
    public void A_container_hears_focus_entering_it_outermost_first ()
    {
        using var form = ShowForm ();
        var panel = new Panel { Size = new Size (200, 100) };
        form.Controls.Add (panel);
        var inside = AddBox (panel, 1);
        var outside = AddBox (form, 0);
        outside.Select ();

        using var recorder = EventRecorder.For (panel, "Enter");
        recorder.Also (inside, "box", "Enter");

        inside.Select ();

        recorder.AssertSequence ("Enter", "box.Enter");
    }

    [Fact]
    public void Moving_between_siblings_inside_one_container_does_not_leave_the_container ()
    {
        // The common ancestor is the panel, so the walk stops below it.
        using var form = ShowForm ();
        var panel = new Panel { Size = new Size (200, 100) };
        form.Controls.Add (panel);
        var a = AddBox (panel, 0);
        var b = AddBox (panel, 1);
        a.Select ();

        using var recorder = EventRecorder.For (panel, "Leave", "Enter");

        b.Select ();

        Assert.Empty (recorder.Entries);
    }

    // ── ValidateChildren / ActiveControl ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateChildren_reports_a_cancelling_child ()
    {
        using var form = ShowForm ();
        var uc = new UserControl { Size = new Size (200, 100) };
        form.Controls.Add (uc);
        var box = AddBox (uc, 0);
        box.Validating += (_, e) => e.Cancel = true;

        Assert.False (uc.ValidateChildren (), "the parameterless overload must not be a `=> true` stub");
    }

    [Fact]
    public void ValidateChildren_returns_true_when_nothing_objects ()
    {
        using var form = ShowForm ();
        var uc = new UserControl { Size = new Size (200, 100) };
        form.Controls.Add (uc);
        AddBox (uc, 0);

        Assert.True (uc.ValidateChildren ());
    }

    [Fact]
    public void ActiveControl_reports_and_moves_focus ()
    {
        using var form = ShowForm ();
        var uc = new UserControl { Size = new Size (200, 100) };
        form.Controls.Add (uc);
        var box = AddBox (uc, 0);

        uc.ActiveControl = box;

        Assert.True (box.Focused, "setting ActiveControl is the standard way to set initial focus");
        Assert.Same (box, uc.ActiveControl);
    }

    [Fact]
    public void GetContainerControl_finds_the_nearest_container ()
    {
        using var form = ShowForm ();
        var uc = new UserControl { Size = new Size (200, 100) };
        form.Controls.Add (uc);
        var box = AddBox (uc, 0);

        // `(GetContainerControl() as ContainerControl).Validate()` is a common idiom in third-party
        // control libraries; it used to NullReference because this always returned null.
        Assert.Same (uc, box.GetContainerControl ());
    }
}
