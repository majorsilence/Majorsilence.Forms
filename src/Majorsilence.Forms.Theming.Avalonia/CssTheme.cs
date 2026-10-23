namespace Majorsilence.Forms.Theming.Avalonia
{
    using global::Avalonia;
    using global::Avalonia.Controls;

    /// <summary>
    /// Opts one of an app's own elements into a Majorsilence.Forms selector that has no Avalonia control: a status row, a tool row, a
    /// panel, a group frame, a picture frame. Avalonia deliberately gives no theme to its layout primitives (they are used inside every
    /// control template, so a rule on <c>Panel</c> would restyle their internals), so nothing is restyled unless the app asks:
    /// <code>&lt;Border theme:CssTheme.Selector="StatusBar" /&gt;</code>
    /// A marked <see cref="Border"/>, <see cref="Panel"/> or templated control takes the rules of that selector -- background, text
    /// colour and fonts, and (not on a panel) border and corner radius -- as <see cref="AvaloniaCssTheme"/> applies them.
    /// </summary>
    public sealed class CssTheme : AvaloniaObject
    {
        /// <summary>
        /// The selector the element takes its rules from: <c>Panel</c>, <c>GroupBox</c>, <c>StatusBar</c>, <c>StatusStrip</c>,
        /// <c>ToolBar</c> or <c>PictureBox</c> (the Majorsilence.Forms control types Avalonia has no counterpart for).
        /// </summary>
        public static readonly AttachedProperty<string?> SelectorProperty = AvaloniaProperty.RegisterAttached<CssTheme, Control, string?> ("Selector");

        /// <summary>Gets the selector an element is marked with, or null.</summary>
        /// <param name="element">The element.</param>
        public static string? GetSelector (Control element)
        {
            ArgumentNullException.ThrowIfNull (element);
            return element.GetValue (SelectorProperty);
        }

        /// <summary>Marks an element with a selector (see <see cref="SelectorProperty"/>), or clears the mark with null.</summary>
        /// <param name="element">The element.</param>
        /// <param name="value">The selector name.</param>
        public static void SetSelector (Control element, string? value)
        {
            ArgumentNullException.ThrowIfNull (element);
            element.SetValue (SelectorProperty, value);
        }
    }
}
