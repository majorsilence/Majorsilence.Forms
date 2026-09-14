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
            // red; HLS halves the luminosity and keeps it red.
            Assert.Equal (Color.FromArgb (255, 128, 0, 0).ToArgb (), ControlPaint.Dark (Color.Red).ToArgb ());
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
            Assert.True (ControlPaint.Light (SystemColors.Control, 0f).GetBrightness () < SystemColors.Control.GetBrightness ());
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
