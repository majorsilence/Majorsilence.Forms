# Majorsilence.Forms

**Take your WinForms apps cross-platform — without rewriting them.**

Majorsilence.Forms is a WinForms-style UI framework that lets you move legacy *and* modern
WinForms applications onto a modern, cross-platform stack. You keep the programming model you
already know — `Form`s, controls, event handlers, even the `*.Designer.cs` files — and gain
Windows, macOS, and Linux out of the box, with mobile and web within reach through
[Uno Platform](https://platform.uno).

> ⚠️ **Early stage.** The API is stabilizing and not every WinForms corner is covered yet.
> Great for new cross-platform LOB apps and for migrating real apps today — just pin your version.

## Why Majorsilence.Forms?

Migrating a WinForms app usually means a ground-up rewrite in a new UI paradigm (XAML, MVVM, the
web). That's expensive, risky, and throws away years of working business logic and UX.

Majorsilence.Forms is built to **collapse that migration**. It mirrors the WinForms API surface and
ships a compatibility layer so your existing forms, controls, and code move over with far less
churn — then runs everywhere on top of best-in-class hosts:

- **Reuse, don't rewrite.** The same control model and event-driven code you wrote in WinForms.
  No XAML, no forced MVVM rewrite, no relearning the framework.
- **Cross-platform by construction.** Everything is drawn with [SkiaSharp](https://github.com/mono/SkiaSharp)
  and runs on a swappable host backend — [Avalonia](https://avaloniaui.net) by default,
  [Uno Platform](https://platform.uno) for the broadest reach (desktop, mobile, WebAssembly).
- **Bring your skills, your team, your code.** WinForms muscle memory transfers directly, so the
  ramp-up cost for an existing .NET shop is close to zero.
- **Modern under the hood.** GPU-accelerated Skia rendering, HiDPI, current .NET — a clean
  foundation, not a museum piece.

If you're moving a WinForms codebase off the Windows-only desktop and want to keep momentum
instead of starting over, this framework is for you.

## How it works

```
        Your app  (Forms, controls, Designer files — the WinForms model you know)
            │
       Majorsilence.Forms  (controls + WinForms-compatible API, drawn with SkiaSharp)
            │
   Swappable host backend
   ├─ Avalonia   → Windows · macOS · Linux            (default)
   ├─ Uno         → desktop · iOS · Android · WebAssembly
   └─ Headless    → offscreen rendering for tests / CI
```

Majorsilence.Forms owns the controls and rendering; the backend only puts pixels on screen and
delivers input. That seam is what lets the same app target Avalonia today and Uno tomorrow.
See [Platform backends](docs/backends.md) for the details and how to add your own.

`Majorsilence.Forms.Drawing` provides a Skia-backed, cross-platform replacement for the Windows-only
`System.Drawing.Common` (GDI+) APIs, so drawing code migrates too.

## Migrating an existing app

- [`MIGRATION.md`](MIGRATION.md) — how the `majorsilence-migrate` CLI tool rewrites a WinForms
  solution onto Majorsilence.Forms, and how to read its output.
- [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md) — what's fully implemented, what's
  approximated, and what's deliberately out of scope, once your code compiles.

## Getting started

See [Getting Started](docs/getting-started.md) to scaffold your first Majorsilence.Forms app.

A form looks exactly like you'd expect:

- [`samples/Explorer/MainForm.cs`](samples/Explorer/MainForm.cs)
- [`samples/Explorer/MainForm.Designer.cs`](samples/Explorer/MainForm.Designer.cs)

## Samples

Explore real apps built with Majorsilence.Forms in the [`samples/`](samples) folder:

- [`ControlGallery`](samples/ControlGallery) — every built-in control, live.
- [`Gallery.Uno`](samples/Gallery.Uno) — the control gallery running on the **Uno** backend.
- [`Explorer`](samples/Explorer) — a Windows Explorer clone.
- [`Outlaw`](samples/Outlaw) — a Microsoft Outlook clone.
- [`WinFormsInterop`](samples/WinFormsInterop) — bi-directional WinForms ↔ Majorsilence.Forms interop (Windows-only). See [WinForms Interop](docs/winforms-interop.md).

Run the gallery on the default (Avalonia) backend:

```bash
dotnet run --project samples/ControlGallery
```

Or on the Uno backend:

```bash
dotnet run --project samples/Gallery.Uno
```

For build and run details, see [Samples](docs/samples.md).

## License

See [license.md](license.md).
