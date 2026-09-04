# Simple and value controls — findings

## Summary
The simple controls split cleanly into two halves. Button/CheckBox/RadioButton/Label/LinkLabel/TrackBar/ScrollBar/ProgressBar are genuinely implemented — real state machines, real renderers — and their defects are precise: a missing guard, an event raised from the wrong place, a property the renderer forgot to read. The value controls are the opposite: `NumericUpDown` has no keyboard input and steps by a hard-coded `1` instead of `Increment`; `DateTimePicker` is a `TextBox` whose drop-down arrow is painted but dead; `MonthCalendar` draws one line of text where a calendar should be; `ErrorProvider` renders nothing at all. The dominant failure patterns are (1) a setter that stores and invalidates while no renderer ever reads the value, (2) events declared with `#pragma warning disable CS0067` or — worse — `add { } remove { }` accessors that discard the subscription, and (3) a base class chosen for local convenience (`DateTimePicker : TextBox`, `DomainUpDown : NumericUpDown`, `NumericUpDown : Control`) that then behaves wrongly through inheritance. 61 findings: 6 P0, 27 P1, 28 P2, plus a short P3 list. Every control here already has a test file, but those tests assert property round-trips only — none of the 61 divergences is covered, which is exactly the blind spot `docs/winforms-gap-plan.md` predicted.

## Status (2026-09-04, W5.20a — the scroll and spin arithmetic)

**Closed:** SMP-31 (P0), SMP-48, SMP-49, plus the wheel's missing `EndScroll`. 12 tests in
`ScrollAndSpinArithmeticTests.cs`, 11 verified to fail with their fix neutralized; 1 is labelled
in-test as a guard. One existing test inverted, `ScrollBarTests.Wheel_raises_Scroll_with_the_proposed_value_before_Value_updates`.

**SMP-47 was already fixed and this file did not know.** `ScrollBar.PerformScroll` raises `Scroll` for
the arrows, the track, the thumb and the wheel, and finishes a drag with `EndScroll`; its comment names
the migrated app whose scrollbar regressed before it existed. The only part still missing was the
`EndScroll` after a *wheel* notch, which is now there. That makes three findings in this phase whose
defect had already been fixed (see also TSM-02 and TSM-14 in `toolstrip.md`): read the code before
trusting the entry.

**A correction to SMP-48's scope, which I got wrong first.** The clamp belongs on the user-driven paths
and the track mapping *only*. Putting `EffectiveMaximum` into `ScrollBar.UpdateFromValue` -- the shared
commit path -- makes a programmatic `Value = Maximum` silently land on `Maximum - LargeChange + 1`,
which upstream permits and validates. The finding says as much; the regression test for it is
`A_programmatic_assignment_may_still_reach_Maximum`.

**Why the wheel bug survived a test.** `ScrollBarTests` sent `Delta = -3` and asserted a move of 3,
which made `Value - Delta * SmallChange` read as "three units" instead of what a backend really sends
(±120 per notch). A test that picks its own input magnitudes can encode the defect.

**Still open in this file:** SMP-32 (P0), SMP-36/SMP-37 (structural, and ordered before SMP-32),
SMP-39/SMP-40/SMP-42 (all P0 -- the date-picking UI), SMP-33, SMP-41, SMP-43.

## Status (2026-09-04, W5.20d -- ErrorProvider renders)

**Closed:** SMP-51 (P0). 8 tests in `ErrorProviderRenderingTests.cs`, 7 verified to fail with their fix
neutralized; 1 is labelled in-test as a guard.

The icon is painted by the errored control's PARENT through a new `Control.PaintAdorners` event, which
runs after `PaintChildren`. That seam is the point: the public `Paint` event fires *before* the
children, so a decoration drawn from it is painted underneath them, and the finding's own suggested fix
("or have the parent's paint pass draw the icon") would have put the icon behind any control sharing
that space. Upstream uses a separate `ErrorWindow` per control; this framework has no child windows.

**A correction to the class documentation, which was half untrue.** It claimed the error text is "shown
in the control's ToolTip text if a ToolTip is set". Nothing read the dictionary but `GetError`, so no
tooltip was ever set either. Both halves are now accurate.

**Not implemented, and named as such rather than left to be discovered:** blinking
(`BlinkStyle`/`BlinkRate` are honoured as state, no timer runs), a custom `Icon` -- the built-in glyph
is always drawn -- and the hover tooltip. None of the three is what made this a P0.

**A trap worth recording, hit for the fourth time this phase.** The first version of these tests used
`PaintSurface.Render`, and every pixel assertion passed while nothing was drawn: `Control.Visible` is
ambient, so in a detached tree the paint loop's `!child.Visible` guard skipped every child.
`RenderOnForm` parents the container first. Together with the `Scaling == 0` / 0x0-bitmap trap, that is
two independent ways for a pixel test in this codebase to succeed by measuring nothing.

## Findings

## Status (2026-09-04, W5.20c — the `MonthCalendar` half of the date-picking pair)

**Closed:** `SMP-42` (P0) in full. Partially closed: `SMP-43` (the date/day-header/week-number/
adjacent-month hit areas now come back correctly; `TitleYear` and `TitleBackground` still do not) and
`SMP-46` (six of its eight properties are now consumed; `CalendarDimensions` > 1x1 is not drawn).

`MonthCalendar` draws a real grid and can be used to pick a date: a new
`Renderers/MonthCalendarRenderer.cs` registered with `RenderManager`, a new
`MonthCalendarGrid.cs` holding the layout, hit-testing, mouse selection and keyboard navigation, and
`DateSelected` turned into a field-backed event with `OnDateChanged`/`OnDateSelected` raisers.
`DateChanged` fires per day crossed during a drag and `DateSelected` exactly once on release. 44 tests
in `MonthCalendarBehaviourTests.cs`, 42 verified to fail with their fix neutralized; 2 are labelled
in-test as guards, because the control had no input handling at all before and no previous version
could fail them. Six entries left `StoredOnlyPropertyBaseline.txt` (746 → 740) and one left
`InertEventBaseline.txt` (64 → 63). Full account, including what was deferred and why, in
`docs/behaviour-gap-plan.md` under **W5.20c**.

**Two corrections to the findings as written.**

1. **`SMP-42`'s "Fix" implies `FirstDayOfWeek` was unconsumed. It was not** — `GetDisplayRange`
   already read it, as `SMP-46` correctly says ("`FirstDayOfWeek` in particular *is* consumed by
   `GetDisplayRange`"). The two findings disagree; `SMP-46` is the accurate one. Nothing else in
   `SMP-42`'s inventory was stale: every line number and every claim checked out against the source.

2. **A defect neither finding names: the doc comments on `ShowToday` and `ShowTodayCircle` were
   swapped.** `ShowToday` was documented as "whether today's date is circled" and `ShowTodayCircle` as
   "whether today's date is shown at the bottom" — each describing the other. Because nothing read
   either property, nothing contradicted them, which is `SMP-46`'s own point turned on the
   documentation: a stored-only property's *description* rots as silently as its value. Both are
   corrected.

**Also worth recording, because it is a shape of bug this file will meet again.** `SMP-43` calls the
`MidSizeControlParity.cs` header's claim — "HitTest and GetDisplayRange are computed from the same
geometry the renderer lays the control out with" — false, and it was. It is now true *structurally*
rather than by coincidence: one `Geometry` property is read by the renderer, by `HitTest` and by the
mouse handlers, so the three cannot drift apart again. Writing the geometry as a fourth, private copy
inside the renderer would have passed every test in this slice and left the original defect intact.

**Still open on `MonthCalendar`:** `SMP-44` (the two disagreeing bolded-date stores — the renderer
bolds through `IsBoldedDate`, so the `Add*BoldedDate` API shows, but the three array properties are
still the second store), `SMP-45` (the selection setters validating against the raw rather than the
effective min/max — the new *gesture* paths do clamp to the effective range), the `TitleYear`/
`TitleBackground` remainder of `SMP-43`, and the `CalendarDimensions` remainder of `SMP-46`.
`DateTimePicker` (`SMP-39`, `SMP-40`, `SMP-41`) is untouched and is the other half of W5.20c.

### SMP-01 — `RadioButton.Checked` / `UpdateSiblings` ignores `AutoCheck` — Cat A — P1 — High
- **Ours:** `UpdateSiblings()` unchecks *every* sibling `RadioButton` on the parent, with no regard for either this button's `AutoCheck` or the sibling's `AutoCheck` (`src/Majorsilence.Forms/RadioButton.cs:315-323`). `AutoCheck` is a bare auto-property (`RadioButton.cs:39`) with no setter side-effect.
- **Upstream:** `PerformAutoUpdates` returns immediately when `!_autoCheck`, and only unchecks a sibling when `radioButton.AutoCheck && radioButton.Checked` (`src/System.Windows.Forms/System/Windows/Forms/Controls/Buttons/RadioButton.cs:411-441`). The `AutoCheck` setter itself calls `PerformAutoUpdates(false)` (`RadioButton.cs:60-71`).
- **Impact:** The standard "manually managed radio group" pattern (`AutoCheck = false`, code decides who is checked) is broken: setting `Checked = true` on one member still wipes every other member, and a manual group can never show two checked buttons. Apps that mix an `AutoCheck=false` button into a group lose its state silently.
- **Fix:** In `RadioButton.Checked` setter guard `UpdateSiblings()` with `AutoCheck`; inside `UpdateSiblings` filter `rb.AutoCheck && rb.Checked`. Make `AutoCheck`'s setter call the same routine.
- **Test:** Two `RadioButton`s in a `Panel`, both `AutoCheck = false`; set `a.Checked = true; b.Checked = true;` and assert both remain `Checked`.
- **Tests today:** none found for AutoCheck semantics.

### SMP-02 — `RadioButton` never manages `TabStop` — Cat B — P1 — High
- **Ours:** `RadioButton`'s ctor (`src/Majorsilence.Forms/RadioButton.cs:32-35`) does not set `TabStop = false`, and nothing anywhere writes `TabStop` on a radio button; `UpdateSiblings` (`RadioButton.cs:315`) only touches `Checked`.
- **Upstream:** ctor sets `TabStop = false` (`Controls/Buttons/RadioButton.cs:48`); `PerformAutoUpdates` sets `TabStop = _isChecked` and `WipeTabStops` clears `TabStop` on every other radio button in the container (`RadioButton.cs:411-460`); `OnEnter` re-arms it (`RadioButton.cs:390-406`).
- **Impact:** Every radio button in a group is an individual tab stop. Tabbing through a form with a 6-option group now takes 6 tabs instead of 1, and the group is not entered on the currently-checked option — the classic WinForms "one tab stop per group, arrow keys within" behaviour is gone. Very visible on any data-entry form.
- **Fix:** Port `PerformAutoUpdates`/`WipeTabStops` verbatim into `RadioButton`; call from the `Checked` setter, the `AutoCheck` setter, and `OnEnter`. Set `TabStop = false` in the ctor.
- **Test:** Add three radios to a Panel, check the second, then assert `TabStop` is true only on the second.
- **Tests today:** none.

### SMP-03 — `RadioButton.Appearance` / `CheckBox.Appearance` stored only — Cat C — P1 — High
- **Ours:** `public Appearance Appearance { get; set; } = Appearance.Normal;` explicitly marked "Stub in Majorsilence.Forms" (`src/Majorsilence.Forms/RadioButton.cs:299`, `src/Majorsilence.Forms/CheckBox.cs:342`). `grep -rn 'Appearance\.Button' src/` returns nothing — no renderer reads it. `RadioButtonRenderer`/`CheckBoxRenderer` always draw the glyph + label form.
- **Upstream:** `Appearance.Button` swaps the whole rendering to a toggle button (`Controls/Buttons/RadioButton.cs:73-110`, and `ButtonInternal/CheckBoxBaseAdapter`/`ButtonStandardAdapter` selection in `ButtonBase.Adapter`).
- **Impact:** Toolbar-style toggle groups (a very common WinForms idiom: `Appearance = Button` radio buttons acting as a segmented control) render as ordinary radio circles, so the UI looks nothing like the designer intended and there is no pressed-state feedback.
- **Fix:** Have `RadioButtonRenderer`/`CheckBoxRenderer` branch on `Appearance`: when `Button`, delegate to the `ButtonRenderer` path and use `Checked`/`CheckState` to pick the pressed style. Make the setter raise `OnAppearanceChanged` + `Invalidate`.
- **Test:** Headless render a `RadioButton { Appearance = Appearance.Button, Checked = true }` and assert the glyph circle is absent / the background is the pressed style.
- **Tests today:** none.

### SMP-04 — `AppearanceChanged` never raised — Cat D — P2 — High
- **Ours:** `CheckBox.AppearanceChanged` / `RadioButton.AppearanceChanged` are declared in `src/Majorsilence.Forms/RemainingMemberParity.cs:91-106` with an `OnAppearanceChanged` raiser, but `Appearance` is an auto-property in both controls so the raiser is never invoked.
- **Upstream:** the `Appearance` setter raises `OnAppearanceChanged` after invalidating (`Controls/Buttons/RadioButton.cs:73-110`, `Controls/Buttons/CheckBox.cs:~100`).
- **Impact:** Handlers subscribed to `AppearanceChanged` never fire. Low traffic, but it is a natural trigger point that exists.
- **Fix:** Turn `Appearance` into a backed property that calls `Invalidate(); OnAppearanceChanged(EventArgs.Empty);`.
- **Test:** Subscribe, set `Appearance = Appearance.Button`, assert the handler ran once.
- **Tests today:** none.

### SMP-05 — `FlatStyle` / `FlatAppearance` unconsumed on CheckBox and RadioButton — Cat C — P1 — High
- **Ours:** both controls override the property purely to store it (`src/Majorsilence.Forms/CheckBox.cs:345,348`, `src/Majorsilence.Forms/RadioButton.cs:302,305`), both doc-commented "Stub in Majorsilence.Forms". Only `Button` actually folds `FlatAppearance` into its style chain (`src/Majorsilence.Forms/Button.cs:264-294`). `FlatButtonAppearance` itself (`src/Majorsilence.Forms/WinFormsCompat.cs:3348-3364`) documents every member as a stub.
- **Upstream:** `FlatStyle` picks the adapter (`ButtonBase.Adapter`), and `FlatAppearance.BorderSize/BorderColor/MouseOverBackColor/MouseDownBackColor/CheckedBackColor` are all consumed by `ButtonInternal/*FlatAdapter` when drawing.
- **Impact:** A flat/borderless checkbox or radio button keeps the themed 3-D style; hover/down/checked custom colours never appear. Designer-emitted `FlatAppearance.*` lines are inert.
- **Fix:** Lift `Button.ApplyFlatAppearance()` to `ButtonBase` (or an extension) and call it from `CheckBox`/`RadioButton`'s `CurrentStyle`. Also honour `MouseDownBackColor` and `CheckedBackColor`, which even `Button` ignores today.
- **Test:** Set `FlatStyle = Flat; FlatAppearance.BorderSize = 0` on a CheckBox and assert the rendered border width is 0.
- **Tests today:** none.

### SMP-06 — `Button.FlatAppearance.MouseDownBackColor` / `CheckedBackColor` ignored — Cat C — P2 — High
- **Ours:** `ApplyFlatAppearance()` reads only `BorderSize`, `BorderColor` and `MouseOverBackColor` (`src/Majorsilence.Forms/Button.cs:284-293`); `MouseDownBackColor` and `CheckedBackColor` are never read anywhere in `src/`.
- **Upstream:** consumed in the flat adapter's `PaintDown`/`PaintUp` paths.
- **Impact:** No pressed-state colour on flat buttons — a flat button looks completely inert while held down.
- **Fix:** Add a pressed style to the chain in `ApplyFlatAppearance` keyed off the button's pressed state.
- **Test:** Render a flat button in the pressed state, assert the background equals `MouseDownBackColor`.
- **Tests today:** none.

### SMP-07 — `Form.AcceptButton` never calls `NotifyDefault`; `IsDefault` never rendered — Cat B — P1 — High
- **Ours:** `Form.AcceptButton` is a bare auto-property (`src/Majorsilence.Forms/Form.cs:188`). `Button.NotifyDefault` exists and sets `IsDefault` + `Invalidate` (`src/Majorsilence.Forms/RemainingMemberParity.cs:80-87`), but nothing ever calls it, and no renderer reads `IsDefault` (`grep IsDefault src/Majorsilence.Forms/Renderers/` → nothing).
- **Upstream:** `Form.AcceptButton`'s setter calls `UpdateDefaultButton()`, which calls `NotifyDefault(true/false)`; `ButtonBase`/`Button` then draw the heavier default-button border, and focus moving between buttons re-targets the default (`Button.NotifyDefault`, `Form.UpdateDefaultButton`).
- **Impact:** The default (OK) button on every dialog is visually indistinguishable from the others — users cannot see which button Enter will hit.
- **Fix:** In `Form.AcceptButton`'s setter, call `NotifyDefault(false)` on the old value and `NotifyDefault(true)` on the new. Have `ButtonRenderer` widen/recolour the border when `IsDefault`.
- **Test:** `form.AcceptButton = btn;` assert `btn.IsDefault`; headless-render and assert the border differs from a non-default button.
- **Tests today:** none.

### SMP-08 — `Button.PerformClick()` ignores `CanSelect`/`Enabled` — Cat A — P2 — High
- **Ours:** `PerformClick()` unconditionally calls `OnClick(...)` (`src/Majorsilence.Forms/Button.cs:244-247`); same for `RadioButton.PerformClick` (`RadioButton.cs:308`).
- **Upstream:** `Button.PerformClick()` is guarded by `if (CanSelect)` — a disabled or invisible button does nothing (`Controls/Buttons/Button.cs`, `PerformClick`).
- **Impact:** `btn.Enabled = false; btn.PerformClick();` still runs the handler. Code that disables a button as a re-entrancy guard and then routes keyboard/accelerator clicks through `PerformClick` will double-execute.
- **Fix:** Guard both with `if (!CanSelect) return;`.
- **Test:** `btn.Enabled = false; btn.PerformClick();` assert the `Click` handler did not run.
- **Tests today:** none.

### SMP-09 — `ButtonBase.UseMnemonic` stored only — Cat C — P2 — High
- **Ours:** `public bool UseMnemonic { get; set; } = true;` on `ButtonBase` (`src/Majorsilence.Forms/WinFormsBaseControls.cs:44`). `ButtonRenderer`/`CheckBoxRenderer`/`RadioButtonRenderer` always call `DrawMnemonicText(...)` regardless (`src/Majorsilence.Forms/Renderers/ButtonRenderer.cs:29`, `CheckBoxRenderer.cs:39`, `RadioButtonRenderer.cs:40`), and `TextImageLayoutEngine` special-cases only `Label { UseMnemonic: false }` when deciding whether to strip `&` (`src/Majorsilence.Forms/Layout/TextImageLayoutEngine.cs:268-270`).
- **Upstream:** when `UseMnemonic` is false the `&` is drawn literally and no character is underlined (`ButtonBase.UseMnemonic`, consumed via `TextFormatFlags.NoPrefix`).
- **Impact:** A button captioned `"Save & Exit"` with `UseMnemonic = false` still renders as "Save  Exit" with `E` underlined — the ampersand disappears from the UI.
- **Fix:** Thread `UseMnemonic` through the three button renderers (pick `DrawText` vs `DrawMnemonicText`, as `LabelRenderer.cs:44-47` already does) and through `TextImageLayoutEngine`'s strip decision.
- **Test:** Measure/render `new Button { Text = "A & B", UseMnemonic = false }` and assert the drawn string still contains `&`.
- **Tests today:** none.

### SMP-10 — `ProcessMnemonic` is a hard `=> false`; Alt+letter access keys do nothing anywhere — Cat B — P1 — High
- **Ours:** `protected virtual bool ProcessMnemonic (char charCode) => false;` in `src/Majorsilence.Forms/Control.Compat.cs:606` and `src/Majorsilence.Forms/WindowBase.Compat.cs:53`, doc-commented "Majorsilence.Forms stub". Nothing in `src/` or `tests/` ever *calls* `ProcessMnemonic`, so no override could help either. `Control.IsMnemonic` exists (`src/Majorsilence.Forms/Control.cs:987`) but has no caller in the key path.
- **Upstream:** `Control.ProcessMnemonic`, overridden by `ButtonBase` (click the button), `Label` (focus the next control in tab order), `GroupBox`, `TabPage`, etc., driven from `Form.ProcessCmdKey`/`ProcessDialogChar` on Alt+char.
- **Impact:** Every `&`-mnemonic in a migrated app is dead: Alt+S does not press `&Save`, Alt+N does not focus the textbox after a `&Name:` label. Keyboard-driven data-entry apps become mouse-only. The `&` is still stripped from the caption, so the UI *advertises* accelerators that do not work.
- **Fix:** Dispatch Alt+char from the form's key handler down the control tree to `ProcessMnemonic`; override it in `ButtonBase` (→ `PerformClick`) and `Label` (→ `Parent.SelectNextControl(this, true, true, true, false)`), gated on `UseMnemonic`.
- **Test:** Headless: form with `&Save` button, send Alt+S, assert `Click` fired. Form with `&Name:` label followed by a TextBox: Alt+N focuses the TextBox.
- **Tests today:** none.

### SMP-11 — `ButtonBase.Command` / `CommandParameter` never executed — Cat C — P2 — High
- **Ours:** stored-only auto-properties on `ButtonBase` (`src/Majorsilence.Forms/TailParity.cs:39-43`); `CommandChanged`/`CommandParameterChanged`/`CommandCanExecuteChanged` sit under `#pragma warning disable CS0067` with a comment saying nothing raises them (`TailParity.cs:45-56`). Nothing invokes the command on `Click`.
- **Upstream:** `ButtonBase.OnClick` invokes `Command.Execute(CommandParameter)`; the button also subscribes to `CanExecuteChanged` and syncs `Enabled` (.NET 8 command binding).
- **Impact:** MVVM-ish apps that bind `button.Command` get a button that does nothing at all — no error, no execution.
- **Fix:** In `ButtonBase.OnClick`, after raising `Click`, call `Command?.Execute(CommandParameter)`; wire `CanExecuteChanged` → `Enabled` on assignment and raise the three notifications from the setters.
- **Test:** Assign a command whose `Execute` sets a flag; `PerformClick()`; assert the flag.
- **Tests today:** none.

### SMP-12 — Button/CheckBox/RadioButton `DefaultCursor` is `Hand` — Cat E — P2 — High
- **Ours:** `protected override Cursor DefaultCursor => Cursors.Hand;` on all three (`src/Majorsilence.Forms/Button.cs:81`, `CheckBox.cs:148`, `RadioButton.cs:~123`).
- **Upstream:** none of `Button`/`CheckBox`/`RadioButton` override `DefaultCursor`; they inherit `Control.DefaultCursor` = `Cursors.Default` (arrow). Hand is a web idiom, not a WinForms one.
- **Impact:** Every button in a migrated app shows a pointing hand. Cosmetic but pervasive and instantly noticed; also means an app that deliberately sets `Cursor = Cursors.Hand` on one button can't be distinguished.
- **Fix:** Drop the three overrides (or gate behind a theme option).
- **Test:** `Assert.Equal(Cursors.Default, new Button().Cursor)`.
- **Tests today:** none.

### SMP-13 — Button/CheckBox/RadioButton captions never word-wrap — Cat A — P1 — High
- **Ours:** all three renderers hard-code `maxLines: 1` (`src/Majorsilence.Forms/Renderers/ButtonRenderer.cs:29`, `CheckBoxRenderer.cs:39`, `RadioButtonRenderer.cs:40`).
- **Upstream:** `ButtonBaseAdapter.CreateTextFormatFlags` → `ControlPaint.CreateTextFormatFlags`, which unconditionally ORs `TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl` (`src/System.Windows.Forms/System/Windows/Forms/Rendering/ControlPaint.cs:2640-2652`). Button text wraps to as many lines as the button is tall.
- **Impact:** A tall button with a two-word caption ("Export\nSelected") that wrapped in WinForms now shows one clipped/ellipsised line. Same for multi-line checkbox labels, which are common on consent/option forms.
- **Fix:** Pass `maxLines: null` (or a height-derived count) in the three renderers and let the text engine wrap, matching `LabelRenderer`'s `Multiline` path.
- **Test:** Render a 60x60 button with text "Export Selected" and assert two text lines are laid out.
- **Tests today:** none.

### SMP-14 — `Label` does not word-wrap by default (`Multiline` defaults false) — Cat E — P1 — High
- **Ours:** `Label.Multiline` is a Majorsilence-only property defaulting to `false` (`src/Majorsilence.Forms/Label.cs:277-289`; the ctor at `Label.cs:38-47` sets only `UseMnemonic`), and `LabelRenderer` passes `maxLines: control.Multiline ? null : 1` (`src/Majorsilence.Forms/Renderers/LabelRenderer.cs:45-47`).
- **Upstream:** `Label` has no `Multiline` property at all — it *always* word-wraps (`Label.CreateTextFormatFlags` at `Controls/Labels/Label.cs:911-932` starts from `ControlPaint.CreateTextFormatFlags`, which always sets `WordBreak`, and only strips it when the text already fits).
- **Impact:** Every multi-line label in a migrated app — descriptions, warnings, wrapped column captions — collapses to one line and is truncated. This is the single most visible layout regression for a text-heavy form, and the app has no `Multiline` line in its designer file to fix it because upstream has no such property.
- **Fix:** Default `Multiline` to `true` (or make the renderer wrap whenever `AutoSize == false`), keeping the property as an opt-out.
- **Test:** Render a 100x60 label with a long sentence; assert more than one line is produced.
- **Tests today:** none.

### SMP-15 — `Label.BorderStyle` stored only, never drawn — Cat C — P1 — High
- **Ours:** `public virtual BorderStyle BorderStyle { get; set; } = BorderStyle.None;` (`src/Majorsilence.Forms/Label.cs:377`) — plain auto-property, no `Invalidate`, no layout. `LabelRenderer` (`src/Majorsilence.Forms/Renderers/LabelRenderer.cs`) draws only image + text; nothing in `src/Majorsilence.Forms/Renderers/` reads `Label.BorderStyle`.
- **Upstream:** the setter invalidates and re-layouts (`Controls/Labels/Label.cs:205-225`), the border is painted, and `GetBordersAndPadding` shrinks the text rectangle by 1px (FixedSingle) / 2px (Fixed3D) (`Label.cs:285-300`), which also changes `PreferredSize`.
- **Impact:** Labels used as separators/boxes (a common "poor man's group box" and status-strip idiom) lose their frame entirely, and text sits 1-2px off where it did.
- **Fix:** Map `BorderStyle` onto the control's `Style.Border` in a `CurrentStyle` override (as `Button.ApplyFlatAppearance` does), invalidate + `DoLayoutIf(AutoSize, ...)` in the setter, and subtract the border from the text bounds.
- **Test:** Headless render a `Label { BorderStyle = FixedSingle }` and assert the outermost pixel ring is the border colour.
- **Tests today:** none.

### SMP-16 — `Label.FlatStyle` / `Label.UseCompatibleTextRendering` / `LiveSetting` stored only — Cat C — P2 — High
- **Ours:** `Label.FlatStyle` auto-property (`src/Majorsilence.Forms/Label.cs:380`); `UseCompatibleTextRendering` and `LiveSetting` auto-properties in `src/Majorsilence.Forms/TailParity.Two.cs:165-168`.
- **Upstream:** `FlatStyle` combines with `BorderStyle` to select `Popup`/`System` border rendering (`Controls/Labels/Label.cs:285-300`); `LiveSetting` drives the UIA LiveRegion announcement.
- **Impact:** Cosmetic once SMP-15 is fixed (FlatStyle only matters when a border exists); `LiveSetting` means screen readers never announce label changes.
- **Fix:** Fold `FlatStyle` into the same border resolution as SMP-15; raise a UIA LiveRegionChanged from the automation peer on `TextChanged` when `LiveSetting != Off`.
- **Test:** Assert `FlatStyle = Popup` + `BorderStyle = FixedSingle` renders a different border than `Flat`.
- **Tests today:** none.

### SMP-17 — `Label.PreferredWidth`/`PreferredHeight` ignore border and mnemonic stripping — Cat A — P2 — Medium
- **Ours:** `PreferredHeight`/`PreferredWidth` = measured text + `Padding.Vertical`/`Horizontal` only (`src/Majorsilence.Forms/TailParity.Two.cs:166-172`), measuring `Text` raw (so a `&` counts as a glyph even when `UseMnemonic` is on).
- **Upstream:** `Label.PreferredHeight`/`PreferredWidth` add `GetBordersAndPadding()` and measure with the mnemonic-stripped, `WordBreak` flags (`Controls/Labels/Label.cs:285-300` and the `PreferredHeight` property).
- **Impact:** Layout code that positions the next control at `label.Top + label.PreferredHeight` is short by 2-4px per bordered label; a `&`-bearing caption over-measures by one character.
- **Fix:** Subtract/add the border thickness derived from `BorderStyle`, and measure `Mnemonics.Strip(Text)` when `UseMnemonic`.
- **Test:** `new Label { Text = "&Name", UseMnemonic = true }.PreferredWidth` should equal that of `Text = "Name"`.
- **Tests today:** none.

### SMP-18 — `LinkLabel` never shows the hand cursor over a link — Cat B — P2 — High
- **Ours:** `LinkLabel.OnMouseMove` only flips `LinkState.Hover` and invalidates (`src/Majorsilence.Forms/LinkLabel.cs:359-380`); the word `Cursor` does not appear anywhere in `src/Majorsilence.Forms/LinkLabel.cs`.
- **Upstream:** `OverrideCursor = Cursors.Hand` when the pointer is inside a link, cleared to `null` when it leaves (`src/System.Windows.Forms/System/Windows/Forms/Controls/Labels/LinkLabel.cs:928-933`, `:828`, `:1199`).
- **Impact:** Links look clickable but the pointer never changes, so users don't discover them — especially in a `LinkArea` covering only part of the label's text.
- **Fix:** In `OnMouseMove`/`OnMouseLeave`, set the effective cursor to `Cursors.Hand` when `PointInLink(e.Location)?.Enabled == true` and restore it otherwise.
- **Test:** Simulate a mouse move over the link range and assert the resolved cursor is `Cursors.Hand`.
- **Tests today:** none.

### SMP-19 — `LinkLabel` auto-marks links `Visited` on activation — Cat A — P2 — High
- **Ours:** `ActivateLink` sets `link.Visited = true` before raising `LinkClicked` (`src/Majorsilence.Forms/LinkLabel.cs:541-548`).
- **Upstream:** neither `OnMouseUp` nor the keyboard path sets `Visited`; `LinkLabel.cs:802`, `:889`, `:1400` just raise `OnLinkClicked`. Marking a link visited is the *application's* job (`linkLabel1.LinkVisited = true;` inside the handler is the canonical MSDN sample).
- **Impact:** Every clicked link immediately turns purple whether or not the app wanted that; apps that use `LinkVisited` as state ("has this row been opened?") get it set for them, so the flag no longer means what the app thinks.
- **Fix:** Remove the `link.Visited = true;` line from `ActivateLink`.
- **Test:** Click a link with no handler and assert `Links[0].Visited` is still `false`.
- **Tests today:** none.

### SMP-20 — `PictureBox.Load()` is asynchronous and swallows failures — Cat A — P1 — High
- **Ours:** `Load(url)` just assigns `ImageLocation`, whose setter calls `private async void LoadInternal(...)` (`src/Majorsilence.Forms/PictureBox.cs:94-137`). The method returns before any bytes are read, and the `catch (Exception)` sets `IsErrored` and swallows. A missing *local* file is worse: `SKBitmap.Decode(path)` returns `null` without throwing, so `_skImage` becomes `null`, `IsErrored` stays `false`, and the box silently renders nothing.
- **Upstream:** `PictureBox.Load()` is synchronous — it opens the stream, decodes, and `InstallNewImage` before returning, and rethrows on failure outside design mode (`src/System.Windows.Forms/System/Windows/Forms/Controls/PictureBox/PictureBox.cs:457-500`).
- **Impact:** `pb.Load(path); int w = pb.Image.Width;` throws `NullReferenceException` because the load has not happened yet; and `try { pb.Load(path); } catch (FileNotFoundException) { ... }` never catches — an unhandled exception in an `async void` can instead tear the process down on some paths.
- **Fix:** Make `Load(string)` do the read synchronously (decode from a `FileStream`/`HttpClient.GetByteArrayAsync().GetAwaiter().GetResult()` as upstream does) and rethrow; keep the async path for `LoadAsync`. Treat a `null` `SKBitmap.Decode` result as an error (`IsErrored = true`).
- **Test:** `Assert.Throws<...>(() => pb.Load("/does/not/exist.png"))`, and after a successful `Load` assert `pb.Image is not null` on the same thread.
- **Tests today:** none.

### SMP-21 — `PictureBox.LoadAsync` / `CancelAsync` / `LoadCompleted` / `LoadProgressChanged` are inert — Cat B/D — P1 — High
- **Ours:** `LoadAsync(url) => Load(url)` (fire-and-forget), `CancelAsync() { }`, and both events are declared as `add { } remove { }` so subscriptions are *discarded at the add site* (`src/Majorsilence.Forms/PictureBox.cs:179-189`).
- **Upstream:** `LoadAsync` runs a real async download, reports `LoadProgressChanged` and finally raises `LoadCompleted` with `AsyncCompletedEventArgs` carrying `Error`/`Cancelled`; `CancelAsync` cancels it (`Controls/PictureBox/PictureBox.cs`, `LoadAsync`/`CancelAsync`/`OnLoadCompleted`).
- **Impact:** The whole async image pattern is dead: the completion handler that hides the spinner and shows the image never runs, and no exception surfaces. The `add { }` accessor form means even a `-=` is meaningless — an app cannot detect the failure at runtime.
- **Fix:** Back the two events with real `EventHandler`/`AsyncCompletedEventHandler` fields, have `LoadInternal` raise `LoadProgressChanged` and `LoadCompleted`, and give `CancelAsync` a `CancellationTokenSource`. Also change the event types: upstream's `LoadCompleted` is `AsyncCompletedEventHandler` and `LoadProgressChanged` is `ProgressChangedEventHandler`, not `EventHandler` — the current signatures will not compile against migrated handler code.
- **Test:** Subscribe to `LoadCompleted`, `LoadAsync` a valid local file, and assert the handler ran.
- **Tests today:** none.

### SMP-22 — `PictureBox.ErrorImage` / `InitialImage` / `WaitOnLoad` stored only — Cat C — P2 — High
- **Ours:** three auto-properties doc-commented "Stub in Majorsilence.Forms" (`src/Majorsilence.Forms/PictureBox.cs:170-177`); `WaitOnLoad` even defaults to `true` where upstream defaults to `false`. `PictureBoxRenderer` draws a hard-coded red X for the error state and never consults `ErrorImage`, and never draws `InitialImage` (`src/Majorsilence.Forms/Renderers/PictureBoxRenderer.cs:44-49`).
- **Upstream:** `ErrorImage` (default a broken-image bitmap) and `InitialImage` are installed into the display during/after a failed or pending async load.
- **Impact:** Custom placeholder/error artwork never appears; a failed load shows a red X the app never asked for. `WaitOnLoad`'s inverted default means code branching on it takes the wrong path.
- **Fix:** Have the renderer draw `InitialImage` while a load is pending and `ErrorImage` when `IsErrored` (falling back to the X only when `ErrorImage` is null). Default `WaitOnLoad` to `false`.
- **Test:** Set `ErrorImage` to a known bitmap, `Load` a bad path, and assert the rendered pixels match it.
- **Tests today:** none.

### SMP-23 — `PictureBox.SizeMode` setter never `Invalidate()`s and does not sync `AutoSize` — Cat A — P1 — High
- **Ours:** the setter calls `UpdateSize(); OnSizeModeChanged(...)` (`src/Majorsilence.Forms/PictureBox.cs:142-154`), and `UpdateSize` returns immediately when there is no image and otherwise only resizes for `AutoSize` (`PictureBox.cs:205-223`) — there is no `Invalidate()` on any path that doesn't change `Size`.
- **Upstream:** the setter flips `AutoSize` / `ControlStyles.FixedHeight|FixedWidth`, saves `_savedSize`, then calls `AdjustSize(); Invalidate(); OnSizeModeChanged(...)` (`Controls/PictureBox/PictureBox.cs:821-847`).
- **Impact:** Switching `SizeMode` from `Normal` to `Zoom`/`StretchImage`/`CenterImage` at runtime leaves the previously painted image on screen until something else invalidates the control — the classic "click Fit and nothing happens" bug. Also `AutoSize` stays `false` after `SizeMode = AutoSize`, so any layout container that consults `AutoSize` mis-measures the box.
- **Fix:** Add `Invalidate();` to the setter and set `AutoSize = (value == PictureBoxSizeMode.AutoSize)` plus the saved-size restore, mirroring upstream.
- **Test:** With an image set, change `SizeMode` and assert the control's invalidated region is non-empty / the next render differs.
- **Tests today:** none.

### SMP-24 — `PictureBox.BorderStyle` stored only — Cat C — P2 — High
- **Ours:** `BorderStyle` forwards to `PictureBoxBorderStyle`, an auto-property doc-commented "Stub in Majorsilence.Forms" (`src/Majorsilence.Forms/PictureBox.cs:161-168`). `PictureBoxRenderer` never reads it.
- **Upstream:** the border is painted (and shrinks `ImageRectangle`) via `PictureBox.OnPaint`/`ClientRectangle` accounting.
- **Impact:** `BorderStyle = FixedSingle` (the near-universal designer setting for an image placeholder) draws no frame, and the image is drawn 1-2px larger than it should be.
- **Fix:** Map it onto the control's `Style.Border` and shrink `PaddedClientRectangle` accordingly; invalidate in the setter.
- **Test:** Render a bordered PictureBox and assert the outer ring is the border colour.
- **Tests today:** none.

### SMP-25 — `PictureBox` `Normal`/`AutoSize` ignore `Padding` — Cat A — P2 — High
- **Ours:** the `Normal`/`AutoSize` arm draws at `new Rectangle(0, 0, w, h)` (`src/Majorsilence.Forms/Renderers/PictureBoxRenderer.cs:25`), while every other arm uses `control.PaddedClientRectangle`.
- **Upstream:** `ImageRectangle` for `Normal` is anchored at the *client* rectangle's origin, i.e. inside padding and border.
- **Impact:** A PictureBox with `Padding` set (or, once SMP-24 lands, a border) draws its image overlapping the padding/border on the top-left. Inconsistent with the other four modes, so a `SizeMode` change visibly shifts the image.
- **Fix:** Use `control.PaddedClientRectangle.Location` as the draw origin in the `Normal`/`AutoSize` arm.
- **Test:** `pb.Padding = new Padding(10)`, render, assert the image's first opaque pixel is at (10,10).
- **Tests today:** none.

### SMP-26 — `ProgressBar.Style` (Blocks/Continuous/Marquee) never rendered; Marquee shows an empty bar — Cat C — P1 — High
- **Ours:** `ProgressBarRenderer.Render` computes `percent` from `Value` and fills one rectangle; it never reads `control.Style` (`src/Majorsilence.Forms/Renderers/ProgressBarRenderer.cs:9-21`). `MarqueeAnimationSpeed` is doc-commented "Stub in Majorsilence.Forms" (`src/Majorsilence.Forms/ProgressBar.cs:109-118`) and there is no timer anywhere.
- **Upstream:** `ProgressBarStyle.Marquee` sends `PBM_SETMARQUEE` and the bar animates a travelling block independently of `Value`; `Blocks` draws segmented chunks, `Continuous` a solid fill.
- **Impact:** The standard "indeterminate busy" progress bar (`Style = Marquee`, `Value` left at 0) renders as a permanently **empty** bar — the app looks hung. This is the most common non-default ProgressBar configuration in LOB apps.
- **Fix:** Branch in `ProgressBarRenderer` on `control.Style`; for `Marquee` drive a `System.Windows.Forms.Timer`-equivalent at `MarqueeAnimationSpeed` ms that advances a phase offset and invalidates, drawing a sliding block. For `Blocks`, draw segments rather than one rect.
- **Test:** With `Style = Marquee`, render twice with the animation clock advanced and assert the filled region moved.
- **Tests today:** none.

### SMP-27 — `ProgressBar.ForeColor` / `BackColor` ignored by the renderer — Cat C — P2 — High
- **Ours:** the fill colour is hard-coded to `Theme.AccentColor2` / `Theme.ForegroundDisabledColor` (`src/Majorsilence.Forms/Renderers/ProgressBarRenderer.cs:20`).
- **Upstream:** `ProgressBar.ForeColor` is the bar colour and `BackColor` the trough (the classic green/red/yellow status bars are done exactly this way).
- **Impact:** Apps that colour-code progress (red for over-budget, etc.) all render the same accent colour.
- **Fix:** Use `control.ForeColor` when it is not the default, else the theme accent.
- **Test:** Set `ForeColor = Color.Red`, render at 50%, assert the filled pixels are red.
- **Tests today:** none.

### SMP-28 — `ProgressBar.Style` shadows `Control.Style`, orphaning `ProgressBar.DefaultStyle` — Cat E — P2 — High
- **Ours:** `public new ProgressBarStyle Style` (`src/Majorsilence.Forms/ProgressBar.cs:102`) hides `Control.Style`, so `ProgressBar` cannot do what every other control does — `public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);` (cf. `src/Majorsilence.Forms/Button.cs:250`, `src/Majorsilence.Forms/TrackBar.cs:101`). The `public new static ControlStyle DefaultStyle = ... style.Border.Width = 1` at `ProgressBar.cs:29-30` is therefore dead code: the instance keeps `Control.DefaultStyle`'s borderless style.
- **Upstream:** N/A structurally, but the visual consequence is that a ProgressBar in WinForms always has a trough border.
- **Impact:** ProgressBars render without their 1px trough border, so the empty part of the bar is indistinguishable from the form background. Also any framework code that reads `Control.Style` on a `ProgressBar`-typed variable silently gets the wrong member.
- **Fix:** Rename the internal styling member or expose the `ControlStyle` under a different name for `ProgressBar` (e.g. override `CurrentStyle` to seed from the static `DefaultStyle`), so the declared `DefaultStyle` actually reaches the renderer.
- **Test:** Render a default ProgressBar and assert a 1px border ring exists.
- **Tests today:** none.

### SMP-29 — `TrackBar.Value` setter raises `Scroll` — Cat A — P1 — High
- **Ours:** the `Value` setter calls `SetValueCore(value, raiseScroll: true)` (`src/Majorsilence.Forms/TrackBar.cs:290-298`), and `SetValueCore` raises `OnScroll` then `OnValueChanged` (`TrackBar.cs:611-626`).
- **Upstream:** the `Value` setter calls only `SetTrackBarPosition(); OnValueChanged(EventArgs.Empty);` (`src/System.Windows.Forms/System/Windows/Forms/Controls/TrackBar/TrackBar.cs:603-625`). `OnScroll` is raised **only** from user interaction — the wheel handler (`TrackBar.cs:970`) and the reflected `WM_HSCROLL/WM_VSCROLL` (`TrackBar.cs:1183`).
- **Impact:** `Scroll` is the standard "the *user* moved it" signal. Apps use it to write settings, re-query, or drive a linked control while using `Value = x` to update the slider from code. With `Value` firing `Scroll`, the two feed each other — re-entrant updates, duplicate saves, and in linked-pair sliders an infinite loop.
- **Fix:** `Value`'s setter should call `SetValueCore(value, raiseScroll: false)`. Keep `raiseScroll: true` only in `ChangeValueBy`, the mouse handlers and `OnMouseWheel`.
- **Test:** Subscribe to `Scroll`, set `trackBar.Value = 5` in code, assert the handler did not run while `ValueChanged` did.
- **Tests today:** none.

### SMP-30 — `TrackBar.Value` silently snaps to a tick and always range-checks — Cat A — P2 — High
- **Ours:** `SetValueCore` applies `SnapValueToTick(value)` on *every* path including the public setter (`src/Majorsilence.Forms/TrackBar.cs:611-615`), and the setter throws whenever the value is out of range even during `BeginInit` — `ISupportInitialize.BeginInit/EndInit` are empty (`TrackBar.cs:47-48`).
- **Upstream:** `Value`'s setter stores the value verbatim (no snapping) and skips the range check while `_initializing` (`Controls/TrackBar/TrackBar.cs:612-624`). Snapping is a native behaviour of user dragging only.
- **Impact:** `tb.SnapToTicks = true; tb.TickFrequency = 5; tb.Value = 3;` leaves `tb.Value == 5` — a round-trip through a settings file quietly changes the user's stored value. And a designer/ISupportInitialize block that sets `Value` before `Maximum` throws `ArgumentOutOfRangeException` at form construction where WinForms tolerates it.
- **Fix:** Move `SnapValueToTick` out of `SetValueCore` into the mouse/keyboard paths only; add an `_initializing` flag set by `BeginInit`/`EndInit` and skip the range check (clamping in `EndInit`) while it is set.
- **Test:** `tb.SnapToTicks = true; tb.TickFrequency = 5; tb.Value = 3; Assert.Equal(3, tb.Value);`
- **Tests today:** none.

### SMP-31 — `NumericUpDown` arrow clicks step by 1, ignoring `Increment` — Cat A — P0 — High
- **Ours:** `OnMouseClick` hard-codes the step: `Value = Math.Min (Value + 1, Maximum);` / `Value = Math.Max (Value - 1, Minimum);` (`src/Majorsilence.Forms/NumericUpDown.cs:267-274`). The correct `UpButton()`/`DownButton()`, which do use `Increment` (`NumericUpDown.cs:193-218`), are never called from the mouse path.
- **Upstream:** the up/down buttons call `UpButton()`/`DownButton()`, which add/subtract `Increment` (and then `Accelerations`) — `src/System.Windows.Forms/System/Windows/Forms/Controls/UpDown/NumericUpDown.cs`, `UpButton`/`DownButton`.
- **Impact:** Any NumericUpDown with a non-1 `Increment` — currency at 0.01, quantities at 5, percentages at 0.5 — steps by 1 when the user clicks the arrows. Silent data corruption in exactly the control whose job is numeric entry. Only code that calls `UpButton()` directly behaves correctly, so the bug is invisible in unit tests that don't simulate a click.
- **Fix:** Replace the two lines in `OnMouseClick` with `UpButton();` / `DownButton();`.
- **Test:** `nud.Increment = 5; nud.Value = 0;` simulate a click in `GetIncrementArea()`; assert `Value == 5`.
- **Tests today:** none.

### SMP-32 — `NumericUpDown` has no keyboard input at all — Cat B — P0 — High
- **Ours:** there is no `OnKeyDown`, `OnKeyPress`, `OnKeyUp` or `ProcessDialogKey` in `src/Majorsilence.Forms/NumericUpDown.cs` or in any of its partials (`RemainingParity.cs:279`, `KryptonPortParity.Three.cs:52`). `InterceptArrowKeys` is a stored-only auto-property (`NumericUpDown.cs:100`), `ReadOnly` likewise (`NumericUpDown.cs:109`), `UserEdit` is doc-commented "Stub", and `Select(int,int)` is `{ }` (`NumericUpDown.cs:131`). The renderer draws `control.Value.ToString(format)` with no caret and no selection (`src/Majorsilence.Forms/Renderers/NumericUpDownRenderer.cs:24`).
- **Upstream:** `NumericUpDown` is an editable text box with spin buttons: `UpDownBase.OnTextBoxKeyDown` routes Up/Down to `UpButton`/`DownButton` when `InterceptArrowKeys`, typing edits the text, and `ValidateEditText`/`OnLostFocus` parses it into `Value` (`Controls/UpDown/UpDownBase.cs`, `Controls/UpDown/NumericUpDown.cs`).
- **Impact:** The user cannot type a number into a NumericUpDown — the only way to change it is clicking the arrows, one `1` at a time (see SMP-31). Arrow keys do nothing. On a data-entry form with a quantity or amount field this is a hard blocker. `ReadOnly = true` is also meaningless because there is no editing to block.
- **Fix:** Give the control a text-entry model (or host a `TextBox` as `UpDownBase` does): handle digits/`-`/decimal separator/Backspace in `OnKeyPress`, Up/Down in `OnKeyDown` gated on `InterceptArrowKeys` and `ReadOnly`, and parse+clamp on `Leave`/`Validating`, raising `ValueChanged` there (not per keystroke, matching upstream).
- **Test:** Focus the control, send `KeyPress('4')`, `KeyPress('2')`, then `OnLostFocus`; assert `Value == 42` and that `ValueChanged` fired once.
- **Tests today:** none.

### SMP-33 — `NumericUpDown` ignores `ThousandsSeparator`, `Hexadecimal`, `TextAlign`, `UpDownAlign`, `ForeColor` and `Font` — Cat C — P1 — High
- **Ours:** the renderer builds the string as `"F" + DecimalPlaces` (or `"F0"`) and draws it with `Theme.UIFont`, `Theme.FontSize`, `Theme.ForegroundColor`, `ContentAlignment.MiddleLeft` (`src/Majorsilence.Forms/Renderers/NumericUpDownRenderer.cs:15-24`). Button areas are always at `Width - ButtonWidth` (`NumericUpDown.cs:226-227`), so `UpDownAlign = Left` does nothing. `TextAlign` and `UpDownAlign` are doc-commented "Stub in Majorsilence.Forms" (`NumericUpDown.cs:124-128`).
- **Upstream:** `UpdateEditText` formats with `"N"`/`"F"` depending on `ThousandsSeparator`, or `ToString("X")` when `Hexadecimal`; `TextAlign` and `UpDownAlign` reposition the edit box and buttons; the hosted TextBox uses the control's `Font` and `ForeColor` (`Controls/UpDown/NumericUpDown.cs` `UpdateEditText`, `Controls/UpDown/UpDownBase.cs` `PositionControls`).
- **Impact:** A currency field shows `1234567.00` instead of `1,234,567.00`; a `Hexadecimal = true` port-number box shows decimal; right-aligned numeric columns are all left-aligned; and — most visibly — setting `Font` on a NumericUpDown has **no effect at all**, so it does not scale with the rest of the form.
- **Fix:** In the renderer use `control.Font`/`control.ForeColor`, map `TextAlign` to a `ContentAlignment`, and build the format string from `Hexadecimal`/`ThousandsSeparator`/`DecimalPlaces` as upstream's `UpdateEditText` does. Derive `GetIncrementArea`/`GetDecrementArea` from `UpDownAlign`.
- **Test:** `nud.ThousandsSeparator = true; nud.Value = 1234;` render and assert the drawn text contains a group separator; `nud.Font = new Font(..., 20)` changes the measured text height.
- **Tests today:** none.

### SMP-34 — `NumericUpDown.Value` clamps where upstream throws — Cat A — P2 — High
- **Ours:** `Value`'s setter silently clamps to `[minimum, maximum]` (`src/Majorsilence.Forms/NumericUpDown.cs:83-94`).
- **Upstream:** the setter throws `ArgumentOutOfRangeException` when the value is outside the range and the control is not initializing (`Controls/UpDown/NumericUpDown.cs`, `Value` setter).
- **Impact:** A load routine that assigns a stale value outside the configured range gets a silently different number instead of an exception, so the record is saved back with the clamped value. The clamp is arguably friendlier, but it diverges and the difference is data-visible.
- **Fix:** Throw when out of range unless a `BeginInit`-set `_initializing` flag is on (see SMP-35); `UpButton`/`DownButton` already clamp before assigning so they are unaffected.
- **Test:** `nud.Maximum = 10; Assert.Throws<ArgumentOutOfRangeException>(() => nud.Value = 20);`
- **Tests today:** none.

### SMP-35 — `NumericUpDown.BeginInit/EndInit` and `Accelerations` are inert — Cat B/C — P2 — High
- **Ours:** both `ISupportInitialize.BeginInit/EndInit` (`src/Majorsilence.Forms/NumericUpDown.cs:42-43`) and the public `BeginInit/EndInit` (`src/Majorsilence.Forms/RemainingParity.cs:286-290`) are `{ }` — four empty methods, and there is no `_initializing` flag. `Accelerations` returns a live collection (`RemainingParity.cs:282`) that nothing reads, and there is no button-hold repeat timer anywhere.
- **Upstream:** `BeginInit`/`EndInit` suppress range validation while designer code assigns properties in an arbitrary order and then re-validate in `EndInit`; `Accelerations` change the step size while the button is held (`Controls/UpDown/NumericUpDown.cs`, `UpButton` consults `Accelerations`).
- **Impact:** Designer-generated `BeginInit(); ... Value = 500; Maximum = 1000; ... EndInit();` clamps `Value` to the default `Maximum` of 100 and then never restores it (with SMP-34's fix it would throw). Holding an arrow button never accelerates and, in fact, never repeats at all.
- **Fix:** Add an `_initializing` bool set/cleared by the four methods, defer range enforcement to `EndInit`, and add a repeat timer in the button hit path that consults `Accelerations`.
- **Test:** `((ISupportInitialize)nud).BeginInit(); nud.Value = 500; nud.Maximum = 1000; ((ISupportInitialize)nud).EndInit(); Assert.Equal(500, nud.Value);`
- **Tests today:** none.

### SMP-36 — `NumericUpDown` does not derive from `UpDownBase` — Cat E — P1 — High
- **Ours:** `public partial class NumericUpDown : Control` (`src/Majorsilence.Forms/NumericUpDown.cs:9`). A separate `public abstract partial class UpDownBase : ContainerControl` exists (`src/Majorsilence.Forms/WinFormsBaseControls.cs:164`) but nothing derives from it — which is why `BorderStyle`, `ChangingText`, `OnTextBoxGotFocus/LostFocus/TextChanged` and `PreferredHeight` all had to be re-declared on `NumericUpDown` with apologetic comments (`NumericUpDown.cs:139-190`, `KryptonPortParity.Three.cs:52-62`).
- **Upstream:** `NumericUpDown : UpDownBase : ContainerControl` and `DomainUpDown : UpDownBase`.
- **Impact:** `(UpDownBase)nud` throws `InvalidCastException`; `if (c is UpDownBase u)` sweeps over a form's controls miss every spin box; third-party themers and designers that type against `UpDownBase` (Krypton does) don't see the control. Also `NumericUpDown` is not a `ContainerControl`, so it does not participate in `ActiveControl`/validation the way upstream does.
- **Fix:** Reparent `NumericUpDown` (and `DomainUpDown`) onto `UpDownBase`, moving the duplicated members up.
- **Test:** `Assert.IsAssignableFrom<UpDownBase>(new NumericUpDown());`
- **Tests today:** none.

### SMP-37 — `DomainUpDown` derives from `NumericUpDown` and renders a number, not its items — Cat E — P1 — High
- **Ours:** `public partial class DomainUpDown : NumericUpDown` (`src/Majorsilence.Forms/WinFormsCompat.cs:2475`). It adds `Items`/`SelectedIndex`/`SelectedItem` but does **not** override `OnPaint`, `UpButton` or `DownButton`. `RenderManager.GetRenderer` walks up the base chain (`src/Majorsilence.Forms/Renderers/RenderManager.cs:59-67`), so a `DomainUpDown` is painted by `NumericUpDownRenderer`, which draws `control.Value.ToString("F0")` (`src/Majorsilence.Forms/Renderers/NumericUpDownRenderer.cs:21-24`).
- **Upstream:** `DomainUpDown : UpDownBase`; `UpdateEditText` shows `Items[SelectedIndex].ToString()`, and `UpButton`/`DownButton` move `SelectedIndex` (honouring `Wrap`) — `src/System.Windows.Forms/System/Windows/Forms/Controls/UpDown/DomainUpDown.cs`.
- **Impact:** A `DomainUpDown` displays the literal text `0` regardless of its `Items`, and its arrows change an invisible numeric `Value` between 0 and 100 rather than stepping through the items. It also inherits a nonsense public surface (`Minimum`, `Maximum`, `DecimalPlaces`, `Hexadecimal`, `Increment`, `Accelerations`). The control is unusable.
- **Fix:** Reparent to `UpDownBase`, add a `DomainUpDownRenderer` (or override `OnPaint`) that draws `SelectedItem?.ToString()`, and override `UpButton`/`DownButton` to move `SelectedIndex` with `Wrap`.
- **Test:** `dud.Items.Add("Alpha"); dud.SelectedIndex = 0;` render and assert the drawn text is `Alpha`, then `dud.UpButton()` and assert `SelectedIndex` moved.
- **Tests today:** none.

### SMP-38 — `DomainUpDown.SelectedIndex` accepts out-of-range, never raises `SelectedItemChanged`; `Sorted`/`Wrap` inert — Cat A/C/D — P2 — High
- **Ours:** the setter stores any int and just recomputes `Text` (`src/Majorsilence.Forms/WinFormsCompat.cs:2490-2496`); `SelectedItemChanged` sits under `#pragma warning disable CS0067` with a "not yet raised (stub)" comment (`WinFormsCompat.cs:2513-2516`); `Sorted` and `Wrap` are auto-properties in `src/Majorsilence.Forms/TailParity.Two.cs:300-304` that nothing reads. `SelectedItem`'s setter silently does nothing when no item matches (no reset to -1).
- **Upstream:** `SelectedIndex` throws `ArgumentOutOfRangeException` outside `[-1, Items.Count)`, calls `UpdateEditText`, and raises `OnSelectedItemChanged`; `Sorted` re-sorts `Items` on assignment and on every `Add`; `Wrap` makes `UpButton` past the last item roll to the first.
- **Impact:** `SelectedIndex = 99` on a 3-item control leaves the control in a state where `SelectedItem` is `null` and no exception told the app; nothing that listens for the selection ever fires; `Sorted = true` leaves the items in insertion order.
- **Fix:** Range-check in the setter, raise `OnSelectedItemChanged`, sort `Items` when `Sorted` is set/items are added, and honour `Wrap` in the overridden `UpButton`/`DownButton` from SMP-37.
- **Test:** `Assert.Throws<ArgumentOutOfRangeException>(() => dud.SelectedIndex = 99);` and assert `SelectedItemChanged` fires on a valid assignment.
- **Tests today:** none.

### SMP-39 — `DateTimePicker` derives from `TextBox`; `Text` is free-form and never parsed back into `Value` — Cat E — P0 — High
- **Ours:** `public partial class DateTimePicker : TextBox` (`src/Majorsilence.Forms/DateTimePicker.cs:24`). `UpdateText()` writes the formatted date into the inherited `Text` (`DateTimePicker.cs:190-199`), but nothing overrides `Text`'s setter, so the user (or code) can type anything and `Value` never changes.
- **Upstream:** `public partial class DateTimePicker : Control` (`src/System.Windows.Forms/System/Windows/Forms/Controls/DateTimePicker/DateTimePicker.cs:23`), and `Text`'s setter parses: empty → `ResetValue()`, otherwise `Value = DateTime.Parse(value, CultureInfo.CurrentCulture)` (`DateTimePicker.cs:821-836`).
- **Impact:** Two failures at once. (a) `dtp.Text = "2024-01-15";` — a common way to seed a picker from a string — displays the text but leaves `Value` at today, so the app saves the wrong date. (b) Because it *is* a TextBox, the user can delete the date and type "asdf"; nothing validates, and `Value` still reads today. It also pollutes the surface with `Multiline`, `PasswordChar`, `AcceptsReturn`, `CharacterCasing`, and makes `foreach (Control c in ...) if (c is TextBox t)` sweeps pick up every date picker on the form.
- **Fix:** Reparent to `Control` and implement the segmented editor; at minimum, override `Text` to parse into `Value` and make the control read-only to free-form typing.
- **Test:** `dtp.Text = "2024-01-15"; Assert.Equal(new DateTime(2024,1,15), dtp.Value);` and `Assert.IsNotAssignableFrom<TextBox>(dtp)`.
- **Tests today:** none.

### SMP-40 — `DateTimePicker` has no drop-down calendar; `DropDown`/`CloseUp`/`FormatChanged` never raised — Cat B/D — P0 — High
- **Ours:** `OnPaint` draws a `▾` glyph in a 16px strip (`src/Majorsilence.Forms/DateTimePicker.cs:51-62`) but there is no `OnMouseDown`/hit-test for it and no popup anywhere. `DropDown` and `CloseUp` are declared under `#pragma warning disable CS0067` with the comment "raised once the popup pipeline exposes open/close notifications" (`DateTimePicker.cs:159-164`); `FormatChanged` is the same in `src/Majorsilence.Forms/TailParity.Two.cs:250-253` and the `Format` setter (`DateTimePicker.cs:81-87`) does not raise it.
- **Upstream:** clicking the button opens a `MonthCalendar` popup, raising `DropDown` then `CloseUp`, and committing the picked date through `Value`; `Format`'s setter raises `OnFormatChanged`.
- **Impact:** The drop-down arrow is painted but dead — the user has no mouse way to change the date, and (with SMP-39) no keyboard way either. A DateTimePicker in a migrated app is a read-only display of today's date.
- **Fix:** Hit-test the button strip in `OnMouseDown`, show a popup hosting a `MonthCalendar` (which itself needs SMP-42), raise `DropDown`/`CloseUp` around it, and raise `OnFormatChanged` from the `Format` and `CustomFormat` setters.
- **Test:** Simulate a click in the button rect and assert `DropDown` fired; assert `Format = Short` raises `FormatChanged`.
- **Tests today:** none.

### SMP-41 — `DateTimePicker.ShowCheckBox`/`Checked`/`ShowUpDown`/`Calendar*` colours stored only — Cat C — P1 — High
- **Ours:** `ShowUpDown` (`src/Majorsilence.Forms/DateTimePicker.cs:90`), `ShowCheckBox` and `Checked` (`DateTimePicker.cs:172-176`), `CalendarFont`, `CalendarForeColor`, `CalendarMonthBackground`, `CalendarTitleForeColor`, `CalendarTitleBackColor` (`DateTimePicker.cs:92-93,178-188`), `CalendarTrailingForeColor` and `DropDownAlign` (`src/Majorsilence.Forms/TailParity.Two.cs:227-230`) — all auto-properties, most doc-commented "Stub in Majorsilence.Forms". `OnPaint` reads none of them.
- **Upstream:** `ShowCheckBox` draws a checkbox at the left; when `Checked` is false the date text is greyed and the control is treated as "no value" (the standard nullable-date idiom); `ShowUpDown` replaces the drop-down button with a spin control.
- **Impact:** The `ShowCheckBox` + `Checked` pattern is the *only* way WinForms expresses an optional date, and it is used on virtually every "date of X (optional)" field. Here the checkbox never draws, so the user can neither clear nor set the date, and code reading `dtp.Checked` always gets `true`, so nulls are written as today's date.
- **Fix:** Render the checkbox and the spin/drop-down variants in `OnPaint`, hit-test the checkbox, and grey the text when `!Checked`.
- **Test:** `dtp.ShowCheckBox = true; dtp.Checked = false;` render and assert the date text uses the disabled foreground and a checkbox glyph is drawn.
- **Tests today:** none.

### SMP-42 — `MonthCalendar` draws no calendar and cannot be clicked — Cat B — P0 — High — **DONE (2026-09-04, W5.20c)**
- **Ours:** the class doc says "Stub in Majorsilence.Forms — renders as a simple label showing the selected date" (`src/Majorsilence.Forms/MonthCalendar.cs:8`), and `OnPaint` does exactly that: one centred line of `ToShortDateString()` (`MonthCalendar.cs:250-259`). There is no `MonthCalendarRenderer` registered in `src/Majorsilence.Forms/Renderers/RenderManager.cs:10-42`, no `OnMouseDown`/`OnKeyDown` anywhere in `MonthCalendar.cs` or `src/Majorsilence.Forms/MidSizeControlParity.cs`, and `DateSelected` is declared as `add { } remove { }` so subscriptions are discarded (`MonthCalendar.cs:181`).
- **Upstream:** a full month grid with day headers, week numbers, bolded dates, prev/next arrows, a Today link, mouse range selection and keyboard navigation, raising `DateChanged` while dragging and `DateSelected` on release (`src/System.Windows.Forms/System/Windows/Forms/Controls/MonthCalendar/MonthCalendar.cs`).
- **Impact:** A MonthCalendar on a form shows a date string in the middle of a 220x162 empty box. The user cannot select a date. Everything downstream of it (`DateSelected` handlers, `SelectionRange`) is driven only by code. Combined with SMP-40 there is no working date-picking UI in the framework at all.
- **Fix:** Implement a `MonthCalendarRenderer` drawing the grid from `FirstDayOfWeek`/`CalendarDimensions`/`ShowWeekNumbers`/`ShowToday`/bolded dates, add mouse and keyboard handling that sets the selection and raises `DateChanged` then `DateSelected`, and back `DateSelected` with a real event field.
- **Test:** Headless render and assert 7 day-header cells and the day numbers of the current month are drawn; simulate a click on a day cell and assert `SelectionStart` moved and `DateSelected` fired.
- **Tests today:** `MonthCalendarBehaviourTests.cs` (44 tests, W5.20c). The "Fix" above implies `FirstDayOfWeek` was unconsumed; it was already read by `GetDisplayRange` -- see `SMP-46`, which has it right.

### SMP-43 — `MonthCalendar.HitTest` returns `SelectionStart` for every point in the body — Cat A — P1 — High — **PARTLY DONE (2026-09-04, W5.20c)**
- **Ours:** after the title-band and today-link checks, `HitTest` returns `new HitTestInfo (point, HitArea.Date, SelectionStart)` for *any* remaining point (`src/Majorsilence.Forms/MidSizeControlParity.cs:154-175`). It never maps the point to a day cell. `HitArea.WeekNumbers`, `DayOfWeek`, `TitleYear`, `PrevMonthDate`, `NextMonthDate`, `TitleBackground` and `CalendarBackground` are declared but never returned. The file header at `MidSizeControlParity.cs:16-18` claims "HitTest and GetDisplayRange are computed from the same geometry the renderer lays the control out with, so they agree with what the user sees" — the renderer lays out nothing but a centred string.
- **Upstream:** `MonthCalendar.HitTest(Point)` sends `MCM_HITTEST` and returns the actual date under the cursor plus the precise `HitArea` (`Controls/MonthCalendar/MonthCalendar.cs`, `HitTest`).
- **Impact:** The standard "what date did the user hover/right-click?" pattern — a context menu on a calendar day, a tooltip per day — always reports the currently selected date, so the menu acts on the wrong day. Silently wrong rather than obviously broken.
- **Fix:** Once SMP-42 gives the control a real grid geometry, compute the cell from the point and return that date; return the specific `HitArea` values for headers, week numbers and adjacent-month days.
- **Test:** With a rendered grid, `HitTest` a point in the first day cell and assert `Time` is the first displayed date, not `SelectionStart`.
- **Tests today:** `MonthCalendarBehaviourTests.cs` covers `Date`, `PrevMonthDate`, `NextMonthDate`, `DayOfWeek` and `WeekNumbers`. `TitleYear` and `TitleBackground` are still never returned -- `TitleMonth` covers the whole middle of the title band -- because splitting them needs the title text measured and hit-tested run by run, and `MidSizeControlParityTests` pins `HitTest (100, 1)` on a 200px calendar as `TitleMonth`.

### SMP-44 — `MonthCalendar` bolded dates have two disagreeing backing stores — Cat A — P2 — High
- **Ours:** `BoldedDates`/`AnnuallyBoldedDates`/`MonthlyBoldedDates` are plain auto-properties on `MonthCalendar` (`src/Majorsilence.Forms/MonthCalendar.cs:159-166`), while `AddBoldedDate`/`RemoveBoldedDate`/`IsBoldedDate` operate on private `List<DateTime>` fields in the other partial (`src/Majorsilence.Forms/MidSizeControlParity.cs:24-26, 87-122`). `UpdateBoldedDates()` copies list → property one way only (`MidSizeControlParity.cs:112-118`).
- **Upstream:** `BoldedDates`'s setter replaces the internal array that the paint code and the bolding logic both read (`Controls/MonthCalendar/MonthCalendar.cs`, `BoldedDates`).
- **Impact:** `cal.BoldedDates = new[]{ d };` then `cal.IsBoldedDate(d)` returns `false`, and a subsequent `UpdateBoldedDates()` **erases** the assignment by overwriting the property from the (empty) list. Assigning the property and using the Add/Remove API in the same app silently loses data.
- **Fix:** Make the three array properties project from / write through to the three lists.
- **Test:** `cal.BoldedDates = new[]{ d }; Assert.True(cal.IsBoldedDate(d)); cal.UpdateBoldedDates(); Assert.Contains(d, cal.BoldedDates);`
- **Tests today:** none.

### SMP-45 — `MonthCalendar` range validation uses the raw min/max, not the effective ones — Cat A — P2 — High
- **Ours:** `SelectionStart`/`SelectionEnd`/`SetDate`/`SetSelectionRange`/`TodayDate` all validate against the raw `_minDate`/`_maxDate` fields, which default to `DateTime.MinValue`/`MaxValue` (`src/Majorsilence.Forms/MonthCalendar.cs:41-42, 63-64, 145-146, 189-201`), while the public `MinDate`/`MaxDate` getters clamp to 1753..9998 via `EffectiveMinDate`/`EffectiveMaxDate` (`MonthCalendar.cs:30-32, 85-101`).
- **Upstream:** the same setters validate against the effective `MinDate`/`MaxDate` and throw `ArgumentOutOfRangeException` outside 1753-01-01..9998-12-31.
- **Impact:** `cal.SelectionStart = new DateTime(1200, 1, 1);` is accepted here and throws in WinForms; a port that relied on the exception to reject bad input now stores an undisplayable date, and `MinDate` still reports 1753 so the invariant `MinDate <= SelectionStart` is violated.
- **Fix:** Use `MinDate`/`MaxDate` (the effective getters) in all five validation sites.
- **Test:** `Assert.Throws<ArgumentOutOfRangeException>(() => cal.SelectionStart = new DateTime(1200,1,1));`
- **Tests today:** none.

### SMP-46 — `MonthCalendar` display/appearance properties stored only — Cat C — P2 — High — **PARTLY DONE (2026-09-04, W5.20c)**
- **Ours:** `CalendarDimensions`, `FirstDayOfWeek`, `ShowWeekNumbers`, `ShowToday`, `ShowTodayCircle`, `TitleForeColor`, `TitleBackColor`, `TrailingForeColor` are auto-properties, six of them doc-commented "Stub in Majorsilence.Forms" (`src/Majorsilence.Forms/MonthCalendar.cs:113-176`). `SetCalendarDimensions` stores and invalidates (`src/Majorsilence.Forms/MidSizeControlParity.cs:62-77`) but nothing draws multiple months.
- **Upstream:** all of these change the rendered calendar.
- **Impact:** Follows directly from SMP-42 — listed separately because each is an independent designer-set property that a fixer will need to wire. `FirstDayOfWeek` in particular *is* consumed by `GetDisplayRange`, so the padded range is computed for a layout that is never drawn.
- **Fix:** Consume all eight in the new `MonthCalendarRenderer`.
- **Test:** `cal.ShowWeekNumbers = true` adds a leading column to the rendered grid.
- **Tests today:** `MonthCalendarBehaviourTests.cs`. `FirstDayOfWeek`, `ShowWeekNumbers`, `ShowToday`, `ShowTodayCircle`, `TitleForeColor`, `TitleBackColor` and `TrailingForeColor` are consumed by `MonthCalendarRenderer`; `ScrollChange` (from `SMP-43`'s neighbourhood, also stored-only) is consumed by the scroll arrows. `CalendarDimensions` > 1x1 is still not drawn -- one month is painted across the whole client area -- so `SetCalendarDimensions` remains cosmetic and `GetDisplayRange` still reports more months than are visible. The doc comments on `ShowToday` and `ShowTodayCircle` were also swapped, each describing the other; corrected.

### SMP-47 — `ScrollBar.Scroll` is raised only for `ThumbTrack`; arrows, track clicks, wheel and `EndScroll` never fire it — Cat D — P1 — High
- **Ours:** the only `OnScroll` call site is inside `OnMouseMove` while the thumb is held: `OnScroll (new ScrollEventArgs (ScrollEventType.ThumbTrack, Value))` (`src/Majorsilence.Forms/ScrollBar.cs:200-209`). `OnMouseDown` on the arrows/track just assigns `Value` (`ScrollBar.cs:172-197`), `OnMouseUp` only clears `thumb_pressed` (`ScrollBar.cs:211-217`), and `OnMouseWheel` calls `UpdateFromValue` directly (`ScrollBar.cs:219-229`) — none raise `Scroll`.
- **Upstream:** every user action goes through `DoScroll(ScrollEventType)` which raises `Scroll` with the specific type — `SmallIncrement`/`SmallDecrement`/`LargeIncrement`/`LargeDecrement`/`First`/`Last`/`ThumbPosition`/`ThumbTrack` — and always finishes with `EndScroll` (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollBar.cs:626-690`, wheel path at `:526-566`).
- **Impact:** Code that distinguishes scroll kinds (`if (e.Type == ScrollEventType.EndScroll) Refresh();` — the standard way to defer an expensive redraw until the drag finishes) never runs, and `ScrollEventArgs.OldValue`/`e.NewValue = x` cancellation is impossible. An app hooked to `Scroll` sees nothing at all when the user clicks an arrow.
- **Fix:** Add a private `DoScroll(ScrollEventType)` mirroring upstream: compute the new value, raise `Scroll` (respecting the handler's `e.NewValue` write-back), then set `Value`; call it from every mouse/wheel path and raise `EndScroll` on mouse-up and after wheel.
- **Test:** Click the increment arrow and assert `Scroll` fired once with `ScrollEventType.SmallIncrement`, then once with `EndScroll`.
- **Tests today:** none.

### SMP-48 — `ScrollBar` lets `Value` reach `Maximum` instead of `Maximum - LargeChange + 1` — Cat A — P1 — High
- **Ours:** the `Value` setter accepts anything in `[minimum, maximum]` (`src/Majorsilence.Forms/ScrollBar.cs:119-128`), the arrow/track handlers clamp to `Maximum` (`ScrollBar.cs:172-197`), and `UpdateFromPoint`/`UpdateFromValue` map the whole track onto `[minimum, maximum]` (`ScrollBar.cs:272-327`, via `PossibleValuesCount = maximum - minimum + 1`).
- **Upstream:** every user-driven increment is clamped to `_maximum - LargeChange + 1`; `ScrollEventType.Last` is defined as exactly that value (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollBar.cs:671-681`), and `ReflectPosition` uses the same expression (`:574-582`).
- **Impact:** With the canonical `Maximum = itemCount - 1 + LargeChange - 1`, dragging the thumb to the bottom yields a `Value` `LargeChange - 1` too large, so a custom-scrolled list/canvas scrolls a whole page past its content and shows blank space. The thumb also never reaches the end of the track at the true maximum. Silently wrong arithmetic in the one property everybody reads.
- **Fix:** Introduce `int EffectiveMaximum => Math.Max(minimum, maximum - LargeChange + 1);` and clamp the user-driven paths and the track mapping to it (leave the property setter's `ArgumentOutOfRangeException` bound at `Maximum`, as upstream does).
- **Test:** `sb.Minimum=0; sb.Maximum=100; sb.LargeChange=10;` drag the thumb to the far end and assert `Value == 91`.
- **Tests today:** none.

### SMP-49 — `ScrollBar.OnMouseWheel` scrolls 120x too far — Cat A — P1 — High
- **Ours:** `if (e.Delta != 0) UpdateFromValue (Value - (e.Delta * SmallChange));` (`src/Majorsilence.Forms/ScrollBar.cs:219-229`). `Delta` is ±120 per notch, so one wheel click moves `Value` by `120 * SmallChange`.
- **Upstream:** accumulates `_wheelDelta` and emits one `SmallDecrement`/`SmallIncrement` per whole `WHEEL_DELTA` (120), then `EndScroll` (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollBar.cs:526-559`).
- **Impact:** A single wheel notch slams the scrollbar from one end to the other on any range under 120. Every scroll wheel interaction with a standalone `HScrollBar`/`VScrollBar` is unusable.
- **Fix:** Accumulate the delta and step by `SmallChange` per 120 units, exactly as upstream.
- **Test:** `sb.SmallChange = 1;` send one `MouseEventArgs` with `Delta = -120` and assert `Value` increased by 1.
- **Tests today:** none.

### SMP-50 — `ScrollBar.Scroll` has the wrong delegate type — Cat E — P2 — High
- **Ours:** `public new event EventHandler<ScrollEventArgs>? Scroll;` (`src/Majorsilence.Forms/ScrollBar.cs:93`).
- **Upstream:** `public event ScrollEventHandler? Scroll` (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollBar.cs:450`).
- **Impact:** Designer-generated and hand-written code that writes `this.vScrollBar1.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vScrollBar1_Scroll);` — the exact form `InitializeComponent` emits — does not compile against this layer. Method-group syntax happens to work, which is why the divergence survives a name-level audit.
- **Fix:** Declare the event as `ScrollEventHandler` (the delegate already exists in this layer for `Control.Scroll`).
- **Test:** Compile-time; assert `typeof(ScrollBar).GetEvent("Scroll").EventHandlerType == typeof(ScrollEventHandler)`.
- **Tests today:** none.

### SMP-51 — `ErrorProvider` never renders anything — Cat B — P0 — High
- **Ours:** the class summary states outright "Majorsilence.Forms does not render error icons natively; the error text is stored for programmatic access and shown in the control's ToolTip text if a ToolTip is set" (`src/Majorsilence.Forms/ErrorProvider.cs:8-12`) — and even the ToolTip half is untrue: `SetError` only writes `_errors[control] = value` (`ErrorProvider.cs:111-119`) and nothing reads `_errors` except `GetError`/`HasErrors`. `Icon`, `BlinkStyle`, `BlinkRate`, `ContainerControl`, `SetIconAlignment`, `SetIconPadding`, `DataSource`, `DataMember` are all "Stub in Majorsilence.Forms" (`ErrorProvider.cs:40-171`); the alignment/padding dictionaries are written and only ever read back by their own getters.
- **Upstream:** `ErrorProvider` attaches a `ControlItem`/`ErrorWindow` per control, paints the icon beside it at `ErrorIconAlignment` + `IconPadding`, blinks per `BlinkStyle`/`BlinkRate`, and shows the error text as a tooltip on hover (`src/System.Windows.Forms/System/Windows/Forms/ErrorProvider/ErrorProvider.cs`).
- **Impact:** This is the canonical WinForms validation affordance — `errorProvider1.SetError(txtName, "Required")` in a `Validating` handler. In a migrated app the user gets **no feedback at all**: no icon, no tooltip, no blink. The form silently refuses to submit with nothing on screen explaining why. Highest-impact stub in this area.
- **Fix:** Give `ErrorProvider` a per-control adorner: on `SetError`, position a small owner-drawn overlay (or have the parent's paint pass draw the icon) at `GetIconAlignment`/`GetIconPadding` relative to the control's bounds, using `Icon` or a built-in default; hook the control's `Paint`/`LocationChanged`/`SizeChanged`/`VisibleChanged`, and register the message with the tooltip layer. Blinking can reuse the same timer approach as SMP-26.
- **Test:** `ep.SetError(txt, "Required");` then render the parent and assert non-background pixels appear immediately to the right of `txt.Bounds`.
- **Tests today:** none.

### SMP-52 — `ErrorProvider.ContainerControl` is typed `Component` and there is no `ErrorProvider(ContainerControl)` ctor — Cat E — P2 — High
- **Ours:** `public Component? ContainerControl { get; set; }` with a comment explaining that `Form` and `Control` sit on separate branches here (`src/Majorsilence.Forms/ErrorProvider.cs:75-81`); the only ctors are `()` and `(IContainer)` (`ErrorProvider.cs:30-38`).
- **Upstream:** `public ContainerControl? ContainerControl { get; set; }` plus `public ErrorProvider(ContainerControl parentControl)`, and the setter re-hosts the error windows and hooks the container's binding context.
- **Impact:** `new ErrorProvider(this)` from a `Form` — a common hand-written form — does not bind to any container overload here (`Form` is not an `IContainer`), and code assigning `ep.ContainerControl` gets no behaviour because nothing reads it. Once SMP-51 is fixed the container is where the adorners must live, so this needs settling first.
- **Fix:** Add a `ContainerControl` (or `Control`) typed overload and store the real container; consume it as the adorner host.
- **Test:** `new ErrorProvider(form)` compiles and `ep.ContainerControl` returns `form`.
- **Tests today:** none.

### SMP-53 — `ImageList.Images.SetKeyName` is an empty method — Cat B — P1 — High
- **Ours:** `public void SetKeyName (int index, string name) { }` (`src/Majorsilence.Forms/ImageCollection.cs:16-19`). Images added via the streamer or `Add(image)` get an auto-generated key (`GenerateAutoKey()`, `ImageCollection.cs:219-223`).
- **Upstream:** `ImageList.ImageCollection.SetKeyName(int, string)` renames the entry so `Images[key]`, `IndexOfKey`, `ContainsKey` and every `ImageKey` consumer find it.
- **Impact:** This is designer-emitted code. `InitializeComponent` for any image list populated from a `.resx` writes `this.imageList1.ImageStream = ...;` followed by one `this.imageList1.Images.SetKeyName(0, "save.png");` per image. Because the call does nothing, every `button.ImageKey = "save.png"` / `listViewItem.ImageKey` / `treeNode.ImageKey` in the app resolves to nothing and no icon appears — while `ImageIndex`-based code keeps working, so the failure looks random.
- **Fix:** Re-key the entry in the backing `OrderedDictionary` at `index` (remove + reinsert at the same ordinal, or switch to a parallel key list so ordering is preserved).
- **Test:** `il.Images.Add(bmp); il.Images.SetKeyName(0, "a"); Assert.Equal(0, il.Images.IndexOfKey("a"));`
- **Tests today:** `tests/Majorsilence.Forms.Tests/ImageListTests.cs` (24 facts) — none cover `SetKeyName`.

### SMP-54 — `ImageList.ImageSize` throws once images are present — Cat A — P1 — High
- **Ours:** `SetImageSize` throws `InvalidOperationException ("Cannot set ImageSize after Images are already added.")` when `_images.Count > 0` (`src/Majorsilence.Forms/ImageCollection.cs:157-166`), reached from `ImageList.ImageSize`'s setter (`src/Majorsilence.Forms/ImageList.cs:41-44`).
- **Upstream:** the setter validates the size is 1..256 in each dimension and otherwise just stores it and recreates the native handle — existing images are re-rendered at the new size, no exception (`src/System.Windows.Forms/System/Windows/Forms/Controls/ImageList/ImageList.cs:144-173`).
- **Impact:** Any code that resizes an existing image list — DPI-scaling a toolbar's icons at runtime, or designer code that assigns `ImageStream` before `ImageSize` (the streamer already populates `Images`, see `ImageList.cs:67-80`) — throws at form construction where WinForms works. Also note the type divergence: the exception is `InvalidOperationException`, not one of the upstream `ArgumentException`s, so existing catch blocks don't match either.
- **Fix:** Store the new size and rescale the existing bitmaps (or clear them, but do not throw); validate 1..256 and throw `ArgumentOutOfRangeException` as upstream does.
- **Test:** `il.Images.Add(bmp); il.ImageSize = new Size(32,32); Assert.Equal(32, il.Images[0].Width);`
- **Tests today:** `tests/Majorsilence.Forms.Tests/ImageListTests.cs`.

### SMP-55 — `ImageList.ImageCollection` is an `IDictionary<string, SKBitmap>`, not an `IList` of `Image` — Cat E — P2 — High
- **Ours:** `public class ImageCollection : IDictionary<string, SKBitmap>` (`src/Majorsilence.Forms/ImageCollection.cs:13`); the indexers return `SKBitmap` (`ImageCollection.cs:171-198`), `Keys` returns `ICollection<string>` (`ImageCollection.cs:131`), and enumeration yields `KeyValuePair<string, SKBitmap>` (`ImageCollection.cs:113`).
- **Upstream:** `ImageList.ImageCollection : IList` yielding `Image`; `Keys` is a `StringCollection`.
- **Impact:** `pictureBox1.Image = imageList1.Images[0];` and `foreach (Image img in imageList1.Images)` — both extremely common — do not compile or need a cast that migrated source doesn't have. `Images.Add(image)` happens to work, which again hides the divergence from a name-level audit.
- **Fix:** Keep the dictionary internally but expose the WinForms shape: an `IList`-implementing collection whose indexers return `Majorsilence.Forms.Drawing.Image`, with `Keys` as a string collection.
- **Test:** `Assert.IsAssignableFrom<System.Collections.IList>(new ImageList().Images);`
- **Tests today:** `tests/Majorsilence.Forms.Tests/ImageListTests.cs`.

### SMP-56 — `ImageList.ColorDepth` / `TransparentColor` stored only, `Draw` silently no-ops out of range — Cat C/A — P2 — High
- **Ours:** both auto-properties, doc-commented "Stored but not enforced" / "Stub in Majorsilence.Forms" (`src/Majorsilence.Forms/ImageList.cs:46-50`). The three `Draw` overloads guard with `if (index >= 0 && index < Images.Count)` and otherwise do nothing (`ImageList.cs:82-97`).
- **Upstream:** `TransparentColor` makes that colour transparent when images are added (the standard way a magenta-keyed toolbar bitmap strip is made see-through); `Draw` throws `ArgumentOutOfRangeException` for a bad index.
- **Impact:** Legacy bitmap strips keyed on magenta/`Color.Fuchsia` draw with the key colour visible as solid blocks behind every icon. `Draw` with a stale index paints nothing instead of surfacing the bug.
- **Fix:** Apply `TransparentColor` as an alpha mask in `ImageCollection.Add` when it is not `Color.Transparent`; throw from `Draw` on a bad index.
- **Test:** `il.TransparentColor = Color.Magenta;` add a bitmap with magenta pixels and assert those pixels have alpha 0.
- **Tests today:** `tests/Majorsilence.Forms.Tests/ImageListTests.cs`.

### SMP-57 — `WebBrowser` navigation history, title, scripting and most events are inert — Cat B/D — P1 — High
- **Ours:** `CanGoBack => false`, `CanGoForward => false`, `DocumentTitle => string.Empty` (`src/Majorsilence.Forms/WebBrowser.cs:60-66`); `GoBack`, `GoForward`, `GoHome`, `Stop`, `Print`, `ShowPrintDialog` are all `{ }` (`WebBrowser.cs:101-116`); both `InvokeScript` overloads `=> null` (`WebBrowser.cs:127-130`); and `Navigated`, `Navigating`, `CanGoBackChanged`, `CanGoForwardChanged`, `DocumentTitleChanged`, `StatusTextChanged` are declared `add { } remove { }` so subscriptions are discarded (`WebBrowser.cs:151-166`). `ReadyState` is a settable-but-never-updated `Complete`. Only `DocumentCompleted` and the non-WinForms `WebMessageReceived` are real events. `ScriptErrorsSuppressed`, `ScrollBarsEnabled`, `IsWebBrowserContextMenuEnabled`, `WebBrowserShortcutsEnabled` are stored-only.
- **Upstream:** all of these are live against the hosted browser (`src/System.Windows.Forms/System/Windows/Forms/Controls/WebBrowser/WebBrowser.cs`).
- **Impact:** A migrated help/report viewer gets a page that loads but has no Back/Forward (the buttons are wired to no-op methods and `CanGoBack` keeps them permanently disabled), no title for the window caption, and no `Navigating` hook — so the common "intercept the link and open it in the OS browser / cancel it" pattern cannot fire at all. `InvokeScript` returning `null` means JS bridges silently produce nothing.
- **Fix:** Route these through the existing `IWebViewFactory`/`WebViewHost` seam (`src/Majorsilence.Forms/WebViewHost.cs`): back the history/title/ready-state properties with host queries, back the six events with real fields raised from host callbacks, and map `InvokeScript` onto `ExecuteScriptAsync` (which already exists at `WebBrowser.cs:139`).
- **Test:** With a fake `IWebViewFactory`, `Navigate(a); Navigate(b);` then assert `CanGoBack` is true and `Navigating` fired twice.
- **Tests today:** none found for WebBrowser navigation.

### SMP-58 — `PropertyGrid` cannot edit, and `PropertyValueChanged`/`SelectedGridItemChanged`/`SelectedGridItem` are inert — Cat B/D — P1 — High
- **Ours:** the class doc says "Editing is not implemented" (`src/Majorsilence.Forms/PropertyGrid.cs:13`); `OnMouseDown` only moves a selection highlight (`PropertyGrid.cs:156-167`) and `OnPaint` draws two read-only text columns (`PropertyGrid.cs:170-231`). `PropertyValueChanged`, `SelectedGridItemChanged`, `SelectedObjectsChanged` are `add { } remove { }`; `SelectedGridItem => null`; `ExpandAllGridItems`/`CollapseAllGridItems` are `{ }`; `GridItem.Select()` is `{ }` (`PropertyGrid.cs:60-99, 276`).
- **Upstream:** a full editable grid with per-type `UITypeEditor`s, expandable sub-properties, and `PropertyValueChanged` raised on every commit (`src/System.Windows.Forms/System/Windows/Forms/Controls/PropertyGrid/PropertyGrid.cs`).
- **Impact:** Apps that use PropertyGrid as a settings/inspector surface become read-only viewers; the `PropertyValueChanged` handler that persists the edit never runs, and `SelectedGridItem` returning `null` NREs any code that reads `.Label`/`.Value` from it.
- **Fix:** At minimum add in-place text editing for `string`/numeric/`bool`/`enum` via the `TypeConverter`, commit through `PropertyDescriptor.SetValue`, and raise `PropertyValueChanged`; back `SelectedGridItem` with the row the selection index points at.
- **Test:** Select a row, set its text, commit, and assert the target object's property changed and `PropertyValueChanged` fired.
- **Tests today:** none found.

### SMP-59 — `PropertyGrid.SelectedObjects` silently keeps only the first object — Cat A — P1 — High
- **Ours:** `get => _selected_object == null ? null : new[] { _selected_object };  set => SelectedObject = value?.Length > 0 ? value[0] : null;` (`src/Majorsilence.Forms/PropertyGrid.cs:47-50`).
- **Upstream:** `SelectedObjects` holds the whole array and the grid shows the *intersection* of the objects' properties, editing all of them at once (`Controls/PropertyGrid/PropertyGrid.cs`, `SelectedObjects`).
- **Impact:** Multi-select editing — "select five shapes, change FillColor once" — silently edits only the first object, and reading `SelectedObjects` back gives an array of length 1 where the app set 5. Round-tripping the property loses data with no error.
- **Fix:** Store the array; enumerate the common browsable properties across all elements and write back to every object on commit.
- **Test:** `pg.SelectedObjects = new[]{a,b}; Assert.Equal(2, pg.SelectedObjects.Length);`
- **Tests today:** none found.

### SMP-60 — `PropertyGrid.ToolbarVisible` / `HelpVisible` / `BrowsableAttributes` and ~20 colour properties stored only — Cat C — P2 — High
- **Ours:** `ToolbarVisible`, `HelpVisible`, `CommandsVisibleIfAvailable`, `PropertySort` (partly), `HelpBackColor`, `HelpForeColor` (`src/Majorsilence.Forms/PropertyGrid.cs:66-102`) plus `BrowsableAttributes`, `CategoryForeColor`, `CategorySplitterColor`, `DisabledItemForeColor`, `HelpBorderColor`, `ViewBorderColor`, `SelectedItemWithFocus*`, `Commands*`, `LargeButtons`, `CanShowVisualStyleGlyphs` (`src/Majorsilence.Forms/MidSizeControlParity.cs:243-300`) — all auto-properties. `OnPaint` reads only `ViewBackColor`, `ViewForeColor` and `LineColor` (`PropertyGrid.cs:184-187`); the property enumeration filters on `p.IsBrowsable` and never consults `BrowsableAttributes` (`PropertyGrid.cs:113-122`).
- **Upstream:** the toolbar (sort/categorize/property-pages buttons) and the help description pane are real child areas whose visibility these toggle; `BrowsableAttributes` filters which properties are listed.
- **Impact:** The grid always shows just the two-column list — no sort toolbar, no description pane at the bottom — regardless of what the designer set, and `BrowsableAttributes = new AttributeCollection(new MyFilterAttribute())` (the documented way to show only a subset) lists everything.
- **Fix:** Reserve and draw the toolbar/help bands when visible; apply `BrowsableAttributes` in the `Where` clause at `PropertyGrid.cs:113-115`.
- **Test:** `pg.HelpVisible = false` reduces the height available to the row list by the help-pane height.
- **Tests today:** none found.

### SMP-61 — `HelpProvider.SetShowHelp` is a no-op while `GetShowHelp` returns a constant `true` — Cat A — P2 — High
- **Ours:** `public void SetShowHelp (Control ctl, bool value) { }` and `public bool GetShowHelp (Control ctl) => true;` (`src/Majorsilence.Forms/WinFormsCompat.cs:4035-4044`) — the setter writes nowhere and the getter reads nothing. `HelpNamespace` is stored-only ("Stub in Majorsilence.Forms", `WinFormsCompat.cs:4008`), and there is no `Help` static class / F1 dispatch anywhere in `src/` (`grep ShowHelp` finds only these and the dialogs' own `ShowHelp` flags).
- **Upstream:** `SetShowHelp` records per-control state and `GetShowHelp` returns it; F1 on a control with help set opens `HelpNamespace` at the control's keyword via `Help.ShowHelp`.
- **Impact:** `provider.SetShowHelp(txt, false)` then `provider.GetShowHelp(txt)` returns `true` — a write/read pair that disagrees, so logic branching on it takes the wrong path. And F1 does nothing anywhere in the app.
- **Fix:** Back `SetShowHelp`/`GetShowHelp` with a dictionary like the sibling `SetHelpString`/`SetHelpKeyword` already use (`WinFormsCompat.cs:3998-4026`); add F1 handling that raises `Control.HelpRequested` and falls back to opening `HelpNamespace`.
- **Test:** `hp.SetShowHelp(c, false); Assert.False(hp.GetShowHelp(c));`
- **Tests today:** none found.

## Low-priority / Win32-only (P3) — one line each
- `ButtonBase.UseCompatibleTextRendering` / `Label.UseCompatibleTextRendering` / `PropertyGrid.UseCompatibleTextRendering` — selects GDI vs GDI+ text; all text here goes through one Skia path, no portable meaning.
- `RadioButton.OwnerDraw` / `ButtonBase.Adapter` machinery — the WinForms adapter split exists only to choose between owner-draw and the Win32 theme engine.
- `ImageList.ColorDepth` — Skia surfaces are 32bpp; a lower depth has no representation (listed under SMP-56 for completeness only).
- `WebBrowser.EncryptionLevel` / `Document`(IE DOM) / `ObjectForScripting` — the IE-specific hosting surface; the modern web-view seam offers no equivalent.
- `MonthCalendar.ShowTodayCircle` vs `ShowToday` — both are native `MCS_*` styles; only meaningful once SMP-42 draws a grid.
- `DateTimePicker` `CustomFormat` Win32 token dialect (`ddddd`, `dddddd`, single-char `d`/`M`) differs from .NET format strings — our `UpdateText` passes the string straight to `DateTime.ToString`, so `CustomFormat = "d"` yields a short date rather than the day number. Low frequency; noted here rather than as a numbered finding.
- `NumericUpDown.UserEdit` — an internal WinForms flag exposed publicly; nothing here has a text editor to set it (subsumed by SMP-32).

## Systemic patterns
- **Renderer never reads the property.** The single most common shape: a setter stores and `Invalidate()`s, but the matching `Renderers/*.cs` has no branch for it. Hit list for one sweep: `Label.BorderStyle`/`FlatStyle`, `PictureBox.BorderStyle`/`ErrorImage`/`InitialImage`, `ProgressBar.Style`/`ForeColor`, `CheckBox`/`RadioButton` `Appearance`/`FlatStyle`/`FlatAppearance`, `ButtonBase.UseMnemonic`, `NumericUpDown` `TextAlign`/`UpDownAlign`/`ThousandsSeparator`/`Hexadecimal`/`Font`/`ForeColor`, `MonthCalendar`'s eight display properties, `PropertyGrid`'s ~20 colours. A single "does any renderer reference this member?" scan over `src/Majorsilence.Forms/Renderers/` would find them all.
- **Events declared to satisfy the compiler, then thrown away.** Two variants, both grep-able: `#pragma warning disable CS0067` (`DateTimePicker.DropDown/CloseUp`, `FormatChanged`, `DomainUpDown.SelectedItemChanged`, `ButtonBase.Command*Changed`) and, worse, `add { } remove { }` accessors which discard the subscription at the add site so even reflection cannot see it (`PictureBox.LoadCompleted/LoadProgressChanged`, `MonthCalendar.DateSelected`, `WebBrowser`'s six, `PropertyGrid`'s three). The second form should be converted to real fields first — it is indistinguishable from a working event at the call site.
- **`Scroll`-family events fired from the wrong place.** `TrackBar` raises `Scroll` from the programmatic `Value` setter (too often); `ScrollBar` raises it only from thumb-drag (too rarely) and never emits `EndScroll`. Both break the "was this the user?" test that apps rely on. Fix both by funnelling every value change through one `DoScroll(type, newValue)` and having the property setter bypass it.
- **Two backing stores that disagree.** `MonthCalendar`'s bolded dates (array properties vs private lists), `HelpProvider.SetShowHelp`/`GetShowHelp` (writes nowhere / reads a constant), `ImageCollection.SetKeyName` (writes nowhere). Any pair where the setter and getter don't touch the same field.
- **`Increment`/`SmallChange`/`Delta` arithmetic done twice, differently.** `NumericUpDown` steps by `Increment` in `UpButton()` but by literal `1` in `OnMouseClick`; `ScrollBar` multiplies by the raw 120-unit `Delta`; `ScrollBar` clamps to `Maximum` where upstream clamps to `Maximum - LargeChange + 1`. Route every mutation through the one public method (`UpButton`, `DoScroll`) rather than duplicating the maths at each call site.
- **Wrong base class, chosen for local convenience.** `DateTimePicker : TextBox`, `DomainUpDown : NumericUpDown`, `NumericUpDown : Control` (not `UpDownBase`), `ImageCollection : IDictionary<,>` (not `IList`), `ScrollBar.Scroll : EventHandler<ScrollEventArgs>` (not `ScrollEventHandler`). Each one compiles for the simple case and then diverges: an inherited member does the wrong thing, a designer-emitted delegate construction fails, or an `is`/cast sweep misses the control.
- **`DefaultCursor => Cursors.Hand` on the button family**, and no hand cursor on `LinkLabel` — the two are exactly inverted from WinForms.
- **Existing tests pin property round-trips, not behaviour.** Every control here has a test file (`ButtonTests.cs`, `ScrollBarTests.cs`, `NumericUpDownTests.cs`, ...) with 12-47 facts, but they assert defaults and getter/setter symmetry. None of the 61 findings above is covered, because none of them is visible from a property round-trip — which is the same blind spot `docs/winforms-gap-plan.md` describes.
