# Theming (CSS themes) — notes for AI coding tools

The CSS theme subset the core accepts, its parser, and the host-neutral model other appliers read.
User docs: `docs/theming.md` (reference tables generated from `ThemeCssReference`). Tests:
`tests/Majorsilence.Forms.Tests/ThemeCss*.cs`.

| File | Role |
|---|---|
| `ThemeCssReference.cs` | **The single description of the grammar**: `Tokens` (one per `Theme` property), `Selectors` (control types, with `:hover` support and `::part`s), `Properties`. Add here first; parser, docs, Studio and host appliers follow. |
| `ThemeStyleSheet.cs` | The parser. Never throws; collects `Diagnostics` and keeps the valid rest. Compiles rules to `Action<ControlStyle>` for our renderers **and** builds the public model (`Tokens`, `Rules`) from the same parse. |
| `ThemeCssModel.cs` | Host-neutral model: `ThemeCssValue` (ARGB / px / families / weight / slant — no Skia types), `ThemeCssDeclaration` (live value when written as `var()`), `ThemeCssRule`, `ThemeCssTokenValue`. |
| `Theme.Css.cs` | `Theme.RegisterThemeCss`, `LoadFromCss`, `ApplyStyleSheet`, `ExportCss`, `StyleSheetApplied` (raised after `ThemeChanged`, what host bridges subscribe to), `CurrentStyleSheets`. |
| `ThemeCssDiagnostic.cs` | `Warning` / `Error` / `Info` + `ThemeCssException`. `Info` is for host appliers ("no WinForms counterpart"), never the parser. |
| `ThemeCssPart.cs`, `ThemeCssValues.cs`, `ThemeCssTokenizer.cs` | Parts (`Type::part`), value parsing (colours, lengths, fonts, times, timing functions, shadows), tokens. |
| `ThemeCssCalc.cs` | `calc()`/`min()`/`max()`/`clamp()`, units (`rem` reads `Theme.RootFontSize`), relative-colour channel names. Every unit conversion lives here. |
| `ThemeCssColorFunctions.cs`, `ThemeCssColorSpaces.cs` | CSS Color 4/5 functions, `color-mix()`, relative colours, `light-dark()`; colour-space conversions and CSS's gamut mapping. |
| `ThemeCssState.cs`, `ThemeCssMotion.cs`, `ThemeCssGradient.cs` | The host-neutral view a host applier needs beyond declarations: `ThemeCssState` (a rule's one pseudo-class; `ThemeCssSelector.Supports (state)`), `ThemeCssRule.Transitions` / `Animations` and the static `Resolve (declarations)` that lets a state inherit what it leaves unsaid, `ThemeCssTiming.Evaluate` (the core's easing curves, for a host with its own timer), `ThemeStyleSheet.Keyframes`, and `ThemeCssValue.Gradients` (`ThemeCssGradient`, with `Render` for a host that paints with Skia). Tests: `ThemeCssHostModelTests`. |
| `ThemeCssGradients.cs`, `ThemeCssMedia.cs` | `background-image` gradients (painted by `../GradientStyle.cs`); `@media` queries over `MediaEnvironment`. |

## Rules

- **Never a silent no-op.** Anything outside the subset is an *error* with a message that says what to
  write instead (name the offending text, suggest the nearest supported spelling). Do not widen the
  grammar to "accept and ignore".
- `border` expands to `border-width` / `border-color` during parsing; per-side properties stay per-side.
  Host appliers only ever see longhands.
- `:root` tokens layer onto the current `Theme`; control rules **replace** the previous sheet's rules
  (`ApplyControlRules` resets styles no longer targeted). Keep that asymmetry.
- Changing a token, selector, part or property means regenerating `docs/theming.md`
  (`MAJORSILENCE_WRITE_THEMING_DOC=1 dotnet test tests/Majorsilence.Forms.Tests --filter ThemeCssDoc`)
  and, if a selector or part was added, adding its rows to
  `src/Majorsilence.Forms.Theming.WinForms/WinFormsThemeSupport.cs` (its completeness tests fail otherwise).
- Every ```css block in `docs/theming.md` must parse without errors (a test checks) unless fenced as
  ```css invalid.
- A sheet with `@media` is compiled once per `MediaEnvironment` (four of them) from one syntax pass;
  `Tokens`/`Rules`/`ControlRules` read the current one. Anything compiled per sheet (keyframes, rule
  variables) must be reset per variant in `Parser.Parse`.
- Transitions and animations run in `../Control.StyleTransitions.cs`, keyed off `CurrentStyle`. A new
  animatable property needs a channel there and an entry in `ControlTransition.AnimatableProperties`.
  Frames must not allocate (`TransitionBenchmarks`, `KeyframeAnimationBenchmarks` gate it) -- beware
  lambdas capturing locals in that hot path.
