using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Owns the <see cref="MSBuildWorkspace"/> backing <c>--engine roslyn</c>: registers the MSBuild toolchain,
/// loads the solution/project(s) named by <see cref="MigrationOptions.Input"/> once per run, and exposes a
/// per-file <see cref="Document"/> lookup. Failure is handled at two granularities:
/// <list type="bullet">
///   <item>The <b>whole workspace</b> can't be constructed at all (no MSBuild locatable) — this is a hard,
///   process-ending error; see <see cref="TryCreate"/>.</item>
///   <item>An <b>individual project</b> fails to load (reported via <see cref="Workspace.WorkspaceFailed"/>,
///   which is how <see cref="MSBuildWorkspace"/> mostly communicates problems rather than throwing) — every
///   file under that project alone falls back to the text engine, tracked via <see cref="ProjectLoadFailed"/>.</item>
/// </list>
/// A bare directory or single-file input with no <c>.sln</c>/<c>.csproj</c>/<c>.vbproj</c> to load is not a
/// failure of this type at all — <see cref="Migrator"/> checks for that case before ever constructing a
/// context (see <see cref="HasLoadableProject"/>) and falls back to the text engine for the whole run.
/// </summary>
internal sealed class RoslynWorkspaceContext : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private readonly Dictionary<string, DocumentId> _documentsByPath;
    private readonly HashSet<string> _failedProjectPaths;

    private RoslynWorkspaceContext(MSBuildWorkspace workspace, Dictionary<string, DocumentId> documentsByPath,
        HashSet<string> failedProjectPaths)
    {
        _workspace = workspace;
        _documentsByPath = documentsByPath;
        _failedProjectPaths = failedProjectPaths;
    }

    /// <summary>
    /// True when <paramref name="input"/> is something Roslyn can actually load (a solution or project
    /// file). A bare directory or a single <c>.cs</c>/<c>.vb</c>/<c>.resx</c> file has no project to load,
    /// so the caller should warn once and fall back to the text engine for the whole run rather than ever
    /// calling <see cref="TryCreate"/>.
    /// </summary>
    public static bool HasLoadableProject(string input)
    {
        if (!File.Exists(input))
            return false;
        var ext = Path.GetExtension(input);
        return ext.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".vbproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registers the MSBuild toolchain (if not already registered — it can only happen once per process)
    /// and loads <paramref name="input"/> (a <c>.sln</c>, <c>.csproj</c>, or <c>.vbproj</c>). Returns
    /// <c>null</c> with a <paramref name="error"/> only when the workspace itself could not be constructed
    /// at all (e.g. no MSBuild/.NET SDK locatable) — the caller should treat that as a hard, process-ending
    /// error, not a per-project fallback.
    /// </summary>
    public static RoslynWorkspaceContext? TryCreate(string input, out string? error)
    {
        try
        {
            if (MSBuildLocator.CanRegister)
                MSBuildLocator.RegisterDefaults();
        }
        catch (Exception ex)
        {
            error = $"could not locate an MSBuild/.NET SDK installation required by --engine roslyn: {ex.Message}";
            return null;
        }

        MSBuildWorkspace workspace;
        try
        {
            workspace = MSBuildWorkspace.Create();
        }
        catch (Exception ex)
        {
            error = $"could not construct an MSBuild workspace required by --engine roslyn: {ex.Message}";
            return null;
        }

        var failedProjectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // MSBuildWorkspace reports load problems here rather than throwing. We can't always recover the
        // originating project path from the diagnostic alone, so failures are also cross-checked against
        // the per-project OpenProjectAsync try/catch below — this handler's job is mainly to surface the
        // message. RegisterWorkspaceFailedHandler is the non-obsolete replacement for the WorkspaceFailed
        // event (which no longer runs on a UI thread by default — irrelevant here, this is a console app).
        workspace.RegisterWorkspaceFailedHandler(e =>
            Console.Error.WriteLine($"warning: [roslyn] workspace load issue: {e.Diagnostic.Message}"));

        try
        {
            var ext = Path.GetExtension(input);
            var projectPaths = ext.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                ? SolutionReader.ProjectPaths(input)
                : [input];

            foreach (var projectPath in projectPaths)
            {
                try
                {
                    workspace.OpenProjectAsync(projectPath).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    // A specific project failed to load — fall back to the text engine for every file under
                    // that project only, rather than failing the whole run.
                    failedProjectPaths.Add(Path.GetFullPath(projectPath));
                    Console.Error.WriteLine($"warning: [roslyn] project failed to load, falling back to the " +
                        $"text engine for its files: {projectPath} ({ex.Message})");
                }
            }
        }
        catch (Exception ex)
        {
            workspace.Dispose();
            error = $"could not load the solution/project(s) under --engine roslyn: {ex.Message}";
            return null;
        }

        var documentsByPath = new Dictionary<string, DocumentId>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in workspace.CurrentSolution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath is not null)
                    documentsByPath[Path.GetFullPath(document.FilePath)] = document.Id;
            }
        }

        error = null;
        return new RoslynWorkspaceContext(workspace, documentsByPath, failedProjectPaths);
    }

    /// <summary>The live, current solution — rewrites are applied against (and update) this.</summary>
    public Solution CurrentSolution => _workspace.CurrentSolution;

    /// <summary>Applies an updated solution back to the workspace (mirrors <see cref="Document"/> edits made via a syntax rewrite).</summary>
    public bool TryApplyChanges(Solution newSolution) => _workspace.TryApplyChanges(newSolution);

    /// <summary>
    /// Looks up the Roslyn <see cref="Document"/> for a source file path. Returns <c>false</c> with a
    /// <paramref name="failureReason"/> when the path either belongs to a project that failed to load
    /// (per-project fallback — see <see cref="RoslynWorkspaceContext"/>'s summary) or was never part of any
    /// loaded project at all (e.g. an orphaned file outside every project's compile-item list).
    /// </summary>
    public bool TryGetDocument(string path, out Document? document, out string? failureReason)
    {
        var full = Path.GetFullPath(path);
        if (_documentsByPath.TryGetValue(full, out var id))
        {
            document = CurrentSolution.GetDocument(id);
            failureReason = document is null ? "document could not be resolved from the current solution" : null;
            return document is not null;
        }

        document = null;
        failureReason = IsUnderFailedProject(full)
            ? "belongs to a project that failed to load under --engine roslyn"
            : "not part of any project loaded under --engine roslyn (not a compile item, or an orphaned file)";
        return false;
    }

    private bool IsUnderFailedProject(string path)
    {
        foreach (var projectPath in _failedProjectPaths)
        {
            var projectDir = Path.GetDirectoryName(projectPath);
            if (projectDir is not null && path.StartsWith(projectDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public void Dispose() => _workspace.Dispose();
}
