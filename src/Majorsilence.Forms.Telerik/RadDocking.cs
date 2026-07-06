using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat docking manager. Backed by <see cref="Majorsilence.Forms.Panel"/>. Docking is not
    /// implemented; windows are tracked and hosted as child panels so layout/code compiles and runs.
    /// </summary>
    public class RadDock : Panel
    {
        /// <summary>Gets or sets the split orientation. Stored for Telerik compat.</summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        /// <summary>Gets or sets the splitter width. Stored for Telerik compat.</summary>
        public int SplitterWidth { get; set; } = 4;

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Gets or sets whether auto-cleanup removes this dock's windows. Stored for compat.</summary>
        public bool IsCleanUpTarget { get; set; }

        /// <summary>Raised when a new tab strip is needed. Stub (never raised yet).</summary>
#pragma warning disable CS0067
        public event EventHandler<DockTabStripNeededEventArgs>? DockTabStripNeeded;

        /// <summary>Raised when the selected dock tab changes. Stub (the compat dock does not tab yet).</summary>
        public event EventHandler<SelectedTabChangedEventArgs>? SelectedTabChanged;
#pragma warning restore CS0067

        /// <summary>Saves the dock layout to a stream. Stub: writes an empty layout document.</summary>
        public void SaveToXml (System.IO.Stream stream)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes ("<DockLayout />");
            stream.Write (bytes, 0, bytes.Length);
        }

        /// <summary>Loads the dock layout from a stream. Stub: layout restore is not supported yet.</summary>
        public void LoadFromXml (System.IO.Stream stream) { }

        private readonly List<ToolWindow> _toolWindows = new ();

        /// <summary>Gets or sets the active dock window.</summary>
        public DockWindowBase? ActiveWindow { get; set; }
        /// <summary>Gets or sets the main document container.</summary>
        public DocumentContainer? MainDocumentContainer { get; set; }
        /// <summary>Gets or sets whether the main document container is visible.</summary>
        public bool MainDocumentContainerVisible { get; set; } = true;

        /// <summary>Returns a docking service. Stub returns null.</summary>
        public new object? GetService (Type serviceType) => null;
        /// <summary>Returns a docking service. Stub returns default.</summary>
        public T? GetService<T> () where T : class => null;

        /// <summary>Docks the specified window. Hosts it as a child panel.</summary>
        public void DockWindow (DockWindowBase window, DockPosition position = DockPosition.Fill)
        {
            if (window is ToolWindow tw && !_toolWindows.Contains (tw))
                _toolWindows.Add (tw);
        }

        /// <summary>Docks the specified window relative to another. Stub.</summary>
        public void DockWindow (DockWindowBase window, DockWindowBase relativeTo, DockPosition position) => DockWindow (window, position);

        /// <summary>Gets the windows in the specified state.</summary>
        public IEnumerable<DockWindowBase> GetWindows (DockState state) => _toolWindows;
        /// <summary>Gets all dock windows (Telerik-shaped collection with the ToolWindows view).</summary>
        public DockWindowCollection DockWindows => new DockWindowCollection (_toolWindows);

        /// <summary>Closes the specified dock window: removes it from this dock and hides it.</summary>
        public void CloseWindow (DockWindowBase window)
        {
            if (window is ToolWindow tool)
                _toolWindows.Remove (tool);

            window.Visible = false;
        }

        // SelectedTabChanged is declared above with Telerik-typed SelectedTabChangedEventArgs.
    }

    /// <summary>Base for Telerik dock windows. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public abstract class DockWindowBase : Panel
    {
        /// <summary>Gets or sets the dock state.</summary>
        public DockState DockState { get; set; } = DockState.Docked;
        /// <summary>Gets the previous dock state.</summary>
        public DockState PreviousDockState { get; set; } = DockState.Docked;
        /// <summary>Gets or sets which dock states this window may transition to. Stub.</summary>
        public AllowedDockState AllowedDockState { get; set; } = AllowedDockState.All;
        /// <summary>Gets or sets how the window scales with DPI. Stored for WinForms designer compat.</summary>
        public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Dpi;
        /// <summary>Closes the window (hides it).</summary>
        public void Close () => Visible = false;
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

        /// <summary>Gets the tool windows among the dock windows.</summary>
        public IEnumerable<ToolWindow> ToolWindows => windows.OfType<ToolWindow> ();

        /// <inheritdoc/>
        public IEnumerator<DockWindowBase> GetEnumerator () => windows.GetEnumerator ();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>Telerik-compat tool window.</summary>
    public class ToolWindow : DockWindowBase
    {
        /// <summary>Document-mode buttons setting. Stored for Telerik compat.</summary>
        public object? DocumentButtons { get; set; }

        /// <summary>Initializes a new instance.</summary>
        public ToolWindow () { }
        /// <summary>Initializes a new instance with the specified caption.</summary>
        public ToolWindow (string caption) { Caption = caption; Text = caption; }

        /// <summary>Gets or sets the caption.</summary>
        public string Caption { get; set; } = string.Empty;
        /// <summary>Gets or sets which caption buttons are shown. Defaults to all.</summary>
        public ToolStripCaptionButtons ToolCaptionButtons { get; set; } = ToolStripCaptionButtons.All;
        /// <summary>Gets or sets the auto-hide size. Stub.</summary>
        public Size AutoHideSize { get; set; }
        /// <summary>Gets or sets the default floating size. Stub.</summary>
        public Size DefaultFloatingSize { get; set; }
        /// <summary>Gets or sets the close action.</summary>
        public DockWindowCloseAction CloseAction { get; set; } = DockWindowCloseAction.Hide;
        /// <summary>Gets the tab strip hosting this window (stub).</summary>
        public ToolTabStrip TabStrip { get; } = new ToolTabStrip ();
    }

    /// <summary>Telerik-compat document window.</summary>
    public class DocumentWindow : DockWindowBase
    {
        /// <summary>Initializes a new instance.</summary>
        public DocumentWindow () { }
        /// <summary>Initializes a new instance with the specified caption.</summary>
        public DocumentWindow (string caption) { Text = caption; }
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
    public class ToolTabStrip : Panel
    {
        /// <summary>Gets or sets the selected tab index. Stored for Telerik compat.</summary>
        public int SelectedIndex { get; set; }

        /// <summary>Gets or sets whether the caption is visible. Stored for Telerik compat.</summary>
        public bool CaptionVisible { get; set; } = true;

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
    public class DocumentTabStrip : Panel
    {
        /// <summary>Gets or sets the selected tab index. Stored for Telerik compat.</summary>
        public int SelectedIndex { get; set; }

        /// <summary>Raised when the selected tab changes. Never raised by the compat strip (it does not tab yet).</summary>
#pragma warning disable CS0067
        public event EventHandler? SelectedIndexChanged;
#pragma warning restore CS0067

        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the size info (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
        /// <summary>Gets or sets the selected tab. Stub.</summary>
        public object? SelectedTab { get; set; }
        /// <summary>Gets or sets which document buttons show. Defaults to all.</summary>
        public DocumentStripButtons DocumentButtons { get; set; } = DocumentStripButtons.All;
        /// <summary>Returns the strip element tree child at the given index (stub).</summary>
        public RadElement GetChildAt (int index) => RootElement.GetChildAt (index);
    }

    /// <summary>Telerik-compat document container. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class DocumentContainer : Panel
    {
        /// <summary>Whether the container is collapsed. Stored for Telerik compat.</summary>
        public bool Collapsed { get; set; }

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
