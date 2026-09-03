using System.Drawing;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a TreeNode.
    /// </summary>
    public partial class TreeNode : ILayoutable
    {
        private readonly TreeView? tree_view;

        private bool expanded;
        internal TreeViewItemCollection? items;

        /// <summary>
        /// Initializes a new instance of the TreeNode class.
        /// </summary>
        public TreeNode ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TreeNode class with the specified text.
        /// </summary>
        public TreeNode (string text) : this () => Text = text;

        /// <summary>
        /// Initializes a new instance of the TreeNode class with the specified text and child nodes.
        /// </summary>
        public TreeNode (string text, params TreeNode[] children) : this (text) => Items.AddRange (children);

        /// <summary>
        /// Initializes a new instance with text and the indices of its normal and selected images.
        /// </summary>
        public TreeNode (string text, int imageIndex, int selectedImageIndex) : this (text)
        {
            ImageIndex = imageIndex;
            SelectedImageIndex = selectedImageIndex;
        }

        /// <summary>
        /// Initializes a new instance with text, image indices and child nodes.
        /// </summary>
        public TreeNode (string text, int imageIndex, int selectedImageIndex, TreeNode[] children)
            : this (text, children)
        {
            ImageIndex = imageIndex;
            SelectedImageIndex = selectedImageIndex;
        }

        // This constructor is used by the TreeView to create the root node
        internal TreeNode (TreeView treeView)
        {
            tree_view = treeView;
            // Set the field, not the property: the invisible root is always expanded (the whole
            // visible-node walk hangs off it), including before any child is added -- and Expand ()
            // now returns early for a node with no children.
            expanded = true;
        }

        /// <summary>
        /// Gets the current bounding box of the item.
        /// </summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>
        /// Hides this item's children.
        /// </summary>
        public void Collapse ()
        {
            // Don't let the root_item be collapsed
            if (!expanded || tree_view is not null)
                return;

            // BeforeCollapse was never raised from anywhere, and AfterCollapse only from a glyph
            // click -- so a programmatic Collapse ()/CollapseAll () announced nothing, and code that
            // frees child nodes in AfterCollapse never ran (LST-23).
            var tree = TreeView;

            if (tree?.RaiseBeforeCollapse (this) == false)
                return;

            expanded = false;
            Invalidate ();

            tree?.RaiseAfterExpandCollapse (this, expanded: false);
        }

        /// <summary>Toggles the expanded/collapsed state of this item.</summary>
        public void Toggle ()
        {
            if (Expanded)
                Collapse ();
            else
                Expand ();
        }

        /// <summary>Begins editing the label of this tree node. Stub in Majorsilence.Forms.</summary>
        public void BeginEdit () { }

        /// <summary>Ends the editing of the label of this tree node. Stub in Majorsilence.Forms.</summary>
        public void EndEdit (bool cancel) { }

        /// <summary>
        /// Gets or sets a context menu to display when the item is right-clicked.
        /// </summary>
        public ContextMenu? ContextMenu { get; set; }

        /// <summary>
        /// Ensure this item is visible, expanding items and scrolling the view as needed.
        /// </summary>
        public void EnsureVisible ()
        {
            tree_view?.EnsureItemVisible (this);
        }

        /// <summary>
        /// Shows this item's children.
        /// </summary>
        public void Expand ()
        {
            if (expanded)
                return;

            // WinForms raises BeforeExpand only for a node that actually has children to show --
            // a leaf never fires it. Checking (and returning) before OnBeforeExpand also stops a
            // handler that lazily fills a node from running for a node it was never meant to:
            // DialogDatabase in the migrated ReportDesigner wires BeforeExpand to a column-loader
            // that dereferences node.FirstNode, and double-clicking a leaf column node (OnDoubleClick
            // toggles Expanded on whatever was hit, glyph or not) drove it in with FirstNode null --
            // an unhandled NullReferenceException that took the app down.
            // (Items is also what creates the collection, marking that an expand was attempted so the
            // glyph stops being drawn -- keep touching it here.)
            if (Items.Count == 0) {
                Invalidate ();
                return;
            }

            if (TreeView?.OnBeforeExpand (this) == false)
                return;

            expanded = true;
            Invalidate ();

            // AfterExpand used to fire only from a glyph click, so a lazy-loading tree populated in
            // BeforeExpand had no matching completion hook on the programmatic path (LST-23).
            TreeView?.RaiseAfterExpandCollapse (this, expanded: true);
        }

        /// <summary>
        /// Gets or sets a value indicating this node is showing its child nodes.
        /// </summary>
        public bool Expanded {
            get => expanded;
            set {
                if (value)
                    Expand ();
                else
                    Collapse ();
            }
        }

        // Get an IEnumerable of this item and all of its children, recursive.
        internal IEnumerable<TreeNode> GetAllItems ()
        {
            yield return this;

            if (HasChildren)
                foreach (var item in Items)
                    foreach (var child in item.GetAllItems ())
                        yield return child;
        }

        // Gets the element of the item at the specified location.
        internal TreeViewItemElement GetElementAtLocation (Point location)
        {
            var tv = TreeView;

            if (tv is null)
                return TreeViewItemElement.None;

            var renderer = RenderManager.GetRenderer<TreeViewRenderer> ();

            var glyph_bounds = renderer!.GetGlyphBounds (tv, this);

            // Give the user a slightly more generous click target
            if (!glyph_bounds.IsEmpty)
                glyph_bounds.Inflate (4, 4);

            // location arrives logical (MouseEventArgs); glyph_bounds is device-pixel (built from the
            // item's device Bounds). Convert before testing, as TreeView.GetItemAtLocation does.
            var device = new Point (tv.LogicalToDeviceUnits (location.X), tv.LogicalToDeviceUnits (location.Y));

            if (glyph_bounds.Contains (device))
                return TreeViewItemElement.Glyph;

            return TreeViewItemElement.None;
        }

        /// <summary>
        /// Gets the preferred size of the item.
        /// </summary>
        public virtual Size GetPreferredSize (Size proposedSize)
        {
            // The tree's ItemHeight wins when it has been set: the row layout asks each NODE for its
            // height, so honouring the property only in TreeView.ScaledItemHeight left the drawn rows
            // at the measured height and the scroll maths disagreeing with them (LST-26). 20 is the
            // default, so a caller has to mean it.
            if (TreeView is { ItemHeight: > 0 } tree && tree.ItemHeight != 20)
                return new Size (0, LogicalToDeviceUnits (tree.ItemHeight));

            var font_size = LogicalToDeviceUnits (Theme.FontSize);
            var padding = LogicalToDeviceUnits (10);

            return new Size (0, font_size + padding);
        }

        // Gets the number of currently visible children nodes, recursively.
        // Note this is nodes whose state is visible (parent is expanded).
        // Not necessarily nodes currently scrolled into view.
        internal int GetVisibleChildrenCount () => GetVisibleItems ().Count () - 1;

        // Gets an enumerator of this node and currently visible children nodes, recursively.
        // Note this is nodes whose state is visible (parent is expanded).
        // Not necessarily nodes currently scrolled into view.
        internal IEnumerable<TreeNode> GetVisibleItems ()
        {
            yield return this;

            if (Expanded && HasChildren)
                foreach (var item in Items)
                    foreach (var child in item.GetVisibleItems ())
                        yield return child;
        }

        /// <summary>
        /// Gets a value indicating whether this item contains child items.
        /// </summary>
        public bool HasChildren => (items?.Count ?? 0) > 0;

        private Majorsilence.Forms.Drawing.Image? _image;
        private SKBitmap? _imageSK;

        /// <summary>
        /// Gets or sets the image of the item. Accepts <see cref="Majorsilence.Forms.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Image? Image {
            get => _image;
            set {
                _image = value;
                _imageSK?.Dispose ();
                _imageSK = value?.ToSKBitmap ();
            }
        }
#pragma warning restore CA1416

        /// <summary>Gets the SKBitmap representation of the image (used by renderers).</summary>
        internal SKBitmap? ImageSK => _imageSK;

        /// <summary>Sets the image directly from an SKBitmap (internal use).</summary>
        internal void SetImageSK (SKBitmap? bmp) { _image = null; _imageSK = bmp; }

        /// <summary>
        /// Gets a value indicating how many levels this item is nested from the root.
        /// </summary>
        public int IndentLevel {
            get {
                // Root node is -1
                if (tree_view != null)
                    return -1;

                // If this is called without a Parent, return 0 ?
                if (Parent == null)
                    return 0;

                return Parent.IndentLevel + 1;
            }
        }

        // Invalidates the node.
        internal void Invalidate ()
        {
            TreeView?.Invalidate ();
        }

        /// <summary>
        /// Gets the collection of child nodes.
        /// </summary>
        public TreeViewItemCollection Items => items ??= new TreeViewItemCollection (this);

        /// <summary>WinForms compatibility alias for <see cref="Items"/>.</summary>
        public TreeViewItemCollection Nodes => Items;

        /// <summary>
        /// Gets the amount of margin to leave around this item. This is internal API and should not be called.
        /// </summary>
        public Padding Margin => Padding.Empty;

        /// <summary>
        /// Retrives the next sibling of this item.
        /// </summary>
        public TreeNode? NextItem ()
        {
            if (Parent is TreeNode parent) {
                var index = Parent.Items.IndexOf (this);

                if (parent.Items.Count > index + 1)
                    return parent.Items[index + 1];
            }

            return null;
        }

        /// <summary>
        /// The parent item that contains this item.
        /// </summary>
        public TreeNode? Parent { get; internal set; }

        /// <summary>
        /// Retrives the previous sibling of this item.
        /// </summary>
        public TreeNode? PreviousItem ()
        {
            if (Parent is TreeNode parent) {
                var index = Parent.Items.IndexOf (this);

                if (index > 0)
                    return parent.Items[index - 1];
            }

            return null;
        }

        /// <summary>
        /// Sets the bounding box of the item. This is internal API and should not be called.
        /// </summary>
        public void SetBounds (int x, int y, int width, int height, BoundsSpecified specified = BoundsSpecified.All)
        {
            Bounds = new Rectangle (x, y, width, height);
        }

        /// <summary>
        /// Gets or sets an object with additional user data about this item.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>Gets or sets the ToolTip text shown for this item.</summary>
        public string ToolTipText { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the item.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the item is checked (when TreeView.CheckBoxes is true).</summary>
        public bool Checked {
            get => checked_state;
            set => SetChecked (value, TreeViewAction.Unknown);
        }

        private bool checked_state;

        // Ask, set, report -- the shape upstream's TreeNode.Checked has. It was an auto-property, so
        // BeforeCheck could not veto and the cascading-check code every permission tree hangs off
        // AfterCheck never ran (LST-24).
        internal void SetChecked (bool value, TreeViewAction action)
        {
            if (checked_state == value)
                return;

            var tree = TreeView;

            if (tree?.RaiseBeforeCheck (this, action) == false)
                return;

            checked_state = value;
            Invalidate ();

            tree?.RaiseAfterCheck (this, action);
        }

        /// <summary>Gets or sets the foreground color override for this item. Empty means use default.</summary>
        public System.Drawing.Color ForeColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets or sets the background color override for this item. Empty means use default.</summary>
        public System.Drawing.Color BackColor { get; set; } = System.Drawing.Color.Empty;

        /// <summary>Gets the depth level (0 = nodes directly in the TreeView, matching WinForms).</summary>
        public int Level {
            get {
                // WinForms: nodes directly in the TreeView (or a detached node) are Level 0,
                // their children are Level 1, etc. The internal root node is not counted.
                var level = 0;
                var parent = Parent;

                while (parent != null && parent.tree_view == null) {
                    level++;
                    parent = parent.Parent;
                }

                return level;
            }
        }

        /// <summary>Gets whether this item is currently expanded.</summary>
        public bool IsExpanded => Expanded;

        /// <summary>Gets whether this item is currently selected.</summary>
        public bool IsSelected => TreeView?.SelectedItem == this;

        /// <summary>Gets the full path of node names from root to this node.</summary>
        public string FullPath {
            get {
                if (Parent is null)
                    return Text;

                return Parent.Parent is null ? Text : Parent.FullPath + "\\" + Text;
            }
        }

        /// <summary>Gets the first child node of this item, or null if no children.</summary>
        public TreeNode? FirstNode => Items.Count > 0 ? Items[0] : null;

        /// <summary>Gets the last child node of this item, or null if no children.</summary>
        public TreeNode? LastNode => Items.Count > 0 ? Items[Items.Count - 1] : null;

        /// <summary>Gets the next sibling item.</summary>
        public TreeNode? NextNode {
            get {
                if (Parent is null)
                    return null;

                var idx = Parent.Items.IndexOf (this);
                return idx >= 0 && idx < Parent.Items.Count - 1 ? Parent.Items[idx + 1] : null;
            }
        }

        /// <summary>Gets the previous sibling item.</summary>
        public TreeNode? PrevNode {
            get {
                if (Parent is null)
                    return null;

                var idx = Parent.Items.IndexOf (this);
                return idx > 0 ? Parent.Items[idx - 1] : null;
            }
        }

        /// <summary>
        /// Gets or sets the text of the item.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets the TreeView that contains this item.
        /// </summary>
        public TreeView? TreeView {
            get {
                if (tree_view != null)
                    return tree_view;

                return Parent?.TreeView;
            }
        }

        /// <summary>Gets the zero-based index of this node in its parent's Nodes collection.</summary>
        public int Index {
            get {
                // WinForms returns 0 for a detached node (one with no parent collection).
                if (Parent is null)
                    return TreeView?.Items.IndexOf (this) ?? 0;

                return Parent.Items.IndexOf (this);
            }
        }

        /// <summary>Gets or sets the font for this node. Null means use the TreeView font.</summary>
#pragma warning disable CA1416
        public Majorsilence.Forms.Drawing.Font? NodeFont { get; set; }
#pragma warning restore CA1416

        /// <summary>Gets or sets the image list index for this node's image. Stub in Majorsilence.Forms.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image list key for this node's image. Stub in Majorsilence.Forms.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the image list index shown when this node is selected. Stub in Majorsilence.Forms.</summary>
        public int SelectedImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image list key shown when this node is selected. Stub in Majorsilence.Forms.</summary>
        public string SelectedImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the state image index for the node. Stub in Majorsilence.Forms.</summary>
        public int StateImageIndex { get; set; } = -1;


        /// <summary>Removes this node from its parent's collection.</summary>
        public void Remove ()
        {
            if (Parent != null)
                Parent.Items.Remove (this);
            else
                TreeView?.Items.Remove (this);
        }

        /// <summary>Returns the number of child nodes, optionally counting all descendants.</summary>
        public int GetNodeCount (bool includeSubTrees)
        {
            if (!includeSubTrees) return Items.Count;
            int count = 0;
            foreach (var child in Items) count += 1 + child.GetNodeCount (true);
            return count;
        }

        private int LogicalToDeviceUnits (int value) => TreeView?.LogicalToDeviceUnits (value) ?? value;

        /// <summary>
        /// Elements of a TreeNode.
        /// </summary>
        protected internal enum TreeViewItemElement
        {
            /// <summary>
            /// No element.
            /// </summary>
            None,

            /// <summary>
            /// The glyph (dropdown arrow) of the TreeNode.
            /// </summary>
            Glyph,

            /// <summary>
            /// The image of the TreeNode.
            /// </summary>
            Image,

            /// <summary>
            /// The text of the TreeNode.
            /// </summary>
            Text
        }
    }
}
