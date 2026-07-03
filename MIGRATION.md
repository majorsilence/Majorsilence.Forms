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

## What it does

1. **Project files** (`.csproj`/`.vbproj`): removes `UseWindowsForms`/`UseWPF`, drops the
   `-windows` TFM suffix (`net8.0-windows` → `net8.0`, including in imported `.props`/`.targets`),
   drops the Windows-desktop framework reference, removes WinForms-only NuGet packages (Telerik UI
   for WinForms, DevExpress, ...), and adds a `Majorsilence.Forms` + backend reference — only to
   projects that are/use WinForms; class libraries with no UI are left untouched.
2. **Source files** (`.cs`/`.vb`): rewrites namespaces via a longest-prefix-first table (see
   [Namespace mapping](#namespace-mapping) below), collapses duplicate `using`/`Imports` lines that
   result from multiple source namespaces mapping to the same target, and — for VB — injects the
   implicit WinForms constructor lost when `MyType=Empty` no longer applies, generates a
   `My.Resources` accessor, and warns on the remaining unimplemented `My.*` framework usage (see
   [VB Application Model](#vb-application-model-myapplication-myforms)).
3. **Resx files**: scans for image/type references that need to survive the framework swap, and — for
   a VB project's `My Project\Resources.resx` — generates the `My.Resources` accessor module described
   above.
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

**Mostly not implemented — three narrow, usage-driven exceptions.** The classic VB "My" application
model — `My.Application` (single-instance handling, startup/shutdown events, splash screens),
`My.Forms` (implicit form instances), `My.User`, `My.Computer` (registry/clipboard/OS info) — is
deeply tied to `Microsoft.VisualBasic` Windows-only infrastructure and thread-static application
contexts that don't map cleanly onto a cross-platform backend model. A feasibility audit against a
real, large WinForms codebase found this surface is used exhaustively in a handful of designer/settings
boilerplate files but, outside that boilerplate, real hand-written application code touches only three
narrow pieces — so those three are now implemented for real, while everything else still **warns**
rather than being silently rewritten.

### Implemented

- **`My.Application.Info.*`** — a small `Majorsilence.Forms.ApplicationInfo` facade (exposed as
  `Application.Info`) wrapping the assembly-metadata reflection `Application` already computes.
  Covers `Title`, `AssemblyName`, `Version` (a real `System.Version`, not a string — confirmed against
  code that calls `.ToString()` on it), `Copyright`, `CompanyName`, `Description`, and `ProductName`.
  Observed real usage: **8 occurrences in 1 file** (`AboutFixed.vb`'s About-box population).
- **`My.Resources.*`** — the migrator generates a companion `My Project\Resources.vb` module for each
  project's `My Project\Resources.resx` (replacing the excluded, non-compiling
  `Resources.Designer.vb`), embedding the `.resx` content and exposing one property per resource,
  typed to match real call sites exactly: image entries (`System.Drawing.Bitmap`/`Image`/`Icon`)
  return `Majorsilence.Drawing.Image` (works both for a direct assignment and an explicit
  `CType(My.Resources.X, Majorsilence.Drawing.Image)`), `System.Byte[]` entries return `Byte()` (the
  shape `BinaryWriter.Write` needs), and everything else returns `String`. Every property forwards to
  `Majorsilence.Forms.ComponentResourceManager`. Observed real usage: **55 occurrences across 25
  files, 26 distinct resource names** (mostly images; 8 occurrences across 5 files are byte-array
  file exports). One known gap: resx entries stored as `System.Resources.ResXFileRef` (a resource
  added as a linked file rather than inline data) compile fine but resolve to `null` at runtime,
  because `ComponentResourceManager` only reads inline `.resx` data — a pre-existing limitation of
  that type, not something this feature widens.
- **`My.Computer.Name`** — a minimal `Majorsilence.Forms.ComputerInfo` type with a `Name` property
  forwarding to `Environment.MachineName`. Deliberately just `.Name`: no other `My.Computer` member
  (Registry/Clipboard/Info/FileSystem/...) had any observed usage. Observed real usage: **1
  occurrence** (a debug-only machine-name check).

The migrator's `My.*` warning is narrowed to match exactly: `My.Application.Info.*`, `My.Resources.*`,
and `My.Computer.Name` no longer produce a manual-review warning, while every other `My.*` reference
— including *other* `My.Application.*`/`My.Computer.*` members (`My.Application.Log`,
`My.Application.Shutdown`/`Startup`, `My.Computer.Registry`/`Clipboard`/`Info`) plus `My.Forms`,
`My.Settings`, `My.User` — still warns exactly as before.

### Still not implemented, and why

`My.Forms` (implicit per-form singletons), `My.Application`'s lifecycle events
(`Startup`/`Shutdown`/`UnhandledException`, splash screens), `My.Settings`, and
`My.Computer.Registry`/`.Clipboard`/`.Info` remain unimplemented: the audit found **zero** real
hand-written usage of any of them (the only hits were inside auto-generated `My
Project\Settings.Designer.vb` boilerplate, never invoked from actual application logic), and several
are genuinely Windows-specific with no clean cross-platform equivalent (the registry and clipboard
have no portable substitute; `My.Application`'s lifecycle events are tied to a message-pump timing
model Majorsilence.Forms doesn't replicate). Developers hitting these still re-plumb by hand
(typically: replace `My.Forms.X` with an explicit field/DI instance, replace
`Application.Run(New MainForm())`-style startup with Majorsilence.Forms' `Application.Run`, and drop
single-instance/splash-screen logic in as needed for the target platform). Should real usage turn up
in the future, the same usage-driven approach applies: implement the narrow slice that's actually
used, not the full historical API surface.

What the rewriter *also* still handles for VB, independent of the above: the implicit parameterless
constructor that `MyType=Empty` provides for a form (removed once the file leaves the classic VB
compiler pipeline) is re-injected automatically, using cross-file knowledge of a form's designer
partial so it's never duplicated or written into the wrong file.

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
