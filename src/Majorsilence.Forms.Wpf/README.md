# Majorsilence.Forms.Wpf

The **WPF (`System.Windows`) platform backend** for
[Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms) — run a cross-platform
WinForms-style app on a real WPF `Window`, or embed Majorsilence.Forms controls inside an existing
WPF application **one control at a time**.

Majorsilence.Forms draws all of its own controls with SkiaSharp; this package is the *host*
underneath — a real WPF `Window`, the WPF `Dispatcher` message loop, the `Microsoft.Win32` file
dialogs and `System.Windows.Clipboard` — with the Skia surface presented through a `WriteableBitmap`.
It is the WPF counterpart of `Majorsilence.Forms.Avalonia`, `Majorsilence.Forms.Uno` and
`Majorsilence.Forms.WinForms`, including their embedding direction (`MajorsilenceFormsPresenter`,
`ToWpfElement()`, `ToWpfWindow()`).

**Windows-only** at runtime, like WPF itself.

**Target frameworks:** `net48` (classic .NET Framework 4.8 — pairs with the `netstandard2.0` build of
`Majorsilence.Forms`), plus `net8.0-windows` and `net10.0-windows` for modern .NET.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Wpf
```

```csharp
// Program.cs — Majorsilence.Forms owns the app
[STAThread]
static void Main ()
{
    Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.Wpf.WpfPlatformBackend ();
    Majorsilence.Forms.Application.Run (new MainForm ());
}
```

Or embed in an existing WPF app:

```csharp
myWpfGrid.Children.Add (myMfControl.ToWpfElement ());
// or set MajorsilenceFormsPresenter.Content directly
```

## Why: migration in steps

- **A WPF app** can keep its shell and windows on WPF while individual screens move to
  Majorsilence.Forms — each ported piece drops back in as a standard WPF `FrameworkElement`.
- When everything is ported, swap this package for `Majorsilence.Forms.Avalonia` or
  `Majorsilence.Forms.Uno` and the same code goes cross-platform. Nothing above the backend seam
  changes.
