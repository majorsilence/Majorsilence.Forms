using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ListView control.
    /// Note the ListView control has not been fully developed, and probably does not contain enough functionality to be useful yet.
    /// </summary>
    public partial class ListView : Control
    {
        /// <summary>
        /// Initializes a new instance of the ListView class.
        /// </summary>
        public ListView ()
        {
            Items = new ListViewItemCollection (this);
            Columns = new ColumnHeaderCollection (this);

            // A real scrollbar, as ListBox and TreeView have (LST-19). Without one, a list taller than
            // its control was simply truncated -- the remaining items laid out past the bottom edge with
            // no way to reach them, and EnsureVisible was an Invalidate.
            vscrollbar = new VerticalScrollBar {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = false,
                Dock = DockStyle.Right,
            };

            vscrollbar.ValueChanged += (_, _) => {
                top_index = Math.Max (0, vscrollbar.Value);
                Invalidate ();
            };

            Controls.AddImplicitControl (vscrollbar);
        }

        private readonly VerticalScrollBar vscrollbar;
        private int top_index;

        /// <summary>The index of the first item shown, as moved by the scrollbar.</summary>
        internal int TopIndex => top_index;

        // One "line" is a row in the row views and a whole tile row in the tile views, so the
        // scrollbar counts what the layout actually steps by.
        private int ScaledLineHeight
            => IsRowView ? ScaledRowHeight : ScaledTileSize + LogicalToDeviceUnits (6);

        internal int ItemsPerLine {
            get {
                if (IsRowView)
                    return 1;

                var stride = ScaledTileSize + LogicalToDeviceUnits (6);

                return Math.Max (1, (ItemArea.Width + LogicalToDeviceUnits (6)) / Math.Max (1, stride));
            }
        }

        private int LineCount => (Items.Count + ItemsPerLine - 1) / Math.Max (1, ItemsPerLine);

        /// <summary>The number of whole lines that fit in the item area.</summary>
        internal int VisibleLineCount => Math.Max (1, ItemArea.Height / Math.Max (1, ScaledLineHeight));

        internal void UpdateVerticalScrollBar ()
        {
            var lines = LineCount;
            var visible = VisibleLineCount;

            if (!Scrollable || lines <= visible) {
                vscrollbar.Visible = false;

                if (top_index != 0) {
                    top_index = 0;
                    vscrollbar.Value = 0;
                }

                return;
            }

            vscrollbar.Visible = true;
            // Maximum is the *conceptual last item index* (see ScrollBar.EffectiveMaximum), not the
            // last valid top_index -- with LargeChange set below to the page size, EffectiveMaximum
            // works out to lines - visible, which is what top_index actually clamps against. Setting
            // Maximum to that directly left the thumb, which is positioned from EffectiveMaximum,
            // reaching the end of the track a whole page early.
            vscrollbar.Maximum = Math.Max (0, lines - 1);
            vscrollbar.LargeChange = Math.Max (1, visible);

            if (top_index > vscrollbar.EffectiveMaximum) {
                top_index = vscrollbar.EffectiveMaximum;
                vscrollbar.Value = top_index;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (vscrollbar.Visible)
                vscrollbar.RaiseMouseWheel (e);
        }

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (3);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (450, 450);

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.BackgroundColor = Theme.ControlLowColor);

        /// <summary>
        /// Raised when a list view item is double-clicked.
        /// </summary>
        public event EventHandler<EventArgs<ListViewItem>>? ItemDoubleClicked;

        /// <summary>
        /// Gets the collection of items contained by this ListView.
        /// </summary>
        public ListViewItemCollection Items { get; }

        // ── Metrics ──────────────────────────────────────────────────────────────────────────────
        //
        // All of these, and every Bounds this control hands out, are DEVICE pixels -- the same
        // convention ListBox uses, and what the renderer draws with. The mouse arrives in LOGICAL
        // units, so it is converted on the way in rather than the bounds being converted on the way
        // out (see ToDevice; finding LST-20, which this layout would otherwise have inherited).

        /// <summary>The height of one row in the row-shaped views, in device pixels.</summary>
        internal int ScaledRowHeight => LogicalToDeviceUnits (RowHeight);

        // Measured from the font the renderer will actually draw with, so a themed or scaled font
        // gets rows that fit its text rather than a constant that used to.
        private int RowHeight
            => Math.Max ((int)TextMeasurer.MeasureText ("The quick brown Fox", this).Height + 4,
                         Theme.ItemFontSize + 6);

        /// <summary>The height of the Details header band, in device pixels; zero when it is hidden.</summary>
        internal int ScaledHeaderHeight
            => View == View.Details && HeaderStyle != ColumnHeaderStyle.None
                ? LogicalToDeviceUnits (RowHeight + 2)
                : 0;

        /// <summary>The width of a large-icon tile, in device pixels.</summary>
        internal int ScaledTileSize => LogicalToDeviceUnits (70);

        /// <summary>The width the check box column takes when <see cref="CheckBoxes"/> is set.</summary>
        internal int ScaledCheckWidth => CheckBoxes ? LogicalToDeviceUnits (18) : 0;

        // Whether this view lays items out as one row each (Details, List, SmallIcon) rather than as
        // a grid of tiles (LargeIcon, Tile).
        internal bool IsRowView => View is View.Details or View.List or View.SmallIcon;

        /// <summary>The width of a column, resolving the -1 (fit content) and -2 (fit header) sentinels.</summary>
        internal int ScaledColumnWidth (ColumnHeader column)
        {
            var width = column.Width;

            // -1 and -2 are WinForms' autosize sentinels, and they were stored verbatim -- so a
            // designer's `column.Width = -2` produced a column two pixels WIDE, in the negative
            // direction (LST-01).
            if (width == -1)
                width = MeasuredColumnWidth (column, includeItems: true);
            else if (width == -2)
                width = MeasuredColumnWidth (column, includeItems: false);

            return LogicalToDeviceUnits (Math.Max (0, width));
        }

        private int MeasuredColumnWidth (ColumnHeader column, bool includeItems)
        {
            var padding = 12;
            var width = (int)TextMeasurer.MeasureText (column.Text ?? string.Empty, this).Width + padding;

            if (!includeItems)
                return width;

            var index = Columns.IndexOf (column);

            foreach (var item in Items) {
                var text = index == 0
                    ? item.Text
                    : index < item.SubItems.Count ? item.SubItems[index].Text : string.Empty;

                width = Math.Max (width, (int)TextMeasurer.MeasureText (text ?? string.Empty, this).Width + padding);
            }

            return width;
        }

        // The area items are laid out in: the padded client rectangle, less the space the scrollbar
        // occupies and less the header band.
        internal Rectangle ItemArea {
            get {
                var bounds = PaddedClientRectangle;
                var width = bounds.Width - (vscrollbar.Visible ? vscrollbar.ScaledWidth : 0);

                return new Rectangle (bounds.Left, bounds.Top + ScaledHeaderHeight,
                    Math.Max (0, width), Math.Max (0, bounds.Height - ScaledHeaderHeight));
            }
        }

        // Lays out the ListViewItems for the current View. Called from OnPaint and from anything that
        // needs bounds before the first paint (hit-testing, EnsureVisible, SubItem.Bounds).
        internal void LayoutItems ()
        {
            var bounds = ItemArea;

            if (IsRowView)
                LayoutRows (bounds);
            else
                LayoutTiles (bounds);
        }

        private void LayoutRows (Rectangle bounds)
        {
            var row_height = ScaledRowHeight;
            var y = bounds.Top - top_index * row_height;

            foreach (var item in Items) {
                item.SetBounds (bounds.Left, y, bounds.Width, row_height);
                LayoutSubItems (item);

                y += row_height;
            }
        }

        // Per-cell rectangles for Details, so a DrawSubItem handler -- and any code reading
        // SubItems[i].Bounds -- gets a real answer instead of Rectangle.Empty (LST-01).
        private void LayoutSubItems (ListViewItem item)
        {
            if (View != View.Details)
                return;

            var x = item.Bounds.Left + ScaledCheckWidth;

            for (var i = 0; i < Columns.Count; i++) {
                var width = ScaledColumnWidth (Columns[i]);

                if (i < item.SubItems.Count)
                    item.SubItems[i].Bounds = new Rectangle (x, item.Bounds.Top, width, item.Bounds.Height);

                x += width;
            }
        }

        private void LayoutTiles (Rectangle bounds)
        {
            var item_size = ScaledTileSize;
            var item_margin = LogicalToDeviceUnits (6);

            var x = bounds.Left;
            var y = bounds.Top - top_index * (item_size + item_margin);

            foreach (var item in Items) {
                item.SetBounds (x, y, item_size, item_size);
                x += item_size + item_margin;

                // Against the RIGHT EDGE, not the width: laid out from bounds.Left, comparing to
                // Width wrapped a padded or scrolled list a column early.
                if (x + item_size > bounds.Right) {
                    x = bounds.Left;
                    y += item_size + item_margin;
                }
            }
        }

        // Mouse coordinates are logical; item bounds are device. Same conversion, and the same
        // reason, as ListBox.GetIndexAtLocation.
        private Point ToDevice (Point location)
            => new Point (LogicalToDeviceUnits (location.X), LogicalToDeviceUnits (location.Y));

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            base.OnMouseClick (e);

            LayoutItems ();

            var location = ToDevice (e.Location);

            // A click in the header band is a column click, not an item click (LST-18).
            if (ScaledHeaderHeight > 0 && location.Y < PaddedClientRectangle.Top + ScaledHeaderHeight) {
                var column = ColumnIndexAt (location.X);

                if (column >= 0 && HeaderStyle == ColumnHeaderStyle.Clickable)
                    OnColumnClick (new ColumnClickEventArgs (column));

                return;
            }

            var clicked_item = Items.FirstOrDefault (tp => tp.Bounds.Contains (location));

            if (clicked_item is null)
                return;

            // The check box is its own hit target: clicking it toggles and does not re-select.
            if (CheckBoxes && location.X < clicked_item.Bounds.Left + ScaledCheckWidth) {
                clicked_item.Checked = !clicked_item.Checked;
                return;
            }

            FocusedItem = clicked_item;

            // Ctrl adds to the selection, Shift extends from the focused item -- both only when
            // MultiSelect allows it, which is what MultiSelect = false is for (LST-17).
            if (MultiSelect && (ModifierKeys & Keys.Control) == Keys.Control)
                clicked_item.Selected = !clicked_item.Selected;
            else if (MultiSelect && (ModifierKeys & Keys.Shift) == Keys.Shift && anchor_index >= 0)
                SelectRange (anchor_index, Items.IndexOf (clicked_item));
            else {
                SelectedItem = clicked_item;
                anchor_index = Items.IndexOf (clicked_item);
            }
        }

        private int anchor_index = -1;

        // Puts the given line at the top, clamped to what there is to scroll. TopItem's setter and
        // EnsureVisible both land here so the scrollbar and the layout cannot disagree.
        internal void ScrollToLine (int line)
        {
            UpdateVerticalScrollBar ();

            var target = Math.Max (0, Math.Min (line, Math.Max (0, LineCount - VisibleLineCount)));

            if (target == top_index)
                return;

            top_index = target;

            if (vscrollbar.Visible)
                vscrollbar.Value = target;

            Invalidate ();
        }

        private void SelectRange (int from, int to)
        {
            var start = Math.Min (from, to);
            var end = Math.Max (from, to);

            selection_batch++;

            try {
                for (var i = 0; i < Items.Count; i++)
                    Items[i].Selected = i >= start && i <= end;
            } finally {
                selection_batch--;
            }

            FlushSelectionBatch ();
        }

        /// <summary>The index of the column at the given device x-offset, or -1.</summary>
        internal int ColumnIndexAt (int x)
        {
            var offset = ItemArea.Left + ScaledCheckWidth;

            for (var i = 0; i < Columns.Count; i++) {
                var width = ScaledColumnWidth (Columns[i]);

                if (x >= offset && x < offset + width)
                    return i;

                offset += width;
            }

            return -1;
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            LayoutItems ();

            var clicked_item = Items.FirstOrDefault (tp => tp.Bounds.Contains (ToDevice (e.Location)));

            if (clicked_item != null) {
                ItemDoubleClicked?.Invoke (this, new EventArgs<ListViewItem> (clicked_item));

                // WinForms' own name for this, and the one migrated code subscribes: double-click (or
                // Enter) ACTIVATES an item. It was declared with discarding accessors (LST-18).
                OnItemActivate (EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            UpdateVerticalScrollBar ();
            LayoutItems ();

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets the currently selected item, if any. If there are multiple selected items, the first selected item will be returned.
        /// </summary>
        public ListViewItem? SelectedItem {
            get => Items.FirstOrDefault (i => i.Selected);
            set {
                var current_item = Items.FirstOrDefault (i => i.Selected);

                if (current_item == value)
                    return;

                // The per-item setters do the announcing now (LST-17), so this batches them: the
                // deselection and the selection each report ItemSelectionChanged as they happen -- a
                // handler tracking selection sees the item it must let go of before the one it gains --
                // and ONE SelectedIndexChanged follows, once the selection has settled. Raising both
                // here as well, as this used to, reported every change twice over.
                selection_batch++;

                try {
                    if (current_item != null)
                        current_item.Selected = false;

                    if (value != null)
                        value.Selected = true;
                } finally {
                    selection_batch--;
                }

                FlushSelectionBatch ();
            }
        }

        /// <summary>Gets or sets the view mode of the list view.</summary>
        public View View { get; set; } = View.LargeIcon;

        /// <summary>Gets or sets whether the entire row is highlighted when selected in Details view.</summary>
        public bool FullRowSelect { get; set; }

        /// <summary>Gets or sets whether grid lines appear between rows in Details view.</summary>
        public bool GridLines { get; set; }

        /// <summary>Gets or sets whether multiple items can be selected.</summary>
        public bool MultiSelect { get; set; } = true;

        /// <summary>Gets or sets whether check boxes are shown next to items.</summary>
        public bool CheckBoxes { get; set; }

        /// <summary>Gets or sets the ImageList for small images.</summary>
        public ImageList? SmallImageList { get; set; }

        /// <summary>Gets or sets the ImageList for large images.</summary>
        public ImageList? LargeImageList { get; set; }

        /// <summary>Gets or sets the ImageList for state images.</summary>
        public ImageList? StateImageList { get; set; }

        /// <summary>Gets or sets the sort order applied to the items' text.</summary>
        /// <remarks>Sorts on assignment, as upstream does (LST-12).</remarks>
        public SortOrder Sorting {
            get => sorting;
            set {
                if (sorting == value)
                    return;

                sorting = value;
                Sort ();
            }
        }

        private SortOrder sorting = SortOrder.None;

        /// <summary>Gets or sets whether items can be grouped. Stub in Majorsilence.Forms.</summary>
        public bool ShowGroups { get; set; } = true;

        /// <summary>Gets or sets whether labels are automatically arranged. Stub in Majorsilence.Forms.</summary>
        public bool AutoArrange { get; set; } = true;

        /// <summary>Gets or sets the style of column headers. Stub in Majorsilence.Forms.</summary>
        public ColumnHeaderStyle HeaderStyle { get; set; } = ColumnHeaderStyle.Clickable;

        /// <summary>Gets or sets the activation method for items. Stub in Majorsilence.Forms.</summary>
        public ItemActivation Activation { get; set; } = ItemActivation.Standard;

        /// <summary>Gets or sets whether the user can reorder columns. Stub in Majorsilence.Forms.</summary>
        public bool AllowColumnReorder { get; set; }

        /// <summary>Gets the collection of column headers for Details view.</summary>
        public ColumnHeaderCollection Columns { get; }

        /// <summary>Returns the bounding rectangle of the item at the specified index.</summary>
        public Rectangle GetItemRect (int index) =>
            index >= 0 && index < Items.Count ? Items[index].Bounds : Rectangle.Empty;

        /// <summary>Raised before an item's check state changes, and able to veto it.</summary>
        /// <remarks>Real as of W5.6 (LST-18): this and the six events below were declared
        /// <c>add { } remove { } }</c>, so <c>+=</c> compiled and silently dropped the delegate.</remarks>
        public event EventHandler<ItemCheckEventArgs>? ItemCheck;

        /// <summary>Raises the <see cref="ItemCheck"/> event.</summary>
        protected virtual void OnItemCheck (ItemCheckEventArgs e) => ItemCheck?.Invoke (this, e);

        // Called from ListViewItem.Checked's setter: asks first (the handler may rewrite NewValue),
        // then reports. Returns the state that should actually be stored.
        internal bool RaiseItemCheck (ListViewItem item, bool value)
        {
            var index = Items.IndexOf (item);
            var current = value ? CheckState.Unchecked : CheckState.Checked;
            var e = new ItemCheckEventArgs (index, value ? CheckState.Checked : CheckState.Unchecked, current);

            OnItemCheck (e);

            return e.NewValue == CheckState.Checked;
        }

        internal void RaiseItemChecked (ListViewItem item)
        {
            OnItemChecked (new ItemCheckedEventArgs (item));
            Invalidate ();
        }

        /// <summary>Raised when an item's selection state changes.</summary>
        /// <remarks>
        /// Real, and typed with WinForms' <see cref="ListViewItemSelectionChangedEventHandler"/>. It was a
        /// plain <c>EventHandler</c> with empty accessors, so it carried none of the information the event
        /// exists to carry -- which item, at which index, selected or deselected -- and dropped its
        /// handlers besides. Raised from the <see cref="SelectedItem"/> setter.
        /// </remarks>
        public event ListViewItemSelectionChangedEventHandler? ItemSelectionChanged;

        /// <summary>Raises the <see cref="ItemSelectionChanged"/> event.</summary>
        protected virtual void OnItemSelectionChanged (ListViewItemSelectionChangedEventArgs e)
            => ItemSelectionChanged?.Invoke (this, e);

        /// <summary>Raised when the selected indices change.</summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>Raises the SelectedIndexChanged event.</summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e) => SelectedIndexChanged?.Invoke (this, e);

        /// <summary>Raised when an item is activated (double-clicked or Enter pressed).</summary>
        public event EventHandler? ItemActivate;

        /// <summary>Raises the <see cref="ItemActivate"/> event.</summary>
        protected virtual void OnItemActivate (EventArgs e) => ItemActivate?.Invoke (this, e);

        /// <summary>Raised when the user begins dragging a list item.</summary>
        public event EventHandler<ItemDragEventArgs>? ItemDrag;

        /// <summary>Raises the <see cref="ItemDrag"/> event.</summary>
        protected virtual void OnItemDrag (ItemDragEventArgs e) => ItemDrag?.Invoke (this, e);

        /// <summary>Raised when an item is checked or unchecked.</summary>
        public event EventHandler<ItemCheckedEventArgs>? ItemChecked;

        /// <summary>Raises the <see cref="ItemChecked"/> event.</summary>
        protected virtual void OnItemChecked (ItemCheckedEventArgs e) => ItemChecked?.Invoke (this, e);

        /// <summary>Raised before a label is edited.</summary>
        public event EventHandler<LabelEditEventArgs>? BeforeLabelEdit;

        /// <summary>Raises the <see cref="BeforeLabelEdit"/> event.</summary>
        protected virtual void OnBeforeLabelEdit (LabelEditEventArgs e) => BeforeLabelEdit?.Invoke (this, e);

        /// <summary>Raised after a label is edited.</summary>
        public event EventHandler<LabelEditEventArgs>? AfterLabelEdit;

        /// <summary>Raises the <see cref="AfterLabelEdit"/> event.</summary>
        protected virtual void OnAfterLabelEdit (LabelEditEventArgs e) => AfterLabelEdit?.Invoke (this, e);

        /// <summary>Raised when a column header is clicked.</summary>
#pragma warning disable CA1711
        public event ColumnClickEventHandler? ColumnClick;
#pragma warning restore CA1711

        /// <summary>Raises the <see cref="ColumnClick"/> event.</summary>
        protected virtual void OnColumnClick (ColumnClickEventArgs e) => ColumnClick?.Invoke (this, e);

        /// <summary>Gets or sets whether the selected items are still highlighted when focus leaves. Stub in Majorsilence.Forms.</summary>
        public bool HideSelection { get; set; }

        /// <summary>Gets or sets whether labels can be edited in place. Stub in Majorsilence.Forms.</summary>
        public bool LabelEdit { get; set; }

        /// <summary>Gets or sets whether item labels wrap. Stub in Majorsilence.Forms.</summary>
        public bool LabelWrap { get; set; } = true;

        /// <summary>Gets or sets whether hover-selection is enabled. Stub in Majorsilence.Forms.</summary>
        public bool HoverSelection { get; set; }

        /// <summary>Gets or sets whether items can be scrolled. Stub in Majorsilence.Forms.</summary>
        public bool Scrollable { get; set; } = true;

        /// <summary>Gets or sets whether item tooltips are shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowItemToolTips { get; set; }

        /// <summary>Gets or sets the virtual mode (no real items, populated via events). Stub in Majorsilence.Forms.</summary>
        public bool VirtualMode { get; set; }

        /// <summary>Gets or sets the number of virtual list items when VirtualMode is true. Stub in Majorsilence.Forms.</summary>
        public int VirtualListSize { get; set; }

        /// <summary>Raised when virtual mode items need to be retrieved. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<RetrieveVirtualItemEventArgs>? RetrieveVirtualItem { add { } remove { } }

        /// <summary>Raised when virtual items need to be cached. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<CacheVirtualItemsEventArgs>? CacheVirtualItems { add { } remove { } }

        /// <summary>Scrolls the specified item into view.</summary>
        /// <remarks>Real as of W5.6 (LST-19); it was an <c>Invalidate</c>, so the standard
        /// "append a line then scroll to it" idiom did nothing.</remarks>
        public void EnsureVisible (int index)
        {
            if (index < 0 || index >= Items.Count)
                return;

            UpdateVerticalScrollBar ();

            var line = index / Math.Max (1, ItemsPerLine);
            var visible = VisibleLineCount;
            var target = top_index;

            if (line < top_index)
                target = line;
            else if (line >= top_index + visible)
                target = line - visible + 1;

            target = Math.Max (0, Math.Min (target, Math.Max (0, LineCount - visible)));

            if (target == top_index) {
                Invalidate ();
                return;
            }

            top_index = target;

            // Through the scrollbar, so the thumb and the view cannot disagree.
            if (vscrollbar.Visible)
                vscrollbar.Value = target;

            Invalidate ();
        }

        /// <summary>Returns the item at the specified display coordinates, or null if none.</summary>
        /// <remarks>Takes LOGICAL coordinates, as every public hit-test here does, and converts them to
        /// the device space the bounds live in (LST-20). It also lays out first, so an answer before the
        /// first paint is a real one rather than a comparison against stale or empty bounds.</remarks>
        public ListViewItem? GetItemAt (int x, int y)
        {
            LayoutItems ();

            var location = ToDevice (new Point (x, y));

            return Items.FirstOrDefault (i => i.Bounds.Contains (location));
        }

        /// <summary>Returns the first item whose text matches the specified string.</summary>
        public ListViewItem? FindItemWithText (string text) =>
            Items.FirstOrDefault (i => string.Equals (i.Text, text, StringComparison.OrdinalIgnoreCase));

        /// <summary>Clears all currently selected items.</summary>
        public void ClearSelection ()
        {
            foreach (var item in Items)
                item.Selected = false;
        }

        // Called by ListViewItem.Selected's setter -- the one place a selection change is announced,
        // so `item.Selected = true` (the standard programmatic selection) updates dependent UI instead
        // of silently flipping a field (LST-17).
        private bool suppress_selection_events;

        // Depth, not a flag: SelectRange and the SelectedItem setter both batch, and one can run
        // inside the other.
        private int selection_batch;
        private bool selection_batch_pending;

        private void FlushSelectionBatch ()
        {
            if (selection_batch > 0 || !selection_batch_pending)
                return;

            selection_batch_pending = false;
            OnSelectedIndexChanged (EventArgs.Empty);
        }

        internal void OnItemSelectedChanged (ListViewItem item, bool selected)
        {
            if (suppress_selection_events)
                return;

            // Single-select: selecting one deselects the rest, which is what MultiSelect = false means.
            if (selected && !MultiSelect) {
                suppress_selection_events = true;

                try {
                    foreach (var other in Items)
                        if (!ReferenceEquals (other, item) && other.Selected)
                            other.SetSelectedInternal (false);
                } finally {
                    suppress_selection_events = false;
                }
            }

            Invalidate ();

            OnItemSelectionChanged (new ListViewItemSelectionChangedEventArgs (item, Items.IndexOf (item), selected));

            if (selection_batch > 0)
                selection_batch_pending = true;
            else
                OnSelectedIndexChanged (EventArgs.Empty);
        }

        /// <summary>Gets or sets whether the state image list uses a compatible image behavior. Stub in Majorsilence.Forms.</summary>
        public bool UseCompatibleStateImageBehavior { get; set; }

        /// <summary>Gets or sets the item that is currently focused.</summary>
        public ListViewItem? FocusedItem { get; set; }

        /// <summary>Gets the number of items that can be fully displayed vertically.</summary>
        /// <remarks>Counts the rows of the CURRENT view rather than always dividing by a 70px tile,
        /// which reported a fifth of the truth for a Details list.</remarks>
        public int CountPerPage => VisibleLineCount * ItemsPerLine;

        /// <summary>Gets the collection of ListViewGroup objects assigned to the control.</summary>
        public ListViewGroupCollection Groups { get; } = new ListViewGroupCollection ();

        /// <summary>Prevents the control from drawing until EndUpdate is called.</summary>
        public new void BeginUpdate () => SuspendLayout ();

        /// <summary>Resumes drawing the control after BeginUpdate.</summary>
        public new void EndUpdate () { ResumeLayout (false); Invalidate (); }

        /// <summary>Gets or sets the IComparer used for sorting list items.</summary>
        /// <remarks>Sorts on assignment, as upstream does (LST-12).</remarks>
        public System.Collections.IComparer? ListViewItemSorter {
            get => item_sorter;
            set {
                if (ReferenceEquals (item_sorter, value))
                    return;

                item_sorter = value;
                Sort ();
            }
        }

        private System.Collections.IComparer? item_sorter;

        /// <summary>Sorts the items, by <see cref="ListViewItemSorter"/> if one is set, else by text.</summary>
        /// <remarks>
        /// Real as of W5.6. It was <c>Invalidate ()</c> -- not an empty body, so the no-op scanner never
        /// saw it -- which made the canonical column-click sort (`ListViewItemSorter = new Comparer(col);
        /// Sort ();`) redraw the list in its original order (LST-12).
        /// </remarks>
        public void Sort ()
        {
            if (Items.Count < 2)
                return;

            var comparer = item_sorter;

            if (comparer is null) {
                if (sorting == SortOrder.None)
                    return;

                comparer = new TextComparer (sorting);
            }

            var ordered = Items.ToList ();

            // A stable sort: OrderBy is stable where List.Sort is not, and two rows that compare equal
            // must not swap places on every re-sort.
            ordered = ordered.OrderBy (i => i, new ComparerAdapter (comparer)).ToList ();

            suppress_selection_events = true;

            try {
                Items.Clear ();

                foreach (var item in ordered)
                    Items.Add (item);
            } finally {
                suppress_selection_events = false;
            }

            Invalidate ();
        }

        private sealed class ComparerAdapter : IComparer<ListViewItem>
        {
            private readonly System.Collections.IComparer inner;

            internal ComparerAdapter (System.Collections.IComparer inner) => this.inner = inner;

            public int Compare (ListViewItem? x, ListViewItem? y) => inner.Compare (x, y);
        }

        private sealed class TextComparer : System.Collections.IComparer
        {
            private readonly SortOrder order;

            internal TextComparer (SortOrder order) => this.order = order;

            public int Compare (object? x, object? y)
            {
                var result = string.Compare ((x as ListViewItem)?.Text, (y as ListViewItem)?.Text,
                    StringComparison.CurrentCulture);

                return order == SortOrder.Descending ? -result : result;
            }
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>Specifies the horizontal alignment of content.</summary>
    public enum HorizontalAlignment
    {
        /// <summary>Content is left-aligned.</summary>
        Left,
        /// <summary>Content is right-aligned.</summary>
        Right,
        /// <summary>Content is center-aligned.</summary>
        Center
    }

    /// <summary>Specifies how items are displayed in a ListView.</summary>
    public enum View
    {
        /// <summary>Items are displayed as large icons with text below.</summary>
        LargeIcon = 0,
        /// <summary>Items are displayed as small icons with text to the right.</summary>
        SmallIcon = 2,
        /// <summary>Items are displayed in a single column of small icons with text.</summary>
        List = 3,
        /// <summary>Items are displayed with details in columns.</summary>
        Details = 1,
        /// <summary>Items are displayed as large icons with more text.</summary>
        Tile = 4,
    }

    /// <summary>Delegate for the ListView.ColumnClick event.</summary>
#pragma warning disable CA1711
    public delegate void ColumnClickEventHandler (object sender, ColumnClickEventArgs e);
#pragma warning restore CA1711

    /// <summary>Provides data for the ListView.ColumnClick event.</summary>
    public class ColumnClickEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance.</summary>
        public ColumnClickEventArgs (int column) { Column = column; }

        /// <summary>Gets the index of the clicked column.</summary>
        public int Column { get; }
    }

    /// <summary>Represents a column header in a ListView Details view.</summary>
    public partial class ColumnHeader
    {
        /// <summary>Gets or sets the column header text.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the width of the column in pixels.</summary>
        public int Width { get; set; } = 60;

        /// <summary>Gets or sets the horizontal alignment of items in this column.</summary>
        public HorizontalAlignment TextAlign { get; set; }

        /// <summary>Gets the index of the column within its ListView.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Gets or sets the name of the column.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets user data associated with the column.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the display index of the column in the ListView.</summary>
        public int DisplayIndex { get; set; } = -1;

        /// <summary>Adjusts the column width based on the specified sizing mode. Stub in Majorsilence.Forms.</summary>
        public void AutoResize (ColumnHeaderAutoResizeStyle headerAutoResize) { }

        /// <summary>Gets or sets the index of the image for this column header. Stub in Majorsilence.Forms.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the key of the image for this column header. Stub in Majorsilence.Forms.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets the ListView that contains this column header.</summary>
        public ListView? ListView { get; internal set; }
    }

    /// <summary>Specifies how a ListView column auto-sizes.</summary>
    public enum ColumnHeaderAutoResizeStyle
    {
        /// <summary>The column is not resized.</summary>
        None,
        /// <summary>The column is resized to fit the header text.</summary>
        HeaderSize,
        /// <summary>The column is resized to fit the largest item text.</summary>
        ColumnContent
    }

    /// <summary>Represents a collection of ColumnHeader objects.</summary>
    public class ColumnHeaderCollection : Collection<ColumnHeader>
    {
        /// <summary>Adds a column header with the specified text.</summary>
        public ColumnHeader Add (string text)
        {
            var h = new ColumnHeader { Text = text, Index = Count };
            Add (h);
            return h;
        }

        /// <summary>Adds a column header with the specified text and width.</summary>
        public ColumnHeader Add (string text, int width)
        {
            var h = new ColumnHeader { Text = text, Width = width, Index = Count };
            Add (h);
            return h;
        }

        /// <summary>Adds a column header with the specified text, width and alignment.</summary>
        public ColumnHeader Add (string text, int width, HorizontalAlignment textAlign)
        {
            var h = new ColumnHeader { Text = text, Width = width, TextAlign = textAlign, Index = Count };
            Add (h);
            return h;
        }

        /// <summary>Adds an array of column headers to the collection.</summary>
        /// <remarks>
        /// This is the shape the WinForms designer emits for a ListView with columns -- one
        /// <c>Columns.AddRange (new ColumnHeader[] { ... })</c> per list -- so every migrated
        /// designer file needs it.
        /// </remarks>
        public void AddRange (params ColumnHeader[] values)
        {
            Guard.ThrowIfNull (values);

            foreach (var value in values)
                Add (value);
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, ColumnHeader item)
        {
            item.Index = index;
            base.InsertItem (index, item);
        }
    }

    /// <summary>Represents a group of items within a ListView.</summary>
    public partial class ListViewGroup
    {
        /// <summary>Initializes a new ListViewGroup.</summary>
        public ListViewGroup () { }

        /// <summary>Initializes a new ListViewGroup with the specified header.</summary>
        public ListViewGroup (string header) { Header = header; }

        /// <summary>Initializes a new ListViewGroup with the specified key and header.</summary>
        public ListViewGroup (string key, string header) { Name = key; Header = header; }

        /// <summary>Gets or sets the header text for the group.</summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the group.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the alignment of the group header.</summary>
        public HorizontalAlignment HeaderAlignment { get; set; }

        /// <summary>Gets or sets arbitrary data for the group.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets the items belonging to this group.</summary>
        public List<ListViewItem> Items { get; } = new List<ListViewItem> ();
    }

    /// <summary>Represents the collection of ListViewGroup objects in a ListView.</summary>
    public partial class ListViewGroupCollection : System.Collections.ObjectModel.Collection<ListViewGroup>
    {
        /// <summary>Adds a group with the specified header text.</summary>
        public ListViewGroup Add (string header)
        {
            var g = new ListViewGroup (header);
            Add (g);
            return g;
        }

        /// <summary>Adds a group with the specified key and header.</summary>
        public ListViewGroup Add (string key, string header)
        {
            var g = new ListViewGroup (key, header);
            Add (g);
            return g;
        }
    }
}
