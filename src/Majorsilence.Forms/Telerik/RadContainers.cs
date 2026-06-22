using System;
using System.Drawing;
using Majorsilence.Forms;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat tabbed page view. Backed by <see cref="Majorsilence.Forms.TabControl"/>.</summary>
    public class RadPageView : TabControl
    {
        /// <summary>Gets the collection of pages (alias for <see cref="TabControl.TabPages"/>).</summary>
        public TabPageCollection Pages => TabPages;

        /// <summary>Gets or sets the selected page (alias for <see cref="TabControl.SelectedTabPage"/>).</summary>
        public TabPage? SelectedPage {
            get => SelectedTabPage;
            set => SelectedTabPage = value;
        }

        /// <summary>Gets or sets the default page. Stub.</summary>
        public TabPage? DefaultPage { get; set; }
        /// <summary>Gets or sets the item size mode. Stub.</summary>
        public PageViewItemSizeMode ItemSizeMode { get; set; } = PageViewItemSizeMode.EqualWidth;
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        private readonly RadPageViewStripElement _strip = new ();

        /// <summary>Returns the strip element at the given index (stub; index 0 is the tab strip).</summary>
        public RadPageViewStripElement GetChildAt (int index) => _strip;

        /// <summary>Raised when the selected page changes (alias for SelectedIndexChanged).</summary>
        public event EventHandler? SelectedPageChanged {
            add => SelectedIndexChanged += value;
            remove => SelectedIndexChanged -= value;
        }

        /// <summary>Raised before a page is removed. Stub.</summary>
        public event EventHandler? PageRemoving { add { } remove { } }
        /// <summary>Raised when a page is collapsed. Stub.</summary>
        public event EventHandler? PageCollapsed { add { } remove { } }
    }

    /// <summary>Telerik-compat page-view page. Backed by <see cref="Majorsilence.Forms.TabPage"/>.</summary>
    public class RadPageViewPage : TabPage
    {
        /// <summary>Gets or sets the tab item size. Stub.</summary>
        public SizeF ItemSize { get; set; }
        /// <summary>Gets the strip item element for this page (stub).</summary>
        public RadElement Item { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat page-view strip element (the tab header strip). Stub.</summary>
    public class RadPageViewStripElement : RadElement
    {
        /// <summary>Gets or sets which strip buttons are shown. Stub.</summary>
        public StripViewButtons StripButtons { get; set; } = StripViewButtons.None;
        /// <summary>Gets or sets whether each item shows a close button. Stub.</summary>
        public bool ShowItemCloseButton { get; set; }
        /// <summary>Gets or sets the item fit mode. Stub.</summary>
        public object? ItemFitMode { get; set; }
        /// <summary>Gets or sets the item size mode. Stub.</summary>
        public PageViewItemSizeMode ItemSizeMode { get; set; } = PageViewItemSizeMode.EqualWidth;
        /// <summary>Gets or sets the highlight color. Stub.</summary>
        public Color HighlightColor { get; set; } = Color.Empty;
    }

    /// <summary>Telerik-compat split container. Backed by <see cref="Majorsilence.Forms.SplitContainer"/>.</summary>
    public class RadSplitContainer : SplitContainer
    {
        /// <summary>Gets the root element of the container (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets whether this is a cleanup target during docking layout. Stub.</summary>
        public bool IsCleanUpTarget { get; set; }
        /// <summary>Gets the size info for the panel (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
    }

    /// <summary>Telerik-compat split panel. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class SplitPanel : Panel
    {
        /// <summary>Gets the root element of the panel (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the size info for the panel (stub).</summary>
        public SplitPanelSizeInfo SizeInfo { get; } = new SplitPanelSizeInfo ();
        /// <summary>Gets or sets whether the panel is collapsed.</summary>
        public bool Collapsed { get; set; }
        /// <summary>Gets or sets the splitter width. Stub.</summary>
        public int SplitterWidth { get; set; } = 4;
        /// <summary>Gets or sets the panel orientation. Stub.</summary>
        public Orientation Orientation { get; set; } = Orientation.Horizontal;
    }

    /// <summary>Telerik-compat split-panel size information. Stub.</summary>
    public class SplitPanelSizeInfo
    {
        /// <summary>Gets or sets the sizing mode.</summary>
        public SplitPanelSizeMode SizeMode { get; set; } = SplitPanelSizeMode.Absolute;
        /// <summary>Gets or sets the absolute size.</summary>
        public SizeF AbsoluteSize { get; set; }
        /// <summary>Gets or sets the splitter correction.</summary>
        public SizeF SplitterCorrection { get; set; }
    }

    /// <summary>Specifies how a split panel is sized. Compat for Telerik SplitPanelSizeMode.</summary>
    public enum SplitPanelSizeMode
    {
        /// <summary>An absolute pixel size.</summary>
        Absolute = 0,
        /// <summary>Fills the available space.</summary>
        Fill = 1,
        /// <summary>A relative (proportional) size.</summary>
        Relative = 2,
        /// <summary>Automatic sizing.</summary>
        Auto = 3
    }

    /// <summary>Specifies the alignment of a tab strip. Compat for Telerik TabStripAlignment.</summary>
    public enum TabStripAlignment
    {
        /// <summary>Top.</summary>
        Top = 0,
        /// <summary>Bottom.</summary>
        Bottom = 1,
        /// <summary>Left.</summary>
        Left = 2,
        /// <summary>Right.</summary>
        Right = 3
    }

    /// <summary>Specifies which buttons a tab strip shows. Compat for Telerik StripViewButtons.</summary>
    [Flags]
    public enum StripViewButtons
    {
        /// <summary>No buttons.</summary>
        None = 0,
        /// <summary>Scroll buttons.</summary>
        Scroll = 1,
        /// <summary>Item-list button.</summary>
        ItemList = 2,
        /// <summary>Close button.</summary>
        Close = 4,
        /// <summary>Buttons appear automatically.</summary>
        Auto = 8,
        /// <summary>All buttons.</summary>
        All = Scroll | ItemList | Close
    }

    /// <summary>Specifies how page-view items are sized. Compat for Telerik PageViewItemSizeMode.</summary>
    public enum PageViewItemSizeMode
    {
        /// <summary>Each item sized to its content.</summary>
        Individual = 0,
        /// <summary>All items the same width.</summary>
        EqualWidth = 1,
        /// <summary>All items the same height.</summary>
        EqualHeight = 2,
        /// <summary>Items fill the strip.</summary>
        Fill = 3
    }
}
