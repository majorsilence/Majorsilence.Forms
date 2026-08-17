using System.Reflection;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;

// Constructs every Form in the Examples assembly on the headless backend and reports which ones throw.
// Clicking through the demo one button at a time costs a launch, a crash and a rebuild per bug; this
// finds every constructor-time failure in one pass. It does NOT replace looking at the app -- painting,
// layout and interaction are all still untested here -- it just clears the crashes first.
internal static class Program
{
    private static int Main ()
    {
        Platform.Backend = new HeadlessPlatformBackend ();

        var examples = typeof (Examples.MainWindow).Assembly;

        var forms = examples.GetTypes ()
            .Where (t => !t.IsAbstract
                && typeof (Majorsilence.Forms.Form).IsAssignableFrom (t)
                && t.GetConstructor (Type.EmptyTypes) is not null)
            .OrderBy (t => t.Name, StringComparer.Ordinal)
            .ToList ();

        var failed = 0;

        foreach (var type in forms) {
            try {
                var form = (Majorsilence.Forms.Form)Activator.CreateInstance (type)!;
                form.Dispose ();
                Console.WriteLine ($"  ok    {type.Name}");
            } catch (Exception ex) {
                failed++;
                // The useful part is the innermost exception and the first frame inside the suite: a
                // TargetInvocationException wrapper says nothing about which field was null.
                var root = ex;
                while (root.InnerException is not null)
                    root = root.InnerException;

                var frame = (root.StackTrace ?? string.Empty)
                    .Split ('\n')
                    .FirstOrDefault (l => l.Contains ("Krypton", StringComparison.Ordinal)
                        || l.Contains ("Examples", StringComparison.Ordinal))
                    ?.Trim () ?? "(no frame)";

                Console.WriteLine ($"  FAIL  {type.Name}: {root.GetType ().Name}: {root.Message}");
                Console.WriteLine ($"          {frame}");
            }
        }

        Console.WriteLine ($"\n{forms.Count - failed}/{forms.Count} example forms construct; {failed} failed.");
        return failed == 0 ? 0 : 1;
    }
}
