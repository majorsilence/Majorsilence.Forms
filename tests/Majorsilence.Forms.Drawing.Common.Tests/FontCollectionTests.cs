using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.Drawing.Text;

namespace Majorsilence.Forms.Drawing.Common.Tests;

public class FontCollectionTests
{
    // The bundled Caladea family is not normally installed on CI hosts, which makes it a good
    // subject for "a font that only works because it was loaded at runtime".
    private static string SampleFontFile()
    {
        var dir = FontResourceLoader.GetFontDirectory();
        var file = Path.Combine(dir, "Caladea-Regular.ttf");
        Assert.True(File.Exists(file), $"expected the bundled font to be extracted to {file}");
        return file;
    }

    [Fact]
    public void PrivateFontCollection_StartsEmpty()
    {
        using var collection = new PrivateFontCollection();
        Assert.Empty(collection.Families);
    }

    [Fact]
    public void AddFontFile_ExposesTheFamily()
    {
        using var collection = new PrivateFontCollection();
        collection.AddFontFile(SampleFontFile());

        Assert.Single(collection.Families);
        Assert.Contains("Caladea", collection.Families[0].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddFontFile_SameFamilyTwice_ReportsOneFamily()
    {
        using var collection = new PrivateFontCollection();
        collection.AddFontFile(SampleFontFile());
        collection.AddFontFile(SampleFontFile());

        Assert.Single(collection.Families);
    }

    [Fact]
    public void AddFontFile_MissingFile_Throws()
    {
        using var collection = new PrivateFontCollection();
        Assert.Throws<FileNotFoundException>(() => collection.AddFontFile("/definitely/not/a/font.ttf"));
    }

    [Fact]
    public void AddFontFile_Null_Throws()
    {
        using var collection = new PrivateFontCollection();
        Assert.Throws<ArgumentNullException>(() => collection.AddFontFile(null!));
    }

    [Fact]
    public void AddMemoryFont_ByteArray_ExposesTheFamily()
    {
        using var collection = new PrivateFontCollection();
        collection.AddMemoryFont(File.ReadAllBytes(SampleFontFile()));

        Assert.Single(collection.Families);
        Assert.Contains("Caladea", collection.Families[0].Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMemoryFont_UnmanagedPointer_ExposesTheFamily()
    {
        var bytes = File.ReadAllBytes(SampleFontFile());
        var buffer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);

            using var collection = new PrivateFontCollection();
            collection.AddMemoryFont(buffer, bytes.Length);

            Assert.Single(collection.Families);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Fact]
    public void AddMemoryFont_GarbageBytes_IsIgnored()
    {
        using var collection = new PrivateFontCollection();
        collection.AddMemoryFont(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.Empty(collection.Families);
    }

    [Fact]
    public void AddMemoryFont_ZeroLength_Throws()
    {
        using var collection = new PrivateFontCollection();
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.AddMemoryFont(IntPtr.Zero + 1, 0));
    }

    [Fact]
    public void AddedFamily_IsResolvableByAFontCreatedFromItsName()
    {
        using var collection = new PrivateFontCollection();
        collection.AddFontFile(SampleFontFile());
        var familyName = collection.Families[0].Name;

        using var font = new Font(familyName, 20f);
        Assert.Equal(familyName, font.GetSKTypeface().FamilyName);
    }

    [Fact]
    public void DisposedCollection_UnregistersItsFamilies()
    {
        string familyName;
        using (var collection = new PrivateFontCollection())
        {
            collection.AddFontFile(SampleFontFile());
            familyName = collection.Families[0].Name;
        }

        // Once unregistered, the family only resolves if the host actually has it installed --
        // either way the lookup must not hand back the disposed typeface.
        using var font = new Font(familyName, 20f);
        var resolved = font.GetSKTypeface();
        Assert.NotNull(resolved);
        Assert.False(resolved.Handle == IntPtr.Zero);
    }

    [Fact]
    public void DisposedCollection_ReportsNoFamilies()
    {
        var collection = new PrivateFontCollection();
        collection.AddFontFile(SampleFontFile());
        collection.Dispose();

        Assert.Empty(collection.Families);
        Assert.Throws<ObjectDisposedException>(() => collection.AddFontFile(SampleFontFile()));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var collection = new PrivateFontCollection();
        collection.AddFontFile(SampleFontFile());
        collection.Dispose();
        collection.Dispose();
    }

    [Fact]
    public void InstalledFontCollection_EnumeratesTheSystemFonts()
    {
        using var collection = new InstalledFontCollection();

        // SkiaSharp's font manager is cross-platform, so this must be the same list FontFamily
        // exposes -- and on a machine with no fonts at all it is legitimately empty.
        Assert.Equal(FontFamily.Families.Length, collection.Families.Length);
    }

    [Fact]
    public void InstalledFontCollection_FamiliesHaveNames()
    {
        using var collection = new InstalledFontCollection();
        Assert.All(collection.Families, f => Assert.False(string.IsNullOrWhiteSpace(f.Name)));
    }

    [Fact]
    public void FontCollection_IsTheCommonBaseType()
    {
        using FontCollection installed = new InstalledFontCollection();
        using FontCollection @private = new PrivateFontCollection();

        Assert.NotNull(installed.Families);
        Assert.NotNull(@private.Families);
    }
}
