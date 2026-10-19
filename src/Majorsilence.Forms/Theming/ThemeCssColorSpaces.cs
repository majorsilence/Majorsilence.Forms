using SkiaSharp;

namespace Majorsilence.Forms
{
    // The colour spaces CSS Color Level 4 names, and conversions between them through CIE XYZ (D65). The matrices are the ones the
    // specification's sample code uses (CSS Color 4, section 18 "Sample code for color conversions"); OKLab is Björn Ottosson's
    // definition, which CSS adopts. Every result that has to be painted is brought into sRGB by the specification's gamut-mapping
    // algorithm (section 13.2), which lowers chroma in OKLCH until the colour fits instead of clipping each channel, so an out-of-gamut
    // oklch() keeps its hue.
    internal enum CssColorSpace
    {
        Srgb,
        SrgbLinear,
        DisplayP3,
        XyzD65,
        XyzD50,
        Lab,
        Lch,
        Oklab,
        Oklch,
        Hsl,
        Hwb
    }

    // A colour as three channels in some space plus alpha (0..1). Hue channels are degrees and NaN when powerless (an achromatic colour),
    // which is what lets color-mix take the other colour's hue instead of swinging through red.
    internal struct CssColor
    {
        public double C0, C1, C2, Alpha;

        public CssColor (double c0, double c1, double c2, double alpha)
        {
            C0 = c0;
            C1 = c1;
            C2 = c2;
            Alpha = alpha;
        }
    }

    internal static class ThemeCssColorSpaces
    {
        // ---- what each space's channels are -------------------------------------------------------

        // The channel names a relative colour (oklch(from X l c h)) binds, and the value 100% stands for in each channel. A hue channel
        // has no percentage form.
        public static (string Name, double Percent, bool IsHue)[] Channels (CssColorSpace space) => space switch {
            CssColorSpace.Srgb or CssColorSpace.SrgbLinear or CssColorSpace.DisplayP3 => new[] { ("r", 1.0, false), ("g", 1.0, false), ("b", 1.0, false) },
            CssColorSpace.XyzD65 or CssColorSpace.XyzD50 => new[] { ("x", 1.0, false), ("y", 1.0, false), ("z", 1.0, false) },
            CssColorSpace.Lab => new[] { ("l", 100.0, false), ("a", 125.0, false), ("b", 125.0, false) },
            CssColorSpace.Lch => new[] { ("l", 100.0, false), ("c", 150.0, false), ("h", 0.0, true) },
            CssColorSpace.Oklab => new[] { ("l", 1.0, false), ("a", 0.4, false), ("b", 0.4, false) },
            CssColorSpace.Oklch => new[] { ("l", 1.0, false), ("c", 0.4, false), ("h", 0.0, true) },
            CssColorSpace.Hsl => new[] { ("h", 0.0, true), ("s", 100.0, false), ("l", 100.0, false) },
            _ => new[] { ("h", 0.0, true), ("w", 100.0, false), ("b", 100.0, false) },
        };

        public static int HueIndex (CssColorSpace space) => space switch {
            CssColorSpace.Lch or CssColorSpace.Oklch => 2,
            CssColorSpace.Hsl or CssColorSpace.Hwb => 0,
            _ => -1
        };

        public static bool TryParseSpace (string name, out CssColorSpace space)
        {
            switch (name.ToLowerInvariant ()) {
                case "srgb": space = CssColorSpace.Srgb; return true;
                case "srgb-linear": space = CssColorSpace.SrgbLinear; return true;
                case "display-p3": space = CssColorSpace.DisplayP3; return true;
                case "xyz": case "xyz-d65": space = CssColorSpace.XyzD65; return true;
                case "xyz-d50": space = CssColorSpace.XyzD50; return true;
                case "lab": space = CssColorSpace.Lab; return true;
                case "lch": space = CssColorSpace.Lch; return true;
                case "oklab": space = CssColorSpace.Oklab; return true;
                case "oklch": space = CssColorSpace.Oklch; return true;
                case "hsl": space = CssColorSpace.Hsl; return true;
                case "hwb": space = CssColorSpace.Hwb; return true;
                default: space = default; return false;
            }
        }

        public const string SpaceNames = "srgb, srgb-linear, display-p3, xyz, xyz-d50, xyz-d65, lab, lch, oklab, oklch, hsl, hwb";

        public static readonly string[] SpaceList = SpaceNames.Split (new[] { ", " }, StringSplitOptions.None);

        // ---- to and from SKColor ------------------------------------------------------------------

        public static CssColor FromSKColor (SKColor color, CssColorSpace space)
        {
            var srgb = new CssColor (color.Red / 255.0, color.Green / 255.0, color.Blue / 255.0, color.Alpha / 255.0);
            return space == CssColorSpace.Srgb ? srgb : Convert (srgb, CssColorSpace.Srgb, space);
        }

        public static SKColor ToSKColor (CssColor color, CssColorSpace space)
        {
            var srgb = GamutMapToSrgb (color, space);
            return new SKColor (Byte (srgb.C0 * 255), Byte (srgb.C1 * 255), Byte (srgb.C2 * 255), Byte (color.Alpha * 255));
        }

        private static byte Byte (double value) => (byte) MathCompat.Clamp (Math.Round (value), 0, 255);

        // ---- conversions --------------------------------------------------------------------------

        public static CssColor Convert (CssColor color, CssColorSpace from, CssColorSpace to)
        {
            if (from == to)
                return color;

            var (x, y, z) = ToXyzD65 (color, from);
            var result = FromXyzD65 (x, y, z, to);
            result.Alpha = color.Alpha;
            return result;
        }

        private static (double X, double Y, double Z) ToXyzD65 (CssColor c, CssColorSpace space)
        {
            switch (space) {
                case CssColorSpace.Srgb:
                    return Multiply (srgb_to_xyz, Linearize (c.C0), Linearize (c.C1), Linearize (c.C2));
                case CssColorSpace.SrgbLinear:
                    return Multiply (srgb_to_xyz, c.C0, c.C1, c.C2);
                case CssColorSpace.DisplayP3:
                    return Multiply (p3_to_xyz, Linearize (c.C0), Linearize (c.C1), Linearize (c.C2));
                case CssColorSpace.XyzD65:
                    return (c.C0, c.C1, c.C2);
                case CssColorSpace.XyzD50:
                    return Multiply (d50_to_d65, c.C0, c.C1, c.C2);
                case CssColorSpace.Lab:
                    return LabToXyzD65 (c.C0, c.C1, c.C2);
                case CssColorSpace.Lch: {
                    var (a, b) = Polar (c.C1, c.C2);
                    return LabToXyzD65 (c.C0, a, b);
                }
                case CssColorSpace.Oklab:
                    return OklabToXyz (c.C0, c.C1, c.C2);
                case CssColorSpace.Oklch: {
                    var (a, b) = Polar (c.C1, c.C2);
                    return OklabToXyz (c.C0, a, b);
                }
                case CssColorSpace.Hsl: {
                    var (r, g, b) = HslToSrgb (c.C0, c.C1, c.C2);
                    return Multiply (srgb_to_xyz, Linearize (r), Linearize (g), Linearize (b));
                }
                default: {
                    var (r, g, b) = HwbToSrgb (c.C0, c.C1, c.C2);
                    return Multiply (srgb_to_xyz, Linearize (r), Linearize (g), Linearize (b));
                }
            }
        }

        private static CssColor FromXyzD65 (double x, double y, double z, CssColorSpace space)
        {
            switch (space) {
                case CssColorSpace.Srgb: {
                    var (r, g, b) = Multiply (xyz_to_srgb, x, y, z);
                    return new CssColor (Gamma (r), Gamma (g), Gamma (b), 1);
                }
                case CssColorSpace.SrgbLinear: {
                    var (r, g, b) = Multiply (xyz_to_srgb, x, y, z);
                    return new CssColor (r, g, b, 1);
                }
                case CssColorSpace.DisplayP3: {
                    var (r, g, b) = Multiply (xyz_to_p3, x, y, z);
                    return new CssColor (Gamma (r), Gamma (g), Gamma (b), 1);
                }
                case CssColorSpace.XyzD65:
                    return new CssColor (x, y, z, 1);
                case CssColorSpace.XyzD50: {
                    var (a, b, c) = Multiply (d65_to_d50, x, y, z);
                    return new CssColor (a, b, c, 1);
                }
                case CssColorSpace.Lab: {
                    var (l, a, b) = XyzD65ToLab (x, y, z);
                    return new CssColor (l, a, b, 1);
                }
                case CssColorSpace.Lch: {
                    var (l, a, b) = XyzD65ToLab (x, y, z);
                    var (chroma, hue) = ToPolar (a, b, 0.0015);
                    return new CssColor (l, chroma, hue, 1);
                }
                case CssColorSpace.Oklab: {
                    var (l, a, b) = XyzToOklab (x, y, z);
                    return new CssColor (l, a, b, 1);
                }
                case CssColorSpace.Oklch: {
                    var (l, a, b) = XyzToOklab (x, y, z);
                    var (chroma, hue) = ToPolar (a, b, 0.0001);
                    return new CssColor (l, chroma, hue, 1);
                }
                case CssColorSpace.Hsl: {
                    var (r, g, b) = Multiply (xyz_to_srgb, x, y, z);
                    var (h, s, l) = SrgbToHsl (Gamma (r), Gamma (g), Gamma (b));
                    return new CssColor (h, s, l, 1);
                }
                default: {
                    var (r, g, b) = Multiply (xyz_to_srgb, x, y, z);
                    var (h, w, bl) = SrgbToHwb (Gamma (r), Gamma (g), Gamma (b));
                    return new CssColor (h, w, bl, 1);
                }
            }
        }

        // sRGB's transfer function, and its inverse; display-p3 shares it. Extended to negative values by symmetry, as CSS does.
        private static double Linearize (double v)
        {
            var abs = Math.Abs (v);
            var linear = abs <= 0.04045 ? abs / 12.92 : Math.Pow ((abs + 0.055) / 1.055, 2.4);
            return v < 0 ? -linear : linear;
        }

        private static double Gamma (double v)
        {
            var abs = Math.Abs (v);
            var encoded = abs > 0.0031308 ? 1.055 * Math.Pow (abs, 1 / 2.4) - 0.055 : 12.92 * abs;
            return v < 0 ? -encoded : encoded;
        }

        private static (double, double, double) Multiply (double[,] m, double a, double b, double c)
            => (m[0, 0] * a + m[0, 1] * b + m[0, 2] * c,
                m[1, 0] * a + m[1, 1] * b + m[1, 2] * c,
                m[2, 0] * a + m[2, 1] * b + m[2, 2] * c);

        private static readonly double[,] srgb_to_xyz = {
            { 506752.0 / 1228815, 87881.0 / 245763, 12673.0 / 70218 },
            { 87098.0 / 409605, 175762.0 / 245763, 12673.0 / 175545 },
            { 7918.0 / 409605, 87881.0 / 737289, 1001167.0 / 1053270 },
        };

        private static readonly double[,] xyz_to_srgb = Invert (srgb_to_xyz);

        private static readonly double[,] p3_to_xyz = {
            { 608311.0 / 1250200, 189793.0 / 714400, 198249.0 / 1000160 },
            { 35783.0 / 156275, 247089.0 / 357200, 198249.0 / 2500400 },
            { 0, 32229.0 / 714400, 5220557.0 / 5000800 },
        };

        private static readonly double[,] xyz_to_p3 = Invert (p3_to_xyz);

        // Bradford chromatic adaptation between the D65 white of sRGB and OKLab and the D50 white of CIE Lab.
        private static readonly double[,] d65_to_d50 = {
            { 1.0479297925449969, 0.022946870601609652, -0.05019226628920524 },
            { 0.02962780877005599, 0.9904344267538799, -0.017073799063418826 },
            { -0.009243040646204504, 0.015055191490298152, 0.7518742814281371 },
        };

        private static readonly double[,] d50_to_d65 = Invert (d65_to_d50);

        private static readonly double[] d50_white = { 0.3457 / 0.3585, 1.0, (1.0 - 0.3457 - 0.3585) / 0.3585 };

        private static (double, double, double) XyzD65ToLab (double x, double y, double z)
        {
            const double epsilon = 216.0 / 24389;
            const double kappa = 24389.0 / 27;
            var (dx, dy, dz) = Multiply (d65_to_d50, x, y, z);

            double F (double v) => v > epsilon ? Math.Pow (v, 1.0 / 3) : (kappa * v + 16) / 116;

            var fx = F (dx / d50_white[0]);
            var fy = F (dy / d50_white[1]);
            var fz = F (dz / d50_white[2]);

            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        private static (double, double, double) LabToXyzD65 (double l, double a, double b)
        {
            const double epsilon = 216.0 / 24389;
            const double kappa = 24389.0 / 27;

            var fy = (l + 16) / 116;
            var fx = a / 500 + fy;
            var fz = fy - b / 200;

            var x = Math.Pow (fx, 3) > epsilon ? Math.Pow (fx, 3) : (116 * fx - 16) / kappa;
            var y = l > kappa * epsilon ? Math.Pow ((l + 16) / 116, 3) : l / kappa;
            var z = Math.Pow (fz, 3) > epsilon ? Math.Pow (fz, 3) : (116 * fz - 16) / kappa;

            return Multiply (d50_to_d65, x * d50_white[0], y * d50_white[1], z * d50_white[2]);
        }

        // OKLab, through its linear-sRGB definition: XYZ -> linear sRGB -> LMS -> cube root -> Lab.
        private static (double, double, double) XyzToOklab (double x, double y, double z)
        {
            var (r, g, b) = Multiply (xyz_to_srgb, x, y, z);

            var l = Cbrt (0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
            var m = Cbrt (0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
            var s = Cbrt (0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);

            return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
        }

        private static (double, double, double) OklabToXyz (double L, double a, double b)
        {
            var l = L + 0.3963377774 * a + 0.2158037573 * b;
            var m = L - 0.1055613458 * a - 0.0638541728 * b;
            var s = L - 0.0894841775 * a - 1.2914855480 * b;

            l = l * l * l;
            m = m * m * m;
            s = s * s * s;

            return Multiply (srgb_to_xyz,
                4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
        }

        // Math.Cbrt is not on netstandard2.0.
        private static double Cbrt (double v) => v < 0 ? -Math.Pow (-v, 1.0 / 3) : Math.Pow (v, 1.0 / 3);

        // Rectangular to polar and back; a chroma below `powerless` has no meaningful hue, which is NaN.
        private static (double Chroma, double Hue) ToPolar (double a, double b, double powerless)
        {
            var chroma = Math.Sqrt (a * a + b * b);
            if (chroma < powerless)
                return (chroma, double.NaN);
            var hue = Math.Atan2 (b, a) * 180 / Math.PI;
            return (chroma, hue < 0 ? hue + 360 : hue);
        }

        private static (double A, double B) Polar (double chroma, double hue)
        {
            if (double.IsNaN (hue))
                return (0, 0);
            var radians = hue * Math.PI / 180;
            return (chroma * Math.Cos (radians), chroma * Math.Sin (radians));
        }

        // HSL and HWB, with saturation, lightness, whiteness and blackness as 0..100, the way CSS's channels are written.
        private static (double, double, double) HslToSrgb (double h, double s, double l)
        {
            h = double.IsNaN (h) ? 0 : ((h % 360) + 360) % 360;
            s /= 100;
            l /= 100;

            double F (double n)
            {
                var k = (n + h / 30) % 12;
                var a = s * Math.Min (l, 1 - l);
                return l - a * Math.Max (-1, Math.Min (Math.Min (k - 3, 9 - k), 1));
            }

            return (F (0), F (8), F (4));
        }

        private static (double, double, double) SrgbToHsl (double r, double g, double b)
        {
            var max = Math.Max (r, Math.Max (g, b));
            var min = Math.Min (r, Math.Min (g, b));
            var l = (min + max) / 2;
            var d = max - min;
            double h = double.NaN, s = 0;

            if (d > 1e-9) {
                s = l is 0 or 1 ? 0 : (max - l) / Math.Min (l, 1 - l);
                if (max == r)
                    h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g)
                    h = (b - r) / d + 2;
                else
                    h = (r - g) / d + 4;
                h *= 60;
            }

            return (h, s * 100, l * 100);
        }

        private static (double, double, double) HwbToSrgb (double h, double w, double b)
        {
            w /= 100;
            b /= 100;

            if (w + b >= 1) {
                var gray = w / (w + b);
                return (gray, gray, gray);
            }

            var (r, g, bl) = HslToSrgb (h, 100, 50);
            var scale = 1 - w - b;
            return (r * scale + w, g * scale + w, bl * scale + w);
        }

        private static (double, double, double) SrgbToHwb (double r, double g, double b)
        {
            var (h, _, _) = SrgbToHsl (r, g, b);
            var white = Math.Min (r, Math.Min (g, b));
            var black = 1 - Math.Max (r, Math.Max (g, b));
            return (h, white * 100, black * 100);
        }

        private static double[,] Invert (double[,] m)
        {
            var a = m[0, 0]; var b = m[0, 1]; var c = m[0, 2];
            var d = m[1, 0]; var e = m[1, 1]; var f = m[1, 2];
            var g = m[2, 0]; var h = m[2, 1]; var i = m[2, 2];

            var A = e * i - f * h;
            var B = -(d * i - f * g);
            var C = d * h - e * g;
            var det = a * A + b * B + c * C;

            return new[,] {
                { A / det, -(b * i - c * h) / det, (b * f - c * e) / det },
                { B / det, (a * i - c * g) / det, -(a * f - c * d) / det },
                { C / det, -(a * h - b * g) / det, (a * e - b * d) / det },
            };
        }

        // ---- gamut mapping (CSS Color 4, 13.2) ----------------------------------------------------

        private static bool InSrgbGamut (CssColor srgb)
        {
            const double epsilon = 0.000_1;
            return srgb.C0 >= -epsilon && srgb.C0 <= 1 + epsilon
                && srgb.C1 >= -epsilon && srgb.C1 <= 1 + epsilon
                && srgb.C2 >= -epsilon && srgb.C2 <= 1 + epsilon;
        }

        private static CssColor Clip (CssColor srgb)
            => new (MathCompat.Clamp (srgb.C0, 0, 1), MathCompat.Clamp (srgb.C1, 0, 1), MathCompat.Clamp (srgb.C2, 0, 1), srgb.Alpha);

        private static double DeltaEOK (CssColor a, CssColorSpace aSpace, CssColor b, CssColorSpace bSpace)
        {
            var x = Convert (a, aSpace, CssColorSpace.Oklab);
            var y = Convert (b, bSpace, CssColorSpace.Oklab);
            var dl = x.C0 - y.C0;
            var da = x.C1 - y.C1;
            var db = x.C2 - y.C2;
            return Math.Sqrt (dl * dl + da * da + db * db);
        }

        public static CssColor GamutMapToSrgb (CssColor color, CssColorSpace space)
        {
            var srgb = Convert (color, space, CssColorSpace.Srgb);

            if (InSrgbGamut (srgb))
                return Clip (srgb);

            var origin = Convert (color, space, CssColorSpace.Oklch);

            if (origin.C0 >= 1)
                return new CssColor (1, 1, 1, color.Alpha);
            if (origin.C0 <= 0)
                return new CssColor (0, 0, 0, color.Alpha);

            const double jnd = 0.02;
            const double epsilon = 0.000_1;

            var current = origin;
            var clipped = Clip (Convert (current, CssColorSpace.Oklch, CssColorSpace.Srgb));

            if (DeltaEOK (clipped, CssColorSpace.Srgb, current, CssColorSpace.Oklch) < jnd)
                return clipped;

            double min = 0, max = origin.C1;
            var minInGamut = true;

            while (max - min > epsilon) {
                var chroma = (min + max) / 2;
                current.C1 = chroma;
                var candidate = Convert (current, CssColorSpace.Oklch, CssColorSpace.Srgb);

                if (minInGamut && InSrgbGamut (candidate)) {
                    min = chroma;
                    continue;
                }

                clipped = Clip (candidate);
                var e = DeltaEOK (clipped, CssColorSpace.Srgb, current, CssColorSpace.Oklch);

                if (e < jnd) {
                    if (jnd - e < epsilon)
                        return clipped;
                    minInGamut = false;
                    min = chroma;
                } else {
                    max = chroma;
                }
            }

            return clipped;
        }
    }
}
