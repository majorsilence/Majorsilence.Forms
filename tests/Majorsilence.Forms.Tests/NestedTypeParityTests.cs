using System;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms nests its collection and accessibility types inside the control that owns them, and
    // designer files always spell the nested name out. These tests are written the way a designer
    // file is -- with the nested name as the declared type -- because that is exactly the spelling
    // that used not to compile.
    public class NestedTypeParityTests
    {
        [Fact]
        public void A_list_views_items_are_its_nested_collection ()
        {
            using var view = new ListView ();

            ListView.ListViewItemCollection items = view.Items;

            Assert.Same (view.Items, items);
        }

        [Fact]
        public void The_nested_collection_is_still_the_namespace_scope_one ()
        {
            using var view = new ListView ();

            // Nesting must not have orphaned the old spelling: code written against it still
            // compiles and still sees the same object.
            ListViewItemCollection items = view.Items;

            Assert.Same (view.Items, items);
        }

        [Fact]
        public void A_tab_controls_pages_are_its_nested_collection ()
        {
            using var tabs = new TabControl ();

            TabControl.TabPageCollection pages = tabs.TabPages;

            Assert.Same (tabs.TabPages, pages);
        }

        [Fact]
        public void A_list_views_columns_are_its_nested_collection ()
        {
            using var view = new ListView ();

            ListView.ColumnHeaderCollection columns = view.Columns;

            Assert.Same (view.Columns, columns);
        }

        [Fact]
        public void A_button_reports_its_own_accessible_name ()
        {
            using var button = new Button { Text = "&Save" };
            var accessible = new ButtonBase.ButtonBaseAccessibleObject (button);

            Assert.Equal ("&Save", accessible.Name);
            Assert.Equal ("Alt+S", accessible.KeyboardShortcut);

            button.AccessibleName = "Save the document";
            Assert.Equal ("Save the document", accessible.Name);
        }

        [Fact]
        public void A_double_ampersand_is_not_a_mnemonic ()
        {
            using var button = new Button { Text = "Save && &Close" };
            var accessible = new ButtonBase.ButtonBaseAccessibleObject (button);

            // "&&" is an escaped ampersand in the label, not an access key.
            Assert.Equal ("Alt+C", accessible.KeyboardShortcut);
        }

        [Fact]
        public void A_check_boxs_accessible_object_toggles_it ()
        {
            using var box = new CheckBox ();
            var accessible = new CheckBox.CheckBoxAccessibleObject (box);

            Assert.Equal (AccessibleRole.CheckButton, accessible.Role);
            Assert.Equal ("Check", accessible.DefaultAction);

            accessible.DoDefaultAction ();

            Assert.True (box.Checked);
            Assert.Equal ("Uncheck", accessible.DefaultAction);
            Assert.True (accessible.State.HasFlag (AccessibleStates.Checked));
        }

        [Fact]
        public void A_radio_buttons_accessible_object_selects_it ()
        {
            using var radio = new RadioButton ();
            var accessible = new RadioButton.RadioButtonAccessibleObject (radio);

            Assert.Equal (AccessibleRole.RadioButton, accessible.Role);
            Assert.Equal ("Select", accessible.DefaultAction);

            accessible.DoDefaultAction ();

            Assert.True (radio.Checked);
            Assert.True (accessible.State.HasFlag (AccessibleStates.Checked));
        }

        [Fact]
        public void A_disabled_control_reports_unavailable ()
        {
            using var button = new Button { Enabled = false };
            var accessible = new ButtonBase.ButtonBaseAccessibleObject (button);

            Assert.True (accessible.State.HasFlag (AccessibleStates.Unavailable));
        }

        [Fact]
        public void A_tool_strips_accessible_object_walks_its_items ()
        {
            using var strip = new ToolStrip ();
            var item = new ToolStripButton ("Save");
            strip.Items.Add (item);

            var accessible = new ToolStrip.ToolStripAccessibleObject (strip);

            Assert.Equal (AccessibleRole.ToolBar, accessible.Role);
            Assert.Equal (1, accessible.GetChildCount ());
            Assert.Same (item.AccessibilityObject, accessible.GetChild (0));
            Assert.Null (accessible.GetChild (5));
        }

        [Fact]
        public void An_items_accessible_object_can_be_given_extra_state ()
        {
            var item = new ToolStripButton ("Save");
            var accessible = new ToolStripItem.ToolStripItemAccessibleObject (item);

            Assert.False (accessible.State.HasFlag (AccessibleStates.Pressed));

            accessible.AddState (AccessibleStates.Pressed);

            Assert.True (accessible.State.HasFlag (AccessibleStates.Pressed));
        }

        [Fact]
        public void A_combo_boxs_child_accessible_object_names_its_owner ()
        {
            using var combo = new ComboBox { Text = "Pick one" };
            var accessible = new ComboBox.ChildAccessibleObject (combo, IntPtr.Zero);

            Assert.Equal ("Pick one", accessible.Name);
        }

        [Fact]
        public void A_tool_strip_panel_row_collection_holds_rows ()
        {
            using var panel = new ToolStripPanel ();
            var row = new ToolStripPanelRow (panel);

            var rows = new ToolStripPanel.ToolStripPanelRowCollection (panel, [row]);

            Assert.Same (panel, rows.Owner);
            Assert.Equal (1, rows.Count);
            Assert.Same (row, rows[0]);
        }
    }
}
