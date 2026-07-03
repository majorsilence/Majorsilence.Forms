using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// cannot be read on modern .NET; <see cref="GetObject"/> returns <see langword="null"/> for those
    /// (the migrator flags them for manual re-export).
    /// </summary>
    public class ComponentResourceManager
    {
        // name -> raw resx entry (we parse lazily on first access so an unused entry never costs work).
        private readonly Dictionary<string, ResxEntry> _entries = new (StringComparer.Ordinal);

        /// <summary>Creates an empty resource manager (no backing <c>.resx</c>).</summary>
        public ComponentResourceManager () { }

        /// <summary>
        /// Locates the <c>.resx</c> associated with <paramref name="resourceSource"/> — first an embedded
        /// raw <c>&lt;FullName&gt;.resx</c> in the type's assembly, then a <c>.resx</c> on disk beside the
        /// assembly or under the app base directory. If none is found the manager is simply empty, so
        /// designer code still runs (controls keep their coded defaults).
        /// </summary>
        public ComponentResourceManager (Type resourceSource)
        {
            ArgumentNullException.ThrowIfNull (resourceSource);
            var xml = LocateResx (resourceSource);
            if (xml is not null)
                Load (xml);
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
            => _entries.TryGetValue (name, out var e) ? e.RawValue : null;

        /// <summary>
        /// Returns the resource named <paramref name="name"/>: a string, a framework primitive
        /// (<c>Point</c>/<c>Size</c>/<c>Color</c>/<c>bool</c>/<c>int</c>/…), or a
        /// <see cref="Majorsilence.Forms.Drawing"/> image/icon. Returns null for absent or unreadable entries.
        /// </summary>
        public object? GetObject (string name)
            => _entries.TryGetValue (name, out var e) ? Materialize (e) : null;

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

            foreach (var (name, entry) in _entries)
            {
                if (!name.StartsWith (prefix, StringComparison.Ordinal))
                    continue;

                var propertyName = name[prefix.Length..];
                // Skip designer bookkeeping keys that aren't simple settable properties.
                if (propertyName.Contains ('.'))
                    continue;

                var property = type.GetProperty (propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null || !property.CanWrite)
                    continue;

                if (TryConvert (Materialize (entry), property.PropertyType, out var converted))
                {
                    try { property.SetValue (value, converted); }
                    catch { /* a property that rejects the value is non-fatal — keep applying the rest. */ }
                }
            }
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
