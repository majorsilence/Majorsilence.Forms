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

/// <summary>Which engine rewrites source files.</summary>
internal enum SourceEngine
{
    /// <summary>
    /// The default, fast, regex-based textual rewriter (<see cref="SourceConverter"/>). Tolerates
    /// non-compiling code and needs no project to load, at the cost of not being able to tell apart two
    /// same-named types (e.g. a custom <c>Panel</c> vs. <c>System.Windows.Forms.Panel</c>).
    /// </summary>
    Text,

    /// <summary>
    /// Opt-in, heavier engine using real Roslyn symbol resolution (<see cref="RoslynSourceConverter"/>).
    /// Requires an evaluable project (loaded via MSBuildWorkspace) and is orders of magnitude slower, but
    /// correctly disambiguates same-named types. Falls back to <see cref="Text"/> per-project on load
    /// failure, or for the whole run when the input has no project to load.
    /// </summary>
    Roslyn,
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

    /// <summary>
    /// Skip the <c>.bak</c> copy normally left beside each changed file during an in-place conversion.
    /// Useful when the source is under version control (git already preserves the originals). No effect
    /// when <see cref="Output"/> is set — that mode never touches the originals.
    /// </summary>
    public bool NoBackup { get; init; }

    /// <summary>Print a unified diff for each changed file.</summary>
    public bool ShowDiff { get; init; }

    public Backend Backend { get; init; } = Backend.Avalonia;

    public ReferenceMode ReferenceMode { get; init; } = ReferenceMode.Package;

    /// <summary>
    /// Which engine rewrites source files. Defaults to <see cref="SourceEngine.Text"/> — zero behavior
    /// change for every existing caller/test that doesn't set this.
    /// </summary>
    public SourceEngine Engine { get; init; } = SourceEngine.Text;

    /// <summary>
    /// Target framework for converted projects. When null (the default) the converter keeps each
    /// project's .NET version and only drops the Windows desktop platform suffix
    /// (<c>net8.0-windows</c> → <c>net8.0</c>). Set it (via <c>--tfm</c>) to force one exact framework.
    /// </summary>
    public string? TargetFramework { get; init; }

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
    public IReadOnlyList<string> MapFiles { get; init; } = [];
}
