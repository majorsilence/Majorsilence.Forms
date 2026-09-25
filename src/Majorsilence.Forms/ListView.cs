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
            BackgroundImageLayout = ImageLayout.None; // BackgroundImageTiled defaults to off
            ApplyBorderStyle (border_style);
            Items = new ListViewItemCollection (this);
            Columns = new ColumnHeaderCollection (this);
            groups = new ListViewGroupCollection (this);

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
            => IsRowView ? ScaledRowHeight : ScaledTileHeight + LogicalToDeviceUnits (6);

        internal int ItemsPerLine {
            get {
                if (IsRowView)
                    return 1;

                var stride = ScaledTileSize + LogicalToDeviceUnits (6);

                return Math.Max (1, (ItemArea.Width + LogicalToDeviceUnits (6)) / Math.Max (1, stride));
            }
        }

        // Group header bands occupy a line each, so the scrollbar counts them: a grouped list that
        // counted only items could not scroll far enough to reach its last row.
        internal int LineCount => (Items.Count + ItemsPerLine - 1) / Math.Max (1, ItemsPerLine) + GroupBandCount;

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
        /// The default style of the selection highlight: the selected item's background and, when set,
        /// its text colour. CSS: <c>ListView::selection</c>.
        /// </summary>
        public static readonly ControlStyle DefaultSelectionStyle = new ControlStyle (null,
            (style) => style.BackgroundColor = Theme.ControlHighlightLowColor);

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
        /// <remarks>
        /// Honours <see cref="TileSize"/> when the application set one. It used to be a hard-coded 70
        /// and <c>TileSize</c> was stored and read by nothing, so the property that exists to size
        /// tiles did not size them (<c>W6.2</c>).
        /// </remarks>
        internal int ScaledTileSize => LogicalToDeviceUnits (TileSize.Width > 0 ? TileSize.Width : DefaultTileExtent);

        /// <summary>The height of a large-icon tile, in device pixels.</summary>
        /// <remarks>
        /// Separate from the width because <see cref="TileSize"/> is a <see cref="Size"/> and tiles are
        /// not required to be square; the layout used one value for both, so an application asking for
        /// a wide tile could not get one even once the size was read.
        /// </remarks>
        internal int ScaledTileHeight => LogicalToDeviceUnits (TileSize.Height > 0 ? TileSize.Height : DefaultTileExtent);

        // The size a tile takes when the application has not asked for one. Upstream reads the real
        // value back off the native control; there is none here, so this is the figure the layout has
        // always used.
        private const int DefaultTileExtent = 70;

        /// <summary>The width the check box column takes when <see cref="CheckBoxes"/> is set.</summary>
        internal int ScaledCheckWidth => CheckBoxes ? LogicalToDeviceUnits (18) : 0;

        // Whether this view lays items out as one row each (Details, List, SmallIcon) rather than as
        // a grid of tiles (LargeIcon, Tile).
        internal bool IsRowView => View is View.Details or View.List or View.SmallIcon;

        /// <summary>The columns in the order they are displayed: by <see cref="ColumnHeader.DisplayIndex"/>,
        /// with an unset index falling back to the column's own position (W6 mechanisms).</summary>
        /// <remarks>Every piece of header and cell geometry walks this, so a reordered column moves its
        /// header, its divider and its cells together while <c>SubItems[column.Index]</c> stays put.</remarks>
        internal IReadOnlyList<ColumnHeader> DisplayColumns
            => Columns.OrderBy (c => c.DisplayIndex >= 0 ? c.DisplayIndex : c.Index).ThenBy (c => c.Index).ToList ();

        /// <summary>Moves a column to a new display position, shifting the others to keep the order dense.</summary>
        internal void SetColumnDisplayIndex (ColumnHeader header, int displayIndex)
        {
            var order = DisplayColumns.ToList ();

            order.Remove (header);
            order.Insert (Math.Max (0, Math.Min (displayIndex, order.Count)), header);

            for (var i = 0; i < order.Count; i++)
                order[i].SetDisplayIndexInternal (i);

            Invalidate ();
        }

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
                LayoutRowsGrouped (bounds);
            else
                LayoutTiles (bounds);
        }

        // Per-cell rectangles for Details, so a DrawSubItem handler -- and any code reading
        // SubItems[i].Bounds -- gets a real answer instead of Rectangle.Empty (LST-01).
        private void LayoutSubItems (ListViewItem item)
        {
            if (View != View.Details)
                return;

            // Device pixels throughout -- ScaledCheckWidth and ScaledColumnWidth are scaled, and so is
            // the item rectangle this is laid out inside (LAY-38). The public Bounds converts out.
            var device = item.DeviceBounds;
            var x = device.Left + ScaledCheckWidth;

            // Display order: a reordered column's cells sit where its header does (W6 mechanisms).
            foreach (var column in DisplayColumns) {
                var width = ScaledColumnWidth (column);
                var i = column.Index;

                if (i >= 0 && i < item.SubItems.Count) {
                    // The owner is what lets a sub-item find the display scale for its own conversion.
                    item.SubItems[i].Owner = item;
                    item.SubItems[i].DeviceBounds = new Rectangle (x, device.Top, width, device.Height);
                }

                x += width;
            }
        }

        private void LayoutTiles (Rectangle bounds)
        {
            var item_width = ScaledTileSize;
            var item_height = ScaledTileHeight;
            var item_margin = LogicalToDeviceUnits (6);

            var x = bounds.Left;
            var y = bounds.Top - top_index * (item_height + item_margin);

            foreach (var item in Items) {
                // A placed item (ListViewItem.Position, with AutoArrange off) sits where it was put and
                // takes no slot in the flow (W6 mechanisms).
                if (!AutoArrange && item.PlacedPosition is { } placed) {
                    // Client coordinates, as Position reads them back: the item area's padding is not added.
                    item.SetBounds (LogicalToDeviceUnits (placed.X), LogicalToDeviceUnits (placed.Y) - top_index * (item_height + item_margin), item_width, item_height);
                    continue;
                }

                item.SetBounds (x, y, item_width, item_height);
                x += item_width + item_margin;

                // Against the RIGHT EDGE, not the width: laid out from bounds.Left, comparing to
                // Width wrapped a padded or scrolled list a column early.
                if (x + item_width > bounds.Right) {
                    x = bounds.Left;
                    y += item_height + item_margin;
                }
            }
        }

        // Mouse coordinates are logical; item bounds are device. Same conversion, and the same
        // reason, as ListBox.GetIndexAtLocation.
        // A test seam: OnMouseClick is protected, and the modifier-extended selection below can only be
        // driven through a MouseEventArgs carrying its own keyData -- priming the static
        // Control.ModifierKeys does not work, because MouseEventArgs' constructor assigns that static
        // from its own keyData and so resets it to None.
        internal void DriveClick (Point location, Keys modifiers = Keys.None)
            => DriveClick (new MouseEventArgs (MouseButtons.Left, 1, location.X, location.Y, Point.Empty, keyData: modifiers));

        /// <inheritdoc cref="DriveClick(Point, Keys)"/>
        internal void DriveClick (MouseEventArgs e) => OnMouseClick (e);

        private Point ToDevice (Point location)
            => new Point (LogicalToDeviceUnits (location.X), LogicalToDeviceUnits (location.Y));

        internal void NotifyColumnWidthChanged (ColumnHeader column)
        {
            Invalidate ();
            OnColumnWidthChanged (new ColumnWidthChangedEventArgs (column.Index));
        }

        // ── header divider resize (W6) ─────────────────────────────────────────────────────────────
        // Dragging the divider at a column's right edge resizes it: ColumnWidthChanging on every
        // notification (cancellable), ColumnWidthChanged through the Width setter when it takes.
        private int resize_column = -1;
        private int resize_start_x;
        private int resize_start_width;
        private bool suppress_header_click;

        // The column whose divider is under the device-space point, or -1. The divider is the
        // column's trailing edge: its right in a left-to-right layout, its left when mirrored.
        internal int HeaderDividerAt (Point location)
        {
            if (ScaledHeaderHeight <= 0 || location.Y >= PaddedClientRectangle.Top + ScaledHeaderHeight)
                return -1;

            var zone = LogicalToDeviceUnits (4);

            foreach (var (column, left, width) in ColumnCells (ItemArea.Left + ScaledCheckWidth, ItemArea.Right)) {
                var edge = MirrorsColumns ? left : left + width;

                if (Math.Abs (location.X - edge) <= zone)
                    return column.Index;
            }

            return -1;
        }

        /// <summary>Whether the columns run right to left: <see cref="RightToLeftLayout"/> under <see cref="RightToLeft.Yes"/> (W6 mechanisms).</summary>
        internal bool MirrorsColumns => RightToLeftLayout && RightToLeft == RightToLeft.Yes;

        /// <summary>
        /// The device x range of every displayed column, in display order, laid from
        /// <paramref name="left"/> rightwards -- or from <paramref name="right"/> leftwards when the
        /// columns are mirrored. Every header, row and hit-test walks the columns through this, so the
        /// mirror is one decision (W6 mechanisms).
        /// </summary>
        internal IEnumerable<(ColumnHeader Column, int Left, int Width)> ColumnCells (int left, int right)
        {
            var mirrored = MirrorsColumns;
            var x = mirrored ? right : left;

            foreach (var column in DisplayColumns) {
                var width = ScaledColumnWidth (column);

                if (mirrored)
                    x -= width;

                yield return (column, x, width);

                if (!mirrored)
                    x += width;
            }
        }

        /// <summary>The device width one <see cref="ListViewItem.IndentCount"/> step moves an item's content: a small image's width.</summary>
        internal int ScaledIndentUnit => LogicalToDeviceUnits (SmallImageList?.ImageSize.Width is > 0 and var w ? w : 16);

        // ── header reorder drag (W6 mechanisms) ───────────────────────────────────────────────────
        // A press on a header (not on a divider) with AllowColumnReorder set arms a reorder; moving
        // past the drag threshold makes it one; the release asks ColumnReordered and, unless
        // cancelled, moves the column's DisplayIndex to the slot under the pointer.
        private int reorder_column = -1;
        private int reorder_start_x;
        private bool reorder_active;

        // ── ItemDrag (W6 mechanisms) ──────────────────────────────────────────────────────────────
        // A press on an item remembers it; the first move past SystemInformation.DragSize while the
        // button is held raises ItemDrag once for that press, which is when upstream raises it.
        private ListViewItem? drag_candidate;
        private Point drag_origin;
        private bool item_drag_raised;

        /// <inheritdoc/>
        protected override void OnMouseDown (MouseEventArgs e)
        {
            base.OnMouseDown (e);

            LayoutItems ();

            var device = ToDevice (e.Location);
            var in_header = ScaledHeaderHeight > 0 && device.Y < PaddedClientRectangle.Top + ScaledHeaderHeight;

            if (!in_header) {
                drag_candidate = Items.FirstOrDefault (i => i.DeviceBounds.Contains (device));
                drag_origin = e.Location;
                item_drag_raised = false;
            }

            if (e.Button != MouseButtons.Left)
                return;

            var column = HeaderDividerAt (device);

            if (column >= 0) {
                resize_column = column;
                resize_start_x = e.X;
                resize_start_width = DeviceToLogicalUnits (ScaledColumnWidth (Columns[column]));
                suppress_header_click = false;
                Capture = true;
                return;
            }

            if (in_header && AllowColumnReorder && ColumnIndexAt (device.X) is >= 0 and var header) {
                reorder_column = header;
                reorder_start_x = e.X;
                reorder_active = false;
                Capture = true;
            }
        }

        private void TrackHeaderReorder (MouseEventArgs e)
        {
            if (reorder_column < 0 || reorder_active)
                return;

            if (Math.Abs (e.X - reorder_start_x) > SystemInformation.DragSize.Width)
                reorder_active = true;
        }

        private void EndHeaderReorder (MouseEventArgs e)
        {
            if (reorder_column < 0)
                return;

            var header = Columns[reorder_column];
            var active = reorder_active;

            reorder_column = -1;
            reorder_active = false;
            Capture = false;

            if (!active)
                return;

            // The release that ends a drag is not a header click, as with a divider drag.
            suppress_header_click = true;

            var order = DisplayColumns.ToList ();
            var old_slot = order.IndexOf (header);
            var new_slot = DisplaySlotAt (ToDevice (e.Location).X);

            if (old_slot == new_slot)
                return;

            var args = new ColumnReorderedEventArgs (old_slot, new_slot, header);
            OnColumnReordered (args);

            if (!args.Cancel)
                SetColumnDisplayIndex (header, new_slot);
        }

        private void TrackItemDrag (MouseEventArgs e)
        {
            if (drag_candidate is null || item_drag_raised || e.Button == MouseButtons.None)
                return;

            var threshold = SystemInformation.DragSize;

            if (Math.Abs (e.X - drag_origin.X) <= threshold.Width && Math.Abs (e.Y - drag_origin.Y) <= threshold.Height)
                return;

            item_drag_raised = true;
            OnItemDrag (new ItemDragEventArgs (e.Button, drag_candidate));
        }

        private void TrackHeaderResize (MouseEventArgs e)
        {
            if (resize_column < 0)
                return;

            var proposed = Math.Max (0, resize_start_width + (e.X - resize_start_x));
            var column = Columns[resize_column];

            if (proposed == column.Width)
                return;

            var changing = new ColumnWidthChangingEventArgs (resize_column, proposed, false);
            OnColumnWidthChanging (changing);

            if (changing.Cancel)
                return;

            suppress_header_click = true;
            column.Width = changing.NewWidth;
        }

        /// <inheritdoc/>
        protected override void OnMouseUp (MouseEventArgs e)
        {
            base.OnMouseUp (e);

            if (resize_column >= 0) {
                resize_column = -1;
                Capture = false;
            }

            EndHeaderReorder (e);

            drag_candidate = null;
            item_drag_raised = false;
        }

        // The item under the pointer, for HotTracking's hot colour (W6). Tracked here rather than in the
        // renderer so a change repaints only when the hot item actually moves.
        internal ListViewItem? HotItem { get; private set; }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);
            TrackHeaderResize (e);
            TrackHeaderReorder (e);
            TrackItemDrag (e);
            UpdateDividerCursor (e.Location);
            SetHotItem (HotTracking ? GetItemAt (e.X, e.Y) : null);
        }

        // The west-east cursor over a header divider, and for the whole of a resize drag; put back only
        // if this control set it, so an application's own cursor is left alone (W6 mechanisms).
        private bool showing_divider_cursor;

        private void UpdateDividerCursor (Point location)
        {
            var over_divider = resize_column >= 0 || (ScaledHeaderHeight > 0 && HeaderDividerAt (ToDevice (location)) >= 0);

            if (over_divider == showing_divider_cursor)
                return;

            showing_divider_cursor = over_divider;
            Cursor = over_divider ? Cursors.VSplit : Cursors.Default;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);
            SetHotItem (null);

            if (showing_divider_cursor && resize_column < 0) {
                showing_divider_cursor = false;
                Cursor = Cursors.Default;
            }
        }

        /// <inheritdoc/>
        /// <remarks>F2 begins editing the focused item's label when <see cref="LabelEdit"/> allows it,
        /// as upstream does (W6 mechanisms).</remarks>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            if (!e.Handled && e.KeyCode == Keys.F2 && LabelEdit && FocusedItem is { } focused) {
                BeginLabelEdit (focused);
                e.Handled = true;
            }
        }

        // ── label editing (W6 mechanisms) ─────────────────────────────────────────────────────────
        // ListViewItem.BeginEdit and F2 put a LabelEditBox over the item's label. BeforeLabelEdit may
        // refuse; AfterLabelEdit sees the typed text (or null for a cancelled edit, as upstream passes)
        // and may refuse that too, in which case the item keeps its text.
        private LabelEditBox? label_editor;
        private ListViewItem? editing_item;

        /// <summary>The in-place editor while a label is being edited; null otherwise.</summary>
        internal TextBox? LabelEditor => editing_item is null ? null : label_editor;

        /// <summary>The item whose label is being edited, or null.</summary>
        internal ListViewItem? EditingItem => editing_item;

        internal void BeginLabelEdit (ListViewItem item)
        {
            if (!LabelEdit)
                throw new InvalidOperationException ("LabelEdit must be true to edit an item's label.");

            if (!ReferenceEquals (item.ListView, this))
                return;

            EndLabelEdit (commit: true);

            var before = new LabelEditEventArgs (item.Index);
            OnBeforeLabelEdit (before);

            if (before.CancelEdit)
                return;

            LayoutItems ();

            if (label_editor is null) {
                label_editor = new LabelEditBox { Visible = false };
                label_editor.Commit = () => EndLabelEdit (commit: true);
                label_editor.Cancel = () => EndLabelEdit (commit: false);
                Controls.Add (label_editor);
            }

            editing_item = item;
            label_editor.Bounds = DeviceToLogicalUnits (LabelDeviceBounds (item));
            label_editor.Text = item.Text;
            label_editor.Visible = true;
            label_editor.SelectAll ();
            label_editor.Focus ();
        }

        // The rectangle the label occupies, in device pixels: the first display column's cell in
        // Details, the text after the check box (and small image) in the other row views, and the
        // caption band under the icon in the tile views.
        private Rectangle LabelDeviceBounds (ListViewItem item)
        {
            var bounds = item.DeviceBounds;
            var left = bounds.Left + ScaledCheckWidth;

            if (View == View.Details && DisplayColumns.Count > 0)
                return new Rectangle (left, bounds.Top, ScaledColumnWidth (DisplayColumns[0]), bounds.Height);

            if (IsRowView) {
                if (View == View.SmallIcon && item.ImageSK is not null)
                    left += LogicalToDeviceUnits (20);

                return new Rectangle (left, bounds.Top, Math.Max (0, bounds.Right - left), bounds.Height);
            }

            var caption_top = bounds.Top + LogicalToDeviceUnits (38);

            return new Rectangle (bounds.Left, caption_top, bounds.Width, Math.Max (0, bounds.Bottom - caption_top));
        }

        internal void EndLabelEdit (bool commit)
        {
            if (editing_item is null || label_editor is null)
                return;

            // Cleared first: hiding the editor drops its focus, which would otherwise re-enter here.
            var item = editing_item;
            var text = label_editor.Text;

            editing_item = null;
            label_editor.Visible = false;

            var after = new LabelEditEventArgs (item.Index, commit ? text : null);
            OnAfterLabelEdit (after);

            if (commit && !after.CancelEdit && after.Label is { } label)
                item.Text = label;

            Invalidate ();
            Focus ();
        }

        private void SetHotItem (ListViewItem? item)
        {
            if (ReferenceEquals (HotItem, item))
                return;

            HotItem = item;
            Invalidate ();
        }

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            base.OnMouseClick (e);

            LayoutItems ();

            var location = ToDevice (e.Location);

            // A click in the header band is a column click, not an item click (LST-18).
            if (ScaledHeaderHeight > 0 && location.Y < PaddedClientRectangle.Top + ScaledHeaderHeight) {
                // The release that ends a divider drag is not a header click (W6).
                if (suppress_header_click) {
                    suppress_header_click = false;
                    return;
                }

                var column = ColumnIndexAt (location.X);

                if (column >= 0 && HeaderStyle == ColumnHeaderStyle.Clickable)
                    OnColumnClick (new ColumnClickEventArgs (column));

                return;
            }

            // A group's task link is a hit target of its own, above the items: it sits in a band, so no
            // item covers it, and the event it raises was declared and never raised from anywhere.
            if (GroupTaskLinkAt (location) is { } linked) {
                OnGroupTaskLinkClick (new ListViewGroupEventArgs (Groups.IndexOf (linked)));

                return;
            }

            // DeviceBounds, because `location` is device (ToDevice above) -- LAY-38 made the public
            // Bounds logical and these three hit-tests were missed, so a click landed nowhere near the
            // item it was over on any display whose scale is not 1.
            var clicked_item = Items.FirstOrDefault (tp => tp.DeviceBounds.Contains (location));

            if (clicked_item is null)
                return;

            // The check box is its own hit target: clicking it toggles and does not re-select.
            if (CheckBoxes && location.X < clicked_item.DeviceBounds.Left + ScaledCheckWidth) {
                clicked_item.Checked = !clicked_item.Checked;
                return;
            }

            FocusedItem = clicked_item;

            // Ctrl adds to the selection, Shift extends from the focused item -- both only when
            // MultiSelect allows it, which is what MultiSelect = false is for (LST-17).
            //
            // e.Modifiers, not the static Control.ModifierKeys: the modifiers that belong to THIS
            // click are the ones carried on its event args, and the static is whatever is held down
            // now -- which for a queued or replayed event is a different question. It also made both
            // branches untestable, because MouseEventArgs' constructor assigns the static from its own
            // keyData and so resets it to None (LST-27).
            if (MultiSelect && (e.Modifiers & Keys.Control) == Keys.Control)
                clicked_item.Selected = !clicked_item.Selected;
            else if (MultiSelect && (e.Modifiers & Keys.Shift) == Keys.Shift && anchor_index >= 0)
                SelectRange (anchor_index, Items.IndexOf (clicked_item));
            else {
                SelectedItem = clicked_item;
                anchor_index = Items.IndexOf (clicked_item);
            }

            // Activation says which gesture activates an item. It was stored and read by nothing, so
            // only a double click ever activated -- a list set to OneClick, which is the whole point of
            // the property, behaved exactly like a Standard one. A modified click extends the selection
            // rather than activating, which is why this sits after the branch above rather than beside
            // the hit test.
            if (Activation == ItemActivation.OneClick && (e.Modifiers & (Keys.Control | Keys.Shift)) == Keys.None)
                OnItemActivate (EventArgs.Empty);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <see cref="ShowItemToolTips"/> and <see cref="ListViewItem.ToolTipText"/>, both stored and
        /// read by nothing before (<c>LST-59</c>).
        /// </remarks>
        internal override string? GetToolTipText (System.Drawing.Point location)
            => ShowItemToolTips ? GetItemAt (location.X, location.Y)?.ToolTipText : null;

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

            // Virtual mode reports a range as one notification rather than one per item, as upstream
            // does (W6 mechanisms). The items outside the range are deselected the same way.
            if (VirtualMode) {
                for (var i = 0; i < Items.Count; i++)
                    Items.RawAt (i).SetSelectedInternal (i >= start && i <= end);

                Invalidate ();
                OnVirtualItemsSelectionRangeChanged (new ListViewVirtualItemsSelectionRangeChangedEventArgs (start, end, true));
                OnSelectedIndexChanged (EventArgs.Empty);
                return;
            }

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
            foreach (var (column, left, width) in ColumnCells (ItemArea.Left + ScaledCheckWidth, ItemArea.Right))
                if (x >= left && x < left + width)
                    return column.Index;

            return -1;
        }

        // The display slot a header dropped at device x lands in: the slot of the column under the
        // pointer, or the last slot past the final column.
        private int DisplaySlotAt (int x)
        {
            var slot = 0;

            foreach (var (_, left, width) in ColumnCells (ItemArea.Left + ScaledCheckWidth, ItemArea.Right)) {
                if (MirrorsColumns ? x >= left : x < left + width)
                    return slot;

                slot++;
            }

            return Math.Max (0, DisplayColumns.Count - 1);
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            LayoutItems ();

            var clicked_item = Items.FirstOrDefault (tp => tp.DeviceBounds.Contains (ToDevice (e.Location)));

            if (clicked_item != null) {
                ItemDoubleClicked?.Invoke (this, new EventArgs<ListViewItem> (clicked_item));

                // WinForms' own name for this, and the one migrated code subscribes: double-click (or
                // Enter) ACTIVATES an item. It was declared with discarding accessors (LST-18).
                //
                // Not when Activation is OneClick: the single click that opened this double click has
                // already activated, and firing again would deliver two activations for one gesture.
                if (Activation != ItemActivation.OneClick)
                    OnItemActivate (EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            UpdateVerticalScrollBar ();
            ResolveVisibleVirtualItems ();
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
        public bool ShowGroups {
            get => show_groups;
            set {
                if (show_groups == value)
                    return;

                show_groups = value;
                RefreshGroups ();
            }
        }

        private bool show_groups = true;

        /// <summary>Gets or sets whether icons are automatically arranged into the flow.</summary>
        /// <remarks>Read as of W6 mechanisms: with it off, an item whose <see cref="ListViewItem.Position"/>
        /// was set stays where it was put.</remarks>
        public bool AutoArrange { get; set; } = true;

        /// <summary>Gets or sets the style of column headers. Stub in Majorsilence.Forms.</summary>
        public ColumnHeaderStyle HeaderStyle { get; set; } = ColumnHeaderStyle.Clickable;

        /// <summary>Gets or sets the activation method for items. Stub in Majorsilence.Forms.</summary>
        public ItemActivation Activation { get; set; } = ItemActivation.Standard;

        /// <summary>Gets or sets whether the user can reorder columns by dragging their headers.</summary>
        /// <remarks>Read as of W6 mechanisms: a header drag past the drag threshold asks
        /// <see cref="ColumnReordered"/> on release and moves the column's <see cref="ColumnHeader.DisplayIndex"/>.</remarks>
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
        /// <remarks>Real as of W6 mechanisms: raised once per press when the pointer moves past
        /// <see cref="SystemInformation.DragSize"/> with a button held over an item.</remarks>
        public event EventHandler<ItemDragEventArgs>? ItemDrag;

        /// <summary>Raises the <see cref="ItemDrag"/> event.</summary>
        protected virtual void OnItemDrag (ItemDragEventArgs e) => ItemDrag?.Invoke (this, e);

        /// <summary>Raised when an item is checked or unchecked.</summary>
        public event EventHandler<ItemCheckedEventArgs>? ItemChecked;

        /// <summary>Raises the <see cref="ItemChecked"/> event.</summary>
        protected virtual void OnItemChecked (ItemCheckedEventArgs e) => ItemChecked?.Invoke (this, e);

        /// <summary>Raised before a label is edited; <see cref="LabelEditEventArgs.CancelEdit"/> refuses.</summary>
        /// <remarks>Real as of W6 mechanisms; see <see cref="LabelEdit"/>.</remarks>
        public event EventHandler<LabelEditEventArgs>? BeforeLabelEdit;

        /// <summary>Raises the <see cref="BeforeLabelEdit"/> event.</summary>
        protected virtual void OnBeforeLabelEdit (LabelEditEventArgs e) => BeforeLabelEdit?.Invoke (this, e);

        /// <summary>Raised after a label is edited, with the new text -- or a null
        /// <see cref="LabelEditEventArgs.Label"/> for a cancelled edit, as upstream passes.</summary>
        /// <remarks>Real as of W6 mechanisms; <see cref="LabelEditEventArgs.CancelEdit"/> keeps the old text.</remarks>
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

        /// <summary>Gets or sets whether item labels can be edited in place.</summary>
        /// <remarks>Read as of W6 mechanisms: <see cref="ListViewItem.BeginEdit"/> and F2 on the focused
        /// item open an editor over the label, asking <see cref="BeforeLabelEdit"/> first and reporting
        /// through <see cref="AfterLabelEdit"/>.</remarks>
        public bool LabelEdit { get; set; }

        /// <summary>Gets or sets whether item labels wrap. Stub in Majorsilence.Forms.</summary>
        public bool LabelWrap { get; set; } = true;

        /// <summary>Gets or sets whether hover-selection is enabled. Stub in Majorsilence.Forms.</summary>
        public bool HoverSelection { get; set; }

        /// <summary>Gets or sets whether items can be scrolled. Stub in Majorsilence.Forms.</summary>
        public bool Scrollable { get; set; } = true;

        /// <summary>Gets or sets whether item tooltips are shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowItemToolTips { get; set; }

        /// <summary>Gets or sets whether the list is in virtual mode: <see cref="VirtualListSize"/> items,
        /// supplied on demand through <see cref="RetrieveVirtualItem"/>.</summary>
        /// <remarks>
        /// Real as of W6 mechanisms. In virtual mode <see cref="Items"/> holds one placeholder per
        /// index and refuses adds and removes; reading <c>Items[i]</c>, painting, or hit-testing
        /// resolves an index through <see cref="RetrieveVirtualItem"/> (the visible run is announced
        /// with <see cref="CacheVirtualItems"/> first), <see cref="FindItemWithText(string)"/> asks
        /// <see cref="SearchForVirtualItem"/>, and a range selection reports through
        /// <see cref="VirtualItemsSelectionRangeChanged"/>. The placeholders cost one small object per
        /// index, so this is for tens of thousands of rows rather than millions.
        /// </remarks>
        public bool VirtualMode {
            get => virtual_mode;
            set {
                if (virtual_mode == value)
                    return;

                virtual_mode = value;
                RebuildVirtualItems ();
            }
        }

        private bool virtual_mode;

        /// <summary>Gets or sets the number of items when <see cref="VirtualMode"/> is set.</summary>
        /// <remarks>Real as of W6 mechanisms: setting it (re)creates the placeholders, so every index is
        /// asked of <see cref="RetrieveVirtualItem"/> afresh.</remarks>
        public int VirtualListSize {
            get => virtual_list_size;
            set {
                if (value < 0)
                    throw new ArgumentOutOfRangeException (nameof (value), value, "VirtualListSize cannot be negative.");

                if (virtual_list_size == value)
                    return;

                virtual_list_size = value;

                if (VirtualMode)
                    RebuildVirtualItems ();
            }
        }

        private int virtual_list_size;

        /// <summary>Raised in virtual mode to supply the item at an index.</summary>
        /// <remarks>Real as of W6 mechanisms; see <see cref="VirtualMode"/>. A handler that supplies no
        /// item leaves an empty placeholder in that slot.</remarks>
        public event EventHandler<RetrieveVirtualItemEventArgs>? RetrieveVirtualItem;

        /// <summary>Raised in virtual mode before a run of items is retrieved, naming the range.</summary>
        /// <remarks>Real as of W6 mechanisms: raised for the visible run before each paint that still
        /// has unresolved items in it.</remarks>
        public event EventHandler<CacheVirtualItemsEventArgs>? CacheVirtualItems;

        /// <summary>Raises the <see cref="RetrieveVirtualItem"/> event.</summary>
        protected virtual void OnRetrieveVirtualItem (RetrieveVirtualItemEventArgs e) => RetrieveVirtualItem?.Invoke (this, e);

        /// <summary>Raises the <see cref="CacheVirtualItems"/> event.</summary>
        protected virtual void OnCacheVirtualItems (CacheVirtualItemsEventArgs e) => CacheVirtualItems?.Invoke (this, e);

        // True while this control itself is filling or swapping Items in virtual mode, which is the
        // one mutation the collection allows there.
        internal bool VirtualFill { get; private set; }

        private void RebuildVirtualItems ()
        {
            VirtualFill = true;

            try {
                Items.Clear ();

                if (VirtualMode)
                    for (var i = 0; i < virtual_list_size; i++)
                        Items.Add (new ListViewItem { IsVirtualPlaceholder = true });
            } finally {
                VirtualFill = false;
            }

            Invalidate ();
        }

        /// <summary>Resolves the placeholder at <paramref name="index"/> through <see cref="RetrieveVirtualItem"/>, once.</summary>
        internal void ResolveVirtualItem (int index)
        {
            if (!VirtualMode || index < 0 || index >= Items.Count || !Items.PlaceholderAt (index))
                return;

            var e = new RetrieveVirtualItemEventArgs (index);
            OnRetrieveVirtualItem (e);

            if (e.Item is not { } item)
                return;

            // The slot's selection and check state belong to the INDEX, as upstream keeps them.
            var placeholder = Items.RawAt (index);
            item.SetSelectedInternal (placeholder.Selected);
            item.SetCheckedInternal (placeholder.Checked);

            VirtualFill = true;

            try {
                Items[index] = item;
            } finally {
                VirtualFill = false;
            }
        }

        // Before a paint: announce the visible run, then resolve whatever in it is still a placeholder.
        private void ResolveVisibleVirtualItems ()
        {
            if (!VirtualMode || Items.Count == 0)
                return;

            var per_line = Math.Max (1, ItemsPerLine);
            var first = Math.Max (0, Math.Min (Items.Count - 1, top_index * per_line));
            var last = Math.Max (first, Math.Min (Items.Count - 1, (top_index + VisibleLineCount + 1) * per_line - 1));

            var unresolved = false;

            for (var i = first; i <= last && !unresolved; i++)
                unresolved = Items.PlaceholderAt (i);

            if (!unresolved)
                return;

            OnCacheVirtualItems (new CacheVirtualItemsEventArgs (first, last));

            for (var i = first; i <= last; i++)
                ResolveVirtualItem (i);
        }

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

            return Items.FirstOrDefault (i => i.DeviceBounds.Contains (location));
        }

        /// <summary>Returns the first item whose text matches the specified string.</summary>
        /// <remarks>In <see cref="VirtualMode"/> the application answers through
        /// <see cref="SearchForVirtualItem"/> (a prefix text search from index 0, as upstream frames
        /// it) and the item at the index it names is returned (W6 mechanisms).</remarks>
        public ListViewItem? FindItemWithText (string text)
        {
            if (VirtualMode)
                return SearchVirtual (text, includeSubItems: false, startIndex: 0, isPrefixSearch: true);

            return Items.FirstOrDefault (i => string.Equals (i.Text, text, StringComparison.OrdinalIgnoreCase));
        }

        // The virtual-mode half of every FindItemWithText overload: the application answers, and the
        // index it names is resolved and returned.
        internal ListViewItem? SearchVirtual (string text, bool includeSubItems, int startIndex, bool isPrefixSearch)
        {
            var search = new SearchForVirtualItemEventArgs (true, isPrefixSearch, includeSubItems, text, Point.Empty,
                SearchDirectionHint.Down, startIndex);
            OnSearchForVirtualItem (search);

            return search.Index >= 0 && search.Index < Items.Count ? Items[search.Index] : null;
        }

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
        public bool UseCompatibleStateImageBehavior { get; set; } = true;

        /// <summary>Gets or sets the item that is currently focused.</summary>
        public ListViewItem? FocusedItem { get; set; }

        /// <summary>Gets the number of items that can be fully displayed vertically.</summary>
        /// <remarks>Counts the rows of the CURRENT view rather than always dividing by a 70px tile,
        /// which reported a fifth of the truth for a Details list.</remarks>
        public int CountPerPage => VisibleLineCount * ItemsPerLine;

        /// <summary>Gets the collection of ListViewGroup objects assigned to the control.</summary>
        public ListViewGroupCollection Groups => groups;

        private readonly ListViewGroupCollection groups;

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

        // A width change tells the owning list, which raises ColumnWidthChanged and repaints (W6). -1
        // and -2 keep their auto-size meanings.
        private int width = 60;

        /// <summary>Gets or sets the width of the column in pixels.</summary>
        public int Width {
            get => width;
            set {
                if (width == value)
                    return;

                width = value;
                ListView?.NotifyColumnWidthChanged (this);
            }
        }

        /// <summary>Gets or sets the horizontal alignment of items in this column.</summary>
        public HorizontalAlignment TextAlign { get; set; }

        /// <summary>Gets the index of the column within its ListView.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Gets or sets the name of the column.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets user data associated with the column.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the display index of the column in the ListView.</summary>
        /// <remarks>Read as of W6 mechanisms: the header, dividers and cells are laid out in display
        /// order, and setting this on a column that belongs to a list moves it there and renumbers the
        /// others, as upstream does. A header drag with <see cref="ListView.AllowColumnReorder"/> set
        /// arrives here through <see cref="ListView.ColumnReordered"/>.</remarks>
        public int DisplayIndex {
            get => display_index;
            set {
                if (ListView is { } list)
                    list.SetColumnDisplayIndex (this, value);
                else
                    display_index = value;
            }
        }

        private int display_index = -1;

        internal void SetDisplayIndexInternal (int value) => display_index = value;

        /// <summary>Adjusts the column width based on the specified sizing mode. Stub in Majorsilence.Forms.</summary>
        /// <summary>Resizes this column to fit its header text or its contents.</summary>
        /// <remarks>Real as of W6 mechanisms: it forwards to the list's own <c>AutoResizeColumn</c>,
        /// which measures the same way. A header that is not in a list has nothing to measure against
        /// and does nothing.</remarks>
        public void AutoResize (ColumnHeaderAutoResizeStyle headerAutoResize)
            => ListView?.AutoResizeColumn (Index, headerAutoResize);

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
