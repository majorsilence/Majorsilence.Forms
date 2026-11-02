using System.Drawing;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // GFX-02: ControlPaint.Light/Dark/LightLight/DarkDark took the percentage as 0-100 and added it
    // linearly to each RGB channel.
    //
    // Two independent defects. The documented call -- ControlPaint.Light (c, 0.5f) -- moved each channel
    // by +1, a visually identical colour, so every hand-rolled bevel collapsed to flat. And linear RGB
    // addition desaturates towards white or black instead of moving luminosity, so the shade was the
    // wrong colour as well as the wrong amount.
    //
    // Pure colour maths: no display, no rendering, exact values from the Win32 HLS algorithm.
    public class ControlPaintColorTests
    {
        [Fact]
        public void Dark_keeps_the_hue_instead_of_washing_it_out ()
        {
            // The finding's own value. Linear subtraction gave (230,0,0) -- barely distinguishable from
            // red; HLS moves the luminosity and keeps it red, at upstream's (85,0,0).
            Assert.Equal (Color.FromArgb (255, 85, 0, 0).ToArgb (), ControlPaint.Dark (Color.Red).ToArgb ());
        }

        // Upstream's shades, measured from WinForms (.NET 10, Windows 11): an ordinary colour darkens from one
        // shadow step (two-thirds of its luminosity) and lightens towards one highlight step; SystemColors.Control
        // goes from ControlDark/ControlLight to ControlDarkDark/ControlLightLight in a straight line. The last row is
        // SystemColors.Control.
        public static TheoryData<uint, string, uint> UpstreamShades => new () {
            { 0xFFE9F0F9u, "Dark0", 0xFF6A98D7u },
            { 0xFFE9F0F9u, "Dark25", 0xFF346FBEu },
            { 0xFFE9F0F9u, "Dark", 0xFF234A7Eu },
            { 0xFFE9F0F9u, "Dark1", 0xFF000000u },
            { 0xFFE9F0F9u, "DarkDark", 0xFF000000u },
            { 0xFFE9F0F9u, "Light0", 0xFFEAF0F9u },
            { 0xFFE9F0F9u, "Light25", 0xFFEBF1FAu },
            { 0xFFE9F0F9u, "Light", 0xFFEEF3FBu },
            { 0xFFE9F0F9u, "LightLight", 0xFFF5F9FCu },
            { 0xFFFFFFFFu, "Dark0", 0xFFAAAAAAu },
            { 0xFFFFFFFFu, "Dark25", 0xFF7F7F7Fu },
            { 0xFFFFFFFFu, "Dark", 0xFF555555u },
            { 0xFFFFFFFFu, "Dark1", 0xFF000000u },
            { 0xFFFFFFFFu, "DarkDark", 0xFF000000u },
            { 0xFFFFFFFFu, "Light0", 0xFFFFFFFFu },
            { 0xFFFFFFFFu, "Light25", 0xFFFFFFFFu },
            { 0xFFFFFFFFu, "Light", 0xFFFFFFFFu },
            { 0xFFFFFFFFu, "LightLight", 0xFFFFFFFFu },
            { 0xFFFF0000u, "Dark0", 0xFFAA0000u },
            { 0xFFFF0000u, "Dark25", 0xFF800000u },
            { 0xFFFF0000u, "Dark", 0xFF550000u },
            { 0xFFFF0000u, "Dark1", 0xFF000000u },
            { 0xFFFF0000u, "DarkDark", 0xFF000000u },
            { 0xFFFF0000u, "Light0", 0xFFFF0000u },
            { 0xFFFF0000u, "Light25", 0xFFFF2020u },
            { 0xFFFF0000u, "Light", 0xFFFF4040u },
            { 0xFFFF0000u, "LightLight", 0xFFFF8080u },
            { 0xFF000080u, "Dark0", 0xFF000055u },
            { 0xFF000080u, "Dark25", 0xFF000040u },
            { 0xFF000080u, "Dark", 0xFF00002Bu },
            { 0xFF000080u, "Dark1", 0xFF000000u },
            { 0xFF000080u, "DarkDark", 0xFF000000u },
            { 0xFF000080u, "Light0", 0xFF000080u },
            { 0xFF000080u, "Light25", 0xFF0000AEu },
            { 0xFF000080u, "Light", 0xFF0000DFu },
            { 0xFF000080u, "LightLight", 0xFF4040FFu },
            { 0xFF804020u, "Dark0", 0xFF552B15u },
            { 0xFF804020u, "Dark25", 0xFF412010u },
            { 0xFF804020u, "Dark", 0xFF2B150Bu },
            { 0xFF804020u, "Dark1", 0xFF000000u },
            { 0xFF804020u, "DarkDark", 0xFF000000u },
            { 0xFF804020u, "Light0", 0xFF803F20u },
            { 0xFF804020u, "Light25", 0xFFA25028u },
            { 0xFF804020u, "Light", 0xFFC66231u },
            { 0xFF804020u, "LightLight", 0xFFDC9674u },
            { 0xFFC8C8C8u, "Dark0", 0xFF848484u },
            { 0xFFC8C8C8u, "Dark25", 0xFF636363u },
            { 0xFFC8C8C8u, "Dark", 0xFF424242u },
            { 0xFFC8C8C8u, "Dark1", 0xFF000000u },
            { 0xFFC8C8C8u, "DarkDark", 0xFF000000u },
            { 0xFFC8C8C8u, "Light0", 0xFFC7C7C7u },
            { 0xFFC8C8C8u, "Light25", 0xFFCECECEu },
            { 0xFFC8C8C8u, "Light", 0xFFD5D5D5u },
            { 0xFFC8C8C8u, "LightLight", 0xFFE3E3E3u },
            { 0xFFF0F0F0u, "Dark0", 0xFFA0A0A0u },
            { 0xFFF0F0F0u, "Dark25", 0xFF939393u },
            { 0xFFF0F0F0u, "Dark", 0xFF858585u },
            { 0xFFF0F0F0u, "Dark1", 0xFF696969u },
            { 0xFFF0F0F0u, "DarkDark", 0xFF696969u },
            { 0xFFF0F0F0u, "Light0", 0xFFE3E3E3u },
            { 0xFFF0F0F0u, "Light25", 0xFFEAEAEAu },
            { 0xFFF0F0F0u, "Light", 0xFFF1F1F1u },
            { 0xFFF0F0F0u, "LightLight", 0xFFFFFFFFu },
        };

        [Theory]
        [MemberData (nameof (UpstreamShades))]
        public void A_shade_is_upstream_s (uint argb, string shade, uint expected)
        {
            var c = argb == 0xFFF0F0F0u ? SystemColors.Control : Color.FromArgb ((int) argb);

            var actual = shade switch {
                "Dark0" => ControlPaint.Dark (c, 0f),
                "Dark25" => ControlPaint.Dark (c, 0.25f),
                "Dark" => ControlPaint.Dark (c),
                "Dark1" => ControlPaint.Dark (c, 1f),
                "DarkDark" => ControlPaint.DarkDark (c),
                "Light0" => ControlPaint.Light (c, 0f),
                "Light25" => ControlPaint.Light (c, 0.25f),
                "Light" => ControlPaint.Light (c),
                _ => ControlPaint.LightLight (c),
            };

            Assert.Equal (expected.ToString ("X8"), ((uint) Majorsilence.Forms.Drawing.SystemColorPalette.Resolve (actual).ToArgb ()).ToString ("X8"));
        }

        [Fact]
        public void The_control_colours_shades_are_the_exact_system_values ()
        {
            // The short-circuit that makes a classic 3D bevel on a default-coloured control match the
            // platform's: these are not computed shades, they are the exact system values -- and
            // ControlLight is DARKER than Control, which no amount of "add to each channel" produces.
            //
            // At the ENDS of the range. GFX-02's suggested test asserts this of Dark (Control) too, but
            // upstream maps the one-argument Dark to Darker (0.5f), which takes the interpolating branch
            // between one and two shadow steps -- only Darker (0f) short-circuits. The finding's
            // shorthand, not upstream's contract; asserted here as upstream actually defines it.
            Assert.Equal (SystemColors.ControlDark.ToArgb (), ControlPaint.Dark (SystemColors.Control, 0f).ToArgb ());
            Assert.Equal (SystemColors.ControlDarkDark.ToArgb (), ControlPaint.DarkDark (SystemColors.Control).ToArgb ());
            Assert.Equal (SystemColors.ControlLight.ToArgb (), ControlPaint.Light (SystemColors.Control, 0f).ToArgb ());
            Assert.Equal (SystemColors.ControlLightLight.ToArgb (), ControlPaint.LightLight (SystemColors.Control).ToArgb ());
        }

        [Fact]
        public void Light_of_the_control_colour_is_darker_than_the_control_colour ()
        {
            // The counter-intuitive consequence of the system short-circuit, and the one a linear
            // "add to each channel" implementation can never produce: SystemColors.ControlLight is
            // darker than SystemColors.Control, so lightening the control colour moves it DOWN.
            // Compared as painted: a system colour's own channels are the runtime's table (GFX-39).
            static float Brightness (Color c) => Majorsilence.Forms.Drawing.SystemColorPalette.Resolve (c).GetBrightness ();

            Assert.True (Brightness (ControlPaint.Light (SystemColors.Control, 0f)) < Brightness (SystemColors.Control));
        }

        [Fact]
        public void The_percentage_is_a_fraction_not_a_hundredth ()
        {
            // ControlPaint.Light (c, 0.5f) is the documented form. Read as a 0-100 percentage it moved
            // each channel by one, so the result was the input.
            var lightened = ControlPaint.Light (Color.FromArgb (100, 100, 100), 0.5f);

            Assert.NotEqual (Color.FromArgb (100, 100, 100).ToArgb (), lightened.ToArgb ());
            Assert.True (lightened.R > 120, $"0.5f should lighten substantially, got {lightened}");
        }

        [Fact]
        public void A_larger_fraction_lightens_further ()
        {
            var half = ControlPaint.Light (Color.FromArgb (60, 60, 60), 0.5f);
            var full = ControlPaint.Light (Color.FromArgb (60, 60, 60), 1.0f);

            Assert.True (full.R > half.R, $"1.0f ({full.R}) should exceed 0.5f ({half.R})");
        }

        [Fact]
        public void LightLight_and_DarkDark_bracket_Light_and_Dark ()
        {
            var colour = Color.FromArgb (120, 90, 60);

            Assert.True (ControlPaint.LightLight (colour).GetBrightness () > ControlPaint.Light (colour).GetBrightness ());
            Assert.True (ControlPaint.DarkDark (colour).GetBrightness () < ControlPaint.Dark (colour).GetBrightness ());
        }

        [Fact]
        public void A_grey_stays_grey ()
        {
            // GUARD, not proof: linear addition also kept greys grey. It pins that the HLS round-trip
            // does not introduce a colour cast where there was no hue to preserve.
            var lighter = ControlPaint.Light (Color.FromArgb (128, 128, 128));

            Assert.Equal (lighter.R, lighter.G);
            Assert.Equal (lighter.G, lighter.B);
        }

        [Fact]
        public void Black_and_white_do_not_overflow ()
        {
            // GUARD, not proof: the old code clamped too. It pins that the integer maths stays in range
            // at the extremes, where a 0-240 luminosity scale is easiest to push out of bounds.
            var lighter = ControlPaint.LightLight (Color.Black);
            var darker = ControlPaint.DarkDark (Color.White);

            Assert.InRange (lighter.R, 0, 255);
            Assert.InRange (darker.R, 0, 255);
        }
    }
}
