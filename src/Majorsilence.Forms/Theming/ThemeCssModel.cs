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
    public sealed class ThemeCssValue
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

        /// <summary>For <see cref="ThemeCssValueKind.Length"/>: whole logical pixels.</summary>
        public int Pixels { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.FontFamily"/>: the family list in preference order, as written.</summary>
        public IReadOnlyList<string> FontFamilies { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.FontWeight"/>: the CSS weight, a multiple of 100 from 100 to 900 (400 normal, 700 bold).</summary>
        public int FontWeight { get; private set; }

        /// <summary>For <see cref="ThemeCssValueKind.FontStyle"/>: <c>normal</c>, <c>italic</c> or <c>oblique</c>.</summary>
        public string FontStyle { get; private set; }

        internal static ThemeCssValue Color (uint argb) => new (ThemeCssValueKind.Color) { Argb = argb };

        internal static ThemeCssValue Length (int pixels) => new (ThemeCssValueKind.Length) { Pixels = pixels };

        internal static ThemeCssValue Families (IReadOnlyList<string> families) => new (ThemeCssValueKind.FontFamily) { FontFamilies = families };

        internal static ThemeCssValue Weight (int weight) => new (ThemeCssValueKind.FontWeight) { FontWeight = weight };

        internal static ThemeCssValue Style (string style) => new (ThemeCssValueKind.FontStyle) { FontStyle = style };

        /// <summary>The value spelled the way the stylesheet would write it.</summary>
        public override string ToString ()
        {
            switch (Kind) {
                case ThemeCssValueKind.Color:
                    return Alpha == 255
                        ? $"#{Red:x2}{Green:x2}{Blue:x2}"
                        : $"#{Red:x2}{Green:x2}{Blue:x2}{Alpha:x2}";
                case ThemeCssValueKind.Length:
                    return Pixels.ToString (CultureInfo.InvariantCulture) + "px";
                case ThemeCssValueKind.FontFamily:
                    return string.Join (", ", FontFamilies.Select (f => f.Contains (' ') || f.Contains (',') ? "\"" + f + "\"" : f));
                case ThemeCssValueKind.FontWeight:
                    return FontWeight == 400 ? "normal" : FontWeight == 700 ? "bold" : FontWeight.ToString (CultureInfo.InvariantCulture);
                default:
                    return FontStyle;
            }
        }
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
    /// One control rule of a parsed stylesheet: a selector, whether it is the <c>:hover</c> variant, and
    /// its declarations in source order (later declarations win). A comma list of selectors produces one
    /// rule per selector, sharing the declarations.
    /// </summary>
    public sealed class ThemeCssRule
    {
        internal ThemeCssRule (ThemeCssSelector selector, bool hover, IReadOnlyList<ThemeCssDeclaration> declarations, int line, int column)
        {
            Selector = selector;
            Hover = hover;
            Declarations = declarations;
            Line = line;
            Column = column;
        }

        /// <summary>The control type targeted.</summary>
        public ThemeCssSelector Selector { get; }

        /// <summary>Whether this is the <c>Type:hover</c> rule.</summary>
        public bool Hover { get; }

        /// <summary>The expanded declarations in source order.</summary>
        public IReadOnlyList<ThemeCssDeclaration> Declarations { get; }

        /// <summary>The 1-based line of the selector in the source.</summary>
        public int Line { get; }

        /// <summary>The 1-based column of the selector in the source.</summary>
        public int Column { get; }

        /// <inheritdoc/>
        public override string ToString ()
        {
            var sb = new StringBuilder ();
            sb.Append (Selector.Name);
            if (Hover)
                sb.Append (":hover");
            sb.Append (" { ");
            foreach (var declaration in Declarations)
                sb.Append (declaration).Append (' ');
            sb.Append ('}');
            return sb.ToString ();
        }
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
