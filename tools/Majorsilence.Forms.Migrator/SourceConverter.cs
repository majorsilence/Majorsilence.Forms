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
/// while GDI+ types (Bitmap/Brush/Pen/…) are redirected to <c>Majorsilence.Forms.Drawing</c>. See <see cref="NamespaceMap"/>.
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

    // [DllImport("user32.dll")] / [LibraryImport("user32")] / <DllImport("user32.dll")> (VB), plus VB's
    // older `Declare Function Foo Lib "user32"` form, which names the library without any attribute.
    private static readonly Regex NativeImportDeclaration = new(
        """(?:[\[<]\s*(?:System\.Runtime\.InteropServices\.)?(?:DllImport|LibraryImport)\s*\(\s*"(?<lib>[^"]+)")""" +
        """|(?:\bDeclare\s+(?:Auto\s+|Ansi\s+|Unicode\s+)?(?:Function|Sub)\s+\w+\s+Lib\s+"(?<lib>[^"]+)")""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Declarations spell the same library a few ways -- "user32.dll", "USER32.DLL", "user32". Reduce to
    // one lower-case stem so a single table entry covers them all, and so a file that uses two spellings
    // of the same library warns once rather than once per spelling.
    private static string NormalizeLibraryName(string library)
    {
        var name = library.Trim().ToLowerInvariant();

        // A few declarations give a full path; only the file name identifies the library.
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
            name = name[(slash + 1)..];

        return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }

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

    /// <param name="dualBuild">
    /// When true, the top-of-file <c>using System.Windows.Forms;</c> / <c>Imports System.Windows.Forms</c>
    /// import (and, when added, the <c>Majorsilence.Forms.Drawing</c> companion import) is wrapped in an
    /// <c>#if MAJORSILENCE_FORMS</c> conditional instead of being rewritten unconditionally — see
    /// <see cref="ConditionalImports"/>. Everything else about the rewrite is unchanged.
    /// </param>
    public static Result Convert(string text, CustomMap? customMap = null, SourceLanguage language = SourceLanguage.CSharp,
        VbConstructorMode vbConstructor = VbConstructorMode.Auto, bool dualBuild = false)
    {
        var warnings = new List<string>();
        var seenWarnings = new HashSet<string>(StringComparer.Ordinal);
        var original = text;

        void Warn(string message)
        {
            if (seenWarnings.Add(message))
                warnings.Add(message);
        }

        // --dual-build isn't offered for VB: ApplyVbConstructor's explicit-constructor injection (below,
        // Pass 7) exists precisely because MyType=Empty removes VB's implicit WinForms constructor — and
        // ProjectConverter's dual-build path deliberately never sets MyType=Empty for a VB project (see its
        // comment), so injecting that constructor here would just duplicate the one the still-active VB
        // application framework already supplies. Fall back to the normal, fully-rewritten conversion.
        var effectiveDualBuild = dualBuild && language == SourceLanguage.CSharp;
        if (dualBuild && language == SourceLanguage.VisualBasic)
            Warn("--dual-build is not supported for Visual Basic yet (MyType=Empty and the VB application " +
                "framework can't be toggled via a preprocessor symbol) — converted normally, same as without --dual-build");

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

        // 1b. A file that DECLARED a namespace under System (a library extending System.Windows.Forms with
        //     its own controls does exactly this) reached System.IntPtr, System.EventHandler, System.Type
        //     and friends through namespace nesting, with no `using System;` anywhere in the file. Moving
        //     the declaration to Majorsilence.Forms takes that scope away, and the file stops compiling on
        //     names that have nothing to do with WinForms. Restore it explicitly. Only System is added:
        //     the intermediate ancestors (System.Windows, say) may have no referenced assembly, so a using
        //     for one would be an error in its own right.
        text = RestoreSystemScope(original, text, language);

        // 1a. User-supplied namespace rewrites (e.g. Telerik -> Majorsilence.Forms.Telerik). Same boundary
        //     rules as the built-ins; longest-first ordering is guaranteed by CustomMap.Load.
        foreach (var (from, to) in (customMap ?? CustomMap.Empty).Namespaces)
        {
            var pattern = $@"(?<![\w.]){Regex.Escape(from)}(?!\w)";
            text = Regex.Replace(text, pattern, to.Replace("$", "$$"));
        }

        // 2. System.Drawing type references. Three buckets: keep the framework primitives as-is; redirect
        //    GDI+ types to Majorsilence.Forms.Drawing; redirect the WinForms-compat types (Graphics, ContentAlignment,
        //    SystemColors, …) to Majorsilence.Forms; warn on anything with no Majorsilence equivalent at all.
        text = DrawingType.Replace(text, m =>
        {
            var type = m.Groups[1].Value;
            switch (DrawingTypeClassifier.Classify(type))
            {
                case DrawingTypeBucket.Primitive:
                    return m.Value; // framework primitive — Majorsilence.Forms uses it as-is.
                case DrawingTypeBucket.MajorsilenceDrawing:
                    return $"{NamespaceMap.DrawingTarget}.{type}";
                case DrawingTypeBucket.MajorsilenceForms:
                    return $"Majorsilence.Forms.{type}";
                case DrawingTypeBucket.Unknown:
                    // Part of an already-unsupported sub-namespace (e.g. System.Drawing.Design.UITypeEditor) —
                    // the unsupported-namespace pass below reports it once, cleanly.
                    return m.Value;
                default:
                    Warn($"'System.Drawing.{type}' has no Majorsilence equivalent — review manually");
                    return m.Value;
            }
        });

        // 3. Reconcile a bare `using System.Drawing;`. It's only still needed when the file uses a
        //    System.Drawing primitive (Color/Point/…) unqualified; otherwise drop it. GDI+ types used
        //    unqualified now live in Majorsilence.Forms.Drawing, so add/keep that import when they're present.
        text = RewriteDrawingImports(text, effectiveDualBuild);

        // 3b. A handful of the WinForms-compat types redirected in pass 2 also ship in
        //     System.Drawing.Primitives, so when the file keeps its System.Drawing import both namespaces
        //     offer the same bare name and an unqualified use stops compiling (CS0104). Pin those to the
        //     Majorsilence.Forms one with a using-alias rather than rewriting every use site.
        text = AddAmbiguityAliases(text);

        // 3c. Microsoft.Win32's SystemEvents (and its argument types) ship in the Windows-only
        //     System.Drawing.Common; Majorsilence.Forms reimplements them. Only those names are redirected --
        //     the rest of Microsoft.Win32 (Registry, SafeHandles) is left alone, which is why this is a
        //     type-level rewrite rather than another entry in the namespace table.
        text = RewriteWin32CompatTypes(text);

        // 3d. Same shape for the System.ComponentModel.Design types that ship only in the Windows design
        //     assemblies; the rest of that namespace is real BCL and stays.
        text = RewriteDesignTimeTypes(text);

        // 3e. And pin the handful of names both the BCL and the compat layer declare, so an unqualified use
        //     keeps meaning the BCL type it meant before the migration rather than becoming ambiguous.
        text = AddBclPreferenceAliases(text);

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
                    Warn($"uses '{type}' (System.Drawing.Common) which has no Majorsilence.Forms.Drawing equivalent — review manually");
            }
        }

        // 5c. Windows registry access. Unlike everything else flagged here this is not a compile problem
        //     at all — Microsoft.Win32.Registry resolves on every platform — so the build stays green and
        //     the app dies on its first run instead, typically in a form constructor reading a
        //     run-at-startup entry. Warn so it surfaces in the report rather than as a crash.
        if (original.Contains(NamespaceMap.WindowsRegistryNamespace, StringComparison.Ordinal))
        {
            foreach (var type in NamespaceMap.WindowsOnlyRegistryTypes)
            {
                if (Regex.IsMatch(original, $@"(?<![\w.]){Regex.Escape(type)}\b"))
                    Warn($"uses '{type}' (Windows registry), which has no cross-platform equivalent — " +
                         "it compiles anywhere but only works on Windows; review manually");
            }
        }

        // 5d. P/Invokes into Windows system libraries. Like 5c this compiles everywhere and fails at
        //     run time, but later and more confusingly: the binding happens on the first *call*, so the
        //     app can start, show a form, and only die when some code path reaches the declaration.
        //     Warn per library, and name the managed replacement for the entry points that have one.
        foreach (Match m in NativeImportDeclaration.Matches(original))
        {
            var library = NormalizeLibraryName(m.Groups["lib"].Value);

            if (!NamespaceMap.WindowsOnlyNativeLibraries.Contains(library, StringComparer.OrdinalIgnoreCase))
                continue;

            Warn($"declares a P/Invoke into '{library}' (Windows system library) — it compiles anywhere " +
                 "but throws DllNotFoundException on the first call off Windows; review manually");
        }

        if (NativeImportDeclaration.IsMatch(original))
        {
            foreach (var (entryPoint, replacement) in NamespaceMap.PInvokeManagedEquivalents)
            {
                if (Regex.IsMatch(original, $@"(?<![\w.]){Regex.Escape(entryPoint)}\s*\("))
                    Warn($"'{entryPoint}' is P/Invoked — Majorsilence.Forms exposes {replacement}, " +
                         "which works on every platform");
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
        //    equivalent (reads the .resx directly, returns Majorsilence.Forms.Drawing images), so redirect just
        //    that one type — System.ComponentModel itself stays put (it holds many unrelated BCL types).
        text = Regex.Replace(text,
            @"(?<![\w.])System\.ComponentModel\.ComponentResourceManager\b",
            "Majorsilence.Forms.ComponentResourceManager");

        // 6b. The same problem one layer over, for the *strongly-typed resource designer* a project gets
        //     from a .resx with a code generator set (Resources.Designer.cs, ICO.Designer.cs, ...). Its
        //     accessors are `return ((Icon)(ResourceManager.GetObject ("New", culture)));`, and pass 2 has
        //     just retyped that cast to Majorsilence.Forms.Drawing.Icon — but the manager is still a
        //     System.Resources.ResourceManager, which hands back whatever type the compiled .resources
        //     names (a System.Drawing.Icon, live on Windows or the embedded shim elsewhere). The cast then
        //     throws InvalidCastException at runtime on the first resource read, having compiled cleanly.
        //     Majorsilence.Forms.ComponentResourceManager takes the same (baseName, Assembly) and (Type)
        //     constructors, reads the same compiled .resources, and normalizes graphics entries to
        //     Majorsilence.Forms.Drawing types, so the generated cast succeeds.
        //
        //     Deliberately gated on the generator's own attribute rather than applied file-wide:
        //     System.Resources.ResourceManager is an ordinary BCL type that hand-written code uses for
        //     plain string lookups, and that code should keep the real one.
        if (text.Contains("StronglyTypedResourceBuilder", StringComparison.Ordinal))
        {
            text = Regex.Replace(text,
                @"(?<![\w.])System\.Resources\.ResourceManager\b",
                "Majorsilence.Forms.ComponentResourceManager");

            // The generator emits the name unqualified when it also emits `using System.Resources;`,
            // which the rewrite above cannot see. An alias catches every use without editing generated
            // code -- and it has to be caught: the designer casts what the manager returns to the
            // migrated Bitmap type, so the stock ResourceManager hands back a System.Drawing.Bitmap and
            // every image property throws InvalidCastException at run time, not compile time.
            if (Regex.IsMatch(text, @"(?m)^[ \t]*using[ \t]+System\.Resources[ \t]*;")
                && !Regex.IsMatch(text, @"(?m)^[ \t]*using[ \t]+ResourceManager[ \t]*="))
            {
                text = Regex.Replace(text,
                    @"(?m)^([ \t]*)using([ \t]+)System\.Resources[ \t]*;",
                    "$1using$2System.Resources;\n$1using ResourceManager = Majorsilence.Forms.ComponentResourceManager;",
                    RegexOptions.None);
            }
        }

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

        // 9. Dual-build: Pass 1 has already turned the file's bare `using System.Windows.Forms;` /
        //    `Imports System.Windows.Forms` line into `using Majorsilence.Forms;` — the only namespace
        //    prefix rule that maps to that exact bare left-hand side — so it's identifiable and safe to wrap
        //    here, after every other pass has finished touching the text. See ConditionalImports.
        if (effectiveDualBuild)
            text = ConditionalImports.WrapBareImport(text, "Majorsilence.Forms", "System.Windows.Forms");

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
    internal static string ApplyVbConstructor(string text, VbConstructorMode mode, Action<string> warn)
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
    internal static bool OnlyReferencesAvailableTypes(string text, string ns)
    {
        if (!AvailableTypesUnderUnsupportedNamespace.TryGetValue(ns, out var available))
            return false;

        foreach (Match m in Regex.Matches(text, $@"(?<![\w.]){Regex.Escape(ns)}\.(\w+)"))
            if (!available.Contains(m.Groups[1].Value))
                return false;   // a reference to some other (unavailable) member — keep the warning.
        return true;
    }

    // My.Application.Info.* now resolves against the real Majorsilence.Forms.ApplicationInfo facade (see
    // Application.Info) — so a bare "My.Application" reference, or any OTHER My.Application.* member
    // (Log, Shutdown/Startup, ...), still needs a human, but "My.Application.Info" (and anything hanging
    // off it, e.g. ".Info.Title") does not. The negative lookahead only excludes the ".Info" sub-path;
    // "My.Application.Log"/bare "My.Application" still match and warn as before.
    private static readonly Regex MyApplicationNeedsWarning =
        new(@"(?<![\w.])My\.Application(?!\.Info\b)\b", RegexOptions.Compiled);

    // My.Computer.Name now resolves against Majorsilence.Forms.ComputerInfo — so only that one member is
    // carved out; a bare "My.Computer" reference or any other member (Registry/Clipboard/Info/FileSystem/
    // …) still needs a human.
    private static readonly Regex MyComputerNeedsWarning =
        new(@"(?<![\w.])My\.Computer(?!\.Name\b)\b", RegexOptions.Compiled);

    // internal (not private): the Roslyn engine (RoslynSourceConverter) deliberately reuses this VB helper
    // verbatim rather than reimplementing it — see MIGRATION.md's "what's NOT better with Roslyn" note.
    internal static void WarnVisualBasic(string original, Action<string> warn)
    {
        // The 'My' namespace (My.Settings/My.Application/My.Computer/My.Forms/…) is generated by the VB
        // WinForms application framework, which MyType=Empty switches off for cross-platform builds. Name
        // the specific members in play so the human gets a targeted replacement, not a generic note.
        //
        // Three narrow sub-paths are real, working code against Majorsilence.Forms facades and are
        // deliberately NOT flagged here: My.Application.Info.* (see Application.Info /
        // ApplicationInfo), My.Resources.* (see the migrator's generated My.Resources accessor /
        // ComponentResourceManager), and My.Computer.Name (see ComputerInfo). Every other 'My.*' member —
        // including other My.Application.*/My.Computer.* members — still warns exactly as before.
        var myHints = new List<string>();
        void Hint(string member, string advice)
        {
            if (Regex.IsMatch(original, $@"(?<![\w.])My\.{member}\b"))
                myHints.Add(advice);
        }
        void HintIf(bool condition, string advice)
        {
            if (condition)
                myHints.Add(advice);
        }
        Hint("MySettings", "My.Settings/My.MySettings → a settings class backed by a config/JSON file");
        Hint("Settings", "My.Settings/My.MySettings → a settings class backed by a config/JSON file");
        HintIf(MyApplicationNeedsWarning.IsMatch(original),
            "My.Application.X → AppContext/Assembly APIs (e.g. Info.DirectoryPath → AppContext.BaseDirectory); " +
            "note My.Application.Info.* is now supported directly, see Application.Info");
        HintIf(MyComputerNeedsWarning.IsMatch(original),
            "My.Computer.X → System.IO / RuntimeInformation equivalents; note My.Computer.Name is now supported directly, see ComputerInfo");
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
        new(@"(?m)^(?<indent>[ \t]*)(?<global>global[ \t]+)?(?<kw>using|Imports)[ \t]+System\.Drawing[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);

    private static string RewriteDrawingImports(string text, bool dualBuild = false)
    {
        var match = BareDrawingImport.Match(text);
        if (!match.Success)
            return text;

        // A `global using` governs every file in the project, so this file's own usage says nothing about
        // whether it is needed -- and dropping it would strip the primitives from all of them. Keep it and
        // add the GDI+ companion, which is the pair that compiles: the Windows-only forwarded types in
        // System.Drawing lose to the real Majorsilence.Forms.Drawing ones rather than conflicting.
        // (Without this, a project that centralises its imports this way -- as Krypton's components do --
        // migrated to source that still bound every Bitmap, Font and Graphics to System.Drawing.Common.)
        if (match.Groups["global"].Success)
            return AddGlobalDrawingCompanion(text, match);

        // The import is only needed for the primitives Majorsilence.Forms keeps in System.Drawing;
        // GDI+ types used unqualified need the Majorsilence.Forms.Drawing companion instead.
        var needsSystemDrawing = NamespaceMap.DrawingPrimitives.Any(p => UsedUnqualified(text, p));
        var usesGdiPlus = NamespaceMap.MajorsilenceDrawingTypes.Any(t => UsedUnqualified(text, t));
        var companionPresent = Regex.IsMatch(text,
            $@"(?m)^[ \t]*(using|Imports)[ \t]+{Regex.Escape (NamespaceMap.DrawingTarget)}[ \t]*;?[ \t]*$");

        var indent = match.Groups["indent"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var kw = match.Groups["kw"].Value;
        var companion = kw == "Imports"
            ? $"{indent}Imports {NamespaceMap.DrawingTarget}"
            : $"{indent}using {NamespaceMap.DrawingTarget};";

        if (dualBuild)
        {
            // System.Drawing stays unconditionally whenever anything under it is used unqualified: in
            // WinForms-mode the GDI+ types (Bitmap/Brush/Pen/…) still live directly in System.Drawing too, so
            // the bare import alone already covers both primitives and GDI+ types there. Only the
            // Majorsilence.Forms.Drawing companion — needed in Majorsilence-mode only — becomes conditional.
            if (needsSystemDrawing || usesGdiPlus)
            {
                if (usesGdiPlus && !companionPresent)
                    return text[..match.Index] + match.Value + newline
                        + ConditionalImports.BuildDrawingCompanionBlock(indent, kw, newline)
                        + text[(match.Index + match.Length)..];
                return text;
            }
            return RemoveImportLine(text, match, null, newline);
        }

        if (needsSystemDrawing)
        {
            // Keep System.Drawing; add the GDI+ companion only when it's actually used and not already there.
            if (usesGdiPlus && !companionPresent)
                return text[..match.Index] + match.Value + newline + companion + text[(match.Index + match.Length)..];
            return text;
        }

        // System.Drawing is no longer needed. Replace it with the Majorsilence.Forms.Drawing import when GDI+
        // types are used unqualified, otherwise drop the line entirely.
        var replacement = usesGdiPlus && !companionPresent ? companion : null;
        return RemoveImportLine(text, match, replacement, newline);
    }

    // Matches a `System.ComponentModel.Design` import, plain or global, C# or VB.
    private static readonly Regex DesignTimeImport =
        new(@"(?m)^(?<indent>[ \t]*)(?<global>global[ \t]+)?(?<kw>using|Imports)[ \t]+System\.ComponentModel\.Design[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);

    /// <summary>
    /// Redirects the <c>System.ComponentModel.Design</c> types that live in the Windows-only design
    /// assemblies to their <c>Majorsilence.Forms.Design</c> replacements, leaving that namespace's real BCL
    /// types alone. See <see cref="NamespaceMap.DesignTimeTypes"/>.
    /// </summary>
    /// <remarks>
    /// The fully-qualified form is a straight rewrite. An unqualified use needs a per-type using-alias
    /// instead: real WinForms designer code commonly imports only the bare <c>System.ComponentModel.Design</c>
    /// (every name in the set really is declared there upstream), which off Windows resolves to the
    /// lightweight cross-platform slice of that namespace — one that doesn't include any of these types —
    /// so the bare import must stay (other, real BCL members of it may still be in use) while each
    /// redirected name gets pinned to its Majorsilence.Forms.Design replacement. Same shape as
    /// <see cref="RewriteWin32CompatTypes"/> for a bare <c>Microsoft.Win32</c> import.
    /// </remarks>
    private static string RewriteDesignTimeTypes(string text)
    {
        foreach (var type in NamespaceMap.DesignTimeTypes)
            text = Regex.Replace(text, $@"(?<![\w.])System\.ComponentModel\.Design\.{Regex.Escape (type)}(?![\w])",
                                 $"Majorsilence.Forms.Design.{type}");

        var import = DesignTimeImport.Match(text);
        if (!import.Success)
            return text;

        // Already reachable through a rewritten System.Windows.Forms.Design / System.Drawing.Design import
        // elsewhere in the same file (pass 1 already turned those into Majorsilence.Forms.Design) — nothing
        // more to add.
        if (Regex.IsMatch(text, @"(?m)^[ \t]*(global[ \t]+)?(using|Imports)[ \t]+Majorsilence\.Forms\.Design[ \t]*;?[ \t]*$"))
            return text;

        var isGlobal = import.Groups["global"].Success;
        var indent = import.Groups["indent"].Value;
        var kw = import.Groups["kw"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var aliases = new List<string>();

        foreach (var type in NamespaceMap.DesignTimeTypes)
        {
            if (!UsedUnqualified(text, type) || DeclaresType(text, type))
                continue;
            if (Regex.IsMatch(text, $@"(?m)^[ \t]*(global[ \t]+)?(using|Imports)[ \t]+{Regex.Escape (type)}[ \t]*="))
                continue;

            aliases.Add(kw == "Imports"
                ? $"{indent}Imports {type} = Majorsilence.Forms.Design.{type}"
                : $"{indent}{(isGlobal ? "global " : "")}using {type} = Majorsilence.Forms.Design.{type};");
        }

        if (aliases.Count == 0)
            return text;

        return text[..(import.Index + import.Length)] + newline + string.Join(newline, aliases)
            + text[(import.Index + import.Length)..];
    }

    /// <summary>
    /// Pins a name that both a BCL namespace and a Majorsilence namespace declare to the BCL one the source
    /// originally meant, with a using-alias. See <see cref="NamespaceMap.BclPreferredTypes"/>.
    /// </summary>
    private static string AddBclPreferenceAliases(string text)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        foreach (var (type, ns) in NamespaceMap.BclPreferredTypes)
        {
            if (DeclaresType(text, type))
                continue;

            // Already pinned (including a re-run over an already-migrated tree).
            if (Regex.IsMatch(text, $@"(?m)^[ \t]*(global[ \t]+)?(using|Imports)[ \t]+{Regex.Escape (type)}[ \t]*="))
                continue;

            var import = Regex.Match(text,
                $@"(?m)^(?<indent>[ \t]*)(?<global>global[ \t]+)?(?<kw>using|Imports)[ \t]+{Regex.Escape (ns)}[ \t]*;?[ \t]*$");
            if (!import.Success)
                continue;

            // No ambiguity unless the Majorsilence side is imported too -- an alias then would be noise.
            if (!Regex.IsMatch(text, @"(?m)^[ 	]*(global[ 	]+)?(using|Imports)[ 	]+Majorsilence\.Forms(\.Drawing)?[ 	]*;?[ 	]*$"))
                continue;

            var indent = import.Groups["indent"].Value;
            var isGlobal = import.Groups["global"].Success;

            // A file-scoped alias is only worth adding where the name is actually used; a global one has to
            // be added regardless, because the file that declares a project's global usings is precisely
            // the file that does not use them -- every ambiguous use site is somewhere else.
            if (!isGlobal && !UsedUnqualified(text, type))
                continue;

            var line = import.Groups["kw"].Value == "Imports"
                ? $"{indent}Imports {type} = {ns}.{type}"
                : $"{indent}{(isGlobal ? "global " : "")}using {type} = {ns}.{type};";

            text = text[..(import.Index + import.Length)] + newline + line + text[(import.Index + import.Length)..];
        }

        return text;
    }

    // Matches a `Microsoft.Win32` import, plain or global, C# or VB.
    private static readonly Regex Win32Import =
        new(@"(?m)^(?<indent>[ \t]*)(?<global>global[ \t]+)?(?<kw>using|Imports)[ \t]+Microsoft\.Win32[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);

    /// <summary>
    /// Redirects the <c>Microsoft.Win32</c> system-notification types Majorsilence.Forms reimplements —
    /// <c>SystemEvents</c> and the arguments it raises — leaving the rest of that namespace alone.
    /// </summary>
    /// <remarks>
    /// A themed control library subscribes to <c>SystemEvents.UserPreferenceChanged</c> to rebuild its
    /// palette when the desktop switches between light and dark. The type lives in the Windows-only
    /// <c>System.Drawing.Common</c>, so left as-is every use of it failed to resolve — one type accounted
    /// for 136 of the errors in the Krypton port. An unqualified use gets an alias, as the System.Drawing
    /// ambiguity pass does, because the import that brought it in has to stay for <c>Registry</c>.
    /// </remarks>
    private static string RewriteWin32CompatTypes(string text)
    {
        foreach (var type in NamespaceMap.Win32CompatTypes)
            text = Regex.Replace(text, $@"(?<![\w.])Microsoft\.Win32\.{Regex.Escape (type)}(?![\w])",
                                 $"Majorsilence.Forms.{type}");

        var import = Win32Import.Match(text);
        if (!import.Success)
            return text;

        var isGlobal = import.Groups["global"].Success;
        var indent = import.Groups["indent"].Value;
        var kw = import.Groups["kw"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var aliases = new List<string>();

        foreach (var type in NamespaceMap.Win32CompatTypes)
        {
            if (!UsedUnqualified(text, type) || DeclaresType(text, type))
                continue;
            if (Regex.IsMatch(text, $@"(?m)^[ \t]*(global[ \t]+)?(using|Imports)[ \t]+{Regex.Escape (type)}[ \t]*="))
                continue;

            aliases.Add(kw == "Imports"
                ? $"{indent}Imports {type} = Majorsilence.Forms.{type}"
                : $"{indent}{(isGlobal ? "global " : "")}using {type} = Majorsilence.Forms.{type};");
        }

        if (aliases.Count == 0)
            return text;

        return text[..(import.Index + import.Length)] + newline + string.Join(newline, aliases)
            + text[(import.Index + import.Length)..];
    }

    // Adds `global using Majorsilence.Forms.Drawing;` beside a kept `global using System.Drawing;`.
    private static string AddGlobalDrawingCompanion(string text, Match match)
    {
        var indent = match.Groups["indent"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        if (Regex.IsMatch(text,
                $@"(?m)^[ \t]*global[ \t]+using[ \t]+{Regex.Escape (NamespaceMap.DrawingTarget)}[ \t]*;[ \t]*$"))
            return text;

        var companion = $"{indent}global using {NamespaceMap.DrawingTarget};";
        return text[..(match.Index + match.Length)] + newline + companion + text[(match.Index + match.Length)..];
    }

    // Emits `using SystemColors = Majorsilence.Forms.SystemColors;` (VB: `Imports SystemColors = …`) for
    // each name that a kept System.Drawing import would otherwise make ambiguous. One alias line fixes every
    // use site in the file, which suits a textual rewriter far better than qualifying each reference — and it
    // leaves the code reading exactly as it did before the migration.
    private static string AddAmbiguityAliases(string text)
    {
        // Anchor on the kept `using System.Drawing;` when there is one, so the alias reads as the
        // correction to it. When there isn't, the alias is still needed and often *more* so: the import
        // pass replaces `using System.Drawing;` with the Majorsilence.Forms.Drawing companion in a file
        // that uses GDI+ types, which leaves a name like SystemBrushes -- reimplemented under
        // Majorsilence.Forms, not .Drawing -- with no candidate at all (CS0103) in a file that never
        // needed to import the control namespace. Fall back to the last import or alias line in that case,
        // and to the file's namespace declaration when it has neither (a project that imports through
        // global usings) -- the alias is needed there just the same, and nothing else marks file scope.
        var match = BareDrawingImport.Match(text);
        var anchor = match.Success ? match : LastImportOrAliasLine(text);
        var declaration = anchor is null ? NamespaceDeclaration.Match(text) : Match.Empty;
        if (anchor is null && !declaration.Success)
            return text;

        var kw = anchor?.Groups["kw"].Success == true ? anchor.Groups["kw"].Value : "using";
        var indent = anchor?.Groups["indent"].Success == true ? anchor.Groups["indent"].Value : "";
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var aliases = new List<string>();

        foreach (var type in NamespaceMap.AliasedWithSystemDrawing.OrderBy(t => t, StringComparer.Ordinal))
        {
            if (!UsedUnqualified(text, type))
                continue;

            // The file declares its own type by that name -- alias it and every reference would silently
            // retarget. Leave it alone; a name collision this direct is the author's to resolve.
            if (DeclaresType(text, type))
                continue;

            // Already aliased (e.g. a re-run over an already-migrated tree) — don't add a duplicate.
            if (Regex.IsMatch(text, $@"(?m)^[ \t]*(using|Imports)[ \t]+{Regex.Escape(type)}[ \t]*="))
                continue;

            aliases.Add(kw == "Imports"
                ? $"{indent}Imports {type} = Majorsilence.Forms.{type}"
                : $"{indent}using {type} = Majorsilence.Forms.{type};");
        }

        if (aliases.Count == 0)
            return text;

        // Above the namespace when that is all there is to go by; otherwise directly under the imports.
        if (anchor is null)
            return text[..declaration.Index] + string.Join(newline, aliases) + newline + newline
                + text[declaration.Index..];

        return text[..(anchor.Index + anchor.Length)] + newline + string.Join(newline, aliases)
            + text[(anchor.Index + anchor.Length)..];
    }

    // A namespace declaration, block or file-scoped, C# or VB.
    private static readonly Regex NamespaceDeclaration =
        new(@"(?m)^[ \t]*(namespace|Namespace)[ \t]+(?<name>[\w.]+)", RegexOptions.Compiled);

    /// <summary>
    /// Adds <c>using System;</c> when the file's own namespace declaration has just been moved out from
    /// under <c>System</c>, restoring the implicit access to it that nesting used to provide.
    /// </summary>
    private static string RestoreSystemScope(string original, string text, SourceLanguage language)
    {
        var declared = NamespaceDeclaration.Match(original);
        if (!declared.Success)
            return text;

        var name = declared.Groups["name"].Value;
        if (name != "System" && !name.StartsWith("System.", StringComparison.Ordinal))
            return text;

        // Still under System after the rewrite (nothing mapped it) -- the scope was never lost.
        var rewritten = NamespaceDeclaration.Match(text);
        if (rewritten.Success && rewritten.Groups["name"].Value is var now
            && (now == "System" || now.StartsWith("System.", StringComparison.Ordinal)))
            return text;

        var keyword = language == SourceLanguage.VisualBasic ? "Imports" : "using";
        if (Regex.IsMatch(text, $@"(?m)^[ \t]*{keyword}[ \t]+System[ \t]*;?[ \t]*$"))
            return text;

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var line = language == SourceLanguage.VisualBasic ? "Imports System" : "using System;";

        // After the existing imports when there are any, otherwise straight above the declaration.
        var anchor = LastImportLine(text);
        if (anchor is not null)
            return text[..(anchor.Index + anchor.Length)] + newline + line + text[(anchor.Index + anchor.Length)..];

        var at = NamespaceDeclaration.Match(text);
        return at.Success
            ? text[..at.Index] + line + newline + newline + text[at.Index..]
            : line + newline + text;
    }

    // Any top-level import line, C# or VB. A file-scoped `namespace X;` and any attribute or type
    // declaration come after these, so the last match is the end of the import block.
    //
    // The target has to look like a namespace or type name. `[^=]+` also matched a C# *using statement* --
    // `using (someDisposable)` names no alias, so nothing excluded it -- and since alias directives carry an
    // '=' and are skipped here, a file whose only plain-looking import was that statement anchored its new
    // alias inside a method body, which does not compile. (Krypton's components import through global
    // usings, so that is the normal shape of a file there, not a corner case.)
    private static readonly Regex ImportLine =
        new(@"(?m)^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+(static[ \t]+)?[A-Za-z_@][\w.]*[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);

    // As above, but alias directives (`using X = A.B;`) count too. A project that imports through global
    // usings has files whose only directives are the aliases this migration itself adds, and an alias is
    // just as good an anchor: both sit at the top of the file, at file scope.
    private static readonly Regex ImportOrAliasLine =
        new(@"(?m)^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+([A-Za-z_@][\w]*[ \t]*=[ \t]*)?(static[ \t]+)?[A-Za-z_@][\w.]*[ \t]*;?[ \t]*$",
            RegexOptions.Compiled);

    // The last import line in the file, or null when it has none to hang an alias off.
    private static Match? LastImportLine(string text) => LastMatch(ImportLine, text);

    // The last import or alias line in the file, or null when it has neither.
    private static Match? LastImportOrAliasLine(string text) => LastMatch(ImportOrAliasLine, text);

    private static Match? LastMatch(Regex regex, string text)
    {
        Match? last = null;
        foreach (Match m in regex.Matches(text))
            last = m;

        return last;
    }

    // A type name used unqualified (a whole word not preceded by '.' or another identifier char), i.e. one
    // that depends on an imported namespace rather than a fully-qualified reference.
    private static bool UsedUnqualified(string text, string typeName) =>
        Regex.IsMatch(text, $@"(?<![\w.]){Regex.Escape(typeName)}(?![\w])");

    // Whether this file declares a type of its own by that name (C# or VB).
    private static bool DeclaresType(string text, string typeName) =>
        Regex.IsMatch(text, $@"(?im)^[ \t]*((public|internal|private|protected|partial|sealed|abstract|static|friend|notinheritable|mustinherit)[ \t]+)*(class|struct|interface|enum|record|module)[ \t]+{Regex.Escape(typeName)}\b");

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
