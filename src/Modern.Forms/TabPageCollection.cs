using System.Collections.ObjectModel;

namespace Modern.Forms
{
    /// <summary>
    /// Represents a collection of TabPages.
    /// </summary>
    public class TabPageCollection : Collection<TabPage>
    {
        private readonly TabControl owner;
        private readonly TabStrip tab_strip;

        internal TabPageCollection (TabControl owner, TabStrip tabStrip)
        {
            this.owner = owner;
            tab_strip = tabStrip;
        }

        /// <summary>
        /// Adds the TabPage to the collection.
        /// </summary>
        public new TabPage Add (TabPage item)
        {
            base.Add (item);

            return item;
        }

        /// <summary>Adds a new TabPage with the specified text.</summary>
        public TabPage Add (string text) => Add (new TabPage (text));

        /// <summary>Adds a new TabPage with the specified key and text.</summary>
        public TabPage Add (string key, string text)
        {
            var page = new TabPage (text) { Name = key };
            return Add (page);
        }

        /// <summary>Adds a new TabPage with the specified key, text, and image index.</summary>
        public TabPage Add (string key, string text, int imageIndex)
        {
            var page = new TabPage (text) { Name = key, ImageIndex = imageIndex };
            return Add (page);
        }

        /// <summary>Gets the TabPage with the specified key (Name), or null.</summary>
        public TabPage? this[string key]
            => this.FirstOrDefault (p => string.Equals (p.Name, key, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns true if a TabPage with the specified key exists.</summary>
        public bool ContainsKey (string key) => this.Any (p => string.Equals (p.Name, key, StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc/>
        protected override void InsertItem (int index, TabPage item)
        {
            base.InsertItem (index, item);

            item.Visible = false;
            owner.Controls.Insert (index, item);
            tab_strip.Tabs.Insert (index, item.TabStripItem);
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            base.RemoveItem (index);

            owner.Controls.RemoveAt (index);
            tab_strip.Tabs.RemoveAt (index);
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, TabPage item)
        {
            base.SetItem (index, item);

            item.Visible = false;

            owner.SuspendLayout ();
            RemoveItem (index);
            InsertItem (index, item);
            owner.ResumeLayout ();
        }
    }
}
