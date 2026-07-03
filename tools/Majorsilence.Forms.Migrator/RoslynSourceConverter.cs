using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using CSharpSyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using VBSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using VBSyntaxFactory = Microsoft.CodeAnalysis.VisualBasic.SyntaxFactory;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// The opt-in <c>--engine roslyn</c> counterpart to <see cref="SourceConverter"/>: rewrites namespaces using
/// real symbol resolution (<c>SemanticModel.GetSymbolInfo</c>) instead of regexes, so it can tell apart two
/// same-named types that the textual engine cannot (e.g. a project-local <c>Panel</c> vs.
/// <c>System.Windows.Forms.Panel</c>) — the entire reason this engine exists.
///
/// <b>V1 pass scope</b> (see MIGRATION.md's "optional Roslyn engine" section for the full rationale):
/// <list type="bullet">
///   <item><b>Reimplemented with real symbol resolution</b>: namespace-prefix rewrites (<see cref="NamespaceMap.NamespacePrefixes"/>
///   + <see cref="CustomMap"/>), the <c>System.Drawing</c> 3-way bucketing via <see cref="DrawingTypeClassifier"/>
///   (including types used <i>unqualified</i> under a bare <c>using System.Drawing;</c> — something the
///   textual engine's Pass 5 can only warn about, never fix, because it has no import-resolution of its own),
///   import reconciliation, and <c>System.ComponentModel.ComponentResourceManager</c> redirection.</item>
///   <item><b>Deliberately reused from the textual engine, unchanged</b>: the <c>ApplicationConfiguration.Initialize()</c>
///   comment-out (a small textual post-process over this engine's re-serialized output — no symbol
///   resolution question to answer, a regex is strictly sufficient), the unsupported-namespace warnings
///   (<see cref="NamespaceMap.UnsupportedNamespaces"/>) and unmapped-Telerik-type warnings (same reasoning),
///   and VB's constructor injection / <c>My.*</c> warnings (<see cref="SourceConverter.ApplyVbConstructor"/> /
///   <see cref="SourceConverter.WarnVisualBasic"/>, widened to <c>internal</c> for this reuse).</item>
///   <item><b>Superseded, not reused</b>: the textual engine's Pass 5 (unqualified GDI+-type-under-bare-import
///   warning) has no equivalent call here — this engine's <c>System.Drawing</c> bucketing pass already
///   <i>fixes</i> the unqualified case via symbol resolution rather than merely flagging it, so Roslyn mode
///   intentionally produces <b>fewer</b> warnings for this specific case. A report-diffing reviewer comparing
///   <c>--engine text</c> and <c>--engine roslyn</c> output should expect this divergence, not treat it as a
///   regression.</item>
/// </list>
///
/// Architecture note: despite the design doc's working name ("CSharpNamespaceRewriter : CSharpSyntaxRewriter"),
/// this is implemented as a two-pass resolve-then-apply (a read-only <c>CSharpSyntaxWalker</c>/
/// <c>VisualBasicSyntaxWalker</c> collecting symbol-backed rewrites against the <i>original</i> tree, applied
/// afterwards via <see cref="DocumentEditor"/>) rather than a single interleaved <c>SyntaxRewriter</c> pass.
/// A <c>SemanticModel</c> is bound to the exact tree it was created from; querying it against a
/// newly-fabricated replacement node (which a single-pass rewriter risks doing the moment it starts replacing
/// ancestors before visiting their not-yet-rewritten descendants) throws or silently misresolves. Two-pass
/// resolve/apply is the standard, safer Roslyn idiom for exactly this "resolve a symbol, then replace the
/// node" shape and produces the identical end result the design doc calls for.
/// </summary>
internal static class RoslynSourceConverter
{
    public static async Task<SourceConverter.Result> ConvertAsync(Document document, CustomMap? customMap,
        VbConstructorMode vbConstructor)
    {
        var originalText = (await document.GetTextAsync().ConfigureAwait(false)).ToString();
        var warnings = new List<string>();
        var seenWarnings = new HashSet<string>(StringComparer.Ordinal);
        void Warn(string message)
        {
            if (seenWarnings.Add(message))
                warnings.Add(message);
        }

        var isVb = document.Project.Language == LanguageNames.VisualBasic;
        var map = customMap ?? CustomMap.Empty;

        var editor = await DocumentEditor.CreateAsync(document).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
        var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);

        if (semanticModel is null || root is null)
        {
            // No semantic model / syntax root available at all — nothing a Roslyn pass can safely do with
            // this document. The caller (Migrator) treats this the same as an unresolvable document.
            Warn("--engine roslyn could not obtain a semantic model for this file — left unmodified; review manually");
            return new SourceConverter.Result(originalText, false, warnings);
        }

        var rewrites = isVb
            ? VisualBasicNamespaceRewriter.CollectRewrites(root, semanticModel, map)
            : CSharpNamespaceRewriter.CollectRewrites(root, semanticModel, map);

        foreach (var (node, replacement) in rewrites)
            editor.ReplaceNode(node, replacement);

        var changedDocument = editor.GetChangedDocument();
        var rewrittenText = (await changedDocument.GetTextAsync().ConfigureAwait(false)).ToString();

        // Reconcile the System.Drawing / Majorsilence.Drawing import lines now that the qualified names
        // inside the file have been rewritten by symbol resolution above — mirrors Pass 3 of the textual
        // engine, but operating on this engine's already-rewritten text (see the class doc's pass-scope
        // list: "import reconciliation" is reimplemented, not reused, but shares the textual helper's
        // string-based line surgery since importing/removing a line is not itself a symbol question).
        rewrittenText = ReconcileDrawingImports(rewrittenText, isVb);

        // Deliberately-reused textual passes (see class doc): Pass 0 as a post-process over this engine's
        // output, then the unsupported-namespace / unmapped-Telerik warnings and (for VB) constructor
        // injection + My.* warnings, all evaluated against the *original* text exactly like the textual
        // engine does.
        rewrittenText = ApplyApplicationConfigurationCommentOut(rewrittenText, Warn);
        EmitUnsupportedNamespaceWarnings(originalText, Warn);
        EmitUnmappedTelerikWarnings(originalText, Warn);

        if (isVb)
        {
            rewrittenText = SourceConverter.ApplyVbConstructor(rewrittenText, vbConstructor, Warn);
            SourceConverter.WarnVisualBasic(originalText, Warn);
        }

        rewrittenText = DeduplicateImportLines(rewrittenText);

        return new SourceConverter.Result(rewrittenText, !string.Equals(rewrittenText, originalText, StringComparison.Ordinal), warnings);
    }

    // Mirrors SourceConverter's Pass 0 exactly (same regex, same warning message) — a small textual
    // post-process over this engine's re-serialized output. No ambiguity a symbol resolves here: a
    // 0-argument static method call with a fixed name is a regex away either way.
    private static readonly Regex ApplicationConfigurationInitialize =
        new(@"(?<![\w.])ApplicationConfiguration\.Initialize\s*\(\s*\)\s*;", RegexOptions.Compiled);

    private static string ApplyApplicationConfigurationCommentOut(string text, Action<string> warn) =>
        ApplicationConfigurationInitialize.Replace(text, m =>
        {
            warn("commented out 'ApplicationConfiguration.Initialize()' — Majorsilence.Forms sets up visual styles implicitly");
            return $"// {m.Value} // [majorsilence-migrate] no Majorsilence equivalent";
        });

    // Mirrors SourceConverter's Pass 4 exactly: flags a deliberately-unrewritten namespace only when it
    // resolves to something with no cross-platform equivalent.
    private static void EmitUnsupportedNamespaceWarnings(string originalText, Action<string> warn)
    {
        foreach (var unsupported in NamespaceMap.UnsupportedNamespaces)
        {
            if (!originalText.Contains(unsupported, StringComparison.Ordinal))
                continue;
            if (SourceConverter.OnlyReferencesAvailableTypes(originalText, unsupported))
                continue;
            warn($"references '{unsupported}', which has no Majorsilence equivalent — review manually");
        }
    }

    // Mirrors SourceConverter's Pass 5b exactly: heavyweight Telerik types with no compat equivalent.
    private static void EmitUnmappedTelerikWarnings(string originalText, Action<string> warn)
    {
        foreach (var type in NamespaceMap.UnmappedTelerikTypes)
        {
            if (Regex.IsMatch(originalText, $@"(?<!\w){Regex.Escape(type)}\b"))
                warn($"uses Telerik '{type}', which has no Majorsilence.Forms.Telerik equivalent — left unrewritten; review manually");
        }
    }

    // A plain namespace import directive, same shape as SourceConverter's ImportDirective — reused here
    // (rather than calling into SourceConverter's private regex) because the Roslyn engine's own rewrite
    // already ran via the semantic-model-driven pass above; this only needs to remove exact duplicate
    // *lines* left behind by two distinct source namespaces mapping to the same target (Pass 8's job).
    private static readonly Regex ImportDirectiveLine =
        new(@"^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+(?<ns>[A-Za-z_][\w.]*)[ \t]*;?[ \t]*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

    private static string DeduplicateImportLines(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        var removedAny = false;

        foreach (var raw in lines)
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;
            var m = ImportDirectiveLine.Match(line);
            if (m.Success && !seen.Add($"{m.Groups["kw"].Value} {m.Groups["ns"].Value}"))
            {
                removedAny = true;
                continue;
            }
            kept.Add(line);
        }

        if (!removedAny)
            return text;

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return string.Join(newline, kept);
    }

    // Reconciles a bare `using System.Drawing;` / `Imports System.Drawing` the same way SourceConverter's
    // Pass 3 does, operating on this engine's already-rewritten text: keep it only if a primitive is still
    // referenced unqualified; add/keep the Majorsilence.Drawing companion import only if a GDI+ type is
    // referenced unqualified.
    private static readonly Regex BareDrawingImportLine =
        new(@"(?m)^(?<indent>[ \t]*)(?<kw>using|Imports)[ \t]+System\.Drawing[ \t]*;?[ \t]*$", RegexOptions.Compiled);

    private static string ReconcileDrawingImports(string text, bool isVb)
    {
        var match = BareDrawingImportLine.Match(text);
        if (!match.Success)
            return text;

        var needsSystemDrawing = NamespaceMap.DrawingPrimitives.Any(p => UsedUnqualified(text, p));
        var usesGdiPlus = NamespaceMap.MajorsilenceDrawingTypes.Any(t => UsedUnqualified(text, t));
        var companionPresent = Regex.IsMatch(text,
            @"(?m)^[ \t]*(using|Imports)[ \t]+Majorsilence\.Drawing[ \t]*;?[ \t]*$");

        var indent = match.Groups["indent"].Value;
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var companion = isVb
            ? $"{indent}Imports {NamespaceMap.DrawingTarget}"
            : $"{indent}using {NamespaceMap.DrawingTarget};";

        if (needsSystemDrawing)
        {
            if (usesGdiPlus && !companionPresent)
                return text[..match.Index] + match.Value + newline + companion + text[(match.Index + match.Length)..];
            return text;
        }

        var replacement = usesGdiPlus && !companionPresent ? companion : null;
        return RemoveImportLine(text, match, replacement, newline);
    }

    private static bool UsedUnqualified(string text, string typeName) =>
        Regex.IsMatch(text, $@"(?<![\w.]){Regex.Escape(typeName)}(?![\w])");

    private static string RemoveImportLine(string text, Match match, string? replacement, string newline)
    {
        var start = match.Index;
        var end = match.Index + match.Length;
        if (end < text.Length && text[end] == '\r') end++;
        if (end < text.Length && text[end] == '\n') end++;

        var insert = replacement is null ? "" : replacement + newline;
        return text[..start] + insert + text[end..];
    }

    /// <summary>
    /// Given a symbol's containing namespace, resolves it to the destination namespace the file's engine
    /// should rewrite it to — checking, in order, the built-in <see cref="NamespaceMap.NamespacePrefixes"/>
    /// and the user's <see cref="CustomMap.Namespaces"/> (longest-prefix-first, matching the textual
    /// engine's ordering guarantee from <see cref="CustomMap.Load"/>). Returns null when nothing matches, or
    /// when <paramref name="containingNamespace"/> is itself (or falls under) a namespace deliberately left
    /// unrewritten — <see cref="NamespaceMap.UnsupportedNamespaces"/> — mirroring the textual engine's
    /// per-prefix guard (see <c>SourceConverter.BuildPrefixRules</c>) that keeps e.g.
    /// <c>System.Windows.Forms.VisualStyles</c> from being clipped by the broader
    /// <c>System.Windows.Forms</c> rule.
    /// </summary>
    internal static string? MapNamespace(string containingNamespace, CustomMap customMap)
    {
        if (IsUnsupported(containingNamespace))
            return null;

        foreach (var (from, to) in NamespaceMap.NamespacePrefixes)
        {
            if (containingNamespace == from)
                return to;
            if (containingNamespace.StartsWith(from + ".", StringComparison.Ordinal))
                return to + containingNamespace[from.Length..];
        }

        foreach (var (from, to) in customMap.Namespaces)
        {
            if (containingNamespace == from)
                return to;
            if (containingNamespace.StartsWith(from + ".", StringComparison.Ordinal))
                return to + containingNamespace[from.Length..];
        }

        return null;
    }

    private static bool IsUnsupported(string ns) =>
        NamespaceMap.UnsupportedNamespaces.Any(u => ns == u || ns.StartsWith(u + ".", StringComparison.Ordinal));

    /// <summary>
    /// Classifies and maps a resolved <c>System.Drawing.&lt;Leaf&gt;</c> named-type symbol using the shared
    /// <see cref="DrawingTypeClassifier"/>, returning the destination fully-qualified name, or null when the
    /// type should be left as-is (a primitive, or an unsupported/unknown leaf the warning passes handle).
    /// </summary>
    internal static string? MapDrawingType(INamedTypeSymbol type, out bool isUnsupportedLeaf)
    {
        isUnsupportedLeaf = false;
        if (type.ContainingNamespace?.ToDisplayString() != "System.Drawing")
            return null;

        var name = type.Name;
        return DrawingTypeClassifier.Classify(name) switch
        {
            DrawingTypeBucket.Primitive => null,
            DrawingTypeBucket.MajorsilenceDrawing => $"{NamespaceMap.DrawingTarget}.{name}",
            DrawingTypeBucket.MajorsilenceForms => $"Majorsilence.Forms.{name}",
            DrawingTypeBucket.Unknown => null,
            _ => Mark(ref isUnsupportedLeaf),
        };

        static string? Mark(ref bool flag)
        {
            flag = true;
            return null;
        }
    }

    /// <summary>
    /// True for the one BCL type <see cref="SourceConverter"/>'s Pass 6 redirects unconditionally:
    /// <c>System.ComponentModel.ComponentResourceManager</c> → <c>Majorsilence.Forms.ComponentResourceManager</c>.
    /// </summary>
    internal static bool IsComponentResourceManager(INamedTypeSymbol type) =>
        type.Name == "ComponentResourceManager"
        && type.ContainingNamespace?.ToDisplayString() == "System.ComponentModel";
}

/// <summary>
/// Collects symbol-resolution-backed namespace rewrites for a C# document. See
/// <see cref="RoslynSourceConverter"/>'s class doc for why this is a read-only walker (producing a
/// node-&gt;replacement map) rather than a mutating <c>CSharpSyntaxRewriter</c>.
/// </summary>
internal sealed class CSharpNamespaceRewriter : CSharpSyntaxWalker
{
    private readonly SemanticModel _model;
    private readonly CustomMap _customMap;
    private readonly List<(SyntaxNode Node, SyntaxNode Replacement)> _rewrites = new();

    private CSharpNamespaceRewriter(SemanticModel model, CustomMap customMap) : base(SyntaxWalkerDepth.Node)
    {
        _model = model;
        _customMap = customMap;
    }

    public static IReadOnlyList<(SyntaxNode Node, SyntaxNode Replacement)> CollectRewrites(
        SyntaxNode root, SemanticModel model, CustomMap customMap)
    {
        var walker = new CSharpNamespaceRewriter(model, customMap);
        walker.Visit(root);
        return walker._rewrites;
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        // `using static X;` / `using X = Y;` carry meaning beyond a plain namespace import — never rewrite
        // those here; only a bare `using Namespace;` is in scope (mirrors SourceConverter's ImportDirective
        // deliberately excluding the same two forms).
        if (node.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) || node.Alias is not null || node.Name is null)
            return;

        if (TryResolveNamespace(node.Name, out var mapped))
            _rewrites.Add((node.Name, CSharpSyntaxFactory.ParseName(mapped).WithTriviaFrom(node.Name)));
    }

    public override void VisitQualifiedName(QualifiedNameSyntax node)
    {
        // Skip a qualified name that is itself part of a using directive — VisitUsingDirective already
        // handled the whole `.Name`, and resolving/replacing it twice would just redo the same work.
        if (node.Parent is UsingDirectiveSyntax)
            return;

        if (TryResolveDrawingType(node, out var drawingReplacement))
        {
            _rewrites.Add((node, drawingReplacement!));
            return; // don't descend into a node we just replaced wholesale.
        }

        if (TryResolveNamespace(node, out var mapped))
        {
            _rewrites.Add((node, CSharpSyntaxFactory.ParseName(mapped).WithTriviaFrom(node)));
            return;
        }

        if (TryResolveComponentResourceManager(node, out var crmReplacement))
        {
            _rewrites.Add((node, crmReplacement!));
            return;
        }

        base.VisitQualifiedName(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (TryResolveDrawingType(node, out var drawingReplacement))
        {
            _rewrites.Add((node, drawingReplacement!));
            return;
        }

        if (TryResolveNamespace(node, out var mapped))
        {
            _rewrites.Add((node, CSharpSyntaxFactory.ParseName(mapped).WithTriviaFrom(node)));
            return;
        }

        if (TryResolveComponentResourceManager(node, out var crmReplacement))
        {
            _rewrites.Add((node, crmReplacement!));
            return;
        }

        base.VisitMemberAccessExpression(node);
    }

    // A fully-qualified type reference, e.g. the QualifiedNameSyntax "System.Windows.Forms.Button" (type
    // position) or the MemberAccessExpressionSyntax "System.Windows.Forms.MessageBox" (expression
    // position, itself a child of a larger invocation). Resolves the symbol at this exact node — a
    // namespace prefix match here always covers the *whole* dotted node, so no partial-node splitting
    // is needed the way plain-text regex rewriting has to worry about.
    private bool TryResolveNamespace(ExpressionSyntax node, out string mapped)
    {
        mapped = "";
        var info = _model.GetSymbolInfo(node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

        var ns = symbol switch
        {
            INamespaceSymbol n => n,
            INamedTypeSymbol t => t.ContainingNamespace,
            _ => null,
        };
        if (ns is null)
            return false;

        // For a namespace symbol itself (a using directive's target, or a namespace segment mid-chain),
        // the display string of `ns` *is* the namespace being referenced. For a type symbol, the display
        // string of `node` includes the type name — so we only rewrite the namespace portion (node minus
        // the trailing type-name segment) leaving the leaf identifier untouched.
        var nodeText = node.ToString();
        if (symbol is INamespaceSymbol)
        {
            var full = ns.ToDisplayString();
            var result = RoslynSourceConverter.MapNamespace(full, _customMap);
            if (result is null)
                return false;
            mapped = result;
            return true;
        }

        // Type symbol: rewrite only if the *whole* containing namespace matches — i.e. this QualifiedName/
        // MemberAccess node's text is exactly "<namespace>.<TypeName>" (a bare fully-qualified type ref).
        // A deeper chain (Namespace.Type.Member) resolves at an ancestor node during the same namespace
        // check via a different `node`, since the walker visits every qualified-name/member-access node.
        var containingNs = ns.ToDisplayString();
        if (symbol is not INamedTypeSymbol namedType || !nodeText.EndsWith("." + namedType.Name, StringComparison.Ordinal))
            return false;
        if (nodeText != containingNs + "." + namedType.Name)
            return false;

        var mappedNs = RoslynSourceConverter.MapNamespace(containingNs, _customMap);
        if (mappedNs is null)
            return false;
        mapped = mappedNs + "." + namedType.Name;
        return true;
    }

    private bool TryResolveDrawingType(ExpressionSyntax node, out SyntaxNode? replacement)
    {
        replacement = null;
        var info = _model.GetSymbolInfo(node);
        if (info.Symbol is not INamedTypeSymbol type)
            return false;

        var nodeText = node.ToString();
        if (nodeText != "System.Drawing." + type.Name)
            return false;

        var mapped = RoslynSourceConverter.MapDrawingType(type, out _);
        if (mapped is null)
            return false;

        replacement = CSharpSyntaxFactory.ParseName(mapped).WithTriviaFrom(node);
        return true;
    }

    private bool TryResolveComponentResourceManager(ExpressionSyntax node, out SyntaxNode? replacement)
    {
        replacement = null;
        var info = _model.GetSymbolInfo(node);
        if (info.Symbol is not INamedTypeSymbol type || !RoslynSourceConverter.IsComponentResourceManager(type))
            return false;
        if (node.ToString() != "System.ComponentModel.ComponentResourceManager")
            return false;

        replacement = CSharpSyntaxFactory.ParseName("Majorsilence.Forms.ComponentResourceManager").WithTriviaFrom(node);
        return true;
    }
}

/// <summary>
/// Collects symbol-resolution-backed namespace rewrites for a Visual Basic document. Structurally identical
/// to <see cref="CSharpNamespaceRewriter"/> — see its class doc — swapped onto VB's syntax node kinds
/// (<c>ImportsStatementSyntax</c>/<c>SimpleImportsClauseSyntax</c> instead of <c>UsingDirectiveSyntax</c>;
/// VB's own <c>QualifiedNameSyntax</c>/<c>MemberAccessExpressionSyntax</c>, distinct types from C#'s despite
/// the shared names).
/// </summary>
internal sealed class VisualBasicNamespaceRewriter : Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxWalker
{
    private readonly SemanticModel _model;
    private readonly CustomMap _customMap;
    private readonly List<(SyntaxNode Node, SyntaxNode Replacement)> _rewrites = new();

    private VisualBasicNamespaceRewriter(SemanticModel model, CustomMap customMap)
        : base(Microsoft.CodeAnalysis.SyntaxWalkerDepth.Node)
    {
        _model = model;
        _customMap = customMap;
    }

    public static IReadOnlyList<(SyntaxNode Node, SyntaxNode Replacement)> CollectRewrites(
        SyntaxNode root, SemanticModel model, CustomMap customMap)
    {
        var walker = new VisualBasicNamespaceRewriter(model, customMap);
        walker.Visit(root);
        return walker._rewrites;
    }

    public override void VisitSimpleImportsClause(VBSyntax.SimpleImportsClauseSyntax node)
    {
        // An aliased import (`Imports WF = System.Windows.Forms`) carries meaning beyond a plain namespace
        // import — never rewrite those here, mirroring the C# alias-import exclusion.
        if (node.Alias is not null || node.Name is null)
            return;

        if (TryResolveNamespace(node.Name, out var mapped))
            _rewrites.Add((node.Name, VBSyntaxFactory.ParseName(mapped).WithTriviaFrom(node.Name)));
    }

    public override void VisitQualifiedName(VBSyntax.QualifiedNameSyntax node)
    {
        if (node.Parent is VBSyntax.SimpleImportsClauseSyntax)
            return;

        if (TryResolveDrawingType(node, out var drawingReplacement))
        {
            _rewrites.Add((node, drawingReplacement!));
            return;
        }

        if (TryResolveNamespace(node, out var mapped))
        {
            _rewrites.Add((node, VBSyntaxFactory.ParseName(mapped).WithTriviaFrom(node)));
            return;
        }

        if (TryResolveComponentResourceManager(node, out var crmReplacement))
        {
            _rewrites.Add((node, crmReplacement!));
            return;
        }

        base.VisitQualifiedName(node);
    }

    public override void VisitMemberAccessExpression(VBSyntax.MemberAccessExpressionSyntax node)
    {
        if (TryResolveDrawingType(node, out var drawingReplacement))
        {
            _rewrites.Add((node, drawingReplacement!));
            return;
        }

        if (TryResolveNamespace(node, out var mapped))
        {
            _rewrites.Add((node, VBSyntaxFactory.ParseName(mapped).WithTriviaFrom(node)));
            return;
        }

        if (TryResolveComponentResourceManager(node, out var crmReplacement))
        {
            _rewrites.Add((node, crmReplacement!));
            return;
        }

        base.VisitMemberAccessExpression(node);
    }

    private bool TryResolveNamespace(VBSyntax.ExpressionSyntax node, out string mapped)
    {
        mapped = "";
        var info = _model.GetSymbolInfo(node);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

        var ns = symbol switch
        {
            INamespaceSymbol n => n,
            INamedTypeSymbol t => t.ContainingNamespace,
            _ => null,
        };
        if (ns is null)
            return false;

        var nodeText = node.ToString();
        if (symbol is INamespaceSymbol)
        {
            var full = ns.ToDisplayString();
            var result = RoslynSourceConverter.MapNamespace(full, _customMap);
            if (result is null)
                return false;
            mapped = result;
            return true;
        }

        var containingNs = ns.ToDisplayString();
        if (symbol is not INamedTypeSymbol namedType || !nodeText.EndsWith("." + namedType.Name, StringComparison.Ordinal))
            return false;
        if (nodeText != containingNs + "." + namedType.Name)
            return false;

        var mappedNs = RoslynSourceConverter.MapNamespace(containingNs, _customMap);
        if (mappedNs is null)
            return false;
        mapped = mappedNs + "." + namedType.Name;
        return true;
    }

    private bool TryResolveDrawingType(VBSyntax.ExpressionSyntax node, out SyntaxNode? replacement)
    {
        replacement = null;
        var info = _model.GetSymbolInfo(node);
        if (info.Symbol is not INamedTypeSymbol type)
            return false;

        var nodeText = node.ToString();
        if (nodeText != "System.Drawing." + type.Name)
            return false;

        var mapped = RoslynSourceConverter.MapDrawingType(type, out _);
        if (mapped is null)
            return false;

        replacement = VBSyntaxFactory.ParseName(mapped).WithTriviaFrom(node);
        return true;
    }

    private bool TryResolveComponentResourceManager(VBSyntax.ExpressionSyntax node, out SyntaxNode? replacement)
    {
        replacement = null;
        var info = _model.GetSymbolInfo(node);
        if (info.Symbol is not INamedTypeSymbol type || !RoslynSourceConverter.IsComponentResourceManager(type))
            return false;
        if (node.ToString() != "System.ComponentModel.ComponentResourceManager")
            return false;

        replacement = VBSyntaxFactory.ParseName("Majorsilence.Forms.ComponentResourceManager").WithTriviaFrom(node);
        return true;
    }
}
