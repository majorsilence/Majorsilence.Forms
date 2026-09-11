using System;
using System.Drawing;
using System.Linq;
using Majorsilence.Forms;
using Majorsilence.Forms.Renderers;
using SkiaSharp;

namespace ThemeStudio
{
    // One instance of every themable control, grouped on tabs, plus a swatch page for the tokens. The
    // controls carry no colours of their own so that everything visible comes from the theme -- which is
    // what makes the preview an honest answer to "what will my app look like".
    public class PreviewPanel : Panel
    {
        private readonly TokenSwatches swatches;
        private readonly TabControl tabs;

        public PreviewPanel ()
        {
            tabs = Controls.Add (new TabControl { Dock = DockStyle.Fill });

            BuildInputs (tabs.TabPages.Add ("Inputs"));
            BuildLists (tabs.TabPages.Add ("Lists & grids"));
            BuildChrome (tabs.TabPages.Add ("Menus & chrome"));

            // The swatch list is taller than the page once the parts are included, so the page scrolls
            // and the list takes its preferred height rather than filling.
            var tokens = tabs.TabPages.Add ("Tokens");
            tokens.AutoScroll = true;
            swatches = tokens.Controls.Add (new TokenSwatches { Dock = DockStyle.Top, Height = TokenSwatches.PreferredHeight });
        }

        public void RefreshTokens () => swatches.Invalidate ();

        /// <summary>Shows one of the preview tabs (0 = Inputs, 1 = Lists &amp; grids, 2 = Menus &amp; chrome, 3 = Tokens).</summary>
        public void SelectTab (int index) => tabs.SelectedIndex = Math.Max (0, Math.Min (index, tabs.TabPages.Count - 1));

        private static void BuildInputs (TabPage page)
        {
            page.Controls.Add (new Label { Text = "Buttons", Left = 16, Top = 12, Width = 200 });
            page.Controls.Add (new Button { Text = "OK", Left = 16, Top = 36, Width = 100, Height = 30 });
            page.Controls.Add (new Button { Text = "Cancel", Left = 124, Top = 36, Width = 100, Height = 30 });
            page.Controls.Add (new Button { Text = "Disabled", Left = 232, Top = 36, Width = 100, Height = 30, Enabled = false });

            page.Controls.Add (new Label { Text = "Check boxes and radio buttons", Left = 16, Top = 84, Width = 300 });
            page.Controls.Add (new CheckBox { Text = "Checked", Left = 16, Top = 108, Width = 120, Checked = true });
            page.Controls.Add (new CheckBox { Text = "Unchecked", Left = 140, Top = 108, Width = 120 });
            page.Controls.Add (new CheckBox { Text = "Disabled", Left = 264, Top = 108, Width = 120, Enabled = false, Checked = true });

            var group = page.Controls.Add (new GroupBox { Text = "Shipping", Left = 16, Top = 140, Width = 340, Height = 96 });
            group.Controls.Add (new RadioButton { Text = "Standard", Left = 12, Top = 26, Width = 100, Checked = true });
            group.Controls.Add (new RadioButton { Text = "Express", Left = 120, Top = 26, Width = 100 });
            group.Controls.Add (new RadioButton { Text = "Disabled", Left = 228, Top = 26, Width = 100, Enabled = false });
            group.Controls.Add (new LinkLabel { Text = "Delivery options…", Left = 12, Top = 58, Width = 200 });

            page.Controls.Add (new Label { Text = "Text inputs", Left = 16, Top = 250, Width = 200 });
            page.Controls.Add (new TextBox { Text = "Jane Appleseed", Left = 16, Top = 274, Width = 200 });
            page.Controls.Add (new TextBox { Placeholder = "Placeholder text", Left = 224, Top = 274, Width = 200 });
            page.Controls.Add (new TextBox { Text = "Read only", Left = 16, Top = 308, Width = 200, ReadOnly = true });
            page.Controls.Add (new TextBox { Text = "Disabled", Left = 224, Top = 308, Width = 200, Enabled = false });
            page.Controls.Add (new TextBox { Multiline = true, Text = "Multi-line text.\nSelect me to see the selection colour.", Left = 16, Top = 342, Width = 408, Height = 70 });

            page.Controls.Add (new Label { Text = "Selectors and numbers", Left = 16, Top = 426, Width = 300 });
            var combo = page.Controls.Add (new ComboBox { Left = 16, Top = 450, Width = 200 });
            combo.Items.Add ("Apples");
            combo.Items.Add ("Oranges");
            combo.Items.Add ("Pears");
            combo.SelectedIndex = 0;
            page.Controls.Add (new NumericUpDown { Left = 224, Top = 450, Width = 120, Value = 42 });

            page.Controls.Add (new Label { Text = "Progress and sliders", Left = 16, Top = 494, Width = 300 });
            page.Controls.Add (new ProgressBar { Left = 16, Top = 518, Width = 300, Value = 62 });
            page.Controls.Add (new TrackBar { Left = 16, Top = 552, Width = 300, Value = 3 });

            page.Controls.Add (new Label { Text = "A Label with the ambient font and colour.", Left = 16, Top = 606, Width = 400 });

            var custom = page.Controls.Add (new Label { Text = "Per-control values always win over a theme rule.", Left = 16, Top = 630, Width = 420 });
            custom.Style.ForegroundColor = new SKColor (0xe0, 0x6c, 0x00);
        }

        private static void BuildLists (TabPage page)
        {
            page.Controls.Add (new Label { Text = "ListBox", Left = 16, Top = 12, Width = 200 });
            var list = page.Controls.Add (new ListBox { Left = 16, Top = 36, Width = 200, Height = 160 });
            for (var i = 1; i <= 12; i++)
                list.Items.Add ($"List item {i}");
            list.SelectedIndex = 2;

            page.Controls.Add (new Label { Text = "TreeView", Left = 232, Top = 12, Width = 200 });
            var tree = page.Controls.Add (new TreeView { Left = 232, Top = 36, Width = 220, Height = 160 });
            var root = tree.Items.Add ("Documents");
            root.Items.Add ("Invoices");
            root.Items.Add ("Reports");
            var drafts = root.Items.Add ("Drafts");
            drafts.Items.Add ("Q3 plan");
            root.Expanded = true;
            drafts.Expanded = true;
            tree.Items.Add ("Pictures");

            page.Controls.Add (new Label { Text = "ListView", Left = 468, Top = 12, Width = 200 });
            var view = page.Controls.Add (new ListView { Left = 468, Top = 36, Width = 300, Height = 160 });
            for (var i = 1; i <= 8; i++)
                view.Items.Add (new ListViewItem { Text = $"Item {i}" });

            page.Controls.Add (new Label { Text = "DataGridView", Left = 16, Top = 212, Width = 200 });
            var grid = page.Controls.Add (new DataGridView { Left = 16, Top = 236, Width = 752, Height = 230 });
            grid.Columns.Add ("Name", 180);
            grid.Columns.Add ("Role", 160);
            grid.Columns.Add ("City", 160);
            grid.Columns.Add ("Status", 120);
            grid.Rows.Add ("Alice Johnson", "Engineer", "Halifax", "Active");
            grid.Rows.Add ("Bob Smith", "Designer", "St. John's", "Active");
            grid.Rows.Add ("Carol Williams", "Teacher", "Moncton", "On leave");
            grid.Rows.Add ("David Brown", "Doctor", "Charlottetown", "Active");
            grid.Rows.Add ("Eve Davis", "Lawyer", "Fredericton", "Inactive");
            grid.Rows.Add ("Frank Miller", "Student", "Sydney", "Active");

            page.Controls.Add (new Label { Text = "MonthCalendar", Left = 16, Top = 482, Width = 200 });
            page.Controls.Add (new MonthCalendar { Left = 16, Top = 506 });

            page.Controls.Add (new Label { Text = "PictureBox", Left = 300, Top = 482, Width = 200 });
            var picture = page.Controls.Add (new PictureBox { Left = 300, Top = 506, Width = 160, Height = 100 });
            picture.Style.Border.Width = 1;
        }

        private static void BuildChrome (TabPage page)
        {
            // Fill first, then the strips from the inside out: docking resolves in reverse z-order,
            // so the menu -- added last -- docks first and ends up on top of the tool bar.
            var body = page.Controls.Add (new Panel { Dock = DockStyle.Fill });

            page.Controls.Add (new StatusBar { Text = "Ready — 3 items selected" });

            var toolbar = page.Controls.Add (new ToolBar ());
            toolbar.Items.Add (new MenuItem ("Back"));
            toolbar.Items.Add (new MenuItem ("Forward"));
            toolbar.Items.Add (new MenuSeparatorItem ());
            var search = toolbar.Items.Add (new MenuItem ("Search"));
            search.Items.Add ("Files");
            search.Items.Add ("Everywhere");
            toolbar.Items.Add (new MenuItem ("Disabled") { Enabled = false });

            var menu = page.Controls.Add (new Menu ());
            var file = menu.Items.Add ("File");
            file.Items.Add ("New");
            file.Items.Add ("Open…");
            file.Items.Add (new MenuSeparatorItem ());
            file.Items.Add ("Exit");
            var edit = menu.Items.Add ("Edit");
            edit.Items.Add ("Undo");
            edit.Items.Add ("Redo");
            menu.Items.Add ("View");
            menu.Items.Add ("Disabled").Enabled = false;

            body.Controls.Add (new Label { Text = "TabControl / TabStrip", Left = 16, Top = 12, Width = 300 });
            var tabs = body.Controls.Add (new TabControl { Left = 16, Top = 36, Width = 360, Height = 160 });
            tabs.TabPages.Add ("General").Controls.Add (new Label { Text = "A tab page is a Panel.", Left = 8, Top = 8, Width = 300 });
            tabs.TabPages.Add ("Advanced");
            tabs.TabPages.Add ("About");

            body.Controls.Add (new Label { Text = "SplitContainer", Left = 400, Top = 12, Width = 300 });
            var split = body.Controls.Add (new SplitContainer { Left = 400, Top = 36, Width = 360, Height = 160, SplitterDistance = 150 });
            split.Panel1.Controls.Add (new Label { Text = "Panel 1", Left = 8, Top = 8, Width = 120 });
            split.Panel2.Controls.Add (new Label { Text = "Panel 2", Left = 8, Top = 8, Width = 120 });

            body.Controls.Add (new Label { Text = "ScrollBars", Left = 16, Top = 212, Width = 300 });
            body.Controls.Add (new HorizontalScrollBar { Left = 16, Top = 236, Width = 360, Height = 16, Maximum = 100, Value = 30 });
            body.Controls.Add (new VerticalScrollBar { Left = 400, Top = 212, Width = 16, Height = 160, Maximum = 100, Value = 30 });

            body.Controls.Add (new Label { Text = "NavigationPane", Left = 16, Top = 270, Width = 300 });
            // A NavigationPane docks to the left by default (it is a side bar); undock it to sit in the grid.
            var nav = body.Controls.Add (new NavigationPane { Dock = DockStyle.None, Left = 16, Top = 294, Width = 360, Height = 200 });
            nav.Items.Add (new NavigationPaneItem (Glyph (), "Mail"));
            nav.Items.Add (new NavigationPaneItem (Glyph (), "Calendar"));
            nav.Items.Add (new NavigationPaneItem (Glyph (), "Contacts"));
        }

        // A neutral placeholder icon for the controls that require one; the sample ships no image files.
        private static SKBitmap Glyph ()
        {
            var bitmap = new SKBitmap (24, 24);
            using var canvas = new SKCanvas (bitmap);
            using var paint = new SKPaint { Color = new SKColor (0x90, 0x90, 0x90), IsAntialias = true };
            canvas.Clear (SKColors.Transparent);
            canvas.DrawCircle (12, 12, 9, paint);
            return bitmap;
        }
    }

    // Paints every token as a swatch with its name and current value, so the designer can see which
    // token drives which colour and copy the value that is there now.
    public class TokenSwatches : Panel
    {
        private const int RowHeight = 26;

        /// <summary>The logical height of the whole list: one row per token, a heading, and one row per part (two when it has a hover state).</summary>
        public static int PreferredHeight
            => 10 + (ThemeCssReference.Tokens.Count + 2 + ThemeCssReference.Parts.Sum (p => p.Part.SupportsHover ? 2 : 1)) * RowHeight + 10;

        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            var rowHeight = LogicalToDeviceUnits (RowHeight);
            var swatch = LogicalToDeviceUnits (44);
            var left = LogicalToDeviceUnits (12);
            var y = LogicalToDeviceUnits (10);
            var fontSize = LogicalToDeviceUnits (12);
            var font = Theme.UIFont;
            var foreground = GetEffectiveForeground ();
            var nameWidth = LogicalToDeviceUnits (280);
            var valueWidth = LogicalToDeviceUnits (210);
            var border = Theme.BorderLowColor;

            foreach (var token in ThemeCssReference.Tokens) {
                var value = Theme.FormatTokenValue (token);

                if (token.Kind == ThemeCssValueKind.Color) {
                    var color = (SKColor) typeof (Theme).GetProperty (token.PropertyName)!.GetValue (null)!;
                    e.Canvas.FillRectangle (left, y, swatch, rowHeight - LogicalToDeviceUnits (6), color);
                    e.Canvas.DrawRectangle (left, y, swatch, rowHeight - LogicalToDeviceUnits (6), border);
                }

                var textBounds = new Rectangle (left + swatch + LogicalToDeviceUnits (10), y, nameWidth, rowHeight - LogicalToDeviceUnits (6));
                e.Canvas.DrawText (token.Name, font, fontSize, textBounds, foreground, ContentAlignment.MiddleLeft);

                var valueBounds = new Rectangle (textBounds.Right, y, valueWidth, textBounds.Height);
                e.Canvas.DrawText (value, font, fontSize, valueBounds, foreground, ContentAlignment.MiddleLeft);

                var descriptionBounds = new Rectangle (valueBounds.Right, y, Math.Max (0, ScaledSize.Width - valueBounds.Right - left), textBounds.Height);
                e.Canvas.DrawText (token.Description, font, fontSize, descriptionBounds, Theme.ForegroundDisabledColor, ContentAlignment.MiddleLeft, maxLines: 1, ellipsis: true);

                y += rowHeight;
            }

            // Parts: the current background (left half) and text colour (right half) of each
            // `Selector::part`, so a designer sees which pseudo-element paints which piece.
            y += LogicalToDeviceUnits (8);
            e.Canvas.DrawText ("Parts (Selector::part) -- background | text", Theme.UIFontBold, fontSize,
                new Rectangle (left, y, LogicalToDeviceUnits (600), rowHeight), foreground, ContentAlignment.MiddleLeft);
            y += rowHeight;

            foreach (var (selector, part) in ThemeCssReference.Parts) {
                DrawPartRow (e, $"{selector.Name}::{part.Name}", part.Style, part.Description, left, y, swatch, rowHeight, nameWidth, valueWidth, fontSize, font, foreground, border);
                y += rowHeight;

                if (part.HoverStyle is { } hover) {
                    DrawPartRow (e, $"{selector.Name}::{part.Name}:hover", hover, "The same part when hovered.", left, y, swatch, rowHeight, nameWidth, valueWidth, fontSize, font, foreground, border);
                    y += rowHeight;
                }
            }
        }

        private void DrawPartRow (PaintEventArgs e, string name, ControlStyle style, string description, int left, int y, int swatch, int rowHeight, int nameWidth, int valueWidth, int fontSize, SKTypeface font, SKColor foreground, SKColor border)
        {
            var height = rowHeight - LogicalToDeviceUnits (6);
            var half = swatch / 2;

            // A part with no background of its own shows the strip/list behind it; draw that as a gap.
            if (style.BackgroundColor is { } background || (style.GetBackgroundColor () is { } inherited && (background = inherited) != default))
                e.Canvas.FillRectangle (left, y, half, height, background);
            e.Canvas.FillRectangle (left + half, y, swatch - half, height, style.GetForegroundColor ());
            e.Canvas.DrawRectangle (left, y, swatch, height, border);

            var textBounds = new Rectangle (left + swatch + LogicalToDeviceUnits (10), y, nameWidth, height);
            e.Canvas.DrawText (name, font, fontSize, textBounds, foreground, ContentAlignment.MiddleLeft);

            var valueBounds = new Rectangle (textBounds.Right, y, valueWidth, height);
            var value = (style.BackgroundColor is { } bg ? ThemeCssValueText (bg) : "(inherits)") + " | " + ThemeCssValueText (style.GetForegroundColor ());
            e.Canvas.DrawText (value, font, fontSize, valueBounds, foreground, ContentAlignment.MiddleLeft);

            var descriptionBounds = new Rectangle (valueBounds.Right, y, Math.Max (0, ScaledSize.Width - valueBounds.Right - left), height);
            e.Canvas.DrawText (description, font, fontSize, descriptionBounds, Theme.ForegroundDisabledColor, ContentAlignment.MiddleLeft, maxLines: 1, ellipsis: true);
        }

        private static string ThemeCssValueText (SKColor color)
            => color.Alpha == 255 ? $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}" : $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}{color.Alpha:x2}";

        private SKColor GetEffectiveForeground () => Style.ForegroundColor ?? Theme.ForegroundColor;
    }
}
