# Backlog

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
