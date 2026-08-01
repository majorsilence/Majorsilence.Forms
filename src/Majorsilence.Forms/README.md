# Majorsilence.Forms

**Take your WinForms apps cross-platform — without rewriting them.**

A WinForms-style UI framework for .NET. You keep the programming model you already know — `Form`s,
controls, event handlers, even your `*.Designer.cs` files — and gain Windows, macOS, and Linux, with
mobile and web reachable through the backends below.

> ⚠️ **Beta.** The API is stabilizing and not every WinForms corner is covered yet. Great for new
> cross-platform LOB apps and for migrating real apps today — just pin your version.

## This package needs a backend

`Majorsilence.Forms` contains the controls and does all its own drawing with
[SkiaSharp](https://github.com/mono/SkiaSharp). It deliberately references **no windowing toolkit**,
so on its own it cannot open a window. Add one backend package alongside it:

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Avalonia   # desktop default
```

## Minimal app

```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
```

```csharp
using Majorsilence.Forms;

public class MainForm : Form
{
    public MainForm ()
    {
        Text = "Hello";
        Controls.Add (new Label { Text = "Hello, Majorsilence.Forms!", Left = 20, Top = 20 });
    }
}

internal static class Program
{
    [STAThread]
    private static void Main () => Application.Run (new MainForm ());
}
```

Referencing `Majorsilence.Forms.Avalonia` is all the configuration a desktop app needs — the backend
is discovered automatically. To use a different one, set it before the first window is created:

```csharp
Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.Headless.HeadlessPlatformBackend ();
```

Or scaffold the whole thing:

```bash
dotnet new install MajorsilenceForms.Templates
dotnet new majorsilenceforms
dotnet run
```

## The package family

| Package | Use it for |
|---|---|
| **`Majorsilence.Forms`** | The controls and the WinForms-compatible API. Always required. |
| [`Majorsilence.Forms.Avalonia`](https://www.nuget.org/packages/Majorsilence.Forms.Avalonia) | Desktop backend (Windows/macOS/Linux), plus Android, iOS, and Browser (WASM). |
| [`Majorsilence.Forms.Uno`](https://www.nuget.org/packages/Majorsilence.Forms.Uno) | Uno Platform backend — desktop, mobile, WebAssembly. |
| [`Majorsilence.Forms.Headless`](https://www.nuget.org/packages/Majorsilence.Forms.Headless) | Offscreen rendering for tests and CI. No display required. |
| [`Majorsilence.Forms.Drawing.Common`](https://www.nuget.org/packages/Majorsilence.Forms.Drawing.Common) | Skia-backed replacement for the Windows-only `System.Drawing.Common` (GDI+). Referenced automatically. |
| [`Majorsilence.Forms.Telerik`](https://www.nuget.org/packages/Majorsilence.Forms.Telerik) | Telerik UI for WinForms compatibility surface, for apps migrating off it. |

## Migrating an existing WinForms app

The repo ships a `majorsilence-migrate` CLI that rewrites a whole solution — project files,
namespaces, and `System.Drawing` usage — onto Majorsilence.Forms, and reports what needs a human.
It can also convert incrementally with `--dual-build`, leaving the app buildable against real
WinForms until you're ready to switch.

- [Migration guide](https://github.com/majorsilence/Majorsilence.Forms/blob/main/MIGRATION.md)
- [Compatibility matrix](https://github.com/majorsilence/Majorsilence.Forms/blob/main/COMPATIBILITY_MATRIX.md) — what's implemented, approximated, or out of scope

## Links

- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Getting started](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/getting-started.md)
- [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md)
- [Samples](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples) — control gallery, an Explorer clone, an Outlook clone

Licensed under the MIT License.
