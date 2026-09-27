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
    }
}
