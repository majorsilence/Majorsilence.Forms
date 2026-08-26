using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Reads the built assembly's metadata and IL to find members that compile, run, and silently do
/// nothing. Shared by the three baseline gates: <see cref="InertEventBaselineTests"/>,
/// <see cref="UnraisedEventBaselineTests"/> and <see cref="StoredOnlyPropertyBaselineTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the same technique <see cref="NoOpStubBaselineTests"/> uses, generalised. That test's own
/// remarks explain why it reads metadata off the file rather than reflecting over loaded types --
/// <c>MethodBody</c> is only reachable once a type is loaded, and loading every type would drag in
/// SkiaSharp natives for no benefit -- and they also name the category this file exists for:
/// <em>"an inert event is a separate (and much larger) category than a method that quietly discards
/// its arguments."</em>
/// </para>
/// <para>
/// Two of the three gates need to answer "is this field ever touched outside its own accessors?",
/// which means walking IL instruction by instruction rather than pattern-matching bytes: an operand
/// byte can look exactly like an opcode, so a scan that does not track instruction boundaries reports
/// phantom field accesses. <see cref="OperandSize"/> is the ECMA-335 operand table that makes the walk
/// exact.
/// </para>
/// </remarks>
internal static class StubSurfaceScanner
{
    /// <summary>An event whose add/remove accessors both have an empty body -- `add { } remove { }`.</summary>
    internal const string InertEventBaselineFileName = "InertEventBaseline.txt";

    /// <summary>A field-backed event that no code ever reads in order to invoke.</summary>
    internal const string UnraisedEventBaselineFileName = "UnraisedEventBaseline.txt";

    /// <summary>An auto-property whose backing field is read only by its own getter.</summary>
    internal const string StoredOnlyPropertyBaselineFileName = "StoredOnlyPropertyBaseline.txt";

    // ---------------------------------------------------------------------------------------------
    // Scans
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Events declared `add { } remove { }`: both accessors are public and have a body that is
    /// nothing but a return, so subscribing compiles, hands over a delegate, and drops it.
    /// </summary>
    internal static List<string> ScanInertEvents (string assemblyPath)
    {
        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();
        var found = new List<string> ();

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);
            if (!IsPubliclyVisible (md, type))
                continue;

            foreach (var eventHandle in type.GetEvents ()) {
                var evt = md.GetEventDefinition (eventHandle);
                var accessors = evt.GetAccessors ();

                if (accessors.Adder.IsNil || accessors.Remover.IsNil)
                    continue;

                var adder = md.GetMethodDefinition (accessors.Adder);
                var remover = md.GetMethodDefinition (accessors.Remover);

                if (!IsPublicOrProtected (adder.Attributes) || !IsPublicOrProtected (remover.Attributes))
                    continue;
                if (!IsEmptyBody (pe, adder) || !IsEmptyBody (pe, remover))
                    continue;

                found.Add ($"{FullTypeName (md, type)}.{md.GetString (evt.Name)}");
            }
        }

        return Normalise (found);
    }

    /// <summary>
    /// Field-backed events that are declared and never raised. The compiler-generated add/remove pair
    /// reads the backing field (the Interlocked.CompareExchange loop), so "read by anything other than
    /// its own accessors" is what distinguishes an event somebody invokes from one that only exists.
    /// This is the `#pragma warning disable CS0067` set, found without relying on the pragma.
    /// </summary>
    internal static List<string> ScanUnraisedEvents (string assemblyPath)
    {
        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();
        var access = BuildFieldAccessMap (pe, md);
        var found = new List<string> ();

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);
            if (!IsPubliclyVisible (md, type))
                continue;

            var fieldsByName = type.GetFields ()
                .ToDictionary (h => md.GetString (md.GetFieldDefinition (h).Name), h => h);

            foreach (var eventHandle in type.GetEvents ()) {
                var evt = md.GetEventDefinition (eventHandle);
                var accessors = evt.GetAccessors ();
                if (accessors.Adder.IsNil || accessors.Remover.IsNil)
                    continue;

                var adder = md.GetMethodDefinition (accessors.Adder);
                if (!IsPublicOrProtected (adder.Attributes))
                    continue;

                var name = md.GetString (evt.Name);

                // A field-like event's backing field carries the event's own name. An event written
                // with explicit accessors has no such field and is not this category -- it is either
                // real (forwards somewhere) or inert (caught by ScanInertEvents).
                if (!fieldsByName.TryGetValue (name, out var fieldHandle))
                    continue;

                var readers = access.Readers.TryGetValue (fieldHandle, out var r) ? r : [];
                var raisedBySomething = readers.Any (m =>
                    m != accessors.Adder && m != accessors.Remover);

                if (!raisedBySomething)
                    found.Add ($"{FullTypeName (md, type)}.{name}");
            }
        }

        return Normalise (found);
    }

    /// <summary>
    /// Public settable auto-properties that nothing in this assembly ever reads the value of -- set by
    /// application code, readable back, and consumed by no code here.
    /// </summary>
    /// <remarks>
    /// "Consumed" has to mean two things, and getting this wrong is easy. A property can be read
    /// through its <em>getter</em> (<c>form.AcceptButton != null</c> compiles to a <c>callvirt</c>,
    /// not a field load) or, from inside its own type, straight off the backing field. Checking only
    /// the field reports every properly-encapsulated property as inert -- <c>Form.AcceptButton</c> was
    /// the case that caught this -- so both are checked here.
    /// </remarks>
    internal static List<string> ScanStoredOnlyProperties (string assemblyPath)
        => ScanStoredOnlyProperties (assemblyPath, out _);

    /// <param name="examined">
    /// How many public settable auto-properties were considered, so a baseline can record the
    /// denominator alongside the count.
    /// </param>
    internal static List<string> ScanStoredOnlyProperties (string assemblyPath, out int examined)
    {
        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();
        var access = BuildFieldAccessMap (pe, md);
        var found = new List<string> ();
        examined = 0;

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);
            if (!IsPubliclyVisible (md, type))
                continue;

            var fieldsByName = type.GetFields ()
                .ToDictionary (h => md.GetString (md.GetFieldDefinition (h).Name), h => h);

            foreach (var propertyHandle in type.GetProperties ()) {
                var property = md.GetPropertyDefinition (propertyHandle);
                var accessors = property.GetAccessors ();

                if (accessors.Getter.IsNil || accessors.Setter.IsNil)
                    continue;

                var getter = md.GetMethodDefinition (accessors.Getter);
                if (!IsPublicOrProtected (getter.Attributes))
                    continue;

                var name = md.GetString (property.Name);

                // Only auto-properties: a property with hand-written accessors may compute, forward or
                // validate, and "its backing field" is not a well-defined question.
                if (!fieldsByName.TryGetValue ($"<{name}>k__BackingField", out var fieldHandle))
                    continue;

                examined++;

                var readers = access.Readers.TryGetValue (fieldHandle, out var r) ? r : [];
                if (readers.Any (m => m != accessors.Getter))
                    continue;

                if (IsCalled (access, md, accessors.Getter, getter))
                    continue;

                found.Add ($"{FullTypeName (md, type)}.{name}");
            }
        }

        return Normalise (found);
    }

    /// <summary>
    /// Whether anything in the assembly calls this accessor. A virtual accessor is also treated as
    /// called when <em>any</em> accessor of the same name is called anywhere, because a call through a
    /// base-typed or interface-typed reference carries the base's token, not the override's -- and
    /// erring toward "consumed" keeps a working property out of the baseline.
    /// </summary>
    private static bool IsCalled (FieldAccessMap access, MetadataReader md,
        MethodDefinitionHandle handle, MethodDefinition method)
    {
        if (access.CalledMethods.Contains (handle))
            return true;

        var isVirtual = (method.Attributes & MethodAttributes.Virtual) != 0;
        return isVirtual && access.CalledMethodNames.Contains (md.GetString (method.Name));
    }

    // ---------------------------------------------------------------------------------------------
    // IL walking
    // ---------------------------------------------------------------------------------------------

    private sealed class FieldAccessMap
    {
        /// <summary>Per field, the methods that load it.</summary>
        public Dictionary<FieldDefinitionHandle, HashSet<MethodDefinitionHandle>> Readers { get; } = [];

        /// <summary>Every method this assembly calls, where the call names a definition here.</summary>
        public HashSet<MethodDefinitionHandle> CalledMethods { get; } = [];

        /// <summary>
        /// The simple name of every method called, including through <c>MemberRef</c>. Used only to
        /// resolve virtual dispatch, where the call site carries the base's token.
        /// </summary>
        public HashSet<string> CalledMethodNames { get; } = [];
    }

    /// <summary>
    /// Walks every method body once and records, per field, which methods load it, plus every method
    /// that is called. Field <em>stores</em> are deliberately not tracked: a store is what a setter
    /// does, and every gate here asks whether anything reads the value back out.
    /// </summary>
    private static FieldAccessMap BuildFieldAccessMap (PEReader pe, MetadataReader md)
    {
        var map = new FieldAccessMap ();

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);

            foreach (var methodHandle in type.GetMethods ()) {
                var method = md.GetMethodDefinition (methodHandle);
                if (method.RelativeVirtualAddress == 0)
                    continue;

                var il = pe.GetMethodBody (method.RelativeVirtualAddress).GetILBytes ();
                if (il is null)
                    continue;

                foreach (var (kind, token) in TokenOperandsIn (il)) {
                    if (!TryGetEntityHandle (token, out var handle))
                        continue;

                    if (kind == TokenKind.FieldLoad) {
                        // A MemberRef here names another assembly's field, which no gate cares about.
                        if (handle.Kind != HandleKind.FieldDefinition)
                            continue;

                        var fieldHandle = (FieldDefinitionHandle) handle;
                        if (!map.Readers.TryGetValue (fieldHandle, out var set))
                            map.Readers[fieldHandle] = set = [];
                        set.Add (methodHandle);
                        continue;
                    }

                    switch (handle.Kind) {
                        case HandleKind.MethodDefinition:
                            var called = (MethodDefinitionHandle) handle;
                            map.CalledMethods.Add (called);
                            map.CalledMethodNames.Add (md.GetString (md.GetMethodDefinition (called).Name));
                            break;
                        case HandleKind.MemberReference:
                            map.CalledMethodNames.Add (md.GetString (md.GetMemberReference ((MemberReferenceHandle) handle).Name));
                            break;
                        case HandleKind.MethodSpecification:
                            var spec = md.GetMethodSpecification ((MethodSpecificationHandle) handle);
                            if (spec.Method.Kind == HandleKind.MethodDefinition) {
                                var generic = (MethodDefinitionHandle) spec.Method;
                                map.CalledMethods.Add (generic);
                                map.CalledMethodNames.Add (md.GetString (md.GetMethodDefinition (generic).Name));
                            }
                            break;
                    }
                }
            }
        }

        return map;
    }

    // ldfld / ldflda / ldsfld / ldsflda. stfld and stsfld are deliberately absent -- see the remarks
    // on BuildFieldAccessMap.
    private const int OpLdfld = 0x7B;
    private const int OpLdflda = 0x7C;
    private const int OpLdsfld = 0x7E;
    private const int OpLdsflda = 0x7F;

    // call / callvirt / newobj, and the two that take a method's address to build a delegate.
    private const int OpCall = 0x28;
    private const int OpCallvirt = 0x6F;
    private const int OpNewobj = 0x73;
    private const int OpLdftn = 0xFE06;
    private const int OpLdvirtftn = 0xFE07;

    private enum TokenKind { FieldLoad, Call }

    /// <summary>
    /// Converts an IL token operand to a metadata handle, rejecting anything that is not one of the
    /// tables a field load or a call can name.
    /// </summary>
    /// <remarks>
    /// <see cref="MetadataTokens.EntityHandle(int)"/> throws for a table it does not model, and a
    /// gate that crashes on an unfamiliar instruction is worse than one that skips it. Release IL is
    /// where this first bit: the optimiser emits shapes Debug does not, so a table byte the walk had
    /// never met took the whole scan down. Filtering by table also contains any future alignment slip
    /// — a garbage operand almost never carries one of these six bytes.
    /// </remarks>
    private static bool TryGetEntityHandle (int token, out EntityHandle handle)
    {
        handle = default;

        // TypeRef, TypeDef, Field, MethodDef, MemberRef, TypeSpec, MethodSpec.
        var table = (token >> 24) & 0xFF;
        if (table is not (0x01 or 0x02 or 0x04 or 0x06 or 0x0A or 0x1B or 0x2B))
            return false;

        if ((token & 0x00FFFFFF) == 0)
            return false;

        try {
            handle = MetadataTokens.EntityHandle (token);
            return true;
        } catch (ArgumentException) {
            return false;
        }
    }

    /// <summary>
    /// Walks the IL instruction by instruction, yielding the token operand of every field load and
    /// every call. Tracking instruction boundaries is the point: an operand byte can be numerically
    /// identical to an opcode, so a scan that pattern-matches bytes invents field accesses that are
    /// not there.
    /// </summary>
    private static IEnumerable<(TokenKind Kind, int Token)> TokenOperandsIn (byte[] il)
    {
        var offset = 0;

        // `offset >= 0` is not paranoia: a mis-sized opcode can make the arithmetic below overflow to
        // a negative index, and `offset < il.Length` alone happily lets that through to an
        // IndexOutOfRangeException. A gate that crashes on unfamiliar IL is worse than one that stops
        // reading it, so every step out of bounds ends the walk instead.
        while (offset >= 0 && offset < il.Length) {
            var opcode = (int) il[offset++];

            if (opcode == 0xFE) {
                if (offset >= il.Length)
                    yield break;
                opcode = 0xFE00 | il[offset++];
            }

            var operand = OperandSize (opcode);

            if (operand == OperandSwitch) {
                if (offset + 4 > il.Length)
                    yield break;

                var count = BitConverter.ToUInt32 (il, offset);

                // A plausible table cannot be longer than the bytes that remain; anything larger means
                // the walk is already lost.
                if (count > (uint) ((il.Length - offset - 4) / 4))
                    yield break;

                offset += 4 + ((int) count * 4);
                continue;
            }

            if (operand < 0)
                yield break; // Unknown opcode: the walk has lost alignment, so stop rather than guess.

            if (offset + 4 <= il.Length) {
                if (opcode is OpLdfld or OpLdflda or OpLdsfld or OpLdsflda)
                    yield return (TokenKind.FieldLoad, BitConverter.ToInt32 (il, offset));
                else if (opcode is OpCall or OpCallvirt or OpNewobj or OpLdftn or OpLdvirtftn)
                    yield return (TokenKind.Call, BitConverter.ToInt32 (il, offset));
            }

            offset += operand;
        }
    }

    private const int OperandSwitch = -2;

    /// <summary>
    /// Operand size in bytes for each opcode, per ECMA-335 Partition III. <c>-1</c> means the opcode is
    /// not defined (the caller stops walking rather than mis-align); <see cref="OperandSwitch"/> means
    /// the variable-length <c>switch</c> table.
    /// </summary>
    private static int OperandSize (int opcode) => opcode switch {
        // Single-byte opcodes with no operand.
        //
        // Two ranges here were wrong and both desynchronised the walk. `box` is 0x8C and takes a
        // 4-byte type token, but sat inside the 0x82..0x8C conv.ovf.*.un run and was sized 0 -- so
        // every boxing conversion shifted the walk four bytes and it read operands as opcodes from
        // there on. Release IL boxes far more than Debug, which is why it surfaced as a crash there
        // and not here. 0xDA..0xDC (sub.ovf, sub.ovf.un, endfinally) were missing entirely and ended
        // the walk early.
        0x00 or 0x01 or (>= 0x02 and <= 0x0D) or 0x14 or (>= 0x15 and <= 0x1E) or 0x25 or 0x26 or 0x2A
            or (>= 0x46 and <= 0x57) or (>= 0x58 and <= 0x66) or (>= 0x67 and <= 0x6E) or 0x76 or 0x7A
            or (>= 0x82 and <= 0x8B) or 0x8E or (>= 0x90 and <= 0xA2) or (>= 0xB3 and <= 0xBA)
            or 0xC3 or (>= 0xD1 and <= 0xDC) or 0xDF or 0xE0 => 0,

        // One-byte operand.
        0x0E or 0x0F or 0x10 or 0x11 or 0x12 or 0x13 or 0x1F or 0x2B or 0x2C or 0x2D
            or (>= 0x2E and <= 0x37) or 0xDE => 1,

        // Four-byte operand (tokens, branch targets, int32, float32).
        0x20 or 0x22 or 0x27 or 0x28 or 0x29 or 0x38 or 0x39 or 0x3A or (>= 0x3B and <= 0x44)
            or 0x6F or 0x70 or 0x71 or 0x72 or 0x73 or 0x74 or 0x75 or 0x79
            or OpLdfld or OpLdflda or 0x7D or OpLdsfld or OpLdsflda or 0x80 or 0x81
            or 0x8C or 0x8D or 0x8F or 0xA3 or 0xA4 or 0xA5 or 0xC2 or 0xC6 or 0xD0 or 0xDD => 4,

        // Eight-byte operand.
        0x21 or 0x23 => 8,

        0x45 => OperandSwitch,

        // Two-byte (0xFE-prefixed) opcodes.
        0xFE00 or (>= 0xFE01 and <= 0xFE05) or 0xFE0F or 0xFE11 or 0xFE13 or 0xFE14 or 0xFE17
            or 0xFE18 or 0xFE1A or 0xFE1D or 0xFE1E => 0,
        0xFE12 or 0xFE19 => 1,
        0xFE09 or 0xFE0A or 0xFE0B or 0xFE0C or 0xFE0D or 0xFE0E => 2,
        0xFE06 or 0xFE07 or 0xFE15 or 0xFE16 or 0xFE1C => 4,

        _ => -1,
    };

    // ---------------------------------------------------------------------------------------------
    // Metadata helpers
    // ---------------------------------------------------------------------------------------------

    // Release emits `ret`; Debug prefixes a `nop`. Same shape as NoOpStubBaselineTests.IsJustReturn.
    private static bool IsEmptyBody (PEReader pe, MethodDefinition method)
    {
        if (method.RelativeVirtualAddress == 0)
            return false;

        var il = pe.GetMethodBody (method.RelativeVirtualAddress).GetILBytes ();

        return il?.Length switch {
            1 => il[0] == 0x2A,
            2 => il[0] == 0x00 && il[1] == 0x2A,
            _ => false,
        };
    }

    private static bool IsPublicOrProtected (MethodAttributes attributes)
    {
        var access = attributes & MethodAttributes.MemberAccessMask;
        return access is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem;
    }

    // A nested type is only reachable if every type enclosing it is too.
    private static bool IsPubliclyVisible (MetadataReader md, TypeDefinition type)
    {
        var visibility = type.Attributes & TypeAttributes.VisibilityMask;

        if (visibility == TypeAttributes.Public)
            return true;

        if (visibility is not (TypeAttributes.NestedPublic or TypeAttributes.NestedFamily
            or TypeAttributes.NestedFamORAssem))
            return false;

        var declaring = type.GetDeclaringType ();
        return !declaring.IsNil && IsPubliclyVisible (md, md.GetTypeDefinition (declaring));
    }

    private static string FullTypeName (MetadataReader md, TypeDefinition type)
    {
        var name = md.GetString (type.Name);
        var declaring = type.GetDeclaringType ();

        if (!declaring.IsNil)
            return $"{FullTypeName (md, md.GetTypeDefinition (declaring))}+{name}";

        var ns = md.GetString (type.Namespace);
        return ns.Length == 0 ? name : $"{ns}.{name}";
    }

    // Ordinal and de-duplicated so a baseline is a stable, reviewable diff.
    private static List<string> Normalise (IEnumerable<string> entries)
        => entries.Distinct ().OrderBy (x => x, StringComparer.Ordinal).ToList ();

    // ---------------------------------------------------------------------------------------------
    // Baseline plumbing, shared by the three gates
    // ---------------------------------------------------------------------------------------------

    /// <summary>Walks up from the test binary so a baseline lives next to the test source.</summary>
    internal static string LocateBaseline (string fileName)
    {
        var dir = new DirectoryInfo (AppContext.BaseDirectory);

        while (dir is not null) {
            var candidate = Path.Combine (dir.FullName, "tests", "Majorsilence.Forms.Tests", fileName);
            if (File.Exists (candidate))
                return candidate;

            if (File.Exists (Path.Combine (dir.FullName, "Majorsilence.Forms.slnx")))
                return Path.Combine (dir.FullName, "tests", "Majorsilence.Forms.Tests", fileName);

            dir = dir.Parent;
        }

        throw new InvalidOperationException ($"could not locate {fileName} from {AppContext.BaseDirectory}");
    }

    internal static List<string> ReadBaseline (string path)
        => File.ReadAllLines (path)
            .Where (l => l.Length > 0 && !l.StartsWith ('#'))
            .Select (l => l.Trim ())
            .Where (l => l.Length > 0)
            .ToList ();

    internal static void WriteBaseline (string path, IEnumerable<string> header, IEnumerable<string> entries)
        => File.WriteAllLines (path, [.. header, .. entries]);
}
