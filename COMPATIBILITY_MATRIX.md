# Compatibility matrix

What's real, what's approximated, and what's out of scope in Majorsilence.Forms — for developers
migrating a WinForms app and for AI coding assistants generating code against this framework. For
how source gets here in the first place, see [`MIGRATION.md`](MIGRATION.md).

## Package layout

| Package | Contents | Depends on |
|---|---|---|
| `Majorsilence.Forms` | Core: controls, layout, events, `Majorsilence.Forms.Drawing` (GDI+ replacement), printing, spellcheck engine, the native-webview seam (`IWebViewFactory`) | SkiaSharp, Topten.RichTextKit |
| `Majorsilence.Forms.Avalonia` | Default backend — Windows/macOS/Linux desktop, real `WebView2`/`WKWebView`/`WebKitGTK` support via `Avalonia.Controls.WebView` | `Majorsilence.Forms` + Avalonia |
| `Majorsilence.Forms.Uno` | Uno Platform (Skia) backend — desktop, iOS, Android, WebAssembly | `Majorsilence.Forms` + Uno.WinUI |
| `Majorsilence.Forms.Headless` | Offscreen SkiaSharp backend — CI, automated tests, pixel-diff verification. No native webview support (`IWebViewFactory` is absent, not just unsupported) | `Majorsilence.Forms` |
| `Majorsilence.Forms.WindowsUIAutomation` | Windows-only UI Automation bridge so screen readers/magnifiers can drive a Majorsilence.Forms window. Off Windows, ships as an empty stub so `dotnet build` stays green cross-platform | `Majorsilence.Forms`, Windows-only |
| `Majorsilence.Forms.Telerik` | Telerik UI for WinForms compat layer — see [below](#telerik-ui-for-winforms-compat-layer). Depends only on core, **not** on any specific backend — the webview-backed controls in it (`RadPdfViewer`, `RadRichTextEditor`) work with whichever backend the host app references, or degrade gracefully if none supports webviews | `Majorsilence.Forms` only |

Two-way embedding — hosting a Majorsilence.Forms window *inside* an existing Avalonia/Uno app, or
the reverse (embedding a native control inside a Majorsilence.Forms app) — is demonstrated in
[`samples/WinFormsInterop`](samples/WinFormsInterop) (Windows-only; see
[`docs/winforms-interop.md`](docs/winforms-interop.md)).

## Stub policy

**If a member has no working implementation yet, it safely no-ops (or returns a sensible default)
instead of throwing `NotImplementedException`.** This is a deliberate, consistent policy across the
whole compat layer — migrated code should compile *and run*, even when a specific visual feature
doesn't yet do anything. Concretely:

- A property with no backing behavior is a plain settable auto-property (e.g.
  `Form.ShowInTaskbar` on backends that don't yet distinguish taskbar visibility) — it stores
  whatever you set and is readable back, it just doesn't (yet) change runtime behavior.
- A method with no implementation returns a default/neutral value rather than throwing —
  e.g. a dialog's `ShowDialog()` that has no real UI yet returns `DialogResult.OK` immediately
  rather than blocking or crashing.
- An event that's never raised compiles and can be subscribed to; it simply never fires. This is
  used sparingly and is called out per-type below where it applies (with a documented,
  intentional `#pragma warning disable CS0067` where the compiler would otherwise flag it).
- The [Telerik compat layer](#telerik-ui-for-winforms-compat-layer) states this most explicitly in
  its own source: *"Coverage is compile-and-approximate, not pixel-perfect: the rich element tree
  of Telerik is represented by lightweight stub elements so formatting handlers and designer code
  compile and run."*

If you find a member that throws instead of stubbing, that's a bug — file it.

## Core WinForms surface

Standard controls (`Button`, `TextBox`, `ComboBox`, `ListBox`, `ListView`, `TreeView`,
`DataGridView`, `Panel`, `SplitContainer`, `TabControl`, menus/toolbars/status bars, common
dialogs, etc.) are functionally implemented, not stubs — see them live in
[`samples/ControlGallery`](samples/ControlGallery), which has one demo panel per control. The
`*.Designer.cs`/`*.Designer.vb` code-behind pattern is preserved as-is; you don't rewrite your
designer-generated layout code.

**Spellcheck** (`Majorsilence.Forms.SpellCheck`, wired into `TextBox`) is a dependency-free,
from-scratch implementation — a pre-expanded Hunspell/SCOWL en-US wordlist embedded as a compressed
resource, with wavy-underline rendering and a right-click suggestions/add-to-dictionary menu. Not a
WinForms API (WinForms never had built-in spellcheck) — it exists to back
[`RadSpellChecker`](#telerik-ui-for-winforms-compat-layer) below.

## `System.Drawing` / GDI+

See [`MIGRATION.md`'s namespace table](MIGRATION.md#namespace-mapping) for the exact rewrite rules.
Summary: primitive value types (`Color`, `Point`, `Size`, `Rectangle`, ...) are the real
cross-platform BCL types and need no reimplementation. GDI+ (`Bitmap`, `Font`, `Pen`, `Brush`,
`Graphics`, imaging/text-layout namespaces) is reimplemented cross-platform in `Majorsilence.Forms.Drawing`
on top of SkiaSharp, replacing the Windows-only `System.Drawing.Common`.

**Printing** (`Majorsilence.Forms.Printing.PrintDocument`) renders pages through the same SkiaSharp
pipeline as on-screen controls and outputs a real PDF (`SKDocument.CreatePdf`) rather than spooling
to an OS print driver — `PrintPreviewDialog` opens that PDF in the system's default viewer. This is
a platform-agnostic substitute for driver-level printing, not a gap to be filled per-OS.

## Telerik UI for WinForms compat layer

`Majorsilence.Forms.Telerik` (its own package — see [Package layout](#package-layout)) mirrors the
public surface of Telerik UI for WinForms controls actually found in migrated codebases, backed by
real Majorsilence.Forms controls underneath. ~200 public types across grid, docking, containers,
drop-downs, menus, and the areas below.

### Fully implemented / real behavior

| Area | Notes |
|---|---|
| `RadGridView` + grid ecosystem | Filtering, sorting, grouping, drag column reorder/freeze, master-detail expansion, row/cell formatting, summary rows, layout XML save/load — backed by the real `DataGridView` engine, not a facade. |
| Docking (`RadDock`, `ToolWindow`, `DocumentWindow`, ...) | Real dock/tab/tear-off behavior. |
| Controls (`RadButton`, `RadTextBox`, `RadCheckBox`, `RadDropDownList`, `RadTreeView`, `RadCalendar`, ...) | Thin wrappers over the equivalent Majorsilence.Forms control — full behavior. |
| `RadDesktopAlert` | A real toast (built on `PopupWindow`/`Timer`), positions bottom-right with stacking for multiple simultaneous alerts, auto-closes on a timer. Its Telerik-shaped element-color properties (`ForeColor`/`BackColor`/`BorderColor`) feed the actual rendering. |
| Grid export (`GridViewSpreadExport`/`ExportToExcelML`, `ExportToCSV`, `ExportToHTML`, `GridViewPdfExport`) | Produces real, openable `.xlsx`/CSV/HTML/PDF files — not stubs. |
| Scheduler data layer (`SchedulerBindingDataSource`, `AppointmentMappingInfo`, `ResourceMappingInfo`) | Real two-way binding: materializes appointments from a bound `DataTable`, supports `SchedulerMapping.ConvertCallback` for custom field conversion, writes edits back. |
| Scheduler view | An **agenda/list view** (appointments grouped by day) — real navigation and data-driven rendering. Printing renders that agenda through the standard `PrintDocument` pipeline. |
| `RadSpellChecker` | Attaches the real spellcheck engine (above) to a `TextBox` via a settable property. |

### Approximated (works, but not pixel-parity with real Telerik)

| Type | What it actually does |
|---|---|
| `RadPdfViewer` / `RadPdfViewerNavigator` | Loads a PDF (from a `Stream` or path) into a real native browser webview and lets the engine's own built-in PDF renderer display it — see the [backend matrix](#webview-backed-features-by-backend) below for exactly where this works inline vs. falls back to the system PDF viewer. `RadPdfViewerNavigator` renders as an empty/minimal strip, since the browser's own PDF toolbar replaces Telerik's. |
| `RadRichTextEditor` / `RichTextEditorRibbonBar` | A webview `contenteditable` HTML editor. `RadDocument` is a thin HTML-string carrier — `HtmlFormatProvider.Import`/`Export` are near-passthroughs, matching real-world usage where content round-trips as HTML end-to-end. Formatting (bold/italic/lists/alignment/etc.) goes through the browser's own `execCommand`. Native browser spellcheck (`spellcheck="true"`) is free; `DocumentSpellChecker.AddDictionary` is a no-op (dictionary management lives in the browser). Where no webview backend is available, falls back to a plain `RichTextBox` showing raw HTML source — functional but not WYSIWYG. |
| `RadPrintDocument` / `RadPrintWatermark` / `Scheduler*PrintStyle` | Wraps the standard `PrintDocument` PDF pipeline; watermarks are drawn diagonally via Skia. `SchedulerPrintSettingsDialog` doesn't show a real dialog (its `ShowDialog()` returns immediately per the [stub policy](#stub-policy)) since the audited usage doesn't require interactive settings. |

### Deliberately out of scope (warn-and-leave, tracked in [`BACKLOG.md`](BACKLOG.md))

| Item | Why |
|---|---|
| Full month/week/day calendar **grid** rendering (drag-resize appointments, timeline swimlanes) | The data layer and agenda view above cover real-world usage; the full interactive calendar grid was judged too large to fake convincingly. `GetMonthView()`/`GetWeekView()`/`GetDayView()`/`GetTimelineView()` return settable-but-not-rendered carriers so migrated code still compiles. |
| `Telerik.WinControls.Themes` (e.g. `Office2007BlackTheme`) | No visual theming system exists to back it; references are left unrewritten by the migrator and flagged for manual review rather than silently ignored. |
| `Telerik.WinControls.Design`, `.Primitives`, `.Layouts` | Design-time/layout infrastructure with no runtime equivalent; same warn-and-leave treatment. (Note: `.Layouts` specifically can't be flattened into the compat namespace even in principle — a type named `Dock` there would collide with `Control.Dock` in any VB file that imports both namespaces.) |

## Heavyweight controls implementation notes

### WebView-backed features by backend

`RadPdfViewer` and `RadRichTextEditor` both build on a single core seam
(`Majorsilence.Forms.Backends.IWebViewFactory`) discovered at runtime from whichever backend the
host app references — the Telerik package itself has no backend dependency.

| Backend / OS | Engine | `RadPdfViewer` | `RadRichTextEditor` |
|---|---|---|---|
| Avalonia / Windows (WebView2 runtime present) | WebView2 | Inline PDF (the engine's own PDFium viewer + toolbar) | Full webview editor |
| Avalonia / Windows (WebView2 runtime missing) | — | Temp file handed to the system's default PDF viewer | Falls back to `RichTextBox` showing raw HTML |
| Avalonia / macOS | WKWebView | Inline PDF (native WebKit PDF rendering) | Full webview editor |
| Avalonia / Linux (WebKitGTK/WPE present) | WebKitGTK | System PDF viewer **by policy** — WebKit has no built-in inline PDF viewer, so Linux always uses the system-viewer path even though the webview itself works | Full webview editor (native spellcheck depends on `enchant` dictionaries being installed) |
| Uno backend, or Avalonia with the engine unavailable | — | System PDF viewer | `RichTextBox` fallback |
| Headless backend | — (no `IWebViewFactory` at all) | Caches the document and paints a placeholder; never shells out to a system viewer (so CI/automated tests never spawn OS processes) | `RichTextBox` fallback |

## VB Application Model

Not implemented — see [`MIGRATION.md`'s VB Application Model section](MIGRATION.md#vb-application-model-myapplication-myforms)
for what that means in practice and how the migrator flags it.
