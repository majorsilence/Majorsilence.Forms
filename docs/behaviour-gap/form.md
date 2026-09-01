# Form / WindowBase / Application / MDI / message loop — findings

## Summary
`Form : WindowBase : Component` re-declares the whole Control surface by hand and forwards most of it to
an internal `ControlAdapter`, so the name-level surface is complete; the behavioural gaps are in the window
lifecycle and the message-loop seams that WinForms gets from Win32 and this layer has to synthesise. Four
patterns dominate: (1) **one-shot lifecycle flags** (`_loadFired`, `_formClosedFired`, `shown`, `visible`,
`dialog_result`) are never reset, so a form is only correct the first time it is shown/closed — reusing a
dialog instance breaks in three independent ways; (2) **key-processing hooks are declared but never
dispatched** (`ProcessCmdKey`/`ProcessDialogKey`/`ProcessMnemonic`/`ProcessKeyPreview` have zero call sites) while
the real routing lives in `WindowBase.HandleKeyDown` with its own, different rules; (3) the **custom title bar
is a child of the client area**, so `ClientSize` and designer coordinates are off by the caption height on
Windows/Linux; (4) **`Application` lifecycle events are inert** (`ApplicationExit` discards handlers,
`Idle`/`ThreadException` are never raised by the loop, `Exit` skips FormClosing/FormClosed, `Restart` just quits).
Counts: **P0 x4, P1 x22, P2 x13**, plus a P3 list. The MDI emulation itself (activation order, LayoutMdi,
MdiChildActivate) is comparatively solid and produced no P0/P1 findings of its own.

## Findings

### FRM-01 — `Form.ShowDialog()` (no open owner) — Cat A — P0 — High
- **Ours:** When `Application.ModalOwnerCandidates` yields nothing (first form of the app, or every open form is frame-hosted), `ShowDialog()` calls `Show()` and returns `DialogResult.OK` immediately, non-modally (`src/Majorsilence.Forms/Form.cs:731-741`). `MessageBox.Show(text, caption, buttons, icon)` does the same (`src/Majorsilence.Forms/WinFormsCompat.cs:910-920`).
- **Upstream:** `ShowDialog(IWin32Window?)` always runs `Application.RunDialog(this)` regardless of owner; a null owner just means the active window becomes the parent HWND (`src/System.Windows.Forms/System/Windows/Forms/Form.cs:5662-5830`, loop at 5786-5789).
- **Impact:** The universal "login/splash dialog before `Application.Run(main)`" pattern (`if (new LoginForm().ShowDialog() != DialogResult.OK) return;`) sees OK instantly with nothing filled in and continues into the main form; the login window is left floating. Same for any `MessageBox.Show` before the first form.
- **Fix:** In `ShowDialog()` when no owner is found, still create `dialog_task`, call `Backend.Show()`/`EnsureShownBookkeeping`, and `RunModal(dialog_task.Task)`; `CompleteClose` already handles `dialog_parent == null`. Same for `MessageBox.Show` (route to the ownerless modal path).
- **Test:** Headless: with `OpenForms` empty, start `ShowDialog()` on a thread-safe posted `DialogResult = Cancel` after N ticks; assert the call blocks until then and returns `Cancel`, not `OK`.
- **Tests today:** `LoadTimeDialogModalityTests.cs` covers the Load-time case only; none for the empty-OpenForms case.

### FRM-02 — `Form.ShowDialog` re-entry (stale `DialogResult`) — Cat A — P0 — High
- **Ours:** `ShowDialogAsync` returns immediately with the previous result if `dialog_result != None` (`src/Majorsilence.Forms/Form.cs:821-824`). Nothing ever resets `dialog_result` (only the setter at 485-493 writes it, and `CompleteClose` at 287-313 does not clear it).
- **Upstream:** `ShowDialog` sets `_dialogResult = DialogResult.None` before showing (`Form.cs:5732`) — the "already set" short-circuit at 5786 only fires if a handler set it during `CreateControl`/`OnLoad`.
- **Impact:** Any dialog kept as a field and shown twice (`if (_findDialog.ShowDialog() == OK) ...`, Options dialogs, MessageBoxForm reuse) returns the previous answer without appearing on the second call. Silent and very common.
- **Fix:** At the top of `ShowDialogAsync` (after `dialog_task` creation) set `dialog_result = DialogResult.None`, then let `OnLoad` set it if it wants; also reset `_formClosedFired`/`_loadFired`/`shown` (see FRM-03) and `visible` (FRM-04).
- **Test:** Headless: `f.DialogResult = OK` via a button click during first `ShowDialog`; second `ShowDialog` must raise `Load`/`Shown` again and block until a new result is set.
- **Tests today:** none.

### FRM-03 — `Form.Load` / `Shown` / `FormClosed` fire once per *instance* — Cat A — P1 — High
- **Ours:** `_loadFired` (`Form.cs:416-428`), `shown` (`KryptonPortParity.cs:115-122`, `WindowBase.cs:1731-1741`) and `_formClosedFired` (`Form.cs:327-343`) are never reset after a close, so the second `ShowDialog`/`Show` of a closed-but-not-disposed form raises neither `Load` nor `Shown`, and its second close raises no `FormClosed`.
- **Upstream:** `ShowDialog` resets `CalledOnLoad = false; CalledMakeVisible = false` (`Form.cs:5703-5704`) and destroys the handle on exit (5806), so `Load`, `Shown` and `FormClosed` fire on every modal cycle; `Dispose` also resets them (3516-3518).
- **Impact:** Dialogs that (re)populate controls in `Load` show stale data on reuse; `FormClosed` cleanup runs only once; `IsHandleCreated`/`Created` stay `true` forever after the first show.
- **Fix:** Reset `_loadFired`, `_formClosedFired` and `shown` in `OnBackendClosed` (after `RaiseDestroyHandle`), mirroring upstream's handle destruction.
- **Test:** Show/close/show a form headless; assert `Load` count == 2 and `FormClosed` count == 2 after the second close.
- **Tests today:** `FormLoadShownOrderTests.Load_fires_exactly_once` pins the *first* cycle only.

### FRM-04 — `WindowBase.Visible` stays `true` after `Close()` — Cat A — P1 — High
- **Ours:** `visible` is set false only in `Hide()` and `Dispose()` (`WindowBase.cs:574, 260`); `OnBackendClosed` (48-78) never clears it. After `Close()`, `form.Visible == true`, and a later `Show()` skips `EnsureShownBookkeeping` (1701-1702) so the form never re-joins `OpenForms` and raises no `VisibleChanged`.
- **Upstream:** `SetVisibleCore(false)` runs in the `ShowDialog` finally block (`Form.cs:5793`) and the handle is destroyed, so `Visible` is false after any close.
- **Impact:** Code gating on `dialog.Visible` (e.g. "if (!find.Visible) find.Show(); else find.Activate();") believes a closed tool window is still open; re-shown dialogs are invisible to `Application.OpenForms`/`ActiveForm`.
- **Fix:** In `OnBackendClosed`, set `visible = false` and raise `OnVisibleChanged` before `OnClosed`.
- **Test:** Headless: show, `Close()`, assert `Visible == false` and `OpenForms` does not contain it; then `Show()` again and assert it is re-added.
- **Tests today:** `FormDisposeClosesWindowTests.Disposing_a_shown_form_marks_it_not_visible` covers Dispose only.

### FRM-05 — `WindowBase.ProcessCmdKey` / `ProcessDialogKey` / `ProcessKeyPreview` never invoked — Cat B — P0 — High
- **Ours:** All four are `protected virtual ... => false` with no call sites anywhere in `src/` (`src/Majorsilence.Forms/WindowBase.Compat.cs:34-53`; grep shows only declarations). Key routing is done directly in `WindowBase.HandleKeyDown` (`WindowBase.cs:1341-1383`) → `OnKeyDown` → `adapter.RaiseKeyDown`.
- **Upstream:** Every WM_KEYDOWN goes through `Control.PreProcessMessage`, which calls `ProcessCmdKey` first, then `IsInputKey`, then `ProcessDialogKey`, each climbing the parent chain to the Form (`Control.cs:8636-8680, 8784-8785, 8905`); `Form.ProcessKeyPreview` implements `KeyPreview` (`Form.cs:4806-4813`).
- **Impact:** `protected override bool ProcessCmdKey(ref Message msg, Keys keyData)` on a Form is the standard way LOB apps implement Ctrl+S / F5 / Escape-to-close shortcuts. It compiles, is never called, and every such shortcut is dead. Overrides of `ProcessDialogKey` (custom Enter/Tab handling) likewise.
- **Fix:** In `HandleKeyDown`, before anything else, build a `Message` (`WM_KEYDOWN`, wParam = key) and call `ProcessCmdKey(ref m, keys)`; if false, call `ProcessDialogKey(keys)` for non-input keys (Return/Escape/Tab/arrows) and move the Accept/Cancel logic into the base `ProcessDialogKey` so overrides can call `base`. Do the same on the focused `Control` chain (Control.Compat.cs has the same dead hooks — coordinate with the Control auditor).
- **Test:** Headless: subclass Form overriding `ProcessCmdKey` to record `keyData`; call `HandleKeyDown(Keys.S | Keys.Control)`; assert it was seen and, when it returns true, the focused TextBox did not receive KeyDown.
- **Tests today:** `FormTests.KeyEvents_*` cover KeyPreview event routing only.

### FRM-06 — `Form.ClientSize` / designer coordinates vs. the in-client `TitleBar` — Cat A — P0 — High
- **Ours:** `FormTitleBar` is an implicit child of the client `Controls` (`Form.cs:49`), `Dock = Top`, default height 34 (`FormTitleBar.cs:32, 165`). `Form.Size`, `ClientSize` and `ClientRectangle` all report `Backend.ClientSize` (`Form.cs:850-880, 898-901`; `WindowBase.cs:560-561`), which *includes* that strip. Absolutely-positioned/anchored children are not offset — the repo's own tests document it: "an absolutely-positioned sibling ... is not shifted down to clear it" (`tests/Majorsilence.Forms.Tests/GestureTests.cs:14-18`).
- **Upstream:** `ClientSize` is the area *inside* the non-client frame; the caption lives in the non-client region and (0,0) is below it (`Form.cs:727-731`, `SetClientSizeCore` 5355-5386).
- **Impact:** On Windows/Linux (custom chrome by default; `Form.cs:59-60` only picks system decorations on macOS) every designer form loses its top 34px under the title bar and its bottom 34px off the window: a `ClientSize = (800, 450)` form gets 416px of usable height, the first row of controls is hidden behind the caption, and `Anchor = Bottom` rows are clipped. Also affects `PointToClient`, `ClientRectangle` hit tests, and `MessageBoxForm` sizing.
- **Fix:** Either (a) exclude the title bar from the client model: have `Form.ClientSize` get/set `Backend.ClientSize` minus `TitleBar.Height` (+border) when `TitleBar.Visible`, place the adapter below the title bar (`SyncAdapterBounds` → `adapter.SetBounds(border, border + titleBarHeight, w, h - titleBarHeight)`) and keep `TitleBar` outside `adapter`; or (b) on the frame-hosted/system-decorations path leave as is. `Size` must then report client + caption.
- **Test:** Headless: `new Form { ClientSize = (400, 300) }`, add `Button { Location = (0,0) }`, `Show()`; assert `button.PointToScreen(0,0).Y - form.PointToScreen(0,0).Y == TitleBar.Height` (or that `form.ClientRectangle.Height == 300` and the button's screen rect does not intersect the title bar's).
- **Tests today:** none asserting the WinForms contract; `GestureTests`/`MenuClickReproTests` encode the current (divergent) behaviour.

### FRM-07 — `Form.Close()` does not dispose a non-modal form — Cat A — P1 — High
- **Ours:** `Form.Close` → `WindowBase.Close` → `Backend.Close()` → `OnBackendClosed` (`Form.cs:232-273`, `WindowBase.cs:205-226, 48-78`). No `Dispose()` call anywhere on that path; `IsDisposed` stays false, `Disposed` never fires.
- **Upstream:** `WmClose` ends with `Dispose()` for a non-modal form (`Form.cs:6812-6821`); `Close()` on a form with no handle disposes directly (3253-3257). Only *modal* forms survive closing (`if (Modal) return;` at 6771).
- **Impact:** Designer `components` (Timers, BindingSources, ToolTips) are disposed by the form's `Dispose(bool)` override, which never runs: a `Timer` started in a closed form keeps ticking against a dead form; `Disposed` handlers used for cleanup never run; `using`-less code that leaked forms relies on Close to free them.
- **Fix:** In `Form.Close()`/`OnBackendClosed`, after `FormClosed`, call `Dispose()` when the form is not currently modal (`dialog_task is null`) — matching `WmClose`.
- **Test:** Headless: subclass with a `Timer` in `components`; `Show(); Close();` assert `IsDisposed` and `Disposed` fired, and the timer's `Enabled` is false.
- **Tests today:** `FormClosingClosedOrderTests.cs` asserts the event order only.

### FRM-08 — `Form.AcceptButton` / `CancelButton` fire before the focused control, ignore Alt/Ctrl, no "focused button is default" — Cat A — P1 — High
- **Ours:** `HandleKeyDown` clicks `AcceptButton` on *any* Return and `CancelButton` on *any* Escape before `KeyPreview`, before the focused control, and without checking modifiers (`WindowBase.cs:1350-1368`). The focused `Button` also handles Enter itself on KeyUp (`Button.cs:221-224`), so both the AcceptButton and the focused button can click.
- **Upstream:** `Form.ProcessDialogKey` runs only after the focused control's `IsInputKey`/`ProcessDialogKey` declined, skips when Alt/Control is held, and clicks the *default* button — which `UpdateDefaultButton` sets to the focused `IButtonControl` when there is one, else `AcceptButton` (`Form.cs:4732-4776, 6079-6106`; `Control.cs:8647-8660`).
- **Impact:** Enter in a multiline `TextBox` (AcceptsReturn), a `ComboBox` with an open list, or a `DataGridView` edit commits by closing the dialog; Tab to "Cancel" then Enter fires OK; Ctrl+Enter/Alt+Enter also fire OK; Escape while a drop-down is open cancels the whole dialog.
- **Fix:** Move Accept/Cancel into `WindowBase.ProcessDialogKey`, call it only after `adapter.SelectedControl` has declined (its own `ProcessDialogKey`/`IsInputKey`), require `(keys & (Alt|Control)) == 0`, and click `SelectedControl as IButtonControl ?? AcceptButton`.
- **Test:** Headless: form with AcceptButton=OK, focused Cancel button; `HandleKeyDown(Return)` → only Cancel clicked. Focused multiline TextBox → OK not clicked.
- **Tests today:** `FormTests.AcceptButton_Set_GetReturnsExpected` (storage only).

### FRM-09 — `WindowBase.ProcessMnemonic` never called (Alt+letter dead) — Cat B — P1 — High
- **Ours:** `ProcessMnemonic` is `=> false` and has no caller (`WindowBase.Compat.cs:53`); `HandleKeyDown`/`HandleTextInput` never look at `Keys.Alt`; only `Label.UseMnemonic` (drawing) and `Control.IsMnemonic` exist.
- **Upstream:** WM_SYSCHAR → `ProcessDialogChar` → `ContainerControl.ProcessMnemonic` walks children calling `Button/Label/CheckBox.ProcessMnemonic` (`Control.cs:8670, 8891`; `Form.cs:3854`).
- **Impact:** Every `&Save`/`&Cancel` button and `&Name:` label mnemonic in ported dialogs is inert; keyboard-only users cannot operate dialogs; `MenuStrip` Alt-navigation likewise.
- **Fix:** In `HandleKeyDown` when `Alt` is held and the key is a letter/digit, call `ProcessMnemonic(char)` on the window, whose base walks `adapter.Controls.GetAllControls()` calling each control's `ProcessMnemonic` (implement on `ButtonBase`/`Label`/`CheckBox`/`RadioButton`/`MenuStrip`).
- **Test:** Headless: button `Text = "&Save"`; `HandleKeyDown(Keys.S | Keys.Alt)` → Click raised.
- **Tests today:** none.

### FRM-10 — `Form.ActiveControl` getter returns the first control in tab order — Cat A — P1 — High
- **Ours:** `get => adapter.GetNextControl(null, true)` (`Form.cs:1124-1127`); with `start == null`, `GetNextControl` returns `GetFirstChildControlInTabOrder` (`Control.cs:721-735`). The actual focus holder is `adapter.SelectedControl` (`ControlAdapter.cs:74-88`); `GetFocusedControl()` (`Form.cs:1601`) does the right walk.
- **Upstream:** `ContainerControl.ActiveControl` returns `_activeControl`, the focused control (`Layout/Containers/ContainerControl.cs:283-287`).
- **Impact:** Edit → Copy/Paste handlers (`if (ActiveControl is TextBoxBase tb) tb.Copy()`), "which grid has focus" toolbar logic, and `ActiveControl.Focus()` on re-activation all act on the wrong control (always the first one).
- **Fix:** `get => adapter.SelectedControl` (or the innermost `ContainerControl.ActiveControl` chain); setter: `null` should deselect.
- **Test:** Headless: two TextBoxes, `tb2.Select()`; assert `form.ActiveControl == tb2`.
- **Tests today:** none.

### FRM-11 — `Form.ActiveForm` returns the most recently *opened* form — Cat A — P1 — High
- **Ours:** `=> Application.OpenForms.LastOrDefault()` (`Form.cs:895`).
- **Upstream:** `FromHandle(GetForegroundWindow()) as Form` — the form that currently has activation (`Form.cs:286`).
- **Impact:** Multi-window apps (main + tool windows, MDI shells with floating windows) use `Form.ActiveForm` to decide where to route a command or centre a dialog; here it always names the newest window, even after the user clicks back to the main one.
- **Fix:** Track `WindowBase.IsActive` (already maintained by `OnBackendActivated`/`Deactivated`, `WindowBase.cs:109-117, 175-184`) and return `OpenForms.LastOrDefault(f => f.PresentationWindow.IsActive)`.
- **Test:** Headless: show A then B, call `A.Backend`'s `OnBackendActivated()`; assert `ActiveForm == A`.
- **Tests today:** none.

### FRM-12 — `Form.Modal` is never set — Cat A — P1 — High
- **Ours:** `public bool Modal { get; private set; }` (`Form.cs:1591`); no assignment anywhere in `src/Majorsilence.Forms/*.cs`.
- **Upstream:** `SetState(States.Modal, true)` for the duration of `ShowDialog` (`Form.cs:5722, 5809`); `Modal => GetState(States.Modal)` (1554).
- **Impact:** The idiom `if (Modal) DialogResult = OK; else Close();` (forms usable both as dialog and as window) always takes the `Close()` branch — which works here by accident only because `DialogResult`'s setter closes; `if (Modal) ...` guards in `FormClosing` never run.
- **Fix:** Set `Modal = true` in `ShowDialogAsync` before `base.ShowDialog(parent)` and `false` in `CompleteClose`.
- **Test:** Headless: inside a `Load` handler during `ShowDialog`, assert `Modal == true`; after return, `false`.
- **Tests today:** none.

### FRM-13 — `Hide()` on a modal dialog does not end `ShowDialog` — Cat A — P1 — High
- **Ours:** `WindowBase.Hide` sets `visible=false`, hides the backend, raises `VisibleChanged` (`WindowBase.cs:572-589`); `dialog_task` is untouched, so `RunModal` keeps pumping and the owner stays disabled (`ShowDialog(WindowBase)` disabled it at 1687).
- **Upstream:** the modal loop exits when `CheckCloseDialog` sees `!Visible` (`Form.cs:3232`), returning `DialogResult.Cancel`.
- **Impact:** Dialogs written as `this.Hide()` (older code, wizards that hide themselves before launching another window) leave `ShowDialog` blocked forever and the parent disabled — the app appears hung.
- **Fix:** In `Hide()` (or an override in `Form`), if `dialog_task is not null`, set `dialog_result = Cancel` when `None` and call `CompleteClose()`.
- **Test:** Headless: start `ShowDialog`, post `Hide()`; assert it returns `Cancel`.
- **Tests today:** none.

### FRM-14 — Modal dialog disables only its owner window — Cat A — P1 — High
- **Ours:** `WindowBase.ShowDialog(parent)` sets `parentWindow.Backend.Enabled = false` on the one owner (`WindowBase.cs:1678-1690`); `CompleteClose` re-enables only that one.
- **Upstream:** `DisableWindowsForModalLoop` disables every top-level window of the thread (`Application.ThreadContext.cs:247-258`, called from 232).
- **Impact:** With two open forms, a dialog raised from A leaves B fully interactive; B can open a second dialog, close A's owner, or call `Application.Exit` mid-modal. Typical in MDI shells with floating tool windows.
- **Fix:** In `ShowDialog(WindowBase)`, disable every `Application.ModalOwnerCandidates` window (and `PopupWindow`s) except the dialog, remembering which were enabled; restore in `CompleteClose`.
- **Test:** Headless: show A and B; `ShowDialog` from A; assert `B.Backend.Enabled == false` during and `true` after.
- **Tests today:** `ModalOwnerHostedFormTests` covers owner re-enable only.

### FRM-15 — `Form.Owner` / `OwnedForms` / `Show(owner)` / `ShowDialog(owner)` are not wired — Cat A — P1 — High
- **Ours:** `Owner { get; set; }` is a bare auto-property (`Form.cs:1440`) — setting it does not add to the owner's `OwnedForms`; `Show(IWin32Window)` and `ShowDialog(IWin32Window)` discard the argument (`Form.cs:744-747`); `ShowDialog(Form)` never assigns `Owner`; closing/disposing a form does nothing to `_ownedForms`.
- **Upstream:** the `Owner` setter calls `ownerOld.RemoveOwnedForm(this)` / `value.AddOwnedForm(this)` (`Form.cs:1622-1650`); `Show(owner)`/`ShowDialog(owner)` set `Owner` (5476-5481, 5779-5781); `WmClose` raises `FormClosing`/`FormClosed` on owned forms with `CloseReason.FormOwnerClosing` (6735-6745, 6800-6810); `Dispose` disposes owned forms (3545-3552).
- **Impact:** Tool windows opened with `Show(this)` are not kept above / minimised with / closed with their owner; `MessageBox.Show(owner, …)` centres on the wrong window (`ShowDialog(IWin32Window)` picks `OpenForms.First()`); `OwnedForms` is empty unless code called `AddOwnedForm` explicitly.
- **Fix:** Make `Owner`'s setter call `AddOwnedForm`/`RemoveOwnedForm`; have `Show(IWin32Window)`/`ShowDialog(IWin32Window)` resolve `owner as Form ?? (owner as Control)?.FindForm()` and use it as `dialog_parent`/`Owner`; in `Close()` and `Dispose()` iterate `OwnedForms` (close/dispose) and raise their events with `FormOwnerClosing`.
- **Test:** Headless: `child.Show(parent)` → `parent.OwnedForms` contains child and `child.Owner == parent`; `parent.Close()` → child `FormClosed` with `FormOwnerClosing`.
- **Tests today:** `FormTests.Owner_Set_GetReturnsExpected`, `AddOwnedForm_*` (storage only).

### FRM-16 — `IsHandleCreated` is `false` during `Load`; `OnHandleCreated` fires *after* `Load` — Cat A — P1 — High
- **Ours:** `EnsureShownBookkeeping` calls `EnsureLoaded()` (→ `OnLoad`) at `WindowBase.cs:1721`, then `MarkHandleCreated()` (sets `shown`, raises `OnHandleCreated`) at 1733; `IsHandleCreated => shown` (`WindowBase.cs:2085`), `Created => shown` (1840). Same order on the hosted paths (`Form.cs:1371-1424`). `HandleCreated` *event* is a different object entirely (forwarded to `adapter.HandleCreated`, `WindowBase.cs:2042-2045`, raised by `Control.CreateControl`).
- **Upstream:** the handle exists before `OnLoad`: `OnCreateControl` (after `CreateHandle`) raises `OnLoad` (`Form.cs:4101-4111`), and `OnLoad` ends with `if (IsHandleCreated) BeginInvoke(CallShownEvent)` (4321-4326). Order is HandleCreated → Load → VisibleChanged → Activated → Shown.
- **Impact:** `Load` handlers that guard with `if (!IsHandleCreated) return;` (common in refresh routines shared with timers), or `if (IsHandleCreated) BeginInvoke(...)`, silently skip; state set up in an `OnHandleCreated` override (fonts, caches, native-ish setup) is not yet there when `OnLoad` runs; subscribers to `HandleCreated` and overriders of `OnHandleCreated` fire at two different moments.
- **Fix:** Call `MarkHandleCreated()` (and `adapter.CreateControl()`) *before* `EnsureLoaded()` in `EnsureShownBookkeeping` and the three `TryShowHosted` branches; keep `OnShown` after.
- **Test:** Headless: record order of `OnHandleCreated`/`Load`/`Shown` overrides and `IsHandleCreated` inside `Load`; assert `H, L, S` and `true`.
- **Tests today:** `FormHandleCreatedTests.OnHandleCreated_precedes_OnShown` (does not check Load), `FormLoadShownOrderTests`.

### FRM-17 — `Form.AutoScaleMode` / `AutoScaleDimensions` stored-only (no font/DPI autoscale) — Cat C — P1 — High
- **CLOSED 2026-08-31 (`W3.6`)** for `AutoScaleMode.Font`, on `Form`, `ContainerControl` and
  `UserControl`, through one shared `AutoScaleEngine` (`src/Majorsilence.Forms/AutoScale.cs`); 11 tests
  in `AutoScaleTests.cs`. Three corrections to the fix as written above: the scale has to run from a new
  pre-show hook rather than `EnsureShownBookkeeping` (which is after `Backend.Show ()`, where a
  `Form.Size` write is a backend no-op); the one-shot flag has to be armed by the
  `AutoScaleMode`/`AutoScaleDimensions`/`Font` setters rather than consumed by the first layout, because
  `Controls.Add` triggers a layout before the designer assigns the dimensions; and `AutoScaleMode.Dpi` is
  deliberately left inert, because `Bounds` here are logical and the backend already applies the display
  factor, so a dpi ratio would scale every form twice. See "What W3.6 found" in the plan.
- **Ours:** Both are auto-properties on `Form` (`Form.cs:919-922`); `PerformAutoScale`/`CurrentAutoScaleDimensions` exist only on `ContainerControl` (`RemainingMemberParity.cs:138-158`, and its "Font" width formula `Font.Size * 2f` is not the upstream average-char-width metric). Nothing on the Form path reads `AutoScaleDimensions`. Same for `UserControl` (`UserControl.cs:24-27, 73-76`).
- **Upstream:** `ContainerControl.OnLayout` → `PerformNeededAutoScaleOnLayout` scales all children by `CurrentAutoScaleDimensions / AutoScaleDimensions` on first layout, where the current dimensions are the live font's `tmAveCharWidth`/`tmHeight` (`ContainerControl.cs:884-888, 306-330, 700-740, 931`).
- **Impact:** Every designer file records `AutoScaleDimensions = new SizeF(6F, 13F)` (Segoe UI 9pt @96dpi). The default font here is a different family and size (`SystemFonts.cs:21, 51`: 9pt / 8.25pt DejaVu/SF/Segoe fallback) with a different average glyph width, so text laid out for 6px/char is now measured at ~7px/char with no compensating scale: truncated labels, buttons whose captions ellipsize, TableLayout columns too narrow — the classic "WinForms on a different font" breakage that AutoScale exists to prevent. Also no scaling when `Application.SetDefaultFont` changes the default.
- **Fix:** Give `Form` the same `CurrentAutoScaleDimensions`/`PerformAutoScale` as `ContainerControl` (measure `tmAveCharWidth` equivalent via `TextMeasurer` over the upstream `FontMeasureString`), and call `PerformAutoScale()` once from `EnsureShownBookkeeping` before `EnsureLoaded` when `AutoScaleMode == Font && !AutoScaleDimensions.IsEmpty` (upstream: on first layout / handle creation). Make it no-op when the ratio is 1.
- **Test:** Headless: form with `AutoScaleDimensions=(6,13)`, `Font` set so current dims are (9,19.5); button at (100,100) size (75,23) → after `Show()`, bounds scaled by (1.5,1.5).
- **Tests today:** none.

### FRM-18 — `Form.MaximizeBox` controls window resizability — Cat A — P1 — High
- **Ours:** `MaximizeBox { get => Backend.CanResize; set => Backend.CanResize = value; }` (`Form.cs:991-994`), while the custom title bar's maximize button is a separate `AllowMaximize`/`TitleBar.AllowMaximize` (191-194).
- **Upstream:** `MaximizeBox` toggles `WS_MAXIMIZEBOX` (`Form.cs:1389-1397`); resizability is `FormBorderStyle`.
- **Impact:** `MaximizeBox = false` on a `Sizable` form (very common: "resizable but not maximizable" dialogs, and every designer-generated dialog sets `MaximizeBox = false; MinimizeBox = false`) makes the window non-resizable; meanwhile the drawn maximize glyph stays visible and functional.
- **Fix:** Store the flag, forward to `TitleBar.AllowMaximize` (and `MdiChildWindow` already reads it), and leave `Backend.CanResize` to `FormBorderStyle`.
- **Test:** Headless: `FormBorderStyle = Sizable; MaximizeBox = false` → `Backend.CanResize == true`, `TitleBar.AllowMaximize == false`.
- **Tests today:** `FormTests.MaximizeBox_Set_GetReturnsExpected` (round-trip only).

### FRM-19 — `Form.MinimizeBox` / `ControlBox` stored-only for top-level forms — Cat C — P1 — High
- **Ours:** `MinimizeBox { get; set; } = true` (`Form.cs:997`) and `ControlBox { get; set; } = true` (1574) are read only by `MdiChildWindow` (`MdiChildWindow.cs:148, 196-197`); the top-level `FormTitleBar` uses its own `AllowMinimize`/`AllowMaximize` and always shows the close button.
- **Upstream:** both drive `WS_MINIMIZEBOX`/`WS_SYSMENU` via `UpdateFormStyles` (`Form.cs:1537-1545, 740-748`); `ControlBox = false` removes all caption buttons and the icon.
- **Impact:** Dialogs that hide minimise (`MinimizeBox = false`) still show it; kiosk/splash/progress forms that set `ControlBox = false` to prevent the user closing them still get a working close button.
- **Fix:** Forward `MinimizeBox` → `TitleBar.AllowMinimize`, `MaximizeBox` → `TitleBar.AllowMaximize`, and add a `TitleBar.ShowControlBox` that hides all three buttons + icon; on the system-decorations path forward to a backend `CanMinimize`/`CanClose` where available.
- **Test:** Headless: `ControlBox = false; Show()` → title bar `CaptionButtonsWidth == 0`.
- **Tests today:** round-trip tests only.

### FRM-20 — `Form.CenterToScreen()` / `CenterToParent()` do not move the window — Cat A — P1 — High
- **Ours:** `CenterToScreen` only assigns `StartPosition = CenterScreen` (and does nothing when it is `Manual`), so it has an effect only if called before the first `Show()` (`Form.cs:1527-1531`); `CenterToParent` without an `Owner` falls through to it (1534-1543). `CenterParent` at show time also does nothing when there is no owner (`SetWindowStartupLocation`, `Form.cs:794-804`).
- **Upstream:** `CenterToScreen` computes the working-area centre and sets `Location` immediately (`Form.cs:3945-3968`); `CenterToParent` falls back to `CenterToScreen` (3894-3942); WinForms invokes these at first show for `CenterScreen`/`CenterParent`.
- **Impact:** `CenterToScreen()` called from `Load`/after a resize (the documented way to recentre a form whose size was computed at runtime) is a no-op; forms with `StartPosition = Manual` can never be centred programmatically; `CenterParent` with no owner appears at the OS default position instead of screen centre.
- **Fix:** Implement `CenterToScreen` as upstream (working area of `Screen.FromPoint(Location)`), and have `SetWindowStartupLocation` call it for `CenterScreen`, and for `CenterParent` when `owner == null`.
- **Test:** Headless (`HeadlessPlatformBackend.GetScreens` is fixed): after `CenterToScreen()`, `Location == ((wa.Width - Width)/2, (wa.Height - Height)/2)`.
- **Tests today:** `FormTests.CenterToScreen_Invoke_SetsStartPosition` pins the divergent behaviour.

### FRM-21 — `Application.Exit()` raises no `FormClosing`/`FormClosed` — Cat A — P1 — High
- **Ours:** `Exit` sets `is_exiting`, raises the non-standard `OnExit`, cancels the main-loop token (`Application.cs:184-194`). Open forms are never closed; their windows are torn down by the loop ending.
- **Upstream:** `Exit` walks `OpenForms` backwards calling `RaiseFormClosingOnAppExit` (cancellable; a `Cancel` aborts the exit) then `RaiseFormClosedOnAppExit` with `CloseReason.ApplicationExitCall` (`Application.cs:1003-1080`; `Form.cs:4841-4890`).
- **Impact:** "Save window layout / settings / prompt for unsaved changes" written in `FormClosing` (the canonical place) never runs when the app exits via a File→Exit menu calling `Application.Exit()`; `e.Cancel` cannot veto exit.
- **Fix:** In `Exit(CancelEventArgs?)`, iterate `OpenForms.ToArray()` in reverse: `OnFormClosing(new FormClosingEventArgs(ApplicationExitCall, false))`; if any cancels, set `e.Cancel = true` and return; then `RaiseFormClosed` for each with `ApplicationExitCall`, then raise `ApplicationExit` (FRM-22) and cancel the loop.
- **Test:** Headless: show a form whose `FormClosing` sets `Cancel = true`; `Application.Exit(e)` → `e.Cancel == true`, loop token not cancelled.
- **Tests today:** `ApplicationTests.OpenForms` only.

### FRM-22 — `Application.ApplicationExit` discards handlers — Cat D — P1 — High
- **Ours:** `public static event EventHandler? ApplicationExit { add { } remove { } }` (`Application.cs:480`); the trigger point exists (`OnExit` at 191/375).
- **Upstream:** real `EventHandlerList`-backed event raised from `ThreadContext.Dispose` on exit (`Application.cs:772-776`).
- **Impact:** `Application.ApplicationExit += (s,e) => Settings.Save()` — the standard "persist on exit" hook — silently never runs.
- **Fix:** Back it with a field and raise it wherever `OnExit` is raised (`Exit` and the end of `RunCore`); keep `OnExit` as an alias.
- **Test:** Subscribe, call `Application.Exit()` (with no loop running), assert handler ran.
- **Tests today:** none.

### FRM-23 — `Application.Idle` is never raised by the message loop — Cat D — P1 — High
- **Ours:** `public static event EventHandler? Idle;` (`Application.cs:483`) is raised only by the public helper `RaiseIdle` (`AppMenuBindingParity.cs:103`), which nothing in the library calls; `RunMainLoop` is `Dispatcher.UIThread.MainLoop(token)` (`AvaloniaPlatformBackend.cs:34`).
- **Upstream:** `Idle` handlers registered on the `ThreadContext` are raised each time the message queue drains (`Application.cs:832-848`, `ThreadContext._idleHandler`).
- **Impact:** `Application.Idle += UpdateToolbarState` is the dominant WinForms pattern for enabling/disabling menu and toolbar items from document state (every MDI shell, every editor template). Those handlers never run: commands stay in their initial enabled state.
- **Fix:** Hook the backend's idle notification (Avalonia: `Dispatcher.UIThread` job-queue-empty / a `DispatcherPriority.ApplicationIdle` posted callback re-armed after each input batch; Headless: after each `DoEvents`) to call `RaiseIdle` once per drained queue, throttled so a handler that invalidates does not spin.
- **Test:** Headless: subscribe, run `Application.DoEvents()` after posting a job; assert `Idle` raised at least once.
- **Tests today:** none.

### FRM-24 — `Application.ThreadException` is never raised; exceptions in handlers crash the process — Cat D — P1 — High
- **Ours:** `ThreadException` (`Application.cs:477`) is raised only by the public `OnThreadException` helper (`AppMenuBindingParity.cs:99-100`); no `try/catch` around dispatch in `WindowBase.Handle*` or the backend loop; `SetUnhandledExceptionMode` is a no-op (486-489); no `UnhandledException` hookup in `src/Majorsilence.Forms.Avalonia`.
- **Upstream:** the message loop catches exceptions from `PreProcessControlMessage`/WndProc and routes them to `OnThreadException`, which invokes the handler or shows `ThreadExceptionDialog` (`Application.ThreadContext.cs:955-964, 582-620`).
- **Impact:** Apps that install a global handler (`Application.ThreadException += ShowErrorDialog`) — nearly all shipped LOB apps — get an unhandled-exception process crash for any exception in a Click/Tick/Paint handler instead of their error dialog.
- **Fix:** Wrap the dispatch in `WindowBase.HandlePointer*/HandleKey*/HandleTextInput`, `Timer.OnTick`, `BackgroundWorker` completion posts, and `RenderFrame` in a single `Application.DispatchGuard(Action)` that catches and calls `OnThreadException` when a handler is attached (or when mode == CatchException), rethrowing otherwise.
- **Test:** Headless: attach handler; button `Click` throws; call `HandlePointerPressed/Released` over it; assert handler received the exception and no exception escaped.
- **Tests today:** none.

### FRM-25 — `Application.Restart()` exits without relaunching — Cat A — P1 — High
- **Ours:** `public static void Restart() => Environment.Exit(0);` (`Application.cs:462`) — the doc comment even calls it a no-op.
- **Upstream:** starts a new process with `ExecutablePath` and the original arguments, then `Exit()` (`Application.cs:1294-1330`).
- **Impact:** "Settings changed — restart now?" → the app disappears and never comes back; no `FormClosing`, no `ApplicationExit`.
- **Fix:** `Process.Start(new ProcessStartInfo(ExecutablePath) { Arguments = <quoted args[1..]> }); Exit();` mirroring upstream (handle `dotnet app.dll` hosts by using `Environment.ProcessPath`).
- **Test:** Unit-test the start-info builder (file name + quoted args) rather than the process launch.
- **Tests today:** none.

### FRM-26 — `MessageBox` supports only OK / OKCancel / YesNo; icon and default button ignored — Cat A — P1 — High
- **Ours:** `MessageBoxForm.AddButtons` switches on `YesNo`, `OKCancel`, and `default` → a single OK (`MessageBoxForm.cs:57-84`). `YesNoCancel`, `RetryCancel`, `AbortRetryIgnore`, `CancelTryContinue` all render one OK button returning `DialogResult.OK`. `MessageBoxIcon` and `MessageBoxDefaultButton` are dropped (`WinFormsCompat.cs:1010-1020`); no `AcceptButton`/`CancelButton` is set, so Enter does nothing and Escape returns `Cancel` even for `YesNo` (see FRM-28).
- **Upstream:** all button sets map to `MB_*` styles with the requested icon and default button (`Dialogs/MessageBox.cs:40-70`).
- **Impact:** `MessageBox.Show("Save changes?", ..., YesNoCancel)` — the most common three-way prompt — shows only OK and returns OK, which matches neither `Yes` nor `No`; typical code falls into the cancel branch and the user can never proceed. Retry loops (`RetryCancel`) cannot retry.
- **Fix:** Add cases for all `MessageBoxButtons` (Yes/No/Cancel, Retry/Cancel, Abort/Retry/Ignore, Cancel/TryAgain/Continue), set `AcceptButton` to the default-button index and `CancelButton` only when the set has a Cancel/No, draw the icon via `SystemIcons`.
- **Test:** Headless: `new MessageBoxForm("t","m", YesNoCancel)` → three buttons with DialogResults Yes/No/Cancel; `AcceptButton` == first.
- **Tests today:** none for MessageBox.

### FRM-27 — `MessageBox.Show(IWin32Window owner, …, defaultButton, options[, help…])` NREs for a `Control` owner — Cat A — P2 — High
- **Ours:** the seven-plus-argument owner overloads do `Show((owner as Form)!, …)` (`WinFormsCompat.cs:950-976`) → `Show(Form owner, …)` → `form.ShowDialog(owner)` with `owner == null` → `ShowDialogAsync(null)` → `parent.PresentationWindow` throws `NullReferenceException` (`Form.cs:829`, `WindowBase.cs:1685`). The five-argument owner overload (999-1004) resolves correctly.
- **Upstream:** any `IWin32Window` owner works (`Dialogs/MessageBox.cs:160-170`).
- **Impact:** `MessageBox.Show(this, msg, title, OK, Error, Button1, RightAlign)` from inside a `UserControl` crashes.
- **Fix:** Route all owner overloads through the same `owner as Form ?? (owner as Control)?.FindForm() ?? first candidate` resolution used at 999-1004.
- **Test:** Call the 7-arg overload with a `Panel` owner headless (posting `DialogResult` to close); assert no exception.
- **Tests today:** none.

### FRM-28 — Escape closes a modal form with `Cancel` even without a `CancelButton` — Cat A — P2 — High
- **Ours:** `HandleKeyDown`: if `CancelButton == null && dialog_task != null` → `DialogResult = Cancel` (`WindowBase.cs:1364-1367`).
- **Upstream:** `ProcessDialogKey` clicks `CancelButton` only if set; otherwise Escape is ignored (`Form.cs:4756-4770`).
- **Impact:** Data-entry dialogs that deliberately have no cancel path (mandatory input) close on Esc; `MessageBox` `YesNo` returns `Cancel` on Esc (upstream YesNo has no Esc behaviour).
- **Fix:** Delete the fallback branch (or gate it behind `ControlBox && CancelButton == null` only for `MessageBoxForm` with an OK/Cancel set).
- **Test:** Headless modal form, no CancelButton, `HandleKeyDown(Escape)` → `DialogResult` still `None`.
- **Tests today:** none.

### FRM-29 — `Dispose()` raises `FormClosed` (and `Closed`) — Cat A — P2 — Medium
- **Ours:** `Dispose` calls `Backend.Close()` with `_closingHandled = true` (`WindowBase.cs:263-273`); both backends invoke `OnBackendClosed` from their closed callback (`MajorsilenceFormsWindowHost.cs:101`, `HeadlessWindowHost.cs:55-60`), which raises `Closed`, `FormClosed` (once) and `HandleDestroyed`. The matrix row says only that `FormClosing` is not raised.
- **Upstream:** `Dispose` without `Close` raises neither `FormClosing` nor `FormClosed` (`Form.cs:3512-3560`; the events live in `WmClose`).
- **Impact:** `FormClosed` handlers that persist state / detach shared services run on a plain `Dispose` of a never-closed form (e.g. a form constructed, populated, then discarded); `Closed` ends `Application.Run(WindowBase)` (`Application.cs:284`) when the main form is disposed — acceptable, but it happens via a "closed" event the form never had.
- **Fix:** In `Dispose`, set a `_disposingWithoutClose` flag that makes `OnBackendClosed` skip `OnClosed`/`RaiseFormClosed` (still remove from `OpenForms`, complete any dialog task with `Cancel`, and raise `HandleDestroyed`).
- **Test:** Headless: show, subscribe `FormClosed`, `Dispose()`; assert not raised.
- **Tests today:** `FormDisposeClosesWindowTests.Disposing_does_not_raise_FormClosing` (FormClosing only).

### FRM-30 — `CloseReason` is always `UserClosing` — Cat A — P2 — High
- **Ours:** `OnClosing` builds `new FormClosingEventArgs { Cancel = e.Cancel }` (`Form.cs:685`) and `RaiseFormClosed` uses `new FormClosedEventArgs()` (342); both types default `CloseReason` to `UserClosing` (`WinFormsCompat.cs:349, 362`). MDI parent close does not notify children with `MdiFormClosing`; owner close (FRM-15) and `Application.Exit` (FRM-21) do not run at all.
- **Upstream:** `WmClose` passes the tracked `CloseReason`, uses `MdiFormClosing` for children and `FormOwnerClosing` for owned forms (`Form.cs:6675-6822`); `Exit` uses `ApplicationExitCall`.
- **Impact:** Once FRM-21 is fixed, the "minimise to tray on UserClosing, really exit on ApplicationExitCall/WindowsShutDown" pattern needs the real reason or it will hide instead of exiting; MDI children cannot tell a parent close from their own.
- **Fix:** Add an internal `CloseReason` field set to `UserClosing` by `Close()`/title-bar close, `MdiFormClosing` when the container closes its children, `FormOwnerClosing`/`ApplicationExitCall` from those paths; pass it to both args.
- **Test:** Headless: MDI container `Close()` → child `FormClosing.CloseReason == MdiFormClosing`.
- **Tests today:** none.

### FRM-31 — `Form.Text` setter never raises `TextChanged` — Cat D — P2 — High
- **Ours:** setter updates `Backend.Title` and `TitleBar.Text` only (`Form.cs:1046-1055`); `WindowBase.TextChanged`/`OnTextChanged` exist (`WindowBase.cs:1137-1140`) but have no raiser.
- **Upstream:** `Control.Text` setter → `OnTextChanged` (`Form.cs:4666` overrides it).
- **Impact:** Code binding the caption ("Document* — App") via `TextChanged`, or an `OnTextChanged` override, never runs.
- **Fix:** Call `OnTextChanged(EventArgs.Empty)` at the end of the setter when the value changed.
- **Test:** Subscribe, set `Text`, assert raised once.
- **Tests today:** `FormTests.Text_Set_GetReturnsExpected` (storage).

### FRM-32 — `Form.DefaultSize` is 1080x720 — Cat E — P2 — High
- **Ours:** `protected override Size DefaultSize => new Size(1080, 720)` (`Form.cs:474`), applied to the backend in the ctor (62).
- **Upstream:** `new Size(300, 300)` (`Form.cs:882`).
- **Impact:** Any form created without a designer `ClientSize` (code-built dialogs, `new Form { Text = "..." }` hosts, quick property sheets) opens near full-screen on a laptop.
- **Fix:** Return `(300, 300)`; samples that want a large default can set `Size`.
- **Test:** `new Form().Size == (300,300)` headless.
- **Tests today:** none.

### FRM-33 — `Screen.FromControl(...)` always returns the primary screen — Cat A — P2 — High
- **Ours:** both overloads `=> PrimaryScreen` (`Screen.cs:54-59`).
- **Upstream:** `FromHandle(control.Handle)` → `MonitorFromWindow` nearest (`Screen.cs:265-277`).
- **Impact:** Dialogs positioned/sized against `Screen.FromControl(this).WorkingArea` on a secondary monitor open on the primary one; popups clamped to the wrong monitor.
- **Fix:** `FromControl(Control c) => FromRectangle(c.RectangleToScreen(c.ClientRectangle))`; `FromControl(WindowBase w) => FromRectangle(w.Bounds)`.
- **Test:** Headless with two fake screens via a settable `GetScreens`; window located on the second → `FromControl` returns it.
- **Tests today:** none.

### FRM-34 — `BackgroundWorker` semantics: `IsBusy` clears early, `Result` never throws, type shadows `System.ComponentModel` — Cat A/E — P2 — High
- **Ours:** `_is_busy = false` is set on the worker thread *before* the completion callback is posted (`BackgroundWorker.cs:56-58`); `RunWorkerCompletedEventArgs.Result` is a plain getter (111-112) even when `Error != null` or `Cancelled`; the class is `Majorsilence.Forms.BackgroundWorker`, so a file with both `using System.ComponentModel;` and `using Majorsilence.Forms;` gets CS0104 on `BackgroundWorker`/`DoWorkEventArgs`/`RunWorkerCompletedEventArgs`.
- **Upstream:** `System.ComponentModel.BackgroundWorker` (runtime, not in the winforms repo) clears `IsBusy` on the UI thread immediately before raising `RunWorkerCompleted`; `Result` throws `TargetInvocationException` when `Error` is set and `InvalidOperationException` when `Cancelled`; WinForms apps use the BCL type directly.
- **Impact:** `if (!worker.IsBusy) worker.RunWorkerAsync()` from a UI timer can start a second run while the first completion is still queued (the second `RunWorkerAsync` then throws "already running" upstream but not here — instead handlers interleave); `e.Result` after a failed DoWork silently yields null instead of surfacing the error.
- **Fix:** Move `_is_busy = false` into the posted continuation; make `Result` throw as the BCL does; consider deleting the duplicate type and letting apps use `System.ComponentModel.BackgroundWorker` (it only needs a `SynchronizationContext`, which `AvaloniaSynchronizationContext.InstallIfNeeded` already provides).
- **Test:** DoWork throws → `Assert.Throws<TargetInvocationException>(() => e.Result)` in `RunWorkerCompleted`.
- **Tests today:** none.

### FRM-35 — `Application.ProductName` / `CompanyName` / `ProductVersion` lack fallbacks; `UserAppDataPath` omits the version segment — Cat A — P2 — High
- **Ours:** `ProductName`/`CompanyName` return the assembly attribute or `null` (`Application.cs:408-411, 432-435`); `UserAppDataPath`/`LocalUserAppDataPath`/`CommonAppDataPath` are `Path.Combine(base, Company ?? "", Product ?? "")` (438-456).
- **Upstream:** `ProductName` falls back to the entry type's namespace then type name (`Application.cs:495-535`); `CompanyName`/`ProductVersion` have similar chains; `UserAppDataPath` = `GetDataPath(ApplicationData)` = `<base>\<Company>\<Product>\<Version>` (665-666).
- **Impact:** An app without `AssemblyCompany`/`AssemblyProduct` (SDK projects default `Product` to the assembly name but `Company` also to the assembly name — fine — but hand-edited csproj/AssemblyInfo often omit them) gets `UserAppDataPath == %APPDATA%` root and writes settings there; version-scoped settings folders differ from the Windows build.
- **Fix:** Port the upstream fallback chain (attribute → entry type namespace → type name; version → informational → assembly version → "1.0.0.0") and append `ProductVersion` in the three data paths.
- **Test:** With a fake entry assembly lacking attributes, assert `ProductName` is non-empty and `UserAppDataPath` ends with `/<Product>/<Version>`.
- **Tests today:** `ApplicationInfoTests.cs` (covers `ApplicationInfo` only).

### FRM-36 — `Form.RestoreBounds` returns current `Bounds` even when maximized/minimized — Cat A — P2 — High
- **Ours:** `public Rectangle RestoreBounds => Bounds;` (`Form.cs:1571`); `MaximizedBounds` is stored-only (1290).
- **Upstream:** tracks the last normal-state bounds and returns those while maximized (`Form.cs:1660-1678`).
- **Impact:** "Save window placement on close" code stores the maximized rectangle and restores a form that fills the screen in `Normal` state next launch.
- **Fix:** Record `Bounds` on every move/resize while `WindowState == Normal` and return that when not Normal.
- **Test:** Headless: set bounds, `WindowState = Maximized`, change backend size; `RestoreBounds` unchanged.
- **Tests today:** `WindowStateGeometryParityTests.cs` (does not assert RestoreBounds under Maximized).

### FRM-37 — `Form.Size`/`Width`/`Height` setter raises no synchronous `Resize`/`SizeChanged`/layout — Cat A — P2 — Medium
- **Ours:** the setter writes `Backend.Size` only (`Form.cs:858-879`); the layout pass and `OnResize` happen later in `SyncAdapterBounds` when the backend reports the new client size or the next frame paints (`WindowBase.cs:512-526`). `SetBoundsCore` likewise (324-339).
- **Upstream:** `SetBoundsCore` → `UpdateBounds` → `OnSizeChanged`/`OnResize` and layout synchronously (Control.cs), so `form.Width = 500; var w = panel.Width;` sees the docked panel already resized.
- **Impact:** Code that resizes a form and immediately reads a docked/anchored child's size, or relies on `Resize` firing before the next statement (e.g. to re-centre), reads stale geometry.
- **Fix:** After writing `Backend.Size`, call `SyncAdapterBounds(value.Width, value.Height)` immediately when not frame-hosted (the Avalonia host already tracks `_pendingClientSize`), so `OnResize`/layout run inline.
- **Test:** Headless: `form.Size = (400, 300)`; assert a `Dock=Fill` child's `Width == 400 - borders` without pumping.
- **Tests today:** `WindowGeometryWriteTests.cs` (asserts the write reaches the backend only).

### FRM-38 — `ShowIcon`, `TransparencyKey`, `SizeGripStyle`, `HelpButton`, `MaximizedBounds`, `AutoValidate`, `AutoSize`/`AutoSizeMode` stored-only — Cat C — P2 — High
- **Ours:** `ShowIcon` (`Form.cs:1580`), `HelpButton` (1577), `TransparencyKey` (1035), `SizeGripStyle` (1012-1019), `MaximizedBounds` (1290), `AutoValidate` (925), `WindowBase.AutoSize` (`WindowBase.cs:2115`) and `Form.AutoSizeMode` (`ControlAndFormParity.cs:462`) are auto-properties nothing reads; `TitleBar.ShowImage` is a separate knob.
- **Upstream:** `ShowIcon` toggles the caption icon (`Form.cs:1834-1846`); `TransparencyKey` makes the form layered/colour-keyed (2081-2100); `SizeGripStyle` draws the grip; `HelpButton` shows `?` and raises `HelpButtonClicked`; `AutoSize` sizes the form to its content; `AutoValidate` gates implicit validation.
- **Impact:** `ShowIcon = false` forms still show an icon; shaped/colour-keyed forms (`TransparencyKey = Magenta` splash screens) render opaque magenta; `AutoSize = true` forms keep their designer size; forms with `AutoValidate = Disable` still validate on focus change.
- **Fix:** `ShowIcon` → `TitleBar.ShowImage`; `TransparencyKey` → `Region` built from the rendered frame's key-colour mask (or backend `SetShaped` + clip in `RenderFrame`); `AutoSize` → set `Size = PreferredSize` on layout when true; `AutoValidate` → forward to `adapter.AutoValidate`. `SizeGripStyle`/`HelpButton`/`MaximizedBounds` may stay P3.
- **Test:** `ShowIcon = false` → `TitleBar.ShowImage == false`; `AutoSize = true` with a 200x100 child → `ClientSize` grows to fit after `PerformLayout()`.
- **Tests today:** round-trip tests only (`FormTests`).

### FRM-39 — `SystemInformation.CaptionHeight` / `MenuHeight` do not describe the chrome this library draws — Cat A — P2 — Medium
- **Ours:** `CaptionHeight => 30` (`SystemInformation.cs:39`) while `FormTitleBar.DefaultSize.Height` is 34 (`FormTitleBar.cs:165`); `MenuHeight => 24` is a constant unrelated to `MenuStrip`'s actual height; `VerticalScrollBarWidth => 17` (matches the drawn scrollbars only if theirs is 17 — not verified here).
- **Upstream:** all are live `GetSystemMetrics` values that match the drawn chrome.
- **Impact:** Layout code that reserves `SystemInformation.CaptionHeight` for a custom caption or positions a popup under a menu using `MenuHeight` is off by a few pixels; ports that draw their own title bar over the library's get a 4px double band.
- **Fix:** Return `FormTitleBar`'s preferred height and `MenuStrip`'s default height; derive `VerticalScrollBarWidth` from the `ScrollBar` default size.
- **Test:** `SystemInformation.CaptionHeight == new Form().TitleBar.PreferredHeight`.
- **Tests today:** `SystemInformationTests.cs` (pins the constants).

## Low-priority / Win32-only (P3) — one line each
- `WindowBase.WndProc` / `DefWndProc` / `OnNotifyMessage` — never called; there is no Win32 message pump (`WindowBase.Compat.cs:12-17`, `WindowBase.cs:1586`).
- `WindowBase.Handle => GetHashCode() | 1` vs `IWin32Window.Handle => IntPtr.Zero` on the same Form (`WindowBase.cs:669`, `Form.cs:750`) — two different fake handles for one window; nothing portable consumes an HWND.
- `Form.CreateParams` — returned and ignored; the "remove close button via CreateParams" pattern needs `ControlBox` (FRM-19) instead.
- `Application.SetUnhandledExceptionMode`, `EnableVisualStyles`, `SetCompatibleTextRenderingDefault`, `SetHighDpiMode`, `VisualStyleState`, `RenderWithVisualStyles`, `SafeTopLevelCaptionFormat`, `OleRequired`, `SetSuspendState` — Win32/COM/theme-engine switches with no portable effect.
- `Application.MessageLoop => true` always (`Application.cs:537`); `HasMessageLoop` (internal) has the real answer — expose it.
- `Application.ExitThread` == `Exit` — single-UI-thread model, acceptable.
- `Application.MainForm => OpenForms[0]` — the first *shown* form (a splash) rather than the form passed to `Run`; minor.
- `Form.DesktopLocation` / `DesktopBounds` / `SetDesktopLocation` — screen coordinates instead of working-area-relative (`Form.cs:1503-1512`); only differs when the taskbar is on the top/left.
- `Form.SendToBack()` — no-op for top-level windows (`Form.cs:1564`); no portable "lower window" API.
- `Form.InputLanguageChanged` / `InputLanguageChanging` — `add {} remove {}` (`Form.cs:405-408`); IME/keyboard-layout notifications are OS-specific.
- `Form.Focus()` returns `true` even when it did nothing (`Form.cs:152-156`).
- `WindowBase.Font = null` is ignored instead of resetting to the ambient font (`WindowBase.cs:352-361`).
- `Form.ShowInTaskbar` / `TopMost` / `Opacity` — forwarded to the backend; fidelity is the backend's (not stored-only).
- `SystemInformation.PowerStatus` / `HighContrast` / `MouseButtonsSwapped` / `Network` / `VirtualScreen` — constants; OS signals not surfaced.
- `Screen.BitsPerPixel => 32`, `Screen.DeviceName` — cosmetic.
- `Timer` — behaviour matches upstream (interval validation, UI-thread tick via `DispatcherTimer`); no finding.
- MDI: `ActiveMdiChild`, `ActivateMdiChild`, `LayoutMdi`, `MdiChildActivate`, `MdiChildren` — real and tested (`MdiTests.cs`); `Menu`/`MergedMenu` merge is absent (documented), `MdiChildrenMinimizedAnchorBottom` stored-only.

## Systemic patterns
- **One-shot lifecycle flags never reset.** `_loadFired`, `_formClosedFired`, `shown`, `visible`, `dialog_result` model a form as shown-once; WinForms models the *handle* as the unit of lifetime and recreates it on every `ShowDialog`. Fix once in `OnBackendClosed`: reset all of them (FRM-02/03/04/16) and set `Modal=false` (FRM-12).
- **Declared hooks with no dispatcher.** The whole `Process*Key`/`ProcessMnemonic` family (Form *and* Control) is `=> false` with zero call sites, while the real routing lives in `WindowBase.HandleKeyDown` with its own rules (FRM-05/08/09/28). A single `PreProcessKey` step in `HandleKeyDown` that calls the chain would fix all four.
- **Chrome inside the client area.** `FormTitleBar` being a child of `Controls` makes `ClientSize`, `ClientRectangle`, `DisplayRectangle`, designer `Location`s, `SystemInformation.CaptionHeight` and `PointToClient` all disagree with WinForms by the caption height (FRM-06/39). Moving the title bar out of `adapter` (or offsetting the adapter) is one change.
- **Chrome flags with two spellings.** `MaximizeBox`/`MinimizeBox`/`ControlBox`/`ShowIcon` (WinForms) and `AllowMaximize`/`AllowMinimize`/`TitleBar.ShowImage` (this library) are independent stores; the WinForms ones must forward to the library ones (FRM-18/19/38).
- **`Application` static events that are inert.** `ApplicationExit` discards, `Idle`/`ThreadException` have public raise helpers nothing calls, `Exit` does not walk `OpenForms`, `Restart` exits (FRM-21..25). All five need the same thing: the backend loop calling back into `Application` at loop-idle, on exception, and on exit.
- **Owner relationship is a bare property.** `Owner`, `Show(owner)`, `ShowDialog(owner)`, owner-close cascade and multi-window modal disabling are all missing the same graph (FRM-14/15/30).
- **Stored-only auto-properties on Form** flagged by the cheap `{ get; set; }` scan: `AutoScaleMode`, `AutoScaleDimensions`, `MinimizeBox`, `ControlBox`, `ShowIcon`, `HelpButton`, `TransparencyKey`, `MaximizedBounds`, `AutoValidate`, `AutoSize`, `AutoSizeMode`, `Owner`, `MainMenuStrip`, `Modal` (private set, never set), `KeyPreview` (this one *is* consumed), `Name` (fine).
- **Tests pin current behaviour, not the WinForms contract** in several places (`CenterToScreen_Invoke_SetsStartPosition`, `Load_fires_exactly_once`, `GestureTests` title-bar offsets, `MaximizeBox_Set_GetReturnsExpected`); fixing the findings above will require updating those.
