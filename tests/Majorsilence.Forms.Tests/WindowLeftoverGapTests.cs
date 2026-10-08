using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Automation;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Form, window and control leftovers (docs/behaviour-gap/form.md FRM-10/FRM-38, control.md CTL-29,
// simple.md SMP-16, events.md EVT-33): Form.ActiveControl, disposing a form's controls with it, the
// size grip, MaximizedBounds, BindingContextChanged on creation, and the accessibility events a tool
// strip item and a window raise. Each test names the upstream behaviour it pins.
[Collection ("Headless")]
public sealed class WindowLeftoverGapTests
{
    public WindowLeftoverGapTests () => HeadlessRenderer.Use ();

    private static Form Shown (params Control[] controls)
    {
        var form = new Form { Width = 400, Height = 300 };
        form.Controls.AddRange (controls);
        form.Show ();
        return form;
    }

    // ── FRM-10: Form.ActiveControl ───────────────────────────────────────────────────────────────

    [Fact]
    public void FRM10_ActiveControl_is_the_control_that_has_focus ()
    {
        var first = new TextBox { Bounds = new Rectangle (10, 10, 100, 20) };
        var second = new TextBox { Bounds = new Rectangle (10, 40, 100, 20) };
        using var form = Shown (first, second);

        second.Focus ();

        // Upstream ContainerControl.ActiveControl is the focused control. It used to be the first
        // control in tab order whatever had focus, or null.
        Assert.True (second.Focused);
        Assert.Same (second, form.ActiveControl);

        first.Focus ();
        Assert.Same (first, form.ActiveControl);
    }

    [Fact]
    public void FRM10_setting_ActiveControl_moves_focus_and_null_clears_it ()
    {
        var first = new TextBox { Bounds = new Rectangle (10, 10, 100, 20) };
        var second = new TextBox { Bounds = new Rectangle (10, 40, 100, 20) };
        using var form = Shown (first, second);

        form.ActiveControl = second;

        Assert.True (second.Focused);
        Assert.Same (second, form.ActiveControl);

        form.ActiveControl = null;

        Assert.False (second.Focused);
        Assert.Null (form.ActiveControl);
    }

    [Fact]
    public void FRM10_with_focus_in_a_user_control_the_form_names_the_user_control ()
    {
        var inner = new TextBox { Bounds = new Rectangle (5, 5, 100, 20) };
        var user = new UserControl { Bounds = new Rectangle (10, 10, 200, 100) };
        user.Controls.Add (inner);
        using var form = Shown (user);

        inner.Focus ();

        // Upstream UpdateFocusedControl: each container's active control is the step below it on the
        // focus path, so the form's is the user control and the user control's is the text box.
        Assert.Same (user, form.ActiveControl);
        Assert.Same (inner, user.ActiveControl);
    }

    [Fact]
    public void FRM10_ActiveControl_follows_focus_and_forgets_a_control_that_left ()
    {
        var first = new TextBox { Bounds = new Rectangle (10, 10, 100, 20) };
        var second = new TextBox { Bounds = new Rectangle (10, 40, 100, 20) };
        using var form = Shown (first, second);
        form.ActiveControl = first;
        second.Focus ();
        first.Enabled = false;   // nothing left for focus to move to once `second` goes

        form.Controls.Remove (second);

        // Upstream's _activeControl follows focus (UpdateFocusedControl) and AfterControlRemoved clears
        // it, so the answer is neither the control assigned earlier nor the one that has left.
        Assert.False (second.Focused);
        Assert.Null (form.ActiveControl);
        second.Dispose ();
    }

    // ── WindowBase.Dispose disposes the window's controls ─────────────────────────────────────────

    [Fact]
    public void Disposing_a_form_disposes_its_controls ()
    {
        var panel = new Panel ();
        var button = new Button ();
        panel.Controls.Add (button);
        var form = Shown (panel);
        var disposed = new List<string> ();
        panel.Disposed += (_, _) => disposed.Add ("panel");
        button.Disposed += (_, _) => disposed.Add ("button");

        form.Dispose ();

        // Upstream Control.Dispose disposes every child, and a Form is a Control.
        Assert.True (panel.IsDisposed);
        Assert.True (button.IsDisposed);
        Assert.Equal (new[] { "button", "panel" }, disposed);
    }

    [Fact]
    public void Disposing_a_form_that_was_never_shown_disposes_its_controls ()
    {
        var form = new Form ();
        var box = new TextBox ();
        form.Controls.Add (box);

        form.Dispose ();

        Assert.True (box.IsDisposed);
    }

    [Fact]
    public void Disposing_an_MDI_parent_disposes_its_children ()
    {
        var parent = new Form { IsMdiContainer = true, ClientSize = new Size (600, 400) };
        parent.Show ();
        var child = new Form { MdiParent = parent, Text = "Doc" };
        child.Show ();
        Assert.Contains (child, Application.OpenForms);

        parent.Dispose ();

        // Upstream the child forms are the MDI client's child controls, so they go with the parent.
        Assert.True (child.IsDisposed);
        Assert.DoesNotContain (child, Application.OpenForms);
    }

    [Fact]
    public void Disposing_a_form_disposes_a_form_hosted_on_it ()
    {
        var host = new Form { ClientSize = new Size (600, 400) };
        var panel = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add (panel);
        host.Show ();
        var hosted = new Form { TopLevel = false, FormBorderStyle = FormBorderStyle.None };
        panel.Controls.Add (hosted);
        hosted.Show ();
        Assert.Contains (hosted, Application.OpenForms);

        host.Dispose ();

        // A TopLevel = false form is a child control upstream, disposed with its container -- and not
        // left behind in OpenForms as a top-level window a dialog could pick as its owner.
        Assert.True (hosted.IsDisposed);
        Assert.DoesNotContain (hosted, Application.OpenForms);
    }

    // ── FRM-38: SizeGripStyle ────────────────────────────────────────────────────────────────────

    private static Form GripForm (SizeGripStyle style, FormBorderStyle border = FormBorderStyle.Sizable)
    {
        // The default BackColor: ControlPaint.Dark of a non-system colour such as White comes back as the
        // colour itself here (HLSColor.Darker skips upstream's NewLuma step), which would draw an
        // invisible grip -- a ControlPaint gap of its own, not the grip's.
        var form = new Form { Width = 300, Height = 200, SizeGripStyle = style, FormBorderStyle = border };
        form.Show ();
        return form;
    }

    // A point in the grip (logical window coordinates): 6px in from the client area's bottom-right
    // corner, which is inside the 16px grip and clear of the 4px resize border.
    private static Point InGrip (Form form)
    {
        var client = form.ContentRoot.Bounds;
        var border = form.Style.Border;
        return new Point (
            border.Left.GetWidth () + client.Right - 6,
            border.Top.GetWidth () + client.Bottom - 6);
    }

    private static SKBitmap Render (Form form)
        => SKBitmap.Decode (HeadlessRenderer.CapturePng (form));

    [Fact]
    public void FRM38_Show_draws_the_grip_in_the_client_areas_bottom_right_corner_only ()
    {
        using var shown = GripForm (SizeGripStyle.Show);
        using var hidden = GripForm (SizeGripStyle.Hide);
        using var with_grip = Render (shown);
        using var without = Render (hidden);

        // The grip square, in device pixels.
        var scaling = shown.Scaling;
        var grip_corner = InGrip (shown);
        var grip = new Rectangle (
            (int) ((grip_corner.X + 6 - 16) * scaling), (int) ((grip_corner.Y + 6 - 16) * scaling),
            (int) Math.Ceiling (16 * scaling), (int) Math.Ceiling (16 * scaling));

        var inside = 0;
        var outside = 0;

        for (var y = 0; y < with_grip.Height; y++) {
            for (var x = 0; x < with_grip.Width; x++) {
                if (with_grip.GetPixel (x, y) == without.GetPixel (x, y))
                    continue;

                if (grip.Contains (x, y))
                    inside++;
                else
                    outside++;
            }
        }

        Assert.True (inside > 0, "the grip drew nothing");
        Assert.Equal (0, outside);
    }

    [Theory]
    [InlineData (SizeGripStyle.Show, FormBorderStyle.Sizable, true)]
    [InlineData (SizeGripStyle.Show, FormBorderStyle.SizableToolWindow, true)]
    [InlineData (SizeGripStyle.Show, FormBorderStyle.FixedSingle, false)]
    [InlineData (SizeGripStyle.Hide, FormBorderStyle.Sizable, false)]
    [InlineData (SizeGripStyle.Auto, FormBorderStyle.Sizable, false)]   // not modal
    public void FRM38_the_grip_follows_upstreams_UpdateRenderSizeGrip (SizeGripStyle style, FormBorderStyle border, bool renders)
    {
        using var form = new Form { SizeGripStyle = style, FormBorderStyle = border };

        Assert.Equal (renders, form.RendersSizeGrip);
    }

    [Fact]
    public void FRM38_Auto_shows_the_grip_while_the_form_is_modal ()
    {
        using var owner = new Form ();
        owner.Show ();
        var dialog = new Form { SizeGripStyle = SizeGripStyle.Auto };
        bool? during = null;
        dialog.Shown += (_, _) => {
            during = dialog.RendersSizeGrip;
            dialog.Close ();
        };

        dialog.ShowDialog (owner);

        Assert.True (during);
        Assert.False (dialog.RendersSizeGrip);
        dialog.Dispose ();
    }

    [Fact]
    public void FRM38_pressing_the_grip_starts_a_resize ()
    {
        using var form = GripForm (SizeGripStyle.Show);
        var resizes = 0;
        form.ResizeBegin += (_, _) => resizes++;
        var at = InGrip (form);

        HeadlessRenderer.MouseDown (form, at.X, at.Y);
        HeadlessRenderer.MouseUp (form, at.X, at.Y);

        // Upstream WmNCHitTest answers HTBOTTOMRIGHT over the grip.
        Assert.Equal (1, resizes);
    }

    [Fact]
    public void FRM38_without_a_grip_the_same_point_is_client_area ()
    {
        using var form = GripForm (SizeGripStyle.Hide);
        var resizes = 0;
        var presses = 0;
        form.ResizeBegin += (_, _) => resizes++;
        form.MouseDown += (_, _) => presses++;
        var at = InGrip (form);

        HeadlessRenderer.MouseDown (form, at.X, at.Y);
        HeadlessRenderer.MouseUp (form, at.X, at.Y);

        Assert.Equal (0, resizes);
        Assert.Equal (1, presses);
    }

    // ── FRM-38: MaximizedBounds ──────────────────────────────────────────────────────────────────

    [Fact]
    public void FRM38_MaximizedBounds_is_handed_to_a_backend_that_takes_the_hint ()
    {
        using var form = new Form ();
        var bounds = new Rectangle (100, 50, 640, 480);
        var changes = 0;
        form.MaximizedBoundsChanged += (_, _) => changes++;

        form.MaximizedBounds = bounds;

        // Upstream answers WM_GETMINMAXINFO from it; here the backend gets it as its maximize hint.
        Assert.Equal (bounds, ((HeadlessWindowHost) form.Backend).MaximizedBounds);
        Assert.Equal (1, changes);

        form.MaximizedBounds = Rectangle.Empty;
        Assert.Equal (Rectangle.Empty, ((HeadlessWindowHost) form.Backend).MaximizedBounds);
    }

    // ── CTL-29: a control created into a live parent hears its inherited BindingContext ───────────

    [Fact]
    public void CTL29_a_control_added_to_a_shown_form_raises_BindingContextChanged_once ()
    {
        using var form = Shown ();
        var control = new Control ();
        var changes = 0;
        control.BindingContextChanged += (_, _) => changes++;

        form.Controls.Add (control);

        // Upstream ControlCollection.Add calls the public CreateControl, which notifies an uncreated
        // control with a parent and no context of its own (Control.cs CreateControl).
        Assert.True (control.Created);
        Assert.Equal (1, changes);
    }

    [Fact]
    public void CTL29_a_hidden_control_hears_it_when_it_is_shown_not_when_it_is_added ()
    {
        using var form = Shown ();
        var control = new Control { Visible = false };
        var changes = 0;
        control.BindingContextChanged += (_, _) => changes++;

        form.Controls.Add (control);
        Assert.Equal (0, changes);

        control.Visible = true;
        Assert.Equal (1, changes);
    }

    [Fact]
    public void CTL29_children_created_with_their_parent_do_not_raise_it ()
    {
        using var form = Shown ();
        var panel = new Panel ();
        var child = new Control ();
        panel.Controls.Add (child);
        var child_changes = 0;
        child.BindingContextChanged += (_, _) => child_changes++;

        form.Controls.Add (panel);

        // The panel's change cascades to the child once (OnParentBindingContextChanged); the child's own
        // creation, part of the panel's, adds nothing -- upstream's internal CreateControl (bool).
        Assert.True (child.Created);
        Assert.Equal (1, child_changes);
    }

    // ── SMP-16: a ToolStripStatusLabel's LiveSetting reaches the observer ─────────────────────────

    private static Form StripForm (out ToolStripStatusLabel label)
    {
        var form = new Form { Width = 300, Height = 200 };
        var strip = new StatusStrip ();
        label = new ToolStripStatusLabel { Name = "progress", Text = "Idle" };
        strip.Items.Add (label);
        form.Controls.Add (strip);
        HeadlessRenderer.CapturePng (form, 300, 200);
        return form;
    }

    [Fact]
    public void SMP16_a_live_status_labels_text_change_reaches_the_observer ()
    {
        using var form = StripForm (out var label);
        using var observer = new AutomationObserver (form);
        var changes = new List<AutomationElement?> ();
        observer.LiveRegionChanged += (_, e) => changes.Add (e);
        label.LiveSetting = AutomationLiveSetting.Assertive;

        label.Text = "Saving";

        // Upstream ToolStripStatusLabel.OnTextChanged raises the live region change; the element is the
        // item's own, which the UI Automation bridge raises UIA's LiveRegionChanged on.
        var change = Assert.Single (changes);
        Assert.Same (label, change?.Source);
        Assert.Equal ("Saving", change?.Name);
        Assert.Equal (AutomationLiveSetting.Assertive, change?.LiveSetting);
    }

    [Fact]
    public void SMP16_a_status_label_whose_LiveSetting_is_Off_raises_nothing ()
    {
        using var form = StripForm (out var label);
        using var observer = new AutomationObserver (form);
        var changes = 0;
        observer.LiveRegionChanged += (_, _) => changes++;

        label.Text = "Saving";

        Assert.Equal (0, changes);
        Assert.False (label.AccessibilityObject.RaiseLiveRegionChanged ());
    }

    [Fact]
    public void SMP16_RaiseLiveRegionChanged_on_a_status_label_is_true_when_an_observer_takes_it ()
    {
        using var form = StripForm (out var label);
        label.LiveSetting = AutomationLiveSetting.Polite;
        Assert.False (label.AccessibilityObject.RaiseLiveRegionChanged ());

        using var observer = new AutomationObserver (form);
        observer.LiveRegionChanged += (_, _) => { };

        Assert.True (label.AccessibilityObject.RaiseLiveRegionChanged ());
    }

    // ── EVT-33: ToolStripItem and WindowBase QueryAccessibilityHelp ──────────────────────────────

    [Fact]
    public void EVT33_a_tool_strip_items_Help_asks_its_QueryAccessibilityHelp_handlers ()
    {
        using var form = StripForm (out var label);
        object? sender = null;
        label.QueryAccessibilityHelp += (s, e) => {
            sender = s;
            e.HelpString = "Shows the save progress";
            e.HelpNamespace = "app.chm";
            e.HelpKeyword = "7";
        };

        Assert.Equal ("Shows the save progress", label.AccessibilityObject.Help);
        Assert.Same (label, sender);
        Assert.Equal (7, label.AccessibilityObject.GetHelpTopic (out var file));
        Assert.Equal ("app.chm", file);

        // And through the automation tree, which is what the UI Automation bridge's HelpText reads.
        var element = AutomationProvider.BuildTree (form).Self ().Single (e => ReferenceEquals (e.Source, label));
        Assert.Equal ("Shows the save progress", element.HelpText);
    }

    [Fact]
    public void EVT33_a_tool_strip_item_without_a_handler_has_the_base_help ()
    {
        using var form = StripForm (out var label);

        Assert.Null (label.AccessibilityObject.Help);
        Assert.Equal (-1, label.AccessibilityObject.GetHelpTopic (out var file));
        Assert.Null (file);
    }

    [Fact]
    public void EVT33_a_windows_Help_asks_its_QueryAccessibilityHelp_handlers ()
    {
        using var form = new Form { Text = "Orders" };
        object? sender = null;
        form.QueryAccessibilityHelp += (s, e) => {
            sender = s;
            e.HelpString = "Lists the open orders";
            e.HelpNamespace = "orders.chm";
            e.HelpKeyword = "3";
        };

        Assert.Equal ("Lists the open orders", form.AccessibilityObject.Help);
        Assert.Same (form, sender);
        Assert.Equal (3, form.AccessibilityObject.GetHelpTopic (out var file));
        Assert.Equal ("orders.chm", file);

        // The tree's root is the window; its HelpText is the window's.
        Assert.Equal ("Lists the open orders", AutomationProvider.BuildTree (form).HelpText);
    }
    // ── The form's own cursor over its empty client area ─────────────────────────────────────────

    [Fact]
    public void The_forms_cursor_comes_back_after_Cursor_Show_over_the_empty_client_area ()
    {
        using var form = new Form { Width = 300, Height = 200, Cursor = Cursors.Cross };
        form.Show ();
        var host = (HeadlessWindowHost) form.Backend;

        Cursor.Hide ();
        try {
            HeadlessRenderer.MouseMove (form, 150, 100);
            HeadlessRenderer.MouseMove (form, 151, 101);
            Assert.Equal (CursorType.None, host.Cursor);
        } finally {
            Cursor.Show ();
        }

        // Upstream the form is the root control, so its client area is the form and the form's cursor
        // is what WM_SETCURSOR puts back.
        Assert.Equal (CursorType.Cross, host.Cursor);
        Assert.Same (Cursors.Cross, form.Cursor);
    }

    [Fact]
    public void The_forms_cursor_is_shown_over_its_empty_client_area_and_inherited_by_its_controls ()
    {
        var panel = new Panel { Bounds = new Rectangle (10, 10, 100, 60) };
        var form = new Form { Width = 300, Height = 200, Cursor = Cursors.Cross };
        form.Controls.Add (panel);
        form.Show ();
        var host = (HeadlessWindowHost) form.Backend;

        HeadlessRenderer.MouseMove (form, 200, 150);
        HeadlessRenderer.MouseMove (form, 201, 151);
        Assert.Equal (CursorType.Cross, host.Cursor);

        // A control with no cursor of its own inherits its parent's, and the form is the top of that chain.
        Assert.Same (Cursors.Cross, panel.Cursor);
        var at = panel.GetPositionInForm ();
        HeadlessRenderer.MouseMove (form, at.X + 5, at.Y + 5);
        HeadlessRenderer.MouseMove (form, at.X + 6, at.Y + 6);
        Assert.Equal (CursorType.Cross, host.Cursor);

        // Hovering a control with its own cursor does not change what the form's Cursor property says.
        panel.Cursor = Cursors.Hand;
        Assert.Equal (CursorType.Hand, host.Cursor);
        Assert.Same (Cursors.Cross, form.Cursor);

        // Changing the form's cursor while over a control with its own leaves that control's showing;
        // over one that inherits, the new cursor shows at once.
        form.Cursor = Cursors.IBeam;
        Assert.Equal (CursorType.Hand, host.Cursor);
        panel.Cursor = null!;   // upstream's setter takes null: inherit again
        Assert.Equal (CursorType.Ibeam, host.Cursor);
        form.Dispose ();
    }
}
