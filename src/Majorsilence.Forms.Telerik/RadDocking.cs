using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat docking manager. Backed by <see cref="Majorsilence.Forms.Panel"/>. Docking is not
    /// implemented; windows are tracked and hosted as child panels so layout/code compiles and runs.
    /// </summary>
    public partial class RadDock : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets or sets the split orientation. Stored for Telerik compat.</summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        /// <summary>Gets or sets the splitter width. Stored for Telerik compat.</summary>
        public int SplitterWidth { get; set; } = 4;

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets whether auto-cleanup removes this dock's windows. Stored for compat.</summary>
        public bool IsCleanUpTarget { get; set; }

        /// <summary>Gets or sets whether the auto-hide tool tabs are visible. Stored for Telerik compat.</summary>
        public bool ToolTabsVisible { get; set; } = true;

        /// <summary>Gets or sets the alignment of the auto-hide tool tab strips. Stored for Telerik compat.</summary>
        public TabStripAlignment ToolTabsAlignment { get; set; } = TabStripAlignment.Bottom;

        /// <summary>Floats the specified dock window. Compat: records the Floating state (no separate window is shown).</summary>
        /// <remarks>Honours <see cref="DockWindowBase.AllowedDockState"/> as of W6 mechanisms (#176): a
        /// window not allowed to float stays as it is.</remarks>
        public void FloatWindow (DockWindowBase window)
        {
            if (window is not null && (window.AllowedDockState & AllowedDockState.Floating) != 0)
                window.DockState = DockState.Floating;
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
        /// its strip. Telerik's full layout also records split sizes and floating positions, which this
        /// dock does not lay out; <see cref="LoadFromXml(System.IO.Stream)"/> restores what is saved. A
        /// window with no <see cref="Control.Name"/> cannot be matched on the way back and is skipped.
        /// </remarks>
        public void SaveToXml (System.IO.Stream stream)
        {
            Guard.ThrowIfNull (stream);

            var root = new System.Xml.Linq.XElement ("DockLayout");

            foreach (var window in AllWindowsInTree ()) {
                if (string.IsNullOrEmpty (window.Name))
                    continue;

                root.Add (new System.Xml.Linq.XElement ("Window",
                    new System.Xml.Linq.XAttribute ("Name", window.Name),
                    new System.Xml.Linq.XAttribute ("DockState", window.DockState.ToString ()),
                    new System.Xml.Linq.XAttribute ("Selected", DockStrip.IsSelected (window) ? "true" : "false")));
            }

            new System.Xml.Linq.XDocument (root).Save (stream);
        }

        /// <summary>Saves the layout to a file. See <see cref="SaveToXml(System.IO.Stream)"/>.</summary>
        public void SaveToXml (string fileName)
        {
            using var file = System.IO.File.Create (fileName);
            SaveToXml (file);
        }

        /// <summary>Restores a layout <see cref="SaveToXml(System.IO.Stream)"/> wrote.</summary>
        /// <remarks>Windows are matched by name; a saved window that is not in the dock, or a window
        /// not in the saved layout, is left alone. A document that is not a well-formed layout throws
        /// <see cref="System.Xml.XmlException"/>.</remarks>
        public void LoadFromXml (System.IO.Stream stream)
        {
            Guard.ThrowIfNull (stream);

            var document = System.Xml.Linq.XDocument.Load (stream);
            var windows = AllWindowsInTree ()
                .Where (w => !string.IsNullOrEmpty (w.Name))
                .GroupBy (w => w.Name, StringComparer.Ordinal)
                .ToDictionary (g => g.Key, g => g.First (), StringComparer.Ordinal);

            var selected = new List<DockWindowBase> ();

            foreach (var element in document.Root?.Elements ("Window") ?? Enumerable.Empty<System.Xml.Linq.XElement> ()) {
                if ((string?) element.Attribute ("Name") is not { } name || !windows.TryGetValue (name, out var window))
                    continue;

                if (Enum.TryParse<DockState> ((string?) element.Attribute ("DockState"), out var state))
                    window.DockState = state;

                if ((string?) element.Attribute ("Selected") == "true")
                    selected.Add (window);
            }

            // Selection last, once every window is back in the state that decides whether it has a tab.
            foreach (var window in selected)
                ActivateWindow (window);
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
        {
            if (window is ToolWindow tw && !_toolWindows.Contains (tw))
                _toolWindows.Add (tw);

            if (window.Parent is not null)
                return;

            if (window is DocumentWindow)
                GetDefaultDocumentTabStrip (createIfMissing: true).Controls.Add (window);
            else
                Controls.Add (window);
        }

        /// <summary>Docks the specified window relative to another. Stub.</summary>
        public void DockWindow (DockWindowBase window, DockWindowBase relativeTo, DockPosition position) => DockWindow (window, position);

        /// <summary>Gets the windows in the specified state.</summary>
        /// <remarks>
        /// The state argument used to be ignored entirely -- every call returned the same list of tool
        /// windows, so asking for the floating windows and asking for the hidden ones gave the same
        /// answer, and neither was right.
        /// </remarks>
        public IEnumerable<DockWindowBase> GetWindows (DockState state)
            => AllDockWindows ().Where (w => w.DockState == state);

        /// <summary>Gets all dock windows (Telerik-shaped collection with the ToolWindows view).</summary>
        public DockWindowCollection DockWindows => new DockWindowCollection (AllDockWindows ());

        // Tool windows this dock was told about, plus every document in the control tree. Documents
        // were missing before, so DockWindows.DocumentWindows was always empty however the dock was
        // populated -- including by the designer-generated structure the rest of the layout reads.
        private IEnumerable<DockWindowBase> AllDockWindows ()
            => _toolWindows.Cast<DockWindowBase> ().Concat (AllDocumentWindows ());

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

            window.Close ();
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

                PreviousDockState = dock_state;
                dock_state = value;

                if (Parent is DocumentTabStrip or ToolTabStrip)
                    Parent.PerformLayout ();
                else
                    Visible = value != DockState.Hidden;
            }
        }

        private DockState dock_state = DockState.Docked;

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
        /// <summary>Gets or sets the default floating size. Stub.</summary>
        public Size DefaultFloatingSize { get; set; }
        /// <summary>Closes the window, honouring <see cref="CloseAction"/>.</summary>
        /// <remarks>
        /// <see cref="DockWindowCloseAction.Hide"/> hides it; <see cref="DockWindowCloseAction.CloseAndDispose"/>
        /// disposes it. The property was stored and nothing read it, so closing always meant hiding.
        /// </remarks>
        public void Close ()
        {
            if (CloseAction == DockWindowCloseAction.CloseAndDispose) {
                CloseAndDispose ();
                return;
            }

            // Hidden, as Telerik's hide-on-close is: the tab goes too, not only the content.
            DockState = DockState.Hidden;
            Visible = false;
        }
        /// <summary>Closes and disposes the window.</summary>
        public void CloseAndDispose () { Visible = false; Dispose (); }
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
        /// <summary>Gets or sets the auto-hide size. Stub.</summary>
        public Size AutoHideSize { get; set; }
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

    /// <summary>Telerik-compat placeholder marking where a dock window sits within a saved docking layout.</summary>
    public class DockWindowPlaceholder
    {
        /// <summary>Initializes a new, empty placeholder.</summary>
        public DockWindowPlaceholder () { }

        /// <summary>Initializes a placeholder for the specified window name.</summary>
        public DockWindowPlaceholder (string dockWindowName) => DockWindowName = dockWindowName;

        /// <summary>Gets or sets the name of the dock window this placeholder represents.</summary>
        public string DockWindowName { get; set; } = string.Empty;

        /// <summary>Gets or sets the resolved dock window, once available.</summary>
        public DockWindowBase? DockWindow { get; set; }
    }

    /// <summary>Telerik-compat tool tab strip. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public partial class ToolTabStrip : Panel, ISupportInitializeCompat
    {
        // SelectedIndex, ActiveWindow, CaptionVisible, TabStripVisible and TabStripAlignment live with
        // the tab behaviour in RadDockingLayout.cs.

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
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
        /// <summary>Gets or sets the split orientation. Stored for Telerik compat.</summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the size info (stub).</summary>
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
