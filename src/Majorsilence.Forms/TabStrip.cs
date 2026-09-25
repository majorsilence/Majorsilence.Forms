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

        // Part styles for the tabs themselves (CSS `TabStrip::item`, `::item:hover`, `::selected`).
        // Hover and selected layer on the item style, so a colour set on `::item` carries into both.

        /// <summary>The default style of a tab: an optional background and the caption colour (unset = the strip's ambient text colour).</summary>
        public static readonly ControlStyle DefaultItemStyle = new ControlStyle (null, _ => { });

        /// <summary>The default style of a hovered tab. CSS: <c>TabStrip::item:hover</c>.</summary>
        public static readonly ControlStyle DefaultItemHoverStyle = new ControlStyle (DefaultItemStyle,
            (style) => style.BackgroundColor = Theme.ControlLowColor);

        /// <summary>
        /// The default style of the selected tab; <c>Border.Bottom</c> is the accent underline (colour and
        /// thickness). CSS: <c>TabStrip::selected</c>.
        /// </summary>
        public static readonly ControlStyle DefaultSelectedItemStyle = new ControlStyle (DefaultItemStyle,
            (style) => {
                style.Border.Bottom.Color = Theme.AccentColor2;
                style.Border.Bottom.Width = 3;
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
        private TabStripItem? GetTabAtLocation (Point location)
            => ScrollArrowBand.Contains (location) ? null : Tabs.FirstOrDefault (tp => tp.Bounds.Contains (location));

        // Single-row scrolling (TabControl.Multiline = false, W6 mechanisms). The tabs are laid out in
        // one row shifted left by scroll_offset (logical); when they overflow the strip an arrow band
        // at the trailing edge scrolls them a tab at a time, and selecting a tab scrolls it into view.
        private int scroll_offset;

        /// <summary>The logical width the arrow band takes when the tabs overflow a single-row strip.</summary>
        internal const int ScrollArrowBandWidth = 34;

        /// <summary>Whether the single-row layout has more tabs than fit, so the arrows are showing.</summary>
        internal bool TabsOverflow { get; private set; }

        /// <summary>The logical band the scroll arrows occupy; empty when nothing overflows.</summary>
        internal Rectangle ScrollArrowBand
            => TabsOverflow
                ? new Rectangle (DeviceToLogicalUnits (ClientRectangle.Width) - ScrollArrowBandWidth, 0, ScrollArrowBandWidth, DeviceToLogicalUnits (ClientRectangle.Height))
                : Rectangle.Empty;

        /// <summary>How far the single row is scrolled, in logical pixels.</summary>
        internal int ScrollOffset => scroll_offset;

        /// <summary>Scrolls the single row so the tab at <paramref name="index"/> is wholly visible.</summary>
        internal void EnsureTabVisible (int index)
        {
            if (index < 0 || index >= Tabs.Count || OwnerTabControl is not { Multiline: false })
                return;

            var bounds = Tabs[index].Bounds;
            var visible_right = DeviceToLogicalUnits (ClientRectangle.Width) - (TabsOverflow ? ScrollArrowBandWidth : 0);

            if (bounds.Left < 0)
                scroll_offset = Math.Max (0, scroll_offset + bounds.Left);
            else if (bounds.Right > visible_right)
                scroll_offset += bounds.Right - visible_right;
            else
                return;

            LayoutTabs ();
            Invalidate ();
        }

        // A click in the arrow band scrolls one tab in that direction: the leading half backwards,
        // the trailing half forwards.
        private void ScrollByArrow (Point location)
        {
            var band = ScrollArrowBand;
            var forward = location.X >= band.Left + band.Width / 2;
            var first_hidden = -1;

            if (forward) {
                for (var i = 0; i < Tabs.Count; i++)
                    if (Tabs[i].Bounds.Right > band.Left) { first_hidden = i; break; }
            } else {
                for (var i = Tabs.Count - 1; i >= 0; i--)
                    if (Tabs[i].Bounds.Left < 0) { first_hidden = i; break; }
            }

            if (first_hidden < 0)
                return;

            if (forward)
                scroll_offset += Tabs[first_hidden].Bounds.Right - band.Left;
            else
                scroll_offset = Math.Max (0, scroll_offset + Tabs[first_hidden].Bounds.Left);

            LayoutTabs ();
            Invalidate ();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <see cref="TabControl.ShowToolTips"/> and <see cref="TabPage.ToolTipText"/>, both stored and
        /// read by nothing before (<c>LST-59</c>). The override lives here rather than on
        /// <c>TabControl</c> because the strip is the control the pointer is actually over -- the tabs
        /// are its children, not the tab control's.
        /// </remarks>
        internal override string? GetToolTipText (Point location)
        {
            if (OwnerTabControl is not { ShowToolTips: true } owner)
                return null;

            return owner.PageFor (GetTabAtLocation (location))?.ToolTipText;
        }

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

            // A single row (Multiline = false, upstream's default) never wraps: the overflow scrolls
            // behind the arrow band instead (W6 mechanisms). An owner-less strip keeps wrapping.
            if (owner is { Multiline: false }) {
                LayoutTabsInOneRow (row_height, item_size, size_mode, avail);
                return;
            }

            TabsOverflow = false;
            scroll_offset = 0;

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
                ResizeKeepingDockedEdge (Width, desired);
        }

        // The strip measures its own wrapped size here, from inside OnLayout -- which the parent runs
        // as part of ITS layout pass, after the dock pass has already positioned the strip from the
        // size it had on entry. That entry size is routinely stale: the first passes run before the
        // TabControl is sized, so the tabs wrap against a zero width and the strip comes out of them
        // several rows tall. A bottom-docked element is placed at (container.Bottom - Height), so
        // shrinking the height afterwards moves the strip's bottom edge off the container's and leaves
        // the header floating above it -- 62px up, for a 93px three-row measurement settling back to
        // one 31px row. Nothing corrects it: the resize asks the parent to lay out again, but the
        // parent is mid-pass, so PerformLayout only records LayoutDeferred and then clears that very
        // flag when the pass it is nested in unwinds. The first thing to trigger a fresh top-level
        // layout -- clicking a tab, a resize -- snapped the strip into place, which made this look like
        // a selection bug.
        //
        // The dock pass anchors the edge it docks to (Bottom pins Bottom, Right pins Right), so
        // holding that edge across the resize puts the strip exactly where the pass intended and the
        // discarded re-layout stops mattering. Top/Left dock to the origin, which a resize cannot
        // move, so they keep the plain assignment.
        private void ResizeKeepingDockedEdge (int width, int height)
        {
            var x = Dock == DockStyle.Right ? Right - width : Left;
            var y = Dock == DockStyle.Bottom ? Bottom - height : Top;

            SetBounds (x, y, width, height);
        }

        // Multiline = false: every tab at its measured width in one row, shifted by the scroll offset.
        // FillToRight stretches the row only when it fits; an overflowing row has no slack to share.
        private void LayoutTabsInOneRow (int rowHeight, Size itemSize, TabSizeMode sizeMode, int available)
        {
            RowCount = 1;

            var widths = new int[Tabs.Count];
            var total = 0;

            for (var i = 0; i < Tabs.Count; i++) {
                widths[i] = Math.Max (1, MeasureTab (Tabs[i], itemSize, sizeMode));
                total += widths[i];
            }

            TabsOverflow = total > available;

            if (!TabsOverflow) {
                scroll_offset = 0;

                if (sizeMode == TabSizeMode.FillToRight)
                    FillRowsToRight (widths, new int[Tabs.Count], available);
            } else {
                // Never scroll past the point where the last tab sits against the arrow band.
                scroll_offset = Math.Max (0, Math.Min (scroll_offset, total - (available - ScrollArrowBandWidth)));
            }

            var offset = -scroll_offset;

            for (var i = 0; i < Tabs.Count; i++) {
                Tabs[i].SetBounds (offset, 0, widths[i], rowHeight);
                offset += widths[i];
            }

            if (Height != rowHeight)
                ResizeKeepingDockedEdge (Width, rowHeight);
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
                ResizeKeepingDockedEdge (width, Height);
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
            if (e.Button == MouseButtons.Left) {
                if (ScrollArrowBand.Contains (e.Location)) {
                    ScrollByArrow (e.Location);
                    return;
                }

                SelectTabAt (e.Location);
            }
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

                EnsureTabVisible (value);
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
