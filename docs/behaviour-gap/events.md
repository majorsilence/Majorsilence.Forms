# Event wiring and ordering (cross-cutting) — findings

## Summary
Event *wiring* in this layer is in good shape — the ambient-property cascades, layout, resize, paint
z-order, capture routing and the `Controls` collection notifications are faithful ports and I verified
them as matching (see the "verified as MATCHING" section, which the fixer can treat as safe). Event
*ordering and gating* is where it breaks, and it breaks in four concentrated places. (1) The focus
choke point is split across `Control.Select ()` and `ControlAdapter.SelectedControl`, so both halves
raise events and a mouse-driven focus change fires the new control's `Enter`/`GotFocus` **before** the
old one's `Leave`/`Validating` — while Tab fires them in the right order; validation itself runs after
`LostFocus`, ignores `CausesValidation` entirely, and `e.Cancel` does nothing. (2) The entire
`PreProcessMessage` layer is absent: `ProcessCmdKey`, `ProcessDialogKey`, `ProcessKeyPreview` and
`IsInputKey` are `=> false` virtuals with **no caller anywhere**, and the behaviours they gate
(AcceptButton/CancelButton, Tab) are hard-coded ahead of the focused control instead. (3) A
double-click raises `Click` twice, because `DoubleClick` and `Click` are raised as independent events
rather than upstream's `if/else`. (4) A family of events has both the declaration and the `On*` raiser
but no call site — `ClientSizeChanged`, `SystemColorsChanged`, `ApplicationExit`, `Application.Idle`,
`ChangeUICues`, `HelpRequested`, the modal-loop trio — most of them a one-line fix at a trigger point
that already exists in this framework. The dominant failure pattern is "the raiser exists, the choke
point exists, nothing connects them", followed by "two things that upstream orders were written as two
independent statements".
Counts: **38 findings — 4 P0, 15 P1, 18 P2, 1 P3.**

## Findings
### EVT-01 — `WindowBase.HandlePointerReleased` / `Control.RaiseClick` — Cat A — P0 — High
Double-click raises `Click`/`MouseClick` **in addition to** `DoubleClick`/`MouseDoubleClick`.

- **Ours:** on pointer release, `BuildMouseClickArgs` computes `Clicks`, then
  `if (ev.Clicks > 1) adapter.RaiseDoubleClick (ev); adapter.RaiseClick (ev); adapter.RaiseMouseUp (ev);`
  (`src/Majorsilence.Forms/WindowBase.cs:1091-1097`). `RaiseDoubleClick` -> `OnDoubleClick` raises
  `DoubleClick` + `MouseDoubleClick` (`src/Majorsilence.Forms/Control.cs:1201-1206`); `RaiseClick` then
  unconditionally raises `Click` + `MouseClick` (`src/Majorsilence.Forms/Control.cs:1778-1781`).
  Net for one double-click: `MouseDown(1) MouseUp(1) Click MouseClick` then
  `MouseDown(1) DoubleClick MouseDoubleClick Click MouseClick MouseUp(1)` — **two** Clicks.
- **Upstream:** `WmMouseUp` fires exactly one of the two:
  `if (!GetState(States.DoubleClickFired)) { OnClick(...); OnMouseClick(...); } else { OnDoubleClick(new MouseEventArgs(button,2,location)); OnMouseDoubleClick(...); }`
  then `OnMouseUp(...)` (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:11695-11707`).
  Documented WinForms sequence is MouseDown, MouseUp, Click, MouseClick, MouseDown(Clicks=2),
  DoubleClick, MouseDoubleClick, MouseUp — one Click per gesture.
- **Impact:** every control whose `Click` handler performs an action fires it twice on a double-click.
  Impatient double-clicks on a "Save"/"Post"/"Add" button submit twice; a grid row that both
  selects on Click and opens on DoubleClick opens *and* re-selects; navigation runs twice.
- **Fix:** in `WindowBase.HandlePointerReleased`, make it exclusive:
  `if (ev.Clicks > 1) adapter.RaiseDoubleClick (ev); else adapter.RaiseClick (ev); adapter.RaiseMouseUp (ev);`
  and have `RaiseDoubleClick` fall back to `RaiseClick` when the target control does not have
  `ControlStyles.StandardDoubleClick` (upstream `WmLButtonDblClk` only sets `DoubleClickFired` when the
  style is on, so a non-double-click control gets two plain Clicks instead).
- **Test:** headless: `HeadlessRenderer.Input.MouseDown/MouseUp` twice inside `DOUBLE_CLICK_TIME` on a
  Button; assert `Click` handler ran once and `DoubleClick` once.
- **Tests today:** none found (`grep -rn "DoubleClick" tests/` shows no ordering test).

### EVT-02 — `Control.Select` / `ControlAdapter.SelectedControl` — Cat A — P0 — High
On a mouse-driven focus change the **entering** control's `Enter`/`GotFocus` fire *before* the
leaving control's `Leave`/`LostFocus`/`Validating`/`Validated`.

- **Ours:** `Control.Select()` sets `Selected = true`, calls `OnGotFocus` (which raises `Enter` then
  `GotFocus`), and only *then* assigns `adapter.SelectedControl = this`
  (`src/Majorsilence.Forms/Control.cs:2290-2306`). The adapter setter is what deselects the previous
  control: `selected_control?.Deselect (); ... selected_control?.Select ();`
  (`src/Majorsilence.Forms/ControlAdapter.cs:75-88`), and `Deselect` -> `OnDeselected` -> `OnLostFocus`
  raises `Leave`, `LostFocus`, `Validating`, `Validated`
  (`src/Majorsilence.Forms/Control.cs:514-519`, `1243-1254`). Mouse focus goes through
  `RaiseMouseDown` -> `Select ()` (`src/Majorsilence.Forms/Control.cs:1908`), so clicking B while A has
  focus produces: `B.Enter, B.GotFocus, A.Leave, A.LostFocus, A.Validating, A.Validated`.
  Tab focus goes through `SelectNextControl` -> `adapter.SelectedControl = next`, which *does* get
  A-then-B order — so the same app sees two different orders depending on mouse vs keyboard.
- **Upstream:** `ContainerControl.UpdateFocusedControl` walks *up* from the old focused control raising
  `OnLeave` on it and its exclusive ancestors, calls `EnterValidation` (Validating/Validated), and only
  then walks *down* raising `OnEnter` on the entering control
  (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/ContainerControl.cs:1534-1665`).
  `GotFocus` is raised last, from `WmSetFocus` after `ActivateControl` returns
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:12054-12083`). There is no path in which
  the entering control's `Enter` precedes the leaving control's `Leave`.
- **Impact:** the single most common LOB pattern — "commit/parse the field being left, then load
  dependent data in the next field's `Enter`" — runs backwards. `Enter` on B reads model state that
  `Leave`/`Validating` on A has not written yet; error providers set in `Validating` are cleared by
  B's `Enter`; a cascading combo pair (Country -> State) loads the wrong list.
- **Fix:** invert `Control.Select ()`: assign `FindAdapter ().SelectedControl = this` *first* and let the
  adapter setter be the only place that raises focus events — i.e. move the `Selected = true; OnGotFocus (...)`
  out of `Select()` into the adapter setter after `selected_control?.Deselect ()`. Keep the
  `if (Selected) return;` re-entry guard.
- **Test:** headless: two TextBoxes on a form, focus A, `Input.MouseDown/MouseUp` on B, record event
  order in a list; assert `["A.Leave","A.Validating","A.Validated","A.LostFocus","B.Enter","B.GotFocus"]`.
- **Tests today:** none found for cross-control focus ordering.

### EVT-03 — `Control.OnLostFocus` — Cat A — P1 — High
`Validating`/`Validated` are raised **after** `LostFocus`, and are raised unconditionally, ignoring
`CausesValidation` on both the leaving and the entering control.

- **Ours:** `OnLostFocus` raises `Leave`, then `LostFocus`, then always builds a `CancelEventArgs`,
  calls `OnValidating`, and calls `OnValidated` when not cancelled
  (`src/Majorsilence.Forms/Control.cs:1243-1254`). No `CausesValidation` test appears anywhere on this
  path; `Control.CausesValidation` is only read by the standalone `Validate()` helper
  (`src/Majorsilence.Forms/Control.Compat.cs:401-415`).
- **Upstream:** validation is not part of `OnLostFocus` at all. `EnterValidation` bails out when
  `_unvalidatedControl is null`, when `!enterControl.CausesValidation`, and when the container's
  `AutoValidate` is `Disable`
  (`.../Layout/Containers/ContainerControl.cs:1727-1748`); `ValidateThroughAncestor` then validates the
  unvalidated control **and its ancestors** up to the common ancestor
  (`.../ContainerControl.cs:1862-1910`). It runs before `OnEnter` of the entering control.
- **Impact:** (a) clicking a `CausesValidation=false` button — the standard way to make Cancel/Help
  escape a half-filled form — still runs every `Validating` handler, so Cancel pops the validation
  message box the app deliberately suppressed; (b) a control with `CausesValidation=false` still
  validates itself on leave; (c) handlers that assume `Validating` precedes `LostFocus` (e.g. resetting
  a formatting flag in `LostFocus` after `Validating` reformatted the text) run inverted.
- **Fix:** remove the validation block from `Control.OnLostFocus`; run it from
  `ControlAdapter.SelectedControl`'s setter between `Deselect()` and `Select()`, gated on
  `leaving.CausesValidation && entering?.CausesValidation != false`, mirroring `EnterValidation`.
- **Test:** headless: `b.CausesValidation = false;` focus A (with a `Validating` handler that flags),
  click B; assert the flag was never set.
- **Tests today:** none.

### EVT-04 — `Control.OnValidating` cancel is ignored — Cat A — P1 — High
- **Ours:** `OnLostFocus` only skips `Validated` when `e.Cancel` is set; focus has already moved
  (`Selected` was set and `OnGotFocus` already ran on the new control — see EVT-02), and nothing
  restores it (`src/Majorsilence.Forms/Control.cs:1249-1253`).
- **Upstream:** with the default `AutoValidate.EnablePreventFocusChange`, `ValidateThroughAncestor`
  re-points the container at the invalid control (`SetActiveControl(_unvalidatedControl)`) and marks the
  control that was about to receive focus with `ValidationCancelled = true`, which also suppresses its
  pending click (`.../ContainerControl.cs:1911-1946`); `WmMouseUp` then skips `OnClick` because
  `fireClick && !ValidationCancelled` is false
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:11694`).
- **Impact:** `e.Cancel = true` in `Validating` — the canonical "you must fill this in" guard — does
  nothing here. Focus leaves the invalid field and, worse, the button the user clicked still fires its
  `Click`, so the app saves invalid data.
- **Fix:** on cancel, restore `adapter.SelectedControl` to the leaving control and set a
  `ValidationCancelled` flag on the control that was about to be entered; have `Control.RaiseClick`
  return early when that flag is set, clearing it in `RaiseMouseUp` (upstream clears it in
  `WmMouseUp`'s `finally`).
- **Test:** headless: TextBox A with `Validating` handler setting `e.Cancel = true`, Button B with a
  `Click` counter; click B; assert `A.Focused` is still true and the click counter is 0.
- **Tests today:** none.

### EVT-05 — `Leave`/`Enter` are never raised on container ancestors — Cat B — P1 — High
- **Ours:** only the leaf control's `Leave`/`Enter` fire. `Deselect`/`Select` touch a single control
  (`src/Majorsilence.Forms/Control.cs:514-519`, `2290-2306`); there is no ancestor walk anywhere in
  `ControlAdapter.SelectedControl` (`src/Majorsilence.Forms/ControlAdapter.cs:75-88`).
- **Upstream:** `UpdateFocusedControl`'s "heading up" loop raises `OnLeave` on every control from the
  old focused control up to (excluding) the common ancestor, and the "heading down" branch raises
  `OnEnter` on each control on the path down to the new one
  (`.../ContainerControl.cs:1543-1580` and `1616-1650`).
- **Impact:** a `GroupBox`/`Panel`/`UserControl`/`TabPage` that handles `Enter`/`Leave` to enable a
  toolbar section, start an edit transaction, or highlight the active group never hears anything.
  `UserControl`-based "editor panels" — the standard way LOB apps compose screens — get no lifecycle
  notification when focus enters or leaves them.
- **Fix:** in the focus choke point, compute the common ancestor of old and new (`Parent` walk), raise
  `OnLeave` bottom-up from old to the ancestor, then `OnEnter` top-down from the ancestor to new.
- **Test:** headless: Panel containing TextBox A; TextBox B outside it; focus A then B; assert
  `Panel.Leave` fired once.
- **Tests today:** none.

### EVT-06 — `ProcessCmdKey` / `ProcessDialogKey` / `ProcessKeyPreview` / `IsInputKey` / `PreProcessMessage` are never called — Cat B — P0 — High
- **Ours:** all five are `=> false` virtuals with **no caller anywhere in the framework**
  (`src/Majorsilence.Forms/Control.Compat.cs:568,579,585,601`;
  `src/Majorsilence.Forms/WindowBase.Compat.cs:23,34,40,48`;
  `src/Majorsilence.Forms/ControlAndFormParity.cs:344`). `grep -rn 'ProcessCmdKey (' src/` returns only
  the declarations. Key dispatch goes straight from `WindowBase.HandleKeyDown` to
  `adapter.RaiseKeyDown` -> `OnKeyDown` (`src/Majorsilence.Forms/WindowBase.cs:1341-1383`,
  `src/Majorsilence.Forms/Control.cs:1808-1837`).
- **Upstream:** `Control.PreProcessMessage` runs, for WM_KEYDOWN, `OnPreviewKeyDown`, then
  `ProcessCmdKey` (which bubbles the *whole parent chain* and the ContextMenuStrip), then `IsInputKey`,
  then `ProcessDialogKey` — all **before** `OnKeyDown` is raised from
  `ProcessKeyMessage`/`ProcessKeyEventArgs` (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`,
  `PreProcessMessage`/`ProcessKeyMessage`/`ProcessKeyEventArgs`).
- **Impact:** `protected override bool ProcessCmdKey(ref Message msg, Keys keyData)` is *the* WinForms
  idiom for form-level shortcuts (F5 refresh, Ctrl+S save, Ctrl+F find, Esc close) and for grid/tree
  key interception. Every one of those overrides compiles and is silently dead. Same for
  `ProcessDialogKey` overrides used to swallow Enter/Tab in editors.
- **Fix:** in `WindowBase.HandleKeyDown`, before raising anything, walk from
  `adapter.SelectedControl` up through `Parent`s to the window calling `ProcessCmdKey`; return true if
  any returns true. Then consult `IsInputKey` on the focused control; if false, walk the same chain
  calling `ProcessDialogKey` (which is where AcceptButton/CancelButton belong — see EVT-07).
- **Test:** headless: a Form subclass overriding `ProcessCmdKey` to count `Keys.F5`;
  `HeadlessRenderer.Input.KeyDown (form, Keys.F5)`; assert the count is 1 and no `KeyDown` handler ran.
- **Tests today:** none.

### EVT-07 — `WindowBase.HandleKeyDown` AcceptButton/CancelButton pre-empt the focused control — Cat A — P0 — High
- **Ours:** Enter -> `form.AcceptButton.PerformClick ()` and Escape -> `form.CancelButton.PerformClick ()`
  run at the very top of `HandleKeyDown`, before the form's own `KeyDown`, before `KeyPreview`, and
  before the focused control ever sees the key; both `return true` unconditionally
  (`src/Majorsilence.Forms/WindowBase.cs:1349-1370`). Nothing consults `IsInputKey`, `AcceptsReturn`,
  or whether the focused control is itself a Button.
- **Upstream:** Enter/Escape reach `Form.ProcessDialogKey` only after `ProcessCmdKey` and after
  `IsInputKey(keyData)` has been asked of the focused control; a multiline `TextBox` (or one with
  `AcceptsReturn = true`) returns `true` from `IsInputKey` for `Keys.Enter`, which short-circuits
  `PreProcessMessage` and hands the key to the control. `Form.ProcessDialogKey` also defers to the
  focused control when it is itself an `IButtonControl`
  (`src/System.Windows.Forms/System/Windows/Forms/Form.cs`, `ProcessDialogKey`).
- **Impact:** on any form with an `AcceptButton` — i.e. essentially every dialog — pressing Enter in a
  multiline `TextBox` submits the dialog instead of inserting a newline; pressing Enter while a
  different Button has focus clicks the *AcceptButton*, not the focused one; a grid in edit mode cannot
  commit a cell with Enter. Escape in a control that wants it (an inline editor cancelling) closes the
  dialog instead.
- **Fix:** move the Accept/Cancel handling to the end of the dispatch, into a real `ProcessDialogKey`
  implemented on `Form`, invoked only after `ProcessCmdKey` and only when
  `adapter.SelectedControl?.IsInputKey (keyData) != true` and the focused control is not an
  `IButtonControl` (for Enter).
- **Test:** headless: form with `AcceptButton = ok` and a `TextBox { Multiline = true }` focused;
  `Input.KeyDown (form, Keys.Return)`; assert the ok button's `Click` did not fire and the TextBox
  received `KeyDown`.
- **Tests today:** none.

### EVT-08 — `Control.RaiseKeyDown` ignores `PreviewKeyDownEventArgs.IsInputKey` and raises PreviewKeyDown in the wrong place — Cat A — P2 — High
- **Ours:** `OnPreviewKeyDown (new PreviewKeyDownEventArgs (e.KeyData));` is called inline and the args
  object is discarded — `IsInputKey` set by a handler has no effect
  (`src/Majorsilence.Forms/Control.cs:1834-1836`). It also runs *after* the form-level Enter/Escape
  shortcuts (EVT-07), so `PreviewKeyDown` never sees Enter on a form with an AcceptButton.
- **Upstream:** `PreProcessMessage` raises `OnPreviewKeyDown(args)` first and then
  `if (args.IsInputKey) { SetExtendedState(ExtendedStates.InputKey, true); return false; }` — the whole
  point of the event is to let a handler claim a key that would otherwise be a dialog key
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`, `PreProcessMessage`; the state flag is
  documented in `Control.ExtendedStates.cs`).
- **Impact:** the standard "let my control handle the arrow keys / Tab / Enter" recipe
  (`e.IsInputKey = true` in `PreviewKeyDown`) is inert; the key is still consumed by Tab navigation or
  the AcceptButton.
- **Fix:** keep the args, and treat `args.IsInputKey == true` the same way EVT-06's `IsInputKey`
  consultation does — skip dialog-key processing for that key.
- **Test:** headless: control with `PreviewKeyDown += (s,e) => e.IsInputKey = true;` on `Keys.Tab`;
  send Tab; assert focus did not move and the control got `KeyDown`.
- **Tests today:** none.

### EVT-09 — `Control.RaiseKeyDown` Tab handling bypasses validation and `ProcessDialogKey` — Cat A — P2 — High
- **Ours:** `ControlAdapter` intercepts `Keys.Tab` before anything else and calls
  `SelectNextControl (adapter.SelectedControl, !e.Shift, true, true, true)` then `e.Handled = true`
  (`src/Majorsilence.Forms/Control.cs:1820-1830`), and `RaiseKeyPress` repeats the same for
  `e.KeyChar == 9` (`src/Majorsilence.Forms/Control.cs:1853-1861`). The focused control's own
  `KeyDown` never sees Tab, and `IsInputKey`/`AcceptsTab` are not consulted.
- **Upstream:** Tab reaches `Control.ProcessDialogKey` -> `ContainerControl.ProcessTabKey`, *after*
  `ProcessCmdKey` and *after* `IsInputKey` — a `TextBox` with `AcceptsTab = true` or a grid in edit mode
  keeps the Tab.
- **Impact:** `AcceptsTab` is unimplementable; a `DataGridView`/grid cannot use Tab to move between
  cells (it moves out of the grid entirely); custom controls that navigate internally with Tab cannot.
- **Fix:** fold Tab into the `ProcessDialogKey` chain added for EVT-06 instead of special-casing it in
  the adapter.
- **Test:** headless: `TextBox { AcceptsTab = true }` focused, send Tab, assert focus unchanged.
- **Tests today:** none.

### EVT-10 — `WindowBase.EnsureShownBookkeeping` — Cat A — P1 — High
Form show order is `VisibleChanged, Load, Activated, HandleCreated, Shown, Layout`; upstream is
`HandleCreated, (children) HandleCreated, Load, Layout, VisibleChanged, Activated, Shown`.

- **Ours:** `visible = true; OnVisibleChanged (...)`, then `OpenForms.Add`, `SyncAdapterBounds ()`,
  `EnsureLoaded ()` (raises `Load`), `IsActive = true`, then `MarkHandleCreated ()` (raises
  `HandleCreated`), `OnShown (...)`, then `adapter.PerformLayout ()`
  (`src/Majorsilence.Forms/WindowBase.cs:1699-1740`; `MarkHandleCreated` is
  `src/Majorsilence.Forms/KryptonPortParity.cs:115-122`).
- **Upstream:** `Form.SetVisibleCore` raises `OnLoad` *before* `base.SetVisibleCore(value)`, and
  `base.SetVisibleCore` is what raises `VisibleChanged`
  (`src/System.Windows.Forms/System/Windows/Forms/Form.cs:2145-2175`); `OnLoad` itself is reached from
  `OnCreateControl`, i.e. after the handle (and every child handle) exists
  (`.../Form.cs:4102-4112`).
- **Impact:** two concrete breakages. (a) `HandleCreated` on the *form* arrives after `Load`, so the
  common `if (IsHandleCreated) BeginInvoke(...)` guard inside a `Load` handler takes the "no handle"
  branch (`WindowBase.IsHandleCreated => shown`, `src/Majorsilence.Forms/WindowBase.cs:2085`, and
  `shown` is only set by `MarkHandleCreated`). (b) `VisibleChanged` fires before `Load`, so a handler
  that sets up state on becoming visible runs before `Load` has initialised it — the reverse of every
  WinForms sample.
- **Fix:** reorder `EnsureShownBookkeeping` to `MarkHandleCreated (); SyncAdapterBounds ();
  EnsureLoaded (); adapter.PerformLayout (); visible = true; OnVisibleChanged (...); IsActive = true;
  OnShown (...)`, keeping the `OpenForms.Add` before `EnsureLoaded`.
- **Test:** headless: record the order of `HandleCreated`, `Load`, `VisibleChanged`, `Shown` on a Form,
  call `Show ()`, assert the sequence.
- **Tests today:** none for the relative order (form.md covers Load/Shown existence).

### EVT-11 — `Form.Shown` is raised synchronously, not posted — Cat A — P1 — High
- **Ours:** `OnShown (EventArgs.Empty)` runs inline inside `EnsureShownBookkeeping`, which runs inside
  `Show ()`, before anything has painted (`src/Majorsilence.Forms/WindowBase.cs:1732-1734`; the code's
  own comment at `1738` confirms the first paint has not happened yet).
- **Upstream:** `OnLoad` ends with `BeginInvoke(new MethodInvoker(CallShownEvent))` — `Shown` is
  *posted* and runs on a later message-loop turn, after the form has been painted
  (`src/System.Windows.Forms/System/Windows/Forms/Form.cs:4321-4325`, `3800-3803`).
- **Impact:** the standard "do the slow/blocking work in `Shown` so the user sees the form first"
  idiom is defeated: a `Shown` handler that runs a query, or opens a modal progress dialog, does so
  over an unpainted window and blocks `Show ()`/`ShowDialog ()` from returning.
- **Fix:** post it — `BeginInvoke (() => OnShown (EventArgs.Empty))` (or the backend's
  post-to-UI-thread equivalent) after the first frame; keep the synchronous call only for the
  headless backend where there is no loop.
- **Test:** headless is a poor fit; assert instead that `Shown` fires after at least one
  `HeadlessRenderer` render pass rather than during `Show ()`.
- **Tests today:** none.

### EVT-12 — Closing a Form never raises `VisibleChanged` and leaves `Visible == true` — Cat A — P1 — High
- **Ours:** `Close ()` raises FormClosing, removes from `OpenForms`, calls `Backend.Close ()`;
  `OnBackendClosed` then raises `Closed`, `FormClosed`, `CompleteClose`, `HandleDestroyed`
  (`src/Majorsilence.Forms/WindowBase.cs:47-77`, `204-226`). Nothing sets `visible = false` on this
  path — the only two `visible = false` assignments are in `Hide ()` (`WindowBase.cs:574`) and
  `Dispose` (`WindowBase.cs:260`). `Deactivate` is not raised either.
- **Upstream:** closing destroys the handle, which sends WM_SHOWWINDOW/WM_ACTIVATE: the documented
  close sequence is `FormClosing, FormClosed, Deactivate, VisibleChanged(false), HandleDestroyed,
  Disposed`.
- **Impact:** after `form.Close ()` the form still reports `Visible == true`; app code that keeps a
  reference and tests `Visible` to decide whether to re-show, or that hangs "release the singleton"
  logic off `VisibleChanged`, never fires. `Deactivate` handlers (save-on-deactivate) are skipped.
- **Fix:** in `OnBackendClosed`, before `OnClosed`, set `visible = false` + `OnVisibleChanged` and
  raise `OnDeactivate` when `IsActive`.
- **Test:** headless: `form.Show (); form.Close ();` assert `form.Visible == false` and that a
  `VisibleChanged` handler saw one `false` transition.
- **Tests today:** none.

### EVT-13 — `ControlCollection.Add` raises `VisibleChanged` on a control whose visibility did not change — Cat A — P1 — High
- **Ours:** the `finally` of the reparent block does
  `if (item.Visible) { item.CreateControl (); item.OnVisibleChanged (EventArgs.Empty); }`
  (`src/Majorsilence.Forms/ControlCollection.cs:476-481`).
- **Upstream:** the corresponding block is
  `if (oldParent != value._parent && (Owner._state & States.Created) != 0) { value.SetParentHandle(...); if (value.Visible) value.CreateControl(); }` — no `OnVisibleChanged` at all, and
  `CreateControl` only when the *parent* is already created
  (`src/System.Windows.Forms/System/Windows/Forms/Control.ControlCollection.cs`, `Add`, the
  `finally` after `AssignParent`).
- **Impact:** (a) every `Controls.Add` in `InitializeComponent` raises a spurious `VisibleChanged` on
  the child — a control that lazily loads data or starts a timer on `VisibleChanged` does so at
  construction time, on every control, before `Load`. (b) `HandleCreated` fires (and
  `Created`/`IsHandleCreated` become true) during designer construction rather than when the form is
  created, so `if (IsHandleCreated)` guards that are meant to distinguish design-time-ish from live
  are wrong, and `Invalidate()`'s `if (!Created) return;` guard
  (`src/Majorsilence.Forms/Control.cs:943`) stops protecting anything.
- **Fix:** drop the `OnVisibleChanged` call; gate `item.CreateControl ()` on `Owner.Created`, and
  create the children from the form's own create pass instead (`WindowBase.CreateControl` ->
  `adapter.CreateControl ()`, which already recurses — `src/Majorsilence.Forms/Control.cs:387-389`).
- **Test:** headless: `var t = new TextBox (); int n = 0; t.VisibleChanged += (s,e) => n++;
  panel.Controls.Add (t);` assert `n == 0`.
- **Tests today:** none.

### EVT-14 — Parent never gets `MouseLeave` when the pointer moves onto one of its children — Cat A — P1 — High
- **Ours:** `Control.RaiseMouseMove` only raises `MouseLeave` on the *previous child*
  (`current_mouse_in`), never on `this`. Moving from the parent's own area onto a child hits
  `current_mouse_in == null`, so the first `if` is skipped entirely and the parent's `OnMouseLeave` is
  never called (`src/Majorsilence.Forms/Control.cs:2016-2030`). The reverse transition (child ->
  parent area) *does* raise `OnMouseEnter (e)` on the parent again
  (`src/Majorsilence.Forms/Control.cs:2020-2021`), so the parent accumulates repeated `MouseEnter`
  with no matching `MouseLeave`.
- **Upstream:** each control is its own HWND; moving into a child sends WM_MOUSELEAVE to the parent
  (`WmMouseLeave` -> `OnMouseLeave`,
  `src/System.Windows.Forms/System/Windows/Forms/Control.cs:11553-11557`) and WM_MOUSEENTER to the
  child, so enter/leave are always balanced per control.
- **Impact:** any container that paints a hover state, shows a hover toolbar, or starts/stops a timer
  on `MouseEnter`/`MouseLeave` sticks in the "hovered" state permanently once the pointer crosses one
  of its children. `IsHovering` on a `Hoverable` container stays true
  (`src/Majorsilence.Forms/Control.cs:1311-1315`), so the hot visual never clears. Handler counts also
  diverge (N enters, 0 leaves).
- **Fix:** in `RaiseMouseMove`, when `current_mouse_in == null && child != null`, call
  `OnMouseLeave (EventArgs.Empty)` on `this` before raising the child's enter; and guard the
  parent's re-`OnMouseEnter` with a per-control "mouse is inside" flag so it is not raised twice.
- **Test:** headless: Panel with a Button inside it; move the pointer to a panel-only point then onto
  the button; assert `panel.MouseLeave` fired exactly once and `panel.MouseEnter` exactly once.
- **Tests today:** none.

### EVT-15 — `MouseHover` fires immediately on enter and on every move, with no dwell timer — Cat A — P2 — High
- **Ours:** `OnMouseEnter` sets `hover_raised = true` and calls `OnMouseHover (EventArgs.Empty)`
  straight away (`src/Majorsilence.Forms/Control.cs:1317-1322`); `OnMouseMove` re-raises it whenever
  `ResetMouseEventArgs ()` cleared the flag (`src/Majorsilence.Forms/Control.cs:1363-1371`).
- **Upstream:** `MouseHover` comes from WM_MOUSEHOVER, which the OS posts only after the pointer has
  rested for `SystemInformation.MouseHoverTime` (~400 ms) inside the tracked rectangle
  (`WmMouseHover`, `src/System.Windows.Forms/System/Windows/Forms/Control.cs:11641-11645`), and at
  most once per `TrackMouseEvent` arm.
- **Impact:** hover-triggered work runs on every pass-through. A `MouseHover` handler that fetches a
  row detail, shows a preview popup, or logs telemetry fires while the pointer is merely crossing the
  control — visible flicker on toolbars and grids, and needless I/O.
- **Fix:** arm a `System.Windows.Forms`-equivalent timer in `OnMouseEnter` for
  `SystemInformation.MouseHoverTime`, cancel it in `OnMouseMove`/`OnMouseLeave`, raise `MouseHover`
  from the tick. Keep `ResetMouseEventArgs` as the re-arm.
- **Test:** headless: enter a control, assert `MouseHover` has not fired; advance the injected clock
  past the hover time and pump; assert it fired once.
- **Tests today:** none. (The behaviour is documented in the XML comment on `Control.MouseHover`, so
  this is a known divergence — it is listed because the "fires on every move" half is not documented.)

### EVT-16 — `MouseDown` always reports `Clicks == 1` — Cat A — P2 — High
- **Ours:** `HandlePointerPressed` hard-codes the click count:
  `new MouseEventArgs (button, 1, lx, ly, ...)` (`src/Majorsilence.Forms/WindowBase.cs:1075`). Only the
  *release* path computes a real count (`BuildMouseClickArgs`,
  `src/Majorsilence.Forms/WindowBase.cs:189-202`).
- **Upstream:** the second press of a double-click arrives as WM_LBUTTONDBLCLK and reaches
  `WmMouseDown(ref m, MouseButtons.Left, 2)`, so `MouseDown` handlers see `e.Clicks == 2`
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:11487`, `11543-11546`).
- **Impact:** the common `if (e.Clicks == 2)` test inside a `MouseDown` handler — used by list/tree/
  grid code to open an item without subscribing to `DoubleClick` — never triggers.
- **Fix:** move the click-count computation ahead of the press dispatch: compute it in
  `HandlePointerPressed` and reuse the same count for the matching release.
- **Test:** headless: two down/up pairs within the double-click interval; assert the second
  `MouseDown` reported `Clicks == 2`.
- **Tests today:** none.

### EVT-17 — `PaintEventArgs.ClipRectangle` is always the whole control; `Invalidate(Rectangle)` does not narrow it — Cat C — P2 — High
- **Ours:** `Invalidate (Rectangle)` sets a single boolean (`States.IsDirty`) and forwards the
  rectangle only to the window, then raises `Invalidated` with it
  (`src/Majorsilence.Forms/Control.cs:941-951`). The paint pass builds
  `new PaintEventArgs (info, canvas, Scaling)` where `info` is the control's *full* `ScaledSize` and
  the canvas has no clip set (`src/Majorsilence.Forms/Control.cs:1429-1438`), and `ClipRectangle` is
  derived from `Canvas.LocalClipBounds`
  (`src/Majorsilence.Forms/PaintEventArgs.cs:57-61`).
- **Upstream:** `PaintEventArgs.ClipRectangle` is the actual update region from WM_PAINT, so
  `Invalidate(rect)` genuinely narrows the next paint.
- **Impact:** owner-drawn controls that use `e.ClipRectangle` to skip rows/cells outside the damaged
  band (the standard optimisation in ported grid/list/chart code) redraw everything every frame —
  correct output, but O(n) work per repaint on large surfaces. Code that uses `ClipRectangle` to
  *decide what changed* (e.g. only re-measuring the invalidated row) may also mis-handle the
  full-rect case.
- **Fix:** accumulate the invalid rectangle per control (union) in `Invalidate (Rectangle)`, clip the
  child canvas to it in `PaintChildren` before `RaisePaint`, and clear it after the pass.
- **Test:** headless: control whose `Paint` records `e.ClipRectangle`; `Invalidate (new Rectangle (
  10, 10, 5, 5))` then render; assert the recorded rect is not the full bounds.
- **Tests today:** none.

### EVT-18 — `Control.Refresh` / `Control.Update` are asynchronous — Cat A — P1 — High
- **Ours:** `public virtual void Refresh () => Invalidate ();`
  (`src/Majorsilence.Forms/Control.Compat.cs:31`) and `public void Update () => Invalidate ();`
  (`src/Majorsilence.Forms/Control.Compat.cs:450`); the same on the window
  (`src/Majorsilence.Forms/WindowBase.cs:614`, `1503`). Neither paints; both just mark dirty and
  return.
- **Upstream:** `Refresh()` is `Invalidate(true); Update();` and `Update()` calls
  `PInvoke.UpdateWindow` — a **synchronous** WM_PAINT before returning
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`, `Refresh`/`Update`).
- **Impact:** the classic single-threaded progress idiom — `label.Text = i.ToString (); label.Refresh ();`
  inside a `for` loop, or `Application.DoEvents ()`-free status updates — shows nothing until the loop
  ends, because the loop never yields to the backend's render tick. Migrated batch/import screens
  appear frozen.
- **Fix:** give `Control.Update ()` a synchronous path: render this control's back buffer immediately
  (the code already exists in `PaintChildren`/`PreWarm`) and blit it, then make `Refresh ()` be
  `Invalidate (true); Update ();`.
- **Test:** headless: change `Text`, call `Refresh ()`, and read the control's back buffer without
  running a render pass; assert the new text is already drawn.
- **Tests today:** none.

### EVT-19 — `Click` fires even when the button is released outside the control — Cat A — P1 — High
- **Ours:** on release, `Control.RaiseClick` hands the click to `Controls.FindCapturedChild ()` first
  and returns — with no test of whether the release point is inside that control
  (`src/Majorsilence.Forms/Control.cs:1743-1751`). Since `RaiseMouseDown` sets `Capture = true` on the
  pressed control (`src/Majorsilence.Forms/Control.cs:1908`), press-on-button / drag-away / release
  still raises `Click` and `MouseClick`.
- **Upstream:** `WmMouseUp` computes
  `bool fireClick = ... && PInvoke.WindowFromPoint(screenLocation) == HWND;` — the click is raised only
  when the release lands over the same window
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:11689-11692`).
- **Impact:** the universal "I changed my mind — drag off the button before letting go" gesture no
  longer cancels the action. Users cannot back out of a mis-aimed Delete/Post button.
- **Fix:** in `RaiseClick`, before raising on a capture holder, test
  `holder.ClientRectangle.Contains (translated.Location)` (or hit-test the release point back through
  the tree) and skip `OnClick`/`OnMouseClick` when it is outside. `MouseUp` must still be raised.
- **Test:** headless: `Input.MouseDown` at (5,5) inside a Button, `Input.MouseUp` at a point outside
  its bounds; assert the `Click` counter is 0 and `MouseUp` fired.
- **Tests today:** none.

### EVT-20 — `Paint` event is raised outside `OnPaint`, so overriding `OnPaint` without calling base no longer suppresses it — Cat A — P2 — High
- **Ours:** `RaisePaint` calls `OnPaint (e)` and then, separately, `Paint?.Invoke (this, e)`
  (`src/Majorsilence.Forms/Control.cs:2182-2192`); `Control.OnPaint` is deliberately empty
  (`src/Majorsilence.Forms/Control.cs:1406-1413`).
- **Upstream:** the event *is* the body of the hook —
  `protected virtual void OnPaint(PaintEventArgs e) { ((PaintEventHandler?)Events[s_paintEvent])?.Invoke(this, e); }`
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:7966-7969`).
- **Impact:** two behaviours change for ported custom controls. (1) A derived control that overrides
  `OnPaint` and intentionally does not call base — the documented way to suppress user `Paint`
  handlers — now still fires them. (2) Ordering: upstream, a control that calls `base.OnPaint (e)`
  *last* gets handlers drawing on top of its own output, and calling it *first* gets them underneath;
  here handlers always draw last, so a decorator control that expected to overpaint the handler's
  output is now overpainted by it.
- **Fix:** move `Paint?.Invoke (this, e)` into `Control.OnPaint`, leaving `RaisePaint` as
  `SetState (IsDirty, false); OnPaint (e); PaintChildren (e);`. (`Paint` is a plain field-backed event
  here, not `Events`-backed — `src/Majorsilence.Forms/Control.Events.cs`, `public event PaintEventHandler? Paint;`.)
- **Test:** headless: a control overriding `OnPaint` with an empty body plus a `Paint` handler;
  render; assert the handler did not run.
- **Tests today:** none.

### EVT-21 — Removing the focused control leaves it as `ControlAdapter.SelectedControl` — Cat B — P2 — High
- **Ours:** `ControlCollection.RemoveCore` detaches the control and raises `ControlRemoved`, but never
  reassigns focus; the code carries the upstream call commented out as a TODO:
  `// ContainerControl needs to see it needs to find a new ActiveControl. TODO` /
  `//if (Owner.GetContainerControl () is ContainerControl cc) cc.AfterControlRemoved (value, Owner);`
  (`src/Majorsilence.Forms/ControlCollection.cs:583-590`).
- **Upstream:** `Control.ControlCollection.Remove` calls
  `ContainerControl.AfterControlRemoved(value, Owner)`, which clears `_activeControl`/`_focusedControl`
  and selects the next control (`.../Layout/Containers/ContainerControl.cs`, `AfterControlRemoved`).
- **Impact:** dynamic UIs that swap panels (wizard steps, tab content built at runtime) leave the
  adapter pointing at a detached control: `RaiseKeyDown`/`RaiseKeyPress` keep routing keystrokes to an
  orphan (`src/Majorsilence.Forms/Control.cs:1830`, `1862`), and the *next* control the user clicks
  gets its `Leave`/`Validating` raised on the removed one.
- **Fix:** in `RemoveCore`, after `AssignParent (null)`, if `item` or one of its descendants is the
  adapter's `SelectedControl`, set `SelectedControl = null` and `SelectNextControl` from the owner.
- **Test:** headless: focus a TextBox, `panel.Controls.Remove (textBox)`, send a key; assert the
  removed TextBox got no `KeyDown`.
- **Tests today:** none.

### EVT-22 — `Control.SystemColorsChanged` — Cat D — P1 — High
- **Ours:** `public event EventHandler? SystemColorsChanged { add { } remove { } }` — empty accessors,
  so a handler attaches and is discarded (`src/Majorsilence.Forms/Control.Events.cs`, last line of the
  file). There is no `OnSystemColorsChanged` hook at all.
- **Natural trigger that already exists:** `Application.DoThemeChanged ()`
  (`src/Majorsilence.Forms/Application.cs:84-91`) already walks every open form and hosted surface and
  calls `Control.OnThemeChanged`, which already recurses to every descendant
  (`src/Majorsilence.Forms/Control.cs:1631-1639`). That is exactly the moment upstream raises this.
- **Upstream:** `Control.OnSystemColorsChanged` is raised from `WmSysColorChange` and cascades to
  children (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`, `OnSystemColorsChanged`).
- **Impact:** light/dark switching is a first-class feature here (`Application.SetColorMode`), and the
  one event a ported control uses to re-resolve cached brushes/pens/images on a theme change is dead.
  Custom-drawn controls keep painting with colours captured at construction.
- **Fix:** make it a real `Events`-backed event with a `protected virtual void OnSystemColorsChanged
  (EventArgs e)`, and call it from `Control.OnThemeChanged` before the child recursion.
- **Test:** headless: subscribe on a control, call `Application.SetColorMode (SystemColorMode.Dark)`;
  assert the handler ran once.
- **Tests today:** none.

### EVT-23 — `Control.ClientSizeChanged` / `OnClientSizeChanged` — Cat D — P2 — High
- **Ours:** the event and the raiser both exist
  (`src/Majorsilence.Forms/ControlAndFormParity.cs:355`, `399`), and the one place that would call it
  is commented out: `// OnClientSizeChanged (EventArgs.Empty);`
  (`src/Majorsilence.Forms/Control.Layout.cs:647`), directly under the live
  `OnSizeChanged (EventArgs.Empty)`.
- **Upstream:** `UpdateBounds` raises `OnSizeChanged` then `OnClientSizeChanged`
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`, `UpdateBounds`).
- **Impact:** code that sizes content to the client area (scroll extents, virtual canvases, docked
  overlays) and hangs off `ClientSizeChanged` rather than `Resize` never runs. `WindowBase` raises its
  own copy (`src/Majorsilence.Forms/WindowBase.cs:893`), so the same app gets it on the Form but not
  on any Panel/UserControl — the inconsistency is the trap.
- **Fix:** uncomment the call. Guard it on the client rectangle actually changing (border/padding can
  change it without the bounds changing, which is the second upstream call site).
- **Test:** headless: `panel.Size = new Size (200, 100);` assert `ClientSizeChanged` fired once.
- **Tests today:** none.

### EVT-24 — `Application.Idle` — Cat D — P1 — Medium
- **Ours:** the event is real (`src/Majorsilence.Forms/Application.cs:483`) and there is a public
  `Application.RaiseIdle` helper (`src/Majorsilence.Forms/AppMenuBindingParity.cs:103`), but the
  framework never calls it — the only caller in the repo is a unit test
  (`tests/Majorsilence.Forms.Tests/AppMenuBindingParityTests.cs:260`).
- **Upstream:** raised from the message loop whenever the queue drains
  (`Application.ThreadContext.OnIdle` / `FComponentIdle`,
  `src/System.Windows.Forms/System/Windows/Forms/Application.ComponentThreadContext.cs`).
- **Natural trigger that already exists:** the backend run loop —
  `Application.Run`'s `_mainLoopCancellationTokenSource` loop (`src/Majorsilence.Forms/Application.cs:179-194`
  and the `Run` body) and the Avalonia/headless render tick.
- **Impact:** `Application.Idle` is the standard place LOB apps refresh command state (enable/disable
  toolbar buttons, update status bars) instead of polling. Menus and toolbars stay stale.
- **Fix:** call `RaiseIdle (EventArgs.Empty)` from the main loop once per pass when no input is
  pending (and from the Avalonia dispatcher's idle priority for that backend).
- **Test:** headless: subscribe, pump one loop iteration, assert at least one Idle.
- **Tests today:** `AppMenuBindingParityTests.RaiseIdle_reaches_a_registered_handler` tests only the
  helper, not that anything calls it.

### EVT-25 — `Application.ApplicationExit` — Cat D — P1 — High
- **Ours:** `public static event EventHandler? ApplicationExit { add { } remove { } }`
  (`src/Majorsilence.Forms/Application.cs:480`) — handlers are dropped. `Application.Exit` right above
  it *does* raise an internal `OnExit` (`src/Majorsilence.Forms/Application.cs:191`), so the trigger
  point exists and is one line away.
- **Upstream:** `Application.RaiseExit` fires `s_eventApplicationExit` during `ExitInternal`.
- **Impact:** the standard "flush settings / close the log / release the mutex on shutdown" hook never
  runs. Data written only in an `ApplicationExit` handler is lost on every exit.
- **Fix:** back the event with a static field and raise it alongside `OnExit` in
  `Application.Exit (CancelEventArgs?)`.
- **Test:** subscribe, call `Application.Exit ()`, assert the handler ran once.
- **Tests today:** none.

### EVT-26 — `Application.EnterThreadModal` / `LeaveThreadModal` / `ThreadExit` — Cat D — P2 — High
- **Ours:** real events with `internal static` raise helpers
  (`src/Majorsilence.Forms/AppMenuBindingParity.cs:128-142`) and **no caller anywhere** —
  `grep -rn 'RaiseEnterThreadModal|RaiseLeaveThreadModal|RaiseThreadExit' src/` returns only the
  declarations.
- **Upstream:** `EnterThreadModal`/`LeaveThreadModal` bracket `Application.ThreadContext.BeginModalMessageLoop`
  /`EndModalMessageLoop`; `ThreadExit` fires when the thread's message loop ends.
- **Natural trigger that already exists:** `WindowBase.ShowDialog`
  (`src/Majorsilence.Forms/WindowBase.cs:1678-1690`) — which already disables the parent window — and
  `Form.CompleteClose`; `ThreadExit` belongs next to `Application.Exit`.
- **Impact:** apps that suspend background timers/polling while a modal dialog is up (to avoid
  re-entrancy) never get the notification, so background work keeps firing into a blocked UI.
- **Fix:** call `RaiseEnterThreadModal ()` at the top of `ShowDialog` and `RaiseLeaveThreadModal ()`
  when the dialog completes; `RaiseThreadExit ()` from `Application.Exit`.
- **Test:** subscribe, `form.ShowDialog ()` on the headless backend with an immediate close; assert one
  enter and one leave, in that order.
- **Tests today:** none.

### EVT-27 — `Control.HelpRequested` / `OnHelpRequested`, `Form.HelpButtonClicked` — Cat D — P2 — Medium
- **Ours:** `Control.HelpRequested` is a real field-backed event whose own doc says "Nothing in this
  layer raises it" (`src/Majorsilence.Forms/Control.Events.cs`, the `HelpRequested` remarks);
  `Form.HelpButtonClicked` + `OnHelpButtonClicked` exist with zero callers
  (`src/Majorsilence.Forms/ControlAndFormParity.cs:577`, `608`).
- **Upstream:** `HelpRequested` is raised from WM_HELP (F1 or the `?` caption button) and bubbles from
  the focused control to its parents; `Form.HelpButtonClicked` from the caption `?` button.
- **Natural trigger that already exists:** `WindowBase.HandleKeyDown`
  (`src/Majorsilence.Forms/WindowBase.cs:1341`) already has the key dispatch — F1 is a one-line add —
  and `Form`'s managed caption buttons are drawn and hit-tested by this layer
  (`Form.UpdateCaptionRegions`, `src/Majorsilence.Forms/Form.cs:447`), so `HelpButton` has a real
  click site.
- **Impact:** context-sensitive help (F1) is dead across the app; `HelpProvider`-driven UIs lose their
  entire mechanism.
- **Fix:** on `Keys.F1` in `HandleKeyDown`, walk from `adapter.SelectedControl` up raising
  `OnHelpRequested` until `HelpEventArgs.Handled`; wire the caption `?` button to
  `OnHelpButtonClicked`.
- **Test:** headless: subscribe to `HelpRequested` on the focused TextBox, `Input.KeyDown (form, Keys.F1)`;
  assert it fired.
- **Tests today:** none.

### EVT-28 — `Control.ChangeUICues` / `Form.ShowFocusCues` — Cat D — P2 — High
- **Ours:** `ChangeUICues` is real with an `OnChangeUICues` raiser
  (`src/Majorsilence.Forms/Control.Events.cs`, the `ChangeUICues` block) and no caller. Meanwhile the
  framework **does** change focus-cue state: `Control.RaiseKeyDown` sets `f.ShowFocusCues = true` when
  Tab is pressed (`src/Majorsilence.Forms/Control.cs:1822-1823`, and the same in `RaiseKeyPress` at
  `1855-1856`).
- **Upstream:** `OnChangeUICues` is raised from `WM_UPDATEUISTATE` exactly when the keyboard/focus cue
  state flips (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`, `WmUpdateUIState`).
- **Impact:** owner-drawn controls that decide whether to paint the focus rectangle / mnemonic
  underline from `ChangeUICues` never repaint when the user switches from mouse to keyboard
  navigation, so focus rectangles appear on some controls and not others.
- **Fix:** raise `OnChangeUICues (new UICuesEventArgs (UICues.ChangeFocus | UICues.ShowFocus))` on
  every control of the form at the point `ShowFocusCues` is set.
- **Test:** headless: subscribe on a Button, send Tab; assert `ChangeUICues` fired with
  `e.ShowFocus == true`.
- **Tests today:** none.

### EVT-29 — `Control.RegionChanged` — Cat D — P3/P2 — High
- **Ours:** event + `OnRegionChanged` raiser with zero callers
  (`src/Majorsilence.Forms/ControlAndFormParity.cs:358`, `402`); there is no `Control.Region` property
  to change (`grep -rn 'Region Region' src/` finds nothing), so there is genuinely no trigger.
- **Upstream:** raised from the `Region` setter / WM_WINDOWPOSCHANGED.
- **Impact:** none in practice — the property that would raise it does not exist. Listed so the fixer
  does not go looking for a trigger.
- **Fix:** leave it, or add it when `Control.Region` is implemented.
- **Test:** n/a.
- **Tests today:** none.

### EVT-30 — `ScrollableControl.Scroll` / `ScrollBar.Scroll` use the wrong delegate type — Cat E — P2 — High
- **Ours:** `public new event EventHandler<ScrollEventArgs>? Scroll;`
  (`src/Majorsilence.Forms/ScrollableControl.cs:330` and `src/Majorsilence.Forms/ScrollBar.cs:93`) —
  and they hide `Control.Scroll`, which is declared as the correct `ScrollEventHandler` but with empty
  accessors (`src/Majorsilence.Forms/Control.Events.cs`, the `Scroll` line).
- **Upstream:** `public event ScrollEventHandler Scroll` on both `ScrollableControl` and `ScrollBar`.
- **Impact:** designer-generated wiring — `this.panel1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.panel1_Scroll);`
  — does not compile, because `ScrollEventHandler` and `EventHandler<ScrollEventArgs>` are distinct
  delegate types. This is exactly the `*.Designer.cs` case a migration cannot hand-edit. On any control
  that is *not* a `ScrollableControl`, `Scroll` resolves to the empty-accessor `Control.Scroll` and the
  handler is silently dropped instead.
- **Fix:** retype both to `ScrollEventHandler` and delete the empty-accessor `Control.Scroll`, moving
  the declaration to `ScrollableControl` where upstream has it.
- **Test:** compile-level; plus headless: drag a scrollbar thumb and assert one `Scroll` with
  `e.Type == ScrollEventType.ThumbTrack`.
- **Tests today:** none found for the delegate shape.

### EVT-31 — Drag-and-drop event family is never raised — Cat D — P2 — High
- **Ours:** `DragEnter`, `DragOver`, `DragLeave`, `DragDrop`, `GiveFeedback`, `QueryContinueDrag` are
  real `Events`-backed events with real `On*` hooks
  (`src/Majorsilence.Forms/Control.Events.cs` and `src/Majorsilence.Forms/Control.Hooks.cs:196-224`),
  and the code says outright that nothing raises them because there is no OS drag source.
  `Control.AllowDrop` is a stored-only auto-property (`src/Majorsilence.Forms/Control.Compat.cs:390`).
- **Upstream:** raised from the OLE drop target (`Control.OnDragEnter` etc.).
- **Natural trigger that could exist:** the Avalonia backend has `DragDrop` attached events available
  on its presenter/window hosts (`src/Majorsilence.Forms.Avalonia/MajorsilenceFormsPresenter.cs`
  already routes pointer/key input through the same seam); nothing subscribes to them today.
- **Impact:** drag-and-drop is entirely absent. Migrated apps that drop files onto a form, or drag
  rows between grids, do nothing at all — and because `AllowDrop = true` is accepted, the app looks
  configured.
- **Fix:** subscribe the Avalonia hosts to `DragDrop.DragEnter/DragOver/DragLeave/Drop`, translate to
  `DragEventArgs`, and route them through a `Control.RaiseDragOver`-style hit-test dispatcher that
  mirrors `RaiseMouseMove`. Headless can expose `HeadlessRenderer.Input.Drag*` for tests.
- **Test:** headless: `Input.DragEnter/Drop` on a control with `AllowDrop = true`; assert
  `DragEnter` then `DragDrop` fired with the payload.
- **Tests today:** none.

### EVT-32 — `Control.DpiChangedBeforeParent` / `DpiChangedAfterParent` — Cat D — P2 — Medium
- **Ours:** real events with real `On*` raisers and no caller
  (`src/Majorsilence.Forms/Control.Events.cs`, the `DpiChanged*` block plus
  `OnDpiChangedAfterParent`/`OnDpiChangedBeforeParent`).
- **Upstream:** raised from WM_DPICHANGED_BEFOREPARENT/AFTERPARENT during a per-monitor DPI change.
- **Natural trigger that already exists:** `Control.DeviceDpi` is computed from
  `FindWindow ()?.Scaling` (`src/Majorsilence.Forms/Control.cs:524`), and the backends already report a
  scaling change (`WindowBase` reacts to it for layout). That change is the trigger point.
- **Impact:** controls that rescale cached bitmaps/fonts on a DPI change (the `ScaleBitmapLogicalToDevice`
  helper right there in `Control.Hooks.cs` exists for exactly this) never get told to. Dragging a
  window between monitors of different scale leaves blurry cached art.
- **Fix:** when the backend reports a scaling change, walk the tree raising
  `OnDpiChangedBeforeParent` top-down and `OnDpiChangedAfterParent` bottom-up.
- **Test:** headless: change the window scaling; assert both hooks fired once per control.
- **Tests today:** none.

### EVT-33 — `Control.QueryAccessibilityHelp` and `Control.BindingContextChanged` — Cat D — P2 — High
- **Ours:** both declared `{ add { } remove { } }`
  (`src/Majorsilence.Forms/Control.Events.cs`, last block) — handlers attach and are dropped.
- **Upstream:** `BindingContextChanged` is raised from `Control.BindingContext`'s setter and cascades to
  children; `QueryAccessibilityHelp` from the accessible object.
- **Natural trigger that already exists:** `Control` implements `IBindableComponent`
  (`src/Majorsilence.Forms/Control.cs:12`) and has a `BindingContext` — its setter is the trigger.
  (Detail belongs to the binding auditor; noted here only because the empty accessor is a
  silent-drop, which is worse than absence.)
- **Impact:** `BindingContextChanged` is how bound controls know to re-read their data source after a
  form's `BindingContext` is swapped (common in MDI/UserControl hosting).
- **Fix:** back both with `Events`; raise `BindingContextChanged` from the `BindingContext` setter.
- **Test:** binding auditor's area.
- **Tests today:** see `binding.md`.

### EVT-34 — `Control.OnLocationChanged` raises `LocationChanged` before `Move` — Cat A — P2 — High
- **Ours:** `(Events[s_locationChangedEvent] as EventHandler)?.Invoke (this, e); OnMove (e);`
  (`src/Majorsilence.Forms/Control.cs:1286-1293`).
- **Upstream:** `protected virtual void OnLocationChanged(EventArgs e) { OnMove(e); if (Events[s_locationEvent] is EventHandler eh) eh(this, e); }`
  — `Move` first, `LocationChanged` second
  (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:7497-7504`).
- **Impact:** small but real for code that subscribes to both (satellite windows / drop shadows /
  adorner overlays that reposition in `Move` and then recompute in `LocationChanged`): the two run in
  the reverse order, so the recompute uses the pre-move position.
- **Fix:** swap the two lines in `OnLocationChanged`.
- **Test:** headless: subscribe to both, set `Location`; assert `Move` was recorded first.
- **Tests today:** none.

### EVT-35 — `UserControl.ValidateChildren()` and `ContainerControl.ValidateChildren()` still `=> true` — Cat B — P1 — High
- **Ours:** `public bool ValidateChildren () => true;` on both
  (`src/Majorsilence.Forms/UserControl.cs:36` and `:79`), while the `ValidationConstraints` overload
  right beside them is real (`src/Majorsilence.Forms/OverloadParity.Final.cs:26`, `:33`). `Form` had the
  same bug and was fixed (`src/Majorsilence.Forms/Form.cs:934`, whose remark documents exactly this
  trap) — `UserControl` and `ContainerControl` were not.
- **Upstream:** `ContainerControl.ValidateChildren()` is
  `ValidateChildren(ValidationConstraints.Selectable)`
  (`.../Layout/Containers/ContainerControl.cs:1844`).
- **Impact:** the parameterless overload is the one nearly every app calls (`if (!ValidateChildren ()) return;`
  before Save). On a `UserControl`-hosted editor panel it returns `true` without running a single
  `Validating` handler, so invalid data saves silently. This is the fixed-in-`Form`, missed-elsewhere
  half of a known bug.
- **Fix:** one line each: `public bool ValidateChildren () => ValidateChildren (ValidationConstraints.Selectable);`
- **Test:** headless: UserControl containing a TextBox whose `Validating` sets `e.Cancel = true`;
  assert `uc.ValidateChildren ()` returns false.
- **Tests today:** none.

### EVT-36 — `ValidateChildrenCore` does not recurse and short-circuits on the first cancel — Cat A — P2 — High
- **Ours:** a flat `foreach` over the *immediate* children that returns `false` at the first
  `!child.Validate ()` (`src/Majorsilence.Forms/OverloadParity.More.cs:24-40`).
  `ValidationConstraints.ImmediateChildren` is not honoured (it is the only constraint the loop
  ignores).
- **Upstream:** `ValidateChildren` -> `PerformContainerValidation`, which recurses into every
  descendant container unless `ValidationConstraints.ImmediateChildren` is set, and accumulates
  (`failed |= ...`) rather than stopping — so **every** control's `Validating` runs and every error
  provider gets set (`.../Layout/Containers/ContainerControl.cs:1852-1860` and
  `Control.PerformContainerValidation`).
- **Impact:** on a form whose fields sit inside `GroupBox`/`Panel`/`TabPage` — i.e. the normal layout —
  `ValidateChildren ()` validates nothing at all, because the direct children are containers with
  `CanSelect == false`. Where it does reach a field, the first failure hides the rest, so the user
  fixes one error at a time instead of seeing all of them.
- **Fix:** mirror upstream: recurse unless `ImmediateChildren` is set, and accumulate a `failed` flag
  instead of returning early.
- **Test:** headless: Form -> GroupBox -> two TextBoxes both cancelling validation; assert
  `ValidateChildren ()` is false and *both* `Validating` handlers ran.
- **Tests today:** none.

### EVT-37 — `PaintEventArgs.Graphics` is in DEVICE pixels while every control property is logical — Cat A — P1 — High
- **Ours:** the paint canvas for a control is created at `control.ScaledSize` and handed over with no
  transform: `new SKImageInfo (control.ScaledSize.Width, control.ScaledSize.Height, ...)`,
  `new SKCanvas (buffer)`, `new PaintEventArgs (info, canvas, Scaling)`
  (`src/Majorsilence.Forms/Control.cs:1429-1438`). `PaintEventArgs.Graphics` just wraps that canvas —
  `_graphics ??= new Graphics (Canvas)` (`src/Majorsilence.Forms/PaintEventArgs.cs`, the `Graphics`
  property) — and `Graphics` applies no scale of its own (`src/Majorsilence.Forms/Graphics.cs`; the
  only scale is the explicit `ScaleTransform`). The framework's own drawing compensates by using
  `ScaledBounds` (e.g. `OnPaintBackground`, `src/Majorsilence.Forms/Control.cs:1483`), but user code
  cannot: `Width`/`Height`/`ClientRectangle` are logical.
- **Upstream:** `e.Graphics` in `OnPaint` is in the control's own client units, the same units
  `Width`/`Height`/`ClientRectangle` report — the whole point of WinForms' DPI virtualisation.
- **Impact:** on any display where `Scaling != 1` (a Retina Mac is 2.0; Windows at 125%/150% is common)
  the canonical owner-draw line `e.Graphics.DrawRectangle (pen, 0, 0, Width - 1, Height - 1)` draws a
  box at half/two-thirds the control's size, and every hand-drawn control is wrong by the scale
  factor. It is invisible in the headless tests because that backend runs at 96 DPI / scaling 1.
- **Fix:** `canvas.Scale ((float)Scaling)` on the child back-buffer canvas before raising
  `RaisePaintBackground`/`RaisePaint`, and switch the framework's internal drawing from `ScaledBounds`
  to logical bounds in the same pass. (Large change — but the alternative is that every ported
  owner-drawn control is wrong off 96 DPI.)
- **Test:** headless at a forced scaling of 2: a control that fills `new Rectangle (0, 0, Width, Height)`
  in its `Paint` handler; assert the bottom-right device pixel of the back buffer is filled.
- **Tests today:** none at a scaling other than 1.

### EVT-38 — `WindowBase.Paint` (`Form.Paint`) origin is the window frame, not the client area — Cat A — P2 — High
- **Ours:** `RenderFrame` builds one `PaintEventArgs` over the whole physical window surface
  (`new SKImageInfo (physW, physH, ...)`), paints the background and border, calls `OnPaint (e)` and
  raises `Paint?.Invoke (this, e)` — and only *afterwards* clips the canvas to the client rectangle
  (`src/Majorsilence.Forms/WindowBase.cs:414-476`, the clip at `471-473`). No translate is applied, so
  `(0,0)` for a `Form.Paint` handler is the outer top-left of the window, including the managed border
  and title bar.
- **Upstream:** `Form` is a `Control`; WM_PAINT's DC origin is the **client** area, so
  `(0,0)` in `Form.Paint` is just below the title bar and inside the border, and
  `e.ClipRectangle`/`ClientRectangle` agree.
- **Impact:** a form that draws its own watermark, gradient, or grid in `Paint` is offset up-and-left
  by the border + caption height, and can paint over the managed title bar.
- **Fix:** save the canvas, `canvas.Translate (physBorderLeft, physBorderTop + captionHeight)` and clip
  to the client rect *before* `OnPaint`/`Paint`, restore after; combine with EVT-37's `canvas.Scale`.
- **Test:** headless: `form.Paint += (s, e) => e.Graphics.FillRectangle (Brushes.Red, 0, 0, 4, 4);`
  render; assert the red block starts at the client origin, not at pixel (0,0).
- **Tests today:** none.

## Low-priority / Win32-only (P3) — one line each
- `Control.RegionChanged` — see EVT-29; there is no `Control.Region` property to change, so there is no trigger to wire.
- `Form.InputLanguageChanged` / `InputLanguageChanging` (`src/Majorsilence.Forms/Form.cs:405`) — IME/keyboard-layout switching, WM_INPUTLANGCHANGE; no portable notification exists behind Skia/Avalonia.
- `Control.ImeModeChanged` is raised by its setter, but `ImeMode` itself is stored-only — IME composition is a Win32/OS-IME concept with no portable meaning here.
- `Control.StyleChanged` (`src/Majorsilence.Forms/ControlAndFormParity.cs:364`) and `Form.OnStyleChanged` (`src/Majorsilence.Forms/KryptonPortParity.cs:133`) — WS_* window-style bits; the code's own comment says "there is no window style to change".
- `Control.QueryAccessibilityHelp` — the accessible-object side of EVT-33; no accessibility help provider exists in this layer.
- `Control.RecreateHandle` — no HWND, so no handle to recreate.
- `Application.DisplaySettingsChanged` / `Screen`-level notifications — WM_DISPLAYCHANGE, no portable backend event.
- `ToolStripLabel.MouseEnter` / `MouseLeave` re-declared with `{ add { } remove { } }`, hiding the working base events (`src/Majorsilence.Forms/WinFormsCompat.cs:1364`, `1367`) — a silent-drop, but ToolStrip is another auditor's area; flagged for them.
- `WindowBase.Paint`'s `#pragma warning disable CS0067` (`src/Majorsilence.Forms/WindowBase.cs:692-694`) is **stale** — the event *is* raised at `WindowBase.cs:469`. Harmless, but it makes the CS0067 scan over-report; worth deleting so the next audit's signal is clean.
- `WinFormsEvents.cs` (2,108 lines) contains only enums, `EventArgs` subclasses and delegate types — **no `On*` raiser helpers at all**, so the brief's "are `On*` methods present but never called" question does not apply to that file. The never-called raisers live in `ControlAndFormParity.cs` (`OnRegionChanged`, `OnHelpButtonClicked`), `Control.Events.cs` (`OnChangeUICues`, `OnDpiChanged*`) and `Control.Hooks.cs` (the drag-and-drop six) — all covered above.

## Sequences verified as MATCHING upstream (safe — no fix needed)
- **Single-click order.** `MouseDown` (on press) then, on release, `Click` -> `MouseClick` -> `MouseUp` — `WindowBase.HandlePointerReleased` (`WindowBase.cs:1096-1097`) + `Control.RaiseClick` (`Control.cs:1778-1781`) vs `WmMouseUp` (`Control.cs:11694-11710`). The *content* of the sequence is right; only the double-click case (EVT-01) and the released-outside case (EVT-19) diverge.
- **`ControlStyles.StandardClick` / `StandardDoubleClick` gating.** `RaiseClick`/`RaiseDoubleClick` both check the style before raising (`Control.cs:1774-1776`, `1801`), matching `_controlStyle.HasFlag(ControlStyles.StandardClick)` in `WmMouseUp`.
- **Capture routing.** Press sets `Capture = true`, release clears it, and the capture holder receives every move/up wherever the pointer is (`Control.cs:1893-1912`, `2036-2069`) — same rule as upstream's `Capture` handling in `WmMouseDown`/`WmMouseUp`.
- **`Enter`/`GotFocus` and `Leave`/`LostFocus` pairing on a single control.** `OnGotFocus` raises `Enter` then `GotFocus`; `OnLostFocus` raises `Leave` then `LostFocus` (`Control.cs:1227-1247`). Correct within one control — the cross-control ordering is EVT-02/03.
- **Form deactivation does NOT run validation.** `OnBackendDeactivated` raises only the window's `Deactivate` + `LostFocus` (`WindowBase.cs:178-183`); the focused control's `Validating` is untouched. Matches upstream (activation loss does not change `ActiveControl`).
- **`KeyPreview`.** `FormSeesKeyFirst` gives the form `KeyDown` before the focused control and returns early when the form marks it handled (`WindowBase.cs:1338`, `1372-1377`) — matches `Form.ProcessKeyPreview` semantics.
- **`SuppressKeyPress`.** `KeyEventArgs.SuppressKeyPress`'s setter also sets `Handled` (`KeyEventArgs.cs:55-61`), byte-for-byte the upstream implementation, and `Handled` stops the dispatch.
- **Keystroke order.** `PreviewKeyDown` -> `KeyDown` (`Control.cs:1834-1836`), then `KeyPress` from the backend's separate text-input callback, then `KeyUp` — the Avalonia and headless backends deliver KeyDown/TextInput/KeyUp in that order (`MajorsilenceFormsWindowHost.cs:302-316`, `HeadlessRenderer.cs:103-109`).
- **Layout.** `OnLayout` raises the `Layout` event then runs `LayoutEngine.Layout` and propagates `LayoutIsDirty` to the parent (`Control.Layout.cs:334-347`); `PerformLayout` defers while suspended and caches the args (`Control.Layout.cs:403-442`) — a faithful port of upstream `Control.OnLayout`/`PerformLayout`. `LayoutEventArgs.AffectedControl`/`AffectedProperty` are populated at every call site.
- **`SizeChanged` vs `Resize`.** `OnSizeChanged` calls `OnResize` first, and `OnResize` does `ResizeRedraw` -> `Invalidate`, then `LayoutTransaction.DoLayout`, then the `Resize` handlers (`Control.cs:1591-1611`) — identical to upstream `OnSizeChanged`/`OnResize`.
- **Layout suspension during `Controls.Add`.** `Owner.SuspendLayout ()` around `AssignParent`, `LayoutTransaction.DoLayout` then `OnControlAdded` after (`ControlCollection.cs:466-493`) — same shape and same ordering as upstream `Add`.
- **`ControlAdded`/`ControlRemoved`** are raised on the *parent* with `new ControlEventArgs (item)`, last in the operation (`ControlCollection.cs:492`, `586`); `ParentChanged` is raised on the *child* from `AssignParent` (`Control.cs:96`) — all matching.
- **`AssignParent` change notifications.** Old/new `Enabled` and `Visible` are snapshotted and only raised when they actually changed, with upstream's "don't raise on invisible->visible while un-parenting" special case (`Control.cs:83-110`).
- **Ambient cascades.** `OnBackColorChanged`/`OnForeColorChanged`/`OnFontChanged`/`OnRightToLeftChanged` each `Invalidate`, raise, then walk children calling `OnParent*Changed`, which re-raises only when the child has no explicit value of its own (`Control.Hooks.cs:20-110`) — matches upstream, and `OnFontChanged` also does the `PerformLayout` upstream does.
- **`EnabledChanged` / `VisibleChanged` propagation.** `OnEnabledChanged` cascades via `OnParentEnabledChanged`, guarded by the child's own `States.Enabled` (`Control.cs:1211-1222`, `1559-1563`) — exactly upstream's guard.
- **`TextChanged`.** The `Text` setter coerces null to empty, returns early when unchanged, and only then raises (`Control.cs:2609-2625`) — matches.
- **Paint pass.** `OnPaintBackground` -> `OnPaint` -> children, children in reverse z-order so index 0 lands on top, and hidden/zero-size children skipped (`Control.cs:1420-1446`, `2182-2192`). `Invalidate` during `Paint` survives, because `States.IsDirty` is cleared *before* `OnPaint` (`Control.cs:2184-2188`) — deliberate and correct. (The `Paint`-outside-`OnPaint` split is EVT-20; the coordinate space is EVT-37/38.)
- **`MouseCaptureChanged`.** Raised on both edges of the `Capture` setter, once per real transition (`Control.cs:198-224`).

## Systemic patterns
- **The focus choke point is split in two, and each half raises events itself.** `Control.Select ()` raises `Enter`/`GotFocus` and *then* tells `ControlAdapter.SelectedControl`, whose setter raises `Leave`/`LostFocus` on the previous control. Because both halves raise, the order depends on which one the caller entered through — mouse (via `Select`) gets it backwards, Tab (via the setter) gets it right. Every focus finding (EVT-02, 03, 04, 05, 21) collapses into "make `ControlAdapter.SelectedControl`'s setter the only place focus events are raised, and give it the full upstream `UpdateFocusedControl` shape: Leave-up, validate, Enter-down, LostFocus, GotFocus."
- **The whole `PreProcessMessage` layer is missing.** `ProcessCmdKey`, `ProcessDialogKey`, `ProcessKeyPreview`, `IsInputKey`, `PreProcessMessage` all exist as `=> false` virtuals with no caller, and the two behaviours they are supposed to gate (AcceptButton/CancelButton, Tab navigation) have instead been hard-coded at the *top* of the dispatch where nothing can override them. EVT-06, 07, 08, 09 are one fix: build the upstream pre-process chain in `WindowBase.HandleKeyDown` and move the hard-coded shortcuts into it.
- **"Do it eagerly at `Controls.Add` time" instead of "at create/show time".** `ControlCollection.Add` creates handles and raises `VisibleChanged` unconditionally, which shifts `HandleCreated`, `UserControl.Load` and `VisibleChanged` from the show sequence into `InitializeComponent`, and makes `Created`/`IsHandleCreated` true from construction (EVT-13, and the `IsHandleCreated` half of EVT-10).
- **Repaint is always deferred; nothing in this layer is synchronous.** `Refresh`, `Update`, `Invalidate` and `Invalidate(rect)` all collapse to "set a dirty bit and return" (EVT-17, EVT-18). Anything whose WinForms contract is "paint now" or "paint only this rectangle" is lost.
- **Coordinate space: logical for properties, device for canvases.** Every control property is logical; every paint canvas is physical, with the framework compensating internally via `ScaledBounds` and user code left without that option (EVT-37, EVT-38). This is invisible in the headless tests because they run at scaling 1 — worth adding a scaled headless fixture, which would catch a whole class of bugs at once.
- **Two mutually exclusive events raised as if they were independent.** The double-click path fires `DoubleClick` *and* `Click` (EVT-01) because the release handler treats them as separate concerns; upstream models it as one `if/else` on a `DoubleClickFired` state bit.
- **An event exists, its `On*` raiser exists, and the one line that would call it is commented out or absent.** `OnClientSizeChanged` (commented out, EVT-23), `AfterControlRemoved` (commented TODO, EVT-21), `ApplicationExit` (an internal `OnExit` is raised on the very next line, EVT-25), `RaiseIdle`/`RaiseEnterThreadModal`/`RaiseLeaveThreadModal`/`RaiseThreadExit` (helpers with no caller, EVT-24/26). These are the cheapest fixes in the file — mostly one line each.
- **A stub fixed in one type and missed in its siblings.** `ValidateChildren ()` was made real on `Form` (with a remark explaining why the no-arg overload is the one that matters) and left `=> true` on `UserControl` and `ContainerControl` (EVT-35). Worth grepping for other `=> true`/`=> false` parity stubs whose sibling was fixed.
