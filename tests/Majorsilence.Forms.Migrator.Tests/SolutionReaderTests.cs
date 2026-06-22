using Majorsilence.Forms.Migrator;
using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

public class SolutionReaderTests : IDisposable
{
    private readonly string _dir;

    public SolutionReaderTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-sln-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose ()
    {
        Directory.Delete (_dir, recursive: true);
        GC.SuppressFinalize (this);
    }

    [Fact]
    public void Extracts_only_existing_csproj_paths ()
    {
        Directory.CreateDirectory (Path.Combine (_dir, "A"));
        Directory.CreateDirectory (Path.Combine (_dir, "B"));
        File.WriteAllText (Path.Combine (_dir, "A", "A.csproj"), "<Project/>");
        File.WriteAllText (Path.Combine (_dir, "B", "B.csproj"), "<Project/>");

        var sln = Path.Combine (_dir, "Test.sln");
        File.WriteAllText (sln, """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "A", "A\A.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "B", "B\B.csproj", "{22222222-2222-2222-2222-222222222222}"
            EndProject
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "SolutionFolder", "SolutionFolder", "{33333333-3333-3333-3333-333333333333}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Missing", "C\Missing.csproj", "{44444444-4444-4444-4444-444444444444}"
            EndProject
            """);

        var projects = SolutionReader.ProjectPaths (sln);

        Assert.Equal (2, projects.Count);
        Assert.Contains (projects, p => p.EndsWith ("A.csproj", StringComparison.Ordinal));
        Assert.Contains (projects, p => p.EndsWith ("B.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain (projects, p => p.Contains ("Missing", StringComparison.Ordinal)); // file doesn't exist
        Assert.DoesNotContain (projects, p => p.Contains ("SolutionFolder", StringComparison.Ordinal)); // not a csproj
    }
}
