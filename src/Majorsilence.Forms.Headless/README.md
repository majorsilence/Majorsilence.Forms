# Majorsilence.Forms.Headless

An **offscreen platform backend** for [Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms):
renders real frames and processes real input with **no display, no window manager, and no windowing
toolkit** — just SkiaSharp.

Use it to unit-test UI logic, snapshot-render forms in CI, or generate images on a server.

## Install

```bash
dotnet add package Majorsilence.Forms
dotnet add package Majorsilence.Forms.Headless
```

Unlike the desktop backend, this one is selected explicitly. `HeadlessRenderer.Use()` does it:

```csharp
using Majorsilence.Forms;
using Majorsilence.Forms.Headless;

HeadlessRenderer.Use ();
```

## Render a form to a PNG

```csharp
HeadlessRenderer.Use ();

var form = new MainForm ();
HeadlessRenderer.CapturePng (form, 800, 600);          // first call lays out
var png = HeadlessRenderer.CapturePng (form, 800, 600);

File.WriteAllBytes ("form.png", png);
```

The first capture performs the layout pass, so capture twice when you need the settled frame.

## Drive input in a test

```csharp
HeadlessRenderer.Use ();

var clicks = 0;
var button = new Button { Text = "Click", Left = 20, Top = 20, Width = 120, Height = 40 };
button.Click += (_, _) => clicks++;

var form = new Form ();
form.Controls.Add (button);

HeadlessRenderer.CapturePng (form, 300, 200);   // force a layout pass
HeadlessRenderer.Click (form, 80, 40);          // center of the button

Assert.Equal (1, clicks);
```

Available: `CapturePng`, `Click`, `MouseDown`, `MouseUp`, `MouseMove`, `KeyDown`, `KeyUp`, `TextInput`.
The keyboard methods return the framework's "handled" flag.

## Why it exists

Beyond testing, this is the reference *second* backend. Because it shares no code with the Avalonia or
Uno backends, anything that works here is genuinely going through the neutral
`IPlatformBackend`/`IWindowBackend` seam rather than leaning on a specific toolkit — which is what keeps
the same app able to target Avalonia today and Uno tomorrow.

## Links

- [**Documentation**](https://forms.majorsilence.com) · [Automation & UI testing](https://forms.majorsilence.com/automation/)
- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Platform backends](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md)
- [Getting started](https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/getting-started.md)

Licensed under the MIT License.
