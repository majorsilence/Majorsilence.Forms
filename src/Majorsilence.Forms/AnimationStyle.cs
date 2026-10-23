using System.Collections.Generic;
using System.Linq;

namespace Majorsilence.Forms
{
    /// <summary>Which way an animation's iterations run (CSS <c>animation-direction</c>).</summary>
    public enum ControlAnimationDirection
    {
        /// <summary>Every iteration runs from the first keyframe to the last.</summary>
        Normal,

        /// <summary>Every iteration runs from the last keyframe to the first.</summary>
        Reverse,

        /// <summary>Iterations alternate, starting forwards.</summary>
        Alternate,

        /// <summary>Iterations alternate, starting backwards.</summary>
        AlternateReverse
    }

    /// <summary>Whether an animation's values apply before it starts and after it ends (CSS <c>animation-fill-mode</c>).</summary>
    public enum ControlAnimationFillMode
    {
        /// <summary>Only while it runs.</summary>
        None,

        /// <summary>The last keyframe's values stay after it ends.</summary>
        Forwards,

        /// <summary>The first keyframe's values apply during the delay.</summary>
        Backwards,

        /// <summary>Both.</summary>
        Both
    }

    /// <summary>
    /// A named set of keyframes -- a stylesheet's <c>@keyframes name { from { ... } 50% { ... } to { ... } }</c> -- that an
    /// <see cref="ControlAnimation"/> plays.
    /// </summary>
    public sealed class ControlKeyframes
    {
        internal ControlKeyframes (string name, IReadOnlyList<ControlKeyframe> frames)
        {
            Name = name;
            Frames = frames;
        }

        /// <summary>The name <c>animation-name</c> refers to it by.</summary>
        public string Name { get; }

        /// <summary>The keyframes, in order of their offsets.</summary>
        public IReadOnlyList<ControlKeyframe> Frames { get; }

        /// <inheritdoc/>
        public override string ToString () => Name;
    }

    /// <summary>One keyframe: where in the animation it is, and the values it sets.</summary>
    public sealed class ControlKeyframe
    {
        internal ControlKeyframe (double offset, Action<ControlStyle> apply, IReadOnlyList<string> properties, ControlTimingFunction? timingFunction,
            IReadOnlyList<ThemeCssDeclaration>? declarations = null)
        {
            Offset = offset;
            Apply = apply;
            Properties = properties;
            TimingFunction = timingFunction;
            Declarations = declarations ?? Array.Empty<ThemeCssDeclaration> ();
        }

        /// <summary>Where the keyframe sits, 0 (<c>from</c>) to 1 (<c>to</c>).</summary>
        public double Offset { get; }

        /// <summary>The CSS properties the keyframe sets.</summary>
        public IReadOnlyList<string> Properties { get; }

        /// <summary>
        /// The keyframe's declarations in the host-neutral form a rule's are (longhands, values resolved and live), for a host
        /// applier that plays the animation with its own toolkit's animation system instead of ours.
        /// </summary>
        public IReadOnlyList<ThemeCssDeclaration> Declarations { get; }

        /// <summary>The easing from this keyframe to the next, or null for the animation's own.</summary>
        public ControlTimingFunction? TimingFunction { get; }

        // Puts the keyframe's values on a style. Run each time an animation starts, so a value written as a theme token reads the
        // token as it is then.
        internal Action<ControlStyle> Apply { get; }
    }

    /// <summary>
    /// The keyframe animations a style plays while a control is in it -- the CSS <c>animation</c> property. Each list is one CSS
    /// longhand; one left null is inherited from the parent style, as <see cref="ControlTransition"/>'s are, and they line up by index
    /// with <see cref="Keyframes"/>, a shorter list repeating.
    /// </summary>
    /// <remarks>
    /// An animation starts when its control first shows the style, or enters it, and runs on display frames. Its values win over the
    /// style's own and over a transition's, as in CSS. Only the properties a transition can animate can be animated
    /// (<see cref="ControlTransition.AnimatableProperties"/>). An infinite animation pauses while the control is not on screen, and
    /// none runs when the user prefers reduced motion (<see cref="SystemInformation.PrefersReducedMotion"/>).
    /// </remarks>
    public sealed class ControlAnimation
    {
        /// <summary>Creates an animation; each list left null is inherited from the parent style.</summary>
        public ControlAnimation (IReadOnlyList<ControlKeyframes>? keyframes = null, IReadOnlyList<TimeSpan>? durations = null,
            IReadOnlyList<ControlTimingFunction>? timingFunctions = null, IReadOnlyList<TimeSpan>? delays = null,
            IReadOnlyList<double>? iterationCounts = null, IReadOnlyList<ControlAnimationDirection>? directions = null,
            IReadOnlyList<ControlAnimationFillMode>? fillModes = null, IReadOnlyList<bool>? paused = null)
        {
            if (durations is not null && durations.Any (d => d < TimeSpan.Zero))
                throw new ArgumentException ("An animation duration cannot be negative.", nameof (durations));
            if (iterationCounts is not null && iterationCounts.Any (c => c < 0 || double.IsNaN (c)))
                throw new ArgumentException ("An iteration count cannot be negative.", nameof (iterationCounts));

            Keyframes = keyframes;
            Durations = Empty (durations);
            TimingFunctions = Empty (timingFunctions);
            Delays = Empty (delays);
            IterationCounts = Empty (iterationCounts);
            Directions = Empty (directions);
            FillModes = Empty (fillModes);
            Paused = Empty (paused);
        }

        private static IReadOnlyList<T>? Empty<T> (IReadOnlyList<T>? list) => list is { Count: 0 } ? null : list;

        /// <summary>The keyframes each animation plays (<c>animation-name</c>); empty for <c>none</c>; null inherits.</summary>
        public IReadOnlyList<ControlKeyframes>? Keyframes { get; }

        /// <summary>How long one iteration takes (<c>animation-duration</c>); null inherits.</summary>
        public IReadOnlyList<TimeSpan>? Durations { get; }

        /// <summary>The easing between keyframes (<c>animation-timing-function</c>); null inherits.</summary>
        public IReadOnlyList<ControlTimingFunction>? TimingFunctions { get; }

        /// <summary>How long each waits before starting (<c>animation-delay</c>); negative starts part-way; null inherits.</summary>
        public IReadOnlyList<TimeSpan>? Delays { get; }

        /// <summary>How many times each runs (<c>animation-iteration-count</c>), <see cref="double.PositiveInfinity"/> for ever; null inherits.</summary>
        public IReadOnlyList<double>? IterationCounts { get; }

        /// <summary>Which way each runs (<c>animation-direction</c>); null inherits.</summary>
        public IReadOnlyList<ControlAnimationDirection>? Directions { get; }

        /// <summary>Whether each applies before and after it runs (<c>animation-fill-mode</c>); null inherits.</summary>
        public IReadOnlyList<ControlAnimationFillMode>? FillModes { get; }

        /// <summary>Whether each is paused (<c>animation-play-state: paused</c>); null inherits.</summary>
        public IReadOnlyList<bool>? Paused { get; }

        /// <summary>Returns a copy with the given lists replaced; a null argument keeps this animation's list.</summary>
        public ControlAnimation With (IReadOnlyList<ControlKeyframes>? keyframes = null, IReadOnlyList<TimeSpan>? durations = null,
            IReadOnlyList<ControlTimingFunction>? timingFunctions = null, IReadOnlyList<TimeSpan>? delays = null,
            IReadOnlyList<double>? iterationCounts = null, IReadOnlyList<ControlAnimationDirection>? directions = null,
            IReadOnlyList<ControlAnimationFillMode>? fillModes = null, IReadOnlyList<bool>? paused = null)
            => new (keyframes ?? Keyframes, durations ?? Durations, timingFunctions ?? TimingFunctions, delays ?? Delays,
                iterationCounts ?? IterationCounts, directions ?? Directions, fillModes ?? FillModes, paused ?? Paused);

        internal ControlAnimation Inherit (ControlAnimation? fallback)
            => fallback is null ? this : new (Keyframes ?? fallback.Keyframes, Durations ?? fallback.Durations, TimingFunctions ?? fallback.TimingFunctions,
                Delays ?? fallback.Delays, IterationCounts ?? fallback.IterationCounts, Directions ?? fallback.Directions,
                FillModes ?? fallback.FillModes, Paused ?? fallback.Paused);

        // The n-th animation's settings, CSS's initial values where a list is unset.
        internal (TimeSpan Duration, ControlTimingFunction Timing, TimeSpan Delay, double Iterations, ControlAnimationDirection Direction, ControlAnimationFillMode Fill, bool Paused) At (int index)
            => (Pick (Durations, index, TimeSpan.Zero), Pick (TimingFunctions, index, ControlTimingFunction.Ease), Pick (Delays, index, TimeSpan.Zero),
                Pick (IterationCounts, index, 1.0), Pick (Directions, index, ControlAnimationDirection.Normal),
                Pick (FillModes, index, ControlAnimationFillMode.None), Pick (Paused, index, false));

        private static T Pick<T> (IReadOnlyList<T>? list, int index, T fallback) => list is { Count: > 0 } ? list[index % list.Count] : fallback;

        /// <inheritdoc/>
        public override string ToString ()
            => Keyframes is { Count: > 0 } k ? string.Join (", ", k.Select ((f, i) => $"{f.Name} {ControlTransition.FormatTime (At (i).Duration)}")) : "none";
    }
}
