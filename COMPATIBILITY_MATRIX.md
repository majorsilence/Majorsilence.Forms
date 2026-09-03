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

For putting native content (video, maps, a browser engine) inside a Majorsilence.Forms control, and
for what `Control.Handle`/`WindowBase.Handle` actually are, see
[`docs/native-interop.md`](docs/native-interop.md). The short version: **native handles are real only
at the window level and must never be faked** — `Control.Handle` is `IntPtr.Zero` because a control
here is paint operations on a canvas, not an OS window. `NativeControlHost` is the supported seam for
hosting native elements, and for video specifically a frame-callback surface drawn into Skia composites
properly where a hosted native element cannot.

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

### The cost of that policy, and the guard on it

A silent no-op is the hardest gap to find: it compiles, it runs, and the only symptom is wrong output
somewhere downstream. `Image.MakeTransparent` was one — ported code keyed a sprite's background colour
to transparent, the call did nothing, and every sprite drew with a white box behind it. Nothing threw
and there was nothing to grep for.

`NoOpStubBaselineTests` therefore pins the known set of empty-bodied public `void` methods in
`NoOpStubBaseline.txt` (161 at the time of writing). A newly added one fails the test, so accepting a
stub is a conscious act that includes recording it here. Shrinking that list is the goal; regenerate it
with `MAJORSILENCE_WRITE_STUB_BASELINE=1` after implementing or deliberately accepting an entry.

Gaps found by migrating real apps (RibbonWinForms, a WinForms game) and since fixed:

| Was | Symptom | Now |
| --- | --- | --- |
| `WindowBase` never raised `Paint` | `form.Paint += handler` compiled and never fired; a form that draws its own content rendered nothing | Raised after `OnPaint`, mirroring `Control.RaisePaint` |
| `Image.MakeTransparent` empty | Colour-keyed sprite sheets kept an opaque background | Implemented with GDI+ semantics (bottom-left key pixel, full-ARGB match, 32bpp conversion) |
| `TextBox.Text` accepted null | `NullReferenceException` from `TextBoxDocument.DisplayText`; `Control.Text` already coerced, the override bypassed it | Coerced to empty, as WinForms does |
| Measuring resolved fonts by family name only | A `PrivateFontCollection` font was drawn correctly but measured with the system fallback, so text was laid out to the wrong width; italic was dropped too | `TypefaceCache.Resolve` plus private-font lookup in `CachingFontMapper` |
| `CachingFontMapper` installed only by `Theme`'s static constructor | A pure measuring path silently got RichTextKit's built-in mapper, losing both the typeface cache and private fonts | Installed from `TextMeasurer`'s static constructor |
| `Application.AddMessageFilter` empty | The portable way to watch input application-wide did nothing, pushing ported code toward a global OS hook (`SetWindowsHookEx`, which aborts off Windows) | Filters registered and run for mouse/keyboard input before dispatch |
| `TextBoxBase.Undo`/`ClearUndo` empty | No undo (honestly reported, since `CanUndo` returned false) | Single-level undo on `TextBoxDocument` with Win32 toggle semantics and typing-run coalescing — reachable from code only, since Ctrl+Z is still not bound (`TXT-13`) |
| `Application.SetDefaultFont` empty | An app-wide default font was silently discarded | Sets the ambient default every unfonted control inherits |

## Core WinForms surface

Standard controls (`Button`, `TextBox`, `ComboBox`, `ListBox`, `ListView`, `TreeView`,
`DataGridView`, `Panel`, `SplitContainer`, `TabControl`, menus/toolbars/status bars, common
dialogs, etc.) are functionally implemented, not stubs — see them live in
[`samples/ControlGallery`](samples/ControlGallery), which has one demo panel per control. The
`*.Designer.cs`/`*.Designer.vb` code-behind pattern is preserved as-is; you don't rewrite your
designer-generated layout code.

**Visual designer:** there isn't one yet, and it is a wanted feature -- see
[Wanted soon: visual designer support](BACKLOG.md#wanted-soon-visual-designer-support). Designer
*code* migrates and runs; what is missing is a design surface to edit it in.
`Majorsilence.Forms.Design` supplies the design-time types (`ControlDesigner`, `UITypeEditor`,
`CollectionEditor`, `IWindowsFormsEditorService`, adorner glyphs) so a control library's own designers
compile and are preserved, but nothing instantiates them at runtime.

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

> **That baseline is now at zero, and it is the smaller half of the problem.** Every WinForms member
> upstream has, this layer declares. Whether the member *behaves* like WinForms is a separate question
> that no reflection diff can ask, and a twelve-area source audit (2026-08-25) found **483 places
> where it does not** — 41 of them severe enough to break or corrupt a common migrated app.
>
> **Phases 0–3 of that plan have since landed** (2026-08-26 → 08-28): the `ProcessCmdKey` keyboard chain
> is dispatched, focus and validation run through one choke point, forms are reusable and their dialogs
> real, the title bar is out of the client area, and text is measured at the size it is drawn at. The
> hollowness baselines moved with it — inert events 84 → 79, unraised events 130 → 120, stored-only
> properties 822 → 796. **Phase 4 (data binding) landed 2026-09-01**; what remains is most of phase 5
> (the per-control families) and the phase 6 sweeps. The rows below were corrected against the audit on 2026-08-31
> (item `W6.5`); a row saying a member exists is still not a promise that it behaves. See
> [`docs/behaviour-gap-plan.md`](docs/behaviour-gap-plan.md) and
> [`docs/behaviour-gap/`](docs/behaviour-gap/).

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

Three systemic patterns showed up across almost every control, worth stating once instead of
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
  events with no `On*` hook. Derived-type-specific hooks (`OnSelectedIndexChanged`,
  `OnCellPainting`, ...) are unchanged by this and are still mostly absent — see the per-row notes.
  `TabControl.OnDrawItem` is the exception that now works for real: setting `DrawMode` to either
  owner-draw value makes the tab strip raise `DrawItem` per tab (with the tab's bounds, index and
  selected/disabled/hot state) *instead of* painting its own tabs, as WinForms does.
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
  *Updated 2026-08-14 — the divergence is now measured and gated rather than discovered one compiler
  error at a time.* `ControlWindowParityTests` reflects over both surfaces and pins every `Control`
  member the window side lacks in `tests/Majorsilence.Forms.Tests/ControlWindowParityBaseline.txt`
  (200 entries today). Adding a member to `Control` without a `WindowBase`/`Form` counterpart now
  fails that test by name, so the hole is caught here instead of in somebody's ported application.
  Note the baseline is a *list of differences*, not a to-do list: `Dock`, `Anchor`, `Parent`,
  `TabIndex`, `Left`/`Top`/`Right`/`Bottom` and the rest of the placement-inside-a-parent surface have
  no meaning for a top-level window even upstream. The entries worth closing are the ones that
  describe a window as readily as a control — which is how `DeviceDpi`, `CreateGraphics`,
  `GetChildAtPoint`, `SetBounds`, `ResizeRedraw`, `OnHelpRequested`, `RecreateHandle` and
  `HandleDestroyed` came off it. Going the other way (making `Form` a `Control`) would mean merging
  `WindowBase` with the internal `ControlAdapter` that currently *is* the root control, which collides
  on coordinate space, on `Visible`/`Enabled` semantics, and on layout ordering.
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
- **Appearance properties are ambient, and drawing resolves them the same way the property does.**
  `BackColor`, `ForeColor` and `Font` each walk the control's own style chain first, then the parent
  chain, ending at the hosting window before falling back to the theme — which is what makes the
  common WinForms idiom of colouring a container once and letting its children pick it up work.
  *Added 2026-08-07 — `ForeColor` was the outlier:* it read only its own style chain, so a dark panel
  that set `ForeColor = White` still handed its buttons and labels the default dark text, i.e.
  invisible captions. Note the deliberate asymmetry with `BackColor`: an input surface (`TextBox`,
  `ComboBox`) pins its own background because WinForms gives it `SystemColors.Window`, so it stays
  light on a dark container.

- **Mouse capture belongs to the control that took it, for the whole gesture.** *Added 2026-08-07.*
  A control that captures on `MouseDown` receives every subsequent move and the release until the
  button comes up — over its own children included, which is what lets a drag begun on a container
  survive crossing a button sitting on it. Routing by hit-test after the press instead handed the
  move to whichever child the pointer crossed and silently ended the gesture; a window dragged by a
  custom title bar stopped dead at the caption buttons. A child that took the capture itself still
  wins over its ancestors.

Status below is scored from a migrating developer's point of view: **Implemented** means the
mainstream, commonly-used surface is there (gaps are limited to the two patterns above, or to
deep/rare corners); **Partial** names the specific commonly-used members that are missing;
**Missing** means the type doesn't exist under that name at all.

| Control / type | Status | Notes |
|---|---|---|
| `Control` (base) | Implemented | Inherited by every control below. The protected extensibility surface named in the first systemic pattern above is present and firing as of 2026-07-29; the residue is the stub events listed there that still have no `On*` hook. |
| `AutoSize` / `PreferredSize` / `Scale` wiring | Implemented | Real as of 2026-08-31; the layout engines (`DefaultLayout`, `FlowLayout`, `TableLayout`) were always a faithful port of upstream's, but four things failed to reach them. `Panel` overrode the *public* `GetPreferredSize` with a scan of where its children currently sat — so an `AutoSize` `FlowLayoutPanel`/`TableLayoutPanel` (both inherit it) never consulted its own engine, `proposedSize` was discarded (a wrapping panel reported one row), `Padding` was ignored and `MinimumSize`/`MaximumSize` never reached `PreferredSize`. `Control.Scale` bypassed `ScaleControl`, the documented DPI hook, so an app's override never ran and `Padding`/`Margin`/`MinimumSize`/`MaximumSize`/anchor distances kept their 96-DPI values while bounds grew. `Button`/`CheckBox`/`RadioButton` had no `GetPreferredSizeCore` at all, so `AutoSize = true` on a button did nothing (silently — it read back `true`) and the wrong size propagated up through any auto-sized container. `GroupBox.AutoSize` was a `new`-shadowed auto-property that never reached the layout state, so `((Control)gb).AutoSize` disagreed with `gb.AutoSize`. All four now go through the engine. Not covered: a top- or bottom-centred `GlyphAlign` on a check box measures its glyph as a side column. |
| `AutoScaleMode` / `AutoScaleDimensions` (`Form`, `ContainerControl`, `UserControl`) | Implemented (Font mode) | Real as of 2026-08-31; stored-only before that, which mattered because **every** designer file records the font dimensions it was laid out with. A form recorded on Windows at 6px per character was laid out here at nearer 7 with nothing compensating: clipped labels, ellipsized button captions, table columns too narrow. `Font` mode now scales the container and its children once — a form before its window opens, a container/user control on the layout following an `AutoScaleMode`/`AutoScaleDimensions`/`Font` assignment — by `CurrentAutoScaleDimensions / AutoScaleDimensions`. An unrecorded `AutoScaleDimensions` (the normal case for a form built in code) scales nothing, and neither does a ratio of 1. **`Dpi` mode is deliberately inert**: `Bounds` here are logical and the backend already applies the display's factor, so a dpi/96 ratio on top would scale every form twice — `CurrentAutoScaleDimensions` still reports the device DPI honestly. A font assigned after the first pass rescales by the difference (so `Application.SetDefaultFont` is honoured), except on a `Form` already shown, which keeps the size it opened at. |
| Keyboard pre-processing (`ProcessCmdKey`, `ProcessDialogKey`, `IsInputKey`, `ProcessMnemonic`, `KeyPreview`) | Implemented | Added to this table 2026-08-31 because its absence had no row at all. Until 2026-08-26 every member of the chain was `=> false` with no caller, so `override ProcessCmdKey` — the standard way a WinForms form claims a shortcut — compiled and never ran, and no menu shortcut, mnemonic or dialog key reached anything. The chain now runs in upstream's order: `ProcessCmdKey` up the parent chain, then `IsInputKey` on the focused control, then `ProcessDialogKey`, then `KeyDown`/`KeyPress`, with `ProcessKeyPreview` consulted on the form when `KeyPreview` is set. `e.Handled`/`SuppressKeyPress` really suppress downstream processing, so `KeyPress` filters work; `ToolStripMenuItem.ShortcutKeys`, legacy `MenuItem.Shortcut`, `&`-mnemonics, F10/Alt, `AcceptButton`/`CancelButton` are all resolved through it. Not yet bound: Ctrl+Z (`TXT-13`). |
| `Button`, `CheckBox`, `RadioButton`, `Label`, `LinkLabel`, `PictureBox`, `Panel`, `GroupBox`, `TabControl`/`TabPage`, `FlowLayoutPanel`, `TableLayoutPanel`, `TrackBar`, `ProgressBar`, `ScrollBar`/`HScrollBar`/`VScrollBar`, `Splitter`, `UserControl` | Implemented | Only the systemic gaps above; no missing members specific to these types. They pick up the whole `Control` protected surface by inheritance. `Button.TextAlign`/`ImageAlign` default to `MiddleCenter` as WinForms' `ButtonBase` does (as of 2026-08-07); `CheckBox`/`RadioButton` keep WinForms' `MiddleLeft`. `Label.AutoSize` really measures the text and resizes to it, growing *and* shrinking (as of 2026-08-07): `GetPreferredSize` used to return the size the label already had, so an auto-sized label kept whatever the designer left on it — and an over-wide label is opaque to the mouse, so it swallows the clicks of the container underneath. |
| `TextBox` | Implemented | Full surface for get/set/select/undo usage. `BorderStyle` and `TextAlign` drive rendering rather than only storing a value (as of 2026-08-07), and a single-line box centres its text vertically the way a Win32 `EDIT` without `ES_MULTILINE` does; multiline still starts at the top. As of 2026-09-03 (`W5.11`) five more properties do something: `CharacterCasing` converts typed, pasted and assigned text; `ShortcutsEnabled = false` refuses Ctrl+C/X/V/A; `WordWrap = false` stops a multiline box wrapping and gives it a horizontal scrollbar (except centre/right-aligned, which still wraps — the layout engine needs a right edge to align against); `ScrollBars` decides whether a bar appears at all, where previously **no** box ever showed one, because a `new` shadow hid the working base property; and `HideSelection` decides whether the selection is *painted*, the selection itself now surviving focus loss so an Edit menu or Find dialog can still act on it. Two crashes fixed with them: typing into a box whose text is already longer than `MaxLength` (`TXT-05`), and the caret walking into `PlaceholderText` and taking the next keystroke down with it (`TXT-06`). Undo works when called but is unreachable from the keyboard — Ctrl+Z is not bound in the key chain (`TXT-13`, noted 2026-08-31). |
| `RichTextBox` | Partial | No `Undo`/`Redo`/`CanUndo`/`CanRedo`/`RedoActionName`, no `SelectedRtf`, no `CanPaste`, no `AutoWordSelection`. |
| `MaskedTextBox` | Implemented | **The mask is enforced as of 2026-09-02** (`W5.13`), through the BCL's own `System.ComponentModel.MaskedTextProvider` — the same engine upstream uses. Typing places characters at fixed mask positions and rejects what does not fit (raising `MaskInputRejected` per rejected character); Backspace blanks a position back to its prompt rather than shortening the field; the field now displays its prompt characters, so a masked box no longer looks like a plain `TextBox`; `Text` reports the provider's value under `TextMaskFormat` (default `IncludeLiterals`, so an app parsing `"(555) 123-4567"` gets its separators back) and assigning runs through the provider; `MaskCompleted`/`MaskFull` answer from it, so the documented `if (!mtb.MaskCompleted)` guard works. `OnValidating` runs type validation and raises `TypeValidationCompleted` with a handler-honoured `Cancel`, and `UseSystemPasswordChar` forwards to the `TextBox` that implements it instead of shadowing it — a PIN box used to show its contents in clear text. Note `Text` and the displayed string differ here, unlike Win32 where the edit control's text *is* the display: `DisplayedMaskText` is the prompt-and-literal string on screen. An empty `Mask` means plain `TextBox` behaviour. Still missing: `InsertKeyMode`/`IsOverwriteMode` overwrite handling, `HidePromptOnLeave`, and the `GetCharIndexFromPosition` family. |
| `ComboBox`, `ListBox`, `CheckedListBox` — selection and check state | Implemented | Real as of 2026-09-02 (`W5.7`/`W5.8`), and previously the worst kind of gap: the state was right and the notification never happened. Every selection path except the `SelectedIndex` setter — `SelectedItem =`, `SetSelected`, `ClearSelected`, Ctrl-click, Space, Shift+arrow, and the `SelectionMode` setter — changed the selection through the collection's internal setters and raised nothing, so in a multi-select list the "N items selected" label, the enabled state of Delete/Move and any `SelectedItems`-driven detail view never updated from user input (`LST-03`/`LST-04`, both P0). `SetSelected` also swallowed an out-of-range index and a `SelectionMode.None` list where upstream throws, turning a caller's off-by-one into a selection that silently did not happen. On `ComboBox`, `SelectedIndex = -1` announced nothing — a "Clear filter" button left every dependent control showing the old choice (`LST-06`) — and `TextChanged` never fired at all, which is what validation, dirty-tracking and a `Binding` on `Text` listen to (`LST-09`). `CheckedListBox` drew no check boxes and nothing toggled one, so a migrated permissions or options dialog was an ordinary list the user could not tick and `CheckedItems` stayed empty unless code pre-checked it (`LST-02`, P0); its `SelectedItem` also handed back the internal wrapper, so `(Role)clb.SelectedItem` threw `InvalidCastException` (`LST-16`). All of the above now works, including the cancellable `ItemCheck` on user clicks. |
| `ComboBox`, `ListBox`, `CheckedListBox` | Partial | Data-binding format hooks missing (`Format`/`FormatString`/`FormatInfo` and their `*Changed` events), no `DataSourceChanged`/`DisplayMemberChanged`/`ValueMemberChanged`, no `Sort()` on `ListBox` (`PreferredHeight` does exist — `MidSizeControlParity.Three.cs:238`; corrected 2026-08-31). `DataSource`/`DisplayMember`/`ValueMember` themselves *do* exist. Corrected 2026-09-03: this row used to end by naming the checkbox and selection-event gaps as the two that outweighed the missing members; both were closed on 2026-09-02 by `W5.7`/`W5.8` and are described in the row above. What remains here is the missing-member list. |
| `ComboBox` — editable region, `Text` and autocomplete | Partial | Real as of 2026-09-03 (`W5.10`). The region is a child `TextBox`, so a `DropDownStyle.DropDown` combo — the default, and previously indistinguishable from `DropDownList` — can be typed into, and `SelectionStart`/`SelectionLength`/`SelectedText`/`MaxLength`/`Select`/`SelectAll` act on it instead of storing ints that only read each other (`LST-07`). Typing raises `TextUpdate` then `TextChanged`; Enter commits typed text through the `Text` setter, so a typed item name selects it. `Text` follows upstream (`LST-08`): the getter answers `Control.Text` rather than the selected item's text, so restoring a saved free-text value no longer shows the previously selected entry; `Text = null` clears the selection; a value matching no item keeps the text. `AutoCompleteMode.Append`/`SuggestAppend` complete inline against `AutoCompleteSource.ListItems` or `CustomSource`. **Still missing:** `Suggest`'s filtered drop-down and `Simple`'s always-visible inline list — both need a presentation list separate from `Items`, because a combo's items *are* the popup list's items here — and the OS-backed `AutoCompleteSource` values (`FileSystem`, `HistoryList`, …), which complete nothing. |
| `ListView` | Partial | **`View` is real as of 2026-09-01** (`W5.6`), which retires what the audit called the largest single visual divergence in the layer: `Details` draws a header from `Columns` and one row per item with a cell per column — honouring `Width` (including the `-1`/`-2` autosize sentinels), `TextAlign`, `GridLines`, `FullRowSelect` and `CheckBoxes` — and `List`/`SmallIcon` draw single-line rows, where every mode used to render as a 70px icon tile showing column 0 only, with no header and every subitem invisible. `ListViewSubItem.Bounds` reports a real cell. Vertical scrolling works (`EnsureVisible`, `TopItem`, `CountPerPage`, the wheel); `Items[i].Selected` and `.Checked` announce (`SelectedIndexChanged`, `ItemSelectionChanged`, cancellable `ItemCheck` then `ItemChecked`), `MultiSelect = false` is honoured, Ctrl/Shift extend a selection, and `ColumnClick`/`ItemActivate` fire — all seven of those events previously discarded their handlers. `Sort`/`ListViewItemSorter`/`Sorting` sort. Still missing: owner-draw (`OwnerDraw`, `DrawItem`/`DrawSubItem`/`DrawColumnHeader`), in-place label editing (`BeforeLabelEdit`/`AfterLabelEdit` are raisable but nothing edits), `ItemDrag` (raisable, no drag recogniser), virtual mode (`VirtualMode` is a plain property, and the retrieval events are stubs), groups' `TaskLink`/`CollapsedState`, and `InsertionMark`. |
| `TreeView` | Partial | W5.9 (2026-09-02) made the control behave: `SelectedNode` is null when nothing is selected and after the selected node is removed (`LST-05`, was P0); `GetNodeAt`/`HitTest` answer from the same layout the mouse is routed through, in the same coordinate space (`LST-21`, `LST-20`); `BeforeSelect`, `BeforeCollapse`, `BeforeCheck` and `AfterCheck` are real and cancellable, `AfterExpand`/`AfterCollapse` fire on programmatic `Expand ()`/`Collapse ()`, and `AfterSelect` reports `ByKeyboard` for keyboard navigation (`LST-23`); `NodeMouseClick` fires for the right button, so the context-menu pattern works (`LST-22`); `CheckBoxes`/`TreeNode.Checked` draw, click and toggle with Space (`LST-24`); images resolve from `Image`, then `ImageKey`/`SelectedImageKey`, then `ImageIndex`/`SelectedImageIndex` against the `ImageList` (`LST-25`); `Sorted`, `TreeViewNodeSorter` and `Sort ()` order the nodes (`LST-11`); per-node `ForeColor`/`BackColor`/`NodeFont`, `ItemHeight`, `Indent` and `ShowPlusMinus` all reach layout and painting (`LST-26`). Still stored-only: `ShowLines`, `ShowRootLines` and `LineColor` — the renderer draws no connector lines. No label editing (`BeforeLabelEdit`/`AfterLabelEdit`), no `NodeMouseHover`, no `ItemDrag`. |
| `DataGridView` | Partial | Still the largest single-type gap, but the highest-traffic hooks are real and firing as of 2026-07-29: **`CellFormatting`** (raised per cell during paint; honors `e.Value`/`FormattingApplied` and applies `e.CellStyle` for that frame only — the resolved style's `Format` string is now applied even with no handler attached), **`CellPainting`** (real per-cell event with live `Graphics`/`CellBounds`/`CellStyle`; `Paint`/`PaintBackground`/`PaintContent` run the grid's own painting, and `Handled`/`PaintParts` really suppress it), **`RowPrePaint`/`RowPostPaint`** (real args with `Graphics`, `RowBounds`, `InheritedRowStyle`, `State`, working `PaintCells*`/`PaintHeader`, and honored `Handled`/`PaintParts`), **`CellParsing`** (raised on edit commit; a `ParsingApplied` handler's typed value is what gets stored, in the cell and in the bound object), **`RowValidating`/`RowValidated`** plus `RowEnter`/`RowLeave` (run on current-row change and on focus loss via `ValidateCurrentRow()`; cancelling keeps the row current, and a pending edit commits first), **`GetClipboardContent()`** (returns a `DataObject` with tab-delimited text, CSV and an HTML table, honoring `ClipboardCopyMode`), and **`CellBorderStyle`/`AdvancedCellBorderStyle`** (+ the header variants — per-edge styles the renderer actually reads, so `None`/`*Horizontal`/`*Vertical` change the drawn grid lines). Still missing (declared for source compat but never raised): `RowsAdded`/`RowsRemoved`, `UserDeletingRow`/`UserDeletedRow`, `DataError`, `CellStateChanged`/`RowStateChanged`, `SortCompare`, `CellValueNeeded`/`CellValuePushed` (`VirtualMode` is a plain property), `DefaultValuesNeeded`/`NewRowNeeded`/`UserAddedRow`, `CellMouseDown`/`Up`/`Move`/`DoubleClick` and the per-column/row granular `*Changed` events (`ColumnWidthChanged`, `RowHeightChanged`, `ColumnDisplayIndexChanged`, ... — all still `add { } remove { }` stubs). Auto-sizing (`AutoResize*`, `AutoSizeColumnsMode`) still only invalidates — accurate, but it understates the traffic: `Fill` is what most designer-built grids use, and it leaves a blank band down the right-hand side (`DGV-18`). Corrected 2026-08-31 with two gaps this row did not mention. **Editing:** `BeginEdit(bool)`, per-column editor types, the dirty-flag family (`IsCurrentCellDirty`, `NotifyCurrentCellDirty`, `CurrentCellDirtyStateChanged`) and typed conversion via `ParseFormattedValue` are not implemented, `DataError` is swallowed rather than raised, and `ReadOnly` is not honoured on the edit path (`DGV-01`, P0). **Passive objects:** `Cell.Value` raises no `CellValueChanged` and does not repaint, `Row.Visible` does not hide the row, and `Row.Selected`/`Cell.Selected`/`Column.DisplayIndex` are auto-properties nothing reads (`DGV-02`/`DGV-20`, P0). |
| `DataGrid` (legacy) | Partial | Similar shape of gaps to `DataGridView`; present for basic bound-grid usage. |
| `DataGridViewColumn`/`Row`/`Cell` and the typed column family (`*ComboBoxColumn`, `*CheckBoxColumn`, etc.) | Partial | Core get/set works. `Clone()` is real on all three (returns the same runtime type, deep-copies styles, and a row clones its cells; the typed column/cell subclasses carry their own extra members across via `CopyStateTo`), as are `InheritedStyle` (the full WinForms cascade: grid → column → `RowsDefaultCellStyle` → `AlternatingRowsDefaultCellStyle` on odd rows → row → cell) and `State`/`InheritedState`. A row's `DefaultCellStyle.BackColor` is now actually painted (it outranks the alternating-row stripe); the rest of a row/column `DefaultCellStyle` still only reaches the renderer through a cell's own `Style` or a `CellFormatting` handler. Note the grid stores its own default styles as `ControlStyle`, so only the members the two style types share participate in the cascade (colors, alignment, format; not the Skia typeface). Still missing: custom-paint (`Paint`/`PaintCells`/`PaintHeader` on `Row`; `Paint`/`PaintBorder`/`PaintErrorIcon` on `Cell`) — the renderer paints cells directly rather than routing through the cell objects, so those would need the paint pipeline inverted; use the grid-level `CellPainting`/`RowPrePaint` hooks instead, which do the same job. Also missing: `GetPreferredSize`/`GetContentBounds`/`GetErrorIconBounds`, `AdjustColumnHeaderBorderStyle`, and cell-level `Detach`/`SetDataGridView` plumbing. |
| `DateTimePicker`, `MonthCalendar` | Stub UI — **no working date picker** | Corrected 2026-08-31; the previous row ("Partial", missing bolded dates and `DropDownAlign`) described theming gaps on a control that does not draw. `MonthCalendar.OnPaint` renders **one line of text** with the selected range and has no grid, no day cells and no mouse handling (`MonthCalendar.cs:255-263`, `SMP-42`, P0). `DateTimePicker` derives from `TextBox`, has no drop-down calendar behind its painted button, and its `Text` is free-form and never parsed back into `Value` (`SMP-39`/`SMP-40`, P0). Taken together there is **no date-picking UI in the framework**: migrated forms that rely on one need a substitute control until `W5.20` lands. Also still missing: the bolded-date API and `HitTest`. |
| `NumericUpDown`, `DomainUpDown` | Partial | No `BeginInit`/`EndInit` (`ISupportInitialize`), no `BorderStyle`, no `ParseEditText`/`UpdateEditText` overrides. Corrected 2026-08-31: `NumericUpDown` has **no keyboard input** and its arrows step by 1 regardless of `Increment` (`SMP-31`/`SMP-32`, P0); `DomainUpDown` derives from it and renders a number instead of its `Items` (`SMP-37`). Structurally neither derives from `UpDownBase`, as upstream does (`SMP-36`). |
| `SplitContainer` | Partial | Not `ContainerControl`-derived here, so no `ActiveControl`, `AutoValidate`, `ValidateChildren`. **`Orientation` changed meaning** — it is now the direction of the bar, as in WinForms; see [MIGRATION.md](MIGRATION.md#breaking-change-splitcontainerorientation). |
| `Form` | Partial | The plain-`Control` surface named in the `WindowBase` note above is present as of 2026-07-29, and mostly with real behavior rather than as stubs: **`Padding`** and **`RightToLeft`** forward to the root `ControlAdapter` (which *is* a `Control`), so padding genuinely insets docked/anchored children and children left on `RightToLeft.Inherit` resolve through the form the same way they resolve through a parent `Control`; **`AutoScroll`/`AutoScrollMargin`/`AutoScrollMinSize`/`AutoScrollPosition`/`SetAutoScrollMargin`** forward to that same root (a `ScrollableControl`), so they really scroll; **`MouseEnter`/`MouseLeave`** are really raised — every backend already reports pointer exit, and entry is inferred from the first pointer event to arrive, so they track the window's whole surface (chrome included) and fire once per entry rather than once per child; **`Parent`** reports the container's `MdiClient` while the form is hosted as an MDI child (matching upstream, where an MDI child's `Parent` is the `MdiClient`) and is a stored value otherwise, since a native top-level window can't be re-parented into a control tree. Stored stubs by design: `Anchor`, `Dock`, `TabIndex` (they describe placement inside a layout parent, which a top-level window doesn't have even in real WinForms), plus `Region` (matching `Control.Region`, also stored) and `FormCornerPreference` (no backend exposes window-corner or region clipping). Still missing: the MDI menu-merge members (`Menu`/`MergedMenu`/`MenuStart`/`MenuComplete`) — there is no Win32 menu handle to merge, same reasoning as the legacy-menu row below. Core lifecycle (`Load`, `Shown`, `Closing`, `ShowDialog`, `Show`) is solid. **`ContextMenuStrip` actually opens as of 2026-08-21** — it was a stored value nothing read, so a form's own menu never appeared on right-click even though its child controls' menus did; it and the legacy `ContextMenu` now forward to the root adapter, which is the surface a right-click on the form's background lands on. Validation is real as of 2026-08-21: `Validate()`, `Validate(bool)`, `Validating`/`Validated` and `ValidateChildren()` run an actual cycle through the root adapter and honour a handler's `Cancel` — all three were stubs returning `true` (or discarding handlers) before, so a form gating a button on `Validate()` always proceeded. The designer `Reset*` pattern (`ResetBackColor`/`ResetForeColor`/`ResetCursor`/`ResetFont`/`ResetRightToLeft`/`ResetText`) is present as of 2026-08-21, each clearing the same storage its property writes. Note `ProductName`/`CompanyName` are inherited from the window now, exactly as `Control` supplies them in WinForms — a form with its own domain `ProductName` needs `new`, same as it would there. `ImeMode`/`DefaultImeMode`/`ResetImeMode` forward there too, so children inherit the window's IME mode through the same chain they inherit a parent control's. Accessibility (`AccessibilityObject`, `AccessibleRole`, `AccessibleDefaultActionDescription`, `IsAccessible`, `CreateAccessibilityInstance`) is present as of 2026-08-21 and window-owned — a screen reader addresses the window, not its internal adapter — but it is a *described* surface, not a live one: nothing is published to a platform accessibility API yet, and `AccessibilityNotifyClients` is a deliberate no-op, exactly as `Control`'s is. Data binding is **live** as of 2026-08-21 rather than a stub: `DataBindings` binds the window's own properties, `DataContext` forwards to the root adapter so it is inherited by every child, and `Binding` really moves values — see the `Binding` row. `FormBorderStyle` is real as of 2026-08-07: `None` suppresses both chromes — the OS decorations and the `TitleBar` this library draws in their place — so an app that paints its own caption gets one title bar, not two; the fixed styles keep a caption but drop the resize grip. `UseSystemDecorations` still chooses *whose* caption it is. |
| `ToolStrip` | Partial | Real base class, and as of 2026-07-30 the real base of the whole strip family (`MenuStrip`, `ContextMenuStrip`, `StatusStrip`), as upstream. Most `ToolStrip`-level members are still stub properties (`Renderer`, `RenderMode`, `LayoutStyle`, `GripStyle`, `Stretch`, `CanOverflow`, ...) — set and read back, but nothing consumes them. Missing `Items`-level layout events (`LayoutCompleted`) and `GetItemAt`/`GetNextItem`. Items added to its `ToolStripItemCollection` do forward into the collection layout/rendering/hit-testing actually consume. Two members added to this row 2026-08-31 because neither was recorded anywhere: **`OverflowButton` is never assigned and returns null** (`ToolStripParity.cs:392`), so the documented `toolStrip.OverflowButton.Visible` idiom throws `NullReferenceException`; and `ToolStripManager.Merge`/`RevertMerge` return `false` without doing anything (`WinFormsCompat.cs:2966-2972`), so MDI-style menu merging silently does not happen. |
| `MenuStrip`, `ContextMenuStrip`, `StatusStrip` | Partial | `ToolStrip`-derived as of 2026-07-30 (see the systemic note above), so `ToolStrip`'s whole member surface is now reachable from all three — but the `ToolStrip`-level members it brings are still stubs that nothing consumes (`Renderer`/`RenderMode` don't change painting, `LayoutStyle` doesn't change layout, `GripStyle`/`Stretch`/`CanOverflow` have no effect). What genuinely works is the behavior each already had: `MenuStrip` is a real top-docked bar (horizontal-expand layout, hover-opens-drop-down, `MenuRenderer`), `ContextMenuStrip` is a real popup with the `Opening`→`Opened`→`Closing`→`Closed` lifecycle, and `StatusStrip` paints its items through `StatusStripRenderer` (whose item positions are now the laid-out bounds, so a status item's clickable region matches where it's drawn — while `StatusStrip` derived straight from `Control` nothing laid its items out and they could never be hit-tested at all). `ContextMenuStrip.Show(Point)` used to be an empty override shadowing the working inherited overloads and silently doing nothing — it is now real (it anchors to the source control's window, else the active window, and throws `InvalidOperationException` if no window is open). Note `MenuStrip.Items`/`ContextMenuStrip.Items` are `MenuItemCollection`, not upstream's `ToolStripItemCollection` — deliberately, since that is the collection the layout, renderers and hit-testing consume; `StatusStrip.Items` is a `ToolStripItemCollection` that forwards into it. Still missing per-type: `MenuStrip.MdiWindowListItem` is a plain property with no MDI window list behind it. |
| `ToolStripMenuItem`, `ToolStripButton`, `ToolStripLabel`, `ToolStripComboBox`, `ToolStripTextBox`, `ToolStripSeparator`, `ToolStripDropDownButton`, `ToolStripSplitButton`, `ToolStripStatusLabel`, `ToolStripProgressBar`, `ToolStripDropDown` | Partial | Core `ToolStripItem` surface (`Text`, `Image`, `Click`, `Enabled`, `Visible`) present; `ToolStripMenuItem.DropDown` is a real `ToolStripDropDown` view onto the item — its `Items` are the same collection as `DropDownItems` and its `Visible` reports whether the sub-menu is open, and setting it opens/closes the sub-menu. Missing accessibility (`AccessibilityObject`), drag/drop (`DoDragDrop`, `DragEnter`/`DragDrop`), and layout internals (`ContentRectangle`, `DefaultMargin`/`DefaultPadding`, `Placement`). Corrected 2026-08-31: `Enabled` and `Checked` are **`new`-shadowed** on `ToolStripItem`, `ToolStripButton` and `ToolStripMenuItem` (`WinFormsCompat.cs:1098`, `:1322`, `:1692`), so they store a value the renderer and the hit-test never read — a disabled menu item still looks enabled and still fires, and a checked one draws no check (`TSM-01`, P0; `TSM-04`). `Tag`, `Height`, `Alignment` and the item mouse events shadow the same way. |
| `ToolStripContainer` | Implemented | Only the systemic gaps above. |
| `MenuItem`, `ContextMenu`, `MainMenu` (legacy) | Partial | Basic construction/click/items work; MDI menu-merging (`MergeMenu`, `MdiListItem`, `FindMergePosition`) and Win32 handle interop (`Handle`, `CreateMenuHandle`) are absent — reasonable, since there's no Win32 menu handle to merge. |
| `Binding`, `BindingContext`, `CurrencyManager`, `PropertyManager` | Partial | **Live as of 2026-08-21** — until then `Binding` stored its property name and data source and did nothing with them (`Format`/`Parse` discarded their handlers, `ReadValue`/`WriteValue` were empty), so every binding in every migrated form silently moved no data. Now working: simple property-to-property binding over a scalar source, an `IList`/`DataView`/`DataTable`/`IListSource`, or a `BindingSource`; the initial pull on `DataBindings.Add`; source→control updates via `INotifyPropertyChanged` and via `CurrencyManager` position changes; control→source write-back through the `<Property>Changed` convention, honouring `DataSourceUpdateMode` (`OnPropertyChanged`/`OnValidation`/`Never`) and `ControlUpdateMode.Never`; real `Format`/`Parse` events; `FormatString`/`FormatInfo`, `NullValue` and `DataSourceNullValue`; and type coercion in both directions, where an unconvertible value (a half-typed number) is left alone rather than throwing mid-edit. A scalar data source now yields a `PropertyManager` rather than a `CurrencyManager` over a null list, which is why binding to a plain object works at all. **Phase 4 (2026-09-01) closed the correction this row carried since 2026-08-31**, and the four gaps it named before that. The `CurrencyManager` is one live object for the life of a `BindingSource` — built over the BindingSource itself, subscribed to `ListChanged` — so the designer's own ordering (`BeginInit` → `DataBindings.Add` → `EndInit` → fill the data in `Load`) works, list mutations move `Position`/`Current`, and `bs.ResetBindings`/`ResetCurrentItem` really refresh simple-bound controls. Member resolution is `TypeDescriptor` on both sides, so **`DataRowView` columns (the typed-DataSet form) bind and write back**. OnValidation bindings write inside `Validating` and cancel it on failure; `EndEdit`/`CancelEdit` commit/roll back every binding plus `IEditableObject`/`ICancelAddNew` (a Cancel button over a DataSet reverts, because the manager opens `BeginEdit` on the item that becomes current). A failed conversion **leaves the source alone** and resets the control instead of writing `default(T)`, reporting through `BindingComplete` when `FormattingEnabled`. `DataSource = typeof(T)` yields a typed list with a schema; a scalar source is wrapped; a `DataMember` over a non-DataSet source is real master/detail that follows the parent's current item. Still open: `BindingContext` re-homing when a bound control is parented later (`BND-15` — two controls bound to the same plain list before `Controls.Add` still get separate managers; a shared `BindingSource` avoids it), `DefaultDataSourceUpdateMode`/duplicate detection on `DataBindings.Add` (`BND-17`), computed `AllowNew`/`AllowEdit`/`AllowRemove` (`BND-22`), `ListControl` `Format`/`FormatString` (`BND-27`), and the `nullValue`/`formatString` `Binding` constructor overloads (`BND-33`). Binding is reflective by nature, so a trimmed app must root the types it binds. |
| `ListBox`/`ComboBox` data binding | Implemented | `DataSource`/`DisplayMember`/`ValueMember` track the source as of 2026-08-21 rather than snapshotting it at bind time: the control re-reads on `ListChanged`, and its selection and the source's current-item position move together in both directions through the shared `CurrencyManager`, so master/detail works. Before this, binding a control in `InitializeComponent` and filling the data afterwards — the normal designer shape — left it permanently empty. |
| `Graphics.DrawString` font fallback | Implemented | As of 2026-08-24 a solid-brush `DrawString` routes through the same RichTextKit path as `MeasureString` and the library's own renderers, so it falls back to a font that has the glyph — CJK, emoji and any other non-Latin text used to draw as tofu while measuring correctly. Gradient and texture brushes still take the direct Skia path and still lack fallback. |
| `GraphicsPath.AddString` font fallback | Implemented | As of 2026-08-24 `AddString` splits text into runs that each resolved typeface can actually render, instead of taking every outline from one `SKFont` — which rendered any codepoint that face lacked as a `.notdef` box. This is a second text path, parallel to `DrawString`: a library that draws all its text as filled glyph outlines (for sharper anti-aliasing than `DrawString` gives) routes **every** label through here, so with that mode on its entire UI was tofu for any non-Latin script while the same screen with the mode off rendered correctly. Fallback faces are resolved through the platform font manager and cached per codepoint, since text is laid out on every paint. The rectangle overloads also honour `StringFormat.Alignment`/`LineAlignment` as of the same date: they used to ignore the format and lay out from the rectangle's top-left, so every centred caption drawn through this path sat top-left of its box — card titles riding above their divider, button labels hugging the corner. Alignment is measured run by run with the same substituted faces the text is drawn with, so it cannot disagree with what appears. Still not applied: wrapping to the rectangle — a single line is laid out and aligned. |
| `Form.Dispose` closes the window | Implemented | As of 2026-08-25 disposing a form tears down its backend window and clears `Visible`, as WinForms does by destroying the handle — the window leaves the screen whether or not anything called `Close` first. It used to detach the form from `Application.OpenForms` and leave the window up with nothing painting into it, which mattered because popups are routinely dismissed by disposing them: closing one left a blank rectangle on screen that could not be got rid of. `FormClosing` is deliberately **not** raised — disposing is not closing, and a handler that cancels closing must not be able to veto a dispose. |
| `Control.Dispose` detaches from its parent | Implemented | As of 2026-08-24 disposing a control removes it from its parent's `Controls`, as WinForms does — a great deal of ported code depends on it, because the standard way to swap a page or panel is to dispose the old control and add the new one without removing the old one explicitly. It used to stay parented, so it remained in the collection and, if docked, went on filling its container: found in a control library's demo shell where the first page opened was the only one that ever showed, every later navigation adding its page behind the dead one. Explicit children only — implicit chrome lives in a separate list that `ControlCollection.Remove` does not touch and is owned by the parent that created it. The disposal walk over children now iterates a snapshot, since each child detaches itself on the way down. |
| Mouse-wheel delta units | Implemented | As of 2026-08-24 the Avalonia backend converts wheel movement into WinForms' units. Avalonia reports LINES (1.0 per notch on a mouse, a fraction per frame on a trackpad); WinForms reports multiples of `WHEEL_DELTA` = 120, and consumers either use the value directly as a pixel count or divide it by 120 with integer arithmetic. The backend cast the Avalonia value straight to an `int`, so one notch arrived as `1` instead of `120` and any sub-notch trackpad movement truncated to `0` and was dropped — scrolling ran at roughly a hundredth of its proper speed, a few pixels per notch. The remainder is now accumulated (`WheelDeltaAccumulator`) so only whole units are emitted and nothing is discarded, which is also what Windows does for a precision touchpad. |
| `Form.OnHandleCreated` | Implemented | As of 2026-08-24 this is actually raised, immediately before `OnShown`, at the moment `IsHandleCreated` becomes true. It was a documented-but-never-called empty method, while the `HandleCreated` **event** did fire (it is forwarded to the internal adapter) — so overriding the method looked right, compiled, and silently did nothing. It is the standard place ported code does its window-level setup once the window exists; found in a control library where every popup's rendering was gated on a flag set in that override, leaving all of them blank. Note the consequence when porting: overrides that were silently skipped now run, so Win32 work in them needs the same guarding as anywhere else. |
| `Form.BeginResizeDrag` | Implemented | Added 2026-08-24 as the public counterpart of `BeginMoveDrag`, which was public while this was reachable only from inside `Form`. Both exist so a borderless form can implement its own title bar and resize grips: on Windows that is done by faking a non-client mouse-down (`ReleaseCapture` then `WM_NCLBUTTONDOWN`), which has no equivalent off Windows, so ported code needs a managed way to ask for the same gesture. |
| `Graphics.MeasureString` with a zero layout width | Implemented | As of 2026-08-24 a `maxWidth`/`layoutArea` of zero or less means UNBOUNDED, which is GDI+'s convention — `MeasureString(text, font)` is itself specified as passing `SizeF(0, 0)`, so a library that funnels all its measurement through one constrained overload passes 0 to mean "no limit". The zero used to be taken literally and handed to the layout engine, which wrapped every string to one grapheme per line. That is not a small numeric error: auto-sized controls asked for the size of a vertical sliver — roughly one character wide and a dozen lines tall — so captions were clipped to a couple of letters and the controls grew far past the rows meant to contain them, leaving round buttons drawn as domes. A positive width still constrains and wraps as before. |
| `Graphics.Transform`, `MultiplyTransform`, `ResetTransform`, the `MatrixOrder` overloads | Implemented | As of 2026-08-24 the world transform is real. It was a stub: the getter returned a fresh identity `Matrix`, the setter and both `MultiplyTransform` overloads did nothing, and the `MatrixOrder` arguments were discarded. Nothing threw, so the standard save/modify/restore idiom — read `Transform`, apply a translation, draw, put the old one back — silently drew everything at the untransformed origin. The transform is tracked RELATIVE to the canvas matrix captured when the `Graphics` was created, because GDI+ keeps the device origin (for a control, its client origin) out of `Transform` while Skia has it in `TotalMatrix`; without that baseline, assigning a transform would move the drawing to the top-left of the whole surface. `ResetTransform` restores that baseline rather than Skia's absolute identity. `MatrixOrder.Append` composes through `Matrix` since the canvas can only prepend. |
| `Graphics.SetClip`/`ResetClip` interaction with `Transform` | Implemented | As of 2026-08-24 changing the clip no longer disturbs the world transform. The clip is emulated with Skia save/restore frames (Skia's clip only ever narrows, whereas `SetClip` replaces), and Skia's restore pops the MATRIX along with the clip — so replacing a clip silently reset the transform. In System.Drawing the two are independent. The matrix is now carried across the restore by hand. The failure was invisible in the common case, because code that clips usually is not also translating; where both were in play — an SVG renderer, which restores the clip after every element while a nested `<g transform>` is active — the first shape in each group landed correctly and every later sibling drew at the untransformed origin. |
| `Graphics.DrawImage` three-point (parallelogram) overloads | Implemented | As of 2026-08-24 these honour their `srcRect` and their `ImageAttributes` instead of always drawing the whole image with no colour adjustment. This matters beyond colour remapping: it is the overload GDI+ callers use to composite a layer at partial opacity, by handing it an alpha-scaling `ColorMatrix`. While the attributes were dropped, such a layer drew fully opaque — so an element asking for zero opacity rendered as a solid block of its own fill colour rather than vanishing. The rectangle-destination overloads already did this; the `PointF[]`/`Point[]` family now shares their implementation. |
| `DataGridView` scrollbars in `Controls` | Implemented | As of 2026-08-24 a grid's scrollbars are ordinary children in `Controls`, of types `VScrollBar`/`HScrollBar`, matching WinForms — code that scans a grid's `Controls` for them (to mirror their state onto skinned scrollbars, say) now finds them. This is deliberately per-control, not general: `ScrollableControl` keeps its own scrollbars private, because `new Panel().Controls` is empty in WinForms too. `NumericUpDown` exposes its up/down buttons as `Controls[0]`, the position and role WinForms' `UpDownBase` gives them, so the `Controls[0].Paint` theming idiom works; it has no edit child (`Controls[1]`), having no text editing to put in one. |
| `BindingSource` | Partial | This row was out of date: the sort/filter surface (`ApplySort`/`RemoveSort`/`IsSorted`/`SortDescriptions`, `Supports*`), the `AddingNew`/`DataError`/`CurrentItemChanged` events and the `List`/`CurrencyManager` accessors all exist. Fixed 2026-08-21, once live bindings made the gaps observable: `CurrencyManager` built a **new manager on every read**, so nothing shared a current item — it is now cached, kept in step with `Position` in both directions, and reached through `ICurrencyManagerProvider` so a control bound to a BindingSource follows `Position` instead of tracking a private position of its own. `AddNew` added a literal `null` to the caller's list and returned it; it now takes the item from an `AddingNew` handler, else the list's own `AddNew`, else the element type's parameterless constructor, and makes it current. `Find` always returned -1; it now delegates to a searchable `IBindingList` and otherwise walks the list — a deliberate divergence, since WinForms throws `NotSupportedException` there and "not found" was the worse answer. `EndEdit`/`CancelEdit` reach an `IEditableObject` current item. `Sort` parses the WinForms expression shape (`"Name"`, `"Name DESC"`, `"Name ASC, Age DESC"`) and applies it through the list, using `IBindingListView` for multi-property sorts; a list that cannot sort is left alone and says so through `SupportsSorting` rather than being reordered behind the caller's back. Still missing: `IBindingListView` on BindingSource itself, and `RemoveFilter` on a non-view list. |
| `BindingNavigator` | Implemented | Wired as of 2026-09-01 (this row previously overstated in the other direction — the items existed and every one was dead: stored-only properties, no `Click` handlers, position and count never updated, and `EndInit` **destroyed the designer's own items** by clearing and rebuilding the strip, so a custom Save button vanished and its handler held an orphan). Item setters now hook their action, the navigator displays and follows its `BindingSource` (position, count, enabled states), and `EndInit` refreshes without touching the items. Still inherits `ToolStrip`'s own gaps above. |
| `PropertyGrid` | Partial | Grid/property display and `SelectedObject` work; no category/commands-pane theming (`CommandsBackColor`, `CategorySplitterColor`, ...), no `PropertyTabs`, no `ToolStripRenderer`. |
| `WebBrowser` | Partial | Navigation (`Navigate`, `Url`, `DocumentTitle`, nav events) works; no DOM object model at all — `Document`/`DocumentStream`/`ObjectForScripting` and the whole `HtmlDocument`/`HtmlElement`/`HtmlWindow` family don't exist, because there's no MSHTML-equivalent behind it (it's backed by a real browser webview, not COM automation). |
| `ToolTip`, `Timer`, `ImageList`, `SplitButton` | Implemented | `SplitButton` isn't an upstream WinForms type (only `ToolStripSplitButton` is) — likely meant that. Minor gaps only (e.g. `ToolTip.OwnerDraw`/`Popup`). |
| `NotifyIcon` | Stub | Corrected 2026-08-31 (this row said "Implemented"): the properties store and read back, but **every event is `add { } remove { }`** — `Click`, `DoubleClick`, `MouseClick`, `MouseDoubleClick`, `MouseMove` and the `BalloonTip*` family (`NotifyIcon.cs:58-84`) — and no backend exposes a tray seam, so no icon appears in the notification area, `ShowBalloonTip` shows nothing, and a tray-only app has no way to be reopened. The source itself calls it a stub. |
| `ErrorProvider` | Stub | Corrected 2026-08-31 (this row said "Implemented"): `SetError`/`SetIconAlignment`/`SetIconPadding` record their values in dictionaries and **nothing renders** — there is no glyph beside the control and no blink (`SMP-51`, P0). A form whose validation reports errors only through an `ErrorProvider` shows the user nothing at all. |
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

The sub-namespace split mirrors GDI+ rather than flattening it: the brushes that upstream puts in
`System.Drawing.Drawing2D` — `LinearGradientBrush`, `PathGradientBrush`, `HatchBrush` and `HatchStyle` —
live in `Majorsilence.Forms.Drawing.Drawing2D`, while `Brush`, `SolidBrush` and `TextureBrush` (really
`System.Drawing` types) live in `Majorsilence.Forms.Drawing`. That is what makes a rewritten
`using System.Drawing.Drawing2D;` resolve the same set of types it did before the migration.

That namespace lives in a single project, [`src/Majorsilence.Forms.Drawing.Common`](src/Majorsilence.Forms.Drawing.Common),
which `Majorsilence.Forms` references and which also ships as its own package for consumers that want
the drawing layer without the control layer. Four files remain under `src/Majorsilence.Forms/Drawing/`
because they depend on the Forms layer and would otherwise form a circular project reference:
`Graphics.cs` (declares a partial of `Control`, and calls `Theme`/`TextMeasurer`), `SkiaGraphics.cs`
(`ContentAlignment`, `TextMeasurer`), `BufferedGraphics.cs` (typed throughout on that `Graphics`), and
`NrbfResourceReader.cs` (materialises `ImageListStreamer`). Each carries a header comment saying so.
The drawing project grants `InternalsVisibleTo` to `Majorsilence.Forms` so those four can keep using
the SkiaSharp interop seam (`CreatePaint`, `GetSKBitmap`, `ToSKPath`, `ImageAttributes.ToSKColorFilter`,
...) without that seam becoming public API. They are *namespaced* `Majorsilence.Forms.Drawing`
regardless of which project builds them — assembly and namespace are separate choices, and only the
assembly is constrained by the cycle. `Graphics` moved into that namespace on 2026-08-10, having been
the one `System.Drawing` type sitting in `Majorsilence.Forms`; that exception was what forced the
migrator to special-case the name, and a file that drew without naming a control type never imported
`Majorsilence.Forms`, so the name fell through to the type-forwarded (unreferenced) `System.Drawing.Graphics`
and failed with CS1069.

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

One correction to that, though (2026-08-31): **`PrintDocument.Print()` writes `%TEMP%/<DocumentName>.pdf`,
returns the path, and surfaces it to nobody** (`Printing/PrintDocument.cs:69`) — nothing opens it and
nothing spools it, so a user who clicks Print sees no output and no error (`SVC-29`). Only
`PrintPreviewDialog.ShowDialog()` launches a viewer. `PrinterSettings.InstalledPrinters` is empty and
`PrinterName` is `""` while `IsValid` reports true. The PDF pipeline is the intended substitute; what is
missing is the last step that hands the PDF to the user or the spooler.

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
| `FontCollection`, `PrivateFontCollection`, `InstalledFontCollection` | Implemented | `PrivateFontCollection.AddFontFile`/`AddMemoryFont` (both the `IntPtr`/length GDI+ shape and a `byte[]` convenience overload) load real typefaces via `SKTypeface.FromFile`/`FromData`, following the same `SKData`-retention pattern `FontSubstitution` already uses for the embedded fallback fonts. Loaded families register process-wide, so `new Font(collection.Families[0].Name, size)` genuinely renders with the loaded font without it being installed; disposing the collection unregisters them again. Measurement honours them too: `Graphics.MeasureString`/`TextRenderer.MeasureText` resolve through the same registry as the paint path (via `TypefaceCache.Resolve` and `CachingFontMapper`), so text is not laid out from a substituted face. `InstalledFontCollection` is backed by SkiaSharp's own `SKFontManager` enumeration, which *is* cross-platform (DirectWrite / CoreText / fontconfig) — no faking needed, and it returns exactly what `FontFamily.Families` returns. |
| `SystemBrushes`, `SystemPens` | Implemented | These already existed in `Majorsilence.Forms/SystemColors.cs` when the audit was taken (the row was a false positive from the type-level source-listing method — they live next to `SystemColors`, not in a file of their own), but they covered only ~half of `SystemColors` and allocated a new object per property read. Both now expose one property per `SystemColors` entry plus `FromSystemColor(Color)`, and each returns a cached instance, matching System.Drawing's process-wide singletons. |
| `ColorPalette` (`Image.Palette`) | Partial | The type and `Image.Palette` round-trip, and `ImageAttributes.GetAdjustedPalette` applies the remap table and color matrix to its entries. Assigning a palette does not re-quantize the image: modern SkiaSharp has no indexed bitmap type, so every surface here is 32bpp regardless. |
| `Metafile`/`MetafileHeader`/`EmfType`/`EmfPlusRecordType`/`MetaHeader`/`WmfPlaceableFileHeader`, Win32 handle interop (`H*`/`hdevmode`/`hdevnames`), the design-time converters | Present; mostly functional | *Updated 2026-08-04.* **Metafiles render.** EMF and WMF are parsed and replayed onto Skia by this layer's own record interpreters, so a metafile loaded from a file, stream or the clipboard draws in a `PictureBox`, through `DrawImage`, or onto a printed page like any other image — and re-renders when scaled, being vector data. Unknown records are skipped and counted (`UnsupportedRecordCount`); EMF+ records inside an EMF are ignored, which is what a downlevel GDI renderer does. Metafile *recording* still throws. The converters (`FontConverter`, `IconConverter`, `ImageConverter`, `ImageFormatConverter`, `MarginsConverter`), `Font.FromLogFont`/`ToLogFont` and `Region.GetRegionData` are fully functional and round-trip. Every member that would have to produce or read a Win32 handle throws `PlatformNotSupportedException` naming the alternative — a zero handle would corrupt silently in the caller's next P/Invoke. See [the GDI plan](docs/gdi-gap-plan.md#phase-10--metafile-playback). |
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
| Avalonia / browser, Android, iOS (single-view) | — (`AvaloniaWebViewHandle` is excluded from these TFMs) | Placeholder label (`AllowSystemViewerFallback` defaults off here — `Process.Start`/`UseShellExecute` has nothing to service on these platforms) | `RichTextBox` fallback |
| Headless backend | — (no `IWebViewFactory` at all) | Caches the document and paints a placeholder; never shells out to a system viewer (so CI/automated tests never spawn OS processes) | `RichTextBox` fallback |

## Visual styles (`System.Windows.Forms.VisualStyles`)

`VisualStyleRenderer` and `VisualStyleElement` exist so that code which draws a themed part — a status
bar's resize grip, a themed arrow — compiles and runs, but there is **no msstyles theme engine** off
Windows to draw with, so `VisualStyleRenderer.DrawBackground` is a no-op and
`VisualStyleRenderer.IsSupported` / `IsElementDefined` report `false`. That pair is how WinForms code is
already written to decide whether to theme or fall back to its own painting, so code that checks first
takes its fallback path and never reaches the no-op. `VisualStyleElement` carries the element groups the
compat layer has been asked for so far, under the upstream nested-class names.

`VisualStyleInformation` describes the style in force, and there is none: `IsSupportedByOS` and
`IsEnabledByUser` report `false` and the descriptive members (`ColorScheme`, `DisplayName`, `ThemeFilename`,
…) return empty strings. Those are the two answers callers actually branch on — the upstream pattern is
`if (VisualStyleInformation.IsEnabledByUser)` or a test of `ColorScheme` for emptiness, followed by a
palette of the caller's own — so reporting "no theme" routes them to the path that renders correctly here.
The two colour members are real: `TextControlBorder` and `ControlHighlightHot` answer from
`SystemColors`, so a control outlining a box gets a border matching the palette actually in force.

## Asking the OS about itself

Three small areas where WinForms reaches Win32 and this layer answers from what it can genuinely see.

**`OSFeature`** (and its `FeatureSupport` base) reports every optional feature **absent** —
`GetVersionPresent` returns `null` and `IsPresent` returns `false`. For the two that are asked for in
practice that is the true answer: per-pixel window alpha is not implemented (`Form.AllowTransparency`
stores its value and does nothing with it) and there is no msstyles engine. It is also the useful
direction to be wrong in, which is why the type is worth having rather than stubbing at the call site:
code testing for layered windows does so to choose between an alpha-blended effect and a plain one, so
`null` routes it to the one that actually draws.

**`InputLanguage`** cannot enumerate keyboard layouts — that is a Win32 call with no cross-platform
equivalent — so it answers from the culture instead. `CurrentInputLanguage` is the current culture
(settable, and the setting is remembered, but it does not switch the OS layout);
`InstalledInputLanguages` lists the current and installed-UI cultures, de-duplicated; `LayoutName` gives
the culture's English name rather than inventing a layout identifier; and `Handle` is `IntPtr.Zero`,
because there is no HKL. That is enough for what callers do with it — naming the language the user is
working in.

**`Majorsilence.Forms.Media.SystemSounds` and `SoundPlayer`** replace their `System.Media` namesakes,
which live in a Windows-only assembly. Playback is **real** as of 2026-08: it routes through the
operating system's own playback utility (`afplay` on macOS, `paplay`/`aplay` on Linux, PowerShell's
`System.Media` on Windows — see `Media/NativeAudio.cs`). The child process gives the API its upstream
semantics for free: `Stop` kills it, `PlaySync` waits for it, `PlayLooping` respawns it until stopped,
and a `Stream` is materialised to a temporary .wav once and deleted on dispose. The trade is ~50–200ms
of launch latency per play — these APIs serve alert sounds and short cues, which is also their upstream
contract (SoundPlayer is WAV-only even in WinForms). What stays deliberately silent: platforms with no
utility to spawn (mobile/browser, until a backend supplies a native path — `NativeAudio` is the seam),
URL sound locations (no implicit fetching), and any failure at all (missing utility, dead audio daemon,
unplayable file) — fire-and-forget APIs degrade to silence, never to an exception. `Load`/`LoadAsync`
still complete immediately: the OS utility opens the file itself, so there is nothing to preload. The
migrator redirects `System.Media` here, because the bare namespace resolves off Windows and every type
reference in the file then fails as an unknown name — far more confusing than a missing namespace.

## Design-time smart tags

`DesignerActionUIService` exists so that the guarded calls around it compile: a component's action list
reaches for it after changing a property, so the smart-tag panel redraws with the new state. `Refresh`,
`ShowUI` and `HideUI` are **no-ops** and `ShouldAutoShow` returns `false` — there is no panel to refresh.
In practice the calls are never made at all: the service is requested through
`GetService(typeof(DesignerActionUIService))`, which returns `null` here, so the surrounding `is` pattern
fails and the body is skipped. The type has to resolve for that pattern to compile. The same boundary
applies to `ControlDesigner.AutoResizeHandles` (stored; the handles it governs are drawn by a design
surface, and there is none) and `ControlDesigner.EnableDesignMode`, which returns `false` — enabling
design mode needs a design surface to enable it on, and a caller that checks the result correctly
concludes the child is not designable. `CollectionEditor.DestroyInstance` is the exception: it really
disposes, because an editor that creates a component, has it rejected and never disposes it leaks
whatever that component held.

## VB Application Model

Not implemented — see [`MIGRATION.md`'s VB Application Model section](MIGRATION.md#vb-application-model-myapplication-myforms)
for what that means in practice and how the migrator flags it.
