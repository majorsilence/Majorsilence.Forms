## Majorsilence.Forms Samples

Every sample lives under `samples/`. Unless noted otherwise, each one runs with:

```bash
dotnet run --project samples/<Name>
```

You need the .NET 10 SDK (<https://dotnet.microsoft.com/download>); the repo targets `net10.0` via
`_TargetFramework` in `Directory.Build.props`. In an IDE, open `Majorsilence.Forms.slnx`, set the
sample as the startup project, and launch it.

`Gallery.Android` and `Gallery.iOS` are in `Majorsilence.Forms.slnx`, but compile as empty stub
libraries unless the matching workload is installed: `Directory.Build.props` sets
`EnableMobileHeads=true` when it detects the `android` workload on disk (Visual Studio's ".NET
Multi-platform App UI development" install does this), and only then do they build as real
`net10.0-android` / `net10.0-ios` apps. So a plain `dotnet build` / `dotnet test` at the repo root
still works without any platform workload. Force it with `-p:EnableMobileHeads=true`.

`EnableMobileHeads` is an umbrella, not a switch: it resolves into `EnableAndroidHead` (which also
requires the android workload to be present) and `EnableIOSHead` (which also requires macOS), and the
projects key on those. To ask for exactly one platform — which is what you want on a machine or CI
runner that has only one of the two workloads — pass `-p:EnableAndroidTarget=true` or
`-p:EnableIOSTarget=true` instead.

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
| [`EmbeddingAvalonia` / `EmbeddingUno` / `EmbeddingWinForms`](#embeddingavalonia--embeddinguno--embeddingwinforms) | Majorsilence.Forms hosted *inside* a native app | Desktop (WinForms: Windows only) |
| [`WinFormsInterop`](#winformsinterop-windows-only) | Bi-directional `System.Windows.Forms` interop | Windows |
| [`WinFormsCompatDemo`](#winformscompatdemo) | Source-generated `System.Windows.Forms` namespace, no real WinForms assembly | Windows, macOS, Linux |
| [`AutomationTarget`](#automationtarget) | An app that exposes its own automation endpoint | Windows, macOS, Linux |

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
WebAssembly SDK project.

CI publishes this bundle on every PR (the `wasm` job in `.github/workflows/dotnet.yml`, artifact
`gallery-wasm`) and attaches it to each GitHub Release, so you can download and serve it without a
toolchain. The live copy at [forms.majorsilence.com/gallery](https://forms.majorsilence.com/gallery/)
is built from a separate repo.

> **Known gap: the gallery's icons don't render in the browser.** There is no real filesystem in the
> browser, and the `WasmFilesToIncludeInFileSystem` item in `Gallery.Wasm.csproj` that is meant to
> preload the images into the in-memory one has no effect here: it is only consumed by the
> `_WasmGenerateAppBundle` target, which is gated on `$(WasmGenerateAppBundle) == 'true'` and so never
> runs under `Microsoft.NET.Sdk.WebAssembly` (this project evaluates that property to `false`). The
> item is populated and then silently ignored — no warning. `ImageLoader.Get` therefore misses every
> file, and `Bitmap(string)` degrades to a 1×1 placeholder rather than throwing, so the app boots
> cleanly with invisible icons. The same applies to `Gallery.Android` and `Gallery.iOS`. Making the
> images `EmbeddedResource`s of `ControlGallery` would fix all three at once.

There's no separate WASM package: `Majorsilence.Forms.Avalonia` multi-targets `net10.0-browser`
alongside its desktop TFMs, and startup is async and host-driven rather than a blocking
`Application.Run` (`samples/Gallery.Wasm/Program.cs` is a one-liner over
`Application.RunBrowserAsync`). This is a young path, not part of the headless CI build — see
[Single-view platforms](backends.md#single-view-platforms-browser-android-ios) for how it works and
what doesn't work there (no WebView, no window chrome, popup-dismissal gap).

### Gallery.Android (Android-only, work in progress)

> ⚠️ Android/mobile support is early. It has had an initial real-device pass — the gallery boots (an
> AppCompat-theme startup crash was found and fixed there), and tap hit-testing, render scaling and
> touch scroll/flick are confirmed working — but the on-screen keyboard, safe-area insets, rotation
> and full control coverage have not had the same testing as the desktop/browser backends. Expect
> rough edges.

Runs the same `ControlGallery` `MainForm` on the Avalonia backend's Android target, hosted by a single
Activity. Requires the `android` workload:

```bash
dotnet workload install android
dotnet build samples/Gallery.Android -t:Run
```

Once the `android` workload is installed, `Directory.Build.props` detects it and switches this
project from its stub build to the real `net10.0-android` head automatically (so Visual Studio just
works — set it as the startup project and press F5 against an emulator). Pass
`-p:EnableAndroidTarget=true` to force it, e.g. on a machine where the auto-detection misfires; that
one is ungated, so a missing workload fails the build rather than silently reverting to the stub.

CI publishes a sideloadable APK + AAB on every PR (the `android` job in
`.github/workflows/dotnet.yml`, artifact `gallery-android`) and attaches them to each GitHub Release.
They are signed with the .NET Android debug key — fine for sideloading, not for the Play Store.

In the solution but stubbed out without the workload — see the comment at the top of
`samples/Gallery.Android/Gallery.Android.csproj` for how. Android shares the browser's
[single-view host](backends.md#single-view-platforms-browser-android-ios), so the same window-chrome
and WebView limitations apply.

### Gallery.iOS (iOS-only, unverified)

> ⚠️ iOS is the least-proven backend: it was written from Avalonia.iOS's decompiled API and standard
> .NET-for-iOS conventions. CI's `ios` job (on `macos-latest`) compiles the real head and launches it
> in a simulator as a smoke check — both green since the android/ios flag split — but the job is still
> `continue-on-error` (only a couple of green runs on a flaky runner), and nobody has run it
> interactively on a device. Treat rough edges as expected, not as a regression.

Runs the same `ControlGallery` `MainForm` on the Avalonia backend's iOS target, hosted by a single
`UIViewController`. Requires a Mac with the `ios` workload:

```bash
dotnet workload install ios
dotnet build samples/Gallery.iOS -t:Run
```

As with `Gallery.Android`, `Directory.Build.props` switches this from a stub to the real
`net10.0-ios` head automatically once a mobile workload is present — but only on macOS, since the
`ios` workload exists nowhere else. Force it with `-p:EnableIOSTarget=true`; prefer that over the
`EnableMobileHeads` umbrella, which on a Mac without the `android` workload would also ask for the
`net10.0-android` row and fail.

CI publishes a zipped iOS-simulator `.app` on every PR when the build succeeds (the `ios` job,
artifact `gallery-ios`) and attaches it to each GitHub Release. There is no signed device `.ipa` —
that needs an Apple distribution certificate.

In the solution but stubbed out except on macOS with the workload, for the same reason as
`Gallery.Android` — see the comment at the top of `samples/Gallery.iOS/Gallery.iOS.csproj`. iOS shares the same
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

### AutomationTarget

A deliberately small app that starts a `WebDriverServer` on itself, so there is something real to drive
while learning the automation tooling — from the [MCP server](../tools/Majorsilence.Forms.Mcp), a Selenium
client, or plain `curl`.

```bash
dotnet run --project samples/AutomationTarget -- --webdriver 4444
```

It prints the endpoint and the commands to drive it. `--webdriver <port>` picks the port (default 4444);
`--no-webdriver` runs it as an ordinary app. Each control demonstrates one thing a client has to cope
with — a control that refuses to be clicked, one that only becomes enabled once a checkbox is ticked, and
one deliberately left unnamed — and every action is logged on screen and to stdout, so you can check a
client's claims against what the app actually saw.

See [`samples/AutomationTarget/README.md`](../samples/AutomationTarget/README.md) for the control-by-control
breakdown, and [Automation & UI testing](automation.md) for the tooling itself.

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

### EmbeddingAvalonia / EmbeddingUno / EmbeddingWinForms

The reverse hosting direction: an ordinary Avalonia, Uno, or classic WinForms application that uses
Majorsilence.Forms objects as if they were its own native ones.

```bash
dotnet run --project samples/EmbeddingAvalonia
dotnet run --project samples/EmbeddingUno
dotnet run --project samples/EmbeddingWinForms   # Windows only
```

Each window puts native host controls and an embedded Majorsilence.Forms scene side by side, and
demonstrates all three seams:

- `ToAvaloniaControl()` / `ToUnoControl()` / `ToWinFormsControl()` — a Majorsilence control hosted
  as a native one via `MajorsilenceFormsPresenter`.
- `ToAvaloniaWindow()` / `ToUnoWindow()` / `ToWinFormsForm()` — a Majorsilence `Form`'s backend
  window handed back to the host. Avalonia and WinForms get a genuine OS-level modal dialog ("Open
  as Avalonia dialog" / "Open as WinForms dialog"); Uno has no owner concept in this backend, so it
  gets an independent top-level window ("Open as Uno window") and `Form.ShowDialog(parent)` is the
  way to get modal behaviour there.
- `NativeControlHost` — a native button hosted *inside* the Majorsilence scene, the other direction
  again. See [`native-interop.md`](native-interop.md).

The Avalonia and Uno ones also toggle the host theme, so you can watch Majorsilence.Forms controls
follow it. The WinForms one is Windows-only and exists as the port-one-control-at-a-time migration
path — see [The WinForms backend](backends.md#the-winforms-backend).

See [Embedding in a host app](backends.md#embedding-in-a-host-app) for the API details.

### WinFormsInterop (Windows-only)

Demonstrates bi-directional interop between `System.Windows.Forms` and Majorsilence.Forms in a
single process. The sample starts as a real WinForms host (Direction B: WF → MF) and each
opened Majorsilence window can in turn open legacy WinForms forms (Direction A: MF → WF).

See [WinForms Interop](winforms-interop.md) for full API documentation.

```bash
dotnet run --project samples/WinFormsInterop
```

### WinFormsCompatDemo

Not to be confused with `WinFormsInterop` above: there is no real `System.Windows.Forms` assembly
anywhere in this process. `Form1.cs`/`Form1.Designer.cs` here are ordinary, unmodified WinForms
designer-generated source — a `Button`, `Label`, `TextBox`, a `MessageBox.Show(...)` call — that
compile against `Majorsilence.Forms` because the
[`Majorsilence.Forms.WinFormsShims.Compat`](../src/Majorsilence.Forms.WinFormsShims.Compat)
Roslyn source generator emits a same-named `System.Windows.Forms` namespace backed by it, purely at
compile time. This sample references the Avalonia backend so `Application.Run` opens a real window,
not just a compile check — see its own [`RESULTS.md`](../samples/WinFormsCompatDemo/RESULTS.md) for
what does and doesn't survive that translation today.

```bash
dotnet run --project samples/WinFormsCompatDemo
```
