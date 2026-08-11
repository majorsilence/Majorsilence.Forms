using Xunit;

namespace Majorsilence.Forms.Tests;

// Application.SetDefaultFont was an empty stub, so an app-wide font choice was silently discarded and
// every unfonted control kept resolving to the platform default.
public class ApplicationDefaultFontTests
{
    [Fact]
    public void SetDefaultFont_ChangesTheAmbientDefault ()
    {
        var original = SystemFonts.DefaultFont;
        try {
            Application.SetDefaultFont (new Font ("Courier New", 11f));

            Assert.Equal ("Courier New", SystemFonts.DefaultFont.FamilyName);
            Assert.Equal (11f, SystemFonts.DefaultFont.Size);

            // A control with no explicit Font inherits it (Control.Font falls back to DefaultFont).
            using var control = new Label ();
            Assert.Equal ("Courier New", control.Font.FamilyName);
        } finally {
            Application.SetDefaultFont (null!);
            Assert.Equal (original.FamilyName, SystemFonts.DefaultFont.FamilyName);
        }
    }
}
