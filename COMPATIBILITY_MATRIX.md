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

> **Superseded for gap-finding (2026-08-02).** This section was written by hand and compares names
> against upstream's `PublicAPI.Shipped.txt`. That audit is now automated:
> [`tools/Majorsilence.Forms.ApiDiff`](tools/Majorsilence.Forms.ApiDiff) diffs `Majorsilence.Forms`
> against the real `System.Windows.Forms` reference assembly by reflection, keeps a committed
> `baseline.winforms.txt`, and fails CI if the gap set grows. Its first run found **1,905 entries**,
> including **126 enum members that exist with the wrong numeric value** — a class of defect no
> name-level audit can see, since the code compiles and runs and silently means something else. The
> findings and a suggested order are in [`docs/winforms-gap-plan.md`](docs/winforms-gap-plan.md).
> Treat the tool's baseline as the authoritative gap list and the narrative below as commentary.

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

- **Protected extensibility hooks are thin above the `Control` base.** *Updated 2026-07-29 — the
  `Control`-level set named in the original finding now exists and fires.* `Control` now has the
  ambient-appearance notifications (`OnBackColorChanged`/`OnForeColorChanged`/`OnFontChanged` plus
  their `OnParent*Changed` cascades, `OnRightToLeftChanged`, `OnCausesValidationChanged`,
  `OnImeModeChanged`, `OnContextMenuStripChanged`), the handle-lifetime pair
  (`OnHandleCreated`/`OnHandleDestroyed`), the focus pair (`OnEnter`/`OnLeave`, no longer aliases of
  `GotFocus`/`LostFocus`), the mouse/key hooks (`OnMouseClick`/`OnMouseDoubleClick`/`OnMouseHover`/
  `OnMouseCaptureChanged`/`OnPreviewKeyDown`), the drag set
  (`OnDragEnter`/`OnDragOver`/`OnDragDrop`/`OnDragLeave`/`OnGiveFeedback`/`OnQueryContinueDrag`),
  `OnPrint`, the `Reset*` methods designer serialization relies on
  (`ResetBackColor`/`ResetForeColor`/`ResetCursor`/`ResetImeMode`/`ResetRightToLeft`, joining the
  existing `ResetText`/`ResetFont`), and the RTL/scaling helpers (`RtlTranslateAlignment` and its
  `RtlTranslateHorizontal`/`RtlTranslateLeftRight`/`RtlTranslateContent` overloads, `ScaleControl`,
  `ScaleBitmapLogicalToDevice`). These are wired, not declared: the corresponding events are real
  `Events`-backed properties (previously ~20 of them were `add { } remove { }` no-ops) and the
  `BackColor`/`ForeColor`/`Font`/`RightToLeft`/`CausesValidation`/`ImeMode` setters raise them on a
  real value change. **Still thin:** hooks with no framework trigger yet — the drag set has no OS
  drag source (`DoDragDrop` still returns `None`), so a derived control must raise those itself; and
  `ChangeUICues`, `HelpRequested`, `QueryAccessibilityHelp`, `Scroll`, `DpiChangedBeforeParent`/
  `DpiChangedAfterParent`, `BindingContextChanged` and `SystemColorsChanged` remain no-op stub
  events with no `On*` hook. Derived-type-specific hooks (`OnDrawItem`, `OnSelectedIndexChanged`,
  `OnCellPainting`, ...) are unchanged by this and are still mostly absent — see the per-row notes.
- **A few "family" controls don't share upstream's common base class**, so members upstream gets
  for free through inheritance have to exist per-type here, and sometimes don't yet:
  `Majorsilence.Forms.Form` derives from an internal `WindowBase` (not `Control`), so every plain
  `Control` member has to be declared on `WindowBase`/`Form` by hand rather than inherited.
  *Updated 2026-07-29 — the specific members named in the original finding now exist:* `Anchor`,
  `Dock`, `TabIndex`, `Padding`/`Margin`, `Parent` and `MouseEnter`/`MouseLeave` are all reachable
  from a `Form` (see the `Form` row for which of them behave and which are stored stubs). The
  base-class gap itself is unchanged: a `Form` still isn't a `Control`, so it can't go into a
  `Control.ControlCollection` or be found by a `Control`-typed walk of a tree, and any *other*
  `Control` member not listed in the `Form` row still has to be added one at a time.
  *Updated 2026-07-30 — the `ToolStrip` half of this finding is fixed too.*
  `MenuStrip`, `ContextMenuStrip` and `StatusStrip` now genuinely derive from `ToolStrip`, matching
  upstream, so `Renderer`, `RenderMode`, `LayoutStyle`, `GripStyle`, `GripVisible`, `Stretch`,
  `CanOverflow`, `ImageScalingSize`, `ShowItemToolTips`, `TextDirection` and the `ItemAdded`/
  `ItemClicked` events are reachable from all three. `ToolStrip` was spliced into the existing chain
  rather than duplicated onto them: `Menu : ToolStrip` and `MenuDropDown : ToolStrip`, so
  `MenuStrip : Menu : ToolStrip` and `ContextMenuStrip : ContextMenu : MenuDropDown : ToolStrip`,
  while `StatusStrip : ToolStrip` directly. **What this does *not* buy you:** those members are still
  stub properties per the [stub policy](#stub-policy) — they store and return a value but nothing
  consumes them. Painting is still dispatched by `Majorsilence.Forms.Renderers.RenderManager` keyed on
  the concrete type (`MenuRenderer` for `MenuStrip`, `MenuDropDownRenderer` for `ContextMenuStrip`,
  `StatusStripRenderer` for `StatusStrip`), so assigning `Renderer`/`RenderMode` does not change how
  anything draws; `LayoutStyle` does not change layout; `GripStyle`/`Stretch` have no visual effect and
  there is no overflow button, so `CanOverflow` does nothing. Two shape differences remain: here
  `ToolStrip` sits on the legacy `ToolBar`, so these three also inherit `Buttons`/`ButtonSize`/
  `ButtonClick` that upstream's `ToolStrip` (a `ScrollableControl`) has no business having; and
  upstream's `ToolStripDropDown`/`ToolStripDropDownMenu` intermediates are not in
  `ContextMenuStrip`'s chain (`MenuDropDown`/`ContextMenu` stand in for them).

Status below is scored from a migrating developer's point of view: **Implemented** means the
mainstream, commonly-used surface is there (gaps are limited to the two patterns above, or to
deep/rare corners); **Partial** names the specific commonly-used members that are missing;
**Missing** means the type doesn't exist under that name at all.

| Control / type | Status | Notes |
|---|---|---|
| `Control` (base) | Implemented | Inherited by every control below. The protected extensibility surface named in the first systemic pattern above is present and firing as of 2026-07-29; the residue is the stub events listed there that still have no `On*` hook. |
| `Button`, `CheckBox`, `RadioButton`, `Label`, `LinkLabel`, `PictureBox`, `Panel`, `GroupBox`, `TabControl`/`TabPage`, `FlowLayoutPanel`, `TableLayoutPanel`, `TrackBar`, `ProgressBar`, `ScrollBar`/`HScrollBar`/`VScrollBar`, `Splitter`, `UserControl` | Implemented | Only the systemic gaps above; no missing members specific to these types. They pick up the whole `Control` protected surface by inheritance. |
| `TextBox` | Implemented | Full surface for get/set/select/undo usage. |
| `RichTextBox` | Partial | No `Undo`/`Redo`/`CanUndo`/`CanRedo`/`RedoActionName`, no `SelectedRtf`, no `CanPaste`, no `AutoWordSelection`. |
| `MaskedTextBox` | Partial | No `InsertKeyMode`/`IsOverwriteMode`, no `GetCharIndexFromPosition`/`GetPositionFromCharIndex` family, no `ValidateText`. |
| `ComboBox`, `ListBox`, `CheckedListBox` | Partial | Data-binding format hooks missing (`Format`/`FormatString`/`FormatInfo` and their `*Changed` events), no `DataSourceChanged`/`DisplayMemberChanged`/`ValueMemberChanged`, no `Sort()` (`ListBox`)/`PreferredHeight`. `DataSource`/`DisplayMember`/`ValueMember` themselves *do* exist. |
| `ListView` | Partial | No owner-draw (`OwnerDraw`, `DrawItem`/`DrawSubItem`/`DrawColumnHeader`), no virtual mode retrieval (`OnRetrieveVirtualItem`/`OnSearchForVirtualItem`/`OnCacheVirtualItems` — `VirtualMode` itself is a plain property), no groups' `TaskLink`/`CollapsedState`, no `InsertionMark`. |
| `TreeView` | Partial | No `Sorted`, no `ImageKey`/`SelectedImageKey` (index-based `ImageIndex` works), no `HitTest`, no `ShowNodeToolTips`. |
| `DataGridView` | Partial | Still the largest single-type gap, but the highest-traffic hooks are real and firing as of 2026-07-29: **`CellFormatting`** (raised per cell during paint; honors `e.Value`/`FormattingApplied` and applies `e.CellStyle` for that frame only — the resolved style's `Format` string is now applied even with no handler attached), **`CellPainting`** (real per-cell event with live `Graphics`/`CellBounds`/`CellStyle`; `Paint`/`PaintBackground`/`PaintContent` run the grid's own painting, and `Handled`/`PaintParts` really suppress it), **`RowPrePaint`/`RowPostPaint`** (real args with `Graphics`, `RowBounds`, `InheritedRowStyle`, `State`, working `PaintCells*`/`PaintHeader`, and honored `Handled`/`PaintParts`), **`CellParsing`** (raised on edit commit; a `ParsingApplied` handler's typed value is what gets stored, in the cell and in the bound object), **`RowValidating`/`RowValidated`** plus `RowEnter`/`RowLeave` (run on current-row change and on focus loss via `ValidateCurrentRow()`; cancelling keeps the row current, and a pending edit commits first), **`GetClipboardContent()`** (returns a `DataObject` with tab-delimited text, CSV and an HTML table, honoring `ClipboardCopyMode`), and **`CellBorderStyle`/`AdvancedCellBorderStyle`** (+ the header variants — per-edge styles the renderer actually reads, so `None`/`*Horizontal`/`*Vertical` change the drawn grid lines). Still missing (declared for source compat but never raised): `RowsAdded`/`RowsRemoved`, `UserDeletingRow`/`UserDeletedRow`, `DataError`, `CellStateChanged`/`RowStateChanged`, `SortCompare`, `CellValueNeeded`/`CellValuePushed` (`VirtualMode` is a plain property), `DefaultValuesNeeded`/`NewRowNeeded`/`UserAddedRow`, `CellMouseDown`/`Up`/`Move`/`DoubleClick` and the per-column/row granular `*Changed` events (`ColumnWidthChanged`, `RowHeightChanged`, `ColumnDisplayIndexChanged`, ... — all still `add { } remove { }` stubs). Auto-sizing (`AutoResize*`, `AutoSizeColumnsMode`) still only invalidates. |
| `DataGrid` (legacy) | Partial | Similar shape of gaps to `DataGridView`; present for basic bound-grid usage. |
| `DataGridViewColumn`/`Row`/`Cell` and the typed column family (`*ComboBoxColumn`, `*CheckBoxColumn`, etc.) | Partial | Core get/set works. `Clone()` is real on all three (returns the same runtime type, deep-copies styles, and a row clones its cells; the typed column/cell subclasses carry their own extra members across via `CopyStateTo`), as are `InheritedStyle` (the full WinForms cascade: grid → column → `RowsDefaultCellStyle` → `AlternatingRowsDefaultCellStyle` on odd rows → row → cell) and `State`/`InheritedState`. A row's `DefaultCellStyle.BackColor` is now actually painted (it outranks the alternating-row stripe); the rest of a row/column `DefaultCellStyle` still only reaches the renderer through a cell's own `Style` or a `CellFormatting` handler. Note the grid stores its own default styles as `ControlStyle`, so only the members the two style types share participate in the cascade (colors, alignment, format; not the Skia typeface). Still missing: custom-paint (`Paint`/`PaintCells`/`PaintHeader` on `Row`; `Paint`/`PaintBorder`/`PaintErrorIcon` on `Cell`) — the renderer paints cells directly rather than routing through the cell objects, so those would need the paint pipeline inverted; use the grid-level `CellPainting`/`RowPrePaint` hooks instead, which do the same job. Also missing: `GetPreferredSize`/`GetContentBounds`/`GetErrorIconBounds`, `AdjustColumnHeaderBorderStyle`, and cell-level `Detach`/`SetDataGridView` plumbing. |
| `DateTimePicker`, `MonthCalendar` | Partial | `MonthCalendar` has no bolded-date API (`AddBoldedDate`/`AddAnnuallyBoldedDate`/etc.) and no `HitTest`; `DateTimePicker` has no `DropDownAlign` or the `CalendarTrailingForeColor`-style theming properties. |
| `NumericUpDown`, `DomainUpDown` | Partial | No `BeginInit`/`EndInit` (`ISupportInitialize`), no `BorderStyle`, no `ParseEditText`/`UpdateEditText` overrides. |
| `SplitContainer` | Partial | Not `ContainerControl`-derived here, so no `ActiveControl`, `AutoValidate`, `BeginInit`/`EndInit`, `ValidateChildren`. |
| `Form` | Partial | The plain-`Control` surface named in the `WindowBase` note above is present as of 2026-07-29, and mostly with real behavior rather than as stubs: **`Padding`** and **`RightToLeft`** forward to the root `ControlAdapter` (which *is* a `Control`), so padding genuinely insets docked/anchored children and children left on `RightToLeft.Inherit` resolve through the form the same way they resolve through a parent `Control`; **`AutoScroll`/`AutoScrollMargin`/`AutoScrollMinSize`/`AutoScrollPosition`/`SetAutoScrollMargin`** forward to that same root (a `ScrollableControl`), so they really scroll; **`MouseEnter`/`MouseLeave`** are really raised — every backend already reports pointer exit, and entry is inferred from the first pointer event to arrive, so they track the window's whole surface (chrome included) and fire once per entry rather than once per child; **`Parent`** reports the container's `MdiClient` while the form is hosted as an MDI child (matching upstream, where an MDI child's `Parent` is the `MdiClient`) and is a stored value otherwise, since a native top-level window can't be re-parented into a control tree. Stored stubs by design: `Anchor`, `Dock`, `TabIndex` (they describe placement inside a layout parent, which a top-level window doesn't have even in real WinForms), plus `Region` (matching `Control.Region`, also stored) and `FormCornerPreference` (no backend exposes window-corner or region clipping). Still missing: the MDI menu-merge members (`Menu`/`MergedMenu`/`MenuStart`/`MenuComplete`) — there is no Win32 menu handle to merge, same reasoning as the legacy-menu row below. Core lifecycle (`Load`, `Shown`, `Closing`, `ShowDialog`, `Show`) is solid. |
| `ToolStrip` | Partial | Real base class, and as of 2026-07-30 the real base of the whole strip family (`MenuStrip`, `ContextMenuStrip`, `StatusStrip`), as upstream. Most `ToolStrip`-level members are still stub properties (`Renderer`, `RenderMode`, `LayoutStyle`, `GripStyle`, `Stretch`, `CanOverflow`, ...) — set and read back, but nothing consumes them. Missing `Items`-level layout events (`LayoutCompleted`) and `GetItemAt`/`GetNextItem`. Items added to its `ToolStripItemCollection` do forward into the collection layout/rendering/hit-testing actually consume. |
| `MenuStrip`, `ContextMenuStrip`, `StatusStrip` | Partial | `ToolStrip`-derived as of 2026-07-30 (see the systemic note above), so `ToolStrip`'s whole member surface is now reachable from all three — but the `ToolStrip`-level members it brings are still stubs that nothing consumes (`Renderer`/`RenderMode` don't change painting, `LayoutStyle` doesn't change layout, `GripStyle`/`Stretch`/`CanOverflow` have no effect). What genuinely works is the behavior each already had: `MenuStrip` is a real top-docked bar (horizontal-expand layout, hover-opens-drop-down, `MenuRenderer`), `ContextMenuStrip` is a real popup with the `Opening`→`Opened`→`Closing`→`Closed` lifecycle, and `StatusStrip` paints its items through `StatusStripRenderer` (whose item positions are now the laid-out bounds, so a status item's clickable region matches where it's drawn — while `StatusStrip` derived straight from `Control` nothing laid its items out and they could never be hit-tested at all). `ContextMenuStrip.Show(Point)` used to be an empty override shadowing the working inherited overloads and silently doing nothing — it is now real (it anchors to the source control's window, else the active window, and throws `InvalidOperationException` if no window is open). Note `MenuStrip.Items`/`ContextMenuStrip.Items` are `MenuItemCollection`, not upstream's `ToolStripItemCollection` — deliberately, since that is the collection the layout, renderers and hit-testing consume; `StatusStrip.Items` is a `ToolStripItemCollection` that forwards into it. Still missing per-type: `MenuStrip.MdiWindowListItem` is a plain property with no MDI window list behind it. |
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
the SkiaSharp interop seam (`CreatePaint`, `GetSKBitmap`, `ToSKPath`, `ImageAttributes.ToSKColorFilter`,
...) without that seam becoming public API.

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

> **Superseded for gap-finding (2026-08-01).** That member-level pass now exists and is automated:
> [`tools/Majorsilence.Forms.ApiDiff`](tools/Majorsilence.Forms.ApiDiff) diffs this surface against the
> real `System.Drawing.Common` by reflection, keeps a committed `baseline.txt`, and fails CI if the gap
> set grows. It found gaps this type-level table could not see — members missing from types listed here
> as implemented, and members that existed but were hollow. The remediation plan is
> [`docs/gdi-gap-plan.md`](docs/gdi-gap-plan.md); the rows below have been corrected where it disagreed
> with them. Treat the tool's baseline as the authoritative gap list and this table as the narrative.

This audit was run against a since-fixed split: at the time, the shipping GDI+ implementation lived
inline in `src/Majorsilence.Forms/Drawing/` while the separately-packaged
`Majorsilence.Forms.Drawing.Common` project sat unreferenced and less complete. The two have since
been consolidated (see [above](#system-drawing--gdi)) — the table below still reflects the
consolidated, more-complete surface (the merge kept whichever of the two implementations was ahead
per member, so the findings below hold, with one exception noted in its row: multi-stop gradient
blend support was folded in during that consolidation and is no longer purely missing).

**Update:** most of the gaps this audit found have since been closed — `ImageAttributes`/`ColorMatrix`,
`BitmapData`/`LockBits`, the font collections, `Blend`/`ColorBlend`, `GraphicsPathIterator` and
`GraphicsContainer` are all implemented against SkiaSharp; `CustomLineCap` and `PenAlignment` are
partial for reasons stated in their row; `SystemBrushes`/`SystemPens` turned out to already exist and
were completed and cached. The table below reflects the current state, with each row saying exactly
what is and is not backed by real rendering.

| Type / area | Status | Notes |
|---|---|---|
| `Bitmap`, `Image`, `Icon`, `Graphics` (core drawing), `Pen`/`Pens`, `Brush` family (`SolidBrush`, `HatchBrush`, `LinearGradientBrush`, `PathGradientBrush`, `TextureBrush`, `Brushes`), `Region`, `Font`/`FontFamily`/`FontStyle`, `StringFormat` family, `Matrix`, `GraphicsPath`, `BufferedGraphics`/`Context`/`Manager`, `RotateFlipType`, `GraphicsUnit`, `Drawing2D` core enums (`SmoothingMode`, `CompositingMode`/`Quality`, `InterpolationMode`, `PixelOffsetMode`, `LineCap`/`LineJoin`, `DashStyle`, `WrapMode`) | Implemented | The mainstream drawing/imaging/text-layout path a migrated `OnPaint` override uses. `ImageAnimator` was listed here until the member-level audit; see its own row below. `Region` gained its full combine algebra (`Xor`/`Complement`/`Translate`/`Transform`/`IsInfinite` and the `Region`/`GraphicsPath`/`Rectangle` overloads) and `TextureBrush` its `Image`/transform/`Clone` surface in the same pass — both were thinner than this row implied before it. |
| `Graphics.MeasureCharacterRanges` + `StringFormat.SetMeasurableCharacterRanges` + `CharacterRange` | Implemented | Returns one `Region` per range, with one rectangle per line when a range wraps, as GDI+ does. Wrapping is greedy word wrap against the layout rectangle's width; horizontal `StringFormat.Alignment` is not applied to the measurement. Until this landed, `SetMeasurableCharacterRanges` stored ranges that nothing read. |
| `ImageAnimator`, `FrameDimension`, `Image.GetFrameCount`/`SelectActiveFrame`/`FrameDimensionsList` | Implemented | Multi-frame decode is real: `SelectActiveFrame` decodes the requested frame through `SKCodec`, and `ImageAnimator` tracks per-image frame state and advances it, so animated GIFs animate. Frames advance when the caller calls `UpdateFrames` (System.Drawing's own pull model, driven by a control's paint loop) rather than from a background timer, and per-frame delays stored in the file are not honored. `SaveAdd` remains a no-op: the Skia encoders write one image per file. |
| `ImageAttributes`, `ColorMatrix`, `ColorMap`, `ColorMatrixFlag`/`ColorAdjustType` | Implemented | Real color-transform path for `Graphics.DrawImage`. `SetColorMatrix` and `SetGamma` become an `SKColorFilter` on the draw paint (composed when both are set); `SetColorKey` and `SetRemapTable` are per-pixel lookups, so they are baked into a temporary copy of the source bitmap before drawing. `ColorMatrix` is the GDI+ 5x5 row-vector layout (`Matrix00`..`Matrix44` plus indexer) transposed into Skia's 4x5 row-major array — Skia 3.x normalizes the whole matrix *including* the translation column to 0..1, the same convention GDI+ uses, so no 0..255 rescaling happens (asserted by `ImageAttributesTests`). `Graphics.DrawImage(Image, Rectangle, float, float, float, float, GraphicsUnit, ImageAttributes)` and its `int`/`srcRect`/whole-image siblings live on `Graphics` in `Majorsilence.Forms` (see [above](#system-drawing--gdi) for why) and call the `internal` `ImageAttributes.ToSKColorFilter()` seam. Not applied: the separate gray matrix from `SetColorMatrices` and the per-`ColorAdjustType` category split (both stored and round-tripped); `SetWrapMode` is stored — the draw path always clamps. |
| `GraphicsPath.AddString`, `Flatten`, `Reverse`, `Warp`, `AddPie`, `AddClosedCurve`, `PathTypes`/`PathData`, markers, `IsOutlineVisible`; `FontFamily` metrics (`GetCellAscent`/`GetCellDescent`/`GetEmHeight`/`GetLineSpacing`) | Implemented | `AddString` builds real glyph outlines via `SKFont.GetTextPath`, laid out from the top-left corner as GDI+ defines it, so text can be filled, stroked or hit-tested as geometry. The font metrics are computed from the real typeface in design units rather than estimated. Fixed alongside: `Graphics.DrawPath`/`FillPath` previously replayed a path as a polyline built from `PathPoints`, discarding curves, the path's `FillMode` and the pen's dash/cap/join/brush; both now use the real path. `StringFormat` tab stops and digit substitution round-trip but are not applied by the text path. |
| `GraphicsPathIterator`, `GraphicsContainer` (`BeginContainer`/`EndContainer`), `PathPointType` | Implemented | `GraphicsPathIterator` walks the real `SKPath` (raw iterator) into GDI+ point/type arrays — `Count`, `SubpathCount`, `NextSubpath`, `NextPathType`, `NextMarker`, `HasCurve`, `Enumerate`, `CopyData`, `Rewind`, and the `GraphicsPath`-filling overloads all work. Skia's quads/conics (what ovals, arcs and round-rects actually store) are elevated to cubics, so callers only ever see `Start`/`Bezier` types in groups of three, exactly as GDI+ reports. `NextMarker` returns the whole path once: `GraphicsPath` has no marker API, and that is what GDI+ itself returns for a marker-free path. `BeginContainer`/`EndContainer` reuse the same `SKCanvas` save-count stack as `Graphics.Save`/`Restore` (both of which became real rather than no-ops in the same pass), so the two nest freely; the `dstrect`/`srcrect` overload really does clip and map the coordinate space. |
| `CustomLineCap`, `AdjustableArrowCap`, `PenAlignment` (`Pen.Alignment`, `Pen.CustomStartCap`/`CustomEndCap`) | Partial | The types are real and every property round-trips; `AdjustableArrowCap` builds a genuine triangular `GraphicsPath` outline (reachable via `FillPath`) that resizes with `Width`/`Height`. What does *not* happen is Skia stroking the custom outline at each line end — `SKPaint` only offers butt/round/square caps — so a pen with a custom cap strokes using that cap's declared `BaseCap` instead of being ignored outright. `Pen.Alignment` is likewise stored but not applied: an inset/outset stroke needs the geometry offset, which `SKPaint` cannot express. |
| `Blend`/`ColorBlend` (`SetBlendTriangularShape`/`SetSigmaBellShape`) | Implemented | The real GDI+ data types exist (`Blend.Factors`/`.Positions`, `ColorBlend.Colors`/`.Positions`) and both shape presets are implemented against the documented algorithms — triangular is the piecewise-linear ramp peaking at `focus`, sigma-bell is a cumulative-normal (erf-based) falloff sampled at 256 points. Both feed the existing multi-stop plumbing rather than a second path: setting `Blend` expands the factors into color stops, and setting `InterpolationColors` clears `Blend`. Available on `LinearGradientBrush` and `PathGradientBrush`. **Breaking change:** `LinearGradientBrush.InterpolationColors` is now a `ColorBlend` (it was a bare `Color[]`) to match upstream — the `Color[]`/`float[]` pair is still reachable through `ColorBlend.Colors`/`.Positions` and the `InterpolationPositions` convenience property, which shares the same storage. |
| `BitmapData` (`Bitmap.LockBits`/`UnlockBits`), `ImageLockMode` | Implemented | Real bulk pixel-buffer access. `LockBits` hands back a freshly-allocated buffer in the requested layout with a GDI+-correct 4-byte-aligned `Stride`, and `UnlockBits` copies it back unless the lock was `ReadOnly`. This copies rather than pointing straight into the `SKBitmap`, deliberately: the Skia backing store is premultiplied 32bpp, while `Format32bppArgb` is defined as straight alpha, so handing out the raw pointer would quietly hand out the wrong pixels. `Format32bppArgb`, `Format32bppPArgb`, `Format32bppRgb` and `Format24bppRgb` are laid out directly; narrower formats widen to `Format32bppArgb` and `BitmapData.PixelFormat` reports what was actually produced. Sub-rectangle locks, double-lock detection and lock cleanup on `Dispose` all behave as GDI+ does. |
| `PropertyItem` (`Image.PropertyItems`) | Implemented | EXIF is parsed directly out of the JPEG APP1/TIFF IFD structure (`ExifReader`), since SkiaSharp exposes only the orientation tag. Covers the primary IFD and the EXIF sub-IFD, which is where the commonly-requested tags live; PNG text chunks, GPS sub-IFDs and maker notes are not parsed. `SetPropertyItem`/`RemovePropertyItem` edit the in-memory set, which is not written back on `Save`. |
| `FontCollection`, `PrivateFontCollection`, `InstalledFontCollection` | Implemented | `PrivateFontCollection.AddFontFile`/`AddMemoryFont` (both the `IntPtr`/length GDI+ shape and a `byte[]` convenience overload) load real typefaces via `SKTypeface.FromFile`/`FromData`, following the same `SKData`-retention pattern `FontSubstitution` already uses for the embedded fallback fonts. Loaded families register process-wide, so `new Font(collection.Families[0].Name, size)` genuinely renders with the loaded font without it being installed; disposing the collection unregisters them again. `InstalledFontCollection` is backed by SkiaSharp's own `SKFontManager` enumeration, which *is* cross-platform (DirectWrite / CoreText / fontconfig) — no faking needed, and it returns exactly what `FontFamily.Families` returns. |
| `SystemBrushes`, `SystemPens` | Implemented | These already existed in `Majorsilence.Forms/SystemColors.cs` when the audit was taken (the row was a false positive from the type-level source-listing method — they live next to `SystemColors`, not in a file of their own), but they covered only ~half of `SystemColors` and allocated a new object per property read. Both now expose one property per `SystemColors` entry plus `FromSystemColor(Color)`, and each returns a cached instance, matching System.Drawing's process-wide singletons. |
| `ColorPalette` (`Image.Palette`) | Partial | The type and `Image.Palette` round-trip, and `ImageAttributes.GetAdjustedPalette` applies the remap table and color matrix to its entries. Assigning a palette does not re-quantize the image: modern SkiaSharp has no indexed bitmap type, so every surface here is 32bpp regardless. |
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
