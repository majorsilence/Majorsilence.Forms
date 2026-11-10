using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat docking manager. Backed by <see cref="Majorsilence.Forms.Panel"/>: it lays its split panels out,
    /// floats windows in <see cref="FloatingWindow"/>s (RadDockFloating.cs) and auto-hides them to tabs along its sides
    /// (RadDockAutoHide.cs), as Telerik's does. Dragging windows to dock them is not implemented.
    /// </summary>
    public partial class RadDock : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets or sets the orientation the dock lays its split panels out along; side by side by default
        /// (Vertical), as Telerik's.</summary>
        public Orientation Orientation {
            get => orientation;
            set {
                orientation = value;
                PerformLayout ();
            }
        }

        private Orientation orientation = Orientation.Vertical;

        /// <summary>Gets or sets the gap between neighbouring split panels.</summary>
        public int SplitterWidth {
            get => splitter_width;
            set {
                splitter_width = value;
                PerformLayout ();
            }
        }

        private int splitter_width = 4;

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets whether auto-cleanup removes this dock's windows. Stored for compat.</summary>
        public bool IsCleanUpTarget { get; set; }

        /// <summary>Gets or sets whether the auto-hide tool tabs are visible. Stored for Telerik compat.</summary>
        public bool ToolTabsVisible { get; set; } = true;

        /// <summary>Gets or sets the alignment of the auto-hide tool tab strips. Stored for Telerik compat.</summary>
        public TabStripAlignment ToolTabsAlignment { get; set; } = TabStripAlignment.Bottom;

        /// <summary>Gets or sets the kind of window an MDI child form is hosted in. <see cref="DockType.Document"/> by default, as Telerik's is.</summary>
        /// <remarks>Telerik reads it only when the dock detects MDI children itself (its <c>AutoDetectMdiChildren</c>,
        /// off by default), which the compat dock does not do; with detection off it changes nothing there either.</remarks>
        public DockType MdiChildrenDockType { get; set; } = DockType.Document;

        /// <summary>Gets the auto-hide groups a designer records, which the dock applies when its initialization ends.</summary>
        /// <remarks>
        /// As Telerik's (measured on UI.for.WinForms 2026.2.520): at <c>EndInit</c> every window a group names --
        /// itself, or through a <see cref="DockWindowPlaceholder"/> by its <see cref="DockWindowPlaceholder.DockWindowName"/>
        /// -- that is in the dock is put in the <see cref="DockState.AutoHide"/> state, a name no window in the dock
        /// has is passed over, and the four collections are emptied. A group added after initialization waits for
        /// the next <c>EndInit</c>.
        /// </remarks>
        public DockAutoHideSerializationContainer SerializableAutoHideContainer { get; } = new ();

        void System.ComponentModel.ISupportInitialize.BeginInit () { }

        void System.ComponentModel.ISupportInitialize.EndInit () => ApplySerializedAutoHideGroups ();

        private void ApplySerializedAutoHideGroups ()
        {
            var container = SerializableAutoHideContainer;
            var sides = new[] { container.LeftAutoHideGroups, container.TopAutoHideGroups, container.RightAutoHideGroups, container.BottomAutoHideGroups };

            if (sides.All (side => side.Count == 0))
                return;

            var windows = AllWindowsInTree ().Where (w => w is not DockWindowPlaceholder).ToList ();

            var positions = new[] { AutoHidePosition.Left, AutoHidePosition.Top, AutoHidePosition.Right, AutoHidePosition.Bottom };

            for (var s = 0; s < sides.Length; s++) {
                var side = sides[s];

                foreach (var group in side) {
                    foreach (var named in group.Windows) {
                        var window = named is DockWindowPlaceholder placeholder
                            ? windows.FirstOrDefault (w => string.Equals (w.Name, placeholder.DockWindowName, StringComparison.Ordinal))
                            : windows.Contains (named) ? named : null;

                        if (window is not null)
                            AutoHide (window, positions[s]);
                    }
                }

                side.Clear ();
            }
        }

        /// <summary>Removes the specified dock window from this dock (compat alias for CloseWindow).</summary>
        public void RemoveWindow (DockWindowBase window) => CloseWindow (window);

        /// <summary>Raised when the dock needs a new document tab strip, before it creates one.</summary>
        /// <remarks>Raised as of W6 mechanisms (#176), from <see cref="GetDefaultDocumentTabStrip"/> and
        /// so from docking a document into an empty dock. A <see cref="DocumentTabStrip"/> the handler
        /// assigns to <c>e.Strip</c> is the one used.</remarks>
        public event EventHandler<DockTabStripNeededEventArgs>? DockTabStripNeeded;

        /// <summary>Raised when the selected dock tab changes.</summary>
        public event EventHandler<SelectedTabChangedEventArgs>? SelectedTabChanged;

        /// <summary>Saves which windows are open, their dock states and the selected tab of each strip.</summary>
        /// <remarks>
        /// Real as of W6 mechanisms (#176), and bounded: the layout is what this dock models -- each
        /// named window's <see cref="DockWindowBase.DockState"/> and whether it is the selected tab of
        /// its strip, with where a floating window floats and the side and size of an auto-hidden one's tab.
        /// Telerik's full layout also records split sizes, which this dock does not save;
        /// <see cref="LoadFromXml(System.IO.Stream)"/> restores what is saved. A
        /// window with no <see cref="Control.Name"/> cannot be matched on the way back and is skipped.
        /// </remarks>
        public void SaveToXml (System.IO.Stream stream)
        {
            Guard.ThrowIfNull (stream);

            var root = new System.Xml.Linq.XElement ("DockLayout");

            foreach (var window in AllWindowsInTree ().Concat (detached_windows.Where (w => !w.IsDisposed)).Distinct ()) {
                if (string.IsNullOrEmpty (window.Name))
                    continue;

                var element = new System.Xml.Linq.XElement ("Window",
                    new System.Xml.Linq.XAttribute ("Name", window.Name),
                    new System.Xml.Linq.XAttribute ("DockState", window.DockState.ToString ()),
                    new System.Xml.Linq.XAttribute ("Selected", DockStrip.IsSelected (window) ? "true" : "false"));

                // Where a floating window floats, and the side and size of an auto-hidden one's tab.
                if (window.FloatingParent is { } host)
                    element.Add (new System.Xml.Linq.XAttribute ("FloatingBounds", Join (host.Location.X, host.Location.Y, host.Size.Width, host.Size.Height)));

                if (window.DockState == DockState.AutoHide) {
                    element.Add (new System.Xml.Linq.XAttribute ("AutoHidePosition", window.auto_hide_side.ToString ()));

                    if (window is ToolWindow tool)
                        element.Add (new System.Xml.Linq.XAttribute ("AutoHideSize", Join (tool.AutoHideSize.Width, tool.AutoHideSize.Height)));
                }

                root.Add (element);
            }

            new System.Xml.Linq.XDocument (root).Save (stream);
        }

        /// <summary>Saves the layout to a file. See <see cref="SaveToXml(System.IO.Stream)"/>.</summary>
        public void SaveToXml (string fileName)
        {
            using var file = System.IO.File.Create (fileName);
            SaveToXml (file);
        }

        /// <summary>Restores a layout <see cref="SaveToXml(System.IO.Stream)"/> wrote, or one Telerik's dock saved.</summary>
        /// <remarks>Windows are matched by name. For this dock's own layout, a saved window that is not in the dock, or a window
        /// not in the saved layout, is left alone. A document that is not a well-formed layout throws
        /// <see cref="System.Xml.XmlException"/>.</remarks>
        public void LoadFromXml (System.IO.Stream stream)
        {
            Guard.ThrowIfNull (stream);

            var document = System.Xml.Linq.XDocument.Load (stream);

            if (document.Root?.Name.LocalName == "RadDock") {
                LoadTelerikLayout (document.Root);
                return;
            }

            var windows = AllWindowsInTree ().Concat (detached_windows.Where (w => !w.IsDisposed))
                .Where (w => !string.IsNullOrEmpty (w.Name))
                .GroupBy (w => w.Name, StringComparer.Ordinal)
                .ToDictionary (g => g.Key, g => g.First (), StringComparer.Ordinal);

            var selected = new List<DockWindowBase> ();

            foreach (var element in document.Root?.Elements ("Window") ?? Enumerable.Empty<System.Xml.Linq.XElement> ()) {
                if ((string?) element.Attribute ("Name") is not { } name || !windows.TryGetValue (name, out var window))
                    continue;

                if (!Enum.TryParse<DockState> ((string?) element.Attribute ("DockState"), out var state))
                    continue;

                if (state == DockState.Floating && Numbers ((string?) element.Attribute ("FloatingBounds")) is { Length: 4 } b)
                    Float (window, new Rectangle (b[0], b[1], b[2], b[3]), activate: false);
                else if (state == DockState.AutoHide) {
                    if (window is ToolWindow tool && Numbers ((string?) element.Attribute ("AutoHideSize")) is { Length: 2 } size)
                        tool.AutoHideSize = new Size (size[0], size[1]);

                    AutoHide (window, Enum.TryParse<AutoHidePosition> ((string?) element.Attribute ("AutoHidePosition"), out var side) ? side : AutoHidePosition.Auto);
                } else
                    window.DockState = state;

                if ((string?) element.Attribute ("Selected") == "true")
                    selected.Add (window);
            }

            // Selection last, once every window is back in the state that decides whether it has a tab.
            foreach (var window in selected)
                ActivateWindow (window);
        }

        private static string Join (params int[] values) => string.Join (",", values.Select (v => v.ToString (System.Globalization.CultureInfo.InvariantCulture)));

        // "250, 180" or "250,180,640,330": the numbers a layout writes a point, size or rectangle as; null when one is not a number.
        private static int[]? Numbers (string? text)
        {
            if (string.IsNullOrWhiteSpace (text))
                return null;

            var parts = text!.Split (',');
            var result = new int[parts.Length];

            for (var i = 0; i < parts.Length; i++)
                if (!int.TryParse (parts[i].Trim (), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result[i]))
                    return null;

            return result;
        }

        // A layout Telerik's own dock saved -- what an app that ran on Telerik has stored: the dock's control
        // tree, each tab strip listing its windows in tab order with their Text, and the strip's
        // SelectedIndex. Measured on Telerik (UI for WinForms 2026.2.520), loading one gives each listed
        // window the saved Text and tab order and selects the saved tab, and a window saved hidden comes back
        // as a tab. Its floating windows (SerializableFloatingWindows: a Location, a ClientSize and the
        // placeholders of the windows floated together) float again there, and the windows its auto-hide groups
        // name (SerializableAutoHideContainer, a placeholder's AutoHideSize with each) auto-hide on their side.
        //
        // A window the layout does not name is NOT handled the same way for both kinds. Measured on
        // 2026.2.520 by saving a layout and loading it into a dock holding one extra window of each kind:
        //
        //   tool window      DockState.Hidden, Parent null, IsDisposed FALSE -- hidden, kept alive
        //   document window  DockState.Hidden, Parent null, IsDisposed TRUE  -- hidden and disposed
        //
        // and each named window takes the state of the strip that names it: TabbedDocument from a
        // DocumentTabStrip, Docked from a ToolTabStrip. That holds whatever state the window is in when the
        // load starts. Measured on 2026.2.520, loading the same document into a dock whose twFilter was
        // auto-hidden, and again into one where it was floating: both came back Docked and visible in a
        // ToolTabStrip, because the saved tree puts twFilter in one. An auto-hidden window is named by a
        // ToolTabStrip AND by the auto-hide container, and the auto-hide pass below runs last, so a window
        // the layout really does hide still ends AutoHide.
        //
        // NOT read: the split tree. Telerik's document is a tree of nested RadSplitContainers, each with a
        // SizeInfo, and loading one restores the saved sizes (a strip forced to 377 wide came back at the
        // saved 198). This compat layer has no SizeInfo/SplitPanel engine at all (see DockStrip), so a
        // layout restores which windows are in which strip, in what order, with what text and state, but
        // not how the strips are arranged or sized. Telerik also CREATES a window the layout names that the
        // dock does not have, by deserialising it from the element; matching by Name as this does cannot,
        // so such a window stays absent.
        private void LoadTelerikLayout (System.Xml.Linq.XElement root)
        {
            var windows = OwnWindows ().Concat (detached_windows.Where (w => !w.IsDisposed))
                .Where (w => !string.IsNullOrEmpty (w.Name))
                .GroupBy (w => w.Name, StringComparer.Ordinal)
                .ToDictionary (g => g.Key, g => g.First (), StringComparer.Ordinal);

            var selected = new List<DockWindowBase> ();

            // Every window the document names anywhere -- strips, floating groups, auto-hide groups. The
            // sweep after the strip loop needs this: a window can sit in a strip the layout never mentions,
            // and the per-strip pass below only ever reaches strips that hold a named window, so without it
            // an extra panel stayed docked and visible where Telerik hides it.
            var named = new HashSet<string> (StringComparer.Ordinal);

            foreach (var element in root.Descendants ()) {
                if ((string?) element.Attribute ("Name") is { Length: > 0 } element_name)
                    named.Add (element_name);
                if ((string?) element.Attribute ("DockWindowName") is { Length: > 0 } placeholder_name)
                    named.Add (placeholder_name);
            }

            // Hidden the way Telerik leaves it: out of its strip, Hidden, and disposed only if it is a
            // document. Disposing a tool window as well used to take a panel out of the form for good --
            // the application still held the reference, but the control behind it was gone.
            static void Drop (DockWindowBase window)
            {
                window.DockState = DockState.Hidden;
                window.Parent?.Controls.Remove (window);

                if (window is DocumentWindow)
                    window.Dispose ();
            }

            foreach (var strip_element in root.Descendants ().Where (e => e.Name.LocalName.EndsWith ("TabStrip", StringComparison.Ordinal))) {
                var listed = new List<DockWindowBase> ();
                var documents = strip_element.Name.LocalName.EndsWith ("DocumentTabStrip", StringComparison.Ordinal);

                foreach (var element in strip_element.Element ("Controls")?.Elements () ?? Enumerable.Empty<System.Xml.Linq.XElement> ()) {
                    if ((string?) element.Attribute ("Name") is not { } name || !windows.TryGetValue (name, out var window))
                        continue;

                    if ((string?) element.Attribute ("Text") is { } text)
                        window.Text = text;

                    // The strip the layout puts it in decides its state, as Telerik's deserialised tree does.
                    // Without this a document restored into the document strip kept whatever state it was
                    // built with -- Docked, where Telerik gives TabbedDocument. A window that is floating or
                    // auto-hidden right now takes it too: the saved tree holds it in a strip, so that is
                    // where Telerik puts it back, and leaving those two states alone used to strand a panel
                    // on its auto-hide tab for the rest of the session with no way back to the layout the
                    // user saved.
                    var state = documents ? DockState.TabbedDocument : DockState.Docked;

                    if (window.DockState != state)
                        window.DockState = state;

                    listed.Add (window);
                }

                if (listed.Count == 0 || listed[0].Parent is not { } strip)
                    continue;

                for (var i = 0; i < listed.Count; i++)
                    if (listed[i].Parent == strip)
                        strip.Controls.SetChildIndex (listed[i], i);

                foreach (var unlisted in strip.Controls.OfType<DockWindowBase> ().Except (listed).ToList ())
                    Drop (unlisted);

                if (int.TryParse ((string?) strip_element.Attribute ("SelectedIndex"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
                    && index >= 0 && index < listed.Count)
                    selected.Add (listed[index]);
            }

            // The windows the document never names, wherever they sit. Only docked ones: what Telerik does
            // with an unnamed window that is floating or auto-hidden was not measured, and leaving those
            // alone keeps this to what was. A window with no Name cannot be matched by name either way, so
            // it is left alone rather than guessed at.
            foreach (var window in windows.Values.ToList ())
                if (!named.Contains (window.Name) && window.DockState is DockState.Docked or DockState.TabbedDocument)
                    Drop (window);

            DockWindowBase? Placeholder (System.Xml.Linq.XElement element)
                => (string?) element.Attribute ("DockWindowName") is { } name && windows.TryGetValue (name, out var window) ? window : null;

            foreach (var floating in root.Element ("SerializableFloatingWindows")?.Elements () ?? Enumerable.Empty<System.Xml.Linq.XElement> ()) {
                if (Numbers ((string?) floating.Attribute ("Location")) is not { Length: 2 } location
                    || Numbers ((string?) floating.Attribute ("ClientSize")) is not { Length: 2 } client)
                    continue;

                var floated = floating.Descendants ().Where (e => e.Name.LocalName.EndsWith ("DockWindowPlaceholder", StringComparison.Ordinal))
                    .Select (Placeholder).OfType<DockWindowBase> ().ToList ();

                if (floated.Count == 0)
                    continue;

                FloatingWindow? host = null;

                foreach (var window in floated) {
                    Float (window, new Rectangle (location[0], location[1], client[0], client[1]), activate: false, host);
                    host ??= window.floating_parent;
                }

                if (host is not null)
                    host.ClientSize = new Size (client[0], client[1]);
            }

            if (root.Element ("SerializableAutoHideContainer") is { } auto_hide) {
                foreach (var (group_name, side) in new[] { ("LeftAutoHideGroups", AutoHidePosition.Left), ("TopAutoHideGroups", AutoHidePosition.Top), ("RightAutoHideGroups", AutoHidePosition.Right), ("BottomAutoHideGroups", AutoHidePosition.Bottom) }) {
                    foreach (var placeholder in auto_hide.Element (group_name)?.Descendants ().Where (e => e.Name.LocalName.EndsWith ("DockWindowPlaceholder", StringComparison.Ordinal)) ?? Enumerable.Empty<System.Xml.Linq.XElement> ()) {
                        if (Placeholder (placeholder) is not { } window)
                            continue;

                        if (window is ToolWindow tool && Numbers ((string?) placeholder.Attribute ("AutoHideSize")) is { Length: 2 } size)
                            tool.AutoHideSize = new Size (size[0], size[1]);

                        AutoHide (window, side);
                    }
                }
            }

            foreach (var window in selected)
                ActivateWindow (window);
        }

        // The windows in this dock's own strips: not those of a dock nested inside one of its windows, which
        // that dock's own layout describes.
        private IEnumerable<DockWindowBase> OwnWindows ()
        {
            var stack = new Stack<Control> ();

            foreach (Control c in Controls)
                stack.Push (c);

            while (stack.Count > 0) {
                var c = stack.Pop ();

                if (c is DockWindowBase window) {
                    yield return window;
                    continue;
                }

                foreach (Control child in c.Controls)
                    stack.Push (child);
            }
        }

        /// <summary>Restores a layout from a file. See <see cref="LoadFromXml(System.IO.Stream)"/>.</summary>
        public void LoadFromXml (string fileName)
        {
            using var file = System.IO.File.OpenRead (fileName);
            LoadFromXml (file);
        }

        // Every dock window anywhere under this dock, tool windows included.
        internal IEnumerable<DockWindowBase> AllWindowsInTree ()
        {
            var stack = new Stack<Control> ();

            foreach (Control c in Controls)
                stack.Push (c);

            while (stack.Count > 0) {
                var c = stack.Pop ();

                if (c is DockWindowBase window)
                    yield return window;

                foreach (Control child in c.Controls)
                    stack.Push (child);
            }
        }

        private readonly List<ToolWindow> _toolWindows = new ();

        /// <summary>Gets or sets the active dock window.</summary>
        /// <remarks>Setting it activates the window as of W6 mechanisms (#176) -- its tab is selected,
        /// with the same Leave, Enter and <see cref="SelectedTabChanged"/> a click raises -- where it
        /// used to be a stored field the dock never looked at.</remarks>
        public DockWindowBase? ActiveWindow {
            get => active_window;
            set {
                if (value is null) {
                    active_window = null;
                    return;
                }

                ActivateWindow (value);
                active_window = value;
            }
        }

        private DockWindowBase? active_window;
        /// <summary>Gets or sets the main document container.</summary>
        public DocumentContainer? MainDocumentContainer { get; set; }
        /// <summary>Gets or sets whether the main document container is visible.</summary>
        public bool MainDocumentContainerVisible { get; set; } = true;

        // Telerik docking services the compat dock can hand back. Cached per dock so repeated
        // GetService(Of ContextMenuService)() calls (and the AddHandler on the returned service that
        // real docking code does) see the same instance rather than a NullReference.
        private ContextMenuService? _contextMenuService;

        /// <summary>Returns a docking service, or null if unsupported.</summary>
        public new object? GetService (Type serviceType)
            => serviceType == typeof (ContextMenuService) ? (_contextMenuService ??= new ContextMenuService ()) : null;

        /// <summary>
        /// Returns a docking service. <see cref="ContextMenuService"/> is supported (its event is never
        /// raised by the compat dock, but code subscribes to it); other service types return null.
        /// </summary>
        public T? GetService<T> () where T : class => GetService (typeof (T)) as T;

        /// <summary>Docks the specified window, making it a child of this dock.</summary>
        /// <remarks>
        /// This used to do nothing but remember tool windows in a list. Nothing was ever parented, so a
        /// window docked through this API never appeared, and <c>AllDocumentWindows</c> -- a real walk
        /// of the control tree -- could not find a document that had been docked rather than added to a
        /// tab strip by hand.
        ///
        /// Documents go into the document tab strip, which is the structure the designer generates and
        /// the one <c>DockStrip</c> lays tabs out over; tool windows are parented to the dock itself.
        /// A window that already has a parent is left where it is rather than reparented, so docking a
        /// window twice is not a move.
        /// </remarks>
        public void DockWindow (DockWindowBase window, DockPosition position = DockPosition.Fill)
            => DockWindowCore (window, null, position);

        /// <summary>Docks the specified window relative to another: <see cref="DockPosition.Fill"/> makes it a tab of the other's strip.</summary>
        public void DockWindow (DockWindowBase window, DockWindowBase relativeTo, DockPosition position)
            => DockWindowCore (window, relativeTo, position);

        // A floating or auto-hidden window leaves its floating form or auto-hide tab first and becomes Docked, as
        // Telerik's does (measured: DockStateChanging and DockStateChanged, then the document is active again). A tool
        // window docked to a side gets a strip of its own there (see DockToolStrip); one docked to Fill is parented
        // to the dock as it always was. A window that already has a parent in the dock is left where it is.
        private void DockWindowCore (DockWindowBase window, DockWindowBase? relativeTo, DockPosition position)
        {
            Guard.ThrowIfNull (window);

            var added = !Has (window);

            if (window is ToolWindow tw && !_toolWindows.Contains (tw))
                _toolWindows.Add (tw);

            var leaving = window.DockState is DockState.Floating or DockState.AutoHide && window.manager is not null;

            if (leaving) {
                if (!RaiseDockStateChanging (window, DockState.Docked))
                    return;

                Detach (window);
                detached_windows.Remove (window);
                window.SetStateField (DockState.Docked);
                window.Visible = true;
            } else if (window.Parent is not null) {
                return;
            }

            window.manager = this;

            if (relativeTo?.Parent is ToolTabStrip or DocumentTabStrip && position == DockPosition.Fill && IsInDock (relativeTo.Parent))
                relativeTo.Parent.Controls.Add (window);
            else if (window is DocumentWindow)
                GetDefaultDocumentTabStrip (createIfMissing: true).Controls.Add (window);
            else if (position == DockPosition.Fill)
                Controls.Add (window);
            else
                DockToolStrip (window, position);

            if (leaving) {
                RaiseDockStateChanged (window);
                ActivateDocument ();
            } else if (added) {
                RaiseDockWindowAdded (window);
            }
        }

        /// <summary>Gets the windows in the specified state.</summary>
        /// <remarks>
        /// The state argument used to be ignored entirely -- every call returned the same list of tool
        /// windows, so asking for the floating windows and asking for the hidden ones gave the same
        /// answer, and neither was right.
        /// </remarks>
        public IEnumerable<DockWindowBase> GetWindows (DockState state)
            => AllDockWindows ().Where (w => w.DockState == state);

        /// <summary>Gets whether a dock window with the given <see cref="Control.Name"/> belongs to the dock.</summary>
        /// <remarks>Matched on the name, not the caption, as Telerik's is; a window closed to hidden still belongs to it
        /// (measured on UI.for.WinForms 2026.2.520).</remarks>
        public bool Contains (string name)
            => AllDockWindows ().Any (w => string.Equals (w.Name, name, StringComparison.Ordinal));

        /// <summary>Gets all dock windows (Telerik-shaped collection with the ToolWindows view).</summary>
        public DockWindowCollection DockWindows => new DockWindowCollection (AllDockWindows ());

        // Tool windows this dock was told about, plus every document in the control tree. Documents
        // were missing before, so DockWindows.DocumentWindows was always empty however the dock was
        // populated -- including by the designer-generated structure the rest of the layout reads.
        private IEnumerable<DockWindowBase> AllDockWindows ()
            => _toolWindows.Cast<DockWindowBase> ().Concat (AllDocumentWindows ()).Concat (detached_windows.Where (w => !w.IsDisposed)).Distinct ();

        /// <summary>Closes the specified dock window: removes it from this dock and closes it.</summary>
        /// <remarks>
        /// Honours the window's <see cref="DockWindowBase.CloseAction"/>, which was stored and never
        /// read -- so a window configured to dispose on close was only hidden, and an application that
        /// set <c>CloseAndDispose</c> to release a document's resources kept every one of them alive.
        /// </remarks>
        public void CloseWindow (DockWindowBase window)
        {
            if (window is ToolWindow tool)
                _toolWindows.Remove (tool);

            window.manager ??= this;
            window.Close ();

            if (window.IsDisposed)
                detached_windows.Remove (window);

            if (!AllDockWindows ().Contains (window))
                RaiseDockWindowRemoved (window);
        }

        /// <summary>Gets the tab strip hosting document windows, creating one if asked to.</summary>
        /// <remarks>
        /// Returns the first strip inside <see cref="MainDocumentContainer"/> (or inside the dock, when
        /// no main container has been set) -- the structure the designer generates and the one
        /// <c>DockStrip</c> lays tabs out over. It used to return a strip that was never parented to
        /// anything, so a document added through it was invisible and unreachable whatever the caller
        /// did next.
        /// </remarks>
        public DocumentTabStrip GetDefaultDocumentTabStrip (bool createIfMissing)
        {
            var host = (Majorsilence.Forms.Control?)MainDocumentContainer ?? this;

            foreach (Majorsilence.Forms.Control child in host.Controls)
                if (child is DocumentTabStrip existing)
                    return existing;

            if (!createIfMissing)
                return _defaultDocumentTabStrip;

            // The application may supply the strip (DockTabStripNeeded, W6 mechanisms).
            var needed = new DockTabStripNeededEventArgs ();
            DockTabStripNeeded?.Invoke (this, needed);

            var strip = needed.Strip as DocumentTabStrip ?? new DocumentTabStrip ();

            if (strip.Parent is null)
                host.Controls.Add (strip);

            return strip;
        }

        // Returned only when the caller asked not to create one. Detached, as it always was -- the
        // alternative is a nullable return, which this API's Telerik shape does not have.
        private readonly DocumentTabStrip _defaultDocumentTabStrip = new ();

        /// <summary>Gets the document-window manager. Stub over the same window list DockWindows tracks.</summary>
        public RadDockDocumentManager DocumentManager => new RadDockDocumentManager (this);

        // SelectedTabChanged is declared above with Telerik-typed SelectedTabChangedEventArgs.
    }

    /// <summary>Compat stand-in for Telerik's RadDock document manager (RadDock.DocumentManager).</summary>
    public class RadDockDocumentManager
    {
        private readonly RadDock owner;

        internal RadDockDocumentManager (RadDock owner) => this.owner = owner;

        /// <summary>Gets the open document windows, in dock order.</summary>
        public IReadOnlyList<DocumentWindow> DocumentArray => owner.AllDocumentWindows ().ToList ();
    }

    /// <summary>Base for Telerik dock windows. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public abstract class DockWindowBase : Panel
    {
        /// <summary>Gets the root element of the dock window (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>
        /// Default style for dock windows. Telerik dock windows paint their own THEMED background
        /// (documents/tools show the theme surface color regardless of the form's BackColor), so the
        /// compat window sets one explicitly rather than inheriting the WinForms-ambient background
        /// from its parent -- otherwise a form-level BackColor would bleed through every document.
        /// </summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Majorsilence.Forms.Control.DefaultStyle,
            (style) => style.BackgroundColor = Theme.BackgroundColor);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets the dock state.</summary>
        /// <remarks>
        /// As of W6 mechanisms (#176) <see cref="DockState.Hidden"/> takes the window out of view: out of
        /// its strip's tabs when it has one, invisible when it sits in the dock directly. Any other
        /// state brings it back. The state it leaves is kept in <see cref="PreviousDockState"/>.
        /// </remarks>
        public DockState DockState {
            get => dock_state;
            set {
                if (dock_state == value)
                    return;

                // Floating and auto-hide are the dock's to do, as are the ways back from them; a window that belongs to
                // no dock yet only records the state, as a designer's assignment does.
                if (DockManager is { } dock && (value is DockState.Floating or DockState.AutoHide || dock_state is DockState.Floating or DockState.AutoHide)) {
                    dock.SetWindowState (this, value);
                    return;
                }

                SetStateDirect (value);
            }
        }

        // The state change the window makes for itself: in and out of its strip's tabs.
        internal void SetStateDirect (DockState value)
        {
            {
                if (dock_state == value)
                    return;

                // Hiding the selected tab hands it on, to the tab after it or else the one before.
                var successor = value == DockState.Hidden ? DockStrip.Successor (this) : null;

                PreviousDockState = dock_state;
                dock_state = value;

                if (successor is not null)
                    DockStrip.ActivateSuccessor (successor);

                if (Parent is DocumentTabStrip or ToolTabStrip)
                    Parent.PerformLayout ();
                else
                    Visible = value != DockState.Hidden;

                if (Parent is ToolTabStrip strip && DockManager is { } manager_dock) {
                    manager_dock.UpdateStripVisibility (strip);
                    manager_dock.PerformLayout ();
                }
            }
        }

        // The state change the dock makes moving the window between floating, auto-hide and docked: the state alone.
        // Telerik's PreviousDockState only moves when a window is hidden (measured), so it stays.
        internal void SetStateField (DockState value) => dock_state = value;

        private DockState dock_state = DockState.Docked;

        // The dock that floated, auto-hid or docked the window, the strip it was docked in and its place there, the
        // floating form it is (or was last) in and the side it auto-hides to.
        internal RadDock? manager { get; set; }
        internal Control? home { get; set; }
        internal int home_index { get; set; }
        internal FloatingWindow? floating_parent { get; set; }
        internal AutoHidePosition auto_hide_side { get; set; } = AutoHidePosition.Left;

        /// <summary>Gets the dock the window belongs to: the one it is in, or the one holding it floating or auto-hidden.</summary>
        public RadDock? DockManager {
            get {
                for (var p = Parent; p is not null; p = p.Parent)
                    if (p is RadDock dock)
                        return dock;

                return manager is { IsDisposed: false } held ? held : null;
            }
        }

        /// <summary>Gets the floating form the window is in, while it floats.</summary>
        public FloatingWindow? FloatingParent => dock_state == DockState.Floating && floating_parent is { } host && ReferenceEquals (Parent, host.Strip) ? host : null;

        /// <summary>Gets whether the window floats.</summary>
        public bool IsInFloatingMode => FloatingParent is not null;

        /// <summary>Gets the dock state the window was in before the current one.</summary>
        /// <remarks>Kept by <see cref="DockState"/> as of W6 mechanisms (#176); settable for designer code.</remarks>
        public DockState PreviousDockState { get; set; } = DockState.Docked;
        /// <summary>Gets or sets which dock states this window may transition to. Stub.</summary>
        public AllowedDockState AllowedDockState { get; set; } = AllowedDockState.All;
        /// <summary>Gets or sets how the window scales with DPI. Stored for WinForms designer compat.</summary>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Dpi;
        /// <summary>Gets or sets which caption buttons are shown. Defaults to all.</summary>
        public ToolStripCaptionButtons ToolCaptionButtons { get; set; } = ToolStripCaptionButtons.All;
        /// <summary>Gets or sets the close action.</summary>
        public DockWindowCloseAction CloseAction { get; set; } = DockWindowCloseAction.Hide;
        /// <summary>Gets or sets the size of the floating form the window floats in: 300 x 300 by default, as Telerik's.</summary>
        public Size DefaultFloatingSize { get; set; } = FloatingWindow.DefaultFloatingSize;
        /// <summary>Closes the window, honouring <see cref="CloseAction"/>.</summary>
        /// <remarks>
        /// <see cref="DockWindowCloseAction.Hide"/> hides it; <see cref="DockWindowCloseAction.CloseAndDispose"/>
        /// disposes it. The property was stored and nothing read it, so closing always meant hiding.
        /// </remarks>
        public void Close ()
        {
            // In a dock, with the events Telerik raises around a close (measured): DockStateChanging (Hidden),
            // DockWindowClosing, DockStateChanged and DockWindowClosed; a floating or auto-hidden window leaves its
            // form or tab.
            if (DockManager is { } dock) {
                dock.CloseCore (this, CloseNow);
                return;
            }

            CloseNow ();
        }

        private void CloseNow ()
        {
            if (CloseAction == DockWindowCloseAction.CloseAndDispose) {
                CloseAndDispose ();
                return;
            }

            // Hidden, as Telerik's hide-on-close is: the tab goes too, not only the content.
            HideDirect ();
            Visible = false;
        }

        // Hidden, remembering the state it leaves: what Show brings back.
        private void HideDirect ()
        {
            if (dock_state == DockState.Hidden)
                return;

            if (dock_state is DockState.Floating or DockState.AutoHide && DockManager is { } dock)
                dock.Detach (this);

            SetStateDirect (DockState.Hidden);
        }
        /// <summary>Closes and disposes the window.</summary>
        public void CloseAndDispose () { Visible = false; Dispose (); }

        /// <summary>Hides the window from its dock: its tab goes, and the next tab is activated if it was active.</summary>
        /// <remarks>Upstream's DockWindow.Hide sets the dock state to Hidden. Measured on Telerik (UI for
        /// WinForms 2026.2.520): hiding the active document made the one after it active (the one
        /// before when it was last), raising Leave and Enter but not SelectedTabChanged, and the hidden window
        /// left its strip. Control.Hide, which this used to be, only cleared Visible, so the
        /// tab stayed and the next tab click showed the window again.</remarks>
        public new void Hide () => HideDirect ();

        /// <summary>Brings a hidden window back to the state it had, as the first tab of its strip; otherwise shows it.</summary>
        /// <remarks>Measured on Telerik: Show after Hide put a document back as the first tab of its strip,
        /// wherever it had been, and the tab that was active stayed active.</remarks>
        public new void Show ()
        {
            if (DockState != DockState.Hidden) {
                base.Show ();
                return;
            }

            Parent?.Controls.SetChildIndex (this, 0);

            // Back into the floating form or onto the auto-hide tab it was hidden from.
            if (PreviousDockState is DockState.Floating or DockState.AutoHide && DockManager is { } dock) {
                dock.SetWindowState (this, PreviousDockState);
                return;
            }

            DockState = PreviousDockState;
        }
    }

    /// <summary>
    /// Telerik-compat dock-window collection: enumerates all windows and exposes the
    /// <see cref="ToolWindows"/> view Telerik code filters on.
    /// </summary>
    public class DockWindowCollection : IEnumerable<DockWindowBase>
    {
        private readonly IReadOnlyList<DockWindowBase> windows;

        internal DockWindowCollection (IEnumerable<DockWindowBase> windows) => this.windows = windows.ToList ();

        /// <summary>Gets the number of dock windows.</summary>
        public int Count => windows.Count;

        /// <summary>Gets the dock window with the specified name, or null.</summary>
        public DockWindowBase? this[string name]
            => windows.FirstOrDefault (w => string.Equals (w.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Gets the tool windows among the dock windows.</summary>
        public IEnumerable<ToolWindow> ToolWindows => windows.OfType<ToolWindow> ();

        /// <summary>Gets the document windows among the dock windows.</summary>
        public IEnumerable<DocumentWindow> DocumentWindows => windows.OfType<DocumentWindow> ();

        /// <inheritdoc/>
        public IEnumerator<DockWindowBase> GetEnumerator () => windows.GetEnumerator ();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>Telerik-compat tool window.</summary>
    public class ToolWindow : DockWindowBase, ISupportInitializeCompat
    {
        /// <summary>Document-mode buttons setting. Stored for Telerik compat.</summary>
        public object? DocumentButtons { get; set; }

        /// <summary>Initializes a new instance.</summary>
        public ToolWindow () { }
        /// <summary>Initializes a new instance with the specified caption.</summary>
        public ToolWindow (string caption) { Caption = caption; Text = caption; }

        /// <summary>Gets or sets the caption.</summary>
        public string Caption { get; set; } = string.Empty;
        // ToolCaptionButtons is inherited from DockWindowBase.
        /// <summary>Gets or sets how far the window slides out when auto-hidden; empty until it is first auto-hidden,
        /// when it takes its strip's size, as Telerik's.</summary>
        public Size AutoHideSize { get; set; }

        /// <summary>Floats the window at the given screen bounds.</summary>
        public void Float (Rectangle bounds) => DockManager?.FloatWindow (this, bounds);

        /// <summary>Floats the window at the given screen location and size.</summary>
        public void Float (Point location, Size size) => Float (new Rectangle (location, size));

        /// <summary>Auto-hides the window, with the other windows of its strip.</summary>
        public void AutoHide () => DockManager?.AutoHideWindow (this);
        // CloseAction and DefaultFloatingSize are inherited from DockWindowBase.
        /// <summary>Gets the tab strip hosting this window.</summary>
        /// <remarks>The strip the window is in as of W6 mechanisms (#176); a detached strip while it is
        /// in none, so chained calls stay safe.</remarks>
        public ToolTabStrip TabStrip => Parent as ToolTabStrip ?? detached_strip;

        private readonly ToolTabStrip detached_strip = new ();
    }

    /// <summary>Telerik-compat document window.</summary>
    public class DocumentWindow : DockWindowBase, ISupportInitializeCompat
    {
        /// <summary>Initializes a new instance.</summary>
        public DocumentWindow () { }
        /// <summary>Initializes a new instance with the specified caption.</summary>
        public DocumentWindow (string caption) { Text = caption; }
    }

    /// <summary>
    /// Telerik-compat concrete dock window (Telerik.WinControls.UI.Docking.DockWindow) — used where code
    /// declares a plain DockWindow rather than a Tool/Document window. Backed by <see cref="Panel"/>.
    /// </summary>
    public class DockWindow : DockWindowBase, ISupportInitializeCompat
    {
        /// <summary>Initializes a new instance.</summary>
        public DockWindow () { }
        /// <summary>Initializes a new instance with the specified caption.</summary>
        public DockWindow (string caption) { Text = caption; }
    }

    /// <summary>
    /// Telerik-compat auto-hide group: a set of dock windows sharing the same auto-hide tab strip.
    /// </summary>
    public class AutoHideGroup
    {
        /// <summary>Initializes an empty auto-hide group.</summary>
        public AutoHideGroup () { }

        /// <summary>Initializes an auto-hide group containing the specified windows.</summary>
        public AutoHideGroup (params DockWindowBase[] windows) => Windows.AddRange (windows);

        /// <summary>Gets the windows belonging to this group.</summary>
        public List<DockWindowBase> Windows { get; } = new ();
    }

    /// <summary>
    /// Telerik-compat placeholder standing for a dock window in a designer-recorded layout, such as the
    /// windows of an <see cref="AutoHideGroup"/> in <see cref="RadDock.SerializableAutoHideContainer"/>.
    /// A dock window itself, as Telerik's is, so the designer's Name, Text, Font, Location, Size and
    /// PreviousDockState lines apply to it.
    /// </summary>
    public class DockWindowPlaceholder : DockWindow
    {
        /// <summary>Initializes a new, empty placeholder.</summary>
        public DockWindowPlaceholder () { }

        /// <summary>Initializes a placeholder for the specified window name.</summary>
        public DockWindowPlaceholder (string dockWindowName) => DockWindowName = dockWindowName;

        /// <summary>Initializes a placeholder standing for the specified window: its name and caption are copied, the placeholder's own Name and Text stay empty.</summary>
        public DockWindowPlaceholder (DockWindowBase window)
        {
            Guard.ThrowIfNull (window);

            DockWindowName = window.Name;
            DockWindowText = window.Text;
        }

        /// <summary>Gets or sets the name of the dock window this placeholder represents.</summary>
        /// <remarks>What <see cref="RadDock.SerializableAutoHideContainer"/> finds the window by.</remarks>
        public string DockWindowName { get; set; } = string.Empty;

        /// <summary>Gets or sets the caption of the dock window this placeholder represents. Independent of the placeholder's own Text.</summary>
        public string DockWindowText { get; set; } = string.Empty;

        /// <summary>Gets or sets the resolved dock window, once available.</summary>
        public DockWindowBase? DockWindow { get; set; }
    }

    /// <summary>The auto-hide groups of a <see cref="RadDock"/>, one collection per side, as a designer records them.</summary>
    public class DockAutoHideSerializationContainer
    {
        /// <summary>Gets the groups auto-hidden on the left side.</summary>
        public AutoHideGroupCollection LeftAutoHideGroups { get; } = new ();

        /// <summary>Gets the groups auto-hidden on the top side.</summary>
        public AutoHideGroupCollection TopAutoHideGroups { get; } = new ();

        /// <summary>Gets the groups auto-hidden on the right side.</summary>
        public AutoHideGroupCollection RightAutoHideGroups { get; } = new ();

        /// <summary>Gets the groups auto-hidden on the bottom side.</summary>
        public AutoHideGroupCollection BottomAutoHideGroups { get; } = new ();
    }

    /// <summary>A collection of <see cref="AutoHideGroup"/>s.</summary>
    public class AutoHideGroupCollection : System.Collections.ObjectModel.Collection<AutoHideGroup>
    {
    }

    /// <summary>Telerik-compat tool tab strip. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public partial class ToolTabStrip : Panel, ISupportInitializeCompat
    {
        // SelectedIndex, ActiveWindow, CaptionVisible, TabStripVisible and TabStripAlignment live with
        // the tab behaviour in RadDockingLayout.cs.

        /// <summary>Gets the root of the strip's element tree: its panel element, with the fill and border primitives of its chrome.</summary>
        /// <remarks>Setting the fill primitive's <see cref="RadElement.BackColor"/> fills the strip with it, as Telerik's does.</remarks>
        public RadElement RootElement => root_element ??= BuildChrome ();
        /// <summary>Gets the size info (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
        /// <summary>Gets or sets the splitter width. Stub.</summary>
        public int SplitterWidth { get; set; } = 4;
        /// <summary>Returns the strip element tree child at the given index (stub).</summary>
        public RadElement GetChildAt (int index) => RootElement.GetChildAt (index);
    }

    /// <summary>Telerik-compat document tab strip. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public partial class DocumentTabStrip : Panel, ISupportInitializeCompat
    {
        // SelectedIndex, SelectedTab, ActiveWindow, SelectedIndexChanged and DocumentButtons live with
        // the tab behaviour in RadDockingLayout.cs.

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets the size info (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();

        /// <summary>Selects the tab hosting the specified dock window, raising the standard
        /// activation events (same path as clicking the tab header).</summary>
        public void SelectTab (DockWindowBase window) => SelectWindowInternal (window);

        /// <summary>Gets the strip's visual element tree (stub; Items is always empty).</summary>
        public RadPageViewStripElement TabStripElement { get; } = new RadPageViewStripElement ();

        /// <summary>Returns the strip element tree child at the given index (stub).</summary>
        public RadElement GetChildAt (int index) => RootElement.GetChildAt (index);
    }

    /// <summary>Telerik-compat document container. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public partial class DocumentContainer : Panel, ISupportInitializeCompat
    {
        /// <summary>Whether the container is collapsed. Stored for Telerik compat.</summary>
        public bool Collapsed { get; set; }
        /// <summary>Gets or sets the orientation its document tab strips are laid out along; side by side by default
        /// (Vertical), as Telerik's.</summary>
        public Orientation Orientation { get; set; } = Orientation.Vertical;

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets how the container is sized in the dock.</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
        /// <summary>Gets or sets the selected tab. Stub.</summary>
        public object? SelectedTab { get; set; }
        /// <summary>Gets or sets the splitter width. Stub.</summary>
        public int SplitterWidth { get; set; } = 4;
    }

    /// <summary>Specifies the dock state of a Telerik dock window.</summary>
    public enum DockState
    {
        /// <summary>Docked to an edge.</summary>
        Docked = 0,
        /// <summary>A tabbed document.</summary>
        TabbedDocument = 1,
        /// <summary>Floating.</summary>
        Floating = 2,
        /// <summary>Hidden.</summary>
        Hidden = 3,
        /// <summary>Auto-hidden.</summary>
        AutoHide = 4
    }

    /// <summary>Specifies a dock position.</summary>
    public enum DockPosition
    {
        /// <summary>Fill.</summary>
        Fill = 0,
        /// <summary>Left.</summary>
        Left = 1,
        /// <summary>Right.</summary>
        Right = 2,
        /// <summary>Top.</summary>
        Top = 3,
        /// <summary>Bottom.</summary>
        Bottom = 4
    }

    /// <summary>Specifies the dock window type.</summary>
    public enum DockType
    {
        /// <summary>A tool window.</summary>
        ToolWindow = 0,
        /// <summary>A document window.</summary>
        Document = 1
    }

    /// <summary>Specifies what happens when a dock window is closed.</summary>
    public enum DockWindowCloseAction
    {
        /// <summary>Hide the window.</summary>
        Hide = 0,
        /// <summary>Close and dispose the window.</summary>
        CloseAndDispose = 1
    }

    /// <summary>Specifies the dock states a dock window is permitted to transition to. Compat for Telerik AllowedDockState.</summary>
    [Flags]
    public enum AllowedDockState
    {
        /// <summary>No dock state is allowed.</summary>
        None = 0,
        /// <summary>Docked to an edge is allowed.</summary>
        Docked = 1,
        /// <summary>Floating is allowed.</summary>
        Floating = 2,
        /// <summary>Auto-hide is allowed.</summary>
        AutoHide = 4,
        /// <summary>Hidden is allowed.</summary>
        Hidden = 8,
        /// <summary>Tabbed-document is allowed.</summary>
        TabbedDocument = 16,
        /// <summary>All dock states are allowed.</summary>
        All = Docked | Floating | AutoHide | Hidden | TabbedDocument
    }

    /// <summary>Specifies which caption buttons a <see cref="ToolWindow"/> shows. Compat for Telerik.WinControls.UI.Docking.ToolStripCaptionButtons.</summary>
    [Flags]
    public enum ToolStripCaptionButtons
    {
        /// <summary>No buttons.</summary>
        None = 0,
        /// <summary>The close button.</summary>
        Close = 1,
        /// <summary>The auto-hide (pin) button.</summary>
        AutoHide = 2,
        /// <summary>The menu (options) button.</summary>
        Menu = 4,
        /// <summary>All buttons.</summary>
        All = Close | AutoHide | Menu
    }

    /// <summary>Specifies which buttons a <see cref="DocumentTabStrip"/> shows. Compat for Telerik.WinControls.UI.Docking.DocumentStripButtons.</summary>
    [Flags]
    public enum DocumentStripButtons
    {
        /// <summary>No buttons.</summary>
        None = 0,
        /// <summary>The close button.</summary>
        Close = 1,
        /// <summary>The scroll buttons.</summary>
        Scroll = 2,
        /// <summary>The item-list (overflow) button.</summary>
        ItemList = 4,
        /// <summary>All buttons.</summary>
        All = Close | Scroll | ItemList
    }
}
