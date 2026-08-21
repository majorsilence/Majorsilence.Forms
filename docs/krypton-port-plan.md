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
   0x0 until something paints -- a headless artifact, though it did mean pre-paint layout of docked children
   was wrong (fixed separately, see below). Second, `Segoe UI` reporting a line height exactly equal to
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

1. **FIXED 2026-08-18: the root adapter was sized only by the paint pass.** Found while chasing the
   ribbon (above) and split out because it is not a ribbon problem at all. `WindowBase.RenderFrame` was
   the only thing that ever gave the root `ControlAdapter` its bounds, so until the first frame painted a
   window's client area was 0x0 and every docked or anchored child laid out against nothing. Consequences:
   a `Load` handler reading a docked panel's `Width` got 0, an explicit `PerformLayout` produced a layout
   the first paint then silently redid, and headless code -- which never paints -- could not lay a window
   out at all. The ribbon probe had to reflect in and call `adapter.SetBounds` itself to measure anything;
   it no longer does, and reports the ribbon at 800x134 inside an 800x450 form straight after `Show()`.

   WinForms has no such window: a form's client rectangle is real as soon as it has a size, well before
   anything is drawn. Per the standing directive the fix is here, not in the port -- `SyncAdapterBounds`
   (WindowBase.cs) is now shared by the paint path and three pre-paint ones: the show sequence (before
   `Load`, as in WinForms), `PerformLayout`, and `ResumeLayout(true)`. The resize bookkeeping lives inside
   it, so `OnClientLayoutChanged`/`OnResize` still fire exactly once per real size change -- just at
   `Show()` now rather than at first paint.

   Two things that constrain any future change here. The sizing pass at `Show()` runs before `shown` is
   set, and `ControlAdapter.OnLayout` forwards to the window's own `OnLayout` only once it is -- so the
   show sequence runs one further `adapter.PerformLayout ()` after setting it, which is the pass the first
   painted frame used to be responsible for. A `Form` subclass that decides anything in `OnLayout`
   (DockPanelSuite's `FloatWindow` sets its own `Visible` there) depends on that. And a frame-hosted form
   -- MDI child or panel host -- is skipped (`IsFrameHosted`, now virtual on `WindowBase`): it draws
   through its host's `RenderFrame` at whatever size the host allots, which is not its own client size.
   Pinned by `PrePaintDockLayoutTests` (4 tests, all four failing before the fix).

1. **CLOSED 2026-08-18: the three remaining ribbon defects were two font bugs and one piece of example
   content.** Re-examined against a fresh render, as this entry said to. Two are simply gone, both
   downstream of the font fix: button captions no longer overlap their images (they sit cleanly below
   them), and the app button no longer overlaps the tab (`File` at 54x21 and `Tab` sit side by side).

   **2026-08-19, after a GUI review raised the app button again: the closure stands, but not for the reason
   originally given.** A demo session showed the app button as a round orb straddling the caption/ribbon
   boundary, which looked like the old overlap defect. It is not. Krypton's Office2007 app button is a fixed
   39x39 orb built from `_sizeTop = 39x22` and `_sizeBottom = 39x17` (`ViewDrawRibbonAppButton`) -- it is
   DESIGNED to sit 22px in the caption and protrude 17px into the ribbon. Measured under that palette: the
   caption area is `{0,0,800,28}`, the orb `{X=4,Y=6,W=39,H=39}`, and horizontal space is properly reserved
   (`ViewLayoutRibbonAppButton` is 43px wide in both the caption and tabs areas, with the first tab starting
   at x=49). So it overlaps no tab. The salmon-tinted selected tab is SparkleOrange being orange, not a
   colour bug.

   **The real lesson is about the harness, and it is worth internalising: the ribbon's SHAPE comes from
   Krypton's application-global palette, and one example changes it for the whole process.**
   `MessageBoxExample.Designer.cs` assigns `kryptonManager1.GlobalPaletteMode = PaletteMode.SparkleOrange`,
   and `GlobalPaletteMode` is global by design in Krypton -- so a demo session that visits MessageBox sees
   every later form in Sparkle, where `RibbonShape` resolves to `Office2007` (orb). A fresh headless process
   gets Krypton's default, `GLOBAL_DEFAULT_PALETTE_MODE = Microsoft365Blue`, where the same ribbon resolves
   to `Microsoft365` and draws a FLAT "File" tab on a light band instead. Two legitimately different
   layouts. Neither is a port defect, and comparing a headless render against a demo screenshot without
   fixing the palette compares two different things.

   Both probes now take `PALETTE=<PaletteMode>`, which sets the global palette before the form is built;
   `PALETTE=SparkleOrange` reproduces the demo session's ribbon exactly. **Use it whenever a headless render
   is being compared against something seen in the demo.**

   The third -- "the gallery renders as a thin vertical strip" -- is not a defect. `KryptonGallery`
   measures 22x65 with a preferred size of 22x45, which is its scroll-button column and nothing else,
   because `KryptonRibbonExtendedExample.Designer.cs` sets `kryptonRibbonGroupGallery1.ImageList = null`.
   Upstream does the same thing with that input: `ViewLayoutRibbonGalleryItems.SyncChildren` creates one
   child per image and so creates none, and `GetPreferredSize` returns `Size.Empty` plus padding when it
   has no children. Windows would draw the same 22px strip. Same shape of finding as the empty app menu
   above: an unconfigured control rendered faithfully.

   **How to look at the ribbon without the GUI.** The scratchpad probe now takes `SHOT=<path>` and writes
   a PNG through `HeadlessRenderer.CapturePng` -- a real picture, no window server, no clicking through
   the landing grid, and no CVDisplayLink to refuse to start. `DUMP_CONTROLS=1` walks the Controls
   collection as well as the view tree, which is the only way to see a gallery or custom control: the view
   tree stops at `ViewLayoutControl`, whose contents are a hosted child Control rather than more views.
   Both of those found things the view dump alone could not.

1. **DIAGNOSED 2026-08-19, not yet a fix: the ribbon's own 28px caption area is Krypton's documented
   fallback for a caption it could not integrate into.** The strip is `ViewDrawRibbonCaptionArea` at
   y=0..28, and the stray chevron in it is `ViewDrawRibbonQATExtraButtonMini` (13x22). The whole chain is
   now pinned by reflection rather than guessed at:

   - `UpdateVisible` is `Visible = !_integrated && (AppButtonVisible || QATLocation == Above ||
     RibbonContexts.Count > 0)`. Measured: `QATLocation = Above` and `AppButtonVisible = True`, so the
     strip shows; `QATButtons.Count = 0` and `RibbonContexts.Count = 0`, so it has nothing to put in it.
     That is the whole of why a 28px band appears with one chevron in it. The caption area's own app
     button is correctly hidden -- this ribbon shape draws the app button as the "File" TAB
     (`ViewLayoutRibbonAppTab`, 54x21), which is what satisfies `AppButtonVisible`.
   - `_integrated` is false, and NOT because the bridge failed: `CaptionArea.KryptonForm` resolves to the
     example form and `PreventIntegration` is false. Integration needs
     `_kryptonForm.RealWindowBorders.Top >= MIN_INTEGRATED_HEIGHT`, which is `FactorDpiY * 26` = 26.
     `RealWindowBorders` measures **{0,0,0,0}**.
   - It is zero because `CommonHelper.GetWindowBorders` derives it from
     `PI.AdjustWindowRectEx(ref rect, style, false, exStyle)` -- a user32 call the shim no-ops, leaving the
     rect at zeros. **Same class of defect as the `PI.ShowWindow` popup bug**: Krypton asking Win32 for a
     geometric fact and silently getting nothing.

   **Do not just make `RealWindowBorders` honest.** Integration makes the ribbon inject its app button and
   QAT into the KryptonForm's caption via `_kryptonForm.InjectViewElement(...)`, and KryptonForm's own
   chrome does not appear to render here at all -- the example form's only child control is the
   `KryptonRibbon`, and a Majorsilence.Forms `Form` draws its own title bar rather than a Krypton view
   tree (`Form : WindowBase`, so `VisualForm`'s Control-based chrome has nothing to paint into). Forcing
   integration would then remove the strip AND lose the QAT with it -- worse than the fallback. So the
   prerequisite is KryptonForm chrome under Majorsilence.Forms; answer that first, and treat the honest
   `AdjustWindowRectEx` as the second step, not the first.

   Worth noting the fallback is arguably correct today: on screen the strip sits under the real window
   title bar, which duplicates a caption, but nothing is broken and nothing is unreadable.

1. ~~**KryptonRibbon lays out collapsed.**~~ (superseded by the two entries above) The whole ribbon renders at a fraction of its size: the tab strip
   shows a floating "Tab" label, the group is squeezed into a ~150x80 box with its two buttons overlapping
   their captions, and the group caption sits below the group instead of inside it. The ribbon band is also
   painted the dark theme colour while the form below it is light, so the band's own background is being
   filled but its content is not measured. Suspect the ribbon's measurement pass -- it is the one control
   family with its own multi-level layout engine, and a zero/degenerate size propagating down would produce
   exactly this. **Affects: KryptonRibbonExtendedExample, and Ribbon Extended in the Landing grid.**
2. **FIXED 2026-08-18: group-box captions and check-box labels were near-invisible -- and it was neither
   the palette nor the renderer.** This entry said to establish which of those picked the colour before
   changing anything; it was a third thing. Sampling the rendered pixels settled it in one step: inside a
   check box the pixel was `F0F0F0` and one pixel outside it, in the same group panel, `636C87`. The light
   strip was therefore an opaque fill the exact size of each control, not a text colour -- the palette's
   light text was correct all along and was landing on it.

   The fill came from `Control.PaintTransparentBackground`, which exists **only to be found by
   reflection**: WinForms declares it private, and Krypton's `VisualControlBase` looks it up on
   `typeof(Control)` and invokes it on every paint of every label, check box, radio button and group
   caption. It was present here but stubbed to fill the nearest ancestor's `BackColor` -- which looks
   equivalent and is not, because a Krypton container paints itself from its palette and leaves
   `BackColor` at the `SystemColors.Control` default. It now samples the parent's already-painted PIXELS,
   which works out to a blit: a child is drawn into its own buffer from inside its parent's
   `PaintChildren`, after the parent has painted its own background into that buffer. No recursion, and it
   reproduces palette-painted parents exactly.

   Two pipeline properties this rests on, so changing either breaks it: the parent's background pass
   overwrites the previous frame's blitted children (so what is sampled is never last frame's picture of
   this same control), and a child is never repainted without its parent repainting first. Pinned by
   `TransparentBackgroundPaintTests`, one of which performs Krypton's exact reflection lookup so a rename
   or signature change fails a test instead of silently washing out every themed caption again.

3. **FIXED 2026-08-18: single-line text boxes and numeric up/downs collapsed to a 2-3px rule.** This is
   what the entry below called "a stray horizontal rule". `Control.GetPreferredSizeCore` reports the bounds
   that were explicitly SET, so a text-entry control whose height had been laid out to zero went on
   *asking* for zero -- a feedback loop, not a one-off bad measurement. Krypton's `KryptonTextBox` and
   `KryptonNumericUpDown` host one of these and take their own height from it, so the pair settled at 2px
   and 3px. A text-entry control's height comes from its font, and `PreferredHeight` already computed it
   correctly with nothing consuming it; `TextBoxBase` and `NumericUpDown` now override
   `GetPreferredSizeCore` to fill in a height of zero from it. Only zero is filled in, and not for a
   multiline box -- a designer that sized the control still wins, which is what keeps this from resizing
   layouts that were already right. Pinned by `PreferredHeightSizingTests`.

3b. **CLOSED 2026-08-19: MessageBoxExample's "Show" button does nothing because the example does nothing.**
   Raised by GUI review. `kbtnShow_Click` is empty -- every line of it is commented out, and it was already
   that way before the migration (`git show aa808a44:.../MessageBoxExample.cs`), so the button is inert on
   Windows too. Nothing to fix in Majorsilence.Forms or the port. Worth remembering when this example comes
   up again: the one control most likely to be reached for is wired to nothing.

4. **CLOSED 2026-08-18: MessageBoxExample's top-left controls are where the example puts them.** The stray
   horizontal rule beside them was the collapsed text box above, and it is gone. The rest is the example's
   own layout, confirmed in its designer file: `kryptonLabel1`/`kryptonLabel2` ("Caption:", "Message
   Text:") are added to `kryptonPanel1` directly rather than to any group box, and `ClientSize` is
   assigned `1585x437`, so the form really is that much wider than its content. Not a port defect.

5. **FIXED 2026-08-18: ToolStripItems was empty because `ToolStripControlHost` never hosted anything.**
   The type held a `Control` reference and forwarded properties to it, but never parented the control or
   gave it a position, so the hosted control was never displayed and what appeared instead was the item's
   Text drawn by the strip's renderer -- which is why a hosted slider read as the literal words
   "kryptonSlider1" in the corner. `SetBounds` (now virtual on `MenuItem`, as `ToolStripItem.SetBounds` is
   upstream) parents the control into the strip and moves it to the item's bounds. The renderers also stop
   drawing an item's own background and text for a control host: with the hosted control's `BackColor` set
   to `Transparent` -- which this example does -- the renderer's text showed straight through it, so the
   slider came out with "kryptonSlider1" printed across its track. Affects every
   `ToolStripControlHost`, `ToolStripTextBox` and `ToolStripComboBox` included. Pinned by
   `ToolStripControlHostingTests`.

   Also checked while here: `statusStrip1.Location` really is `(0, -1)` in the designer, so that is not
   ours.

6. **FIXED 2026-08-19: choosing Tools -> Colors dismissed the menu instead of dropping the colour
   palette.** Found by GUI review, and the diagnosis went through two wrong candidates before the right
   one, both worth recording so they are not re-tried.

   Not the hosting: with the menu open the item measures `{X=1,Y=45,W=162,H=20}` and the
   `KryptonColorButton` is parented into the `MenuDropDown` at exactly those bounds, visible. Not
   `MenuDropDown.OnMouseClick` either -- `Control.RaiseMouseDown`/`RaiseClick` hand the event to
   `Controls.FindVisibleChildAt` and RETURN, so with the hosted control covering that row the menu's own
   click handler never runs. (A guard was written there and then reverted for that reason: dead code for
   this case. Do not re-add it.) And not stacked popups: driving `KryptonColorButton.PerformDropDown ()`
   with the menu open leaves the menu up and `VisualPopupManager.CurrentPopup` set to the palette's
   `VisualContextMenu`, so that machinery works.

   The actual cause was in `Control.RaiseMouseDown`, in the branch that dismisses an open menu -- it
   exempted the menu CONTROL and nothing hosted inside it:

       if ((this as MenuBase)?.GetTopLevelMenu () != Application.ActiveMenu || Application.ActiveMenu is null)
           Application.ClosePopups (true, false);

   Because mouse-down routes to the deepest child, the hosted `KryptonColorButton` ran this check itself,
   was not a `MenuBase`, and closed the very menu it was sitting in -- before its own handler could show the
   palette. Replaced by `IsWithinActiveMenu ()`, which walks up the parent chain looking for the active
   menu. Pinned by `MenuHostedControlClickTests`: one test that the hosted row no longer dismisses, one
   that a press OUTSIDE the menu still does (the behaviour the old check existed for). Note those tests
   must set `Application.ActiveMenu` themselves -- opening a drop-down via `Selected` does not, and without
   it the whole dismissal path is inert and proves nothing in either direction.

7. **OPEN (narrowed 2026-08-18): a Krypton button hosted in a tool strip draws wider than it measures.**
   All that is left of the ToolStripItems entry above. `KryptonColorButton` in `toolStrip1` reports a
   `PreferredSize` of 53x20 and is laid out at 53px, then draws an icon, the caption "Color" and a
   drop-down arrow into it, so the caption clips to "Co". The hosting is correct now, so this is Krypton's
   own content measurement for that control, reached through our text metrics -- start by comparing what
   `GetPreferredSize` measures against what the renderer then draws, rather than at the strip.

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

   **FIXED 2026-08-19, and the description above was wrong.** The write was never ignored. A GUI probe
   (`scratchpad/sizeprobe`: a real Avalonia window that resizes itself and logs every read-back) caught the
   resize landing one tick late --

       2. straight after Size=640x480   Size={400, 300}    <- stale
       3. one tick later                Size={640, 480}    <- landed

   -- so the defect is the SYNCHRONOUS read-back, not the resize. Assigning Avalonia's `Width`/`Height` is
   a request it reconciles on its next layout pass, whereas WinForms resizes through `SetWindowPos` and
   reads back the new size immediately. Ported code that sets a size and then uses `Width`/`Height` in the
   same breath therefore computed against the previous one. It never reproduced headlessly because the
   headless backend simply stores the value.

   The Avalonia host's `IWindowBackend.Size` setter now records a `_pendingClientSize`, `ClientSize`
   answers with it until the resize lands, and `OnSurfaceSizeChanged` clears it -- so a USER dragging the
   window edge is still reported honestly instead of being masked by the last programmatic size. Verified
   with the same probe: every read-back now reflects its write, and later ticks still agree with Avalonia.
   No unit test, because this is backend behaviour the headless harness cannot express -- the probe is the
   evidence, and it is worth keeping for the next backend-geometry question.

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
- **[TRIAGED 2026-08-19] Baseline triage:** `tests/.../ControlWindowParityBaseline.txt`. All 194 entries
  are now grouped in the file itself by whether they should ever be closed -- six "N/A" groups (no parent to
  dock against, no Win32 handle to recreate, MF-internal plumbing, layout-engine internals, child-scrolling,
  child-selection) and eight "WORTH CLOSING" groups ordered by how likely ported code is to hit them
  (state/geometry, data binding, accessibility, context menu + IME, designer Reset* pattern, validation,
  gestures/drag-drop, static helpers). **25 entries came off the list in the same pass**: the Control EVENTS
  a WinForms Form inherits (MouseClick, MouseDoubleClick, BackColorChanged, ControlAdded, PreviewKeyDown and
  friends) now forward from `WindowBase` to the root `ControlAdapter`, the same shape as the existing
  `DoubleClick`/`Layout` forwards -- `form.MouseClick += ...` did not previously compile. 169 remain.
  Pinned by `WindowControlEventForwardingTests`, which checks the events actually ARRIVE: a forward wired to
  the wrong object compiles and silently never fires. Caution: regenerating the baseline with
  `MAJORSILENCE_WRITE_CONTROL_WINDOW_BASELINE=1` FLATTENS the grouping and loses the triage; edit by hand.

  **2026-08-20: the first "worth closing" group is closed -- 169 down to 152.** State and geometry, the
  group ranked most likely for ported code to hit: `Created`, `CreateControl`/`OnCreateControl`, `Contains`,
  `HasChildren`, `PreferredSize`, `GetPreferredSize`, `GetContainerControl`, `GetStyle` (the counterpart of
  the `SetStyle` that already forwarded, so the pair is finally usable), `UseWaitCursor`,
  `LogicalToDeviceUnits`/`DeviceToLogicalUnits`, `ScaleBitmapLogicalToDevice`, `PerformLayout/2`,
  `Invalidate/2`, plus `Container` and `DesignMode` as the constant answers Control gives. Pinned by
  `WindowStateGeometryParityTests` (7 tests) which check the answers concern the right rectangle and the
  right control tree -- the parity test only checks EXISTENCE, so a forward wired to the wrong object
  satisfies it and still lies.

  Two entries were moved to N/A groups rather than closed, with the reasoning recorded in the file:
  `Site` (Control shadows `Component.Site`; doing that on a window would hide the real one the designer and
  `IContainer` plumbing read through, to gain a property Control only answers nothing from) and
  `FromScreenPoint/1` (a static CONTROL finder; the window equivalent already exists as `Form.ActiveForm` /
  `GetChildAtPoint`).

  Watch for `CS0108` when closing any further group: `PrintPreviewDialog` redeclares a lot of Control
  surface to hide it from the designer, so each addition can shadow one of those and Release treats the
  warning as an error. `UseWaitCursor` needed `new` for exactly that reason, as five events did before it.

  **2026-08-21: data binding and accessibility closed -- 152 down to 141.**

  Data binding (`DataBindings`, `DataContext`, `DataContextChanged`, `ResetBindings`). Worth knowing before
  touching this: binding here is a COMPILE-compatibility surface, not a working facility --
  `Binding.WriteValue` is an empty stub, so no binding moves a value in either direction. The members are
  still wired to the correct objects so that implementing `Binding` later makes them work rather than
  making them wrong, and the split is not obvious: `DataBindings` belongs to the WINDOW (`form.DataBindings
  .Add ("Text", src, "Title")` is a statement about the window's title, and handing back the adapter's
  collection would compile and bind the adapter's Text, which nothing displays), while `DataContext`
  forwards to the adapter because its entire purpose is to be inherited by descendants and that chain
  terminates there. `WindowBase` implements `IBindableComponent` for this, with the interface's
  `BindingContext` routed through an internal `BindingContextCore` that `Form` overrides onto its existing
  public property -- a second property would have shadowed it and then drifted from it.
  `OnDataContextChanged/1` was moved to the already-forwarded On* group rather than added, since the event
  is raised by the adapter and a window-side raiser would double it.

  Accessibility (`AccessibilityObject`, `CreateAccessibilityInstance`, `AccessibleRole`,
  `AccessibleDefaultActionDescription`, `IsAccessible`, `AccessibilityNotifyClients`,
  `QueryAccessibilityHelp`). Window-owned rather than forwarded: a screen reader addresses the window.
  Also a described surface rather than a live one -- nothing reaches a platform accessibility API yet -- so
  `AccessibilityNotifyClients` is a deliberate no-op and had to be recorded in `NoOpStubBaseline.txt` and
  `COMPATIBILITY_MATRIX.md`; `NoOpStubBaselineTests` catches a new empty public void method and will fail
  until it is. Both facts are now in the matrix so the surface is not mistaken for a working one.

  `PrintPreviewDialog` collided twice more (`DataBindings`, `AccessibleRole`). `DataBindings` genuinely
  differs -- it binds the hosted `PrintPreviewControl`, which is what binding that dialog means -- so it
  says `new`. `AccessibleRole` did not: it existed only to change the default to `Client`, so the property
  was DELETED and the default moved into a constructor, leaving one property where callers expect one.
  Prefer that shape over `new` whenever the shadow exists only to change a default.

- **[IMPLEMENTED 2026-08-21] Data binding is live, not a stub.** Found while closing the data-binding
  parity group: `Binding` held its property name and data source and did nothing with them --
  `Format`/`Parse` were `add { } remove { }`, `ReadValue`/`WriteValue` were empty -- so every binding in
  every migrated form compiled, ran, and moved no data. `BindingRuntime.cs` implements the mechanism:
  initial pull on `Add`, source->control via `INotifyPropertyChanged` and `CurrencyManager` position
  changes, control->source via the `<Property>Changed` convention honouring `DataSourceUpdateMode`, real
  `Format`/`Parse`, `FormatString`/`NullValue`/`DataSourceNullValue`, and two-way type coercion.
  17 tests in `BindingRuntimeTests`.

  Three bugs it exposed, all of which had been invisible while nothing was live:
  - `BindingContext` handed back a `CurrencyManager` over a NULL list for every scalar source, so
    `Current` was always null and a binding to a plain object had nothing to read. Scalar sources now get
    a `PropertyManager`, and `BindingManagerBase.Current`/`Count` became virtual so it can answer.
  - `Binding`'s constructor accepted `formattingEnabled` and dropped it on the floor.
  - `ControlBindingsCollection.Add(..., updateMode, ...)` added the binding and THEN set
    `DataSourceUpdateMode`. Once adding became the thing that subscribes, that ordering meant every
    binding created through the designer's favourite overload watched `Validated` instead of the
    property's own changed event and never wrote back. The overload now configures before adding, and the
    property re-subscribes if it is changed on a live binding.

  Worth knowing: `Delegate.CreateDelegate`'s by-NAME overload does not find a private method and returns
  null with `throwOnBindFailure: false`, which is how the write-back half stayed silently unwired through
  the first round of tests. Bind through an explicit `MethodInfo`.

- **FLAKY, not investigated:** `ImageMetadataAndFrameTests.Image_codecs_are_fully_described`
  (Drawing.Common) failed once in a whole-solution `dotnet test` run on 2026-08-21 and then passed on
  re-run and twice in isolation, with and without the changes in flight. Unrelated to the window-parity
  work; it looks like shared state or parallelism across test assemblies. Worth pinning down before it
  wastes someone's afternoon attributing it to their own change.
- **Mobile/browser audio:** desktop audio is real now (`Media/NativeAudio.cs` spawns the OS player;
  see COMPATIBILITY_MATRIX "Majorsilence.Forms.Media"). Android/iOS/wasm stay silent until a backend
  supplies a native path -- `NativeAudio.LauncherOverride` is currently the only seam, promote it to a
  proper backend hook when the Uno backend grows audio.
- **[FIXED 2026-08-19] Drop-down open-path events.** `ToolStripDropDownItem` declared `ShowDropDown` and
  `HideDropDown` with `new` rather than `override`, so they hid `MenuItem`'s. Every caller that actually
  opens a menu holds the item as a `MenuItem` -- the `Selected` setter, `MenuDropDown`'s selection
  tracking, `ToolStripDropDown`'s `Visible` setter, `MenuBase` -- so they all bound to the base method:
  opening a menu by clicking it raised neither `DropDownOpening` nor `DropDownOpened`, and never consulted
  the cancellable `Opening`. The events only fired for code that called `ShowDropDown` on the derived type
  by hand, which is why this looked like a missing raise rather than a dispatch bug. Both are virtual on
  `MenuItem` now and overridden properly. Pinned by `DropDownOpenPathTests` (3 tests, all three failing
  before).

Gates for every task: Release build (analyzers), `MF_HEADLESS_SCALE=2 dotnet test`, ApiDiff `--check`,
and the two baselines (regenerate deliberately, never to silence).
