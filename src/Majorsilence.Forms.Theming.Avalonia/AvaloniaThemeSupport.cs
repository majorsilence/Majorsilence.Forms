using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Majorsilence.Forms.Theming.Avalonia
{
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Presenters;

    /// <summary>
    /// How faithfully <see cref="AvaloniaCssTheme"/> can express one (selector, property) pair on native
    /// Avalonia controls.
    /// </summary>
    public enum AvaloniaThemeSupportLevel
    {
        /// <summary>The property maps to a Fluent theme resource or a control property and applies exactly.</summary>
        Native,

        /// <summary>The property applies, but not exactly as on Majorsilence.Forms (e.g. a shared resource, one brush for every border side).</summary>
        Approximate,

        /// <summary>Avalonia's Fluent theme has no seam for it; the declaration is reported as a diagnostic and skipped.</summary>
        Unsupported
    }

    /// <summary>
    /// One row of the support matrix: what happens to <c>Selector[::Part][:hover] { Property }</c>
    /// when applied to native Avalonia controls.
    /// </summary>
    public sealed class AvaloniaThemeSupportEntry
    {
        internal AvaloniaThemeSupportEntry (string selector, string? part, bool hover, string property, AvaloniaThemeSupportLevel level, string notes, IReadOnlyList<AvaloniaTarget> targets)
        {
            Selector = selector;
            Part = part;
            Hover = hover;
            Property = property;
            Level = level;
            Notes = notes;
            Targets = targets;
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
        public AvaloniaThemeSupportLevel Level { get; }

        /// <summary>What it maps to, or why it cannot.</summary>
        public string Notes { get; }

        /// <summary>The Fluent theme resource keys the declaration writes (empty when it is a style setter or unsupported).</summary>
        public IReadOnlyList<string> ResourceKeys => Targets.OfType<ResourceTarget> ().Select (t => t.Key).ToList ();

        /// <summary>The style selectors (Avalonia syntax) the declaration sets a property on.</summary>
        public IReadOnlyList<string> StyleSelectors => Targets.OfType<StyleTarget> ().Select (t => $"{t.Selector.Key} {{ {t.Property.Name} }}").ToList ();

        internal IReadOnlyList<AvaloniaTarget> Targets { get; }

        /// <inheritdoc/>
        public override string ToString ()
            => $"{Selector}{(Part is null ? "" : "::" + Part)}{(Hover ? ":hover" : "")} {{ {Property} }} -> {Level}";
    }

    /// <summary>Where a CSS selector lands in Avalonia, or why it cannot.</summary>
    public sealed class AvaloniaThemeSelectorMapping
    {
        internal AvaloniaThemeSelectorMapping (string selector, string? avaloniaTypes, string notes)
        {
            Selector = selector;
            AvaloniaTypes = avaloniaTypes;
            Notes = notes;
        }

        /// <summary>The CSS selector (the Majorsilence.Forms control type name).</summary>
        public string Selector { get; }

        /// <summary>The Avalonia control type(s) the rule applies to, or null when Avalonia has no counterpart.</summary>
        public string? AvaloniaTypes { get; }

        /// <summary>How the mapping works, or why there is none.</summary>
        public string Notes { get; }

        /// <inheritdoc/>
        public override string ToString () => $"{Selector} -> {AvaloniaTypes ?? "(no Avalonia counterpart)"}";
    }

    /// <summary>
    /// The property × control support matrix of <see cref="AvaloniaCssTheme"/>: for every selector the
    /// stylesheet grammar accepts, where it lands in Avalonia (a Fluent theme resource or a style
    /// setter) and how faithfully each property applies. <c>docs/theming-avalonia.md</c> is generated
    /// from it (a test keeps the two in sync), the applier is driven by it, and the Approximate /
    /// Unsupported rows surface as diagnostics — never a silent no-op.
    /// </summary>
    public static class AvaloniaThemeSupport
    {
        private static readonly string[] font_properties = { "font-family", "font-size", "font-weight", "font-style" };

        private static readonly string[] side_widths = { "border-top-width", "border-right-width", "border-bottom-width", "border-left-width" };

        private static readonly string[] side_colors = { "border-top-color", "border-right-color", "border-bottom-color", "border-left-color" };

        // Every longhand a control rule can carry ('border' expands to border-width/border-color during
        // parsing, so it never reaches an applier).
        private static readonly string[] all_properties =
            new[] { "background-color", "color", "border-width", "border-color", "border-radius" }
                .Concat (side_widths)
                .Concat (side_colors)
                .Concat (font_properties)
                .ToArray ();

        private static readonly string[] box_properties =
            new[] { "border-width", "border-radius" }.Concat (side_widths).Concat (font_properties).ToArray ();

        /// <summary>Every selector, with the Avalonia type(s) it applies to (null = no counterpart).</summary>
        public static IReadOnlyList<AvaloniaThemeSelectorMapping> Mappings { get; }

        /// <summary>Every (selector, part, hover, property) row.</summary>
        public static IReadOnlyList<AvaloniaThemeSupportEntry> Entries { get; }

        private static readonly Dictionary<(string Selector, string? Part, bool Hover, string Property), AvaloniaThemeSupportEntry> index;
        private static readonly Dictionary<string, AvaloniaThemeSelectorMapping> mapping_index;

        /// <summary>
        /// Looks up the row for one declaration. Returns null when the selector has no Avalonia
        /// counterpart at all (see <see cref="FindMapping"/>).
        /// </summary>
        public static AvaloniaThemeSupportEntry? Find (string selector, string? part, bool hover, string property)
        {
            index.TryGetValue ((selector.ToLowerInvariant (), part?.ToLowerInvariant (), hover, property.ToLowerInvariant ()), out var entry);
            return entry;
        }

        /// <summary>Looks up where a selector lands in Avalonia, or null for a selector the grammar does not know.</summary>
        public static AvaloniaThemeSelectorMapping? FindMapping (string selector)
        {
            mapping_index.TryGetValue (selector.ToLowerInvariant (), out var mapping);
            return mapping;
        }

        static AvaloniaThemeSupport ()
        {
            var mappings = new List<AvaloniaThemeSelectorMapping> ();
            var entries = new List<AvaloniaThemeSupportEntry> ();

            void Map (string selector, string? types, string notes)
                => mappings.Add (new AvaloniaThemeSelectorMapping (selector, types, notes));

            bool Has (string selector, string? part, bool hover, string property)
                => entries.Any (e => e.Selector == selector && e.Part == part && e.Hover == hover && e.Property == property);

            void Add (string selector, string? part, bool hover, string property, AvaloniaThemeSupportLevel level, string notes, IReadOnlyList<AvaloniaTarget> targets)
            {
                if (!Has (selector, part, hover, property))
                    entries.Add (new AvaloniaThemeSupportEntry (selector, part, hover, property, level, notes, targets));
            }

            // A Fluent theme resource (or several) the declaration's colour is written to.
            void Res (string selector, string? part, bool hover, string property, AvaloniaThemeSupportLevel level, string notes, params string[] keys)
                => Add (selector, part, hover, property, level, notes, Targets.Brushes (keys));

            void Target (string selector, string? part, bool hover, string property, AvaloniaThemeSupportLevel level, string notes, params AvaloniaTarget[] targets)
                => Add (selector, part, hover, property, level, notes, targets);

            // Style setters for the listed longhands on one or more selectors. Per-side colours become
            // the one BorderBrush Avalonia has (approximate); per-side widths build a Thickness.
            void Styled (string selector, string? part, bool hover, IEnumerable<string> properties, string notes, params StyleSelector[] targets)
            {
                foreach (var property in properties) {
                    var side = P.SideOf (property);
                    var isSideColor = property.StartsWith ("border-", StringComparison.Ordinal) && property.EndsWith ("-color", StringComparison.Ordinal) && side != BorderSide.All;
                    var avaloniaProperty = isSideColor ? P.BorderBrush : P.For (property)!;
                    var list = targets.Select (t => (AvaloniaTarget) new StyleTarget (t, avaloniaProperty, side)).ToList ();
                    var level = isSideColor ? AvaloniaThemeSupportLevel.Approximate : AvaloniaThemeSupportLevel.Native;
                    var rowNotes = isSideColor ? "Avalonia controls have one BorderBrush; a side colour colours every side that has a width." : notes;
                    Add (selector, part, hover, property, level, rowNotes, list);
                }
            }

            // Everything not answered yet for (selector, part, hover) is unsupported, with one reason.
            void Fill (string selector, string? part, bool hover, string notes)
            {
                var properties = part is null
                    ? all_properties
                    : ThemeCssReference.FindSelector (selector)!.FindPart (part)!.Properties.ToArray ();

                foreach (var property in properties)
                    Add (selector, part, hover, property, AvaloniaThemeSupportLevel.Unsupported, notes, Array.Empty<AvaloniaTarget> ());
            }

            const string StyleNote = "A setter in the generated Styles (application styles outrank the Fluent control theme).";
            const string HoverColoursOnly = "Fluent's pointer-over state only swaps colours; the rest comes from the normal rule.";

            // ---- Buttons -----------------------------------------------------------------------------
            Map ("Button", "Button, ToggleButton", "Colours go to the Fluent Button*/ToggleButton* resources (so every state follows); geometry and fonts are style setters.");
            Res ("Button", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "ButtonBackground / ToggleButtonBackground.", "ButtonBackground", "ToggleButtonBackground");
            Res ("Button", null, false, "color", AvaloniaThemeSupportLevel.Native, "ButtonForeground / ToggleButtonForeground.", "ButtonForeground", "ToggleButtonForeground");
            Res ("Button", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "ButtonBorderBrush / ToggleButtonBorderBrush.", "ButtonBorderBrush", "ToggleButtonBorderBrush");
            Styled ("Button", null, false, box_properties.Concat (side_colors), StyleNote, S.Button, S.ToggleButton);
            Res ("Button", null, true, "background-color", AvaloniaThemeSupportLevel.Native, "The pointer-over fill; the pressed fill is the same colour, slightly darker.",
                "ButtonBackgroundPointerOver", "ButtonBackgroundPressed", "ToggleButtonBackgroundPointerOver", "ToggleButtonBackgroundPressed");
            Res ("Button", null, true, "color", AvaloniaThemeSupportLevel.Native, "The pointer-over and pressed text colour.",
                "ButtonForegroundPointerOver", "ButtonForegroundPressed", "ToggleButtonForegroundPointerOver", "ToggleButtonForegroundPressed");
            Res ("Button", null, true, "border-color", AvaloniaThemeSupportLevel.Native, "The pointer-over and pressed border.",
                "ButtonBorderBrushPointerOver", "ButtonBorderBrushPressed", "ToggleButtonBorderBrushPointerOver", "ToggleButtonBorderBrushPressed");
            Fill ("Button", null, true, HoverColoursOnly);

            Map ("CheckBox", "CheckBox", "Text colour and backdrop through the CheckBox* resources; the box glyph follows --accent-color.");
            Res ("CheckBox", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "The backdrop behind the box and caption, in every check state.",
                "CheckBoxBackgroundUnchecked", "CheckBoxBackgroundChecked", "CheckBoxBackgroundIndeterminate");
            Res ("CheckBox", null, false, "color", AvaloniaThemeSupportLevel.Native, "The caption, in every check state (pointer-over included).",
                "CheckBoxForegroundUnchecked", "CheckBoxForegroundChecked", "CheckBoxForegroundIndeterminate",
                "CheckBoxForegroundUncheckedPointerOver", "CheckBoxForegroundCheckedPointerOver", "CheckBoxForegroundIndeterminatePointerOver");
            Res ("CheckBox", null, false, "border-color", AvaloniaThemeSupportLevel.Approximate, "The outline of the unchecked box (the checked box is filled with the accent).",
                "CheckBoxCheckBackgroundStrokeUnchecked");
            Styled ("CheckBox", null, false, font_properties, StyleNote, S.CheckBox);
            Fill ("CheckBox", null, false, "The box glyph has a fixed Fluent geometry.");

            Map ("RadioButton", "RadioButton", "Text colour, backdrop and the ring outline through the RadioButton* resources.");
            Res ("RadioButton", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "The backdrop behind the ring and caption.", "RadioButtonBackground");
            Res ("RadioButton", null, false, "color", AvaloniaThemeSupportLevel.Native, "The caption (pointer-over included).", "RadioButtonForeground", "RadioButtonForegroundPointerOver");
            Res ("RadioButton", null, false, "border-color", AvaloniaThemeSupportLevel.Approximate, "The outer ring's stroke.", "RadioButtonOuterEllipseStroke");
            Styled ("RadioButton", null, false, font_properties, StyleNote, S.RadioButton);
            Fill ("RadioButton", null, false, "The ring glyph has a fixed Fluent geometry.");

            Map ("LinkLabel", "HyperlinkButton", "The HyperlinkButton* resources; :hover is pointer-over (and pressed).");
            Res ("LinkLabel", null, false, "color", AvaloniaThemeSupportLevel.Native, "HyperlinkButtonForeground.", "HyperlinkButtonForeground");
            Res ("LinkLabel", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "HyperlinkButtonBackground.", "HyperlinkButtonBackground");
            Res ("LinkLabel", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "HyperlinkButtonBorderBrush.", "HyperlinkButtonBorderBrush");
            Styled ("LinkLabel", null, false, box_properties.Concat (side_colors), StyleNote, S.Hyperlink);
            Res ("LinkLabel", null, true, "color", AvaloniaThemeSupportLevel.Native, "The pointer-over and pressed link colour.", "HyperlinkButtonForegroundPointerOver", "HyperlinkButtonForegroundPressed");
            Res ("LinkLabel", null, true, "background-color", AvaloniaThemeSupportLevel.Native, "The pointer-over and pressed fill.", "HyperlinkButtonBackgroundPointerOver", "HyperlinkButtonBackgroundPressed");
            Res ("LinkLabel", null, true, "border-color", AvaloniaThemeSupportLevel.Native, "The pointer-over and pressed border.", "HyperlinkButtonBorderBrushPointerOver", "HyperlinkButtonBorderBrushPressed");
            Fill ("LinkLabel", null, true, HoverColoursOnly);

            // ---- Text inputs -------------------------------------------------------------------------
            Map ("TextBox", "TextBox, CalendarDatePicker", "Colours go to the TextControl* (and CalendarDatePicker*) resources, so NumericUpDown's and ComboBox's inner editors follow too; the focused state keeps Fluent's accent border.");
            Res ("TextBox", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "The resting fill (focused keeps Fluent's).", "TextControlBackground", "CalendarDatePickerBackground");
            Res ("TextBox", null, false, "color", AvaloniaThemeSupportLevel.Native, "The text, at rest, pointer-over and focused.",
                "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused", "CalendarDatePickerForeground", "CalendarDatePickerTextForeground");
            Res ("TextBox", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "The resting border (focused keeps the accent).", "TextControlBorderBrush", "CalendarDatePickerBorderBrush");
            Styled ("TextBox", null, false, box_properties.Concat (side_colors), StyleNote, S.TextBox, S.DatePicker);

            Map ("ComboBox", "ComboBox", "The closed box through the ComboBox* resources; the open list follows the ListBox rules.");
            Res ("ComboBox", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "ComboBoxBackground.", "ComboBoxBackground");
            Res ("ComboBox", null, false, "color", AvaloniaThemeSupportLevel.Native, "ComboBoxForeground.", "ComboBoxForeground");
            Res ("ComboBox", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "ComboBoxBorderBrush.", "ComboBoxBorderBrush");
            Styled ("ComboBox", null, false, box_properties.Concat (side_colors), StyleNote, S.ComboBox);

            Map ("NumericUpDown", "NumericUpDown", "Style setters on the spinner; its inner text editor also follows the TextBox rule.");
            Styled ("NumericUpDown", null, false, all_properties.Where (p => p is "background-color" or "color" or "border-color").Concat (box_properties).Concat (side_colors), StyleNote, S.NumericUpDown);

            Map ("Label", "Label", "Style setters on Avalonia's Label (TextBlock is left alone: it is the text inside every control).");
            Styled ("Label", null, false, all_properties, StyleNote, S.Label);

            // ---- Lists and trees ---------------------------------------------------------------------
            Map ("ListBox", "ListBox", "The control is style setters; ::selection writes Fluent's shared list-accent resources.");
            Styled ("ListBox", null, false, all_properties, StyleNote, S.ListBox);
            Res ("ListBox", "selection", false, "background-color", AvaloniaThemeSupportLevel.Approximate,
                "SystemControlHighlightListAccent{Low,Medium,High}Brush: selected, selected + pointer-over, selected + pressed. Fluent shares these with other list-style controls.",
                "SystemControlHighlightListAccentLowBrush", "SystemControlHighlightListAccentMediumBrush", "SystemControlHighlightListAccentHighBrush");
            Target ("ListBox", "selection", false, "color", AvaloniaThemeSupportLevel.Native, "The selected item's content presenter.",
                new StyleTarget (S.ListBoxItemSelected, ContentPresenter.ForegroundProperty));

            Map ("ListView", null, "Avalonia has no ListView; use ListBox (the ListBox rules) or DataGrid (the DataGridView rules).");

            Map ("TreeView", "TreeView", "The control is style setters; ::selection writes the TreeViewItem*Selected resources.");
            Styled ("TreeView", null, false, all_properties, StyleNote, S.TreeView);
            Res ("TreeView", "selection", false, "background-color", AvaloniaThemeSupportLevel.Native, "Selected, selected + pointer-over and selected + pressed.",
                "TreeViewItemBackgroundSelected", "TreeViewItemBackgroundSelectedPointerOver", "TreeViewItemBackgroundSelectedPressed");
            Res ("TreeView", "selection", false, "color", AvaloniaThemeSupportLevel.Native, "Selected, selected + pointer-over and selected + pressed.",
                "TreeViewItemForegroundSelected", "TreeViewItemForegroundSelectedPointerOver", "TreeViewItemForegroundSelectedPressed");

            // ---- Data grid ---------------------------------------------------------------------------
            Map ("DataGridView", "DataGrid (Avalonia.Controls.DataGrid)", "The grid is style setters; headers, rows and selection are the DataGrid* resources and row styles.");
            Styled ("DataGridView", null, false, all_properties, StyleNote, S.DataGrid);
            Res ("DataGridView", "header", false, "background-color", AvaloniaThemeSupportLevel.Native, "DataGridColumnHeaderBackgroundBrush.", "DataGridColumnHeaderBackgroundBrush");
            Res ("DataGridView", "header", false, "color", AvaloniaThemeSupportLevel.Native, "DataGridColumnHeaderForegroundBrush.", "DataGridColumnHeaderForegroundBrush");
            Styled ("DataGridView", "header", false, font_properties, StyleNote, S.DataGridColumnHeader);
            Fill ("DataGridView", "header", false, "The header separators come from the grid-lines brush, which is shared with the cells.");
            Styled ("DataGridView", "row-header", false, new[] { "background-color", "color" }, StyleNote, S.DataGridRowHeader);
            Fill ("DataGridView", "row-header", false, "The row-header separator has no brush of its own.");
            Target ("DataGridView", "selection", false, "background-color", AvaloniaThemeSupportLevel.Native,
                "The selected row, focused or not and with or without the pointer over it (the Fluent selection opacities are set to 1 so the colour is exact).",
                new ResourceTarget ("DataGridRowSelectedBackgroundBrush", ResourceKind.Brush),
                new ResourceTarget ("DataGridRowSelectedUnfocusedBackgroundBrush", ResourceKind.Brush),
                new ResourceTarget ("DataGridRowSelectedHoveredBackgroundBrush", ResourceKind.Brush),
                new ResourceTarget ("DataGridRowSelectedHoveredUnfocusedBackgroundBrush", ResourceKind.Brush),
                new ResourceTarget ("DataGridRowSelectedBackgroundOpacity", ResourceKind.Double, fixedValue: 1.0),
                new ResourceTarget ("DataGridRowSelectedUnfocusedBackgroundOpacity", ResourceKind.Double, fixedValue: 1.0),
                new ResourceTarget ("DataGridRowSelectedHoveredBackgroundOpacity", ResourceKind.Double, fixedValue: 1.0),
                new ResourceTarget ("DataGridRowSelectedHoveredUnfocusedBackgroundOpacity", ResourceKind.Double, fixedValue: 1.0));
            Target ("DataGridView", "selection", false, "color", AvaloniaThemeSupportLevel.Native, "The selected row's text.",
                new StyleTarget (S.DataGridRowSelected, P.Foreground));
            Res ("DataGridView", "selection", false, "border-color", AvaloniaThemeSupportLevel.Approximate, "The current cell's focus outline.", "DataGridCellFocusVisualPrimaryBrush");
            Fill ("DataGridView", "selection", false, "The focus outline has a fixed width.");
            Target ("DataGridView", "alternating-row", false, "background-color", AvaloniaThemeSupportLevel.Native, "Every second row (DataGridRow:nth-child(2n)).",
                new StyleTarget (S.DataGridRowAlternate, P.Background));

            // ---- Menus -------------------------------------------------------------------------------
            Map ("Menu", "Menu (and its top-level MenuItems)", "The bar is style setters; ::item targets the top-level MenuItems, :hover their pointer-over template parts.");
            Styled ("Menu", null, false, all_properties.Where (p => p != "color"), StyleNote, S.Menu);
            Target ("Menu", null, false, "color", AvaloniaThemeSupportLevel.Native, "The top-level items' text (a MenuItem does not inherit the bar's Foreground).",
                new StyleTarget (S.MenuBarItem, P.Foreground));
            Target ("Menu", "item", false, "background-color", AvaloniaThemeSupportLevel.Native, "Top-level MenuItem background.", new StyleTarget (S.MenuBarItem, P.Background));
            Target ("Menu", "item", false, "color", AvaloniaThemeSupportLevel.Native, "Top-level MenuItem text.", new StyleTarget (S.MenuBarItem, P.Foreground));
            Target ("Menu", "item", true, "background-color", AvaloniaThemeSupportLevel.Native, "The pointer-over item's template root.",
                new StyleTarget (S.MenuBarItemHover, Border.BackgroundProperty));
            Target ("Menu", "item", true, "color", AvaloniaThemeSupportLevel.Native, "The pointer-over item's header presenter.",
                new StyleTarget (S.MenuBarItemHoverText, ContentPresenter.ForegroundProperty));

            Map ("MenuDropDown", "MenuFlyoutPresenter, ContextMenu (and their MenuItems)", "The MenuFlyoutPresenter* and MenuFlyoutItem* resources, shared by drop-downs and context menus.");
            Res ("MenuDropDown", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "MenuFlyoutPresenterBackground.", "MenuFlyoutPresenterBackground");
            Res ("MenuDropDown", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "MenuFlyoutPresenterBorderBrush.", "MenuFlyoutPresenterBorderBrush");
            Res ("MenuDropDown", null, false, "color", AvaloniaThemeSupportLevel.Approximate, "The items' text (MenuFlyoutItemForeground); the presenter itself draws no text.", "MenuFlyoutItemForeground");
            Styled ("MenuDropDown", null, false, box_properties.Concat (side_colors), StyleNote, S.MenuFlyout, S.ContextMenu);
            Res ("MenuDropDown", "item", false, "background-color", AvaloniaThemeSupportLevel.Native, "MenuFlyoutItemBackground.", "MenuFlyoutItemBackground");
            Res ("MenuDropDown", "item", false, "color", AvaloniaThemeSupportLevel.Native, "MenuFlyoutItemForeground.", "MenuFlyoutItemForeground");
            Res ("MenuDropDown", "item", true, "background-color", AvaloniaThemeSupportLevel.Native, "Pointer-over and pressed (slightly darker).", "MenuFlyoutItemBackgroundPointerOver", "MenuFlyoutItemBackgroundPressed");
            Res ("MenuDropDown", "item", true, "color", AvaloniaThemeSupportLevel.Native, "Pointer-over and pressed.", "MenuFlyoutItemForegroundPointerOver", "MenuFlyoutItemForegroundPressed");

            // ---- Tabs --------------------------------------------------------------------------------
            Map ("TabControl", "TabControl", "Style setters on the TabControl (the frame around the pages).");
            Styled ("TabControl", null, false, all_properties, StyleNote, S.TabControl);

            Map ("TabStrip", "TabItem headers (TabControl)", "The TabItemHeader* resources; Avalonia's tab row has no brush of its own, so the strip's own colours apply to the unselected tabs.");
            Res ("TabStrip", null, false, "background-color", AvaloniaThemeSupportLevel.Approximate, "Fills the unselected tabs (there is no header band to fill).", "TabItemHeaderBackgroundUnselected");
            Res ("TabStrip", null, false, "color", AvaloniaThemeSupportLevel.Approximate, "The unselected tabs' captions.", "TabItemHeaderForegroundUnselected");
            Styled ("TabStrip", null, false, font_properties, StyleNote, S.TabItem);
            Fill ("TabStrip", null, false, "Avalonia's tab row is a plain items panel with no border or background of its own.");
            Res ("TabStrip", "item", false, "background-color", AvaloniaThemeSupportLevel.Native, "TabItemHeaderBackgroundUnselected.", "TabItemHeaderBackgroundUnselected");
            Res ("TabStrip", "item", false, "color", AvaloniaThemeSupportLevel.Native, "TabItemHeaderForegroundUnselected.", "TabItemHeaderForegroundUnselected");
            Res ("TabStrip", "item", true, "background-color", AvaloniaThemeSupportLevel.Native, "Unselected pointer-over and pressed.", "TabItemHeaderBackgroundUnselectedPointerOver", "TabItemHeaderBackgroundUnselectedPressed");
            Res ("TabStrip", "item", true, "color", AvaloniaThemeSupportLevel.Native, "Unselected pointer-over and pressed.", "TabItemHeaderForegroundUnselectedPointerOver", "TabItemHeaderForegroundUnselectedPressed");
            Res ("TabStrip", "selected", false, "background-color", AvaloniaThemeSupportLevel.Native, "Selected, including pointer-over and pressed.",
                "TabItemHeaderBackgroundSelected", "TabItemHeaderBackgroundSelectedPointerOver", "TabItemHeaderBackgroundSelectedPressed");
            Res ("TabStrip", "selected", false, "color", AvaloniaThemeSupportLevel.Native, "Selected, including pointer-over and pressed.",
                "TabItemHeaderForegroundSelected", "TabItemHeaderForegroundSelectedPointerOver", "TabItemHeaderForegroundSelectedPressed");
            Res ("TabStrip", "selected", false, "border-bottom-color", AvaloniaThemeSupportLevel.Native, "The selection pipe (TabItemHeaderSelectedPipeFill).", "TabItemHeaderSelectedPipeFill");
            Target ("TabStrip", "selected", false, "border-bottom-width", AvaloniaThemeSupportLevel.Native, "The selection pipe's thickness (TabItemPipeThickness).",
                new ResourceTarget ("TabItemPipeThickness", ResourceKind.Double));

            // ---- Scroll bars, sliders, splitters, calendar -------------------------------------------
            Map ("ScrollBar", "ScrollBar", "The ScrollBar* resources: track, thumb and line buttons.");
            Res ("ScrollBar", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "The track, at rest and pointer-over.", "ScrollBarTrackFill", "ScrollBarTrackFillPointerOver");
            Res ("ScrollBar", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "The track outline, at rest and pointer-over.", "ScrollBarTrackStroke", "ScrollBarTrackStrokePointerOver");
            Fill ("ScrollBar", null, false, "Fluent scroll bars have a fixed geometry and no text.");
            Target ("ScrollBar", "thumb", false, "background-color", AvaloniaThemeSupportLevel.Native, "The thumb, at rest, pointer-over and pressed (slightly darker).",
                new ResourceTarget ("ScrollBarThumbBackgroundColor", ResourceKind.Color),
                new ResourceTarget ("ScrollBarThumbFillPointerOver", ResourceKind.Brush),
                new ResourceTarget ("ScrollBarThumbFillPressed", ResourceKind.Brush, pressed: true));
            Fill ("ScrollBar", "thumb", false, "The Fluent thumb has no outline and a fixed shape.");
            Res ("ScrollBar", "arrow", false, "background-color", AvaloniaThemeSupportLevel.Native, "The line buttons, at rest, pointer-over and pressed.",
                "ScrollBarButtonBackground", "ScrollBarButtonBackgroundPointerOver", "ScrollBarButtonBackgroundPressed");
            Res ("ScrollBar", "arrow", false, "color", AvaloniaThemeSupportLevel.Native, "The arrow glyphs, at rest, pointer-over and pressed.",
                "ScrollBarButtonArrowForeground", "ScrollBarButtonArrowForegroundPointerOver", "ScrollBarButtonArrowForegroundPressed");
            Res ("ScrollBar", "arrow", false, "border-color", AvaloniaThemeSupportLevel.Native, "The line buttons' outline.", "ScrollBarButtonBorderBrush");

            Map ("TrackBar", "Slider", "The slider's container through SliderContainerBackground; the track and thumb follow --accent-color.");
            Res ("TrackBar", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "SliderContainerBackground.", "SliderContainerBackground");
            Fill ("TrackBar", null, false, "The track and thumb are drawn from the accent and fixed Fluent geometry.");
            Res ("TrackBar", null, true, "background-color", AvaloniaThemeSupportLevel.Native, "SliderContainerBackgroundPointerOver.", "SliderContainerBackgroundPointerOver");
            Fill ("TrackBar", null, true, HoverColoursOnly);

            Map ("SplitContainer", "GridSplitter", "The splitter bar's background.");
            Styled ("SplitContainer", null, false, new[] { "background-color" }, StyleNote, S.GridSplitter);
            Fill ("SplitContainer", null, false, "A GridSplitter is a plain bar.");
            Map ("Splitter", "GridSplitter", "The splitter bar's background.");
            Styled ("Splitter", null, false, new[] { "background-color" }, StyleNote, S.GridSplitter);
            Fill ("Splitter", null, false, "A GridSplitter is a plain bar.");

            Map ("MonthCalendar", "Calendar", "The CalendarView* resources; the selected day uses --accent-color.");
            Res ("MonthCalendar", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "CalendarViewBackground.", "CalendarViewBackground");
            Res ("MonthCalendar", null, false, "color", AvaloniaThemeSupportLevel.Native, "CalendarViewForeground.", "CalendarViewForeground");
            Res ("MonthCalendar", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "CalendarViewBorderBrush.", "CalendarViewBorderBrush");
            Styled ("MonthCalendar", null, false, font_properties, StyleNote, S.Calendar);
            Fill ("MonthCalendar", null, false, "The calendar grid has a fixed Fluent geometry.");

            // ---- Windows -----------------------------------------------------------------------------
            Map ("Form", "Window", "Style setters on every Window; Foreground and the font properties are inherited by the controls inside.");
            Styled ("Form", null, false, new[] { "background-color", "color" }.Concat (font_properties), StyleNote, S.Window);
            Fill ("Form", null, false, "The window frame is drawn by the OS (or Avalonia's own chrome, which has no border seam).");

            // ---- Selectors with no Avalonia counterpart ----------------------------------------------
            Map ("GroupBox", null, "Avalonia has no GroupBox; a HeaderedContentControl or a Border with a header has no Fluent theme to restyle.");
            Map ("NavigationPane", null, "Avalonia has no Outlook-style navigation pane.");
            Map ("Panel", null, "Avalonia panels are layout primitives used inside every control template; restyling them would restyle the controls' internals.");
            Map ("PictureBox", null, "Avalonia's Image has no background or border.");
            Map ("PropertyGrid", null, "Avalonia has no PropertyGrid.");
            Map ("Ribbon", null, "Avalonia has no ribbon control.");
            Map ("StatusBar", null, "Avalonia has no status bar control; bind your own status row to the published token resources instead.");
            Map ("ToolBar", null, "Avalonia has no tool bar control; bind your own tool row to the published token resources instead.");

            // Anything the explicit rows above left open on a mapped selector is unsupported.
            foreach (var selector in ThemeCssReference.Selectors) {
                var mapping = mappings.FirstOrDefault (m => m.Selector == selector.Name);
                if (mapping?.AvaloniaTypes is null)
                    continue;

                Fill (selector.Name, null, false, "No Avalonia seam for this property on this control.");
                if (selector.SupportsHover)
                    Fill (selector.Name, null, true, HoverColoursOnly);

                foreach (var part in selector.Parts) {
                    Fill (selector.Name, part.Name, false, "No Avalonia seam for this property on this part.");
                    if (part.SupportsHover)
                        Fill (selector.Name, part.Name, true, HoverColoursOnly);
                }
            }

            Mappings = mappings;
            Entries = entries;

            index = entries.ToDictionary (e => (e.Selector.ToLowerInvariant (), e.Part?.ToLowerInvariant (), e.Hover, e.Property.ToLowerInvariant ()));
            mapping_index = mappings.ToDictionary (m => m.Selector.ToLowerInvariant ());
        }

        /// <summary>
        /// Renders the matrix as the Markdown tables committed in <c>docs/theming-avalonia.md</c> (a test
        /// keeps the two in sync). Paste it into a prompt to let a coding assistant predict what a
        /// stylesheet will do to an Avalonia app.
        /// </summary>
        public static string ToMarkdown ()
        {
            var sb = new StringBuilder ();

            sb.AppendLine ("### Selector mapping");
            sb.AppendLine ();
            sb.AppendLine ("| Selector | Avalonia | Notes |");
            sb.AppendLine ("|---|---|---|");
            foreach (var m in Mappings.OrderBy (m => m.Selector, StringComparer.Ordinal))
                sb.AppendLine ($"| `{m.Selector}` | {(m.AvaloniaTypes is null ? "—" : $"`{m.AvaloniaTypes}`")} | {Escape (m.Notes)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Property support");
            sb.AppendLine ();
            sb.AppendLine ("Unsupported rows are reported as diagnostics when a stylesheet uses them — never silently ignored.");
            sb.AppendLine ();
            sb.AppendLine ("| Rule | Property | Support | Writes | Notes |");
            sb.AppendLine ("|---|---|---|---|---|");
            foreach (var e in Entries.Where (e => e.Level != AvaloniaThemeSupportLevel.Unsupported))
                sb.AppendLine ($"| `{RuleText (e)}` | `{e.Property}` | {LevelText (e.Level)} | {Escape (WritesText (e))} | {Escape (e.Notes)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Unsupported (reported, then skipped)");
            sb.AppendLine ();
            sb.AppendLine ("| Rule | Property | Why |");
            sb.AppendLine ("|---|---|---|");
            foreach (var group in Entries.Where (e => e.Level == AvaloniaThemeSupportLevel.Unsupported)
                         .GroupBy (e => (RuleText (e), e.Notes)))
                sb.AppendLine ($"| `{group.Key.Item1}` | {string.Join (", ", group.Select (e => $"`{e.Property}`"))} | {Escape (group.Key.Notes)} |");

            return sb.ToString ();
        }

        private static string WritesText (AvaloniaThemeSupportEntry e)
        {
            var parts = e.ResourceKeys.Select (k => $"`{k}`").Concat (e.StyleSelectors.Select (s => $"`{s}`"));
            return string.Join ("<br>", parts);
        }

        private static string RuleText (AvaloniaThemeSupportEntry e)
            => $"{e.Selector}{(e.Part is null ? "" : "::" + e.Part)}{(e.Hover ? ":hover" : "")}";

        private static string LevelText (AvaloniaThemeSupportLevel level) => level switch {
            AvaloniaThemeSupportLevel.Native => "native",
            AvaloniaThemeSupportLevel.Approximate => "approximate",
            _ => "unsupported"
        };

        private static string Escape (string text) => text.Replace ("|", "\\|");
    }
}
