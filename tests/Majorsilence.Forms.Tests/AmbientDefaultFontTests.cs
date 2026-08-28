using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: a Control whose Font is never set (and whose ancestors never set one
    // either) falls all the way back to the ambient system default -- real WinForms uses
    // Control.DefaultFont / SystemFonts.DefaultFont ("Microsoft Sans Serif, 8.25pt" on Windows),
    // NOT the active Majorsilence.Forms theme's chrome font. Conflating the two meant an unfonted
    // AutoSize=True Label rendered at Theme.FontSize (14pt) while its designer-frozen Size (baked
    // in assuming 8.25pt at design time) stayed small, clipping the text.
    public class AmbientDefaultFontTests
    {
        [Fact]
        public void Unfonted_control_falls_back_to_SystemFonts_DefaultFont ()
        {
            using var label = new Label ();

            Assert.Equal (Majorsilence.Forms.SystemFonts.DefaultFont.Name, label.Font.Name);
            Assert.Equal (Majorsilence.Forms.SystemFonts.DefaultFont.Size, label.Font.Size);
        }

        [Fact]
        public void Unfonted_child_inherits_parents_explicit_Font_not_the_theme_default ()
        {
            using var panel = new Panel { Font = new Majorsilence.Forms.Drawing.Font ("Microsoft Sans Serif", 12F) };
            var label = new Label ();
            panel.Controls.Add (label);

            Assert.Equal (12F, label.Font.Size);
        }

        [Fact]
        public void GetEffectiveFontSize_is_the_default_fonts_PIXEL_size_when_unfonted ()
        {
            // This used to assert (int) SystemFonts.DefaultFontSize, i.e. 8. That number is in
            // POINTS, but every caller of GetEffectiveFontSize feeds it to the renderers as a
            // PIXEL size -- so the fix for "unfonted controls wrongly use the 14px theme font"
            // overshot from too big to too small, and unfonted controls (which is nearly all of
            // them) drew at 8px instead of 11px. The default font is still the right answer; the
            // units were not.
            using var label = new Label ();

            var expected = (int) System.Math.Round (Majorsilence.Forms.SystemFonts.DefaultFont.PixelSize);

            Assert.Equal (expected, label.GetEffectiveFontSize ());
            Assert.NotEqual ((int) Majorsilence.Forms.SystemFonts.DefaultFontSize, label.GetEffectiveFontSize ());
        }

        [Fact]
        public void SystemFonts_DefaultFont_is_not_the_theme_chrome_font ()
        {
            // Guards against re-conflating the two: DefaultFont must stay decoupled from Theme.
            Assert.NotEqual (Theme.FontSize, (int) Majorsilence.Forms.SystemFonts.DefaultFontSize);
        }
    }
}
