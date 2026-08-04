using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Covers the ToolStrip hosted-editor facades (docs/winforms-gap-plan.md).
    ///
    /// The defect worth guarding is not a missing member but a disconnected one: ToolStripTextBox
    /// kept Text in a private string while Cut/Paste/SelectAll operated on the hosted TextBox, so the
    /// two disagreed and the editing verbs appeared to do nothing. Most of these assert that a write
    /// on the item is visible on the hosted control and the other way round.
    /// </summary>
    public class ToolStripHostParityTests
    {
        [Fact]
        public void Item_text_and_hosted_text_are_the_same_value ()
        {
            var item = new ToolStripTextBox ();

            item.Text = "from the item";
            Assert.Equal ("from the item", item.TextBox.Text);

            item.TextBox.Text = "from the control";
            Assert.Equal ("from the control", item.Text);
        }

        [Fact]
        public void The_editing_verbs_act_on_the_text_the_item_reports ()
        {
            // The old shape passed this only by accident: Cut emptied the hosted box while Text still
            // returned the item's own string.
            var item = new ToolStripTextBox { Text = "hello world" };
            item.SelectionStart = 0;
            item.SelectionLength = 6;

            item.Cut ();

            Assert.Equal ("world", item.Text);
        }

        [Fact]
        public void SelectAll_selects_what_the_item_reports_as_its_text ()
        {
            var item = new ToolStripTextBox { Text = "abcdef" };

            item.SelectAll ();

            Assert.Equal ("abcdef", item.SelectedText);
            Assert.Equal (6, item.SelectionLength);
        }

        [Fact]
        public void The_state_properties_reach_the_hosted_text_box ()
        {
            var item = new ToolStripTextBox ();

            item.ReadOnly = true;
            item.MaxLength = 20;
            item.Multiline = true;
            item.AcceptsTab = true;

            Assert.True (item.TextBox.ReadOnly);
            Assert.Equal (20, item.TextBox.MaxLength);
            Assert.True (item.TextBox.Multiline);
            Assert.True (item.TextBox.AcceptsTab);
        }

        [Fact]
        public void TextChanged_on_the_item_fires_for_an_edit_to_the_hosted_box ()
        {
            var item = new ToolStripTextBox ();
            var raised = 0;
            item.TextChanged += (_, _) => raised++;

            item.TextBox.Text = "typed";

            Assert.Equal (1, raised);
        }

        [Fact]
        public void The_line_family_forwards_to_the_hosted_box ()
        {
            var item = new ToolStripTextBox { Text = "one\ntwo\nthree" };

            Assert.Equal (["one", "two", "three"], item.Lines);
            Assert.Equal (13, item.TextLength);
            Assert.Equal (1, item.GetLineFromCharIndex (5));
            Assert.Equal (8, item.GetFirstCharIndexFromLine (2));
        }

        [Fact]
        public void ToolStripTextBox_is_a_control_host_as_it_is_upstream ()
        {
            // Matters for the common `foreach (ToolStripItem i in strip.Items) if (i is
            // ToolStripControlHost h)` shape, which used to miss both editors entirely.
            var item = new ToolStripTextBox ();

            Assert.IsAssignableFrom<ToolStripControlHost> (item);
            Assert.Same (item.TextBox, item.Control);
        }

        [Fact]
        public void ToolStripComboBox_hosts_the_combo_it_exposes ()
        {
            using var item = new ToolStripComboBox ();

            Assert.IsAssignableFrom<ToolStripControlHost> (item);
            Assert.Same (item.ComboBox, item.Control);
        }

        [Fact]
        public void ToolStripComboBox_forwards_the_list_properties ()
        {
            using var item = new ToolStripComboBox ();
            item.Items.Add ("alpha");
            item.Items.Add ("beta");

            item.SelectedItem = "beta";
            Assert.Equal (1, item.SelectedIndex);
            Assert.Equal ("beta", item.ComboBox.SelectedItem);

            item.MaxDropDownItems = 4;
            item.Sorted = true;
            item.IntegralHeight = false;

            Assert.Equal (4, item.ComboBox.MaxDropDownItems);
            Assert.True (item.ComboBox.Sorted);
            Assert.False (item.ComboBox.IntegralHeight);
        }

        [Fact]
        public void ToolStripComboBox_find_methods_search_the_hosted_items ()
        {
            using var item = new ToolStripComboBox ();
            item.Items.Add ("alpha");
            item.Items.Add ("alphabet");
            item.Items.Add ("beta");

            Assert.Equal (0, item.FindString ("alph"));
            Assert.Equal (1, item.FindString ("alph", 0));      // starts after the given index
            Assert.Equal (1, item.FindStringExact ("alphabet"));
            Assert.Equal (-1, item.FindStringExact ("gamma"));
        }

        [Fact]
        public void DropDownStyleChanged_is_raised_through_the_item ()
        {
            using var item = new ToolStripComboBox ();
            var raised = 0;
            item.DropDownStyleChanged += (_, _) => raised++;

            item.DropDownStyle = ComboBoxStyle.DropDownList;
            item.DropDownStyle = ComboBoxStyle.DropDownList;   // no change, no event

            Assert.Equal (1, raised);
        }

        [Fact]
        public void GetItemHeight_rejects_a_negative_index ()
        {
            using var item = new ToolStripComboBox ();
            item.Items.Add ("alpha");

            Assert.Equal (item.ComboBox.ItemHeight, item.GetItemHeight (0));
            Assert.Throws<System.ArgumentOutOfRangeException> (() => item.GetItemHeight (-1));
        }

        [Fact]
        public void The_host_accessible_object_describes_the_hosted_control ()
        {
            var item = new ToolStripTextBox ();
            item.TextBox.AccessibleName = "Search";

            Assert.Equal ("Search", item.AccessibilityObject.Name);
            Assert.Same (item.AccessibilityObject, item.AccessibilityObject);

            item.AccessibleName = "Search the catalogue";
            Assert.Equal ("Search the catalogue", item.AccessibilityObject.Name);
        }

        [Fact]
        public void The_host_forwards_focus_and_validation_to_its_control ()
        {
            var control = new TextBox ();
            var host = new ToolStripControlHost (control);

            host.CausesValidation = false;
            Assert.False (control.CausesValidation);

            host.Site = null;
            Assert.Null (control.Site);
            Assert.Equal (control.Focused, host.Focused);
        }
    }
}
