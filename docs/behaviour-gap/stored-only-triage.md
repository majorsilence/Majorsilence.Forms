# Stored-only properties — triage

**1068 entries**, from `StoredOnlyPropertyBaseline.txt` (core) and
`TelerikStoredOnlyPropertyBaseline.txt`. Generated 2026-09-16 for W6.2 (#91).

This replaces `stored-only-properties.txt`, a 2026-08-25 source-level scan of 263 entries that said in
its own header that the IL gate should supersede it. The baselines **are** that gate, so the old file
is gone rather than left to drift alongside them.

## What this is for

W6.2 asks, for each entry, that it be wired to its consumer **or** recorded as legitimately inert. At
1068 entries that is not a list anyone works linearly, and worse, working it linearly would badly
overstate what is there. The `ListView` slice is the worked example: **20 entries, six causes** — 14 of
them were the single fact that `Groups` was a collection nothing read.

So this triage groups the entries by *cause*, not by entry.

## The buckets

| Bucket | Entries | Share |
|---|---:|---:|
| **Candidate** — a real control member with a plausible consumer | 599 | 56% |
| Outbound data carrier (`*EventArgs` the framework writes for a handler to read) | 147 | 14% |
| Telerik visual-element surface — there is no element tree | 112 | 10% |
| Legacy control that upstream .NET marks `PlatformNotSupportedException` | 72 | 7% |
| Bound by what a native file/font/colour picker exposes | 69 | 6% |
| Blocked on editing infrastructure that does not exist | 39 | 4% |
| **`Cancel`/`Handled` the framework never reads back** | 10 | 1% |
| Win32 interop shim (`CreateParams`) | 11 | 1% |
| Blocked on a browser host that does not exist | 9 | 1% |

**599 entries across 194 types are candidates** — that is the real work queue, and it is
roughly half the headline number. The other 469 have a structural reason that applies
to every entry in the bucket at once.

## Why each non-candidate bucket is inert

**Framework-written outbound state — now marked mechanically in the baseline itself.** As of
2026-09-17 the scanner reports this per entry: an entry the framework writes carries
`-- framework-written (outbound state)` beside it. **147 of the core baseline's 637 entries are marked**,
so **roughly 23% of what remains is not a gap at all.**

The detection is two-part, and the first version got it wrong: a direct `stfld` is only possible inside
the declaring type, and `Modal = true;` on a `{ get; private set; }` property compiles to a *setter
call*, not a store. Marking only stores missed every motivating case. It counts both now.

**The second version was wrong too, in the opposite direction, and that one mattered more.** "Written by
a method other than its own setter" counts the **constructor**, and an auto-property with an initializer
— `public DrawMode DrawMode { get; set; } = DrawMode.Normal;` — has its backing field written by exactly
that. So every initialized auto-property was reported as outbound state: the one category a sweep is
told not to touch. The figure quoted here was **378 of 663**; the true figure is **147**. Constructors
are excluded as of 2026-09-18, which moved **219 entries back into scope**.

It was found by wiring six of them. `ListBox.BorderStyle` and its five siblings (LST-56) all carried the
marker and were all plain unwired stubs. A marker that says "do not touch this" is worth testing on both
sides, and it had none — it is annotation, which the baseline gate strips, so nothing asserted it. It
does now: `StoredOnlyPropertyBaselineTests.FrameworkWrittenNoteSeparatesOutboundStateFromInitialisers`
pins `Form.Modal` marked and `ListView.UseCompatibleStateImageBehavior` unmarked (it pinned `ComboBox.DrawMode` until W6 mechanisms wired that).

The note is **annotation, not assertion**: whether a setter call survives as a call depends on the build
configuration, so Debug and Release disagree about it, and the gate strips the note from both sides
before comparing. The gate's question is still "does anything read this".

Not yet done on the Telerik side: that baseline uses the deep-reachability scan, whose model does not
track writes. Recorded rather than half-built.

**Framework-written outbound state (the original note).** A property the framework *writes* for the application to read.
Nothing in the assembly reads the getter, which is exactly right — the reader is application code — so
the scan flags it and it will never leave the baseline. **`Form.Modal` is the clearest case**: it is set
by the dialog path (`Form.cs:1086`) and cleared on close (`:447`), and upstream's `Modal` is read-only
for precisely this reason. `WindowBase.Disposing` is the same shape (`WindowBase.cs:304`, `:347`).

This category was missed when this document was first written, and it matters in both directions: these
entries are not gaps, and a sweep that "wires" one is changing a property that already works. Checking
whether the framework *assigns* a member is the cheap test — and worth doing before touching anything on
`Form`, `Control` or `WindowBase`, where several entries are state rather than settings.

**Outbound data carriers.** An `*EventArgs` property the framework *writes* so a handler can read it.
Nothing in the assembly reads it back, which is exactly right — the reader is application code. This
was recorded once before, when `DataGroup.HeaderText`/`Level` went straight onto the baseline in #183:
a legitimately inert entry, and a reminder that adding a projection type adds stored-only surface by
construction. Not defects, and they will never leave the baseline.

**`Cancel`/`Handled` the framework never reads back.** The *opposite* case, and the highest-value
entries in the file: an inbound member the framework is supposed to consult. A `Cancel` nobody reads is
a veto that silently does nothing, which is the bug class #182, #185, #187 and #192 each fixed. All 10
were checked individually — see below, because the count is misleading in both directions.

**Telerik visual-element surface.** `RadElement` and its relatives describe a visual element tree this
layer does not have; the compat controls paint directly. Established in #192 for `RadGridView.CreateCell`
and it generalises to the whole family.

**Legacy controls upstream marks unsupported.** `DataGrid`, `ToolBar`, `StatusBar`, `MainMenu`,
`ContextMenu` and their satellites live in upstream's `Controls/Unsupported/` and throw
`PlatformNotSupportedException` on .NET. Determined mechanically by checking each baseline type against
that folder rather than by judgement. This layer implements more than upstream here already, so these
are the lowest priority in the file.

**Native dialogs.** `FileDialog` and friends reach a platform picker through `IWindowBackend`, which
takes a request carrying title, initial directory, filters and multi-select. The rest — `ClientGuid`,
`ShowPinnedPlaces`, `AutoUpgradeEnabled`, `DereferenceLinks`, `AddToRecent` and the other shell flags —
have no portable equivalent. A handful (`AddExtension`, `CheckFileExists`, `ValidateNames`) *could* be
honoured as managed post-processing of the picker's result; those are candidates and are counted as
such, not here.

**Blocked on infrastructure.** `PropertyGrid`/`GridItem` need an editing pipeline; `WebBrowser` needs a
browser host. Both are named as blocked elsewhere in this plan.

**Win32 shim.** `CreateParams` describes window-creation flags for an API this layer does not call.

## The `Cancel`/`Handled` cluster, checked one by one

The rule that finds these over-matches in both directions, so each of the 10 was checked against its
raise site. That is the point: a mechanical bucket is a starting hypothesis, not a finding.

| Member | Verdict |
|---|---|
| `Telerik.GridViewCellCancelEventArgs.Cancel` | **Real defect.** `RadGridView` raises `CellBeginEdit` from the base event and never propagates `args.Cancel` back to the base args, so a handler cancelling a cell edit is ignored. Not yet fixed — filed as the next item. |
| `DataGridViewDataErrorEventArgs.Cancel` | **Real, but subtle.** Upstream's `Cancel` means "do not restore the old value" and is tangled with a revert path this layer only partly has. Needs its own item rather than a sweep entry. |
| `AppointmentEditDialogShowingEventArgs.Cancel` | Inert. `ShowAppointmentEditDialog` raises the event and does nothing else — there is no dialog to suppress. The doc comment claiming handlers "may cancel" overstates it. |
| `SchedulerAppointmentCancelEventArgs.Cancel` | Inert. The agenda's `AppointmentSelecting` is a notification; no selection state is set afterwards, so there is nothing to cancel. |
| `SchedulerContextMenuOpeningEventArgs.Cancel` | Not a defect. `RaiseContextMenuOpening` **returns the args**, so the caller reads `Cancel`. It is on the baseline because the reader is outside the assembly, by design. |
| `Telerik.ValueChangingEventArgs.NewValue` | Not a defect, and a false positive of the rule: `NewValue` is *outbound*. Corrected here rather than left to mislead the next reader. |
| `DataGridViewCellEditEventArgs.Cancel`, `HelpEventArgs.Handled`, `HtmlElementErrorEventArgs.Handled`, `ToolStripContentPanelRenderEventArgs.Handled`, `ToolStripPanelRenderEventArgs.Handled`, `Telerik.PropertyValidatingEventArgs.NewValue` | **No raise site at all.** These belong to the dead-event backlog (W6.1), not here: there is no moment at which the flag could be consulted because the event never fires. |

So of 10 flagged, **two are real**, one is a rule artefact, three are inert for a stated reason, and six
are really W6.1 entries wearing a W6.2 costume.

## The candidate queue

Largest candidate types, which is where the remaining W6.2 work is:

| Type | Entries |
|---|---:|
| `ListView` | 19 |
| `RichTextBox` | 18 |
| `ToolStripItem` | 18 |
| `ToolStrip` | 17 |
| `Form` | 16 |
| `TreeView` | 14 |
| `Control` | 11 |
| `ListBox` | 11 |
| `WindowBase` | 11 |
| `DataGridView` | 10 |
| `ToolTip` | 9 |
| `Graphics` | 8 |
| `ListViewGroup` | 8 |
| `MasterGridViewTemplate` | 8 |
| `RadGridView` | 8 |
| `DataGridViewLinkCell` | 7 |
| `ScrollPropertiesBase` | 7 |
| `GridViewCommandColumn` | 7 |
| `RadTreeView` | 7 |
| `PrintPreviewControl` | 6 |

A count here is still not a count of defects — `ListView`'s 20 were six causes — but it is the right
place to start, and each type should be triaged the way `ListView` was before anything is wired.

## A falling count is not always a closure

The scan sees auto-properties only. Converting one to a hand-written notifying setter removes it from
the baseline whether or not anything now *reads* it -- #226 named the hazard, and the 2026-09-22 event
batch was the large case: twelve of thirteen departures (`WindowBase.AutoSize`/`Margin`/`TabIndex`,
`Control.Region`, `ToolStrip.LayoutStyle`, the `CommandParameter`s and the rest) fire an event now and
are still consumed by no layout or paint. Read the plan entry for a batch before crediting its count.

## State after the 2026-09-22 sweep

Every line of the stored-only baseline now carries a reason (`-- ...`): what would read it, why nothing
does, or which feature it waits on. The per-type table above is the dated snapshot from before that pass;
the plan entry "W6.2 — the sweep" lists the 26 that were read, the annotation classes, and the remainder
grouped by feature. A new entry appearing without a reason is the signal this baseline exists to give.

## How to regenerate

The buckets are derived from the two baseline files plus a scan of upstream's `Controls/Unsupported/`
folder. Re-derive after any sweep lands; the entry counts above are a dated snapshot, deliberately, for
the same reason the baselines themselves no longer carry a live count (see `CONTRIBUTING.md`).
