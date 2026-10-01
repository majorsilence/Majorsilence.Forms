using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat tabbed page view. Backed by <see cref="Majorsilence.Forms.TabControl"/>.</summary>
    public class RadPageView : TabControl, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the page view (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets the collection of pages (alias for <see cref="TabControl.TabPages"/>).</summary>
        public TabPageCollection Pages => TabPages;

        /// <summary>Gets or sets the selected page (alias for <see cref="TabControl.SelectedTabPage"/>).</summary>
        public TabPage? SelectedPage {
            get => SelectedTabPage;
            set => SelectedTabPage = value;
        }

        /// <summary>Initializes a new instance of the <see cref="RadPageView"/> class.</summary>
        public RadPageView ()
        {
            // Telerik's themes frame the whole page view (strip and content) in a 1px border.
            Style.Border.Width = 1;
            // Telerik's strip items lead with their caption rather than centring it.
            TabStrip.ItemTextAlign = ContentAlignment.MiddleLeft;
            _strip = new RadPageViewStripElement (PerformLayout);
        }

        // A themed Telerik control takes its text colour from the theme, not from the ambient ForeColor
        // of the form it sits on -- RadControl.ForeColor reads the theme's root element -- and its pages'
        // children inherit THAT. A form with ForeColor = White (a common way to recolour labels on a dark
        // header) therefore still shows dark tab captions, and dark text in the combos and labels on each
        // page. The underlying TabControl follows the WinForms ambient rule, so the type-level style
        // supplies the theme colour; an explicit ForeColor on the page view still wins, and the default
        // is re-applied on theme change like every other type-level style.
        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (TabControl.DefaultStyle,
            (style) => style.ForegroundColor = Theme.ForegroundColor);

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>Gets or sets the page shown when the view starts.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): the page is selected when this is set, or -- the
        /// designer's order, the property before the pages -- at the first layout after it is added.</remarks>
        public TabPage? DefaultPage {
            get => default_page;
            set {
                default_page = value;
                default_pending = value is not null;
                ApplyDefaultPage ();
            }
        }

        private TabPage? default_page;
        private bool default_pending;

        private void ApplyDefaultPage ()
        {
            // Both lists must hold it: a layout can run partway through adding a page, after the page is
            // in TabPages but before its tab is in the strip.
            if (!default_pending || default_page is null || TabPages.IndexOf (default_page) is var index && (index < 0 || index >= TabStrip.Tabs.Count))
                return;

            default_pending = false;
            SelectedTabPage = default_page;
        }

        /// <summary>Gets or sets how the tab items are sized; applied with the strip's <see cref="RadPageViewStripElement.ItemFitMode"/>.</summary>
        /// <remarks>Telerik's default is <see cref="PageViewItemSizeMode.Individual"/> -- which is why designers serialize <c>EqualWidth</c> explicitly.</remarks>
        public PageViewItemSizeMode ItemSizeMode {
            get => _strip.ItemSizeMode;
            set => _strip.ItemSizeMode = value;
        }

        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;

        private readonly RadPageViewStripElement _strip;

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            ApplyStripItemSizing ();
            base.OnLayout (e);

            // Not from ControlAdded: the tab list is not up to date yet at that moment.
            ApplyDefaultPage ();
        }

        // Translates Telerik's strip sizing onto the TabControl's ItemSize/SizeMode, which the strip
        // already lays out from: EqualWidth gives every tab the widest one's width, Fill stretches the
        // row across the strip (both together: equal shares of the whole width), and the height the
        // designer recorded on each page's ItemSize becomes the row height. Setting an unchanged
        // ItemSize/SizeMode is a no-op, so re-entering from the layout this triggers settles at once.
        private void ApplyStripItemSizing ()
        {
            var height = 0;
            var widest = 0;

            foreach (var page in TabPages)
                if (page is RadPageViewPage rad)
                    height = Math.Max (height, (int)Math.Round (rad.ItemSize.Height));

            foreach (var tab in TabStrip.Tabs)
                widest = Math.Max (widest, tab.GetPreferredSize (Size.Empty).Width);

            var equal = _strip.ItemSizeMode == PageViewItemSizeMode.EqualWidth;
            var fill = (_strip.ItemFitMode & StripViewItemFitMode.Fill) != 0;
            var count = TabStrip.Tabs.Count;

            if (!equal && !fill && height == 0)
                return;

            var width = ItemSize.Width;

            if (equal && fill && count > 0) {
                width = Math.Max (1, DeviceToLogicalUnits (ClientRectangle.Width) / count);
                SizeMode = TabSizeMode.Fixed;
            } else if (fill) {
                SizeMode = TabSizeMode.FillToRight;
            } else if (equal) {
                width = TabStrip.DeviceToLogicalUnits (widest);
                SizeMode = TabSizeMode.Fixed;
            }

            ItemSize = new Size (width, height > 0 ? height : ItemSize.Height);
        }

        /// <summary>Returns the strip element at the given index (stub; index 0 is the tab strip).</summary>
        public RadPageViewStripElement GetChildAt (int index) => _strip;

        /// <summary>Raised when the selected page changes (alias for SelectedIndexChanged).</summary>
        public event EventHandler? SelectedPageChanged {
            add => SelectedIndexChanged += value;
            remove => SelectedIndexChanged -= value;
        }

        /// <summary>
        /// Raised before a page is removed, and able to veto it: set
        /// <c>Cancel</c> to keep the page.
        /// </summary>
        /// <remarks>
        /// The accessors were <c>add { } remove { }</c>, which discards the delegate at the add site,
        /// so the "save before closing this tab?" prompt every document UI is built around never
        /// appeared and the close was never vetoed.
        /// </remarks>
        public event EventHandler<RadPageViewCancelEventArgs>? PageRemoving;

        /// <summary>Raises the <see cref="PageRemoving"/> event; returns false when a handler vetoed.</summary>
        /// <param name="page">The page about to be removed.</param>
        protected internal override bool OnPageRemoving (TabPage page)
        {
            if (PageRemoving is null)
                return true;

            var e = new RadPageViewCancelEventArgs { Page = page as RadPageViewPage };

            PageRemoving (this, e);

            return !e.Cancel;
        }

        // Real, and never raised -- deliberately. Telerik's PageCollapsed belongs to RadPageView's
        // ExplorerBar/Outlook/Accordion modes, where a page's content region collapses in place. This
        // page view is a TabControl, which has no collapse concept at all and exposes no Mode, so
        // there is no moment at which a page collapses. Declared as a real event rather than
        // `add { } remove { }` so a handler at least survives being attached.
#pragma warning disable CS0067
        /// <summary>
        /// Raised when a page is collapsed. Never raised here: collapsing requires an accordion or
        /// ExplorerBar page-view mode, which this compat page view does not implement.
        /// </summary>
        public event EventHandler<RadPageViewEventArgs>? PageCollapsed;
#pragma warning restore CS0067
    }

    /// <summary>Telerik-compat page-view page. Backed by <see cref="Majorsilence.Forms.TabPage"/>.</summary>
    public class RadPageViewPage : TabPage, ISupportInitializeCompat
    {
        /// <summary>Gets or sets the tab item size; its height becomes the owning <see cref="RadPageView"/>'s tab row height.</summary>
        public SizeF ItemSize {
            get => item_size;
            set {
                item_size = value;
                Parent?.PerformLayout ();
            }
        }

        private SizeF item_size;
        /// <summary>Gets the strip item element for this page (stub).</summary>
        public RadElement Item { get; } = new RadElement ();
    }

    /// <summary>Telerik-compat page-view strip element (the tab header strip). Stub.</summary>
    public class RadPageViewStripElement : RadElement
    {
        // The owning page view's re-layout, so a designer line such as
        // `CType(pv.GetChildAt(0), RadPageViewStripElement).ItemFitMode = Fill` re-sizes the tabs.
        private readonly Action? relayout;

        /// <summary>Initializes a new, freestanding instance of the <see cref="RadPageViewStripElement"/> class.</summary>
        public RadPageViewStripElement () { }

        internal RadPageViewStripElement (Action relayout) => this.relayout = relayout;

        /// <summary>Gets the strip items. Empty stub -- the compat page view has no per-item element tree.</summary>
        public List<RadPageViewItem> Items { get; } = new ();
        /// <summary>Gets or sets which strip buttons are shown. Stub.</summary>
        public StripViewButtons StripButtons { get; set; } = StripViewButtons.None;
        /// <summary>Gets or sets whether each item shows a close button. Stub.</summary>
        public bool ShowItemCloseButton { get; set; }

        /// <summary>Gets or sets the item fit mode; <see cref="StripViewItemFitMode.Fill"/> stretches the tabs across the strip.</summary>
        public StripViewItemFitMode ItemFitMode {
            get => item_fit_mode;
            set {
                item_fit_mode = value;
                relayout?.Invoke ();
            }
        }

        private StripViewItemFitMode item_fit_mode = StripViewItemFitMode.Default;

        /// <summary>Gets or sets the item size mode; <see cref="PageViewItemSizeMode.EqualWidth"/> gives every tab the same width.</summary>
        public PageViewItemSizeMode ItemSizeMode {
            get => item_size_mode;
            set {
                item_size_mode = value;
                relayout?.Invoke ();
            }
        }

        private PageViewItemSizeMode item_size_mode = PageViewItemSizeMode.Individual;
        /// <summary>Gets or sets the highlight color. Stub.</summary>
        public Color HighlightColor { get; set; } = Color.Empty;
    }

    /// <summary>Telerik-compat page-view tab-strip element (the <c>RadPageView.Mode = PageViewMode.RibbonBar/ExplorerBar/…</c> strip). Stub.</summary>
    public class RadPageViewTabStripElement : RadPageViewStripElement
    {
        /// <summary>Gets or sets the orientation items are laid out in. Stub.</summary>
        public PageViewContentOrientation ItemContentOrientation { get; set; } = PageViewContentOrientation.Horizontal;
    }

    /// <summary>
    /// Telerik-compat container hosted by a strip-view item (e.g. a pinned/floating tab content host).
    /// Derives from <see cref="PathAwareElement"/> (rather than the plainer <see cref="RadElement"/>) so
    /// that when it sits inside a larger path-aware element tree — see
    /// <c>RadRichTextEditorRibbon.cs</c>'s <see cref="RadRibbonBarElement"/>, which nests one of these at
    /// its tab-strip position — chained <c>GetChildAt</c> calls through it keep resolving against that
    /// tree's registered paths instead of falling back to untyped stubs.
    /// </summary>
    public class StripViewItemContainer : PathAwareElement
    {
        /// <summary>Initializes a new, freestanding instance of the <see cref="StripViewItemContainer"/> class (its own path-aware tree root).</summary>
        public StripViewItemContainer () : base (string.Empty) { }

        /// <summary>Initializes a new instance of the <see cref="StripViewItemContainer"/> class at the given root-relative dotted index path within a larger path-aware element tree.</summary>
        internal StripViewItemContainer (string path) : base (path) { }
    }

    /// <summary>
    /// Telerik-compat strip item (a single tab header). Note: this collides in name with the pre-existing
    /// <see cref="Majorsilence.Forms.TabStripItem"/>; files that import both <c>Majorsilence.Forms</c> and
    /// <c>Majorsilence.Forms.Telerik</c> must qualify one of the two.
    /// </summary>
    public class TabStripItem : RadItem
    {
        /// <summary>Gets or sets the item's image.</summary>
        public Majorsilence.Forms.Drawing.Image? Image { get; set; }
        /// <summary>Gets or sets whether this item (page) is the selected one. Stub.</summary>
        public bool IsSelected { get; set; }
        /// <summary>Gets or sets whether this item is pinned (not scrolled/reordered). Stub.</summary>
        public bool IsPinned { get; set; }
        /// <summary>Gets or sets the item's title (Telerik alias for <see cref="RadItem.Text"/>).</summary>
        public string Title {
            get => Text;
            set => Text = value;
        }
    }

    /// <summary>Provides data for Telerik page-view events (e.g. PageViewChanging/Changed).</summary>
    public class RadPageViewEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance with the specified page.</summary>
        public RadPageViewEventArgs (RadPageViewPage? page) => Page = page;

        /// <summary>Gets the affected page.</summary>
        public RadPageViewPage? Page { get; }
    }

    /// <summary>Compat stand-in for Telerik's RadPageViewItem (a strip-header item representing a page).</summary>
    public class RadPageViewItem : RadItem
    {
        /// <summary>Gets or sets the page this item represents.</summary>
        public RadPageViewPage? Page { get; set; }
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
        /// <summary>Gets or sets whether the container is collapsed. Stored stub (designer-assigned).</summary>
        public bool Collapsed { get; set; }
    }

    /// <summary>Telerik-compat split panel. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class SplitPanel : Panel, ISupportInitializeCompat
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

        /// <summary>Gets or sets the auto-size scale (proportional split size). Stored for Telerik compat.</summary>
        public SizeF AutoSizeScale { get; set; } = new SizeF (1f, 1f);
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

    /// <summary>Specifies the overall visual mode of a <see cref="RadPageView"/>. Compat for Telerik PageViewMode.</summary>
    public enum PageViewMode
    {
        /// <summary>Classic tab-strip mode.</summary>
        Tabs = 0,
        /// <summary>Ribbon-bar style mode.</summary>
        RibbonBar = 1,
        /// <summary>Outlook-style explorer bar mode.</summary>
        ExplorerBar = 2,
        /// <summary>Backstage (full-screen menu) mode.</summary>
        Backstage = 3
    }

    /// <summary>Specifies the layout orientation of page-view strip content. Compat for Telerik PageViewContentOrientation.</summary>
    public enum PageViewContentOrientation
    {
        /// <summary>Items are laid out horizontally.</summary>
        Horizontal = 0,
        /// <summary>Items are laid out vertically.</summary>
        Vertical = 1
    }

    /// <summary>Specifies how strip-view items are sized to fit the strip. Compat for Telerik StripViewItemFitMode.</summary>
    public enum StripViewItemFitMode
    {
        /// <summary>Items keep their natural (content-driven) size.</summary>
        Default = 0,
        /// <summary>Items stretch to fill the strip.</summary>
        Fill = 1,
        /// <summary>Items wrap onto multiple lines instead of scrolling.</summary>
        Multiline = 2,
        /// <summary>Items shrink/scroll as needed to fit the available space.</summary>
        Fit = 3
    }

    /// <summary>
    /// Telerik-compat collapsible panel. Backed by <see cref="Majorsilence.Forms.Panel"/>; hosts a single
    /// child <see cref="PanelContainer"/> whose visibility is toggled by <see cref="IsExpanded"/>.
    /// </summary>
    public class RadCollapsiblePanel : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the panel (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        private bool _isExpanded = true;

        private readonly Label _header;
        private int _expandedHeight;

        // The header strip's height, in logical units.
        internal const int HeaderHeight = 24;

        /// <summary>Initializes a new instance of the RadCollapsiblePanel class.</summary>
        /// <remarks>
        /// W6 mechanisms (#176): the panel had no header, so <see cref="HeaderText"/> was never shown and
        /// nothing in the UI could expand or collapse it. The header is added after the content so it docks
        /// first, at the top; clicking it toggles <see cref="IsExpanded"/>.
        /// </remarks>
        public RadCollapsiblePanel ()
        {
            PanelContainer = new Panel { Dock = DockStyle.Fill };
            Controls.Add (PanelContainer);

            _header = new HeaderLabel { Dock = DockStyle.Top, Height = HeaderHeight, TextAlign = ContentAlignment.MiddleLeft };
            _header.Click += (_, _) => IsExpanded = !IsExpanded;
            Controls.AddImplicitControl (_header);
            UpdateHeader ();
        }

        internal Label Header => _header;

        // A header toggles on every click, as a button does: a fast second click collapses what the first
        // expanded rather than being swallowed as a DoubleClick (EVT-01 made those exclusive).
        private sealed class HeaderLabel : Label
        {
            public HeaderLabel () => SetStyle (ControlStyles.StandardDoubleClick, false);
        }

        private void UpdateHeader () => _header.Text = (_isExpanded ? "\u25BE " : "\u25B8 ") + _headerText;

        /// <summary>Gets the panel hosting the collapsible content.</summary>
        public Panel PanelContainer { get; }

        private string _headerText = string.Empty;
        /// <summary>Gets or sets the header text shown above the content.</summary>
        public string HeaderText {
            get => _headerText;
            set {
                _headerText = value ?? string.Empty;
                UpdateHeader ();
            }
        }

        /// <summary>Gets or sets whether the panel is expanded (showing its content) or collapsed.</summary>
        public bool IsExpanded {
            get => _isExpanded;
            set {
                if (_isExpanded == value)
                    return;
                if (value)
                    Expand ();
                else
                    Collapse ();
            }
        }

        /// <summary>Gets or sets whether expand/collapse is animated. Stub (no animation is performed).</summary>
        public bool EnableAnimation { get; set; } = true;

        /// <summary>Raised after the panel expands.</summary>
        public event EventHandler? Expanded;
        /// <summary>Raised after the panel collapses.</summary>
        public event EventHandler? Collapsed;

        private bool FillsVertically => Dock is DockStyle.Fill or DockStyle.Left or DockStyle.Right;

        /// <summary>Expands the panel, showing its content.</summary>
        public void Expand ()
        {
            _isExpanded = true;
            PanelContainer.Visible = true;
            UpdateHeader ();

            // Back to the height it had before collapsing -- unless docking sizes it.
            if (_expandedHeight > 0 && !FillsVertically)
                Height = _expandedHeight;
            Expanded?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>Collapses the panel, hiding its content.</summary>
        public void Collapse ()
        {
            _isExpanded = false;
            PanelContainer.Visible = false;
            UpdateHeader ();

            // Shrink to the header, as Telerik's does, so what sits below moves up.
            if (!FillsVertically) {
                _expandedHeight = Height;
                Height = HeaderHeight + Padding.Vertical;   // Height is logical, like Bounds (RC-8)
            }
            Collapsed?.Invoke (this, EventArgs.Empty);
        }
    }

    /// <summary>Telerik-compat scrollable panel. Backed by <see cref="Majorsilence.Forms.Panel"/>; hosts a single filling <see cref="RadScrollablePanelContainer"/>.</summary>
    public class RadScrollablePanel : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the panel (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Initializes a new instance of the RadScrollablePanel class.</summary>
        public RadScrollablePanel ()
        {
            PanelContainer = new RadScrollablePanelContainer { Dock = DockStyle.Fill };
            Controls.Add (PanelContainer);
        }

        /// <summary>Gets the panel hosting the scrollable content.</summary>
        public RadScrollablePanelContainer PanelContainer { get; }
    }

    /// <summary>Telerik-compat container hosted by a <see cref="RadScrollablePanel"/>. Backed by <see cref="Majorsilence.Forms.Panel"/>.</summary>
    public class RadScrollablePanelContainer : Panel, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the container (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
    }
}
