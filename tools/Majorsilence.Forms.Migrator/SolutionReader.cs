using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>Pulls the C# project paths out of a Visual Studio <c>.sln</c> file.</summary>
internal static partial class SolutionReader
{
    // Project("{type-guid}") = "Name", "relative\path.csproj", "{project-guid}"
    [GeneratedRegex("""^Project\("\{[^}]+\}"\)\s*=\s*"[^"]*",\s*"([^"]+\.(?:cs|vb)proj)"\s*,""",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ProjectLine();

    public static IReadOnlyList<string> ProjectPaths(string solutionPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
        var text = File.ReadAllText(solutionPath);

        return ProjectLine().Matches(text)
            .Select(m => m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar))
            .Select(rel => Path.GetFullPath(Path.Combine(dir, rel)))
            .Where(File.Exists)
            .Distinct()
            .ToList();
    }
}
