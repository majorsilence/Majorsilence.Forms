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
            : this (selector, part, hover ? ThemeCssState.Hover : ThemeCssState.Normal, property, level, notes, targets)
        {
        }

        internal AvaloniaThemeSupportEntry (string selector, string? part, ThemeCssState state, string property, AvaloniaThemeSupportLevel level, string notes, IReadOnlyList<AvaloniaTarget> targets)
        {
            Selector = selector;
            Part = part;
            State = state;
            Property = property;
            Level = level;
            Notes = notes;
            Targets = targets;
        }

        /// <summary>The CSS selector (the Majorsilence.Forms control type name).</summary>
        public string Selector { get; }

        /// <summary>The <c>::part</c> pseudo-element, or null for the control itself.</summary>
        public string? Part { get; }

        /// <summary>The state the row describes: the resting look or one pseudo-class.</summary>
        public ThemeCssState State { get; }

        /// <summary>Whether the row describes the <c>:hover</c> variant.</summary>
        public bool Hover => State == ThemeCssState.Hover;

        /// <summary>The longhand property name.</summary>
        public string Property { get; }

        /// <summary>How faithfully the pair applies.</summary>
        public AvaloniaThemeSupportLevel Level { get; }

        /// <summary>What it maps to, or why it cannot.</summary>
        public string Notes { get; }

        /// <summary>The Fluent theme resource keys the declaration writes (empty when it is a style setter or unsupported).</summary>
        public IReadOnlyList<string> ResourceKeys => Targets.OfType<ResourceTarget> ().Select (t => t.Key).ToList ();

        /// <summary>The style selectors (Avalonia syntax) the declaration sets a property on.</summary>
        public IReadOnlyList<string> StyleSelectors => Targets.OfType<StyleTarget> ().Select (t => $"{t.Selector.Key} {{ {t.Property.Name} }}")
            .Concat (Targets.OfType<MotionTarget> ().Select (_ => "Transitions / Animations on the template parts")).ToList ();

        internal IReadOnlyList<AvaloniaTarget> Targets { get; }

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

        // Added in #286, after every row was complete for the properties that existed then.
        private static readonly (string Property, string Note)[] effect_properties = {
            ("opacity", "This control has no style selector to put an Opacity on."),
            ("background-image", "This control's fill is not a brush resource or a setter, so there is nowhere to put a gradient."),
            ("outline-width", "Only a :focus / :focus-visible outline maps (Avalonia's FocusAdorner); an outline at rest needs an adorner layer."),
            ("outline-style", "Only a :focus / :focus-visible outline maps (Avalonia's FocusAdorner); an outline at rest needs an adorner layer."),
            ("outline-color", "Only a :focus / :focus-visible outline maps (Avalonia's FocusAdorner); an outline at rest needs an adorner layer."),
            ("outline-offset", "Only a :focus / :focus-visible outline maps (Avalonia's FocusAdorner); an outline at rest needs an adorner layer."),
        };

        private static readonly string[] transition_properties = { "transition-property", "transition-duration", "transition-timing-function", "transition-delay",
            "animation-name", "animation-duration", "animation-timing-function", "animation-delay", "animation-iteration-count", "animation-direction", "animation-fill-mode", "animation-play-state" };

        private static readonly string[] issue_286_properties = {
            "border-style", "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius"
        };

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

        /// <summary>Every (selector, part, state, property) row.</summary>
        public static IReadOnlyList<AvaloniaThemeSupportEntry> Entries { get; }

        private static readonly Dictionary<(string Selector, string? Part, ThemeCssState State, string Property), AvaloniaThemeSupportEntry> index;
        private static readonly Dictionary<string, AvaloniaThemeSelectorMapping> mapping_index;

        /// <summary>
        /// Looks up the row for one declaration. Returns null when the selector has no Avalonia
        /// counterpart at all (see <see cref="FindMapping"/>).
        /// </summary>
        public static AvaloniaThemeSupportEntry? Find (string selector, string? part, bool hover, string property)
            => Find (selector, part, hover ? ThemeCssState.Hover : ThemeCssState.Normal, property);

        /// <summary>
        /// Looks up the row for one declaration in a given state. Returns null when the selector has no
        /// Avalonia counterpart, or does not take that state.
        /// </summary>
        public static AvaloniaThemeSupportEntry? Find (string selector, string? part, ThemeCssState state, string property)
        {
            index.TryGetValue ((selector.ToLowerInvariant (), part?.ToLowerInvariant (), state, property.ToLowerInvariant ()), out var entry);
            return entry;
        }

        /// <summary>
        /// Whether a state has at least one row that does something on Avalonia (Native or Approximate).
        /// Until it does, the applier reports a rule for the state once and skips it.
        /// </summary>
        /// <param name="selector">The selector.</param>
        /// <param name="state">The state to ask about.</param>
        public static bool AppliesInState (string selector, ThemeCssState state)
            => Entries.Any (e => e.State == state && e.Level != AvaloniaThemeSupportLevel.Unsupported
                && string.Equals (e.Selector, selector, StringComparison.OrdinalIgnoreCase));

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
                => entries.Any (e => e.Selector == selector && e.Part == part && e.State == (hover ? ThemeCssState.Hover : ThemeCssState.Normal) && e.Property == property);

            void Add (string selector, string? part, bool hover, string property, AvaloniaThemeSupportLevel level, string notes, IReadOnlyList<AvaloniaTarget> targets)
            {
                if (!Has (selector, part, hover, property))
                    entries.Add (new AvaloniaThemeSupportEntry (selector, part, hover, property, level, notes, targets));
            }

            // A selector with no Avalonia control, opted into by the app: elements marked with CssTheme.Selector take its rules. A marked
            // Border, Panel or templated control is restyled; the text colour and fonts are the inherited TextElement properties.
            void Marked (string selector, string notes)
            {
                Map (selector, "Border, Panel or templated control marked with CssTheme.Selector=\"" + selector + "\"", notes);

                var border = S.Marked<Border> (selector);
                var panel = S.Marked<Panel> (selector);
                var control = S.Marked<global::Avalonia.Controls.Primitives.TemplatedControl> (selector);

                void Row (string property, AvaloniaThemeSupportLevel level, string rowNotes, params AvaloniaTarget[] targets)
                    => entries.Add (new AvaloniaThemeSupportEntry (selector, null, ThemeCssState.Normal, property, level, rowNotes, targets));

                AvaloniaTarget[] All (global::Avalonia.AvaloniaProperty property) => new AvaloniaTarget[] { new StyleTarget (border, property), new StyleTarget (panel, property), new StyleTarget (control, property) };
                AvaloniaTarget[] BorderOrControl (global::Avalonia.AvaloniaProperty onBorder, global::Avalonia.AvaloniaProperty onControl, BorderSide side = BorderSide.All, CornerSide corner = CornerSide.All)
                    => new AvaloniaTarget[] { new StyleTarget (border, onBorder, side, corner), new StyleTarget (control, onControl, side, corner) };

                const string setter = "A setter in the generated Styles on the marked element.";
                const string noPanelBorder = " A marked Panel has no border.";

                Row ("background-color", AvaloniaThemeSupportLevel.Native, setter, new AvaloniaTarget[] {
                    new StyleTarget (border, Border.BackgroundProperty), new StyleTarget (panel, Panel.BackgroundProperty), new StyleTarget (control, P.Background) });
                Row ("color", AvaloniaThemeSupportLevel.Native, "TextElement.Foreground, which the text inside the marked element inherits.", All (global::Avalonia.Controls.Documents.TextElement.ForegroundProperty));
                Row ("font-family", AvaloniaThemeSupportLevel.Native, "TextElement.FontFamily, inherited by the text inside.", All (global::Avalonia.Controls.Documents.TextElement.FontFamilyProperty));
                Row ("font-size", AvaloniaThemeSupportLevel.Native, "TextElement.FontSize, inherited by the text inside.", All (global::Avalonia.Controls.Documents.TextElement.FontSizeProperty));
                Row ("font-weight", AvaloniaThemeSupportLevel.Native, "TextElement.FontWeight, inherited by the text inside.", All (global::Avalonia.Controls.Documents.TextElement.FontWeightProperty));
                Row ("font-style", AvaloniaThemeSupportLevel.Native, "TextElement.FontStyle, inherited by the text inside.", All (global::Avalonia.Controls.Documents.TextElement.FontStyleProperty));
                Row ("border-color", AvaloniaThemeSupportLevel.Native, setter + noPanelBorder, BorderOrControl (Border.BorderBrushProperty, P.BorderBrush));
                Row ("border-width", AvaloniaThemeSupportLevel.Native, setter + noPanelBorder, BorderOrControl (Border.BorderThicknessProperty, P.BorderThickness));
                Row ("border-radius", AvaloniaThemeSupportLevel.Native, setter + noPanelBorder, BorderOrControl (Border.CornerRadiusProperty, P.CornerRadius));

                foreach (var (property, side) in new[] { ("border-top-width", BorderSide.Top), ("border-right-width", BorderSide.Right), ("border-bottom-width", BorderSide.Bottom), ("border-left-width", BorderSide.Left) })
                    Row (property, AvaloniaThemeSupportLevel.Native, setter + noPanelBorder, BorderOrControl (Border.BorderThicknessProperty, P.BorderThickness, side));

                foreach (var property in side_colors)
                    Row (property, AvaloniaThemeSupportLevel.Approximate, "Avalonia controls have one BorderBrush; a side colour colours every side that has a width." + noPanelBorder, BorderOrControl (Border.BorderBrushProperty, P.BorderBrush));

                // Per-corner radii are added with the other selectors' (see below): they are copied from this border-radius row.
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

            // box-shadow (#285) has no entry in all_properties: several Styled() calls below pass that
            // array straight into P.For() to resolve an Avalonia style target, and box-shadow has none
            // (Avalonia's own BoxShadow property is a different shape -- blur and spread included --
            // and is not wired to this CSS property). It is added explicitly instead, in the trailing
            // sweep below, always Unsupported.
            // The #286 properties (per-corner radius, dashed borders) are like box-shadow: not in
            // all_properties, added explicitly below, always Unsupported. Avalonia's CornerRadius is
            // per-corner and could carry the radii, but nothing is wired to it yet.
            const string NoCornerRadiusTarget = "This control's corner radius is a template resource or fixed, not a CornerRadius setter, so there is no per-corner seam.";
            const string NoBorderStyleTarget = "Avalonia borders are solid; there is no dashed-border seam.";

            const string NoBoxShadowTarget = "The Fluent template of this control has no named root border or content presenter to put BoxShadows on.";
            // Avalonia has Transitions, but Fluent's state colours live in template resources, which a Transitions setter on the control
            // does not reach; a half-working mapping would animate some brushes and switch others.
            const string NoTransitionTarget = "This control's Fluent template has no named root part to put Transitions and Animations on.";

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

            Map ("DateTimePicker", "DatePicker, TimePicker", "The DatePicker*/TimePicker* button resources and fonts; the drop-down flyout follows the PopupWindow rules. CalendarDatePicker follows the TextBox rule.");
            Res ("DateTimePicker", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "The picker button's fill (DatePickerButtonBackground / TimePickerButtonBackground).",
                "DatePickerButtonBackground", "TimePickerButtonBackground");
            Res ("DateTimePicker", null, false, "color", AvaloniaThemeSupportLevel.Native, "The picker button's text.", "DatePickerButtonForeground", "TimePickerButtonForeground");
            Res ("DateTimePicker", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "The picker button's border.", "DatePickerButtonBorderBrush", "TimePickerButtonBorderBrush");
            Styled ("DateTimePicker", null, false, font_properties, StyleNote, S.DatePickerControl, S.TimePickerControl);

            Map ("PopupWindow", "FlyoutPresenter, ToolTip", "FlyoutPresenterBackground / FlyoutBorderThemeBrush and the ToolTip* resources.");
            Res ("PopupWindow", null, false, "background-color", AvaloniaThemeSupportLevel.Native, "The popup behind its content (FlyoutPresenterBackground / ToolTipBackground).",
                "FlyoutPresenterBackground", "ToolTipBackground");
            Res ("PopupWindow", null, false, "border-color", AvaloniaThemeSupportLevel.Native, "The popup's border (FlyoutBorderThemeBrush / ToolTipBorderBrush).",
                "FlyoutBorderThemeBrush", "ToolTipBorderBrush");
            Res ("PopupWindow", null, false, "color", AvaloniaThemeSupportLevel.Approximate, "A tool tip's text (ToolTipForeground); a flyout presenter draws no text of its own.", "ToolTipForeground");

            Map ("ProgressBar", "ProgressBar", "Style setters on the bar: Background is the track, Foreground the fill (--accent-color-2).");
            Styled ("ProgressBar", null, false, all_properties.Where (p => p != "color"), StyleNote, S.ProgressBar);
            Target ("ProgressBar", null, false, "color", AvaloniaThemeSupportLevel.Approximate,
                "ProgressBar.Foreground is the fill; Majorsilence.Forms fills with --accent-color-2 and does not read color, so this is an Avalonia-only way to recolour the fill.",
                new StyleTarget (S.ProgressBar, P.Foreground));

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
            Fill ("DataGridView", "header", false, "The header separators are filled with the grid-lines brush (shared with the cells) as a local value in the template, which no style or resource of their own reaches.");
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
            Marked ("GroupBox", "Avalonia has no GroupBox; mark your own Border, panel or HeaderedContentControl with CssTheme.Selector=\"GroupBox\".");
            Map ("NavigationPane", null, "Avalonia has no Outlook-style navigation pane.");
            Marked ("Panel", "Avalonia panels are layout primitives used inside every control template, so none is restyled unless the app marks it with CssTheme.Selector=\"Panel\".");
            Marked ("PictureBox", "Avalonia's Image has no background or border; mark the Border around it with CssTheme.Selector=\"PictureBox\".");
            Map ("PropertyGrid", null, "Avalonia has no PropertyGrid.");
            Map ("Ribbon", null, "Avalonia has no ribbon control.");
            Map ("FormTitleBar", null, "No counterpart: Avalonia windows draw their own chrome.");
            Map ("HostedSurface", null, "No counterpart: it is the surface Majorsilence.Forms embeds in an Avalonia host.");
            Map ("MdiClient", null, "No counterpart: Avalonia has no MDI workspace.");
            Map ("PrintPreviewControl", null, "No counterpart: Avalonia has no print preview control.");
            Map ("DockWindowBase", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RadCommandBar", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RadPdfViewerNavigator", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RadRibbonBar", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RadScheduler", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RadSchedulerNavigator", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RadStatusStrip", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Map ("RichTextEditorRibbonBar", null, "A Majorsilence.Forms.Telerik control with no Avalonia counterpart.");
            Marked ("StatusBar", "Avalonia has no status bar control; mark your own status row with CssTheme.Selector=\"StatusBar\".");
            Marked ("StatusStrip", "Avalonia has no status bar control; mark your own status row with CssTheme.Selector=\"StatusStrip\".");
            Marked ("ToolBar", "Avalonia has no tool bar control; mark your own tool row with CssTheme.Selector=\"ToolBar\".");

            // ---- States ------------------------------------------------------------------------------------------
            // Fluent has a resource per state: pressed (:active), disabled and, for text boxes, focused. Buttons and links have no
            // focused colours of their own, so :focus and :focus-visible are style setters on the template's content presenter,
            // reached through Avalonia's :focus and :focus-visible pseudo-classes.
            void StateRes (string selector, ThemeCssState state, string property, AvaloniaThemeSupportLevel level, string notes, params string[] keys)
                => entries.Add (new AvaloniaThemeSupportEntry (selector, null, state, property, level, notes, Targets.Exact (keys)));

            void StateTarget (string selector, ThemeCssState state, string property, AvaloniaThemeSupportLevel level, string notes, params AvaloniaTarget[] targets)
                => entries.Add (new AvaloniaThemeSupportEntry (selector, null, state, property, level, notes, targets));

            void Colours3 (string selector, ThemeCssState state, string what, string[] background, string[] foreground, string[] border)
            {
                StateRes (selector, state, "background-color", AvaloniaThemeSupportLevel.Native, $"The {what} fill.", background);
                StateRes (selector, state, "color", AvaloniaThemeSupportLevel.Native, $"The {what} text colour.", foreground);
                StateRes (selector, state, "border-color", AvaloniaThemeSupportLevel.Native, $"The {what} border.", border);
            }

            void PartColours (string selector, ThemeCssState state, string what, StyleSelector part)
            {
                StateTarget (selector, state, "background-color", AvaloniaThemeSupportLevel.Native, $"The {what} fill, on the template's content presenter.", new StyleTarget (part, ContentPresenter.BackgroundProperty));
                StateTarget (selector, state, "color", AvaloniaThemeSupportLevel.Native, $"The {what} text colour, on the template's content presenter.", new StyleTarget (part, ContentPresenter.ForegroundProperty));
                StateTarget (selector, state, "border-color", AvaloniaThemeSupportLevel.Native, $"The {what} border, on the template's content presenter.", new StyleTarget (part, ContentPresenter.BorderBrushProperty));
            }

            Colours3 ("Button", ThemeCssState.Active, "pressed", new[] { "ButtonBackgroundPressed", "ToggleButtonBackgroundPressed" },
                new[] { "ButtonForegroundPressed", "ToggleButtonForegroundPressed" }, new[] { "ButtonBorderBrushPressed", "ToggleButtonBorderBrushPressed" });
            Colours3 ("Button", ThemeCssState.Disabled, "disabled", new[] { "ButtonBackgroundDisabled", "ToggleButtonBackgroundDisabled" },
                new[] { "ButtonForegroundDisabled", "ToggleButtonForegroundDisabled" }, new[] { "ButtonBorderBrushDisabled", "ToggleButtonBorderBrushDisabled" });
            PartColours ("Button", ThemeCssState.Focus, "focused", S.ButtonFocus);
            PartColours ("Button", ThemeCssState.FocusVisible, "keyboard-focused", S.ButtonFocusVisible);

            Colours3 ("LinkLabel", ThemeCssState.Active, "pressed", new[] { "HyperlinkButtonBackgroundPressed" }, new[] { "HyperlinkButtonForegroundPressed" }, new[] { "HyperlinkButtonBorderBrushPressed" });
            Colours3 ("LinkLabel", ThemeCssState.Disabled, "disabled", new[] { "HyperlinkButtonBackgroundDisabled" }, new[] { "HyperlinkButtonForegroundDisabled" }, new[] { "HyperlinkButtonBorderBrushDisabled" });
            PartColours ("LinkLabel", ThemeCssState.Focus, "focused", S.HyperlinkFocus);
            PartColours ("LinkLabel", ThemeCssState.FocusVisible, "keyboard-focused", S.HyperlinkFocusVisible);

            // Fluent's text box has one focused look; the core treats a focused text box as :focus-visible too, so both states write it.
            foreach (var state in new[] { ThemeCssState.Focus, ThemeCssState.FocusVisible }) {
                StateRes ("TextBox", state, "background-color", AvaloniaThemeSupportLevel.Native, "The focused fill (TextControlBackgroundFocused).", "TextControlBackgroundFocused");
                StateRes ("TextBox", state, "color", AvaloniaThemeSupportLevel.Native, "The focused text colour (TextControlForegroundFocused).", "TextControlForegroundFocused");
                StateRes ("TextBox", state, "border-color", AvaloniaThemeSupportLevel.Native, "The focused border (TextControlBorderBrushFocused), which replaces Fluent's accent border.", "TextControlBorderBrushFocused");
            }

            StateRes ("TrackBar", ThemeCssState.Active, "background-color", AvaloniaThemeSupportLevel.Native, "The pressed container (SliderContainerBackgroundPressed).", "SliderContainerBackgroundPressed");
            StateRes ("TrackBar", ThemeCssState.Disabled, "background-color", AvaloniaThemeSupportLevel.Native, "The disabled container (SliderContainerBackgroundDisabled).", "SliderContainerBackgroundDisabled");

            // ---- Paint effects (#285, #286) ----------------------------------------------------------------------
            // opacity, box-shadow, background-image and outline: Avalonia has a property or a brush type for each; what is missing is
            // a name for the template part to put it on, so each is wired to the parts the Fluent templates name.
            var states = new[] { ThemeCssState.Hover, ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus, ThemeCssState.FocusVisible };

            // The style selectors a rule for a control as a whole lands on: whatever its border, font or fill setters already use.
            List<StyleSelector> ControlSelectors (string selector)
            {
                foreach (var property in new[] { "border-width", "font-family", "background-color" }) {
                    var found = entries.Where (e => e.Selector == selector && e.Part is null && e.State == ThemeCssState.Normal && e.Property == property)
                        .SelectMany (e => e.Targets.OfType<StyleTarget> ()).Select (t => t.Selector).Distinct ().ToList ();
                    if (found.Count > 0)
                        return found;
                }

                return new List<StyleSelector> ();
            }

            void Effect (string selector, ThemeCssState state, string property, AvaloniaThemeSupportLevel level, string notes, IEnumerable<AvaloniaTarget> targets)
            {
                if (!entries.Any (e => e.Selector == selector && e.Part is null && e.State == state && e.Property == property))
                    entries.Add (new AvaloniaThemeSupportEntry (selector, null, state, property, level, notes, targets.ToList ()));
            }

            // opacity: the control's Opacity -- children included, as in the core -- at rest and in every state it has.
            foreach (var mapping in mappings.ToArray ()) {
                if (mapping.AvaloniaTypes is null || ThemeCssReference.FindSelector (mapping.Selector) is not { } selector)
                    continue;

                var controls = ControlSelectors (mapping.Selector);
                if (mapping.Selector == "TrackBar")
                    controls = new List<StyleSelector> { S.Slider };
                else if (mapping.Selector == "ScrollBar")
                    controls = new List<StyleSelector> { S.ScrollBar };

                if (controls.Count == 0)
                    continue;

                Effect (mapping.Selector, ThemeCssState.Normal, "opacity", AvaloniaThemeSupportLevel.Native,
                    "The control's Opacity, children included (a setter in the generated Styles).", controls.Select (c => (AvaloniaTarget) new StyleTarget (c, P.Opacity)));

                foreach (var state in states)
                    if (selector.Supports (state))
                        Effect (mapping.Selector, state, "opacity", AvaloniaThemeSupportLevel.Native,
                            $"The control's Opacity while it matches Avalonia's {S.ClassOf (state)} pseudo-class.",
                            controls.Select (c => (AvaloniaTarget) new StyleTarget (S.InState (c, state), P.Opacity)));
            }

            // box-shadow: BoxShadows on the template's root border or content presenter, at rest and in every state.
            void Shadow (string selector, global::Avalonia.AvaloniaProperty property, params Func<ThemeCssState, StyleSelector>[] parts)
            {
                const string note = "BoxShadows on the template's root part (outer and inset, a list), outside the control's box like the core's shadow but not shrinking it.";
                var info = ThemeCssReference.FindSelector (selector)!;
                Effect (selector, ThemeCssState.Normal, "box-shadow", AvaloniaThemeSupportLevel.Native, note, parts.Select (p => (AvaloniaTarget) new StyleTarget (p (ThemeCssState.Normal), property)));

                foreach (var state in states)
                    if (info.Supports (state))
                        Effect (selector, state, "box-shadow", AvaloniaThemeSupportLevel.Native, note, parts.Select (p => (AvaloniaTarget) new StyleTarget (p (state), property)));
            }

            Shadow ("Button", P.PresenterBoxShadow, S.ButtonPart, S.ToggleButtonPart);
            Shadow ("LinkLabel", P.PresenterBoxShadow, S.HyperlinkPart);
            Shadow ("TextBox", P.BorderBoxShadow, S.TextBoxPart, S.DatePickerPart);
            Shadow ("ComboBox", P.BorderBoxShadow, S.ComboBoxPart);
            Shadow ("ListBox", P.BorderBoxShadow, S.ListBoxPart);
            Shadow ("DataGridView", P.BorderBoxShadow, S.DataGridPart);
            Shadow ("ProgressBar", P.BorderBoxShadow, S.ProgressBarPart);

            // background-image: the gradient goes where the fill goes -- the same resource or setter -- in every state that has one.
            foreach (var fill in entries.Where (e => e.Part is null && e.Property == "background-color" && e.Level != AvaloniaThemeSupportLevel.Unsupported && e.Targets.Count > 0).ToList ())
                Effect (fill.Selector, fill.State, "background-image", AvaloniaThemeSupportLevel.Approximate,
                    "A gradient brush where the fill goes (the same resource or setter): the first layer only, in relative units, so a diagonal bends a little on a non-square box; stops in pixels are spaced evenly and a repeating gradient repeats its stop span.",
                    fill.Targets.Select (t => t is ResourceTarget r ? new ResourceTarget (r.Key, r.Kind) : t));

            // outline: Avalonia draws the keyboard-focus ring from the control's FocusAdorner, which is a ring outside the control that
            // takes no room -- the CSS focus outline. It has no other outlines.
            void Outline (string selector, params StyleSelector[] controls)
            {
                var info = ThemeCssReference.FindSelector (selector)!;

                foreach (var state in new[] { ThemeCssState.Focus, ThemeCssState.FocusVisible })
                    if (info.Supports (state))
                        foreach (var property in new[] { "outline-width", "outline-style", "outline-color", "outline-offset" })
                            Effect (selector, state, property, AvaloniaThemeSupportLevel.Approximate,
                                "Drawn as the control's FocusAdorner -- a ring outside it that takes no room -- which Avalonia shows for keyboard focus only; the ring is solid with a fixed corner radius.",
                                controls.Select (c => (AvaloniaTarget) new StyleTarget (c, P.FocusAdorner)));
            }

            Outline ("Button", S.Button, S.ToggleButton);
            Outline ("LinkLabel", S.Hyperlink);
            Outline ("TextBox", S.TextBox, S.DatePicker);
            Outline ("TrackBar", S.Slider);

            // transition-* and animation-*: Avalonia's Transitions and Animations, put on the template parts the colours change on (see
            // AvaloniaThemeApplier.Motion.cs), per state. Only the controls whose template parts are named have them.
            const string MotionNote = "Avalonia Transitions / Animations on the same template part and state selector the fill, border, shadow and radius are set on, with the rule's easing; the opacity and, where the part is a Border, the text colour move on the control. Fractional iteration counts round up and a paused animation still plays.";
            foreach (var spec in new[] { "Button", "LinkLabel", "TextBox", "ComboBox", "ListBox", "DataGridView", "ProgressBar" }) {
                var info = ThemeCssReference.FindSelector (spec)!;

                // The grammar only takes a transition on the controls that have states; an animation runs on any control.
                foreach (var state in new[] { ThemeCssState.Normal }.Concat (states))
                    if (state == ThemeCssState.Normal || info.Supports (state))
                        foreach (var property in transition_properties.Where (p => p.StartsWith ("animation-", StringComparison.Ordinal) || info.HasStates))
                            Effect (spec, state, property, AvaloniaThemeSupportLevel.Approximate, MotionNote, new AvaloniaTarget[] { new MotionTarget () });
            }

            // Per-corner radii (#286): Avalonia's CornerRadius has four corners, so wherever border-radius is a style setter the
            // corner longhands are too, on the same selectors.
            foreach (var radius in entries.Where (e => e.Part is null && e.State == ThemeCssState.Normal && e.Property == "border-radius"
                                                       && e.Level == AvaloniaThemeSupportLevel.Native && e.Targets.OfType<StyleTarget> ().Any ()).ToList ())
                foreach (var (property, corner) in new[] {
                             ("border-top-left-radius", CornerSide.TopLeft), ("border-top-right-radius", CornerSide.TopRight),
                             ("border-bottom-right-radius", CornerSide.BottomRight), ("border-bottom-left-radius", CornerSide.BottomLeft) })
                    entries.Add (new AvaloniaThemeSupportEntry (radius.Selector, null, ThemeCssState.Normal, property, AvaloniaThemeSupportLevel.Native, StyleNote,
                        radius.Targets.OfType<StyleTarget> ().Select (t => (AvaloniaTarget) new StyleTarget (t.Selector, t.Property, corner: corner)).ToList ()));

            // Anything the explicit rows above left open on a mapped selector is unsupported.
            foreach (var selector in ThemeCssReference.Selectors) {
                var mapping = mappings.FirstOrDefault (m => m.Selector == selector.Name);
                if (mapping?.AvaloniaTypes is null)
                    continue;

                Fill (selector.Name, null, false, "No Avalonia seam for this property on this control.");
                if (selector.SupportsHover)
                    Fill (selector.Name, null, true, HoverColoursOnly);

                Add (selector.Name, null, false, "box-shadow", AvaloniaThemeSupportLevel.Unsupported, NoBoxShadowTarget, Array.Empty<AvaloniaTarget> ());
                if (selector.SupportsHover)
                    Add (selector.Name, null, true, "box-shadow", AvaloniaThemeSupportLevel.Unsupported, NoBoxShadowTarget, Array.Empty<AvaloniaTarget> ());

                foreach (var (property, note) in effect_properties) {
                    Add (selector.Name, null, false, property, AvaloniaThemeSupportLevel.Unsupported, note, Array.Empty<AvaloniaTarget> ());
                    if (selector.SupportsHover)
                        Add (selector.Name, null, true, property, AvaloniaThemeSupportLevel.Unsupported, note, Array.Empty<AvaloniaTarget> ());
                }

                foreach (var property in transition_properties) {
                    Add (selector.Name, null, false, property, AvaloniaThemeSupportLevel.Unsupported, NoTransitionTarget, Array.Empty<AvaloniaTarget> ());
                    if (selector.SupportsHover)
                        Add (selector.Name, null, true, property, AvaloniaThemeSupportLevel.Unsupported, NoTransitionTarget, Array.Empty<AvaloniaTarget> ());
                }

                foreach (var property in issue_286_properties) {
                    var note = property == "border-style" ? NoBorderStyleTarget : NoCornerRadiusTarget;

                    Add (selector.Name, null, false, property, AvaloniaThemeSupportLevel.Unsupported, note, Array.Empty<AvaloniaTarget> ());
                    if (selector.SupportsHover)
                        Add (selector.Name, null, true, property, AvaloniaThemeSupportLevel.Unsupported, note, Array.Empty<AvaloniaTarget> ());
                }

                foreach (var part in selector.Parts) {
                    Fill (selector.Name, part.Name, false, "No Avalonia seam for this property on this part.");
                    if (part.SupportsHover)
                        Fill (selector.Name, part.Name, true, HoverColoursOnly);
                }
            }

            // The other pseudo-classes (#285): only :hover had rows, so :active, :disabled, :focus and
            // :focus-visible were invisible to the completeness tests. One row per longhand, in every state the
            // control takes, Unsupported until a state gets its seam (phase 2 of docs/theming-parity-plan.md).
            const string NoStateTarget = "Fluent has no resource or pseudo-class seam for this state on this control.";

            foreach (var mapping in mappings.ToArray ()) {
                if (mapping.AvaloniaTypes is null || ThemeCssReference.FindSelector (mapping.Selector) is not { } selector)
                    continue;

                var properties = entries.Where (e => e.Selector == mapping.Selector && e.Part is null && e.State == ThemeCssState.Normal).Select (e => e.Property).ToArray ();

                foreach (var state in new[] { ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus, ThemeCssState.FocusVisible })
                    if (selector.Supports (state)) {
                        var answered = entries.Where (e => e.Selector == mapping.Selector && e.Part is null && e.State == state).Select (e => e.Property).ToHashSet ();
                        var note = answered.Count > 0 ? "Avalonia maps only some properties in this state; the rest comes from the normal rule." : NoStateTarget;

                        foreach (var property in properties)
                            if (!answered.Contains (property))
                                entries.Add (new AvaloniaThemeSupportEntry (mapping.Selector, null, state, property, AvaloniaThemeSupportLevel.Unsupported, note, Array.Empty<AvaloniaTarget> ()));
                    }
            }

            Mappings = mappings;
            Entries = entries;

            index = entries.ToDictionary (e => (e.Selector.ToLowerInvariant (), e.Part?.ToLowerInvariant (), e.State, e.Property.ToLowerInvariant ()));
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
            => $"{e.Selector}{(e.Part is null ? "" : "::" + e.Part)}{AvaloniaThemeSupportEntry.StateText (e.State)}";

        private static string LevelText (AvaloniaThemeSupportLevel level) => level switch {
            AvaloniaThemeSupportLevel.Native => "native",
            AvaloniaThemeSupportLevel.Approximate => "approximate",
            _ => "unsupported"
        };

        private static string Escape (string text) => text.Replace ("|", "\\|");
    }
}
