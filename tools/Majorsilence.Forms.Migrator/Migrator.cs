namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Drives a full conversion: discovers the project + source files under the input, applies the
/// project and source transforms, and writes the results (or, in dry-run mode, just reports them).
/// </summary>
internal sealed class Migrator
{
    private readonly MigrationOptions _options;
    private int _filesScanned;
    private readonly List<(string Kind, string Path)> _changes = new();
    private readonly List<string> _warnings = new();
    private CustomMap _customMap = CustomMap.Empty;

    public Migrator(MigrationOptions options) => _options = options;

    public int Run()
    {
        if (!File.Exists(_options.Input) && !Directory.Exists(_options.Input))
        {
            Console.Error.WriteLine($"error: input not found: {_options.Input}");
            return 1;
        }

        try
        {
            _customMap = CustomMap.Load(_options.MapFiles);
        }
        catch (Exception ex) when (ex is FileNotFoundException or FormatException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }

        var projects = DiscoverProjects();
        var sourceFiles = WalkFiles("*.cs", "*.vb");
        var resxFiles = WalkFiles("*.resx");

        Console.WriteLine($"Majorsilence.Forms migrator");
        Console.WriteLine($"  input    : {_options.Input}");
        Console.WriteLine($"  output   : {_options.Output ?? "(in place)"}");
        Console.WriteLine($"  backend  : {_options.Backend}");
        Console.WriteLine($"  refs     : {_options.ReferenceMode}");
        Console.WriteLine($"  mode     : {(_options.DryRun ? "DRY RUN (no files written)" : "write")}");
        Console.WriteLine();

        foreach (var proj in projects)
            ConvertProject(proj);

        foreach (var src in sourceFiles)
            ConvertSource(src);

        foreach (var resx in resxFiles)
            ProcessResx(resx);

        CopySolutionFile();

        Console.WriteLine();
        Console.WriteLine($"Scanned {_filesScanned} file(s); {_changes.Count} would change.");
        if (_warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{_warnings.Count} warning(s):");
            foreach (var w in _warnings)
                Console.WriteLine($"  ! {w}");
        }

        WriteReport();

        // Strict mode turns unresolved manual-review items into a failing exit code so a CI
        // migration gate can block until a human has dealt with them.
        if (_options.Strict && _warnings.Count > 0)
        {
            Console.Error.WriteLine($"\nstrict: {_warnings.Count} warning(s) require manual review.");
            return 3;
        }

        return 0;
    }

    private void WriteReport()
    {
        if (_options.NoReport)
            return;

        var path = _options.ReportPath ?? Path.Combine(ReportDirectory(), "migration-report.md");
        var report = ReportBuilder.Build(_options, _filesScanned, _changes, _warnings);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, report);
        Console.WriteLine($"\nReport written to {path}");
    }

    private string ReportDirectory()
    {
        if (_options.Output is not null)
            return _options.Output;
        return Directory.Exists(_options.Input)
            ? _options.Input
            : Path.GetDirectoryName(Path.GetFullPath(_options.Input))!;
    }

    private void ConvertProject(string path)
    {
        _filesScanned++;
        var xml = File.ReadAllText(path);
        var isVb = Path.GetExtension(path).Equals(".vbproj", StringComparison.OrdinalIgnoreCase);
        var result = ProjectConverter.Convert(xml, _options, Path.GetDirectoryName(path)!, isVb);

        foreach (var w in result.Warnings)
            _warnings.Add($"{Rel(path)}: {w}");

        if (!result.Changed)
            return;

        _changes.Add(("proj", Rel(path)));
        Console.WriteLine($"  [proj] {Rel(path)}");
        MaybePrintDiff(Rel(path), xml, result.Xml);
        WriteResult(path, result.Xml);
    }

    private void ConvertSource(string path)
    {
        // The VB "My Project" designer files (Resources/Settings/Application .Designer.vb) are excluded
        // from compilation by the project converter (they don't build with MyType=Empty), so there's
        // nothing to convert or flag in them — skip entirely rather than emit false-positive warnings.
        if (IsExcludedVbDesignerFile(path))
            return;

        _filesScanned++;
        var text = File.ReadAllText(path);
        var language = Path.GetExtension(path).Equals(".vb", StringComparison.OrdinalIgnoreCase)
            ? SourceLanguage.VisualBasic
            : SourceLanguage.CSharp;
        var result = SourceConverter.Convert(text, _customMap, language);

        foreach (var w in result.Warnings)
            _warnings.Add($"{Rel(path)}: {w}");

        if (!result.Changed)
            return;

        _changes.Add(("src", Rel(path)));
        Console.WriteLine($"  [src ] {Rel(path)}");
        MaybePrintDiff(Rel(path), text, result.Text);
        WriteResult(path, result.Text);
    }

    // Matches the project converter's `<Compile Remove="My Project\*.Designer.vb" />`: a *.Designer.vb
    // directly under a "My Project" folder (the VB application-framework designer files).
    private static bool IsExcludedVbDesignerFile(string path)
    {
        if (!path.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase))
            return false;
        var dir = Path.GetFileName(Path.GetDirectoryName(path));
        return string.Equals(dir, "My Project", StringComparison.OrdinalIgnoreCase);
    }

    private void MaybePrintDiff(string relPath, string oldText, string newText)
    {
        if (!_options.ShowDiff)
            return;
        var diff = UnifiedDiff.Build(oldText, newText, relPath);
        foreach (var line in diff.Split('\n'))
            if (line.Length > 0)
                Console.WriteLine($"         {line}");
        Console.WriteLine();
    }

    // When the input is a solution and we're mirroring to an output tree, copy the .sln across so the
    // result is immediately openable. Project paths in the .sln are relative to its folder, which the
    // mirrored output preserves, so no rewrite is needed.
    private void CopySolutionFile()
    {
        if (_options.Output is null || _options.DryRun)
            return;
        if (!File.Exists(_options.Input) ||
            !Path.GetExtension(_options.Input).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            return;

        var destination = DestinationFor(_options.Input);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(_options.Input, destination, overwrite: true);
        Console.WriteLine($"  [sln ] {Path.GetFileName(_options.Input)}");
    }

    private void ProcessResx(string path)
    {
        _filesScanned++;
        var result = ResxScanner.Scan(File.ReadAllText(path));

        // Designer values, primitive metadata, and bytearray images all load cross-platform via
        // Majorsilence.Forms.ComponentResourceManager — mirror the .resx into the output tree so the
        // converted project keeps them and the resource manager can read them at runtime.
        if (result.HasConsumableResources)
        {
            _changes.Add(("resx", Rel(path)));
            Console.WriteLine($"  [resx] {Rel(path)}");
            CopyResxToOutput(path);
        }

        // What's left needs a human, with guidance specific to what the blob actually is.
        if (result.ActiveXBlobCount > 0)
            _warnings.Add($"{Rel(path)}: contains {result.ActiveXBlobCount} ActiveX/COM control state(s) " +
                "(AxHost) — ActiveX is Windows-COM-only with no managed equivalent; replace the control with " +
                "a managed one and drop the serialized OcxState");

        if (result.BinaryResourceCount > 0)
            _warnings.Add($"{Rel(path)}: contains {result.BinaryResourceCount} BinaryFormatter/SOAP-serialized " +
                "object(s) of an unsupported type — re-create the value in code (BinaryFormatter is gone from " +
                "modern .NET)");
    }

    // Unlike source/project files, a .resx is not rewritten; copy it verbatim into the output tree.
    private void CopyResxToOutput(string path)
    {
        if (_options.DryRun || _options.Output is null)
            return;
        var destination = DestinationFor(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(path, destination, overwrite: true);
    }

    private void WriteResult(string sourcePath, string content)
    {
        if (_options.DryRun)
            return;

        var destination = DestinationFor(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (_options.Output is null)
        {
            // In-place: keep a one-time backup so the edit is reversible.
            var backup = sourcePath + ".bak";
            if (!File.Exists(backup))
                File.Copy(sourcePath, backup);
        }

        File.WriteAllText(destination, content);
    }

    /// <summary>
    /// When writing to a separate output directory, mirror the input's tree so relative layout
    /// (and thus project references) are preserved.
    /// </summary>
    private string DestinationFor(string sourcePath)
    {
        if (_options.Output is null)
            return sourcePath;

        var inputRoot = Directory.Exists(_options.Input)
            ? _options.Input
            : Path.GetDirectoryName(Path.GetFullPath(_options.Input))!;

        var relative = Path.GetRelativePath(inputRoot, sourcePath);
        return Path.Combine(_options.Output, relative);
    }

    private List<string> DiscoverProjects()
    {
        if (File.Exists(_options.Input))
        {
            return Path.GetExtension(_options.Input).ToLowerInvariant() switch
            {
                ".csproj" or ".vbproj" => new() { _options.Input },
                ".sln" => SolutionReader.ProjectPaths(_options.Input).ToList(),
                _ => new(),
            };
        }

        return ProjectGlobs
            .SelectMany(pat => Directory.EnumerateFiles(_options.Input, pat, SearchOption.AllDirectories))
            .Where(p => !IsInIgnoredDirectory(p))
            .ToList();
    }

    private static readonly string[] ProjectGlobs = { "*.csproj", "*.vbproj" };

    /// <summary>The directories to walk for source/resource files, derived from the input kind.</summary>
    private List<string> InputRoots()
    {
        if (!File.Exists(_options.Input))
            return new() { _options.Input };

        return Path.GetExtension(_options.Input).ToLowerInvariant() switch
        {
            ".csproj" or ".vbproj" => new() { Path.GetDirectoryName(Path.GetFullPath(_options.Input))! },
            ".sln" => SolutionReader.ProjectPaths(_options.Input)
                .Select(p => Path.GetDirectoryName(Path.GetFullPath(p))!)
                .ToList(),
            _ => new(),
        };
    }

    private List<string> WalkFiles(params string[] patterns)
    {
        // Single-file input: convert just that file when its extension matches one of the patterns.
        if (File.Exists(_options.Input))
        {
            var ext = Path.GetExtension(_options.Input);
            if (patterns.Any(p => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                return new() { _options.Input };
        }

        return InputRoots()
            .SelectMany(r => patterns.SelectMany(pat => Directory.EnumerateFiles(r, pat, SearchOption.AllDirectories)))
            .Where(p => !IsInIgnoredDirectory(p) && !IsUnderOutput(p))
            .Distinct()
            .ToList();
    }

    // When --output is nested inside the input tree, never re-scan our own generated files.
    private bool IsUnderOutput(string path)
    {
        if (_options.Output is null)
            return false;
        var output = Path.GetFullPath(_options.Output);
        var full = Path.GetFullPath(path);
        return full.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static bool IsInIgnoredDirectory(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is "bin" or "obj" or ".git" or ".vs");
    }

    private string Rel(string path)
    {
        var baseDir = Directory.Exists(_options.Input)
            ? _options.Input
            : Path.GetDirectoryName(Path.GetFullPath(_options.Input))!;
        return Path.GetRelativePath(baseDir, path);
    }
}
