using System.Xml.Linq;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Drops the Windows-desktop platform suffix from target frameworks so a WinForms project keeps its
/// .NET version but stops pinning Windows: <c>net8.0-windows</c> → <c>net8.0</c>,
/// <c>net10.0-windows10.0.19041.0</c> → <c>net10.0</c>. Shared by the project converter and the
/// migrator's handling of custom <c>.props</c>/<c>.targets</c> files imported by a project.
/// </summary>
internal static class TargetFrameworkRewriter
{
    /// <summary>Strips a Windows platform suffix from a single TFM; other TFMs are returned unchanged.</summary>
    public static string StripWindowsSuffix(string tfm)
    {
        var i = tfm.IndexOf("-windows", StringComparison.OrdinalIgnoreCase);
        return i < 0 ? tfm : tfm[..i];
    }

    /// <summary>Applies <see cref="StripWindowsSuffix"/> to each TFM in a (possibly <c>;</c>-separated) value.</summary>
    public static string StripWindowsSuffixes(string value) =>
        string.Join(';', value.Split(';').Select(StripWindowsSuffix));

    /// <summary>
    /// Strips Windows suffixes from every <c>TargetFramework</c>/<c>TargetFrameworks</c> element under
    /// <paramref name="root"/>. A plural <c>&lt;TargetFrameworks&gt;</c> stays plural (each entry is
    /// rewritten in place). Returns whether anything changed.
    /// </summary>
    public static bool StripWindowsTargetFrameworks(XElement root)
    {
        var changed = false;
        foreach (var el in root.Descendants()
                     .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                     .ToList())
        {
            var rewritten = StripWindowsSuffixes(el.Value);
            if (!string.Equals(rewritten, el.Value, StringComparison.Ordinal))
            {
                el.Value = rewritten;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Strips Windows suffixes from the TFMs in a standalone MSBuild file (a <c>.props</c>/<c>.targets</c>
    /// imported by a project). Malformed XML is returned untouched. Returns the (possibly) updated XML and
    /// whether it changed.
    /// </summary>
    public static (string Xml, bool Changed) StripWindowsFromDocument(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (System.Xml.XmlException) { return (xml, false); }
        if (doc.Root is null)
            return (xml, false);

        return StripWindowsTargetFrameworks(doc.Root) ? (doc.ToString(), true) : (xml, false);
    }
}
