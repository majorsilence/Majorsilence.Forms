using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Majorsilence.Forms.Animation;
using SkiaSharp;

namespace Majorsilence.Forms
{
    // The parsed pieces of a declaration's value, after the tokenizer and before a property gives them
    // meaning. A var() reference to a theme token survives as a CssTokenRef so the compiled rule can
    // read the token's *current* value every time it is applied.
    internal abstract class CssComponent
    {
        protected CssComponent (int line, int column, string raw)
        {
            Line = line;
            Column = column;
            Raw = raw;
        }

        public int Line { get; }
        public int Column { get; }
        public string Raw { get; }

        public override string ToString () => Raw;
    }

    internal sealed class CssIdent : CssComponent
    {
        public CssIdent (string name, int line, int column) : base (line, column, name) => Name = name;
        public string Name { get; }
    }

    internal sealed class CssHash : CssComponent
    {
        public CssHash (string name, int line, int column) : base (line, column, "#" + name) => Name = name;
        public string Name { get; }
    }

    internal sealed class CssString : CssComponent
    {
        public CssString (string value, int line, int column, string raw) : base (line, column, raw) => Value = value;
        public string Value { get; }
    }

    internal sealed class CssNumber : CssComponent
    {
        public CssNumber (double value, string unit, int line, int column, string raw) : base (line, column, raw)
        {
            Value = value;
            Unit = unit;
        }

        public double Value { get; }
        public string Unit { get; }
        public bool IsPercentage => Unit == "%";
    }

    internal sealed class CssFunction : CssComponent
    {
        public CssFunction (string name, List<List<CssComponent>> arguments, int line, int column, string raw) : base (line, column, raw)
        {
            Name = name;
            Arguments = arguments;
        }

        public string Name { get; }

        /// <summary>The comma-separated argument groups; each group is the components between commas.</summary>
        public List<List<CssComponent>> Arguments { get; }
    }

    internal sealed class CssDelim : CssComponent
    {
        public CssDelim (char value, int line, int column) : base (line, column, value.ToString ()) => Value = value;
        public char Value { get; }
    }

    internal sealed class CssTokenRef : CssComponent
    {
        public CssTokenRef (ThemeCssToken token, int line, int column) : base (line, column, $"var({token.Name})") => Token = token;
        public ThemeCssToken Token { get; }
    }

    // Value interpretation shared by the ':root' token declarations and the control-rule properties:
    // colors, pixel lengths, font families/weights/slants. Every TryParse* returns a *thunk* rather than
    // a value because a var(--token) reference must track the token as the theme changes later.
    internal static class ThemeCssValues
    {
        public const string ColorSyntaxHelp =
            "Use #rgb, #rrggbb, #rrggbbaa, rgb(), hsl(), hwb(), lab(), lch(), oklab(), oklch(), color(display-p3 ...), color-mix(in oklch, a, b), "
            + "light-dark(light, dark), a CSS color name, 'transparent', or var(--token).";

        public const string LengthSyntaxHelp = "Lengths are pixels: write e.g. '14px', '14', '0.875rem' (1rem is --root-font-size, 16px by default) or calc(1rem + 2px).";

        public const string BoxShadowSyntaxHelp =
            "A shadow is '[inset] <offset-x> <offset-y> [<blur> [<spread>]] <color>', and box-shadow is a comma-separated list of them or 'none', "
            + "e.g. 'box-shadow: 0 1px 3px rgb(0 0 0 / 20%), inset 0 0 0 1px #fff;'.";

        private static readonly ConcurrentDictionary<string, SKTypeface> typefaces = new (StringComparer.OrdinalIgnoreCase);

        // Whether the system font manager really has a family, as opposed to substituting a default for it. Only asked while a private
        // font is registered, to decide whether an earlier family in a list beats a later private one.
        private static readonly ConcurrentDictionary<string, bool> systemHas = new (StringComparer.OrdinalIgnoreCase);

        // ---- colors ------------------------------------------------------------------------------

        public static bool TryParseColor (List<CssComponent> components, out Func<SKColor> color, out string? error)
        {
            color = null!;
            error = null;

            if (components.Count != 1) {
                error = $"'{Join (components)}' is not a valid color. {ColorSyntaxHelp}";
                return false;
            }

            var component = components[0];

            switch (component) {
                case CssHash hash:
                    if (TryParseHexColor (hash.Name, out var hex)) {
                        color = Constant (hex);
                        return true;
                    }
                    error = $"'#{hash.Name}' is not a valid hex color: expected 3, 4, 6 or 8 hex digits (#rgb, #rgba, #rrggbb or #rrggbbaa -- note the alpha comes LAST, unlike the #AARRGGBB form used by XML themes).";
                    return false;

                case CssIdent ident:
                    if (string.Equals (ident.Name, "transparent", StringComparison.OrdinalIgnoreCase)) {
                        color = Constant (SKColors.Transparent);
                        return true;
                    }
                    if (NamedColors.TryGetValue (ident.Name, out var named)) {
                        color = Constant (named);
                        return true;
                    }
                    if (string.Equals (ident.Name, "currentcolor", StringComparison.OrdinalIgnoreCase)) {
                        error = "'currentcolor' is not supported: a type rule has no element whose colour it could follow. Use var(--foreground-color) for the theme's text colour.";
                        return false;
                    }
                    error = $"'{ident.Name}' is not a CSS color name. {ColorSyntaxHelp}"
                        + Suggest (ident.Name, NamedColors.Keys);
                    return false;

                case CssFunction fn:
                    return TryParseColorFunction (fn, out color, out error);

                case CssTokenRef tokenRef:
                    if (tokenRef.Token.Kind != ThemeCssValueKind.Color) {
                        error = $"'{tokenRef.Token.Name}' is a {DescribeKind (tokenRef.Token.Kind)} token, not a color, so it cannot be used where a color is expected.";
                        return false;
                    }
                    var token = tokenRef.Token;
                    color = () => (SKColor) Theme.GetTokenValue (token);
                    return true;

                default:
                    error = $"'{component.Raw}' is not a valid color. {ColorSyntaxHelp}";
                    return false;
            }
        }

        private static bool TryParseColorFunction (CssFunction fn, out Func<SKColor> color, out string? error)
            => ThemeCssColorFunctions.TryParse (fn, out color, out error);

        private static byte ClampByte (double value) => (byte) MathCompat.Clamp (Math.Round (value), 0, 255);

        public static bool TryParseHexColor (string hex, out SKColor color)
        {
            color = SKColor.Empty;

            if (hex.Length is not (3 or 4 or 6 or 8) || !hex.All (Uri.IsHexDigit))
                return false;

            if (hex.Length <= 4) {
                var sb = new StringBuilder ();
                foreach (var c in hex)
                    sb.Append (c).Append (c);
                hex = sb.ToString ();
            }

            var r = HexByte (hex, 0);
            var g = HexByte (hex, 2);
            var b = HexByte (hex, 4);
            var a = hex.Length == 8 ? HexByte (hex, 6) : (byte) 255;

            color = new SKColor (r, g, b, a);
            return true;
        }

        // Two hex digits at `index`; the caller has already checked every character is a hex digit.
        private static byte HexByte (string hex, int index)
            => (byte) ((Uri.FromHex (hex[index]) << 4) | Uri.FromHex (hex[index + 1]));

        /// <summary>Formats a color the way the stylesheet would spell it: #rrggbb, or #rrggbbaa when translucent.</summary>
        public static string FormatColor (SKColor color)
            => color.Alpha == 255
                ? $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}"
                : $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}{color.Alpha:x2}";

        // ---- lengths -----------------------------------------------------------------------------

        public static bool TryParseLength (List<CssComponent> components, out Func<int> length, out string? error, bool allowNegative = false)
        {
            length = null!;
            error = null;

            if (components.Count != 1) {
                error = $"'{Join (components)}' is not a valid length. {LengthSyntaxHelp}";
                return false;
            }

            switch (components[0]) {
                // rem computes; em and pt go the same way for their specific explanations.
                case CssNumber relative when relative.Unit.ToLowerInvariant () is "rem" or "em" or "pt":
                    return TryParseComputedLength (relative, out length, out error, allowNegative);

                case CssFunction fn when ThemeCssCalc.IsMathFunction (fn):
                    return TryParseComputedLength (fn, out length, out error, allowNegative);

                case CssNumber number:
                    if (number.Unit.Length > 0 && !string.Equals (number.Unit, "px", StringComparison.OrdinalIgnoreCase)) {
                        error = number.IsPercentage
                            ? $"'{number.Raw}': percentages are not supported -- a theme length has nothing to be a percentage of. {LengthSyntaxHelp}"
                            : $"Unsupported unit '{number.Unit}' in '{number.Raw}'. {LengthSyntaxHelp} Points and em are not supported: every size is a pixel value, and em would depend on an inherited font size a type rule cannot know.";
                        return false;
                    }
                    if (number.Value < 0 && !allowNegative) {
                        error = $"'{number.Raw}' is negative; lengths must be zero or positive.";
                        return false;
                    }
                    var value = (int) Math.Round (number.Value);
                    length = () => value;
                    return true;

                case CssTokenRef tokenRef:
                    if (tokenRef.Token.Kind != ThemeCssValueKind.Length) {
                        error = $"'{tokenRef.Token.Name}' is a {DescribeKind (tokenRef.Token.Kind)} token, not a length, so it cannot be used where a length is expected.";
                        return false;
                    }
                    var token = tokenRef.Token;
                    length = () => (int) Theme.GetTokenValue (token);
                    return true;

                default:
                    error = $"'{components[0].Raw}' is not a valid length. {LengthSyntaxHelp}";
                    return false;
            }
        }

        // A length written in rem or with calc(): evaluated live (a rem follows --root-font-size, a length token its token) and rounded
        // to whole pixels when applied.
        private static bool TryParseComputedLength (CssComponent component, out Func<int> length, out string? error, bool allowNegative)
        {
            length = null!;

            if (!ThemeCssCalc.TryCompile (component, null, out var value, out error)) {
                error = $"'{component.Raw}': {error}";
                return false;
            }

            // A plain number is accepted as pixels, the same as a bare '14' outside calc().
            if (value.Unit is not (CalcUnit.Length or CalcUnit.Number)) {
                error = $"'{component.Raw}' is {ThemeCssCalc.Describe (value.Unit)}, not a length. {LengthSyntaxHelp}";
                return false;
            }

            if (value.IsConstant && value.Evaluate () < 0 && !allowNegative) {
                error = $"'{component.Raw}' is negative; lengths must be zero or positive.";
                return false;
            }

            var evaluate = value.Evaluate;
            length = allowNegative
                ? () => (int) Math.Round (evaluate ())
                : () => Math.Max (0, (int) Math.Round (evaluate ()));
            return true;
        }

        // ---- box-shadow (#285) --------------------------------------------------------------------

        /// <summary>
        /// Parses a <c>box-shadow</c> value: <c>none</c>, or a comma-separated list of shadows, each
        /// <c>[inset] &lt;offset-x&gt; &lt;offset-y&gt; [&lt;blur&gt; [&lt;spread&gt;]] &lt;color&gt;</c> with the colour and
        /// <c>inset</c> on either side of the lengths, as CSS allows. The colour is required: CSS's default is <c>currentcolor</c>,
        /// which a type rule has no element for.
        /// </summary>
        public static bool TryParseBoxShadows (List<CssComponent> components, out Func<IReadOnlyList<ControlBoxShadow>> shadows, out string? error)
        {
            shadows = null!;
            error = null;

            if (components.Count == 1 && components[0] is CssIdent { Name: var none } && none.Equals ("none", StringComparison.OrdinalIgnoreCase)) {
                var empty = Array.Empty<ControlBoxShadow> ();
                shadows = () => empty;
                return true;
            }

            var parsed = new List<Func<ControlBoxShadow>> ();

            foreach (var group in SplitCommas (components)) {
                if (!TryParseOneShadow (group, out var shadow, out error))
                    return false;
                parsed.Add (shadow);
            }

            var thunks = parsed.ToArray ();
            shadows = () => {
                var result = new ControlBoxShadow[thunks.Length];
                for (var i = 0; i < thunks.Length; i++)
                    result[i] = thunks[i] ();
                return result;
            };
            return true;
        }

        private static bool TryParseOneShadow (List<CssComponent> components, out Func<ControlBoxShadow> shadow, out string? error)
        {
            shadow = null!;
            error = null;

            if (components.Count == 0) {
                error = $"an entry of the list is empty. {BoxShadowSyntaxHelp}";
                return false;
            }

            var inset = false;
            Func<SKColor>? color = null;
            var lengths = new List<Func<int>> ();
            var lengthsDone = false;

            for (var i = 0; i < components.Count; i++) {
                var component = components[i];

                if (component is CssIdent { Name: var word } && word.Equals ("inset", StringComparison.OrdinalIgnoreCase)) {
                    if (inset) {
                        error = $"'inset' is written twice. {BoxShadowSyntaxHelp}";
                        return false;
                    }
                    inset = true;
                    lengthsDone |= lengths.Count > 0;
                    continue;
                }

                if (IsShadowLength (component)) {
                    if (lengthsDone) {
                        error = $"'{Join (components)}': the lengths of a shadow are written together, not split by its colour or 'inset'. {BoxShadowSyntaxHelp}";
                        return false;
                    }

                    var index = lengths.Count;
                    if (index == 4) {
                        error = $"'{Join (components)}' has a fifth length; a shadow takes two offsets, then an optional blur and spread. {BoxShadowSyntaxHelp}";
                        return false;
                    }

                    var part = index switch { 0 => "horizontal offset", 1 => "vertical offset", 2 => "blur radius", _ => "spread" };

                    if (!TryParseLength (new List<CssComponent> { component }, out var length, out var lengthError, allowNegative: index != 2)) {
                        error = $"box-shadow's {part}: {lengthError}";
                        return false;
                    }

                    lengths.Add (length);
                    continue;
                }

                if (color is not null) {
                    error = $"'{component.Raw}': a shadow takes one colour. {BoxShadowSyntaxHelp}";
                    return false;
                }

                if (!TryParseColor (new List<CssComponent> { component }, out var parsedColor, out var colorError)) {
                    error = lengths.Count < 2
                        ? $"box-shadow's horizontal offset: '{component.Raw}' is neither a length nor a colour. {BoxShadowSyntaxHelp}"
                        : $"box-shadow's color: {colorError}";
                    return false;
                }

                color = parsedColor;
                lengthsDone |= lengths.Count > 0;
            }

            if (lengths.Count < 2) {
                error = $"'{Join (components)}' has {(lengths.Count == 0 ? "no offsets" : "only one offset")}. {BoxShadowSyntaxHelp}";
                return false;
            }

            if (color is null) {
                error = $"'{Join (components)}' has no colour. CSS's default is currentcolor, which a type rule has no element for; write one, e.g. rgb(0 0 0 / 15%). {BoxShadowSyntaxHelp}";
                return false;
            }

            var x = lengths[0];
            var y = lengths[1];
            var blur = lengths.Count > 2 ? lengths[2] : null;
            var spread = lengths.Count > 3 ? lengths[3] : null;
            var c = color;

            shadow = () => new ControlBoxShadow (x (), y (), blur?.Invoke () ?? 0, spread?.Invoke () ?? 0, c (), inset);
            return true;
        }

        // A length a shadow can start with: a number (bare zero, px, rem) or a math function. Not a colour.
        private static bool IsShadowLength (CssComponent component)
            => component is CssNumber || ThemeCssCalc.IsMathFunction (component) || component is CssTokenRef { Token.Kind: ThemeCssValueKind.Length };

        /// <summary>Parses an opacity: a number from 0 to 1 or a percentage, possibly computed; out-of-range values are clamped, as in CSS.</summary>
        public static bool TryParseOpacity (List<CssComponent> components, out Func<float> opacity, out string? error)
        {
            opacity = null!;

            if (components.Count != 1) {
                error = $"'{Join (components)}' is not an opacity: write a number from 0 to 1, or a percentage, e.g. 0.65 or 65%.";
                return false;
            }

            if (!ThemeCssCalc.TryCompile (components[0], null, out var value, out error) || value.Unit is not (CalcUnit.Number or CalcUnit.Percent)) {
                error = $"'{components[0].Raw}' is not an opacity: write a number from 0 to 1, or a percentage, e.g. 0.65 or 65%.";
                return false;
            }

            var evaluate = value.Evaluate;
            var scale = value.Unit == CalcUnit.Percent ? 0.01 : 1.0;
            opacity = () => (float) MathCompat.Clamp (evaluate () * scale, 0, 1);
            error = null;
            return true;
        }

        // ---- transitions -------------------------------------------------------------------------

        public const string TimeSyntaxHelp = "Times take a unit: '150ms' or '.15s'.";

        public const string TimingSyntaxHelp =
            "Timing functions are linear, ease, ease-in, ease-out, ease-in-out, step-start, step-end, cubic-bezier(x1, y1, x2, y2) "
            + "(x1 and x2 between 0 and 1) or steps(n[, jump-start | jump-end | jump-none | jump-both | start | end]).";

        /// <summary>Splits a declaration value at its top-level commas; each element is the components between two commas.</summary>
        public static List<List<CssComponent>> SplitCommas (List<CssComponent> components)
        {
            var groups = new List<List<CssComponent>> { new () };

            foreach (var component in components)
                if (component is CssDelim { Value: ',' })
                    groups.Add (new List<CssComponent> ());
                else
                    groups[groups.Count - 1].Add (component);

            return groups;
        }

        /// <summary>Whether <paramref name="component"/> is a number with a time unit, which is how the transition shorthand tells times apart.</summary>
        public static bool IsTime (CssComponent component)
            => component is CssNumber { Unit: { } unit } && (unit.Equals ("s", StringComparison.OrdinalIgnoreCase) || unit.Equals ("ms", StringComparison.OrdinalIgnoreCase))
               || (ThemeCssCalc.IsMathFunction (component) && ThemeCssCalc.TryCompile (component, null, out var value, out _) && value.Unit == CalcUnit.Time);

        public static bool TryParseTime (CssComponent component, bool allowNegative, out TimeSpan time, out string? error)
        {
            time = TimeSpan.Zero;
            error = null;

            if (ThemeCssCalc.IsMathFunction (component)) {
                if (!ThemeCssCalc.TryCompile (component, null, out var computed, out error)) {
                    error = $"'{component.Raw}': {error}";
                    return false;
                }
                if (computed.Unit != CalcUnit.Time) {
                    error = $"'{component.Raw}' is {ThemeCssCalc.Describe (computed.Unit)}, not a time. {TimeSyntaxHelp}";
                    return false;
                }
                var ms = computed.Evaluate ();
                if (ms < 0 && !allowNegative) {
                    error = $"'{component.Raw}' is negative; a duration must be zero or positive (only a delay may be negative).";
                    return false;
                }
                time = TimeSpan.FromTicks ((long) Math.Round (ms * TimeSpan.TicksPerMillisecond));
                return true;
            }

            if (component is not CssNumber number) {
                error = $"'{component.Raw}' is not a time. {TimeSyntaxHelp}";
                return false;
            }

            if (number.Unit.Length == 0) {
                error = $"'{number.Raw}' has no unit; a time needs one, as in CSS: '{number.Raw}ms' or '{number.Raw}s'.";
                return false;
            }

            double milliseconds;

            if (number.Unit.Equals ("ms", StringComparison.OrdinalIgnoreCase))
                milliseconds = number.Value;
            else if (number.Unit.Equals ("s", StringComparison.OrdinalIgnoreCase))
                milliseconds = number.Value * 1000.0;
            else {
                error = $"'{number.Raw}' is not a time: '{number.Unit}' is not a time unit. {TimeSyntaxHelp}";
                return false;
            }

            if (milliseconds < 0 && !allowNegative) {
                error = $"'{number.Raw}' is negative; a duration must be zero or positive (only a delay may be negative).";
                return false;
            }

            time = TimeSpan.FromTicks ((long) Math.Round (milliseconds * TimeSpan.TicksPerMillisecond));
            return true;
        }

        private static readonly string[] timing_keywords = { "linear", "ease", "ease-in", "ease-out", "ease-in-out", "step-start", "step-end" };

        /// <summary>Whether <paramref name="component"/> spells a timing function (a keyword or one of the functions), valid or not.</summary>
        public static bool LooksLikeTiming (CssComponent component)
            => component switch {
                CssIdent ident => timing_keywords.Contains (ident.Name, StringComparer.OrdinalIgnoreCase),
                CssFunction fn => fn.Name.Equals ("cubic-bezier", StringComparison.OrdinalIgnoreCase)
                                  || fn.Name.Equals ("steps", StringComparison.OrdinalIgnoreCase)
                                  || fn.Name.Equals ("linear", StringComparison.OrdinalIgnoreCase),
                _ => false
            };

        public static bool TryParseTimingFunction (CssComponent component, out ControlTimingFunction timing, out string? error)
        {
            timing = ControlTimingFunction.Ease;
            error = null;

            switch (component) {
                case CssIdent ident:
                    switch (ident.Name.ToLowerInvariant ()) {
                        case "linear": timing = ControlTimingFunction.Linear; return true;
                        case "ease": timing = ControlTimingFunction.Ease; return true;
                        case "ease-in": timing = ControlTimingFunction.EaseIn; return true;
                        case "ease-out": timing = ControlTimingFunction.EaseOut; return true;
                        case "ease-in-out": timing = ControlTimingFunction.EaseInOut; return true;
                        case "step-start": timing = ControlTimingFunction.StepStart; return true;
                        case "step-end": timing = ControlTimingFunction.StepEnd; return true;
                    }
                    error = $"'{ident.Name}' is not a timing function.{Suggest (ident.Name, timing_keywords)} {TimingSyntaxHelp}";
                    return false;

                case CssFunction fn when fn.Name.Equals ("cubic-bezier", StringComparison.OrdinalIgnoreCase): {
                    var numbers = fn.Arguments.Select (g => g.Count == 1 && g[0] is CssNumber { Unit.Length: 0 } n ? n : null).ToList ();

                    if (numbers.Count != 4 || numbers.Any (n => n is null)) {
                        error = $"'{fn.Raw}' needs exactly four plain numbers, comma-separated: cubic-bezier(x1, y1, x2, y2), e.g. cubic-bezier(0.22, 1, 0.36, 1).";
                        return false;
                    }

                    var x1 = numbers[0]!.Value;
                    var x2 = numbers[2]!.Value;

                    if (x1 is < 0 or > 1 || x2 is < 0 or > 1) {
                        error = $"'{fn.Raw}': x1 and x2 (the first and third numbers) must be between 0 and 1, so the curve moves forward in time; only the y values may overshoot.";
                        return false;
                    }

                    timing = ControlTimingFunction.CubicBezier ((float) x1, (float) numbers[1]!.Value, (float) x2, (float) numbers[3]!.Value);
                    return true;
                }

                case CssFunction fn when fn.Name.Equals ("steps", StringComparison.OrdinalIgnoreCase): {
                    if (fn.Arguments.Count is < 1 or > 2 || fn.Arguments[0].Count != 1 || fn.Arguments[0][0] is not CssNumber { Unit.Length: 0 } count
                        || count.Value != Math.Floor (count.Value) || count.Value < 1) {
                        error = $"'{fn.Raw}' needs a whole number of steps of at least 1, then optionally a position: steps(4) or steps(4, jump-start).";
                        return false;
                    }

                    var position = StepPosition.JumpEnd;

                    if (fn.Arguments.Count == 2) {
                        var word = fn.Arguments[1].Count == 1 && fn.Arguments[1][0] is CssIdent p ? p.Name.ToLowerInvariant () : string.Empty;
                        switch (word) {
                            case "jump-start": case "start": position = StepPosition.JumpStart; break;
                            case "jump-end": case "end": position = StepPosition.JumpEnd; break;
                            case "jump-none": position = StepPosition.JumpNone; break;
                            case "jump-both": position = StepPosition.JumpBoth; break;
                            default:
                                error = $"'{fn.Raw}': the step position is jump-start, jump-end, jump-none, jump-both, start or end.";
                                return false;
                        }
                    }

                    if (position == StepPosition.JumpNone && count.Value < 2) {
                        error = $"'{fn.Raw}': steps(n, jump-none) needs at least 2 steps.";
                        return false;
                    }

                    timing = ControlTimingFunction.Steps ((int) count.Value, position);
                    return true;
                }

                case CssFunction fn when fn.Name.Equals ("linear", StringComparison.OrdinalIgnoreCase):
                    error = $"'{fn.Raw}': the linear() stop-list function is not supported. Use the 'linear' keyword, or cubic-bezier() for a curve.";
                    return false;

                default:
                    error = $"'{component.Raw}' is not a timing function. {TimingSyntaxHelp}";
                    return false;
            }
        }

        // ---- fonts -------------------------------------------------------------------------------

        /// <summary>
        /// Parses a font-family list: quoted strings and/or bare identifiers (multi-word unquoted names
        /// are joined with a space), separated by commas. A var() of a font token contributes that
        /// token's family at apply time.
        /// </summary>
        public static bool TryParseFontFamilies (List<CssComponent> components, out Func<IReadOnlyList<string>> families, out string? error)
        {
            families = null!;
            error = null;

            var parts = new List<Func<string>> ();
            var pending = new List<string> ();

            void Flush ()
            {
                if (pending.Count > 0) {
                    var name = string.Join (" ", pending);
                    parts.Add (() => name);
                    pending.Clear ();
                }
            }

            foreach (var component in components) {
                switch (component) {
                    case CssString s:
                        Flush ();
                        var value = s.Value;
                        parts.Add (() => value);
                        break;
                    case CssIdent ident:
                        pending.Add (ident.Name);
                        break;
                    case CssDelim { Value: ',' }:
                        Flush ();
                        break;
                    case CssTokenRef tokenRef when tokenRef.Token.Kind == ThemeCssValueKind.FontFamily:
                        Flush ();
                        var token = tokenRef.Token;
                        parts.Add (() => ((SKTypeface) Theme.GetTokenValue (token)).FamilyName);
                        break;
                    default:
                        error = $"'{component.Raw}' is not a valid font family. Write a quoted name like \"Segoe UI\", a bare name like Arial, or a list: \"Inter\", \"Segoe UI\", sans-serif.";
                        return false;
                }
            }

            Flush ();

            if (parts.Count == 0) {
                error = "A font family is required, e.g. font-family: \"Segoe UI\", sans-serif;";
                return false;
            }

            families = () => parts.Select (p => p ()).ToList ();
            return true;
        }

        public static bool TryParseFontWeight (List<CssComponent> components, out SKFontStyleWeight weight, out string? error)
        {
            weight = SKFontStyleWeight.Normal;
            error = null;

            if (components.Count == 1) {
                if (components[0] is CssIdent ident) {
                    switch (ident.Name.ToLowerInvariant ()) {
                        case "normal": weight = SKFontStyleWeight.Normal; return true;
                        case "bold": weight = SKFontStyleWeight.Bold; return true;
                        case "lighter": weight = SKFontStyleWeight.Light; return true;
                        case "bolder": weight = SKFontStyleWeight.ExtraBold; return true;
                    }
                } else if (components[0] is CssNumber { Unit: "" } number
                           && number.Value >= 100 && number.Value <= 900 && Math.Abs (number.Value % 100) < 0.001) {
                    weight = (SKFontStyleWeight) (int) number.Value;
                    return true;
                }
            }

            error = $"'{Join (components)}' is not a valid font-weight. Use normal, bold, lighter, bolder, or a multiple of 100 from 100 to 900.";
            return false;
        }

        public static bool TryParseFontStyle (List<CssComponent> components, out SKFontStyleSlant slant, out string? error)
        {
            slant = SKFontStyleSlant.Upright;
            error = null;

            if (components.Count == 1 && components[0] is CssIdent ident) {
                switch (ident.Name.ToLowerInvariant ()) {
                    case "normal": slant = SKFontStyleSlant.Upright; return true;
                    case "italic": slant = SKFontStyleSlant.Italic; return true;
                    case "oblique": slant = SKFontStyleSlant.Oblique; return true;
                }
            }

            error = $"'{Join (components)}' is not a valid font-style. Use normal, italic or oblique.";
            return false;
        }

        /// <summary>
        /// Resolves a family list to a typeface the way a browser does: the first family the system
        /// actually has wins; if none match, Skia's fallback for the first name is used so text still
        /// draws. A family registered through <c>PrivateFontCollection</c> counts as one the system has.
        /// Cached, because compiled rules re-resolve on every theme change.
        /// </summary>
        public static SKTypeface GetTypeface (IReadOnlyList<string> families, SKFontStyleWeight weight, SKFontStyleSlant slant)
        {
            // A privately registered family is unknown to the system font manager, which would answer with a default face for its
            // name, so ask the registry first, in list order. Deliberately ahead of, and excluded from, the cache below: the registry
            // owns those typefaces and disposes them with their collection, and a cached fallback would hide a family registered
            // after this list was first resolved. IsEmpty keeps the common no-private-fonts path to one uncontended lock.
            if (!Drawing.Text.PrivateFontRegistry.IsEmpty) {
                var style = new SKFontStyle (weight, SKFontStyleWidth.Normal, slant);

                foreach (var family in families) {
                    var privateFace = Drawing.Text.PrivateFontRegistry.Resolve (family, style);

                    if (privateFace is not null)
                        return privateFace;

                    // A family the system really has, listed before a private one, still wins, as it would in a browser.
                    if (SystemHasFamily (family, weight, slant))
                        break;
                }
            }

            var key = string.Join ("|", families) + "|" + (int) weight + "|" + (int) slant;

            return typefaces.GetOrAdd (key, _ => {
                SKTypeface? first = null;

                foreach (var family in families) {
                    var typeface = SKTypeface.FromFamilyName (family, weight, SKFontStyleWidth.Normal, slant);

                    if (typeface is null)
                        continue;

                    first ??= typeface;

                    if (string.Equals (typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                        return typeface;
                }

                return first ?? SKTypeface.FromFamilyName (families[0], weight, SKFontStyleWidth.Normal, slant) ?? SKTypeface.Default;
            });
        }

        private static bool SystemHasFamily (string family, SKFontStyleWeight weight, SKFontStyleSlant slant)
            => systemHas.GetOrAdd (family + "|" + (int) weight + "|" + (int) slant, _ => {
                using var typeface = SKTypeface.FromFamilyName (family, weight, SKFontStyleWidth.Normal, slant);
                return typeface is not null && string.Equals (typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase);
            });

        // ---- helpers -----------------------------------------------------------------------------

        private static Func<SKColor> Constant (SKColor color) => () => color;

        public static string Join (IEnumerable<CssComponent> components) => string.Join (" ", components.Select (c => c.Raw));

        public static string DescribeKind (ThemeCssValueKind kind) => kind switch {
            ThemeCssValueKind.Color => "color",
            ThemeCssValueKind.Length => "length",
            ThemeCssValueKind.FontWeight => "font-weight",
            ThemeCssValueKind.FontStyle => "font-style",
            ThemeCssValueKind.Time => "time",
            ThemeCssValueKind.Number => "number",
            ThemeCssValueKind.Image => "image",
            ThemeCssValueKind.TimingFunction => "timing function",
            ThemeCssValueKind.List => "list",
            ThemeCssValueKind.Keyword => "keyword",
            ThemeCssValueKind.BoxShadow => "box-shadow",
            _ => "font-family"
        };

        /// <summary>
        /// Returns " Did you mean 'x'?" for the closest candidate within a small edit distance, or an
        /// empty string. Typos are the most common failure in hand- or model-written CSS, and a name
        /// suggestion turns a "why does nothing happen" into a one-character fix.
        /// </summary>
        public static string Suggest (string name, IEnumerable<string> candidates)
        {
            var best = FindClosest (name, candidates);
            return best is null ? string.Empty : $" Did you mean '{best}'?";
        }

        public static string? FindClosest (string name, IEnumerable<string> candidates)
        {
            string? best = null;
            var bestDistance = int.MaxValue;
            // Up to half the characters may differ: 'heading' -> 'header' (3 edits of 7) is the kind of
            // near miss a suggestion exists for, and the candidate lists are short and distinct enough
            // that a looser bound does not produce misleading matches.
            var limit = Math.Max (2, (name.Length + 1) / 2);

            foreach (var candidate in candidates) {
                var lower = name.ToLowerInvariant ();
                var candidateLower = candidate.ToLowerInvariant ();

                // A truncated or over-long spelling ('background' for 'background-color') is as common
                // as a typo and scores badly on edit distance, so treat a prefix match as nearly exact.
                var prefix = Math.Min (lower.Length, candidateLower.Length) >= 4
                    && (candidateLower.StartsWith (lower, StringComparison.Ordinal) || lower.StartsWith (candidateLower, StringComparison.Ordinal));
                var distance = prefix ? 1 : Levenshtein (lower, candidateLower);

                if (distance < bestDistance && distance <= limit) {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static int Levenshtein (string a, string b)
        {
            var costs = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
                costs[j] = j;

            for (var i = 1; i <= a.Length; i++) {
                var previous = costs[0];
                costs[0] = i;

                for (var j = 1; j <= b.Length; j++) {
                    var current = costs[j];
                    costs[j] = Math.Min (Math.Min (costs[j] + 1, costs[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                    previous = current;
                }
            }

            return costs[b.Length];
        }

        // The CSS Color Module Level 4 named colors (148 including rebeccapurple; the gray/grey pairs
        // are both present). Kept as a table rather than reflected off SKColors so the set is exactly
        // the documented CSS one and trimming/AOT never has to preserve SKColors' fields.
        public static readonly IReadOnlyDictionary<string, SKColor> NamedColors = BuildNamedColors ();

        private static Dictionary<string, SKColor> BuildNamedColors ()
        {
            var table = new Dictionary<string, SKColor> (StringComparer.OrdinalIgnoreCase);

            void Add (string name, uint rgb) => table[name] = new SKColor ((byte) (rgb >> 16), (byte) (rgb >> 8), (byte) rgb, 255);

            Add ("aliceblue", 0xf0f8ff); Add ("antiquewhite", 0xfaebd7); Add ("aqua", 0x00ffff); Add ("aquamarine", 0x7fffd4);
            Add ("azure", 0xf0ffff); Add ("beige", 0xf5f5dc); Add ("bisque", 0xffe4c4); Add ("black", 0x000000);
            Add ("blanchedalmond", 0xffebcd); Add ("blue", 0x0000ff); Add ("blueviolet", 0x8a2be2); Add ("brown", 0xa52a2a);
            Add ("burlywood", 0xdeb887); Add ("cadetblue", 0x5f9ea0); Add ("chartreuse", 0x7fff00); Add ("chocolate", 0xd2691e);
            Add ("coral", 0xff7f50); Add ("cornflowerblue", 0x6495ed); Add ("cornsilk", 0xfff8dc); Add ("crimson", 0xdc143c);
            Add ("cyan", 0x00ffff); Add ("darkblue", 0x00008b); Add ("darkcyan", 0x008b8b); Add ("darkgoldenrod", 0xb8860b);
            Add ("darkgray", 0xa9a9a9); Add ("darkgreen", 0x006400); Add ("darkgrey", 0xa9a9a9); Add ("darkkhaki", 0xbdb76b);
            Add ("darkmagenta", 0x8b008b); Add ("darkolivegreen", 0x556b2f); Add ("darkorange", 0xff8c00); Add ("darkorchid", 0x9932cc);
            Add ("darkred", 0x8b0000); Add ("darksalmon", 0xe9967a); Add ("darkseagreen", 0x8fbc8f); Add ("darkslateblue", 0x483d8b);
            Add ("darkslategray", 0x2f4f4f); Add ("darkslategrey", 0x2f4f4f); Add ("darkturquoise", 0x00ced1); Add ("darkviolet", 0x9400d3);
            Add ("deeppink", 0xff1493); Add ("deepskyblue", 0x00bfff); Add ("dimgray", 0x696969); Add ("dimgrey", 0x696969);
            Add ("dodgerblue", 0x1e90ff); Add ("firebrick", 0xb22222); Add ("floralwhite", 0xfffaf0); Add ("forestgreen", 0x228b22);
            Add ("fuchsia", 0xff00ff); Add ("gainsboro", 0xdcdcdc); Add ("ghostwhite", 0xf8f8ff); Add ("gold", 0xffd700);
            Add ("goldenrod", 0xdaa520); Add ("gray", 0x808080); Add ("green", 0x008000); Add ("greenyellow", 0xadff2f);
            Add ("grey", 0x808080); Add ("honeydew", 0xf0fff0); Add ("hotpink", 0xff69b4); Add ("indianred", 0xcd5c5c);
            Add ("indigo", 0x4b0082); Add ("ivory", 0xfffff0); Add ("khaki", 0xf0e68c); Add ("lavender", 0xe6e6fa);
            Add ("lavenderblush", 0xfff0f5); Add ("lawngreen", 0x7cfc00); Add ("lemonchiffon", 0xfffacd); Add ("lightblue", 0xadd8e6);
            Add ("lightcoral", 0xf08080); Add ("lightcyan", 0xe0ffff); Add ("lightgoldenrodyellow", 0xfafad2); Add ("lightgray", 0xd3d3d3);
            Add ("lightgreen", 0x90ee90); Add ("lightgrey", 0xd3d3d3); Add ("lightpink", 0xffb6c1); Add ("lightsalmon", 0xffa07a);
            Add ("lightseagreen", 0x20b2aa); Add ("lightskyblue", 0x87cefa); Add ("lightslategray", 0x778899); Add ("lightslategrey", 0x778899);
            Add ("lightsteelblue", 0xb0c4de); Add ("lightyellow", 0xffffe0); Add ("lime", 0x00ff00); Add ("limegreen", 0x32cd32);
            Add ("linen", 0xfaf0e6); Add ("magenta", 0xff00ff); Add ("maroon", 0x800000); Add ("mediumaquamarine", 0x66cdaa);
            Add ("mediumblue", 0x0000cd); Add ("mediumorchid", 0xba55d3); Add ("mediumpurple", 0x9370db); Add ("mediumseagreen", 0x3cb371);
            Add ("mediumslateblue", 0x7b68ee); Add ("mediumspringgreen", 0x00fa9a); Add ("mediumturquoise", 0x48d1cc); Add ("mediumvioletred", 0xc71585);
            Add ("midnightblue", 0x191970); Add ("mintcream", 0xf5fffa); Add ("mistyrose", 0xffe4e1); Add ("moccasin", 0xffe4b5);
            Add ("navajowhite", 0xffdead); Add ("navy", 0x000080); Add ("oldlace", 0xfdf5e6); Add ("olive", 0x808000);
            Add ("olivedrab", 0x6b8e23); Add ("orange", 0xffa500); Add ("orangered", 0xff4500); Add ("orchid", 0xda70d6);
            Add ("palegoldenrod", 0xeee8aa); Add ("palegreen", 0x98fb98); Add ("paleturquoise", 0xafeeee); Add ("palevioletred", 0xdb7093);
            Add ("papayawhip", 0xffefd5); Add ("peachpuff", 0xffdab9); Add ("peru", 0xcd853f); Add ("pink", 0xffc0cb);
            Add ("plum", 0xdda0dd); Add ("powderblue", 0xb0e0e6); Add ("purple", 0x800080); Add ("rebeccapurple", 0x663399);
            Add ("red", 0xff0000); Add ("rosybrown", 0xbc8f8f); Add ("royalblue", 0x4169e1); Add ("saddlebrown", 0x8b4513);
            Add ("salmon", 0xfa8072); Add ("sandybrown", 0xf4a460); Add ("seagreen", 0x2e8b57); Add ("seashell", 0xfff5ee);
            Add ("sienna", 0xa0522d); Add ("silver", 0xc0c0c0); Add ("skyblue", 0x87ceeb); Add ("slateblue", 0x6a5acd);
            Add ("slategray", 0x708090); Add ("slategrey", 0x708090); Add ("snow", 0xfffafa); Add ("springgreen", 0x00ff7f);
            Add ("steelblue", 0x4682b4); Add ("tan", 0xd2b48c); Add ("teal", 0x008080); Add ("thistle", 0xd8bfd8);
            Add ("tomato", 0xff6347); Add ("turquoise", 0x40e0d0); Add ("violet", 0xee82ee); Add ("wheat", 0xf5deb3);
            Add ("white", 0xffffff); Add ("whitesmoke", 0xf5f5f5); Add ("yellow", 0xffff00); Add ("yellowgreen", 0x9acd32);

            return table;
        }
    }
}
