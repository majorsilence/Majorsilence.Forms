using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TabStrip control.
    /// </summary>
    public class TabStrip : Control
    {
        /// <summary>
        /// Initializes a new instance of the TabStrip class.
        /// </summary>
        public TabStrip ()
        {
            Tabs = new TabStripItemCollection (this);
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 31);

        // The TabControl (or Ribbon) this strip is the header of, when it has one. The strip is an
        // implicit child of its owner, so the owner's Alignment/ItemSize/SizeMode/Padding are read
        // from here rather than mirrored into a second set of fields.
        internal TabControl? OwnerTabControl => Parent as TabControl;

        // Vetoable notification handed to the owner BEFORE a selection change is committed. The owner
        // raises TabControl.Deselecting/Deselected from it, which is the only point at which
        // TabControl.SelectedTab still reports the OUTGOING page -- a handler saving the page it is
        // leaving used to be shown the page it was arriving at (LAY-13). Returning false vetoes the
        // change, so a cancelled Deselecting never moves the strip at all.
        internal Func<int, bool>? SelectionChanging;

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.BackgroundColor;
            });

        private int FindNextTab (int startIndex, bool forward, bool wrap)
        {
            if (forward) {
                for (var i = startIndex + 1; i < Tabs.Count; i++)
                    if (Tabs[i].Enabled)
                        return i;
                if (wrap) {
                    for (var i = 0; i < startIndex; i++)
                        if (Tabs[i].Enabled)
                            return i;
                }
            } else {
                for (var i = startIndex - 1; i >= 0; i--)
                    if (Tabs[i].Enabled)
                        return i;
                if (wrap) {
                    for (var i = Tabs.Count - 1; i > startIndex; i--)
                        if (Tabs[i].Enabled)
                            return i;
                }
            }

            return -1;
        }

        // Returns the tab at the specified location.
        private TabStripItem? GetTabAtLocation (Point location) => Tabs.FirstOrDefault (tp => tp.Bounds.Contains (location));

        /// <summary>Gets the number of tab rows currently displayed (tabs wrap when they overflow).</summary>
        public int RowCount { get; private set; } = 1;

        // Lays the tabs out left-to-right at their preferred widths, WRAPPING to a new row when a
        // tab would cross the strip's right edge (multiline tab behavior) so every tab stays
        // visible and clickable. The strip grows to hold all rows; since it docks at the top of
        // its TabControl, the pages automatically move below the whole band.
        //
        // TabControl.Alignment = Left/Right instead stacks the tabs in a single column and the strip
        // sizes its own width; ItemSize/SizeMode/Padding adjust the extents (LAY-15).
        private void LayoutTabs ()
        {
            var owner = OwnerTabControl;

            // ItemSize.Height replaces the row height outright; ItemSize.Width only applies under
            // SizeMode.Fixed, which is how upstream's TCS_FIXEDWIDTH reads the same two values.
            var item_size = owner?.ItemSize ?? Size.Empty;
            var size_mode = owner?.SizeMode ?? TabSizeMode.Normal;
            var extra_height = 2 * (owner?.Padding.Y ?? 0);
            var row_height = (item_size.Height > 0 ? item_size.Height : DefaultSize.Height) + extra_height;

            if (owner is { Alignment: TabAlignment.Left or TabAlignment.Right }) {
                LayoutTabsVertically (row_height, item_size, size_mode);
                return;
            }

            // All logical. Tab Bounds are logical and are hit-tested against logical MouseEventArgs
            // coordinates, but ClientRectangle is device-scaled and rowHeight was being scaled up too --
            // so on a 2x display tabs got device-sized rows and a logical width, and a click aimed at one
            // tab landed on another. Identity at scaling 1.
            var avail = Math.Max (60, DeviceToLogicalUnits (ClientRectangle.Width));

            // Widths first, then rows, because SizeMode.FillToRight has to know how many tabs share a
            // row before it can hand out the slack.
            var widths = new int[Tabs.Count];
            var rows = new int[Tabs.Count];
            var x = 0;
            var row = 0;

            for (var i = 0; i < Tabs.Count; i++) {
                var width = Math.Min (Math.Max (1, MeasureTab (Tabs[i], item_size, size_mode)), avail);

                if (x > 0 && x + width > avail) {
                    x = 0;
                    row++;
                }

                widths[i] = width;
                rows[i] = row;
                x += width;
            }

            RowCount = row + 1;

            if (size_mode == TabSizeMode.FillToRight)
                FillRowsToRight (widths, rows, avail);

            var offset = 0;
            for (var i = 0; i < Tabs.Count; i++) {
                if (i > 0 && rows[i] != rows[i - 1])
                    offset = 0;

                Tabs[i].SetBounds (offset, rows[i] * row_height, widths[i], row_height);
                offset += widths[i];
            }

            // Grow (or shrink) the strip to fit every row; no-op while the row count is stable.
            var desired = RowCount * row_height;
            if (Height != desired)
                Height = desired;
        }

        // Alignment = Left/Right: one column of full-width tabs, and the strip takes the width of the
        // widest of them (docked to a side, the layout engine keeps whatever width the strip asks for).
        private void LayoutTabsVertically (int rowHeight, Size itemSize, TabSizeMode sizeMode)
        {
            RowCount = 1;

            var width = 1;
            for (var i = 0; i < Tabs.Count; i++)
                width = Math.Max (width, MeasureTab (Tabs[i], itemSize, sizeMode));

            for (var i = 0; i < Tabs.Count; i++)
                Tabs[i].SetBounds (0, i * rowHeight, width, rowHeight);

            if (Tabs.Count > 0 && Width != width)
                Width = width;
        }

        // A tab's laid-out width: its measured preferred width, or the fixed one when the owner asked
        // for SizeMode.Fixed with a real ItemSize.Width.
        private static int MeasureTab (TabStripItem tab, Size itemSize, TabSizeMode sizeMode)
            => sizeMode == TabSizeMode.Fixed && itemSize.Width > 0
                ? itemSize.Width
                : tab.GetPreferredSize (Size.Empty).Width;

        // SizeMode.FillToRight (upstream's TCS_RIGHTJUSTIFY): every row is stretched to the strip's
        // width, the slack split evenly and the rounding remainder given to the last tab in the row so
        // the row ends exactly on the edge.
        private static void FillRowsToRight (int[] widths, int[] rows, int available)
        {
            var start = 0;

            while (start < widths.Length) {
                var end = start;
                var used = 0;

                while (end < widths.Length && rows[end] == rows[start]) {
                    used += widths[end];
                    end++;
                }

                var count = end - start;
                var slack = available - used;

                if (slack > 0) {
                    var share = slack / count;

                    for (var i = start; i < end; i++)
                        widths[i] += share;

                    widths[end - 1] += slack - (share * count);
                }

                start = end;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            // WinForms commits the tab change on mouse DOWN, so by the time Click and MouseClick are
            // raised the new tab is already current. Selecting in OnMouseClick instead meant every
            // Click handler observed the tab the user had just left -- and migrated code reads
            // SelectedTab inside Click to decide which tab's data to load.
            if (e.Button == MouseButtons.Left)
                SelectTabAt (e.Location);
        }

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            base.OnMouseClick (e);

            // Kept for input paths that deliver a click without a preceding mouse-down. The setter
            // ignores a selection that has not changed, so this is a no-op after OnMouseDown.
            SelectTabAt (e.Location);
        }

        private void SelectTabAt (System.Drawing.Point location)
        {
            var clicked_tab = GetTabAtLocation (location);

            // This does a null check
            if (clicked_tab?.Enabled == true)
                SelectedTab = clicked_tab;
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            // Left and right select the next tab, no wrapping
            // Ctrl-Tab and Ctrl-Shift-Tab select the next tab, with wrapping
            // Ctrl-PageUp and Ctrl-PageDown select the next tab, with wrapping
            if (e.KeyCode == Keys.Right || (e.KeyCode == Keys.Tab && e.Control && !e.Shift) || (e.KeyCode == Keys.PageDown && e.Control)) {
                SelectNextTab (true, false, (e.KeyCode == Keys.Tab && e.Control && !e.Shift) || (e.KeyCode == Keys.PageDown && e.Control));
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Left || (e.KeyCode == Keys.Tab && e.Control && e.Shift) || (e.KeyCode == Keys.PageUp && e.Control)) {
                SelectNextTab (false, false, (e.KeyCode == Keys.Tab && e.Control && e.Shift) || (e.KeyCode == Keys.PageUp && e.Control));
                e.Handled = true;
                return;
            }

            // End selects the last tab
            if (e.KeyCode == Keys.End) {
                SelectNextTab (true, true, false);
                e.Handled = true;
                return;
            }

            // Home selects the first tab
            if (e.KeyCode == Keys.Home) {
                SelectNextTab (false, true, false);
                e.Handled = true;
                return;
            }

            base.OnKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            Tabs.HoveredIndex = -1;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            var hover_tab = GetTabAtLocation (e.Location);
            Tabs.HoveredIndex = hover_tab is null ? -1 : Tabs.IndexOf (hover_tab);
        }

        /// <inheritdoc/>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            // Settle the wrap (including the strip's own height) during the LAYOUT phase so the
            // first painted frame is already correct; growing the strip mid-paint leaves wrapped
            // rows clipped outside the current back buffer until the next frame.
            LayoutTabs ();
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            // Kept for paths that paint without a preceding layout pass (cheap and idempotent).
            LayoutTabs ();

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the SelectedTabChanged event.
        /// </summary>
        protected virtual void OnSelectedTabChanged (EventArgs e) => SelectedTabChanged?.Invoke (this, e);

        /// <summary>
        /// Raised when the selected tab changes.
        /// </summary>
        public event EventHandler? SelectedTabChanged;

        private void SelectNextTab (bool forward, bool end, bool wrap)
        {
            if (!end) {
                var index = FindNextTab (SelectedIndex, forward, wrap);

                if (index != -1)
                    SelectedIndex = index;

                return;
            }

            if (forward) {
                var index = FindNextTab (Tabs.Count, false, false);

                if (index != -1)
                    SelectedIndex = index;

                return;
            }

            if (!forward) {
                var index = FindNextTab (-1, true, false);

                if (index != -1)
                    SelectedIndex = index;

                return;
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <summary>
        /// Gets or sets the index of the currently selected tab.
        /// </summary>
        public int SelectedIndex {
            get => Tabs.SelectedIndex;
            set {
                if (Tabs.SelectedIndex == value)
                    return;

                // Validate up front. The owner's veto below raises its cancelable Deselecting, and
                // throwing after that would leave handlers having seen a change that never happened.
                Tabs.ValidateIndex (value);

                // The owner gets its veto -- and with it the chance to raise Deselecting/Deselected
                // while this strip is still on the outgoing tab -- before anything moves. An empty
                // collection has no outgoing tab (designer code emits SelectedIndex = 0 before the
                // tabs exist), so there is nothing to announce and nothing to cancel.
                if (Tabs.Count > 0 && SelectionChanging?.Invoke (value) == false)
                    return;

                Tabs.SelectedIndex = value;
                OnSelectedTabChanged (EventArgs.Empty);

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets or sets the currently selected tab.
        /// </summary>
        public TabStripItem? SelectedTab {
            get => SelectedIndex >= 0 ? Tabs[SelectedIndex] : null;
            set {
                if (value is null) {
                    SelectedIndex = -1;
                    return;
                }

                var index = Tabs.IndexOf (value);

                if (index == -1)
                    throw new ArgumentException ("Item is not part of this list");

                SelectedIndex = index;
            }
        }

        /// <summary>
        /// Gets the collection of tabs contained by this TabStrip.
        /// </summary>
        public TabStripItemCollection Tabs { get; }
    }
}
