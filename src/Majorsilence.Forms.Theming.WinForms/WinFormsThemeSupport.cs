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

    /// <summary>The opt-out switch a row depends on: with the switch off the row behaves as Unsupported.</summary>
    internal enum WinFormsThemeGate
    {
        /// <summary>No switch.</summary>
        None,

        /// <summary><see cref="WinFormsCssTheme.PaintBorders"/>.</summary>
        PaintBorders,

        /// <summary><see cref="WinFormsCssTheme.PaintEffects"/>.</summary>
        PaintEffects,

        /// <summary><see cref="WinFormsCssTheme.Animate"/>.</summary>
        Animate
    }

    /// <summary>
    /// One row of the support matrix: what happens to <c>Selector[::Part][:hover] { Property }</c>
    /// when applied to real WinForms controls.
    /// </summary>
    public sealed class WinFormsThemeSupportEntry
    {
        internal WinFormsThemeSupportEntry (string selector, string? part, bool hover, string property, WinFormsThemeSupportLevel level, string notes, WinFormsThemeGate gate = WinFormsThemeGate.None)
            : this (selector, part, hover ? ThemeCssState.Hover : ThemeCssState.Normal, property, level, notes, gate)
        {
        }

        internal WinFormsThemeSupportEntry (string selector, string? part, ThemeCssState state, string property, WinFormsThemeSupportLevel level, string notes, WinFormsThemeGate gate = WinFormsThemeGate.None)
        {
            Gate = gate;
            Selector = selector;
            Part = part;
            State = state;
            Property = property;
            Level = level;
            Notes = notes;
        }

        /// <summary>The CSS selector (the Majorsilence.Forms control type name).</summary>
        public string Selector { get; }

        internal WinFormsThemeGate Gate { get; }

        /// <summary>The <c>::part</c> pseudo-element, or null for the control itself.</summary>
        public string? Part { get; }

        /// <summary>The state the row describes: the resting look or one pseudo-class.</summary>
        public ThemeCssState State { get; }

        /// <summary>Whether the row describes the <c>:hover</c> variant.</summary>
        public bool Hover => State == ThemeCssState.Hover;

        /// <summary>The longhand property name.</summary>
        public string Property { get; }

        /// <summary>How faithfully the pair applies.</summary>
        public WinFormsThemeSupportLevel Level { get; }

        /// <summary>What it maps to, or why it cannot.</summary>
        public string Notes { get; }

        /// <inheritdoc/>
        public override string ToString ()
            => $"{Selector}{(Part is null ? "" : "::" + Part)}{StateText (State)} {{ {Property} }} -> {Level}";

        internal static string StateText (ThemeCssState state) => state switch {
            ThemeCssState.Hover => ":hover",
            ThemeCssState.Active => ":active",
            ThemeCssState.Disabled => ":disabled",
            ThemeCssState.Focus => ":focus",
            ThemeCssState.FocusVisible => ":focus-visible",
            _ => ""
        };
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
        private static readonly (string Property, string Note)[] effect_properties = {
            ("opacity", "System.Windows.Forms child controls are separate windows composited opaquely; only a Form has Opacity."),
            ("background-image", "Gradients are painted into a BackgroundImage where the control paints one."),
            ("outline-width", "System.Windows.Forms has no outline; the focus rectangle is drawn by each control."),
            ("outline-style", "System.Windows.Forms has no outline; the focus rectangle is drawn by each control."),
            ("outline-color", "System.Windows.Forms has no outline; the focus rectangle is drawn by each control."),
            ("outline-offset", "System.Windows.Forms has no outline; the focus rectangle is drawn by each control."),
        };

        private static readonly string[] transition_properties = { "transition-property", "transition-duration", "transition-timing-function", "transition-delay",
            "animation-name", "animation-duration", "animation-timing-function", "animation-delay", "animation-iteration-count", "animation-direction", "animation-fill-mode", "animation-play-state" };

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

        /// <summary>Every (selector, part, state, property) row.</summary>
        public static IReadOnlyList<WinFormsThemeSupportEntry> Entries { get; }

        private static readonly Dictionary<(string Selector, string? Part, ThemeCssState State, string Property), WinFormsThemeSupportEntry> index;
        private static readonly Dictionary<string, WinFormsThemeSelectorMapping> mapping_index;

        /// <summary>
        /// Looks up the row for one declaration. Returns null when the selector has no WinForms
        /// counterpart at all (see <see cref="FindMapping"/>).
        /// </summary>
        public static WinFormsThemeSupportEntry? Find (string selector, string? part, bool hover, string property)
            => Find (selector, part, hover ? ThemeCssState.Hover : ThemeCssState.Normal, property);

        /// <summary>
        /// Looks up the row for one declaration in a given state. Returns null when the selector has no
        /// WinForms counterpart, or does not take that state (see <see cref="FindMapping"/>).
        /// </summary>
        public static WinFormsThemeSupportEntry? Find (string selector, string? part, ThemeCssState state, string property)
        {
            index.TryGetValue ((selector.ToLowerInvariant (), part?.ToLowerInvariant (), state, property.ToLowerInvariant ()), out var entry);
            return entry;
        }

        /// <summary>
        /// Whether any state other than the resting look and <c>:hover</c> has a row that does something on
        /// WinForms (Native or Approximate). Until a state gains such a row the appliers report a rule for it once and skip it.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <param name="state">The state to ask about.</param>
        public static bool AppliesInState (string selector, ThemeCssState state)
            => Entries.Any (e => e.State == state && e.Level != WinFormsThemeSupportLevel.Unsupported
                && string.Equals (e.Selector, selector, StringComparison.OrdinalIgnoreCase));

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

            void Add (string selector, string? part, bool hover, string[] properties, WinFormsThemeSupportLevel level, string notes, WinFormsThemeGate gate = WinFormsThemeGate.None)
            {
                foreach (var property in properties)
                    entries.Add (new WinFormsThemeSupportEntry (selector, part, hover, property, level, notes, gate));
            }

            void Fill (string selector, string? part, bool hover, WinFormsThemeSupportLevel level, string notes)
            {
                var present = entries.Where (e => e.Selector == selector && e.Part == part && e.Hover == hover).Select (e => e.Property).ToHashSet ();
                foreach (var property in all_properties)
                    if (!present.Contains (property))
                        entries.Add (new WinFormsThemeSupportEntry (selector, part, hover, property, level, notes));
            }

            string[] P (params string[] names) => names;

            void AddState (string selector, ThemeCssState state, string[] properties, WinFormsThemeSupportLevel level, string notes, WinFormsThemeGate gate = WinFormsThemeGate.None)
            {
                foreach (var property in properties)
                    entries.Add (new WinFormsThemeSupportEntry (selector, null, state, property, level, notes, gate));
            }

            const string uniformSides = "WinForms control borders are uniform; use border-width / border-color.";
            const string noBorderSeam = "WinForms draws this control's border natively (BorderStyle only); only its colour can be repainted (border-color).";
            const string ncBorder = "The 1px frame is repainted in this colour after the control draws it (WM_NCPAINT), and a 3-D border becomes BorderStyle.FixedSingle. Off with WinFormsCssTheme.PaintBorders = false; a control that paints itself keeps its own.";
            const string overlayBorder = "The control's own 1px border is repainted in this colour after it paints. Off with WinFormsCssTheme.PaintBorders = false; a control that paints itself keeps its own.";

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
            Add ("Button", null, true, P ("color", "border-color"), WinFormsThemeSupportLevel.Approximate, "ForeColor / FlatAppearance.BorderColor are swapped while the pointer is over the button (MouseEnter / MouseLeave); the hovered text defaults to --foreground-color-on-accent and the border to --accent-color-2, as in Majorsilence.Forms.");
            Fill ("Button", null, true, WinFormsThemeSupportLevel.Unsupported, "FlatAppearance only recolours the hovered background; the rest comes from the normal rule.");

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
            Add ("LinkLabel", null, true, P ("color"), WinFormsThemeSupportLevel.Native, "LinkColor, swapped while the pointer is over the link (MouseEnter / MouseLeave).");
            Add ("LinkLabel", null, true, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "BackColor, swapped while the pointer is over the link.");
            Fill ("LinkLabel", null, true, WinFormsThemeSupportLevel.Unsupported, "Only the link colour and fill change on hover.");

            // ---- Inputs -----------------------------------------------------------------------
            Map ("TextBox", "TextBox, DateTimePicker", "BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None.");
            Add ("TextBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "TextBox.BackColor / ForeColor.");
            Add ("TextBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "TextBox.Font.");
            Add ("TextBox", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Add ("TextBox", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
            Fill ("TextBox", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);

            Map ("NumericUpDown", "NumericUpDown", "BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None.");
            Add ("NumericUpDown", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "NumericUpDown.BackColor / ForeColor.");
            Add ("NumericUpDown", null, false, font_properties, WinFormsThemeSupportLevel.Native, "NumericUpDown.Font.");
            Add ("NumericUpDown", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Add ("NumericUpDown", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
            Fill ("NumericUpDown", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam + " The spin arrows stay native-drawn.");

            Map ("ComboBox", "ComboBox", "BackColor / ForeColor / Font (FlatStyle.Flat so the colours are honoured).");
            Add ("ComboBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ComboBox.BackColor / ForeColor (FlatStyle.Flat).");
            Add ("ComboBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ComboBox.Font.");
            Add ("ComboBox", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, overlayBorder, WinFormsThemeGate.PaintBorders);
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
            Add ("Panel", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
            Fill ("Panel", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);

            Map ("GroupBox", "GroupBox", "ForeColor is the caption; the frame is native-drawn.");
            Add ("GroupBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "GroupBox.BackColor / ForeColor (the caption).");
            Add ("GroupBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "GroupBox.Font.");
            Add ("GroupBox", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate,
                "The frame is erased and redrawn in this colour as a plain rectangle (square corners) with a gap for the caption. Off with WinFormsCssTheme.PaintBorders = false.", WinFormsThemeGate.PaintBorders);
            Fill ("GroupBox", null, false, WinFormsThemeSupportLevel.Unsupported, "The frame is repainted as a plain rectangle; its width and corners cannot change.");

            Map ("SplitContainer", "SplitContainer", "BackColor is the splitter bar (the panels are Panels).");
            Add ("SplitContainer", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "SplitContainer.BackColor / ForeColor.");
            Fill ("SplitContainer", null, false, WinFormsThemeSupportLevel.Unsupported, "Only colours apply to the splitter bar.");

            Map ("Splitter", "Splitter", "BackColor.");
            Add ("Splitter", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "Splitter.BackColor / ForeColor.");
            Fill ("Splitter", null, false, WinFormsThemeSupportLevel.Unsupported, "Only colours apply to a splitter bar.");

            Map ("PictureBox", "PictureBox", "BackColor.");
            Add ("PictureBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "PictureBox.BackColor / ForeColor.");
            Add ("PictureBox", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Add ("PictureBox", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
            Fill ("PictureBox", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);

            Map ("DateTimePicker", "DateTimePicker", "BackColor / ForeColor / Font, and the drop-down calendar's month background and text. A DateTimePicker rule wins over a TextBox rule, which styled it before.");
            Add ("DateTimePicker", null, false, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "DateTimePicker.BackColor and CalendarMonthBackground; the drop button keeps the system look.");
            Add ("DateTimePicker", null, false, P ("color"), WinFormsThemeSupportLevel.Approximate, "DateTimePicker.ForeColor and CalendarForeColor; the drop button keeps the system look.");
            Add ("DateTimePicker", null, false, font_properties, WinFormsThemeSupportLevel.Native, "DateTimePicker.Font.");
            Fill ("DateTimePicker", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam + " The drop button and check box are native-drawn.");

            Map ("ProgressBar", "ProgressBar", "BackColor is the track and ForeColor the fill (--accent-color-2); visual styles are switched off for the bar's window so they are honoured.");
            Add ("ProgressBar", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "ProgressBar.BackColor — the track.");
            Add ("ProgressBar", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate,
                "The outer ring of the bar is repainted in this colour after it paints (inside the bar, over the track). Off with WinFormsCssTheme.PaintBorders = false.", WinFormsThemeGate.PaintBorders);
            Fill ("ProgressBar", null, false, WinFormsThemeSupportLevel.Unsupported, "The fill follows --accent-color-2, the bar draws no text and its frame is only repainted in a colour.");

            // ---- Lists and trees ----------------------------------------------------------------
            Map ("ListBox", "ListBox, CheckedListBox", "BackColor / ForeColor / Font; 'border: none' maps to BorderStyle.None. Items are owner-drawn (DrawMode.OwnerDrawFixed) so the selection takes the ::selection colours; CheckedListBox and lists the app already owner-draws stay as they are.");
            Add ("ListBox", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ListBox.BackColor / ForeColor.");
            Add ("ListBox", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ListBox.Font.");
            Add ("ListBox", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Add ("ListBox", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
            Fill ("ListBox", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);
            Add ("ListBox", "selection", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "Owner-drawn items (DrawMode.OwnerDrawFixed): the selected item's fill and text. Not applied to CheckedListBox (DrawMode.Normal only) or to a list the app already owner-draws.");

            Map ("ListView", "ListView", "BackColor / ForeColor / Font. In Details view the items and column headers are owner-drawn so the selection takes the ::selection colours; other views, lists with CheckBoxes and lists the app already owner-draws stay native.");
            Add ("ListView", null, false, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "ListView.BackColor / ForeColor.");
            Add ("ListView", null, false, font_properties, WinFormsThemeSupportLevel.Native, "ListView.Font.");
            Add ("ListView", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
            Fill ("ListView", null, false, WinFormsThemeSupportLevel.Unsupported, noBorderSeam);
            Add ("ListView", "selection", false, P ("background-color", "color"), WinFormsThemeSupportLevel.Approximate, "Owner-drawn rows in Details view (OwnerDraw = true; headers are drawn in the list's colours too). Icon/tile/list views, lists with CheckBoxes and lists the app already owner-draws keep SystemColors.Highlight.");

            Map ("TreeView", "TreeView", "BackColor / ForeColor / Font (node lines follow ForeColor via LineColor); the selected node's text cell is owner-drawn (DrawMode.OwnerDrawText).");
            Add ("TreeView", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "TreeView.BackColor.");
            Add ("TreeView", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "TreeView.ForeColor and LineColor.");
            Add ("TreeView", null, false, font_properties, WinFormsThemeSupportLevel.Native, "TreeView.Font.");
            Add ("TreeView", null, false, P ("border-width"), WinFormsThemeSupportLevel.Approximate, "0 -> BorderStyle.None, otherwise BorderStyle.FixedSingle; the width itself is fixed at 1px.");
            Add ("TreeView", null, false, P ("border-color"), WinFormsThemeSupportLevel.Approximate, ncBorder, WinFormsThemeGate.PaintBorders);
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

            // A real StatusStrip is the core's StatusStrip; the StatusBar rule still styles it when the sheet has no StatusStrip rule.
            Map ("StatusStrip", "StatusStrip", stripRenderer + " A StatusStrip rule wins over a StatusBar rule.");
            Add ("StatusStrip", null, false, P ("background-color"), WinFormsThemeSupportLevel.Native, "StatusStrip.BackColor and the renderer's status gradient.");
            Add ("StatusStrip", null, false, P ("color"), WinFormsThemeSupportLevel.Native, "StatusStrip.ForeColor.");
            Add ("StatusStrip", null, false, font_properties, WinFormsThemeSupportLevel.Native, "StatusStrip.Font.");
            Add ("StatusStrip", null, false, P ("border-top-color", "border-top-width"), WinFormsThemeSupportLevel.Approximate, "The renderer draws the seam along the strip's top edge.");
            Fill ("StatusStrip", null, false, WinFormsThemeSupportLevel.Unsupported, "The strip has no border seam beyond its top edge.");

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
            Add ("TrackBar", null, true, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "TrackBar.BackColor, swapped while the pointer is over the control (the groove and thumb stay native-drawn).");
            Add ("TrackBar", null, true, all_properties.Where (p => p != "background-color").ToArray (), WinFormsThemeSupportLevel.Unsupported, "Only the track bar's background colour can change on hover.");

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
            Map ("FormTitleBar", null, "No counterpart: the Windows title bar is themed through the Form rule (DwmSetWindowAttribute); the rule is reported as an info diagnostic and skipped.");
            Map ("HostedSurface", null, "No counterpart: it is the surface Majorsilence.Forms embeds in another UI toolkit; the rule is reported as an info diagnostic and skipped.");
            Map ("MdiClient", null, "No counterpart: the WinForms MDI workspace is native-drawn; the rule is reported as an info diagnostic and skipped.");
            Map ("PopupWindow", null, "No counterpart: WinForms popups are native windows; the rule is reported as an info diagnostic and skipped.");
            Map ("PrintPreviewControl", null, "No counterpart: the WinForms print preview is native-drawn; the rule is reported as an info diagnostic and skipped.");
            Map ("DockWindowBase", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RadCommandBar", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RadPdfViewerNavigator", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RadRibbonBar", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RadScheduler", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RadSchedulerNavigator", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RadStatusStrip", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");
            Map ("RichTextEditorRibbonBar", null, "A Majorsilence.Forms.Telerik control with no System.Windows.Forms counterpart; the rule is reported as an info diagnostic and skipped.");

            // The paint effects (#285, #286): box-shadow, outline and background-image. System.Windows.Forms has no property for any of
            // them, so the theme paints them itself -- shadows and outlines from the control's parent, an inset shadow from its own
            // Paint event, a gradient into its BackgroundImage -- behind WinFormsCssTheme.PaintEffects. A window and the strips are
            // not painted by a parent; a control that does not paint a BackgroundImage cannot show a gradient.
            var not_parent_painted = new HashSet<string> { "Form", "Menu", "MenuDropDown", "ToolBar", "StatusBar", "StatusStrip" };
            var gradient_selectors = new HashSet<string> { "Form", "Button", "Label", "LinkLabel", "CheckBox", "RadioButton", "Panel", "GroupBox" };
            const string ShadowNote = "An outer shadow (offset, spread, blur, a list) is painted behind the control by its parent, outside it and not shrinking it; an inset shadow is painted over the face from the control's own Paint event, on the controls that raise one. Off with WinFormsCssTheme.PaintEffects = false.";
            const string OutlineNote = "A ring painted around the control, outside it, by its parent: it takes no room, follows the button's corner radii and honours outline-offset. Off with WinFormsCssTheme.PaintEffects = false.";
            const string GradientNote = "The gradient layers are painted over the background colour into a BackgroundImage that follows the control's size. Off with WinFormsCssTheme.PaintEffects = false.";
            const string NoParentPaint = "A window and a strip are not painted by a parent, which is where the theme paints shadows and outlines.";
            const string NoBackgroundImage = "This control does not paint a BackgroundImage, so there is nowhere to put a gradient.";

            (WinFormsThemeSupportLevel Level, string Note, WinFormsThemeGate Gate) EffectRow (string selector, string property, bool stateRow)
            {
                if (property == "box-shadow" || property.StartsWith ("outline-", StringComparison.Ordinal)) {
                    if (not_parent_painted.Contains (selector))
                        return (WinFormsThemeSupportLevel.Unsupported, NoParentPaint, WinFormsThemeGate.None);

                    return (WinFormsThemeSupportLevel.Approximate, property == "box-shadow" ? ShadowNote : OutlineNote, WinFormsThemeGate.PaintEffects);
                }

                if (property == "background-image") {
                    if (stateRow)
                        return (WinFormsThemeSupportLevel.Unsupported, "Only the resting background-image is painted; it does not change with the control's state.", WinFormsThemeGate.None);

                    return gradient_selectors.Contains (selector)
                        ? (WinFormsThemeSupportLevel.Approximate, GradientNote, WinFormsThemeGate.PaintEffects)
                        : (WinFormsThemeSupportLevel.Unsupported, NoBackgroundImage, WinFormsThemeGate.None);
                }

                return (WinFormsThemeSupportLevel.Unsupported, effect_properties.First (p => p.Property == property).Note, WinFormsThemeGate.None);
            }

            foreach (var mapping in mappings) {
                if (mapping.WinFormsTypes is null)
                    continue;

                var (level, note, gate) = EffectRow (mapping.Selector, "box-shadow", false);
                entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, false, "box-shadow", level, note, gate));

                if (ThemeCssReference.FindSelector (mapping.Selector)?.SupportsHover == true)
                    entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, true, "box-shadow", level, note, gate));
            }

            // Per-corner radii and dashed borders (#286). A button is clipped to a rounded Region built from four radii; a control whose
            // 1px frame is repainted (BorderPainter) can repaint it as dashes. Everything else is Unsupported.
            const string NoCornerRadius = "Only buttons are clipped to a rounded Region; other controls keep square corners.";
            const string CornerRadiusRegion = "The Region that clips the button takes a radius per corner; the corners are not anti-aliased.";
            const string NoBorderStyle = "System.Windows.Forms draws solid borders; only a frame this theme repaints (see border-color) can be dashed.";
            const string DashedFrame = "The repainted frame is drawn as dashes (each dash and gap three border-widths long) over the control's background colour. Off with WinFormsCssTheme.PaintBorders = false.";
            var dashable = new HashSet<string> { "TextBox", "NumericUpDown", "ComboBox", "Panel", "PictureBox", "ListBox", "ListView", "TreeView", "ProgressBar" };

            foreach (var mapping in mappings) {
                if (mapping.WinFormsTypes is null)
                    continue;

                foreach (var property in new[] { "border-style", "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius" }) {
                    var note = property == "border-style" ? NoBorderStyle : NoCornerRadius;
                    var level = WinFormsThemeSupportLevel.Unsupported;
                    var gate = WinFormsThemeGate.None;

                    if (property == "border-style" && dashable.Contains (mapping.Selector)) {
                        level = WinFormsThemeSupportLevel.Approximate;
                        note = DashedFrame;
                        gate = WinFormsThemeGate.PaintBorders;
                    } else if (property != "border-style" && mapping.Selector == "Button") {
                        level = WinFormsThemeSupportLevel.Approximate;
                        note = CornerRadiusRegion;
                    }

                    entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, false, property, level, note, gate));

                    if (ThemeCssReference.FindSelector (mapping.Selector)?.SupportsHover == true)
                        entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, true, property, WinFormsThemeSupportLevel.Unsupported, note));
                }
            }

            // CSS transitions and keyframe animations. WinForms repaints a changed property at once and has no animation system, so a
            // timer steps the colours a state changes (the controls that have states) along the rule's easing. Only colours move.
            const string NoTransition = "This control has no states whose colours the theme swaps, so there is nothing to animate; System.Windows.Forms repaints it at once.";
            const string MotionNote = "The colours the control's states swap (BackColor, ForeColor, a border or link colour) fade along the rule's easing from a UI-thread timer, and @keyframes play on those colours; a shadow, radius or opacity does not move. Nothing moves when Windows has UI effects off. Off with WinFormsCssTheme.Animate = false.";
            var animated = new HashSet<string> { "Button", "LinkLabel", "TextBox", "TrackBar" };

            (WinFormsThemeSupportLevel Level, string Note, WinFormsThemeGate Gate) MotionRow (string selector)
                => animated.Contains (selector)
                    ? (WinFormsThemeSupportLevel.Approximate, MotionNote, WinFormsThemeGate.Animate)
                    : (WinFormsThemeSupportLevel.Unsupported, NoTransition, WinFormsThemeGate.None);

            foreach (var mapping in mappings) {
                if (mapping.WinFormsTypes is null)
                    continue;

                var (motionLevel, motionNote, motionGate) = MotionRow (mapping.Selector);

                foreach (var property in transition_properties) {
                    entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, false, property, motionLevel, motionNote, motionGate));

                    if (ThemeCssReference.FindSelector (mapping.Selector)?.SupportsHover == true)
                        entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, true, property, motionLevel, motionNote, motionGate));
                }
            }

            // opacity, outline and background-image. Child windows are composited opaquely, so opacity stays Unsupported everywhere.
            foreach (var mapping in mappings) {
                if (mapping.WinFormsTypes is null)
                    continue;

                foreach (var (property, _) in effect_properties) {
                    var (level, note, gate) = EffectRow (mapping.Selector, property, false);
                    entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, false, property, level, note, gate));

                    if (ThemeCssReference.FindSelector (mapping.Selector)?.SupportsHover == true) {
                        var (hoverLevel, hoverNote, hoverGate) = EffectRow (mapping.Selector, property, true);
                        entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, true, property, hoverLevel, hoverNote, hoverGate));
                    }
                }
            }

            // ---- States ------------------------------------------------------------------------------------------
            // WinForms has no per-state styling, so a state is applied by swapping properties from the control's own events
            // (MouseDown / MouseUp, EnabledChanged, GotFocus / LostFocus; :focus-visible is :focus once the window shows focus
            // cues). It is not animated and not as exact as a renderer, but it is real: see WinFormsThemeApplier.States.cs.
            const string pressNote = "Swapped on MouseDown / MouseUp.";
            const string enabledNote = "Swapped on EnabledChanged.";
            const string focusNote = "Swapped on GotFocus / LostFocus.";
            const string focusVisibleNote = "Swapped on GotFocus / LostFocus while the window shows focus cues (after Tab), on top of :focus.";

            AddState ("Button", ThemeCssState.Active, P ("background-color"), WinFormsThemeSupportLevel.Native, "FlatAppearance.MouseDownBackColor.");
            AddState ("Button", ThemeCssState.Active, P ("color", "border-color"), WinFormsThemeSupportLevel.Approximate, "ForeColor / FlatAppearance.BorderColor. " + pressNote);
            AddState ("Button", ThemeCssState.Disabled, P ("background-color"), WinFormsThemeSupportLevel.Native, "Button.BackColor, which a flat button honours when disabled. " + enabledNote);
            AddState ("Button", ThemeCssState.Disabled, P ("color", "border-color"), WinFormsThemeSupportLevel.Approximate, "ForeColor / FlatAppearance.BorderColor; a flat button draws disabled text in a shade it derives itself. " + enabledNote);
            AddState ("Button", ThemeCssState.Focus, P ("background-color", "color", "border-color"), WinFormsThemeSupportLevel.Approximate, "BackColor / ForeColor / FlatAppearance.BorderColor. " + focusNote);
            AddState ("Button", ThemeCssState.FocusVisible, P ("background-color", "color", "border-color"), WinFormsThemeSupportLevel.Approximate, "BackColor / ForeColor / FlatAppearance.BorderColor. " + focusVisibleNote);

            AddState ("LinkLabel", ThemeCssState.Active, P ("color"), WinFormsThemeSupportLevel.Native, "LinkLabel.ActiveLinkColor, which WinForms shows while the link is pressed.");
            AddState ("LinkLabel", ThemeCssState.Active, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "BackColor. " + pressNote);
            AddState ("LinkLabel", ThemeCssState.Disabled, P ("color"), WinFormsThemeSupportLevel.Native, "LinkLabel.DisabledLinkColor.");
            AddState ("LinkLabel", ThemeCssState.Disabled, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "BackColor. " + enabledNote);
            AddState ("LinkLabel", ThemeCssState.Focus, P ("color", "background-color"), WinFormsThemeSupportLevel.Approximate, "LinkColor / BackColor. " + focusNote);
            AddState ("LinkLabel", ThemeCssState.FocusVisible, P ("color", "background-color"), WinFormsThemeSupportLevel.Approximate, "LinkColor / BackColor. " + focusVisibleNote);

            foreach (var state in new[] { ThemeCssState.Focus, ThemeCssState.FocusVisible }) {
                AddState ("TextBox", state, P ("background-color", "color"), WinFormsThemeSupportLevel.Native, "TextBox.BackColor / ForeColor. " + focusNote + " A focused text box always counts as :focus-visible.");
                AddState ("TextBox", state, P ("border-color"), WinFormsThemeSupportLevel.Approximate, "The repainted frame's colour while the box has focus (see TextBox { border-color }).", WinFormsThemeGate.PaintBorders);
            }

            foreach (var state in new[] { ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus, ThemeCssState.FocusVisible })
                AddState ("TrackBar", state, P ("background-color"), WinFormsThemeSupportLevel.Approximate, "TrackBar.BackColor, swapped from the control's own events (the groove and thumb stay native-drawn).");

            // The other pseudo-classes (#285): until now only :hover had rows, so :active, :disabled, :focus
            // and :focus-visible were invisible to the completeness tests. One row per longhand the control
            // accepts, in every state the control takes, all Unsupported for now -- a state gains a seam by
            // flipping its rows, in the same change as the applier (phase 2 of docs/theming-parity-plan.md).
            const string NoStateSeam = "System.Windows.Forms has no seam for this state on this control.";

            foreach (var mapping in mappings.ToArray ()) {
                if (mapping.WinFormsTypes is null || ThemeCssReference.FindSelector (mapping.Selector) is not { } selector)
                    continue;

                var properties = entries.Where (e => e.Selector == mapping.Selector && e.Part is null && e.State == ThemeCssState.Normal).Select (e => e.Property).ToArray ();

                foreach (var state in new[] { ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus, ThemeCssState.FocusVisible })
                    if (selector.Supports (state)) {
                        var answered = entries.Where (e => e.Selector == mapping.Selector && e.Part is null && e.State == state).Select (e => e.Property).ToHashSet ();
                        var note = answered.Count > 0 ? "Only colours change per state; the rest comes from the normal rule." : NoStateSeam;

                        foreach (var property in properties)
                            if (!answered.Contains (property)) {
                                var isEffect = property == "box-shadow" || property == "background-image" || property.StartsWith ("outline-", StringComparison.Ordinal);
                                var row = isEffect ? EffectRow (mapping.Selector, property, true)
                                    : transition_properties.Contains (property) ? MotionRow (mapping.Selector)
                                    : (Level: WinFormsThemeSupportLevel.Unsupported, Note: note, Gate: WinFormsThemeGate.None);
                                entries.Add (new WinFormsThemeSupportEntry (mapping.Selector, null, state, property, row.Level, row.Note, row.Gate));
                            }
                    }
            }

            Mappings = mappings;
            Entries = entries;

            index = entries.ToDictionary (e => (e.Selector.ToLowerInvariant (), e.Part?.ToLowerInvariant (), e.State, e.Property.ToLowerInvariant ()));
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
            => $"{e.Selector}{(e.Part is null ? "" : "::" + e.Part)}{WinFormsThemeSupportEntry.StateText (e.State)}";

        private static string LevelText (WinFormsThemeSupportLevel level) => level switch {
            WinFormsThemeSupportLevel.Native => "native",
            WinFormsThemeSupportLevel.Approximate => "approximate",
            _ => "unsupported"
        };

        private static string Escape (string text) => text.Replace ("|", "\\|");
    }
}
