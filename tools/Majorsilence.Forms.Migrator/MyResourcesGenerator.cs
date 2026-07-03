using System.Text;
using System.Xml.Linq;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Generates the <c>My.Resources</c> accessor module that a migrated VB project needs once its real
/// compiler-generated <c>My Project\Resources.Designer.vb</c> is excluded from the build (see
/// <c>ProjectConverter</c>'s <c>&lt;Compile Remove="My Project\*.Designer.vb" /&gt;</c>, forced by
/// <c>MyType=Empty</c>). Without a replacement, every <c>My.Resources.X</c> call site left behind by the
/// (unchanged, still-warning-worthy-if-we-didn't-do-this) source rewrite would fail to compile.
///
/// Modeled on the same idempotent, generated-file-aware precedent as
/// <see cref="SourceConverter"/>'s VB constructor injection: this looks only at the project's
/// <c>My Project\Resources.resx</c> — the file that backs classic VB's <c>My.Resources</c> — and writes a
/// single companion <c>My Project\Resources.vb</c> (deliberately NOT named <c>*.Designer.vb</c>, so the
/// exclusion glob above never removes it). Regenerating overwrites the file wholesale each run, which is
/// safe/idempotent because the file is 100% derived from the <c>.resx</c> and carries no hand-edits.
///
/// Each resource gets a property typed to match what real migrated call sites already expect (confirmed
/// against Financial's actual usage): image-typed entries (<c>System.Drawing.Bitmap</c>/<c>Image</c>/
/// <c>Icon</c>) return <see cref="Majorsilence.Drawing.Image"/> — satisfying both a direct assignment to
/// an <c>Image</c>-typed property and an explicit
/// <c>CType(My.Resources.X, Majorsilence.Drawing.Image)</c> — <c>System.Byte[]</c> entries return
/// <c>Byte()</c> (the shape <c>BinaryWriter.Write</c> needs), and everything else (plain string-table
/// values, or a type this generator doesn't recognize) returns <see cref="string"/> via
/// <c>GetString</c>/<c>GetObject</c>. Every property forwards to a single
/// <see cref="Majorsilence.Forms.ComponentResourceManager"/> built from this <c>.resx</c> — the same
/// engine real per-form designer code already uses, so there is exactly one resx-reading implementation
/// in play.
///
/// One known, documented gap: entries stored as <c>System.Resources.ResXFileRef</c> (a resource added to
/// the project as a linked file rather than inline data) compile fine through this generator — the
/// declared type still drives the property's return type — but resolve to <see langword="null"/> at
/// runtime, because <see cref="Majorsilence.Forms.ComponentResourceManager"/> does not read file-linked
/// resx entries (only inline <c>value</c> data). This mirrors the resource manager's existing, documented
/// treatment of anything it can't materialize.
/// </summary>
internal static class MyResourcesGenerator
{
    public sealed record Resource(string RawName, string PropertyName, ResourceKind Kind);

    public enum ResourceKind { Image, ByteArray, StringOrOther }

    /// <summary>
    /// True for the one <c>.resx</c> per VB project that backs <c>My.Resources</c> — the compiler-managed
    /// <c>My Project\Resources.resx</c>. Per-form designer <c>.resx</c> files (already served by
    /// <c>ComponentResourceManager(typeof(FormX))</c>) are untouched by this generator.
    /// </summary>
    public static bool IsMyResourcesResx(string path)
    {
        if (!Path.GetFileName(path).Equals("Resources.resx", StringComparison.OrdinalIgnoreCase))
            return false;
        var dir = Path.GetFileName(Path.GetDirectoryName(path));
        return string.Equals(dir, "My Project", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Parses a <c>Resources.resx</c>'s entries into the resources this generator will expose.</summary>
    public static List<Resource> ParseResources(string resxXml)
    {
        var results = new List<Resource>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        XDocument doc;
        try { doc = XDocument.Parse(resxXml); }
        catch { return results; }   // a malformed resx yields no resources — same tolerance as ComponentResourceManager.

        foreach (var data in doc.Descendants("data"))
        {
            var rawName = (string?)data.Attribute("name");
            if (rawName is null || !seenNames.Add(rawName))
                continue; // skip duplicates defensively; a well-formed resx never has them.

            var typeAttr = (string?)data.Attribute("type");
            var kind = ClassifyType(typeAttr, data.Element("value")?.Value);
            results.Add(new Resource(rawName, SanitizeIdentifier(rawName), kind));
        }

        return results;
    }

    // Classifies a resource by its declared CLR type: the `type` attribute for an inline entry, or (for
    // a System.Resources.ResXFileRef entry, which carries no useful `type` of its own) the second
    // semicolon-delimited segment of its <value> — "..\Resources\Completed.png;System.Drawing.Bitmap, ...".
    private static ResourceKind ClassifyType(string? typeAttr, string? value)
    {
        var leading = LeadingType(typeAttr);

        if (leading.StartsWith("System.Resources.ResXFileRef", StringComparison.Ordinal))
            leading = LeadingType(FileRefTypeSegment(value));

        if (string.IsNullOrEmpty(leading))
            return ResourceKind.StringOrOther; // no type => plain string-table entry.

        if (leading is "System.Byte[]")
            return ResourceKind.ByteArray;

        if (leading is "System.Drawing.Bitmap" or "System.Drawing.Image" or "System.Drawing.Icon"
            or "System.Windows.Forms.ImageListStreamer")
            return ResourceKind.Image;

        return ResourceKind.StringOrOther;
    }

    // "..\Resources\Completed.png;System.Drawing.Bitmap, System.Drawing, Version=..." -> "System.Drawing.Bitmap, System.Drawing, Version=...".
    private static string? FileRefTypeSegment(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        var parts = value.Split(';');
        return parts.Length >= 2 ? parts[1] : null;
    }

    // The bare type name, dropping the assembly-qualified tail — same convention as
    // ComponentResourceManager.LeadingType.
    private static string LeadingType(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;
        var comma = typeName.IndexOf(',');
        return (comma < 0 ? typeName : typeName[..comma]).Trim();
    }

    // Turns an arbitrary resx entry name into a valid VB identifier the same way the real
    // StronglyTypedResourceBuilder does: any character that isn't a letter/digit/underscore becomes '_',
    // and a name starting with a digit gets an '_' prefix. Confirmed against real generated code — e.g.
    // "number of assert on maintains" -> "number_of_assert_on_maintains_" (the trailing space also becomes
    // a trailing underscore).
    private static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder(name.Length + 1);
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        if (sb.Length == 0 || char.IsDigit(sb[0]))
            sb.Insert(0, '_');
        return sb.ToString();
    }

    /// <summary>
    /// Generates the VB source for <c>My Project\Resources.vb</c>. Deterministic (stable resource order,
    /// same as the resx) so a re-run over an unchanged <c>.resx</c> produces byte-identical output. The
    /// source <c>.resx</c> XML is embedded verbatim as a VB string constant and parsed once via
    /// <see cref="Majorsilence.Forms.ComponentResourceManager.FromXml"/> — deliberately not a
    /// disk/embedded-manifest lookup at runtime (unlike the per-form <c>ComponentResourceManager(Type)</c>
    /// path), so this has no dependency on the project's build output layout or item types.
    /// </summary>
    public static string Generate(List<Resource> resources, string resxXml)
    {
        var sb = new StringBuilder();
        sb.Append("' [majorsilence-migrate] Generated My.Resources accessor — replaces the excluded\r\n");
        sb.Append("' My Project\\Resources.Designer.vb so 'My.Resources.X' call sites keep compiling.\r\n");
        sb.Append("' Regenerated verbatim from Resources.resx on every migration run; do not hand-edit.\r\n");
        sb.Append("Option Strict On\r\n");
        sb.Append("Option Explicit On\r\n");
        sb.Append('\r').Append('\n');
        sb.Append("Namespace My.Resources\r\n");
        sb.Append("\r\n");
        sb.Append("    Friend Module Resources\r\n");
        sb.Append("\r\n");
        sb.Append("        Private ReadOnly _resxXml As String =\r\n");
        AppendVbStringLiteral(sb, resxXml, indent: "            ");
        sb.Append('\r').Append('\n');
        sb.Append("\r\n");
        sb.Append("        Private ReadOnly _resourceManager As Majorsilence.Forms.ComponentResourceManager =\r\n");
        sb.Append("            Majorsilence.Forms.ComponentResourceManager.FromXml(_resxXml)\r\n");

        foreach (var resource in resources)
        {
            sb.Append("\r\n");
            var vbType = resource.Kind switch
            {
                ResourceKind.Image => "Majorsilence.Drawing.Image",
                ResourceKind.ByteArray => "Byte()",
                _ => "String",
            };
            sb.Append($"        Friend ReadOnly Property {resource.PropertyName}() As {vbType}\r\n");
            sb.Append("            Get\r\n");
            sb.Append(resource.Kind switch
            {
                ResourceKind.Image =>
                    $"                Return CType(_resourceManager.GetObject(\"{EscapeVbString(resource.RawName)}\"), Majorsilence.Drawing.Image)\r\n",
                ResourceKind.ByteArray =>
                    $"                Return CType(_resourceManager.GetObject(\"{EscapeVbString(resource.RawName)}\"), Byte())\r\n",
                _ =>
                    $"                Return _resourceManager.GetString(\"{EscapeVbString(resource.RawName)}\")\r\n",
            });
            sb.Append("            End Get\r\n");
            sb.Append("        End Property\r\n");
        }

        sb.Append("\r\n");
        sb.Append("    End Module\r\n");
        sb.Append("\r\n");
        sb.Append("End Namespace\r\n");
        return sb.ToString();
    }

    private static string EscapeVbString(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    // A VB string literal cannot contain a raw line break, so the resx XML — which is full of them — is
    // emitted as a sequence of same-line chunks joined by " & vbCrLf & ", one XML line per chunk. Each
    // chunk is also capped well under VB's practical logical-line length so a single huge base64 <value>
    // line (a large embedded image) still compiles instead of tripping the compiler's line-length limit.
    private const int MaxChunkLength = 4000;

    private static void AppendVbStringLiteral(StringBuilder sb, string text, string indent)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var chunks = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                chunks.Add(string.Empty);
                continue;
            }
            for (var i = 0; i < line.Length; i += MaxChunkLength)
                chunks.Add(line.Substring(i, Math.Min(MaxChunkLength, line.Length - i)));
        }

        if (chunks.Count == 0)
        {
            sb.Append(indent).Append("\"\"\r\n");
            return;
        }

        for (var i = 0; i < chunks.Count; i++)
        {
            var last = i == chunks.Count - 1;
            sb.Append(indent).Append('"').Append(EscapeVbString(chunks[i])).Append('"');
            sb.Append(last ? "\r\n" : " & vbCrLf &\r\n");
        }
    }
}
