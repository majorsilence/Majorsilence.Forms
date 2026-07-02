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

/// <summary>
/// Whether to inject the explicit VB constructor that <c>MyType=Empty</c> removes. The migrator decides
/// this per file with cross-file knowledge of a form's partials (so it never duplicates a constructor
/// that already lives in a sibling, and never writes one into a designer file). Direct callers default
/// to <see cref="Auto"/>, the original single-file heuristic.
/// </summary>
internal enum VbConstructorMode
{
    /// <summary>Inject only if this file alone looks like a form lacking a constructor (single-file heuristic).</summary>
    Auto,

    /// <summary>This file is the chosen target — inject unless it already has a constructor.</summary>
    Inject,

    /// <summary>Never inject here (a designer file, or a sibling already supplies the constructor).</summary>
    Suppress,
}

internal static class SourceConverter
{
    public sealed record Result(string Text, bool Changed, IReadOnlyList<string> Warnings);

    // A fully-qualified System.Drawing type reference, e.g. "System.Drawing.Bitmap". The boundary
    // lookbehind avoids matching the tail of an unrelated namespace (MyApp.System.Drawing.X).
    private static readonly Regex DrawingType =
        new(@"(?<![\w.])System\.Drawing\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    // Precomputed, compiled rewrite rules for NamespaceMap.NamespacePrefixes. Each rule carries a guard —
    // a negative lookahead that excludes the specific dotted remainders this prefix must NOT swallow,
    // because they are deliberately left unrewritten (an UnsupportedNamespaces entry, or a leaf type under
    // NamespaceMap.TelerikUiNamespace with no compat equivalent). Computing the guard once here, instead of
    // hardcoding a single special case, means every prefix — not just System.Windows.Forms — automatically
    // steers clear of whatever the warning passes (4 and 5b) are responsible for reporting.
    private static readonly (string From, string To, Regex Pattern)[] PrefixRules = BuildPrefixRules();

    private static (string, string, Regex)[] BuildPrefixRules()
    {
        // Every "left unrewritten" name, expressed as the full dotted name a rewrite could accidentally
        // produce or clip into: the UnsupportedNamespaces entries themselves, plus each UnmappedTelerikTypes
        // leaf qualified under Telerik.WinControls.UI (the only namespace they're ever written under).
        var protectedNames = NamespaceMap.UnsupportedNamespaces
            .Concat(NamespaceMap.UnmappedTelerikTypes.Select(t => $"{NamespaceMap.TelerikUiNamespace}.{t}"))
            .ToArray();

        var rules = new (string, string, Regex)[NamespaceMap.NamespacePrefixes.Length];
        for (var i = 0; i < NamespaceMap.NamespacePrefixes.Length; i++)
        {
            var (from, to) = NamespaceMap.NamespacePrefixes[i];
            var prefix = from + ".";

            // The remainder of each protected name after this prefix, e.g. prefix "Telerik.WinControls.UI" +
            // protected "Telerik.WinControls.UI.RadPdfViewer" -> remainder "RadPdfViewer"; prefix
            // "Telerik.WinControls" + the same protected name -> remainder "UI.RadPdfViewer" (multi-segment,
            // because the bare prefix would otherwise clip straight through the guarded UI rule's leftovers).
            var remainders = protectedNames
                .Where(p => p.StartsWith(prefix, StringComparison.Ordinal))
                .Select(p => Regex.Escape(p[prefix.Length..]))
                .ToArray();

            // \b (not \w-boundary alone) after each alternative so a longer name sharing a prefix — e.g.
            // RadPdfViewerNavigator vs. RadPdfViewer — doesn't accidentally veto the shorter one's rewrite.
            var guard = remainders.Length == 0 ? "" : $@"(?!\.(?:{string.Join("|", remainders)})\b)";
            var pattern = $@"(?<![\w.]){Regex.Escape(from)}(?!\w){guard}";
            rules[i] = (from, to, new Regex(pattern, RegexOptions.Compiled));
        }
        return rules;
    }

    public static Result Convert(string text, CustomMap? customMap = null, SourceLanguage language = SourceLanguage.CSharp,
        VbConstructorMode vbConstructor = VbConstructorMode.Auto)
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

        // 1. Whole-namespace prefix rewrites (sub-namespaces, Printing, WinForms, and Telerik). Longest-first
        //    so a parent prefix never clips a child. (?!\w) lets a trailing '.' through, so both
        //    `using X;` and `X.Type` are covered. Each compiled pattern already carries its own guard —
        //    see BuildPrefixRules — that steers around whatever the warning passes (4 and 5b) report on.
        foreach (var (_, to, pattern) in PrefixRules)
            text = pattern.Replace(text, to);

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

        // 3. Reconcile a bare `using System.Drawing;`. It's only still needed when the file uses a
        //    System.Drawing primitive (Color/Point/…) unqualified; otherwise drop it. GDI+ types used
        //    unqualified now live in Majorsilence.Drawing, so add/keep that import when they're present.
        text = RewriteDrawingImports(text);

        // 4. Flag any namespace we deliberately refused to rewrite — but only when a reference actually
        //    resolves to something cross-platform-unavailable. Some types under these namespaces ship in
        //    the BCL (e.g. System.ComponentModel.Design.HelpKeywordAttribute, used by typed-DataSet
        //    designer code), and flagging those is a false positive.
        foreach (var unsupported in NamespaceMap.UnsupportedNamespaces)
        {
            if (!original.Contains(unsupported, StringComparison.Ordinal))
                continue;
            if (OnlyReferencesAvailableTypes(original, unsupported))
                continue;
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

        // 5b. Heavyweight Telerik types with no compat equivalent (RadPdfViewer, RadRichTextEditor, the
        //     scheduler data layer, …). Pass 1's guards leave the qualified form untouched; this also
        //     catches the *unqualified* form — e.g. `Dim v As RadPdfViewer` under a bare `Imports
        //     Telerik.WinControls.UI` has no "Telerik" text anywhere near the reference itself, so (unlike
        //     Pass 5's UnmappedDrawingTypes, which gates on a `System.Drawing` import) there is deliberately
        //     no cheaper gate here — the names are distinctive enough that a false positive is unlikely.
        foreach (var type in NamespaceMap.UnmappedTelerikTypes)
        {
            if (Regex.IsMatch(original, $@"(?<!\w){Regex.Escape(type)}\b"))
                Warn($"uses Telerik '{type}', which has no Majorsilence.Forms.Telerik equivalent — left unrewritten; review manually");
        }

        // 6. ComponentResourceManager: designer code instantiates the WinForms-flavoured
        //    System.ComponentModel.ComponentResourceManager. Majorsilence.Forms ships a cross-platform
        //    equivalent (reads the .resx directly, returns Majorsilence.Drawing images), so redirect just
        //    that one type — System.ComponentModel itself stays put (it holds many unrelated BCL types).
        text = Regex.Replace(text,
            @"(?<![\w.])System\.ComponentModel\.ComponentResourceManager\b",
            "Majorsilence.Forms.ComponentResourceManager");

        // 7. Visual Basic specifics. With MyType=Empty there is no implicit WinForms constructor, so
        //    inject the explicit one each form needs; then flag the 'My' framework and Windows-only types
        //    that genuinely need a human.
        if (language == SourceLanguage.VisualBasic)
        {
            text = ApplyVbConstructor(text, vbConstructor, Warn);
            WarnVisualBasic(original, Warn);
        }

        // 8. Collapse duplicate import directives. Distinct source namespaces can map to the same target —
        //    e.g. Telerik.WinControls.UI and Telerik.WinControls.Enumerations both become
        //    Majorsilence.Forms.Telerik — leaving redundant `using`/`Imports` lines (CS0105). Drop the
        //    later exact duplicates, keeping the first.
        text = DeduplicateImports(text);

        return new Result(text, !string.Equals(text, original, StringComparison.Ordinal), warnings);
    }

    // A VB class header, capturing leading indentation and the class name. Tolerates a Partial / access
    // modifier prefix; an attribute list on its own preceding line is matched separately when inserting.
    private static readonly Regex VbClassHeader =
        new(@"(?im)^(?<indent>[ \t]*)(Partial[ \t]+)?(Public[ \t]+|Friend[ \t]+|Private[ \t]+|Protected[ \t]+)*(NotInheritable[ \t]+|MustInherit[ \t]+)?Class[ \t]+(?<name>\w+)[ \t]*\r?$",
        RegexOptions.Compiled);

    // An `Inherits <Base>` line — the VB rule is it must be the first statement in the class body, so a
    // constructor has to be inserted *after* it, not before.
    private static readonly Regex VbInheritsLine =
        new(@"(?im)^[ \t]*Inherits[ \t]+[^\r\n]+\r?$", RegexOptions.Compiled);

    /// <summary>
    /// VB's WinForms application framework synthesised a parameterless constructor that called
    /// <c>InitializeComponent()</c>. <c>MyType=Empty</c> (which the project converter sets for
    /// cross-platform builds) removes that, so a form that has no explicit <c>Sub New()</c> would no
    /// longer initialise its controls. This decides, for the current file, whether to inject it.
    /// </summary>
    private static string ApplyVbConstructor(string text, VbConstructorMode mode, Action<string> warn)
    {
        switch (mode)
        {
            case VbConstructorMode.Suppress:
                // A designer file, or a sibling partial already supplies the constructor.
                return text;

            case VbConstructorMode.Auto:
                // Single-file heuristic: only a file that itself looks like a form (uses
                // InitializeComponent) and has no constructor gets one. The migrator uses the explicit
                // Inject/Suppress modes instead, having looked across the form's partials.
                if (!text.Contains("InitializeComponent", StringComparison.Ordinal))
                    return text;
                return InjectVbConstructor(text, warn);

            default: // Inject — the migrator chose this file as the form's constructor home.
                return InjectVbConstructor(text, warn);
        }
    }

    private static string InjectVbConstructor(string text, Action<string> warn)
    {
        // Idempotent / safe: never add a second constructor.
        if (Regex.IsMatch(text, @"(?i)\bSub\s+New\s*\("))
            return text;

        var header = VbClassHeader.Match(text);
        if (!header.Success)
        {
            warn("VB form has InitializeComponent but no 'Public Sub New()' and no class header could be " +
                 "located to inject one — add a constructor that calls InitializeComponent() manually");
            return text;
        }

        var indent = header.Groups["indent"].Value;
        var memberIndent = indent + "    ";

        // The constructor must follow `Inherits`/`Implements` if present, else sit right after the
        // class line. Scan the lines immediately after the header for a leading Inherits/Implements.
        var insertAt = header.Index + header.Length;
        var scan = insertAt;
        while (true)
        {
            // Skip the newline the header regex stopped on, plus any blank lines.
            var nextLineStart = SkipNewlinesAndBlankLines(text, scan);
            var lineEnd = LineEnd(text, nextLineStart);
            var line = text[nextLineStart..lineEnd];
            if (Regex.IsMatch(line, @"^[ \t]*(Inherits|Implements)\b", RegexOptions.IgnoreCase))
            {
                insertAt = lineEnd;
                scan = lineEnd;
                continue;
            }
            break;
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var ctor =
            $"{newline}{newline}{memberIndent}' [majorsilence-migrate] MyType=Empty removes VB's implicit WinForms constructor; added explicitly.{newline}" +
            $"{memberIndent}Public Sub New(){newline}" +
            $"{memberIndent}    InitializeComponent(){newline}" +
            $"{memberIndent}End Sub";

        return text[..insertAt] + ctor + text[insertAt..];
    }

    // Advance past consecutive newline characters and whitespace-only lines, returning the start index
    // of the next line that contains non-whitespace (or end-of-text).
    private static int SkipNewlinesAndBlankLines(string text, int index)
    {
        while (index < text.Length)
        {
            var lineEnd = LineEnd(text, index);
            if (text[index..lineEnd].Trim().Length != 0)
                return index;
            index = lineEnd < text.Length && text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n'
                ? lineEnd + 2
                : lineEnd + 1;
        }
        return index;
    }

    private static int LineEnd(string text, int index)
    {
        var nl = text.IndexOf('\n', index);
        if (nl < 0)
            return text.Length;
        return nl > index && text[nl - 1] == '\r' ? nl - 1 : nl;
    }

    // A handful of types under otherwise-unsupported namespaces actually ship in the cross-platform BCL,
    // so a reference to them is not a blocker. Keyed by namespace -> the member names that are available.
    private static readonly Dictionary<string, string[]> AvailableTypesUnderUnsupportedNamespace = new(StringComparer.Ordinal)
    {
        // Design-time attributes emitted into typed-DataSet (.xsd) designer code. Pure data-layer files
        // (System.Data), no WinForms — these compile and run as-is on modern .NET.
        ["System.ComponentModel.Design"] = ["HelpKeywordAttribute"],
    };

    // True when every reference to <paramref name="ns"/> in the file targets a known-available type, so
    // there is nothing for a human to fix.
    private static bool OnlyReferencesAvailableTypes(string text, string ns)
    {
        if (!AvailableTypesUnderUnsupportedNamespace.TryGetValue(ns, out var available))
            return false;

        foreach (Match m in Regex.Matches(text, $@"(?<![\w.]){Regex.Escape(ns)}\.(\w+)"))
            if (!available.Contains(m.Groups[1].Value))
                return false;   // a reference to some other (unavailable) member — keep the warning.
        return true;
    }

    private static void WarnVisualBasic(string original, Action<string> warn)
    {
        // The 'My' namespace (My.Settings/My.Resources/My.Application/My.Computer/…) is generated by the
        // VB WinForms application framework, which MyType=Empty switches off for cross-platform builds.
        // Name the specific members in play so the human gets a targeted replacement, not a generic note.
        var myHints = new List<string>();
        void Hint(string member, string advice)
        {
            if (Regex.IsMatch(original, $@"(?<![\w.])My\.{member}\b"))
                myHints.Add(advice);
        }
        Hint("Resources", "My.Resources.X → load from the project .resx (e.g. Majorsilence.Forms.ComponentResourceManager / a ResourceManager)");
        Hint("MySettings", "My.Settings/My.MySettings → a settings class backed by a config/JSON file");
        Hint("Settings", "My.Settings/My.MySettings → a settings class backed by a config/JSON file");
        Hint("Application", "My.Application.X → AppContext/Assembly APIs (e.g. Info.DirectoryPath → AppContext.BaseDirectory)");
        Hint("Computer", "My.Computer.X → System.IO / RuntimeInformation equivalents");
        Hint("User", "My.User → System.Security.Principal / your own identity accessor");
        Hint("Forms", "My.Forms → hold and reuse your own Form instances");
        if (myHints.Count > 0)
            warn("uses the VB 'My.*' namespace, which MyType=Empty disables — " + string.Join("; ", myHints.Distinct()));

        // Microsoft.VisualBasic.Devices.ComputerInfo (and My.Computer.Info) is Windows-desktop only.
        if (original.Contains("ComputerInfo", StringComparison.Ordinal))
            warn("uses 'ComputerInfo', which is Windows-only — replace e.g. OSFullName with RuntimeInformation.OSDescription");

        // The missing-constructor case (MyType=Empty drops VB's implicit WinForms ctor) is now fixed
        // automatically by InjectVbConstructor, which warns only if it cannot find where to insert it.
    }

    // A plain namespace import directive: `using A.B.C;` (C#) or `Imports A.B.C` (VB). Deliberately does
    // NOT match `using static …`, alias imports (`using X = Y;`), or `using (…)` resource statements —
    // those carry meaning beyond the namespace and must never be deduped away.
    private static readonly Regex ImportDirective =
        new(@"^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+(?<ns>[A-Za-z_][\w.]*)[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);

    /// <summary>
    /// Removes redundant import directives, keeping the first occurrence of each. Only plain namespace
    /// imports are considered (see <see cref="ImportDirective"/>); all other lines pass through untouched
    /// and order is preserved. Returns the original text unchanged when there is nothing to remove.
    /// </summary>
    private static string DeduplicateImports(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        var removedAny = false;

        foreach (var raw in lines)
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;
            var m = ImportDirective.Match(line);
            if (m.Success && !seen.Add($"{m.Groups["kw"].Value} {m.Groups["ns"].Value}"))
            {
                removedAny = true;
                continue; // an identical import already appeared earlier — drop this one.
            }
            kept.Add(line);
        }

        if (!removedAny)
            return text;

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return string.Join(newline, kept);
    }

    // Matches a standalone `using System.Drawing;` (C#) or `Imports System.Drawing` (VB) import line.
    // Uses [ \t] rather than \s so it never swallows the line's own newline — VB has no `;` terminator,
    // and a greedy \s* would otherwise consume the break and misplace the inserted companion import.
    private static readonly Regex BareDrawingImport =
        new(@"(?m)^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+System\.Drawing[ \t]*;?[ \t]*$", RegexOptions.Compiled);

    private static string RewriteDrawingImports(string text)
    {
        var match = BareDrawingImport.Match(text);
        if (!match.Success)
            return text;

        // The import is only needed for the primitives Majorsilence.Forms keeps in System.Drawing;
        // GDI+ types used unqualified need the Majorsilence.Drawing companion instead.
        var needsSystemDrawing = NamespaceMap.DrawingPrimitives.Any(p => UsedUnqualified(text, p));
        var usesGdiPlus = NamespaceMap.MajorsilenceDrawingTypes.Any(t => UsedUnqualified(text, t));
        var companionPresent = Regex.IsMatch(text,
            @"(?m)^[ \t]*(using|Imports)[ \t]+Majorsilence\.Drawing[ \t]*;?[ \t]*$");

        var indent = match.Groups["indent"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var companion = match.Groups["kw"].Value == "Imports"
            ? $"{indent}Imports {NamespaceMap.DrawingTarget}"
            : $"{indent}using {NamespaceMap.DrawingTarget};";

        if (needsSystemDrawing)
        {
            // Keep System.Drawing; add the GDI+ companion only when it's actually used and not already there.
            if (usesGdiPlus && !companionPresent)
                return text[..match.Index] + match.Value + newline + companion + text[(match.Index + match.Length)..];
            return text;
        }

        // System.Drawing is no longer needed. Replace it with the Majorsilence.Drawing import when GDI+
        // types are used unqualified, otherwise drop the line entirely.
        var replacement = usesGdiPlus && !companionPresent ? companion : null;
        return RemoveImportLine(text, match, replacement, newline);
    }

    // A type name used unqualified (a whole word not preceded by '.' or another identifier char), i.e. one
    // that depends on an imported namespace rather than a fully-qualified reference.
    private static bool UsedUnqualified(string text, string typeName) =>
        Regex.IsMatch(text, $@"(?<![\w.]){Regex.Escape(typeName)}(?![\w])");

    // Removes the import line the match covers, including its trailing newline so no blank line is left;
    // optionally substitutes a replacement import line in its place.
    private static string RemoveImportLine(string text, Match match, string? replacement, string newline)
    {
        var start = match.Index;
        var end = match.Index + match.Length;
        if (end < text.Length && text[end] == '\r') end++;
        if (end < text.Length && text[end] == '\n') end++;

        var insert = replacement is null ? "" : replacement + newline;
        return text[..start] + insert + text[end..];
    }
}
