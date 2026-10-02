using System.Runtime.CompilerServices;

namespace Majorsilence.Forms.Tests;

// PrintDocument.Print submits to the system's printer (SVC-29). No test may reach a real one, so the whole
// test assembly falls back to a launcher that accepts the job and does nothing. A test that wants to see
// the command sets NativePrinting.LauncherOverride, which is scoped to that test's own flow.
internal static class NoRealPrinting
{
#pragma warning disable CA2255 // a module initializer is exactly the tool for an assembly-wide test default
    [ModuleInitializer]
    internal static void Install () => Majorsilence.Forms.Printing.NativePrinting.DefaultLauncher = _ => true;
#pragma warning restore CA2255
}
