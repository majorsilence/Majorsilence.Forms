using System;
using System.Drawing;

namespace Majorsilence.Forms.Animation
{
    /// <summary>
    /// A value that moves from one value to another over a duration, shaped by an easing function. It holds no clock and no state: ask it for
    /// the value at a time, so it is deterministic and one tween can drive any number of animations. An <see cref="Animator"/> runs one.
    /// </summary>
    /// <typeparam name="T">The type moved. <see cref="Tween.Of(float, float, TimeSpan, EasingFunction)"/> makes one for <see cref="float"/>, <see cref="Color"/> or <see cref="PointF"/>.</typeparam>
    public sealed class Tween<T>
    {
        private readonly Func<T, T, float, T> interpolate;

        /// <summary>Creates a tween with your own interpolation.</summary>
        /// <param name="from">The value at the start.</param>
        /// <param name="to">The value at the end.</param>
        /// <param name="duration">How long it takes. Zero or less means it is at <paramref name="to"/> from the first frame.</param>
        /// <param name="interpolate">Returns the value a given fraction of the way from the first argument to the second. The fraction is the
        /// eased progress, so it can be below 0 or above 1 with a back easing.</param>
        /// <param name="easing">The shape of the movement. Defaults to <see cref="Easing.Linear"/>.</param>
        public Tween (T from, T to, TimeSpan duration, Func<T, T, float, T> interpolate, EasingFunction? easing = null)
        {
            Guard.ThrowIfNull (interpolate);

            From = from;
            To = to;
            Duration = duration;
            this.interpolate = interpolate;
            Easing = easing ?? Animation.Easing.Linear;
        }

        /// <summary>Gets the value at the start.</summary>
        public T From { get; }

        /// <summary>Gets the value at the end.</summary>
        public T To { get; }

        /// <summary>Gets how long the movement takes.</summary>
        public TimeSpan Duration { get; }

        /// <summary>Gets the easing function.</summary>
        public EasingFunction Easing { get; }

        /// <summary>Returns the value <paramref name="elapsed"/> after the start: <see cref="From"/> at or before 0, <see cref="To"/> at or after <see cref="Duration"/>.</summary>
        public T ValueAt (TimeSpan elapsed)
            => ValueAtProgress (Duration <= TimeSpan.Zero ? 1f : (float) (elapsed.TotalSeconds / Duration.TotalSeconds));

        /// <summary>Returns the value at linear progress from 0 to 1, clamped to that range before it is eased.</summary>
        public T ValueAtProgress (float progress)
        {
            if (progress <= 0f)
                progress = 0f;
            else if (progress >= 1f)
                progress = 1f;

            return interpolate (From, To, Easing (progress));
        }

        /// <summary>Returns whether <paramref name="elapsed"/> has reached the end.</summary>
        public bool IsComplete (TimeSpan elapsed) => elapsed >= Duration;
    }

    /// <summary>Makes tweens for the types most animations move.</summary>
    public static class Tween
    {
        /// <summary>Makes a tween of a number, moved linearly between the two values before easing.</summary>
        public static Tween<float> Of (float from, float to, TimeSpan duration, EasingFunction? easing = null)
            => new (from, to, duration, static (a, b, t) => a + (b - a) * t, easing);

        /// <summary>Makes a tween of a point, each coordinate moved as a number.</summary>
        public static Tween<PointF> Of (PointF from, PointF to, TimeSpan duration, EasingFunction? easing = null)
            => new (from, to, duration, static (a, b, t) => new PointF (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t), easing);

        /// <summary>
        /// Makes a tween of a colour, each of alpha, red, green and blue moved as a number in sRGB, which is what the channels hold. That is
        /// simple and predictable, and it is not perceptually even. A channel is clamped to 0 to 255, so a back easing that overshoots does
        /// not wrap around.
        /// </summary>
        public static Tween<Color> Of (Color from, Color to, TimeSpan duration, EasingFunction? easing = null)
            => new (from, to, duration, static (a, b, t) => Color.FromArgb (
                Channel (a.A, b.A, t), Channel (a.R, b.R, t), Channel (a.G, b.G, t), Channel (a.B, b.B, t)), easing);

        private static int Channel (byte from, byte to, float t)
        {
            var value = (int) Math.Round (from + (to - from) * t);
            return value < 0 ? 0 : value > 255 ? 255 : value;
        }
    }
}
