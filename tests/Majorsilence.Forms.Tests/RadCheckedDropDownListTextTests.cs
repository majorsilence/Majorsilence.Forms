using System.Data;
using System.Linq;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // A RadCheckedDropDownList's text is its checked items, and binding selects nothing. Measured (UI for WinForms
    // 2026.2.520) with a list bound to CLOSED, OPEN and WIP: bound, SelectedIndex -1 and Text ""; OPEN checked,
    // "OPEN - Open;"; then WIP, "OPEN - Open;WIP - Work in progress;"; both unchecked, ""; WIP then CLOSED checked,
    // "WIP - Work in progress;CLOSED - Closed;" -- the order they were checked; Text set to "OPEN - Open;", OPEN alone
    // checked. Here binding selected the first item, so a status filter opened showing "CLOSED - Closed" with nothing
    // checked.
    public class RadCheckedDropDownListTextTests
    {
        private static RadCheckedDropDownList Bound ()
        {
            var table = new DataTable ();
            table.Columns.Add ("Code");
            table.Columns.Add ("Name");
            table.Rows.Add ("CLOSED", "CLOSED - Closed");
            table.Rows.Add ("OPEN", "OPEN - Open");
            table.Rows.Add ("WIP", "WIP - Work in progress");

            var list = new RadCheckedDropDownList { Text = string.Empty, DisplayMember = "Name", ValueMember = "Code" };
            list.DataSource = table;
            return list;
        }

        [Fact]
        public void Binding_selects_nothing_and_shows_nothing ()
        {
            using var list = Bound ();

            Assert.Equal (3, list.Items.Count);
            Assert.Equal (-1, list.SelectedIndex);
            Assert.Equal (string.Empty, list.Text);
        }

        [Fact]
        public void The_text_is_the_checked_items_each_followed_by_a_semicolon ()
        {
            using var list = Bound ();

            list.Items[1].Checked = true;
            Assert.Equal ("OPEN - Open;", list.Text);
            Assert.Equal (-1, list.SelectedIndex);

            list.Items[2].Checked = true;
            Assert.Equal ("OPEN - Open;WIP - Work in progress;", list.Text);

            list.Items[1].Checked = false;
            list.Items[2].Checked = false;
            Assert.Equal (string.Empty, list.Text);
        }

        [Fact]
        public void The_text_follows_the_order_the_items_were_checked_in ()
        {
            using var list = Bound ();

            list.Items[2].Checked = true;
            list.Items[0].Checked = true;

            Assert.Equal ("WIP - Work in progress;CLOSED - Closed;", list.Text);
        }

        [Fact]
        public void Setting_the_text_checks_the_items_it_names_and_unchecks_the_rest ()
        {
            using var list = Bound ();
            list.Items[0].Checked = true;
            list.Items[2].Checked = true;

            list.Text = "OPEN - Open;";

            Assert.Equal (new[] { "OPEN - Open" }, list.CheckedItems.Select (i => i.Text));
            Assert.Equal ("OPEN - Open;", list.Text);
        }
    }
}
