using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Majorsilence.Forms.Tests;

// A public void method with an empty body is the worst kind of compatibility gap: it compiles, it
// runs, and it silently does nothing. Image.MakeTransparent was one of these -- ported code keyed a
// sprite's background colour to transparent, the call did nothing, and the only symptom was a white
// box behind every sprite. No exception, no warning, nothing to grep for.
//
// This does not try to force the existing stubs to be implemented or annotated; there are too many for
// that to be honest. It pins the current set so a NEW one cannot be added without the author either
// implementing it or consciously accepting it, at which point it also belongs in
// COMPATIBILITY_MATRIX.md.
//
// To accept an intentional new stub, re-run with MAJORSILENCE_WRITE_STUB_BASELINE=1 and commit the
// regenerated baseline alongside the matrix entry.
public class NoOpStubBaselineTests
{
    private const string BaselineFileName = "NoOpStubBaseline.txt";

    [Fact]
    public void NoNewEmptyBodiedPublicVoidMethods ()
    {
        var actual = ScanEmptyBodiedPublicVoidMethods (typeof (Control).Assembly.Location);
        var baselinePath = LocateBaseline ();

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_STUB_BASELINE") == "1") {
            File.WriteAllLines (baselinePath, [
                "# Public void methods in Majorsilence.Forms whose body is empty -- they compile, run and",
                "# silently do nothing. This file pins the known set so a new one cannot slip in unnoticed;",
                "# see NoOpStubBaselineTests. Entries are Namespace.Type.Method/parameterCount.",
                "#",
                "# Shrinking this list is the goal. Regenerate with MAJORSILENCE_WRITE_STUB_BASELINE=1.",
                .. actual,
            ]);
            return;
        }

        var baseline = File.ReadAllLines (baselinePath)
            .Where (l => l.Length > 0 && !l.StartsWith ('#'))
            .ToList ();

        var added = actual.Except (baseline).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "New empty-bodied public void method(s). Implement them, or -- if the no-op is deliberate --\n" +
            "record them in COMPATIBILITY_MATRIX.md and regenerate this baseline with\n" +
            "MAJORSILENCE_WRITE_STUB_BASELINE=1:\n  " + string.Join ("\n  ", added));

        // Not a failure in itself, but the baseline should shrink as gaps are closed rather than drift.
        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries no longer exist (implemented or renamed). Regenerate\n" +
            "the baseline with MAJORSILENCE_WRITE_STUB_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }

    // Walks up from the test binary to the repo root, so the baseline lives next to the test source
    // and stays reviewable in a diff rather than being buried in a resource.
    private static string LocateBaseline ()
    {
        var dir = new DirectoryInfo (AppContext.BaseDirectory);

        while (dir is not null) {
            var candidate = Path.Combine (dir.FullName, "tests", "Majorsilence.Forms.Tests", BaselineFileName);
            if (File.Exists (candidate))
                return candidate;

            if (File.Exists (Path.Combine (dir.FullName, "Majorsilence.Forms.slnx")))
                return Path.Combine (dir.FullName, "tests", "Majorsilence.Forms.Tests", BaselineFileName);

            dir = dir.Parent;
        }

        throw new InvalidOperationException ($"could not locate {BaselineFileName} from {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Returns "Namespace.Type.Method/argCount" for every public, non-abstract, void-returning method
    /// whose IL body is nothing but a return.
    /// </summary>
    /// <remarks>
    /// Reads metadata straight off the file rather than reflecting over loaded types: MethodBody is
    /// only reachable once a type is loaded, and loading every type here would drag in SkiaSharp
    /// natives for no benefit. Property and event accessors are excluded -- an inert event is a
    /// separate (and much larger) category than a method that quietly discards its arguments.
    /// </remarks>
    internal static List<string> ScanEmptyBodiedPublicVoidMethods (string assemblyPath)
    {
        var found = new List<string> ();

        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);
            if ((type.Attributes & TypeAttributes.Public) == 0)
                continue;

            var ns = md.GetString (type.Namespace);
            var typeName = (ns.Length == 0 ? "" : ns + ".") + md.GetString (type.Name);

            foreach (var methodHandle in type.GetMethods ()) {
                var method = md.GetMethodDefinition (methodHandle);

                if ((method.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public)
                    continue;
                if ((method.Attributes & MethodAttributes.Abstract) != 0)
                    continue;
                if (method.RelativeVirtualAddress == 0)
                    continue;

                var name = md.GetString (method.Name);
                if (name.StartsWith ("get_") || name.StartsWith ("set_") ||
                    name.StartsWith ("add_") || name.StartsWith ("remove_"))
                    continue;

                var signature = md.GetBlobBytes (method.Signature);
                // [0] calling convention, [1] parameter count, [2] return type.
                // ELEMENT_TYPE_VOID is 0x01.
                if (signature.Length < 3 || signature[2] != 0x01)
                    continue;

                var il = pe.GetMethodBody (method.RelativeVirtualAddress).GetILBytes ();
                if (il is null || !IsJustReturn (il))
                    continue;

                found.Add ($"{typeName}.{name}/{signature[1]}");
            }
        }

        // Sorted and de-duplicated so the baseline is a stable, reviewable diff. Overloads that differ
        // only in parameter types collapse to one entry, which is fine: the point is to notice a new
        // stub, not to enumerate every signature.
        return found.Distinct ().OrderBy (x => x, StringComparer.Ordinal).ToList ();
    }

    // Release emits `ret`; Debug prefixes a `nop`.
    private static bool IsJustReturn (byte[] il) => il.Length switch {
        1 => il[0] == 0x2A,
        2 => il[0] == 0x00 && il[1] == 0x2A,
        _ => false,
    };
}
