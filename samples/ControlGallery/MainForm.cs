using ControlGallery.Panels;
using Majorsilence.Forms;
using SkiaSharp;

namespace ControlGallery
{
    public class MainForm : Form
    {
        private Panel? current_panel;
        private readonly TreeView tree;
        private readonly Dictionary<string, Panel> _panelCache = new ();
        private Queue<string>? _preWarmQueue;

        public MainForm ()
        {
            tree = new TreeView {
                Dock = DockStyle.Left,
                ShowDropdownGlyph = false
            };

            tree.Style.Border.Width = 0;
            tree.Style.Border.Right.Width = 1;

            tree.Items.Add ("Button", ImageLoader.Get ("button.png"));
            tree.Items.Add ("CheckBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ComboBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("DataGridView", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Dialogs", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FileDialogs", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FlowLayoutPanel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FormPaint", ImageLoader.Get ("button.png"));
            tree.Items.Add ("GroupBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("FormShortcuts", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ImageList", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Label", ImageLoader.Get ("button.png"));
            tree.Items.Add ("LinkLabel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ListBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ListView", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MDI", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Menu", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MenuStrip", ImageLoader.Get ("button.png"));
            tree.Items.Add ("MessageBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("NavigationPane", ImageLoader.Get ("button.png"));
            tree.Items.Add ("NumericUpDown", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Panel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("PictureBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ProgressBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("RadioButton", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Ribbon", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ScrollableControl", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ScrollBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("SplitContainer", ImageLoader.Get ("button.png"));
            tree.Items.Add ("StatusBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("StatusStrip", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TabControl", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: Controls", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: GridView", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: PageView", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: PropertyGrid", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: TabbedForm", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: PdfViewer", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: RichTextEditor", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: SpellCheck", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: DesktopAlert", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: GridExport", ImageLoader.Get ("button.png"));
            tree.Items.Add ("Telerik: Scheduler", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TableLayoutPanel", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TabStrip", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TextBox", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TimePicker", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TitleBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("ToolBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TrackBar", ImageLoader.Get ("button.png"));
            tree.Items.Add ("TreeView", ImageLoader.Get ("button.png"));

            tree.ItemSelected += Tree_ItemSelected;
            Controls.Add (tree);

            Text = "Control Gallery";
            Image = ImageLoader.Get ("button.png");
        }

        private void Tree_ItemSelected (object? sender, EventArgs<TreeViewItem> e)
        {
            if (current_panel != null) {
                Controls.Remove (current_panel);

                if (current_panel is BasePanel bp)
                    bp.UnloadPanel ();

                // Keep the panel in the cache for instant reuse — do NOT dispose.
            }

            var name = e.Value.Text;

            if (!_panelCache.TryGetValue (name, out var panel)) {
                panel = CreatePanel (name);

                if (panel != null)
                    _panelCache[name] = panel;
            }

            current_panel = panel;

            if (panel != null) {
                panel.Dock = DockStyle.Fill;
                Controls.Insert (0, panel);
                // Ensure the panel renders even if IsDirty was cleared by a prior pre-warm.
                panel.Invalidate ();

                if (panel is BasePanel bp2)
                    bp2.LoadPanel ();
            }
        }

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            if (tree.SelectedItem.Text == "FormPaint") {
                e.Canvas.FillRectangle (Scale (300), Scale (50), Scale (100), Scale (100), SKColors.Red);

                DrawThemeColor (e.Canvas, Scale (450), Scale (50), Scale (150), Scale (40), Theme.BackgroundColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (90), Scale (150), Scale (40), Theme.ControlLowColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (130), Scale (150), Scale (40), Theme.ControlMidColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (170), Scale (150), Scale (40), Theme.ControlMidHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (210), Scale (150), Scale (40), Theme.ControlHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (250), Scale (150), Scale (40), Theme.ControlVeryHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (290), Scale (150), Scale (40), Theme.ControlHighlightLowColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (330), Scale (150), Scale (40), Theme.ControlHighlightMidColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (370), Scale (150), Scale (40), Theme.ControlHighlightHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (410), Scale (150), Scale (40), Theme.BorderLowColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (450), Scale (150), Scale (40), Theme.BorderMidColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (490), Scale (150), Scale (40), Theme.BorderHighColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (530), Scale (150), Scale (40), Theme.ForegroundColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (570), Scale (150), Scale (40), Theme.ForegroundDisabledColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (610), Scale (150), Scale (40), Theme.ForegroundColorOnAccent);
                DrawThemeColor (e.Canvas, Scale (450), Scale (650), Scale (150), Scale (40), Theme.AccentColor);
                DrawThemeColor (e.Canvas, Scale (450), Scale (690), Scale (150), Scale (40), Theme.AccentColor2);
                DrawThemeColor (e.Canvas, Scale (450), Scale (730), Scale (150), Scale (40), Theme.WarningHighlightColor);
            }

            // After each paint, silently pre-warm the next uncached panel so that
            // first-visit lag is eliminated before the user reaches those items.
            _preWarmQueue ??= new Queue<string> (GetPreWarmNames ());

            while (_preWarmQueue.Count > 0) {
                var name = _preWarmQueue.Dequeue ();

                if (_panelCache.ContainsKey (name))
                    continue; // already visible or previously pre-warmed

                var panel = CreatePanel (name);

                if (panel != null) {
                    // Render into a throwaway bitmap: populates the TextBlock cache and
                    // pre-renders child back buffers so first-show is a cheap blit.
                    panel.PreWarm ((float)Scaling);
                    _panelCache[name] = panel;
                }

                // One panel per frame keeps the UI responsive during the warm-up burst.
                Invalidate ();
                break;
            }
        }

        private static string[] GetPreWarmNames () =>
            new[]
            {
                "Button", "CheckBox", "ComboBox", "DataGridView", "Dialogs", "FileDialogs",
                "FlowLayoutPanel", "GroupBox", "ImageList", "Label", "LinkLabel",
                "ListBox", "ListView", "Menu", "MenuStrip", "MessageBox",
                "NavigationPane", "NumericUpDown", "Panel", "PictureBox", "ProgressBar",
                "RadioButton", "Ribbon", "ScrollableControl", "ScrollBar", "SplitContainer",
                "StatusBar", "StatusStrip", "TabControl", "TableLayoutPanel", "TabStrip",
                "TextBox", "TimePicker", "TitleBar", "ToolBar", "TrackBar", "TreeView",
                // Panels with side effects (open child windows / register events) are
                // pre-warmed last; their PreWarm just renders static children into back buffers.
                "Telerik: Controls", "Telerik: GridView", "Telerik: PageView",
                "Telerik: PropertyGrid", "Telerik: TabbedForm",
                "Telerik: PdfViewer", "Telerik: RichTextEditor", "Telerik: SpellCheck",
                "Telerik: DesktopAlert", "Telerik: GridExport", "Telerik: Scheduler",
                "FormShortcuts", "MDI",
            };

        private Panel? CreatePanel (string text)
        {
            switch (text) {
                case "Button":
                    return new ButtonPanel ();
                case "CheckBox":
                    return new CheckBoxPanel ();
                case "ComboBox":
                    return new ComboBoxPanel ();
                case "DataGridView":
                    return new DataGridViewPanel ();
                case "Dialogs":
                    return new DialogPanel ();
                case "FileDialogs":
                    return new FileDialogPanel ();
                case "FlowLayoutPanel":
                    return new FlowLayoutPanelPanel ();
                case "GroupBox":
                    return new GroupBoxPanel ();
                case "FormShortcuts":
                    return new FormShortcutsPanel (this);
                case "ImageList":
                    return new ImageListPanel ();
                case "Label":
                    return new LabelPanel ();
                case "LinkLabel":
                    return new LinkLabelPanel ();
                case "ListBox":
                    return new ListBoxPanel ();
                case "ListView":
                    return new ListViewPanel ();
                case "MDI":
                    return new MdiPanel ();
                case "Menu":
                    return new MenuPanel ();
                case "MenuStrip":
                    return new MenuStripPanel ();
                case "MessageBox":
                    return new MessageBoxPanel ();
                case "NavigationPane":
                    return new NavigationPanePanel ();
                case "NumericUpDown":
                    return new NumericUpDownPanel ();
                case "Panel":
                    return new PanelPanel ();
                case "PictureBox":
                    return new PictureBoxPanel ();
                case "ProgressBar":
                    return new ProgressBarPanel ();
                case "RadioButton":
                    return new RadioButtonPanel ();
                case "Ribbon":
                    return new RibbonPanel ();
                case "ScrollableControl":
                    return new ScrollableControlPanel ();
                case "ScrollBar":
                    return new ScrollBarPanel ();
                case "SplitContainer":
                    return new SplitContainerPanel ();
                case "StatusBar":
                    return new Panels.StatusBarPanel ();
                case "StatusStrip":
                    return new StatusStripPanel ();
                case "TabControl":
                    return new TabControlPanel ();
                case "Telerik: Controls":
                    return new TelerikControlsPanel ();
                case "Telerik: GridView":
                    return new TelerikGridViewPanel ();
                case "Telerik: PageView":
                    return new TelerikPageViewPanel ();
                case "Telerik: PropertyGrid":
                    return new TelerikPropertyGridPanel ();
                case "Telerik: TabbedForm":
                    return new TelerikTabbedFormPanel ();
                case "Telerik: PdfViewer":
                    return new TelerikPdfViewerPanel ();
                case "Telerik: RichTextEditor":
                    return new TelerikRichTextEditorPanel ();
                case "Telerik: SpellCheck":
                    return new TelerikSpellCheckPanel ();
                case "Telerik: DesktopAlert":
                    return new TelerikDesktopAlertPanel ();
                case "Telerik: GridExport":
                    return new TelerikGridExportPanel ();
                case "Telerik: Scheduler":
                    return new TelerikSchedulerPanel ();
                case "TableLayoutPanel":
                    return new TableLayoutPanelPanel ();
                case "TabStrip":
                    return new TabStripPanel ();
                case "TextBox":
                    return new TextBoxPanel ();
                case "TimePicker":
                    return new TimePickerPanel ();
                case "TitleBar":
                    return new TitleBarPanel ();
                case "ToolBar":
                    return new ToolBarPanel ();
                case "TrackBar":
                    return new TrackBarPanel ();
                case "TreeView":
                    return new TreeViewPanel ();
            }

            return null;
        }

        protected override void OnPaintBackground (PaintEventArgs e)
        {
            base.OnPaintBackground (e);

            if (tree.SelectedItem.Text == "FormPaint")
                e.Canvas.Clear (SKColors.Green);
        }

        private static void DrawThemeColor (SKCanvas canvas, int x, int y, int width, int height, SKColor color)
        {
            canvas.FillRectangle (x, y, width, height, color);
            canvas.DrawText (color.ToString (), Theme.UIFont, 12, new System.Drawing.Rectangle (x + 4, y + 4, width - 8, height - 8), Theme.ForegroundColor, ContentAlignment.MiddleLeft);
        }

        private int Scale (int value) => (int)(value * Scaling);
    }
}
