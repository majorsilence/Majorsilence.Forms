using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Menu shortcuts and access keys, the second half of the keyboard chain (W1.3).
//
// Both halves were stored-only before this: ToolStripMenuItem.ShortcutKeys and the legacy
// MenuItem.Shortcut held a value nothing ever compared a keystroke against, and ProcessMnemonic was a
// `=> false` stub, so `&File` underlined the F and Alt+F did nothing. See findings TSM-02 (rated P0),
// FRM-09 and SMP-10.
public class MenuShortcutTests
{
    private static Form ShowForm (Form form)
    {
        HeadlessRenderer.Use ();
        form.Size = new Size (300, 200);
        form.Show ();
        return form;
    }

    private static (Form Form, ToolStripMenuItem Item, int[] Clicks) FormWithSaveItem (Keys shortcut)
    {
        var form = ShowForm (new Form ());
        var strip = new MenuStrip ();
        var file = new ToolStripMenuItem { Text = "&File" };
        var save = new ToolStripMenuItem { Text = "&Save", ShortcutKeys = shortcut };

        var clicks = new int[1];
        save.Click += (_, _) => clicks[0]++;

        file.DropDownItems.Add (save);
        strip.Items.Add (file);
        form.Controls.Add (strip);
        form.MainMenuStrip = strip;

        return (form, save, clicks);
    }

    [Fact]
    public void A_menu_item_shortcut_fires_its_Click ()
    {
        var (form, _, clicks) = FormWithSaveItem (Keys.Control | Keys.S);
        using var _form = form;

        HeadlessRenderer.KeyDown (form, Keys.Control | Keys.S);

        Assert.Equal (1, clicks[0]);
    }

    [Fact]
    public void A_shortcut_fires_even_while_a_TextBox_has_focus ()
    {
        // The reason shortcuts run in ProcessCmdKey rather than after IsInputKey: Ctrl+S has to reach
        // the menu from inside a text box, which is where the user usually is when they press it.
        var (form, _, clicks) = FormWithSaveItem (Keys.Control | Keys.S);
        using var _form = form;

        var text = new TextBox { Size = new Size (100, 20) };
        form.Controls.Add (text);
        text.Select ();

        HeadlessRenderer.KeyDown (form, Keys.Control | Keys.S);

        Assert.Equal (1, clicks[0]);
    }

    [Fact]
    public void A_shortcut_suppresses_the_KeyDown_that_triggered_it ()
    {
        var (form, _, clicks) = FormWithSaveItem (Keys.Control | Keys.S);
        using var _form = form;

        var text = new TextBox { Size = new Size (100, 20) };
        form.Controls.Add (text);
        text.Select ();

        using var recorder = EventRecorder.For (text, "KeyDown");
        var handled = HeadlessRenderer.KeyDown (form, Keys.Control | Keys.S);

        Assert.True (handled);
        Assert.Equal (1, clicks[0]);
        Assert.Empty (recorder.Entries);
    }

    [Fact]
    public void A_different_key_does_not_fire_the_shortcut ()
    {
        var (form, _, clicks) = FormWithSaveItem (Keys.Control | Keys.S);
        using var _form = form;

        HeadlessRenderer.KeyDown (form, Keys.Control | Keys.O);
        HeadlessRenderer.KeyDown (form, Keys.S);

        Assert.Equal (0, clicks[0]);
    }

    [Fact]
    public void A_disabled_item_does_not_fire_its_shortcut ()
    {
        var (form, save, clicks) = FormWithSaveItem (Keys.Control | Keys.S);
        using var _form = form;

        save.Enabled = false;
        HeadlessRenderer.KeyDown (form, Keys.Control | Keys.S);

        Assert.Equal (0, clicks[0]);
    }

    [Fact]
    public void The_legacy_MenuItem_Shortcut_spelling_works_too ()
    {
        // Shortcut.CtrlS is 131155, numerically identical to Keys.Control | Keys.S, which is what lets
        // one comparison serve both the modern and the legacy property.
        using var form = ShowForm (new Form ());
        var strip = new MenuStrip ();
        var file = new ToolStripMenuItem { Text = "&File" };
        var save = new ToolStripMenuItem { Text = "&Save", Shortcut = Shortcut.CtrlS };

        var clicks = 0;
        save.Click += (_, _) => clicks++;

        file.DropDownItems.Add (save);
        strip.Items.Add (file);
        form.Controls.Add (strip);

        HeadlessRenderer.KeyDown (form, Keys.Control | Keys.S);

        Assert.Equal (1, clicks);
    }

    // ── Access keys ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Alt_plus_a_buttons_access_key_clicks_it ()
    {
        using var form = ShowForm (new Form ());
        var clicks = 0;
        var button = new Button { Text = "&Save", Size = new Size (80, 24) };
        button.Click += (_, _) => clicks++;
        form.Controls.Add (button);

        HeadlessRenderer.KeyDown (form, Keys.S | Keys.Alt);

        Assert.Equal (1, clicks);
    }

    [Fact]
    public void An_access_key_reaches_a_button_from_inside_a_TextBox ()
    {
        using var form = ShowForm (new Form ());
        var clicks = 0;
        var button = new Button { Text = "&Save", Size = new Size (80, 24) };
        button.Click += (_, _) => clicks++;
        form.Controls.Add (button);

        var text = new TextBox { Size = new Size (100, 20) };
        form.Controls.Add (text);
        text.Select ();

        HeadlessRenderer.KeyDown (form, Keys.S | Keys.Alt);

        Assert.Equal (1, clicks);
    }

    [Fact]
    public void UseMnemonic_false_turns_the_access_key_off ()
    {
        using var form = ShowForm (new Form ());
        var clicks = 0;
        var button = new Button { Text = "&Save", UseMnemonic = false, Size = new Size (80, 24) };
        button.Click += (_, _) => clicks++;
        form.Controls.Add (button);

        HeadlessRenderer.KeyDown (form, Keys.S | Keys.Alt);

        Assert.Equal (0, clicks);
    }

    [Fact]
    public void A_disabled_button_does_not_answer_its_access_key ()
    {
        using var form = ShowForm (new Form ());
        var clicks = 0;
        var button = new Button { Text = "&Save", Enabled = false, Size = new Size (80, 24) };
        button.Click += (_, _) => clicks++;
        form.Controls.Add (button);

        HeadlessRenderer.KeyDown (form, Keys.S | Keys.Alt);

        Assert.Equal (0, clicks);
    }

    [Fact]
    public void A_menu_access_key_wins_over_a_button_with_the_same_letter ()
    {
        // WinForms offers the character to the menus first, so Alt+F is the File menu even when a
        // button is captioned "&Format".
        using var form = ShowForm (new Form ());
        var strip = new MenuStrip ();
        var file = new ToolStripMenuItem { Text = "&File" };
        var menuClicks = 0;
        file.Click += (_, _) => menuClicks++;
        strip.Items.Add (file);
        form.Controls.Add (strip);

        var buttonClicks = 0;
        var button = new Button { Text = "&Format", Size = new Size (80, 24) };
        button.Click += (_, _) => buttonClicks++;
        form.Controls.Add (button);

        HeadlessRenderer.KeyDown (form, Keys.F | Keys.Alt);

        Assert.Equal (1, menuClicks);
        Assert.Equal (0, buttonClicks);
    }
}
