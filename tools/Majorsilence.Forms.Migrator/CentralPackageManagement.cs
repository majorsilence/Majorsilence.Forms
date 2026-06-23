using System.Xml.Linq;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Helpers for NuGet Central Package Management (CPM). When a project sits under a
/// <c>Directory.Packages.props</c> that opts into central management, package versions live centrally
/// in that file and individual <c>&lt;PackageReference&gt;</c>s must omit their <c>Version</c> attribute.
/// The migrator honours that: it emits version-less references and adds the versions to the props file.
/// </summary>
internal static class CentralPackageManagement
{
    public const string FileName = "Directory.Packages.props";

    /// <summary>
    /// Walks up from <paramref name="projectDirectory"/> to the governing <c>Directory.Packages.props</c>
    /// (the nearest one in any ancestor directory, mirroring how MSBuild discovers it), or null if none.
    /// </summary>
    public static string? Find(string projectDirectory)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(projectDirectory)); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// True when the props file opts into central management. The <c>ManagePackageVersionsCentrally</c>
    /// property defaults to <c>true</c> simply by the file being present, so only an explicit
    /// <c>false</c> turns it off.
    /// </summary>
    public static bool IsEnabled(string propsXml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(propsXml); }
        catch (System.Xml.XmlException) { return false; }

        var flag = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ManagePackageVersionsCentrally")?.Value;
        return flag is null || flag.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures a <c>&lt;PackageVersion&gt;</c> entry exists for each requested package, appending any that
    /// are missing in a fresh <c>ItemGroup</c>. Existing entries (matched case-insensitively by Include)
    /// are left untouched. Returns the updated XML and whether anything changed.
    /// </summary>
    public static (string Xml, bool Changed) EnsureVersions(
        string propsXml, IEnumerable<(string Id, string Version)> packages)
    {
        var doc = XDocument.Parse(propsXml);
        var root = doc.Root!;
        var ns = root.Name.Namespace;

        bool Exists(string id) => root.Descendants()
            .Where(e => e.Name.LocalName == "PackageVersion")
            .Any(e => (e.Attribute("Include")?.Value ?? "").Equals(id, StringComparison.OrdinalIgnoreCase));

        var missing = packages.Where(p => !Exists(p.Id)).ToList();
        if (missing.Count == 0)
            return (propsXml, false);

        var itemGroup = new XElement(ns + "ItemGroup");
        foreach (var (id, version) in missing)
            itemGroup.Add(new XElement(ns + "PackageVersion",
                new XAttribute("Include", id),
                new XAttribute("Version", version)));
        root.Add(itemGroup);
        return (doc.ToString(), true);
    }
}
