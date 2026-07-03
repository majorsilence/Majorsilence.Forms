# Migrating to Majorsilence.Forms

This document describes `majorsilence-migrate`, the CLI tool that automates moving a WinForms
solution onto Majorsilence.Forms, and how to interpret its output. For what's actually implemented
once your code compiles against Majorsilence.Forms, see [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md).

Source: [`tools/Majorsilence.Forms.Migrator`](tools/Majorsilence.Forms.Migrator).

## Design: a textual rewriter, not a Roslyn transform — on purpose

`majorsilence-migrate` does **not** parse a syntax tree or resolve symbols. It's a deliberate,
multi-pass **textual/regex rewriter** over the raw source text. From the tool's own source comment:

> This is a deliberately textual transform — it does not parse the syntax tree — which keeps it
> fast and tolerant of files that don't currently compile.

That trade-off is the point, not a shortcut:

- **Works on broken code.** A half-migrated solution, a `.vb` file that references a type nobody's
  ported yet, a project with a missing reference — none of that stops the rewriter, because it
  never needs the code to compile or even parse. A Roslyn-based tool with real symbol resolution
  would refuse to touch a project until it builds, which defeats the point of a *first pass* over a
  legacy codebase.
- **Fast.** No compilation, no `MSBuildWorkspace`, no project graph loading. It runs over
  thousands of files in seconds.
- **What it gives up:** no true cross-project symbol resolution. The rewriter can't tell "is this
  `Panel` a `System.Windows.Forms.Panel` or your own class also named `Panel`?" — it relies on
  namespace-prefix patterns and `using`/`Imports` context instead. In practice this is rarely
  ambiguous (WinForms/Telerik type names are distinctive), and every namespace it doesn't
  recognize gets flagged for manual review rather than silently guessed at.

If you need real symbol-aware rewriting (e.g. disambiguating a genuinely name-colliding custom
type), that's a natural candidate for an optional, heavier second pass layered on top of the fast
textual one — not a replacement for it.

### The optional Roslyn engine (`--engine roslyn`)

That "natural candidate" above is no longer hypothetical — it shipped as an **opt-in** second
engine, selected with `--engine roslyn` (`--engine text` remains the default and is unchanged byte
for byte). It uses `MSBuildWorkspace` + real Roslyn symbol resolution
(`SemanticModel.GetSymbolInfo`) instead of regexes, which lets it do the one thing the textual
engine categorically cannot: tell a project-local `Panel` apart from `System.Windows.Forms.Panel`
when both are used by bare name in the same file. It is layered *on top of* the textual engine, not
a replacement — several passes are deliberately still textual even in `--engine roslyn` mode (see
the scope table below), and the two engines are designed to produce byte-identical output on every
file where there's no ambiguity for symbol resolution to add value.

**Trade-offs:**

| | `--engine text` (default) | `--engine roslyn` |
|---|---|---|
| Input requirement | Any `.sln`/`.csproj`/`.vbproj`/directory/single file | Needs a loadable `.sln`/`.csproj`/`.vbproj` — a bare directory or single file falls back to the text engine for the whole run, with a warning |
| Tolerates non-compiling code | Yes — never needs the code to parse or build | No — a project has to actually load via MSBuild; a project that "looks fine" can still fail in-process (implicit restore differences, SDK resolver quirks, multi-targeted projects) |
| Speed | Seconds, even for large solutions | Orders of magnitude slower — loading a full solution via MSBuild evaluation dominates the run |
| Cross-project same-named-type disambiguation | No — relies on namespace-prefix/`using` context, which is usually unambiguous but not always | Yes — this is the feature's reason for existing |
| Failure handling | N/A (never fails) | Fails closed **per project**: one project failing to load falls back to the text engine for just that project's files (with a warning), not the whole run. If MSBuild itself can't be located at all, the whole run hard-fails with a non-zero exit — an explicit opt-in should never silently downgrade to the default with no signal |

**When to reach for it:** you've hit a real, confirmed case of the textual engine's known blind
spot — a custom type sharing a bare name with a WinForms/GDI+ type in the same file — and you have
a project that already loads cleanly in MSBuild. For the first pass over a large, possibly
half-broken legacy codebase, keep using the default `--engine text`; run `--engine roslyn`
afterwards, on the now-loadable result, if you still see suspicious rewrites in the diff.

**V1 scope — what actually gets Roslyn treatment:**

| Pass | `--engine roslyn` behavior |
|---|---|
| Namespace-prefix rewrites (`System.Windows.Forms`, Telerik, custom `--map` entries) | Reimplemented with real symbol resolution |
| `System.Drawing` 3-way bucketing (primitive / GDI+ / WinForms-compat) | Reimplemented — **including** types used *unqualified* under a bare `using System.Drawing;`, which the textual engine's corresponding pass can only warn about, never fix (see below) |
| `using`/`Imports` reconciliation for the `System.Drawing`/`Majorsilence.Drawing` pair | Reimplemented |
| `System.ComponentModel.ComponentResourceManager` redirect | Reimplemented (trivial either way — no ambiguity, just consistency with the rest of the engine) |
| Duplicate `using`/`Imports` dedup | Reimplemented, natively via Roslyn's import-line handling |
| `ApplicationConfiguration.Initialize()` comment-out | **Not** Roslyn — runs as the same small textual post-process over this engine's re-serialized output. No ambiguity a symbol resolves: a fixed zero-argument static call is a regex either way |
| Unsupported-namespace / unmapped-Telerik-type warnings | **Not** Roslyn — reused verbatim from the textual engine for the same reason (nothing here is a symbol-resolution question) |
| VB constructor injection + `My.*` warnings | **Not** Roslyn — reused verbatim (`SourceConverter.ApplyVbConstructor`/`WarnVisualBasic`, widened to `internal` for this reuse) |
| Unqualified-GDI+-type-under-bare-import warning (textual engine's corresponding pass) | **Superseded, not reused** — Roslyn mode fixes this case outright instead of flagging it, so it intentionally produces *fewer* warnings here. If you're diffing a `--engine text` report against a `--engine roslyn` report, expect this divergence; it isn't a regression |

**What's NOT better with Roslyn:** the bootstrap comment-out and VB's constructor/`My.*` handling
are exactly as good under either engine, because they were never symbol-resolution problems — a
heavier engine buys nothing there, and reimplementing them "in Roslyn terms" would just be the same
regex wearing a syntax tree as a costume. If your migration pain is in that territory, `--engine
text` already handles it identically, faster.

## What it does

1. **Project files** (`.csproj`/`.vbproj`): removes `UseWindowsForms`/`UseWPF`, drops the
   `-windows` TFM suffix (`net8.0-windows` → `net8.0`, including in imported `.props`/`.targets`),
   drops the Windows-desktop framework reference, removes WinForms-only NuGet packages (Telerik UI
   for WinForms, DevExpress, ...), and adds a `Majorsilence.Forms` + backend reference — only to
   projects that are/use WinForms; class libraries with no UI are left untouched.
2. **Source files** (`.cs`/`.vb`): rewrites namespaces via a longest-prefix-first table (see
   [Namespace mapping](#namespace-mapping) below), collapses duplicate `using`/`Imports` lines that
   result from multiple source namespaces mapping to the same target, and — for VB — injects the
   implicit WinForms constructor lost when `MyType=Empty` no longer applies, and warns on `My.*`
   framework usage (see [VB Application Model](#vb-application-model-myapplication-myforms)).
3. **Resx files**: scans for image/type references that need to survive the framework swap.
4. **Report**: writes a Markdown summary (see [Reading the report](#reading-the-migration-report)).

## CLI usage

```
majorsilence-migrate <input> [options]

<input>   A .sln, .csproj/.vbproj, a directory, or a single .cs/.vb/.resx file.

OPTIONS
  -o, --output <dir>      Write converted files to <dir> (mirrors the input tree).
                          Omit to convert in place (a .bak is left beside each changed file).
  -n, --dry-run           Report what would change without writing anything.
      --no-backup         In-place: don't leave a .bak beside each changed file
                          (e.g. when the source is under version control).
      --diff              Print a unified diff for each changed file.
      --backend <name>    Platform backend to reference: avalonia (default) | uno | headless.
      --references <mode>  How to reference Majorsilence.Forms: package (default) | project.
      --engine <name>     Source-rewrite engine: text (default) | roslyn. See "The optional
                          Roslyn engine" above — roslyn requires a loadable project, is much
                          slower, and correctly disambiguates same-named types; falls back to
                          text per-project on load failure, or for the whole run when the
                          input has no project to load.
      --tfm <tfm>         Force a target framework. Default: keep the project's version and
                          just drop the -windows suffix (net8.0-windows -> net8.0).
      --package-version <v>  NuGet version for package references.
      --repo-root <dir>   Repo root for resolving --references project paths (default: cwd).
      --map <file>        JSON file of extra namespace mappings (repeatable — e.g. a
                          third-party control vendor not already built in).
      --strict            Exit non-zero if any manual-review warning is produced (CI gate).
      --report <file>     Path for the Markdown report (default: migration-report.md by output).
      --no-report         Do not write the migration report.
  -h, --help              Show this help.
```

### Recommended first run

```bash
# On a clean git branch, dry-run first to see the scope:
majorsilence-migrate MySolution.sln --dry-run --diff

# Then commit, and run for real — the diff against the previous commit IS the migration:
git checkout -b migrate-to-majorsilence
majorsilence-migrate MySolution.sln --no-backup
git add -A && git commit -m "Migrate to Majorsilence.Forms"
```

Running in-place on a git-tracked branch (with `--no-backup`, since git is your backup) makes the
migration **idempotent and diffable**: run it, inspect exactly what changed file-by-file, re-run it
safely if you pull in more legacy code later.

### Extending it for a third-party control vendor

Built-in support already covers Telerik UI for WinForms (see
[Third-party control vendors](#third-party-control-vendors-eg-telerik) below). For a vendor with no
built-in mapping, supply a `--map` file:

```json
{
  "namespaces":    { "DevExpress.XtraEditors": "Majorsilence.Forms.DevExpress" },
  "removePackages": [ "DevExpress.Win.*" ]
}
```

`namespaces` entries are merged into the built-in table (longest-prefix-first still applies across
both sets — don't rely on ordering between multiple `--map` files for overlapping prefixes).
`removePackages` are extra WinForms-only package-id globs to strip from project files, on top of
the built-ins.

## Namespace mapping

The rewriter's namespace table has one governing asymmetry:

| Source | Target | Why |
|---|---|---|
| `System.Windows.Forms` (whole namespace) | `Majorsilence.Forms` | The WinForms API surface maps wholesale onto its own namespace. |
| `System.Drawing` — **primitives** (`Color`, `Point`, `PointF`, `Size`, `SizeF`, `Rectangle`, `RectangleF`) | *unchanged* | These ship in `System.Drawing.Primitives` on every OS/platform already — Majorsilence.Forms keeps using them as-is, so a fully-qualified reference is left alone. |
| `System.Drawing` — **GDI+ types** (`Bitmap`, `Font`, `Pen`, `Brush`, `Graphics`-adjacent types, etc.) | `Majorsilence.Drawing` | GDI+ is Windows-only in `System.Drawing.Common`; Majorsilence reimplements it cross-platform on SkiaSharp. |
| `System.Drawing.Drawing2D` / `.Imaging` / `.Text` | `Majorsilence.Drawing.Drawing2D` / `.Imaging` / `.Text` | Same GDI+ split, sub-namespaced. |
| `System.Drawing.Printing` | `Majorsilence.Forms.Printing` | Printing lives on the Forms side of the compat layer, not the drawing side. |
| `System.Windows.Forms.VisualStyles`, `System.Drawing.Design`, `System.ComponentModel.Design` | *left unchanged* | No Majorsilence equivalent — flagged for manual review rather than rewritten into something that doesn't exist. |

An unqualified GDI+ type used under a bare `using System.Drawing;` (no prefix for the rewriter to
anchor on) is also caught: a name-match warns on `Metafile`, `ImageAttributes`, `ColorMatrix`, and
similar Windows-only types that would otherwise be silent compile breaks.

### Third-party control vendors (e.g. Telerik)

Telerik UI for WinForms has a **built-in** mapping onto
[`Majorsilence.Forms.Telerik`](COMPATIBILITY_MATRIX.md#telerik-ui-for-winforms-compat-layer) — no
`--map` file needed. Every Telerik sub-namespace Financial-style codebases actually use collapses
onto the same flat target: `Telerik.WinControls.UI`, `.Enumerations`, `.UI.Docking`, `.UI.Data`,
`.Data`, `.UI.Export`, `.Export`, the document-model namespaces
(`Telerik.WinForms.Documents.*`) used by the rich text editor, and the bare `Telerik.WinControls`
root. Sub-namespaces with genuinely no compat target — `Telerik.WinControls.Themes`, `.Design`,
`.Primitives`, `.Layouts` — are deliberately left unrewritten and flagged, the same as the
unsupported `System.*` namespaces above.

A handful of heavyweight Telerik types have no realistic 1:1 equivalent and are rewritten to a
*working but approximate* implementation rather than left broken — see
[Telerik heavyweight controls](COMPATIBILITY_MATRIX.md#telerik-heavyweight-controls) in the
compatibility matrix for exactly what each one does and doesn't do.

## VB Application Model (`My.Application`, `My.Forms`)

**Not implemented.** The classic VB "My" application model — `My.Application` (single-instance
handling, startup/shutdown events, splash screens), `My.Forms` (implicit form instances), `My.User`,
`My.Computer` — is deeply tied to `Microsoft.VisualBasic` Windows-only infrastructure and thread-static
application contexts that don't map cleanly onto a cross-platform backend model. The migrator
**warns** on any `My.*` reference rather than attempting to rewrite it; developers currently
re-plumb that infrastructure by hand (typically: replace `My.Forms.X` with an explicit field/DI
instance, replace `Application.Run(New MainForm())`-style startup with Majorsilence.Forms'
`Application.Run`, and drop single-instance/splash-screen logic in as needed for the target
platform).

What the rewriter *does* still handle for VB: the implicit parameterless constructor that
`MyType=Empty` provides for a form (removed once the file leaves the classic VB compiler pipeline)
is re-injected automatically, using cross-file knowledge of a form's designer partial so it's never
duplicated or written into the wrong file.

## Reading the migration report

Unless `--no-report` is passed, every run writes a Markdown report (default `migration-report.md`)
with:

- **Scanned vs. changed** counts, so you immediately know the scope of the diff.
- A **per-file change list** for everything the rewriter actually touched.
- A **Manual review** section grouping every warning by cause — unsupported namespace references,
  unqualified GDI+ types under a bare `System.Drawing` import, `My.*` usage, and any project the
  rewriter skipped outright (most commonly: a legacy non-SDK-style `.csproj`/`.vbproj`, which must
  be converted to SDK-style before the rewriter can parse it — this is a `.csproj` file-format
  issue, not a namespace issue, and is called out as its own item so it's obvious it's a
  prerequisite step, not something the migrator missed).

Pass `--strict` in CI to make any manual-review warning a non-zero exit — useful as a gate that
fails a pipeline the moment a migrated branch introduces a new unmapped reference.

## Compile-and-approximate, not pixel-perfect

Once your code compiles against Majorsilence.Forms, not every property/event is fully wired — some
are safe no-ops. See [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md) for exactly which members
of which types are stubbed versus fully functional, and the [stub policy](COMPATIBILITY_MATRIX.md#stub-policy)
that governs how a not-yet-implemented member behaves at runtime.
