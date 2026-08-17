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
        // The Krypton suites ship as Windows-only WinForms packages. A repo that builds Krypton from
        // source (Standard-Toolkit beside this one) gets it as a ProjectReference instead, and
        // leaving the package alongside is worse than useless: every Krypton type then exists twice
        // (CS0433 "exists in both ... Version=95 ... and ... Version=110"), and the package drags
        // real System.Windows.Forms back in, so its types leak into signatures the migrated source
        // cannot satisfy (CS0012). Covers .Canary/.Nightly and the sibling suites.
        // SkiaSharp's WinForms view host: a Windows-only control wrapper around the same Skia this
        // library already renders with, so a converted project draws through Majorsilence.Forms
        // instead. (Plain "SkiaSharp" stays -- it is cross-platform and is this library's own
        // renderer; only the WinForms/WPF view hosts go.)
        // Cyotek's WinForms control packages (ColorPicker, ImageBox, and the "Unofficial.*" repack).
        // The ColorPicker has a migrated fork that is referenced as a project instead; the rest are
        // Windows-only and have no cross-platform build.
        "*Cyotek.Windows.Forms*",
        "SkiaSharp.Views.WindowsForms*",
        "SkiaSharp.Views.Desktop*",
        "Krypton.Toolkit*",
        "Krypton.Navigator*",
        "Krypton.Docking*",
        "Krypton.Workspace*",
        "Krypton.Ribbon*",
        "Telerik.UI.for.WinForms*",   // Telerik UI for WinForms -> Majorsilence.Forms.Telerik
        "DevExpress.Win*",            // DevExpress WinForms (DevExpress.Win.*)
        "Infragistics.Win*",          // Infragistics WinForms
        "C1.Win*",                    // ComponentOne WinForms
        "Syncfusion.*.WinForms*",     // Syncfusion WinForms
        // GDI+ itself: Windows-only from .NET 7 on, and wholly replaced by Majorsilence.Forms.Drawing.
        // Leaving it referenced is worse than useless -- it puts System.Drawing.Bitmap/Font/Pen/... back
        // in scope beside their Majorsilence.Forms.Drawing replacements, so every unqualified use in a
        // migrated file becomes an ambiguous reference (CS0104) rather than resolving to the port.
        "System.Drawing.Common",
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
