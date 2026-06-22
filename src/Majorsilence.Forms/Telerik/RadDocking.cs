using System;
using System.Collections.Generic;
using System.Drawing;
using Majorsilence.Forms;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat docking manager. Backed by <see cref="Majorsilence.Forms.Panel"/>. Docking is not
    /// implemented; windows are tracked and hosted as child panels so layout/code compiles and runs.
    /// </summary>
    public class RadDock : Panel
    {
        private readonly List<ToolWindow> _toolWindows = new ();

        /// <summary>Gets or sets the active dock window.</summary>
        public DockWindowBase? ActiveWindow { get; set; }
        /// <summary>Gets or sets the main document container.</summary>
        public DocumentContainer? MainDocumentContainer { get; set; }
        /// <summary>Gets or sets whether the main document container is visible.</summary>
        public bool MainDocumentContainerVisible { get; set; } = true;

        /// <summary>Returns a docking service. Stub returns null.</summary>
        public object? GetService (Type serviceType) => null;
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
        /// <summary>Gets all dock windows.</summary>
        public IEnumerable<DockWindowBase> DockWindows => _toolWindows;

        /// <summary>Raised when the selected tab changes. Stub.</summary>
        public event EventHandler? SelectedTabChanged { add { } remove { } }
    }

    /// <summary>Base for Telerik dock windows. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public abstract class DockWindowBase : Panel
    {
        /// <summary>Gets or sets the dock state.</summary>
        public DockState DockState { get; set; } = DockState.Docked;
        /// <summary>Gets the previous dock state.</summary>
        public DockState PreviousDockState { get; set; } = DockState.Docked;
        /// <summary>Closes the window (hides it).</summary>
        public void Close () => Visible = false;
        /// <summary>Closes and disposes the window.</summary>
        public void CloseAndDispose () { Visible = false; Dispose (); }
    }

    /// <summary>Telerik-compat tool window.</summary>
    public class ToolWindow : DockWindowBase
    {
        /// <summary>Initializes a new instance.</summary>
        public ToolWindow () { }
        /// <summary>Initializes a new instance with the specified caption.</summary>
        public ToolWindow (string caption) { Caption = caption; Text = caption; }

        /// <summary>Gets or sets the caption.</summary>
        public string Caption { get; set; } = string.Empty;
        /// <summary>Gets or sets which caption buttons are shown. Stub.</summary>
        public object? ToolCaptionButtons { get; set; }
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

    /// <summary>Telerik-compat tool tab strip. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class ToolTabStrip : Panel
    {
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the size info (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
        /// <summary>Gets or sets the splitter width. Stub.</summary>
        public int SplitterWidth { get; set; } = 4;
    }

    /// <summary>Telerik-compat document tab strip. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class DocumentTabStrip : Panel
    {
        /// <summary>Gets the root element (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the size info (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
        /// <summary>Gets or sets the selected tab. Stub.</summary>
        public object? SelectedTab { get; set; }
        /// <summary>Gets or sets which document buttons show. Stub.</summary>
        public object? DocumentButtons { get; set; }
    }

    /// <summary>Telerik-compat document container. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class DocumentContainer : Panel
    {
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
}
