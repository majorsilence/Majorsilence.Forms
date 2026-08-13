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
