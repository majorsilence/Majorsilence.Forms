# Classes and ids in theme CSS — design

Status: **accepted; implementation pending.** Nothing here is implemented yet. It is phase 5 of the plan to widen the
theme CSS toward what is common on the web (transitions, modern values, paint effects, `@media` and
`@keyframes` are done; see [theming.md](theming.md)).

## Why

Every web component framework styles **variants** with classes: `.btn-primary`, `.btn-outline`,
`.danger`. A theme here can only say "every `Button`", so a primary button, a destructive button and a
quiet toolbar button cannot be told apart in CSS; the application has to set colours in code, which is
what a theme is meant to replace. Classes, and ids for one-off controls, close that gap.

## Goals and non-goals

Goals:

- `.class` and `#id` selectors, alone or compounded with a type and a pseudo-class or part:
  `.primary`, `Button.primary`, `Button.primary:hover`, `#saveButton`, `DataGridView.dense::header`.
- CSS specificity, so a more specific rule wins regardless of order, as authors expect.
- Everything already built works with them: transitions, animations, rule variables, `@media`,
  `ThemeScope`.
- Nothing changes for a sheet without classes or ids, and an application that never uses them pays
  nothing.

Non-goals (for this phase):

- **Combinators** (`Panel Button`, `.toolbar > Button`, `+`, `~`). They make a control's style depend on
  its ancestors and siblings, so every reparent and reorder has to re-resolve styles. They are worth a
  separate decision once classes have been used for a while.
- `:not()`, `:is()`, `:where()`, `:has()`, attribute selectors, `:nth-child()`.
- `!important`. Specificity is enough.

## Syntax

| Selector | Matches |
|---|---|
| `.primary` | any control whose classes include `primary` |
| `Button.primary` | a `Button` (or a type styled by `Button`'s rule) with that class |
| `.primary.large` | both classes |
| `#saveButton` | the control whose `Name` is `saveButton` |
| `Button.primary:hover` | that variant, hovered — one pseudo-class, as today |
| `DataGridView.dense::header` | the header part of a grid with class `dense` |

`#id` matches **`Control.Name`**. WinForms already gives every designer-made control a name
(`saveButton`, `button1`), so ids need no new API and ported forms can be themed at once. Names are not
guaranteed unique; a rule then styles every control with that name, which is harmless.

## The control API (decision 1)

Recommended: `Control.StyleClasses`, a small set of class names with `Add`, `Remove`, `Contains` and
`Toggle`, plus a `string CssClass` property that reads and writes it space-separated (what a designer or
a property grid edits). Changing either restyles the control.

Alternatives: only the string property (simplest, but toggling one class means string surgery), or
reusing `Tag` (rejected: `Tag` is application storage, and silently reinterpreting it would surprise).

## Semantics

- **Specificity** is CSS's: ids, then classes and pseudo-classes, then types; ties go to the later
  rule. `Button:hover` (0,1,1) beats `.primary` (0,1,0) while hovered, and `.primary:hover` (0,2,0)
  beats both — exactly as in a browser.
- **Pseudo-classes and parts** keep their current rules: a pseudo-class only where the type supports
  it, and a part only on a type with that part. `.primary:hover` is accepted when every type the rule
  can match supports `:hover`; a bare `.primary:active` is checked against the controls it reaches at
  run time and reported once as a warning if one cannot honour it.
- **Rule variables** gain the selector as part of their scope: `.primary { --bg: ...; }` is visible to
  `.primary:hover`, and a state rule overriding it still changes the base declarations that read it.
- **Transitions and animations** need nothing new: they act on whatever style a control resolves to. A
  class change is a style change, so `transition` fades between variants too.
- **Explicit per-control values still win** (`button.Style.BackgroundColor`), above every rule.

## How it fits the style chains

Today a rule sets a **type-level** style (`Button.DefaultStyle`, `Button.DefaultStyleHover`, ...) and
every instance reads through to it. That cannot express classes: the same type needs different values
per control. And the chains are linear — an instance's hover style chains to the type's hover style,
then to the type's base style — so a class layer slotted into one chain is skipped by the others
(`.primary`'s background would vanish on hover, because the hover chain never passes through it).

Recommended design: **a materialized cascade per control and state.**

- A control that has classes or an id that some rule matches gets one extra style per state it can be
  in (base, hover, active, disabled, focus, focus-visible), inserted directly above its instance styles.
- Each of these is filled by running, in specificity order, **every** rule that matches the control in
  that state — type rules included — so the cascade is resolved once, correctly, and renderers read the
  result through the chain as they do today. Type defaults that are not CSS (the code in each type's
  `DefaultStyle`) stay below, untouched.
- They are rebuilt **lazily**: when a sheet is applied or a control's classes or name change, a
  generation number moves on (the same mechanism transitions use, `ControlStyle.Generation`), and the
  next read of `CurrentStyle` rebuilds them. No registry of live controls is needed, and a control with
  no classes or ids skips all of this with one check.
- A control without classes or ids — every control in every application today — keeps the current
  type-level path exactly.

Cost: per classed control, per state, the matching rules' actions run once per sheet change or class
change. That is the same work a sheet apply does for a type today, multiplied by the number of classed
controls; it never runs per paint.

## Host appliers (decision 2)

- **System.Windows.Forms**: `#id` maps naturally (WinForms controls have `Name`); classes have no
  counterpart. Recommended: apply `#id` rules to the named control, and report class rules as info
  diagnostics ("styles Majorsilence.Forms controls only"), the same pattern as the other unsupported
  rows.
- **Avalonia**: Avalonia has style classes (`Classes`) and names, so `.primary` and `#saveButton` map to
  Avalonia selectors directly. Recommended as native rows, with the same support-matrix treatment.

## Model

`ThemeCssRule` gains `Classes` (the class names), `Id`, and `Specificity`; `ThemeCssReference` gains a
"Selectors with classes and ids" section, and `docs/theming.md` a chapter, all generated as today.

## Plan, if accepted

1. Parse compound selectors (`Type`, `.class`, `#id`, pseudo-class, part) and specificity; errors for
   combinators and the other non-goals, each saying why.
2. `Control.StyleClasses` / `CssClass`, the per-control state styles and lazy rebuild; rule variable
   scopes extended to selectors.
3. Tests: specificity ordering (proved by paint, both orders), class toggling with and without a
   transition, ids from designer names, `ThemeScope`, `@media`, an allocation gate for a paint of a
   classed control, and the "no classes" path unchanged.
4. Host appliers: Avalonia native mapping; WinForms `#id`, classes as diagnostics.
5. Docs and the support matrices regenerated.

## Decisions

Decided:

1. **The control API: `Control.StyleClasses` *and* `CssClass`.** A small set of class names with `Add`, `Remove`,
   `Contains` and `Toggle`, plus a `string CssClass` property that reads and writes it space-separated (what a designer
   or a property grid edits). Changing either restyles the control. Both, not only the string.
2. **Host appliers: `#id` maps on System.Windows.Forms and classes on Avalonia**, as recommended above. WinForms applies
   `#id` rules to the control whose `Name` matches and reports class rules as info diagnostics ("styles
   Majorsilence.Forms controls only"); Avalonia maps `.class` to `Classes` (and `#id` to `Name`) natively, with
   support-matrix rows as everywhere else.
3. **Combinators follow in a later phase**, not this one (`Panel Button`, `>`, `+`, `~` stay errors that say so).

Implementation order is the "Plan, if accepted" list above; the parity plan's phase 6
([theming-parity-plan.md](theming-parity-plan.md)) is the host half of it.
