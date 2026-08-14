using System.Collections.ObjectModel;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of TreeViewItems.
    /// </summary>
    /// <summary>WinForms-compatible name for the tree item collection (TreeView.Nodes).</summary>
    public partial class TreeNodeCollection : Collection<TreeNode>
    {
        /// <summary>
        /// Adds a new node with the specified text. Returns a <see cref="TreeNode"/> so WinForms
        /// migration code can assign directly to <c>TreeNode</c> variables.
        /// </summary>
        public TreeNode Add (string text)
        {
            var node = new TreeNode (text);
            Add (node);
            return node;
        }

        /// <summary>Adds a new node with the specified key (Name) and text. Mirrors WinForms TreeNodeCollection.Add(key, text).</summary>
        public TreeNode Add (string key, string text)
        {
            var node = new TreeNode (text) { Name = key };
            Add (node);
            return node;
        }

        /// <summary>Returns whether the collection contains a node whose Name equals the specified key. Mirrors WinForms.</summary>
        public bool ContainsKey (string key)
            => this.Any (n => string.Equals (n.Name, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets the first node whose Name equals the specified key, or null. Typed
        /// <see cref="TreeNode"/> for WinForms migration code -- nodes added through the
        /// string-based Add overloads are always TreeNode instances.
        /// </summary>
        public TreeNode? this[string key]
            => this.FirstOrDefault (n => string.Equals (n.Name, key, StringComparison.OrdinalIgnoreCase)) as TreeNode;

        /// <summary>Removes the first node whose Name equals the specified key, if present. Mirrors WinForms.</summary>
        public void RemoveByKey (string key)
        {
            var node = this[key];

            if (node is not null)
                Remove (node);
        }

        /// <summary>
        /// Finds nodes whose Name equals the specified key, optionally searching all descendants.
        /// Mirrors WinForms TreeNodeCollection.Find.
        /// </summary>
        public TreeNode[] Find (string key, bool searchAllChildren)
        {
            var matches = new List<TreeNode> ();
            Collect (this, key, searchAllChildren, matches);
            return matches.ToArray ();
        }

        private static void Collect (IEnumerable<TreeNode> items, string key, bool recurse, List<TreeNode> matches)
        {
            foreach (var item in items) {
                if (item is TreeNode node && string.Equals (node.Name, key, StringComparison.OrdinalIgnoreCase))
                    matches.Add (node);

                if (recurse)
                    Collect (item.Items, key, true, matches);
            }
        }

        // The overloads WinForms code actually writes. Add (TreeNode) in particular returns the new
        // node's index -- `var i = nodes.Add (node);` is ordinary WinForms and did not compile here,
        // because the only Add taking a node was Collection<T>'s void one.

        /// <summary>Adds a node with the given key, text and image index.</summary>
        public TreeNode Add (string key, string text, int imageIndex)
        {
            var node = new TreeNode (text) { Name = key, ImageIndex = imageIndex };
            Add (node);
            return node;
        }

        /// <summary>Adds a node with the given key, text and image indices.</summary>
        public TreeNode Add (string key, string text, int imageIndex, int selectedImageIndex)
        {
            var node = new TreeNode (text) { Name = key, ImageIndex = imageIndex, SelectedImageIndex = selectedImageIndex };
            Add (node);
            return node;
        }

        /// <summary>Adds a node with the given key, text and image key.</summary>
        public TreeNode Add (string key, string text, string imageKey)
        {
            var node = new TreeNode (text) { Name = key, ImageKey = imageKey };
            Add (node);
            return node;
        }

        /// <summary>Adds a node with the given key, text and image keys.</summary>
        public TreeNode Add (string key, string text, string imageKey, string selectedImageKey)
        {
            var node = new TreeNode (text) { Name = key, ImageKey = imageKey, SelectedImageKey = selectedImageKey };
            Add (node);
            return node;
        }

        /// <summary>Adds an existing node and returns its index.</summary>
        /// <remarks>
        /// Calls <c>base.Add</c> explicitly. It used to cast the argument to the collection's element type
        /// and call <c>Add</c> unqualified, which reached <c>Collection&lt;T&gt;.Add</c> only because the
        /// element type was then the node's base class. Once node and element type became the same type
        /// that cast stopped changing anything, so the call bound to this method and recursed until the
        /// stack ran out -- the qualification is what makes the target explicit rather than incidental.
        /// </remarks>
        public new int Add (TreeNode node)
        {
            base.Add (node);
            return Count - 1;
        }

        /// <summary>Returns whether the given node is in this collection.</summary>
        /// <inheritdoc cref="Add(TreeNode)" path="/remarks"/>
        public new bool Contains (TreeNode node) => base.Contains (node);

        /// <summary>Returns the index of the given node, or -1.</summary>
        public new int IndexOf (TreeNode node) => base.IndexOf (node);

        /// <summary>Removes the given node.</summary>
        public new void Remove (TreeNode node) => base.Remove (node);

        /// <summary>Copies this collection into an array.</summary>
        public void CopyTo (Array dest, int index)
        {
            ArgumentNullException.ThrowIfNull (dest);

            foreach (var node in this)
                dest.SetValue (node, index++);
        }

        /// <summary>Inserts an existing node at the given index.</summary>
        public new void Insert (int index, TreeNode node) => base.Insert (index, node);

        /// <summary>Inserts a node with the given text at the given index.</summary>
        public TreeNode Insert (int index, string text) => Insert (index, string.Empty, text);

        /// <summary>Inserts a node with the given key and text at the given index.</summary>
        public TreeNode Insert (int index, string key, string text)
        {
            var node = new TreeNode (text) { Name = key };
            Insert (index, node);
            return node;
        }

        /// <summary>Inserts a node with the given key, text and image index.</summary>
        public TreeNode Insert (int index, string key, string text, int imageIndex)
        {
            var node = new TreeNode (text) { Name = key, ImageIndex = imageIndex };
            Insert (index, node);
            return node;
        }

        /// <summary>Inserts a node with the given key, text and image indices.</summary>
        public TreeNode Insert (int index, string key, string text, int imageIndex, int selectedImageIndex)
        {
            var node = new TreeNode (text) { Name = key, ImageIndex = imageIndex, SelectedImageIndex = selectedImageIndex };
            Insert (index, node);
            return node;
        }

        /// <summary>Inserts a node with the given key, text and image key.</summary>
        public TreeNode Insert (int index, string key, string text, string imageKey)
        {
            var node = new TreeNode (text) { Name = key, ImageKey = imageKey };
            Insert (index, node);
            return node;
        }

        /// <summary>Inserts a node with the given key, text and image keys.</summary>
        public TreeNode Insert (int index, string key, string text, string imageKey, string selectedImageKey)
        {
            var node = new TreeNode (text) { Name = key, ImageKey = imageKey, SelectedImageKey = selectedImageKey };
            Insert (index, node);
            return node;
        }
    }

    /// <summary>Represents the collection of items in a TreeView.</summary>
    public class TreeViewItemCollection : TreeNodeCollection
    {
        private readonly TreeNode owner;

        internal TreeViewItemCollection (TreeNode owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds the TreeNode to the collection.
        /// </summary>
        public new TreeNode Add (TreeNode item)
        {
            base.Add (item);

            return item;
        }

        // Add(string) comes from the TreeNodeCollection base (returns TreeNode).

        /// <summary>
        /// Adds a new item to the collection with the specified text and SKBitmap image.
        /// </summary>
        public TreeNode Add (string text, SKBitmap image) { var item = new TreeNode (text); item.SetImageSK (image); return Add (item); }

        /// <summary>
        /// Adds a new item to the collection with the specified text and image (WinForms compatibility overload).
        /// </summary>
        public TreeNode Add (string text, Majorsilence.Forms.Drawing.Image image) { var item = new TreeNode (text); item.Image = image; return Add (item); }

        /// <summary>
        /// Adds a collection of TreeViewItems to the collection.
        /// </summary>
        public void AddRange (IEnumerable<TreeNode> children)
        {
            var tv = owner.TreeView;

            tv?.SuspendLayout ();

            foreach (var item in children)
                Add (item);

            tv?.ResumeLayout ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, TreeNode item)
        {
            base.InsertItem (index, item);

            item.Parent = owner;
            owner.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void ClearItems ()
        {
            foreach (var item in this)
                item.Parent = null;

            base.ClearItems ();

            owner.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            var item = this[index];

            base.RemoveItem (index);

            item.Parent = null;
            owner.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, TreeNode item)
        {
            var old_item = this.ElementAtOrDefault (index);

            if (old_item != null)
                old_item.Parent = null;

            base.SetItem (index, item);

            item.Parent = owner;
            owner.Invalidate ();
        }
    }
}
