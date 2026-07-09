using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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
            ArgumentNullException.ThrowIfNull (resourceSource);

            LoadCompiledResources (resourceSource);

            var xml = LocateResx (resourceSource);
            if (xml is not null)
                Load (xml);
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
            ArgumentNullException.ThrowIfNull (baseName);
            ArgumentNullException.ThrowIfNull (assembly);

            LoadCompiledResources (assembly, baseName);
        }

        /// <summary>Builds a resource manager from a <c>.resx</c> file on disk.</summary>
        public static ComponentResourceManager FromFile (string path)
        {
            var mgr = new ComponentResourceManager ();
            mgr.Load (File.ReadAllText (path));
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
        public void ApplyResources (object value, string objectName)
        {
            ArgumentNullException.ThrowIfNull (value);
            ArgumentNullException.ThrowIfNull (objectName);

            var prefix = objectName + ".";
            var type = value.GetType ();

            foreach (var (name, raw) in EnumerateWithPrefix (prefix))
            {
                var propertyName = name[prefix.Length..];
                // Skip designer bookkeeping keys that aren't simple settable properties.
                if (propertyName.Contains ('.'))
                    continue;

                var property = type.GetProperty (propertyName,
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

            using var stream = assembly.GetManifestResourceStream (resourceName);
            if (stream is null)
                return;

            System.Resources.Extensions.DeserializingResourceReader reader;
            try { reader = new System.Resources.Extensions.DeserializingResourceReader (stream); }
            catch { return; }   // not the preserialized format this reader expects -- leave empty.

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

                    try
                    {
                        var name = (string) enumerator.Key;
                        _binaryEntries[name] = NormalizeDeserialized (enumerator.Value);
                    }
                    catch { /* this entry's type couldn't be resolved/deserialized -- skip it, keep going. */ }
                }
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

            AssemblyLoadContext.Default.Resolving += (_, name) =>
                name.Name == "System.Windows.Forms" ? shimAssembly : null;
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

        private sealed record ResxEntry (string? TypeName, string? MimeType, string RawValue);

        private void Load (string xml)
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
                    RawValue: data.Element ("value")?.Value ?? string.Empty);
            }
        }

        // Turns a raw resx entry into a live object: string, primitive, or Majorsilence.Forms.Drawing image.
        private static object? Materialize (ResxEntry entry)
        {
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

        private static (int, int)? ParsePoint (string value)
        {
            var parts = value.Split (',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2
                   && int.TryParse (parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
                   && int.TryParse (parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
                ? (a, b)
                : null;
        }

        private static System.Drawing.Color ParseColor (string value)
        {
            var parts = value.Split (',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
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
            var parts = value.Split (',', StringSplitOptions.TrimEntries);
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

        private static string? LocateResx (Type type)
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
                    return reader.ReadToEnd ();
                }
            }

            // 2. A .resx on disk beside the assembly or under the app base directory.
            var dirs = new[] { Path.GetDirectoryName (assembly.Location), AppContext.BaseDirectory }
                .Where (d => !string.IsNullOrEmpty (d))
                .Distinct ();
            var candidates = new[] { type.FullName + ".resx", type.Name + ".resx" }
                .Where (c => c is not null);

            foreach (var dir in dirs)
                foreach (var candidate in candidates)
                {
                    var path = Path.Combine (dir!, candidate!);
                    if (File.Exists (path))
                        return File.ReadAllText (path);
                }

            return null;
        }
    }
}
