using System.Drawing;
using SkiaSharp;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// The colours this library paints system colours with, and the one place a <see cref="Color"/>
    /// becomes an <see cref="SKColor"/>.
    /// </summary>
    /// <remarks>
    /// GFX-39: <c>SystemColors</c> members are real known colours (<c>Color.FromKnownColor</c>), as
    /// upstream's are, so they keep their identity -- <c>IsSystemColor</c>, <c>Name</c>,
    /// <c>ToKnownColor ()</c>, and equality with a designer- or resx-deserialised <c>KnownColor</c>.
    /// But a known colour's channels come from the BCL's table, which off Windows is the Windows XP
    /// palette (<c>Control</c> = 236,233,216) and on Windows is the live OS scheme. Neither is what
    /// this library draws: a system colour is resolved here, at draw time, to the light Windows 10
    /// defaults below, on every platform. Anything that turns a colour into pixels, or does arithmetic
    /// on its channels for pixels (shading, blending), must come through <see cref="Resolve"/> or
    /// <see cref="ToSKColor"/>; <c>SystemColorPaletteGuardTests</c> fails on a direct conversion.
    /// </remarks>
    internal static class SystemColorPalette
    {
        /// <summary>
        /// Returns the colour to paint for <paramref name="color"/>: this palette's value for a system
        /// colour (as an anonymous ARGB colour), otherwise <paramref name="color"/> itself.
        /// </summary>
        public static Color Resolve (Color color)
            => TryGetPaletteArgb (color, out var argb) ? Color.FromArgb (argb) : color;

        /// <summary>Converts a colour to the <see cref="SKColor"/> to paint, resolving system colours.</summary>
        public static SKColor ToSKColor (Color color)
            => TryGetPaletteArgb (color, out var argb)
                ? new SKColor ((uint)argb)
                : new SKColor (color.R, color.G, color.B, color.A);

        /// <summary>Whether <paramref name="color"/> is a system colour this palette has a value for.</summary>
        public static bool IsSystemColor (Color color) => TryGetPaletteArgb (color, out _);

        private static bool TryGetPaletteArgb (Color color, out int argb)
        {
            argb = 0;

            // Only a system colour carries a name this table knows. netstandard2.0's facade has neither
            // IsSystemColor nor KnownColor, so that build goes by name alone -- a system colour's name is
            // its KnownColor member's, and no web colour shares one.
#if NETSTANDARD2_0
            if (!color.IsNamedColor)
                return false;
#else
            if (!color.IsSystemColor)
                return false;
#endif

            var rgb = PaletteRgb (color.Name);

            if (rgb < 0)
                return false;

            argb = unchecked ((int)0xFF000000) | rgb;
            return true;
        }

        // The light Windows 10 defaults SystemColors carried as literals before GFX-39, one per system
        // KnownColor (all 33 of them). -1 for any other name.
        private static int PaletteRgb (string name) => name switch {
            "ActiveBorder" => 0xB4B4B4,
            "ActiveCaption" => 0x0078D7,
            "ActiveCaptionText" => 0xFFFFFF,
            "AppWorkspace" => 0xABABAB,
            "ButtonFace" => 0xF0F0F0,
            "ButtonHighlight" => 0xFFFFFF,
            "ButtonShadow" => 0xA0A0A0,
            "Control" => 0xF0F0F0,
            "ControlDark" => 0xA0A0A0,
            "ControlDarkDark" => 0x696969,
            "ControlLight" => 0xE3E3E3,
            "ControlLightLight" => 0xFFFFFF,
            "ControlText" => 0x000000,
            "Desktop" => 0x000000,
            "GradientActiveCaption" => 0xB9D1EA,
            "GradientInactiveCaption" => 0xD7E4F2,
            "GrayText" => 0x6D6D6D,
            "Highlight" => 0x0078D7,
            "HighlightText" => 0xFFFFFF,
            "HotTrack" => 0x0066CC,
            "InactiveBorder" => 0xF4F7FC,
            "InactiveCaption" => 0xBFCDDB,
            "InactiveCaptionText" => 0x434E54,
            "Info" => 0xFFFFE1,
            "InfoText" => 0x000000,
            "Menu" => 0xF0F0F0,
            "MenuBar" => 0xF0F0F0,
            "MenuHighlight" => 0x0078D7,
            "MenuText" => 0x000000,
            "ScrollBar" => 0xC8C8C8,
            "Window" => 0xFFFFFF,
            "WindowFrame" => 0x646464,
            "WindowText" => 0x000000,
            _ => -1,
        };
    }
}
