using System.Collections.Generic;
using System.Data;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Rebinding a grid that generates its columns keeps the columns the new schema shares with the old one.
    // Measured on WinForms (.NET 10), every column a string, Subject made 77 wide after the first bind:
    //  - bound to To, From, Subject, MessagePlainText, Attachments, ToSendDateUTC, SentDateUTC, then to ID, To,
    //    From, Subject, MessageHtml, MessagePlainText, Extra, Tag, ToSendDateUTC, IsSent, SentDateUTC, UserID,
    //    Attachments, AttemptCount, IsDiscarded, the columns are in the new schema's order but display as
    //    To, From, Subject, MessagePlainText, Attachments, ID, ToSendDateUTC, SentDateUTC, MessageHtml, Extra,
    //    Tag, IsSent, UserID, AttemptCount, IsDiscarded, and Subject is still 77 wide;
    //  - rebound then to To, From, Subject, MessagePlainText, Attachments, ID they display in that order, and
    //    to ID, Type, Description, which shares nothing, too;
    //  - header renames, hidden columns and a column moved by DisplayIndex survive the rebind;
    //  - a column the app added that names no member stays, unbound; one whose DataPropertyName names a
    //    member of the new schema becomes that member's column, also on the first bind;
    //  - DataSource = null takes the generated columns out; AutoGenerateColumns = false keeps every column.
    // The fork used to clear every column and generate the new schema in its own order.
    public class DataGridViewRebindColumnsTests
    {
        private static DataTable Table (params string[] columns)
        {
            var table = new DataTable ();
            foreach (var c in columns)
                table.Columns.Add (c);
            table.Rows.Add (columns.Select (c => (object) (c + "1")).ToArray ());
            return table;
        }

        private static string[] Display (DataGridView grid)
            => grid.Columns.Cast<DataGridViewColumn> ().OrderBy (c => c.DisplayIndex).Select (c => c.Name).ToArray ();

        private static string[] Names (DataGridView grid)
            => grid.Columns.Cast<DataGridViewColumn> ().Select (c => c.Name).ToArray ();

        private static int[] DisplayIndexes (DataGridView grid)
            => grid.Columns.Cast<DataGridViewColumn> ().Select (c => c.DisplayIndex).ToArray ();

        private static readonly string[] Overview = { "To", "From", "Subject", "MessagePlainText", "Attachments", "ToSendDateUTC", "SentDateUTC" };

        private static readonly string[] Full = {
            "ID", "To", "From", "Subject", "MessageHtml", "MessagePlainText", "Extra", "Tag", "ToSendDateUTC",
            "IsSent", "SentDateUTC", "UserID", "Attachments", "AttemptCount", "IsDiscarded" };

        [Fact]
        public void Rebinding_to_a_wider_schema_keeps_the_shared_columns_where_they_were_displayed ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table (Overview);
            grid.Columns["Subject"]!.Width = 77;

            grid.DataSource = Table (Full);

            Assert.Equal (Full, Names (grid));
            Assert.Equal (new[] {
                "To", "From", "Subject", "MessagePlainText", "Attachments", "ID", "ToSendDateUTC", "SentDateUTC",
                "MessageHtml", "Extra", "Tag", "IsSent", "UserID", "AttemptCount", "IsDiscarded" }, Display (grid));
            Assert.Equal (new[] { 5, 0, 1, 2, 8, 3, 9, 10, 6, 11, 7, 12, 4, 13, 14 }, DisplayIndexes (grid));
            Assert.Equal (77, grid.Columns["Subject"]!.Width);
            Assert.Equal (100, grid.Columns["ID"]!.Width);

            // Each cell still shows its own member, now that the columns are filled by name.
            Assert.Equal ("Subject1", grid.Rows[0].Cells["Subject"]!.Value);
            Assert.Equal ("ID1", grid.Rows[0].Cells["ID"]!.Value);
            Assert.Equal ("Attachments1", grid.Rows[0].Cells["Attachments"]!.Value);
        }

        [Fact]
        public void Rebinding_to_a_narrower_or_an_unrelated_schema_follows_the_new_schema ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table (Overview);
            grid.Columns["Subject"]!.Width = 77;
            grid.DataSource = Table (Full);

            grid.DataSource = Table ("To", "From", "Subject", "MessagePlainText", "Attachments", "ID");

            Assert.Equal (new[] { "To", "From", "Subject", "MessagePlainText", "Attachments", "ID" }, Names (grid));
            Assert.Equal (new[] { "To", "From", "Subject", "MessagePlainText", "Attachments", "ID" }, Display (grid));
            Assert.Equal (77, grid.Columns["Subject"]!.Width);

            grid.DataSource = Table ("ID", "Type", "Description");

            Assert.Equal (new[] { "ID", "Type", "Description" }, Display (grid));
        }

        // Measured: A (55 wide), B (captioned Bee), C (hidden), D (moved to display 0), rebound to D, X, C, B, A,
        // displays D, A, X, B, C with every setting kept.
        [Fact]
        public void Header_renames_hidden_columns_and_moved_columns_survive_a_rebind ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table ("A", "B", "C", "D");
            grid.Columns["B"]!.HeaderText = "Bee";
            grid.Columns["C"]!.Visible = false;
            grid.Columns["D"]!.DisplayIndex = 0;
            grid.Columns["A"]!.Width = 55;

            grid.DataSource = Table ("D", "X", "C", "B", "A");

            Assert.Equal (new[] { "D", "X", "C", "B", "A" }, Names (grid));
            Assert.Equal (new[] { "D", "A", "X", "B", "C" }, Display (grid));
            Assert.Equal ("Bee", grid.Columns["B"]!.HeaderText);
            Assert.False (grid.Columns["C"]!.Visible);
            Assert.Equal (55, grid.Columns["A"]!.Width);
        }

        // Measured: bound to A, B, C, then Mine (no DataPropertyName) moved to display 1 and MyX (DataPropertyName
        // X) added; rebound to X, C, Y, A the columns are Mine, MyX, C, Y, A displayed A, Mine, MyX, C, Y; rebound
        // then to B, Z only Mine stays, displayed first.
        [Fact]
        public void Columns_the_app_added_survive_a_rebind_and_claim_the_member_they_name ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table ("A", "B", "C");
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "Mine", HeaderText = "Mine" });
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "MyX", HeaderText = "MyX", DataPropertyName = "X" });
            grid.Columns["Mine"]!.DisplayIndex = 1;

            grid.DataSource = Table ("X", "C", "Y", "A");

            Assert.Equal (new[] { "Mine", "MyX", "C", "Y", "A" }, Names (grid));
            Assert.Equal (new[] { "A", "Mine", "MyX", "C", "Y" }, Display (grid));
            Assert.False (grid.Columns["Mine"]!.IsDataBound);
            Assert.True (grid.Columns["MyX"]!.IsDataBound);
            Assert.Equal ("X1", grid.Rows[0].Cells["MyX"]!.Value);
            Assert.Null (grid.Rows[0].Cells["Mine"]!.Value);

            grid.DataSource = Table ("B", "Z");

            Assert.Equal (new[] { "Mine", "B", "Z" }, Names (grid));
            Assert.Equal (new[] { "Mine", "B", "Z" }, Display (grid));
        }

        // Measured: MyB (DataPropertyName B, 44 wide) and Free added before the first bind, bound to A, B, C: the
        // columns are Free, A, MyB, C, displayed MyB, Free, A, C -- no second B column is generated.
        [Fact]
        public void A_column_the_app_added_before_the_first_bind_is_the_column_of_the_member_it_names ()
        {
            using var grid = new DataGridView ();
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "MyB", HeaderText = "MyB", DataPropertyName = "B", Width = 44 });
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "Free", HeaderText = "Free" });

            grid.DataSource = Table ("A", "B", "C");

            Assert.Equal (new[] { "Free", "A", "MyB", "C" }, Names (grid));
            Assert.Equal (new[] { "MyB", "Free", "A", "C" }, Display (grid));
            Assert.Equal (44, grid.Columns["MyB"]!.Width);
            Assert.Equal ("B1", grid.Rows[0].Cells["MyB"]!.Value);
        }

        // Measured: A, B with B2 (DataPropertyName B, 11 wide) added, rebound to C, B, A: the columns are C, B, A,
        // B2 displayed A, C, B2, B -- the first column naming B is B's column, the other one is added last.
        [Fact]
        public void A_second_column_naming_the_same_member_is_kept_after_the_schema ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table ("A", "B");
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "B2", HeaderText = "B2", DataPropertyName = "B", Width = 11 });

            grid.DataSource = Table ("C", "B", "A");

            Assert.Equal (new[] { "C", "B", "A", "B2" }, Names (grid));
            Assert.Equal (new[] { "A", "C", "B2", "B" }, Display (grid));
            Assert.Equal (11, grid.Columns["B2"]!.Width);
            Assert.Equal ("B1", grid.Rows[0].Cells["B2"]!.Value);
        }

        public class Message
        {
            public string? To { get; set; }
            public string? From { get; set; }
            public string? Subject { get; set; }
        }

        public class Detail
        {
            public int ID { get; set; }
            public string? Subject { get; set; }
            public string? Body { get; set; }
            public string? To { get; set; }
        }

        // Measured: a List<T> of To, From, Subject with Subject 88 wide and To moved to display 2, rebound to a
        // List<T> of ID, Subject, Body, To, displays ID, Subject, To, Body.
        [Fact]
        public void Rebinding_an_object_list_keeps_the_shared_columns_too ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = new List<Message> { new Message { Subject = "s" } };
            grid.Columns["Subject"]!.Width = 88;
            grid.Columns["To"]!.DisplayIndex = 2;

            grid.DataSource = new List<Detail> { new Detail { ID = 7, Subject = "t", To = "u" } };

            Assert.Equal (new[] { "ID", "Subject", "Body", "To" }, Names (grid));
            Assert.Equal (new[] { "ID", "Subject", "To", "Body" }, Display (grid));
            Assert.Equal (88, grid.Columns["Subject"]!.Width);
            Assert.Equal ("t", grid.Rows[0].Cells["Subject"]!.Value);
            Assert.Equal ("u", grid.Rows[0].Cells["To"]!.Value);
            Assert.Equal (7, grid.Rows[0].Cells["ID"]!.Value);
        }

        // Measured: A, B with B 22 wide, then DataSource = null leaves no columns, and binding B, C generates them
        // fresh (B 100 wide).
        [Fact]
        public void Unbinding_takes_the_generated_columns_out ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table ("A", "B");
            grid.Columns["B"]!.Width = 22;
            grid.Columns.Add (new DataGridViewTextBoxColumn { Name = "Mine", HeaderText = "Mine" });

            grid.DataSource = null;

            Assert.Equal (new[] { "Mine" }, Names (grid));

            grid.DataSource = Table ("B", "C");

            Assert.Equal (new[] { "Mine", "B", "C" }, Names (grid));
            Assert.Equal (100, grid.Columns["B"]!.Width);
        }

        // Measured: rebound to a new table of the same schema, the columns keep their widths and display order
        // (C moved first stays first).
        [Fact]
        public void Rebinding_to_the_same_schema_keeps_the_columns ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table ("A", "B", "C");
            grid.Columns["B"]!.Width = 66;
            grid.Columns["C"]!.DisplayIndex = 0;

            grid.DataSource = Table ("A", "B", "C");

            Assert.Equal (new[] { "A", "B", "C" }, Names (grid));
            Assert.Equal (new[] { "C", "A", "B" }, Display (grid));
            Assert.Equal (66, grid.Columns["B"]!.Width);
        }

        // Measured: with AutoGenerateColumns turned off after binding A, B, C, rebinding to C, D, B keeps A, B, C
        // as they were; A is no longer bound.
        [Fact]
        public void Without_AutoGenerateColumns_a_rebind_keeps_every_column ()
        {
            using var grid = new DataGridView ();
            grid.DataSource = Table ("A", "B", "C");
            grid.Columns["B"]!.Width = 33;
            grid.AutoGenerateColumns = false;

            grid.DataSource = Table ("C", "D", "B");

            Assert.Equal (new[] { "A", "B", "C" }, Names (grid));
            Assert.Equal (33, grid.Columns["B"]!.Width);
            Assert.False (grid.Columns["A"]!.IsDataBound);
            Assert.True (grid.Columns["B"]!.IsDataBound);
        }
    }
}
