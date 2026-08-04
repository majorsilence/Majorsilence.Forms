using Majorsilence.Forms.Drawing;
using Xunit;

using Color = System.Drawing.Color;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// Phase 8 of docs/gdi-gap-plan.md: the small real gaps left after the big phases — the gradient
    /// caption system colors, <c>SystemFonts.GetFontByName</c>, the <c>ColorTranslator</c> Win32/OLE
    /// conversions, and the <c>Font</c> metadata properties.
    /// </summary>
    public class SystemResourcesAndColorTranslatorTests
    {
        [Fact]
        public void Gradient_caption_colors_exist_across_colors_brushes_and_pens ()
        {
            Assert.NotEqual (Color.Empty, SystemColors.GradientActiveCaption);
            Assert.NotEqual (Color.Empty, SystemColors.GradientInactiveCaption);

            Assert.Equal (SystemColors.GradientActiveCaption, SystemBrushes.GradientActiveCaption.Color);
            Assert.Equal (SystemColors.GradientInactiveCaption, SystemBrushes.GradientInactiveCaption.Color);
            Assert.Equal (SystemColors.GradientActiveCaption, SystemPens.GradientActiveCaption.Color);
            Assert.Equal (SystemColors.GradientInactiveCaption, SystemPens.GradientInactiveCaption.Color);
        }

        [Fact]
        public void System_brushes_and_pens_stay_cached_singletons ()
        {
            // System.Drawing's are process-wide singletons that must not be disposed; the gradient
            // additions have to follow the same rule as the rest of the class.
            Assert.Same (SystemBrushes.GradientActiveCaption, SystemBrushes.GradientActiveCaption);
            Assert.Same (SystemPens.GradientInactiveCaption, SystemPens.GradientInactiveCaption);
        }

        [Theory]
        [InlineData ("MenuFont")]
        [InlineData ("CaptionFont")]
        [InlineData ("MessageBoxFont")]
        [InlineData ("DefaultFont")]
        public void GetFontByName_returns_the_named_system_font (string name)
        {
            var font = SystemFonts.GetFontByName (name);

            Assert.NotNull (font);
            Assert.Equal (name, font!.SystemFontName);
            Assert.True (font.IsSystemFont);
        }

        [Fact]
        public void GetFontByName_returns_null_for_an_unknown_name ()
            => Assert.Null (SystemFonts.GetFontByName ("NoSuchFont"));

        [Fact]
        public void A_font_not_from_SystemFonts_is_not_a_system_font ()
        {
            using var font = new Font ("Arial", 10f);

            Assert.False (font.IsSystemFont);
            Assert.Equal (string.Empty, font.SystemFontName);
            Assert.Equal ("Arial", font.OriginalFontName);
            Assert.False (font.GdiVerticalFont);
        }

        [Theory]
        // COLORREF packs as 0x00BBGGRR, so the byte order is the reverse of the usual RGB reading.
        [InlineData (255, 0, 0, 0x0000FF)]
        [InlineData (0, 255, 0, 0x00FF00)]
        [InlineData (0, 0, 255, 0xFF0000)]
        [InlineData (18, 52, 86, 0x563412)]
        public void ToWin32_packs_the_channels_as_BGR (int r, int g, int b, int expected)
            => Assert.Equal (expected, ColorTranslator.ToWin32 (Color.FromArgb (r, g, b)));

        [Fact]
        public void Win32_and_Ole_colors_round_trip ()
        {
            var color = Color.FromArgb (18, 52, 86);

            Assert.Equal (color.ToArgb (), ColorTranslator.FromWin32 (ColorTranslator.ToWin32 (color)).ToArgb ());
            Assert.Equal (color.ToArgb (), ColorTranslator.FromOle (ColorTranslator.ToOle (color)).ToArgb ());
            // OLE and Win32 use the same packing, so the two must agree.
            Assert.Equal (ColorTranslator.ToWin32 (color), ColorTranslator.ToOle (color));
        }

        [Fact]
        public void FromWin32_produces_an_opaque_color ()
        {
            // COLORREF carries no alpha; the result must be fully opaque rather than transparent.
            Assert.Equal (255, ColorTranslator.FromWin32 (0x563412).A);
        }
    }
}
