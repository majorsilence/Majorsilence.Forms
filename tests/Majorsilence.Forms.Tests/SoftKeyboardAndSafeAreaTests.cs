using System.Drawing;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Phase 1 of the mobile/WASM work: the framework tells the backend when a text control is focused so a
// single-view backend can raise the on-screen keyboard, and the backend pushes device safe-area insets
// back in so a Form keeps its docked/anchored children clear of the status bar / notch. Both seams are
// exercised here on the Headless backend (which records the keyboard request and lets a test inject a
// safe area) -- the real behaviour needs a device, but the wiring does not.
public class SoftKeyboardAndSafeAreaTests
{
    private static Form ShowForm (int w = 400, int h = 300)
    {
        HeadlessRenderer.Use ();
        var form = new Form { Size = new Size (w, h) };
        form.Show ();
        return form;
    }

    private static HeadlessWindowHost Host (Form form) => (HeadlessWindowHost) form.Backend;

    // ── Soft keyboard ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Focusing_a_TextBox_asks_the_backend_to_show_the_keyboard ()
    {
        using var form = ShowForm ();
        var box = new TextBox { Size = new Size (120, 24), Location = new Point (10, 10) };
        form.Controls.Add (box);

        box.Select ();

        Assert.True (Host (form).TextInputActive);
        Assert.Equal (TextInputKind.Normal, Host (form).LastTextInputKind);
    }

    [Fact]
    public void Moving_focus_off_the_TextBox_asks_the_backend_to_hide_the_keyboard ()
    {
        using var form = ShowForm ();
        var box = new TextBox { Size = new Size (120, 24), Location = new Point (10, 10) };
        var button = new Button { Size = new Size (80, 24), Location = new Point (10, 50) };
        form.Controls.Add (box);
        form.Controls.Add (button);

        box.Select ();
        button.Select ();

        Assert.False (Host (form).TextInputActive);
    }

    [Fact]
    public void A_read_only_TextBox_does_not_raise_the_keyboard ()
    {
        using var form = ShowForm ();
        var box = new TextBox { ReadOnly = true, Size = new Size (120, 24), Location = new Point (10, 10) };
        form.Controls.Add (box);

        box.Select ();

        Assert.False (Host (form).TextInputActive);
    }

    [Fact]
    public void A_multiline_TextBox_reports_the_Multiline_kind ()
    {
        using var form = ShowForm ();
        var box = new TextBox { Multiline = true, Size = new Size (120, 60), Location = new Point (10, 10) };
        form.Controls.Add (box);

        box.Select ();

        Assert.Equal (TextInputKind.Multiline, Host (form).LastTextInputKind);
    }

    [Fact]
    public void A_password_TextBox_reports_the_Password_kind ()
    {
        using var form = ShowForm ();
        var box = new TextBox { PasswordChar = '*', Size = new Size (120, 24), Location = new Point (10, 10) };
        form.Controls.Add (box);

        box.Select ();

        Assert.Equal (TextInputKind.Password, Host (form).LastTextInputKind);
    }

    [Fact]
    public void Focusing_a_non_text_control_never_activates_text_input ()
    {
        using var form = ShowForm ();
        var button = new Button { Size = new Size (80, 24), Location = new Point (10, 10) };
        form.Controls.Add (button);

        button.Select ();

        Assert.Equal (0, Host (form).TextInputActivationCount);
    }

    // ── Safe area ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_docked_child_insets_by_the_safe_area ()
    {
        using var form = ShowForm (400, 600);
        var panel = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add (panel);
        form.PerformLayout ();
        var before = panel.Bounds;

        form.HandleSafeAreaChanged (new Padding (0, 40, 0, 24));

        Assert.Equal (before.Y + 40, panel.Bounds.Y);
        Assert.Equal (before.Height - 40 - 24, panel.Bounds.Height);
        Assert.Equal (new Padding (0, 40, 0, 24), form.SafeAreaPadding);
    }

    [Fact]
    public void Clearing_the_safe_area_restores_the_full_client_layout ()
    {
        using var form = ShowForm (400, 600);
        var panel = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add (panel);
        form.PerformLayout ();
        var full = panel.Bounds;

        form.HandleSafeAreaChanged (new Padding (10, 40, 10, 24));
        form.HandleSafeAreaChanged (Padding.Empty);

        Assert.Equal (full, panel.Bounds);
    }

    // ── ScrollControlIntoView ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ScrollControlIntoView_scrolls_an_off_screen_child_into_the_viewport ()
    {
        using var form = ShowForm (300, 200);
        var panel = new Panel { Size = new Size (200, 150), Location = new Point (0, 0), AutoScroll = true };
        form.Controls.Add (panel);
        var faraway = new Button { Size = new Size (80, 24), Location = new Point (10, 600) };
        panel.Controls.Add (faraway);
        form.PerformLayout ();

        Assert.Equal (0, -panel.AutoScrollPosition.Y);

        faraway.ScrollControlIntoView (faraway);

        Assert.True (-panel.AutoScrollPosition.Y > 0, "the panel should have scrolled down to reveal the button");
    }
}
