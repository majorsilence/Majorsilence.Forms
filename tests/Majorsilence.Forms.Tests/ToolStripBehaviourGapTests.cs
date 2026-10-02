using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Issue #351, the ToolStrip family's open behaviour-gap findings (docs/behaviour-gap/toolstrip.md):
/// DisplayStyle (TSM-07), the split button's two halves (TSM-15), ToolTip.Show and the hover delays
/// (TSM-17, TSM-34), item MouseDown/MouseUp (TSM-18), a host's Text and Enabled (TSM-20, TSM-38),
/// context-menu close reasons and AutoClose (TSM-21), separators on a bar (TSM-23), CheckState
/// (TSM-24), IsOnDropDown (TSM-25), PerformClick's gate (TSM-26), TabStop (TSM-29), Form.Menu and
/// the legacy radio glyph (TSM-32) and DropDownOpened accuracy (TSM-35).
/// </summary>
[Collection ("Headless")]
public class ToolStripBehaviourGapTests
{
    private static MouseEventArgs Left (int x, int y) => new (MouseButtons.Left, 1, x, y, 0);

    private static Point Centre (MenuItem item)
        => new (item.Bounds.Left + item.Bounds.Width / 2, item.Bounds.Top + item.Bounds.Height / 2);

    private static SKBitmap Swatch ()
    {
        var bitmap = new SKBitmap (16, 16);

        using (var canvas = new SKCanvas (bitmap))
            canvas.Clear (new SKColor (255, 0, 0));

        return bitmap;
    }

    private static bool SamePixels (SKBitmap a, SKBitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return false;

        for (var y = 0; y < a.Height; y++)
            for (var x = 0; x < a.Width; x++)
                if (a.GetPixel (x, y) != b.GetPixel (x, y))
                    return false;

        return true;
    }

    private static ToolStrip StripWith (params ToolStripItem[] items)
    {
        HeadlessRenderer.Use ();
        var strip = new ToolStrip { Width = 400, Height = 40 };

        foreach (var item in items)
            strip.Items.Add (item);

        PaintSurface.Render (strip).Dispose ();   // items lay out on paint
        return strip;
    }

    // ---------------- TSM-07: DisplayStyle

    [Fact]
    public void DisplayStyle_Image_measures_the_image_alone ()
    {
        var image_only = new ToolStripButton { Text = "Save", Image = Swatch (), DisplayStyle = ToolStripItemDisplayStyle.Image };
        var no_text = new ToolStripButton { Text = "", Image = Swatch () };
        var both = new ToolStripButton { Text = "Save", Image = Swatch () };
        using var strip = StripWith (image_only, no_text, both);

        Assert.Equal (no_text.GetPreferredSize (Size.Empty), image_only.GetPreferredSize (Size.Empty));
        Assert.True (both.GetPreferredSize (Size.Empty).Width > image_only.GetPreferredSize (Size.Empty).Width);
    }

    [Fact]
    public void DisplayStyle_Text_measures_the_text_alone ()
    {
        var text_only = new ToolStripButton { Text = "Save", Image = Swatch (), DisplayStyle = ToolStripItemDisplayStyle.Text };
        var no_image = new ToolStripButton { Text = "Save" };
        using var strip = StripWith (text_only, no_image);

        Assert.Equal (no_image.GetPreferredSize (Size.Empty), text_only.GetPreferredSize (Size.Empty));
    }

    [Fact]
    public void DisplayStyle_Image_paints_no_caption ()
    {
        // Two strips, one item each, identical but for the caption: with DisplayStyle Image the caption
        // must not reach the canvas, so the two renders are the same picture.
        using var with_caption = StripWith (new ToolStripButton { Text = "WWWW", Image = Swatch (), DisplayStyle = ToolStripItemDisplayStyle.Image });
        using var without = StripWith (new ToolStripButton { Text = "", Image = Swatch () });
        using var a = PaintSurface.Render (with_caption);
        using var b = PaintSurface.Render (without);

        Assert.True (SamePixels (a, b));
    }

    [Fact]
    public void A_status_label_with_DisplayStyle_Image_paints_no_caption ()
    {
        HeadlessRenderer.Use ();

        SKBitmap Render (string text, ToolStripItemDisplayStyle style)
        {
            using var strip = new StatusStrip { Width = 300, Height = 24 };
            strip.Items.Add (new ToolStripStatusLabel { Text = text, DisplayStyle = style });
            PaintSurface.Render (strip).Dispose ();
            return PaintSurface.Render (strip);
        }

        using var hidden = Render ("WWWW", ToolStripItemDisplayStyle.Image);
        using var empty = Render ("", ToolStripItemDisplayStyle.ImageAndText);
        using var shown = Render ("WWWW", ToolStripItemDisplayStyle.Text);

        Assert.True (SamePixels (hidden, empty));
        Assert.False (SamePixels (shown, empty));   // the control: the caption does draw when asked to
    }

    // ---------------- TSM-15: the split button's halves

    private sealed class Split : ToolStripSplitButton
    {
        internal int OverrideCalls;

        protected override void OnButtonClick (EventArgs e)
        {
            OverrideCalls++;
            base.OnButtonClick (e);
        }
    }

    private static (Form form, ToolStrip strip) ShownStrip (params ToolStripItem[] items)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 500, Height = 200 };
        var strip = new ToolStrip ();

        foreach (var item in items)
            strip.Items.Add (item);

        form.Controls.Add (strip);
        form.Show ();
        PaintSurface.Render (strip).Dispose ();
        return (form, strip);
    }

    [Fact]
    public void The_button_half_raises_ButtonClick_and_does_not_open_the_menu ()
    {
        var split = new Split { Text = "Save" };
        split.DropDownItems.Add (new ToolStripMenuItem ("Save As"));
        var (form, strip) = ShownStrip (split);

        try {
            var clicks = 0;
            var button_clicks = 0;
            split.Click += (_, _) => clicks++;
            split.ButtonClick += (_, _) => button_clicks++;

            strip.RaiseClick (Left (split.Bounds.Left + 3, split.Bounds.Top + split.Bounds.Height / 2));

            Assert.Equal (1, clicks);
            Assert.Equal (1, button_clicks);
            Assert.Equal (1, split.OverrideCalls);   // through the virtual, so an override takes part
            Assert.False (split.IsDropDownOpened);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void The_arrow_half_opens_the_menu_without_ButtonClick ()
    {
        var split = new ToolStripSplitButton { Text = "Save" };
        split.DropDownItems.Add (new ToolStripMenuItem ("Save As"));
        var (form, strip) = ShownStrip (split);

        try {
            var button_clicks = 0;
            split.ButtonClick += (_, _) => button_clicks++;

            strip.RaiseClick (Left (split.Bounds.Right - 3, split.Bounds.Top + split.Bounds.Height / 2));

            Assert.Equal (0, button_clicks);
            Assert.True (split.IsDropDownOpened);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void The_button_half_clicks_the_DefaultItem_first ()
    {
        var save = new ToolStripMenuItem ("Save");
        var split = new ToolStripSplitButton { Text = "Save" };
        split.DropDownItems.Add (save);
        split.DefaultItem = save;
        var (form, strip) = ShownStrip (split);

        try {
            var order = new System.Collections.Generic.List<string> ();
            save.Click += (_, _) => order.Add ("default");
            split.ButtonClick += (_, _) => order.Add ("button");

            strip.RaiseClick (Left (split.Bounds.Left + 3, split.Bounds.Top + split.Bounds.Height / 2));

            Assert.Equal (new[] { "default", "button" }, order);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void PerformButtonClick_raises_ButtonClick_once_and_PerformClick_not_at_all ()
    {
        var split = new ToolStripSplitButton ();
        var button_clicks = 0;
        split.ButtonClick += (_, _) => button_clicks++;

        split.PerformButtonClick ();
        Assert.Equal (1, button_clicks);

        split.PerformClick ();                     // upstream: Click only
        Assert.Equal (1, button_clicks);
    }

    // ---------------- TSM-17: ToolTip.Show

    private static (Form form, Button button) ShownButton ()
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 300, Height = 200 };
        var button = new Button { Left = 20, Top = 20, Width = 100, Height = 30 };
        form.Controls.Add (button);
        form.Show ();
        return (form, button);
    }

    [Fact]
    public void Show_displays_the_tip_at_once_and_leaves_the_controls_own_tip ()
    {
        var (form, button) = ShownButton ();
        using var tip = new ToolTip { ShowAlways = true, UseAnimation = false };

        try {
            tip.SetToolTip (button, "Saves the file");
            tip.Show ("Required", button, 5, button.Height);

            Assert.True (tip.IsTipShown);
            Assert.Equal ("Required", tip.PopupText);
            Assert.Equal ("Saves the file", tip.GetToolTip (button));
            Assert.False (tip.IsHidePending);       // no duration: it stays until hidden
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void Show_with_a_duration_hides_the_tip_when_it_runs_out ()
    {
        var (form, button) = ShownButton ();
        using var tip = new ToolTip { ShowAlways = true, UseAnimation = false };

        try {
            tip.Show ("Required", button, 2000);

            Assert.True (tip.IsTipShown);
            Assert.True (tip.IsHidePending);

            tip.ElapseHideDelay ();
            Assert.False (tip.IsTipShown);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void Show_rejects_a_negative_duration ()
    {
        var (form, button) = ShownButton ();
        using var tip = new ToolTip ();

        try {
            Assert.Throws<ArgumentOutOfRangeException> (() => tip.Show ("x", button, -1));
        } finally {
            form.Close ();
        }
    }

    // ---------------- TSM-34: the hover delays

    [Fact]
    public void A_hover_tip_waits_for_InitialDelay_and_then_for_AutoPopDelay ()
    {
        var (form, button) = ShownButton ();
        using var tip = new ToolTip { ShowAlways = true, UseAnimation = false, InitialDelay = 700, AutoPopDelay = 9000 };

        try {
            tip.SetToolTip (button, "Saves the file");
            button.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, 0));

            Assert.False (tip.IsTipShown);
            Assert.Equal (700, tip.PendingShowDelay);

            tip.ElapseShowDelay ();
            Assert.True (tip.IsTipShown);
            Assert.True (tip.IsHidePending);       // AutoPopDelay

            tip.ElapseHideDelay ();
            Assert.False (tip.IsTipShown);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void Leaving_before_the_delay_cancels_the_show ()
    {
        var (form, button) = ShownButton ();
        using var tip = new ToolTip { ShowAlways = true, UseAnimation = false };

        try {
            tip.SetToolTip (button, "Saves the file");
            button.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, 0));
            button.RaiseMouseLeave (EventArgs.Empty);

            Assert.Null (tip.PendingShowDelay);
            tip.ElapseShowDelay ();
            Assert.False (tip.IsTipShown);
        } finally {
            form.Close ();
        }
    }

    [Fact]
    public void Moving_on_from_a_shown_tip_waits_ReshowDelay ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 300, Height = 200 };
        var first = new Button { Left = 10, Top = 10, Width = 80, Height = 30 };
        var second = new Button { Left = 100, Top = 10, Width = 80, Height = 30 };
        form.Controls.Add (first);
        form.Controls.Add (second);
        form.Show ();
        using var tip = new ToolTip { ShowAlways = true, UseAnimation = false, InitialDelay = 60000, ReshowDelay = 40 };

        try {
            tip.SetToolTip (first, "One");
            tip.SetToolTip (second, "Two");

            first.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, 0));
            tip.ElapseShowDelay ();
            Assert.True (tip.IsTipShown);

            first.RaiseMouseLeave (EventArgs.Empty);
            second.RaiseMouseEnter (new MouseEventArgs (MouseButtons.None, 0, 5, 5, 0));

            Assert.Equal (40, tip.PendingShowDelay);
        } finally {
            form.Close ();
        }
    }

    // ---------------- TSM-18: item MouseDown / MouseUp

    [Fact]
    public void The_strip_raises_MouseDown_and_MouseUp_on_the_item_item_relative ()
    {
        var first = new ToolStripButton ("One");
        var second = new ToolStripButton ("Two");
        using var strip = StripWith (first, second);
        Point? down = null;
        Point? up = null;
        second.MouseDown += (_, e) => down = e.Location;
        second.MouseUp += (_, e) => up = e.Location;

        var at = new Point (second.Bounds.Left + 3, second.Bounds.Top + 4);
        strip.RaiseMouseDown (Left (at.X, at.Y));
        strip.RaiseMouseUp (Left (at.X, at.Y));

        Assert.True (second.Bounds.Left > 0);       // otherwise item- and strip-relative coincide
        Assert.Equal (new Point (3, 4), down);
        Assert.Equal (new Point (3, 4), up);
    }

    [Fact]
    public void A_release_on_another_strip_item_is_no_MouseUp_and_a_disabled_item_gets_neither ()
    {
        var first = new ToolStripButton ("One");
        var second = new ToolStripButton ("Two") { Enabled = false };
        using var strip = StripWith (first, second);
        var events = 0;
        first.MouseUp += (_, _) => events++;
        second.MouseDown += (_, _) => events++;
        second.MouseUp += (_, _) => events++;

        strip.RaiseMouseDown (Left (Centre (first).X, Centre (first).Y));
        strip.RaiseMouseUp (Left (Centre (second).X, Centre (second).Y));
        strip.RaiseMouseDown (Left (Centre (second).X, Centre (second).Y));
        strip.RaiseMouseUp (Left (Centre (second).X, Centre (second).Y));

        // GUARD, not proof: before the fix nothing raised these at all, so it held then too. It pins
        // the two upstream rules (MouseDownAndUpMustBeInSameItem; a disabled item fires no mouse event).
        Assert.Equal (0, events);
    }

    // ---------------- TSM-20 / TSM-38: the hosted control's Text and Enabled

    [Fact]
    public void A_combo_items_Text_is_the_combos ()
    {
        using var item = new ToolStripComboBox ();

        item.ComboBox.Text = "abc";
        Assert.Equal ("abc", item.Text);

        item.Text = "xyz";
        Assert.Equal ("xyz", item.ComboBox.Text);
    }

    [Fact]
    public void Disabling_a_host_disables_the_hosted_control ()
    {
        using var item = new ToolStripComboBox ();

        item.Enabled = false;
        Assert.False (item.ComboBox.Enabled);

        item.Enabled = true;
        Assert.True (item.ComboBox.Enabled);
    }

    [Fact]
    public void A_strip_holding_a_plain_menu_item_survives_Renderer_and_GetItemAt ()
    {
        HeadlessRenderer.Use ();
        using var strip = new ToolStrip ();
        strip.Items.Add (new MenuSeparatorItem ());
        strip.Items.Add (new MenuItem ("plain"));

        strip.Renderer = new ToolStripProfessionalRenderer ();
        Assert.Null (strip.GetItemAt (new Point (-50, -50)));
        Assert.Null (new ToolStrip.ToolStripAccessibleObject (strip).HitTest (-50, -50));
    }

    // ---------------- TSM-21: close reasons, cancel, AutoClose

    private static (Form form, ContextMenuStrip menu, ToolStripMenuItem item) ShownMenu (Action<ContextMenuStrip>? configure = null)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Width = 400, Height = 300 };
        form.Show ();
        var menu = new ContextMenuStrip ();
        var item = new ToolStripMenuItem ("Bold");
        menu.Items.Add (item);
        configure?.Invoke (menu);
        menu.Show (form.ContentRoot, new Point (10, 10));
        PaintSurface.Render (menu).Dispose ();
        return (form, menu, item);
    }

    [Fact]
    public void Close_reports_CloseCalled_and_a_cancelled_Closing_keeps_the_menu_open ()
    {
        var (form, menu, _) = ShownMenu ();

        try {
            var reasons = new System.Collections.Generic.List<ToolStripDropDownCloseReason> ();
            var cancel = true;
            menu.Closing += (_, e) => { reasons.Add (e.CloseReason); e.Cancel = cancel; };

            menu.Close ();
            Assert.True (menu.Visible);

            cancel = false;
            menu.Close (ToolStripDropDownCloseReason.CloseCalled);
            Assert.False (menu.Visible);

            Assert.Equal (new[] { ToolStripDropDownCloseReason.CloseCalled, ToolStripDropDownCloseReason.CloseCalled }, reasons);
        } finally {
            form.Close ();
            menu.Dispose ();
        }
    }

    [Fact]
    public void An_item_click_reports_ItemClicked_and_cancelling_it_keeps_the_menu_open ()
    {
        var (form, menu, item) = ShownMenu ();

        try {
            ToolStripDropDownCloseReason? reason = null;
            var clicked = 0;
            item.Click += (_, _) => clicked++;
            menu.Closing += (_, e) => { reason = e.CloseReason; e.Cancel = e.CloseReason == ToolStripDropDownCloseReason.ItemClicked; };

            menu.RaiseClick (Left (Centre (item).X, Centre (item).Y));

            Assert.Equal (ToolStripDropDownCloseReason.ItemClicked, reason);
            Assert.Equal (1, clicked);
            Assert.True (menu.Visible);
        } finally {
            form.Close ();
            menu.Dispose ();
        }
    }

    [Fact]
    public void Escape_reports_Keyboard_and_a_click_elsewhere_AppClicked ()
    {
        var (form, menu, _) = ShownMenu ();

        try {
            var reasons = new System.Collections.Generic.List<ToolStripDropDownCloseReason> ();
            menu.Closing += (_, e) => { reasons.Add (e.CloseReason); e.Cancel = true; };

            menu.HandleNavigationKey (Keys.Escape);
            Application.ClosePopups ();

            Assert.Equal (new[] { ToolStripDropDownCloseReason.Keyboard, ToolStripDropDownCloseReason.AppClicked }, reasons);
            Assert.True (menu.Visible);
        } finally {
            form.Close ();
            menu.Dispose ();
        }
    }

    [Fact]
    public void AutoClose_off_keeps_the_menu_open_except_for_Close ()
    {
        var (form, menu, item) = ShownMenu (m => m.AutoClose = false);

        try {
            bool? pre_cancelled = null;
            var closings = 0;
            menu.Closing += (_, e) => { closings++; pre_cancelled ??= e.Cancel; };

            menu.RaiseClick (Left (Centre (item).X, Centre (item).Y));
            Assert.True (menu.Visible);
            Assert.Equal (0, closings);                // an item click does not even ask

            Application.ClosePopups ();
            Assert.True (pre_cancelled);               // a click elsewhere arrives already cancelled
            Assert.True (menu.Visible);

            menu.Close ();
            Assert.False (menu.Visible);
        } finally {
            form.Close ();
            menu.Dispose ();
        }
    }

    // ---------------- TSM-23: a separator on a bar

    [Fact]
    public void A_ToolStripSeparator_on_a_ToolStrip_measures_and_paints_as_a_MenuSeparatorItem ()
    {
        HeadlessRenderer.Use ();
        var tss = new ToolStripSeparator ();
        var msi = new MenuSeparatorItem ();
        using var a = new ToolStrip { Width = 200, Height = 30 };
        using var b = new ToolStrip { Width = 200, Height = 30 };
        a.Items.Add (new ToolStripButton ("One"));
        a.Items.Add (tss);
        b.Items.Add (new ToolStripButton ("One"));
        b.Items.Add (msi);
        PaintSurface.Render (a).Dispose ();
        PaintSurface.Render (b).Dispose ();

        Assert.Equal (((MenuItem) msi).GetPreferredSize (Size.Empty), ((MenuItem) tss).GetPreferredSize (Size.Empty));

        using var pa = PaintSurface.Render (a);
        using var pb = PaintSurface.Render (b);
        Assert.True (SamePixels (pa, pb));
    }

    [Fact]
    public void A_ToolStripSeparator_on_a_MenuStrip_measures_as_a_MenuSeparatorItem ()
    {
        HeadlessRenderer.Use ();
        var tss = new ToolStripSeparator ();
        var msi = new MenuSeparatorItem ();
        using var a = new MenuStrip { Width = 200, Height = 24 };
        using var b = new MenuStrip { Width = 200, Height = 24 };
        a.Items.Add (tss);
        b.Items.Add (msi);
        PaintSurface.Render (a).Dispose ();
        PaintSurface.Render (b).Dispose ();

        Assert.Equal (((MenuItem) msi).GetPreferredSize (Size.Empty), ((MenuItem) tss).GetPreferredSize (Size.Empty));
    }

    [Fact]
    public void A_ToolStripSeparator_does_not_hover ()
    {
        var tss = new ToolStripSeparator ();
        using var strip = StripWith (new ToolStripButton ("One"), tss);

        strip.RaiseMouseMove (new MouseEventArgs (MouseButtons.None, 0, Centre (tss).X, Centre (tss).Y, 0));

        Assert.False (tss.Hovered);
        Assert.False (tss.CanSelect);
    }

    // ---------------- TSM-24: CheckState

    [Fact]
    public void A_menu_items_Indeterminate_round_trips_and_raises_both_events ()
    {
        var item = new ToolStripMenuItem ("Bold");
        var checked_changed = 0;
        var state_changed = 0;
        item.CheckedChanged += (_, _) => checked_changed++;
        item.CheckStateChanged += (_, _) => state_changed++;

        item.CheckState = CheckState.Indeterminate;

        Assert.Equal (CheckState.Indeterminate, item.CheckState);
        Assert.True (item.Checked);
        Assert.True (((MenuItem) item).Checked);    // the renderers' view
        Assert.Equal (1, checked_changed);
        Assert.Equal (1, state_changed);

        item.Checked = false;
        Assert.Equal (CheckState.Unchecked, item.CheckState);
        Assert.Equal (2, state_changed);
    }

    [Fact]
    public void A_buttons_Indeterminate_round_trips_and_raises_both_events ()
    {
        var button = new ToolStripButton ("Bold");
        var checked_changed = 0;
        var state_changed = 0;
        button.CheckedChanged += (_, _) => checked_changed++;
        button.CheckStateChanged += (_, _) => state_changed++;

        button.CheckState = CheckState.Indeterminate;

        Assert.Equal (CheckState.Indeterminate, button.CheckState);
        Assert.True (button.Checked);
        Assert.Equal (1, checked_changed);
        Assert.Equal (1, state_changed);
    }

    private static SKBitmap RenderDropDown (MenuItem item)
    {
        HeadlessRenderer.Use ();
        var menu = new ContextMenu { Width = 160, Height = 40 };
        menu.Items.Add (item);
        PaintSurface.Render (menu).Dispose ();
        return PaintSurface.Render (menu);
    }

    [Fact]
    public void An_indeterminate_menu_item_draws_a_different_glyph_from_a_checked_one ()
    {
        using var indeterminate = RenderDropDown (new ToolStripMenuItem ("Bold") { CheckState = CheckState.Indeterminate });
        using var ticked = RenderDropDown (new ToolStripMenuItem ("Bold") { CheckState = CheckState.Checked });

        Assert.False (SamePixels (indeterminate, ticked));
    }

    // ---------------- TSM-25: IsOnDropDown

    [Fact]
    public void An_item_under_a_menu_bar_item_is_on_a_drop_down ()
    {
        HeadlessRenderer.Use ();
        using var bar = new MenuStrip ();
        var file = new ToolStripMenuItem ("File");
        var open = new ToolStripMenuItem ("Open");
        file.DropDownItems.Add (open);
        bar.Items.Add (file);

        Assert.True (open.IsOnDropDown);
        Assert.False (file.IsOnDropDown);
    }

    // ---------------- TSM-26: PerformClick's gate

    [Fact]
    public void PerformClick_does_nothing_on_a_disabled_or_hidden_strip_item ()
    {
        var item = new ToolStripButton ("Save") { Enabled = false };
        var clicks = 0;
        item.Click += (_, _) => clicks++;

        item.PerformClick ();
        Assert.Equal (0, clicks);

        item.Enabled = true;
        item.Available = false;
        item.PerformClick ();
        Assert.Equal (0, clicks);

        item.Available = true;
        item.PerformClick ();
        Assert.Equal (1, clicks);
    }

    [Fact]
    public void A_legacy_MenuItem_keeps_the_Framework_rule ()
    {
        // GUARD, not proof: the legacy type always clicked, and the fix deliberately leaves it so.
        var item = new MenuItem ("Save") { Enabled = false };
        var clicks = 0;
        item.Click += (_, _) => clicks++;

        item.PerformClick ();
        Assert.Equal (1, clicks);
    }

    // ---------------- TSM-29: TabStop

    [Fact]
    public void Strips_are_not_tab_stops ()
    {
        HeadlessRenderer.Use ();

        Assert.False (new ToolStrip ().TabStop);
        Assert.False (new MenuStrip ().TabStop);
        Assert.False (new StatusStrip ().TabStop);
        Assert.False (new ContextMenuStrip ().TabStop);
    }

    // ---------------- TSM-32: Form.Menu, and RadioCheck on the legacy item

    [Fact]
    public void Assigning_Form_Menu_docks_the_bar_and_replacing_it_takes_the_old_one_off ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form ();
        var first = new MainMenu ();
        var second = new MainMenu ();

        form.Menu = first;
        Assert.Contains (first, form.Controls.Cast<Control> ());
        Assert.Equal (DockStyle.Top, first.Dock);

        form.Menu = second;
        Assert.DoesNotContain (first, form.Controls.Cast<Control> ());
        Assert.Contains (second, form.Controls.Cast<Control> ());

        form.Menu = null;
        Assert.DoesNotContain (second, form.Controls.Cast<Control> ());
    }

    [Fact]
    public void A_legacy_RadioCheck_item_draws_a_bullet_rather_than_a_tick ()
    {
        using var radio = RenderDropDown (new MenuItem ("Large") { Checked = true, RadioCheck = true });
        using var tick = RenderDropDown (new MenuItem ("Large") { Checked = true });

        Assert.False (SamePixels (radio, tick));
    }

    // ---------------- TSM-35: DropDownOpened only when something opened

    [Fact]
    public void A_leaf_item_raises_DropDownOpening_but_not_DropDownOpened ()
    {
        HeadlessRenderer.Use ();
        using var form = new Form { Width = 400, Height = 200 };
        var bar = new MenuStrip ();
        var leaf = new ToolStripMenuItem ("Help");
        var file = new ToolStripMenuItem ("File");
        file.DropDownItems.Add (new ToolStripMenuItem ("Open"));
        bar.Items.Add (leaf);
        bar.Items.Add (file);
        form.Controls.Add (bar);
        form.Show ();

        try {
            var leaf_opened = 0;
            var file_opened = 0;
            leaf.DropDownOpened += (_, _) => leaf_opened++;
            file.DropDownOpened += (_, _) => file_opened++;

            leaf.ShowDropDown ();
            file.ShowDropDown ();

            Assert.Equal (0, leaf_opened);
            Assert.Equal (1, file_opened);              // the control: a real open still reports
        } finally {
            form.Close ();
        }
    }
}
