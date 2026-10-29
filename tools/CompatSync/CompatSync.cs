#:package Microsoft.CodeAnalysis.CSharp
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property IsAotCompatible=false
#:property TreatWarningsAsErrors=false

// CompatSync -- builds the System.Windows.Forms-namespace copy of Majorsilence.Forms.
//
//   dotnet run tools/CompatSync/CompatSync.cs -- --project core [--force] [--verbose]
//   dotnet run tools/CompatSync/CompatSync.cs -- --project all
//
// Each project named in compat-rules.json is copied into its git-ignored `generated` folder with the
// namespaces rewritten to the ones real WinForms uses, so a consumer keeps `using System.Windows.Forms;`
// (or VB's `Imports System.Windows.Forms`) and compiles unchanged. The real projects are never touched.
// The *.Compat*.csproj files run this from an MSBuild target, incrementally, on every build.
//
// It is not a find-and-replace. The failures a blind replace hits, and what is done about each:
//   * Strings. A type name in a string is rewritten only when it names a type this repo declares (backend
//     discovery uses such a string); resource names and AppContext switches that merely start with
//     "Majorsilence.Forms" are left alone. Comments are only touched inside cref="...".
//   * Lost namespace nesting. Code in Majorsilence.Forms.Drawing saw its parent Majorsilence.Forms
//     implicitly; System.Drawing is not nested in System.Windows.Forms. Every simple name is resolved the
//     way the original did (nearest enclosing namespace) and a `using` is added where the new home is out
//     of scope.
//   * Collisions. The original's enclosing-namespace lookup beat usings, so where the new using would be
//     ambiguous the tool emits an alias, which does win.
//   * Relocations. Types real WinForms puts in another namespace (ContentAlignment is System.Drawing).
//   * Duplicates across the generated assemblies are reported rather than shipped.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

return CompatSync.Run (args);

static class CompatSync
{
    const int Code = 0, InString = 1, InComment = 2, InDisabled = 3;

    static readonly Regex QualifiedName = new (@"(?<![\w.])Majorsilence\.Forms(?:\.[A-Za-z_]\w*)*", RegexOptions.Compiled);

    static bool verbose;

    public static int Run (string[] args)
    {
        try {
            var wanted = new List<string> ();
            var force = false;
            string? repoArg = null;

            for (var i = 0; i < args.Length; i++) {
                switch (args[i]) {
                    case "--project": wanted.Add (args[++i]); break;
                    case "--repo": repoArg = args[++i]; break;
                    case "--force": force = true; break;
                    case "--verbose": verbose = true; break;
                    default: throw new ArgumentException ($"Unknown argument '{args[i]}'.");
                }
            }

            var repo = repoArg ?? FindRepoRoot ();
            var rulesPath = Path.Combine (repo, "tools", "CompatSync", "compat-rules.json");
            var rulesText = File.ReadAllText (rulesPath);
            var rules = JsonSerializer.Deserialize<Rules> (rulesText, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidDataException ("compat-rules.json is empty.");

            if (wanted.Count == 0 || wanted.Contains ("all"))
                wanted = rules.Projects.Keys.ToList ();

            foreach (var name in wanted)
                if (!rules.Projects.ContainsKey (name))
                    throw new ArgumentException ($"No project '{name}' in compat-rules.json (have: {string.Join (", ", rules.Projects.Keys)}).");

            // The MSBuild target can start this once per target framework in parallel; the first generates,
            // the rest wait here, then find the stamp current and return.
            using var gate = AcquireLock (repo);

            var model = new Model (repo, rules);
            model.Load ();

            var stamp = model.ComputeStamp (rulesText, ThisFile ());
            var failures = 0;

            foreach (var name in wanted) {
                var project = rules.Projects[name];
                var output = Path.GetFullPath (Path.Combine (repo, project.Output));
                var stampFile = Path.Combine (output, ".stamp");

                if (!force && File.Exists (stampFile) && File.ReadAllText (stampFile) == stamp) {
                    Log ($"{name}: up to date");
                    continue;
                }

                failures += model.Generate (name, output, stamp);
            }

            return failures == 0 ? 0 : 1;
        } catch (Exception ex) {
            Console.Error.WriteLine ($"CompatSync: error: {ex.Message}");
            if (verbose)
                Console.Error.WriteLine (ex);
            return 2;
        }
    }

    static FileStream AcquireLock (string repo)
    {
        var name = "compatsync-" + Convert.ToHexString (SHA256.HashData (Encoding.UTF8.GetBytes (repo)))[..16] + ".lock";
        var path = Path.Combine (Path.GetTempPath (), name);
        var deadline = DateTime.UtcNow.AddMinutes (10);

        while (true) {
            try {
                return new FileStream (path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException) when (DateTime.UtcNow < deadline) {
                Thread.Sleep (200);
            }
        }
    }

    static string ThisFile ([CallerFilePath] string path = "") => path;

    static string FindRepoRoot ()
    {
        for (var dir = new DirectoryInfo (Directory.GetCurrentDirectory ()); dir is not null; dir = dir.Parent)
            if (File.Exists (Path.Combine (dir.FullName, "Majorsilence.Forms.slnx")))
                return dir.FullName;

        // Fall back to where this file lives (tools/CompatSync/), for a run from outside the checkout.
        var here = Path.GetDirectoryName (ThisFile ());
        if (here is not null) {
            var root = Path.GetFullPath (Path.Combine (here, "..", ".."));
            if (File.Exists (Path.Combine (root, "Majorsilence.Forms.slnx")))
                return root;
        }

        throw new DirectoryNotFoundException ("Run from inside the repository, or pass --repo <path>.");
    }

    static void Log (string message) => Console.WriteLine ($"CompatSync: {message}");

    // ---- model -----------------------------------------------------------------------------------------

    sealed class Model
    {
        readonly string repo;
        readonly Rules rules;
        readonly Dictionary<string, List<SourceFile>> files = new ();

        // original namespace -> type name -> has a non-generic declaration (an alias needs one)
        readonly Dictionary<string, Dictionary<string, bool>> declared = new (StringComparer.Ordinal);

        // new namespace -> type names; the other half of `declared`, for spotting a clash after the move
        readonly Dictionary<string, HashSet<string>> declaredAfter = new (StringComparer.Ordinal);

        // The same two tables per source project: a project resolves simple names only against itself and the
        // projects it references (Drawing.Common cannot see core's internal Guard, whatever core is called).
        readonly Dictionary<string, Dictionary<string, Dictionary<string, bool>>> declaredIn = new ();
        readonly Dictionary<string, Dictionary<string, HashSet<string>>> declaredAfterIn = new ();

        readonly Dictionary<string, HashSet<string>> globalUsings = new ();

        public Model (string repo, Rules rules)
        {
            this.repo = repo;
            this.rules = rules;
        }

        // ---- loading -----------------------------------------------------------------------------------

        public void Load ()
        {
            foreach (var (name, project) in rules.Projects) {
                var root = Path.Combine (repo, project.Source);
                var list = new List<SourceFile> ();

                foreach (var path in Directory.EnumerateFiles (root, "*.cs", SearchOption.AllDirectories).OrderBy (p => p, StringComparer.Ordinal)) {
                    var rel = Path.GetRelativePath (root, path).Replace ('\\', '/');

                    if (rel.StartsWith ("obj/", StringComparison.Ordinal) || rel.StartsWith ("bin/", StringComparison.Ordinal)
                        || rel.Contains ("/obj/", StringComparison.Ordinal) || rel.Contains ("/bin/", StringComparison.Ordinal))
                        continue;

                    var text = File.ReadAllText (path);
                    var tree = CSharpSyntaxTree.ParseText (text, new CSharpParseOptions (LanguageVersion.Latest));
                    list.Add (new SourceFile (rel, text, tree));
                }

                files[name] = list;
                globalUsings[name] = new HashSet<string> (StringComparer.Ordinal);
            }

            foreach (var (name, list) in files) {
                foreach (var file in list) {
                    var root = file.Tree.GetCompilationUnitRoot ();
                    CollectTopLevelTypes (file, root, "");

                    foreach (var u in root.Usings)
                        if (!u.GlobalKeyword.IsKind (SyntaxKind.None) && u.Alias is null && u.StaticKeyword.IsKind (SyntaxKind.None))
                            globalUsings[name].Add (u.Name!.ToString ());
                }
            }

            foreach (var rel in rules.Relocations)
                foreach (var type in rel.Types)
                    if (!files.Values.SelectMany (f => f).SelectMany (f => f.Types).Any (t => t.Namespace == rel.From && t.Name == type))
                        throw new InvalidDataException ($"Relocation of {rel.From}.{type} names a type no source project declares.");

            foreach (var (project, list) in files) {
                declaredIn[project] = new Dictionary<string, Dictionary<string, bool>> (StringComparer.Ordinal);
                declaredAfterIn[project] = new Dictionary<string, HashSet<string>> (StringComparer.Ordinal);

                foreach (var file in list)
                    foreach (var t in file.Types) {
                        if (!declaredIn[project].TryGetValue (t.Namespace, out var own))
                            declaredIn[project][t.Namespace] = own = new Dictionary<string, bool> (StringComparer.Ordinal);

                        own[t.Name] = (own.TryGetValue (t.Name, out var ng) && ng) || t.Arity == 0;

                        var moved = NewNamespace (t.Namespace, t.Name);

                        if (!declaredAfterIn[project].TryGetValue (moved, out var movedSet))
                            declaredAfterIn[project][moved] = movedSet = new HashSet<string> (StringComparer.Ordinal);

                        movedSet.Add (t.Name);
                    }
            }

            foreach (var list in files.Values)
                foreach (var file in list)
                    foreach (var t in file.Types) {
                        if (!declared.TryGetValue (t.Namespace, out var names))
                            declared[t.Namespace] = names = new Dictionary<string, bool> (StringComparer.Ordinal);

                        names[t.Name] = (names.TryGetValue (t.Name, out var nonGeneric) && nonGeneric) || t.Arity == 0;

                        var after = NewNamespace (t.Namespace, t.Name);

                        if (!declaredAfter.TryGetValue (after, out var set))
                            declaredAfter[after] = set = new HashSet<string> (StringComparer.Ordinal);

                        set.Add (t.Name);
                    }

            foreach (var (name, project) in rules.Projects)
                foreach (var reference in project.References)
                    if (!rules.Projects.ContainsKey (reference))
                        throw new InvalidDataException ($"Project '{name}' references unknown project '{reference}'.");

            foreach (var fix in rules.FileFixups)
                if (!File.Exists (Path.Combine (repo, fix.File)))
                    throw new InvalidDataException ($"fileFixups entry '{fix.File}' does not exist; remove or update it.");
        }

        static void CollectTopLevelTypes (SourceFile file, SyntaxNode container, string ns)
        {
            var members = container switch {
                CompilationUnitSyntax cu => cu.Members,
                BaseNamespaceDeclarationSyntax nd => nd.Members,
                _ => default,
            };

            foreach (var member in members) {
                switch (member) {
                    case BaseNamespaceDeclarationSyntax inner:
                        var name = inner.Name.ToString ().Trim ();
                        CollectTopLevelTypes (file, inner, ns.Length == 0 ? name : ns + "." + name);
                        break;
                    case BaseTypeDeclarationSyntax type:
                        file.Types.Add (new TopType (ns, type.Identifier.ValueText, (type as TypeDeclarationSyntax)?.TypeParameterList?.Parameters.Count ?? 0, type));
                        break;
                    case DelegateDeclarationSyntax del:
                        file.Types.Add (new TopType (ns, del.Identifier.ValueText, del.TypeParameterList?.Parameters.Count ?? 0, del));
                        break;
                }
            }
        }

        public string ComputeStamp (string rulesText, string toolPath)
        {
            var sb = new StringBuilder ();
            sb.Append (rulesText);
            sb.Append (File.Exists (toolPath) ? File.ReadAllText (toolPath) : "");

            foreach (var (name, list) in files.OrderBy (f => f.Key, StringComparer.Ordinal)) {
                sb.Append ('\n').Append (name);
                foreach (var file in list)
                    sb.Append ('\n').Append (file.Relative).Append ('\n').Append (file.Text);
            }

            return Convert.ToHexString (SHA256.HashData (Encoding.UTF8.GetBytes (sb.ToString ())));
        }

        // ---- name mapping ------------------------------------------------------------------------------

        string MapNamespace (string ns)
        {
            var segs = ns.Split ('.');

            for (var k = segs.Length; k >= 1; k--) {
                var prefix = string.Join (".", segs, 0, k);

                if (rules.Namespaces.TryGetValue (prefix, out var mapped))
                    return k == segs.Length ? mapped : mapped + "." + string.Join (".", segs, k, segs.Length - k);
            }

            return ns;
        }

        Relocation? FindRelocation (string ns, string type)
            => rules.Relocations.FirstOrDefault (r => r.From == ns && r.Types.Contains (type));

        string NewNamespace (string ns, string type)
            => FindRelocation (ns, type)?.To ?? MapNamespace (ns);

        bool IsDropped (string ns, string type)
            => rules.Relocations.Any (r => r.Drop && r.From == ns && r.Types.Contains (type));

        /// <summary>Rewrites one dotted name that starts with Majorsilence.Forms, or returns it unchanged.</summary>
        string ResolveQualified (string text, bool strict)
        {
            var segs = text.Split ('.');

            // The longest prefix that is a namespace some source project declares.
            var declaredLength = 0;
            for (var k = segs.Length; k >= 2; k--)
                if (declared.ContainsKey (string.Join (".", segs, 0, k))) {
                    declaredLength = k;
                    break;
                }

            // In a string, only a name that is `<declared namespace>.<declared type>` counts.
            if (strict && (declaredLength == 0 || declaredLength == segs.Length || !declared[string.Join (".", segs, 0, declaredLength)].ContainsKey (segs[declaredLength])))
                return text;

            var relocation = declaredLength > 0 && declaredLength < segs.Length
                ? FindRelocation (string.Join (".", segs, 0, declaredLength), segs[declaredLength])
                : null;

            return relocation is not null
                ? relocation.To + "." + string.Join (".", segs, declaredLength, segs.Length - declaredLength)
                : MapNamespace (text);
        }

        // ---- generation --------------------------------------------------------------------------------

        public int Generate (string name, string output, string stamp)
        {
            var project = rules.Projects[name];

            // The folder is deleted and rebuilt, so refuse anything that is not plainly a `generated` folder
            // inside the repository.
            if (!output.StartsWith (repo, StringComparison.Ordinal) || Path.GetFileName (output) != "generated")
                throw new InvalidOperationException ($"Refusing to clear '{output}': not a 'generated' folder under the repository.");

            if (Directory.Exists (output))
                Directory.Delete (output, true);

            Directory.CreateDirectory (output);

            var count = 0;
            foreach (var file in files[name]) {
                var transformed = Transform (name, file);
                var dest = Path.Combine (output, file.Relative);
                Directory.CreateDirectory (Path.GetDirectoryName (dest)!);
                File.WriteAllText (dest, transformed, new UTF8Encoding (false));
                count++;
            }

            foreach (var copy in project.Copy)
                CopyFile (project, copy, output);

            var ivt = new StringBuilder ("// <auto-generated/>\n");
            foreach (var friend in project.InternalsVisibleTo)
                ivt.Append ($"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo (\"{friend}\")]\n");
            File.WriteAllText (Path.Combine (output, "AssemblyInfo.g.cs"), ivt.ToString (), new UTF8Encoding (false));

            Log ($"{name}: wrote {count} files to {Path.GetRelativePath (repo, output)}");

            var failures = CheckDuplicates ();

            // Written last, and only on success: a failed run must not look up to date.
            if (failures == 0)
                File.WriteAllText (Path.Combine (output, ".stamp"), stamp);

            return failures;
        }

        void CopyFile (ProjectRule project, CopyRule copy, string output)
        {
            var text = File.ReadAllText (Path.Combine (repo, project.Source, copy.From));

            if (copy.Kind == "linker-descriptor") {
                // <assembly fullname="Majorsilence.Forms"> ... <type fullname="Majorsilence.Forms.Control">
                text = Regex.Replace (text, "(<assembly\\s+fullname=\")([^\"]+)(\")", m
                    => m.Groups[1].Value + (m.Groups[2].Value == project.SourceAssembly ? project.Assembly : m.Groups[2].Value) + m.Groups[3].Value);
                text = Regex.Replace (text, "(<type\\s+fullname=\")(Majorsilence\\.Forms[^\"]*)(\")", m
                    => m.Groups[1].Value + ResolveQualified (m.Groups[2].Value, strict: false) + m.Groups[3].Value);
            }

            File.WriteAllText (Path.Combine (output, copy.To ?? copy.From), text, new UTF8Encoding (false));
        }

        // The same type in the same namespace from two generated assemblies is a CS0436 at every use, and so is
        // one the BCL already ships (System.Drawing.SystemColors, ColorTranslator, ...): consumers reference
        // both, so every use of the name is ambiguous.
        int CheckDuplicates ()
        {
            var seen = new Dictionary<string, string> (StringComparer.Ordinal);
            var failures = 0;
            var bcl = LoadFrameworkTypes ();

            foreach (var (project, list) in files)
                foreach (var file in list)
                    foreach (var t in file.Types) {
                        if (IsDropped (t.Namespace, t.Name))
                            continue;

                        var key = NewNamespace (t.Namespace, t.Name) + "." + t.Name + "`" + t.Arity;

                        if (bcl.Contains (key)) {
                            Console.Error.WriteLine ($"CompatSync: error: {key.Replace ('`', '/')} (from {file.Relative}) already ships in the .NET framework, so declaring it again makes every use ambiguous. Add it to a relocation with \"drop\": true so references bind to the framework's.");
                            failures++;
                            continue;
                        }

                        if (seen.TryGetValue (key, out var first) && first != project) {
                            Console.Error.WriteLine ($"CompatSync: error: {key.Replace ('`', '/')} is declared by both '{first}' and '{project}' once moved ({file.Relative}). Add a relocation with \"drop\": true, or relocate one of them.");
                            failures++;
                        } else {
                            seen.TryAdd (key, project);
                        }
                    }

            return failures;
        }

        // Every public type the running .NET framework ships, in the namespaces we generate into.
        HashSet<string> LoadFrameworkTypes ()
        {
            var ours = rules.Projects.Values.SelectMany (_ => rules.Namespaces.Values).Concat (rules.Relocations.Select (r => r.To)).ToHashSet (StringComparer.Ordinal);
            var found = new HashSet<string> (StringComparer.Ordinal);

            foreach (var path in Directory.EnumerateFiles (RuntimeEnvironment.GetRuntimeDirectory (), "*.dll")) {
                try {
                    using var stream = File.OpenRead (path);
                    using var pe = new PEReader (stream);

                    if (!pe.HasMetadata)
                        continue;

                    var reader = pe.GetMetadataReader ();

                    foreach (var handle in reader.TypeDefinitions) {
                        var def = reader.GetTypeDefinition (handle);

                        if ((def.Attributes & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public)
                            continue;

                        var ns = reader.GetString (def.Namespace);

                        if (!ours.Contains (ns))
                            continue;

                        // Generic names already end in `N; give the rest the `0 the generated keys carry.
                        var name = reader.GetString (def.Name);
                        found.Add (ns + "." + (name.Contains ('`') ? name : name + "`0"));
                    }
                } catch (BadImageFormatException) {
                    // native library
                }
            }

            return found;
        }

        // ---- one file ----------------------------------------------------------------------------------

        string Transform (string projectName, SourceFile file)
        {
            var text = file.Text;
            var root = file.Tree.GetCompilationUnitRoot ();
            var kinds = Classify (text, root);
            var edits = new List<Edit> ();

            // Namespace declarations are renamed from what they declare, not by pattern: a relocated type
            // drags its whole namespace block with it, which must not be a mixed one.
            var nameSpans = new List<TextSpan> ();
            foreach (var nd in root.DescendantNodes ().OfType<BaseNamespaceDeclarationSyntax> ()) {
                nameSpans.Add (nd.Name.Span);
                var original = FullNamespace (nd);

                if (!original.StartsWith ("Majorsilence.Forms", StringComparison.Ordinal))
                    continue;

                if (nd.Parent is BaseNamespaceDeclarationSyntax)
                    throw new NotSupportedException ($"{file.Relative}: nested namespace '{original}' is not supported; declare it with its full name.");

                var homes = nd.Members
                    .Select (m => m switch {
                        BaseTypeDeclarationSyntax t => t.Identifier.ValueText,
                        DelegateDeclarationSyntax d => d.Identifier.ValueText,
                        _ => null,
                    })
                    .Where (n => n is not null && !IsDropped (original, n))
                    .Select (n => NewNamespace (original, n!))
                    .Distinct ()
                    .ToList ();

                if (homes.Count > 1)
                    throw new NotSupportedException ($"{file.Relative}: namespace '{original}' mixes types that move to {string.Join (" and ", homes)}; split the file or adjust the relocation.");

                var target = homes.Count == 1 ? homes[0] : MapNamespace (original);

                if (target != original)
                    edits.Add (new Edit (nd.Name.Span.Start, nd.Name.Span.End, target));
            }

            // Dropped declarations are removed whole.
            var removed = new List<TextSpan> ();
            foreach (var t in file.Types.Where (t => IsDropped (t.Namespace, t.Name))) {
                removed.Add (t.Node.FullSpan);
                edits.Add (new Edit (t.Node.FullSpan.Start, t.Node.FullSpan.End, ""));
            }

            // Members real WinForms does not have, whose only users are other removed members.
            foreach (var rule in rules.RemoveMembers)
                foreach (var t in file.Types.Where (t => t.Namespace == rule.From && t.Name == rule.Type)) {
                    var members = t.Node.DescendantNodes ().OfType<MemberDeclarationSyntax> ()
                        .Where (m => m.Parent == t.Node && m switch {
                            PropertyDeclarationSyntax p => rule.Names.Contains (p.Identifier.ValueText),
                            MethodDeclarationSyntax me => rule.Names.Contains (me.Identifier.ValueText),
                            FieldDeclarationSyntax f => f.Declaration.Variables.Any (v => rule.Names.Contains (v.Identifier.ValueText)),
                            _ => false,
                        });

                    foreach (var member in members) {
                        removed.Add (member.FullSpan);
                        edits.Add (new Edit (member.FullSpan.Start, member.FullSpan.End, ""));
                    }
                }

            foreach (Match m in QualifiedName.Matches (text)) {
                if (nameSpans.Any (s => s.Start <= m.Index && m.Index < s.End) || removed.Any (s => s.Start <= m.Index && m.Index < s.End))
                    continue;

                var kind = kinds[m.Index];
                var strict = kind == InString;

                if (kind == InDisabled) {
                    // Inactive #if text is not tokenised, so decide from the line: an odd number of quotes
                    // before the match means it is inside a string; a leading // means a comment.
                    var lineStart = text.LastIndexOf ('\n', Math.Max (0, m.Index - 1)) + 1;
                    var before = text.Substring (lineStart, m.Index - lineStart);

                    if (before.TrimStart ().StartsWith ("//", StringComparison.Ordinal))
                        kind = InComment;
                    else
                        strict = before.Count (c => c == '"') % 2 == 1;
                }

                if (kind == InComment && !(m.Index >= 6 && string.CompareOrdinal (text, m.Index - 6, "cref=\"", 0, 6) == 0))
                    continue;

                // `"Namespace.Type, Majorsilence.Forms.Avalonia"`: the assembly half of an assembly-qualified name.
                var replacement = strict && FollowsComma (text, m.Index) && TryMapAssembly (m.Value, out var assembly)
                    ? assembly
                    : ResolveQualified (m.Value, strict);

                if (replacement != m.Value)
                    edits.Add (new Edit (m.Index, m.Index + m.Length, replacement));
            }

            AddScopeFixes (projectName, file, root, edits);

            var sb = new StringBuilder (text);
            foreach (var edit in edits.OrderByDescending (e => e.Start).ThenByDescending (e => e.End))
                sb.Remove (edit.Start, edit.End - edit.Start).Insert (edit.Start, edit.Text);

            // Auto-generated files default to nullable-disabled regardless of the project setting, and
            // the original relied on the project's enabled one.
            return "// <auto-generated/> by tools/CompatSync from " + file.Relative + "; do not edit.\n#nullable enable\n" + sb;
        }

        static bool FollowsComma (string text, int index)
        {
            var i = index - 1;
            while (i >= 0 && text[i] == ' ')
                i--;
            return i >= 0 && text[i] == ',';
        }

        bool TryMapAssembly (string value, out string mapped)
        {
            foreach (var project in rules.Projects.Values)
                if (project.SourceAssembly == value) {
                    mapped = project.Assembly;
                    return true;
                }

            mapped = value;
            return false;
        }

        static string FullNamespace (BaseNamespaceDeclarationSyntax nd)
        {
            var parts = new List<string> { nd.Name.ToString ().Trim () };

            for (var p = nd.Parent; p is not null; p = p.Parent)
                if (p is BaseNamespaceDeclarationSyntax outer)
                    parts.Insert (0, outer.Name.ToString ().Trim ());

            return string.Join (".", parts);
        }

        static byte[] Classify (string text, CompilationUnitSyntax root)
        {
            var kinds = new byte[text.Length + 1];

            void Fill (TextSpan span, byte kind)
            {
                for (var i = span.Start; i < span.End && i < kinds.Length; i++)
                    kinds[i] = kind;
            }

            foreach (var token in root.DescendantTokens ()) {
                var k = token.Kind ().ToString ();

                if (k.Contains ("StringLiteral", StringComparison.Ordinal) || k == "InterpolatedStringTextToken")
                    Fill (token.Span, InString);
            }

            foreach (var trivia in root.DescendantTrivia ()) {
                var k = trivia.Kind ().ToString ();

                if (k.EndsWith ("CommentTrivia", StringComparison.Ordinal))
                    Fill (trivia.Span, InComment);
                else if (k == "DisabledTextTrivia")
                    Fill (trivia.Span, InDisabled);
            }

            return kinds;
        }

        // ---- scope: usings and aliases -----------------------------------------------------------------

        void AddScopeFixes (string projectName, SourceFile file, CompilationUnitSyntax root, List<Edit> edits)
        {
            var imported = new HashSet<string> (StringComparer.Ordinal);
            foreach (var u in root.DescendantNodes ().OfType<UsingDirectiveSyntax> ())
                if (u.Alias is null && u.StaticKeyword.IsKind (SyntaxKind.None) && u.Name is not null)
                    imported.Add (ResolveQualified (u.Name.ToString (), strict: false));
            foreach (var g in globalUsings[projectName])
                imported.Add (ResolveQualified (g, strict: false));

            var needed = new SortedSet<string> (StringComparer.Ordinal);
            var resolvedTo = new List<(string Type, string Ns)> ();

            // A file can hold several namespace blocks, and a block does not see its siblings, so what is in
            // scope is worked out per block. The usings found are added at the top, where they serve all.
            foreach (var block in root.Members.OfType<BaseNamespaceDeclarationSyntax> ()) {
                var original = FullNamespace (block);
                var blockNew = file.Types
                    .Where (t => t.Namespace == original && !IsDropped (t.Namespace, t.Name))
                    .Select (t => NewNamespace (t.Namespace, t.Name))
                    .ToHashSet (StringComparer.Ordinal);

                bool InScope (string ns)
                    => imported.Contains (ns) || blockNew.Any (o => o == ns || o.StartsWith (ns + ".", StringComparison.Ordinal));

                // Resolve every simple name the way the original did: the nearest enclosing namespace that
                // declares it. If that declaration's new home is not in scope, bring it in.
                foreach (var type in block.DescendantTokens ().Where (t => t.IsKind (SyntaxKind.IdentifierToken)).Select (t => t.ValueText).Distinct ()) {
                    var owner = NearestDeclaring (projectName, original, type);

                    if (owner is null)
                        continue;

                    var after = NewNamespace (owner, type);

                    if (!InScope (after) && Visible (projectName).Any (v => declaredAfterIn[v].TryGetValue (after, out var there) && there.Contains (type))) {
                        needed.Add (after);
                        resolvedTo.Add ((type, after));
                    }
                }
            }

            // A using that is ambiguous for a name needs an alias for it. Ambiguity is spotted among the
            // namespaces we declare, plus the names rules.ExternalNames says other assemblies also have.
            var aliases = new SortedDictionary<string, string> (StringComparer.Ordinal);
            var visible = imported.Union (needed).ToList ();

            foreach (var (type, ns) in resolvedTo) {
                if (!Visible (projectName).Any (v => declaredIn[v].Values.Any (d => d.TryGetValue (type, out var nonGeneric) && nonGeneric)))
                    continue;

                var clash = rules.ExternalNames.Contains (type)
                    || visible.Any (other => other != ns && Visible (projectName).Any (v => declaredAfterIn[v].TryGetValue (other, out var set) && set.Contains (type)));

                if (clash)
                    aliases[type] = ns + "." + type;
            }

            var lines = needed.Select (ns => $"using {ns};").ToList ();
            lines.AddRange (aliases.Select (a => $"using {a.Key} = {a.Value};"));

            var key = (rules.Projects[projectName].Source + "/" + file.Relative).Replace ('\\', '/');
            foreach (var fix in rules.FileFixups.Where (f => f.File.Replace ('\\', '/') == key))
                lines.AddRange (fix.Usings.Select (u => $"using {u};"));

            if (lines.Count == 0)
                return;

            var newline = file.Text.Contains ("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var last = root.Usings.LastOrDefault ();
            var position = last is not null
                ? last.FullSpan.End
                : root.Members.Count > 0 ? root.Members[0].FullSpan.Start : 0;

            var insert = string.Join (newline, lines) + newline;

            if (last is not null && position > 0 && file.Text[position - 1] != '\n')
                insert = newline + insert;

            edits.Add (new Edit (position, position, insert));
        }

        IEnumerable<string> Visible (string project)
            => new[] { project }.Concat (rules.Projects[project].References);

        // The innermost namespace, walking outwards from `ns`, that declares `type` in a project visible from `project`.
        string? NearestDeclaring (string project, string ns, string type)
        {
            while (ns.StartsWith ("Majorsilence.Forms", StringComparison.Ordinal)) {
                if (Visible (project).Any (v => declaredIn[v].TryGetValue (ns, out var names) && names.ContainsKey (type)))
                    return ns;

                var dot = ns.LastIndexOf ('.');
                if (dot < 0)
                    break;

                ns = ns[..dot];
            }

            return null;
        }
    }

    // ---- types -----------------------------------------------------------------------------------------

    sealed record Edit (int Start, int End, string Text);

    sealed record TopType (string Namespace, string Name, int Arity, SyntaxNode Node);

    sealed class SourceFile
    {
        public SourceFile (string relative, string text, SyntaxTree tree)
        {
            Relative = relative;
            Text = text;
            Tree = tree;
        }

        public string Relative { get; }
        public string Text { get; }
        public SyntaxTree Tree { get; }
        public List<TopType> Types { get; } = new ();
    }

    sealed class Rules
    {
        public Dictionary<string, string> Namespaces { get; set; } = new ();
        public List<Relocation> Relocations { get; set; } = new ();
        public List<string> ExternalNames { get; set; } = new ();
        public List<FileFixup> FileFixups { get; set; } = new ();
        public List<RemoveMembersRule> RemoveMembers { get; set; } = new ();
        public Dictionary<string, ProjectRule> Projects { get; set; } = new ();
    }

    sealed class Relocation
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public bool Drop { get; set; }
        public List<string> Types { get; set; } = new ();
    }

    sealed class RemoveMembersRule
    {
        public string From { get; set; } = "";
        public string Type { get; set; } = "";
        public List<string> Names { get; set; } = new ();
    }

    sealed class FileFixup
    {
        public string File { get; set; } = "";
        public List<string> Usings { get; set; } = new ();
    }

    sealed class ProjectRule
    {
        public string Source { get; set; } = "";
        public string SourceAssembly { get; set; } = "";
        public string Assembly { get; set; } = "";
        public string Output { get; set; } = "";
        public List<string> References { get; set; } = new ();
        public List<string> InternalsVisibleTo { get; set; } = new ();
        public List<CopyRule> Copy { get; set; } = new ();
    }

    sealed class CopyRule
    {
        public string From { get; set; } = "";
        public string? To { get; set; }
        public string Kind { get; set; } = "";
    }
}
