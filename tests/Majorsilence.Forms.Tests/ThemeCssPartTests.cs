using System;
using System.Linq;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Renderers;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Pseudo-elements: `Type::part { ... }` styles a piece inside a control (grid headers, the selected
    // tab, a hovered menu item, the scroll bar thumb) through a named type-level ControlStyle the
    // renderer reads with the theme tokens as defaults. Same reset semantics as control rules; each
    // part accepts only the properties its renderer honours, and says so.
    public class ThemeCssPartTests : IDisposable
    {
        public ThemeCssPartTests () => Theme.SetBuiltInTheme (BuiltInTheme.Light);

        public void Dispose ()
        {
            GC.SuppressFinalize (this);

            foreach (var name in Theme.RegisteredThemes.ToList ())
                Theme.UnregisterTheme (name);

            Theme.SetBuiltInTheme (BuiltInTheme.Light);
        }

        private static ThemeStyleSheet ParseClean (string css)
        {
            var sheet = ThemeStyleSheet.Parse (css);
            Assert.False (sheet.HasErrors, string.Join ("\n", sheet.Diagnostics));
            return sheet;
        }

        private static string ErrorsOf (string css)
            => string.Join ("\n", ThemeStyleSheet.Parse (css).Diagnostics.Where (d => d.Severity == ThemeCssSeverity.Error).Select (d => d.Message));

        // ---- grammar and model --------------------------------------------------------------------

        [Fact]
        public void PartRule_ParsesIntoTheModel ()
        {
            var sheet = ParseClean ("DataGridView::header { background-color: #010203; font-weight: bold; } Menu::item:hover { background-color: red; }");

            Assert.Equal (2, sheet.Rules.Count);

            var header = sheet.Rules[0];
            Assert.Equal ("DataGridView", header.Selector.Name);
            Assert.Equal ("header", header.Part?.Name);
            Assert.False (header.Hover);
            Assert.Equal (new[] { "background-color", "font-weight" }, header.Declarations.Select (d => d.Property));
            Assert.StartsWith ("DataGridView::header { background-color: #010203;", header.ToString ());

            var item = sheet.Rules[1];
            Assert.Equal ("item", item.Part?.Name);
            Assert.True (item.Hover);
            Assert.StartsWith ("Menu::item:hover {", item.ToString ());
        }

        [Fact]
        public void PartNames_AreCaseInsensitive_AndWorkInCommaLists ()
        {
            var sheet = ParseClean ("datagridview::HEADER, listbox::Selection, Button { color: red; }");

            Assert.Equal (3, sheet.Rules.Count);
            Assert.Equal ("header", sheet.Rules[0].Part?.Name);
            Assert.Equal ("selection", sheet.Rules[1].Part?.Name);
            Assert.Null (sheet.Rules[2].Part);
        }

        [Fact]
        public void CommaList_MixingPartsAndControls_EachGetsWhatItAccepts ()
        {
            // The part takes only what its renderer reads; the control takes everything. The dropped
            // declaration is reported for the part, not silently skipped.
            var sheet = ThemeStyleSheet.Parse ("Menu::item, Button { color: red; font-size: 13px; }");

            Assert.True (sheet.HasErrors);
            Assert.Contains ("'font-size' does not apply to Menu::item; it accepts: background-color, color.",
                string.Join ("\n", sheet.Diagnostics.Select (d => d.Message)));

            Assert.Equal (2, sheet.Rules.Count);
            Assert.Equal (new[] { "color" }, sheet.Rules[0].Declarations.Select (d => d.Property));
            Assert.Equal (new[] { "color", "font-size" }, sheet.Rules[1].Declarations.Select (d => d.Property));
        }

        [Fact]
        public void BorderShorthand_OnAPart_ExpandsAndValidatesTheLonghands ()
        {
            var thumb = ParseClean ("ScrollBar::thumb { border: 2px solid red; border-radius: 4px; }");
            Assert.Equal (new[] { "border-width", "border-color", "border-radius" }, Assert.Single (thumb.Rules).Declarations.Select (d => d.Property));

            // ::arrow accepts border-color but not border-width, so the shorthand's width is dropped for it.
            var arrow = ThemeStyleSheet.Parse ("ScrollBar::arrow { border: 2px solid red; }");
            Assert.Contains ("'border-width' does not apply to ScrollBar::arrow", ErrorsOf ("ScrollBar::arrow { border: 2px solid red; }"));
            Assert.Equal (new[] { "border-color" }, Assert.Single (arrow.Rules).Declarations.Select (d => d.Property));
        }

        [Theory]
        [InlineData ("Button::icon { color: red; }", "Button has no separately styleable parts")]
        [InlineData ("Button::icon { color: red; }", "Controls with parts: DataGridView, ListBox, ListView, Menu, MenuDropDown, ScrollBar, TabStrip, ToolBar, TreeView")]
        [InlineData ("DataGridView::heading { color: red; }", "'DataGridView::heading' is not a part of DataGridView. Did you mean 'header'?")]
        [InlineData ("DataGridView::heading { color: red; }", "DataGridView parts: ::header, ::row-header, ::selection, ::alternating-row")]
        [InlineData ("DataGridView::header:hover { color: red; }", "'DataGridView::header:hover' is not supported: the header does not change when hovered")]
        [InlineData ("DataGridView::header:hover { color: red; }", "Parts with ':hover': Menu::item, MenuDropDown::item, TabStrip::item, ToolBar::item")]
        [InlineData ("ScrollBar::thumb { font-size: 12px; }", "'font-size' does not apply to ScrollBar::thumb; it accepts: background-color, border-color, border-width, border-radius")]
        [InlineData ("DataGridView::header::text { color: red; }", "parts do not nest")]
        [InlineData ("Menu:hover::item { color: red; }", "put the part before the pseudo-class -- 'Menu::item:hover'")]
        [InlineData ("DataGridView:: { color: red; }", "expected a part name after '::'")]
        [InlineData ("Menu::item:active { color: red; }", "':active' is not supported")]
        public void Diagnostics_ExplainPartMistakes (string css, string expectedFragment)
        {
            Assert.Contains (expectedFragment, ErrorsOf (css));
        }

        [Fact]
        public void EveryPartIsListedInTheReference ()
        {
            var parts = ThemeCssReference.Parts.ToList ();
            Assert.True (parts.Count >= 14, $"expected the documented parts, found {parts.Count}");

            var seen = new System.Collections.Generic.HashSet<ControlStyle> ();

            foreach (var selector in ThemeCssReference.Selectors) {
                seen.Add (selector.Style);
                if (selector.HoverStyle is { } hover)
                    seen.Add (hover);
            }

            foreach (var (selector, part) in parts) {
                // Each part is its own layer -- never the control's style, never another part's.
                Assert.True (seen.Add (part.Style), $"{selector.Name}::{part.Name} shares a style with something else");
                if (part.SupportsHover)
                    Assert.True (seen.Add (part.HoverStyle!), $"{selector.Name}::{part.Name}:hover shares a style with something else");

                Assert.NotEmpty (part.Properties);
                Assert.All (part.Properties, p => Assert.Contains (p, ThemeCssReference.PropertyNames));
                Assert.Same (part, selector.FindPart (part.Name.ToUpperInvariant ()));
            }

            Assert.Contains ("### Parts (`Selector::part` pseudo-elements)", ThemeCssReference.ToMarkdown ());
            Assert.Contains ("| `DataGridView::header` |", ThemeCssReference.ToMarkdown ());
        }

        // ---- application ----------------------------------------------------------------------------

        [Fact]
        public void PartRule_SetsThePartStyle_AndIsReplacedByTheNextSheet ()
        {
            Theme.LoadFromCss ("DataGridView::header { background-color: #010203; color: #040506; font-size: 19px; }");

            Assert.Equal (new SKColor (1, 2, 3), DataGridView.DefaultColumnHeaderStyle.BackgroundColor);
            Assert.Equal (new SKColor (4, 5, 6), DataGridView.DefaultColumnHeaderStyle.ForegroundColor);
            Assert.Equal (19, DataGridView.DefaultColumnHeaderStyle.FontSize);
            // The control's own style is untouched.
            Assert.Equal (Theme.ControlLowColor, DataGridView.DefaultStyle.BackgroundColor);

            Theme.LoadFromCss ("Button { color: red; }");

            // Back to the token-derived defaults, not left behind.
            Assert.Equal (Theme.ControlMidColor, DataGridView.DefaultColumnHeaderStyle.BackgroundColor);
            Assert.Equal (Theme.ItemFontSize, DataGridView.DefaultColumnHeaderStyle.FontSize);
        }

        [Fact]
        public void PartRule_SurvivesTokenChanges_AndIsClearedByABuiltIn ()
        {
            Theme.LoadFromCss ("Menu::item:hover { background-color: #0a0b0c; } TabStrip::selected { border-bottom-color: #111213; border-bottom-width: 5px; }");

            Theme.AccentColor2 = new SKColor (9, 9, 9);   // re-runs every part's defaults; the rule must still win

            Assert.Equal (new SKColor (0x0a, 0x0b, 0x0c), Menu.DefaultItemHoverStyle.BackgroundColor);
            Assert.Equal (new SKColor (0x11, 0x12, 0x13), TabStrip.DefaultSelectedItemStyle.Border.Bottom.Color);
            Assert.Equal (5, TabStrip.DefaultSelectedItemStyle.Border.Bottom.Width);

            Theme.SetBuiltInTheme (BuiltInTheme.Dark);

            Assert.Equal (Theme.ControlHighlightLowColor, Menu.DefaultItemHoverStyle.BackgroundColor);
            Assert.Equal (Theme.AccentColor2, TabStrip.DefaultSelectedItemStyle.Border.Bottom.Color);
            Assert.Equal (3, TabStrip.DefaultSelectedItemStyle.Border.Bottom.Width);
        }

        [Fact]
        public void PartHover_LayersOnThePart ()
        {
            // A colour on ::item carries into ::item:hover unless the hover rule overrides it.
            Theme.LoadFromCss ("Menu::item { color: #123456; } Menu::item:hover { background-color: black; }");

            Assert.Equal (new SKColor (0x12, 0x34, 0x56), Menu.DefaultItemHoverStyle.GetForegroundColor ());
            Assert.Equal (SKColors.Black, Menu.DefaultItemHoverStyle.BackgroundColor);
        }

        [Fact]
        public void TokensStillDriveParts_WhenNoRuleTargetsThem ()
        {
            Theme.LoadFromCss (":root { --control-highlight-low-color: #0f0e0d; --accent-color-2: #0c0b0a; }");

            Assert.Equal (new SKColor (0x0f, 0x0e, 0x0d), ListBox.DefaultSelectionStyle.GetBackgroundColor ());
            Assert.Equal (new SKColor (0x0f, 0x0e, 0x0d), TreeView.DefaultSelectionStyle.GetBackgroundColor ());
            Assert.Equal (new SKColor (0x0f, 0x0e, 0x0d), DataGridView.DefaultSelectionStyle.GetBackgroundColor ());
            Assert.Equal (new SKColor (0x0c, 0x0b, 0x0a), TabStrip.DefaultSelectedItemStyle.Border.Bottom.GetColor ());
        }

        // ---- pixels ------------------------------------------------------------------------------------

        private static SKBitmap RenderBackBuffer (Control control, Form form, Action<Form>? beforeSecondPass = null)
        {
            form.Show ();
            HeadlessRenderer.CapturePng (form);
            beforeSecondPass?.Invoke (form);
            HeadlessRenderer.CapturePng (form);

            var buffer = typeof (Control).GetMethod ("GetBackBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return (SKBitmap) buffer.Invoke (control, null)!;
        }

        private static int CountPixels (SKBitmap bitmap, SKColor color)
        {
            var hits = 0;
            for (var x = 0; x < bitmap.Width; x++)
                for (var y = 0; y < bitmap.Height; y++)
                    if (bitmap.GetPixel (x, y) == color)
                        hits++;
            return hits;
        }

        private static readonly SKColor Marker = new (0xfe, 0x01, 0x7f);   // a colour no theme uses

        [Fact]
        public void GridHeader_IsPaintedWithThePartColour ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("DataGridView::header { background-color: #fe017f; }");

            using var form = new Form { Size = new System.Drawing.Size (500, 300) };
            var grid = new DataGridView { Left = 0, Top = 0, Width = 480, Height = 260 };
            grid.Columns.Add ("Name", 150);
            grid.Columns.Add ("City", 150);
            grid.Rows.Add ("Alice", "Halifax");
            form.Controls.Add (grid);

            var bitmap = RenderBackBuffer (grid, form);

            Assert.True (CountPixels (bitmap, Marker) > 480 * 10, "the header band should be painted with the part colour");
        }

        [Fact]
        public void ListBoxSelection_IsPaintedWithThePartColour ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("ListBox::selection { background-color: #fe017f; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var list = new ListBox { Left = 0, Top = 0, Width = 200, Height = 200 };
            list.Items.Add ("one");
            list.Items.Add ("two");
            list.SelectedIndex = 0;
            form.Controls.Add (list);

            var bitmap = RenderBackBuffer (list, form);

            Assert.True (CountPixels (bitmap, Marker) > 150 * 10, "the selected item should be painted with the part colour");
        }

        [Fact]
        public void TreeViewSelection_IsPaintedWithThePartColour ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("TreeView::selection { background-color: #fe017f; }");

            using var form = new Form { Size = new System.Drawing.Size (300, 300) };
            var tree = new TreeView { Left = 0, Top = 0, Width = 200, Height = 200 };
            var node = tree.Items.Add ("Documents");
            tree.Items.Add ("Pictures");
            tree.SelectedNode = node;
            form.Controls.Add (tree);

            var bitmap = RenderBackBuffer (tree, form);

            Assert.True (CountPixels (bitmap, Marker) > 150 * 10, "the selected node should be painted with the part colour");
        }

        [Fact]
        public void ScrollBarThumb_IsPaintedWithThePartColour ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("ScrollBar::thumb { background-color: #fe017f; border-radius: 4px; }");

            using var form = new Form { Size = new System.Drawing.Size (200, 300) };
            var bar = new VerticalScrollBar { Left = 10, Top = 10, Width = 16, Height = 200, Maximum = 100, Value = 20 };
            form.Controls.Add (bar);

            var bitmap = RenderBackBuffer (bar, form);

            Assert.True (CountPixels (bitmap, Marker) > 16 * 8, "the thumb should be painted with the part colour");
        }

        [Fact]
        public void TabStripSelectedUnderline_UsesThePartColour ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("TabStrip::selected { border-bottom-color: #fe017f; border-bottom-width: 4px; }");

            using var form = new Form { Size = new System.Drawing.Size (400, 200) };
            var strip = new TabStrip { Left = 0, Top = 0, Width = 380, Height = 40 };
            strip.Tabs.Add ("General");
            strip.Tabs.Add ("Advanced");
            strip.SelectedIndex = 0;
            form.Controls.Add (strip);

            var bitmap = RenderBackBuffer (strip, form);

            Assert.True (CountPixels (bitmap, Marker) > 20 * 2, "the selected tab's underline should use the part colour");
        }

        [Fact]
        public void MenuItemHover_IsPaintedWithThePartColour ()
        {
            HeadlessRenderer.Use ();
            Theme.LoadFromCss ("Menu::item:hover { background-color: #fe017f; }");

            using var form = new Form { Size = new System.Drawing.Size (400, 200) };
            var menu = new Menu ();
            var file = menu.Items.Add ("File");
            menu.Items.Add ("Edit");
            form.Controls.Add (menu);

            // Put the item into its hovered state directly (the setter is internal) rather than steering
            // the pointer: where the menu sits in the window differs between the custom-chrome and native
            // title-bar paths and with render scaling, and the pointer-to-hover mapping is not what this
            // test is about -- what the renderer paints for a hovered item is.
            var bitmap = RenderBackBuffer (menu, form, _ => {
                typeof (MenuItem).GetProperty (nameof (MenuItem.Hovered))!.GetSetMethod (nonPublic: true)!.Invoke (file, new object[] { true });
                menu.Invalidate ();
            });

            Assert.True (file.Hovered);
            Assert.True (CountPixels (bitmap, Marker) > 20 * 10, "the hovered item should be painted with the part colour");
        }
    }
}
