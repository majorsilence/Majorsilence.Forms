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
| `WinFormsThemeSupport.cs` | The **data**: selector → WinForms type mapping and the (selector, part, hover, property) → native / approximate / unsupported matrix with notes. Diagnostics and the doc table are generated from it. |
| `ThemeRuleSet.cs` | Chain of sheets merged to `(selector, part, hover) → property → live value`; `TokenSnapshot` reads `Theme.*` once per apply as `System.Drawing.Color`. |
| `ThemeToolStripRenderer.cs` | `ProfessionalColorTable` + renderer built from tokens and the `Menu`/`ToolBar`/`StatusBar`/`MenuDropDown` rules; installed on `ToolStripManager.Renderer`. |
| `Dwm.cs` | Windows 11 title bar / frame colours, best effort. |
| `NativeMethods.cs` | `SetWindowTheme (hwnd, "", "")` so a ProgressBar honours `ForeColor`/`BackColor` (WinForms forwards those to `PBM_SETBARCOLOR`/`PBM_SETBKCOLOR` itself on every handle creation — sending the messages from a `HandleCreated` hook is overwritten by `ProgressBar.OnHandleCreated`). Token-only: the grammar has no `ProgressBar` selector. |

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
- Only reference the core project. No `Majorsilence.Forms.WinForms` (the backend) and no
  `SkiaSharp.Views.WindowsForms` (collides with the core's `SKControl` shims and drags in net461 OpenTK).

## Gotchas

- `UseWindowsForms` makes `System.Windows.Forms` an implicit global using: write `WF.` for WinForms
  types and fully qualify `System.Threading.Timer`.
- `Application.SetDefaultFont` / `SetColorMode` throw `InvalidOperationException` once a window handle
  exists — they are wrapped and surfaced as info diagnostics, never as failures. `SetColorMode` is
  `NET9_0_OR_GREATER` and needs `#pragma warning disable WFO5001`.
- `Color.Equals` compares name/known-colour metadata as well as ARGB; when comparing to a control's
  value use `ToArgb ()` (tests) or compare against the exact struct you set (applier).
