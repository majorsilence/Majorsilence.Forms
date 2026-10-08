using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Majorsilence.Forms.Automation
{
    /// <summary>
    /// One element of the accessibility DOM a browser host keeps next to its canvas: what an ARIA-aware
    /// reader needs to know about one <see cref="AutomationElement"/>.
    /// </summary>
    /// <remarks>
    /// Host-neutral on purpose -- it is plain data, built from the automation tree that UI Automation and
    /// the WebDriver server already read, so the mapping is unit-tested here without a browser. The
    /// browser backend only applies the <see cref="AriaDomOp"/>s that <see cref="AriaDom.Diff"/> derives
    /// from two of these trees.
    /// </remarks>
    internal sealed class AriaNode : IEquatable<AriaNode>
    {
        internal AriaNode (string id, string? parentId, int index, string? role, IReadOnlyList<KeyValuePair<string, string>> attributes,
            string? text, Rectangle bounds, bool focused, object source)
        {
            Id = id;
            ParentId = parentId;
            Index = index;
            Role = role;
            Attributes = attributes;
            Text = text;
            Bounds = bounds;
            Focused = focused;
            Source = source;
        }

        /// <summary>Stable across snapshots for as long as the control lives; the DOM element's key.</summary>
        public string Id { get; }

        /// <summary>The parent node's <see cref="Id"/>, or null for a window (a child of the mirror root).</summary>
        public string? ParentId { get; }

        /// <summary>Position among the parent's element children, in z-order (the automation tree's order).</summary>
        public int Index { get; }

        /// <summary>The ARIA role, or null for a generic element.</summary>
        public string? Role { get; }

        /// <summary>Every other attribute (<c>aria-*</c>, <c>data-mf-*</c>), sorted by name.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; }

        /// <summary>Text content: the name of a role named from its content, a label's text, a text box's value.</summary>
        public string? Text { get; }

        /// <summary>Bounds relative to the parent element (for a window: to the page area the UI fills), in CSS pixels.</summary>
        public Rectangle Bounds { get; }

        /// <summary>Whether this is the element with keyboard focus. Not part of equality: focus moves
        /// by <c>aria-activedescendant</c> on the host, not by rewriting elements.</summary>
        public bool Focused { get; }

        // What the node was built from, so tests and the owner links of popups can find a control's node.
        internal object Source { get; }

        /// <summary>Whether this is the item a keyboard user is on inside an open drop-down -- the
        /// highlighted menu item, the selected option of a combo box's open list -- which
        /// <c>aria-activedescendant</c> points at in preference to the focused control. Not part of
        /// equality, like <see cref="Focused"/>.</summary>
        public bool Active { get; init; }

        /// <summary>When this node's <see cref="Spoken"/> text is announced through the live region.</summary>
        public AriaAnnounce Announce { get; init; }

        /// <summary>How urgently: <see cref="AriaLive.Assertive"/> interrupts the reader.</summary>
        public AriaLive Live { get; init; }

        /// <summary>What is announced for this node: a live label's text, a value, a dialog's title and message.</summary>
        public string? Spoken { get; init; }

        /// <summary>A copy with the given attributes added or replaced (the links a popup adds to its owner).</summary>
        internal AriaNode With (IEnumerable<KeyValuePair<string, string>> extra)
        {
            var merged = new SortedDictionary<string, string> (StringComparer.Ordinal);
            foreach (var a in Attributes)
                merged[a.Key] = a.Value;
            foreach (var a in extra)
                merged[a.Key] = a.Value;

            return new AriaNode (Id, ParentId, Index, Role, merged.ToList (), Text, Bounds, Focused, Source) {
                Active = Active, Announce = Announce, Live = Live, Spoken = Spoken,
            };
        }

        /// <summary>The DOM <c>id</c>, which <c>aria-activedescendant</c> refers to.</summary>
        public string ElementId => AriaDom.ElementIdPrefix + Id;

        /// <summary>Reads one attribute, or null.</summary>
        public string? this [string name] => Attributes.FirstOrDefault (a => a.Key == name).Value;

        /// <inheritdoc/>
        public bool Equals (AriaNode? other) =>
            other is not null
            && Id == other.Id && ParentId == other.ParentId && Index == other.Index && Role == other.Role
            && Text == other.Text && Bounds == other.Bounds
            && Attributes.SequenceEqual (other.Attributes);

        /// <inheritdoc/>
        public override bool Equals (object? obj) => Equals (obj as AriaNode);

        /// <inheritdoc/>
        public override int GetHashCode () => Id.GetHashCode ();

        /// <inheritdoc/>
        public override string ToString () => $"{Role ?? "-"} #{Id} \"{Text ?? this["aria-label"]}\" {Bounds}";
    }

    /// <summary>How urgently an announcement is spoken: the ARIA <c>aria-live</c> politeness.</summary>
    internal enum AriaLive
    {
        /// <summary>Not announced.</summary>
        Off,
        /// <summary>Spoken when the reader is idle.</summary>
        Polite,
        /// <summary>Spoken at once, interrupting.</summary>
        Assertive,
    }

    /// <summary>What change to a node is announced through the live region.</summary>
    internal enum AriaAnnounce
    {
        /// <summary>Nothing.</summary>
        None,
        /// <summary>Its text changing: a label with a <c>LiveSetting</c>, anything on a status bar.</summary>
        TextChange,
        /// <summary>Its value changing while it has focus and focus stays on it: a combo box, slider or
        /// spin box, whose value a reader following <c>aria-activedescendant</c> would not otherwise hear.</summary>
        FocusedValueChange,
        /// <summary>Its appearing: a modal dialog or message box.</summary>
        Opened,
    }

    /// <summary>Something to say through the live region.</summary>
    internal readonly struct AriaAnnouncement
    {
        internal AriaAnnouncement (string text, bool assertive, string? key)
        {
            Text = text;
            Assertive = assertive;
            Key = key;
        }

        /// <summary>What is said.</summary>
        public string Text { get; }

        /// <summary>Whether it interrupts (<c>aria-live="assertive"</c>) rather than waits.</summary>
        public bool Assertive { get; }

        /// <summary>What it is about: a later polite announcement with the same key replaces one not yet
        /// spoken, and the same text for the same key is not repeated. Null for a one-off.</summary>
        public string? Key { get; }

        /// <inheritdoc/>
        public override string ToString () => (Assertive ? "assertive " : "polite ") + Key + ": " + Text;
    }

    /// <summary>One change to apply to the accessibility DOM.</summary>
    internal readonly struct AriaDomOp
    {
        private AriaDomOp (string id, AriaNode? node)
        {
            Id = id;
            Node = node;
        }

        /// <summary>Removes the element (and so its subtree).</summary>
        public static AriaDomOp Remove (string id) => new (id, null);

        /// <summary>Creates the element, or updates it in place -- attributes, text, bounds and position.</summary>
        public static AriaDomOp Upsert (AriaNode node) => new (node.Id, node);

        public string Id { get; }

        /// <summary>The element's new state, or null for a removal.</summary>
        public AriaNode? Node { get; }

        public bool IsRemove => Node is null;
    }

    /// <summary>
    /// Maps the automation tree onto ARIA elements and computes the minimal set of DOM changes between
    /// two snapshots. See <c>docs/backends.md</c>, "Accessibility DOM (browser)".
    /// </summary>
    internal static class AriaDom
    {
        /// <summary>Prefix of every mirrored element's DOM id.</summary>
        internal const string ElementIdPrefix = "mf-a11y-";

        // Ids for controls and items: assigned on first sight and kept for the object's lifetime, so a
        // control that moves, is renamed or is re-parented updates one element instead of replacing it.
        private static readonly ConditionalWeakTable<object, string> ids = new ();
        private static int next_id;

        /// <summary>Builds the mirror of every window, in z-order (later windows on top).</summary>
        /// <remarks>
        /// Popups (<see cref="PopupWindow"/>: combo box lists, menu drop-downs, tool tips) are mirrored
        /// after the forms, each inside the element of the window it was opened for -- so a drop-down of a
        /// modal dialog is inside the <c>aria-modal</c> dialog, where a reader still looks -- and linked
        /// to the control that opened it.
        /// </remarks>
        internal static List<AriaNode> Build (IEnumerable<WindowBase> windows)
        {
            var nodes = new List<AriaNode> ();
            var popups = new List<PopupWindow> ();
            var index = 0;

            foreach (var window in windows) {
                if (window is PopupWindow popup) {
                    popups.Add (popup);
                    continue;
                }

                // The modal stack rather than Form.Modal: that property is outbound state for applications
                // (the stored-only baseline keeps it as the example of one), and the stack is what the
                // framework itself consults.
                var modal = window is Form form && Application.ModalStack.Contains (form);
                var owned = window is Form { Owner: not null };
                AddWindow (nodes, AutomationProvider.BuildTree (window), window.Bounds, modal, owned, index++, window as MessageBoxForm);
            }

            // In the order they were shown, so a submenu comes after (and inside) the menu it hangs off.
            foreach (var popup in popups)
                AddPopup (nodes, popup, ref index);

            return nodes;
        }

        /// <summary>
        /// Adds one window's subtree. <paramref name="windowBounds"/> is where the window sits in the page
        /// area; the automation tree's own bounds are window-relative, so they are offset by it.
        /// </summary>
        internal static void AddWindow (List<AriaNode> nodes, AutomationElement root, Rectangle windowBounds, bool modal, bool owned, int index,
            MessageBoxForm? message = null)
        {
            var used = new HashSet<string> (nodes.Select (n => n.Id));
            var id = IdOf (root.Source, null, index, used);

            var attributes = new SortedDictionary<string, string> (StringComparer.Ordinal);
            string? role = null;

            if (modal || owned) {
                // A message box is the ARIA alert dialog: it exists to say something urgent and wait.
                role = message is null ? "dialog" : "alertdialog";
                if (modal)
                    attributes["aria-modal"] = "true";
            } else if (root.Name.Length > 0) {
                // A titled main window is a named landmark, so a reader can jump to it; untitled, it would
                // be an unnamed region, which the ARIA spec says not to expose as one.
                role = "region";
            }

            if (root.Name.Length > 0)
                attributes["aria-label"] = root.Name;

            if (message is not null)
                attributes["aria-describedby"] = ElementIdPrefix + KeyOf (message.MessageLabel);

            attributes["data-mf-type"] = root.ControlType;

            // Opening a modal dialog is announced: focus moving into it is not always enough for a reader
            // to say what it is, and a message box's text is what the user must hear. An error or warning
            // interrupts; anything else waits its turn.
            var announce = modal || message is not null ? AriaAnnounce.Opened : AriaAnnounce.None;
            var spoken = message is null ? root.Name : JoinSentences (root.Name, message.MessageLabel.Text);
            var live = message?.Glyph is Renderers.MessageGlyph.Error or Renderers.MessageGlyph.Warning ? AriaLive.Assertive : AriaLive.Polite;

            nodes.Add (new AriaNode (id, null, index, role, attributes.ToList (), null,
                new Rectangle (windowBounds.Location, windowBounds.Size), false, root.Source) {
                Announce = announce,
                Live = announce == AriaAnnounce.None ? AriaLive.Off : live,
                Spoken = announce == AriaAnnounce.None ? null : spoken,
            });

            // Children's bounds are window-client logical coordinates, which are window coordinates
            // already offset past the caption; the window element starts at the window's own corner.
            AddChildren (nodes, root, id, Point.Empty, used, default, null);
        }

        private static string JoinSentences (string first, string second)
        {
            first = first.Trim ();
            second = second.Trim ();

            if (first.Length == 0)
                return second;
            if (second.Length == 0)
                return first;

            return first + (char.IsPunctuation (first[first.Length - 1]) ? " " : ". ") + second;
        }

        // What kind of popup a subtree is in, which decides what its items mean to a reader.
        private enum PopupKind
        {
            None,
            Menu,
            ComboList,
            Other,
        }

        private readonly struct Context
        {
            internal Context (PopupKind popup, bool inStatus)
            {
                Popup = popup;
                InStatus = inStatus;
            }

            public PopupKind Popup { get; }

            // Under a status bar: its text changes are announced, as ARIA's role="status" means.
            public bool InStatus { get; }
        }

        /// <summary>
        /// Adds a shown popup: inside the element of the window it was opened for (positioned relative to
        /// it), or at the top level if that window is not mirrored; then links the control that opened it.
        /// </summary>
        private static void AddPopup (List<AriaNode> nodes, PopupWindow popup, ref int rootIndex)
        {
            var root = AutomationProvider.BuildTree (popup);
            var used = new HashSet<string> (nodes.Select (n => n.Id));

            var parent_window = popup.ParentWindow;
            var parent = nodes.FirstOrDefault (n => ReferenceEquals (n.Source, parent_window.adapter));
            // The size the popup asked to be shown at: its backend's client size is not always settled
            // yet when the mirror reads it (a tool tip's read 0 x 0 in the browser).
            var bounds = new Rectangle (popup.Location, popup.Size);

            // The popup's Location is a desktop position (PointToScreen's space: the client origin plus
            // device pixels), the element's coordinates are logical and relative to the client area.
            if (parent is not null) {
                var origin = parent_window.ClientOriginOnScreen;
                var scale = parent_window.DesktopScaling;
                // Plus the adapter's own position: the tree's coordinates start from it, and
                // PointToScreen measures from the window, as BuildTree's origin does not.
                var tree_origin = parent_window.adapter.Bounds.Location;
                bounds.Location = new Point (
                    (int)Math.Round ((popup.Location.X - origin.X) / scale) + tree_origin.X,
                    (int)Math.Round ((popup.Location.Y - origin.Y) / scale) + tree_origin.Y);
            }

            var index = parent is null ? rootIndex++ : nodes.Count (n => n.ParentId == parent.Id);
            var id = IdOf (root.Source, parent?.Id, index, used);

            var menu = root.Children.Count == 1 ? root.Children[0].Source as MenuDropDown : null;
            var owner = popup.AccessibleOwner ?? menu?.DropDownOwnerItem;
            var kind = menu is not null ? PopupKind.Menu : owner is ComboBox ? PopupKind.ComboList : PopupKind.Other;

            var attributes = new SortedDictionary<string, string> (StringComparer.Ordinal) {
                ["data-mf-popup"] = popup.IsToolTip ? "tooltip" : kind switch {
                    PopupKind.Menu => "menu",
                    PopupKind.ComboList => "listbox",
                    _ => "other",
                },
                ["data-mf-type"] = root.ControlType,
            };

            if (popup.IsToolTip) {
                // A tip is one piece of text; its label's own node would only say it twice.
                var tip = string.Join (Environment.NewLine, root.Children.Select (c => c.Name).Where (n => n.Length > 0));
                var node = new AriaNode (id, parent?.Id, index, "tooltip", attributes.ToList (), tip.Length > 0 ? tip : null, bounds, false, root.Source);
                nodes.Add (node);
                Link (nodes, owner, new[] { new KeyValuePair<string, string> ("aria-describedby", node.ElementId) }, null);
                return;
            }

            nodes.Add (new AriaNode (id, parent?.Id, index, null, attributes.ToList (), null, bounds, false, root.Source));
            AddChildren (nodes, root, id, Point.Empty, used, new Context (kind, false), null);

            // The owner controls the menu or list itself rather than the wrapper around it, so a reader
            // that follows aria-controls lands on the items.
            var target = nodes.FirstOrDefault (n => n.ParentId == id && n.Role is "menu" or "listbox")
                ?? nodes.First (n => n.Id == id);

            Link (nodes, owner, new[] {
                new KeyValuePair<string, string> ("aria-controls", target.ElementId),
                new KeyValuePair<string, string> ("aria-expanded", "true"),
            }, target);
        }

        // Adds the popup's links to its owner's element, and names an unnamed menu or list after the
        // owner ("File", the combo box's label) so a reader entering it hears what it belongs to.
        private static void Link (List<AriaNode> nodes, object? owner, KeyValuePair<string, string>[] links, AriaNode? target)
        {
            if (owner is null)
                return;

            var at = nodes.FindIndex (n => ReferenceEquals (n.Source, owner));
            if (at < 0)
                return;

            var owner_node = nodes[at];
            nodes[at] = owner_node.With (links);

            var name = owner_node.Text ?? owner_node["aria-label"];
            if (target is null || target.Role is not ("menu" or "listbox") || target["aria-label"] is not null || string.IsNullOrEmpty (name))
                return;

            var t = nodes.IndexOf (target);
            nodes[t] = target.With (new[] { new KeyValuePair<string, string> ("aria-label", name!) });
        }

        private static void AddChildren (List<AriaNode> nodes, AutomationElement parent, string parentId, Point parentOrigin, HashSet<string> used,
            Context context, string? parentRole)
        {
            for (var i = 0; i < parent.Children.Count; i++) {
                var child = parent.Children[i];
                var id = IdOf (child.Source, parentId, i, used);
                var node = Map (child, parent, id, parentId, i, parentOrigin, context, parentRole);

                nodes.Add (node);

                // The automation tree nests a menu item's whole submenu under it, open or not. ARIA has no
                // menu item inside a menu item: a submenu is its own menu, mirrored as the popup it is
                // when it is open, and the item says it has one (aria-haspopup, aria-expanded).
                if (child.Source is MenuItem)
                    continue;

                AddChildren (nodes, child, id, child.Bounds.Location, used,
                    new Context (context.Popup, context.InStatus || node.Role == "status"), node.Role);
            }
        }

        private static AriaNode Map (AutomationElement e, AutomationElement parent, string id, string parentId, int index, Point parentOrigin,
            Context context, string? parentRole)
        {
            var role = e.Source is MenuItem item && e.Role == "menuitem" ? ItemRole (item, parentRole) : RoleOf (e);
            var attributes = new SortedDictionary<string, string> (StringComparer.Ordinal);
            string? text = null;
            var active = false;
            var announce = AriaAnnounce.None;
            var live = AriaLive.Off;
            string? spoken = null;

            // AutomationProvider falls back to the control's Name (its designer identifier, "button1") when
            // a control has no text or accessible name. That is an id for tests, not a name for people, so
            // it goes in data-mf-automation-id and is not read out.
            var name = e.Name.Length > 0 && e.Name != e.AutomationId ? e.Name : string.Empty;

            if (role is not null && NamedFromContent (role) || role is null && e.Children.Count == 0) {
                // Content, not aria-label: it is what browser find-in-page and text-based test locators
                // match, and for these roles ARIA computes the name from it anyway.
                text = name.Length > 0 ? name : null;
            } else if (name.Length > 0) {
                attributes["aria-label"] = name;
            }

            switch (role) {
            case "checkbox":
                attributes["aria-checked"] = e.Source is CheckBox { CheckState: CheckState.Indeterminate } ? "mixed" : Flag (e.Value);
                break;
            case "radio":
                attributes["aria-checked"] = Flag (e.Value);
                break;
            case "textbox":
                // A password box's text never reaches the page. Its name still does.
                if (e.Source is TextBox { PasswordProtect: true }) {
                    text = null;
                } else {
                    text = e.Value;
                    if (e.Source is TextBox { Multiline: true })
                        attributes["aria-multiline"] = "true";
                }

                // The automation name of an unlabelled text box is its own text; as a label that would
                // read the value twice.
                if (attributes.TryGetValue ("aria-label", out var label) && label == e.Value)
                    attributes.Remove ("aria-label");
                break;
            case "combobox":
                attributes["aria-expanded"] = e.Source is ComboBox { DroppedDown: true } ? "true" : "false";
                if (!string.IsNullOrEmpty (e.Value))
                    attributes["aria-valuetext"] = e.Value!;
                announce = AriaAnnounce.FocusedValueChange;
                spoken = e.Value;
                break;
            case "option":
                var selected = parent.Value is { Length: > 0 } value && value == e.Name;
                attributes["aria-selected"] = selected ? "true" : "false";
                // In a combo box's open list the selected option is where the arrow keys are.
                active = selected && context.Popup == PopupKind.ComboList;
                break;
            case "slider" when e.Source is TrackBar track:
                attributes["aria-valuemin"] = Number (track.Minimum);
                attributes["aria-valuemax"] = Number (track.Maximum);
                attributes["aria-valuenow"] = Number (track.Value);
                attributes["aria-orientation"] = track.Orientation == Orientation.Vertical ? "vertical" : "horizontal";
                announce = AriaAnnounce.FocusedValueChange;
                spoken = attributes["aria-valuenow"];
                break;
            case "spinbutton" when e.Source is NumericUpDown number:
                attributes["aria-valuemin"] = number.Minimum.ToString (System.Globalization.CultureInfo.InvariantCulture);
                attributes["aria-valuemax"] = number.Maximum.ToString (System.Globalization.CultureInfo.InvariantCulture);
                attributes["aria-valuenow"] = number.Value.ToString (System.Globalization.CultureInfo.InvariantCulture);
                // What the box shows (thousands separators, hex) is what a reader should say.
                if (!string.IsNullOrEmpty (number.Text))
                    attributes["aria-valuetext"] = number.Text;
                announce = AriaAnnounce.FocusedValueChange;
                spoken = number.Text;
                break;
            case "spinbutton" when e.Source is DomainUpDown domain:
                if (!string.IsNullOrEmpty (domain.Text))
                    attributes["aria-valuetext"] = domain.Text;
                announce = AriaAnnounce.FocusedValueChange;
                spoken = domain.Text;
                break;
            case "status":
                // The live region next to the mirror announces a status bar's changes, once and debounced;
                // the element's own implicit politeness would announce them a second time.
                attributes["aria-live"] = "off";
                break;
            }

            if (e.Source is MenuItem menu_item) {
                if (role == "menuitemcheckbox")
                    attributes["aria-checked"] = menu_item.Checked ? "true" : "false";
                else if (role == "button" && menu_item is ToolStripButton { CheckOnClick: true } or { Checked: true })
                    attributes["aria-pressed"] = menu_item.Checked ? "true" : "false";

                if (role is "menuitem" or "menuitemcheckbox" or "button" && menu_item.HasItems) {
                    attributes["aria-haspopup"] = "menu";
                    attributes["aria-expanded"] = menu_item.IsDropDownOpened ? "true" : "false";
                }

                // The highlighted item of an open menu, or of a menu bar being driven from the keyboard.
                active = menu_item.Selected && (context.Popup == PopupKind.Menu || menu_item.ParentControl is MenuBase { IsActivated: true });
            }

            // The accessible object's Help, which asks the control's QueryAccessibilityHelp handlers (EVT-33)
            // -- the UI Automation HelpText a Windows screen reader reads after the name.
            if (e.HelpText is { Length: > 0 } help)
                attributes["aria-description"] = help;

            if (!e.Enabled)
                attributes["aria-disabled"] = "true";

            if (e.AutomationId.Length > 0)
                attributes["data-mf-automation-id"] = e.AutomationId;

            attributes["data-mf-type"] = e.ControlType;

            foreach (var state in e.State)
                attributes["data-mf-state-" + state.Key] = state.Value;

            // A label the application marked live (LiveSetting), and anything on a status bar, is
            // announced when its text changes, as upstream raises LiveRegionChanged for it.
            var setting = e.LiveSetting;
            if (setting != AutomationLiveSetting.Off || context.InStatus || role == "status") {
                announce = AriaAnnounce.TextChange;
                live = setting == AutomationLiveSetting.Assertive ? AriaLive.Assertive : AriaLive.Polite;
                spoken = text ?? (name.Length > 0 ? name : null);
            } else if (announce != AriaAnnounce.None) {
                live = AriaLive.Polite;
            }

            var bounds = new Rectangle (e.Bounds.X - parentOrigin.X, e.Bounds.Y - parentOrigin.Y, e.Bounds.Width, e.Bounds.Height);

            return new AriaNode (id, parentId, index, role, attributes.ToList (), text, bounds, e.Focused, e.Source) {
                Active = active,
                Announce = announce,
                Live = live,
                Spoken = spoken,
            };
        }

        // A tool strip item's role depends on the strip: a menu's items are menu items, a toolbar's are
        // buttons, a status bar's labels are text. ARIA allows a menu item only inside a menu or menu bar.
        private static string? ItemRole (MenuItem item, string? parentRole)
        {
            if (item is ToolStripStatusLabel or ToolStripLabel)
                return null;

            switch (parentRole) {
            case "toolbar":
                return item is ToolStripButton or ToolStripDropDownButton ? "button" : null;
            case "status":
                return null;
            }

            return item is ToolStripMenuItem { CheckOnClick: true } || item.Checked ? "menuitemcheckbox" : "menuitem";
        }

        private static string Number (int value) => value.ToString (System.Globalization.CultureInfo.InvariantCulture);

        private static string Flag (string? value) => value == "true" ? "true" : "false";

        // ARIA roles whose accessible name is computed from their content.
        private static bool NamedFromContent (string role) => role switch {
            "button" or "checkbox" or "radio" or "link" or "menuitem" or "menuitemcheckbox" or "option" or "tab" or "tooltip" or "treeitem" => true,
            _ => false,
        };

        /// <summary>
        /// The ARIA role for an element, from its automation role -- the inferred one
        /// (<c>button</c>, <c>textbox</c>, …) or an explicit <see cref="AccessibleRole"/> name, lower-cased.
        /// Null means a generic element.
        /// </summary>
        internal static string? RoleOf (AutomationElement e)
        {
            // A drop-down (a submenu, a context menu) and a menu bar are told apart by type: their automation
            // roles are their type names, and a menu bar's class is plainly "Menu".
            if (e.Source is Control { AccessibleRole: AccessibleRole.Default } control) {
                if (control is MenuDropDown)
                    return "menu";
                if (control is Menu)
                    return "menubar";
            }

            return e.Role switch {
                "button" or "pushbutton" or "splitbutton" or "buttondropdown" or "buttonmenu" or "buttondropdowngrid" => "button",
                "checkbox" or "checkbutton" => "checkbox",
                "radio" or "radiobutton" => "radio",
                "textbox" or "text" or "richtextbox" or "maskedtextbox" or "hotkeyfield" or "ipaddress" => "textbox",
                "combobox" or "droplist" => "combobox",
                "list" or "listbox" or "checkedlistbox" => "listbox",
                "listitem" => "option",
                "tablist" or "pagetablist" => "tablist",
                "pagetab" => "tab",
                "tabpage" => "tabpanel",
                "progressbar" => "progressbar",
                "scrollbar" => "scrollbar",
                "slider" or "trackbar" => "slider",
                "spinbutton" or "numericupdown" or "domainupdown" => "spinbutton",
                "link" or "linklabel" => "link",
                "menustrip" or "menubar" or "mainmenu" => "menubar",
                "menuitem" => "menuitem",
                "menupopup" or "contextmenustrip" or "contextmenu" or "menudropdown" => "menu",
                "separator" => "separator",
                "toolstrip" or "toolbar" => "toolbar",
                "statusstrip" or "statusbar" => "status",
                "treeview" or "outline" => "tree",
                "outlineitem" => "treeitem",
                "picturebox" or "graphic" => e.Name.Length > 0 && e.Name != e.AutomationId ? "img" : null,
                // An unnamed group is noise to a reader: every layout panel would be announced.
                "group" or "grouping" or "groupbox" => e.Name.Length > 0 && e.Name != e.AutomationId ? "group" : null,
                "dialog" => "dialog",
                "alert" => "alert",
                "tooltip" => "tooltip",
                "document" => "document",
                "table" => "table",
                "row" => "row",
                "cell" => "cell",
                "columnheader" => "columnheader",
                "rowheader" => "rowheader",
                _ => null,
            };
        }

        /// <summary>The element key a control or menu item has (or will have) in the mirror, or null for
        /// anything else. What an explicit announcement about it is keyed by.</summary>
        internal static string? KeyOf (object? source) =>
            source is Control or MenuItem
                ? ids.GetValue (source, _ => System.Threading.Interlocked.Increment (ref next_id).ToString (System.Globalization.CultureInfo.InvariantCulture))
                : null;

        // A control or menu item keeps one id for its lifetime. Anything else -- a list item is whatever
        // object the application added, often a string, and equal strings are one object -- is keyed by
        // its position under its parent.
        private static string IdOf (object source, string? parentId, int index, HashSet<string> used)
        {
            string id;

            if (source is Control or MenuItem)
                id = ids.GetValue (source, _ => System.Threading.Interlocked.Increment (ref next_id).ToString (System.Globalization.CultureInfo.InvariantCulture));
            else
                id = (parentId ?? "w") + "-" + index.ToString (System.Globalization.CultureInfo.InvariantCulture);

            // The same object twice in one tree would make two elements fight over one id.
            if (!used.Add (id)) {
                var n = 2;
                while (!used.Add (id + "x" + n.ToString (System.Globalization.CultureInfo.InvariantCulture)))
                    n++;
                id += "x" + n.ToString (System.Globalization.CultureInfo.InvariantCulture);
            }

            return id;
        }

        /// <summary>
        /// The changes that turn the DOM built from <paramref name="before"/> into
        /// <paramref name="after"/>: a removal for each subtree that went away (only its top element),
        /// then an upsert for each element that is new or differs, parents before children.
        /// </summary>
        internal static List<AriaDomOp> Diff (IReadOnlyDictionary<string, AriaNode> before, IReadOnlyList<AriaNode> after)
        {
            var ops = new List<AriaDomOp> ();
            var present = new HashSet<string> (after.Select (n => n.Id));

            foreach (var old in before.Values) {
                if (present.Contains (old.Id))
                    continue;

                // Removing the parent removes this with it; a separate removal would be a no-op at best.
                if (old.ParentId is { } parent && before.ContainsKey (parent) && !present.Contains (parent))
                    continue;

                ops.Add (AriaDomOp.Remove (old.Id));
            }

            foreach (var node in after) {
                if (!before.TryGetValue (node.Id, out var old) || !old.Equals (node))
                    ops.Add (AriaDomOp.Upsert (node));
            }

            return ops;
        }

        /// <summary>The element that has keyboard focus, or null: the deepest focused node.</summary>
        internal static AriaNode? FocusedNode (IEnumerable<AriaNode> nodes) => DeepestFocused (nodes);

        // The focused node with the most ancestors: Build lists parents before children, but a dictionary
        // of the last snapshot promises no order.
        private static AriaNode? DeepestFocused (IEnumerable<AriaNode> nodes)
        {
            var list = nodes as IReadOnlyList<AriaNode> ?? nodes.ToList ();
            var focused = list.Where (n => n.Focused).ToList ();

            if (focused.Count <= 1)
                return focused.FirstOrDefault ();

            var parents = list.ToDictionary (n => n.Id, n => n.ParentId);
            return focused.OrderBy (n => Depth (n.Id, parents)).Last ();
        }

        private static int Depth (string id, Dictionary<string, string?> parents)
        {
            var depth = 0;
            while (parents.TryGetValue (id, out var parent) && parent is not null) {
                id = parent;
                depth++;
            }
            return depth;
        }

        /// <summary>
        /// The element <c>aria-activedescendant</c> points at: the highlighted item of an open drop-down
        /// (the last one, so the innermost submenu), else the focused control -- or, for a focused list,
        /// its selected option, which is what the ARIA listbox pattern makes active.
        /// </summary>
        internal static AriaNode? ActiveNode (IReadOnlyList<AriaNode> nodes)
        {
            if (nodes.LastOrDefault (n => n.Active) is { } highlighted)
                return highlighted;

            var focused = FocusedNode (nodes);

            if (focused?.Role == "listbox")
                return nodes.FirstOrDefault (n => n.ParentId == focused.Id && n["aria-selected"] == "true") ?? focused;

            return focused;
        }

        /// <summary>
        /// What to say through the live region for the change from <paramref name="before"/> to
        /// <paramref name="after"/>: a live label's or status bar's new text, a modal dialog or message box
        /// that opened, the new value of the control that kept focus. Nothing for the first snapshot -- a
        /// page that has just loaded has nothing to announce -- and nothing twice: one announcement per text.
        /// </summary>
        internal static List<AriaAnnouncement> Announcements (IReadOnlyDictionary<string, AriaNode> before, IReadOnlyList<AriaNode> after)
        {
            var list = new List<AriaAnnouncement> ();

            if (before.Count == 0)
                return list;

            var after_by_id = new Dictionary<string, AriaNode> ();
            foreach (var n in after)
                after_by_id[n.Id] = n;

            var focused_now = FocusedNode (after);
            var focus_now = FocusPath (after_by_id, focused_now);
            var focus_before = FocusPath (before, FocusedNode (before.Values));

            foreach (var node in after) {
                if (node.Spoken is not { } spoken || string.IsNullOrWhiteSpace (spoken))
                    continue;

                before.TryGetValue (node.Id, out var old);

                switch (node.Announce) {
                case AriaAnnounce.Opened when old is null:
                case AriaAnnounce.TextChange when old is not null && old.Spoken != spoken:
                    Add (list, spoken, node.Live == AriaLive.Assertive, node.Id);
                    break;
                case AriaAnnounce.FocusedValueChange when old is not null && old.Spoken != spoken:
                    // Only while focus stays on it: a reader announces the control, value and all, when
                    // focus arrives. And not while the user types into an editable combo box -- the reader
                    // echoes the typing already.
                    if (!focus_now.Contains (node.Id) || !focus_before.Contains (node.Id))
                        break;
                    if (focused_now is not null && focused_now.Id != node.Id && focused_now.Role == "textbox")
                        break;
                    Add (list, spoken, false, node.Id);
                    break;
                }
            }

            return list;
        }

        // The focused node and its ancestors: a combo box or spin box counts as focused when its inner
        // edit box is.
        private static HashSet<string> FocusPath (IReadOnlyDictionary<string, AriaNode> nodes, AriaNode? focused)
        {
            var path = new HashSet<string> ();

            for (var n = focused; n is not null; n = n.ParentId is { } p && nodes.TryGetValue (p, out var parent) ? parent : null)
                path.Add (n.Id);

            return path;
        }

        /// <summary>Adds an announcement unless the same text is already there; an assertive one wins.</summary>
        internal static void Add (List<AriaAnnouncement> list, string text, bool assertive, string? key)
        {
            var at = list.FindIndex (a => a.Text == text);

            if (at < 0)
                list.Add (new AriaAnnouncement (text, assertive, key));
            else if (assertive && !list[at].Assertive)
                list[at] = new AriaAnnouncement (text, true, list[at].Key ?? key);
        }

        /// <summary>
        /// Serializes the operations for the browser side: an array of <c>{"remove": id}</c> and
        /// <c>{"id", "parent", "index", "role", "attrs", "text", "x", "y", "w", "h"}</c> objects.
        /// </summary>
        internal static string ToJson (IReadOnlyList<AriaDomOp> ops)
        {
            using var stream = new MemoryStream ();

            using (var json = new Utf8JsonWriter (stream)) {
                json.WriteStartArray ();

                foreach (var op in ops) {
                    json.WriteStartObject ();

                    if (op.Node is not { } node) {
                        json.WriteString ("remove", ElementIdPrefix + op.Id);
                    } else {
                        json.WriteString ("id", node.ElementId);
                        json.WriteString ("key", node.Id);

                        if (node.ParentId is { } parent)
                            json.WriteString ("parent", ElementIdPrefix + parent);
                        else
                            json.WriteNull ("parent");

                        json.WriteNumber ("index", node.Index);

                        if (node.Role is { } role)
                            json.WriteString ("role", role);

                        json.WriteStartObject ("attrs");
                        foreach (var a in node.Attributes)
                            json.WriteString (a.Key, a.Value);
                        json.WriteEndObject ();

                        if (node.Text is { } text)
                            json.WriteString ("text", text);

                        json.WriteNumber ("x", node.Bounds.X);
                        json.WriteNumber ("y", node.Bounds.Y);
                        json.WriteNumber ("w", node.Bounds.Width);
                        json.WriteNumber ("h", node.Bounds.Height);
                    }

                    json.WriteEndObject ();
                }

                json.WriteEndArray ();
            }

            return Encoding.UTF8.GetString (stream.ToArray ());
        }
    }

    /// <summary>Where <see cref="AriaDomMirror"/> sends its changes: the browser backend's JavaScript side.</summary>
    internal interface IAriaDomSink
    {
        /// <summary>Applies a batch of operations, serialized by <see cref="AriaDom.ToJson"/>.</summary>
        void Apply (string opsJson);

        /// <summary>Points the host's <c>aria-activedescendant</c> at the focused element, or clears it.</summary>
        void SetActiveDescendant (string? elementId);

        /// <summary>Says something through the live region: assertively, or politely after a short quiet
        /// spell in which a newer announcement with the same <paramref name="key"/> replaces it.</summary>
        void Announce (string text, bool assertive, string? key);
    }

    /// <summary>
    /// Where the framework's explicit announcements go: <see cref="AccessibleObject.RaiseAutomationNotification"/>
    /// and <see cref="AccessibleObject.RaiseLiveRegionChanged"/>. A browser's <see cref="AriaDomMirror"/>
    /// listens and speaks them through its live region; with nothing listening they are not delivered,
    /// and those methods say so by returning false, as upstream does without an automation client.
    /// </summary>
    internal static class LiveAnnouncer
    {
        /// <summary>Raised for each announcement; a listener speaks it.</summary>
        internal static event Action<AriaAnnouncement>? Announced;

        /// <summary>Hands the announcement to the listeners; false if there are none or there is nothing to say.</summary>
        internal static bool Announce (string? text, bool assertive, string? key)
        {
            var handler = Announced;

            if (handler is null || string.IsNullOrWhiteSpace (text))
                return false;

            handler (new AriaAnnouncement (text!, assertive, key));
            return true;
        }

        /// <summary>The <c>LiveSetting</c> of a label or status-bar label; Off for anything else.</summary>
        internal static AutomationLiveSetting LiveSettingOf (object? source) => source switch {
            Label label => label.LiveSetting,
            ToolStripStatusLabel status => status.LiveSetting,
            _ => AutomationLiveSetting.Off,
        };

        /// <summary>
        /// <see cref="AccessibleObject.RaiseLiveRegionChanged"/>, and so a live label's text change: the one
        /// path every platform's announcement takes. A control's change goes to each
        /// <see cref="AutomationObserver"/> of its window that listens (the Windows UI Automation bridge raises
        /// UIA's LiveRegionChanged from it, SMP-16), and any owner's text to the browser's live region. True
        /// when either took it, as upstream returns whether the UIA event was raised.
        /// </summary>
        internal static bool LiveRegionChanged (object? owner)
        {
            var setting = LiveSettingOf (owner);

            if (setting == AutomationLiveSetting.Off)
                return false;

            var observed = owner is Control control && AutomationObserver.NotifyLiveRegionChanged (control);

            // The same text the mirror computes for the label's node, so this and the mirror noticing the
            // text change are recognised as one announcement rather than spoken twice.
            var text = owner switch {
                Control c => !string.IsNullOrEmpty (c.AccessibleName) ? c.AccessibleName : Mnemonics.Strip (c.Text),
                ToolStripItem i => Mnemonics.Strip (i.Text),
                _ => null,
            };

            return Announce (text, setting == AutomationLiveSetting.Assertive, AriaDom.KeyOf (owner)) | observed;
        }

        /// <summary><see cref="AccessibleObject.RaiseAutomationNotification"/>.</summary>
        internal static bool Notify (object? owner, AutomationNotificationProcessing processing, string? text)
        {
            var assertive = processing is AutomationNotificationProcessing.ImportantAll or AutomationNotificationProcessing.ImportantMostRecent;

            // The "most recent" kinds replace a notification from the same source not yet spoken; the
            // "all" kinds are each spoken.
            var replaces = processing is AutomationNotificationProcessing.ImportantMostRecent
                or AutomationNotificationProcessing.MostRecent or AutomationNotificationProcessing.CurrentThenMostRecent;

            return Announce (text, assertive, replaces ? "notification-" + (AriaDom.KeyOf (owner) ?? "app") : null);
        }
    }

    /// <summary>
    /// Keeps an accessibility DOM in step with the open forms: after a frame is drawn it re-reads the
    /// automation tree (at most every <see cref="IntervalMilliseconds"/>) and sends only what changed.
    /// </summary>
    /// <remarks>
    /// Driven by painting because every change a reader cares about -- text, visibility, bounds, focus,
    /// enabled state, controls coming and going -- repaints, and an idle UI then costs nothing. The browser
    /// backend creates one; it is host-neutral so the headless suite can drive it with a recording sink.
    /// </remarks>
    internal sealed class AriaDomMirror : IDisposable
    {
        private readonly IAriaDomSink sink;
        private readonly Func<IEnumerable<WindowBase>> windows;
        private readonly bool announce;
        private readonly List<AriaAnnouncement> pending = new ();
        private Dictionary<string, AriaNode> current = new ();
        private string? active_element;
        private Backends.IPlatformTimer? timer;
        private bool sync_pending;
        private bool disposed;

        /// <summary>The longest a change waits before it reaches the DOM, and the shortest gap between syncs.</summary>
        internal const int IntervalMilliseconds = 100;

        /// <param name="sink">Where the changes go.</param>
        /// <param name="windows">The windows to mirror; by default the shown forms and popups.</param>
        /// <param name="announce">Whether to speak changes through the live region (and so deliver
        /// <see cref="AccessibleObject.RaiseAutomationNotification"/>).</param>
        internal AriaDomMirror (IAriaDomSink sink, Func<IEnumerable<WindowBase>>? windows = null, bool announce = true)
        {
            this.sink = sink;
            this.windows = windows ?? DefaultWindows;
            this.announce = announce;
            WindowBase.FrameRendered += OnFrameRendered;
            PopupWindow.ShownPopupsChanged += RequestSync;
            Application.OpenForms.Changed += RequestSync;

            if (announce)
                LiveAnnouncer.Announced += OnAnnounced;
        }

        // Shown forms, in the order they were opened (a dialog opened later sits above its owner), then
        // the shown popups, in the order they were shown.
        private static WindowBase[] DefaultWindows () =>
            Application.OpenForms.Cast<Form> ().Where (f => f.Visible).Cast<WindowBase> ()
                .Concat (PopupWindow.ShownPopups.Where (p => p.Visible && !p.IsDisposed))
                .ToArray ();

        private void OnAnnounced (AriaAnnouncement announcement)
        {
            lock (pending)
                pending.Add (announcement);

            RequestSync ();
        }

        /// <summary>The nodes last sent, by id.</summary>
        internal IReadOnlyDictionary<string, AriaNode> Current => current;

        private void OnFrameRendered (WindowBase window) => RequestSync ();

        /// <summary>Schedules a sync if none is pending.</summary>
        internal void RequestSync ()
        {
            // Not restarted while pending: a steady stream of frames (an animation) would otherwise keep
            // pushing the sync back and the DOM would never catch up.
            if (disposed || sync_pending)
                return;

            sync_pending = true;

            if (timer is null) {
                timer = Backends.Platform.Backend.CreateTimer ();
                timer.IntervalMilliseconds = IntervalMilliseconds;
                timer.Tick += () => {
                    timer.Stop ();
                    SyncNow ();
                };
            }

            timer.Start ();
        }

        /// <summary>Re-reads the tree and sends the difference now.</summary>
        internal void SyncNow ()
        {
            sync_pending = false;

            if (disposed)
                return;

            var nodes = AriaDom.Build (windows ());
            var ops = AriaDom.Diff (current, nodes);

            if (ops.Count > 0)
                sink.Apply (AriaDom.ToJson (ops));

            var spoken = announce ? AriaDom.Announcements (current, nodes) : new List<AriaAnnouncement> ();

            current = nodes.ToDictionary (n => n.Id);

            var focused = AriaDom.ActiveNode (nodes)?.ElementId;

            if (focused != active_element) {
                active_element = focused;
                sink.SetActiveDescendant (focused);
            }

            // After the DOM they talk about is in place. An explicit announcement of a change the mirror
            // noticed too (a live label's text, then RaiseLiveRegionChanged) is one announcement.
            lock (pending) {
                foreach (var p in pending)
                    AriaDom.Add (spoken, p.Text, p.Assertive, p.Key);
                pending.Clear ();
            }

            foreach (var a in spoken)
                sink.Announce (a.Text, a.Assertive, a.Key);
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            WindowBase.FrameRendered -= OnFrameRendered;
            PopupWindow.ShownPopupsChanged -= RequestSync;
            Application.OpenForms.Changed -= RequestSync;
            LiveAnnouncer.Announced -= OnAnnounced;
            timer?.Dispose ();
        }
    }
}
