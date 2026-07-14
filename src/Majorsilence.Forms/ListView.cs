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
    public class ListView : Control
    {
        /// <summary>
        /// Initializes a new instance of the ListView class.
        /// </summary>
        public ListView ()
        {
            Items = new ListViewItemCollection (this);
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

        // Lays out the ListViewItems.
        private void LayoutItems ()
        {
            var bounds = PaddedClientRectangle;
            var item_size = LogicalToDeviceUnits (70);
            var item_margin = LogicalToDeviceUnits (6);

            var x = bounds.Left;
            var y = bounds.Top;

            foreach (var item in Items) {
                item.SetBounds (x, y, item_size, item_size);
                x += item_size + item_margin;

                if (x + item_size > bounds.Width) {
                    x = bounds.Left;
                    y += item_size + item_margin;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            base.OnClick (e);

            var clicked_item = Items.FirstOrDefault (tp => tp.Bounds.Contains (e.Location));

            SelectedItem = clicked_item;
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            var clicked_item = Items.FirstOrDefault (tp => tp.Bounds.Contains (e.Location));

            if (clicked_item != null)
                ItemDoubleClicked?.Invoke (this, new EventArgs<ListViewItem> (clicked_item));
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

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

                if (current_item != null)
                    current_item.Selected = false;

                if (value != null)
                    value.Selected = true;

                Invalidate ();

                // The guard above ensures this only fires on an actual selection change (WinForms).
                OnSelectedIndexChanged (EventArgs.Empty);
            }
        }

        /// <summary>Gets all currently selected items.</summary>
        public IEnumerable<ListViewItem> SelectedItems => Items.Where (i => i.Selected);

        /// <summary>Gets the indices of all currently selected items.</summary>
        public IEnumerable<int> SelectedIndices => Items
            .Select ((item, index) => (item, index))
            .Where (t => t.item.Selected)
            .Select (t => t.index);

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

        /// <summary>Gets or sets the sort order for items. Stub in Majorsilence.Forms.</summary>
        public SortOrder Sorting { get; set; } = SortOrder.None;

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
        public ColumnHeaderCollection Columns { get; } = new ColumnHeaderCollection ();

        /// <summary>Gets all items with a checked state.</summary>
        public IEnumerable<ListViewItem> CheckedItems => Items.Where (i => i.Checked);

        /// <summary>Gets the indices of all checked items.</summary>
        public IEnumerable<int> CheckedIndices => Items
            .Select ((item, index) => (item, index))
            .Where (t => t.item.Checked)
            .Select (t => t.index);

        /// <summary>Returns the bounding rectangle of the item at the specified index.</summary>
        public Rectangle GetItemRect (int index) =>
            index >= 0 && index < Items.Count ? Items[index].Bounds : Rectangle.Empty;

        /// <summary>Raised before an item's check state changes.</summary>
        public event EventHandler<ItemCheckEventArgs>? ItemCheck { add { } remove { } }

        /// <summary>Raised when an item's selection state changes.</summary>
        public event EventHandler? ItemSelectionChanged { add { } remove { } }

        /// <summary>Raised when the selected indices change.</summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>Raises the SelectedIndexChanged event.</summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e) => SelectedIndexChanged?.Invoke (this, e);

        /// <summary>Raised when an item is activated (double-clicked or Enter pressed).</summary>
        public event EventHandler? ItemActivate { add { } remove { } }

        /// <summary>Raised when the user begins dragging a list item. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<ItemDragEventArgs>? ItemDrag { add { } remove { } }

        /// <summary>Raised when an item is checked or unchecked.</summary>
        public event EventHandler<ItemCheckedEventArgs>? ItemChecked { add { } remove { } }

        /// <summary>Raised before a label is edited.</summary>
        public event EventHandler<LabelEditEventArgs>? BeforeLabelEdit { add { } remove { } }

        /// <summary>Raised after a label is edited.</summary>
        public event EventHandler<LabelEditEventArgs>? AfterLabelEdit { add { } remove { } }

        /// <summary>Raised when a column header is clicked.</summary>
#pragma warning disable CA1711
        public event ColumnClickEventHandler? ColumnClick { add { } remove { } }
#pragma warning restore CA1711

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
        public void EnsureVisible (int index) => Invalidate ();

        /// <summary>Returns the item at the specified display coordinates, or null if none.</summary>
        public ListViewItem? GetItemAt (int x, int y) => Items.FirstOrDefault (i => i.Bounds.Contains (x, y));

        /// <summary>Returns the first item whose text matches the specified string.</summary>
        public ListViewItem? FindItemWithText (string text) =>
            Items.FirstOrDefault (i => string.Equals (i.Text, text, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns the index of the item at the specified display coordinates, or -1.</summary>
        public int HitTest (int x, int y)
        {
            for (var i = 0; i < Items.Count; i++)
                if (Items[i].Bounds.Contains (x, y)) return i;
            return -1;
        }

        /// <summary>Clears all currently selected items.</summary>
        public void ClearSelection ()
        {
            foreach (var item in Items)
                item.Selected = false;
        }

        /// <summary>Gets or sets whether the state image list uses a compatible image behavior. Stub in Majorsilence.Forms.</summary>
        public bool UseCompatibleStateImageBehavior { get; set; }

        /// <summary>Gets or sets the item that is currently focused.</summary>
        public ListViewItem? FocusedItem { get; set; }

        /// <summary>Gets or sets whether items are arranged in topdown order. Stub in Majorsilence.Forms.</summary>
        public bool TopItem { get; set; }

        /// <summary>Gets the number of items that can be fully displayed vertically.</summary>
        public int CountPerPage => Height > 0 ? Math.Max (1, Height / LogicalToDeviceUnits (70)) : 1;

        /// <summary>Gets the collection of ListViewGroup objects assigned to the control.</summary>
        public ListViewGroupCollection Groups { get; } = new ListViewGroupCollection ();

        /// <summary>Prevents the control from drawing until EndUpdate is called.</summary>
        public new void BeginUpdate () => SuspendLayout ();

        /// <summary>Resumes drawing the control after BeginUpdate.</summary>
        public new void EndUpdate () { ResumeLayout (false); Invalidate (); }

        /// <summary>Gets or sets the IComparer used for sorting list items.</summary>
        public System.Collections.IComparer? ListViewItemSorter { get; set; }

        /// <summary>Sorts the items in the ListView.</summary>
        public void Sort () => Invalidate ();

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
        LargeIcon,
        /// <summary>Items are displayed as small icons with text to the right.</summary>
        SmallIcon,
        /// <summary>Items are displayed in a single column of small icons with text.</summary>
        List,
        /// <summary>Items are displayed with details in columns.</summary>
        Details,
        /// <summary>Items are displayed as large icons with more text.</summary>
        Tile
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
    public class ColumnHeader
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

        /// <inheritdoc/>
        protected override void InsertItem (int index, ColumnHeader item)
        {
            item.Index = index;
            base.InsertItem (index, item);
        }
    }

    /// <summary>Represents a group of items within a ListView.</summary>
    public class ListViewGroup
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
    public class ListViewGroupCollection : System.Collections.ObjectModel.Collection<ListViewGroup>
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
