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
        private readonly Dictionary<(string Selector, string? Part, ThemeCssState State), Dictionary<string, ThemeCssDeclaration>> rules = new ();

        // Every declaration of a key in source order across the chain: the property dictionary above keeps the last of each
        // property, which loses what a corner longhand written before or after border-radius means.
        private readonly Dictionary<(string Selector, string? Part, ThemeCssState State), List<ThemeCssDeclaration>> ordered = new ();
        private readonly Dictionary<string, ControlKeyframes> keyframes = new (StringComparer.Ordinal);

        // The rules that name only an id (#saveButton, Button#saveButton), by the id: they style the control whose Name it is, so they
        // are not part of the merged type rules above but laid over them for that control (OverlayFor). Rules that name a class are
        // not here: a real WinForms control has no style classes, so they reach none of its controls (WinFormsCssTheme reports them).
        private readonly Dictionary<string, List<(ThemeCssRule Rule, int Order)>> id_rules = new (StringComparer.Ordinal);

        /// <summary>Whether any rule in the chain is an id rule.</summary>
        public bool HasIdRules => id_rules.Count > 0;

        /// <summary>Whether a rule in the chain names exactly this id.</summary>
        public bool HasIdRule (string name) => id_rules.ContainsKey (name);

        public static ThemeRuleSet Build (IReadOnlyList<ThemeStyleSheet> chain)
        {
            var set = new ThemeRuleSet ();
            var order = 0;

            foreach (var sheet in chain) {
                foreach (var keyframe in sheet.Keyframes)
                    set.keyframes[keyframe.Name] = keyframe;

                foreach (var rule in sheet.Rules) {
                    order++;

                    // A rule naming a class or an id is not a rule for the type: it reaches individual controls.
                    if (rule.IsScoped) {
                        if (rule.Classes.Count == 0 && rule.Id is { } id) {
                            if (!set.id_rules.TryGetValue (id, out var list))
                                set.id_rules[id] = list = new List<(ThemeCssRule, int)> ();
                            list.Add ((rule, order));
                        }

                        continue;
                    }

                    // Each state keeps its own declarations, so an :active, :disabled, :focus or :focus-visible rule never
                    // merges into the at-rest key and overrides the base rule.
                    var key = (rule.Selector.Name, rule.Part?.Name, rule.State);

                    if (!set.rules.TryGetValue (key, out var declarations)) {
                        declarations = new Dictionary<string, ThemeCssDeclaration> (StringComparer.OrdinalIgnoreCase);
                        set.rules[key] = declarations;
                    }

                    if (!set.ordered.TryGetValue (key, out var sequence))
                        set.ordered[key] = sequence = new List<ThemeCssDeclaration> ();

                    foreach (var declaration in rule.Declarations) {
                        declarations[declaration.Property] = declaration;
                        sequence.Add (declaration);
                    }
                }
            }

            return set;
        }

        private ThemeRuleSet Copy ()
        {
            var copy = new ThemeRuleSet ();

            foreach (var pair in rules)
                copy.rules[pair.Key] = new Dictionary<string, ThemeCssDeclaration> (pair.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in ordered)
                copy.ordered[pair.Key] = new List<ThemeCssDeclaration> (pair.Value);
            foreach (var pair in keyframes)
                copy.keyframes[pair.Key] = pair.Value;

            return copy;
        }

        /// <summary>The id rules that style a control named <paramref name="name"/> whose own selector is <paramref name="selector"/>, in cascade order.</summary>
        public IReadOnlyList<ThemeCssRule> IdRulesFor (string name, string selector)
            => id_rules.TryGetValue (name, out var list)
                ? list.Where (r => r.Rule.Selector.IsAny || string.Equals (r.Rule.Selector.Name, selector, StringComparison.OrdinalIgnoreCase))
                    .OrderBy (r => r.Rule.Specificity).ThenBy (r => r.Order).Select (r => r.Rule).ToList ()
                : Array.Empty<ThemeCssRule> ();

        /// <summary>
        /// These rules with the id rules for a control named <paramref name="name"/> laid over the rules of its own
        /// <paramref name="selector"/>, so the applier reads them exactly as it reads the type's -- under the control's own selector.
        /// An id rule outranks every type rule, so its declarations come after the type's, in each state it reaches: a resting
        /// <c>#save</c> rule also holds in the hovered, pressed, disabled and focused states the selector has (a rule for the
        /// state itself, written later or more specifically, is laid over it), as a class or id rule does in a browser.
        /// Returns this set when no id rule reaches the control.
        /// </summary>
        public ThemeRuleSet OverlayFor (string name, string selector)
        {
            var reaching = IdRulesFor (name, selector);

            if (reaching.Count == 0)
                return this;

            var copy = Copy ();
            var info = ThemeCssReference.FindSelector (selector);

            foreach (var rule in reaching) {
                var states = new List<ThemeCssState> { rule.State };

                // :focus-visible reads the :focus key as well, so it needs no copy of its own.
                if (rule.State == ThemeCssState.Normal && info is not null)
                    foreach (var state in new[] { ThemeCssState.Hover, ThemeCssState.Active, ThemeCssState.Disabled, ThemeCssState.Focus })
                        if (info.Supports (state))
                            states.Add (state);

                foreach (var state in states) {
                    var key = (selector, (string?) null, state);

                    if (!copy.rules.TryGetValue (key, out var declarations))
                        copy.rules[key] = declarations = new Dictionary<string, ThemeCssDeclaration> (StringComparer.OrdinalIgnoreCase);
                    if (!copy.ordered.TryGetValue (key, out var sequence))
                        copy.ordered[key] = sequence = new List<ThemeCssDeclaration> ();

                    foreach (var declaration in rule.Declarations) {
                        declarations[declaration.Property] = declaration;
                        sequence.Add (declaration);
                    }
                }
            }

            return copy;
        }

        public ThemeCssValue? Value (string selector, string? part, bool hover, string property)
            => Value (selector, part, hover ? ThemeCssState.Hover : ThemeCssState.Normal, property);

        public ThemeCssValue? Value (string selector, string? part, ThemeCssState state, string property)
            => rules.TryGetValue ((selector, part, state), out var declarations) && declarations.TryGetValue (property, out var declaration)
                ? declaration.Value
                : null;

        public Color? Color (string selector, string? part, ThemeCssState state, string property)
        {
            var value = Value (selector, part, state, property);
            return value is { Kind: ThemeCssValueKind.Color }
                ? System.Drawing.Color.FromArgb (value.Alpha, value.Red, value.Green, value.Blue)
                : null;
        }

        public int? Length (string selector, string? part, ThemeCssState state, string property)
        {
            var value = Value (selector, part, state, property);
            return value is { Kind: ThemeCssValueKind.Length } ? value.Pixels : null;
        }

        /// <summary>Whether any rule exists for the selector in this state (any part when <paramref name="part"/> is null).</summary>
        public bool HasState (string selector, ThemeCssState state, string? part = null)
            => rules.Keys.Any (k => k.Selector == selector && k.State == state && (part is null || k.Part == part));

        /// <summary>The declarations of one (selector, part, state), or none.</summary>
        public IEnumerable<ThemeCssDeclaration> Declarations (string selector, string? part, ThemeCssState state)
            => rules.TryGetValue ((selector, part, state), out var declarations) ? declarations.Values : Enumerable.Empty<ThemeCssDeclaration> ();

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

        /// <summary>
        /// The four corner radii (top-left, top-right, bottom-right, bottom-left) the rules give a selector, or null when none does.
        /// <c>border-radius</c> sets every corner and resets the ones written before it; a corner longhand wins over an earlier
        /// <c>border-radius</c>, so the declarations are replayed in source order.
        /// </summary>
        public (int TopLeft, int TopRight, int BottomRight, int BottomLeft)? Corners (string selector, string? part, ThemeCssState state)
        {
            if (!ordered.TryGetValue ((selector, part, state), out var sequence))
                return null;

            int tl = 0, tr = 0, br = 0, bl = 0;
            var any = false;

            foreach (var declaration in sequence) {
                if (declaration.Value is not { Kind: ThemeCssValueKind.Length } length)
                    continue;

                switch (declaration.Property) {
                    case "border-radius": tl = tr = br = bl = length.Pixels; any = true; break;
                    case "border-top-left-radius": tl = length.Pixels; any = true; break;
                    case "border-top-right-radius": tr = length.Pixels; any = true; break;
                    case "border-bottom-right-radius": br = length.Pixels; any = true; break;
                    case "border-bottom-left-radius": bl = length.Pixels; any = true; break;
                }
            }

            return any ? (tl, tr, br, bl) : null;
        }

        /// <summary>
        /// Every declaration that applies to a selector in a state, base first: the resting rule's, then each of <paramref name="states"/>'
        /// in order. The input of <see cref="ThemeCssTransition.Resolve"/> and <see cref="ThemeCssAnimation.Resolve"/>, which let a later
        /// longhand replace an earlier one the way CSS lets a state inherit what it leaves unsaid.
        /// </summary>
        public IEnumerable<ThemeCssDeclaration> Cascade (string selector, IEnumerable<ThemeCssState> states)
        {
            var list = new List<ThemeCssDeclaration> ();

            if (ordered.TryGetValue ((selector, null, ThemeCssState.Normal), out var resting))
                list.AddRange (resting);

            foreach (var state in states)
                if (ordered.TryGetValue ((selector, null, state), out var more))
                    list.AddRange (more);

            return list;
        }

        /// <summary>The <c>@keyframes</c> block of that name, or null.</summary>
        public ControlKeyframes? Keyframes (string name) => keyframes.TryGetValue (name, out var frames) ? frames : null;

        /// <summary>Whether any rule for the selector, in any state, declares a transition or an animation.</summary>
        public bool HasMotion (string selector)
            => ordered.Any (p => p.Key.Selector == selector && p.Key.Part is null
                                 && p.Value.Any (d => d.Property.StartsWith ("transition-", StringComparison.Ordinal) || d.Property.StartsWith ("animation-", StringComparison.Ordinal)));

        public bool HasAny (string selector, string? part = null)
            => rules.Keys.Any (k => k.Selector == selector && (part is null || k.Part == part));

        /// <summary>The rules whose selector is one of the control-level (no part) selectors, in any state.</summary>
        public IEnumerable<(string Selector, string? Part, ThemeCssState State)> Keys => rules.Keys;
    }
}
