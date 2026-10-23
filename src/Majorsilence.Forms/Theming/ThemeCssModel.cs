using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A resolved CSS value in host-neutral terms: colours as ARGB, lengths as whole pixels, fonts as
    /// family lists, weights as 100–900, slants as the CSS keyword. No SkiaSharp types appear here, so a
    /// host that does not draw with Skia (System.Windows.Forms, Avalonia) can consume a parsed theme
    /// without translating through ours.
    /// </summary>
    public sealed partial class ThemeCssValue
    {
        private ThemeCssValue (ThemeCssValueKind kind)
        {
            Kind = kind;
            FontFamilies = Array.Empty<string> ();
            FontStyle = "normal";
        }

        /// <summary>Which of the value members is meaningful.</summary>
        public ThemeCssValueKind Kind { get; }

        /// <summary>For <see cref="ThemeCssValueKind.Color"/>: the colour as 0xAARRGGBB.</summary>
        public uint Argb { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.Color"/>: the alpha channel, 0–255.</summary>
        public byte Alpha => (byte) (Argb >> 24);

        /// <summary>For <see cref="ThemeCssValueKind.Color"/>: the red channel, 0–255.</summary>
        public byte Red => (byte) (Argb >> 16);

        /// <summary>For <see cref="ThemeCssValueKind.Color"/>: the green channel, 0–255.</summary>
        public byte Green => (byte) (Argb >> 8);

        /// <summary>For <see cref="ThemeCssValueKind.Color"/>: the blue channel, 0–255.</summary>
        public byte Blue => (byte) Argb;

        /// <summary>
        /// For <see cref="ThemeCssValueKind.Length"/>: whole logical pixels. For
        /// <see cref="ThemeCssValueKind.BoxShadow"/>: the horizontal offset (the vertical offset is
        /// <see cref="OffsetY"/> and the colour is <see cref="Argb"/>).
        /// </summary>
        public int Pixels { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.Keyword"/>: the keyword (<c>solid</c> or <c>dashed</c>), lower case.</summary>
        public string Keyword => FontStyle;

        /// <summary>For <see cref="ThemeCssValueKind.BoxShadow"/>: the vertical offset in pixels.</summary>
        public int OffsetY { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.BoxShadow"/>: the blur radius in pixels (0 for a hard edge).</summary>
        public int Blur { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.BoxShadow"/>: the spread in pixels.</summary>
        public int Spread { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.BoxShadow"/>: whether the shadow is <c>inset</c>.</summary>
        public bool Inset { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.FontFamily"/>: the family list in preference order, as written.</summary>
        public IReadOnlyList<string> FontFamilies { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.FontWeight"/>: the CSS weight, a multiple of 100 from 100 to 900 (400 normal, 700 bold).</summary>
        public int FontWeight { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.FontStyle"/>: <c>normal</c>, <c>italic</c> or <c>oblique</c>.</summary>
        public string FontStyle { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.Image"/>: the gradient as CSS spells it.</summary>
        public string Css => FontStyle;

        /// <summary>For <see cref="ThemeCssValueKind.Number"/>: the number.</summary>
        public double Number { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.Time"/>: the time in milliseconds (a delay may be negative).</summary>
        public double Milliseconds { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.TimingFunction"/>: the function as CSS spells it, e.g. <c>ease-in-out</c> or <c>cubic-bezier(0.22, 1, 0.36, 1)</c>.</summary>
        public string TimingFunction => FontStyle;

        /// <summary>For <see cref="ThemeCssValueKind.List"/>: the elements, in order.</summary>
        public IReadOnlyList<ThemeCssValue> Items { get; private set; } = Array.Empty<ThemeCssValue> ();

        internal static ThemeCssValue Color (uint argb) => new (ThemeCssValueKind.Color) { Argb = argb };

        /// <summary>
        /// A colour value, for a host applier that synthesises declarations of its own (for example defaults derived from the
        /// theme tokens) and runs them through the same code as a parsed rule's.
        /// </summary>
        /// <param name="argb">The colour as 0xAARRGGBB.</param>
        public static ThemeCssValue FromColor (uint argb) => Color (argb);

        internal static ThemeCssValue Length (int pixels) => new (ThemeCssValueKind.Length) { Pixels = pixels };

        internal static ThemeCssValue Families (IReadOnlyList<string> families) => new (ThemeCssValueKind.FontFamily) { FontFamilies = families };

        internal static ThemeCssValue Weight (int weight) => new (ThemeCssValueKind.FontWeight) { FontWeight = weight };

        internal static ThemeCssValue Style (string style) => new (ThemeCssValueKind.FontStyle) { FontStyle = style };

        internal static ThemeCssValue KeywordOf (string keyword) => new (ThemeCssValueKind.Keyword) { FontStyle = keyword };

        internal static ThemeCssValue Images (IReadOnlyList<ControlGradient> layers)
            => layers.Count == 1 ? ImageOf (layers[0])
                : ListOf (layers.Select (ImageOf).ToArray ());

        internal static ThemeCssValue NumberOf (double number) => new (ThemeCssValueKind.Number) { Number = number };

        internal static ThemeCssValue Time (TimeSpan time) => new (ThemeCssValueKind.Time) { Milliseconds = time.TotalMilliseconds };

        internal static ThemeCssValue Timing (ControlTimingFunction timing) => new (ThemeCssValueKind.TimingFunction) { FontStyle = timing.Css };

        internal static ThemeCssValue ListOf (IReadOnlyList<ThemeCssValue> items) => new (ThemeCssValueKind.List) { Items = items };

        internal static ThemeCssValue Shadow (ControlBoxShadow shadow) => new (ThemeCssValueKind.BoxShadow) {
            Pixels = shadow.OffsetX, OffsetY = shadow.OffsetY, Blur = shadow.Blur, Spread = shadow.Spread, Inset = shadow.Inset, Argb = (uint) shadow.Color
        };

        // One shadow is a BoxShadow value, as it always was; several (or 'none') are a list of them.
        internal static ThemeCssValue Shadows (IReadOnlyList<ControlBoxShadow> shadows)
            => shadows.Count == 1 ? Shadow (shadows[0]) : ListOf (shadows.Select (Shadow).ToArray ());

        /// <summary>The value spelled the way the stylesheet would write it.</summary>
        public override string ToString ()
        {
            switch (Kind) {
                case ThemeCssValueKind.Color:
                    return FormatColor ();
                case ThemeCssValueKind.Length:
                    return Pixels.ToString (CultureInfo.InvariantCulture) + "px";
                case ThemeCssValueKind.FontFamily:
                    return string.Join (", ", FontFamilies.Select (f => f.Contains (' ') || f.Contains (',') ? "\"" + f + "\"" : f));
                case ThemeCssValueKind.FontWeight:
                    return FontWeight == 400 ? "normal" : FontWeight == 700 ? "bold" : FontWeight.ToString (CultureInfo.InvariantCulture);
                case ThemeCssValueKind.Keyword:
                    return FontStyle;
                case ThemeCssValueKind.BoxShadow:
                    return $"{(Inset ? "inset " : "")}{Pixels}px {OffsetY}px{(Blur != 0 || Spread != 0 ? $" {Blur}px" : "")}{(Spread != 0 ? $" {Spread}px" : "")} {FormatColor ()}";
                case ThemeCssValueKind.Number:
                    return Number.ToString ("0.###", CultureInfo.InvariantCulture);
                case ThemeCssValueKind.Time:
                    return Milliseconds.ToString ("0.###", CultureInfo.InvariantCulture) + "ms";
                case ThemeCssValueKind.List:
                    return Items.Count == 0 ? "none" : string.Join (", ", Items.Select (i => i.ToString ()));
                default:
                    return FontStyle;
            }
        }

        private string FormatColor ()
            => Alpha == 255
                ? $"#{Red:x2}{Green:x2}{Blue:x2}"
                : $"#{Red:x2}{Green:x2}{Blue:x2}{Alpha:x2}";
    }

    /// <summary>
    /// One <c>property: value;</c> inside a control rule, after shorthands were expanded and
    /// <c>var()</c> references resolved. <see cref="Value"/> is read live: when the declaration
    /// references a theme token (<see cref="TokenReference"/> is set) it reflects the token's current
    /// value, so a host applier that re-runs on <see cref="Theme.ThemeChanged"/> tracks token edits
    /// exactly as the built-in renderers do.
    /// </summary>
    public sealed class ThemeCssDeclaration
    {
        private readonly Func<ThemeCssValue> _resolve;

        internal ThemeCssDeclaration (string property, Func<ThemeCssValue> resolve, ThemeCssToken? tokenReference, int line, int column)
        {
            Property = property;
            _resolve = resolve;
            TokenReference = tokenReference;
            Line = line;
            Column = column;
        }

        /// <summary>The longhand property name, e.g. <c>background-color</c>, <c>border-top-width</c>, <c>font-family</c>.</summary>
        public string Property { get; }

        /// <summary>The current value (live when <see cref="TokenReference"/> is set).</summary>
        public ThemeCssValue Value => _resolve ();

        /// <summary>The theme token the value was written as <c>var(--token)</c> of, or null for a literal.</summary>
        public ThemeCssToken? TokenReference { get; }

        /// <summary>The 1-based line of the declaration in the source.</summary>
        public int Line { get; }

        /// <summary>The 1-based column of the declaration in the source.</summary>
        public int Column { get; }

        /// <inheritdoc/>
        public override string ToString () => $"{Property}: {(TokenReference is null ? Value.ToString () : $"var({TokenReference.Name})")};";
    }

    /// <summary>
    /// One control rule of a parsed stylesheet: a selector, which (if any) pseudo-class variant it is,
    /// and its declarations in source order (later declarations win). A comma list of selectors
    /// produces one rule per selector, sharing the declarations. At most one of <see cref="Hover"/>,
    /// <see cref="Active"/>, <see cref="Disabled"/> and <see cref="Focus"/> is ever true (#285): the
    /// grammar accepts one pseudo-class per selector, e.g. <c>Button:active</c>, never a combination.
    /// </summary>
    public sealed partial class ThemeCssRule
    {
        internal ThemeCssRule (ThemeCssSelector selector, ThemeCssPart? part, bool hover, bool active, bool disabled, bool focus, IReadOnlyList<ThemeCssDeclaration> declarations, int line, int column)
        {
            Selector = selector;
            Part = part;
            Hover = hover;
            Active = active;
            Disabled = disabled;
            Focus = focus;
            Declarations = declarations;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// The control type targeted. For a rule with no type -- <c>.primary { ... }</c>, <c>#save { ... }</c> -- this is
        /// a stand-in selector whose <see cref="ThemeCssSelector.IsAny"/> is true, never null: check <see cref="AppliesToAnyType"/> (or <see cref="IsScoped"/>) before
        /// looking the type up, since such a rule reaches every control type that has the class or id.
        /// </summary>
        public ThemeCssSelector Selector { get; }

        /// <summary>
        /// The <c>.class</c> names the control must have for the rule to apply (<c>.a.b</c> needs both), in source order and without
        /// duplicates; empty for a rule on every control of the type.
        /// </summary>
        public IReadOnlyList<string> Classes { get; internal set; } = Array.Empty<string> ();

        /// <summary>The <c>#id</c> the rule requires -- matched against <c>Control.Name</c> -- or null.</summary>
        public string? Id { get; internal set; }

        /// <summary>
        /// The selector's CSS specificity (see <see cref="ThemeCssSpecificity"/>). A host applier that cannot resolve the cascade
        /// itself orders the rules that reach one element by it, ties to the later rule.
        /// </summary>
        public ThemeCssSpecificity Specificity { get; internal set; }

        /// <summary>Whether the rule names a class or an id, so it reaches individual controls rather than every control of a type.</summary>
        public bool IsScoped => Classes.Count > 0 || Id is not null;

        /// <summary>Whether the selector names no control type (<c>.primary</c>, <c>#save</c>): <see cref="Selector"/> is then a stand-in selector whose <see cref="ThemeCssSelector.IsAny"/> is true.</summary>
        public bool AppliesToAnyType => Selector.IsAny;

        /// <summary>The part inside the control targeted (<c>Type::part</c>), or null for the control itself.</summary>
        public ThemeCssPart? Part { get; }

        /// <summary>Whether this is the <c>:hover</c> variant (of the control, or of the part when <see cref="Part"/> is set).</summary>
        public bool Hover { get; }

        /// <summary>Whether this is the <c>:active</c> variant -- the mouse held down on the control (#285). Never set on a part.</summary>
        public bool Active { get; }

        /// <summary>Whether this is the <c>:disabled</c> variant -- <c>Control.Enabled</c> false (#285). Never set on a part.</summary>
        public bool Disabled { get; }

        /// <summary>Whether this is the <c>:focus</c> variant -- <c>Control.Focused</c> true (#285). Never set on a part.</summary>
        public bool Focus { get; }

        /// <summary>Whether this is the <c>:focus-visible</c> variant -- focused with the window's focus cues showing. Never set on a part.</summary>
        public bool FocusVisible { get; internal set; }

        /// <summary>
        /// The state the rule styles, as one value: <see cref="ThemeCssState.Normal"/> when no pseudo-class
        /// is set. For a rule on a part only <see cref="ThemeCssState.Normal"/> and <see cref="ThemeCssState.Hover"/> occur.
        /// </summary>
        public ThemeCssState State
            => Hover ? ThemeCssState.Hover
                : Active ? ThemeCssState.Active
                : Disabled ? ThemeCssState.Disabled
                : Focus ? ThemeCssState.Focus
                : FocusVisible ? ThemeCssState.FocusVisible
                : ThemeCssState.Normal;

        /// <summary>The expanded declarations in source order.</summary>
        public IReadOnlyList<ThemeCssDeclaration> Declarations { get; }

        /// <summary>
        /// The <c>@media</c> query the rule sits inside, e.g. <c>(prefers-color-scheme: dark)</c>, or null. A sheet's
        /// <see cref="ThemeStyleSheet.Rules"/> only lists the rules whose query matches the current settings.
        /// </summary>
        public string? Media => media_text;

        internal string? media_text;

        /// <summary>The 1-based line of the selector in the source.</summary>
        public int Line { get; }

        /// <summary>The 1-based column of the selector in the source.</summary>
        public int Column { get; }

        /// <inheritdoc/>
        public override string ToString ()
        {
            var sb = new StringBuilder ();
            if (!Selector.IsAny)
                sb.Append (Selector.Name);
            foreach (var name in Classes)
                sb.Append ('.').Append (name);
            if (Id is not null)
                sb.Append ('#').Append (Id);
            if (Part is not null)
                sb.Append ("::").Append (Part.Name);
            if (Hover)
                sb.Append (":hover");
            else if (Active)
                sb.Append (":active");
            else if (Disabled)
                sb.Append (":disabled");
            else if (Focus)
                sb.Append (":focus");
            else if (FocusVisible)
                sb.Append (":focus-visible");
            sb.Append (" { ");
            foreach (var declaration in Declarations)
                sb.Append (declaration).Append (' ');
            sb.Append ('}');
            return sb.ToString ();
        }
    }

    /// <summary>
    /// One author variable of a parsed stylesheet: a <c>:root</c> custom property that is not a theme
    /// token (<c>--brand-blue: #00578e;</c>). Majorsilence.Forms controls only see a variable through
    /// the <c>var()</c> references that use it, but a host applier can publish variables as named
    /// resources (the Avalonia applier does), so the design system's names reach the host's own views.
    /// </summary>
    public sealed class ThemeCssVariable
    {
        private readonly Func<ThemeCssValue>? _resolve;

        internal ThemeCssVariable (string name, Func<ThemeCssValue>? resolve, ThemeCssToken? tokenReference, int line, int column)
        {
            Name = name;
            _resolve = resolve;
            TokenReference = tokenReference;
            Line = line;
            Column = column;
        }

        /// <summary>The custom-property name, e.g. <c>--brand-blue</c>.</summary>
        public string Name { get; }

        /// <summary>
        /// The resolved value -- a colour, a length or a font-family list, tried in that order -- or null
        /// when the value is none of those (such a variable can still be used where it fits, e.g. a
        /// border shorthand, but has no single typed value).
        /// </summary>
        public ThemeCssValue? Value => _resolve?.Invoke ();

        /// <summary>The theme token the value refers to through <c>var()</c>, when it is exactly one; its value then tracks the token live.</summary>
        public ThemeCssToken? TokenReference { get; }

        /// <summary>The 1-based line of the declaration.</summary>
        public int Line { get; }

        /// <summary>The 1-based column of the declaration.</summary>
        public int Column { get; }

        /// <inheritdoc/>
        public override string ToString () => $"{Name}: {(TokenReference is null ? Value?.ToString () ?? "?" : $"var({TokenReference.Name})")};";
    }

    /// <summary>
    /// One <c>:root</c> token declaration of a parsed stylesheet, with its resolved value.
    /// </summary>
    public sealed class ThemeCssTokenValue
    {
        private readonly Func<ThemeCssValue> _resolve;

        internal ThemeCssTokenValue (ThemeCssToken token, Func<ThemeCssValue> resolve, ThemeCssToken? tokenReference, int line, int column)
        {
            Token = token;
            _resolve = resolve;
            TokenReference = tokenReference;
            Line = line;
            Column = column;
        }

        /// <summary>The token being set.</summary>
        public ThemeCssToken Token { get; }

        /// <summary>The value the token is set to. Live when <see cref="TokenReference"/> is set.</summary>
        public ThemeCssValue Value => _resolve ();

        /// <summary>The other token this one was written as <c>var(--token)</c> of, or null for a literal.</summary>
        public ThemeCssToken? TokenReference { get; }

        /// <summary>The <c>@media</c> query the declaration sits inside, or null (see <see cref="ThemeCssRule.Media"/>).</summary>
        public string? Media => media_text;

        internal string? media_text;

        /// <summary>The 1-based line of the declaration in the source.</summary>
        public int Line { get; }

        /// <summary>The 1-based column of the declaration in the source.</summary>
        public int Column { get; }

        /// <inheritdoc/>
        public override string ToString () => $"{Token.Name}: {(TokenReference is null ? Value.ToString () : $"var({TokenReference.Name})")};";
    }

    /// <summary>
    /// Arguments of <see cref="Theme.StyleSheetApplied"/>: the stylesheet that was applied and the full
    /// chain it resolved to (its <c>extends</c> bases first), which is what a host bridge needs to
    /// mirror the theme onto its own controls.
    /// </summary>
    public sealed class ThemeStyleSheetEventArgs : EventArgs
    {
        internal ThemeStyleSheetEventArgs (IReadOnlyList<ThemeStyleSheet> chain)
        {
            Chain = chain;
        }

        /// <summary>Every CSS stylesheet in the applied chain, base first; the last entry is the one that was applied.</summary>
        public IReadOnlyList<ThemeStyleSheet> Chain { get; }

        /// <summary>The stylesheet that was applied (the last of <see cref="Chain"/>).</summary>
        public ThemeStyleSheet StyleSheet => Chain[Chain.Count - 1];
    }
}
