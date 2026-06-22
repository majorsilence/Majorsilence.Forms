# majorsilence-migrate

A console tool that converts WinForms source and projects to [Majorsilence.Forms](../../README.md).

```
dotnet run --project tools/Majorsilence.Forms.Migrator -- <input> [options]
```

`<input>` is a `.sln`, a `.csproj`/`.vbproj`, a directory, or a single `.cs`/`.vb`/`.resx` file.

## What it does

### Project files (`.csproj`)
- Removes `UseWindowsForms` / `UseWPF` / `EnableWindowsTargeting`.
- Retargets the framework (e.g. `net8.0-windows` → `net10.0`).
- Drops the `Microsoft.WindowsDesktop.App` framework reference.
- Adds `Majorsilence.Forms` + a platform backend reference (package or project).
- Skips legacy non-SDK projects and malformed XML with a warning (never throws).

### Source files (`.cs` / `.vb`)
- `System.Windows.Forms` → `Majorsilence.Forms` (using directives and fully-qualified refs).
- `System.Drawing` is **split three ways**:
  - Primitive value types (`Color`, `Point`, `Size`, `Rectangle`, `PointF`, `SizeF`, `RectangleF`)
    are framework types Majorsilence.Forms keeps — **left untouched**.
  - GDI+ types (`Bitmap`, `Brush`, `Pen`, `Font`, …) → `Majorsilence.Drawing`.
  - WinForms-compat types Majorsilence relocates (`Graphics`, `ContentAlignment`, `SystemColors`,
    `SystemBrushes`, `SystemPens`, `SystemFonts`, `ColorTranslator`) → `Majorsilence.Forms`.
  - A bare `using System.Drawing;` is kept and a `using Majorsilence.Drawing;` companion is added.
- `System.Drawing.Drawing2D` / `.Imaging` / `.Text` → `Majorsilence.Drawing.*`; `.Printing` → `Majorsilence.Forms.Printing`.
- `ApplicationConfiguration.Initialize()` is commented out (no equivalent; styles are implicit).
- Anything it can't confidently map (e.g. `Graphics`, `System.Drawing.Design`, `VisualStyles`)
  is **flagged for manual review**, not silently mangled.

### Solutions (`.sln`)
When the input is a solution and `--output` is set, every project it lists is converted and the `.sln`
itself is copied into the output tree (project paths are preserved), so the result opens directly.

### Visual Basic (`.vbproj` / `.vb`)
- `.vbproj` projects are discovered and converted alongside `.csproj`.
- Forces `<MyType>Empty</MyType>` — the VB "My" application framework (`MyType=Windows/WindowsForms`)
  pulls in `Microsoft.VisualBasic.Devices.Computer`, `WinForms.Form` and `ApplicationSettingsBase`,
  none of which exist cross-platform.
- Excludes the generated `My Project\*.Designer.vb` files (they don't compile with `MyType=Empty`).
- Rewrites project-level VB global imports (`<Import Include="System.Windows.Forms" />`).
- Warns about the consequences a human must address: with `MyType=Empty` there's no auto entry point
  (add a `Module` with `<STAThread> Sub Main` + `<StartupObject>`) and no auto form constructor
  (each `Form` needs a `Public Sub New()` calling `InitializeComponent()`). Also flags `My.*` usage,
  Windows-only `ComputerInfo`, and the ambiguous `ContentAlignment`.

### Resources (`.resx`)
- Read-only scan. Designer-serialized resources (typed `System.Drawing`/`System.Windows.Forms`
  values, binary blobs) are flagged — Majorsilence.Forms builds UI in code. Plain string tables pass clean.

## Options

| Option | Description |
| --- | --- |
| `-o, --output <dir>` | Write to a mirrored tree. Omit to convert in place (leaves `.bak` files). |
| `-n, --dry-run` | Report changes without writing. |
| `--diff` | Print a unified diff for each changed file. |
| `--backend <name>` | `avalonia` (default) \| `uno` \| `headless`. |
| `--references <mode>` | `package` (default) \| `project`. |
| `--tfm <tfm>` | Target framework (default `net10.0`). |
| `--package-version <v>` | NuGet version for package references (default `0.3.0`). |
| `--repo-root <dir>` | Repo root for resolving `--references project` paths. |
| `--map <file>` | JSON file of extra namespace mappings (repeatable). |
| `--strict` | Exit non-zero if any manual-review warning is produced (CI gate). |
| `--report <file>` | Path for the Markdown report (default `migration-report.md` by the output). |
| `--no-report` | Don't write the report. |

## Custom mappings

Bring third-party controls across with a JSON map (layered on the built-in rules):

```json
{
  "namespaces": {
    "Telerik.WinControls.UI": "Majorsilence.Forms.Telerik",
    "DevExpress.XtraEditors":  "Majorsilence.Forms.DevExpress"
  }
}
```

```
majorsilence-migrate ./LegacyApp --map telerik.json --strict
```

## Caveats

This is a **textual** transform, not a Roslyn rewrite — it's fast and tolerant of code that doesn't
currently compile, but type- and member-level API differences between WinForms and Majorsilence.Forms
still need a build pass and manual fixes afterward. Always build the converted project and work through
the `migration-report.md` before committing.
