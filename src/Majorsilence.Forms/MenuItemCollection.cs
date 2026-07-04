using System.Collections.ObjectModel;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of MenuItems.
    /// </summary>
    public class MenuItemCollection : Collection<MenuItem>
    {
        private readonly MenuItem owner;

        internal MenuItemCollection (MenuItem owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds a range of MenuItems to the collection.
        /// </summary>
        public void AddRange (IEnumerable<MenuItem> items)
        {
            foreach (var item in items)
                Add (item);
        }

        /// <summary>
        /// Finds an item by its ToolStripItem.Name (matches System.Windows.Forms.ToolStripItemCollection's
        /// string indexer). Returns null if no item with that name exists (MenuItem itself has no
        /// Name property, only ToolStripItem subclasses do, so plain MenuItems never match).
        /// </summary>
        public MenuItem? this [string name] {
            get {
                foreach (var item in this) {
                    if (item is ToolStripItem tsi && tsi.Name == name)
                        return item;
                }
                return null;
            }
        }

        /// <summary>
        /// Adds the MenuItem to the collection.
        /// </summary>
        public T Add<T> (T item) where T : MenuItem
        {
            base.Add (item);
            return item;
        }

        /// <summary>
        /// Adds a new MenuItem to the collection with the specified text, image (SKBitmap), and Click handler.
        /// </summary>
        public MenuItem Add (string text, SKBitmap? image = null, EventHandler<MouseEventArgs>? onClick = null)
        {
            return Add (new MenuItem (text, image, onClick));
        }

        /// <summary>
        /// Adds a new MenuItem to the collection with the specified text, image, and Click handler.
        /// </summary>
#pragma warning disable CA1416
        public MenuItem Add (string text, Majorsilence.Forms.Drawing.Image? image, EventHandler<MouseEventArgs>? onClick = null)
        {
            var item = new MenuItem (text, (SKBitmap?)null, onClick);
            item.Image = image;
            return Add (item);
        }
#pragma warning restore CA1416

        /// <inheritdoc/>
        protected override void InsertItem (int index, MenuItem item)
        {
            base.InsertItem (index, item);

            item.Parent = owner;
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            var item = this[index];

            base.RemoveItem (index);

            item.Parent = null;
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, MenuItem item)
        {
            var old_item = this.ElementAtOrDefault (index);

            if (old_item != null)
                old_item.Parent = null;

            base.SetItem (index, item);

            item.Parent = owner;
        }
    }
}
