# Compatibility matrix

What's real, what's approximated, and what's out of scope in Majorsilence.Forms — for developers
migrating a WinForms app and for AI coding assistants generating code against this framework. For
how source gets here in the first place, see [`MIGRATION.md`](MIGRATION.md).

## Package layout

| Package | Contents | Depends on |
|---|---|---|
| `Majorsilence.Forms` | Core: controls, layout, events, printing, spellcheck engine, the native-webview seam (`IWebViewFactory`) | `Majorsilence.Forms.Drawing.Common`, SkiaSharp, Topten.RichTextKit |
| `Majorsilence.Forms.Drawing.Common` | The `Majorsilence.Forms.Drawing` GDI+ replacement (`Bitmap`, `Font`, `Pen`, `Brush`, `Icon`, `Region`, `StringFormat`, `Drawing2D`, `Imaging`, ...) plus the bundled fallback font set. Usable standalone, without any of the WinForms control layer | SkiaSharp |
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

## Public API surface audit (2026-07-29)

The section above describes what works. This section is a generated-and-reviewed audit of what's
*there to call in the first place* — comparing `Majorsilence.Forms`'s actual public/protected
member surface against upstream `dotnet/winforms`'s own `PublicAPI.Shipped.txt` (the file the
WinForms team uses to track its shipped API), as of 2026-07-29. Methodology: `System.Windows.Forms.Foo`
and `Majorsilence.Forms.Foo` are treated as "the same type" per the
[namespace mapping](MIGRATION.md#namespace-mapping); members were compared by name (not exact
overload signatures) across each type's full effective surface (own + inherited), via reflection
over a Release build of `Majorsilence.Forms.dll`. Per the [stub policy](#stub-policy) above, a
member that exists but no-ops is **not** a finding here — the only thing worth flagging is a member
that doesn't exist at all, because that's the difference between migrated code compiling with
reduced fidelity versus not compiling.

Two systemic patterns showed up across almost every control, worth stating once instead of
per-row:

- **Protected extensibility hooks are thin.** The `On*Changed`/`On*` overridable methods WinForms
  uses for subclassing (`OnFontChanged`, `OnHandleCreated`, `OnDragEnter`/`OnDragDrop`/`OnGiveFeedback`,
  `OnPreviewKeyDown`, `OnPrint`, ...), the `Reset*` methods designer serialization relies on
  (`ResetBackColor`, `ResetCursor`, `ResetImeMode`, ...), and the RTL/scaling helper methods
  (`RtlTranslateAlignment`, `ScaleControl`, ...) are mostly absent from `Control` and therefore from
  every control derived from it. Calling code (setting properties, handling public events) is
  unaffected; a custom control that overrides one of these protected members to hook framework
  behavior won't compile.
- **A few "family" controls don't share upstream's common base class**, so members upstream gets
  for free through inheritance have to exist per-type here, and sometimes don't yet:
  `Majorsilence.Forms.Form` derives from an internal `WindowBase` (not `Control`), so plain
  `Control` members like `Anchor`, `Dock`, `TabIndex`, `Padding`/`Margin`, `Parent`, and
  `MouseEnter`/`MouseLeave` don't exist on a `Form` here even though they do upstream (`Form` is
  Control-derived there). `MenuStrip`/`ContextMenuStrip`/`StatusStrip` are built on the legacy
  `Menu`/`ContextMenu`/`Control` classes rather than on `ToolStrip` (upstream, all three *are*
  `ToolStrip` subclasses) — so `Renderer`, `RenderMode`, `LayoutStyle`, `GripStyle`, `Stretch`,
  `CanOverflow`, and similar `ToolStrip`-level members (present, as stub properties, on the real
  `Majorsilence.Forms.ToolStrip`) aren't reachable from those three at all.

Status below is scored from a migrating developer's point of view: **Implemented** means the
mainstream, commonly-used surface is there (gaps are limited to the two patterns above, or to
deep/rare corners); **Partial** names the specific commonly-used members that are missing;
**Missing** means the type doesn't exist under that name at all.

| Control / type | Status | Notes |
|---|---|---|
| `Button`, `CheckBox`, `RadioButton`, `Label`, `LinkLabel`, `PictureBox`, `Panel`, `GroupBox`, `TabControl`/`TabPage`, `FlowLayoutPanel`, `TableLayoutPanel`, `TrackBar`, `ProgressBar`, `ScrollBar`/`HScrollBar`/`VScrollBar`, `Splitter`, `UserControl` | Implemented | Only the systemic gaps above; no missing members specific to these types. |
| `TextBox` | Implemented | Full surface for get/set/select/undo usage. |
| `RichTextBox` | Partial | No `Undo`/`Redo`/`CanUndo`/`CanRedo`/`RedoActionName`, no `SelectedRtf`, no `CanPaste`, no `AutoWordSelection`. |
| `MaskedTextBox` | Partial | No `InsertKeyMode`/`IsOverwriteMode`, no `GetCharIndexFromPosition`/`GetPositionFromCharIndex` family, no `ValidateText`. |
| `ComboBox`, `ListBox`, `CheckedListBox` | Partial | Data-binding format hooks missing (`Format`/`FormatString`/`FormatInfo` and their `*Changed` events), no `DataSourceChanged`/`DisplayMemberChanged`/`ValueMemberChanged`, no `Sort()` (`ListBox`)/`PreferredHeight`. `DataSource`/`DisplayMember`/`ValueMember` themselves *do* exist. |
| `ListView` | Partial | No owner-draw (`OwnerDraw`, `DrawItem`/`DrawSubItem`/`DrawColumnHeader`), no virtual mode retrieval (`OnRetrieveVirtualItem`/`OnSearchForVirtualItem`/`OnCacheVirtualItems` — `VirtualMode` itself is a plain property), no groups' `TaskLink`/`CollapsedState`, no `InsertionMark`. |
| `TreeView` | Partial | No `Sorted`, no `ImageKey`/`SelectedImageKey` (index-based `ImageIndex` works), no `HitTest`, no `ShowNodeToolTips`. |
| `DataGridView` | Partial | Largest gap in the audit (~300 missing members). `VirtualMode`/`CellValueNeeded`/`CellValuePushed` exist; missing: `RowValidating`/`RowValidated`, `CellFormatting`/`CellParsing`, custom-paint hooks (`CellPainting`, `RowPrePaint`/`RowPostPaint`), advanced border styles (`AdvancedCellBorderStyle` and friends), `GetClipboardContent`, per-column/row granular `*Changed` events. |
| `DataGrid` (legacy) | Partial | Similar shape of gaps to `DataGridView`; present for basic bound-grid usage. |
| `DataGridViewColumn`/`Row`/`Cell` and the typed column family (`*ComboBoxColumn`, `*CheckBoxColumn`, etc.) | Partial | Core get/set works; no `Clone()`, no custom-paint (`Paint`/`PaintCells`/`PaintHeader` on `Row`; `Paint`/`PaintBorder`/`PaintErrorIcon` on `Cell`), no `InheritedStyle`/`InheritedState`. |
| `DateTimePicker`, `MonthCalendar` | Partial | `MonthCalendar` has no bolded-date API (`AddBoldedDate`/`AddAnnuallyBoldedDate`/etc.) and no `HitTest`; `DateTimePicker` has no `DropDownAlign` or the `CalendarTrailingForeColor`-style theming properties. |
| `NumericUpDown`, `DomainUpDown` | Partial | No `BeginInit`/`EndInit` (`ISupportInitialize`), no `BorderStyle`, no `ParseEditText`/`UpdateEditText` overrides. |
| `SplitContainer` | Partial | Not `ContainerControl`-derived here, so no `ActiveControl`, `AutoValidate`, `BeginInit`/`EndInit`, `ValidateChildren`. |
| `Form` | Partial | See the `WindowBase` note above — missing the plain-`Control` surface (`Anchor`, `Dock`, `TabIndex`, `Padding`, `Parent`, mouse-enter/leave events, `Region`, `RightToLeft`) in addition to Form-specific gaps (`AutoScroll*`, `FormCornerPreference`, MDI merge members `Menu`/`MergedMenu`/`MenuStart`/`MenuComplete`). Core lifecycle (`Load`, `Shown`, `Closing`, `ShowDialog`, `Show`) is solid. |
| `ToolStrip` | Partial | Real base class with stub properties for most `ToolStrip`-level members (`Renderer`, `LayoutStyle`, `GripStyle`, ...); missing `Items`-level layout events (`LayoutCompleted`) and `GetItemAt`/`GetNextItem`. |
| `MenuStrip`, `ContextMenuStrip`, `StatusStrip` | Partial | Not `ToolStrip`-derived (see above) — none of `ToolStrip`'s member surface (stubbed or otherwise) is reachable from these three. Basic menu/status functionality via `Menu`/`Control` works. |
| `ToolStripMenuItem`, `ToolStripButton`, `ToolStripLabel`, `ToolStripComboBox`, `ToolStripTextBox`, `ToolStripSeparator`, `ToolStripDropDownButton`, `ToolStripSplitButton`, `ToolStripStatusLabel`, `ToolStripProgressBar`, `ToolStripDropDown` | Partial | Core `ToolStripItem` surface (`Text`, `Image`, `Click`, `Enabled`, `Visible`) present; missing accessibility (`AccessibilityObject`), drag/drop (`DoDragDrop`, `DragEnter`/`DragDrop`), and layout internals (`ContentRectangle`, `DefaultMargin`/`DefaultPadding`, `Placement`). |
| `ToolStripContainer` | Implemented | Only the systemic gaps above. |
| `MenuItem`, `ContextMenu`, `MainMenu` (legacy) | Partial | Basic construction/click/items work; MDI menu-merging (`MergeMenu`, `MdiListItem`, `FindMergePosition`) and Win32 handle interop (`Handle`, `CreateMenuHandle`) are absent — reasonable, since there's no Win32 menu handle to merge. |
| `BindingSource` | Partial | No sort/filter surface (`ApplySort`/`RemoveSort`/`IsSorted`/`SortDescriptions`, `SupportsSorting`/`SupportsFiltering`/`SupportsSearching`), no `AddingNew`/`DataError`/`CurrentItemChanged` events, no `List`/`CurrencyManager` accessors. |
| `BindingNavigator` | Partial | Standard toolbar items (`MoveFirstItem`, `AddNewItem`, etc.) work; inherits `ToolStrip`'s gaps above plus its own `AddStandardItems`/`BeginInit`/`EndInit`. |
| `PropertyGrid` | Partial | Grid/property display and `SelectedObject` work; no category/commands-pane theming (`CommandsBackColor`, `CategorySplitterColor`, ...), no `PropertyTabs`, no `ToolStripRenderer`. |
| `WebBrowser` | Partial | Navigation (`Navigate`, `Url`, `DocumentTitle`, nav events) works; no DOM object model at all — `Document`/`DocumentStream`/`ObjectForScripting` and the whole `HtmlDocument`/`HtmlElement`/`HtmlWindow` family don't exist, because there's no MSHTML-equivalent behind it (it's backed by a real browser webview, not COM automation). |
| `NotifyIcon`, `ErrorProvider`, `ToolTip`, `Timer`, `ImageList`, `SplitButton` | Implemented | `SplitButton` isn't an upstream WinForms type (only `ToolStripSplitButton` is) — likely meant that. Minor gaps only (e.g. `ToolTip.OwnerDraw`/`Popup`). |
| `MessageBox` | Implemented | |
| `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog`, `ColorDialog`, `FontDialog`, `PrintDialog`, `PrintPreviewDialog` | Partial | Result/selection properties and `ShowDialog()` work. Missing: Windows-shell-only extras (`CustomPlaces`, `ShowPinnedPlaces`, `AutoUpgradeEnabled`), `FolderBrowserDialog.Multiselect`/`SelectedPaths` (.NET 5+ addition), `PrintDialog.AllowCurrentPage`/`PrintToFile`. `Instance`/`HookProc`/`RunDialog`/`OwnerWndProc` (Win32 dialog-hook plumbing) are absent everywhere — expected, there's no native dialog to hook. |
| `Application`, `ApplicationContext`, `Screen`, `Cursor`, `Clipboard` | Partial | Everyday members (`Run`, `Exit`, `DoEvents`, `PrimaryScreen`, `Current`, `GetData`/`SetText`) present. `Clipboard` has no audio/file-drop-list support; `SystemInformation` (a big static grab-bag of Win32 metrics — caret blink time, menu fade, DPI-scaled scrollbar sizes, etc.) implements only a fraction, the rest having no meaningful cross-platform value. |
| `ListViewItem`, `ListViewGroup`, `TreeNode` | Partial | Core properties present; no `Clone()`, no `Serialize`/`Deserialize` (used for drag-drop persistence), `TreeNode` has no `ExpandAll`/`IsVisible`/`NextVisibleNode`/`PrevVisibleNode`. |
| `ButtonBase`, `ListControl`, `UpDownBase`, `WebBrowserBase`, `DataGridViewBand`/`DataGridViewElement`, `ToolStripDropDownItem`/`ToolStripDropDownMenu` | Architectural, not a gap | These upstream abstract/intermediate base classes aren't separately modeled — their members are folded directly into the concrete controls above (e.g. `Button` implements what upstream splits across `Button`+`ButtonBase`). Only matters if your code declares a variable of the base type for polymorphism across, e.g., `Button`/`CheckBox`/`RadioButton`. |

**Rarely used, not separately audited in depth** (each gets a one-line disposition rather than a
row): `AxHost`/ActiveX control hosting, `ComponentEditorForm` and other design-time-only forms, the
whole `System.Windows.Forms.Design`/`PropertyGridInternal` surface, `Message`/`NativeWindow`
low-level Win32 message plumbing (present in narrow form, not a full message-loop replacement),
`DataObject`/`DataFormats`/OLE drag-drop custom formats (basic drag-drop works; custom OLE format
registration doesn't), `StatusBarPanel`/`ToolBarButton` (the pre-.NET-2.0 `StatusBar`/`ToolBar`
legacy controls exist and work at a basic level), IME-specific classes beyond the `ImeMode`
property itself, and `HelpProvider`/`PowerStatus` (present, thin). None of these came up as
commonly-referenced in the priority-control review above.

**Audit scope**: ~110 types were checked in depth (the ones named in this section plus their
immediate supporting types); 2 came back with no missing members at all (`MessageBox`, `Timer`),
roughly a dozen more are "Implemented" above with only the two systemic gaps, and the remainder are
"Partial" with the specific missing members named. No type on this list was found completely absent
under its own name — the "Missing" cases above are all upstream-only intermediate base classes,
not surface a migrating app calls directly.

## `System.Drawing` / GDI+

See [`MIGRATION.md`'s namespace table](MIGRATION.md#namespace-mapping) for the exact rewrite rules.
Summary: primitive value types (`Color`, `Point`, `PointF`, `Size`, `SizeF`, `Rectangle`,
`RectangleF`) are the real cross-platform BCL types from `System.Drawing.Primitives` and are
deliberately **not** reimplemented — reimplementing them would make every bare `Point`/`Rectangle`/
`Color` ambiguous in the (very common) files that have both `System.Drawing` and
`Majorsilence.Forms.Drawing` in scope. GDI+ proper (`Bitmap`, `Font`, `Pen`, `Brush`, imaging and
text-layout namespaces) is reimplemented cross-platform in the `Majorsilence.Forms.Drawing` namespace
on top of SkiaSharp, replacing the Windows-only `System.Drawing.Common`.

That namespace lives in a single project, [`src/Majorsilence.Forms.Drawing.Common`](src/Majorsilence.Forms.Drawing.Common),
which `Majorsilence.Forms` references and which also ships as its own package for consumers that want
the drawing layer without the control layer. Four files remain under `src/Majorsilence.Forms/Drawing/`
because they depend on the Forms layer and would otherwise form a circular project reference:
`Graphics.cs` (declares a partial of `Control`, and calls `Theme`/`TextMeasurer`), `SkiaGraphics.cs`
(`ContentAlignment`, `TextMeasurer`), `BufferedGraphics.cs` (typed throughout on that `Graphics`), and
`NrbfResourceReader.cs` (materialises `ImageListStreamer`). Each carries a header comment saying so.
The drawing project grants `InternalsVisibleTo` to `Majorsilence.Forms` so those four can keep using
the SkiaSharp interop seam (`CreatePaint`, `GetSKBitmap`, `ToSKPath`, ...) without that seam becoming
public API.

Two font-related root files also stay, for a different reason. `SystemFonts.cs` builds its fonts from
`Theme`, so it hits the same cycle. `CachingFontMapper.cs` has no cycle and *could* move, but installs
a process-wide default for Topten.RichTextKit — the text **layout** engine — and every RichTextKit
consumer (`TextMeasurer`, `TextBoxDocument`, `TextBox`, `TextBoxRenderer`, `SkiaTextExtensions`,
`Theme`) lives in `Majorsilence.Forms`. The drawing project contains no RichTextKit code; its text path
is SkiaSharp `SKFont`-based. Moving that one internal class would force a Topten.RichTextKit dependency
onto the standalone drawing package for something none of its consumers can reach.

**Printing** (`Majorsilence.Forms.Printing.PrintDocument`) renders pages through the same SkiaSharp
pipeline as on-screen controls and outputs a real PDF (`SKDocument.CreatePdf`) rather than spooling
to an OS print driver — `PrintPreviewDialog` opens that PDF in the system's default viewer. This is
a platform-agnostic substitute for driver-level printing, not a gap to be filled per-OS.

### GDI+ surface audit (2026-07-29)

Same audit pass as [above](#public-api-surface-audit-2026-07-29), applied to upstream's
`src/System.Drawing.Common/src/System/Drawing` tree (no `PublicAPI.Shipped.txt` exists for this
assembly — it's tracked via `ApiCompatExcludeAttributes.txt`/`CompatibilitySuppressions.xml`
instead — so this pass is type-level source-listing comparison, not member-level reflection).

This audit was run against a since-fixed split: at the time, the shipping GDI+ implementation lived
inline in `src/Majorsilence.Forms/Drawing/` while the separately-packaged
`Majorsilence.Forms.Drawing.Common` project sat unreferenced and less complete. The two have since
been consolidated (see [above](#system-drawing--gdi)) — the table below still reflects the
consolidated, more-complete surface (the merge kept whichever of the two implementations was ahead
per member, so the findings below hold, with one exception noted in its row: multi-stop gradient
blend support was folded in during that consolidation and is no longer purely missing).

| Type / area | Status | Notes |
|---|---|---|
| `Bitmap`, `Image`, `Icon`, `ImageAnimator`, `Graphics` (core drawing), `Pen`/`Pens`, `Brush` family (`SolidBrush`, `HatchBrush`, `LinearGradientBrush`, `PathGradientBrush`, `TextureBrush`, `Brushes`), `Region`, `Font`/`FontFamily`/`FontStyle`, `StringFormat` family, `Matrix`, `GraphicsPath`, `BufferedGraphics`/`Context`/`Manager`, `RotateFlipType`, `GraphicsUnit`, `Drawing2D` core enums (`SmoothingMode`, `CompositingMode`/`Quality`, `InterpolationMode`, `PixelOffsetMode`, `LineCap`/`LineJoin`, `DashStyle`, `WrapMode`) | Implemented | The mainstream drawing/imaging/text-layout path a migrated `OnPaint` override uses. |
| `ImageAttributes`, `ColorMatrix` | Missing | No color-transform path for `Graphics.DrawImage` (grayscale/opacity-via-matrix tricks). Real gap for anything doing image color remapping at draw time. |
| `CustomLineCap`, `GraphicsPathIterator`, `PenAlignment`, `GraphicsContainer` (`BeginContainer`/`EndContainer`) | Missing | Advanced `Drawing2D` corner: custom pen end-caps, path iteration/markers, container save/restore. |
| `Blend`/`ColorBlend` (`SetBlendTriangularShape`/`SetSigmaBellShape`) | Partial | Multi-stop gradients themselves work — `LinearGradientBrush.InterpolationColors`/`InterpolationColorPositions` accept an arbitrary color/position ramp — but the real GDI+ `Blend`/`ColorBlend` types and the shape-preset helper methods don't exist. |
| `BitmapData` (`Bitmap.LockBits`/`UnlockBits`) | Missing | No low-level pixel-buffer access path; `Bitmap.GetPixel`/`SetPixel`-style per-pixel access works through the SkiaSharp-backed `Bitmap` itself. |
| `PropertyItem` (`Image.PropertyItems`) | Missing | No EXIF/image-metadata read path. |
| `FontCollection`, `PrivateFontCollection`, `InstalledFontCollection` | Missing | No API for loading a custom font file at runtime or enumerating installed system fonts; `FontFamily`'s built-in generic families (`GenericSansSerif`, etc.) work. |
| `SystemBrushes`, `SystemPens` | Missing | `SystemColors` and `SystemFonts` (in `Majorsilence.Forms`, not `.Drawing`) exist; the brush/pen wrapper convenience types around those colors don't. |
| `FrameDimension` | Missing | No multi-frame image API (animated GIF frame / multi-page TIFF page selection). |
| `Metafile`/`MetafileHeader`/`EmfType`/`EmfPlusFlags`/`EmfPlusRecordType`/`MetaHeader`/`WmfPlaceableFileHeader`, `IDeviceContext`, `Gdiplus.cs`-level GDI/HDC interop, `StockIconId`/`StockIconOptions` | Deliberately out of scope | EMF/WMF metafile recording-and-playback and raw Win32 HDC interop are Windows-GDI concepts with no cross-platform meaning on a SkiaSharp backend — same category of non-goal as the [VB Application Model](MIGRATION.md#vb-application-model-myapplication-myforms) elsewhere in this doc, not a gap to be filled. Design-time-only converters (`FontConverter`, `IconConverter`, `ImageConverter`, `ToolboxBitmapAttribute`) are likewise out of scope, consistent with `System.Windows.Forms.Design` above. |
| `Printing` (`PageSettings`, `PrinterSettings`, `PaperSize`/`PaperSource`/`PrinterResolution`, `Margins`, `PrintRange`, `Duplex`, `QueryPageSettingsEventArgs`, `PrintDocument`, `PreviewPrintController`) | Implemented | Lives in `Majorsilence.Forms.Printing` (see above), not `.Drawing`. Missing: `PrinterUnit`/`PrinterUnitConvert`, `TriState`, `PrintAction`, `PrintEventArgs`/`PrintEventHandler`, `PreviewPageInfo` — minor, rarely referenced directly by app code. |

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
