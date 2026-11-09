using System;
using System.Collections.Concurrent;
using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Telerik
{
    /// <summary>
    /// The colours a <see cref="RadGridView"/> paints with under a named Telerik theme: the grid's own
    /// (<see cref="Grid"/>) and those of the bands only a RadGridView has.
    /// </summary>
    /// <remarks>
    /// Measured from Telerik UI for WinForms 2026.2.520's Fluent and FluentDark themes. Both are flat: one fill per
    /// part, grid lines under the rows and the header band but none between the cells, the selected row in the accent
    /// with white text and the current cell outlined 2px inside its edges.
    /// </remarks>
    internal sealed class RadGridThemePalette
    {
        /// <summary>The theme's own accent, Fluent's and FluentDark's alike.</summary>
        internal static readonly Color FluentAccent = Color.FromArgb (0x00, 0x99, 0xBC);

        // An app's recolouring of the theme's accent touches the colours in this hue range with at least this
        // saturation; an accent whose hue is already in it leaves the theme alone.
        private const float AccentHueMin = 160f;
        private const float AccentHueMax = 201f;
        private const float AccentMinSaturation = 0.15f;

        private static readonly ConcurrentDictionary<(bool Dark, int Accent), RadGridThemePalette> cache = new ();

        /// <summary>The grid's rows, header band, grid lines and selection.</summary>
        public DataGridViewThemePalette Grid { get; init; } = null!;

        /// <summary>The theme's accent, after any recolouring.</summary>
        public SKColor Accent { get; init; }

        /// <summary>Text on the accent (the current page's number).</summary>
        public SKColor AccentForeground { get; init; }

        /// <summary>The filtering row's fill.</summary>
        public SKColor FilterRowBackground { get; init; }

        /// <summary>The filtering row's operator label and value: the accent.</summary>
        public SKColor FilterRowForeground { get; init; }

        /// <summary>The filtering row's operator buttons' glyph while the column is not filtered.</summary>
        public SKColor FilterButtonForeground { get; init; }

        /// <summary>The group panel's fill.</summary>
        public SKColor GroupPanelBackground { get; init; }

        /// <summary>The paging panel's fill.</summary>
        public SKColor PagerBackground { get; init; }

        /// <summary>The paging panel's text and arrows.</summary>
        public SKColor PagerForeground { get; init; }

        /// <summary>The palette of the theme <paramref name="themeName"/>, recoloured to <paramref name="accent"/>, or null when this library does not paint that theme.</summary>
        public static RadGridThemePalette? For (string? themeName, Color accent)
        {
            bool dark;

            if (string.Equals (themeName, "Fluent", StringComparison.OrdinalIgnoreCase))
                dark = false;
            else if (string.Equals (themeName, "FluentDark", StringComparison.OrdinalIgnoreCase))
                dark = true;
            else
                return null;

            var key = (dark, accent.IsEmpty ? 0 : accent.ToArgb ());
            return cache.GetOrAdd (key, k => Create (k.Dark, Recolour (FluentAccent, accent)));
        }

        private static RadGridThemePalette Create (bool dark, Color accent)
        {
            var accent_sk = accent.ToSKColor ();

            return dark
                ? new RadGridThemePalette {
                    Grid = new DataGridViewThemePalette {
                        Background = SKColors.Black,
                        Foreground = SKColors.White,
                        HeaderBackground = SKColors.Black,
                        HeaderForeground = SKColors.White,
                        GridLine = new SKColor (0x4C, 0x4C, 0x4C),
                        AlternatingRow = new SKColor (0x2B, 0x2B, 0x2B),
                        HoverRow = new SKColor (0x4C, 0x4C, 0x4C),
                        SelectionBackground = accent_sk,
                        SelectionForeground = SKColors.White,
                        CurrentCellBorder = SKColors.White,
                    },
                    Accent = accent_sk,
                    AccentForeground = SKColors.White,
                    FilterRowBackground = new SKColor (0x2B, 0x2B, 0x2B),
                    FilterRowForeground = accent_sk,
                    FilterButtonForeground = SKColors.White,
                    GroupPanelBackground = new SKColor (0x33, 0x33, 0x33),
                    PagerBackground = SKColors.Black,
                    PagerForeground = SKColors.White,
                }
                : new RadGridThemePalette {
                    Grid = new DataGridViewThemePalette {
                        Background = SKColors.White,
                        Foreground = SKColors.Black,
                        HeaderBackground = SKColors.White,
                        HeaderForeground = SKColors.Black,
                        GridLine = new SKColor (0xCC, 0xCC, 0xCC),
                        AlternatingRow = new SKColor (0xF2, 0xF2, 0xF2),
                        HoverRow = new SKColor (0xC7, 0xC7, 0xC7),
                        SelectionBackground = accent_sk,
                        SelectionForeground = SKColors.White,
                        CurrentCellBorder = new SKColor (0x33, 0x33, 0x33),
                    },
                    Accent = accent_sk,
                    AccentForeground = SKColors.White,
                    FilterRowBackground = new SKColor (0xF2, 0xF2, 0xF2),
                    FilterRowForeground = accent_sk,
                    FilterButtonForeground = new SKColor (0x33, 0x33, 0x33),
                    GroupPanelBackground = new SKColor (0xE6, 0xE6, 0xE6),
                    PagerBackground = SKColors.White,
                    PagerForeground = SKColors.Black,
                };
        }

        /// <summary>
        /// A theme accent colour moved to <paramref name="accent"/>'s hue, keeping its own saturation and lightness;
        /// unchanged when there is no accent, when the colour is not in the accent family, or when the accent's hue is
        /// already in it.
        /// </summary>
        internal static Color Recolour (Color colour, Color accent)
        {
            if (accent.IsEmpty)
                return colour;

            var accent_hue = accent.GetHue ();
            if (accent_hue >= AccentHueMin && accent_hue <= AccentHueMax)
                return colour;

            if (colour.A == 0 || (colour.R == colour.G && colour.G == colour.B))
                return colour;

            var hue = colour.GetHue ();
            if (hue < AccentHueMin || hue > AccentHueMax || colour.GetSaturation () < AccentMinSaturation)
                return colour;

            return FromHsl (accent_hue, colour.GetSaturation (), colour.GetBrightness (), colour.A);
        }

        private static Color FromHsl (float h, float s, float l, int alpha)
        {
            if (s <= 0f) {
                var grey = (int) Math.Round (l * 255f);
                return Color.FromArgb (alpha, grey, grey, grey);
            }

            var chroma = (1f - Math.Abs (2f * l - 1f)) * s;
            var hp = h / 60f;
            var x = chroma * (1f - Math.Abs ((hp % 2f) - 1f));
            float r = 0f, g = 0f, b = 0f;

            if (hp < 1f) { r = chroma; g = x; }
            else if (hp < 2f) { r = x; g = chroma; }
            else if (hp < 3f) { g = chroma; b = x; }
            else if (hp < 4f) { g = x; b = chroma; }
            else if (hp < 5f) { r = x; b = chroma; }
            else { r = chroma; b = x; }

            var m = l - chroma / 2f;
            return Color.FromArgb (alpha, (int) Math.Round ((r + m) * 255f), (int) Math.Round ((g + m) * 255f), (int) Math.Round ((b + m) * 255f));
        }
    }
}
