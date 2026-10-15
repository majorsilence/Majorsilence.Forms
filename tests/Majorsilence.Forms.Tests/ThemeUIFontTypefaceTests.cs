using System.Text;
using Majorsilence.Forms.Headless;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Theme.UIFont takes an SKTypeface, but text layout resolves a face from the family *name* (CachingFontMapper:
// the private font registry, then the system font manager). A typeface created from bundled bytes was never
// used where its family is not installed: text silently drew in the system UI font. Found by Recital, which
// bundles its UI font (FRAMEWORK_GAPS GAP-004).
[Collection ("Headless")]
public sealed class ThemeUIFontTypefaceTests
{
    // Liberation Mono renamed to a family no machine has, so the system font manager cannot find it by name.
    private static SKTypeface UninstalledMonospace ()
    {
        var assembly = typeof (Majorsilence.Forms.Drawing.Font).Assembly;
        var resource = assembly.GetManifestResourceNames ().Single (n => n.EndsWith ("LiberationMono-Regular.ttf", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream (resource)!;
        using var copy = new MemoryStream ();
        stream.CopyTo (copy);
        var bytes = copy.ToArray ();

        // The name table holds the family in single-byte and UTF-16BE records; same length, so offsets hold.
        Replace (bytes, Encoding.ASCII.GetBytes ("Liberation Mono"), Encoding.ASCII.GetBytes ("Xyzbundled Mono"));
        Replace (bytes, Encoding.BigEndianUnicode.GetBytes ("Liberation Mono"), Encoding.BigEndianUnicode.GetBytes ("Xyzbundled Mono"));

        var typeface = SKTypeface.FromData (SKData.CreateCopy (bytes));
        Assert.Equal ("Xyzbundled Mono", typeface.FamilyName);
        return typeface;
    }

    private static void Replace (byte[] haystack, byte[] needle, byte[] with)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan (i, needle.Length).SequenceEqual (needle))
                with.CopyTo (haystack, i);
    }

    private static float Width (string text) => TextMeasurer.MeasureText (text, Theme.UIFont, 12).Width;

    [Fact]
    public void A_bundled_typeface_is_the_one_text_is_laid_out_with ()
    {
        HeadlessRenderer.Use ();
        var original = Theme.UIFont;
        var bundled = UninstalledMonospace ();

        try {
            Theme.UIFont = bundled;

            // Monospace: a run of narrow letters is as wide as a run of wide ones. Whatever proportional
            // face the system substitutes makes "iiiiiiii" a fraction of "MMMMMMMM".
            Assert.Equal (Width ("MMMMMMMM"), Width ("iiiiiiii"), 0.5);
        } finally {
            Theme.UIFont = original;
        }
    }

    [Fact]
    public void Replacing_the_font_unregisters_the_bundled_one ()
    {
        HeadlessRenderer.Use ();
        var original = Theme.UIFont;
        var bundled = UninstalledMonospace ();

        Theme.UIFont = bundled;
        Assert.Same (bundled, Drawing.Text.PrivateFontRegistry.Resolve (bundled.FamilyName, bundled.FontStyle));

        Theme.UIFont = original;
        Assert.Null (Drawing.Text.PrivateFontRegistry.Resolve (bundled.FamilyName, bundled.FontStyle));
    }

    [Fact]
    public void An_installed_typeface_is_not_registered ()
    {
        HeadlessRenderer.Use ();
        var original = Theme.UIFont;
        var installed = SKFontManager.Default.MatchFamily (original.FamilyName) ?? original;

        try {
            Theme.UIFont = installed;
            Assert.Null (Drawing.Text.PrivateFontRegistry.Resolve (installed.FamilyName, installed.FontStyle));
        } finally {
            Theme.UIFont = original;
        }
    }
}
