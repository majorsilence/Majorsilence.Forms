using System.Data;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Telerik's list filter popup (UI for WinForms 2026.2.520, probes/wfcellfmt), supplied from FilterPopupRequired: a
    // search box over a tri-state check-box tree -- "Null" and "Not Null" when the column has blanks, then "All" over
    // the distinct values ("(Blanks)" first) -- with OK and Cancel. Typing replaces the tree with one "All Search Result"
    // node over the matching values. A subclass's OnPopupOpened runs once it is shown.
    [Collection ("Headless")]
    public class RadListFilterPopupTests
    {
        // A subclass as an application writes one: sorts the tree, unchecks its first node, focuses the search box.
        private sealed class SearchListPopup : RadListFilterPopup
        {
            public int Opened;
            public bool Focused;

            public SearchListPopup (GridViewDataColumn column) : base (column) => Size = new Size (250, 500);

            protected override void OnPopupOpened ()
            {
                base.OnPopupOpened ();
                Opened++;
                MenuTreeElement.TreeView.Nodes.First (n => string.Equals (n.Text, "all", System.StringComparison.OrdinalIgnoreCase)).TreeView!.SortOrder = SortOrder.Ascending;
                MenuTreeElement.TreeView.Nodes.First ().CheckState = ToggleState.Off;
                Focused = TextBoxMenuItem.TextBox.TextBoxElement.TextBoxItem.TextBoxControl!.Focus ();
            }
        }

        private static (Form Form, RadGridView Grid) Grid (bool blanks)
        {
            HeadlessRenderer.Use ();
            var form = new Form { Width = 600, Height = 400 };
            var grid = new RadGridView { Dock = DockStyle.Fill, ShowGroupPanel = false, AllowAddNewRow = false, EnableFiltering = true };
            form.Controls.Add (grid);

            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Columns.Add ("Name");
            table.Rows.Add ("A100", "Charlie");
            table.Rows.Add ("B200", "Alpha");
            table.Rows.Add ("C300", "Bravo");
            if (blanks)
                table.Rows.Add ("D400", System.DBNull.Value);
            grid.DataSource = table;
            form.Show ();
            return (form, grid);
        }

        private static string[] Texts (RadTreeNodeCollection nodes) => nodes.Select (n => n.Text).ToArray ();

        [Fact]
        public void The_tree_holds_All_over_the_sorted_values_all_checked ()
        {
            var (form, grid) = Grid (blanks: false);
            using (form) {
                var tree = new RadListFilterPopup ((GridViewDataColumn) grid.Columns["Name"]!).MenuTreeElement.TreeView;

                Assert.True (tree.CheckBoxes);
                Assert.Equal (new[] { "All" }, Texts (tree.Nodes));
                Assert.Equal (new[] { "Alpha", "Bravo", "Charlie" }, Texts (tree.Nodes[0]!.Nodes));
                Assert.Equal (ToggleState.On, tree.Nodes[0]!.CheckState);
                Assert.All (tree.Nodes[0]!.Nodes, n => Assert.Equal (ToggleState.On, n.CheckState));
            }
        }

        [Fact]
        public void A_column_with_blanks_adds_Null_Not_Null_and_Blanks ()
        {
            var (form, grid) = Grid (blanks: true);
            using (form) {
                var tree = new RadListFilterPopup ((GridViewDataColumn) grid.Columns["Name"]!).MenuTreeElement.TreeView;

                Assert.Equal (new[] { "Null", "Not Null", "All" }, Texts (tree.Nodes));
                Assert.Equal (ToggleState.Off, tree.Nodes[0]!.CheckState);
                Assert.Equal (new[] { "(Blanks)", "Alpha", "Bravo", "Charlie" }, Texts (tree.Nodes[2]!.Nodes));
            }
        }

        [Fact]
        public void Checks_cascade_both_ways ()
        {
            var (form, grid) = Grid (blanks: false);
            using (form) {
                var tree = new RadListFilterPopup ((GridViewDataColumn) grid.Columns["Name"]!).MenuTreeElement.TreeView;
                var all = tree.Nodes[0]!;

                all.CheckState = ToggleState.Off;
                Assert.All (all.Nodes, n => Assert.Equal (ToggleState.Off, n.CheckState));

                all.Nodes[1]!.CheckState = ToggleState.On;
                Assert.Equal (ToggleState.Indeterminate, all.CheckState);

                all.Nodes[0]!.CheckState = ToggleState.On;
                all.Nodes[2]!.CheckState = ToggleState.On;
                Assert.Equal (ToggleState.On, all.CheckState);
            }
        }

        [Fact]
        public void Typing_shows_the_matching_values_under_All_Search_Result_and_clearing_brings_the_tree_back ()
        {
            var (form, grid) = Grid (blanks: false);
            using (form) {
                var popup = new RadListFilterPopup ((GridViewDataColumn) grid.Columns["Name"]!);
                var tree = popup.MenuTreeElement.TreeView;
                tree.Nodes[0]!.Nodes[0]!.CheckState = ToggleState.Off;   // Alpha

                popup.TextBoxMenuItem.TextBox.Text = "ra";
                Assert.Equal (new[] { "All Search Result" }, Texts (tree.Nodes));
                Assert.Equal (new[] { "Bravo" }, Texts (tree.Nodes[0]!.Nodes));

                popup.TextBoxMenuItem.TextBox.Text = "";
                Assert.Equal (new[] { "All" }, Texts (tree.Nodes));
                Assert.Equal (ToggleState.Off, tree.Nodes[0]!.Nodes[0]!.CheckState);
                Assert.Equal (ToggleState.Indeterminate, tree.Nodes[0]!.CheckState);
            }
        }

        [Fact]
        public void A_supplied_popup_is_shown_its_OnPopupOpened_runs_and_OK_filters_the_column ()
        {
            var (form, grid) = Grid (blanks: false);
            using (form) {
                SearchListPopup? popup = null;
                grid.FilterPopupRequired += (_, e) => e.FilterPopup = popup = new SearchListPopup (e.Column!);

                try {
                    grid.DriveFilterPopup (grid.Columns["Name"]!.Index);

                    Assert.NotNull (popup);
                    Assert.True (popup!.IsPopupOpen);
                    Assert.Equal (1, popup.Opened);
                    Assert.True (popup.Focused);

                    // The subclass's sort and uncheck: All first, everything off.
                    var tree = popup.MenuTreeElement.TreeView;
                    Assert.Equal ("All", tree.Nodes[0]!.Text);
                    Assert.All (tree.Nodes[0]!.Nodes, n => Assert.Equal (ToggleState.Off, n.CheckState));

                    tree.Nodes[0]!.Nodes.First (n => n.Text == "Bravo").CheckState = ToggleState.On;
                    popup.ButtonsMenuItem.ButtonOK.PerformClick ();

                    Assert.False (popup.IsPopupOpen);
                    var filter = Assert.Single (grid.FilterDescriptors);
                    Assert.Equal (new[] { "Bravo" }, filter.SelectedValues!.ToArray ());
                    Assert.Equal (1, grid.ChildRows.Count);
                    Assert.Equal ("Bravo", grid.ChildRows[0].Cells["Name"].Value);
                } finally {
                    popup?.ClosePopup ();
                }
            }
        }

        [Fact]
        public void Clear_Filter_removes_the_column_s_filter_and_Cancel_leaves_it ()
        {
            var (form, grid) = Grid (blanks: false);
            using (form) {
                grid.FilterDescriptors.Add (new FilterDescriptor { PropertyName = "Name", SelectedValues = new System.Collections.Generic.HashSet<string> { "Alpha" } });
                Assert.Equal (1, grid.ChildRows.Count);

                RadListFilterPopup? popup = null;
                grid.FilterPopupRequired += (_, e) => e.FilterPopup = popup = new RadListFilterPopup (e.Column!);

                try {
                    grid.DriveFilterPopup (grid.Columns["Name"]!.Index);

                    // The column's filter shows in the checks.
                    Assert.Equal (ToggleState.Indeterminate, popup!.MenuTreeElement.TreeView.Nodes[0]!.CheckState);

                    popup.ButtonsMenuItem.ButtonCancel.PerformClick ();
                    Assert.Single (grid.FilterDescriptors);

                    grid.DriveFilterPopup (grid.Columns["Name"]!.Index);
                    popup.ClearFilterMenuItem.PerformClick ();
                    Assert.Empty (grid.FilterDescriptors);
                    Assert.Equal (3, grid.ChildRows.Count);
                } finally {
                    popup?.ClosePopup ();
                }
            }
        }
    }
}
