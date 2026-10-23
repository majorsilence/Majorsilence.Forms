using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A CSS timing function in host-neutral terms: the spelling plus, for the curves a toolkit's animation system
    /// can usually take natively, the numbers. <c>ease</c> and friends are cubic Béziers in CSS; <c>steps()</c> is a
    /// step function.
    /// </summary>
    public sealed class ThemeCssTiming
    {
        /// <summary>A timing function from its CSS spelling (<c>ease-in-out</c>, <c>cubic-bezier(...)</c>, <c>steps(...)</c>), for a host that evaluates curves itself.</summary>
        /// <param name="css">The CSS spelling. One that is not a timing function behaves as <c>linear</c>.</param>
        public static ThemeCssTiming FromCss (string css) => new (css);

        internal ThemeCssTiming (string css)
        {
            Css = css;

            if (TryKeyword (css, out var bezier)) {
                CubicBezier = bezier;
                return;
            }

            // step-start and step-end are one-step functions.
            switch (css.Trim ().ToLowerInvariant ()) {
                case "step-start": Steps = 1; StepPosition = "jump-start"; return;
                case "step-end": Steps = 1; StepPosition = "jump-end"; return;
            }

            var open = css.IndexOf ('(');
            if (open < 0 || css[css.Length - 1] != ')')
                return;

            var name = css.Substring (0, open).Trim ().ToLowerInvariant ();
            var args = css.Substring (open + 1, css.Length - open - 2).Split (',').Select (a => a.Trim ()).ToArray ();

            if (name == "cubic-bezier" && args.Length == 4 && args.All (a => double.TryParse (a, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                CubicBezier = args.Select (a => double.Parse (a, CultureInfo.InvariantCulture)).ToArray ();
            else if (name == "steps" && args.Length is 1 or 2 && int.TryParse (args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var steps)) {
                Steps = steps;
                StepPosition = args.Length == 2 ? args[1].ToLowerInvariant () switch { "jump-start" or "start" => "jump-start", "jump-none" => "jump-none", "jump-both" => "jump-both", _ => "jump-end" } : "jump-end";
            }
        }

        private static bool TryKeyword (string css, out double[] bezier)
        {
            // CSS Easing 1: the keyword curves are cubic Béziers.
            switch (css.Trim ().ToLowerInvariant ()) {
                case "linear": bezier = new[] { 0.0, 0.0, 1.0, 1.0 }; return true;
                case "ease": bezier = new[] { 0.25, 0.1, 0.25, 1.0 }; return true;
                case "ease-in": bezier = new[] { 0.42, 0.0, 1.0, 1.0 }; return true;
                case "ease-out": bezier = new[] { 0.0, 0.0, 0.58, 1.0 }; return true;
                case "ease-in-out": bezier = new[] { 0.42, 0.0, 0.58, 1.0 }; return true;
                default: bezier = System.Array.Empty<double> (); return false;
            }
        }

        /// <summary>The timing function as the stylesheet spelled it, e.g. <c>ease-in-out</c> or <c>steps(4, jump-start)</c>.</summary>
        public string Css { get; }

        /// <summary>
        /// The four numbers <c>x1, y1, x2, y2</c> of the cubic Bézier this is (the keywords included), or an empty list when it is a
        /// step function.
        /// </summary>
        public IReadOnlyList<double> CubicBezier { get; } = System.Array.Empty<double> ();

        /// <summary>The number of steps of a <c>steps()</c> / <c>step-start</c> / <c>step-end</c> function, or 0 when it is a curve.</summary>
        public int Steps { get; }

        /// <summary><c>jump-start</c>, <c>jump-end</c>, <c>jump-none</c> or <c>jump-both</c> for a step function; empty for a curve.</summary>
        public string StepPosition { get; } = string.Empty;

        /// <summary>
        /// The eased progress for a linear progress from 0 to 1, by CSS Easing: the cubic Bézier's y at the x that equals
        /// <paramref name="progress"/>, or the step function's value. For a host that animates with its own timer.
        /// </summary>
        /// <param name="progress">Linear progress, clamped to 0..1.</param>
        public double Evaluate (double progress)
        {
            progress = Math.Min (1, Math.Max (0, progress));

            if (CubicBezier.Count == 4)
                return EvaluateBezier (CubicBezier[0], CubicBezier[1], CubicBezier[2], CubicBezier[3], progress);

            if (Steps > 0) {
                var jumps = StepPosition switch { "jump-none" => Steps - 1, "jump-both" => Steps + 1, _ => Steps };
                if (jumps <= 0)
                    return progress >= 1 ? 1 : 0;

                var step = Math.Floor (progress * Steps);
                if (StepPosition is "jump-start" or "jump-both")
                    step++;

                // At exactly 1 the function has finished its last jump whatever the position.
                if (progress >= 1)
                    step = jumps;

                return Math.Min (1, Math.Max (0, step / jumps));
            }

            return progress;
        }

        private static double EvaluateBezier (double x1, double y1, double x2, double y2, double x)
        {
            if (x <= 0)
                return 0;
            if (x >= 1)
                return 1;

            static double Axis (double a, double b, double t) => 3 * a * t * (1 - t) * (1 - t) + 3 * b * t * t * (1 - t) + t * t * t;

            // Solve x(t) = x by bisection: the curve's x is monotonic for the allowed control points (x1 and x2 in 0..1).
            double lo = 0, hi = 1, mid = x;
            for (var i = 0; i < 40; i++) {
                mid = (lo + hi) / 2;
                var current = Axis (x1, x2, mid);
                if (Math.Abs (current - x) < 1e-7)
                    break;
                if (current < x)
                    lo = mid;
                else
                    hi = mid;
            }

            return Axis (y1, y2, mid);
        }

        /// <inheritdoc/>
        public override string ToString () => Css;
    }

    /// <summary>One entry of a rule's <c>transition</c>: a property (or <c>all</c>) and how it moves.</summary>
    public sealed class ThemeCssTransition
    {
        internal ThemeCssTransition (string property, double durationMs, double delayMs, ThemeCssTiming timing)
        {
            Property = property;
            DurationMs = durationMs;
            DelayMs = delayMs;
            Timing = timing;
        }

        /// <summary>The animated property, <c>all</c> for every animatable one.</summary>
        public string Property { get; }

        /// <summary>How long it takes, in milliseconds.</summary>
        public double DurationMs { get; }

        /// <summary>How long it waits before it starts, in milliseconds; negative starts it part-way through.</summary>
        public double DelayMs { get; }

        /// <summary>The easing.</summary>
        public ThemeCssTiming Timing { get; }

        /// <summary>
        /// The transitions a set of declarations describe, in order, the lists lined up by index as in CSS. Declarations are read in
        /// the order given and a later longhand replaces an earlier one, so passing a control's resting rule and then a state rule's
        /// declarations gives CSS's result: the state inherits what it leaves unsaid (<c>Button:active { transition-duration: 60ms; }</c>
        /// keeps the properties and easing of the <c>Button</c> rule). Empty for <c>transition: none</c> or when nothing is set.
        /// </summary>
        /// <param name="declarations">The declarations, base first.</param>
        public static IReadOnlyList<ThemeCssTransition> Resolve (IEnumerable<ThemeCssDeclaration> declarations)
        {
            ThemeCssValue? properties = null, durations = null, delays = null, timings = null;

            foreach (var declaration in declarations) {
                switch (declaration.Property) {
                    case "transition-property": properties = declaration.Value; break;
                    case "transition-duration": durations = declaration.Value; break;
                    case "transition-delay": delays = declaration.Value; break;
                    case "transition-timing-function": timings = declaration.Value; break;
                }
            }

            var names = Items (properties);
            if (names.Count == 0)
                return System.Array.Empty<ThemeCssTransition> ();

            var duration = Items (durations);
            var delay = Items (delays);
            var timing = Items (timings);

            return names.Select ((p, i) => new ThemeCssTransition (p.ToString (),
                At (duration, i)?.Milliseconds ?? 0,
                At (delay, i)?.Milliseconds ?? 0,
                At (timing, i) is { } t ? new ThemeCssTiming (t.TimingFunction) : ease_default)).ToArray ();
        }

        private static readonly ThemeCssTiming ease_default = new ("ease");

        private static IReadOnlyList<ThemeCssValue> Items (ThemeCssValue? value)
            => value is null ? System.Array.Empty<ThemeCssValue> () : value.Kind == ThemeCssValueKind.List ? value.Items : new[] { value };

        private static ThemeCssValue? At (IReadOnlyList<ThemeCssValue> list, int index) => list.Count == 0 ? null : list[index % list.Count];

        /// <inheritdoc/>
        public override string ToString () => $"{Property} {DurationMs.ToString ("0.###", CultureInfo.InvariantCulture)}ms {Timing}";
    }

    /// <summary>One entry of a rule's <c>animation</c>: the keyframes it plays and how.</summary>
    public sealed class ThemeCssAnimation
    {
        internal ThemeCssAnimation (string name, double durationMs, double delayMs, ThemeCssTiming timing, double iterations, string direction, string fillMode, bool paused)
        {
            Name = name;
            DurationMs = durationMs;
            DelayMs = delayMs;
            Timing = timing;
            IterationCount = iterations;
            Direction = direction;
            FillMode = fillMode;
            Paused = paused;
        }

        /// <summary>The <c>@keyframes</c> name (see <see cref="ThemeStyleSheet.Keyframes"/>).</summary>
        public string Name { get; }

        /// <summary>How long one iteration takes, in milliseconds.</summary>
        public double DurationMs { get; }

        /// <summary>How long it waits before starting, in milliseconds; negative starts part-way through.</summary>
        public double DelayMs { get; }

        /// <summary>The easing between keyframes (a keyframe can set its own).</summary>
        public ThemeCssTiming Timing { get; }

        /// <summary>How many times it runs; <see cref="double.PositiveInfinity"/> for <c>infinite</c>.</summary>
        public double IterationCount { get; }

        /// <summary><c>normal</c>, <c>reverse</c>, <c>alternate</c> or <c>alternate-reverse</c>.</summary>
        public string Direction { get; }

        /// <summary><c>none</c>, <c>forwards</c>, <c>backwards</c> or <c>both</c>.</summary>
        public string FillMode { get; }

        /// <summary>Whether <c>animation-play-state</c> is <c>paused</c>.</summary>
        public bool Paused { get; }

        /// <summary>
        /// The animations a set of declarations describe, lined up by index. Declarations are read in the order given and a later
        /// longhand replaces an earlier one (a control's resting rule first, then a state's), as for
        /// <see cref="ThemeCssTransition.Resolve"/>. Empty for <c>animation: none</c> or when nothing is set.
        /// </summary>
        /// <param name="declarations">The declarations, base first.</param>
        public static IReadOnlyList<ThemeCssAnimation> Resolve (IEnumerable<ThemeCssDeclaration> declarations)
        {
            var longhands = new Dictionary<string, ThemeCssValue> ();
            foreach (var declaration in declarations)
                if (declaration.Property.StartsWith ("animation-", StringComparison.Ordinal))
                    longhands[declaration.Property] = declaration.Value;

            static IReadOnlyList<ThemeCssValue> List (Dictionary<string, ThemeCssValue> d, string key)
                => !d.TryGetValue (key, out var v) ? System.Array.Empty<ThemeCssValue> () : v.Kind == ThemeCssValueKind.List ? v.Items : new[] { v };

            static ThemeCssValue? Pick (IReadOnlyList<ThemeCssValue> list, int index) => list.Count == 0 ? null : list[index % list.Count];

            var names = List (longhands, "animation-name");
            if (names.Count == 0)
                return System.Array.Empty<ThemeCssAnimation> ();

            var durations = List (longhands, "animation-duration");
            var delays = List (longhands, "animation-delay");
            var timings = List (longhands, "animation-timing-function");
            var counts = List (longhands, "animation-iteration-count");
            var directions = List (longhands, "animation-direction");
            var fills = List (longhands, "animation-fill-mode");
            var states = List (longhands, "animation-play-state");

            return names.Select ((n, i) => {
                var count = Pick (counts, i);
                return new ThemeCssAnimation (n.ToString (),
                    Pick (durations, i)?.Milliseconds ?? 0,
                    Pick (delays, i)?.Milliseconds ?? 0,
                    Pick (timings, i) is { } t ? new ThemeCssTiming (t.TimingFunction) : new ThemeCssTiming ("ease"),
                    count is null ? 1 : count.Kind == ThemeCssValueKind.Number ? count.Number : double.PositiveInfinity,
                    Pick (directions, i)?.ToString () ?? "normal",
                    Pick (fills, i)?.ToString () ?? "none",
                    Pick (states, i)?.ToString () == "paused");
            }).ToArray ();
        }

        /// <inheritdoc/>
        public override string ToString () => $"{Name} {DurationMs.ToString ("0.###", CultureInfo.InvariantCulture)}ms {Timing}";
    }

    public sealed partial class ThemeCssRule
    {
        private IReadOnlyList<ThemeCssTransition>? transitions;
        private IReadOnlyList<ThemeCssAnimation>? animations;

        /// <summary>
        /// The transitions the rule's own <c>transition-*</c> declarations describe, the lists lined up by index as in CSS. Empty when the
        /// rule declares none (it may still inherit one from the base rule of its control; CSS applies the transition of the style being
        /// entered, falling back to the base rule's). <c>transition: none</c> gives an empty list too.
        /// </summary>
        public IReadOnlyList<ThemeCssTransition> Transitions => transitions ??= BuildTransitions ();

        /// <summary>The animations the rule's own <c>animation-*</c> declarations describe, lined up by index. Empty for none.</summary>
        public IReadOnlyList<ThemeCssAnimation> Animations => animations ??= BuildAnimations ();

        private ThemeCssTransition[] BuildTransitions () => ThemeCssTransition.Resolve (Declarations).ToArray ();

        private ThemeCssAnimation[] BuildAnimations () => ThemeCssAnimation.Resolve (Declarations).ToArray ();
    }
}
