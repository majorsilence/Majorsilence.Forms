using System.Collections;
using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// Telerik-compat tabbed form. The document tabs are presented by a drag-capable strip
    /// (<see cref="RadDocumentTabStrip"/>) that supports drag-to-reorder, tear-off (detach into a new
    /// window) and re-attach (drop onto another tabbed form's tab strip) on every platform. The only
    /// platform difference is where the strip sits: inside the title bar on platforms with custom
    /// (Majorsilence-drawn) chrome, or docked directly below the OS title bar on platforms that use
    /// native window decorations (e.g. macOS), where we can't draw into the title bar. The content is
    /// hosted by a <see cref="TabControl"/> whose built-in header strip is hidden.
    /// </summary>
    public class RadTabbedForm : RadForm
    {
        // Open, shown tabbed forms — the candidate drop targets for re-attach (every platform).
        internal static readonly List<RadTabbedForm> DocumentForms = new ();

        private readonly RadDocumentTabStrip tab_strip;

        /// <summary>Initializes a new instance of the RadTabbedForm class.</summary>
        public RadTabbedForm ()
        {
            TabbedFormControl = new RadTabbedFormControl ();

            // On macOS the base Form uses the native title bar; opt into extending our content up into it
            // (Avalonia 12 full-size content view) so the tab strip lives in the title bar like it does on
            // the custom-chrome platforms, instead of being docked below the OS title bar.
            if (OperatingSystem.IsMacOS ())
                ExtendsContentIntoTitleBar = true;

            // Always present the headers with our own drag-capable strip; the TabControl is used purely
            // as a content host, so hide its built-in header strip.
            TabbedFormControl.Host.TabStripVisible = false;
            tab_strip = new RadDocumentTabStrip (this, TabbedFormControl);

            if (!UseSystemDecorations || ExtendsContentIntoTitleBar) {
                // Tabs in the title bar — either fully custom chrome, or merged into the native macOS
                // title bar (ExtendsContentIntoTitleBar). Docking is processed in z-order from the
                // highest child index to the lowest, and a control's Fill claims whatever space is left
                // when it is reached. The title bar's caption buttons / traffic-light inset are IMPLICIT
                // children, which sort after explicit ones, so adding the strip as an EXPLICIT child
                // gives it the lowest index — the buttons/inset (Left/Right) claim their edges first and
                // the Fill strip takes the centre (after the macOS traffic lights when merged).
                tab_strip.Dock = DockStyle.Fill;
                // The tab strip owns the title bar, so don't also paint the window title over it.
                TitleBar.ShowText = false;
                Controls.Add (TabbedFormControl.Host);
                TitleBar.Controls.Add (tab_strip);
            } else {
                // Plain native chrome: the OS owns the title bar and we don't extend into it, so dock the
                // strip at the top of the client area (just below it). Add the Fill content FIRST so the
                // higher-indexed Top strip is processed first and reserves its space before the rest.
                tab_strip.Dock = DockStyle.Top;
                Controls.Add (TabbedFormControl.Host);
                Controls.Add (tab_strip);
            }

            DocumentForms.Add (this);
            FormClosed += (_, _) => DocumentForms.Remove (this);
        }

        /// <summary>Gets the tabbed-form control that owns the form's document tabs.</summary>
        public RadTabbedFormControl TabbedFormControl { get; }

        /// <summary>Gets or sets whether the caption (title bar) is shown. Stub.</summary>
        public bool ShowCaption { get; set; } = true;

        internal RadDocumentTabStrip TabStrip => tab_strip;

        // Returns the open, shown tabbed form whose tab strip contains the given screen point, or null.
        // Used to decide re-attach vs. tear-off on drop.
        internal static RadTabbedForm? FindFormAt (Point screen, RadTabbedForm? exclude)
        {
            foreach (var form in DocumentForms) {
                if (form == exclude || !form.Visible)
                    continue;

                if (form.tab_strip.ScreenBounds.Contains (screen))
                    return form;
            }

            return null;
        }
    }

    /// <summary>
    /// Telerik-compat tabbed-form control. Owns the collection of document tabs and the selection.
    /// Backed by a <see cref="Majorsilence.Forms.TabControl"/> that hosts the page content.
    /// </summary>
    public class RadTabbedFormControl : RadElement
    {
        /// <summary>Initializes a new instance of the RadTabbedFormControl class.</summary>
        public RadTabbedFormControl ()
        {
            Host = new TabControl { Dock = DockStyle.Fill };
            Items = new RadTabbedFormControlItemCollection (this);
            Host.SelectedIndexChanged += (_, _) => SelectedTabChanged?.Invoke (this, EventArgs.Empty);
        }

        /// <summary>The backing tab control that hosts page content, added to the owning form.</summary>
        internal TabControl Host { get; }

        /// <summary>Gets the collection of document tabs.</summary>
        public RadTabbedFormControlItemCollection Items { get; }

        /// <summary>Gets or sets the selected tab.</summary>
        public RadTabbedFormControlItem? SelectedTab {
            get => Items.FromPage (Host.SelectedTabPage);
            set => Host.SelectedTabPage = value?.TabPage;
        }

        /// <summary>Gets or sets the selected tab (alias for <see cref="SelectedTab"/>).</summary>
        public RadTabbedFormControlItem? SelectedItem {
            get => SelectedTab;
            set => SelectedTab = value;
        }

        /// <summary>Gets or sets whether tabs can be reordered by dragging. Stub (always enabled in the title bar).</summary>
        public bool EnableTabReorder { get; set; } = true;
        /// <summary>Gets or sets whether each tab shows a close button. Stub.</summary>
        public bool ShowItemCloseButton { get; set; }
        /// <summary>Gets or sets whether the document-tabs separator is shown. Stub.</summary>
        public bool ShowDocumentTabsSeparator { get; set; }

        /// <summary>Raised when the selected tab changes.</summary>
        public event EventHandler? SelectedTabChanged;

        // Raised when items are added, removed, reordered or cleared so the title-bar strip can resync.
        internal event Action? ItemsChanged;

        internal void RaiseItemsChanged () => ItemsChanged?.Invoke ();
    }

    /// <summary>
    /// Telerik-compat document tab for a <see cref="RadTabbedForm"/>. Backed by a
    /// <see cref="Majorsilence.Forms.TabPage"/> whose content is hosted in <see cref="ContentPanel"/>.
    /// </summary>
    public class RadTabbedFormControlItem : RadElement
    {
        /// <summary>Initializes a new instance of the RadTabbedFormControlItem class.</summary>
        public RadTabbedFormControlItem () : this (string.Empty) { }

        /// <summary>Initializes a new instance of the RadTabbedFormControlItem class with the specified caption.</summary>
        public RadTabbedFormControlItem (string text)
        {
            TabPage = new TabPage (text);
            ContentPanel = new RadPanel { Dock = DockStyle.Fill };
            TabPage.Controls.Add (ContentPanel);
        }

        /// <summary>The backing tab page held by the host tab control.</summary>
        internal TabPage TabPage { get; }

        /// <summary>The owning control, set when this item is added to a collection.</summary>
        internal RadTabbedFormControl? Owner { get; set; }

        /// <summary>Gets or sets the tab caption (Telerik alias for the page text).</summary>
        public string Text {
            get => TabPage.Text;
            set {
                TabPage.Text = value;
                Owner?.RaiseItemsChanged ();
            }
        }

        /// <summary>Gets or sets the tab name/key.</summary>
        public string Name {
            get => TabPage.Name;
            set => TabPage.Name = value;
        }

        /// <summary>Gets the panel that hosts this tab's content controls.</summary>
        public RadPanel ContentPanel { get; }

        /// <summary>Gets or sets whether this tab is the selected tab.</summary>
        public bool IsSelected {
            get => Owner?.SelectedTab == this;
            set {
                if (value && Owner != null)
                    Owner.SelectedTab = this;
            }
        }
    }

    /// <summary>Telerik-compat collection of <see cref="RadTabbedFormControlItem"/> document tabs.</summary>
    public class RadTabbedFormControlItemCollection : IEnumerable<RadTabbedFormControlItem>
    {
        private readonly RadTabbedFormControl _owner;
        private readonly List<RadTabbedFormControlItem> _items = new ();

        internal RadTabbedFormControlItemCollection (RadTabbedFormControl owner)
        {
            _owner = owner;
        }

        /// <summary>Gets the number of tabs in the collection.</summary>
        public int Count => _items.Count;

        /// <summary>Gets the tab at the specified index.</summary>
        public RadTabbedFormControlItem this[int index] => _items[index];

        /// <summary>Adds a tab to the collection and its page to the host control.</summary>
        public RadTabbedFormControlItem Add (RadTabbedFormControlItem item)
        {
            item.Owner = _owner;
            _items.Add (item);
            _owner.Host.TabPages.Add (item.TabPage);
            _owner.RaiseItemsChanged ();
            return item;
        }

        /// <summary>Creates and adds a tab with the specified caption.</summary>
        public RadTabbedFormControlItem Add (string text) => Add (new RadTabbedFormControlItem (text));

        /// <summary>Adds a range of tabs to the collection.</summary>
        public void AddRange (IEnumerable<RadTabbedFormControlItem> items)
        {
            foreach (var item in items)
                Add (item);
        }

        /// <summary>Removes a tab from the collection and its page from the host control.</summary>
        public bool Remove (RadTabbedFormControlItem item)
        {
            _owner.Host.TabPages.Remove (item.TabPage);
            item.Owner = null;
            var removed = _items.Remove (item);
            if (removed)
                _owner.RaiseItemsChanged ();
            return removed;
        }

        /// <summary>Removes the tab at the specified index.</summary>
        public void RemoveAt (int index) => Remove (_items[index]);

        /// <summary>Removes all tabs from the collection.</summary>
        public void Clear ()
        {
            foreach (var item in _items)
                item.Owner = null;
            _items.Clear ();
            _owner.Host.TabPages.Clear ();
            _owner.RaiseItemsChanged ();
        }

        /// <summary>Returns whether the collection contains the specified tab.</summary>
        public bool Contains (RadTabbedFormControlItem item) => _items.Contains (item);

        /// <summary>Returns the zero-based index of the specified tab, or -1.</summary>
        public int IndexOf (RadTabbedFormControlItem item) => _items.IndexOf (item);

        // Reorders an existing item to a new index (used by title-bar drag-to-reorder). The host page
        // order is left unchanged — selection maps tab to page by reference, not by index.
        internal void Move (RadTabbedFormControlItem item, int newIndex)
        {
            var oldIndex = _items.IndexOf (item);
            if (oldIndex < 0)
                return;

            newIndex = Math.Clamp (newIndex, 0, _items.Count - 1);
            if (newIndex == oldIndex)
                return;

            _items.RemoveAt (oldIndex);
            _items.Insert (newIndex, item);
            _owner.RaiseItemsChanged ();
        }

        internal RadTabbedFormControlItem? FromPage (TabPage? page) =>
            page == null ? null : _items.FirstOrDefault (i => i.TabPage == page);

        /// <inheritdoc/>
        public IEnumerator<RadTabbedFormControlItem> GetEnumerator () => _items.GetEnumerator ();

        IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>
    /// The document-tab strip that presents a <see cref="RadTabbedForm"/>'s tabs (in the title bar on
    /// custom-chrome platforms, docked below the title bar on native-chrome platforms). Reuses
    /// <see cref="TabStrip"/> for layout/hit-testing/rendering and adds drag-to-reorder, tear-off and
    /// re-attach. Clicking empty space (or a single-tab strip) drags the window instead.
    /// </summary>
    internal sealed class RadDocumentTabStrip : TabStrip
    {
        // Distance (logical px) the pointer must leave the strip's vertical band before a drag is
        // treated as a tear-off rather than an in-strip reorder.
        private const int TearOffThreshold = 12;

        private readonly RadTabbedForm _form;
        private readonly RadTabbedFormControl _control;

        private RadTabbedFormControlItem? _drag_item;
        private bool _dragging;
        private bool _syncing;

        public RadDocumentTabStrip (RadTabbedForm form, RadTabbedFormControl control)
        {
            _form = form;
            _control = control;

            _control.ItemsChanged += SyncFromOwner;
            _control.SelectedTabChanged += (_, _) => SyncSelectionFromOwner ();
            SelectedTabChanged += OnStripSelectedTabChanged;

            SyncFromOwner ();
        }

        // The strip's bounds in screen coordinates (used for cross-window drop hit-testing).
        internal Rectangle ScreenBounds {
            get {
                var topLeft = PointToScreen (Point.Empty);
                return new Rectangle (topLeft.X, topLeft.Y, Width, Height);
            }
        }

        // Fully qualified: Majorsilence.Forms.Telerik also declares a (Telerik-compat) TabStripItem;
        // this strip is UI chrome and always means the Majorsilence.Forms one.
        private Majorsilence.Forms.TabStripItem? TabAt (Point location) => Tabs.FirstOrDefault (t => t.Bounds.Contains (location));

        private static RadTabbedFormControlItem? ItemOf (Majorsilence.Forms.TabStripItem? tab) => tab?.Tag as RadTabbedFormControlItem;

        // Rebuilds the visible tabs from the owning control's items, preserving selection.
        private void SyncFromOwner ()
        {
            _syncing = true;
            try {
                Tabs.Clear ();
                foreach (var item in _control.Items)
                    Tabs.Add (new Majorsilence.Forms.TabStripItem (item.Text) { Tag = item });

                ApplyOwnerSelection ();
            } finally {
                _syncing = false;
            }

            // Lay the tabs out immediately so a drag-reorder hit-test (which reads Bounds) sees the new
            // positions without waiting for the next paint — otherwise consecutive moves no-op until a
            // repaint repopulates Bounds and the reorder feels stuck.
            LayoutTabsNow ();
            Invalidate ();
        }

        private void LayoutTabsNow () =>
            StackLayoutEngine.HorizontalExpand.Layout (ClientRectangle, Tabs.Cast<ILayoutable> ());

        private void SyncSelectionFromOwner ()
        {
            _syncing = true;
            try {
                ApplyOwnerSelection ();
            } finally {
                _syncing = false;
            }

            Invalidate ();
        }

        private void ApplyOwnerSelection ()
        {
            var selected = _control.SelectedTab;
            SelectedTab = selected is null ? null : Tabs.FirstOrDefault (t => ItemOf (t) == selected);
        }

        private void OnStripSelectedTabChanged (object? sender, EventArgs e)
        {
            if (_syncing)
                return;

            _control.SelectedTab = ItemOf (SelectedTab);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (e.Button != MouseButtons.Left)
                return;

            var tab = TabAt (e.Location);

            // Empty space (no tab under the pointer): nothing to drag. When the strip lives in the title
            // bar (custom chrome, or merged into the native macOS title bar), fall back to dragging the
            // whole window so the empty caption area still works; when it's docked in the client area
            // there's no window-drag affordance to mimic, so just ignore.
            if (tab is null) {
                _drag_item = null;
                _dragging = false;
                Capture = false;
                if (!_form.UseSystemDecorations || _form.ExtendsContentIntoTitleBar)
                    _form.BeginMoveDrag ();
                return;
            }

            // A real tab was grabbed — track our own drag for reorder / tear-off / re-attach. This runs
            // even for a single-tab window (e.g. one produced by an earlier tear-off): grabbing its only
            // tab must be draggable so it can be re-attached to another tabbed form. (Falling back to
            // BeginMoveDrag here, as we do for empty space, would hand the gesture to the OS move loop and
            // we'd never see the drop — which is exactly why re-attach from a torn-off window failed.)
            SelectedTab = tab;
            _drag_item = ItemOf (tab);
            _dragging = _drag_item != null;
            Capture = _dragging;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (!_dragging || _drag_item is null)
                return;

            // Reorder live while the pointer stays within (or near) the strip's vertical band.
            var withinBand = e.Location.Y >= -TearOffThreshold && e.Location.Y <= Height + TearOffThreshold;
            if (!withinBand || !_control.EnableTabReorder)
                return;

            var over = TabAt (new Point (e.Location.X, Height / 2));
            var overItem = ItemOf (over);
            if (overItem != null && overItem != _drag_item)
                _control.Items.Move (_drag_item, _control.Items.IndexOf (overItem));
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (!_dragging || _drag_item is null) {
                _dragging = false;
                _drag_item = null;
                return;
            }

            Capture = false;
            _dragging = false;

            var item = _drag_item;
            _drag_item = null;

            var dropScreen = PointToScreen (e.Location);

            // Dropped back inside our own strip → the live reorder is the final result.
            if (ScreenBounds.Contains (dropScreen))
                return;

            // Dropped on another tabbed form's tab strip → re-attach there.
            var target = RadTabbedForm.FindFormAt (dropScreen, _form);
            if (target != null) {
                _control.Items.Remove (item);
                target.TabbedFormControl.Items.Add (item);
                target.TabbedFormControl.SelectedItem = item;
                target.BringToFront ();

                // If that move emptied this window, close it. Defer via BeginInvoke so the form isn't
                // disposed while we're still unwinding its own strip's mouse-up handler.
                if (_control.Items.Count == 0)
                    _form.BeginInvoke (_form.Close);
                return;
            }

            // Dropped on empty desktop. If this is the only tab there's nothing to tear off into a *new*
            // window — that would just abandon an empty source window — so move this window to the drop
            // point instead. Otherwise tear the tab off into a new window at the drop point.
            if (_control.Items.Count <= 1)
                _form.Location = new Point (dropScreen.X - 40, dropScreen.Y - 8);
            else
                DetachToNewWindow (item, dropScreen);
        }

        private void DetachToNewWindow (RadTabbedFormControlItem item, Point dropScreen)
        {
            var torn = new RadTabbedForm {
                Text = item.Text,
                StartPosition = FormStartPosition.Manual,
                Size = _form.Size
            };

            _control.Items.Remove (item);
            torn.TabbedFormControl.Items.Add (item);
            torn.TabbedFormControl.SelectedItem = item;

            torn.Show ();
            // Offset so the torn-off tab sits roughly under the cursor rather than the window corner.
            torn.Location = new Point (dropScreen.X - 40, dropScreen.Y - 8);
            torn.BringToFront ();
        }
    }
}
