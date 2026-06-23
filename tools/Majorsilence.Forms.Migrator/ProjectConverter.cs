using System.Xml.Linq;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Rewrites an SDK-style <c>.csproj</c> so it targets Majorsilence.Forms instead of WinForms: it drops the
/// Windows-desktop opt-ins, retargets the framework, and wires in the Majorsilence.Forms references plus a
/// platform backend.
/// </summary>
internal static class ProjectConverter
{
    public sealed record Result(string Xml, bool Changed, IReadOnlyList<string> Warnings, IReadOnlyList<string> AddedPackages);

    /// <param name="centralPackageManagement">
    /// When true, the project is governed by a <c>Directory.Packages.props</c> with central management
    /// on, so added <c>PackageReference</c>s omit their <c>Version</c> (the version belongs in the props
    /// file). The added package ids are returned via <see cref="Result.AddedPackages"/> so the caller can
    /// pin their versions centrally.
    /// </param>
    /// <param name="addMajorsilenceReferences">
    /// Whether to add the <c>Majorsilence.Forms</c> + backend references. The migrator sets this false for
    /// a project that is neither a WinForms project nor contains any WinForms code, so non-UI projects in a
    /// solution aren't given a dependency they don't need.
    /// </param>
    public static Result Convert(string xml, MigrationOptions options, string projectDirectory,
        bool isVisualBasic = false, bool centralPackageManagement = false,
        IReadOnlyList<string>? removePackagePatterns = null, bool addMajorsilenceReferences = true)
    {
        var warnings = new List<string>();
        var addedPackages = new List<string>();
        XDocument doc;
        try
        {
            // Parse without preserving whitespace so the document re-serializes cleanly indented —
            // including any ItemGroup we inject — rather than leaving newly added nodes unformatted.
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            warnings.Add($"could not parse project XML ({ex.Message}); skipped");
            return new Result(xml, Changed: false, warnings, addedPackages);
        }

        var root = doc.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "Project", StringComparison.Ordinal))
        {
            warnings.Add("not a recognizable MSBuild project; skipped");
            return new Result(xml, Changed: false, warnings, addedPackages);
        }

        // Majorsilence.Forms only supports SDK-style projects. A legacy project has no Sdk attribute.
        if (root.Attribute("Sdk") is null)
        {
            warnings.Add("legacy (non-SDK) project format — convert to SDK-style first; skipped");
            return new Result(xml, Changed: false, warnings, addedPackages);
        }

        var changed = false;

        // Strip the Windows-desktop opt-ins. UseWindowsForms/UseWPF pull in the Windows-only desktop
        // framework, which defeats the whole point of moving to a cross-platform stack.
        foreach (var prop in new[] { "UseWindowsForms", "UseWPF", "EnableWindowsTargeting" })
        {
            foreach (var el in root.Descendants().Where(e => e.Name.LocalName == prop).ToList())
            {
                el.Remove();
                changed = true;
            }
        }

        // Retarget the framework. WinForms projects pin a -windows TFM (e.g. net8.0-windows).
        if (options.TargetFramework is { } forcedTfm)
        {
            // Explicit --tfm: force the exact framework and collapse a plural list to it.
            foreach (var name in new[] { "TargetFramework", "TargetFrameworks" })
            {
                foreach (var el in root.Descendants().Where(e => e.Name.LocalName == name).ToList())
                {
                    if (!string.Equals(el.Value, forcedTfm, StringComparison.Ordinal))
                    {
                        el.Value = forcedTfm;
                        changed = true;
                    }
                    // Always normalize to the singular element name.
                    if (name == "TargetFrameworks")
                        el.Name = el.Name.Namespace + "TargetFramework";
                }
            }
        }
        else
        {
            // Default: keep the project's .NET version, just drop the Windows desktop platform suffix
            // (net8.0-windows -> net8.0). A plural <TargetFrameworks> stays plural.
            if (TargetFrameworkRewriter.StripWindowsTargetFrameworks(root))
                changed = true;
        }

        // Drop a WinForms FrameworkReference / Windows-desktop SDK reference if present.
        foreach (var fr in root.Descendants()
                     .Where(e => e.Name.LocalName == "FrameworkReference")
                     .Where(e => (e.Attribute("Include")?.Value ?? "").Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            fr.Remove();
            changed = true;
        }

        // Drop WinForms-only NuGet packages (Telerik UI for WinForms, DevExpress, …). Their vendor UI
        // suites map onto the Majorsilence.Forms.* compat layers (wired up via source rewrites / --map),
        // and the rest are Windows-desktop-only, so the original packages don't belong here.
        RemoveWinFormsPackages(root, removePackagePatterns ?? WinFormsPackages.DefaultPatterns, ref changed);

        if (addMajorsilenceReferences)
            AddReferences(root, options, projectDirectory, centralPackageManagement, ref changed, warnings, addedPackages);

        if (isVisualBasic)
            ApplyVisualBasicFixups(root, ref changed, warnings);

        if (!changed)
            return new Result(xml, Changed: false, warnings, addedPackages);

        return new Result(doc.ToString(), Changed: true, warnings, addedPackages);
    }

    // VB WinForms projects lean on the "My" application framework (MyType=Windows/WindowsForms), which
    // drags in Microsoft.VisualBasic.Devices.Computer, WinForms.Form and ApplicationSettingsBase — none
    // of which exist cross-platform. Switching MyType to Empty turns that framework off.
    private static void ApplyVisualBasicFixups(XElement root, ref bool changed, List<string> warnings)
    {
        var ns = root.Name.Namespace;

        // 1. Force MyType=Empty (set the existing element, or add one to the first PropertyGroup).
        var myType = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "MyType");
        if (myType is not null)
        {
            if (!string.Equals(myType.Value, "Empty", StringComparison.Ordinal))
            {
                myType.Value = "Empty";
                changed = true;
            }
        }
        else
        {
            var group = root.Elements(ns + "PropertyGroup").FirstOrDefault()
                ?? AddTo(root, new XElement(ns + "PropertyGroup"));
            group.Add(new XElement(ns + "MyType", "Empty"));
            changed = true;
        }

        // 2. The My Project designer files won't compile with MyType=Empty — exclude them.
        const string designerGlob = @"My Project\*.Designer.vb";
        var alreadyRemoved = root.Descendants()
            .Any(e => e.Name.LocalName == "Compile" && (e.Attribute("Remove")?.Value ?? "")
                .Contains("My Project", StringComparison.OrdinalIgnoreCase));
        if (!alreadyRemoved)
        {
            root.Add(new XElement(ns + "ItemGroup",
                new XElement(ns + "Compile", new XAttribute("Remove", designerGlob))));
            changed = true;
        }

        // 3. Rewrite project-level VB global imports (<Import Include="System.Windows.Forms" />).
        foreach (var import in root.Descendants().Where(e => e.Name.LocalName == "Import").ToList())
        {
            var includeAttr = import.Attribute("Include");
            if (includeAttr is null)
                continue;
            foreach (var (from, to) in NamespaceMap.NamespacePrefixes)
            {
                if (includeAttr.Value.Equals(from, StringComparison.Ordinal))
                {
                    includeAttr.Value = to;
                    changed = true;
                    break;
                }
            }
        }

        // 4. With MyType=Empty there is no auto-generated entry point — but only an executable needs one.
        //    Class libraries (the SDK default when <OutputType> is absent) have no entry point to lose,
        //    so warning about them is just noise.
        if (IsExecutable(root))
            warnings.Add("VB project: MyType=Empty removes the auto entry point — add a Module with " +
                "<STAThread> Sub Main calling Application.Run(...), set <StartupObject>, and ensure each " +
                "Form's constructor calls InitializeComponent()");
    }

    // An SDK project is an app only when OutputType is Exe/WinExe; absent OutputType defaults to a library.
    private static bool IsExecutable(XElement root)
    {
        var outputType = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value;
        return outputType is not null
            && (outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase)
                || outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase));
    }

    private static XElement AddTo(XElement parent, XElement child)
    {
        parent.Add(child);
        return child;
    }

    private static void AddReferences(XElement root, MigrationOptions options, string projectDirectory,
        bool centralPackageManagement, ref bool changed, List<string> warnings, List<string> addedPackages)
    {
        var ns = root.Name.Namespace;
        var backendCore = options.Backend switch
        {
            Backend.Avalonia => "Majorsilence.Forms.Avalonia",
            Backend.Uno => "Majorsilence.Forms.Uno",
            Backend.Headless => "Majorsilence.Forms.Headless",
            _ => "Majorsilence.Forms.Avalonia",
        };
        var packages = new[] { "Majorsilence.Forms", backendCore };

        var itemGroup = new XElement(ns + "ItemGroup");

        foreach (var pkg in packages)
        {
            if (ReferenceAlreadyPresent(root, pkg))
                continue;

            if (options.ReferenceMode == ReferenceMode.Package)
            {
                var packageRef = new XElement(ns + "PackageReference", new XAttribute("Include", pkg));
                // Under central package management the version is pinned in Directory.Packages.props,
                // and a Version here would be a build error (NU1008) — omit it and report the package
                // so the caller can add the central <PackageVersion> entry.
                if (!centralPackageManagement)
                    packageRef.Add(new XAttribute("Version", options.PackageVersion));
                itemGroup.Add(packageRef);
                addedPackages.Add(pkg);
            }
            else
            {
                var relative = RelativeProjectPath(pkg, projectDirectory, options, warnings);
                itemGroup.Add(new XElement(ns + "ProjectReference",
                    new XAttribute("Include", relative)));
            }
            changed = true;
        }

        if (itemGroup.HasElements)
            root.Add(itemGroup);
    }

    // Removes every <PackageReference> (and any matching <PackageVersion>, for central package
    // management) whose id matches a WinForms-only pattern, tidying up any ItemGroup it empties out.
    private static void RemoveWinFormsPackages(XElement root, IReadOnlyList<string> patterns, ref bool changed)
    {
        if (patterns.Count == 0)
            return;

        var toRemove = root.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "PackageVersion")
            .Where(e =>
            {
                var id = e.Attribute("Include")?.Value ?? e.Attribute("Update")?.Value;
                return id is not null && WinFormsPackages.IsMatch(id, patterns);
            })
            .ToList();

        foreach (var el in toRemove)
        {
            var parent = el.Parent;
            el.Remove();
            changed = true;
            // Don't leave an empty <ItemGroup> behind once its last reference is gone.
            if (parent is not null && parent.Name.LocalName == "ItemGroup" && !parent.Elements().Any())
                parent.Remove();
        }
    }

    private static bool ReferenceAlreadyPresent(XElement root, string id) =>
        root.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference")
            .Any(e =>
            {
                var include = e.Attribute("Include")?.Value ?? "";
                return include.Equals(id, StringComparison.OrdinalIgnoreCase)
                    || include.EndsWith($"{id}.csproj", StringComparison.OrdinalIgnoreCase);
            });

    private static string RelativeProjectPath(string projectName, string projectDirectory, MigrationOptions options, List<string> warnings)
    {
        var repoRoot = options.RepoRoot ?? Directory.GetCurrentDirectory();
        var target = Path.Combine(repoRoot, "src", projectName, $"{projectName}.csproj");
        if (!File.Exists(target))
            warnings.Add($"project reference target not found: {target}");

        var relative = Path.GetRelativePath(projectDirectory, target);
        // Use Windows-style separators in the csproj for portability with VS on Windows.
        return relative.Replace('/', '\\');
    }
}
