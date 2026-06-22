using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Inspects a WinForms <c>.resx</c> file. Majorsilence.Forms builds its UI in code and has no
/// <c>ComponentResourceManager</c>, so designer-serialized resources (typed <c>System.Drawing</c> /
/// <c>System.Windows.Forms</c> values, or binary-serialized objects) cannot be consumed as-is and need
/// a human. Plain string-table resx — the localization case — references none of that and passes clean.
///
/// This is a read-only scan: it never rewrites the file, it only reports what a human must revisit.
/// </summary>
internal static partial class ResxScanner
{
    public sealed record Result(int DesignerResourceCount, int BinaryResourceCount)
    {
        public bool NeedsReview => DesignerResourceCount > 0 || BinaryResourceCount > 0;
    }

    // A <data>/<metadata> entry typed against the WinForms or GDI+ assemblies, e.g.
    // type="System.Drawing.Size, System.Drawing" — but NOT the boilerplate resheader reader/writer.
    [GeneratedRegex("""type\s*=\s*"System\.(?:Drawing|Windows\.Forms)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex DesignerTypedEntry();

    // A binary/SOAP-serialized object payload (icons, fonts, embedded images, …).
    [GeneratedRegex("""mimetype\s*=\s*"application/x-microsoft\.net\.object\.(?:binary|soap)\.base64""", RegexOptions.IgnoreCase)]
    private static partial Regex BinarySerializedEntry();

    public static Result Scan(string xml) =>
        new(DesignerTypedEntry().Count(xml), BinarySerializedEntry().Count(xml));
}
