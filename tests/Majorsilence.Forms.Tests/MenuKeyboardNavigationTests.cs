using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The remaining half of TSM-13: keyboard navigation of a menu that is on screen. W1.3 (Phase 1)
    // closed the accelerator half -- ShortcutKeys, the legacy Shortcut, and Alt+letter reaching an
    // item -- but there was no OnKeyDown in MenuBase, Menu, MenuDropDown or ContextMenu at all, so
    // nothing routed keys to a menu: F10 did not reach the bar, the arrows did not walk it, Enter did
    // not activate, and an accidentally opened menu could not be dismissed with Escape.
    [Collection ("Headless")]
    public class MenuKeyboardNavigationTests
    {
        private static (Form form, MenuStrip strip, ToolStripMenuItem file, ToolStripMenuItem edit) Barred ()
        {
            HeadlessRenderer.Use ();
            var form = new Form { Width = 400, Height = 300 };
            var strip = new MenuStrip { Width = 400, Height = 24 };
            var file = new ToolStripMenuItem { Text = "&File" };
            var edit = new ToolStripMenuItem { Text = "&Edit" };

            file.DropDownItems.Add (new ToolStripMenuItem { Text = "&Open" });
            file.DropDownItems.Add (new ToolStripMenuItem { Text = "&Save" });
            edit.DropDownItems.Add (new ToolStripMenuItem { Text = "&Copy" });

            strip.Items.Add (file);
            strip.Items.Add (edit);
            form.Controls.Add (strip);
            form.MainMenuStrip = strip;
            form.Show ();

            return (form, strip, file, edit);
        }

        [Fact]
        public void Alt_and_a_menus_access_key_opens_that_menu ()
        {
            // The finding's own test.
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);

                Assert.Same (file, strip.SelectedItem);
                Assert.True (file.IsDropDownOpened);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Escape_closes_an_open_menu ()
        {
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);
                Assert.True (file.IsDropDownOpened);

                HeadlessRenderer.KeyDown (form, Keys.Escape);

                Assert.False (file.IsDropDownOpened);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void F10_puts_the_selection_on_the_menu_bar ()
        {
            // Upstream's F10 highlights the first menu WITHOUT opening it. Here selecting a menu item
            // is the same state as opening it -- MenuItem.Selected's setter calls ShowDropDown, which
            // is how click-to-open works -- so entering menu mode opens the first menu too. Splitting
            // "highlighted" from "dropped down" would change every mouse path into a menu, so this is
            // a documented deviation rather than a thing to assert falsely.
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.F10);

                Assert.Same (file, strip.SelectedItem);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_bare_Alt_enters_menu_mode_too ()
        {
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                // Keys.Menu is the Alt key itself, as opposed to the Alt modifier bit.
                HeadlessRenderer.KeyDown (form, Keys.Menu);

                Assert.Same (file, strip.SelectedItem);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Right_and_Left_walk_along_the_bar ()
        {
            var (form, strip, file, edit) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.F10);

                HeadlessRenderer.KeyDown (form, Keys.Right);
                Assert.Same (edit, strip.SelectedItem);
                Assert.False (file.IsDropDownOpened);

                HeadlessRenderer.KeyDown (form, Keys.Left);
                Assert.Same (file, strip.SelectedItem);

                // And it wraps rather than stopping at the end.
                HeadlessRenderer.KeyDown (form, Keys.Left);
                Assert.Same (edit, strip.SelectedItem);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Walking_the_bar_with_a_menu_open_keeps_the_next_one_open ()
        {
            // What makes Left/Right feel like moving between menus rather than closing them.
            var (form, strip, file, edit) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);
                Assert.True (file.IsDropDownOpened);

                HeadlessRenderer.KeyDown (form, Keys.Right);

                Assert.Same (edit, strip.SelectedItem);
                Assert.True (edit.IsDropDownOpened);
                Assert.False (file.IsDropDownOpened);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Down_from_the_bar_opens_the_menu_and_selects_its_first_item ()
        {
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.F10);
                HeadlessRenderer.KeyDown (form, Keys.Down);

                Assert.True (file.IsDropDownOpened);
                Assert.Equal ("&Open", SelectedInDropDown (file)?.Text);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Down_and_Up_move_within_the_open_menu ()
        {
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);
                HeadlessRenderer.KeyDown (form, Keys.Down);
                Assert.Equal ("&Open", SelectedInDropDown (file)?.Text);

                HeadlessRenderer.KeyDown (form, Keys.Down);
                Assert.Equal ("&Save", SelectedInDropDown (file)?.Text);

                HeadlessRenderer.KeyDown (form, Keys.Up);
                Assert.Equal ("&Open", SelectedInDropDown (file)?.Text);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Enter_activates_the_selected_item ()
        {
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                var clicks = 0;
                ((ToolStripMenuItem)file.DropDownItems[0]).Click += (_, _) => clicks++;

                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);
                HeadlessRenderer.KeyDown (form, Keys.Down);
                HeadlessRenderer.KeyDown (form, Keys.Enter);

                Assert.Equal (1, clicks);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void A_disabled_item_is_skipped_by_the_arrows_and_refuses_Enter ()
        {
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                var open = (ToolStripMenuItem)file.DropDownItems[0];
                var clicks = 0;
                open.Enabled = false;
                open.Click += (_, _) => clicks++;

                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);
                HeadlessRenderer.KeyDown (form, Keys.Down);

                // Skipped over, so the first stop is the enabled item below it.
                Assert.Equal ("&Save", SelectedInDropDown (file)?.Text);

                HeadlessRenderer.KeyDown (form, Keys.Enter);

                Assert.Equal (0, clicks);
            } finally {
                form.Close ();
            }
        }

        // NOT TESTED HERE, deliberately: Right opening a nested submenu, and Escape closing one
        // level back out of it. Both are implemented (see MenuBase.HandleNavigationKey and
        // IsNestedDropDown), but they cannot be evaluated on this backend -- opening a second,
        // nested popup while the first is up tears the whole menu down through
        // Application.ScheduleClosePopupsOnDeactivate, because the newly shown popup does not report
        // itself active without a real window server. A test here would measure the backend rather
        // than the navigation, and asserting the teardown as correct would be worse: it would pin
        // behaviour nobody wants. Flagged in docs/behaviour-gap/toolstrip.md for a GUI check.

        [Fact]
        public void An_open_menu_takes_the_arrows_away_from_a_focused_text_box ()
        {
            // The reason this routing sits ahead of the pre-processing chain: with a menu up, a control
            // that wants the arrows must not eat them.
            var (form, strip, file, _) = Barred ();
            using var _form = form;

            try {
                var box = new TextBox { Width = 100, Height = 24, Text = "abc" };
                form.Controls.Add (box);
                box.Select ();
                box.Select (0, 0);

                HeadlessRenderer.KeyDown (form, Keys.Alt | Keys.F);
                HeadlessRenderer.KeyDown (form, Keys.Down);

                Assert.Equal ("&Open", SelectedInDropDown (file)?.Text);
                Assert.Equal (0, box.SelectionStart);      // the caret did not move
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void With_no_menu_open_the_arrows_still_belong_to_the_focused_control ()
        {
            // GUARD, not proof: nothing routed keys to menus before, so no previous version could fail
            // this. It pins that the new routing only claims keys while a menu is actually up.
            var (form, strip, _, _) = Barred ();
            using var _form = form;

            try {
                var box = new TextBox { Width = 100, Height = 24, Text = "abc" };
                form.Controls.Add (box);
                box.Select ();
                box.Select (0, 0);

                HeadlessRenderer.KeyDown (form, Keys.Right);

                Assert.Equal (1, box.SelectionStart);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void An_active_menu_does_not_swallow_another_windows_Escape ()
        {
            // The regression this pins failed CI on all three platforms, in a DIFFERENT KeyboardChainTests
            // case on each, because which test noticed depended on collection ordering -- it read as a
            // flake. Application.ActiveMenu is process-global, so once any test opened a menu, every
            // later Escape anywhere in the process was claimed by it and the pre-processing chain
            // (ProcessCmdKey, ProcessDialogKey, Form.CancelButton) never ran. Deterministic here:
            // one form holds an open menu while a second form is sent the key.
            var (owner, _, file, _) = Barred ();
            using var _owner = owner;

            try {
                HeadlessRenderer.KeyDown (owner, Keys.Alt | Keys.F);
                Assert.True (file.IsDropDownOpened);            // menu mode is genuinely entered

                using var other = new Form { Width = 300, Height = 200 };
                var cancel = new Button { Text = "Cancel", Width = 80, Height = 24 };
                other.Controls.Add (cancel);
                other.CancelButton = cancel;
                var cancelled = 0;
                cancel.Click += (_, _) => cancelled++;
                other.Show ();

                try {
                    HeadlessRenderer.KeyDown (other, Keys.Escape);

                    Assert.Equal (1, cancelled);
                } finally {
                    other.Close ();
                }
            } finally {
                owner.Close ();
            }
        }

        [Fact]
        public void A_menu_that_was_never_opened_does_not_claim_keys ()
        {
            // GUARD, not proof: a menu that was never opened is not Application.ActiveMenu either, so
            // the routing never consults it and no version of this code could fail this. It is here
            // because the tempting extra defence -- refusing keys unless MenuBase.IsActivated -- is
            // unreachable for the same reason, and was removed rather than left as unprovable code.
            var (form, strip, _, _) = Barred ();
            using var _form = form;

            try {
                Assert.False (strip.IsActivated);

                var box = new TextBox { Width = 100, Height = 24, Text = "abc" };
                form.Controls.Add (box);
                box.Select ();
                box.Select (0, 0);

                HeadlessRenderer.KeyDown (form, Keys.Right);

                Assert.Equal (1, box.SelectionStart);
            } finally {
                form.Close ();
            }
        }

        private static MenuItem? SelectedInDropDown (ToolStripMenuItem item)
            => item.DropDownItems.Cast<MenuItem> ().FirstOrDefault (i => i.Selected);
    }
}
