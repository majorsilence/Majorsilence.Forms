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
| `ThemeCssPart.cs`, `ThemeCssValues.cs`, `ThemeCssTokenizer.cs` | Parts (`Type::part`), value parsing (colours, lengths, fonts), tokens. |

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
