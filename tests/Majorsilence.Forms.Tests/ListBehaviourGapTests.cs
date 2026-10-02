using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // #347: the open list-control findings in docs/behaviour-gap/lists.md -- display text and
    // formatting, the data-binding events, selection order and event order, keyboard and wheel
    // input, the combo's drop-down sizing, and the TreeView/ListView/CheckedListBox odds and ends.
    // Each test names its finding; each was run against its fix neutralized and went red.
    [Collection ("Headless")]
    public class ListBehaviourGapTests
    {
        private sealed class Customer
        {
            public Customer (int id, string name, decimal price = 0m)
            {
                Id = id;
                Name = name;
                Price = price;
            }

            public int Id { get; }
            public string Name { get; }
            public decimal Price { get; }

            // Deliberately not Name: a search that compares ToString cannot pass by accident.
            public override string ToString () => $"Customer#{Id}";
        }

        private static List<Customer> Customers () =>
        [
            new Customer (1, "Alice", 1.25m),
            new Customer (2, "Bob", 2.5m),
            new Customer (3, "Carol", 3.75m),
        ];

        private static ListBox BoundList ()
            => new ListBox { DataSource = Customers (), DisplayMember = "Name", ValueMember = "Id" };

        // A list on a shown form, laid out, so its scrollbar and visible-row count are real.
        private static ListBox ShownList (out Form form, int items)
        {
            HeadlessRenderer.Use ();
            form = new Form { Width = 400, Height = 300 };
            var box = new ListBox { Width = 200, Height = 100, IntegralHeight = false, ItemHeight = 20 };

            for (var i = 0; i < items; i++)
                box.Items.Add ($"Item {i}");

            form.Controls.Add (box);
            form.Show ();
            PaintSurface.RenderOnForm (box, 1f).Dispose ();
            return box;
        }

        private static MouseEventArgs Click (ListBox box, int index, Keys modifiers = Keys.None)
        {
            var r = box.GetItemRectangle (index);
            return new MouseEventArgs (MouseButtons.Left, 1, r.Left + 5, r.Top + r.Height / 2, Point.Empty, keyData: modifiers);
        }

        private static MouseEventArgs Wheel (int delta) => new (MouseButtons.None, 0, 5, 5, new Point (0, delta));

        // ── LST-14: FindString / FindStringExact compare the display text ─────────────────────────────

        [Fact]
        public void FindStringExact_matches_the_DisplayMember_text ()
        {
            using var list = BoundList ();

            Assert.Equal (1, list.FindStringExact ("Bob"));
            Assert.Equal (1, list.FindStringExact ("bob"));
            Assert.Equal (-1, list.FindStringExact ("Customer#2"));
        }

        [Fact]
        public void FindString_prefix_matches_the_DisplayMember_text ()
        {
            using var list = BoundList ();

            Assert.Equal (2, list.FindString ("Car"));
            Assert.Equal (-1, list.FindString ("Customer"));
        }

        // ── LST-27: FormatString / FormatInfo / Format ───────────────────────────────────────────────

        [Fact]
        public void FormatString_formats_the_display_member_value ()
        {
            using var list = new ListBox {
                DataSource = Customers (),
                DisplayMember = "Price",
                FormattingEnabled = true,
                FormatString = "0.0",
                FormatInfo = CultureInfo.InvariantCulture,
            };

            Assert.Equal ("1.3", list.GetItemText (list.Items[0]));
        }

        [Fact]
        public void FormatInfo_alone_sets_the_culture ()
        {
            using var list = new ListBox { FormattingEnabled = true, FormatInfo = new CultureInfo ("de-DE") };
            list.Items.Add (1.5m);

            Assert.Equal ("1,5", list.GetItemText (list.Items[0]));
        }

        [Fact]
        public void Format_is_offered_the_member_value_not_its_text ()
        {
            using var list = new ListBox { DataSource = Customers (), DisplayMember = "Price", FormattingEnabled = true };
            object? offered = null;
            list.Format += (_, e) => {
                offered = e.Value;
                e.Value = $"{((Customer)e.ListItem).Name}: {e.Value}";
            };

            var text = list.GetItemText (list.Items[1]);

            Assert.IsType<decimal> (offered);
            Assert.Equal ("Bob: 2.5", text);
        }

        [Fact]
        public void A_combo_drop_down_shows_the_combos_formatted_text ()
        {
            // The drop-down is a ListBox of its own: unless it asks the combo, the combo's Format
            // handler and FormatString never reach the rows the user picks from.
            using var combo = new ComboBox { FormattingEnabled = true, FormatString = "0.00", FormatInfo = CultureInfo.InvariantCulture };
            combo.Items.Add (2m);

            Assert.Equal ("2.00", combo.PopupListBox.GetItemText (combo.Items[0]));
        }

        // ── LST-28: DataSource / DisplayMember / ValueMember events; a null source empties ───────────

        [Fact]
        public void ListBox_DataSource_null_clears_the_items_and_announces_it ()
        {
            using var list = BoundList ();
            list.SelectedIndex = 1;
            var changed = 0;
            list.DataSourceChanged += (_, _) => changed++;

            list.DataSource = null;

            Assert.Equal (1, changed);
            Assert.Empty (list.Items);
            Assert.Equal (-1, list.SelectedIndex);
            Assert.Equal (string.Empty, list.DisplayMember);
        }

        [Fact]
        public void ComboBox_DataSource_null_clears_the_items_and_announces_it ()
        {
            using var combo = new ComboBox { DataSource = Customers (), DisplayMember = "Name" };
            var changed = 0;
            combo.DataSourceChanged += (_, _) => changed++;

            combo.DataSource = null;

            Assert.Equal (1, changed);
            Assert.Empty (combo.Items);
            Assert.Equal (-1, combo.SelectedIndex);
        }

        [Fact]
        public void Member_changes_raise_their_events ()
        {
            using var list = new ListBox ();
            using var combo = new ComboBox ();
            var raised = new List<string> ();

            list.DisplayMemberChanged += (_, _) => raised.Add ("list display");
            list.ValueMemberChanged += (_, _) => raised.Add ("list value");
            combo.DisplayMemberChanged += (_, _) => raised.Add ("combo display");
            combo.ValueMemberChanged += (_, _) => raised.Add ("combo value");

            list.DisplayMember = "Name";
            list.DisplayMember = "Name";    // no change, no event
            list.ValueMember = "Id";
            combo.DisplayMember = "Name";
            combo.ValueMember = "Id";

            Assert.Equal (["list display", "list value", "combo display", "combo value"], raised);
        }

        // ── LST-29: SelectedValue on a miss ──────────────────────────────────────────────────────────

        [Fact]
        public void SelectedValue_that_no_row_holds_clears_the_selection ()
        {
            using var list = BoundList ();
            using var combo = new ComboBox { DataSource = Customers (), DisplayMember = "Name", ValueMember = "Id" };

            list.SelectedValue = 2;
            combo.SelectedValue = 2;
            Assert.Equal (1, list.SelectedIndex);
            Assert.Equal (1, combo.SelectedIndex);

            list.SelectedValue = 99;
            combo.SelectedValue = 99;
            Assert.Equal (-1, list.SelectedIndex);
            Assert.Equal (-1, combo.SelectedIndex);
        }

        // ── LST-30: multi-select order ───────────────────────────────────────────────────────────────

        [Fact]
        public void A_multi_selection_reports_in_item_order ()
        {
            using var list = new ListBox { SelectionMode = SelectionMode.MultiSimple };
            list.Items.AddRange ("a", "b", "c", "d");

            list.SetSelected (3, true);
            list.SetSelected (0, true);
            list.SetSelected (2, true);

            Assert.Equal (0, list.SelectedIndex);
            Assert.Equal ([0, 2, 3], list.SelectedIndices.ToArray ());
            Assert.Equal (["a", "c", "d"], list.SelectedItems.Cast<object> ().ToArray ());
        }

        // ── LST-31: event order ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void ListBox_raises_SelectedValueChanged_before_SelectedIndexChanged ()
        {
            using var list = new ListBox ();
            list.Items.AddRange ("a", "b");
            var order = new List<string> ();
            list.SelectedValueChanged += (_, _) => order.Add ("Value");
            list.SelectedIndexChanged += (_, _) => order.Add ("Index");

            list.SelectedIndex = 1;

            Assert.Equal (["Value", "Index"], order);
        }

        [Fact]
        public void ComboBox_raises_item_then_value_then_index ()
        {
            using var combo = new ComboBox ();
            combo.Items.AddRange ("a", "b");
            var order = new List<string> ();
            combo.SelectedItemChanged += (_, _) => order.Add ("Item");
            combo.SelectedValueChanged += (_, _) => order.Add ("Value");
            combo.SelectedIndexChanged += (_, _) => order.Add ("Index");

            combo.SelectedIndex = 1;

            Assert.Equal (["Item", "Value", "Index"], order);
        }

        // ── LST-32: SelectionChangeCommitted from the keyboard with the list closed ──────────────────

        [Theory]
        [InlineData (ComboBoxStyle.DropDown)]
        [InlineData (ComboBoxStyle.DropDownList)]
        public void A_keyboard_change_on_a_closed_combo_is_committed_before_it_is_announced (ComboBoxStyle style)
        {
            using var combo = new ComboBox { DropDownStyle = style };
            combo.Items.AddRange ("a", "b", "c");
            combo.SelectedIndex = 0;
            var order = new List<string> ();
            combo.SelectionChangeCommitted += (_, _) => order.Add ("Committed");
            combo.SelectedIndexChanged += (_, _) => order.Add ("Index");

            combo.RaiseKeyDown (new KeyEventArgs (Keys.Down));

            Assert.Equal (1, combo.SelectedIndex);
            Assert.Equal (["Committed", "Index"], order);
        }

        [Fact]
        public void A_programmatic_change_is_not_committed ()
        {
            // Guard: the negative half of the distinction SelectionChangeCommitted exists for. It held
            // before the fix too.
            using var combo = new ComboBox ();
            combo.Items.AddRange ("a", "b");
            var commits = 0;
            combo.SelectionChangeCommitted += (_, _) => commits++;

            combo.SelectedIndex = 1;

            Assert.Equal (0, commits);
        }

        // ── LST-33: the drop-down's height, and ItemHeight ───────────────────────────────────────────

        [Fact]
        public void An_explicit_DropDownHeight_sizes_the_drop_down ()
        {
            using var combo = new ComboBox ();

            for (var i = 0; i < 30; i++)
                combo.Items.Add ($"Item {i}");

            var by_rows = combo.ComputePopupSize ().Height;
            combo.DropDownHeight = by_rows + 57;

            Assert.Equal (by_rows + 57, combo.ComputePopupSize ().Height);
            Assert.False (combo.IntegralHeight);
        }

        [Fact]
        public void ItemHeight_is_the_drop_downs_row_height ()
        {
            using var combo = new ComboBox ();
            combo.Items.Add ("x");

            Assert.Equal (combo.PopupListBox.ItemHeight, combo.ItemHeight);
            Assert.Equal (combo.PopupListBox.ItemHeight, combo.GetItemHeight (0));

            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = combo.PopupListBox.ItemHeight + 11;

            Assert.Equal (combo.ItemHeight, combo.PopupListBox.ItemHeight);
        }

        // ── LST-34: TopIndex moves the scrollbar ─────────────────────────────────────────────────────

        [Fact]
        public void TopIndex_survives_the_next_wheel_notch ()
        {
            var list = ShownList (out var form, 50);

            using (form) {
                list.TopIndex = 20;
                Assert.Equal (20, list.TopIndex);

                // A wheel notch scrolls from where the thumb is. With the thumb left at 0 it snapped
                // the list back to the top.
                list.RaiseMouseWheel (Wheel (-120));

                Assert.True (list.TopIndex > 20, $"TopIndex {list.TopIndex} went back towards the top");
            }
        }

        // ── LST-35: key down navigates; Shift-click selects a range ──────────────────────────────────

        [Fact]
        public void Down_arrow_moves_the_selection_on_key_down ()
        {
            using var list = new ListBox ();
            list.Items.AddRange ("a", "b", "c");
            list.SelectedIndex = 0;

            list.RaiseKeyDown (new KeyEventArgs (Keys.Down));
            list.RaiseKeyDown (new KeyEventArgs (Keys.Down));    // auto-repeat: no key up between

            Assert.Equal (2, list.SelectedIndex);
        }

        [Fact]
        public void A_KeyDown_handler_can_claim_the_key ()
        {
            using var list = new ListBox ();
            list.Items.AddRange ("a", "b");
            list.SelectedIndex = 0;
            list.KeyDown += (_, e) => e.Handled = true;

            list.RaiseKeyDown (new KeyEventArgs (Keys.Down));

            Assert.Equal (0, list.SelectedIndex);
        }

        [Fact]
        public void Shift_click_selects_the_range_from_the_anchor ()
        {
            var list = ShownList (out var form, 8);

            using (form) {
                list.SelectionMode = SelectionMode.MultiExtended;
                var changes = 0;
                list.SelectedIndexChanged += (_, _) => changes++;

                list.RaiseMouseDown (Click (list, 1));
                list.RaiseMouseDown (Click (list, 4, Keys.Shift));

                Assert.Equal ([1, 2, 3, 4], list.SelectedIndices.ToArray ());

                // The anchor stays at 1, so a second Shift-click re-ranges from there.
                list.RaiseMouseDown (Click (list, 2, Keys.Shift));

                Assert.Equal ([1, 2], list.SelectedIndices.ToArray ());
                Assert.Equal (3, changes);
            }
        }

        // ── LST-36: ListView.FindItemWithText (string) ───────────────────────────────────────────────

        [Fact]
        public void ListView_FindItemWithText_is_a_prefix_search_including_sub_items ()
        {
            using var view = new ListView ();
            view.Items.Add ("apple");
            var banana = view.Items.Add ("banana");
            var cherry = view.Items.Add ("cherry");
            cherry.SubItems.Add ("red fruit");

            Assert.Same (banana, view.FindItemWithText ("ban"));
            Assert.Same (cherry, view.FindItemWithText ("red"));
        }

        // ── LST-37: no hand cursor ───────────────────────────────────────────────────────────────────

        [Fact]
        public void ListBox_and_ComboBox_use_the_default_cursor ()
        {
            using var list = new ListBox ();
            using var combo = new ComboBox ();

            Assert.Same (Cursors.Default, list.Cursor);
            Assert.Same (Cursors.Default, combo.Cursor);
        }

        // ── LST-38: FullPath of a detached node ──────────────────────────────────────────────────────

        [Fact]
        public void FullPath_of_a_node_in_no_tree_throws ()
        {
            var parent = new TreeNode ("Parent");
            var child = parent.Nodes.Add ("Child");

            Assert.Throws<InvalidOperationException> (() => child.FullPath);
        }

        // ── LST-39: CheckedListBox.Items[i] = value ──────────────────────────────────────────────────

        [Fact]
        public void CheckedListBox_item_replacement_keeps_the_check_state ()
        {
            using var list = new CheckedListBox ();
            list.Items.Add ("old", true);
            list.Items.Add ("other");

            list.Items[0] = "new";

            Assert.Equal ("new", list.Items[0]);
            Assert.True (list.GetItemChecked (0));
            Assert.Equal (2, list.Items.Count);
        }

        // ── LST-40: wheel on a focused, closed combo ─────────────────────────────────────────────────

        [Fact]
        public void The_wheel_moves_a_focused_combos_selection ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 300, Height = 200 };
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange ("a", "b", "c");
            combo.SelectedIndex = 0;
            form.Controls.Add (combo);
            form.Show ();
            combo.Select ();
            Assert.True (combo.Focused);
            var commits = 0;
            combo.SelectionChangeCommitted += (_, _) => commits++;

            combo.RaiseMouseWheel (Wheel (-120));
            Assert.Equal (1, combo.SelectedIndex);

            combo.RaiseMouseWheel (Wheel (120));
            combo.RaiseMouseWheel (Wheel (120));    // clamped at the top
            Assert.Equal (0, combo.SelectedIndex);

            Assert.Equal (2, commits);
        }

        [Fact]
        public void The_wheel_leaves_an_unfocused_combo_alone ()
        {
            // Guard: a wheel passing over a form must not change every combo under the pointer.
            using var combo = new ComboBox ();
            combo.Items.AddRange ("a", "b");
            combo.SelectedIndex = 0;

            combo.RaiseMouseWheel (Wheel (-120));

            Assert.Equal (0, combo.SelectedIndex);
        }

        // ── LST-41: ListBox.Text ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ListBox_Text_is_the_selected_items_display_text ()
        {
            using var list = BoundList ();

            list.SelectedIndex = 1;
            Assert.Equal ("Bob", list.Text);

            list.Text = "carol";
            Assert.Equal (2, list.SelectedIndex);
        }

        // ── LST-42: TreeView.TopNode / TreeNode.IsVisible ────────────────────────────────────────────

        [Fact]
        public void TopNode_scrolls_and_IsVisible_follows_the_scroll ()
        {
            HeadlessRenderer.Use ();
            using var form = new Form { Width = 400, Height = 300 };
            var tree = new TreeView { Width = 200, Height = 120 };

            for (var i = 0; i < 50; i++)
                tree.Nodes.Add ($"Node {i}");

            form.Controls.Add (tree);
            form.Show ();
            PaintSurface.RenderOnForm (tree, 1f).Dispose ();

            Assert.Same (tree.Nodes[0], tree.TopNode);
            Assert.True (tree.Nodes[0].IsVisible);
            Assert.False (tree.Nodes[40].IsVisible);

            tree.TopNode = tree.Nodes[30];

            Assert.Same (tree.Nodes[30], tree.TopNode);
            Assert.False (tree.Nodes[0].IsVisible);
            Assert.True (tree.Nodes[30].IsVisible);
        }

        [Fact]
        public void A_node_in_no_tree_is_not_visible ()
        {
            Assert.False (new TreeNode ("loose").IsVisible);
        }
    }
}
