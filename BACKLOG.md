# Backlog

## Behavioural gaps: the stub audit

**The big one.** Both API-surface gap plans are at zero — every WinForms and GDI+ member that upstream
has, this layer now declares. What a name-level scanner cannot see is whether the member *does* what
WinForms does, and a twelve-area source audit (2026-08-25) found **483 places where it does not**: 41
P0, 223 P1.

**Phases 0-3 have since landed** (2026-08-26 -> 08-28), which changes the headline numbers rather than
retiring them: the keyboard pre-processing chain (`ProcessCmdKey` and friends) is now dispatched, focus
and validation run through one choke point, forms are reusable and their dialogs real, the title bar is
out of the client area, and text is measured at the size it is drawn at. The hollowness baselines moved
with it -- inert events 84 -> 79, unraised events 130 -> 127, stored-only properties 822 -> 812 of 1254.
Phase 3 finished on 2026-08-31 with `W3.6`: `AutoScaleMode.Font` now really scales a container and
its children by the ratio between the designer-recorded font dimensions and the current font's, which
is the correction every migrated designer file was written expecting.
`W5.24` landed the same day: the layout engines were already a faithful port, and the four places that
failed to reach them -- `Panel`'s preferred size, `Control.Scale`, the button family's preferred size and
`GroupBox.AutoSize` -- now do.
Phase 4 (data binding) landed 2026-09-01: the `CurrencyManager` is a live object, `DataRowView`
columns bind, conversion failures stay out of the data, and the `BindingNavigator` is buttons rather
than scenery.
`W5.6` followed on 2026-09-01: `ListView.View` is real, so `Details` -- the most common ListView shape
in a LOB app, and the audit's largest single visual divergence -- renders its header, columns and
subitems instead of a grid of icon tiles.
**What is left is phases 5 and 6:** the rest of the per-control families (`W5.*`, of which `W5.6`,
`W5.17` and `W5.24` are done), and the mechanical sweeps (`W6.1`-`W6.4`; `W6.5` was done 2026-08-31).

Findings, root causes and a phased plan: [`docs/behaviour-gap-plan.md`](docs/behaviour-gap-plan.md).
Per-area detail with both sides cited: [`docs/behaviour-gap/`](docs/behaviour-gap/).


## HiDPI

Covered in CI: the full suite runs under `MF_HEADLESS_SCALE=2` and passes
(`.github/workflows/dotnet.yml`). Nothing outstanding here -- kept as a note on what the failures
turned out to be, because the same mistake is easy to reintroduce.

The suite went from 22 failures at scale 2 to none. Almost all of it was **one confusion: logical versus
device units**. `Bounds`, `MouseEventArgs` and `GetTabRect` are logical; `ClientRectangle`, back buffers
and captured bitmaps are device pixels. The two are identical at scaling 1, so mixing them is invisible
until a scaled display shows up -- and it was mixed in six places:

| Where | What it did |
|---|---|
| Input routing | Child lookup and per-control translation ran in device space, so `MouseEventArgs` reached controls in device pixels and hit tests against logical rectangles missed. |
| `HeadlessRenderer` / `AutomationSession` | Injected logical coordinates straight into handlers that take device pixels, so every synthetic click landed at 1/scale of its target. |
| Renderer preferred sizes | Measured text at the device font size and returned the result as a logical size, so strip items came out scale-times too wide. |
| `TabStrip` / dock layout | Laid children out into the device `ClientRectangle` and stored the result in logical `Bounds` -- compounding once per nesting level (a 400-logical dock produced a 1600-logical tab strip). |
| Dock header hit rects | Stored in device units while mouse coordinates are logical. |
| Several tests | Sampled device-pixel bitmaps using logical control bounds, or asserted scale-1 pixel geometry outright. |
| `Control.RectangleToScreen/ToClient` | Converted the origin through `PointToScreen` (which scales) but passed `Width`/`Height` through unscaled, so every converted rectangle was scale-times too small. Found later than the six above via `RadDocumentTabStrip.ScreenBounds`, where a tab dropped on its own strip's right half tested as "outside" and tore off instead of reordering. |

Two things worth keeping:

- **`Control.ClientRectangle` is device-scaled while `Bounds` is logical.** That asymmetry is the root of
  most of the above and it is still there -- 81 call sites, 33 of them renderers that genuinely want
  device pixels, so it was not something to flip in passing. `DeviceToLogicalUnits` and the local
  `LogicalClient` helpers convert at each layout site instead. Worth revisiting as its own change.
- **The scale-2 suite must run with `xunit.parallelizeTestCollections=false`.** It is not a scaling
  problem: a test that opens a modal dialog picks its owner from the global `Application.OpenForms`, and
  in parallel it can pick another test's window and wait on it forever. Run serially it finishes in
  seconds. Fixing the isolation properly would let the gate drop the flag.

## Wanted: `--dual-build` for VB

**Status: wanted, not yet started.** `--dual-build` currently converts a VB project the normal
fully-committed way and emits a warning explaining why (`MIGRATION.md`, "Incremental migration"). That
leaves VB shops without the one workflow C# shops get for de-risking a migration: keep building against
real WinForms while the Majorsilence side is brought up, flipping between them with one MSBuild
property. A cut-over is a much harder sell than a switch, so this gap is a real adoption blocker rather
than a convenience.

**The language is not the obstacle.** The stated reason for not offering it -- that `MyType=Empty`
switches off the whole VB "My" application framework and a preprocessor symbol can't toggle that -- is
about the *My framework*, not about conditional compilation. Verified with the .NET 10 SDK: VB happily
accepts conditional compilation directives around `Imports`, compiling either branch depending on the
symbol:

```vb
#If MAJORSILENCE_FORMS Then
Imports Majorsilence.Forms
#Else
Imports System.Windows.Forms
#End If
```

So the import swap -- the entire C# implementation of `--dual-build` -- transfers directly. What does
not transfer is everything the migrator does for VB *because* the My framework went away.

**What it would take:**

| Piece | What it involves |
| --- | --- |
| Conditional project properties | `MyType`, `UseWindowsForms`, the `-windows` TFM suffix and the WinForms-only package references all have to vary with the switch. These are MSBuild properties, so `Condition="'$(MAJORSILENCE_FORMS)' == 'true'"` is the obvious tool -- but whether `MyType` can be flipped this way in an SDK-style `.vbproj` (rather than being baked in by the VB targets) needs proving out first. This is the one genuine unknown. |
| Conditional constructor injection | The implicit parameterless constructor `MyType=Empty` supplies is re-injected as an explicit `Sub New()` today. Against real WinForms that duplicates the compiler-supplied one, so it has to be wrapped in `#If MAJORSILENCE_FORMS Then` -- which is legal inside a class, so this is mechanical. |
| Conditional `My.Resources` accessor | The generated `My Project\Resources.vb` module collides with the real `My.Resources` when building against WinForms. Either wrap the generated module in the same directive, or exclude the file with an MSBuild condition -- the latter is cleaner, since the file is generated wholesale. |
| Remaining `My.*` usage | Unchanged from today: still warn-and-leave. Dual-build does not make `My.Forms`/`My.Settings` work on the Majorsilence side, and shouldn't pretend to. |
| Docs | `MIGRATION.md` currently states dual-build is C#-only, and the training guide on the site repeats it (telling VB teams to plan a cut-over). Both need updating together with the code. |

**Acceptance test:** a VB WinForms project with a form, a designer partial, a `Resources.resx` and at
least one `My.Resources` use, converted with `--dual-build`, that builds *and runs* both with
`MAJORSILENCE_FORMS=true` and without it, from the same source tree, with no manual edits in between.
Anything less than "runs both ways" is not the feature -- the C# version's value is precisely that the
old build keeps working untouched.

## Wanted soon: visual designer support

**Status: wanted, not yet started.** This is a planned feature, not a deferred one — unlike everything
under "genuinely deferred" below, the intent is to build it.

What exists today is the *shape* and none of the surface. `Majorsilence.Forms.Design`
(`src/Majorsilence.Forms/Design.cs`) declares the design-time types WinForms spreads across three
namespaces -- `ComponentDesigner`, `ControlDesigner`, `ParentControlDesigner`, `UITypeEditor`,
`CollectionEditor`, `IWindowsFormsEditorService`, and the `Behavior`/`Glyph`/`Adorner` shapes -- so a
migrated control library that ships a designer per control, adorner glyphs and collection editors
compiles and keeps that code intact. Nothing instantiates any of it at runtime: there is no design
surface, no selection service, and no adorner window, so the verbs and glyphs a designer registers are
never shown.

Turning that into real designer support means, roughly in dependency order:

| Piece | What it involves |
|---|---|
| Design surface host | Something that creates designers for components, owns the `IDesignerHost` service container, and hosts a live control tree in "design mode" rather than running it. `System.ComponentModel.Design.IDesignerHost` is in-box and usable; the host implementation is not. |
| Selection + adorners | A selection service, sizing/moving grips, and an adorner layer above the control surface for the glyphs `ControlDesigner.Adorners` already lets designers register. |
| Property grid integration | `PropertyGrid` exists as a control; it needs to drive `UITypeEditor`/`CollectionEditor` through a real `IWindowsFormsEditorService` so drop-down and modal editors work. Today that interface resolves but nothing provides it, so `GetService` returns null and editors fall back to the plain value. |
| Code serialization | Round-tripping the designer's changes back into `InitializeComponent` in `*.Designer.cs`/`*.Designer.vb`. The `Reset*`/`ShouldSerialize*` members the matrix already tracks exist for exactly this. |
| Editor/IDE surface | Where the designer actually runs. Worth deciding early, because it constrains everything above: an in-app design mode, a standalone tool, or an extension for an existing editor are materially different projects. |

Design-time attributes (`[Designer]`, `[Editor]`, `[DesignerSerializationVisibility]`, `[Browsable]`,
`[Category]`, `[DefaultValue]`) are already carried through migration, so existing consumer code should
not need editing when a surface arrives -- that was the point of keeping the shapes.

Related: the migrator no longer remaps `System.ComponentModel.Design` (it is partly in-box, and the
blanket remap hid `IDesignerHost`); `System.Windows.Forms.Design` and `System.Drawing.Design` still map
to `Majorsilence.Forms.Design`.

## Compiled `.resources`: the legacy (pre-extensions) layout

`RawResourcesReader` (`src/Majorsilence.Forms/RawResourcesReader.cs`) recovers the raw payload of entries
that `DeserializingResourceReader` refuses to hand back — a designer resource written through
BinaryFormatter throws `PlatformNotSupportedException` outright on .NET 9+, which is what left every
migrated `ImageList.ImageStream` null and every toolbar button image missing.

It parses only the layout the **extensions** writer emits (the header names
`System.Resources.Extensions.DeserializingResourceReader`), where each user payload is tagged with a
`SerializationFormat` and length-prefixed — which is what makes a payload extractable on its own, and what
any modern SDK build produces. A `.resources` written by the plain BCL `ResourceWriter` (a prebuilt
.NET Framework-era assembly) stores BinaryFormatter graphs with neither tag nor length, so an entry's
extent is only implied by where the next one starts. Reading those would mean sorting the data-section
offsets and slicing between them; not done, and untested against a real such file, so those entries are
still skipped rather than guessed at.

Also unresolved from the same area: `RawFormat.TypeConverterString` payloads are not converted (a `Font`
recorded that way still falls to the shim path), and `ToolBarButton.ImageIndex`/`ImageKey` remain stubs —
the legacy `ToolBar.Buttons` collection is not rendered at all, unlike `Items`.

## Telerik compat layer: genuinely deferred items

`Majorsilence.Forms.Telerik` (`src/Majorsilence.Forms/Telerik/*.cs`) now covers every heavyweight Telerik
UI for WinForms surface previously tracked here (PDF viewer, rich text editor, spell checker, scheduler
data-binding + printing, desktop alerts, grid export suite, ribbon). `NamespaceMap.UnmappedTelerikTypes`
(`tools/Majorsilence.Forms.Migrator/NamespaceMap.cs`) is now empty — every type that was listed there has
a compat implementation and a migrator rewrite test. What remains below is deliberately out of scope, not
merely unimplemented yet.

| Item | Area | Why deferred |
|---|---|---|
| Month/week calendar grid UI | Scheduler | `RadScheduler` (`src/Majorsilence.Forms/Telerik/RadScheduler.cs`) implements a real data-binding layer, navigation, and a scrollable **agenda/list view** (appointments grouped by day) — this covers the audited Financial usage (`Reminders.vb`, print/export). The full Telerik month/week/day calendar *grid* rendering (drag-resize appointments, timeline swimlanes, etc.) was judged too large/behaviorally rich to fake and is not implemented; `GetMonthView()`/`GetWeekView()`/`GetDayView()`/`GetTimelineView()` return settable-but-not-rendered compat carriers so migrated code still compiles and runs against the agenda view. |
| `Telerik.WinControls.Themes` (e.g. `Office2007BlackTheme`) | Visual themes | Still unused by Financial (excluding `.bak` files) as of this audit. The migrator continues to warn-and-leave references under this namespace (`NamespaceMap.UnsupportedNamespaces`) rather than rewrite them into something that doesn't exist. |
| `Telerik.WinControls.Design`, `Telerik.WinControls.Primitives`, `Telerik.WinControls.Layouts` | Designer/primitive/layout infrastructure | Also warn-and-leave. Mapping `.Layouts` flat would require a type named `Dock` in `Majorsilence.Forms.Telerik`, colliding with `Control.Dock` resolution in VB consumers that import both namespaces. |

Consumers whose code uses the calendar grid UI need to either rewrite that feature against the agenda
view/`RadScheduler` data layer, or wait for month/week grid rendering to be picked up here.

## Packaging: what ships, and the two remaining decisions

**Mostly resolved (2026-08-27).** The gap this section used to describe -- `Majorsilence.Forms.WebDriver`
carrying a `PackageId`, a description and a `PackageOutputPath` while appearing in no publish list -- is
closed: `e65d30c` added it to both workflows, and the Windows-only trio (`WinForms`,
`WindowsFormsInterop`, `WindowsUIAutomation`) went with it via the windows-latest pack job.

Read the two workflows knowing which does what, or the lists look contradictory: **`release.yml` packs,
uploads artifacts and creates a *draft* GitHub release; it never holds nuget.org credentials. `publish-nuget.yml` is what actually pushes, and only when a human clicks Publish
on that draft.** So `publish-nuget.yml`'s `PACKABLE_PROJECTS` / `PACKABLE_PROJECTS_WINDOWS` is the
authoritative answer to "what goes to nuget.org": core, Avalonia, Headless, Uno, Telerik, Drawing.Common,
WebDriver, Wpf, Templates, Migrator and Mcp, plus the three Windows-only packages.

**Decision 1 -- the two shim facades stay unpublished, deliberately.** `Majorsilence.Forms.DrawingShims`
and `Majorsilence.Forms.WinFormsEnumShims` set `AssemblyName` to `System.Drawing.Common` and
`System.Windows.Forms` respectively. Both have a `PackageId`, so packing them is a one-line change; what
makes it a decision rather than an oversight is that they ship assemblies under **in-box identities**. A
consumer who installs one gets a `System.Windows.Forms.dll` out of a third-party package, colliding with
or outranking the real one in ways that are very hard to diagnose from the outside. Keep them
`ProjectReference`-only unless a concrete consumer turns up that cannot work any other way.

**Decision 2 -- `Majorsilence.Forms.WinFormsShims.Compat` is a PoC that already has a `PackageId`.** It
source-generates a `System.Windows.Forms`-namespace surface backed by this layer, for control libraries
that cannot rewrite their own public API namespace, and it packs itself as an analyzer
(`analyzers/dotnet/cs`). It is in neither publish list. A package id is permanent once pushed, so the
question to answer first is whether the PoC is the shape this feature keeps; until then, unpublished is
the right default.

**Half of one gap left, and it is in the smoke test rather than the publish.** `Migrator` and `Mcp` were
both in `publish-nuget.yml`'s list but **not** in `release.yml`'s -- and `release.yml` is the workflow
that runs on pull requests and main pushes. Their `dotnet pack` therefore ran for the first time
*during* the nuget.org publish, after a human had clicked Publish, with no draft artifact to inspect and
no earlier run to have caught a packaging error.

`Migrator` is now in `release.yml`'s `PACKABLE_PROJECTS` (2026-09-03), because the standalone
single-file binaries it used to publish there were dropped in favour of the tool package being the only
shipped form -- and that removed release.yml's only reference to the project, so the package had to take
its place. Note that the old `dotnet publish --self-contained` was a different operation that would
never have caught a bad `.nupkg` anyway. **`Mcp` is still missing from `release.yml`**; adding that one
line is the rest of the fix.

**Not answerable from this repo:** whether each of these is actually *on* nuget.org today. The workflows
describe what the next published release would push, not what previous releases did.

## Wanted: screenshots from a desktop-hosted window

**Status: a real gap, found while driving `samples/AutomationTarget` over the MCP server.** The WebDriver
endpoint's `GET /session/{id}/screenshot` renders through `HeadlessRenderer`, which refuses a window it
does not host, so it only works when the app under test runs on the Headless backend. Against a normal
desktop app on Avalonia it fails with `Window is not hosted on the Headless backend` — every other
command (tree, find, click, keys, rect, attributes) works there.

That is the one asymmetry between headless and desktop automation, and it is the first thing anyone hits
when they point an assistant at a running app: the tree is readable but the window is not viewable.
Avalonia can render a control tree to a `RenderTargetBitmap`, so the backend seam could grow a
`CaptureWindow` that the endpoint prefers when the host provides one, falling back to `HeadlessRenderer`.

Documented as a boundary in `docs/automation.md` (level 2 and the limits table) and in the MCP tool's
README until then. `tools/Majorsilence.Forms.Mcp` translates the raw message into an actionable one,
because on its own it reads like a bug rather than a limit.
