using System.Collections.Generic;
using SkiaSharp;

namespace Majorsilence.Forms.Theming.Avalonia
{
    /// <summary>One declaration the tokens imply for a control that has no rule of its own for it.</summary>
    internal readonly struct TokenDefault
    {
        public TokenDefault (string selector, string? part, ThemeCssState state, string property, SKColor color)
        {
            Selector = selector;
            Part = part;
            State = state;
            Property = property;
            Argb = (uint) color;
        }

        public string Selector { get; }
        public string? Part { get; }
        public ThemeCssState State { get; }
        public string Property { get; }
        public uint Argb { get; }
    }

    /// <summary>
    /// The colours the Majorsilence.Forms controls take from the theme tokens when no rule says otherwise -- the button face is
    /// <c>--control-mid-color</c>, the paper of a list or input <c>--control-low-color</c>, borders <c>--border-low-color</c> -- as
    /// declarations. <see cref="AvaloniaThemeApplier"/> runs them through the same support matrix as a parsed rule's, before the
    /// sheet's own rules, so a sheet that sets only tokens restyles native Avalonia controls the way it restyles ours, and an
    /// explicit rule still wins. The table follows the "Read by" column of <c>docs/theming.md</c> and the defaults
    /// <c>Majorsilence.Forms.Theming.WinForms</c> already applies.
    /// </summary>
    internal static class AvaloniaTokenDefaults
    {
        /// <summary>
        /// Fluent resources that carry the disabled text colour. A disabled Majorsilence.Forms control draws its text in
        /// <c>--foreground-disabled-color</c> whether or not a rule says so, so these follow the token; a <c>:disabled</c> rule
        /// writes the same keys afterwards and wins.
        /// </summary>
        public static readonly string[] DisabledForegroundKeys = {
            "ButtonForegroundDisabled", "ToggleButtonForegroundDisabled", "HyperlinkButtonForegroundDisabled", "TextControlForegroundDisabled",
            "ComboBoxForegroundDisabled", "CheckBoxForegroundUncheckedDisabled", "RadioButtonForegroundDisabled", "DatePickerButtonForegroundDisabled",
            "MenuFlyoutItemForegroundDisabled", "TreeViewItemForegroundDisabled",
        };

        public static IReadOnlyList<TokenDefault> Read ()
        {
            var list = new List<TokenDefault> ();

            void Add (string selector, string? part, ThemeCssState state, string property, SKColor color)
                => list.Add (new TokenDefault (selector, part, state, property, color));

            void Face (string selector, SKColor background, SKColor border)
            {
                Add (selector, null, ThemeCssState.Normal, "background-color", background);
                Add (selector, null, ThemeCssState.Normal, "color", Theme.ForegroundColor);
                Add (selector, null, ThemeCssState.Normal, "border-color", border);
            }

            Face ("Button", Theme.ControlMidColor, Theme.BorderLowColor);
            Add ("Button", null, ThemeCssState.Hover, "background-color", Theme.AccentColor);
            Add ("Button", null, ThemeCssState.Hover, "color", Theme.ForegroundColorOnAccent);
            Add ("Button", null, ThemeCssState.Hover, "border-color", Theme.AccentColor2);

            Add ("CheckBox", null, ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("RadioButton", null, ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("Label", null, ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("LinkLabel", null, ThemeCssState.Normal, "color", Theme.AccentColor);

            Face ("TextBox", Theme.ControlLowColor, Theme.BorderLowColor);
            Face ("DateTimePicker", Theme.ControlLowColor, Theme.BorderLowColor);
            Face ("ComboBox", Theme.ControlMidColor, Theme.BorderLowColor);
            Face ("NumericUpDown", Theme.ControlMidColor, Theme.BorderLowColor);

            Face ("ListBox", Theme.ControlLowColor, Theme.BorderLowColor);
            Add ("ListBox", "selection", ThemeCssState.Normal, "background-color", Theme.ControlHighlightLowColor);
            Add ("ListBox", "selection", ThemeCssState.Normal, "color", Theme.ForegroundColor);

            Face ("TreeView", Theme.ControlLowColor, Theme.BorderLowColor);
            Add ("TreeView", "selection", ThemeCssState.Normal, "background-color", Theme.ControlHighlightLowColor);
            Add ("TreeView", "selection", ThemeCssState.Normal, "color", Theme.ForegroundColor);

            Face ("DataGridView", Theme.ControlLowColor, Theme.BorderLowColor);
            Add ("DataGridView", "header", ThemeCssState.Normal, "background-color", Theme.ControlLowColor);
            Add ("DataGridView", "header", ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("DataGridView", "selection", ThemeCssState.Normal, "background-color", Theme.AccentColor);
            Add ("DataGridView", "selection", ThemeCssState.Normal, "color", Theme.ForegroundColorOnAccent);

            Add ("Menu", null, ThemeCssState.Normal, "background-color", Theme.BackgroundColor);
            Add ("Menu", null, ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("Menu", "item", ThemeCssState.Hover, "background-color", Theme.ControlHighlightLowColor);
            Add ("Menu", "item", ThemeCssState.Hover, "color", Theme.ForegroundColor);

            Add ("MenuDropDown", null, ThemeCssState.Normal, "background-color", Theme.ControlLowColor);
            Add ("MenuDropDown", null, ThemeCssState.Normal, "border-color", Theme.BorderLowColor);
            Add ("MenuDropDown", null, ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("MenuDropDown", "item", ThemeCssState.Hover, "background-color", Theme.ControlHighlightLowColor);
            Add ("MenuDropDown", "item", ThemeCssState.Hover, "color", Theme.ForegroundColor);

            Add ("TabStrip", "selected", ThemeCssState.Normal, "background-color", Theme.ControlLowColor);
            Add ("TabStrip", "selected", ThemeCssState.Normal, "color", Theme.ForegroundColor);
            Add ("TabStrip", "selected", ThemeCssState.Normal, "border-bottom-color", Theme.AccentColor2);
            Add ("TabStrip", "item", ThemeCssState.Normal, "color", Theme.ForegroundColor);

            Add ("ProgressBar", null, ThemeCssState.Normal, "background-color", Theme.ControlMidHighColor);
            Add ("ProgressBar", null, ThemeCssState.Normal, "color", Theme.AccentColor2);
            Add ("ProgressBar", null, ThemeCssState.Normal, "border-color", Theme.BorderLowColor);

            Add ("PopupWindow", null, ThemeCssState.Normal, "background-color", Theme.ControlLowColor);
            Add ("PopupWindow", null, ThemeCssState.Normal, "border-color", Theme.BorderLowColor);
            Add ("PopupWindow", null, ThemeCssState.Normal, "color", Theme.ForegroundColor);

            return list;
        }
    }
}
