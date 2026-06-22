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
- `System.ComponentModel.ComponentResourceManager` → `Majorsilence.Forms.ComponentResourceManager`
  (the cross-platform resource manager — see Resources below). Only that one type is redirected;
  the rest of `System.ComponentModel` is left alone.
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
- **Adds the missing form constructor automatically.** `MyType=Empty` removes VB's implicit WinForms
  constructor, so each `Form` whose designer defines `InitializeComponent` but has no `Public Sub New()`
  gets one injected (after `Inherits`), marked with a `' [majorsilence-migrate]` comment. It only warns
  if it can't find where to insert.
- **Entry-point warning only for executables.** `MyType=Empty` removes the auto entry point, but only
  `Exe`/`WinExe` projects need one — class libraries (the SDK default) are not flagged.
- **Skips the `My Project\*.Designer.vb` files** (Resources/Settings/Application). The project converter
  excludes them from compilation, so they're neither converted nor flagged.
- Remaining warnings a human still owns: real-exe entry points, and `My.*` usage — flagged with a
  member-specific hint (`My.Resources` → load from the `.resx`; `My.Settings` → a config/JSON-backed
  settings class; `My.Application`/`My.Computer` → `AppContext`/`System.IO`/`RuntimeInformation`).

### Typed DataSets (`.xsd` + `.Designer.vb`)
These are pure ADO.NET (`System.Data`) — no WinForms UI — so they work cross-platform as-is. The migrator
does not modify them. (Their `System.ComponentModel.Design.HelpKeywordAttribute` is a design-time
attribute that ships in the BCL, so it is **not** flagged.)

### Resources (`.resx`)
Majorsilence.Forms ships a cross-platform `ComponentResourceManager` that reads the `.resx` XML directly
(no GDI+, no `BinaryFormatter`), so migrated designer code — `resources.GetObject(...)`,
`resources.GetString(...)`, `resources.ApplyResources(ctrl, name)` — works at runtime on every OS.
Because of that, the migrator only flags what genuinely can't be carried across:
- **Plain string tables**, **primitive designer values** (`Point`/`Size`/`Color`/`Boolean`/`Int32`/…),
  and **images stored as `bytearray.base64`** (the modern form — raw image bytes decoded by SkiaSharp)
  are all *consumable*. They're not flagged; with `--output` the `.resx` is copied into the mirrored tree.
- **`BinaryFormatter`/SOAP blobs are recovered from the NRBF wire format** (via `System.Formats.Nrbf`),
  without running `BinaryFormatter`:
  - `System.Drawing.Bitmap`/`Icon` → the embedded image bytes;
  - `System.Windows.Forms.ImageListStreamer` → the comctl32 image-list strip (RLE + BMP + mask) decoded
    into per-frame images;
  - `System.Data.SqlTypes` scalars and `DBNull` (a `SqlCommand`'s design-time parameter defaults).
  These are *consumable*, not flagged.
- **The genuinely unportable cases** are flagged with case-specific guidance: **ActiveX/COM control state**
  (`AxHost`/OCX) has no managed equivalent — replace the control; any **other** serialized object must be
  re-created in code (`BinaryFormatter` is gone from modern .NET).

> To let `new ComponentResourceManager(typeof(Form))` find its `.resx` at runtime, embed the **raw**
> `.resx` (logical name `<FullTypeName>.resx`) or keep it beside the assembly. Constructing from an
> explicit `.resx` via `ComponentResourceManager.FromFile/FromStream/FromXml` always works.

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
