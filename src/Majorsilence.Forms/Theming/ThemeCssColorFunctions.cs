using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // The CSS Color 4 and 5 colour functions: rgb(), hsl(), hwb(), lab(), lch(), oklab(), oklch() and color(), each in its modern
    // space-separated form (with 'none', calc() and '/ alpha') and, for rgb() and hsl(), the legacy comma form; the relative form
    // (oklch(from var(--accent-color) calc(l * .9) c h)); color-mix(); and light-dark(). Each returns a thunk, like every value parser
    // here, so a colour built from a theme token follows the token.
    internal static class ThemeCssColorFunctions
    {
        private static readonly string[] names = { "rgb", "rgba", "hsl", "hsla", "hwb", "lab", "lch", "oklab", "oklch", "color", "color-mix", "light-dark" };

        public static bool IsColorFunction (string name) => names.Contains (name, StringComparer.OrdinalIgnoreCase);

        public static bool TryParse (CssFunction fn, out Func<SKColor> color, out string? error)
        {
            color = null!;
            error = null;

            switch (fn.Name.ToLowerInvariant ()) {
                case "rgb":
                case "rgba":
                    return TryParseChannels (fn, CssColorSpace.Srgb, rgbScale: true, out color, out error);
                case "hsl":
                case "hsla":
                    return TryParseChannels (fn, CssColorSpace.Hsl, false, out color, out error);
                case "hwb":
                    return TryParseChannels (fn, CssColorSpace.Hwb, false, out color, out error);
                case "lab":
                    return TryParseChannels (fn, CssColorSpace.Lab, false, out color, out error);
                case "lch":
                    return TryParseChannels (fn, CssColorSpace.Lch, false, out color, out error);
                case "oklab":
                    return TryParseChannels (fn, CssColorSpace.Oklab, false, out color, out error);
                case "oklch":
                    return TryParseChannels (fn, CssColorSpace.Oklch, false, out color, out error);
                case "color":
                    return TryParseChannels (fn, null, false, out color, out error);
                case "color-mix":
                    return TryParseMix (fn, out color, out error);
                case "light-dark":
                    return TryParseLightDark (fn, out color, out error);
                default:
                    error = fn.Name.Equals ("var", StringComparison.OrdinalIgnoreCase)
                        ? $"'{fn.Raw}' could not be resolved."
                        : $"'{fn.Name}()' is not a supported color function. {ThemeCssValues.ColorSyntaxHelp}";
                    return false;
            }
        }

        // ---- rgb(), hsl(), hwb(), lab(), lch(), oklab(), oklch(), color() --------------------------

        // `space` is null for color(), whose first component names it. rgb() writes its channels 0..255, which rgbScale undoes.
        private static bool TryParseChannels (CssFunction fn, CssColorSpace? space, bool rgbScale, out Func<SKColor> color, out string? error)
        {
            color = null!;
            var name = fn.Name.ToLowerInvariant ();

            if (fn.Arguments.Count > 1)
                return TryParseLegacy (fn, space, rgbScale, out color, out error);

            var components = fn.Arguments.Count == 1 ? fn.Arguments[0] : new List<CssComponent> ();
            var position = 0;

            // The relative form: fn(from <color> ...), whose channel names read the origin colour.
            Func<SKColor>? origin = null;

            if (position < components.Count && components[position] is CssIdent { Name: var from } && from.Equals ("from", StringComparison.OrdinalIgnoreCase)) {
                if (position + 1 >= components.Count) {
                    error = $"'{fn.Raw}': 'from' must be followed by the origin colour, e.g. {name}(from var(--accent-color) ...).";
                    return false;
                }

                if (!ThemeCssValues.TryParseColor (new List<CssComponent> { components[position + 1] }, out var parsedOrigin, out error)) {
                    error = $"'{fn.Raw}': the origin colour: {error}";
                    return false;
                }

                origin = parsedOrigin;
                position += 2;
            }

            if (space is null) {
                if (position >= components.Count || components[position] is not CssIdent spaceName
                    || !ThemeCssColorSpaces.TryParseSpace (spaceName.Name, out var named)
                    || named is CssColorSpace.Lab or CssColorSpace.Lch or CssColorSpace.Oklab or CssColorSpace.Oklch or CssColorSpace.Hsl or CssColorSpace.Hwb) {
                    error = $"'{fn.Raw}': color() starts with a colour space: srgb, srgb-linear, display-p3, xyz, xyz-d50 or xyz-d65, e.g. color(display-p3 1 0.5 0).";
                    return false;
                }

                space = named;
                position++;
            }

            var channelSpecs = ThemeCssColorSpaces.Channels (space.Value);

            if (rgbScale)
                channelSpecs = channelSpecs.Select (c => (c.Name, 255.0, c.IsHue)).ToArray ();

            // The origin's channels, refilled each time the colour is evaluated, and the names that read them.
            var originValues = new double[4];
            Dictionary<string, Func<double>>? env = null;

            if (origin is not null) {
                env = new Dictionary<string, Func<double>> (StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < 3; i++) {
                    var index = i;
                    env[channelSpecs[i].Name] = () => originValues[index];
                }
                env["alpha"] = () => originValues[3];
            }

            var channels = new Func<double>[4];
            var count = 0;

            while (position < components.Count && components[position] is not CssDelim { Value: '/' }) {
                if (count == 3) {
                    error = $"'{fn.Raw}': {name}() takes three channels{(name == "color" ? " after the colour space" : "")}, then optionally '/ alpha'; '{components[position].Raw}' is a fourth.";
                    return false;
                }

                if (!TryCompileChannel (components[position], channelSpecs[count], env, out channels[count], out error)) {
                    error = $"'{fn.Raw}': {error}";
                    return false;
                }

                count++;
                position++;
            }

            if (count != 3) {
                error = $"'{fn.Raw}' is not a valid color: {name}() needs three channels{(origin is null ? "" : " (the origin's own are r g b, l c h... by name)")}, got {count}. {Example (name)}";
                return false;
            }

            channels[3] = origin is null ? () => 1.0 : () => originValues[3];

            if (position < components.Count) {
                position++;   // '/'

                if (position != components.Count - 1) {
                    error = $"'{fn.Raw}': '/' must be followed by exactly one alpha value, e.g. / 0.5 or / 50%.";
                    return false;
                }

                if (!TryCompileChannel (components[position], ("alpha", 1.0, false), env, out channels[3], out error)) {
                    error = $"'{fn.Raw}': {error}";
                    return false;
                }
            }

            var target = space.Value;
            var specs = channelSpecs;
            var gate = new object ();

            color = () => {
                lock (gate) {
                    if (origin is not null) {
                        var o = ThemeCssColorSpaces.FromSKColor (origin (), target);
                        originValues[0] = Channel (o.C0, specs[0], rgbScale);
                        originValues[1] = Channel (o.C1, specs[1], rgbScale);
                        originValues[2] = Channel (o.C2, specs[2], rgbScale);
                        originValues[3] = o.Alpha;
                    }

                    var c0 = channels[0] ();
                    var c1 = channels[1] ();
                    var c2 = channels[2] ();
                    var alpha = MathCompat.Clamp (channels[3] (), 0, 1);

                    if (rgbScale) {
                        c0 = MathCompat.Clamp (c0, 0, 255) / 255;
                        c1 = MathCompat.Clamp (c1, 0, 255) / 255;
                        c2 = MathCompat.Clamp (c2, 0, 255) / 255;
                    }

                    var value = Normalize (new CssColor (c0, c1, c2, alpha), target);
                    return ThemeCssColorSpaces.ToSKColor (value, target);
                }
            };

            error = null;
            return true;
        }

        // An origin's channel as its name reads it: rgb()'s 0..255, a powerless hue as 0.
        private static double Channel (double value, (string Name, double Percent, bool IsHue) spec, bool rgbScale)
        {
            if (spec.IsHue && double.IsNaN (value))
                return 0;
            return rgbScale ? value * 255 : value;
        }

        // CSS's clamps: lightness, saturation, whiteness and blackness stay in range; chroma is not negative; the hue wraps.
        private static CssColor Normalize (CssColor color, CssColorSpace space)
        {
            switch (space) {
                case CssColorSpace.Hsl:
                    return new CssColor (Wrap (color.C0), MathCompat.Clamp (color.C1, 0, 100), MathCompat.Clamp (color.C2, 0, 100), color.Alpha);
                case CssColorSpace.Hwb:
                    return new CssColor (Wrap (color.C0), MathCompat.Clamp (color.C1, 0, 100), MathCompat.Clamp (color.C2, 0, 100), color.Alpha);
                case CssColorSpace.Lab:
                    return new CssColor (MathCompat.Clamp (color.C0, 0, 100), color.C1, color.C2, color.Alpha);
                case CssColorSpace.Lch:
                    return new CssColor (MathCompat.Clamp (color.C0, 0, 100), Math.Max (0, color.C1), Wrap (color.C2), color.Alpha);
                case CssColorSpace.Oklab:
                    return new CssColor (MathCompat.Clamp (color.C0, 0, 1), color.C1, color.C2, color.Alpha);
                case CssColorSpace.Oklch:
                    return new CssColor (MathCompat.Clamp (color.C0, 0, 1), Math.Max (0, color.C1), Wrap (color.C2), color.Alpha);
                default:
                    return color;
            }
        }

        private static double Wrap (double hue) => double.IsNaN (hue) ? 0 : ((hue % 360) + 360) % 360;

        // One channel: a number, a percentage of the channel's 100% value, an angle for a hue, 'none', a calc(), or an origin channel.
        private static bool TryCompileChannel (CssComponent component, (string Name, double Percent, bool IsHue) spec,
            Dictionary<string, Func<double>>? env, out Func<double> channel, out string? error)
        {
            channel = null!;
            error = null;

            if (component is CssIdent { Name: var none } && none.Equals ("none", StringComparison.OrdinalIgnoreCase) && (env is null || !env.ContainsKey (none))) {
                channel = () => 0;
                return true;
            }

            if (!ThemeCssCalc.TryCompile (component, env, out var value, out error))
                return false;

            switch (value.Unit) {
                case CalcUnit.Number:
                    channel = value.Evaluate;
                    return true;
                case CalcUnit.Percent when !spec.IsHue:
                    var scale = spec.Percent / 100;
                    var evaluate = value.Evaluate;
                    channel = () => evaluate () * scale;
                    return true;
                case CalcUnit.Angle when spec.IsHue:
                    channel = value.Evaluate;
                    return true;
                default:
                    error = spec.IsHue
                        ? $"the hue '{component.Raw}' must be a number of degrees or an angle (deg, rad, grad, turn)."
                        : $"the {spec.Name} channel '{component.Raw}' must be a number or a percentage, not {ThemeCssCalc.Describe (value.Unit)}.";
                    return false;
            }
        }

        // rgb(r, g, b[, a]) and hsl(h, s, l[, a]): the comma form, which takes no 'none', no relative origin and no '/'.
        private static bool TryParseLegacy (CssFunction fn, CssColorSpace? space, bool rgbScale, out Func<SKColor> color, out string? error)
        {
            color = null!;
            var name = fn.Name.ToLowerInvariant ();

            if (space is not (CssColorSpace.Srgb or CssColorSpace.Hsl)) {
                error = $"'{fn.Raw}': {name}() takes space-separated channels, e.g. {Example (name)} Commas are only the legacy form of rgb() and hsl().";
                return false;
            }

            if (fn.Arguments.Count is not (3 or 4)) {
                error = $"'{fn.Raw}' is not a valid color: expected 3 components plus an optional alpha, got {fn.Arguments.Count}.";
                return false;
            }

            var specs = ThemeCssColorSpaces.Channels (space.Value);

            if (rgbScale)
                specs = specs.Select (c => (c.Name, 255.0, c.IsHue)).ToArray ();

            var channels = new Func<double>[4];
            channels[3] = () => 1.0;

            for (var i = 0; i < fn.Arguments.Count; i++) {
                var group = fn.Arguments[i];

                if (group.Count != 1 || group[0] is CssIdent) {
                    var bad = group.Count == 0 ? "an empty value" : $"'{string.Join (" ", group.Select (c => c.Raw))}'";
                    error = $"'{fn.Raw}' is not a valid color: {bad} is not a number. Example: {(name.StartsWith ("hsl", StringComparison.Ordinal) ? "hsl(210, 60%, 50%)" : "rgb(42, 138, 208)")}.";
                    return false;
                }

                var spec = i == 3 ? ("alpha", 1.0, false) : specs[i];

                // The legacy hsl() wrote saturation and lightness as percentages; a bare number there has always meant the same.
                if (!TryCompileChannel (group[0], spec, null, out channels[i], out error)) {
                    error = $"'{fn.Raw}': {error}";
                    return false;
                }
            }

            var target = space.Value;

            color = () => {
                var c0 = channels[0] ();
                var c1 = channels[1] ();
                var c2 = channels[2] ();
                var alpha = MathCompat.Clamp (channels[3] (), 0, 1);

                if (rgbScale)
                    return new SKColor (Byte (c0), Byte (c1), Byte (c2), Byte (alpha * 255));

                return ThemeCssColorSpaces.ToSKColor (Normalize (new CssColor (c0, c1, c2, alpha), target), target);
            };

            error = null;
            return true;
        }

        private static byte Byte (double value) => (byte) MathCompat.Clamp (Math.Round (value), 0, 255);

        private static string Example (string name) => name switch {
            "rgb" or "rgba" => "Example: rgb(42 138 208) or rgb(42 138 208 / 50%).",
            "hsl" or "hsla" => "Example: hsl(210 60% 50%).",
            "hwb" => "Example: hwb(210 10% 20%).",
            "lab" => "Example: lab(55% 40 -60).",
            "lch" => "Example: lch(55% 70 300).",
            "oklab" => "Example: oklab(0.6 0.1 -0.1).",
            "oklch" => "Example: oklch(60% 0.24 240).",
            _ => "Example: color(display-p3 1 0.5 0)."
        };

        // ---- color-mix() -------------------------------------------------------------------------

        private static bool TryParseMix (CssFunction fn, out Func<SKColor> color, out string? error)
        {
            color = null!;
            const string Form = "The form is color-mix(in <space> [shorter | longer | increasing | decreasing hue], <color> [<percentage>], <color> [<percentage>]), e.g. color-mix(in oklch, var(--accent-color) 70%, white).";

            var head = fn.Arguments.Count > 0 ? fn.Arguments[0] : new List<CssComponent> ();

            if (head.Count < 2 || head[0] is not CssIdent { Name: var inWord } || !inWord.Equals ("in", StringComparison.OrdinalIgnoreCase)
                || head[1] is not CssIdent spaceName) {
                error = $"'{fn.Raw}': color-mix() starts with 'in' and a colour space. {Form}";
                return false;
            }

            if (fn.Arguments.Count != 3) {
                error = $"'{fn.Raw}': color-mix() takes a colour space and two colours, comma-separated. {Form}";
                return false;
            }

            if (!ThemeCssColorSpaces.TryParseSpace (spaceName.Name, out var space)) {
                error = $"'{fn.Raw}': '{spaceName.Name}' is not a colour space.{ThemeCssValues.Suggest (spaceName.Name, ThemeCssColorSpaces.SpaceList)} Spaces: {ThemeCssColorSpaces.SpaceNames}.";
                return false;
            }

            var hueMethod = "shorter";

            if (head.Count == 4 && head[2] is CssIdent method && head[3] is CssIdent { Name: var hueWord } && hueWord.Equals ("hue", StringComparison.OrdinalIgnoreCase)) {
                hueMethod = method.Name.ToLowerInvariant ();

                if (hueMethod is not ("shorter" or "longer" or "increasing" or "decreasing")) {
                    error = $"'{fn.Raw}': '{method.Name} hue' is not a hue interpolation method; use shorter, longer, increasing or decreasing hue.";
                    return false;
                }

                if (ThemeCssColorSpaces.HueIndex (space) < 0) {
                    error = $"'{fn.Raw}': {spaceName.Name} has no hue, so '{method.Name} hue' does not apply. It does in lch, oklch, hsl and hwb.";
                    return false;
                }
            } else if (head.Count != 2) {
                error = $"'{fn.Raw}': unexpected '{string.Join (" ", head.Skip (2).Select (c => c.Raw))}' after the colour space. {Form}";
                return false;
            }

            if (!TryParseMixColor (fn, fn.Arguments[1], out var first, out var p1, out error)
                || !TryParseMixColor (fn, fn.Arguments[2], out var second, out var p2, out error))
                return false;

            if (p1 is not null && p2 is not null && p1.Value.IsConstant && p2.Value.IsConstant && p1.Value.Evaluate () + p2.Value.Evaluate () == 0) {
                error = $"'{fn.Raw}': the two percentages add up to 0%, which mixes nothing.";
                return false;
            }

            var hue = ThemeCssColorSpaces.HueIndex (space);
            var target = space;

            color = () => {
                var w1 = p1?.Evaluate ();
                var w2 = p2?.Evaluate ();

                if (w1 is null && w2 is null) {
                    w1 = 50;
                    w2 = 50;
                } else if (w1 is null) {
                    w1 = 100 - w2!.Value;
                } else if (w2 is null) {
                    w2 = 100 - w1.Value;
                }

                var sum = w1.Value + w2!.Value;
                if (sum <= 0)
                    return SKColors.Transparent;

                // Percentages that add up to less than 100% make the result that much more transparent.
                var alphaMultiplier = sum < 100 ? sum / 100 : 1;
                var t = w2.Value / sum;

                var a = ThemeCssColorSpaces.FromSKColor (first (), target);
                var b = ThemeCssColorSpaces.FromSKColor (second (), target);

                if (hue >= 0)
                    FixHues (ref a, ref b, hue, hueMethod);

                var alpha = a.Alpha + (b.Alpha - a.Alpha) * t;
                var mixed = new CssColor (
                    Mix (a, b, 0, hue, t, alpha),
                    Mix (a, b, 1, hue, t, alpha),
                    Mix (a, b, 2, hue, t, alpha),
                    alpha * alphaMultiplier);

                return ThemeCssColorSpaces.ToSKColor (Normalize (mixed, target), target);
            };

            error = null;
            return true;
        }

        private static bool TryParseMixColor (CssFunction fn, List<CssComponent> group, out Func<SKColor> color, out CalcValue? percentage, out string? error)
        {
            color = null!;
            percentage = null;
            error = null;
            CssComponent? colorComponent = null;

            foreach (var component in group) {
                if (component is CssNumber { IsPercentage: true } || (ThemeCssCalc.IsMathFunction (component) && percentage is null && colorComponent is not null)) {
                    if (!ThemeCssCalc.TryCompile (component, null, out var value, out error) || value.Unit != CalcUnit.Percent) {
                        error = $"'{fn.Raw}': '{component.Raw}' must be a percentage.";
                        return false;
                    }
                    if (value.IsConstant && value.Evaluate () is < 0 or > 100) {
                        error = $"'{fn.Raw}': '{component.Raw}' must be between 0% and 100%.";
                        return false;
                    }
                    if (percentage is not null) {
                        error = $"'{fn.Raw}': each colour takes one percentage.";
                        return false;
                    }
                    percentage = value;
                    continue;
                }

                if (colorComponent is not null) {
                    error = $"'{fn.Raw}': '{component.Raw}' -- each comma-separated part is one colour and an optional percentage.";
                    return false;
                }

                colorComponent = component;
            }

            if (colorComponent is null) {
                error = $"'{fn.Raw}': a colour is missing.";
                return false;
            }

            if (!ThemeCssValues.TryParseColor (new List<CssComponent> { colorComponent }, out color, out error)) {
                error = $"'{fn.Raw}': {error}";
                return false;
            }

            return true;
        }

        // A powerless hue (an achromatic colour) takes the other colour's, so mixing with white or grey keeps the hue; then the hue
        // interpolation method picks which way round the circle to go (CSS Color 4, 12.4).
        private static void FixHues (ref CssColor a, ref CssColor b, int index, string method)
        {
            var h1 = Get (a, index);
            var h2 = Get (b, index);

            if (double.IsNaN (h1) && double.IsNaN (h2))
                h1 = h2 = 0;
            else if (double.IsNaN (h1))
                h1 = h2;
            else if (double.IsNaN (h2))
                h2 = h1;

            var difference = h2 - h1;

            switch (method) {
                case "longer":
                    if (difference > 0 && difference < 180)
                        h1 += 360;
                    else if (difference > -180 && difference <= 0)
                        h2 += 360;
                    break;
                case "increasing":
                    if (h2 < h1)
                        h2 += 360;
                    break;
                case "decreasing":
                    if (h1 < h2)
                        h1 += 360;
                    break;
                default:
                    if (difference > 180)
                        h1 += 360;
                    else if (difference < -180)
                        h2 += 360;
                    break;
            }

            Set (ref a, index, h1);
            Set (ref b, index, h2);
        }

        // One channel of the mix, premultiplied by alpha unless it is the hue, as CSS interpolates.
        private static double Mix (CssColor a, CssColor b, int index, int hue, double t, double alpha)
        {
            var x = Get (a, index);
            var y = Get (b, index);

            if (index == hue)
                return x + (y - x) * t;

            var premultiplied = x * a.Alpha + (y * b.Alpha - x * a.Alpha) * t;
            return alpha > 0 ? premultiplied / alpha : 0;
        }

        private static double Get (CssColor color, int index) => index switch { 0 => color.C0, 1 => color.C1, _ => color.C2 };

        private static void Set (ref CssColor color, int index, double value)
        {
            switch (index) {
                case 0: color.C0 = value; break;
                case 1: color.C1 = value; break;
                default: color.C2 = value; break;
            }
        }

        // ---- light-dark() ------------------------------------------------------------------------

        private static bool TryParseLightDark (CssFunction fn, out Func<SKColor> color, out string? error)
        {
            color = null!;

            if (fn.Arguments.Count != 2 || fn.Arguments.Any (g => g.Count != 1)) {
                error = $"'{fn.Raw}': light-dark() takes two colours, the one for a light theme first: light-dark(#1a1a1a, #f0f0f0).";
                return false;
            }

            if (!ThemeCssValues.TryParseColor (fn.Arguments[0], out var light, out error)
                || !ThemeCssValues.TryParseColor (fn.Arguments[1], out var dark, out error)) {
                error = $"'{fn.Raw}': {error}";
                return false;
            }

            color = () => IsDarkTheme () ? dark () : light ();
            return true;
        }

        /// <summary>
        /// Whether the theme in effect is dark: its <see cref="Theme.BackgroundColor"/> is darker than mid-grey (OKLab lightness under
        /// 0.5). It is what <c>light-dark()</c> picks by -- the theme's own colours, not the operating system's setting -- so the same
        /// sheet extending Light or Dark picks the right colour of each pair.
        /// </summary>
        internal static bool IsDarkTheme ()
            => ThemeCssColorSpaces.FromSKColor (Theme.BackgroundColor, CssColorSpace.Oklab).C0 < 0.5;
    }
}
