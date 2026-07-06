using System.Collections.ObjectModel;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of TreeViewItems.
    /// </summary>
    /// <summary>WinForms-compatible name for the tree item collection (TreeView.Nodes).</summary>
    public class TreeNodeCollection : Collection<TreeViewItem>
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

        private static void Collect (IEnumerable<TreeViewItem> items, string key, bool recurse, List<TreeNode> matches)
        {
            foreach (var item in items) {
                if (item is TreeNode node && string.Equals (node.Name, key, StringComparison.OrdinalIgnoreCase))
                    matches.Add (node);

                if (recurse)
                    Collect (item.Items, key, true, matches);
            }
        }
    }

    /// <summary>Represents the collection of items in a TreeView.</summary>
    public class TreeViewItemCollection : TreeNodeCollection
    {
        private readonly TreeViewItem owner;

        internal TreeViewItemCollection (TreeViewItem owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds the TreeViewItem to the collection.
        /// </summary>
        public new TreeViewItem Add (TreeViewItem item)
        {
            base.Add (item);

            return item;
        }

        // Add(string) comes from the TreeNodeCollection base (returns TreeNode).

        /// <summary>
        /// Adds a new item to the collection with the specified text and SKBitmap image.
        /// </summary>
        public TreeViewItem Add (string text, SKBitmap image) { var item = new TreeViewItem (text); item.SetImageSK (image); return Add (item); }

        /// <summary>
        /// Adds a new item to the collection with the specified text and image (WinForms compatibility overload).
        /// </summary>
        public TreeViewItem Add (string text, Majorsilence.Forms.Drawing.Image image) { var item = new TreeViewItem (text); item.Image = image; return Add (item); }

        /// <summary>
        /// Adds a collection of TreeViewItems to the collection.
        /// </summary>
        public void AddRange (IEnumerable<TreeViewItem> children)
        {
            var tv = owner.TreeView;

            tv?.SuspendLayout ();

            foreach (var item in children)
                Add (item);

            tv?.ResumeLayout ();
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, TreeViewItem item)
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
        protected override void SetItem (int index, TreeViewItem item)
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
