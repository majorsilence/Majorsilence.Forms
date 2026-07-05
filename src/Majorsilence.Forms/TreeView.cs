using System.Drawing;
using System.Globalization;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TreeView control.
    /// </summary>
    public class TreeView : Control
    {
        private TreeViewDrawMode draw_mode;
        private readonly TreeViewItem root_item;
        private int top_index;
        private TreeViewItem selected_item;
        private bool show_dropdown_glyph = true;
        private bool show_item_images = true;
        private bool virtual_mode;
        private readonly VerticalScrollBar vscrollbar;

        // Reused across paints to avoid per-frame List<> allocation.
        private readonly List<TreeViewItem> _layoutItems = new ();

        private static readonly object s_drawNode = new object ();

        /// <summary>
        /// Initializes a new instance of the TreeView class.
        /// </summary>
        public TreeView ()
        {
            root_item = new TreeViewItem (this) {
                Expanded = true
            };

            selected_item = root_item;

            vscrollbar = Controls.AddImplicitControl (new VerticalScrollBar {
                Minimum = 0,
                Maximum = 0,
                SmallChange = 1,
                LargeChange = 1,
                Visible = false,
                Dock = DockStyle.Right
            });

            vscrollbar.ValueChanged += VerticalScrollBar_ValueChanged;
        }

        /// <summary>
        /// Raised before a node is expanded. Set Cancel=true to prevent expansion.
        /// </summary>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeExpand;

        /// <inheritdoc/>
        public new static readonly TreeViewControlStyle DefaultStyle = new TreeViewControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlLowColor;
                style.Border.Width = 1;

                if (style is TreeViewControlStyle s)
                    s.SelectedItemBackgroundColor = Theme.ControlHighlightLowColor;
            });

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (250, 500);

        /// <summary>
        /// Gets or sets a value indicating who will perform the tree node painting.
        /// </summary>
        public TreeViewDrawMode DrawMode {
            get => draw_mode;
            set {
                if (draw_mode != value) {
                    draw_mode = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Raised when TreeView needs an owner drawn node painted.
        /// </summary>
        public event EventHandler<TreeViewDrawEventArgs>? DrawNode {
            add => Events.AddHandler (s_drawNode, value);
            remove => Events.RemoveHandler (s_drawNode, value);
        }

        internal void EnsureItemVisible (TreeViewItem item)
        {
            // Make sure all parent are expanded so this node is shown
            var parent = item.Parent;

            while (parent != null && parent != root_item) {
                parent.Expand ();
                parent = parent.Parent;
            }

            // If the control hasn't been laid out yet (e.g. SelectedItem set in a constructor),
            // there's no viewport to scroll within. The next layout pass (SetBoundsCore ->
            // UpdateVerticalScrollBar) reconciles the scroll position, so just bail out.
            if (VisibleItemCount <= 0)
                return;

            var all_items = root_item.GetVisibleItems ().Skip (1).ToList ();

            if (all_items.Count <= VisibleItemCount)
                return;

            var index = all_items.IndexOf (item);

            if (index < 0)
                return;

            int target;

            if (index < top_index)
                target = index;
            else if (index >= top_index + VisibleItemCount - 1)
                target = index - (VisibleItemCount - 1);
            else
                return;

            // Make sure the scrollbar's range reflects the current item count, then clamp so we
            // never assign a value outside [Minimum, Maximum] (ScrollBar.Value throws otherwise).
            UpdateVerticalScrollBar ();

            target = Math.Clamp (target, vscrollbar.Minimum, vscrollbar.Maximum);
            top_index = target;
            vscrollbar.Value = target;
        }

        /// <summary>
        /// Finds the index of the next item after startIndex that begins with the specified string. This search is case-insensitive.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage ("Globalization", "CA1309:Use ordinal string comparison", Justification = "This should be culture aware.")]
        private TreeViewItem? FindString (string s, TreeViewItem startItem)
        {
            var all_items = GetVisibleItems ().ToList ();
            var start_index = all_items.IndexOf (startItem);

            if (s is null || all_items.Count == 0)
                return null;

            // We actually look for matches AFTER the start index
            start_index = (start_index == all_items.Count - 1) ? 0 : start_index + 1;
            var current = start_index;

            while (true) {
                var item = all_items[current];

                if (string.Compare (s, 0, item.Text, 0, s.Length, true, CultureInfo.CurrentCulture) == 0)
                    return item;

                current++;

                if (current == all_items.Count)
                    current = 0;

                if (current == start_index)
                    return null;
            }
        }

        /// <summary>
        /// Returns the TreeViewItem at the specified location.
        /// </summary>
        public TreeViewItem? GetItemAtLocation (Point location)
        {
            // Use the already-laid-out list from the most recent LayoutItems() call instead of
            // re-traversing the whole visible tree.
            for (var i = 0; i < _layoutItems.Count; i++)
                if (_layoutItems[i].Bounds.Contains (location)) return _layoutItems[i];

            return null;
        }

        // Enumerates through every visible TreeViewItem. Note items may not be in the currently shown part.
        internal IEnumerable<TreeViewItem> GetVisibleItems (bool skipOffscreen = false) => root_item.GetVisibleItems ().Skip (1 + (skipOffscreen ? top_index : 0));

        /// <summary>
        /// Gets the collection of items contained by this TreeView.
        /// </summary>
        public TreeViewItemCollection Items => root_item.Items;

        /// <summary>
        /// Raised when an item is selected.
        /// </summary>
        public event EventHandler<EventArgs<TreeViewItem>>? ItemSelected;

        /// <summary>WinForms compatibility: raised after an item is selected (alias for ItemSelected).</summary>
        public event EventHandler<TreeViewEventArgs>? AfterSelect;

        /// <summary>WinForms compatibility: raised after an item is expanded.</summary>
        public event EventHandler<TreeViewEventArgs>? AfterExpand;

        /// <summary>WinForms compatibility: raised after an item is collapsed.</summary>
        public event EventHandler<TreeViewEventArgs>? AfterCollapse;

        /// <summary>WinForms compatibility: raised before an item is selected.</summary>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeSelect { add { } remove { } }

        /// <summary>WinForms compatibility: raised before an item is collapsed.</summary>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeCollapse { add { } remove { } }

        /// <summary>WinForms compatibility: raised after an item's check state changes.</summary>
        public event EventHandler<TreeViewEventArgs>? AfterCheck { add { } remove { } }

        /// <summary>WinForms compatibility: raised before an item's check state changes.</summary>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeCheck { add { } remove { } }

        /// <summary>WinForms compatibility: raised after a node label is edited.</summary>
        public event EventHandler<NodeLabelEditEventArgs>? AfterLabelEdit { add { } remove { } }

        /// <summary>WinForms compatibility: raised before a node label is edited.</summary>
        public event EventHandler<NodeLabelEditEventArgs>? BeforeLabelEdit { add { } remove { } }

        /// <summary>WinForms compatibility: raised when the user clicks a node with the mouse.</summary>
        public event EventHandler<TreeNodeMouseClickEventArgs>? NodeMouseClick;

        /// <summary>WinForms compatibility: raised when the user double-clicks a node with the mouse.</summary>
        public event EventHandler<TreeNodeMouseClickEventArgs>? NodeMouseDoubleClick { add { } remove { } }

        /// <summary>WinForms compatibility: raised when the mouse enters a node.</summary>
        public event EventHandler<TreeNodeMouseHoverEventArgs>? NodeMouseHover { add { } remove { } }

        /// <summary>Raised when the user begins dragging a node. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<ItemDragEventArgs>? ItemDrag { add { } remove { } }

        /// <summary>Gets or sets whether check boxes appear next to tree items.</summary>
        public bool CheckBoxes { get; set; }

        /// <summary>Gets or sets whether clicking a tree node selects the full row. Stub in Majorsilence.Forms.</summary>
        public bool FullRowSelect { get; set; }

        /// <summary>Gets or sets whether selections remain highlighted when the control loses focus. Stub in Majorsilence.Forms.</summary>
        public bool HideSelection { get; set; }

        /// <summary>Gets or sets the height of each tree node row in pixels.</summary>
        public int ItemHeight { get; set; } = 20;

        /// <summary>Gets or sets whether in-place label editing is enabled. Stub in Majorsilence.Forms.</summary>
        public bool LabelEdit { get; set; }

        /// <summary>Gets or sets the separator character used in node paths.</summary>
        public string PathSeparator { get; set; } = "\\";

        /// <summary>Gets or sets whether lines are drawn between tree nodes. Stub in Majorsilence.Forms.</summary>
        public bool ShowLines { get; set; } = true;

        /// <summary>Gets or sets whether expand/collapse buttons are shown. Stub in Majorsilence.Forms.</summary>
        public bool ShowPlusMinus { get; set; } = true;

        /// <summary>Gets or sets whether root-level tree lines are drawn. Stub in Majorsilence.Forms.</summary>
        public bool ShowRootLines { get; set; } = true;

        /// <summary>Gets or sets the ImageList for tree item images.</summary>
        public ImageList? ImageList { get; set; }

        /// <summary>Gets or sets the default image index for items.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image index shown for selected items.</summary>
        public int SelectedImageIndex { get; set; } = -1;

        /// <summary>Gets or sets whether nodes are highlighted when the mouse pointer hovers over them. Stub in Majorsilence.Forms.</summary>
        public bool HotTracking { get; set; }

        /// <summary>Gets or sets the ImageList used for state images. Stub in Majorsilence.Forms.</summary>
        public ImageList? StateImageList { get; set; }

        /// <summary>Gets or sets the indentation width in pixels for child items.</summary>
        public int Indent { get; set; } = 19;

        /// <summary>Returns the tree node at the specified client coordinates, or null if none.</summary>
        public TreeViewItem? GetNodeAt (int x, int y) => GetNodeAt (new System.Drawing.Point (x, y));

        /// <summary>Returns the tree node at the specified client point, or null if none.</summary>
        public TreeViewItem? GetNodeAt (System.Drawing.Point pt)
        {
            foreach (var item in GetAllItems ())
                if (GetItemBounds (item).Contains (pt))
                    return item;
            return null;
        }

        private IEnumerable<TreeViewItem> GetAllItems ()
        {
            var stack = new Stack<TreeViewItem> (Items);
            while (stack.Count > 0) {
                var item = stack.Pop ();
                yield return item;
                if (item.IsExpanded)
                    foreach (var child in item.Items)
                        stack.Push (child);
            }
        }

        private System.Drawing.Rectangle GetItemBounds (TreeViewItem item)
        {
            // Approximate — items are stacked vertically
            var all = GetAllItems ().ToList ();
            var index = all.IndexOf (item);
            if (index < 0) return System.Drawing.Rectangle.Empty;
            return new System.Drawing.Rectangle (0, index * ItemHeight, Width, ItemHeight);
        }

        /// <summary>Gets or sets the selected tree node (WinForms compatibility alias for SelectedItem).</summary>
        public TreeViewItem? SelectedNode {
            get => SelectedItem;
            set { if (value is not null) SelectedItem = value; }
        }

        /// <summary>Gets the root tree nodes (WinForms compatibility alias for Items).</summary>
        public TreeViewItemCollection Nodes => Items;

        /// <summary>Gets or sets the first visible node in the tree. Stub in Majorsilence.Forms.</summary>
        public TreeViewItem? TopNode {
            get => Items.FirstOrDefault ();
            set { }
        }

        /// <summary>Gets or sets the object used to sort tree nodes. Stub in Majorsilence.Forms.</summary>
        public System.Collections.IComparer? TreeViewNodeSorter { get; set; }

        /// <summary>Sorts all nodes in the tree using the default string comparison. Stub in Majorsilence.Forms.</summary>
        public void Sort () { }

        /// <summary>Returns the number of tree nodes in the collection, optionally including subnodes.</summary>
        public int GetNodeCount (bool includeSubTrees)
        {
            if (!includeSubTrees) return Items.Count;
            var count = 0;
            CountNodes (Items, ref count);
            return count;
        }

        private static void CountNodes (TreeViewItemCollection items, ref int count)
        {
            count += items.Count;
            foreach (var item in items)
                CountNodes (item.Items, ref count);
        }

        /// <summary>Expands all tree nodes.</summary>
        public void ExpandAll ()
        {
            foreach (var item in Items)
                ExpandRecursive (item);

            Invalidate ();
        }

        /// <summary>Collapses all tree nodes.</summary>
        public void CollapseAll ()
        {
            foreach (var item in Items)
                CollapseRecursive (item);

            Invalidate ();
        }

        private static void ExpandRecursive (TreeViewItem item)
        {
            item.Expand ();

            foreach (var child in item.Items)
                ExpandRecursive (child);
        }

        private static void CollapseRecursive (TreeViewItem item)
        {
            foreach (var child in item.Items)
                CollapseRecursive (child);

            item.Collapse ();
        }

        /// <summary>Gets the item with the specified full path, or null if not found.</summary>
        public TreeViewItem? FindNodeByFullPath (string fullPath)
        {
            foreach (var item in Items) {
                var found = FindNodeByFullPathRecursive (item, fullPath);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static TreeViewItem? FindNodeByFullPathRecursive (TreeViewItem item, string fullPath)
        {
            if (item.FullPath == fullPath)
                return item;

            foreach (var child in item.Items) {
                var found = FindNodeByFullPathRecursive (child, fullPath);

                if (found != null)
                    return found;
            }

            return null;
        }

        // The items laid out by the most recent LayoutItems() call.
        // Exposed internally so the renderer can use it without a separate tree traversal.
        internal IReadOnlyList<TreeViewItem> LayoutedItems => _layoutItems;

        // Runs a layout pass on all visible TreeViewItems.
        // Single tree traversal: simultaneously counts all visible items (for the scrollbar) and
        // collects the items on the current page (for layout and rendering).
        private List<TreeViewItem> LayoutItems ()
        {
            _layoutItems.Clear ();

            int totalVisible = 0;    // all visible nodes excluding root
            foreach (var item in root_item.GetVisibleItems ()) {
                if (totalVisible == 0) { totalVisible++; continue; }  // skip the synthetic root

                // Items below the scroll offset are still counted but not added to the page.
                if (totalVisible > top_index)
                    _layoutItems.Add (item);

                totalVisible++;
            }

            UpdateVerticalScrollBar (totalVisible - 1);  // -1 to exclude root

            var client_rect = ClientRectangle;

            if (vscrollbar.Visible)
                client_rect.Width -= (client_rect.Width - vscrollbar.ScaledLeft + 1);

            StackLayoutEngine.VerticalExpand.Layout (client_rect, _layoutItems.Cast<ILayoutable> ());

            return _layoutItems;
        }

        /// <summary>
        /// Raises the BeforeExpand event. Returns true if expansion should proceed (was not cancelled).
        /// </summary>
        public bool OnBeforeExpand (TreeViewItem node)
        {
            if (node is not TreeNode treeNode)
                return true;
            var e = new TreeViewCancelEventArgs (treeNode, false, TreeViewAction.Expand);
            BeforeExpand?.Invoke (this, e);
            return !e.Cancel;
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            var item = GetItemAtLocation (e.Location);

            // If an item wasn't clicked, let the base run and nothing else
            if (item is null) {
                base.OnClick (e);
                return;
            }

            // If an item with a ContextMenu was right-clicked, show its ContextMenu
            if (e.Button == MouseButtons.Right) {
                if (item.ContextMenu != null) {
                    item.ContextMenu.Show (this, PointToScreen (e.Location));
                    return;
                }

                // Otherwise let the base handle any right-click
                base.OnClick (e);
                return;
            }

            base.OnClick (e);

            var element = item.GetElementAtLocation (e.Location);

            if (element == TreeViewItem.TreeViewItemElement.Glyph) {
                var was_expanded = item.Expanded;
                if (!was_expanded && !OnBeforeExpand (item))
                    return;
                item.Expanded = !item.Expanded;
                RaiseExpandCollapseEvents (item, was_expanded);
            } else {
                SelectedItem = item;
                NodeMouseClick?.Invoke (this, new TreeNodeMouseClickEventArgs (item, e.Button, e.Clicks, e.X, e.Y));
            }
        }

        private void RaiseExpandCollapseEvents (TreeViewItem item, bool wasExpanded)
        {
            // TreeViewEventArgs.Node is TreeNode-typed for WinForms compat; skip for plain items.
            if (item is not TreeNode node)
                return;

            if (node.Expanded && !wasExpanded)
                AfterExpand?.Invoke (this, new TreeViewEventArgs (node, TreeViewAction.Expand));
            else if (!node.Expanded && wasExpanded)
                AfterCollapse?.Invoke (this, new TreeViewEventArgs (node, TreeViewAction.Collapse));
        }

        /// <inheritdoc/>
        protected override void OnDoubleClick (MouseEventArgs e)
        {
            base.OnDoubleClick (e);

            if (!e.Button.HasFlag (MouseButtons.Left))
                return;

            var item = GetItemAtLocation (e.Location);

            if (item is null)
                return;

            var element = item.GetElementAtLocation (e.Location);

            if (element != TreeViewItem.TreeViewItemElement.Glyph) {
                var was_expanded = item.Expanded;
                item.Expanded = !item.Expanded;
                RaiseExpandCollapseEvents (item, was_expanded);
            }
        }

        /// <summary>
        ///  Raises the <see cref='DrawNode'/> event.
        /// </summary>
        protected internal virtual void OnDrawNode (TreeViewDrawEventArgs e) => (Events[s_drawNode] as EventHandler<TreeViewDrawEventArgs>)?.Invoke (this, e);

        /// <summary>
        /// Raises the ItemSelected event.
        /// </summary>
        protected virtual void OnItemSelected (EventArgs<TreeViewItem> e)
        {
            ItemSelected?.Invoke (this, e);

            // TreeViewEventArgs.Node is TreeNode-typed for WinForms compat; skip for plain items.
            if (e.Value is TreeNode node)
                AfterSelect?.Invoke (this, new TreeViewEventArgs (node, TreeViewAction.ByMouse));
        }

        /// <inheritdoc/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            // PERF: Anything using GetVisibleItems () could probably be written more efficiently
            // Down moves down one visible node
            if (e.KeyCode == Keys.Down) {
                var all = GetVisibleItems ().ToList ();
                var index = all.IndexOf (selected_item);

                if (index + 1 < all.Count)
                    SelectedItem = all[index + 1];

                e.Handled = true;
                return;
            }

            // Up moves up one visible node
            if (e.KeyCode == Keys.Up) {
                var all = GetVisibleItems ().ToList ();
                var index = all.IndexOf (selected_item);

                if (index > 0)
                    SelectedItem = all[index - 1];

                e.Handled = true;
                return;
            }

            // End moves to last expanded node
            if (e.KeyCode == Keys.End) {
                var all = GetVisibleItems ().ToList ();

                if (all.Count == 0)
                    return;

                SelectedItem = all.Last ();

                e.Handled = true;
                return;
            }

            // Home moves to first expanded node
            if (e.KeyCode == Keys.Home) {
                var all = GetVisibleItems ().ToList ();

                if (all.Count == 0)
                    return;

                SelectedItem = all.First ();

                e.Handled = true;
                return;
            }

            // PgDown moves down by amount of visible nodes
            if (e.KeyCode == Keys.PageDown) {
                var all = GetVisibleItems ().ToList ();

                if (all.Count == 0)
                    return;

                var index = all.IndexOf (selected_item);
                var new_index = Math.Min (index + VisibleItemCount - 1, all.Count - 1);

                SelectedItem = all[new_index];

                e.Handled = true;
                return;
            }

            // PgUp moves up by amount of visible nodes
            if (e.KeyCode == Keys.PageUp) {
                var all = GetVisibleItems ().ToList ();

                if (all.Count == 0)
                    return;

                var index = all.IndexOf (selected_item);
                var new_index = Math.Max (index - (VisibleItemCount - 1), 0);

                SelectedItem = all[new_index];

                e.Handled = true;
                return;
            }

            // Right when HasChildren expands node (if needed) and selects first child
            if (e.KeyCode == Keys.Right) {
                selected_item.Expand ();

                if (selected_item.HasChildren)
                    SelectedItem = selected_item.Items.First ();

                e.Handled = true;
                return;
            }

            // Left with expanded children collapses children
            if (e.KeyCode == Keys.Left && selected_item.HasChildren && selected_item.Expanded) {
                selected_item.Collapse ();
                e.Handled = true;
                return;
            }

            // Left with no children or collapsed selects parent
            if (e.KeyCode == Keys.Left && !selected_item.Expanded) {
                if (selected_item.Parent is TreeViewItem parent && parent != root_item)
                    SelectedItem = parent;

                e.Handled = true;
                return;
            }

            // First letter toggles between all expanded nodes
            if (char.IsLetterOrDigit ((char)e.KeyCode)) {
                var item = FindString (((char)e.KeyCode).ToString (), selected_item);

                if (item != null) {
                    SelectedItem = item;
                    e.Handled = true;
                    return;
                }
            }

            // TODO: If checkboxes, space toggles checkbox
            base.OnKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel (MouseEventArgs e)
        {
            base.OnMouseWheel (e);

            if (vscrollbar.Visible)
                vscrollbar.RaiseMouseWheel (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            LayoutItems ();

            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        // The scaled height of each TreeViewItem.
        internal int ScaledItemHeight => (root_item.Items.FirstOrDefault () ?? root_item).GetPreferredSize (Size.Empty).Height;

        /// <summary>
        /// Gets or sets the currently selected TreeViewItem.
        /// </summary>
        public TreeViewItem SelectedItem {
            get => selected_item;
            set {
                // Don't allow user to unselect items
                if (value is null)
                    return;

                var current_selection = selected_item;

                if (current_selection == value)
                    return;

                selected_item = value;

                EnsureItemVisible (value);
                Invalidate ();

                OnItemSelected (new EventArgs<TreeViewItem> (value));
            }
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            base.SetBoundsCore (x, y, width, height, specified);

            UpdateVerticalScrollBar ();
        }

        /// <summary>
        /// Gets or sets a value indicating the drop down glyph should be shown.
        /// </summary>
        public bool ShowDropdownGlyph {
            get => show_dropdown_glyph;
            set {
                if (show_dropdown_glyph != value) {
                    show_dropdown_glyph = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating item images should be shown.
        /// </summary>
        public bool ShowItemImages {
            get => show_item_images;
            set {
                if (show_item_images != value) {
                    show_item_images = value;
                    Invalidate ();
                }
            }
        }

        /// <summary>Prevents the control from drawing until EndUpdate is called.</summary>
        public new void BeginUpdate () => SuspendLayout ();

        /// <summary>Resumes drawing the control after BeginUpdate.</summary>
        public new void EndUpdate () { ResumeLayout (false); Invalidate (); }

        /// <inheritdoc/>
        public override TreeViewControlStyle Style { get; } = new TreeViewControlStyle (DefaultStyle);

        // Determines scrollbar visibility and values using a pre-computed visible child count.
        // Called from LayoutItems() after the single-pass traversal to avoid a second traversal.
        private void UpdateVerticalScrollBar (int childCount)
        {
            if (Items.Count == 0 || ScaledItemHeight * childCount <= ScaledHeight) {
                vscrollbar.Visible = false;
                top_index = 0;
                return;
            }

            if (!vscrollbar.Visible)
                vscrollbar.Value = 0;

            vscrollbar.Visible = true;
            vscrollbar.Maximum = childCount - VisibleItemCount;
            vscrollbar.LargeChange = Math.Max (0, VisibleItemCount);
        }

        // Determines scrollbar visibility and scrollbar values.
        // Used by SetBoundsCore (resize) — traverses the tree to get the count.
        private void UpdateVerticalScrollBar ()
            => UpdateVerticalScrollBar (root_item.GetVisibleChildrenCount ());

        // Handles scrollbar scrolling.
        private void VerticalScrollBar_ValueChanged (object? sender, EventArgs e)
        {
            top_index = vscrollbar.Value;

            Invalidate ();
        }

        /// <summary>
        /// Gets or sets a value indicating if TreeViewItem nodes will be resolved when expanded.
        /// </summary>
        public bool VirtualMode {
            get => virtual_mode;
            set {
                if (virtual_mode != value) {
                    virtual_mode = value;
                    Invalidate ();
                }
            }
        }

        // The number of items that can be shown with the current height.
        private int VisibleItemCount => ScaledHeight / ScaledItemHeight;

        /// <inheritdoc/>
        public class TreeViewControlStyle : ControlStyle
        {
            /// <inheritdoc/>
            public TreeViewControlStyle (ControlStyle? parent, Action<ControlStyle> setDefaults) : base (parent, setDefaults)
            {
            }

            /// <inheritdoc/>
            public TreeViewControlStyle (ControlStyle parent) : base (parent)
            {
            }

            /// <summary>
            /// Gets or sets the background color of the currently selected item.
            /// </summary>
            public SKColor? SelectedItemBackgroundColor { get; set; }

            /// <summary>
            /// Gets the computed selected item background color.
            /// </summary>
            public SKColor GetSelectedItemBackgroundColor () => SelectedItemBackgroundColor ?? (_parent as TreeViewControlStyle)?.GetSelectedItemBackgroundColor () ?? Theme.ControlHighlightLowColor;
        }
    }
}
