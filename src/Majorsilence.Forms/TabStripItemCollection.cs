using System.Collections.ObjectModel;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a collection of TabStripItems.
    /// </summary>
    public class TabStripItemCollection : Collection<TabStripItem>
    {
        private readonly TabStrip owner;
        private int focused_index;
        private int hovered_index = -1;
        private int selected_index = -1;

        internal TabStripItemCollection (TabStrip owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Adds the TabStripItem to the collection.
        /// </summary>
        public new TabStripItem Add (TabStripItem item)
        {
            item.Parent = owner;
            base.Add (item);

            return item;
        }

        /// <summary>
        /// Adds a new TabStripItem to the collection with the specified text.
        /// </summary>
        public TabStripItem Add (string text) => Add (new TabStripItem { Text = text });

        internal int FocusedIndex {
            get => focused_index;
            set {
                if (focused_index != value) {
                    focused_index = value;
                    owner.Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the tab the mouse is currently hovered over.
        /// </summary>
        internal int HoveredIndex {
            get => hovered_index;
            set {
                if (hovered_index != value) {
                    hovered_index = value;
                    owner.Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void InsertItem (int index, TabStripItem item)
        {
            item.Parent = owner;

            base.InsertItem (index, item);

            if (Count == 1)
                owner.SelectedTab = item;
            else
                owner.Invalidate ();
        }

        /// <inheritdoc/>
        protected override void RemoveItem (int index)
        {
            var item = this[index];

            item.Parent = null;

            var selected_tab = owner.SelectedTab;

            base.RemoveItem (index);

            if (selected_tab == item && Count > 0) {
                // Need to temporarily set this to nothing in case the index doesn't change,
                // we still want to force it to be treated like a new selection.
                selected_index = -1;
                owner.SelectedIndex = Math.Max (index - 1, 0);
            }

            if (selected_tab is null && Count > 0)
                owner.SelectedIndex = 0;
            else
                owner.Invalidate ();
        }

        /// <summary>
        /// Gets or sets the index of the currently selected tab.
        /// </summary>
        internal int SelectedIndex {
            get => selected_index;
            set {
                if (value < -1)
                    throw new ArgumentOutOfRangeException (nameof (value), "Index out of range");

                // WinForms compatibility: designer-generated InitializeComponent code
                // unconditionally emits `tabControl1.SelectedIndex = 0;` for every TabControl,
                // including ones whose tabs are added dynamically at runtime rather than
                // statically in InitializeComponent -- at that point Count is 0, and any
                // non-negative index (0 included) is technically "out of range". Real
                // System.Windows.Forms tolerates this (the native tab control silently has
                // nothing to select yet); treat an empty collection as "nothing to select" here
                // too rather than throwing, so ported code doesn't need to special-case or strip
                // this ubiquitous line. The first tab added later still gets auto-selected (see
                // InsertItem below). Negative values below -1 still throw above, unconditionally --
                // that's not a "no tabs yet" situation, it's a genuinely invalid argument.
                if (Count == 0) {
                    selected_index = -1;
                    return;
                }

                if (value >= Count)
                    throw new ArgumentOutOfRangeException (nameof (value), "Index out of range");

                if (selected_index != value) {
                    selected_index = value;
                    focused_index = value;
                }
            }
        }

        /// <inheritdoc/>
        protected override void SetItem (int index, TabStripItem item)
        {
            this[index].Parent = null;
            item.Parent = owner;

            base.SetItem (index, item);
        }
    }
}
