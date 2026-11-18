# Majorsilence.Forms — guide for AI coding tools

Cross-platform WinForms compatibility layer: the `System.Windows.Forms` programming model reimplemented
over SkiaSharp, hosted by Avalonia / Uno / GTK 4 / WPF / real WinForms backends. Read
[CONTRIBUTING.md](CONTRIBUTING.md) first — it is the long version of everything here. This file is the
short orientation: where things are, how to verify a change, and the traps that have cost real time.

## Layout

| Path | What it is |
|---|---|
| `src/Majorsilence.Forms/` | The core: controls, `Form`, layout, `Theme`, the CSS theme parser (`Theming/`). No host dependency. |
| `src/Majorsilence.Forms.{Avalonia,Uno,Gtk4,Wpf,WinForms}/` | Platform backends. `Headless/` renders to bitmaps for tests and CI. |
| `src/Majorsilence.Forms.Theming.WinForms/` | Applies CSS themes to **real** `System.Windows.Forms` controls (Windows-only). |
| `src/Majorsilence.Forms.Theming.Avalonia/` | Applies CSS themes to **native** Avalonia controls (Fluent resources + generated styles). |
| `src/Majorsilence.Forms.Drawing.Common/` | The `System.Drawing` reimplementation. |
| `tests/Majorsilence.Forms.Tests/` | The main suite (xunit v3, headless backend). Windows-only suites sit beside it. |
| `benchmarks/` | BenchmarkDotNet suite, and the allocation gate CI runs (`-- --alloc-gate`); see `docs/performance-plan.md`. |
| `samples/` | One app per scenario; `docs/samples.md` describes each. `ThemeStudio*` are the theme editors. |
| `tools/` | `ApiDiff` (public-surface gate), `Migrator`, `Mcp`, templates. |
| `docs/` | User docs. Several sections are **generated from code** between marker comments — see below. |

The solution file is `Majorsilence.Forms.slnx`. New projects go there (in the matching folder) or CI
never builds them.

## Build and test

```bash
dotnet build -c Release                      # the cross-platform compile gate (Release = warnings are errors)
dotnet test -c Release --filter "FullyQualifiedName!~Migrator"   # everything except the ten-minute migrator suite
```

- On a Windows machine without the Android workload, add `-p:EnableMobileHeads=false` to the solution
  build or `samples/Gallery.Android` fails with NETSDK1147 before anything compiles.
- CI runs the suite in four shapes (Debug, Release, `MF_FORCE_CUSTOM_CHROME=1`, `MF_HEADLESS_SCALE=2`
  serial). See CONTRIBUTING.md "The four gates". A test project's assembly disables xunit
  parallelization because `Theme`, `Application.OpenForms` and similar are process-wide.
- Windows-only projects (anything with `UseWindowsForms`) follow one pattern: on `'$(OS)' == 'Windows_NT'`
  they target `net*-windows`; otherwise they compile to an empty placeholder (`EnableDefaultCompileItems=false`)
  so the Linux/macOS legs stay green. Copy `tests/Majorsilence.Forms.WindowsUIAutomation.Tests/*.csproj`
  when adding one.
- Packages are centrally versioned in `Directory.Packages.props`; no `Version=` on a `PackageReference`.

## Adding a new consumer-facing project

A new library project meant for consumers (not a sample/test/internal tool) must:

1. Be packable: set `<PackageId>`, `<Description>`, `<PackageTags>`, `<PackageOutputPath>../../nupkg</PackageOutputPath>`,
   and a `README.md` via `<PackageReadmeFile>` — copy `src/Majorsilence.Forms.Theming.WinForms/`'s
   csproj as a template, including its Windows-only-placeholder pattern if applicable.
2. Be added to **one** list in `build/build.cs`: `crossPlatformProjects`, `windowsProjects`
   (`net*-windows` / `UseWindowsForms`/`UseWPF`) or `macProjects` (carries a `net10.0-ios` row, so only a
   macOS runner can pack it; `Majorsilence.Forms.Essentials` and `Majorsilence.Forms.Avalonia` today). The
   `Pack` target packs whichever list matches the OS it runs on, and `dotnet.yml`, `release.yml` and
   `publish-nuget.yml` all call that target, so there is nothing to keep in sync. (These used to be three
   hand-maintained copies in the workflows; missing one meant a package built in CI but never reached a
   GitHub Release or nuget.org.)

There is no wildcard/auto-discovery for this today, and it's a deliberate choice, not an oversight: the
split into three lists exists because Windows-only projects need a `windows-latest` runner and
compile to an empty placeholder on the Linux/macOS legs, so a naive `**/*.csproj` glob would either pack
those (empty package) on Linux or need an `IsPackable` probe run per-OS before every pack step. That's
possible (`dotnet msbuild <path> -getProperty:IsPackable`, filtered per runner OS) but nobody has built
and proven it, so for now add the project to its list by hand.

A new consumer-facing **sample** worth shipping prebuilt (a runnable desktop/mobile/web app, not source)
follows a different path: add a `Sample<Name>` target to `build/build.cs` that publishes it and zips it
into `./sample-out` (see `SampleWasm`, `SampleAndroid`, `SampleThemeStudio` for the pattern), add a
`sample-<name>` job to `release.yml` that calls it and uploads `sample-out/<name>*`, then add that job's
name to the `release` job's `needs:` list — it already globs `*.zip`/`*.apk`/`*.aab`/`*.nupkg`/`*.snupkg`
out of the downloaded `artifacts/` directory, so nothing else changes there.

## The build script (`build/build.cs`)

CI and release steps are [Cake.Sdk](https://cakebuild.net/docs/running-builds/runners/cake-sdk) targets in one
file-based app, so they run the same on a laptop: `dotnet build/build.cs -- --target=Test --shape=scale2`.
Run `--target=Info` to see what a machine would do. The workflows keep only what Actions must own
(triggers, runner matrix, `setup-dotnet`, artifact upload/download, the OIDC login and push to nuget.org,
the draft Release, the emulator runner); the reasoning for each workload pin and test shape is in comments
on the target, not in the YAML.

- The Cake.Sdk version is pinned in `global.json` (`msbuild-sdks`) so Dependabot tracks it. That file has no
  `sdk` section on purpose: `Directory.Build.props` keys the net11.0 row off whichever SDK resolves, and the
  CI matrix relies on that, so do **not** add an `sdk` version there.
- `build/Directory.Build.props`, `.targets` and `Directory.Packages.props` are deliberately near-empty.
  A file-based app inherits the nearest MSBuild files above it, so without them the script would get the
  repo's `TreatWarningsAsErrors`, central package management and net11 runtime-async targets.
- It needs the .NET 10 SDK or later (the same `setup-dotnet` step that provides the SDK for the real build).
- `publish-nuget.yml` builds from the release's **tag**, so it can only publish a tag that contains
  `build/build.cs`. It packs with that tag as `PACK_VERSION`; the release steps are in
  [docs/releasing.md](docs/releasing.md).

## Conventions the analyzers do not enforce

- A space before every parameter list: `Method (arg)`, `new Size (1, 2)`, `Foo ()`. Block-scoped
  namespaces in `src/`; file-scoped is tolerated in newer samples. Private static fields are
  `snake_case`.
- In WinForms-hosting code alias the toolkits: `using WF = System.Windows.Forms; using MF = Majorsilence.Forms;`
  — both define `Button`, `Form`, `Timer`, …, and `UseWindowsForms` adds `System.Windows.Forms` as an
  **implicit global using**, so an unqualified name silently binds to the wrong one.
- Comments say *why*, not what. Cite the upstream `dotnet/winforms` file when a value or event order
  was taken from it.
- Stubs no-op or return a default; they never throw `NotImplementedException`. Accepting a no-op is a
  recorded act (the baseline files in `tests/Majorsilence.Forms.Tests/`), and `COMPATIBILITY_MATRIX.md`
  must not overstate.
- Every public member has XML docs (Release builds generate the doc file).

## Generated documentation — regenerate, do not hand-edit

Tables between `<!-- BEGIN GENERATED: … -->` / `<!-- END GENERATED: … -->` markers are produced by a
test that fails when code and document drift. Set the named variable and run the test to rewrite:

| Document | Source | Regenerate with |
|---|---|---|
| `docs/theming.md` | `ThemeCssReference.ToMarkdown ()` | `MAJORSILENCE_WRITE_THEMING_DOC=1` (Majorsilence.Forms.Tests) |
| `docs/theming-winforms.md` | `WinFormsThemeSupport.ToMarkdown ()` | `MAJORSILENCE_WRITE_THEMING_WINFORMS_DOC=1` (Theming.WinForms.Tests, Windows) |
| `docs/theming-avalonia.md` | `AvaloniaThemeSupport.ToMarkdown ()` | `MAJORSILENCE_WRITE_THEMING_AVALONIA_DOC=1` (Theming.Avalonia.Tests) |
| `benchmarks/Majorsilence.Forms.Benchmarks/AllocationBaseline.txt` | allocated bytes of `AllocGate` benchmarks | `dotnet run -c Release --project benchmarks/Majorsilence.Forms.Benchmarks -- --alloc-gate --write` (Linux) |
| `tests/Majorsilence.Forms.Tests/*Baseline.txt` | reflection scans | `MAJORSILENCE_WRITE_*_BASELINE=1` (see CONTRIBUTING.md) |

## Theming, in one paragraph

A theme is a strict CSS subset (`docs/theming.md`): `:root { --token: v }` sets a `Theme` property;
`Type { prop: v }` / `Type:hover` / `Type::part` set a control type's default `ControlStyle`;
`@media (prefers-color-scheme / prefers-reduced-motion)`, `transition` and `@keyframes` are supported
(classes and ids are a proposal, `docs/theming-selectors-plan.md`).
`ThemeStyleSheet.Parse` never throws — it collects `ThemeCssDiagnostic`s and keeps the valid rest — and
exposes a host-neutral model (`Tokens`, `Rules`) that other hosts consume instead of re-parsing.
`ThemeCssReference` is the single description of the grammar; extend it (selector, part, property) and
the parser, docs and Studio follow. The rule the whole subset is built on: **never a silent no-op** —
anything not understood is an error, anything a host cannot express is a diagnostic.

## Tests: prove they can fail

Before claiming a new test covers a fix, break the fix and watch the test go red. Assert relationships
and mechanism-specific values, not "some ink here" or absolute pixel thresholds (fonts measure 11px on
macOS, 13px elsewhere). Render through `HeadlessRenderer` rather than showing windows.

## Git

Do not commit or push on the user's behalf; leave the working tree for them to review. Branch off
`main`; PRs target `main` and must be up to date with it before merging.

Do not add `Co-Authored-By`, `Claude-Session`, "Generated with Claude Code", or similar
attribution/session trailers to commit messages or PR descriptions — plain messages only.
