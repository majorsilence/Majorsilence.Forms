# Majorsilence.Forms.Drawing.Common

A **cross-platform, SkiaSharp-backed replacement for the Windows-only `System.Drawing.Common`** (GDI+),
usable on its own or as the drawing layer under
[Majorsilence.Forms](https://www.nuget.org/packages/Majorsilence.Forms).

If your migration is blocked by `System.Drawing.Common` throwing
`PlatformNotSupportedException` off Windows, this is the drop-in shape you're looking for.

## Install

```bash
dotnet add package Majorsilence.Forms.Drawing.Common
```

Types live under `Majorsilence.Forms.Drawing` (plus `.Drawing2D`, `.Imaging`, `.Text`), mirroring the
`System.Drawing` layout — so migration is largely a namespace change:

```csharp
using Majorsilence.Forms.Drawing;

using var bitmap = new Bitmap (200, 100);
using var g = Graphics.FromImage (bitmap);

g.Clear (Color.White);
g.FillRectangle (new SolidBrush (Color.CornflowerBlue), 10, 10, 180, 80);
g.DrawString ("Hello", new Font ("Arial", 14), Brushes.Black, 20, 35);

bitmap.Save ("out.png", ImageFormat.Png);
```

## What it implements

`Brush` · `Brushes` · `SolidBrush` · `HatchBrush` · `LinearGradientBrush` · `PathGradientBrush` ·
`TextureBrush` · `Pen` · `Pens` · `Font` · `FontFamily` · `Image` · `Bitmap` · `Icon` · `Region` ·
`StringFormat` · `GraphicsPath` · `Matrix` · imaging codecs and `ImageAttributes`/`ColorMatrix`, and
the `Drawing2D`/`Imaging`/`Text` enums that go with them.

## What it deliberately does *not* reimplement

The **value types are not replaced**: `Color`, `Point`, `PointF`, `Size`, `SizeF`, `Rectangle`, and
`RectangleF` come from the real BCL `System.Drawing.Primitives`, which is already cross-platform and
ships with the base framework everywhere.

This matters when migrating: keep using those types exactly as-is, and only redirect the GDI+ types.
The [`majorsilence-migrate`](https://github.com/majorsilence/Majorsilence.Forms/blob/main/MIGRATION.md)
CLI applies precisely this split automatically.

EMF/WMF metafile recording and playback is out of scope — a Windows-GDI concept with no cross-platform
meaning on a Skia backend.

## Notes

- Targets `net8.0` and `net10.0`. AOT-compatible and trim-analyzed.
- SkiaSharp is an implementation detail: no `SK*` type appears in the public API, so you are not
  coupled to it.
- Originally developed as part of Majorsilence.Reporting.

## Links

- [Repository](https://github.com/majorsilence/Majorsilence.Forms)
- [Compatibility matrix](https://github.com/majorsilence/Majorsilence.Forms/blob/main/COMPATIBILITY_MATRIX.md) — the `System.Drawing` / GDI+ section
- [Migration guide](https://github.com/majorsilence/Majorsilence.Forms/blob/main/MIGRATION.md)

Licensed under the MIT License.
