# Behavioural gap analysis and implementation plan

**Measured 2026-08-25** by reading `Majorsilence.Forms` against the `dotnet/winforms` source tree,
control by control, member by member.

This is the third gap document in the series, and the first one that is not about *what exists*.

| Document | Question it answers | Baseline |
|---|---|---|
| [`gdi-gap-plan.md`](gdi-gap-plan.md) | Does the drawing member exist? | 0 |
| [`winforms-gap-plan.md`](winforms-gap-plan.md) | Does the WinForms member exist? | 0 |
| **this document** | **Does the member that exists actually do what WinForms does?** | **483 findings** |

Both predecessors are at zero and both say, repeatedly, that the count was the less interesting half
of the problem. `winforms-gap-plan.md` puts it plainly:

> *A missing member fails loudly at compile time; a member that exists with the wrong shape or no
> wiring fails silently, and the scanner cannot see it because the name matches.*

That sentence names the entire subject of this document. Every gap the reflection scanner can see is
closed. What is left is the part it is structurally blind to, and it is much larger than the closed
part was.

## Headline

**483 findings: 41 P0, 223 P1, 211 P2**, across twelve areas. Three numbers carry the shape of it:

| | Count | What it means |
|---|---|---|
| Public settable auto-properties nothing reads | **822 of 1254 (65%)** | Two thirds of the settable surface stores a value no code here consumes |
| Events declared `add { } remove { }` | **80** | Compile, accept a handler, discard it |
| Field-backed events nothing raises | **130** | A handler is stored and never called |

Those three are now measured by CI gates rather than by hand — see
[Guardrails](#guardrails-to-build-first), which are built. The property figure is much larger than the
source-level estimate this audit started from (263), because the gate asks the precise question the
grep could not: is the value read *anywhere*, by a getter call or a field load.

Per area:

| Area | Findings | P0 | P1 | P2 | File |
|---|---:|---:|---:|---:|---|
| Simple & value controls | 61 | 6 | 27 | 28 | [`simple.md`](behaviour-gap/simple.md) |
| Drawing / GDI+ | 46 | 3 | 17 | 26 | [`drawing.md`](behaviour-gap/drawing.md) |
| List controls | 43 | 5 | 22 | 16 | [`lists.md`](behaviour-gap/lists.md) |
| `DataGridView` | 41 | 5 | 21 | 15 | [`datagridview.md`](behaviour-gap/datagridview.md) |
| `Form` / `Application` | 39 | 4 | 22 | 13 | [`form.md`](behaviour-gap/form.md) |
| Dialogs, clipboard, input, printing | 39 | 2 | 24 | 13 | [`services.md`](behaviour-gap/services.md) |
| `ToolStrip` & menus | 38 | 3 | 17 | 18 | [`toolstrip.md`](behaviour-gap/toolstrip.md) |
| Event wiring & order | 38 | 4 | 15 | 18 | [`events.md`](behaviour-gap/events.md) |
| Layout & containers | 37 | 2 | 17 | 17 | [`layout.md`](behaviour-gap/layout.md) |
| Text controls | 35 | 4 | 15 | 16 | [`text.md`](behaviour-gap/text.md) |
| Data binding | 35 | 3 | 14 | 12 | [`binding.md`](behaviour-gap/binding.md) |
| `Control` base | 31 | 0 | 12 | 19 | [`control.md`](behaviour-gap/control.md) |

`Control` scoring zero P0 is the good news in the table: the layout half of the base class is a
faithful port of upstream and behaves. Everything above it is where the divergence lives.

The five findings most likely to be costing someone right now:

1. **`override ProcessCmdKey` never runs** (RC-1) — the whole keyboard pre-processing chain is
   declared and never dispatched, so menu shortcuts, mnemonics and dialog keys are dead.
2. **Disabling a menu item does nothing** (`TSM-01`) — `ToolStripItem.Enabled` is a `new` shadow; the
   item still paints enabled and still fires `Click`.
3. **A "Yes/No/Cancel" `MessageBox` always returns `OK`** (`SVC-01`) — four of the seven button sets
   are unimplemented and fall through to the success value.
4. **Every `BindingSource` binding made in `InitializeComponent` is orphaned** (`BND-01`) — the
   designer's own ordering (`BeginInit` → `DataBindings.Add` → `EndInit`) rebuilds the manager and
   drops the binding.
5. **`ListView` ignores `View`** (`LST-01`) — every item renders as a large-icon tile, so `Details`,
   the mode nearly every LOB app uses, does not exist.

## Why this audit was run

Stubs keep surfacing during real migrations — the pattern in `COMPATIBILITY_MATRIX.md`'s
"Gaps found by migrating real apps" table, where each row was discovered by someone's application
misbehaving rather than by a test. That is an expensive discovery channel. This audit front-runs it:
twelve parallel passes over the source, each comparing one area against the upstream implementation
and recording every member that compiles, runs, and does something other than what WinForms does.

The [stub policy](../COMPATIBILITY_MATRIX.md#stub-policy) is not under review here. A silent no-op
instead of a `NotImplementedException` remains the right default. This document is the inventory of
where that default is currently being paid for, so the debt can be retired deliberately instead of
one production bug at a time.

## Method

Twelve areas, one auditor each, all working from the same brief: enumerate the members in the area,
open the upstream implementation for each candidate, and record only divergences confirmed by reading
both sides. Findings cite `file:line` on both sides. Areas:

`Control` · `Form`/`Application` · text controls · list controls · `DataGridView` · layout engines ·
`ToolStrip`/menus · simple & value controls · data binding · drawing/GDI+ · dialogs/clipboard/input/printing ·
cross-cutting event order

Each finding carries a category, a severity and a confidence:

| Category | Meaning |
|---|---|
| **A** | Silent **wrong** behaviour — exists, does something different from upstream |
| **B** | **No-op** where upstream does something apps rely on |
| **C** | **Stored-only** property — settable, readable, nothing consumes it |
| **D** | **Never-raised event** with a natural trigger point that already exists here |
| **E** | **Structural** — wrong base class, wrong shape, wrong default |

| Severity | Meaning |
|---|---|
| **P0** | Will break or visibly corrupt common migrated apps |
| **P1** | Common feature wrong, or commonly noticed |
| **P2** | Niche or cosmetic |
| **P3** | Win32-only, no portable meaning — listed for completeness, not work |

Full findings live in [`docs/behaviour-gap/`](behaviour-gap/), one file per area, each finding with
**Ours** / **Upstream** / **Impact** / **Fix** / **Test** / **Tests today**. This document is the plan
built on top of them; the finding files are the evidence and the working detail. Work items below
reference finding IDs (`CTL-01`, `DGV-14`, …) — read those before starting an item.

## How to work an item

Every work item below is sized to be one branch and one review. Before starting:

1. **Read the findings it names**, in `docs/behaviour-gap/<area>.md`. Each carries the upstream
   `file:line` — open it. The finding's **Fix** line is a starting point, not a specification; the
   upstream source is the specification.
2. **Check the "tests today" line.** A significant number of existing tests assert the *current*,
   divergent behaviour — see [Tests that pin the divergence](#tests-that-pin-the-divergence). Those
   need inverting as part of the item, with the reason in the commit message. A test that fails
   because you fixed the product is not a test to work around.
3. **Follow the house rule from the two predecessor plans: generate, do not transcribe.** Where the
   correct answer is a value or a shape upstream already knows (an enum number, a default, a
   sequence of events), take it from the reference assembly or the upstream source rather than
   writing it from memory. Both earlier plans record cases where a hand-written assertion was wrong
   and the generated one was right.
4. **Definition of done, per item:** the behaviour matches upstream; a test asserts the *computed
   result* rather than the member's existence; `COMPATIBILITY_MATRIX.md` is corrected wherever it
   overstates the area; the relevant baseline is regenerated; the whole suite passes at scale 1 and
   under `MF_HEADLESS_SCALE=2`.

Tests can be written without a display: `Majorsilence.Forms.Headless`'s `HeadlessRenderer` provides
`Use()`, `CapturePng`, `MouseMove/Down/Up/Click`, `KeyDown/KeyUp`, `TextInput`, and
`Majorsilence.Forms.Automation.AutomationSession` drives a window through the same input path the
backends use. Event-order findings are best tested with a recorder that appends every event name to a
list and asserts the whole sequence at once — several findings here are ordering bugs that a set of
independent single-event tests would pass.

## Guardrails to build first

The two predecessor plans both ended with a committed tool and a CI gate, on the stated grounds that
hand-written audits "went stale within a release". This audit is hand-written and will go stale the
same way unless the same thing is done. Three gates cover most of what was found, and all three are
mechanical.

`NoOpStubBaselineTests` already does exactly this job for empty-bodied public void methods, using
`System.Reflection.Metadata` to read IL straight off the built assembly. Its own remarks anticipate
the largest gate below:

> *Property and event accessors are excluded — an inert event is a separate (and much larger)
> category than a method that quietly discards its arguments.*

That category is now measured. Reuse `NoOpStubBaselineTests.ScanEmptyBodiedPublicVoidMethods`'s
`PEReader`/`MetadataReader` approach and its baseline-file conventions (`MAJORSILENCE_WRITE_*`
regeneration, a committed reviewable text file, added-entries fail / removed-entries prompt).

**Status: built.** All three, plus the test helper, landed as Phase 0. They share one IL scanner
(`tests/Majorsilence.Forms.Tests/StubSurfaceScanner.cs`) and follow `NoOpStubBaselineTests`'
conventions exactly: a committed reviewable text file, `MAJORSILENCE_WRITE_*` regeneration,
added-entries fail and removed-entries prompt.

| Gate | What it pins | Baseline |
|---|---|---|
| **Inert events** | Events whose `add`/`remove` accessors have an IL body of just `ret` — the `add { } remove { }` idiom, which compiles, accepts a handler and discards it | **80** |
| **Unraised events** | Field-backed events nothing ever loads in order to invoke — the `#pragma warning disable CS0067` set, found from IL so the pragma cannot hide one | **130** |
| **Stored-only properties** | Public settable auto-properties nothing reads: neither the getter is called nor the backing field loaded outside it | **822 of 1254 (65%)** |

The third is the number worth watching over time. Note it is much larger than the 263 this audit's
source-level grep estimated, because the gate asks the precise question — the grep only caught
properties whose *name* appeared nowhere else, which misses every one that is mentioned in a comment
or shares a name with a working member elsewhere.

**One thing not to misread.** These gates answer "is the value ever read", which is narrower than "does
this member work". A property read only by code that is itself inert counts as consumed:
`ListView.View` is read by `ListViewParity.cs:599` and still ignored by the renderer;
`TextBox.WordWrap` is read by a `ToolStripTextBox` forwarder that draws nothing;
`TextBox.AcceptsReturn` is read inside a key handler nothing calls. Absence from a baseline is not a
certificate. Catching that class needs transitive reachability and is a worthwhile follow-up; until
then the finding files are where it lives, and these gates are the mechanical floor beneath them.

### The platform gate

Two of this work's regressions reached CI because the suite ran on one platform. Worth stating as a
guardrail in its own right, because it is not a baseline and no scanner catches it:

- **macOS is the only platform that uses system decorations.** Every other one draws the library's own
  title bar, so anything touching the caption — client-area geometry, hit-testing near the top of a
  form, tab order through the caption buttons, the automation tree — behaves differently on the two
  sides of that branch.
- **The suite used to run on Windows only**, which is the chrome side. The no-caption path had no test
  coverage at all, while being the shape most contributors develop against, and Linux had none either.
  CI now runs both axes on every platform that can express them: Windows and Linux natively draw the
  library's chrome, and macOS runs its own system decorations *and* the forced-chrome path, each at
  scale 1 and 2 — eight runs.
- **Every run is the solution minus the migrator suite.** That one takes over ten minutes because it
  builds generated projects, against roughly eight seconds for the other 4,424 tests, and is
  OS-agnostic — so it runs once, in the full Windows pass, and the matrix costs about a minute in
  total.
- **`MF_FORCE_CUSTOM_CHROME=1` covers the reverse case locally**, making a macOS process take the
  Windows branch. The full local matrix is `{chrome, no-chrome} × {scale 1, scale 2}`, and it is worth
  running all four before pushing anything that touches window geometry or input routing.
- **The HiDPI gate now runs even when the plain test step fails.** It was skipped on two consecutive
  PRs because a red first step hid it, and the HiDPI-only failure underneath (`EVT-39`) cost an extra
  round trip each time. Independent gates should not mask each other.

All three baselines include legitimate entries — `Tag` is app storage by definition,
`FileDialog.ClientGuid` has no portable meaning — so each is annotated in place rather than trimmed.
The point is not to drive the numbers to zero; it is that adding to them becomes a conscious act.

## Scope and limits of this audit

**What it is.** Twelve source-reading passes, each comparing one area's members against the upstream
implementation. Where a finding says "upstream does X", someone opened upstream and read X.

**What it is not.** It is not exhaustive, it is not generated, and it will go stale — which is the
argument for the gates above and for treating the finding files as a snapshot rather than a registry.
Specifically:

- **Not a runtime verification.** Findings are from reading code, not from running it. Confidence is
  recorded per finding; `Medium`/`Low` items should be reproduced before being fixed.
- **Coverage is uneven by design.** Auditors were told to prefer depth on high-traffic members over
  breadth on obscure ones, so a thin section means "not examined closely", never "clean". The
  `Majorsilence.Forms.Telerik`, `Uno`, `WebDriver`, `WindowsUIAutomation` and `Migrator` projects were
  not audited at all; neither were the `Design`/`PropertyGridInternal` design-time surfaces beyond
  their effect on the controls above.
- **Win32-only behaviour is out of scope, not missing.** Anything whose upstream implementation is a
  window handle, a message pump hook, an IME context or the theme engine is recorded as P3 with a
  one-line reason. Those are not work; they are the documented shape of the port.
- **Duplicate findings across areas are deliberate.** The keyboard chain shows up in five area files
  because five areas trip over it. They are reconciled in the root causes below, and a work item
  closes them together.

## The nine root causes

The findings are not nine hundred independent bugs. They cluster hard, and the clustering is the plan:
a handful of structural decisions each produce dozens of symptoms, and fixing the structure closes the
symptoms in bulk. Work items are ordered by how many findings each unblocks, not by severity alone.

### RC-1 — The keyboard pre-processing chain is declared but never dispatched

The single highest-leverage finding in the audit. `ProcessCmdKey`, `ProcessDialogKey`,
`ProcessDialogChar`, `ProcessKeyPreview`, `ProcessMnemonic` and `IsInputKey` exist as `protected
virtual … => false` on **both** `Control` (`Control.Compat.cs:568-606`) and `WindowBase`
(`WindowBase.Compat.cs:23-53`), and `PreProcessMessage` on `ControlAndFormParity.cs:344`. Every one of
them has **zero call sites** in the entire assembly — they exist so that a migrated app's `override`
compiles. The real routing lives in `WindowBase.HandleKeyDown` with its own, different rules.

In WinForms this chain *is* the keyboard: menu shortcuts, mnemonics, dialog keys, arrow traversal, and
every control's claim on Tab/Enter/Escape all run through it. Because nothing calls it here, an
`override ProcessCmdKey` — one of the most common customisations in a WinForms codebase — silently
never runs.

Closes or unblocks: `FRM-05`, `FRM-08`, `FRM-09`, `FRM-28`, `SVC-02`–`SVC-08`, `CTL-28`, `TSM-02`,
`TSM-13`, `TXT-01`, `TXT-09`, `TXT-10`, `TXT-13`, and the `AcceptsReturn`/`AcceptsTab` family.

### RC-2 — `IsFocusManagingContainerControl` is hard-wired `false`

One expression disables an entire subsystem. `GetContainerControl()` returns null for every control,
which in turn disables `ActiveControl`, container-level `Validate`/`ValidateChildren`,
`SelectNextIfFocused` (so hidden and disabled controls keep keyboard focus) and
`ScrollControlIntoView`-on-focus. Compounding it, validation is raised from `OnLostFocus` — by which
time focus has already moved, so `e.Cancel`, the entering control's `CausesValidation` and
`AutoValidate` cannot be honoured even in principle. "Cancel to keep focus in the invalid field", the
standard WinForms validation idiom, does nothing.

Closes: `CTL-01`, `CTL-02`, `CTL-07`, `CTL-08`, `CTL-09`, `CTL-30`, `CTL-31`, `FRM-10`, and the
`OnValidation` half of `BND-07`.

### RC-3 — Form lifecycle modelled as one-shot flags

`_loadFired`, `_formClosedFired`, `shown`, `visible` and `dialog_result` are set once and never reset.
WinForms models the *handle* as the unit of lifetime and recreates it on each show, so a form instance
is reusable; here it is correct only the first time. A dialog shown twice — the single most common
dialog pattern there is — returns its previous `DialogResult`, skips `Load`, and reports
`Visible == true` after `Close()`.

Closes: `FRM-01`, `FRM-02`, `FRM-03`, `FRM-04`, `FRM-07`, `FRM-12`, `FRM-13`, `FRM-16`.

### RC-4 — The `CurrencyManager` is a snapshot, not a live object

It is constructed over an `IList` reference, never subscribes to that list, and `BindingSource`
discards and rebuilds it whenever `DataSource`/`DataMember` re-resolve — including at `EndInit`.
`Binding` never registers with it, so `BindingManagerBase.Bindings` is always empty and the manager can
neither push nor pull. In the exact order designer code runs — `BeginInit`, `DataBindings.Add(…,
bindingSource, …)`, `EndInit`, fill the data in `Load` — **every simple binding to a `BindingSource` is
orphaned**, and every binding to a still-empty list stays at `Position -1` forever.

Closes: `BND-01`, `BND-02`, `BND-08`, `BND-09`, `BND-10`, `BND-14`, `BND-16`, `BND-19`, `BND-28`, and
the grid half of `DGV-31`/`DGV-32`.

### RC-5 — Events declared, handlers discarded

**84** events are written `add { } remove { }`: they compile, accept a subscription, and throw the
delegate away. A further **89** `#pragma warning disable CS0067` sites declare a field-backed event
that nothing ever raises. They are concentrated exactly where LOB apps subscribe — `DataGridView` (24),
`ListView` (9), `TreeView` (8), `NotifyIcon` (8), `WebBrowser` (6) — and in most cases **the trigger
point already exists**: `OnMouseDown`, `OnKeyUp`, `EndEdit`, a property setter, `ListChanged`. The
compiler warning was suppressed instead of the event wired.

Closes: the `D`-category findings across every area — roughly 60 findings.

### RC-6 — Two backing stores for one concept

The same idea is stored twice and read from the wrong one. `ControlStyles.Selectable` vs
`ControlBehaviors.Selectable`; `UseWaitCursor` auto-property vs `States.UseWaitCursor`;
`Form.MaximizeBox`/`MinimizeBox`/`ControlBox` vs the library's own `AllowMaximize`/`AllowMinimize`;
and most damagingly the `public new` shadows on `ToolStripItem`, where `Enabled`, `Tag`, `Checked`,
`Height`, `Alignment` and the `MouseEnter`/`MouseLeave` events each hide a `MenuItem` member that the
layout, renderer and hit-test path reads through a `MenuItem`-typed reference.

`ToolStripItem.Enabled` is the clearest instance and a genuine P0: `WinFormsCompat.cs:1093` declares
`public new bool Enabled { get; set; } = true;`, while `MenuBase.cs:135` (click dispatch),
`MenuRenderer.cs:34` and `MenuDropDownRenderer.cs:40,44` all read `MenuItem.Enabled`
(`MenuItem.cs:67`). **Disabling a menu item stores `false` into the shadow; the item still paints
enabled and still fires its `Click`.**

Closes: `TSM-01` and the shadow family, `CTL-03`, `CTL-06`, `CTL-11`, `CTL-17`, `CTL-24`, `FRM-18`,
`FRM-19`, `FRM-38`, `TXT-18`.

### RC-7 — Stored-only properties: the WinForms name is a decoy

**822 of 1254** public settable auto-properties (65%) are read nowhere in the assembly. The recurring
shape is worse than inertness: the renderer reads a *private twin* while the WinForms-named property
sits beside it storing values nobody consumes — `column.DefaultCellStyleAlignment` vs
`DefaultCellStyle.Alignment`; `column.SortOrder` vs `HeaderCell.SortGlyphDirection`;
`ShowDropdownGlyph` vs `ShowPlusMinus`; `INDENT_SIZE` vs `Indent`; `ScrollbarAlwaysVisible` vs
`ScrollAlwaysVisible`. Migrated code sets the WinForms name, which is precisely the one that does
nothing.

Closes: the `C`-category findings — roughly 90 findings, including `ListView.View` (every item renders
as a large-icon tile whatever the mode), the whole `RichTextBox.Selection*` family, and
`TextBox.WordWrap`/`CharacterCasing`/`AcceptsReturn`/`AcceptsTab`.

### RC-8 — Logical versus device coordinates at every hit-test boundary

`BACKLOG.md` already records this as the root of most HiDPI failures and says the asymmetry is still
there: `Control.Bounds` and `MouseEventArgs` are logical; `ClientRectangle`, `ClientSize` and
back-buffers are device. Hit-testing that mixes them is invisible at scale 1 and wrong on every HiDPI
display. `ListBox.GetIndexAtLocation` was fixed for exactly this; `ListView`, `TreeView.GetNodeAt`,
`IndexFromPoint`, `GetItemAt`/`HitTest` and `ContextMenuStrip.Show(control, point)` were not. The
scale-2 CI gate does not catch them because no test exercises those paths.

The sharpest instance is `PaintEventArgs.Graphics`: it is in **device pixels** while the control's own
`Width`/`Height`/`ClientRectangle` are **logical**, so every owner-drawn control — the whole point of
`Paint` — draws wrong by the scale factor on any display that is not 96 DPI. The framework compensates
internally through `ScaledBounds`; user code has no equivalent and is not told it needs one.

Closes: `EVT-37`, `EVT-38`, `CTL-10`, `LST-15`, `LST-20`, `LST-21`, `TSM-03`, and the `FRM-06`
title-bar offset.

### RC-9 — Getters that return a confident wrong answer

The stub policy says a member with no implementation should return "a sensible default". Applied to a
*getter that reports state*, that produces an answer the caller cannot distinguish from a real one, and
it is strictly worse than throwing. `MaskedTextBox.MaskCompleted` returns `true` unconditionally, so
mask validation always passes; `RichTextBox.Rtf` returns a stale stored string, so saving a document
after the user edits it writes the old content; `FontFamily.IsStyleAvailable` returns `true` for every
family; `ImageCodecInfo.GetImageDecoders()` returns the *encoder* list. The drawing plan already drew
this line and it should be promoted to policy for the whole layer:

> *A no-op is a reasonable default for control behavior, but for drawing and metrics a wrong number
> propagates into layout. Metric-returning members should compute or throw, never guess.*

### RC-10 — Fallbacks that synthesise success

Where a path is unsupported the layer returns the value that means "it worked". `Form.ShowDialog()`
with no open owner returns `DialogResult.OK` without showing anything; `MessageBox.Show` with four of
the seven `MessageBoxButtons` sets returns `OK`; `FileDialog.ShowDialog()` with no owner returns
`Cancel` without showing; `Control.DoDragDrop` returns `None`. Each turns a missing feature into a
silently wrong branch in the caller — the "Yes/No/Cancel" dialog that always takes the Yes path is the
worst of them.

## `COMPATIBILITY_MATRIX.md` corrections

The matrix is the document migrating developers and AI assistants read to decide whether a control is
safe to use, so a row that overstates is a defect in its own right. The audit found these; correcting
them is cheap and should not wait for the code fixes.

| Row | Says | Actually |
|---|---|---|
| `NotifyIcon` | "Implemented" | Every event is `add { } remove { }` and no backend exposes a tray seam; the source itself calls it a stub (`NotifyIcon.cs:58-84`) |
| `ToolStripMenuItem` | "Core `ToolStripItem` surface (`Text`, `Image`, `Click`, `Enabled`, `Visible`) present" | `Enabled` and `Checked` are `new`-shadowed and non-functional on the common paths (`TSM-01`, `TSM-04`) |
| `TreeView` | "index-based `ImageIndex` works" | `ImageList`/`ImageIndex`/`SelectedImageIndex` are stored; the renderer reads `TreeNode.Image` only (`LST-25`) |
| `ListBox` | "no `PreferredHeight`/`Sort()`" | `PreferredHeight` exists now; `Sort()` still does not (`LST-10`) |
| `Binding` | DataView/DataTable sources supported | `DataRowView` columns are invisible to `Binding` — it uses CLR reflection, not `TypeDescriptor` (`BND-03`) |
| `DataGridView` | `AutoSizeColumnsMode` "only invalidates" | Accurate, but understates traffic: `Fill` is in most designer-built grids and leaves a blank right band (`DGV-18`) |
| `ToolStrip` | stored-only group documented | `OverflowButton` returns null (`NullReferenceException` on the documented idiom) and `ToolStripManager.Merge` returns `false` — neither is on the matrix |
| Printing | the PDF pipeline is the substitute for `Print()` | `Print()` writes a temp PDF and surfaces it to nobody (`SVC-29`) |
| Undo | undo implemented | Unreachable from the keyboard — Ctrl+Z is not bound (`TXT-13`) |

Add a row, too, for the thing the matrix has no entry for at all: **`override ProcessCmdKey` never
runs** (RC-1). It is one of the most common WinForms customisations and its silent absence deserves to
be stated where people look.

## What was verified as correct

Worth stating as prominently as the failures, because it bounds the work and prevents "fixing" things
that already match. Two areas came back substantially clean:

**The layout engines are a faithful port.** Normalised diffs of `Layout/` against upstream's
`DefaultLayout`, `FlowLayout`, `TableLayout`, `CommonProperties` and `LayoutUtils` show only naming
differences, `#if DEBUG` blocks and the opt-in AnchorLayoutV2 path. The container-level *hooks* into
them are what is missing (W5.24) — a small amount of wiring in front of a large amount of working
machinery.

**Most event wiring is right; it is the ordering and gating that is not.** The events audit explicitly
verified these sequences as matching upstream, and a fixer should treat them as safe:

- single-click order (`MouseDown` → on release `Click` → `MouseClick` → `MouseUp`), and the
  `StandardClick`/`StandardDoubleClick` style gating
- mouse capture routing — the capture holder receives every move and the release wherever the pointer is
- `Enter`/`GotFocus` and `Leave`/`LostFocus` pairing *within* one control (the cross-control ordering is
  `EVT-02`)
- form deactivation correctly does **not** run validation
- `KeyPreview`, `SuppressKeyPress`, and the `PreviewKeyDown` → `KeyDown` → `KeyPress` → `KeyUp` order
- the whole layout notification set: `Layout`/`PerformLayout` suspension, `SizeChanged` vs `Resize`,
  layout suspension during `Controls.Add`, `ControlAdded`/`ControlRemoved` on the parent,
  `ParentChanged` on the child, and `AssignParent`'s change snapshotting
- the ambient cascades (`BackColor`/`ForeColor`/`Font`/`RightToLeft`) including the "only re-raise when
  the child has no explicit value" guard, and `EnabledChanged`/`VisibleChanged` propagation
- `TextChanged` (null coerced, early-out when unchanged)
- the paint pass: `OnPaintBackground` → `OnPaint` → children in reverse z-order, hidden children
  skipped, `Invalidate`-during-`Paint` surviving
- `MouseCaptureChanged` on both edges, once per real transition

`Control`'s zero P0 count belongs in the same paragraph: the base class's layout half behaves, and the
divergence lives above it.

**The drawing plan's own hollow list is genuinely closed.** All five members `gdi-gap-plan.md` called
out as "present but hollow" — the `TextureBrush` shell, `FontFamily.IsStyleAvailable => true`,
`ImageAnimator.CanAnimate => false`, `ImageCodecInfo.GetImageDecoders` returning the encoder list, and
`StringFormat.SetMeasurableCharacterRanges` being write-only — were re-checked against today's source
and are fixed. That plan's Class C worked; the findings here are new ground, not a relapse.


## The plan

Six phases. The ordering is deliberate and is **not** by severity: phases 0–2 are small in code and
large in leverage — they unblock work that cannot be tested properly until they exist. Phases 3–5 are
the bulk. Phase 6 is mechanical cleanup that can proceed in parallel with anything.

Each item names the findings it closes. Read those first; they carry both sides' `file:line`.

---

### Phase 0 — Make it measurable *(do first; nothing here changes behaviour)* — **DONE**

Three baseline gates and one test helper. Without these, phases 1–5 have no way to prove they shrank
anything, and the regression channel that produced these findings stays open.

**W0.1 — Inert-event baseline gate.**
Scan the built assembly for `add_`/`remove_` accessors whose IL body is just `ret`; pin the 84 in a
committed `InertEventBaseline.txt`. Model it on `NoOpStubBaselineTests` — same `PEReader` walk, same
`MAJORSILENCE_WRITE_*` regeneration, same added-fails/removed-prompts assertions.
*Files:* `tests/Majorsilence.Forms.Tests/` (new). *Risk:* none.

**W0.2 — Unraised-event baseline gate.**
The `#pragma warning disable CS0067` set (89 sites). For each field-backed event, check whether the
backing field is ever loaded outside its own `add`/`remove`; report those that are not. Pin them.
*Risk:* none. *Note:* this and W0.1 together are the "much larger category" `NoOpStubBaselineTests`
explicitly deferred.

**W0.3 — Stored-only-property baseline gate.**
Public settable auto-properties whose `<Name>k__BackingField` is read only by their own getter. Pin
the 263. Seed the baseline from
[`docs/behaviour-gap/stored-only-properties.txt`](behaviour-gap/stored-only-properties.txt), then
annotate the legitimately-inert ones (`Tag`, the Win32 shell extras) in-file with the reason, as the
stub baseline does.
*Risk:* none. *Payoff:* this is the number that tracks whether the layer is getting more or less
hollow over time.

**W0.4 — An event-sequence test helper.**
A recorder that attaches to a control/form and appends every raised event name (with key args) to a
list, so a test can assert an entire ordered sequence in one assertion. Most of Phase 1 and 2's tests
need it, and several findings here are *ordering* bugs that a set of independent single-event tests
would happily pass.
*Files:* `tests/Majorsilence.Forms.Tests/` (new helper). *Risk:* none.

---

### Phase 1 — The keyboard chain (RC-1) *(highest leverage in the audit)* — **DONE**

**W1.1 — Dispatch the pre-processing chain.**
Give `WindowBase.HandleKeyDown` a real `PreProcessMessage` step that runs the WinForms order:
`ProcessCmdKey` up the parent chain first (so menu shortcuts win), then `IsInputKey` on the focused
control, then `ProcessDialogKey` if the control did not claim the key, then `KeyDown`/`KeyPress`, with
`ProcessKeyPreview` consulted on the form when `KeyPreview` is set. Mirror the same chain on
`Control` so a nested container's override participates. Verify the exact order against upstream
`Control.PreProcessMessage`/`ProcessKeyMessage`/`ProcessKeyEventArgs` — do not infer it.
*Closes:* `FRM-05`, `SVC-02`, `CTL-28`, and the dispatch half of `FRM-28`.
*Files:* `WindowBase.cs` (`HandleKeyDown`), `WindowBase.Compat.cs`, `Control.Compat.cs`,
`ControlAdapter.cs`. *Risk:* **high** — this is the input path for every key in the framework. Land it
with the W0.4 recorder and a sequence test per key class before anything else builds on it.

**W1.2 — Honour the control's claim on the key.**
With W1.1 in place, implement `IsInputKey` on the controls that need it and stop hard-wiring
Tab/Enter ahead of the focused control in `ControlAdapter.RaiseKeyDown`. Make `e.Handled` and
`SuppressKeyPress` actually suppress downstream processing — today `OnKeyDown` overwrites `Handled`
rather than combining it, and `OnKeyPress` never reads it, so every "digits-only" `KeyPress` filter in
every migrated app is dead.
*Closes:* `TXT-01` (P0), `TXT-09`, `TXT-10`, `SVC-04`, `SVC-05`, `SVC-07`, `SVC-08`.
*Files:* `TextBox.cs`, `TextBoxBase.cs`, `ControlAdapter.cs`, `Control.Events.cs`.

**W1.3 — Menu shortcuts and mnemonics.**
On the chain from W1.1: `ToolStripMenuItem.ShortcutKeys` and legacy `MenuItem.Shortcut` resolved
through `ProcessCmdKey`; `&File` / Alt+F through `ProcessMnemonic`; F10/Alt to focus the menu bar;
arrows and Escape within an open menu.
*Closes:* `TSM-02` (P0), `TSM-13`, `FRM-09`, `SVC-06`.
*Files:* `WinFormsCompat.cs` (ToolStrip family), `MenuBase.cs`, `Mnemonics.cs`, `Application.cs`.

**W1.4 — Accept/Cancel button and dialog keys.**
Enter → `AcceptButton` only after the focused control declines the key; Escape → `CancelButton`;
"focused button becomes the default" semantics; Alt/Ctrl modifiers respected.
*Closes:* `FRM-08`, `SVC-03`. *Files:* `Form.cs`, `WindowBase.cs`.

---

### Phase 2 — Focus, validation and `ActiveControl` (RC-2) — **DONE**

**W2.1 — Turn on the container-control subsystem.**
Implement `IsFocusManagingContainerControl` properly and give `ControlAdapter` a real
`IContainerControl`. That alone makes `GetContainerControl()`, `ActiveControl`, `ValidateChildren` and
`SelectNextIfFocused` reachable.
*Closes:* `CTL-02`, `CTL-07`, `CTL-08`, `CTL-09`, `CTL-30`, `CTL-31`, `FRM-10`.
*Files:* `Control.cs`, `Control.Compat.cs`, `ControlAdapter.cs`, `UserControl.cs`, `Form.cs`.
*Risk:* medium — changes which control has focus in existing scenarios.

**W2.2 — Move validation to the focus-switch choke point.**
Validation currently runs inside `OnLostFocus`, after focus has already moved, so `e.Cancel`,
`CausesValidation` on the *entering* control and `AutoValidate` cannot be honoured. Move it to the
switch point (`ControlAdapter.SelectedControl`) and implement upstream's order: `Leave` on the leaving
control and its exclusive ancestors → `Validating`/`Validated` → `Enter` on the entering chain →
`LostFocus`/`GotFocus`. Cancel returns focus and suppresses `Enter`.
*Closes:* `CTL-01`, and unblocks `BND-07`.
*Files:* `ControlAdapter.cs`, `Control.cs`, `Control.Events.cs`. *Risk:* medium-high.

---

### Phase 3 — Form and application lifecycle (RC-3, RC-10) — **W3.1–W3.5 DONE**

**W3.1 — Make a form reusable.**
Reset `_loadFired`, `_formClosedFired`, `shown`, `visible` and `dialog_result` when the window is
recreated, in `OnBackendClosed`. Set `Modal` for the duration of `ShowDialog`. Make `Hide()` end a
modal loop. Make `Visible` false after `Close()`. Do not dispose a non-modal form on `Close()`; do not
leave a modal one undisposed where upstream keeps it alive for result-reading.
*Closes:* `FRM-02` (P0), `FRM-03`, `FRM-04`, `FRM-07`, `FRM-12`, `FRM-13`, `FRM-16`.
*Files:* `WindowBase.cs`, `Form.cs`. *Tests to invert:* `FormLoadShownOrderTests.Load_fires_exactly_once`,
`FormDisposeClosesWindowTests.*`, `FormHandleCreatedTests.OnHandleCreated_precedes_OnShown`.

**W3.2 — Stop synthesising dialog results (RC-10).**
`ShowDialog()` with no open owner returns `OK` without showing anything; `MessageBox` implements 3 of
7 button sets and returns `OK` for the rest; `FileDialog`/`FolderBrowserDialog` return `Cancel`
without showing. Show against the active form or a hidden owner instead, and implement the remaining
button sets, the icons and the default-button/Enter/Escape mapping.
*Closes:* `FRM-01` (P0), `SVC-01` (P0), `SVC-19`, `SVC-20`, `SVC-21`, `SVC-25`, `FRM-26`, `SVC-31`.
*Files:* `Form.cs`, `MessageBoxForm.cs`, `FileDialog.cs`, `FolderBrowserDialog.cs`, `PrintDialog.cs`.
*Note:* the "Yes/No/Cancel dialog that silently always takes Yes" is the single most dangerous item in
this audit for data integrity.

**W3.3 — The owner graph.** `Owner`, `OwnedForms`, `Show(owner)`, `ShowDialog(owner)`, owner-close
cascade, and disabling *all* windows for a modal rather than only the owner.
*Closes:* `FRM-14`, `FRM-15`, `FRM-30`. *Files:* `Form.cs`, `FormCollection.cs`, `Application.cs`.

**W3.4 — Application lifecycle.** `Exit()` walking `OpenForms` and raising `FormClosing`/`FormClosed`;
`ApplicationExit` actually invoked; `Idle` raised by the loop; `ThreadException` raised instead of the
process dying; `Restart()` relaunching.
*Closes:* `FRM-21`–`FRM-25`. *Files:* `Application.cs`, both backends' loops.

**W3.5 — Take the title bar out of the client area. — DONE**
`FormTitleBar` was an implicit child of the collection `Form.Controls` hands out, and `ClientSize`
reported the whole backend surface, caption included. There is now an implicit `FormClientArea`
(a fill-docked `ScrollableControl`) beside it, and `Form.Controls`/`ContentRoot` hand out *its*
collection — so `(0, 0)` is below the caption, `ClientSize`/`ClientRectangle` describe the usable
region, and `SetClientSizeCore` adds the caption back when sizing the window. `SystemInformation.CaptionHeight`
now reports the height actually drawn instead of a constant four pixels short of it.

*Closed:* `FRM-06` (P0), `FRM-39`. 8 contract tests.

It also turned up a defect of its own, filed as **`EVT-39`**: the gesture entry points take device
pixels while the routing compares against logical bounds, so a long press lands at `1/scale` of where
it was aimed. That mismatch predates this work — the extra level of nesting simply made it large
enough to miss a control rather than merely misplace the hit inside one.
`GestureTests.HandleLongPress_OpensContextMenu` returns early above scaling 1 with the reason in a
comment, so the scaled case is recorded as broken rather than quietly untested.

**Three things this cost, worth knowing before touching the same area:**

- **`WindowBase` forwards about a dozen members to `adapter` that mean "the client surface", not
  "the window":** `Padding`, `Contains`, `HasChildren`, `ContextMenu(Strip)`, `ImeMode`, the
  `BackColorChanged`/`ForeColorChanged`/`PaddingChanged`/`ControlAdded`/`ControlRemoved` forwards, the
  `AutoScroll*` family, and `Form`'s own mouse-event forwards. An `internal virtual Control ContentRoot`
  seam handles them in one place. The client area has to be a `ScrollableControl` because that is the
  type whose `DisplayRectangle` is deflated by `Padding`.
- **Do not suppress the container's painting with a transparent background colour.**
  `GetEffectiveBackgroundColor` resolves ambient colour by walking the *parent chain*, so an explicit
  transparent on the client area becomes the answer for every descendant: buttons and labels paint
  transparent and whatever is behind them shows through, which reads exactly like a child being
  overpainted by its parent. Override `OnPaintBackground` to do nothing instead. This was the entire
  cause of a first attempt's failures, initially and wrongly diagnosed as paint sensitivity to nesting
  depth — a probe showed a zero-sized intermediate paints its children perfectly well.
- **Dock layout runs in z-order and the loop walks children backwards**, so the last child added is
  docked first. The `Fill` client area therefore has to be added *before* the `Top` title bar, or it
  claims the whole window before the caption takes its strip.

**W3.6 — `AutoScaleMode` / `AutoScaleDimensions`.** Stored-only today. Every designer file emits them
and expects font-ratio scaling. *Closes:* `FRM-17`. *Risk:* medium-high — interacts with W3.5 and RC-8.

---

### Phase 4 — Data binding (RC-4)

**W4.1 — Make the `CurrencyManager` a live object.**
Cache it per (source, member) and keep it; subscribe it to `IBindingList.ListChanged` and
`INotifyPropertyChanged`; have `Binding` register itself in `manager.Bindings` so the manager can
`PullData`/`PushData`. Stop rebuilding it at `EndInit`.
*Closes:* `BND-01` (P0), `BND-02` (P0), `BND-10`, `BND-14`, `BND-16`, `BND-19`, `BND-28`.
*Files:* `BindingSource.cs`, `BindingContext.cs`, `BindingRuntime.cs`, `AppMenuBindingParity.cs`.
*Risk:* medium-high — it is the centre of the binding runtime. This is the item that makes the
designer's own ordering (`BeginInit` → `DataBindings.Add` → `EndInit` → fill in `Load`) work at all.

**W4.2 — `TypeDescriptor`, not reflection.** Resolve source members, target properties and
`ListBindingHelper.GetProperty` through `TypeDescriptor` so `DataRowView` columns, `ICustomTypeDescriptor`
and dynamic sources are visible. *Closes:* `BND-03` (P0), `BND-30`.

**W4.3 — The validation/edit half.** Write on `Validating` (not `Validated`/`LostFocus`) so a binding
can be cancelled; implement `EndCurrentEdit`/`CancelCurrentEdit`; call `IEditableObject.BeginEdit` when
an item becomes current so `CancelEdit` can roll back; honour `ICancelAddNew`.
*Closes:* `BND-07`, `BND-08`, `BND-09`. *Depends on:* W2.2.

**W4.4 — Report conversion failure instead of writing `default(T)`.** `Coerce` returning null conflates
"could not convert" with "convert to null", so a half-typed number writes `0` into the source. Give it
a distinguishable failure, surface it through `BindingComplete` and `DataError`.
*Closes:* `BND-13`, `BND-18`. *Tests to invert:*
`BindingRuntimeTests.A_half_typed_number_does_not_throw_and_leaves_the_source_alone`.

**W4.5 — `ResolveList`'s catch-all.** `Type`, scalar objects and non-`DataSet` `DataMember` paths all
fall into `_ => new List<object?>()` and come back as a silent empty list. Implement `Type` →
`BindingList<T>`, scalar → single-item list, and `DataMember` master/detail re-targeting.
*Closes:* `BND-04`, `BND-05`, `BND-06`. *Tests to invert:* `BindingSourceTests.DataSource_SetNonList_IsEmpty`.

**W4.6 — `BindingNavigator`.** Wire the standard items to the `BindingSource`; stop `EndInit` from
`Items.Clear()`-ing the designer's own items. *Closes:* `BND-11`, `BND-12`.

---

### Phase 5 — Per-control behaviour

Independent of each other; parallelise freely. Each closes a block of P0/P1 findings in one control
family. Ordered by traffic in a typical LOB app.

**W5.1 — `DataGridView` editing lifecycle.** `BeginEdit(bool)`, per-column editor types, the dirty
flag (`IsCurrentCellDirty`, `NotifyCurrentCellDirty`, `CurrentCellDirtyStateChanged`), typed conversion
via `ParseFormattedValue`, `DataError` raised rather than swallowed, `ReadOnly` honoured, and the
upstream event order (`CellBeginEdit` → `EditingControlShowing` → … → `CellEndEdit`).
*Closes:* `DGV-01` (P0), `DGV-06`, `DGV-07`, `DGV-08`, `DGV-10`, `DGV-11`.

**W5.2 — `DataGridView` cell/row/column objects become participants.** `Cell.Value` setter raising
`CellValueChanged` and repainting; `Row.Visible` actually hiding; `Row.Selected`/`Cell.Selected`;
`Column.DisplayIndex`. They are passive auto-properties today, so none of them change anything.
*Closes:* `DGV-02` (P0), `DGV-20` (P0), `DGV-14`.

**W5.3 — `DataGridView` incremental data binding.** `OnBoundListChanged` ignores `ListChangedType` and
regenerates every column and row on any change — which is also why `RowsAdded` never fires for bound
rows and `RowsRemoved` fires spuriously. Honour the change type.
*Closes:* `DGV-31` (P0), `DGV-32`, `DGV-33`, `DGV-03` (P0).

**W5.4 — `DataGridView` styles, sizing and sorting.** Make the renderer read the WinForms properties
instead of its private twins (`DefaultCellStyle.Alignment`, `HeaderCell.SortGlyphDirection`,
`SortMode`, `row.Selected`, `GridColor`, `BackgroundColor`); implement `AutoSizeColumnsMode.Fill`,
`AutoResize*`, `RowTemplate`, and record `SortedColumn`/`SortOrder`.
*Closes:* `DGV-16`, `DGV-17`, `DGV-18`, `DGV-19`, `DGV-21`, `DGV-22`, `DGV-13`.

**W5.5 — `DataGridView` mouse and keyboard.** The 24 `add { } remove { }` events with existing trigger
points; keyboard handled on `KeyUp` instead of `KeyDown`; Enter/Delete/Ctrl+C/Home/End/Tab.
*Closes:* `DGV-29`, `DGV-30`, `DGV-25`, `DGV-26`.

**W5.6 — `ListView` is not a list view.** `View` is stored-only, so every item renders as a 70px
large-icon tile whatever the mode — `Details` (the overwhelmingly common choice) does not exist. Plus
vertical scrolling, `EnsureVisible`/`TopItem`, and the dropped `ColumnClick`/`ItemActivate`/`ItemCheck`
handlers. *Closes:* `LST-01` (P0), `LST-17`, `LST-18`, `LST-19`, `LST-12`.
*This is the largest single visual divergence in the audit.*

**W5.7 — `CheckedListBox` has no checkboxes.** No glyph is drawn and no click toggles one; the control
is a `ListBox` with unreachable bookkeeping. *Closes:* `LST-02` (P0), `LST-16`.

**W5.8 — Selection events on list controls.** Every selection path except the `SelectedIndex` setter
— `SelectedItem =`, `SetSelected`, `ClearSelected`, multi-select mouse and keyboard — mutates through
the collection's internal setters and never raises `SelectedIndexChanged`.
*Closes:* `LST-03` (P0), `LST-04` (P0), `LST-06`, `LST-09`.

**W5.9 — `TreeView`.** `SelectedNode` returning a synthetic root instead of null; `GetNodeAt`/`HitTest`
built on a fake layout that disagrees with the mouse's; images, checkboxes, `NodeFont`/colours,
`Sorted`, `Indent`, `ItemHeight` all stored-only; the `BeforeSelect`/`BeforeCheck`/`BeforeCollapse`
family discarded. *Closes:* `LST-05` (P0), `LST-11`, `LST-21`–`LST-26`.

**W5.10 — `ComboBox` editable region.** `DropDownStyle.DropDown` is the default and there is no text
region at all — no `SelectionStart/Length/SelectedText`, no `MaxLength`, no autocomplete.
*Closes:* `LST-07`, `LST-08`.

**W5.11 — `TextBox` stored-only behaviour.** `WordWrap`, `CharacterCasing`, `ScrollBars`,
`HideSelection`, `ShortcutsEnabled` each have exactly one obvious consumer. Plus the two crash paths:
typing when existing text exceeds `MaxLength`, and the caret walking into `PlaceholderText`.
*Closes:* `TXT-05`, `TXT-06`, `TXT-07`, `TXT-11`, `TXT-12`, `TXT-22`, `TXT-26`.

**W5.12 — Stop routing mutations through the `Text` setter.** `AppendText`, `RichTextBox.AppendText`
and both `SelectedText` setters assign `Text`, which resets caret to 0, scrolls to top, clears undo and
clears `Modified` — so a log window shows its oldest line after every append.
*Closes:* `TXT-02` (P0), `TXT-35`.

**W5.13 — `MaskedTextBox` mask engine.** The mask is never enforced; `Text` is raw input and
`MaskCompleted`/`MaskFull` return `true` unconditionally, so mask validation always passes. Use
`System.ComponentModel.MaskedTextProvider`. *Closes:* `TXT-03` (P0), `TXT-19`, `TXT-18`.
*Tests to invert:* `MaskedTextBoxTests.MaskCompletedAndMaskFull_AlwaysTrue`, `Text_SetUnaffectedByMask`.

**W5.14 — `RichTextBox` document model.** `Rtf` returns a stale stored string, so saving after editing
writes the old content; `Find` is case-sensitive by default and never selects; the whole `Selection*`
formatting family is stored-only. *Closes:* `TXT-04` (P0), `TXT-14`–`TXT-17`.

**W5.15 — `ToolStrip` item storage and appearance.** Remove the `new` shadows so `Enabled`, `Checked`,
`Tag`, `Height`, `Alignment` and the mouse events reach the members the renderer and hit-test actually
read; then make the renderers consume `DisplayStyle`, `Checked`, `ForeColor`/`Font`, `Alignment`,
`Spring`, `ToolTipText`. *Closes:* `TSM-01` (P0), `TSM-04`, `TSM-06`, `TSM-30`, `TSM-31`.

**W5.16 — Strip facade and coordinates.** `Menu`/`MenuDropDown` re-exposing `Items => RootItems`
bypasses `ToolStrip`'s facade callbacks, so `ItemAdded`/`ItemRemoved`/`ItemClicked` are dead on
`MenuStrip`/`ContextMenuStrip`; `Show(control, point)` treats a client point as screen.
*Closes:* `TSM-03` (P0), and the facade group.

**W5.17 — Text measurement is wrong at the root (drawing).**
`TextRenderer.MeasureText(string, Font)` measures at the font's **point size treated as pixels**, so
every measurement is off by the point→pixel ratio; it also word-wraps by default where upstream does
not, and adds none of the GDI padding upstream adds. `Graphics.DrawString` into a `RectangleF` never
word-wraps at all. Layout code all over the framework and in migrated apps depends on these two.
*Closes:* `GFX-25` (P0), `GFX-06` (P0), `GFX-26`, `GFX-27`, `GFX-28`, `GFX-14`.
*Risk:* **high** — changes measured sizes everywhere, so it will move rendered layout in many tests.
Land it early in the phase, not late, because per-control sizing work done before it will be tuned
against wrong numbers.

**W5.18 — Pens lose everything but colour and width.**
Every simple stroke call discards the `Pen`'s dash style, caps, join and brush — so dashed focus
rectangles, grid lines and custom borders all draw solid. Plus: `SmoothingMode.Default` antialiases
where GDI+ does not; `IntersectClip(Region)` replaces instead of intersecting; clips are reduced to
their bounding rectangle; `SetClip(GraphicsPath)` flattens to control points; `DrawImage` with
`ImageAttributes` + callback drops the attributes.
*Closes:* `GFX-23` (P0), `GFX-07`, `GFX-12`, `GFX-13`, `GFX-24`, `GFX-15`, `GFX-08`, `GFX-09`.

**W5.19 — `ControlPaint`'s chrome family, and the themed/classic fork above it.**
20 empty methods — the primary way an owner-drawn migrated control paints a border, a button face, a
check glyph or a focus rectangle. Worth treating as one item rather than twenty: about two-thirds need
only rectangles. Also fix `Light`/`LightLight`/`Dark`/`DarkDark`, which take the percentage as 0–100 and
add linearly in RGB where upstream takes a 0–1 fraction through the Win32 HLS algorithm and
short-circuits to the exact `SystemColors.Control*` values.

Fix the fork above it in the same pass. `Application.RenderWithVisualStyles` returns `true` while
`VisualStyleRenderer.IsSupported` returns `false` and `DrawBackground` is empty — so the standard
`if (visual styles) … else …` that every themed custom control is written around **takes the themed
branch and draws nothing**, never reaching the (also empty) `ControlPaint` fallback. Two literals that
upstream derives from each other; assert the invariant in a test rather than the values.

*Closes:* `GFX-01`, `GFX-02`, `GFX-03`, `GFX-38`.

**W5.20 — The value controls are not implemented.**
Four controls in this family look present and are not: `MonthCalendar` draws no calendar and cannot be
clicked; `DateTimePicker` derives from `TextBox`, has no drop-down calendar, and its `Text` is
free-form and never parsed back into `Value`; `ErrorProvider` never renders anything; `NumericUpDown`
has no keyboard input and its arrows step by 1 regardless of `Increment`.
Taken together with `MonthCalendar`, **there is no working date-picking UI in the framework** —
`DateTimePicker`'s drop-down button is painted but dead, and the calendar it would open draws one line
of text and has no mouse handling.

Two arithmetic bugs in `ScrollBar` belong with this item because they are the same class of defect and
invisible to property tests: `Value` is allowed to reach `Maximum` instead of stopping at
`Maximum - LargeChange + 1`, and `OnMouseWheel` multiplies by the raw ±120 `Delta`, so one wheel notch
moves **120 × `SmallChange`**.

*Closes:* `SMP-42` (P0), `SMP-39` (P0), `SMP-40` (P0), `SMP-51` (P0), `SMP-31` (P0), `SMP-32` (P0),
`SMP-33`, `SMP-41`, `SMP-43`, `SMP-47`, `SMP-48`, `SMP-49`.
*Note:* `SMP-36`/`SMP-37` are structural — `NumericUpDown` does not derive from `UpDownBase`, and
`DomainUpDown` derives from `NumericUpDown` and renders a number instead of its items. Fix the shape
first or the member work lands twice.

**W5.21 — Buttons, labels and pictures.**
`RadioButton` ignores `AutoCheck` and never manages `TabStop`; `Appearance`, `FlatStyle`/`FlatAppearance`
are stored-only on `CheckBox`/`RadioButton`; button and label captions never word-wrap (`Label.Multiline`
defaults false, so a long caption clips instead of wrapping — upstream wraps); `Label.BorderStyle` is
never drawn; `PictureBox.Load()` is asynchronous and swallows failures, `SizeMode` does not invalidate.
*Closes:* `SMP-01`, `SMP-02`, `SMP-03`, `SMP-05`, `SMP-13`, `SMP-14`, `SMP-15`, `SMP-20`, `SMP-21`,
`SMP-23`, `SMP-26`, `SMP-29`, `SMP-07`.

**W5.22 — `SplitContainer` and `Splitter`.**
`Panel1MinSize`/`Panel2MinSize`/`FixedPanel` stored-only; `SplitterMoving`/`SplitterMoved` never raised;
the legacy `Splitter` does not resize its docked sibling — its entire purpose. Structurally, `Panel1`/
`Panel2` are `Panel` rather than `SplitterPanel`, and the constructor forces `Dock = Fill`.
*Closes:* `LAY-01`–`LAY-05`, `LAY-07`, `LAY-08`.

**W5.23 — `TabControl`.** `TabPage.Enabled` structural; the Selecting/Selected/Deselecting/Deselected
order; `ImageList` + `TabPage.ImageIndex`; `Alignment`/`ItemSize`/`SizeMode` stored-only.
*Closes:* `LAY-12`–`LAY-15`.

**W5.24 — Scaling and preferred size: connect the engine that already works.**
The single most encouraging result in the audit is a *non*-finding: normalised diffs of `Layout/`
against upstream's `DefaultLayout`, `FlowLayout`, `TableLayout`, `CommonProperties` and `LayoutUtils`
show only naming differences, `#if DEBUG` blocks and the opt-in AnchorLayoutV2 path. **The layout
engines are a faithful port.** What is broken is the wiring into them:

- `Panel.GetPreferredSize` overrides the *public* method with a hand-rolled child-bounds scan instead
  of going through `GetPreferredSizeCore` → `LayoutEngine.GetPreferredSize`. `FlowLayoutPanel` and
  `TableLayoutPanel` inherit that override, so an `AutoSize` container never consults its own
  correctly-ported engine and ignores `proposedSize`, `Padding` and Min/MaxSize.
- `Control.Scale(SizeF)` goes straight to `ScaleCore` (bounds only). `ScaleControl(SizeF,
  BoundsSpecified)` and `DefaultLayout.ScaleAnchorInfo` have **no callers at all**, so `Padding`,
  `Margin`, Min/MaxSize and anchor distances are never DPI-scaled and an app's `ScaleControl`
  override never fires.
- `ButtonBase`/`Button`/`CheckBox`/`RadioButton` have no `GetPreferredSizeCore` at all, so
  `AutoSize = true` on a button never measures its text. All four have one upstream.

*Closes:* `LAY-25` (P0), `LAY-21` (P0), `LAY-34`, `LAY-26`. *Pairs with:* W3.6 (`AutoScaleMode`) and
W5.17 (text measurement — a button that finally measures its text needs the measurement to be right).
*Leverage:* high. This is a small amount of wiring in front of a large amount of working machinery.

**W5.25 — Scrolling containers.** `ScrollableControl.DisplayRectangle` carries neither the scroll offset
nor the content size, and `ScrollControlIntoView` is a no-op — so an `AutoScroll` panel scrolls nothing
into view. Plus `Panel.BorderStyle` never drawn or inset, and `TableLayoutPanel.CellPaint`/cell borders.
*Closes:* `LAY-29`, `LAY-30`, `LAY-28`, `LAY-22`.

---

### Phase 6 — Mechanical sweeps *(parallelisable, low risk, high count)*

**W6.1 — The dead-event sweep (RC-5).** Convert `add { } remove { }` to real events and raise them at
the trigger point that already exists. Work area by area against the W0.1/W0.2 baselines; each area is
an independent branch. ~60 findings.

**W6.2 — The stored-only sweep (RC-7).** For each entry in the W0.3 baseline, either wire it to its one
consumer or record in the baseline *why* it is legitimately inert. Prefer deleting a private twin over
keeping both (RC-6).

**W6.3 — Coordinate-space audit (RC-8).** Every public hit-test and rectangle-returning member:
confirm which space it is in and convert once at the boundary. `ListBox.GetIndexAtLocation` is the
worked example. Add scale-2 tests for the paths the existing HiDPI gate does not reach.

**W6.4 — Getters that guess (RC-9).** Promote the drawing plan's rule to the whole layer: a member that
*reports state* must compute it or throw, never return a plausible constant. Start with
`MaskCompleted`, `RichTextBox.Rtf`, `FontFamily.IsStyleAvailable`, `ImageCodecInfo.GetImageDecoders`.

**W6.5 — Matrix corrections.** The table above. Cheap; do it early so the docs stop overstating while
the code catches up.

## Tests that pin the divergence

A meaningful number of existing tests assert the *current* behaviour, because they were written from
the implementation rather than from upstream. They will fail when the corresponding item lands, and
the correct response is to invert them with the reason in the commit message — not to work around them.
This is the same trap `winforms-gap-plan.md` records twice ("a hand-written test asserting
`SearchDirectionHint` as `0..3` failed against the generated `37..40` … the generated values were right
and the human assertion was wrong"; and the `DataGridViewColumn.AutoSizeMode` default, where "an
existing test asserted the wrong default and was corrected").

Known instances, by the item that will break them:

| Item | Tests to invert |
|---|---|
| W3.1 | `FormLoadShownOrderTests.Load_fires_exactly_once`, `FormDisposeClosesWindowTests.Disposing_a_shown_form_marks_it_not_visible`, `FormDisposeClosesWindowTests.Disposing_does_not_raise_FormClosing`, `FormHandleCreatedTests.OnHandleCreated_precedes_OnShown` |
| W3.5 / W3.6 | `GestureTests` (title-bar offsets), `FormTests.CenterToScreen_Invoke_SetsStartPosition` |
| W3.2 | `FormTests.MaximizeBox_Set_GetReturnsExpected` |
| W4.4 | `BindingRuntimeTests.A_half_typed_number_does_not_throw_and_leaves_the_source_alone` |
| W4.5 | `BindingSourceTests.DataSource_SetNonList_IsEmpty` |
| W5.1 / W5.4 | `DataGridViewTests.Ctor_Default`, `DataGridViewRowTests.Height_SetDefault_IsTwentyFive`, `DataGridViewTests.NewRowIndex_ReflectsAllowUserToAddRows`, `DataGridViewTests.ClearSelection_ResetsCurrentRowAndCell`, `DataGridViewHookTests.CellParsing_NotHandled_StoresTheEditedTextAsBefore`, `DataGridViewHookTests.GetClipboardContent_UsesTheFormattedValue` |
| W5.12 | `TextBoxBaseTests.Clear_and_AppendText_work_through_the_base`, `RichTextBoxTests.AppendText_Invoke_AppendsAndMovesSelection` |
| W5.13 | `MaskedTextBoxTests.Text_SetUnaffectedByMask`, `MaskedTextBoxTests.UseSystemPasswordChar_Set_GetReturnsExpected`, `MidSizeControlParityTests.MaskedTextBox_ValidateText_converts_or_reports_null` |
| W5.14 | `RichTextBoxTests.Rtf_Set_GetStripsToPlainText`, `OverloadParityTests.RichTextBox_Find_over_a_character_set_honours_the_range` |
| W1.2 | `TextBoxTests.MaxLength_DefaultsToZero`, `TextBoxTests.MaxLength_LimitsTextLengthOnInput` |
| Services | `CursorTests` (`HSplit == SizeWestEast`), `FileDialogTests` (`FileName` returns a full path), `ClipboardTests` (`GetText(Rtf)` returns plain text), `PrintingSurfaceTests` (calls `OnStartPage` by hand) |

The per-area finding files carry a **Tests today** line on every finding; treat that as the
authoritative list and this table as the map of the big ones.

## Progress

| Phase | Status |
|---|---|
| 0 — Make it measurable | **Done.** Three baseline gates and the event recorder; 8 self-tests. |
| 1 — The keyboard chain | **Done.** The chain is dispatched, controls can claim keys, menu shortcuts and access keys work; 25 tests. |
| 2 — Focus, validation, `ActiveControl` | **Done.** One focus choke point running WinForms' sequence; validation can cancel; containers are containers again; 14 tests. |
| 3 — Form and application lifecycle | **W3.1–W3.5 done** (reuse, real modal dialogs, the owner graph, `Application` lifecycle, the client area); 35 tests. W3.6 (`AutoScaleMode`) outstanding, and blocked on W5.17. |
| 4 — Data binding | Not started. |
| 5 — Per-control behaviour | Not started. |
| 6 — Mechanical sweeps | Not started. |

Suite: **3962 passing, 0 failing**, in Debug and Release and under `MF_HEADLESS_SCALE=2`. The API gap
gate still reports zero for both surfaces. Baselines: inert events 80 → 79, unraised events 130 → 127,
stored-only properties 822 → 812.

### What phase 0 found

**The stored-only question was being asked wrongly, and the first answer was 1249.** The gate's first
cut asked "is the backing field read anywhere other than the getter", which reports every
properly-encapsulated property as inert — `form.AcceptButton != null` compiles to a `callvirt` on the
getter, not a field load. `Form.AcceptButton` appearing in the output is what exposed it. A property
is consumed if *either* its getter is called or its field is read elsewhere; with both checked the
figure is **822 of 1254**. Worth remembering if these scans are ever extended: the encapsulated path
is the easy one to forget, and forgetting it produces a confidently wrong number.

**The source-level estimate was low by a factor of three.** The grep this audit started from found 263,
because it only counted properties whose *name* appeared nowhere else in the source — which misses
every one mentioned in a comment or sharing a name with a working member on another type.

**Absence from a baseline is not a certificate, and three known-broken properties prove it.**
`ListView.View`, `TextBox.WordWrap` and `TextBox.AcceptsReturn` are all read by something and all
ignored where it matters — respectively by an image-list lookup while the renderer ignores the mode, by
a `ToolStripTextBox` forwarder that draws nothing, and by a key handler nothing called. "Read by code
that is itself inert" needs transitive reachability; the gates are the floor, not the ceiling. This is
recorded in the test's own remarks so the next reader does not over-trust it.

**Release and Debug IL differ enough to crash a naive walker.** `MetadataTokens.EntityHandle` throws
for a table it does not model, and the optimiser emits shapes Debug does not, so the scan died on the
Release assembly having passed on Debug. The gate now filters by table byte before converting — which
also contains any future alignment slip, since a garbage operand rarely carries one of the six valid
bytes. CI runs Release; a gate that only works in Debug is not a gate.

### What phase 3 found

**W3.5 took two attempts, and the first diagnosis was wrong.** The title-bar item is the plan's own
high-risk P0. The first attempt failed three paint tests and I read that as the extra nesting level
upsetting the paint path; the second attempt began by writing a probe for exactly that, which passed —
a zero-sized intermediate paints its children perfectly well. The real cause was one line of the first
attempt's own setup: setting the client area's background to transparent, which ambient colour
resolution then handed to every descendant. Reverting rather than pushing on was still right, but the
lesson is narrower than "this area is dangerous": **the failing tests were describing a colour problem
and I read them as a geometry problem.** Both attempts are written up in W3.5's entry.

**Making the ownerless dialog modal hung the test suite rather than failing it.** `RadGridExportTests`
had a comment explaining, at length, that `MessageBox.Show` with no owner "falls back to a non-modal
Show() and returns DialogResult.OK immediately" and leaks the form for the caller to clean up — the
divergence written down as intended behaviour. With the fix the call correctly waits for an answer
nobody gives. The test now dismisses the dialog through the UI queue and asserts `Cancel`, on a
background thread with a join timeout so the next regression here fails in ten seconds instead of
wedging CI. **A test that documents a bug is the most expensive kind to have**: it reads as
justification rather than as a defect.

**Disposing a form from its own close re-entered the close sequence.** `Form.Close` calls
`CompleteClose` after the backend callback has already run it, so the "was this modal?" test answered
differently the second time and disposed a dialog out from under the caller about to read its
`DialogResult`. Two existing tests caught the first version — `Closed`/`FormClosed` fired twice, and
`ApplicationContext.ExitThreadCore` was notified twice. The decision now lives in the re-entrancy-
guarded backend path with `wasModal` captured before anything clears it.

**`IsHandleCreated` and the `HandleCreated` event were two different moments.** The property is
`shown`, set by `MarkHandleCreated`; the event is forwarded to the root adapter and raised by
`Control.CreateControl`. Fixing the property's timing alone left the event still arriving after `Load`,
so an override and a subscription still disagreed. Both are now before `Load`, as upstream.

**`Application.Exit` and `OpenForms` are process-wide, which limits what can honestly be tested.** The
suite runs collections in parallel, so a test that completed an `Exit` would close other tests' forms
and fail them for the wrong reason. The `Exit` tests here end in a cancelled close — real coverage of
the `OpenForms` walk and the cancel contract, no teardown — and `Restart` is not tested at all, because
it relaunches the test host. Worth stating rather than quietly leaving a gap.

### What phase 2 found

**The focus sequence was inconsistent with itself.** `Control.Select ()` raised the *entering*
control's Enter/GotFocus and only then told the adapter, whose setter deselected the leaving one — so a
mouse click produced `B.Enter, B.GotFocus, A.Leave, A.LostFocus` while Tab, which went through a
different path, produced the opposite. The same application saw two different orders depending on how
focus moved. There is now one choke point (`ControlAdapter.ChangeFocus`) and both paths run through it.

**Validation was attached to the one event where it cannot work.** It ran inside `OnLostFocus`, after
focus had already moved, so `e.Cancel` had nothing left to prevent — the standard "cancel to keep focus
in the invalid field" idiom did nothing at all, and neither the entering control's `CausesValidation`
nor the container's `AutoValidate` was ever consulted. All three work now, and `AutoValidate` moved out
of the stored-only baseline as a result.

**`ActiveControl`'s getter is a stored field upstream, not a live search.** The first implementation
derived it from "which descendant is focused", which is right when focus can move and wrong in the
ordinary designer case of assigning `ActiveControl` before the container is on a shown form. The
existing `UserControlTests.ActiveControl_Set_GetReturnsExpected` — one of the tests this plan expected
to have to invert — caught it, and turned out to be asserting the correct contract all along. It passes
unchanged.

**`IContainerControl.ActiveControl` was declared non-nullable here and `Control?` upstream.** A small
divergence, but it is what made `UserControl` unable to implement the interface, which is what made
`GetContainerControl ()` return null, which is what made the whole subsystem unreachable.

**A real bug in the phase 0 IL walker, found by phase 2's Release run.** `box` (0x8C) takes a 4-byte
type token and had been sized 0, sitting inside the `conv.ovf.*.un` run — so every boxing conversion
shifted the walk four bytes and it read operands as opcodes from there on. Release IL boxes far more
than Debug, which is why it surfaced as an `IndexOutOfRangeException` there and silently truncated
scans here. Two properties were being reported stored-only whose only read sat after a `box`
(814 → 812). The walk now also refuses to step out of bounds rather than trusting the table, on the
same principle the token filter already followed: a gate that crashes on unfamiliar IL is worse than
one that stops reading it.

### What phase 1 found

**Confirmed: the chain had no callers at all.** `ProcessCmdKey`, `ProcessDialogKey`,
`ProcessDialogChar`, `ProcessKeyPreview`, `ProcessMnemonic` and `IsInputKey` were `=> false` on both
`Control` and `WindowBase`, and `grep` across the assembly found declarations and no invocations. The
behaviours they gate were hard-coded instead: `AcceptButton`/`CancelButton` at the very top of
`WindowBase.HandleKeyDown`, Tab inside `Control.RaiseKeyDown`. Both are now downstream of the chain,
which is what lets a multiline text box see Enter and a control claim Tab.

**Three stored-only properties became live, and the gate said so.** `ToolStripMenuItem.ShortcutKeys`,
`ButtonBase.UseMnemonic` and `Form.MainMenuStrip` all moved out of the stored-only baseline as a direct
result of this work — 822 → 818. That is the loop the guardrails exist to close: the number moves in
the right direction and the movement is reviewed rather than asserted.

**`TSM-01` is real, and a test found it independently.** `A_disabled_item_does_not_fire_its_shortcut`
failed on the first run: `save.Enabled = false` writes `ToolStripItem`'s `new`-shadowed property while
`KeyboardShortcuts` — like the renderers and `MenuBase`'s click dispatcher — reads `MenuItem.Enabled`
and still sees true. The shortcut path reads both for now, with the workaround commented and pointed at
W5.15; deleting the shadow is that item's job.

**`Control.ModifierKeys` is stale global state.** It is a static auto-property written by every
`KeyEventArgs` and `MouseEventArgs` constructor (`Control.Compat.cs:20`), so it reports whatever
modifiers the last constructed args happened to carry — including from an unrelated window, or from a
test running in parallel. It made one new test fail in Release and pass in Debug purely on scheduling.
Not fixed here (it belongs with `SVC-09`, which already flags `Control.MouseButtons` as the same
shape), but worth recording: **any code reading `ModifierKeys` outside a live key handler is reading a
value that may be arbitrarily old.**

**One test expectation was wrong, and upstream settled it.** "Ctrl+Tab moves focus out of a box that
accepts tabs" reads as obviously right and is not: `ContainerControl.ProcessDialogKey` guards its Tab
case on `(Alt | Control) == None`, so nobody claims Ctrl+Tab and focus stays put. The test now asserts
that, with the reasoning in a comment. Second time in this repo's history that checking upstream
overturned a plausible hand-written assertion rather than the code.

## Suggested execution order

The phases are dependency-ordered, but they are not all equally urgent and they do not all need the
same people. A reasonable sequencing for a small team or a queue of agents:

**First, and serially — the foundations.** W0.1–W0.4, then W1.1, then W2.1/W2.2. These are the items
everything else is measured or tested against, and W1.1 in particular changes the input path for every
key in the framework. One at a time, each with its own review. Expect the suite to move under you.

**Then, in parallel — the independent control families.** Phase 5 items barely touch each other:
`DataGridView` (W5.1–W5.5), lists (W5.6–W5.10), text (W5.11–W5.14), strips (W5.15–W5.16), value
controls (W5.20–W5.21), layout containers (W5.22–W5.25). Different owners, different branches.

**Two exceptions that must go early inside Phase 5.** W5.17 (text measurement) changes measured sizes
everywhere — any per-control sizing tuned before it lands is tuned against wrong numbers. W3.5 (title
bar out of the client area) moves every form's geometry. Both want to be near the front and on their
own.

**Anytime — the sweeps.** Phase 6 needs no coordination and makes good filler work between larger
items. W6.5 (matrix corrections) should be done immediately: it costs nothing and stops the
documentation overstating while the code catches up.

**A note on sequencing risk.** Three items are rated high risk — W1.1, W3.5, W5.17 — and all three
are load-bearing. Landing any two of them in the same review makes a regression impossible to
bisect. Keep them apart.

## Definition of done, for the whole effort

This document is finished when:

- the three baselines from Phase 0 exist, are gated in CI, and have **shrunk** from 84 / 89 / 263;
- every P0 is closed or has a recorded, reasoned decision not to close it;
- `COMPATIBILITY_MATRIX.md` no longer overstates any row this audit contradicted;
- the finding files in `docs/behaviour-gap/` are annotated with what was done, in the manner of
  `winforms-gap-plan.md`'s "What item N found" sections — because the things discovered *while*
  fixing have historically been more valuable than the original findings, and both predecessor plans
  say so.

Two lessons from those predecessors are worth restating here, because this audit re-confirmed both:

- **Generate, do not transcribe.** Values, defaults and event sequences should come from upstream, not
  from memory. Every time this repo has transcribed, it has transcribed something wrong.
- **A member that behaves plausibly is not a member that behaves like WinForms.** The
  `EditingControlWantsInputKey` story in `winforms-gap-plan.md` — a flat key list that "reads sensibly
  and is wrong", where upstream is caret-aware — is the exact failure mode this whole document is
  cataloguing.
