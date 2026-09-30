using Xunit;

namespace Majorsilence.Forms.Tests;

// #100's acceptance scan: every public, concrete control and window in both assemblies is reached by a
// theme selector -- its own, or its nearest ancestor's -- unless it is on the frame-only list, whose
// content is not the library's to paint.
public class ThemeCssCoverageTests
{
    [Fact]
    public void Every_public_control_has_a_selector_or_inherits_one ()
    {
        // The Telerik assembly is part of the scan; referencing one of its types makes sure it is loaded.
        Assert.NotNull (typeof (Majorsilence.Forms.Telerik.RadGridView));

        var exempt = ThemeCssReference.FrameOnlyControls.Concat (ThemeCssReference.BaseControls)
            .Select (f => f.Name).ToHashSet (StringComparer.Ordinal);

        var uncovered = ThemeCssReference.PublicControlTypes ()
            .Where (t => ThemeCssReference.NearestSelector (t) is null && !exempt.Contains (t.Name))
            .Select (t => t.FullName)
            .ToList ();

        Assert.True (uncovered.Count == 0,
            "Public controls no theme selector reaches. Give each its own selector (a DefaultStyle and a ThemeCssSelector) or "
            + "list it in ThemeCssReference.FrameOnlyControls with the reason:\n  " + string.Join ("\n  ", uncovered));
    }

    [Fact]
    public void Also_styles_is_the_real_inheritance ()
    {
        // Spot checks the hand-written list used to miss or that the issue names.
        Assert.Contains ("RadDateTimePicker", ThemeCssReference.FindSelector ("DateTimePicker")!.AlsoAppliesTo);
        Assert.DoesNotContain ("DateTimePicker", ThemeCssReference.FindSelector ("TextBox")!.AlsoAppliesTo);
        Assert.Contains ("RichTextBox", ThemeCssReference.FindSelector ("TextBox")!.AlsoAppliesTo);
        Assert.Contains ("RadWaitingBar", ThemeCssReference.FindSelector ("ProgressBar")!.AlsoAppliesTo);
        Assert.Contains ("RadButton", ThemeCssReference.FindSelector ("Button")!.AlsoAppliesTo);
        Assert.DoesNotContain ("TextBox", ThemeCssReference.FindSelector ("TextBox")!.AlsoAppliesTo);
    }
    [Fact]
    public void Also_styles_types_really_chain_to_the_rule ()
    {
        // The table is derived from the class hierarchy; this proves the claim at run time. A type that
        // declares its own DefaultStyle but not a Style override paints from Control.DefaultStyle and the
        // rule never reaches it -- the silent no-op the theming subset promises never to have.
        Assert.NotNull (typeof (Majorsilence.Forms.Telerik.RadGridView));

        var broken = new List<string> ();
        var checkedCount = 0;

        foreach (var selector in ThemeCssReference.Selectors)
            foreach (var type in ThemeCssReference.PublicControlTypes ().Where (t => ThemeCssReference.NearestSelector (t) == selector)) {
                if (type.GetConstructor (Type.EmptyTypes) is null)
                    continue;

                object instance;
                try {
                    instance = Activator.CreateInstance (type)!;
                } catch (System.Reflection.TargetInvocationException) {
                    continue;
                }

                try {
                    var style = instance switch {
                        Control c => c.Style,
                        WindowBase w => w.Style,
                        _ => null,
                    };

                    if (style is null)
                        continue;

                    checkedCount++;
                    if (!Chains (style, selector.Style))
                        broken.Add ($"{type.FullName} (rule {selector.Name})");
                } finally {
                    (instance as IDisposable)?.Dispose ();
                }
            }

        Assert.True (checkedCount > 100, $"Only {checkedCount} types were instantiated; the check proves little.");
        Assert.True (broken.Count == 0,
            "These types are listed under a rule their Style does not inherit from:\n  " + string.Join ("\n  ", broken));

        static bool Chains (ControlStyle? style, ControlStyle target)
        {
            for (; style is not null; style = style._parent)
                if (ReferenceEquals (style, target))
                    return true;
            return false;
        }
    }
}
