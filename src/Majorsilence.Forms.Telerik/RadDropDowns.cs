using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>Telerik-compat drop-down list. Backed by <see cref="Majorsilence.Forms.ComboBox"/>.</summary>
    public class RadDropDownList : ComboBox
    {
        /// <summary>Gets or sets the drop-down style using Telerik's enum; maps onto the base ComboBox style.</summary>
        public new RadDropDownStyle DropDownStyle {
            get => base.DropDownStyle == ComboBoxStyle.DropDownList ? RadDropDownStyle.DropDownList : RadDropDownStyle.DropDown;
            set => base.DropDownStyle = value == RadDropDownStyle.DropDownList ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown;
        }

        /// <summary>Gets or sets whether the drop-down animates. No-op stub.</summary>
        public bool DropDownAnimationEnabled { get; set; } = true;
        /// <summary>Gets or sets the text shown when nothing is selected. Stub.</summary>
        public string NullText { get; set; } = string.Empty;
        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();
        /// <summary>Gets or sets the theme name. No-op stub.</summary>
        public string ThemeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Telerik-compat multi-select checked drop-down list. Backed by <see cref="Majorsilence.Forms.ComboBox"/>;
    /// the checked-item surface is provided on top.
    /// </summary>
    public class RadCheckedDropDownList : ComboBox
    {
        private readonly List<RadCheckedListDataItem> _checkedItems = new ();

        /// <summary>Gets the collection of checked items.</summary>
        public IReadOnlyList<RadCheckedListDataItem> CheckedItems => _checkedItems;

        /// <summary>Gets or sets whether the drop-down animates. No-op stub.</summary>
        public bool DropDownAnimationEnabled { get; set; } = true;

        /// <summary>Gets or sets the text-block formatting behavior. Stub.</summary>
        public object? TextBlockFormatting { get; set; }

        /// <summary>Gets the root element of the control (stub).</summary>
        public RadElement RootElement { get; } = new RadElement ();

        /// <summary>Marks an item as checked and raises <see cref="ItemCheckedChanged"/>.</summary>
        public void SetItemChecked (RadCheckedListDataItem item, bool isChecked)
        {
            if (item is null)
                return;

            item.Checked = isChecked;

            if (isChecked) {
                if (!_checkedItems.Contains (item))
                    _checkedItems.Add (item);
            } else {
                _checkedItems.Remove (item);
            }

            ItemCheckedChanged?.Invoke (this, new RadCheckedListDataItemEventArgs (item));
        }

        /// <summary>Finds the first item whose text exactly matches, or null. Stub-ish search over Items.</summary>
        public object? FindItemExact (string text, bool caseSensitive = false)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (var item in Items)
                if (string.Equals (GetItemText (item), text, comparison))
                    return item;

            return null;
        }

        /// <summary>Raised when an item's checked state changes.</summary>
        public event EventHandler<RadCheckedListDataItemEventArgs>? ItemCheckedChanged;
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

        /// <summary>Gets or sets the display text.</summary>
        public string Text { get; set; } = string.Empty;
        /// <summary>Gets or sets the value.</summary>
        public object? Value { get; set; }
        /// <summary>Gets or sets whether the item is checked.</summary>
        public bool Checked { get; set; }
        /// <summary>Gets or sets the item tag.</summary>
        public object? Tag { get; set; }

        /// <inheritdoc/>
        public override string ToString () => Text;
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
    public class RadDropDownButton : Button
    {
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
