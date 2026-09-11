using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>
    /// How faithfully <see cref="WinFormsCssTheme"/> can express one (selector, property) pair on real
    /// System.Windows.Forms controls.
    /// </summary>
    public enum WinFormsThemeSupportLevel
    {
        /// <summary>The property maps to a real WinForms property or renderer seam and applies exactly.</summary>
        Native,

        /// <summary>The property applies, but not pixel-for-pixel (e.g. border-radius via a Region, without anti-aliasing).</summary>
        Approximate,

        /// <summary>WinForms has no way to express it; the declaration is reported as a diagnostic and skipped.</summary>
        Unsupported
    }

    /// <summary>
    /// One row of the support matrix: what happens to <c>Selector[::Part][:hover] { Property }</c>
    /// when applied to real WinForms controls.
    /// </summary>
    public sealed class WinFormsThemeSupportEntry
    {
        internal WinFormsThemeSupportEntry (string selector, string? part, bool hover, string property, WinFormsThemeSupportLevel level, string notes)
        {
            Selector = selector;
            Part = part;
            Hover = hover;
            Property = property;
            Level = level;
            Notes = notes;
        }

        /// <summary>The CSS selector (the Majorsilence.Forms control type name).</summary>
        public string Selector { get; }

        /// <summary>The <c>::part</c> pseudo-element, or null for the control itself.</summary>
        public string? Part { get; }

        /// <summary>Whether the row describes the <c>:hover</c> variant.</summary>
        public bool Hover { get; }

        /// <summary>The longhand property name.</summary>
        public string Property { get; }

        /// <summary>How faithfully the pair applies.</summary>
        public WinFormsThemeSupportLevel Level { get; }

        /// <summary>What it maps to, or why it cannot.</summary>
        public string Notes { get; }

        /// <inheritdoc/>
        public override string ToString ()
            => $"{Selector}{(Part is null ? "" : "::" + Part)}{(Hover ? ":hover" : "")} {{ {Property} }} -> {Level}";
    }

    /// <summary>
    /// Where a CSS selector lands in System.Windows.Forms, or why it cannot.
    /// </summary>
    public sealed class WinFormsThemeSelectorMapping
    {
        internal WinFormsThemeSelectorMapping (string selector, string? winFormsTypes, string notes)
        {
            Selector = selector;
            WinFormsTypes = winFormsTypes;
            Notes = notes;
        }

        /// <summary>The CSS selector (the Majorsilence.Forms control type name).</summary>
        public string Selector { get; }

        /// <summary>The System.Windows.Forms type(s) the rule applies to, or null when WinForms has no counterpart.</summary>
        public string? WinFormsTypes { get; }

        /// <summary>How the mapping works, or why there is none.</summary>
        public string Notes { get; }

        /// <inheritdoc/>
        public override string ToString () => $"{Selector} -> {WinFormsTypes ?? "(no WinForms counterpart)"}";
    }

    /// <summary>
    /// The property × control support matrix of <see cref="WinFormsCssTheme"/>: for every selector the
    /// stylesheet grammar accepts, where it lands in System.Windows.Forms and how faithfully each
    /// property applies. <c>docs/theming-winforms.md</c> is generated from it (a test keeps the two in
    /// sync), and the applier surfaces the Approximate/Unsupported rows as diagnostics — never a silent
    /// no-op, which is the rule the whole CSS subset is built on.
    /// </summary>
    public static class WinFormsThemeSupport
    {
        private static readonly string[] font_properties = { "font-family", "font-size", "font-weight", "font-style" };

        private static readonly string[] side_properties = {
            "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
            "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        };

        // Every longhand a control rule can carry ('border' expands to border-width/border-color
        // during parsing, so it never reaches an applier).
        private static readonly string[] all_properties =
            new[] { "background-color", "color", "border-width", "border-color", "border-radius" }
                .Concat (side_properties)
                .Concat (font_properties)
                .ToArray ();

        /// <summary>Every selector, with the System.Windows.Forms type(s) it applies to (null = no counterpart).</summary>
        public static IReadOnlyList<WinFormsThemeSelectorMapping> Mappings { get; }

        /// <summary>Every (selector, part, hover, property) row.</summary>
        public static IReadOnlyList<WinFormsThemeSupportEntry> Entries { get; }

        private static readonly Dictionary<(string Selector, string? Part, bool Hover, string Property), WinFormsThemeSupportEntry> index;
        private static readonly Dictionary<string, WinFormsThemeSelectorMapping> mapping_index;

        /// <summary>
        /// Looks up the row for one declaration. Returns null when the selector has no WinForms
        /// counterpart at all (see <see cref="FindMapping"/>).
        /// </summary>
        public static WinFormsThemeSupportEntry? Find (string selector, string? part, bool hover, string property)
        {
            index.TryGetValue ((selector.ToLowerInvariant (), part?.ToLowerInvariant (), hover, property.ToLowerInvariant ()), out var entry);
            return entry;
        }

        /// <summary>Looks up where a selector lands in WinForms, or null for a selector the grammar does not know.</summary>
        public static WinFormsThemeSelectorMapping? FindMapping (string selector)
        {
            mapping_index.TryGetValue (selector.ToLowerInvariant (), out var mapping);
            return mapping;
        }

        static WinFormsThemeSupport ()
        {
            var mappings = new List<WinFormsThemeSelectorMapping> ();
            var entries = new List<WinFormsThemeSupportEntry> ();

            // Local helpers so the table below stays declarative. Add() records specific rows;
            // Fill() completes the remaining longhands of a (selector, part, hover) at one level so
            // every declaration the grammar accepts has an explicit answer.
            void Map (string selector, string? types, string notes) => mappings.Add (new WinFormsThemeSelectorMapping (selector, types, notes));

            void Add (string selector, string? part, bool hover, string[] properties, WinFormsThemeSupportLevel level, string notes)
            {
                foreach (var property in properties)
                    entries.Add (new WinFormsThemeSupportEntry (selector, part, hover, property, level, notes));
            }

            void Fill (string selector, string? part, bool hover, WinFormsThemeSupportLevel level, string notes)
            {
                var present = entries.Where (e => e.Selector == selector && e.Part == part && e.Hover == hover).Select (e => e.Property).ToHashSet ();
                foreach (var property in all_properties)
                    if (!present.Contains (property))
                        entries.Add (new WinFormsThemeSupportEntry (selector, part, hover, property, level, notes));
            }

            string[] P (params string[] names) => names;

            const string uniformSides = "WinForms control borders are uniform; use border-width / border-color.";
            const string noBorderSeam = "WinForms draws this control's border natively and exposes no colour for it (BorderStyle only).";

            // ---- Form -------------------------------------------------------------------------
            Map ("Form", "Form", "BackColor / ForeColor / Font (the WinForms ambient defaults every child inherits); the Windows 11 title bar and frame via DwmSetWindowAttribute.");
            Add ("Form", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "Form.BackColor; also the Windows 11 title-bar colour (DWMWA_CAPTION_COLOR).");
            Add ("Form", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "Form.ForeColor (inherited by children) and the title-bar caption text (DWMWA_TEXT_COLOR).");
            Add ("Form", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, "The window frame colour on Windows 11 (DWMWA_BORDER_COLOR); older Windows ignores it.");
            Add ("Form", null, false, font_properties, WinFormsThemeSupportLevel.Native, "Form.Font — the ambient font every child without its own inherits. Application.SetDefaultFont is used too when no window exists yet.");
            Fill ("Form", null, false, WinFormsThemeSupportLevel.Unsupported, "The window frame is drawn by Windows; only its colour is themable (border-color, Windows 11).");

            // ---- Button -----------------------------------------------------------------------
            Map ("Button", "Button", "FlatStyle.Flat + FlatAppearance. Mapped buttons are switched to FlatStyle.Flat so their colours are honoured.");
            Add ("Button", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "Button.BackColor / ForeColor (FlatStyle.Flat).");
            Add ("Button", null, false, P ("border-width"), WinFormsThemeSupportLevel.Native, "FlatAppearance.BorderSize; 0 ('border: none') removes the border.");
            Add ("Button", null, false, P ("border-color"), WinFormsThemeSupportLevel.Native, "FlatAppearance.BorderColor.");
            Add ("Button", null, false, P ("border-radius"), WinFormsThemeSupportLevel.Approximate, "A rounded Region clips the button; corners are not anti-aliased.");
            Add ("Button", null, false, font_properties, WinFormsThemeSupportLevel.Native, "Button.Font.");
            Add ("Button", null, false, side_properties, WinFormsThemeSupportLevel.Unsupported, uniformSides);
            Add ("Button", null, true, P ("background-color"), WinFormsThemeSupportLevel.Native, "FlatAppearance.MouseOverBackColor (MouseDownBackColor is derived from it).");
            Fill ("Button", null, true, WinFormsThemeSupportLevel.Unsupported, "FlatAppearance only recolours the hovered background; text and border do not change on hover.");

            // ---- Plain text controls ------------------------------------------------------------
            Map ("Label", "Label", "BackColor / ForeColor / Font.");
            Add ("Label", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "Label.BackColor / ForeColor.");
            Add ("Label", null, false, font_properties, WinFormsThemeSupportLevel.Native, "Label.Font.");
            Fill ("Label", null, false, WinFormsThemeSupportLevel.Unsupported, "Labels have no border to style (BorderStyle has no colour).");

            Map ("LinkLabel", "LinkLabel", "LinkColor; :hover color maps to ActiveLinkColor.");
            Add ("LinkLabel", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "LinkLabel.BackColor.");
            Add ("LinkLabel", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "LinkLabel.LinkColor (and ForeColor).");
            Add ("LinkLabel", null, false, font_properties, WinFormsThemeSupportLevel.Native, "LinkLabel.Font.");
            Fill ("LinkLabel", null, false, WinFormsThemeSupportLevel.Unsupported, "Links have no border to style.");
            Add ("LinkLabel", null, true, P ("color"), WinFormsThemeSupportLevel.Approximate, "ActiveLinkColor — WinForms shows it while the link is pressed, not on hover.");
            Fill ("LinkLabel", null, true, WinFormsThemeSupportLevel.Unsupported, "Only the link colour has a pressed-state seam.");

            // ---- Inputs -----------------------------------------------------------------------
            Map ("TextBox", "TextBox, DateTimePicker", "BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None.");
            Add ("TextBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "TextBox.BackColor / ForeColor.");
            Add ("TextBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "TextBox.Font.");
            Add ("TextBox", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Fill ("TextBox", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);

            Map ("NumericUpDown", "NumericUpDown", "BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None.");
            Add ("NumericUpDown", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "NumericUpDown.BackColor / ForeColor.");
            Add ("NumericUpDown", null, false, font_properties, WinFormsThemeSupportLevel.Native, "NumericUpDown.Font.");
            Add ("NumericUpDown", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Fill ("NumericUpDown", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam + " The spin arrows stay native-drawn.");

            Map ("ComboBox", "ComboBox", "BackColor / ForeColor / Font (FlatStyle.Flat so the colours are honoured).");
            Add ("ComboBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ComboBox.BackColor / ForeColor (FlatStyle.Flat).");
            Add ("ComboBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ComboBox.Font.");
            Fill ("ComboBox", null, false, WinFormsThemeSupportLevel.Unsupported, "The combo border and drop arrow are native-drawn; no colour is settable.");

            Map ("CheckBox", "CheckBox", "BackColor / ForeColor / Font; the box glyph follows ForeColor only in FlatStyle.Flat, which is not forced.");
            Add ("CheckBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "CheckBox.BackColor / ForeColor (the glyph stays native-drawn).");
            Add ("CheckBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "CheckBox.Font.");
            Fill ("CheckBox", null, false, WinFormsThemeSupportLevel.Unsupported, "The check glyph is native-drawn.");

            Map ("RadioButton", "RadioButton", "BackColor / ForeColor / Font; the glyph stays native-drawn.");
            Add ("RadioButton", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "RadioButton.BackColor / ForeColor (the glyph stays native-drawn).");
            Add ("RadioButton", null, false, font_properties, WinFormsThemeSupportLevel.Native, "RadioButton.Font.");
            Fill ("RadioButton", null, false, WinFormsThemeSupportLevel.Unsupported, "The radio glyph is native-drawn.");

            // ---- Containers --------------------------------------------------------------------
            Map ("Panel", "Panel, FlowLayoutPanel, TableLayoutPanel, TabPage, SplitterPanel", "BackColor / ForeColor.");
            Add ("Panel", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "Panel.BackColor / ForeColor.");
            Add ("Panel", null, false, font_properties, WinFormsThemeSupportLevel.Native, "Panel.Font (inherited by children).");
            Add ("Panel", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Fill ("Panel", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);

            Map ("GroupBox", "GroupBox", "ForeColor is the caption; the frame is native-drawn.");
            Add ("GroupBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "GroupBox.BackColor / ForeColor (the caption).");
            Add ("GroupBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "GroupBox.Font.");
            Fill ("GroupBox", null, false, WinFormsThemeSupportLevel.Unsupported, "The frame line is native-drawn; recolouring it needs an owner-drawn GroupBox.");

            Map ("SplitContainer", "SplitContainer", "BackColor is the splitter bar (the panels are Panels).");
            Add ("SplitContainer", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "SplitContainer.BackColor / ForeColor.");
            Fill ("SplitContainer", null, false, WinFormsThemeSupportLevel.Unsupported, "Only colours apply to the splitter bar.");

            Map ("Splitter", "Splitter", "BackColor.");
            Add ("Splitter", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "Splitter.BackColor / ForeColor.");
            Fill ("Splitter", null, false, WinFormsThemeSupportLevel.Unsupported, "Only colours apply to a splitter bar.");

            Map ("PictureBox", "PictureBox", "BackColor.");
            Add ("PictureBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "PictureBox.BackColor / ForeColor.");
            Add ("PictureBox", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Fill ("PictureBox", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);

            // ---- Lists and trees ----------------------------------------------------------------
            Map ("ListBox", "ListBox, CheckedListBox", "BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None. Items are owner-drawn (DrawMode.OwnerDrawFixed) so the selection takes the ::selection colours; CheckedListBox and lists the app already owner-draws stay as they are.");
            Add ("ListBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ListBox.BackColor / ForeColor.");
            Add ("ListBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ListBox.Font.");
            Add ("ListBox", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Fill ("ListBox", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);
            Add ("ListBox", "selection", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "Owner-drawn items (DrawMode.OwnerDrawFixed): the selected item's fill and text. Not applied to CheckedListBox (DrawMode.Normal only) or to a list the app already owner-draws.");

            Map ("ListView", "ListView", "BackColor / ForeColor / Font. In Details view the items and column headers are owner-drawn so the selection takes the ::selection colours; other views, lists with CheckBoxes and lists the app already owner-draws stay native.");
            Add ("ListView", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ListView.BackColor / ForeColor.");
            Add ("ListView", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ListView.Font.");
            Fill ("ListView", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);
            Add ("ListView", "selection", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "Owner-drawn rows in Details view (OwnerDraw = true; headers are drawn in the list's colours too). Icon/tile/list views, lists with CheckBoxes and lists the app already owner-draws keep SystemColors.Highlight.");

            Map ("TreeView", "TreeView", "BackColor / ForeColor / Font (node lines follow ForeColor via LineColor); the selected node's text cell is owner-drawn (DrawMode.OwnerDrawText).");
            Add ("TreeView", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "TreeView.BackColor.");
            Add ("TreeView", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "TreeView.ForeColor and LineColor.");
            Add ("TreeView", null, false, font_properties, WinFormsThemeSupportLevel.Native, "TreeView.Font.");
            Add ("TreeView", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Fill ("TreeView", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);
            Add ("TreeView", "selection", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "DrawMode.OwnerDrawText paints the selected node's text cell (glyphs, lines and images stay native). Not applied to a tree the app already owner-draws.");

            // ---- DataGridView -------------------------------------------------------------------
            Map ("DataGridView", "DataGridView", "BackgroundColor, GridColor, DefaultCellStyle and the header/selection/alternating-row cell styles (EnableHeadersVisualStyles is switched off).");
            Add ("DataGridView", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "DataGridView.BackgroundColor and DefaultCellStyle.BackColor.");
            Add ("DataGridView", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "DefaultCellStyle.ForeColor.");
            Add ("DataGridView", null, false, P ("border-color"), WinFormsThemeSupportLevel.Native, "DataGridView.GridColor (the cell grid lines).");
            Add ("DataGridView", null, false, font_properties, WinFormsThemeSupportLevel.Native, "DefaultCellStyle.Font.");
            Fill ("DataGridView", null, false, WinFormsThemeSupportLevel.Unsupported, "The outer border is BorderStyle only; grid lines take a colour (border-color) but no width or radius.");
            Add ("DataGridView", "header", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ColumnHeadersDefaultCellStyle (EnableHeadersVisualStyles = false).");
            Add ("DataGridView", "header", false, font_properties, WinFormsThemeSupportLevel.Native, "ColumnHeadersDefaultCellStyle.Font.");
            Fill ("DataGridView", "header", false, WinFormsThemeSupportLevel.Unsupported, "Header separator lines follow the grid colour; they have no seam of their own.");
            Add ("DataGridView", "row-header", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "RowHeadersDefaultCellStyle.");
            Fill ("DataGridView", "row-header", false, WinFormsThemeSupportLevel.Unsupported, "Row-header separators follow the grid colour.");
            Add ("DataGridView", "selection", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "DefaultCellStyle.SelectionBackColor / SelectionForeColor (headers included).");
            Fill ("DataGridView", "selection", false, WinFormsThemeSupportLevel.Unsupported, "WinForms draws no outline around the current cell beyond the focus rectangle.");
            Add ("DataGridView", "alternating-row", false, P ("background-color"), WinFormsThemeSupportLevel.Native, "AlternatingRowsDefaultCellStyle.BackColor.");

            // ---- Strips (menu, toolbar, statusbar, context menus) --------------------------------
            const string stripRenderer = "A ToolStripProfessionalRenderer built from the theme's tokens is installed on ToolStripManager.Renderer.";
            Map ("Menu", "MenuStrip", stripRenderer);
            Add ("Menu", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "MenuStrip.BackColor and the renderer's strip gradient.");
            Add ("Menu", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "MenuStrip.ForeColor (item text).");
            Add ("Menu", null, false, font_properties, WinFormsThemeSupportLevel.Native, "MenuStrip.Font.");
            Fill ("Menu", null, false, WinFormsThemeSupportLevel.Unsupported, "The strip has no border seam beyond the renderer's own.");
            Add ("Menu", "item", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "The renderer paints items with these colours.");
            Add ("Menu", "item", true, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "The renderer's hovered/open item colours (MenuItemSelected and the hover text colour).");

            Map ("MenuDropDown", "ContextMenuStrip, ToolStripDropDownMenu", stripRenderer);
            Add ("MenuDropDown", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "The renderer's drop-down background (image margin included).");
            Add ("MenuDropDown", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "Drop-down item text.");
            Add ("MenuDropDown", null, false, P ("border-color"), WinFormsThemeSupportLevel.Native, "The renderer's MenuBorder colour.");
            Add ("MenuDropDown", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ToolStripDropDownMenu.Font (set when the drop-down is themed).");
            Fill ("MenuDropDown", null, false, WinFormsThemeSupportLevel.Unsupported, "Only the border colour of a drop-down is themable.");
            Add ("MenuDropDown", "item", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "The renderer paints drop-down items with these colours.");
            Add ("MenuDropDown", "item", true, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "The renderer's hovered item colours.");

            Map ("ToolBar", "ToolStrip", stripRenderer);
            Add ("ToolBar", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "ToolStrip.BackColor and the renderer's strip gradient.");
            Add ("ToolBar", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "ToolStrip.ForeColor (item text).");
            Add ("ToolBar", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ToolStrip.Font.");
            Fill ("ToolBar", null, false, WinFormsThemeSupportLevel.Unsupported, "The strip has no border seam beyond the renderer's own.");
            Add ("ToolBar", "item", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "The renderer paints items with these colours.");
            Add ("ToolBar", "item", true, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "The renderer's hovered/checked item colours.");

            Map ("StatusBar", "StatusStrip", stripRenderer);
            Add ("StatusBar", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "StatusStrip.BackColor and the renderer's status gradient.");
            Add ("StatusBar", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "StatusStrip.ForeColor.");
            Add ("StatusBar", null, false, font_properties, WinFormsThemeSupportLevel.Native, "StatusStrip.Font.");
            Fill ("StatusBar", null, false, WinFormsThemeSupportLevel.Unsupported, "The strip has no border seam.");

            // ---- Tabs ---------------------------------------------------------------------------
            Map ("TabControl", "TabControl", "TabPage backgrounds; any colour on the header band needs owner draw (see TabStrip).");
            Add ("TabControl", null, false, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "Applies to each TabPage's BackColor; the control's own band stays native-drawn.");
            Add ("TabControl", null, false, P ("color"), WinFormsThemeSupportLevel.Approximate, "Applies to each TabPage's ForeColor.");
            Add ("TabControl", null, false, font_properties, WinFormsThemeSupportLevel.Native, "TabControl.Font.");
            Fill ("TabControl", null, false, WinFormsThemeSupportLevel.Unsupported, "The frame around the pages is native-drawn.");

            Map ("TabStrip", "TabControl (the header row, via DrawMode.OwnerDrawFixed)", "Owner-drawn tab headers; WinForms tabs do not repaint on hover.");
            Add ("TabStrip", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "Owner-drawn tab headers (DrawMode.OwnerDrawFixed); the band around them stays native.");
            Add ("TabStrip", null, false, font_properties, WinFormsThemeSupportLevel.Native, "TabControl.Font.");
            Fill ("TabStrip", null, false, WinFormsThemeSupportLevel.Unsupported, "The header band is native-drawn beyond the tabs themselves.");
            Add ("TabStrip", "item", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "The owner-drawn tab's fill and caption.");
            Add ("TabStrip", "item", true, all_properties, WinFormsThemeSupportLevel.Unsupported, "WinForms tabs do not repaint on hover.");
            Add ("TabStrip", "selected", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "The owner-drawn selected tab's fill and caption.");
            Add ("TabStrip", "selected", false, P ("border-bottom-color", "border-bottom-width"), WinFormsThemeSupportLevel.Approximate, "An accent underline drawn inside the owner-drawn selected tab.");
            Fill ("TabStrip", "selected", false, WinFormsThemeSupportLevel.Unsupported, "Only the fill, caption and underline of the selected tab are drawn.");
            Fill ("TabStrip", "item", false, WinFormsThemeSupportLevel.Unsupported, "Only the fill and caption of a tab are drawn.");

            // ---- Native-drawn controls with (almost) no seams ------------------------------------
            Map ("TrackBar", "TrackBar", "BackColor only; the groove and thumb are native-drawn.");
            Add ("TrackBar", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "TrackBar.BackColor.");
            Fill ("TrackBar", null, false, WinFormsThemeSupportLevel.Unsupported, "The groove, thumb and ticks are native-drawn.");
            Add ("TrackBar", null, true, all_properties, WinFormsThemeSupportLevel.Unsupported, "WinForms track bars have no hover seam.");

            Map ("MonthCalendar", "MonthCalendar", "BackColor / ForeColor apply to the day grid; the title keeps visual styles.");
            Add ("MonthCalendar", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "MonthCalendar.BackColor / ForeColor recolour the day grid; the title bar and today circle keep visual styles.");
            Fill ("MonthCalendar", null, false, WinFormsThemeSupportLevel.Unsupported, "The calendar chrome is native-drawn (TitleBackColor and friends only work with visual styles off, process-wide).");

            Map ("ScrollBar", "HScrollBar, VScrollBar", "Native-drawn; no colour applies without owner-drawing a replacement control.");
            Add ("ScrollBar", null, false, all_properties, WinFormsThemeSupportLevel.Unsupported, "WinForms scroll bars are native-drawn; BackColor/ForeColor have no visible effect.");
            Add ("ScrollBar", "thumb", false, all_properties, WinFormsThemeSupportLevel.Unsupported, "The thumb is native-drawn.");
            Add ("ScrollBar", "arrow", false, all_properties, WinFormsThemeSupportLevel.Unsupported, "The arrows are native-drawn.");

            Map ("PropertyGrid", "PropertyGrid", "ViewBackColor / ViewForeColor / LineColor / HelpBackColor.");
            Add ("PropertyGrid", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "PropertyGrid.BackColor, ViewBackColor and HelpBackColor.");
            Add ("PropertyGrid", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "PropertyGrid.ViewForeColor, HelpForeColor and CategoryForeColor.");
            Add ("PropertyGrid", null, false, P ("border-color"), WinFormsThemeSupportLevel.Native, "PropertyGrid.LineColor (the category/grid lines).");
            Add ("PropertyGrid", null, false, font_properties, WinFormsThemeSupportLevel.Native, "PropertyGrid.Font.");
            Fill ("PropertyGrid", null, false, WinFormsThemeSupportLevel.Unsupported, "The grid draws no themable outer border.");

            // ---- Selectors with no WinForms counterpart ------------------------------------------
            Map ("NavigationPane", null, "System.Windows.Forms has no Outlook-style navigation pane; the rule is reported as an info diagnostic and skipped.");
            Map ("Ribbon", null, "System.Windows.Forms has no ribbon control; the rule is reported as an info diagnostic and skipped.");

            Mappings = mappings;
            Entries = entries;

            index = entries.ToDictionary (e => (e.Selector.ToLowerInvariant (), e.Part?.ToLowerInvariant (), e.Hover, e.Property.ToLowerInvariant ()));
            mapping_index = mappings.ToDictionary (m => m.Selector.ToLowerInvariant ());
        }

        /// <summary>
        /// Renders the matrix as the Markdown tables committed in <c>docs/theming-winforms.md</c>
        /// (a test keeps the two in sync). Paste it into a prompt to let a coding assistant predict
        /// what a stylesheet will do to a real WinForms app.
        /// </summary>
        public static string ToMarkdown ()
        {
            var sb = new StringBuilder ();

            sb.AppendLine ("### Selector mapping");
            sb.AppendLine ();
            sb.AppendLine ("| Selector | System.Windows.Forms | Notes |");
            sb.AppendLine ("|---|---|---|");
            foreach (var m in Mappings)
                sb.AppendLine ($"| `{m.Selector}` | {(m.WinFormsTypes is null ? "—" : $"`{m.WinFormsTypes}`")} | {Escape (m.Notes)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Property support");
            sb.AppendLine ();
            sb.AppendLine ("Unsupported rows are reported as diagnostics when a stylesheet uses them — never silently ignored.");
            sb.AppendLine ();
            sb.AppendLine ("| Rule | Property | Support | Notes |");
            sb.AppendLine ("|---|---|---|---|");
            foreach (var e in Entries.Where (e => e.Level != WinFormsThemeSupportLevel.Unsupported))
                sb.AppendLine ($"| `{RuleText (e)}` | `{e.Property}` | {LevelText (e.Level)} | {Escape (e.Notes)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Unsupported (reported, then skipped)");
            sb.AppendLine ();
            sb.AppendLine ("| Rule | Property | Why |");
            sb.AppendLine ("|---|---|---|");
            foreach (var group in Entries.Where (e => e.Level == WinFormsThemeSupportLevel.Unsupported)
                         .GroupBy (e => (RuleText (e), e.Notes)))
                sb.AppendLine ($"| `{group.Key.Item1}` | {string.Join (", ", group.Select (e => $"`{e.Property}`"))} | {Escape (group.Key.Notes)} |");

            return sb.ToString ();
        }

        private static string RuleText (WinFormsThemeSupportEntry e)
            => $"{e.Selector}{(e.Part is null ? "" : "::" + e.Part)}{(e.Hover ? ":hover" : "")}";

        private static string LevelText (WinFormsThemeSupportLevel level) => level switch {
            WinFormsThemeSupportLevel.Native => "native",
            WinFormsThemeSupportLevel.Approximate => "approximate",
            _ => "unsupported"
        };

        private static string Escape (string text) => text.Replace ("|", "\\|");
    }
}
