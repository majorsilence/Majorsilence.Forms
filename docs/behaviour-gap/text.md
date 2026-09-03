# Text editing controls — findings

## Summary
The area is a single real engine (`TextBoxDocument`, a RichTextKit-backed plain-text buffer with caret, anchor/end selection, single-level undo and a cached `TextBlock`) with `TextBox` as its only consumer; `TextBoxBase` re-derives everything from `Text`+selection, and `RichTextBox`/`MaskedTextBox` are `TextBox` subclasses that add stored-only properties. The core editing loop (typing, Backspace/Delete, arrows, Home/End, Shift-select, Ctrl+C/X/V/A, programmatic `Text`/`Select`/`SelectedText`, `TextChanged`, `Modified`, `Undo`) is genuinely implemented and mostly right. The dominant failure patterns are (1) the input pipeline ignores the app's own `KeyPress.Handled`/`KeyDown.SuppressKeyPress`, so every "digits-only" text box in a migrated LOB app stops filtering; (2) high-traffic verbs are routed through the `Text` setter (`AppendText`, `SelectedText` on `RichTextBox`), which resets caret/scroll/undo/`Modified` to the "fresh assignment" state; (3) ~15 behaviour-changing properties are stored and never read (`WordWrap`, `CharacterCasing`, `AcceptsReturn`, `AcceptsTab`, `ShortcutsEnabled`, `ScrollBars`, `HideSelection`, every `RichTextBox.Selection*`, the whole `MaskedTextBox.Mask` family); (4) two arithmetic paths throw on ordinary input (`MaxLength` shorter than existing text, caret moved into placeholder text). `RichTextBox.Rtf`/`Find`/`LoadFile` and `MaskedTextBox` return answers that look valid but are not (stale RTF, case-sensitive Find that never selects, `MaskCompleted == true` always). Count: **P0 × 4, P1 × 17, P2 × 14**, plus a P3 list. Existing tests cover the getters/setters well and in three places pin the divergent behaviour as expected (`MaskCompletedAndMaskFull_AlwaysTrue`, `Text_SetUnaffectedByMask`, `MaxLength_DefaultsToZero`).

## Status (2026-09-03, W5.14 — the RichTextBox document model)

**Closed:** TXT-04 (P0), TXT-14, TXT-15, TXT-16, TXT-17. 27 tests in `RichTextBoxDocumentTests.cs`,
22 verified to fail with their fix neutralized; 5 are labelled in-test as guards. Two existing tests
inverted: `RichTextBoxTests.Rtf_SetNullOrEmpty_EmptiesText` (which asserted the getter returned an
empty string, true only of the stored value it used to return) and the `SelectionColor` half of
`Ctor_Default`. **This file has no P0 findings left.**

**A correction to TXT-17's expectations for the getters.** The two colour properties do not share a
default. Upstream's `SelectionColor` reads `CFE_AUTOCOLOR` and answers the control's `ForeColor`,
because a real colour is what gets painted; `SelectionBackColor` reads `CFE_AUTOBACKCOLOR` and answers
`Color.Empty`, because "no background" is not the same as "a background matching the control" to
anything about to serialise the document. `Ctor_Default` asserted `Empty` for both.

**Deliberate limits, stated in the code as well as here.** Character formatting is *not* serialised
into the RTF the getter produces: that needs a colour table and per-run control words the writer does
not emit, so a save keeps the text and the paragraphs and loses the colours. `SelectionFont` carries
bold/italic/underline but not a per-run family or size, which the span type it is painted through
cannot express. And the runs follow typing, Backspace/Delete and `AppendText` -- the seams this control
owns -- but not `Undo`, `Paste` or a programmatic `SelectedText` assignment, which move text without
telling the run list; a run can therefore end up over the wrong characters after one of those. Fixing
that means the document owning the formatting, which is a larger change than these findings describe.

**Where the seams came from.** `InsertTypedCharacter` and `DeleteAtCaret` were added by W5.13 so
`MaskedTextBox` could filter input; they are exactly the hooks per-run formatting needs, so this item
added nothing to `TextBox`. The same fact defines the limit above: `Undo` and `Paste` have no seam.

**Still open in this file:** TXT-13 (Ctrl+Z unbound), TXT-20, and the paragraph/IME family listed under
low-priority, plus `RichTextBox.ScrollBars`, which keeps its `new` shadow.

## Status (2026-09-03, W5.12 — mutations off the Text setter)

**Closed:** TXT-02 (P0), TXT-35. 12 tests in `AppendTextRoutingTests.cs`, 8 verified to fail with their
fix neutralized; 4 are labelled in-test as guards.

`TextBoxDocument.ReplaceRange (start, length, value, ignoreLimits, captureUndo)` is the primitive this
file's "Systemic patterns" section asked for, and `TextBox.AppendText` is its first caller. The two
`new` shadows on `RichTextBox` (`AppendText`, `SelectedText`) are deleted.

**A correction to TXT-02's suggested test.** It says to assert `Modified` is unchanged by an append.
Upstream's `EM_REPLACESEL` sets the modify flag, so an append DOES mark the control modified; the
defect was that routing through the `Text` setter forced `Modified` false, making a dirty document look
saved. The test asserts upstream's direction instead.

**Not moved, deliberately:** `TextBox.SelectedText` still calls `InsertText` rather than
`ReplaceRange`. It was already document-based and behaves correctly; switching it over belongs with
TXT-20, which is about `SelectedText`/`Paste` ignoring `ReadOnly` and `MaxLength` as upstream does.

**Still open in this file:** TXT-04 (P0), TXT-13–TXT-17, TXT-20, and the rest of the `RichTextBox`
cluster.

## Status (2026-09-03, W5.11 — TextBox's stored-only behaviour and two crashes)

**Closed:** TXT-05, TXT-06, TXT-07, TXT-11, TXT-12, TXT-22, TXT-26. 15 tests (13 methods) in
`TextBoxStoredBehaviourTests.cs`, 12 verified to fail with their fix neutralized; 3 are labelled
in-test as guards.

**A correction to TXT-26's impact.** It says boxes designed as `ScrollBars.None` grow a scrollbar. The
reverse was true: **no** `TextBox` ever displayed one. `ScrollControl.ScrollBars` already shows and
hides both bars, and `public new ScrollBars ScrollBars { get; set; }` on `TextBox` shadowed it, so
`UpdateScrollBars` set `Enabled` on a bar nothing had made `Visible`. Deleting the shadow is the fix --
the same defect class as the `ToolStrip` `new`-shadows in TSM-01, not a missing feature.

**A deliberate deviation on TXT-11.** With `WordWrap = false`, a centre- or right-aligned multiline box
still wraps: the layout engine needs a real right edge to align against, and aligning inside
`int.MaxValue` puts every glyph off the end of the world and paints the control blank -- the same
trade-off the single-line path already carries and documents. Left-aligned text, which is what a log
viewer or a code view uses, does not wrap.

**Also in scope but left alone:** `RichTextBox.ScrollBars` has its own `new` shadow and remains
stored-only. It belongs to W5.14 with the rest of that control, and fixing it here would have meant
touching the RichTextBox document model this item does not otherwise go near.

**Still open in this file:** TXT-02 (P0), TXT-04 (P0), TXT-13–TXT-17, TXT-35, and the rest of the
`RichTextBox` cluster.

## Status (2026-09-02, W5.13 — the mask engine)

**Closed:** TXT-03 (P0), TXT-18, TXT-19. 11 tests in `MaskedTextBoxMaskEngineTests.cs`, 10 verified to
fail with their fix neutralized (the 11th is labelled in-test as a guard); both tests TXT-03 named for
inversion were inverted.

**A correction to TXT-03's fix note:** it says to override `OnKeyPress`/`HandleKeyDown`. Neither is
overridable for this purpose — `TextBox.OnKeyPress` raises the event and inserts in one method, and
`HandleKeyDown` is private — so the fix adds two virtual seams to `TextBox`
(`InsertTypedCharacter`, `DeleteAtCaret`) and overrides those instead. Any future input-filtering box
(`CharacterCasing`, TXT-05) wants the same seams.

**Still open in this file:** TXT-01, TXT-02, TXT-04–TXT-17, TXT-20 onward — including the
`RichTextBox` document model (TXT-04, P0) and `AppendText` routing through the `Text` setter (TXT-02,
P0).

## Findings

### TXT-01 — `TextBox.OnKeyPress` / `OnKeyDown` ignore the app's `Handled` / `SuppressKeyPress` — Cat A — P0 — High
- **Ours:** `OnKeyPress` calls `base.OnKeyPress (e)` (raising the app's `KeyPress` handler) and then inserts `e.Text` unconditionally — `e.Handled` is never read (`src/Majorsilence.Forms/TextBox.cs:297-312`). `OnKeyDown` does `e.Handled = HandleKeyDown (e)` *after* `base.OnKeyDown (e)`, overwriting whatever the app's `KeyDown` handler set — including `SuppressKeyPress`, whose setter only sets `Handled` (`TextBox.cs:289-294`, `KeyEventArgs.cs:55-61`). `HandleKeyDown` also runs its own editing (arrows, Delete, Enter) regardless of the app having handled the key.
- **Upstream:** `Control.ProcessKeyEventArgs` stops WM_CHAR delivery when `KeyPressEventArgs.Handled` is true, and `KeyEventArgs.SuppressKeyPress` removes the pending WM_CHAR; the native edit control only ever sees unhandled keys (`src/System.Windows.Forms/System/Windows/Forms/Control.cs` `ProcessKeyEventArgs`; the edit control itself is fed by the message loop after `OnKeyDown`/`OnKeyPress`).
- **Impact:** `if (!char.IsDigit (e.KeyChar)) e.Handled = true;` — the single most common WinForms input-validation idiom — does nothing; letters land in numeric fields. A `KeyDown` handler that eats Enter/Down/Delete to do its own thing (grid-like navigation, "Enter moves to next field") sees the text box still act on the key.
- **Fix:** In `OnKeyPress`, `if (e.Handled) return;` after `base.OnKeyPress (e)`. In `OnKeyDown`, `if (e.Handled) return;` after `base.OnKeyDown (e)` and OR the result: `e.Handled |= HandleKeyDown (e)`. Also make `WindowBase.HandleKeyDown` remember `SuppressKeyPress` so `HandleTextInput` drops the next text event (today only the "handled" return value can suppress it).
- **Test:** Headless: subscribe `KeyPress` with `e.Handled = true`, call `OnKeyPress (new KeyPressEventArgs ('7'))` via a subclass → `Text` stays empty. Subscribe `KeyDown` with `e.SuppressKeyPress = true` for Keys.Delete on "abc" with caret 0 → `Text` still "abc".
- **Tests today:** none (TextBoxTextChangedTests only checks the event fires).

### TXT-02 — `TextBoxBase.AppendText` / `TextBox.AppendText` — Cat A — P0 — High
- **Ours:** `Text += text` (`TextBoxBase.cs:197-201`, `TextBox.cs:581-587`). That runs the `Text` setter → `TextBoxDocument.Text` setter, which calls `ClearUndo ()` and `SetCursorToCharIndex (0)` (`TextBoxDocument.cs:547-567`), then `Modified = false` and `ScrollToCaret ()` — with the caret now at 0, so the view scrolls to the **top** (`TextBox.cs:689-711`). `RichTextBox.AppendText` (`new`, `RichTextBox.cs:59-63`) patches the caret afterwards but only when called through a `RichTextBox`-typed reference, and does not scroll.
- **Upstream:** selects the end of text and replaces the (empty) selection via `EM_REPLACESEL` — caret ends after the appended text, the edit control scrolls the caret into view, the undo buffer and `Modified` are untouched, existing selection colour is applied to the new text (`TextBoxBase.cs:1232-1260`, `SetSelectedTextInternal` `:992-1020`).
- **Impact:** Every log/console/output window (`txtLog.AppendText (line + Environment.NewLine)`) jumps to the top on each append and shows the oldest line; a following `ScrollToCaret ()` also goes to the top because the caret is at 0. Also O(n) re-layout of the whole text per append, `CanUndo` becomes false, `Modified` flips false.
- **Fix:** Implement on the document: `document.Deselect (); document.SetCursorToCharIndex (text.Length); document.InsertText (text)` under `suppress_undo_capture`, not clearing undo or `Modified`, then `ScrollToCaret ()`. Bypass `MaxLength` and `ReadOnly` for this path (upstream `EM_LIMITTEXT 0` + `EM_REPLACESEL` ignore both). Delete the `RichTextBox.AppendText` shadow.
- **Test:** Multiline box sized 100×40 with 50 lines; `AppendText ("x")` → `SelectionStart == TextLength`, `GetPositionFromCharIndex (TextLength - 1).Y` inside `ClientRectangle`, `CanUndo` unchanged, `Modified` unchanged.
- **Tests today:** `TextBoxTests.AppendText_*` (text only), `RichTextBoxTests.AppendText_Invoke_AppendsAndMovesSelection`, `TextBoxBaseTests.Clear_and_AppendText_work_through_the_base`.

### TXT-03 — `MaskedTextBox.Mask` / `Text` / `MaskCompleted` / `MaskFull` / `PromptChar` — Cat C+A — P0 — High
- **Ours:** `Mask` is stored (`MaskedTextBox.cs:15-18`); nothing reads it during input. `Text` is the raw typed string. `MaskCompleted => true`, `MaskFull => true` (`:30,33`). `PromptChar`, `HidePromptOnLeave`, `TextMaskFormat`, `CutCopyMaskFormat`, `AsciiOnly`, `BeepOnError`, `RejectInputOnFirstFailure`, `SkipLiterals`, `ResetOnPrompt/Space`, `AllowPromptAsInput`, `InsertKeyMode` are all stored-only (`MaskedTextBox.cs:20-83`, `MidSizeControlParity.Two.cs:265-292`). The header comment says so.
- **Upstream:** `Text` get returns `TextOutput` (the provider's formatted string, literals and prompts per `TextMaskFormat`); `Text` set runs the value through `MaskedTextProvider.Set` and raises `MaskInputRejected` on failure (`MaskedTextBox.cs:1141-1181`); `OnKeyPress` places each char via the provider (`:1950-2004`); `MaskCompleted`/`MaskFull` come from the provider (`:735-753`).
- **Impact:** Phone/SSN/date/postal-code fields accept anything; `if (!mtb.MaskCompleted) { error }` never triggers; `Text` no longer contains the literal separators the app parses (`"(555) 123-4567"` becomes `"5551234567"` or whatever was typed); the prompt underscores never appear so the field looks like a plain `TextBox`.
- **Fix:** Keep a live `System.ComponentModel.MaskedTextProvider` field (it is already constructed in the `MaskedTextProvider` getter, `:58-74`); override `OnKeyPress`/`HandleKeyDown` (Back/Delete) to `InsertAt`/`Replace`/`RemoveAt` and push `provider.ToDisplayString ()` into the document with the caret at the provider's returned position; `Text` get → `provider.ToString (includePrompt, includeLiterals)` per `TextMaskFormat`; `Text` set → `provider.Set`; `MaskCompleted/MaskFull` → provider; raise `MaskInputRejected`. When `Mask` is empty, fall through to `TextBox` (upstream `s_isNullMask`).
- **Test:** `new MaskedTextBox { Mask = "000-0000" }`; feed KeyPress '5','a','5' → `Text == "55_-____"` under `IncludePromptAndLiterals`… default `TextMaskFormat = IncludeLiterals` → `"55-"`; `MaskCompleted == false`; `MaskInputRejected` raised once with `LetterExpected`-class hint.
- **Tests today:** `MaskedTextBoxTests.Text_SetUnaffectedByMask`, `MaskCompletedAndMaskFull_AlwaysTrue` — pin the stub and will need inverting.

### TXT-04 — `RichTextBox.Rtf` getter returns a stale stored string — Cat A — P0 — High
- **Ours:** `Rtf` get returns `_rtf`, which is only written by the `Rtf` setter (`RichTextBox.cs:26-32`). Setting `Text`, typing, `AppendText`, `LoadFile`, `Clear` never update it.
- **Upstream:** `Rtf` get streams the current document out as RTF (`RichTextBox.cs:591-608`, `StreamOut (SF_RTF)`), so `rtb.Text = "Hello"; rtb.Rtf` is a full RTF document containing "Hello".
- **Impact:** The standard persistence pattern — `note.Body = rtb.Rtf` on save — stores `""` for anything the user typed (data loss) or the RTF from the last programmatic `Rtf` set (stale data overwrites edits). `rtb.Rtf = rtb.Rtf` round trips also silently drop edits.
- **Fix:** Generate RTF on read: wrap `Text` in a minimal `{\rtf1\ansi\deff0{\fonttbl{\f0 <Font.Name>;}}\fs<2*size> ...\par}` with `\`, `{`, `}` escaped, non-ASCII as `\u<n>?`, newlines as `\par`. Drop `_rtf` or invalidate it on `OnTextChanged`.
- **Test:** `rtb.Text = "a\nb"; var r = rtb.Rtf;` → starts with `{\rtf1`, contains `a\par` and `b`; `new RichTextBox { Rtf = r }.Text == "a\nb"`.
- **Tests today:** `RichTextBoxTests.Rtf_Set_GetStripsToPlainText`, `Rtf_SetNullOrEmpty_EmptiesText` (setter only).

### TXT-05 — `TextBoxDocument.InsertText` throws when existing text exceeds `MaxLength` — Cat A — P1 — High
- **Ours:** `if (text.Length + str.Length > max_length) str = str.Substring (0, max_length - text.Length);` (`TextBoxDocument.cs:287-288`). When `text.Length > max_length` the length is negative → `ArgumentOutOfRangeException` out of `OnKeyPress`.
- **Upstream:** `EM_LIMITTEXT` only limits user input and does not affect text already present (`TextBoxBase.cs:1000-1003` comment); typing at/over the limit is simply rejected.
- **Impact:** `Text = "long value from DB"` followed by `MaxLength = 10` (or set in the designer before data binding fills it — the normal order) then any keystroke crashes the app.
- **Fix:** `var room = max_length - text.Length; if (room <= 0) return false; if (str.Length > room) str = str.Substring (0, room);`.
- **Test:** `new TextBox { MaxLength = 3, Text = "abcdef" }`; `OnKeyPress ('x')` → no throw, `Text == "abcdef"`.
- **Tests today:** `TextBoxTests.MaxLength_LimitsTextLengthOnInput` (text shorter than limit only).

### TXT-06 — Caret can be moved into placeholder text and then typing throws — Cat A — P1 — High
- **Ours:** `GetTextBlock ()` lays out `DisplayText`, which is the **placeholder** when `text` is empty (`TextBoxDocument.cs:160-162, 215`). `MoveCursor` derives the new caret from that block's `CaretIndicies`/`HitTest` (`:389, 395, 406`) and `SetCursorToCharIndex` does not clamp (`:526-534`), so Right/End/Down in an empty box with `PlaceholderText = "Search…"` sets `cursor_index` to 1…7. The next `InsertText` does `text.Insert (cursor_index, str)` on an empty string → `ArgumentOutOfRangeException` (`:290`).
- **Upstream:** the placeholder is painted in `WM_PAINT` only; the edit control's text is empty and the caret cannot leave index 0 (`TextBox.cs:925-962`).
- **Impact:** Tab into an empty search box, press End (or Right/Down) out of habit, type → unhandled exception.
- **Fix:** Clamp in `SetCursorToCharIndex` (`index = Math.Clamp (index, 0, text.Length)`), and in `MoveCursor` return early when `text.Length == 0`. Longer term, lay the placeholder out in a separate block used only by the renderer.
- **Test:** `new TextBox { PlaceholderText = "Search", Width = 100 }`; `OnKeyDown (Keys.End)` then `OnKeyPress ('a')` → `Text == "a"`, no throw.
- **Tests today:** none (PlaceholderText tests are get/set only).

### TXT-07 — Losing focus destroys the selection (`OnDeselected` → `document.Deselect`), so `HideSelection` is meaningless — Cat A — P1 — High
- **Ours:** `TextBox.OnDeselected` calls `document.Deselect ()` (`TextBox.cs:273-278`), invoked from `ControlAdapter.SelectedControl` on every focus move (`ControlAdapter.cs:80`). `HideSelection` is stored and never read (`TextBoxBase.cs:78-87`); the renderer always paints whatever selection remains (`Renderers/TextBoxRenderer.cs:36`).
- **Upstream:** the selection survives focus loss; `HideSelection` (ES_NOHIDESEL absent) only controls whether it is *painted* while unfocused. `SelectionStart/Length/SelectedText` are unchanged.
- **Impact:** Any UI that acts on the text box's selection after focus moved elsewhere — an Edit menu, a toolbar `Button` (focusable here), a Find/Replace dialog, a "Insert field" button beside the box — reads `SelectionLength == 0` and inserts/replaces at the wrong place or copies nothing. Editors that set `HideSelection = false` to keep the highlight visible get nothing.
- **Fix:** Remove `document.Deselect ()` from `OnDeselected` (keep `Invalidate`). In `TextBoxRenderer.Render`, pass `TextSelection.Empty` when `!control.Selected && control.HideSelection`.
- **Test:** `Select (1, 2)` then call the protected `OnDeselected` via a subclass → `SelectionLength == 2`, `SelectedText` unchanged.
- **Tests today:** none.

### TXT-08 — `Copy`/`Cut` leak the plaintext of a password box — Cat A — P1 — Medium
- **Ours:** `TextBox.Copy`/`Cut` push `document.SelectedText` (the real text) to the clipboard regardless of `PasswordChar` (`TextBox.cs:63-73, 95-104`); Ctrl+C is bound to them (`:213-222`).
- **Upstream:** `Copy ()`/`Cut ()` are `WM_COPY`/`WM_CUT` (`TextBoxBase.cs:1291, 1316`) against an edit control created with `ES_PASSWORD` (`TextBox.cs:313`), and the Win32 edit control ignores copy/cut when in password mode.
- **Impact:** Users (or a script) can Ctrl+C a masked password field and paste it in clear; also `Ctrl+X` removes it. Security regression relative to the original app.
- **Fix:** In `Copy`/`Cut`, `if (PasswordChar != '\0' || UseSystemPasswordChar) return;` (Cut: also skip the delete, matching Win32).
- **Test:** `new TextBox { PasswordChar = '*', Text = "secret" }`, `SelectAll (); Copy ();` → `Clipboard.GetText ()` unchanged from before.
- **Tests today:** none.

### TXT-09 — `TextBox.AcceptsReturn` stored-only; `Form.AcceptButton` swallows Enter in every multiline box — Cat C — P1 — High
- **Ours:** `AcceptsReturn { get; set; }` (`TextBox.cs:595`), never read. `WindowBase.HandleKeyDown` fires `form.AcceptButton.PerformClick ()` for every Enter before routing to the focused control (`WindowBase.cs:1349-1356`).
- **Upstream:** `TextBox.IsInputKey` returns `_acceptsReturn` for Enter when `Multiline` (`TextBox.cs:517-529`), so with `AcceptsReturn = true` the key never reaches `ProcessDialogKey`/`AcceptButton` and inserts a line break.
- **Impact:** A dialog with an OK `AcceptButton` and a multiline Notes box set to `AcceptsReturn = true` (the designer default is false, but any app that has a comments box on a dialog sets it) closes the dialog on Enter instead of inserting a newline; the user cannot type a paragraph.
- **Fix:** In `WindowBase.HandleKeyDown`, before the AcceptButton branch, ask the focused control: `if (adapter.SelectedControl is TextBox { Multiline: true, AcceptsReturn: true } && (keys & (Keys.Control|Keys.Alt)) == 0) skip`. Better: add an internal `IsInputKey (Keys)` virtual on `Control` and have the dispatcher honour it, mirroring upstream.
- **Test:** Form with `AcceptButton = ok`, multiline `TextBox { AcceptsReturn = true }` focused; `HandleKeyDown (Keys.Return)` → ok not clicked, `Text == "\n"`.
- **Tests today:** none (`Enter_inserts_a_newline_in_a_multiline_box` runs without a form).

### TXT-10 — `TextBoxBase.AcceptsTab` stored-only; Tab always moves focus — Cat C — P1 — High
- **Ours:** `AcceptsTab` stores and raises its event (`TextBoxBase.cs:45-54`). `Control.RaiseKeyDown`/`RaiseKeyPress` on the adapter turn every Tab into `SelectNextControl` before the focused control sees it (`Control.cs:1821-1827, 1849-1856`). `HandleKeyDown` has no `Keys.Tab` case and `OnKeyPress` rejects `\t` as `< 32`.
- **Upstream:** `IsInputKey` returns true for Tab when `Multiline && AcceptsTab && !Ctrl` (`TextBoxBase.cs:1337-1340`), so Tab inserts `\t`; Ctrl+Tab still navigates (`:1413-1424`).
- **Impact:** Code/notes/JSON editors built on `TextBox { Multiline = true, AcceptsTab = true }` cannot indent; Tab leaves the control.
- **Fix:** In `Control.RaiseKeyDown`/`RaiseKeyPress` (adapter branch) skip the Tab shortcut when `adapter.SelectedControl is TextBoxBase { Multiline: true, AcceptsTab: true }` and no Ctrl; add `case Keys.Tab: need_refresh = document.InsertText ("\t")` to `HandleKeyDown` under the same condition.
- **Test:** Multiline `AcceptsTab` box on a form with a second control; `HandleKeyDown (Keys.Tab)` → focus stays, `Text == "\t"`.
- **Tests today:** `TextBoxBaseTests.The_state_properties_raise_their_changed_event_once` (event only).

### TXT-11 — `TextBoxBase.WordWrap` stored-only; multiline always wraps, no horizontal scroll — Cat C — P1 — High
- **Ours:** `WordWrap` auto-properties on base and `TextBox` (`TextBoxBase.cs:129`, `TextBox.cs:592`); `GetTextBlock` always bounds multiline layout to the control width (`TextBoxDocument.cs:205`); `UpdateScrollBars` has `// TODO: Horizontal scrollbar not supported` (`TextBox.cs:754`).
- **Upstream:** `WordWrap = false` creates the edit without `ES_AUTOHSCROLL` removed — lines run past the right edge and a horizontal scrollbar appears when `ScrollBars` includes Horizontal.
- **Impact:** Log viewers, code views, fixed-width report previews that rely on `WordWrap = false` for alignment wrap mid-token; the column layout of monospaced output is destroyed.
- **Fix:** Route `WordWrap` to the document: when `multiline && !WordWrap` use `new Size (int.MaxValue, int.MaxValue)`-style unbounded width (as the single-line path already does) and let `scroll_x` do the work; enable the horizontal `ScrollBar` from `MeasuredWidth`. Make `TextBox.WordWrap` a real override that invalidates the block.
- **Test:** Multiline `WordWrap = false`, width 50, `Text = "aaaa bbbb cccc dddd"` → `GetLineFromCharIndex`/`GetPositionFromCharIndex (18).Y == GetPositionFromCharIndex (0).Y`.
- **Tests today:** `TextBoxTests.WordWrap_*` (get/set only).

### TXT-12 — `TextBox.CharacterCasing` stored-only — Cat C — P1 — High
- **Ours:** `CharacterCasing { get; set; } = Normal` (`TextBox.cs:598`); no consumer.
- **Upstream:** `ES_UPPERCASE`/`ES_LOWERCASE` applied on handle creation (`TextBox.cs:215-231`, `CreateParams`), so typed, pasted and programmatically-set text is converted.
- **Impact:** Every "code" field (customer code, part number, postal code) a LOB app marked `Upper` now accepts lower case; downstream lookups are case-sensitive and fail.
- **Fix:** In `TextBoxDocument.InsertText` and the `Text` setter, apply `ToUpperInvariant`/`ToLowerInvariant` according to `textbox.CharacterCasing` (culture per upstream is the thread culture; use `CultureInfo.CurrentCulture`).
- **Test:** `new TextBox { CharacterCasing = Upper }`, `OnKeyPress ('a')` → `Text == "A"`; `Text = "abc"` → `"ABC"`.
- **Tests today:** none.

### TXT-13 — Ctrl+Z / Ctrl+Y not bound — `Undo` unreachable from the keyboard — Cat B — P1 — High
- **Ours:** `HandleKeyDown` handles C/X/V/A only (`TextBox.cs:213-232`); `Undo ()` exists and works (`:673-683`, `TextBoxDocument.cs:144-158`).
- **Upstream:** the Win32 edit control processes Ctrl+Z (`WM_UNDO`) natively; RichEdit also Ctrl+Y (redo).
- **Impact:** Users cannot undo typing anywhere; the undo implementation added in the matrix (line 77) is only reachable via an explicit menu.
- **Fix:** `case Keys.Z: if (e.Control) { Undo (); need_refresh = true; } return e.Control;` (Ctrl+Y for `RichTextBox.Redo` once it exists).
- **Test:** type "ab" via `OnKeyPress`, `OnKeyDown (Keys.Control | Keys.Z)` → `Text == ""`.
- **Tests today:** `TextBoxUndoTests` (method only).

### TXT-14 — `RichTextBox.Find` family: case-sensitive by default, never selects, `end == -1` finds nothing, options ignored — Cat A — P1 — High
- **Ours:** `Find (string)` = `Text.IndexOf (str, Ordinal)` (`RichTextBox.cs:157`); `Find (str, start, options)` ignores `options` (`:184-185`); `Find (str, start, end, options)` does `Math.Min (end, Text.Length) - start` so `end = -1` yields an empty range → always `-1` (`:188-193`); no overload moves the selection or scrolls.
- **Upstream:** `FR_MATCHCASE` only when `MatchCase` is set — default is case-insensitive; `WholeWord`/`Reverse` honoured; `end == -1` means "to end of text"; unless `NoHighlight`, the match is selected (`EM_EXSETSEL`) and scrolled into view (`EM_SCROLLCARET`) (`RichTextBox.cs:1755-1905`, esp. `:1789-1830, 1880-1895`).
- **Impact:** The standard "highlight all occurrences" loop — `while ((i = rtb.Find (word, i, RichTextBoxFinds.None)) != -1) { rtb.SelectionColor = …; i += word.Length; }` — misses differently-cased hits and, because nothing is selected, colours nothing; `Find (s, 0, -1, opts)` (the documented "whole text" form) never matches.
- **Fix:** One `FindCore (str, start, end, options)`: `end == -1 → TextLength`; `StringComparison` = `MatchCase ? Ordinal : OrdinalIgnoreCase` (upstream is culture/RichEdit, ignore-case is the behaviour that matters); `Reverse` → `LastIndexOf`; `WholeWord` → check boundaries with `char.IsLetterOrDigit`; on hit and `!NoHighlight` → `Select (pos, str.Length); ScrollToCaret ()`. Throw `ArgumentOutOfRangeException` for `start < 0 || start > TextLength`, `end < -1`, `start > end` as upstream.
- **Test:** `Text = "Hello hello"`: `Find ("HELLO") == 0` and `SelectionStart == 0, SelectionLength == 5`; `Find ("hello", 1, -1, None) == 6`; `Find ("Hello", MatchCase)` on "hello" → -1.
- **Tests today:** `RichTextBoxTests.Find_*`, `OverloadParityTests.RichTextBox_Find_over_a_character_set_honours_the_range` (index only, same-case).

### TXT-15 — `RichTextBox.StripRtf` loses paragraph breaks, hex/unicode escapes and grouped text — Cat A — P1 — High
- **Ours:** every control word is skipped (`RichTextBox.cs:262-271`), so `\par`, `\line`, `\tab` vanish instead of becoming `\n`/`\t`; `\'e9` is not a letter-run so `'e9` is emitted literally; `\u233?` is dropped; text at `depth != 1` is discarded (`:256, 275`), which removes text inside any `{...}` group (Word/WordPad/RichEdit emit `{\rtlch ...}`/`{\f1 ...}` groups round-tripped text routinely).
- **Upstream:** RichEdit parses RTF fully; `Text` returns the plain text with `\r\n` per `\par`.
- **Impact:** Loading stored RTF (from a DB written by the original app) shows all paragraphs run together on one line; accented characters become `'e9`; bold/colored runs disappear entirely. Since TXT-04 then saves this back, the damage is persisted.
- **Fix:** In the control-word branch map `par`/`line` → `'\n'`, `tab` → `'\t'`, `'xx` → `(char)hex` via the current code page, `u<n>` → `(char)n` and skip the following fallback char per `\uc`; treat `{\*` destination groups (and `fonttbl`, `colortbl`, `stylesheet`, `info`, `pict`) as skipped, but append text from all other groups regardless of depth.
- **Test:** `Rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\f0 Caf\'e9\par {\b Bold} line\par}"` → `Text == "Café\nBold line\n"` (trailing per upstream; at minimum contains the `\n` and `é`).
- **Tests today:** `RichTextBoxTests.Rtf_Set_GetStripsToPlainText` (one flat group).

### TXT-16 — `RichTextBox.LoadFile (path)` / `SaveFile (path)` default to plain text — Cat A — P1 — High
- **Ours:** optional parameter default `RichTextBoxStreamType.PlainText` and the `fileType` argument is ignored: `LoadFile` does `Text = File.ReadAllText (path)`, `SaveFile` writes `Text` (`RichTextBox.cs:66-89`).
- **Upstream:** the one-argument overloads mean `RichText` (`RichTextBox.cs:2283-2286, 2609-2612`); `LoadFile (…, RichText)` parses RTF (and throws `ArgumentException` if the file is not RTF); `SaveFile (…, RichText)` writes RTF.
- **Impact:** `rtb.LoadFile ("notes.rtf")` displays raw `{\rtf1\ansi…` markup; `rtb.SaveFile ("notes.rtf")` writes a plain-text file with an `.rtf` extension that the original WinForms app (or WordPad) then refuses/misreads.
- **Fix:** Default both to `RichText`; for `RichText`/`RichNoOleObjs` route load through the `Rtf` setter and save through the `Rtf` getter (TXT-04); `PlainText`/`UnicodePlainText` keep the current path.
- **Test:** Write `{\rtf1 Hello}` to a temp file; `LoadFile (path)` → `Text == "Hello"`; `SaveFile (path2)` → file starts with `{\rtf1`.
- **Tests today:** none.

### TXT-17 — `RichTextBox.SelectionColor` / `SelectionFont` / `SelectionBackColor` / `SelectionBold|Italic|Underline` stored-only — Cat C — P1 — High
- **Ours:** plain auto-properties (`RichTextBox.cs:92-122`); the renderer paints one colour (or the `Colorizer` spans, `TextBoxDocument.cs:212-258`).
- **Upstream:** get/set `CHARFORMAT2W` on the current selection (`RichTextBox.cs:847-865`); `AppendText` applies the current selection format to appended text (`TextBoxBase.cs:1243` comment).
- **Impact:** Coloured log output (`rtb.SelectionColor = Color.Red; rtb.AppendText ("ERROR …")`), highlighted search hits, bold headings — all render in one colour. Getters also lie: `SelectionColor` returns the last set value, not the colour under the caret.
- **Fix:** Keep a per-run style list on the `RichTextBox` (start, length, `TextSpanStyle`) updated when a `Selection*` setter runs with `SelectionLength > 0`, remembered as "pending insert style" when the selection is empty and applied by `InsertText`/`AppendText`; expose it through the existing `Colorizer` hook so `TextBoxDocument.BuildColorizedTextBlock` paints it. Getters read the run at `SelectionStart`.
- **Test:** `SelectionColor = Red; AppendText ("x"); SelectionColor = Black; AppendText ("y"); Select (0,1)` → `SelectionColor == Red`. Rendering: headless PNG pixel at `GetPositionFromCharIndex (0)` is red.
- **Tests today:** none.

### TXT-18 — `MaskedTextBox.UseSystemPasswordChar` (`new bool`) hides the working `TextBox` implementation — Cat A — P1 — High
- **Ours:** `public new bool UseSystemPasswordChar { get; set; }` (`MaskedTextBox.cs:24`) stores a bool; the base implementation that actually masks (`TextBox.cs:610-613`) is not called.
- **Upstream:** sets the provider's password char and the edit control's `EM_SETPASSWORDCHAR` (`MaskedTextBox.cs:1386-1410`).
- **Impact:** A PIN/account-number `MaskedTextBox` with `UseSystemPasswordChar = true` shows its contents in clear text.
- **Fix:** Delete the shadow (or make it `get => base.UseSystemPasswordChar; set => base.UseSystemPasswordChar = value;`) and, once TXT-03 lands, mirror into `provider.PasswordChar`.
- **Test:** `new MaskedTextBox { UseSystemPasswordChar = true, Text = "1234" }` → `PasswordChar != '\0'`; rendered text block's `DisplayText` is `"****"`-like, not "1234".
- **Tests today:** `MaskedTextBoxTests.UseSystemPasswordChar_Set_GetReturnsExpected` (get/set only, pins the shadow).

### TXT-19 — `MaskedTextBox.MaskInputRejected` / `TypeValidationCompleted` are `add { } remove { }` — Cat D — P1 — High
- **Ours:** handlers are silently discarded (`MaskedTextBox.cs:86, 89`). `ValidateText ()` (`MidSizeControlParity.Two.cs:294-306`) never raises `TypeValidationCompleted`, and nothing runs on `Validating`.
- **Upstream:** `OnValidating` → `PerformTypeValidation` → `OnTypeValidationCompleted` with `IsValidInput`/`ReturnValue`/`Message`, and `e.Cancel` from the handler cancels validation (`MaskedTextBox.cs:2123-2130, 2300-2357`). `MaskInputRejected` fires on every rejected char (`:2056`).
- **Impact:** Date/number fields validated via `TypeValidationCompleted` (the documented way to validate a `MaskedTextBox`) never run their handler; the "invalid date" message and focus trap disappear.
- **Fix:** Make both real events; override `OnValidating` to call `ValidateText`-style parsing (use the type's static `Parse (string, IFormatProvider)` like upstream `Formatter.ParseObject`, falling back to `Convert.ChangeType`) and raise `TypeValidationCompleted`, propagating `Cancel`. Raise `MaskInputRejected` from TXT-03's input path. Also raise `MaskChanged` from the `Mask` setter (today `#pragma`-disabled, `MidSizeControlParity.Two.cs:314`).
- **Test:** `ValidatingType = typeof (int)`, `Text = "x"`, subscribe, call `OnValidating` via subclass → event raised with `IsValidInput == false`; setting `e.Cancel = true` makes `CancelEventArgs.Cancel` true.
- **Tests today:** `MidSizeControlParityTests.MaskedTextBox_ValidateText_converts_or_reports_null`.

### TXT-20 — `TextBox.SelectedText` setter: honours `ReadOnly`/`MaxLength`, sets `Modified`, keeps undo — Cat A — P2 — High
- **Ours:** `set => document.InsertText (value)` (`TextBox.cs:565-568`): returns false silently when `ReadOnly` (`TextBoxDocument.cs:269`), truncates to `MaxLength` (`:287`), raises `OnDocumentTextChanged` with `setting_text == false` so `Modified` becomes true (`TextBox.cs:721-727`), and captures an undo step.
- **Upstream:** `SetSelectedTextInternal (text, clearUndo: true)`: lifts the limit (`EM_LIMITTEXT 0`), replaces via `EM_REPLACESEL` (works on read-only controls), then `EM_SETMODIFY 0` and `ClearUndo ()` (`TextBoxBase.cs:976-1020`).
- **Impact:** Read-only "display" boxes updated by `SelectedText` never change; `Modified` dirty-checks fire after programmatic inserts; text longer than `MaxLength` is silently cut.
- **Fix:** Add `TextBoxDocument.ReplaceSelection (string, bool ignoreLimits)` used by `SelectedText`, `Paste (string)` and `AppendText` that bypasses `read_only`/`max_length`, and wrap it in `setting_text = true` + `document.ClearUndo ()` for the `SelectedText` case.
- **Test:** `new TextBox { ReadOnly = true, Text = "ab" }; SelectionStart = 1; SelectedText = "X";` → `Text == "aXb"`, `Modified == false`.
- **Tests today:** `TextBoxBaseTests.SelectedText_reads_and_replaces_the_selection` (writable box).

### TXT-21 — `TextBox.SelectionStart` setter collapses the selection — Cat A — P2 — High
- **Ours:** setter clears the anchor and moves the caret (`TextBox.cs:516-531`), with a comment claiming WinForms does the same.
- **Upstream:** `Select (value, SelectionLength)` — the length is preserved (`TextBoxBase.cs:1064-1068`).
- **Impact:** Code that sets `SelectionLength` first and `SelectionStart` second (or adjusts `SelectionStart` to move an existing highlight) ends with no selection. Negative values throw upstream; ours clamps (minor).
- **Fix:** `set { var len = SelectionLength; Select (value, len); }` implemented on the document (`selection_start = start; selection_end = start + len` clamped; caret at end).
- **Test:** `Text = "abcdef"; SelectionLength = 2; SelectionStart = 3;` → `SelectedText == "de"`.
- **Tests today:** `TextBoxTests.SelectionStart_then_SelectionLength_selects_from_that_point` (only the working order).

### TXT-22 — `TextBoxBase.ShortcutsEnabled` stored-only — Cat C — P2 — High
- **Ours:** auto-property (`TextBoxBase.cs:132`); Ctrl+C/X/V/A always act (`TextBox.cs:213-232`).
- **Upstream:** `ProcessCmdKey` eats the whole shortcut list when false (`TextBoxBase.cs:178-188`) and suppresses the default context menu.
- **Impact:** Kiosk/exam-style apps that disable clipboard shortcuts on a field get them anyway.
- **Fix:** In `HandleKeyDown`, guard the C/X/V/A(/Z) cases with `if (!ShortcutsEnabled) return false;`.
- **Test:** `ShortcutsEnabled = false`, `Text = "abc"`, `OnKeyDown (Keys.Control | Keys.A)` → `SelectionLength == 0`.
- **Tests today:** none.

### TXT-23 — Ctrl+Backspace / Ctrl+Delete delete one character, not a word — Cat B — P2 — High
- **Ours:** `DeleteText (forward, wholeWord)` has `// TODO: wholeWord not implemented` and ignores the flag (`TextBoxDocument.cs:72-95`).
- **Upstream:** `ProcessCmdKey` implements Ctrl+Backspace word deletion using `GetWordBoundaryStart` (`TextBoxBase.cs:205-224`); RichEdit handles Ctrl+Delete natively.
- **Impact:** A muscle-memory editing key does the wrong thing; not a crash.
- **Fix:** When `wholeWord`, compute the boundary with the existing `TextMeasurer.FindNextSeparator (text, cursor_index, forward)` and `RemoveText` that range.
- **Test:** `Text = "foo bar"`, caret at 7, `OnKeyDown (Keys.Control | Keys.Back)` → `Text == "foo "`.
- **Tests today:** none.

### TXT-24 — No double-click word select, Shift+click extend, PageUp/PageDown — Cat B — P2 — High
- **Ours:** `TextBox` overrides `OnMouseDown/Move/Up` only (`TextBox.cs:315-371`); `OnMouseDown` always resets `selection_anchor` (no Shift check); `HandleKeyDown` has no `PageUp`/`PageDown`/`Insert` cases.
- **Upstream:** native edit behaviour: double-click selects the word, Shift+click extends from the anchor, PageUp/PageDown scroll a page (`IsInputKey` claims them, `TextBoxBase.cs:1355-1359`).
- **Impact:** Everyday editing gestures do nothing; the multiline box cannot be paged with the keyboard.
- **Fix:** Override `OnMouseDoubleClick` → select `[FindNextSeparator (…, false), FindNextSeparator (…, true))`; in `OnMouseDown` if `Control.ModifierKeys` has Shift keep the existing anchor; add PageUp/PageDown = move caret by `PaddedClientRectangle.Height` via `GetCharIndexFromPosition`.
- **Test:** `Text = "foo bar"`, `OnMouseDoubleClick` at the position of index 5 → `SelectedText == "bar"`.
- **Tests today:** none.

### TXT-25 — `ReadOnly` does not change appearance — Cat C — P2 — High
- **Ours:** `ReadOnly` flips a document flag and invalidates (`TextBox.cs:457-466`, `TextBoxDocument.cs:462-470`); the renderer/`Style.BackgroundColor` never look at it.
- **Upstream:** `BackColor` reports `SystemColors.Control` instead of `Window` when read-only and no explicit `BackColor` was set (`TextBoxBase.cs:278-298`).
- **Impact:** Read-only fields look editable; users click and type into them and think the app is broken.
- **Fix:** In `TextBox`, override `BackColor` get (or the renderer's background lookup) to return `Theme.ControlColor` when `ReadOnly && !ShouldSerializeBackColor ()`, mirroring upstream.
- **Test:** `new TextBox { ReadOnly = true }.BackColor == SystemColors.Control`, and equals `Window` after `ReadOnly = false`.
- **Tests today:** `TextBoxTests.ReadOnly_*` (state only).

### TXT-26 — `TextBox.ScrollBars` stored-only (`new`), vertical bar auto-shows — Cat C — P2 — High
- **Ours:** `public new ScrollBars ScrollBars { get; set; }` (`TextBox.cs:418`) default `None`; `UpdateScrollBars` enables the vertical bar whenever text overflows (`:752-772`) and never a horizontal one. `RichTextBox.ScrollBars` likewise stored (`RichTextBox.cs:44`).
- **Upstream:** `None` shows no bars (content still scrolls with the caret); `Vertical`/`Both` show them (disabled when not needed); `Horizontal` only meaningful with `WordWrap = false`.
- **Impact:** Boxes designed as `ScrollBars.None` grow a scrollbar; `Both`/`Horizontal` never get a horizontal bar (see TXT-11).
- **Fix:** In `UpdateScrollBars`, gate `VerticalScrollBar.Visible/Enabled` on `ScrollBars is Vertical or Both`; add the horizontal bar when `Horizontal or Both` and `!WordWrap`.
- **Test:** Multiline `ScrollBars = None` with overflowing text → `VerticalScrollBar.Enabled == false`; `Vertical` → true.
- **Tests today:** none.

### TXT-27 — `PlaceholderText` stays visible while focused — Cat A — P2 — High
- **Ours:** `DisplayText` returns the placeholder whenever `text` is empty (`TextBoxDocument.cs:160`) and the renderer paints it plus the caret (`TextBoxRenderer.cs:22-46`).
- **Upstream:** `ShouldRenderPlaceHolderText` requires `!Focused` (`TextBox.cs:958-962`).
- **Impact:** Cosmetic, but a focused empty box shows grey hint text with a caret sitting on top of it — users often think there is text to delete.
- **Fix:** In the renderer use `control.Text.Length > 0 ? Text : (control.Selected ? "" : Placeholder)`; once TXT-06 separates the placeholder block this falls out.
- **Test:** Headless render with `Selected` true and empty text → no glyph pixels in the text area.
- **Tests today:** none.

### TXT-28 — `TextBox` does not select-all on first keyboard focus — Cat B — P2 — High
- **Ours:** no `OnGotFocus`/`OnSelected` override in `TextBox`.
- **Upstream:** first `OnGotFocus` after a `Text` set selects all when no selection exists and no mouse button is down (`TextBox.cs:563-583`, `_selectionSet`).
- **Impact:** Tabbing through a data-entry form no longer highlights each field's value for overtype; users must Ctrl+A or delete manually. Very noticeable in heads-down entry apps.
- **Fix:** Track `selection_set` cleared in the `Text` setter; in `OnGotFocus` (or `OnSelected`) `if (!selection_set) { selection_set = true; if (SelectionLength == 0 && Control.MouseButtons == None) SelectAll (); }`.
- **Test:** `Text = "abc"`, call `Select ()`/`OnGotFocus` via subclass → `SelectionLength == 3`.
- **Tests today:** none.

### TXT-29 — `RichTextBox.SelectionChanged` never raised; `ContentsResized` is `add { } remove { }` — Cat D — P2 — High
- **Ours:** `SelectionChanged` declared and only raised by `OnSelectionChanged`, which nothing calls (`RichTextBox.cs:227-230`); `ContentsResized` drops handlers (`:211`). The document has the natural trigger points (`SelectionStart/End` setters, `SetCursorToCharIndex`, `GetTextBlock` rebuild).
- **Upstream:** `EN_SELCHANGE` → `OnSelectionChanged`; `EN_REQUESTRESIZE` → `OnContentsResized` with the new rectangle.
- **Impact:** Editor toolbars that update Bold/Italic state on `SelectionChanged` never update; the common auto-grow pattern (`rtb.Height = e.NewRectangle.Height` in `ContentsResized`) does nothing.
- **Fix:** Have `TextBoxDocument` call back `textbox.OnDocumentSelectionChanged ()` when `selection_start/end/cursor_index` change; `TextBox` exposes an internal virtual that `RichTextBox` overrides to raise `SelectionChanged`. Raise `ContentsResized` from `UpdateScrollBars` when `MeasuredHeight` changes.
- **Test:** subscribe, `Select (0, 1)` → raised once; `Text = "a\nb\nc"` on a `RichTextBox` → `ContentsResized` raised with `NewRectangle.Height > 0`.
- **Tests today:** none.

### TXT-30 — `RichTextBox.DetectUrls` / `LinkClicked` never fire — Cat D — P2 — Medium
- **Ours:** `DetectUrls` stored (`RichTextBox.cs:35`); `LinkClicked` only raised by an uncalled `OnLinkClicked` (`:214-217`).
- **Upstream:** `EN_LINK` notification → `OnLinkClicked (linktext, start, length)` (`RichTextBox.cs:3144, 3243`); URLs are auto-underlined/blue when `DetectUrls`.
- **Impact:** Read-only "about"/help/log panes whose links open a browser via `LinkClicked` show plain text; clicking does nothing.
- **Fix:** When `DetectUrls`, run a URL regex over `Text` and feed the matches to the `Colorizer` (blue + underline); in `OnMouseUp` with no drag, hit-test the index into a match and raise `OnLinkClicked`.
- **Test:** `Text = "see https://x.y"`, `OnMouseUp` at `GetPositionFromCharIndex (6)` → `LinkClicked` raised with `LinkText == "https://x.y"`.
- **Tests today:** `RichTextBoxTests.DetectUrls_Set_GetReturnsExpected`.

### TXT-31 — `TextBoxBase.GetLineFromCharIndex` / `GetFirstCharIndexFromLine` count logical lines, not wrapped lines — Cat A — P2 — High
- **Ours:** count `\n` in `Text` (`TextBoxBase.cs:255-286`); the remark admits it.
- **Upstream:** `EM_LINEFROMCHAR`/`EM_LINEINDEX` are *visual* lines — with `WordWrap` a long paragraph is several lines (`TextBoxBase.cs:1597, 1616-1621`).
- **Impact:** Line-number gutters and "Ln x, Col y" status bars disagree with what is on screen for wrapped text; `GetFirstCharIndexOfCurrentLine` returns the paragraph start rather than the visual line start (Home already uses the visual line, so the two disagree).
- **Fix:** In `TextBox`, override both to use `document.GetTextBlock ().Lines` (RichTextKit exposes line `Start`/`Length`); keep the base's logical version for non-document subclasses.
- **Test:** width 40, `WordWrap` on, `Text = "aaaa bbbb cccc"` → `GetLineFromCharIndex (10) >= 1`.
- **Tests today:** `TextBoxBaseTests.GetLineFromCharIndex_reports_the_logical_line` (pins logical).

### TXT-32 — `RichTextBox.ZoomFactor` stored-only — Cat C — P2 — High
- **Ours:** `ZoomFactor { get; set; } = 1.0f` (`RichTextBox.cs:139`).
- **Upstream:** `EM_SETZOOM` scales rendering; throws for values outside (1/64, 64).
- **Impact:** Ctrl+wheel zoom / "Zoom" menu in viewers does nothing.
- **Fix:** Multiply `CurrentFontSize` by `ZoomFactor` in `TextBoxDocument.GetTextBlock` (via a `textbox.ZoomFactor` internal virtual returning 1 on `TextBox`) and invalidate the block on set.
- **Test:** `ZoomFactor = 2` → `PreferredHeight`/`GetTextBlock ().MeasuredHeight` roughly doubles.
- **Tests today:** none.

### TXT-33 — `MaskedTextBox.ValidateText` uses `Convert.ChangeType`, no `MaskCompleted` gate — Cat A — P2 — Medium
- **Ours:** `Convert.ChangeType (Text, ValidatingType, FormatProvider ?? CurrentCulture)`; returns null on failure (`MidSizeControlParity.Two.cs:294-306`).
- **Upstream:** returns null with an "incomplete mask" message when `!MaskCompleted`; otherwise parses `provider.ToString (false, IncludeLiterals)` (prompts removed) through the type's `Parse`/`TypeConverter` (`MaskedTextBox.cs:2300-2357`).
- **Impact:** Types without `IConvertible` (e.g. `Guid`, `TimeSpan`, custom structs with `Parse`) always report null; once TXT-03 lands, prompt characters would be fed into the parser.
- **Fix:** Try `ValidatingType.GetMethod ("Parse", [typeof (string), typeof (IFormatProvider)])` then `Parse (string)` then `TypeDescriptor.GetConverter`, falling back to `Convert.ChangeType`; strip prompts first.
- **Test:** `ValidatingType = typeof (Guid)`, `Text = Guid.Empty.ToString ()` → non-null.
- **Tests today:** `MidSizeControlParityTests.MaskedTextBox_ValidateText_converts_or_reports_null`.

### TXT-34 — `TextBox.UseSystemPasswordChar = false` erases an explicit `PasswordChar` — Cat A — P2 — High
- **Ours:** setter `else if (!value) PasswordCharacter = null;` (`TextBox.cs:610-613`); getter is `PasswordCharacter.HasValue`, so `PasswordChar = '*'` alone makes `UseSystemPasswordChar` read true.
- **Upstream:** two independent flags; `UseSystemPasswordChar` wins while true, and turning it off restores `_passwordChar` (`TextBox.cs:329-359, 466-487`).
- **Impact:** Designer emits `UseSystemPasswordChar = false` after `PasswordChar = '*'` in some orderings → field unmasked.
- **Fix:** Store a separate `use_system_password_char` bool; `document.PasswordCharacter = use_system ? '●' : (password_char == '\0' ? null : password_char)`.
- **Test:** `PasswordChar = '*'; UseSystemPasswordChar = false;` → `PasswordChar == '*'`.
- **Tests today:** `TextBoxTests.PasswordChar_*` (no interaction test).

### TXT-35 — `RichTextBox.SelectedText` (`new`) routes through `Text =` — Cat A — P2 — High
- **Ours:** rebuilds the whole string and assigns `Text` (`RichTextBox.cs:125-135`) → scroll to top, undo cleared, `Modified = false`, `TextChanged` raised for the whole document; when called through a `TextBoxBase`/`TextBox` reference the base implementation runs instead, so behaviour depends on the static type.
- **Upstream:** same `SetSelectedTextInternal` as `TextBox` (`TextBoxBase.cs:983-986`).
- **Impact:** Editors replacing a selection in a long document jump to the top each time; two different behaviours for the same object.
- **Fix:** Delete the shadow; the inherited `TextBox.SelectedText` (after TXT-20) is correct.
- **Test:** `RichTextBox` with 100 lines, `Select (TextLength - 1, 1); SelectedText = "x";` → `SelectionStart == TextLength`, scroll position unchanged.
- **Tests today:** `RichTextBoxTests.SelectionLength_SetWithSelectionStart_*`.

## Low-priority / Win32-only (P3) — one line each
- `TextBox.MaxLength` default reads 0 (unlimited) vs upstream 32767, and negative values map to unlimited instead of throwing — pinned by `TextBoxTests.MaxLength_DefaultsToZero`; harmless unless an app reads it back.
- Enter inserts `"\n"` where the Win32 edit control inserts `"\r\n"`, and `Lines` set joins with `"\n"` (upstream `Environment.NewLine`) — platform-consistent on macOS/Linux; only `Text.Contains ("\r\n")` style code notices.
- `TextBoxBase.DeselectAll` / `TextBox.SelectionLength = 0` leave the caret where it was (upstream collapses to `SelectionStart`); `SelectAll` leaves the caret at its old index (upstream puts it at the end) — invisible until the next arrow key.
- `TextBox.Paste (string)` honours `ReadOnly`/`MaxLength`; upstream's `SetSelectedTextInternal (text, false)` ignores both — same root as TXT-20.
- `PasswordChar` is applied to multiline boxes; Win32 ignores `ES_PASSWORD` with `ES_MULTILINE`.
- Caret does not blink (`TextBoxRenderer.cs:43-46` draws it solid) — cosmetic, needs a timer.
- `MaskedTextBox.Multiline` is settable (upstream forces false); `AcceptsTab`, `Lines`, `WordWrap` are not hidden — surface-shape only.
- `MaskedTextBox.IsOverwriteModeChanged`, `InsertKeyMode.Default` tracking of the Insert key — keyboard-state dependent, no trigger here yet.
- `RichTextBox.SelectionProtected` / `Protected` event, `ImeChange`, `LanguageOption`, `RichTextShortcutsEnabled`, `ShowSelectionMargin`, `RightMargin`, `SelectionTabs`, `SelectionCharOffset`, `SelectionHangingIndent`, `SelectionRightIndent`, `BulletIndent`, `SelectionIndent`, `SelectionAlignment`, `SelectionBullet`, `EnableAutoDragDrop` — paragraph/IME features of the RichEdit engine; stored-only and honestly documented, no plain-text equivalent.
- `RichTextBox.SelectedRtf` returns plain text — follows from TXT-04/TXT-17; fix together.
- `RichTextBox.Redo`/`CanRedo`/`RedoActionName`/`UndoActionName` — honest no-op (in `NoOpStubBaseline.txt`); `UndoActionName` could return "Typing"/"Delete" from `TextBoxDocument.last_edit` cheaply.
- `TextBox.AutoCompleteMode` / `AutoCompleteSource` / `AutoCompleteCustomSource` stored-only (`TextBox.cs:601-607`) — the Windows autocomplete dropdown is shell-provided (`SHAutoComplete`); a portable list popup is possible but a new feature rather than a wiring fix.
- `TextBoxBase.GetCharIndexFromPosition`/`GetPositionFromCharIndex` base defaults return 0/`Point.Empty` — only reachable from a non-`TextBox` subclass, none exist.
- `TextBox.PreferredHeight` measures "Wg" + padding + 4 vs upstream `FontHeight + border*4+3`; a pixel or two apart, layout-only.
- `TextBoxBase.Modified` getter is a plain field; upstream re-reads `EM_GETMODIFY` and may raise `ModifiedChanged` lazily from the getter — ours raises eagerly from `OnDocumentTextChanged`, arguably better.

## Systemic patterns
- **`Text` setter used as the mutation primitive.** `AppendText`, `RichTextBox.AppendText`, `RichTextBox.SelectedText`, `TextBoxBase.SelectedText`/`Cut`/`Paste` (base) all assign `Text`, which by design resets caret to 0, clears undo, clears `Modified` and scrolls to the top (TXT-02, TXT-35, and the P3 items). Fix once: give `TextBoxDocument` a `ReplaceRange (start, length, text, ignoreLimits, captureUndo)` and make every verb call it.
- **`e.Handled` is overwritten rather than combined** in `OnKeyDown`, and never read in `OnKeyPress` (TXT-01). The dispatcher (`WindowBase.HandleKeyDown`, `Control.RaiseKeyDown`) also decides Tab/Enter before the focused control can claim them (TXT-09, TXT-10). An `IsInputKey`-style virtual on `Control`, consulted by the dispatcher, would fix all three the way upstream does.
- **Stored-only behaviour properties on `TextBox`/`TextBoxBase`:** `WordWrap`, `CharacterCasing`, `AcceptsReturn`, `AcceptsTab`, `ShortcutsEnabled`, `ScrollBars`, `HideSelection`, `AutoComplete*` (TXT-09–12, 22, 26, 07). Each has one obvious consumer (`TextBoxDocument.GetTextBlock`/`InsertText`, `HandleKeyDown`, `UpdateScrollBars`, the renderer) — a single sweep through those four sites wires all of them.
- **`new` shadows that hide a working base implementation** (`MaskedTextBox.UseSystemPasswordChar`, `RichTextBox.AppendText`, `RichTextBox.SelectedText`, `RichTextBox.SelectionStart/Length/ReadOnly`, `TextLength`) produce static-type-dependent behaviour. Delete the shadows that only forward; the parity scanner cannot see the difference.
- **Index arithmetic without clamping** in `TextBoxDocument` (`InsertText` vs `max_length`, `SetCursorToCharIndex`, `MoveCursor` over the placeholder block) turns ordinary input into exceptions (TXT-05, TXT-06). Clamp at the two mutators (`SetCursorToCharIndex`, `InsertText`) and both go away.
- **Placeholder shares the layout block with real text** (`DisplayText`), so caret, hit-testing and focus rendering all see hint text as content (TXT-06, TXT-27).
- **Documented stubs whose *getters* return confident wrong answers** (`MaskCompleted => true`, `Rtf` stale, `Find` case-sensitive/-1, `SelectionColor` last-set). Tests pin three of them; when fixing, invert `MaskCompletedAndMaskFull_AlwaysTrue`, `Text_SetUnaffectedByMask`, and extend `Find_*` with a case-insensitive row.
- **Events declared but never raised where the trigger exists**: `RichTextBox.SelectionChanged` (document selection setters), `ContentsResized` (`UpdateScrollBars`), `LinkClicked` (mouse-up hit test), `MaskedTextBox.MaskChanged` (its own setter), `MaskInputRejected`/`TypeValidationCompleted` (`add { } remove { }` — these two silently discard the handler, worse than never firing).
