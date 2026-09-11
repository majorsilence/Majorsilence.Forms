using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Majorsilence.Forms.Theming.WinForms;
using Xunit;
using MF = Majorsilence.Forms;
using WF = System.Windows.Forms;

namespace Majorsilence.Forms.Theming.WinForms.Tests
{
    // Real System.Windows.Forms controls, a real stylesheet, assertions on the WinForms properties the
    // applier is documented to set (docs/theming-winforms.md). Nothing here is shown on screen:
    // properties are set on unshown forms, and the one pixel check uses Control.DrawToBitmap.
    public class WinFormsCssThemeTests : IDisposable
    {
        private const string Sheet = @"
@theme ""Graphite Test"" extends Dark;

:root {
  --accent-color: #f2a93b;
  --accent-color-2: #c77f12;
  --background-color: #1c1c1e;
  --control-low-color: #232326;
  --control-mid-color: #2c2c30;
  --control-highlight-low-color: #34343a;
  --border-low-color: #3a3a40;
  --foreground-color: #e8e8ea;
  --foreground-color-on-accent: #1a1200;
}

Button { background-color: #38383d; border: none; border-radius: 2px; }
Button:hover { background-color: var(--accent-color); color: var(--foreground-color-on-accent); }
TextBox, NumericUpDown { background-color: #141416; border: 1px solid var(--border-low-color); }
Menu, ToolBar, StatusBar { background-color: #141416; }
Menu::item:hover { background-color: var(--accent-color); color: var(--foreground-color-on-accent); }
MenuDropDown { background-color: #101012; border-color: #55555c; }
DataGridView::header { background-color: #141416; color: #9a9aa2; border-bottom-color: var(--accent-color-2); }
DataGridView::selection { background-color: #3a3220; border-color: var(--accent-color); }
DataGridView::alternating-row { background-color: #26262a; }
LinkLabel { color: #f2a93b; }
TabStrip::selected { color: var(--accent-color); border-bottom-color: var(--accent-color); border-bottom-width: 2px; }
NavigationPane { background-color: #000000; }
Form { font-family: ""Segoe UI"", sans-serif; font-size: 14px; }
";

        private readonly WF.Form form = new () { ClientSize = new Size (400, 300) };

        public void Dispose ()
        {
            GC.SuppressFinalize (this);
            form.Dispose ();
            MF.Theme.SetBuiltInTheme (MF.BuiltInTheme.Light);
        }

        private static Color Rgb (string hex)
            => Color.FromArgb (255, Convert.ToInt32 (hex.Substring (1, 2), 16), Convert.ToInt32 (hex.Substring (3, 2), 16), Convert.ToInt32 (hex.Substring (5, 2), 16));

        private static void AssertColor (string expectedHex, Color actual)
            => Assert.Equal (Rgb (expectedHex).ToArgb (), actual.ToArgb ());

        private T Add<T> (T control) where T : WF.Control
        {
            form.Controls.Add (control);
            return control;
        }

        private void TrackAndApply ()
        {
            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply (Sheet);
        }

        [Fact]
        public void Button_GetsFlatAppearanceFromRule ()
        {
            var button = Add (new WF.Button { Text = "OK", Size = new Size (100, 30) });

            TrackAndApply ();

            Assert.Equal (WF.FlatStyle.Flat, button.FlatStyle);
            AssertColor ("#38383d", button.BackColor);
            AssertColor ("#e8e8ea", button.ForeColor);
            Assert.Equal (0, button.FlatAppearance.BorderSize);
            AssertColor ("#f2a93b", button.FlatAppearance.MouseOverBackColor);
            Assert.NotNull (button.Region);
        }

        [Fact]
        public void Button_WithoutRule_UsesTokens ()
        {
            var button = Add (new WF.Button { Text = "OK" });
            WinFormsCssTheme.Track (form);

            WinFormsCssTheme.Apply (":root { --control-mid-color: #2c2c30; --border-low-color: #3a3a40; --accent-color: #f2a93b; }");

            AssertColor ("#2c2c30", button.BackColor);
            Assert.Equal (1, button.FlatAppearance.BorderSize);
            AssertColor ("#3a3a40", button.FlatAppearance.BorderColor);
            AssertColor ("#f2a93b", button.FlatAppearance.MouseOverBackColor);
            Assert.Null (button.Region);
        }

        [Fact]
        public void TextInputs_GetBackgroundAndBorderStyle ()
        {
            var textBox = Add (new WF.TextBox ());
            var numeric = Add (new WF.NumericUpDown ());
            var rich = Add (new WF.RichTextBox ());

            TrackAndApply ();

            AssertColor ("#141416", textBox.BackColor);
            AssertColor ("#141416", numeric.BackColor);
            AssertColor ("#141416", rich.BackColor);
            AssertColor ("#e8e8ea", textBox.ForeColor);
            Assert.Equal (WF.BorderStyle.FixedSingle, textBox.BorderStyle);
            Assert.Equal (WF.BorderStyle.FixedSingle, numeric.BorderStyle);
        }

        [Fact]
        public void TextBox_BorderNone_MapsToBorderStyleNone ()
        {
            var textBox = Add (new WF.TextBox ());
            WinFormsCssTheme.Track (form);

            WinFormsCssTheme.Apply ("TextBox { border: none; }");

            Assert.Equal (WF.BorderStyle.None, textBox.BorderStyle);
        }

        [Fact]
        public void DataGridView_GetsCellStylesFromPartsAndTokens ()
        {
            var grid = Add (new WF.DataGridView ());

            TrackAndApply ();

            AssertColor ("#232326", grid.BackgroundColor);
            AssertColor ("#232326", grid.DefaultCellStyle.BackColor);
            AssertColor ("#e8e8ea", grid.DefaultCellStyle.ForeColor);
            AssertColor ("#3a3a40", grid.GridColor);
            Assert.False (grid.EnableHeadersVisualStyles);

            AssertColor ("#141416", grid.ColumnHeadersDefaultCellStyle.BackColor);
            AssertColor ("#9a9aa2", grid.ColumnHeadersDefaultCellStyle.ForeColor);

            AssertColor ("#3a3220", grid.DefaultCellStyle.SelectionBackColor);
            AssertColor ("#1a1200", grid.DefaultCellStyle.SelectionForeColor);

            AssertColor ("#26262a", grid.AlternatingRowsDefaultCellStyle.BackColor);
        }

        [Fact]
        public void Form_GetsTokensAndAmbientFont ()
        {
            var label = Add (new WF.Label { Text = "hi" });

            TrackAndApply ();

            AssertColor ("#1c1c1e", form.BackColor);
            AssertColor ("#e8e8ea", form.ForeColor);
            Assert.Equal (10.5f, form.Font.SizeInPoints, 2);

            // The ambient WinForms inheritance carries the form's values to a label with no rule.
            AssertColor ("#1c1c1e", label.BackColor);
            AssertColor ("#e8e8ea", label.ForeColor);
            Assert.Equal (10.5f, label.Font.SizeInPoints, 2);
        }

        [Fact]
        public void ListsAndTrees_GetPaperBackground ()
        {
            var list = Add (new WF.ListBox ());
            var tree = Add (new WF.TreeView ());
            var listView = Add (new WF.ListView ());
            var checked_ = Add (new WF.CheckedListBox ());
            var combo = Add (new WF.ComboBox ());

            TrackAndApply ();

            AssertColor ("#232326", list.BackColor);
            AssertColor ("#232326", tree.BackColor);
            AssertColor ("#232326", listView.BackColor);
            AssertColor ("#232326", checked_.BackColor);
            AssertColor ("#e8e8ea", tree.LineColor);
            AssertColor ("#2c2c30", combo.BackColor);
            Assert.Equal (WF.FlatStyle.Flat, combo.FlatStyle);
        }

        [Fact]
        public void Selection_IsOwnerDrawnOnListsAndTrees_ButNotOnAppOwnedOnes ()
        {
            var list = Add (new WF.ListBox ());
            var checkedList = Add (new WF.CheckedListBox ());
            var appDrawn = Add (new WF.ListBox { DrawMode = WF.DrawMode.OwnerDrawVariable });
            var tree = Add (new WF.TreeView ());
            var appDrawnTree = Add (new WF.TreeView { DrawMode = WF.TreeViewDrawMode.OwnerDrawAll });
            var details = Add (new WF.ListView { View = WF.View.Details });
            var withChecks = Add (new WF.ListView { View = WF.View.Details, CheckBoxes = true });

            TrackAndApply ();

            Assert.Equal (WF.DrawMode.OwnerDrawFixed, list.DrawMode);
            Assert.Equal (WF.DrawMode.Normal, checkedList.DrawMode);
            Assert.Equal (WF.DrawMode.OwnerDrawVariable, appDrawn.DrawMode);
            Assert.Equal (WF.TreeViewDrawMode.OwnerDrawText, tree.DrawMode);
            Assert.Equal (WF.TreeViewDrawMode.OwnerDrawAll, appDrawnTree.DrawMode);
            Assert.True (details.OwnerDraw);
            Assert.False (withChecks.OwnerDraw);
        }

        [Fact]
        public void DrawToBitmap_SelectedListBoxItem_PaintsTheSelectionColour ()
        {
            var list = Add (new WF.ListBox { Size = new Size (120, 80), Location = new Point (10, 10), IntegralHeight = false });
            list.Items.AddRange (new object[] { "one", "two", "three" });
            list.SelectedIndex = 1;
            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply (":root { --control-low-color: #232326; } ListBox::selection { background-color: #3a3220; }");

            using var bitmap = new Bitmap (list.Width, list.Height);
            list.DrawToBitmap (bitmap, new Rectangle (0, 0, list.Width, list.Height));

            var itemHeight = list.GetItemHeight (0);
            AssertColor ("#3a3220", bitmap.GetPixel (list.Width - 10, itemHeight + itemHeight / 2));   // row 2: selected
            AssertColor ("#232326", bitmap.GetPixel (list.Width - 10, itemHeight / 2));               // row 1: not
        }

        [Fact]
        public void DrawToBitmap_ProgressBar_PaintsTheAccent2Fill ()
        {
            var bar = Add (new WF.ProgressBar { Size = new Size (200, 24), Location = new Point (10, 10), Minimum = 0, Maximum = 100, Value = 100 });
            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply (":root { --accent-color-2: #c77f12; --control-mid-high-color: #38383d; }");

            using var bitmap = new Bitmap (bar.Width, bar.Height);
            bar.DrawToBitmap (bitmap, new Rectangle (0, 0, bar.Width, bar.Height));

            AssertColor ("#c77f12", bitmap.GetPixel (bar.Width / 2, bar.Height / 2));
            Assert.Equal (WF.ProgressBarStyle.Continuous, bar.Style);
        }

        [Fact]
        public void LinkLabel_ColorIsTheLinkColor ()
        {
            var link = Add (new WF.LinkLabel { Text = "link" });

            TrackAndApply ();

            AssertColor ("#f2a93b", link.LinkColor);
            AssertColor ("#f2a93b", link.VisitedLinkColor);
        }

        [Fact]
        public void TabControl_WithTabStripRule_IsOwnerDrawn ()
        {
            var tabs = Add (new WF.TabControl ());
            tabs.TabPages.Add ("One");

            TrackAndApply ();

            Assert.Equal (WF.TabDrawMode.OwnerDrawFixed, tabs.DrawMode);
        }

        [Fact]
        public void Strips_GetTheRendererAndColors ()
        {
            var menu = Add (new WF.MenuStrip ());
            var tools = Add (new WF.ToolStrip ());
            var status = Add (new WF.StatusStrip ());

            TrackAndApply ();

            var renderer = Assert.IsAssignableFrom<WF.ToolStripProfessionalRenderer> (WF.ToolStripManager.Renderer);
            AssertColor ("#141416", renderer.ColorTable.MenuStripGradientBegin);
            AssertColor ("#141416", renderer.ColorTable.ToolStripGradientMiddle);
            AssertColor ("#141416", renderer.ColorTable.StatusStripGradientEnd);
            AssertColor ("#f2a93b", renderer.ColorTable.MenuItemSelected);
            AssertColor ("#101012", renderer.ColorTable.ToolStripDropDownBackground);
            AssertColor ("#55555c", renderer.ColorTable.MenuBorder);

            AssertColor ("#141416", menu.BackColor);
            AssertColor ("#141416", tools.BackColor);
            AssertColor ("#141416", status.BackColor);
            AssertColor ("#e8e8ea", menu.ForeColor);
        }

        [Fact]
        public void ContextMenu_AttachedToAControl_IsStyled ()
        {
            var label = Add (new WF.Label { Text = "x" });
            using var context = new WF.ContextMenuStrip ();
            label.ContextMenuStrip = context;

            TrackAndApply ();

            AssertColor ("#101012", context.BackColor);
        }

        [Fact]
        public void Track_StylesControlsAddedLater ()
        {
            TrackAndApply ();

            var panel = Add (new WF.Panel ());
            var late = new WF.Button { Text = "late" };
            panel.Controls.Add (late);

            var lateText = new WF.TextBox ();
            panel.Controls.Add (lateText);

            Assert.Equal (WF.FlatStyle.Flat, late.FlatStyle);
            AssertColor ("#38383d", late.BackColor);
            AssertColor ("#141416", lateText.BackColor);
        }

        [Fact]
        public void ExplicitAppColor_IsKept ()
        {
            var pinned = Add (new WF.Button { Text = "pinned", BackColor = Color.Red });
            var themed = Add (new WF.Button { Text = "themed" });

            TrackAndApply ();

            Assert.Equal (Color.Red.ToArgb (), pinned.BackColor.ToArgb ());
            AssertColor ("#e8e8ea", pinned.ForeColor);
            AssertColor ("#38383d", themed.BackColor);
        }

        [Fact]
        public void AppChangeAfterApply_IsKeptOnReapply ()
        {
            var button = Add (new WF.Button { Text = "b" });

            TrackAndApply ();
            AssertColor ("#38383d", button.BackColor);

            button.BackColor = Color.Blue;
            WinFormsCssTheme.Apply (Sheet.Replace ("#38383d", "#404040"));

            Assert.Equal (Color.Blue.ToArgb (), button.BackColor.ToArgb ());
        }

        [Fact]
        public void InheritedValueThatMatchedOnce_IsNotPinnedWhenTheParentMoves ()
        {
            // Pass 1: the strip's rule value equals the form's ambient background, so nothing is set
            // on the strip. Pass 2 moves the form background but not the rule: the strip must get the
            // rule value rather than be mistaken for an app-owned property.
            var status = Add (new WF.StatusStrip ());
            WinFormsCssTheme.Track (form);

            WinFormsCssTheme.Apply (":root { --background-color: #141416; } StatusBar { background-color: #141416; }");
            AssertColor ("#141416", status.BackColor);

            WinFormsCssTheme.Apply (":root { --background-color: #1c1c1e; } StatusBar { background-color: #141416; }");

            AssertColor ("#1c1c1e", form.BackColor);
            AssertColor ("#141416", status.BackColor);
        }

        [Fact]
        public void Reapply_MovesValuesTheApplierOwns ()
        {
            var button = Add (new WF.Button { Text = "b" });

            TrackAndApply ();
            WinFormsCssTheme.Apply (Sheet.Replace ("#38383d", "#404040"));

            AssertColor ("#404040", button.BackColor);
        }

        [Fact]
        public void Diagnostics_ReportUnmappedSelectorsAndUnsupportedProperties ()
        {
            TrackAndApply ();

            var diagnostics = WinFormsCssTheme.Diagnostics;

            var navigation = Assert.Single (diagnostics, d => d.Message.Contains ("'NavigationPane'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Info, navigation.Severity);

            var hoverText = Assert.Single (diagnostics, d => d.Message.StartsWith ("'Button:hover { color }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, hoverText.Severity);
            Assert.Contains ("not supported on WinForms", hoverText.Message, StringComparison.Ordinal);

            var radius = Assert.Single (diagnostics, d => d.Message.StartsWith ("'Button { border-radius }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Info, radius.Severity);
            Assert.Contains ("approximately", radius.Message, StringComparison.Ordinal);

            var headerLine = Assert.Single (diagnostics, d => d.Message.StartsWith ("'DataGridView::header { border-bottom-color }'", StringComparison.Ordinal));
            Assert.Equal (MF.ThemeCssSeverity.Warning, headerLine.Severity);

            // Native declarations are not reported.
            Assert.DoesNotContain (diagnostics, d => d.Message.StartsWith ("'Button { background-color }'", StringComparison.Ordinal));
            Assert.DoesNotContain (diagnostics, d => d.Message.StartsWith ("'Menu::item:hover", StringComparison.Ordinal));

            // Every diagnostic points at its source line.
            Assert.All (diagnostics.Where (d => !d.Message.StartsWith ("Application.", StringComparison.Ordinal)), d => Assert.True (d.Line > 0));
        }

        [Fact]
        public void Apply_ThrowsOnParseErrors_AndAppliesNothing ()
        {
            var button = Add (new WF.Button ());
            WinFormsCssTheme.Track (form);
            var before = button.FlatStyle;

            var ex = Assert.Throws<MF.ThemeCssException> (() => WinFormsCssTheme.Apply ("Button { colr: red; }"));

            Assert.Contains (ex.Diagnostics, d => d.Severity == MF.ThemeCssSeverity.Error);
            Assert.Equal (before, button.FlatStyle);
        }

        [Fact]
        public void Apply_AlsoAppliesToTheMajorsilenceTheme ()
        {
            TrackAndApply ();

            Assert.Equal (new SkiaSharp.SKColor (0xf2, 0xa9, 0x3b), MF.Theme.AccentColor);
            Assert.Single (MF.Theme.CurrentStyleSheets);
        }

        [Fact]
        public void SheetAppliedFromTheMajorsilenceSide_IsMirroredOntoWinForms ()
        {
            var button = Add (new WF.Button ());
            TrackAndApply ();
            AssertColor ("#38383d", button.BackColor);

            MF.Theme.LoadFromCss ("Button { background-color: #112233; }");

            AssertColor ("#112233", button.BackColor);
        }

        [Fact]
        public void DrawToBitmap_ButtonPaintsTheThemedBackground ()
        {
            var button = Add (new WF.Button { Text = "", Size = new Size (120, 40), Location = new Point (10, 10) });
            WinFormsCssTheme.Track (form);
            WinFormsCssTheme.Apply ("Button { background-color: #38383d; border: none; }");

            using var bitmap = new Bitmap (button.Width, button.Height);
            button.DrawToBitmap (bitmap, new Rectangle (0, 0, button.Width, button.Height));

            AssertColor ("#38383d", bitmap.GetPixel (button.Width / 2, button.Height / 2));
        }

        [Fact]
        public void Watch_ReappliesWhenTheFileChanges ()
        {
            var path = Path.Combine (Path.GetTempPath (), "mf-theme-" + Guid.NewGuid ().ToString ("N") + ".css");
            File.WriteAllText (path, ":root { --accent-color: #111111; }");

            try {
                using var watcher = WinFormsCssTheme.Watch (path);
                Assert.Equal (1, watcher.LoadCount);
                Assert.Equal (new SkiaSharp.SKColor (0x11, 0x11, 0x11), MF.Theme.AccentColor);

                File.WriteAllText (path, ":root { --accent-color: #222222; }");

                var deadline = DateTime.UtcNow.AddSeconds (10);
                while (watcher.LoadCount < 2 && DateTime.UtcNow < deadline)
                    Thread.Sleep (50);

                Assert.True (watcher.LoadCount >= 2, "the watcher did not reload after the file changed");
                Assert.Equal (new SkiaSharp.SKColor (0x22, 0x22, 0x22), MF.Theme.AccentColor);
                Assert.DoesNotContain (watcher.SheetDiagnostics, d => d.Severity == MF.ThemeCssSeverity.Error);
            } finally {
                File.Delete (path);
            }
        }

        [Fact]
        public void Watch_KeepsGoingPastParseErrors ()
        {
            var path = Path.Combine (Path.GetTempPath (), "mf-theme-" + Guid.NewGuid ().ToString ("N") + ".css");
            File.WriteAllText (path, ":root { --accent-color: #333333; } Button { colr: red; }");

            try {
                using var watcher = WinFormsCssTheme.Watch (path);

                Assert.Contains (watcher.SheetDiagnostics, d => d.Severity == MF.ThemeCssSeverity.Error);
                Assert.Equal (new SkiaSharp.SKColor (0x33, 0x33, 0x33), MF.Theme.AccentColor);
            } finally {
                File.Delete (path);
            }
        }
    }
}
