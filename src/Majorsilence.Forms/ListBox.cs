using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ListBox control.
    /// </summary>
    public partial class ListBox : ListControl
    {
        private int item_height = -1;
        private SelectionMode selection_mode = SelectionMode.One;
        private bool scrollbar_always_visible;
        private int top_index;
        private readonly VerticalScrollBar vscrollbar;

        // W6 mechanisms: the horizontal scrollbar serves two layouts. A MultiColumn list scrolls by
        // whole columns (its value is the first visible column); a single-column list with
        // HorizontalScrollbar scrolls by device pixels across HorizontalExtent (its value is the
        // pixel offset, kept in h_offset).
        private readonly HorizontalScrollBar hscrollbar;
        private int h_offset;
        private bool multi_column;
        private int column_width;
        private bool horizontal_scrollbar;
        private int horizontal_extent;
        private bool integral_height = true;
        private bool use_tab_stops = true;
        private object? _dataSource;
        private string _displayMember = string.Empty;
        private string _valueMember = string.Empty;

        /// <summary>
        /// Initializes a new instance of the ListBox class.
        /// </summary>
        public ListBox ()
        {
            ApplyBorderStyle (border_style);
            // Through CreateItemCollection so a derived list can substitute its own collection type --
            // which is how CheckedListBox returns one that tracks check state alongside each item.
            Items = CreateItemCollection ();

            source_tracker = new DataSourceBinding.ListSourceTracker (
                RefreshDataSource,
                // Dropping an index the items cannot hold yet is deliberate: the source's manager
                // announces its position before this control has reloaded, and the reload re-applies
                // it once the items exist (see ListSourceTracker.OnListChanged).
                position => { if (position < Items.Count) SelectedIndex = position; },
                () => SelectedIndex);

            Items.CollectionChanged += (o, e) => UpdateVerticalScrollBar ();

            vscrollbar = new VerticalScrollBar {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = false,
                Dock = DockStyle.Right
            };

            vscrollbar.ValueChanged += VerticalScrollBar_ValueChanged;

            Controls.AddImplicitControl (vscrollbar);

            hscrollbar = new HorizontalScrollBar {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = false,
                Dock = DockStyle.Bottom
            };

            hscrollbar.ValueChanged += HorizontalScrollBar_ValueChanged;

            Controls.AddImplicitControl (hscrollbar);
        }

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (120, 96);

        /// <inheritdoc/>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.Border.Width = 1;
            });

        /// <summary>
        /// The default style of the selection highlight: the selected item's background and, when set,
        /// its text colour. CSS: <c>ListBox::selection</c>.
        /// </summary>
        public static readonly ControlStyle DefaultSelectionStyle = new ControlStyle (null,
            (style) => style.BackgroundColor = Theme.ControlHighlightLowColor);

        private void EnsureItemVisible (int index)
        {
            // If there aren't enough items to need scrolling, things are good
            if (VisibleItemCount >= Items.Count)
                return;

            if (index < FirstVisibleIndex)
                FirstVisibleIndex = index;
            else if (index >= FirstVisibleIndex + VisibleItemCount)
                FirstVisibleIndex = index - VisibleItemCount + 1;
        }

        /// <summary>
        /// Finds the index of the next item after startIndex that begins with the specified string. This search is case-insensitive.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage ("Globalization", "CA1309:Use ordinal string comparison", Justification = "This should be culture aware.")]
        public int FindString (string s, int startIndex = -1)
        {
            if (s is null || Items.Count == 0)
                return -1;

            if (startIndex < -1 || startIndex >= Items.Count)
                throw new ArgumentOutOfRangeException (nameof (startIndex));

            // We actually look for matches AFTER the start index
            startIndex = (startIndex == Items.Count - 1) ? 0 : startIndex + 1;
            var current = startIndex;

            while (true) {
                var item = Items[current]?.ToString ();

                if (string.Compare (s, 0, item, 0, s.Length, true, CultureInfo.CurrentCulture) == 0)
                    return current;

                current++;

                if (current == Items.Count)
                    current = 0;

                if (current == startIndex)
                    return -1;
            }
        }

        /// <summary>
        /// Gets or sets the index of the first visible item.
        /// </summary>
        public int FirstVisibleIndex {
            get => top_index;
            set {
                if (top_index == value)
                    return;

                if (value < 0 || value >= Items.Count)
                    return;

                if (multi_column) {
                    TopIndex = value;
                    return;
                }

                vscrollbar.Value = Math.Min (value, vscrollbar.EffectiveMaximum);
            }
        }
        /// <summary>
        /// Gets the index of the item currently at the specified location.
        /// </summary>
        /// <param name="location">The coordinates used to determine the index.</param>
        public int GetIndexAtLocation (Point location)
        {
            // Both sides logical: the point comes from a mouse handler, and GetItemRectangle answers in
            // the same space as of W6.3. The device-space rectangle behind it is GetItemRectangleDevice.
            var device = LogicalToDeviceUnits (location);

            for (var i = top_index; i < Math.Min (Items.Count, top_index + VisibleItemCount + 1); i++)
                if (GetItemRectangleDevice (i).Contains (device))
                    return i;

            return -1;
        }

        /// <summary>
        /// Gets the bounding rectangle for the specified item.
        /// </summary>
        /// <param name="index">The zero-based index of the desired item.</param>
        public Rectangle GetItemRectangle (int index)
            => DeviceToLogicalUnits (GetItemRectangleDevice (index));

        /// <summary>
        /// The item's rectangle in device pixels -- the space the renderer, the hit-test and the
        /// automation peer all work in.
        /// </summary>
        /// <remarks>
        /// W6.3: <see cref="GetItemRectangle"/> used to return this directly. It is public, so the
        /// idiom an application writes -- <c>GetItemRectangle (i).Contains (e.Location)</c>, with a
        /// logical <c>MouseEventArgs</c> point -- was wrong by the display scale, while every caller
        /// inside this assembly knew to convert. The public member now answers in logical units like
        /// everything else that faces an application, and the internal callers use this instead.
        /// </remarks>
        internal Rectangle GetItemRectangleDevice (int index)
        {
            if (index < 0 || index >= Items.Count)
                throw new ArgumentOutOfRangeException (nameof (index), "Index out of range.");

            var client = ItemsArea;

            // MultiColumn (W6 mechanisms): items run down each column and the columns run across, as
            // upstream's LBS_MULTICOLUMN lays them; top_index is the first item of the first visible
            // column. Nothing sub-row applies: the list scrolls sideways by whole columns.
            if (multi_column) {
                var rows = RowsPerColumn;
                var column = index / rows - top_index / rows;
                var row = index % rows;
                var column_left = client.Left + column * ScaledColumnWidth;
                var item_top = client.Top + row * ScaledItemHeight;

                return new Rectangle (column_left, item_top, Math.Min (ScaledColumnWidth, Math.Max (0, client.Right - column_left)), ScaledItemHeight);
            }

            // Subtract the sub-row touch-scroll offset so a fluid drag moves items by the pixel;
            // item[top_index] is then partly above client.Top and the renderer clips it there.
            // h_offset (HorizontalScrollbar) slides every item left by the scrolled amount; the band
            // grows by the same so a selection still reaches the right edge.
            var height = ItemHeightDeviceAt (index);
            var top = ItemOffsetDevice (index) + client.Top - (int) Math.Round (_scrollOffsetPx);
            return new Rectangle (client.Left - h_offset, top, client.Width + h_offset, Math.Min (height, client.Bottom - top));
        }

        /// <summary>
        /// The device rectangle the items are laid into: the client area less whichever scrollbars
        /// are showing (W6 mechanisms; the horizontal one is new).
        /// </summary>
        internal Rectangle ItemsArea {
            get {
                var client = ClientRectangle;
                var width = client.Width - (vscrollbar.Visible ? vscrollbar.ScaledWidth : 0);
                var height = client.Height - (hscrollbar.Visible ? hscrollbar.ScaledHeight : 0);
                return new Rectangle (client.Left, client.Top, Math.Max (0, width), Math.Max (0, height));
            }
        }

        /// <summary>Whole rows that fit in one column of a <see cref="MultiColumn"/> list; at least one.</summary>
        internal int RowsPerColumn => Math.Max (1, ItemsArea.Height / Math.Max (1, ScaledItemHeight));

        /// <summary>Columns a <see cref="MultiColumn"/> list needs for all its items.</summary>
        internal int ColumnCount => multi_column ? (Items.Count + RowsPerColumn - 1) / RowsPerColumn : 1;

        /// <summary>Whole columns that fit across the items area of a <see cref="MultiColumn"/> list; at least one.</summary>
        internal int VisibleColumnCount => Math.Max (1, ItemsArea.Width / Math.Max (1, ScaledColumnWidth));

        /// <summary>
        /// The width of one column in device pixels: <see cref="ColumnWidth"/>, or when that is 0 the
        /// widest item's text plus a margin, which is what upstream's default column width amounts to.
        /// </summary>
        internal int ScaledColumnWidth => LogicalToDeviceUnits (column_width > 0 ? column_width : WidestItemWidth () + 8);

        // The widest item text in logical units, measured with the control's font. Measured on demand
        // rather than cached: the count, the texts and the font can all change under it.
        private int WidestItemWidth ()
        {
            var widest = 1;

            for (var i = 0; i < Items.Count; i++)
                widest = Math.Max (widest, (int) Math.Ceiling (TextMeasurer.MeasureText (GetItemText (Items[i]), this).Width));

            return widest;
        }

        /// <summary>
        /// The horizontal extent the items need, in device pixels: <see cref="HorizontalExtent"/>
        /// when the application set one, else (as upstream measures for <see cref="DrawMode.Normal"/>)
        /// the widest item's text plus a margin. Owner-drawn items only ever scroll to the set extent.
        /// </summary>
        internal int ScaledHorizontalExtent
            => LogicalToDeviceUnits (horizontal_extent > 0 ? horizontal_extent : draw_mode == DrawMode.Normal && Items.Count > 0 ? WidestItemWidth () + 8 : 0);

        /// <summary>The horizontal scroll offset in device pixels (HorizontalScrollbar).</summary>
        internal int HorizontalOffset => h_offset;

        /// <summary>
        /// Gets or sets the unscaled height each item will use.
        /// </summary>
        public int ItemHeight {
            get {
                if (item_height == -1)
                    item_height = (int)TextMeasurer.MeasureText ("The quick brown Fox", this).Height + 3;

                return item_height;
            }
            set {
                if (value > 255)
                    throw new ArgumentOutOfRangeException (nameof (value), "The ItemHeight property was set beyond 255 pixels");

                item_height = value;

                Invalidate ();
            }
        }

        /// <summary>
        /// Gets the collection of items contained by this ListBox.
        /// </summary>
        /// <remarks>
        /// Typed as the nested <see cref="ObjectCollection"/>, which is the name WinForms code writes when
        /// it declares a variable or re-exposes this property (<c>public ListBox.ObjectCollection Items
        /// =&gt; _listBox.Items;</c>). It used to return the base <see cref="ListBoxItemCollection"/>, so
        /// that assignment needed a downcast; the instance was always an ObjectCollection anyway, which is
        /// what made the cast safe and therefore pointless.
        /// </remarks>
        public ObjectCollection Items { get; }

        /// <summary>Gets or sets the data source for the ListBox.</summary>
        public override object? DataSource {
            get => _dataSource;
            set {
                _dataSource = value;
                source_tracker.Attach (value);
                RefreshDataSource ();
            }
        }

        // Re-reads the source when it changes and keeps SelectedIndex and the source's current-item
        // position in step. Without it the control kept whatever the list held at bind time -- which for
        // designer code, where the data arrives after InitializeComponent, is nothing.
        private readonly DataSourceBinding.ListSourceTracker source_tracker;

        /// <summary>Gets or sets the property to display from the data source.</summary>
        public override string DisplayMember {
            get => _displayMember;
            set {
                _displayMember = value ?? string.Empty;
                RefreshDataSource ();
            }
        }

        /// <summary>Gets or sets the property used as the value from the data source.</summary>
        public override string ValueMember {
            get => _valueMember;
            set => _valueMember = value ?? string.Empty;
        }

        /// <summary>
        /// Returns the display text for the specified item, honoring <see cref="DisplayMember"/>.
        /// Mirrors WinForms ListControl.GetItemText.
        /// </summary>
        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection.")]
        public override string GetItemText (object? item)
        {
            // Property descriptors first so DataRowView columns resolve; see DataSourceBinding.
            return ApplyFormat (item, DataSourceBinding.DisplayText (item, _displayMember));
        }

        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection.")]
        private void RefreshDataSource ()
        {
            var list = DataSourceBinding.AsList (_dataSource);

            if (list is null)
                return;

            Items.Clear ();

            // Bound objects, not display text -- see the matching comment in ComboBox.RefreshDataSource.
            foreach (var item in list)
                Items.Add (item);
        }

        // The height that would be needed to display all items.
        private int NeededHeightForItems {
            get {
                if (draw_mode != DrawMode.OwnerDrawVariable)
                    return ScaledItemHeight * Items.Count;

                var total = 0;

                for (var i = 0; i < Items.Count; i++)
                    total += ItemHeightDeviceAt (i);

                return total;
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e) => ChangeSelection (() => KeyUpCore (e));

        // How far an arrow key moves the focus: a column in a MultiColumn list for Left/Right, else one.
        private int NavigationStep (KeyEventArgs e)
            => multi_column && e.KeyCode.In (Keys.Left, Keys.Right) ? RowsPerColumn : 1;

        // Wrapped by ChangeSelection so every branch below -- Space toggles, Shift+arrow extension, the
        // add/remove pairs -- announces its change, without each one having to remember to (LST-04).
        private void KeyUpCore (KeyEventArgs e)
        {
            // In "None" mode, the focus goes up and down
            // In "MultiSimple" mode, the focus goes up and down, and space selects or deselects
            // MultiColumn (W6 mechanisms): Left/Right cross to the neighbouring column, one row's
            // worth of items away, as upstream's list does; Up/Down still walk the column.
            var step = NavigationStep (e);

            if (selection_mode.In (SelectionMode.None, SelectionMode.MultiSimple)) {
                if (e.KeyCode.In (Keys.Down, Keys.Right)) {
                    if (Items.FocusedIndex < Items.Count - 1) {
                        Items.FocusedIndex = Math.Min (Items.Count - 1, Items.FocusedIndex + step);
                        EnsureItemVisible (Items.FocusedIndex);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.KeyCode.In (Keys.Up, Keys.Left)) {
                    if (Items.FocusedIndex > 0) {
                        Items.FocusedIndex = Math.Max (0, Items.FocusedIndex - step);
                        EnsureItemVisible (Items.FocusedIndex);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.KeyCode == Keys.Space && selection_mode == SelectionMode.MultiSimple) {
                    Items.ToggleSelectedIndex (Items.FocusedIndex);
                    e.Handled = true;
                    return;
                }
            }

            // In "One" mode, the selection goes up and down
            // In "MultiExtended" mode, the selection goes up and down, and SHIFT adds or subtracts to a contiguous selection
            if (selection_mode.In (SelectionMode.One, SelectionMode.MultiExtended)) {

                if (selection_mode == SelectionMode.MultiExtended && e.Shift) {
                    // Find contiguous selection index is part of
                    // - If at top or bottom, add or subtract based towards or away from selection
                    // - If not in contiguous, or middle of contiguous, or more than one contiguous: start new selection, adding item in direction
                    var (start, end) = Items.GetSingleContiguousSelection ();

                    if (start == -1 || (start != Items.FocusedIndex && end != Items.FocusedIndex)) {
                        // No existing selection, make a new one
                        SelectedIndex = Items.FocusedIndex;

                        if (e.KeyCode.In (Keys.Down, Keys.Right) && SelectedIndex < Items.Count - 1) {
                            Items.AddSelectedIndex (SelectedIndex + 1, false);
                            EnsureItemVisible (Items.FocusedIndex);
                            e.Handled = true;
                            return;
                        }

                        if (e.KeyCode.In (Keys.Up, Keys.Left) && SelectedIndex > 0) {
                            Items.AddSelectedIndex (SelectedIndex - 1, false);
                            EnsureItemVisible (Items.FocusedIndex);
                            e.Handled = true;
                            return;
                        }
                    } else {
                        // At the top and moving up, add to top of the selection
                        if (start == Items.FocusedIndex && e.KeyCode.In (Keys.Up, Keys.Left)) {
                            if (Items.FocusedIndex > 0) {
                                Items.AddSelectedIndex (start - 1, false);
                                EnsureItemVisible (Items.FocusedIndex);
                            }
                            e.Handled = true;
                            return;
                        }

                        // At the top and moving down, remove from top of the selection
                        if (start == Items.FocusedIndex && start != end && e.KeyCode.In (Keys.Down, Keys.Right)) {
                            Items.RemoveSelectedIndex (start);
                            Items.FocusedIndex++;
                            EnsureItemVisible (Items.FocusedIndex);
                            e.Handled = true;
                            return;
                        }

                        // At the bottom and moving down, add to bottom of the selection
                        if (end == Items.FocusedIndex && e.KeyCode.In (Keys.Down, Keys.Right)) {
                            if (Items.FocusedIndex < Items.Count - 1) {
                                Items.AddSelectedIndex (end + 1, false);
                                EnsureItemVisible (Items.FocusedIndex);
                            }
                            e.Handled = true;
                            return;
                        }

                        // At the bottom and moving up, remove from bottom of the selection
                        if (end == Items.FocusedIndex && start != end && e.KeyCode.In (Keys.Up, Keys.Left)) {
                            Items.RemoveSelectedIndex (end);
                            Items.FocusedIndex--;
                            EnsureItemVisible (Items.FocusedIndex);
                            e.Handled = true;
                            return;
                        }
                    }
                }

                // If any direction is pressed and nothing is selected, select the item with focus.
                // This is generally used to select the first item when you tab into an listbox
                // with nothing currently selected.
                if (e.KeyCode.In (Keys.Down, Keys.Right, Keys.Up, Keys.Left) && SelectedIndex == -1) {
                    SelectedIndex = Items.FocusedIndex;
                    EnsureItemVisible (Items.FocusedIndex);
                    e.Handled = true;
                    return;
                }

                if (e.KeyCode.In (Keys.Down, Keys.Right)) {
                    if (SelectedIndex < Items.Count - 1) {
                        SelectedIndex = Math.Min (Items.Count - 1, Items.FocusedIndex + step);
                        EnsureItemVisible (Items.FocusedIndex);
                        e.Handled = true;
                        return;
                    }
                }


                if (e.KeyCode.In (Keys.Up, Keys.Left)) {
                    if (SelectedIndex > 0) {
                        SelectedIndex = Math.Max (0, Items.FocusedIndex - step);
                        EnsureItemVisible (Items.FocusedIndex);
                        e.Handled = true;
                        return;
                    }
                }

                if (char.IsLetterOrDigit ((char)e.KeyCode)) {
                    var index = FindString (((char)e.KeyCode).ToString (), SelectedIndex);

                    if (index >= 0) {
                        SelectedIndex = index;
                        EnsureItemVisible (Items.FocusedIndex);
                        e.Handled = true;
                        return;
                    }
                }
            }

            base.OnKeyUp (e);
        }

        private void OnMouseButtonLogic (MouseEventArgs e) => ChangeSelection (() => MouseButtonLogicCore (e));

        // See KeyUpCore: Ctrl-click and MultiSimple toggles went through the collection's internal
        // setters and reported nothing (LST-04).
        private void MouseButtonLogicCore (MouseEventArgs e)
        {

            if (!Enabled || !e.Button.HasFlag (MouseButtons.Left))
                return;

            var index = GetIndexAtLocation (e.Location);

            if (index == -1)
                return;

            switch (SelectionMode) {
                case SelectionMode.None:
                    Items.FocusedIndex = index;
                    break;

                case SelectionMode.One:
                    SelectedIndex = index;
                    break;

                case SelectionMode.MultiSimple:
                    Items.ToggleSelectedIndex (index);
                    break;

                case SelectionMode.MultiExtended:
                    // TODO: Shift

                    // When Control is held we treat this like MultiSimple
                    if (e.Control) {
                        Items.ToggleSelectedIndex (index);
                        break;
                    }

                    // Else we treat this like SelectionMode.One
                    SelectedIndex = index;

                    break;
            }

            EnsureItemVisible (index);
        }

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            if (!SelectItemOnMouseUp)
                OnMouseButtonLogic (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (SelectItemOnMouseUp)
                OnMouseButtonLogic (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            if (ShowHover)
                Items.HoveredIndex = -1;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            if (ShowHover)
                Items.HoveredIndex = GetIndexAtLocation (e.Location);
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (vscrollbar.Visible)
                vscrollbar.RaiseMouseWheel (e);
        }

        // Fine-grained scroll: how far item[top_index] is pushed up above the client top, in device
        // pixels (0 .. ScaledItemHeight). top_index is the coarse row index and the vscrollbar tracks
        // it; this is the sub-row remainder that lets a touch drag track the finger by the pixel
        // instead of jumping a whole row at a time. Always 0 for wheel / thumb / keyboard scrolling.
        private double _scrollOffsetPx;
        private bool _settingScrollbarFromGesture;

        /// <summary>
        /// Pans the visible range on a touch drag/flick, pixel by pixel. ListBox owns its own vertical
        /// scrollbar rather than deriving from <see cref="ScrollableControl"/>, so the neutral
        /// scroll-gesture event (drag, plus the recognizer's decaying inertia deltas after lift-off) is
        /// bridged here. Content follows the finger: dragging up reveals items further down.
        /// </summary>
        protected override void OnScrollGesture (ScrollGestureEventArgs e)
        {
            base.OnScrollGesture (e);

            if (!vscrollbar.Visible)
                return;

            // This control scrolls itself; don't also pan a scrollable ancestor (RaiseScrollGesture).
            e.Handled = true;

            if (e.Delta.Y == 0)
                return;

            // e.Delta.Y is logical pixels (converted at the WindowBase boundary); scroll in device
            // pixels to line up with ScaledItemHeight and the device-space item rectangles.
            ScrollByDevicePixels (e.Delta.Y * ScaleFactor.Height);
        }

        // Scrolls by a device-pixel amount, rolling the sub-row offset over into top_index. Matches
        // the gesture-delta sign: a negative delta (upward drag) increases the scroll position.
        private void ScrollByDevicePixels (double deltaPx)
        {
            var itemH = Math.Max (1, ScaledItemHeight);
            var maxPosPx = Math.Max (0, vscrollbar.EffectiveMaximum) * (double) itemH;

            // How far item[top_index] currently sits above the client top -- see GetItemRectangle,
            // which places item[i] at client.Top + (i - top_index) * itemH - this value. Captured before
            // _scrollOffsetPx below is overwritten, so a pure sub-row move (top_index unchanged) can be
            // repainted as a pixel shift of the existing frame instead of a full re-render.
            var oldRenderOffsetPx = top_index * itemH + (int) Math.Round (_scrollOffsetPx);

            var posPx = Math.Max (0, Math.Min (top_index * (double) itemH + _scrollOffsetPx - deltaPx, maxPosPx));

            var newTop = (int) (posPx / itemH);
            _scrollOffsetPx = posPx - newTop * (double) itemH;

            if (newTop != top_index) {
                _settingScrollbarFromGesture = true;
                try { vscrollbar.Value = Math.Max (vscrollbar.Minimum, Math.Min (newTop, vscrollbar.EffectiveMaximum)); }
                finally { _settingScrollbarFromGesture = false; }
                top_index = newTop;

                // Crossing a row also moves the scroll thumb, which dirties it -- and a dirty child
                // forces this control's own OnPaint to run again anyway once its parent notices
                // NeedsPaint (see Control.PaintChildren), so there is nothing for the fast path to save
                // here. Full repaint, exactly as before.
                Invalidate ();
                return;
            }

            var newRenderOffsetPx = top_index * itemH + (int) Math.Round (_scrollOffsetPx);
            if (!TryFastScrollBlit (oldRenderOffsetPx - newRenderOffsetPx))
                Invalidate ();
        }

        // A full repaint is due (Invalidate () was called, to get this control re-blitted into its
        // ancestors -- see the comment on TryFastScrollBlit for why that call can't be skipped), but
        // TryFastScrollBlit already left the back buffer in the correct state by hand: the next
        // OnPaintBackground/OnPaint pass must do nothing and leave those pixels alone.
        //
        // Kept correct by OnInvalidated, not by "one-shot and cleared the moment OnPaint runs": several
        // scroll deltas routinely land back-to-back before the next real paint (the Android host
        // throttles its own repaint while a finger is down), so TryFastScrollBlit can run several times
        // in a row while this is still pending -- each further shift must still take the fast path.
        // OnInvalidated turns it back off the moment anything OTHER than TryFastScrollBlit calls
        // Invalidate () (a selection/data change, say): that content cannot be produced by shifting
        // pixels, so the pending state must fall through to a real, full repaint.
        private bool _skipNextRepaint;
        private bool _invalidatingForFastScroll;

        /// <inheritdoc/>
        protected override void OnInvalidated (InvalidateEventArgs e)
        {
            _skipNextRepaint = _invalidatingForFastScroll;
            base.OnInvalidated (e);
        }

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            if (_skipNextRepaint)
                return;
            base.OnPaintBackground (e);
        }

        // Repaints a pure sub-row scroll (the common case for a touch drag/fling: top_index unchanged,
        // no scrollbar thumb movement to keep in sync) by shifting the control's own already-rendered
        // back buffer by the pixel amount the content actually moved, then rendering only the strip of
        // rows the shift newly exposed -- instead of re-running every visible row's renderer (text
        // shaping included) on every scroll frame. <paramref name="shiftPx"/> is the on-screen
        // displacement (positive = content moves down, revealing earlier rows at the top; negative =
        // content moves up, revealing later rows at the bottom).
        //
        // Returns false (no repaint performed -- the caller falls back to Invalidate()) whenever the
        // shortcut is not safe: nothing to shift from yet, a full repaint is already pending for some
        // other reason (NeedsPaint but _skipNextRepaint is false -- see OnInvalidated), or a background
        // image / rounded border would need re-compositing across the whole client area rather than
        // just the exposed strip.
        private bool TryFastScrollBlit (int shiftPx)
        {
            if (shiftPx == 0 || (NeedsPaint && !_skipNextRepaint) || BackgroundImage is not null || CurrentStyle.Border.GetRadius () > 0)
                return false;

            if (BackBufferPixels is not { } buffer || buffer.Width != ScaledSize.Width || buffer.Height != ScaledSize.Height)
                return false;   // no existing frame to shift (never painted yet, or just resized)

            var client = ClientRectangle;
            var contentWidth = client.Width - (vscrollbar.Visible ? vscrollbar.ScaledWidth : 0);
            if (contentWidth <= 0 || Math.Abs (shiftPx) >= client.Height)
                return false;

            // Excludes the scrollbar's own strip (a separate child, composited on top of this buffer by
            // the normal paint pass) so its already-correct pixels are left exactly where they are.
            var contentRect = new Rectangle (client.Left, client.Top, contentWidth, client.Height);

            // A deep copy, not SKImage.FromBitmap (which can share the source's pixel memory): the
            // canvas below writes into this same buffer while reading the snapshot, and a shared,
            // overlapping source/destination is undefined for a translated blit.
            using var snapshot = buffer.Copy ();
            if (snapshot is null)
                return false;

            using (var canvas = new SKCanvas (buffer)) {
                canvas.Save ();
                canvas.ClipRect (contentRect.ToSKRect ());
                canvas.DrawBitmap (snapshot, 0, shiftPx);
                canvas.Restore ();

                var exposed = shiftPx > 0
                    ? new Rectangle (contentRect.Left, contentRect.Top, contentRect.Width, shiftPx)
                    : new Rectangle (contentRect.Left, contentRect.Bottom + shiftPx, contentRect.Width, -shiftPx);

                canvas.Save ();
                canvas.ClipRect (exposed.ToSKRect ());
                canvas.Clear (GetEffectiveBackgroundColor ());

                var info = new SKImageInfo (buffer.Width, buffer.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
                RenderManager.Render (this, new PaintEventArgs (info, canvas, Scaling));
                canvas.Restore ();

                canvas.Flush ();
            }

            // Invalidate (), not a quieter window-only nudge: a control's own IsDirty is what makes
            // Controls.AnyNeedsPaint () bubble NeedsPaint up through every ancestor, which is what makes
            // Control.PaintChildren actually walk down and re-blit this control into them at all -- skip
            // it and this correctly-updated buffer would simply never be recomposited. The
            // _invalidatingForFastScroll flag tells OnInvalidated that THIS particular Invalidate () must
            // not force a real repaint (see its own comment for why a plain one-shot bool can't do this
            // safely across several chained calls).
            _invalidatingForFastScroll = true;
            try { Invalidate (); } finally { _invalidatingForFastScroll = false; }
            return true;
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            if (_skipNextRepaint) {
                _skipNextRepaint = false;
                return;
            }

            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the SelectedIndexChanged event.
        /// </summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e)
        {
            SelectedIndexChanged?.Invoke (this, e);
            // WinForms ListControl raises SelectedValueChanged whenever the selection changes;
            // SelectedValue is derived from SelectedIndex, so it changes at the same moment.
            OnSelectedValueChanged (e);
        }

        /// <summary>Raises the SelectedValueChanged event.</summary>
        protected override void OnSelectedValueChanged (EventArgs e) => base.OnSelectedValueChanged (e);

        /// <summary>
        /// Gets the scaled height each item occupies.
        /// </summary>
        public int ScaledItemHeight => LogicalToDeviceUnits (ItemHeight);

        /// <summary>
        /// Gets or sets a value indicating if the vertical scrollbar is always shown.
        /// </summary>
        public bool ScrollbarAlwaysVisible {
            get => scrollbar_always_visible;
            set {
                if (scrollbar_always_visible != value) {
                    scrollbar_always_visible = value;
                    UpdateVerticalScrollBar ();
                }
            }
        }
        /// <summary>
        /// Gets or sets the index of the currently selected item.  If there are multiple selected items, the first item's index will be returned.
        /// </summary>
        public override int SelectedIndex {
            get => Items.SelectedIndex;
            set {
                if (SelectionMode == SelectionMode.None)
                    throw new ArgumentException ("Cannot call this method if SelectionMode is SelectionMode.None");

                if (Items.SelectedIndex != value || Items.SelectedIndexes.Count > 1) {
                    Items.SelectedIndex = value;

                    // Inside a ChangeSelection batch the announcement is that batch's job, so an input
                    // handler that lands here reports its change once rather than twice.
                    if (selection_batch == 0) {
                        // Move the bound source's current item with the selection, so a BindingSource
                        // driving a detail view follows what the user picked here.
                        source_tracker.OnSelectionChanged (value);

                        OnSelectedIndexChanged (EventArgs.Empty);
                    }

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Raised when the value of the SelectedIndex property changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>
        /// Gets or sets the currently selected item, if any.  If there are multiple selected items, the first selected item will be returned.
        /// </summary>
        public virtual object? SelectedItem {
            get => Items.SelectedItem;
            set {
                // Through the PUBLIC SelectedIndex, which raises. This used to assign the collection's
                // INTERNAL setter, so `cbo.SelectedItem = customer` -- the commonest way LOB code
                // selects programmatically -- moved the selection silently and never ran the
                // SelectedIndexChanged handler that loads the detail panel (LST-03, P0).
                if (value is null) {
                    if (SelectionMode != SelectionMode.None)
                        SelectedIndex = -1;

                    return;
                }

                var index = Items.IndexOf (value);

                // An item that is not in the list leaves the selection alone, as upstream does -- a
                // designer sets SelectedValue before the items are populated, and a bound editor writes
                // back a value the current filter excluded. Throwing turned both into a crash inside
                // InitializeComponent.
                if (index != -1 && SelectionMode != SelectionMode.None)
                    SelectedIndex = index;
            }
        }

        /// <summary>
        /// Gets all currently selected items.
        /// </summary>
        /// <remarks>Typed as the nested <see cref="SelectedObjectCollection"/> for the same reason
        /// <see cref="Items"/> is typed as <see cref="ObjectCollection"/>: that is the name WinForms code
        /// uses for it. A snapshot, as it was before -- adding to the result does not select anything.</remarks>
        public SelectedObjectCollection SelectedItems => new SelectedObjectCollection (this);

        /// <summary>
        /// Gets or set the selection mode of the ListBox.
        /// </summary>
        public SelectionMode SelectionMode {
            get => selection_mode;
            set {
                if (!EnumCompat.IsDefined (value))
                    throw new InvalidEnumArgumentException ($"Enum argument value '{value}' is not valid for SelectionMode");

                if (selection_mode == value)
                    return;

                selection_mode = value;

                // Both of these drop items from the selection -- None clears it, One collapses a
                // multi-selection to its first item -- and both did so silently (LST-04).
                ChangeSelection (() => {
                    if (selection_mode == SelectionMode.None)
                        Items.SelectedIndex = -1;
                    else if (selection_mode == SelectionMode.One)
                        Items.SelectedIndex = Items.SelectedIndex;  // Yes this does something  ;)
                });
            }
        }

        // By default, we select the item in MouseDown. However, when used in a ComboBox,
        // we need to select the item in MouseUp, or else the popup will close and the MouseUp
        // event will leak to the control/form beneath the popup.
        internal bool SelectItemOnMouseUp { get; set; }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore (x, y, width, IntegralHeightFor (height), specified);

            UpdateVerticalScrollBar ();
        }

        // IntegralHeight (W6 mechanisms): the requested height less the non-client band, snapped down
        // to whole items, plus the band again -- never below one item, so a list cannot vanish. Left
        // alone under variable owner draw (no single item height) and when docked to fill an edge
        // (the container's height is not this control's to shorten).
        private int IntegralHeightFor (int height)
        {
            if (!integral_height || draw_mode == DrawMode.OwnerDrawVariable || Dock is DockStyle.Fill or DockStyle.Left or DockStyle.Right)
                return height;

            var item = Math.Max (1, ItemHeight);
            var chrome = Math.Max (0, Height - DeviceToLogicalUnits (ClientRectangle.Height));
            var rows = Math.Max (1, (height - chrome) / item);

            return rows * item + chrome;
        }

        /// <summary>
        /// Gets or sets whether the item currently containing the mouse pointer should be highlighted.
        /// </summary>
        public bool ShowHover { get; set; }

        /// <summary>Gets or sets the selected value using ValueMember reflection.</summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "DataSource item types require runtime reflection — same as WinForms.")]
        public override object? SelectedValue {
            get {
                var list = DataSourceBinding.AsList (DataSource);

                if (SelectedIndex < 0 || list is null || SelectedIndex >= list.Count)
                    return SelectedItem;

                var item = list[SelectedIndex];

                if (string.IsNullOrEmpty (ValueMember))
                    return item;

                return DataSourceBinding.MemberValue (item, ValueMember);
            }
            set {
                var list = DataSourceBinding.AsList (DataSource);

                if (list is null || value == null) {
                    SelectedItem = value;
                    return;
                }

                for (int i = 0; i < list.Count; i++) {
                    var item = list[i];
                    var item_value = string.IsNullOrEmpty (ValueMember)
                        ? item
                        : DataSourceBinding.MemberValue (item, ValueMember);

                    if (Equals (item_value, value)) {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        /// <summary>Gets or sets whether items are sorted alphabetically.</summary>
        /// <remarks>
        /// Read by nothing before, so a sorted list box came out in insertion order. Sorting happens on
        /// the item collection itself rather than at paint, because <see cref="SelectedIndex"/> and
        /// every index-based API have to agree with what is on screen.
        /// </remarks>
        public bool Sorted {
            get => sorted;
            set {
                if (sorted == value)
                    return;

                sorted = value;

                if (sorted)
                    SortItems ();
            }
        }

        private bool sorted;

        // Ordinal by display text, which is what ListBox.Sorted means: the strings the user reads, not
        // the objects behind them. Selection is preserved by value, since the index it used to sit at
        // no longer means the same row.
        internal void SortItems ()
        {
            if (Items.Count < 2)
                return;

            var selected = SelectedItem;
            var ordered = Items.Cast<object> ()
                .OrderBy (GetItemText, System.StringComparer.CurrentCulture)
                .ToList ();

            Items.Clear ();

            foreach (var item in ordered)
                Items.Add (item);

            if (selected is not null)
                SelectedItem = selected;
        }

        /// <summary>Gets or sets how the items are drawn: by the control, or by the application.</summary>
        /// <remarks>
        /// Read as of W6 mechanisms. In either owner-draw mode the renderer raises <see cref="DrawItem"/>
        /// for every visible item instead of painting it; <see cref="DrawMode.OwnerDrawVariable"/> also
        /// asks <see cref="MeasureItem"/> for each item's height and lays the rows out from the answers.
        /// </remarks>
        public virtual DrawMode DrawMode {
            get => draw_mode;
            set {
                if (draw_mode == value)
                    return;

                draw_mode = value;
                variable_heights = null;
                Invalidate ();
            }
        }

        private DrawMode draw_mode = DrawMode.Normal;

        // OwnerDrawVariable (W6 mechanisms): one measured height per item, in LOGICAL pixels as the
        // handler answers them, taken from MeasureItem and refreshed when the item count changes.
        // Logical, not device: the list can be measured before it is parented, when its scale is not
        // yet the window's, and a device-space cache would then be wrong by that factor. The canvas
        // handed to the event is a scratch surface: upstream gives a Graphics for measuring text
        // against, nothing more.
        private List<int>? variable_heights;
        private static readonly SkiaSharp.SKCanvas MeasureCanvas = new (new SkiaSharp.SKBitmap (1, 1));

        /// <summary>The height of one item in device pixels: the fixed height, or the measured one in
        /// <see cref="DrawMode.OwnerDrawVariable"/>.</summary>
        internal int ItemHeightDeviceAt (int index)
        {
            // With nobody to ask, the answer is the fixed height and nothing is cached -- so a handler
            // attached after the items were added is still asked, which the count-keyed cache alone
            // would have missed.
            if (draw_mode != DrawMode.OwnerDrawVariable || measure_item is null)
                return ScaledItemHeight;

            EnsureVariableHeights ();

            return index >= 0 && index < variable_heights!.Count ? LogicalToDeviceUnits (variable_heights[index]) : ScaledItemHeight;
        }

        private void EnsureVariableHeights ()
        {
            if (variable_heights is { } heights && heights.Count == Items.Count)
                return;

            variable_heights = new List<int> (Items.Count);

            for (var i = 0; i < Items.Count; i++) {
                var e = new MeasureItemEventArgs (MeasureCanvas, i) { ItemHeight = ItemHeight };
                OnMeasureItem (e);
                variable_heights.Add (Math.Max (1, e.ItemHeight));
            }
        }

        // The top of item[index] relative to item[top_index], in device pixels, for either mode.
        private int ItemOffsetDevice (int index)
        {
            if (draw_mode != DrawMode.OwnerDrawVariable)
                return (index - top_index) * ScaledItemHeight;

            var offset = 0;

            if (index >= top_index)
                for (var i = top_index; i < index; i++)
                    offset += ItemHeightDeviceAt (i);
            else
                for (var i = index; i < top_index; i++)
                    offset -= ItemHeightDeviceAt (i);

            return offset;
        }

        /// <summary>Re-measures the items: <see cref="MeasureItem"/> is asked again for every item.</summary>
        public void RefreshItems ()
        {
            variable_heights = null;
            Invalidate ();
        }

        /// <summary>Gets or sets whether the control height resizes to avoid showing partial items.</summary>
        /// <remarks>
        /// Real as of W6 mechanisms: the height is snapped down to a whole number of items (plus the
        /// non-client band) whenever the bounds are set, as upstream's list box does, so a designer
        /// height of 95 with 13-pixel items comes out at the height that shows seven whole rows. Not
        /// applied under <see cref="DrawMode.OwnerDrawVariable"/>, where there is no single item height,
        /// nor when the list is docked to fill an edge, where the container owns the height.
        /// </remarks>
        public bool IntegralHeight {
            get => integral_height;
            set {
                if (integral_height == value)
                    return;

                integral_height = value;

                // Straight to the core: SetBounds skips a call whose bounds have not changed, and
                // the whole point here is to re-snap the height that is already set.
                if (value)
                    SetBoundsCore (Left, Top, Width, Height, BoundsSpecified.Height);
            }
        }

        /// <summary>Gets or sets whether the selection is hidden when the control loses focus. Stub in Majorsilence.Forms.</summary>
        public bool HideSelection { get; set; }

        /// <summary>Whether a selected item should be drawn selected right now.</summary>
        /// <remarks>
        /// <see cref="HideSelection"/> is NOT an upstream ListBox member -- upstream has it on
        /// <c>ListView</c>, <c>TreeView</c> and <c>TextBoxBase</c> only -- so there is no upstream
        /// default to match. It defaulted to <c>true</c> here while nothing read it, which meant the
        /// property described behaviour the control did not have; honouring that default would have
        /// taken the highlight off every unfocused list in existence. Default <c>false</c> now, so
        /// wiring it changes nothing until an application asks for it, and the property finally means
        /// what it says. Recorded as a decision, not parity.
        /// </remarks>
        internal bool ShowsSelection => Focused || !HideSelection;

        /// <summary>Gets or sets the horizontal extent to enable horizontal scrolling.</summary>
        /// <remarks>Logical pixels. With <see cref="HorizontalScrollbar"/> on, an extent wider than the
        /// list shows the scrollbar; 0 lets a <see cref="DrawMode.Normal"/> list measure its own items,
        /// as upstream does (W6 mechanisms).</remarks>
        public int HorizontalExtent {
            get => horizontal_extent;
            set {
                if (horizontal_extent == value)
                    return;

                horizontal_extent = Math.Max (0, value);
                UpdateVerticalScrollBar ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets whether tab characters in item text are expanded to tab stops.</summary>
        /// <remarks>Real as of W6 mechanisms: the renderer lays a tabbed item out across the stops --
        /// every eight average characters, or <see cref="CustomTabOffsets"/> when
        /// <see cref="UseCustomTabOffsets"/> asks for them. Off, a tab is drawn as the font draws it.</remarks>
        public bool UseTabStops {
            get => use_tab_stops;
            set {
                if (use_tab_stops == value)
                    return;

                use_tab_stops = value;
                Invalidate ();
            }
        }

        /// <summary>Gets or sets whether the ListBox always shows a scroll bar.</summary>
        /// <remarks>
        /// The WinForms name for <see cref="ScrollbarAlwaysVisible"/>, and until now a second store
        /// beside it: the scrollbar logic read the other one, so setting the property WinForms code
        /// actually writes did nothing (<c>RC-6</c> — a private twin kept alongside the real member).
        /// They are one value now, so which name a caller uses cannot change the answer.
        /// </remarks>
        public bool ScrollAlwaysVisible {
            get => ScrollbarAlwaysVisible;
            set => ScrollbarAlwaysVisible = value;
        }

        /// <summary>Gets or sets whether a horizontal scrollbar is shown when the items are wider than the list.</summary>
        /// <remarks>Real as of W6 mechanisms: the bar appears when <see cref="HorizontalExtent"/> (or the
        /// measured items) is wider than the list, and scrolling it slides the items sideways.</remarks>
        public bool HorizontalScrollbar {
            get => horizontal_scrollbar;
            set {
                if (horizontal_scrollbar == value)
                    return;

                horizontal_scrollbar = value;
                UpdateVerticalScrollBar ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets whether the ListBox displays items in multiple columns.</summary>
        /// <remarks>Real as of W6 mechanisms: items run down each column and the columns across, the
        /// list scrolls sideways by whole columns through a horizontal scrollbar, and Left/Right move
        /// between columns. <see cref="ColumnWidth"/> sets the column; 0 measures the widest item.</remarks>
        public bool MultiColumn {
            get => multi_column;
            set {
                if (multi_column == value)
                    return;

                multi_column = value;
                top_index = 0;
                h_offset = 0;
                _scrollOffsetPx = 0;
                UpdateVerticalScrollBar ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the width of each column in a multi-column ListBox, in logical pixels; 0 measures the items.</summary>
        public int ColumnWidth {
            get => column_width;
            set {
                if (column_width == value)
                    return;

                column_width = Math.Max (0, value);
                UpdateVerticalScrollBar ();
                Invalidate ();
            }
        }

        /// <summary>Gets or sets the index of the first visible item.</summary>
        /// <remarks>A <see cref="MultiColumn"/> list scrolls by columns, so the index is snapped to the
        /// first item of its column.</remarks>
        public int TopIndex {
            get => top_index;
            set {
                var clamped = Math.Max (0, Math.Min (value, Items.Count - 1));
                top_index = multi_column ? clamped / RowsPerColumn * RowsPerColumn : clamped;
                _scrollOffsetPx = 0;

                if (multi_column)
                    hscrollbar.Value = Math.Max (hscrollbar.Minimum, Math.Min (top_index / RowsPerColumn, hscrollbar.EffectiveMaximum));

                Invalidate ();
            }
        }

        /// <summary>Deselects all items in the ListBox.</summary>
        public void ClearSelected () => ChangeSelection (() => Items.SelectedIndexes.Clear ());

        /// <summary>Selects or deselects the item at the specified index.</summary>
        /// <remarks>Announces the change, and rejects what upstream rejects: an out-of-range index and
        /// a <see cref="SelectionMode.None"/> list both threw there and were swallowed here, which
        /// turned a caller's off-by-one into a selection that silently did not happen
        /// (<c>LST-04</c>).</remarks>
        public void SetSelected (int index, bool value)
        {
            if (SelectionMode == SelectionMode.None)
                throw new InvalidOperationException (
                    "Cannot call SetSelected when SelectionMode is SelectionMode.None.");

            if (index < 0 || index >= Items.Count)
                throw new ArgumentOutOfRangeException (nameof (index));

            ChangeSelection (() => {
                if (value)
                    Items.AddSelectedIndex (index, single: SelectionMode == SelectionMode.One);
                else
                    Items.RemoveSelectedIndex (index);
            });
        }

        /// <summary>Returns whether the item at the specified index is selected.</summary>
        public bool GetSelected (int index) => Items.SelectedIndexes.Contains (index);

        /// <summary>
        /// Applies a selection change and announces it exactly once, if it changed anything.
        /// </summary>
        /// <remarks>
        /// The one place a multi-selection change is reported (finding <c>LST-04</c>, P0). Every path
        /// except the <see cref="SelectedIndex"/> setter -- <see cref="SetSelected"/>,
        /// <see cref="ClearSelected"/>, Ctrl-click and Space toggles, Shift+arrow extension -- mutated
        /// the collection's internal index list and raised nothing, so in a multi-select list the
        /// "N items selected" label, the enabled state of Delete/Move, and any
        /// <c>SelectedItems</c>-driven detail view never updated from user input.
        /// <para>
        /// Compared by snapshot rather than by trusting the caller: several of these paths are no-ops in
        /// practice (re-selecting what is already selected), and WinForms raises only on a real change.
        /// </para>
        /// </remarks>
        private int selection_batch;

        internal void ChangeSelection (Action mutate)
        {
            var before = Items.SelectedIndexes.ToList ();

            // Depth, not a flag: these wrap whole input handlers, and a handler branch that assigns
            // SelectedIndex would otherwise report the change twice -- once from that setter and once
            // from here. Exactly the double-report W5.6 hit when ListViewItem.Selected became the
            // choke point while ListView.SelectedItem still raised on its own.
            selection_batch++;

            try {
                mutate ();
            } finally {
                selection_batch--;
            }

            var after = Items.SelectedIndexes;
            var changed = before.Count != after.Count || before.Any (i => !after.Contains (i));

            if (!changed) {
                Invalidate ();
                return;
            }

            // The bound source's current item follows the selection, as it does from the
            // SelectedIndex setter -- otherwise a BindingSource driving a detail view tracks
            // single-clicks but not Ctrl-clicks.
            source_tracker.OnSelectionChanged (Items.SelectedIndex);

            OnSelectedIndexChanged (EventArgs.Empty);
            Invalidate ();
        }

        /// <summary>Returns the collection of indices of all currently selected items.</summary>
        public SelectedIndexCollection SelectedIndices => new (Items.SelectedIndexes);

        /// <summary>Read-only collection of a ListBox's selected item indices. Mirrors WinForms ListBox.SelectedIndexCollection.</summary>
        public class SelectedIndexCollection : System.Collections.ObjectModel.ReadOnlyCollection<int>
        {
            internal SelectedIndexCollection (IList<int> indexes) : base (indexes) { }
        }

        /// <summary>Finds the first item that exactly matches the specified string, starting at the given index. This search is case-insensitive and wraps around.</summary>
        public int FindStringExact (string s, int startIndex = -1)
        {
            if (s is null || Items.Count == 0)
                return -1;

            if (startIndex < -1 || startIndex >= Items.Count)
                throw new ArgumentOutOfRangeException (nameof (startIndex));

            // We look for matches AFTER the start index, wrapping around the collection.
            startIndex = (startIndex == Items.Count - 1) ? 0 : startIndex + 1;
            var current = startIndex;

            while (true) {
                var text = Items[current]?.ToString () ?? string.Empty;

                if (string.Equals (text, s, StringComparison.CurrentCultureIgnoreCase))
                    return current;

                current++;

                if (current == Items.Count)
                    current = 0;

                if (current == startIndex)
                    return -1;
            }
        }

        /// <summary>Raised in <see cref="DrawMode.OwnerDrawVariable"/> to ask the height of an item.</summary>
        /// <remarks>Real as of W6 mechanisms: raised once per item when the rows are laid out, and
        /// again after <see cref="RefreshItems"/> or a change in the item count. The answer's
        /// <see cref="MeasureItemEventArgs.ItemHeight"/> is in logical pixels, like <see cref="ItemHeight"/>.</remarks>
        public event MeasureItemEventHandler? MeasureItem {
            add {
                measure_item += value;
                // Heights measured before this handler existed are not its answers.
                variable_heights = null;
            }
            remove => measure_item -= value;
        }

        private MeasureItemEventHandler? measure_item;

        /// <summary>Raises the <see cref="MeasureItem"/> event.</summary>
        protected virtual void OnMeasureItem (MeasureItemEventArgs e) => measure_item?.Invoke (this, e);

        // The renderer's door to DrawItem (W6 mechanisms): the item's device rectangle and state.
        internal void RaiseDrawItem (int index, Rectangle bounds, DrawItemState state, PaintEventArgs e)
        {
            var back = (state & DrawItemState.Selected) != 0 ? SystemColors.Highlight : BackColor;
            var fore = (state & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : ForeColor;

            OnDrawItem (new DrawItemEventArgs (e.Graphics, Font, bounds, index, state, fore, back));
        }

        /// <summary>Raised when an item needs to be drawn (OwnerDraw).</summary>
        /// <remarks>
        /// Real now, and typed with WinForms' own <see cref="DrawItemEventHandler"/> -- it was
        /// <c>EventHandler&lt;DrawItemEventArgs&gt;</c> with empty accessors, which meant a handler
        /// attached the WinForms way (<c>+= new DrawItemEventHandler(...)</c>) failed to compile at all,
        /// and one attached some other way was silently dropped. Raised by the renderer as of W6
        /// mechanisms for every visible item while <see cref="DrawMode"/> is an owner-draw mode, with the
        /// item's device rectangle and its selected / focused / disabled / hot state.
        /// </remarks>
        public event DrawItemEventHandler? DrawItem;

        /// <summary>Raises the <see cref="DrawItem"/> event.</summary>
        protected virtual void OnDrawItem (DrawItemEventArgs e) => DrawItem?.Invoke (this, e);

        /// <summary>Prevents the control from drawing until EndUpdate is called.</summary>
        public new void BeginUpdate () => SuspendLayout ();

        /// <summary>Resumes drawing the control after BeginUpdate.</summary>
        public new void EndUpdate () { ResumeLayout (false); Invalidate (); }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        // Update the scroll bars to match the current items and layout (W6 mechanisms: the horizontal
        // bar, and the multi-column shape, are new; the name is kept for its callers).
        private void UpdateVerticalScrollBar ()
        {
            if (multi_column) {
                UpdateMultiColumnScrollBar ();
                return;
            }

            UpdateHorizontalScrollBar ();

            if (Items.Count == 0)
                vscrollbar.Visible = ScrollbarAlwaysVisible;

            if (NeededHeightForItems > ItemsArea.Height) {
                vscrollbar.Visible = true;
                // Maximum is the *conceptual last item index* (see ScrollBar.EffectiveMaximum), not the
                // last valid top_index -- with LargeChange set below to the page size, EffectiveMaximum
                // works out to Items.Count - VisibleItemCount, which is what top_index/ScrollByDevicePixels
                // actually clamp against. Setting Maximum to that directly left the thumb, which is
                // positioned from EffectiveMaximum, reaching the end of the track a whole page early.
                vscrollbar.Maximum = Math.Max (0, Items.Count - 1);
                vscrollbar.LargeChange = Math.Max (0, VisibleItemCount);
            } else {
                vscrollbar.Visible = ScrollbarAlwaysVisible;
                _scrollOffsetPx = 0;
            }
        }

        // A single-column list with HorizontalScrollbar: the bar spans the extent in device pixels.
        private void UpdateHorizontalScrollBar ()
        {
            var visible_width = ClientRectangle.Width - (vscrollbar.Visible ? vscrollbar.ScaledWidth : 0);
            var extent = horizontal_scrollbar ? ScaledHorizontalExtent : 0;
            var show = extent > visible_width && visible_width > 0;

            hscrollbar.Visible = show;

            if (!show) {
                h_offset = 0;
                return;
            }

            hscrollbar.Maximum = extent - 1;
            hscrollbar.LargeChange = visible_width;
            hscrollbar.SmallChange = LogicalToDeviceUnits (8);
        }

        // A MultiColumn list: no vertical bar; the horizontal bar counts columns.
        private void UpdateMultiColumnScrollBar ()
        {
            vscrollbar.Visible = false;
            _scrollOffsetPx = 0;

            var columns = ColumnCount;
            var visible = VisibleColumnCount;

            hscrollbar.Visible = columns > visible || (ScrollbarAlwaysVisible && Items.Count > 0);
            hscrollbar.Maximum = Math.Max (0, columns - 1);
            hscrollbar.LargeChange = Math.Max (1, visible);
            hscrollbar.SmallChange = 1;

            var rows = RowsPerColumn;
            top_index = Math.Min (top_index / rows, Math.Max (0, columns - visible)) * rows;
        }

        private void HorizontalScrollBar_ValueChanged (object? sender, EventArgs e)
        {
            if (multi_column)
                top_index = Math.Max (0, hscrollbar.Value) * RowsPerColumn;
            else
                h_offset = Math.Max (0, hscrollbar.Value);

            Invalidate ();
        }

        // Handle changes to the vertical scroll bar.
        private void VerticalScrollBar_ValueChanged (object? sender, EventArgs e)
        {
            top_index = Math.Max (vscrollbar.Value, 0);

            // A thumb drag, wheel notch or programmatic scroll snaps to a whole row; only a touch
            // drag (which sets the value through ScrollByDevicePixels) keeps a sub-row offset.
            if (!_settingScrollbarFromGesture)
                _scrollOffsetPx = 0;

            Invalidate ();
        }

        /// <summary>
        /// The number of full items that can be shown at a time.
        /// </summary>
        public int VisibleItemCount {
            get {
                if (multi_column)
                    return RowsPerColumn * VisibleColumnCount;

                if (draw_mode != DrawMode.OwnerDrawVariable)
                    return ItemsArea.Height / ScaledItemHeight;

                // Variable heights: count the whole rows that fit from the top row down.
                var remaining = ItemsArea.Height;
                var count = 0;

                for (var i = top_index; i < Items.Count; i++) {
                    remaining -= ItemHeightDeviceAt (i);

                    if (remaining < 0)
                        break;

                    count++;
                }

                return count;
            }
        }
    }
}
