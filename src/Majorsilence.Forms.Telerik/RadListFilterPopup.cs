using System.Drawing;

namespace Majorsilence.Forms.Telerik
{
    // Telerik's list filter popup, as a FilterPopupRequired handler supplies it (or a subclass of it). Measured on UI for
    // WinForms 2026.2.520 (probes/wfcellfmt), for a column of "Alpha", "Bravo", "Charlie":
    // - Six items, top to bottom, 1px inside the popup's border: "Clear Filter" (24px), "Available Filters" (24, a
    //   submenu of conditions), a separator (5), the search text box (29), the value tree (the rest) and OK/Cancel (28).
    //   The popup is 200x300 unless sized; sized 250x500 the tree is 378 tall.
    // - The tree (MenuTreeElement.TreeView) has check boxes and cascades them (TriStateMode): "Null" and "Not Null",
    //   shown only when the column has blank values, then "All" with a node per distinct value ("(Blanks)" first when
    //   there are blanks). Everything is checked to start with, Null and Not Null excepted.
    // - Typing in the text box replaces the tree with one "All Search Result" node over the values containing the text
    //   (case-insensitive); clearing the text brings the full tree back.
    // - OK filters the column to the checked values; Cancel closes; Clear Filter removes the column's filter.
    // OnPopupOpened runs once the popup is on screen, for a subclass to adjust the tree or focus the text box.
    //
    // Here Null and Not Null are not added when there are no blanks (Telerik keeps them as hidden nodes), and "Available
    // Filters" opens nothing: the conditions are the built-in popup's.

    /// <summary>Telerik-compat base of the grid's filter popups. Shown by <see cref="RadGridView"/> when a <see cref="RadGridView.FilterPopupRequired"/> handler supplies one.</summary>
    public class BaseFilterPopup : IDisposable
    {
        private PopupWindow? window;
        private Action? on_confirmed;

        /// <summary>Gets or sets the popup's size. 200x300 by default, as Telerik's.</summary>
        public Size Size { get; set; } = new Size (200, 300);

        /// <summary>Gets the popup's items, top to bottom.</summary>
        public System.Collections.Generic.List<RadItem> Items { get; } = new ();

        /// <summary>Gets or sets the filter the popup applies when confirmed; null for none.</summary>
        public FilterDescriptor? FilterDescriptor { get; set; }

        /// <summary>Raised when the popup has been shown.</summary>
        public event EventHandler? PopupOpened;

        /// <summary>Raised when the user confirms the popup (OK, or Clear Filter), with <see cref="FilterDescriptor"/> set to the result.</summary>
        public event EventHandler? FilterConfirmed;

        /// <summary>Gets whether the popup is on screen.</summary>
        public bool IsPopupOpen => window is { IsDisposed: false, Visible: true };

        /// <summary>Called once the popup is on screen. Raises <see cref="PopupOpened"/>.</summary>
        protected virtual void OnPopupOpened () => PopupOpened?.Invoke (this, EventArgs.Empty);

        /// <summary>Builds the popup's content into the panel the window shows. Called each time it is shown.</summary>
        internal virtual void BuildContent (Panel content) { }

        /// <summary>Confirms the popup with the given filter and closes it.</summary>
        protected void Confirm (FilterDescriptor? descriptor)
        {
            FilterDescriptor = descriptor;
            FilterConfirmed?.Invoke (this, EventArgs.Empty);
            on_confirmed?.Invoke ();
            ClosePopup ();
        }

        // Shows the popup at a screen point for a control in a window.
        internal void ShowFor (Control owner, Point screenLocation, Action? onConfirmed = null)
        {
            if (owner.FindWindow () is not WindowBase parent)
                return;

            on_confirmed = onConfirmed;
            ClosePopup ();
            window = new PopupWindow (parent) { Size = Size };
            var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            window.Controls.Add (content);
            BuildContent (content);
            window.Show (screenLocation);
            OnPopupOpened ();
        }

        /// <summary>Closes the popup.</summary>
        public void ClosePopup ()
        {
            if (window is { IsDisposed: false } w)
                w.Close ();
            window = null;
        }

        /// <summary>Closes the popup. The reason is not reported anywhere here.</summary>
        public void ClosePopup (RadPopupCloseReason reason) => ClosePopup ();

        /// <inheritdoc/>
        public void Dispose ()
        {
            ClosePopup ();
            GC.SuppressFinalize (this);
        }
    }

    /// <summary>Why a popup closed. Mirrors Telerik's <c>RadPopupCloseReason</c>.</summary>
    public enum RadPopupCloseReason
    {
        /// <summary>The popup's owner closed it.</summary>
        CloseCalled,
        /// <summary>A key closed it.</summary>
        Keyboard,
        /// <summary>A click elsewhere closed it.</summary>
        Mouse,
        /// <summary>The application lost focus.</summary>
        AppFocusChange,
        /// <summary>The parent window moved.</summary>
        ParentCommand,
    }

    /// <summary>
    /// Telerik-compat list filter popup: a search box over a check-box tree of the column's distinct values, with OK and
    /// Cancel. Supply one (or a subclass) from <see cref="RadGridView.FilterPopupRequired"/> to have the grid show it.
    /// </summary>
    public class RadListFilterPopup : BaseFilterPopup
    {
        internal const string NullText = "Null", NotNullText = "Not Null", AllText = "All", SearchResultText = "All Search Result", BlanksText = "(Blanks)";

        private readonly System.Collections.Generic.List<string> values;
        private readonly bool has_blanks;
        private readonly System.Collections.Generic.Dictionary<string, bool> value_checked = new (StringComparer.Ordinal);
        private bool null_checked, not_null_checked;
        private bool cascading;
        private bool searching;

        /// <summary>Initializes the popup for a column of a grid: its tree holds the column's distinct values, checked as the column's filter leaves them.</summary>
        public RadListFilterPopup (GridViewDataColumn column)
        {
            Column = column;

            var grid = column.DataGridView as RadGridView;
            var index = grid is null ? -1 : column.Index;
            var distinct = grid is not null && index >= 0 ? grid.DistinctValues (index) : new System.Collections.Generic.List<string> ();
            has_blanks = distinct.Exists (string.IsNullOrEmpty);
            values = distinct.FindAll (v => !string.IsNullOrEmpty (v));
            values.Sort (StringComparer.CurrentCultureIgnoreCase);

            var current = grid?.FilterDescriptors.Find (f => string.Equals (f.PropertyName, column.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var value in AllValues ())
                value_checked[value] = current?.SelectedValues is not { } selected || selected.Contains (value);

            MenuTreeElement.TreeView.CheckBoxes = true;
            MenuTreeElement.TreeView.NodeCheckedChanged += (_, e) => Cascade (e.Node);
            TextBoxMenuItem.TextBox.TextChanged += (_, _) => Search (TextBoxMenuItem.TextBox.Text);
            ButtonsMenuItem.ButtonOK.Click += (_, _) => Confirm (BuildDescriptor ());
            ButtonsMenuItem.ButtonCancel.Click += (_, _) => ClosePopup ();
            ClearFilterMenuItem.Click += (_, _) => Confirm (null);

            Items.Add (ClearFilterMenuItem);
            Items.Add (AvailableFiltersMenuItem);
            Items.Add (new RadItem ());   // the separator
            Items.Add (TextBoxMenuItem);
            Items.Add (MenuTreeItem);
            Items.Add (ButtonsMenuItem);

            FillTree (null);
        }

        /// <summary>Gets the column the popup filters.</summary>
        public GridViewDataColumn Column { get; }

        /// <summary>Gets the "Clear Filter" item.</summary>
        public RadItem ClearFilterMenuItem { get; } = new RadItem { Text = "Clear Filter" };

        /// <summary>Gets the "Available Filters" item. It opens nothing here.</summary>
        public RadItem AvailableFiltersMenuItem { get; } = new RadItem { Text = "Available Filters" };

        /// <summary>Gets the search text box item.</summary>
        public FilterMenuTextBoxItem TextBoxMenuItem { get; } = new FilterMenuTextBoxItem ();

        /// <summary>Gets the value tree item.</summary>
        public FilterMenuTreeItem MenuTreeItem { get; } = new FilterMenuTreeItem ();

        /// <summary>Gets the value tree's element, whose <see cref="FilterMenuTreeElement.TreeView"/> holds the values.</summary>
        public FilterMenuTreeElement MenuTreeElement => MenuTreeItem.TreeElement;

        /// <summary>Gets the OK/Cancel item.</summary>
        public FilterMenuButtonsItem ButtonsMenuItem { get; } = new FilterMenuButtonsItem ();

        private System.Collections.Generic.IEnumerable<string> AllValues ()
        {
            if (has_blanks)
                yield return string.Empty;
            foreach (var v in values)
                yield return v;
        }

        // The tree as Telerik builds it: Null and Not Null (when there are blanks) and All over every value, or, while the
        // text box has text, one "All Search Result" node over the values containing it.
        private void FillTree (string? search)
        {
            var tree = MenuTreeElement.TreeView;
            cascading = true;
            try {
                tree.Nodes.Clear ();

                if (search is null) {
                    if (has_blanks) {
                        tree.Nodes.Add (new RadTreeNode (NullText) { Value = NullText, CheckState = null_checked ? ToggleState.On : ToggleState.Off });
                        tree.Nodes.Add (new RadTreeNode (NotNullText) { Value = NotNullText, CheckState = not_null_checked ? ToggleState.On : ToggleState.Off });
                    }
                }

                var all = new RadTreeNode (search is null ? AllText : SearchResultText) { Value = search is null ? AllText : SearchResultText };
                tree.Nodes.Add (all);

                foreach (var value in AllValues ()) {
                    if (search is not null && (value.Length == 0 || value.IndexOf (search, StringComparison.CurrentCultureIgnoreCase) < 0))
                        continue;
                    all.Nodes.Add (new RadTreeNode (value.Length == 0 ? BlanksText : value) {
                        Tag = value, CheckState = value_checked[value] ? ToggleState.On : ToggleState.Off
                    });
                }

                all.CheckState = StateOf (all);
                all.Expand ();
            } finally {
                cascading = false;
            }
        }

        private void Search (string text)
        {
            searching = text.Length > 0;
            FillTree (searching ? text : null);
        }

        // Tri-state: a parent's state goes to its children, and a child's change sets its parent's.
        private void Cascade (RadTreeNode node)
        {
            if (cascading)
                return;

            cascading = true;
            try {
                if (node.Nodes.Count > 0 && node.CheckState != ToggleState.Indeterminate)
                    foreach (var child in node.Nodes)
                        child.CheckState = node.CheckState;

                if (node.Parent is { } parent)
                    parent.CheckState = StateOf (parent);

                Remember (MenuTreeElement.TreeView.Nodes);
            } finally {
                cascading = false;
            }
        }

        private static ToggleState StateOf (RadTreeNode parent)
        {
            var on = 0;
            var count = 0;
            foreach (var child in parent.Nodes) {
                count++;
                if (child.CheckState == ToggleState.On)
                    on++;
                else if (child.CheckState == ToggleState.Indeterminate)
                    return ToggleState.Indeterminate;
            }
            return count == 0 ? parent.CheckState : on == count ? ToggleState.On : on == 0 ? ToggleState.Off : ToggleState.Indeterminate;
        }

        // Keeps the checks of the nodes on show, so a search and its clearing do not lose them.
        private void Remember (RadTreeNodeCollection nodes)
        {
            foreach (var node in nodes) {
                if (node.Tag is string value)
                    value_checked[value] = node.CheckState == ToggleState.On;
                else if (Equals (node.Value, NullText))
                    null_checked = node.CheckState == ToggleState.On;
                else if (Equals (node.Value, NotNullText))
                    not_null_checked = node.CheckState == ToggleState.On;
                Remember (node.Nodes);
            }
        }

        // The checked values as a filter: none when everything (and not just the values) is let through.
        internal FilterDescriptor? BuildDescriptor ()
        {
            Remember (MenuTreeElement.TreeView.Nodes);

            var selected = new System.Collections.Generic.HashSet<string> (StringComparer.CurrentCultureIgnoreCase);
            foreach (var (value, on) in value_checked)
                if (on || (value.Length == 0 && null_checked) || (value.Length > 0 && not_null_checked))
                    selected.Add (value);

            if (selected.Count == value_checked.Count)
                return null;

            return new FilterDescriptor { PropertyName = Column.Name, SelectedValues = selected };
        }

        internal override void BuildContent (Panel content)
        {
            // Bottom up, as docking stacks them: the buttons, then the items above the tree, then the tree in the rest.
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            ButtonsMenuItem.ButtonCancel.SetBounds (Size.Width - 2 - 4 - 64, 3, 64, 22);
            ButtonsMenuItem.ButtonOK.SetBounds (ButtonsMenuItem.ButtonCancel.Left - 4 - 64, 3, 64, 22);
            buttons.Controls.Add (ButtonsMenuItem.ButtonOK);
            buttons.Controls.Add (ButtonsMenuItem.ButtonCancel);

            var tree = MenuTreeElement.TreeView;
            tree.Dock = DockStyle.Fill;
            var tree_host = new Panel { Dock = DockStyle.Fill, Padding = new Padding (26, 4, 4, 0) };
            tree_host.Controls.Add (tree);

            var box = TextBoxMenuItem.TextBox;
            box.Dock = DockStyle.Fill;
            var box_host = new Panel { Dock = DockStyle.Top, Height = 29, Padding = new Padding (26, 4, 4, 4) };
            box_host.Controls.Add (box);

            var separator = new Panel { Dock = DockStyle.Top, Height = 5 };
            var available = new Label { Dock = DockStyle.Top, Height = 24, Text = AvailableFiltersMenuItem.Text, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding (24, 0, 0, 0) };
            var clear = new Label { Dock = DockStyle.Top, Height = 24, Text = ClearFilterMenuItem.Text, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding (24, 0, 0, 0) };
            clear.Click += (_, _) => ClearFilterMenuItem.PerformClick ();

            content.Controls.Add (tree_host);
            content.Controls.Add (box_host);
            content.Controls.Add (separator);
            content.Controls.Add (available);
            content.Controls.Add (clear);
            content.Controls.Add (buttons);
        }
    }

    /// <summary>Telerik-compat search text box item of a list filter popup.</summary>
    public class FilterMenuTextBoxItem : RadItem
    {
        /// <summary>Gets the text box.</summary>
        public RadTextBox TextBox { get; } = new RadTextBox ();
    }

    /// <summary>Telerik-compat value tree item of a list filter popup.</summary>
    public class FilterMenuTreeItem : RadItem
    {
        /// <summary>Gets the tree's element.</summary>
        public FilterMenuTreeElement TreeElement { get; } = new FilterMenuTreeElement ();
    }

    /// <summary>Telerik-compat element of a list filter popup's value tree.</summary>
    public class FilterMenuTreeElement : LightVisualElement
    {
        /// <summary>Gets the tree of values.</summary>
        public RadTreeView TreeView { get; } = new RadTreeView ();
    }

    /// <summary>Telerik-compat OK/Cancel item of a filter popup.</summary>
    public class FilterMenuButtonsItem : RadItem
    {
        /// <summary>Gets the OK button.</summary>
        public Button ButtonOK { get; } = new Button { Text = "OK" };

        /// <summary>Gets the Cancel button.</summary>
        public Button ButtonCancel { get; } = new Button { Text = "Cancel" };
    }

    public partial class RadGridView
    {
        // Shows a popup a FilterPopupRequired handler supplied, under the column's funnel, and filters the column to what
        // it confirms.
        private void ShowSuppliedFilterPopup (BaseFilterPopup popup, DataGridViewColumn column, int columnIndex)
        {
            var anchor = FilterGlyphRects.TryGetValue (columnIndex, out var glyph)
                ? new Point (glyph.Left, column.HeaderBounds.Bottom)
                : new Point (column.HeaderBounds.Left, column.HeaderBounds.Bottom);

            void Confirmed ()
            {
                var name = !string.IsNullOrEmpty (column.Name) ? column.Name : column.HeaderText;
                var current = FilterDescriptors.Find (f => NameMatches (f.PropertyName, column));
                if (current is not null)
                    FilterDescriptors.Remove (current);
                if (popup.FilterDescriptor is { } descriptor) {
                    descriptor.PropertyName = name;
                    FilterDescriptors.Add (descriptor);
                } else {
                    RebuildView ();
                }
                Invalidate ();
            }

            popup.ShowFor (this, PointToScreen (anchor), Confirmed);
        }
    }
}
