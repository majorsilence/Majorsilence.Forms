using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    // What a numeric CSS value measures, after its unit is converted to this subset's canonical one: lengths in pixels, times in
    // milliseconds, angles in degrees.
    internal enum CalcUnit
    {
        Number,
        Percent,
        Length,
        Time,
        Angle
    }

    // A compiled numeric expression: its unit, known when it is parsed, and a thunk for its value, so a var(--token) or a rem inside it
    // tracks the theme. IsConstant marks one that reads nothing live, so the parser can check it (a negative width, a division by zero)
    // before anything is applied.
    internal readonly struct CalcValue
    {
        public CalcValue (CalcUnit unit, Func<double> evaluate, bool isConstant)
        {
            Unit = unit;
            Evaluate = evaluate;
            IsConstant = isConstant;
        }

        public CalcUnit Unit { get; }
        public Func<double> Evaluate { get; }
        public bool IsConstant { get; }

        public static CalcValue Constant (CalcUnit unit, double value) => new (unit, () => value, true);
    }

    // calc(), min(), max(), clamp() and parentheses over the units a theme uses, plus the named channels of a relative colour
    // (oklch(from var(--x) calc(l * .9) c h)). Every unit conversion lives here, so 'rem' means the same thing in a border width, a
    // calc() and a font size.
    internal static class ThemeCssCalc
    {
        public static bool IsMathFunction (CssComponent component)
            => component is CssFunction fn && IsMathFunctionName (fn.Name);

        private static bool IsMathFunctionName (string name)
            => name.Length == 0 || name.Equals ("calc", StringComparison.OrdinalIgnoreCase) || name.Equals ("min", StringComparison.OrdinalIgnoreCase)
               || name.Equals ("max", StringComparison.OrdinalIgnoreCase) || name.Equals ("clamp", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Compiles one component -- a number with a unit, a length token, a channel name, or a math function -- to a value.
        /// <paramref name="channels"/> names the channels of a relative colour's origin, which are numbers.
        /// </summary>
        public static bool TryCompile (CssComponent component, IReadOnlyDictionary<string, Func<double>>? channels, out CalcValue value, out string? error)
        {
            value = default;
            error = null;

            switch (component) {
                case CssNumber number:
                    return TryCompileNumber (number, out value, out error);

                case CssTokenRef tokenRef when tokenRef.Token.Kind == ThemeCssValueKind.Length:
                    var token = tokenRef.Token;
                    value = new CalcValue (CalcUnit.Length, () => (int) Theme.GetTokenValue (token), false);
                    return true;

                case CssTokenRef tokenRef:
                    error = $"'{tokenRef.Token.Name}' is a {ThemeCssValues.DescribeKind (tokenRef.Token.Kind)} token; only length tokens can be used in arithmetic.";
                    return false;

                case CssIdent ident when channels is not null && channels.TryGetValue (ident.Name, out var channel):
                    value = new CalcValue (CalcUnit.Number, channel, false);
                    return true;

                case CssFunction fn when IsMathFunctionName (fn.Name):
                    return TryCompileFunction (fn, channels, out value, out error);

                default:
                    error = channels is null
                        ? $"'{component.Raw}' is not a number, length, time or angle."
                        : $"'{component.Raw}' is not a number or one of the origin colour's channels ({string.Join (", ", channels.Keys)}).";
                    return false;
            }
        }

        private static bool TryCompileNumber (CssNumber number, out CalcValue value, out string? error)
        {
            error = null;
            var n = number.Value;
            var unit = number.Unit.ToLowerInvariant ();

            switch (unit) {
                case "":
                    value = CalcValue.Constant (CalcUnit.Number, n);
                    return true;
                case "%":
                    value = CalcValue.Constant (CalcUnit.Percent, n);
                    return true;
                case "px":
                    value = CalcValue.Constant (CalcUnit.Length, n);
                    return true;
                case "rem":
                    // Relative to --root-font-size (16px unless a theme changes it, as a browser's root font size is), read live.
                    value = new CalcValue (CalcUnit.Length, () => n * Theme.RootFontSize, false);
                    return true;
                case "ms":
                    value = CalcValue.Constant (CalcUnit.Time, n);
                    return true;
                case "s":
                    value = CalcValue.Constant (CalcUnit.Time, n * 1000.0);
                    return true;
                case "deg":
                    value = CalcValue.Constant (CalcUnit.Angle, n);
                    return true;
                case "rad":
                    value = CalcValue.Constant (CalcUnit.Angle, n * 180.0 / Math.PI);
                    return true;
                case "grad":
                    value = CalcValue.Constant (CalcUnit.Angle, n * 0.9);
                    return true;
                case "turn":
                    value = CalcValue.Constant (CalcUnit.Angle, n * 360.0);
                    return true;
                case "em":
                    value = default;
                    error = $"'{number.Raw}': 'em' is relative to the inherited font size, which a type rule cannot know. Use 'rem' (relative to --root-font-size) or 'px'.";
                    return false;
                case "pt":
                    value = default;
                    error = $"'{number.Raw}': points are not supported; every size in a theme is a pixel value. Write '{Math.Round (n * 96 / 72)}px' (1pt is 4/3 px).";
                    return false;
                default:
                    value = default;
                    error = $"'{number.Raw}': '{number.Unit}' is not a supported unit. Lengths are px or rem, times s or ms, angles deg, rad, grad or turn.";
                    return false;
            }
        }

        private static bool TryCompileFunction (CssFunction fn, IReadOnlyDictionary<string, Func<double>>? channels, out CalcValue value, out string? error)
        {
            value = default;
            var name = fn.Name.Length == 0 ? "(" : fn.Name.ToLowerInvariant ();

            if (name is "calc" or "(") {
                if (fn.Arguments.Count != 1) {
                    error = $"'{fn.Raw}': calc() takes one expression; commas are for min(), max() and clamp().";
                    return false;
                }

                return TryCompileExpression (fn.Arguments[0], channels, out value, out error);
            }

            var arguments = new List<CalcValue> ();

            foreach (var group in fn.Arguments) {
                if (!TryCompileExpression (group, channels, out var argument, out error))
                    return false;
                arguments.Add (argument);
            }

            if (name == "clamp" && arguments.Count != 3) {
                error = $"'{fn.Raw}': clamp() takes three values: clamp(minimum, preferred, maximum).";
                return false;
            }

            if (arguments.Count == 0) {
                error = $"'{fn.Raw}' needs at least one value.";
                return false;
            }

            var unit = arguments[0].Unit;

            if (arguments.Any (a => a.Unit != unit)) {
                error = $"'{fn.Raw}': every value must have the same kind of unit ({Describe (unit)} and {Describe (arguments.First (a => a.Unit != unit).Unit)} cannot be compared).";
                return false;
            }

            var thunks = arguments.Select (a => a.Evaluate).ToArray ();
            var constant = arguments.All (a => a.IsConstant);

            Func<double> evaluate = name switch {
                "min" => () => thunks.Min (t => t ()),
                "max" => () => thunks.Max (t => t ()),
                _ => () => Math.Max (thunks[0] (), Math.Min (thunks[1] (), thunks[2] ())),
            };

            value = new CalcValue (unit, evaluate, constant);
            error = null;
            return true;
        }

        /// <summary>Compiles the inside of a calc(): sums of products of values, with parentheses.</summary>
        public static bool TryCompileExpression (List<CssComponent> components, IReadOnlyDictionary<string, Func<double>>? channels, out CalcValue value, out string? error)
        {
            var position = 0;
            value = default;

            if (components.Count == 0) {
                error = "an empty expression.";
                return false;
            }

            if (!TryParseSum (components, ref position, channels, out value, out error))
                return false;

            if (position < components.Count) {
                var extra = components[position];
                error = extra is CssNumber { Value: < 0 } || (extra.Raw.Length > 0 && extra.Raw[0] == '+')
                    ? $"'{Join (components)}': put spaces around '+' and '-' -- CSS reads '{extra.Raw}' as a signed number, not an operator."
                    : $"'{Join (components)}': expected an operator (+, -, * or /) before '{extra.Raw}'.";
                return false;
            }

            return true;
        }

        private static bool TryParseSum (List<CssComponent> components, ref int position, IReadOnlyDictionary<string, Func<double>>? channels, out CalcValue value, out string? error)
        {
            if (!TryParseProduct (components, ref position, channels, out value, out error))
                return false;

            while (position < components.Count && components[position] is CssDelim { Value: '+' or '-' } op) {
                position++;

                if (!TryParseProduct (components, ref position, channels, out var right, out error))
                    return false;

                if (right.Unit != value.Unit) {
                    error = $"'{Join (components)}': cannot {(op.Value == '+' ? "add" : "subtract")} {Describe (right.Unit)} {(op.Value == '+' ? "to" : "from")} {Describe (value.Unit)}.";
                    return false;
                }

                var l = value.Evaluate;
                var r = right.Evaluate;
                value = new CalcValue (value.Unit, op.Value == '+' ? () => l () + r () : () => l () - r (), value.IsConstant && right.IsConstant);
            }

            return true;
        }

        private static bool TryParseProduct (List<CssComponent> components, ref int position, IReadOnlyDictionary<string, Func<double>>? channels, out CalcValue value, out string? error)
        {
            if (!TryParseValue (components, ref position, channels, out value, out error))
                return false;

            while (position < components.Count && components[position] is CssDelim { Value: '*' or '/' } op) {
                position++;

                if (!TryParseValue (components, ref position, channels, out var right, out error))
                    return false;

                var l = value.Evaluate;
                var r = right.Evaluate;
                var constant = value.IsConstant && right.IsConstant;

                if (op.Value == '*') {
                    if (value.Unit != CalcUnit.Number && right.Unit != CalcUnit.Number) {
                        error = $"'{Join (components)}': cannot multiply {Describe (value.Unit)} by {Describe (right.Unit)}; one side of '*' must be a plain number.";
                        return false;
                    }

                    value = new CalcValue (value.Unit == CalcUnit.Number ? right.Unit : value.Unit, () => l () * r (), constant);
                    continue;
                }

                if (right.Unit != CalcUnit.Number) {
                    error = $"'{Join (components)}': the right side of '/' must be a plain number, not {Describe (right.Unit)}.";
                    return false;
                }

                if (right.IsConstant && right.Evaluate () == 0) {
                    error = $"'{Join (components)}': division by zero.";
                    return false;
                }

                value = new CalcValue (value.Unit, () => r () == 0 ? 0 : l () / r (), constant);
            }

            return true;
        }

        private static bool TryParseValue (List<CssComponent> components, ref int position, IReadOnlyDictionary<string, Func<double>>? channels, out CalcValue value, out string? error)
        {
            if (position >= components.Count) {
                value = default;
                error = $"'{Join (components)}' ends with an operator.";
                return false;
            }

            var component = components[position++];

            if (component is CssDelim delim) {
                value = default;
                error = $"'{Join (components)}': unexpected '{delim.Value}'.";
                return false;
            }

            // CSS's constants, inside a calc() only.
            if (component is CssIdent { Name: var constant } && (channels is null || !channels.ContainsKey (constant))) {
                if (constant.Equals ("pi", StringComparison.OrdinalIgnoreCase)) {
                    value = CalcValue.Constant (CalcUnit.Number, Math.PI);
                    error = null;
                    return true;
                }
                if (constant.Equals ("e", StringComparison.OrdinalIgnoreCase)) {
                    value = CalcValue.Constant (CalcUnit.Number, Math.E);
                    error = null;
                    return true;
                }
            }

            return TryCompile (component, channels, out value, out error);
        }

        public static string Describe (CalcUnit unit) => unit switch {
            CalcUnit.Number => "a number",
            CalcUnit.Percent => "a percentage",
            CalcUnit.Length => "a length",
            CalcUnit.Time => "a time",
            _ => "an angle"
        };

        private static string Join (List<CssComponent> components) => string.Join (" ", components.Select (c => c.Raw));
    }
}
