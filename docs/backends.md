# Platform backends

Majorsilence.Forms does **all of its own drawing** with SkiaSharp. Every control paints into an
`SKSurface`/`SKCanvas`; the windowing toolkit underneath is only a *host* — it creates native
windows, runs the message loop, delivers input, and presents the Skia surface to the screen.

That host is abstracted behind a small seam so Majorsilence.Forms can run on more than one toolkit:

| Assembly | Backend | Notes |
|----------|---------|-------|
| `Majorsilence.Forms.Avalonia` | Avalonia 12 (`AvaloniaPlatformBackend`) | Default desktop backend (Windows/macOS/Linux). Also multi-targets Browser/WASM, Android, and iOS through Avalonia's own platform packages, so it is a second path to mobile and web alongside Uno — see [The Avalonia backend](#the-avalonia-backend). |
| `Majorsilence.Forms.Headless` | Dependency-free SkiaSharp (`HeadlessPlatformBackend`) | Offscreen rendering for tests/servers; the reference second backend. |
| `Majorsilence.Forms.Uno` | Uno Platform / Skia (`UnoPlatformBackend`) | Builds against `Uno.WinUI 6.5.237` + `SkiaSharp.Views.Uno.WinUI`; presents via `SKXamlCanvas`. Runs through a Uno app head (`samples/Gallery.Uno`) — verified bootstrapping + rendering Majorsilence.Forms on macOS. |
| `Majorsilence.Forms.WinForms` | System.Windows.Forms (`WinFormsPlatformBackend`) | Windows-only migration backend: real WinForms windows on the classic Win32 pump, presenting Skia through a GDI-backed control. Exists for incremental migration — embed MF controls in a WinForms app via `ToWinFormsControl()`, then swap to Avalonia/Uno when fully ported. See [The WinForms backend](#the-winforms-backend). |

The **core `Majorsilence.Forms` assembly references no windowing toolkit** — only SkiaSharp. Backends are
separate assemblies that depend on the core and reach into its internal render/input plumbing via
`[InternalsVisibleTo]`.

## The seam

Two interfaces in `Majorsilence.Forms.Backends` define everything a host must provide.

### `IPlatformBackend` — application + process services

```
Name, Initialize, RunMainLoop(token), Stop, Post, Invoke, Invoke<T>, CheckAccess, DoEvents,
CreateWindow(WindowBase, isPopup), CreateTimer,
GetClipboardText / SetClipboardText / ClearClipboard,
GetScreens, RunModalLoop(Task)
```

### `IWindowBackend` — one native window

```
Location, Size, ClientSize, Scaling,
Show, ShowDialog(owner), Hide, Close, Activate,
Title, Topmost, SetSystemDecorations, SetCursor(CursorType), SetIcon, MinimumSize, MaximumSize,
CanResize, ShowInTaskbar, Opacity, WindowState, Enabled,
PointToClient / PointToScreen, BeginMoveDrag, BeginResizeDrag(WindowEdge), Invalidate,
ShowOpenFileDialog / ShowSaveFileDialog / ShowOpenFolderDialog
```

`IWindowBackend` is the **pull** side — operations `WindowBase` invokes on its window. The **push**
side (native input → Majorsilence.Forms, and paint requests) is delivered by the backend calling the
owning window's neutral methods directly, none of which expose any platform type:

- **Paint:** `WindowBase.RenderFrame(SKCanvas canvas, int physW, int physH, double scaling)` — the
  backend creates/obtains a Skia surface for the window and calls this to draw a frame.
- **Pointer:** `HandlePointerPressed/Released/Moved/Wheel/Exited(MouseButtons, int x, int y, …, Keys)`
- **Keyboard:** `HandleKeyDown(Keys)→bool`, `HandleKeyUp(Keys)→bool`, `HandleTextInput(string)→bool`
  (the `bool` is "handled" — the backend maps it to its native "handled" flag).
- **Lifecycle:** `OnBackendActivated/OnBackendDeactivated/OnBackendClosed()` and
  `OnBackendClosing()→bool` (true = cancel the close).

All coordinates crossing the seam are `System.Drawing` value types and `Majorsilence.Forms` enums
(`MouseButtons`, `Keys`, `CursorType`, `WindowEdge`, `FormWindowState`); no toolkit types leak into
the core.

### Selecting the backend

`Majorsilence.Forms.Backends.Platform.Backend` holds the active `IPlatformBackend`. If unset, it is
resolved by name (reflection) to `Majorsilence.Forms.Backends.AvaloniaPlatformBackend, Majorsilence.Forms.Avalonia`
when that assembly is referenced — so a desktop app just references `Majorsilence.Forms.Avalonia` and calls
`Application.Run(new MyForm())` with zero configuration. To use a different backend, set it before the
first window is created:

```csharp
Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.Headless.HeadlessPlatformBackend ();
```

## Embedding in a host app

Everything above assumes Majorsilence.Forms owns the top-level window and the backend is just the
rendering host underneath (`Form.Show()` → `Platform.Backend.CreateWindow()`). The Avalonia and Uno
backends also support the *reverse* direction: an existing Avalonia or Uno app that wants to use MF
objects as if they were its own native objects, additively and without changing anything about the
usual `Form.Show()` flow.

**MF Control → host control**, via `MajorsilenceFormsPresenter` (a real `Avalonia.Controls.Canvas` /
WinUI `Grid`) and its convenience extension methods:

```csharp
// Avalonia (namespace Majorsilence.Forms)
Avalonia.Controls.Control hostControl = myMfControl.ToAvaloniaControl ();

// Uno (namespace Majorsilence.Forms.Uno)
Microsoft.UI.Xaml.FrameworkElement hostControl = myMfControl.ToUnoControl ();

// WinForms (namespace Majorsilence.Forms.WinForms)
System.Windows.Forms.Control hostControl = myMfControl.ToWinFormsControl ();
```

Drop the result into any native visual tree. This is exactly what `samples/EmbeddingAvalonia`,
`samples/EmbeddingUno` and `samples/EmbeddingWinForms` do.

**MF Form → host window.** A `Form`'s backend window is created eagerly in the Form's own constructor
(before `Show()` is ever called), and on both backends that object already *is* (Avalonia) or *wraps*
(Uno) a real native window. `ToAvaloniaWindow()`/`ToUnoWindow()` hand that window back directly:

```csharp
Avalonia.Controls.Window  window = myForm.ToAvaloniaWindow ();   // Majorsilence.Forms.AvaloniaHostInterop
Microsoft.UI.Xaml.Window  window = myForm.ToUnoWindow ();        // Majorsilence.Forms.Uno.UnoHostInterop
System.Windows.Forms.Form form   = myForm.ToWinFormsForm ();     // Majorsilence.Forms.WinForms.WinFormsHostInterop
```

The host owns showing it from here on — assign it as the app's main window, set `Owner`, call
`Show()`/`ShowDialog(owner)`, etc. Majorsilence's own `Load`/`Shown`/`Application.OpenForms`
bookkeeping still runs correctly the first time the window actually becomes visible, regardless of
which side triggered that.

**Owner/modal-dialog relationships differ by backend.** A real `Avalonia.Controls.Window` supports
native `.Owner` and `.ShowDialog(owner)`, so `ToAvaloniaWindow()` gives a host app a genuine OS-level
modal relationship (see the "Open as Avalonia dialog" button in `samples/EmbeddingAvalonia`) — and
the `System.Windows.Forms.Form` handed back by `ToWinFormsForm()` gives the same, through WinForms'
own `Owner`/`ShowDialog(owner)` (see `samples/EmbeddingWinForms`'s "Open as WinForms dialog" button). Uno has
no such concept in this backend today — `UnoWindowHost`'s own `ShowDialog` implementation already just
shows the window and ignores any owner — so `ToUnoWindow()` only gives back an independent top-level
window (see `samples/EmbeddingUno`'s "Open as Uno window" button). `Form.ShowDialog(parent)` (MF's own
modal loop, which doesn't depend on native window ownership) is the way to get modal behavior under
Uno regardless of hosting style.

## The Avalonia backend

`Majorsilence.Forms.Avalonia` is the default, and what a new desktop app gets with zero
configuration. On Windows/macOS/Linux `MajorsilenceFormsWindowHost` *is* a real
`Avalonia.Controls.Window`, which is why this is the only cross-platform backend that implements
`TryGetPlatformHandle` (`HWND`/`NSWindow`/`XID` — see [`native-interop.md`](native-interop.md); the
Windows-only WinForms backend returns its form's HWND too), and
why `ToAvaloniaWindow()` gives a host app genuine OS-level `Owner`/`ShowDialog` semantics where the
Uno equivalent cannot.

It is not desktop-only. The project multi-targets:

| TFM | Built | Avalonia platform package |
|---|---|---|
| `net8.0`, `net10.0` | Always | `Avalonia.Desktop` + `Avalonia.Controls.WebView` |
| `net10.0-browser` | Always | `Avalonia.Browser` |
| `net10.0-android` | Opt-in: `EnableAndroidTarget=true` | `Avalonia.Android` |
| `net10.0-ios` | Opt-in: `EnableIOSTarget=true` | `Avalonia.iOS` |

The browser row is unconditional because wasm-tools is only needed to *publish*. Android and iOS are
opt-in because their workloads' reference assemblies are needed just to **compile** that row, so
listing them unconditionally would break `dotnet build` for every contributor without the workload
installed. `samples/Gallery.Android` and `samples/Gallery.iOS` set the property on their
`ProjectReference`; CI sets it only in the dedicated jobs that install the workload first. The iOS
workload additionally only installs on macOS at all.

### Single-view platforms (browser, Android, iOS)

None of those three has an OS window manager — each offers exactly one embeddable view per
app/tab/screen (`ISingleViewApplicationLifetime.MainView`), and Avalonia's browser platform's
`CreateWindow` always throws. They therefore share one host, `MajorsilenceFormsSingleViewHost`,
compiled in under the `SINGLEVIEW` constant, where **every** Majorsilence.Forms window is a `Canvas`
rather than an Avalonia `Window`:

- The first non-popup window becomes `MainHost` and registers itself as MainView, filling the
  viewport through ordinary Stretch layout.
- Everything else — popups like ComboBox dropdowns and menus, plus any additional top-level forms —
  is an absolutely positioned child of that Canvas. There is only one "screen" (the page/activity),
  so `PointToScreen`/`PointToClient` and `Location` need no special-casing between the root and its
  overlay children.

Startup is host-driven rather than a blocking `Application.Run`, so each platform has its own entry
point taking a **factory** — constructing a `WindowBase` touches the backend, so the form must not
exist until after the backend is initialized:

```csharp
await Majorsilence.Forms.Application.RunBrowserAsync (() => new MainForm ());  // async bootstrap
Majorsilence.Forms.Application.RunAndroid (() => new MainForm ());             // from OnCreate
Majorsilence.Forms.Application.RunIOS (() => new MainForm ());                 // from FinishedLaunching
```

None of them block or run a main loop: the host's own event loop (the tab's JS event loop, the
Activity's Looper, the OS run loop) drives the UI from then on, and `RunCore` must not be called.

**What doesn't work there**, inherent to having no window manager rather than pending work:

- **No window chrome.** `Title`, `Topmost`, `SetSystemDecorations`, `SetIcon`, `MinimumSize`/
  `MaximumSize`, `CanResize`, `ShowInTaskbar`, and `WindowState` are all no-ops — `WindowState` always
  reads `Normal`, so maximize/minimize do nothing. `BeginMoveDrag`/`BeginResizeDrag` have no window
  manager to drag against.
- **`ShowDialog` isn't OS-modal**, because there is no modal window concept. It still *behaves*
  modally: the parent-disable and blocking wait that make it modal live above the seam, in
  `WindowBase.ShowDialog` + `RunModalLoop`.
- **No WebView.** `AvaloniaWebViewHandle.cs` is excluded from the compile for every single-view TFM
  (no WebView2/WKWebView/WebKitGTK there), and the backend's WebView members report unsupported, so
  compat controls that need one — `RadPdfViewer`, `RadRichTextEditor` — fall back to their
  plain-viewer/`RichTextBox` paths. See `COMPATIBILITY_MATRIX.md`.
- **Outside-click popup dismissal via window deactivation doesn't fire**, since a Canvas has no such
  concept. Clicking elsewhere *inside* the app still dismisses popups (`Control.RaiseMouseDown` closes
  them independently of window activation); only losing focus to something outside the app entirely
  is unhandled.

Maturity differs sharply across the three, and none is part of the headless CI build: the browser
path runs the full gallery (`samples/Gallery.Wasm`) but is young, Android boots the gallery without
having had real-device testing, and iOS has never been compiled at all. See
[`samples.md`](samples.md) for how to build and run each.

## The Headless backend (reference)

`Majorsilence.Forms.Headless` is the simplest possible backend and a good template:

- `HeadlessPlatformBackend` — a work-queue "message loop", in-memory clipboard, a virtual screen,
  a `System.Threading.Timer`-based `IPlatformTimer`, and a `RunModalLoop` that pumps the queue.
- `HeadlessWindowHost` — renders the owner into an offscreen `SKSurface`; chrome/input are no-ops.
- `HeadlessRenderer` — `Use()` installs the backend; `CapturePng(window, w, h)` renders to PNG; and
  input-injection helpers (`Click`, `MouseDown/Up/Move`, `KeyDown/Up`, `TextInput`) drive the same
  neutral `Handle*` path a real backend uses.

It needs no display, so it powers the unit tests (`tests/Majorsilence.Forms.Tests` runs entirely on it via a
`[ModuleInitializer]`) and can render the ControlGallery headlessly:

```
dotnet run --project samples/Gallery.Avalonia -- --render-headless out.png 1100 750 --select-row 0
```

## The Uno backend

`Majorsilence.Forms.Uno` implements the seam on Uno Platform's Skia target:

- `UnoPlatformBackend : IPlatformBackend` — drives the Uno `DispatcherQueue`
  (`Post`/`Invoke`/`CheckAccess`), a `DispatcherTimer`, the WinUI clipboard, and `RunModalLoop`.
- `UnoWindowHost : IWindowBackend` — hosts a `SkiaSharp.Views.Windows.SKXamlCanvas`; its
  `PaintSurface` calls `owner.RenderFrame(canvas, physW, physH, scaling)`, and Uno pointer/key/character
  events are translated (via `UnoKeyInterop`) into the neutral `owner.Handle*` calls.

The backend **library** depends only on `Uno.WinUI` + `SkiaSharp.Views.Uno.WinUI` (restored from
nuget.org via `src/Majorsilence.Forms.Uno/nuget.config`, since the corporate feeds 403 on Uno). It pins
`SkiaSharp.Views.Uno.WinUI` to `3.119.4` to match the core `SkiaSharp` version.

**Running it** needs a Uno *app head* — a sample is provided at `samples/Gallery.Uno`. It references
the platform Skia runtimes (`Uno.WinUI.Runtime.Skia.X11`/`.Win32`/`.MacOS`, all at Uno `6.5.237`),
builds the host, installs the backend, and shows a Majorsilence.Forms window:

```csharp
var host = UnoPlatformHostBuilder.Create ()
    .App (() => new MajorsilenceFormsUnoApp ())   // OnLaunched: Platform.Backend = new UnoPlatformBackend(); new DemoForm().Show();
    .UseX11 ().UseWin32 ().UseMacOS ()
    .Build ();
host.Run ();
```

`samples/Gallery.Uno` references the `ControlGallery` sample and shows its full `MainForm`, so the
entire control gallery renders on Uno. Run it on a desktop session (it needs a windowing session, so
it is not part of the headless CI build, and its Uno packages come from nuget.org via
`samples/Gallery.Uno/nuget.config`):

```
dotnet run --project samples/Gallery.Uno
```

Verified on macOS: the Uno host launches, `UnoPlatformBackend` creates the window, and the gallery's
`MainForm` renders into the `SKXamlCanvas` (RenderFrame 1080×720).

### Window drag & resize with self-drawn chrome

`BeginMoveDrag`/`BeginResizeDrag` are no-ops on the Uno backend — WinUI/Uno has no programmatic
"begin drag from code" API. Instead, window move/resize for Majorsilence.Forms' custom (self-drawn)
chrome is handled **declaratively**:

- **Resize** comes for free: a borderless `OverlappedPresenter`
  (`SetBorderAndTitleBar(false, false)`) keeps the OS resize margins, so the window stays resizable
  as long as `IsResizable` isn't forced off. `UnoWindowHost.ApplyDecorations` drives it from
  `CanResize`.
- **Title-bar drag + Snap Layouts** come from the new `IWindowBackend.SetCaptionRegions` seam. The
  `Form` publishes its title-bar strip (minus the caption buttons, which stay clickable client area)
  on every layout/resize via `OnClientLayoutChanged`; `UnoWindowHost` forwards it to WinUI's
  `InputNonClientPointerSource.SetRegionRects(NonClientRegionKind.Caption, …)` in physical pixels.

`SetCaptionRegions` is a default no-op on the interface, so backends using the interactive
`BeginMoveDrag` path (Avalonia) ignore it.

**Platform support:** `InputNonClientPointerSource` is a Windows-desktop / WinAppSDK API, so OS
title-bar drag works on the **Win32 desktop head**. On the macOS head `Form` uses native decorations
(`UseSystemDecorations`) and the OS owns drag/resize. On the **X11 head** the caption-region call
no-ops (caught) — edge-resize may still work via the presenter, but title-bar drag is unavailable;
use `UseSystemDecorations` there if you need OS window dragging.

## The WinForms backend

`Majorsilence.Forms.WinForms` implements the seam on classic `System.Windows.Forms` — Windows-only
by definition, and built for one purpose: **incremental migration**. A WinForms app (or a WinForms
control library's consumers) can adopt Majorsilence.Forms one control at a time, with everything
running on real WinForms windows and the app's existing Win32 message pump; when the last piece is
ported, swapping this backend for Avalonia or Uno takes the same code cross-platform.

- `WinFormsPlatformBackend : IPlatformBackend` — drives a `System.Windows.Forms` message loop
  (`Application.Run` when Majorsilence.Forms owns the app; the host's own loop when embedded), a
  hidden marshaling control for `Post`/`Invoke`, `System.Windows.Forms.Timer`, the WinForms
  clipboard, `Screen.AllScreens`, and a `DoEvents`-pumping `RunModalLoop`.
- `WinFormsWindowHost : IWindowBackend` — a real `System.Windows.Forms.Form` filled by a
  `SkiaHostControl`, which renders `owner.RenderFrame` into a SkiaSharp surface backed by a GDI
  bitmap (the same present technique as SkiaSharp's own WinForms `SKControl`, done in-repo because
  that package's types collide with the core assembly's `SkiaSharp.Views.Desktop` compatibility
  shims). WinForms mouse/keyboard events are already in physical device pixels and the WinForms
  `Keys`/`MouseButtons` enums are numerically identical to Majorsilence.Forms' own, so input
  translation is a cast (`WinFormsKeyInterop`). Popups are borderless `WS_EX_NOACTIVATE` tool
  windows; `BeginMoveDrag`/`BeginResizeDrag` use the classic `WM_NCLBUTTONDOWN` non-client-hit
  trick; `TryGetPlatformHandle` returns the real HWND (untested against the Windows UI Automation
  bridge so far, but the handle it needs is there).
- `MajorsilenceFormsPresenter : System.Windows.Forms.Control` + `ToWinFormsControl()`/
  `ToWinFormsForm()` — the embedding direction, mirroring the Avalonia/Uno presenters (see
  [Embedding in a host app](#embedding-in-a-host-app)). The presenter installs the backend
  automatically when none is configured, and implements `INativeControlHostBackend` so real
  WinForms controls can sit inside the embedded scene.

Verified interactively on Windows via `samples/EmbeddingWinForms`: rendering, mouse + keyboard
input, combo-dropdown popups, `NativeControlHost` overlays, and `ToWinFormsForm()` native modal
dialogs. Not implemented: gestures (WinForms has no gesture API — touch arrives as mouse), and
`IWebViewFactory` (WebView-dependent compat controls fall back, as on Headless).

Relationship to `Majorsilence.Forms.WindowsFormsInterop`: the interop package bridges **whole
forms** between a WinForms app and Majorsilence.Forms-on-Avalonia sharing one message pump; this
backend removes Avalonia from the picture and works at **control** granularity. They can coexist —
the presenter leaves an already-configured backend alone.

## Gesture support

`Control` has five new, purely-additive events for touch/pen input: `LongPress`, `Pinch` (pinch-to-
zoom and two-finger rotate together — see `PinchGestureEventArgs.Scale`/`Angle`/`AngleDelta`),
`Swipe`, and `ScrollGesture` (continuous drag-to-pan, still firing with a decaying delta during the
platform's own momentum/inertia phase after the contact lifts — this is the whole flick/momentum-
scrolling implementation, no deceleration physics written here). None of them fire for the mouse.
`ScrollableControl` already applies `ScrollGesture` to `AutoScrollPosition` automatically (content
follows the finger), so existing `Panel`/`ListBox`/`TreeView`/etc. subclasses gained touch panning
with no app code changes; `LongPress`'s default handler opens `ContextMenu` if one is set, mirroring
the existing right-click behavior in `Control.OnClick`.

Both the **Avalonia** and **Uno** backends implement this, via a per-backend `*GestureWiring` helper
attaching the platform's own gesture facilities to each host control (`MajorsilenceFormsWindowHost`,
`MajorsilenceFormsSingleViewHost` — the class Avalonia's Android/browser targets use — and
`MajorsilenceFormsPresenter`, on both backends) — so it works the same way whether Majorsilence.Forms
owns the top-level window or is embedded via `ToAvaloniaControl()`/`ToAvaloniaWindow()`/
`ToUnoControl()`/`ToUnoWindow()`. The two backends' underlying gesture models are meaningfully
different, though, confirmed by decompiling the actual installed packages rather than assumed:

- **Avalonia** (`AvaloniaGestureWiring`) attaches separate, dedicated recognizer classes
  (`PinchGestureRecognizer`/`SwipeGestureRecognizer`/`ScrollGestureRecognizer`) plus the built-in
  `Holding` event. Every one of those recognizers is self-gated to touch/pen pointers by Avalonia
  itself, so this is attached unconditionally with no effect on mouse-driven desktop interactions.
- **Uno** (`UnoGestureWiring`) uses WinUI's unified manipulation model instead — one
  `UIElement.ManipulationMode` flags enum and one `ManipulationDelta`/`ManipulationCompleted` event
  stream covering pan, pinch-zoom, and rotate together, split in `UnoGestureWiring` itself into
  `Pinch` vs. `ScrollGesture` calls based on whether a given frame's incremental scale/rotation is
  non-trivial. Two real differences from Avalonia here, not just an implementation detail:
  - WinUI's manipulation engine is **not** self-gated to non-mouse pointers (unlike Avalonia's
    recognizers) — `UnoGestureWiring` filters `PointerDeviceType.Mouse` out itself in every
    manipulation handler to keep ordinary desktop mouse-drag interactions unaffected. `Holding`
    (long-press) doesn't need this: subscribing to it only ever enables the recognizer's non-mouse
    "Hold" setting, never the separate "HoldWithMouse" one, so it's safe by construction.
  - WinUI has no native swipe gesture (only the unrelated, heavyweight `SwipeControl` reveal-action
    control) — `Swipe` is synthesized from `ManipulationCompleted`'s velocity against a chosen
    threshold, a heuristic rather than a platform capability.
  - **The two platforms report velocity in different units**, confirmed from each vendor's own shipped
    XML documentation: Avalonia's `SwipeGestureEventArgs.Velocity` is pixels per *second*, WinUI's
    `ManipulationVelocities.Linear` is DIP per *millisecond*. `SwipeGestureEventArgs.VelocityX`/`Y`
    here are documented as pixels per second, so `UnoGestureWiring` converts. Passing the raw WinUI
    value through would both break that contract and — since the threshold is expressed per second —
    demand roughly 150× the speed a human can move, so `Swipe` would never have fired at all.

  The two judgement calls Uno needs and Avalonia does not — is this frame a pinch or a pan, and was
  that flick fast enough to be a swipe — live in `GestureHeuristics` in the core assembly rather than
  inline in the wiring, and are unit-tested (`GestureHeuristicsTests`). Neither can be verified by
  running it: the Uno backend needs multi-touch hardware, and both have failure modes that read as
  "gestures feel wrong" rather than as a crash. In particular the pinch/pan split uses a tolerance
  rather than exact equality, because two contacts dragged across a screen never hold an exactly
  constant separation — testing `Delta.Scale != 1f` classifies every two-finger pan as a pinch, and
  since the two are mutually exclusive, two-finger panning would then never scroll.

Like the rest of the backend seam, this is wired through new methods directly on the concrete
`WindowBase` class (`HandleLongPress`/`HandlePinch`/`HandleSwipe`/`HandleScrollGesture`), not through
`IWindowBackend`/`IPlatformBackend` — a backend that doesn't call them just never raises gesture
events, with nothing to implement and no effect on its own behavior (the same pattern as the
optional `IWebViewFactory` capability above).

## Hosting native elements

`INativeControlHostBackend` is a third optional capability, alongside `IWebViewFactory` — implemented
by the Avalonia, Uno and WinForms backends, absent on Headless. It lets a `NativeControlHost` control reserve a
rectangle that the backend fills with a real toolkit element (an Avalonia `Control`, an Uno
`UIElement`) overlaid on top of the Skia surface, kept aligned to the placeholder's bounds, clip and
visibility. See [`native-interop.md`](native-interop.md) for how to use it, its airspace limits, why
native handles can't be faked, and why video is usually better done with frame callbacks drawn into
Skia than with a hosted native surface.

### Adding another backend

A new backend is a new assembly referencing `Majorsilence.Forms` (core) + the toolkit, implementing the two
interfaces — mirror the Avalonia/Headless/Uno trio: drive the dispatcher + lifecycle in the
`IPlatformBackend`, and present a Skia surface (calling `owner.RenderFrame`) + translate input
(`owner.Handle*`) in the `IWindowBackend`. Add an `[InternalsVisibleTo]` entry in the core `.csproj`.
