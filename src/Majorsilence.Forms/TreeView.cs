using System.Drawing;
using System.Globalization;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TreeView control.
    /// </summary>
    public partial class TreeView : Control
    {
        private TreeViewDrawMode draw_mode;
        private readonly TreeNode root_item;
        private int top_index;
        private TreeNode selected_item;
        private bool show_dropdown_glyph = true;
        private bool show_item_images = true;
        private bool virtual_mode;
        private readonly VerticalScrollBar vscrollbar;

        // Reused across paints to avoid per-frame List<> allocation.
        private readonly List<TreeNode> _layoutItems = new ();

        private static readonly object s_drawNode = new object ();

        /// <summary>
        /// Initializes a new instance of the TreeView class.
        /// </summary>
        public TreeView ()
        {
            root_item = new TreeNode (this) {
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
        /// <remarks>
        /// Typed with WinForms' own <see cref="DrawTreeNodeEventHandler"/>. It used to be
        /// <c>EventHandler&lt;TreeViewDrawEventArgs&gt;</c> -- this library's own args type -- so an
        /// owner-draw handler written the WinForms way could not be attached at all.
        /// </remarks>
        public event DrawTreeNodeEventHandler? DrawNode {
            add => Events.AddHandler (s_drawNode, value);
            remove => Events.RemoveHandler (s_drawNode, value);
        }

        internal void EnsureItemVisible (TreeNode item)
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

            target = MathCompat.Clamp (target, vscrollbar.Minimum, vscrollbar.Maximum);
            top_index = target;
            _scrollOffsetPx = 0;
            vscrollbar.Value = target;
        }

        /// <summary>
        /// Finds the index of the next item after startIndex that begins with the specified string. This search is case-insensitive.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage ("Globalization", "CA1309:Use ordinal string comparison", Justification = "This should be culture aware.")]
        private TreeNode? FindString (string s, TreeNode startItem)
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
        /// Returns the TreeNode at the specified location.
        /// </summary>
        public TreeNode? GetItemAtLocation (Point location)
        {
            // Mouse coordinates are logical (MouseEventArgs, like Bounds), while the laid-out node bounds
            // come from LayoutItems() against ClientRectangle and the items' device-pixel GetPreferredSize
            // and are therefore in device pixels. Comparing the two directly picks the node at index x scale
            // -- on a phone at scale ~2.6 a tap on the second row selects one two or three rows down -- so
            // the point is converted before it is tested. Mirrors ListBox.GetIndexAtLocation.
            var device = new Point (LogicalToDeviceUnits (location.X), LogicalToDeviceUnits (location.Y));

            // Use the already-laid-out list from the most recent LayoutItems() call instead of
            // re-traversing the whole visible tree.
            for (var i = 0; i < _layoutItems.Count; i++)
                if (_layoutItems[i].Bounds.Contains (device)) return _layoutItems[i];

            return null;
        }

        // Enumerates through every visible TreeNode. Note items may not be in the currently shown part.
        internal IEnumerable<TreeNode> GetVisibleItems (bool skipOffscreen = false) => root_item.GetVisibleItems ().Skip (1 + (skipOffscreen ? top_index : 0));

        /// <summary>
        /// Gets the collection of items contained by this TreeView.
        /// </summary>
        public TreeViewItemCollection Items => root_item.Items;

        /// <summary>
        /// Raised when an item is selected.
        /// </summary>
        public event EventHandler<EventArgs<TreeNode>>? ItemSelected;

        /// <summary>WinForms compatibility: raised after an item is selected (alias for ItemSelected).</summary>
        /// <remarks>Typed with WinForms' <see cref="TreeViewEventHandler"/> rather than
        /// <c>EventHandler&lt;TreeViewEventArgs&gt;</c>: the delegate types are not interchangeable, so
        /// code forwarding this event on (<c>add =&gt; tree.AfterSelect += value;</c> with a
        /// <c>TreeViewEventHandler</c> parameter) did not compile.</remarks>
        public event TreeViewEventHandler? AfterSelect;

        /// <summary>WinForms compatibility: raised after an item is expanded.</summary>
        public event EventHandler<TreeViewEventArgs>? AfterExpand;

        /// <summary>WinForms compatibility: raised after an item is collapsed.</summary>
        public event EventHandler<TreeViewEventArgs>? AfterCollapse;

        /// <summary>Raised before an item is selected; set <c>Cancel</c> to prevent it.</summary>
        /// <remarks>Real as of W5.9 (<c>LST-23</c>). These four were declared
        /// <c>add { } remove { }</c>, so the "unsaved changes, really switch?" prompt every editor
        /// tree hangs off <see cref="BeforeSelect"/> silently never ran.</remarks>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeSelect;

        /// <summary>Raises the <see cref="BeforeSelect"/> event, returning false when cancelled.</summary>
        protected virtual bool OnBeforeSelect (TreeViewCancelEventArgs e)
        {
            BeforeSelect?.Invoke (this, e);

            return !e.Cancel;
        }

        /// <summary>Raised before an item is collapsed; set <c>Cancel</c> to prevent it.</summary>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeCollapse;

        /// <summary>Raises the <see cref="BeforeCollapse"/> event, returning false when cancelled.</summary>
        protected virtual bool OnBeforeCollapse (TreeViewCancelEventArgs e)
        {
            BeforeCollapse?.Invoke (this, e);

            return !e.Cancel;
        }

        /// <summary>Raised after an item's check state changes.</summary>
        public event EventHandler<TreeViewEventArgs>? AfterCheck;

        /// <summary>Raises the <see cref="AfterCheck"/> event.</summary>
        protected virtual void OnAfterCheck (TreeViewEventArgs e) => AfterCheck?.Invoke (this, e);

        /// <summary>Raised before an item's check state changes; set <c>Cancel</c> to prevent it.</summary>
        public event EventHandler<TreeViewCancelEventArgs>? BeforeCheck;

        /// <summary>Raises the <see cref="BeforeCheck"/> event, returning false when cancelled.</summary>
        protected virtual bool OnBeforeCheck (TreeViewCancelEventArgs e)
        {
            BeforeCheck?.Invoke (this, e);

            return !e.Cancel;
        }

        // Called by TreeNode.Checked's setter: asks first, then reports, and answers whether the
        // change may proceed (LST-24).
        internal bool RaiseBeforeCheck (TreeNode node, TreeViewAction action)
            => OnBeforeCheck (new TreeViewCancelEventArgs (node, false, action));

        internal void RaiseAfterCheck (TreeNode node, TreeViewAction action)
        {
            OnAfterCheck (new TreeViewEventArgs (node, action));
            Invalidate ();
        }

        /// <summary>WinForms compatibility: raised after a node label is edited.</summary>
        public event EventHandler<NodeLabelEditEventArgs>? AfterLabelEdit { add { } remove { } }

        /// <summary>WinForms compatibility: raised before a node label is edited.</summary>
        public event EventHandler<NodeLabelEditEventArgs>? BeforeLabelEdit { add { } remove { } }

        /// <summary>WinForms compatibility: raised when the user clicks a node with the mouse.</summary>
        public event TreeNodeMouseClickEventHandler? NodeMouseClick;

        /// <summary>WinForms compatibility: raised when the user double-clicks a node with the mouse.</summary>
        public event TreeNodeMouseClickEventHandler? NodeMouseDoubleClick;

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
        /// <remarks>Drives the row height when set to a positive value (<c>LST-26</c>): it was stored
        /// and the renderer measured every row from the node's own preferred size, so taller rows for
        /// touch were silently ignored -- and <c>VisibleCount</c> divided by this while the drawing
        /// divided by the measured height, so the two disagreed.</remarks>
        public int ItemHeight { get; set; } = 20;

        /// <summary>Gets or sets whether in-place label editing is enabled. Stub in Majorsilence.Forms.</summary>
        public bool LabelEdit { get; set; }

        /// <summary>Gets or sets the separator character used in node paths.</summary>
        public string PathSeparator { get; set; } = "\\";

        /// <summary>Gets or sets whether lines are drawn between tree nodes. Stub in Majorsilence.Forms.</summary>
        public bool ShowLines { get; set; } = true;

        /// <summary>Gets or sets whether expand/collapse buttons are shown. Stub in Majorsilence.Forms.</summary>
        /// <remarks>The same knob as <see cref="ShowDropdownGlyph"/>, which is this library's own name
        /// for it -- they were separate properties for one piece of state, so a designer setting the
        /// WinForms one changed nothing (<c>LST-26</c>).</remarks>
        public bool ShowPlusMinus {
            get => ShowDropdownGlyph;
            set => ShowDropdownGlyph = value;
        }

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
        public TreeNode? GetNodeAt (int x, int y) => GetNodeAt (new System.Drawing.Point (x, y));

        /// <summary>Returns the tree node at the specified client point, or null if none.</summary>
        /// <remarks>
        /// The same answer the control itself uses (finding <c>LST-21</c>). It used to walk a
        /// <c>Stack</c>-based traversal that yielded siblings in REVERSE order and compare against
        /// rectangles synthesised from the stored <see cref="ItemHeight"/>, ignoring the scroll
        /// position and the client origin -- so <c>tree.GetNodeAt (e.X, e.Y)</c> in a MouseDown
        /// handler (the canonical right-click-select and drag-drop pattern) returned a different node
        /// from the one the tree had just selected on that very click.
        /// </remarks>
        public TreeNode? GetNodeAt (System.Drawing.Point pt)
        {
            // A caller can hit-test before the first paint, when the laid-out list is still empty.
            if (_layoutItems.Count == 0)
                LayoutItems ();

            return GetItemAtLocation (pt);
        }

        /// <summary>Gets or sets the selected tree node, or null when nothing is selected.</summary>
        /// <remarks>
        /// Null-correct as of W5.9 (finding <c>LST-05</c>, P0). This used to return
        /// <see cref="SelectedItem"/> unfiltered, and that is seeded with the hidden synthetic root --
        /// so with nothing selected it handed back a non-null node whose <c>Text</c> was <c>""</c> and
        /// whose <c>Nodes</c> were the tree's own top-level nodes. Every
        /// <c>if (tree.SelectedNode == null) return;</c> guard failed to fire, callers read
        /// <c>SelectedNode.Tag</c> (null) and carried on, and <c>SelectedNode.Nodes.Add (...)</c> added
        /// a top-level node. Assigning null was ignored outright, so the standard way to clear a
        /// selection did nothing.
        /// </remarks>
        public TreeNode? SelectedNode {
            get => selected_item == root_item || selected_item.TreeView != this ? null : selected_item;
            set {
                if (value is null) {
                    ClearSelectedNode ();
                    return;
                }

                SelectedItem = value;
            }
        }

        // Back to the synthetic root, which is this control's representation of "nothing selected".
        // No AfterSelect: upstream's TVN_SELCHANGED with a null handle skips OnAfterSelect too.
        internal void ClearSelectedNode ()
        {
            if (selected_item == root_item)
                return;

            selected_item = root_item;
            Invalidate ();
        }

        /// <summary>Gets the root tree nodes (WinForms compatibility alias for Items).</summary>
        public TreeViewItemCollection Nodes => Items;

        /// <summary>Gets or sets the first visible node in the tree. Stub in Majorsilence.Forms.</summary>
        public TreeNode? TopNode {
            get => Items.FirstOrDefault ();
            set { }
        }

        /// <summary>Gets or sets the object used to sort tree nodes. Stub in Majorsilence.Forms.</summary>
        public System.Collections.IComparer? TreeViewNodeSorter {
            get => node_sorter;
            set {
                if (ReferenceEquals (node_sorter, value))
                    return;

                node_sorter = value;
                Sort ();
            }
        }

        private System.Collections.IComparer? node_sorter;

        /// <summary>Sorts all nodes in the tree using the default string comparison. Stub in Majorsilence.Forms.</summary>
        /// <summary>Sorts every level of the tree, by <see cref="TreeViewNodeSorter"/> if one is set,
        /// else by node text.</summary>
        /// <remarks>
        /// Real as of W5.9 (<c>LST-11</c>). It was an empty method and <see cref="Sorted"/> was a
        /// stored flag, so a folder or category tree appeared in load order however the designer had
        /// set it up, and the canonical <c>TreeViewNodeSorter = comparer; Sort ();</c> pair did
        /// nothing.
        /// </remarks>
        public void Sort () => SortNodes (root_item);

        private void SortNodes (TreeNode parent)
        {
            var comparer = node_sorter ?? (Sorted ? TextComparer.Instance : null);

            if (comparer is null)
                return;

            SortLevel (parent, comparer);

            foreach (var child in parent.Items)
                SortNodes (child);

            Invalidate ();
        }

        // Re-inserting rather than sorting in place: the collection maintains each node's Parent as
        // items move, and OrderBy is a stable sort where List.Sort is not -- two nodes comparing equal
        // must not swap on every re-sort.
        private static void SortLevel (TreeNode parent, System.Collections.IComparer comparer)
        {
            if (parent.Items.Count < 2)
                return;

            var ordered = parent.Items.OrderBy (n => n, new ComparerAdapter (comparer)).ToList ();

            // Remove-then-insert rather than a Move the collection does not have: RemoveItem clears
            // each node's Parent and InsertItem restores it, so the nodes stay correctly owned.
            for (var i = 0; i < ordered.Count; i++) {
                var current = parent.Items.IndexOf (ordered[i]);

                if (current == i)
                    continue;

                parent.Items.RemoveAt (current);
                parent.Items.Insert (i, ordered[i]);
            }
        }

        private sealed class ComparerAdapter : IComparer<TreeNode>
        {
            private readonly System.Collections.IComparer inner;

            internal ComparerAdapter (System.Collections.IComparer inner) => this.inner = inner;

            public int Compare (TreeNode? x, TreeNode? y) => inner.Compare (x, y);
        }

        private sealed class TextComparer : System.Collections.IComparer
        {
            internal static readonly TextComparer Instance = new TextComparer ();

            public int Compare (object? x, object? y)
                => string.Compare ((x as TreeNode)?.Text, (y as TreeNode)?.Text, StringComparison.CurrentCulture);
        }

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

        private static void ExpandRecursive (TreeNode item)
        {
            item.Expand ();

            foreach (var child in item.Items)
                ExpandRecursive (child);
        }

        private static void CollapseRecursive (TreeNode item)
        {
            foreach (var child in item.Items)
                CollapseRecursive (child);

            item.Collapse ();
        }

        /// <summary>Gets the item with the specified full path, or null if not found.</summary>
        public TreeNode? FindNodeByFullPath (string fullPath)
        {
            foreach (var item in Items) {
                var found = FindNodeByFullPathRecursive (item, fullPath);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static TreeNode? FindNodeByFullPathRecursive (TreeNode item, string fullPath)
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
        internal IReadOnlyList<TreeNode> LayoutedItems => _layoutItems;

        // Runs a layout pass on all visible TreeViewItems.
        // Single tree traversal: simultaneously counts all visible items (for the scrollbar) and
        // collects the items on the current page (for layout and rendering).
        // Internal, not private: GetNodeAt has to be able to lay out before the first paint, and a
        // test asserting node bounds needs the same.
        internal List<TreeNode> LayoutItems ()
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

            // Push the whole stack up by the sub-row scroll offset; item[top_index] is then partly
            // clipped at the top and one extra row peeks in at the bottom (drawn by TreeViewRenderer).
            client_rect.Y -= (int) System.Math.Round (_scrollOffsetPx);

            StackLayoutEngine.VerticalExpand.Layout (client_rect, _layoutItems.Cast<ILayoutable> ());

            return _layoutItems;
        }

        // Called from TreeNode.Collapse, so a programmatic collapse is announced like a clicked one.
        internal bool RaiseBeforeCollapse (TreeNode node)
            => OnBeforeCollapse (new TreeViewCancelEventArgs (node, false, TreeViewAction.Collapse));

        internal void RaiseAfterExpandCollapse (TreeNode node, bool expanded)
        {
            if (expanded)
                AfterExpand?.Invoke (this, new TreeViewEventArgs (node, TreeViewAction.Expand));
            else
                AfterCollapse?.Invoke (this, new TreeViewEventArgs (node, TreeViewAction.Collapse));
        }

        /// <summary>
        /// Raises the <see cref="BeforeExpand"/> event.
        /// </summary>
        /// <param name="node">The node about to expand.</param>
        /// <returns><see langword="true"/> if expansion should proceed; <see langword="false"/> if a handler cancelled it.</returns>
        public bool OnBeforeExpand (TreeNode node)
        {
            if (node is not TreeNode treeNode)
                return true;
            var e = new TreeViewCancelEventArgs (treeNode, false, TreeViewAction.Expand);
            BeforeExpand?.Invoke (this, e);
            return !e.Cancel;
        }

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            var item = GetItemAtLocation (e.Location);

            // If an item wasn't clicked, let the base run and nothing else
            if (item is null) {
                base.OnMouseClick (e);
                return;
            }

            // Any button reports the click, before the context-menu shortcut takes over: the standard
            // `NodeMouseClick += (s, e) => { if (e.Button == Right) { tree.SelectedNode = e.Node;
            // menu.Show (...); } }` never ran, because the right button returned above the only raise
            // (finding LST-22). Right-click deliberately does not change the selection -- upstream's
            // does not either -- so the handler above is what decides whether it should.
            if (e.Button == MouseButtons.Right) {
                OnNodeMouseClick (new TreeNodeMouseClickEventArgs (item, e.Button, e.Clicks, e.X, e.Y));

                if (item.ContextMenu != null) {
                    item.ContextMenu.Show (this, e.Location);   // client coordinates now: see TSM-03
                    return;
                }

                // Otherwise let the base handle any right-click
                base.OnMouseClick (e);
                return;
            }

            base.OnMouseClick (e);

            // The check box is its own hit target, ahead of the glyph and the label: clicking it
            // toggles and does not move the selection (LST-24).
            if (CheckBoxes) {
                var device = new Point (LogicalToDeviceUnits (e.Location.X), LogicalToDeviceUnits (e.Location.Y));

                if (CheckBounds (item).Contains (device)) {
                    item.SetChecked (!item.Checked, TreeViewAction.ByMouse);
                    return;
                }
            }

            var element = item.GetElementAtLocation (e.Location);

            if (element == TreeNode.TreeViewItemElement.Glyph) {
                var was_expanded = item.Expanded;
                if (!was_expanded && !OnBeforeExpand (item))
                    return;
                item.Expanded = !item.Expanded;
                RaiseExpandCollapseEvents (item, was_expanded);
            } else {
                SelectItem (item, TreeViewAction.ByMouse);
                OnNodeMouseClick (new TreeNodeMouseClickEventArgs (item, e.Button, e.Clicks, e.X, e.Y));
            }
        }

        /// <summary>Raises the <see cref="NodeMouseClick"/> event.</summary>
        protected virtual void OnNodeMouseClick (TreeNodeMouseClickEventArgs e) => NodeMouseClick?.Invoke (this, e);

        /// <summary>The width the check box column takes, in device pixels; zero when hidden.</summary>
        internal int ScaledCheckWidth => CheckBoxes ? LogicalToDeviceUnits (CheckGlyphSize + 4) : 0;

        private const int CheckGlyphSize = 13;

        /// <summary>The check box rectangle for a node, in device pixels.</summary>
        internal System.Drawing.Rectangle CheckBounds (TreeNode node)
        {
            if (!CheckBoxes)
                return System.Drawing.Rectangle.Empty;

            var size = LogicalToDeviceUnits (CheckGlyphSize);
            var left = CheckColumnLeft (node);

            return new System.Drawing.Rectangle (left, node.Bounds.Top + System.Math.Max (0, (node.Bounds.Height - size) / 2), size, size);
        }

        // The check box sits after the indent and the glyph, before the image -- which is where
        // upstream's state image goes.
        internal int CheckColumnLeft (TreeNode node)
        {
            var indent = node.Bounds.Left + node.IndentLevel * LogicalToDeviceUnits (Indent) + 2;

            return ShowPlusMinus ? indent + LogicalToDeviceUnits (CheckGlyphSize) : indent;
        }

        private void RaiseExpandCollapseEvents (TreeNode item, bool wasExpanded)
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

            // Fire for every double-clicked node, independent of the expand/collapse glyph branch below
            // (mirrors the NodeMouseClick raise in OnClick).
            NodeMouseDoubleClick?.Invoke (this, new TreeNodeMouseClickEventArgs (item, e.Button, e.Clicks, e.X, e.Y));

            var element = item.GetElementAtLocation (e.Location);

            if (element != TreeNode.TreeViewItemElement.Glyph) {
                var was_expanded = item.Expanded;
                item.Expanded = !item.Expanded;
                RaiseExpandCollapseEvents (item, was_expanded);
            }
        }

        /// <summary>
        ///  Raises the <see cref='DrawNode'/> event from the renderer's paint pass.
        /// </summary>
        /// <remarks>
        /// Takes this library's <see cref="TreeViewDrawEventArgs"/> (which carries the Skia canvas the
        /// renderer is already painting into) and translates it into the WinForms-shaped
        /// <see cref="DrawTreeNodeEventArgs"/> that <see cref="OnDrawNode(DrawTreeNodeEventArgs)"/> -- the
        /// actual WinForms-shaped override point -- takes. <c>DrawDefault</c> is copied back afterwards,
        /// so a handler that sets it still suppresses the default painting -- that flag is the entire
        /// point of the event, and losing it would make every owner-draw handler double-paint. Named
        /// distinctly from <c>OnDrawNode</c> itself (which upstream WinForms declares as
        /// <c>protected virtual void OnDrawNode(DrawTreeNodeEventArgs e)</c>) so a ported control's own
        /// override of that real hook is reachable, rather than being shadowed by a same-named method
        /// taking this library's internal, Skia-carrying args type.
        /// </remarks>
        protected internal virtual void RaiseDrawNode (TreeViewDrawEventArgs e)
        {
            Guard.ThrowIfNull (e);

            var state = TreeNodeStates.Default;

            if (e.Item == SelectedItem)
                state |= TreeNodeStates.Selected;

            if (Focused && e.Item == SelectedItem)
                state |= TreeNodeStates.Focused;

            if (!Enabled)
                state |= TreeNodeStates.Grayed;

            var args = new DrawTreeNodeEventArgs (e.Graphics, e.Item, e.Item.Bounds, state) {
                DrawDefault = e.DrawDefault,
                TreeView = this,
                Canvas = e.Canvas,
                Scaling = e.Scaling,
            };

            OnDrawNode (args);

            e.DrawDefault = args.DrawDefault;
        }

        /// <summary>Raises the <see cref="DrawNode"/> event. The real WinForms owner-draw override point.</summary>
        protected virtual void OnDrawNode (DrawTreeNodeEventArgs e)
        {
            Guard.ThrowIfNull (e);

            if (Events[s_drawNode] is DrawTreeNodeEventHandler handler)
                handler (this, e);
        }

        /// <summary>
        /// Raises the ItemSelected event.
        /// </summary>
        protected virtual void OnItemSelected (EventArgs<TreeNode> e) => OnItemSelected (e, TreeViewAction.Unknown);

        // The action-carrying overload. Kept separate so the public virtual above stays the shape a
        // derived control may already override.
        private void OnItemSelected (EventArgs<TreeNode> e, TreeViewAction action)
        {
            ItemSelected?.Invoke (this, e);

            // TreeViewEventArgs.Node is TreeNode-typed for WinForms compat; skip for plain items.
            if (e.Value is TreeNode node)
                AfterSelect?.Invoke (this, new TreeViewEventArgs (node, action));
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
                    SelectItem (all[index + 1], TreeViewAction.ByKeyboard);

                e.Handled = true;
                return;
            }

            // Up moves up one visible node
            if (e.KeyCode == Keys.Up) {
                var all = GetVisibleItems ().ToList ();
                var index = all.IndexOf (selected_item);

                if (index > 0)
                    SelectItem (all[index - 1], TreeViewAction.ByKeyboard);

                e.Handled = true;
                return;
            }

            // End moves to last expanded node
            if (e.KeyCode == Keys.End) {
                var all = GetVisibleItems ().ToList ();

                if (all.Count == 0)
                    return;

                SelectItem (all.Last (), TreeViewAction.ByKeyboard);

                e.Handled = true;
                return;
            }

            // Home moves to first expanded node
            if (e.KeyCode == Keys.Home) {
                var all = GetVisibleItems ().ToList ();

                if (all.Count == 0)
                    return;

                SelectItem (all.First (), TreeViewAction.ByKeyboard);

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

                SelectItem (all[new_index], TreeViewAction.ByKeyboard);

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

                SelectItem (all[new_index], TreeViewAction.ByKeyboard);

                e.Handled = true;
                return;
            }

            // Right when HasChildren expands node (if needed) and selects first child
            if (e.KeyCode == Keys.Right) {
                selected_item.Expand ();

                if (selected_item.HasChildren)
                    SelectItem (selected_item.Items.First (), TreeViewAction.ByKeyboard);

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
                if (selected_item.Parent is TreeNode parent && parent != root_item)
                    SelectItem (parent, TreeViewAction.ByKeyboard);

                e.Handled = true;
                return;
            }

            // First letter toggles between all expanded nodes
            if (char.IsLetterOrDigit ((char)e.KeyCode)) {
                var item = FindString (((char)e.KeyCode).ToString (), selected_item);

                if (item != null) {
                    SelectItem (item, TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    return;
                }
            }

            // Space toggles the selected node's check box, as upstream does (LST-24).
            if (e.KeyCode == Keys.Space && CheckBoxes && SelectedNode is { } selected) {
                selected.SetChecked (!selected.Checked, TreeViewAction.ByKeyboard);
                e.Handled = true;
                return;
            }

            base.OnKeyDown (e);
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

        // See ListBox's identical pair for the full rationale: Invalidate() is what makes NeedsPaint
        // bubble up through ancestors so the back-buffer we already patched in TryFastScrollBlit
        // actually gets re-blitted into the window, but we don't want the *content* re-rendered on
        // top of what we just shifted. _skipNextRepaint tells OnPaint/OnPaintBackground to no-op for
        // exactly the one repaint that our own fast-blit Invalidate() triggers; _invalidatingForFastScroll
        // is how OnInvalidated tells an unrelated Invalidate() (a real content change) apart from that.
        private bool _skipNextRepaint;
        private bool _invalidatingForFastScroll;

        /// <summary>
        /// Pans the visible range on a touch drag/flick, pixel by pixel. TreeView owns its own
        /// vertical scrollbar rather than deriving from <see cref="ScrollableControl"/>, so the
        /// neutral scroll-gesture event (drag, plus the recognizer's decaying inertia deltas after
        /// lift-off) is bridged here. Content follows the finger: dragging up reveals items below.
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
            // pixels to line up with ScaledItemHeight and the device-space client rect.
            ScrollByDevicePixels (e.Delta.Y * ScaleFactor.Height);
        }

        // Scrolls by a device-pixel amount, rolling the sub-row offset over into top_index. Matches
        // the gesture-delta sign: a negative delta (upward drag) increases the scroll position.
        private void ScrollByDevicePixels (double deltaPx)
        {
            var itemH = System.Math.Max (1, ScaledItemHeight);
            var maxPosPx = System.Math.Max (0, vscrollbar.Maximum) * (double) itemH;

            // Where item[top_index] currently sits relative to the client top, before this delta --
            // the exact pixel shift a fast-blit needs is the difference between this and the same
            // quantity recomputed after the delta, since both use identical rounding to whatever a
            // full LayoutItems()/repaint would have produced.
            var oldRenderOffsetPx = top_index * itemH + (int) System.Math.Round (_scrollOffsetPx);

            var posPx = MathCompat.Clamp (top_index * (double) itemH + _scrollOffsetPx - deltaPx, 0, maxPosPx);

            var newTop = (int) (posPx / itemH);
            _scrollOffsetPx = posPx - newTop * (double) itemH;

            if (newTop != top_index) {
                // Crossing a row changes which nodes are even in _layoutItems (LayoutItems() only
                // keeps the ones from top_index down), so the fast path -- which only shifts existing
                // pixels -- can't handle this case; fall back to a normal full repaint.
                _settingScrollbarFromGesture = true;
                try { vscrollbar.Value = MathCompat.Clamp (newTop, vscrollbar.Minimum, vscrollbar.Maximum); }
                finally { _settingScrollbarFromGesture = false; }
                top_index = newTop;
                Invalidate ();
                return;
            }

            var newRenderOffsetPx = top_index * itemH + (int) System.Math.Round (_scrollOffsetPx);
            if (!TryFastScrollBlit (oldRenderOffsetPx - newRenderOffsetPx))
                Invalidate ();
        }

        /// <summary>
        /// Shifts the existing back-buffer by <paramref name="shiftPx"/> device pixels instead of
        /// re-running LayoutItems()'s full StackLayoutEngine pass plus a text/icon re-render for every
        /// visible node -- the expensive part of a TreeView repaint -- when a sub-row scroll delta
        /// hasn't changed which nodes are visible. Only the freshly-exposed strip is actually rendered.
        /// </summary>
        private bool TryFastScrollBlit (int shiftPx)
        {
            if (shiftPx == 0 || (NeedsPaint && !_skipNextRepaint) || BackgroundImage is not null || CurrentStyle.Border.GetRadius () > 0)
                return false;
            if (BackBufferPixels is not { } buffer || buffer.Width != ScaledSize.Width || buffer.Height != ScaledSize.Height)
                return false;

            var client = ClientRectangle;
            var contentWidth = client.Width - (vscrollbar.Visible ? vscrollbar.ScaledWidth : 0);
            if (contentWidth <= 0 || System.Math.Abs (shiftPx) >= client.Height)
                return false;

            // Node rectangles must reflect the new scroll position before we render the exposed strip
            // (and before returning, so a click landing before the next real repaint still hit-tests
            // against the right node) -- this is the one part of LayoutItems() that isn't optional here,
            // unlike ListBox where item rectangles are computed on the fly from top_index/_scrollOffsetPx.
            LayoutItems ();

            var contentRect = new Rectangle (client.Left, client.Top, contentWidth, client.Height);
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

            _invalidatingForFastScroll = true;
            try { Invalidate (); } finally { _invalidatingForFastScroll = false; }
            return true;
        }

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

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            // Always keep node rectangles in sync, even when the actual drawing below is skipped --
            // LayoutItems() is cheap bookkeeping (rectangle math over the visible nodes), not the
            // render-with-text-and-icons cost this fast path exists to avoid.
            LayoutItems ();

            if (_skipNextRepaint) {
                _skipNextRepaint = false;
                return;
            }

            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        // The scaled height of each TreeNode.
        internal int ScaledItemHeight
            // An explicitly-set ItemHeight wins over the measured one, which is what the property is
            // for; 20 is the default, so a caller has to mean it (LST-26).
            => ItemHeight > 0 && ItemHeight != 20
                ? LogicalToDeviceUnits (ItemHeight)
                : (root_item.Items.FirstOrDefault () ?? root_item).GetPreferredSize (Size.Empty).Height;

        /// <summary>
        /// Gets or sets the currently selected TreeNode.
        /// </summary>
        public TreeNode SelectedItem {
            get => selected_item;
            set => SelectItem (value, TreeViewAction.Unknown);
        }

        // The one place the selection moves, so BeforeSelect can veto it and both events carry the
        // action that actually caused it. AfterSelect used to report ByMouse for everything, keyboard
        // and programmatic included (LST-23).
        internal void SelectItem (TreeNode? value, TreeViewAction action)
        {
            // Don't allow user to unselect items through here; SelectedNode = null is the documented
            // way to clear a selection and goes through ClearSelectedNode.
            if (value is null)
                return;

            var current_selection = selected_item;

            if (current_selection == value)
                return;

            if (!OnBeforeSelect (new TreeViewCancelEventArgs (value, false, action)))
                return;

            selected_item = value;

            EnsureItemVisible (value);
            Invalidate ();

            OnItemSelected (new EventArgs<TreeNode> (value), action);
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
#if NETSTANDARD2_0
        // netstandard2.0 (the .NET Framework consumer surface) has no covariant-return support, so the
        // override must keep the base ControlStyle return type; the instance is still a
        // TreeViewControlStyle. Callers that need the derived members cast (see TreeViewRenderer).
        public override ControlStyle Style { get; } = new TreeViewControlStyle (DefaultStyle);
#else
        public override TreeViewControlStyle Style { get; } = new TreeViewControlStyle (DefaultStyle);
#endif

        // Determines scrollbar visibility and values using a pre-computed visible child count.
        // Called from LayoutItems() after the single-pass traversal to avoid a second traversal.
        private void UpdateVerticalScrollBar (int childCount)
        {
            if (Items.Count == 0 || ScaledItemHeight * childCount <= ScaledHeight) {
                vscrollbar.Visible = false;
                top_index = 0;
                _scrollOffsetPx = 0;
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

            // A thumb drag, wheel notch or programmatic scroll snaps to a whole row; only a touch
            // drag (which sets the value through ScrollByDevicePixels) keeps a sub-row offset.
            if (!_settingScrollbarFromGesture)
                _scrollOffsetPx = 0;

            Invalidate ();
        }

        /// <summary>
        /// Gets or sets a value indicating if TreeNode nodes will be resolved when expanded.
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

        // The number of items that can be shown with the current height. Must use the client area
        // (border excluded), not ScaledHeight (the full control including its border) -- ListBox uses
        // the equivalent ClientRectangle.Height for the same reason: overcounting here understates
        // vscrollbar.Maximum/LargeChange, shrinking the usable scroll range and the thumb's travel.
        private int VisibleItemCount => ClientRectangle.Height / ScaledItemHeight;

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
