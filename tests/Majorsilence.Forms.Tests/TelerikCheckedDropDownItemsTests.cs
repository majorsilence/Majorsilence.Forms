using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Telerik;
using Xunit;

namespace Majorsilence.Forms.Tests;

// RadCheckedDropDownList's items as Telerik code uses them: Items(i).Checked on a bound list,
// FindItemExact(...).Index, CheckedItems in check order and CheckedItems.Clear(). Expectations measured
// on UI.for.WinForms 2026.2.520.
[Collection ("Headless")]
public class TelerikCheckedDropDownItemsTests
{
    private sealed class Day
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
    }

    private static RadCheckedDropDownList Bound (out Form form)
    {
        HeadlessRenderer.Use ();

        var list = new RadCheckedDropDownList { Width = 200, DisplayMember = "Name", ValueMember = "Id" };
        list.DataSource = new List<Day> { new () { Name = "Mon", Id = 1 }, new () { Name = "Tue", Id = 2 }, new () { Name = "Wed", Id = 3 } };

        form = new Form { Width = 300, Height = 200 };
        form.Controls.Add (list);
        form.Show ();
        return list;
    }

    [Fact]
    public void A_bound_lists_items_are_checked_list_items_of_its_data ()
    {
        var list = Bound (out var form);

        using (form) {
            Assert.Equal (3, list.Items.Count);

            var tue = list.Items[1];
            Assert.Equal ("Tue", tue.Text);
            Assert.Equal (2, tue.Value);
            Assert.Equal ("Tue", ((Day) tue.DataBoundItem!).Name);
            Assert.Equal (1, tue.Index);

            // The same item every time it is asked for.
            Assert.Same (tue, list.Items[1]);
            Assert.Equal (new[] { "Mon", "Tue", "Wed" }, list.Items.Select (i => i.Text));
        }
    }

    [Fact]
    public void FindItemExact_finds_by_text_and_the_item_knows_its_index ()
    {
        var list = Bound (out var form);

        using (form) {
            var found = list.FindItemExact ("Tue", true);
            Assert.Equal (1, found!.Index);
            Assert.Same (list.Items[1], found);

            Assert.Null (list.FindItemExact ("tue", true));
            Assert.Same (found, list.FindItemExact ("tue", false));
            Assert.Null (list.FindItemExact ("Zzz", true));

            // The shape the calling code takes: Items(FindItemExact(text, True).Index).Checked = True.
            list.Items[list.FindItemExact ("Wed", true)!.Index].Checked = true;
            Assert.True (list.Items[2].Checked);
        }
    }

    [Fact]
    public void Checking_an_item_raises_the_lists_events_and_CheckedItems_keeps_the_check_order ()
    {
        var list = Bound (out var form);

        using (form) {
            var events = new List<string> ();
            list.ItemCheckedChanging += (_, e) => events.Add ("changing:" + e.Item.Text);
            list.ItemCheckedChanged += (_, e) => events.Add ("changed:" + e.Item.Text);

            list.Items[1].Checked = true;
            list.Items[0].Checked = true;

            Assert.Equal (new[] { "changing:Tue", "changed:Tue", "changing:Mon", "changed:Mon" }, events);
            Assert.Equal (new[] { "Tue", "Mon" }, list.CheckedItems.Select (i => i.Text));

            // Setting it to what it is raises nothing.
            events.Clear ();
            list.Items[0].Checked = true;
            Assert.Empty (events);
        }
    }

    [Fact]
    public void A_handler_can_refuse_a_check ()
    {
        var list = Bound (out var form);

        using (form) {
            var changed = 0;
            list.ItemCheckedChanging += (_, e) => e.Cancel = e.Item.Text == "Wed";
            list.ItemCheckedChanged += (_, _) => changed++;

            list.Items[2].Checked = true;
            list.Items[0].Checked = true;

            Assert.False (list.Items[2].Checked);
            Assert.Equal (1, changed);
            Assert.Equal (new[] { "Mon" }, list.CheckedItems.Select (i => i.Text));
        }
    }

    [Fact]
    public void CheckedItems_Clear_unchecks_every_checked_item ()
    {
        var list = Bound (out var form);

        using (form) {
            list.Items[1].Checked = true;
            list.Items[0].Checked = true;

            var unchecked_in = new List<string> ();
            list.ItemCheckedChanged += (_, e) => unchecked_in.Add (e.Item.Text);

            list.CheckedItems.Clear ();

            Assert.Empty (list.CheckedItems);
            Assert.All (list.Items, i => Assert.False (i.Checked));
            Assert.Equal (new[] { "Tue", "Mon" }, unchecked_in);
        }
    }

    [Fact]
    public void Items_added_by_hand_are_the_lists_items ()
    {
        HeadlessRenderer.Use ();

        using var list = new RadCheckedDropDownList ();
        var jan = new RadCheckedListDataItem ("Jan");
        list.Items.Add (jan);
        var feb = list.Items.Add ("Feb");

        Assert.Same (jan, list.Items[0]);
        Assert.Same (feb, list.Items[1]);
        Assert.Equal (1, feb.Index);
        Assert.Equal (1, list.FindItemExact ("feb")!.Index);
        Assert.Equal (-1, new RadCheckedListDataItem ("Mar").Index);

        var changed = 0;
        list.ItemCheckedChanged += (_, _) => changed++;
        feb.Checked = true;
        Assert.Equal (1, changed);
        Assert.Equal (new[] { feb }, list.CheckedItems);

        // A removed item is no longer the list's.
        list.Items.Remove (jan);
        Assert.Equal (-1, jan.Index);
        Assert.Equal (0, feb.Index);
    }

    [Fact]
    public void Rebinding_drops_the_old_datas_checks ()
    {
        var list = Bound (out var form);

        using (form) {
            list.Items[0].Checked = true;

            list.DataSource = new List<Day> { new () { Name = "Thu", Id = 4 } };

            Assert.Empty (list.CheckedItems);
            Assert.False (list.Items[0].Checked);
        }
    }
}
