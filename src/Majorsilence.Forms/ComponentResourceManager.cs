using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
#if !NETSTANDARD2_0
using System.Runtime.Loader;   // AssemblyLoadContext; absent on .NET Framework / netstandard2.0
#endif
using System.Xml.Linq;

namespace Majorsilence.Forms
{
    /// <summary>
    /// A cross-platform stand-in for <c>System.ComponentModel.ComponentResourceManager</c>. WinForms
    /// designer code reaches resources through this type:
    /// <code>
    /// var resources = new ComponentResourceManager (typeof (Form1));
    /// this.button1.Image = (Image) resources.GetObject ("button1.Image");
    /// resources.ApplyResources (this.button1, "button1");
    /// </code>
    /// The framework version pulls images through <c>System.Drawing</c> (GDI+) and can deserialize
    /// <c>BinaryFormatter</c> payloads, neither of which works cross-platform. This implementation reads
    /// the <c>.resx</c> XML directly and returns framework primitives + <see cref="Majorsilence.Forms.Drawing"/>
    /// images, so a migrated form initialises its controls on Windows, macOS and Linux alike.
    ///
    /// Resources stored as <c>BinaryFormatter</c>/SOAP blobs (<c>binary.base64</c> / <c>soap.base64</c>)
    /// cannot be read on modern .NET; <see cref="GetObject(string)"/> returns <see langword="null"/> for those
    /// (the migrator flags them for manual re-export).
    /// </summary>
    public class ComponentResourceManager
    {
        // name -> raw resx entry (we parse lazily on first access so an unused entry never costs work).
        private readonly Dictionary<string, ResxEntry> _entries = new (StringComparer.Ordinal);

        // name -> already-materialized value, read from a compiled .resources binary (see
        // LoadCompiledResources). This is the real data source for every normal SDK-built project:
        // `<EmbeddedResource Include="Foo.resx">` compiles to a `<Namespace>.Foo.resources` manifest
        // resource, not a raw XML `.resx` -- the _entries path above only ever fires for the unusual
        // case of a hand-embedded raw .resx file (FromFile/FromStream/FromXml, or one copied loose to
        // the output directory).
        private readonly Dictionary<string, object?> _binaryEntries = new (StringComparer.Ordinal);

        static ComponentResourceManager ()
        {
            RegisterWinFormsEnumResolver ();
            RegisterDrawingShimResolver ();
        }

        /// <summary>Creates an empty resource manager (no backing <c>.resx</c>).</summary>
        public ComponentResourceManager () { }

        /// <summary>
        /// Locates the resources associated with <paramref name="resourceSource"/> — first the
        /// standard SDK-compiled <c>&lt;Namespace&gt;.&lt;Name&gt;.resources</c> binary embedded by
        /// any normal <c>&lt;EmbeddedResource Include="Foo.resx"&gt;</c> project item (see
        /// <see cref="LoadCompiledResources(System.Type)"/>), then falls back to a raw <c>&lt;FullName&gt;.resx</c>
        /// XML resource or a loose <c>.resx</c> file beside the assembly. If neither is found the
        /// manager is simply empty, so designer code still runs (controls keep their coded defaults).
        /// </summary>
        public ComponentResourceManager (Type resourceSource)
        {
            Guard.ThrowIfNull (resourceSource);

            LoadCompiledResources (resourceSource);

            var resx = LocateResx (resourceSource);
            if (resx is not null)
                Load (resx.Value.Xml, resx.Value.Directory);
        }

        /// <summary>
        /// Creates a resource manager for the compiled <c>&lt;baseName&gt;.resources</c> embedded in
        /// <paramref name="assembly"/> — the shape VB's <c>My.Resources</c> designer code constructs
        /// (<c>New ResourceManager("&lt;RootNamespace&gt;.Resources", GetType(...).Assembly)</c>). Retargeted
        /// projects alias <c>System.Resources.ResourceManager</c> to this on the Majorsilence.Forms flavor so
        /// <c>My.Resources.SomeImage</c> comes back as a <see cref="Majorsilence.Forms.Drawing"/> type
        /// (normalized in <see cref="LoadCompiledResources(System.Reflection.Assembly, string, string?)"/>) instead of a live
        /// System.Drawing.Bitmap that the generated <c>CType(obj, Bitmap)</c> then fails to cast.
        /// </summary>
        public ComponentResourceManager (string baseName, Assembly assembly)
        {
            Guard.ThrowIfNull (baseName);
            Guard.ThrowIfNull (assembly);

            LoadCompiledResources (assembly, baseName);
        }

        /// <summary>
        /// Builds a resource manager from a <c>.resx</c> file on disk. File-linked entries
        /// (<c>ResXFileRef</c>) are resolved relative to that file's own directory, the way the
        /// designer wrote them.
        /// </summary>
        public static ComponentResourceManager FromFile (string path)
        {
            var mgr = new ComponentResourceManager ();
            string? directory;
            try { directory = Path.GetDirectoryName (Path.GetFullPath (path)); }
            catch { directory = null; }
            mgr.Load (File.ReadAllText (path), directory);
            return mgr;
        }

        /// <summary>Builds a resource manager from a <c>.resx</c> stream.</summary>
        public static ComponentResourceManager FromStream (Stream stream)
        {
            using var reader = new StreamReader (stream);
            var mgr = new ComponentResourceManager ();
            mgr.Load (reader.ReadToEnd ());
            return mgr;
        }

        /// <summary>Builds a resource manager from <c>.resx</c> XML held in memory.</summary>
        public static ComponentResourceManager FromXml (string xml)
        {
            var mgr = new ComponentResourceManager ();
            mgr.Load (xml);
            return mgr;
        }

        /// <summary>Returns the resource named <paramref name="name"/> as a string, or null if absent.</summary>
        public string? GetString (string name)
        {
            if (_binaryEntries.TryGetValue (name, out var bv))
                return bv as string;
            return _entries.TryGetValue (name, out var e) ? e.RawValue : null;
        }

        /// <summary>
        /// Returns the resource named <paramref name="name"/>: a string, a framework primitive
        /// (<c>Point</c>/<c>Size</c>/<c>Color</c>/<c>bool</c>/<c>int</c>/…), or a
        /// <see cref="Majorsilence.Forms.Drawing"/> image/icon. Returns null for absent or unreadable entries.
        /// </summary>
        public object? GetObject (string name)
        {
            if (_binaryEntries.TryGetValue (name, out var bv))
                return bv;
            return _entries.TryGetValue (name, out var e) ? Materialize (e) : null;
        }

        /// <summary>
        /// Culture-aware overload matching <c>System.Resources.ResourceManager.GetObject(name, culture)</c>
        /// (the signature VB's My.Resources designer code calls). The compat manager is culture-agnostic
        /// (invariant/neutral resources only), so <paramref name="culture"/> is ignored.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage ("Globalization", "CA1304:Specify CultureInfo",
            Justification = "This compat manager is culture-agnostic (invariant/neutral resources only); the culture-aware overload intentionally ignores culture and forwards to the single-arg accessor.")]
        public object? GetObject (string name, System.Globalization.CultureInfo? culture) => GetObject (name);

        /// <summary>Culture-aware overload matching <c>ResourceManager.GetString(name, culture)</c>. Culture is ignored.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage ("Globalization", "CA1304:Specify CultureInfo",
            Justification = "This compat manager is culture-agnostic (invariant/neutral resources only); the culture-aware overload intentionally ignores culture and forwards to the single-arg accessor.")]
        public string? GetString (string name, System.Globalization.CultureInfo? culture) => GetString (name);

        /// <summary>
        /// Applies every resx entry named <c>"<paramref name="objectName"/>.&lt;Property&gt;"</c> to the
        /// matching public property of <paramref name="value"/> by reflection — the cross-platform
        /// equivalent of the framework's culture-aware property application.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("ReflectionAnalysis", "IL2072",
            Justification = "value.GetType() is a live designer/runtime object (a Control subclass constructed by " +
                "InitializeComponent) whose settable public properties are never trimmed in a WinForms-shaped app; " +
                "there is no static annotation surface for GetType()'s return type to propagate through.")]
        public void ApplyResources (object value, string objectName)
        {
            Guard.ThrowIfNull (value);
            Guard.ThrowIfNull (objectName);

            var prefix = objectName + ".";
            var type = value.GetType ();

            foreach (var (name, raw) in EnumerateWithPrefix (prefix))
            {
                var propertyName = name[prefix.Length..];
                // Skip designer bookkeeping keys that aren't simple settable properties.
                if (propertyName.Contains ('.'))
                    continue;

                var property = GetPropertyResolvingHiding (type, propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null || !property.CanWrite)
                    continue;

                if (TryConvert (raw, property.PropertyType, out var converted))
                {
                    try { property.SetValue (value, converted); }
                    catch { /* a property that rejects the value is non-fatal — keep applying the rest. */ }
                }
            }
        }

        // Type.GetProperty(name) throws AmbiguousMatchException when a property is redeclared with
        // `new` somewhere in the hierarchy (e.g. TabControl.Padding hiding Control.Padding) — both
        // members share the name, and the single-property overload has no way to prefer one. Real
        // WinForms designer serialization goes through TypeDescriptor, which resolves `new`-hiding
        // correctly; this walks the hierarchy from the most-derived type down and returns the first
        // declared match, matching that behavior without pulling in TypeDescriptor.
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("ReflectionAnalysis", "IL2075",
            Justification = "Type.BaseType has no DynamicallyAccessedMembers annotation to propagate through a " +
                "hierarchy walk — a known analyzer gap, not a real trimming hazard here (see ApplyResources).")]
        private static PropertyInfo? GetPropertyResolvingHiding (
            [DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type type,
            string propertyName, BindingFlags flags)
        {
            for (var t = type; t is not null; t = t.BaseType) {
                var property = t.GetProperty (propertyName, flags | BindingFlags.DeclaredOnly);
                if (property is not null)
                    return property;
            }

            return null;
        }

        // Yields (name, materialized value) for every entry starting with prefix, across both the
        // compiled-.resources and raw-XML-.resx data sources. _binaryEntries wins on a name that
        // (unusually) exists in both, since it holds real deserialized objects rather than strings
        // still needing type-directed parsing.
        private IEnumerable<(string Name, object? Value)> EnumerateWithPrefix (string prefix)
        {
            foreach (var (name, v) in _binaryEntries)
                if (name.StartsWith (prefix, StringComparison.Ordinal))
                    yield return (name, v);

            foreach (var (name, entry) in _entries)
                if (name.StartsWith (prefix, StringComparison.Ordinal) && !_binaryEntries.ContainsKey (name))
                    yield return (name, Materialize (entry));
        }

        // ── compiled .resources binary parsing ───────────────────────────────────────────────

        // Finds and reads the standard SDK-compiled "<Namespace>.<Name>.resources" manifest resource
        // for resourceSource's assembly -- the actual embedded format for any ordinary
        // `<EmbeddedResource Include="Foo.resx">` project item. Every entry that fails to
        // deserialize is skipped individually rather than aborting the whole resource set: WinForms-
        // only enum types (DockStyle, AnchorStyles, ImeMode, Keys/ShortcutKeys -- anything the
        // original resx recorded against "System.Windows.Forms, ...", which that assembly
        // deliberately doesn't reference) and GDI+-backed Font/Image/Icon entries (System.Drawing.
        // Common's native layer isn't available/functional cross-platform) are the only entries
        // expected to fail this way; everything else -- the Size/Location/Text/Dock/etc. that
        // actually drive layout -- reads fine.
        private void LoadCompiledResources (Type resourceSource)
            => LoadCompiledResources (resourceSource.Assembly, resourceSource.FullName!, resourceSource.Name);

        // baseName is the resx's namespace-qualified name ("<RootNamespace>.<Name>", e.g.
        // "libUtilities.Resources") -- the compiled manifest resource is "<baseName>.resources".
        private void LoadCompiledResources (Assembly assembly, string baseName, string? shortName = null)
        {
            var resourceName = assembly.GetManifestResourceNames ()
                .FirstOrDefault (n => n.EndsWith ("." + baseName + ".resources", StringComparison.Ordinal)
                                   || n.EndsWith (baseName + ".resources", StringComparison.Ordinal)
                                   || (shortName is not null && n.EndsWith ("." + shortName + ".resources", StringComparison.Ordinal)));
            if (resourceName is null)
                return;

            byte[] bytes;
            using (var manifest = assembly.GetManifestResourceStream (resourceName))
            {
                if (manifest is null)
                    return;

                using var buffer = new MemoryStream ();
                manifest.CopyTo (buffer);
                bytes = buffer.ToArray ();
            }

            System.Resources.Extensions.DeserializingResourceReader reader;
            try { reader = new System.Resources.Extensions.DeserializingResourceReader (new MemoryStream (bytes, writable: false)); }
            catch
            {
                // Not the preserialized format this reader expects -- try to lift the payloads out of the
                // container directly rather than giving up on the whole resource set.
                RecoverRawEntries (bytes, _ => true);
                return;
            }

            // Names whose value the reader would not produce. Collected rather than acted on inline
            // because recovering them re-reads the container, which would disturb this enumerator.
            var unreadable = new List<string> ();

            using (reader)
            {
                var enumerator = reader.GetEnumerator ();
                while (true)
                {
                    bool moved;
                    try { moved = enumerator.MoveNext (); }
                    catch { break; }   // can't recover mid-stream; keep whatever was already read.
                    if (!moved)
                        break;

                    // Key is safe to read for every entry -- only Value deserializes, and only Value throws.
                    string name;
                    try { name = (string) enumerator.Key; }
                    catch { continue; }

                    try
                    {
                        var value = NormalizeDeserialized (enumerator.Value);
                        _binaryEntries[name] = value;

                        if (value is null)
                            unreadable.Add (name);   // deserialized, but into nothing we could use.
                    }
                    catch { unreadable.Add (name); }
                }
            }

            if (unreadable.Count > 0)
                RecoverRawEntries (bytes, unreadable.Contains);
        }

        // Second pass over the entries the reader above could not turn into a value: takes their payload
        // bytes straight out of the container and decodes the shapes we support ourselves. The case that
        // matters in practice is a designer resource the original tooling wrote with BinaryFormatter --
        // ImageList.ImageStream, and images/icons saved the same way -- which the reader now refuses
        // outright (BinaryFormatter was removed in .NET 9) even though the bytes are ordinary NRBF that
        // NrbfResourceReader reads. Left unrecovered, an ImageList stays empty and every toolbar button
        // that indexes into it draws with no image at all.
        private void RecoverRawEntries (byte[] file, Func<string, bool> wanted)
        {
            foreach (var (name, entry) in RawResourcesReader.Read (file, wanted))
            {
                var value = entry.Format switch {
                    // Already returns Majorsilence.Forms.Drawing types, so no NormalizeDeserialized here.
                    RawResourcesReader.RawFormat.BinaryFormatter
                        => NrbfResourceReader.TryReadObject (entry.Data),

                    // Both store an image/icon as its original file bytes.
                    RawResourcesReader.RawFormat.TypeConverterByteArray or
                    RawResourcesReader.RawFormat.ActivatorStream
                        => BuildImage (entry.TypeName, entry.Data),

                    _ => null,
                };

                if (value is not null)
                    _binaryEntries[name] = value;
            }
        }

        // Migrated WinForms .resx files record Dock/Anchor property values against the *original*
        // System.Windows.Forms.DockStyle/AnchorStyles types (that's what Visual Studio wrote when the
        // form was last saved on Windows) -- and that assembly deliberately isn't available in a
        // cross-platform Majorsilence.Forms app. Left alone, DeserializingResourceReader's
        // Type.GetType(...) call for those entries throws (see LoadCompiledResources' per-entry
        // catch), so every docked/anchored control's layout silently reverts to the coded default
        // (DockStyle.None) -- the real cause of a "renders as a totally blank window" bug once found
        // in the wild (ReportDesigner.Forms: RdlDesigner's InitializeComponent has no inline `.Dock =`
        // anywhere, since the migrated designer code relied entirely on resx-driven Dock).
        //
        // AssemblyLoadContext.Resolving fires only after normal probing for "System.Windows.Forms"
        // has already failed, so this never intercepts a *real* System.Windows.Forms.dll if one
        // happens to be present (e.g. WindowsFormsInterop's Windows-only bridge) -- it only fills the
        // gap where that assembly plain doesn't exist. Majorsilence.Forms.WinFormsEnumShims (a small,
        // fully cross-platform satellite project, embedded into this assembly at build time -- see
        // the EmbedWinFormsEnumShims target in Majorsilence.Forms.csproj) declares DockStyle/
        // AnchorStyles under that same namespace with the same numeric values as the real thing,
        // purely so Type.GetType's by-name lookup inside the returned assembly succeeds; TryConvert
        // then bridges the resulting (wrong-type-but-right-value) enum across to the control's real
        // Majorsilence.Forms.DockStyle/AnchorStyles property by underlying integer, not type identity.
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "The loaded assembly is Majorsilence.Forms.WinFormsEnumShims, embedded above: a couple of plain enum types with no members or reflection-driven behavior of their own for a trimmer to remove.")]
        private static void RegisterWinFormsEnumResolver ()
        {
            // Load the shim from bytes embedded in *this* assembly, not a normal referenced/copied
            // file: a plain ProjectReference would need its own AssemblyName ("System.Windows.Forms",
            // deliberately -- see that project) reflected correctly in every consumer's deps.json,
            // which the SDK's deps.json generation for ProjectReferences doesn't actually do (it
            // keys the runtime-file entry by project name instead), so the file .NET expects at that
            // deps.json-recorded name doesn't exist -- FileNotFoundException the first time a
            // deps.json-driven host (e.g. `dotnet test`/apphost) tries to load it, even though the
            // physically-copied .dll sits right there in the output folder. Loading the bytes
            // directly bypasses deps.json entirely; there is nothing there to fall out of sync with.
            //
            // Resolved and cached *before* registering the handler, and captured once rather than
            // re-loading on every call: this shim is itself named "System.Windows.Forms" (again, see
            // that project), so if the handler re-triggered the same load from inside its own body on
            // every invocation, that would recurse into this same handler forever (a real stack
            // overflow hit during testing). Loading it here first, unconditionally, means any
            // recursive Resolving dispatch happens before the handler below is even registered.
            byte[]? shimBytes;
            using (var stream = typeof (ComponentResourceManager).Assembly
                       .GetManifestResourceStream ("Majorsilence.Forms.WinFormsEnumShims.dll"))
            {
                if (stream is null)
                    return;   // build didn't embed it (e.g. a consumer building this project oddly) -- degrade quietly.
                using var buffer = new MemoryStream ();
                stream.CopyTo (buffer);
                shimBytes = buffer.ToArray ();
            }

            Assembly shimAssembly;
            try { shimAssembly = Assembly.Load (shimBytes); }
            catch { return; }

#if NETSTANDARD2_0
            // .NET Framework / netstandard2.0 has no AssemblyLoadContext; AppDomain.AssemblyResolve is
            // the equivalent last-resort hook, and likewise fires only after normal probing fails.
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                new AssemblyName (args.Name).Name == "System.Windows.Forms" ? shimAssembly : null;
#else
            AssemblyLoadContext.Default.Resolving += (_, name) =>
                name.Name == "System.Windows.Forms" ? shimAssembly : null;
#endif
        }

        // The image counterpart of RegisterWinFormsEnumResolver. A compiled .resx stores an image
        // against "System.Drawing.Bitmap, System.Drawing, ..." -- and while System.Drawing itself is a
        // shared-framework facade that resolves fine, the Bitmap it type-forwards to lives in
        // System.Drawing.Common, a NuGet package a cross-platform app has no reason to reference (and
        // that cannot decode images off Windows anyway). So the reader's Type.GetType threw
        // FileNotFoundException for System.Drawing.Common, LoadCompiledResources' per-entry catch
        // swallowed it, and every single image in a Resources.resx came back null -- a form's buttons
        // rendered as empty rectangles, which is how this was found.
        //
        // Majorsilence.Forms.DrawingShims (embedded at build time, see EmbedDrawingShims) declares
        // stand-in Bitmap/Icon/Image types that decode nothing and just keep the original file bytes;
        // NormalizeDeserialized below reads those back off and hands them to SkiaSharp.
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "The loaded assembly is Majorsilence.Forms.DrawingShims, embedded above: three byte[]-holding types with no reflection-driven behavior of their own for a trimmer to remove.")]
        private static void RegisterDrawingShimResolver ()
        {
            // Resolved and cached before the handler is registered, for the same recursion reason
            // spelled out in RegisterWinFormsEnumResolver: this shim is itself named
            // "System.Drawing.Common".
            byte[]? shimBytes;
            using (var stream = typeof (ComponentResourceManager).Assembly
                       .GetManifestResourceStream ("Majorsilence.Forms.DrawingShims.dll"))
            {
                if (stream is null)
                    return;   // build didn't embed it -- degrade quietly, exactly as before this existed.
                using var buffer = new MemoryStream ();
                stream.CopyTo (buffer);
                shimBytes = buffer.ToArray ();
            }

            Assembly shimAssembly;
            try { shimAssembly = Assembly.Load (shimBytes); }
            catch { return; }

#if NETSTANDARD2_0
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                new AssemblyName (args.Name).Name == "System.Drawing.Common" ? shimAssembly : null;
#else
            AssemblyLoadContext.Default.Resolving += (_, name) =>
                name.Name == "System.Drawing.Common" ? shimAssembly : null;
#endif
        }

        // Pulls the original file bytes back off a stand-in image produced by the embedded
        // System.Drawing.Common shim (see RegisterDrawingShimResolver). Reflection rather than a
        // direct reference because that assembly is loaded from bytes at runtime and is deliberately
        // not referenced at compile time -- its type names collide with the real System.Drawing.
        [UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "The property is on a Majorsilence.Forms.DrawingShims type, which is IsTrimmable but reached only by having been instantiated by the resource reader moments earlier; a miss degrades to false and the caller falls through to the live-System.Drawing path.")]
        private static bool TryGetShimImageBytes (object value, [NotNullWhen (true)] out byte[]? bytes)
        {
            bytes = value.GetType ()
                .GetProperty ("MajorsilenceRawBytes", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue (value) as byte[];

            return bytes is { Length: > 0 };
        }

        // Companion to TryGetShimImageBytes for the font stand-in; same reflection-not-reference reasoning.
        [UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "The property is on a Majorsilence.Forms.DrawingShims type, which is IsTrimmable but reached only by having been instantiated by the resource reader moments earlier; a miss degrades to false and the caller falls through to the live-System.Drawing path.")]
        private static bool TryGetShimFontSpec (object value, [NotNullWhen (true)] out string? spec)
        {
            spec = value.GetType ()
                .GetProperty ("MajorsilenceFontSpec", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue (value) as string;

            return !string.IsNullOrWhiteSpace (spec);
        }

        // On Windows, System.Drawing.Common is functional, so DeserializingResourceReader hands back
        // LIVE System.Drawing.Icon/Bitmap/Font objects for graphics entries -- but designer code (and
        // every migrated property) is typed against Majorsilence.Forms.Drawing, so an unconditional
        // `(Icon) resources.GetObject ("$this.Icon")` cast throws InvalidCastException (found via a
        // real migrated login form's window icon). Convert them here, via reflection: this assembly
        // deliberately does not reference System.Drawing.Common, and off-Windows those entries fail
        // deserialization long before reaching this point (see the per-entry catch above).
        [UnconditionalSuppressMessage ("Trimming", "IL2075",
            Justification = "Reflection targets are System.Drawing.Common members (Icon.Save/Bitmap.Save/Font.Name...), reached only when that assembly materialized the value at runtime -- if a trimmer removed them, deserialization above could not have produced the object in the first place; every miss degrades to null (coded default).")]
        [UnconditionalSuppressMessage ("Trimming", "IL2026",
            Justification = "Assembly.GetType(\"System.Drawing.Imaging.ImageFormat\") resolves against the assembly that just produced a live System.Drawing.Bitmap; same reasoning as IL2075 above -- absence degrades to null.")]
        internal static object? NormalizeDeserialized (object? value)
        {
            if (value is null)
                return null;

            var type = value.GetType ();
            try
            {
                // A stand-in from the embedded shim: it never decoded anything, it just carried the
                // original file across, so build the real image straight from those bytes. Checked
                // before the switch below because the stand-ins share their FullNames with the live
                // System.Drawing types that switch is written for.
                if (TryGetShimImageBytes (value, out var shimBytes))
                    return BuildImage (type.FullName, shimBytes);

                // The font stand-in, likewise: it carried the converter string across undecoded, and
                // ParseFont is the same parser a font written straight into .resx XML goes through.
                if (TryGetShimFontSpec (value, out var fontSpec))
                    return ParseFont (fontSpec);

                switch (type.FullName)
                {
                    case "System.Drawing.Icon":
                    {
                        // Icon.Save(Stream) writes the original .ico bytes back out.
                        using var ms = new MemoryStream ();
                        type.GetMethod ("Save", new[] { typeof (Stream) })?.Invoke (value, new object[] { ms });
                        if (ms.Length == 0)
                            return null;
                        ms.Position = 0;
                        return new Majorsilence.Forms.Drawing.Icon (ms);
                    }
                    case "System.Drawing.Bitmap":
                    {
                        // Bitmap.Save(Stream, ImageFormat) with PNG preserves alpha.
                        var imageFormatType = type.Assembly.GetType ("System.Drawing.Imaging.ImageFormat");
                        var png = imageFormatType?.GetProperty ("Png")?.GetValue (null);
                        var save = imageFormatType is null ? null : type.GetMethod ("Save", new[] { typeof (Stream), imageFormatType });
                        if (png is null || save is null)
                            return null;
                        using var ms = new MemoryStream ();
                        save.Invoke (value, new[] { (object) ms, png });
                        return Majorsilence.Forms.Drawing.Image.FromBytes (ms.ToArray ());
                    }
                    case "System.Drawing.Font":
                    {
                        var family = (string) type.GetProperty ("Name")!.GetValue (value)!;
                        var size = (float) type.GetProperty ("Size")!.GetValue (value)!;
                        // FontStyle flag values match System.Drawing's by design; bridge by integer.
                        var style = System.Convert.ToInt32 (type.GetProperty ("Style")!.GetValue (value)!, CultureInfo.InvariantCulture);
                        return new Majorsilence.Forms.Drawing.Font (family, size, (Majorsilence.Forms.Drawing.FontStyle) style);
                    }
                }
            }
            catch
            {
                // An unusable graphics payload: treat as absent so the coded default wins -- returning
                // the raw System.Drawing object would just recreate the InvalidCastException downstream.
                return null;
            }

            return value;
        }

        // ── resx parsing ──────────────────────────────────────────────────────────────────────

        private sealed record ResxEntry (string? TypeName, string? MimeType, string RawValue, string? BaseDirectory);

        private void Load (string xml, string? baseDirectory = null)
        {
            XDocument doc;
            try { doc = XDocument.Parse (xml); }
            catch { return; }   // a malformed resx leaves an empty manager rather than throwing.

            foreach (var data in doc.Descendants ("data"))
            {
                var name = (string?) data.Attribute ("name");
                if (name is null)
                    continue;
                _entries[name] = new ResxEntry (
                    TypeName: (string?) data.Attribute ("type"),
                    MimeType: (string?) data.Attribute ("mimetype"),
                    RawValue: data.Element ("value")?.Value ?? string.Empty,
                    BaseDirectory: baseDirectory);
            }
        }

        // Turns a raw resx entry into a live object: string, primitive, or Majorsilence.Forms.Drawing image.
        private static object? Materialize (ResxEntry entry)
        {
            // A file-linked entry: the value is "<relative path>;<type>[;<encoding>]" and the real
            // payload lives in a separate file next to the .resx. This is what Visual Studio writes
            // for every image dragged into a Resources.resx, so without it the common case of a
            // strongly-typed Resources.Play comes back as that "path;type" string rather than a
            // picture -- and the generated `(Bitmap) obj` cast then throws.
            if (IsFileRef (entry.TypeName))
                return MaterializeFileRef (entry);

            // A serialized payload (image bytes, or a BinaryFormatter blob).
            if (!string.IsNullOrEmpty (entry.MimeType))
            {
                // BinaryFormatter / SOAP — we don't run BinaryFormatter, but we can recover the common
                // image cases from the NRBF wire format (System.Drawing.Bitmap/Icon/ImageListStreamer).
                if (entry.MimeType.Contains ("binary.base64", StringComparison.OrdinalIgnoreCase) ||
                    entry.MimeType.Contains ("soap.base64", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryDecodeBase64 (entry.RawValue, out var blob))
                        return null;
                    return NrbfResourceReader.TryReadObject (blob);   // null if it's not a type we handle
                }

                // bytearray.base64 — the payload is raw image/file bytes.
                if (entry.MimeType.Contains ("bytearray.base64", StringComparison.OrdinalIgnoreCase))
                {
                    return TryDecodeBase64 (entry.RawValue, out var bytes)
                        ? BuildImage (entry.TypeName, bytes)
                        : null;
                }
                return null;
            }

            // An inline, type-converter-formatted value.
            return entry.TypeName is null
                ? entry.RawValue                              // no type => plain string-table entry
                : ParsePrimitive (LeadingType (entry.TypeName), entry.RawValue.Trim ());
        }

        private static bool TryDecodeBase64 (string value, out byte[] bytes)
        {
            try { bytes = System.Convert.FromBase64String (value.Trim ()); return true; }
            catch { bytes = Array.Empty<byte> (); return false; }
        }

        // "System.Resources.ResXFileRef, System.Windows.Forms" -- the only type whose value is a
        // pointer to another file rather than the data itself.
        private static bool IsFileRef (string? typeName)
            => LeadingType (typeName).Equals ("System.Resources.ResXFileRef", StringComparison.Ordinal);

        // The value of a file-linked entry is "<path>;<type name>[;<encoding>]", e.g.
        // "..\Resources\play.png;System.Drawing.Bitmap, System.Drawing, Version=2.0.0.0, ...".
        // The type name itself contains commas but never a semicolon, so splitting on ';' is safe;
        // a path containing one is not representable in this format to begin with.
        private static object? MaterializeFileRef (ResxEntry entry)
        {
            var parts = entry.RawValue.Split (';');
            if (parts.Length == 0)
                return null;

            var path = ResolveFileRefPath (parts[0].Trim (), entry.BaseDirectory);
            if (path is null)
                return null;

            var targetType = parts.Length > 1 ? parts[1].Trim () : null;

            byte[] bytes;
            try { bytes = File.ReadAllBytes (path); }
            catch { return null; }   // a link to a file that isn't there degrades to null, as an absent entry would.

            var leading = LeadingType (targetType);

            if (leading.Equals ("System.String", StringComparison.Ordinal))
            {
                // parts[2], when present, names the text encoding the file was written in.
                var encoding = System.Text.Encoding.UTF8;
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace (parts[2]))
                {
                    try { encoding = System.Text.Encoding.GetEncoding (parts[2].Trim ()); }
                    catch { /* an unknown encoding name falls back to UTF-8. */ }
                }
                try { return encoding.GetString (bytes); }
                catch { return null; }
            }

            if (leading.Equals ("System.Byte[]", StringComparison.Ordinal))
                return bytes;

            return BuildImage (targetType, bytes);
        }

        // File refs are written with the separator of whatever machine last saved the .resx -- almost
        // always Windows -- so a "..\Resources\play.png" has to be re-separated before it will open
        // anywhere else.
        private static string? ResolveFileRefPath (string reference, string? baseDirectory)
        {
            if (string.IsNullOrEmpty (reference))
                return null;

            var normalized = reference.Replace ('\\', Path.DirectorySeparatorChar)
                                      .Replace ('/', Path.DirectorySeparatorChar);

            try
            {
                return string.IsNullOrEmpty (baseDirectory) || Path.IsPathRooted (normalized)
                    ? normalized
                    : Path.GetFullPath (Path.Combine (baseDirectory, normalized));
            }
            catch { return null; }   // a reference that isn't a usable path at all.
        }

        private static object? BuildImage (string? typeName, byte[] bytes)
        {
            var type = LeadingType (typeName);
            try
            {
                if (type.EndsWith ("Icon", StringComparison.Ordinal))
                    return new Majorsilence.Forms.Drawing.Icon (new MemoryStream (bytes));
                return Majorsilence.Forms.Drawing.Image.FromBytes (bytes);
            }
            catch { return null; }
        }

        // The bare type name, dropping the assembly-qualified tail: "System.Drawing.Size, ..." -> "System.Drawing.Size".
        private static string LeadingType (string? typeName)
        {
            if (string.IsNullOrEmpty (typeName))
                return string.Empty;
            var comma = typeName.IndexOf (',');
            return (comma < 0 ? typeName : typeName[..comma]).Trim ();
        }

        private static object? ParsePrimitive (string type, string value)
        {
            try
            {
                switch (type)
                {
                    case "System.String": return value;
                    case "System.Boolean": return bool.Parse (value);
                    case "System.Int32": return int.Parse (value, CultureInfo.InvariantCulture);
                    case "System.Int64": return long.Parse (value, CultureInfo.InvariantCulture);
                    case "System.Single": return float.Parse (value, CultureInfo.InvariantCulture);
                    case "System.Double": return double.Parse (value, CultureInfo.InvariantCulture);
                    case "System.Drawing.Point":
                    {
                        var p = ParsePoint (value);
                        return p.HasValue ? new System.Drawing.Point (p.Value.Item1, p.Value.Item2) : value;
                    }
                    case "System.Drawing.Size":
                    {
                        var s = ParsePoint (value);
                        return s.HasValue ? new System.Drawing.Size (s.Value.Item1, s.Value.Item2) : value;
                    }
                    case "System.Drawing.Color": return ParseColor (value);
                    case "System.Drawing.Font": return ParseFont (value);
                    default: return value;   // unknown type: hand back the raw string, best-effort.
                }
            }
            catch { return value; }
        }

        // Comma-split + trim each entry. Replaces value.Split(',', StringSplitOptions.TrimEntries [|
        // RemoveEmptyEntries]) -- the char overload of Split and StringSplitOptions.TrimEntries are both
        // post-netstandard2.0 additions.
        private static string[] SplitTrimmed (string value, bool removeEmpty = true)
        {
            var parts = value.Split (',').Select (p => p.Trim ());
            if (removeEmpty)
                parts = parts.Where (p => p.Length > 0);
            return parts.ToArray ();
        }

        private static (int, int)? ParsePoint (string value)
        {
            var parts = SplitTrimmed (value);
            return parts.Length == 2
                   && int.TryParse (parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
                   && int.TryParse (parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
                ? (a, b)
                : null;
        }

        private static System.Drawing.Color ParseColor (string value)
        {
            var parts = SplitTrimmed (value);
            if (parts.Length is 3 or 4 && parts.All (p => byte.TryParse (p, out _)))
            {
                var c = parts.Select (byte.Parse).ToArray ();
                return parts.Length == 4
                    ? System.Drawing.Color.FromArgb (c[0], c[1], c[2], c[3])
                    : System.Drawing.Color.FromArgb (c[0], c[1], c[2]);
            }
            return System.Drawing.Color.FromName (value);   // a named colour (e.g. "Red", "ControlText").
        }

        private static Majorsilence.Forms.Drawing.Font ParseFont (string value)
        {
            // Format: "Family, 8.25pt[, style=Bold, Italic]".
            var parts = SplitTrimmed (value, removeEmpty: false);
            var family = parts.Length > 0 ? parts[0] : "Microsoft Sans Serif";
            var size = 8.25f;
            if (parts.Length > 1)
                float.TryParse (parts[1].Replace ("pt", "", StringComparison.OrdinalIgnoreCase).Trim (),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out size);

            var style = Majorsilence.Forms.Drawing.FontStyle.Regular;
            var styleText = value.Contains ("style=", StringComparison.OrdinalIgnoreCase)
                ? value[(value.IndexOf ("style=", StringComparison.OrdinalIgnoreCase) + 6)..]
                : string.Empty;
            if (styleText.Contains ("Bold", StringComparison.OrdinalIgnoreCase)) style |= Majorsilence.Forms.Drawing.FontStyle.Bold;
            if (styleText.Contains ("Italic", StringComparison.OrdinalIgnoreCase)) style |= Majorsilence.Forms.Drawing.FontStyle.Italic;
            if (styleText.Contains ("Underline", StringComparison.OrdinalIgnoreCase)) style |= Majorsilence.Forms.Drawing.FontStyle.Underline;
            if (styleText.Contains ("Strikeout", StringComparison.OrdinalIgnoreCase)) style |= Majorsilence.Forms.Drawing.FontStyle.Strikeout;

            return new Majorsilence.Forms.Drawing.Font (family, size, style);
        }

        // ── reflection conversion for ApplyResources ─────────────────────────────────────────

        private static bool TryConvert (object? value, Type target, out object? result)
        {
            result = null;
            if (value is null)
                return false;

            var underlying = Nullable.GetUnderlyingType (target) ?? target;

            if (underlying.IsInstanceOfType (value))
            {
                result = value;
                return true;
            }

            try
            {
                if (underlying.IsEnum && value is string s)
                {
                    result = Enum.Parse (underlying, s, ignoreCase: true);
                    return true;
                }
                // A resolved-but-wrong-type enum value (see RegisterWinFormsEnumResolver): the
                // control's real property is e.g. Majorsilence.Forms.DockStyle, but value is a
                // System.Windows.Forms.DockStyle instance from the shim assembly. Bridge by
                // underlying integer -- the two enums are deliberately kept value-compatible.
                if (underlying.IsEnum && value is Enum && value.GetType () != underlying)
                {
                    result = Enum.ToObject (underlying, System.Convert.ToInt64 (value, CultureInfo.InvariantCulture));
                    return true;
                }
                if (value is string str && underlying != typeof (string))
                {
                    result = System.Convert.ChangeType (str, underlying, CultureInfo.InvariantCulture);
                    return true;
                }
                if (value is IConvertible)
                {
                    result = System.Convert.ChangeType (value, underlying, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch { /* not convertible — skip this property. */ }

            return false;
        }

        // ── resx discovery for the (Type) constructor ────────────────────────────────────────

        // Returns the .resx XML together with the directory file-linked entries resolve against —
        // null for an embedded copy, whose ResXFileRef paths point into the source tree that built it
        // and so can't be followed at runtime.
        private static (string Xml, string? Directory)? LocateResx (Type type)
        {
            var assembly = type.Assembly;

            // 1. A raw .resx embedded with a logical name ending in "<FullName>.resx" or "<Name>.resx".
            var resourceName = assembly.GetManifestResourceNames ()
                .FirstOrDefault (n => n.EndsWith (type.FullName + ".resx", StringComparison.Ordinal)
                                   || n.EndsWith ("." + type.Name + ".resx", StringComparison.Ordinal));
            if (resourceName is not null)
            {
                using var stream = assembly.GetManifestResourceStream (resourceName);
                if (stream is not null)
                {
                    using var reader = new StreamReader (stream);
                    return (reader.ReadToEnd (), null);
                }
            }

            // 2. A .resx on disk beside the assembly or under the app base directory.
            // Assembly.Location is the empty string for a single-file app (IL3000) -- harmless here, the
            // IsNullOrEmpty filter on the next line drops it and AppContext.BaseDirectory still applies.
            [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage ("SingleFile", "IL3000",
                Justification = "An empty Location is filtered out immediately below; BaseDirectory covers the single-file case.")]
            static string? AssemblyDir (Assembly a) => Path.GetDirectoryName (a.Location);

            var dirs = new[] { AssemblyDir (assembly), AppContext.BaseDirectory }
                .Where (d => !string.IsNullOrEmpty (d))
                .Distinct ();
            var candidates = new[] { type.FullName + ".resx", type.Name + ".resx" }
                .Where (c => c is not null);

            foreach (var dir in dirs)
                foreach (var candidate in candidates)
                {
                    var path = Path.Combine (dir!, candidate!);
                    if (File.Exists (path))
                        return (File.ReadAllText (path), dir);
                }

            return null;
        }
    }
}
