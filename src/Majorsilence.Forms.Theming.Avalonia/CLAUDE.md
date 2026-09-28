# Majorsilence.Forms.Theming.Avalonia — notes for AI coding tools

Applies a parsed CSS theme (`ThemeStyleSheet`, from the core) to **native Avalonia controls** under the
Fluent theme. `net8.0;net10.0`. User-facing description and the support matrix:
`docs/theming-avalonia.md`. Tests: `tests/Majorsilence.Forms.Theming.Avalonia.Tests` (headless, any OS).

## Shape

| File | Role |
|---|---|
| `AvaloniaCssTheme.cs` | Public API: `Apply (css / sheet)`, `Watch (path)`, `Diagnostics`, `Support`, `SetThemeVariant`. Installs the build into `Application.Current`; mirrors sheets applied from the Majorsilence.Forms side via `Theme.StyleSheetApplied`. |
| `AvaloniaThemeSupport.cs` | The **data**: selector → Avalonia type mapping and the (selector, part, hover, property) → native / approximate / unsupported matrix, each row with its targets. Diagnostics, the applier and the doc table are all driven by it. |
| `AvaloniaThemeTargets.cs` | Target kinds (`ResourceTarget` = a Fluent resource key + CLR kind; `StyleTarget` = a style selector + property) and the selectors/properties the matrix uses. |
| `AvaloniaThemeApplier.cs` | Builds one `ResourceDictionary` + a list of `Style`s from the chain; publishes tokens and author variables under PascalCase keys; derives the accent ramp. Touches no application state. |

## Invariants to keep

- **Never a silent no-op.** Every longhand a rule can carry has a row for every mapped selector (tests
  enforce completeness against `ThemeCssReference`). Supported rows must write something; unsupported
  rows must write nothing.
- **Every resource key must exist in Fluent / the DataGrid Fluent theme with the kind we write**
  (`EveryResourceKey_ExistsInTheFluentThemesWithTheSameType`). Find candidate keys in the Fluent
  sources; do not guess.
- **Colours through resources, geometry through styles.** Fluent sets hover / pressed / selected colours
  on template parts; only its resources reach them. A style setter for a colour is right only where the
  template binds the control property (Label, Window, NumericUpDown, DataGrid itself).
- **Swap, don't mutate, on re-apply.** `Install` replaces the merged `ResourceDictionary` and the
  `Styles` object; a `Styles` cleared and refilled while attached is not applied to controls created
  later (`ReApply_StylesControlsCreatedAfterwards`).
- **Read tokens from `Theme.*`**, author variables from `ThemeStyleSheet.Variables`.
- Adding a row = change `AvaloniaThemeSupport` and regenerate the doc
  (`MAJORSILENCE_WRITE_THEMING_AVALONIA_DOC=1 dotnet test tests/Majorsilence.Forms.Theming.Avalonia.Tests`).

## Gotchas

- The namespace ends in `.Avalonia` and sits inside `Majorsilence.Forms`, whose `Button`, `Label`,
  `Timer`, `Application`... would win over Avalonia's. Put `using global::Avalonia.*;` **inside** the
  namespace block, and write `System.Threading.Timer` in full.
- Tests share one headless session and the process-wide `Theme`; keep them sequential and never block
  on `Headless.Run (...)` from teardown (use `IAsyncDisposable`) — a blocking wait deadlocks xunit.
