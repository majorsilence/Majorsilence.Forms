# Majorsilence.Forms.Theming.WinForms — notes for AI coding tools

Applies a parsed CSS theme (`ThemeStyleSheet`, from the core) to **real `System.Windows.Forms`**
controls. Windows-only (`net8.0-windows;net10.0-windows`), empty placeholder elsewhere. User-facing
description and the support matrix: `docs/theming-winforms.md`. Tests:
`tests/Majorsilence.Forms.Theming.WinForms.Tests` (run on Windows).

## Shape

| File | Role |
|---|---|
| `WinFormsCssTheme.cs` | Public API: `Apply (css / sheet)`, `Watch (path)`, `Track (form)`, `Diagnostics`, `Support`. Also mirrors sheets applied from the Majorsilence.Forms side via `Theme.StyleSheetApplied`. |
| `WinFormsThemeApplier.cs` | Walks control trees, sets properties per control type, owns the per-control opt-out state. |
| `WinFormsThemeSupport.cs` | The **data**: selector → WinForms type mapping and the (selector, part, state, property) → native / approximate / unsupported matrix with notes. Diagnostics and the doc table are generated from it. |
| `WinFormsThemeApplier.States.cs` | `:hover` / `:active` / `:disabled` / `:focus` / `:focus-visible`: per-control event hooks (installed once, read `Current`) that recompute a control's colours for its state (`RefreshLook`) and swap them. |
| `WinFormsThemeApplier.Effects.cs`, `EffectRenderer.cs` | `box-shadow`, `outline`, `background-image`: shadows/outlines painted by the *parent's* `Paint` event, inset shadows from the control's own `Paint`, gradients into `BackgroundImage`. `EffectRenderer` draws with SkiaSharp and converts to `Bitmap`. |
| `WinFormsThemeApplier.Motion.cs` | Transitions and `@keyframes` on the colours a state swaps: one UI-thread timer (runs only while something moves), `ThemeCssTiming.Evaluate` for the curves. |
| `BorderPainter.cs` | A `NativeWindow` that repaints a control's 1px frame in the theme's `border-color` (`WM_NCPAINT` / `WM_PAINT` / `WM_PRINT`); also `PaintsItself` / `OverridesPaint`, which decide who is left alone. |
| `ThemeRuleSet.cs` | Chain of sheets merged to `(selector, part, state) → property → live value`; `TokenSnapshot` reads `Theme.*` once per apply as `System.Drawing.Color`. |
| `ThemeToolStripRenderer.cs` | `ProfessionalColorTable` + renderer built from tokens and the `Menu`/`ToolBar`/`StatusBar`/`MenuDropDown` rules; installed on `ToolStripManager.Renderer`. |
| `Dwm.cs` | Windows 11 title bar / frame colours, best effort. |
| `NativeMethods.cs` | `SetWindowTheme (hwnd, "", "")` so a ProgressBar honours `ForeColor`/`BackColor` (WinForms forwards those to `PBM_SETBARCOLOR`/`PBM_SETBKCOLOR` itself on every handle creation — sending the messages from a `HandleCreated` hook is overwritten by `ProgressBar.OnHandleCreated`). The fill is `--accent-color-2`; `ProgressBar { background-color }` recolours the track. |

Owner draw is used only for *selection* (ListBox `OwnerDrawFixed`, TreeView `OwnerDrawText`, ListView
`OwnerDraw` in Details view) and tab headers, and never on a control the app already owner-draws or a
`CheckedListBox` (which throws on any other `DrawMode`). Keep that rule: the applier must not make an
app's own painting disappear.

## Invariants to keep

- **Never a silent no-op.** Every longhand a rule can carry must have a row in `WinFormsThemeSupport`
  for every mapped selector (the tests enforce completeness against `ThemeCssReference`). Adding a
  seam = change the applier **and** flip the row to `Native`/`Approximate` **and** regenerate the doc
  (`MAJORSILENCE_WRITE_THEMING_WINFORMS_DOC=1 dotnet test tests/Majorsilence.Forms.Theming.WinForms.Tests`).
- **Explicit per-control values win.** Only `BackColor` / `ForeColor` / `Font` go through `SetTracked`,
  which pins a property the app set explicitly (`PropertyDescriptor.ShouldSerializeValue`) or changed
  after we styled it. When an inherited value already matches, record the `Ambient` marker — do not
  record the value — or the child gets wrongly pinned when the parent's colour moves on the next
  apply (that bug shipped once; see `InheritedValueThatMatchedOnce_IsNotPinnedWhenTheParentMoves`).
- **Read tokens from `Theme.*`, not from the sheet's `:root`.** `Theme.ApplyStyleSheet` has already
  resolved the `extends` chain and every `var()`; a sheet that sets three tokens still themes
  everything.
- **Hooks read `WinFormsThemeApplier.Current`.** `ControlAdded`, `HandleCreated`, `DrawItem`, `Resize`
  handlers are installed once per control (flags in `ControlState`) and look up the current context, so
  a re-apply changes what they do without re-hooking.
- **Flatten alpha.** WinForms does not composite translucent `BackColor`s; every colour is
  `TokenSnapshot.Flatten`ed over the theme background before it is set.
- **Three switches, all on by default, each a kill switch for code that paints**: `WinFormsCssTheme.PaintBorders`,
  `PaintEffects` and `Animate`. A support row that depends on one carries a `WinFormsThemeGate`; with the switch off
  `BuildDiagnostics` reports it as Unsupported ("... PaintBorders is off"). A new painting feature gets a gate, a row, a
  test that it can be turned off, and a test that turning it off puts everything back.
- **Never paint a control the app paints itself** (`BorderPainter.PaintsItself` / `OverridesPaint`), never on a control
  or parent the sheet excludes, and always say so with an info diagnostic (`ApplyContext.Report`, once per message).
- **Paint hooks must release**: a `NativeWindow` is released on `HandleDestroyed`, bitmaps on `Disposed`; the leak tests
  (`BorderPainters_AreReleasedWithTheirControls`, `EffectsAreReleasedWithTheirControls`) count USER / GDI objects.
- **A state's look is recomputed, not patched**: `RefreshLook` derives BackColor/ForeColor/border from rules + state every
  time, through `SetTracked` (so an explicit app colour still wins) and `Animated` (so a transition or keyframe can
  stand between the old and the new value).
- Only reference the core project. No `Majorsilence.Forms.WinForms` (the backend) and no
  `SkiaSharp.Views.WindowsForms` (collides with the core's `SKControl` shims and drags in net461 OpenTK).

## Gotchas

- Tests draw with `Control.DrawToBitmap`. A form that was never shown prints **none of its children** and reads every child
  as `Visible == false`; draw the child itself, or the parent when you are checking what the parent paints. Force the handle
  first (`_ = control.Handle`): a `ListBox` snaps to whole items (`IntegralHeight`) when it is created, so a bitmap sized
  before that is too tall.
- A frame is the outermost ring of the *window*, which for a control with a non-client border (`ProgressBar`,
  `NumericUpDown`, `BorderStyle.FixedSingle`) is outside the client area: paint it in `WM_NCPAINT` mode, and offset a
  `WM_PRINT` client paint by the non-client size.

- `UseWindowsForms` makes `System.Windows.Forms` an implicit global using: write `WF.` for WinForms
  types and fully qualify `System.Threading.Timer`.
- `Application.SetDefaultFont` / `SetColorMode` throw `InvalidOperationException` once a window handle
  exists — they are wrapped and surfaced as info diagnostics, never as failures. `SetColorMode` is
  `NET9_0_OR_GREATER` and needs `#pragma warning disable WFO5001`.
- `Color.Equals` compares name/known-colour metadata as well as ARGB; when comparing to a control's
  value use `ToArgb ()` (tests) or compare against the exact struct you set (applier).
