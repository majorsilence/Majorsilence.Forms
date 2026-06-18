# Platform backends

Modern.Forms does **all of its own drawing** with SkiaSharp. Every control paints into an
`SKSurface`/`SKCanvas`; the windowing toolkit underneath is only a *host* — it creates native
windows, runs the message loop, delivers input, and presents the Skia surface to the screen.

That host is abstracted behind a small seam so Modern.Forms can run on more than one toolkit:

| Assembly | Backend | Notes |
|----------|---------|-------|
| `Modern.Forms.Avalonia` | Avalonia 12 (`AvaloniaPlatformBackend`) | Default desktop backend (Windows/macOS/Linux). |
| `Modern.Forms.Headless` | Dependency-free SkiaSharp (`HeadlessPlatformBackend`) | Offscreen rendering for tests/servers; the reference second backend. |
| `Modern.Forms.Uno` | Uno Platform / Skia (`UnoPlatformBackend`) | Builds against `Uno.WinUI 6.0.465` + `SkiaSharp.Views.Uno.WinUI`; presents via `SKXamlCanvas`. Runs through a Uno app head (`samples/Gallery.Uno`) — verified bootstrapping + rendering Modern.Forms on macOS. |

The **core `Modern.Forms` assembly references no windowing toolkit** — only SkiaSharp. Backends are
separate assemblies that depend on the core and reach into its internal render/input plumbing via
`[InternalsVisibleTo]`.

## The seam

Two interfaces in `Modern.Forms.Backends` define everything a host must provide.

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
side (native input → Modern.Forms, and paint requests) is delivered by the backend calling the
owning window's neutral methods directly, none of which expose any platform type:

- **Paint:** `WindowBase.RenderFrame(SKCanvas canvas, int physW, int physH, double scaling)` — the
  backend creates/obtains a Skia surface for the window and calls this to draw a frame.
- **Pointer:** `HandlePointerPressed/Released/Moved/Wheel/Exited(MouseButtons, int x, int y, …, Keys)`
- **Keyboard:** `HandleKeyDown(Keys)→bool`, `HandleKeyUp(Keys)→bool`, `HandleTextInput(string)→bool`
  (the `bool` is "handled" — the backend maps it to its native "handled" flag).
- **Lifecycle:** `OnBackendActivated/OnBackendDeactivated/OnBackendClosed()` and
  `OnBackendClosing()→bool` (true = cancel the close).

All coordinates crossing the seam are `System.Drawing` value types and `Modern.Forms` enums
(`MouseButtons`, `Keys`, `CursorType`, `WindowEdge`, `FormWindowState`); no toolkit types leak into
the core.

### Selecting the backend

`Modern.Forms.Backends.Platform.Backend` holds the active `IPlatformBackend`. If unset, it is
resolved by name (reflection) to `Modern.Forms.Backends.AvaloniaPlatformBackend, Modern.Forms.Avalonia`
when that assembly is referenced — so a desktop app just references `Modern.Forms.Avalonia` and calls
`Application.Run(new MyForm())` with zero configuration. To use a different backend, set it before the
first window is created:

```csharp
Modern.Forms.Backends.Platform.Backend = new Modern.Forms.Headless.HeadlessPlatformBackend ();
```

## The Headless backend (reference)

`Modern.Forms.Headless` is the simplest possible backend and a good template:

- `HeadlessPlatformBackend` — a work-queue "message loop", in-memory clipboard, a virtual screen,
  a `System.Threading.Timer`-based `IPlatformTimer`, and a `RunModalLoop` that pumps the queue.
- `HeadlessWindowHost` — renders the owner into an offscreen `SKSurface`; chrome/input are no-ops.
- `HeadlessRenderer` — `Use()` installs the backend; `CapturePng(window, w, h)` renders to PNG; and
  input-injection helpers (`Click`, `MouseDown/Up/Move`, `KeyDown/Up`, `TextInput`) drive the same
  neutral `Handle*` path a real backend uses.

It needs no display, so it powers the unit tests (`tests/Modern.Forms.Tests` runs entirely on it via a
`[ModuleInitializer]`) and can render the ControlGallery headlessly:

```
dotnet run --project samples/ControlGallery -- --render-headless out.png 1100 750 --select-row 0
```

## The Uno backend

`Modern.Forms.Uno` implements the seam on Uno Platform's Skia target:

- `UnoPlatformBackend : IPlatformBackend` — drives the Uno `DispatcherQueue`
  (`Post`/`Invoke`/`CheckAccess`), a `DispatcherTimer`, the WinUI clipboard, and `RunModalLoop`.
- `UnoWindowHost : IWindowBackend` — hosts a `SkiaSharp.Views.Windows.SKXamlCanvas`; its
  `PaintSurface` calls `owner.RenderFrame(canvas, physW, physH, scaling)`, and Uno pointer/key/character
  events are translated (via `UnoKeyInterop`) into the neutral `owner.Handle*` calls.

The backend **library** depends only on `Uno.WinUI` + `SkiaSharp.Views.Uno.WinUI` (restored from
nuget.org via `src/Modern.Forms.Uno/nuget.config`, since the corporate feeds 403 on Uno). It pins
`SkiaSharp.Views.Uno.WinUI` to `3.119.4` to match the core `SkiaSharp` version.

**Running it** needs a Uno *app head* — a sample is provided at `samples/Gallery.Uno`. It references
the platform Skia runtimes (`Uno.WinUI.Runtime.Skia.X11`/`.Win32`/`.MacOS`, all at Uno `6.0.465`),
builds the host, installs the backend, and shows a Modern.Forms window:

```csharp
var host = UnoPlatformHostBuilder.Create ()
    .App (() => new ModernFormsUnoApp ())   // OnLaunched: Platform.Backend = new UnoPlatformBackend(); new DemoForm().Show();
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

### Known limitation: interactive window drag

`BeginMoveDrag`/`BeginResizeDrag` are no-ops on the Uno backend. WinUI/Uno has no programmatic
"begin drag from code" API — window move/resize is OS-driven via a designated title-bar region
(`Window.SetTitleBar` + `ExtendsContentIntoTitleBar`) rather than a call invoked from a pointer-down
handler. Supporting Modern.Forms' custom (self-drawn) chrome would require mapping its title-bar
layout onto WinUI's non-client `InputNonClientPointerSource` regions (with passthrough holes for the
caption buttons) — a platform-specific integration left as future work. Use native decorations
(`UseSystemDecorations`) on Uno if you need OS window dragging.

### Adding another backend

A new backend is a new assembly referencing `Modern.Forms` (core) + the toolkit, implementing the two
interfaces — mirror the Avalonia/Headless/Uno trio: drive the dispatcher + lifecycle in the
`IPlatformBackend`, and present a Skia surface (calling `owner.RenderFrame`) + translate input
(`owner.Handle*`) in the `IWindowBackend`. Add an `[InternalsVisibleTo]` entry in the core `.csproj`.
