using System;
using System.Collections.Generic;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The keyboard pre-processing chain -- ProcessCmdKey -> IsInputKey -> ProcessDialogKey -- and the
// behaviours that hang off it.
//
// Until this landed, every one of those virtuals was `=> false` with no caller anywhere in the
// assembly: `override ProcessCmdKey` on a ported Form compiled and silently never ran, and the two
// behaviours the chain is supposed to gate (AcceptButton/CancelButton, Tab traversal) were hard-coded
// ahead of the focused control instead. See docs/behaviour-gap-plan.md RC-1, and findings FRM-05,
// FRM-08, SVC-02, CTL-28, EVT-06, EVT-07, TXT-09, TXT-10.
//
// The ordering is the contract, so most of these assert a sequence rather than a single outcome.
public class KeyboardChainTests
{
    // Records and matches on the key CODE, deliberately dropping modifiers. Control.ModifierKeys is
    // static mutable state that every KeyEventArgs and MouseEventArgs constructor writes
    // (Control.Compat.cs:20), so a test running in parallel can leave Control or Shift set and any
    // assertion made against the full keyData becomes order-dependent. That is the same class of
    // global-state flakiness BACKLOG.md records for Application.OpenForms.
    private sealed class ChainForm : Form
    {
        public List<string> Calls { get; } = [];
        public Keys? CmdKeyToSwallow { get; set; }

        protected override bool ProcessCmdKey (ref Message msg, Keys keyData)
        {
            var code = keyData & Keys.KeyCode;
            Calls.Add ($"ProcessCmdKey({code})");

            if (CmdKeyToSwallow == code)
                return true;

            return base.ProcessCmdKey (ref msg, keyData);
        }

        protected override bool ProcessDialogKey (Keys keyData)
        {
            Calls.Add ($"ProcessDialogKey({keyData & Keys.KeyCode})");
            return base.ProcessDialogKey (keyData);
        }
    }

    private sealed class ClaimingControl : Control
    {
        public Keys Claim { get; set; } = Keys.None;

        protected override bool IsInputKey (Keys keyData)
            => (keyData & Keys.KeyCode) == Claim || base.IsInputKey (keyData);
    }

    private static Form ShowForm (Form form)
    {
        HeadlessRenderer.Use ();
        form.Size = new System.Drawing.Size (300, 200);
        form.Show ();
        return form;
    }

    [Fact]
    public void An_override_of_ProcessCmdKey_actually_runs ()
    {
        using var form = (ChainForm) ShowForm (new ChainForm ());

        HeadlessRenderer.KeyDown (form, Keys.F5);

        Assert.Contains ($"ProcessCmdKey({Keys.F5})", form.Calls);
    }

    [Fact]
    public void ProcessCmdKey_runs_before_ProcessDialogKey ()
    {
        using var form = (ChainForm) ShowForm (new ChainForm ());

        HeadlessRenderer.KeyDown (form, Keys.Escape);

        Assert.Equal (
            [$"ProcessCmdKey({Keys.Escape})", $"ProcessDialogKey({Keys.Escape})"],
            form.Calls);
    }

    [Fact]
    public void A_swallowed_command_key_suppresses_KeyDown_entirely ()
    {
        using var form = (ChainForm) ShowForm (new ChainForm ());
        form.CmdKeyToSwallow = Keys.F5;

        using var recorder = EventRecorder.For (form, "KeyDown");
        var handled = HeadlessRenderer.KeyDown (form, Keys.F5);

        Assert.True (handled);
        Assert.Empty (recorder.Entries);
        Assert.DoesNotContain ($"ProcessDialogKey({Keys.F5})", form.Calls);
    }

    [Fact]
    public void A_control_that_claims_a_key_stops_dialog_processing ()
    {
        using var form = (ChainForm) ShowForm (new ChainForm ());
        var control = new ClaimingControl { Claim = Keys.Escape, Size = new System.Drawing.Size (50, 20) };
        form.Controls.Add (control);
        control.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Escape);

        // ProcessCmdKey still ran -- a shortcut outranks the focused control -- but the control's
        // claim stopped the key before it could become a dialog key.
        Assert.Contains ($"ProcessCmdKey({Keys.Escape})", form.Calls);
        Assert.DoesNotContain ($"ProcessDialogKey({Keys.Escape})", form.Calls);
    }

    // ── AcceptButton / CancelButton ──────────────────────────────────────────────────────────────

    [Fact]
    public void Enter_still_activates_the_AcceptButton ()
    {
        using var form = (Form) ShowForm (new Form ());
        var clicks = 0;
        var accept = new Button { Text = "OK" };
        accept.Click += (_, _) => clicks++;
        form.Controls.Add (accept);
        form.AcceptButton = accept;

        HeadlessRenderer.KeyDown (form, Keys.Return);

        Assert.Equal (1, clicks);
    }

    [Fact]
    public void Escape_still_activates_the_CancelButton ()
    {
        using var form = (Form) ShowForm (new Form ());
        var clicks = 0;
        var cancel = new Button { Text = "Cancel" };
        cancel.Click += (_, _) => clicks++;
        form.Controls.Add (cancel);
        form.CancelButton = cancel;

        HeadlessRenderer.KeyDown (form, Keys.Escape);

        Assert.Equal (1, clicks);
    }

    [Fact]
    public void Enter_in_a_multiline_TextBox_that_accepts_it_does_not_press_the_default_button ()
    {
        // The headline regression this chain exists to fix: AcceptButton used to be handled at the top
        // of HandleKeyDown, so every multiline box on a form with a default button submitted the form
        // instead of adding a line (TXT-09, EVT-07).
        using var form = (Form) ShowForm (new Form ());
        var clicks = 0;
        var accept = new Button { Text = "OK" };
        accept.Click += (_, _) => clicks++;
        form.Controls.Add (accept);
        form.AcceptButton = accept;

        var text = new TextBox { Multiline = true, AcceptsReturn = true, Size = new System.Drawing.Size (100, 60) };
        form.Controls.Add (text);
        text.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Return);

        Assert.Equal (0, clicks);
    }

    [Fact]
    public void Enter_in_a_multiline_TextBox_that_declines_it_still_presses_the_default_button ()
    {
        using var form = (Form) ShowForm (new Form ());
        var clicks = 0;
        var accept = new Button { Text = "OK" };
        accept.Click += (_, _) => clicks++;
        form.Controls.Add (accept);
        form.AcceptButton = accept;

        var text = new TextBox { Multiline = true, AcceptsReturn = false, Size = new System.Drawing.Size (100, 60) };
        form.Controls.Add (text);
        text.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Return);

        Assert.Equal (1, clicks);
    }

    [Theory]
    [InlineData (Keys.Control)]
    [InlineData (Keys.Alt)]
    public void A_modified_Enter_does_not_press_the_default_button (Keys modifier)
    {
        // Upstream's Form.ProcessDialogKey guards on (Alt | Control) == None: Ctrl+Enter is not the
        // accept gesture, and treating it as one fires the default button on a shortcut meant for
        // something else.
        using var form = (Form) ShowForm (new Form ());
        var clicks = 0;
        var accept = new Button { Text = "OK" };
        accept.Click += (_, _) => clicks++;
        form.Controls.Add (accept);
        form.AcceptButton = accept;

        HeadlessRenderer.KeyDown (form, Keys.Return | modifier);

        Assert.Equal (0, clicks);
    }

    // ── Tab traversal, now a dialog key ──────────────────────────────────────────────────────────

    [Fact]
    public void Tab_does_not_walk_onto_the_caption_buttons ()
    {
        // Regression, and a platform trap: CI runs the tests on Windows only, while this machine is
        // macOS -- which is the one platform that uses SYSTEM decorations (Form's constructor sets
        // UseSystemDecorations there). Everywhere else the library draws its own title bar whose
        // Minimise/Maximise/Close are real Buttons, and Button is a tab stop by default, so Tab off the
        // last control walked onto chrome. They are implicit children, so nothing in the form's own
        // Controls reported focus and it looked as though focus had vanished.
        //
        // Forcing custom chrome here makes the test mean the same thing on every platform instead of
        // passing vacuously on the one the author happens to be using.
        HeadlessRenderer.Use ();
        using var form = new Form { Size = new System.Drawing.Size (300, 200) };
        form.UseSystemDecorations = false;
        form.Show ();

        var first = new TextBox { TabIndex = 0, Size = new System.Drawing.Size (80, 20) };
        var second = new TextBox { TabIndex = 1, Size = new System.Drawing.Size (80, 20) };
        form.Controls.Add (first);
        form.Controls.Add (second);
        first.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Tab);

        Assert.True (second.Focused, "Tab must reach the next control, not the window's caption buttons");
    }

    [Fact]
    public void Tab_still_moves_focus_between_controls ()
    {
        using var form = (Form) ShowForm (new Form ());
        var first = new TextBox { TabIndex = 0, Size = new System.Drawing.Size (80, 20) };
        var second = new TextBox { TabIndex = 1, Size = new System.Drawing.Size (80, 20) };
        form.Controls.Add (first);
        form.Controls.Add (second);
        first.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Tab);

        Assert.True (second.Focused, "Tab should have moved focus to the second control.");
    }

    [Fact]
    public void Tab_in_a_multiline_TextBox_that_accepts_tabs_does_not_move_focus ()
    {
        using var form = (Form) ShowForm (new Form ());
        var first = new TextBox {
            TabIndex = 0, Multiline = true, AcceptsTab = true,
            Size = new System.Drawing.Size (100, 60),
        };
        var second = new TextBox { TabIndex = 1, Size = new System.Drawing.Size (80, 20) };
        form.Controls.Add (first);
        form.Controls.Add (second);
        first.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Tab);

        Assert.True (first.Focused, "AcceptsTab should have kept focus in the text box.");
        Assert.False (second.Focused);
    }

    [Fact]
    public void Ctrl_Tab_is_claimed_by_nobody_and_moves_no_focus ()
    {
        // Worth spelling out, because the intuition is wrong. Ctrl+Tab looks like "traversal even out
        // of a box that accepts tabs", and it is not: TextBoxBase.IsInputKey declines Tab while Control
        // is held, but ContainerControl.ProcessDialogKey guards its Tab case on
        // (Alt | Control) == None too -- so nobody claims the key and focus stays put. Ctrl+Tab as
        // traversal is an MDI/TabControl behaviour, not a general one.
        using var form = (Form) ShowForm (new Form ());
        var first = new TextBox {
            TabIndex = 0, Multiline = true, AcceptsTab = true,
            Size = new System.Drawing.Size (100, 60),
        };
        var second = new TextBox { TabIndex = 1, Size = new System.Drawing.Size (80, 20) };
        form.Controls.Add (first);
        form.Controls.Add (second);
        first.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Tab | Keys.Control);

        Assert.True (first.Focused, "Ctrl+Tab should not move focus.");
        Assert.False (second.Focused);
    }

    // ── PreProcessMessage, the public entry point ────────────────────────────────────────────────

    [Fact]
    public void PreProcessMessage_runs_the_chain_rather_than_returning_false ()
    {
        using var form = (ChainForm) ShowForm (new ChainForm ());
        form.CmdKeyToSwallow = Keys.F5;

        var control = new ClaimingControl { Size = new System.Drawing.Size (50, 20) };
        form.Controls.Add (control);

        var msg = new Message { Msg = WindowMessages.WM_KEYDOWN, WParam = (IntPtr) (int) Keys.F5 };

        Assert.True (control.PreProcessMessage (ref msg));
        Assert.Contains ($"ProcessCmdKey({Keys.F5})", form.Calls);
    }
}
