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
            text = InjectVbConstructor(text, Warn);
            WarnVisualBasic(original, Warn);
        }

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
    /// cross-platform builds) removes that, so a form whose designer defines
    /// <c>InitializeComponent</c> but has no explicit <c>Sub New()</c> would no longer initialise its
    /// controls. Insert the constructor the framework used to generate.
    /// </summary>
    private static string InjectVbConstructor(string text, Action<string> warn)
    {
        // Nothing to do unless the file defines/uses InitializeComponent and lacks any constructor.
        if (!text.Contains("InitializeComponent", StringComparison.Ordinal))
            return text;
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
        ["System.ComponentModel.Design"] = new[] { "HelpKeywordAttribute" },
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
