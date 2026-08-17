using System.IO;
using System.Resources.Extensions;
using System.Text;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// <see cref="RawResourcesReader"/> lifts the undecoded payload of an entry out of a compiled
/// <c>.resources</c> binary, which is the only way to reach a resource the designer tooling wrote
/// through BinaryFormatter.
/// </summary>
/// <remarks>
/// DeserializingResourceReader throws <c>PlatformNotSupportedException</c> for those entries outright
/// (BinaryFormatter was removed in .NET 9), and the per-entry catch in
/// <see cref="ComponentResourceManager"/> then dropped them. Every <c>ImageList.ImageStream</c> in a
/// migrated app went missing that way, so a designer-populated toolbar drew with no button images at all.
/// Written against the real <see cref="PreserializedResourceWriter"/> rather than a hand-built file, so
/// this stays honest about the layout the SDK actually emits.
/// </remarks>
// AddBinaryFormattedResource is obsolete because BinaryFormatter is. Writing such an entry is exactly
// what these tests need: the payloads under test are the ones real designer tooling left behind, and
// nothing here deserializes one -- RawResourcesReader only ever hands back bytes.
#pragma warning disable SYSLIB0011

public class RawResourcesReaderTests
{
    private static byte[] WriteResources (System.Action<PreserializedResourceWriter> add)
    {
        var stream = new MemoryStream ();

        using (var writer = new PreserializedResourceWriter (stream)) {
            add (writer);
            writer.Generate ();
        }

        return stream.ToArray ();   // still readable after the writer closed it.
    }

    [Fact]
    public void Recovers_the_exact_payload_of_a_BinaryFormatter_entry ()
    {
        var payload = new byte[] { 0, 1, 0, 0, 0, 255, 255, 255, 42, 7 };
        var file = WriteResources (w =>
            w.AddBinaryFormattedResource ("imageList.ImageStream", payload,
                                          "System.Windows.Forms.ImageListStreamer, System.Windows.Forms"));

        var entries = RawResourcesReader.Read (file, _ => true);

        var entry = Assert.Contains ("imageList.ImageStream", entries);
        Assert.Equal (RawResourcesReader.RawFormat.BinaryFormatter, entry.Format);
        Assert.Equal (payload, entry.Data);
    }

    [Fact]
    public void Recovers_a_type_converter_byte_array_entry ()
    {
        var payload = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };   // a PNG signature, as an icon would be
        var file = WriteResources (w =>
            w.AddTypeConverterResource ("$this.Icon", payload, "System.Drawing.Icon, System.Drawing.Common"));

        var entries = RawResourcesReader.Read (file, _ => true);

        var entry = Assert.Contains ("$this.Icon", entries);
        Assert.Equal (RawResourcesReader.RawFormat.TypeConverterByteArray, entry.Format);
        Assert.Equal (payload, entry.Data);
    }

    [Fact]
    public void Reads_only_the_entries_the_caller_asked_for ()
    {
        var file = WriteResources (w => {
            w.AddBinaryFormattedResource ("wanted", new byte[] { 1, 2, 3 }, "Some.Type, SomeAssembly");
            w.AddBinaryFormattedResource ("ignored", new byte[] { 4, 5, 6 }, "Some.Type, SomeAssembly");
        });

        var entries = RawResourcesReader.Read (file, name => name == "wanted");

        Assert.Contains ("wanted", entries);
        Assert.DoesNotContain ("ignored", entries);
    }

    [Fact]
    public void Finds_the_wanted_entry_among_several ()
    {
        // Entry order in the file is the writer's business, so the one we want may sit behind others.
        var payload = new byte[] { 9, 9, 9, 9 };
        var file = WriteResources (w => {
            w.AddResource ("aString", "text");
            w.AddResource ("anInt", 42);
            w.AddBinaryFormattedResource ("blob", payload, "Some.Type, SomeAssembly");
            w.AddResource ("another", "more text");
        });

        var entries = RawResourcesReader.Read (file, _ => true);

        Assert.Equal (payload, Assert.Contains ("blob", entries).Data);

        // Primitives are not user-typed payloads; the normal reader already handles them.
        Assert.DoesNotContain ("aString", entries);
        Assert.DoesNotContain ("anInt", entries);
    }

    [Fact]
    public void Returns_nothing_for_a_file_that_is_not_a_resources_binary ()
    {
        var entries = RawResourcesReader.Read (Encoding.UTF8.GetBytes ("not a .resources file at all"), _ => true);

        Assert.Empty (entries);
    }

    [Fact]
    public void Returns_nothing_rather_than_throwing_on_a_truncated_file ()
    {
        var file = WriteResources (w =>
            w.AddBinaryFormattedResource ("blob", new byte[] { 1, 2, 3 }, "Some.Type, SomeAssembly"));

        var truncated = new byte[file.Length / 2];
        System.Array.Copy (file, truncated, truncated.Length);

        Assert.Empty (RawResourcesReader.Read (truncated, _ => true));
    }
}

#pragma warning restore SYSLIB0011
