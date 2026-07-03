using Xunit;

namespace Majorsilence.Forms.Tests;

// Exercises Application.Info — the backing facade for classic VB's My.Application.Info, confirmed
// against real migrated call shapes (AboutFixed.vb): Title/AssemblyName/Version/Copyright/CompanyName/
// Description/ProductName, all read as plain members (Version being the one real System.Version, used
// via .ToString()).
public class ApplicationInfoTests
{
    [Fact]
    public void Info_returns_the_same_instance ()
    {
        // Application.Info is a facade over the test-runner's own entry assembly; two reads must agree.
        Assert.Same (Application.Info, Application.Info);
    }

    [Fact]
    public void Title_is_a_non_null_string ()
    {
        Assert.NotNull (Application.Info.Title);
    }

    [Fact]
    public void AssemblyName_is_a_non_null_string ()
    {
        Assert.NotNull (Application.Info.AssemblyName);
    }

    [Fact]
    public void Version_is_a_real_System_Version ()
    {
        // My.Application.Info.Version.ToString() is the real-world call shape (AboutFixed.vb) — this only
        // compiles/behaves correctly if Version returns System.Version, not a string.
        Version version = Application.Info.Version;
        Assert.NotNull (version.ToString ());
    }

    [Fact]
    public void Copyright_is_a_non_null_string ()
    {
        Assert.NotNull (Application.Info.Copyright);
    }

    [Fact]
    public void CompanyName_matches_Application_CompanyName ()
    {
        Assert.Equal (Application.CompanyName ?? string.Empty, Application.Info.CompanyName);
    }

    [Fact]
    public void Description_is_a_non_null_string ()
    {
        Assert.NotNull (Application.Info.Description);
    }

    [Fact]
    public void ProductName_matches_Application_ProductName ()
    {
        Assert.Equal (Application.ProductName ?? string.Empty, Application.Info.ProductName);
    }
}
