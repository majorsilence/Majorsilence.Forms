using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Builds the <c>--dual-build</c> conditional-compilation replacement for a namespace import, used by both
/// <see cref="SourceConverter"/> and <see cref="RoslynSourceConverter"/>.
///
/// The idea: a project can define <c>MAJORSILENCE_FORMS</c> (as an MSBuild property, typically in a
/// repo-root <c>Directory.Build.props</c>, propagated to <c>DefineConstants</c> by <see cref="ProjectConverter"/>
/// — see its <c>AddDualBuildConstant</c>) to switch which namespace a migrated file's top-of-file import binds
/// to, without touching the rest of the file. On Windows this lets a developer keep building against real
/// System.Windows.Forms/System.Drawing while they migrate incrementally, flipping the property only once
/// they're satisfied with the Majorsilence.Forms build.
///
/// Deliberately narrow in scope: only the plain <c>using</c>/<c>Imports</c> line itself becomes conditional.
/// Fully-qualified references elsewhere in the file body (e.g. <c>System.Windows.Forms.MessageBox.Show(...)</c>)
/// are still rewritten unconditionally by the namespace-prefix pass, same as non-dual-build mode — those
/// specific statements only compile once MAJORSILENCE_FORMS is defined. Most WinForms source relies on
/// unqualified type names via the top-of-file import, which is what this targets.
/// </summary>
internal static class ConditionalImports
{
    /// <summary>The MSBuild property / preprocessor symbol name that switches dual-build mode.</summary>
    public const string ConditionSymbol = "MAJORSILENCE_FORMS";

    /// <summary>
    /// Finds a bare <c>using &lt;majorsilenceNs&gt;;</c> / <c>Imports &lt;majorsilenceNs&gt;</c> import line
    /// (with no further dotted segments — i.e. exactly that namespace) and replaces it with an
    /// <c>#if MAJORSILENCE_FORMS</c> / <c>#else</c> block choosing between it and <paramref name="originalNs"/>.
    /// Both engines' namespace-prefix rewrite already turns the original WinForms import into this exact bare
    /// line before this runs (see each engine's call site), so this is a purely textual, engine-agnostic swap.
    /// Returns <paramref name="text"/> unchanged if no such bare import line is present.
    /// </summary>
    public static string WrapBareImport(string text, string majorsilenceNs, string originalNs)
    {
        var pattern = new Regex(
            $@"(?m)^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+{Regex.Escape(majorsilenceNs)}[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);
        var match = pattern.Match(text);
        if (!match.Success)
            return text;

        var indent = match.Groups["indent"].Value;
        var kw = match.Groups["kw"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var block = BuildSwitchedBlock(indent, kw, majorsilenceNs, originalNs, newline);

        return text[..match.Index] + block + text[(match.Index + match.Length)..];
    }

    /// <summary>
    /// An <c>#if MAJORSILENCE_FORMS</c> / <c>#else</c> block choosing between <paramref name="majorsilenceNs"/>
    /// and <paramref name="originalNs"/>, matching either C# (<c>using</c>) or VB (<c>Imports</c>) preprocessor
    /// syntax depending on <paramref name="kw"/>.
    /// </summary>
    public static string BuildSwitchedBlock(string indent, string kw, string majorsilenceNs, string originalNs, string newline) =>
        kw == "Imports"
            ? $"{indent}#If {ConditionSymbol} Then{newline}" +
              $"{indent}Imports {majorsilenceNs}{newline}" +
              $"{indent}#Else{newline}" +
              $"{indent}Imports {originalNs}{newline}" +
              $"{indent}#End If"
            : $"{indent}#if {ConditionSymbol}{newline}" +
              $"{indent}using {majorsilenceNs};{newline}" +
              $"{indent}#else{newline}" +
              $"{indent}using {originalNs};{newline}" +
              $"{indent}#endif";

    /// <summary>
    /// An <c>#if MAJORSILENCE_FORMS</c> block (no <c>#else</c> — nothing extra is needed in WinForms-mode,
    /// where the GDI+ companion type already lives directly under the kept <c>System.Drawing</c> import)
    /// adding the <c>Majorsilence.Forms.Drawing</c> companion import only when MAJORSILENCE_FORMS is defined.
    /// </summary>
    public static string BuildDrawingCompanionBlock(string indent, string kw, string newline) =>
        kw == "Imports"
            ? $"{indent}#If {ConditionSymbol} Then{newline}" +
              $"{indent}Imports {NamespaceMap.DrawingTarget}{newline}" +
              $"{indent}#End If"
            : $"{indent}#if {ConditionSymbol}{newline}" +
              $"{indent}using {NamespaceMap.DrawingTarget};{newline}" +
              $"{indent}#endif";
}
