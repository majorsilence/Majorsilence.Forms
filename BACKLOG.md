# Backlog

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
