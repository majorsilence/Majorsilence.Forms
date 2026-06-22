using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class ReportBuilderTests
{
    private static MigrationOptions Options () => new () { Input = "/src", Output = "/out" };

    [Fact]
    public void Includes_summary_counts ()
    {
        var report = ReportBuilder.Build (
            Options (),
            filesScanned: 5,
            changes: new[] { ("proj", "App.csproj"), ("src", "Form1.cs") },
            warnings: new[] { "Form1.cs: review X" });

        Assert.Contains ("Files scanned: **5**", report);
        Assert.Contains ("Files changed: **2**", report);
        Assert.Contains ("Manual-review items: **1**", report);
    }

    [Fact]
    public void Lists_changed_files_and_warnings ()
    {
        var report = ReportBuilder.Build (
            Options (),
            filesScanned: 1,
            changes: new[] { ("src", "Form1.cs") },
            warnings: new[] { "Form1.cs: uses Graphics" });

        Assert.Contains ("`[src]` Form1.cs", report);
        Assert.Contains ("Form1.cs: uses Graphics", report);
    }

    [Fact]
    public void Clean_run_states_nothing_flagged ()
    {
        var report = ReportBuilder.Build (Options (), 1, Array.Empty<(string, string)> (), Array.Empty<string> ());
        Assert.Contains ("Nothing flagged", report);
    }
}
