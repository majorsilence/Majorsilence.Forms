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

## Out of scope — do not implement

Confirming and extending what the matrix already records, so the 54 is not read as 54 units of work:

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
| 0 — repeatable audit | **Done.** `tools/Majorsilence.Forms.GdiDiff` + committed `baseline.txt` + a CI gate in `dotnet.yml`. |
| 1 — Class C hollows | **Mostly done** — see below. |
| 2–6 | Not started. |

Baseline went from **431 → 416** entries (51 → 50 missing types, 380 → 366 missing members).

**Phase 1 landed:** `Graphics.MeasureCharacterRanges` (with real greedy word wrap and one rectangle per
wrapped line, making `SetMeasurableCharacterRanges` non-dead); the full `Region` algebra
(`Union`/`Intersect`/`Exclude`/`Xor`/`Complement` × `RectangleF`/`Rectangle`/`Region`/`GraphicsPath`,
plus `Translate`/`Transform`/`IsInfinite`) and the `CombineMode` enum; a real `TextureBrush`
(`Image`, the transform family, `Clone`).

**Phase 1 remaining:**

1. Tests for `TextureBrush` transforms and `Graphics.MeasureCharacterRanges` — `RegionAlgebraTests`
   (16 tests) covers the region work only. The `MeasureCharacterRanges` wrap logic in particular is
   untested and is the most intricate code added.
2. Correct the `COMPATIBILITY_MATRIX.md` GDI+ table: it lists `TextureBrush` and `ImageAnimator` under
   "Implemented". `TextureBrush` now genuinely is; `ImageAnimator` still only renders one frame and
   should be moved out of that row until Phase 4 adds `FrameDimension`.

**Two corrections to the Class C table above, found while implementing:** `ImageAnimator.CanAnimate`
and `FontFamily.GetName(int)` are *honestly self-documented in source* as no-ops — the overstatement
lives in `COMPATIBILITY_MATRIX.md`, not the code, so the fix there is documentation, not
implementation. `FontFamily.IsStyleAvailable` was the real hollow of the three (documented, but
returning a wrong answer rather than no-op) and now queries the resolved typeface; it had zero
in-repo callers, so tightening it was safe.

## Phased plan

Ordered by *fidelity-per-effort for a migrated app*, not by upstream layout.

### Phase 0 — make the audit repeatable

Move the diff tool to `tools/Majorsilence.Forms.GdiDiff/`, and add a test or CI step asserting the gap
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
