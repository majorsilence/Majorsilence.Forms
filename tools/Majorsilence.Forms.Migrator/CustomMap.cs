using System.Text.Json;

namespace Majorsilence.Forms.Migrator;

/// <summary>
/// User-supplied namespace rewrites layered on top of the built-in WinForms/GDI+ rules. This is how a
/// project brings its third-party controls across — e.g. mapping <c>Telerik.WinControls.UI</c> onto the
/// <c>Majorsilence.Forms.Telerik</c> compat layer — without the tool having to bake in every vendor.
///
/// File format (JSON):
/// <code>
/// {
///   "namespaces": {
///     "Telerik.WinControls.UI": "Majorsilence.Forms.Telerik",
///     "DevExpress.XtraEditors":  "Majorsilence.Forms.DevExpress"
///   }
/// }
/// </code>
/// </summary>
internal sealed class CustomMap
{
    /// <summary>Whole-namespace prefix rewrites, ordered longest-first so nested prefixes win.</summary>
    public IReadOnlyList<(string From, string To)> Namespaces { get; }

    private CustomMap(IReadOnlyList<(string, string)> namespaces) => Namespaces = namespaces;

    public static readonly CustomMap Empty = new(Array.Empty<(string, string)>());

    private sealed class Schema
    {
        public Dictionary<string, string>? Namespaces { get; set; }
    }

    public static CustomMap Load(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
            return Empty;

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

        foreach (var file in files)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"map file not found: {file}");

            Schema? schema;
            try
            {
                schema = JsonSerializer.Deserialize<Schema>(File.ReadAllText(file), jsonOptions);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"invalid map file '{file}': {ex.Message}", ex);
            }

            if (schema?.Namespaces is null)
                continue;

            foreach (var (from, to) in schema.Namespaces)
                merged[from] = to; // later files override earlier ones
        }

        var ordered = merged
            .Select(kvp => (kvp.Key, kvp.Value))
            .OrderByDescending(m => m.Key.Length)
            .ToList();

        return new CustomMap(ordered);
    }
}
