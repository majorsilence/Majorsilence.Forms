using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Majorsilence.Forms.Tests;

// A Form here is not a Control -- Form : WindowBase : Component, while Control : Component -- so the
// window side does not inherit one line of Control's surface. Every member the two share had to be
// written twice, and every member Control has that the window side does not is a hole that only shows
// up as a compiler error in somebody's ported application.
//
// Those holes are what this pins. Porting the Krypton Standard Toolkit found roughly a dozen of them
// one compiler error at a time -- DeviceDpi, CreateGraphics, GetChildAtPoint, SetBounds, ResizeRedraw,
// OnHelpRequested, RecreateHandle, HandleDestroyed -- and each cost a build cycle to discover. There
// was no way to see the set in advance, because nothing compared the two surfaces.
//
// So: the divergence is now a reviewed list rather than an unknown. Adding a member to Control without
// a window counterpart fails this test, and the author either adds the counterpart or records the
// omission by regenerating the baseline. Note the direction -- this does NOT claim every entry in the
// baseline should be closed. Plenty genuinely do not apply to a top-level window, and the file is the
// place to say which.
//
// Regenerate with MAJORSILENCE_WRITE_CONTROL_WINDOW_BASELINE=1.
public class ControlWindowParityTests
{
    private const string BaselineFileName = "ControlWindowParityBaseline.txt";

    [Fact]
    public void NoNewControlMembersMissingFromTheWindowSide ()
    {
        var actual = ControlMembersAbsentFromWindows ();
        var baselinePath = LocateBaseline ();

        if (Environment.GetEnvironmentVariable ("MAJORSILENCE_WRITE_CONTROL_WINDOW_BASELINE") == "1") {
            File.WriteAllLines (baselinePath, [
                "# Members of Control that WindowBase/Form do not have.",
                "#",
                "# A Form is not a Control here (Form : WindowBase : Component), so nothing on Control is",
                "# inherited by the window side -- it is all written twice, and anything written once is a",
                "# hole a ported application falls into. This file pins the known holes so a new one cannot",
                "# be added silently; see ControlWindowParityTests.",
                "#",
                "# Not every entry should be closed: Dock, Anchor, Parent, TabIndex and friends have no",
                "# meaning for a top-level window. Entries that DO belong on a window are the ones worth",
                "# taking off this list. Methods are Name/argCount; properties and events are just Name.",
                "#",
                "# Regenerate with MAJORSILENCE_WRITE_CONTROL_WINDOW_BASELINE=1.",
                .. actual,
            ]);
            return;
        }

        var baseline = File.ReadAllLines (baselinePath)
            .Where (l => l.Length > 0 && !l.StartsWith ('#'))
            .ToList ();

        var added = actual.Except (baseline, StringComparer.Ordinal).OrderBy (x => x, StringComparer.Ordinal).ToList ();
        var removed = baseline.Except (actual, StringComparer.Ordinal).OrderBy (x => x, StringComparer.Ordinal).ToList ();

        Assert.True (added.Count == 0,
            "Control gained member(s) that WindowBase/Form do not have. A Form does not inherit them, so\n" +
            "ported code calling them on a Form will not compile. Add the counterpart to WindowBase (or\n" +
            "Form), or -- if it genuinely has no meaning for a window -- regenerate the baseline with\n" +
            "MAJORSILENCE_WRITE_CONTROL_WINDOW_BASELINE=1:\n  " + string.Join ("\n  ", added));

        // Shrinking is the goal, so a closed gap is not a failure -- but the baseline has to be
        // regenerated to record it, or the file stops describing what is actually there.
        Assert.True (removed.Count == 0,
            $"{removed.Count} baseline entry/entries are no longer missing (the gap was closed, or the\n" +
            "member was renamed). Regenerate the baseline with\n" +
            "MAJORSILENCE_WRITE_CONTROL_WINDOW_BASELINE=1:\n  " + string.Join ("\n  ", removed));
    }

    /// <summary>
    /// Every member of <see cref="Control"/> with no same-named counterpart on <see cref="WindowBase"/>
    /// or <see cref="Form"/>, as "Name/argCount" for methods and "Name" for properties and events.
    /// </summary>
    /// <remarks>
    /// Arity is part of a method's key because a missing overload is a real hole, not a near miss:
    /// <c>GetChildAtPoint(Point)</c> existed on the window side while the hit-testing overload that
    /// takes a <c>GetChildAtPointSkip</c> did not, and the second is the one a form routing a click
    /// actually needs. Parameter <em>types</em> are deliberately not part of the key -- the point is to
    /// notice an absent member, not to diff every signature.
    ///
    /// Both types are walked to (but not including) <see cref="System.ComponentModel.Component"/>: it is
    /// the shared base, so its members are on both sides and would only add noise.
    /// </remarks>
    internal static List<string> ControlMembersAbsentFromWindows ()
    {
        var onWindows = Surface (typeof (WindowBase))
            .Concat (Surface (typeof (Form)))
            .ToHashSet (StringComparer.Ordinal);

        return Surface (typeof (Control))
            .Where (key => !onWindows.Contains (key))
            .Distinct (StringComparer.Ordinal)
            .OrderBy (key => key, StringComparer.Ordinal)
            .ToList ();
    }

    private static IEnumerable<string> Surface (Type type)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        for (var current = type; current is not null && current != typeof (System.ComponentModel.Component); current = current.BaseType) {
            foreach (var member in current.GetMembers (Declared)) {
                switch (member) {
                    // Accessors are covered by the property or event that owns them.
                    case MethodInfo { IsSpecialName: true }:
                        continue;

                    case MethodInfo method when IsVisible (method):
                        yield return $"{method.Name}/{method.GetParameters ().Length}";
                        break;

                    case PropertyInfo property
                        when IsVisible (property.GetMethod) || IsVisible (property.SetMethod):
                        yield return property.Name;
                        break;

                    case EventInfo evt when IsVisible (evt.AddMethod):
                        yield return evt.Name;
                        break;
                }
            }
        }
    }

    // Public and protected only: private and internal members are not part of what a ported
    // application or a derived control can reach, so a difference in them is not a compatibility gap.
    private static bool IsVisible (MethodInfo? method)
        => method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

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
}
