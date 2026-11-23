using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Opts a migrated app into WinForms' look by choosing its default font at startup, the way the WinForms app
/// itself did, used by both <see cref="SourceConverter"/> and <see cref="RoslynSourceConverter"/>.
///
/// Majorsilence.Forms draws its own theme (its check box, its tree rows, its text sizes) until the app calls
/// <c>Application.SetDefaultFont</c>; from then on it is a "ported app" and draws what WinForms draws. Every
/// WinForms app has a default font, so every migrated one should make that call -- without it a migrated
/// menu's checked item showed the theme's box instead of WinForms' tick (#721), and a tree ignored its font
/// size (#723). The call goes where the app started WinForms up:
/// <list type="bullet">
///   <item>.NET 6+: in place of <c>ApplicationConfiguration.Initialize()</c>, at its default font, Segoe UI 9pt.</item>
///   <item>Older templates: after <c>Application.EnableVisualStyles()</c>, at .NET Framework's default font,
///   Microsoft Sans Serif 8.25pt -- the font such a project's forms were laid out in.</item>
/// </list>
/// A file that already calls <c>SetDefaultFont</c> chose its own and is left alone.
/// </summary>
internal static class StartupFont
{
    /// <summary>.NET 6+'s default font (Control.DefaultFont), which ApplicationConfiguration.Initialize leaves in place.</summary>
    internal const string ModernFamily = "Segoe UI";
    internal const string ModernSize = "9";

    /// <summary>.NET Framework's default font (SystemFonts.DefaultFont).</summary>
    internal const string FrameworkFamily = "Microsoft Sans Serif";
    internal const string FrameworkSize = "8.25";

    private static readonly Regex ApplicationConfigurationInitialize =
        new(@"(?<![\w.])ApplicationConfiguration\.Initialize\s*\(\s*\)\s*;", RegexOptions.Compiled);

    // A whole line holding only the call (optionally qualified, VB without the semicolon), so the new call
    // can go on the next line at the same indentation.
    private static readonly Regex EnableVisualStyles = new(
        @"(?m)^(?<indent>[ \t]*)(?:(?:global::)?(?:System\.Windows\.Forms|Majorsilence\.Forms)\.)?Application\.EnableVisualStyles\s*\(\s*\)[ \t]*;?[ \t]*(?:'[^\r\n]*|//[^\r\n]*)?(?<eol>\r?\n|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SetDefaultFontCall =
        new(@"(?<![\w])SetDefaultFont\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Replaces <c>ApplicationConfiguration.Initialize()</c> (commenting the original out, as it has no
    /// equivalent) and, failing that, follows <c>Application.EnableVisualStyles()</c> with a
    /// <c>SetDefaultFont</c> call. Run on the ORIGINAL file's startup call, before or after the namespace
    /// rewrite: the emitted call is fully qualified, so the rewrite leaves it alone.
    /// </summary>
    public static string Apply(string text, SourceLanguage language, bool dualBuild, Action<string> warn)
    {
        var choosesOwnFont = SetDefaultFontCall.IsMatch(text);
        var replacedInitialize = false;

        text = ApplicationConfigurationInitialize.Replace(text, m => {
            if (choosesOwnFont || dualBuild || replacedInitialize) {
                warn("commented out 'ApplicationConfiguration.Initialize()' — Majorsilence.Forms sets up visual styles implicitly");
                if (dualBuild && !choosesOwnFont)
                    WarnDualBuild(warn, ModernFamily, ModernSize);
                return $"// {m.Value} // [majorsilence-migrate] no Majorsilence equivalent";
            }

            replacedInitialize = true;
            WarnAdded(warn, "in place of 'ApplicationConfiguration.Initialize()'", ModernFamily, ModernSize);
            return $"{Call (SourceLanguage.CSharp, ModernFamily, ModernSize)} // {m.Value} // [majorsilence-migrate] replaced: Majorsilence.Forms sets up visual styles implicitly, and the default font is .NET's";
        });

        if (choosesOwnFont || replacedInitialize)
            return text;

        var match = EnableVisualStyles.Match(text);
        if (!match.Success)
            return text;

        if (dualBuild) {
            WarnDualBuild(warn, FrameworkFamily, FrameworkSize);
            return text;
        }

        WarnAdded(warn, "after 'Application.EnableVisualStyles()'", FrameworkFamily, FrameworkSize);

        var newline = match.Groups["eol"].Value is { Length: > 0 } eol ? eol
            : text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var line = match.Groups["indent"].Value + Call (language, FrameworkFamily, FrameworkSize);
        var insert = match.Groups["eol"].Length > 0 ? line + newline : newline + line;

        return text[..(match.Index + match.Length)] + insert + text[(match.Index + match.Length)..];
    }

    // Fully qualified, so it binds to Majorsilence.Forms whatever the file imports (VB's My.Application, WPF's
    // System.Windows.Application) and the namespace rewrite has nothing to do to it.
    internal static string Call (SourceLanguage language, string family, string size)
        => language == SourceLanguage.VisualBasic
            ? $"Majorsilence.Forms.Application.SetDefaultFont (New Majorsilence.Forms.Drawing.Font (\"{family}\", {size}F))"
            : $"Majorsilence.Forms.Application.SetDefaultFont (new Majorsilence.Forms.Drawing.Font (\"{family}\", {size}f));";

    private static void WarnAdded(Action<string> warn, string where, string family, string size)
        => warn($"added 'Application.SetDefaultFont({family} {size}pt)' {where} — it makes Majorsilence.Forms draw " +
            "WinForms' check marks, row heights and text metrics instead of its own theme; change the font if the app used another");

    private static void WarnDualBuild(Action<string> warn, string family, string size)
        => warn($"--dual-build: add '{Call (SourceLanguage.CSharp, family, size)}' under '#if {ConditionalImports.ConditionSymbol}' " +
            "at startup — without it Majorsilence.Forms draws its own theme rather than WinForms' look");
}
