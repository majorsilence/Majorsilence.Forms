using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Identifies WinForms-only NuGet packages that have no place in a cross-platform Majorsilence.Forms
/// project, so the converter can drop their <c>&lt;PackageReference&gt;</c>s. The vendor UI suites
/// (Telerik, DevExpress, …) are replaced by the <c>Majorsilence.Forms.*</c> compat layers; the rest are
/// Windows-desktop-only. Patterns are case-insensitive and support a <c>*</c> wildcard. A project can add
/// its own via a map file's <c>removePackages</c> array.
/// </summary>
internal static class WinFormsPackages
{
    public static readonly IReadOnlyList<string> DefaultPatterns = new[]
    {
        "Telerik.UI.for.WinForms*",   // Telerik UI for WinForms -> Majorsilence.Forms.Telerik
        "DevExpress.Win*",            // DevExpress WinForms (DevExpress.Win.*)
        "Infragistics.Win*",          // Infragistics WinForms
        "C1.Win*",                    // ComponentOne WinForms
        "Syncfusion.*.WinForms*",     // Syncfusion WinForms
    };

    public static bool IsMatch(string packageId, IEnumerable<string> patterns) =>
        patterns.Any(p => GlobMatch(packageId, p));

    // Translate a '*'-glob to an anchored, case-insensitive regex (only '*' is special).
    private static bool GlobMatch(string input, string pattern)
    {
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
    }
}
