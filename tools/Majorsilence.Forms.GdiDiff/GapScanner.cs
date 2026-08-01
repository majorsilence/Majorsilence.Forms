using System.Reflection;

namespace Majorsilence.Forms.GdiDiff;

/// <summary>
/// The actual comparison: walks every exported type in upstream <c>System.Drawing.Common</c>, maps its
/// namespace onto this repo's equivalent, and reports what has no counterpart.
/// </summary>
internal static class GapScanner
{
    /// <summary>
    /// Namespace mapping, mirroring MIGRATION.md's table. <c>System.Drawing</c> has two targets because
    /// a handful of its types (<c>Graphics</c>, <c>SystemBrushes</c>/<c>SystemPens</c>/<c>SystemFonts</c>,
    /// the buffered-graphics trio) live in <c>Majorsilence.Forms</c> rather than the drawing package —
    /// they depend on the Forms layer and would otherwise create a circular project reference. See
    /// COMPATIBILITY_MATRIX.md's "System.Drawing / GDI+" section.
    /// </summary>
    private static string[] TargetNamespaces(string ns) => ns switch
    {
        "System.Drawing" => ["Majorsilence.Forms.Drawing", "Majorsilence.Forms"],
        "System.Drawing.Drawing2D" => ["Majorsilence.Forms.Drawing.Drawing2D"],
        "System.Drawing.Imaging" => ["Majorsilence.Forms.Drawing.Imaging"],
        "System.Drawing.Text" => ["Majorsilence.Forms.Drawing.Text"],
        "System.Drawing.Printing" => ["Majorsilence.Forms.Printing"],
        _ => [],
    };

    /// <summary>
    /// Value types that are deliberately NOT reimplemented: the real cross-platform BCL
    /// <c>System.Drawing.Primitives</c> types are used as-is. Reimplementing them would make every bare
    /// <c>Point</c>/<c>Rectangle</c>/<c>Color</c> ambiguous in files that import both namespaces.
    /// </summary>
    private static readonly HashSet<string> Primitives = new(StringComparer.Ordinal)
    {
        "Color", "Point", "PointF", "Size", "SizeF", "Rectangle", "RectangleF", "KnownColor",
    };

    public static string[] Scan(string repoRoot, string configuration)
    {
        var upstream = LocateUpstream();
        var ours = new[]
        {
            Path.Combine(repoRoot, "src", "Majorsilence.Forms.Drawing.Common", "bin", configuration, "net10.0", "Majorsilence.Forms.Drawing.Common.dll"),
            Path.Combine(repoRoot, "src", "Majorsilence.Forms", "bin", configuration, "net10.0", "Majorsilence.Forms.dll"),
        };
        foreach (var path in ours)
            if (!File.Exists(path))
                throw new FileNotFoundException($"expected assembly not found: {path}");

        // The resolver needs the shared framework (for System.Object etc.) plus everything sitting
        // beside our own assemblies, so their base types and referenced types resolve.
        var searchPaths = new List<string>(Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
        searchPaths.Add(upstream);
        searchPaths.AddRange(ours);
        foreach (var path in ours)
            searchPaths.AddRange(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.dll"));

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(searchPaths.Distinct(StringComparer.Ordinal)));
        var upstreamAsm = mlc.LoadFromAssemblyPath(upstream);

        var ourTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var path in ours)
            foreach (var type in mlc.LoadFromAssemblyPath(path).GetExportedTypes())
                ourTypes.TryAdd(type.FullName ?? type.Name, type);

        var gaps = new List<string>();

        foreach (var upstreamType in upstreamAsm.GetExportedTypes())
        {
            var ns = upstreamType.Namespace ?? "";
            if (TargetNamespaces(ns).Length == 0 || upstreamType.IsNested || Primitives.Contains(upstreamType.Name))
                continue;

            var ourType = Resolve(ourTypes, ns, upstreamType.Name);
            if (ourType is null)
            {
                gaps.Add($"TYPE   {upstreamType.FullName}");
                continue;
            }

            var upstreamMembers = PublicMemberNames(upstreamType);
            var ourMembers = new HashSet<string>(PublicMemberNames(ourType), StringComparer.Ordinal);
            // A base class may legitimately supply the member, so walk our inheritance chain too.
            for (var b = ourType.BaseType; b is not null && b.FullName != "System.Object"; b = b.BaseType)
                foreach (var name in PublicMemberNames(b))
                    ourMembers.Add(name);

            foreach (var name in upstreamMembers.Except(ourMembers, StringComparer.Ordinal))
                gaps.Add($"MEMBER {upstreamType.FullName}.{name}");
        }

        gaps.Sort(StringComparer.Ordinal);
        return [.. gaps];
    }

    private static Type? Resolve(Dictionary<string, Type> ourTypes, string ns, string name)
    {
        foreach (var target in TargetNamespaces(ns))
            if (ourTypes.TryGetValue($"{target}.{name}", out var exact))
                return exact;

        // Fall back to a same-simple-name match anywhere under Majorsilence.Forms: several upstream
        // Drawing2D/Imaging types are deliberately declared in the flatter Majorsilence.Forms.Drawing
        // namespace rather than a sub-namespace, and that is a naming choice, not a missing type.
        foreach (var candidate in ourTypes.Values)
            if (candidate.Name == name && (candidate.Namespace?.StartsWith("Majorsilence.Forms", StringComparison.Ordinal) ?? false))
                return candidate;

        return null;
    }

    /// <summary>
    /// Public instance+static members declared on <paramref name="type"/>, by name. Constructors are
    /// excluded (overload-only differences are not what this audit tracks), as are operators and the
    /// <c>object</c> overrides, which are noise rather than API gaps.
    /// </summary>
    private static IEnumerable<string> PublicMemberNames(Type type)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var member in type.GetMembers(Flags))
        {
            if (member is ConstructorInfo)
                continue;
            if (member is MethodInfo method && method.IsSpecialName)
                continue;   // property/event accessors, reported via the property/event itself
            if (member.Name.StartsWith("op_", StringComparison.Ordinal))
                continue;
            if (member.Name is "GetHashCode" or "Equals" or "ToString" or "Finalize")
                continue;
            yield return member.Name;
        }
    }

    /// <summary>
    /// Locates the upstream reference assembly from the NuGet package root baked in at build time (see
    /// this project's csproj), preferring the newest <c>lib/net*</c> flavor available in the package.
    /// </summary>
    private static string LocateUpstream()
    {
        var root = typeof(GapScanner).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SystemDrawingCommonPackageRoot")?.Value;

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            throw new FileNotFoundException("could not locate the System.Drawing.Common package (SystemDrawingCommonPackageRoot was not baked in at build time)");

        var candidate = Directory.GetDirectories(Path.Combine(root, "lib"), "net*")
            .Where(d => !Path.GetFileName(d).StartsWith("net4", StringComparison.Ordinal))
            .OrderByDescending(d => d, StringComparer.Ordinal)
            .Select(d => Path.Combine(d, "System.Drawing.Common.dll"))
            .FirstOrDefault(File.Exists);

        return candidate ?? throw new FileNotFoundException($"no System.Drawing.Common.dll under {root}/lib");
    }
}
