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
        FontStyle,

        /// <summary>
        /// One shadow (#285): offsets, blur, spread, inset and a colour. Only appears on the <c>box-shadow</c> control-rule
        /// declaration (several shadows are a <see cref="List"/> of these), never on a <c>:root</c> token.
        /// </summary>
        BoxShadow,

        /// <summary>
        /// A CSS keyword from a short fixed list (#286): <c>solid</c> or <c>dashed</c>. Only appears on
        /// the <c>border-style</c> control-rule declaration.
        /// </summary>
        Keyword,

        /// <summary>A plain number, e.g. an <c>opacity</c> between 0 and 1 (a percentage is converted).</summary>
        Number,

        /// <summary>A gradient, one layer of <c>background-image</c>; <see cref="ThemeCssValue.Css"/> is its CSS text.</summary>
        Image,

        /// <summary>A time, <c>150ms</c> or <c>.15s</c>: an element of a <c>transition-duration</c> or <c>transition-delay</c> list.</summary>
        Time,

        /// <summary>A CSS timing function -- <c>ease</c>, <c>cubic-bezier(...)</c>, <c>steps(...)</c>: an element of a <c>transition-timing-function</c> list.</summary>
        TimingFunction,

        /// <summary>
        /// A comma-separated list (<see cref="ThemeCssValue.Items"/>): the value of each <c>transition-*</c> longhand. A
        /// <c>transition-property</c> list holds <see cref="Keyword"/> values (property names, or <c>all</c>; empty for <c>none</c>).
        /// </summary>
        List
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
    public sealed partial class ThemeCssSelector
    {
        internal ThemeCssSelector (string name, string description, Func<ControlStyle> getStyle, Func<ControlStyle>? getHoverStyle = null,
            Func<ControlStyle>? getActiveStyle = null, Func<ControlStyle>? getDisabledStyle = null, Func<ControlStyle>? getFocusStyle = null,
            Func<ControlStyle>? getFocusVisibleStyle = null)
        {
            GetFocusVisibleStyle = getFocusVisibleStyle;
            Name = name;
            Description = description;
            GetStyle = getStyle;
            GetHoverStyle = getHoverStyle;
            GetActiveStyle = getActiveStyle;
            GetDisabledStyle = getDisabledStyle;
            GetFocusStyle = getFocusStyle;
        }

        // A Telerik control's selector. The core cannot reference the Telerik assembly, and a theme is
        // usually parsed before any Telerik type has loaded, so the selector is known here by name and the
        // control's DefaultStyle is read when a rule is applied -- Type.GetType loads the assembly if the
        // application ships it. Without it the rule applies to a style no control uses, and does nothing.
        internal static ThemeCssSelector Telerik (string name, string description, Func<Type?> resolve)
            => new ThemeCssSelector (name, description, () => TelerikStyle (name, resolve ())) { resolve_type = resolve, IsTelerik = true };

        // The selector of a rule that names no control type (.primary, #save). It is not in ThemeCssReference.Selectors and no control
        // type is styled by it; its style is a detached one nothing paints from, so a host that forgets to check IsAny cannot restyle
        // anything by accident.
        internal static ThemeCssSelector Any { get; } = new ThemeCssSelector ("*", "Any control type: a rule with only classes and ids.", () => new ControlStyle (Control.DefaultStyle));

        /// <summary>
        /// Whether this is the stand-in selector of a rule that names no control type (<c>.primary</c>, <c>#save</c>), as opposed to a
        /// real control type from <see cref="ThemeCssReference.Selectors"/>.
        /// </summary>
        public bool IsAny => ReferenceEquals (this, Any);

        private static readonly Dictionary<string, ControlStyle> detached_styles = new (StringComparer.Ordinal);

        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2075", Justification = "The Telerik control types are named by literal strings, which the trimmer resolves and keeps; their public static DefaultStyle field is part of their public surface.")]
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2070", Justification = "As above.")]
        private static ControlStyle TelerikStyle (string name, Type? type)
        {
            if (type?.GetField ("DefaultStyle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue (null) is ControlStyle style)
                return style;

            lock (detached_styles) {
                if (!detached_styles.TryGetValue (name, out var detached))
                    detached_styles[name] = detached = new ControlStyle (Control.DefaultStyle);

                return detached;
            }
        }

        private Func<Type?>? resolve_type;

        /// <summary>Whether this is a <c>Majorsilence.Forms.Telerik</c> control's selector.</summary>
        public bool IsTelerik { get; private init; }

        // The control type the selector names: a core type by name, a Telerik type through its resolver.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Used to generate the theming reference; a trimmed application lists fewer derived types.")]
        internal Type? ControlType => resolve_type is { } resolve ? resolve () : typeof (Control).Assembly.GetType ("Majorsilence.Forms." + Name);

        /// <summary>The selector text: the control's type name.</summary>
        public string Name { get; }

        /// <summary>What the control is, and anything a designer should know about how it paints.</summary>
        public string Description { get; }

        /// <summary>Whether <c>Name:hover</c> is meaningful -- only controls that repaint on hover.</summary>
        public bool SupportsHover => GetHoverStyle is not null;

        /// <summary>Whether <c>Name:active</c> is meaningful (#285) -- only controls that repaint while pressed.</summary>
        public bool SupportsActive => GetActiveStyle is not null;

        /// <summary>Whether <c>Name:disabled</c> is meaningful (#285) -- only controls that repaint when disabled.</summary>
        public bool SupportsDisabled => GetDisabledStyle is not null;

        /// <summary>Whether <c>Name:focus</c> is meaningful (#285) -- only controls that repaint when focused.</summary>
        public bool SupportsFocus => GetFocusStyle is not null;

        /// <summary>Whether <c>Name:focus-visible</c> is meaningful -- the controls with <c>:focus</c>.</summary>
        public bool SupportsFocusVisible => GetFocusVisibleStyle is not null;

        /// <summary>Whether the control changes appearance for any state, so a <c>transition</c> on it can run.</summary>
        public bool HasStates => SupportsHover || SupportsActive || SupportsDisabled || SupportsFocus || SupportsFocusVisible;

        /// <summary>Derived controls that share this type's default style and therefore this rule.</summary>
        /// <remarks>Computed from the real inheritance tree as of #100: every public control whose nearest
        /// ancestor with a selector is this one. It used to be a hand-written list that named four or five
        /// derived types where dozens inherit.</remarks>
        public IReadOnlyList<string> AlsoAppliesTo => also_applies_to ??= ThemeCssReference.DerivedStyledTypes (this);

        private IReadOnlyList<string>? also_applies_to;

        /// <summary>
        /// The pieces inside the control a theme can address as <c>Name::part</c> (see
        /// <see cref="ThemeCssPart"/>). Empty for controls that paint no separately styleable parts.
        /// </summary>
        public IReadOnlyList<ThemeCssPart> Parts { get; private set; } = Array.Empty<ThemeCssPart> ();

        internal Func<ControlStyle> GetStyle { get; }
        internal Func<ControlStyle>? GetHoverStyle { get; }
        internal Func<ControlStyle>? GetActiveStyle { get; }
        internal Func<ControlStyle>? GetDisabledStyle { get; }
        internal Func<ControlStyle>? GetFocusStyle { get; }
        internal Func<ControlStyle>? GetFocusVisibleStyle { get; }

        /// <summary>The type's default <see cref="ControlStyle"/> -- what a <c>Type { ... }</c> rule sets.</summary>
        public ControlStyle Style => GetStyle ();

        /// <summary>The type's default hover style -- what a <c>Type:hover { ... }</c> rule sets -- or null.</summary>
        public ControlStyle? HoverStyle => GetHoverStyle?.Invoke ();

        /// <summary>The type's default active style -- what a <c>Type:active { ... }</c> rule sets -- or null (#285).</summary>
        public ControlStyle? ActiveStyle => GetActiveStyle?.Invoke ();

        /// <summary>The type's default disabled style -- what a <c>Type:disabled { ... }</c> rule sets -- or null (#285).</summary>
        public ControlStyle? DisabledStyle => GetDisabledStyle?.Invoke ();

        /// <summary>The type's default focus style -- what a <c>Type:focus { ... }</c> rule sets -- or null (#285).</summary>
        public ControlStyle? FocusStyle => GetFocusStyle?.Invoke ();

        /// <summary>The type's default focus-visible style -- what a <c>Type:focus-visible { ... }</c> rule sets -- or null.</summary>
        public ControlStyle? FocusVisibleStyle => GetFocusVisibleStyle?.Invoke ();

        /// <summary>Finds a part by name (case-insensitive), or null.</summary>
        public ThemeCssPart? FindPart (string name)
            => Parts.FirstOrDefault (p => string.Equals (p.Name, name, StringComparison.OrdinalIgnoreCase));

        internal ThemeCssSelector WithParts (params ThemeCssPart[] parts)
        {
            Parts = parts;
            return this;
        }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>One selector form with classes or ids, as documented in <see cref="ThemeCssReference.ClassAndIdForms"/>.</summary>
    public sealed class ThemeCssSelectorForm
    {
        internal ThemeCssSelectorForm (string selector, string matches, string specificity)
        {
            Selector = selector;
            Matches = matches;
            Specificity = specificity;
        }

        /// <summary>The selector as written, e.g. <c>Button.primary:hover</c>.</summary>
        public string Selector { get; }

        /// <summary>What it matches.</summary>
        public string Matches { get; }

        /// <summary>Its specificity as <see cref="ThemeCssSpecificity.ToString"/> writes it.</summary>
        public string Specificity { get; }

        /// <inheritdoc/>
        public override string ToString () => Selector;
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
                "Button hover background, LinkLabel text, selected DataGridView cells, MonthCalendar selection, TrackBar thumb, the custom title bar. Telerik: RadGridView, RadToggleSwitch."),
            new ThemeCssToken (nameof (Theme.AccentColor2), ThemeCssValueKind.Color,
                "The secondary accent, used where the primary one needs a companion.",
                "Button hover border, Form border, ProgressBar fill, the selected TabStrip tab underline, StatusStrip, NavigationPane, TrackBar."),
            new ThemeCssToken (nameof (Theme.BackgroundColor), ThemeCssValueKind.Color,
                "The window background and the default background of every control that does not pin its own.",
                "Form, Panel and every ambient control; Menu, ToolBar and Ribbon items when the strip itself has no background rule. Telerik: the dock windows (ToolWindow, DocumentWindow, DockWindow)."),
            new ThemeCssToken (nameof (Theme.BorderLowColor), ThemeCssValueKind.Color,
                "The everyday border colour.",
                "Default control borders (TextBox, ListBox, ComboBox, GroupBox, NumericUpDown), DataGridView grid lines, ScrollBar outlines, ListView lines, Ribbon. Telerik: RadGridView, RadScheduler."),
            new ThemeCssToken (nameof (Theme.BorderMidColor), ThemeCssValueKind.Color,
                "A stronger border colour.",
                "DataGridView header separators, ListView. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.BorderHighColor), ThemeCssValueKind.Color,
                "The strongest border colour.",
                "DataGridView row headers, TrackBar ticks. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.ControlLowColor), ThemeCssValueKind.Color,
                "The lightest control surface -- the 'paper' that lists and inputs sit on.",
                "ListBox, DataGridView, PropertyGrid and TreeView backgrounds, the selected TabStrip tab, ScrollBar arrows and thumb, MenuDropDown, NavigationPane. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.ControlMidColor), ThemeCssValueKind.Color,
                "The default control surface.",
                "Button, ComboBox and NumericUpDown faces, alternating DataGridView rows, ListBox items, TrackBar groove. Telerik: RadGridView, RadToggleSwitch, RadScheduler agenda day headers, RadDock tab strips."),
            new ThemeCssToken (nameof (Theme.ControlMidHighColor), ThemeCssValueKind.Color,
                "A slightly darker surface.",
                "ScrollBar track (ScrollBar background), TrackBar. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.ControlHighColor), ThemeCssValueKind.Color,
                "A dark control surface. Not read by the built-in renderers; available to custom controls and VisualStyleRenderer.",
                "Custom controls. Telerik: RadToggleSwitch."),
            new ThemeCssToken (nameof (Theme.ControlVeryHighColor), ThemeCssValueKind.Color,
                "The darkest control surface. Not read by the built-in renderers; available to custom controls.",
                "Custom controls. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.ControlHighlightLowColor), ThemeCssValueKind.Color,
                "The hover highlight for items inside a control.",
                "Hovered Menu, ToolBar, Ribbon and MenuDropDown items, hovered TabStrip tabs (TabControl.HotTrack), hovered ListBox/ListView rows, selected DataGridView rows, MonthCalendar hover."),
            new ThemeCssToken (nameof (Theme.ControlHighlightMidColor), ThemeCssValueKind.Color,
                "The pressed / selected item highlight.",
                "Selected Ribbon item, ScrollBar and NumericUpDown arrow glyphs, MonthCalendar."),
            new ThemeCssToken (nameof (Theme.ControlHighlightHighColor), ThemeCssValueKind.Color,
                "The strongest item highlight. Not read by the built-in renderers; available to custom controls.",
                "Custom controls."),
            new ThemeCssToken (nameof (Theme.ForegroundColor), ThemeCssValueKind.Color,
                "The default text colour.",
                "Every control's text unless a rule or the control sets its own; menu, toolbar, grid, tree and title bar text. Telerik: RadGridView, RadToggleSwitch, RadPageView."),
            new ThemeCssToken (nameof (Theme.ForegroundColorOnAccent), ThemeCssValueKind.Color,
                "Text drawn on top of an accent-coloured surface. Keep it readable against --accent-color.",
                "Hovered Button text, the custom title bar caption, selected MonthCalendar day, DataGridView selection text. Telerik: RadGridView, RadToggleSwitch."),
            new ThemeCssToken (nameof (Theme.ForegroundDisabledColor), ThemeCssValueKind.Color,
                "Text and glyphs of disabled controls and items.",
                "Every renderer, when the control or item is disabled; the ProgressBar fill when disabled. Telerik: RadGridView, RadToggleSwitch, RadDock tab strips."),
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
                "DataGridView cells, ListView items, Ribbon items. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.RootFontSize), ThemeCssValueKind.Length,
                "What one 'rem' is, in pixels: 16px by default, a browser's root font size, so a rem copied from a web stylesheet keeps its size. Changes no font by itself.",
                "Every rem in the theme: lengths, calc() and font sizes written in rem."),
            new ThemeCssToken (nameof (Theme.UIFont), ThemeCssValueKind.FontFamily,
                "The theme font family (regular weight).",
                "Menu, ToolBar, StatusStrip, MenuDropDown, DataGridView, ListView, NavigationPane and the custom title bar. Button/Label/TextBox text uses the ambient font instead -- set that with a `Form { font-family }` rule. Telerik: RadGridView."),
            new ThemeCssToken (nameof (Theme.UIFontBold), ThemeCssValueKind.FontFamily,
                "The theme font family used where text is bold. Resolved at bold weight.",
                "DataGridView column headers, MonthCalendar title, NavigationPane group headers. Telerik: RadGridView."),
        };

        /// <summary>The control type selectors a theme can target.</summary>
        public static IReadOnlyList<ThemeCssSelector> Selectors { get; } = new[] {
            new ThemeCssSelector ("Button", "Push buttons. Hovering applies the :hover rule on top of the normal one. Supports :active, :disabled, :focus and :focus-visible.",
                () => Button.DefaultStyle, () => Button.DefaultStyleHover, () => Button.DefaultStyleActive, () => Button.DefaultStyleDisabled, () => Button.DefaultStyleFocus,
                () => Button.DefaultStyleFocusVisible),
            new ThemeCssSelector ("CheckBox", "Check boxes: the text and the box glyph's surround.", () => CheckBox.DefaultStyle),
            new ThemeCssSelector ("ComboBox", "Drop-down selectors (the closed box; the open list is a ListBox).", () => ComboBox.DefaultStyle),
            new ThemeCssSelector ("DataGridView", "Data grids: the control background and border. Cells follow the tokens (--control-low-color, --border-low-color); headers, selection and alternating rows are parts.", () => DataGridView.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("header", "Column headers: background, text colour, font; border-color is the separator between headers, border-bottom-color the line under the header row.",
                        () => DataGridView.DefaultColumnHeaderStyle, null,
                        "background-color", "color", "border-color", "border-bottom-color", "font-family", "font-size", "font-weight", "font-style"),
                    new ThemeCssPart ("row-header", "Row headers: background, the current-row indicator (color) and the separator (border-color).",
                        () => DataGridView.DefaultRowHeaderStyle, null,
                        "background-color", "color", "border-color"),
                    new ThemeCssPart ("selection", "The selected row's background and text colour, and the outline of the selected cell in cell-select mode (border-color, border-width).",
                        () => DataGridView.DefaultSelectionStyle, null,
                        "background-color", "color", "border-color", "border-width"),
                    new ThemeCssPart ("alternating-row", "The background of every second row. Unset by default (a shade derived from the grid background).",
                        () => DataGridView.DefaultAlternatingRowStyle, null,
                        "background-color")),
            new ThemeCssSelector ("DateTimePicker", "Date pickers: background-color is behind the date text; the drop button and the check box follow the tokens.", () => DateTimePicker.DefaultStyle),
            new ThemeCssSelector ("Form", "The window. background-color is the window background; border sets the window frame on platforms that draw their own; font-family / font-size / color here become the ambient defaults every child control inherits when it sets none of its own.", () => Form.DefaultStyle),
            new ThemeCssSelector ("FormTitleBar", "The title bar a window draws for itself (every platform but macOS, which uses the system's). background-color is the bar, color the caption text. On macOS's merged title bar it blends with the window background instead.", () => FormTitleBar.DefaultStyle),
            new ThemeCssSelector ("GroupBox", "Titled group frames: the border colour is the frame, color is the caption.", () => GroupBox.DefaultStyle),
            new ThemeCssSelector ("HostedSurface", "A Majorsilence.Forms surface embedded in an Avalonia or Uno host. Transparent by default so the host shows through; a background-color rule makes it opaque.", () => HostedSurface.DefaultStyle),
            new ThemeCssSelector ("Label", "Static text.", () => Label.DefaultStyle),
            new ThemeCssSelector ("LinkLabel", "Hyperlink text; color is the link colour. Supports :hover, :active, :disabled, :focus and :focus-visible.",
                () => LinkLabel.DefaultStyle, () => LinkLabel.DefaultStyleHover, () => LinkLabel.DefaultStyleActive, () => LinkLabel.DefaultStyleDisabled, () => LinkLabel.DefaultStyleFocus,
                () => LinkLabel.DefaultStyleFocusVisible),
            new ThemeCssSelector ("ListBox", "Single-column lists (also the ComboBox drop-down list). The selected item is the ::selection part.",
                () => ListBox.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("selection", "The selected item's background and, when set, its text colour.",
                        () => ListBox.DefaultSelectionStyle, null, "background-color", "color")),
            new ThemeCssSelector ("ListView", "Icon / detail lists. The selected item is the ::selection part.", () => ListView.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("selection", "The selected item's background and, when set, its text colour.",
                        () => ListView.DefaultSelectionStyle, null, "background-color", "color")),
            new ThemeCssSelector ("MdiClient", "The workspace of an MDI parent form, behind its child windows: background-color is the workspace colour.", () => MdiClient.DefaultStyle),
            new ThemeCssSelector ("Menu", "The menu bar. Items are the ::item part; they paint on the strip's background unless ::item sets one.", () => Menu.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("item", "A menu bar item: text colour and optional background; :hover is the hovered or open item.",
                        () => Menu.DefaultItemStyle, () => Menu.DefaultItemHoverStyle, "background-color", "color")),
            new ThemeCssSelector ("MenuDropDown", "Drop-down and context menus. Items are the ::item part.", () => MenuDropDown.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("item", "A drop-down item: background and text colour; :hover is the hovered or open item.",
                        () => MenuDropDown.DefaultItemStyle, () => MenuDropDown.DefaultItemHoverStyle, "background-color", "color")),
            new ThemeCssSelector ("MonthCalendar", "The calendar grid; the selected day uses --accent-color.", () => MonthCalendar.DefaultStyle),
            new ThemeCssSelector ("NavigationPane", "The Outlook-style side navigation bar.", () => NavigationPane.DefaultStyle),
            new ThemeCssSelector ("NumericUpDown", "Numeric spinners.", () => NumericUpDown.DefaultStyle),
            new ThemeCssSelector ("Panel", "Plain containers, including layout panels and tab pages.",
                () => Panel.DefaultStyle),
            new ThemeCssSelector ("PictureBox", "Image boxes.", () => PictureBox.DefaultStyle),
            new ThemeCssSelector ("PopupWindow", "Floating popup windows -- a combo box's list, a tool tip, a menu host: background-color is the popup behind its content.", () => PopupWindow.DefaultStyle),
            new ThemeCssSelector ("PrintPreviewControl", "Print previews: background-color is the surround the pages sit on; the pages themselves are paper and stay white.", () => PrintPreviewControl.DefaultStyle),
            new ThemeCssSelector ("ProgressBar", "Progress bars: background-color is the track and border its frame; the fill is --accent-color-2.", () => ProgressBar.DefaultStyle),
            new ThemeCssSelector ("PropertyGrid", "Property editors.", () => PropertyGrid.DefaultStyle),
            new ThemeCssSelector ("RadioButton", "Radio buttons.", () => RadioButton.DefaultStyle),
            new ThemeCssSelector ("Ribbon", "The ribbon; items paint on its background and highlight with --control-highlight-low-color / --control-highlight-mid-color.", () => Ribbon.DefaultStyle),
            new ThemeCssSelector ("ScrollBar", "Scroll bars: background-color is the track; the grip and the arrow buttons are the ::thumb and ::arrow parts.", () => ScrollBar.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("thumb", "The draggable grip: fill, outline (border-color, border-width) and corner radius.",
                        () => ScrollBar.DefaultThumbStyle, null, "background-color", "border-color", "border-width", "border-radius"),
                    new ThemeCssPart ("arrow", "The two arrow buttons: fill, outline and the arrow glyph colour (color).",
                        () => ScrollBar.DefaultArrowStyle, null, "background-color", "border-color", "color")),
            new ThemeCssSelector ("SplitContainer", "Split containers (the splitter bar between the two panels).", () => SplitContainer.DefaultStyle),
            new ThemeCssSelector ("Splitter", "Stand-alone splitter bars.", () => Splitter.DefaultStyle),
            new ThemeCssSelector ("StatusBar", "The status bar along the bottom of a form.", () => StatusBar.DefaultStyle),
            new ThemeCssSelector ("StatusStrip", "Status strips (the ToolStrip-based status bar): background-color is the strip, border-top its seam with the form above.", () => StatusStrip.DefaultStyle),
            new ThemeCssSelector ("TabControl", "Tab controls: the frame around the pages (the tab headers are a TabStrip, the pages are Panels).", () => TabControl.DefaultStyle),
            new ThemeCssSelector ("TabStrip", "The row of tab headers. Tabs are the ::item part (with :hover) and the current one the ::selected part.", () => TabStrip.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("item", "A tab: optional background and the caption colour; :hover is the hovered tab, painted only when TabControl.HotTrack is set (default --control-highlight-low-color).",
                        () => TabStrip.DefaultItemStyle, () => TabStrip.DefaultItemHoverStyle, "background-color", "color"),
                    new ThemeCssPart ("selected", "The selected tab: optional background, caption colour, and the accent underline (border-bottom-color, border-bottom-width; default --accent-color-2, 3px).",
                        () => TabStrip.DefaultSelectedItemStyle, null, "background-color", "color", "border-bottom-color", "border-bottom-width")),
            new ThemeCssSelector ("TextBox", "Text inputs. Selected text uses --text-selection-background-color. Supports :focus and :focus-visible, for a focus ring (a text box always counts as :focus-visible when focused, as in a browser).", () => TextBox.DefaultStyle,
                getFocusStyle: () => TextBox.DefaultStyleFocus, getFocusVisibleStyle: () => TextBox.DefaultStyleFocusVisible),
            new ThemeCssSelector ("ToolBar", "Tool bars. Items are the ::item part; :hover also covers a checked (toggled) item.", () => ToolBar.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("item", "A tool bar item: text colour and optional background; :hover is the hovered, open or checked item.",
                        () => ToolBar.DefaultItemStyle, () => ToolBar.DefaultItemHoverStyle, "background-color", "color")),
            new ThemeCssSelector ("TrackBar", "Sliders. Supports :hover, :active, :disabled, :focus and :focus-visible.",
                () => TrackBar.DefaultStyle, () => TrackBar.DefaultStyleHover, () => TrackBar.DefaultStyleActive, () => TrackBar.DefaultStyleDisabled, () => TrackBar.DefaultStyleFocus,
                () => TrackBar.DefaultStyleFocusVisible),
            new ThemeCssSelector ("TreeView", "Tree views. The selected node is the ::selection part.", () => TreeView.DefaultStyle)
                .WithParts (
                    new ThemeCssPart ("selection", "The selected node's background and, when set, its text colour.",
                        () => TreeView.DefaultSelectionStyle, null, "background-color", "color")),

            // Telerik controls whose style is parented on Control's, so no core selector reaches them (#100).
            ThemeCssSelector.Telerik ("DockWindowBase", "Telerik dock windows (tool, document and plain dock windows): background-color is the window behind its content, which is --background-color by default rather than the form's.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.DockWindowBase, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RadCommandBar", "Telerik command bars: background-color is the bar behind its strips.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RadCommandBar, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RadPdfViewerNavigator", "The Telerik PDF viewer's navigation toolbar.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RadPdfViewerNavigator, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RadRibbonBar", "Telerik ribbon bars: background-color is the ribbon behind its tabs and groups.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RadRibbonBar, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RadScheduler", "The Telerik scheduler's agenda view: background-color is behind the appointment list.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RadScheduler, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RadSchedulerNavigator", "The Telerik scheduler's navigation bar.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RadSchedulerNavigator, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RadStatusStrip", "Telerik status strips along the bottom of a form.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RadStatusStrip, Majorsilence.Forms.Telerik")),
            ThemeCssSelector.Telerik ("RichTextEditorRibbonBar", "The Telerik rich text editor's ribbon bar.",
                () => Type.GetType ("Majorsilence.Forms.Telerik.RichTextEditorRibbonBar, Majorsilence.Forms.Telerik")),
        };

        /// <summary>
        /// The selector forms with classes and ids, each with what it matches and its specificity: the grammar of a selector that names
        /// individual controls (see <see cref="Control.StyleClasses"/>). A test parses every form here and checks the specificity.
        /// </summary>
        public static IReadOnlyList<ThemeCssSelectorForm> ClassAndIdForms { get; } = new[] {
            new ThemeCssSelectorForm (".primary", "any control whose classes include `primary`", "(0,1,0)"),
            new ThemeCssSelectorForm ("Button.primary", "a `Button` (or a type styled by `Button`'s rule) with that class", "(0,1,1)"),
            new ThemeCssSelectorForm (".primary.large", "a control with both classes", "(0,2,0)"),
            new ThemeCssSelectorForm ("#saveButton", "the control whose `Name` is `saveButton` (a designer gives every control one)", "(1,0,0)"),
            new ThemeCssSelectorForm ("Button#saveButton", "the `Button` named `saveButton`", "(1,0,1)"),
            new ThemeCssSelectorForm ("Button.primary:hover", "that variant while hovered -- one pseudo-class, after the classes and ids", "(0,2,1)"),
            new ThemeCssSelectorForm (".primary:hover", "any control with the class that supports `:hover` (the pseudo-class column above)", "(0,2,0)"),
        };

        /// <summary>The properties accepted inside a control rule.</summary>
        public static IReadOnlyList<ThemeCssProperty> Properties { get; } = new[] {
            new ThemeCssProperty ("background-color", "color", "The control's background."),
            new ThemeCssProperty ("color", "color", "The control's text (foreground) colour."),
            new ThemeCssProperty ("border", "[width] [solid | dashed | none] [color]", "Shorthand for border-width, border-style and border-color, in any order. Borders are solid or dashed; 'none' is width 0."),
            new ThemeCssProperty ("border-style", "solid | dashed", "How the border line is drawn: a continuous line or dashes (each dash and gap three times the border width). dotted, double, groove, ridge, inset and outset are rejected."),
            new ThemeCssProperty ("border-width", "length", "The width of all four border sides, in pixels."),
            new ThemeCssProperty ("border-color", "color", "The colour of all four border sides."),
            new ThemeCssProperty ("border-radius", "length{1,4}", "The corner radius, in pixels. One value applies to all four corners; two to top-left/bottom-right and top-right/bottom-left; three to top-left, top-right/bottom-left and bottom-right; four to top-left, top-right, bottom-right, bottom-left. When any corner is greater than 0 all four sides are drawn with the same width and colour. Elliptical radii ('/') are not supported."),
            new ThemeCssProperty ("border-top-left-radius", "length", "The radius of one corner. Also border-top-right-radius, border-bottom-right-radius, border-bottom-left-radius. Wins over an earlier border-radius; a later border-radius resets it."),
            new ThemeCssProperty ("border-top-width", "length", "The width of one side. Also border-right-width, border-bottom-width, border-left-width."),
            new ThemeCssProperty ("border-top-color", "color", "The colour of one side. Also border-right-color, border-bottom-color, border-left-color."),
            new ThemeCssProperty ("font-family", "family list", "The typeface. The first family the machine has is used, and a font registered with PrivateFontCollection counts as one it has; generic names (sans-serif, serif, monospace) are passed to the OS font matcher."),
            new ThemeCssProperty ("font-size", "length", "The text size in pixels (not points)."),
            new ThemeCssProperty ("font-weight", "normal | bold | 100..900", "The weight. Without a font-family in the same rule, the default UI font family is used at that weight."),
            new ThemeCssProperty ("font-style", "normal | italic | oblique", "The slant. Without a font-family in the same rule, the default UI font family is used."),
            new ThemeCssProperty ("box-shadow", "none | [inset] <offset-x> <offset-y> [<blur> [<spread>]] <color>#",
                "Shadows with the control's own shape, the first on top: an outer one behind the face, an inset one inside it over the background. "
                + "e.g. 'box-shadow: 4px 4px #2b1b4d;' (hard and offset) or '0 1px 3px rgb(0 0 0 / 20%), inset 0 0 0 1px #fff'. The colour is required. "
                + "A control cannot paint outside itself, so the face shrinks to leave room for outer shadows on each side they reach."),
            new ThemeCssProperty ("background-image", "none | <gradient>#",
                "Gradient layers over the background colour, inside the control's rounded face, the first on top: linear-gradient(), radial-gradient(), conic-gradient() and their repeating- forms, "
                + "e.g. 'linear-gradient(to bottom, #ffffff, #e6e6e6)'. Stops take lengths, percentages (angles for conic) and two positions for a hard band; "
                + "url() images and transition hints are not supported. 'background' (the shorthand) stays an error."),
            new ThemeCssProperty ("outline", "[width] [solid | dashed | auto | none] [color]",
                "A ring drawn around the control, outside it, by its parent: it takes no room and moves nothing -- the usual focus ring, e.g. 'Button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }'. "
                + "Shorthand for outline-width (thin 1px, medium 3px, thick 5px), outline-style and outline-color (the text colour when omitted). It follows the corner radii. Not on windows or parts."),
            new ThemeCssProperty ("outline-offset", "length", "How far outside the control's edge the outline sits; negative draws it inside."),
            new ThemeCssProperty ("opacity", "<number> | <percentage>",
                "How opaque the control is, 0 to 1 (or 0% to 100%), children included: its parent composites it translucent, e.g. 'Button:disabled { opacity: .65; }'. "
                + "Hit-testing is unchanged. Not on windows (Form, PopupWindow, HostedSurface): a window's own opacity is Form.Opacity."),
            new ThemeCssProperty ("transition", "[<property> || <duration> || <timing-function> || <delay>]# | none",
                "Animates the change into this rule's state: shorthand for the four transition-* longhands, one comma-separated entry per property, "
                + "e.g. 'transition: background-color 150ms ease-out, box-shadow 200ms;'. In an entry the first time is the duration and the second the delay; "
                + "a missing property is 'all', a missing duration 0s, a missing timing function 'ease'. Runs on controls with pseudo-classes, and not at all when the user prefers reduced motion."),
            new ThemeCssProperty ("transition-property", "none | all | <property>#",
                "Which properties animate. Animatable: " + string.Join (", ", ControlTransition.AnimatableProperties)
                + ". Border widths are rejected (they move the client area, and with it the children); fonts and border-style have no in-between values."),
            new ThemeCssProperty ("transition-duration", "<time>#", "How long each transition takes: '150ms' or '.15s'. The n-th duration goes with the n-th property; a shorter list repeats."),
            new ThemeCssProperty ("transition-timing-function", "<timing-function>#",
                "The easing: linear, ease (the default), ease-in, ease-out, ease-in-out, step-start, step-end, cubic-bezier(x1, y1, x2, y2), steps(n[, position]). linear() stop lists are not supported."),
            new ThemeCssProperty ("transition-delay", "<time>#", "How long each transition waits before it starts; a negative delay starts it part-way through."),
            new ThemeCssProperty ("animation", "[<name> || <duration> || <timing-function> || <delay> || <iteration-count> || <direction> || <fill-mode> || <play-state>]# | none",
                "Plays '@keyframes' while the control is in this rule's state, e.g. 'animation: pulse 1.2s ease-in-out infinite alternate;'. Shorthand for the eight animation-* longhands; "
                + "the first time is the duration, the second the delay. The keyframes must be in the same sheet. Animates the same properties as transition, wins over it, and does not run when the user prefers reduced motion. Not on windows or parts."),
            new ThemeCssProperty ("animation-name", "none | <keyframes-name>#", "Which '@keyframes' play. Changing the name restarts the animation; keeping it lets it run on across a state change."),
            new ThemeCssProperty ("animation-duration", "<time>#", "How long one iteration takes."),
            new ThemeCssProperty ("animation-timing-function", "<timing-function>#", "The easing between keyframes; a keyframe can set its own with animation-timing-function inside it."),
            new ThemeCssProperty ("animation-delay", "<time>#", "How long it waits before starting; negative starts part-way through."),
            new ThemeCssProperty ("animation-iteration-count", "<number> | infinite", "How many times it runs; fractions stop part-way. An infinite one pauses while its control is off screen."),
            new ThemeCssProperty ("animation-direction", "normal | reverse | alternate | alternate-reverse", "Which way the iterations run."),
            new ThemeCssProperty ("animation-fill-mode", "none | forwards | backwards | both", "Whether the first keyframe applies during the delay (backwards) and the last stays after the end (forwards)."),
            new ThemeCssProperty ("animation-play-state", "running | paused", "A paused animation holds its starting frame."),
        };

        /// <summary>
        /// Every property name a control rule accepts, including the per-side border variants the
        /// table above abbreviates.
        /// </summary>
        public static IReadOnlyList<string> PropertyNames { get; } = new[] {
            "background-color", "color",
            "border", "border-width", "border-color", "border-style", "border-radius",
            "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
            "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
            "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
            "font-family", "font-size", "font-weight", "font-style",
            "box-shadow", "opacity", "background-image",
            "outline", "outline-width", "outline-style", "outline-color", "outline-offset",
            "transition", "transition-property", "transition-duration", "transition-timing-function", "transition-delay",
            "animation", "animation-name", "animation-duration", "animation-timing-function", "animation-delay",
            "animation-iteration-count", "animation-direction", "animation-fill-mode", "animation-play-state",
        };

        /// <summary>
        /// The shorthand properties: a rule may write them, but the parser expands each into longhands, so a host applier and
        /// <see cref="ThemeCssRule.Declarations"/> only ever see <see cref="LonghandNames"/>.
        /// </summary>
        public static IReadOnlyList<string> ShorthandNames { get; } = new[] { "border", "outline", "transition", "animation" };

        /// <summary>Every property a parsed rule's declarations can carry: <see cref="PropertyNames"/> without the <see cref="ShorthandNames"/>.</summary>
        public static IReadOnlyList<string> LonghandNames { get; } = PropertyNames.Except (ShorthandNames).ToArray ();

        /// <summary>
        /// The pseudo-classes a selector may carry: <c>:hover</c>, <c>:active</c>, <c>:disabled</c> and
        /// <c>:focus</c> (#285), and only on the selectors (and, for <c>:hover</c>, the parts) that
        /// support the one written.
        /// </summary>
        public static IReadOnlyList<string> PseudoClasses { get; } = new[] { "hover", "active", "disabled", "focus", "focus-visible" };

        /// <summary>Every <c>(selector, part)</c> pair a theme can address as <c>Selector::part</c>.</summary>
        public static IEnumerable<(ThemeCssSelector Selector, ThemeCssPart Part)> Parts
            => Selectors.SelectMany (s => s.Parts.Select (p => (s, p)));

        /// <summary>The CSS named colours a colour value may use (case-insensitive), plus <c>transparent</c>.</summary>
        public static IReadOnlyDictionary<string, SKColor> NamedColors => ThemeCssValues.NamedColors;

        /// <summary>Finds a token by its CSS name (case-insensitive), or null.</summary>
        public static ThemeCssToken? FindToken (string name)
            => Tokens.FirstOrDefault (t => string.Equals (t.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Finds a selector by type name (case-insensitive), or null.</summary>
        public static ThemeCssSelector? FindSelector (string name)
            => Selectors.FirstOrDefault (s => string.Equals (s.Name, name, StringComparison.OrdinalIgnoreCase));

        // ── coverage: which selector reaches which control (#100) ───────────────────────────────────

        /// <summary>
        /// Controls whose content is not the library's to paint: a theme rule for their nearest selector
        /// styles the frame -- background and border -- and nothing inside it.
        /// </summary>
        internal static readonly (string Name, string Why)[] FrameOnlyControls = {
            ("NativeControlHost", "a native platform widget draws the content"),
            ("SKControl", "the application paints the content"),
            ("SKGLControl", "the application paints the content"),
            ("WebBrowser", "a native browser widget draws the content"),
            ("RadPdfViewer", "the page area is the browser's own PDF renderer"),
            ("RadRichTextEditor", "the document is web-view content"),
        };

        /// <summary>
        /// The types every control is built on. They are themed through the controls built on them; a
        /// selector for one would restyle every control at once, which is what the <c>:root</c> tokens are for.
        /// </summary>
        internal static readonly (string Name, string Why)[] BaseControls = {
            ("Control", "the base of every control; the :root tokens style it"),
            ("ScrollableControl", "the base of the scrolling containers"),
            ("ScrollControl", "the base of the text inputs"),
            ("RadControl", "the base of the Telerik controls"),
        };

        // Built in full before it is published; two threads that race build equal maps.
        private static volatile Dictionary<Type, ThemeCssSelector>? selectors_by_type;

        private static Dictionary<Type, ThemeCssSelector> SelectorsByType ()
        {
            if (selectors_by_type is { } built)
                return built;

            var map = new Dictionary<Type, ThemeCssSelector> ();

            foreach (var selector in Selectors)
                if (selector.ControlType is { } type && !map.ContainsKey (type))
                    map[type] = selector;

            return selectors_by_type = map;
        }

        /// <summary>The selector a control type is themed by: its own, or its nearest ancestor's.</summary>
        internal static ThemeCssSelector? NearestSelector (Type type)
        {
            var map = SelectorsByType ();

            for (var current = type; current is not null && current != typeof (Control) && current != typeof (WindowBase) && current != typeof (object); current = current.BaseType)
                if (map.TryGetValue (current, out var selector))
                    return selector;

            return null;
        }

        /// <summary>Every public, concrete control and window type in the core and, when it is loaded or loadable, the Telerik assembly.</summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Used to generate the theming reference and its coverage check; a trimmed application lists fewer types.")]
        internal static IReadOnlyList<Type> PublicControlTypes ()
        {
            var assemblies = new List<System.Reflection.Assembly> { typeof (Control).Assembly };

            if (Selectors.FirstOrDefault (s => s.IsTelerik)?.ControlType?.Assembly is { } telerik)
                assemblies.Add (telerik);

            return assemblies
                .SelectMany (a => a.GetExportedTypes ())
                .Where (t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters
                             && (typeof (Control).IsAssignableFrom (t) || typeof (WindowBase).IsAssignableFrom (t)))
                .OrderBy (t => t.FullName, StringComparer.Ordinal)
                .ToList ();
        }

        internal static IReadOnlyList<string> DerivedStyledTypes (ThemeCssSelector selector)
        {
            var own = selector.ControlType;

            return PublicControlTypes ()
                .Where (t => t != own && ReferenceEquals (NearestSelector (t), selector))
                .Select (t => t.Name)
                .Distinct (StringComparer.Ordinal)
                .OrderBy (n => n, StringComparer.Ordinal)
                .ToList ();
        }

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
            sb.AppendLine ("| Selector | `:hover` | `:active` | `:disabled` | `:focus` | `:focus-visible` | Also styles | Notes |");
            sb.AppendLine ("|---|---|---|---|---|---|---|---|");
            foreach (var selector in Selectors)
                sb.AppendLine ($"| `{selector.Name}` | {(selector.SupportsHover ? "yes" : "no")} | {(selector.SupportsActive ? "yes" : "no")} | {(selector.SupportsDisabled ? "yes" : "no")} | {(selector.SupportsFocus ? "yes" : "no")} | {(selector.SupportsFocusVisible ? "yes" : "no")} | {(selector.AlsoAppliesTo.Count == 0 ? "" : string.Join (", ", selector.AlsoAppliesTo.Select (a => $"`{a}`")))} | {Escape (selector.Description)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Selectors with classes and ids");
            sb.AppendLine ();
            sb.AppendLine ("A control has style classes (`Control.StyleClasses`, or the space-separated `Control.CssClass`) and an id, its `Name`. A selector can name them, alone or after a type, and the rules that reach one control are applied in order of specificity -- ids, then classes and pseudo-classes, then types; ties go to the later rule -- as in a browser. Not on a window (`Form`) and not combined with a `::part`.");
            sb.AppendLine ();
            sb.AppendLine ("| Selector | Matches | Specificity |");
            sb.AppendLine ("|---|---|---|");
            foreach (var form in ClassAndIdForms)
                sb.AppendLine ($"| `{form.Selector}` | {Escape (form.Matches)} | `{form.Specificity}` |");

            sb.AppendLine ();
            sb.AppendLine ("Combinators (`Panel Button`, `>`, `+`, `~`), `:not ()`, `:is ()`, `:where ()`, `:has ()` and attribute selectors are errors that say they come in a later phase.");

            sb.AppendLine ();
            sb.AppendLine ("### Telerik controls (`Majorsilence.Forms.Telerik`)");
            sb.AppendLine ();
            sb.AppendLine ("Each `Rad*` control follows the selector named here -- its own, or the core control it is built on. \"Tokens only\" means no control rule reaches it, and it follows the `:root` tokens alone.");
            sb.AppendLine ();
            sb.AppendLine ("| Control | Follows |");
            sb.AppendLine ("|---|---|");
            foreach (var type in PublicControlTypes ().Where (t => t.Namespace?.StartsWith ("Majorsilence.Forms.Telerik", StringComparison.Ordinal) == true
                                                                 && !BaseControls.Any (b => b.Name == t.Name))) {
                var follows = NearestSelector (type) is { } s ? $"`{s.Name}`" : "tokens only";
                var frame = FrameOnlyControls.Any (f => f.Name == type.Name) ? " (frame only)" : string.Empty;
                sb.AppendLine ($"| `{type.Name}` | {follows}{frame} |");
            }

            sb.AppendLine ();
            sb.AppendLine ("### Frame-only controls");
            sb.AppendLine ();
            sb.AppendLine ("A rule for the selector these follow styles their background and border; the content is not the library's to paint.");
            sb.AppendLine ();
            sb.AppendLine ("| Control | Why |");
            sb.AppendLine ("|---|---|");
            foreach (var (name, why) in FrameOnlyControls)
                sb.AppendLine ($"| `{name}` | {Escape (why)} |");

            sb.AppendLine ();
            sb.AppendLine ("### Parts (`Selector::part` pseudo-elements)");
            sb.AppendLine ();
            sb.AppendLine ("| Part | `:hover` | Accepts | What it is |");
            sb.AppendLine ("|---|---|---|---|");
            foreach (var (selector, part) in Parts)
                sb.AppendLine ($"| `{selector.Name}::{part.Name}` | {(part.SupportsHover ? "yes" : "no")} | {string.Join (", ", part.Properties.Select (p => $"`{p}`"))} | {Escape (part.Description)} |");

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

        private static string Escape (string text) => text.Replace ("|", "|");

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
