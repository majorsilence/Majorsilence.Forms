using System.Drawing;
using Continuum.Forms.Renderers;
using SkiaSharp;

namespace Continuum.Forms
{
    /// <summary>
    /// Represents a TreeViewItem.
    /// </summary>
    public class TreeViewItem : ILayoutable
    {
        private readonly TreeView? tree_view;

        private bool expanded;
        internal TreeViewItemCollection? items;

        /// <summary>
        /// Initializes a new instance of the TreeViewItem class.
        /// </summary>
        public TreeViewItem ()
        {
        }

        /// <summary>
        /// Initializes a new instance of the TreeViewItem class with the specified text.
        /// </summary>
        public TreeViewItem (string text) : this () => Text = text;

        /// <summary>
        /// Initializes a new instance of the TreeViewItem class with the specified text and child nodes.
        /// </summary>
        public TreeViewItem (string text, params TreeViewItem[] children) : this (text) => Items.AddRange (children);

        // This constructor is used by the TreeView to create the root node
        internal TreeViewItem (TreeView treeView)
        {
            tree_view = treeView;
            Expanded = true;
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
            if (expanded && tree_view is null) {
                expanded = false;
                Invalidate ();
            }
        }

        /// <summary>Toggles the expanded/collapsed state of this item.</summary>
        public void Toggle ()
        {
            if (Expanded)
                Collapse ();
            else
                Expand ();
        }

        /// <summary>Begins editing the label of this tree node. Stub in Continuum.Forms.</summary>
        public void BeginEdit () { }

        /// <summary>Ends the editing of the label of this tree node. Stub in Continuum.Forms.</summary>
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
            if (TreeView?.OnBeforeExpand (this) == false)
                return;

            // If no nodes were added, don't actually expand
            // Note this also calls Items, which creates the collection, denoting that an
            // Expand has been called and we don't need to draw the dropdown glyph anymore
            if (tree_view == null && Items.Count == 0) {
                Invalidate ();
                return;
            }

            expanded = true;
            Invalidate ();
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
        internal IEnumerable<TreeViewItem> GetAllItems ()
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

            if (glyph_bounds.Contains (location))
                return TreeViewItemElement.Glyph;

            return TreeViewItemElement.None;
        }

        /// <summary>
        /// Gets the preferred size of the item.
        /// </summary>
        public virtual Size GetPreferredSize (Size proposedSize)
        {
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
        internal IEnumerable<TreeViewItem> GetVisibleItems ()
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

        private Continuum.Drawing.Image? _image;
        private SKBitmap? _imageSK;

        /// <summary>
        /// Gets or sets the image of the item. Accepts <see cref="Continuum.Drawing.Image"/> for WinForms compatibility.
        /// </summary>
#pragma warning disable CA1416
        public Continuum.Drawing.Image? Image {
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
        public TreeViewItem? NextItem ()
        {
            if (Parent is TreeViewItem parent) {
                var index = Parent.Items.IndexOf (this);

                if (parent.Items.Count > index + 1)
                    return parent.Items[index + 1];
            }

            return null;
        }

        /// <summary>
        /// The parent item that contains this item.
        /// </summary>
        public TreeViewItem? Parent { get; internal set; }

        /// <summary>
        /// Retrives the previous sibling of this item.
        /// </summary>
        public TreeViewItem? PreviousItem ()
        {
            if (Parent is TreeViewItem parent) {
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
        public bool Checked { get; set; }

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
        public TreeViewItem? FirstNode => Items.Count > 0 ? Items[0] : null;

        /// <summary>Gets the last child node of this item, or null if no children.</summary>
        public TreeViewItem? LastNode => Items.Count > 0 ? Items[Items.Count - 1] : null;

        /// <summary>Gets the next sibling item.</summary>
        public TreeViewItem? NextNode {
            get {
                if (Parent is null)
                    return null;

                var idx = Parent.Items.IndexOf (this);
                return idx >= 0 && idx < Parent.Items.Count - 1 ? Parent.Items[idx + 1] : null;
            }
        }

        /// <summary>Gets the previous sibling item.</summary>
        public TreeViewItem? PrevNode {
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
        public Continuum.Drawing.Font? NodeFont { get; set; }
#pragma warning restore CA1416

        /// <summary>Gets or sets the image list index for this node's image. Stub in Continuum.Forms.</summary>
        public int ImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image list key for this node's image. Stub in Continuum.Forms.</summary>
        public string ImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the image list index shown when this node is selected. Stub in Continuum.Forms.</summary>
        public int SelectedImageIndex { get; set; } = -1;

        /// <summary>Gets or sets the image list key shown when this node is selected. Stub in Continuum.Forms.</summary>
        public string SelectedImageKey { get; set; } = string.Empty;

        /// <summary>Gets or sets the state image index for the node. Stub in Continuum.Forms.</summary>
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
        /// Elements of a TreeViewItem.
        /// </summary>
        protected internal enum TreeViewItemElement
        {
            /// <summary>
            /// No element.
            /// </summary>
            None,

            /// <summary>
            /// The glyph (dropdown arrow) of the TreeViewItem.
            /// </summary>
            Glyph,

            /// <summary>
            /// The image of the TreeViewItem.
            /// </summary>
            Image,

            /// <summary>
            /// The text of the TreeViewItem.
            /// </summary>
            Text
        }
    }
}
