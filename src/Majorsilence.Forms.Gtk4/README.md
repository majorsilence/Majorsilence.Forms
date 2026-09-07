# Majorsilence.Forms.Gtk4

A **GTK 4 platform backend** for [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms),
built on the [gir.core](https://github.com/gircore/gir.core) bindings.

Majorsilence.Forms does all of its own drawing with SkiaSharp; this backend is only the *host* — it
creates a real `Gtk.Window`, runs the GLib main loop, delivers input, and presents the Skia surface
through a `Gtk.DrawingArea`.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Gtk4
```

You also need GTK 4 installed on the machine (`libgtk-4-1` on Debian/Ubuntu, `gtk4` on Fedora/Arch,
`brew install gtk4` on macOS, the GTK runtime on Windows).

## Use it

Unlike the default (Avalonia) backend, the GTK 4 backend is selected explicitly:

```csharp
using Majorsilence.Forms;
using Majorsilence.Forms.Gtk4;

Gtk4Application.Use ();                 // installs Gtk4PlatformBackend
Application.Run (new MainForm ());      // GLib main loop
```

## What works

- Real `Gtk.Window` per top-level `Form`, borderless windows for popups (menus, combo dropdowns).
- Skia frame rendered straight into a Cairo image surface each paint.
- Mouse (click / move / wheel), keyboard, and typed-text input via GTK event controllers.
- GLib timers back `Majorsilence.Forms.Timer`; `Post`/`Invoke` marshal onto the GLib loop.
- Clipboard text, multi-monitor `Screen` enumeration.
- Custom-chrome window move/resize drags via `Gdk.Toplevel.BeginMove`/`BeginResize`.
- `ShowDialog` gets a real transient-for / modal window relationship.
- `NativeControlHost` — a real `Gtk.Widget` overlaid *inside* an MF scene (`INativeControlHostBackend`).
  GTK 4 composites every widget into one render tree, so there is no airspace problem.

## Embed in a host GTK app

The reverse direction — an existing `Gtk.Application` that wants to use MF objects as its own:

```csharp
using Majorsilence.Forms.Gtk4;

// MF Control  → GTK widget
Gtk.Widget widget = myMfControl.ToGtkWidget ();          // or: new MajorsilenceFormsPresenter { Content = myMfControl }
someGtkBox.Append (widget);

// MF Form → Gtk.Window (created eagerly in the Form's constructor)
Gtk.Window window = myForm.ToGtkWindow ();
window.SetTransientFor (hostWindow);
window.Present ();
```

`MajorsilenceFormsPresenter` exposes a `Widget` property rather than deriving from a GTK widget
(gir.core's GObject subclassing needs an extra integration package and a type-registration call). See
`samples/EmbeddingGtk4`.

## Known limits (v1)

- **No screen-position control.** GTK 4 removed client-side positioning of top-levels, so
  `Form.Location` is a stored value the window manager may ignore, and `PointToScreen`/`PointToClient`
  assume it.
- **`Topmost`, `ShowInTaskbar`, `MaximumSize`** round-trip as properties but GTK 4 has no API to
  enforce them.
- **Window icons** come from a themed icon name in GTK 4, so `SetIcon(byte[])` is a no-op.
- **File pickers** (`OpenFileDialog` etc.) return empty — the compat dialogs fall back, as on Headless.
- **Fractional display scaling** uses the integer scale factor GTK reports (1 or 2); text is slightly
  soft at 1.25×/1.5× until fractional-scale support lands.
- Runs only where a display server (X11/Wayland) is available — use
  `Majorsilence.Forms.Headless` for offscreen/CI rendering.

## Links

- [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)

Licensed under the MIT License.
