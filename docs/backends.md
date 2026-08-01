# Platform backends

Majorsilence.Forms does **all of its own drawing** with SkiaSharp. Every control paints into an
`SKSurface`/`SKCanvas`; the windowing toolkit underneath is only a *host* — it creates native
windows, runs the message loop, delivers input, and presents the Skia surface to the screen.

That host is abstracted behind a small seam so Majorsilence.Forms can run on more than one toolkit:

| Assembly | Backend | Notes |
|----------|---------|-------|
| `Majorsilence.Forms.Avalonia` | Avalonia 12 (`AvaloniaPlatformBackend`) | Default desktop backend (Windows/macOS/Linux). Avalonia also ships its own Android, iOS, and Browser (WASM) targets, so this backend is a second path to mobile and web alongside Uno — not just desktop. |
| `Majorsilence.Forms.Headless` | Dependency-free SkiaSharp (`HeadlessPlatformBackend`) | Offscreen rendering for tests/servers; the reference second backend. |
| `Majorsilence.Forms.Uno` | Uno Platform / Skia (`UnoPlatformBackend`) | Builds against `Uno.WinUI 6.5.237` + `SkiaSharp.Views.Uno.WinUI`; presents via `SKXamlCanvas`. Runs through a Uno app head (`samples/Gallery.Uno`) — verified bootstrapping + rendering Majorsilence.Forms on macOS. |

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
```

Drop the result into any native visual tree. This is exactly what `samples/EmbeddingAvalonia` and
`samples/EmbeddingUno` do.

**MF Form → host window.** A `Form`'s backend window is created eagerly in the Form's own constructor
(before `Show()` is ever called), and on both backends that object already *is* (Avalonia) or *wraps*
(Uno) a real native window. `ToAvaloniaWindow()`/`ToUnoWindow()` hand that window back directly:

```csharp
Avalonia.Controls.Window window = myForm.ToAvaloniaWindow ();   // Majorsilence.Forms.AvaloniaHostInterop
Microsoft.UI.Xaml.Window  window = myForm.ToUnoWindow ();       // Majorsilence.Forms.Uno.UnoHostInterop
```

The host owns showing it from here on — assign it as the app's main window, set `Owner`, call
`Show()`/`ShowDialog(owner)`, etc. Majorsilence's own `Load`/`Shown`/`Application.OpenForms`
bookkeeping still runs correctly the first time the window actually becomes visible, regardless of
which side triggered that.

**Owner/modal-dialog relationships differ by backend.** A real `Avalonia.Controls.Window` supports
native `.Owner` and `.ShowDialog(owner)`, so `ToAvaloniaWindow()` gives a host app a genuine OS-level
modal relationship (see the "Open as Avalonia dialog" button in `samples/EmbeddingAvalonia`). Uno has
no such concept in this backend today — `UnoWindowHost`'s own `ShowDialog` implementation already just
shows the window and ignores any owner — so `ToUnoWindow()` only gives back an independent top-level
window (see `samples/EmbeddingUno`'s "Open as Uno window" button). `Form.ShowDialog(parent)` (MF's own
modal loop, which doesn't depend on native window ownership) is the way to get modal behavior under
Uno regardless of hosting style.

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
    threshold (`UnoGestureWiring.MinSwipeVelocity`), a heuristic rather than a platform capability.

Like the rest of the backend seam, this is wired through new methods directly on the concrete
`WindowBase` class (`HandleLongPress`/`HandlePinch`/`HandleSwipe`/`HandleScrollGesture`), not through
`IWindowBackend`/`IPlatformBackend` — a backend that doesn't call them just never raises gesture
events, with nothing to implement and no effect on its own behavior (the same pattern as the
optional `IWebViewFactory` capability above).

### Adding another backend

A new backend is a new assembly referencing `Majorsilence.Forms` (core) + the toolkit, implementing the two
interfaces — mirror the Avalonia/Headless/Uno trio: drive the dispatcher + lifecycle in the
`IPlatformBackend`, and present a Skia surface (calling `owner.RenderFrame`) + translate input
(`owner.Handle*`) in the `IWindowBackend`. Add an `[InternalsVisibleTo]` entry in the core `.csproj`.
