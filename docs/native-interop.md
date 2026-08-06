# Native interop: handles, hosted native content, and video

This document answers two questions that turn out to be the same question:

- *"How do I put a native thing — a video surface, a map view, a webview — inside a
  Majorsilence.Forms control?"*
- *"How do I get an `HWND` for a control, so I can hand it to a library that wants one?"*

The short answer to the second is **you can't, and you shouldn't try to fake one** — but you almost
never need it, because the first question has a real answer:
[`NativeControlHost`](#the-seam-nativecontrolhost).

For hosting a Majorsilence.Forms window *inside* a real WinForms app (the opposite direction), see
[`winforms-interop.md`](winforms-interop.md).

## Why handles aren't real here

Majorsilence.Forms does all of its own drawing into a single Skia surface (see
[`backends.md`](backends.md)). Both real backends follow the same model as the toolkits underneath
them: **one native OS window per top-level window, and everything inside it is drawn, not composed
from native child windows.** A `Button` here is paint operations on a canvas. There is no `HWND`
behind it because there is no OS object behind it.

That is why these return what they do:

| Member | Value | Why |
|---|---|---|
| `Control.Handle` | `IntPtr.Zero` | There is no per-control OS window to report. Same for `ImageList.Handle`, `TreeNode.Handle`, `Cursor.Handle`, `TaskDialog.Handle`. |
| `WindowBase.Handle` | An opaque nonzero token (`GetHashCode() \| 1`) | **Not an `HWND`.** WinForms code routinely reads `.Handle` to force handle creation before `Invoke`; returning zero breaks that idiom. This value is meaningful only inside managed code. |
| `WindowBase.PlatformHandle` | The real native handle, or `IntPtr.Zero` | The genuine article, via `IWindowBackend.TryGetPlatformHandle()`. Implemented by the Avalonia backend (`HWND` on Windows, `NSWindow` on macOS, `XID` on X11). Currently **zero on Uno and Headless** — see [Known gaps](#known-gaps). |

### The rule about faking

A fabricated handle is safe **only while it round-trips through managed code that you control** —
which is exactly what `WindowBase.Handle` does and all it is for. It stops being safe the moment it
crosses into native code.

A native library does not merely store the handle you give it. LibVLC's
`libvlc_media_player_set_hwnd`, mpv's `--wid`, and GStreamer's `GstVideoOverlay.set_window_handle`
all pass it on to the OS — `SetParent`, `CreateWindowEx(…, parent, …)`, `GetClientRect`,
`SetWindowPos`, or `XReparentWindow` on X11. Handing those a made-up number gets you one of two
outcomes, and the second is worse than the first:

1. The call fails, and you get a black rectangle or a crash inside the native library.
2. The call *succeeds against a window that belongs to something else*. `HWND`s are handle-table
   indices, not pointers, and small integers are live values. A hash code is precisely the shape of
   number that can collide with a real window.

So: never synthesize a handle for anything that will reach the OS. Use one of the two routes below
instead.

## The seam: `NativeControlHost`

`Majorsilence.Forms.NativeControlHost` is a `Control` that **reserves a rectangle** for a native
element of the underlying toolkit. It paints nothing itself (its background is transparent); the
backend overlays the real native element on top of the Skia surface and keeps it aligned to the
placeholder's bounds. This is the "airspace" interop model — native elements cannot be composited
into the Skia buffer, so they are positioned over it.

Assign the toolkit object to `NativeControl`:

```csharp
var host = new NativeControlHost { Dock = DockStyle.Fill };
host.NativeControl = someAvaloniaControl;   // or an Uno UIElement
panel.Controls.Add (host);
```

The host tracks bounds, the intersected clip region of every scrolling/clipping ancestor, and
effective visibility along the whole parent chain, forwarding all three to the backend via
`INativeControlHostBackend.AttachNativeControl` / `UpdateNativeControl` / `DetachNativeControl`.
Re-sync happens automatically on paint and on visibility changes; call `SyncNativeControl()` yourself
if you move or resize the host outside the normal paint cycle.

### What each backend accepts

| Backend | Expected type of `NativeControl` | Behaviour |
|---|---|---|
| Avalonia (window host, single-view host, presenter) | `Avalonia.Controls.Control` | Added to an overlay `Canvas` above the Skia surface. |
| Uno (window host, presenter) | `Microsoft.UI.Xaml.UIElement` | Added to the root `Canvas`/panel above the `SKXamlCanvas`. |
| Headless | — | Does not implement `INativeControlHostBackend`. The host renders as an empty placeholder. |

> **Assigning the wrong type fails silently.** Both backends type-check `nativeControl` and simply
> `return` if it doesn't match, and the subsequent `UpdateNativeControl` finds no overlay entry and
> returns too. Nothing throws, nothing logs, and nothing appears on screen. If your native content is
> invisible, check the type first — an Avalonia `Control` given to the Uno backend, or vice versa,
> produces exactly this.

### Airspace limitations

These apply to *any* hosted native element, on either backend, and are inherent to the model rather
than bugs to be fixed:

- The native element draws **above** the entire Majorsilence.Forms scene. It cannot be z-ordered
  between Majorsilence controls, and any Majorsilence control that visually overlaps it — a dropdown,
  a tooltip, a `ContextMenuStrip` — will be painted *underneath* it.
- Clipping is rectangular only, applied by the backend from the `clipBounds` the host computes.
  Rotation, non-rectangular clips and opacity from the Majorsilence side do not apply to it.
- Scrolling works but is not free: the overlay is repositioned per sync, so it can visibly lag the
  Skia content during a fast scroll. Keep hosted elements in non-scrolling areas where you can.

## Route A — hosting real native content

Use this when you need something that genuinely must be an OS window: a GPU-accelerated video
surface, a native map/CAD view, a browser engine.

The important point is that you are **not faking a handle — you are creating a real one** and giving
the native library that. `NativeControlHost.NativeControl` takes a toolkit object, not a handle, so
there is a wrapper step in between. `AvaloniaWebViewHandle` (in `Majorsilence.Forms.Avalonia`) is the
worked example already in the tree: it wraps an `Avalonia.Controls.NativeWebView` and exposes it as
`IWebViewHandle.NativeControl`.

**Avalonia.** Subclass `Avalonia.Controls.NativeControlHost` and override
`CreateNativeControlCore (IPlatformHandle parent)`, which returns a real `IPlatformHandle` — an
`HWND` on Windows, an `XID` on X11, an `NSView` on macOS. Create your child window there, hand its
handle to the player, and release it in `DestroyNativeControlCore`. Assign the resulting Avalonia
control to `Majorsilence.Forms.NativeControlHost.NativeControl`.

**Uno.** `Uno.UI.NativeElementHosting` exposes `Win32NativeWindow(IntPtr Hwnd)` and
`X11NativeWindow(IntPtr WindowId)` — public wrappers around a real handle you created — plus
`BrowserHtmlElement` for WASM. These are consumed through Uno's native-element-hosting path (set one
as the `Content` of a `ContentPresenter`, and give that `ContentPresenter` to `NativeControl`); the
hosting extension itself is internal to Uno, so this rests on the public wrapper contract.

**Platform reach.** Realistically Windows and X11. Wayland needs subsurfaces, macOS gives you an
`NSView` rather than anything `HWND`-shaped, and WASM/Android/iOS give you no usable window handle at
all. A feature built this way will not run everywhere the rest of the framework does.

## Route B — video without a handle (recommended)

Most video libraries can be told to hand you **decoded frames** instead of taking a window. That
removes the handle from the problem entirely:

| Library | Callback API |
|---|---|
| LibVLC | `libvlc_video_set_callbacks` + `libvlc_video_set_format` (the `vmem` output) |
| mpv | the render API in software mode |
| GStreamer | an `appsink` element |
| FFmpeg | you already own the frames |

You receive a pixel buffer, copy it into an `SKBitmap`, and draw it in `OnPaint` like any other
control. Exact binding signatures differ per library and per wrapper package; the Majorsilence side
looks like this regardless:

```csharp
public class VideoView : Control
{
    private SKBitmap? frame;

    // Called from the decoder's own callback thread with a freshly decoded BGRA frame.
    // Ask the library for a pitch of width * 4 when you set the output format, so the
    // incoming buffer is tightly packed and matches SKBitmap's row layout.
    public void PresentFrame (ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (frame is null || frame.Width != width || frame.Height != height) {
            frame?.Dispose ();
            frame = new SKBitmap (width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        }

        // One bulk copy, not per-pixel: SKBitmap.SetPixel is a P/Invoke per call, which for a
        // megapixel frame costs seconds rather than milliseconds. DeviceIndependentBitmap.Decode
        // hit exactly this and measured the fix at 34x.
        bgra.CopyTo (frame.GetPixelSpan ());
        frame.NotifyPixelsChanged ();

        // Invalidate() does NOT marshal -- it walks to the window and marks it dirty on whatever
        // thread you call it from. Hop to the UI thread explicitly.
        BeginInvoke (Invalidate);
    }

    protected override void OnPaint (PaintEventArgs e)
    {
        base.OnPaint (e);
        if (frame is not null)
            e.Canvas.DrawBitmap (frame, new SKRect (0, 0, Width, Height));
    }
}
```

This sketch uses one bitmap, so a paint can in principle read it while the decoder thread is writing
the next frame — visible as tearing under load, not as a crash. Swapping between two bitmaps (write
to the back one, publish it with a single reference assignment) is the usual fix and is worth doing
for anything beyond a demo.

**Why this is the better default here.** The frame becomes part of the Skia scene, so z-order,
clipping, scrolling, opacity and transforms all behave like they do for every other control — none of
the airspace caveats apply. It works on *every* backend, including Headless and WASM, which means it
is also testable: you can assert on rendered pixels the same way the metafile playback tests do.

The cost is a per-frame CPU copy and software decode. For typical playback that is acceptable; if you
need hardware decode kept on the GPU, that is the case for Route A.

## Choosing

| | Route A (native host) | Route B (frame callbacks) |
|---|---|---|
| Composites with Majorsilence content | No — draws on top | Yes |
| Backends | Avalonia, Uno | All, including Headless |
| Platforms | Windows, X11 realistically | Everywhere |
| GPU decode path | Yes | No (software decode + copy) |
| Testable in CI | No | Yes |
| Needs a real OS handle | Yes (create one — never fake) | No |

Default to **B** for video. Reach for **A** when you need a browser engine, hardware decode, or a
third-party native view that only knows how to draw into a window.

## Known gaps

- **Uno does not implement `IWindowBackend.TryGetPlatformHandle`.** Only the Avalonia backend does,
  so `WindowBase.PlatformHandle` is `IntPtr.Zero` on Uno even though
  `Uno.UI.NativeElementHosting.Win32NativeWindow.Hwnd` and `X11NativeWindow.WindowId` are public and
  would close it. This also means platform accessibility bridges can't attach on Uno.
- **No media/video control ships in the framework.** Both routes above are integration guidance, not
  a `VideoView` you can instantiate.
- **Route A's Uno path is documented from the public API surface, not from a running app.** The
  Avalonia path is exercised in the tree by `AvaloniaWebViewHandle`; the Uno equivalent is not.
