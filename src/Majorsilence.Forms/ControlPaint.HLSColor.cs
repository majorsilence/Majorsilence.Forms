using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    public static partial class ControlPaint
    {
        /// <summary>
        /// The Win32 hue/luminosity/saturation model <c>ControlPaint.Light</c> and <c>Dark</c> are
        /// defined in terms of (GFX-02). Ported from <c>ControlPaint.HLSColor</c> in dotnet/winforms
        /// (<c>src/System.Windows.Forms/System/Windows/Forms/Rendering/ControlPaint.HLSColor.cs</c>):
        /// integer maths on a 0-240 range, with no Win32 dependency.
        /// </summary>
        /// <remarks>
        /// This exists because linear RGB addition is not the same operation. Adding a constant to each
        /// channel desaturates towards white or black; moving luminosity in HLS keeps the hue. The
        /// visible difference: <c>Dark (Color.Red)</c> is (128,0,0) here and was (230,0,0) -- barely
        /// distinguishable from red -- so every bevel built out of these collapsed to flat.
        /// </remarks>
        internal readonly struct HLSColor
        {
            private const int ShadowAdjustment = -333;
            private const int HighlightAdjustment = 500;

            private const int Range = 240;
            private const int HLSMax = Range;
            private const int RGBMax = 255;
            private const int Undefined = HLSMax * 2 / 3;

            private readonly int hue;
            private readonly int saturation;
            private readonly int luminosity;
            private readonly bool is_system_colors_control;

            internal HLSColor (Color color)
            {
                // Whether the colour IS SystemColors.Control decides more than a shade: the lighter and
                // darker paths short-circuit to the exact ControlLight/ControlDark values for it, which
                // is why Light (SystemColors.Control) is DARKER than Control in WinForms.
                is_system_colors_control = color.ToArgb () == SystemColors.Control.ToArgb ();

                int r = color.R, g = color.G, b = color.B;
                var max = Math.Max (Math.Max (r, g), b);
                var min = Math.Min (Math.Min (r, g), b);
                var sum = max + min;

                luminosity = ((sum * HLSMax) + RGBMax) / (2 * RGBMax);

                var dif = max - min;

                if (dif == 0) {
                    saturation = 0;
                    hue = Undefined;
                    return;
                }

                saturation = luminosity <= HLSMax / 2
                    ? ((dif * HLSMax) + (sum / 2)) / sum
                    : ((dif * HLSMax) + ((2 * RGBMax - sum) / 2)) / (2 * RGBMax - sum);

                var rdelta = (((max - r) * (HLSMax / 6)) + (dif / 2)) / dif;
                var gdelta = (((max - g) * (HLSMax / 6)) + (dif / 2)) / dif;
                var bdelta = (((max - b) * (HLSMax / 6)) + (dif / 2)) / dif;

                if (r == max)
                    hue = bdelta - gdelta;
                else if (g == max)
                    hue = (HLSMax / 3) + rdelta - bdelta;
                else
                    hue = (2 * HLSMax / 3) + gdelta - rdelta;

                if (hue < 0)
                    hue += HLSMax;

                if (hue > HLSMax)
                    hue -= HLSMax;
            }

            internal int Luminosity => luminosity;

            internal Color Darker (float percDarker)
            {
                if (!is_system_colors_control)
                    return ColorFromHLS (hue, luminosity - (int)(luminosity * percDarker), saturation);

                // The exact system values, not a computed shade -- what upstream does, and what makes a
                // classic 3D bevel on a default-coloured control match the platform's.
                var oneLumDarker = NewLuma (ShadowAdjustment);

                return percDarker == 0.0f ? SystemColors.ControlDark
                    : percDarker == 1.0f ? SystemColors.ControlDarkDark
                    : ColorFromHLS (hue, oneLumDarker - (int)((oneLumDarker - NewLuma (ShadowAdjustment * 2)) * percDarker), saturation);
            }

            internal Color Lighter (float percLighter)
            {
                if (!is_system_colors_control)
                    return ColorFromHLS (hue, luminosity + (int)((HLSMax - luminosity) * percLighter), saturation);

                var oneLumLighter = NewLuma (HighlightAdjustment);

                return percLighter == 0.0f ? SystemColors.ControlLight
                    : percLighter == 1.0f ? SystemColors.ControlLightLight
                    : ColorFromHLS (hue, oneLumLighter + (int)((NewLuma (HighlightAdjustment * 2) - oneLumLighter) * percLighter), saturation);
            }

            // Upstream's NewLuma takes a `scale` flag; every caller here passes true, so the unscaled
            // branch is not carried -- an unexercised branch is worse than a missing one.
            private int NewLuma (int n)
            {
                if (n == 0)
                    return luminosity;

                return n > 0
                    ? (int)((luminosity * (1000 - n) + (Range + 1L) * n) / 1000)
                    : luminosity * (n + 1000) / 1000;
            }

            private static Color ColorFromHLS (int hue, int luminosity, int saturation)
            {
                byte r, g, b;

                if (saturation == 0) {
                    r = g = b = (byte)(luminosity * RGBMax / HLSMax);
                } else {
                    var magic2 = luminosity <= HLSMax / 2
                        ? (luminosity * (HLSMax + saturation) + (HLSMax / 2)) / HLSMax
                        : luminosity + saturation - ((luminosity * saturation) + (HLSMax / 2)) / HLSMax;
                    var magic1 = 2 * luminosity - magic2;

                    r = (byte)((HueToRGB (magic1, magic2, hue + (HLSMax / 3)) * RGBMax + (HLSMax / 2)) / HLSMax);
                    g = (byte)((HueToRGB (magic1, magic2, hue) * RGBMax + (HLSMax / 2)) / HLSMax);
                    b = (byte)((HueToRGB (magic1, magic2, hue - (HLSMax / 3)) * RGBMax + (HLSMax / 2)) / HLSMax);
                }

                return Color.FromArgb (r, g, b);
            }

            private static int HueToRGB (int n1, int n2, int hue)
            {
                if (hue < 0)
                    hue += HLSMax;

                if (hue > HLSMax)
                    hue -= HLSMax;

                if (hue < HLSMax / 6)
                    return n1 + (((n2 - n1) * hue + (HLSMax / 12)) / (HLSMax / 6));

                if (hue < HLSMax / 2)
                    return n2;

                if (hue < HLSMax * 2 / 3)
                    return n1 + (((n2 - n1) * ((HLSMax * 2 / 3) - hue) + (HLSMax / 12)) / (HLSMax / 6));

                return n1;
            }
        }
    }
}
