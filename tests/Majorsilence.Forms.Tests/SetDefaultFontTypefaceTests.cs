using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Application.SetDefaultFont changed what Control.Font reported, but GetEffectiveFont -- the typeface
    // TreeView, ListView and most renderers draw with -- fell back to the built-in system face regardless,
    // so a tree drew in the machine's sans-serif under an app-wide default font. Found by Recital's UI
    // tests, whose tree rendered differently on each Linux machine (FRAMEWORK_GAPS GAP-011).
    [Collection ("Headless")]
    public class SetDefaultFontTypefaceTests
    {
        [Fact]
        public void An_app_default_font_is_the_typeface_controls_draw_with ()
        {
            HeadlessRenderer.Use ();
            var font = new Majorsilence.Forms.Drawing.Font ("Courier New", 10);

            try {
                Application.SetDefaultFont (font);
                var tree = new TreeView ();

                Assert.Equal (font.GetSKTypeface ().FamilyName, tree.GetEffectiveFont ().FamilyName);
            } finally {
                SystemFonts.SetDefaultFont (null);
            }
        }
    }
}
