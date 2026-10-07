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

        // What the node was built from, so tests (and a later live-region phase) can find a control's node.
        internal object Source { get; }

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
        internal static List<AriaNode> Build (IEnumerable<WindowBase> windows)
        {
            var nodes = new List<AriaNode> ();
            var index = 0;

            foreach (var window in windows) {
                // The modal stack rather than Form.Modal: that property is outbound state for applications
                // (the stored-only baseline keeps it as the example of one), and the stack is what the
                // framework itself consults.
                var modal = window is Form form && Application.ModalStack.Contains (form);
                var owned = window is Form { Owner: not null };
                AddWindow (nodes, AutomationProvider.BuildTree (window), window.Bounds, modal, owned, index++);
            }

            return nodes;
        }

        /// <summary>
        /// Adds one window's subtree. <paramref name="windowBounds"/> is where the window sits in the page
        /// area; the automation tree's own bounds are window-relative, so they are offset by it.
        /// </summary>
        internal static void AddWindow (List<AriaNode> nodes, AutomationElement root, Rectangle windowBounds, bool modal, bool owned, int index)
        {
            var used = new HashSet<string> (nodes.Select (n => n.Id));
            var id = IdOf (root.Source, null, index, used);

            var attributes = new SortedDictionary<string, string> (StringComparer.Ordinal);
            string? role = null;

            if (modal || owned) {
                role = "dialog";
                if (modal)
                    attributes["aria-modal"] = "true";
            } else if (root.Name.Length > 0) {
                // A titled main window is a named landmark, so a reader can jump to it; untitled, it would
                // be an unnamed region, which the ARIA spec says not to expose as one.
                role = "region";
            }

            if (root.Name.Length > 0)
                attributes["aria-label"] = root.Name;

            attributes["data-mf-type"] = root.ControlType;

            nodes.Add (new AriaNode (id, null, index, role, attributes.ToList (), null,
                new Rectangle (windowBounds.Location, windowBounds.Size), false, root.Source));

            // Children's bounds are window-client logical coordinates, which are window coordinates
            // already offset past the caption; the window element starts at the window's own corner.
            AddChildren (nodes, root, id, Point.Empty, used);
        }

        private static void AddChildren (List<AriaNode> nodes, AutomationElement parent, string parentId, Point parentOrigin, HashSet<string> used)
        {
            for (var i = 0; i < parent.Children.Count; i++) {
                var child = parent.Children[i];
                var id = IdOf (child.Source, parentId, i, used);

                nodes.Add (Map (child, parent, id, parentId, i, parentOrigin));
                AddChildren (nodes, child, id, child.Bounds.Location, used);
            }
        }

        private static AriaNode Map (AutomationElement e, AutomationElement parent, string id, string parentId, int index, Point parentOrigin)
        {
            var role = RoleOf (e);
            var attributes = new SortedDictionary<string, string> (StringComparer.Ordinal);
            string? text = null;

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
                break;
            case "option":
                attributes["aria-selected"] = parent.Value is { Length: > 0 } selected && selected == e.Name ? "true" : "false";
                break;
            }

            if (!e.Enabled)
                attributes["aria-disabled"] = "true";

            if (e.AutomationId.Length > 0)
                attributes["data-mf-automation-id"] = e.AutomationId;

            attributes["data-mf-type"] = e.ControlType;

            foreach (var state in e.State)
                attributes["data-mf-state-" + state.Key] = state.Value;

            var bounds = new Rectangle (e.Bounds.X - parentOrigin.X, e.Bounds.Y - parentOrigin.Y, e.Bounds.Width, e.Bounds.Height);

            return new AriaNode (id, parentId, index, role, attributes.ToList (), text, bounds, e.Focused, e.Source);
        }

        private static string Flag (string? value) => value == "true" ? "true" : "false";

        // ARIA roles whose accessible name is computed from their content.
        private static bool NamedFromContent (string role) => role switch {
            "button" or "checkbox" or "radio" or "link" or "menuitem" or "option" or "tab" or "tooltip" or "treeitem" => true,
            _ => false,
        };

        /// <summary>
        /// The ARIA role for an element, from its automation role -- the inferred one
        /// (<c>button</c>, <c>textbox</c>, …) or an explicit <see cref="AccessibleRole"/> name, lower-cased.
        /// Null means a generic element.
        /// </summary>
        internal static string? RoleOf (AutomationElement e) => e.Role switch {
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
            "menupopup" or "contextmenustrip" or "contextmenu" => "menu",
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
        internal static AriaNode? FocusedNode (IReadOnlyList<AriaNode> nodes) => nodes.LastOrDefault (n => n.Focused);

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
        private Dictionary<string, AriaNode> current = new ();
        private string? active_element;
        private Backends.IPlatformTimer? timer;
        private bool sync_pending;
        private bool disposed;

        /// <summary>The longest a change waits before it reaches the DOM, and the shortest gap between syncs.</summary>
        internal const int IntervalMilliseconds = 100;

        internal AriaDomMirror (IAriaDomSink sink, Func<IEnumerable<WindowBase>>? windows = null)
        {
            this.sink = sink;
            this.windows = windows ?? DefaultWindows;
            WindowBase.FrameRendered += OnFrameRendered;
        }

        // Shown forms, in the order they were opened: a dialog opened later sits above its owner.
        private static Form[] DefaultWindows () =>
            Application.OpenForms.Cast<Form> ().Where (f => f.Visible).ToArray ();

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

            current = nodes.ToDictionary (n => n.Id);

            var focused = AriaDom.FocusedNode (nodes)?.ElementId;

            if (focused != active_element) {
                active_element = focused;
                sink.SetActiveDescendant (focused);
            }
        }

        /// <inheritdoc/>
        public void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            WindowBase.FrameRendered -= OnFrameRendered;
            timer?.Dispose ();
        }
    }
}
