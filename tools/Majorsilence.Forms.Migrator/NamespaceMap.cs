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
        ("System.Windows.Forms.VisualStyles", "Majorsilence.Forms.VisualStyles"),
        ("System.Windows.Forms.Design", "Majorsilence.Forms.Design"),
        ("System.Drawing.Design", "Majorsilence.Forms.Design"),
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
        "Font", "FontFamily", "FontStyle", "Graphics", "GraphicsPath", "GraphicsState", "GraphicsUnit",
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

    /// <summary>
    /// Windows system libraries that appear in <c>[DllImport]</c>/<c>[LibraryImport]</c> declarations.
    /// </summary>
    /// <remarks>
    /// Same failure shape as <see cref="WindowsOnlyRegistryTypes"/>, and worse to diagnose: a P/Invoke
    /// is bound lazily at the first call, so the build is green, the app starts, and it dies with a
    /// <c>DllNotFoundException</c> naming a <c>.dll</c> the reader knows perfectly well exists on
    /// Windows. Warned about rather than rewritten -- the declaration and its call sites are separate
    /// pieces of syntax, and the replacement is per-function, not mechanical.
    ///
    /// Deliberately only *system* libraries. A P/Invoke into a third-party native library is often
    /// perfectly portable (libmpv, SQLite, MediaInfo) once the name is resolved per platform, so
    /// flagging every DllImport would bury the real breakages in noise.
    /// </remarks>
    public static readonly string[] WindowsOnlyNativeLibraries =
    [
        "user32", "kernel32", "gdi32", "gdiplus", "shell32", "shlwapi", "advapi32", "comctl32",
        "comdlg32", "dwmapi", "uxtheme", "ole32", "oleaut32", "oleacc", "winmm", "msimg32",
        "ntdll", "psapi", "version", "wtsapi32", "secur32", "crypt32", "imm32", "powrprof",
        "setupapi", "usp10", "dbghelp", "winspool.drv", "hhctrl.ocx", "shcore",
    ];

    /// <summary>
    /// Entry points common enough in WinForms code to be worth naming their managed replacement.
    /// </summary>
    /// <remarks>
    /// Only entries whose replacement is a genuine equivalent rather than a rough analogue -- a wrong
    /// suggestion here costs more than no suggestion, because it reads as authoritative. Anything else
    /// gets the generic "no cross-platform equivalent" warning.
    /// </remarks>
    public static readonly (string EntryPoint, string Replacement)[] PInvokeManagedEquivalents =
    [
        ("SetProcessDPIAware", "Application.SetHighDpiMode (HighDpiMode.SystemAware)"),
        ("SetProcessDpiAwareness", "Application.SetHighDpiMode (HighDpiMode.PerMonitor)"),
        ("SetProcessDpiAwarenessContext", "Application.SetHighDpiMode (HighDpiMode.PerMonitorV2)"),
        ("GetSystemMetrics", "the SystemInformation properties"),
        ("SetForegroundWindow", "Form.Activate ()"),
        ("GetKeyState", "Control.ModifierKeys"),
        ("GetAsyncKeyState", "Control.ModifierKeys"),
        ("GetTickCount", "Environment.TickCount64"),
        ("GetTickCount64", "Environment.TickCount64"),
        ("ShellExecute", "Process.Start (new ProcessStartInfo (path) { UseShellExecute = true })"),
        ("GetCursorPos", "Control.MousePosition"),
        ("SetCursorPos", "Cursor.Position"),
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
    /// namespace (its WinForms-compat surface) rather than <c>Majorsilence.Forms.Drawing</c>. A fully-qualified
    /// <c>System.Drawing.&lt;name&gt;</c> is rewritten to <c>Majorsilence.Forms.&lt;name&gt;</c>. Verified
    /// against the type declarations in <c>src/Majorsilence.Forms/*.cs</c>.
    /// </summary>
    public static readonly HashSet<string> MajorsilenceFormsTypes = new(StringComparer.Ordinal)
    {
        "ContentAlignment", "ColorTranslator",
        "SystemColors", "SystemBrushes", "SystemPens", "SystemFonts",
    };

    /// <summary>
    /// Every name in <see cref="MajorsilenceFormsTypes"/> needs a using-alias in a file that keeps its
    /// <c>using System.Drawing;</c>, for one of two reasons:
    ///
    /// <list type="bullet">
    /// <item><c>SystemColors</c> and <c>ColorTranslator</c> also ship in <c>System.Drawing.Primitives</c>,
    /// which a migrated project still references. Used unqualified they bind to two candidates at once —
    /// <c>System.Drawing.X</c> and <c>Majorsilence.Forms.X</c> — and the file fails with CS0104.</item>
    /// <item>The rest (<c>ContentAlignment</c>, <c>SystemBrushes</c>, <c>SystemPens</c>,
    /// <c>SystemFonts</c>) is type-forwarded to the Windows-only <c>System.Drawing.Common</c>, which a
    /// migrated project drops. That looks safe — one candidate, no ambiguity — but only if the file also
    /// imports <c>Majorsilence.Forms</c>, and a file that draws without naming a single control type never
    /// gets that import. Then there is no Majorsilence candidate at all, the name still resolves through
    /// <c>using System.Drawing;</c> to the forwarded type, and the file fails with CS1069.</item>
    /// </list>
    ///
    /// One alias line fixes every use site, and is harmless where the import happens to be present: an
    /// alias outranks a using-directive, so it just pins the name to the same type it would have bound to.
    /// </summary>
    public static readonly HashSet<string> AliasedWithSystemDrawing = MajorsilenceFormsTypes;

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

    /// <summary>
    /// Names that exist in <b>both</b> a BCL namespace and a Majorsilence one, mapped to the BCL namespace
    /// the pre-migration source meant by them. Importing both namespaces makes an unqualified use ambiguous
    /// (CS0104), and the migration must not silently change which type the code refers to — so the alias
    /// pins the name to what it resolved to before.
    /// </summary>
    /// <remarks>
    /// <c>BackgroundWorker</c> and <c>PropertyTabScope</c> are real cross-platform BCL types that the compat
    /// layer also declares; <c>ColorConverter</c> ships in <c>System.Drawing.Primitives</c> as well as in
    /// <c>Majorsilence.Forms.Drawing</c>. All three are only a problem for a file (or a project's global
    /// usings) that imports both sides, which is exactly what a migrated WinForms project does.
    /// </remarks>
    public static readonly (string Type, string BclNamespace)[] BclPreferredTypes =
    [
        ("BackgroundWorker", "System.ComponentModel"),
        ("PropertyTabScope", "System.ComponentModel"),
        ("ColorConverter", "System.Drawing"),
    ];

    /// <summary>
    /// <c>System.ComponentModel.Design</c> types that do <b>not</b> ship in the BCL — they live in the
    /// Windows-only <c>System.Windows.Forms.Design</c> assembly — and that Majorsilence.Forms reimplements
    /// under <c>Majorsilence.Forms.Design</c>. Everything else in that namespace (<c>IDesigner</c>,
    /// <c>IDesignerHost</c>, <c>DesignerVerb</c>, <c>DesignerVerbCollection</c>, …) is real BCL and must be
    /// left alone, which is why this is a type-level redirect rather than a namespace mapping.
    /// </summary>
    /// <remarks>
    /// This is the smart-tag surface a control library declares one of per control
    /// (<c>DesignerActionList</c> + items) plus the collection-editor dialog and the multi-line string
    /// editor that properties are attributed with. <c>CollectionForm</c> is nested inside
    /// <c>CollectionEditor</c> upstream and here too, so a qualified <c>System.ComponentModel.Design.CollectionForm</c>
    /// is not a thing to rewrite — an unqualified use resolves through the <c>Majorsilence.Forms.Design</c> import.
    /// </remarks>
    public static readonly HashSet<string> DesignTimeTypes = new(StringComparer.Ordinal)
    {
        "CollectionEditor", "DesignerActionList", "DesignerActionListCollection", "DesignerActionItem",
        "DesignerActionItemCollection", "DesignerActionMethodItem", "DesignerActionPropertyItem",
        "DesignerActionHeaderItem", "DesignerActionTextItem", "MultilineStringEditor",
    };

    /// <summary>
    /// <c>Microsoft.Win32</c> types Majorsilence.Forms reimplements under its own namespace. These are the
    /// system-notification types that ship in the Windows-only <c>System.Drawing.Common</c>:
    /// <c>SystemEvents</c> and everything its <c>UserPreferenceChanged</c> event needs. The rest of
    /// <c>Microsoft.Win32</c> — <c>Registry</c>, <c>RegistryKey</c>, <c>SafeHandles</c> — is genuinely
    /// Windows-only or genuinely cross-platform BCL, and is left untouched (the registry types are already
    /// reported as manual-review items).
    /// </summary>
    public static readonly HashSet<string> Win32CompatTypes = new(StringComparer.Ordinal)
    {
        "SystemEvents", "UserPreferenceChangedEventArgs", "UserPreferenceCategory",
        "UserPreferenceChangedEventHandler",
    };
}
