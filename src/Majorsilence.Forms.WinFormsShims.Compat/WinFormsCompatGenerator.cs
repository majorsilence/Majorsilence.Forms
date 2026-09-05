using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Majorsilence.Forms.WinFormsShims.Compat;

/// <summary>
/// PoC source generator that emits compatibility surfaces backed by <c>Majorsilence.Forms</c>, one per
/// <see cref="NamespaceMapping"/>: <c>Majorsilence.Forms</c> -&gt; <c>System.Windows.Forms</c> (the
/// WinForms surface) and <c>Majorsilence.Forms.Drawing</c> -&gt; <c>System.Drawing</c> (the GDI+-shaped
/// drawing types real WinForms code also expects -- <c>Font</c>, <c>Brush</c>, the
/// <c>Graphics.DrawString</c> overloads <c>PaintEventArgs.Graphics</c> returns, ...). Each mapping runs
/// the same five independent passes over its own source namespace's public members:
///
/// 1. Every public, non-sealed, non-generic class that does NOT derive from <see cref="System.EventArgs"/>
///    and exposes at least one accessible constructor gets a same-named subclass with forwarding
///    constructors -- <c>Button</c>, <c>Form</c> and the rest of the <c>Component</c> hierarchy, but
///    also plain classes with no <c>Component</c> ancestor at all, like <c>ApplicationContext</c>.
///    EventArgs types are excluded on purpose: pass 5 gives them a different, purpose-built
///    treatment -- a plain compat subclass wouldn't let a handler bind to the original event anyway
///    (C#'s method-group contravariance requires the handler's parameter to be the delegate's
///    declared type or a BASE of it, never a more-derived subclass).
/// 2. Every public, non-nested enum gets a same-named, same-valued copy -- needed because #1/#3/#4/#5's
///    forwarders surface Majorsilence-specific enums such as <c>DialogResult</c> or
///    <c>MessageBoxButtons</c> in their own public signatures, and code that only imports the target
///    namespace has no other way to name them.
/// 3. Every public, non-generic interface gets a same-named, empty sub-interface (<c>IMessageFilter</c>,
///    <c>IWin32Window</c>, <c>IDataObject</c>, ...), so it can be named and implemented under the
///    target namespace and passed as a parameter. This only works as an INPUT: a framework-returned
///    instance implements only the original interface, never this marker, so
///    <see cref="TryTranslateType"/> refuses to hand one back out as the compat type (see
///    <see cref="TypeTranslation.IsDowncastSafe"/>).
/// 4. Every public static, non-generic class -- <c>Application</c>, <c>MessageBox</c>,
///    <c>Clipboard</c>, <c>SystemInformation</c>, ... -- gets a same-named static class that
///    forwards each member whose signature is fully translatable (see <see cref="TryTranslateType"/>,
///    which consults every mapping, not just the one currently being processed -- this is how e.g. a
///    <c>System.Windows.Forms</c> member returning a Drawing type gets that type translated too) into
///    the real Majorsilence.Forms one. A member with any untranslatable type in its signature is
///    silently dropped rather than emitted broken; see the README for what that excludes today.
/// 5. Event shadowing (WinForms mapping only -- there is no Drawing analog of <c>Control</c>):
///    <see cref="DiscoverEventFamilies"/> finds every event <c>Control</c> itself declares whose
///    delegate's second parameter is a Majorsilence-specific EventArgs (Paint, Mouse*, Key*, Drag*,
///    the gesture family, ...). Each distinct EventArgs type gets a compat wrapper class
///    (<see cref="GenerateEventArgsWrapperSource"/>) forwarding its translatable public properties to
///    the real instance it wraps; each distinct NAMED delegate (not the generic
///    <see cref="System.EventHandler{T}"/>, which is reused directly with the compat args type as its
///    argument) gets a compat copy. Then, on every subclass from pass 1 that reaches an overridable
///    On* method and a matching event for a given family (see
///    <see cref="FindReachableOverridableMethod"/>/<see cref="FindReachableEvent"/> -- most classes
///    do, since nearly everything derives from <c>Control</c>), a second partial-class file
///    (<see cref="GenerateEventShadowBlock"/>) overrides the original On* method, translates,
///    dispatches to a NEW compat-typed virtual On* hook of the same name, and shadows the public
///    event with the compat delegate type -- so both <c>control.Paint += handler;</c> and
///    <c>protected override void OnPaint(PaintEventArgs e)</c> work. This has to be repeated on
///    every reaching subclass individually, not solved once on a shared compat <c>Control</c>,
///    because compat subclasses are flat: <c>System.Windows.Forms.Panel</c> derives directly from
///    <c>Majorsilence.Forms.Panel</c>, never from <c>System.Windows.Forms.Control</c> (see
///    BACKLOG.md for the investigation that established this). Scoped deliberately to exactly what
///    <c>Control</c> itself declares -- a control-specific family further out, like
///    <c>TreeView.AfterSelect</c>, is not attempted here.
///
/// Events typed to the real BCL <see cref="System.EventHandler"/> (Click, TextChanged, Resize, ...)
/// need no special handling from pass 5: they already resolve identically regardless of namespace.
///
/// Every emission is also checked against types the *compilation itself* already has in the target
/// namespace (see the "seed" step in <see cref="Execute"/>) before being written -- essential for the
/// Drawing mapping, since a project can genuinely already have real <c>System.Drawing.Rectangle</c>/
/// <c>Point</c>/<c>Color</c> (from the BCL's drawing primitives) even without WinForms itself.
/// </summary>
[Generator]
public sealed class WinFormsCompatGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, static (spc, compilation) => Execute(compilation, spc));
    }

    /// <summary>
    /// One (source namespace -&gt; target namespace) shim this generator maintains. Types belonging to
    /// <see cref="SourceNamespace"/>, gathered from every assembly in <see cref="AssemblyNames"/>
    /// (a namespace can span assemblies; <c>Majorsilence.Forms.Drawing</c> is contributed to by both
    /// the core assembly and the standalone <c>Majorsilence.Forms.Drawing.Common</c>), get a
    /// compatibility surface under <see cref="TargetNamespace"/>. <see cref="Sets"/> and
    /// <see cref="EmittedNames"/> are populated as passes 1-3 run and consulted by every later pass,
    /// including other mappings' pass 4 (a member elsewhere can return/accept a type from this
    /// mapping too).
    /// </summary>
    private sealed class NamespaceMapping
    {
        public NamespaceMapping(string sourceNamespace, string targetNamespace, params string[] assemblyNames)
        {
            SourceNamespace = sourceNamespace;
            TargetNamespace = targetNamespace;
            AssemblyNames = assemblyNames;
        }

        public string SourceNamespace { get; }
        public string TargetNamespace { get; }
        public IReadOnlyList<string> AssemblyNames { get; }
        public CompatSets Sets { get; } = new();

        // Also seeded up front with any type the compilation already has in TargetNamespace (see
        // Execute), so this generator never tries to redeclare a real one and collide with it.
        public HashSet<string> EmittedNames { get; } = new(StringComparer.Ordinal);
    }

    private static void Execute(Compilation compilation, SourceProductionContext context)
    {
        var eventArgsType = compilation.GetTypeByMetadataName("System.EventArgs");
        if (eventArgsType is null)
            return;

        var mappings = new List<NamespaceMapping>
        {
            new("Majorsilence.Forms", "System.Windows.Forms", "Majorsilence.Forms"),
            new("Majorsilence.Forms.Drawing", "System.Drawing", "Majorsilence.Forms", "Majorsilence.Forms.Drawing.Common"),
        };

        var typeMembersByMapping = new Dictionary<NamespaceMapping, List<INamedTypeSymbol>>();
        foreach (var mapping in mappings)
        {
            var members = new List<INamedTypeSymbol>();
            foreach (var assemblyName in mapping.AssemblyNames)
            {
                var assembly = compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(a => a.Name == assemblyName);
                if (assembly is null)
                    continue;
                if (FindNamespace(assembly.GlobalNamespace, mapping.SourceNamespace) is { } ns)
                    members.AddRange(ns.GetTypeMembers());
            }
            typeMembersByMapping[mapping] = members;

            // Deliberately NOT seeded with whatever `compilation.GlobalNamespace` already reports for
            // the target namespace: on many TFMs that resolves things like System.Drawing.Font to an
            // unresolvable type-forwarder stub (a reference-assembly facade forwards the full GDI+
            // surface to a real "System.Drawing.Common" NuGet package that isn't referenced), which
            // looks like a normal, real, already-declared type from every angle *except* actually
            // using it (CS1069) -- seeding from it would block this generator's own, perfectly valid
            // declaration for that exact name. A type genuinely declared in THIS compilation's own
            // source always wins ordinary name lookup over a referenced assembly's forward, so no
            // seeding is needed for that case either; a same-named type from an ACTUALLY resolvable
            // referenced assembly (vanishingly unlikely for e.g. Font/Brush/Graphics, and impossible
            // for the WinForms mapping's System.Windows.Forms unless something references the real
            // WinForms assembly) would show up as a plain, easy-to-diagnose CS0101 rather than being
            // silently avoided -- an acceptable PoC tradeoff over a much more expensive full-syntax
            // scan to detect it in advance.
        }

        if (typeMembersByMapping[mappings[0]].Count == 0)
            return; // core assembly not referenced -- nothing to do

        // Passes 1-3, independently per mapping.
        foreach (var mapping in mappings)
        {
            var typeMembers = typeMembersByMapping[mapping];

            // Pass 1: eligible classes -> forwarding-constructor subclasses.
            foreach (var type in typeMembers)
            {
                if (!IsEligibleClass(type, eventArgsType))
                    continue;

                var ctors = GetAccessibleConstructors(compilation, type);
                if (ctors.Count == 0)
                    continue;

                if (!mapping.EmittedNames.Add(type.Name))
                    continue;

                mapping.Sets.Subclasses.Add(type);
                var source = GenerateSource(type, ctors, mapping);
                context.AddSource(HintName(mapping, type.Name, "g"), SourceText.From(source, Encoding.UTF8));
            }

            // Pass 2: public enums -> identical copies, so pass 1's own generated signatures and pass
            // 4 can reference an enum type that resolves under the target namespace.
            foreach (var type in typeMembers)
            {
                if (type.TypeKind != TypeKind.Enum)
                    continue;
                if (type.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (type.ContainingType is not null)
                    continue;

                if (!mapping.EmittedNames.Add(type.Name))
                    continue;

                mapping.Sets.Enums.Add(type);
                var source = GenerateEnumSource(type, mapping);
                context.AddSource(HintName(mapping, type.Name, "Enum.g"), SourceText.From(source, Encoding.UTF8));
            }

            // Pass 3: public interfaces -> empty sub-interfaces, usable as parameter types (see the
            // class remarks and TypeTranslation.IsDowncastSafe for why not as return types).
            foreach (var type in typeMembers)
            {
                if (type.TypeKind != TypeKind.Interface)
                    continue;
                if (type.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (type.IsGenericType)
                    continue;
                if (type.ContainingType is not null)
                    continue;

                if (!mapping.EmittedNames.Add(type.Name))
                    continue;

                mapping.Sets.Interfaces.Add(type);
                var source = GenerateInterfaceSource(type, mapping);
                context.AddSource(HintName(mapping, type.Name, "Interface.g"), SourceText.From(source, Encoding.UTF8));
            }
        }

        // Pass "event families" -- WinForms mapping only; Control has no Drawing-mapping analog. See
        // DiscoverEventFamilies for why this is scoped to exactly what Control itself can reach, and
        // BACKLOG.md for why the shadow has to be re-emitted per subclass rather than solved once.
        var winFormsMapping = mappings[0];
        var genericEventHandlerType = compilation.GetTypeByMetadataName("System.EventHandler`1");
        if (genericEventHandlerType is not null
            && typeMembersByMapping[winFormsMapping].FirstOrDefault(t => t.Name == "Control") is { } controlType)
        {
            var families = DiscoverEventFamilies(controlType, eventArgsType, genericEventHandlerType, winFormsMapping)
                .Where(f => FindReachableOverridableMethod(controlType, "On" + f.EventName, f.ArgsType) is not null)
                .ToList();

            var compatArgsTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var argsType in families.Select(f => f.ArgsType).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>())
            {
                if (!winFormsMapping.EmittedNames.Add(argsType.Name))
                    continue; // name collision; skip -- families using this args type fall out below too

                compatArgsTypes.Add(argsType);
                var source = GenerateEventArgsWrapperSource(argsType, eventArgsType, winFormsMapping, mappings);
                context.AddSource(HintName(winFormsMapping, argsType.Name, "EventArgs.g"), SourceText.From(source, Encoding.UTF8));
            }

            // Named custom delegates (PaintEventHandler, MouseEventHandler, ...) need a compat copy;
            // a constructed `EventHandler<T>` doesn't -- the compat event just reuses the BCL generic
            // delegate with the compat args type as its argument (see EventFamily.IsGenericEventHandler).
            var compatDelegates = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var family in families)
            {
                if (family.IsGenericEventHandler)
                    continue;
                if (!compatArgsTypes.Contains(family.ArgsType))
                    continue;
                if (!compatDelegates.Add(family.DelegateType))
                    continue;

                var source = GenerateDelegateSource(family.DelegateType, family.ArgsType.Name, winFormsMapping);
                context.AddSource(HintName(winFormsMapping, family.DelegateType.Name, "Delegate.g"), SourceText.From(source, Encoding.UTF8));
            }

            families = families
                .Where(f => compatArgsTypes.Contains(f.ArgsType) && (f.IsGenericEventHandler || compatDelegates.Contains(f.DelegateType)))
                .ToList();

            foreach (var type in winFormsMapping.Sets.Subclasses)
            {
                var blocks = new List<string>();
                foreach (var family in families)
                {
                    if (FindReachableOverridableMethod(type, "On" + family.EventName, family.ArgsType) is null)
                        continue;
                    if (FindReachableEvent(type, family.EventName, family.DelegateType) is null)
                        continue;

                    blocks.Add(GenerateEventShadowBlock(family, winFormsMapping));
                }

                if (blocks.Count == 0)
                    continue;

                var source = GenerateEventShadowFileSource(type, winFormsMapping, blocks);
                context.AddSource(HintName(winFormsMapping, type.Name, "Events.g"), SourceText.From(source, Encoding.UTF8));
            }
        }

        // Pass 4: public static utility classes -> forwarding static classes, per mapping. Each type's
        // members are translated against ALL mappings (see TryTranslateType), not just its own, so a
        // System.Windows.Forms member returning a Drawing type still gets that type translated.
        foreach (var mapping in mappings)
        {
            foreach (var type in typeMembersByMapping[mapping])
            {
                if (!IsEligibleStaticClass(type))
                    continue;

                var memberBlocks = CollectStaticMemberBlocks(type, mapping, mappings);
                if (memberBlocks.Count == 0)
                    continue;

                if (!mapping.EmittedNames.Add(type.Name))
                    continue;

                var source = GenerateStaticWrapperSource(type, mapping, memberBlocks);
                context.AddSource(HintName(mapping, type.Name, "Static.g"), SourceText.From(source, Encoding.UTF8));
            }
        }
    }

    /// <summary>The three kinds of compat counterpart a type in one mapping's source namespace can
    /// have, populated by that mapping's passes 1-3 and consulted by <see cref="TryTranslateType"/>
    /// during any mapping's pass 4/5.</summary>
    private sealed class CompatSets
    {
        public HashSet<INamedTypeSymbol> Subclasses { get; } = new(SymbolEqualityComparer.Default);
        public HashSet<INamedTypeSymbol> Enums { get; } = new(SymbolEqualityComparer.Default);
        public HashSet<INamedTypeSymbol> Interfaces { get; } = new(SymbolEqualityComparer.Default);
    }

    // Hint names are prefixed with the target namespace so two mappings can never collide even if
    // (hypothetically) they each had a type of the same simple name -- they'd land in different C#
    // namespaces, but AddSource's hint names must be unique across the whole generator run regardless.
    private static string HintName(NamespaceMapping mapping, string typeName, string suffix) =>
        $"{mapping.TargetNamespace}.{typeName}.{suffix}.cs";

    private static INamespaceSymbol? FindNamespace(INamespaceSymbol root, string dottedName)
    {
        INamespaceSymbol current = root;
        foreach (var part in dottedName.Split('.'))
        {
            var next = current.GetNamespaceMembers().FirstOrDefault(n => n.Name == part);
            if (next is null)
                return null;
            current = next;
        }
        return current;
    }

    private static bool IsEligibleClass(INamedTypeSymbol type, INamedTypeSymbol eventArgsType)
    {
        if (type.DeclaredAccessibility != Accessibility.Public)
            return false;
        if (type.TypeKind != TypeKind.Class)
            return false;
        if (type.IsSealed || type.IsStatic)
            return false;
        if (type.IsGenericType)
            return false; // out of scope for this PoC
        if (type.ContainingType is not null)
            return false; // nested types out of scope for this PoC
        if (DerivesFrom(type, eventArgsType))
            return false; // see the class remarks: a subclass can't satisfy a delegate's base-typed parameter

        return true;
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol candidateBase)
    {
        for (var b = type.BaseType; b is not null; b = b.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(b, candidateBase))
                return true;
        }
        return false;
    }

    private static List<IMethodSymbol> GetAccessibleConstructors(Compilation compilation, INamedTypeSymbol type)
    {
        var result = new List<IMethodSymbol>();
        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                continue;
            if (ctor.Parameters.Any(p => !IsPubliclyAccessible(p.Type)))
                continue;
            result.Add(ctor);
        }
        return result;
    }

    private static bool IsPubliclyAccessible(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return IsPubliclyAccessible(array.ElementType);
        if (type is not INamedTypeSymbol named)
            return true; // type parameters, pointers, etc. -- assume fine
        return named.DeclaredAccessibility is Accessibility.Public or Accessibility.NotApplicable;
    }

    private static string GenerateSource(INamedTypeSymbol type, List<IMethodSymbol> ctors, NamespaceMapping mapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- a thin subclass so unmodified");
        sb.AppendLine($"// `{mapping.TargetNamespace}` source keeps compiling against Majorsilence.Forms. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {mapping.TargetNamespace}");
        sb.AppendLine("{");

        // `partial` even though most of these have exactly one part: the event-shadowing pass
        // (see DiscoverEventFamilies) adds a second part -- an On*/`new event` pair -- to any
        // WinForms-mapping subclass whose original type reaches Control's Paint/Mouse/Key family.
        var baseRef = "global::" + mapping.SourceNamespace + "." + type.Name;
        var classKeyword = type.IsAbstract ? "abstract partial class" : "partial class";
        sb.AppendLine($"    public {classKeyword} {type.Name} : {baseRef}");
        sb.AppendLine("    {");

        foreach (var ctor in ctors)
        {
            var accessibility = AccessibilityKeyword(ctor.DeclaredAccessibility);
            var (parameterList, argumentList) = BuildParameterAndArgumentLists(ctor);
            sb.AppendLine($"        {accessibility} {type.Name}({parameterList}) : base({argumentList})");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        _ => "public",
    };

    private static (string ParameterList, string ArgumentList) BuildParameterAndArgumentLists(IMethodSymbol ctor)
    {
        var parameters = new List<string>();
        var arguments = new List<string>();

        // Once any parameter's default can't be safely rendered, drop defaults for the whole
        // constructor rather than emit an invalid "optional before required" parameter list.
        var defaults = new string?[ctor.Parameters.Length];
        var allDefaultsRenderable = true;
        for (var i = 0; i < ctor.Parameters.Length; i++)
        {
            var p = ctor.Parameters[i];
            if (!p.HasExplicitDefaultValue)
                continue;
            var rendered = FormatDefaultValue(p);
            defaults[i] = rendered;
            if (rendered is null)
                allDefaultsRenderable = false;
        }

        for (var i = 0; i < ctor.Parameters.Length; i++)
        {
            var p = ctor.Parameters[i];
            var modifier = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => "",
            };
            var paramsPrefix = p.IsParams ? "params " : "";
            var typeStr = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var defaultStr = allDefaultsRenderable && defaults[i] is { } d ? " = " + d : "";
            parameters.Add($"{paramsPrefix}{modifier}{typeStr} {p.Name}{defaultStr}");
            arguments.Add($"{modifier}{p.Name}");
        }

        return (string.Join(", ", parameters), string.Join(", ", arguments));
    }

    private static string? FormatDefaultValue(IParameterSymbol p, string? typeStrOverride = null)
    {
        var value = p.ExplicitDefaultValue;
        var typeStr = typeStrOverride ?? p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (value is null)
            return "default";

        if (p.Type.TypeKind == TypeKind.Enum)
            return $"({typeStr}){Convert.ToInt64(value, CultureInfo.InvariantCulture)}";

        return value switch
        {
            bool b => b ? "true" : "false",
            string s => EscapeStringLiteral(s),
            char c => "'" + EscapeCharForLiteral(c) + "'",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            sbyte or byte or short or ushort or int or uint or long or ulong =>
                Convert.ToString(value, CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private static string EscapeStringLiteral(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
            sb.Append(c == '"' || c == '\\' ? EscapeCharForLiteral(c) : c.ToString());
        sb.Append('"');
        return sb.ToString();
    }

    private static string EscapeCharForLiteral(char c) => c switch
    {
        '\'' => "\\'",
        '"' => "\\\"",
        '\\' => "\\\\",
        '\0' => "\\0",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        _ => c.ToString(),
    };

    // ── Enum copies (pass 2) ────────────────────────────────────────────────────────────────

    private static string GenerateEnumSource(INamedTypeSymbol enumType, NamespaceMapping mapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- an identical copy of the");
        sb.AppendLine($"// {mapping.SourceNamespace} enum of the same name, so code compiling only against");
        sb.AppendLine($"// `{mapping.TargetNamespace}` can still name it. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {mapping.TargetNamespace}");
        sb.AppendLine("{");

        var isFlags = enumType.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
        if (isFlags)
            sb.AppendLine("    [global::System.Flags]");

        var underlying = enumType.EnumUnderlyingType?.SpecialType;
        var underlyingClause = underlying is null or SpecialType.System_Int32
            ? ""
            : $" : {enumType.EnumUnderlyingType!.ToDisplayString(DisplayFormatWithNullability)}";
        sb.AppendLine($"    public enum {enumType.Name}{underlyingClause}");
        sb.AppendLine("    {");

        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (!member.HasConstantValue)
                continue;

            var literal = Convert.ToString(member.ConstantValue, CultureInfo.InvariantCulture);
            sb.AppendLine($"        {member.Name} = {literal},");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Interface copies (pass 3) ───────────────────────────────────────────────────────────

    private static string GenerateInterfaceSource(INamedTypeSymbol interfaceType, NamespaceMapping mapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- an empty interface that");
        sb.AppendLine($"// extends the {mapping.SourceNamespace} interface of the same name, so it can be named,");
        sb.AppendLine($"// implemented, and passed as a parameter under `{mapping.TargetNamespace}`. Handing a");
        sb.AppendLine("// value back OUT as this type is not supported: a framework-returned instance");
        sb.AppendLine("// implements only the original interface, never this one. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {mapping.TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public interface {interfaceType.Name} : global::{mapping.SourceNamespace}.{interfaceType.Name}");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Static utility class forwarding (pass 4) ───────────────────────────────────────────

    private static bool IsEligibleStaticClass(INamedTypeSymbol type)
    {
        if (type.DeclaredAccessibility != Accessibility.Public)
            return false;
        if (type.TypeKind != TypeKind.Class)
            return false;
        if (!type.IsStatic)
            return false;
        if (type.IsGenericType)
            return false;
        if (type.ContainingType is not null)
            return false;
        return true;
    }

    /// <summary>Finds which mapping (if any) owns <paramref name="type"/>, by matching its containing
    /// namespace against each mapping's source namespace. Null means <paramref name="type"/> is
    /// outside every mapping this generator knows about (a BCL type, System.Drawing's real
    /// Rectangle/Point/Color, ...) and needs no translation at all.</summary>
    private static NamespaceMapping? FindMapping(ITypeSymbol type, IReadOnlyList<NamespaceMapping> mappings)
    {
        if (type.ContainingNamespace is not { IsGlobalNamespace: false } ns)
            return null;

        var display = ns.ToDisplayString();
        foreach (var mapping in mappings)
        {
            if (display == mapping.SourceNamespace)
                return mapping;
        }
        return null;
    }

    /// <summary>
    /// A member signature's type, restated as the two forms a forwarding wrapper needs: the type its
    /// own (compat-namespace) signature exposes, and the type the real Majorsilence.Forms member
    /// actually uses -- plus whether converting between them, in each direction, needs an explicit
    /// cast.
    /// </summary>
    private readonly struct TypeTranslation
    {
        public TypeTranslation(
            string compatDisplay, string originalDisplay, bool needsCastToOriginal, bool needsCastToCompat,
            bool isDowncastSafe = true, bool isReferenceDowncast = false)
        {
            CompatDisplay = compatDisplay;
            OriginalDisplay = originalDisplay;
            NeedsCastToOriginal = needsCastToOriginal;
            NeedsCastToCompat = needsCastToCompat;
            IsDowncastSafe = isDowncastSafe;
            IsReferenceDowncast = isReferenceDowncast;
        }

        public string CompatDisplay { get; }
        public string OriginalDisplay { get; }
        public bool NeedsCastToOriginal { get; }

        /// <summary>Whether handing a value of this type back OUT as <see cref="CompatDisplay"/>
        /// needs an explicit cast. True for enums (unrelated types, but same values by construction)
        /// and Component-derived subclasses (a downcast, assumed safe because a consumer of this
        /// package only ever constructs the compat subclass). False for interfaces -- see
        /// <see cref="IsDowncastSafe"/>.</summary>
        public bool NeedsCastToCompat { get; }

        /// <summary>
        /// Whether <see cref="NeedsCastToCompat"/>, if true, is actually safe to emit. True for enums
        /// and Component-derived subclasses. False for interfaces (pass 3): a compat interface is an
        /// empty marker sub-interface, and a framework-returned instance was never constructed as
        /// that marker, so a cast to it would fail at runtime for every real value -- callers must
        /// reject the whole member rather than emit that cast.
        /// </summary>
        public bool IsDowncastSafe { get; }

        /// <summary>
        /// True only for the class-subclass downcast case. Even a "safe" class downcast can still
        /// throw: a base-typed member (e.g. <c>Brushes.Black</c>, typed <c>Brush</c>) can hand back a
        /// framework-owned singleton of some OTHER, sealed leaf subclass (<c>SolidBrush</c>) that was
        /// never constructed as the compat subclass at all -- unrelated to it, in fact, since both are
        /// siblings under the same original base. A hard cast there throws every time; casting with
        /// `as` instead (see <see cref="BuildDowncastExpression"/>) turns that into a null, which -
        /// unlike interfaces - is still worth doing because the common case (a user-constructed
        /// instance, e.g. <c>Application.MainForm</c>) genuinely is the compat subclass and the cast
        /// genuinely succeeds. Enum downcasts don't need this: an enum-to-enum cast can never throw.
        /// </summary>
        public bool IsReferenceDowncast { get; }
    }

    /// <summary>Builds the downcast expression for handing <paramref name="expr"/> (of the original
    /// Majorsilence.Forms type) back out as <paramref name="t"/>'s compat type. Only call when
    /// <see cref="TypeTranslation.NeedsCastToCompat"/> and <see cref="TypeTranslation.IsDowncastSafe"/>
    /// are both true. See <see cref="TypeTranslation.IsReferenceDowncast"/> for why classes use `as`
    /// (a mismatch becomes null) while enums use a hard cast (a mismatch is impossible).</summary>
    private static string BuildDowncastExpression(TypeTranslation t, string expr)
    {
        if (!t.IsReferenceDowncast)
            return $"({t.CompatDisplay})({expr})";

        // `as` always produces a nullable result on its own; C# doesn't allow (and doesn't need)
        // writing the target type with a trailing `?` too (CS8651), so strip one if CompatDisplay has it.
        var targetType = t.CompatDisplay.EndsWith("?", StringComparison.Ordinal)
            ? t.CompatDisplay.Substring(0, t.CompatDisplay.Length - 1)
            : t.CompatDisplay;
        return $"({expr} as {targetType})";
    }

    private static readonly SymbolDisplayFormat DisplayFormatWithNullability = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Decides whether <paramref name="type"/> can appear in a forwarding wrapper's own signature,
    /// and how to convert a value between that signature and the real Majorsilence.Forms member it
    /// forwards to. <paramref name="mappings"/> is every mapping this generator knows about, not just
    /// the one currently being processed -- a System.Windows.Forms member can return a Drawing type
    /// and vice versa. Only arrays are rejected outright (the caller drops the whole member); every
    /// other type falls into one of four cases:
    ///
    /// - A mapped enum with a compat copy (pass 2) -- both directions need an explicit cast (unrelated
    ///   enum types with matching values by construction).
    /// - A mapped interface with a compat sub-interface (pass 3) -- accepting one as a parameter is an
    ///   implicit upcast (no cast needed); handing one back out as the compat type is rejected outright
    ///   (<see cref="TypeTranslation.IsDowncastSafe"/> is false), since nothing the framework itself
    ///   returns was ever constructed as that marker sub-interface -- the caller drops the member.
    /// - A mapped class with a compat subclass (pass 1) -- passing the compat subclass to the original
    ///   member is an implicit upcast (no cast needed); handing a value back out as the compat type is
    ///   a downcast (needs a cast, and is tolerated -- unlike the interface case -- because a consumer
    ///   of this package, by construction, constructs everything through the compat subclasses, never
    ///   the plain Majorsilence.Forms type directly).
    /// - Everything else -- BCL types, and any mapped type this generator has no compat counterpart
    ///   for (a plain class/interface with none, e.g. <c>FormCollection</c>; a sealed leaf type pass 1
    ///   never subclasses, e.g. <c>Majorsilence.Forms.Drawing.Graphics</c>/<c>Font</c>; a struct; a
    ///   delegate) -- needs no translation and is exposed as-is: it's the identical type on both
    ///   sides, so neither direction ever needs a cast, and it's always fully qualified, so it
    ///   resolves without a `using`. The one cost is that such a member's signature names a
    ///   Majorsilence.Forms type rather than a compat-namespaced one -- a wrinkle in the
    ///   namespace-transparency goal, not a reason to drop the member.
    /// </summary>
    private static bool TryTranslateType(ITypeSymbol type, IReadOnlyList<NamespaceMapping> mappings, out TypeTranslation result)
    {
        if (type is IArrayTypeSymbol)
        {
            result = default;
            return false;
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
        {
            var inner = nullableType.TypeArguments[0];
            if (inner is INamedTypeSymbol innerEnum && innerEnum.TypeKind == TypeKind.Enum
                && FindMapping(innerEnum, mappings) is { } innerMapping && innerMapping.Sets.Enums.Contains(innerEnum))
            {
                if (!TryTranslateType(inner, mappings, out var innerResult))
                {
                    result = default;
                    return false;
                }

                result = new TypeTranslation(
                    innerResult.CompatDisplay + "?",
                    innerResult.OriginalDisplay + "?",
                    innerResult.NeedsCastToOriginal,
                    innerResult.NeedsCastToCompat,
                    innerResult.IsDowncastSafe);
                return true;
            }

            // Any other Nullable<T> (int?, bool?, a BCL enum?, ...) needs no translation.
            var passthroughDisplay = type.ToDisplayString(DisplayFormatWithNullability);
            result = new TypeTranslation(passthroughDisplay, passthroughDisplay, false, false);
            return true;
        }

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType
            && FindMapping(enumType, mappings) is { } enumMapping && enumMapping.Sets.Enums.Contains(enumType))
        {
            result = new TypeTranslation(
                "global::" + enumMapping.TargetNamespace + "." + enumType.Name,
                "global::" + enumMapping.SourceNamespace + "." + enumType.Name,
                needsCastToOriginal: true,
                needsCastToCompat: true);
            return true;
        }

        if (type.TypeKind == TypeKind.Interface && type is INamedTypeSymbol interfaceType
            && FindMapping(interfaceType, mappings) is { } interfaceMapping && interfaceMapping.Sets.Interfaces.Contains(interfaceType))
        {
            var suffix = type.NullableAnnotation == NullableAnnotation.Annotated ? "?" : "";
            result = new TypeTranslation(
                "global::" + interfaceMapping.TargetNamespace + "." + interfaceType.Name + suffix,
                "global::" + interfaceMapping.SourceNamespace + "." + interfaceType.Name + suffix,
                needsCastToOriginal: false,
                needsCastToCompat: true,
                isDowncastSafe: false);
            return true;
        }

        if (type is INamedTypeSymbol classType && FindMapping(classType, mappings) is { } classMapping && classMapping.Sets.Subclasses.Contains(classType))
        {
            var suffix = type.NullableAnnotation == NullableAnnotation.Annotated ? "?" : "";
            result = new TypeTranslation(
                "global::" + classMapping.TargetNamespace + "." + classType.Name + suffix,
                "global::" + classMapping.SourceNamespace + "." + classType.Name + suffix,
                needsCastToOriginal: false,
                needsCastToCompat: true,
                isReferenceDowncast: true);
            return true;
        }

        // Anything else -- a BCL type, or a mapped-namespace type this generator has no compat
        // counterpart for (a plain class/interface with none, a struct, a sealed leaf type pass 1
        // can't subclass such as Majorsilence.Forms.Drawing.Graphics/Font, a delegate, ...) -- needs
        // no translation, because there's nothing TO translate it to: expose the original type
        // directly. It's always safe (it's literally the same type on both sides, so neither
        // direction ever needs a cast) and fully qualified, so it resolves with no `using` needed;
        // the only cost is that such a member's signature names a Majorsilence.Forms type rather than
        // a fully compat-namespaced one, which is one non-fatal wrinkle in the namespace-transparency
        // goal, not a reason to drop the member outright.
        var display = type.ToDisplayString(DisplayFormatWithNullability);
        result = new TypeTranslation(display, display, false, false);
        return true;
    }

    private static List<string> CollectStaticMemberBlocks(INamedTypeSymbol type, NamespaceMapping ownerMapping, IReadOnlyList<NamespaceMapping> mappings)
    {
        var blocks = new List<string>();

        foreach (var member in type.GetMembers())
        {
            string? block = member switch
            {
                IMethodSymbol method => TryFormatMethod(method, ownerMapping, mappings),
                IPropertySymbol property => TryFormatProperty(property, ownerMapping, mappings),
                IFieldSymbol field => TryFormatField(field, ownerMapping, mappings),
                IEventSymbol ev => TryFormatEvent(ev, ownerMapping, mappings),
                _ => null,
            };

            if (block is not null)
                blocks.Add(block);
        }

        return blocks;
    }

    private static bool TryBuildForwardedParameters(
        System.Collections.Immutable.ImmutableArray<IParameterSymbol> parameters,
        bool isExtensionMethod,
        IReadOnlyList<NamespaceMapping> mappings,
        out string parameterList,
        out string argumentList)
    {
        parameterList = "";
        argumentList = "";

        // Extension methods need `this` preserved on the first parameter to keep working as
        // extension methods; skipped for now rather than handled, since every extension-method
        // candidate seen so far extends IDataObject, and an interface's own extension methods gain
        // nothing from pass 3's marker sub-interface -- the receiver is still a parameter, but the
        // whole point of an extension method is being callable on an *existing* value, which here
        // would always be a plain IDataObject instance, never the marker subtype.
        if (isExtensionMethod)
            return false;

        var paramParts = new List<string>(parameters.Length);
        var argParts = new List<string>(parameters.Length);
        var defaults = new string?[parameters.Length];
        var allDefaultsRenderable = true;

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (p.RefKind != RefKind.None)
                return false;
            if (!TryTranslateType(p.Type, mappings, out var t))
                return false;

            if (p.HasExplicitDefaultValue)
            {
                var rendered = FormatDefaultValue(p, t.CompatDisplay);
                defaults[i] = rendered;
                if (rendered is null)
                    allDefaultsRenderable = false;
            }

            var paramsPrefix = p.IsParams ? "params " : "";
            paramParts.Add($"{paramsPrefix}{t.CompatDisplay} {p.Name}");
            argParts.Add(t.NeedsCastToOriginal ? $"({t.OriginalDisplay})({p.Name})" : p.Name);
        }

        if (allDefaultsRenderable)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (defaults[i] is { } d)
                    paramParts[i] += " = " + d;
            }
        }

        parameterList = string.Join(", ", paramParts);
        argumentList = string.Join(", ", argParts);
        return true;
    }

    private static string? TryFormatMethod(IMethodSymbol method, NamespaceMapping ownerMapping, IReadOnlyList<NamespaceMapping> mappings)
    {
        if (method.MethodKind != MethodKind.Ordinary)
            return null;
        if (method.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!method.IsStatic)
            return null;
        if (method.IsGenericMethod)
            return null;

        if (!TryBuildForwardedParameters(method.Parameters, method.IsExtensionMethod, mappings,
                out var parameterList, out var argumentList))
            return null;

        var owner = "global::" + ownerMapping.SourceNamespace + "." + method.ContainingType.Name;
        var call = $"{owner}.{method.Name} ({argumentList})";

        if (method.ReturnsVoid)
            return $"        public static void {method.Name} ({parameterList}) => {call};\n";

        if (!TryTranslateType(method.ReturnType, mappings, out var returnType))
            return null;
        if (returnType.NeedsCastToCompat && !returnType.IsDowncastSafe)
            return null; // e.g. a method returning a mapped interface -- see IsDowncastSafe

        var body = returnType.NeedsCastToCompat ? BuildDowncastExpression(returnType, call) : call;
        return $"        public static {returnType.CompatDisplay} {method.Name} ({parameterList}) => {body};\n";
    }

    private static string? TryFormatProperty(IPropertySymbol property, NamespaceMapping ownerMapping, IReadOnlyList<NamespaceMapping> mappings)
    {
        if (property.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!property.IsStatic)
            return null;
        if (property.IsIndexer || property.Parameters.Length > 0)
            return null;

        var canGet = property.GetMethod is { DeclaredAccessibility: Accessibility.Public };
        var canSet = property.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false };
        if (!canGet && !canSet)
            return null;

        if (!TryTranslateType(property.Type, mappings, out var t))
            return null;
        if (canGet && t.NeedsCastToCompat && !t.IsDowncastSafe)
            return null; // e.g. a property of a mapped interface type -- see IsDowncastSafe

        var owner = "global::" + ownerMapping.SourceNamespace + "." + property.ContainingType.Name + "." + property.Name;

        if (canGet && !canSet)
        {
            var getExpr = t.NeedsCastToCompat ? BuildDowncastExpression(t, owner) : owner;
            return $"        public static {t.CompatDisplay} {property.Name} => {getExpr};\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"        public static {t.CompatDisplay} {property.Name}");
        sb.AppendLine("        {");
        if (canGet)
        {
            var getExpr = t.NeedsCastToCompat ? BuildDowncastExpression(t, owner) : owner;
            sb.AppendLine($"            get => {getExpr};");
        }
        if (canSet)
        {
            var setExpr = t.NeedsCastToOriginal ? $"({t.OriginalDisplay})value" : "value";
            sb.AppendLine($"            set => {owner} = {setExpr};");
        }
        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static string? TryFormatField(IFieldSymbol field, NamespaceMapping ownerMapping, IReadOnlyList<NamespaceMapping> mappings)
    {
        if (field.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!field.IsStatic)
            return null;

        if (!TryTranslateType(field.Type, mappings, out var t))
            return null;
        if (t.NeedsCastToCompat && !t.IsDowncastSafe)
            return null; // e.g. a field of a mapped interface type -- see IsDowncastSafe

        var owner = "global::" + ownerMapping.SourceNamespace + "." + field.ContainingType.Name + "." + field.Name;
        var getExpr = t.NeedsCastToCompat ? BuildDowncastExpression(t, owner) : owner;

        if (field.IsConst || field.IsReadOnly)
            return $"        public static {t.CompatDisplay} {field.Name} => {getExpr};\n";

        var sb = new StringBuilder();
        sb.AppendLine($"        public static {t.CompatDisplay} {field.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            get => {getExpr};");
        var setExpr = t.NeedsCastToOriginal ? $"({t.OriginalDisplay})value" : "value";
        sb.AppendLine($"            set => {owner} = {setExpr};");
        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static string? TryFormatEvent(IEventSymbol ev, NamespaceMapping ownerMapping, IReadOnlyList<NamespaceMapping> mappings)
    {
        if (ev.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!ev.IsStatic)
            return null;

        if (!TryTranslateType(ev.Type, mappings, out var t))
            return null;
        // Only a passthrough delegate type (EventHandler, EventHandler<T> of a BCL T, ...) is
        // supported: an event whose delegate itself needed translation would need a wrapper
        // delegate instance per subscriber to bridge the two invoke signatures, which this
        // generator does not attempt.
        if (t.NeedsCastToOriginal || t.NeedsCastToCompat)
            return null;

        var owner = "global::" + ownerMapping.SourceNamespace + "." + ev.ContainingType.Name + "." + ev.Name;
        var sb = new StringBuilder();
        sb.AppendLine($"        public static event {t.CompatDisplay} {ev.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            add => {owner} += value;");
        sb.AppendLine($"            remove => {owner} -= value;");
        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static string GenerateStaticWrapperSource(INamedTypeSymbol type, NamespaceMapping mapping, List<string> memberBlocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- forwards every translatable");
        sb.AppendLine("// public static member to the real Majorsilence.Forms type of the same name. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {mapping.TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {type.Name}");
        sb.AppendLine("    {");
        foreach (var block in memberBlocks)
            sb.Append(block);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ── Event shadowing (Control's Paint/Mouse/Key family) ─────────────────────────────────

    /// <summary>An event declared on <c>Control</c> whose delegate's second parameter is a
    /// Majorsilence-specific <see cref="System.EventArgs"/> subclass -- the shape that needs a compat
    /// EventArgs wrapper and delegate copy, plus a per-subclass override/shadow pair, to become usable
    /// under <c>System.Windows.Forms</c> (see the class remarks on why plain <c>EventHandler</c>
    /// events like <c>Click</c> need none of this).</summary>
    private readonly struct EventFamily
    {
        public EventFamily(string eventName, INamedTypeSymbol delegateType, INamedTypeSymbol argsType, bool isGenericEventHandler)
        {
            EventName = eventName;
            DelegateType = delegateType;
            ArgsType = argsType;
            IsGenericEventHandler = isGenericEventHandler;
        }

        public string EventName { get; }
        public INamedTypeSymbol DelegateType { get; }
        public INamedTypeSymbol ArgsType { get; }

        /// <summary>
        /// True when <see cref="DelegateType"/> is a constructed <c>System.EventHandler&lt;T&gt;</c>
        /// (e.g. <c>EventHandler&lt;LongPressEventArgs&gt;</c>) rather than a named custom delegate
        /// (e.g. <c>PaintEventHandler</c>). Constructed generics share the short name "EventHandler"
        /// regardless of their type argument, so they can't be told apart by <c>DelegateType.Name</c>
        /// the way named delegates can -- and don't need a compat copy at all, since the compat event
        /// can just reuse the BCL <c>EventHandler&lt;T&gt;</c> instantiated with the compat args type.
        /// </summary>
        public bool IsGenericEventHandler { get; }
    }

    private static List<EventFamily> DiscoverEventFamilies(
        INamedTypeSymbol controlType, INamedTypeSymbol eventArgsType, INamedTypeSymbol genericEventHandlerType, NamespaceMapping winFormsMapping)
    {
        var result = new List<EventFamily>();
        foreach (var ev in controlType.GetMembers().OfType<IEventSymbol>())
        {
            if (ev.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (ev.IsStatic)
                continue;
            if (ev.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType)
                continue;

            var invoke = delegateType.DelegateInvokeMethod;
            if (invoke is null || invoke.Parameters.Length != 2)
                continue;
            if (invoke.Parameters[1].Type is not INamedTypeSymbol argsType)
                continue;
            if (argsType.TypeKind != TypeKind.Class)
                continue;
            if (argsType.ContainingNamespace?.ToDisplayString() != winFormsMapping.SourceNamespace)
                continue;
            if (!DerivesFrom(argsType, eventArgsType))
                continue;

            var isGenericEventHandler = delegateType.IsGenericType
                && SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, genericEventHandlerType);
            result.Add(new EventFamily(ev.Name, delegateType, argsType, isGenericEventHandler));
        }
        return result;
    }

    /// <summary>
    /// Finds the closest (most-derived) accessible, overridable method named <paramref name="name"/>
    /// with exactly one parameter of type <paramref name="paramType"/>, walking from
    /// <paramref name="type"/> up through its Majorsilence.Forms base chain. Returns null if none is
    /// reachable, or if the closest one found is sealed -- either way, the caller can't safely
    /// generate an override, so it skips this family for this type entirely.
    /// </summary>
    private static IMethodSymbol? FindReachableOverridableMethod(INamedTypeSymbol type, string name, INamedTypeSymbol paramType)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var m in t.GetMembers(name).OfType<IMethodSymbol>())
            {
                if (m.Parameters.Length != 1)
                    continue;
                if (!SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, paramType))
                    continue;
                if (m.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                    return null;
                if (!(m.IsVirtual || m.IsOverride) || m.IsSealed)
                    return null;
                return m;
            }
        }
        return null;
    }

    /// <summary>Same idea as <see cref="FindReachableOverridableMethod"/>, for the event half of the
    /// pair: the closest event named <paramref name="name"/> must be public, non-static, and typed to
    /// exactly <paramref name="delegateType"/> (not some other type that happens to share the name).</summary>
    private static IEventSymbol? FindReachableEvent(INamedTypeSymbol type, string name, INamedTypeSymbol delegateType)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var e in t.GetMembers(name).OfType<IEventSymbol>())
            {
                if (e.IsStatic)
                    return null;
                if (!SymbolEqualityComparer.Default.Equals(e.Type, delegateType))
                    return null;
                if (e.DeclaredAccessibility != Accessibility.Public)
                    return null;
                return e;
            }
        }
        return null;
    }

    private static string GenerateEventArgsWrapperSource(
        INamedTypeSymbol argsType, INamedTypeSymbol eventArgsType, NamespaceMapping winFormsMapping, IReadOnlyList<NamespaceMapping> mappings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- wraps the real Majorsilence.Forms");
        sb.AppendLine("// event-args instance and forwards every translatable public instance property to it, so");
        sb.AppendLine("// handlers under `System.Windows.Forms` can read (and, where settable, write) the same");
        sb.AppendLine("// data. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {winFormsMapping.TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {argsType.Name} : global::System.EventArgs");
        sb.AppendLine("    {");
        sb.AppendLine($"        internal {argsType.Name} (global::{winFormsMapping.SourceNamespace}.{argsType.Name} inner) => Inner = inner;");
        sb.AppendLine($"        internal global::{winFormsMapping.SourceNamespace}.{argsType.Name} Inner {{ get; }}");
        sb.AppendLine();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var t = argsType; t is not null && !SymbolEqualityComparer.Default.Equals(t, eventArgsType); t = t.BaseType)
        {
            foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (!seen.Add(prop.Name))
                    continue; // a more-derived declaration of the same name already won
                if (prop.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (prop.IsStatic || prop.IsIndexer)
                    continue;

                var canGet = prop.GetMethod is { DeclaredAccessibility: Accessibility.Public };
                var canSet = prop.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false };
                if (!canGet && !canSet)
                    continue;

                if (!TryTranslateType(prop.Type, mappings, out var pt))
                    continue;
                if (canGet && pt.NeedsCastToCompat && !pt.IsDowncastSafe)
                    continue; // e.g. DragEventArgs.Data (IDataObject) -- see TypeTranslation.IsDowncastSafe

                var owner = "Inner." + prop.Name;
                if (canGet && !canSet)
                {
                    var getExpr = pt.NeedsCastToCompat ? BuildDowncastExpression(pt, owner) : owner;
                    sb.AppendLine($"        public {pt.CompatDisplay} {prop.Name} => {getExpr};");
                    continue;
                }

                sb.AppendLine($"        public {pt.CompatDisplay} {prop.Name}");
                sb.AppendLine("        {");
                if (canGet)
                {
                    var getExpr = pt.NeedsCastToCompat ? BuildDowncastExpression(pt, owner) : owner;
                    sb.AppendLine($"            get => {getExpr};");
                }
                if (canSet)
                {
                    var setExpr = pt.NeedsCastToOriginal ? $"({pt.OriginalDisplay})value" : "value";
                    sb.AppendLine($"            set => {owner} = {setExpr};");
                }
                sb.AppendLine("        }");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateDelegateSource(INamedTypeSymbol delegateType, string compatArgsName, NamespaceMapping winFormsMapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- a delegate copy typed to the");
        sb.AppendLine("// compat EventArgs wrapper above, since the original delegate is typed to the real");
        sb.AppendLine("// Majorsilence.Forms one. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {winFormsMapping.TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public delegate void {delegateType.Name} (object? sender, {compatArgsName} e);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// The per-subclass shadow for one event family: override the original virtual On* method,
    /// translate, dispatch to a new compat-typed virtual hook of the same name (so a further-derived
    /// class can override *that* instead), and shadow the public event with one of the compat delegate
    /// type. The new virtual hook -- not the override -- raises the shadowed event, matching real
    /// WinForms: a subclass that overrides the hook without calling base suppresses the event, exactly
    /// as overriding upstream's OnPaint without calling base.OnPaint does.
    /// </summary>
    private static string GenerateEventShadowBlock(EventFamily family, NamespaceMapping winFormsMapping)
    {
        var argsCompat = "global::" + winFormsMapping.TargetNamespace + "." + family.ArgsType.Name;
        var argsOriginal = "global::" + winFormsMapping.SourceNamespace + "." + family.ArgsType.Name;
        var delegateCompat = family.IsGenericEventHandler
            ? $"global::System.EventHandler<{argsCompat}>"
            : "global::" + winFormsMapping.TargetNamespace + "." + family.DelegateType.Name;
        var onName = "On" + family.EventName;

        var sb = new StringBuilder();
        sb.AppendLine($"        protected override void {onName} ({argsOriginal} e)");
        sb.AppendLine("        {");
        sb.AppendLine($"            {onName} (new {argsCompat} (e));");
        sb.AppendLine($"            base.{onName} (e);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        protected virtual void {onName} ({argsCompat} e) => {family.EventName}?.Invoke (this, e);");
        sb.AppendLine();
        sb.AppendLine($"        public new event {delegateCompat}? {family.EventName};");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GenerateEventShadowFileSource(INamedTypeSymbol type, NamespaceMapping winFormsMapping, List<string> blocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- the second part of this type's");
        sb.AppendLine("// partial subclass (see the other generated file for its constructors): shadows Control's");
        sb.AppendLine("// Paint/Mouse/Key event family with compat EventArgs types. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {winFormsMapping.TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {type.Name}");
        sb.AppendLine("    {");
        foreach (var block in blocks)
            sb.Append(block);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
