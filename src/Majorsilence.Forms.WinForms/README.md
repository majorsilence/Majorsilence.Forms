# Majorsilence.Forms.WinForms

The **System.Windows.Forms platform backend** for
[Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms) — and the piece that lets a
WinForms application (or a WinForms control library's consumers) adopt Majorsilence.Forms **one
control at a time**.

Majorsilence.Forms draws all of its own controls with SkiaSharp; this package is the *host*
underneath — real `System.Windows.Forms` windows, the classic Win32 message pump, native WinForms
file dialogs and clipboard — with the Skia surface presented through a GDI-backed control. It is the
WinForms counterpart of `Majorsilence.Forms.Avalonia` and `Majorsilence.Forms.Uno`, including their
embedding direction (`MajorsilenceFormsPresenter`, `ToWinFormsControl()`, `ToWinFormsForm()`).

Windows-only by definition — `System.Windows.Forms` does not exist on macOS or Linux. Off Windows it
builds as an empty placeholder assembly so a cross-platform solution still compiles everywhere.

## Why: migration in steps, in both directions

- **A WinForms app** can keep its shell, menus and forms on real WinForms while individual screens
  or controls move to Majorsilence.Forms — each ported piece drops back in as a standard
  `System.Windows.Forms.Control`.
- **A WinForms control library** can port its internals to Majorsilence.Forms while still shipping
  WinForms controls to its consumers, so the library migrates without forcing its consumers to.
- When everything is ported, swap this package for `Majorsilence.Forms.Avalonia` or
  `Majorsilence.Forms.Uno` and the same code goes cross-platform. Nothing above the backend seam
  changes.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.WinForms
```

## Embed Majorsilence.Forms controls in an existing WinForms app

Drop a presenter into any WinForms form or container — the backend installs itself automatically,
and the app's existing `Application.Run` services everything:

```csharp
using Majorsilence.Forms.WinForms;

// namespace-qualified: your app has both System.Windows.Forms and Majorsilence.Forms in scope
var scene = new Majorsilence.Forms.Panel ();
scene.Controls.Add (new Majorsilence.Forms.Button { Text = "Ported button", Left = 12, Top = 12 });

System.Windows.Forms.Control host = scene.ToWinFormsControl ();   // or: new MajorsilenceFormsPresenter { Content = scene }
myWinFormsForm.Controls.Add (host);
```

Popups opened by the embedded content (combo dropdowns, menus, tooltips) are real borderless OS
windows, and `NativeControlHost` works in the other direction — a real WinForms control hosted
*inside* the Majorsilence.Forms scene.

## Use a Majorsilence.Forms Form as a WinForms form

A Form's backend window under this backend *is* a `System.Windows.Forms.Form`; take it and own it:

```csharp
Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.WinForms.WinFormsPlatformBackend ();

var form = new Majorsilence.Forms.Form { Text = "Ported dialog" };
System.Windows.Forms.Form native = form.ToWinFormsForm ();
native.ShowDialog (ownerWinFormsForm);   // real native-modal relationship
```

## Run a whole Majorsilence.Forms app on WinForms

```csharp
internal static class Program
{
    [STAThread]
    private static void Main ()
    {
        Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.WinForms.WinFormsPlatformBackend ();
        Majorsilence.Forms.Application.Run (new MainForm ());
    }
}
```

## Relationship to Majorsilence.Forms.WindowsFormsInterop

Both exist for incremental migration; they solve different layers of it:

| | `WindowsFormsInterop` | `Majorsilence.Forms.WinForms` (this package) |
|---|---|---|
| Granularity | Whole forms/windows | Individual controls (plus forms) |
| MF runs on | The Avalonia backend, sharing the Win32 pump | Real WinForms windows — no Avalonia involved |
| Best for | Opening legacy WinForms forms from an MF app, and vice versa | Embedding MF controls inside WinForms UI; WinForms libraries porting internals first |

They can coexist; the presenter leaves an already-configured backend alone.

## Links

- [Migration guide](https://forms.majorsilence.com/migration/)
- [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md)
- [Documentation](https://forms.majorsilence.com) · [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- Sample: [`samples/EmbeddingWinForms`](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples/EmbeddingWinForms)

Licensed under the MIT License.
