namespace Majorsilence.Forms.Migrator;

/// <summary>
/// The rules that move WinForms / GDI+ source onto the Majorsilence.Forms surface.
///
/// Two important asymmetries drive the design:
/// <list type="bullet">
///   <item><c>System.Windows.Forms</c> maps wholesale to <c>Majorsilence.Forms</c>, which exposes a
///   WinForms-shaped API under its own namespace.</item>
///   <item><c>System.Drawing</c> is <b>split</b>. The primitive value types (Color, Point, Size,
///   Rectangle, …) live in <c>System.Drawing.Primitives</c>, ship with the base framework on every
///   OS, and Majorsilence.Forms keeps using them as-is — so they must <b>not</b> be rewritten. The GDI+
///   types (Bitmap, Brush, Pen, Font, …) are Windows-only and are reimplemented under
///   <c>Majorsilence.Drawing</c>, so those <b>are</b> rewritten.</item>
/// </list>
/// </summary>
internal static class NamespaceMap
{
    /// <summary>
    /// Whole-namespace prefix rewrites that are unambiguous regardless of the type that follows.
    /// Ordered longest-first so a sub-namespace is handled before its parent prefix can clip it.
    /// </summary>
    public static readonly (string From, string To)[] NamespacePrefixes =
    {
        ("System.Drawing.Drawing2D", "Majorsilence.Drawing.Drawing2D"),
        ("System.Drawing.Imaging", "Majorsilence.Drawing.Imaging"),
        ("System.Drawing.Text", "Majorsilence.Drawing.Text"),
        ("System.Drawing.Printing", "Majorsilence.Forms.Printing"),
        ("System.Windows.Forms", "Majorsilence.Forms"),
    };

    /// <summary>
    /// <c>System.Drawing</c> primitive value types that Majorsilence.Forms keeps verbatim. A
    /// fully-qualified reference to one of these is left untouched.
    /// </summary>
    public static readonly HashSet<string> DrawingPrimitives = new(StringComparer.Ordinal)
    {
        "Color", "Point", "PointF", "Size", "SizeF", "Rectangle", "RectangleF",
    };

    /// <summary>
    /// Top-level GDI+ types that Majorsilence.Drawing reimplements. A fully-qualified
    /// <c>System.Drawing.&lt;name&gt;</c> reference to one of these is rewritten to
    /// <c>Majorsilence.Drawing.&lt;name&gt;</c>. Kept in sync with <c>src/Majorsilence.Forms/Drawing/*.cs</c>.
    /// </summary>
    public static readonly HashSet<string> MajorsilenceDrawingTypes = new(StringComparer.Ordinal)
    {
        "Bitmap", "Brush", "Brushes", "CompositingMode", "CompositingQuality", "DashStyle", "FillMode",
        "Font", "FontFamily", "FontStyle", "GraphicsPath", "GraphicsState", "GraphicsUnit",
        "HatchBrush", "HatchStyle", "HotkeyPrefix", "Icon", "Image", "ImageFormat",
        "ImageLockMode", "InterpolationMode", "LinearGradientBrush", "LineCap", "LineJoin",
        "Matrix", "MatrixOrder", "Pen", "Pens", "PixelFormat", "PixelOffsetMode",
        "PathGradientBrush", "Region", "RotateFlipType", "SmoothingMode", "SolidBrush",
        "StringAlignment", "StringFormat", "StringFormatFlags", "StringTrimming", "TextureBrush",
        "TextRenderingHint", "WrapMode", "SystemIcons", "ImageAnimator", "ColorConverter",
        "BufferedGraphics", "BufferedGraphicsContext", "BufferedGraphicsManager",
    };

    /// <summary>
    /// Namespaces with no Majorsilence equivalent. References are flagged for manual review and left
    /// untouched rather than being rewritten into something that does not exist.
    /// </summary>
    public static readonly string[] UnsupportedNamespaces =
    {
        "System.Windows.Forms.VisualStyles",
        "System.Drawing.Design",
        "System.ComponentModel.Design",
    };

    /// <summary>
    /// <c>System.Drawing</c> types that Majorsilence reimplements in the <b><c>Majorsilence.Forms</c></b>
    /// namespace (its WinForms-compat surface) rather than <c>Majorsilence.Drawing</c>. A fully-qualified
    /// <c>System.Drawing.&lt;name&gt;</c> is rewritten to <c>Majorsilence.Forms.&lt;name&gt;</c>. Verified
    /// against the type declarations in <c>src/Majorsilence.Forms/*.cs</c>.
    /// </summary>
    public static readonly HashSet<string> MajorsilenceFormsTypes = new(StringComparer.Ordinal)
    {
        "Graphics", "ContentAlignment", "ColorTranslator",
        "SystemColors", "SystemBrushes", "SystemPens", "SystemFonts",
    };

    /// <summary>
    /// High-signal <c>System.Drawing</c> top-level types from the Windows-only <c>System.Drawing.Common</c>
    /// that have <b>no</b> Majorsilence replacement (in either namespace). When one is used <i>unqualified</i>
    /// under a <c>using System.Drawing;</c>, the textual rewrite can't see it, so we name-match it to warn —
    /// they would otherwise be silent compile breaks. The names are distinctive enough (nobody calls a local
    /// <c>TextureBrush</c>) that false positives are unlikely.
    /// </summary>
    public static readonly HashSet<string> UnmappedDrawingTypes = new(StringComparer.Ordinal)
    {
        "Metafile", "MetafileHeader", "ImageAttributes", "ColorMatrix", "ColorMap",
        "Encoder", "EncoderParameter", "EncoderParameters", "CharacterRange",
    };

    /// <summary>The namespace the GDI+ replacements live in; added alongside a kept <c>System.Drawing</c> import.</summary>
    public const string DrawingTarget = "Majorsilence.Drawing";
}
