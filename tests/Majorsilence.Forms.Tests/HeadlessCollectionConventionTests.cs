using System.Reflection;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// #330: Backends.Platform.Backend is process-wide, so every test class that calls HeadlessRenderer.Use ()
// must be in the "Headless" collection, which xunit runs serially. Seventy files once called it without
// the tag and raced the rest; the convention had no enforcement, so it drifted. This finds the calls in the
// compiled test assembly -- lambdas, local functions and async state machines included, since those
// compile into nested types -- and asks the outermost class for the attribute.
public class HeadlessCollectionConventionTests
{
    [Fact]
    public void Every_class_that_calls_HeadlessRenderer_Use_is_in_the_Headless_collection ()
    {
        var use = typeof (HeadlessRenderer).GetMethod (nameof (HeadlessRenderer.Use), BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)!;
        var assembly = typeof (HeadlessCollectionConventionTests).Assembly;

        var callers = assembly.GetTypes ()
            .Where (t => Methods (t).Any (m => Calls (m, use)))
            .Select (Outermost)
            .Distinct ()
            .ToList ();

        // Guards against a scan that silently finds nothing (a changed IL shape, an overload) and so
        // passes vacuously.
        Assert.True (callers.Count > 100, $"Only {callers.Count} classes were found calling HeadlessRenderer.Use; the scan is broken.");

        var untagged = callers
            .Where (t => !t.GetCustomAttributes<CollectionAttribute> ().Any (a => a.Name == "Headless"))
            .Select (t => t.FullName)
            .OrderBy (n => n, StringComparer.Ordinal)
            .ToList ();

        Assert.True (untagged.Count == 0,
            "These classes call HeadlessRenderer.Use () but are not [Collection (\"Headless\")], so they race every "
            + "other headless test on the process-wide backend (#330):\n  " + string.Join ("\n  ", untagged));
    }

    private static IEnumerable<MethodBase> Methods (Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        return type.GetMethods (all).Cast<MethodBase> ().Concat (type.GetConstructors (all));
    }

    private static Type Outermost (Type type)
    {
        while (type.DeclaringType is { } outer)
            type = outer;
        return type;
    }

    // A call or callvirt (0x28 / 0x6F) whose token resolves to the method. Operand bytes that happen to
    // be 0x28 resolve to something else, or throw, and are skipped.
    private static bool Calls (MethodBase method, MethodInfo target)
    {
        byte[]? il;
        try {
            il = method.GetMethodBody ()?.GetILAsByteArray ();
        } catch (Exception) {
            return false;
        }

        if (il is null)
            return false;

        for (var i = 0; i + 4 < il.Length; i++) {
            if (il[i] != 0x28 && il[i] != 0x6F)
                continue;

            var token = BitConverter.ToInt32 (il, i + 1);
            try {
                if (method.Module.ResolveMethod (token) == target)
                    return true;
            } catch (Exception) {
                // Not a method token.
            }
        }

        return false;
    }
}
