# Majorsilence.Forms.WinFormsShims.Compat

`System.Windows.Forms` for every platform, from the same source as [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms).
This package **is** Majorsilence.Forms, generated at build time with the namespaces and type names real
WinForms uses, so source written against WinForms — C# **and Visual Basic**, Designer files included —
compiles unchanged:

```csharp
using System.Windows.Forms;      // C#
```
```vb
Imports System.Windows.Forms     ' Visual Basic: Handles, WithEvents, Inherits Control, ...
```

It exists for the case where rewriting namespaces is not an option: a distributed control library whose
public API is typed to `System.Windows.Forms`/`System.Drawing`, or a large codebase you want running
before you touch it. If you *can* rewrite, use Majorsilence.Forms directly (the migrator's default).

```
dotnet add package Majorsilence.Forms.WinFormsShims.Compat
dotnet add package Majorsilence.Forms.WinFormsShims.Compat.Avalonia   # a backend; the desktop host
```

## What you get

Three assemblies, mirroring the three Majorsilence.Forms projects they are generated from:

| Package | Generated from | Declares |
|---|---|---|
| `Majorsilence.Forms.WinFormsShims.Compat` | `Majorsilence.Forms` | `System.Windows.Forms`, `System.Drawing.Printing`, `System.ComponentModel.Design`, … |
| `Majorsilence.Forms.WinFormsShims.Compat.Drawing` | `Majorsilence.Forms.Drawing.Common` | `System.Drawing`, `.Drawing2D`, `.Imaging`, `.Text` (`Font`, `Brush`, `Pen`, `Bitmap`, `GraphicsPath`, …) |
| `Majorsilence.Forms.WinFormsShims.Compat.Avalonia` | `Majorsilence.Forms.Avalonia` | the Avalonia backend, hosting the `System.Windows.Forms` `Form` |

Because they are the real classes under the real names — not wrappers or subclasses — there is nothing to
work around: `Control` is the base of every control, `Inherits Control` and `Handles btn.Click` work, a
`Control` variable holds a `TextBox`, and events and enum properties have their real types.

## Use it *instead of* Majorsilence.Forms

These are standalone copies. Referencing both a Compat package and `Majorsilence.Forms` gives a project
two unrelated `Form` classes, in different namespaces. Pick one family per application.

Types the .NET framework already ships in `System.Drawing` (`Color`, `Point`, `Size`, `Rectangle`,
`SystemColors`, `ColorTranslator`, `ColorConverter`, …) are the framework's own, not copies: the generator
refuses to declare a type the framework has, since that would make every use of the name ambiguous.

## How it is made

Nothing under `generated/` is checked in or hand-written. Each project's build runs
[`tools/CompatSync`](../../tools/CompatSync) — a single-file `dotnet` tool (`dotnet run
tools/CompatSync/CompatSync.cs`) — which copies the real sources, rewrites them according to
[`compat-rules.json`](../../tools/CompatSync/compat-rules.json), and writes the result here. The real
projects are never touched, and an unchanged tree is skipped on the next build. See the tool's README for
what the rewrite does and why it is not a find-and-replace.

## Known limits

- **Desktop only so far.** The Avalonia compat build targets `net8.0`/`net10.0`; the browser, Android and
  iOS rows, and the Uno, GTK, WPF and Headless backends, are not generated yet.
- **Not for a machine with real WinForms in the same project.** On Windows, referencing
  `Microsoft.WindowsDesktop.App` as well declares `System.Windows.Forms.Form` twice.
- **Telerik.** The earlier generator also mapped `Majorsilence.Forms.Telerik` onto the `Telerik.WinControls`
  namespaces; that has not been carried over.
- **Behaviour is Majorsilence.Forms'.** This changes the namespace your code compiles against, not what
  runs behind it; `COMPATIBILITY_MATRIX.md` applies unchanged.
