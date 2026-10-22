# Theming parity — real WinForms and native Avalonia vs. the Majorsilence.Forms engine

Status: **implemented**, phases 0 to 5 and the opt-in selectors of phase 6. The rest of phase 6 -- classes and ids -- waits
on the core's decision in [theming-selectors-plan.md](theming-selectors-plan.md), which the core has not built. The sections below
are the plan as written; [What shipped](#what-shipped) says where reality differs.

## What shipped

| Phase | WinForms | Avalonia |
|---|---|---|
| 0 Foundations | Support rows per **state**; the parity ledger (`tests/*/ParityLedger.txt`: a row that gets worse fails a test); `ThemeCssState`, `ThemeStyleSheet.Keyframes`, `ThemeCssRule.Transitions` / `Animations`, `ThemeCssValue.Gradients`, `ThemeCssTiming.Evaluate` in the core. | same |
| 1 Tokens and selectors | `ProgressBar`, `StatusStrip` (and its render mode), `DateTimePicker` mapped; `--border-low-color` frames. | **`DeriveFromTokens`, on by default**: a tokens-only sheet restyles buttons, inputs, lists, grids, menus, tabs, progress bars and popups; `ProgressBar`, `DateTimePicker`, `PopupWindow` mapped. |
| 3.2 Borders | **`PaintBorders`, on by default**: `border-color` on text boxes, lists, trees, panels, picture boxes, combo boxes, up-downs, group boxes and progress bars (`BorderPainter`). | native already |
| 2 States | `:hover` colour/border on buttons, `:active`, `:disabled`, `:focus`, `:focus-visible` on buttons, links, text boxes and track bars, from the controls' own events. | `*Pressed`, `*Disabled`, `*Focused` resources; `:focus` / `:focus-visible` as setters on the template part. |
| 3 Corners | Per-corner `border-radius` on buttons; dashed repainted frames. | Per-corner `CornerRadius`. Dashed borders, scroll-bar thumb and tab-strip borders and the data-grid header separator stay unsupported: Fluent has no seam (probed). |
| 4 Effects | **`PaintEffects`, on by default**: outer / inset `box-shadow`, `outline`, gradients (Skia -> `BackgroundImage`). `opacity` stays unsupported on controls. | `opacity`, `box-shadow` (the template's root part, per state), gradients as brushes, `outline` as the `FocusAdorner`. |
| 5 Motion | **`Animate`**: colour transitions and `@keyframes` from one UI-thread timer, honouring Windows' UI-effects setting. | `Transitions` / `Animation` on the template parts and state selectors. |
| 6 Opt-in | -- (see below) | `CssTheme.Selector` for `Panel`, `GroupBox`, `StatusBar`, `StatusStrip`, `ToolBar`, `PictureBox`. |

Differences from the plan:

- The grammar only allows `transition` on `Button`, `LinkLabel`, `TextBox` and `TrackBar` (the controls with states), so motion
  is wired for those; `@keyframes` run on any control in the core and are wired for the controls with a named template part
  (Avalonia) or states (WinForms).
- Avalonia's `ScrollBar::thumb` / `TabStrip` borders and the `DataGridView::header` separator have no seam: probing the Fluent
  resources found no key, and the separator's fill is a local value in the template that no style reaches.
- `WinFormsCssTheme.Animate` is a fourth switch, added with the other three on the same terms (default on).
- Phase 6's classes and ids, and `#id` on WinForms, are not built: they need `Control.StyleClasses`, a cascade and specificity
  in the core first (theming-selectors-plan.md, "Decisions needed").

Ledger totals after this work (rows by level): WinForms 184 native / 549 approximate / 1390 unsupported; Avalonia 502 / 374 / 1098.
Run the ledger test to see exactly which rows moved.

## What "parity" means

The Majorsilence.Forms engine renders every construct in [theming.md](theming.md). The two host appliers
([WinForms](theming-winforms.md), [Avalonia](theming-avalonia.md)) consume the same parsed model, but each
can only express what its toolkit has a seam for. Parity is therefore **not** "everything Native". It is:

1. **No construct is dropped without a recorded reason.** Today that holds (the matrices are complete and a
   test enforces it). It must keep holding as the grammar grows.
2. **Every construct the toolkit *can* express, the applier does** — Native where the seam is exact,
   Approximate where it is close, with the difference written down.
3. **A sheet written for the core looks the same on all three hosts to within the recorded gaps**, including a
   sheet that sets *only tokens* (the commonest kind of theme).

The remaining Unsupported rows after this plan are the permanent ones (native glyphs, OS window frame, no such
control) and are listed under [Permanent gaps](#permanent-gaps).

## Where the gap is today

Read from `docs/theming-winforms.md`, `docs/theming-avalonia.md` and the appliers.

| Area | Core engine | Real WinForms | Native Avalonia |
|---|---|---|---|
| Tokens, no rules | Every control follows its token | Yes — tokens become defaults (`--control-low-color` on lists, etc.) | **Only** accent ramp, selection, fonts, window bg/fg. `--control-*`, `--border-*`, `--foreground-disabled-color` are published as resources but drive no Fluent control |
| Colours, fonts, hover | Yes | Yes (hover: Button only; LinkLabel approximate) | Yes (Fluent resources) |
| `:active` `:disabled` `:focus` `:focus-visible` | Yes | Skipped, one info diagnostic | Skipped, one info diagnostic |
| Per-corner radius | Yes | Single radius only (Region) | Single radius only, though `CornerRadius` has four corners |
| `border-style: dashed` | Yes | No | No |
| `border-color` on inputs/lists/panels | Yes | **No** — the largest hole: the doc's own example (`TextBox, ComboBox { border-color }`) is a warning | Yes |
| `box-shadow`, `opacity`, `outline` | Yes | No | No ("not mapped yet") |
| `background-image` gradients | Yes | No | No |
| `transition`, `@keyframes` / `animation` | Yes | No | No ("not mapped yet") |
| `@media`, rule variables, `calc()`, colour functions | Yes | Yes — resolved in the model, re-applied on change | Yes |
| Selectors with no counterpart | — | `ProgressBar`, `StatusStrip`, `DateTimePicker`, `Ribbon`, `NavigationPane`, … | `ProgressBar`, `DateTimePicker`, `Panel`, `GroupBox`, `StatusBar`, `ToolBar`, `StatusStrip`, `ListView`, `PictureBox`, `PropertyGrid`, … |
| Classes / ids (when the core ships them) | Planned | `#id` via `Control.Name` | `.class` / `#id` via `Classes` / `Name` |

Two facts shape the order of work:

- `ThemeCssRule` carries `Hover/Active/Disabled/Focus/FocusVisible`, but both support matrices key on a single
  `Hover` bool. Every state beyond hover is invisible to the "never a silent no-op" tests. That has to widen
  first.
- `@keyframes` are compiled for the core's renderers but **not exposed in the public model**
  (`ThemeStyleSheet` holds them privately), so no applier could animate even if it wanted to.

## Principles

- **Same discipline as the rest of the subset:** never a silent no-op; a row flips to Native/Approximate in
  the same change that makes the applier do it, and the doc is regenerated.
- **Spike before promising.** Several seams below are believed, not verified (called out as *spike*). A spike
  that fails turns into a documented Unsupported row, which is an acceptable outcome.
- **Don't take over the app's painting.** The WinForms rule already stands: no owner-draw on a control the app
  owner-draws. Any new paint hook follows it.
- **Tokens first.** A token-only sheet is the highest-leverage case; fix it before the long tail of effects.
- **Each phase is its own PR**, targets `main`, and ships with regenerated docs and baselines.

## Phase 0 — Foundations (core + both appliers)  · S–M

1. **Widen the support key from `Hover` to a state.** Replace `bool Hover` in `WinFormsThemeSupportEntry` /
   `AvaloniaThemeSupportEntry` and the `index` dictionaries with `ThemeCssState`
   (`Normal, Hover, Active, Disabled, Focus, FocusVisible`). Selectors only get rows for the states
   `ThemeCssReference` says they support (`Button`, `LinkLabel`, `TrackBar`; `TextBox` focus). Rows start as
   Unsupported with today's note, so behaviour is unchanged and the diagnostics become per-state instead of
   one info line. Doc generators and completeness tests follow.
2. **Expose keyframes in the model.** Add `ThemeStyleSheet.Keyframes` (name → ordered frames, each with
   offset, per-frame timing function and the same `ThemeCssDeclaration` shape as a rule) and
   `ThemeCssRule.Animation` / `Transition` as parsed values rather than twelve loose longhands. Hosts should
   not re-parse `animation-*` strings. Needed by phase 5.
3. **Expose gradients structurally.** `background-image` reaches hosts as a CSS string
   (`ThemeCssValue.Css`). Add a `ThemeCssGradient` model (kind, angle/position, repeating, stops with
   colour + offset) so hosts build brushes without a second parser. Needed by phase 4.
4. **A parity ledger.** A baseline file in `tests/` (same convention as the other `*Baseline.txt`) listing
   `(host, selector, part, state, property) → level`. A change that downgrades a row fails the test; an
   upgrade rewrites the baseline with `MAJORSILENCE_WRITE_*_BASELINE=1`. The doc's per-host Native /
   Approximate / Unsupported counts come from it, so progress is visible and regressions are recorded acts.
5. **Fix stale text.** `docs/theming-winforms.md` and `Majorsilence.Forms.Theming.WinForms/CLAUDE.md` say the
   grammar "has no ProgressBar selector"; it does (see theming.md). Reconcile when phase 1 lands.

## Phase 1 — Tokens and selector coverage  · M (Avalonia), S (WinForms)

### Avalonia: make a tokens-only sheet recolour Fluent

Today an Avalonia `Button` with no rule ignores `--control-mid-color`; in the core it is the button face.
Add a **token-derived default layer** in `AvaloniaThemeApplier`: before the sheet's own rules, synthesise the
declarations the core's `DefaultStyle`s derive from tokens for each mapped selector (face from
`--control-mid-color`, input paper from `--control-low-color`, borders from `--border-low-color`, hover
highlight from `--control-highlight-low-color`, disabled text from `--foreground-disabled-color`, …), and
feed them through the **existing** rule → resource/setter path so the matrix, diagnostics and Fluent-key
test cover them for free. The derivation table is lifted from the core, not re-invented; a test renders the
same tokens-only sheet in the core headless renderer and in an Avalonia headless window and compares the
dominant colour of each control (a relationship, not a pixel threshold).

Explicit rules still win over derived ones, as in the core. This layer is **on by default**
(`AvaloniaCssTheme.DeriveFromTokens`, see [Decisions](#decisions)); turning it off restores today's
accent/selection/font/window-only behaviour.

### WinForms: selectors that are skipped but have a seam

| Selector | Change |
|---|---|
| `ProgressBar` | Map to `ProgressBar` (track `BackColor`, fill `ForeColor` from `--accent-color-2`; visual styles off via the existing `SetWindowTheme` hook). Replaces "no counterpart". |
| `StatusStrip` | Map to `StatusStrip`: `background-color` and `border-top` through the renderer. A real StatusStrip is already themed by `StatusBar`; the separate selector should not read as unsupported. |
| `DateTimePicker` | Map to `DateTimePicker`: `CalendarForeColor`, `CalendarMonthBackground`, `CalendarTitle*` for the drop-down; text area stays native (Approximate, noted). |

### Avalonia: selectors Fluent can express

| Selector | Change |
|---|---|
| `ProgressBar` | Fluent `ProgressBarBackground/Foreground/BorderBrush` resources. |
| `DateTimePicker` | `DatePicker*` / `TimePicker*` resources (Fluent publishes them). |
| `ListView` | Map to `ListBox` and `DataGrid` rules (it is both in the core), or keep as the documented alias. |
| `PopupWindow` | `FlyoutPresenter` / `PopupRoot` background resources. |

*Spike* each resource-key against the real Fluent theme using the existing
`EveryResourceKey_ExistsInTheFluentThemesWithTheSameType` test before committing to a row.

## Phase 2 — States  · M (Avalonia), M–L (WinForms)

### Avalonia (mostly Native)

Fluent already has a resource per state; today only `PointerOver`/`Pressed` are written.

| CSS | Avalonia |
|---|---|
| `:active` | `*Pressed` resources, written independently of `:hover` (today pressed is derived from hover) |
| `:disabled` | `*Disabled` resources (`ButtonBackgroundDisabled`, `TextControlBackgroundDisabled`, …) |
| `:focus` | `*Focused` resources (`TextControlBackgroundFocused`, `TextControlBorderBrushFocused`, …) |
| `:focus-visible` | Avalonia's `:focus-visible` pseudo-class in a generated `Style` (*spike*: confirm the pseudo-class and that a setter on the template part reaches it) |

The Fluent-key test already proves each key exists with the right type.

### WinForms (Approximate, event-driven)

WinForms has no state styling, so state is applied by hooks in the existing `ControlState` flags
(installed once, reading `WinFormsThemeApplier.Current`, so a re-apply changes behaviour without re-hooking):

| CSS | Mechanism |
|---|---|
| `Button:active` | `FlatAppearance.MouseDownBackColor` — Native for background; today derived from hover. |
| `:disabled` | `EnabledChanged` swaps `BackColor`/`ForeColor` where the control honours them (Label, Panel, TextBox `BackColor`); Flat buttons and ComboBox ignore a disabled `ForeColor`, so Approximate with the limit stated. |
| `:focus` | `Enter`/`Leave` swap colours for TextBox, ComboBox, NumericUpDown, ListBox. |
| `:focus-visible` | `:focus` gated on `Control.ShowFocusCues`, which is the same signal the core uses. |

Hooks must respect the existing pin rule: an explicit `BackColor`/`ForeColor` wins.

## Phase 3 — Borders and corners  · M–L

1. **Per-corner `border-radius`, both hosts.** Avalonia: `CornerRadius` already has four corners; extend the
   `StyleBuilder` the way `BorderThickness` is assembled per side. Native. WinForms: build the `Region` from a
   `GraphicsPath` with four distinct radii instead of one. Approximate (not anti-aliased, as today).
2. **WinForms `border-color` on inputs, lists, panels (the big one).** WinForms exposes `BorderStyle` only.
   Add a `NativeWindow` subclass per themed control that handles `WM_NCPAINT` and paints the 1px non-client
   border in the rule's colour, as the well-known flat-border technique does. Covers `TextBox`,
   `NumericUpDown` (edit part), `ListBox`, `ListView`, `TreeView`, `Panel`, `PictureBox`, `RichTextBox`.
   `ComboBox` paints its border inside the client area, so it needs a `Paint` overdraw on the *parent* —
   *spike* before committing. Never installed on a control the app already custom-paints. This converts a
   large block of "not supported" warnings into Approximate and is the highest-value WinForms item. **On by
   default** behind `WinFormsCssTheme.PaintBorders` (see [Decisions](#decisions)).
3. **WinForms `GroupBox` frame** (`border-color`, `border-radius`): overdraw the frame in the control's
   `Paint` event. Approximate.
4. **`border-style: dashed`.** WinForms: draw the border in the same NC/Paint hook with a dashed pen
   (dash and gap are three border-widths, per the core). Avalonia: `Border` has no dashed stroke; options are
   a `DrawingBrush` as `BorderBrush` (*spike* — likely clips badly with radii) or replace the template
   `Border` with a `Rectangle`. If neither is clean, leave Unsupported with a sharper reason.
5. **Avalonia gaps with a seam:** `ScrollBar::thumb` `border-*` (the matrix says the Fluent thumb has no
   outline; check whether `ScrollBarThumbFill`-adjacent templates allow one), `TabStrip` border (via the
   TabControl's own `Border` template part), `DataGridView::header` `border-color` (grid-lines brush is
   shared; a `DataGridColumnHeader` style may separate it).

## Phase 4 — Paint effects  · L

The WinForms column is **on by default** behind `WinFormsCssTheme.PaintEffects`; see [Decisions](#decisions)
for the flicker and skip-with-diagnostic exit criteria.

| Property | WinForms | Avalonia |
|---|---|---|
| `opacity` | **Form only** (`Form.Opacity`), which the core rejects anyway → stays Unsupported on controls, with the existing reason. | `Opacity` style setter on the control. Native. |
| `box-shadow` (outer) | Paint the shadow in the **parent's** `Paint` handler, beneath the child (children paint after the parent, so it shows around the face); blur by a cached, pre-blurred bitmap; invalidate on child move/resize. Approximate; not for controls whose parent is the app's own custom-painted surface. | `Border.BoxShadow` on the template's root `Border`/`ContentPresenter` (*spike*: which Fluent templates expose a `Border` we can target). Native, including `inset`. |
| `box-shadow` (inset) | Overdraw in the control's own `Paint` for Panel/Label; not possible on natively-painted inputs → Unsupported there. | Native with `inset`. |
| `background-image` (gradients) | Render with the core's gradient code to a `Bitmap` sized to the control and set `BackgroundImage` (Button, Panel, Label, Form, GroupBox); clip to the radius `Region`. Regenerate on `Resize`. Approximate: no live hover blend. | Build `LinearGradientBrush` / `RadialGradientBrush` / `ConicGradientBrush` from the phase-0 model and write it through the same brush resources/setters as colours. Repeating gradients and hard bands: Approximate (*spike* the stop semantics). |
| `outline` / `outline-offset` | An overlay adorner window per focused control painting the ring in the parent. Fragile (flicker, z-order, DPI) → **last in the phase; may be Unsupported**. | Fluent draws `FocusAdorner`; set a custom `FocusAdorner` template from `outline-*`. Approximate (the ring is drawn by the adorner layer, outside the control, which matches CSS). |

## Phase 5 — Motion  · L

Both hosts consume the phase-0 `Transition` / `Animation` / `Keyframes` model and honour
`SystemInformation.PrefersReducedMotion` exactly as the core does (no motion when set).

- **Avalonia.** The matrix says Fluent's state colours live on template parts that transitions on the control
  can't reach. They can if the transition is placed **on the part** that carries the changing property:
  emit `Style`s targeting `Button /template/ ContentPresenter#PART_ContentPresenter` with `Transitions`
  (`BrushTransition` for `Background`/`BorderBrush`/`Foreground`, `DoubleTransition` for `Opacity`,
  `BoxShadowsTransition`, `CornerRadiusTransition`). Timing: the CSS easings map to `SplineEasing`
  (`ease*`, `cubic-bezier`); `steps()` needs a small custom `Easing`. `@keyframes` become
  `Avalonia.Animation.Animation`/`KeyFrame` objects in the `Style`'s `Animations`. Native for the animatable
  set the core allows (colours, radii, shadow, opacity, outline); *spike* part targeting on Button, TextBox,
  CheckBox first, because the same-part rule decides how many controls this works for.
- **WinForms.** A single UI-thread `System.Windows.Forms.Timer` "tweener" interpolating `BackColor`,
  `ForeColor`, `FlatAppearance.*Color` toward a state change, started from the phase-2 hover/focus/active
  hooks; frames must not allocate. Approximate: colour properties only (no shadow/radius/opacity), coarser
  frame rate, and a disabled `ForeColor` snaps. Keyframe animations: colour keyframes only.

Both honour the core's rule that a change cut short reverses from what is on screen.

## Phase 6 — Classes, ids and selectors that need an opt-in  · M

Starts only after the core ships `ThemeCssRule.Classes/Id/Specificity` (theming-selectors-plan.md).

- **Avalonia.** `.primary` → `Classes`; `#id` → `Name`. Style specificity differs from CSS, so the applier
  emits `Styles` **sorted by the core's specificity** (ascending) and relies on declaration order within
  Avalonia — verify with a test that proves both orders. Native.
- **WinForms.** `#id` → `Control.Name` (designer-made names work). Classes: a `CssClass` **extender
  provider** component (`IExtenderProvider`, so the designer shows a `CssClass` property on every control,
  like `ToolTip`) plus `WinFormsCssTheme.SetClass (control, "primary")`. Cascade in `ThemeRuleSet`: replace
  the `(selector, part, hover) → property` last-wins dictionary with a per-control resolve using the core's
  specificity. Native on the control's own properties; classed rules inherit all the gaps above.
- **Selectors with no control (Avalonia `Panel`, `GroupBox`, `StatusBar`, `ToolBar`, `StatusStrip`,
  `PictureBox`; WinForms none).** Avalonia refuses to restyle layout primitives, correctly. Offer an opt-in
  instead: an attached property `CssTheme.Selector="StatusBar"` that marks the app's own `Border`/`Grid`; the
  applier targets marked elements. Nothing is restyled unless the app asks.

## Permanent gaps

These stay Unsupported with a reason in the matrix; the plan does not try to fix them.

- **WinForms:** the OS window frame width/radius, native glyphs (check/radio glyph shape, combo arrow, spin
  arrows, scroll-bar grip and arrows — a dark/light scroll bar via `SetWindowTheme` is the most that is
  worth doing), `opacity` on child controls (child windows composite opaquely), `NavigationPane`, `Ribbon`,
  `MdiClient`, Telerik-only selectors.
- **Avalonia:** the window chrome, `HostedSurface`, `PrintPreviewControl`, `PropertyGrid`, `Ribbon`,
  `NavigationPane`, MDI, Telerik-only selectors, non-Fluent themes (documented out of scope).
- **Both:** `composited` effects that need a render target the toolkit doesn't give (blurred backdrops).

## Verification

- **Completeness tests stay the gate.** Every `(selector, part, state, property)` has a row per host.
- **Prove it can fail.** For each new seam, break the applier and watch the host test go red before keeping it
  (project rule). WinForms: render with `DrawToBitmap` and assert *relationships* (border pixel equals the
  rule colour; hover pixel differs from rest), not absolute thresholds. Avalonia: headless render capture,
  the same way `EmbeddingAvalonia --render-headless` does.
- **Cross-host parity test.** One fixture sheet (tokens only; then a rules sheet exercising each phase) is
  rendered by the core headless renderer, a WinForms form and an Avalonia window; per control the test
  asserts the same colour *relationships* hold and lists any difference that isn't a recorded Approximate row.
- **Allocation gate** for the phase-5 tweener frames (`benchmarks/`), as the core's transitions are gated.
- **Run on all legs.** Both test projects are Windows/any-OS as today; the four CI shapes still apply.

## Decisions

Decided (all three **on by default**). Each keeps a kill switch, because on-by-default code that paints is
the code an app will occasionally need to turn off for one control or one process:

1. **WinForms `WM_NCPAINT` borders (phase 3.2): on by default.** Switch: `WinFormsCssTheme.PaintBorders`
   (default `true`). Per-control opt-out: the existing `WinFormsCssTheme.Exclude`. Because this is on for
   every app that applies a sheet with a `border-color`, the safety rules matter more, not less: never
   install on a control the app custom-paints, never on a `CheckedListBox`-style control that throws on
   unexpected state, remove the `NativeWindow` on handle destroy and on `Exclude`, and re-paint on DPI and
   dark-mode changes. Ship a test that creates and destroys a few hundred themed controls to prove no
   handle or `NativeWindow` leak.
2. **WinForms effects (phase 4): on by default.** Switch: `WinFormsCssTheme.PaintEffects` (default `true`),
   covering parent-painted shadows and generated gradient bitmaps. This is the flicker-prone code, so the
   phase-4 exit criteria include: double-buffered parent painting where the parent allows it, no repaint
   storm on `Resize` (coalesce bitmap regeneration), and the effects skipped — with an info diagnostic, not
   silently — when the parent is an app-custom-painted surface or the control is excluded.
3. **Avalonia token-derived defaults (phase 1): on by default.** Switch: `AvaloniaCssTheme.DeriveFromTokens`
   (default `true`). It changes how existing Avalonia apps look when a sheet sets control tokens, so it is
   called out in the release notes and the migration notes of `docs/theming-avalonia.md`; explicit rules
   still win over derived values.

Still open:

4. **Order.** Recommended: 0 → 1 → 3.2 → 2 → 3 (rest) → 4 → 5 → 6. Phases 1 and 3.2 give the most visible
   parity per line of code; 5 is the most expensive and the least needed for correct *static* appearance.

## Rough size

| Phase | Avalonia | WinForms | Notes |
|---|---|---|---|
| 0 Foundations | S | S | plus core model work (M) |
| 1 Tokens / selectors | M | S | derivation table lifted from the core |
| 2 States | M | M–L | Avalonia mostly resources |
| 3 Borders and corners | S | L | WM_NCPAINT is the cost |
| 4 Effects | M | L | outline on WinForms may not ship |
| 5 Motion | L | M | |
| 6 Classes / opt-in selectors | M | M | blocked on the core |
