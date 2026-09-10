using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Majorsilence.Forms
{
    /// <summary>
    /// The kind of value a theme token holds.
    /// </summary>
    public enum ThemeCssValueKind
    {
        /// <summary>A color: <c>#rrggbb</c>, <c>rgb()</c>, a CSS color name, ...</summary>
        Color,

        /// <summary>A whole number of pixels: <c>14px</c> or <c>14</c>.</summary>
        Length,

        /// <summary>A font-family list: <c>"Segoe UI", sans-serif</c>.</summary>
        FontFamily,

        /// <summary>A CSS font weight (100–900). Only appears on control-rule declarations, never on a token.</summary>
        FontWeight,

        /// <summary>A CSS font style keyword (normal, italic, oblique). Only appears on control-rule declarations.</summary>
        FontStyle
    }

    /// <summary>
    /// A theme-wide value that a CSS theme sets as a custom property on <c>:root</c>
    /// (e.g. <c>--accent-color: #2a8ad0;</c>). Each token is one static property of <see cref="Theme"/>;
    /// the CSS name is the kebab-case form of the property name.
    /// </summary>
    public sealed class ThemeCssToken
    {
        internal ThemeCssToken (string propertyName, ThemeCssValueKind kind, string description, string usedBy)
        {
            PropertyName = propertyName;
            Name = "--" + ThemeCssReference.ToKebabCase (propertyName);
            Kind = kind;
            Description = description;
            UsedBy = usedBy;
        }

        /// <summary>The custom-property name, e.g. <c>--accent-color</c>.</summary>
        public string Name { get; }

        /// <summary>The <see cref="Theme"/> property it sets, e.g. <c>AccentColor</c>.</summary>
        public string PropertyName { get; }

        /// <summary>What kind of value the token takes.</summary>
        public ThemeCssValueKind Kind { get; }

        /// <summary>What the value is for.</summary>
        public string Description { get; }

        /// <summary>Which controls read the token, so a designer can predict what a change will move.</summary>
        public string UsedBy { get; }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>
    /// A control type a CSS theme can target with a type selector (e.g. <c>Button { ... }</c>). The
    /// rule sets that type's static default <see cref="ControlStyle"/>, so every instance without its
    /// own explicit value picks it up.
    /// </summary>
    public sealed class ThemeCssSelector
    {
        internal ThemeCssSelector (string name, string description, Func<ControlStyle> getStyle, Func<ControlStyle>? getHoverStyle = null, params string[] alsoAppliesTo)
        {
            Name = name;
            Description = description;
            GetStyle = getStyle;
            GetHoverStyle = getHoverStyle;
            AlsoAppliesTo = alsoAppliesTo;
        }

        /// <summary>The selector text: the control's type name.</summary>
        public string Name { get; }

        /// <summary>What the control is, and anything a designer should know about how it paints.</summary>
        public string Description { get; }

        /// <summary>Whether <c>Name:hover</c> is meaningful -- only controls that repaint on hover.</summary>
        public bool SupportsHover => GetHoverStyle is not null;

        /// <summary>Derived controls that share this type's default style and therefore this rule.</summary>
        public IReadOnlyList<string> AlsoAppliesTo { get; }

        internal Func<ControlStyle> GetStyle { get; }
        internal Func<ControlStyle>? GetHoverStyle { get; }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>
    /// A CSS property accepted inside a control rule.
    /// </summary>
    public sealed class ThemeCssProperty
    {
        internal ThemeCssProperty (string name, string valueSyntax, string description)
        {
            Name = name;
            ValueSyntax = valueSyntax;
            Description = description;
        }

        /// <summary>The property name, e.g. <c>background-color</c>.</summary>
        public string Name { get; }

        /// <summary>The accepted value syntax, in prose.</summary>
        public string ValueSyntax { get; }

        /// <summary>What it changes on the control.</summary>
        public string Description { get; }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>
    /// The complete, machine-readable description of the CSS subset Majorsilence.Forms themes accept:
    /// every token, selector and property, with descriptions. <c>docs/theming.md</c> is generated from
    /// it (a test keeps the two in sync), the Theme Studio sample shows it, and
    /// <see cref="ToMarkdown"/> gives a coding assistant everything it needs to write a valid theme.
    /// </summary>
    public static class ThemeCssReference
    {
        /// <summary>The <c>:root</c> tokens, one per themable <see cref="Theme"/> property.</summary>
        public static IReadOnlyList<ThemeCssToken> Tokens { get; } = new[] {
            new ThemeCssToken (nameof (Theme.AccentColor), ThemeCssValueKind.Color,
                "The primary accent.",
                "Button hover background, LinkLabel text, selected DataGridView cells, MonthCalendar selection, TrackBar thumb, the custom title bar."),
            new ThemeCssToken (nameof (Theme.AccentColor2), ThemeCssValueKind.Color,
                "The secondary accent, used where the primary one needs a companion.",
                "Button hover border, Form border, ProgressBar fill, the selected TabStrip tab underline, StatusStrip, NavigationPane, TrackBar."),
            new ThemeCssToken (nameof (Theme.BackgroundColor), ThemeCssValueKind.Color,
                "The window background and the default background of every control that does not pin its own.",
                "Form, Panel and every ambient control; Menu, ToolBar and Ribbon items when the strip itself has no background rule."),
            new ThemeCssToken (nameof (Theme.BorderLowColor), ThemeCssValueKind.Color,
                "The everyday border colour.",
                "Default control borders (TextBox, ListBox, ComboBox, GroupBox, NumericUpDown), DataGridView grid lines, ScrollBar outlines, ListView lines, Ribbon."),
            new ThemeCssToken (nameof (Theme.BorderMidColor), ThemeCssValueKind.Color,
                "A stronger border colour.",
                "DataGridView header separators, ListView."),
            new ThemeCssToken (nameof (Theme.BorderHighColor), ThemeCssValueKind.Color,
                "The strongest border colour.",
                "DataGridView row headers, TrackBar ticks."),
            new ThemeCssToken (nameof (Theme.ControlLowColor), ThemeCssValueKind.Color,
                "The lightest control surface -- the 'paper' that lists and inputs sit on.",
                "ListBox, DataGridView, PropertyGrid and TreeView backgrounds, the selected TabStrip tab, ScrollBar arrows and thumb, MenuDropDown, NavigationPane."),
            new ThemeCssToken (nameof (Theme.ControlMidColor), ThemeCssValueKind.Color,
                "The default control surface.",
                "Button, ComboBox and NumericUpDown faces, alternating DataGridView rows, ListBox items, TrackBar groove."),
            new ThemeCssToken (nameof (Theme.ControlMidHighColor), ThemeCssValueKind.Color,
                "A slightly darker surface.",
                "ScrollBar track (ScrollBar background), TrackBar."),
            new ThemeCssToken (nameof (Theme.ControlHighColor), ThemeCssValueKind.Color,
                "A dark control surface. Not read by the built-in renderers; available to custom controls and VisualStyleRenderer.",
                "Custom controls."),
            new ThemeCssToken (nameof (Theme.ControlVeryHighColor), ThemeCssValueKind.Color,
                "The darkest control surface. Not read by the built-in renderers; available to custom controls.",
                "Custom controls."),
            new ThemeCssToken (nameof (Theme.ControlHighlightLowColor), ThemeCssValueKind.Color,
                "The hover highlight for items inside a control.",
                "Hovered Menu, ToolBar, Ribbon and MenuDropDown items, hovered ListBox/ListView rows, selected DataGridView rows, MonthCalendar hover."),
            new ThemeCssToken (nameof (Theme.ControlHighlightMidColor), ThemeCssValueKind.Color,
                "The pressed / selected item highlight.",
                "Selected Ribbon item, ScrollBar and NumericUpDown arrow glyphs, MonthCalendar."),
            new ThemeCssToken (nameof (Theme.ControlHighlightHighColor), ThemeCssValueKind.Color,
                "The strongest item highlight. Not read by the built-in renderers; available to custom controls.",
                "Custom controls."),
            new ThemeCssToken (nameof (Theme.ForegroundColor), ThemeCssValueKind.Color,
                "The default text colour.",
                "Every control's text unless a rule or the control sets its own; menu, toolbar, grid, tree and title bar text."),
            new ThemeCssToken (nameof (Theme.ForegroundColorOnAccent), ThemeCssValueKind.Color,
                "Text drawn on top of an accent-coloured surface. Keep it readable against --accent-color.",
                "Hovered Button text, the custom title bar caption, selected MonthCalendar day, DataGridView selection text."),
            new ThemeCssToken (nameof (Theme.ForegroundDisabledColor), ThemeCssValueKind.Color,
                "Text and glyphs of disabled controls and items.",
                "Every renderer, when the control or item is disabled; the ProgressBar fill when disabled."),
            new ThemeCssToken (nameof (Theme.TextSelectionBackgroundColor), ThemeCssValueKind.Color,
                "The highlight behind selected text.",
                "TextBox, NumericUpDown and other text editors."),
            new ThemeCssToken (nameof (Theme.WarningHighlightColor), ThemeCssValueKind.Color,
                "The colour of a destructive affordance.",
                "The custom title bar's Close button when hovered; PictureBox error state."),
            new ThemeCssToken (nameof (Theme.FontSize), ThemeCssValueKind.Length,
                "The pixel size of text drawn with the theme font.",
                "Menu, ToolBar, StatusStrip, MenuDropDown, TreeView, NumericUpDown, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-size }` rule."),
            new ThemeCssToken (nameof (Theme.ItemFontSize), ThemeCssValueKind.Length,
                "A smaller pixel size for dense item text.",
                "DataGridView cells, ListView items, Ribbon items."),
            new ThemeCssToken (nameof (Theme.UIFont), ThemeCssValueKind.FontFamily,
                "The theme font family (regular weight).",
                "Menu, ToolBar, StatusStrip, MenuDropDown, DataGridView, ListView, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-family }` rule."),
            new ThemeCssToken (nameof (Theme.UIFontBold), ThemeCssValueKind.FontFamily,
                "The theme font family used where text is bold. Resolved at bold weight.",
                "DataGridView column headers, MonthCalendar title, NavigationPane group headers."),
        };

        /// <summary>The control type selectors a theme can target.</summary>
        public static IReadOnlyList<ThemeCssSelector> Selectors { get; } = new[] {
            new ThemeCssSelector ("Button", "Push buttons. Hovering applies the :hover rule on top of the normal one.",
                () => Button.DefaultStyle, () => Button.DefaultStyleHover),
            new ThemeCssSelector ("CheckBox", "Check boxes: the text and the box glyph's surround.", () => CheckBox.DefaultStyle),
            new ThemeCssSelector ("ComboBox", "Drop-down selectors (the closed box; the open list is a ListBox).", () => ComboBox.DefaultStyle),
            new ThemeCssSelector ("DataGridView", "Data grids: the control background and border. Cells and headers follow the tokens (--control-low-color, --border-low-color, --accent-color).", () => DataGridView.DefaultStyle),
            new ThemeCssSelector ("Form", "The window. background-color is the window background; border sets the window frame on platforms that draw their own; font-family / font-size / color here become the ambient defaults every child control inherits when it sets none of its own.", () => Form.DefaultStyle),
            new ThemeCssSelector ("GroupBox", "Titled group frames: the border colour is the frame, color is the caption.", () => GroupBox.DefaultStyle),
            new ThemeCssSelector ("Label", "Static text.", () => Label.DefaultStyle),
            new ThemeCssSelector ("LinkLabel", "Hyperlink text; color is the link colour. Supports :hover.", () => LinkLabel.DefaultStyle, () => LinkLabel.DefaultStyleHover),
            new ThemeCssSelector ("ListBox", "Single-column lists (also the ComboBox drop-down list). Item highlight comes from --control-highlight-low-color.",
                () => ListBox.DefaultStyle, null, "CheckedListBox"),
            new ThemeCssSelector ("ListView", "Icon / detail lists.", () => ListView.DefaultStyle),
            new ThemeCssSelector ("Menu", "The menu bar. Its items paint on the strip's background; the hovered item uses --control-highlight-low-color.", () => Menu.DefaultStyle),
            new ThemeCssSelector ("MenuDropDown", "Drop-down and context menus.", () => MenuDropDown.DefaultStyle),
            new ThemeCssSelector ("MonthCalendar", "The calendar grid; the selected day uses --accent-color.", () => MonthCalendar.DefaultStyle),
            new ThemeCssSelector ("NavigationPane", "The Outlook-style side navigation bar.", () => NavigationPane.DefaultStyle),
            new ThemeCssSelector ("NumericUpDown", "Numeric spinners.", () => NumericUpDown.DefaultStyle),
            new ThemeCssSelector ("Panel", "Plain containers, including layout panels and tab pages.",
                () => Panel.DefaultStyle, null, "FlowLayoutPanel", "TableLayoutPanel", "TabPage", "SplitterPanel"),
            new ThemeCssSelector ("PictureBox", "Image boxes.", () => PictureBox.DefaultStyle),
            new ThemeCssSelector ("PropertyGrid", "Property editors.", () => PropertyGrid.DefaultStyle),
            new ThemeCssSelector ("RadioButton", "Radio buttons.", () => RadioButton.DefaultStyle),
            new ThemeCssSelector ("Ribbon", "The ribbon; items paint on its background and highlight with --control-highlight-low-color / --control-highlight-mid-color.", () => Ribbon.DefaultStyle),
            new ThemeCssSelector ("ScrollBar", "Scroll bars: background-color is the track; the thumb and arrows follow --control-low-color and --border-low-color.", () => ScrollBar.DefaultStyle),
            new ThemeCssSelector ("SplitContainer", "Split containers (the splitter bar between the two panels).", () => SplitContainer.DefaultStyle),
            new ThemeCssSelector ("Splitter", "Stand-alone splitter bars.", () => Splitter.DefaultStyle),
            new ThemeCssSelector ("StatusBar", "The status bar along the bottom of a form.", () => StatusBar.DefaultStyle),
            new ThemeCssSelector ("TabControl", "Tab controls: the frame around the pages (the tab headers are a TabStrip, the pages are Panels).", () => TabControl.DefaultStyle),
            new ThemeCssSelector ("TabStrip", "The row of tab headers. The selected tab uses --control-low-color with an --accent-color-2 underline.", () => TabStrip.DefaultStyle),
            new ThemeCssSelector ("TextBox", "Text inputs. Selected text uses --text-selection-background-color.", () => TextBox.DefaultStyle, null, "DateTimePicker"),
            new ThemeCssSelector ("ToolBar", "Tool bars; items highlight with --control-highlight-low-color.", () => ToolBar.DefaultStyle),
            new ThemeCssSelector ("TrackBar", "Sliders. Supports :hover.", () => TrackBar.DefaultStyle, () => TrackBar.DefaultStyleHover),
            new ThemeCssSelector ("TreeView", "Tree views.", () => TreeView.DefaultStyle),
        };

        /// <summary>The properties accepted inside a control rule.</summary>
        public static IReadOnlyList<ThemeCssProperty> Properties { get; } = new[] {
            new ThemeCssProperty ("background-color", "color", "The control's background."),
            new ThemeCssProperty ("color", "color", "The control's text (foreground) colour."),
            new ThemeCssProperty ("border", "[width] [solid | none] [color]", "Shorthand for border-width, border-style and border-color, in any order. Only solid borders are drawn; 'none' is width 0."),
            new ThemeCssProperty ("border-width", "length", "The width of all four border sides, in pixels."),
            new ThemeCssProperty ("border-color", "color", "The colour of all four border sides."),
            new ThemeCssProperty ("border-radius", "length", "The corner radius, in pixels, applied to all four corners. When it is greater than 0 all four sides are drawn with the same width and colour."),
            new ThemeCssProperty ("border-top-width", "length", "The width of one side. Also border-right-width, border-bottom-width, border-left-width."),
            new ThemeCssProperty ("border-top-color", "color", "The colour of one side. Also border-right-color, border-bottom-color, border-left-color."),
            new ThemeCssProperty ("font-family", "family list", "The typeface. The first family installed on the machine is used; generic names (sans-serif, serif, monospace) are passed to the OS font matcher."),
            new ThemeCssProperty ("font-size", "length", "The text size in pixels (not points)."),
            new ThemeCssProperty ("font-weight", "normal | bold | 100..900", "The weight. Without a font-family in the same rule, the default UI font family is used at that weight."),
            new ThemeCssProperty ("font-style", "normal | italic | oblique", "The slant. Without a font-family in the same rule, the default UI font family is used."),
        };

        /// <summary>
        /// Every property name a control rule accepts, including the per-side border variants the
        /// table above abbreviates.
        /// </summary>
        public static IReadOnlyList<string> PropertyNames { get; } = new[] {
            "background-color", "color",
            "border", "border-width", "border-color", "border-radius",
            "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
            "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
            "font-family", "font-size", "font-weight", "font-style",
        };

        /// <summary>The pseudo-classes a selector may carry. Only <c>:hover</c>, and only on the selectors that support it.</summary>
        public static IReadOnlyList<string> PseudoClasses { get; } = new[] { "hover" };

        /// <summary>The CSS named colours a colour value may use (case-insensitive), plus <c>transparent</c>.</summary>
        public static IReadOnlyDictionary<string, SKColor> NamedColors => ThemeCssValues.NamedColors;

        /// <summary>Finds a token by its CSS name (case-insensitive), or null.</summary>
        public static ThemeCssToken? FindToken (string name)
            => Tokens.FirstOrDefault (t => string.Equals (t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Finds a selector by type name (case-insensitive), or null.</summary>
        public static ThemeCssSelector? FindSelector (string name)
            => Selectors.FirstOrDefault (s => string.Equals (s.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Renders the reference as Markdown tables -- the same text that appears in
        /// <c>docs/theming.md</c>. Paste it into a prompt to let a coding assistant write a theme.
        /// </summary>
        public static string ToMarkdown ()
        {
            var sb = new StringBuilder ();

            sb.AppendLine ("### Tokens (`:root` custom properties)");
            sb.AppendLine ();
            sb.AppendLine ("| Token | Value | Sets `Theme.` | What it is | Read by |");
            sb.AppendLine ("|---|---|---|---|---|");
            foreach (var token in Tokens)
                sb.AppendLine ($"| `{token.Name}` | {KindName (token.Kind)} | `{token.PropertyName}` | {Escape (token.Description)} | {Escape (token.UsedBy)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Selectors (control type names)");
            sb.AppendLine ();
            sb.AppendLine ("| Selector | `:hover` | Also styles | Notes |");
            sb.AppendLine ("|---|---|---|---|");
            foreach (var selector in Selectors)
                sb.AppendLine ($"| `{selector.Name}` | {(selector.SupportsHover ? "yes" : "no")} | {(selector.AlsoAppliesTo.Count == 0 ? "" : string.Join (", ", selector.AlsoAppliesTo.Select (a => $"`{a}`")))} | {Escape (selector.Description)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Properties (inside a control rule)");
            sb.AppendLine ();
            sb.AppendLine ("| Property | Value | Effect |");
            sb.AppendLine ("|---|---|---|");
            foreach (var property in Properties)
                sb.AppendLine ($"| `{property.Name}` | {Escape (property.ValueSyntax)} | {Escape (property.Description)} |");

            return sb.ToString ();
        }

        internal static string KindName (ThemeCssValueKind kind) => kind switch {
            ThemeCssValueKind.Color => "color",
            ThemeCssValueKind.Length => "length (px)",
            ThemeCssValueKind.FontWeight => "font weight",
            ThemeCssValueKind.FontStyle => "font style",
            _ => "font family list"
        };

        private static string Escape (string text) => text.Replace ("|", "\\|");

        /// <summary>Converts <c>AccentColor2</c> to <c>accent-color-2</c> and <c>UIFontBold</c> to <c>ui-font-bold</c>.</summary>
        public static string ToKebabCase (string name)
        {
            var sb = new StringBuilder ();

            for (var i = 0; i < name.Length; i++) {
                var c = name[i];
                var previous = i > 0 ? name[i - 1] : '\0';
                var next = i + 1 < name.Length ? name[i + 1] : '\0';

                // A boundary sits before an upper-case letter that follows a lower-case letter or digit
                // (accent|Color), before the last capital of an acronym run (UI|Font), and before a digit
                // that follows a letter (Color|2).
                var boundary = i > 0 && (
                    (char.IsUpper (c) && (char.IsLower (previous) || char.IsDigit (previous)))
                    || (char.IsUpper (c) && char.IsUpper (previous) && char.IsLower (next))
                    || (char.IsDigit (c) && char.IsLetter (previous)));

                if (boundary)
                    sb.Append ('-');

                sb.Append (char.ToLowerInvariant (c));
            }

            return sb.ToString ();
        }
    }
}
