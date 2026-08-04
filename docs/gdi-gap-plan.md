# `System.Drawing` gap analysis and implementation plan

**Measured 2026-08-01** against `System.Drawing.Common` 8.0.12 (`lib/net8.0`) vs. a Release build of
`Majorsilence.Forms.Drawing.Common.dll` + `Majorsilence.Forms.dll`.

This supersedes nothing in [`COMPATIBILITY_MATRIX.md`](../COMPATIBILITY_MATRIX.md) — it *extends* the
[GDI+ surface audit](../COMPATIBILITY_MATRIX.md#gdi-surface-audit-2026-07-29) there, which states of
itself: *"this pass is type-level source-listing comparison, **not member-level reflection**"*. That is
exactly the gap this document closes. Every finding below is from reflection over the real assemblies,
not from reading source listings.

## Method

A `MetadataLoadContext` diff (nothing is executed, so TFM mismatch is irrelevant) walking every exported
type in `System.Drawing.Common`, mapping its namespace per
[MIGRATION.md's table](../MIGRATION.md#namespace-mapping):

| Upstream | Ours |
|---|---|
| `System.Drawing` | `Majorsilence.Forms.Drawing`, `Majorsilence.Forms` |
| `System.Drawing.Drawing2D` | `Majorsilence.Forms.Drawing.Drawing2D` |
| `System.Drawing.Imaging` | `Majorsilence.Forms.Drawing.Imaging` |
| `System.Drawing.Text` | `Majorsilence.Forms.Drawing.Text` |
| `System.Drawing.Printing` | `Majorsilence.Forms.Printing` |

`Color`/`Point`/`PointF`/`Size`/`SizeF`/`Rectangle`/`RectangleF`/`KnownColor` are excluded — they are
deliberately the real BCL primitives and must never be reimplemented.

Members are compared **by name**, including the inherited chain. Constructors, operators, and
accessor/`object` overrides are ignored.

> The diff tool should be committed under `tools/` so this is repeatable and can gate CI (see
> [Phase 0](#phase-0--make-the-audit-repeatable)). It currently lives only in a scratch directory.

## Headline

| | Count |
|---|---|
| Types missing entirely | **54** (≈34 in scope, ≈20 deliberately out of scope) |
| Types present but missing members | **45** |

Raw counts overstate the problem: the single largest row is `PaperKind`, a 112-value enum of paper
sizes — pure data. The counts also *understate* a third class the name diff cannot see at all.

## Three classes of gap

**A — Missing types.** Nothing to call. Migrated code fails to compile.

**B — Missing members on existing types.** The type exists; the member doesn't. Also a compile failure,
but scoped.

**C — Present but hollow.** Invisible to any name-level diff; found by reading source. These are worse
than A and B, because code *compiles and runs* and silently misbehaves. Confirmed instances:

| Finding | Evidence |
|---|---|
| `StringFormat.SetMeasurableCharacterRanges` is **write-only** | It stores ranges, and its own doc comment says *"used by `Graphics.MeasureCharacterRanges`"* — a method that does not exist. Nothing ever reads the stored value. |
| `TextureBrush` is a shell | Public surface is the constructor + `WrapMode` only (8 of 9 upstream members absent: `Image`, `Transform`, `Multiply/Rotate/Scale/TranslateTransform`, `ResetTransform`, `Clone`). It renders, but is not configurable. The matrix lists it under "Implemented" in the brush-family row — overstated. |
| `FontFamily.IsStyleAvailable` always returns `true` | `public bool IsStyleAvailable (FontStyle style) => true;` — reports bold/italic available for every family. |
| `FontFamily.GetName(int language)` ignores `language` | `=> Name`. |
| `ImageAnimator.CanAnimate` always returns `false` | Animated GIF playback is effectively off, though `ImageAnimator` is listed as implemented. |
| `Region` combine methods take only `RectangleF` | No `Region`/`GraphicsPath`/`Rectangle` overloads, and `Xor`/`Complement` are absent entirely — so real region algebra can't be expressed. Overload gaps are invisible to a by-name diff. |
| `ImageCodecInfo.GetImageDecoders()` returns the **encoder** list | Literally `=> GetImageEncoders ()`. Upstream's decoder and encoder sets differ; Skia decodes formats it will not encode. Both are also a hardcoded array rather than a real codec enumeration. |

**Class C is the priority.** A missing member is an honest compile error the developer sees immediately;
a hollow member is a silent wrong result at runtime. This also argues for one addition to the
[stub policy](../COMPATIBILITY_MATRIX.md#stub-policy): a no-op is a reasonable default for *control
behavior*, but for *drawing and metrics* a wrong number propagates into layout. Metric-returning members
should compute or throw, never guess.

## Rescan, 2026-08-02: 107 overload gaps the name-level pass could not see

With phases 1–5 done, the name-level baseline was nearly exhausted (108 entries, mostly out of scope).
So the scanner was taught to compare **overloads**, not just member names — the blind spot that had
already bitten this work twice: `Region.Union(Region)` was missing for the entire life of that type
while `Region.Union(RectangleF)` existed, and the presence check happily said "have it".

This immediately found **107 real gaps**, none of which any previous pass could report. Baseline is
now 215 entries (27 `TYPE`, 81 `MEMBER`, 107 `SIG`).

The check is call-compatibility, not signature equality: our methods routinely add a trailing optional
parameter (a `MatrixOrder`, say), which still binds a call written against the shorter upstream
overload. Ignoring that distinction reported 137 gaps, 30 of them noise.

### What the 107 are

| Group | Count | Notes |
|---|---|---|
| Integer/`float` convenience overloads | ~56 | `AddLine(int,…)`, `AddEllipse(int,…)`, `Region.IsVisible(float,float)`, `GraphicsPath.IsVisible(int,int)`. Purely mechanical, and exactly what designer-generated code emits. |
| Overloads taking a `Graphics` | 22 | `Region.IsVisible(PointF, Graphics)`, `GraphicsPath.IsOutlineVisible(PointF, Pen, Graphics)`, … |
| `Graphics.DrawImage` family | 21 | The largest single method group; upstream has ~30 overloads. |
| `Widen`, `GetBounds(Matrix, Pen)`, misc | ~8 | Real methods with missing shapes. |

The `Graphics`-taking group is the interesting one. `Region` and `GraphicsPath` live in
`Majorsilence.Forms.Drawing.Common`, which **cannot reference `Graphics`** — it lives in
`Majorsilence.Forms`, and the dependency runs the other way. That is why the existing members take
`object? graphics = null`. The fix is not to move types around: an `object?`-typed overload accepts a
`Graphics` argument at the call site, so migrated code compiles unchanged. It just has to exist for
each shape, which today it does not (`Region.IsEmpty` has it; `Region.IsVisible` does not).

`CopyFromScreen` and `FromHdc` in that list are Win32 screen/device-context interop and stay out of
scope.

## Plan to close everything remaining (2026-08-02)

Of the 215 baseline entries, **51 are already-documented non-goals** (metafile/EMF/WMF, Win32 handle
interop, design-time converters, `RegionData`, `CopyPixelOperation`, `CopyFromScreen`). They stay.
That leaves **164 to close**, in three phases, ordered by how much each affects whether real migrated
code compiles and behaves:

### Phase 7 — overload completion (107 `SIG`) — *highest impact*

Mechanical and low-risk, but this is the group that decides whether a migrated project builds at all:
the integer overloads are exactly what `*.Designer.cs` emits.

1. **Integer/`float` convenience overloads (~56).** `GraphicsPath.AddLine(int,…)`,
   `AddEllipse(int,…)`, `Graphics.DrawPie(…int…)`, `Region.IsVisible(float,float)`, … Pure
   delegation to the existing `float`/`PointF` implementation.
2. **`object?`-graphics overloads (22).** `Region.IsVisible(PointF, Graphics)` and friends. As
   explained above, an `object?` parameter binds a `Graphics` argument at the call site, so this is
   additive and needs no type moves.
3. **`Graphics.DrawImage` family (21)** plus `SetClip`/`DrawCurve`/`MeasureString` shapes.
4. **Stragglers (~8):** `GraphicsPath.Widen`, `GetBounds(Matrix, Pen)`, `Bitmap.Clone(Rectangle, PixelFormat)`,
   `Bitmap.LockBits(…, BitmapData)`, `Matrix.TransformVectors(Point[])`,
   `Image.Save(string, ImageCodecInfo, EncoderParameters)`, `Font.GetHeight(Graphics)`.

Tests are call-shape assertions — that each overload exists and delegates to the same result — rather
than re-testing behavior already covered.

### Phase 8 — small real gaps (~16 `MEMBER`) — *cheap and genuine*

- `SystemColors`/`SystemBrushes`/`SystemPens.GradientActiveCaption`/`GradientInactiveCaption` (6).
- `SystemFonts.GetFontByName` (1).
- `ColorTranslator.ToWin32`/`FromWin32`/`ToOle`/`FromOle` (4) — these are plain BGR integer
  conversions and are perfectly meaningful cross-platform, despite the Win32/OLE names.
- `Font.OriginalFontName`/`SystemFontName`/`IsSystemFont`/`GdiVerticalFont` (4).
- `Region.GetRegionScans` (1) — the scanline rectangles, which `SKRegion` can enumerate.

### Phase 6 — printing surface (7 `TYPE` + 41 `MEMBER`) — *lowest priority, done last*

Unchanged from the original plan: printing already renders through `SKDocument.CreatePdf`, so this is
API-shape completion for code that references the types directly. `PrintController` and its two
subclasses, `PreviewPageInfo`, the print event args/handlers, `PrinterUnitConvert`,
`InvalidPrinterException`, `PageSettings.PrintableArea`/`HardMargin*`, and the `PrinterSettings`
collection types.

**Definition of done is unchanged** (see the section at the end): real computed values in tests, the
`NamespaceMap` updated for new types, `COMPATIBILITY_MATRIX.md` corrected, and the baseline
regenerated so the gap set provably shrank.

## Out of scope — superseded 2026-08-04

**This section is kept for the record; phase 9 closed all of it.** The reasoning below was right about
what Skia cannot do and wrong about what that implies. The repo's own
[stub policy](../COMPATIBILITY_MATRIX.md#stub-policy) draws the line at *compiles with reduced
fidelity* versus *does not compile*, and by that line an absent member is the worse outcome: a file
that will not compile blocks the ninety per cent of it that would have worked. See "Phase 9" below
for what each of these actually does now.

The original text:

- **EMF/WMF metafiles** — `Metafile`, `MetafileHeader`, `MetaHeader`, `MetafileType`,
  `MetafileFrameUnit`, `EmfType`, `EmfPlusRecordType`, `WmfPlaceableFileHeader`, `PlayRecordCallback`,
  `Graphics.EnumerateMetafile`/`AddMetafileComment`. Windows-GDI record-and-replay with no Skia meaning.
- **Win32 handle interop** — `IDeviceContext`, `CopyPixelOperation`, `Bitmap.FromHicon`/`GetHbitmap`,
  `Icon.FromHandle`/`ExtractIcon`/`ExtractAssociatedIcon`, `Font.FromHdc`/`FromHfont`/`ToHfont`/
  `FromLogFont`/`ToLogFont`, `Region.FromHrgn`/`GetHrgn`/`ReleaseHrgn`, `Graphics.FromHdcInternal`/
  `FromHwndInternal`/`ReleaseHdcInternal`/`GetHalftonePalette`, `PrinterSettings.GetHdevmode`/
  `SetHdevmode`/`GetHdevnames`/`SetHdevnames`, `PageSettings.CopyToHdevmode`/`SetHdevmode`.
- **Design-time converters** — `FontConverter`, `IconConverter`, `ImageConverter`,
  `ImageFormatConverter`, `MarginsConverter`, `ToolboxBitmapAttribute`,
  `BitmapSuffixInSameAssemblyAttribute`, `BitmapSuffixInSatelliteAssemblyAttribute`.
- **Windows shell icons** — `StockIconId`, `StockIconOptions`, `SystemIcons.GetStockIcon`.
- **`CachedBitmap`** — a device-bound GDI+ acceleration handle; `Graphics.DrawCachedBitmap` with it.

**Permanently partial** (Skia genuinely cannot express these; document, don't attempt):
`Pen.Alignment` inset/outset stroking, `CustomLineCap` outline stroking at line ends,
`Region.GetRegionData`/`RegionData`.

## Progress

| Phase | Status |
|---|---|
| 0 — repeatable audit | **Done.** `tools/Majorsilence.Forms.ApiDiff` + committed `baseline.drawing.txt` + a CI gate in `dotnet.yml`. |
| 1 — Class C hollows | **Done.** 36 tests; `COMPATIBILITY_MATRIX.md` corrected. |
| 2 — data-only enums | **Done.** 176 gaps closed, plus a new class of bug found and fixed (below). |
| 3 — transform & clone families | **Done.** All 39 members; 23 tests. |
| 4 — imaging, metadata, frames | **Done.** 57 gaps closed; 23 tests. |
| 5 — `GraphicsPath` & `Graphics` | **Done.** 36 gaps closed; 32 tests. |
| 6 — printing | **Done.** Controllers, preview capture, unit conversion, settings shapes. |
| 7 — overload completion | **Done.** 103 shapes; 13 tests. |
| 8 — small real gaps | **Done.** System colors/brushes/pens, ColorTranslator, Font metadata. |
| 9 — the documented non-goals | **Done.** All 49. **Baseline: 0.** |
| 10 — metafile playback | **Done.** EMF and WMF are parsed and rendered, not stubbed. |

**The drawing surface is at zero gaps** — 1,200+ → 0.

### Phase 9 — the non-goals, reconsidered

Phase 8 left 49 entries recorded as permanent non-goals. Re-reading them against the stub policy,
that verdict conflated two different things: *this cannot work on Skia* and *this should not exist*.
Only the first was true, and it does not imply the second. Each of the 49 now falls into one of three
cases, and which case it is says something real about the member:

**Genuinely implementable, and implemented** — about half. The five design-time converters
(`FontConverter` with its two nested converters, `IconConverter`, `ImageConverter`,
`ImageFormatConverter`, `MarginsConverter`) are string and byte-array work with nothing
Windows-specific in them; they were classed as non-goals for being "design-time", which is a
statement about where they are *used*, not about whether they can be written. `CopyPixelOperation`
and the metafile enums are numbers. `MetaHeader`, `WmfPlaceableFileHeader` and `MetafileHeader` are
data. `Font.FromLogFont`/`ToLogFont` reads and writes a LOGFONT, which is a *layout*, not an API
call — the fields are matched by name off whatever struct the caller declared, so any LOGFONT works.
`Region.GetRegionData` encodes the region's rectangles and `Region(RegionData)` reads them back, so
cloning a region through its data round-trips exactly. `Metafile.GetMetafileHeader` really parses the
EMF and placeable-WMF headers, both fixed little-endian layouts, so code that inspects a file before
deciding what to do with it works here.

**Cannot work, and says so loudly** — the handle members. `GetHbitmap`, `ToHfont`, `GetHrgn`,
`GetHalftonePalette`, `GetHdevmode`, `GetHdevnames`, `FromHicon`, `FromHbitmap`, `FromHrgn`,
`FromHfont`, `Icon.FromHandle`, `BufferedGraphics.Render(IntPtr)` and the metafile recording
constructors all throw `PlatformNotSupportedException` naming the member and what to use instead.
Returning `IntPtr.Zero` would have been strictly worse than absence: the caller hands it to
`DeleteObject` or `SelectObject` and corrupts silently somewhere else, whereas a throw names the line
that caused it. Absence, meanwhile, stops the whole file compiling.

**Correct as a no-op** — three of them. `Graphics.AddMetafileComment` does nothing, which is exactly
what upstream does when the surface is not recording a metafile, so a caller that comments its
drawing code behaves identically on both. `ReleaseHrgn` and `ReleaseHdcInternal` are no-ops rather
than throws because you can only reach a release with a handle you never obtained — usually from a
`finally` block, where throwing would mask the exception that actually stopped the caller.

Two members are answered rather than refused: `Graphics.GetContextInfo` reports the offset and clip
this surface already tracks, and `Icon.ExtractAssociatedIcon` returns null — an outcome upstream also
returns for a path it cannot resolve, so a caller that checks the result behaves correctly.

**Still permanently partial** (unchanged): `Pen.Alignment` inset/outset stroking and `CustomLineCap`
outline stroking. `Region.GetRegionData` has moved off this list — it works, but its bytes are this
layer's encoding rather than GDI+'s, so they round-trip here and are not something to hand to Win32.

### Phase 10 — metafile playback

Phase 9 left `Metafile` able to read a header and nothing else, on the reasoning that EMF and WMF are
"Windows GDI record-and-replay formats with no Skia equivalent". That conflated the *format* with the
*API*. EMF and WMF are published specifications (MS-EMF, MS-WMF): a metafile is a length-prefixed
sequence of little-endian record structs, and reading one has nothing to do with Windows. What needs
Windows is asking GDI to replay it. Interpreting the records ourselves is a different problem, and a
solved one — libwmf, Inkscape and LibreOffice all render these cross-platform.

So playback is now real. `src/Majorsilence.Forms.Drawing.Common/Metafiles/` holds the record readers,
a GDI device-context state machine, and the two record interpreters; `Metafile` parses on construction
and rasterises onto a Skia canvas. Because it rasterises into the same backing bitmap every other
`Image` uses, a metafile renders anywhere an image does — a `PictureBox`, `DrawImage`, a printed page
— with no changes to any of those paths. And because it is vector data, `Image.PrepareForDraw` lets it
re-render when asked for a size it has not drawn at yet, so scaling one up stays sharp instead of
enlarging pixels.

What the players cover: object creation and selection (pens, brushes, fonts, and the GDI stock
objects), lines, polylines, polygons, poly-polygons, Béziers, rectangles, round rectangles, ellipses,
arcs, chords, pies, path construction and filling, clipping, text with alignment and escapement,
device-independent bitmap blits at 1/4/8/16/24/32 bits per pixel, the window/viewport and world
transforms, and the SaveDC/RestoreDC stack.

Three design points, because each is a way to get this wrong:

- **Unknown records are skipped, not thrown on.** Metafiles routinely carry records from producers
  nobody documents, and a clipboard metafile is routinely truncated. Drawing everything that parsed
  beats discarding a picture over one unfamiliar record in three hundred.
  `Metafile.UnsupportedRecordCount` reports the skips, so a systematically misread file is visible
  rather than quietly half-drawn.
- **EMF+ records are ignored.** They travel inside EMF comment records; rendering the EMF half is
  exactly what a downlevel GDI renderer does with a dual metafile, so this is defined behaviour.
- **WMF is not EMF with smaller integers.** It stores rectangles bottom-right first, takes several
  records' arguments in reverse order, and selects objects by an index into a table it fills in
  creation order rather than by a handle the record names — with deletes leaving a hole the next
  create reuses. Each of those has a test whose failure mode is a picture that still draws, just
  wrong, which is why they are asserted on pixels.

One real bug surfaced during this: the viewport extent defaulted to 1×1, so any metafile that set a
window extent without a viewport extent — which is most WMFs — collapsed its entire picture into a
single pixel. GDI defaults it to the device extent.

The tests build real EMF and WMF byte streams from the published record layouts and assert on the
rendered pixels. That is deliberate on both counts: the platform cannot produce a metafile here, which
is the whole reason the players exist, and a player can decode every field correctly and still draw
the wrong picture, because what a record means depends on the device-context state it inherits.

**Recording is still out of scope.** Creating a metafile by drawing into it needs `Graphics` to emit
records instead of Skia calls, and `Graphics` is sealed over an `SKCanvas`; those constructors throw.


Two real bugs surfaced while finishing, both fixed:

- **`Graphics.FromImage` did not keep its image alive.** An `SKCanvas` does not root its backing
  `SKBitmap`, so any caller who did not separately hold the image could have the bitmap collected out
  from under native code — a process abort, not an exception. Every existing caller happened to hold
  it; the first one that didn't was `PrinterSettings.CreateMeasurementGraphics`.
- **`PageSettings` ↔ `PrinterSettings` initialization cycle.** Giving `PageSettings` a
  `PrinterSettings` property, as GDI+ has, made construction infinitely recursive, because
  `PrinterSettings` builds a `DefaultPageSettings`. `new PageSettings()` stack-overflowed. Now the
  property is lazy and `PrinterSettings` wires the back-reference itself.

Baseline went **431 → 416 → 240 → 201 → 144 → 108** entries. Everything still listed for
`Graphics` is out of scope (metafile enumeration and HDC interop).

**Phase 5 landed:** `GraphicsPath.AddString` (real glyph outlines via `SKFont.GetTextPath`, laid out
from the top-left as GDI+ defines it), `Flatten`, `Reverse`, `Warp`, `AddPie`, `AddClosedCurve`,
`Clone`, `GetLastPoint`, `IsOutlineVisible`, `PathTypes`/`PathData` (+ the `PathData` type) and
`SetMarkers`/`ClearMarkers`; the four real `FontFamily` metrics plus `GetFamilies`;
`StringFormat` tab stops and digit substitution; and on `Graphics`, `FillRegion`,
`DrawClosedCurve`/`FillClosedCurve`, `DrawImageUnscaledAndClipped`, `IsClipEmpty`, `TranslateClip`,
`TransformPoints`, `GetNearestColor`, `Flush`, `RenderingOrigin` and `TextContrast`.

**Two pre-existing fidelity bugs fixed in passing.** `Graphics.DrawPath` and `FillPath` replayed the
path as a polyline rebuilt from `PathPoints`, which threw away every curve, the path's `FillMode`, and
(for `DrawPath`) the pen's dash pattern, caps, join and brush. Both now stroke/fill the real `SKPath`.
This mattered immediately: a path built by `AddString` would otherwise have rendered as a scribble of
straight lines between glyph control points.

Markers deserve a note: GDI+ carries them as a flag bit on the point type rather than as separate
state, so `SetMarkers` records the index and `PathTypes` ORs in `PathPointType.PathMarker` — making it
observable rather than a stored value nothing reads.

Still stored-but-not-applied, documented in place: `StringFormat` tab stops and digit substitution
(the text path draws runs without a tab or locale-substitution pass), `Graphics.RenderingOrigin` and
`TextContrast`, and `Warp`'s `WarpMode.Perspective` (interpolated bilinearly rather than re-projected).

**Phase 4 landed:** `FrameDimension`, `PropertyItem` and `ColorPalette`; `Image.GetFrameCount`/
`SelectActiveFrame`/`FrameDimensionsList`, the property-item set, `Palette`, `GetBounds`, `Flags`,
`Tag`, and the static `PixelFormat` predicates; real `ImageCodecInfo` metadata; the full `Encoder`
GUID set and `EncoderParameter.Type`/`ValueType`/`NumberOfValues`; `ImageFormat.Guid` plus `Webp` and
`Heif`; and the `ImageAttributes` remainder.

Two things became genuinely real rather than surface:

- **Animated GIFs actually animate.** `SelectActiveFrame` decodes through `SKCodec`, so
  `ImageAnimator` is no longer the documented no-op it had been since before this plan — it tracks
  per-image frame state and advances it. That closes the last Phase 1 Class C item, which was
  deferred here precisely because it needed frame decoding to exist first.
- **EXIF is read for real.** SkiaSharp exposes only the orientation tag, so `ExifReader` walks the
  JPEG APP1/TIFF IFD structure directly to populate `PropertyItems`. Deliberately narrow: primary IFD
  plus the EXIF sub-IFD, which is where the tags applications actually ask for live.

Encoded source bytes are retained only when they will still be needed — a multi-frame image, or one
carrying metadata — so an ordinary single-frame PNG does not pay to hold its source twice.

Still stored-but-not-applied, documented in place: `ColorPalette` (Skia has no indexed bitmap type, so
assigning a palette does not re-quantize), `ImageAttributes.SetThreshold`/`SetOutputChannel`/
`SetBrushRemapTable`, and `Image.SaveAdd` (the Skia encoders write one image per file).

**Phase 3 landed:** the six-member transform surface on `LinearGradientBrush`, `PathGradientBrush` and
`Pen` (`TextureBrush` already had it from Phase 1), sharing one internal `BrushTransform` helper;
`Clone` across the whole brush family, declared on the `Brush` base as GDI+ does it and overridden with
covariant returns; `LinearGradientBrush.LinearColors`/`Rectangle`/`WrapMode`;
`PathGradientBrush.Rectangle`/`WrapMode`/`FocusScales`; `Pen.Brush`/`PenType`/`DashCap`/`SetLineCap`/
`CompoundArray`; `Matrix.OffsetX`/`OffsetY`/`Shear`/`VectorTransformPoints`; `Margins.Clone`.

Three of these are real new *behavior*, not just surface: gradient brushes now honor their transform
and `WrapMode` through the Skia shader, and `Pen.Brush` means a gradient- or texture-stroked outline
renders as that brush instead of collapsing to a flat color.

Four are honestly stored-but-not-applied, each documented in place with why:
`Pen.Transform` (no per-pen matrix in `SKPaint`), `Pen.CompoundArray` and `Pen.Alignment` (Skia strokes
one centered ribbon), `Pen.DashCap` (one stroke cap applies to the whole path), and
`PathGradientBrush.FocusScales` (a Skia radial gradient has no inner focus region —
`InterpolationColors` is the portable substitute).

### Phase 2 found a fourth class of gap: wrong numbers

Filling the enums surfaced something neither the type-level audit nor the member-level diff could see:
**members that exist on both sides with different numeric values.** The presence-only diff is blind to
it, and it is worse than a missing member — the code compiles, runs, and silently means something else,
because designer-generated code and `.resx` resources persist these as raw integers.

The first run of the value check found **14 real mismatches**, all pre-existing:

| Enum | Was | Should be |
|---|---|---|
| `StringFormatFlags.DirectionRightToLeft` / `.DirectionVertical` | 2 / 1 — **transposed** | 1 / 2 |
| `PaperKind.Legal` / `.A3` / `.A4` | 2 / 4 / 3 | 5 / 8 / 9 |
| `PaperSourceKind.Manual` / `.Envelope` / `.AutomaticFeed` / `.Custom` | 3 / 4 / 0 / 5 | 4 / 5 / 7 / 257 |
| `PrinterResolutionKind.High`…`.Custom` | 0…4 | −4…0 |

The transposed `StringFormatFlags` pair is the most consequential: both are honored at layout time, so
right-to-left text was being laid out vertically. The rest came from enums declared with *implicit*
values (`0, 1, 2, …`) that happened not to match GDI+ — which also collided once the full upstream
`PaperKind` set was added (`A4` and `Tabloid` both landed on 3).

All 14 are fixed, and the check is now part of `tools/Majorsilence.Forms.ApiDiff` as a `VALUE` gap line,
so CI fails if one ever reappears. `EnumValueFidelityTests` and `PrintingEnumValueTests` additionally
name the specific values that were wrong, so a regression identifies itself.

**Lesson for the remaining phases:** when completing a data-only type, give every member an explicit
value taken from upstream. Implicit numbering is how all four of these bugs happened.

**Phase 1 landed:** `Graphics.MeasureCharacterRanges` (with real greedy word wrap and one rectangle per
wrapped line, making `SetMeasurableCharacterRanges` non-dead); the full `Region` algebra
(`Union`/`Intersect`/`Exclude`/`Xor`/`Complement` × `RectangleF`/`Rectangle`/`Region`/`GraphicsPath`,
plus `Translate`/`Transform`/`IsInfinite`) and the `CombineMode` enum; a real `TextureBrush`
(`Image`, the transform family, `Clone`); a real `FontFamily.IsStyleAvailable`.

Covered by `RegionAlgebraTests` (16), `TextureBrushTests` (10, including pixel-level proof that the
texture transform is actually *applied* rather than merely stored) and `MeasureCharacterRangesTests`
(10). `COMPATIBILITY_MATRIX.md` now carries a corrected `ImageAnimator` row, a
`MeasureCharacterRanges` row, and a pointer from its type-level audit section to the automated
member-level one.

**Two corrections to the Class C table above, found while implementing:** `ImageAnimator.CanAnimate`
and `FontFamily.GetName(int)` are *honestly self-documented in source* as no-ops — the overstatement
lives in `COMPATIBILITY_MATRIX.md`, not the code, so the fix there is documentation, not
implementation. `FontFamily.IsStyleAvailable` was the real hollow of the three (documented, but
returning a wrong answer rather than no-op) and now queries the resolved typeface; it had zero
in-repo callers, so tightening it was safe.

## Phased plan

Ordered by *fidelity-per-effort for a migrated app*, not by upstream layout.

### Phase 0 — make the audit repeatable

Move the diff tool to `tools/Majorsilence.Forms.ApiDiff/`, and add a test or CI step asserting the gap
set does not grow. Without this the numbers here rot within a release, exactly as the 2026-07-29
type-level audit did.

**Deliverable:** committed tool + a baseline file the build compares against.

### Phase 1 — close the Class C hollows *(highest value, small)*

These are correctness bugs, not missing features.

1. `Graphics.MeasureCharacterRanges` — makes `SetMeasurableCharacterRanges` + `CharacterRange` (both
   already implemented) non-dead. Implement via SkiaSharp text measurement per range, returning
   `Region[]`.
2. `TextureBrush` — add `Image`, `Transform`, the four transform mutators, `ResetTransform`, `Clone`.
   Skia backs this with `SKShader.CreateBitmap(..., SKShaderTileMode, localMatrix)`.
3. `FontFamily.IsStyleAvailable` — query the real `SKTypeface` for the style rather than returning
   `true`; `GetName(language)` should at minimum document that it is culture-invariant.
4. `Region` overloads — `Union`/`Intersect`/`Exclude` taking `Region`, `GraphicsPath`, `Rectangle`;
   add `Xor`, `Complement`, `Translate`, `Transform`, `IsInfinite`. Requires the `CombineMode` enum
   (Phase 2). `SKRegion` supports all five ops natively.
5. `ImageAnimator.CanAnimate` — either implement against real frame count (see Phase 4) or make the
   permanent `false` explicit and documented.

**Verification:** unit tests in `Majorsilence.Forms.Drawing.Common.Tests` asserting real values, plus a
`HeadlessRenderer` pixel-diff for the `TextureBrush` transform path.

### Phase 2 — data-only types and enum values *(cheap, unblocks compilation)*

No behavior, pure declaration. Highest compile-surface win per hour.

- **Enum value completions:** `PaperKind` (+112), `PixelFormat` (+15), `PaperSourceKind` (+8),
  `RotateFlipType` (+8 composite aliases), `LineCap` (+7 anchor caps), `HatchStyle`
  (`Min`/`Max`/`LargeGrid`), `SmoothingMode`/`PixelOffsetMode` (`Invalid`), `StringFormatFlags`
  (`MeasureTrailingSpaces`), `PrintRange` (`CurrentPage`).
- **New enums:** `CombineMode`, `DashCap`, `PenType`, `QualityMode`, `CoordinateSpace`, `WarpMode`,
  `FlushIntention`, `ColorMode`, `ColorChannelFlag`, `ColorMapType`, `PaletteFlags`, `ImageFlags`,
  `ImageCodecFlags`, `EncoderValue`, `EncoderParameterValueType`, `StringDigitSubstitute`,
  `StringUnit`, `GenericFontFamilies`, `PrintAction`, `PrinterUnit`.

Values must match upstream numerically — migrated code and `.resx`/designer output persist raw ints.

**Also required:** add every new *type* to `NamespaceMap.MajorsilenceDrawingTypes` in
`tools/Majorsilence.Forms.Migrator/NamespaceMap.cs`, or the migrator will not rewrite references to it.
This is easy to forget and silently degrades migration quality.

### Phase 3 — the transform and clone families *(most common real paint code)*

Upstream's brushes, pens and paths are all transformable; ours largely are not.

- `Transform`, `MultiplyTransform`, `RotateTransform`, `ScaleTransform`, `TranslateTransform`,
  `ResetTransform` on `LinearGradientBrush`, `PathGradientBrush`, `TextureBrush`, `Pen`.
  Backed by `SKShader.WithLocalMatrix` / composing into the paint's matrix.
- `Clone` on `Brush`, `SolidBrush`, `HatchBrush`, `LinearGradientBrush`, `PathGradientBrush`,
  `TextureBrush`, `Margins`.
- `Pen`: `Brush`, `SetLineCap`, `DashCap`, `PenType`, `CompoundArray` (store; Skia cannot stroke
  compound lines — document as partial).
- `LinearGradientBrush.LinearColors`/`Rectangle`/`WrapMode`; `PathGradientBrush.FocusScales`/
  `Rectangle`/`WrapMode`.
- `Matrix`: `OffsetX`, `OffsetY`, `Shear`, `MatrixElements`, `VectorTransformPoints`.

### Phase 4 — imaging, metadata and frames *(self-contained)*

- `ColorPalette` + `Image.Palette`, `Image.GetPixelFormatSize`/`IsAlphaPixelFormat`/
  `IsCanonicalPixelFormat`/`IsExtendedPixelFormat`, `Image.GetBounds`.
- `PropertyItem` + `Image.PropertyItems`/`PropertyIdList`/`GetPropertyItem`/`SetPropertyItem`/
  `RemovePropertyItem` — EXIF read/write. Closes the matrix's existing `PropertyItem` = Missing row.
- `FrameDimension` + `Image.FrameDimensionsList`/`GetFrameCount`/`SelectActiveFrame`/`SaveAdd` —
  animated GIF and multi-page TIFF. Closes the `FrameDimension` = Missing row **and** unblocks
  `ImageAnimator` from Phase 1.
- `ImageCodecInfo` real population — `Clsid`/`MimeType`/`Format`/`FormatDescription` already exist; add
  `CodecName`, `FormatID`, `FilenameExtension`, `Flags`, `Version`, `DllName`, signature
  masks/patterns, and give `GetImageDecoders` a real decoder set (see Class C). Plus
  `Image.GetEncoderParameterList`.
- `Encoder` static GUID fields (12 of 14 missing: `Quality`, `ColorDepth`, `Compression`, ...) and
  `EncoderParameter.Type`/`ValueType`/`NumberOfValues`.
- `ImageFormat.Guid`, `Webp`, `Heif` — Skia already decodes WebP.
- `ImageAttributes` remainder: `SetThreshold`, `SetNoOp`/`ClearNoOp`, `SetBrushRemapTable`,
  `SetOutputChannel`, `GetAdjustedPalette`.

### Phase 5 — `GraphicsPath` and `Graphics` completion

- `GraphicsPath`: **`AddString`** (highest value — text-to-outline, via `SKTypeface`/`SKFont.GetTextPath`),
  `AddPie`, `AddClosedCurve`, `Flatten`, `Reverse`, `GetLastPoint`, `IsOutlineVisible`, `PathTypes`,
  `PathData` (+ `PathData` type), `SetMarkers`/`ClearMarkers`, `Warp` (+ `WarpMode`), `Clone`.
- `Graphics`: `FillRegion`, `DrawClosedCurve`/`FillClosedCurve`, `IsClipEmpty`, `TranslateClip`,
  `TransformPoints`, `GetNearestColor`, `Flush`, `RenderingOrigin`, `TextContrast`,
  `DrawImageUnscaledAndClipped`.
- `StringFormat`: `SetTabStops`/`GetTabStops`, `SetDigitSubstitution`, `DigitSubstitutionMethod`/
  `DigitSubstitutionLanguage`.
- `FontFamily` metrics: `GetCellAscent`, `GetCellDescent`, `GetEmHeight`, `GetLineSpacing`,
  `GetFamilies` — all available from `SKFontMetrics`/`SKTypeface`. These feed text layout, so a wrong
  value is a Class C hazard; implement rather than stub.

### Phase 6 — printing surface completion

Lower priority: printing already renders via `SKDocument.CreatePdf`, and this is API-shape completion
for code that references the types directly.

- `PrintController`/`StandardPrintController`/`PreviewPrintController` virtuals (`OnStartPrint`,
  `OnStartPage`, `OnEndPage`, `OnEndPrint`, `IsPreview`), `PreviewPrintController.GetPreviewPageInfo`/
  `UseAntiAlias`.
- `PreviewPageInfo`, `PrintEventArgs`, `PrintEventHandler`, `PrintPageEventHandler`,
  `QueryPageSettingsEventHandler`, `InvalidPrinterException`.
- `PrinterUnit` + `PrinterUnitConvert`.
- `PageSettings`: `PrintableArea`, `HardMarginX`/`Y`, `Clone`, `PrinterSettings`.
- `PrinterSettings`: `Clone`, `Collate`, `SupportsColor`, `IsPlotter`, `LandscapeAngle`, the
  `PaperSize`/`PaperSource`/`PrinterResolution` collection types, `CreateMeasurementGraphics`.

## Sequencing note

Phases 1 and 2 are independent and can land together — Phase 1 item 4 (`Region`) depends only on
`CombineMode` from Phase 2. Phase 3 depends on Phase 2's enums. Phases 4, 5 and 6 are mutually
independent and can be parallelised or dropped individually without blocking the others.

## Definition of done, per phase

1. Unit tests in `tests/Majorsilence.Forms.Drawing.Common.Tests` asserting **real computed values**, not
   just member existence — the whole point of the Class C finding.
2. Pixel-diff coverage via `Majorsilence.Forms.Headless` for anything that changes rendering.
3. `NamespaceMap.MajorsilenceDrawingTypes` updated for new types, with a migrator rewrite test.
4. [`COMPATIBILITY_MATRIX.md`](../COMPATIBILITY_MATRIX.md)'s GDI+ table row updated — including
   downgrading the `TextureBrush`/`ImageAnimator` claims until Phase 1 lands.
5. The Phase 0 baseline regenerated, so the gap set provably shrank.
