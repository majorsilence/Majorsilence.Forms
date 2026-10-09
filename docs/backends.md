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
| `Majorsilence.Forms.Gtk4` | GTK 4 via gir.core (`Gtk4PlatformBackend`) | Builds against `GirCore.Gtk-4.0 0.8.1`; a real `Gtk.Window` per Form, Skia rendered into a Cairo image surface behind a `Gtk.DrawingArea`, GLib main loop. Runs through `samples/Gallery.Gtk4` — verified rendering + timers on Wayland. See [The GTK 4 backend](#the-gtk-4-backend). |
| `Majorsilence.Forms.Terminal` | Console / ANSI (`TerminalPlatformBackend`) | Runs a Form in a terminal as a single-view host (like a phone: the form fills the screen, no title bar): Skia renders offscreen and is shown as Kitty graphics or Sixel at the terminal's real pixel resolution, else as Unicode block elements (2×4 pixels per cell, truecolor/256/16; classic `▄` half-blocks, 1×2, when pinned). The mode comes from asking the terminal (graphics and keyboard-protocol queries plus device attributes), with environment variables as the first guess and fallback. Only changed cells/regions/tiles are rewritten. Mouse and keyboard input via raw mode + SGR mouse reporting (pixel-exact with Kitty) and, where offered, the Kitty keyboard protocol (real releases, exact modifiers); Ctrl+C exits. Run `samples/Gallery.Terminal`. |
| `Majorsilence.Forms.WinForms` | System.Windows.Forms (`WinFormsPlatformBackend`) | Windows-only migration backend: real WinForms windows on the classic Win32 pump, presenting Skia through a GDI-backed control. Exists for incremental migration — embed MF controls in a WinForms app via `ToWinFormsControl()`, then swap to Avalonia/Uno when fully ported. See [The WinForms backend](#the-winforms-backend). |
| `Majorsilence.Forms.Wpf` | WPF / System.Windows (`WpfPlatformBackend`) | Windows-only migration backend: a real WPF `Window` on the `Dispatcher` loop, presenting Skia through a `WriteableBitmap`. Same shape and purpose as the WinForms backend — embed MF controls in a WPF app via `ToWpfElement()`, then swap to Avalonia/Uno when ported. |

The **core `Majorsilence.Forms` assembly references no windowing toolkit** — only SkiaSharp. Backends are
separate assemblies that depend on the core and reach into its internal render/input plumbing via
`[InternalsVisibleTo]`.

**Target frameworks.** The core (`Majorsilence.Forms`, `Majorsilence.Forms.Drawing.Common`,
`Majorsilence.Forms.Telerik`, and the two embedded shim assemblies) multi-targets `net8.0`, `net10.0`
and `netstandard2.0`. `Majorsilence.Forms.WinForms` and `Majorsilence.Forms.Wpf` each add a `net48`
row (paired with the core's `netstandard2.0` build) alongside their `net8.0-windows` /
`net10.0-windows` targets, so a classic .NET Framework 4.8 WinForms or WPF app can use it as the
host. The `net48` row of both builds for real on every OS via the
`Microsoft.NETFramework.ReferenceAssemblies` package (the WinForms *and* WPF reference assemblies are
cross-platform; only *running* needs Windows), so it is a compile gate on all CI legs. For the
`net*-windows` rows the two backends differ: the WinForms one falls back to an empty placeholder off
Windows, while the WPF one compiles them for real everywhere through `EnableWindowsTargeting`. The
other backends (Avalonia, Uno, Gtk4, Headless) target `net8.0`+ only — there is no `netstandard2.0`
`IPlatformBackend` implementation, so on a non-Windows .NET Framework runtime an app can reference
the controls but not host a window. On the `netstandard2.0` row the five optional no-op members of `IWindowBackend`
(`SetShaped`, `SetTextInputActive`, …) are plain interface members rather than default
implementations, and `Control` carries an explicit no-op `ISupportInitialize`; both differences are
compiled out on `net8.0`/`net10.0`. The `net48` WinForms and WPF backends therefore implement those
five members explicitly.

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

### Logical vs. device pixels

Everything an application sees on `Control` is in **logical** units, the numbers it sets and reads back
at any display scaling:

- `Width`, `Height`, `Left`, `Top`, `Right`, `Bottom`, `Location`, `Bounds` and `Size`;
- `ClientSize` and `ClientRectangle` (the control minus its border);
- `MouseEventArgs.X`/`Y`;
- the paint canvas: `OnPaint`, `OnPaintBackground` and `Paint` handlers draw in these units, and
  `e.ClipRectangle` is in them too. The framework scales the canvas to the display.

So the ordinary layout and paint idioms are right at every scaling:

```csharp
child.Left = (ClientSize.Width - child.Width) / 2;                 // centres the child
e.Graphics.DrawRectangle (pen, 0, 0, Width - 1, Height - 1);       // frames the control
```

**Until 2026-10-01, `ClientSize`, `ClientRectangle` and the paint canvas were device pixels** (EVT-37,
CTL-10), and a custom control had to scale its own graphics by `e.Scaling`. Remove such a
`ScaleTransform` if you added one: the drawing would now be scaled twice. Device pixels are still
reachable for code that wants them: the explicitly named `Scaled*` family (`ScaledWidth`,
`ScaledHeight`, `ScaledBounds`, …), `PaintEventArgs.Scaling` and `LogicalToDeviceUnits`. The library's
own renderers draw in device pixels internally.

One exception remains: **owner-draw events** (`DrawItem`, `DrawNode`, `DrawListViewItem`, the grid's
`CellPainting`, …) still hand over device-pixel `Bounds` with a device-pixel `Graphics`. The two agree
with each other, but not with the rest of the control. See `BACKLOG.md`.

- `Form.ClientSize` is a **separate property**, declared on `Form` itself rather than inherited from
  `Control` (`Form` derives from `WindowBase`, not `Control`), and it is logical: built from `Size`
  minus the caption height, not from `ClientRectangle`. A `Form`'s own `ClientSize` mixes safely with
  its `Width`/`Height`; a child `Control`'s (`UserControl`, `Panel`, …) does not.
- At the backend seam itself, `IWindowBackend.ClientSize`/`Size` (the block above) are logical — each
  backend converts its own native device pixels before handing a value up through the seam (see e.g.
  `HeadlessWindowHost.ClientSize`, or the Avalonia presenter's `Bounds`) — a third, unrelated meaning of
  "ClientSize" behind the same two words. Don't assume it lines up with `Control.ClientSize`.

Test scaling assumptions like these under `MF_HEADLESS_SCALE=2` ([`docs/automation.md`](automation.md))
— they are identical to scaling 1 and wrong on any other display.

### Selecting the backend

`Majorsilence.Forms.Backends.Platform.Backend` holds the active `IPlatformBackend`. If unset, it is
resolved by name (reflection) to `Majorsilence.Forms.Backends.AvaloniaPlatformBackend, Majorsilence.Forms.Avalonia`
when that assembly is referenced — so a desktop app just references `Majorsilence.Forms.Avalonia` and calls
`Application.Run(new MyForm())` with zero configuration. To use a different backend, set it before the
first window is created:

```csharp
Majorsilence.Forms.Backends.Platform.Backend = new Majorsilence.Forms.Headless.HeadlessPlatformBackend ();
```

### Trimming and NativeAOT

`Majorsilence.Forms`, `Majorsilence.Forms.Drawing.Common`, `Majorsilence.Forms.Avalonia` and
`Majorsilence.Forms.Headless` build with `IsAotCompatible` on for their .NET target frameworks, so the
trim, AOT and single-file analyzers run and — because Release also sets `TreatWarningsAsErrors` — a new
`IL2xxx`/`IL3xxx` hazard fails the build rather than silently breaking a trimmed or NativeAOT consumer.
The reflection that *is* left (a handful of clipboard/drag "as JSON" helpers, `Message.GetLParam(Type)`)
carries `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`, so a caller is warned at its own call
site. The `Majorsilence.Forms.Backends.Platform` default-backend lookup uses `Type.GetType` with a
**constant** string, which the IL trimmer follows, so a trimmed app that just references
`Majorsilence.Forms.Avalonia` still resolves it. The netstandard2.0 rows are unanalysed — trimming and
AOT are not concepts there (.NET Framework / Mono).

The other backends (`Uno`, `Gtk4`, `WinForms`, `Wpf`, `Telerik`) are **not** analysed: Uno/WinUI, gir.core, WinForms and
WPF are themselves reflection-heavy and out of scope for an AOT guarantee.

`tests/Majorsilence.Forms.AotSmoke` is a `PublishAot=true` console that renders a `Form` (Label +
Button + TreeView) to a PNG on the Headless backend and binds a view model to a `Label` and a `TextBox`
in both directions; CI's `aot-smoke` job publishes it for `linux-x64`
and runs the native binary, so an ILC failure the analysers can't see (something in a dependency, a
trimmed static constructor, a binding that quietly does nothing) shows up as a non-zero exit.
Avalonia-on-desktop NativeAOT additionally
depends on Avalonia's own trim story and is not covered here.

### Binding and trimming

`Control.DataBindings` is live, and it is reflective: it finds the bound control property, that
property's `<Property>Changed` event and the data source's property **by name, at run time**. Nothing
in the program refers to those members in a way the trimmer or ILC can follow, and the framework's own
trim warnings at these calls are suppressed on exactly that ground ("a trimmed app has to root the
types it binds"). So a trimmed or NativeAOT app has to keep them itself, and what goes wrong depends on
what is missing:

| Not kept | What happens |
|---|---|
| The control property, for example `Label.Text` | `DataBindings.Add` throws `ArgumentException: 'Text' is not a settable public property of Label`. |
| The control's `<Property>Changed` event, for example `TextChanged` | Reading works. Text typed into the control never reaches the data source, and nothing is thrown. |
| A property of the data source (the view model) | `DataBindings.Add` throws `ArgumentException: Cannot bind to the property or column '...' on the DataSource (...)` (#290). |

Only the middle row is still silent: the control did not raise an event under this name, which is not
necessarily a mistake (a binding in `OnValidation` mode does not need one), so it stays a quiet
one-way-only degradation rather than a thrown exception. The other two are a programming error —
a typo, or a member trimmed away — and both now fail loudly at the point `DataBindings.Add` is called,
naming the member, which is what the fix in #290 was for.

**The control side is now mostly the framework's own problem, not yours.** `Majorsilence.Forms.dll`
embeds its own `ILLink.Descriptors.xml` (`src/Majorsilence.Forms/ILLink.Descriptors.xml`) rooting the
small, closed set of `<Property>`/`<Property>Changed` pairs its own controls expose for binding —
`Control.Text`/`TextChanged` (so every control, since every control derives from `Control`), plus
`CheckBox`/`RadioButton.Checked`, `ComboBox`/`ListBox.SelectedIndex`, and
`NumericUpDown`/`TrackBar`/`DateTimePicker.Value`, each with its `Changed` event. A trimmer
auto-discovers a resource with that exact name inside any assembly it trims, so this needs no
`TrimmerRootDescriptor` entry in the consuming project at all — `tests/Majorsilence.Forms.AotSmoke`
used to declare `Majorsilence.Forms`/`Control`/`Text`/`TextChanged` itself and no longer does; the smoke
test passing is what proves the embedded descriptor is doing that job on its own. This list is a
starting point, not exhaustive: a control bound through a property it does not cover (a custom control's
own value property, say) needs the same treatment your own view model does — add it to your app's
`TrimmerRootDescriptor`.

**The data-source side is still yours**, because the framework cannot know what your view model looks
like. Root what you bind with a descriptor, and add it to the app project:

```xml
<ItemGroup>
  <TrimmerRootDescriptor Include="BindingRoots.xml" />
</ItemGroup>
```

```xml
<linker>
  <assembly fullname="MyApp.ViewModels">
    <type fullname="MyApp.ViewModels.CounterViewModel">
      <property name="Count" />
    </type>
  </assembly>
</linker>
```

That is `tests/Majorsilence.Forms.AotSmoke/BindingRoots.xml` in outline (it roots `SmokeViewModel`'s
`Title` and `Name`). The smoke test is what backs it: it fails with either view-model property left out,
and passes with both present. A property genuinely missing from the view model (a typo in the binding
call, not a trimming gap) now fails the same way regardless of this file, per the table above. Rooting
whole types (`preserve="all"`) also works but keeps far more; rooting all of `Majorsilence.Forms` fails
a warnings-as-errors build with IL2026 from the resource reader (#290) — exactly the reason the embedded
descriptor above lists individual property/event pairs instead.

What is **not** established: the embedded descriptor's coverage beyond `Label`/`TextBox.Text` is not
exercised by any automated test, so check a `CheckBox`, `ComboBox` or the others it lists in your own app
with a published build before relying on them. The descriptor is tested with NativeAOT (ILC); a
`PublishTrimmed` app (and Android's `TrimMode=full` specifically) reads an embedded `ILLink.Descriptors.xml`
the same way in principle, but that is not covered by the smoke test either. And ILC's up-to-date check
does not notice a change to a `TrimmerRootDescriptor`'s contents (measured with SDK 10.0.112): if an edit
seems to have no effect, rebuild clean.

The alternative that needs nothing rooted is to wire the view model by hand, subscribing to
`PropertyChanged` and forwarding control events to an `ICommand`, which is what the ControlGallery
CommunityToolkit.Mvvm sample does. The references are then ordinary code (`nameof`, lambdas) that
trimming can see.

## Embedding in a host app

Everything above assumes Majorsilence.Forms owns the top-level window and the backend is just the
rendering host underneath (`Form.Show()` → `Platform.Backend.CreateWindow()`). The Avalonia, Uno,
WinForms, WPF and GTK 4 backends also support the *reverse* direction: an existing host app that wants
to use MF objects as if they were its own native objects, additively and without changing anything
about the usual `Form.Show()` flow.

**MF Control → host control**, via `MajorsilenceFormsPresenter` (a real `Avalonia.Controls.Canvas` /
WinUI `Grid` / WPF `Grid` / GTK `DrawingArea`) and its convenience extension methods:

```csharp
// Avalonia (namespace Majorsilence.Forms)
Avalonia.Controls.Control hostControl = myMfControl.ToAvaloniaControl ();

// Uno (namespace Majorsilence.Forms.Uno)
Microsoft.UI.Xaml.FrameworkElement hostControl = myMfControl.ToUnoControl ();

// WinForms (namespace Majorsilence.Forms.WinForms)
System.Windows.Forms.Control hostControl = myMfControl.ToWinFormsControl ();

// GTK 4 (namespace Majorsilence.Forms.Gtk4)
Gtk.Widget hostControl = myMfControl.ToGtkWidget ();
```

Drop the result into any native visual tree. This is exactly what `samples/EmbeddingAvalonia`,
`samples/EmbeddingUno`, `samples/EmbeddingWinForms` and `samples/EmbeddingGtk4` do. The GTK 4
`MajorsilenceFormsPresenter` *exposes* a `Widget` rather than deriving from a GTK widget (gir.core's
GObject subclassing needs an extra integration package and a type-registration call); everything else
is the same shape.

**MF Form → host window.** A `Form`'s backend window is created eagerly in the Form's own constructor
(before `Show()` is ever called), and on these backends that object already *is* (Avalonia, GTK 4) or
*wraps* (Uno) a real native window. `ToAvaloniaWindow()`/`ToUnoWindow()`/`ToGtkWindow()` hand that
window back directly:

```csharp
Avalonia.Controls.Window  window = myForm.ToAvaloniaWindow ();   // Majorsilence.Forms.AvaloniaHostInterop
Microsoft.UI.Xaml.Window  window = myForm.ToUnoWindow ();        // Majorsilence.Forms.Uno.UnoHostInterop
System.Windows.Forms.Form form   = myForm.ToWinFormsForm ();     // Majorsilence.Forms.WinForms.WinFormsHostInterop
Gtk.Window                window = myForm.ToGtkWindow ();         // Majorsilence.Forms.Gtk4.Gtk4HostInterop
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
| `net10.0-android` | Opt-in: `EnableAndroidHead` (forced by `EnableAndroidTarget=true`) | `Avalonia.Android` |
| `net10.0-ios` | Opt-in: `EnableIOSHead` (forced by `EnableIOSTarget=true`) | `Avalonia.iOS` |

The browser row is unconditional because wasm-tools is only needed to *publish*. Android and iOS are
opt-in because their workloads' reference assemblies are needed just to **compile** that row, so
listing them unconditionally would break `dotnet build` for every contributor without the workload
installed. `samples/Gallery.Android` and `samples/Gallery.iOS` set the property on their
`ProjectReference` — but only in the ItemGroup that their own head gate switches on, so without a
mobile workload the heads (and this row) drop out entirely rather than fail to compile.

The two gates are deliberately separate. `EnableMobileHeads` is only an umbrella that
`Directory.Build.props` resolves into `EnableAndroidHead` and `EnableIOSHead`; nothing keys on it
directly, because android and iOS need *different* workloads and a host commonly has one without the
other. CI's dedicated mobile jobs therefore name their own platform — `-p:EnableAndroidTarget=true`
and `-p:EnableIOSTarget=true` — which is both narrower (no cross-platform row dragged in) and stricter
(an ungated force, so a missing workload fails loudly instead of quietly building a stub). The iOS
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

**What doesn't work there**, mostly inherent to having no window manager rather than pending work:

- **No window chrome.** `Topmost`, `SetSystemDecorations`, `SetIcon`, `MinimumSize`/`MaximumSize`,
  `CanResize`, `ShowInTaskbar`, and `WindowState` are all no-ops — `WindowState` always reads `Normal`,
  so maximize/minimize do nothing. `BeginMoveDrag`/`BeginResizeDrag` have no window manager to drag
  against. `Title` is also a no-op today, but that one is pending work, not inherent — the browser tab
  (`document.title`) and the Android task label can both carry it.
- **`ShowDialog` isn't OS-modal**, because there is no modal window concept. It still *behaves*
  modally: the parent-disable that makes it modal lives above the seam, in `Form.ShowDialogAsync`.
  Every window after the first is an absolutely positioned child of the first one's view, so the root
  is disabled with a flag of its own rather than Avalonia's inherited `IsEnabled` (which would disable
  the dialog with it), and it ignores input that started in another window's view. A secondary window
  has no caption and opens at the view's top-left: `Screen` reports no screens there, so
  `CenterParent`/`CenterScreen` have nothing to centre on.
  **In the browser the blocking `ShowDialog` does not work at all** -- it throws, and
  `ShowDialogAsync` is the call to make; see [Browser threading](#browser-threading). **Nor does it
  on Android or iOS** -- measured, Avalonia's dispatcher cannot push a nested frame there either; see
  [Blocking modal calls on Android and iOS](#blocking-modal-calls-on-android-and-ios).
- **No WebView.** `AvaloniaWebViewHandle.cs` is excluded from the compile for every single-view TFM
  and the backend's WebView members report unsupported, so compat controls that need one —
  `RadPdfViewer`, `RadRichTextEditor` — fall back to their plain-viewer/`RichTextBox` paths. Browser/
  WASM has no native webview at all; Android and iOS do (`android.webkit.WebView` / `WKWebView`), so
  wiring `Avalonia.Controls.WebView` up for those two rows is deferred work rather than a hard limit.
  See `COMPATIBILITY_MATRIX.md`.
- **Outside-click popup dismissal via window deactivation doesn't fire**, since a Canvas has no such
  concept. Clicking elsewhere *inside* the app still dismisses popups (`Control.RaiseMouseDown` closes
  them independently of window activation); only losing focus to something outside the app entirely
  is unhandled.

**What does work there** (mobile parity, Avalonia backend):

- **On-screen keyboard.** Focusing a `TextBox` raises the platform soft keyboard and dismisses it on
  blur; `Multiline` and password boxes select the matching keyboard layout. The core drives this
  through a new default-no-op seam member, `IWindowBackend.SetTextInputActive(bool, TextInputKind)`,
  from a `SoftKeyboardObserver` watching the focus choke-point; the single-view host answers Avalonia's
  `TextInputMethodClientRequested` while a box is focused. Committed text still arrives on the normal
  `OnTextInput` → `WindowBase.HandleTextInput` path. A box asks for a different layout with
  `TextBoxBase.InputKind` (`Number`, `Email`, `Url`, `Phone`; read at focus, so set it first): a masked
  box that asks for `Number` or `Phone` gets the `Pin` keypad (numbers, no suggestions), any other masked
  box a password keyboard, and a multiline box always the multiline one. Desktop backends ignore it.
- **Safe-area insets.** The host reads `TopLevel.InsetsManager.SafeAreaPadding` (and follows
  `SafeAreaChanged` on rotation / keyboard) and pushes it in via `WindowBase.HandleSafeAreaChanged`.
  `Form` deflates its client layout by it — `Form.SafeAreaPadding` — so every docked and anchored
  control stays clear of the status bar, notch and home indicator with no app change. When the
  keyboard opens, `WindowBase.HandleInputPaneChanged` scrolls the focused field above it.

Maturity differs sharply across the three. All three compile in CI — the browser row on every build,
Android and iOS by dedicated `-p:EnableAndroidTarget=true` / `-p:EnableIOSTarget=true` jobs — but the
headless test suite exercises the shared core, not a real head.

- **Browser** runs the full gallery (`samples/Gallery.Wasm`), but the path is young.
- **Android** has had initial real-device testing: the gallery boots (an AppCompat-theme startup
  crash was found and fixed on-device), and tap hit-testing, render scaling, and touch scroll / flick
  gestures are confirmed working on hardware. The on-screen keyboard, safe-area insets, and rotation
  are unit-tested on the Headless backend but not yet exercised on a device.
- **iOS** compiles, and CI launches it in a simulator as a smoke check, but nobody has run it
  interactively on a simulator or device.

See [`samples.md`](samples.md) for how to build and run each.

### Browser threading

In the browser, .NET runs on the page's one JavaScript thread, and the tab's event loop -- not .NET --
decides when anything runs. A call that does not return also stops the input, timers and painting that
would have let it return. Desktop WinForms code blocks that thread all the time; on this target it has
to `await` instead (issue #406).

**What happens, measured.** `samples/Gallery.Wasm` has a browser-head check for exactly this
(`?check=<name>`; see [`samples.md`](samples.md#gallerywasm)). Run in headless Chrome against
Avalonia.Browser 12.1.1:

| Call | Before #406 | Now |
|---|---|---|
| `Form.ShowDialog` | Threw `PlatformNotSupportedException` with no message (`Arg_PlatformNotSupported`): Avalonia.Browser's dispatcher has no nested frame, so `Dispatcher.PushFrame` refuses. It did not hang. | Throws `PlatformNotSupportedException` naming `Form.ShowDialogAsync`, before the dialog is shown. |
| `MessageBox.Show` | The same message-less exception -- and the message box stayed on screen, since the throw came from inside the loop, after it was shown. | Throws naming `MessageBox.ShowAsync`; nothing is left open. |
| `OpenFileDialog.ShowDialog` (`CommonDialog`-style pickers) | The same message-less exception. | Throws naming `FileDialog.ShowDialogAsync`. |
| `TaskDialog.ShowDialog`, and `TaskDialog.ShowDialogAsync` | Both threw: the async one wrapped the blocking one. | `ShowDialog` throws naming `ShowDialogAsync`; `ShowDialogAsync` works. |
| `Form.ShowDialogAsync`, `MessageBox.ShowAsync` | Work: the dialog is modal and the task completes with the result. | Unchanged. |

So a nested loop cannot run here, and the blocking calls now fail *clearly* instead of obscurely. The
seam for this is `IModalLoopSupport.CanRunModalLoop` (an optional `IPlatformBackend` capability; the
Avalonia backend reports `false` on its `net10.0-browser` row, and -- for their own reason -- on
`net10.0-android` and `net10.0-ios`; see [below](#blocking-modal-calls-on-android-and-ios)). Every blocking modal entry point
checks it before it shows anything, so a refused call leaves no dialog on screen, nothing on the modal
stack and no owner disabled; `RunModalLoop` itself also throws the same explanation for a direct caller.

**The async forms.** Every modal API has one, and each works on every backend -- the dialog is just as
modal, because modality (owner disabled, `Application.ModalStack`) lives in `Form.ShowDialogAsync`, not in
the waiting:

| Blocking | Awaitable |
|---|---|
| `Form.ShowDialog ()`, `(IWin32Window)`, `(Form)` | `Form.ShowDialogAsync ()`, `(IWin32Window)`, `(Form?)` -- the same owner choice as the blocking overload |
| `MessageBox.Show (…)`, every overload | `MessageBox.ShowAsync (…)`, the same arguments |
| `OpenFileDialog`/`SaveFileDialog`/`FolderBrowserDialog.ShowDialog (…)` | `ShowDialogAsync ()`, `(IWin32Window)`, `(Form)` |
| `TaskDialog.ShowDialog (…)` | `TaskDialog.ShowDialogAsync (…)` |
| `ColorDialog`/`FontDialog`/`PrintPreviewDialog.ShowDialog` | `ShowDialogAsync (…)` (they are forms) |
| `PrintDialog`/`PageSetupDialog.ShowDialog` | `ShowDialogAsync (…)`: UI-less stubs, both answer OK at once |
| `CommonDialog.ShowDialog (…)` (your own subclass) | `CommonDialog.ShowDialogAsync (…)`; override `RunDialogAsync` to show your UI with `ShowDialogAsync` |
| `VbInteraction.MsgBox`/`InputBox` | `VbInteraction.MsgBoxAsync`/`InputBoxAsync` |
| `RadMessageBox.Show (…)` (Telerik compat) | `RadMessageBox.ShowAsync (…)` |

The usual shape is an `async void` event handler -- the one place `async void` is the idiom:

```csharp
private async void deleteButton_Click (object sender, EventArgs e)
{
    if (await MessageBox.ShowAsync (this, "Delete the selected rows?", "Orders", MessageBoxButtons.YesNo) != DialogResult.Yes)
        return;

    using var options = new DeleteOptionsForm ();
    if (await options.ShowDialogAsync (this) == DialogResult.OK)
        await DeleteAsync (options.Mode);
}
```

The same rule covers everything else that blocks: `task.Result`, `task.Wait ()` and
`GetAwaiter ().GetResult ()` wait on the one thread that would complete the task, and `Thread.Sleep`
freezes the page for its duration -- use `await` and `await Task.Delay`. Synchronous HTTP
(`HttpClient.Send`) is marked unsupported on the browser by .NET itself; use the `…Async` methods.

**The analyzer.** The `Majorsilence.Forms` package carries a Roslyn analyzer that flags these in browser
code, with a code fix to the awaited form:

| Id | Flags |
|---|---|
| `MFB001` | A blocking modal call (`ShowDialog`, `MessageBox.Show`, `TaskDialog.ShowDialog`, the pickers, `VbInteraction.MsgBox`/`InputBox`, `RadMessageBox.Show`), naming its awaitable twin. |
| `MFB002` | A synchronous wait on a task: `.Result`, `.Wait ()`, `Task.WaitAll`/`WaitAny`, `.GetAwaiter ().GetResult ()`. |
| `MFB003` | `Thread.Sleep`. |

It is silent unless the code is browser code: a `net*-browser` target framework (the SDK defines
`BROWSER`), a library that declares `<SupportedPlatform Include="browser" />`, or an explicit opt-in --
the way to cover a shared UI library that a browser head references:

```ini
# .editorconfig (or a .globalconfig) next to the shared UI library
[*.cs]
majorsilence_forms.browser_target = true
```

The code fix (`await dialog.ShowDialogAsync (this)`, `await MessageBox.ShowAsync (…)`, `await task`,
`await Task.Delay (n)`) is offered only where it keeps the program's shape: inside a function that is
already `async`, or a `void` event handler shaped `(object sender, EventArgs e)`, which it marks
`async`. Anywhere else -- a value-returning method, a constructor, inside `lock` -- making the caller
async changes its signature and every caller of it, so the diagnostic stands without a fix.
`Task.WaitAll`/`WaitAny` and `Wait (timeout)` are flagged without a fix too: their awaited forms answer
a different question.

**Why the platform's threading options don't remove the need for async.**

- **`WasmEnableThreads` with COOP/COEP.** Serving the page with `Cross-Origin-Opener-Policy: same-origin`
  and `Cross-Origin-Embedder-Policy: require-corp` lets the experimental multithreaded runtime start
  worker threads. It does not let the main thread block -- browsers still forbid that -- and JavaScript
  interop, and with it the UI, stays on the main thread. The modal loop would have to run on that
  thread, so nothing changes for it. The threaded runtime also still has open bugs (timers on .NET 10/11,
  dotnet/runtime#133984).
- **The deputy-thread proposal** (dotnet/aspnetcore#54365) runs .NET off the main thread. It is a
  proposal, and it breaks synchronous JS interop.

**What to watch** (tracked in [`BACKLOG.md`](../BACKLOG.md#browser-blocking-calls-re-evaluate-when-the-platform-moves)):

- **JSPI** (JavaScript Promise Integration) lets WebAssembly call a promise-returning browser API as if it
  were synchronous, and ships in every major browser. .NET does not use it today. If it does -- for
  example with the CoreCLR browser runtime expected in .NET 12 -- a synchronous wait on async browser
  work could become possible, and a nested modal loop with it.
- **The CoreCLR browser runtime**, and **WASM threading** becoming supported rather than experimental.

When any of these lands, re-run the Gallery.Wasm check: if a nested loop works, the Avalonia backend's
`CanRunModalLoop` is the one switch to turn back on for the browser.

### Blocking modal calls on Android and iOS

The same rule holds on the Avalonia backend's Android and iOS rows, for a different reason: the browser
has no thread of its own to block, while on Android and iOS Avalonia's dispatcher does not support a
nested frame (`Dispatcher.PushFrame`). Use the [async forms](#browser-threading) there too.

**What happens, measured.** The browser check (`ModalCheckForm.cs`) is linked into `samples/Gallery.Android`
and `samples/Gallery.iOS`, which run it in place of the gallery when launched with a check name
(`tools/modal-check.sh` in each head; see [`samples.md`](samples.md#gallerywasm)). On the mobile heads
it adds a thread-pool watchdog that would log `HUNG` after 10 s, and a UI-thread timer that counts
whether the UI kept running during the call. Debug builds, Avalonia 12.1.1, .NET 10 (SDK 10.0.108;
android workload 36.1.69, ios workload 26.5.10284):

- **Android:** emulator, Android 15 (API 35), arm64 Google APIs image; Mono runtime.
- **iOS:** iPhone 17 Pro simulator, iOS 26.5, Xcode 26.6 (built with `-p:ValidateXcodeVersion=false`,
  since this .NET for iOS asks for Xcode 26.5).

Both platforms gave the same results:

| Call | Before | Now |
|---|---|---|
| `Form.ShowDialog` | Threw a message-less `PlatformNotSupportedException` from `Dispatcher.PushFrame` (via `AvaloniaPlatformBackend.RunModalLoop`), at once. It did not hang; the UI-thread timer never ticked. | Throws `PlatformNotSupportedException` naming `Form.ShowDialogAsync`, before the dialog is shown. |
| `MessageBox.Show` | The same exception -- and the message box stayed open (two open forms after the throw). | Throws naming `MessageBox.ShowAsync`; nothing is left open. |
| `OpenFileDialog.ShowDialog` | The same exception, after the native picker was already on screen: Android's document picker and iOS's document browser both stayed up over the app. | Throws naming `FileDialog.ShowDialogAsync`; no picker is opened. |
| `TaskDialog.ShowDialog` | The same exception from `PushFrame`. | Throws naming `TaskDialog.ShowDialogAsync`. |
| `Form.ShowDialogAsync`, `MessageBox.ShowAsync`, `TaskDialog.ShowDialogAsync` | Work: the task completes with the result once the dialog is answered. | Unchanged. |

So the Avalonia backend reports `CanRunModalLoop = false` on its `net10.0-android` and `net10.0-ios`
rows as well as `net10.0-browser`, and the refusal's message gives the mobile reason and points here.

**Not measured:** a physical device of either kind; Release builds, AOT (iOS device builds are always
AOT) and Android's CoreCLR runtime; a picker actually used to choose a file (the check only asks whether
the blocking wrapper can run). The refusal came from Avalonia's dispatcher, which is the same code in
those builds, so they are not expected to differ -- but they have not been run. The browser-blocking-call analyzer
(`MFB001`-`MFB003`) is still browser-only, so a blocking call in Android or iOS code is not flagged at
build time; it fails at run time with the message above.

### Accessibility DOM (browser)

Majorsilence.Forms draws every control into one canvas, which a screen reader, the browser's
find-in-page or a DOM-based test tool cannot see into. On the browser target the Avalonia backend
therefore keeps a **DOM mirror of the open forms next to the canvas**: one transparent, click-through
element per control, carrying its ARIA role, name, state and bounds.

**Why it is built here and not taken from Avalonia.** Avalonia.Browser 12.1.1 has no accessibility layer
to reuse. The DOM it creates is the canvas, a native-control host `div` and a hidden IME `<input>`
(`createAvaloniaHost` in its `webapp/modules/avalonia/dom.ts`); there is no automation-peer-to-ARIA
bridge in its script or its assembly. Even if there were, it could not help: Majorsilence.Forms renders
all its controls into a single Avalonia visual, so Avalonia's own automation tree holds one node. The
mirror is built instead from the framework's own automation tree -- the same `AutomationElement` tree
that the Windows UI Automation bridge, the WebDriver server and in-process tests read -- so anything a
test can find, a screen reader can find too.

**How it works.**

- `AriaDom` (core, `Automation/AriaDom.cs`) maps the tree to ARIA elements and diffs two snapshots into
  create/update/remove operations. It is host-neutral and unit-tested (`AriaDomTests`).
- `AriaDomMirror` re-reads the tree after a window paints, at most every 100 ms, and sends only what
  changed: anything a reader would notice changing -- text, visibility, bounds, enabled state, focus,
  controls coming and going -- repaints, and an idle UI costs nothing. A popup opening or closing and a
  form joining or leaving `Application.OpenForms` also schedule a sync, since those need not repaint.
- `BrowserAccessibility.cs` (Avalonia backend, `net10.0-browser` row only) carries the operations to
  `BrowserAccessibility.js` through `[JSImport]`. The script is embedded in the assembly and loaded from a
  `data:` URL, so a head project needs no extra file.

**What a page gets.** Inside the host element (`<div id="out">` in the gallery), after the canvas:

```html
<div class="mf-a11y-root">
  <div id="mf-a11y-1" role="region" aria-label="Customer" data-mf-type="CustomerForm" style="left:0px;top:0px;…">
    <div id="mf-a11y-3" role="button" data-mf-automation-id="saveButton" data-mf-type="Button" style="…"><span class="mf-a11y-text">Save</span></div>
    <div id="mf-a11y-4" role="checkbox" aria-checked="true" data-mf-automation-id="agree" …><span class="mf-a11y-text">I agree</span></div>
    <div id="mf-a11y-5" role="textbox" aria-label="Customer name" data-mf-automation-id="nameBox" …><span class="mf-a11y-text">Ada</span></div>
  </div>
</div>
```

- **Roles** follow the control (or an explicit `AccessibleRole`): `button`, `checkbox`, `radio`,
  `textbox`, `combobox`, `listbox`/`option`, `tablist`/`tab`/`tabpanel`, `menubar`/`menu`/`menuitem`/
  `menuitemcheckbox`, `toolbar`, `status`, `tree`, `slider`, `spinbutton`, `progressbar`, `link`, `img`,
  `group` (named groups only), `tooltip`. A form is a named `region`; a modal dialog is
  `role="dialog" aria-modal="true"`, and a message box `role="alertdialog"` with `aria-describedby`
  pointing at its message. A tool strip item takes its strip's meaning: a menu item in a menu or menu
  bar, a `button` on a toolbar (`aria-pressed` when it checks), plain text on a status bar.
- **Names** come from `AccessibleName`, else the text without its mnemonic `&`. A role that ARIA names
  from content (button, checkbox, option, menu item, …) and plain labels carry it as text, which is also
  what find-in-page and text locators match; other roles use `aria-label`. A designer identifier
  (`button1`) is never read out as a name -- it is `data-mf-automation-id`.
- **States:** `aria-checked` (`mixed` for an indeterminate check box; also a checkable menu item's),
  `aria-selected`, `aria-disabled`, `aria-expanded`, `aria-haspopup`, `aria-pressed`, `aria-multiline`,
  `aria-valuenow`/`-min`/`-max`/`-text` for sliders and spin boxes; custom-painted controls'
  `IAutomationStateProvider` state appears as `data-mf-state-*`.
- **Focus:** the host element (which Avalonia keeps focused) gets `role="application"`, if it has no role
  of its own, and `aria-activedescendant` pointing at the focused control's element -- or, while a menu
  or a combo box's list is open, at its highlighted item, and for a focused list box at its selected
  option.
- **Privacy:** a password box's text never reaches the page; its name does.
- **Help:** a control's `AccessibilityObject.Help` (what a `QueryAccessibilityHelp` handler supplies)
  is its `aria-description`.
- **Bounds:** each element is absolutely positioned over the control it mirrors, in CSS pixels, so
  find-in-page highlights land on the right place.

**Popups.** Combo box lists, menu and context-menu drop-downs, date-picker calendars and tool tips are
`PopupWindow`s -- separate windows on the desktop backends, absolutely positioned overlays in the
browser -- and each shown one is mirrored inside the element of the window it was opened for (so a
drop-down of a modal dialog is inside the `aria-modal` dialog), positioned where it is drawn, and
removed when it closes. Each is a `<div data-mf-popup="listbox|menu|tooltip|other">` around:

- a combo box's list: `role="listbox"` named after the combo box, with its options; the combo box gets
  `aria-expanded="true"` and `aria-controls` pointing at the list;
- a menu drop-down or context menu: `role="menu"` named after the item it hangs off, with
  `menuitem`/`menuitemcheckbox`/`separator` items; an item with a submenu has `aria-haspopup="menu"` and
  `aria-expanded`, and `aria-controls` while it is open. A closed submenu is not in the DOM (the
  automation tree nests it under its item; ARIA has no menu item inside a menu item), and an open one is
  mirrored inside the menu it opened from;
- a tool tip: the div itself is `role="tooltip"` with the tip's text, and the control it is for gets
  `aria-describedby` pointing at it.

**Live region.** After the last window element the mirror keeps two visually hidden regions,
`aria-live="polite"` and `aria-live="assertive"` (`[data-mf-live]`). What they say, from
`AriaDom.Announcements` and the explicit APIs:

| Change | Said | How |
|---|---|---|
| A `Label` or `ToolStripStatusLabel` with `LiveSetting` `Polite`/`Assertive` changes its text | the new text | that politeness |
| Anything on a status bar (`StatusStrip`, `StatusBar`) changes its text | the new text | polite |
| A modal dialog opens | its title | polite |
| A message box opens | "title. message" | assertive for an error or warning icon, else polite |
| The focused combo box, slider or spin box changes value while focus stays on it | the new value | polite |
| `AccessibilityObject.RaiseAutomationNotification (kind, processing, text)` | the text | assertive for `ImportantAll`/`ImportantMostRecent`; the "most recent" kinds replace one not yet spoken |
| `AccessibilityObject.RaiseLiveRegionChanged ()` on a live label | its text | its politeness |

A live label raises `RaiseLiveRegionChanged` itself when its text changes, as upstream's `Label` does. That
one call feeds both this live region and the `AutomationObserver` that the Windows UI Automation bridge
turns into UIA's LiveRegionChanged. The mirror also notices the text change on its own, and the two
count as one announcement.

This follows the ARIA practice of announcing only what a reader would not otherwise hear: nothing on
the first sync (a page that just loaded), nothing for focus arriving on a control (the reader announces
the control, value and all), nothing while the user types into an editable combo box (the reader echoes
typing), nothing for a label whose `LiveSetting` is `Off` (upstream's default -- a ticking clock would
never stop talking), and a text only when it changes. The status element itself is `aria-live="off"`, so
its implicit politeness does not say a change a second time. In the page, polite announcements wait
250 ms for the UI to settle (at most 1 s), a newer one with the same key replacing the older; the same
text for the same control is not repeated within a second; an assertive one is said at once.
`RaiseLiveRegionChanged` after setting a live label's text, as code written for .NET Framework does, is
one announcement, not two.

`RaiseAutomationNotification` and `RaiseLiveRegionChanged` return true when the mirror took the
announcement, and false -- as before, and as upstream does without an automation client -- everywhere
else, including on desktop backends: there is still no UI Automation bridge for them.

**For test tools.** Locate by role and name, or by `[data-mf-automation-id=…]`. The elements are
`pointer-events: none` -- input still belongs to the canvas -- so click at the element's bounding box
(Playwright: `locator.boundingBox ()` then `page.mouse.click`) rather than with a DOM click.

**Limits, today.** Grids and list views expose what the automation tree does, which is not their rows. A
form hidden with `Hide` (rather than closed) leaves the mirror only at the next repaint. Arrow keys in an
editable combo box are not announced (indistinguishable from typing). Popups hosting arbitrary controls
(`Control.TopLevel`, a date picker's calendar) are mirrored as generic containers of whatever the
automation tree has. **Nothing here has been tried with a real screen reader**; it was verified by
reading the DOM and recording what the live regions said in headless Chrome (`samples/Gallery.Wasm`,
`?check=a11y`: `tools/modal-check.mjs` reads the page after each step, lists the live region output as
`LIVE` lines and flags any `aria-controls`/`-describedby`/`-activedescendant` that names a missing
element; with `--expect`, as CI runs it, each step's roles, states, references and announcements are
compared with the script's `expected` table). Whether VoiceOver, NVDA or JAWS actually speak these as intended -- in particular inside
`role="application"` and through `aria-activedescendant` -- is untested.

**Opting out.** Set the `Majorsilence.Forms.Browser.DisableAccessibilityDom` AppContext switch, for
example in the head's project file:

```xml
<ItemGroup>
  <RuntimeHostConfigurationOption Include="Majorsilence.Forms.Browser.DisableAccessibilityDom" Value="true" />
</ItemGroup>
```

`Majorsilence.Forms.Browser.DisableLiveAnnouncements` turns off only the live region (the DOM mirror stays,
and `RaiseAutomationNotification` returns false again). Per control, announcements follow the WinForms
API: a label is announced only with a `LiveSetting`, and a status bar stops being one -- and being
announced -- with an explicit `AccessibleRole` other than `StatusBar`.

A page whose Content-Security-Policy does not allow `data:` scripts refuses the module; the app runs
unmirrored and logs why to the console.

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

## The GTK 4 backend

`Majorsilence.Forms.Gtk4` implements the seam on GTK 4 through the
[gir.core](https://github.com/gircore/gir.core) bindings (`GirCore.Gtk-4.0`, which pulls
Gdk/Gsk/Pango/Cairo/Gio/GObject/GLib transitively). It is a real-window desktop backend for
Linux first (Wayland/X11), and also compiles and runs on Windows/macOS wherever the GTK 4
runtime is installed.

- `Gtk4PlatformBackend : IPlatformBackend, IWebViewFactory` — initializes GTK
  (`Gtk.Functions.InitCheck`), owns the loop by iterating `GLib.MainContext.Default()` on the calling
  thread (the same shape as the Headless work-queue loop), a `GLib.Functions.TimeoutAdd` timer,
  `Post`/`Invoke` via `GLib.Functions.IdleAdd`, the GDK clipboard (async read pumped against the loop
  we own), and `Gdk.Display` monitor enumeration. `RunModalLoop` nests another iteration loop. The
  `IWebViewFactory` side is below.
- `Gtk4SkiaSurface` — the shared `Gtk.DrawingArea` both hosts draw and receive input through. The
  draw func renders `owner.RenderFrame` straight into a **fresh per-frame** `Cairo.ImageSurface`
  buffer (a persistent one trips `cairo_surface_mark_dirty` once cairo snapshots it as a paint
  source, so it is allocated per paint — the same order of cost as the WPF backend's
  `WriteableBitmap` present), then blits it with `SetSourceSurface`/`Paint`, scaled by
  `1/scaleFactor` for HiDPI. Input arrives through `GestureClick`, `EventControllerMotion`,
  `EventControllerScroll` and `EventControllerKey`; `Gtk4KeyInterop` maps GDK keysyms → `Keys`,
  modifier masks, gesture button numbers and CSS cursor names.
- `Gtk4WindowHost : IWindowBackend` — wraps `Gtk4SkiaSurface` in a `Gtk.Window` and adds chrome,
  geometry and lifecycle. `BeginMoveDrag`/`BeginResizeDrag` call
  `Gdk.Toplevel.BeginMove`/`BeginResize` (Majorsilence.Forms draws its own chrome on Linux);
  `ShowDialog` sets transient-for + modal for a genuine z-ordered dialog.
- `MajorsilenceFormsPresenter` + `ToGtkWidget()` / `ToGtkWindow()` — the embedding direction (see
  [Embedding in a host app](#embedding-in-a-host-app)). The presenter reuses `Gtk4SkiaSurface` and
  implements `IWindowBackend` for a window-less scene, exposing a `Widget` property rather than
  deriving from a GTK widget. `samples/EmbeddingGtk4` shows it inside a host `Gtk.Application`.
- `Gtk4NativeOverlay` — `INativeControlHostBackend` on both hosts: the `Gtk4SkiaSurface` sits inside
  a `Gtk.Overlay`, and a `NativeControlHost`'s widget is added as an overlay child positioned by
  margins. GTK 4 has *no airspace problem* (every widget composites into one render tree), so this is
  simpler here than on Avalonia/Uno/WinForms — the only compromise is that a host scrolled partly out
  of a viewport reflows its native widget into the visible box rather than translating it under a
  clip. The overlay add is deferred one idle turn because `SyncNativeControl` runs inside GTK's
  snapshot pass.
- `Gtk4WebViewHandle` — `IWebViewFactory` via `WebKit.WebView` (WebKitGTK 6.0, `GirCore.WebKit-6.0`).
  `WebKit.WebView` is a `Gtk.Widget`, so it rides the `INativeControlHostBackend` overlay above. Nav
  events map from `load-changed`/`load-failed`; `ExecuteScriptAsync` awaits gir.core's
  `EvaluateJavascriptAsync` (a `Task<JavaScriptCore.Value>`); the JS→host bridge registers a
  `majorsilenceForms` script-message handler. `IsSupported` probes by calling
  `WebKit.Functions.GetMajorVersion()` (a `DllNotFoundException` when WebKitGTK 6.0 isn't installed,
  or off Linux, is caught → unsupported). The `GirCore.WebKit-6.0` package (+ its transitive
  JavaScriptCore / Soup bindings) is a hard dependency of the backend but is all managed — the native
  `libwebkitgtk-6.0` is only `dlopen`'d when a `WebBrowser` is actually created.

**Running it** needs a display session and the GTK 4 native libraries; `samples/Gallery.Gtk4` is the
head:

```
dotnet run --project samples/Gallery.Gtk4                 # full ControlGallery
MF_GTK4_DEMO=1 dotnet run --project samples/Gallery.Gtk4  # tiny render+input smoke form
```

`MF_GTK4_SELFTEST=1` (with `MF_GTK4_DEMO=1`) drives four timer ticks + programmatic clicks and then
`Application.Exit()`s — a non-interactive check that render, the GLib loop, timers and invalidation
are all live. Verified on Wayland: the window shows, the ControlGallery `MainForm` renders, the GLib
timer fires and repaints follow input.

`samples/EmbeddingGtk4` is the host-owned counterpart — a `Gtk.Application` that drops an embedded MF
scene in via `MajorsilenceFormsPresenter` and opens an MF `Form` as a GTK window via `ToGtkWindow()`.
`EMBED_SELFTEST=1` runs a non-interactive check (embedded scene paints, `ToGtkWindow()` presents);
verified on Wayland.

`MF_GTK4_WEBVIEW=1 dotnet run --project samples/Gallery.Gtk4` shows a `WebBrowser`; with
`MF_GTK4_SELFTEST=1` it loads an HTML string, round-trips a script message and an
`ExecuteScriptAsync("document.title")`, then exits. Verified on Wayland against WebKitGTK 6.0:
`IsWebViewFunctional` true, `DocumentCompleted` and `WebMessageReceived` fire, script eval returns.

**What doesn't work** (mostly GTK 4 API removals rather than pending work):

- **No screen-position control.** GTK 4 dropped client-side positioning of top-levels, so
  `Form.Location` is a stored hint the window manager may ignore and `PointToClient`/`PointToScreen`
  are computed against it. `Topmost`, `ShowInTaskbar` and `MaximumSize` round-trip as properties but
  have no enforcing API.
- **`SetIcon(byte[])` is a no-op** — GTK 4 window icons are a themed icon *name*.
- **File/folder pickers return empty**, so the common-dialog fallbacks take over, exactly as on
  Headless. `Gtk.FileDialog` wiring is deferred work.
- **Fractional scaling** uses GTK's integer `scale-factor` (1 or 2); 1.25×/1.5× displays render at
  1× and let the compositor upscale until fractional-scale support is added.
- **Not AOT-analysed** — gir.core's generated bindings are P/Invoke-heavy, out of scope for the AOT
  guarantee (same call as Uno/WinForms/Wpf).

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

`ScrollableControl` applies `ScrollGesture` to `AutoScrollPosition` automatically (content follows the
finger), so `Panel` and its other subclasses pan with no app code. `ListBox` and `TreeView` are *not*
`ScrollableControl` subclasses — they drive their own scrollbar — so each has an explicit
`OnScrollGesture` that pans that scrollbar the same way. Routing gives the hit-tested leaf first
refusal and only bubbles the gesture to a scrollable ancestor when the leaf leaves
`ScrollGestureEventArgs.Handled` clear, so a drag that starts over a non-scrolling child still pans
the list. `LongPress`'s default handler opens `ContextMenu` if one is set, mirroring the existing
right-click behavior in `Control.OnClick`.

`WindowBase.HandleLongPress`/`HandlePinch`/`HandleSwipe`/`HandleScrollGesture` take device pixels from
the backend and convert the point (and a scroll delta / swipe velocity) to logical units at that
boundary, exactly as `HandlePointerPressed` does — routing then hit-tests against logical `Bounds`
consistently at any render scaling, which matters on Android (`RenderScaling` ~2.6).

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
by the Avalonia, Uno, WinForms and GTK 4 backends, absent on Headless. It lets a `NativeControlHost`
control reserve a rectangle that the backend fills with a real toolkit element (an Avalonia
`Control`, an Uno `UIElement`, a `Gtk.Widget`) overlaid on top of the Skia surface, kept aligned to
the placeholder's bounds, clip and visibility. See [`native-interop.md`](native-interop.md) for how
to use it, its airspace limits, why native handles can't be faked, and why video is usually better
done with frame callbacks drawn into Skia than with a hosted native surface. The GTK 4 backend is the
exception to the airspace limits — GTK composites every widget into one render tree, so a hosted
`Gtk.Widget` clips and blends correctly with no separate native surface (see
[The GTK 4 backend](#the-gtk-4-backend)).

## In-process audio

`IAudioBackend` is a fourth optional capability. `Media.SoundPlayer` and `Media.SystemSounds` try
`Platform.Backend as IAudioBackend` before falling back to `Media.NativeAudio`'s desktop path of spawning
the OS's own playback utility — and fall back to it too whenever the backend answers `null`, exactly as
if the interface were not implemented at all, so a backend can implement it everywhere and genuinely play
only on some rows. The Avalonia backend does this: real on Android (`MediaPlayer`) and iOS
(`AVAudioPlayer`/`AudioToolbox.SystemSound`), `null` everywhere else. See `COMPATIBILITY_MATRIX.md`'s
`SoundPlayer`/`SystemSounds` entry for what each platform actually does, and
`tests/Majorsilence.Forms.Tests/MobileAudioTests.cs` for how `HeadlessRenderer.AudioIsSupported` (false by
default, so the rest of the suite is unaffected) proves the routing without a device.

`IAudioBackend.PlayTrack` is the same interface's third member, backing `Media.AudioPlayer` (register item
F9) rather than `SoundPlayer`/`SystemSounds`. It has no `NativeAudio` fallback — a volume, a caller-chosen
`Media.AudioUsage` (which platform audio stream/session a track plays through) and a real completion event
do not map onto spawning a short-lived OS utility process the way `PlayFile` does, so `AudioPlayer` is real
only where `PlayTrack` is: Android and iOS. `AudioPlayer.IsSupported` (`Platform.Backend is IAudioBackend`)
is how a caller checks that ahead of committing to, say, a looping-alarm UX — unlike `SoundPlayer`'s silent
degrade. `AudioUsage.Alarm` is the case the whole class exists for: Android's `USAGE_ALARM` (its own volume
stream, audible with media volume down) and an iOS `Playback` session (overrides the silent switch); the
other three usages stay on the same conservative streams/sessions `PlayFile`/`PlaySystemSound` already use.
See `tests/Majorsilence.Forms.Tests/AudioPlayerTests.cs` for how `HeadlessRenderer.AudioTrackRequests` /
`ActiveAudioTrackCount` / `CompleteNextAudioTrack` prove volume clamping, overlapping concurrent `Play`
calls, `Stop` disposing every track an instance started (and no other instance's), and `Completed` firing
only for a natural finish — all without a device.

## Application lifecycle (Avalonia-specific, register item F10)

Unlike the seams above, `Application.Suspended`/`Resumed` and the single-view host's `Form.Activated`/
`Deactivate` are not a new `IPlatformBackend` capability every backend can opt into — every *other* backend
(WinForms, WPF, GTK 4, Uno, Headless) already fires `Activated`/`Deactivate` correctly from its own real
window-activation signal, and none of them has an OS-level "backgrounded" concept to raise `Suspended`/
`Resumed` from at all. The gap was single-view (Android, iOS, browser): nothing there ever called
`WindowBase.OnBackendActivated`/`OnBackendDeactivated`, and nothing raised `Suspended`/`Resumed` anywhere.

`AvaloniaPlatformBackend.HookApplicationLifecycle` (called once, idempotently, from `Initialize`/
`InitializeAsync`) is entirely internal to this one backend. It reaches Avalonia's `IActivatableLifetime`
through `Application.Current.TryGetFeature (typeof (IActivatableLifetime))` — **not**
`Application.Current.ApplicationLifetime`, the pattern `IWebViewFactory`/`IReducedMotionSource` both use:
on Android, `ApplicationLifetime` resolves to `Avalonia.Android.ApplicationLifetime`, which does not
implement `IActivatableLifetime` at all (confirmed by inspecting the shipped assembly directly, not
assumed). Filtered to `ActivationKind.Background`, it forwards to `Application.RaiseSuspended`/
`RaiseResumed` and, on the single-view root host only, to the owning `WindowBase`'s own
`OnBackendActivated`/`OnBackendDeactivated`. See `COMPATIBILITY_MATRIX.md`'s "Application lifecycle" entry
for the full finding and how it is verified.

## Back button (Avalonia-specific, register item F11)

`WindowBase.BackRequested`/`RaiseBackRequested` is on the shared base (not `Form` alone) specifically so a
`PopupWindow` has it too — the acceptance criterion is "closes a sheet without leaving the app", and a sheet
is exactly what `PopupWindow` already models (a dropdown, a context menu, a filter grid). Only Android/iOS
have a real platform back button/gesture to raise it from; every other backend leaves it unraised, same as
`Suspended`/`Resumed` above.

`AvaloniaPlatformBackend.RaiseBackRequested` is **not** automatic the way `HookApplicationLifecycle` is.
`Avalonia.Android.AvaloniaActivity.BackRequested` is declared directly on the Activity class (confirmed by
inspecting the shipped assembly the same way `HookApplicationLifecycle`'s finding was), and nothing in this
assembly can discover "the current Activity" generically — the type that does track it,
`Avalonia.Android.Platform.AndroidActivatableLifetime`, is `internal` in a different assembly. A host app's
own `MainActivity` (already required to subclass `AvaloniaMainActivity` and carry an AppCompat theme, #288)
forwards its own `BackRequested` here instead, one line, the same shape `Application.RunAndroid` already
requires. `dotnet new majorsilenceforms --IncludeAndroid` generates a `MainActivity` that already does this (pinned by
`AndroidTemplateThemeTests`), and `samples/Gallery.Android/MainActivity.cs` shows it alongside the other activity hooks. `RaiseBackRequested` prefers
`Application.ActivePopupWindow` (matching `Application.ScheduleClosePopupsOnDeactivate`'s own check for
"which window is really active right now") so an open sheet gets the back-press before the main screen.

A second, unrelated finding hit while adding this: the new `WindowBase` method that raises `BackRequested`
had to be named `RaiseBackRequested`, not `OnBackendBackRequested` (which is otherwise the established
naming for a method a backend calls into core to report something) — `OnBackendActivated`/
`OnBackendDeactivated` above are the precedent. `tests/Majorsilence.Forms.Tests/StubSurfaceScanner.cs`'s
`NoNewUnraisedEvents` gate treats a public method as a safe "this is definitely called from somewhere"
entry point unless its name starts with `On`, regardless of visibility — `On`-prefixed methods are treated
as an internal framework convention (a backend overriding a hook), not a cross-assembly entry point, so an
`On`-named raiser whose only real caller lives in a different assembly (as this one's does, from
`Majorsilence.Forms.Avalonia`) fails the gate even when made `public`. Making `Application.RaiseSuspended`/
`RaiseResumed` (F10, above) public happened to satisfy the gate already because they were never `On`-named
to begin with. Renaming to `RaiseBackRequested` (matching `RaiseIdle`/`RaiseSuspended`/`RaiseResumed`) fixed
it with no other change. See `COMPATIBILITY_MATRIX.md`'s "Back button" entry for how this is verified.

## Haptics (register item F13)

Unlike every backend seam above, `IHapticsBackend` is deliberately **not** implemented everywhere with a
null/no-op body the way `IAudioBackend` is: it is only declared in `AvaloniaPlatformBackend`'s own base
list under `#if ANDROID || IOS`. The register item's own acceptance criterion is "`IsSupported` false on
Headless" (unlike F8/F9's audio, which is real and test-hooked even under Headless), and haptics has no
desktop/browser equivalent worth representing as "supported but does nothing" — a machine with no
vibration motor is exactly the same as a backend that never heard of `IHapticsBackend` at all. This means
`Haptics.IsSupported` (`Backend is IHapticsBackend`) is `false` everywhere but Android and iOS with a single
check, no separate per-row fallback needed.

`AndroidHapticsBackend` drives `Vibrator` through `VibrationEffect` (API 26+) so the OS, not this code,
picks the actual waveform/amplitude for `Tap`/`Impact`'s "click"/"heavy click" shapes.
`VibrationEffect.CreatePredefined` needs API 29+; between this project's floor of API 24 and that, every
member falls back to a plain one-shot buzz of its own length rather than a second, cruder code path — still
real feedback, just not the platform's distinct predefined shapes. Below API 26 there is no
`VibrationEffect` at all, so every member is a no-op there. The `android.permission.VIBRATE` manifest entry
(a normal, not dangerous, permission — no runtime request needed) is declared once, as an assembly-level
`[assembly: Android.App.UsesPermission (...)]` attribute on `Majorsilence.Forms.Avalonia` itself, so it
merges into any consuming app's manifest automatically — confirmed by inspecting the built
`Gallery.Android` APK's own merged manifest, not assumed. `IosHapticsBackend` uses
`UISelectionFeedbackGenerator`/`UIImpactFeedbackGenerator` (UIKit, iOS 10+) for `Tap`/`Impact`; `Vibrate`
has no public UIKit API taking an explicit duration, so it triggers the same fixed-length system-wide buzz
iOS itself uses for a phone call or an alert (`AudioServicesPlaySystemSound`'s `kSystemSoundID_Vibrate`,
reached the same "known, stable numeric identifier" way `IosAudioBackend`'s own `SystemSoundId` already
documents) rather than a caller-chosen length.

A real iOS-only build bug was caught by CI, not guessed: `UIImpactFeedbackGenerator (UIImpactFeedbackStyle)`
is obsoleted from iOS 17.5 (`CA1422`) in favour of `UIFeedbackGenerator.GetFeedbackGenerator`, a view-scoped
factory needing a `UIView` to attach to. This backend has no view reference to thread through for a feature
this minor, and the old constructor still works on every iOS version, only deprecated, not removed, so the
warning is suppressed at that one call site rather than the API avoided.

A real Android-only build bug was caught here, not guessed: the platform-compat analyzer (`CA1416`) flagged
`VibrationEffect.EffectClick`/`EffectHeavyClick` (API 29+ members) as reachable from this project's API 24
floor, even though the call was already behind an `OperatingSystem.IsAndroidVersionAtLeast (29)` check —
the analyzer was tripped by the *caller* (`Tap`/`Impact`) passing the field's value as a plain `int`
argument to a shared helper, outside the guarded branch, not by the eventual `Vibrator.Vibrate` call itself.
Fixed by passing an enum discriminator instead and resolving the actual `VibrationEffect.EffectXxx` field
only inside the version-guarded branch. See `COMPATIBILITY_MATRIX.md`'s "Haptics" entry for the full
verification story — the acceptance criterion specifically needs a real phone (Android emulators have no
vibrator), which this session did not have.

## Local notifications (register item F14, Android half)

`INotificationBackend` takes each `NotificationChannel`/`LocalNotification` field individually (`RegisterChannel (string id, string name, string? description, NotificationImportance importance, bool sound)`, `Show (int id, string channelId, string title, string text, bool ongoing, bool fullScreen)`) rather than the model object itself — the same shape `IAudioBackend.PlayTrack` takes an `AudioUsage`/`volume`/`loop` rather than an `AudioPlayer`. This is not just style: `LocalNotifications.RegisterChannel`/`Show` read every field themselves before forwarding, which is what keeps each field off `StoredOnlyPropertyBaselineTests` — a real gate failure hit while building this, not guessed. That gate scans only `Majorsilence.Forms.dll` for a property's readers; a field read only inside `AndroidNotificationBackend` (a different assembly, and `#if ANDROID`-gated even there) is invisible to it, so passing the whole object through and letting the backend read it directly would have left every settable field looking "stored, never read" from the gate's point of view.

Two capabilities have no analogue among F8–F13's backend members: `AndroidNotificationBackend.RequestPermission` needs a live `Activity` (`ActivityCompat.RequestPermissions`, API 33+ only — below that, `IsPermissionGranted` is unconditionally `true`, since no runtime permission exists to deny), and a notification tap needs the host's own `Intent` read back. Both extend the "host app forwards to the framework" idiom F11's `BackRequested` established, rather than inventing a second mechanism:

- `AvaloniaPlatformBackend.RegisterAndroidActivity (Activity)` — called once, from `MainActivity.OnCreate`, so `RequestPermission` has something to call `ActivityCompat.RequestPermissions` on. Nothing in this assembly can discover "the current Activity" generically, the same gap `RaiseBackRequested`'s own remarks document.
- `AvaloniaPlatformBackend.ReportAndroidIntent (Intent?)` — called from both `MainActivity.OnCreate` and `OnNewIntent` (the Activity is `LaunchMode.SingleTop`, so a tap while already running arrives via `OnNewIntent`, not a fresh `OnCreate`). It recognises `AndroidNotificationBackend`'s own extra key and raises `LocalNotifications.Tapped` — the host never needs to know that key itself, unlike `RegisterAndroidActivity`, which is a plain one-line call.
- `AvaloniaPlatformBackend.ReportNotificationPermissionResult ()` — called from `MainActivity.OnRequestPermissionsResult`. It does not need the result itself: `IsPermissionGranted` always re-reads `NotificationManagerCompat.AreNotificationsEnabled ()` fresh (which also covers the user disabling notifications from system settings, not just the runtime grant), so this only needs to trigger `LocalNotifications.PermissionChanged`.

The notification's tap `PendingIntent` targets `PackageManager.GetLaunchIntentForPackage` — the app's own launcher activity, found the same generic, no-per-app-registration way an Android "reopen my app" shortcut always does — with the tapped id as an extra and `FLAGS: NewTask | SingleTop | ClearTop` so a running instance receives it via `OnNewIntent`.

A small icon is mandatory -- `NotificationManager.notify` throws `IllegalArgumentException` without one -- and the framework does not own an icon resource in the consuming app's namespace, so `context.ApplicationInfo.Icon` (the app's own manifest-declared launcher icon, read generically at runtime) is tried first. A real CI failure, not guessed, found that fallback alone is not enough: neither this repo's `Gallery.Android` sample nor `tools/Majorsilence.Forms.Templates`' Android project head declares an app icon at all (no `<application android:icon="...">`, no mipmap resources), so `ApplicationInfo.Icon` is `0` for both -- and, by the same gap, for any real app built from that template until it adds its own. `Show` now falls back a second step, to `Resource.Drawable.IcDialogInfo` (a generic icon bundled in every AOSP framework, needing no app-side resource), so a missing app icon degrades to a generic-looking notification rather than the notification silently never appearing at all.

A real `CA1416`/nullable-reference pass, not guessed: `PendingIntentFlags.Immutable` needs API 23+ (this project's library floor is API 21, not `Gallery.Android`'s own 24 — see the Haptics entry above for the same distinction), so it is only added when `OperatingSystem.IsAndroidVersionAtLeast (23)` — every `PendingIntent` was implicitly mutable before that flag existed, so omitting it below API 23 is correct behaviour, not just a version-guard formality. Separately, AndroidX's Java binding declares every `NotificationChannelCompat.Builder`/`NotificationCompat.Builder` `Set*`/`Build` return as nullable (Java has no non-null annotation the binding can trust, even though the real contract always returns `this`); calling each step on the one, never-reassigned builder local — rather than fluently chaining — avoided a dozen-plus spurious nullable warnings without a dozen-plus null-forgiving operators.

`android.permission.POST_NOTIFICATIONS` (a dangerous, API 33+ permission) and `USE_FULL_SCREEN_INTENT` (a normal, declare-only permission -- without it, Android 14+ silently strips `SetFullScreenIntent` rather than throwing, confirmed by a real CI run, not assumed) both ship as assembly-level `[assembly: Android.App.UsesPermission (...)]` attributes on `Majorsilence.Forms.Avalonia`, the same mechanism F13's `VIBRATE` permission uses — confirmed by inspecting the built `Gallery.Android` APK's own merged manifest. See `COMPATIBILITY_MATRIX.md`'s "Local notifications" entry for the full real-device verification story, including the missing-icon and full-screen-intent findings CI itself surfaced, what `android-smoke-test.sh` independently confirms via `dumpsys notification` and a replayed tap intent, and what still needs iOS/desktop follow-up work.

## Keep screen awake (register item F12)

`Application.KeepScreenAwake` is the first capability seam with something real to do on **every** row but browser — Android, iOS and all three desktop OSes — unlike every seam above it (Haptics real only on mobile; local notifications real only on Android so far). `IKeepScreenAwakeBackend` is declared in `AvaloniaPlatformBackend`'s base list for every row but `BROWSER` (see the class declaration), and `AndroidKeepAwakeBackend`/the iOS branch/`DesktopKeepAwake` split the same way `IAudioBackend`'s Android/iOS/desktop members already do.

Desktop is the new part: `DesktopKeepAwake` (in the core `Majorsilence.Forms` project, not a backend assembly) is a direct sibling of `DesktopReducedMotion` (register item F7) — the same `Dispatch (isWindows, isMacOS, isLinux, ...)` shape, told apart at runtime via `OperatingSystemCompat`, injectable for testing without depending on which OS the test itself runs on. A single desktop build reaches all three:

- **Windows**: `SetThreadExecutionState` (`kernel32.dll`), the documented Win32 API — `ES_CONTINUOUS` is required on every call (it is not sticky); passing it alone on disable clears only this process's own flags.
- **macOS**: an IOKit power assertion (`IOPMAssertionCreateWithName`/`IOPMAssertionRelease`), reached via raw P/Invoke to `CoreFoundation.framework`/`IOKit.framework` rather than a Xamarin.Mac/.NET-for-macOS binding — the same reasoning `DesktopReducedMotion`'s own macOS section already documents for going straight to `libobjc` instead. `PreventUserIdleDisplaySleep` is the assertion type: it keeps the *display* on without also preventing the system from idling to a low-power state the way `PreventUserIdleSystemSleep` would, which is what a bedside app actually wants.
- **Linux**: `systemd-inhibit --what=idle:sleep ... sleep infinity`, held for exactly as long as that placeholder process runs (`systemd-inhibit` execs its wrapped command directly, so the `Process.Start`-returned PID *is* the held inhibitor lock — no D-Bus reply/cookie parsing needed). The same "shell out to a CLI tool wrapping the real IPC mechanism" idiom `DesktopReducedMotion`'s own `gsettings` call already uses, rather than a hand-rolled D-Bus client.

Unlike Haptics/local notifications, Headless implements `IKeepScreenAwakeBackend` for real (a plain settable field, the same shape `HeadlessPlatformBackend.PrefersReducedMotion` already uses) rather than answering unsupported: this is a plain stateful property an app's own code turns on and off (a bedside/status-display screen), so a fake here is exactly what a view-model test needs to assert against — matching the issue's own "fake-backend tests" wording, not F13/F14's "`IsSupported` false on Headless" (this has no `IsSupported` at all; reading or setting it on a backend that does not implement the capability degrades to the same conservative false/no-op every other seam here uses).

**Verification, three different ways for three different rows.** Android: `MainActivity.RunKeepScreenAwakeSmokeTest` sets `KeepScreenAwake` true then false on the real Activity `RegisterAndroidActivity` already registers (register item F14), confirmed via `android-smoke-test.sh`'s `F12_KEEPAWAKE_SMOKE` log line. Linux: `KeepScreenAwakeTests.The_real_desktop_set_never_throws_and_toggles_IsEnabled_regardless_of_host` calls the real `Set (true)`/`Set (false)` (not the injectable `Dispatch`) directly, genuinely spawning and killing a real `systemd-inhibit` child process on whatever Linux CI runner executes it. Windows and macOS: unlike `DesktopReducedMotion`'s own P/Invoke, which has run on this project's real Windows/macOS CI build jobs ever since F7 merged without a reported failure, this specific P/Invoke is new as of this register item — the *same* test above also runs for real on CI's `build (windows-latest)`/`build (macos-latest)` jobs (which run the full test suite, not just a compile check), so a wrong `DllImport` signature or constant value there would show up as an actual test failure, not just "written from the documented API, not run." iOS: written from the documented `UIApplication.IdleTimerDisabled` API, compiles clean via CI's `ios`/`sample-ios` jobs, but not run on a simulator or device — the same honest gap F13/F14 already have for iOS.

## Native item picker for `ComboBox` (#438)

A `ComboBox` normally opens its list as a small `PopupWindow` under the control. On a phone that popup is hard to hit
with a finger, and the single-view host never showed it, so no `ComboBox` could be changed on Android.
`Backends.IItemPickerBackend` is the optional seam a backend implements when its platform has its own way to pick one
item from a list: `PrefersNativeItemPicker` says whether it should be used, and `ShowItemPicker (title, items,
selectedIndex, completed)` shows it and answers later through `completed` with the chosen index, or -1 when it was
dismissed. The core discovers it with `Platform.Backend as IItemPickerBackend`, like `IKeepScreenAwakeBackend` and
`IHapticsBackend`.

- **Android**: `AndroidItemPickerBackend` shows an `AlertDialog` of single-choice items with the current one marked, on
  the registered `CurrentAndroidActivity`. Back or a tap outside answers -1. `AvaloniaPlatformBackend` declares the
  interface only under `ANDROID`.
- **iOS and the browser**: not implemented yet, so a `ComboBox` keeps its popup there.
- **Desktop**: unchanged; the popup stays.
- **Headless**: `HeadlessPlatformBackend.ItemPicker` is a hook a test assigns to stand in for a mobile platform and to
  answer when it chooses; null (the default) means no native picker.

Only a `DropDownList` combo with items uses it: an editable combo's text box is part of the control, and a dialog
would hide it. `DroppedDown` is true while the picker shows, a close request from losing focus to the dialog is
ignored (the dialog closes itself), and a choice is a user commit: `SelectedIndexChanged` and
`SelectionChangeCommitted` fire, then `DropDownClosed`. A dismissal fires only `DropDownClosed`.

## SecureStorage is not part of this seam (register item F16)

`Majorsilence.Forms.Essentials.SecureStorage` deliberately does not go through `Backends.Platform`/`IPlatformBackend` the way
Haptics, local notifications and `Application.KeepScreenAwake` above do: which OS credential store exists has nothing to do
with which UI backend (Avalonia, WinForms, Uno) is active, so it picks its own `ISecureStorageBackend` per target framework
directly instead. `Majorsilence.Forms.Essentials` has no `ProjectReference` to this project at all. See
`COMPATIBILITY_MATRIX.md`'s "SecureStorage" entry for the full per-platform detail and verification.

## Speech is not part of this seam either (register item F15)

`Majorsilence.Forms.Essentials.Speech` picks its own `ISpeechBackend` per target framework the same way `SecureStorage`
does, for the same reason: which speech engine exists has nothing to do with which UI backend is active. See
`COMPATIBILITY_MATRIX.md`'s "Speech" entry for the full per-platform detail and verification.

### Adding another backend

A new backend is a new assembly referencing `Majorsilence.Forms` (core) + the toolkit, implementing the two
interfaces — mirror the Avalonia/Headless/Uno trio: drive the dispatcher + lifecycle in the
`IPlatformBackend`, and present a Skia surface (calling `owner.RenderFrame`) + translate input
(`owner.Handle*`) in the `IWindowBackend`. Add an `[InternalsVisibleTo]` entry in the core `.csproj`.
