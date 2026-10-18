using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Majorsilence.Forms.Animation;

namespace Majorsilence.Forms
{
    /// <summary>
    /// How a style animates its values when a control enters it -- the CSS <c>transition</c> property. Each of the four lists is one CSS
    /// longhand (<c>transition-property</c>, <c>-duration</c>, <c>-timing-function</c>, <c>-delay</c>); a list left null is inherited from
    /// the parent style, so <c>Button:active { transition-duration: 60ms; }</c> keeps the properties and easing a <c>Button</c> rule set.
    /// </summary>
    /// <remarks>
    /// As in CSS, the transition that runs is the one on the style being entered, and the lists line up by index: the n-th property uses
    /// the n-th duration, timing function and delay, a shorter list repeating from its start. Only paint-only values animate (see
    /// <see cref="AnimatableProperties"/>): a value that changes layout, such as a border width, would move child controls every frame.
    /// The user's reduced-motion setting (<see cref="SystemInformation.PrefersReducedMotion"/>) turns every transition into an instant change.
    /// </remarks>
    public sealed class ControlTransition
    {
        /// <summary>Creates a transition; each list left null is inherited from the parent style.</summary>
        /// <param name="properties">The animated CSS property names (<c>all</c>, or names from <see cref="AnimatableProperties"/>), or an empty list for <c>none</c>.</param>
        /// <param name="durations">How long each takes.</param>
        /// <param name="timingFunctions">The easing of each.</param>
        /// <param name="delays">How long each waits before it starts; negative starts part-way through.</param>
        public ControlTransition (IReadOnlyList<string>? properties = null, IReadOnlyList<TimeSpan>? durations = null,
            IReadOnlyList<ControlTimingFunction>? timingFunctions = null, IReadOnlyList<TimeSpan>? delays = null)
        {
            if (properties is not null)
                foreach (var property in properties)
                    if (!IsAnimatable (property))
                        throw new ArgumentException ($"'{property}' is not an animatable property. Animatable: all, {string.Join (", ", AnimatableProperties)}.", nameof (properties));

            if (durations is not null && durations.Any (d => d < TimeSpan.Zero))
                throw new ArgumentException ("A transition duration cannot be negative.", nameof (durations));

            Properties = properties;
            Durations = durations is { Count: 0 } ? null : durations;
            TimingFunctions = timingFunctions is { Count: 0 } ? null : timingFunctions;
            Delays = delays is { Count: 0 } ? null : delays;
        }

        /// <summary>The CSS property names that animate, <c>all</c> for every animatable one, empty for <c>none</c>; null inherits.</summary>
        public IReadOnlyList<string>? Properties { get; }

        /// <summary>The durations, by index with <see cref="Properties"/>; null inherits.</summary>
        public IReadOnlyList<TimeSpan>? Durations { get; }

        /// <summary>The timing functions, by index with <see cref="Properties"/>; null inherits.</summary>
        public IReadOnlyList<ControlTimingFunction>? TimingFunctions { get; }

        /// <summary>The delays, by index with <see cref="Properties"/>; null inherits.</summary>
        public IReadOnlyList<TimeSpan>? Delays { get; }

        /// <summary>
        /// The CSS properties a transition can animate. They only change how a control is painted; a property that moves layout (a border
        /// width, a font size) or has no in-between values (a font family, a border line style) is not one.
        /// </summary>
        public static IReadOnlyList<string> AnimatableProperties { get; } = new[] {
            "background-color", "color",
            "border-color", "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
            "border-radius", "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
            "box-shadow",
        };

        /// <summary>Whether <paramref name="property"/> is <c>all</c> or one of <see cref="AnimatableProperties"/> (case-insensitive).</summary>
        public static bool IsAnimatable (string property)
            => string.Equals (property, "all", StringComparison.OrdinalIgnoreCase)
               || AnimatableProperties.Contains (property, StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns a copy with the given lists replaced; a null argument keeps this transition's list.</summary>
        public ControlTransition With (IReadOnlyList<string>? properties = null, IReadOnlyList<TimeSpan>? durations = null,
            IReadOnlyList<ControlTimingFunction>? timingFunctions = null, IReadOnlyList<TimeSpan>? delays = null)
            => new ControlTransition (properties ?? Properties, durations ?? Durations, timingFunctions ?? TimingFunctions, delays ?? Delays);

        // Fills each list this transition leaves null from `fallback` (the parent's resolved transition).
        internal ControlTransition Inherit (ControlTransition? fallback)
            => fallback is null || (Properties is not null && Durations is not null && TimingFunctions is not null && Delays is not null)
                ? this
                : new ControlTransition (Properties ?? fallback.Properties, Durations ?? fallback.Durations,
                    TimingFunctions ?? fallback.TimingFunctions, Delays ?? fallback.Delays);

        /// <summary>
        /// Finds the timing of <paramref name="property"/> (an animatable property name, not <c>all</c>): the last entry that names it or
        /// <c>all</c>, with its duration, timing function and delay picked by index. Null when no entry covers it.
        /// </summary>
        /// <param name="property">The property to look up.</param>
        /// <param name="duration">Its duration (CSS's default 0s when no durations are set).</param>
        /// <param name="delay">Its delay (0s by default).</param>
        /// <param name="timing">Its timing function (<c>ease</c> by default).</param>
        /// <returns>Whether the property transitions.</returns>
        public bool TryGetTiming (string property, out TimeSpan duration, out TimeSpan delay, out ControlTimingFunction timing)
        {
            duration = TimeSpan.Zero;
            delay = TimeSpan.Zero;
            timing = ControlTimingFunction.Ease;

            // An unset transition-property is CSS's initial value, all.
            var properties = Properties ?? all_list;
            var index = -1;

            // A later entry for the same property wins, as in CSS.
            for (var i = 0; i < properties.Count; i++)
                if (string.Equals (properties[i], "all", StringComparison.OrdinalIgnoreCase) || Covers (properties[i], property))
                    index = i;

            if (index < 0)
                return false;

            if (Durations is { Count: > 0 } durations)
                duration = durations[index % durations.Count];
            if (Delays is { Count: > 0 } delays)
                delay = delays[index % delays.Count];
            if (TimingFunctions is { Count: > 0 } timings)
                timing = timings[index % timings.Count];

            // A zero-length, undelayed transition is CSS's "no transition".
            return duration > TimeSpan.Zero || delay > TimeSpan.Zero;
        }

        private static readonly IReadOnlyList<string> all_list = new[] { "all" };

        // Whether the transition entry `entry` names `longhand`: itself, or a shorthand that includes it (border-color covers
        // border-top-color, border-radius covers border-top-left-radius).
        private static bool Covers (string entry, string longhand)
        {
            if (string.Equals (entry, longhand, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals (entry, "border-color", StringComparison.OrdinalIgnoreCase))
                return longhand.StartsWith ("border-", StringComparison.OrdinalIgnoreCase) && longhand.EndsWith ("-color", StringComparison.OrdinalIgnoreCase);

            if (string.Equals (entry, "border-radius", StringComparison.OrdinalIgnoreCase))
                return longhand.StartsWith ("border-", StringComparison.OrdinalIgnoreCase) && longhand.EndsWith ("-radius", StringComparison.OrdinalIgnoreCase);

            return false;
        }

        /// <summary>The transition spelled as the CSS <c>transition</c> shorthand would write it, lists unset shown as their CSS initial values.</summary>
        public override string ToString ()
        {
            var properties = Properties ?? all_list;

            if (properties.Count == 0)
                return "none";

            var items = new List<string> ();

            for (var i = 0; i < properties.Count; i++) {
                var duration = Durations is { Count: > 0 } d ? d[i % d.Count] : TimeSpan.Zero;
                var timing = TimingFunctions is { Count: > 0 } t ? t[i % t.Count] : ControlTimingFunction.Ease;
                var delay = Delays is { Count: > 0 } l ? l[i % l.Count] : TimeSpan.Zero;
                items.Add ($"{properties[i]} {FormatTime (duration)} {timing}{(delay == TimeSpan.Zero ? "" : " " + FormatTime (delay))}");
            }

            return string.Join (", ", items);
        }

        internal static string FormatTime (TimeSpan time)
            => time.TotalMilliseconds.ToString ("0.###", CultureInfo.InvariantCulture) + "ms";
    }

    /// <summary>
    /// A CSS timing function -- <c>ease</c>, <c>cubic-bezier(...)</c>, <c>steps(...)</c> -- with the curve it names. <see cref="Css"/> is
    /// how a stylesheet spells it; <see cref="Function"/> maps linear progress to eased progress.
    /// </summary>
    public sealed class ControlTimingFunction
    {
        private ControlTimingFunction (string css, EasingFunction function)
        {
            Css = css;
            Function = function;
        }

        /// <summary>The CSS spelling, e.g. <c>ease-in-out</c> or <c>cubic-bezier(0.22, 1, 0.36, 1)</c>.</summary>
        public string Css { get; }

        /// <summary>The curve: linear progress from 0 to 1 in, eased progress out.</summary>
        public EasingFunction Function { get; }

        /// <summary>CSS <c>linear</c>.</summary>
        public static ControlTimingFunction Linear { get; } = new ("linear", Easing.Linear);

        /// <summary>CSS <c>ease</c>, the initial value of <c>transition-timing-function</c>.</summary>
        public static ControlTimingFunction Ease { get; } = new ("ease", Easing.Ease);

        /// <summary>CSS <c>ease-in</c>.</summary>
        public static ControlTimingFunction EaseIn { get; } = new ("ease-in", Easing.EaseIn);

        /// <summary>CSS <c>ease-out</c>.</summary>
        public static ControlTimingFunction EaseOut { get; } = new ("ease-out", Easing.EaseOut);

        /// <summary>CSS <c>ease-in-out</c>.</summary>
        public static ControlTimingFunction EaseInOut { get; } = new ("ease-in-out", Easing.EaseInOut);

        /// <summary>CSS <c>step-start</c>, <c>steps(1, jump-start)</c>.</summary>
        public static ControlTimingFunction StepStart { get; } = new ("step-start", Easing.Steps (1, StepPosition.JumpStart));

        /// <summary>CSS <c>step-end</c>, <c>steps(1, jump-end)</c>.</summary>
        public static ControlTimingFunction StepEnd { get; } = new ("step-end", Easing.Steps (1, StepPosition.JumpEnd));

        /// <summary>CSS <c>cubic-bezier(x1, y1, x2, y2)</c>; see <see cref="Easing.CubicBezier"/>.</summary>
        public static ControlTimingFunction CubicBezier (float x1, float y1, float x2, float y2)
            => new ($"cubic-bezier({Format (x1)}, {Format (y1)}, {Format (x2)}, {Format (y2)})", Easing.CubicBezier (x1, y1, x2, y2));

        /// <summary>CSS <c>steps(n, position)</c>; see <see cref="Easing.Steps"/>.</summary>
        public static ControlTimingFunction Steps (int steps, StepPosition position = StepPosition.JumpEnd)
            => new ($"steps({steps.ToString (CultureInfo.InvariantCulture)}, {position switch {
                StepPosition.JumpStart => "jump-start",
                StepPosition.JumpNone => "jump-none",
                StepPosition.JumpBoth => "jump-both",
                _ => "jump-end"
            }})", Easing.Steps (steps, position));

        /// <summary>A timing function with a curve of your own, under a name for display.</summary>
        /// <param name="name">How it is shown.</param>
        /// <param name="function">The curve.</param>
        public static ControlTimingFunction Custom (string name, EasingFunction function)
        {
            Guard.ThrowIfNull (name);
            Guard.ThrowIfNull (function);
            return new (name, function);
        }

        private static string Format (float value) => value.ToString ("0.###", CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override string ToString () => Css;
    }
}
