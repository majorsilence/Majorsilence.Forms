using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TabControl control.
    /// </summary>
    public class TabControl : Control
    {
        private readonly TabStrip tab_strip;

        /// <summary>
        /// Initializes a new instance of the TabControl class.
        /// </summary>
        public TabControl ()
        {
            tab_strip = Controls.AddImplicitControl (new TabStrip {
                Dock = DockStyle.Top
            });

            tab_strip.SelectedTabChanged += TabStrip_SelectedTabChanged;

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
        public ImageList? ImageList { get; set; }

        /// <summary>Gets or sets whether more than one row of tabs can be displayed. Stub in Majorsilence.Forms.</summary>
        public bool Multiline { get; set; }

        /// <summary>Gets or sets the alignment of the tabs. Stub in Majorsilence.Forms (always top).</summary>
        public TabAlignment Alignment { get; set; } = TabAlignment.Top;

        /// <summary>Gets or sets the draw mode for the tabs. Stub in Majorsilence.Forms.</summary>
        public TabDrawMode DrawMode { get; set; } = TabDrawMode.Normal;

        /// <summary>Gets or sets the fixed size of each tab. Stub in Majorsilence.Forms.</summary>
        public System.Drawing.Size ItemSize { get; set; }

        /// <summary>Gets or sets the width of the selected tab padding. Stub in Majorsilence.Forms.</summary>
        public new System.Drawing.Point Padding { get; set; }

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

        /// <summary>Gets or sets the size mode of the tabs. Stub in Majorsilence.Forms.</summary>
        public TabSizeMode SizeMode { get; set; } = TabSizeMode.Normal;

        /// <summary>Gets the number of tab rows. Always returns 1 in Majorsilence.Forms.</summary>
        public int RowCount => 1;

        /// <summary>Gets the bounding rectangle of a tab at the specified index.</summary>
        public System.Drawing.Rectangle GetTabRect (int index) =>
            new System.Drawing.Rectangle (index * 100, 0, 100, 25);

        /// <summary>Raised when a tab is drawn (owner-draw mode). Stub in Majorsilence.Forms.</summary>
        public event EventHandler<DrawItemEventArgs>? DrawItem { add { } remove { } }

        /// <summary>Raised before a tab page is selected. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<TabControlCancelEventArgs>? Selecting { add { } remove { } }

        /// <summary>Raised after a tab page is selected. Stub in Majorsilence.Forms.</summary>
        public new event EventHandler<TabControlEventArgs>? Selected { add { } remove { } }

        /// <summary>Raised before a tab page is deselected. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<TabControlCancelEventArgs>? Deselecting { add { } remove { } }

        /// <summary>Raised after a tab page is deselected. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<TabControlEventArgs>? Deselected { add { } remove { } }

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

        // Handles changes of the TabStrip's selected tab.
        private void TabStrip_SelectedTabChanged (object? sender, EventArgs e)
        {
            var old_selected = Controls.OfType<TabPage> ().FirstOrDefault (tp => tp.Visible);
            var new_selected = GetPageFromTab (tab_strip.SelectedTab);

            if (old_selected == new_selected)
                return;

            if (old_selected != null)
                old_selected.Visible = false;

            if (new_selected != null)
                new_selected.Visible = true;

            // Match WinForms: SelectedIndexChanged is not raised while tabs are being added during
            // InitializeComponent (before the control's handle is created). Firing it then runs the form's
            // SelectedIndexChanged handler mid-construction -- before the fields it touches are initialized --
            // which NullReferences (hit opening frmMaintainProperty, whose tab handler calls ShowTransLookup).
            // The visibility swap above still happens so the correct page shows; only the event waits.
            if (Created)
                OnSelectedIndexChanged (EventArgs.Empty);
        }
    }
}
