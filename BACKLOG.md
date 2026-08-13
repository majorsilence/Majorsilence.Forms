# Backlog

## HiDPI: the full suite does not pass at simulated scale 2

`MF_HEADLESS_SCALE=2` makes the headless backend report a scaled display. CI runs a scaling-focused
subset at that scale (see `.github/workflows/dotnet.yml`); the **whole** suite does not pass there yet,
which is why the gate is scoped rather than blanket.

State as of this triage, after fixing the headless input contract (`HeadlessRenderer`'s coordinates are
logical and are now converted to device pixels before injection -- previously passed straight through,
so at scale 2 every injected click landed at half its intended position):

| Cluster | Examples | Notes |
|---|---|---|
| Hit-testing under scale | `A_full_click_lands_on_the_clicked_tab`, `Second_row_tab_is_hit_testable`, `A_disabled_tab_header_is_not_selected`, `MenuStrip_LaysItemsOutLeftToRightAcrossTheBar` | These pass **logical** coordinates computed from `GetTabRect`/`Bounds`, so the injection is now correct and the failure is downstream: the library's own hit-testing does not agree with its layout at scale != 1. Most likely a real defect a HiDPI user would hit. |
| Painting / capture | `ChildIsPainted_WhetherOrNotOverrideChainsToBase`, `ChildPaintsAboveParentsOwnDrawing`, `PaintEvent_DoesNotSuppressChildControls`, `RendersFormToPng_AtRequestedSize` | Capture size vs scale; needs deciding whether `CapturePng`'s width/height are logical or device. |
| Text metrics | `Designer_sized_radio_text_is_not_clipped`, `Single_line_text_is_centred_vertically`, `DropDownList_TooShortForFont_KeepsCapsInsteadOfSlicingTop`, `Overflowing_headers_wrap_into_multiple_rows` | Some are genuine scale bugs; some assert scale-1 pixel geometry by construction and should assert proportionally instead. |
| A hang | one test does not return at scale 2 | Not yet identified. Blocks running the full suite at scale 2 at all, so it is the first thing to find. |

Two things worth keeping in mind when picking this up. The clusters are not all the same kind of
problem -- some are library defects, some are tests that hardcode scale-1 pixels and are simply wrong to
assert that -- so each needs classifying before fixing. And the hang has to go first: until it does,
there is no way to get a full failure list at scale 2, only the prefix before it stalls.

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
