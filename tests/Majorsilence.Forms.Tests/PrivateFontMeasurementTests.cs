using System.IO;
using Majorsilence.Forms.Drawing.Text;
using SkiaSharp;
using Xunit;

namespace Majorsilence.Forms.Tests;

// The paint path resolves a Font's typeface through Font.GetSKTypeface(), which honours families
// loaded at runtime via PrivateFontCollection. The measuring path used to look the family name up in
// the system font manager instead, so a privately-loaded font was DRAWN correctly but MEASURED with
// the system fallback face. Nothing threw -- the text was just laid out to the wrong width, which
// shows up as clipping or bad centring. The same lookup also dropped the font's slant.
public class PrivateFontMeasurementTests
{
    private const string Sample = "Measuring private font metrics";

    // Caladea ships with the library and is not normally installed on a host, so a Font naming it
    // only resolves because the collection loaded it -- exactly the case that used to be missed.
    private static string PrivateFontFile ()
    {
        var file = Path.Combine (Majorsilence.Forms.Drawing.FontResourceLoader.GetFontDirectory (), "Caladea-Regular.ttf");
        Assert.True (File.Exists (file), $"expected the bundled font at {file}");
        return file;
    }

    private static PrivateFontCollection LoadPrivate ()
    {
        var collection = new PrivateFontCollection ();
        collection.AddFontFile (PrivateFontFile ());
        Assert.NotEmpty (collection.Families);
        return collection;
    }

    [Fact]
    public void Resolve_UsesTheFamilyLoadedAtRuntime ()
    {
        using var collection = LoadPrivate ();
        var family = collection.Families[0].Name;

        using var font = new Font (family, 14f);

        Assert.Equal (family, TypefaceCache.Resolve (font).FamilyName);
    }

    [Fact]
    public void Resolve_CarriesItalicSlant ()
    {
        // The old lookup passed weight only, so an italic Font measured as upright.
        using var font = new Font ("Arial", 14f, FontStyle.Italic);

        Assert.Equal (SKFontStyleSlant.Italic, TypefaceCache.Resolve (font).FontSlant);
    }

    [Fact]
    public void TextRendererMeasureText_UsesPrivateFontMetrics ()
    {
        using var collection = LoadPrivate ();

        using var privateFont = new Font (collection.Families[0].Name, 14f);
        // A family that cannot resolve anywhere, so this measures the system fallback face. If the
        // private font were being ignored, both measurements would come from that same face.
        using var fallbackFont = new Font ("No Such Family 4E5A9C", 14f);

        var measured = TextRenderer.MeasureText (Sample, privateFont);
        var fallback = TextRenderer.MeasureText (Sample, fallbackFont);

        Assert.True (measured.Width > 0, "measured nothing");
        Assert.NotEqual (fallback.Width, measured.Width);
    }

    [Fact]
    public void GraphicsMeasureString_UsesPrivateFontMetrics ()
    {
        using var collection = LoadPrivate ();
        using var surface = new Bitmap (10, 10);
        using var g = Graphics.FromImage (surface);

        using var privateFont = new Font (collection.Families[0].Name, 14f);
        using var fallbackFont = new Font ("No Such Family 4E5A9C", 14f);

        var measured = g.MeasureString (Sample, privateFont);
        var fallback = g.MeasureString (Sample, fallbackFont);

        Assert.True (measured.Width > 0, "measured nothing");
        Assert.NotEqual (fallback.Width, measured.Width);
    }

    [Fact]
    public void MeasuringInstallsTheCachingFontMapper ()
    {
        // A TextBlock resolves its family through FontMapper.Default, so everything above depends on
        // the library's mapper being installed. It used to be installed only by Theme's static
        // constructor, which a pure measuring path never triggers -- leaving RichTextKit's built-in
        // mapper, which knows nothing about PrivateFontCollection.
        TextRenderer.MeasureText ("trigger", new Font ("Arial", 12f));

        Assert.IsType<CachingFontMapper> (Topten.RichTextKit.FontMapper.Default);
    }
}
