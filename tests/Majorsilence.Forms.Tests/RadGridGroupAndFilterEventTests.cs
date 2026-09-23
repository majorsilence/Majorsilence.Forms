using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // W6.1, the Telerik dead-event sweep -- the grid-internals slice from #176.
    //
    // Four events were declared under `#pragma warning disable CS0067` as "the compat grid does not
    // raise them". Three of them had a real trigger sitting in the file the whole time:
    //
    //   GroupExpanding       -> ToggleGroupRow, and IGridGroupOwner.SetGroupExpanded
    //   GroupSummaryEvaluate -> ComputeAggregate, which really does compute group aggregates
    //   FilterPopupRequired  -> ShowFilterPopup
    //
    // The fourth, CreateCell, genuinely cannot be raised: Telerik raises it so an application can
    // substitute the visual element used for a cell, and this grid renders cells directly rather than
    // building an element tree. It stays declared and unraised, with the reason on the declaration --
    // the same call as RadPageView.PageCollapsed in #185.
    [Collection ("Headless")]
    public class RadGridGroupAndFilterEventTests
    {
        private static RadGridView Grouped ()
        {
            var grid = new RadGridView ();
            grid.Columns.Add (new GridViewTextBoxColumn ("Name") { HeaderText = "Name", Width = 150 });
            grid.Columns.Add (new GridViewTextBoxColumn ("Dept") { HeaderText = "Dept", Width = 150 });
            grid.Columns.Add (new GridViewDecimalColumn ("Salary") { HeaderText = "Salary", Width = 120 });

            void Add (string name, string dept, decimal salary)
            {
                grid.Rows.Add ();
                var row = grid.Rows[grid.RowCount - 1];
                row.Cells["Name"].Value = name;
                row.Cells["Dept"].Value = dept;
                row.Cells["Salary"].Value = salary;
            }

            Add ("Alice", "Eng", 85000m);
            Add ("Bob", "Ops", 48000m);
            Add ("Carol", "Eng", 65000m);

            grid.EnableGrouping = true;
            grid.GroupByColumn ("Dept");

            return grid;
        }

        // ---------------- GroupExpanding

        [Fact]
        public void Toggling_a_group_header_raises_GroupExpanding ()
        {
            using var grid = Grouped ();

            var raised = 0;
            grid.GroupExpanding += (_, _) => raised++;

            Assert.True (grid.DriveFirstGroupToggle ());
            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_handler_can_veto_a_group_toggle ()
        {
            // The half that only a real event can do: the group stays as it was. Asserted through the
            // row count, which is what the veto is protecting -- collapsing a group removes its rows
            // from the display.
            using var grid = Grouped ();

            var before = grid.RowCount;
            grid.GroupExpanding += (_, e) => e.Cancel = true;

            Assert.True (grid.DriveFirstGroupToggle ());
            Assert.Equal (before, grid.RowCount);
        }

        [Fact]
        public void Without_a_veto_the_group_really_collapses ()
        {
            // PREMISE for the test above: without this, "row count unchanged" would also be what a
            // toggle that never worked looks like.
            using var grid = Grouped ();

            var before = grid.RowCount;

            Assert.True (grid.DriveFirstGroupToggle ());
            Assert.NotEqual (before, grid.RowCount);
        }

        [Fact]
        public void The_event_carries_the_group_being_toggled ()
        {
            using var grid = Grouped ();

            DataGroup? announced = null;
            grid.GroupExpanding += (_, e) => announced = e.DataGroup;

            Assert.True (grid.DriveFirstGroupToggle ());

            Assert.NotNull (announced);
            Assert.Equal (grid.Groups.First ().HeaderText, announced!.HeaderText);
        }

        [Fact]
        public void Collapsing_through_the_object_model_raises_it_too ()
        {
            // DataGroup.Collapse and a click on the header row reach the same collapse set, and the
            // file says so. An event raised on only one of them would make the veto depend on which
            // route the user's code happened to take.
            using var grid = Grouped ();

            var raised = 0;
            grid.GroupExpanding += (_, _) => raised++;

            grid.Groups.First ().Collapse ();

            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_veto_holds_on_the_object_model_route_as_well ()
        {
            using var grid = Grouped ();

            var before = grid.RowCount;
            grid.GroupExpanding += (_, e) => e.Cancel = true;

            grid.Groups.First ().Collapse ();

            Assert.Equal (before, grid.RowCount);
        }

        [Fact]
        public void Bulk_expand_and_collapse_do_not_raise_it ()
        {
            // Deliberate, and documented on the event: a per-group veto part-way through a bulk call
            // would leave the grid half-collapsed with the caller none the wiser. Pinned so that
            // "nobody raised it there" stays a decision rather than becoming an oversight.
            using var grid = Grouped ();

            var raised = 0;
            grid.GroupExpanding += (_, _) => raised++;

            grid.CollapseAllGroups ();
            grid.ExpandAllGroups ();

            Assert.Equal (0, raised);
        }

        [Fact]
        public void Toggling_still_works_with_no_handler ()
        {
            // GUARD: a raiser that returned false by default would freeze every group.
            using var grid = Grouped ();

            var before = grid.RowCount;

            Assert.True (grid.DriveFirstGroupToggle ());
            Assert.NotEqual (before, grid.RowCount);
        }

        // ---------------- GroupSummaryEvaluate

        // The summary item is added AFTER the handler in every test below, deliberately: adding one is
        // what rebuilds the view, and the aggregates are computed during that rebuild. Wiring second
        // would leave the handler attached to a grid that had already done its summing.
        private static void AddSalarySum (RadGridView grid)
            => grid.GroupSummaryItems.Add (new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum));

        [Fact]
        public void Evaluating_a_group_summary_raises_it ()
        {
            using var grid = Grouped ();

            var groups = new List<string?> ();
            grid.GroupSummaryEvaluate += (_, e) => groups.Add (e.Group?.HeaderText);

            AddSalarySum (grid);

            // One per group, each naming its own group rather than a null or a shared one.
            Assert.NotEmpty (groups);
            Assert.All (groups, g => Assert.False (string.IsNullOrEmpty (g)));
            Assert.Contains ("Eng", groups);
        }

        [Fact]
        public void A_handler_can_replace_the_summary_value ()
        {
            // The reason Telerik has this event: an aggregate the built-in set cannot express. The
            // value below is not any aggregate of the data, so it can only have come from the handler.
            using var grid = Grouped ();

            grid.GroupSummaryEvaluate += (_, e) => e.Value = "REPLACED";

            AddSalarySum (grid);

            var text = SummaryTexts (grid);

            Assert.NotEmpty (text);
            Assert.All (text, t => Assert.Equal ("REPLACED", t));
        }

        [Fact]
        public void A_handler_can_replace_the_format ()
        {
            using var grid = Grouped ();

            grid.GroupSummaryEvaluate += (_, e) => {
                e.Value = 1234;
                e.FormatString = "{0:C0}";
            };

            AddSalarySum (grid);

            Assert.All (SummaryTexts (grid), t => Assert.Contains ("1,234", t));
        }

        [Fact]
        public void A_grand_total_reports_a_null_group ()
        {
            // What GroupSummaryEvaluationEventArgs.Group's own documentation says it means, and the
            // only way a handler can tell a per-group figure from the total.
            using var grid = Grouped ();

            var sawGrandTotal = false;
            var sawNonNull = false;

            grid.GroupSummaryEvaluate += (_, e) => {
                if (e.Group is null)
                    sawGrandTotal = true;
                else
                    sawNonNull = true;
            };

            grid.SummaryRowsBottom.Add (new GridViewSummaryRowItem (
                new GridViewSummaryItem ("Salary", GridAggregateFunction.Sum)));

            Assert.True (sawGrandTotal);
            Assert.False (sawNonNull);
        }

        // The rendered text of every summary row the grid projected.
        private static List<string> SummaryTexts (RadGridView grid)
        {
            var result = new List<string> ();

            // The Telerik-typed Rows collection projects GridViewRowInfo; the structural rows the grid
            // injects (group headers, summary rows) live on the DataGridView underneath.
            foreach (Majorsilence.Forms.DataGridViewRow row in ((Majorsilence.Forms.DataGridView)grid).Rows)
                if (row.Tag is GridSummaryRow summary)
                    result.AddRange (summary.Values.Values);

            return result;
        }

        // ---------------- FilterPopupRequired

        [Fact]
        public void Asking_for_a_filter_popup_raises_it_with_the_column ()
        {
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 600, Height = 400 };
            var grid = Grouped ();
            form.Controls.Add (grid);
            form.Show ();

            try {
                Majorsilence.Forms.DataGridViewColumn? announced = null;

                // Supplying a popup suppresses the built-in one, so this also leaves no window open.
                grid.FilterPopupRequired += (_, e) => {
                    announced = e.Column;
                    e.FilterPopup = new object ();
                };

                grid.DriveFilterPopup (grid.Columns["Salary"]!.Index);

                Assert.NotNull (announced);
                Assert.Equal ("Salary", announced!.Name);
            } finally {
                // The built-in popup outlives a closed form (a gap in its own right, see the plan); close
                // it so the sibling test's premise -- no active popup -- holds whatever the run order.
                Application.ActivePopupWindow?.Close ();
                form.Close ();
            }
        }

        [Fact]
        public void Supplying_a_popup_suppresses_the_built_in_one ()
        {
            // The implementable half of Telerik's contract. Without it the application's own filter UI
            // and this grid's would both appear for one click.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 600, Height = 400 };
            var grid = Grouped ();
            form.Controls.Add (grid);
            form.Show ();

            try {
                grid.FilterPopupRequired += (_, e) => e.FilterPopup = new object ();

                grid.DriveFilterPopup (grid.Columns["Salary"]!.Index);

                Assert.Null (Application.ActivePopupWindow);
            } finally {
                form.Close ();
            }
        }

        [Fact]
        public void Leaving_the_popup_null_keeps_the_built_in_one ()
        {
            // PREMISE for the suppression test: without this, "no popup" is also what a grid that never
            // shows one looks like, and the assertion above would hold against a broken build.
            HeadlessRenderer.Use ();

            using var form = new Form { Width = 600, Height = 400 };
            var grid = Grouped ();
            form.Controls.Add (grid);
            form.Show ();

            try {
                var raised = 0;
                grid.FilterPopupRequired += (_, _) => raised++;

                grid.DriveFilterPopup (grid.Columns["Salary"]!.Index);

                Assert.Equal (1, raised);
                Assert.NotNull (Application.ActivePopupWindow);
            } finally {
                Application.ActivePopupWindow?.Close ();
                form.Close ();
            }
        }
    }
}
