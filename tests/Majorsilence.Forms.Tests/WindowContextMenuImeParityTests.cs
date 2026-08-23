using System.Drawing;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A window's context menu and IME mode. WindowBase.ContextMenuStrip was a STORED value nothing read,
    // so a form with a context menu assigned in the designer showed nothing when right-clicked while its
    // child controls' menus worked -- which reads as the form's menu being broken rather than absent.
    // Both now forward to the root adapter, which is the window's client surface and already knows how to
    // open a context menu on right-click.
    public class WindowContextMenuImeParityTests
    {
        [Fact]
        public void Right_clicking_a_forms_background_opens_its_context_menu ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { ClientSize = new Size (300, 200) };
            var menu = new ContextMenuStrip ();
            menu.Items.Add (new ToolStripMenuItem { Text = "Refresh" });
            form.ContextMenuStrip = menu;

            form.Show ();
            HeadlessRenderer.Click (form, 150, 100, MouseButtons.Right);

            Assert.True (menu.Visible, "Right-clicking the form's background did not open its menu.");
        }

        [Fact]
        public void ContextMenuStrip_and_ContextMenu_are_the_same_assignment ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var menu = new ContextMenuStrip ();
            form.ContextMenuStrip = menu;

            // As on Control, where ContextMenuStrip is an alias for ContextMenu -- so the legacy and
            // modern property names cannot disagree about which menu a window has.
            Assert.Same (menu, form.ContextMenu);
            Assert.Same (menu, form.ContextMenuStrip);
        }

        [Fact]
        public void Assigning_a_context_menu_raises_both_changed_events ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();
            var contextMenuChanged = 0;
            var stripChanged = 0;
            form.ContextMenuChanged += (_, _) => contextMenuChanged++;
            form.ContextMenuStripChanged += (_, _) => stripChanged++;

            form.ContextMenuStrip = new ContextMenuStrip ();

            Assert.Equal (1, contextMenuChanged);
            Assert.Equal (1, stripChanged);
        }

        [Fact]
        public void ImeMode_round_trips_and_resets_to_its_default ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form ();

            form.ImeMode = ImeMode.Katakana;
            Assert.Equal (ImeMode.Katakana, form.ImeMode);

            var raised = 0;
            form.ImeModeChanged += (_, _) => raised++;

            form.ResetImeMode ();

            Assert.Equal (ImeMode.NoControl, form.ImeMode);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_windows_ImeMode_is_the_one_its_children_see ()
        {
            HeadlessRenderer.Use ();

            // Forwarded to the root adapter so children inherit it through the same chain they inherit a
            // parent control's, rather than the window holding a second, unrelated value.
            using var form = new Form ();
            form.ImeMode = ImeMode.Hiragana;

            Assert.Equal (ImeMode.Hiragana, form.Controls.Owner.ImeMode);
        }

        [Fact]
        public void PrintPreviewDialog_keeps_its_own_ImeMode_default_without_shadowing ()
        {
            HeadlessRenderer.Use ();

            // It declared its own ImeMode purely to hold a different default; the default now comes from
            // its constructor, so there is one property.
            using var dialog = new PrintPreviewDialog ();

            Assert.Equal (ImeMode.Inherit, dialog.ImeMode);
        }
    }
}
