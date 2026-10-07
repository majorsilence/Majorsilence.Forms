using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Event wiring and order behaviour-gap findings (docs/behaviour-gap/events.md, issue #344). Each test
// names the finding it pins; the upstream source each behaviour was taken from is cited in the fix.
[Collection ("Headless")]
public sealed class EventsGapTests : IDisposable
{
    private readonly double original_ui_scale = Application.UiScale;

    public EventsGapTests () => HeadlessRenderer.Use ();

    // Application.UiScale is process-wide.
    public void Dispose () => Application.UiScale = original_ui_scale;

    private static Form Shown (params Control[] controls)
    {
        var form = new Form { Width = 400, Height = 300 };
        form.Controls.AddRange (controls);
        form.Show ();
        return form;
    }

    // ── EVT-18: a window's Refresh / Update painted nothing synchronously ──────────────────────────

    [Fact]
    public void EVT18_Form_Refresh_raises_its_controls_Paint_before_it_returns ()
    {
        var label = new Label { Bounds = new Rectangle (10, 10, 100, 20), Text = "0" };
        using var form = Shown (label);
        HeadlessRenderer.CapturePng (form);   // the first frame: nothing dirty is left
        var paints = 0;
        label.Paint += (_, _) => paints++;

        form.Refresh ();

        Assert.Equal (1, paints);
    }

    [Fact]
    public void EVT18_Form_Update_paints_an_invalidated_control_at_once ()
    {
        var label = new Label { Bounds = new Rectangle (10, 10, 100, 20), Text = "0" };
        using var form = Shown (label);
        HeadlessRenderer.CapturePng (form);
        var paints = 0;
        label.Paint += (_, _) => paints++;

        label.Text = "1";
        form.Update ();

        Assert.Equal (1, paints);
    }

    [Fact]
    public void EVT18_Refresh_on_a_container_repaints_its_children ()
    {
        var child = new Label { Bounds = new Rectangle (5, 5, 60, 20), Text = "child" };
        var panel = new Panel { Bounds = new Rectangle (10, 10, 200, 100) };
        panel.Controls.Add (child);
        using var form = Shown (panel);
        HeadlessRenderer.CapturePng (form);
        var paints = 0;
        child.Paint += (_, _) => paints++;

        // Upstream's Refresh is Invalidate (true): every child repaints, not just the container.
        panel.Refresh ();

        Assert.Equal (1, paints);
    }

    // ── EVT-08: PreviewKeyDown's IsInputKey was ignored, and the event came after the dialog keys ──

    [Fact]
    public void EVT08_IsInputKey_from_PreviewKeyDown_keeps_Tab_in_the_control ()
    {
        var first = new Button { Bounds = new Rectangle (10, 10, 80, 25), TabIndex = 0 };
        var second = new Button { Bounds = new Rectangle (10, 50, 80, 25), TabIndex = 1 };
        using var form = Shown (first, second);
        first.Focus ();
        var keys = new List<Keys> ();
        first.PreviewKeyDown += (_, e) => e.IsInputKey = e.KeyCode == Keys.Tab;
        first.KeyDown += (_, e) => keys.Add (e.KeyCode);

        HeadlessRenderer.KeyDown (form, Keys.Tab);

        Assert.True (first.Focused);
        Assert.Equal (new[] { Keys.Tab }, keys);
    }

    [Fact]
    public void EVT08_without_IsInputKey_Tab_still_moves_focus_and_is_previewed ()
    {
        var first = new Button { Bounds = new Rectangle (10, 10, 80, 25), TabIndex = 0 };
        var second = new Button { Bounds = new Rectangle (10, 50, 80, 25), TabIndex = 1 };
        using var form = Shown (first, second);
        first.Focus ();
        var previewed = new List<Keys> ();
        first.PreviewKeyDown += (_, e) => previewed.Add (e.KeyCode);

        HeadlessRenderer.KeyDown (form, Keys.Tab);

        // Previewed even though the key went on to be a dialog key -- it used to be raised only
        // alongside KeyDown, which a dialog key never reaches.
        Assert.Equal (new[] { Keys.Tab }, previewed);
        Assert.True (second.Focused);
    }

    [Fact]
    public void EVT08_KeyDown_is_preceded_by_PreviewKeyDown ()
    {
        var box = new TextBox { Bounds = new Rectangle (10, 10, 100, 25) };
        using var form = Shown (box);
        box.Focus ();
        var order = new List<string> ();
        box.PreviewKeyDown += (_, e) => order.Add ("PreviewKeyDown:" + e.KeyCode);
        box.KeyDown += (_, e) => order.Add ("KeyDown:" + e.KeyCode);

        HeadlessRenderer.KeyDown (form, Keys.A);

        Assert.Equal (new[] { "PreviewKeyDown:A", "KeyDown:A" }, order);
    }

    // ── EVT-20: overriding OnPaint without base did not suppress the Paint handlers ────────────────

    private sealed class SilentControl : Control
    {
        protected override void OnPaint (PaintEventArgs e) { }
    }

    private sealed class SilentForm : Form
    {
        // protected internal only because this assembly sees the library's internals.
        protected internal override void OnPaint (PaintEventArgs e) { }
    }

    [Fact]
    public void EVT20_an_OnPaint_override_that_skips_base_suppresses_Paint ()
    {
        var silent = new SilentControl { Bounds = new Rectangle (10, 10, 50, 50) };
        var plain = new Control { Bounds = new Rectangle (70, 10, 50, 50) };
        using var form = Shown (silent, plain);
        var silent_paints = 0;
        var plain_paints = 0;
        silent.Paint += (_, _) => silent_paints++;
        plain.Paint += (_, _) => plain_paints++;

        HeadlessRenderer.CapturePng (form);

        Assert.Equal (0, silent_paints);
        Assert.Equal (1, plain_paints);   // the control that does call base is unaffected
    }

    [Fact]
    public void EVT20_a_Form_whose_OnPaint_skips_base_raises_no_Paint ()
    {
        using var form = new SilentForm { Width = 200, Height = 150 };
        form.Show ();
        var paints = 0;
        form.Paint += (_, _) => paints++;

        HeadlessRenderer.CapturePng (form);

        Assert.Equal (0, paints);
    }

    [Fact]
    public void EVT20_LinkLabel_still_raises_Paint ()
    {
        // LinkLabel does not call base (Label's OnPaint would draw the text twice), so it asks for the
        // handlers itself, as upstream's does with RaisePaintEvent.
        var link = new LinkLabel { Bounds = new Rectangle (10, 10, 100, 20), Text = "link" };
        using var form = Shown (link);
        var paints = 0;
        link.Paint += (_, _) => paints++;

        HeadlessRenderer.CapturePng (form);

        Assert.Equal (1, paints);
    }

    // ── EVT-21: removing the focused control left it focused ───────────────────────────────────────

    [Fact]
    public void EVT21_removing_the_focused_control_moves_focus_off_it ()
    {
        var panel = new Panel { Bounds = new Rectangle (10, 10, 300, 200) };
        var doomed = new TextBox { Bounds = new Rectangle (10, 10, 100, 25), TabIndex = 0 };
        var next = new TextBox { Bounds = new Rectangle (10, 50, 100, 25), TabIndex = 1 };
        panel.Controls.AddRange (new Control[] { doomed, next });
        using var form = Shown (panel);
        doomed.Focus ();
        var events = new List<string> ();
        doomed.Leave += (_, _) => events.Add ("Leave");
        doomed.LostFocus += (_, _) => events.Add ("LostFocus");
        doomed.KeyDown += (_, _) => events.Add ("KeyDown");

        panel.Controls.Remove (doomed);
        HeadlessRenderer.KeyDown (form, Keys.A);

        Assert.False (doomed.Focused);
        Assert.True (next.Focused);
        Assert.Equal (new[] { "Leave", "LostFocus" }, events);   // and no keystroke reached it
    }

    [Fact]
    public void EVT21_removing_the_last_focusable_control_leaves_nothing_focused ()
    {
        var doomed = new TextBox { Bounds = new Rectangle (10, 10, 100, 25) };
        using var form = Shown (doomed);
        doomed.Focus ();

        form.Controls.Remove (doomed);

        Assert.False (doomed.Focused);
        Assert.Null (form.adapter.SelectedControl);
    }

    [Fact]
    public void EVT21_ending_a_grid_edit_gives_focus_back_to_the_grid ()
    {
        // Ahead of the grid in tab order, so "the next control" and "the grid" are different answers.
        var button = new Button { Bounds = new Rectangle (10, 10, 80, 25), TabIndex = 0 };
        var grid = new DataGridView { Bounds = new Rectangle (10, 50, 300, 150), TabIndex = 1, AllowUserToAddRows = false };
        grid.Columns.Add ("a", "A");
        grid.Rows.Add ("x");
        using var form = Shown (button, grid);
        grid.CurrentCell = grid.Rows[0].Cells[0];
        Assert.True (grid.BeginEdit (true));
        Assert.True (grid.ContainsFocus);

        grid.EndEdit ();

        // Upstream's DataGridViewCell.DetachEditingControl focuses the grid before the editor leaves.
        Assert.True (grid.Focused);
        Assert.False (button.Focused);
    }

    [Fact]
    public void EVT21_a_second_grid_edit_stays_open ()
    {
        var grid = new DataGridView { Bounds = new Rectangle (10, 10, 300, 150), AllowUserToAddRows = false };
        grid.Columns.Add ("a", "A");
        grid.Rows.Add ("x");
        using var form = Shown (grid);
        grid.CurrentCell = grid.Rows[0].Cells[0];
        Assert.True (grid.BeginEdit (true));
        grid.EndEdit ();

        // Focus is now really on the grid, and its LostFocus -- raised as the next editor takes focus --
        // used to validate the row and so end the edit it was starting.
        Assert.True (grid.BeginEdit (true));

        Assert.True (grid.IsCurrentCellInEditMode);
        Assert.NotNull (grid.EditingControl);
    }

    // ── EVT-28: ChangeUICues was never raised ──────────────────────────────────────────────────────

    [Fact]
    public void EVT28_the_first_Tab_raises_ChangeUICues_with_ShowFocus ()
    {
        var first = new Button { Bounds = new Rectangle (10, 10, 80, 25), TabIndex = 0 };
        var second = new Button { Bounds = new Rectangle (10, 50, 80, 25), TabIndex = 1 };
        using var form = Shown (first, second);
        first.Focus ();
        var seen = new List<UICuesEventArgs> ();
        second.ChangeUICues += (_, e) => seen.Add (e);

        HeadlessRenderer.KeyDown (form, Keys.Tab);
        HeadlessRenderer.KeyDown (form, Keys.Tab);   // already showing: nothing changes

        var e = Assert.Single (seen);
        Assert.True (e.ShowFocus);
        Assert.True (e.ChangeFocus);
        Assert.False (e.ChangeKeyboard);
        Assert.Equal (UICues.ChangeFocus, e.Changed);
    }

    [Fact]
    public void EVT28_UICuesEventArgs_reports_the_flags_it_was_given ()
    {
        var e = new UICuesEventArgs (UICues.ShowKeyboard | UICues.ChangeKeyboard);

        Assert.True (e.ShowKeyboard);
        Assert.True (e.ChangeKeyboard);
        Assert.False (e.ShowFocus);
        Assert.Equal (UICues.ChangeKeyboard, e.Changed);
    }

    // ── EVT-32: the DPI-change events were never raised ────────────────────────────────────────────

    private sealed class DpiProbe : Panel
    {
        public DpiProbe (string name, List<string> log)
        {
            Name = name;
            DpiChangedBeforeParent += (_, _) => log.Add ("Before:" + name);
            DpiChangedAfterParent += (_, _) => log.Add ("After:" + name);
        }
    }

    [Fact]
    public void EVT32_a_scale_change_raises_the_DPI_events_in_Windows_order ()
    {
        var log = new List<string> ();
        var outer = new DpiProbe ("outer", log) { Bounds = new Rectangle (10, 10, 200, 100) };
        var inner = new DpiProbe ("inner", log) { Bounds = new Rectangle (10, 10, 50, 50) };
        outer.Controls.Add (inner);
        using var form = Shown (outer);
        HeadlessRenderer.CapturePng (form);   // the scale the controls first know
        DpiChangedEventArgs? changed = null;
        form.DpiChanged += (_, e) => { changed = e; log.Add ("Form"); };

        Application.UiScale = original_ui_scale * 2;

        // BEFOREPARENT bottom-up, the form's own change, then AFTERPARENT top-down.
        Assert.Equal (new[] { "Before:inner", "Before:outer", "Form", "After:outer", "After:inner" }, log);
        Assert.NotNull (changed);
        Assert.Equal (changed!.DeviceDpiOld * 2, changed.DeviceDpiNew);
        Assert.Equal (changed.DeviceDpiNew, inner.DeviceDpi);
    }

    // A guard rather than a regression test: nothing before the fix raised these at all, so it could
    // not fail then. It pins that the per-frame check stays quiet when the scale has not moved.
    [Fact]
    public void EVT32_a_frame_at_the_same_scale_raises_nothing ()
    {
        var log = new List<string> ();
        var probe = new DpiProbe ("probe", log) { Bounds = new Rectangle (10, 10, 50, 50) };
        using var form = Shown (probe);

        HeadlessRenderer.CapturePng (form);
        HeadlessRenderer.CapturePng (form);

        Assert.Empty (log);
    }

    // ── EVT-34: LocationChanged came before Move ───────────────────────────────────────────────────

    [Fact]
    public void EVT34_Move_is_raised_before_LocationChanged ()
    {
        var control = new Control { Bounds = new Rectangle (10, 10, 50, 50) };
        var order = new List<string> ();
        control.Move += (_, _) => order.Add ("Move");
        control.LocationChanged += (_, _) => order.Add ("LocationChanged");

        control.Location = new Point (20, 20);

        Assert.Equal (new[] { "Move", "LocationChanged" }, order);
    }

    // ── EVT-36: ValidateChildren neither recursed nor kept going after a failure ───────────────────

    private static (Form form, List<string> ran) GroupedForm ()
    {
        var ran = new List<string> ();
        var group = new GroupBox { Bounds = new Rectangle (10, 10, 300, 200) };
        var first = new TextBox { Bounds = new Rectangle (10, 20, 100, 25), Name = "first" };
        var second = new TextBox { Bounds = new Rectangle (10, 60, 100, 25), Name = "second" };

        foreach (var box in new[] { first, second })
            box.Validating += (s, e) => { ran.Add (((Control) s!).Name); e.Cancel = true; };

        group.Controls.AddRange (new Control[] { first, second });
        return (Shown (group), ran);
    }

    [Fact]
    public void EVT36_ValidateChildren_validates_every_field_inside_a_GroupBox ()
    {
        var (form, ran) = GroupedForm ();
        using var _ = form;

        Assert.False (form.ValidateChildren ());
        Assert.Equal (new[] { "first", "second" }, ran);   // both, not just the first failure
    }

    [Fact]
    public void EVT36_ImmediateChildren_does_not_recurse ()
    {
        var (form, ran) = GroupedForm ();
        using var _ = form;

        Assert.True (form.ValidateChildren (ValidationConstraints.ImmediateChildren));
        Assert.Empty (ran);
    }

    // ── EVT-38: Form.Paint's origin was the window frame, not the client area ──────────────────────

    [Fact]
    public void EVT38_Form_Paint_draws_from_the_client_origin ()
    {
        using var form = new Form { Width = 200, Height = 150 };

        // A thick border puts the client area well away from the frame's corner whatever chrome the
        // platform draws, so the test does not pass vacuously where the two coincide.
        form.Style.Border.Width = 6;
        form.Show ();
        form.Paint += (_, e) => { using var red = new Majorsilence.Forms.Drawing.SolidBrush (Color.Red); e.Graphics.FillRectangle (red, 0, 0, 4, 4); };

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form));
        var client = form.ContentRoot;
        var offset = form.adapter.ChildPaintOffset;
        var x = offset.X + client.ScaledLeft;
        var y = offset.Y + client.ScaledTop;

        Assert.True (x > 0 && y > 0);
        Assert.Equal (SKColors.Red, bitmap.GetPixel (x + 1, y + 1));
        Assert.NotEqual (SKColors.Red, bitmap.GetPixel (1, 1));
    }
}
