using System.Text.RegularExpressions;
using System.Xml.Linq;

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

    // Packages that need a central <PackageVersion> entry, grouped by the governing
    // Directory.Packages.props they belong to. Populated as projects under central package management
    // are converted, then flushed once per props file by UpdateCentralPackageFiles().
    private readonly Dictionary<string, HashSet<string>> _centralPackagesByProps = new(StringComparer.OrdinalIgnoreCase);

    // Custom .props/.targets files reached via an <Import> in a project, already processed for the
    // -windows TFM strip. A file imported by several projects is only rewritten once.
    private readonly HashSet<string> _processedImports = new(StringComparer.OrdinalIgnoreCase);

    // Per VB-file decision on injecting the explicit constructor MyType=Empty removes, computed across
    // each form's partial files. Files absent from the map fall back to the single-file Auto heuristic.
    private Dictionary<string, VbConstructorMode> _vbConstructorPlan = new(StringComparer.OrdinalIgnoreCase);

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

        // Decide, across each VB form's partial files (Form1.vb + Form1.Designer.vb), whether a
        // constructor needs injecting and into which file — so we never duplicate one a sibling already
        // has, nor write one into a designer file.
        _vbConstructorPlan = PlanVbConstructors(sourceFiles);

        Console.WriteLine($"Majorsilence.Forms migrator");
        Console.WriteLine($"  input    : {_options.Input}");
        Console.WriteLine($"  output   : {_options.Output ?? "(in place)"}");
        Console.WriteLine($"  backend  : {_options.Backend}");
        Console.WriteLine($"  refs     : {_options.ReferenceMode}");
        Console.WriteLine($"  mode     : {(_options.DryRun ? "DRY RUN (no files written)" : "write")}");
        Console.WriteLine();

        foreach (var proj in projects)
            ConvertProject(proj);

        UpdateCentralPackageFiles();

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

        // If a Directory.Packages.props governs this project and central management is on, the converter
        // must emit version-less PackageReferences and we pin the versions in that props file instead.
        var propsPath = CentralPackageManagement.Find(Path.GetDirectoryName(path)!);
        var useCpm = propsPath is not null && CentralPackageManagement.IsEnabled(File.ReadAllText(propsPath));

        // Built-in WinForms-only package patterns plus any the user added via a --map file.
        var removePackages = WinFormsPackages.DefaultPatterns.Concat(_customMap.RemovePackages).ToList();

        // Only wire in Majorsilence.Forms when the project actually is/uses WinForms — a non-UI project in
        // a solution (a data/service library) shouldn't gain a UI-framework dependency it never needed.
        var addReferences = ProjectUsesWinForms(Path.GetDirectoryName(path)!, xml);

        var result = ProjectConverter.Convert(xml, _options, Path.GetDirectoryName(path)!, isVb, useCpm, removePackages, addReferences);

        foreach (var w in result.Warnings)
            _warnings.Add($"{Rel(path)}: {w}");

        // Custom .props/.targets imported by the project can carry the -windows TFM too; strip it there
        // as well. Done from the original XML and independent of whether the project file itself changed.
        ProcessImportedProps(path, xml);

        if (!result.Changed)
            return;

        if (useCpm && result.AddedPackages.Count > 0)
        {
            if (!_centralPackagesByProps.TryGetValue(propsPath!, out var set))
                _centralPackagesByProps[propsPath!] = set = new(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in result.AddedPackages)
                set.Add(pkg);
        }

        _changes.Add(("proj", Rel(path)));
        Console.WriteLine($"  [proj] {Rel(path)}");
        MaybePrintDiff(Rel(path), xml, result.Xml);
        WriteResult(path, result.Xml);
    }

    /// <summary>
    /// Pins the migrated packages' versions in each governing <c>Directory.Packages.props</c> that was
    /// found while converting projects under central package management. Each props file is written once,
    /// with the union of packages added across the projects it governs.
    /// </summary>
    private void UpdateCentralPackageFiles()
    {
        foreach (var (propsPath, ids) in _centralPackagesByProps)
        {
            var xml = File.ReadAllText(propsPath);
            var packages = ids.Select(id => (id, _options.PackageVersion));
            var (updated, changed) = CentralPackageManagement.EnsureVersions(xml, packages);
            if (!changed)
                continue;

            _changes.Add(("props", Rel(propsPath)));
            Console.WriteLine($"  [props] {Rel(propsPath)}");
            MaybePrintDiff(Rel(propsPath), xml, updated);

            if (_options.DryRun)
                continue;

            var destination = MirroredDestination(propsPath);
            if (destination is null)
            {
                _warnings.Add($"{Rel(propsPath)}: central package file is outside the migrated output tree — " +
                    $"add PackageVersion entries for {string.Join(", ", ids)} manually");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
            if (_options.Output is null && !_options.NoBackup)
            {
                // In-place: keep a one-time backup, matching how source/project files are handled.
                var backup = propsPath + ".bak";
                if (!File.Exists(backup))
                    File.Copy(propsPath, backup);
            }
            File.WriteAllText(destination, updated);
        }
    }

    /// <summary>
    /// Where an auxiliary file (a <c>Directory.Packages.props</c> or a project-imported
    /// <c>.props</c>/<c>.targets</c>) should be written. In place it's the file itself; with
    /// <c>--output</c> it's mirrored to the same relative location — unless the file lives above the
    /// migrated input tree (a shared, repo-wide file), in which case there's no safe mirror target and
    /// the caller warns instead.
    /// </summary>
    private string? MirroredDestination(string path)
    {
        if (_options.Output is null)
            return path;

        var inputRoot = Directory.Exists(_options.Input)
            ? _options.Input
            : Path.GetDirectoryName(Path.GetFullPath(_options.Input))!;
        var relative = Path.GetRelativePath(Path.GetFullPath(inputRoot), Path.GetFullPath(path));
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            return null;
        return Path.Combine(_options.Output, relative);
    }

    /// <summary>
    /// Strips the <c>-windows</c> TFM suffix from any custom <c>.props</c>/<c>.targets</c> the project pulls
    /// in via <c>&lt;Import Project="…" /&gt;</c>. Each imported file is rewritten once even if several
    /// projects share it. (MSBuild's implicit <c>Directory.Build.props</c>/<c>Directory.Packages.props</c>
    /// aren't <c>&lt;Import&gt;</c>ed, so they're left to their own handling.)
    /// </summary>
    private void ProcessImportedProps(string projectPath, string projectXml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(projectXml); }
        catch (System.Xml.XmlException) { return; }

        var projectDir = Path.GetDirectoryName(projectPath)!;
        foreach (var import in doc.Descendants().Where(e => e.Name.LocalName == "Import").ToList())
        {
            var raw = import.Attribute("Project")?.Value;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var resolved = ResolveImportPath(raw!, projectDir);
            if (resolved is null)
                continue;

            var ext = Path.GetExtension(resolved);
            if (!ext.Equals(".props", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".targets", StringComparison.OrdinalIgnoreCase))
                continue;

            if (_processedImports.Add(resolved))
                ConvertImportedProps(resolved);
        }
    }

    private void ConvertImportedProps(string path)
    {
        _filesScanned++;
        var xml = File.ReadAllText(path);
        var (updated, changed) = TargetFrameworkRewriter.StripWindowsFromDocument(xml);
        if (!changed)
            return;

        _changes.Add(("props", Rel(path)));
        Console.WriteLine($"  [props] {Rel(path)}");
        MaybePrintDiff(Rel(path), xml, updated);
        PersistFile(path, updated);
    }

    /// <summary>
    /// Resolves an <c>&lt;Import Project&gt;</c> value to an absolute path, or null when it can't be
    /// evaluated textually (an unresolved <c>$(…)</c> property) or doesn't exist on disk. Only the two
    /// common location properties are substituted; anything else is left for a human.
    /// </summary>
    private static string? ResolveImportPath(string raw, string projectDir)
    {
        var p = raw.Trim().Replace('\\', Path.DirectorySeparatorChar)
            .Replace("$(MSBuildThisFileDirectory)", projectDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            .Replace("$(MSBuildProjectDirectory)", projectDir, StringComparison.OrdinalIgnoreCase);

        if (p.Contains("$(", StringComparison.Ordinal))
            return null;

        if (!Path.IsPathRooted(p))
            p = Path.Combine(projectDir, p);
        p = Path.GetFullPath(p);
        return File.Exists(p) ? p : null;
    }

    /// <summary>
    /// Writes a transformed auxiliary file (mirroring to <c>--output</c> when set, leaving a one-time
    /// <c>.bak</c> for an in-place edit unless <c>--no-backup</c>). No-op in dry-run. Warns when the file
    /// lies outside the migrated output tree.
    /// </summary>
    private void PersistFile(string sourcePath, string content)
    {
        if (_options.DryRun)
            return;

        var destination = MirroredDestination(sourcePath);
        if (destination is null)
        {
            _warnings.Add($"{Rel(sourcePath)}: imported file is outside the migrated output tree — " +
                "apply the same -windows TFM change manually");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        if (_options.Output is null && !_options.NoBackup)
        {
            var backup = sourcePath + ".bak";
            if (!File.Exists(backup))
                File.Copy(sourcePath, backup);
        }
        File.WriteAllText(destination, content);
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
        var vbConstructor = _vbConstructorPlan.GetValueOrDefault(path, VbConstructorMode.Auto);
        var result = SourceConverter.Convert(text, _customMap, language, vbConstructor);

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

    private static readonly Regex VbConstructor = new(@"(?i)\bSub\s+New\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Works out, for every VB source file, whether it should receive the explicit constructor that
    /// <c>MyType=Empty</c> removes. A form is the set of partial files sharing a base name
    /// (<c>Form1.vb</c> + <c>Form1.Designer.vb</c>); the constructor is injected only when the form uses
    /// <c>InitializeComponent</c> and <b>no</b> partial already declares a <c>Sub New(...)</c>, and only
    /// into the code-behind (never a designer file). Every other VB file is told to suppress.
    /// </summary>
    private Dictionary<string, VbConstructorMode> PlanVbConstructors(List<string> sourceFiles)
    {
        var plan = new Dictionary<string, VbConstructorMode>(StringComparer.OrdinalIgnoreCase);

        var vbFiles = sourceFiles
            .Where(p => Path.GetExtension(p).Equals(".vb", StringComparison.OrdinalIgnoreCase))
            .Where(p => !IsExcludedVbDesignerFile(p));

        foreach (var group in vbFiles.GroupBy(FormKey, StringComparer.OrdinalIgnoreCase))
        {
            var files = group.ToList();
            var texts = files.ToDictionary(f => f, File.ReadAllText, StringComparer.OrdinalIgnoreCase);

            var usesInitializeComponent = texts.Values.Any(t => t.Contains("InitializeComponent", StringComparison.Ordinal));
            var hasConstructor = texts.Values.Any(t => VbConstructor.IsMatch(t));

            var target = usesInitializeComponent && !hasConstructor ? ChooseConstructorTarget(files) : null;

            foreach (var f in files)
                plan[f] = string.Equals(f, target, StringComparison.OrdinalIgnoreCase)
                    ? VbConstructorMode.Inject
                    : VbConstructorMode.Suppress;
        }

        return plan;
    }

    // The form a partial file belongs to: its directory + base name, minus a trailing ".Designer".
    private static string FormKey(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path); // "Form1" or "Form1.Designer"
        if (name.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
            name = name[..^".Designer".Length];
        return Path.Combine(Path.GetDirectoryName(path) ?? "", name);
    }

    // Prefer the code-behind (a non-designer partial) so designer files stay regenerable; fall back to a
    // designer file only when that's the form's only file. Deterministic by path.
    private static string ChooseConstructorTarget(List<string> files)
    {
        var ordered = files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        return ordered.FirstOrDefault(f => !IsDesignerFile(f)) ?? ordered[0];
    }

    private static bool IsDesignerFile(string path) =>
        Path.GetFileNameWithoutExtension(path).EndsWith(".Designer", StringComparison.OrdinalIgnoreCase);

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
        var xml = File.ReadAllText(path);
        var result = ResxScanner.Scan(xml);

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

        // The one .resx per VB project that backs classic My.Resources — generate the companion
        // accessor module that keeps every 'My.Resources.X' call site compiling once the real
        // compiler-generated My Project\Resources.Designer.vb is excluded from the build.
        if (MyResourcesGenerator.IsMyResourcesResx(path))
            GenerateMyResourcesAccessor(path, xml);
    }

    private void GenerateMyResourcesAccessor(string resxPath, string resxXml)
    {
        var resources = MyResourcesGenerator.ParseResources(resxXml);
        var generated = MyResourcesGenerator.Generate(resources, resxXml);

        var targetPath = Path.Combine(Path.GetDirectoryName(resxPath)!, "Resources.vb");
        var existing = File.Exists(targetPath) ? File.ReadAllText(targetPath) : null;
        if (string.Equals(existing, generated, StringComparison.Ordinal))
            return; // idempotent: unchanged .resx regenerates byte-identical output, nothing to do.

        _changes.Add(("src", Rel(targetPath)));
        Console.WriteLine($"  [src ] {Rel(targetPath)} (generated My.Resources accessor, {resources.Count} resource(s))");

        if (_options.DryRun)
            return;

        // Unlike WriteResult, there is no ".bak"-worthy original here on the first run — this file is
        // wholly generated, not migrated in place from user content — so back it up only when overwriting
        // a previous run's output.
        var destination = DestinationFor(targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (_options.Output is null && !_options.NoBackup && File.Exists(targetPath))
        {
            var backup = targetPath + ".bak";
            if (!File.Exists(backup))
                File.Copy(targetPath, backup);
        }
        File.WriteAllText(destination, generated);
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

        if (_options.Output is null && !_options.NoBackup)
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

    private static readonly string[] ProjectGlobs = ["*.csproj", "*.vbproj"];

    /// <summary>
    /// True when the project is a WinForms project or contains WinForms code — the test for whether to
    /// wire in Majorsilence.Forms. Looks at the project XML (UseWindowsForms, a System.Windows.Forms
    /// assembly Reference or VB project import) and, failing that, scans the project's source for any
    /// <c>System.Windows.Forms</c> usage.
    /// </summary>
    private bool ProjectUsesWinForms(string projectDir, string projectXml) =>
        XmlSignalsWinForms(projectXml) || SourceSignalsWinForms(projectDir);

    private static bool XmlSignalsWinForms(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (System.Xml.XmlException) { return false; }

        foreach (var e in doc.Descendants()) {
            switch (e.Name.LocalName) {
                case "UseWindowsForms" when e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase):
                    return true;
                // VB project-level global import: <Import Include="System.Windows.Forms" />
                case "Import" when (e.Attribute("Include")?.Value ?? "").Equals("System.Windows.Forms", StringComparison.OrdinalIgnoreCase):
                    return true;
                // Legacy assembly reference: <Reference Include="System.Windows.Forms" />
                case "Reference" when (e.Attribute("Include")?.Value ?? "").StartsWith("System.Windows.Forms", StringComparison.OrdinalIgnoreCase):
                    return true;
            }
        }

        return false;
    }

    private bool SourceSignalsWinForms(string projectDir)
    {
        if (!Directory.Exists(projectDir))
            return false;

        foreach (var pattern in new[] { "*.cs", "*.vb" }) {
            foreach (var file in Directory.EnumerateFiles(projectDir, pattern, SearchOption.AllDirectories)) {
                if (IsInIgnoredDirectory(file) || IsUnderOutput(file))
                    continue;
                try {
                    if (File.ReadAllText(file).Contains("System.Windows.Forms", StringComparison.Ordinal))
                        return true;
                } catch (IOException) {
                    // Unreadable file — ignore for the heuristic.
                }
            }
        }

        return false;
    }

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
