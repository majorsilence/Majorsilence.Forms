# Majorsilence.Forms — cross-platform WinForms for .NET

**Take your WinForms apps cross-platform — without rewriting them.**

[![NuGet](https://img.shields.io/nuget/v/Majorsilence.Forms.svg?logo=nuget&label=Majorsilence.Forms)](https://www.nuget.org/packages/Majorsilence.Forms)
[![Downloads](https://img.shields.io/nuget/dt/Majorsilence.Forms.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/Majorsilence.Forms)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](license.md)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20macOS%20%7C%20Linux%20%7C%20Wasm-informational)](https://forms.majorsilence.com/backends/)

📖 **[Documentation](https://forms.majorsilence.com)** ·
🚀 **[Getting started](https://forms.majorsilence.com/getting-started/)** ·
🔀 **[Migrate an existing app](https://forms.majorsilence.com/migration/)** ·
🌐 **[Live browser demo](https://forms.majorsilence.com/gallery/)** ·
❓ **[FAQ](https://forms.majorsilence.com/faq/)**

Majorsilence.Forms is a **WinForms compatibility layer** — a WinForms-style UI framework that lets
you move legacy *and* modern Windows Forms applications onto a modern, cross-platform stack. You
keep the programming model you already know — `Form`s, controls, event handlers, even the
`*.Designer.cs` files — and gain Windows, macOS, and Linux out of the box, with mobile and web
within reach through [Uno Platform](https://platform.uno) or through
[Avalonia](https://avaloniaui.net)'s own Android, iOS, and Browser (WASM) targets.

> ⚠️ **Beta stage.** The API is stabilizing and not every WinForms corner is covered yet.
> Great for new cross-platform LOB apps and for migrating real apps today — just pin your version.

<details>
<summary><b>Looking for one specific thing?</b></summary>

| If you searched for… | Read this |
|---|---|
| cross-platform WinForms, WinForms compatibility library | [Cross-platform WinForms, explained](https://forms.majorsilence.com/cross-platform-winforms/) |
| run WinForms on Linux / Ubuntu, WinForms Mono replacement | [WinForms on Linux](https://forms.majorsilence.com/winforms-on-linux/) |
| run WinForms on macOS / Mac, WinForms Apple Silicon | [WinForms on macOS](https://forms.majorsilence.com/winforms-on-macos/) |
| WinForms alternative, MAUI vs Avalonia vs Uno | [WinForms alternatives compared](https://forms.majorsilence.com/winforms-alternatives/) |
| migrate/convert/modernize a WinForms app | [`MIGRATION.md`](MIGRATION.md) · [migration guide](https://forms.majorsilence.com/migration/) |
| is control *X* supported? | [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md) |
| WinForms UI testing, Selenium, headless CI | [Automation & UI testing](https://forms.majorsilence.com/automation/) |
| `System.Drawing.Common` / GDI+ replacement | [`Majorsilence.Forms.Drawing.Common`](https://www.nuget.org/packages/Majorsilence.Forms.Drawing.Common) |

</details>

## Why Majorsilence.Forms?

Migrating a WinForms app usually means a ground-up rewrite in a new UI paradigm (XAML, MVVM, the
web). That's expensive, risky, and throws away years of working business logic and UX.

Majorsilence.Forms is built to **collapse that migration**. It mirrors the WinForms API surface and
ships a compatibility layer so your existing forms, controls, and code move over with far less
churn — then runs everywhere on top of best-in-class hosts:

- **Reuse, don't rewrite.** The same control model and event-driven code you wrote in WinForms.
  No XAML, no forced MVVM rewrite, no relearning the framework.
- **Cross-platform by construction.** Everything is drawn with [SkiaSharp](https://github.com/mono/SkiaSharp)
  and runs on a swappable host backend — [Avalonia](https://avaloniaui.net) by default for desktop,
  with its own Android/iOS/Browser targets as one path to mobile and web, and
  [Uno Platform](https://platform.uno) as another, for the broadest reach (desktop, mobile, WebAssembly).
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
   ├─ Avalonia   → Windows · macOS · Linux  (default)  · also Android · iOS · Browser
   ├─ Uno         → desktop · iOS · Android · WebAssembly
   ├─ WinForms    → Windows-only migration bridge: embed in an existing WinForms app, port in steps
   └─ Headless    → offscreen rendering for tests / CI
```

Majorsilence.Forms owns the controls and rendering; the backend only puts pixels on screen and
delivers input. That seam is what lets the same app target Avalonia today and Uno tomorrow.
See [Platform backends](docs/backends.md) for the details and how to add your own.

`Majorsilence.Forms.Drawing` provides a Skia-backed, cross-platform replacement for the Windows-only
`System.Drawing.Common` (GDI+) APIs, so drawing code migrates too.

## Migrating an existing app

- [`MIGRATION.md`](MIGRATION.md) — how the `majorsilence-migrate` CLI tool rewrites a WinForms
  solution onto Majorsilence.Forms, and how to read its output. Install it with
  `dotnet tool install -g Majorsilence.Forms.Migrator`.
- [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md) — what's fully implemented, what's
  approximated, and what's deliberately out of scope, once your code compiles.

## Getting started

The full documentation site is at **[forms.majorsilence.com](https://forms.majorsilence.com)** —
including a [training guide](https://forms.majorsilence.com/training/) for teams (every example in
both C# and VB.NET) and the [control gallery running live in your browser](https://forms.majorsilence.com/gallery/).

```
dotnet new install Majorsilence.Forms.Templates
dotnet new majorsilenceforms
dotnet run --project MajorsilenceFormsApp
```

See [Getting Started](docs/getting-started.md) to scaffold your first Majorsilence.Forms app, and
[Automation & UI testing](docs/automation.md) to drive it from tests — headlessly in CI, from Selenium
or FlaUI, through Windows UI Automation and a screen reader, or from an AI assistant via the
[MCP server](tools/Majorsilence.Forms.Mcp) in `tools/`.

A form looks exactly like you'd expect:

- [`samples/Explorer/MainForm.cs`](samples/Explorer/MainForm.cs)
- [`samples/Explorer/MainForm.Designer.cs`](samples/Explorer/MainForm.Designer.cs)

## Samples

Explore real apps built with Majorsilence.Forms in the [`samples/`](samples) folder:

- [`ControlGallery`](samples/ControlGallery) — every built-in control, live. A backend-agnostic library (shared `MainForm`/panels) run by the `Gallery.Avalonia`, `Gallery.Uno`, `Gallery.Wasm`, `Gallery.Android` and `Gallery.iOS` heads below.
- [`Gallery.Avalonia`](samples/Gallery.Avalonia) — the control gallery running on the **Avalonia** backend.
- [`Gallery.Uno`](samples/Gallery.Uno) — the control gallery running on the **Uno** backend.
- [`Gallery.Wasm`](samples/Gallery.Wasm) — the control gallery running on **Avalonia in the browser** (WebAssembly).
- [`Gallery.Android`](samples/Gallery.Android) — the control gallery running on **Avalonia on Android**. Requires the `android` workload (`dotnet workload install android`); in the solution, but compiles as an empty stub until the workload is present (see that project's own comment). ⚠️ Work in progress: it has had an initial real-device pass (boots, taps, render scaling and touch scroll all confirmed), but keyboard, safe-area, rotation and full control coverage are not yet as exercised as the desktop/browser backends.
- [`Gallery.iOS`](samples/Gallery.iOS) — the control gallery running on **Avalonia on iOS**. Requires a Mac with the `ios` workload (`dotnet workload install ios`) — that workload doesn't install on Linux/Windows at all, so this can only be built on macOS or in the `ios` CI job; elsewhere it is an empty stub. ⚠️ Unverified: written from Avalonia.iOS's decompiled API and standard .NET-for-iOS conventions. CI now compiles the real head and launches it in a simulator smoke check, but nobody has run it interactively on a device — expect a shakeout.
- [`Explorer`](samples/Explorer) — a Windows Explorer clone.
- [`Outlaw`](samples/Outlaw) — a Microsoft Outlook clone.
- [`WinFormsInterop`](samples/WinFormsInterop) — bi-directional WinForms ↔ Majorsilence.Forms interop (Windows-only). See [WinForms Interop](docs/winforms-interop.md).
- [`EmbeddingWinForms`](samples/EmbeddingWinForms) — Majorsilence.Forms controls embedded inside a classic WinForms app via the **WinForms backend** (`MajorsilenceFormsPresenter`/`ToWinFormsControl()`), the port-one-control-at-a-time migration path (Windows-only). See [Platform backends](docs/backends.md).
- [`AutomationTarget`](samples/AutomationTarget) — a small app that exposes its own automation endpoint, so you have something real to drive from the [MCP server](tools/Majorsilence.Forms.Mcp), Selenium, or `curl`. See [Automation & UI testing](docs/automation.md).

Run the gallery on the Avalonia backend:

```bash
dotnet run --project samples/Gallery.Avalonia
```

Or on the Uno backend:

```bash
dotnet run --project samples/Gallery.Uno
```

For build and run details, see [Samples](docs/samples.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full guide — the four test configurations CI runs, the
baseline gates, the trimming/NativeAOT conventions, and how to tell a test that passes from a test that
proves something.

Contributions are welcome — fork the repo, push to a branch, and open a pull request against
`main`. AI-assisted changes are welcome too: whether you wrote the code by hand or with an AI
coding assistant, the bar is the same — it builds, the test suite passes, and it fits the existing
style and conventions. Note the diff was AI-assisted if a reviewer asking would be useful context,
but it isn't a separate approval track.

Before opening a PR:

- `dotnet build --configuration Release` and `dotnet test` should both be clean — this is what CI
  checks (see [`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)).
- New behavior should come with tests that prove it actually works, not just that a member exists
  or compiles.
- If you're closing a compatibility gap, check [`COMPATIBILITY_MATRIX.md`](COMPATIBILITY_MATRIX.md)
  first — it documents the stub policy (unimplemented members should no-op with a sensible default,
  never throw) and tracks what's real vs. approximated vs. deliberately out of scope, and should be
  updated alongside the code it describes.

For bugs and feature requests, open an issue on GitHub.

## License

See [license.md](license.md).

## 🏗 Project Origin & Evolution
This project is an AI-enhanced fork of Modern Forms (Original Repository: [https://github.com/modern-forms/Modern.Forms](https://github.com/modern-forms/Modern.Forms)) re-architected to bridge the gap between legacy WinForms and modern cross-platform frameworks. 

1. Base Foundation: Built upon the core architecture of Modern Forms, inheriting its initial cross-platform intent.  Full file-level attribution and original licensing terms are preserved within the source code and the LICENSE file.
2. AI-Driven Transformation: The codebase has been extensively refactored and expanded using Artificial Intelligence to achieve the following:
  - WinForms Compatibility: Filled critical API gaps to ensure near-parity with the standard Windows Forms ecosystem, allowing easier migration of legacy applications.
  - Multi-Platform Expansion: Implemented native support for WebAssembly (WASM), Android, and iOS, extending reach beyond desktop environments.
  - Host Rebasement: Successfully rebased the underlying OS hosts on Avalonia and Uno Platform, leveraging their robust rendering engines and platform interop layers for superior performance and stability.
3. Human Verification: All AI-generated adaptations, particularly the complex host rebasing and platform-specific implementations, have been manually reviewed, tested, and integrated to ensure architectural integrity.

## Migrated Project Examples

Majorsilence projects that are migrating to majorsilence.forms.

- https://github.com/majorsilence/MPlayercontrol
- https://github.com/majorsilence/Reporting/tree/feature/modernization-roadmap


A list of projects that have been forked to exercise majorsilence.forms winforms compatibility and migrator.

- https://github.com/majorsilence/AT-NetCore-NotepadPlusPlus 
- https://github.com/majorsilence/DarkUI
- https://github.com/majorsilence/PKHeX
- https://github.com/majorsilence/Calculator
- https://github.com/majorsilence/metroframework-modern-ui
- https://github.com/majorsilence/GymcimDesktopFormApp
- https://github.com/majorsilence/HealthCare-Plus
- https://github.com/majorsilence/WindowsUI
- https://github.com/majorsilence/ArdeshirV.Forms
- https://github.com/majorsilence/KaomojiKeyboard
- https://github.com/majorsilence/C-Flappy-Bird-Game-Windows-Form
- https://github.com/majorsilence/RibbonWinForms
- https://github.com/majorsilence/SuperMarioBros-CSharp-Remake
- https://github.com/majorsilence/advanceddatagridview

