# Performance and optimization plan

**Researched 2026-10-11** by reading `Majorsilence.Forms` and `Majorsilence.Forms.Drawing.Common`, with
the Reporting engine's server-side use in mind. Impact figures in the findings tables are estimates from
reading code. Measured numbers appear in the status table and in each task's commit message.

Majorsilence.Reporting renders reports headlessly through this library, on many threads at once, with one
`Graphics`/`Font` per thread. Its profile (WorldFacts PDF, 66 ms before its own optimisation work) put
`Graphics.MeasureCharacterRanges` at about 20 % of CPU, and its allocation profile pointed at the
`ValueTuple<int,int,int>[]` that `LayoutLines` returns. That is the priority; the UI side is covered
more briefly at the end. The Reporting side of the same work is in that repository's
`docs/performance/` and its plan; the cross-repository dependency is spelled out in "Order".

## Status

| Task | State | Where |
|---|---|---|
| F1 `FitRun` in O(n) | done | #471 |
| F2 pin the phantom empty line | done (pinned, not changed) | #471, `LayoutLinesPinTests` |
| F3 cache the scaled `SKFont` | done | #471, `Font.GetScaledSKFont` |
| F4 `MeasureRunWidth` / `MeasureRunSpans` | done: 4.2 us / 728 B -> 0.84 us / 0 B per 20-char cell | #471 |
| Benchmarks in the solution, Dry smoke run and allocation gate in CI | done | this round, phase 0 |
| F6 `TextMeasurer` | done. (b) two-generation cache: a working set of 1000 strings among a stream of new ones goes from 2.85 us / 1645 B to 2.06 us / 1046 B per measurement. (c) one `IndexOfAny` before the `Replace` calls: a single-line cache hit goes from 68 to 46 ns. (d) painting one cached block on 8 threads at once is safe, pinned by `TextBlockConcurrentPaintTests`. (a) not done: colour is part of the block's `Style`, so a painted block cannot answer for another colour, and measure-only calls already share one key (colour 0) | round 2 |
| F11 unshaped measuring without Skia's lock | done: `MeasureRunWidth` on 8 threads 3.12 ms -> 0.030 ms (500 measurements each); one 20-character cell 0.84 us -> 19 ns; `MeasureRunSpans` over 13 words 23 us -> 0.36 us; same floats, bit for bit (`AdvanceTableParityTests`) | round 2 |
| F7 fonts and startup | done, in part. `FontSubstitution.Resolve` is cached (bounded) and `FontFamily` keeps design-unit metrics per (family, bold/italic): `GetLineSpacing` + `GetCellAscent` 17 ms -> 37 ns, `IsStyleAvailable` 8.5 ms -> 2 us, `GraphicsPath.AddString` 8.4 ms -> 3.4 us. `Font.FontFamily` is one instance. Not done: merging the three typeface caches (they resolve each face once already, and Skia hands back the same managed typeface for a face, so they do not split the text cache), and cold start: the first headless `MeasureString` in a process takes 125 ms and `MeasureRunWidth` 70 ms (`StartupBenchmarks`), almost all of it one-time native initialisation (font manager, HarfBuzz) | round 2 |
| F9 static state | done. `TextMeasurer` cache lock #470; `RenderManager`, `ThemeCssReference` #472. Round 2: `Pens`, `Brushes`, `SystemPens` and `SystemBrushes` are immutable as in GDI+ (a change throws `ArgumentException`; `Dispose` stays a no-op, a deliberate difference). `Theme.Xml` `registered_themes` and `ImageAnimator` `animated` hold their lock on every access, so they are safe as they are | round 2 |
| F5 regions in `MeasureCharacterRanges` | done: each range's region starts from its first rectangle instead of the empty region unioned with it, and the line list is kept per thread. One 20-character cell's width 1.55 us / 728 B -> 0.81 us / 240 B; the 13 word ranges of an 85-character cell 13.6 us / 7152 B -> 7.5 us / 2616 B. Same bounds, emptiness and scans (`MeasureCharacterRangesRegionParityTests`) | round 2 |
| F8 images and graphics | done, in part. `Image.Save` encodes from the bitmap's pixels instead of an `SKImage` snapshot: the same bytes (`ImageSaveParityTests`, 5 pixel formats x 6 containers x 3 qualities), no 12 MB native copy for a 2000x1500 page, the encode itself unchanged (codec-bound: 32 ms JPEG, 91 ms PNG); an unsupported container goes to PNG without a failed encode first. The `SKColor` helpers controls paint with share a per-thread paint: 3000 calls 2.96 ms / 258 KB -> 2.48 ms / 0 B. Not done: gradient shaders per fill (a cached shader would need invalidating on every brush property), `SetClip (Region)`'s clone (needed, because the caller keeps the region), `DrawString`'s 488 B per call (inside RichTextKit's paint), and the PNG-options overload (no caller asked for it) | round 2 |
| F10 UI | done, in part. Profiling (`--profiler EP`) found two causes the plan had not guessed. `EnumValidator` answered by reflection on every call and `xGetDock` validates on every read: 90 % of a docked layout pass. It now works out each enum type's rules once. `ScrollableControl.Recalculate` placed, and reset the value of, the hidden scroll bars of every resized `Panel`, recomputing their thumb geometry. Over 1000 children, docked 10.1 ms / 164 KB -> 0.21 ms / 144 B, anchored 25.2 ms / 1542 KB -> 1.42 ms / 338 KB. Not done: `ElementAt` in the layout loops (`Concat` answers it without enumerating, and the profile does not show it), and a typed `Theme` value cache (a read is 10 ns and allocates nothing, `ThemeBenchmarks`). New: `ThemeBenchmarks`, `DataGridViewScrollBenchmarks` (one row and a repaint, 3.9 ms); `PaintBenchmarks` render without PNG-encoding (the encode was about 22 of each 23 ms; a repaint is about 1.1 ms) | round 2 |
| F12 measuring new strings on many threads | done. Our own locks and cache made `MeasureString` scale badly; `ConcurrentMeasureBenchmarks` gained cache-miss cases (5000 distinct strings). `PrivateFontRegistry.IsEmpty` took a lock once per laid-out run (16 % of 8 threads), and the `TextMeasurer` cache lock another 24 %: both lock-free now. `MeasureText` keeps the size, not the `TextBlock` (the kept layouts were being promoted by the GC). A cached `MeasureString` no longer copies text with inner spaces. 500 new strings: 1 thread 5.9 -> 3.7 ms, 8 threads 43 -> 21 ms; 500 cached: 8 threads 1.70 -> 0.087 ms, 88 KB -> 1.45 KB on one thread. Left: the rest of a miss is RichTextKit's (shaping, and `FontFallback` making an undisposed `SKFont` per run, each registered under SkiaSharp's global handle lock) | round 3 |
| F13 advance tables past U+00FF | done. `AdvanceTable` keeps 256-character pages, Latin-1 built with the table and any other page the first time a run needs it, published lock-free; only a surrogate sends a run to Skia. Polish, Czech, Greek and Cyrillic cells (`MeasureRunWidthExtended`, 500 per thread): 1 thread 710 -> 31 us, 8 threads 3.8 ms -> 57 us. Latin-1 keeps its own loop (19 ns a 20-character cell; with the paging in the same loop it was 49 ns). Same floats, bit for bit (`AdvanceTableParityTests`, corpus extended) | round 3 |
| F14 `MeasureCharacterRanges` without Skia's locks | done. Two native calls per range went through process-wide locks: `SKFont.Metrics` (Skia's glyph cache; now kept in the advance table, whose key gained the three scaler settings it lacked) and the `SKRegion`/`SKPath` behind each `Region` (SkiaSharp's handle table). A `Region` made from one rectangle now keeps the rectangle and makes its native form only when something other than `GetBounds`/`IsEmpty` needs it. 500 per thread: 1 thread 307 -> 48 us, 8 threads 2.34 ms -> 0.115 ms. One cell 830 ns / 240 B -> 81 ns / 96 B; 13 word ranges of an 85-character cell 8.0 us / 2616 B -> 0.83 us / 888 B. `Font.GetHeight` reads the kept metrics too. Same answers: `RegionPendingRectangleTests` (bounds bit for bit, emptiness, scans and every operation after, against the native form), metrics in `AdvanceTableParityTests` | round 3 |
| F15 grid and table lines | done. Profiling a `DataGridView` scroll: a third was cell borders. Skia strokes a line wider than a hairline by building and filling an outline path (1.3 us a short line); a horizontal or vertical one is now filled as the rectangle it covers (0.35 us), in `SkiaExtensions.DrawLine`, `Graphics`' SKColor `DrawLine` and `Graphics.DrawLine (Pen)` for a plain pen without antialiasing, under a scale-and-translate canvas matrix. Same pixels (`LineAsRectParityTests`). The renderers' SKColor canvas helpers share a per-thread paint (round 2 did only `Graphics`' own). `GetColumnDeviceLeft` sorted the columns into a new list per cell painted. One row scrolled and repainted: 4.12 ms / 774 KB -> 2.98 ms / 524 KB. 2000 table-border lines with a pen: 3.66 -> 2.20 ms (`GraphicsAllocationBenchmarks.TableBorders`, new) | round 3 |
| F16 anchored layout allocation | done. An allocation trace (`dotnet-trace --profile gc-verbose`) of anchored passes: half the bytes were a `HybridDictionary` of pending bounds made for each pass, which past eight children grows as a `Hashtable` and boxes every `Rectangle`; a fifth the `WeakReference` in every `LayoutEventArgs`; a tenth a `Concat` iterator each time a panel's children were asked for. Now a typed dictionary kept with the container, the component held strongly until args are cached (then weakly, as upstream), and one live list view of a collection's children that LINQ counts and indexes without enumerating. 1000 children: 1.42 ms / 338 KB -> 1.07 ms / 104 KB a pass. Pending bounds are applied in the order worked out (the `Hashtable` order past eight children followed hash codes) | round 3 |
| F17 what is left of a new string, and invalidation | measured. Of a new string's layout, what is this library's own is the style a block takes from its typeface: three native calls (`FamilyName` a native string) per block, now cached per typeface (2.75 -> 2.70 MB per 500 new strings; time within noise). The rest is RichTextKit: HarfBuzz shaping, and `FontFallback.GetFontRuns` making an `SKFont` per run that it never disposes (14 % of a miss, each registered under SkiaSharp's handle lock); `FontRun.Paint` makes two `SKPaint`s and copies the glyph positions to an unused array on every paint, which is `DrawString`'s 488 B. A fix there is upstream's or a fork's. `Control.Invalidate` costs 51 ns and 32 B (its `InvalidateEventArgs`); repaints already coalesce, as a window only flags itself and the host's `InvalidateVisual` merges requests, so nothing was changed | round 3 |

## Method

- Benchmarks live in `benchmarks/Majorsilence.Forms.Benchmarks` (BenchmarkDotNet, `[MemoryDiagnoser]`),
  which is in the solution, so `dotnet build` compiles it. Run one class with
  `dotnet run -c Release --project benchmarks/Majorsilence.Forms.Benchmarks -- --filter '*TextMeasure*' --job short`.
  Local results go to `BenchmarkDotNet.Artifacts/`, which is ignored. The manual `Benchmarks` workflow
  (`.github/workflows/benchmarks.yml`) runs a filter on a CI machine and uploads the results.
- **CI** (`benchmarks` job in `dotnet.yml`, Linux) runs every benchmark once with the Dry job, so a
  broken benchmark fails the PR, and then runs the **allocation gate**. Benchmarks tagged
  `[BenchmarkCategory ("AllocGate")]` must not allocate more managed bytes per operation than
  `AllocationBaseline.txt` records, plus the larger of 5 % and 64 B. The gate prints a notice when one
  allocates less. Regenerate with `-- --alloc-gate --write` to lock the gain in. Tag a new benchmark
  only after two runs give the same number. Timings are never gated.
- Allocated bytes are exact and comparable between runs; timings are not (battery, background load).
  Compare allocation first.
- Public API: `tools/Majorsilence.Forms.ApiDiff` only reports upstream members that are missing or have
  the wrong shape, so **adding** a public method is safe for the gate; changing or removing an existing
  overload is not. New public members need XML docs (Release treats warnings as errors).
- Thread safety: a shared cache is fine if its entries are immutable and it is bounded; a plain
  `Dictionary`, a lazy field, or a shared mutable object is the bug class that #470 fixed in
  `TextMeasurer`. Every shared structure a task adds or touches gets a concurrent test shaped like
  `TextMeasurerThreadSafetyTests`.
- Output identity: tasks that change widths, wrap points, regions or encoded bytes must prove it with a
  parity test against the old implementation over a corpus before and after.

## A. Headless text measurement (priority)

`MeasureCharacterRanges` (`Graphics.cs:277-318`) is what report engines call for every table cell. It is
**not** the same engine as `MeasureString`: `MeasureString` goes through `TextMeasurer`/RichTextKit
(shaped, integer-rounded size); `MeasureCharacterRanges` uses plain `SKFont.MeasureText` advances at the
unrounded size. A replacement for one is not bit-identical to the other.

| Finding | Where | Cost |
|---|---|---|
| `FitRun` measures every prefix | `Graphics.cs:375-381`: `font.MeasureText(text.AsSpan(start, i - start + 1))` for each character `i`. With `layoutRect.Width = float.MaxValue` the overflow test never fires, so it scans the whole paragraph | O(n²) glyph work and n P/Invokes per call: about 100 calls and ~5,000 glyph advances for a 100-character cell |
| Region built per range | `MeasureCharacterRanges` starts from `EmptyRegion()` and calls `Union(RectangleF)` per range per line (`:310`); `Union` makes a temporary `Region`, and fractional rectangles take the `SKPath` + `SKPath.Op` route (`Region.cs:62-67, 388, 434`) | Several finalizable Skia objects per range, each registering in SkiaSharp's handle table (a probable cross-thread contention point, unverified) |
| `LayoutLines` result | `Graphics.cs:335` returns a `List<(int,int,int)>` | the `ValueTuple<int,int,int>[]` in the Reporting allocation profile |
| `SkFontFor` | `Graphics.cs:525-532` returns the cached `Font.GetSKFont()` only when `FontScale == 1`; otherwise builds a new `SKFont` per call | every call on a 300 DPI report bitmap |
| `CharacterRange` is a mutable class | `StringFormat.SetMeasurableCharacterRanges` stores the caller's array by reference | an array and objects per measurement; a `StringFormat` is needed first |

**Possible correctness issue (pin before touching).** In `LayoutLines`, after `FitRun` consumes a whole
paragraph `cursor == contentEnd` but the loop condition is `cursor <= contentEnd`, so the next `FitRun`
returns 0 and an empty line is added. On the wrapping path every paragraph therefore adds a phantom empty
line and later lines sit one `lineHeight` lower per preceding paragraph; the `maxWidth <= 0` path does not
do this. Passing width 0 and passing `float.MaxValue` are therefore not interchangeable for multi-line
text. Unconfirmed: the first task is a test that pins what actually happens.

### Tasks

1. **F1: `FitRun` in O(n)** (S). Measure the whole paragraph first; if it fits `maxWidth` return
   `end - start`; otherwise binary-search the first overflowing prefix and compute `lastBreak`. Valid
   because unshaped prefix widths from `SKFont.MeasureText` are non-decreasing. Add a parity test of the
   old implementation (kept in the test project as a reference) against the new over varied text, fonts
   and widths, including widths that force wrapping and zero/one-character paragraphs.
2. **F2: pin the phantom empty line** (S). A test that records the line count and positions for a
   multi-paragraph string on both the wrapping and `maxWidth <= 0` paths. **Do not change the behaviour**
   in this task; whether to fix it is a separate decision because callers may depend on it.
3. **F3: cache the scaled `SKFont`** (S). On the `Font`, keyed by (FontUnits, Edging, Subpixel, Hinting)
   (exactly the properties `SkFontFor` copies today); a plain field is fine under one-Font-per-thread.
4. **F4: add a direct measuring API, separate PR after F1** (S-M).
   `Graphics.MeasureRunWidth(string text, Font font)` and
   `MeasureRunSpans(string text, Font font, ReadOnlySpan<CharacterRange> ranges, Span<…> result)` calling
   `SKFont.MeasureText` directly: no `Region`, no `StringFormat`, no layout list. Keep the existing two
   `MeasureText` calls per range (prefix, then segment) so every float is the one
   `MeasureCharacterRanges` produces today; an incremental-prefix variant only with a parity test, since
   a cumulative sum is not proven bit-equal. Document that it is unshaped (`MeasureCharacterRanges`
   metrics), not `MeasureString` metrics. Equality test of old vs new over a corpus. New
   `TextMeasureBenchmarks` covering `MeasureCharacterRanges`, the new API and `MeasureString`.
5. **F5: batch the regions inside `MeasureCharacterRanges`** (M). Start from `new Region(RectangleF)` for
   the first rectangle and union only for later lines, keeping the geometry identical (integral
   rectangles use `SKRegion.Op`, fractional use path union, so verify bounds as well as areas).
6. **F6: `TextMeasurer`** (M). (a) Drop colour from the cache key for measure-only calls (layout is
   colour independent). (b) Replace the wholesale `Clear()` at 2000 entries with LRU or generation
   eviction, since a report with thousands of distinct strings thrashes it. (c) Move the `\n\r`
   `Replace` calls (`TextMeasurer.cs:43-46`) off the hit path. (d) **Verify, and if real protect, painting
   a shared cached `TextBlock` from two threads**: RichTextKit builds each run's `SKTextBlob` lazily on
   first paint, so the cache after #470 is safe as a dictionary but a shared block may not be safe to paint
   concurrently. Test first; the fix may be a per-thread cache or a clone.

### Found while measuring: Skia serialises measurement across threads

`ConcurrentMeasureBenchmarks` gives every thread its own `Graphics`, `Font` and text, so it should scale
flat. It does not. For 500 measurements per thread, on a 12-core machine:

| Threads | `SKFont.MeasureText` alone | `MeasureRunWidth` | `MeasureString` (cached block) | `MeasureCharacterRanges` |
|---|---|---|---|---|
| 1 | 0.32 ms | 0.30 ms | 0.08 ms | 1.26 ms |
| 4 | 1.48 ms | 1.53 ms | 0.53 ms | 4.69 ms |
| 8 | 3.20 ms | 3.12 ms | 1.77 ms | 8.64 ms |

The raw SkiaSharp call, with no code from this library, scales the same way. Every `SKFont.MeasureText`
goes through Skia's process-wide glyph cache (`SkStrikeCache`), which is guarded by one mutex. Eight
report threads measuring cells therefore run more slowly than one thread would.

11. **F11: unshaped advances from a managed table** (M). Keep each glyph's advance for a
    (typeface, size, edging, subpixel, hinting) in an immutable table shared across `Font` instances, and
    sum it in managed code for `MeasureRunWidth`, `MeasureRunSpans`, `FitRun` and
    `MeasureCharacterRanges`. Skia's `measureText` adds the same per-glyph advances in the same order, so
    the result is bit-identical when the strike is not rescaled. A parity test over a corpus of texts,
    sizes and DPIs proves it. Characters outside the table (surrogates, anything past U+00FF) fall back to
    Skia for the whole run.

## B. Fonts and startup

| Finding | Where |
|---|---|
| Three overlapping typeface caches that can hold different `SKTypeface` instances for one face | `Font.s_typefaceCache`, `TypefaceCache` (`:22`), `ThemeCssValues.typefaces` (`:100`) |
| `Font.FontFamily` allocates a `FontFamily` on every access | `Font.cs:120` |
| `FontSubstitution.Resolve` is uncached: a font-manager (fontconfig) query for the family and one per alternative, per call | `FontSubstitution.cs:93`; callers `FontFamily.cs:67,85`, `Drawing2D.cs:279`; `GraphicsPath.AddString` pays `SplitByCoverage` per string (`Drawing2D.cs:238,306`) |
| `Theme`'s static constructor runs font lookups and installs the font mapper, paid by the first headless `MeasureString` that needs `Theme.UIFont`/`FontSize` | `Theme.cs:25-33`, `Graphics.cs:88` |
| `PrivateFontRegistry.Resolve` takes a global lock when a new `Font` is constructed | `FontCollection.cs:176` |

7. **F7: fonts and startup** (S-M). Consolidate the typeface caches behind one process-wide, thread-safe
   lookup; cache `FontFamily` on the `Font`; cache `FontSubstitution.Resolve` results (bounded);
   measure the cold-start cost of the first headless `MeasureString` and move any UI-only work out of it.
   New `FontBenchmarks` (new `Font` + `GetSKFont()`, known and unknown family, cold and warm) and
   `StartupBenchmarks` using `RunStrategy.ColdStart`.

## C. Images and graphics

| Finding | Where |
|---|---|
| `Image.Save` builds `SKImage.FromBitmap` (may snapshot/copy the pixels), then `Encode`, then copies the `SKData` to the stream; PNG has no compression/filter options; an unsupported format fails an encode then encodes again as PNG | `Image.cs:315-333` |
| Per-call `new SKPaint` in the hairline/rect/line helpers | `Graphics.cs:826, 833, 854, 3101-3142` (the main fill and stroke paints are already pooled `[ThreadStatic]`) |
| Gradient, hatch and texture brushes build an owned shader per use | `Brush.CreatePaint`, `Graphics.cs:2058-2062` |
| `SetClip(Region)` clones the region (a native allocation) for the clip shadow | `Graphics.cs` `ReplaceClip(region.Clone())` |
| Shared `Pens.*` / `Brushes.*` / `SystemColors` instances are mutable (`Pen.Width`, `Color`, `DashStyle`); a thread that mutates `Pens.Black` changes it for every thread | `Pens.cs:11`, `Brushes.cs:11`, `SystemColors.cs` |
| `GetPixel`/`SetPixel` validate and P/Invoke per pixel | `Image.cs:586-601`; `LockBits` (`:690`) is the bulk path |

8. **F8: image and graphics** (M). Try `SKImage.FromPixels` over `PeekPixels` for `Save`; add an additive
   PNG-options overload; avoid the double encode for unsupported formats; pool the helper paints like the
   main ones; document or freeze the shared pens/brushes. New `ImageEncodeBenchmarks` (PNG and JPEG at
   800x600 and 2000x1500) and `GraphicsAllocationBenchmarks` (gradient fill, `SetClip(Region)`,
   `DrawString` with a gradient brush). Encoded bytes must be identical unless a task says otherwise.

## D. Static state: thread-safety follow-ups

The #470 class of bug. From a read of the static collections in `src` (not all were verified):

| Where | Concern |
|---|---|
| `Theming/ThemeCssReference.cs:492-494` `selectors_by_type` | lazy initialisation of a plain `Dictionary` looks unsynchronised |
| `Theme.Xml.cs:27` `registered_themes` | plain `Dictionary`; confirm every access (`:55,68,115,130`) holds the lock |
| `RenderManager.cs:8` `renderers` | filled in the static constructor; public `SetRenderer` would race if called off-thread |
| `ImageAnimator.cs:110` `animated` | confirm locking |
| `TextMeasurer` painted blocks | see F6(d) |

9. **F9** (S-M). For each row: read, write a concurrent test that fails if it is a real race, then fix.
   Rows that turn out safe get a one-line note in the code and are dropped from this table.

## E. UI side (shorter; lower priority than the server use)

| Finding | Where |
|---|---|
| Every `Theme.X` getter is a string-keyed `ConcurrentDictionary` lookup plus unboxing, on the paint path (measured since: 10 ns a read, no allocation) | `Theme.cs:15` |
| `children.ElementAt(i)` in layout loops (benchmarks show near-linear docked layout at 10,000 children, so this may already be hidden by an `IList` fast path; anchored layout allocates ~10 KB per 10 children) | `Layout/DockAndAnchorLayout.cs:29, 264, 309, 495, 917` |
| Text block cache churn on repaint (see F6) | `PaintBenchmarks` (four repaints, 44-56 ms each) |
| Uncovered by benchmarks: invalidate coalescing, CSS rule matching (`ThemeStyleSheet` application), `DataGridView` scroll paint | |

10. **F10** (M, after the above). Typed theme-value cache invalidated on `SetValue` (keep the
    theme-changed events); replace `ElementAt` with indexed access where `children` is not already an
    `IList`; look at the anchored-layout allocation; add the missing benchmarks first so each change has a
    number.

## Order

1. F1, F2, F3 (independent, small; F1 and F3 speed existing callers with no API change).
2. F4 as its own PR after F1 lands. **Then a Majorsilence.Forms release**: the Reporting engine adopts
   the API (its task R6) by bumping the package version in its `Directory.Packages.props`, replacing its
   `MeasureCharacterRanges` calls and dropping its `MeasureStringLocked` workaround, which exists only
   because the package it consumed before #470 had the cache race.
3. F6, F7, F9 (thread-safety first within them), then F5, F8, F10 as capacity allows.

## How a task is done

Measure with the relevant benchmark, change, re-measure, and keep the parity or concurrency test beside
the change. `dotnet build -c Release` and
`dotnet test -c Release --filter "FullyQualifiedName!~Migrator"` must stay green, and
`dotnet run --project tools/Majorsilence.Forms.ApiDiff -- --surface drawing --check` must not gain a line.
