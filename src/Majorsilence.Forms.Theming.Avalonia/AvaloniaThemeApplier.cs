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
    internal static partial class AvaloniaThemeApplier
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
            public AvaloniaProperty? ThicknessProperty, RadiusProperty;
            public double? RadiusAll;
            public double? TopLeft, TopRight, BottomRight, BottomLeft;
            public global::Avalonia.Animation.Transitions? Transitions;
            public List<global::Avalonia.Animation.Animation> Animations { get; } = new ();
            public double? OutlineWidth, OutlineOffset;
            public string? OutlineStyle;
            public Color? OutlineColor;

            public Style Build ()
            {
                var style = new Style (Selector.Build);

                foreach (var (property, value) in Setters)
                    style.Setters.Add (new Setter (property, value));

                if (Uniform is not null || Top is not null || Right is not null || Bottom is not null || Left is not null) {
                    var all = Uniform ?? 0;
                    style.Setters.Add (new Setter (ThicknessProperty ?? TemplatedControl.BorderThicknessProperty,
                        new Thickness (Left ?? all, Top ?? all, Right ?? all, Bottom ?? all)));
                }

                if (RadiusAll is not null || TopLeft is not null || TopRight is not null || BottomRight is not null || BottomLeft is not null) {
                    var all = RadiusAll ?? 0;
                    style.Setters.Add (new Setter (RadiusProperty ?? TemplatedControl.CornerRadiusProperty,
                        new CornerRadius (TopLeft ?? all, TopRight ?? all, BottomRight ?? all, BottomLeft ?? all)));
                }

                if (Transitions is not null)
                    style.Setters.Add (new Setter (global::Avalonia.Animation.Animatable.TransitionsProperty, Transitions));

                foreach (var animation in Animations)
                    style.Animations.Add (animation);

                if (OutlineStyle is "solid" or "dashed" or "auto")
                    style.Setters.Add (new Setter (Control.FocusAdornerProperty, OutlineAdorner (OutlineWidth ?? 3, OutlineOffset ?? 0, OutlineColor ?? ToColor (Theme.ForegroundColor))));

                return style;
            }
        }

        // Avalonia draws a control's focus visual -- the keyboard-focus ring, which is what CSS's :focus-visible outline is -- from
        // its FocusAdorner template. A border laid over the control with a negative margin is a ring outside it: the margin is the
        // stroke plus the gap, so it takes no room and moves nothing. Avalonia borders are solid, so a dashed outline is drawn solid.
        private static global::Avalonia.Controls.Templates.FuncTemplate<Control> OutlineAdorner (double width, double offset, Color color)
        {
            var brush = new SolidColorBrush (color);
            return new global::Avalonia.Controls.Templates.FuncTemplate<Control> (() => new Border {
                BorderBrush = brush,
                BorderThickness = new Thickness (width),
                Margin = new Thickness (-(width + offset)),
                CornerRadius = new CornerRadius (Math.Max (0, 3 + offset)),
                IsHitTestVisible = false,
            });
        }

        // Within a sheet the rules are applied by state, the resting look first: :hover writes the pressed colours as a darker
        // hover, and an explicit :active rule must win over that whatever order the author wrote them in.
        private static IEnumerable<ThemeCssRule> InStateOrder (IReadOnlyList<ThemeCssRule> rules)
            => rules.Select ((rule, index) => (rule, index)).OrderBy (r => r.rule.State).ThenBy (r => r.index).Select (r => r.rule);

        public static AvaloniaThemeBuild Build (IReadOnlyList<ThemeStyleSheet> chain, bool deriveFromTokens = true)
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

            // Writes one declaration's value to everything its support row targets.
            void Write (AvaloniaThemeSupportEntry entry, ThemeCssValue value)
            {
                foreach (var target in entry.Targets) {
                    switch (target) {
                        case ResourceTarget resource:
                            if (ResourceValue (resource, entry.Property, value) is { } resourceValue)
                                build.Resources[resource.Key] = resourceValue;
                            break;

                        case StyleTarget style:
                            var builder = StyleFor (style.Selector);
                            if (style.Property.PropertyType == typeof (Thickness) && value.Kind == ThemeCssValueKind.Length) {
                                builder.ThicknessProperty = style.Property;
                                double px = value.Pixels;
                                switch (style.Side) {
                                    case BorderSide.Top: builder.Top = px; break;
                                    case BorderSide.Right: builder.Right = px; break;
                                    case BorderSide.Bottom: builder.Bottom = px; break;
                                    case BorderSide.Left: builder.Left = px; break;
                                    default: builder.Uniform = px; break;
                                }
                            } else if (style.Property.PropertyType == typeof (CornerRadius) && value.Kind == ThemeCssValueKind.Length) {
                                // border-radius sets every corner and resets the ones written before it; a corner longhand sets one.
                                builder.RadiusProperty = style.Property;
                                double px = value.Pixels;
                                switch (style.Corner) {
                                    case CornerSide.TopLeft: builder.TopLeft = px; break;
                                    case CornerSide.TopRight: builder.TopRight = px; break;
                                    case CornerSide.BottomRight: builder.BottomRight = px; break;
                                    case CornerSide.BottomLeft: builder.BottomLeft = px; break;
                                    default:
                                        builder.RadiusAll = px;
                                        builder.TopLeft = builder.TopRight = builder.BottomRight = builder.BottomLeft = null;
                                        break;
                                }
                            } else if (style.Property == Control.FocusAdornerProperty) {
                                // The four outline longhands make one adorner, assembled when the style is built.
                                switch (entry.Property) {
                                    case "outline-width" when value.Kind == ThemeCssValueKind.Length: builder.OutlineWidth = value.Pixels; break;
                                    case "outline-offset" when value.Kind == ThemeCssValueKind.Length: builder.OutlineOffset = value.Pixels; break;
                                    case "outline-style" when value.Kind == ThemeCssValueKind.Keyword: builder.OutlineStyle = value.Keyword; break;
                                    case "outline-color" when value.Kind == ThemeCssValueKind.Color: builder.OutlineColor = ToColor (value); break;
                                }
                            } else if (StyleValue (style.Property, value) is { } styleValue) {
                                builder.Setters[style.Property] = styleValue;
                            }
                            break;
                    }
                }
            }

            // What the tokens imply for each control (the button face is --control-mid-color, ...), written first so any rule
            // of the sheet's own wins. Not reported: nothing the author wrote is being dropped.
            if (deriveFromTokens) {
                var disabled = new SolidColorBrush (ToColor (Theme.ForegroundDisabledColor));
                foreach (var key in AvaloniaTokenDefaults.DisabledForegroundKeys)
                    build.Resources[key] = disabled;
            }

            if (deriveFromTokens)
                foreach (var tokenDefault in AvaloniaTokenDefaults.Read ())
                    if (AvaloniaThemeSupport.Find (tokenDefault.Selector, tokenDefault.Part, tokenDefault.State, tokenDefault.Property) is { Level: not AvaloniaThemeSupportLevel.Unsupported } entry)
                        Write (entry, ThemeCssValue.FromColor (tokenDefault.Argb));

            var seen = new HashSet<string> (StringComparer.Ordinal);

            foreach (var sheet in chain)
                foreach (var rule in InStateOrder (sheet.Rules)) {
                    var mapping = AvaloniaThemeSupport.FindMapping (rule.Selector.Name);
                    if (mapping is null)
                        continue;

                    var state = rule.State;
                    var ruleText = rule.Selector.Name + (rule.Part is null ? "" : "::" + rule.Part.Name) + AvaloniaThemeSupportEntry.StateText (state);

                    // A state with no Avalonia seam at all is reported once for the whole state; a state that has one is
                    // answered declaration by declaration below, like the resting look.
                    if (state > ThemeCssState.Hover && !AvaloniaThemeSupport.AppliesInState (rule.Selector.Name, state)) {
                        var stateText = rule.Selector.Name + AvaloniaThemeSupportEntry.StateText (state);
                        if (seen.Add ("state:" + stateText))
                            build.Diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Info, rule.Line, rule.Column,
                                $"'{stateText}' has no Avalonia counterpart (only :hover maps); the rule styles Majorsilence.Forms controls only."));
                        continue;
                    }

                    if (mapping.AvaloniaTypes is null) {
                        if (seen.Add ("selector:" + rule.Selector.Name))
                            build.Diagnostics.Add (new ThemeCssDiagnostic (ThemeCssSeverity.Info, rule.Line, rule.Column,
                                $"'{rule.Selector.Name}' has no Avalonia counterpart; the rule styles Majorsilence.Forms controls only. {mapping.Notes}"));
                        continue;
                    }

                    foreach (var declaration in rule.Declarations) {
                        var entry = AvaloniaThemeSupport.Find (rule.Selector.Name, rule.Part?.Name, state, declaration.Property);
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

                        Write (entry, declaration.Value);
                    }
                }

            BuildMotion (chain, StyleFor);

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
                case ResourceKind.Brush when value.Gradients.Count > 0:
                    return GradientBrush (value.Gradients[0]);
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
                return value.Kind == ThemeCssValueKind.Color ? new SolidColorBrush (ToColor (value))
                    : value.Gradients.Count > 0 ? GradientBrush (value.Gradients[0])
                    : null;
            if (property.PropertyType == typeof (BoxShadows))
                return value.Kind is ThemeCssValueKind.BoxShadow or ThemeCssValueKind.List ? ToBoxShadows (value) : null;
            if (property.PropertyType == typeof (double) && value.Kind == ThemeCssValueKind.Number)
                return value.Number;
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

        // ---- effects ---------------------------------------------------------------------------------

        // box-shadow: the first shadow of a list is the topmost, as in Avalonia's BoxShadows; "none" is the empty list.
        internal static BoxShadows ToBoxShadows (ThemeCssValue value)
        {
            var shadows = new List<BoxShadow> ();

            void Add (ThemeCssValue v)
            {
                if (v.Kind == ThemeCssValueKind.BoxShadow)
                    shadows.Add (new BoxShadow { OffsetX = v.Pixels, OffsetY = v.OffsetY, Blur = v.Blur, Spread = v.Spread, Color = ToColor (v), IsInset = v.Inset });
            }

            if (value.Kind == ThemeCssValueKind.List)
                foreach (var item in value.Items)
                    Add (item);
            else
                Add (value);

            return shadows.Count == 0 ? default : new BoxShadows (shadows[0], shadows.Skip (1).ToArray ());
        }

        // The stops of a gradient as Avalonia offsets (0 to 1), by CSS's fix-up: a missing first or last position is the start or end,
        // a position before an earlier one moves up to it, the ones in between spread evenly. A stop in pixels has no meaning without
        // the box, so it takes its even place (Approximate, as the support matrix says). Returns where the span the stops cover starts
        // and ends, which a repeating gradient repeats.
        private static (List<GradientStop> Stops, double First, double Last) GradientStops (ThemeCssGradient gradient)
        {
            var count = gradient.Stops.Count;
            var at = new double?[count];

            for (var i = 0; i < count; i++) {
                var stop = gradient.Stops[i];
                at[i] = stop.Unit switch {
                    "percent" => stop.Position / 100,
                    "angle" => stop.Position / 360,
                    _ => null
                };
            }

            at[0] ??= 0;
            at[count - 1] ??= 1;

            var max = at[0]!.Value;
            for (var i = 1; i < count; i++) {
                if (at[i] is { } p) {
                    if (p < max)
                        at[i] = max;
                    max = at[i]!.Value;
                }
            }

            for (var i = 1; i < count - 1; i++) {
                if (at[i] is not null)
                    continue;

                var next = i + 1;
                while (at[next] is null)
                    next++;

                var from = at[i - 1]!.Value;
                var to = at[next]!.Value;
                for (var k = i; k < next; k++)
                    at[k] = from + (to - from) * (k - i + 1) / (next - i + 1);
            }

            var first = at[0]!.Value;
            var last = at[count - 1]!.Value;
            var span = last - first;
            var stops = new List<GradientStop> ();

            for (var i = 0; i < count; i++)
                stops.Add (new GradientStop (ToColor (gradient.Stops[i].Argb), span > 0 && gradient.Repeating ? (at[i]!.Value - first) / span : at[i]!.Value));

            return (stops, first, last);
        }

        private static Color ToColor (uint argb) => Color.FromArgb ((byte) (argb >> 24), (byte) (argb >> 16), (byte) (argb >> 8), (byte) argb);

        // A gradient as an Avalonia brush. Brushes are in relative units, so a non-square box bends a diagonal a little and a circle
        // becomes an ellipse (Approximate); a repeating gradient repeats the span its stops cover.
        internal static IBrush? GradientBrush (ThemeCssGradient gradient)
        {
            if (gradient.Stops.Count == 0)
                return null;

            var (stops, first, last) = GradientStops (gradient);
            var repeat = gradient.Repeating && last > first;

            switch (gradient.Kind) {
                case ControlGradientKind.Linear: {
                    var angle = gradient.Corner switch {
                        "top" => 0, "top right" or "right top" => 45, "right" => 90, "bottom right" or "right bottom" => 135, "bottom" => 180,
                        "bottom left" or "left bottom" => 225, "left" => 270, "top left" or "left top" => 315, _ => gradient.AngleDegrees
                    };
                    var radians = angle * Math.PI / 180;
                    double dx = Math.Sin (radians), dy = -Math.Cos (radians);
                    var length = Math.Abs (dx) + Math.Abs (dy);
                    double sx = 0.5 - dx * length / 2, sy = 0.5 - dy * length / 2, ex = 0.5 + dx * length / 2, ey = 0.5 + dy * length / 2;

                    if (repeat)
                        (sx, sy, ex, ey) = (sx + (ex - sx) * first, sy + (ey - sy) * first, sx + (ex - sx) * last, sy + (ey - sy) * last);

                    var brush = new LinearGradientBrush {
                        StartPoint = new RelativePoint (sx, sy, RelativeUnit.Relative),
                        EndPoint = new RelativePoint (ex, ey, RelativeUnit.Relative),
                        SpreadMethod = repeat ? GradientSpreadMethod.Repeat : GradientSpreadMethod.Pad,
                    };
                    brush.GradientStops.AddRange (stops);
                    return brush;
                }

                case ControlGradientKind.Radial: {
                    double cx = gradient.CenterX.IsLength ? 0.5 : gradient.CenterX.Value, cy = gradient.CenterY.IsLength ? 0.5 : gradient.CenterY.Value;
                    double farX = Math.Max (cx, 1 - cx), farY = Math.Max (cy, 1 - cy), closeX = Math.Min (cx, 1 - cx), closeY = Math.Min (cy, 1 - cy);
                    var root2 = Math.Sqrt (2);
                    var (rx, ry) = gradient.SizeKeyword switch {
                        "closest-side" => (closeX, closeY),
                        "farthest-side" => (farX, farY),
                        "closest-corner" => (closeX * root2, closeY * root2),
                        "explicit" when !gradient.SizeX.IsLength => (gradient.SizeX.Value, gradient.Circle || gradient.SizeY.IsLength ? gradient.SizeX.Value : gradient.SizeY.Value),
                        _ => (farX * root2, farY * root2)
                    };

                    if (repeat)
                        (rx, ry) = (rx * last, ry * last);

                    var brush = new RadialGradientBrush {
                        Center = new RelativePoint (cx, cy, RelativeUnit.Relative),
                        GradientOrigin = new RelativePoint (cx, cy, RelativeUnit.Relative),
                        RadiusX = new RelativeScalar (rx, RelativeUnit.Relative),
                        RadiusY = new RelativeScalar (ry, RelativeUnit.Relative),
                        SpreadMethod = repeat ? GradientSpreadMethod.Repeat : GradientSpreadMethod.Pad,
                    };
                    brush.GradientStops.AddRange (stops);
                    return brush;
                }

                default: {
                    var brush = new ConicGradientBrush {
                        Center = new RelativePoint (gradient.CenterX.IsLength ? 0.5 : gradient.CenterX.Value, gradient.CenterY.IsLength ? 0.5 : gradient.CenterY.Value, RelativeUnit.Relative),
                        Angle = gradient.FromDegrees,
                    };
                    brush.GradientStops.AddRange (stops);
                    return brush;
                }
            }
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
