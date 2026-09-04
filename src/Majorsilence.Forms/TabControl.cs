using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TabControl control.
    /// </summary>
    public partial class TabControl : Control
    {
        private readonly TabStrip tab_strip;

        // Exposed so the WinForms-shaped TabPageCollection (TabControl) constructor can bind to the
        // strip this control already owns instead of creating a second one.
        internal TabStrip TabStrip => tab_strip;

        /// <summary>
        /// Initializes a new instance of the TabControl class.
        /// </summary>
        public TabControl ()
        {
            tab_strip = Controls.AddImplicitControl (new TabStrip {
                Dock = DockStyle.Top
            });

            tab_strip.SelectedTabChanged += TabStrip_SelectedTabChanged;

            // Deselecting/Deselected have to run while the strip is still on the outgoing tab, so they
            // are raised from a veto the strip asks for before it commits (LAY-13).
            tab_strip.SelectionChanging = OnStripSelectionChanging;

            // In WinForms the tab headers are part of the TabControl itself, so clicking one raises
            // TabControl.Click. Here the headers live in an implicit child strip, which would otherwise
            // swallow the click: the strip receives it and the TabControl never hears about it.
            // Migrated code routinely hangs work off `Handles someTab.Click` (loading a tab's grid on
            // demand is the common shape), and that work simply never ran.
            tab_strip.Click += (_, e) => OnClick (e);
            tab_strip.MouseClick += (_, e) => OnMouseClick (e);

            TabPages = new TabPageCollection (this, tab_strip);
        }

        /// <summary>
        /// Gets the collection of tabs contained by this TabControl.
        /// </summary>
        public TabPageCollection TabPages { get; }

        /// <inheritdoc/>
        protected override ControlCollection CreateControlsInstance () => new TabControlControlCollection (this);

        // In real System.Windows.Forms, TabControl.Controls and TabControl.TabPages are the same
        // collection -- ported designer code (`this.tabControl1.Controls.Add(this.tabPage1)`) relies
        // on that. Majorsilence.Forms keeps them separate (TabPages drives the tab strip), so a
        // plain Controls.Add(tabPage) would add the page as an invisible child without ever
        // registering a tab for it -- SelectedIndex = 0 then throws (no tabs exist). This collection
        // detects a TabPage being added directly and redirects to TabPages.Insert, which itself
        // calls back into Controls.Insert once the page is already recorded in TabPages -- the
        // Contains check below prevents that from looping.
        private sealed class TabControlControlCollection : ControlCollection
        {
            private readonly TabControl _owner;

            internal TabControlControlCollection (TabControl owner) : base (owner) => _owner = owner;

            public override void Insert (int index, Control item)
            {
                if (item is TabPage page && !_owner.TabPages.Contains (page)) {
                    _owner.TabPages.Insert (Math.Min (index, _owner.TabPages.Count), page);
                    return;
                }

                base.Insert (index, item);
            }
        }

        // Hides/shows the built-in tab header strip. Used when the headers are presented elsewhere
        // (e.g. RadTabbedForm draws document tabs in the title bar and uses the TabControl only as a
        // content host). The hidden strip is Dock=Top with no space, so pages fill the whole control.
        internal bool TabStripVisible {
            get => tab_strip.Visible;
            set => tab_strip.Visible = value;
        }

        private TabPage? GetPageFromTab (TabStripItem? item) => TabPages.FirstOrDefault (p => p.TabStripItem == item);

        /// <summary>
        /// Raises the SelectedIndexChanged event.
        /// </summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e) => SelectedIndexChanged?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets the index of the currently selected tab page. This value will be -1 if there is not a selected tab page;
        /// </summary>
        public int SelectedIndex {
            get => tab_strip.SelectedIndex;
            set => tab_strip.SelectedIndex = value;
        }

        /// <summary>
        /// Raised when the value of the SelectedIndex property changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>
        /// Gets or sets the currently selected tab page.
        /// </summary>
        public TabPage? SelectedTabPage {
            get => GetPageFromTab (tab_strip.SelectedTab);
            set {
                if (value is null) {
                    tab_strip.SelectedTab = null;
                    return;
                }

                var index = TabPages.IndexOf (value);

                // WinForms quietly clears the selection when the page is not part of this control.
                if (index == -1) {
                    tab_strip.SelectedTab = null;
                    return;
                }

                tab_strip.SelectedIndex = index;
            }
        }

        /// <summary>Gets or sets the ImageList used by the tab pages.</summary>
        public ImageList? ImageList {
            get => image_list;
            set {
                if (image_list == value)
                    return;

                image_list = value;
                UpdateTabImages ();
            }
        }

        private ImageList? image_list;

        /// <summary>
        /// Re-resolves every page's tab image from <see cref="ImageList"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="ImageList"/>, <c>TabPage.ImageIndex</c> and <c>TabPage.ImageKey</c> were three
        /// stores nothing read, so an icon-in-tab UI built the WinForms way lost its icons silently and
        /// the tabs measured narrower than on Windows, moving the header's wrap points (LAY-14).
        /// </remarks>
        internal void UpdateTabImages ()
        {
            foreach (var page in TabPages)
                UpdateTabImage (page);
        }

        // Resolves one page's tab image. Called when the page is added, when its ImageIndex/ImageKey
        // changes, and for every page when the ImageList itself is swapped.
        internal void UpdateTabImage (TabPage page)
        {
            page.TabStripItem.Image = ResolveTabImage (page);
        }

        private SkiaSharp.SKBitmap? ResolveTabImage (TabPage page)
        {
            var images = ImageList?.Images;

            if (images is null || images.Count == 0)
                return null;

            // Key wins over index when both are set, the order upstream's TCITEM resolution uses.
            if (page.ImageKey.HasValue ())
                return images.ContainsKey (page.ImageKey) ? images[page.ImageKey] : null;

            return page.ImageIndex >= 0 && page.ImageIndex < images.Count ? images[page.ImageIndex] : null;
        }

        /// <summary>Gets or sets whether more than one row of tabs can be displayed. Stub in Majorsilence.Forms.</summary>
        public bool Multiline { get; set; }

        /// <summary>Gets or sets the alignment of the tabs.</summary>
        /// <remarks>
        /// Moves the header strip to that edge of the control; the pages follow, because the strip is
        /// docked and the layout engine gives what is left to the <c>Fill</c>ed page. <c>Left</c> and
        /// <c>Right</c> stack the tabs in a single column rather than rotating their text, which is the
        /// part of upstream's <c>TCS_VERTICAL</c> the renderer here cannot express. Was stored and never
        /// read, so <c>Alignment = Bottom</c> silently kept the tabs on top (LAY-15).
        /// </remarks>
        public TabAlignment Alignment {
            get => alignment;
            set {
                if (alignment == value)
                    return;

                alignment = value;
                tab_strip.Dock = DockForAlignment (value);
                PerformLayout ();
            }
        }

        private TabAlignment alignment = TabAlignment.Top;

        private static DockStyle DockForAlignment (TabAlignment alignment) => alignment switch {
            TabAlignment.Bottom => DockStyle.Bottom,
            TabAlignment.Left => DockStyle.Left,
            TabAlignment.Right => DockStyle.Right,
            _ => DockStyle.Top,
        };

        /// <summary>
        /// Gets or sets the draw mode for the tabs. <see cref="TabDrawMode.OwnerDrawFixed"/> hands each
        /// tab's appearance to the <see cref="DrawItem"/> event instead of the built-in renderer.
        /// </summary>
        public TabDrawMode DrawMode { get; set; } = TabDrawMode.Normal;

        /// <summary>Gets or sets the fixed size of each tab.</summary>
        /// <remarks>
        /// The height applies always; the width only under <see cref="TabSizeMode.Fixed"/>, which is how
        /// upstream reads the same two values. Both were stored and ignored (LAY-15).
        /// </remarks>
        public System.Drawing.Size ItemSize {
            get => item_size;
            set {
                if (item_size == value)
                    return;

                item_size = value;
                RelayoutStrip ();
            }
        }

        private System.Drawing.Size item_size;

        /// <summary>Gets or sets the extra padding added to every tab, horizontally and vertically.</summary>
        /// <remarks>Was stored and never read, so designer-set tab padding did nothing (LAY-15).</remarks>
        public new System.Drawing.Point Padding {
            get => tab_padding;
            set {
                if (tab_padding == value)
                    return;

                tab_padding = value;
                RelayoutStrip ();
            }
        }

        private System.Drawing.Point tab_padding;

        // The strip measures its tabs from the owner's ItemSize/SizeMode/Padding, so a change to any of
        // them has to re-run the strip's own layout, not just the container's.
        private void RelayoutStrip ()
        {
            tab_strip.PerformLayout ();
            tab_strip.Invalidate ();
            PerformLayout ();
        }

        /// <summary>Gets or sets whether tab pages show their tooltips. Stub in Majorsilence.Forms.</summary>
        public bool ShowToolTips { get; set; }

        /// <summary>Gets or sets the visual appearance of the tab control. Stub in Majorsilence.Forms.</summary>
        public TabAppearance Appearance { get; set; } = TabAppearance.Normal;

        /// <summary>Gets or sets whether tabs are highlighted when mouse hovers. Stub in Majorsilence.Forms.</summary>
        public bool HotTrack { get; set; }

        /// <summary>Gets or sets a value indicating whether right-to-left mirror placement is turned on. Stub in Majorsilence.Forms.</summary>
        public bool RightToLeftLayout { get; set; }

        /// <summary>Gets the number of tabs in the tab strip.</summary>
        public int TabCount => TabPages.Count;

        /// <summary>Gets or sets the size mode of the tabs.</summary>
        /// <remarks>
        /// <see cref="TabSizeMode.Fixed"/> gives every tab <see cref="ItemSize"/>'s width;
        /// <see cref="TabSizeMode.FillToRight"/> stretches each row of tabs to the strip's width
        /// (upstream's <c>TCS_RIGHTJUSTIFY</c>). Was stored and ignored (LAY-15).
        /// </remarks>
        public TabSizeMode SizeMode {
            get => size_mode;
            set {
                if (size_mode == value)
                    return;

                size_mode = value;
                RelayoutStrip ();
            }
        }

        private TabSizeMode size_mode = TabSizeMode.Normal;

        /// <summary>Gets the number of tab rows (tabs wrap into additional rows when they overflow).</summary>
        public int RowCount => tab_strip.RowCount;

        /// <summary>Gets the bounding rectangle of a tab at the specified index, in this control's coordinates.</summary>
        public System.Drawing.Rectangle GetTabRect (int index)
        {
            if (index < 0 || index >= tab_strip.Tabs.Count)
                return new System.Drawing.Rectangle (index * 100, 0, 100, 25);

            // Tab bounds are relative to the strip, but WinForms' GetTabRect answers in the
            // TabControl's own coordinates. Identity while the strip sits at the top -- and the whole
            // point of the method once Alignment moves the strip to another edge (LAY-15).
            var bounds = tab_strip.Tabs[index].Bounds;
            bounds.Offset (tab_strip.Left, tab_strip.Top);

            return bounds;
        }

        /// <summary>
        /// Raised for each tab when <see cref="DrawMode"/> is an owner-draw mode, letting the handler
        /// paint the tab itself.
        /// </summary>
        public event DrawItemEventHandler? DrawItem;

        /// <summary>
        /// Raises the DrawItem event.
        /// </summary>
        protected virtual void OnDrawItem (DrawItemEventArgs e) => DrawItem?.Invoke (this, e);

        /// <summary>
        /// True when tabs are painted by <see cref="OnDrawItem"/> rather than the built-in renderer.
        /// </summary>
        internal bool IsOwnerDrawn => DrawMode != TabDrawMode.Normal;

        /// <summary>
        /// Lets the tab strip's renderer hand a tab over to the owner-draw event. Kept internal so the
        /// strip -- which is an implementation detail of this control -- doesn't have to expose the
        /// TabControl's protected surface.
        /// </summary>
        internal void RaiseDrawItem (DrawItemEventArgs e) => OnDrawItem (e);

        // Typed with WinForms' own delegates, not EventHandler<T>: the two are not interchangeable, so
        // code that wires one of these the WinForms way (`new TabControlCancelEventHandler(...)`, or by
        // forwarding an event of that delegate type) did not compile.
        /// <summary>Raised before a tab page is selected. Cancelable.</summary>
        public event TabControlCancelEventHandler? Selecting;

        /// <summary>Raised after a tab page is selected.</summary>
        public new event TabControlEventHandler? Selected;

        /// <summary>Raised before a tab page is deselected. Cancelable.</summary>
        public event TabControlCancelEventHandler? Deselecting;

        /// <summary>Raised after a tab page is deselected.</summary>
        public event TabControlEventHandler? Deselected;

        /// <summary>Raises the <see cref="Selecting"/> event.</summary>
        protected virtual void OnSelecting (TabControlCancelEventArgs e) => Selecting?.Invoke (this, e);

        /// <summary>Raises the <see cref="Deselecting"/> event.</summary>
        protected virtual void OnDeselecting (TabControlCancelEventArgs e) => Deselecting?.Invoke (this, e);

        /// <summary>Raises the <see cref="Selected"/> event.</summary>
        protected virtual void OnSelected (TabControlEventArgs e) => Selected?.Invoke (this, e);

        /// <summary>Raises the <see cref="Deselected"/> event.</summary>
        protected virtual void OnDeselected (TabControlEventArgs e) => Deselected?.Invoke (this, e);

        /// <summary>Gets or sets the selected tab page (WinForms alias for SelectedTabPage).</summary>
        public TabPage? SelectedTab {
            get => SelectedTabPage;
            set => SelectedTabPage = value;
        }

        /// <summary>Selects the tab at the specified index.</summary>
        public void SelectTab (int index) => SelectedIndex = index;

        /// <summary>Selects the specified tab page.</summary>
        public void SelectTab (TabPage tabPage) => SelectedTabPage = tabPage;

        /// <summary>Selects the tab page with the specified Name. Mirrors WinForms TabControl.SelectTab(string).</summary>
        public void SelectTab (string tabPageName)
        {
            foreach (var page in TabPages)
                if (string.Equals (page.Name, tabPageName, StringComparison.OrdinalIgnoreCase)) {
                    SelectedTabPage = page;
                    return;
                }
        }

        /// <summary>Removes all tab pages from the TabControl.</summary>
        public void RemoveAll () => TabPages.Clear ();

        /// <summary>Returns the tab page at the specified client point, or null. Stub in Majorsilence.Forms.</summary>
        public TabPage? HitTest (System.Drawing.Point point) =>
            TabPages.FirstOrDefault (tp => tp.Bounds.Contains (point));

        // Raised by the strip before it commits a selection change, so the Deselecting/Deselected pair
        // runs while SelectedTab/SelectedIndex still report the page being LEFT. Returning false vetoes
        // the change outright, which is what a cancelled Deselecting means -- no revert dance, and no
        // Deselected either.
        //
        // Upstream splits the four events across two notifications: WM_NOTIFY/TCN_SELCHANGING raises
        // Deselecting then Deselected before the native control moves, and TCN_SELCHANGE raises
        // Selecting, Selected, SelectedIndexChanged after it has. Ours used to raise all four from the
        // one place, in the order Deselecting, Selecting, Deselected, SelectedIndexChanged, Selected --
        // so a handler saving the outgoing page's state was handed the incoming page, and anything
        // wired to both Selected and SelectedIndexChanged ran them in the reverse of Windows' order
        // (LAY-13).
        private bool OnStripSelectionChanging (int newIndex)
        {
            // Same suppression as the post-change events below: nothing is announced before the handle
            // exists (tabs added during InitializeComponent), and the revert after a cancelled
            // Selecting is not a change of its own.
            if (!Created || reverting_tab_selection)
                return true;

            var old_selected = current_page;
            var old_index = old_selected == null ? -1 : TabPages.IndexOf (old_selected);

            if (old_index == newIndex)
                return true;

            var deselecting = new TabControlCancelEventArgs (old_selected, old_index, false, TabControlAction.Deselecting);
            OnDeselecting (deselecting);

            if (deselecting.Cancel)
                return false;

            OnDeselected (new TabControlEventArgs (old_selected, old_index, TabControlAction.Deselected));

            return true;
        }

        // The page currently on screen. Tracked rather than read back from TabPage.Visible, which this
        // handler used to scan for: Control.Visible is ambient, so every page of a TabControl that is
        // not itself inside a visible parent answers false and the outgoing page cannot be recovered
        // from it at all -- and the outgoing page is precisely what Deselecting/Deselected exist to
        // name. It is also independent of the strip, which has already moved by the time the
        // post-change handler runs.
        private TabPage? current_page;

        // Handles changes of the TabStrip's selected tab.
        private void TabStrip_SelectedTabChanged (object? sender, EventArgs e)
        {
            var old_selected = current_page;
            var new_selected = GetPageFromTab (tab_strip.SelectedTab);

            if (old_selected == new_selected)
                return;

            var old_index = old_selected == null ? -1 : TabPages.IndexOf (old_selected);
            var new_index = new_selected == null ? -1 : TabPages.IndexOf (new_selected);

            // Selecting is cancelable and, unlike Deselecting, runs after the strip has moved -- as
            // upstream's does, the native control having already changed selection by TCN_SELCHANGE.
            // Cancelling therefore has to put the strip back; that second trip through here sees the
            // same page still visible and returns above.
            if (Created && !reverting_tab_selection) {
                var selecting = new TabControlCancelEventArgs (new_selected, new_index, false, TabControlAction.Selecting);
                OnSelecting (selecting);

                if (selecting.Cancel) {
                    reverting_tab_selection = true;
                    try {
                        tab_strip.SelectedIndex = old_index;
                    } finally {
                        reverting_tab_selection = false;
                    }
                    return;
                }
            }

            if (old_selected != null)
                old_selected.Visible = false;

            if (new_selected != null)
                new_selected.Visible = true;

            current_page = new_selected;

            // Match WinForms: SelectedIndexChanged is not raised while tabs are being added during
            // InitializeComponent (before the control's handle is created). Firing it then runs the form's
            // SelectedIndexChanged handler mid-construction -- before the fields it touches are initialized --
            // which NullReferences (hit opening frmMaintainProperty, whose tab handler calls ShowTransLookup).
            // The visibility swap above still happens so the correct page shows; only the event waits.
            //
            // Selected before SelectedIndexChanged, which is the order upstream's TCN_SELCHANGE uses.
            if (Created && !reverting_tab_selection) {
                OnSelected (new TabControlEventArgs (new_selected, new_index, TabControlAction.Selected));
                OnSelectedIndexChanged (EventArgs.Empty);
            }
        }

        // Set while restoring the strip after a cancelled Selecting/Deselecting, so the resulting second
        // trip through this handler does not raise the cancelable pair again (and cancel its own revert).
        private bool reverting_tab_selection;
    }
}
