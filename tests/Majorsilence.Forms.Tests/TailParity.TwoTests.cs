using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the DataGridView row/cell/column family and the second half of the flat tail
    /// (docs/winforms-gap-plan.md).
    ///
    /// The column collection's <c>Get*Column</c> walk and <c>ToolStripItemCollection</c>'s key-based
    /// lookups carry the weight: both are things migrated code calls constantly, and both are easy to
    /// implement in a way that compiles and answers wrongly.
    /// </summary>
    public class TailParityTwoTests
    {
        private static DataGridView WithColumns (params string[] headers)
        {
            var grid = new DataGridView ();

            foreach (var header in headers)
                grid.Columns.Add (new DataGridViewTextBoxColumn { HeaderText = header, Name = header });

            return grid;
        }

        [Fact]
        public void GetNextColumn_walks_by_state_and_wraps_at_neither_end ()
        {
            using var grid = WithColumns ("a", "b", "c");
            grid.Columns[1].Visible = false;

            var first = grid.Columns.GetNextColumn (null!, DataGridViewElementStates.Visible, DataGridViewElementStates.None);
            Assert.Same (grid.Columns[0], first);

            // The invisible middle column is skipped rather than returned.
            Assert.Same (grid.Columns[2],
                grid.Columns.GetNextColumn (grid.Columns[0], DataGridViewElementStates.Visible, DataGridViewElementStates.None));

            Assert.Null (grid.Columns.GetNextColumn (grid.Columns[2], DataGridViewElementStates.Visible, DataGridViewElementStates.None));
        }

        [Fact]
        public void GetPreviousColumn_and_GetLastColumn_read_from_the_other_end ()
        {
            using var grid = WithColumns ("a", "b", "c");

            Assert.Same (grid.Columns[2],
                grid.Columns.GetLastColumn (DataGridViewElementStates.Visible, DataGridViewElementStates.None));
            Assert.Same (grid.Columns[1],
                grid.Columns.GetPreviousColumn (grid.Columns[2], DataGridViewElementStates.Visible, DataGridViewElementStates.None));
            Assert.Null (
                grid.Columns.GetPreviousColumn (grid.Columns[0], DataGridViewElementStates.Visible, DataGridViewElementStates.None));
        }

        [Fact]
        public void The_exclude_filter_removes_columns_the_include_filter_matched ()
        {
            using var grid = WithColumns ("a", "b");
            grid.Columns[0].Frozen = true;

            Assert.Same (grid.Columns[1],
                grid.Columns.GetNextColumn (null!, DataGridViewElementStates.Visible, DataGridViewElementStates.Frozen));
        }

        [Fact]
        public void GetColumnCount_and_GetColumnsWidth_only_count_matching_columns ()
        {
            using var grid = WithColumns ("a", "b", "c");
            grid.Columns[1].Visible = false;
            grid.Columns[0].Width = 40;
            grid.Columns[2].Width = 60;

            Assert.Equal (2, grid.Columns.GetColumnCount (DataGridViewElementStates.Visible));
            Assert.Equal (100, grid.Columns.GetColumnsWidth (DataGridViewElementStates.Visible));

            // None as an include filter matches everything, as it does for rows.
            Assert.Equal (3, grid.Columns.GetColumnCount (DataGridViewElementStates.None));
        }

        [Fact]
        public void CreateCells_builds_one_cell_per_column ()
        {
            using var grid = WithColumns ("a", "b");
            var row = new DataGridViewRow ();

            row.CreateCells (grid);

            Assert.Equal (2, row.Cells.Count);
        }

        [Fact]
        public void SetValues_reports_when_it_had_to_drop_values ()
        {
            using var grid = WithColumns ("a", "b");
            var row = new DataGridViewRow ();
            row.CreateCells (grid);

            Assert.True (row.SetValues ("one", "two"));
            Assert.Equal ("one", row.Cells[0].Value);

            // More values than cells: the extras are dropped, and the caller is told.
            Assert.False (row.SetValues ("one", "two", "three"));
        }

        [Fact]
        public void A_rows_accessible_object_names_its_position ()
        {
            using var grid = WithColumns ("a");
            grid.Rows.Add ("first");

            Assert.Equal ("Row 1", grid.Rows[0].AccessibilityObject.Name);
            Assert.Equal (AccessibleRole.Row, grid.Rows[0].AccessibilityObject.Role);
        }

        [Fact]
        public void InheritedAutoSizeMode_resolves_NotSet_from_the_grid ()
        {
            using var grid = WithColumns ("a");
            var column = grid.Columns[0];

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            Assert.Equal (DataGridViewAutoSizeColumnMode.Fill, column.InheritedAutoSizeMode);

            // An explicit mode on the column wins over the grid's.
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            Assert.Equal (DataGridViewAutoSizeColumnMode.ColumnHeader, column.InheritedAutoSizeMode);
        }

        [Fact]
        public void CellType_reports_the_template_the_column_clones ()
        {
            var column = new DataGridViewTextBoxColumn ();

            Assert.Equal (typeof (DataGridViewTextBoxCell), column.CellType);
        }

        [Fact]
        public void The_check_box_cell_is_an_editing_cell ()
        {
            var cell = new DataGridViewCheckBoxCell ();

            // The interface is the point of the test: grid code reaches for it to drive editing
            // without knowing the cell type.
#pragma warning disable CA1859
            IDataGridViewEditingCell editing = cell;
#pragma warning restore CA1859

            editing.EditingCellFormattedValue = true;

            Assert.Equal (true, cell.Value);
            Assert.Equal (true, editing.GetEditingCellFormattedValue (DataGridViewDataErrorContexts.Display));
        }

        [Fact]
        public void ToolStripItemCollection_finds_items_by_key ()
        {
            using var strip = new ToolStrip ();
            var save = new ToolStripButton { Name = "save", Text = "Save" };
            strip.Items.Add (save);

            Assert.True (strip.Items.ContainsKey ("save"));
            Assert.True (strip.Items.ContainsKey ("SAVE"));       // keys are case-insensitive
            Assert.Equal (0, strip.Items.IndexOfKey ("save"));
            Assert.Equal (-1, strip.Items.IndexOfKey ("absent"));
            Assert.Equal (-1, strip.Items.IndexOfKey (""));
        }

        [Fact]
        public void ToolStripItemCollection_RemoveByKey_removes_the_named_item ()
        {
            using var strip = new ToolStrip ();
            strip.Items.Add (new ToolStripButton { Name = "save" });
            strip.Items.Add (new ToolStripButton { Name = "open" });

            strip.Items.RemoveByKey ("save");

            Assert.Equal (1, strip.Items.Count);
            Assert.False (strip.Items.ContainsKey ("save"));

            strip.Items.RemoveByKey ("absent");                   // a miss is not an error
            Assert.Equal (1, strip.Items.Count);
        }

        [Fact]
        public void ToolStripItemCollection_Find_can_search_drop_downs ()
        {
            using var strip = new ToolStrip ();
            var parent = new ToolStripMenuItem { Name = "file", Text = "File" };
            var child = new ToolStripMenuItem { Name = "target", Text = "Open" };
            parent.DropDownItems.Add (child);
            strip.Items.Add (parent);

            Assert.Empty (strip.Items.Find ("target", searchAllChildren: false));
            Assert.Single (strip.Items.Find ("target", searchAllChildren: true));

            Assert.Throws<ArgumentException> (() => strip.Items.Find ("", true));
        }

        [Fact]
        public void TabControl_DeselectTab_moves_the_selection_off_the_tab ()
        {
            using var tabs = new TabControl ();
            tabs.TabPages.Add (new TabPage { Name = "first" });
            tabs.TabPages.Add (new TabPage { Name = "second" });
            tabs.SelectedIndex = 0;

            tabs.DeselectTab (0);
            Assert.Equal (1, tabs.SelectedIndex);

            // Deselecting the last tab falls back to the previous one rather than leaving none.
            tabs.DeselectTab ("second");
            Assert.Equal (0, tabs.SelectedIndex);
        }

        [Fact]
        public void TabControl_DeselectTab_ignores_a_tab_that_is_not_selected ()
        {
            using var tabs = new TabControl ();
            tabs.TabPages.Add (new TabPage ());
            tabs.TabPages.Add (new TabPage ());
            tabs.SelectedIndex = 0;

            tabs.DeselectTab (1);

            Assert.Equal (0, tabs.SelectedIndex);
        }

        [Fact]
        public void TabControl_GetControl_validates_its_index ()
        {
            using var tabs = new TabControl ();
            var page = new TabPage ();
            tabs.TabPages.Add (page);

            Assert.Same (page, tabs.GetControl (0));
            Assert.Throws<ArgumentOutOfRangeException> (() => tabs.GetControl (1));
            Assert.Throws<ArgumentOutOfRangeException> (() => tabs.GetControl (-1));
        }

        [Fact]
        public void BindingContext_Contains_reports_whether_a_manager_exists ()
        {
            var context = new BindingContext ();
            var source = new System.Collections.Generic.List<string> { "a" };

            Assert.False (context.Contains (source));

            _ = context[source];                                  // asking for one creates it

            Assert.True (context.Contains (source));
            Assert.Throws<ArgumentNullException> (() => context.Contains (null!));
        }

        [Fact]
        public void TrackBar_SetRange_applies_both_bounds_together ()
        {
            using var bar = new TrackBar ();

            bar.SetRange (10, 50);

            Assert.Equal (10, bar.Minimum);
            Assert.Equal (50, bar.Maximum);

            // An inverted range is corrected rather than left to clamp Value unpredictably.
            bar.SetRange (80, 20);
            Assert.Equal (80, bar.Minimum);
            Assert.Equal (80, bar.Maximum);
        }

        [Fact]
        public void Label_preferred_size_grows_with_its_text ()
        {
            using var label = new Label { Text = "x" };
            var narrow = label.PreferredWidth;

            label.Text = "a much longer caption";

            Assert.True (label.PreferredWidth > narrow);
            Assert.True (label.PreferredHeight > 0);
        }

        [Fact]
        public void PrintPreviewControl_StartPage_notifies_and_clamps ()
        {
            using var preview = new PrintPreviewControl ();
            var raised = 0;
            preview.StartPageChanged += (_, _) => raised++;

            preview.StartPage = 3;
            preview.StartPage = 3;

            Assert.Equal (3, preview.StartPage);
            Assert.Equal (1, raised);

            preview.StartPage = -5;
            Assert.Equal (0, preview.StartPage);
        }

        [Fact]
        public void PrintDialog_Reset_returns_every_option_to_its_default ()
        {
            using var dialog = new PrintDialog {
                AllowCurrentPage = true,
                PrintToFile = true,
                ShowHelp = true,
                ShowNetwork = false,
                UseEXDialog = true,
            };

            dialog.Reset ();

            Assert.False (dialog.AllowCurrentPage);
            Assert.False (dialog.PrintToFile);
            Assert.False (dialog.ShowHelp);
            Assert.True (dialog.ShowNetwork);
            Assert.False (dialog.UseEXDialog);
        }

        [Fact]
        public void ImageList_reports_that_it_has_no_Win32_handle ()
        {
            // Images are Skia bitmaps here, not an HIMAGELIST; claiming a handle would break the
            // Win32 call a caller would make next.
            using var images = new ImageList ();

            Assert.Equal (IntPtr.Zero, images.Handle);
            Assert.False (images.HandleCreated);
        }

        [Fact]
        public void DrawItemState_carries_its_Win32_values ()
        {
            // ODS_* constants. Six of the eleven were missing, and the five present were the low ones
            // a reader is most likely to check by eye.
            Assert.Equal (2, (int)DrawItemState.Grayed);
            Assert.Equal (64, (int)DrawItemState.HotLight);
            Assert.Equal (128, (int)DrawItemState.Inactive);
            Assert.Equal (256, (int)DrawItemState.NoAccelerator);
            Assert.Equal (512, (int)DrawItemState.NoFocusRect);
            Assert.Equal (4096, (int)DrawItemState.ComboBoxEdit);
        }

        [Fact]
        public void DataGridViewCellStyleScopes_continues_the_flag_progression ()
        {
            // Five of the nine were missing; each is the next bit, so filling them by counting from
            // the wrong place would have made every scope after Row mean something else.
            Assert.Equal (8, (int)DataGridViewCellStyleScopes.DataGridView);
            Assert.Equal (16, (int)DataGridViewCellStyleScopes.ColumnHeaders);
            Assert.Equal (32, (int)DataGridViewCellStyleScopes.RowHeaders);
            Assert.Equal (64, (int)DataGridViewCellStyleScopes.Rows);
            Assert.Equal (128, (int)DataGridViewCellStyleScopes.AlternatingRows);
        }
    }
}
