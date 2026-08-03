using System.Reflection;

namespace Majorsilence.Forms.ApiDiff;

/// <summary>
/// The comparison: walks every exported type in an upstream assembly, maps its namespace onto this
/// repo's equivalent, and reports what has no counterpart.
///
/// Everything is loaded through <see cref="MetadataLoadContext"/>, so no target assembly is executed
/// and it does not matter that the upstream assemblies only <em>run</em> on Windows.
/// </summary>
internal static class GapScanner
{
    public static string[] Scan(Surface surface, string repoRoot, string configuration)
    {
        var upstreamPath = LocateUpstream(surface);
        var ours = surface.OurAssemblies(repoRoot, configuration);
        foreach (var path in ours)
            if (!File.Exists(path))
                throw new FileNotFoundException($"expected assembly not found: {path}");

        // The resolver needs the shared framework (System.Object and friends), the upstream assembly's
        // own neighbours, our assemblies, and SkiaSharp — our members' parameter types reach into it,
        // and a library build does not copy transitive package assemblies next to its output.
        var searchPaths = new List<string>(Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
        searchPaths.AddRange(Directory.GetFiles(Path.GetDirectoryName(upstreamPath)!, "*.dll"));
        searchPaths.AddRange(ours);
        searchPaths.AddRange(PackageAssemblies("SkiaSharpPackageRoot"));
        foreach (var path in ours)
            searchPaths.AddRange(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.dll"));

        using var mlc = new MetadataLoadContext(new PathAssemblyResolver(searchPaths.Distinct(StringComparer.Ordinal)));
        var upstreamAsm = mlc.LoadFromAssemblyPath(upstreamPath);

        var ourTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var path in ours)
            foreach (var type in mlc.LoadFromAssemblyPath(path).GetExportedTypes())
                ourTypes.TryAdd(type.FullName ?? type.Name, type);

        var gaps = new List<string>();

        foreach (var upstreamType in upstreamAsm.GetExportedTypes())
        {
            var ns = upstreamType.Namespace ?? "";
            if (!surface.NamespaceMap.ContainsKey(ns) || upstreamType.IsNested || surface.ExcludedTypeNames.Contains(upstreamType.Name))
                continue;

            var ourType = Resolve(surface, ourTypes, ns, upstreamType.Name);
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

            if (upstreamType.IsEnum && ourType.IsEnum)
                gaps.AddRange(EnumValueMismatches(upstreamType, ourType));
            else if (surface.IncludeOverloads)
                gaps.AddRange(OverloadGaps(surface, upstreamType, ourType));
        }

        gaps.Sort(StringComparer.Ordinal);
        return [.. gaps];
    }

    /// <summary>
    /// Reports enum members that exist on both sides but carry a different number.
    ///
    /// This is a strictly nastier failure than a missing member and is invisible to a presence-only
    /// diff: the code compiles, runs, and silently means something else. Designer-generated code and
    /// <c>.resx</c> resources persist these as raw integers, so a wrong number corrupts data on
    /// round-trip rather than failing loudly. Added after this check found 14 real mismatches on its
    /// first run, including <c>StringFormatFlags.DirectionRightToLeft</c> and <c>DirectionVertical</c>
    /// being transposed.
    /// </summary>
    private static IEnumerable<string> EnumValueMismatches(Type upstreamType, Type ourType)
    {
        var ourValues = EnumValues(ourType);
        foreach (var (name, upstreamValue) in EnumValues(upstreamType))
            if (ourValues.TryGetValue(name, out var ourValue) && ourValue != upstreamValue)
                yield return $"VALUE  {upstreamType.FullName}.{name} ours={ourValue} upstream={upstreamValue}";
    }

    private static Dictionary<string, long> EnumValues(Type enumType)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var raw = field.GetRawConstantValue();
            if (raw is not null)
                values[field.Name] = Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture);
        }
        return values;
    }

    /// <summary>
    /// Reports methods that exist by name on both sides but are missing a specific upstream
    /// <em>overload</em>.
    ///
    /// The name-level pass above is blind to this, and it is a real gap rather than a cosmetic one:
    /// <c>Region.Union(Region)</c> was absent for the entire life of that type while
    /// <c>Region.Union(RectangleF)</c> existed, so the presence check said "have it" and migrated code
    /// still failed to compile. Parameters are compared by simple type name, which makes the namespace
    /// difference between the two sides a non-issue and costs only the (harmless here) possibility of
    /// two unrelated same-named types matching.
    ///
    /// An overload naming a type the surface excludes is skipped, for the same reason the type is:
    /// <c>MessageBox.Show(IWin32Window, ...)</c> cannot be matched without declaring
    /// <c>IWin32Window</c>, and declaring it is exactly what the exclusion list rules out.
    /// </summary>
    private static IEnumerable<string> OverloadGaps(Surface surface, Type upstreamType, Type ourType)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var ourMethods = new List<(string Name, string[] Parameters, int Required)>();
        for (var t = ourType; t is not null && t.FullName != "System.Object"; t = t.BaseType)
            foreach (var method in t.GetMethods(Flags))
            {
                var parameters = method.GetParameters();
                ourMethods.Add((
                    method.Name,
                    [.. parameters.Select(p => TypeName(p.ParameterType))],
                    parameters.Count(p => !p.IsOptional)));
            }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in upstreamType.GetMethods(Flags))
        {
            if (method.IsSpecialName || method.Name.StartsWith("op_", StringComparison.Ordinal))
                continue;
            if (method.Name is "GetHashCode" or "Equals" or "ToString" or "Finalize")
                continue;
            // Only overloads of a method we already have; a wholly missing method is a MEMBER line.
            if (!ourMethods.Any(m => m.Name == method.Name))
                continue;

            var wanted = method.GetParameters().Select(p => TypeName(p.ParameterType)).ToArray();
            if (ourMethods.Any(m => Satisfies(m, method.Name, wanted)))
                continue;
            // An overload that takes a type this surface excludes is excluded for the same reason the
            // type is. MessageBox.Show(IWin32Window, ...) cannot be matched without declaring
            // IWin32Window, and declaring it is precisely what the exclusion list says is not a goal;
            // reporting the overload anyway would be asking for work already decided against.
            if (wanted.Any(surface.ExcludedTypeNames.Contains))
                continue;

            var signature = $"{method.Name}({string.Join(",", wanted)})";
            if (reported.Add(signature))
                yield return $"SIG    {upstreamType.FullName}.{signature}";
        }

        // A call written against the upstream overload has to compile against ours. That is not the same
        // as an identical signature: our methods routinely add a trailing optional parameter (a
        // MatrixOrder, say), which still binds a call that omits it. Treating those as gaps would bury
        // the real findings in noise.
        static bool Satisfies((string Name, string[] Parameters, int Required) ours, string name, string[] wanted)
        {
            if (ours.Name != name || ours.Parameters.Length < wanted.Length || ours.Required > wanted.Length)
                return false;
            for (var i = 0; i < wanted.Length; i++)
            {
                // An `object` parameter binds an argument of any reference type, so it satisfies the
                // upstream shape even though the type names differ. This is not a loophole: it is how
                // Region and GraphicsPath accept a Graphics they are not allowed to reference (that type
                // lives in Majorsilence.Forms, which depends on the drawing assembly, not the reverse).
                if (string.Equals(ours.Parameters[i], "Object", StringComparison.Ordinal))
                    continue;
                if (!string.Equals(ours.Parameters[i], wanted[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static string TypeName(Type type) =>
            type.IsByRef || type.IsArray ? TypeName(type.GetElementType()!) + (type.IsArray ? "[]" : "&") : type.Name;
    }

    private static Type? Resolve(Surface surface, Dictionary<string, Type> ourTypes, string ns, string name)
    {
        foreach (var target in surface.NamespaceMap[ns])
            if (ourTypes.TryGetValue($"{target}.{name}", out var exact))
                return exact;

        // Fall back to a same-simple-name match anywhere under Majorsilence.Forms: several upstream
        // types are deliberately declared in a flatter namespace than their original, and that is a
        // naming choice rather than a missing type.
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
    /// Returns the assemblies in the newest non-framework <c>ref</c>/<c>lib</c> folder of a package
    /// whose root was baked in at build time, or nothing when the package is unavailable.
    /// </summary>
    private static string[] PackageAssemblies(string metadataKey)
    {
        var root = PackageRoot(metadataKey);
        var best = root is null ? null : BestFrameworkFolder(root);
        return best is null ? [] : Directory.GetFiles(best, "*.dll");
    }

    private static string? PackageRoot(string key) =>
        typeof(GapScanner).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;

    // Targeting packs put their assemblies under ref/<tfm>; ordinary packages under lib/<tfm>.
    private static string? BestFrameworkFolder(string packageRoot)
    {
        foreach (var container in new[] { "ref", "lib" })
        {
            var path = Path.Combine(packageRoot, container);
            if (!Directory.Exists(path))
                continue;

            var best = Directory.GetDirectories(path, "net*")
                .Where(d => !Path.GetFileName(d).StartsWith("net4", StringComparison.Ordinal))
                .OrderByDescending(d => d, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best is not null)
                return best;
        }
        return null;
    }

    private static string LocateUpstream(Surface surface)
    {
        var root = PackageRoot(surface.PackageRootKey)
            ?? throw new FileNotFoundException($"could not locate the package for the {surface.Name} surface ({surface.PackageRootKey} was not baked in at build time)");

        var folder = BestFrameworkFolder(root)
            ?? throw new FileNotFoundException($"no ref/ or lib/ framework folder under {root}");

        var candidate = Path.Combine(folder, surface.UpstreamAssembly);
        return File.Exists(candidate)
            ? candidate
            : throw new FileNotFoundException($"{surface.UpstreamAssembly} not found under {folder}");
    }
}
