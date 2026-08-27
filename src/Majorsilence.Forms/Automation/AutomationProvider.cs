using System.Collections.Generic;
using System.Drawing;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// Builds the <see cref="AutomationElement"/> tree for a window by walking its control hierarchy.
    /// Backend-neutral: it reads the same logical control bounds/state the renderers use, so it works
    /// identically on the headless and real backends.
    /// </summary>
    public static class AutomationProvider
    {
        /// <summary>Builds a fresh snapshot of the window's automation tree, rooted at a synthetic window node.</summary>
        public static AutomationElement BuildTree (WindowBase window)
        {
            System.ArgumentNullException.ThrowIfNull (window);

            var adapter = window.adapter;
            var origin = adapter.Bounds.Location;

            var children = BuildChildren (adapter, origin);

            var title = window is Form form ? form.Text : string.Empty;

            return new AutomationElement (
                source: adapter,
                automationId: string.Empty,
                name: title ?? string.Empty,
                role: "window",
                controlType: window.GetType ().Name,
                value: null,
                enabled: true,
                visible: true,
                focused: false,
                bounds: new Rectangle (origin, adapter.Size),
                children: children);
        }

        private static List<AutomationElement> BuildChildren (Control parent, Point parentOrigin)
        {
            var list = new List<AutomationElement> ();

            foreach (var c in parent.Controls.GetAllControls (includeImplicit: true)) {
                if (!c.Visible)
                    continue;

                var origin = new Point (parentOrigin.X + c.Bounds.X, parentOrigin.Y + c.Bounds.Y);

                // A purely structural control contributes its children at its own position rather than
                // a node of its own: a form's client area exists to keep the caption out of the client
                // region, and a UI Automation client should see the form's controls as direct children
                // of the window, exactly as it did before that container existed -- and as WinForms,
                // which has no such node, presents them.
                if (c.IsAutomationTransparent) {
                    list.AddRange (BuildChildren (c, origin));
                    continue;
                }

                list.Add (BuildNode (c, origin));
            }

            return list;
        }

        private static AutomationElement BuildNode (Control c, Point origin)
        {
            var children = BuildChildren (c, origin);

            // A menu bar or tool strip keeps its items in Items, not Controls -- a MenuItem is not a
            // Control -- so walking controls alone left every menu and every toolbar button out of the
            // tree entirely. An automated test could see that a ToolStrip existed and could not click
            // anything on it, which rules out driving an application by its menus.
            if (c is MenuBase strip)
                children.AddRange (BuildItems (strip.RootItems, origin));

            // A ListBox paints its Items rather than hosting them, so they are not Controls either and the
            // same walk stopped at the list: a caller could find the list and read nothing inside it, which
            // is why apps had to mirror list contents into a label to make them assertable.
            if (c is ListBox list)
                children.AddRange (BuildListItems (list, origin));

            return new AutomationElement (
                source: c,
                automationId: c.Name ?? string.Empty,
                name: AccessibleNameOf (c),
                role: RoleOf (c),
                controlType: c.GetType ().Name,
                value: ValueOf (c),
                enabled: c.Enabled,
                visible: c.Visible,
                focused: c.Focused,
                bounds: new Rectangle (origin, c.Size),
                children: children);
        }

        // Items carry their own Bounds, relative to the strip that lays them out, so the running origin
        // is the same accumulation used for controls. Sub-menus nest through Items in turn, which is what
        // lets a test walk File -> Open without opening anything first.
        private static List<AutomationElement> BuildItems (MenuItemCollection items, Point parentOrigin)
        {
            var list = new List<AutomationElement> ();

            foreach (var item in items) {
                if (!item.Visible)
                    continue;

                var origin = new Point (parentOrigin.X + item.Bounds.X, parentOrigin.Y + item.Bounds.Y);

                list.Add (new AutomationElement (
                    source: item,
                    automationId: (item as ToolStripItem)?.Name ?? string.Empty,
                    name: NameOfItem (item),
                    role: item is ToolStripSeparator ? "separator" : "menuitem",
                    controlType: item.GetType ().Name,
                    value: null,
                    enabled: item.Enabled,
                    visible: item.Visible,
                    focused: false,
                    bounds: new Rectangle (origin, item.Bounds.Size),
                    children: BuildItems (item.Items, origin)));
            }

            return list;
        }

        // Only the items scrolled into view are added: an item off screen has no rectangle to click, and a
        // list of ten thousand rows would otherwise bury the tree it belongs to. The window is the same one
        // GetIndexAtLocation probes -- first visible item to one past the last -- because an item scrolled
        // half out of view is still on screen and still clickable.
        private static List<AutomationElement> BuildListItems (ListBox list, Point origin)
        {
            var items = new List<AutomationElement> ();
            var end = System.Math.Min (list.Items.Count, list.TopIndex + list.VisibleItemCount + 1);

            for (var index = System.Math.Max (0, list.TopIndex); index < end; index++) {
                // GetItemRectangle is built from ClientRectangle and ScaledItemHeight, so it comes back in
                // device pixels, while every Bounds in this tree is logical. Converting here is what keeps a
                // click landing on the item on a scaled display rather than at 1/scale of it.
                var device = list.GetItemRectangle (index);
                if (device.Width <= 0 || device.Height <= 0)
                    continue;

                var item = list.Items [index];

                items.Add (new AutomationElement (
                    source: item ?? (object) string.Empty,
                    // An item has no Name of its own, and a synthetic index-based id would shift under the
                    // caller as the list scrolls or grows -- exactly the brittleness ids exist to avoid.
                    // Items are located by name, text or xpath instead.
                    automationId: string.Empty,
                    name: list.GetItemText (item),
                    role: "listitem",
                    controlType: "ListBoxItem",
                    // No value of its own: GetText reads Value first, so anything here would answer "what
                    // does this item say?" with something other than the item's text. Which item is selected
                    // is reported by the list itself, below.
                    value: null,
                    enabled: list.Enabled,
                    visible: true,
                    focused: false,
                    bounds: new Rectangle (
                        origin.X + list.DeviceToLogicalUnits (device.X),
                        origin.Y + list.DeviceToLogicalUnits (device.Y),
                        list.DeviceToLogicalUnits (device.Width),
                        list.DeviceToLogicalUnits (device.Height)),
                    children: System.Array.Empty<AutomationElement> ()));
            }

            return items;
        }

        private static string NameOfItem (MenuItem item)
        {
            if (!string.IsNullOrEmpty (item.Text))
                return Mnemonics.Strip (item.Text);

            return (item as ToolStripItem)?.Name ?? string.Empty;
        }

        private static string AccessibleNameOf (Control c)
        {
            if (!string.IsNullOrEmpty (c.AccessibleName))
                return c.AccessibleName!;

            // The accessible name is what a user hears and reads, so the mnemonic marker is not part of
            // it: "&File" is named "File", matching what UI Automation and MSAA report on Windows. It
            // also makes By.Name usable -- a caller searching for the text on screen has no reason to
            // know where the designer put the ampersand.
            if (!string.IsNullOrEmpty (c.Text))
                return Mnemonics.Strip (c.Text);

            return c.Name ?? string.Empty;
        }

        // Maps a control to a coarse role. Honors an explicitly-set AccessibleRole, otherwise infers
        // from the control type. Names are lower-case and roughly follow ARIA/WinForms conventions.
        private static string RoleOf (Control c)
        {
            if (c.AccessibleRole != AccessibleRole.Default)
                return c.AccessibleRole.ToString ().ToLowerInvariant ();

            return c switch {
                Button => "button",
                CheckBox => "checkbox",
                RadioButton => "radio",
                TextBox => "textbox",
                ComboBox => "combobox",
                ListBox => "list",
                Label => "label",
                TabControl => "tablist",
                TabStrip => "tablist",
                ProgressBar => "progressbar",
                ScrollBar => "scrollbar",
                Panel => "group",
                _ => c.GetType ().Name.ToLowerInvariant ()
            };
        }

        // Value-bearing controls report their value so tests/automation can assert on it.
        private static string? ValueOf (Control c) => c switch {
            CheckBox cb => cb.Checked ? "true" : "false",
            RadioButton rb => rb.Checked ? "true" : "false",
            TextBox tb => tb.Text,
            ComboBox cbo => cbo.Text,
            // The selected item, the way a ComboBox reports its text -- so "what is selected?" is one read
            // of the list rather than a scan of its items. A multi-select list reports its primary
            // selection here; per-item selection state needs a state field the tree does not have yet.
            ListBox list => list.SelectedItem?.ToString () ?? string.Empty,
            _ => null
        };
    }
}
