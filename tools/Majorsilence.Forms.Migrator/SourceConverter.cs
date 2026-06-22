using System.Text.RegularExpressions;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// Rewrites the namespaces inside a C# (or VB) source file so it compiles against Majorsilence.Forms.
/// This is a deliberately textual transform — it does not parse the syntax tree — which keeps it fast
/// and tolerant of files that don't currently compile, at the cost of needing a human to review the
/// warnings it emits for anything it could not confidently map.
///
/// The one piece of real cleverness is the <c>System.Drawing</c> split: primitive value types
/// (Color/Point/Size/Rectangle/…) are framework types Majorsilence.Forms keeps, so they are left alone,
/// while GDI+ types (Bitmap/Brush/Pen/…) are redirected to <c>Majorsilence.Drawing</c>. See <see cref="NamespaceMap"/>.
/// </summary>
internal enum SourceLanguage
{
    CSharp,
    VisualBasic,
}

internal static class SourceConverter
{
    public sealed record Result(string Text, bool Changed, IReadOnlyList<string> Warnings);

    // A fully-qualified System.Drawing type reference, e.g. "System.Drawing.Bitmap". The boundary
    // lookbehind avoids matching the tail of an unrelated namespace (MyApp.System.Drawing.X).
    private static readonly Regex DrawingType =
        new(@"(?<![\w.])System\.Drawing\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    public static Result Convert(string text, CustomMap? customMap = null, SourceLanguage language = SourceLanguage.CSharp)
    {
        var warnings = new List<string>();
        var seenWarnings = new HashSet<string>(StringComparer.Ordinal);
        var original = text;

        void Warn(string message)
        {
            if (seenWarnings.Add(message))
                warnings.Add(message);
        }

        // 0. The .NET 6+ generated bootstrap `ApplicationConfiguration.Initialize()` has no Majorsilence
        //    equivalent — and would not compile — so comment it out. Its job (EnableVisualStyles /
        //    SetCompatibleTextRenderingDefault) is implicit in Majorsilence.Forms, so dropping it is safe.
        text = Regex.Replace(text,
            @"(?<![\w.])ApplicationConfiguration\.Initialize\s*\(\s*\)\s*;",
            m =>
            {
                Warn("commented out 'ApplicationConfiguration.Initialize()' — Majorsilence.Forms sets up visual styles implicitly");
                return $"// {m.Value} // [majorsilence-migrate] no Majorsilence equivalent";
            });

        // 1. Whole-namespace prefix rewrites (sub-namespaces, Printing, and WinForms). Longest-first
        //    so a parent prefix never clips a child. (?!\w) lets a trailing '.' through, so both
        //    `using X;` and `X.Type` are covered.
        foreach (var (from, to) in NamespaceMap.NamespacePrefixes)
        {
            var guard = from == "System.Windows.Forms"
                ? @"(?!\.VisualStyles\b)"   // VisualStyles has no equivalent; leave it for the warning pass.
                : "";
            var pattern = $@"(?<![\w.]){Regex.Escape(from)}(?!\w){guard}";
            text = Regex.Replace(text, pattern, to);
        }

        // 1a. User-supplied namespace rewrites (e.g. Telerik -> Majorsilence.Forms.Telerik). Same boundary
        //     rules as the built-ins; longest-first ordering is guaranteed by CustomMap.Load.
        foreach (var (from, to) in (customMap ?? CustomMap.Empty).Namespaces)
        {
            var pattern = $@"(?<![\w.]){Regex.Escape(from)}(?!\w)";
            text = Regex.Replace(text, pattern, to.Replace("$", "$$"));
        }

        // 2. System.Drawing type references. Three buckets: keep the framework primitives as-is; redirect
        //    GDI+ types to Majorsilence.Drawing; redirect the WinForms-compat types (Graphics, ContentAlignment,
        //    SystemColors, …) to Majorsilence.Forms; warn on anything with no Majorsilence equivalent at all.
        text = DrawingType.Replace(text, m =>
        {
            var type = m.Groups[1].Value;
            if (NamespaceMap.DrawingPrimitives.Contains(type))
                return m.Value; // framework primitive — Majorsilence.Forms uses it as-is.
            if (NamespaceMap.MajorsilenceDrawingTypes.Contains(type))
                return $"{NamespaceMap.DrawingTarget}.{type}";
            if (NamespaceMap.MajorsilenceFormsTypes.Contains(type))
                return $"Majorsilence.Forms.{type}";
            // Don't mistake an unsupported sub-namespace (e.g. System.Drawing.Design.UITypeEditor) for a
            // leaf type — the unsupported-namespace pass below reports it once, cleanly.
            var qualified = $"System.Drawing.{type}";
            if (NamespaceMap.UnsupportedNamespaces.Any(u => u == qualified || u.StartsWith(qualified + ".", StringComparison.Ordinal)))
                return m.Value;
            Warn($"'{qualified}' has no Majorsilence equivalent — review manually");
            return m.Value;
        });

        // 3. A bare `using System.Drawing;` is kept (it still resolves the primitives), but GDI+ types
        //    used unqualified under it now live in Majorsilence.Drawing, so add a companion import.
        text = AddCompanionDrawingImport(text);

        // 4. Flag any namespace we deliberately refused to rewrite.
        foreach (var unsupported in NamespaceMap.UnsupportedNamespaces)
        {
            if (original.Contains(unsupported, StringComparison.Ordinal))
                Warn($"references '{unsupported}', which has no Majorsilence equivalent — review manually");
        }

        // 5. Unqualified GDI+ types brought in via `using System.Drawing;` are invisible to the textual
        //    rewrite (no namespace to anchor on) and have no Majorsilence replacement — they'd be silent
        //    compile breaks. If the file imports System.Drawing, name-match the high-signal ones and warn.
        if (BareDrawingImport.IsMatch(original))
        {
            foreach (var type in NamespaceMap.UnmappedDrawingTypes)
            {
                if (Regex.IsMatch(original, $@"(?<![\w.])\b{Regex.Escape(type)}\b"))
                    Warn($"uses '{type}' (System.Drawing.Common) which has no Majorsilence.Drawing equivalent — review manually");
            }
        }

        // 6. Visual Basic specifics. The VB 'My' application framework and a couple of Windows-only
        //    Microsoft.VisualBasic types have no cross-platform form; they need a human, not a rewrite.
        if (language == SourceLanguage.VisualBasic)
            WarnVisualBasic(original, Warn);

        return new Result(text, !string.Equals(text, original, StringComparison.Ordinal), warnings);
    }

    private static void WarnVisualBasic(string original, Action<string> warn)
    {
        // The 'My' namespace (My.Settings/My.Resources/My.Application/My.Computer/…) is generated by the
        // VB WinForms application framework, which MyType=Empty switches off for cross-platform builds.
        if (Regex.IsMatch(original, @"(?<![\w.])My\.(Settings|Resources|Application|Computer|Forms|User|WebServices|Log|Request|Response)\b"))
            warn("uses the VB 'My.*' namespace, which MyType=Empty disables — replace with explicit code (e.g. a file-based settings module)");

        // Microsoft.VisualBasic.Devices.ComputerInfo (and My.Computer.Info) is Windows-desktop only.
        if (original.Contains("ComputerInfo", StringComparison.Ordinal))
            warn("uses 'ComputerInfo', which is Windows-only — replace e.g. OSFullName with RuntimeInformation.OSDescription");

        // With MyType=Empty there's no auto WinForms constructor, so each Form needs an explicit
        // Public Sub New() that calls InitializeComponent(). Flag forms that don't appear to have one.
        if (Regex.IsMatch(original, @"(?im)^\s*(Partial\s+)?(Public\s+|Friend\s+)?Class\s+\w+", RegexOptions.None)
            && original.Contains("InitializeComponent", StringComparison.Ordinal)
            && !Regex.IsMatch(original, @"(?i)\bSub\s+New\s*\("))
            warn("VB form has InitializeComponent but no 'Public Sub New()' — with MyType=Empty you must add a constructor that calls InitializeComponent()");
    }

    // Matches a standalone `using System.Drawing;` (C#) or `Imports System.Drawing` (VB) import line.
    // Uses [ \t] rather than \s so it never swallows the line's own newline — VB has no `;` terminator,
    // and a greedy \s* would otherwise consume the break and misplace the inserted companion import.
    private static readonly Regex BareDrawingImport =
        new(@"(?m)^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+System\.Drawing[ \t]*;?[ \t]*$", RegexOptions.Compiled);

    private static string AddCompanionDrawingImport(string text)
    {
        // Already imported (e.g. a previous run, or an idempotent re-convert)? Nothing to do.
        if (Regex.IsMatch(text, @"(?m)^[ \t]*(using|Imports)[ \t]+Majorsilence\.Drawing[ \t]*;?[ \t]*$"))
            return text;

        var match = BareDrawingImport.Match(text);
        if (!match.Success)
            return text;

        var indent = match.Groups["indent"].Value;
        var companion = match.Groups["kw"].Value == "Imports"
            ? $"{indent}Imports {NamespaceMap.DrawingTarget}"
            : $"{indent}using {NamespaceMap.DrawingTarget};";

        // Insert directly after the matched import line, preserving the file's existing newline style.
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return text[..match.Index] + match.Value + newline + companion + text[(match.Index + match.Length)..];
    }
}
