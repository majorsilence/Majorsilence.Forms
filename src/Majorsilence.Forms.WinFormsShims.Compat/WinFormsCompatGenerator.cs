using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Majorsilence.Forms.WinFormsShims.Compat;

/// <summary>
/// PoC source generator that emits a <c>System.Windows.Forms</c>-namespace compatibility surface
/// backed by <c>Majorsilence.Forms</c>, in four independent passes over that namespace's public
/// members:
///
/// 1. Every public, non-sealed, non-generic class that does NOT derive from <see cref="System.EventArgs"/>
///    and exposes at least one accessible constructor gets a same-named subclass with forwarding
///    constructors -- <c>Button</c>, <c>Form</c> and the rest of the <c>Component</c> hierarchy, but
///    also plain classes with no <c>Component</c> ancestor at all, like <c>ApplicationContext</c>.
///    EventArgs types are excluded on purpose: giving e.g. <c>PaintEventArgs</c> a compat subclass
///    would look like it enables `Control.Paint += someHandler;`, but a handler parameter typed to a
///    *subclass* can never satisfy a delegate whose declared parameter is the *base* type (C#
///    contravariance runs the other way) -- see the class remarks on event shadowing below.
/// 2. Every public, non-nested enum gets a same-named, same-valued copy -- needed because #1/#3/#4's
///    forwarders surface Majorsilence-specific enums such as <c>DialogResult</c> or
///    <c>MessageBoxButtons</c> in their own public signatures, and code that only imports
///    <c>System.Windows.Forms</c> has no other way to name them.
/// 3. Every public, non-generic interface gets a same-named, empty sub-interface (<c>IMessageFilter</c>,
///    <c>IWin32Window</c>, <c>IDataObject</c>, ...), so it can be named and implemented under
///    <c>System.Windows.Forms</c> and passed as a parameter. This only works as an INPUT: a
///    framework-returned instance implements only the original interface, never this marker, so
///    <see cref="TryTranslateType"/> refuses to hand one back out as the compat type (see
///    <see cref="TypeTranslation.IsDowncastSafe"/>).
/// 4. Every public static, non-generic class -- <c>Application</c>, <c>MessageBox</c>,
///    <c>Clipboard</c>, <c>SystemInformation</c>, ... -- gets a same-named static class that
///    forwards each member whose signature is fully translatable (see <see cref="TryTranslateType"/>)
///    into the real Majorsilence.Forms one. A member with any untranslatable type in its signature
///    is silently dropped rather than emitted broken; see the README for what that excludes today
///    (a plain Majorsilence class/interface with no compat counterpart above -- e.g. <c>FormCollection</c>,
///    which has no accessible constructor for #1 to subclass -- arrays, ref/out parameters, generic
///    methods, and extension methods).
///
/// This intentionally does NOT attempt to shadow events typed to Majorsilence-specific EventArgs
/// (Paint, MouseDown, KeyDown, ...) -- see the feasibility plan this project exists to validate.
/// Events typed to the real BCL <see cref="System.EventHandler"/> (Click, TextChanged, Resize, ...)
/// need no special handling: they already resolve identically regardless of namespace.
/// </summary>
[Generator]
public sealed class WinFormsCompatGenerator : IIncrementalGenerator
{
    private const string CoreAssemblyName = "Majorsilence.Forms";
    private const string CoreNamespace = "Majorsilence.Forms";
    private const string TargetNamespace = "System.Windows.Forms";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, static (spc, compilation) => Execute(compilation, spc));
    }

    private static void Execute(Compilation compilation, SourceProductionContext context)
    {
        var coreAssembly = compilation.SourceModule.ReferencedAssemblySymbols
            .FirstOrDefault(a => a.Name == CoreAssemblyName);
        if (coreAssembly is null)
            return;

        var eventArgsType = compilation.GetTypeByMetadataName("System.EventArgs");
        if (eventArgsType is null)
            return;

        var coreNamespace = FindNamespace(coreAssembly.GlobalNamespace, CoreNamespace);
        if (coreNamespace is null)
            return;

        // Namespace members can't collide within Majorsilence.Forms itself (a class and an enum can't
        // share a name in the same namespace), but shared across all four passes anyway since this is
        // emitted output covering the whole compat surface.
        var emittedNames = new HashSet<string>(StringComparer.Ordinal);
        var sets = new CompatSets();

        // Pass 1: eligible classes -> forwarding-constructor subclasses.
        foreach (var type in coreNamespace.GetTypeMembers())
        {
            if (!IsEligibleClass(type, eventArgsType))
                continue;

            var ctors = GetAccessibleConstructors(compilation, type);
            if (ctors.Count == 0)
                continue;

            if (!emittedNames.Add(type.Name))
                continue;

            sets.Subclasses.Add(type);
            var source = GenerateSource(type, ctors);
            context.AddSource($"{type.Name}.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        // Pass 2: public enums -> identical copies, so pass 1's own generated signatures and pass 4
        // can reference an enum type that resolves under System.Windows.Forms.
        foreach (var type in coreNamespace.GetTypeMembers())
        {
            if (type.TypeKind != TypeKind.Enum)
                continue;
            if (type.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (type.ContainingType is not null)
                continue;

            if (!emittedNames.Add(type.Name))
                continue;

            sets.Enums.Add(type);
            var source = GenerateEnumSource(type);
            context.AddSource($"{type.Name}.Enum.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        // Pass 3: public interfaces -> empty sub-interfaces, usable as parameter types (see the
        // class remarks and TypeTranslation.IsDowncastSafe for why not as return types).
        foreach (var type in coreNamespace.GetTypeMembers())
        {
            if (type.TypeKind != TypeKind.Interface)
                continue;
            if (type.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (type.IsGenericType)
                continue;
            if (type.ContainingType is not null)
                continue;

            if (!emittedNames.Add(type.Name))
                continue;

            sets.Interfaces.Add(type);
            var source = GenerateInterfaceSource(type);
            context.AddSource($"{type.Name}.Interface.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        // Pass 4: public static utility classes -> forwarding static classes.
        foreach (var type in coreNamespace.GetTypeMembers())
        {
            if (!IsEligibleStaticClass(type))
                continue;

            var memberBlocks = CollectStaticMemberBlocks(type, sets);
            if (memberBlocks.Count == 0)
                continue;

            if (!emittedNames.Add(type.Name))
                continue;

            var source = GenerateStaticWrapperSource(type, memberBlocks);
            context.AddSource($"{type.Name}.Static.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>The three kinds of compat counterpart a Majorsilence.Forms type can have, populated
    /// by passes 1-3 and consulted by <see cref="TryTranslateType"/> during pass 4.</summary>
    private sealed class CompatSets
    {
        public HashSet<INamedTypeSymbol> Subclasses { get; } = new(SymbolEqualityComparer.Default);
        public HashSet<INamedTypeSymbol> Enums { get; } = new(SymbolEqualityComparer.Default);
        public HashSet<INamedTypeSymbol> Interfaces { get; } = new(SymbolEqualityComparer.Default);
    }

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

    private static string GenerateSource(INamedTypeSymbol type, List<IMethodSymbol> ctors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- a thin subclass so unmodified");
        sb.AppendLine("// `System.Windows.Forms` source keeps compiling against Majorsilence.Forms. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {TargetNamespace}");
        sb.AppendLine("{");

        var baseRef = "global::" + CoreNamespace + "." + type.Name;
        var classKeyword = type.IsAbstract ? "abstract class" : "class";
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

    private static string GenerateEnumSource(INamedTypeSymbol enumType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- an identical copy of the");
        sb.AppendLine("// Majorsilence.Forms enum of the same name, so code compiling only against");
        sb.AppendLine("// `System.Windows.Forms` can still name it. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {TargetNamespace}");
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

    private static string GenerateInterfaceSource(INamedTypeSymbol interfaceType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- an empty interface that");
        sb.AppendLine("// extends the Majorsilence.Forms interface of the same name, so it can be named,");
        sb.AppendLine("// implemented, and passed as a parameter under `System.Windows.Forms`. Handing a");
        sb.AppendLine("// value back OUT as this type is not supported: a framework-returned instance");
        sb.AppendLine("// implements only the original interface, never this one. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public interface {interfaceType.Name} : global::{CoreNamespace}.{interfaceType.Name}");
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

    private static bool IsMajorsilenceForms(ITypeSymbol type) =>
        type.ContainingNamespace is { IsGlobalNamespace: false } ns && ns.ToDisplayString() == CoreNamespace;

    /// <summary>
    /// A member signature's type, restated as the two forms a forwarding wrapper needs: the type its
    /// own (compat-namespace) signature exposes, and the type the real Majorsilence.Forms member
    /// actually uses -- plus whether converting between them, in each direction, needs an explicit
    /// cast.
    /// </summary>
    private readonly struct TypeTranslation
    {
        public TypeTranslation(string compatDisplay, string originalDisplay, bool needsCastToOriginal, bool needsCastToCompat, bool isDowncastSafe = true)
        {
            CompatDisplay = compatDisplay;
            OriginalDisplay = originalDisplay;
            NeedsCastToOriginal = needsCastToOriginal;
            NeedsCastToCompat = needsCastToCompat;
            IsDowncastSafe = isDowncastSafe;
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
    }

    private static readonly SymbolDisplayFormat DisplayFormatWithNullability = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// Decides whether <paramref name="type"/> can appear in a forwarding wrapper's own signature,
    /// and how to convert a value between that signature and the real Majorsilence.Forms member it
    /// forwards to. Four cases translate; everything else is rejected so the caller can drop the
    /// whole member rather than emit something that doesn't compile or silently does the wrong thing:
    ///
    /// - A Majorsilence.Forms enum with a compat copy (pass 2) -- both directions need an explicit
    ///   cast (unrelated enum types with matching values by construction).
    /// - A Majorsilence.Forms interface with a compat sub-interface (pass 3) -- accepting one as a
    ///   parameter is an implicit upcast (no cast needed); handing one back out as the compat type is
    ///   rejected outright (<see cref="TypeTranslation.IsDowncastSafe"/> is false), since nothing the
    ///   framework itself returns was ever constructed as that marker sub-interface.
    /// - A Majorsilence.Forms class with a compat subclass (pass 1) -- passing the compat subclass to
    ///   the original member is an implicit upcast (no cast needed); handing a value back out as the
    ///   compat type is a downcast (needs a cast, and is tolerated -- unlike the interface case --
    ///   because a consumer of this package, by construction, constructs everything through the
    ///   compat subclasses, never the plain Majorsilence.Forms type directly).
    /// - Anything outside the Majorsilence.Forms namespace (BCL types, System.Drawing, ...) needs no
    ///   translation: it resolves identically regardless of which namespace the wrapper lives in.
    ///
    /// Arrays, and any other Majorsilence.Forms type without a compat counterpart above (a plain class
    /// or interface with no compat counterpart, e.g. <c>FormCollection</c>), are rejected.
    /// </summary>
    private static bool TryTranslateType(ITypeSymbol type, CompatSets sets, out TypeTranslation result)
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
                && IsMajorsilenceForms(innerEnum) && sets.Enums.Contains(innerEnum))
            {
                if (!TryTranslateType(inner, sets, out var innerResult))
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
            && IsMajorsilenceForms(enumType) && sets.Enums.Contains(enumType))
        {
            result = new TypeTranslation(
                "global::" + TargetNamespace + "." + enumType.Name,
                "global::" + CoreNamespace + "." + enumType.Name,
                needsCastToOriginal: true,
                needsCastToCompat: true);
            return true;
        }

        if (type.TypeKind == TypeKind.Interface && type is INamedTypeSymbol interfaceType
            && IsMajorsilenceForms(interfaceType) && sets.Interfaces.Contains(interfaceType))
        {
            var suffix = type.NullableAnnotation == NullableAnnotation.Annotated ? "?" : "";
            result = new TypeTranslation(
                "global::" + TargetNamespace + "." + interfaceType.Name + suffix,
                "global::" + CoreNamespace + "." + interfaceType.Name + suffix,
                needsCastToOriginal: false,
                needsCastToCompat: true,
                isDowncastSafe: false);
            return true;
        }

        if (type is INamedTypeSymbol classType && IsMajorsilenceForms(classType) && sets.Subclasses.Contains(classType))
        {
            var suffix = type.NullableAnnotation == NullableAnnotation.Annotated ? "?" : "";
            result = new TypeTranslation(
                "global::" + TargetNamespace + "." + classType.Name + suffix,
                "global::" + CoreNamespace + "." + classType.Name + suffix,
                needsCastToOriginal: false,
                needsCastToCompat: true);
            return true;
        }

        if (IsMajorsilenceForms(type))
        {
            // Some other Majorsilence.Forms type this generator has no compat counterpart for -- a
            // plain class/interface with no compat counterpart, a struct, or a delegate
            // (FormCollection has no accessible constructor for pass 1 to subclass, for instance).
            // Reject rather than guess; the caller drops the member.
            result = default;
            return false;
        }

        var display = type.ToDisplayString(DisplayFormatWithNullability);
        result = new TypeTranslation(display, display, false, false);
        return true;
    }

    private static List<string> CollectStaticMemberBlocks(INamedTypeSymbol type, CompatSets sets)
    {
        var blocks = new List<string>();

        foreach (var member in type.GetMembers())
        {
            string? block = member switch
            {
                IMethodSymbol method => TryFormatMethod(method, sets),
                IPropertySymbol property => TryFormatProperty(property, sets),
                IFieldSymbol field => TryFormatField(field, sets),
                IEventSymbol ev => TryFormatEvent(ev, sets),
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
        CompatSets sets,
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
            if (!TryTranslateType(p.Type, sets, out var t))
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

    private static string? TryFormatMethod(IMethodSymbol method, CompatSets sets)
    {
        if (method.MethodKind != MethodKind.Ordinary)
            return null;
        if (method.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!method.IsStatic)
            return null;
        if (method.IsGenericMethod)
            return null;

        if (!TryBuildForwardedParameters(method.Parameters, method.IsExtensionMethod, sets,
                out var parameterList, out var argumentList))
            return null;

        var owner = "global::" + CoreNamespace + "." + method.ContainingType.Name;
        var call = $"{owner}.{method.Name} ({argumentList})";

        if (method.ReturnsVoid)
            return $"        public static void {method.Name} ({parameterList}) => {call};\n";

        if (!TryTranslateType(method.ReturnType, sets, out var returnType))
            return null;
        if (returnType.NeedsCastToCompat && !returnType.IsDowncastSafe)
            return null; // e.g. a method returning a Majorsilence.Forms interface -- see IsDowncastSafe

        var body = returnType.NeedsCastToCompat ? $"({returnType.CompatDisplay})({call})" : call;
        return $"        public static {returnType.CompatDisplay} {method.Name} ({parameterList}) => {body};\n";
    }

    private static string? TryFormatProperty(IPropertySymbol property, CompatSets sets)
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

        if (!TryTranslateType(property.Type, sets, out var t))
            return null;
        if (canGet && t.NeedsCastToCompat && !t.IsDowncastSafe)
            return null; // e.g. a property of a Majorsilence.Forms interface type -- see IsDowncastSafe

        var owner = "global::" + CoreNamespace + "." + property.ContainingType.Name + "." + property.Name;

        if (canGet && !canSet)
        {
            var getExpr = t.NeedsCastToCompat ? $"({t.CompatDisplay})({owner})" : owner;
            return $"        public static {t.CompatDisplay} {property.Name} => {getExpr};\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"        public static {t.CompatDisplay} {property.Name}");
        sb.AppendLine("        {");
        if (canGet)
        {
            var getExpr = t.NeedsCastToCompat ? $"({t.CompatDisplay})({owner})" : owner;
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

    private static string? TryFormatField(IFieldSymbol field, CompatSets sets)
    {
        if (field.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!field.IsStatic)
            return null;

        if (!TryTranslateType(field.Type, sets, out var t))
            return null;
        if (t.NeedsCastToCompat && !t.IsDowncastSafe)
            return null; // e.g. a field of a Majorsilence.Forms interface type -- see IsDowncastSafe

        var owner = "global::" + CoreNamespace + "." + field.ContainingType.Name + "." + field.Name;
        var getExpr = t.NeedsCastToCompat ? $"({t.CompatDisplay})({owner})" : owner;

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

    private static string? TryFormatEvent(IEventSymbol ev, CompatSets sets)
    {
        if (ev.DeclaredAccessibility != Accessibility.Public)
            return null;
        if (!ev.IsStatic)
            return null;

        if (!TryTranslateType(ev.Type, sets, out var t))
            return null;
        // Only a passthrough delegate type (EventHandler, EventHandler<T> of a BCL T, ...) is
        // supported: an event whose delegate itself needed translation would need a wrapper
        // delegate instance per subscriber to bridge the two invoke signatures, which this
        // generator does not attempt.
        if (t.NeedsCastToOriginal || t.NeedsCastToCompat)
            return null;

        var owner = "global::" + CoreNamespace + "." + ev.ContainingType.Name + "." + ev.Name;
        var sb = new StringBuilder();
        sb.AppendLine($"        public static event {t.CompatDisplay} {ev.Name}");
        sb.AppendLine("        {");
        sb.AppendLine($"            add => {owner} += value;");
        sb.AppendLine($"            remove => {owner} -= value;");
        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static string GenerateStaticWrapperSource(INamedTypeSymbol type, List<string> memberBlocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated by Majorsilence.Forms.WinFormsShims.Compat -- forwards every translatable");
        sb.AppendLine("// public static member to the real Majorsilence.Forms type of the same name. Do not edit.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine();
        sb.AppendLine($"namespace {TargetNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {type.Name}");
        sb.AppendLine("    {");
        foreach (var block in memberBlocks)
            sb.Append(block);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
