using System.Reflection;
using BenchmarkDotNet.Running;
using Majorsilence.Forms.Benchmarks;

if (args.Contains ("--alloc-gate"))
    return AllocationGate.Run (args);

var summaries = BenchmarkSwitcher.FromAssembly (Assembly.GetExecutingAssembly ()).Run (args);

// BenchmarkDotNet reports a benchmark that threw, or a class that failed validation, in its output and
// carries on. CI's smoke run relies on the exit code, so turn either into a failure.
var failed = summaries.Where (s => s.HasCriticalValidationErrors).Select (s => s.Title)
    .Concat (summaries.SelectMany (s => s.Reports).Where (r => !r.Success).Select (r => r.BenchmarkCase.DisplayInfo))
    .ToList ();

foreach (var name in failed)
    Console.Error.WriteLine ($"benchmark failed: {name}");

return failed.Count == 0 ? 0 : 1;
