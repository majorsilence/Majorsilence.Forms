using System;
using System.Drawing;

namespace Majorsilence.Forms.Drawing.Drawing2D
{
    /// <summary>
    /// Defines the falloff of a gradient as blend factors at given positions. Cross-platform
    /// replacement for <c>System.Drawing.Drawing2D.Blend</c>.
    /// </summary>
    /// <remarks>
    /// A factor of 0 means "entirely the starting color" and 1 means "entirely the ending color", so
    /// a blend describes how quickly the gradient moves between its two endpoint colors.
    /// <see cref="Positions"/> must run from 0 to 1 and have the same length as
    /// <see cref="Factors"/>.
    /// </remarks>
    public sealed class Blend
    {
        /// <summary>Initializes a new Blend with a single (0, 0) entry.</summary>
        public Blend () : this (1) { }

        /// <summary>Initializes a new Blend with the specified number of factor/position slots.</summary>
        public Blend (int count)
        {
            Guard.ThrowIfNegative (count);
            Factors = new float[count];
            Positions = new float[count];
        }

        /// <summary>Gets or sets the blend factors, one per entry in <see cref="Positions"/>.</summary>
        public float[] Factors { get; set; }

        /// <summary>Gets or sets the positions (0..1) the factors apply at.</summary>
        public float[] Positions { get; set; }
    }

    /// <summary>
    /// Defines a multi-color gradient as colors at given positions. Cross-platform replacement for
    /// <c>System.Drawing.Drawing2D.ColorBlend</c>.
    /// </summary>
    public sealed class ColorBlend
    {
        /// <summary>Initializes a new ColorBlend with a single slot.</summary>
        public ColorBlend () : this (1) { }

        /// <summary>Initializes a new ColorBlend with the specified number of color/position slots.</summary>
        public ColorBlend (int count)
        {
            Guard.ThrowIfNegative (count);
            Colors = new Color[count];
            Positions = new float[count];
        }

        /// <summary>Gets or sets the colors of the gradient stops.</summary>
        public Color[] Colors { get; set; }

        /// <summary>Gets or sets the positions (0..1) of the gradient stops.</summary>
        public float[] Positions { get; set; }
    }

    /// <summary>
    /// Builds the GDI+ <c>SetBlendTriangularShape</c> / <c>SetSigmaBellShape</c> factor ramps and
    /// expands a <see cref="Blend"/> into concrete gradient stops.
    /// </summary>
    internal static class GradientBlendShapes
    {
        // GDI+ samples its bell curve into a fixed-size table; 256 samples matches its resolution
        // closely enough that the rendered ramp is visually identical.
        private const int SigmaSamples = 256;

        /// <summary>
        /// Builds the piecewise-linear "triangular" ramp: factors rise linearly from 0 at position 0
        /// to <paramref name="scale"/> at <paramref name="focus"/>, then fall linearly back to 0 at
        /// position 1. A focus of exactly 0 or 1 degenerates to a single straight ramp.
        /// </summary>
        public static Blend Triangular (float focus, float scale)
        {
            focus = MathCompat.Clamp (focus, 0f, 1f);
            scale = MathCompat.Clamp (scale, 0f, 1f);

            if (focus == 0f) {
                return new Blend { Factors = new[] { scale, 0f }, Positions = new[] { 0f, 1f } };
            }
            if (focus == 1f) {
                return new Blend { Factors = new[] { 0f, scale }, Positions = new[] { 0f, 1f } };
            }
            return new Blend {
                Factors = new[] { 0f, scale, 0f },
                Positions = new[] { 0f, focus, 1f }
            };
        }

        /// <summary>
        /// Builds the "sigma bell" ramp: a Gaussian-shaped falloff that rises from 0 at position 0 to
        /// <paramref name="scale"/> at <paramref name="focus"/> and falls back to 0 at position 1,
        /// following the cumulative normal distribution on each side (so the curve leaves and reaches
        /// the endpoints with zero slope, unlike the triangular ramp).
        /// </summary>
        public static Blend SigmaBell (float focus, float scale)
        {
            focus = MathCompat.Clamp (focus, 0f, 1f);
            scale = MathCompat.Clamp (scale, 0f, 1f);

            var factors = new float[SigmaSamples];
            var positions = new float[SigmaSamples];

            for (var i = 0; i < SigmaSamples; i++) {
                var position = i / (float)(SigmaSamples - 1);
                positions[i] = position;

                float t;
                if (focus <= 0f)
                    t = 1f - position;                              // falling half only
                else if (focus >= 1f)
                    t = position;                                   // rising half only
                else if (position <= focus)
                    t = position / focus;                           // rising to the focus
                else
                    t = (1f - position) / (1f - focus);             // falling from the focus

                factors[i] = scale * NormalizedCumulative (t);
            }

            // Guarantee the exact endpoint/peak values despite floating-point drift.
            factors[0] = focus <= 0f ? scale : 0f;
            factors[SigmaSamples - 1] = focus >= 1f ? scale : 0f;

            return new Blend { Factors = factors, Positions = positions };
        }

        /// <summary>
        /// The cumulative normal distribution over [0, 1], rescaled so f(0) == 0 and f(1) == 1. Sigma
        /// is chosen so the half-range spans three standard deviations, which is where the classic
        /// GDI+ bell visually flattens out.
        /// </summary>
        private static float NormalizedCumulative (float t)
        {
            t = MathCompat.Clamp (t, 0f, 1f);
            const double sigma = 0.5 / 3.0;
            var lo = Cumulative (0.0, sigma);
            var hi = Cumulative (1.0, sigma);
            var value = (Cumulative (t, sigma) - lo) / (hi - lo);
            return (float)MathCompat.Clamp (value, 0.0, 1.0);

            static double Cumulative (double x, double s) => 0.5 * (1.0 + Erf ((x - 0.5) / (s * Math.Sqrt (2.0))));
        }

        // Abramowitz & Stegun 7.1.26; max absolute error 1.5e-7, far below 8-bit color resolution.
        private static double Erf (double x)
        {
            var sign = x < 0 ? -1.0 : 1.0;
            x = Math.Abs (x);

            const double a1 = 0.254829592, a2 = -0.284496736, a3 = 1.421413741,
                         a4 = -1.453152027, a5 = 1.061405429, p = 0.3275911;

            var t = 1.0 / (1.0 + p * x);
            var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp (-x * x);
            return sign * y;
        }

        /// <summary>
        /// Expands a <see cref="Blend"/> into explicit color stops between two endpoint colors, so it
        /// can feed the same SkiaSharp gradient-stop path a <see cref="ColorBlend"/> uses.
        /// </summary>
        public static (Color[] Colors, float[] Positions)? Expand (Blend? blend, Color start, Color end)
        {
            if (blend is null)
                return null;

            var factors = blend.Factors;
            var positions = blend.Positions;
            if (factors is null || positions is null || factors.Length < 2 || factors.Length != positions.Length)
                return null;

            var colors = new Color[factors.Length];
            for (var i = 0; i < factors.Length; i++)
                colors[i] = Lerp (start, end, MathCompat.Clamp (factors[i], 0f, 1f));

            return (colors, (float[])positions.Clone ());
        }

        private static Color Lerp (Color from, Color to, float amount) => Color.FromArgb (
            (int)Math.Round (from.A + (to.A - from.A) * amount),
            (int)Math.Round (from.R + (to.R - from.R) * amount),
            (int)Math.Round (from.G + (to.G - from.G) * amount),
            (int)Math.Round (from.B + (to.B - from.B) * amount));
    }
}
