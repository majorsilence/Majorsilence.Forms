# Services (dialogs, clipboard/drag-drop, keyboard/mouse/cursor, printing, misc) — findings

## Summary
The area is a thin veneer over three real seams (Avalonia file/folder pickers, a text-only clipboard, a
Skia PDF pipeline) plus a large body of stored-only compat properties. The dominant failure patterns are:
(1) the WinForms keyboard *processing chain* (`ProcessCmdKey`/`ProcessDialogKey`/`IsInputKey`/
`ProcessMnemonic`/`PreProcessMessage`) exists as virtuals on both `Control` and `WindowBase` but nothing in
the input pipeline ever calls it, so the form-level shortcut/mnemonic/arrow-traversal/AcceptsReturn/AcceptsTab
behaviours that hang off it are all silently absent while `AcceptButton`/`Tab` are hard-wired ahead of the
focused control; (2) statics that WinForms reads live from the OS (`Cursor.Current`, `Control.MouseButtons`,
`UseWaitCursor`) are auto-properties that nothing consumes or nothing writes; (3) dialogs whose result path
only covers the happy subset (`MessageBox` implements 3 of 7 button sets and returns `OK` for the rest;
`PrintDialog.ShowDialog(owner)` shows a blank form); (4) the clipboard and `DataObject` are text-only with no
format auto-conversion, so every non-string copy/paste and every `GetData(typeof(string))` misses; (5) the
print pipeline hands handlers pixel-unit bounds where WinForms hands hundredths of an inch and never consults
`PrintController`, so `PreviewPrintController` collects nothing and `Print()` produces a temp PDF nobody sees.
Counts: P0 = 2, P1 = 25, P2 = 12, plus a P3 list. Most P0/P1 items have no test covering the divergent
behaviour (existing tests are set/get round-trips).

## Findings

### SVC-01 — `MessageBox.Show(..., MessageBoxButtons.YesNoCancel | AbortRetryIgnore | RetryCancel | CancelTryContinue)` — Cat A — P0 — High
- **Ours:** `MessageBoxForm.AddButtons` has cases only for `YesNo` and `OKCancel`; every other value falls to `default:` which adds a single OK button whose click sets `DialogResult.OK` (`src/Majorsilence.Forms/MessageBoxForm.cs:60-84`). `VbInteraction.MsgBox` routes through the same path (`src/Majorsilence.Forms/Interaction.cs:32-40`), so `MsgBox(..., MsgBoxStyle.YesNoCancel)` also returns `Ok`.
- **Upstream:** `MessageBox.ShowCore` maps all seven `MessageBoxButtons` to `MB_*` styles and returns the Win32 result as `DialogResult` (`src/System.Windows.Forms/System/Windows/Forms/Dialogs/MessageBox.cs:439-490`), so `YesNoCancel` yields Yes/No/Cancel, `AbortRetryIgnore` yields Abort/Retry/Ignore, etc.
- **Impact:** The single most common close-prompt in LOB apps (`"Save changes?"` with `YesNoCancel`) shows only OK and returns `DialogResult.OK`, which matches neither `Yes`, `No` nor `Cancel`; typical `if (r == Yes) Save(); else if (r == Cancel) e.Cancel = true;` silently discards the user's work. `RetryCancel` loops can never retry.
- **Fix:** Extend `AddButtons` with `YesNoCancel` (Yes/No/Cancel), `AbortRetryIgnore`, `RetryCancel`, `CancelTryContinue` (TryAgain=10, Continue=11 in `DialogResult`), each button setting the matching `DialogResult`; wire `MessageBoxDefaultButton` to `AcceptButton`/initial focus and `CancelButton` to the Cancel/No/Ignore button per Win32 rules.
- **Test:** Headless: construct `new MessageBoxForm("t", "m", MessageBoxButtons.YesNoCancel)` and assert three `Button`s with texts Yes/No/Cancel exist and clicking each yields the corresponding `DialogResult`.
- **Tests today:** none (InteractionTests only checks the numeric casts).

### SVC-02 — `Control.ProcessCmdKey / ProcessDialogKey / ProcessDialogChar / IsInputKey / IsInputChar / ProcessKeyPreview / PreProcessMessage` and the `WindowBase` mirrors — Cat B — P0 — High
- **Ours:** All declared as `=> false` stubs (`src/Majorsilence.Forms/Control.Compat.cs:568-606`, `src/Majorsilence.Forms/WindowBase.Compat.cs:23-53`, `src/Majorsilence.Forms/ControlAndFormParity.cs:344`). No caller exists anywhere in `src/` (grep for the names finds only the declarations). Key routing is `WindowBase.HandleKeyDown` → `OnKeyDown` (if `KeyPreview`) → `ControlAdapter.RaiseKeyDown` → focused control `OnPreviewKeyDown`/`OnKeyDown` (`src/Majorsilence.Forms/WindowBase.cs:1341-1382`, `src/Majorsilence.Forms/Control.cs:1808-1839`).
- **Upstream:** `Control.PreProcessMessage` runs `ProcessCmdKey` (which walks up the parent chain to the Form), then `IsInputKey`, then `ProcessDialogKey`, and for WM_CHAR `IsInputChar` then `ProcessDialogChar` (→ `ProcessMnemonic`) *before* `KeyDown`/`KeyPress` are raised (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:8635-8672`).
- **Impact:** The canonical WinForms idiom for form-wide shortcuts — `protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == (Keys.Control | Keys.S)) {...} }` — compiles and never runs. Same for `ProcessDialogKey` overrides that swallow Enter/Escape in custom controls, `IsInputKey` overrides that claim arrow keys in grids, and `ProcessMnemonic` overrides.
- **Fix:** In `WindowBase.HandleKeyDown`, before the AcceptButton/Tab logic, call `form.ProcessCmdKey(ref msg, keys)` and then walk the focused control's parent chain calling `ProcessCmdKey`; then `if (!target.IsInputKey(keys)) { if (target.ProcessDialogKey(keys)) return true; }` walking parents (Form last, where AcceptButton/CancelButton/Tab/arrows belong); do the analogous `IsInputChar`/`ProcessDialogChar` in `HandleTextInput`. Build a `Message` with `WM_KEYDOWN`/wParam=keycode for the `ref Message` overloads.
- **Test:** Headless form with a subclass overriding `ProcessCmdKey` to record `keyData`; call `form.HandleKeyDown(Keys.Control | Keys.S)` (internal) and assert the override ran and returned-true suppressed the focused TextBox's `KeyDown`.
- **Tests today:** none (FormTests covers KeyPreview routing only).

### SVC-03 — `Form` Enter/Escape handling (`AcceptButton`, `CancelButton`, modal Escape) — Cat A — P1 — High
- **Ours:** `WindowBase.HandleKeyDown` fires `AcceptButton.PerformClick()` on any Return (including Ctrl/Alt+Return) *before* the focused control sees the key, and on Escape with no `CancelButton` sets `DialogResult.Cancel` whenever the form is modal (`src/Majorsilence.Forms/WindowBase.cs:1349-1368`). `TextBox.AcceptsReturn` is a bare auto-property nothing reads (`src/Majorsilence.Forms/TextBox.cs:595`).
- **Upstream:** `Form.ProcessDialogKey` only acts when `(keyData & (Alt|Control)) == None`, and it is reached only after the focused control's `IsInputKey` declined the key (`src/System.Windows.Forms/System/Windows/Forms/Form.cs:4732-4772`); `TextBox.IsInputKey` returns `_acceptsReturn` for Return in multiline boxes (`Controls/TextBox/TextBox.cs:517-524`). Escape with no `CancelButton` does nothing; a modal form without a CancelButton does not close on Escape.
- **Impact:** A multiline `TextBox` with `AcceptsReturn = true` on a form with an OK `AcceptButton` submits the form instead of inserting a line; a ComboBox whose list is open commits the form on Enter; Ctrl+Enter shortcuts fire the default button; modal dialogs designed to force a choice (no Cancel button) can be dismissed with Escape returning `Cancel`.
- **Fix:** Move the AcceptButton/CancelButton logic into `Form.ProcessDialogKey` (mirroring upstream, with the modifier guard), call it from the chain in SVC-02 after `IsInputKey`, and delete the `dialog_task is not null → Cancel` branch. Make `TextBox.IsInputKey` return `Multiline && AcceptsReturn` for Return.
- **Test:** Headless form with `AcceptButton` and a focused multiline `TextBox { AcceptsReturn = true }`; `HandleKeyDown(Keys.Return)` must insert "\n" and not click the button; `HandleKeyDown(Keys.Escape)` on a modal form with no CancelButton must leave `DialogResult == None`.
- **Tests today:** FormTests (property round-trips only).

### SVC-04 — `Tab` handled by `ControlAdapter.RaiseKeyDown/RaiseKeyPress` ahead of the focused control — Cat A — P1 — High
- **Ours:** The root adapter intercepts `Keys.Tab` and calls `SelectNextControl` before dispatching to `SelectedControl`, then returns (`src/Majorsilence.Forms/Control.cs:1821-1828`, `:1853-1860`). `OnPreviewKeyDown` is only raised in the leaf after this, so `PreviewKeyDownEventArgs.IsInputKey` cannot claim Tab. `TextBoxBase.AcceptsTab` (`src/Majorsilence.Forms/TextBoxBase.cs:45`) and `DataGridView.StandardTab` (`src/Majorsilence.Forms/DataGridView.cs:810`) are never consulted; `DataGridView`'s Tab cell-navigation lives in `OnKeyUp` and the edit box's `KeyDown` (`DataGridView.cs:1063`, `:2482`) which never see the key because focus has already moved.
- **Upstream:** Tab is a dialog key processed via `IsInputKey` → `ProcessDialogKey` → `ContainerControl.ProcessTabKey` (`Layout/Containers/ContainerControl.cs:1316`); `TextBoxBase.IsInputKey` returns true for Tab when `AcceptsTab` (`Controls/TextBox/TextBoxBase.cs:1331`); `DataGridView` handles Tab in `ProcessDialogKey` unless `StandardTab`.
- **Impact:** Tab in a `DataGridView` leaves the grid instead of moving to the next cell (core grid editing behaviour); multiline text boxes with `AcceptsTab` cannot insert tabs; `PreviewKeyDown` `IsInputKey = true` for Tab has no effect.
- **Fix:** Remove the Tab short-circuit from `ControlAdapter.RaiseKeyDown/RaiseKeyPress`; route Tab through the SVC-02 chain so `IsInputKey`/`PreviewKeyDown.IsInputKey` can claim it and `Form.ProcessDialogKey` → `ProcessTabKey` handles it otherwise.
- **Test:** Headless form with a `DataGridView` focused on cell (0,0); `HandleKeyDown(Keys.Tab)` should leave focus on the grid with `CurrentCell` at (0,1).
- **Tests today:** none for Tab inside a control.

### SVC-05 — Arrow-key focus traversal between non-input controls — Cat B — P1 — High
- **Ours:** No handling of `Keys.Left/Right/Up/Down` for focus movement in `Control.cs`, `Form.cs`, `WindowBase.cs`, `RadioButton.cs` or `GroupBox.cs` (grep empty). Arrow keys reach the focused control's `OnKeyDown` and stop.
- **Upstream:** `ContainerControl.ProcessDialogKey` handles arrows via `ProcessArrowKey`, which calls `SelectNextControl(_activeControl, forward, tabStopOnly:false, nested:false, wrap:true)` within the parent group (`Layout/Containers/ContainerControl.cs:1156-1165`, `:1185-1210`). This is what makes arrow keys move between `RadioButton`s in a `GroupBox` (and check the newly focused one) and between `Button`s.
- **Impact:** Keyboard users cannot change a radio-button selection with arrows — the standard, accessibility-expected behaviour of every WinForms option group.
- **Fix:** Add the upstream `ProcessArrowKey` to `Form.ProcessDialogKey` (called via SVC-02 chain, after the focused control's `IsInputKey` — TextBox/ListBox/ComboBox/DGV must return true for arrows so they keep them).
- **Test:** Headless `GroupBox` with two `RadioButton`s, first focused; `HandleKeyDown(Keys.Down)` → second radio focused and `Checked`.
- **Tests today:** none.

### SVC-06 — Alt+mnemonic activation (`Control.ProcessMnemonic`, `IsMnemonic`) — Cat B — P1 — High
- **Ours:** Mnemonic underlines are drawn (`src/Majorsilence.Forms/Mnemonics.cs`, `Graphics.cs:1776-1795`) and `Control.IsMnemonic` exists (`Control.cs:987`), but `ProcessMnemonic` is a `=> false` stub never called (`Control.Compat.cs:606`, `WindowBase.Compat.cs:53`); nothing in `HandleKeyDown`/`HandleTextInput` looks at `Keys.Alt` + character.
- **Upstream:** WM_SYSCHAR → `ProcessDialogChar` → `ContainerControl.ProcessMnemonic` walks children calling each `ProcessMnemonic` (`Layout/Containers/ContainerControl.cs:1237`); `Button`/`CheckBox`/`RadioButton`/`Label` (focus next) implement it.
- **Impact:** `&Save` draws an underlined S but Alt+S does nothing; every designer-era form relies on this for keyboard access.
- **Fix:** In `WindowBase.HandleKeyDown`, when `(keys & Keys.Alt) != 0` and the key is a letter/digit, call `form.ProcessDialogChar(char)` → `ProcessMnemonic` walk over `Controls` (depth-first, visible+enabled) using `Control.IsMnemonic(c, Text)`; implement `ProcessMnemonic` on ButtonBase (PerformClick), Label (select next), GroupBox/TabPage (select first child).
- **Test:** Headless form with `Button { Text = "&Save" }` and a Click counter; `HandleKeyDown(Keys.Alt | Keys.S)` → counter == 1.
- **Tests today:** none.

### SVC-07 — `KeyPress` never raised for Enter, Tab, Backspace, Escape — Cat A — P1 — High
- **Ours:** `KeyPress` is only raised from `WindowBase.HandleTextInput`, fed by Avalonia's `TextInput` (`src/Majorsilence.Forms/WindowBase.cs:1432-1456`, `src/Majorsilence.Forms.Avalonia/MajorsilenceFormsWindowHost.cs:314-318`). Avalonia does not deliver Enter/Tab/Backspace/Escape as text (the layer's own comment in `src/Majorsilence.Forms/TextBox.cs:202-211` says the `KeyChar == 13` branch is dead on a real window). `HandleKeyDown` does not synthesise a `KeyPressEventArgs` for them.
- **Upstream:** `TranslateMessage` produces WM_CHAR for these keys ('\r', '\t', '\b', '\x1b') and `Control.ProcessKeyEventArgs` turns every WM_CHAR into `OnKeyPress` (`src/System.Windows.Forms/System/Windows/Forms/Control.cs:8919-8935`).
- **Impact:** `textBox_KeyPress: if (e.KeyChar == (char)Keys.Enter) { SelectNextControl(...) }` (the VB-heritage "Enter moves to next field" idiom) and `if (e.KeyChar == (char)13) Search();` never fire; numeric filters that allow `(char)8` are harmless but Enter-driven flows are dead.
- **Fix:** In `WindowBase.HandleKeyDown`, after KeyDown is not handled (and `!SuppressKeyPress`), for `Return`→'\r', `Tab`→'\t', `Back`→'\b', `Escape`→'\x1b' call `HandleTextInput` with that char (mark as synthesised so the TextBox does not double-insert; move the `document.InsertText("\n")` from KeyDown into the KeyPress branch as upstream).
- **Test:** Headless form, focused `TextBox`, subscribe `KeyPress`; `HandleKeyDown(Keys.Return)` → handler saw `KeyChar == '\r'`.
- **Tests today:** none.

### SVC-08 — `KeyEventArgs.SuppressKeyPress` does not suppress the KeyPress/text — Cat A — P1 — Medium
- **Ours:** The flag only sets `Handled` (`src/Majorsilence.Forms/KeyEventArgs.cs:55-61`). `HandleKeyDown` returning true marks the Avalonia `KeyDown` handled (`MajorsilenceFormsWindowHost.cs:300-304`), but Avalonia raises `TextInput` independently of `KeyDown.Handled`, and `HandleTextInput` does not check any suppress state.
- **Upstream:** `ProcessKeyEventArgs` removes the pending WM_CHAR when `SuppressKeyPress` is set (`Control.cs:8977-8981`), so no `KeyPress` and no character insertion follow.
- **Impact:** The standard "numeric only" pattern — `KeyDown: if (!digit) { e.SuppressKeyPress = true; }` — still lets the character into the TextBox.
- **Fix:** Record `suppress_next_char` on the window in `HandleKeyDown` when `kd_e.SuppressKeyPress`; in `HandleTextInput` consume-and-skip when set (clear on next KeyDown). Needs a runtime check on each Avalonia platform of whether TextInput follows a handled KeyDown.
- **Test:** Headless: KeyDown handler sets `SuppressKeyPress`, then `HandleTextInput("a")` on the same window → TextBox text unchanged and no `KeyPress`.
- **Tests today:** EventArgsTests `SuppressKeyPress_AlsoSetsHandled` (flag only).

### SVC-09 — `Control.MouseButtons` (static) — Cat A — P1 — High
- **Ours:** `public static MouseButtons MouseButtons { get; internal set; }` (`src/Majorsilence.Forms/ControlAndFormParity.cs:89`); no assignment anywhere in `src/` (grep for `MouseButtons = ` finds none), so it is always `None`.
- **Upstream:** Reads `GetKeyState` for L/R/M/X1/X2 live (`Control.cs:2625-2660`).
- **Impact:** `MouseMove: if (Control.MouseButtons == MouseButtons.Left) { drag/paint }` — a very common custom-drag idiom — never enters the branch. Ported code that checks `Control.MouseButtons` in a timer or in `Leave` sees no button.
- **Fix:** Set it in `WindowBase.HandlePointerPressed/Released/Moved` from the backend's button state (the `buttons` argument already carries it), analogous to `Cursor.TrackPosition`.
- **Test:** Headless window: simulate pointer pressed with Left → `Control.MouseButtons == Left`; released → `None`.
- **Tests today:** none.

### SVC-10 — `Control.ModifierKeys` updated only as a side effect of `KeyEventArgs`/`MouseEventArgs` construction — Cat A — P2 — Medium
- **Ours:** The constructors write the static (`src/Majorsilence.Forms/KeyEventArgs.cs:18`, `MouseEventArgs.cs:37`). User or test code constructing `new KeyEventArgs(Keys.Control | Keys.C)` (to call `OnKeyDown` manually, a common pattern) overwrites the global; after the last event the value is stale until the next event.
- **Upstream:** `GetKeyState` live (`Control.cs:2596`).
- **Impact:** Stale modifiers in timers/Idle handlers; simulated events corrupt real state. Cosmetic in most apps.
- **Fix:** Move the assignment into `WindowBase.HandleKeyDown/Up` and the pointer handlers (from the `keys` argument) and delete it from the EventArgs constructors.
- **Test:** `new KeyEventArgs(Keys.Shift | Keys.A)` must not change `Control.ModifierKeys`.
- **Tests today:** none.

### SVC-11 — Double-click raises `Click` on the second press and before `DoubleClick` — Cat A — P2 — High
- **Ours:** `HandlePointerReleased`: `if (ev.Clicks > 1) adapter.RaiseDoubleClick(ev); adapter.RaiseClick(ev); adapter.RaiseMouseUp(ev);` (`src/Majorsilence.Forms/WindowBase.cs:1093-1097`).
- **Upstream:** `WmMouseUp` raises either `Click`+`MouseClick` or `DoubleClick`+`MouseDoubleClick`, never both, for one release (`Control.cs:11664-11705`).
- **Impact:** Handlers with both `Click` (toggle/select) and `DoubleClick` (open) see an extra Click on every double-click; a Click-toggled state flips twice.
- **Fix:** `if (ev.Clicks > 1) RaiseDoubleClick(ev); else RaiseClick(ev); RaiseMouseUp(ev);`.
- **Test:** Headless: two rapid presses/releases at the same point → Click count 1, DoubleClick count 1.
- **Tests today:** none.

### SVC-12 — `Cursor.Current` — Cat C — P1 — High
- **Ours:** `public static Cursor? Current { get; set; }` auto-property (`src/Majorsilence.Forms/Cursor.cs:78`); nothing reads it (`WindowBase.Cursor`/`OverrideCursor`/`HandleMouseMove` only consult `current_cursor`/`override_cursor`, `src/Majorsilence.Forms/WindowBase.cs:364-388`, `:563-567`).
- **Upstream:** Setter calls `SetCursor(handle)` immediately (`Input/Cursor.cs:143-150`); this is *the* WinForms busy-cursor idiom (`Cursor.Current = Cursors.WaitCursor; ...; Cursor.Current = Cursors.Default;`).
- **Impact:** Long operations show no wait cursor anywhere in a migrated app.
- **Fix:** In the setter, push `value?.CursorType ?? Arrow` to every open window's `Backend.SetCursor` (via `Application.OpenForms`), and have `WindowBase.HandleMouseMove` prefer `Cursor.Current` when non-null (WinForms resets `Current` on the next mouse move; mirror by clearing it in `HandlePointerMoved` unless `UseWaitCursor`).
- **Test:** Headless window whose fake backend records `SetCursor`; set `Cursor.Current = Cursors.Wait` → backend saw `Wait`.
- **Tests today:** CursorTests `Cursor_Current_Set_GetReturnsExpected` (round-trip only).

### SVC-13 — `Control.UseWaitCursor` / `Application.UseWaitCursor` — Cat A — P1 — High
- **Ours:** `Control.UseWaitCursor` is an auto-property (`src/Majorsilence.Forms/Control.Compat.cs:304`) while `Control.Cursor`'s getter tests `GetState(States.UseWaitCursor)` (`src/Majorsilence.Forms/Control.cs:421`) — a flag no setter ever sets. `Application.UseWaitCursor` stores a bool and does nothing (`src/Majorsilence.Forms/AppMenuBindingParity.cs:57-68`). `WindowBase.UseWaitCursor` forwards to the adapter's dead auto-property.
- **Upstream:** Setter sets `States.UseWaitCursor` and propagates to children; `Cursor` getter returns `Cursors.WaitCursor` when set (`Control.cs:3438-3455`).
- **Impact:** The "recommended" busy indicator does nothing; combined with SVC-12 there is no way to show a wait cursor.
- **Fix:** Replace the auto-property with `get => GetState(States.UseWaitCursor); set { SetState(...); foreach child ...; FindForm()?.SetCursor(Cursor); }`; make `Application.UseWaitCursor` iterate `OpenForms` setting `UseWaitCursor`.
- **Test:** `control.UseWaitCursor = true; Assert.Same(Cursors.Wait, control.Cursor)`.
- **Tests today:** none.

### SVC-14 — `Cursors.HSplit` / `Cursors.VSplit` swapped — Cat A — P2 — High
- **Ours:** `HSplit => SizeWestEast`, `VSplit => SizeNorthSouth` (`src/Majorsilence.Forms/Cursors.cs:117-120`).
- **Upstream:** `HSplit` is `hsplit.cur` — the cursor for a *horizontal* splitter bar, which moves vertically (up/down arrows); `Splitter` uses `HSplit` for `Dock Top/Bottom` and `VSplit` for `Left/Right` (`Input/Cursors.cs:55-56`, `Controls/Splitter/Splitter.cs:100-101`).
- **Impact:** Ported splitter/resizer code that sets `Cursor = Cursors.HSplit` shows left-right arrows over a bar that drags up-down (and vice versa).
- **Fix:** `HSplit => SizeNorthSouth; VSplit => SizeWestEast;` and fix `CursorTests.cs:79` which pins the wrong alias.
- **Test:** `Assert.Same(Cursors.SizeNorthSouth, Cursors.HSplit)`.
- **Tests today:** CursorTests `Cursors_Alias_ReturnsSameUnderlying` asserts the *wrong* mapping.

### SVC-15 — `Control.DoDragDrop` / `AllowDrop` / `DragEnter|DragOver|DragDrop|DragLeave|GiveFeedback|QueryContinueDrag` — Cat B — P1 — High
- **Ours:** `DoDragDrop` returns `DragDropEffects.None` immediately (`src/Majorsilence.Forms/Control.Compat.cs:447`); `AllowDrop` is an auto-property nothing reads (`Control.Compat.cs:390`); the `On*` hooks exist but no framework code raises them (`Control.Hooks.cs:200-220`); no Avalonia `DragDrop`/`DropEvent` wiring exists (grep of `src/Majorsilence.Forms.Avalonia` empty). Documented in COMPATIBILITY_MATRIX.md:146.
- **Upstream:** `DoDragDrop` runs a modal OLE drag loop, raising `QueryContinueDrag`/`GiveFeedback` on the source and `DragEnter/Over/Drop/Leave` on any `AllowDrop` target, returning the final effect (`Control.cs:4920`, `SetAcceptDrops` `:9556`).
- **Impact:** Every drag interaction is dead: TreeView node reorder, ListView reorder, drag-to-reparent in designers, dropping files from the OS onto a form. Since the return is `None`, source code that does `if (DoDragDrop(...) == Move) RemoveItem()` at least does not corrupt data — but nothing ever moves.
- **Fix:** Implement an intra-app drag loop in `DoDragDrop`: capture the pointer on the source window, on each pointer move hit-test `Application.OpenForms` for the deepest `AllowDrop` control, raise `DragEnter/Over/Leave` with screen coords and `KeyState`, on release raise `DragDrop` and return `e.Effect`; raise `QueryContinueDrag` (Escape cancels) and `GiveFeedback` (apply `Cursors.DragCopy/Move/Link` via `OverrideCursor`). Optionally bridge Avalonia `DragDrop.DoDragDrop` for cross-app/OS file drops.
- **Test:** Headless: source control calls `DoDragDrop("x", Move)` while a scripted pointer moves over an `AllowDrop` panel and releases → panel's `DragDrop` fired with `Data.GetData(typeof(string)) == "x"`, return == `Move`.
- **Tests today:** ControlExtensibilityHookTests (verifies the `On*` hooks invoke handlers when called manually).

### SVC-16 — `Clipboard.SetData/GetData/ContainsData/SetDataObject/GetDataObject` for non-text formats — Cat B — P1 — High
- **Ours:** `SetData(format, data)` only acts when `data is string` and then stores it as plain text regardless of `format` (`src/Majorsilence.Forms/Clipboard.cs:88-92`); `GetData(format)` returns text only for `"Text"` (`:95-96`); `SetDataObject` extracts only `DataFormats.Text` from an `IDataObject` (`:69-78`) — a `DataObject` built with `DataFormats.UnicodeText`, `Rtf`, `Html`, `Csv`, `FileDrop`, `Bitmap` or a custom format puts nothing on the clipboard; `GetDataObject()` returns a text-only stub (`:81`). The typed helpers (`SetFileDropList`, `SetAudio`, `ContainsFileDropList`, `TryGetData`) write a *separate* in-process `DataObject Current` (`src/Majorsilence.Forms/TailParity.cs:558-590`) that `Clear()`, `GetDataObject()` and `ContainsData()` never see. The backend seam is text-only (`Backends/IPlatformBackend.cs:56-61`); Headless stores one string, Avalonia calls `IClipboard.SetTextAsync`.
- **Upstream:** `SetData` wraps in `new DataObject(format, data)` and places the whole object (`OLE/Clipboard.cs:433-441`); `SetDataObject` places any `DataObject`/wrapped object with all its formats (`:42-52`); `GetDataObject` returns the live object.
- **Impact:** Intra-app copy/paste of anything but a string (grid rows as a custom-format object, `Clipboard.SetData("MyApp.Rows", list)`, `Clipboard.SetDataObject(new DataObject(DataFormats.Rtf, rtf))`) silently pastes nothing; `Clipboard.SetFileDropList(...)` then `Clipboard.GetDataObject().GetDataPresent(DataFormats.FileDrop)` is false while `Clipboard.ContainsFileDropList()` is true.
- **Fix:** Keep one process-wide `DataObject` as the source of truth: `SetDataObject` stores the object (cloning when `copy`), mirrors its `Text`/`UnicodeText`/`StringFormat` entry to the backend text clipboard, and `GetDataObject` returns the stored object if the backend text still equals what we last wrote, else a fresh text-only object; route `SetData/GetData/ContainsData/Clear/SetImage/GetImage/ContainsImage` through it. Extend the seam with `SetClipboardData(string format, byte[])`/`GetClipboardFormats` for Avalonia's `IDataObject` when cross-app formats matter.
- **Test:** Headless: `Clipboard.SetData("X", new[]{1,2})` then `Clipboard.ContainsData("X")` true and `GetData("X")` returns the array; `Clipboard.Clear()` → false.
- **Tests today:** ClipboardTests (text round-trips; `GetData_UnknownFormat_ReturnsNull` pins the current text-only behaviour).

### SVC-17 — `DataObject` format auto-conversion (`GetData(Type)`, `Text`↔`UnicodeText`↔`System.String`, `Bitmap`↔`Dib`, `FileDrop`↔`FileName`) — Cat A — P1 — High
- **Ours:** A flat dictionary keyed by exact format name; `autoConvert` is ignored (`src/Majorsilence.Forms/Clipboard.cs:186-206`). `new DataObject("hello").GetData(typeof(string))` → looks up `"System.String"` → `null`; `SetData(object)` stores under `GetType().FullName` so `GetData(DataFormats.Text)` → `null`; `GetDataPresent(DataFormats.UnicodeText)` after `SetText` → `false`.
- **Upstream:** `DataStore.GetData(format, autoConvert)` consults `DataFormatNames.AddMappedFormats` so Text/UnicodeText/System.String (and Bitmap/Dib, FileDrop/FileName/FileNameW) are interchangeable; `GetData(string)` defaults `autoConvert: true` (`OLE/DataObject.cs:113-131`, `src/System.Private.Windows.Core/src/System/Private/Windows/Ole/DataStore.cs:44-50`, `DataFormatNames.cs:78`).
- **Impact:** The two most common consumer idioms — `e.Data.GetDataPresent(typeof(string))` / `(string)e.Data.GetData(typeof(string))` and `GetDataPresent(DataFormats.Text)` — return false/null for text stored the other way; `new DataObject(DataFormats.UnicodeText, s).GetText()` is empty.
- **Fix:** Add the upstream mapped-format table to `DataObject`: on `GetData/GetDataPresent(format, autoConvert=true)` try the format then each mapped name; `GetData(Type)` → `GetData(type.FullName)` with mapping (string → Text/UnicodeText); `GetFormats(true)` union the mapped names. Also add the missing `DataObject(object data)` constructor (upstream has it; only `(string)` and `(string, object)` exist here).
- **Test:** `new DataObject("hi").GetData(typeof(string))` == "hi"; `new DataObject(DataFormats.UnicodeText, "hi").GetDataPresent(DataFormats.Text)` true.
- **Tests today:** MidSizeControlParity.ThreeTests (typed helpers), none for conversion.

### SVC-18 — `Clipboard.GetText(TextDataFormat)` / `ContainsText(TextDataFormat)` / `SetText(text, format)` — Cat A — P2 — High
- **Ours:** All format overloads collapse to plain text (`src/Majorsilence.Forms/Clipboard.cs:43`, `:55`, `:58`): `GetText(Rtf)` returns the plain text; `ContainsText(Rtf)` is true whenever any text is present.
- **Upstream:** Each `TextDataFormat` maps to its own clipboard format; `GetText(Rtf)` returns `string.Empty` when no RTF is present (`OLE/Clipboard.cs:118`, `:391-396`).
- **Impact:** `if (Clipboard.ContainsText(TextDataFormat.Rtf)) rtb.SelectedRtf = Clipboard.GetText(TextDataFormat.Rtf);` feeds plain text into an RTF parser; CSV/HTML paste paths receive the wrong payload.
- **Fix:** Route through the SVC-16 process `DataObject` (`SetText(text, format)` → `SetData(FormatName(format))`, `GetText(format)` → that entry only; `Text`/`UnicodeText` fall back to the backend text).
- **Test:** `Clipboard.SetText("plain"); Assert.False(Clipboard.ContainsText(TextDataFormat.Rtf)); Assert.Equal("", Clipboard.GetText(TextDataFormat.Rtf))`.
- **Tests today:** ClipboardTests `GetText_WithFormat_DelegatesToText` pins the wrong behaviour.

### SVC-19 — `MessageBox.Show(...)` with no open form — Cat A — P1 — High
- **Ours:** When `Application.ModalOwnerCandidates` is empty the box is shown non-modally and `DialogResult.OK` is returned immediately (`src/Majorsilence.Forms/WinFormsCompat.cs:912-919`); `Form.ShowDialog()` has the same fallback (`src/Majorsilence.Forms/Form.cs:731-740`). The owner-less overloads also pick `FirstOrDefault()` (the first-opened form) rather than the active one.
- **Upstream:** Always modal; with `owner == null` the active window is used (`Dialogs/MessageBox.cs:459`).
- **Impact:** Startup-time errors (`Main` catch blocks before `Application.Run`, license checks, "database unavailable") flash a box and continue as if the user clicked OK — often into `Application.Exit()` or a null-reference cascade before the user can read it. A `YesNo` prompt returns `OK`.
- **Fix:** When no owner exists, run the dialog through `Form.RunModal(form.ShowDialogAsync(null))` (allow a null parent in `ShowDialogAsync`) so the call blocks until closed; prefer `Form.ActiveForm` over `FirstOrDefault()` for centring/ownership.
- **Test:** Headless: with no open forms, `MessageBox.Show("x", "", MessageBoxButtons.YesNo)` on a background-completed dialog must return the clicked result, not `OK`.
- **Tests today:** LoadTimeDialogModalityTests (dialogs during `Load`), none for the no-form path.

### SVC-20 — `MessageBox` default button / Enter / Escape mapping — Cat A — P1 — Medium
- **Ours:** `MessageBoxForm` sets neither `AcceptButton` nor `CancelButton`, and no control is selected on show, so Enter does nothing (Button activates only on `KeyUp` when focused, `src/Majorsilence.Forms/Button.cs:224`), and Escape closes any box with `DialogResult.Cancel` via the modal fallback (`WindowBase.cs:1364-1366`) — including `OK` and `YesNo` boxes. `MessageBoxDefaultButton` is accepted and discarded (`WinFormsCompat.cs:1015-1025`).
- **Upstream:** Win32 `MessageBox` focuses the default button (Button1 unless `MB_DEFBUTTON2/3`), Enter activates it, Escape maps to Cancel/No/Ignore only when such a button exists (`Dialogs/MessageBox.cs:439-490`, `GetMessageBoxStyle`).
- **Impact:** Keyboard users cannot dismiss a message box with Enter; Escape on a `YesNo` box returns `Cancel`, which the app never expects.
- **Fix:** In `MessageBoxForm`, set `AcceptButton` to the button indexed by `defaultButton`, select it on `OnShown`, set `CancelButton` to Cancel/No(YesNo? none)/Ignore per Win32 table, and (with SVC-03) remove the modal-Escape fallback.
- **Test:** Headless `MessageBoxForm` with `YesNo`: `HandleKeyDown(Keys.Return)` → `DialogResult.Yes`; `HandleKeyDown(Keys.Escape)` → still `None`.
- **Tests today:** none.

### SVC-21 — `MessageBoxForm` sizing by newline count — Cat A — P1 — Medium
- **Ours:** Size is chosen from three fixed buckets by counting `'\n'` only (`src/Majorsilence.Forms/MessageBoxForm.cs:87-96`); a 400x200 form leaves roughly 135 px for the label after the 45 px button panel and padding. Long single-paragraph text (exception messages, `ex.ToString()`) wraps in the label and is clipped; the icon is never drawn.
- **Upstream:** The dialog is measured to its text (wrapping at roughly 60% of the screen width) and grows vertically as needed.
- **Impact:** Error text is cut off with no scrollbar; the icon that distinguishes error/warning/info is absent.
- **Fix:** Measure the label with `TextMeasurer`/`Graphics.MeasureString` at a max width (e.g. `Screen.WorkingArea.Width * 0.6`), size the form to text + icon + buttons, clamp to the working area; draw a 32x32 icon for `MessageBoxIcon` on the left.
- **Test:** Headless render of a 600-char message: label's preferred height <= label bounds height.
- **Tests today:** none.

### SVC-22 — `FileDialog.FileName` — Cat A — P1 — High
- **Ours:** Setter does `Path.GetFullPath(value)` (`src/Majorsilence.Forms/FileDialog.cs:129`) — `dlg.FileName = ""` throws `ArgumentException` ("The path is empty"), and any relative name is resolved against the process CWD; getter returns `null` when nothing is selected (`:124`) and the property is typed `string?`.
- **Upstream:** Getter returns `string.Empty` when unset; setter stores the value verbatim (`Dialogs/CommonDialogs/FileDialog.cs:163-167`).
- **Impact:** `saveFileDialog.FileName = "";` (designer/reset idiom) crashes; `dlg.FileName.Length`, `.EndsWith(...)`, `Path.GetExtension(dlg.FileName)` NRE after a cancelled dialog.
- **Fix:** Store verbatim (skip empty), return `string.Empty` when empty, type as `string`.
- **Test:** `new SaveFileDialog { FileName = "" }` does not throw and `FileName == ""`; `new OpenFileDialog().FileName == ""`.
- **Tests today:** FileDialogTests `FileDialog_FileName_Set_GetReturnsFullPath` pins the divergent full-path behaviour.

### SVC-23 — `FileDialog.FileOk` never raised; `AddExtension`/`DefaultExt` not applied to results — Cat D — P1 — High
- **Ours:** `FileOk`/`OnFileOk` are declared (`src/Majorsilence.Forms/TailParity.cs:517-520`) but `OpenFileDialog.ShowDialogAsync`/`SaveFileDialog.ShowDialogAsync` set `filenames` and return without raising it (`OpenFileDialog.cs:38-55`, `SaveFileDialog.cs:22-42`). `DefaultExt` is only forwarded to the backend for Save; `AddExtension` is never consulted, so an extension-less name returned by the picker (Open on macOS/Linux, or a backend ignoring `DefaultExtension`) stays extension-less.
- **Upstream:** After the shell returns, `ProcessFileNames` appends `DefaultExt`/the selected filter's extension when `AddExtension` and no extension, then `OnFileOk(CancelEventArgs)` runs and `Cancel = true` keeps the dialog open (`FileDialog.cs:396-436`, `:603`, `:620-632`).
- **Impact:** Validation hooks (`FileOk: if (!File.Exists(...)) e.Cancel = true`) and post-processing that apps attach to `FileOk` never run; saved files may lack the expected extension.
- **Fix:** After the backend returns, apply `AddExtension`/`DefaultExt`/filter extension per upstream, then `var e = new CancelEventArgs(); OnFileOk(e); if (e.Cancel) { re-show or return Cancel }`.
- **Test:** Headless backend stub returning `"/tmp/a"` with `DefaultExt = "txt"` → `FileName` ends with `.txt` and a `FileOk` handler ran.
- **Tests today:** FileDialogTests (property round-trips), FileDialogModalPumpTests.

### SVC-24 — `FileDialog.FilterIndex` never sent to or read back from the picker — Cat C — P1 — High
- **Ours:** Auto-property defaulting to 1 (`src/Majorsilence.Forms/FileDialog.cs:56`); `OpenFileRequest`/`SaveFileRequest` have no filter-index field (`src/Majorsilence.Forms/Backends/FileDialogRequests.cs`), so the initial selection is not applied and the user's choice is not reported.
- **Upstream:** `nFilterIndex` is passed in and read back from the OPENFILENAME/IFileDialog (`FileDialog.cs:269`, `:768`).
- **Impact:** Export dialogs that decide the format from `FilterIndex` ("1 = PNG, 2 = JPEG, 3 = BMP") always export format 1 regardless of what the user chose.
- **Fix:** Add `FilterIndex` to the requests and a `SelectedFilterIndex` to the results; Avalonia: pre-select via `FileTypeFilter` order is not settable, so infer the chosen filter from the returned file's extension (upstream-like heuristic) and update `FilterIndex`; when `AddExtension` applies, use the selected filter's first pattern.
- **Test:** Stub backend returning `x.jpg` with filters `PNG|*.png|JPEG|*.jpg` → `FilterIndex == 2`.
- **Tests today:** FileDialogTests `FilterIndex_Set_GetReturnsExpected` (round-trip).

### SVC-25 — `FileDialog.ShowDialog()` / `FolderBrowserDialog.ShowDialog()` with no open form, and `ShowDialog(IWin32Window)` ignoring the owner — Cat A — P1 — High
- **Ours:** Returns `DialogResult.Cancel` without showing anything when `Application.ModalOwnerCandidates` is empty (`src/Majorsilence.Forms/FileDialog.cs:142-146`, `FolderBrowserDialog.cs:38-42`); `ShowDialog(IWin32Window owner) => ShowDialog()` drops the owner (`FileDialog.cs:149`, `FolderBrowserDialog.cs:45`). `FileDialog` also does not derive from `CommonDialog`/`Component` (`FileDialog.cs:8`), so `CommonDialog`-typed code and `HelpRequest` do not apply to it.
- **Upstream:** `CommonDialog.ShowDialog(null)` uses the active window or creates a hidden owner window (`Dialogs/CommonDialogs/CommonDialog.cs:196-210`); an owner passed in is honoured.
- **Impact:** An app that asks for a file before creating its main form (config-file picker at startup, or from a `NotifyIcon` menu with no visible form) silently gets `Cancel`; a dialog owned by a secondary form is centred/parented to the first form instead.
- **Fix:** Resolve `owner as Form ?? (owner as Control)?.FindForm() ?? Form.ActiveForm ?? OpenForms.Last()`; when none, create a hidden owner `Form` for the duration (as upstream) rather than returning Cancel.
- **Test:** Headless: `new OpenFileDialog().ShowDialog()` with no forms must reach the backend's `ShowOpenFileDialog` (record on the stub).
- **Tests today:** FileDialogModalPumpTests (with an owner).

### SVC-26 — `FolderBrowserDialog.Description` never displayed; `SelectedPath` not used as the start folder — Cat C/A — P2 — High
- **Ours:** Only `Title` (a non-WinForms member) and `InitialDirectory` are forwarded (`src/Majorsilence.Forms/FolderBrowserDialog.cs:65-70`); `Description`/`UseDescriptionForTitle` are stored (`:23-33`), `SelectedPath` is write-only input.
- **Upstream:** `Description` is the dialog's prompt text (or title when `UseDescriptionForTitle`), and the start folder is `InitialDirectory` if set else `SelectedPath` (`Dialogs/CommonDialogs/FolderBrowserDialog.cs:337-378`).
- **Impact:** `fbd.Description = "Choose the export folder"; fbd.SelectedPath = lastFolder;` shows an untitled picker opened at the default location.
- **Fix:** `Title = UseDescriptionForTitle || Title.Length == 0 ? Description : Title`; `InitialDirectory = GetInitialDirectory() ?? (Directory.Exists(SelectedPath) ? SelectedPath : null)`.
- **Test:** Stub backend records the `FolderDialogRequest`; assert `Title == Description` and `InitialDirectory == SelectedPath`.
- **Tests today:** FolderBrowserDialogTests (round-trips).

### SVC-27 — `PrintDocument.Print()` bypasses `PrintController`; `PreviewPrintController.GetPreviewPageInfo()` always empty — Cat B — P1 — High
- **Ours:** `PrintToPdf` runs its own loop and never calls `PrintController.OnStartPrint/OnStartPage/OnEndPage/OnEndPrint` (`src/Majorsilence.Forms/Printing/PrintDocument.cs:87-147`); the property is documented "stored but not used" (`:149`). `PreviewPrintController` collects pages only in `OnStartPage` (`:227-262`), so through a real `Print()` it returns `[]`; `PrintControllerWithStatusDialog` is an empty shell (`:214-224`).
- **Upstream:** `PrintDocument.Print()` is `PrintController.Print(this)` (`src/System.Drawing.Common/src/System/Drawing/Printing/PrintDocument.cs:169-173`), and `PrintController.PrintLoop` drives `OnQueryPageSettings` → `OnStartPage` → `OnPrintPage` → `OnEndPage` (`PrintController.cs:99-135`).
- **Impact:** Any app that builds its own preview (`doc.PrintController = new PreviewPrintController(); doc.Print(); var pages = pc.GetPreviewPageInfo();`) gets zero pages; custom controllers (page counters, watermarking, progress) never run; `QueryPageSettings` (see SVC-30) never fires.
- **Fix:** Rewrite `Print()` as `PrintController.Print(this)`; give `PrintController.Print` the upstream loop (`OnStartPrint`, per-page `QueryPageSettings` clone, `OnStartPage` may return a `Graphics` to draw into, `OnEndPage`, `OnEndPrint`); make `StandardPrintController.OnStartPage` return the PDF page `Graphics` (moving the Skia PDF code there) and `PreviewPrintController.OnStartPage` return a bitmap `Graphics` as it already does.
- **Test:** `doc.PrintController = new PreviewPrintController(); doc.Print();` with a 3-page handler → `GetPreviewPageInfo().Length == 3`.
- **Tests today:** PrintingSurfaceTests `PreviewPrintController_captures_a_page_per_OnStartPage` (calls `OnStartPage` by hand, so it passes while `Print()` never does).

### SVC-28 — `PrintPageEventArgs.Graphics/PageBounds/MarginBounds` are in pixels at `PageSettings.Dpi` (96), not hundredths of an inch — Cat A — P1 — High
- **Ours:** `width_px = hundredths / 100 * dpi`, bounds built from those (`src/Majorsilence.Forms/Printing/PrintDocument.cs:97-113`, `:129`); Letter reports `PageBounds = 816x1056`, `MarginBounds = (96,96,624,864)`, and the canvas is scaled so 1 unit = 1/96 inch. `PageSettings.Bounds` (`PageSettings.cs:80-81`) is still in hundredths, so `e.PageBounds != e.PageSettings.Bounds`.
- **Upstream:** `CreatePrintPageEvent` builds both rectangles directly from `PageSettings.Bounds` and `Margins` in hundredths of an inch (Letter = 850x1100, margins (100,100,650,900)) (`PrintController.cs:198-209`), and the printer `Graphics` has `PageUnit = Display` = 1/100 inch (`DefaultPrintController.cs:30`; preview metafile sized from `PrinterUnit.Display`, `PreviewPrintController.cs:12-14`).
- **Impact:** Every migrated `PrintPage` handler is written in hundredths of an inch: `DrawString(..., 100, 100)` for a 1-inch offset, column x-positions like 150/400/650, `e.MarginBounds.Right - 200`. Here each unit is 1/96 inch, so all geometry is stretched by 4.17% and absolute layouts overflow the page by ~35 units on Letter; fonts (points) are correct, so text/layout proportions are visibly off. Handlers that read `e.PageBounds` are internally consistent but disagree with `e.PageSettings.Bounds`/`PrintableArea`.
- **Fix:** Report `PageBounds`/`MarginBounds` in hundredths (from `PageSettings.Bounds` and `Margins`), and scale the Skia canvas by `72/100` (PDF points per hundredth) so drawing units are 1/100 inch; set `Graphics.PageUnit = GraphicsUnit.Display` / `DpiX = DpiY = 100` on the wrapper so `MeasureString` agrees. Drop or repurpose `PageSettings.Dpi` (not an upstream member).
- **Test:** `PrintToPdf` with Letter/default margins: handler sees `e.PageBounds == new Rectangle(0,0,850,1100)` and `e.MarginBounds == new Rectangle(100,100,650,900)`.
- **Tests today:** PrintDocumentTests `MarginBounds_AreInsidePageBounds` (relative check only); PrintPageEventArgsParityTests (type only).

### SVC-29 — `PrintDocument.Print()` produces a temp PDF and nothing else — Cat B — P1 — High
- **Ours:** `Print()` writes `%TEMP%/<DocumentName>.pdf` and returns the path (`src/Majorsilence.Forms/Printing/PrintDocument.cs:69-74`); nothing opens it or submits it to a printer. Only `PrintPreviewDialog.ShowDialog()` launches a viewer (`PrintDialog.cs:49-57`). `PrinterSettings.InstalledPrinters` is empty, `PrinterName` is `""`, `IsValid` is true (`PrinterSettings.cs:70-100`).
- **Upstream:** `Print()` spools to `PrinterSettings.PrinterName` (default printer) via the print controller (`PrintDocument.cs:169`); `PrinterSettings.PrinterName` defaults to the OS default printer (`PrinterSettings.cs:303-311`).
- **Impact:** The user clicks Print (usually after a `PrintDialog` that returned OK immediately, SVC-31) and nothing observable happens — no output, no error. Matrix line 324-327 calls the PDF pipeline the intended substitute, but it never surfaces the PDF from `Print()`.
- **Fix:** Follow the `NativeAudio` precedent: on macOS/Linux submit via `lp [-d PrinterName] file.pdf` (and populate `InstalledPrinters`/default from `lpstat -p -d`), on Windows shell-print the PDF (`ProcessStartInfo { Verb = "print" }`); when `PrintToFile` is set write to `PrintFileName`; fall back to opening the PDF in the default viewer when no spooler is available.
- **Test:** With a test seam like `NativeAudio.LauncherOverride`, assert `Print()` invokes `lp` with the produced path (and `-d` when `PrinterName` is set).
- **Tests today:** PrintDocumentTests (PDF validity only).

### SVC-30 — `PrintDocument.QueryPageSettings` never raised; `OriginAtMargins` stored-only — Cat D/C — P2 — High
- **Ours:** Event under `#pragma warning disable CS0067` (`src/Majorsilence.Forms/Printing/PrintDocument.cs:39-42`); `OriginAtMargins` auto-property (`:45`); every page uses `DefaultPageSettings` (`:92`).
- **Upstream:** Raised per page with a clone of `DefaultPageSettings`; handler changes (e.g. `e.PageSettings.Landscape = true` for one page) apply to that page (`PrintController.cs:101-111`); `OriginAtMargins` translates the Graphics origin to the margin corner (`DefaultPrintController.cs:91-102`).
- **Impact:** Mixed-orientation reports print every page portrait; handlers written against margin-relative (0,0) draw at the paper corner.
- **Fix:** Part of SVC-27's loop: clone settings, `OnQueryPageSettings`, honour `e.Cancel`, use the per-page settings for page size/orientation; apply `TranslateTransform(Margins.Left, Margins.Top)` when `OriginAtMargins`.
- **Test:** Handler sets `Landscape = true` in `QueryPageSettings` for page 2 → page 2 `e.PageBounds.Width > Height`.
- **Tests today:** none.

### SVC-31 — `PrintDialog` / `PageSetupDialog` / `PrintPreviewDialog.ShowDialog(owner)` shows an empty `Form` — Cat A — P1 — High
- **Ours:** These are `Form` subclasses that hide only the parameterless overload with `new DialogResult ShowDialog() => DialogResult.OK` (`src/Majorsilence.Forms/PrintDialog.cs:34`, `:49-57`, `:101`). `Form.ShowDialog(Form)` / `ShowDialog(IWin32Window)` are inherited unchanged (`src/Majorsilence.Forms/Form.cs:744`, `:809`), so `printDialog.ShowDialog(this)` opens a blank modal window with no controls; the user must close it from the title bar and gets `Cancel`. `PrintDialog.PrinterSettings` with no `Document` returns a *new* `PrinterSettings` on every get and discards sets (`PrintDialog.cs:16-22`), so `pd.PrinterSettings.Copies = 2` is lost; `AllowSomePages`/`AllowSelection`/`PrintToFile` etc. are stored only.
- **Upstream:** `PrintDialog.PrinterSettings` is an independent property that `Document`'s setter aliases (`Printing/PrintDialog.cs:73-110`); `ShowDialog(owner)` shows the real dialog and copies the user's choices into that object.
- **Impact:** The overwhelmingly common `if (printDialog1.ShowDialog(this) == DialogResult.OK) printDocument1.Print();` shows a blank window and never prints; the owner-less form of the same code prints immediately with no choice of printer/pages/copies.
- **Fix:** Override all `ShowDialog` overloads consistently (`new` on `ShowDialog(Form)`/`ShowDialog(IWin32Window)` too, or better make these `CommonDialog` subclasses); give `PrintDialog` a minimal real UI (printer list from SVC-29, copies, page range enabled by `AllowSomePages`, print-to-file) writing back into `PrinterSettings`; back `PrinterSettings` with a field defaulting to `Document?.PrinterSettings ?? new()`.
- **Test:** `new PrintDialog { PrinterSettings = ps }.PrinterSettings` is the same reference; headless `ShowDialog(ownerForm)` shows a form containing at least one `Button`.
- **Tests today:** TailParity.TwoTests (property round-trips).

### SVC-32 — `PrintDocument.BeginPrint`/`EndPrint` typed `EventHandler` instead of `PrintEventHandler` — Cat E — P2 — High
- **Ours:** `public event EventHandler? BeginPrint; ... EndPrint;` and `OnBeginPrint(EventArgs)` (`src/Majorsilence.Forms/Printing/PrintDocument.cs:31-36`, `:118`, `:144`); `PrintEventArgs` exists but is unused by the document (`:281-285`).
- **Upstream:** `event PrintEventHandler BeginPrint/EndPrint` with `PrintEventArgs : CancelEventArgs` (`PrintDocument.cs:113-130`); `e.Cancel = true` in `BeginPrint` aborts the job, `e.PrintAction` tells the handler whether this is preview/file/printer.
- **Impact:** A method `void doc_BeginPrint(object sender, PrintEventArgs e)` cannot be subscribed (compile error on the ported designer line); `e.Cancel` is unavailable, so "no data to print → cancel" logic has to be rewritten.
- **Fix:** Change the events to `PrintEventHandler`, raise `new PrintEventArgs { PrintAction = controller.IsPreview ? PrintToPreview : PrintToPrinter }`, and stop the job when `e.Cancel` after `BeginPrint`.
- **Test:** Subscribe a `PrintEventHandler` setting `e.Cancel = true` → `PrintPage` never raised.
- **Tests today:** PrintDocumentTests (does not touch BeginPrint).

### SVC-33 — `PrintPreviewControl` renders nothing; `PrintPreviewDialog` launches an external viewer — Cat B — P2 — High
- **Ours:** `PrintPreviewControl` has `Document`/`Zoom`/`Rows`/`Columns`/`StartPage` stored properties and no `OnPaint` (`src/Majorsilence.Forms/PrintDialog.cs:107-118`, `TailParity.Two.cs:307-343`); `PrintPreviewDialog.ShowDialog()` calls `Document.Print()` and `Process.Start` on the PDF (`PrintDialog.cs:49-57`) — `ShowDialog(owner)` shows an empty form (SVC-31).
- **Upstream:** `PrintPreviewControl` runs the document through `PreviewPrintController` and paints the captured pages in a Rows x Columns grid with zoom (`Printing/PrintPreviewControl.cs:18-233`).
- **Impact:** Apps embedding `PrintPreviewControl` in their own preview window (very common in reporting UIs) show a blank control; `PrintPreviewDialog.ShowDialog(this)` shows nothing useful.
- **Fix:** Once SVC-27 exists, implement `PrintPreviewControl.OnPaint` to run `Document` through a `PreviewPrintController` (cached until `InvalidatePreview`) and draw `Rows*Columns` pages from `StartPage` scaled by `Zoom`/`AutoZoom`; host it in `PrintPreviewDialog` with a small toolbar (zoom, page nav, print).
- **Test:** Headless render of a `PrintPreviewControl` with a one-page document that fills the page black → non-white pixels in the control's bitmap.
- **Tests today:** none.

### SVC-34 — `SendKeys.Send` / `SendWait` — Cat B — P1 — Medium
- **Ours:** Empty bodies (`src/Majorsilence.Forms/WinFormsCompat.cs:3630-3640`), listed in NoOpStubBaseline. Rated here because the input pipeline it needs (`WindowBase.HandleKeyDown/HandleTextInput`) exists.
- **Upstream:** Parses the `{TAB}`, `{ENTER}`, `^c`, `+{F4}` grammar and injects via `SendInput`/journal hook into the active window (`SendKeys/SendKeys.cs:900-985`).
- **Impact:** `SendKeys.Send("{TAB}")` in a `KeyPress` Enter handler (the classic VB6-derived "Enter acts as Tab" idiom) does nothing; test/automation code that types into the active control does nothing.
- **Fix:** Implement the parser (upstream's grammar is small) and dispatch to `Form.ActiveForm.HandleKeyDown/HandleTextInput/HandleKeyUp`; `SendWait` = synchronous, `Send` = `Post`.
- **Test:** Headless form with two TextBoxes, first focused; `SendKeys.SendWait("{TAB}")` → second focused; `SendKeys.SendWait("abc")` → its Text == "abc".
- **Tests today:** none (AutomationTests' `SendKeys` is the WebDriver server, unrelated).

### SVC-35 — `FontDialog` — `Apply` never raised, `MinSize`/`MaxSize`/`FontMustExist`/`FixedPitchOnly` not applied, `Underline`/`Strikeout` lost on OK, `Color` never editable — Cat A/C/D — P2 — High
- **Ours:** `Apply` is `add {} remove {}` (`src/Majorsilence.Forms/FontDialog.cs:154`); `MinSize`/`MaxSize` only clamp each other and never reach `size_box.Minimum/Maximum` (`:114-140`, `size_box` fixed 1..512 at `:34`); `BuildFont` composes only Bold/Italic so a font set with `FontStyle.Underline` comes back without it after OK (`:49-63`); the family is a free-text `TextBox` and an unknown name silently becomes Arial (`:58-62`); `ShowColor` has no colour picker (`Color` is stored only, `:83-86`).
- **Upstream:** `CF_LIMITSIZE` enforces `MinSize`/`MaxSize`, `CF_EFFECTS` shows Underline/Strikeout/Color, `CF_FORCEFONTEXIST` validates, `OnApply` fires on the Apply button (`Dialogs/CommonDialogs/FontDialog.cs:166-168`, `:176-230`, `:246-268`, `:287-361`).
- **Impact:** Font round-trips drop underline/strikeout; Apply-button previews never run; users cannot pick a colour or browse installed fonts.
- **Fix:** Replace the family `TextBox` with a `ListBox`/`ComboBox` over installed families (`FontFamily.Families`), add Underline/Strikeout `CheckBox`es shown when `ShowEffects`, a colour button when `ShowColor`, an Apply button when `ShowApply` raising `OnApply`, and set `size_box.Minimum/Maximum` from `MinSize`/`MaxSize` (0 = unbounded).
- **Test:** `dlg.Font = new Font("Arial", 10, FontStyle.Underline)`; click OK (headless `PerformClick`) → `dlg.Font.Underline`.
- **Tests today:** FontDialogTests (round-trips).

### SVC-36 — `ColorDialog` — `CustomColors`/`FullOpen`/`AllowFullOpen`/`AnyColor`/`SolidColorOnly` stored only; no arbitrary colour entry — Cat C — P2 — High
- **Ours:** A 40-swatch grid; clicking a swatch immediately returns OK (`src/Majorsilence.Forms/ColorDialog.cs:33-62`); the 16 `CustomColors` slots are normalised and stored (`:104-107`, `:126-146`) but never displayed or updated; the current `Color` is not highlighted.
- **Upstream:** `CC_FULLOPEN`/`CC_ANYCOLOR`/`lpCustColors` open the RGB/HSL editor and persist the user's custom colours back into `CustomColors` (`Dialogs/CommonDialogs/ColorDialog.cs:72-136`).
- **Impact:** Apps that persist `CustomColors` between sessions round-trip the stored array but users can never define a custom colour; any colour outside the 40 presets is unreachable.
- **Fix:** Add a "Define Custom Colors >>" pane (R/G/B numeric up-downs + preview) shown when `AllowFullOpen` (initially when `FullOpen`), write the chosen value into the first free `CustomColors` slot (BGR int), render the 16 custom swatches, and require OK (swatch click selects only).
- **Test:** Headless: set RGB fields to (1,2,3), click "Add to Custom Colors" → `CustomColors[0] == 0x030201`.
- **Tests today:** ColorDialogTests (round-trips).

### SVC-37 — `Control.MouseHover` raised immediately on enter (no dwell) — Cat A — P2 — High
- **Ours:** `OnMouseEnter` raises `OnMouseHover` synchronously; re-armed hover fires on the next move (`src/Majorsilence.Forms/Control.cs:1310-1372`); `SystemInformation.MouseHoverTime` returns 400 but is unused (`SystemInformation.cs:71-74`).
- **Upstream:** `TrackMouseEvent` → WM_MOUSEHOVER after `MouseHoverTime` of rest (`Control.cs:5826-5842`, `:11638-11644`).
- **Impact:** Tooltip-on-hover and "hover to preview" handlers fire the instant the pointer crosses the border, including during fast pass-through, so transient popups flicker.
- **Fix:** Start a `Timer(SystemInformation.MouseHoverTime)` on enter/move (restart on move while not yet raised), raise on tick, cancel on leave.
- **Test:** Headless: `RaiseMouseEnter` then assert `MouseHover` count 0 until the fake timer elapses.
- **Tests today:** none.

### SVC-38 — `Cursor.Hide()/Show()`, `Cursor(Stream)/Cursor(string)/Cursor(IntPtr)` — Cat B — P2 — High
- **Ours:** `Hide`/`Show` empty (`src/Majorsilence.Forms/Cursor.cs:81-84`, in baseline); the three constructors produce `Arrow` (`:19-37`).
- **Upstream:** `ShowCursor(false/true)` (`Input/Cursor.cs:373`, `:463`); `.cur`/`.ani` resources load as real cursors.
- **Impact:** Kiosk/game/presentation apps cannot hide the pointer; custom cursors shipped as resources show as the arrow.
- **Fix:** Add `CursorType.None` mapped to `Avalonia.Input.Cursor(StandardCursorType.None)` and have `Hide/Show` set/clear an app-wide override applied in `WindowBase`; decode `.cur` (ICO-format, trivial: BITMAPINFOHEADER + AND mask) into an `SKBitmap` and add a bitmap-cursor path to the seam (Avalonia `new Cursor(Bitmap, PixelPoint)`).
- **Test:** After `Cursor.Hide()`, the recording backend saw `SetCursor(None)`.
- **Tests today:** CursorTests (no-throw only).

### SVC-39 — `IDataObject` returned by `Clipboard.GetDataObject()` is case-sensitive and `SetData` is a no-op — Cat A — P2 — High
- **Ours:** `ClipboardDataObject.GetDataPresent/GetData` compare with `==` while `Clipboard.ContainsData` uses `OrdinalIgnoreCase`; all `SetData` overloads are empty (`src/Majorsilence.Forms/Clipboard.cs:98-112`, in baseline as `ClipboardDataObject.SetData`).
- **Upstream:** Returns the live `DataObject`; `SetData` on it mutates the object (`OLE/Clipboard.cs:55-60`).
- **Impact:** `var d = Clipboard.GetDataObject(); d.SetData(...); Clipboard.SetDataObject(d)` (augment-then-restore idiom) loses the added data.
- **Fix:** Subsumed by SVC-16 (return the process `DataObject`).
- **Test:** As SVC-16.
- **Tests today:** ClipboardTests `SetDataObject_GetDataObject_RoundTripsText`.

## Low-priority / Win32-only (P3) — one line each
- `Cursor.Position` setter, `Cursor.Clip`, `Cursor.Handle/CopyHandle/Size/HotSpot/Draw/DrawStretched` — pointer warping/clipping and HCURSOR have no backend counterpart; documented in code.
- `FileDialog.RestoreDirectory/DereferenceLinks/ValidateNames/SupportMultiDottedExtensions/ShowReadOnly/ReadOnlyChecked/ShowHelp/AutoUpgradeEnabled/ClientGuid/AddToRecent/ShowPinnedPlaces/OkRequiresInteraction/CustomPlaces` — OPENFILENAME/IFileDialog shell flags; the platform picker owns them. (`SaveFileDialog.CheckFileExists` defaults true here vs false upstream — stored-only, harmless.)
- `FolderBrowserDialog.RootFolder/ShowNewFolderButton/Multiselect` — shell namespace roots and picker chrome; `Multiselect` stored, `SelectedPaths` derived from the single path.
- `MessageBoxOptions.RightAlign/RtlReading/ServiceNotification/DefaultDesktopOnly`, help-button overloads — desktop/RTL Win32 flags; help subsystem absent.
- `Help.ShowHelp*/ShowPopup`, `HelpProvider.SetShowHelp/SetHelpNavigator`, F1 → `HelpRequested` — CHM/HTMLHelp; a URL argument could be `Process.Start`ed as a cheap win.
- `CommonDialog.HookProc`, `ColorDialog.HookProc`, `FontDialog.HookProc`, `PageSettings/PrinterSettings.*Hdevmode*/*Hdevnames*` — Win32 common-dialog procs and DEVMODE handles (already throw by design).
- `PrinterSettings.IsValid/IsDefaultPrinter/CanDuplex/Duplex/Collate/LandscapeAngle/PrinterResolutions/PaperSources/MaximumCopies`, `PageSettings.PrinterResolution/PaperSource/Color/HardMarginX/Y` — driver capabilities; constants are honest for a PDF target.
- `FontDialog.AllowScriptChange/AllowSimulations/AllowVectorFonts/AllowVerticalFonts/ScriptsOnly/ShowHelp` — GDI charset/raster-font flags.
- `InputLanguage.Handle/LayoutName`, `InputLanguage.CurrentInputLanguage` setter — HKL keyboard layouts.
- `OSFeature/FeatureSupport` — always absent; correct direction for callers (documented).
- `SystemInformation.*` constants (DoubleClickTime 500 matches `WindowBase.DOUBLE_CLICK_TIME`; `MouseWheelScrollLines` 3, `WheelDelta` 120 matches `WheelDeltaAccumulator`) — Win32 metrics; fine as constants.
- `Clipboard.SetText(null|"")` not throwing, `Clipboard.SetDataObject(..., retryTimes, retryDelay)` ignoring retries — harmless leniency.
- `DataFormats.GetFormat(string)` returning a new id-0 `DataFormat` each call — no registered-format table; only `Id` differs.
- `SoundPlayer`/`SystemSounds`/`ComputerInfo`/`VbInteraction.InputBox` — real implementations; no divergence found beyond SVC-01's button mapping.

## Systemic patterns
- **The keyboard processing chain is declared but never dispatched.** `ProcessCmdKey`/`ProcessDialogKey`/`ProcessDialogChar`/`IsInputKey`/`IsInputChar`/`ProcessMnemonic`/`ProcessKeyPreview`/`PreProcessMessage` exist on both `Control` and `WindowBase` purely for compilation; `WindowBase.HandleKeyDown` and `ControlAdapter.RaiseKeyDown` hard-code AcceptButton/Escape/Tab ahead of the focused control instead. One dispatch rewrite (SVC-02) unblocks SVC-03/04/05/06 and lets AcceptsReturn/AcceptsTab/StandardTab/PreviewKeyDown.IsInputKey work.
- **Text-only seam, everything else stored in-process and inconsistently.** Clipboard (`IPlatformBackend` has three text methods) and `DataObject` (flat exact-name dictionary) have no format model; the typed helpers write a separate static `DataObject` that the core accessors ignore. Fix by making one process `DataObject` authoritative and mirroring text to the backend (SVC-16/17/18/39).
- **Live OS statics implemented as auto-properties.** `Cursor.Current`, `Control.MouseButtons`, `Control.UseWaitCursor` (setter writes an auto-prop while the getter reads a `States` flag), `Application.UseWaitCursor`, `Cursor.Position` setter. The pointer/key handlers in `WindowBase` already have the data — write it there, as `Cursor.TrackPosition` already does.
- **`new` hiding only one overload of `ShowDialog`.** `PrintDialog`/`PageSetupDialog`/`PrintPreviewDialog` hide `ShowDialog()` but inherit `Form.ShowDialog(owner)`, so the owner overload shows an empty window. Dialogs should either be `CommonDialog` subclasses or override every overload.
- **Result paths cover only the common subset.** `MessageBoxForm.AddButtons` (3 of 7 button sets), `Clipboard.SetDataObject` (only `"Text"`), `ColorDialog` (40 presets), `FontDialog.BuildFont` (Bold/Italic only). The fallback is a silently wrong success value (`OK`, empty, Arial) rather than a visible failure.
- **No-owner fallbacks return a synthetic result.** `MessageBox.Show`/`Form.ShowDialog()` → non-modal + `OK`; `FileDialog`/`FolderBrowserDialog.ShowDialog()` → `Cancel` without showing. Upstream always shows; use the active form or a hidden owner.
- **Print pipeline is a parallel implementation, not the WinForms object model.** `PrintDocument.PrintToPdf` inlines what `PrintController.Print` should do, so `PrintController`, `QueryPageSettings`, `OriginAtMargins`, `PrintEventArgs.Cancel` and `PreviewPrintController` are all bypassed, and it chose pixel units where upstream uses hundredths of an inch.
- **Existing tests pin the divergence.** `CursorTests` asserts `HSplit == SizeWestEast`; `FileDialogTests` asserts `FileName` returns a full path; `ClipboardTests` asserts `GetText(Rtf)` returns plain text; `PrintingSurfaceTests` calls `OnStartPage` by hand. These need to flip alongside the fixes.
