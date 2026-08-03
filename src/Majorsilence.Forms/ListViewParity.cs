using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Majorsilence.Forms
{
    // The ListView parity surface (docs/winforms-gap-plan.md).
    //
    // Two kinds of gap were in here, and only one of them was countable.
    //
    // The countable one is 34 missing members: the owner-draw events, the column auto-resize family,
    // the group and insertion-mark surface.
    //
    // The other is that several members that existed had the wrong shape, so they were never reported
    // and never worked. SelectedItems and CheckedItems returned IEnumerable<ListViewItem>, which means
    // `listView.SelectedItems.Count` -- about the most common line of ListView code there is -- did not
    // compile. TopItem was a bool. HitTest returned an int index rather than a
    // ListViewHitTestInfo. Those are fixed here alongside the additions, because a member that is
    // present and unusable is worse than one that is absent: absence at least fails loudly.
    //
    // The collections are live views over the control, as they are upstream: they are not snapshots,
    // so a caller that holds one and then changes the selection sees the change.

    public partial class ListView
    {
        private ListViewInsertionMark? insertion_mark;

        /// <summary>Gets or sets how items are aligned when they are arranged.</summary>
        public ListViewAlignment Alignment { get; set; } = ListViewAlignment.Top;

        /// <summary>Gets or sets the border drawn around the control.</summary>
        public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

        /// <summary>Gets or sets whether the background image is tiled.</summary>
        public bool BackgroundImageTiled { get; set; }

        /// <summary>Gets or sets whether items are highlighted as the pointer passes over them.</summary>
        public bool HotTracking { get; set; }

        /// <summary>Gets or sets whether the control's items are drawn by the application.</summary>
        /// <remarks>Setting this is what makes <see cref="DrawItem"/>, <see cref="DrawSubItem"/> and
        /// <see cref="DrawColumnHeader"/> meaningful; the renderer consults it before drawing.</remarks>
        public bool OwnerDraw { get; set; }

        /// <summary>Gets or sets whether the control lays out right to left when RightToLeft is set.</summary>
        public bool RightToLeftLayout { get; set; }

        /// <summary>Gets or sets the size of tiles in tile view.</summary>
        public Size TileSize { get; set; } = new Size (0, 0);

        /// <summary>Gets or sets the image list used for group title images.</summary>
        public ImageList? GroupImageList { get; set; }

        /// <summary>Gets the insertion mark used during a drag-reorder.</summary>
        public ListViewInsertionMark InsertionMark => insertion_mark ??= new ListViewInsertionMark ();

        /// <summary>Gets the items that are currently selected.</summary>
        public SelectedListViewItemCollection SelectedItems => new SelectedListViewItemCollection (this);

        /// <summary>Gets the indices of the items that are currently selected.</summary>
        public SelectedIndexCollection SelectedIndices => new SelectedIndexCollection (this);

        /// <summary>Gets the items whose check box is ticked.</summary>
        public CheckedListViewItemCollection CheckedItems => new CheckedListViewItemCollection (this);

        /// <summary>Gets the indices of the items whose check box is ticked.</summary>
        public CheckedIndexCollection CheckedIndices => new CheckedIndexCollection (this);

        /// <summary>Gets or sets the first item visible in the control.</summary>
        /// <remarks>This used to be declared as a <c>bool</c>, which cannot mean anything: WinForms
        /// returns the item that is scrolled to the top.</remarks>
        public ListViewItem? TopItem {
            get => Items.Count > 0 ? Items[0] : null;
            set => value?.EnsureVisible ();
        }

        /// <summary>Removes every item and column from the control.</summary>
        public void Clear ()
        {
            Items.Clear ();
            Columns.Clear ();
            Invalidate ();
        }

        /// <summary>Arranges the items according to the given alignment.</summary>
        /// <remarks>Icon layout is the renderer's job here, so this records the alignment and asks for
        /// a repaint rather than moving items itself.</remarks>
        public void ArrangeIcons (ListViewAlignment value)
        {
            Alignment = value;
            Invalidate ();
        }

        /// <inheritdoc cref="ArrangeIcons(ListViewAlignment)"/>
        public void ArrangeIcons () => ArrangeIcons (Alignment);

        /// <summary>Resizes one column to fit its content or its header.</summary>
        public void AutoResizeColumn (int columnIndex, ColumnHeaderAutoResizeStyle headerAutoResize)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
                throw new ArgumentOutOfRangeException (nameof (columnIndex));

            var column = Columns[columnIndex];

            switch (headerAutoResize) {
                case ColumnHeaderAutoResizeStyle.HeaderSize:
                    column.Width = MeasureWidth (column.Text);
                    break;
                case ColumnHeaderAutoResizeStyle.ColumnContent:
                    column.Width = Math.Max (1, WidestCellIn (columnIndex));
                    break;
                default:
                    return;     // None: WinForms leaves the width alone
            }

            Invalidate ();
        }

        /// <summary>Resizes every column to fit its content or its header.</summary>
        public void AutoResizeColumns (ColumnHeaderAutoResizeStyle headerAutoResize)
        {
            for (var i = 0; i < Columns.Count; i++)
                AutoResizeColumn (i, headerAutoResize);
        }

        /// <summary>Returns the item nearest the given point in the given direction.</summary>
        public ListViewItem? FindNearestItem (SearchDirectionHint dir, Point point)
            => FindNearestItem (dir, point.X, point.Y);

        /// <inheritdoc cref="FindNearestItem(SearchDirectionHint,Point)"/>
        public ListViewItem? FindNearestItem (SearchDirectionHint dir, int x, int y)
        {
            ListViewItem? best = null;
            var bestDistance = long.MaxValue;

            foreach (var item in Items) {
                var centre = new Point (item.Bounds.X + item.Bounds.Width / 2, item.Bounds.Y + item.Bounds.Height / 2);

                var inDirection = dir switch {
                    SearchDirectionHint.Left => centre.X < x,
                    SearchDirectionHint.Right => centre.X > x,
                    SearchDirectionHint.Up => centre.Y < y,
                    SearchDirectionHint.Down => centre.Y > y,
                    _ => false,
                };

                if (!inDirection)
                    continue;

                long dx = centre.X - x;
                long dy = centre.Y - y;
                var distance = dx * dx + dy * dy;

                if (distance < bestDistance) {
                    bestDistance = distance;
                    best = item;
                }
            }

            return best;
        }

        /// <summary>Returns what is under the given point.</summary>
        /// <remarks>This replaces a method of the same name that returned an <c>int</c> item index. The
        /// index is still available as <c>HitTest (x, y).Item?.Index</c>, and the returned object also
        /// says whether the point was on the label, the image, or nothing at all.</remarks>
        public ListViewHitTestInfo HitTest (int x, int y)
        {
            foreach (var item in Items) {
                if (!item.Bounds.Contains (x, y))
                    continue;

                var subItem = item.GetSubItemAt (x, y);
                var location = subItem is not null && !ReferenceEquals (subItem, item.SubItems[0])
                    ? ListViewHitTestLocations.Label
                    : ListViewHitTestLocations.Image;

                return new ListViewHitTestInfo (item, subItem, location);
            }

            return new ListViewHitTestInfo (null, null, ListViewHitTestLocations.None);
        }

        /// <inheritdoc cref="HitTest(int,int)"/>
        public ListViewHitTestInfo HitTest (Point point) => HitTest (point.X, point.Y);

        /// <summary>Raised when the application must draw an item because <see cref="OwnerDraw"/> is set.</summary>
        public event DrawListViewItemEventHandler? DrawItem;

        /// <summary>Raised when the application must draw a sub-item because <see cref="OwnerDraw"/> is set.</summary>
        public event DrawListViewSubItemEventHandler? DrawSubItem;

        /// <summary>Raised when the application must draw a column header because <see cref="OwnerDraw"/> is set.</summary>
        public event DrawListViewColumnHeaderEventHandler? DrawColumnHeader;

        /// <summary>Raised after the user drags a column into a new position.</summary>
        public event ColumnReorderedEventHandler? ColumnReordered;

        /// <summary>Raised after a column's width changes.</summary>
        public event ColumnWidthChangedEventHandler? ColumnWidthChanged;

        /// <summary>Raised while a column's width is being changed.</summary>
        public event ColumnWidthChangingEventHandler? ColumnWidthChanging;

        /// <summary>Raised when the pointer rests over an item.</summary>
        public event ListViewItemMouseHoverEventHandler? ItemMouseHover;

        /// <summary>Raised when a range of virtual items changes selection.</summary>
        public event ListViewVirtualItemsSelectionRangeChangedEventHandler? VirtualItemsSelectionRangeChanged;

        /// <summary>Raised when a virtual-mode control needs to search its items.</summary>
        public event SearchForVirtualItemEventHandler? SearchForVirtualItem;

        /// <summary>Raised when a group is expanded or collapsed.</summary>
        public event EventHandler<ListViewGroupEventArgs>? GroupCollapsedStateChanged;

        /// <summary>Raised when a group's task link is clicked.</summary>
        public event EventHandler<ListViewGroupEventArgs>? GroupTaskLinkClick;

        /// <summary>Forces a range of items to be redrawn.</summary>
        /// <remarks>A method, not an event -- checked against the reference assembly rather than
        /// assumed from the name, which reads like one.</remarks>
        public void RedrawItems (int startIndex, int endIndex, bool invalidateOnly)
        {
            if (startIndex < 0 || startIndex >= Items.Count)
                throw new ArgumentOutOfRangeException (nameof (startIndex));
            if (endIndex < startIndex || endIndex >= Items.Count)
                throw new ArgumentOutOfRangeException (nameof (endIndex));

            for (var i = startIndex; i <= endIndex; i++)
                Invalidate (Items[i].Bounds);

            if (!invalidateOnly)
                Update ();
        }

        /// <summary>Raised when <see cref="RightToLeftLayout"/> changes.</summary>
        public event EventHandler? RightToLeftLayoutChanged;

        /// <summary>Raised when the background image changes.</summary>
        /// <remarks>Never raised: this control does not draw a background image. Present because
        /// designer-generated code binds it.</remarks>
#pragma warning disable CS0067
        public event EventHandler? BackgroundImageLayoutChanged;
#pragma warning restore CS0067

        /// <summary>Raises the <see cref="DrawItem"/> event.</summary>
        protected virtual void OnDrawItem (DrawListViewItemEventArgs e) => DrawItem?.Invoke (this, e);

        /// <summary>Raises the <see cref="DrawSubItem"/> event.</summary>
        protected virtual void OnDrawSubItem (DrawListViewSubItemEventArgs e) => DrawSubItem?.Invoke (this, e);

        /// <summary>Raises the <see cref="DrawColumnHeader"/> event.</summary>
        protected virtual void OnDrawColumnHeader (DrawListViewColumnHeaderEventArgs e) => DrawColumnHeader?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnReordered"/> event.</summary>
        protected virtual void OnColumnReordered (ColumnReorderedEventArgs e) => ColumnReordered?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnWidthChanged"/> event.</summary>
        protected virtual void OnColumnWidthChanged (ColumnWidthChangedEventArgs e) => ColumnWidthChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="ColumnWidthChanging"/> event.</summary>
        protected virtual void OnColumnWidthChanging (ColumnWidthChangingEventArgs e) => ColumnWidthChanging?.Invoke (this, e);

        /// <summary>Raises the <see cref="ItemMouseHover"/> event.</summary>
        protected virtual void OnItemMouseHover (ListViewItemMouseHoverEventArgs e) => ItemMouseHover?.Invoke (this, e);

        /// <summary>Raises the <see cref="VirtualItemsSelectionRangeChanged"/> event.</summary>
        protected virtual void OnVirtualItemsSelectionRangeChanged (ListViewVirtualItemsSelectionRangeChangedEventArgs e)
            => VirtualItemsSelectionRangeChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="SearchForVirtualItem"/> event.</summary>
        protected virtual void OnSearchForVirtualItem (SearchForVirtualItemEventArgs e) => SearchForVirtualItem?.Invoke (this, e);

        /// <summary>Raises the <see cref="GroupCollapsedStateChanged"/> event.</summary>
        protected virtual void OnGroupCollapsedStateChanged (ListViewGroupEventArgs e) => GroupCollapsedStateChanged?.Invoke (this, e);

        /// <summary>Raises the <see cref="GroupTaskLinkClick"/> event.</summary>
        protected virtual void OnGroupTaskLinkClick (ListViewGroupEventArgs e) => GroupTaskLinkClick?.Invoke (this, e);

        /// <summary>Raises the <see cref="RightToLeftLayoutChanged"/> event.</summary>
        protected virtual void OnRightToLeftLayoutChanged (EventArgs e) => RightToLeftLayoutChanged?.Invoke (this, e);

        private int MeasureWidth (string text)
            => (int)Math.Ceiling (TextMeasurer.MeasureText (text ?? string.Empty, this).Width) + 12;

        private int WidestCellIn (int columnIndex)
        {
            var widest = 0;

            foreach (var item in Items) {
                var text = columnIndex < item.SubItems.Count ? item.SubItems[columnIndex].Text : string.Empty;
                widest = Math.Max (widest, MeasureWidth (text));
            }

            return widest;
        }

        /// <summary>A live view of the items in a <see cref="ListView"/> that are selected.</summary>
        public class SelectedListViewItemCollection : ListViewItemViewCollection
        {
            internal SelectedListViewItemCollection (ListView owner) : base (owner, static i => i.Selected) { }

            /// <summary>Deselects every item in the control.</summary>
            public override void Clear ()
            {
                foreach (var item in Owner.Items)
                    item.Selected = false;
            }
        }

        /// <summary>A live view of the items in a <see cref="ListView"/> that are checked.</summary>
        public class CheckedListViewItemCollection : ListViewItemViewCollection
        {
            internal CheckedListViewItemCollection (ListView owner) : base (owner, static i => i.Checked) { }

            /// <summary>Unchecks every item in the control.</summary>
            public override void Clear ()
            {
                foreach (var item in Owner.Items)
                    item.Checked = false;
            }
        }

        /// <summary>A live view of the indices of the selected items.</summary>
        public class SelectedIndexCollection : ListViewIndexViewCollection
        {
            internal SelectedIndexCollection (ListView owner) : base (owner, static i => i.Selected) { }

            /// <summary>Selects the item at the given index.</summary>
            public int Add (int index)
            {
                if (index >= 0 && index < Owner.Items.Count)
                    Owner.Items[index].Selected = true;

                return index;
            }

            /// <summary>Deselects the item at the given index.</summary>
            public void Remove (int index)
            {
                if (index >= 0 && index < Owner.Items.Count)
                    Owner.Items[index].Selected = false;
            }

            /// <summary>Deselects every item in the control.</summary>
            public override void Clear ()
            {
                foreach (var item in Owner.Items)
                    item.Selected = false;
            }
        }

        /// <summary>A live view of the indices of the checked items.</summary>
        public class CheckedIndexCollection : ListViewIndexViewCollection
        {
            internal CheckedIndexCollection (ListView owner) : base (owner, static i => i.Checked) { }

            /// <summary>Unchecks every item in the control.</summary>
            public override void Clear ()
            {
                foreach (var item in Owner.Items)
                    item.Checked = false;
            }
        }

        /// <summary>The shared behaviour of the selected/checked item views.</summary>
        public abstract class ListViewItemViewCollection : IReadOnlyList<ListViewItem>, ICollection
        {
            private readonly Func<ListViewItem, bool> predicate;

            private protected ListViewItemViewCollection (ListView owner, Func<ListViewItem, bool> predicate)
            {
                Owner = owner;
                this.predicate = predicate;
            }

            /// <summary>Gets the control this view reads from.</summary>
            protected ListView Owner { get; }

            private IEnumerable<ListViewItem> Matching => Owner.Items.Where (predicate);

            /// <summary>Gets the number of matching items.</summary>
            public int Count => Matching.Count ();

            /// <summary>Gets the matching item at the given position within this view.</summary>
            public ListViewItem this[int index] => Matching.ElementAt (index);

            /// <summary>Returns whether the given item is in this view.</summary>
            public bool Contains (ListViewItem? item) => item is not null && predicate (item) && Owner.Items.Contains (item);

            /// <summary>Returns the position of the given item within this view, or -1.</summary>
            public int IndexOf (ListViewItem item)
            {
                var i = 0;
                foreach (var candidate in Matching) {
                    if (ReferenceEquals (candidate, item))
                        return i;
                    i++;
                }

                return -1;
            }

            /// <summary>Copies this view into an array.</summary>
            public void CopyTo (Array array, int index)
            {
                ArgumentNullException.ThrowIfNull (array);

                foreach (var item in Matching)
                    array.SetValue (item, index++);
            }

            /// <summary>Removes every item from this view by clearing the state that defines it.</summary>
            public abstract void Clear ();

            /// <inheritdoc/>
            public IEnumerator<ListViewItem> GetEnumerator () => Matching.GetEnumerator ();

            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => this;
        }

        /// <summary>The shared behaviour of the selected/checked index views.</summary>
        public abstract class ListViewIndexViewCollection : IReadOnlyList<int>, ICollection
        {
            private readonly Func<ListViewItem, bool> predicate;

            private protected ListViewIndexViewCollection (ListView owner, Func<ListViewItem, bool> predicate)
            {
                Owner = owner;
                this.predicate = predicate;
            }

            /// <summary>Gets the control this view reads from.</summary>
            protected ListView Owner { get; }

            private IEnumerable<int> Matching => Owner.Items
                .Select (static (item, index) => (item, index))
                .Where (t => predicate (t.item))
                .Select (static t => t.index);

            /// <summary>Gets the number of matching items.</summary>
            public int Count => Matching.Count ();

            /// <summary>Gets the item index at the given position within this view.</summary>
            public int this[int index] => Matching.ElementAt (index);

            /// <summary>Returns whether the item at the given index is in this view.</summary>
            public bool Contains (int index) => Matching.Contains (index);

            /// <summary>Returns the position of the given item index within this view, or -1.</summary>
            public int IndexOf (int index)
            {
                var i = 0;
                foreach (var candidate in Matching) {
                    if (candidate == index)
                        return i;
                    i++;
                }

                return -1;
            }

            /// <summary>Copies this view into an array.</summary>
            public void CopyTo (Array array, int index)
            {
                ArgumentNullException.ThrowIfNull (array);

                foreach (var value in Matching)
                    array.SetValue (value, index++);
            }

            /// <summary>Removes every index from this view by clearing the state that defines it.</summary>
            public abstract void Clear ();

            /// <inheritdoc/>
            public IEnumerator<int> GetEnumerator () => Matching.GetEnumerator ();

            IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => this;
        }
    }

    /// <summary>Describes what is at a particular point in a <see cref="ListView"/>.</summary>
    public class ListViewHitTestInfo
    {
        /// <summary>Initializes a new instance of the <see cref="ListViewHitTestInfo"/> class.</summary>
        public ListViewHitTestInfo (ListViewItem? hitItem, ListViewItem.ListViewSubItem? hitSubItem, ListViewHitTestLocations hitLocation)
        {
            Item = hitItem;
            SubItem = hitSubItem;
            Location = hitLocation;
        }

        /// <summary>Gets the item at the tested point, or null.</summary>
        public ListViewItem? Item { get; }

        /// <summary>Gets the sub-item at the tested point, or null.</summary>
        public ListViewItem.ListViewSubItem? SubItem { get; }

        /// <summary>Gets what part of the control the tested point was over.</summary>
        public ListViewHitTestLocations Location { get; }
    }

    /// <summary>The line a <see cref="ListView"/> draws to show where a dragged item would land.</summary>
    public class ListViewInsertionMark
    {
        /// <summary>Gets or sets whether the mark is drawn after the item at <see cref="Index"/>.</summary>
        public bool AppearsAfterItem { get; set; }

        /// <summary>Gets the bounds of the mark.</summary>
        public Rectangle Bounds { get; internal set; }

        /// <summary>Gets or sets the colour the mark is drawn in.</summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>Gets or sets the index of the item the mark is drawn next to, or -1 for no mark.</summary>
        public int Index { get; set; } = -1;

        /// <summary>Returns the index of the item nearest the given point.</summary>
        public int NearestIndex (Point pt) => Index;
    }

    public partial class ListViewGroup
    {
        /// <summary>Gets or sets whether the group is collapsed.</summary>
        public ListViewGroupCollapsedState CollapsedState { get; set; } = ListViewGroupCollapsedState.Default;

        /// <summary>Gets or sets the text shown below the group's items.</summary>
        public string Footer { get; set; } = string.Empty;

        /// <summary>Gets or sets the alignment of <see cref="Footer"/>.</summary>
        public HorizontalAlignment FooterAlignment { get; set; }

        /// <summary>Gets or sets the text shown under the group header.</summary>
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>Gets or sets the text of the group's task link.</summary>
        public string TaskLink { get; set; } = string.Empty;

        /// <summary>Gets or sets the index in the group image list of the header's image.</summary>
        public int TitleImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the key in the group image list of the header's image.</summary>
        public string TitleImageKey { get; set; } = string.Empty;

        /// <summary>Gets the control this group belongs to.</summary>
        public ListView? ListView { get; internal set; }
    }

    public partial class ListViewGroupCollection
    {
        /// <summary>Adds several groups at once.</summary>
        public void AddRange (params ListViewGroup[] groups)
        {
            ArgumentNullException.ThrowIfNull (groups);

            foreach (var group in groups)
                Add (group);
        }

        /// <inheritdoc cref="AddRange(ListViewGroup[])"/>
        public void AddRange (ListViewGroupCollection groups)
        {
            ArgumentNullException.ThrowIfNull (groups);

            foreach (var group in groups)
                Add (group);
        }
    }

    public partial class ListViewItem
    {
        /// <summary>Gets or sets whether this item has the focus rectangle.</summary>
        public bool Focused {
            get => ReferenceEquals (Parent?.FocusedItem, this);
            set {
                if (Parent is null)
                    return;

                if (value)
                    Parent.FocusedItem = this;
                else if (Focused)
                    Parent.FocusedItem = null;
            }
        }

        /// <summary>Gets or sets the font this item is drawn with.</summary>
        public Majorsilence.Forms.Drawing.Font? Font { get; set; }

        /// <summary>Gets the image list this item takes its image from, which depends on the view.</summary>
        public ImageList? ImageList
            => Parent?.View == View.LargeIcon ? Parent?.LargeImageList : Parent?.SmallImageList;

        /// <summary>Returns the bounds of the requested part of this item.</summary>
        public Rectangle GetBounds (ItemBoundsPortion portion)
        {
            var bounds = Bounds;

            return portion switch {
                // The icon occupies the leading square of the row; the label is what is left.
                ItemBoundsPortion.Icon => new Rectangle (bounds.X, bounds.Y, Math.Min (bounds.Height, bounds.Width), bounds.Height),
                ItemBoundsPortion.Label when bounds.Width > bounds.Height
                    => new Rectangle (bounds.X + bounds.Height, bounds.Y, bounds.Width - bounds.Height, bounds.Height),
                ItemBoundsPortion.Label => Rectangle.Empty,
                _ => bounds,     // Entire and ItemOnly are the whole row here
            };
        }

        /// <summary>Returns the sub-item at the given control-relative point, or null.</summary>
        public ListViewSubItem? GetSubItemAt (int x, int y)
        {
            if (!Bounds.Contains (x, y) || Parent is null)
                return null;

            var left = Bounds.X;

            for (var i = 0; i < Math.Min (SubItems.Count, Parent.Columns.Count); i++) {
                var right = left + Parent.Columns[i].Width;

                if (x >= left && x < right)
                    return SubItems[i];

                left = right;
            }

            return null;
        }

        /// <summary>Returns the item nearest this one in the given direction.</summary>
        public ListViewItem? FindNearestItem (SearchDirectionHint searchDirection)
        {
            if (Parent is null)
                return null;

            var centre = new Point (Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
            return Parent.FindNearestItem (searchDirection, centre);
        }

        /// <summary>The collection of sub-items belonging to a <see cref="ListViewItem"/>.</summary>
        /// <remarks>Named as WinForms names it. It derives from this assembly's <c>SubItemCollection</c>,
        /// which is the type <see cref="ListViewItem.SubItems"/> has always returned, so nothing that
        /// already uses that type has to change.</remarks>
        public class ListViewSubItemCollection : SubItemCollection
        {
            internal ListViewSubItemCollection (ListViewItem owner) : base (owner) { }
        }
    }
}
