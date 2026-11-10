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
        // Shared by every node that has nothing of its own to report (register item F19): a fresh empty
        // dictionary per node would be a pointless allocation for what is, for every built-in control and
        // structural node, always going to be empty.
        private static readonly Dictionary<string, string> EmptyState = new ();

        /// <summary>Builds a fresh snapshot of the window's automation tree, rooted at a synthetic window node.</summary>
        public static AutomationElement BuildTree (WindowBase window)
        {
            Guard.ThrowIfNull (window);

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
                state: EmptyState,
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

            // A DataGridView paints its rows and cells too, so the walk stopped at the grid and an automated
            // test could neither read a result set nor click a row in it. Every grid that derives from it
            // (RadGridView among them) inherits these nodes.
            if (c is DataGridView grid)
                children.AddRange (BuildGridItems (grid, origin));

            // An MDI child's frame and a panel-hosted form paint a whole form inside them, but that form's
            // controls belong to the form, not to the frame's Controls, so the walk stopped at the frame and
            // nothing on a child window could be found or clicked. The form's tree hangs under the frame,
            // offset by where its client area starts.
            (Form? Form, Point Client) hosted = c switch {
                MdiChildWindow frame when frame.WindowState != FormWindowState.Minimized => (frame.ChildForm, frame.ContentOrigin),
                FormHost host => (host.ChildForm, Point.Empty),
                _ => (null, Point.Empty),
            };

            if (hosted.Form is { } form) {
                var adapter = form.adapter;
                var at = new Point (origin.X + hosted.Client.X + adapter.Bounds.X, origin.Y + hosted.Client.Y + adapter.Bounds.Y);
                children.AddRange (BuildChildren (adapter, at));
            }

            // A custom-painted control's own IAutomationStateProvider fully replaces the built-in-control
            // inference for value (not blended with it): a control that opts in owns what its value means.
            var stateProvider = c as IAutomationStateProvider;

            return new AutomationElement (
                source: c,
                automationId: c.Name ?? string.Empty,
                name: AccessibleNameOf (c),
                role: RoleOf (c),
                controlType: c.GetType ().Name,
                value: stateProvider is not null ? stateProvider.AutomationValue : ValueOf (c),
                state: stateProvider?.AutomationState ?? EmptyState,
                enabled: c.Enabled,
                visible: c.Visible,
                focused: c.Focused,
                bounds: new Rectangle (origin, c.Size),
                children: children,
                liveSetting: LiveAnnouncer.LiveSettingOf (c));
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
                    state: EmptyState,
                    enabled: item.Enabled,
                    visible: item.Visible,
                    focused: false,
                    bounds: new Rectangle (origin, item.Bounds.Size),
                    children: BuildItems (item.Items, origin),
                    liveSetting: LiveAnnouncer.LiveSettingOf (item)));
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
                // GetItemRectangleDevice is built from ClientRectangle and ScaledItemHeight, so it is in
                // device pixels, while every Bounds in this tree is logical. Converting here is what keeps a
                // click landing on the item on a scaled display rather than at 1/scale of it.
                var device = list.GetItemRectangleDevice (index);
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
                    state: EmptyState,
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

        // Column headers, then one row node per displayed row holding one cell node per displayed cell. As
        // with a list, only what is scrolled into view is added -- a row off screen has no rectangle to click
        // -- using the grid's own display rectangles, which are logical and already clipped to the grid, so a
        // partly visible row or column is still present and its click point is on screen.
        //
        // Names follow what is on screen rather than upstream's UIA names ("Row 3", "Name Row 3"): a test
        // looks for the text it can see, and By.Name ("Smith") should find the cell showing Smith. Where a
        // cell sits is reported as state instead (state-row, state-column), which an xpath can still select on.
        private static List<AutomationElement> BuildGridItems (DataGridView grid, Point origin)
        {
            var items = new List<AutomationElement> ();
            var columns = grid.Columns;

            if (columns.Count == 0)
                return items;

            Rectangle ToTree (Rectangle r) => new (origin.X + r.X, origin.Y + r.Y, r.Width, r.Height);

            if (grid.ColumnHeadersVisible) {
                for (var col = 0; col < columns.Count; col++) {
                    var column = columns [col];
                    if (!column.Visible)
                        continue;

                    var header = grid.GetCellDisplayRectangle (col, -1, cutOverflow: true);
                    if (header.Width <= 0 || header.Height <= 0)
                        continue;

                    items.Add (new AutomationElement (
                        source: column,
                        // Not the column's Name: ids are meant to be unique in the window, and two grids (or a
                        // grid and a control) commonly share column names like "colName". A WebDriver reference
                        // re-resolves by id first, so a shared one would land on the other grid's column.
                        automationId: string.Empty,
                        name: column.HeaderText ?? string.Empty,
                        role: "columnheader",
                        controlType: column.GetType ().Name,
                        value: null,
                        state: new Dictionary<string, string> { ["column"] = column.Name ?? string.Empty },
                        enabled: grid.Enabled,
                        visible: true,
                        focused: false,
                        bounds: ToTree (header),
                        children: System.Array.Empty<AutomationElement> ()));
                }
            }

            var rows = grid.Rows;
            var first = System.Math.Max (0, grid.FirstDisplayedScrollingRowIndex);
            var end = System.Math.Min (rows.Count, first + grid.DisplayedRowCount (includePartialRow: true));

            // DisplayedRowCount skips hidden rows without counting them, so the window has to be widened by
            // the hidden ones met inside it, or a hidden row near the top hides the last displayed row too.
            for (var index = first; index < end; index++) {
                var row = rows [index];
                if (!row.Visible) {
                    end = System.Math.Min (rows.Count, end + 1);
                    continue;
                }

                var cells = new List<AutomationElement> ();
                var texts = new List<string> ();
                var row_bounds = Rectangle.Empty;

                for (var col = 0; col < columns.Count && col < row.Cells.Count; col++) {
                    var column = columns [col];
                    if (!column.Visible)
                        continue;

                    var cell = row.Cells [col];
                    var text = cell.FormattedValue?.ToString () ?? string.Empty;
                    texts.Add (text);

                    var bounds = grid.GetCellDisplayRectangle (col, index, cutOverflow: true);
                    if (bounds.Width <= 0 || bounds.Height <= 0)
                        continue;

                    row_bounds = row_bounds.IsEmpty ? bounds : Rectangle.Union (row_bounds, bounds);

                    cells.Add (new AutomationElement (
                        source: cell,
                        automationId: string.Empty,
                        name: text,
                        role: "cell",
                        controlType: cell.GetType ().Name,
                        value: text,
                        state: new Dictionary<string, string> {
                            ["row"] = index.ToString (System.Globalization.CultureInfo.InvariantCulture),
                            ["column"] = column.Name ?? string.Empty,
                            // Upstream's cell is selected when it, its row or its column is
                            // (DataGridViewCell.GetSelected), so a full-row-select grid -- RadGridView's
                            // default -- reports every cell of the selected row, not none of them.
                            ["selected"] = cell.Selected || row.Selected || column.Selected ? "true" : "false",
                        },
                        enabled: grid.Enabled,
                        visible: true,
                        focused: false,
                        bounds: ToTree (bounds),
                        children: System.Array.Empty<AutomationElement> ()));
                }

                // A row scrolled sideways out of every column still exists but has nothing to click.
                if (row_bounds.IsEmpty)
                    continue;

                // The row's own value is every cell's text, ';'-separated as upstream's row accessible object
                // reports it (DataGridViewRow.DataGridViewRowAccessibleObject.Value), so one read of a row
                // answers "what does it say?" -- including columns scrolled out of view.
                var joined = string.Join (";", texts);

                items.Add (new AutomationElement (
                    source: row,
                    automationId: string.Empty,
                    name: joined,
                    role: "row",
                    controlType: row.GetType ().Name,
                    value: joined,
                    state: new Dictionary<string, string> {
                        ["row"] = index.ToString (System.Globalization.CultureInfo.InvariantCulture),
                        ["selected"] = row.Selected ? "true" : "false",
                    },
                    enabled: grid.Enabled,
                    visible: true,
                    focused: false,
                    bounds: ToTree (row_bounds),
                    children: cells));
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

            // A child window is named by its form's title, as the frame's caption shows it.
            if (c is MdiChildWindow frame)
                return frame.ChildForm.Text ?? string.Empty;

            // The accessible name is what a user hears and reads, so the mnemonic marker is not part of
            // it: "&File" is named "File", matching what UI Automation and MSAA report on Windows. It
            // also makes By.Name usable -- a caller searching for the text on screen has no reason to
            // know where the designer put the ampersand.
            // A ListBox's Text is its selected item's text (as upstream's is), which the native list's
            // window text -- what its accessible name comes from -- is not. Naming the list by its
            // selection would rename it on every click and collide with the item's own name.
            var text = c is ListBox list ? list.WindowText : c.Text;

            if (!string.IsNullOrEmpty (text))
                return Mnemonics.Strip (text);

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
            // selection here; per-item selection state would need each list item's own AutomationElement.State
            // (register item F19 added the field, on BuildListItems' own items below, not wired up here --
            // a separate, built-in-ListBox concern from what this item was filed for).
            ListBox list => list.SelectedItem?.ToString () ?? string.Empty,
            _ => null
        };
    }
}
