using System.Globalization;
using System.Text;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Majorsilence.Forms.Benchmarks;

// The allocation regression gate CI runs (`-- --alloc-gate`). Timings vary with the machine and are never
// gated; managed bytes allocated per operation are measured after warmup and are the same run to run, so a
// change that makes a gated benchmark allocate more fails the build until the baseline is regenerated on
// purpose (`-- --alloc-gate --write`, or MAJORSILENCE_WRITE_ALLOC_BASELINE=1).
//
// Only benchmarks tagged [BenchmarkCategory (AllocationGate.Category)] take part. Tag one only after two
// runs in a row give the same number: a benchmark whose allocation depends on a first-call cache or on the
// thread pool does not belong here.
internal static class AllocationGate
{
    public const string Category = "AllocGate";

    private const string BaselineFileName = "AllocationBaseline.txt";

    // A rise is allowed up to this much before it counts: the larger of 5 % and 64 bytes, so that a
    // benchmark allocating a few hundred bytes is not failed by one boxed value, and a large one is not
    // excused a whole extra buffer.
    private const double RelativeTolerance = 0.05;
    private const long AbsoluteTolerance = 64;

    public static int Run (string[] args)
    {
        var write = args.Contains ("--write") || Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_ALLOC_BASELINE") == "1";
        var baselinePath = FindBaseline (args);
        // --filter <glob> narrows the run to some benchmarks (BenchmarkDotNet's own glob syntax, e.g.
        // '*TextMeasure*'): only those are compared, and --write updates only their lines.
        var filterAt = Array.IndexOf (args, "--filter");
        var filter = filterAt >= 0 && filterAt + 1 < args.Length ? args[filterAt + 1] : null;

        var config = ManualConfig.CreateEmpty ()
            .AddLogger (BenchmarkDotNet.Loggers.ConsoleLogger.Default)
            .AddColumnProvider (DefaultColumnProviders.Instance)
            .AddDiagnoser (MemoryDiagnoser.Default)
            .AddFilter (new AnyCategoriesFilter (new[] { Category }))
            .AddFilter (new GlobFilter (new[] { filter ?? "*" }))
            // In-process: one process for the whole set instead of a generated project per benchmark, so
            // the gate takes minutes rather than tens of minutes. The memory diagnoser's numbers are the
            // same either way.
            .AddJob (Job.Default
                .WithToolchain (InProcessEmitToolchain.Instance)
                .WithLaunchCount (1)
                .WithWarmupCount (1)
                .WithIterationCount (3)
                .WithId ("AllocGate"))
            .WithOptions (ConfigOptions.DisableLogFile);

        var summaries = BenchmarkRunner.Run (typeof (AllocationGate).Assembly, config);

        var measured = new SortedDictionary<string, long> (StringComparer.Ordinal);
        foreach (var summary in summaries) {
            foreach (var report in summary.Reports) {
                var name = NameOf (report.BenchmarkCase);
                var bytes = report.GcStats.GetBytesAllocatedPerOperation (report.BenchmarkCase);
                if (!report.Success || bytes is null) {
                    Console.Error.WriteLine ($"alloc-gate: {name} did not run successfully");
                    return 1;
                }
                measured[name] = bytes.Value;
            }
        }

        if (measured.Count == 0) {
            Console.Error.WriteLine ($"alloc-gate: no benchmark is tagged [BenchmarkCategory (\"{Category}\")]");
            return 1;
        }

        if (write) {
            if (filter is not null && File.Exists (baselinePath)) {
                var merged = Read (baselinePath);
                foreach (var (name, bytes) in measured)
                    merged[name] = bytes;
                measured = merged;
            }
            Write (baselinePath, measured);
            Console.WriteLine ($"alloc-gate: wrote {measured.Count} entries to {baselinePath}");
            return 0;
        }

        if (!File.Exists (baselinePath)) {
            Console.Error.WriteLine ($"alloc-gate: no baseline at {baselinePath}; create it with --alloc-gate --write");
            return 1;
        }

        return Compare (Read (baselinePath), measured, baselinePath, checkMissing: filter is null);
    }

    private static int Compare (SortedDictionary<string, long> baseline, SortedDictionary<string, long> measured, string baselinePath, bool checkMissing)
    {
        var failures = new List<string> ();
        var notices = new List<string> ();

        foreach (var (name, bytes) in measured) {
            if (!baseline.TryGetValue (name, out var expected)) {
                failures.Add ($"  {name}: {bytes} B, not in the baseline");
                continue;
            }

            var allowed = expected + Math.Max ((long)(expected * RelativeTolerance), AbsoluteTolerance);
            if (bytes > allowed)
                failures.Add ($"  {name}: {bytes} B, baseline {expected} B (+{bytes - expected} B, allowed up to {allowed} B)");
            else if (bytes < expected - Math.Max ((long)(expected * RelativeTolerance), AbsoluteTolerance))
                notices.Add ($"  {name}: {bytes} B, baseline {expected} B");
        }

        foreach (var name in baseline.Keys.Where (n => checkMissing && !measured.ContainsKey (n)))
            failures.Add ($"  {name}: in the baseline but did not run");

        Console.WriteLine ();
        Console.WriteLine ($"alloc-gate: {measured.Count} benchmarks checked against {baselinePath}");

        if (notices.Count > 0) {
            Console.WriteLine ("alloc-gate: these allocate less than the baseline records; regenerate it to lock the gain in (--alloc-gate --write):");
            foreach (var line in notices)
                Console.WriteLine (line);
        }

        if (failures.Count == 0) {
            Console.WriteLine ("alloc-gate: passed");
            return 0;
        }

        Console.Error.WriteLine ("alloc-gate: FAILED. These allocate more than the baseline allows. If the rise is intended, regenerate the baseline (--alloc-gate --write) and say why in the commit:");
        foreach (var line in failures)
            Console.Error.WriteLine (line);
        return 1;
    }

    // Type.Method(Param=value, ...): stable across runs, and readable in a diff of the baseline file.
    internal static string NameOf (BenchmarkCase benchmarkCase)
    {
        var builder = new StringBuilder ();
        builder.Append (benchmarkCase.Descriptor.Type.Name).Append ('.').Append (benchmarkCase.Descriptor.WorkloadMethod.Name);

        if (benchmarkCase.Parameters.Items.Count > 0) {
            builder.Append ('(');
            builder.AppendJoin (", ", benchmarkCase.Parameters.Items.Select (p => $"{p.Name}={Convert.ToString (p.Value, CultureInfo.InvariantCulture)}"));
            builder.Append (')');
        }

        return builder.ToString ();
    }

    private static SortedDictionary<string, long> Read (string path)
    {
        var result = new SortedDictionary<string, long> (StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines (path)) {
            var line = raw.Trim ();
            if (line.Length == 0 || line.StartsWith ('#'))
                continue;
            // The name can contain ':' inside a string parameter, so split on the last one.
            var colon = line.LastIndexOf (':');
            result[line[..colon].Trim ()] = long.Parse (line[(colon + 1)..].Trim ().TrimEnd ('B').Trim (), CultureInfo.InvariantCulture);
        }
        return result;
    }

    private static void Write (string path, SortedDictionary<string, long> measured)
    {
        var lines = new List<string> {
            "# Managed bytes allocated per operation by the benchmarks tagged [BenchmarkCategory (\"AllocGate\")].",
            "#",
            "# CI fails when one allocates more than this plus the larger of 5 % and 64 B (see AllocationGate.cs).",
            "# Recorded on Linux, the leg CI gates on: font and fallback paths can allocate differently elsewhere.",
            "#",
            "# Regenerate with: dotnet run -c Release --project benchmarks/Majorsilence.Forms.Benchmarks -- --alloc-gate --write",
            "# (or MAJORSILENCE_WRITE_ALLOC_BASELINE=1). Do not hand-edit.",
            "",
        };
        lines.AddRange (measured.Select (kv => $"{kv.Key}: {kv.Value.ToString (CultureInfo.InvariantCulture)} B"));
        File.WriteAllLines (path, lines);
    }

    // --baseline <path>, else the checked-in file found by walking up from the working directory and from
    // the binary (dotnet run starts in the caller's directory; the binary sits under bin/).
    private static string FindBaseline (string[] args)
    {
        var index = Array.IndexOf (args, "--baseline");
        if (index >= 0 && index + 1 < args.Length)
            return Path.GetFullPath (args[index + 1]);

        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }) {
            for (var dir = new DirectoryInfo (start); dir is not null; dir = dir.Parent) {
                var candidate = Path.Combine (dir.FullName, "benchmarks", "Majorsilence.Forms.Benchmarks", BaselineFileName);
                if (File.Exists (Path.Combine (dir.FullName, "benchmarks", "Majorsilence.Forms.Benchmarks", "Majorsilence.Forms.Benchmarks.csproj")))
                    return candidate;
            }
        }

        throw new InvalidOperationException ("Could not find benchmarks/Majorsilence.Forms.Benchmarks; pass --baseline <path>.");
    }
}
