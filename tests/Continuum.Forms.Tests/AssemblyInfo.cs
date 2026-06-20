using System.Runtime.CompilerServices;
using Continuum.Forms.Backends;
using Continuum.Forms.Headless;
using Xunit;

// These tests exercise global process state — the active platform backend
// (Continuum.Forms.Backends.Platform.Backend) and Application.OpenForms. They must run serially,
// not across parallel test collections.
[assembly: CollectionBehavior (DisableTestParallelization = true)]

namespace Continuum.Forms.Tests
{
    internal static class TestBackend
    {
        // Run the suite on the dependency-free Headless backend: no windowing toolkit and no UI-thread
        // dispatcher affinity (Avalonia's dispatcher is thread-bound and conflicts with xUnit's worker
        // threads). Runs before any test in the assembly.
        [ModuleInitializer]
        internal static void Initialize () => Platform.Backend = new HeadlessPlatformBackend ();
    }
}
