using System.Drawing;
using WF = System.Windows.Forms;

namespace ThemeStudio.WinForms
{
    // One of each System.Windows.Forms control the applier maps (docs/theming-winforms.md), laid out in
    // a scrollable panel. Nothing here sets a colour or font explicitly -- every value comes from the
    // stylesheet through WinFormsCssTheme -- which is exactly what a migrated WinForms form looks like.
    public sealed class PreviewPanel : WF.Panel
    {
        private readonly WF.ProgressBar progress;
        private readonly WF.Label tokens;

        public PreviewPanel ()
        {
            AutoScroll = true;

            var menu = new WF.MenuStrip ();
            var fileMenu = new WF.ToolStripMenuItem ("File");
            fileMenu.DropDownItems.Add ("New");
            fileMenu.DropDownItems.Add ("Open…");
            fileMenu.DropDownItems.Add (new WF.ToolStripSeparator ());
            fileMenu.DropDownItems.Add ("Exit");
            var editMenu = new WF.ToolStripMenuItem ("Edit");
            editMenu.DropDownItems.Add ("Undo");
            editMenu.DropDownItems.Add ("Redo");
            var disabledMenu = new WF.ToolStripMenuItem ("Disabled") { Enabled = false };
            menu.Items.AddRange (new WF.ToolStripItem[] { fileMenu, editMenu, disabledMenu });

            var tools = new WF.ToolStrip { GripStyle = WF.ToolStripGripStyle.Hidden };
            tools.Items.Add (new WF.ToolStripButton ("Cut"));
            tools.Items.Add (new WF.ToolStripButton ("Copy"));
            tools.Items.Add (new WF.ToolStripSeparator ());
            tools.Items.Add (new WF.ToolStripButton ("Toggle") { CheckOnClick = true, Checked = true });
            tools.Items.Add (new WF.ToolStripDropDownButton ("More", null, new WF.ToolStripMenuItem ("First"), new WF.ToolStripMenuItem ("Second")));

            var statusStrip = new WF.StatusStrip ();
            statusStrip.Items.Add (new WF.ToolStripStatusLabel ("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft });
            statusStrip.Items.Add (new WF.ToolStripStatusLabel ("Ln 1, Col 1"));

            var body = new WF.TableLayoutPanel { Dock = WF.DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new WF.Padding (8) };
            body.ColumnStyles.Add (new WF.ColumnStyle (WF.SizeType.Percent, 50f));
            body.ColumnStyles.Add (new WF.ColumnStyle (WF.SizeType.Percent, 50f));

            // ---- left column ----
            var left = new WF.FlowLayoutPanel { Dock = WF.DockStyle.Fill, FlowDirection = WF.FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

            var buttons = new WF.FlowLayoutPanel { AutoSize = true, Margin = new WF.Padding (0, 0, 0, 8) };
            buttons.Controls.Add (new WF.Button { Text = "Button", Width = 110, Height = 30 });
            buttons.Controls.Add (new WF.Button { Text = "Default", Width = 110, Height = 30 });
            buttons.Controls.Add (new WF.Button { Text = "Disabled", Width = 110, Height = 30, Enabled = false });
            left.Controls.Add (buttons);

            var toggles = new WF.FlowLayoutPanel { AutoSize = true, Margin = new WF.Padding (0, 0, 0, 8) };
            toggles.Controls.Add (new WF.CheckBox { Text = "CheckBox", Checked = true, AutoSize = true });
            toggles.Controls.Add (new WF.RadioButton { Text = "RadioButton", Checked = true, AutoSize = true });
            toggles.Controls.Add (new WF.LinkLabel { Text = "LinkLabel", AutoSize = true, Margin = new WF.Padding (8, 5, 0, 0) });
            left.Controls.Add (toggles);

            var inputs = new WF.FlowLayoutPanel { AutoSize = true, Margin = new WF.Padding (0, 0, 0, 8) };
            inputs.Controls.Add (new WF.TextBox { Text = "TextBox", Width = 160 });
            inputs.Controls.Add (new WF.NumericUpDown { Value = 42, Width = 90 });
            var combo = new WF.ComboBox { Width = 140, DropDownStyle = WF.ComboBoxStyle.DropDownList };
            combo.Items.AddRange (new object[] { "ComboBox", "Second", "Third" });
            combo.SelectedIndex = 0;
            inputs.Controls.Add (combo);
            inputs.Controls.Add (new WF.DateTimePicker { Width = 200 });
            left.Controls.Add (inputs);

            var group = new WF.GroupBox { Text = "GroupBox", Width = 420, Height = 90, Margin = new WF.Padding (0, 0, 0, 8) };
            group.Controls.Add (new WF.Label { Text = "Label inside a group box.", Left = 12, Top = 24, AutoSize = true });
            group.Controls.Add (new WF.TrackBar { Left = 12, Top = 44, Width = 200, Value = 4 });
            progress = new WF.ProgressBar { Left = 220, Top = 50, Width = 180, Value = 60 };
            group.Controls.Add (progress);
            left.Controls.Add (group);

            var lists = new WF.FlowLayoutPanel { AutoSize = true, Margin = new WF.Padding (0, 0, 0, 8) };
            var listBox = new WF.ListBox { Width = 130, Height = 110 };
            listBox.Items.AddRange (new object[] { "ListBox", "Second", "Third", "Fourth" });
            listBox.SelectedIndex = 1;
            lists.Controls.Add (listBox);

            var checkedList = new WF.CheckedListBox { Width = 130, Height = 110 };
            checkedList.Items.Add ("CheckedListBox", true);
            checkedList.Items.Add ("Second", false);
            checkedList.Items.Add ("Third", true);
            lists.Controls.Add (checkedList);

            var tree = new WF.TreeView { Width = 150, Height = 110 };
            var root = tree.Nodes.Add ("TreeView");
            root.Nodes.Add ("Child 1");
            root.Nodes.Add ("Child 2").Nodes.Add ("Grandchild");
            tree.ExpandAll ();
            lists.Controls.Add (tree);
            left.Controls.Add (lists);

            var listView = new WF.ListView { View = WF.View.Details, Width = 420, Height = 100, FullRowSelect = true, Margin = new WF.Padding (0, 0, 0, 8) };
            listView.Columns.Add ("Name", 180);
            listView.Columns.Add ("Size", 100);
            listView.Columns.Add ("Kind", 120);
            listView.Items.Add (new WF.ListViewItem (new[] { "ListView", "12 KB", "Document" }));
            listView.Items.Add (new WF.ListViewItem (new[] { "Second", "3 KB", "Image" }));
            listView.Items.Add (new WF.ListViewItem (new[] { "Third", "88 KB", "Archive" }) { Selected = true });
            left.Controls.Add (listView);

            tokens = new WF.Label { AutoSize = true, MaximumSize = new Size (420, 0), Margin = new WF.Padding (0, 4, 0, 0) };
            left.Controls.Add (tokens);

            // ---- right column ----
            var right = new WF.FlowLayoutPanel { Dock = WF.DockStyle.Fill, FlowDirection = WF.FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

            var grid = new WF.DataGridView {
                Width = 440, Height = 190, Margin = new WF.Padding (0, 0, 0, 8),
                AllowUserToAddRows = false, RowHeadersWidth = 32,
                AutoSizeColumnsMode = WF.DataGridViewAutoSizeColumnsMode.Fill,
            };
            grid.Columns.Add ("name", "Name");
            grid.Columns.Add ("qty", "Qty");
            grid.Columns.Add ("price", "Price");
            for (var i = 1; i <= 6; i++)
                grid.Rows.Add ($"Item {i}", i * 3, (i * 4.5).ToString ("C", System.Globalization.CultureInfo.CurrentCulture));
            right.Controls.Add (grid);

            var tabs = new WF.TabControl { Width = 440, Height = 150, Margin = new WF.Padding (0, 0, 0, 8) };
            var tabOne = new WF.TabPage ("First");
            tabOne.Controls.Add (new WF.Label { Text = "TabPage content (a Panel).", Left = 12, Top = 12, AutoSize = true });
            tabOne.Controls.Add (new WF.Button { Text = "In a tab", Left = 12, Top = 40, Width = 110, Height = 30 });
            tabs.TabPages.Add (tabOne);
            tabs.TabPages.Add (new WF.TabPage ("Second"));
            tabs.TabPages.Add (new WF.TabPage ("Third"));
            right.Controls.Add (tabs);

            var propertyGrid = new WF.PropertyGrid { Width = 440, Height = 220, Margin = new WF.Padding (0, 0, 0, 8), SelectedObject = new Sample () };
            right.Controls.Add (propertyGrid);

            var calendar = new WF.MonthCalendar { MaxSelectionCount = 1 };
            right.Controls.Add (calendar);

            body.Controls.Add (left, 0, 0);
            body.Controls.Add (right, 1, 0);

            // Docking resolves in reverse z-order: the Fill control goes in first.
            Controls.Add (body);
            Controls.Add (statusStrip);
            Controls.Add (tools);
            Controls.Add (menu);

            var context = new WF.ContextMenuStrip ();
            context.Items.Add ("ContextMenuStrip");
            context.Items.Add ("Second item");
            ContextMenuStrip = context;
        }

        /// <summary>Refreshes the token readout under the controls after a theme apply.</summary>
        public void RefreshFromTheme ()
        {
            tokens.Text =
                $"Tokens now: accent {Majorsilence.Forms.Theme.FormatTokenValue (Majorsilence.Forms.ThemeCssReference.FindToken ("--accent-color")!)}, " +
                $"background {Majorsilence.Forms.Theme.FormatTokenValue (Majorsilence.Forms.ThemeCssReference.FindToken ("--background-color")!)}, " +
                $"foreground {Majorsilence.Forms.Theme.FormatTokenValue (Majorsilence.Forms.ThemeCssReference.FindToken ("--foreground-color")!)}. " +
                "TrackBar groove/thumb, check and radio glyphs, scroll bars, the GroupBox frame and the calendar chrome stay native-drawn (see the support matrix).";
            progress.Value = 60;
        }

        // Something for the PropertyGrid to show.
        private sealed class Sample
        {
            public string Name { get; set; } = "PropertyGrid";
            public int Count { get; set; } = 3;
            public bool Enabled { get; set; } = true;
            public Color Colour { get; set; } = Color.Orange;
        }
    }
}
