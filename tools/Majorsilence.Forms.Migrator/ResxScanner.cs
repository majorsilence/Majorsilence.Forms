using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Classifies the entries in a WinForms <c>.resx</c> file by how Majorsilence.Forms can consume them.
///
/// Majorsilence.Forms ships a cross-platform <c>ComponentResourceManager</c> that reads the <c>.resx</c>
/// XML directly, so most designer resources now load at runtime without a human:
/// <list type="bullet">
///   <item>plain string-table values (the localization case);</item>
///   <item>primitive designer values typed against <c>System.Drawing</c>/<c>mscorlib</c>;</item>
///   <item>images stored as <c>bytearray.base64</c> (raw image bytes);</item>
///   <item>and even <c>BinaryFormatter</c> blobs for the common image types — <c>System.Drawing.Bitmap</c>,
///         <c>Icon</c>, and <c>ImageListStreamer</c> — which the resource manager recovers from the NRBF
///         wire format without running <c>BinaryFormatter</c>.</item>
/// </list>
///
/// What still needs a human is a <c>BinaryFormatter</c>/SOAP blob of some <em>other</em> type (a
/// serialized component value, an ActiveX OCX state, …): those cannot be deserialized cross-platform.
///
/// This is a read-only classification — it never rewrites the file.
/// </summary>
internal static partial class ResxScanner
{
    public sealed record Result(
        int DesignerResourceCount,
        int BinaryResourceCount,
        int ByteArrayImageCount,
        int StringCount,
        int RecoverableImageBlobCount,
        int PlaceholderBlobCount,
        int ActiveXBlobCount)
    {
        /// <summary>BinaryFormatter blobs that can't be carried across (unsupported types, or ActiveX).</summary>
        public bool NeedsReview => BinaryResourceCount > 0 || ActiveXBlobCount > 0;

        /// <summary>True when the file carries designer values/images the resource manager can load.</summary>
        public bool HasConsumableResources =>
            DesignerResourceCount > 0 || ByteArrayImageCount > 0 || RecoverableImageBlobCount > 0;
    }

    // A <data>/<metadata> entry typed against the WinForms or GDI+ assemblies.
    [GeneratedRegex("""type\s*=\s*"System\.(?:Drawing|Windows\.Forms)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex DesignerTypedEntry();

    // A bytearray-serialized payload — the modern TypeConverter form, raw image bytes.
    [GeneratedRegex("""mimetype\s*=\s*"application/x-microsoft\.net\.object\.bytearray\.base64""", RegexOptions.IgnoreCase)]
    private static partial Regex ByteArrayEntry();

    // A <data> value with neither a type nor a mimetype attribute — a plain string-table entry.
    [GeneratedRegex("""<data\b(?:(?!mimetype|(?<!\w)type\s*=)[^>])*>""", RegexOptions.IgnoreCase)]
    private static partial Regex PlainStringEntry();

    // A full <data ...> ... </data> element (tolerant; the value may span lines).
    [GeneratedRegex("""<data\b(?<attrs>[^>]*)>(?<body>.*?)</data>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DataElement();

    [GeneratedRegex("""<value>(?<v>.*?)</value>""", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ValueElement();

    // BinaryFormatter blob types Majorsilence.Forms.ComponentResourceManager recovers cross-platform:
    // the images, plus the design-time component values WinForms serialized into a form's .resx
    // (System.Data.SqlTypes scalars for a SqlCommand's parameters, and DBNull via UnitySerializationHolder).
    private static readonly string[] RecoverableTypes =
    [
        "System.Drawing.Bitmap",
        "System.Drawing.Image",
        "System.Drawing.Icon",
        "System.Windows.Forms.ImageListStreamer",
        "System.Data.SqlTypes.",
        "System.UnitySerializationHolder",
    ];

    public static Result Scan(string xml)
    {
        var binaryBlockers = 0;
        var recoverable = 0;
        var placeholders = 0;
        var activeX = 0;

        foreach (Match data in DataElement().Matches(xml))
        {
            var attrs = data.Groups["attrs"].Value;
            if (!attrs.Contains("binary.base64", StringComparison.OrdinalIgnoreCase) &&
                !attrs.Contains("soap.base64", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = ValueElement().Match(data.Groups["body"].Value).Groups["v"].Value;
            switch (Classify(value))
            {
                case BlobKind.Recoverable: recoverable++; break;
                case BlobKind.Placeholder: placeholders++; break;
                case BlobKind.ActiveX: activeX++; break;
                default: binaryBlockers++; break;
            }
        }

        return new Result(
            DesignerResourceCount: DesignerTypedEntry().Count(xml),
            BinaryResourceCount: binaryBlockers,
            ByteArrayImageCount: ByteArrayEntry().Count(xml),
            StringCount: PlainStringEntry().Count(xml),
            RecoverableImageBlobCount: recoverable,
            PlaceholderBlobCount: placeholders,
            ActiveXBlobCount: activeX);
    }

    private enum BlobKind { Unsupported, Recoverable, Placeholder, ActiveX }

    private static BlobKind Classify(string rawValue)
    {
        var trimmed = rawValue.Trim();

        // Scrubbed/sanitized corpora replace the payload with a "[base64 mime encoded …]" stub.
        if (trimmed.Length == 0 || trimmed.StartsWith('['))
            return BlobKind.Placeholder;

        byte[] bytes;
        try { bytes = System.Convert.FromBase64String (Regex.Replace(trimmed, @"\s+", "")); }
        catch { return BlobKind.Placeholder; }   // not real base64 — treat as a stub, not a blocker.

        // The NRBF payload embeds its type names as ASCII; a substring scan is enough to classify.
        var ascii = System.Text.Encoding.Latin1.GetString(bytes);

        foreach (var type in RecoverableTypes)
            if (ascii.Contains(type, StringComparison.Ordinal))
                return BlobKind.Recoverable;

        // ActiveX/COM control state (AxHost) has no managed equivalent — call it out specifically.
        if (ascii.Contains("AxHost", StringComparison.Ordinal))
            return BlobKind.ActiveX;

        return BlobKind.Unsupported;
    }
}
