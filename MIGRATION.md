# Migrating to Majorsilence.Forms

This document describes `majorsilence-migrate`, the CLI tool that automates moving a WinForms
solution onto Majorsilence.Forms, and how to interpret its output. For what's actually implemented
once your code compiles against Majorsilence.Forms, see [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md).

Source: [`tools/Majorsilence.Forms.Migrator`](tools/Majorsilence.Forms.Migrator).

## Installing

It ships to nuget.org as a .NET global tool:

```
dotnet tool install -g Majorsilence.Forms.Migrator
majorsilence-migrate --help
```

Update with `dotnet tool update -g Majorsilence.Forms.Migrator`. Prefer a per-repo install? Use
`dotnet tool install Majorsilence.Forms.Migrator` inside a repo with a tool manifest
(`dotnet new tool-manifest`) and run it as `dotnet majorsilence-migrate`.

Two alternatives, if you'd rather not install a tool: each GitHub release also attaches a
self-contained single-file binary per platform (no .NET runtime needed), and from a clone of this
repo you can always run `dotnet run --project tools/Majorsilence.Forms.Migrator -- <input>`.

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
| `using`/`Imports` reconciliation for the `System.Drawing`/`Majorsilence.Forms.Drawing` pair | Reimplemented |
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
   for WinForms, DevExpress, **`System.Drawing.Common`**, ...), and adds a `Majorsilence.Forms` +
   backend reference — to every project the rewrite actually touches; class libraries with no UI are
   left untouched. (`--dual-build` changes this — see
   [Incremental migration](#incremental-migration-building-against-both---dual-build).)

   Two parts of that are easy to get wrong by hand. **`System.Drawing.Common` has to go**, not merely
   because it is Windows-only from .NET 7 on, but because leaving it referenced puts
   `System.Drawing.Bitmap`/`Font`/`Pen`/... back in scope beside the `Majorsilence.Forms.Drawing`
   replacements the rewrite just introduced — every unqualified use then fails as an ambiguous
   reference rather than resolving to the port. And **"projects the rewrite touches" is wider than
   "WinForms projects"**: the plain class libraries a WinForms solution carries alongside its UI
   projects often never mention `System.Windows.Forms`, yet an image or font helper in one still gets
   rewritten to `Majorsilence.Forms.Drawing.*` and cannot compile without the reference. Projects
   using only the primitives that stay in `System.Drawing` (`Color`, `Point`, `Size`, ...) are still
   left alone — nothing in them changes.
2. **Source files** (`.cs`/`.vb`): rewrites namespaces via a longest-prefix-first table (see
   [Namespace mapping](#namespace-mapping) below), collapses duplicate `using`/`Imports` lines that
   result from multiple source namespaces mapping to the same target, emits a using-alias for the
   handful of names a kept `System.Drawing` import would otherwise make ambiguous (see
   [Namespace mapping](#namespace-mapping)), and — for VB — injects the
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
      --engine <name>     Source-rewrite engine: text (default) | roslyn. See "The optional
                          Roslyn engine" above — roslyn requires a loadable project, is much
                          slower, and correctly disambiguates same-named types; falls back to
                          text per-project on load failure, or for the whole run when the
                          input has no project to load.
      --tfm <tfm>         Force a target framework. Default: keep the project's version and
                          just drop the -windows suffix (net8.0-windows -> net8.0).
      --package-version <v>  NuGet version for package references. Defaults to the migrator's own
                          version — the tool and the packages ship from the same release.
      --repo-root <dir>   Repo root for resolving --references project paths (default: cwd).
      --map <file>        JSON file of extra namespace mappings (repeatable — e.g. a
                          third-party control vendor not already built in).
      --dual-build        Keep the project buildable against real WinForms too — see
                          "Incremental migration" below.
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

### Incremental migration: building against both (`--dual-build`)

By default, `majorsilence-migrate` commits a project to Majorsilence.Forms outright: `UseWindowsForms`,
the `-windows` TFM suffix, and WinForms-only packages are all removed, and every
`using System.Windows.Forms;` becomes `using Majorsilence.Forms;` unconditionally. `--dual-build`
instead lets a project (a C# one — see the VB note below) build against **either** stack, switched by
one MSBuild property, so a Windows developer can start migrating and keep building against real
WinForms until they're satisfied with the result:

* **Project files are left otherwise untouched** — `UseWindowsForms`, the `-windows` TFM, and any
  WinForms-only NuGet packages all stay, so the `#else` branch below still compiles. Majorsilence.Forms
  + a backend reference are still added alongside them, and a
  `<DefineConstants Condition="'$(MAJORSILENCE_FORMS)' == 'true'">$(DefineConstants);MAJORSILENCE_FORMS</DefineConstants>`
  is added to propagate the switch.
* **Source files**: only the top-of-file `using System.Windows.Forms;` (and, when it would be added,
  the `Majorsilence.Forms.Drawing` companion import) becomes conditional:
  ```csharp
  #if MAJORSILENCE_FORMS
  using Majorsilence.Forms;
  #else
  using System.Windows.Forms;
  #endif
  ```
  This is deliberately narrow: any *fully-qualified* reference elsewhere in a file's body (e.g.
  `System.Windows.Forms.MessageBox.Show(...)`) is still rewritten unconditionally, exactly as without
  `--dual-build` — that statement only compiles once `MAJORSILENCE_FORMS` is defined. Most WinForms
  source relies on unqualified type names via the top-of-file import, which is what this targets.

To flip a build over, add to a repo-root `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <MAJORSILENCE_FORMS>true</MAJORSILENCE_FORMS>
  </PropertyGroup>
</Project>
```

Leave it unset (or `false`) and every converted project keeps building exactly as it did before —
against real `System.Windows.Forms`/`System.Drawing`. Set it to `true` once you're satisfied with the
Majorsilence.Forms side, then finish the migration by running the tool again without `--dual-build` (or
manually dropping the `#else` branches and the now-unneeded WinForms plumbing).

Not offered for VB: `MyType=Empty` switches off the whole VB "My" application framework (implicit
constructor, `My.*`, ...), which can't be toggled by a preprocessor symbol the same way a plain import
can. A VB project/file passed `--dual-build` is converted the normal, fully-committed way instead, with
a warning explaining why.

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
| `System.Drawing` — **GDI+ types** (`Bitmap`, `Font`, `Pen`, `Brush`, `Graphics`-adjacent types, etc.) | `Majorsilence.Forms.Drawing` | GDI+ is Windows-only in `System.Drawing.Common`; Majorsilence reimplements it cross-platform on SkiaSharp. |
| `System.Drawing.Drawing2D` / `.Imaging` / `.Text` | `Majorsilence.Forms.Drawing.Drawing2D` / `.Imaging` / `.Text` | Same GDI+ split, sub-namespaced. |
| `System.Drawing.Printing` | `Majorsilence.Forms.Printing` | Printing lives on the Forms side of the compat layer, not the drawing side. |
| `System.Windows.Forms.VisualStyles`, `System.Drawing.Design`, `System.ComponentModel.Design` | *left unchanged* | No Majorsilence equivalent — flagged for manual review rather than rewritten into something that doesn't exist. |

An unqualified GDI+ type used under a bare `using System.Drawing;` (no prefix for the rewriter to
anchor on) is also caught: a name-match warns on `Metafile`, `ImageAttributes`, `ColorMatrix`, and
similar Windows-only types that would otherwise be silent compile breaks.

### Ambiguous names: `SystemColors` and `ColorTranslator`

Most of `System.Drawing` lives in the Windows-only `System.Drawing.Common`, which a migrated project
stops referencing — so once `Graphics`, `SystemBrushes`, `SystemPens`, `SystemFonts` and
`ContentAlignment` are redirected to `Majorsilence.Forms`, the `System.Drawing` name is simply gone and
there is nothing to collide with.

`SystemColors` and `ColorTranslator` are the exceptions: they ship in `System.Drawing.Primitives`, part
of the shared framework, so they are *still* resolvable through the `using System.Drawing;` the migration
keeps for the primitives. Used unqualified — `SystemColors.ControlText`, exactly how WinForms code is
written — the name then has two candidates, `System.Drawing.SystemColors` and
`Majorsilence.Forms.SystemColors`, and the file fails to compile with CS0104.

The converter emits a using-alias pinning the name to the Majorsilence one:

```csharp
using System.Drawing;
using SystemColors = Majorsilence.Forms.SystemColors;
```

One line fixes every use site in the file, so the code below it reads exactly as it did before the
migration. The alias is added only to files that actually use the name unqualified, and re-running the
tool over an already-migrated tree won't add it twice.

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
  return `Majorsilence.Forms.Drawing.Image` (works both for a direct assignment and an explicit
  `CType(My.Resources.X, Majorsilence.Forms.Drawing.Image)`), `System.Byte[]` entries return `Byte()` (the
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

## Breaking change: `SplitContainer.Orientation`

`SplitContainer.Orientation` — and `Splitter.Orientation` with it — now means what it means in
WinForms: **the direction of the splitter bar, not of the layout.**

| | Panels side by side | Panels stacked |
|---|---|---|
| WinForms, and Majorsilence.Forms now | `Orientation.Vertical` (the default) | `Orientation.Horizontal` |
| Majorsilence.Forms before | `Orientation.Horizontal` (the default) | `Orientation.Vertical` |

**If you never set `Orientation`, nothing changes** — the default arrangement is still panels side by
side; only the name for it moved. **If you did set it, invert it**, or the layout flips.

Nothing warns you: both values compile before and after. The migrator does not rewrite this either,
because it cannot tell a `SplitContainer.Orientation` from any other `Orientation` in a textual pass —
so a grep for `Orientation` on your `SplitContainer` and `Splitter` instances is worth the minute.

This was a deliberate correction rather than an accident of the port. The old reading also made
`SplitterDistance` inconsistent with itself (its getter and setter disagreed about which panel
dimension it meant), and left `Splitter`'s resize cursor describing the opposite of the bar it was on.

## Renamed to match WinForms: `TreeViewDrawMode.OwnerDrawContent`

`TreeViewDrawMode` had an invented member name. What WinForms calls `OwnerDrawText` was spelled
`OwnerDrawContent` here, and `OwnerDrawAll` was missing entirely — so a tree view could hand over the
text of a node but never the whole row.

Both WinForms names now exist, with WinForms' numbers, and the tree view honours them separately:
`OwnerDrawText` raises `DrawNode` after the background and focus cue are painted, `OwnerDrawAll`
raises it before anything is painted at all.

**Nothing breaks.** `OwnerDrawContent` is still there as an `[Obsolete]` alias with the same value, so
existing code compiles and behaves as before; you will get a warning pointing at the new name. It will
be removed in a future release.

Two members of `DataGridViewDataErrorContexts` went the other way and were **removed**:
`RowDirtyStateNeeded` and `CleanupExceptionHandling`. Neither is a WinForms member, neither was used
anywhere, and the second duplicated `Commit`'s numeric value — which made `ToString()` on a persisted
context able to name something the writer never chose. The WinForms members that belong at those
values, `RowDeletion` and `ClipboardContent`, are now present.

## Breaking change: event delegate types now match WinForms

Designer-generated code wires events up with an explicitly constructed delegate — `this.textBox.KeyDown
+= new KeyEventHandler(this.TextBox_KeyDown);`, `this.MnuFileNew.Click += new
System.EventHandler(this.MnuFileNew_Click);` — and C# will not convert between two delegate types just
because their signatures agree. Several events here were declared as `EventHandler<TArgs>` where WinForms
uses a named delegate, so every one of those designer lines failed to compile. They now use the WinForms
delegate:

| Event | Was | Now |
|---|---|---|
| `Control.KeyDown`, `.KeyUp` | `EventHandler<KeyEventArgs>` | `KeyEventHandler` |
| `Control.MouseDown`, `.MouseUp`, `.MouseMove`, `.MouseWheel`, `.MouseClick`, `.MouseDoubleClick` | `EventHandler<MouseEventArgs>` | `MouseEventHandler` |
| `Form.FormClosing` | `EventHandler<FormClosingEventArgs>` | `FormClosingEventHandler` |
| `PrintDocument.PrintPage` | `EventHandler<PrintPageEventArgs>` | `PrintPageEventHandler` |
| `Control.MouseEnter` | `EventHandler<MouseEventArgs>` | `EventHandler` |
| `MenuItem`/`ToolStripItem`/`ToolStripMenuItem`/`ToolStripButton``.Click` | `EventHandler<MouseEventArgs>` | `EventHandler` |

**Handlers written as lambdas or method groups keep compiling** — the parameter types are unchanged for
the first four rows, so `c.KeyDown += (s, e) => e.KeyCode` is unaffected. Two things do break, both
deliberately:

* **Explicitly constructed `new EventHandler<KeyEventArgs>(...)`** (and the other typed forms) no longer
  converts. Drop the wrapper (`c.KeyDown += Handler;`) or name the WinForms delegate.
* **`Click` and `MouseEnter` no longer carry mouse coordinates**, because in WinForms they never did —
  `Click` is an `EventArgs` event and `MouseClick` is the typed variant. A handler that read `e.X`/`e.Button`
  off a `Click` must move to `MouseClick`; on a menu item, which has no mouse-typed variant in WinForms
  either, take the position from the owning control. The related constructor and factory overloads
  (`new MenuItem(text, image, onClick)`, `MenuItemCollection.Add(text, image, onClick)`,
  `new ToolStripMenuItem(...)`, `new ToolStripButton(...)`) take `EventHandler` to match.

`Control.OnMouseEnter` changes signature with its event, from `OnMouseEnter(MouseEventArgs)` to WinForms'
`OnMouseEnter(EventArgs)` — an override of the old signature fails to compile with CS0115 rather than
silently not being called. Where a control genuinely needs the pointer position at entry, the framework
records it internally (that is how `ToolTip` still places its popup at the cursor).

## Moved to match GDI+: the gradient and hatch brushes

`LinearGradientBrush`, `PathGradientBrush`, `HatchBrush` and `HatchStyle` were in
`Majorsilence.Forms.Drawing`. GDI+ puts them in `System.Drawing.Drawing2D`, not `System.Drawing`, so they
have moved to **`Majorsilence.Forms.Drawing.Drawing2D`** to match. `Brush`, `SolidBrush` and
`TextureBrush` stay put — those really are `System.Drawing` types.

This is what the namespace mapping already promised: `using System.Drawing.Drawing2D;` is rewritten to
`using Majorsilence.Forms.Drawing.Drawing2D;`, and before this move that import resolved none of the
brushes it was supposed to. Code that reaches them through the rewritten import — the overwhelmingly
common case — is unaffected. Only a **fully-qualified** `Majorsilence.Forms.Drawing.LinearGradientBrush`
needs updating, to `Majorsilence.Forms.Drawing.Drawing2D.LinearGradientBrush`.

## Compile-and-approximate, not pixel-perfect

Once your code compiles against Majorsilence.Forms, not every property/event is fully wired — some
are safe no-ops. See [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md) for exactly which members
of which types are stubbed versus fully functional, and the [stub policy](COMPATIBILITY_MATRIX.md#stub-policy)
that governs how a not-yet-implemented member behaves at runtime.
