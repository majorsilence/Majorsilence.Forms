using System.Reflection;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// The Majorsilence.Forms NuGet version the migrator writes into converted projects.
/// </summary>
/// <remarks>
/// Read off this assembly rather than hard-coded, because the migrator ships from the same repo (and
/// the same <c>Directory.Build.props</c> <c>Version</c>) as the packages it adds references to. A
/// literal here silently goes stale: a shipped tool would keep emitting an old
/// <c>&lt;PackageReference Version="…"&gt;</c> long after that version stopped matching the tool. Override
/// per-run with <c>--package-version</c> when migrating against a different release.
/// </remarks>
internal static class MigratorVersion
{
    /// <summary>
    /// The version string, e.g. <c>26.0.27</c>. Derived from
    /// <see cref="AssemblyInformationalVersionAttribute"/>, whose value SourceLink suffixes with
    /// <c>+&lt;commit sha&gt;</c> on CI builds — that build metadata is stripped, since it isn't part of
    /// the NuGet version. Falls back to the assembly version if the attribute is somehow absent.
    /// </summary>
    public static string MajorsilenceFormsPackageVersion { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = typeof(MigratorVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
