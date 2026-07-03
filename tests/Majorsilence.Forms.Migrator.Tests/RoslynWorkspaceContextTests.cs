using Xunit;

namespace Majorsilence.Forms.Migrator.Tests;

[Trait ("Category", "Roslyn")]
public class RoslynWorkspaceContextTests : IDisposable
{
    private readonly string _dir;

    public RoslynWorkspaceContextTests ()
    {
        _dir = Path.Combine (Path.GetTempPath (), "cfm-roslyn-ws-" + Guid.NewGuid ().ToString ("N"));
        Directory.CreateDirectory (_dir);
    }

    public void Dispose ()
    {
        Directory.Delete (_dir, recursive: true);
        GC.SuppressFinalize (this);
    }

    private string Write (string relativePath, string content)
    {
        var path = Path.Combine (_dir, relativePath);
        Directory.CreateDirectory (Path.GetDirectoryName (path)!);
        File.WriteAllText (path, content);
        return path;
    }

    private const string MinimalCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
          </PropertyGroup>
        </Project>
        """;

    [Fact]
    public void HasLoadableProject_is_true_for_a_csproj_path ()
    {
        var proj = Write ("App.csproj", MinimalCsproj);
        Assert.True (RoslynWorkspaceContext.HasLoadableProject (proj));
    }

    [Fact]
    public void HasLoadableProject_is_false_for_a_bare_directory ()
    {
        Assert.False (RoslynWorkspaceContext.HasLoadableProject (_dir));
    }

    [Fact]
    public void HasLoadableProject_is_false_for_a_single_source_file ()
    {
        var file = Write ("Program.cs", "class C { }\n");
        Assert.False (RoslynWorkspaceContext.HasLoadableProject (file));
    }

    [Fact]
    public void TryCreate_loads_a_single_project_and_resolves_its_documents ()
    {
        var proj = Write ("App.csproj", MinimalCsproj);
        Write ("Program.cs", "class C { }\n");

        var context = RoslynWorkspaceContext.TryCreate (proj, out var error);
        try
        {
            Assert.Null (error);
            Assert.NotNull (context);

            var found = context!.TryGetDocument (Path.Combine (_dir, "Program.cs"), out var document, out var failureReason);
            Assert.True (found);
            Assert.NotNull (document);
            Assert.Null (failureReason);
        }
        finally
        {
            context?.Dispose ();
        }
    }

    [Fact]
    public void TryGetDocument_reports_a_failure_reason_for_an_unrelated_path ()
    {
        var proj = Write ("App.csproj", MinimalCsproj);
        Write ("Program.cs", "class C { }\n");

        var context = RoslynWorkspaceContext.TryCreate (proj, out var error);
        try
        {
            Assert.Null (error);
            var found = context!.TryGetDocument (Path.Combine (_dir, "NotAFile.cs"), out var document, out var failureReason);
            Assert.False (found);
            Assert.Null (document);
            Assert.NotNull (failureReason);
        }
        finally
        {
            context?.Dispose ();
        }
    }
}
