using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ComboBox control.
    /// </summary>
    public partial class ComboBox : ListControl
    {
        private PopupWindow? popup;
        private readonly ListBox popup_listbox;
        private readonly ComboBoxEdit edit;
        private bool suppress_popup_close;
        // Guards the two-way sync between the edit region's text and this control's Text. Either side
        // assigning the other raises TextChanged, which would come straight back.
        private bool syncing_edit_text;
        private object? _dataSource;
        private string _displayMember = string.Empty;
        private string _valueMember = string.Empty;

        /// <summary>
        /// Initializes a new instance of the ComboBox class.
        /// </summary>
        public ComboBox ()
        {
            popup_listbox = new PopupList { Dock = DockStyle.Fill, SelectItemOnMouseUp = true, ShowHover = true };
            popup_listbox.SelectedIndexChanged += ListBox_SelectedIndexChanged;

            // The inner list holds the items but the DATA SOURCE belongs to the combo, so the combo does
            // its own tracking; the inner list's own tracker never sees a source.
            source_tracker = new DataSourceBinding.ListSourceTracker (
                RefreshDataSource,
                // See the matching lambda in ListBox: an early position for items not yet reloaded is
                // dropped here and re-applied by the reload.
                position => { if (position < Items.Count) SelectedIndex = position; },
                () => SelectedIndex);

            // The editable region is a real child TextBox, the way WinForms hosts a real edit control
            // inside a combo -- so the caret, selection, undo, clipboard and mouse text selection are
            // the ones TextBox already implements instead of a second, thinner copy of them (LST-07).
            // It is an IMPLICIT child: invisible to the public Controls collection, and skipped by tab
            // order, because the combo itself is the tab stop as upstream. It is built here for every
            // style and merely hidden for DropDownList, rather than created and destroyed as the style
            // changes -- implicit children are meant to be added in constructors (see
            // ControlCollection.AddImplicitControl), and a single instance means MaxLength and the
            // selection survive a style switch.
            edit = Controls.AddImplicitControl (new ComboBoxEdit (this));
            edit.Visible = IsEditable;
            edit.TextChanged += EditTextChanged;
        }

        // The combo's edit region. A TextBox subclass rather than a plain one because two things have
        // to be intercepted: the keys the LIST owns even while the caret is in here, and the moment a
        // character lands, which is when autocompletion runs.
        private sealed class ComboBoxEdit : TextBox
        {
            private readonly ComboBox owner;

            internal ComboBoxEdit (ComboBox owner)
            {
                this.owner = owner;

                // The combo paints the frame; a second border inside it reads as a box in a box.
                Style.Border.Width = 0;
            }

            /// <inheritdoc/>
            protected override void OnKeyDown (KeyEventArgs e)
            {
                // In a WinForms DropDown combo, Up/Down move the SELECTION, not the caret, and
                // Enter/Escape belong to the drop-down. TextBox's own key handling would consume all
                // of them for caret movement and report them handled, so they never reach the combo --
                // which acts on them in its OnKeyUp, where its list navigation already lives.
                if (IsListKey (e))
                    return;

                base.OnKeyDown (e);
            }

            /// <inheritdoc/>
            protected override void OnKeyUp (KeyEventArgs e)
            {
                base.OnKeyUp (e);

                if (!e.Handled)
                    owner.RaiseKeyUp (e);
            }

            // Alt+arrow toggles the drop-down; the rest navigate or commit it.
            private static bool IsListKey (KeyEventArgs e)
                => e.Alt ? e.KeyCode.In (Keys.Up, Keys.Down)
                         : e.KeyCode.In (Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Escape, Keys.Enter);

            /// <inheritdoc/>
            protected override bool InsertTypedCharacter (KeyPressEventArgs e)
            {
                var inserted = base.InsertTypedCharacter (e);

                if (inserted)
                    owner.CompleteTypedText ();

                return inserted;
            }
        }

        /// <summary>Whether the combo has an editable text region: every style except DropDownList.</summary>
        internal bool IsEditable => drop_down_style != ComboBoxStyle.DropDownList;

        // The renderer needs to know whether the child is painting the text, and the tests need to
        // reach the region the caret lives in.
        internal TextBox EditRegion => edit;

        // The list a combo box drops down is a real ListBox, and the combo's items are that list's items.
        // This subclass exists only so the collection they live in is a ComboBox.ObjectCollection -- the
        // type name WinForms code uses for a combo's items -- rather than the list box's own.
        private sealed class PopupList : ListBox
        {
            // ComboBox.ObjectCollection spelled in full deliberately. Unqualified, `ObjectCollection`
            // binds to the one inherited from ListBox -- a base class is searched before the enclosing
            // class -- so this silently built the wrong type and the cast in ComboBox.Items threw.
            protected override ListBox.ObjectCollection CreateItemCollection () => new ComboBox.ObjectCollection (this);
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
        public ComboBoxStyle DropDownStyle {
            get => drop_down_style;
            set {
                if (drop_down_style == value)
                    return;

                drop_down_style = value;

                // DropDownList is the one style with no text region. Showing or hiding the child is
                // the whole difference between a combo you can type into and one you cannot -- which
                // is why the two styles used to look and behave identically (LST-07).
                edit.Visible = IsEditable;
                PerformLayout ();

                DropDownStyleChanged?.Invoke (this, EventArgs.Empty);
                Invalidate ();
            }
        }

        private ComboBoxStyle drop_down_style = ComboBoxStyle.DropDown;

        /// <summary>Raised when the drop-down portion is shown.</summary>
        /// <remarks>The counterpart of <see cref="DropDownClosed"/>. <see cref="DropDownOpened"/> is
        /// raised alongside it; both exist because this control had the latter before the WinForms
        /// name was added, and removing it would break code already using it.</remarks>
        public event EventHandler? DropDown;

        /// <summary>Raised when <see cref="DropDownStyle"/> changes.</summary>
        public event EventHandler? DropDownStyleChanged;

        /// <summary>Raised when the text changes because the user edited it, rather than because the
        /// selection changed.</summary>
        public event EventHandler? TextUpdate;

        /// <summary>Gets or sets the flat-style appearance of the combo box.</summary>
        public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

        /// <summary>The logical width the drop-down glyph strip occupies on the right.</summary>
        /// <remarks>Lives on the control, not the renderer, because the edit region's bounds and the
        /// painted text area have to agree exactly -- two definitions of the same 15 pixels is how a
        /// caret ends up drawn under the glyph. <c>ComboBoxRenderer.GLYPH_SIZE</c> is this value.</remarks>
        internal const int DropDownGlyphWidth = 15;

        /// <summary>The text area, in DEVICE pixels: what the renderer paints into.</summary>
        internal Rectangle EditAreaDeviceBounds {
            get {
                var area = PaddedClientRectangle;
                area.Width -= LogicalToDeviceUnits (DropDownGlyphWidth);

                return area;
            }
        }

        /// <summary>The same area in LOGICAL units: what the child edit control's Bounds need.</summary>
        /// <remarks>Child bounds are logical while <see cref="Control.PaddedClientRectangle"/> is device --
        /// the same conversion <c>DataGridView</c> does when it positions a cell editor.</remarks>
        internal Rectangle EditAreaLogicalBounds {
            get {
                var area = EditAreaDeviceBounds;

                return new Rectangle (DeviceToLogicalUnits (area.Left), DeviceToLogicalUnits (area.Top),
                                      DeviceToLogicalUnits (area.Width), DeviceToLogicalUnits (area.Height));
            }
        }

        /// <summary>Keeps the edit region over the area the renderer treats as the text area.</summary>
        /// <remarks>Derived rather than stored, for the reason <c>NumericUpDown.OnLayout</c> gives: the
        /// area is a function of the control's size, so every resize has to move it and a layout pass
        /// is the one place that reliably runs for all of them.</remarks>
        protected override void OnLayout (LayoutEventArgs e)
        {
            base.OnLayout (e);

            edit.Bounds = EditAreaLogicalBounds;
        }

        /// <summary>Gets the height one line of the combo box needs at the current font.</summary>
        public int PreferredHeight
            => (int)Math.Ceiling (TextMeasurer.MeasureText ("Wg", this).Height) + Padding.Top + Padding.Bottom + 6;

        /// <summary>Gets the height of the item at the specified index.</summary>
        /// <remarks>Every item is the same height here; <see cref="DrawMode"/>'s variable-height mode
        /// is not implemented, so the index is accepted and validated but does not change the answer.</remarks>
        public int GetItemHeight (int index)
        {
            Guard.ThrowIfNegative (index);
            return ItemHeight;
        }

        /// <summary>Raises the <see cref="TextUpdate"/> event.</summary>
        protected virtual void OnTextUpdate (EventArgs e) => TextUpdate?.Invoke (this, e);

        /// <summary>Raises the <see cref="DropDown"/> event.</summary>
        protected virtual void OnDropDown (EventArgs e) => DropDown?.Invoke (this, e);

        /// <summary>
        /// Gets or sets whether items are formatted before display (WinForms compatibility stub).
        /// </summary>
        public override bool FormattingEnabled { get; set; }

        /// <summary>Gets or sets the data source for the ComboBox.</summary>
        public override object? DataSource {
            get => _dataSource;
            set {
                _dataSource = value;
                source_tracker.Attach (value);
                RefreshDataSource ();
            }
        }

        // See ListBox: re-reads the source when it changes and keeps the selection and the source's
        // current-item position in step.
        private readonly DataSourceBinding.ListSourceTracker source_tracker;

        /// <summary>Gets or sets the property to display from the data source.</summary>
        public override string DisplayMember {
            get => _displayMember;
            set {
                _displayMember = value ?? string.Empty;
                RefreshDataSource ();
            }
        }

        /// <summary>Gets or sets the property used as the value from the data source.</summary>
        public override string ValueMember {
            get => _valueMember;
            set => _valueMember = value ?? string.Empty;
        }

        [UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "Data binding requires runtime reflection.")]
        private void RefreshDataSource ()
        {
            var list = DataSourceBinding.AsList (_dataSource);

            if (list is null)
                return;

            Items.Clear ();

            // The BOUND OBJECTS go in, not their display text: WinForms keeps the source items as the
            // control's items (so SelectedItem is e.g. a DataRowView the caller can cast) and uses
            // DisplayMember only when rendering, via GetItemText. Storing strings instead made
            // SelectedItem a String and broke every cast in a SelectedIndexChanged handler.
            foreach (var item in list)
                Items.Add (item);

            // Items live on the popup list, which renders them through its own GetItemText -- it needs
            // the same DisplayMember or it would fall back to ToString on the bound object.
            popup_listbox.DisplayMember = _displayMember;

            // WinForms selects the first row when a non-empty source is bound; without this, code that
            // binds a source and immediately reads/sets the selection sees an unselected control.
            if (Items.Count > 0 && SelectedIndex < 0)
                SelectedIndex = 0;
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

                    popup ??= new PopupWindow (window);

                    popup.Controls.Add (popup_listbox);
                    popup.Size = ComputePopupSize ();

                    popup.Show (this, 1, Height);

                    OnDropDownOpened (EventArgs.Empty);
                }
            }
        }

        // The drop-down list's size. Width: DropDownWidth when set, otherwise the widest item text
        // (never narrower than the control, and capped so a stray long entry can't throw a list off
        // the screen). Height: MaxDropDownItems rows, or fewer when there are fewer items. The old
        // code hard-coded Size(Width, 102), which clipped every item longer than the control -- the
        // font-family pickers in ReportDesigner showed "Times New Ro…".
        internal Size ComputePopupSize ()
        {
            var itemHeight = popup_listbox.ItemHeight;
            var rows = Math.Max (1, Math.Min (Items.Count, MaxDropDownItems));
            var height = rows * itemHeight + 2;   // + the 1px popup border top and bottom

            int width;
            if (DropDownWidth > 0) {
                width = DropDownWidth;
            } else {
                width = Width;
                foreach (var item in Items) {
                    var text = GetItemText (item);
                    if (string.IsNullOrEmpty (text))
                        continue;
                    var w = (int)Math.Ceiling (TextMeasurer.MeasureText (text, popup_listbox).Width) + 6;
                    if (w > width)
                        width = w;
                }

                if (Items.Count > MaxDropDownItems)
                    width += SystemInformation.VerticalScrollBarWidth;

                width = Math.Min (width, Math.Max (Width * 3, 480));
            }

            return new Size (width, height);
        }

        // The nested ObjectCollection needs the list the items actually live on.
        internal ListBox PopupListBox => popup_listbox;

        /// <summary>
        /// Gets the collection of items contained by this ComboBox.
        /// </summary>
        /// <remarks>Typed as the nested <see cref="ObjectCollection"/>, the name WinForms code writes when
        /// it re-exposes a combo's items. The cast always holds: <see cref="PopupList"/> is the only thing
        /// that builds this collection and it builds exactly this type.</remarks>
        public ObjectCollection Items => (ObjectCollection)popup_listbox.Items;

        // When the selected item of the popup ListBox changes, update the ComboBox
        private void ListBox_SelectedIndexChanged (object? sender, EventArgs e)
        {
            // Move the bound source's current item with the selection, so a BindingSource driving a
            // detail view follows what the user picked here.
            source_tracker.OnSelectionChanged (popup_listbox.SelectedIndex);

            var index = popup_listbox.SelectedIndex;

            // Only the popup-closing and commit logic is index-conditional. The raise used to be too
            // (`if (index > -1)`), so clearing the selection announced nothing: a "Clear filter" button
            // setting SelectedIndex = -1 left every dependent control showing the old choice, and the
            // bound source was never moved off it (LST-06).
            // The drop-down is only open when the user is actively picking (mouse/keyboard); a
            // programmatic SelectedIndex/SelectedItem/Text change runs with it closed. Captured before
            // closing so SelectionChangeCommitted fires only for user commits (WinForms).
            var userDriven = index > -1 && DroppedDown;

            if (index > -1 && !suppress_popup_close)
                DroppedDown = false;

            Invalidate ();

            // The combo's Text IS its selection, and Control.Text is the only thing that raises
            // TextChanged -- which nothing wrote, so TextChanged never fired for a combo at all. That
            // is what validation, dirty-tracking and a Binding on Text all listen to (LST-09).
            // Assigned through SetTextCore, not base.Text: the edit region has to show the newly
            // selected item too, and not through this class's own Text setter, which would resolve the
            // string back to an index and recurse.
            SetTextCore (index >= 0 ? GetItemText (SelectedItem) : string.Empty);

            OnSelectedIndexChanged (e);

            if (userDriven)
                OnSelectionChangeCommitted (e);
        }

        /// <inheritdoc/>
        protected override void OnClick (EventArgs e)
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
        protected virtual void OnDropDownOpened (EventArgs e)
        {
            DropDownOpened?.Invoke (this, e);
            OnDropDown (e);     // the WinForms-named event; see DropDown
        }

        /// <summary>Puts a typed character into the edit region.</summary>
        /// <remarks>
        /// The combo is the tab stop and the edit region is implicit, so a keyboard-focused combo -- and
        /// any caller raising input at the control, which is how WinForms code and the tests both treat
        /// a combo -- delivers characters here rather than to the child. Real focus inside the child,
        /// after a click in the text area, bypasses this: key input goes straight to the focused
        /// control (see <c>Control.RaiseKeyPress</c>), so there is no double insert.
        /// </remarks>
        protected override void OnKeyPress (KeyPressEventArgs e)
        {
            base.OnKeyPress (e);

            if (IsEditable && !edit.Selected)
                edit.RaiseKeyPress (e);
        }

        /// <inheritdoc cref="OnKeyPress" path="/remarks"/>
        protected override void OnKeyDown (KeyEventArgs e)
        {
            base.OnKeyDown (e);

            // Editing keys only. Up/Down/Enter/Escape stay with the LIST -- they are acted on in
            // OnKeyUp below -- which is why this cannot simply forward everything.
            if (IsEditable && !edit.Selected && !e.Handled
                && e.KeyCode.In (Keys.Back, Keys.Delete, Keys.Left, Keys.Right, Keys.Home, Keys.End))
                edit.RaiseKeyDown (e);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            // Enter commits what was typed: resolving it against the items is the Text setter's job, so
            // a typed "item3" selects item 3 instead of leaving the control text-only (LST-07). Before
            // the branches below, which close the drop-down and return.
            //
            // Unconditional, deliberately. Guarding it with `edit.Text != base.Text` looks like an
            // obvious short-circuit and is dead code: the edit region's TextChanged has already
            // written base.Text, so the two are always equal here and the commit never ran.
            if (e.KeyCode == Keys.Enter && IsEditable)
                Text = edit.Text;

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
            // SelectedItem and SelectedValue are both derived from SelectedIndex, so they change
            // whenever the index does -- matching WinForms' own OnSelectedIndexChanged, which raises
            // both from the same place.
            OnSelectedItemChanged (e);
            OnSelectedValueChanged (e);
        }

        /// <summary>Raised when the selected item changes.</summary>
        public event EventHandler? SelectedItemChanged;

        /// <summary>Raises the <see cref="SelectedItemChanged"/> event.</summary>
        protected virtual void OnSelectedItemChanged (EventArgs e) => SelectedItemChanged?.Invoke (this, e);

        /// <summary>Raises the SelectionChangeCommitted event.</summary>
        protected virtual void OnSelectionChangeCommitted (EventArgs e) => SelectionChangeCommitted?.Invoke (this, e);

        /// <summary>
        /// Gets or sets the index of the currently selected item.  Returns -1 if no item is selected.
        /// </summary>
        public override int SelectedIndex {
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

        // The four members below forward to the edit region unconditionally, including for
        // DropDownList where it is hidden. That is deliberate: one store means a style switch carries
        // MaxLength and the selection with it, and it is the edit region's own document that defines
        // what these mean (see TextBox.SelectionStart's remarks on the caret-vs-anchor question).
        // They were stored ints that nothing read, so SelectAll left SelectionLength at 0 (LST-07).

        /// <summary>Gets or sets the starting position of text selected in the editable portion.</summary>
        public int SelectionStart {
            get => edit.SelectionStart;
            set => edit.SelectionStart = value;
        }

        /// <summary>Gets or sets the number of characters selected in the editable portion.</summary>
        public int SelectionLength {
            get => edit.SelectionLength;
            set => edit.SelectionLength = value;
        }

        /// <summary>Gets or sets the text in the editable portion of the ComboBox.</summary>
        /// <remarks>Setting it replaces the selection, or inserts at the caret when nothing is
        /// selected, as <c>TextBoxBase.SelectedText</c> does.</remarks>
        public string SelectedText {
            get => edit.SelectedText;
            set => edit.SelectedText = value ?? string.Empty;
        }

        /// <summary>Gets or sets the maximum number of characters that can be entered in the editable portion.</summary>
        public int MaxLength {
            get => edit.MaxLength;
            set => edit.MaxLength = value;
        }

        /// <summary>Gets or sets whether the height of the ComboBox is limited to prevent partial items. Stub in Majorsilence.Forms.</summary>
        public bool IntegralHeight { get; set; } = true;

        /// <summary>Selects a range of text in the editable portion of the ComboBox.</summary>
        public void Select (int start, int length) { SelectionStart = start; SelectionLength = length; }

        /// <summary>Gets or sets the height in pixels of the drop-down portion. Stub in Majorsilence.Forms.</summary>
        public int DropDownHeight { get; set; } = 106;

        /// <summary>Gets or sets the height of each item in the combo box. Stub in Majorsilence.Forms.</summary>
        public int ItemHeight { get; set; } = 15;

        /// <summary>Gets or sets the auto-complete mode.</summary>
        /// <remarks><see cref="AutoCompleteMode.Append"/> and the append half of
        /// <see cref="AutoCompleteMode.SuggestAppend"/> complete inline as you type. The filtered
        /// drop-down of <see cref="AutoCompleteMode.Suggest"/> is not implemented -- see
        /// <see cref="CompleteTypedText"/> for why it cannot be, as this control is built.</remarks>
        public AutoCompleteMode AutoCompleteMode { get; set; } = AutoCompleteMode.None;

        /// <summary>Gets or sets the source of auto-complete strings.</summary>
        /// <remarks><see cref="AutoCompleteSource.ListItems"/> and
        /// <see cref="AutoCompleteSource.CustomSource"/> are honoured. The rest name operating-system
        /// stores (the file system, the shell's URL history) that have no portable meaning here, and
        /// complete nothing.</remarks>
        public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

        // Completes the entry inline and selects the part the user did not type, so the next keystroke
        // replaces it -- AutoCompleteMode.Append. Called from the edit region the moment a character
        // lands, not from its TextChanged, so a programmatic assignment never triggers completion.
        //
        // Suggest's filtered drop-down is absent by construction, not by omission: this control's items
        // ARE the popup ListBox's items (see the Items property), so narrowing what the popup shows
        // would mean deleting the combo's own items and putting them back. That needs a separate
        // presentation list, which is its own change.
        private void CompleteTypedText ()
        {
            if (AutoCompleteMode != AutoCompleteMode.Append && AutoCompleteMode != AutoCompleteMode.SuggestAppend)
                return;

            var typed = edit.Text;

            if (typed.Length == 0)
                return;

            var completion = FindCompletion (typed);

            // A completion no longer than what was typed adds nothing -- and selecting a zero-length
            // remainder would silently clear the caret's selection.
            if (completion is null || completion.Length <= typed.Length)
                return;

            syncing_edit_text = true;

            try {
                edit.Text = completion;
            } finally {
                syncing_edit_text = false;
            }

            base.Text = completion;

            // TextBox.Text resets the caret to 0, so the selection is applied after the assignment.
            edit.SelectionStart = typed.Length;
            edit.SelectionLength = completion.Length - typed.Length;
        }

        // The first entry in the configured source that starts with what was typed, or null.
        private string? FindCompletion (string typed)
        {
            if (AutoCompleteSource == AutoCompleteSource.ListItems) {
                var idx = FindString (typed);

                return idx >= 0 ? GetItemText (Items[idx]) : null;
            }

            if (AutoCompleteSource != AutoCompleteSource.CustomSource)
                return null;

            foreach (string? candidate in AutoCompleteCustomSource)
                if (candidate is not null && candidate.StartsWith (typed, StringComparison.CurrentCultureIgnoreCase))
                    return candidate;

            return null;
        }

        /// <summary>Gets or sets the custom source for auto-complete strings. Stub in Majorsilence.Forms.</summary>
        public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = new AutoCompleteStringCollection ();

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "DataSource item types require runtime reflection — same as WinForms.")]
        private static object? GetPropValue (object? item, string prop) => DataSourceBinding.MemberValue (item, prop);

        /// <summary>Gets or sets the selected value (uses ValueMember if set).</summary>
        public override object? SelectedValue {
            get {
                var list = DataSourceBinding.AsList (DataSource);

                if (SelectedIndex < 0 || list is null || SelectedIndex >= list.Count)
                    return SelectedItem;

                var item = list[SelectedIndex];

                return string.IsNullOrEmpty (ValueMember) ? item : GetPropValue (item, ValueMember);
            }
            set {
                var list = DataSourceBinding.AsList (DataSource);

                if (list is null || value == null) {
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
        public override string GetItemText (object? item)
        {
            // Property descriptors first so DataRowView columns resolve; see DataSourceBinding.
            return DataSourceBinding.DisplayText (item, DisplayMember);
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
        /// <remarks>
        /// Mirrors WinForms (finding <c>LST-08</c>). The getter used to return the selected item's text
        /// whenever anything was selected, so <c>Text = "custom"</c> on a combo with a selection stored
        /// the string and then read back the item -- restoring a saved free-text value silently showed
        /// the previously selected entry. It reports <c>base.Text</c> now, and every path that changes
        /// the selection writes it (see <see cref="SetTextCore"/>), so the two cannot drift.
        /// A null assignment clears the selection, which is the documented WinForms idiom for it; a
        /// value that matches no item keeps the text and leaves the selection alone.
        /// </remarks>
        public override string Text {
            get => base.Text;
            set {
                SetTextCore (value ?? string.Empty);

                if (value is null) {
                    SelectedIndex = -1;
                    return;
                }

                var idx = FindStringExact (value);

                if (idx >= 0)
                    SelectedIndex = idx;
            }
        }

        // The one place this control's text is written. Keeps the edit region and Control.Text in step
        // -- Control.Text is what raises TextChanged, and the edit region is what the user sees.
        private void SetTextCore (string value)
        {
            if (!syncing_edit_text) {
                syncing_edit_text = true;

                try {
                    edit.Text = value;
                } finally {
                    syncing_edit_text = false;
                }
            }

            base.Text = value;
        }

        // The edit region changed under the user's fingers.
        private void EditTextChanged (object? sender, EventArgs e)
        {
            if (syncing_edit_text || !IsEditable)
                return;

            // TextUpdate FIRST: upstream raises it from CBN_EDITUPDATE, which Windows sends before the
            // CBN_EDITCHANGE that becomes TextChanged. Assigning base.Text below is what raises
            // TextChanged, so doing this first is what puts them in the upstream order.
            OnTextUpdate (EventArgs.Empty);

            syncing_edit_text = true;

            try {
                base.Text = edit.Text;
            } finally {
                syncing_edit_text = false;
            }
        }

        /// <summary>Gets or sets the drawing mode for the elements of the ComboBox. Stub in Majorsilence.Forms.</summary>
        public DrawMode DrawMode { get; set; } = DrawMode.Normal;

        /// <summary>Raised when an owner-drawn element needs to be drawn.</summary>
        /// <remarks>
        /// The event is real and <see cref="OnDrawItem"/> is overridable, so a control deriving from
        /// ComboBox to paint its own items compiles and its hook is reachable. The built-in item
        /// rendering does not yet call it, so an owner-drawn combo still paints normally — see the
        /// compatibility matrix.
        /// </remarks>
        public event DrawItemEventHandler? DrawItem;

        /// <summary>Raises the DrawItem event.</summary>
        protected virtual void OnDrawItem (DrawItemEventArgs e) => DrawItem?.Invoke (this, e);

        /// <summary>Raised when an owner-drawn element needs to be measured. Stub in Majorsilence.Forms.</summary>
        public event MeasureItemEventHandler? MeasureItem { add { } remove { } }

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
        DropDown = 1,
        /// <summary>Text portion is read-only; list opens on arrow click.</summary>
        DropDownList = 2,
        /// <summary>Text portion is editable; list is always visible.</summary>
        Simple = 0,
    }
}
