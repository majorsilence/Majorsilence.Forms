using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// The reachability-aware half of <see cref="StubSurfaceScanner"/>, written for
/// <c>Majorsilence.Forms.Telerik</c>.
/// </summary>
/// <remarks>
/// <para>
/// The scans in the other half answer one hop: "is this field loaded anywhere other than its own
/// getter". Issue #91 recorded four ways that reads clean over code that does nothing, and the Telerik
/// layer is built out of two of them by construction -- nearly every <c>RadGridView</c> member is a
/// hand-written forwarder onto <c>MasterTemplate</c>, so the template's auto-property is "read" by a
/// getter whose own callers are nobody. A single-hop scan calls that consumed and reports the densest
/// stub layer in the repo as almost clean.
/// </para>
/// <para>
/// So this half walks two closures instead. <em>Liveness</em>: which methods can actually run, seeded
/// from the surface a consumer or the host framework can reach (public members of public types,
/// overrides of someone else's virtual, explicit interface implementations, static constructors) and
/// propagated along call edges -- a <c>protected virtual OnXxx</c> that introduces a new slot is not a
/// seed, which is exactly trap 3. <em>Escape</em>: whether a value read out of a field reaches a method
/// that is not itself just handing it further out -- a property getter is treated as a relay and the
/// question defers to <em>its</em> callers, so a forwarding chain is only consumed if its far end is,
/// and a ring of stubs reading each other (trap 1) settles at "not consumed" because the least fixed
/// point of a cycle is false.
/// </para>
/// </remarks>
internal static partial class StubSurfaceScanner
{
    /// <summary>Telerik's stored-only auto-properties -- the reachability-aware baseline.</summary>
    internal const string TelerikStoredOnlyPropertyBaselineFileName = "TelerikStoredOnlyPropertyBaseline.txt";

    /// <summary>Telerik's `add { } remove { }` events.</summary>
    internal const string TelerikInertEventBaselineFileName = "TelerikInertEventBaseline.txt";

    /// <summary>Telerik's field-backed events that no live code raises.</summary>
    internal const string TelerikUnraisedEventBaselineFileName = "TelerikUnraisedEventBaseline.txt";

    // ---------------------------------------------------------------------------------------------
    // Scans
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Public settable auto-properties whose stored value never reaches live code that does anything
    /// with it. Strictly a superset of <see cref="ScanStoredOnlyProperties(string, out int)"/>: every
    /// reason that scan has to call a property consumed is still a reason here, but each one now has
    /// to survive the liveness and escape closures described on this class.
    /// </summary>
    /// <param name="examined">
    /// The denominator -- how many public settable auto-properties were considered. Counted the same
    /// way the single-hop scan counts it, so the two percentages are comparable.
    /// </param>
    internal static List<string> ScanStoredOnlyPropertiesDeep (string assemblyPath, out int examined)
    {
        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();
        var model = BuildReachabilityModel (pe, md);
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

                if (!fieldsByName.TryGetValue ($"<{name}>k__BackingField", out var fieldHandle))
                    continue;

                examined++;

                if (model.FieldValueEscapes (fieldHandle, accessors.Getter))
                    continue;

                found.Add ($"{FullTypeName (md, type)}.{name}");
            }
        }

        return Normalise (found);
    }

    /// <summary>
    /// Field-backed events that nothing <em>live</em> ever raises. The difference from
    /// <see cref="ScanUnraisedEvents"/> is trap 3: a <c>protected virtual OnXxx</c> containing
    /// <c>Xxx?.Invoke (...)</c> makes that scan call the event raised even when no code path in the
    /// assembly ever reaches the raiser, which is the shape this layer has.
    /// </summary>
    /// <param name="examined">How many field-backed public/protected events were considered.</param>
    internal static List<string> ScanUnraisedEventsDeep (string assemblyPath, out int examined)
    {
        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();
        var model = BuildReachabilityModel (pe, md);
        var found = new List<string> ();
        examined = 0;

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

                // A field-like event's backing field carries the event's own name. This layer also
                // writes the pair out by hand over a private `_name` field -- RadGridView does it for
                // most of its grid events -- and that shape is invisible to both the other event
                // gates, so the store is recovered from the adder instead.
                if (!fieldsByName.TryGetValue (name, out var fieldHandle)
                    && !model.TryFindHandWrittenEventStore (accessors.Adder, type, out fieldHandle))
                    continue;

                examined++;

                if (model.EventIsRaised (fieldHandle, accessors.Adder, accessors.Remover))
                    continue;

                found.Add ($"{FullTypeName (md, type)}.{name}");
            }
        }

        return Normalise (found);
    }

    /// <summary>
    /// How many events on publicly visible types a consumer can subscribe to at all -- the
    /// denominator both event baselines are a fraction of.
    /// </summary>
    internal static int CountSubscribableEvents (string assemblyPath)
    {
        using var stream = File.OpenRead (assemblyPath);
        using var pe = new PEReader (stream);
        var md = pe.GetMetadataReader ();
        var count = 0;

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);
            if (!IsPubliclyVisible (md, type))
                continue;

            foreach (var eventHandle in type.GetEvents ()) {
                var accessors = md.GetEventDefinition (eventHandle).GetAccessors ();
                if (accessors.Adder.IsNil || accessors.Remover.IsNil)
                    continue;
                if (IsPublicOrProtected (md.GetMethodDefinition (accessors.Adder).Attributes))
                    count++;
            }
        }

        return count;
    }

    // ---------------------------------------------------------------------------------------------
    // The model
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// One assembly's call graph, field reads, and the two closures computed over them.
    /// </summary>
    private sealed class ReachabilityModel
    {
        /// <summary>Per field, the methods that load it.</summary>
        public Dictionary<FieldDefinitionHandle, HashSet<MethodDefinitionHandle>> Readers { get; } = [];

        /// <summary>Per method, the fields it loads -- the reverse of <see cref="Readers"/>.</summary>
        public Dictionary<MethodDefinitionHandle, HashSet<FieldDefinitionHandle>> FieldsRead { get; } = [];

        /// <summary>
        /// Methods that call <c>Delegate.Combine</c>, which is what a hand-written `add` accessor over
        /// a private delegate field compiles to and what an accessor that merely forwards to another
        /// event does not.
        /// </summary>
        public HashSet<MethodDefinitionHandle> CombinesDelegates { get; } = [];

        /// <summary>Per method, the methods that call it (virtual dispatch already expanded).</summary>
        public Dictionary<MethodDefinitionHandle, HashSet<MethodDefinitionHandle>> Callers { get; } = [];

        /// <summary>Methods reachable from the surface a consumer or the host framework can drive.</summary>
        public HashSet<MethodDefinitionHandle> Live { get; } = [];

        /// <summary>Methods whose whole body is `ret` -- they cannot consume anything.</summary>
        public HashSet<MethodDefinitionHandle> EmptyBodied { get; } = [];

        /// <summary>Every property getter, the relay case of the escape closure.</summary>
        public HashSet<MethodDefinitionHandle> Getters { get; } = [];

        /// <summary>Per getter, whether its return value reaches a live non-relay consumer.</summary>
        public Dictionary<MethodDefinitionHandle, bool> GetterEscapes { get; } = [];

        /// <summary>
        /// Whether a stored value ever reaches live code that is not simply handing it back out. The
        /// two ways to read a property are the field (from inside the declaring type) and the getter,
        /// and both have to count -- checking only the field reports every properly encapsulated
        /// property as inert.
        /// </summary>
        public bool FieldValueEscapes (FieldDefinitionHandle field, MethodDefinitionHandle owningGetter)
        {
            if (Readers.TryGetValue (field, out var readers) && readers.Any (m => m != owningGetter && IsRealConsumer (m)))
                return true;

            return GetterEscapes.TryGetValue (owningGetter, out var escapes) && escapes;
        }

        /// <summary>
        /// Whether anything that can actually run reads the event's backing field in order to invoke
        /// it. The compiler-generated add/remove pair reads the field too, so they are excluded.
        /// </summary>
        public bool EventIsRaised (FieldDefinitionHandle field, MethodDefinitionHandle adder, MethodDefinitionHandle remover)
            => Readers.TryGetValue (field, out var readers)
                && readers.Any (m => m != adder && m != remover && Live.Contains (m) && !EmptyBodied.Contains (m));

        /// <summary>
        /// The delegate field behind an event whose accessors were written by hand. The adder has to
        /// load the field to combine into it, so "the one field of this type the adder reads, in an
        /// adder that combines delegates" identifies the store -- and rules out both the inert
        /// `add { }` (reads nothing) and the alias that forwards to another event (combines nothing).
        /// </summary>
        public bool TryFindHandWrittenEventStore (MethodDefinitionHandle adder, TypeDefinition declaringType,
            out FieldDefinitionHandle field)
        {
            field = default;

            if (!CombinesDelegates.Contains (adder) || !FieldsRead.TryGetValue (adder, out var read))
                return false;

            var declared = read.Where (f => declaringType.GetFields ().Contains (f)).ToList ();
            if (declared.Count != 1)
                return false;

            field = declared[0];
            return true;
        }

        /// <summary>
        /// A reader counts only if it can run, does something, and is not a relay -- and a relay counts
        /// only if its own value escapes, which is what makes a forwarding chain resolve at its far end
        /// rather than at its first hop.
        /// </summary>
        private bool IsRealConsumer (MethodDefinitionHandle method)
        {
            if (!Live.Contains (method) || EmptyBodied.Contains (method))
                return false;

            return Getters.Contains (method)
                ? GetterEscapes.TryGetValue (method, out var escapes) && escapes
                : true;
        }
    }

    private static ReachabilityModel BuildReachabilityModel (PEReader pe, MetadataReader md)
    {
        var model = new ReachabilityModel ();
        var callees = new Dictionary<MethodDefinitionHandle, HashSet<MethodDefinitionHandle>> ();
        var roots = new HashSet<MethodDefinitionHandle> ();
        var methodsByTypeAndName = new Dictionary<TypeDefinitionHandle, Dictionary<string, List<MethodDefinitionHandle>>> ();

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);
            var visible = IsPubliclyVisible (md, type);
            var byName = new Dictionary<string, List<MethodDefinitionHandle>> ();
            methodsByTypeAndName[typeHandle] = byName;

            foreach (var methodHandle in type.GetMethods ()) {
                var method = md.GetMethodDefinition (methodHandle);
                var name = md.GetString (method.Name);

                if (!byName.TryGetValue (name, out var sameName))
                    byName[name] = sameName = [];
                sameName.Add (methodHandle);

                if (IsEmptyBody (pe, method))
                    model.EmptyBodied.Add (methodHandle);

                // A consumer can call anything public on a public type, the host calls back into any
                // override, and a static constructor runs on its own. A `protected virtual` that
                // introduces a new slot is deliberately not seeded: nothing in a compat layer derives
                // from its own controls, so an OnXxx raiser nobody calls really is dead code (trap 3).
                var isPublic = (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
                var isOverride = (method.Attributes & MethodAttributes.Virtual) != 0
                    && (method.Attributes & MethodAttributes.NewSlot) == 0;

                if (isOverride || (isPublic && visible) || name == ".cctor")
                    roots.Add (methodHandle);
            }

            // An explicit interface implementation is private and named `IFoo.Bar`, so neither of the
            // rules above finds it, yet every call through the interface lands there.
            foreach (var implHandle in type.GetMethodImplementations ()) {
                var impl = md.GetMethodImplementation (implHandle);
                if (impl.MethodBody.Kind == HandleKind.MethodDefinition)
                    roots.Add ((MethodDefinitionHandle) impl.MethodBody);
            }

            foreach (var propertyHandle in type.GetProperties ()) {
                var getter = md.GetPropertyDefinition (propertyHandle).GetAccessors ().Getter;
                if (!getter.IsNil)
                    model.Getters.Add (getter);
            }
        }

        var overriddenBy = BuildOverrideMap (md, methodsByTypeAndName);

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
                        if (handle.Kind != HandleKind.FieldDefinition)
                            continue;

                        var fieldHandle = (FieldDefinitionHandle) handle;
                        if (!model.Readers.TryGetValue (fieldHandle, out var readers))
                            model.Readers[fieldHandle] = readers = [];
                        readers.Add (methodHandle);

                        if (!model.FieldsRead.TryGetValue (methodHandle, out var read))
                            model.FieldsRead[methodHandle] = read = [];
                        read.Add (fieldHandle);
                        continue;
                    }

                    if (handle.Kind == HandleKind.MemberReference
                        && md.GetString (md.GetMemberReference ((MemberReferenceHandle) handle).Name) == "Combine")
                        model.CombinesDelegates.Add (methodHandle);

                    var target = ResolveCallTarget (md, handle);
                    if (target.IsNil)
                        continue;

                    if (!callees.TryGetValue (methodHandle, out var calls))
                        callees[methodHandle] = calls = [];

                    // A call site carries the token of the type the reference was typed as, so a call
                    // to a base declaration has to reach every override of it in this assembly.
                    calls.Add (target);
                    if (overriddenBy.TryGetValue (target, out var overrides))
                        calls.UnionWith (overrides);
                }
            }
        }

        foreach (var (caller, targets) in callees)
            foreach (var target in targets) {
                if (!model.Callers.TryGetValue (target, out var callers))
                    model.Callers[target] = callers = [];
                callers.Add (caller);
            }

        PropagateLiveness (model, callees, roots);
        PropagateEscape (model);

        return model;
    }

    /// <summary>
    /// Maps a virtual method to the methods in this assembly that override it, so a call typed as the
    /// base reaches the override too.
    /// </summary>
    /// <remarks>
    /// Matched on name alone rather than on the signature blob. Overloads therefore link more edges
    /// than strictly exist, which errs toward "live" and "consumed" -- the same direction the
    /// single-hop scan errs in, and the safe one: a false edge keeps a working member out of the
    /// baseline, a missing edge would put one in.
    /// </remarks>
    private static Dictionary<MethodDefinitionHandle, HashSet<MethodDefinitionHandle>> BuildOverrideMap (
        MetadataReader md, Dictionary<TypeDefinitionHandle, Dictionary<string, List<MethodDefinitionHandle>>> methodsByTypeAndName)
    {
        var map = new Dictionary<MethodDefinitionHandle, HashSet<MethodDefinitionHandle>> ();

        foreach (var typeHandle in md.TypeDefinitions) {
            var type = md.GetTypeDefinition (typeHandle);

            foreach (var methodHandle in type.GetMethods ()) {
                var method = md.GetMethodDefinition (methodHandle);
                if ((method.Attributes & MethodAttributes.Virtual) == 0
                    || (method.Attributes & MethodAttributes.NewSlot) != 0)
                    continue;

                var name = md.GetString (method.Name);

                // Walk the whole in-assembly base chain rather than stopping at the first declaration,
                // so a three-deep hierarchy links the leaf to the root as well as to its parent.
                var baseHandle = type.BaseType;
                while (!baseHandle.IsNil && baseHandle.Kind == HandleKind.TypeDefinition
                    && methodsByTypeAndName.ContainsKey ((TypeDefinitionHandle) baseHandle)) {
                    var baseType = md.GetTypeDefinition ((TypeDefinitionHandle) baseHandle);

                    if (methodsByTypeAndName.TryGetValue ((TypeDefinitionHandle) baseHandle, out var byName)
                        && byName.TryGetValue (name, out var candidates))
                        foreach (var candidate in candidates) {
                            if ((md.GetMethodDefinition (candidate).Attributes & MethodAttributes.Virtual) == 0)
                                continue;
                            if (!map.TryGetValue (candidate, out var set))
                                map[candidate] = set = [];
                            set.Add (methodHandle);
                        }

                    baseHandle = baseType.BaseType;
                }
            }
        }

        return map;
    }

    /// <summary>Resolves a call operand to a method defined in this assembly, if it names one.</summary>
    private static MethodDefinitionHandle ResolveCallTarget (MetadataReader md, EntityHandle handle)
        => handle.Kind switch {
            HandleKind.MethodDefinition => (MethodDefinitionHandle) handle,
            HandleKind.MethodSpecification when md.GetMethodSpecification ((MethodSpecificationHandle) handle).Method.Kind
                == HandleKind.MethodDefinition
                => (MethodDefinitionHandle) md.GetMethodSpecification ((MethodSpecificationHandle) handle).Method,
            // A MemberRef names another assembly's method, or a member of a generic instantiation --
            // neither can be a raise site or a consumer inside this one.
            _ => default,
        };

    private static void PropagateLiveness (ReachabilityModel model,
        Dictionary<MethodDefinitionHandle, HashSet<MethodDefinitionHandle>> callees,
        HashSet<MethodDefinitionHandle> roots)
    {
        var queue = new Queue<MethodDefinitionHandle> (roots);
        model.Live.UnionWith (roots);

        while (queue.Count > 0) {
            var method = queue.Dequeue ();
            if (!callees.TryGetValue (method, out var targets))
                continue;

            foreach (var target in targets)
                if (model.Live.Add (target))
                    queue.Enqueue (target);
        }
    }

    /// <summary>
    /// The least fixed point of "this getter's value reaches a live consumer that is not itself a
    /// relay". Starting every getter at <c>false</c> and only ever turning one <c>true</c> is what
    /// makes a cycle settle at "does not escape": a ring of stub properties reading each other
    /// (trap 1) never gains a reason to flip.
    /// </summary>
    private static void PropagateEscape (ReachabilityModel model)
    {
        foreach (var getter in model.Getters)
            model.GetterEscapes[getter] = false;

        bool changed;
        do {
            changed = false;

            foreach (var getter in model.Getters) {
                if (model.GetterEscapes[getter])
                    continue;
                if (!model.Callers.TryGetValue (getter, out var callers))
                    continue;

                foreach (var caller in callers) {
                    if (caller == getter || !model.Live.Contains (caller) || model.EmptyBodied.Contains (caller))
                        continue;
                    if (model.Getters.Contains (caller) && !model.GetterEscapes[caller])
                        continue;

                    model.GetterEscapes[getter] = true;
                    changed = true;
                    break;
                }
            }
        } while (changed);
    }
}
