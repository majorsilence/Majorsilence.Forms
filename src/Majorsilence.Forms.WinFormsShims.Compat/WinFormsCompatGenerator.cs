using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Majorsilence.Forms.WinFormsShims.Compat;

/// <summary>
/// PoC source generator: for every public, non-sealed, non-generic <c>Majorsilence.Forms</c> type
/// that derives (directly or transitively) from <see cref="System.ComponentModel.Component"/> and
/// exposes at least one accessible constructor, emits a same-named subclass under
/// <c>namespace System.Windows.Forms</c> with forwarding constructors.
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

        var componentType = compilation.GetTypeByMetadataName("System.ComponentModel.Component");
        if (componentType is null)
            return;

        var coreNamespace = FindNamespace(coreAssembly.GlobalNamespace, CoreNamespace);
        if (coreNamespace is null)
            return;

        var emittedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in coreNamespace.GetTypeMembers())
        {
            if (!IsEligible(type, componentType))
                continue;

            var ctors = GetAccessibleConstructors(compilation, type);
            if (ctors.Count == 0)
                continue;

            // Namespace members can't collide within Majorsilence.Forms itself, but guard anyway
            // since this is emitted output shared across the whole compat surface.
            if (!emittedNames.Add(type.Name))
                continue;

            var source = GenerateSource(type, ctors);
            context.AddSource($"{type.Name}.g.cs", SourceText.From(source, Encoding.UTF8));
        }
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

    private static bool IsEligible(INamedTypeSymbol type, INamedTypeSymbol componentType)
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
        if (SymbolEqualityComparer.Default.Equals(type, componentType))
            return false; // Component itself is already the right namespace/type

        for (var b = type.BaseType; b is not null; b = b.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(b, componentType))
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

    private static string? FormatDefaultValue(IParameterSymbol p)
    {
        var value = p.ExplicitDefaultValue;
        var typeStr = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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
}
