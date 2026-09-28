using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Majorsilence.Forms.Theming.Avalonia
{
    using global::Avalonia;
    using global::Avalonia.Controls;
    using global::Avalonia.Controls.Primitives;
    using global::Avalonia.Media;
    using global::Avalonia.Styling;

    /// <summary>
    /// What one apply produces: the resources to install, the styles to install, and the diagnostics.
    /// Built without touching the application, so it can be built on any thread and tested headless.
    /// </summary>
    internal sealed class AvaloniaThemeBuild
    {
        public ResourceDictionary Resources { get; } = new ();

        public List<Style> Styles { get; } = new ();

        public List<ThemeCssDiagnostic> Diagnostics { get; } = new ();

        /// <summary>Whether the theme background is dark (by luminance) -- drives the theme variant.</summary>
        public bool IsDark { get; set; }
    }

    /// <summary>
    /// Turns an applied stylesheet chain into Avalonia resources and styles, driven by
    /// <see cref="AvaloniaThemeSupport"/>. Reads the tokens from <see cref="Theme"/> (the chain has
    /// already been installed there, so <c>extends</c> and every <c>var()</c> are resolved) and the rules
    /// from the chain, base sheet first, later declarations winning.
    /// </summary>
    internal static class AvaloniaThemeApplier
    {
        // One style per selector, with its setters by property; BorderThickness is assembled from the
        // uniform width and the per-side widths at the end.
        private sealed class StyleBuilder
        {
            public StyleBuilder (StyleSelector selector) => Selector = selector;

            public StyleSelector Selector { get; }
            public Dictionary<AvaloniaProperty, object> Setters { get; } = new ();
            public double? Uniform;
            public double? Top, Right, Bottom, Left;

            public Style Build ()
            {
                var style = new Style (Selector.Build);

                foreach (var (property, value) in Setters)
                    style.Setters.Add (new Setter (property, value));

                if (Uniform is not null || Top is not null || Right is not null || Bottom is not null || Left is not null) {
                    var all = Uniform ?? 0;
                    style.Setters.Add (new Setter (TemplatedControl.BorderThicknessProperty,
                        new Thickness (Left ?? all, Top ?? all, Right ?? all, Bottom ?? all)));
                }

                return style;
            }
        }

        public static AvaloniaThemeBuild Build (IReadOnlyList<ThemeStyleSheet> chain)
        {
            var build = new AvaloniaThemeBuild ();
            var styles = new List<StyleBuilder> ();

            StyleBuilder StyleFor (StyleSelector selector)
            {
                var existing = styles.FirstOrDefault (s => ReferenceEquals (s.Selector, selector));
                if (existing is not null)
                    return existing;

                var created = new StyleBuilder (selector);
                styles.Add (created);
                return created;
            }

            var background = ToColor (Theme.BackgroundColor);
            build.IsDark = Luminance (background) < 0.5;

            PublishTokens (build.Resources, chain);
            PublishFluentTokens (build.Resources, chain);

            // The window is the ambient surface: the background and text tokens theme it even when the
            // sheet has no Form rule, the way they theme a Majorsilence.Forms form.
            var window = StyleFor (S.Window);
            window.Setters[TemplatedControl.BackgroundProperty] = new SolidColorBrush (background);
            window.Setters[TemplatedControl.ForegroundProperty] = new SolidColorBrush (ToColor (Theme.ForegroundColor));

            var seen = new HashSet<string> (StringComparer.Ordinal);

            foreach (var sheet in chain)
                foreach (var rule in sheet.Rules) {
                    var mapping = AvaloniaThemeSupport.FindMapping (rule.Selector.Name);
                    if (mapping is null)
                        continue;

                    var ruleText = rule.Selector.Name + (rule.Part is null ? "" : "::" + rule.Part.Name) + (rule.Hover ? ":hover" : "");

                    if (mapping.AvaloniaTypes is null) {
                        if (seen.Add ("selector:" + rule.Selector.Name))
                            build.Diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Info, rule.Line, rule.Column,
                                $"'{rule.Selector.Name}' has no Avalonia counterpart; the rule styles Majorsilence.Forms controls only. {mapping.Notes}"));
                        continue;
                    }

                    foreach (var declaration in rule.Declarations) {
                        var entry = AvaloniaThemeSupport.Find (rule.Selector.Name, rule.Part?.Name, rule.Hover, declaration.Property);
                        var level = entry?.Level ?? AvaloniaThemeSupportLevel.Unsupported;

                        if (level != AvaloniaThemeSupportLevel.Native && seen.Add (ruleText + "|" + declaration.Property)) {
                            var notes = entry?.Notes ?? "Avalonia has no seam for it.";
                            build.Diagnostics.Add (level == AvaloniaThemeSupportLevel.Approximate
                                ? new ThemeCssDiagnostic (ThemeCssSeverity.Info, declaration.Line, declaration.Column,
                                    $"'{ruleText} {{ {declaration.Property} }}' applies approximately on Avalonia ({mapping.AvaloniaTypes}): {notes}")
                                : new ThemeCssDiagnostic (ThemeCssSeverity.Warning, declaration.Line, declaration.Column,
                                    $"'{ruleText} {{ {declaration.Property} }}' is not supported on Avalonia ({mapping.AvaloniaTypes}) and was skipped there: {notes} It still applies to Majorsilence.Forms controls."));
                        }

                        if (entry is null || level == AvaloniaThemeSupportLevel.Unsupported)
                            continue;

                        var value = declaration.Value;

                        foreach (var target in entry.Targets) {
                            switch (target) {
                                case ResourceTarget resource:
                                    if (ResourceValue (resource, declaration.Property, value) is { } resourceValue)
                                        build.Resources[resource.Key] = resourceValue;
                                    break;

                                case StyleTarget style:
                                    var builder = StyleFor (style.Selector);
                                    if (style.Property == TemplatedControl.BorderThicknessProperty && value.Kind == ThemeCssValueKind.Length) {
                                        double px = value.Pixels;
                                        switch (style.Side) {
                                            case BorderSide.Top: builder.Top = px; break;
                                            case BorderSide.Right: builder.Right = px; break;
                                            case BorderSide.Bottom: builder.Bottom = px; break;
                                            case BorderSide.Left: builder.Left = px; break;
                                            default: builder.Uniform = px; break;
                                        }
                                    } else if (StyleValue (style.Property, value) is { } styleValue) {
                                        builder.Setters[style.Property] = styleValue;
                                    }
                                    break;
                            }
                        }
                    }
                }

            foreach (var style in styles)
                build.Styles.Add (style.Build ());

            return build;
        }

        // ---- tokens --------------------------------------------------------------------------------

        // Every theme token (read from Theme, so a sheet that sets three tokens still publishes all of
        // them) and every author variable, under PascalCase resource keys: see ResourceKeyFor.
        private static void PublishTokens (ResourceDictionary resources, IReadOnlyList<ThemeStyleSheet> chain)
        {
            foreach (var token in ThemeCssReference.Tokens) {
                var property = typeof (Theme).GetProperty (token.PropertyName);
                switch (property?.GetValue (null)) {
                    case SKColor color:
                        PublishColor (resources, token.Name, ToColor (color));
                        break;
                    case int length:
                        resources[ResourceKeyFor (token.Name)] = (double) length;
                        break;
                    case SKTypeface typeface:
                        resources[ResourceKeyFor (token.Name)] = new FontFamily (typeface.FamilyName);
                        break;
                }
            }

            foreach (var sheet in chain)
                foreach (var variable in sheet.Variables) {
                    switch (variable.Value) {
                        case { Kind: ThemeCssValueKind.Color } color:
                            PublishColor (resources, variable.Name, ToColor (color));
                            break;
                        case { Kind: ThemeCssValueKind.Length } length:
                            resources[ResourceKeyFor (variable.Name)] = (double) length.Pixels;
                            break;
                        case { Kind: ThemeCssValueKind.FontFamily } families:
                            resources[ResourceKeyFor (variable.Name)] = ToFontFamily (families.FontFamilies);
                            break;
                    }
                }
        }

        private static void PublishColor (ResourceDictionary resources, string cssName, Color color)
        {
            var key = ResourceKeyFor (cssName);

            // "--accent-color" -> AccentColor (Color) + AccentBrush; "--line2" -> Line2 (brush) + Line2Color.
            if (key.EndsWith ("Color", StringComparison.Ordinal) && key.Length > "Color".Length) {
                resources[key] = color;
                resources[string.Concat (key.AsSpan (0, key.Length - "Color".Length), "Brush")] = new SolidColorBrush (color);
            } else {
                resources[key] = new SolidColorBrush (color);
                resources[key + "Color"] = color;
            }
        }

        /// <summary>The resource key a CSS custom property is published under: <c>--line2</c> → <c>Line2</c>, <c>--ts-blue</c> → <c>TsBlue</c>.</summary>
        internal static string ResourceKeyFor (string cssName)
        {
            var sb = new StringBuilder ();
            var upper = true;

            foreach (var c in cssName.TrimStart ('-')) {
                if (c == '-' || c == '_') {
                    upper = true;
                    continue;
                }

                sb.Append (upper ? char.ToUpperInvariant (c) : c);
                upper = false;
            }

            return sb.ToString ();
        }

        // The Fluent system resources the tokens drive: the accent ramp, the text selection and, when
        // the sheet declares them, the default font family and size.
        private static void PublishFluentTokens (ResourceDictionary resources, IReadOnlyList<ThemeStyleSheet> chain)
        {
            var accent = ToColor (Theme.AccentColor);
            resources["SystemAccentColor"] = accent;
            resources["SystemAccentColorLight1"] = Mix (accent, Colors.White, 0.12);
            resources["SystemAccentColorLight2"] = Mix (accent, Colors.White, 0.26);
            resources["SystemAccentColorLight3"] = Mix (accent, Colors.White, 0.42);
            resources["SystemAccentColorDark1"] = Mix (accent, Colors.Black, 0.15);
            resources["SystemAccentColorDark2"] = Mix (accent, Colors.Black, 0.30);
            resources["SystemAccentColorDark3"] = Mix (accent, Colors.Black, 0.45);

            resources["TextControlSelectionHighlightColor"] = new SolidColorBrush (ToColor (Theme.TextSelectionBackgroundColor));

            var declared = chain.SelectMany (s => s.Tokens).ToList ();

            if (declared.LastOrDefault (t => t.Token.PropertyName == nameof (Theme.UIFont)) is { } font)
                resources["ContentControlThemeFontFamily"] = ToFontFamily (font.Value.FontFamilies);

            if (declared.Any (t => t.Token.PropertyName == nameof (Theme.FontSize)))
                resources["ControlContentThemeFontSize"] = (double) Theme.FontSize;
        }

        // ---- values --------------------------------------------------------------------------------

        private static object? ResourceValue (ResourceTarget target, string property, ThemeCssValue value)
        {
            if (target.FixedValue is not null)
                return target.FixedValue;

            switch (target.Kind) {
                case ResourceKind.Brush when value.Kind == ThemeCssValueKind.Color:
                    var color = ToColor (value);
                    return new SolidColorBrush (target.Pressed && property == "background-color" ? Darken (color, 0.08) : color);
                case ResourceKind.Color when value.Kind == ThemeCssValueKind.Color:
                    return ToColor (value);
                case ResourceKind.Double when value.Kind == ThemeCssValueKind.Length:
                    return (double) value.Pixels;
                default:
                    return null;
            }
        }

        private static object? StyleValue (AvaloniaProperty property, ThemeCssValue value)
        {
            if (property.PropertyType == typeof (IBrush))
                return value.Kind == ThemeCssValueKind.Color ? new SolidColorBrush (ToColor (value)) : null;
            if (property.PropertyType == typeof (CornerRadius))
                return value.Kind == ThemeCssValueKind.Length ? new CornerRadius (value.Pixels) : null;
            if (property.PropertyType == typeof (double))
                return value.Kind == ThemeCssValueKind.Length ? (double) value.Pixels : null;
            if (property.PropertyType == typeof (FontFamily))
                return value.Kind == ThemeCssValueKind.FontFamily ? ToFontFamily (value.FontFamilies) : null;
            if (property.PropertyType == typeof (FontWeight))
                return value.Kind == ThemeCssValueKind.FontWeight ? (FontWeight) value.FontWeight : null;
            if (property.PropertyType == typeof (FontStyle))
                return value.Kind == ThemeCssValueKind.FontStyle ? ToFontStyle (value.FontStyle) : null;
            return null;
        }

        /// <summary>
        /// An Avalonia fallback family list. CSS generic names become Avalonia's default family
        /// (<c>$Default</c>), which is what they mean: "whatever the platform's UI font is".
        /// </summary>
        internal static FontFamily ToFontFamily (IReadOnlyList<string> families)
        {
            var names = families
                .Select (f => f.ToLowerInvariant () is "sans-serif" or "serif" or "system-ui" or "ui-sans-serif" or "-apple-system" ? FontFamily.DefaultFontFamilyName : f)
                .Distinct (StringComparer.OrdinalIgnoreCase);

            return new FontFamily (string.Join (", ", names));
        }

        private static FontStyle ToFontStyle (string style) => style switch {
            "italic" => FontStyle.Italic,
            "oblique" => FontStyle.Oblique,
            _ => FontStyle.Normal
        };

        internal static Color ToColor (SKColor c) => Color.FromArgb (c.Alpha, c.Red, c.Green, c.Blue);

        internal static Color ToColor (ThemeCssValue v) => Color.FromArgb (v.Alpha, v.Red, v.Green, v.Blue);

        internal static double Luminance (Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

        internal static Color Mix (Color c, Color with, double amount)
            => Color.FromArgb (c.A,
                (byte) Math.Round (c.R + (with.R - c.R) * amount),
                (byte) Math.Round (c.G + (with.G - c.G) * amount),
                (byte) Math.Round (c.B + (with.B - c.B) * amount));

        internal static Color Darken (Color c, double amount) => Mix (c, Colors.Black, amount);
    }
}
