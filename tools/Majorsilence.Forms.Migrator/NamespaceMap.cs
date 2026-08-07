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
///   <c>Majorsilence.Forms.Drawing</c>, so those <b>are</b> rewritten.</item>
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
        ("System.Drawing.Drawing2D", "Majorsilence.Forms.Drawing.Drawing2D"),
        ("System.Drawing.Imaging", "Majorsilence.Forms.Drawing.Imaging"),
        ("System.Drawing.Text", "Majorsilence.Forms.Drawing.Text"),
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
    /// Top-level GDI+ types that Majorsilence.Forms.Drawing reimplements. A fully-qualified
    /// <c>System.Drawing.&lt;name&gt;</c> reference to one of these is rewritten to
    /// <c>Majorsilence.Forms.Drawing.&lt;name&gt;</c>, and — for the unqualified-under-a-bare-<c>using
    /// System.Drawing;</c> heuristic in <c>SourceConverter.RewriteDrawingImports</c> — its presence adds the
    /// <c>Majorsilence.Forms.Drawing</c> companion import. That heuristic is deliberately approximate for the
    /// handful of these that actually live one level down, in <c>Majorsilence.Forms.Drawing.Imaging</c>
    /// (<c>ImageAttributes</c>, <c>ColorMatrix</c>, <c>ColorMap</c>, <c>Encoder</c>, <c>EncoderParameter</c>,
    /// <c>EncoderParameters</c>, <c>BitmapData</c>) or <c>.Drawing2D</c>/<c>.Text</c> (already true of
    /// <c>HotkeyPrefix</c> before this comment was updated) — real source that uses one of those unqualified
    /// under only a bare <c>System.Drawing</c> import (no matching sub-namespace import at all) wouldn't have
    /// compiled pre-migration either, so the imprecision is harmless in practice. Kept in sync with
    /// <c>src/Majorsilence.Forms.Drawing.Common/*.cs</c> (the consolidated drawing project — see
    /// <c>COMPATIBILITY_MATRIX.md</c>'s "System.Drawing / GDI+" section).
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
        // Added once these gained real implementations (see COMPATIBILITY_MATRIX.md's GDI+ surface audit):
        "CharacterRange", "ImageAttributes", "ColorMatrix", "ColorMap",
        "Encoder", "EncoderParameter", "EncoderParameters", "BitmapData",
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

    /// <summary>
    /// <c>Microsoft.Win32</c> types that compile on every platform but only <i>work</i> on Windows, so
    /// nothing in the build flags them and the failure lands at runtime instead — the registry has no
    /// portable substitute, and off Windows these return null or throw. Warned about (gated on the file
    /// actually referencing <c>Microsoft.Win32</c>, so a project's own <c>Registry</c> class isn't
    /// mistaken for one) rather than rewritten, because there is nothing to rewrite them to: a
    /// run-at-startup entry or a settings hive has to be re-homed by hand per target platform.
    /// </summary>
    public static readonly string[] WindowsOnlyRegistryTypes =
    [
        "Registry", "RegistryKey", "RegistryHive", "RegistryValueKind", "RegistryView",
    ];

    /// <summary>The namespace the types in <see cref="WindowsOnlyRegistryTypes"/> live in.</summary>
    public const string WindowsRegistryNamespace = "Microsoft.Win32";

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
    /// namespace (its WinForms-compat surface) rather than <c>Majorsilence.Forms.Drawing</c>. A fully-qualified
    /// <c>System.Drawing.&lt;name&gt;</c> is rewritten to <c>Majorsilence.Forms.&lt;name&gt;</c>. Verified
    /// against the type declarations in <c>src/Majorsilence.Forms/*.cs</c>.
    /// </summary>
    public static readonly HashSet<string> MajorsilenceFormsTypes = new(StringComparer.Ordinal)
    {
        "Graphics", "ContentAlignment", "ColorTranslator",
        "SystemColors", "SystemBrushes", "SystemPens", "SystemFonts",
    };

    /// <summary>
    /// The subset of <see cref="MajorsilenceFormsTypes"/> that also ships in <c>System.Drawing.Primitives</c>,
    /// i.e. is still resolvable through a kept <c>using System.Drawing;</c> after the migration. Used
    /// <i>unqualified</i>, such a name binds to two candidates at once — <c>System.Drawing.X</c> and
    /// <c>Majorsilence.Forms.X</c> — and the file fails to compile with CS0104, so the converter emits a
    /// using-alias pinning it to the Majorsilence.Forms one.
    ///
    /// The rest of <see cref="MajorsilenceFormsTypes"/> (<c>Graphics</c>, <c>ContentAlignment</c>,
    /// <c>SystemBrushes</c>, <c>SystemPens</c>, <c>SystemFonts</c>) is type-forwarded to the Windows-only
    /// <c>System.Drawing.Common</c> assembly, which a migrated project no longer references — those names
    /// have exactly one candidate and need no alias.
    /// </summary>
    public static readonly HashSet<string> AmbiguousWithSystemDrawing = new(StringComparer.Ordinal)
    {
        "SystemColors", "ColorTranslator",
    };

    /// <summary>
    /// High-signal <c>System.Drawing</c> top-level types from the Windows-only <c>System.Drawing.Common</c>
    /// that have <b>no</b> Majorsilence replacement (in either namespace). When one is used <i>unqualified</i>
    /// under a <c>using System.Drawing;</c>, the textual rewrite can't see it, so we name-match it to warn —
    /// they would otherwise be silent compile breaks. The names are distinctive enough (nobody calls a local
    /// <c>TextureBrush</c>) that false positives are unlikely.
    ///
    /// EMF/WMF metafile recording-and-playback (<c>Metafile</c>/<c>MetafileHeader</c>) is genuinely out of
    /// scope — a Windows-GDI concept with no cross-platform meaning on the SkiaSharp backend, same category
    /// as the VB Application Model non-goal elsewhere in this repo, not a gap to be filled. Everything else
    /// that used to be listed here (<c>ImageAttributes</c>, <c>ColorMatrix</c>, <c>ColorMap</c>, <c>Encoder</c>,
    /// <c>EncoderParameter</c>, <c>EncoderParameters</c>, <c>CharacterRange</c>) gained a real implementation
    /// and moved to <see cref="MajorsilenceDrawingTypes"/> — see that field's doc comment for why the move is
    /// still an approximation for the ones living in a sub-namespace.
    /// </summary>
    public static readonly HashSet<string> UnmappedDrawingTypes = new(StringComparer.Ordinal)
    {
        "Metafile", "MetafileHeader",
    };

    /// <summary>The namespace the GDI+ replacements live in; added alongside a kept <c>System.Drawing</c> import.</summary>
    public const string DrawingTarget = "Majorsilence.Forms.Drawing";
}
