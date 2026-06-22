namespace Majorsilence.Forms.Migrator;

/// <summary>Which Majorsilence.Forms platform backend the converted project should reference.</summary>
internal enum Backend
{
    Avalonia,
    Uno,
    Headless,
}

/// <summary>How the converted project should pull in Majorsilence.Forms.</summary>
internal enum ReferenceMode
{
    /// <summary>Add <c>&lt;PackageReference&gt;</c> entries (the default for external projects).</summary>
    Package,

    /// <summary>Add <c>&lt;ProjectReference&gt;</c> entries (for projects living inside this repo).</summary>
    Project,
}

internal sealed class MigrationOptions
{
    /// <summary>A .sln, .csproj, or directory to convert.</summary>
    public required string Input { get; init; }

    /// <summary>
    /// Destination directory. When null the conversion is applied in place (a <c>.bak</c> copy is left
    /// beside every file that changes).
    /// </summary>
    public string? Output { get; init; }

    /// <summary>Report what would change without writing anything.</summary>
    public bool DryRun { get; init; }

    /// <summary>Print a unified diff for each changed file.</summary>
    public bool ShowDiff { get; init; }

    public Backend Backend { get; init; } = Backend.Avalonia;

    public ReferenceMode ReferenceMode { get; init; } = ReferenceMode.Package;

    /// <summary>Target framework written into converted project files.</summary>
    public string TargetFramework { get; init; } = "net10.0";

    /// <summary>NuGet version used when <see cref="ReferenceMode"/> is <see cref="ReferenceMode.Package"/>.</summary>
    public string PackageVersion { get; init; } = "0.3.0";

    /// <summary>
    /// Repo root used to compute relative paths when <see cref="ReferenceMode"/> is
    /// <see cref="ReferenceMode.Project"/>. Defaults to the current directory.
    /// </summary>
    public string? RepoRoot { get; init; }

    /// <summary>Return a non-zero exit code when any manual-review warning is produced (CI gate).</summary>
    public bool Strict { get; init; }

    /// <summary>Suppress writing the <c>migration-report.md</c> summary.</summary>
    public bool NoReport { get; init; }

    /// <summary>Explicit path for the migration report; defaults to <c>migration-report.md</c> beside the output/input.</summary>
    public string? ReportPath { get; init; }

    /// <summary>Additional namespace/type mapping files (JSON) layered on top of the built-in rules.</summary>
    public IReadOnlyList<string> MapFiles { get; init; } = Array.Empty<string>();
}
