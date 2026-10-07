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
[Collection ("Headless")]
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

    [Theory]
    [InlineData (TextInputKind.Number)]
    [InlineData (TextInputKind.Email)]
    [InlineData (TextInputKind.Url)]
    [InlineData (TextInputKind.Phone)]
    public void An_InputKind_on_a_TextBox_is_what_the_backend_is_asked_for (TextInputKind kind)
    {
        // A four-digit PIN field, an e-mail box and so on could not ask for their keyboard at all: every TextBox brought up the full letter
        // layout (#368).
        using var form = ShowForm ();
        var box = new TextBox { InputKind = kind, Size = new Size (120, 24), Location = new Point (10, 10) };
        form.Controls.Add (box);

        box.Select ();

        Assert.Equal (kind, Host (form).LastTextInputKind);
    }

    [Fact]
    public void A_TextBox_asks_for_the_Normal_keyboard_unless_told_otherwise ()
    {
        using var box = new TextBox ();

        Assert.Equal (TextInputKind.Normal, box.InputKind);
    }

    [Fact]
    public void A_masked_numeric_TextBox_asks_for_the_Pin_keyboard ()
    {
        // A PIN is both: a number pad, and no suggestions or previews. Neither Number nor Password alone says it.
        using var form = ShowForm ();
        var box = new TextBox { PasswordChar = '*', InputKind = TextInputKind.Number, Size = new Size (120, 24), Location = new Point (10, 10) };
        form.Controls.Add (box);

        box.Select ();

        Assert.Equal (TextInputKind.Pin, Host (form).LastTextInputKind);
    }

    [Fact]
    public void Masking_outranks_a_text_hint_and_Multiline_outranks_both ()
    {
        using var form = ShowForm ();
        var masked = new TextBox { PasswordChar = '*', InputKind = TextInputKind.Email, Size = new Size (120, 24), Location = new Point (10, 10) };
        var multi = new TextBox { Multiline = true, InputKind = TextInputKind.Number, Size = new Size (120, 60), Location = new Point (10, 50) };
        form.Controls.Add (masked);
        form.Controls.Add (multi);

        masked.Select ();
        Assert.Equal (TextInputKind.Password, Host (form).LastTextInputKind);

        multi.Select ();
        Assert.Equal (TextInputKind.Multiline, Host (form).LastTextInputKind);
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

    // ── Keyboard shrinks the client area ─────────────────────────────────────────────────────────

    [Fact]
    public void An_open_keyboard_shrinks_a_docked_child_and_closing_it_restores_the_layout ()
    {
        using var form = ShowForm (400, 600);
        var panel = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add (panel);
        form.PerformLayout ();
        var full = panel.Bounds;

        form.HandleInputPaneChanged (new Rectangle (0, 600 - 250, 400, 250));
        Assert.Equal (full.Height - 250, panel.Bounds.Height);

        form.HandleInputPaneChanged (Rectangle.Empty);
        Assert.Equal (full, panel.Bounds);
    }

    [Fact]
    public void A_field_at_the_end_of_a_scrolling_panel_can_be_lifted_clear_of_the_keyboard ()
    {
        using var form = ShowForm (300, 600);
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        form.Controls.Add (panel);
        var last = new TextBox { Size = new Size (200, 24), Location = new Point (10, 780) };
        panel.Controls.Add (last);
        form.PerformLayout ();
        last.Select ();

        form.HandleInputPaneChanged (new Rectangle (0, 300, 300, 300));

        // The viewport now ends where the keyboard begins, and the field sits inside it. A scrolled panel moves its children,
        // so Top is already where the field is drawn; adding the scroll offset again put the field in content coordinates.
        var top = last.Top;
        Assert.True (top + last.Height <= panel.ClientSize.Height, "the field should be above the keyboard");
        Assert.True (top >= 0);
    }
}
