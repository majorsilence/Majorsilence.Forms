using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class TargetFrameworkRewriterTests
{
    [Theory]
    [InlineData ("net8.0-windows", "net8.0")]
    [InlineData ("net10.0-windows", "net10.0")]
    [InlineData ("net10.0-windows10.0.19041.0", "net10.0")]
    [InlineData ("net8.0", "net8.0")]
    [InlineData ("net48", "net48")]
    [InlineData ("netstandard2.0", "netstandard2.0")]
    public void StripWindowsSuffix_drops_only_the_windows_platform (string input, string expected)
    {
        Assert.Equal (expected, TargetFrameworkRewriter.StripWindowsSuffix (input));
    }

    [Fact]
    public void StripWindowsSuffixes_handles_a_semicolon_list ()
    {
        Assert.Equal ("net8.0;net48", TargetFrameworkRewriter.StripWindowsSuffixes ("net8.0-windows;net48"));
    }

    [Fact]
    public void StripWindowsFromDocument_rewrites_a_props_file ()
    {
        var xml = "<Project>\n  <PropertyGroup>\n    <TargetFramework>net8.0-windows</TargetFramework>\n  </PropertyGroup>\n</Project>";

        var (updated, changed) = TargetFrameworkRewriter.StripWindowsFromDocument (xml);

        Assert.True (changed);
        Assert.Contains ("<TargetFramework>net8.0</TargetFramework>", updated);
    }

    [Fact]
    public void StripWindowsFromDocument_is_a_noop_without_windows_tfms ()
    {
        var xml = "<Project>\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>";

        var (updated, changed) = TargetFrameworkRewriter.StripWindowsFromDocument (xml);

        Assert.False (changed);
        Assert.Equal (xml, updated);
    }

    [Fact]
    public void StripWindowsFromDocument_tolerates_malformed_xml ()
    {
        var (updated, changed) = TargetFrameworkRewriter.StripWindowsFromDocument ("<Project><not closed");
        Assert.False (changed);
        Assert.Equal ("<Project><not closed", updated);
    }
}
