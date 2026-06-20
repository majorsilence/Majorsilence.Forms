using System.Collections.ObjectModel;
using SkiaSharp;

namespace Continuum.Forms
{
    /// <summary>
    /// Represents a collection of TreeViewItems.
    /// </summary>
    public class TreeViewItemCollection : Collection<TreeViewItem>
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

        /// <summary>
        /// Adds a new item to the collection with the specified text.
        /// Returns a <see cref="TreeNode"/> so WinForms migration code can assign directly to <c>TreeNode</c> variables.
        /// </summary>
        public TreeNode Add (string text) { var n = new TreeNode (text); Add (n); return n; }

        /// <summary>
        /// Adds a new item to the collection with the specified text and SKBitmap image.
        /// </summary>
        public TreeViewItem Add (string text, SKBitmap image) { var item = new TreeViewItem (text); item.SetImageSK (image); return Add (item); }

        /// <summary>
        /// Adds a new item to the collection with the specified text and image (WinForms compatibility overload).
        /// </summary>
        public TreeViewItem Add (string text, Continuum.Drawing.Image image) { var item = new TreeViewItem (text); item.Image = image; return Add (item); }

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
