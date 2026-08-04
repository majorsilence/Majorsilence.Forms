using System;
using Xunit;

using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the scattered member gaps and the .NET 1.x DataGrid family
    /// (docs/winforms-gap-plan.md).
    ///
    /// The DataGrid family is dominated by two mechanical patterns — a <c>*Changed</c> event per
    /// property and a <c>Reset*</c> per colour — so these check that the setters actually raise and
    /// the resetters actually restore, which is the difference between the pattern being implemented
    /// and merely declared.
    /// </summary>
    public class RemainingParityTests
    {
        [Fact]
        public void DataGridTableStyle_setters_raise_their_changed_event_once ()
        {
            var style = new DataGridTableStyle ();
            var raised = 0;
            style.PreferredColumnWidthChanged += (_, _) => raised++;

            style.PreferredColumnWidth = 120;
            style.PreferredColumnWidth = 120;      // no change, no event

            Assert.Equal (120, style.PreferredColumnWidth);
            Assert.Equal (1, raised);
        }

        [Theory]
        [InlineData ("ColumnHeadersVisible")]
        [InlineData ("RowHeadersVisible")]
        [InlineData ("GridLineStyle")]
        [InlineData ("RowHeaderWidth")]
        public void DataGridTableStyle_notifies_for_each_wired_property (string property)
        {
            var style = new DataGridTableStyle ();
            var raised = 0;

            switch (property) {
                case "ColumnHeadersVisible":
                    style.ColumnHeadersVisibleChanged += (_, _) => raised++;
                    style.ColumnHeadersVisible = !style.ColumnHeadersVisible;
                    break;
                case "RowHeadersVisible":
                    style.RowHeadersVisibleChanged += (_, _) => raised++;
                    style.RowHeadersVisible = !style.RowHeadersVisible;
                    break;
                case "GridLineStyle":
                    style.GridLineStyleChanged += (_, _) => raised++;
                    style.GridLineStyle = DataGridLineStyle.None;
                    break;
                case "RowHeaderWidth":
                    style.RowHeaderWidthChanged += (_, _) => raised++;
                    style.RowHeaderWidth = 90;
                    break;
            }

            Assert.Equal (1, raised);
        }

        [Fact]
        public void DataGridTableStyle_Reset_restores_the_documented_default ()
        {
            var style = new DataGridTableStyle {
                BackColor = System.Drawing.Color.Red,
                HeaderBackColor = System.Drawing.Color.Red,
                LinkHoverColor = System.Drawing.Color.Red,
            };

            style.ResetBackColor ();
            style.ResetHeaderBackColor ();
            style.ResetLinkHoverColor ();

            Assert.Equal (SystemColors.Window, style.BackColor);
            Assert.Equal (SystemColors.Control, style.HeaderBackColor);
            Assert.Equal (SystemColors.HotTrack, style.LinkHoverColor);
        }

        [Fact]
        public void DataGridColumnStyle_Alignment_and_NullText_notify ()
        {
            // DataGridColumnStyle itself: this library's DataGridTextBoxColumn implements
            // IDataGridColumnStyle rather than deriving from it, so it does not carry these members.
            var column = new DataGridColumnStyle ();
            var alignment = 0;
            var nullText = 0;
            column.AlignmentChanged += (_, _) => alignment++;
            column.NullTextChanged += (_, _) => nullText++;

            column.Alignment = HorizontalAlignment.Right;
            column.Alignment = HorizontalAlignment.Right;
            column.NullText = "-";

            Assert.Equal (HorizontalAlignment.Right, column.Alignment);
            Assert.Equal ("-", column.NullText);
            Assert.Equal (1, alignment);
            Assert.Equal (1, nullText);
        }

        [Fact]
        public void DataGridColumnStyle_header_accessible_object_reports_the_header_text ()
        {
            var column = new DataGridColumnStyle { HeaderText = "Amount" };

            Assert.Equal ("Amount", column.HeaderAccessibleObject.Name);
            Assert.Equal (AccessibleRole.ColumnHeader, column.HeaderAccessibleObject.Role);

            column.ResetHeaderText ();
            Assert.Equal (string.Empty, column.HeaderText);
        }

        [Fact]
        public void DataGrid_reports_the_hosted_grids_visible_counts ()
        {
            using var grid = new DataGrid ();
            grid.Grid.Columns.Add (new DataGridViewTextBoxColumn ());
            grid.Grid.Columns.Add (new DataGridViewTextBoxColumn ());
            grid.Grid.Rows.Add ("a");
            grid.Grid.Rows.Add ("b");

            Assert.Equal (2, grid.VisibleColumnCount);
            Assert.Equal (2, grid.VisibleRowCount);
            Assert.Equal (0, grid.FirstVisibleColumn);

            grid.Grid.Columns[0].Visible = false;
            Assert.Equal (1, grid.VisibleColumnCount);
            Assert.Equal (1, grid.FirstVisibleColumn);
        }

        [Fact]
        public void DataGrid_SetDataBinding_sets_both_halves ()
        {
            using var grid = new DataGrid ();
            var rows = new System.Collections.Generic.List<string> { "a" };

            grid.SetDataBinding (rows, "Length");

            Assert.Same (rows, grid.DataSource);
            Assert.Equal ("Length", grid.DataMember);
        }

        [Fact]
        public void DataGrid_navigation_reports_that_it_is_not_supported ()
        {
            // There is no DataRelation model here, so a row can never be expanded -- saying so beats
            // reporting "collapsed" for something that has no children to show.
            using var grid = new DataGrid ();

            Assert.False (grid.IsExpanded (0));

            grid.Expand (0);          // must not throw
            grid.Collapse (0);
            grid.NavigateBack ();
        }

        [Fact]
        public void DataGrid_AllowNavigation_notifies_once ()
        {
            using var grid = new DataGrid ();
            var raised = 0;
            grid.AllowNavigationChanged += (_, _) => raised++;

            grid.AllowNavigation = false;
            grid.AllowNavigation = false;

            Assert.False (grid.AllowNavigation);
            Assert.Equal (1, raised);
        }

        [Fact]
        public void DataGrid_BeginEdit_rejects_a_row_that_does_not_exist ()
        {
            using var grid = new DataGrid ();

            Assert.False (grid.BeginEdit (null!, 5));
            Assert.False (grid.BeginEdit (null!, -1));
        }

        [Fact]
        public void Screen_GetBounds_answers_for_a_point_and_a_control ()
        {
            using var control = new Button ();

            // A headless run may report no screen at all; Empty is the answer a caller can act on.
            var fromPoint = Screen.GetBounds (new Point (10, 10));
            var fromControl = Screen.GetBounds (control);

            Assert.Equal (fromPoint, Screen.GetBounds (new Rectangle (10, 10, 1, 1)));
            Assert.Equal (fromControl, Screen.GetWorkingArea (control) == Rectangle.Empty
                ? Rectangle.Empty
                : fromControl);
        }

        [Fact]
        public void TreeNodeCollection_AddRange_and_IndexOfKey ()
        {
            using var tree = new TreeView ();
            TreeNodeCollection nodes = tree.Nodes;

            nodes.AddRange (new TreeNode ("one") { Name = "a" }, new TreeNode ("two") { Name = "b" });

            Assert.Equal (2, nodes.Count);
            Assert.Equal (1, nodes.IndexOfKey ("b"));
            Assert.Equal (1, nodes.IndexOfKey ("B"));      // keys are case-insensitive
            Assert.Equal (-1, nodes.IndexOfKey ("absent"));
            Assert.Equal (-1, nodes.IndexOfKey (""));
            Assert.False (nodes.IsReadOnly);
        }

        [Fact]
        public void NumericUpDownAccelerations_stay_sorted_by_duration ()
        {
            // The control walks them forwards and stops at the first threshold the hold has not
            // reached, so out-of-order entries would be skipped entirely.
            using var updown = new NumericUpDown ();

            updown.Accelerations.AddRange (
                new NumericUpDownAcceleration (5, 10m),
                new NumericUpDownAcceleration (1, 2m),
                new NumericUpDownAcceleration (3, 5m));

            Assert.Equal (1, updown.Accelerations[0].Seconds);
            Assert.Equal (3, updown.Accelerations[1].Seconds);
            Assert.Equal (5, updown.Accelerations[2].Seconds);
        }

        [Fact]
        public void NumericUpDownAcceleration_rejects_negative_values ()
        {
            Assert.Throws<ArgumentOutOfRangeException> (() => new NumericUpDownAcceleration (-1, 1m));
            Assert.Throws<ArgumentOutOfRangeException> (() => new NumericUpDownAcceleration (1, -1m));
        }

        [Fact]
        public void HelpProvider_and_ErrorProvider_only_extend_controls ()
        {
            using var help = new HelpProvider ();
            using var error = new ErrorProvider ();

            Assert.True (help.CanExtend (new Button ()));
            Assert.False (help.CanExtend ("not a control"));
            Assert.True (error.CanExtend (new Button ()));
            Assert.False (error.CanExtend (null));
        }

        [Fact]
        public void OpenFileDialog_OpenFile_refuses_when_no_file_was_chosen ()
        {
            using var dialog = new OpenFileDialog ();

            Assert.Throws<InvalidOperationException> (() => dialog.OpenFile ());
        }

        [Fact]
        public void BindingNavigator_AddStandardItems_builds_the_documented_set ()
        {
            using var navigator = new BindingNavigator ();

            navigator.AddStandardItems ();

            Assert.NotNull (navigator.MoveFirstItem);
            Assert.NotNull (navigator.MovePreviousItem);
            Assert.NotNull (navigator.PositionItem);
            Assert.NotNull (navigator.CountItem);
            Assert.NotNull (navigator.MoveNextItem);
            Assert.NotNull (navigator.MoveLastItem);
            Assert.NotNull (navigator.AddNewItem);
            Assert.NotNull (navigator.DeleteItem);

            // Named as upstream names them, so designer code assigning to these finds them.
            Assert.Equal ("bindingNavigatorMoveFirstItem", navigator.MoveFirstItem!.Name);
        }

        [Fact]
        public void BindingNavigator_AddStandardItems_is_idempotent ()
        {
            using var navigator = new BindingNavigator ();

            navigator.AddStandardItems ();
            var count = navigator.Items.Count;
            navigator.AddStandardItems ();

            Assert.Equal (count, navigator.Items.Count);
        }

        [Fact]
        public void A_grid_can_be_bound_to_a_list_of_strings ()
        {
            // string carries an indexer, and reading one with no index arguments throws
            // TargetParameterCountException -- so this crashed on the first row until indexers were
            // excluded from the generated columns.
            using var grid = new DataGridView ();

            grid.DataSource = new System.Collections.Generic.List<string> { "alpha", "beta" };

            Assert.Equal (2, grid.Rows.Count);
            Assert.DoesNotContain (grid.Columns.Cast<DataGridViewColumn> (), c => c.Name == "Chars");
        }

        [Fact]
        public void ListBindingHelper_unwraps_a_list_to_its_element_type ()
        {
            Assert.Equal (typeof (string), ListBindingHelper.GetListItemType (new System.Collections.Generic.List<string> ()));
            Assert.Equal (typeof (int), ListBindingHelper.GetListItemType (new[] { 1, 2, 3 }));
            Assert.Equal (typeof (object), ListBindingHelper.GetListItemType (null));
        }

        [Fact]
        public void ListBindingHelper_follows_a_data_member_off_the_current_item ()
        {
            // "Orders" on a list of customers means the current customer's orders -- reading the
            // member off the list itself would find nothing.
            var rows = new System.Collections.Generic.List<Row> {
                new () { Name = "first", Tags = new System.Collections.Generic.List<string> { "a", "b" } },
            };

            var inner = ListBindingHelper.GetList (rows, "Tags");

            Assert.Equal (2, ((System.Collections.IList)inner!).Count);
            Assert.Same (rows, ListBindingHelper.GetList (rows, null));
        }

        private sealed class Row
        {
            public string Name { get; set; } = string.Empty;

            public System.Collections.Generic.List<string> Tags { get; set; } = [];
        }

        [Fact]
        public void A_selected_cell_snapshot_refuses_to_be_edited ()
        {
            // Upstream documents these as copies; an edit that looked as though it changed the grid's
            // selection but changed a detached list would be worse than a refusal.
            using var grid = new DataGridView ();
            grid.Columns.Add (new DataGridViewTextBoxColumn ());
            grid.Rows.Add ("a");

            var snapshot = new DataGridViewSelectedCellCollection (grid.Rows[0].Cells);

            Assert.Equal (1, snapshot.Count);
            Assert.True (snapshot.Contains (grid.Rows[0].Cells[0]));
            Assert.Throws<NotSupportedException> (snapshot.Clear);
            Assert.Throws<NotSupportedException> (() => snapshot.Insert (0, grid.Rows[0].Cells[0]));
        }

        [Fact]
        public void CheckedListBox_checked_views_are_live ()
        {
            using var list = new CheckedListBox ();
            list.Items.Add ("a");
            list.Items.Add ("b");

            var indices = new CheckedListBox.CheckedIndexCollection (list);

            Assert.Equal (0, indices.Count);

            list.SetItemChecked (1, true);

            Assert.Equal (1, indices.Count);       // the same instance sees the change
            Assert.Equal (1, indices[0]);
            Assert.True (indices.Contains (1));
        }

        [Fact]
        public void StatusBarPanelCollection_finds_panels_by_key ()
        {
            var panels = new StatusBar.StatusBarPanelCollection ();
            panels.Add (new StatusBarPanel { Name = "status", Text = "Ready" });

            Assert.True (panels.ContainsKey ("STATUS"));
            Assert.Equal (0, panels.IndexOfKey ("status"));
            Assert.Equal (-1, panels.IndexOfKey ("absent"));

            panels.RemoveByKey ("status");
            Assert.Equal (0, panels.Count);
        }

        [Fact]
        public void The_editing_controls_split_the_keys_with_the_grid ()
        {
            // A text box keeps the keys that move the caret; a drop-down keeps the vertical ones that
            // move through its items. Getting this backwards makes arrow keys jump cells mid-word.
            using var text = new DataGridViewTextBoxEditingControl ();
            using var combo = new DataGridViewComboBoxEditingControl ();

            Assert.True (text.EditingControlWantsInputKey (Keys.Left, dataGridViewWantsInputKey: true));
            Assert.False (text.EditingControlWantsInputKey (Keys.Down, dataGridViewWantsInputKey: true));

            Assert.True (combo.EditingControlWantsInputKey (Keys.Down, dataGridViewWantsInputKey: true));
            Assert.False (combo.EditingControlWantsInputKey (Keys.Left, dataGridViewWantsInputKey: true));
        }

        [Fact]
        public void OwnerDrawPropertyBag_copies_without_sharing ()
        {
            var bag = new OwnerDrawPropertyBag { BackColor = System.Drawing.Color.Red };

            Assert.False (bag.IsEmpty ());
            Assert.True (new OwnerDrawPropertyBag ().IsEmpty ());

            var copy = OwnerDrawPropertyBag.Copy (bag);
            Assert.NotSame (bag, copy);
            Assert.Equal (System.Drawing.Color.Red, copy.BackColor);
        }
    }
}
