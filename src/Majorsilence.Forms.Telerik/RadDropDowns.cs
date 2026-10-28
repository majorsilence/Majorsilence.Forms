using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat drop-down list. Backed by <see cref="Majorsilence.Forms.ComboBox"/>.</summary>
    public class RadDropDownList : ComboBox, ISupportInitializeCompat
    {
        /// <summary>Initializes a new instance, bridging the base selection event to the Telerik-typed one.</summary>
        public RadDropDownList ()
        {
            base.SelectedIndexChanged += (_, _) => SelectedIndexChanged?.Invoke (this, new Majorsilence.Forms.Telerik.Data.PositionChangedEventArgs (SelectedIndex));
        }

        /// <summary>
        /// Occurs when the selected index changes. Telerik-typed replacement of the base event —
        /// handlers receive <see cref="PositionChangedEventArgs"/> carrying the new index.
        /// </summary>
        public new event PositionChangedEventHandler? SelectedIndexChanged;

        /// <summary>
        /// Gets the selected item as a Telerik list item: the item itself when it is a
        /// <see cref="RadListDataItem"/>, otherwise a wrapper whose <c>DataBoundItem</c> is the item and whose
        /// <c>Value</c> is the <see cref="SelectedValue"/>.
        /// </summary>
        /// <remarks>W6 mechanisms (#176): an added <see cref="RadListDataItem"/> used to come back as a new
        /// wrapper around itself, so <c>SelectedItem.Value</c> was always null.</remarks>
        public new RadListDataItem? SelectedItem => base.SelectedItem switch {
            null => null,
            RadListDataItem item => item,
            var item => new RadListDataItem { Text = GetItemText (item), DataBoundItem = item, Value = base.SelectedValue },
        };

        /// <summary>
        /// Gets or sets the selected value. For an unbound list of <see cref="RadListDataItem"/>s it is the
        /// selected item's <see cref="RadListDataItem.Value"/>, as in Telerik; otherwise the
        /// <see cref="ComboBox"/> rules (<c>ValueMember</c>, else the item) apply.
        /// </summary>
        public override object? SelectedValue {
            get => DataSource is null && base.SelectedItem is RadListDataItem item ? item.Value : base.SelectedValue;
            set {
                if (DataSource is null)
                    for (var i = 0; i < Items.Count; i++)
                        if (Items[i] is RadListDataItem item && Equals (item.Value, value)) {
                            SelectedIndex = i;
                            return;
                        }

                base.SelectedValue = value;
            }
        }

        /// <summary>Gets or sets the drop-down style using Telerik's enum; maps onto the base ComboBox style.</summary>
        public new RadDropDownStyle DropDownStyle {
            get => base.DropDownStyle == ComboBoxStyle.DropDownList ? RadDropDownStyle.DropDownList : RadDropDownStyle.DropDown;
            set => base.DropDownStyle = value == RadDropDownStyle.DropDownList ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown;
        }

        /// <summary>Gets or sets whether the drop-down animates. No-op stub.</summary>
        public bool DropDownAnimationEnabled { get; set; } = true;
        /// <summary>Gets or sets the text shown, dimmed, while nothing is selected.</summary>
        /// <remarks>Real as of W6 mechanisms (#176): the editable style shows it as its placeholder, the
        /// drop-down-list style draws it in the box.</remarks>
        public string NullText {
            get => EmptyText;
            set => EmptyText = value;
        }
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Telerik-compat multi-select checked drop-down list. Backed by <see cref="Majorsilence.Forms.ComboBox"/>;
    /// the checked-item surface is provided on top.
    /// </summary>
    /// <remarks>
    /// As Telerik's (measured on UI.for.WinForms 2026.2.520): <see cref="Items"/> holds
    /// <see cref="RadCheckedListDataItem"/>s, a bound list included -- one per data item, carrying its
    /// display text, its <c>ValueMember</c> value and the data item itself; checking an item raises
    /// <see cref="ItemCheckedChanging"/> then <see cref="ItemCheckedChanged"/>; <see cref="CheckedItems"/>
    /// lists the checked items in the order they were checked, and its <c>Clear</c> unchecks them.
    /// </remarks>
    public class RadCheckedDropDownList : ComboBox, ISupportInitializeCompat
    {
        // The checked items, in the order they were checked.
        private readonly List<RadCheckedListDataItem> check_order = new ();

        // The item standing for each data item of a bound list, made the first time it is asked for.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, RadCheckedListDataItem> bound_items = new ();

        private RadCheckedListDataItemCollection? items;

        /// <summary>Gets the items, each a <see cref="RadCheckedListDataItem"/>.</summary>
        public new RadCheckedListDataItemCollection Items => items ??= new RadCheckedListDataItemCollection (this);

        // The list's own entries: the items added, or the data items of a bound list.
        internal ObjectCollection Entries => base.Items;

        // The item an entry is shown as.
        internal RadCheckedListDataItem ItemFor (object entry)
        {
            if (entry is RadCheckedListDataItem item) {
                item.Owner ??= this;
                return item;
            }

            return bound_items.GetValue (entry, data => new RadCheckedListDataItem (GetItemText (data),
                string.IsNullOrEmpty (ValueMember) ? data : DataSourceBinding.MemberValue (data, ValueMember)) {
                DataBoundItem = data,
                Owner = this
            });
        }

        // Where an item is in the list, or -1.
        internal int IndexOf (RadCheckedListDataItem item)
            => item.DataBoundItem is { } data ? Entries.IndexOf (data) : Entries.IndexOf (item);

        /// <summary>
        /// Gets the checked items, in the order they were checked. Its <c>Clear</c> unchecks every one.
        /// </summary>
        /// <remarks>An item checked through <see cref="SetItemChecked"/> that is not in the list is included.</remarks>
        public DropDownCheckedItemsCollection CheckedItems {
            get {
                var list = check_order.Where (i => i.Checked && (i.DataBoundItem is null || IndexOf (i) >= 0)).ToList ();
                return new DropDownCheckedItemsCollection (list);
            }
        }

        /// <summary>Gets or sets whether the drop-down animates. No-op stub.</summary>
        public bool DropDownAnimationEnabled { get; set; } = true;

        /// <summary>Gets or sets the text-block formatting behavior. Stub.</summary>
        public object? TextBlockFormatting { get; set; }

        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Checks or unchecks an item, which need not be in the list.</summary>
        public void SetItemChecked (RadCheckedListDataItem item, bool isChecked)
        {
            if (item is null)
                return;

            item.Owner ??= this;
            item.Checked = isChecked;
        }

        /// <summary>Finds the first item whose text is <paramref name="text"/>, or null.</summary>
        public RadCheckedListDataItem? FindItemExact (string text, bool caseSensitive = false)
        {
            var comparison = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

            foreach (var item in Items)
                if (string.Equals (item.Text, text, comparison))
                    return item;

            return null;
        }

        // An item of this list is about to change its check: a handler may refuse.
        internal bool RaiseItemCheckedChanging (RadCheckedListDataItem item)
        {
            if (ItemCheckedChanging is null)
                return true;

            var e = new RadCheckedListDataItemCancelEventArgs (item);
            ItemCheckedChanging.Invoke (this, e);
            return !e.Cancel;
        }

        // An item of this list changed its check.
        internal void RaiseItemCheckedChanged (RadCheckedListDataItem item)
        {
            check_order.Remove (item);

            if (item.Checked)
                check_order.Add (item);

            ItemCheckedChanged?.Invoke (this, new RadCheckedListDataItemEventArgs (item));
        }

        // An item joined the list already checked: it is checked from now.
        internal void Adopt (RadCheckedListDataItem item)
        {
            item.Owner = this;

            if (item.Checked && !check_order.Contains (item))
                check_order.Add (item);
        }

        /// <summary>Raised before an item's check changes. Set <c>Cancel</c> to keep it.</summary>
        public event EventHandler<RadCheckedListDataItemCancelEventArgs>? ItemCheckedChanging;

        /// <summary>Raised when an item's checked state changes.</summary>
        public event EventHandler<RadCheckedListDataItemEventArgs>? ItemCheckedChanged;
    }

    /// <summary>The items of a <see cref="RadCheckedDropDownList"/>, each a <see cref="RadCheckedListDataItem"/>.</summary>
    public class RadCheckedListDataItemCollection : IEnumerable<RadCheckedListDataItem>
    {
        private readonly RadCheckedDropDownList owner;

        internal RadCheckedListDataItemCollection (RadCheckedDropDownList owner) => this.owner = owner;

        /// <summary>Gets the number of items.</summary>
        public int Count => owner.Entries.Count;

        /// <summary>Gets the item at <paramref name="index"/>.</summary>
        public RadCheckedListDataItem this[int index] => owner.ItemFor (owner.Entries[index]!);

        /// <summary>Adds an item.</summary>
        public void Add (RadCheckedListDataItem item)
        {
            Guard.ThrowIfNull (item);
            owner.Entries.Add (item);
            owner.Adopt (item);
        }

        /// <summary>Adds an item with the text.</summary>
        public RadCheckedListDataItem Add (string text)
        {
            var item = new RadCheckedListDataItem (text);
            Add (item);
            return item;
        }

        /// <summary>Adds the items.</summary>
        public void AddRange (IEnumerable<RadCheckedListDataItem> range)
        {
            Guard.ThrowIfNull (range);

            foreach (var item in range)
                Add (item);
        }

        /// <summary>Inserts an item at <paramref name="index"/>.</summary>
        public void Insert (int index, RadCheckedListDataItem item)
        {
            Guard.ThrowIfNull (item);
            owner.Entries.Insert (index, item);
            owner.Adopt (item);
        }

        /// <summary>Removes an item.</summary>
        public void Remove (RadCheckedListDataItem item)
        {
            if (item is null)
                return;

            var index = owner.IndexOf (item);

            if (index >= 0)
                owner.Entries.RemoveAt (index);
        }

        /// <summary>Removes the item at <paramref name="index"/>.</summary>
        public void RemoveAt (int index) => owner.Entries.RemoveAt (index);

        /// <summary>Removes every item.</summary>
        public void Clear () => owner.Entries.Clear ();

        /// <summary>Returns where the item is, or -1.</summary>
        public int IndexOf (RadCheckedListDataItem item) => item is null ? -1 : owner.IndexOf (item);

        /// <summary>Returns whether the item is in the list.</summary>
        public bool Contains (RadCheckedListDataItem item) => IndexOf (item) >= 0;

        /// <inheritdoc/>
        public IEnumerator<RadCheckedListDataItem> GetEnumerator ()
        {
            // A snapshot: a handler may change the list while it is walked.
            var entries = owner.Entries.Cast<object> ().ToList ();

            foreach (var entry in entries)
                yield return owner.ItemFor (entry);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator () => GetEnumerator ();
    }

    /// <summary>The checked items of a <see cref="RadCheckedDropDownList"/>, in the order they were checked.</summary>
    public class DropDownCheckedItemsCollection : System.Collections.ObjectModel.ReadOnlyCollection<RadCheckedListDataItem>
    {
        internal DropDownCheckedItemsCollection (IList<RadCheckedListDataItem> items) : base (items) { }

        /// <summary>Unchecks every item, in the order they were checked, each raising the list's check events.</summary>
        public void Clear ()
        {
            foreach (var item in this.ToList ())
                item.Checked = false;
        }
    }

    /// <summary>Telerik-compat checked-list data item.</summary>
    public class RadCheckedListDataItem
    {
        /// <summary>Initializes a new, empty instance.</summary>
        public RadCheckedListDataItem () { }
        /// <summary>Initializes a new instance with the specified text.</summary>
        public RadCheckedListDataItem (string text) { Text = text; }
        /// <summary>Initializes a new instance with text and value.</summary>
        public RadCheckedListDataItem (string text, object? value) { Text = text; Value = value; }

        // The list the item is in (or was checked through).
        internal RadCheckedDropDownList? Owner { get; set; }

        /// <summary>Gets or sets the display text.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Gets or sets the value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets the data item this item stands for in a bound list, or null.</summary>
        public object? DataBoundItem { get; internal set; }
        /// <summary>Gets or sets the item tag.</summary>
        public object? Tag { get; set; }

        /// <summary>Gets where the item is in its list, or -1.</summary>
        public int Index => Owner?.IndexOf (this) ?? -1;

        private bool is_checked;

        /// <summary>Gets or sets whether the item is checked.</summary>
        /// <remarks>On an item of a list, a change raises the list's <see cref="RadCheckedDropDownList.ItemCheckedChanging"/>,
        /// which may refuse it, then <see cref="RadCheckedDropDownList.ItemCheckedChanged"/>.</remarks>
        public bool Checked {
            get => is_checked;
            set {
                if (is_checked == value)
                    return;

                if (Owner is { } owner && !owner.RaiseItemCheckedChanging (this))
                    return;

                is_checked = value;
                Owner?.RaiseItemCheckedChanged (this);
            }
        }

        /// <inheritdoc/>
        public override string ToString () => Text;
    }

    /// <summary>Provides data for the <see cref="RadCheckedDropDownList.ItemCheckedChanging"/> event.</summary>
    public class RadCheckedListDataItemCancelEventArgs : System.ComponentModel.CancelEventArgs
    {
        /// <summary>Initializes a new instance with the affected item.</summary>
        public RadCheckedListDataItemCancelEventArgs (RadCheckedListDataItem item) => Item = item;
        /// <summary>Gets the item whose checked state is about to change.</summary>
        public RadCheckedListDataItem Item { get; }
    }

    /// <summary>Provides data for the <see cref="RadCheckedDropDownList.ItemCheckedChanged"/> event.</summary>
    public class RadCheckedListDataItemEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance with the affected item.</summary>
        public RadCheckedListDataItemEventArgs (RadCheckedListDataItem item) => Item = item;
        /// <summary>Gets the item whose checked state changed.</summary>
        public RadCheckedListDataItem Item { get; }
    }

    /// <summary>Specifies the drop-down editing behavior of a Telerik drop-down control. Compat for Telerik RadDropDownStyle.</summary>
    public enum RadDropDownStyle
    {
        /// <summary>The text portion is editable and a drop-down list is offered.</summary>
        DropDown = 0,
        /// <summary>The text portion is read-only; only the drop-down list may be used.</summary>
        DropDownList = 1
    }

    /// <summary>
    /// Telerik-compat drop-down button: a push button that shows a menu of <see cref="RadMenuItem"/>s below
    /// itself when clicked. Backed by <see cref="Majorsilence.Forms.Button"/>.
    /// </summary>
    public class RadDropDownButton : Button, ISupportInitializeCompat
    {
        /// <summary>Gets the root element of the button (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Initializes a new instance of the RadDropDownButton class.</summary>
        public RadDropDownButton () => Click += (_, _) => ShowDropDown ();

        /// <summary>Gets the drop-down's menu items.</summary>
        public List<RadMenuItem> Items { get; } = new ();

        /// <summary>Gets or sets whether the drop-down arrow glyph is shown. Stub.</summary>
        public bool ShowArrow { get; set; } = true;

        /// <summary>Raised before the drop-down menu is shown, so handlers can populate <see cref="Items"/> lazily.</summary>
        public event EventHandler? DropDownOpening;

        private void ShowDropDown ()
        {
            DropDownOpening?.Invoke (this, EventArgs.Empty);

            if (Items.Count == 0)
                return;

            var menu = new ContextMenu ();
            foreach (var item in Items)
                menu.Items.Add (item);

            menu.Show (this, new Point (0, Height));
        }
    }
}
