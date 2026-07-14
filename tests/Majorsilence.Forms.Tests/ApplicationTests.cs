using Xunit;

namespace Majorsilence.Forms.Tests;

public class ApplicationTests
{
    [Fact]
    public void OpenForms ()
    {
        // Delta-based: other tests in the suite leak open forms (Show without Close), so absolute
        // counts are order-dependent. This test's contract is only that ITS forms come and go.
        var baseline = Application.OpenForms.Count;

        // Creating a Form does not add it to open forms
        var f = new Form ();

        Assert.Equal (baseline, Application.OpenForms.Count);

        // Showing a Form adds it to open forms
        f.Show ();

        Assert.Equal (baseline + 1, Application.OpenForms.Count);
        Assert.Contains (f, Application.OpenForms);

        // Showing a dialog Form adds it to open forms
        var f2 = new Form ();
        _ = f2.ShowDialogAsync (f);

        Assert.Equal (baseline + 2, Application.OpenForms.Count);
        Assert.Contains (f2, Application.OpenForms);

        // Closing the dialog Form removes it from open forms
        f2.Close ();
        Assert.DoesNotContain (f2, Application.OpenForms);

        // Closing a Form removes it from open forms
        f.Close ();
        Assert.DoesNotContain (f, Application.OpenForms);
        Assert.Equal (baseline, Application.OpenForms.Count);
    }
}
