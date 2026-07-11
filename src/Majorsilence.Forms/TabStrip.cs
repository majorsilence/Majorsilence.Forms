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
        private void LayoutTabs ()
        {
            var avail = Math.Max (60, ClientRectangle.Width);
            var rowHeight = LogicalToDeviceUnits (DefaultSize.Height);

            var x = 0;
            var row = 0;
            foreach (var tab in Tabs) {
                var width = Math.Min (Math.Max (1, tab.GetPreferredSize (Size.Empty).Width), avail);

                if (x > 0 && x + width > avail) {
                    x = 0;
                    row++;
                }

                tab.SetBounds (x, row * rowHeight, width, rowHeight);
                x += width;
            }

            RowCount = row + 1;

            // Grow (or shrink) the strip to fit every row; no-op while the row count is stable.
            var desired = RowCount * DefaultSize.Height;
            if (Height != desired)
                Height = desired;
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            base.OnClick (e);

            var clicked_tab = GetTabAtLocation (e.Location);

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
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            // TODO: This should only be done when tabs are added or removed, or the TabStrip is resized.
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
                if (Tabs.SelectedIndex != value) {
                    Tabs.SelectedIndex = value;
                    OnSelectedTabChanged (EventArgs.Empty);

                    Invalidate ();
                }
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
