# Krypton Standard Toolkit port — remaining work

All seven Krypton projects compile against Majorsilence.Forms (verified in a mirror). The decision on
record: `Form : WindowBase` stays; the form/control seam is bridged **Krypton-side** by
`tools/fixups/krypton-standard-toolkit-bridge.py` (idempotent; run at the `Source/Krypton Components`
root). Each task below is independent and sized for a single session.

## 1. Bring the real Standard-Toolkit tree to green (mechanical)

The real clone is migrated in place but was migrated before the `System.Media` mapping existed and
still has Windows TFMs. In order:

1. `Source/Krypton Components/Directory.Build.props`: every `<TargetFrameworks>` → `net10.0`
   (drop `net4x`/`-windows`), and normalise the `\` path separators in its imports.
2. Repo-root `nuget.config` restricted to nuget.org (the private ProGet feed 403s).
3. In each project's global-usings file: `global using System.Media;` →
   `global using Majorsilence.Forms.Media;` (the migrator now does this; a re-run also works, but see
   task 3 first).
4. `python3 tools/fixups/krypton-standard-toolkit-bridge.py ".../Source/Krypton Components"`.
5. Krypton.Utilities only: it has TWO globals files and the migrator put the BCL-preference aliases in
   both (CS1537) — remove the alias lines from `Global/Globals.cs`, keep `Globals/GlobalDeclarations.cs`.
6. Build each project; expect 0. Any stragglers will be small — follow the shapes in this repo's
   `KryptonPortParity*.cs` and the bridge script's comments.

## 1b. DONE 2026-08-14: real Standard-Toolkit green, Cyotek migrated, Extended-Toolkit started

- **Standard-Toolkit (real repo)**: §1 executed — props TFMs to net10.0, nuget.org-only nuget.config,
  bridge script applied, System.Media remapped, Utilities alias dedupe. All six projects AND TestForm
  build at 0 errors.
- **Cyotek.Windows.Forms.ColorPicker fork**: migrated in place; library and tests build at 0 and its own
  **52 tests pass**. TestForm now project-references it, so the real ColorPickerDialog is back (the
  bridge script's package-drop entries were replaced with the project reference). Its *demo* is blocked
  only by `TheArtOfDev.HtmlRenderer.WinForms`, an unrelated Windows-only third-party package.
- **Extended-Toolkit**: migrated (670 of 2,367 files). **Superseded by the section below**: as of
  2026-08-15 the Examples demo and all 59 projects it references build at 0 errors and the demo runs.
  Fixups live in `tools/fixups/krypton-extended-toolkit-bridge.py`.

### DONE 2026-08-15: Extended-Toolkit Examples demo builds and runs

All 59 dependency projects plus the `Examples` app build at **0 errors**, and the demo launches and
renders its Landing window on macOS. Every cause listed in the old catalogue below is closed. What it
took, grouped by where the fix belonged:

**Majorsilence.Forms** (the standing preference -- fix here for parity, not in the port):

- **Missing `protected virtual` hooks (17 override sites)** -- the largest single group. A control library
  subclasses a WinForms control and overrides one of these; a missing one is a hard CS0115 at the override
  site, not a silent stub. Added `ToolStripItem.OnClick(EventArgs)`/`OnFontChanged`,
  `DataGridViewCell.OnContentClick`/`OnClick`, `DataGridViewRow.CreateCellsInstance`,
  `TabControl.OnSelecting`/`OnDeselecting`/`OnSelected`/`OnDeselected`, `Form.OnResizeBegin`,
  `ToolStripDropDownItem.OnDropDownClosed`, `Control.OnParentBackgroundImageChanged`,
  `ToolStripComboBox.OnSelectedIndexChanged`. Each is wired into the path that already raised the matching
  event, so an override actually runs -- a raiser nothing calls would have compiled and done nothing.
- **`MenuItem.OnClick(MouseEventArgs)` now funnels into `OnClick(EventArgs)`**, which is where WinForms
  declares it and therefore what ported items override. The cast in that call is load-bearing.
- **`TabControl` selection events are now raised** (they were declared behind `#pragma warning disable
  CS0067` and never fired), including cancel-and-revert semantics for `Selecting`/`Deselecting`.
- **`MenuBase : ScrollableControl`** instead of `: Control`, matching `ToolStrip : ScrollableControl`
  upstream. A pure insertion; it let `ToolStrip` drop five auto-property scroll stubs in favour of the
  real inherited implementations.
- **`SKControl` / `SKGLControl` / `SKPaintSurfaceEventArgs` / `SKPaintGLSurfaceEventArgs`** in
  `SkiaSharp.Views.Desktop` (`SkiaSharpViews.cs`). `SkiaSharp.Views.WindowsForms` is WinForms-bound and
  gets stripped, but this library already renders through SkiaSharp, so a control that hands its surface to
  the caller is a wrapper rather than a port. Unblocks any SkiaSharp-drawing control (here, ScottPlot).
- Smaller additions: `Control : ISynchronizeInvoke` (explicit, so the existing overloads keep their own
  nullability), `ControlStyle.Padding`/`Clone()`, `DataGridView.RowTemplate` settable,
  `DataGridView.VerticalScrollBar`/`HorizontalScrollBar`, `ToolStripProgressBar.Control`,
  `SystemInformation.ComputerName`/`UserName`/`UserDomainName` (real values -- error reporters show these),
  `GroupBoxRenderer`, `VisualStyleElement.Tab`/`.TreeView`, `Design.PaintValueEventArgs`/`ToolboxItem`,
  `UITypeEditor.PaintValue`, `Graphics.CopyFromScreen` 6-arg, `ColumnHeaderCollection.Add(text,width,align)`,
  `TabPageCollection.AddRange`, `Font(family,size,unit)`, `MenuItem.DesignMode`, `DashStyle` moved to
  `Drawing.Drawing2D` where GDI+ puts it.
- **Two events retyped to WinForms' named delegates** -- `ControlAdded`/`ControlRemoved` to
  `ControlEventHandler`, the `DataGridView` cell-mouse family to `DataGridViewCellMouseEventHandler`.
  Note the trap: the raisers cast out of an `EventHandlerList` with `as`, so retyping the event without
  retyping the cast makes the event silently stop firing. A pinned test caught it.

**Migrator** (`tools/Majorsilence.Forms.Migrator`):

- **Implicit usings.** `<UseWindowsForms>` on the WindowsDesktop SDK adds `System.Windows.Forms` and
  `System.Drawing` to a project's implicit usings; conversion moves to `Microsoft.NET.Sdk`, which does not.
  A project whose files never imported those namespaces explicitly loses every WinForms and drawing type
  at once -- 232 CS0246s on names as basic as `Form` and `Color`, in the projects that look cleanest.
  `AddImplicitUsingReplacements` restates them as `<Using>` items.
- `DesktopSdkReplacements` entries carry a per-package version now: `System.CodeDom` has to be pinned in
  the 10.x band because `System.Management 10.0.0` depends on it, and a lower pin is an NU1605 *error*.
- `AlignSharedPackageVersions` raises SkiaSharp/HarfBuzz pins to this library's floors, same reason.

**Upstream drift** (`tools/fixups/krypton-extended-toolkit-bridge.py`) -- would fail on Windows too:

- Code Access Security removed (asserts, `PermissionSet`, `PolicyLevel`, `SecurityManager`), including the
  comma-separated attribute-list form. Referencing the shim package would only trade a compile error for a
  `PlatformNotSupportedException`.
- `KryptonToastNotificationIcon` -> `KryptonToastIcon`; bare `ComponentResourceManager` qualified.
- Missing project references (`Krypton.Docking`, `Krypton.Utilities`) and global usings.
- **Form-is-not-a-Control sites** routed through `Controls.Owner` / `MFFormBridge.AsKryptonForm`, the same
  bridge Standard-Toolkit uses. Includes a `parent is KryptonForm` walk that could never succeed here.
- `MenuItem` -> `ToolStripItem` casts at four call sites: this library inverts that hierarchy
  (`ToolStripItem : MenuItem`), so it is a downcast, not the upcast it is on Windows. A cast at the
  boundary beat reshaping a core menu type that thousands of tests pin.

**Gates after all of the above:** 4,501 tests pass, Release (analyzers + warnings-as-errors) clean, ApiDiff
reports no new gaps, `NoOpStubBaseline` regenerated with three deliberate no-ops. The Control/window parity
gate was satisfied by forwarding `InvokePaint`/`InvokePaintBackground`/`OnParentBackgroundImageChanged` onto
`WindowBase` rather than by waiving it -- a WinForms Form inherits those from Control.

**Running it:** build `Examples/Examples.csproj`; the output lands in `Bin/Examples/Debug/net10.0/`. Copy
the Win32 shim dylibs into `runtimes/osx/native/` first -- the full set, not just the drawing ones.
`user32.dll.dylib` in particular is needed at startup (`VisualForm`'s static constructor calls
`RegisterWindowMessage`), and its absence is a hard crash before any window appears.

**Two runtime crashes found by actually clicking things** (a clean build proved nothing about these):

1. `GraphicsExtensions.LoadIcon` returned null and the UAC-shield button did `.ToBitmap()` on it. `LoadImage`
   is a user32 P/Invoke; the shim answers with a null handle, and `LoadImage` is documented to fail that way
   on Windows too. Fixed in Standard-Toolkit with a `SystemIcons` fallback per `IconType`, so the method
   honours what every caller assumes -- a non-null icon.
2. `KryptonOKDialogButton.OnParentWindowChanged` raised `ParentWindowChanged.Invoke(...)` unguarded. Nothing
   subscribes, so the event is null and the raise aborted the process the moment the designer assigned
   `ParentWindow`. Not a port artefact -- identical on Windows. `guard_event_raises` in the fixup script
   makes expression-bodied raisers null-conditional; 9 sites across 4 files, and it skips lines that merely
   read `.Invoke(`/`.Select(` in a LINQ chain by requiring a void-returning method on the same line.

Both were reached only by opening an example, which is the lesson: "0 errors" and "renders a window" are two
claims, and neither is "the examples work". Drive the UI.

### OPEN: visual defects found by eye in the Examples demo

Every form below constructs and runs; these are rendering faults, which no gate and no smoke harness can
see. Ordered by how much of the suite each one affects.

1. **FIXED 2026-08-18 (root cause was library-wide, not the ribbon).** Chasing the ribbon found that
   `Drawing.Font` handed its `Size` -- expressed in `Unit`, Point by default as in GDI+ -- straight to
   SkiaSharp, which measures text in PIXELS. A 9pt font therefore rendered at 9px instead of 12px: **every
   piece of text in the library was about a quarter too small, and so was every dimension derived from text
   metrics.** `Font.PixelSize` now converts by unit, and the measure path uses it too -- it was passing
   `(int)font.Size`, so measurement and drawing disagreed by that same quarter.

   Two further inconsistencies surfaced while fixing it, both found by tests rather than by eye:
   - Centred text sat ~3px right of centre, because `MeasureString` measured at the point size while
     `DrawString` drew at the pixel size.
   - Vertically centred text sat low, because the baseline was placed one **em** below the text's top edge
     instead of one **ascent**. Close enough to look plausible, but it disagreed with the line height that
     `MeasureString` reports.

   Ribbon effect: tab strip 19px -> 21px, app tab 49x19 -> 54x21. Pinned by `FontPixelSizeTests`.

   **Two things I got wrong on the way, worth recording.** First, the probe showed the ribbon at width 0 and
   I called it definitive; in fact the root `ControlAdapter` is sized only inside `RenderFrame`, so it is
   0x0 until something paints -- a headless artifact, though it does mean pre-paint layout of docked children
   is wrong, which is worth fixing separately. Second, `Segoe UI` reporting a line height exactly equal to
   its em size looked like a degenerate fallback; it is not. It resolves to Helvetica (2,252 glyphs, real
   metrics) and Helvetica genuinely has ascent+descent of 1.0 em with zero leading. Font substitution is
   fine. That is why the tab strip is 21px rather than the ~25px Segoe UI would give -- proportionally
   correct for the substituted face, not a bug.

   **The geometry was never "collapsed".** At a correct width the view tree is sane: ribbon 800x134, groups
   area 799x85, group content 160x84 -- and a ~160px group is right for one tab with one small group. My
   original reading of the screenshot was wrong.

1. **FIXED 2026-08-18: Krypton's popup layer (the File-tab FailFast).** Clicking the ribbon's File tab
   aborted the process on `Debug.Assert(popup.IsHandleCreated)` in `VisualPopupManager.StartTracking`.
   Root cause: `VisualPopup` is a Control that floats itself via `PI.ShowWindow(Handle, SW_SHOWNOACTIVATE)`
   -- a user32 call the shim no-ops against a fake handle -- so no popup in either suite ever appeared:
   eight types derive from that one base (context menus, tooltips, app menu, collapsed-group popups, QAT
   overflow, autocomplete), and 23 sites call PI.ShowWindow.

   Per the standing directive the mechanism went into Majorsilence.Forms: **`Control.SetTopLevel(bool)` /
   `GetTopLevel()`** (Control.TopLevel.cs), the WinForms contract ToolStripDropDown itself uses to float --
   a visible top-level control is hosted in a `PopupWindow` at its own Bounds (screen coordinates), goes
   live synchronously (`CreateControl`, because Krypton asserts IsHandleCreated immediately), hides with
   the generic popup dismissal (host.VisibleChanged reflects back into the control's Visible), and takes
   its window down on Dispose (Krypton dismisses popups by disposing them). Krypton's share is two lines
   in the one base class, via the standard-toolkit bridge script: `SetTopLevel(true); Visible = true;`
   replacing the ShowWindow call.

   Trap for later: `Visible = true` alone cannot trigger hosting -- controls default to the visible STATE,
   so the assignment is a no-op transition, and the Visible GETTER reads false for any parentless control.
   SetTopLevel consults the state flag directly.

   Still open in this area: `VisualPopupManager`'s dismissal runs on an `IMessageFilter` fed by Win32
   messages, which nothing here pumps -- outside-click dismissal currently rides on the generic
   popup-deactivation close instead, and stacked popups (submenu on popup) are untested. Pinned by
   `TopLevelControlTests` (5 tests).

   **Visually confirmed 2026-08-18.** Clicking File now opens the app menu popup instead of aborting.
   It renders showing only a "Recent Documents" header and no command list -- confirmed via a headless
   reflection probe to be correct, not a regression: `ShowAppButtonMenu`'s view tree has the command
   column (`ViewLayoutMenuItemsPile` -> `ViewLayoutStack`) laid out at Width=0 with zero children,
   because `KryptonRibbonExtendedExample` never populates `RibbonFileAppButton.AppButtonMenuItems` --
   grepping the whole Examples project for `AppButtonMenuItems`/`AppMenu` turns up nothing. The Recent
   Documents column, which the example also never adds entries to, correctly lays out at its configured
   250px minimum width and shows just its header. Nothing to fix here without adding menu items to the
   example, which is app content, not a port defect.

1. **OPEN: remaining ribbon defects**, now that text size is no longer the cause -- button captions
   overlapping their images, the gallery rendering as a thin vertical strip, and the app button drawn
   overlapping the tab. Re-examine against a fresh screenshot before theorising: the font fix moves every
   text-driven dimension, so some of these may already be gone.

1. ~~**KryptonRibbon lays out collapsed.**~~ (superseded by the two entries above) The whole ribbon renders at a fraction of its size: the tab strip
   shows a floating "Tab" label, the group is squeezed into a ~150x80 box with its two buttons overlapping
   their captions, and the group caption sits below the group instead of inside it. The ribbon band is also
   painted the dark theme colour while the form below it is light, so the band's own background is being
   filled but its content is not measured. Suspect the ribbon's measurement pass -- it is the one control
   family with its own multi-level layout engine, and a zero/degenerate size propagating down would produce
   exactly this. **Affects: KryptonRibbonExtendedExample, and Ribbon Extended in the Landing grid.**
2. **Group-box captions and check-box labels are near-invisible.** On MessageBoxExample the group captions
   ("Message Content Type Options", "Caption", "Icon", "Buttons", "Options") render as white text on a light
   strip, and the check-box labels ("Show Optional CheckBox", "Is optional checkbox checked?",
   "MessageBoxOptions.RightAlign") as light grey on light grey. Both are contrast, not layout -- the text is
   in the right place. Note this is the same *shape* of finding as the earlier dark-theme investigation that
   turned out to be upstream palette data, so establish whether the palette or the renderer picks the colour
   before changing anything.
3. **MessageBoxExample's top-left controls sit outside their group.** "Caption:", "Message Text:" and the
   "Fill Text" button are drawn over the form background above the group box, with a stray horizontal rule
   beside them, and the form is far wider than its content needs.
4. **ToolStripItems has a mostly empty body** with "kryptonSlider1" as bare text bottom-left and a slider
   drawn half off the form.

### Stop clicking; construct every form headlessly

`tools/Majorsilence.Forms.ExampleSmoke` instantiates all 35 example Forms on the headless backend and reports
which throw. Built after the third round of launch -> click -> crash -> fix -> rebuild, each round finding
exactly one bug because a crash hides every bug behind it. First run: **29/35**. After the fixes below,
**34/35**, and three of them were only reachable because the harness gets to forms a human has not clicked yet.

Constructor-time failures it found, by where the fix belonged:

- **Majorsilence.Forms.** `MenuItemCollection.Add(string, ...)` built a bare `MenuItem`, so
  `ToolStripItem item = menu.DropDownItems.Add(text)` compiled and then threw `InvalidCastException` --
  a cast I had added to the fixup script to make it compile, which was the wrong layer to fix it at. It now
  builds a `ToolStripMenuItem`, which IS a MenuItem here and IS a ToolStripItem, so both typings hold. One
  pinned test (`StripHierarchyTests.The_collection_holds_plain_menu_items_too`) was over-specified and
  asserted the old behaviour; its real point -- the collection holds plain MenuItems too, which key lookup
  has to tolerate -- still passes via the separator.
- **Majorsilence.Forms.** `Drawing.Font` had no `TypeConverter`, so settings storage fell back to XML
  serialization, which needs a parameterless constructor a font cannot have. A capable `FontConverter`
  already existed and simply was not attached to the type. `KryptonInputBoxExtendedExample` threw on its
  first settings read until it was.
- **Upstream: a third event-raise syntax.** C# lets you raise an event by invoking the delegate directly --
  `ItemsPositioned(this, e);`, no `.Invoke` anywhere -- which the earlier guard passes could not see. 2,340
  lines in the suite match that shape and nearly all are ordinary method calls, so the rewrite is gated on
  the identifier being declared as an event *in the same file*. 17 files. Two related traps: the pass was
  skipping any file without `.Invoke(` in it (which hid the Calendar's raises entirely), and delegates
  initialised `= delegate { }` are never null by design and must be left alone.
- **Upstream.** `CircularProgressBar.RecreateBackgroundBrush` disposes its brush before creating it.
- **Upstream rot.** `NaviLayoutEngineOffice` reads four menu captions from a resx that is not in the repo and
  whose strings are in no resx at all; the hard-coded ResourceManager base name outlived the file. Captions
  substituted literally -- a null caption renders blank even when it does not throw.

**Still failing (1/35), deliberately not fixed:** `TreeGridViewExample` needs `SQLite.Interop.dll`.
`System.Data.SQLite` ships Windows-only native interop; making it run means moving the example to
Microsoft.Data.Sqlite/SQLitePCLRaw, which is a change to third-party data access rather than a gap in this
library. Flagged for a decision, not silently swapped.

**Watch for stale assemblies.** The Extended projects write to a shared `Bin/`, and an incremental build was
observed leaving an old assembly in place -- which reported a failure already fixed, twice. `--no-incremental`
after editing Krypton sources.

**The Calendar example: guard the consumer or fix the field?** Its constructor reaches its own `Days`
array twice before allocating it -- once via `HighlightRanges`' setter (`UpdateHighlights`) and once via
its item collection's `CollectionChanged` (`CalendarRenderer.PerformItemsLayout`) -- because `_days` is not
allocated until a `SetViewRange` call further down the constructor. Guarding the first consumer just moved
the crash to the second, which is the tell: initialise `_days = []` instead. One line, fixes every present
and future consumer, and says what the code means -- before a view range is set there are no days, and "no
days" is an empty sequence rather than null. The per-consumer guard was reverted in favour of it.

**Found by looking at a screenshot, not by any gate:** every dialog button rendered its caption with a
literal ampersand -- `&OK`, `C&ancel`, `N&o`. Krypton's `AccurateText` sets
`StringFormat.HotkeyPrefix = Show` and draws through GDI+ `DrawString`, and this fork's `DrawString` read
the format's alignment but ignored its hotkey handling. Now honoured in `Graphics`, in BOTH directions:

- draw strips the prefix and underlines the mnemonic character with a one-pixel rule below the baseline
  (a rule, not an underlined font -- only one character is underlined);
- measure strips it too, because sizing a button from its raw caption reserves space for a character that
  never appears, and pushes centred text off-centre by that width.

`Mnemonics.Parse` already existed and does the parsing, including `&&` -> literal `&` and a trailing `&`
naming no mnemonic. Pinned by `HotkeyPrefixTests` (6 tests, one comparing rendered pixels between Show and
Hide, since the underline is the only difference between them). Worth noting the assertion that first
failed was the test, not the code: `A&&B` is an escaped ampersand and so is legitimately WIDER than `A&B`.

**Verifying a window opened, when screenshots are awkward:** `osascript -e 'tell application "System Events"
to tell (first process whose name is "Examples") to get {name, position, size} of every window'` lists them by
name. `screencapture -R` coordinates did not agree with those positions on a multi-display setup, so the
window list was the reliable check.

**Cascade warning:** the per-cause count is not monotonic. Each root-cause fix unblocks a project that was
never reached before, so the total rises. It went 172 -> 158 -> 64 -> 38 -> 31 -> 21 -> 13 -> 8 -> 256 -> 0;
the 256 was the `Examples` app's own code compiling for the first time. Measure causes, not instances.

## 2. TestForm (the demo)

Blocked only on `Cyotek.Windows.Forms.ColorPicker` (forces the Windows platform, NETSDK1136). Remove
the package (`--map` removePackages, or hand-edit the csproj) and replace its use sites — MF's
`ColorDialog` covers the dialog uses; panel-embedded picker controls can become a stub panel first.
Then run it: see the memory notes on launching from the output dir and the screenshot workflow.

## 3. StartScreen runtime — items 1 and 2 FIXED 2026-08-14; item 3 open

TestForm launches, themes and renders, but StartScreen's anchored children are invisible. Probe at
`scratchpad/kprobe` (in-process AutomationSession dump; run with the Win32 shim dylibs copied into
`runtimes/osx/native`). Three pinned facts, all Majorsilence.Forms-side:

1. **[FIXED]** Two library fixes: the Avalonia backend's pre-open `ClientSize` now answers with the
   pending `Width`/`Height` instead of Avalonia's invented default, and `LayoutAnchoredControls` skips
   the pass against a degenerate DisplayRectangle for ALL containers (was AutoSize-only) -- a themed
   form's root panel reports 0x0 transiently mid-construction, the pass collapsed every anchored child
   against it, and the next re-init laundered the collapse into the stored deltas. Pinned by
   `KryptonPortParityTests.AnchoredChild_SurvivesATransientlyCollapsedParent`. Original diagnosis:
   anchor deltas captured against the backend's default window size (1440x675 here) while
   `InitializeComponent` runs, so when the designer's `ClientSize` (876x729) finally applies, anchored
   children collapse: `876 - 12 - (1402 - 538) = 0` — the observed zero width, exactly. WinForms avoids
   this by deferring anchor capture until `ResumeLayout`, after the final size is set. Fix direction:
   `UpdateAnchorInfo` must not capture while the element's container has layout suspended (or must
   re-capture on resume). See the long comment already in `DockAndAnchorLayout.UpdateAnchorInfo` — this
   is the nonzero-but-wrong variant of the degenerate-capture problem it describes.
2. **[CLOSED — not a bug]** The 902x633 was StartScreen's own ctor: `this.Size = new Size(902, 633)`
   deliberately overrides the designer. With honest pre-open reads it became traceable in one run.
3. **Post-show programmatic `Form.Size` writes are silently ignored even for a plain Form** (Avalonia
   backend path). Independent bug; the probe demonstrates it.

[FIXED 2026-08-14] The invisible button text was GraphicsPath figure semantics in
Majorsilence.Forms.Drawing.Common: GDI+ connects segments appended to an open figure with implicit
lines, and AddArc/AddLine/AddBezier didn't -- so a rounded border built the canonical GDI+ way (four
corner arcs, edges implied) enclosed no area, Region(path) rasterized empty, and Krypton's
ViewDrawCanvas clipped every rounded-corner control's content to nothing. Palette-dependent because
square-border palettes never build that path. Pinned by GraphicsPathFigureTests. Diagnosed via the
offscreen frame probe (scratchpad/kprobe) + env-gated clip tracing -- that toolchain is the way to
chase any future paint bug.

Older note (superseded): the command-link button FACES don't draw their heading/description text (layout and
chrome are correct; also fixed on the way: `ShowFocusCues` made protected-internal so Krypton's
NonPublic reflection finds it, and internal `Control.PaintTransparentBackground(PaintEventArgs,
Rectangle, Region)` added -- both reached by reflection, so absence was a runtime NRE, not a compile
error. Peel the next layer the same way: run TestForm, read the PaintFrame stack, fix, repeat.)

[FIXED 2026-08-14] Double-open on button click: MF raised Click unconditionally on pointer-up, but a
control that raises its own click (Krypton's view controllers) sets ControlStyles.StandardClick=false
precisely to suppress the standard raise. RaiseClick/RaiseDoubleClick now honour the styles; pinned by
KryptonPortParityTests.RaiseClick_HonoursStandardClickStyle.

Dark-theme contrast, measured (2026-08-14): the PALETTES are exonerated -- PaletteOffice2010Black and
PaletteMicrosoft365Black genuinely specify light-gray button faces (RGB 189/169) with DARK text
(70,70,70); that is Krypton's authentic dark-theme design. The divergence is that rendered text comes
out near-WHITE on those palettes: prime suspect is AccurateText.DrawString's brush-type fallback --
`brush is SolidBrush ... is LinearGradientBrush ... else GetContentShortTextColor1(LabelNormalControl)`
-- which on a dark global palette returns near-white. If the `is` tests fail (brush subtype from
CreateColorBrush?) every text draw silently takes the fallback colour. Verify with the offscreen probe:
trace which branch fires under Office2010Black, then fix the brush-type dispatch or the fallback.

Office 2007/2010 Black contrast, RESOLVED as palette-faithful (2026-08-14): traced end to end --
Krypton passes SolidBrush(Color.White) for command-link text, and PaletteOffice2010Black itself
specifies ButtonCommand text=White on back1/back2 = RGB 189/169 light gray, colorStyle=Linear. MF
renders exactly the palette's numbers; white-on-light-gray is the palette data. Command links are a
newer Krypton.Utilities control, likely under-tested against the old dark palettes upstream. Options
if it matters: verify on real WinForms/Windows for an upstream report, or have TestForm set
CommandLinkTextValues colors per-theme. NOT an MF rendering bug.

Still open, user-reported: some THEMES still paint badly (compare palettes side by side; the fixed
GraphicsPath/clip machinery may not be the whole story for gradient-heavy palettes), and assorted
visual glitches on child forms (BadgeTest shows minor artefacts). Chase with the offscreen frame probe.

Also observed: `WindowBase.RenderFrame` invokes `OnResize` during first paint — a Krypton override that
calls Invalidate from OnResize could re-enter; keep in mind when testing fix (1).

## 3b. Migrator hardening (found by this port)

- **Global-alias idempotency (CS1537):** `AddBclPreferenceAliases` adds per-assembly `global using X = …`
  aliases per FILE, so a project with two global-usings files gets duplicates — and a re-run duplicates
  within one file. Needs project-scoped awareness or an already-present check across globals files.
  Test case: Krypton.Utilities' `Globals/GlobalDeclarations.cs` + `Global/Globals.cs`.
- `--output` still omits implicit `Directory.Build.props`/`.targets` and binary assets.

## 4. Library follow-ups (each small, gate-checked)

- **`RichTextBox : TextBox`** here; upstream they are siblings under `TextBoxBase`. The bridge script
  papers over one consequence (pattern-arm order). Rebasing is the real fix; measure blast radius first.
- **`ErrorProvider.ContainerControl`** cannot accept a Form (dropped by the script at one site). If more
  sites appear, consider what the provider actually needs from it here.
- **Baseline triage:** `tests/.../ControlWindowParityBaseline.txt` (~190 entries) — classify each as
  "belongs on a window" (add it; the Krypton port pulled ~10 off the list this way) or "genuinely N/A"
  (annotate). The test names any new gap automatically.
- **Mobile/browser audio:** desktop audio is real now (`Media/NativeAudio.cs` spawns the OS player;
  see COMPATIBILITY_MATRIX "Majorsilence.Forms.Media"). Android/iOS/wasm stay silent until a backend
  supplies a native path -- `NativeAudio.LauncherOverride` is currently the only seam, promote it to a
  proper backend hook when the Uno backend grows audio.
- **Drop-down open-path events:** `DropDownOpening/Opened` fire via `ShowDropDown()` but not when the
  strip opens the menu from a click (`MenuItem.ShowDropDown` doesn't know about them).

Gates for every task: Release build (analyzers), `MF_HEADLESS_SCALE=2 dotnet test`, ApiDiff `--check`,
and the two baselines (regenerate deliberately, never to silence).
