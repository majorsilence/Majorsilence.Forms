# Majorsilence.Forms.Avalonia

The **default platform backend** for [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms),
built on [Avalonia](https://avaloniaui.net) 12.

Majorsilence.Forms draws all of its own controls with SkiaSharp; this package is the *host* underneath —
it creates native windows, runs the message loop, delivers input, and presents the Skia surface. It is
what makes a Majorsilence.Forms app actually open a window.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Avalonia
```

That's the entire setup. The backend is discovered automatically when this package is referenced, so
there is nothing to configure:

```csharp
using Majorsilence.Forms;

internal static class Program
{
    [STAThread]
    private static void Main () => Application.Run (new MainForm ());
}
```

## Platforms

| Target | Notes |
|---|---|
| Windows · macOS · Linux | The default. Native windowing, plus a real native-engine `WebView` control. |
| Browser (WebAssembly) | Ships as a `net10.0-browser` target, selected automatically by a browser-wasm head project. |
| Android | Opt-in `net10.0-android` target. Requires the `android` workload. Early — builds and boots, but less exercised than desktop. |
| iOS | Opt-in `net10.0-ios` target. Requires a Mac with the `ios` workload. Unverified beyond CI compilation — expect a first-build shakeout. |

Android and iOS are extra target rows rather than unconditional ones, because those workloads are
required just to *compile* against them. A normal desktop `dotnet build` never needs either workload.

Because Avalonia ships its own Android, iOS, and Browser targets, this backend is a path to mobile and
web on its own — not only a desktop backend. [`Majorsilence.Forms.Uno`](https://www.nuget.org/packages/Majorsilence.Forms.Uno)
is the alternative route.

## Touch and gestures

Multi-touch pinch/zoom/rotate, swipe, flick/momentum scrolling, and long-press are wired through this
backend to the neutral `Control.Pinch` / `Swipe` / `ScrollGesture` / `LongPress` events. Long-press
opens a `ContextMenuStrip` where one is set, and a drag over a scrollable panel pans it. Mouse input is
unaffected — the underlying Avalonia gesture recognizers exclude it by construction.

## Embedding in an existing Avalonia app

The reverse direction is supported too: an existing Avalonia application can host Majorsilence.Forms
content inside its own visual tree, rather than letting Majorsilence.Forms own the top-level window.
See [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md).

## Links

- [**Documentation**](https://forms.majorsilence.com) · [Platform backends](https://forms.majorsilence.com/backends/)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Getting started](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/getting-started.md)
- [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md) — the backend seam, and how to write your own
- [Samples](https://github.com/majorsilence/Majorsilence.Forms/tree/main/samples) — `Gallery.Avalonia`, `Gallery.Wasm`, `Gallery.Android`, `Gallery.iOS`

Licensed under the MIT License.
