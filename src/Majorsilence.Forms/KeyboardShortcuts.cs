using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Resolves menu shortcuts and access keys for the keyboard pre-processing chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream this work is split between <c>ToolStripManager.ProcessCmdKey</c> (which keeps a static
    /// list of every live <c>ToolStrip</c> and asks each one) and <c>ContainerControl</c>'s mnemonic
    /// walk. There is no such registry here, so both start from the form and walk what it owns: the
    /// menu strips in its control tree, plus <see cref="Form.MainMenuStrip"/> if it was assigned
    /// without being parented.
    /// </para>
    /// <para>
    /// Both halves were previously unreachable. <c>ShortcutKeys</c> and the legacy
    /// <c>MenuItem.Shortcut</c> stored a value nothing consulted, and <c>ProcessMnemonic</c> was a
    /// <c>=&gt; false</c> stub, so <c>&amp;File</c> plus Alt+F did nothing anywhere in the framework
    /// (TSM-02, FRM-09, SMP-10).
    /// </para>
    /// </remarks>
    internal static class KeyboardShortcuts
    {
        /// <summary>
        /// Finds the menu item whose shortcut is <paramref name="keyData"/> and clicks it.
        /// </summary>
        /// <returns>True when an item claimed the key.</returns>
        internal static bool TryInvokeMenuShortcut (Form form, Keys keyData)
        {
            if (keyData == Keys.None)
                return false;

            foreach (var item in MenuItemsOf (form)) {
                if (!IsEnabledAndVisible (item))
                    continue;

                // ToolStripMenuItem.ShortcutKeys is already a Keys combination. The legacy
                // MenuItem.Shortcut is a separate enum whose members carry the identical numbers
                // (Shortcut.CtrlA is 131137, which is Keys.Control | Keys.A), so the same comparison
                // serves both spellings.
                var shortcut = item switch {
                    ToolStripMenuItem menu when menu.ShortcutKeys != Keys.None => menu.ShortcutKeys,
                    _ when item.Shortcut != Shortcut.None => (Keys) item.Shortcut,
                    _ => Keys.None,
                };

                if (shortcut == Keys.None || shortcut != keyData)
                    continue;

                item.PerformClick ();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Offers an access key to the form's menus and then to its controls, clicking or focusing the
        /// first that claims it.
        /// </summary>
        /// <remarks>
        /// Menus first, matching WinForms: Alt+F belongs to the <c>&amp;File</c> menu even when a
        /// button on the form is captioned <c>&amp;Format</c>.
        /// </remarks>
        /// <summary>
        /// Handles F10 and a bare Alt: both move the selection onto the form's menu bar without opening
        /// anything, which is how menu mode is entered from the keyboard.
        /// </summary>
        /// <remarks>Upstream does this in <c>ToolStripManager.ProcessMenuKey</c>. Nothing here did it at
        /// all, so a keyboard-only user could not reach the menus (finding <c>TSM-13</c>).</remarks>
        internal static bool TryEnterMenuMode (Form form)
        {
            foreach (var menu in MenusOf (form)) {
                if (!menu.IsTopLevelMenuBar)
                    continue;

                var first = menu.RootItems.FirstOrDefault (IsEnabledAndVisible);

                if (first is null)
                    continue;

                menu.SelectItemFromKeyboard (first);
                return true;
            }

            return false;
        }

        internal static bool TryInvokeMnemonic (Form form, char charCode)
        {
            foreach (var item in MenuItemsOf (form)) {
                if (!IsEnabledAndVisible (item) || !Control.IsMnemonic (charCode, item.Text ?? string.Empty))
                    continue;

                // An item with a sub-menu OPENS it and takes the selection with it, which is what Alt+F
                // does to a File menu upstream (ToolStripMenuItem.ProcessMnemonic). Clicking it instead
                // fired the item's own Click -- rarely what a menu header has a handler for -- and left
                // the menu closed, so there was nothing to navigate with the keys (finding TSM-13).
                if (item.HasItems) {
                    if (item.OwnerControl is MenuBase owner)
                        owner.SelectItemFromKeyboard (item);

                    item.ShowDropDown ();
                    return true;
                }

                item.PerformClick ();
                return true;
            }

            return form.Controls.Cast<Control> ().Any (child => ProcessMnemonicIn (child, charCode));
        }

        // Depth-first over the control tree, matching the order a container offers a mnemonic to its
        // children. A control that claims it stops the walk.
        private static bool ProcessMnemonicIn (Control control, char charCode)
        {
            if (!control.Visible || !control.Enabled)
                return false;

            if (control.RaiseProcessMnemonic (charCode))
                return true;

            return control.Controls.Cast<Control> ().Any (child => ProcessMnemonicIn (child, charCode));
        }

        // Every menu item reachable from the form, drop-downs included, breadth-first from each strip.
        private static IEnumerable<MenuItem> MenuItemsOf (Form form)
        {
            foreach (var menu in MenusOf (form)) {
                foreach (var item in Flatten (menu.RootItems))
                    yield return item;
            }
        }

        private static IEnumerable<MenuBase> MenusOf (Form form)
        {
            var seen = new HashSet<MenuBase> ();

            if (form.MainMenuStrip is { } main && seen.Add (main))
                yield return main;

            foreach (var menu in Descendants (form.Controls).OfType<MenuBase> ()) {
                if (seen.Add (menu))
                    yield return menu;
            }
        }

        private static IEnumerable<Control> Descendants (IEnumerable<Control> controls)
        {
            foreach (var control in controls) {
                yield return control;

                foreach (var descendant in Descendants (control.Controls))
                    yield return descendant;
            }
        }

        private static IEnumerable<MenuItem> Flatten (MenuItemCollection items)
        {
            foreach (MenuItem item in items) {
                yield return item;

                foreach (var child in Flatten (item.Items))
                    yield return child;
            }
        }

        /// <summary>
        /// Whether the item can be activated. A shortcut or access key on a disabled or hidden item
        /// must not fire — the same rule the visible menu obeys when it is clicked.
        /// </summary>
        /// <remarks>
        /// The <see cref="ToolStripItem"/> cast is working around a real bug rather than expressing a
        /// real distinction. <c>ToolStripItem</c> re-declares <c>Enabled</c> with <c>new</c>
        /// (<c>WinFormsCompat.cs</c>), so it and <see cref="MenuItem.Enabled"/> are two independent
        /// fields: application code writing <c>menuItem.Enabled = false</c> sets the shadow, while
        /// everything that reads through a <c>MenuItem</c> reference — this method, the renderers, the
        /// click dispatcher in <c>MenuBase</c> — reads the other one and still sees true. That is
        /// finding TSM-01, rated P0 precisely because disabling a menu item currently does nothing at
        /// all; it is scheduled as W5.15. Reading both here keeps shortcuts honest in the meantime, and
        /// this cast should be deleted when the shadow is.
        /// </remarks>
        private static bool IsEnabledAndVisible (MenuItem item)
        {
            var enabled = item switch {
                ToolStripItem strip => strip.Enabled && item.Enabled,
                _ => item.Enabled,
            };

            return enabled && item.Visible;
        }
    }
}
