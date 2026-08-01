# Majorsilence.Forms.Uno

An [Uno Platform](https://platform.uno) (Skia) **platform backend** for
[Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms) — the alternative to the
default Avalonia backend, for the broadest reach: desktop, iOS, Android, and WebAssembly.

Majorsilence.Forms draws all of its own controls with SkiaSharp; this package hosts them, presenting
the Skia surface through Uno's `SKXamlCanvas` and delivering input back.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Uno
```

Built against `Uno.WinUI` + `SkiaSharp.Views.Uno.WinUI`. Targets `net10.0`.

## Usage

Unlike the desktop Avalonia backend, this one runs inside an **Uno application head** — an Uno app
project that boots the Uno platform, with Majorsilence.Forms hosted inside it. Select the backend
before the first window is created:

```csharp
using Majorsilence.Forms.Uno;

Majorsilence.Forms.Backends.Platform.Backend = new UnoPlatformBackend ();
```

The [`Gallery.Uno` sample](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples/Gallery.Uno)
is a complete, working head — start from it rather than wiring one by hand.

Majorsilence.Forms content can also be embedded *inside* an existing Uno app's visual tree instead of
owning the window. See [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md).

## Touch and gestures

At parity with the Avalonia backend: multi-touch pinch/zoom/rotate, swipe, flick/momentum scrolling,
and long-press are surfaced through the same neutral `Control.Pinch` / `Swipe` / `ScrollGesture` /
`LongPress` events, so gesture-aware code is portable between backends.

Two behaviors are specific to this backend, because WinUI's gesture model differs from Avalonia's:

- **Swipe is synthesized**, not a platform gesture — WinUI has no swipe gesture on `UIElement`, so it
  is derived from manipulation velocity against a threshold.
- **Mouse input is filtered here, not by the platform.** WinUI's manipulation engine tracks *any*
  pointer type, so this package excludes mouse explicitly to keep ordinary desktop drags, scrollbars,
  and drag-select behaving normally.

## Links

- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md)
- [`Gallery.Uno` sample](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples/Gallery.Uno)
- [Getting started](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/getting-started.md)

Licensed under the MIT License.
