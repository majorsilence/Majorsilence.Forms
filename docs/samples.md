## Majorsilence.Forms Samples

Every sample lives under `samples/`. Unless noted otherwise, each one runs with:

```bash
dotnet run --project samples/<Name>
```

You need the .NET 10 SDK (<https://dotnet.microsoft.com/download>); the repo targets `net10.0` via
`_TargetFramework` in `Directory.Build.props`. In an IDE, open `Majorsilence.Forms.slnx`, set the
sample as the startup project, and launch it.

`Gallery.Android` and `Gallery.iOS` are deliberately left out of `Majorsilence.Forms.slnx`, so a
plain `dotnet build` / `dotnet test` at the repo root never needs their platform workload.

| Sample | What it shows | Platforms |
|---|---|---|
| [`ControlGallery`](#controlgallery) | Every built-in control (a library — see the heads below) | — |
| [`Gallery.Avalonia`](#galleryavalonia) | The gallery on the default backend, incl. headless rendering | Windows, macOS, Linux |
| [`Gallery.Uno`](#galleryuno) | The same gallery on the Uno backend | Desktop (verified on macOS) |
| [`Gallery.Wasm`](#gallerywasm) | The same gallery in the browser | WebAssembly |
| [`Gallery.Android`](#galleryandroid-android-only-work-in-progress) | The same gallery on Android | Android |
| [`Gallery.iOS`](#galleryios-ios-only-unverified) | The same gallery on iOS | iOS |
| [`Explorer`](#explore) | A Windows Explorer clone | Windows, macOS, Linux |
| [`Outlaw`](#outlaw) | An Outlook clone | Windows, macOS, Linux |
| [`PointOfSale`](#pointofsale) | A full client/server LOB app | Windows, macOS, Linux |
| [`EmbeddingAvalonia` / `EmbeddingUno`](#embeddingavalonia--embeddinguno) | Majorsilence.Forms hosted *inside* a native app | Desktop |
| [`WinFormsInterop`](#winformsinterop-windows-only) | Bi-directional `System.Windows.Forms` interop | Windows |

### ControlGallery

`ControlGallery` shows off the various controls and features currently available in
`Majorsilence.Forms` — one demo panel per control.

It is a **backend-agnostic library**, not a runnable app: it holds the shared `MainForm` and demo
panels and references no backend, so it can be hosted by each backend's own app head without
dragging the other backends' dependencies into the process. Run one of the `Gallery.*` heads below.

![Windows ControlGallery Screenshot](controlgallery-windows.png "Windows ControlGallery Screenshot")

### Gallery.Avalonia

The desktop head for `ControlGallery`, on the default (Avalonia) backend:

```bash
dotnet run --project samples/Gallery.Avalonia
```

It can also render the gallery offscreen through the Headless backend, which is what CI and
pixel-diff checks use:

```bash
dotnet run --project samples/Gallery.Avalonia -- --render-headless out.png 1100 750 --select-row 0
```

### Gallery.Uno

The same `MainForm`, hosted by an Uno app head on the Uno backend — see
[`backends.md`](backends.md) for how the backend implements the seam.

```bash
dotnet run --project samples/Gallery.Uno
```

Needs a windowing session, so it is not part of the headless CI build. Its Uno packages restore from
nuget.org via the sample's own `nuget.config`, and it manages its own package versions independently
of the repo's central package management. Verified launching and rendering the full gallery on macOS.

### Gallery.Wasm

The same `MainForm` again, running in the browser on the Avalonia backend's `net10.0-browser` target.
Needs the wasm-tools workload once, then a publish:

```bash
dotnet workload install wasm-tools
dotnet publish samples/Gallery.Wasm -c Release -o out
```

Serve `out/wwwroot` with any static file server and open `index.html` — `dotnet run` does not serve a
WebAssembly SDK project. There is no real filesystem in the browser, so the gallery's images are
preloaded into the in-memory filesystem via `WasmFilesToIncludeInFileSystem` rather than special-casing
`ImageLoader`.

There's no separate WASM package: `Majorsilence.Forms.Avalonia` multi-targets `net10.0-browser`
alongside its desktop TFMs, and startup is async and host-driven rather than a blocking
`Application.Run` (`samples/Gallery.Wasm/Program.cs` is a one-liner over
`Application.RunBrowserAsync`). This is a young path, not part of the headless CI build — see
[Single-view platforms](backends.md#single-view-platforms-browser-android-ios) for how it works and
what doesn't work there (no WebView, no window chrome, popup-dismissal gap).

### Gallery.Android (Android-only, work in progress)

> ⚠️ Android/mobile support is early: it builds and boots the gallery, but hasn't seen the same real-device
> testing or control coverage as the desktop/browser backends. Expect rough edges.

Runs the same `ControlGallery` `MainForm` on the Avalonia backend's Android target, hosted by a single
Activity. Requires the `android` workload:

```bash
dotnet workload install android
dotnet build samples/Gallery.Android -p:EnableAndroidTarget=true -t:Run
```

Not in the solution — see the comment at the top of `samples/Gallery.Android/Gallery.Android.csproj`
for why, and `samples/WinFormsInterop` for the same pattern applied to a different platform-specific
sample. Android shares the browser's
[single-view host](backends.md#single-view-platforms-browser-android-ios), so the same window-chrome
and WebView limitations apply.

### Gallery.iOS (iOS-only, unverified)

> ⚠️ Unlike every other sample in this repo, this one has never actually been compiled: the `ios`
> workload only installs on macOS at all (there is no Linux/Windows path, unlike `android`), and no
> Mac was available to build it with. It's written from Avalonia.iOS's decompiled API and standard
> .NET-for-iOS conventions, not from a working build — expect a first-build shakeout on real hardware
> or in CI's `ios` job before treating it as working.

Runs the same `ControlGallery` `MainForm` on the Avalonia backend's iOS target, hosted by a single
`UIViewController`. Requires a Mac with the `ios` workload:

```bash
dotnet workload install ios
dotnet build samples/Gallery.iOS -p:EnableIOSTarget=true -t:Run
```

Not in the solution, for the same reason as `Gallery.Android` — see the comment at the top of
`samples/Gallery.iOS/Gallery.iOS.csproj`. iOS shares the same
[single-view host](backends.md#single-view-platforms-browser-android-ios) as browser and Android.

### Explore

`Explore` is a clone of Windows' Explorer application, exercising file browsing, tree navigation, and
list views end to end. The project lives in `samples/Explorer` and is named `Explore.csproj`.

```bash
dotnet run --project samples/Explorer
```

Verified running on Windows, Ubuntu, and macOS.

#### Windows

![Windows Explore Screenshot](explorer-windows.png "Windows Explore Screenshot")

#### Ubuntu AMD64

![Ubuntu Explore Screenshot](explorer-ubuntu.png "Ubuntu Explore Screenshot")

#### macOS

![Mac Explore Screenshot](explorer-osx.png "Mac Explore Screenshot")

### Outlaw

`Outlaw` is a clone of Microsoft's Outlook, showing off how `Majorsilence.Forms` can be used to create a complex modern application.

```bash
dotnet run --project samples/Outlaw
```

![Windows Outlaw Screenshot](outlaw-windows.png "Windows Outlaw Screenshot")

### PointOfSale

A complete line-of-business application split across four projects, rather than a single-window demo
— the shape a real Majorsilence.Forms app takes:

| Project | Role |
|---|---|
| `PointOfSale.Client` | The Majorsilence.Forms desktop app (forms, panels, custom controls, services) |
| `PointOfSale.Api` | An ASP.NET Core minimal API with JWT auth and role-based policies |
| `PointOfSale.Contracts` | DTOs shared by both sides |
| `PointOfSale.Data` | EF Core + SQLite persistence and seeding (covered by `tests/PointOfSale.Data.Tests`) |

Start the API first, then the client — the client reads `ApiBaseUrl` (and its kiosk-mode settings)
from its own `appsettings.json`, defaulting to `http://127.0.0.1:5000`:

```bash
dotnet run --project samples/PointOfSale/PointOfSale.Api
dotnet run --project samples/PointOfSale/PointOfSale.Client
```

The API creates and seeds a local `pos.db` on first run. The default JWT signing key in
`appsettings.json` is a placeholder, not a secret — override it in `appsettings.Development.json` or
the environment.

### EmbeddingAvalonia / EmbeddingUno

The reverse hosting direction: an ordinary Avalonia or Uno application that uses Majorsilence.Forms
objects as if they were its own native ones.

```bash
dotnet run --project samples/EmbeddingAvalonia
dotnet run --project samples/EmbeddingUno
```

Each window puts native host controls and an embedded Majorsilence.Forms scene side by side, and
demonstrates all three seams:

- `ToAvaloniaControl()` / `ToUnoControl()` — a Majorsilence control hosted as a native one via
  `MajorsilenceFormsPresenter`.
- `ToAvaloniaWindow()` / `ToUnoWindow()` — a Majorsilence `Form`'s backend window handed back to the
  host. Avalonia gets a genuine OS-level modal dialog ("Open as Avalonia dialog"); Uno has no owner
  concept in this backend, so it gets an independent top-level window ("Open as Uno window") and
  `Form.ShowDialog(parent)` is the way to get modal behaviour there.
- `NativeControlHost` — a native button hosted *inside* the Majorsilence scene, the other direction
  again. See [`native-interop.md`](native-interop.md).

Both also toggle the host theme, so you can watch Majorsilence.Forms controls follow it.

See [Embedding in a host app](backends.md#embedding-in-a-host-app) for the API details.

### WinFormsInterop (Windows-only)

Demonstrates bi-directional interop between `System.Windows.Forms` and Majorsilence.Forms in a
single process. The sample starts as a real WinForms host (Direction B: WF → MF) and each
opened Majorsilence window can in turn open legacy WinForms forms (Direction A: MF → WF).

See [WinForms Interop](winforms-interop.md) for full API documentation.

```bash
dotnet run --project samples/WinFormsInterop
```
