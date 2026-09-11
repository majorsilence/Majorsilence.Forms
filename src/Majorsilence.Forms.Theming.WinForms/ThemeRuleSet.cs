using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms.Theming.WinForms
{
    /// <summary>
    /// The theme tokens read once per apply, converted to System.Drawing colours. Reading
    /// <see cref="Theme"/> after <see cref="Theme.ApplyStyleSheet"/> ran means the whole
    /// <c>extends</c> chain and every <c>:root</c> declaration is already resolved.
    /// </summary>
    internal sealed class TokenSnapshot
    {
        public Color Accent { get; private set; }
        public Color Accent2 { get; private set; }
        public Color Background { get; private set; }
        public Color BorderLow { get; private set; }
        public Color ControlLow { get; private set; }
        public Color ControlMid { get; private set; }
        public Color ControlMidHigh { get; private set; }
        public Color HighlightLow { get; private set; }
        public Color HighlightMid { get; private set; }
        public Color Foreground { get; private set; }
        public Color ForegroundOnAccent { get; private set; }
        public Color ForegroundDisabled { get; private set; }
        public string UIFontFamily { get; private set; } = "";
        public int FontSize { get; private set; }

        /// <summary>Whether the background is dark, decided by luminance — drives the dark-mode switches.</summary>
        public bool IsDark => Luminance (Background) < 0.5;

        public static TokenSnapshot Read () => new () {
            Accent = ToColor (Theme.AccentColor),
            Accent2 = ToColor (Theme.AccentColor2),
            Background = ToColor (Theme.BackgroundColor),
            BorderLow = ToColor (Theme.BorderLowColor),
            ControlLow = ToColor (Theme.ControlLowColor),
            ControlMid = ToColor (Theme.ControlMidColor),
            ControlMidHigh = ToColor (Theme.ControlMidHighColor),
            HighlightLow = ToColor (Theme.ControlHighlightLowColor),
            HighlightMid = ToColor (Theme.ControlHighlightMidColor),
            Foreground = ToColor (Theme.ForegroundColor),
            ForegroundOnAccent = ToColor (Theme.ForegroundColorOnAccent),
            ForegroundDisabled = ToColor (Theme.ForegroundDisabledColor),
            UIFontFamily = Theme.UIFont.FamilyName,
            FontSize = Theme.FontSize,
        };

        public static Color ToColor (SKColor c) => Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue);

        public static double Luminance (Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

        /// <summary>
        /// Composites a possibly translucent colour over an opaque background. Most WinForms
        /// properties reject or mis-render alpha, so every colour is flattened before it is set.
        /// </summary>
        public static Color Flatten (Color over, Color under)
        {
            if (over.A == 255)
                return over;

            var a = over.A / 255.0;
            return Color.FromArgb (255,
                (int) (over.R * a + under.R * (1 - a) + 0.5),
                (int) (over.G * a + under.G * (1 - a) + 0.5),
                (int) (over.B * a + under.B * (1 - a) + 0.5));
        }

        /// <summary>Darkens a colour by a factor in 0..1 — used to derive a pressed shade from a hover one.</summary>
        public static Color Darken (Color c, double factor)
            => Color.FromArgb (c.A, (int) (c.R * (1 - factor)), (int) (c.G * (1 - factor)), (int) (c.B * (1 - factor)));
    }

    /// <summary>The font pieces one rule declares; members are null when the rule does not mention them.</summary>
    internal sealed class FontSpec
    {
        public IReadOnlyList<string>? Families;
        public int? SizePixels;
        public int? Weight;
        public string? Style;

        public bool IsEmpty => Families is null && SizePixels is null && Weight is null && Style is null;
    }

    /// <summary>
    /// The control rules of an applied stylesheet chain, merged the way the core applies them: sheets
    /// base-first, declarations in source order, later wins. Keyed by (selector, part, hover); values
    /// resolve live through <see cref="ThemeCssDeclaration.Value"/>.
    /// </summary>
    internal sealed class ThemeRuleSet
    {
        private readonly Dictionary<(string Selector, string? Part, bool Hover), Dictionary<string, ThemeCssDeclaration>> rules = new ();

        public static ThemeRuleSet Build (IReadOnlyList<ThemeStyleSheet> chain)
        {
            var set = new ThemeRuleSet ();

            foreach (var sheet in chain)
                foreach (var rule in sheet.Rules) {
                    var key = (rule.Selector.Name, rule.Part?.Name, rule.Hover);

                    if (!set.rules.TryGetValue (key, out var declarations)) {
                        declarations = new Dictionary<string, ThemeCssDeclaration> (StringComparer.OrdinalIgnoreCase);
                        set.rules[key] = declarations;
                    }

                    foreach (var declaration in rule.Declarations)
                        declarations[declaration.Property] = declaration;
                }

            return set;
        }

        public ThemeCssValue? Value (string selector, string? part, bool hover, string property)
            => rules.TryGetValue ((selector, part, hover), out var declarations) && declarations.TryGetValue (property, out var declaration)
                ? declaration.Value
                : null;

        public Color? Color (string selector, string? part, bool hover, string property)
        {
            var value = Value (selector, part, hover, property);
            return value is { Kind: ThemeCssValueKind.Color }
                ? System.Drawing.Color.FromArgb (value.Alpha, value.Red, value.Green, value.Blue)
                : null;
        }

        public int? Length (string selector, string? part, bool hover, string property)
        {
            var value = Value (selector, part, hover, property);
            return value is { Kind: ThemeCssValueKind.Length } ? value.Pixels : null;
        }

        public FontSpec Font (string selector, string? part = null, bool hover = false)
        {
            var spec = new FontSpec ();

            if (Value (selector, part, hover, "font-family") is { Kind: ThemeCssValueKind.FontFamily } family)
                spec.Families = family.FontFamilies;
            if (Value (selector, part, hover, "font-size") is { Kind: ThemeCssValueKind.Length } size)
                spec.SizePixels = size.Pixels;
            if (Value (selector, part, hover, "font-weight") is { Kind: ThemeCssValueKind.FontWeight } weight)
                spec.Weight = weight.FontWeight;
            if (Value (selector, part, hover, "font-style") is { Kind: ThemeCssValueKind.FontStyle } style)
                spec.Style = style.FontStyle;

            return spec;
        }

        public bool HasAny (string selector, string? part = null)
            => rules.Keys.Any (k => k.Selector == selector && (part is null || k.Part == part));
    }
}
