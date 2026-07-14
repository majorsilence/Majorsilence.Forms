using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ComboBox control.
    /// </summary>
    public partial class ComboBox : Control
    {
        private PopupWindow? popup;
        private readonly ListBox popup_listbox;
        private bool suppress_popup_close;
        private object? _dataSource;
        private string _displayMember = string.Empty;
        private string _valueMember = string.Empty;

        /// <summary>
        /// Initializes a new instance of the ComboBox class.
        /// </summary>
        public ComboBox ()
        {
            popup_listbox = new ListBox { Dock = DockStyle.Fill, SelectItemOnMouseUp = true, ShowHover = true };
            popup_listbox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
        }

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Padding DefaultPadding => new Padding (4, 0, 3, 0);

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (120, 28);

        /// <summary>
        /// The default ControlStyle for all instances of ComboBox.
        /// </summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.Border.Width = 1;
                style.BackgroundColor = Theme.ControlMidColor;
            });

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);

            popup?.Close ();
            popup = null;

            popup_listbox.Dispose ();
        }

        /// <summary>
        /// Raised when the drop down portion of the ComboBox is closed.
        /// </summary>
        public event EventHandler? DropDownClosed;

        /// <summary>
        /// Raised when the drop down portion of the ComboBox is opened.
        /// </summary>
        public event EventHandler? DropDownOpened;

        /// <summary>Raised when the user commits a selection from the drop-down (not on programmatic changes).</summary>
        public event EventHandler? SelectionChangeCommitted;

        /// <summary>
        /// Gets or sets the appearance and behavior of the combo box.
        /// </summary>
        public ComboBoxStyle DropDownStyle { get; set; } = ComboBoxStyle.DropDown;

        /// <summary>
        /// Gets or sets whether items are formatted before display (WinForms compatibility stub).
        /// </summary>
        public bool FormattingEnabled { get; set; }

        /// <summary>Gets or sets the data source for the ComboBox.</summary>
        public object? DataSource {
            get => _dataSource;
            set {
                _dataSource = value;
                RefreshDataSource ();
            }
        }

        /// <summary>Gets or sets the property to display from the data source.</summary>
        public string DisplayMember {
            get => _displayMember;
            set {
                _displayMember = value ?? string.Empty;
                RefreshDataSource ();
            }
        }

        /// <summary>Gets or sets the property used as the value from the data source.</summary>
        public string ValueMember {
            get => _valueMember;
            set => _valueMember = value ?? string.Empty;
        }

        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection.")]
        private void RefreshDataSource ()
        {
            if (_dataSource is not IList list)
                return;

            Items.Clear ();

            foreach (var item in list) {
                if (!string.IsNullOrEmpty (_displayMember)) {
                    var prop = item?.GetType ().GetProperty (_displayMember);
                    Items.Add (prop?.GetValue (item)?.ToString () ?? item?.ToString () ?? string.Empty);
                } else {
                    Items.Add (item?.ToString () ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the drop down portion of the ComboBox is currently shown.
        /// </summary>
        public bool DroppedDown {
            get => popup?.Visible == true;
            set {
                if (DroppedDown && !value) {
                    popup?.Hide ();
                    OnDropDownClosed (EventArgs.Empty);
                } else if (!DroppedDown && value) {
                    if (FindWindow () is not WindowBase window)
                        throw new InvalidOperationException ("Cannot drop down a ComboBox that is not parented to a window");

                    popup ??= new PopupWindow (window) {
                        Size = new Size (Width, 102)
                    };

                    popup.Controls.Add (popup_listbox);

                    popup.Show (this, 1, Height);

                    OnDropDownOpened (EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Gets the collection of items contained by this ComboBox.
        /// </summary>
        public ListBoxItemCollection Items => popup_listbox.Items;

        // When the selected item of the popup ListBox changes, update the ComboBox
        private void ListBox_SelectedIndexChanged (object? sender, EventArgs e)
        {
            if (popup_listbox.SelectedIndex > -1) {
                // The drop-down is only open when the user is actively picking (mouse/keyboard); a
                // programmatic SelectedIndex/SelectedItem/Text change runs with it closed. Capture that
                // before closing so SelectionChangeCommitted fires only for user commits (WinForms).
                var userDriven = DroppedDown;

                if (!suppress_popup_close)
                    DroppedDown = false;

                Invalidate ();

                OnSelectedIndexChanged (e);

                if (userDriven)
                    OnSelectionChangeCommitted (e);
            }
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            base.OnClick (e);

            DroppedDown = !DroppedDown;
        }

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            DroppedDown = false;
        }

        /// <summary>
        /// Raises the DropDownClosed event.
        /// </summary>
        protected virtual void OnDropDownClosed (EventArgs e) => DropDownClosed?.Invoke (this, e);

        /// <summary>
        /// Raises the DropDownOpened event.
        /// </summary>
        protected virtual void OnDropDownOpened (EventArgs e) => DropDownOpened?.Invoke (this, e);

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            // Alt+Up/Down toggles the dropdown
            if (e.Alt && e.KeyCode.In (Keys.Up, Keys.Down)) {
                DroppedDown = !DroppedDown;
                e.Handled = true;
                return;
            }

            // If dropdown is shown, Esc/Enter will close it
            if (e.KeyCode.In (Keys.Escape, Keys.Enter) && DroppedDown) {
                DroppedDown = false;
                e.Handled = true;
                return;
            }

            // If you mouse click an item we automatically close the dropdown,
            // we don't want that behavior when using the keyboard.
            suppress_popup_close = true;
            popup_listbox.RaiseKeyUp (e);
            suppress_popup_close = false;

            if (e.Handled)
                return;

            base.OnKeyUp (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Raises the SelectedIndexChanged event.
        /// </summary>
        protected virtual void OnSelectedIndexChanged (EventArgs e)
        {
            SelectedIndexChanged?.Invoke (this, e);
            // SelectedValue is derived from SelectedIndex, so it changes whenever the index does.
            OnSelectedValueChanged (e);
        }

        /// <summary>Raises the SelectedValueChanged event.</summary>
        protected virtual void OnSelectedValueChanged (EventArgs e) => SelectedValueChanged?.Invoke (this, e);

        /// <summary>Raises the SelectionChangeCommitted event.</summary>
        protected virtual void OnSelectionChangeCommitted (EventArgs e) => SelectionChangeCommitted?.Invoke (this, e);

        /// <summary>
        /// Gets or sets the index of the currently selected item.  Returns -1 if no item is selected.
        /// </summary>
        public int SelectedIndex {
            get => popup_listbox.SelectedIndex;
            set => popup_listbox.SelectedIndex = value;
        }

        /// <summary>
        /// Gets or sets the currently selected item, if any.
        /// </summary>
        public object? SelectedItem {
            get => popup_listbox.SelectedItem;
            set => popup_listbox.SelectedItem = value;
        }

        /// <summary>
        /// Raised when the value of the SelectedIndex property changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>Raised when the SelectedValue property changes.</summary>
        public event EventHandler? SelectedValueChanged;

        /// <summary>Gets or sets the width of the drop-down list. 0 means match control width.</summary>
        public int DropDownWidth { get; set; }

        /// <summary>Gets or sets the maximum number of items shown in the drop-down list.</summary>
        public int MaxDropDownItems { get; set; } = 8;

        private bool _sorted;

        /// <summary>Gets or sets whether the combo box items are sorted alphabetically.</summary>
        public bool Sorted {
            get => _sorted;
            set {
                if (_sorted == value)
                    return;

                _sorted = value;

                if (_sorted)
                    SortItems ();
            }
        }

        // Sorts the current items in ascending order by their display text, matching
        // WinForms' behavior when Sorted is set to true. The selection is preserved.
        private void SortItems ()
        {
            if (Items.Count < 2)
                return;

            var selected = SelectedItem;

            var sorted = Items.Cast<object> ()
                              .OrderBy (i => GetItemText (i), StringComparer.CurrentCulture)
                              .ToList ();

            Items.Clear ();

            foreach (var item in sorted)
                Items.Add (item);

            if (selected is not null) {
                var index = Items.IndexOf (selected);
                if (index >= 0)
                    SelectedIndex = index;
            }
        }

        /// <summary>Gets or sets whether the selection is hidden when the control loses focus. Stub in Majorsilence.Forms.</summary>
        public bool HideSelection { get; set; } = true;

        /// <summary>Gets or sets the starting position of text selected in the editable portion. Stub in Majorsilence.Forms.</summary>
        public int SelectionStart { get; set; }

        /// <summary>Gets or sets the number of characters selected in the editable portion. Stub in Majorsilence.Forms.</summary>
        public int SelectionLength { get; set; }

        /// <summary>Gets or sets the text in the editable portion of the ComboBox.</summary>
        public string SelectedText {
            get => SelectionLength > 0 && SelectionStart >= 0 ? Text.Substring (SelectionStart, Math.Min (SelectionLength, Text.Length - SelectionStart)) : string.Empty;
            set { }
        }

        /// <summary>Gets or sets the maximum number of characters that can be entered in the editable portion. Stub in Majorsilence.Forms.</summary>
        public int MaxLength { get; set; }

        /// <summary>Gets or sets whether the height of the ComboBox is limited to prevent partial items. Stub in Majorsilence.Forms.</summary>
        public bool IntegralHeight { get; set; } = true;

        /// <summary>Selects a range of text in the editable portion of the ComboBox. Stub in Majorsilence.Forms.</summary>
        public void Select (int start, int length) { SelectionStart = start; SelectionLength = length; }

        /// <summary>Gets or sets the height in pixels of the drop-down portion. Stub in Majorsilence.Forms.</summary>
        public int DropDownHeight { get; set; } = 106;

        /// <summary>Gets or sets the height of each item in the combo box. Stub in Majorsilence.Forms.</summary>
        public int ItemHeight { get; set; } = 15;

        /// <summary>Gets or sets the auto-complete mode. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteMode AutoCompleteMode { get; set; } = AutoCompleteMode.None;

        /// <summary>Gets or sets the source of auto-complete strings. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

        /// <summary>Gets or sets the custom source for auto-complete strings. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = new AutoCompleteStringCollection ();

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "DataSource item types require runtime reflection — same as WinForms.")]
        private static object? GetPropValue (object? item, string prop) => item?.GetType ().GetProperty (prop)?.GetValue (item);

        /// <summary>Gets or sets the selected value (uses ValueMember if set).</summary>
        public object? SelectedValue {
            get {
                if (SelectedIndex < 0 || DataSource is not System.Collections.IList list || SelectedIndex >= list.Count)
                    return SelectedItem;

                var item = list[SelectedIndex];

                return string.IsNullOrEmpty (ValueMember) ? item : GetPropValue (item, ValueMember);
            }
            set {
                if (DataSource is not System.Collections.IList list || value == null) {
                    SelectedItem = value;
                    return;
                }

                for (int i = 0; i < list.Count; i++) {
                    var item_value = string.IsNullOrEmpty (ValueMember) ? list[i] : GetPropValue (list[i], ValueMember);

                    if (Equals (item_value, value)) {
                        SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        /// <summary>Prevents the control from drawing until EndUpdate is called.</summary>
        public new void BeginUpdate () => SuspendLayout ();

        /// <summary>Resumes drawing the control after BeginUpdate.</summary>
        public new void EndUpdate () { ResumeLayout (false); Invalidate (); }

        /// <summary>Returns the display text for the given item, using DisplayMember if set.</summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "DataSource item types require runtime reflection — same as WinForms.")]
        public string GetItemText (object? item)
        {
            if (item is null) return string.Empty;
            if (!string.IsNullOrEmpty (DisplayMember)) {
                var prop = item.GetType ().GetProperty (DisplayMember);
                if (prop != null) return prop.GetValue (item)?.ToString () ?? string.Empty;
            }
            return item.ToString () ?? string.Empty;
        }

        /// <summary>Finds the first item that exactly matches the given string (case-insensitive).</summary>
        public int FindStringExact (string s, int startIndex = -1)
        {
            var items = Items;
            int start = startIndex < 0 ? 0 : startIndex + 1;
            for (int i = 0; i < items.Count; i++) {
                int idx = (start + i) % items.Count;
                if (string.Equals (GetItemText (items[idx]), s, StringComparison.OrdinalIgnoreCase))
                    return idx;
            }
            return -1;
        }

        /// <inheritdoc/>
        public override string Text {
            get {
                if (SelectedIndex >= 0)
                    return GetItemText (SelectedItem);
                return base.Text;
            }
            set {
                int idx = FindStringExact (value);
                if (idx >= 0)
                    SelectedIndex = idx;
                else
                    base.Text = value;
            }
        }

        /// <summary>Gets or sets the drawing mode for the elements of the ComboBox. Stub in Majorsilence.Forms.</summary>
        public DrawMode DrawMode { get; set; } = DrawMode.Normal;

        /// <summary>Raised when an owner-drawn element needs to be drawn. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<DrawItemEventArgs>? DrawItem { add { } remove { } }

        /// <summary>Raised when an owner-drawn element needs to be measured. Stub in Majorsilence.Forms.</summary>
        public event EventHandler<MeasureItemEventArgs>? MeasureItem { add { } remove { } }

        /// <summary>Finds the first item starting with the given string (case-insensitive).</summary>
        public int FindString (string s, int startIndex = -1)
        {
            var items = Items;
            int start = startIndex < 0 ? 0 : startIndex + 1;
            for (int i = 0; i < items.Count; i++) {
                int idx = (start + i) % items.Count;
                var text = GetItemText (items[idx]);
                if (text.StartsWith (s, StringComparison.OrdinalIgnoreCase))
                    return idx;
            }
            return -1;
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }

    /// <summary>
    /// Specifies the appearance and behavior of a ComboBox.
    /// </summary>
    public enum ComboBoxStyle
    {
        /// <summary>Text portion is editable; list opens on arrow click.</summary>
        DropDown,
        /// <summary>Text portion is read-only; list opens on arrow click.</summary>
        DropDownList,
        /// <summary>Text portion is editable; list is always visible.</summary>
        Simple
    }
}
