using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Control-base behaviour-gap findings (docs/behaviour-gap/control.md, issue #341). Each test names the
// finding it pins; the upstream source each behaviour was taken from is cited in the fix.
[Collection ("Headless")]
public sealed class ControlBaseGapTests : IDisposable
{
    private readonly Cursor? original_current = Cursor.Current;
    private readonly MouseButtons original_buttons = Control.MouseButtons;

    public ControlBaseGapTests () => HeadlessRenderer.Use ();

    // Cursor.Current and Control.MouseButtons are process-wide; put them back for the next test.
    public void Dispose ()
    {
        Cursor.Current = original_current;
        Control.MouseButtons = original_buttons;
    }

    private static Form Shown (params Control[] controls)
    {
        var form = new Form { Width = 400, Height = 300 };
        form.Controls.AddRange (controls);
        form.Show ();
        return form;
    }

    // ── CTL-03: ControlStyles.Selectable and CanSelect were two different flags ──────────────────

    private sealed class NotSelectable : Control
    {
        public NotSelectable () => SetStyle (ControlStyles.Selectable, false);

        public bool BehaviorSelectable => GetControlBehavior (ControlBehaviors.Selectable);
    }

    private sealed class SelectablePanel : Panel
    {
        public SelectablePanel () => SetStyle (ControlStyles.Selectable, true);
    }

    [Fact]
    public void CTL03_SetStyle_Selectable_false_makes_a_control_unfocusable ()
    {
        var control = new NotSelectable { Bounds = new Rectangle (10, 10, 50, 20) };
        using var form = Shown (control);

        Assert.False (control.CanSelect);
        Assert.False (control.Focus ());
        Assert.False (control.BehaviorSelectable);   // the library's own flag reads the same store
    }

    [Fact]
    public void CTL03_SetStyle_Selectable_true_makes_a_Panel_focusable ()
    {
        var panel = new SelectablePanel { Bounds = new Rectangle (10, 10, 50, 20) };
        using var form = Shown (panel);

        Assert.True (panel.CanSelect);
        Assert.True (panel.Focus ());
    }

    [Fact]
    public void CTL03_SetControlBehavior_Selectable_is_ControlStyles_Selectable ()
    {
        // Label turns Selectable off through the library's own SetControlBehavior; upstream Label
        // does it with SetStyle (Label.cs). Either way GetStyle must see it.
        Assert.False (new Label ().GetStyle (ControlStyles.Selectable));
        Assert.True (new Button ().GetStyle (ControlStyles.Selectable));
    }

    [Fact]
    public void CTL03_clicking_a_non_selectable_control_leaves_focus_where_it_was ()
    {
        var box = new TextBox { Bounds = new Rectangle (10, 10, 100, 24) };
        var control = new NotSelectable { Bounds = new Rectangle (10, 60, 100, 40) };
        using var form = Shown (box, control);
        box.Focus ();

        var at = control.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 5, at.Y + 5);

        Assert.True (box.Focused);
    }

    // ── CTL-04: the second press of a double-click carries Clicks == 2 ─────────────────────────────

    [Fact]
    public void CTL04_the_second_MouseDown_of_a_double_click_has_Clicks_2 ()
    {
        var control = new Control { Bounds = new Rectangle (20, 20, 100, 40) };
        using var form = Shown (control);
        var clicks = new List<int> ();
        control.MouseDown += (_, e) => clicks.Add (e.Clicks);
        var double_clicks = 0;
        var single_clicks = 0;
        control.DoubleClick += (_, _) => double_clicks++;
        control.Click += (_, _) => single_clicks++;

        var at = control.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10);
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10);

        Assert.Equal (new[] { 1, 2 }, clicks);
        Assert.Equal (1, single_clicks);
        Assert.Equal (1, double_clicks);
    }

    // ── CTL-12: ScrollableControl.Recalculate copy/paste faults ────────────────────────────────────

    private static Control HorizontalBar (ScrollableControl panel)
        => panel.Controls.GetAllControls (true).First (c => c.GetType ().Name == "HorizontalScrollBar");

    [Fact]
    public void CTL12_a_layout_pass_keeps_the_horizontal_scroll_position ()
    {
        var panel = new Panel { AutoScroll = true, Bounds = new Rectangle (0, 0, 200, 100) };
        var child = new Panel { Bounds = new Rectangle (0, 0, 400, 40) };   // wide only: no vertical bar
        panel.Controls.Add (child);
        using var form = Shown (panel);

        panel.HorizontalScroll.Value = 50;
        Assert.Equal (-50, panel.AutoScrollPosition.X);
        Assert.Equal (-50, child.Left);

        panel.PerformLayout ();

        Assert.Equal (-50, panel.AutoScrollPosition.X);

        // The bookkeeping must still match the children, or this scroll moves them by 60, not 10.
        panel.HorizontalScroll.Value = 60;
        Assert.Equal (-60, child.Left);
        Assert.Equal (-60, panel.AutoScrollPosition.X);
    }

    [Fact]
    public void CTL12_the_horizontal_bar_spans_the_width_without_a_size_grip ()
    {
        var panel = new Panel { AutoScroll = true, Bounds = new Rectangle (0, 0, 200, 100) };
        panel.Controls.Add (new Panel { Bounds = new Rectangle (0, 0, 400, 40) });
        using var form = Shown (panel);

        var bar = HorizontalBar (panel);

        Assert.True (bar.Visible);
        Assert.Equal (panel.Width, bar.Width);
    }

    // ── CTL-15: ctl.Parent = x raised ParentChanged twice ─────────────────────────────────────────

    [Fact]
    public void CTL15_setting_Parent_raises_ParentChanged_once ()
    {
        var panel = new Panel ();
        var control = new Control ();
        var count = 0;
        control.ParentChanged += (_, _) => count++;

        control.Parent = panel;

        Assert.Equal (1, count);
        Assert.Same (panel, control.Parent);
    }

    // ── CTL-16: Disposing was never set ────────────────────────────────────────────────────────────

    [Fact]
    public void CTL16_disposing_a_parent_raises_no_VisibleChanged_on_its_children ()
    {
        var parent = new Panel ();
        var child = new Control ();
        parent.Controls.Add (child);
        using var form = Shown (parent);
        var visible_changes = 0;
        child.VisibleChanged += (_, _) => visible_changes++;

        parent.Dispose ();

        Assert.Equal (0, visible_changes);
        Assert.Null (child.Parent);
        Assert.True (child.IsDisposed);
    }

    [Fact]
    public void CTL16_Disposing_is_true_during_the_teardown_and_false_after ()
    {
        var parent = new Panel ();
        var child = new Control ();
        parent.Controls.Add (child);
        bool? parent_disposing_seen = null;
        bool? own_disposing_seen = null;
        child.Disposed += (_, _) => parent_disposing_seen = parent.Disposing;
        parent.Disposed += (_, _) => own_disposing_seen = parent.Disposing;

        parent.Dispose ();

        Assert.True (parent_disposing_seen);
        Assert.True (own_disposing_seen);
        Assert.False (parent.Disposing);
        Assert.True (parent.IsDisposed);
    }

    // ── CTL-19: BeginInvoke returned a Task that never started ─────────────────────────────────────

    [Fact]
    public void CTL19_BeginInvoke_completes_and_EndInvoke_returns_the_value ()
    {
        var control = new Control ();

        var result = control.BeginInvoke (new Func<int> (() => 42));
        Application.DoEvents ();

        Assert.True (result.IsCompleted);
        Assert.Equal (42, control.EndInvoke (result));
    }

    [Fact]
    public void CTL19_EndInvoke_on_the_UI_thread_runs_the_pending_call ()
    {
        var control = new Control ();
        var result = control.BeginInvoke (new Func<string, string> (s => s + "!"), "hi");

        Assert.Equal ("hi!", control.EndInvoke (result));   // no DoEvents first: EndInvoke drains it
    }

    [Fact]
    public void CTL19_EndInvoke_rethrows_the_callback_exception ()
    {
        var control = new Control ();
        var result = control.BeginInvoke ((Delegate) new Action (() => throw new InvalidOperationException ("boom")));

        // The loop reports it too (Application.ThreadException); with no handler it propagates there.
        Assert.Throws<InvalidOperationException> (() => Application.DoEvents ());

        Assert.True (result.IsCompleted);
        Assert.Equal ("boom", Assert.Throws<InvalidOperationException> (() => control.EndInvoke (result)).Message);
    }

    // ── CTL-20: Handle / CreateHandle / RecreateHandle ─────────────────────────────────────────────

    private sealed class HandleProbe : Control
    {
        public int Created_, Destroyed;
        public bool RecreatingSeen;

        protected override void OnHandleCreated (EventArgs e)
        {
            Created_++;
            RecreatingSeen |= RecreatingHandle;
            base.OnHandleCreated (e);
        }

        protected override void OnHandleDestroyed (EventArgs e)
        {
            Destroyed++;
            base.OnHandleDestroyed (e);
        }

        public void Recreate () => RecreateHandle ();
    }

    [Fact]
    public void CTL20_reading_Handle_creates_the_handle_once ()
    {
        var control = new HandleProbe ();
        Assert.False (control.IsHandleCreated);

        var handle = control.Handle;

        Assert.Equal (IntPtr.Zero, handle);   // never faked: docs/native-interop.md
        Assert.True (control.IsHandleCreated);
        Assert.Equal (1, control.Created_);
        _ = control.Handle;
        Assert.Equal (1, control.Created_);   // not announced again

        // CreateControl later does not raise HandleCreated a second time.
        using var form = Shown (control);
        Assert.Equal (1, control.Created_);
    }

    [Fact]
    public void CTL20_RecreateHandle_destroys_and_recreates ()
    {
        var control = new HandleProbe ();
        using var form = Shown (control);
        Assert.Equal (1, control.Created_);

        control.Recreate ();

        Assert.Equal (1, control.Destroyed);
        Assert.Equal (2, control.Created_);
        Assert.True (control.RecreatingSeen);
        Assert.False (control.RecreatingHandle);
        Assert.True (control.IsHandleCreated);
    }

    // ── CTL-21: Refresh / Update painted nothing synchronously ─────────────────────────────────────

    [Fact]
    public void CTL21_Refresh_raises_Paint_before_it_returns ()
    {
        var label = new Label { Bounds = new Rectangle (10, 10, 100, 20), Text = "0" };
        using var form = Shown (label);
        HeadlessRenderer.CapturePng (form);   // the first frame: nothing dirty is left
        var paints = 0;
        label.Paint += (_, _) => paints++;

        label.Text = "1";
        label.Refresh ();

        Assert.Equal (1, paints);
    }

    [Fact]
    public void CTL21_the_next_frame_shows_what_Update_painted ()
    {
        var parent = new Panel { Bounds = new Rectangle (10, 10, 200, 100), BackColor = Color.White };
        var child = new Panel { Bounds = new Rectangle (20, 20, 60, 40), BackColor = Color.White };
        parent.Controls.Add (child);
        using var form = Shown (parent);
        form.FormBorderStyle = FormBorderStyle.None;
        HeadlessRenderer.CapturePng (form, 300, 200);

        child.BackColor = Color.Red;
        child.Update ();

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
        var scale = bitmap.Width / 300f;
        Assert.Equal (new SKColor (255, 0, 0), bitmap.GetPixel ((int) (50 * scale), (int) (50 * scale)));
    }

    // ── CTL-22: the Cursor setter waited for the pointer to re-enter ───────────────────────────────

    [Fact]
    public void CTL22_setting_Cursor_under_the_pointer_shows_it_at_once ()
    {
        var button = new Button { Bounds = new Rectangle (20, 20, 100, 30) };
        using var form = Shown (button);
        var at = button.GetPositionInForm ();
        HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);

        button.Cursor = Cursors.Hand;

        Assert.Equal (CursorType.Hand, ((HeadlessWindowHost) form.Backend).Cursor);
    }

    // ── CTL-23: Region clips painting and hit-testing ──────────────────────────────────────────────

    [Fact]
    public void CTL23_Region_clips_the_paint_and_the_hit_test ()
    {
        var form = new Form { ClientSize = new Size (300, 200), FormBorderStyle = FormBorderStyle.None, BackColor = Color.White };
        var control = new Panel { Bounds = new Rectangle (0, 0, 100, 100), BackColor = Color.Red };
        form.Controls.Add (control);
        form.Show ();
        using var _ = form;
        var changes = 0;
        control.RegionChanged += (_, _) => changes++;

        control.Region = new Majorsilence.Forms.Drawing.Region (new Rectangle (0, 0, 50, 50));

        Assert.Equal (1, changes);

        using var bitmap = SKBitmap.Decode (HeadlessRenderer.CapturePng (form, 300, 200));
        var scale = bitmap.Width / 300f;
        Assert.Equal (new SKColor (255, 0, 0), bitmap.GetPixel ((int) (20 * scale), (int) (20 * scale)));
        Assert.Equal (SKColors.White, bitmap.GetPixel ((int) (80 * scale), (int) (80 * scale)));

        var clicks = 0;
        control.Click += (_, _) => clicks++;
        var at = control.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 80, at.Y + 80);
        Assert.Equal (0, clicks);
        HeadlessRenderer.Click (form, at.X + 20, at.Y + 20);
        Assert.Equal (1, clicks);

        var host = new Panel { Bounds = new Rectangle (0, 0, 200, 200) };
        var shaped = new Control { Bounds = new Rectangle (0, 0, 100, 100), Region = new Majorsilence.Forms.Drawing.Region (new Rectangle (0, 0, 50, 50)) };
        host.Controls.Add (shaped);
        form.Controls.Add (host);   // a parentless control reports itself hidden here
        Assert.Null (host.GetChildAtPoint (new Point (90, 90)));
        Assert.Same (shaped, host.GetChildAtPoint (new Point (10, 10)));
    }

    // ── CTL-25: TopLevelControl ────────────────────────────────────────────────────────────────────

    private sealed class Floating : Control
    {
        public void MakeTopLevel () => SetTopLevel (true);
    }

    [Fact]
    public void CTL25_a_top_level_control_is_its_childrens_TopLevelControl ()
    {
        using var owner = Shown ();
        var floating = new Floating { Bounds = new Rectangle (100, 100, 120, 80) };
        var child = new Control ();
        floating.Controls.Add (child);

        floating.MakeTopLevel ();

        Assert.NotNull (floating.Parent);   // hosted in a popup window's root
        Assert.Same (floating, child.TopLevelControl);
        Assert.Same (floating, floating.TopLevelControl);
        floating.Dispose ();
    }

    // ── CTL-26: right-click with a context menu; focus only on a left press ────────────────────────

    [Fact]
    public void CTL26_a_right_click_on_a_control_with_a_context_menu_is_still_a_MouseClick ()
    {
        var control = new Control { Bounds = new Rectangle (20, 20, 100, 40), ContextMenuStrip = new ContextMenuStrip () };
        control.ContextMenuStrip.Items.Add ("Item");
        using var form = Shown (control);
        var buttons = new List<MouseButtons> ();
        control.MouseClick += (_, e) => buttons.Add (e.Button);

        var at = control.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 10, at.Y + 10, MouseButtons.Right);
        Application.ClosePopups ();

        Assert.Equal (new[] { MouseButtons.Right }, buttons);
    }

    [Fact]
    public void CTL26_a_right_press_does_not_take_focus_but_a_left_press_does ()
    {
        var box = new TextBox { Bounds = new Rectangle (10, 10, 100, 24) };
        var button = new Button { Bounds = new Rectangle (10, 60, 100, 30) };
        using var form = Shown (box, button);
        box.Focus ();
        var at = button.GetPositionInForm ();

        HeadlessRenderer.Click (form, at.X + 5, at.Y + 5, MouseButtons.Right);
        Assert.True (box.Focused);

        // Clicks far enough apart in time not to make a double-click are irrelevant here: focus
        // follows the press, whichever click count it carries.
        HeadlessRenderer.Click (form, at.X + 5, at.Y + 5, MouseButtons.Left);
        Assert.True (button.Focused);
    }

    [Fact]
    public void CTL26_a_right_press_still_focuses_a_TextBox ()
    {
        var other = new TextBox { Bounds = new Rectangle (10, 10, 100, 24) };
        var box = new TextBox { Bounds = new Rectangle (10, 60, 100, 24) };
        using var form = Shown (other, box);
        other.Focus ();

        var at = box.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 5, at.Y + 5, MouseButtons.Right);

        Assert.True (box.Focused);
    }

    // ── CTL-27: clicking a UserControl's blank area kept stealing focus from its child ─────────────

    [Fact]
    public void CTL27_clicking_blank_space_in_a_UserControl_keeps_its_child_focused ()
    {
        var user = new UserControl { Bounds = new Rectangle (10, 10, 200, 120) };
        var box = new TextBox { Bounds = new Rectangle (10, 10, 100, 24) };
        user.Controls.Add (box);
        using var form = Shown (user);
        box.Focus ();
        var leaves = 0;
        box.Leave += (_, _) => leaves++;

        var at = user.GetPositionInForm ();
        HeadlessRenderer.Click (form, at.X + 150, at.Y + 100);

        Assert.True (box.Focused);
        Assert.Equal (0, leaves);
    }

    // ── CTL-29: BindingContextChanged reaches controls that inherit the context ────────────────────

    [Fact]
    public void CTL29_a_new_parent_context_reaches_a_child_that_inherits_it ()
    {
        var parent = new Panel ();
        var child = new Control ();
        var own = new Control { BindingContext = new BindingContext () };
        parent.Controls.Add (child);
        parent.Controls.Add (own);
        var inherited_changes = 0;
        var own_changes = 0;
        child.BindingContextChanged += (_, _) => inherited_changes++;
        own.BindingContextChanged += (_, _) => own_changes++;

        parent.BindingContext = new BindingContext ();

        Assert.Equal (1, inherited_changes);
        Assert.Equal (0, own_changes);   // its own context is unaffected, as upstream
        Assert.Same (parent.BindingContext, child.BindingContext);
    }

    [Fact]
    public void CTL29_reparenting_a_created_control_raises_BindingContextChanged ()
    {
        var first = new Panel ();
        var second = new Panel ();
        using var form = Shown (first, second);
        var child = new Control ();
        first.Controls.Add (child);
        var changes = 0;
        child.BindingContextChanged += (_, _) => changes++;

        second.Controls.Add (child);

        Assert.True (changes >= 1);
    }
}
