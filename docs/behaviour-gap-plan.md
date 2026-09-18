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

**Applied 2026-08-31 as `W6.5`** — 16 edits, each verified against the current source before it was
written, not transcribed from this list. See "What W6.5 found" below: two entries in the table were
themselves wrong by the time they were applied, and the audit had missed three overstatements worse
than any it listed.

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

> **Inverted when applied (2026-08-31).** Phase 1 landed between this list being written and its being
> applied, so the row that went into the matrix says the chain *works* — with the date it started
> working and the one key still unbound (Ctrl+Z, `TXT-13`). The instruction was right about the row
> being missing and wrong about what it should say; a correction list is only current on the day it is
> written.

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

### Phase 3 — Form and application lifecycle (RC-3, RC-10) — **DONE**

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

**W3.6 — `AutoScaleMode` / `AutoScaleDimensions`. — DONE (2026-08-31)**
Stored-only before this; every designer file emits them and expects font-ratio scaling. `Font` mode now
scales a container and its children once, by `CurrentAutoScaleDimensions / AutoScaleDimensions`, through
one shared `AutoScaleEngine` — `Form`, `ContainerControl` and `UserControl` are siblings here, not one
hierarchy, so the metric and the no-op rules had nowhere else to live in common. `Dpi` mode is
deliberately inert (see below). 11 tests; stored-only baseline 810 → 806.
*Closed:* `FRM-17`. *Risk as rated:* medium-high, and RC-8 was indeed where the design decision fell.

---

### Phase 4 — Data binding (RC-4) — **DONE (2026-09-01)**

**W4.1 — Make the `CurrencyManager` a live object. — DONE**
One manager for the life of the `BindingSource`, built over the BindingSource ITSELF — upstream's own
design (`new CurrencyManager(this)`), and simpler than the item as written: the BindingSource is an
`IBindingList`, so one subscription to its `ListChanged` carries every re-resolve, self-mutation and
forwarded inner-list change, and nothing is ever rebuilt at `EndInit` because nothing needs to be.
Bindings register in `manager.Bindings`; position clamps in the manager (BND-21 came along); events
run upstream's order (BND-20); suspend is real. Also closed here: `BND-28` (`UpdateBinding` re-homes
membership, subscriptions and value), `BND-31` (`PropertyManager.Position` is 0).
*Closed:* `BND-01` (P0), `BND-02` (P0), `BND-10`, `BND-14`, `BND-16`, `BND-19`, `BND-20`, `BND-21`,
`BND-28`, `BND-31`.

**W4.2 — `TypeDescriptor`, not reflection. — DONE** Source members and target properties resolve
through `TypeDescriptor.GetProperties(...).Find(name, ignoreCase: true)` — a `PropertyDescriptor`
answers for POCOs too, so there is no fallback path to keep in step. `DataRowView` columns (the whole
typed-DataSet form) bind and write back. *Closed:* `BND-03` (P0), `BND-30`.

**W4.3 — The validation/edit half. — DONE** OnValidation bindings write inside `Validating` and
cancel it when the write fails; `EndCurrentEdit`/`CancelCurrentEdit` pull/push every registered
binding and drive `IEditableObject`/`ICancelAddNew` (which `BindingSource` now forwards to its inner
list, as upstream — without that the manager's `CancelNew` could never reach the `BindingList`
underneath); `BeginEdit` opens on the item that becomes current, which is what lets `CancelEdit`
revert a `DataRowView`. *Closed:* `BND-07`, `BND-08`, `BND-09`.

**W4.4 — Report conversion failure instead of writing `default(T)`. — DONE** `TryCoerce` carries the
failure as a return value; a failed write leaves the source alone, resets the control to the source's
value (upstream's recovery), and reports through `BindingComplete` on the binding, its manager, and
the `BindingSource` when `FormattingEnabled`. The empty-string rules landed with it (BND-24's write
half): `""` into a string member is `""`, `DataSourceNullValue` (now defaulting to `DBNull`) stands in
only under `FormattingEnabled`. The named test was inverted — its NAME said "leaves the source alone"
while its assertion said `Assert.Equal (0, ...)`. *Closed:* `BND-13`, `BND-18`, `BND-23`, most of `BND-24`.

**W4.5 — `ResolveList`'s catch-all. — DONE** `Type` → a typed `BindingList<T>` with a real schema,
scalar → wrapped in a one-item typed list, `DataMember` over a non-DataSet source → the member of the
parent's CURRENT item, re-resolved on the parent's `CurrentChanged` — and validated against the item
type, so a member that exists on nothing throws as upstream does instead of silently binding the wrong
list. `GetRelatedCurrencyManager(member)` returns a cached child `BindingSource`'s manager. The named
test was inverted, plus `Ctor_Object_String_RoundTripsDataSourceAndMember`, which asserted a bogus
member was ignored. *Closed:* `BND-04`, `BND-05`, `BND-06`.

**W4.6 — `BindingNavigator`. — DONE** Item setters hook `Click` to the move they are named for (so
designer code that assigns its own buttons gets working navigation), the `BindingSource` setter
subscribes what keeps the display current, `RefreshItemsCore` renders position/count/enabled-state,
and `EndInit` refreshes instead of destroying — `AddStandardItems` no longer clears, and builds only
into an empty strip. *Closed:* `BND-11`, `BND-12`.

---

### Phase 5 — Per-control behaviour

Independent of each other; parallelise freely. Each closes a block of P0/P1 findings in one control
family. Ordered by traffic in a typical LOB app.

**W5.1 — `DataGridView` editing lifecycle. — DONE (2026-09-11).** `DGV-01` (P0), `DGV-06`, `DGV-07`,
`DGV-08`, `DGV-10`, `DGV-11`, and `DGV-09` with them — that one is a single missing raise in
`CancelEdit`, in the same method family, and leaving a one-line P2 behind for a later branch to touch
the same code again is worse than carrying it. 40 tests, 18 neutralizations each producing a failure.
`BeginEdit (bool)` — which was `{ return true; }`, the only public WinForms way to start an edit from
code — edits the current cell and *reports* whether it did, so a refusal is visible. The column, the
row and the cell each get a `ReadOnly` veto through `IsCellEditable`, which asks
`Cell.InheritedState` rather than re-checking four properties. `EditMode` decides what opens an
editor: F2, a keystroke, a click on the already-current cell, or becoming current. `IsCurrentCellDirty`
is a tracked flag that starts **false** and moves through one `SetCurrentCellDirty`, so
`CurrentCellDirtyStateChanged` fires on the transition and the commit-a-checkbox-immediately idiom
works. `EditingControl` is the live editor. The commit converts to the resolved value type — column,
then cell, then bound member — and a failure raises `DataError` and *stays in edit mode* with the bad
text on screen.
*One structural change worth naming:* every current-cell move now goes through a single
`MoveCurrentCell`, which runs validate → leave → assign → enter → changed once per move. Before,
`CurrentCellChanged` was raised only from the `CurrentCell` setter, so a click never raised it; and
`CellValidating` ran only inside `EndEdit`, so a cell that was never edited was never validated.
Putting it in one place is what stops a two-index move (a cell-mode click sets both) announcing twice.
*A trap found here:* `DataGridViewCell.ParseFormattedValue` converts to the **cell's** `ValueType`,
which is normally unset when the type was declared on the column — so it hands the string straight
back. The cell hook is still asked first, so a derived cell type overriding it is honoured, but its
result is *checked* against the resolved target rather than trusted, and the commit falls through to a
`TypeConverter` on that target. Trusting it was the first version, and it made a typed commit silently
store a string while every test still passed.

**W5.2 — `DataGridView` cell/row/column objects become participants.**
Split into two, because the selection model is a different size of job from the two value/visibility
P0s and there is no ordering dependency between them:

- **W5.2a — the value and visibility choke points. — DONE (2026-09-04).** `DGV-02` (P0) and `DGV-20`
  (P0). `Cell.Value` writes through to the bound object and raises `CellValueChanged`; a hidden row is
  excluded from layout, painting, hit-testing, the scroll extent and `DisplayedRowCount`. 14 tests, 9
  verified to fail with their fix neutralized and 5 labelled in-test as guards.
  *The push-to-bound-object logic existed only inline inside `EndEdit`*, which is precisely why a
  programmatic assignment could not do it; it is now one `TryPushValueToBoundItem` shared by both
  paths, with `EndEdit` suppressing the setter's notification because it parses through `CellParsing`
  first and needs the push's result to drive its own commit/validate sequence. So the push is shared
  and the event fires once, from whichever path the caller used.
  `Row.Visible` is honoured through a single `RowDeviceHeight` — "a hidden row has no height" — rather
  than an `if (!row.Visible) continue` at each of five loops. That is deliberate: `DGV-20` records that
  `Column.Visible` came to be honoured in some places and not others exactly because it was done
  site-by-site.
  *Two deliberate omissions:* WinForms throws when hiding the **current** row; ours neither hid it nor
  threw before, and adding the throw is a behavioural decision separate from making the property work.
  And `Column.DisplayIndex` is untouched — it is named in this item's original text but by no finding
  in its `Closes` list.
- **W5.2b — the selection model. — DONE (2026-09-09).** `DGV-14`, and `DGV-15` with it: that finding
  records that it *cannot* be fixed until "current" and "selected" are separated, which is this item's
  central change. 27 tests, 15 neutralizations each producing a failure, 2 tests labelled in-test as
  guards that did not discriminate (see below).
  `Row.Selected`/`Cell.Selected`/`Column.Selected` are choke points into the grid, so selecting
  repaints and raises `SelectionChanged`; `MultiSelect = false` means one selected element at a time
  whether the selection came from a click or from code; `OnMouseDown` honours Ctrl (toggle) and Shift
  (range for rows and columns, rectangular block for cells); `SelectedRows`/`SelectedCells`/
  `SelectedColumns` come back most-recent-first and are correct per `SelectionMode`; and the renderer
  paints `row.Selected`/`cell.Selected` rather than `SelectedRowIndex`.
  *Ordering is a monotonic stamp on the element, not a list of references on the grid.* The finding's
  fix text suggests `List<int>`, but row indices go stale on every sort and every rebind, and reference
  lists then need pruning against a collection that has already been replaced. A stamp makes recency
  fall out of a sort and keeps "am I selected" on the element, which is also what lets a detached row or
  cell still round-trip the property.
  *Three deliberate deviations.* `Column.Clone` does not carry `Selected` (WinForms clones band
  properties, not selection state), the row-header triangle still marks the **current** row rather than
  the selected ones, and `SelectedColumns` returns a *projection* collection — a normal
  `DataGridViewColumnCollection` re-owns each column and raises `ColumnAdded` on insert, so a populated
  one built the obvious way would fire the grid's column events on every read.
  *`ClearSelection` no longer blanks the current cell* (`DGV-15`), so `grid.ClearSelection ()` followed
  by `grid.CurrentRow.Cells[...]` works instead of throwing. The existing
  `DataGridViewTests.ClearSelection_ResetsCurrentRowAndCell` asserted the divergence and was flipped.
  *Two findings of my own, both recorded rather than papered over.* The first version had a
  `suppress_selection_notification` flag for composite operations; nothing ever took the path it
  guarded, because every batch body writes through `SetSelectedCore` rather than through the properties,
  so it was removed rather than left as unexercised code. And the two "announces once" tests did **not**
  fail when the batch bodies were rewritten to assign the properties element by element, which is the
  implementation they were meant to rule out — so they are labelled guards, not proof, and the design
  comment says so instead of claiming an event-count difference the tests do not demonstrate.

**W5.3 — `DataGridView` incremental data binding. — DONE (2026-09-13).** `DGV-31` (P0), `DGV-03`
(P0), `DGV-32`, `DGV-33`. 26 tests, 18 neutralizations each producing a failure; 1 existing test
re-pointed (`RemainingParityTests.DataGrid_SetDataBinding_sets_both_halves` bound `List<string>` with
`DataMember = "Length"`, which resolves to an `int` — it passed only because the member was never
followed).
`OnBoundListChanged` switches on `ListChangedType`: `ItemAdded` inserts one row, `ItemDeleted` removes
one and moves the current cell if it was on it, `ItemChanged` refreshes one row's cells **without**
raising `CellValueChanged` (the source is telling the grid, not the reverse — announcing it would make a
write-back handler write it straight back), `ItemMoved` moves the row *object*, and only `Reset` and the
`PropertyDescriptor*` types rebuild. A `Reset` regenerates columns **only when the schema differs** from
the columns already there, so `ResetBindings`, a re-sort and a filter keep the app's header renames,
widths and hidden columns — which every change used to silently put back.
*The schema is memoised.* The descriptors (or CLR properties) the columns were generated from are kept on
the grid, so `ItemAdded` builds its one row the same way the full bind built them all. Re-deriving the
schema per change is where a rebuild sneaks back in.
*`DGV-03` was two defects, not one.* `Rows.Add ()` returned `Count` rather than the new index — the
finding's own observation — but fixing that alone still left the canonical
`Rows[Rows.Add ()].Cells[0].Value = …` throwing, now on `Cells[0]`: an empty row had **no cells**.
Upstream's row-template clone has one per column, and so does this now.
*`DGV-32`:* `DataSource`'s getter returns what was assigned, so `((DataTable)grid.DataSource)` works;
`DataMember` re-resolves (a `DataSet` + table name is taken before `ListBindingHelper`, which follows
CLR properties and a table name is not one); an `IEnumerable<T>` query is materialised; anything else
throws `ArgumentException` where it used to keep the *previous* list on screen and say nothing.
*`DGV-33`:* `ReplaceAll` raises one `RowsRemoved` and one `RowsAdded` for the batch so the two balance;
`Insert (int, row)` goes through `InsertItem`; every public `Add`/`Insert` throws
`InvalidOperationException` while bound, and the grid's own path uses `InsertBound`/`RemoveBound`/
`MoveBound`, which do not.
*Not done here:* `DGV-04` (the `Add (params object[])` overloads returning the row rather than an `int`)
is a public-signature change and its own finding.

**W5.4 — `DataGridView` styles, sizing and sorting. — DONE (2026-09-14, `DGV-13` on its own branch).** `DGV-16`,
`DGV-17`, `DGV-18`, `DGV-19`, `DGV-21`, `DGV-22`, and #94 folded in. 27 tests, 23 neutralizations each
producing a failure.
*The private twins are gone, not synchronised.* `Column.SortOrder` **is** `HeaderCell.SortGlyphDirection`
and `Column.DefaultCellStyleAlignment` **is** `DefaultCellStyle.Alignment` (the two alignment enums share
their values, so the alias is a cast). Two fields for one fact had drifted: the header click set one and
the renderer drew it while the public one was stored and never read. RC-6 says delete the twin, and that
is what made both findings a one-line change each.
*The renderer paints the cascade.* `RenderRowCell` folds `cell.InheritedStyle` — grid, column, rows,
alternating, row, cell — into the `ControlStyle` that `RenderCell` already takes, so renderer subclasses
keep their signature and gain alignment, wrap, padding, and the selection colours. `GridColor` colours
the plain grid line; `BackgroundColor` fills below the last row **only when set** — upstream defaults it
to `AppWorkspace`, and this deliberately does not, so an unthemed grid looks as it did.
*Sorting is recorded, gated and delegated.* `SortByColumn` records `SortedColumn`/`SortOrder` **before**
moving rows, so a `ColumnHeaderMouseClick` handler reads the new order — that event is now raised *after*
the sort, as upstream does, which the first version got wrong and a test caught. `SortMode` gates the
header click. A bound list that `SupportsSorting` is asked to `ApplySort` itself, so a `DataView`'s order
survives the next `ListChanged` instead of snapping back. `SortCompare` is real.
*Fill is a layout pass.* `ApplyFillColumnWidths` runs from `UpdateScrollBars`, distributing the content
width by `FillWeight`; the mode and weight setters trigger a layout, which the first version forgot —
the property changed and nothing moved, the original defect in a new coat. `MinimumWidth` is enforced in
exactly one place (`SetWidthFromLayout`); a second clamp in the distribution was removed because no test
could tell the two apart.
*Every row comes from `RowTemplate`* — `Rows.Add`, the value overloads, `Insert`, and both bound paths —
and `RowHeight` is `RowTemplate.Height` under this library's older name.
**`DGV-13` — the default-value flip. — DONE (2026-09-14),** on its own branch as planned:
`RowHeadersVisible = true`, header 30→23, rows 25→22, `RowHeaderSelect`, `MinimumWidth` 30→5,
`DefaultSize` 450×300→240×150. It changed the geometry every DataGridView pixel test measures and the
click semantics of the selection tests; nine existing tests that codified the old values were updated
to the new ones (`Ctor_Default` in three test classes, the three `*_ClampsToMinimum` theories, and the
`Width_SetLessThanMinimumWidth_ClampsToMinimumWidth`/`Height_Set_GetReturnsExpected` theories), plus a
new assertion for `DefaultSize` (untested before). *Flipping `SelectionMode` off `FullRowSelect`
exposed a second, independent bug in the same blast radius:* `GetClipboardContent ()`'s row-selection
check compared `SelectionMode == DataGridViewSelectionMode.FullRowSelect` literally instead of asking
`SelectionIsRowBased` — the predicate the rest of the selection code already uses for "a whole row is
selected regardless of which row-based mode" — so `row.Selected` under the new `RowHeaderSelect`
default (Ctrl+C, `GetClipboardContent_ReturnsSelectedCells_AsTextCsvAndHtml`) copied nothing. Fixed by
routing that check through `SelectionIsRowBased` like the rest of `DataGridView.Selection.cs` does.

**W5.5 — `DataGridView` mouse and keyboard. — DONE except `DGV-26` (2026-09-14).** `DGV-29`, `DGV-30`,
`DGV-25`. 26 tests, 23 neutralizations each producing a failure.
*The mouse pipeline.* Eight cell-level events were `add { } remove { }` and three protected raisers were
empty bodies nothing called. All are backed and raised from one place that answers "what is under this
point" — a `MouseTarget` carrying the row, the column (**-1** for a header, as upstream) and
cell-relative coordinates. `CellClick`, `CellMouseClick` and `CellContentClick` moved from mouse-**down**
to mouse-**up**, where upstream raises them.
*A click is a press and a release on the same cell.* The release records nothing if the press landed
elsewhere: a drag across rows is not a click on either, and treating it as one would toggle a check box
the user dragged over.
*The keyboard moved to `OnKeyDown`.* A key-up handler cannot auto-repeat -- holding an arrow moved one
row and stopped. Enter commits an edit or moves down; Delete removes the selected rows through
`UserDeletingRow` (cancellable, per row) and `UserDeletedRow`, honouring `AllowUserToDeleteRows`;
Ctrl+C/Ctrl+Insert reach the `GetClipboardContent ()` that was already implemented and that no key had
ever called; Home/End move along the **row** and Ctrl+Home/End to the first/last cell -- the unmodified
keys used to do what the modified ones mean; Left/Right work in every `SelectionMode`, not only the cell
modes; and `StandardTab` leaves Tab to the form.
*A bound Delete removes the ITEM, not the row* -- removing the row is undone by the next `ListChanged`
(W5.3). The same lesson as W5.3's own: on a bound grid the list owns the rows.
*The check box commits what it shows.* It assigned a `bool` straight into the cell, so a bound object
never changed and the next rebind reverted the tick. It now goes through the notifying setter (W5.2a's
write-back), marks the cell dirty around the commit so a `CurrentCellDirtyStateChanged` handler sees it
(W5.2a/DGV-08), asks `IsCellEditable` so the grid's and column's `ReadOnly` get a veto and not only the
cell's (DGV-07), and stores the column's `TrueValue`/`FalseValue` -- a `"Y"`/`"N"` flag column never
rendered checked, because both the toggle and the renderer tested for `"True"`/`"1"`.
**`DGV-26` — the combo-box column. — DONE (2026-09-14),** on its own branch as planned. 12 tests, 10
neutralizations each producing a failure.
A lookup column now displays the item whose `ValueMember` matches the cell's value, rendered through its
`DisplayMember` -- the `CustomerId` cell that should read as the customer's name. The lookup sits on the
cell's `FormattedValue` and in `ApplyCellFormatting`, so painting, `CellPainting.FormattedValue` and
`PreferredSize` all agree; a `CellFormatting` handler still pre-empts it. `Items` works the same way as
`DataSource`, for a statically populated column.
Editing a combo column hosts a `DataGridViewComboBoxEditingControl` -- the type that existed and was
never constructed -- bound to the same list, opened on the cell's current value, committing its
`SelectedValue`. The editor field became a `Control` rather than a `TextBox` for this.
*An unmatched value falls back to the value itself,* where upstream raises `DataError`. A blank cell
where a name belongs is the harder bug to diagnose than a visible id, and the `DataError` path for
formatting is not implemented. Recorded as a deviation with a test asserting the fallback, so it is a
decision rather than an accident.

**W5.6 — `ListView` is not a list view. — DONE (2026-09-01)**
`View` now selects the layout and the rendering: `Details` draws a header band from `Columns` and one
row per item with a cell per column (honouring `Width` including the `-1`/`-2` autosize sentinels,
`TextAlign`, `GridLines`, `FullRowSelect` and `CheckBoxes`), `List`/`SmallIcon` draw single-line rows,
`LargeIcon`/`Tile` keep the tiles. `SubItem.Bounds` is real. An implicit `VerticalScrollBar` (the
`ListBox` pattern) backs `EnsureVisible`, `TopItem`, `CountPerPage` and the wheel. `ListViewItem.Selected`
and `.Checked` announce through the parent, so programmatic selection updates dependent UI and
`MultiSelect = false` means something; Ctrl/Shift extend a selection. The seven discarding events are
real with `On*` raisers. `Sort` sorts. 16 tests, 15 verified to fail without their fix.
*Closed:* `LST-01` (P0), `LST-12`, `LST-17`, `LST-18`, `LST-19`, and the `ListView` half of `LST-20`
(the mouse is converted to device units at the hit-test boundary, as `ListBox` does).
*Not covered:* label editing (`BeforeLabelEdit`/`AfterLabelEdit` are raisable but nothing edits in
place), `ItemDrag` (raisable, no drag recogniser), `VirtualMode`, groups, and owner-draw
(`DrawItem`/`DrawSubItem`).

**W5.7 — `CheckedListBox` has no checkboxes. — DONE (2026-09-02)**
A `CheckedListBoxRenderer` (deriving from `ListBoxRenderer`, so the row background, selection, hover
and focus rectangle stay in one place) draws the glyph through the same `ControlPaint.DrawCheckBox` a
`CheckBox` uses. Toggling follows upstream: a click in the glyph, any click when `CheckOnClick`, the
second click of an already-selected row, or Space — all routed through `SetItemCheckState`, so the
cancellable `ItemCheck` applies to user input too. `SelectedItem` unwraps the internal item wrapper.
*Closed:* `LST-02` (P0), `LST-16`.

**W5.8 — Selection events on list controls. — DONE (2026-09-02)**
One `ChangeSelection` choke point on `ListBox`: it snapshots the selected set, applies the mutation,
and announces once if the set changed. `SetSelected`, `ClearSelected`, the `SelectionMode` setter and
the whole mouse and keyboard handlers go through it — the handlers wrapped wholesale rather than
branch by branch, with a batch depth so a branch that also assigns `SelectedIndex` reports once, not
twice. `SelectedItem` assigns the public (raising) `SelectedIndex`; `SetSelected` throws for an
out-of-range index and for a `None` list, as upstream. On `ComboBox`, `SelectedIndex = -1` now
announces, and a selection change writes `base.Text`, so `TextChanged` fires for a combo at all.
*Closed:* `LST-03` (P0), `LST-04` (P0), `LST-06`, `LST-09`. One test inverted
(`SetSelected_OutOfRange_Ignored`, which pinned the swallow).

**W5.9 — `TreeView`. — DONE (2026-09-02)**
`SelectedNode` filters the hidden synthetic root and a node whose `TreeView` is no longer this one, so
it is null on a fresh tree, after `SelectedNode = null`, and after the selected node is removed or the
collection cleared (`TreeViewItemCollection.ForgetIfSelected` from both `ClearItems` and `RemoveItem`).
`GetNodeAt` delegates to the control's own `GetItemAtLocation` — the fake reverse-order traversal and
the rectangles synthesised from the stored `ItemHeight` are gone, and the delegation inherits that
method's device-pixel conversion, which closes the `TreeView` half of `LST-20` as well. One
`SelectItem (node, TreeViewAction)` choke point runs `BeforeSelect` → assign → `AfterSelect`, and the
nine keyboard sites pass `ByKeyboard` where every path used to report `ByMouse`. `Checked` routes
through `SetChecked (value, action)` so `BeforeCheck` can cancel and `AfterCheck` reports the action;
Space toggles it; the renderer draws the box through `ControlPaint.DrawCheckBox` and the layout
reserves `ScaledCheckWidth` for it. `Collapse ()` raises `BeforeCollapse`/`AfterCollapse` and
`Expand ()` raises `AfterExpand`, so programmatic expansion is announced like a clicked one. The
renderer resolves a node's image from `Image`, then `ImageKey`/`SelectedImageKey`, then
`ImageIndex`/`SelectedImageIndex` against the `ImageList`, and honours per-node `ForeColor`,
`BackColor` and `NodeFont`. `Sorted = true` sorts on assignment, `TreeViewNodeSorter` sorts on
assignment, and `Sort ()` is a real recursive sort. `ItemHeight` reaches layout through
`TreeNode.GetPreferredSize` (layout asks each node, not the tree), `Indent` drives the renderer's
indent step, and `ShowPlusMinus` aliases the existing `ShowDropdownGlyph`.
*Closed:* `LST-05` (P0), `LST-11`, `LST-21`, `LST-22`, `LST-23`, `LST-24`, `LST-25`, and the
`TreeView` half of `LST-20`. `LST-26` is closed except `ShowLines`/`ShowRootLines`/`LineColor`, which
need connector-line drawing the renderer has never had — separable, and nothing else waits on it.
18 tests, each verified to fail without its fix.

**W5.10 — `ComboBox` editable region. — DONE (2026-09-03)**
The region is a real child `TextBox`, added as an implicit control, so the caret, selection, undo,
clipboard and mouse text-selection are the ones `TextBox` already implements rather than a second,
thinner copy. It is the combo that stays the tab stop (implicit children are skipped by tab order),
and it is built for every style and merely hidden for `DropDownList` — one instance means `MaxLength`
and the selection survive a style switch. `SelectionStart`, `SelectionLength`, `SelectedText`,
`MaxLength`, `Select` and `SelectAll` all forward to it; they were stored ints that only read each
other. Typing raises `TextUpdate` then `TextChanged`, in that order because upstream's
`CBN_EDITUPDATE` precedes `CBN_EDITCHANGE`. Enter commits the typed text through the `Text` setter, so
a typed `"item3"` selects item 3. `Text` itself follows upstream (`LST-08`): the getter answers
`Control.Text`, every selection path writes it through one `SetTextCore`, a null assignment clears the
selection, and a value matching no item keeps the text without touching the index.
`AutoCompleteMode.Append` (and the append half of `SuggestAppend`) completes inline against
`AutoCompleteSource.ListItems` or `CustomSource` and selects the remainder, so the next keystroke
replaces it.
*Closed:* `LST-08`, and `LST-07` except two pieces. **`Suggest`'s filtered drop-down is absent by
construction**: this control's items *are* the popup `ListBox`'s items, so narrowing what the popup
shows would mean deleting the combo's own items and putting them back — it needs a separate
presentation list, which is its own change. **`Simple`'s always-visible inline list** is not laid out
either (a `Simple` combo is editable, but its list still drops down); that means re-parenting the
popup list into the control and is likewise separable. The OS-backed `AutoCompleteSource` values
(`FileSystem`, `HistoryList`, …) complete nothing and have no portable meaning here.
19 tests, 16 verified to fail with their fix neutralized and 3 labelled in-test as guards.
Also fixed, because forwarding surfaced it: `TextBoxDocument.MaxLength` stored "no limit" **as**
`int.MaxValue`, so an explicit `MaxLength = int.MaxValue` read back as 0 — no limit at all.

**W5.11 — `TextBox` stored-only behaviour. — DONE (2026-09-03)**
Both crash paths first. `MaxLength` limits input and does not truncate text already present, so the
insert clamp computes the *room left* and refuses input when there is none — it used to compute a
negative substring length and throw out of the keystroke, which is what `Text = <database value>`
followed by `MaxLength = 10` (the designer's own order) did to an application. And the caret is now
clamped to the real text in `SetCursorToCharIndex`, with `MoveCursor` returning early on empty text:
the laid-out block is the *placeholder* while the text is empty, so End in an empty search box put the
caret inside `"Search"` and the next character threw.
`CharacterCasing` converts typed, pasted and programmatically assigned text in the document, under the
thread culture as upstream's `ES_UPPERCASE` does. `ShortcutsEnabled = false` refuses Ctrl+C/X/V/A and
leaves the key for the form. `WordWrap` reaches the layout: a multiline box with wrapping off lays out
unbounded and scrolls sideways, and `ScrollControl.HorizontalScrollBar` (new, symmetric with the
vertical one that was always reachable) carries it. `ScrollBars` now decides whether a bar appears at
all, and `HideSelection` decides whether the selection is *painted* — the selection itself survives
focus loss, because `OnDeselected` no longer destroys it.
*Closed:* `TXT-05`, `TXT-06`, `TXT-07`, `TXT-11`, `TXT-12`, `TXT-22`, `TXT-26`. One deliberate
deviation: with `WordWrap = false`, a **centre- or right-aligned** multiline box still wraps, because
the layout engine needs a real right edge to align against — the same trade-off the single-line path
already documents. Left-aligned, which is what a log or code view uses, does not wrap.
`RichTextBox.ScrollBars` keeps its own `new` shadow and stays stored-only; that belongs to W5.14.
15 tests (13 methods), 12 verified to fail with their fix neutralized and 3 labelled in-test as
guards.

**W5.12 — Stop routing mutations through the `Text` setter. — DONE (2026-09-03)**
`TextBoxDocument.ReplaceRange (start, length, value, ignoreLimits, captureUndo)` is the primitive the
findings' systemic note asked for: one document edit, caret after the inserted text, selection
collapsed, undo captured as a single step, and `TextChanged` raised exactly once through the existing
`Invalidate` contract. `TextBox.AppendText` goes through it with `ignoreLimits: true` — upstream
brackets its `EM_REPLACESEL` with `EM_LIMITTEXT 0`, because an append is not user input, so neither
`ReadOnly` nor `MaxLength` applies — and then calls `ScrollToCaret`, which now brings the *new* text
into view because the caret is at the end. The two `new` shadows on `RichTextBox` (`AppendText` and
`SelectedText`) are deleted, so the same object no longer behaves differently depending on the static
type of the reference it is called through.
*Closed:* `TXT-02` (P0), `TXT-35`. 12 tests, 8 verified to fail with their fix neutralized and 4
labelled in-test as guards. One correction to the finding: it suggests asserting `Modified` is
*unchanged* by an append, but `EM_REPLACESEL` sets the edit control's modify flag — the defect was the
direction, since routing through the `Text` setter forced `Modified` **false** and made a dirty
document look clean. `TextBox.SelectedText` still uses `InsertText` rather than `ReplaceRange`; it was
already document-based and correct, and moving it is `TXT-20`'s scope.

**W5.13 — `MaskedTextBox` mask engine. — DONE (2026-09-02)**
One live `System.ComponentModel.MaskedTextProvider` owns the field: typing goes through
`provider.Replace` (not insert — a mask has fixed positions), Backspace blanks a position back to its
prompt rather than shortening the field, `Text` reports the provider's value under `TextMaskFormat`
and assigning runs through `provider.Set`, and `MaskCompleted`/`MaskFull` answer from the provider. The
document holds `ToDisplayString ()`, so the prompt characters finally appear. `MaskInputRejected` fires
per rejected character; `OnValidating` runs type validation and raises `TypeValidationCompleted`,
propagating its `Cancel`; `MaskChanged` has a raiser; `UseSystemPasswordChar` forwards to the
`TextBox` that implements it instead of shadowing it. An empty mask means no provider and plain
`TextBox` behaviour, as upstream's null-mask path.
*Closed:* `TXT-03` (P0), `TXT-18`, `TXT-19`. Both named tests inverted.
*Needed two new seams on `TextBox`* — `InsertTypedCharacter` and `DeleteAtCaret` — because
`TextBox.OnKeyPress` raises the event and inserts in one method, so a subclass could not filter the
character without also suppressing the event.

**W5.14 — `RichTextBox` document model. — DONE (2026-09-03)**
`Rtf`'s getter renders the current document instead of returning a string only its own setter ever
wrote, which is the P0: `note.Body = rtb.Rtf` stored an empty document for everything the user had
typed, or stale RTF from the last programmatic assignment, overwriting the edits. The reader was
compounding it — `\par` and `\tab` vanished, `\'e9` came through as the literal `'e9`, `\u233?` was
dropped, and only text at group depth 1 survived, so anything a real writer wrapped in `{...}`
disappeared — and then the getter saved that back. Reader and writer are now a matched pair over plain
text, with metadata groups (`fonttbl`, `colortbl`, `info`, `{\*\...}` and the rest) skipped by name
rather than by depth. `LoadFile`/`SaveFile` default to `RichText` as upstream, and honour the
`fileType` they used to ignore. One `FindCore` gives the four string overloads case-insensitive search
by default, `MatchCase`, `WholeWord`, `Reverse` and `NoHighlight`, `end == -1` meaning "to the end",
`ArgumentOutOfRangeException` for a range it cannot search, and — the part that makes the standard
highlight loop work — it selects the hit and scrolls it into view. The `Selection*` family keeps
per-run character formatting: colour, background, bold, italic and underline, applied to the selection
or held as the insertion-point format for what is typed or appended next, read back from the run under
the caret, and painted through the existing `Colorizer` hook.
*Closed:* `TXT-04` (P0), `TXT-14`, `TXT-15`, `TXT-16`, `TXT-17`. 27 tests, 22 verified to fail with
their fix neutralized and 5 labelled in-test as guards; 2 existing tests inverted
(`RichTextBoxTests.Rtf_SetNullOrEmpty_EmptiesText` and the `SelectionColor` half of `Ctor_Default`,
both of which pinned the stubs). Deliberate limits, all documented in the code: character formatting
is **not serialised** into the generated RTF (that needs a colour table and per-run control words this
writer does not emit, so a save keeps the text and the paragraphs and loses the colours); `SelectionFont`
carries the style flags but not a per-run family or size, which the span type cannot express; and runs
follow typing, Backspace/Delete and `AppendText` — the seams this control owns — but not `Undo`,
`Paste` or a programmatic `SelectedText` assignment, which move text without telling it.

**W5.15 — `ToolStrip` item storage and appearance. — DONE (2026-09-03)**
The shadows are gone: `Enabled` and `Tag` on `ToolStripItem`, `Checked` on both `ToolStripMenuItem`
and `ToolStripButton`, and `Alignment` on `ToolStripStatusLabel` each kept a store of their own that
nothing else read. `MenuItem` owns all of them now — the properties the click gate, the hover gate and
all four renderers actually read — and `MenuItem.Enabled`/`Visible` are virtual with hooks that
`ToolStripItem` overrides to raise `EnabledChanged`, `VisibleChanged` and `AvailableChanged`.
`Available` delegates to `Visible` instead of keeping a parallel flag. A hidden item is excluded from
`ToolBar`'s layout and its renderer, as `Menu`, `MenuDropDown` and `StatusStrip` already did, so it
stops being a dead-but-painted button. A checked menu item draws a glyph in the image gutter and a
checked `ToolStripButton` draws with the pressed background. `Size`/`Height`, `DisplayStyle` and the
other item-box setters run `InvalidateItemLayout`, which is what makes an assignment reach the box the
renderer draws — item layout happens in `OnPaint`, so invalidating *is* re-laying out.
*Closed:* `TSM-01` (P0), `TSM-04`, `TSM-06`, `TSM-14`, `TSM-31`, and the part of `TSM-30` whose
triggers this item touches (`EnabledChanged`, `VisibleChanged`, `AvailableChanged`,
`DisplayStyleChanged`). The menu-lifecycle half of `TSM-30` — `MenuActivate`/`MenuDeactivate`,
`ContextMenu.Popup`/`Collapse`, `MenuItem.Popup` — is left for **W5.16**, which is where the menu
facade and lifecycle work lives. 14 tests, 11 verified to fail with their fix neutralized and 3
labelled in-test as guards.

**W5.16 — Strip facade and coordinates. — DONE (2026-09-03)**
`ContextMenu.Show (Control, Point)` converts with `parent.PointToScreen (location)`, so the point is
client-relative as `ToolStripDropDown.Show` is upstream: the canonical
`contextMenuStrip1.Show (button1, new Point (0, button1.Height))` opens under the button instead of at
the top-left of the screen, and `Show (grid, e.Location)` from a mouse handler lands under the pointer
(`TSM-03`, P0). `Show (Point)` stays screen-space, and the three internal callers that were
pre-converting with `PointToScreen` — the compensation that showed the API was wrong — now pass the
point through.
The strip notifications moved out of `ToolStrip`'s items facade and onto `MenuItemCollection`, the one
insertion path every strip type shares: `ItemAdded`, `ItemRemoved`, `ItemClicked` and the renderer's
item hook now fire on a `MenuStrip` and a `ContextMenuStrip`, which re-expose the underlying collection
and never went through the facade at all. `ItemClicked` is relayed from the item's own `Click` through a
method group rather than a lambda, so an item removed and re-added reports once rather than twice.
The menu-lifecycle events deferred from W5.15 are raised: `MenuStrip.MenuActivate`/`MenuDeactivate`
from `MenuBase`'s activation (new `OnActivated`/`OnDeactivated` hooks), `ContextMenu.Popup` before
`Opening` — the legacy hook for enabling items just before display — and `Collapse` once per dismissal,
tracked by whether the menu was actually on screen.
*Closed:* `TSM-03` (P0), `TSM-08`, and the menu-lifecycle remainder of `TSM-30`. 12 tests, 11 verified
to fail with their fix neutralized and 1 labelled in-test as a guard.

**W5.17 — Text measurement is wrong at the root (drawing). — DONE**
`TextRenderer.MeasureText(string, Font)` measures at the font's **point size treated as pixels**, so
every measurement is off by the point→pixel ratio; it also word-wraps by default where upstream does
not, and adds none of the GDI padding upstream adds. `Graphics.DrawString` into a `RectangleF` never
word-wraps at all. Layout code all over the framework and in migrated apps depends on these two.
*Closed:* `GFX-25` (P0), `GFX-06` (P0), `GFX-26`, `GFX-27`, and the `WordBreak`/ellipsis half of
`GFX-28`. `GFX-14` (`MeasureString`'s `charactersFitted`/`linesFilled` out-params) and
`PathEllipsis`'s middle-truncation are not done — both are separable and neither blocks anything.
6 tests, each verified to fail without its fix.

**It changed nothing else, and that is the finding.** The expectation was that moving every measured
string by a third would ripple through the suite. Not one existing test failed — because nothing tied
measurement to drawing, which is exactly how a 25% error survived. The new tests assert the
*relationship* (measure vs. draw, and measure vs. real ink) rather than either number, so neither half
can drift alone again.

**Corrections to the finding as written:** `MeasureText`'s wrap condition was inverted *and* the
padding was missing, and both pushed the measurement the same way, so fixing only one would have
looked like a partial improvement while leaving layout wrong. `DrawText` also had to learn `WordBreak`
in the same pass: fixing `MeasureText` alone would have made the pair disagree in a new way, since
measurement started wrapping where drawing still did not.

**The same unit bug was in three places, and only the third one was visible.** After the measuring
fix the user reported that on-screen text still looked tiny, which was correct: `GFX-25` is written up
as a *measurement* defect, but points-as-pixels had also been copied into the two paths that decide
what actually gets drawn.

1. `Control.Font`'s setter assigned `Style.FontSize = (int) value.SizeInPoints`, but `Style.FontSize`
   is in pixels — `Theme.FontSize` is 14, a pixel size. Same in `ControlStyle`, `DataGridViewRenderer`
   and `ControlAndFormParity` (which feeds `CurrentAutoScaleDimensions`, so it scaled every
   designer-built form by a ratio derived from a number a quarter too small).
2. `Control.GetEffectiveFontSize`'s fallback was `(int) SystemFonts.DefaultFontSize` — 8.25 **points**
   truncated to **8**, handed to the renderers as a **pixel** size. This is the one users see, because
   it is the path taken by every control that does not set a `Font`, which is nearly all of them:
   unfonted text drew at 8px where the correct default is 11px. Ironically this fallback was itself a
   fix for the opposite bug (unfonted controls picking up the 14px theme font); it corrected the
   source of the number without correcting its unit, and overshot from too big to too small.

That second one had a test asserting the wrong behaviour outright
(`GetEffectiveFontSize_matches_SystemFonts_DefaultFontSize_when_unfonted`), which is why the fleet of
existing tests stayed green while the application looked wrong. The test's intent — fall back to the
ambient system font, not the theme font — was right; only its unit was wrong, and it now asserts the
default font's pixel size and explicitly asserts *inequality* with the point size.

**Lesson for the remaining work:** a unit defect is never in one place. `GFX-25` was filed against
`MeasureText` and the audit did not connect it to the render path or to the ambient fallback, so the
first fix was verifiable, green, and invisible to the user. The regression test that finally pinned
it asserts that an unfonted control inks the *same height* as an explicitly-fonted one — a
relationship between two paths rather than a number in one — which is the only shape of assertion
that would have caught all three instances at once.

**W5.18 — Pens lose everything but colour and width. — DONE (2026-09-04)**
Every simple stroke call discarded the `Pen`'s dash style, caps, join and brush — so dashed focus
rectangles, grid lines and custom borders all drew solid. Plus: `SmoothingMode.Default` antialiased
where GDI+ does not; `IntersectClip(Region)` replaced instead of intersecting; clips were reduced to
their bounding rectangle; `SetClip(GraphicsPath)` flattened to control points; `DrawImage` with
`ImageAttributes` + callback dropped the attributes.

**Landed.** All strokes go through one paint builder: `RentStrokePaint` keeps the measured pooled
`SKPaint` for a plain pen and falls through to `Pen.CreatePaint ()` — the builder `DrawPath` alone was
using — for any pen carrying a dash, a join, a cap, a custom cap or a stroking brush, so dash, caps,
join, miter limit and brush now apply to all fifteen stroke call sites (`GFX-23`, P0). `MiterLimit` is
copied onto the pooled paint too, since Skia defaults `StrokeMiter` to 4 where `Pen` defaults to 10.
`DrawLines`/`DrawBeziers` build one path instead of looping over per-segment draw calls, because a
`LineJoin` and a dash phase are properties of the polyline, not of a segment. One `Antialias` property
on `Graphics` (`AntiAlias`/`HighQuality` only, as GDI+ maps them) drives all four paint builders, so
fills stop softening their edges by default and strokes start honouring `SmoothingMode` at all
(`GFX-07`). Clipping keeps its shape: `SetClip`/`IntersectClip`/`ExcludeClip` route regions through
`SKCanvas.ClipRegion` and paths through the real `ToSKPath ()` outline (`GFX-13`, `GFX-24`);
`IntersectClip(Region)` can only narrow (`GFX-12`); a tracked clip region makes `Graphics.Clip`
round-trip a non-rectangular clip and gives `CombineMode.Union`/`Xor`/`Complement` real meaning on
every `SetClip` overload, `TranslateClip` included. The four callback-carrying `DrawImage` overloads
forward their `ImageAttributes` (`GFX-15`).
*Closed:* `GFX-23` (P0), `GFX-07`, `GFX-12`, `GFX-13`, `GFX-24`, `GFX-15`. `GFX-08` is closed only for
`CompositingMode` (`SourceCopy` → `SKBlendMode.Src`, three lines in the paint builders). 30 tests
(`tests/Majorsilence.Forms.Tests/PenAndClipFidelityTests.cs`), 29 verified to fail with their fix
neutralized and 1 labelled in-test as a guard.

**Deferred, with reasons.** `GFX-09` (`PageUnit`/`PageScale`) is not done: it changes the meaning of
every coordinate handed to the drawing layer, and it cannot be done correctly while `DpiX`/`DpiY` are
hardcoded to 96 (`GFX-10`) — a page-unit conversion needs a real device resolution, and on a printer
surface a wrong one is wrong by 6×–12×. It wants its own item alongside `GFX-10` and the printing
path. Four fifths of `GFX-08` is likewise deferred — `InterpolationMode`, `PixelOffsetMode`,
`CompositingQuality`, `TextRenderingHint`. Two reasons, both structural rather than effort: (i) the
finding's own note is right that making these effective without widening `GraphicsState` to carry them
(`GFX-16`) turns the standard `var s = g.Save (); g.InterpolationMode = …; g.Restore (s);` block into
a *new* leak, so they belong with `GFX-16`; and (ii) `TextRenderingHint` lands on the RichTextKit text
path, not on the paint builders, which is a different surface from everything else in this item.
`CompositingMode` was taken because it is expressible as one blend-mode assignment per paint and its
failure mode (a "transparent" `SourceCopy` stamp leaving the old pixels) is visible.

**No existing test needed inverting, and that is worth recording.** The whole suite (4263 tests) was
green before and after: not one test pinned "a dashed pen draws solid", "a default fill is
antialiased" or "a region clip is its bounding box". The drawing layer's tests cover the members that
exist rather than the fidelity they carry, which is exactly how a `Pen` could lose four properties on
fifteen call sites without a single red test. The one place the old behaviour *was* asserted is
`GraphicsPhase5Tests.DrawPath_honors_the_pen_dash_pattern`, and it asserted the correct behaviour —
`DrawPath` was the one call site that already worked.

**One existing test was changed, and not for a drawing reason.** Adding a 30-test file to the assembly
made `KeyboardChainTests.Escape_still_activates_the_CancelButton` fail in roughly half of full-suite
runs while passing in isolation. It was not this item's change: reverting `Graphics.cs` to its
pre-W5.18 state and keeping the new test file reproduced the failure 3 times in 6 runs, and the
original code with the new file excluded was clean 6 times in 6. The cause is
`Application.ActiveMenu`, which is process-global and which two menu test classes set (one directly,
without clearing it); an active menu owns the keyboard ahead of the whole pre-processing chain, so
Escape closed the leaked menu and never reached `CancelButton`.

*Resolved in production rather than in the test.* This item first cleared the field in
`KeyboardChainTests.ShowForm`, which made the suite stable but left the shipped defect in place: a
menu open on one window ate every *other* window's Escape, which is a real bug for any multi-window
app and not merely a test-isolation problem. **W5.16** fixes it at source — `WindowBase.HandleKeyDown`
routes a key to `Application.ActiveMenu` only when that menu belongs to the window handling the key —
and the per-class clear was removed once that landed, because keeping it would have masked a
regression of the fix. Verified after removal: 10 clean full-suite Debug runs, where 4 of the previous
18 had failed. Worth recording as a pattern all the same: the suite has 52 classes that call
`HeadlessRenderer.Use ()` without `[Collection ("Headless")]`, so this class of order-dependence is
latent elsewhere too, and any new test file can expose it.

**Correction to the finding as written.** `GFX-23` says the pooled paint is "the paint used by all
fifteen direct stroke call sites" and names `DrawRectangles` and `DrawBeziers` among them. Both of
those delegate to their single-shape sibling rather than building a paint; the pooled paint had
**fourteen** call sites (the fourteen line numbers the finding itself lists). Immaterial to the fix —
the delegating overloads inherit it — but the sweep is over fourteen sites, and `DrawBeziers` needed a
change of its own anyway, since delegating per curve restarts the dash at every join.

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

**— DONE (2026-09-14).** `GFX-01`, `GFX-02`, `GFX-03`, `GFX-38`. 38 tests, 16 neutralizations each
producing a failure; 20 methods left the no-op baseline.
*`GFX-02` first, because everything else depends on it.* `ControlPaint.HLSColor` is ported verbatim --
integer maths on a 0-240 range, no Win32 -- and `Light`/`Dark`/`LightLight`/`DarkDark` go through it.
Two defects in one: the parameter is a 0.0-1.0 **fraction**, so the documented `Light (c, 0.5f)` moved
each channel by one and every hand-rolled bevel collapsed to flat; and linear RGB addition desaturates
towards white or black instead of moving luminosity, so `Dark (Color.Red)` was (230,0,0) rather than
(128,0,0). Separate single-argument overloads rather than a default parameter, because the one-argument
*form* has to exist for reflection and delegate binding.
*`GFX-01`: the twenty chrome methods paint,* from Skia primitives with no glyph font and no Win32
`DrawFrameControl` bitmap. Borders (per-side colour, width and style; `Inset`/`Outset` as two-tone
bevels), `DrawBorder3D` (the classic two-ring bevel, honouring `Border3DSide`), the button-faced family
(`DrawButton`, `DrawCheckBox`, `DrawMixedCheckBox`, `DrawRadioButton`, `DrawComboButton`,
`DrawScrollButton`, `DrawCaptionButton`), the menu glyphs, the designer furniture and `DrawSizeGrip`.
`ButtonState` is honoured: `Pushed` swaps the bevel and offsets the glyph, `Inactive` greys it, `Flat`
drops the bevel.
*`GFX-03`:* the focus rectangle takes its colours (it was hardcoded black, so the indicator vanished on
a dark theme -- an accessibility regression), strokes **inside** its rectangle rather than around it,
and takes its dash phase from `(X + Y) % 2` so adjacent rectangles tile.
*`GFX-38`:* `Application.RenderWithVisualStyles` is gated on `VisualStyleRenderer.IsSupported`, so the
two are the same answer as upstream defines them. Reporting `false` is the honest answer while there is
no visual-style engine: it sends the standard themed-or-classic fork to `ControlPaint`, which now paints.
*One deliberate approximation:* `CaptionButton.Help` draws a filled dot rather than a question mark,
which needs a font. Recorded rather than left blank.

**W5.20 — The value controls are not implemented.** *(As measured 2026-08-25. The `MonthCalendar`
half is closed — see **W5.20c** below.)*
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

**Split into slices (2026-09-04).** This item is six P0s across five controls, and the structural note
above orders part of it, so it is being landed in pieces:

- **W5.20a — the scroll and spin arithmetic. — DONE (2026-09-04).** `SMP-31` (P0), `SMP-48`, `SMP-49`,
  plus the wheel's missing `EndScroll`. A `NumericUpDown` arrow click calls `UpButton`/`DownButton`,
  the methods that apply `Increment`, instead of hard-coding ±1. `ScrollBar` gains
  `EffectiveMaximum` (`Maximum - LargeChange + 1`) and clamps every user-driven path and the track
  mapping to it, while the `Value` setter still accepts anything up to `Maximum`, as upstream. The
  wheel accumulates `Delta` and spends one `SmallChange` per whole 120-unit notch — it used to
  multiply by the raw delta, so one notch moved 120 × `SmallChange` — and finishes with `EndScroll`.
  12 tests, 11 verified to fail with their fix neutralized; 1 existing test inverted
  (`ScrollBarTests.Wheel_raises_Scroll_with_the_proposed_value_before_Value_updates`).
  `SMP-47` needed nothing: `PerformScroll` already raised `Scroll` for every path.
- **W5.20b — `NumericUpDown` text entry and the `UpDownBase`/`DomainUpDown` shape. — DONE
  (2026-09-14).** `SMP-32` (P0), `SMP-33`, `SMP-36`, `SMP-37`. 24 tests, 18 neutralizations each
  producing a failure.
  *`SMP-32`:* the control had no `OnKeyDown`, `OnKeyPress`, `OnKeyUp` or `ProcessDialogKey` anywhere --
  a user could not type a number into it. It now carries its own edit text and caret: digits, the hex
  letters, the culture's decimal separator and a leading minus are accepted (and only where they can
  parse); Backspace, Delete, Home, End and the arrows edit and navigate; the arrows step the value when
  `InterceptArrowKeys`; `ReadOnly` blocks typing but not the arrows, as upstream. **Parsing happens on
  leave and on Enter, not per keystroke** -- a half-typed `1.` is not a number, and committing it would
  snap the field under the user. `Select (int, int)` moves the caret; there is no selection *highlight*,
  which is recorded rather than pretended.
  *Typing REPLACES rather than appends,* because arriving at a spin box selects its text upstream. The
  first version appended, and the test that read "7, type 42, expect 742" is what surfaced it.
  *`SMP-33`:* the renderer reads the control's own `Font`, `ForeColor` and `TextAlign` -- setting `Font`
  had no effect at all before, so a spin box did not scale with its form -- and the format comes from
  `Hexadecimal`/`ThousandsSeparator`/`DecimalPlaces`. `UpDownAlign` moves the button strip.
  *`SMP-36`/`SMP-37`:* `NumericUpDown` derives from `UpDownBase` and `DomainUpDown` from `UpDownBase`
  rather than from `NumericUpDown`. `DomainUpDown` gets its own renderer and steps its `Items` with
  `Wrap`, where it was painted by the numeric renderer and displayed the literal text `0` whatever its
  items held.
- **W5.20c — the date-picking UI: `MonthCalendar` and `DateTimePicker`. — DONE.** `MonthCalendar` half
  2026-09-04; `DateTimePicker` (`SMP-39` P0, `SMP-40` P0, `SMP-41`) 2026-09-14. 17 tests, each verified
  to fail with its fix neutralized. `MonthCalendar` detail below.
  *`SMP-39`:* the control derived from `TextBox`, so `Text` was free-form and never parsed back into
  `Value` — `dtp.Text = "2024-01-15"`, a common way to seed a picker from a string, displayed the text
  and left `Value` at today, so the app saved the wrong date. It now derives from `Control`, and `Text`'s
  setter parses: empty resets to today, an unparseable or out-of-range string is refused rather than
  displayed. That also takes `Multiline`/`PasswordChar`/`AcceptsReturn` off the surface and stops
  `if (c is TextBox)` sweeps picking up every date picker on a form.
  *`SMP-40`:* the drop-down arrow was painted and dead — nothing hit-tested it, there was no popup
  anywhere, and `DropDown`/`CloseUp` sat under a `CS0067` suppression. With no keyboard path either, a
  `DateTimePicker` was a read-only display of today. It now opens a `PopupWindow` hosting the
  `MonthCalendar` this item's first half made real, honours `DropDownAlign`, commits the picked date and
  closes, and raises `DropDown`/`CloseUp`. F4 and Alt+Down open it from the keyboard; `Format` and
  `CustomFormat` raise `FormatChanged`.
  *`SMP-41`:* `ShowCheckBox` + `Checked` — the only way WinForms expresses an *optional* date, on
  virtually every "date of X (optional)" field — were stored and read by nothing, so the user could
  neither clear nor set the date and code reading `Checked` always got `true`, writing nulls as today.
  The check box is painted and hit-tested (Space from the keyboard), an unchecked date is greyed, and
  the `Calendar*` colours and font reach the drop-down. `ShowUpDown` replaces the drop-down button with
  a spin strip that steps the date by a day.
- **W5.20d — `ErrorProvider` rendering. — DONE (2026-09-04).** `SMP-51` (P0). `SetError` now attaches
  the errored control's parent to a new adorner paint layer and draws an error glyph beside the
  control, honouring `SetIconAlignment` and `SetIconPadding`. `Clear` and an empty description remove
  it; a hidden control shows none. The glyph is drawn from primitives rather than shipped as an image,
  so it scales with the display. 8 tests, 7 verified to fail with their fix neutralized and 1 labelled
  in-test as a guard.
  **New framework seam:** `Control.PaintAdorners`, raised *after* `PaintChildren`. The public `Paint`
  event fires before the children, so anything drawn from it lands underneath them — upstream solves
  this with a separate `ErrorWindow` per control, and this framework has no child windows to use that
  way. Still not implemented, and each additive: blinking (`BlinkStyle`/`BlinkRate` are honoured as
  state, no timer runs), a custom `Icon`, and the hover tooltip.
  *Still open in this slice:* `SMP-33`, `SMP-41`, `SMP-43`.

**W5.20c detail — the `MonthCalendar` half.**

*A note on the slice letters:* this entry was written against a base where no a/b/c/d subdivision
existed, and proposed its own lettering. The split above is the live one and is used here instead; the
work described is unaffected.

*Closed:* `SMP-42` (P0) in full, and the date half of `SMP-43` and `SMP-46`.

`MonthCalendar` now draws and behaves as a calendar:

- **`MonthCalendarRenderer`**, registered with `RenderManager` (there was no entry for the type at
  all, so `RenderManager` walked up to `Control`, found nothing, and the control's own `OnPaint` drew
  one centred `ToShortDateString()`). Title band with month/year and two scroll arrows, day-of-week
  header, six week rows of day cells, the selected range filled, today outlined, an optional
  week-number column and an optional "Today:" strip.
- **One geometry, three consumers.** `MonthCalendarGrid.cs` owns `Geometry` and the cell/header/
  week-number/date rectangles; the renderer draws them, `HitTest` maps points onto them, and the
  mouse handlers select through `HitTest`. The claim in `MidSizeControlParity.cs`'s header — "HitTest
  and GetDisplayRange are computed from the same geometry the renderer lays the control out with" —
  was aspirational before this and is now structural.
- **Mouse:** press anchors and previews, move extends, release commits. `DateChanged` fires per day
  crossed and `DateSelected` exactly once, on release. `MaxSelectionCount` trims the *dragged* end and
  leaves the anchor alone (routing a drag through `SetSelectionRange` moves the anchor instead, because
  that method adjusts "whichever limit hasn't changed" — right for a programmatic call, wrong for a
  drag). Arrows page the view without touching the selection, honouring `ScrollChange`; the "Today"
  strip and a week number select today and that week.
- **Keyboard:** arrows by a day and a week, `PageUp`/`PageDown` by a month (keeping the day of month
  where the target month has one), `Home`/`End` to the ends of the month, `Shift` extends from the
  anchor. Every navigation key is marked `Handled`; everything else is left to the base.
- **`DateSelected` is field-backed.** It was `add { } remove { }` — every subscription compiled and
  was discarded. Reverting it to that form does not even compile now, because `OnDateSelected` needs
  the field; the neutralization proof below had to disable the raiser instead.
- **A displayed month distinct from the selection.** `display_month` is null ("follow the selection")
  until something scrolls the view, and `SetSelRange` re-pins it whenever the selection moves out of
  sight, so a programmatic `SetDate` scrolls to the month it selected. `GetDisplayRange` reports the
  displayed month rather than `SelectionStart`'s.
- **Six previously stored-only members are now consumed:** `ShowWeekNumbers`, `ShowTodayCircle`,
  `TitleForeColor`, `TitleBackColor`, `TrailingForeColor`, `ScrollChange`. The stored-only baseline
  shrank 746 → 740 and the inert-event baseline 64 → 63.

**Deliberately deferred, and why:**

- **`CalendarDimensions` > 1x1.** One month is painted across the whole client area whatever the
  dimensions say. A single month that works beats a multi-month grid that half does, and the
  multi-month case needs its own hit-test partitioning and its own title per month. `GetDisplayRange`
  still reports every month the dimensions ask for — which is the one place where the "agrees with
  what the user sees" promise is still unmet, and it is unmet in the direction of the existing
  documented behaviour (there is a test pinning the two-month range). Recorded against `SMP-46`.
- **The bolded-date *collections*.** The renderer bolds through `IsBoldedDate`, so the
  `Add*BoldedDate` API does show, but `BoldedDates`/`AnnuallyBoldedDates`/`MonthlyBoldedDates` remain
  the second, disagreeing backing store `SMP-44` describes. Fixing that is a store-merging job, not a
  rendering one, and belongs to `SMP-44`.
- **`HitArea.TitleYear`, `TitleBackground`.** `TitleMonth` is still returned for the whole middle of
  the title band. Splitting it needs the title text measured and hit-tested run by run, and an
  existing test pins `HitTest (100, 1)` on a 200px-wide calendar as `TitleMonth`. `Date`,
  `PrevMonthDate`, `NextMonthDate`, `DayOfWeek`, `WeekNumbers`, `TodayLink` and `CalendarBackground`
  *are* now returned, which is the part the mouse handling needed. `SMP-43` is therefore partially,
  not fully, closed.
- **`SMP-45`** (the selection setters validating against the raw rather than the effective min/max)
  is untouched: it is a property-validation fix with its own thrown-exception contract. The new
  *gesture* paths do clamp to the effective `MinDate`/`MaxDate`, because a click must never land the
  selection somewhere the grid cannot draw it.

**Two findings were wrong or stale as written, and are corrected in `simple.md`:**

1. `SMP-42` says `DateSelected` is at `MonthCalendar.cs:181` and `OnPaint` at `:250-259`. Both were
   right; but the finding's own "Fix" list implies `FirstDayOfWeek` was unconsumed, while it was
   already read by `GetDisplayRange` — `SMP-46` gets that right and `SMP-42` does not.
2. The doc comments on `ShowToday` and `ShowTodayCircle` were **swapped** (`ShowToday` said "whether
   today's date is circled"). Nothing read either property, so nothing contradicted them. Both now
   describe what they do.

**Tests:** `tests/Majorsilence.Forms.Tests/MonthCalendarBehaviourTests.cs`, 44 tests. 42 verified to
fail with their fix neutralized, in nine batches (renderer registration; mouse handlers; keyboard
handler; the `OnDateSelected` raiser; `HitTest`'s date mapping; `HitTest`'s logical→device conversion;
`GetDisplayRange`'s displayed month; each of the two `ScrollInto` call sites; the renderer's reads of
`TrailingForeColor`/`ShowWeekNumbers`/`ShowToday`/the selection fill). Two are labelled in-test as
guards rather than proofs — a header click selecting nothing, and keyboard navigation stopping at
`MaxDate` — because the control had no input handling at all before, so no previous version could
fail them.

**A pre-existing flake, noted so the next slice does not chase it.**
`KeyboardChainTests.A_control_that_claims_a_key_stops_dialog_processing` fails roughly one run in
four, in the full Debug suite only. Confirmed pre-existing by removing this slice's test file
entirely and running the suite five times — it still failed once. Nothing in it touches
`MonthCalendar`; it looks like cross-test global state (`Application.OpenForms` / the active window),
which is what `[assembly: CollectionBehavior (DisableTestParallelization = true)]` in `AssemblyInfo.cs`
already exists to contain.

**Three things cost time and are worth recording.** First, one `[InlineData]` case was vacuous: the
fixture pre-selects the 14th, so "click the 14th and assert it is selected" passed with every mouse
handler deleted. The neutralization pass is what found it. Second, `PaintSurface.RenderOnForm
(control, 1f)` — pinning the scale to 1 — is wrong for a control whose geometry comes from
`ClientRectangle`: under `MF_HEADLESS_SCALE=2` the bitmap stayed 220x162 while the grid laid itself
out over 440x324, so four ink assertions failed on cells drawn outside the bitmap. Letting
`RenderOnForm` resolve the control's own scaling, and asserting the bitmap equals
`ClientRectangle.Size` **and** is non-empty, is what makes the pixel tests both non-vacuous and
scale-independent.

Third, two more assertions were vacuous for a reason only the fixture could reveal: **1 March 2026 is
a Sunday**, so under the Sunday-first default the 1st lands in column 0 — the answer any broken column
calculation also gives — and the padded display range needs no leading days at all. The column test
now uses April (the 1st is a Wednesday) and the padded-range test a Monday-first week. Both then failed
when the grid's leading padding was neutralized; neither did before. A date fixture can be
accidentally degenerate in a way a numeric one cannot, and the neutralization pass is the only thing
that surfaces it.

**W5.21 — Buttons, labels and pictures. — DONE (2026-09-14).**
43 tests in `tests/Majorsilence.Forms.Tests/ButtonsLabelsPicturesTests.cs`, 22 neutralizations each
producing a failure; 8 tests are labelled in-test as guards.
*Closed:* `SMP-01`, `SMP-02`, `SMP-03`, `SMP-05`, `SMP-07`, `SMP-13`, `SMP-14`, `SMP-15`, `SMP-20`,
`SMP-21`, `SMP-23`, `SMP-26`, `SMP-29` — and `SMP-04` and the `CheckedBackColor` half of `SMP-06`,
which fell out of the fixes for `SMP-03` and `SMP-05` rather than being worked separately.

- **The radio group became a group.** `UpdateSiblings` unchecked every sibling regardless of anyone's
  `AutoCheck`, so the manually-managed group could never show what it was told to (`SMP-01`), and
  nothing anywhere wrote `TabStop`, so a six-option group cost six tabs to cross instead of one and
  tabbing in did not land on the checked option (`SMP-02`). Both are one routine upstream --
  `PerformAutoUpdates` with `WipeTabStops` -- ported into `RadioButton.Group.cs` including its
  `first_focus` flag, without which the sibling updates re-enter `WipeTabStops` and clear the tab stop
  the same call has just set.
- **`Appearance` and `FlatStyle` reached the renderer.** `Appearance.Button` now draws as a toggle
  button -- no glyph, no glyph column in the preferred size, the latched state carried by the
  background -- which is the segmented-control idiom (`SMP-03`); its setter raises `AppearanceChanged`
  (`SMP-04`). `Button.ApplyFlatAppearance` moved up to `ButtonBase` so `CheckBox` and `RadioButton`
  share it (`SMP-05`), and `FlatAppearance.CheckedBackColor` is honoured on the way past.
- **Captions wrap.** Upstream ORs `WordBreak` unconditionally for the button family, so the three
  renderers stopped pinning `maxLines: 1` (`SMP-13`) -- and the family's `IHaveTextAndImageAlign
  .Multiline` had to become `true` with it, or the layout engine hands back a rectangle measured for
  one line and the second is drawn outside it. `Label.Multiline` now defaults to `true`, because
  upstream's `Label` has no such property and always wraps (`SMP-14`).
- **`Label.BorderStyle` draws.** Mapped onto the instance style's border, which both paints the frame
  and shrinks the text region -- the layout engine already deflates by `Style.Border` -- and is added
  to the preferred size, as upstream's `GetBordersAndPadding` does (`SMP-15`).
- **`PictureBox.Load` is synchronous and reports failures.** It assigned `ImageLocation`, whose setter
  ran an `async void`: `pb.Load (path); var w = pb.Image.Width;` threw `NullReferenceException`, and a
  `catch (FileNotFoundException)` around `Load` never caught. A missing local file was worse -- 
  `SKBitmap.Decode (path)` returns null rather than throwing, so the box reported no error and painted
  nothing (`SMP-20`). `LoadAsync`/`CancelAsync` are real, and the two async events stopped being
  `add { } remove { }` -- they are now `AsyncCompletedEventHandler`/`ProgressChangedEventHandler`, the
  types upstream uses and the ones migrated handler code compiles against (`SMP-21`). `SizeMode`
  invalidates and keeps `AutoSize` in step (`SMP-23`).
- **The three progress bar styles.** `Blocks` (the WinForms default) draws discrete chunks,
  `Continuous` a solid fill, and `Marquee` a travelling block driven by a timer at
  `MarqueeAnimationSpeed` and independent of `Value` -- the indeterminate bar used to render
  permanently empty, so the app looked hung exactly when it was trying to say it was working
  (`SMP-26`).
- **`TrackBar.Value` stopped raising `Scroll`.** `Scroll` is the "the user moved it" signal; raising it
  from the setter made a linked pair of sliders feed each other (`SMP-29`).

*Not in scope, deliberately:* the `MouseDownBackColor` half of `SMP-06`. It needs a pressed state the
framework does not have -- there are two style layers, `Style` and `StyleHover`, and no mouse-down
tracking on `ButtonBase` -- so honouring it means adding that state, which is its own item rather than
a line in this one. `SMP-06` stays open with only its `CheckedBackColor` half closed.

**W5.22 — `SplitContainer` and `Splitter`. — DONE (2026-09-04)**
All seven findings closed, the structural half included. `Panel1MinSize`/`Panel2MinSize` are now the
backing store for the clamp (the Majorsilence-only `Panel1MinimumSize`/`Panel2MinimumSize` survive as
forwarding aliases, since a sample and an existing test use them); `FixedPanel` is read from a new
`OnLayout` override that redistributes a container resize between the panels — `None` keeps the split
proportional, `Panel1`/`Panel2` pin their panel — mirroring upstream's `OnResize`/`SetSplitterRect`;
`SplitterMoving` (cancellable, and a handler may rewrite `SplitX`/`SplitY`) and `SplitterMoved` (once,
at the end of the drag) are raised from both `SplitContainer`'s drag path and `Splitter`'s own, whose
two events stopped being `add { } remove { }`; `Splitter.SplitPosition` is the extent of the docked
sibling rather than the bar's own thickness; and the legacy `Splitter` resizes that sibling on a drag,
bounded by `MinSize`/`MinExtra`, for all four dock edges.

The structural half landed rather than being deferred, because both halves turned out cheap and
contained: `Panel1`/`Panel2` are now `SplitterPanel` (the type already existed, unused, in
`MissingTypesParity.cs`) and the constructor's `Dock = DockStyle.Fill` is gone, with `Panel1` starting
at a 50px extent so `SplitterDistance` defaults to WinForms' 50. Nothing in the suite depended on the
old panel type, and only one sample relied on the forced `Fill` (it now sets it explicitly).

`SplitContainer` hosts a `Splitter` as its bar, so the legacy resize-the-sibling behaviour is switched
off for that instance (`Splitter.ResizesTarget`) — the container's clamps are
`Panel1MinSize`/`Panel2MinSize`, not the bar's `MinSize`/`MinExtra`, and left on, every drag would move
the split twice. Upstream's `SplitContainer` does not use a `Splitter` at all.

Also fixed in passing, because the clamp arithmetic was being made load-bearing: `PaddedClientRectangle`
is in scaled device pixels while `Width`/`Height` are unscaled, so every maximum derived from it came
out a factor of the DPI scale too loose. Both classes now divide back out (`Splitter.UnscaledClientExtent`).

32 tests in `tests/Majorsilence.Forms.Tests/SplitContainerBehaviourTests.cs`, 29 of them verified to
fail with their fix neutralized and 3 labelled in-test as guards. Stored-only baseline 759 → 754
(`FixedPanel`, `Panel1MinSize`, `Panel2MinSize`, `SplitterCancelEventArgs.SplitX`/`SplitY` dropped out);
inert-event baseline lost `Splitter.SplitterMoved`/`SplitterMoving`.
*Closed:* `LAY-01`, `LAY-02`, `LAY-03`, `LAY-04`, `LAY-05`, `LAY-07`, `LAY-08`.
*Not in scope:* `LAY-06` (`IsSplitterFixed`/`SplitterIncrement` and the absent keyboard handling) is
P2, was never part of this item, and is left untouched rather than half-done: quantising the drag is
easy, but the arrow-key path needs a focusable splitter, which is its own piece of work.

**W5.23 — `TabControl`. — DONE (2026-09-04)**
All four findings closed. 19 tests in `TabControlBehaviourTests.cs`, 16 of them verified to fail with
their fix neutralized; 3 are labelled in-test as guards. Stored-only baseline 754 → 746.

`TabPage.Enabled` now forwards to `Control.Enabled` instead of storing into its own field, so the page
and its `Control` face agree and the controls *on* the page really go dead — locking a wizard step by
disabling its page was a complete no-op before.

The four selection events are split across two phases, as upstream splits them across `TCN_SELCHANGING`
and `TCN_SELCHANGE`: `Deselecting`, `Deselected`, then `Selecting`, `Selected`,
`SelectedIndexChanged`. Both halves of the deselect now run **before** the strip moves, which needed a
vetoable hook on `TabStrip` (`SelectionChanging`) rather than a reordering of the existing handler —
the point of `Deselecting` is to save the state of the page being left, and it reads `SelectedTab` to
find out which page that is. A cancelled `Deselecting` therefore never moves the strip at all, instead
of moving and moving back.

`TabControl.ImageList` + `TabPage.ImageIndex`/`ImageKey` resolve to a `TabStripItem.Image` on insert,
on either page property changing, and on the image list being swapped (designer files assign it after
`AddRange` as often as before). The tab reserves the image's width plus a gap in `GetPreferredSize` and
`TabStripRenderer` draws it, so icon tabs are both painted and the right width — the widths matter
because they set the header's wrap points.

`Alignment` moves the strip's `Dock`, so `Bottom` really puts the tabs under the pages and
`Left`/`Right` stack them in a column beside them; `GetTabRect` was translated into the control's
coordinates, without which a bottom-aligned tab rect is not comparable with anything else on the
control. `ItemSize` (height always, width under `SizeMode.Fixed`), `SizeMode.Fixed`/`FillToRight` and
`Padding` are all read by the strip's layout.

*Deliberately not done:* `Left`/`Right` stack the tabs rather than rotating their text, which the
renderer cannot express; `Multiline` stays stored-only, because the strip already wraps
unconditionally and making `Multiline = false` stop wrapping needs a scroll affordance that does not
exist (and would lose the current guarantee that every tab is reachable); `Appearance`,
`HotTrack`, `ShowToolTips` and `RightToLeftLayout` stay cosmetic, as `LAY-15` allows — `ShowToolTips`
is `LAY-16`'s. `TabPage.UseVisualStyleBackColor` is still a `new` shadow: unlike `Enabled` its default
genuinely differs from the base's, so forwarding it would change behaviour rather than fix it.
*Closed:* `LAY-12`, `LAY-13`, `LAY-14`, `LAY-15`.
*Corrected:* `LAY-12`'s suggested fix (see the status block in `behaviour-gap/layout.md`).

**W5.24 — Scaling and preferred size: connect the engine that already works. — DONE (2026-08-31)**
All four disconnections closed in one pass: `Panel.GetPreferredSizeCore` delegating to the engine,
`Scale` dispatching through `ScaleControl` (which now scales padding, margin, min/max and anchor info),
`ButtonBase.GetPreferredSizeCore` measuring caption + image + glyph, and `GroupBox.AutoSize` becoming the
real one. 14 tests, 13 of them verified to fail without their fix; stored-only baseline 806 → 805.
*Closed:* `LAY-25` (P0), `LAY-21` (P0), `LAY-34`, `LAY-26`.

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

**W5.25 — Scrolling containers. — DONE (2026-09-14).**
18 tests in `tests/Majorsilence.Forms.Tests/ScrollingContainerTests.cs`, 12 neutralizations each
producing a failure; 2 tests are labelled in-test as guards.
*Closed:* `LAY-22`, `LAY-28`, `LAY-29`, `LAY-30`.

- **`Panel.BorderStyle` draws and insets (`LAY-28`).** The setter validated and invalidated, and
  `PanelRenderer.Render` was an empty method body, so the standard way to group controls without a
  GroupBox showed nothing at all. Mapped onto the instance style's border, which both paints the frame
  and shrinks the client area -- `ClientRectangle` and `DisplayRectangle` already deflate by
  `CurrentStyle.Border`, which is what Win32 does for `WS_BORDER`/`WS_EX_CLIENTEDGE` and why a
  `Dock = Fill` child used to sit 1-2px out from where Windows puts it. The border is added to the
  preferred size too.
- **`TableLayoutPanel` paints its cells (`LAY-22`).** The whole paint region was commented out behind a
  `// TODO: Custom Cell Paint`: the layout engine honoured `CellBorderStyle` and reserved the gap
  between cells, and nothing ever drew in it, so a grid-looking form migrated as a grid of floating
  controls separated by mysterious whitespace. `OnPaintBackground` now walks the strips the engine
  computed, raising `CellPaint` per cell and drawing that cell's border on top, and `OnLayout`
  invalidates. `ControlPaint.PaintTableCellBorder` is new (internal, as upstream's is) and gives
  `Inset` and `Outset` the same lines with the light and dark swapped.
- **`AutoScrollMargin` and `ScrollControlIntoView` (`LAY-30`).** The margin was an auto-property while
  `Recalculate` read a *private field of the same name that nothing ever wrote* -- stored, reported
  back, applied to nothing. `ScrollControlIntoView` turned out not to be the empty body the finding
  describes (it was implemented for the on-screen-keyboard path), but it ignored the margin, ignored
  the horizontal axis entirely, and -- the headline impact -- nothing called it when focus moved, so
  tabbing into a field below the fold still left it invisible. All three fixed; the focus call goes in
  `ControlAdapter`'s focus-change walk, which is where upstream's `ContainerControl` does it.
- **`DisplayRectangle` is the content coordinate space (`LAY-29`).** It reported the visible client
  area with its origin always at the client origin. Upstream's carries the scroll offset as a negative
  origin and the content extent as the size, which is the space every anchor delta in the layout engine
  is expressed in -- so anything converting between content and client coordinates read `(0,0)` and
  mis-placed by exactly the scroll amount.

*A correction to the finding.* `LAY-29` proposes "letting the layout engine move children rather than
`ScrollWindow`", i.e. replacing the scrolling model. That misreads upstream: `SetDisplayRectLocation`
scrolls with `SW_SCROLLCHILDREN`, so the OS moves the child windows there too -- `_displayRect` is
bookkeeping *alongside* the physical move, not a replacement for it. Keeping `ScrollWindow` and adding
the origin/extent to `DisplayRectangle` is therefore the faithful port, and it is consistent by
construction: a layout pass computes a child's position as (delta captured against the old origin) +
(the new origin), which is where scrolling has already put it. The whole suite passed on the first run
with the change in, which is the evidence that the two agree.

---

### Phase 6 — Mechanical sweeps *(parallelisable, low risk, high count)*

**W6.1 — The dead-event sweep (RC-5).** Convert `add { } remove { }` to real events and raise them at
the trigger point that already exists. Work area by area against the W0.1/W0.2 baselines; each area is
an independent branch. ~60 findings.

**W6.1 — the `DataGridView` slice. — 6 of 14 done (2026-09-14).** `ColumnWidthChanged` (from
`Column.Width`'s setter), `RowHeightChanged` (from `Row.Height`'s setter), `ColumnSortModeChanged`
(from `Column.SortMode`, which went from a bare auto-property to one with change detection to get an
owner hook), `ColumnHeadersHeightChanged`, `RowHeadersWidthChanged` (both from the matching
`DataGridView` setter), and `AutoSizeColumnModeChanged` (from `Column.AutoSizeMode`'s setter, carrying
the previous mode). Each is field-backed with a protected `On*` raiser, matching the
`RaiseColumnAdded`/`RaiseRowsAdded` pattern `DataGridViewCollectionEventTests` already established. 7
tests in `DataGridViewChangeEventsTests.cs`, each neutralized and verified to fail.
*Left for their own item:* `RowStateChanged`/`CellStateChanged` — the trigger point (`Selected`) is
real, but `SetRowSelected`/`SetCellSelected` are only the single-item path; every batch selection
change (`Shift`-click ranges, `SelectAll`, `ClearSelection`) writes `SetSelectedCore` directly to keep
the batch to one `SelectionChanged`, and would silently not fire either event. Also left:
`CellValueNeeded`/`CellValuePushed` (`VirtualMode` is a plain property — no read/write path to raise
them from), `DefaultValuesNeeded`/`NewRowNeeded`/`UserAddedRow` (tied to the new-row placeholder, which
does not exist — `DGV-05`), and `ColumnDisplayIndexChanged` (`DisplayIndex`'s setter is a no-op — column
reordering is not implemented). None of these four groups has a single obvious trigger point the way
the six above did; each is its own finding, not a line in this sweep.

**W6.1 — the Telerik `RadGridView.Groups` slice. — done (2026-09-15).** Part of #176. `Groups` returned
`Array.Empty<DataGroup> ()` while grouping was advertised as working -- and grouping genuinely does
work: `GroupByColumn`, the drag-to-group panel, multi-level descriptors, per-group footers and collapse
state are all real and tested. Only the object model exposing them was missing, so a consumer walking
`Groups` to count, label or collapse them silently saw nothing.

It is projected on demand from the same filtered and sorted rows the display is built from, split by
the same run-detection -- which was factored out of `BuildGroupLevel` into `EnumerateGroupRuns` so the
object model cannot describe a different grouping from the one on screen. `DataGroup` gained the
nesting the grid has always supported (it was flat, so multi-level grouping could not be represented at
all even once the projection existed) and a back-reference to the grid, so `IsExpanded`/`Expand`/
`Collapse` read and write the grid's own collapse set rather than a detached bool: collapsing through
the object model and clicking the group header are now the same act, in both directions. 6 tests, 3
neutralizations each producing a failure.

*A note on the baseline.* Two of the new members, `DataGroup.HeaderText` and `Level`, went straight
onto the Telerik stored-only baseline -- nothing in the assembly reads them, because they exist for a
consumer to read. That is a legitimately inert entry rather than a defect, and recording it is what the
baseline is for; it is also a reminder that adding a projection type adds stored-only surface by
construction.

**W6.1 — the Telerik grid-internals slice. — done (2026-09-16).** Part of #176. Four events sat under
one `#pragma warning disable CS0067` labelled "the compat grid does not raise them". Three of them had
a real trigger in the same file, and had done all along:

- **`GroupExpanding`** → `ToggleGroupRow` and `IGridGroupOwner.SetGroupExpanded`. Raised on both,
  because the `Groups` slice above had already made clicking a header and collapsing through the object
  model the same act, and an event on only one route would make a veto depend on which one the
  application happened to use. Deliberately *not* raised by `ExpandAllGroups`/`CollapseAllGroups`: a
  per-group veto part-way through a bulk call leaves the grid half-collapsed and the caller unaware.
  That decision has its own test, so it stays a decision rather than decaying into an oversight.
- **`GroupSummaryEvaluate`** → `ComputeAggregate`, which really does compute group aggregates. Raised
  for every evaluation *including* one that came to null, because a handler supplying a figure the
  built-in aggregates cannot express — a weighted average, a value from elsewhere — is the main reason
  the event exists, and skipping the null case shuts out exactly that use. `Group` is null for a grand
  total and the group otherwise, which is what the args' own documentation says it means.
- **`FilterPopupRequired`** → `ShowFilterPopup`. Telerik hands over its popup element to customise;
  there is no element tree here and no way to host an arbitrary object as this grid's popup, so what
  is implemented is the half that is implementable and the half that matters: the application is told
  which column was asked for, and suppresses the built-in popup by supplying something of its own.

**The fourth, `CreateCell`, stays unraised, and that is the right answer rather than a deferral.**
Telerik raises it so an application can substitute the visual element used for a cell; this grid paints
cells directly and has no element tree, so there is no creation to announce and nothing a handler could
return that anything would read. It keeps a real event — a handler is retained and can be removed —
with the reason on the declaration. Same call as `RadPageView.PageCollapsed` in #185.

15 tests, 3 neutralizations producing 12 failures between them; the 3 that survive are the bulk-call
decision, a no-handler guard and a premise assertion, each labelled in-test. Telerik dead-on-arrival
events 45 → 42, Telerik stored-only properties 424 → 420.

*What this slice cost to find.* The issue's list described all four as unraisable, and three of the
four triggers were already in the file — `ComputeAggregate` had been summing group aggregates the whole
time. This is the third premise from #176's surveys to be wrong in the same direction, and the pattern
is now clear enough to state: **"the compat layer does not do this" is a claim about the compat layer
that is worth checking against the compat layer.** Twice now the machinery existed and only the raise
was missing.
**W6.1 — the Telerik docking slice. — done (2026-09-16).** Part of #176, and the last non-blocked
cluster in it. Five members that stored or ignored what they were handed:

- **`DockWindow` parented nothing.** It added tool windows to a list and returned. A window docked
  through the API never appeared, and `AllDocumentWindows` — which is a real walk of the control tree,
  not a stub — could not find a document that had been docked rather than hand-added to a tab strip.
  Documents now go into the document tab strip (the structure the designer generates and `DockStrip`
  lays tabs over); tool windows are parented to the dock. A window that already has a parent is left
  alone, so docking twice is not a move.
- **`GetDefaultDocumentTabStrip` returned a strip parented to nothing**, so anything added through it
  was invisible and unreachable whatever the caller did next. It now finds or creates the strip inside
  `MainDocumentContainer`.
- **`GetWindows (DockState)` ignored the state**, so asking for the floating windows and asking for the
  auto-hidden ones gave the same answer and neither was right.
- **`DockWindows` enumerated only tool windows**, so `DockWindows.DocumentWindows` was empty however
  the dock was populated — including by the designer-generated structure the rest of the layout reads.
- **`CloseAction` was stored and never read.** Closing always meant hiding, so an application setting
  `CloseAndDispose` to release a document's resources kept every one of them alive. Read now by both
  `RadDock.CloseWindow` and `DockWindowBase.Close`, because an application can close either way.

13 tests, 4 neutralizations each producing failures. Three of the tests are guards rather than proof
and say so: that the strip is reused rather than created per call (a strip each looks like it works
from `DocumentArray` and shows one tab per strip on screen), that docking twice does not reparent, and
that honouring `CloseAction` did not cost the removal bookkeeping that already worked.

**`ContextMenuService.ContextMenuDisplaying` stays unraised, and that is the answer rather than a
deferral** — the compat dock has no context menu of its own, so there is no moment to announce. The
service is still handed back and cached per dock, because docking code subscribes to it; what it cannot
do is fire. Same call as `RadGridView.CreateCell`.

*What the cluster had in common.* Every one of these five was a member that accepted input and threw it
away — a list nothing was added to, an argument nothing read, a property nothing consulted. None would
be caught by a null check or a type error, and each looked correct at its call site. That is the shape
the stored-only and unraised-event baselines exist to surface, and four of the five were on one of them.

**W6.1 — the `Control.BindingContextChanged` slice. — done (2026-09-15).** Closes the setter half of
`CTL-29`/`EVT-33`. `BindingContextChanged` went from `add { } remove { }` to field-backed, and
`OnBindingContextChanged` (an empty virtual in `KryptonPortParity.cs` — "Never raised by this layer")
now invokes it; `Control.BindingContext`'s setter, previously a bare field assignment, gained a
`ReferenceEquals` change-guard and calls it. 2 tests in `ControlExtensibilityHookTests.cs`, each
neutralized and verified to fail (one against the raise itself, one against the guard).
*Left open, per `CTL-29`:* the `AssignParent`/`CreateControl` cascade — reparenting a control whose
*inherited* `BindingContext` changes does not raise, because upstream gates that on `Created`
(handle existence), and this framework's `Control` has no handle of its own to gate on (see
`docs/native-interop.md`). *Also unchanged:* nothing subscribes to the now-real event to re-home a
binding (`BND-15` is a separate, larger finding: `Control.BindingContext`'s getter auto-creates a
private context per unparented control instead of returning `null`, so `Binding.Attach` locks onto
the wrong manager before this event could even help).

**W6.2 — The stored-only sweep (RC-7).** For each entry in the W0.3 baseline, either wire it to its one
consumer or record in the baseline *why* it is legitimately inert. Prefer deleting a private twin over
keeping both (RC-6).

**W6.2 — the `ListView` slice. — done (2026-09-16).** Part of #91. `ListView` carried 20 of the
baseline's entries, the most of any control that is not blocked outright. Three had a consumer waiting:
`TileSize` (the tile layout used a hard-coded 70 for both dimensions, so the property that exists to
size tiles did not size them, and a non-square tile could not be expressed at all), `HideSelection` (the
renderer highlighted a selection whatever the focus was), and `UseCompatibleStateImageBehavior` — whose
**default was wrong**, `false` against upstream's `true`.

That last one is the case worth naming, because it is the one a sweep can get wrong in both directions.
It stays on the baseline afterwards and correctly so: nothing reads it, because it selects between two
.NET 1.1-era state-image behaviours this layer does not implement either way. A stored-only property can
still be *observably* wrong through the value it hands back, and fixing that is not the same as wiring
it. The baseline moved by two, not three, and the third is recorded rather than hidden.

**The reverse error was one property away.** `HideSelection`'s own default reads as though it should
match `ListBox` and `TextBox`, where this layer defaults it to `true`. Upstream's `ListView` carries
`[DefaultValue(false)]` and does not set the flag in its constructor — so `false` was already right, and
"fixing" it to match its siblings would have been the regression. Checking the two defaults cost one
look at upstream each; guessing either would have looked equally reasonable.

The remaining 17 are recorded in `docs/behaviour-gap/lists.md` with the reason each is inert, which is
the half of W6.2 that is not code. They collapse to far fewer than 17 causes: **14 of them are one
feature** — `Groups` is a collection nothing reads, so `ShowGroups`, `GroupImageList` and all 12
`ListViewGroup` members are a single consequence, now `LST-46`. Four more are blocked on infrastructure
that does not exist (virtual mode, label editing, owner draw, column reordering), and the rest need a
hover or tooltip pipeline the control does not have. **The baseline's entry count is not a count of
defects**, and a sweep that treats it as one will report 20 findings where there are six.

10 tests, 4 neutralizations producing 7 failures; the 3 that survive are labelled guards — an unset
`TileSize` still laying out at 70 (which matters more than the feature: reading it without a fallback
collapses every tile on every list that never set one), writing the unset default back without throwing,
and the `HideSelection = false` default keeping its band.

**W6.2 — the whole-baseline triage, and the `ListView.Groups` cluster. — done (2026-09-16).** Part of
#91. Two halves, and the first is the one that changes how the rest of the item should be worked.

*The triage.* All **1,074** stored-only entries (703 core, 371 Telerik) are now classified by **cause**
in `docs/behaviour-gap/stored-only-triage.md`, which replaces `stored-only-properties.txt` — a
2026-08-25 source-level scan of 263 entries whose own header said the IL gate should supersede it. The
baselines are that gate, so the old file is deleted rather than left to drift beside them.

**Roughly half the file is not work.** 599 entries across 194 types are candidates; the other 475 have a
structural reason that applies to a whole bucket at once: outbound `*EventArgs` data carriers (147, and
by construction they will never leave the baseline), the Telerik visual-element surface (112, no element
tree), legacy controls upstream marks `PlatformNotSupportedException` (72 — determined mechanically by
checking each type against upstream's `Controls/Unsupported/` folder, not by judgement), native dialogs
bound by what a platform picker exposes (69), blocked editing and browser infrastructure (48), and the
`CreateParams` Win32 shim (11).

*The one bucket worth reading entry by entry* is `Cancel`/`Handled` members the framework never reads
back — a veto that silently does nothing, which is the bug class #182, #185, #187 and #192 each fixed.
The rule found 10; checking each against its raise site found **two real** (`RadGridView`'s
`CellBeginEdit` cancel is never propagated to the base args; `DataGridViewDataErrorEventArgs.Cancel` is
real but tangled with a revert path), one rule artefact (`NewValue` is outbound), three inert for a
stated reason, and **six that are really W6.1 entries** — no raise site at all, so there is no moment at
which the flag could be consulted. A mechanical bucket is a hypothesis, not a finding.

*The cluster.* `ListView.Groups` was the largest single cause in the whole baseline: 14 entries that
were one fact — the collection was read by nothing, so an application that built groups got an
ungrouped flat list with no error. Now real: a one-row header band per group, group-aware layout,
`CollapsedState` laying a group's items at `Rectangle.Empty` (which removes them from painting and every
hit-test at once, rather than teaching each one about collapse), `ListViewItem.Group` typed and keeping
`group.Items` in step both ways, `ListViewGroup.ListView` set by the collection, and `LineCount` counting
the bands so a grouped list can still scroll to its last row. **6 of the 14 entries closed**; the
decorative half (footers, task links, title images) is still unread and recorded as such. 12 tests, 7
neutralizations each producing a failure.

*A band is one row tall*, which is a deliberate simplification: upstream's are taller, and matching that
means teaching the scroll model about variable line heights. As it stands every line is still one row
and there are simply more of them, so none of the scrolling arithmetic changed — which is why 171
existing `ListView` tests passed untouched through a layout change.

**W6.2 — the `ToolStrip` family slice. — done (2026-09-16).** Part of #91, and the first slice worked
from the triage's candidate queue. 88 baseline entries across the family; five had a consumer sitting
next to them:

- **`ShowShortcutKeys` + `ShortcutKeyDisplayString`** — the *display* half of a shortcut system that has
  really worked since W1.3. Every ported menu fired its accelerators and showed no shortcut text at all.
  `KeysConverter` already formatted a `Keys` the way a menu wants it, and carries a comment saying the
  text is what ends up in a menu item — it had simply never been called from the renderer.
- **`ToolStripDropDownButton.ShowDropDownArrow`**, **`ToolStripStatusLabel.Spring`**, and
  **`ToolStripItem.Alignment`** — an arrow drawn whatever the flag said, status labels that never
  reached the right edge, and items laid out in declaration order whatever the property said.

**Two coordinate-space defects surfaced that have nothing to do with those properties**, and neither
came from reading the code:

- **`TSM-40` (fixed).** `StatusStrip.LayoutItems` laid items straight into `PaddedClientRectangle`,
  which is device-scaled, while item `Bounds` are logical — the exact mismatch
  `MenuBase.LogicalClientRectangle` exists for and which `Menu` and `ToolBar` already avoid. At scaling
  2 a 400px strip laid out across 800 logical units. It surfaced because the `Spring` arithmetic put
  the strip's own width into a calculation for the first time and the scale-2 gate went red.
- **`TSM-41` (recorded).** The strip renderers paint into a logical canvas but position details by
  subtracting device-converted constants from logical edges, so glyphs drift inward as the scale rises.
  Cosmetic, invisible at scaling 1, and its own item — every constant in three renderers is affected.

**The existing test hid `TSM-40`.** `StripHierarchyTests.StatusStrip_LaysItemsOutWhereItPaintsThem`
compared the laid-out bounds against the *same device rectangle the layout used*: both sides wrong
together, and identical at scaling 1. That is the shape to watch for generally — a test that reads its
expectation from the code under test cannot see a systematic error in it.

**Two of this slice's own tests had the same fault and were rewritten.** They sampled a rectangle
derived from the renderer's constants, so they could not find the glyph at scaling 2; they now compare
the item's whole box across two renders, which needs no background guess and no constant. 12 tests, 8
neutralizations each producing a failure. Core stored-only properties 709 → 704.
**W6.2 — the `RichTextBox` slice, and the first of the triage's two real vetoes. — done (2026-09-16).**

**`RichTextBox.ScrollBars` was `TXT-26` one class down.** `TextBox` had a `public new ScrollBars`
shadowing `ScrollControl.ScrollBars` — the property whose setter is what actually shows and hides the
bars — and deleting it was `TXT-26`. `RichTextBox` still had the identical shadow, so **no rich text box
ever displayed a scrollbar**, and a fresh control disagreed with itself: the property said `Both` while
the bars followed the base's `None`. It survived the first fix because its type differs
(`RichTextBoxScrollBars`), and a shadow that changes type is one the compiler cannot warn about. Worth
remembering when a finding names a member rather than a shape: the same defect on a sibling type is not
covered by fixing the one that was reported.

**`ZoomFactor` did nothing.** `TextBox.CurrentFontSize` is now virtual and `RichTextBox` scales it, which
is the one value every caret position, selection rectangle, scroll step and measurement in the base
class reads — so zooming moves all of them together instead of leaving the text one size and the caret
another. The setter also validates against upstream's exclusive `(0.015625, 64)` bounds, which it had
accepted silently.

**Eight of the control's eighteen entries are one cause** and were recorded, not swept: there is no
paragraph model, so `SelectionAlignment`, the three indents, `SelectionTabs`, `SelectionBullet`,
`BulletIndent` and `RightMargin` have nowhere to be stored or painted (`TXT-31`). Same shape as
`LST-46`, and the second time in this sweep that a control's entry count has collapsed to a single
missing feature.

**The first real veto from the triage is closed.** `RadGridView` raised `CellBeginEdit`, built Telerik
args, and dropped the answer — so a handler refusing an edit was ignored and the editor opened anyway.
The base event honours its own `Cancel` (`DataGridView.cs:148`), so forwarding it was all that was
missing. The second, `DataGridViewDataErrorEventArgs.Cancel`, is left as the triage recorded it: upstream's
`Cancel` there means "do not restore the old value", which is tangled with a revert path this layer only
partly has, and changing the default would alter behaviour a comment documents as deliberate.

18 tests, 5 neutralizations each producing a failure. Core stored-only properties 704 → 702, Telerik 371 → 370.

**W6.2 — the four-control sweep. — done (2026-09-16).** Part of #91, and deliberately wider than the
per-control slices before it: seven entries across `TreeView`, `ListBox`, `SplitContainer` and
`ListViewItem`, each a property the control stored and read nowhere.

- **`TreeView.HideSelection`** — read by nothing, **and the default was wrong**. Upstream's `TreeView`
  carries `[DefaultValue(true)]` and sets the flag in its constructor; upstream's `ListView` carries
  `[DefaultValue(false)]` and does not. **The two siblings genuinely differ**, which is worth stating
  because the `ListView` slice nearly "fixed" its `false` to match `ListBox` and `TextBox`. Both were
  checked against the upstream source, one property at a time, and the answers disagreed.
- **`TreeView.FullRowSelect`** — every tree highlighted the whole row, which is what `true` means while
  the property defaults to `false`. An explorer-style tree therefore looked like a list.
- **`TreeView.PathSeparator`** — `FullPath` hard-coded `"\\"`, so a tree told to use `"/"` still built
  backslash paths and `FindNodeByFullPath` could not match one the application had constructed.
- **`ListBox.Sorted`** — a sorted list box came out in insertion order. Sorted on the collection rather
  than at paint, because `SelectedIndex` and every index-based API have to agree with the screen;
  selection is preserved by value, since the index it sat at no longer means the same row.
- **`ListBox.ScrollAlwaysVisible`** — `RC-6` exactly: a second store beside `ScrollbarAlwaysVisible`,
  which is the one the scrollbar logic reads, so the property WinForms code actually writes did
  nothing. The pair was already *named* in `StoredOnlyPropertyBaselineTests`' own header as a known
  twin; it is one value now.
- **`SplitContainer.IsSplitterFixed`** — a container the application had deliberately locked dragged
  like any other.
- **`ListViewItem.UseItemStyleForSubItems`** — the sub-item's own colour always won, which is the
  `false` behaviour applied to every list whether it asked for it or not.

**An existing theme test had to be corrected, and that is worth recording rather than burying.**
`ThemeCssPartTests.TreeViewSelection_IsPaintedWithThePartColour` renders an unfocused tree and asserts
the selection band covers a given area. Both of its premises became real properties here — an unfocused
tree with the corrected `HideSelection` default shows no band, and `FullRowSelect = false` shrinks it to
the label — so the test now pins both and goes on measuring the colour it is named for. It was not
green by accident before; it was green because neither property did anything.

15 tests, 8 neutralizations each producing a failure. Core stored-only properties 702 → 695.
**W6.2 — the dead scroll-properties family. — done (2026-09-16).** Part of #91. Seven baseline entries
closed by one change, because they were one fact.

`ScrollPropertiesBase` was a **parallel, dead copy** of `ScrollProperties`: seven auto-properties with no
connection to any scrollbar, with `HScrollProperties`/`VScrollProperties` derived from it — while the
live `ScrollProperties`, which forwards to a real `ScrollBar` and is what
`ScrollableControl.HorizontalScroll`/`VerticalScroll` actually return, sat in its own file beside it.

So `panel.VerticalScroll.Value = 50` worked, and anything an application declared as
`HScrollProperties` silently did nothing. `RC-6` exactly — "prefer deleting a private twin over keeping
both" — and **upstream settles the shape**: it has `ScrollProperties` with `HScrollProperties` and
`VScrollProperties` derived from it, and no `ScrollPropertiesBase` at all. The invented base type is
deleted, the two real ones derive from the live class, and `ScrollableControl` returns upstream's types.
`ApiDiff` reports no new gaps, which is the expected direction: removing a type upstream does not have
and matching its return types makes the surface closer, not further.

*The same branch then took the `DataGridView` slice* — five more entries. `ScrollBars` is the **fourth**
member of that family found dead, after `TextBox` (`TXT-26`), `RichTextBox` (`TXT-29`) and `ListBox`'s
twin: four controls, four separate discoveries, one shape — a scrollbar-policy property sitting beside
scrollbar logic that never consults it. `HideSelection` is **not an upstream `DataGridView` member at
all**, so there was no default to match and none to copy from the siblings either (`ListView`'s is false,
`TreeView`'s is true); it is wired to the obvious semantics and recorded as a decision rather than left
to look like parity. And `DataGridViewLinkCell` was painted exactly like a text cell — the three colour
members are live now, the four interaction ones are recorded as a feature (`DGV-42`).

*A test-shaped lesson.* Three members — `Maximum`, `LargeChange`, `SmallChange` — plus `Visible` are
owned by the layout on an `AutoScroll` panel and recomputed from the content, so the first version of
the forwarding test was measuring `AutoScroll` rather than the forwarding. It uses a plain panel now.
`Visible` still cannot be asserted independently, because the control owns it in both directions; that
line is labelled in-test as exercised-not-proved rather than dressed up, and the other six carry the
proof.

6 tests, 2 neutralizations. Core stored-only properties 695 → 688.

**W6.2 — the `DataGridView` slice, then worked by FAMILY. — done (2026-09-17).** Part of #91. Seven
entries, and a change of method that is the point of the item.

*The `DataGridView` slice (5).* `ScrollBars` was read by nothing, so a grid told to scroll vertically
only still grew a horizontal bar. `HideSelection` is **not an upstream `DataGridView` member**, so there
was no default to match and none to copy from the siblings either; it is wired to the obvious semantics
with the decision recorded rather than left to look like parity. `DataGridViewLinkCell` was painted
exactly like a text cell — the three colour members are live now, the four interaction ones recorded as
a feature (`DGV-42`).

*Then the method changed.* By this point the same shape had been found five times in five controls, so
the next move was to grep the baseline **for the shape** rather than walk another control:

- **The scrollbar-policy family.** A property saying which bars are permitted, beside logic that never
  consults it: `TextBox` (`TXT-26`), `RichTextBox` (`TXT-29`), `ListBox`'s twin (`LST-51`),
  `DataGridView` (`DGV-40`) and `TreeView.Scrollable` (`LST-54`, closed here). Five controls, found
  separately, months apart, by whoever happened to be working that control. Searching for the shape
  found the fifth in minutes.
- **The `HideSelection` family — and it does not agree with itself.** `ListView` is upstream-false and
  was already right; `TreeView` is upstream-true and was wrong; `DataGridView` and `ListBox` are not
  upstream members at all; `ComboBox` is the same and is left unread because it has no list of its own
  to paint. Four controls, one property name, four different right answers. **Checking upstream per
  control is the only thing that separates "ours is wrong" from "ours is right and the sibling differs"**
  — and this sweep has now made both mistakes' opposites available to compare.
- **`BorderStyle` is eight entries and one cause** (`LST-56`): there is no control-level border model, so
  there is nowhere for the property to be honoured. Recorded rather than wired eight times, which is how
  eight renderers would drift apart.

*A process note worth keeping.* Two edits in this slice silently did not apply — the target property
lived in a parity file rather than the control's own — and the build still succeeded, because the code
that would have used them was equally absent. It was caught by grepping for the new text **after**
writing rather than trusting the absence of an error. That is the same habit the conflict-marker
incidents earlier in this plan produced, and it is worth stating that it pays off on ordinary edits too.

6 tests in the families file plus 12 in the `DataGridView` one, 7 neutralizations between them. Core
stored-only properties 683 → 676.
**W6.2 — two more shapes. — done (2026-09-17).** Part of #91, continuing to work by shape rather than
by control.

- **`TreeView.ImageKey`/`SelectedImageKey`** (`LST-57`). `ResolveImage` resolves by key, then by index.
  The index chain falls back to the tree's own default; the key chain never did — so a tree naming its
  default icon by key showed no icon at all. **The method's own remark said "each falls back to the
  tree's own default"**, describing behaviour only half of which existed. Worth noting as its own small
  lesson: the comment was accurate about intent and wrong about the code, and nothing had ever checked.
- **`ListView.Activation`** (`LST-58`). Only double-click raised `ItemActivate`, so `OneClick` — the
  entire point of the property — behaved exactly like `Standard`. The double-click path now skips
  activating in `OneClick` mode, or one gesture would deliver two activations.
- **Per-item tooltips are 13 entries and one missing host** (`LST-59`). Eight controls, thirteen
  properties, all waiting on the same thing: the `ToolTip` component can show a tip for a *control*, and
  nothing maps a hover over a cell, item, node, tab or strip button to a tip. Recorded, not wired eight
  times.
- **State images are 4 entries and one missing feature** (`LST-60`).

*A test that passed for the wrong reason, caught by the neutralization rather than by review.* The
selected-image test selected the node *after* capturing the baseline render, so the selection band alone
changed the row and the comparison passed whether or not the key was read. The neutralization run showed
only one of three image tests going red, which is what exposed it. Selecting first isolates the image.

7 tests, 3 neutralizations. Core stored-only properties 676 → 673, with 17 more accounted for as two
recorded causes.

**W6.2 — the remaining big types, and a category the triage had missed. — done (2026-09-17).** Part of
#91. `Form`, `Control`, `WindowBase` and `ToolTip` hold 47 entries between them and looked like the last
large seam. Most of it is not a seam at all, and finding out why is the result.

**One real gap: `Form.ControlBox`.** A form that asked for no control box still got minimise, maximise
and close. `FormTitleBar` had switches for minimise and maximise and none for close, so there was no way
to express the one thing the property is for; it gains `AllowClose`, and `ControlBox` drives all three.
It is a master switch rather than an override — turning it back on restores whatever `MinimizeBox` and
`MaximizeBox` were set to, which has its own guard.

**The category the triage had missed: framework-written outbound state.** `Form.Modal` is *already*
set by the dialog path and cleared on close; nothing reads the getter in this assembly because the
reader is application code, and upstream's `Modal` is read-only for exactly that reason.
`WindowBase.Disposing` is the same. **These are not gaps, and a sweep that "wires" one changes a
property that already works** — I started to do precisely that before checking whether the framework
assigns it.

That check is cheap and is now written into the triage document: *does the framework assign this
member?* It is the mirror of the outbound `*EventArgs` data carriers already recorded there, and it
matters most on exactly these types, where several entries are state rather than settings.

*A test-configuration trap worth recording.* The `ControlBox` tests assert caption-button visibility,
and in two of the four gate configurations the managed caption does not exist — the OS draws the chrome
and the whole title bar is hidden. Asserting there would have been vacuous rather than wrong, which is
the harder kind to notice. They guard on a managed caption being present and return early otherwise, and
the neutralizations were run under `MF_FORCE_CUSTOM_CHROME=1`, which is the configuration that actually
exercises them.

5 tests, 2 neutralizations. Core stored-only properties 673 → 672.

**W6.2 — the outbound-state split, measured rather than guessed. — done (2026-09-17).** Part of #91,
and the answer to "what is actually left" rather than another slice.

#202 established that some stored-only entries are **outbound state**: the framework writes them for the
application to read back, so nothing reads the getter here and the scan flags them — and "wiring" one
breaks a property that already works. That was three verified examples and a hand grep. It is now a
fact the gate computes.

`StubSurfaceScanner` tracks field STORES as well as loads, and marks every entry the framework writes
with `-- framework-written (outbound state)`. **378 of the core baseline's 663 entries are marked: 57%
of what remains is not a gap.** The real candidate count is **285**, not 663.

*The first version of the detection was wrong, and the motivating cases caught it.* It counted only
direct `stfld`, which is possible only inside the declaring type — and `Modal = true;` on a
`{ get; private set; }` property compiles to a **setter call**. So it marked 281 entries and **not one of
Form.Modal or WindowBase.Disposing**, the two cases the whole idea came from. Checking the examples that
motivated a change against the change is worth the thirty seconds.

*The marker is annotation, not assertion.* Whether a setter call survives as a call depends on the build
configuration: the baseline regenerated in Debug failed in Release. The gate strips the note from both
sides before comparing, so the two configurations cannot disagree about the answer while still recording
it. Caught by the Release gate, which is the second time a configuration difference in IL has surfaced
in this scanner (the first was `box` in the phase-0 walker).

*Not done:* the Telerik baseline uses the deep-reachability scan, whose model does not track writes.
Adding it is a second scanner's worth of work and is recorded rather than half-built.

**W6.2 — wiring the candidates, batch 1. — done (2026-09-17).** Part of #91, and the start of working
the list the outbound-state split produced rather than hunting for shapes.

*The list is 158.* Of the 662 core entries: 377 are framework-written (not gaps), 127 fall in a
structural bucket already recorded, and **158 are genuine candidates** across ~72 types. That is the
real remaining W6.2 surface, and it will take several passes — this is the first.

- **`DataGridViewCheckBoxCell.TrueValue`/`FalseValue`.** Only the COLUMN's mapping was ever consulted,
  so a cell that overrode it was ticked by the column's rule — the opposite answer for the same value.
  The cell's mapping now wins, on both the read and the write-back, with the column path untouched for
  every grid that relies on it.
- **`ButtonBase.Command`.** Stored and read by nothing, so a button bound to a command did nothing at
  all when clicked — the WinForms 8 idiom, silently inert. Run after the `Click` handlers, as upstream
  does, so a handler that reconfigures the button goes first.

*Two candidates turned out not to be wirable, and are recorded rather than forced:*
`ButtonBase.CommandParameter` has nowhere to go — `ICommandExecutor.Execute` takes no argument here, and
giving the interface a parameterised overload is a public API decision, not a sweep. And
`ToolStripItem.DoubleClickEnabled` gates an `OnDoubleClick` that **nothing calls**: `ToolStripItem` is
not a `Control`, so gating it would be unobservable. That is the W6.1 category wearing a W6.2 costume,
which the triage already recorded happening six times in the `Cancel` bucket.

*A vacuous test the neutralization caught, again.* The `FalseValue` test set a cell's `FalseValue` and
asserted the value read as unchecked — which the `"True"/"1"` fallback answers anyway, so it passed
whether or not the property was read. It now sets the COLUMN to call the same value **true**, which is
the only arrangement where the two answers differ.

*And the Release gate earned its place.* Adding one `<param>` tag to an existing method requires tags
for every parameter (CS1573, warnings-as-errors in Release). Debug built clean; Release did not.

8 tests, 4 neutralizations. Core stored-only properties 662 → 659; **155 candidates remain**.

**W6.2 — wiring the candidates, batch 2. — done (2026-09-17).** Part of #91. Two entries wired, one
attempted and reverted, and the reverted one is the part worth reading.

- **`DataGridView.HorizontalScrollingOffset`** — `RC-6` again, and the **second twin of this exact
  kind** after `ListBox.ScrollAlwaysVisible`. The grid already had a live horizontal offset
  (`horizontal_scroll_offset`, exposed internally as `HorizontalScrollOffset`) and this WinForms-named
  property stored a second one nothing read: migrated code set the name that did nothing while the grid
  scrolled independently. It forwards now, through the scrollbar when one is showing so the thumb and
  the offset cannot disagree.
- **`DataGridView.FirstDisplayedScrollingColumnIndex`** — answered whatever had last been assigned, or
  0 on a grid nobody had assigned it on. It is computed from the offset now, and assigning it scrolls.

**`TabControl.HotTrack` was attempted and reverted.** The gating is one line, and every tab control
hot-tracks today — which is the `true` behaviour applied whatever the property says. But the default
theme gives `TabStrip::item:hover` no background, so a hovered tab is pixel-identical to an unhovered
one and **nothing about the property is observable**; colouring the part through CSS did not make it
observable either. A wiring that cannot be demonstrated is not a wiring, so it is recorded rather than
claimed. Reverting cost less than the two failed attempts to test it.

*The scale-2 gate caught a test asserting something the control cannot do.* `FirstDisplayedScrollingColumnIndex = 2`
is unreachable at `MF_HEADLESS_SCALE=2` — the visible width is smaller, the scrollbar clamps, and the
getter then honestly reports the column it actually reached. The test asked for a reachable column
instead: asking for one the control cannot reach tests the clamp, not the property.

5 tests, 2 neutralizations. Core stored-only properties 659 → 657; **153 candidates remain**.
**W6.2 — wiring the candidates, batch 3. — done (2026-09-17).** Part of #91. All 155 remaining
candidates were examined; two wired, one reverted.

- **`DataGridViewRow.DividerHeight`** — extra space below a row, the usual way a grid separates groups.
  Stored and read by nothing. Wiring it turned up **two parallel row-height computations**:
  `GetRowDisplayRectangle` summed `Rows[i].Height` itself while the paint path went through
  `RowDeviceHeight`. Putting the divider in one would have made the public rectangle disagree with
  where the row is drawn, so both now go through one `RowTotalHeight`.
- **`PictureBox.InitialImage`** — what the box shows *while* an async load runs. Set synchronously
  before the load starts, which is also why it is demonstrable.

**`PictureBox.ErrorImage` was attempted and reverted**, for the same reason as `TabControl.HotTrack` in
batch 2: its moment is on the async failure path, which completes through `RunOnUiThread`, and no
fixture in this suite can pump far enough to observe it — five seconds of `DoEvents` never sees
`IsErrored` flip. The line is written and commented out at the exact point it belongs, so the next
person with a pumping fixture has one line to add rather than a search to repeat.

*The examination is the deliverable as much as the wiring.* Of the 155: about 10 are designer-host
surface (`ControlDesigner`, `Adorner`, the `DesignerAction*` family, `ToolboxItem`) with no designer to
consult them; 12 are the `TaskDialog` family; the `UseCompatibleTextRendering` group has no GDI/GDI+
distinction to make; and the rest cluster behind the hover, editing, virtual-mode and printing features
already recorded. **The wirable remainder is far smaller than the candidate count**, and each one costs
an examination to tell which it is — this batch examined 155 and found two.

*Two Release-only build failures and two scale-2 test failures in one batch.* CS1587 from a doc comment
inserted between an existing one and its member; a `GetRowDisplayRectangle` assertion converting logical
units to device. Both configurations earn their place in the gate set, repeatedly.

6 tests, 3 neutralizations. Core stored-only properties 657 → 655; **153 candidates remain**.

**W6.2 — the `RichTextBox` paragraph cluster, scoped and partly closed. — 2026-09-17.** Part of #91.
Picked as the next mechanism on the grounds that it was self-contained to one control. **That was half
right, and the correction is the useful part.**

The *model* is self-contained. The *rendering* is not: `RichTextBox` shares `TextBox`'s pipeline, which
builds **one `TextBlock` for the whole document** with a single alignment and wrap width. Per-paragraph
alignment and indents mean one block per paragraph, which moves caret positioning, hit-testing,
scrolling and the selection overlay — a text-subsystem change of the kind `W5.17` was rated high risk
for. And a model on its own closes **no** baseline entry, because an entry leaves only when something
reads it. So `TXT-31` stays open with its scope now stated rather than assumed.

**`RightMargin` is separable and is closed.** It is the wrap width itself, which that one block already
has: `TextBox.WrapWidth` is virtual now and `RichTextBox` overrides it. Zero keeps upstream's "wrap to
the control" meaning — the default, so nothing changes for any existing box — and a negative value is
rejected as upstream rejects it rather than stored.

*Worth stating plainly:* I recommended this cluster as low-risk before checking how the text is laid
out. The recommendation was wrong in a way that only reading the pipeline could have caught, and the
scoping note above is what the next person needs so the mistake is not repeated.

4 tests, 3 neutralizations. Core stored-only properties 655 → 654.
**W6.2 — state images (`LST-60`). — done (2026-09-17).** Part of #91, and the second of the recorded
mechanisms to be built. Four baseline entries closed by one change: `ListView.StateImageList`,
`ListViewItem.StateImageIndex`, `TreeView.StateImageList` and `TreeNode.StateImageIndex` were stored and
read by nothing, so a list or tree using state images showed ordinary check boxes instead.

A state image now replaces the check glyph in the slot both renderers already draw, through one shared
resolver so the two cannot answer differently. An index outside the list falls back to the glyph rather
than throwing: an index and a list that disagree is an application mistake, and a paint path is the
worst place to surface it.

**Picked after the `TXT-31` correction, and on the strength of it.** The paragraph model was chosen first
as "self-contained to one control" and was not, because `RichTextBox` shares `TextBox`'s single-block
layout. This one genuinely is: a second image in a slot that already exists, with no shared pipeline
behind it. The lesson generalises — *self-contained to one control* is a claim about the pipeline, not
about the class, and it has to be checked against the pipeline.

6 tests, 4 neutralizations. Core stored-only properties 654 → 650.

**W6.2 — the per-item tool-tip host (`LST-59`). — mostly done (2026-09-18).** Part of #91, and the
largest of the recorded mechanisms: 13 entries across 8 controls, all waiting on the same missing thing.

`ToolTip.SetToolTip` associates text with a whole CONTROL and shows it on `MouseEnter`, which cannot
express a tip that changes as the pointer moves *within* one control. The seam is
`Control.GetToolTipText (Point)`, driven from the mouse-move path that already exists and shown through
`ToolTip`'s existing popup; each control's override is then five lines reading its own two properties.
**8 of the 13 closed** — `ListView`, `TreeView`, `ToolStrip` and `TabControl`, each with its flag and its
per-item text.

*Three design points that are not obvious and are now in the finding.* The tip is re-shown only when the
text CHANGES, or every mouse-move re-creates the popup and it flickers under the pointer. One shared
`ToolTip` instance, since the popup is modeless and only one is ever up. And the `TabControl` override
lives on `TabStrip`, because the tabs are the strip's children — the pointer is never over the
`TabControl` itself when it is over a tab.

*The seam is `internal`, not `protected`, and a gate decided that.* `Control`'s public and protected
members are held to a parity gate against `WindowBase`; making the seam protected failed it immediately.
It is not upstream surface — it is an implementation seam for the controls in this assembly — so
`internal` is both what the gate wants and what it should have been.

*Four gate failures in one batch, every one of them mine:* the parity gate above, an ambiguous `cref`
(`SetToolTip` has two overloads), a doc comment my insertion displaced from `TreeView.GetItemAtLocation`,
and a scale-2 failure from building a LOGICAL point out of `TreeNode.Bounds`, which is DEVICE. The last
is the fourth time this session a test of mine has mixed those two spaces, and every one was caught by
the same gate rather than by review.

8 tests, 2 neutralizations. Core stored-only properties 650 → 642.

**W6.3 — Coordinate-space audit (RC-8). — DONE (2026-09-15).**
7 tests in `tests/Majorsilence.Forms.Tests/CoordinateSpaceTests.cs`, 5 neutralizations each producing
a failure.

*The rule, now stated once and applied:* every public hit-test and rectangle-returning member is in
**logical** units -- the space `Bounds` and `MouseEventArgs` are in -- so that the idiom an application
writes (`list.GetItemRectangle (i).Contains (e.Location)`) is right. Device pixels belong to painting
and to the laid-out item bounds behind those members; the conversion happens once, at the public
boundary. `Control` gained internal `Point`/`Rectangle` overloads of the existing
`DeviceToLogicalUnits`/`LogicalToDeviceUnits` family to write that boundary with, and
`DataGridView`'s private `DeviceToLogicalUnits (Rectangle)` twin was deleted in favour of them (RC-6).

Five members were on the wrong side of the boundary, every one of them invisible at scale 1:

- **`ListBox.GetItemRectangle`** returned device pixels. Every caller inside the assembly knew to
  convert (the renderer, the automation peer, `CheckedListBox`'s glyph hit-test); an application had
  no way to know. It answers in logical units now, with `GetItemRectangleDevice` for the internal
  callers.
- **`ListView.HitTest (x, y)`** compared a logical point straight against device item bounds, so
  `listView.HitTest (e.X, e.Y)` -- the only thing the method is for -- picked the item at index x
  scale: on a 2x display a click on the second row reported the fourth.
- **`TreeView.HitTest`** tested its point against *three* spaces in one method: a device
  `ClientRectangle` for the bounds guard, logical for `GetNodeAt`, and device node bounds for the
  expander indent.
- **`DataGridView.GetCellDisplayRectangle`** converted the rectangle to logical and then clipped it
  against a device `ClientRectangle`, so `cutOverflow` cut nothing on a scaled display -- the one
  thing the flag exists to do.
- **`GetColumnDisplayRectangle`/`GetRowDisplayRectangle`** built one rectangle out of two spaces: x
  and width from the logical `Columns[i].Width`, height from the device `ClientRectangle`.

*Found and deliberately not fixed here:*
- **`ListViewItem.Bounds`, `GetBounds (portion)` and `GetSubItemAt` are public and in device pixels**,
  along with `ListView.GetItemRect`, which returns them. Converting the item-level family means an
  internal device twin and rewriting 33 call sites across the layout, the renderer and 7 test files --
  its own item, not a line in this sweep. `ListView`'s rectangles are therefore uniformly device today
  while its hit-test is logical; that is recorded rather than half-changed.
- **`TreeView`'s `PlusMinus` hit-test band does not exist.** The indent it keys off is
  `item.Bounds.Left`, and a laid-out node's bounds span the whole row from x ~= 1 whatever its depth,
  so the threshold is ~0 and every point classifies as `Label`. The conversion on that line is still
  correct; it is simply unobservable until the indent is real. A separate finding, not a coordinate
  bug.

**W6.4 — Getters that guess (RC-9). — DONE (2026-09-15).**
6 tests in `tests/Majorsilence.Forms.Tests/GettersThatReportStateTests.cs`, 3 neutralizations each
producing a failure; 2 tests labelled in-test as guards.

*All four named starting points were already fixed by earlier items* -- `MaskCompleted` delegates to a
real mask provider (W5.13), `RichTextBox.Rtf` serialises the document (W5.14),
`FontFamily.IsStyleAvailable` queries the Skia typeface, and `ImageCodecInfo.GetImageDecoders` returns
a real list with a doc explaining why it is a superset of the encoders. The item's own note predicted
this. The work was therefore to *find* what still guesses.

*Fixed:* four members of `Graphics`'s clipping family, all answerable from `ClipBounds`, which has been
real the whole time. `IsVisibleClipEmpty` returned a bare `false` and the three `IsVisible` overloads a
bare `true`. Between them they are the early-out every custom-drawn control is written around --
`if (e.Graphics.IsVisibleClipEmpty) return;` and `if (!g.IsVisible (row)) continue;` -- so a constant
answer means the control does all the work it asked to skip, and reports "visible" for a point that
demonstrably is not. `IsVisible (Rectangle)` *intersects* the clip rather than being contained by it,
which is what `System.Drawing` means: a rectangle half inside the clip is partly visible, and a caller
skipping it on a containment test would leave a hole at the clip boundary.

*Surveyed and deliberately left alone.* 39 public members return a bare literal, and most are not
guesses at all: `IsReadOnly => false` on a mutable collection is a fact, `BatteryLifeRemaining => -1`
is WinForms' documented "unknown", and the `SystemInformation`/`Design`/`VisualStyleRenderer`
constants are already annotated with the reason no backend supplies them -- which is the rule's own
"annotate why it cannot be computed" branch, already satisfied. Two are worth recording as *decisions*
rather than omissions:

- **`DataGridViewColumn.HasDefaultCellStyle => true`** stays `true`. It looks computable, and tracking
  assignment would be easy -- but `ShouldSerializeDefaultCellStyle` consults it, and the common
  designer form is in-place mutation (`col.DefaultCellStyle.BackColor = Red`), which no assignment flag
  sees. Returning `false` there would silently drop the style from serialisation: a worse failure than
  the over-serialisation it fixes. Honest computation needs a value comparison on
  `DataGridViewCellStyle`, which does not exist yet.
- **`TreeNode.IsEditing => false`** is true by construction -- there is no label editing in this layer
  (`BeforeLabelEdit`/`AfterLabelEdit` are still inert, see W6.1) -- so the constant is correct and the
  gap is that it read as computed. Annotated.

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
| 3 — Form and application lifecycle | **Done.** W3.1–W3.5 (reuse, real modal dialogs, the owner graph, `Application` lifecycle, the client area); 35 tests. W3.6 (`AutoScaleMode`) landed 2026-08-31; 11 tests. |
| 4 — Data binding | **Done** (2026-09-01). W4.1–W4.6; 26 tests, all verified to fail without their fix; 4 tests inverted. Out of the phase's scope and still open: `BND-15`, `BND-17`, `BND-22`, `BND-25`–`BND-27`, `BND-29`, `BND-32`–`BND-35`. |
| 5 — Per-control behaviour | **Done:** **W5.2** (`DataGridView` cell/row/column participants — `W5.2a` values and visibility, `W5.2b` the selection model), **W5.6** (`ListView`), **W5.7** (`CheckedListBox`), **W5.8** (list selection events), **W5.9** (`TreeView`), **W5.10** (`ComboBox` edit region), **W5.11** (`TextBox` stored-only behaviour), **W5.12** (mutations off the `Text` setter), **W5.13** (`MaskedTextBox`), **W5.14** (`RichTextBox` document model), **W5.15** (`ToolStrip` item storage), **W5.16** (strip facade and coordinates, plus the menu-mode keyboard navigation left over from W1.3), **W5.17** (text measurement), **W5.18** (pens and clipping), **W5.20a** (scroll/spin arithmetic), **W5.20d** (`ErrorProvider` rendering), **W5.22** (`SplitContainer`/`Splitter`), **W5.23** (`TabControl`) and **W5.24** (layout/preferred-size wiring). **Three clusters now have no P0s left:** the text controls, the ToolStrip family (`TSM-02` was closed by W1.3 in Phase 1 — see `MenuShortcutTests.cs` — which the findings file had not recorded), and the list controls. **W5.1** (`DataGridView` editing lifecycle) done 2026-09-11; **W5.3** (incremental binding) done 2026-09-13 and **W5.4** (styles, sizing, sorting, including the `DGV-13` default-value flip) done 2026-09-14. **W5.5** (mouse/keyboard, including the `DGV-26` combo-box column) done 2026-09-14. **W5.19** (`ControlPaint` chrome), **W5.20b** (`NumericUpDown` text entry and the `UpDownBase`/`DomainUpDown` shape) and **W5.20c** in full (`MonthCalendar` 2026-09-04, `DateTimePicker` 2026-09-14) done 2026-09-14 — **W5.20 is now closed end to end**. **W5.21** (buttons, labels, pictures) done 2026-09-14, closing 13 findings plus `SMP-04` and half of `SMP-06`. **W5.25** (scrolling containers) done 2026-09-14, closing the last four layout findings in the phase — **Phase 5 has no items left open except the `MouseDownBackColor` half of `SMP-06`** (no pressed state exists on `ButtonBase` to read — needs its own item, see `SMP-06` in `simple-controls.md`). |
| 6 — Mechanical sweeps | **W6.5 done** (matrix corrections, 2026-08-31). **W6.1 in progress:** the `DataGridView` slice done 2026-09-14 — `ColumnWidthChanged`, `RowHeightChanged`, `ColumnSortModeChanged`, `ColumnHeadersHeightChanged`, `RowHeadersWidthChanged`, `AutoSizeColumnModeChanged`, 6 of the area's 14 `InertEventBaseline` entries (`CellValueNeeded`/`CellValuePushed`, `DefaultValuesNeeded`/`NewRowNeeded`/`UserAddedRow` and `ColumnDisplayIndexChanged` are blocked on features that do not exist yet — `VirtualMode`, the new-row placeholder, column reordering — and `RowStateChanged`/`CellStateChanged` need the batch-selection paths handled, not just the single-item one; see `docs/behaviour-gap/datagridview.md`). The Telerik grid-internals slice done 2026-09-16 — `GroupExpanding`, `GroupSummaryEvaluate` and `FilterPopupRequired` wired (three of the four had a real trigger sitting in the same file), `CreateCell` recorded as genuinely unraisable for want of an element tree; Telerik unraised events 45 → 42. The Telerik docking slice done 2026-09-16 — `DockWindow` now parents what it is given, `GetDefaultDocumentTabStrip` returns a strip that is part of the tree, `GetWindows` honours its state argument, `DockWindows` includes documents and `CloseAction` is read; `ContextMenuDisplaying` recorded as having no moment to announce. The `Control.BindingContextChanged` slice done 2026-09-15 — the setter half of `CTL-29`/`EVT-33`; the `AssignParent`/`CreateControl` cascade half is left open, pending a decision on what "handle created" means for a `Control` that has no handle. Areas remaining in the 45-entry `InertEventBaseline` and the 117-entry `UnraisedEventBaseline`, W6.2–W6.4 not started — tracked as GitHub issues #90–#93. **W6.3 done 2026-09-15** (coordinate-space audit): five public hit-test and rectangle members were in device pixels where `Bounds` and `MouseEventArgs` are logical — `ListBox.GetItemRectangle`, `ListView.HitTest`, `TreeView.HitTest`, `DataGridView.GetCellDisplayRectangle` and the column/row display rectangles — every one of them an exact no-op at scale 1. The rule (public members are logical; convert once at the boundary) is stated in `CoordinateSpaceTests`. Two findings raised and deferred: `LAY-38` (the `ListViewItem` bounds family, 33 call sites — **closed 2026-09-15**, see below) and `LAY-39` (`TreeView`'s `PlusMinus` band is unreachable). **W6.4 done 2026-09-15** (getters that guess): all four members the item named had already been fixed by earlier work, so the job was finding what still guessed — four members of `Graphics`'s clipping family (`IsVisibleClipEmpty` and the three `IsVisible` overloads), every one answerable from the real `ClipBounds`, and between them the early-out every custom-drawn control is written around. Of 39 members returning a bare literal, most are facts rather than guesses; two (`HasDefaultCellStyle`, `TreeNode.IsEditing`) are recorded as decisions with their reasons. **W6.2 in progress:** the `ListView` slice done 2026-09-16 — `TileSize` wired into the tile layout and its setter validated, `HideSelection` read by the renderer, `UseCompatibleStateImageBehavior`'s wrong default corrected; the other 17 entries recorded with reasons in `lists.md`, where 14 of them turn out to be the single `Groups`-is-unread feature (`LST-46`). The whole-baseline triage done 2026-09-16 — all 1,074 entries classified by cause in `docs/behaviour-gap/stored-only-triage.md`, which supersedes the stale `stored-only-properties.txt`; 599 across 194 types are candidates and the rest have a structural reason. `LST-46` mostly closed the same day: group bands, membership, collapse and scroll counting are real, the decorative half is not. The `ToolStrip` family slice done 2026-09-16 — the shortcut display half, `ShowDropDownArrow`, `Spring` and `Alignment` wired, and two coordinate-space defects surfaced by doing so (`TSM-40` fixed, `TSM-41` recorded). **Open:** **W6.1**'s remaining slices, the rest of **W6.2** — tracked as GitHub issues #90 and #91. |

Suite: **4395 passing, 0 failing**, in Debug and Release, with system decorations and with
`MF_FORCE_CUSTOM_CHROME`, and under `MF_HEADLESS_SCALE=2` run serially. The API gap gate reports zero
for both surfaces, and the core builds warning-free under `IsAotCompatible`. Baselines: inert events
80 → 66, unraised events 130 → 119, stored-only properties 822 → 759, no-op stubs
156 → 154.

### LAY-38: the other half of the coordinate-space audit

W6.3 fixed the members it could reach cheaply and recorded the `ListView` item-level family as its own
item, because converting it meant an internal device twin and 33 call sites. Done now, the same way
`ListBox` was: `ListViewItem.DeviceBounds` is the internal store the layout writes, and every public
member converts at the boundary.

**Three of the defects fixed themselves once the store moved**, which is the argument for doing it at
the boundary rather than at each call site:

- `ListView.HitTest` no longer needs the conversion W6.3 put in it — both sides are logical now, so the
  special case disappeared rather than being maintained.
- `FindNearestItem` compared a logical `x`/`y` against device rectangles and picked the wrong item on a
  scaled display. Nobody had noticed; it was never in the finding.
- `GetSubItemAt` walked LOGICAL `Columns[i].Width` across a DEVICE `Bounds`, so the columns it stepped
  through were the wrong size relative to the rectangle it started from — a mixed-space bug *inside a
  single method*, and the second of those this audit has found (`TreeView.HitTest` had three spaces in
  one method).
- `InvalidateItems` passed a device rectangle to `Control.Invalidate`, which takes logical.

**The renderer is the risk in a change like this, and the compiler cannot help.** All 35 of its
`item.Bounds` reads had to become `DeviceBounds`; every one of them compiles either way and is wrong
only when the scale is not 1. The file now opens with a banner saying which space it paints in, because
the next person to add a line there has nothing else to tell them.

### A correction to W5.25 and W6.3

Both items got a finding ID wrong, in opposite directions, and both were already merged when it
surfaced. Recorded here rather than quietly fixed, because the findings register is what every other
document indexes by.

**W6.3 reused two IDs that already existed.** Its two new findings were appended as `LAY-31` and
`LAY-32`; those numbers belonged to `ScrollableControl.DockPadding` and
`ScrollableControl.AutoScrollMargin`. The register carried duplicate IDs from the moment that PR
merged. The new ones are now `LAY-38` and `LAY-39`, and the plan's references to them follow. The
lesson is mechanical: append to a findings file by reading the highest ID first, not by continuing
from the last number you happen to remember.

**W5.25 closed the wrong one.** It recorded the `AutoScrollMargin` work under `LAY-30`
(`ScrollControlIntoView`), when a finding for exactly that member already existed as `LAY-32` -- which
went on reading as open. Worse, the half of `LAY-32` that W5.25 did *not* do went unnoticed for the
same reason: upstream's property rejects a negative component while `SetAutoScrollMargin` clamps it.
That asymmetry is deliberate upstream -- the property is what designer code assigns, so a negative
there is a bug worth surfacing; the method is the programmatic path and has always been forgiving --
and it is now implemented, tested, and pinned in both directions, because the obvious tidy-up is to
make the two agree.

### What the LAY-38 regression taught, and what W6.3 did not cover

A regression shipped in `LAY-38` (#181) and was caught only when issue #96 sent me back into
`ListView`: making `ListViewItem.Bounds` logical missed four hit-test sites in `ListView.cs` that
convert the mouse point to device and compare against it, so **clicking a ListView item selected
nothing at any display scale other than 1**. Review passed it and so did CI.

**Why CI missed it.** The `MF_HEADLESS_SCALE=2` gate runs the whole suite, so it catches anything a
test exercises at scale 2 — and no test drove a `ListView` click at all. The gate is only as good as
the gestures the suite performs; a control with no click test has no scale coverage no matter how many
configurations run.

**W6.3's audit was narrower than its name.** It covered the list and grid controls' public hit-tests
and rectangle members. It did not cover `Ribbon`, `MenuBase`/`MenuDropDown` or `ToolStrip`, whose
hit-tests have the same shape: a logical point from a mouse handler tested against an item rectangle
laid out in device pixels.

**And static inspection does not settle it.** `MenuBase.GetItemAtLocation (e.Location)` reads exactly
like the broken `ListView` code, and the menu path is *correct* — `MenuClickReproTests` drives a real
click through the backend at scale 2 and passes. The only reliable detector is a scale-2 click test per
control, which is what `ListView` lacked and menus had.

So the open work here is **coverage, not a known defect**: an interactive control with no scale-2
gesture test is unaudited, and the way to audit it is to write that test and watch what happens.

**Ribbon was the first one audited that way, and it was broken.** `GetItemAtLocation` tested a logical
`e.Location` against device item rectangles, so on a 2x display a ribbon click fired the wrong command
or none at all. `Ribbon` had no tests of any kind. Fixed, with the scale-2 click test that found it.

*Writing that test took three attempts, and the first two passed while proving nothing.* The first
built the click point from `item.Bounds` — those are device, so a point derived from them matches them
whatever the hit-test does. The second converted to logical but aimed at the FIRST item, whose logical
centre happens to fall inside its own device rectangle because that rectangle is tall and near the
origin. Only the third — aiming at the second item, and asserting **which** item fires — could tell a
correct hit-test from a broken one, and it needs a guard asserting the fixture is genuinely one where
the two readings disagree. A scale test that does not check its own premise is a tautology.

`ToolStrip.GetItemAt` was taken next and is **done** — but the question above was the wrong one. The
space was fine; the method was hit-testing the requested `ToolStripItem.Size` rather than the laid-out
`Bounds`, so it returned null for every point at every scale. `TSM-22` had already recorded exactly
that, in those words. See "What the `ToolStrip.GetItemAt` follow-up found".

### What W6.3 found

**The audit found bugs in code that had already been audited for exactly this.** `ListBox
.GetIndexAtLocation` carries a careful comment about mouse coordinates being logical and item
rectangles being device -- and the member it converts *for*, `GetItemRectangle`, is public and was
handing device pixels to applications the whole time. Fixing the caller and leaving the callee is the
characteristic half-fix of this class: the internal path is made right and the public contract, which
is the thing an application actually depends on, is left as it was.

**A wrong space is invisible until something forces a scale.** All five defects are exact no-ops at
scale 1, so the entire suite passed over them. The new tests force `Application.UiScale = 2` rather
than relying on the `MF_HEADLESS_SCALE=2` gate, which means they assert in every configuration instead
of in one of four -- worth copying for anything scale-dependent.

**Two probes were too far from the boundary to prove anything.** The `TreeView` tests first aimed at
the *centre* of a node and at a point well outside the control; both are past the threshold whichever
space it is measured in, so both passed with the fix neutralized. A scale bug shows up only in the
band between the right answer and the answer times the scale, and a test has to aim there.

**One fix turned out to be unobservable, and that is a finding rather than a fix.** `TreeView`'s
expander band keys off `item.Bounds.Left`, which is ~1 for every node at every depth, so the
`PlusMinus` branch can never be taken. The conversion on that line is right and stays; the claim that
it fixes anything does not, and no test pretends otherwise.

**A guard test asserted its premise away.** "Nothing changed at scale 1" set `Application.UiScale = 1`
-- which under the `MF_HEADLESS_SCALE=2` gate is still scale 2, because the two multiply -- so it
failed in the one configuration it was least about. Deleted: ~4800 existing tests written against
scale 1, none of which moved, say it far better than one test could.

### What W5.25 found

**The finding was wrong about upstream, and the correction made the item small.** `LAY-29` proposes
replacing the scrolling model -- "letting the layout engine move children rather than `ScrollWindow`"
-- which is a change to every container in the framework. But upstream's `SetDisplayRectLocation`
scrolls with `SW_SCROLLCHILDREN`: the OS moves the child windows there too, and `_displayRect` is
bookkeeping alongside that move rather than a replacement for it. Reading the upstream source the
finding cites, rather than the finding's summary of it, turned a rearchitecture into a property
getter. Worth doing before accepting any finding whose fix is "change the model".

**A gate caught a coordinate-space bug that no review had.** `ScrollControlIntoView` compared
`ClientRectangle` (device pixels) against child `Bounds` (logical), so on a 2x display it read the
viewport as twice its logical height, decided the control already fitted, and scrolled too little.
It has been that way since the method was written; only the `MF_HEADLESS_SCALE=2` configuration sees
it. The repo's own rule -- `Bounds` is logical, `ClientRectangle` is device pixels -- is written down,
and the code still mixed them.

**Two of this item's four findings were stale, in the same direction.** `LAY-30` describes
`ScrollControlIntoView` as "an empty body"; it had been implemented for the on-screen-keyboard path.
`LAY-22` says `CellPaint` "does not exist at all, so designer/user code hooking it fails to compile";
it exists in `RemainingMemberParity.cs`. In both cases the real gap was narrower and more specific
than the finding, and in both cases it was still worth fixing -- the margin, the horizontal axis and
the focus call for one; actually raising the event for the other.

**The inert-event baseline cannot see a raiser that nothing calls.** `TableLayoutPanel.CellPaint` was
never on it, because `OnCellPaint` does contain `CellPaint?.Invoke (...)` -- the scan looks for the
invoke, not for whether anything reaches it. `OnCellPaint` was itself called from nowhere. A gate that
watches for dead events has a blind spot exactly one level up, and the only thing that finds it is
asking what calls the raiser.

**A restore script silently reverted a fix.** The neutralization snapshot was taken before a CA1725
fix, so restoring after each neutralization put the warning back -- and it only surfaced two gate runs
later, in the Release build. Snapshot *after* the last edit, or re-run the compile gate straight after
restoring.

### What W5.21 found

**A pixel probe cannot see absence in a control that fills itself.** "A toggle button draws no glyph"
was written as "no ink in the glyph area" and failed at 58 pixels: an `Appearance.Button` control
paints its own face over the whole control, so every region has ink in it whatever is drawn there. The
claim had to be re-aimed at the preferred size, where the missing glyph column is a number. The same
probe shape misled in W5.19, on the check box and radio glyph counts.

**"Not the most common colour" is not a measure of fill.** A progress bar at 100% makes the fill itself
the commonest colour in the bitmap, so the ink count reported a completely full bar as EMPTY and
`quarter < full` failed with both at zero. Counting pixels of the named fill colour is the fix. A probe
that infers what it is looking for from the image is only correct while that thing is in the minority.

**A render comparison cannot detect a missing `Invalidate`.** `Changing_SizeMode_repaints` compared two
renders and passed with the fix neutralized, because rendering rebuilds the bitmap from scratch and
never consults whether the control asked to be repainted. Asserting on the `Invalidated` event is the
only way to see it -- and the control has to be on a shown form, because `Control.Invalidate` returns
early when it is not `Created`. Worth remembering for the whole class of "the setter forgot to
invalidate" findings: the test has to watch the notification, not the picture.

**One test passed for a reason it did not claim.** The wrapped-caption test asserted "more rows than
the single word", and a caption clipped to one line gains a row from its ellipsis -- enough to pass
with wrapping neutralized. A second LINE is what wrapping means, so the assertion became "about twice
the height". A relational assertion still has to be relational about the right quantity.

**Two assertions were vacuous because the themed default already matched.** The finding's own test for
`SMP-05` is `FlatAppearance.BorderSize = 0` giving a border width of 0 -- which passes on a check box
whether or not anything reads the property, because a check box has no themed border to begin with.
Neutralization surfaced it; both tests now use a width the theme would never produce.

**Neutralization found an unprotected enforcement point rather than a redundant one.** Removing the
border fallback that gives `Appearance.Button` a frame broke no test, and an unchecked toggle button
with no frame and no fill is an invisible control. Four items running have had neutralization find
something review did not; this is the first time it was a gap in the tests rather than dead code in
the fix.

**A behaviour fix pulled a layout fact with it.** Making the three button renderers wrap was not
enough: `IHaveTextAndImageAlign.Multiline` had to become `true` on the same three controls, because the
layout engine hands back a rectangle measured for a single line when it is false, and the second line
would have been drawn outside it. The renderer and the layout engine have to agree about how many
lines there are.

### What W5.20b found

**Changing a base class changes what a guard means.** `NumericUpDown.GetPreferredSizeCore` filled in
the height only when the base reported zero. Once the base became `UpDownBase : ContainerControl :
Panel`, the base started sizing itself to its children -- and this control has one, its button strip --
so it answered with a small non-zero number and the guard stopped firing. A reparenting is not a
type-level change only: every `if (the base said nothing)` in the derived class is a place the new
base's behaviour arrives.

**A test I wrote to describe the fix described the wrong behaviour.** "Value 7, type 42, expect 742"
encoded appending, because that is what my first implementation did. WinForms selects a spin box's text
when focus arrives, so typing replaces. The test failing for a *different* reason -- a second control
that started at 0 -- is what made me look at what the number should be at all.

**A clamp in the caller when the setter already clamps.** `CommitEditText` clamped to
`Minimum`/`Maximum` before assigning `Value`, whose setter does the same. Neutralizing the caller's
changed nothing. Removed; the neutralization now targets the setter. That is three items running --
W5.2b's suppression flag, W5.19's grip clip, this -- where the *neutralization* found the redundancy
rather than review.

**A pixel test pinned to the control's own scale is a test of two things.** The font test passed at
scale 1 and failed at `MF_HEADLESS_SCALE=2`, because its probe insets are in bitmap pixels and the
bitmap had doubled. Rendering at a fixed `1f` -- the pattern W5.20d already used for the same reason --
makes it a test of the font the renderer chose, which is what it is for.

### What W5.20c found

**The API-surface gate caught what the reparenting silently removed.** `DateTimePicker.PreferredHeight`
came from `TextBoxBase`; deriving from `Control` instead dropped a member upstream really has, and no
test noticed because nothing in the suite read it. Of the four gates it is the only one watching for
*removal* rather than misbehaviour — worth remembering whenever an item's shape is "reparent this".

**A stale Release assembly made a fixed gate keep failing.** After adding the member back the gap was
still reported: `ApiDiff` inspects the *Release* build, and only the Debug one had been rebuilt. When a
gate disagrees with the source in front of you, check which binary it is reading before you change the
source again.

**Neutralization found a redundant guard, again.** `StepValue`'s range check looked like the enforcement
point for spinning past `MinDate`/`MaxDate`; removing it changed nothing, because `Value`'s setter
already clamps. It stays only because assigning out of range would throw out of a mouse click — the same
shape as W5.20b's commit clamp.

### What W5.19 found

**A test that counts "pixels unlike the background" is blind to a control that fills itself.** The
check-box and radio-button tests compared ink counts between the checked and unchecked states and got
*the same number* -- 400 and 400 -- because both fill their well, so every pixel inside is already
unlike the background and the glyph is lost in the total. Counting the **glyph's own colour** is the
measurement; "something changed" was not.

**A guard the loop condition already enforces is dead code, and the neutralization is what proved it.**
`DrawSizeGrip` had a bounds clip inside its tick loop. Removing it changed no test -- including a test
written specifically for a small grip -- because `offset + 3 < reach` already guarantees every tick is
inside. Deleted, and the neutralization re-pointed at the loop condition, which is the thing actually
holding the invariant. Same lesson as W5.2b's suppression flag, found the same way.

**"Is this exact pixel ink" is too blunt for an off-by-one on a stroked edge.** The focus-rectangle test
asserted that (10,10) was not inked, and passed whether the stroke ended at 9 or at 10 -- a 1px stroke
centred on x=10 can round away from that pixel. Asserting the *rightmost inked column* as a number made
the off-by-one visible. A boundary test has to measure the boundary, not sample beside it.

**A finding's suggested test can be shorthand rather than the contract.** `GFX-02` says to assert
`Dark (SystemColors.Control) == SystemColors.ControlDark`. Upstream maps the one-argument `Dark` to
`Darker (0.5f)`, which takes the *interpolating* branch -- only `Darker (0f)` short-circuits to the exact
system value. The implementation matching upstream failed the finding's assertion. The test now asserts
what upstream actually defines, and says why in the test.

### What W5.5 found

**An event raised at the wrong end of a click is not a small difference.** `CellClick` fired from
`OnMouseDown`. Everything the suite had passed, because nothing asserted *when*. The observable
consequence is that a handler acting on a click ran with the button still down -- and, once the check box
committed through the same path, that a drag beginning on a check box toggled it. Moving the three click
events to mouse-up needed a second idea to go with it: **a click is a press and a release on the same
cell**, so the release has to remember where the press landed. Without that the fix trades one wrong
behaviour for another.

**`add { } remove { }` is worse than a missing member, and the baseline already knew.** Eight of these
sat in the inert-event baseline the whole time. The file is a list of facts, not a to-do list -- but
these eight were a to-do list, and reading them as facts is what let them sit. Worth checking whether
other baseline entries are load-bearing in the same way.

**Two names for one editor, and the type of one of them was the bug.** `EditingControl` returned
`edit_textbox`, which after the field became a `Control` was `edit_control as TextBox` -- null for a
combo editor. Four tests failed with a null reference at a point where `BeginEdit` had just returned
**true**: the editor existed, and the property that hands it out could not see it. When a field is
generalised, every *derived* accessor is a place the old assumption can survive silently.

**A commit path that parses text cannot commit a non-string.** A combo box's `SelectedValue` is the id
behind the name, and routing it through the text parser stored `"3"` instead of `3` -- an unbound lookup
column declares no `ValueType` for the conversion to aim at. The test failure read `Expected: 3, Actual:
3`, which is xunit saying "same rendering, different type" and is worth recognising on sight.

**A key-up handler cannot repeat.** Holding an arrow key moved one row and stopped, which reads as a
sluggish grid rather than as a missing feature -- the kind of defect users report as "it feels wrong"
and nobody files. The move to key-down was mechanical; noticing that it *mattered* required reading the
finding rather than the code.

**Home and End did what Ctrl+Home and Ctrl+End mean.** Not "unimplemented" -- implemented, and bound to
the wrong action, with the modified versions unbound. A test asserting "Home does something" would have
passed. The finding named the specific behaviour, which is why it was catchable at all.

### What W5.4 found

**A property setter that raises its event but does not do the thing is the original defect wearing a
new coat.** `AutoSizeColumnsMode = Fill` raised `AutoSizeColumnsModeChanged` and invalidated. The Fill
distribution was written and correct, hooked into the layout pass — and the layout pass never ran,
because nothing about *setting the mode* triggers a layout. The two Fill tests failed with columns at
exactly their starting widths. A stored-only property was replaced by a property that was, from the
outside, still stored-only until the window was next resized.

**Two enforcement points for one invariant make both unprovable.** `MinimumWidth` was clamped in the
Fill distribution *and* in the column's layout write. Neutralizing the first changed nothing, because
the second caught it. Not a bug — a redundancy — but a redundancy no test can distinguish from a working
implementation, which means the next person cannot tell which one is load-bearing. One was removed.

**Raise the event after the state it describes has moved.** `ColumnHeaderMouseClick` was raised at the
top of the header-click branch, before the sort. The standard handler reads `grid.SortOrder` in it, and
saw the *previous* order. `DGV-16` had written this trap down in advance; the first version walked into
it anyway. The test that caught it asserts what the handler *saw*, not what the grid ended up holding —
the second is fine while the first is wrong.

**`DataView.Sort` writes `"[name]"`.** Not a defect anywhere; a test asserting the exact string was
wrong and now asserts containment. Recorded because the next test against a `DataView` sort expression
will meet the same brackets.

**A pixel test can encode the rasteriser it was written on.** The alignment test asserted "ink in the
right third, none in the left", with ink as three channel thresholds. It passed on macOS and Linux and
found **nothing at all** in the right third on Windows CI — the first platform divergence in this
project that was neither font *height* nor window chrome, the two the gates already knob. Two changes,
both of which the earlier pixel work should have reached on its own:
*Ink is now defined relative to the cell's own background*, sampled from the cell, rather than by an
absolute darkness threshold. How a rasteriser antialiases a glyph is its own business; that text differs
from what it sits on is portable. (The probe also had to exclude the cell's borders, which are "not the
background" too, and being at both edges dragged the mean to the middle wherever the text actually sat.)
*And the assertion is now relational* — the ink's centre moves right by at least a third of the cell —
with both centres in the failure message, so the next platform difference explains itself instead of
needing a CI archaeology session.
**A second test at a different boundary was the more valuable fix**, and the first version of it proved
nothing: it asserted `CellPainting.CellStyle`, which already carried `InheritedStyle` before this work,
so it passed against unmodified code. Re-pointed at the `ControlStyle` that `RenderCell` actually
receives — the link this item created — it fails when the cascade is neutralized. Two tests, two links:
one could pass while the renderer ignored what it was handed, the other while nothing reached it.

**Flipping a default can be a second bug's cover.** `DGV-13` changed `SelectionMode`'s default from
`FullRowSelect` to `RowHeaderSelect`. `GetClipboardContent ()` tested
`SelectionMode == DataGridViewSelectionMode.FullRowSelect` by name rather than asking
`SelectionIsRowBased` — the predicate `ReplaceSelectionWithCurrentCell` and `IsCellSelected` already use
for "any row-based mode" — so a row selected under the old default copied fine and a row selected under
the new one copied nothing. The finding never mentions clipboard copying; nothing about `DGV-13`'s own
description would have caught it. It surfaced only because the full suite, not just the new test, was
run against the flipped default and two existing tests (`Ctrl_C_copies_the_selection`,
`GetClipboardContent_ReturnsSelectedCells_AsTextCsvAndHtml`) went from green to red. A default-value
change earns a full-suite run, not just its own new assertions — the blast radius of "what reads this
value" is bigger than the finding that named it.

### What W5.3 found

**A finding can name one defect and be caused by two.** `DGV-03` says `Rows.Add ()` returns `Count`
instead of the new index, and it does. Fixing exactly that made the finding's own test — `int i =
grid.Rows.Add (); grid.Rows[i].Cells[0].Value = …` — throw on the *next* token: the new row had no
cells. The idiom the finding describes as broken was broken twice over, and the second break was
invisible until the first was fixed. The test for a finding should be the idiom it names, run to
completion, not the sub-assertion that first surfaced it.

**"Ignored" is not the only way a property can be inert — it can be actively dangerous when honoured.**
`DataMember` was stored and never followed. Following it exposed a parity test that bound `List<string>`
with `DataMember = "Length"`: a member that resolves to an `int`, which no version of WinForms would
accept. The test was asserting a contract ("both halves are set") on an input the contract never
covered, and passed for as long as the property did nothing. When a stored-only property starts working,
every existing test that touches it is suspect — not because the tests were wrong, but because they were
never actually exercised.

**A guard can have two branches and one test.** The "regenerate columns only when the schema changed"
guard exists in both the `ITypedList` branch and the reflection branch. `BindingList<T>` is not
`ITypedList`, so the same-schema `Reset` test covered only the reflection branch, and neutralizing the
descriptor branch changed nothing. The descriptor branch's real-world case is a `DataView` re-sort —
which raises `Reset` over an unchanged schema, and which used to reset every header rename on the
grid. It needed its own test, and now has one.

**The source telling the grid is not the grid editing.** `ItemChanged` refreshes a row's cells from the
item. Doing that through `Cell.Value`'s setter — real since W5.2a — would raise `CellValueChanged` and
run the write-back, sending the value straight back to the object it came from. The refresh writes
through the same suppression W5.2a's `EndEdit` uses. A choke point that makes a property *do*
something creates a class of caller that must deliberately bypass it, and each such caller is a place
the reasoning has to be written down.

### What W5.2b found

**A fourth way a baseline reads clean over dead code: a property read only by a state reporter and a
clone.** The three already catalogued here are a ring of stub properties reading each other (W5.10), a
property read only by inert code (W5.11), and an `OnXxx` raiser containing `Xxx?.Invoke` counting as a
raise site even when nothing calls the raiser (W5.16). `Row.Selected` is a new one: it was read, by
`DataGridViewRow.State`/`InheritedState` and by `Clone`, so the stored-only detector never flagged it --
and both of those readers only ever hand the value straight back out. A property whose every reader
returns it verbatim is doing nothing, and looks exactly like a property that is doing something. Making
the two `Selected` properties real dropped the auto-property denominator from 1199 to 1197 and left the
stored-only count at 739, which is the tell: they were never counted in the first place.

**A test can pass because the state it asserts was already true.** The Ctrl-click test originally
clicked row 1 and then Ctrl-clicked row 1, asserting the current row was row 1 -- which the *first*
click had already made true, so neutralizing "the current cell follows a modified click" changed
nothing. Re-pointing the second click at a different row made it discriminate. This is the same shape as
W5.9's `GetNodeAt` test, where three nodes made the probed index the fixed point of the very reversal
the test was meant to catch.

**Reading a property should not raise events, and here the obvious implementation would have.**
`SelectedColumns` is typed as `DataGridViewColumnCollection`, whose `InsertItem` re-owns the column and
raises `ColumnAdded` plus `OnColumnsChanged`. Populating one to return it would have meant every read of
`SelectedColumns` firing the grid's column-added event and forcing a relayout -- a property getter with
side effects on the control it belongs to. The projection constructor exists only to make the getter
inert, and there is a test asserting the read raises nothing.

**Two of my own tests turned out to be guards, and saying so was the only honest option.** Both
"announces once" tests survived a neutralization that rewrote the batch bodies to assign the `Selected`
properties element by element -- the naive implementation they were written to rule out. Tracing showed
two notifications reaching a one-handler invocation list while the counter still read one, which I could
not explain, so the claim was removed rather than asserted. The direct `SetSelectedCore` writes are kept
because they make the single notification structural, but the plan, the code comment and the tests all
now say that no test pins the difference. An unproven claim in a comment is worse than an admitted gap:
the next person reads it as verified.

### What W5.20a found

**A test can hide a 120× error by choosing a delta no device sends.**
`ScrollBarTests.Wheel_raises_Scroll_with_the_proposed_value_before_Value_updates` sent
`Delta = -3` and asserted the value moved by 3 — which made `Value - Delta * SmallChange` read as
"three units" rather than "one hundred and twenty notches' worth". Backends send ±120 per notch, so
the real behaviour was a full-range jump per notch. The test now sends a partial notch (nothing
moves), then completes it (one `SmallChange`). When a test picks its own input magnitudes, the choice
can encode the bug.

**Clamping in the shared commit path broke a documented assignment.** My first version put
`EffectiveMaximum` into `UpdateFromValue`, which every path funnels through — so `Value = Maximum`,
legal upstream and validated by the setter, silently landed on `Maximum - LargeChange + 1`. Upstream
clamps only *user-driven* scrolls; the property setter throws outside `[Minimum, Maximum]` and
otherwise assigns what it was given. The finding says exactly this ("leave the property setter's
`ArgumentOutOfRangeException` bound at `Maximum`, as upstream does") and I read past it. The regression
test for it is now in the file.

**Two of the findings were already fixed, and neither file said so.** `SMP-47` (Scroll raised only for
`ThumbTrack`) is closed: `PerformScroll` raises `Scroll` for arrows, track, thumb and wheel, and its
comment names the migrated app whose scrollbar regressed. That is the third time in this phase a
finding has outlived its defect — see also `TSM-02` and `TSM-14`. Reading the code before the finding
costs a minute and has now saved several afternoons.

**A window-scoped fix for the previous item, found by the chrome gate.** The menu-key routing added in
the last item claimed keys whenever `Application.ActiveMenu` was non-null, including for a menu on
*another* window; under `MF_FORCE_CUSTOM_CHROME` a leftover active menu ate Escape and
`Form.CancelButton` stopped working. It now requires the active menu to belong to the window handling
the key, which is correct on its own terms and makes stale global state harmless. Debug and the
default Release run both passed — only the chrome configuration ordered the tests in the way that
exposed it.

### What the menu-mode keyboard work found (the rest of TSM-13)

**The plan said this was done, and it half was.** W1.3 claims `TSM-02` and `TSM-13`, and Phase 1 is
marked Done. `TSM-02` really is closed — `MenuShortcutTests.cs` has eleven passing tests over
`ShortcutKeys`, the legacy `Shortcut`, disabled items and access keys — but `TSM-13` covers two
mechanisms: *accelerators* (Alt+letter reaching an item) and *menu mode* (F10/Alt to enter the bar,
arrows to walk it, Enter, Escape). Only the first existed; there was no `OnKeyDown` in `MenuBase`,
`Menu`, `MenuDropDown` or `ContextMenu`, and no `Keys.Escape` handling anywhere in them. The findings
file meanwhile still counted `TSM-02` as an open P0, so the two documents were wrong in opposite
directions about the same item. Checking the tests settled it in a minute; believing either document
would have cost an afternoon.

**Selection and "open" are one state in this framework, and that changes what F10 can mean.**
`MenuItem.Selected`'s setter calls `ShowDropDown`/`HideDropDown` directly — that is how click-to-open
works — so moving the selection onto a menu opens it. Upstream separates highlighted-on-the-bar from
dropped-down, and its F10 highlights without opening. Splitting them here would change every mouse
path into a menu, so the navigation lives with the coupling and the test says so rather than asserting
something false.

**My own change made a dormant leak dangerous.** `Application.ActiveMenu` was only ever cleared by
`MenuBase.Deactivate`, which closing a form does not run, so a closed form left its menu bar as the
active menu. That was harmless while nothing consulted it — and the moment keys started routing
through it, a keystroke went to a menu on a window that no longer exists. `Form.RaiseFormClosed`
clears it now. Worth generalising: adding a consumer to stale global state turns a latent leak into a
live bug, so the audit of "who clears this?" belongs in the same change as the new reader.

**One behaviour is implemented but deliberately untested**, and recorded as such in
`toolstrip.md`: Right opening a nested submenu and Escape closing one level back out of it. Opening a
second popup while the first is up tears the whole menu down through
`Application.ScheduleClosePopupsOnDeactivate`, because on this backend the newly shown popup does not
report itself active. A test would measure the backend, and pinning the teardown as expected would be
worse than having no test at all — it would fix in place behaviour nobody wants.

### What W5.16 found

**A third way a baseline reads clean over broken code.** None of the five events this item started
raising — `MenuActivate`, `MenuDeactivate`, `ContextMenu.Popup`, `Collapse`, `MenuItem.Popup` — were
ever in `UnraisedEventBaseline.txt`, so the file did not move when they came to life. Each already had
an `OnXxx` raiser containing `Xxx?.Invoke`, and that is a raise site as far as the scanner is
concerned; that nothing ever *called* the raiser is invisible to it. `TSM-30`'s own title says as much
("declared-never-raised events with existing triggers"). With W5.10's ring of stub properties reading
each other and W5.11's property read only by inert code, that is three distinct mechanisms, one per
gate. Each gate is a floor.

**The compensation in the callers was the evidence.** Every internal caller of
`ContextMenu.Show (Control, Point)` passed `PointToScreen (e.Location)`. That is not a bug in the
callers — it is what a screen-space API requires — and it is exactly why the finding could state with
confidence that the *public* API was wrong: application code following the WinForms documentation
passes a client point and gets no conversion. Fixing the overload meant fixing the callers in the same
change, and a caller that "helpfully" pre-converts is worth reading as a sign the API underneath it
disagrees with its own documentation.

**A test I labelled a guard turned out to be proof.** The plain-`ToolStrip` regression test looked like
it could not fail — that path already worked — but the notifications moved *out* of the facade it used,
so neutralizing the new plumbing breaks it too. The lesson runs the other way from the usual one: a
label claiming a test cannot discriminate deserves the same neutralize-and-rerun check as a claim that
it can. The genuine guard here is the screen-space `Show (Point)` overload, which is untouched and
would have broken had the conversion gone into `ShowCore`.

### What W5.15 found

**Removing the shadow was not enough, and the audit already knew why.** `MenuDropDown.OnMouseClick`
— the path a context menu or any sub-menu actually takes — gates on `clicked_item != null &&
!clicked_item.HasItems` and never checked `Enabled`. `MenuBase.OnMouseClick` does check it, which is
the gate `TSM-01` names, but drop-downs never reach it, so the P0 as written (delete the `new`
property) fixes the menu bar and leaves the case where disabled items overwhelmingly live. The test
found this by driving a click through a real `MenuDropDown` — and `TSM-14` turns out to describe it
exactly, "compounds TSM-01" in its own words. Two lessons: a Cat A finding can be a *precondition* for
another one's fix rather than an independent item, and the plan's per-item finding list is a starting
point rather than a boundary. This item's text named five findings; the work closed six.

**Three ways the same pixel test can pass by measuring nothing.** The checked-glyph test needed all
three fixed before it meant anything: `PaintSurface` sizes its bitmap from `control.Scaling`, which is
**0** for an unhosted control, so the default surface is 0×0; adding a `MenuDropDown` to a `Form`
resets its `Width` to 0, so `RenderOnForm` produces the same nothing; and counting "pixels that differ
from `Theme.ControlLowColor`" counts the row background too, because that is not the colour the
renderer paints there. It counts the glyph's own colour now, on an unparented control, at an explicit
scale — with an assertion that the bitmap is the size it should be, so the next person cannot be fooled
the same way.

**A test can pass against the shadow for exactly the reason the finding warns about.** `TSM-01` notes
that the existing parity test asserts `CanSelect`, "which reads the shadow, so it passes for the wrong
reason" — and the first version of my click test did the same thing, because `CanSelect` is declared on
`ToolStripItem` and binds to whichever `Enabled` is in scope there. Then the fixed version passed for a
*different* wrong reason: it clicked one item twice, and `MenuBase.TryBeginLeafClick` de-duplicates
repeat clicks on the same item within 50ms, so the second click never reached the gate. Two items, one
click each.

**Item layout is a paint-time operation, which changes what "reaches layout" means.** Strips lay their
items out in `OnPaint` (`MenuBase.OnPaint`), not in a layout pass, so `PerformLayout` on a strip leaves
every item with empty bounds and `Invalidate` is the call that actually re-lays out. Any test that
wants item bounds has to render, and `TSM-31`'s fix is the invalidation rather than the layout call —
the `PerformLayout` in `InvalidateItemLayout` is there for the strip's *own* size, which is measured
from its items.

### What W5.14 found

**Two upstream defaults that look like one.** `SelectionColor` and `SelectionBackColor` both start out
"unset", and upstream answers them differently: the foreground reads `CFE_AUTOCOLOR` and reports the
control's `ForeColor` — a real colour, because one is always painted — while the background reads
`CFE_AUTOBACKCOLOR` and reports `Color.Empty`, because "no background" and "a background that happens
to match the control" are different things to a caller about to save the document. The existing
`Ctor_Default` test asserted `Empty` for both, which is what a stub returns; getting this right meant
inverting half of it and leaving the other half alone.

**A test that pinned the stub, and a test that pinned the accident.** `Rtf_SetNullOrEmpty_EmptiesText`
asserted `Rtf == string.Empty` after clearing — true only while the getter returned its own stored
string; an empty document is still a document. And two of the four `Find` range-validation rows passed
before the fix, but incidentally: the old code threw out of `Substring`, not out of an argument check.
Both are recorded in the tests rather than quietly satisfied.

**Reading only depth-1 text hid a second bug behind the first.** The old reader kept text at group
depth 1, which dropped the document's real content — and also dropped `colortbl` and `info` contents,
so the "metadata is skipped" test passes against it. The new reader keeps text at every depth and skips
metadata destinations *by name*, which is the only version of that behaviour that is deliberate rather
than a side effect.

**The seams from W5.13 paid for themselves a second time.** `InsertTypedCharacter` and `DeleteAtCaret`,
added so `MaskedTextBox` could filter input, are exactly the hooks per-run formatting needs to keep its
runs over the right characters as the user types and deletes. Nothing new was needed in `TextBox` for
this item at all — and the honest limit falls out of the same fact: `Undo` and `Paste` do not pass
through a seam, so they are the paths where a run can end up over the wrong text.

### What W5.12 found

**Comparing the visible result was not enough to tell two implementations apart.** The test that a
`RichTextBox` replaces a selection the same way through either reference passed against the `new`
shadow: the shadow rebuilt the same string and then patched the caret to the same index, so the text
and the caret agreed while everything around them differed. It only discriminated once it compared
`CanUndo` and `Modified` — the state the `Text` setter quietly resets. When a finding is about *how*
something is done rather than *what* comes out, the assertion has to name the side effects.

**The finding's suggested assertion was wrong in a way worth keeping.** `TXT-02` says to assert
`Modified` is unchanged by an append. Upstream appends with `EM_REPLACESEL`, which sets the modify
flag, so `Modified` becomes true — the defect was the *direction*: the `Text` setter forced it false,
which is worse than either, because an append made a dirty document look saved. The test now asserts
upstream's behaviour and says why beside it.

**Two of the twelve tests could not fail against the old code for the same reason.** `AppendText`
ignoring `ReadOnly` and `MaxLength` was already true, because the document's `Text` setter never
checked either. The old path was right about the limits and wrong about everything else, which is a
useful reminder that "the old code was broken" is a claim per behaviour, not per method.

### What W5.11 found

**The baseline's own warning had gone stale, and that is the warning.** `StoredOnlyPropertyBaseline.txt`
carries a note saying absence from the file does not mean a property works, and cited `ListView.View`,
`TextBox.WordWrap` and `TextBox.AcceptsReturn` as "absent and all broken". All three have since been
fixed — by W5.6, by this item, and by Phase 1 — so the caveat was illustrated entirely by counter-
examples. It now cites `ComboBox.IntegralHeight` and `ComboBox.DropDownHeight`, which are absent
because their only readers are `ToolStripComboBox`'s pass-through wrappers: inert code reading inert
code. Coming after W5.10, where the same gate under-reported because stub properties read *each
other*, that is two distinct mechanisms by which this baseline reads clean over broken members. It is
a floor.

**The finding's impact line was backwards, and the mechanism explains why.** `TXT-26` says boxes
designed as `ScrollBars.None` grow a scrollbar. The opposite was true: no `TextBox` could ever show
one. `ScrollControl.ScrollBars` already shows and hides both bars correctly, and
`public new ScrollBars ScrollBars { get; set; }` on `TextBox` shadowed it — so `UpdateScrollBars`
dutifully set `Enabled` on a bar that nothing had ever made `Visible`. Deleting the shadow *is* the
fix, and it is the same defect class as the `ToolStrip` shadows in `TSM-01`. Worth remembering when
reading a Cat C finding: "stored-only" sometimes means "a working implementation is being hidden",
which is a two-line fix rather than a feature.

**A test helper named after an existing member, twice in three items.** `Deselectable.Deselect` hid
`Control.Deselect` and failed the Release build on CS0108 — exactly what `ClickableTree.Click` did in
W5.9. Warnings-as-errors in Release catches it every time and Debug never does, which is the
four-configuration rule earning its keep for the third item running.

### What W5.10 found

**The stored-only scanner cannot see properties that read each other.** `LST-07` says
`SelectionStart`, `SelectionLength`, `MaxLength` and the `AutoComplete*` family were stored and
consumed by nothing, and it is right — but none of them appear in `StoredOnlyPropertyBaseline.txt`,
because `SelectedText`'s getter read `SelectionStart` and `SelectionLength`. A ring of stub properties
citing one another looks consumed to a scanner that asks only "is this getter called anywhere". The
baseline is a floor, not a ceiling: it under-reports exactly the clusters that were stubbed together.

**Two independent guards, and either one alone makes the test pass.** The test that a `DropDownList`
combo refuses typed text survived neutralizing the style check in `OnKeyPress` — because the sync from
the edit region back to `Control.Text` checks the style too. It only failed when both were removed.
Neutralize-and-rerun proves a test discriminates against *the change you made*; where a behaviour is
defended twice, removing one defence proves nothing, and the test needs both removed to be honest.

**A short-circuit that could never be true.** Enter committed nothing, because the commit was guarded
with `edit.Text != base.Text` — and the edit region's own `TextChanged` has already written
`base.Text` by then, so the two are always equal at that point. It read like an obvious cheap
early-out and was dead code. The test caught it immediately; a reviewer would very likely not have.

### What W5.9 found

**Three tests passed against the broken code before they were made to discriminate.** The hit-test
test is the instructive one. `GetNodeAt` is now a delegation to `GetItemAtLocation`, so asserting the
two agree is true by construction — it proves nothing. Asserting the *named node* instead still passed
against the restored old algorithm, twice over: with three nodes, index 1 is the fixed point of the
sibling reversal the old traversal performed, and at row 1 the stored height (20) and the measured row
height (~24) have not yet diverged far enough to land in a different row. Both defects only show
further down a longer list, so the test now uses four nodes and probes the last row — and it fails
against the old algorithm. A defect that is off-by-a-reversal or off-by-a-scale-factor is invisible at
the point where the two agree; choose the probe where they cannot.

**`ItemHeight` never reached layout, and the property was not the reason.** Making the setter
invalidate and having the tree scale the value changed nothing, because `StackLayoutEngine` asks each
*node* for its preferred size — the tree's own `ItemHeight` is never consulted during layout. The fix
belongs in `TreeNode.GetPreferredSize`, which had to reach back up to its tree. Wiring a stored-only
property means finding the code that actually decides the outcome, not the code that shares its name.

**A helper that wrote files only after every edit succeeded silently dropped four good edits.** A late
anchor miss aborted the batch after reporting the earlier pairs "ok", leaving `OnBeforeCollapse`
referenced and undefined. Validate every anchor before mutating anything.

### What W5.13 found

**The engine was already in the BCL, and the finding said so.** `System.ComponentModel.MaskedTextProvider`
is cross-platform and is what upstream uses, so nothing here reimplements mask semantics — the work was
wiring, and the one design decision worth recording is that typing uses `Replace` rather than
`InsertAt`: a mask has fixed positions, so a character overwrites the one at the caret instead of
pushing the field along, and Backspace blanks a position back to its prompt instead of shortening it.

**`TextBox.OnKeyPress` could not be subclassed for this.** It raises the `KeyPress` event and inserts
into the document in the same method, so a derived box cannot filter the character without also
suppressing the event (there is no way to reach `Control.OnKeyPress` past the override). Two narrow
virtual seams — `InsertTypedCharacter` and `DeleteAtCaret` — fix that, and they are the natural place
for any future filtering box. Worth knowing before attempting `TXT-05`'s `CharacterCasing`, which
wants the same seam.

**`Text` and the displayed string are deliberately different here**, which has no Win32 analogue: in
WinForms the edit control's text *is* the display. Here the document is the display buffer holding
`ToDisplayString ()` (prompts and literals), while `Text` reports the provider's value under
`TextMaskFormat`. Conflating them is what made a masked box look like a plain `TextBox` — no prompt
characters ever appeared — so `DisplayedMaskText` exists to say which one a caller means.

**A defensive flag that nothing read failed the Release build**, which is the correct outcome: I added
an `applying_mask` guard against an echo that cannot happen (input arrives through the seam, not
through a document-changed callback). Warnings-as-errors in Release caught CS0414. That is the same
defect class this whole plan is about, and it is worth noting that the Debug gate passed it — the
four-configuration rule earned its keep again.

### What W5.7 and W5.8 found

**The double-report trap, predicted and avoided.** W5.6's lesson was that when one member becomes the
notification choke point, every caller that used to notify on its behalf has to stop. Here the input
handlers are wrapped wholesale in `ChangeSelection`, and several of their branches assign
`SelectedIndex`, which raises on its own. A batch depth — the same shape W5.6 ended up with — makes a
click report exactly once, and asserting *once* rather than *at least once* is what would catch a
regression.

**Wrapping handlers beat wrapping call sites.** `LST-04` lists eight silent mutation points across the
mouse and keyboard paths, and that list is the shape of the problem rather than a complete inventory.
`ChangeSelection` compares a snapshot of the selected set instead, so wrapping the two handler bodies
covers every branch including ones nobody enumerated, and a branch that changes nothing announces
nothing.

**A test found a latent duplicate.** `AddSelectedIndex` added unconditionally, so selecting an
already-selected index put it in the list twice — `SelectedIndices` reported the same row twice, and
the Shift+arrow extension paths (which call it per keystroke) accumulated duplicates. Nothing had
noticed because nothing compared the selected set before and after; the "re-selecting announces
nothing" test failed on the duplicate, not on the announcement.

**Two pixel assertions, two wrong reasons.** The glyph test first compared "ink in the glyph column"
between a `CheckedListBox` and a plain `ListBox` — and the plain one has ink there twice over: item
text is inset by 4px, and the control paints a 1px border down its left edge. An empty item label
removes the first, sampling the glyph's own rectangle removes the second. That is three items running
where a region-based pixel assertion needed narrowing; the pattern is that "ink exists here" is almost
never the claim worth making.

**A framework constraint worth knowing:** `RenderManager.SetRenderer<T>` requires the renderer's
declared `Type` to equal `T`, so a renderer deriving from another renderer must override `Type` to
register for the subclass. Without it, registration throws inside the static constructor, which
surfaces as `TypeInitializationException` from whatever control happens to paint first — nowhere near
the actual mistake.

### What W5.6 found

**Making the item the choke point double-reported the selection.** `ListViewItem.Selected` now
announces through the parent (LST-17), but `ListView.SelectedItem`'s setter still raised
`ItemSelectionChanged`/`SelectedIndexChanged` itself — so every change was reported twice, and
`KryptonPortParityTests.ListView_ItemSelectionChanged_ReportsDeselectionThenSelection` caught it
immediately. The fix is a batch depth: the per-item setters report their own
`ItemSelectionChanged` as they happen, and one settled `SelectedIndexChanged` follows the pair. Worth
knowing generally — when a property becomes the single notification point, every caller that used to
notify on its behalf has to stop.

**The pixel test passed against a deliberately broken renderer, and so did two others.** "Some ink in
the second column's x-range" is satisfied by TILE rendering, because tiles are laid across the full
width — the exact shape of vacuous assertion W5.24 catalogued, found again by the same neutralize-and-
rerun pass. It now uses two things only Details can produce: the header band's fill colour (tiles draw
no header, so that pixel stays the list's background) and subitem ink past a deliberately wide first
column, with only two items so no tile reaches that far. `CountPerPage` was likewise asserted against a
floor that the old `Height / 70` also cleared, and is now anchored to the row height the control lays
out with.

**`TopItem` was the half of LST-19 that a scrollbar does not fix by itself.** It returned `Items[0]`
unconditionally, so a scrolled list still claimed its first item was on top — and the test noticed
while everything else about scrolling already worked. Its setter also had to change meaning: upstream
scrolls the assigned item TO THE TOP, where this called `EnsureVisible`, which only guarantees
visibility.

### What phase 4 found

**Upstream's design was simpler than the plan's fix.** W4.1 says to give the manager a `SetList` and
have `ResolveList` call it. Upstream does something better: the manager wraps the `BindingSource`
ITSELF (`new CurrencyManager(this)`), whose identity survives every re-resolve — so there is nothing
to swap, and the one `ListChanged` subscription carries re-resolves, self-mutations and forwarded
inner-list changes alike. Adopting that deleted the whole compensation layer the old design needed:
`ForgetCurrencyManager`, `SyncPosition`, `PushPositionToCurrencyManager`, and a second
independently-stored position that could disagree with the manager's (BND-21 closed itself).

**A listening manager made a latent bug live.** `RemoveCurrent` and `AddNew` raised `ListChanged`
directly instead of through `NotifySelfMutation`, so a mutation on an `IBindingList` inner list was
announced twice — harmless while nothing counted the announcements, position-corrupting the moment the
manager did. Adding a subscriber to an event is not a read-only change; it promotes every double-raise
from waste to defect.

**Subscription order is architecture.** The manager subscribes to the BindingSource in its
constructor, before any control can — so its `PositionChanged` for a first-item add reaches a bound
`ListBox` before that control has reloaded its items, and the naive `SelectedIndex = position` threw
on an empty collection. Upstream reloads and positions in ONE handler; the fix here mirrors that (the
control drops an early out-of-range selection, and the reload re-applies the manager's position).
Found by two existing tests, which is what the suite is for.

**Four tests pinned divergences, and one pinned it in its name.**
`A_half_typed_number_does_not_throw_and_leaves_the_source_alone` asserted `Assert.Equal (0,
person.Age)` — the source demonstrably NOT left alone. Like W5.17's font test, the intent was right
and the assertion asserted the bug; both were kept with the assertion corrected. The other three:
out-of-range `Position` "parks" (upstream clamps), `CurrencyManager.List` is the inner list (upstream:
the BindingSource), a bogus `DataMember` is ignored (upstream throws).

**All 26 new tests were proven against neutralized fixes, in six batches** — every one failed in at
least one batch. Three needed a batch of their own (suspend, `ReadValue`'s force semantics,
`PropertyManager.Position`), which is the W5.24 lesson holding: a test that has never failed proves
nothing yet.

**Deliberately not done here** (out of the phase's item list, still open in the findings file):
`BND-15` (BindingContext re-homing on parenting — wide blast radius, every unparented binding),
`BND-17`, `BND-22`, `BND-25`–`BND-27`, `BND-29`, `BND-32`–`BND-35`.

### What W6.5 found

**Two of the nine listed corrections were stale, in opposite directions.** The `ProcessCmdKey` entry had
been overtaken by Phase 1 and had to be inverted (above). The `ListBox` entry claimed `PreferredHeight`
"exists now" — it does (`MidSizeControlParity.Three.cs:238`), but a source grep said otherwise for a
while, because the grep was truncated with `| head -5`. What settled it was the generated
`Majorsilence.Forms.xml` doc file: `grep 'P:Majorsilence.Forms.ListBox.PreferredHeight'` over the
built surface is a direct answer where a source grep is an inference. **Check member existence against
the built surface, and never truncate a grep you intend to draw a negative conclusion from** — the API
gap gate reporting zero was the second signal that the negative was wrong, and it was right.

**The audit's own list missed three rows worse than any on it.** All three claim a control works when
what exists is storage:

- `DateTimePicker`/`MonthCalendar` were listed as "Partial", missing bolded dates and `DropDownAlign` —
  theming gaps on a control whose `OnPaint` draws **one line of text** (`MonthCalendar.cs:255-263`).
  There is no date-picking UI in the framework at all, and the matrix implied there was. (`MonthCalendar`
  draws and picks a date as of 2026-09-04 and `DateTimePicker` as of 2026-09-14, both W5.20c.)
- `ErrorProvider` sat in an "Implemented ... minor gaps only" row while nothing it is given ever
  renders (`SMP-51`).
- `MaskedTextBox` was "Partial", missing `InsertKeyMode` and friends, while the mask is not enforced and
  `MaskCompleted` is `=> true` (`TXT-03`).

The pattern is that the audit generated its corrections from the *findings* it had filed, and the matrix
row for a control it had filed a P0 against went unchecked. Reading the matrix top to bottom against the
findings — the opposite direction — is what surfaced these. Worth doing that direction once more when
Phases 4 and 5 land.

**Rows were added, not just edited.** Three things had no row anywhere: the keyboard chain (now that it
works, it is a capability worth stating), `ToolStrip.OverflowButton` returning null, and
`ToolStripManager.Merge` returning `false`. A matrix that only ever edits existing rows drifts toward
describing the layer's original shape rather than its current one.

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

### What W5.24 found

**The audit's most encouraging non-finding held up: the engines really are a faithful port.** All four
fixes are wiring, and none of them needed a line of layout arithmetic written. `Panel` went from a
34-line hand-rolled child-bounds scan to a two-line delegation, and `FlowLayoutPanel` and
`TableLayoutPanel` were fixed by deleting that scan rather than by anything done to them.

**A suggested test in the finding was wrong, and being wrong was informative.** `LAY-25` says a padded
panel with one 50x50 child at `(0, 0)` should report `(70, 70)`. It reports 60, correctly: the engine
subtracts the container's padding offset from the anchored preferred size, and upstream's `DefaultLayout`
does the same, because an anchored child's bounds already start inside the padding. 70 is right for a
child at the display-rectangle origin `(10, 10)`, which is where layout actually puts one. Transcribing
the finding's number would have produced a "fix" that made the assertion pass and the behaviour wrong.

**Four of the fourteen tests first passed against a deliberately broken build.** After writing them I
short-circuited each fix in turn to check the tests could see it. Ten failed; four did not — each because
it asserted an absolute floor that the *unfixed* behaviour also cleared: a wrapping panel's height "at
least 60" is satisfied by the panel's own default height, and a check box being "wider than a button"
holds because their default sizes already differ. Rewritten as relationships (narrow *versus* wide,
the glyph column as a *difference*, a bigger caption font *versus* a smaller one) they now fail as they
should. The remaining one — `MaximumSize` clamping `PreferredSize` — is a shape guard rather than a
proof, because any core override picks the clamping up; it is labelled as such in the test.
**A test written after the fix is not a test until it has been shown to fail without it**, and an
absolute threshold is the shape that hides this.

**`ScaleControl`'s min/max ordering is load-bearing, not decoration.** Lift the constraints, scale the
bounds, put the scaled constraints back. A control sitting at its `MinimumSize` — a designer-set button
often is — cannot otherwise grow: the scaled bounds are computed and then clamped straight back to the
value they were meant to outgrow. That is the kind of detail that reads like ceremony in upstream's
source until a test asserts it.

### What W3.6 found

**The one-shot flag has to be armed by the property, not consumed by the first layout.** The first
version scaled on the first layout and cleared a `_performed` flag there. It silently did nothing, and
the reason is the order `InitializeComponent` uses: `Controls.Add` *itself* triggers a layout, so the
flag was spent before `AutoScaleDimensions` had been assigned — the ratio arrived one layout too late,
every time. Upstream arms on the assignment instead (`ContainerControl`'s `stateScalingNeededOnLayout`),
which is not an implementation detail but the whole reason the mechanism works against designer code.
Two of the eleven tests caught it; the one that passed did so for the wrong reason (a font assignment
re-armed the flag), which is a good argument for writing the container tests in both orders.

**`Dpi` mode is inert on purpose, and that is an RC-8 consequence rather than a shortcut.** Upstream's
logical coordinates *are* device pixels, so scaling by `dpi/96` is what makes a form the right physical
size. Here `Bounds` are logical and the backend already applies the display's factor on the way to the
screen — `Control.DeviceDpi` is derived from that same factor — so applying the ratio again scales every
form twice on any HiDPI display. `CurrentAutoScaleDimensions` still reports the device DPI honestly; only
the scaling declines to act, in one place (`AutoScaleEngine.TryGetFactor`) with the reason next to it, and
a test pins both halves so it cannot be "fixed" by accident.

**A form has to scale before its window opens, not during show bookkeeping.** The finding's own fix note
said to call it from `EnsureShownBookkeeping`, which runs *after* `Backend.Show ()` — and a post-show
`Form.Size` write is a known backend gap, so the children would have scaled inside a window that stayed
its original size and clipped them. Hence a new `WindowBase.PrepareForFirstShow` hook ahead of
`SetWindowStartupLocation` on all three show paths. Worth knowing for any other item that needs to change
window geometry: there was no pre-show seam before this.

**The absolute number matters here, unusually.** Most metrics only need to be self-consistent, but
designer files carry dimensions measured by GDI on Windows — (6, 13) for the old Tahoma 8.25pt default,
(7, 15) for Segoe UI 9pt — so a metric off by a unit factor rescales every migrated form by that factor.
`ContainerControl.CurrentAutoScaleDimensions` was `Font.Size * 2f`, a made-up number that happened to
land in range. It is now a measured average glyph width at the font's **pixel** size, which puts a
default font at about (6.5, 11); the test asserts a *range* around the Windows values rather than a
literal, because the two ways this can fail (points-as-pixels, device-as-logical) are both factor-sized
and a range catches them while a literal would just be another transcription.

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

### What the `ToolStrip.GetItemAt` follow-up found

W6.3's hit-test audit left this one open with a specific question: "public API with no internal mouse
caller, so what matters is which space an application is expected to pass." The space turned out to be
the smaller half of the answer.

**The method never returned anything.** It built its hit rectangle from `item.Bounds.Location` paired
with `item.Size`. Those look like the two halves of one rectangle and are two different stores:
`Bounds` is where layout put the item, `Size` is the size the application *requested*, and nothing in
the layout path writes it. On any strip whose items were not explicitly sized — the normal case — `Size`
is `0, 0`, so the rectangle was empty and `GetItemAt` answered null for every point at every scale.
Not a coordinate-space defect at all; an API that never answered.

**The register already knew, and I wrote it up as a discovery anyway.** `TSM-22` says it plainly —
"for AutoSize items `Size` is 0×0 so it returns null for everything" — and names the fix this change
made. It was missed because the check for an existing finding was `grep -rn GetItemAt docs/ | head`,
and `head` cut the output off two lines above the `toolstrip.md` hit. Third time in this plan that a
finding has been duplicated or mis-attached (`LAY-31`/`LAY-32`, `W5.25`'s wrong closure). The habit that
prevents it is not "grep first" — it was grepped — but **never truncating the search that decides
whether something is already known.**

**Why no test and no user had caught it.** Nothing inside the framework calls it: `MenuBase.OnMouseClick`
routes through `GetItemAtLocation`, which was always correct. A member with no internal caller gets
exactly as much verification as someone writes for it, and nobody had. That is the same shape as the
dead events in W6.1 — correct-looking code, one level short of being reached — and it argues the
remaining "public API, no internal caller" members deserve the same treatment rather than a reading.
`TSM-22` having sat open with the defect correctly described is the other half of that argument: the
finding existing is not the same as the finding being worked.

**The space question, answered by cross-validation rather than by reading.** `MenuItem.Bounds` is
logical, so the point is logical client coordinates — the space `MouseEventArgs.Location` arrives in.
The reason that is asserted by a scale-2 test agreeing with a real backend click, rather than stated
from the source, is #189: `MenuBase.GetItemAtLocation` reads *identically* to the code that was broken
in `Ribbon` and is correct. Reading cannot separate the two cases in this area; driving a click can.

**The follow-up closed the rest of TSM-22, and it turned out to be the same defect.** `Size` was a
store of its own, separate from the `Bounds` layout writes; `Width`, `Height` and `ContentRectangle`
all read it, so all three answered 0 for every normally laid-out item. Upstream has one store — `Size`
is a view over `Bounds` in both directions — and taking that shape closes all four members with one
property. It also makes the `GetItemAt` defect **structurally impossible**: with one store,
`new Rectangle (Bounds.Location, Size)` and `Bounds` are the same rectangle, so the hit-test cannot
disagree with the layout however it is written. Verified by restoring the old expression and watching
nothing fail. The hit-test fix treated the symptom; the split store was the cause, and the register had
them filed together under one finding all along.

**A recorded objection is not the same as a settled one.** `ToolStripItem.Height`'s remarks argued
against reading `Bounds`, on the grounds that it would answer 0 for an item never placed on a strip.
That was true, and it stopped being true the moment the setter wrote `Bounds` instead of a private
field — the objection described a consequence of the two-store design, not a reason for it. Worth
noticing generally: a comment explaining why something *cannot* be done is evidence about the code as
it stood when the comment was written, and deserves rechecking rather than deferring to.

**Tests that stop being proof should say so rather than be left looking like proof.** The `GetItemAt`
tests were written with two of seven deliberately surviving neutralization — a premise assertion and a
null guard, both labelled. Merging the stores then invalidated the premise of two more (`Size` is no
longer empty after layout, and assigning it no longer leaves `Bounds` alone), and made the remaining
five unable to fail against the old hit-test at all. They were not quietly kept green: the two whose
premise had gone were deleted, and the file now carries a note saying what the rest do and do not
establish, with the guarding moved to `ToolStripItemSizeTests`. A suite that is green for a reason
nobody has restated is the recurring failure this plan has now recorded half a dozen times, and a fix
that makes older tests redundant is one of the ways it happens.

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
