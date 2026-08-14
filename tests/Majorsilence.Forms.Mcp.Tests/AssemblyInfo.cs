using System.Runtime.CompilerServices;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

// Each test installs a window and an HTTP server into process-wide state (the platform backend, a
// loopback listener), so these must not run across parallel collections.
[assembly: CollectionBehavior (DisableTestParallelization = true)]

namespace Majorsilence.Forms.Mcp.Tests
{
    internal static class TestBackend
    {
        // The dependency-free Headless backend: no windowing toolkit, and no UI-thread affinity to fight
        // with xUnit's worker threads. Runs before any test in the assembly.
        [ModuleInitializer]
        internal static void Initialize () => Platform.Backend = new HeadlessPlatformBackend ();
    }
}
