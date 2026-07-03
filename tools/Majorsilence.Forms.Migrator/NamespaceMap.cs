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
    [
        // Telerik UI for WinForms -> the Majorsilence.Forms.Telerik compat layer (src/Majorsilence.Forms/Telerik/*.cs).
        // All of it — controls (Telerik.WinControls.UI), their enums (Telerik.WinControls.Enumerations),
        // docking (.UI.Docking), grid data (.UI.Data / .Data), and the bare root namespace itself — collapses
        // to the same flat target; the import-dedup pass in SourceConverter removes the resulting duplicate
        // `using`. Listed longest-first (an entry must precede any entry that is its dotted extension), with
        // the bare `Telerik.WinControls` last so it never clips a more specific sub-namespace first.
        ("Telerik.WinControls.UI.Docking", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinControls.UI.Data", "Majorsilence.Forms.Telerik"),
        // The rich text editor's ribbon lives in its own sub-namespace of Telerik.WinControls.UI (its leaf
        // type, RichTextEditorRibbonTab, is reached via Telerik.WinControls.UI.RichTextEditorRibbonUI in
        // designer code) — must precede the bare Telerik.WinControls.UI entry below so that entry doesn't
        // clip it into a nonexistent Majorsilence.Forms.Telerik.RichTextEditorRibbonUI.* namespace first.
        ("Telerik.WinControls.UI.RichTextEditorRibbonUI", "Majorsilence.Forms.Telerik"),
        // The grid export surface (GridViewSpreadExport, ExportToCSV, ExportToHTML, GridViewPdfExport, ...)
        // lives partly under Telerik.WinControls.UI.Export and partly under the sibling Telerik.WinControls.Export
        // — both collapse to the same flat compat layer (src/Majorsilence.Forms/Telerik/RadGridExport.cs).
        // Both must precede the bare Telerik.WinControls.UI / Telerik.WinControls entries below: without this,
        // the bare Telerik.WinControls rule fires first and clips these into a nonexistent
        // Majorsilence.Forms.Telerik.Export.* namespace (the bug this phase fixes — see
        // SourceConverterTests' regression test for it).
        ("Telerik.WinControls.UI.Export", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinControls.Export", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinControls.Enumerations", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinControls.UI", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinControls.Data", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinControls", "Majorsilence.Forms.Telerik"),
        // Telerik's document model / HTML format provider / proofing namespaces (used by RadRichTextEditor's
        // HtmlFormatProvider, HtmlExportSettings, ISpellChecker/DocumentSpellChecker — see
        // src/Majorsilence.Forms/Telerik/RadRichTextEditor.cs) collapse to the same flat compat layer.
        // Ordered longest-first for the same reason as the WinControls group above, with the bare
        // Telerik.WinForms.Documents last so it never clips its own more specific sub-namespaces.
        ("Telerik.WinForms.Documents.Model", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinForms.Documents.FormatProviders.Html", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinForms.Documents.Proofing", "Majorsilence.Forms.Telerik"),
        ("Telerik.WinForms.Documents", "Majorsilence.Forms.Telerik"),
        ("System.Drawing.Drawing2D", "Majorsilence.Drawing.Drawing2D"),
        ("System.Drawing.Imaging", "Majorsilence.Drawing.Imaging"),
        ("System.Drawing.Text", "Majorsilence.Drawing.Text"),
        ("System.Drawing.Printing", "Majorsilence.Forms.Printing"),
        ("System.Windows.Forms", "Majorsilence.Forms"),
    ];

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
    [
        "System.Windows.Forms.VisualStyles",
        "System.Drawing.Design",
        "System.ComponentModel.Design",
        "Telerik.WinControls.Themes",
        "Telerik.WinControls.Design",
        "Telerik.WinControls.Primitives",
        "Telerik.WinControls.Layouts",
    ];

    /// <summary>The <c>Telerik.WinControls.UI</c> namespace, used to qualify the leaf names in <see cref="UnmappedTelerikTypes"/>.</summary>
    public const string TelerikUiNamespace = "Telerik.WinControls.UI";

    /// <summary>
    /// Telerik types with no Majorsilence.Forms.Telerik equivalent. PDF (<c>RadPdfViewer</c>/
    /// <c>RadPdfViewerNavigator</c>), rich text (<c>RadRichTextEditor</c>/<c>RichTextEditorRibbonBar</c>/
    /// <c>RadRibbonBar</c>/...), desktop alerts (<c>RadDesktopAlert</c>), and the scheduler data/printing
    /// surface (<c>SchedulerBindingDataSource</c>, <c>AppointmentMappingInfo</c>, <c>ResourceMappingInfo</c>,
    /// <c>RadPrintDocument</c>, <c>RadPrintWatermark</c>, and the <c>Scheduler*PrintStyle</c> family) are no
    /// longer listed here — all now have compat implementations in
    /// <c>src/Majorsilence.Forms/Telerik/RadPdfViewer.cs</c>, <c>RadRichTextEditor.cs</c>/
    /// <c>RadRichTextEditorRibbon.cs</c>, <c>RadDesktopAlert.cs</c>, and <c>RadSchedulerData.cs</c>/
    /// <c>RadScheduler.cs</c>/<c>RadSchedulerPrinting.cs</c> respectively (Phase 5). This set is now empty —
    /// kept (rather than deleted) as the designated home for any future heavyweight Telerik type found to
    /// have no compat implementation yet; see Pass 5b in <see cref="SourceConverter"/> for how a reference
    /// to a type listed here is left unrewritten rather than being pointed at a type that doesn't exist.
    /// </summary>
    public static readonly HashSet<string> UnmappedTelerikTypes = new(StringComparer.Ordinal)
    {
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
