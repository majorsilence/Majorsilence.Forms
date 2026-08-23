using System.Runtime.CompilerServices;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;

namespace Majorsilence.Forms.Benchmarks;

// BenchmarkDotNet's default toolchain runs each benchmark in a separate generated process that
// loads this assembly, so a one-time setup in Program.Main would not run there. A module
// initializer does, in every process that loads this assembly -- the same reason
// tests/Majorsilence.Forms.Tests/AssemblyInfo.cs uses one to install the headless backend.
internal static class BenchmarkSetup
{
    [ModuleInitializer]
    internal static void Initialize () => Platform.Backend = new HeadlessPlatformBackend ();
}
