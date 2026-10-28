using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A group row reads as Telerik's does. Measured on Telerik (UI for WinForms 2026.2.520): grouping by a
    // column captioned "Statement Type" gave the rows "Statement Type: Current" and "Statement Type: Notice of
    // Arrears" -- no row count -- after raising GroupSummaryEvaluate for each header with SummaryItem.Name
    // "StatementType", FormatString "{0}: {1}", Value the group's value and Parent the group row. A handler
    // that assigns FormatString rewords the header.
    public class RadGridViewGroupHeaderTextTests
    {
        private static RadGridView Grouped (System.EventHandler<GroupSummaryEvaluationEventArgs>? handler = null)
        {
            var table = new DataTable ();
            table.Columns.Add ("StatementType");
            table.Columns.Add ("BatchId", typeof (int));
            table.Rows.Add ("Notice of Arrears", 1);
            table.Rows.Add ("Notice of Arrears", 2);
            table.Rows.Add ("Current", 3);

            var grid = new RadGridView ();
            if (handler is not null)
                grid.GroupSummaryEvaluate += handler;
            grid.DataSource = table;
            grid.Columns["StatementType"]!.HeaderText = "Statement Type";

            var descriptor = new GroupDescriptor ();
            descriptor.GroupNames.Add ("StatementType", ListSortDirection.Ascending);
            grid.GroupDescriptors.Add (descriptor);
            return grid;
        }

        private static string[] Headers (RadGridView grid)
            => ((DataGridView) grid).Rows.Cast<DataGridViewRow> ().Select (r => r.Tag).OfType<GridGroupRow> ().Select (g => g.HeaderText).ToArray ();

        [Fact]
        public void A_group_row_reads_caption_colon_value_without_a_count ()
        {
            using var grid = Grouped ();

            Assert.Equal (new[] { "Statement Type: Current", "Statement Type: Notice of Arrears" }, Headers (grid));
        }

        [Fact]
        public void The_header_is_evaluated_through_GroupSummaryEvaluate ()
        {
            var seen = new List<(string name, string format, object? value)> ();
            using var grid = Grouped ((s, e) => seen.Add ((e.SummaryItem.Name, e.FormatString, e.Value)));

            Assert.Contains (("StatementType", "{0}: {1}", (object?) "Current"), seen);
            Assert.Contains (("StatementType", "{0}: {1}", (object?) "Notice of Arrears"), seen);
        }

        [Fact]
        public void A_handler_rewords_the_header_through_FormatString ()
        {
            using var grid = Grouped ((s, e) => {
                if (e.SummaryItem.Name == "StatementType")
                    e.FormatString = string.Format ("Type - {0}", e.Value);
            });

            Assert.Equal (new[] { "Type - Current", "Type - Notice of Arrears" }, Headers (grid));
        }
    }
}
