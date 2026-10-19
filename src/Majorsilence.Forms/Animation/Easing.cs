namespace Majorsilence.Forms.Animation
{
    /// <summary>Maps linear progress (0 at the start, 1 at the end) to eased progress. It may leave 0 to 1 in between, as a back easing does.</summary>
    /// <param name="progress">Linear progress, from 0 to 1.</param>
    public delegate float EasingFunction (float progress);

    /// <summary>
    /// The easing functions, each taking linear progress from 0 to 1 and returning eased progress that is 0 at 0 and 1 at 1. They match the
    /// standard set at easings.net, and any one converts to an <see cref="EasingFunction"/>: <c>Tween.Of (0f, 1f, duration, Easing.CubicOut)</c>.
    /// </summary>
    public static class Easing
    {
        private const float Back1 = 1.70158f;
        private const float Back2 = Back1 * 1.525f;
        private const float Back3 = Back1 + 1f;

        /// <summary>Constant speed.</summary>
        public static float Linear (float t) => t;

        /// <summary>Starts slowly and speeds up (t squared).</summary>
        public static float QuadIn (float t) => t * t;

        /// <summary>Starts fast and slows down.</summary>
        public static float QuadOut (float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Slow at both ends, fast in the middle.</summary>
        public static float QuadInOut (float t)
        {
            if (t < 0.5f)
                return 2f * t * t;

            var u = -2f * t + 2f;
            return 1f - u * u / 2f;
        }

        /// <summary>Starts slowly and speeds up (t cubed).</summary>
        public static float CubicIn (float t) => t * t * t;

        /// <summary>Starts fast and slows down.</summary>
        public static float CubicOut (float t)
        {
            var u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Slow at both ends, fast in the middle.</summary>
        public static float CubicInOut (float t)
        {
            if (t < 0.5f)
                return 4f * t * t * t;

            var u = -2f * t + 2f;
            return 1f - u * u * u / 2f;
        }

        /// <summary>Pulls back below 0 before it goes.</summary>
        public static float BackIn (float t) => Back3 * t * t * t - Back1 * t * t;

        /// <summary>Overshoots 1 and settles back to it.</summary>
        public static float BackOut (float t)
        {
            var u = t - 1f;
            return 1f + Back3 * u * u * u + Back1 * u * u;
        }

        /// <summary>Pulls back at the start and overshoots at the end.</summary>
        public static float BackInOut (float t)
        {
            if (t < 0.5f) {
                var a = 2f * t;
                return a * a * ((Back2 + 1f) * a - Back2) / 2f;
            }

            var b = 2f * t - 2f;
            return (b * b * ((Back2 + 1f) * b + Back2) + 2f) / 2f;
        }

        /// <summary>Lands and bounces, each bounce smaller.</summary>
        public static float BounceOut (float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;

            if (t < 2f / d1) {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }

            if (t < 2.5f / d1) {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        /// <summary>Bounces before it goes.</summary>
        public static float BounceIn (float t) => 1f - BounceOut (1f - t);

        /// <summary>Bounces at both ends.</summary>
        public static float BounceInOut (float t)
            => t < 0.5f ? (1f - BounceOut (1f - 2f * t)) / 2f : (1f + BounceOut (2f * t - 1f)) / 2f;

        // The CSS keyword curves, as CSS Easing Functions Level 1 defines them.
        private static readonly EasingFunction ease_curve = CubicBezier (0.25f, 0.1f, 0.25f, 1f);
        private static readonly EasingFunction ease_in_curve = CubicBezier (0.42f, 0f, 1f, 1f);
        private static readonly EasingFunction ease_out_curve = CubicBezier (0f, 0f, 0.58f, 1f);
        private static readonly EasingFunction ease_in_out_curve = CubicBezier (0.42f, 0f, 0.58f, 1f);

        /// <summary>The CSS <c>ease</c> curve, <c>cubic-bezier(0.25, 0.1, 0.25, 1)</c>: the default timing of a CSS transition.</summary>
        public static float Ease (float t) => ease_curve (t);

        /// <summary>The CSS <c>ease-in</c> curve, <c>cubic-bezier(0.42, 0, 1, 1)</c>.</summary>
        public static float EaseIn (float t) => ease_in_curve (t);

        /// <summary>The CSS <c>ease-out</c> curve, <c>cubic-bezier(0, 0, 0.58, 1)</c>.</summary>
        public static float EaseOut (float t) => ease_out_curve (t);

        /// <summary>The CSS <c>ease-in-out</c> curve, <c>cubic-bezier(0.42, 0, 0.58, 1)</c>.</summary>
        public static float EaseInOut (float t) => ease_in_out_curve (t);

        /// <summary>
        /// Creates the CSS <c>cubic-bezier(x1, y1, x2, y2)</c> curve: a cubic Bézier from (0, 0) to (1, 1) whose two control points are
        /// given. The x coordinates must be within 0 to 1, so that the curve is a function of time; the y coordinates may leave that range
        /// (overshoot).
        /// </summary>
        /// <param name="x1">The first control point's x, 0 to 1.</param>
        /// <param name="y1">The first control point's y.</param>
        /// <param name="x2">The second control point's x, 0 to 1.</param>
        /// <param name="y2">The second control point's y.</param>
        /// <exception cref="ArgumentOutOfRangeException">An x coordinate is outside 0 to 1.</exception>
        public static EasingFunction CubicBezier (float x1, float y1, float x2, float y2)
        {
            if (!(x1 >= 0f && x1 <= 1f))
                throw new ArgumentOutOfRangeException (nameof (x1), x1, "A cubic-bezier x coordinate must be between 0 and 1.");
            if (!(x2 >= 0f && x2 <= 1f))
                throw new ArgumentOutOfRangeException (nameof (x2), x2, "A cubic-bezier x coordinate must be between 0 and 1.");

            // The polynomial coefficients of x(s) and y(s), the same decomposition WebKit's UnitBezier uses.
            var cx = 3.0 * x1;
            var bx = 3.0 * (x2 - x1) - cx;
            var ax = 1.0 - cx - bx;
            var cy = 3.0 * y1;
            var by = 3.0 * (y2 - y1) - cy;
            var ay = 1.0 - cy - by;

            double SampleX (double s) => ((ax * s + bx) * s + cx) * s;
            double SampleY (double s) => ((ay * s + by) * s + cy) * s;
            double SampleDerivativeX (double s) => (3.0 * ax * s + 2.0 * bx) * s + cx;

            // Finds the curve parameter s whose x is the given time: Newton-Raphson from s = x, falling back to bisection where the slope is
            // too flat for Newton to converge.
            double SolveX (double x)
            {
                const double epsilon = 1e-7;
                var s = x;

                for (var i = 0; i < 8; i++) {
                    var error = SampleX (s) - x;
                    if (Math.Abs (error) < epsilon)
                        return s;
                    var slope = SampleDerivativeX (s);
                    if (Math.Abs (slope) < 1e-6)
                        break;
                    s -= error / slope;
                }

                double low = 0.0, high = 1.0;
                s = x;

                while (low < high) {
                    var value = SampleX (s);
                    if (Math.Abs (value - x) < epsilon)
                        return s;
                    if (x > value)
                        low = s;
                    else
                        high = s;
                    var next = (low + high) / 2.0;
                    if (next == s)
                        break;
                    s = next;
                }

                return s;
            }

            return t => {
                if (t <= 0f)
                    return 0f;
                if (t >= 1f)
                    return 1f;
                return (float) SampleY (SolveX (t));
            };
        }

        /// <summary>
        /// Creates the CSS <c>steps(n, position)</c> function: progress jumps in <paramref name="steps"/> equal stops instead of moving
        /// smoothly. <c>step-start</c> is <c>Steps (1, StepPosition.JumpStart)</c> and <c>step-end</c> is <c>Steps (1, StepPosition.JumpEnd)</c>.
        /// </summary>
        /// <param name="steps">The number of intervals: at least 1, and at least 2 for <see cref="StepPosition.JumpNone"/>.</param>
        /// <param name="position">Where the jumps fall (CSS's <c>jump-start</c>, <c>jump-end</c>, <c>jump-none</c>, <c>jump-both</c>).</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is too small for <paramref name="position"/>.</exception>
        public static EasingFunction Steps (int steps, StepPosition position = StepPosition.JumpEnd)
        {
            if (steps < 1 || (position == StepPosition.JumpNone && steps < 2))
                throw new ArgumentOutOfRangeException (nameof (steps), steps, "steps() needs at least 1 interval, and at least 2 with jump-none.");

            // CSS Easing Functions Level 1, "the steps() function": the current step, adjusted for where the jumps fall, over the number
            // of jumps.
            var jumps = position switch {
                StepPosition.JumpNone => steps - 1,
                StepPosition.JumpBoth => steps + 1,
                _ => steps
            };

            return t => {
                if (t < 0f)
                    return 0f;
                if (t >= 1f)
                    return 1f;

                var step = (float) Math.Floor (t * steps);
                if (position is StepPosition.JumpStart or StepPosition.JumpBoth)
                    step += 1f;

                return (float) MathCompat.Clamp (step / jumps, 0.0, 1.0);
            };
        }
    }

    /// <summary>Where a <see cref="Easing.Steps"/> function's jumps fall, as CSS's <c>steps()</c> positions name them.</summary>
    public enum StepPosition
    {
        /// <summary>The last jump happens when the animation ends (<c>jump-end</c>, also <c>end</c>; the CSS default).</summary>
        JumpEnd,

        /// <summary>The first jump happens when the animation begins (<c>jump-start</c>, also <c>start</c>).</summary>
        JumpStart,

        /// <summary>No jump at either end: the value holds at the start and at the end for one interval each (<c>jump-none</c>).</summary>
        JumpNone,

        /// <summary>A jump at both ends (<c>jump-both</c>).</summary>
        JumpBoth
    }
}
