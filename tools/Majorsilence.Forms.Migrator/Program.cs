using Majorsilence.Forms.Migrator;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

string? input = null;
string? output = null;
var dryRun = false;
var showDiff = false;
var backend = Backend.Avalonia;
var referenceMode = ReferenceMode.Package;
var targetFramework = "net10.0";
var packageVersion = "0.3.0";
string? repoRoot = null;
var strict = false;
var noReport = false;
string? reportPath = null;
var mapFiles = new List<string>();

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    switch (arg)
    {
        case "--output" or "-o":
            output = Next(ref i);
            break;
        case "--dry-run" or "-n":
            dryRun = true;
            break;
        case "--diff":
            showDiff = true;
            break;
        case "--backend":
            if (!TryParseBackend(Next(ref i), out backend))
                return Fail($"unknown backend (expected avalonia|uno|headless)");
            break;
        case "--references":
            referenceMode = Next(ref i).Equals("project", StringComparison.OrdinalIgnoreCase)
                ? ReferenceMode.Project
                : ReferenceMode.Package;
            break;
        case "--tfm":
            targetFramework = Next(ref i);
            break;
        case "--package-version":
            packageVersion = Next(ref i);
            break;
        case "--repo-root":
            repoRoot = Next(ref i);
            break;
        case "--strict":
            strict = true;
            break;
        case "--no-report":
            noReport = true;
            break;
        case "--report":
            reportPath = Next(ref i);
            break;
        case "--map":
            mapFiles.Add(Next(ref i));
            break;
        default:
            if (arg.StartsWith('-'))
                return Fail($"unknown option: {arg}");
            if (input is not null)
                return Fail($"unexpected extra argument: {arg}");
            input = arg;
            break;
    }
}

if (input is null)
    return Fail("missing required <input> (a .sln, .csproj/.vbproj, directory, or .cs/.vb/.resx file)");

var options = new MigrationOptions
{
    Input = Path.GetFullPath(input),
    Output = output is null ? null : Path.GetFullPath(output),
    DryRun = dryRun,
    ShowDiff = showDiff,
    Backend = backend,
    ReferenceMode = referenceMode,
    TargetFramework = targetFramework,
    PackageVersion = packageVersion,
    RepoRoot = repoRoot,
    Strict = strict,
    NoReport = noReport,
    ReportPath = reportPath is null ? null : Path.GetFullPath(reportPath),
    MapFiles = mapFiles.Select(Path.GetFullPath).ToArray(),
};

return new Migrator(options).Run();

string Next(ref int i)
{
    if (i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"error: {args[i]} expects a value");
        Environment.Exit(2);
    }
    return args[++i];
}

static bool TryParseBackend(string value, out Backend backend)
{
    switch (value.ToLowerInvariant())
    {
        case "avalonia": backend = Backend.Avalonia; return true;
        case "uno": backend = Backend.Uno; return true;
        case "headless": backend = Backend.Headless; return true;
        default: backend = Backend.Avalonia; return false;
    }
}

static int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    Console.Error.WriteLine("run with --help for usage.");
    return 2;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        majorsilence-migrate — convert WinForms source & projects to Majorsilence.Forms

        USAGE
          majorsilence-migrate <input> [options]

          <input>   A .sln, .csproj/.vbproj, a directory, or a single .cs/.vb/.resx file.

        OPTIONS
          -o, --output <dir>      Write converted files to <dir> (mirrors the input tree).
                                  Omit to convert in place (a .bak is left beside each changed file).
          -n, --dry-run           Report what would change without writing anything.
              --diff              Print a unified diff for each changed file.
              --backend <name>    Platform backend to reference: avalonia (default) | uno | headless.
              --references <mode>  How to reference Majorsilence.Forms: package (default) | project.
              --tfm <tfm>         Target framework for converted projects (default: net10.0).
              --package-version <v>  NuGet version for package references (default: 0.3.0).
              --repo-root <dir>   Repo root for resolving --references project paths (default: cwd).
              --map <file>        JSON file of extra namespace mappings (repeatable, e.g. Telerik).
              --strict            Exit non-zero if any manual-review warning is produced (CI gate).
              --report <file>     Path for the Markdown report (default: migration-report.md by output).
              --no-report         Do not write the migration report.
          -h, --help              Show this help.

        WHAT IT DOES
          * Project files: removes UseWindowsForms/UseWPF, retargets the TFM, drops the
            Windows-desktop framework reference, and adds Majorsilence.Forms + a backend reference.
          * Source files: rewrites System.Windows.Forms -> Majorsilence.Forms and
            System.Drawing[.*] -> Majorsilence.Drawing[.*]. APIs with no equivalent are flagged
            as warnings for manual review.

        MAP FILE FORMAT (JSON)
          { "namespaces": { "Telerik.WinControls.UI": "Majorsilence.Forms.Telerik" } }

        EXAMPLES
          majorsilence-migrate ./LegacyApp --dry-run
          majorsilence-migrate ./LegacyApp.csproj -o ./Converted --backend avalonia
          majorsilence-migrate ./LegacyApp --map telerik.json --strict
        """);
}
