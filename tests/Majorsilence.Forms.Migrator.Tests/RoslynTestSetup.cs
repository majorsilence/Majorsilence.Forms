using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace Majorsilence.Forms.Migrator.Tests;

/// <summary>
/// <see cref="MSBuildLocator.RegisterDefaults"/> must run before any Roslyn/MSBuild assembly is loaded into
/// the process, and can only be called once per process ever. Production code only calls it lazily, gated
/// behind <c>--engine roslyn</c> being selected (see <see cref="RoslynWorkspaceContext.TryCreate"/>) — but
/// xunit loads every test assembly and runs many <c>[Fact]</c>s in a single shared process, so by the time
/// the first Roslyn-engine test runs, other tests may already have triggered an assembly load that makes a
/// late <c>RegisterDefaults()</c> call throw or silently fail to take effect.
///
/// A <see cref="ModuleInitializerAttribute"/>-attributed method runs once, automatically, the moment this
/// assembly is loaded — before any test method (Roslyn-touching or not) executes. Guarding with
/// <see cref="MSBuildLocator.CanRegister"/> keeps this safe to run defensively even in a process where
/// something else already registered (or where no test in this run happens to need it).
/// </summary>
internal static class RoslynTestSetup
{
    [ModuleInitializer]
    internal static void RegisterMsBuildForTests()
    {
        if (MSBuildLocator.CanRegister)
            MSBuildLocator.RegisterDefaults();
    }
}
