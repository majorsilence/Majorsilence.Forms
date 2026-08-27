# DataGridView family — findings

Paths below are relative to the two repos: ours = `/Users/petergill/Projects/Majorsilence.Forms`, upstream = `/Users/petergill/Projects/winforms`. Upstream DGV sources live under `src/System.Windows.Forms/System/Windows/Forms/Controls/DataGridView/` (abbreviated `…/DataGridView/` below).

## Summary

The grid is a working single-selection, text-editing, row-highlighting table with a solid paint pipeline (CellFormatting/CellPainting/RowPre/PostPaint/CellParsing/RowValidating are real). Almost everything *around* that core is shape-only: the editing lifecycle is a fixed TextBox with no dirty state, no type conversion and no DataError; the data-binding layer re-runs the whole bind (columns included) on every `ListChanged`; selection is a single `(row, col)` pair so `MultiSelect`, `row.Selected`, `SelectedCells`, `SelectedColumns` and `ClearSelection` all mean something different from WinForms; sorting never records `SortedColumn`/`SortOrder`; sizing (`AutoSizeColumnsMode.Fill`, `AutoResize*`, `RowTemplate`) only invalidates; and the renderer reads its own non-WinForms properties (`column.DefaultCellStyleAlignment`, `column.SortOrder`, `column.Sortable`, `SelectedRowIndex`) instead of the WinForms ones (`DefaultCellStyle.Alignment`, `HeaderCell.SortGlyphDirection`, `SortMode`, `row.Selected`), so the WinForms setters store values nothing consumes. Two dominant root causes: (1) no-op/`add { } remove { }` events with natural trigger points already present in `OnMouseDown`/`OnKeyUp`/`EndEdit`; (2) the row/column/cell objects are passive data holders (auto-properties) rather than participants — `Cell.Value`, `Row.Visible`, `Row.Selected`, `Column.DisplayIndex` change nothing. Count: **5 × P0, 21 × P1, 15 × P2** (41 findings), plus a P3 list. Several existing tests codify the divergent behaviour (noted per finding).

## Findings

### DGV-01 — `DataGridView.BeginEdit(bool selectAll)` — Cat B — P0 — High
- **Ours:** `public bool BeginEdit (bool selectAll) { return true; }` — returns success, edits nothing (`src/Majorsilence.Forms/DataGridView.cs:116`). Only the non-WinForms `BeginEdit(int rowIndex, int columnIndex)` (line 121) does work.
- **Upstream:** `BeginEdit(bool selectAll)` begins editing the current cell via `BeginEditInternal` (`…/DataGridView/DataGridView.Methods.cs:2564`, `:2568`).
- **Impact:** The only public WinForms way to start an edit from code (`dgv.BeginEdit(true)` in CellClick/CellEnter handlers, "edit on single click" idioms, toolbar "Edit" buttons) is a silent no-op that reports success.
- **Fix:** `BeginEdit(bool)` → if `selected_row_index/selected_column_index` valid call the existing `BeginEdit(row, col)` then `if (!selectAll) editor.Select(len,0)`; return `IsCurrentCellInEditMode`.
- **Test:** Set `CurrentCell`, call `BeginEdit(true)`, assert `IsCurrentCellInEditMode` and `CellBeginEdit` raised once.
- **Tests today:** none (DataGridViewBeginEditReentrancyTests only uses the (row,col) overload).

### DGV-02 — `DataGridViewCell.Value` setter (programmatic set) — Cat D/A — P0 — High
- **Ours:** stores and `Invalidate()`s; never raises `CellValueChanged`, never writes to the bound item (`src/Majorsilence.Forms/DataGridViewCell.cs:176-184`). The only `OnCellValueChanged` call sites are `EndEdit` (DataGridView.cs:1032) and the check-box click (2280).
- **Upstream:** setter → `SetValue` → pushes to `DataConnection.PushValue` when bound, `OnCellValuePushed` in VirtualMode, then `RaiseCellValueChanged` on any real change (`…/DataGridView/DataGridViewCell.cs:688-692`, `:3877-3937`).
- **Impact:** "recalculate total in CellValueChanged" never runs when code sets `Rows[i].Cells["Qty"].Value = 5`; on a bound grid the value shows but the object is not updated and the next `ListChanged` rebind silently reverts it.
- **Fix:** In the setter, after storing: if `owner?.DataGridView` is bound and `RowIndex` maps to a data item, write through the same descriptor path `EndEdit` uses (factor it into `PushValueToBoundItem(row, col, value)`); then `DataGridView.OnCellValueChanged(new DataGridViewCellEventArgs(ColumnIndex, RowIndex))` when the cell is attached.
- **Test:** Subscribe `CellValueChanged`, set `grid[0,0].Value = "x"`, assert one raise; bind `List<Item>`, set a cell, assert `items[0].Prop` updated.
- **Tests today:** none.

### DGV-03 — `DataGridViewRowCollection.Add()` / `Add(int count)` return value — Cat A — P0 — High
- **Ours:** `Add(int count)` returns `Count` (`src/Majorsilence.Forms/DataGridViewRowCollection.cs:61-68`); `Add() => Add(1)` (`src/Majorsilence.Forms/OverloadParity.Final.cs:100`), so `Add()` on an empty grid returns 1.
- **Upstream:** `Add()` returns the new row's index (`…/DataGridView/DataGridViewRowCollection.cs:175-200`); `Add(int count)` returns `insertionIndex + count - 1`, the index of the *last* row added (`:374`).
- **Impact:** The canonical unbound idiom `int i = dgv.Rows.Add(); dgv.Rows[i].Cells[0].Value = …;` throws `ArgumentOutOfRangeException` on every call.
- **Fix:** `Add(int count)` → `return Count - 1;` (and guard `count <= 0`).
- **Test:** `Assert.Equal(0, grid.Rows.Add()); Assert.Equal(2, grid.Rows.Add(2));`
- **Tests today:** `DataGridViewTests.Rows_AddCount_AddsEmptyRows` asserts only `Count`.

### DGV-04 — `Rows.Add(params object[])`, `Rows.Add(params string[])`, `Columns.Add(string,string)`, `DataBindingComplete` — Cat E — P1 — High
- **Ours:** `Rows.Add(params object[])` and a non-WinForms `Add(params string[])` return `DataGridViewRow` (`DataGridViewRowCollection.cs:33-56`); `Columns.Add(string name, string headerText)` returns `DataGridViewColumn` (`DataGridViewColumnCollection.cs:107-113`); `DataBindingComplete` is `EventHandler<EventArgs>` and the `OnDataBindingComplete(DataGridViewBindingCompleteEventArgs)` hook is never called (`DataGridView.cs:239-241`, `KryptonPortParity.cs:199`).
- **Upstream:** `int Add(params object[] values)` (`…/DataGridViewRowCollection.cs:272`); `int Add(string? columnName, string? headerText)`; `DataGridViewBindingCompleteEventHandler DataBindingComplete`.
- **Impact:** Compile-time, not silent, but ubiquitous: `int r = dgv.Rows.Add("a", 1);`, `int c = dgv.Columns.Add("Id", "ID");` and designer-generated `void grid_DataBindingComplete(object s, DataGridViewBindingCompleteEventArgs e)` all fail to compile. (Listed because the name-level scanner reports 0 gaps here.)
- **Fix:** Return `int` (index) from those `Add` overloads; drop the `string[]` overload (object[] covers it); retype `DataBindingComplete` and raise via `OnDataBindingComplete(new DataGridViewBindingCompleteEventArgs(ListChangedType.Reset))`.
- **Test:** `int i = grid.Rows.Add("a"); Assert.Equal(0, i);`
- **Tests today:** none on return types.

### DGV-05 — New-row placeholder: `Rows.Count`, `NewRowIndex`, `DataGridViewRow.IsNewRow`, `AllowUserToAddRows` — Cat A — P1 — High
- **Ours:** No placeholder row exists; `NewRowIndex => AllowUserToAddRows ? Rows.Count : -1` (`DataGridView.cs:2933`), `IsNewRow => false` (`DataGridViewRow.cs:76`), `AllowUserToAddRows` stored-only (655-668). `DefaultValuesNeeded`/`UserAddedRow`/`NewRowNeeded` are `add { } remove { }` (304, 416, 407).
- **Upstream:** With `AllowUserToAddRows`, `AddNewRow` appends a `RowTemplateClone` and `NewRowIndex = Rows.Count - 1`, so `Rows.Count` includes it and `Rows[NewRowIndex].IsNewRow` is true (`…/DataGridView.Methods.cs:71-80`, `:5472-5476`).
- **Impact:** Ported loops `for (i = 0; i < dgv.Rows.Count - 1; i++)` skip the last real row; `dgv.Rows[dgv.NewRowIndex]` throws; `if (row.IsNewRow) continue;` is harmless. Users cannot add rows by typing into the grid.
- **Fix:** Implement the placeholder: when `AllowUserToAddRows && !ReadOnly && (unbound || bound IBindingList.AllowNew)`, keep a trailing `RowTemplate.Clone()` with `IsNewRow = true` excluded from binding write-back; raise `DefaultValuesNeeded` when it becomes current, `UserAddedRow`/`RowsAdded` when editing commits into it, then append a fresh placeholder. `NewRowIndex` = its index.
- **Test:** `AllowUserToAddRows = true; Rows.Add("a");` → `Rows.Count == 2`, `Rows[1].IsNewRow`, `NewRowIndex == 1`.
- **Tests today:** `DataGridViewTests.NewRowIndex_ReflectsAllowUserToAddRows` (line 183) codifies the divergent `NewRowIndex == Rows.Count`.

### DGV-06 — `EditMode` / keystroke editing / click-on-current-cell / `EditingControl` — Cat C — P1 — High
- **Ours:** `EditMode` stored-only (`DataGridView.cs:780-792`). Editing starts only on F2 (`OnKeyUp` 2415) and double-click (`OnDoubleClick` 2113) regardless of mode (so `EditProgrammatically` still edits); typing a character does nothing; `EditOnEnter` never auto-edits; Tab inside the editor always begins editing the next cell (1063-1073). `EditingControl => null`, `EditingPanel => null` (3136-3139); `EditingControlShowing` receives a fresh empty `DataGridViewCellStyle` (176).
- **Upstream:** `ProcessKeyEventArgs` → `cell.KeyEntersEditMode` → `BeginEditInternal` (`…/DataGridView.Methods.cs:21886-21900`); a left-click on the *current* text cell begins editing unless `EditProgrammatically` (`…/DataGridViewTextBoxCell.cs:575-585`); `EditOnEnter` begins editing in `SetCurrentCellAddressCore` (`:4113`, `:12831`); Tab only moves (`:24339-24368`); `EditingControl` is the live control.
- **Impact:** Users must know to press F2/double-click; `EditMode = EditProgrammatically` grids (read-mostly with custom editors) still open the text box; `grid.EditingControl as TextBox` is always null so `EditingControl.KeyPress += …` idioms NRE.
- **Fix:** Handle `OnKeyDown`/`OnKeyPress`: if `!ReadOnly && cell.KeyEntersEditMode(e) && EditMode is EditOnKeystroke/EditOnKeystrokeOrF2` → `BeginEdit(false)` and seed the editor with the typed char; F2 only for `EditOnF2/EditOnKeystrokeOrF2`; nothing for `EditProgrammatically`; in `SelectedRowIndex/SelectedColumnIndex` setters begin edit when `EditOnEnter`; in `OnMouseDown` begin edit when the clicked cell is already current; `EditingControl => edit_textbox`; pass `cell.InheritedStyle` to `EditingControlShowing`.
- **Test:** `EditMode = EditProgrammatically; OnKeyUp(F2)` → not editing; `EditMode = EditOnKeystrokeOrF2; OnKeyDown('A')` → editing with text "A".
- **Tests today:** `DataGridViewTests.EditMode_Set_GetReturnsExpected` (store/read only); `DataGridViewParityTests.KeyEntersEditMode_*` (cell method only).

### DGV-07 — `BeginEdit` ignores column/row/cell `ReadOnly` — Cat A — P1 — High
- **Ours:** `BeginEdit(row,col)` checks only the grid's `read_only` (`DataGridView.cs:123`); `OnDoubleClick` same (2105). `Column.ReadOnly` (`DataGridViewColumn.cs:92`) and `Cell.ReadOnly` (`DataGridViewCell.cs:197`) are consulted only by `InheritedState` and the check-box toggle.
- **Upstream:** `BeginEditInternal` refuses when `IsSharedCellReadOnly(cell) || !ColumnEditable(col)` (`…/DataGridView.Methods.cs:2585`, `:2618`).
- **Impact:** `Columns["Id"].ReadOnly = true` (every LOB grid) is editable by F2/double-click and the edit is written back to the bound object.
- **Fix:** At the top of `BeginEdit(row,col)`: `if (Rows[row].Cells[col].InheritedState.HasFlag(ReadOnly)) return;`.
- **Test:** `Columns[0].ReadOnly = true; BeginEdit(0,0);` → `IsCurrentCellInEditMode == false`, `CellBeginEdit` not raised.
- **Tests today:** none.

### DGV-08 — `IsCurrentCellDirty`, `IsCurrentRowDirty`, `CurrentCellDirtyStateChanged`, `NotifyCurrentCellDirty` — Cat A/D — P1 — High
- **Ours:** `IsCurrentCellDirty => edit_textbox is not null` (`DataGridView.cs:925`) — true the instant editing starts, before any change; `CurrentCellDirtyStateChanged` never raised (928-929); `NotifyCurrentCellDirty` empty (3016, in NoOpStubBaseline); `IsCurrentRowDirty => IsCurrentCellDirty` (`DataGridViewParity.cs:440`).
- **Upstream:** a real flag set by editing-control changes and `NotifyCurrentCellDirty`, raising `OnCurrentCellDirtyStateChanged` on transition (`…/DataGridView.cs:2856-2867`, `…/DataGridView.Methods.cs:10513-10520`).
- **Impact:** The canonical check-box commit idiom `CurrentCellDirtyStateChanged += (s,e) => { if (IsCurrentCellDirty) CommitEdit(Commit); }` never runs; "prompt to save if dirty" logic reports dirty after F2+Escape.
- **Fix:** Track `editor.TextChanged` → set a `current_cell_dirty` flag and raise the event; set it from the check-box toggle path; `NotifyCurrentCellDirty(bool)` sets the flag and raises; `EndEdit/CancelEdit` clear it; `IsCurrentRowDirty` = any cell in the current row committed since it became current.
- **Test:** `BeginEdit(0,0)` → `IsCurrentCellDirty == false`; set editor text → true and event raised once.
- **Tests today:** none.

### DGV-09 — `CancelEdit()` / Escape does not raise `CellEndEdit` — Cat D — P2 — High
- **Ours:** tears down the editor without events (`DataGridView.cs:1088-1102`).
- **Upstream:** `CancelEdit` → `EndEdit(…)` → `OnCellEndEdit` (`…/DataGridView.Methods.cs:3036-3062`, `:6269`).
- **Impact:** Handlers that re-enable buttons / clear "editing" status in `CellEndEdit` stay stuck after Escape.
- **Fix:** Raise `OnCellEndEdit(new DataGridViewCellEventArgs(col,row))` in `CancelEdit` before clearing indices.
- **Test:** `BeginEdit(0,0); CancelEdit();` → `CellEndEdit` count 1.
- **Tests today:** none.

### DGV-10 — Commit path: no typed conversion without a `CellParsing` handler; `DataError` never raised — Cat A/D — P1 — High
- **Ours:** `EndEdit` stores the raw editor string unless a `CellParsing` handler is attached (`DataGridView.cs:963-981`); write-back failures are caught, the cell silently reverted, `committed=false`, and `_dataError` is never invoked anywhere (1004-1007, 1021-1025; event at 235-237). `DataGridViewCell.ParseFormattedValue` exists (`DataGridViewParity.Cell.cs:195-215`) but nothing calls it.
- **Upstream:** `PushFormattedValue` always calls `cell.ParseFormattedValue` to the cell's `ValueType` (`…/DataGridView.Methods.cs:25373`); failures go to `OnDataError`, which invokes the handler or shows a message box, and re-throws when `e.ThrowException` (`:14608-14645`).
- **Impact:** An unbound grid with `Columns[0].ValueType = typeof(int)` stores `"5"` (string) — `(int)cell.Value` casts fail, numeric sort falls to text; typing "abc" into a bound int column just vanishes with no `DataError` and no message; `DataError` handlers (present in most bound grids) never run.
- **Fix:** In `EndEdit`: `parsed = editing_cell.ParseFormattedValue(new_value, editing_cell.InheritedStyle, null, null)` with `ValueType` falling back to column `ValueType` → bound descriptor `PropertyType`; wrap parse + write-back in try/catch → `var e = new DataGridViewDataErrorEventArgs(ex, col, row, Commit|Parsing); OnDataError(true, e); if (e.ThrowException) throw;` and return false (stay in edit mode) as upstream does. Map empty text to `style.DataSourceNullValue` for non-string types.
- **Test:** `Columns[0].ValueType = typeof(int)`; edit "5" → `Value is int 5`; edit "x" → `DataError` raised once, still in edit mode.
- **Tests today:** `DataGridViewHookTests.CellParsing_NotHandled_StoresTheEditedTextAsBefore` codifies the string storage.

### DGV-11 — `CurrentCellChanged`, `CellEnter`, `CellLeave`, `CellValidating` on cell leave — Cat D — P1 — High
- **Ours:** `_currentCellChanged` is invoked only from the `CurrentCell` setter (`DataGridView.cs:1539`), not from mouse/keyboard selection; `CellLeave` declared under `#pragma warning disable CS0067` (196-198); `CellEnter` declared, never raised (`DataGridViewParity.cs:113`); `CellValidating` is raised only inside `EndEdit` (950), never when leaving a non-edited cell.
- **Upstream:** `SetCurrentCellAddressCore` raises `OnCurrentCellChanged` for every path (`…/DataGridView.Methods.cs:26798`, `:26929`, `:27084`); `CellLeave`/`CellValidating`/`CellValidated`/`CellEnter` run on every current-cell move.
- **Impact:** Master-detail forms that refresh on `CurrentCellChanged` never refresh on click; per-cell validation that relies on `CellValidating` when tabbing through untouched cells does not run.
- **Fix:** Raise `OnCurrentCellChanged` from `SelectedRowIndex`/`SelectedColumnIndex` setters (once per change, after `OnRowEnter`); raise `CellLeave`(old)/`CellValidating`/`CellValidated` before and `CellEnter`(new) after the move, mirroring the existing `ValidateRow` shape.
- **Test:** `SelectedRowIndex = 1` → `CurrentCellChanged` 1, `CellLeave(0,0)` then `CellEnter(0,1)`.
- **Tests today:** none.

### DGV-12 — `CurrentCell` setter semantics — Cat A — P2 — High
- **Ours:** `null` is ignored; assigning sets `SelectedColumnIndex` then `SelectedRowIndex`, so `SelectionChanged` fires twice and `RowValidating` runs between the two halves (`DataGridView.cs:1532-1540`). Does not scroll the cell into view.
- **Upstream:** `null` clears selection and current cell; one `SetCurrentCellAddressCore` + a single flushed `SelectionChanged` (`…/DataGridView.cs:1718-1729`, `…/DataGridView.Methods.cs:6787-6791`).
- **Impact:** `grid.CurrentCell = null` (the documented way to "deselect everything" before rebinding) does nothing; double `SelectionChanged` doubles detail refreshes.
- **Fix:** Add a private `SetCurrentCellAddress(col,row)` that validates the old row once, updates both indices, raises `RowEnter`, `CurrentCellChanged`, one `SelectionChanged`; `null` → indices -1 + `ClearSelection`.
- **Test:** count `SelectionChanged` after `CurrentCell = grid[1,1]` == 1; `CurrentCell = null` → `CurrentCell is null`.
- **Tests today:** `DataGridViewTests.CurrentCell_*` (getter only).

### DGV-13 — Default values: `SelectionMode`, `RowHeadersVisible`, `RowHeadersWidth`, `ColumnHeadersHeight`, row height, `DefaultSize`, `Column.MinimumWidth` — Cat E — P1 — High
- **Ours:** `SelectionMode = FullRowSelect` (`DataGridView.cs:32`), `row_headers_visible = false` (17), `row_headers_width = 40` (16), `header_height = 30` (14), `DefaultSize 450×300` (560), `DataGridViewRow.height = 25` (`DataGridViewRow.cs:10`), `Column.MinimumWidth = 30` and `Width` clamped to it (`DataGridViewColumn.cs:167`, `:246`); `ColumnHeadersHeight` min 10 (1513), `Row.Height` min 10 (`DataGridViewRow.cs:48`).
- **Upstream:** `RowHeaderSelect` (`…/DataGridView.cs:250`), `RowHeadersVisible = true` (`:450`), width 41 (`:305`), header height 23 (`:310`), `DefaultSize 240×150` (`:2117`), row height `DefaultFont.Height + 9` = 22 at 96 dpi (`…/DataGridViewRow.cs:157`), `MinimumWidth` default 5 (`…/DataGridViewColumn.cs:21,55`), header min 4, row min 3.
- **Impact:** Designer code omits default-valued properties, so a form designed with row headers visible shows none here; a 24-px icon column (`Width = 24`) silently becomes 30; clicking a cell selects the whole row where WinForms selects the cell.
- **Fix:** Flip the defaults to upstream's; clamp `Width` to `MinimumWidth` default 5; keep `RowHeight` (non-WinForms) as an alias for `RowTemplate.Height`.
- **Test:** `new DataGridView()` → `RowHeadersVisible`, `SelectionMode == RowHeaderSelect`, `RowHeadersWidth == 41`, `ColumnHeadersHeight == 23`.
- **Tests today:** `DataGridViewTests.Ctor_Default`, `DataGridViewRowTests.Height_SetDefault_IsTwentyFive`, `*_ClampsToMinimum` codify the divergent values.

### DGV-14 — Multi-selection: `MultiSelect`, Ctrl/Shift-click, `row.Selected`/`cell.Selected` setters, `SelectedRows` order, `SelectedCells`, `SelectedColumns` — Cat C/A — P1 — High
- **Ours:** `MultiSelect` stored-only (`DataGridView.cs:735-747`); `OnMouseDown` always single-selects via `SelectedRowIndex` (2260-2267, no modifier check); `DataGridViewRow.Selected` / `DataGridViewCell.Selected` are auto-properties (`DataGridViewRow.cs:62`, `DataGridViewCell.cs:140`) — no `SelectionChanged`, no repaint, and the renderer highlights only `SelectedRowIndex` (`Renderers/DataGridViewRenderer.cs:223`, `:413`, `:456`); `SelectedCells` is derived from `SelectedRows` (2629-2638), so in `CellSelect` mode after `SelectAll` (2986-2989 sets `cell.Selected`) `SelectedCells` is empty; `SelectedColumns` returns a new empty collection (2978); `SelectedRows` is in index order.
- **Upstream:** selection lists are prepended (`…/DataGridViewIntLinkedList.cs:93-101`) so `SelectedRows` is most-recent-first (`…/DataGridView.cs:3726`); `row.Selected = true` selects and repaints and raises `SelectionChanged`; Ctrl/Shift extend selection when `MultiSelect`.
- **Impact:** "delete selected rows" loops delete one row; programmatic `Rows[i].Selected = true` highlights nothing and raises nothing; `SelectedRows[0]` is the *first* rather than the most recently clicked row (code that reads `SelectedRows[0]` after a Shift-click gets the wrong row); `SelectedCells.Count` is wrong in cell modes.
- **Fix:** Keep a `List<int>` of selected rows/cells (most-recent-first) on the grid; make `Row.Selected`/`Cell.Selected` setters call back into it (`owner?.DataGridView?.SetRowSelected(this, value)`) which raises `SelectionChanged` + `Invalidate`; in `OnMouseDown` honour `MultiSelect` with Ctrl (toggle) and Shift (range); renderer paints `row.Selected`/`cell.Selected`; `SelectedCells` per mode; `SelectedColumns` from a selected-column list (header click in FullColumnSelect/ColumnHeaderSelect).
- **Test:** `rows[0].Selected = rows[2].Selected = true` → `SelectionChanged` ×2, `SelectedRows.Count == 2`, `SelectedRows[0] == rows[2]`; render and probe both rows' highlight colour.
- **Tests today:** `DataGridViewTests.SelectAll_FullRowSelect_SelectsAllRows`, `GetCellCount_SelectedFilter_CountsSelectedCells` (row mode only).

### DGV-15 — `ClearSelection()` resets `CurrentCell` and raises no `SelectionChanged` — Cat A — P2 — High
- **Ours:** clears flags and sets both indices to -1 without raising (`DataGridView.cs:2806-2814`).
- **Upstream:** clears selection only; `_ptCurrentCell` is untouched and `SelectionChanged` is flushed (`…/DataGridView.Methods.cs:3385-3450`, `:6787`).
- **Impact:** `grid.ClearSelection(); … grid.CurrentRow.Cells[…]` NREs; selection-count labels don't update.
- **Fix:** Leave `selected_row_index/selected_column_index` alone (they are the current cell); raise `OnSelectionChanged` once if anything changed. (Requires DGV-14's separation of "current" from "selected".)
- **Test:** `SelectedRowIndex = 1; ClearSelection();` → `CurrentRow == Rows[1]`, `SelectedRows` empty, `SelectionChanged` raised.
- **Tests today:** `DataGridViewTests.ClearSelection_ResetsCurrentRowAndCell` (line 463) asserts the divergent `CurrentRow == null`.

### DGV-16 — `SortedColumn`, `SortOrder`, `Sorted`, `HeaderCell.SortGlyphDirection`, `SortCompare` — Cat A/D/C — P1 — High
- **Ours:** `SortedColumn`/`SortOrder` have private setters that are never assigned (`DataGridView.cs:2936-2939`; grep shows no writer) so they are always `null`/`None`; `Sorted` under `CS0067` (932); `SortCompare` is `add { } remove { }` (440); `Sort(column, direction)` just reorders rows (2817-2823); the header-click path toggles the non-WinForms `column.SortOrder` (1630-1638) which the renderer draws (`Renderer:117`), while `HeaderCell.SortGlyphDirection` is stored-only (`RemainingMemberParity.cs:217`) and never drawn.
- **Upstream:** `Sort` → `SortInternal` sets `SortedColumn`, `SortOrder`, clears/sets `HeaderCell.SortGlyphDirection`, raises `OnSorted`; `OnSortCompare` is consulted for unbound sorts (`…/DataGridView.Methods.cs:28156`, `:28251-28267`, `:18910-18929`).
- **Impact:** The standard toggle `if (grid.SortedColumn == col && grid.SortOrder == Ascending) … Descending` always sees `null`; custom `SortCompare` (e.g. natural/numeric-string sorting) is ignored; programmatic sorts show no glyph; `Sorted` handlers (re-select row after sort) never run.
- **Fix:** In `SortByColumn`: set `SortedColumn`/`SortOrder`, set the old and new `HeaderCell.SortGlyphDirection`, consult `SortCompare` (if subscribed, `e.Handled` → use `e.SortResult`) in the comparer, raise `Sorted`; make `column.SortOrder` an alias of `HeaderCell.SortGlyphDirection` and the renderer read the header cell.
- **Test:** `Sort(col, Ascending)` → `SortedColumn == col`, `SortOrder == Ascending`, `col.HeaderCell.SortGlyphDirection == Ascending`, `Sorted` raised.
- **Tests today:** `DataGridViewTypedValueTests.*_sort_*` (order only).

### DGV-17 — Header-click sorting ignores `SortMode`; bound grids are sorted by reordering rows — Cat A — P1 — Medium
- **Ours:** gate is the non-WinForms `column.Sortable` (default true) (`DataGridView.cs:2246`); `SortMode` is stored-only (`DataGridViewColumn.cs:115`) so `NotSortable`/`Programmatic` columns still sort; `SortByColumn` reorders `Rows` (2785-2803) even when bound, and the next `ListChanged` rebind (1718-1725) restores source order.
- **Upstream:** `OnColumnHeaderMouseClick` sorts only when `CanSort(column)` (SortMode Automatic) and, when bound, only if the list `SupportsSorting`, via `IBindingList.ApplySort` (`…/DataGridView.Methods.cs:13641-13660`).
- **Impact:** Programmatic-sort columns (custom sort on click) get double-sorted; a `DataView`-bound grid appears sorted until any edit, then snaps back.
- **Fix:** Gate on `SortMode == Automatic`; when bound and `bound_list is IBindingList { SupportsSorting: true }` → `ApplySort(descriptor, direction)` (DataView honours it) and let the rebind order rows; otherwise sort rows as today.
- **Test:** `SortMode = Programmatic`, simulate header click → row order unchanged, `ColumnHeaderMouseClick` still raised.
- **Tests today:** none.

### DGV-18 — Auto-sizing: `AutoSizeColumnsMode` (incl. `Fill`), `AutoSizeRowsMode`, `AutoResizeColumn(s)`, `AutoResizeRow(s)`, `ColumnHeadersHeightSizeMode.AutoSize`, `RowHeadersWidthSizeMode` — Cat C/B — P1 — High
- **Ours:** setters store + `Invalidate` (`DataGridView.cs:463-510`, 794-807); `AutoResize*` → `Invalidate()` (2941-2960); `column.AutoSizeMode`/`FillWeight` stored (`DataGridViewColumn.cs:202-205`); `GetPreferredWidth`/`GetPreferredHeight` exist (`DataGridViewFamilyParity.cs:233`, `:159`) but nothing calls them.
- **Upstream:** `AutoResizeColumns` measures (`…/DataGridView.Methods.cs:1927-1932`); `Fill` is redistributed by `FillWeight` on every layout (`AdjustFillingColumns` `:882`).
- **Impact:** `AutoSizeColumnsMode = Fill` (present in most designer-built grids) leaves 100-px columns and a blank right band; `AllCells` leaves truncated text; `RowHeadersWidthSizeMode.AutoSizeToAllHeaders` does nothing. The compat matrix documents "only invalidates", but `Fill` is P1 by traffic.
- **Fix:** In `UpdateScrollBars` (called from `SetBoundsCore`/`OnColumnsChanged`): if `AutoSizeColumnsMode == Fill` or any column `InheritedAutoSizeMode == Fill`, distribute `available_width − fixed columns` across fill columns by `FillWeight` (respect `MinimumWidth`); implement `AutoResizeColumn(s)` via `GetPreferredWidth`, `AutoResizeRow(s)` via `GetPreferredHeight`, and apply `AutoSizeRowsMode`/`ColumnHeadersHeightSizeMode.AutoSize` after row/column changes.
- **Test:** 2 columns, `AutoSizeColumnsMode = Fill`, `Width = 400` → column widths sum to the content width, proportional to `FillWeight`.
- **Tests today:** `DataGridViewTests.AutoSizeColumnsMode_Set_GetReturnsExpected` (store only).

### DGV-19 — `RowTemplate` never used to create rows (`RowTemplate.Height`, `RowTemplate.DefaultCellStyle`) — Cat C — P1 — High
- **Ours:** `RowTemplate` is a settable field with no readers (`DataGridView.cs:2925-2930`); every add path does `new DataGridViewRow()` — `Rows.Add` overloads (`DataGridViewRowCollection.cs:35,49,64`), `Insert(int,int)` (`OverloadParity.Final.cs:107`), `RowCount` setter (3130), and the three bound-row builders (1762, 1818, 1835).
- **Upstream:** all row creation goes through `RowTemplateClone` (`…/DataGridView.cs:3564-3572`; callers `…/DataGridViewRowCollection.cs:204,348,1367`, `…/DataGridView.DataConnection.cs:598`).
- **Impact:** `dgv.RowTemplate.Height = 32;` (the WinForms way to set row height, emitted by the designer) does nothing; `RowTemplate.DefaultCellStyle`/`MinimumHeight`/`Resizable` are ignored.
- **Fix:** Add `internal DataGridViewRow CreateRowFromTemplate() => (DataGridViewRow)RowTemplate.Clone()` and use it in every path above (clear its cells first for bound rows).
- **Test:** `RowTemplate.Height = 40; Rows.Add();` → `Rows[0].Height == 40`.
- **Tests today:** none.

### DGV-20 — `DataGridViewRow.Visible = false` still laid out, painted and hit-tested — Cat C — P0 — High
- **Ours:** auto-property (`DataGridViewRow.cs:100`); the renderer iterates every row (`Renderers/DataGridViewRenderer.cs:173-186`); `GetCellBounds` (1173), `GetRowAtLocation` (1491), `UpdateScrollBars` (3055), `DisplayedRowCount` (3156) and `EnsureRowVisible` never consult it. Only `InheritedState`/`GetRowDisplayRectangle` do.
- **Upstream:** `DataGridViewBand.Visible` setter raises `OnStateChanging/OnStateChanged(Visible)` and the grid removes the row from layout, hit-testing and painting (`…/DataGridViewBand.cs:668-700`, `…/DataGridViewRow.cs:336-350`).
- **Impact:** The most common client-side filter idiom — `foreach (var r in grid.Rows) r.Visible = !Matches(r)` — shows every row. (Note WinForms throws when hiding the current row; ours neither hides nor throws.)
- **Fix:** Make `Visible` call `owner?.OnRowsChanged()` and have the renderer, `GetCellBounds`, `GetRowAtLocation`, `UpdateScrollBars`, `DisplayedRowCount`, `EnsureRowVisible`, `Get*Row(Visible)` skip `!row.Visible`. Same sweep for `Column.Visible` already exists — mirror it.
- **Test:** 3 rows, `Rows[1].Visible = false` → `GetCellDisplayRectangle(0, 2, false).Y == old Y of row 1`; `GetRowAtLocation` at that Y returns 2; render and probe.
- **Tests today:** `DataGridViewHookTests:721` asserts only the `InheritedState` flag.

### DGV-21 — Cell painting ignores the style cascade: `DefaultCellStyle.Alignment/WrapMode/SelectionBackColor/SelectionForeColor/Padding`, `column.DefaultCellStyle.BackColor/ForeColor/Font`, `RowsDefaultCellStyle`, `row.DefaultCellStyle.ForeColor` — Cat C — P1 — High
- **Ours:** `RenderCell` takes alignment from the non-WinForms `column.DefaultCellStyleAlignment` (`Renderers/DataGridViewRenderer.cs:487`, `DataGridViewColumn.cs:229`), colours/font from `cell.Style` (a `ControlStyle`) merged with a CellFormatting handler's style only (339, 374-399, 443-474), selection colour is fixed `Theme.ControlHighlightLowColor`/`Theme.AccentColor` (224, 457), `maxLines: 1` ignores `WrapMode` (528), row background honours `row.DefaultCellStyle.BackColor` and `AlternatingRowsDefaultCellStyle.BackgroundColor` only (227-236). `cell.InheritedStyle` (correctly cascaded, `DataGridViewCell.cs:234-259`) is passed to handlers but never used for drawing.
- **Upstream:** cell paint uses `cellStyle.Alignment`, `WrapMode`, `SelectionBackColor/ForeColor`, `Padding`, `Font`, `BackColor/ForeColor` (`…/DataGridViewTextBoxCell.cs:139-232`, `:410-411`).
- **Impact:** `Columns["Amount"].DefaultCellStyle.Alignment = MiddleRight` — the single most common column customisation — leaves numbers left-aligned; `DefaultCellStyle.SelectionBackColor` is ignored; `col.DefaultCellStyle.BackColor` only takes effect via a CellFormatting handler; `WrapMode = True` never wraps. (The matrix admits the column/row style gap; the alignment case is not mentioned.)
- **Fix:** In `RenderRowCell` compute `var style = the_cell.InheritedStyle` once, apply `handlerStyle` over it, and drive `RenderCell` from it: map `DataGridViewContentAlignment` → `ContentAlignment`; `WrapMode == True` → `maxLines: null`; `SelectionBackColor/ForeColor` when selected; `Padding` insets; `Font/BackColor/ForeColor` fall back to the existing `ControlStyle` path. Make `DefaultCellStyleAlignment` an alias of `DefaultCellStyle.Alignment`.
- **Test:** `Columns[0].DefaultCellStyle.Alignment = MiddleRight`; render "1" in a 200-px column; probe that ink is in the right third. Or expose the resolved alignment through `CellPainting.CellStyle` and assert.
- **Tests today:** `DataGridViewHookTests.RowDefaultCellStyle_BackColor_IsActuallyPainted`, `Cell_InheritedStyle_MergesTheGridColumnAndRowCascade` (cascade only).

### DGV-22 — `GridColor` and `BackgroundColor` never read by the renderer — Cat C — P1 — High
- **Ours:** both store + raise + `Invalidate` (`DataGridView.cs:750-777`) with `Color.Empty` defaults; the renderer draws grid lines with `Theme.BorderLowColor` (`Renderers/DataGridViewRenderer.cs:253`, `:503-509`, `:516-521`) and never fills the area below the last row.
- **Upstream:** grid lines use `GridPenColor` (default `SystemColors.WindowFrame`, `…/DataGridView.cs:2087`); the empty area is filled with `BackgroundColor` (default `AppWorkspace`).
- **Impact:** Themed grids (`GridColor = Color.LightGray; BackgroundColor = Color.White`) — in nearly every styled app — render with theme colours and a theme-grey empty band.
- **Fix:** `BorderColor(style)` → `control.GridColor.IsEmpty ? theme : ToSK(control.GridColor)`; after `RenderRows`, fill `[lastRowBottom, content.Bottom)` with `BackgroundColor` when set (default it to `SystemColors.AppWorkspace`).
- **Test:** `GridColor = Color.Red`; render; probe a pixel on a row's bottom border.
- **Tests today:** none.

### DGV-23 — `DataGridViewColumn.DisplayIndex` setter is a no-op; `AllowUserToOrderColumns` stored-only — Cat B — P2 — High
- **Ours:** `DisplayIndex { get => Index; set { /* ordering not implemented */ } }` (`DataGridViewColumn.cs:223-226`); `ColumnDisplayIndexChanged` is `add { } remove { }` (`DataGridView.cs:422`).
- **Upstream:** `DisplayIndex` reorders display without changing `Index`; header drag reorders when `AllowUserToOrderColumns`.
- **Impact:** `Columns["Total"].DisplayIndex = 0` (moving a bound column) silently does nothing; designer-serialized `DisplayIndex` values are dropped.
- **Fix:** Keep a display-order list in `DataGridViewColumnCollection` consulted by the renderer/geometry helpers (or, minimally, physically `Move` and raise `ColumnDisplayIndexChanged`).
- **Test:** 3 columns, `Columns[2].DisplayIndex = 0` → `GetCellDisplayRectangle(2,0,false).X == 0`.
- **Tests today:** none.

### DGV-24 — `HeaderText` is not `HeaderCell.Value` — Cat A — P2 — High
- **Ours:** `HeaderText` is a private field (`DataGridViewColumn.cs:10`, `:149-157`); the renderer draws `column.HeaderText` (`Renderer:114`); `HeaderCell.Value` is independent.
- **Upstream:** `HeaderText` reads/writes `HeaderCell.Value` (`…/DataGridViewColumn.cs:394`).
- **Impact:** `col.HeaderCell.Value = "Qty"` shows the old text; `row.HeaderCell.Value = rowNumber` (row-number idiom) is never painted either — `RenderRowHeader` draws only the current-row triangle (`Renderer:404-427`).
- **Fix:** `HeaderText { get => header_cell.Value as string ?? ""; set => header_cell.Value = value; }`; paint `row.HeaderCell.Value` in `RenderRowHeader`.
- **Test:** `col.HeaderCell.Value = "X"` → `col.HeaderText == "X"`.
- **Tests today:** none.

### DGV-25 — Check-box column: toggles without dirty/commit, ignores `TrueValue/FalseValue`, no bound write-back, ignores column/grid `ReadOnly` — Cat A — P1 — High
- **Ours:** `OnMouseDown` flips `cell.Value` to a `bool` and raises `CellValueChanged` immediately, checking only `cell.ReadOnly` (`DataGridView.cs:2269-2282`); nothing writes to the data source (only `EndEdit` does); renderer treats only `"True"`/`"1"` as checked (`Renderer:591`); `TrueValue/FalseValue/IndeterminateValue/ThreeState` on column and cell are stored-only (`DataGridViewFamilyParity.cs:340-346`, `:406-415`).
- **Upstream:** click → `SwitchFormattedValue` + `NotifyCurrentCellDirty(true)`; the value commits (and pushes to the bound item) on `CommitEdit`/cell leave; `GetFormattedValue` maps `TrueValue/FalseValue` to `CheckState` (`…/DataGridViewCheckBoxCell.cs:774-797`, `:564-585`); read-only cells don't toggle (`BeginEditInternal` gate).
- **Impact:** Bound `bool` columns show the toggle but the object never changes and the next `ListChanged` reverts it; `TrueValue = "Y"` columns (char flags in DataTables) never render checked and toggling writes a `bool` into a string column; `grid.ReadOnly = true` still toggles.
- **Fix:** Gate on `InheritedState.ReadOnly`; compute checked via `Equals(value, TrueValue ?? true)`; toggle to `TrueValue/FalseValue` (`IndeterminateValue` when `ThreeState`); mark dirty (DGV-08) and commit through the same write-back as `EndEdit` (or immediately call it), then `CellValueChanged`.
- **Test:** `BindingList<Item{bool Done}>`; simulate mouse-down on the cell → `items[0].Done == true`; `ReadOnly = true` → unchanged; `TrueValue="Y"` with value `"Y"` → `CellPainting.FormattedValue`/renderer shows checked.
- **Tests today:** `DataGridViewLiveBindingTests.Bool_member_generates_a_checkbox_column` (column type only).

### DGV-26 — Combo-box column shows the raw value, not the `DisplayMember` lookup; editor is a TextBox — Cat A — P1 — High
- **Ours:** `RenderComboBoxCell` draws the formatted raw value (`Renderer:484-485`, `:606-617`); `DataSource/DisplayMember/ValueMember/Items` are stored-only (`DataGridViewCompat.cs:364-398`); `BeginEdit` always hosts a `TextBox` (`DataGridView.cs:150`) and `DataGridViewComboBoxEditingControl` (`MissingTypesParity.cs:335`) is never used.
- **Upstream:** `GetFormattedValue` resolves the value through `DisplayMember`/`ValueMember` (`…/DataGridViewComboBoxCell.cs:948-975`) and raises `DataError` for unmatched values; editing hosts a combo box.
- **Impact:** The universal lookup-column pattern (`CustomerId` shown as customer name) renders the id; editing offers free text instead of a list.
- **Fix:** In `ApplyCellFormatting` (and `FormattedValue`) for `DataGridViewComboBoxColumn`/`Cell`: resolve via `ListBindingHelper.GetList(DataSource)` + `ValueMember → DisplayMember` (fallback to `Items`); in `BeginEdit` host a `ComboBox` bound the same way and commit `SelectedValue`.
- **Test:** column `DataSource = List<(Id,Name)>`, `ValueMember="Id"`, `DisplayMember="Name"`; cell value 2 → `CellPainting.FormattedValue == "Two"`.
- **Tests today:** none.

### DGV-27 — Button column `UseColumnTextForButtonValue` uses `HeaderText` instead of `Text` — Cat A — P2 — High
- **Ours:** `btn_col.UseColumnTextForButtonValue ? btn_col.HeaderText : value` (`Renderers/DataGridViewRenderer.cs:482`).
- **Upstream:** `GetValue` returns `dataGridViewButtonColumn.Text` (`…/DataGridViewButtonCell.cs:422-433`).
- **Impact:** A column with `HeaderText = "Actions"`, `Text = "Delete"` renders "Actions" on every button.
- **Fix:** Use `btn_col.Text`.
- **Test:** render / `CellPainting.FormattedValue == "Delete"`.
- **Tests today:** none.

### DGV-28 — Link column painted as plain text; `CellContentClick` raised for any click in any cell — Cat A — P2 — High
- **Ours:** no `DataGridViewLinkColumn` branch in `RenderCell` (falls to `DrawText`, `Renderer:486-488`); `LinkColor/VisitedLinkColor/LinkBehavior` stored-only (`DataGridViewCompat.cs:297-331`); `OnMouseDown` raises `CellContentClick` for every `col >= 0` (`DataGridView.cs:2287-2291`), after `CellMouseClick` (upstream order is `CellClick → CellContentClick → CellMouseClick`).
- **Upstream:** link cells paint underlined in `LinkColor`; `CellContentClick` fires only when the hit is inside the content bounds (`…/DataGridView.Methods.cs:11622`).
- **Impact:** Links are not distinguishable from text; handlers assuming content-only clicks fire on padding. Mostly cosmetic.
- **Fix:** Paint link cells underlined with `LinkColor`; raise `CellContentClick` only when the hit lies inside `cell.GetContentBounds`.
- **Test:** click in a cell's padding → `CellClick` yes, `CellContentClick` no.
- **Tests today:** none.

### DGV-29 — Keyboard: handled on `KeyUp`; Enter, Delete, Ctrl+C, Home/End, Left/Right (FullRowSelect), Tab (FullRowSelect), `StandardTab`, `AllowUserToDeleteRows`, `UserDeletingRow`/`UserDeletedRow` — Cat A/B/D — P1 — High
- **Ours:** all navigation is in `OnKeyUp` (`DataGridView.cs:2412-2494`): Enter does nothing; Delete does nothing (`AllowUserToDeleteRows` stored-only 671-683; `UserDeletingRow`/`UserDeletedRow` fields never invoked 227-233); Ctrl+C not handled although `GetClipboardContent` works (2835); Home/End jump to first/last *row*; Left/Right/Tab are ignored in `FullRowSelect`; `StandardTab` stored (810).
- **Upstream:** `ProcessEnterKey` commits and moves down (`…/DataGridView.Methods.cs:21395-21419`); `ProcessDeleteKey` removes selected rows with `UserDeletingRow`/`UserDeletedRow` (`:19924-19953`); Ctrl+C/Ctrl+Insert → `ProcessInsertKey` → clipboard (`:20091`, `:21853-21870`); Home/End move to first/last column (Ctrl+ to first/last cell, `:21543-21561`); Tab honours `StandardTab` (`:24339-24368`); all on key-down with auto-repeat.
- **Impact:** Enter in a grid does nothing (users expect move-down/commit); Delete on a selected row does nothing; Ctrl+C copies nothing; holding an arrow key does not repeat; Home/End behave like Ctrl+Home/End.
- **Fix:** Move handling to `OnKeyDown` (or `ProcessDialogKey`); add Enter (`EndEdit` + down), Delete (if `AllowUserToDeleteRows && !ReadOnly`: for each selected row raise `UserDeletingRow` → `Rows.RemoveAt`/`IBindingList.Remove` → `UserDeletedRow`), Ctrl+C/Ctrl+Insert → `Clipboard.SetDataObject(GetClipboardContent())`, Home/End = first/last column, Ctrl+Home/End = first/last cell, Left/Right/Tab in all modes, `StandardTab` → let the form handle Tab.
- **Test:** select row 1, `OnKeyDown(Delete)` → `Rows.Count` decremented, both events raised; `OnKeyDown(Enter)` → `SelectedRowIndex + 1`.
- **Tests today:** none.

### DGV-30 — Mouse cell events never raised: `CellMouseDown/Up/Move/DoubleClick`, `RowHeaderMouseClick`, `RowHeaderMouseDoubleClick`, `ColumnHeaderMouseDoubleClick`, `CellContentDoubleClick`; `CellClick`/`ColumnHeaderMouseClick` raised on mouse-down — Cat D — P1 — High
- **Ours:** `add { } remove { }` discards subscribers (`DataGridView.cs:258-267`, `:355`, `:369`, `:401`, `:413`); the protected `OnCellMouseDown/Up/Move` are empty and never called (`KryptonPortParity.cs:196-210`); `CellClick`/`CellMouseClick`/`ColumnHeaderMouseClick` fire inside `OnMouseDown` (2244, 2285-2288).
- **Upstream:** raised from the mouse pipeline with cell-relative coordinates: `OnCellMouseDown` on down, `OnCellMouseUp`/`OnCellClick`/`OnCellMouseClick` on up, double-click variants on the second click (`…/DataGridView.Methods.cs:5805-5930`).
- **Impact:** The right-click idiom `CellMouseDown += (s,e) => { if (e.Button == Right) CurrentCell = grid[e.ColumnIndex, e.RowIndex]; }` (context menu on the clicked row) never runs, so menus act on the wrong row; row-header click handlers never run; drag-select/drag-drop from `CellMouseMove` impossible.
- **Fix:** Back the events with fields; in `OnMouseDown/OnMouseUp/OnMouseMove/OnDoubleClick` hit-test (existing helpers) and raise with `new DataGridViewCellMouseEventArgs(col,row, x - cellLeft, y - cellTop, e)`; when `x` is within the row-header band raise `RowHeaderMouseClick` with `ColumnIndex = -1`; move `CellClick`/`CellMouseClick`/`ColumnHeaderMouseClick` to `OnMouseUp`.
- **Test:** simulate `OnMouseDown` on cell (1,1) → `CellMouseDown` with `(ColumnIndex 1, RowIndex 1)` and local coordinates; `CellClick` not yet; `OnMouseUp` → `CellClick`.
- **Tests today:** none.

### DGV-31 — Data binding: every `ListChanged` rebuilds all columns and rows — Cat A — P0 — High
- **Ours:** `OnBoundListChanged` → `OnDataSourceChanged()` for every `ListChangedType` (`DataGridView.cs:1718-1725`); that method does `Rows.Clear()` and, when `AutoGenerateColumns`, `Columns.Clear()` + regenerate (1751, 1797), then rebuilds every row (1729-1855).
- **Upstream:** `ProcessListChanged`: `ItemAdded` inserts one `RowTemplateClone` row, `ItemDeleted` removes one, `ItemChanged` refreshes one row; only `Reset`/`PropertyDescriptor*` call `RefreshColumnsAndRows` (`…/DataGridView.DataConnection.cs:567-622`, `:799-816`).
- **Impact:** `grid.DataSource = bindingList; grid.Columns["Id"].Visible = false; grid.Columns["Name"].HeaderText = "Customer"; grid.Columns[0].Width = 60;` — the first add/edit/delete in the list silently restores the auto-generated columns (Id visible, header "Name", widths re-estimated). Every `ItemChanged` (any `INotifyPropertyChanged` edit, including the grid's own `EndEdit` write-back) rebuilds N rows: O(N) per commit, cached `DataGridViewCell`/`DataGridViewRow` references (and `row.Tag`, `row.DefaultCellStyle`, `row.Height` set by the app) go stale/lost, and `RowsRemoved(0,N)` fires with no matching `RowsAdded` (see DGV-33).
- **Fix:** Switch on `e.ListChangedType`: `ItemAdded` → build one row from the descriptors and `Rows.Insert(e.NewIndex, row)`; `ItemDeleted` → `Rows.RemoveAt(e.NewIndex)`; `ItemChanged` → refresh cell values of `Rows[e.NewIndex]` (and `Invalidate`); `ItemMoved` → move; `Reset` → rebuild rows but regenerate columns only if the descriptor names/types differ; `PropertyDescriptor*` → full rebuild. Keep `SetInitialCurrentCell` clamping `selected_row_index` to `Rows.Count - 1`.
- **Test:** bind `BindingList<T>`, rename a column header, `list.Add(...)` → header text preserved, `Rows.Count + 1`, `RowsAdded(index, 1)` raised once, existing `DataGridViewRow` references still owned.
- **Tests today:** `DataGridViewLiveBindingTests.Grid_picks_up_rows_added_after_it_was_bound` (count only); `DataGridViewTests.AutoGeneratedColumns_*_survive_HeaderText_rename` covers a *rename before* data change, not after.

### DGV-32 — `DataMember` ignored; `DataSource` getter returns the resolved list; unsupported sources silently keep the old list — Cat A — P1 — High
- **Ours:** `DataMember` stores + raises (`DataGridView.cs:569-582`), never used in binding; `DataSource` setter resolves `DataTable → DefaultView`, `IListSource → GetList()`, and `_ => data_source` (keeps the previous list for e.g. a LINQ `IEnumerable<T>`), and the getter returns that resolved `IList` (538-557).
- **Upstream:** `DataMember` setter re-resolves `SetDataConnection(DataSource, value)` (`…/DataGridView.cs:1884-1897`); `DataSource` getter returns the object assigned (`:1913-1915`); an invalid source throws `ArgumentException`.
- **Impact:** `grid.DataSource = dataSet; grid.DataMember = "Orders";` (classic ADO.NET) shows a `DataViewManager`'s rows instead of the Orders table; `((DataTable)grid.DataSource).Rows` throws `InvalidCastException` (it is a `DataView`); `grid.DataSource = customers.Where(...)` leaves the *previous* data on screen with no error.
- **Fix:** Keep `data_source_object`; resolve `data_source = ListBindingHelper.GetList(data_source_object, DataMember) as IList` (materialise `IEnumerable` like `BindingSource` already does); re-resolve in the `DataMember` setter; throw `ArgumentException` for non-list sources.
- **Test:** `DataSource = dataSet; DataMember = "Orders"` → `Columns` match Orders; `Assert.Same(dataSet, grid.DataSource)`.
- **Tests today:** `DataGridViewLiveBindingTests` use `BindingSource.DataMember` only.

### DGV-33 — `RowsAdded` not raised for bound rows or `ReplaceAll`; `RowsRemoved` raised on rebind; `Rows.Insert(int,row)` bypasses `RowsAdded`; `Rows.Add*` while bound does not throw — Cat D/A — P1 — High
- **Ours:** `ReplaceAll` raises nothing (`DataGridViewRowCollection.cs:127-141`) while `ClearItems` raises `RowsRemoved(0, N)` (93-104) — so every rebind (DGV-31) fires N removals and zero additions; `Insert(int, DataGridViewRow)` goes straight to `Items.Insert` (85-90); no bound-mode guard in any `Add`.
- **Upstream:** bound rows arrive through `InsertInternal` → `OnRowsAdded`; `Add()`/`Add(params)` throw `InvalidOperationException` when `DataSource != null` (`…/DataGridViewRowCollection.cs:175-180`, `:272-282`).
- **Impact:** `RowsAdded` handlers that colour/number bound rows (a top-5 idiom) never run; counters tracking `RowsAdded/RowsRemoved` go negative; `Rows.Add(...)` on a bound grid creates a phantom row that vanishes on the next `ListChanged`.
- **Fix:** `ReplaceAll` → `RaiseRowsAdded(0, rows.Count)` when non-empty; route `Insert(int,row)` through `InsertItem`; in `Add*` overloads `if (owner.DataSource is not null) throw new InvalidOperationException(...)`.
- **Test:** bind 3 items → `RowsAdded(0,3)` once; `Rows.Add()` while bound → throws.
- **Tests today:** `DataGridViewCollectionEventTests` (unbound only).

### DGV-34 — Scrolling API: `ScrollIntoView`, `FirstDisplayedCell`, `FirstDisplayedScrollingColumnIndex`, `HorizontalScrollingOffset`, `ScrollBars`, `Scroll` event, `DisplayedRowCount(includePartialRow)` — Cat B/C — P2 — High
- **Ours:** `ScrollIntoView => Invalidate()` (`DataGridView.cs:2996`); `FirstDisplayedCell` getter returns `Rows[0].Cells[0]` regardless of scroll (`DataGridViewParity.cs:443-450`); `FirstDisplayedScrollingColumnIndex`, `HorizontalScrollingOffset` (not tied to `horizontal_scroll_offset`), `ScrollBars` are auto-properties (3112-3118); `Scroll` is `add { } remove { }` on `Control` (`Control.Events.cs:568`) and `OnScroll` is never called (`KryptonPortParity.cs:199`); `DisplayedRowCount` ignores its parameter and counts from row 0 rather than `top_index` (3149-3167).
- **Upstream:** `DisplayedRowCount` distinguishes partial rows (`…/DataGridView.Methods.cs:5563-5567`); `Scroll` raised on every scroll (`…/DataGridView.cs:4868`); `ScrollBars.None` hides both bars.
- **Impact:** "scroll to the newly added row" via `ScrollIntoView`/`FirstDisplayedCell = …` does nothing (only `FirstDisplayedScrollingRowIndex` works); two grids kept in sync via `Scroll` never sync; `ScrollBars = None` still shows bars.
- **Fix:** `ScrollIntoView(col,row)` → `EnsureRowVisible(row)` + horizontal ensure; `FirstDisplayedCell` getter → `Rows[top_index].Cells[first visible col]`, setter → scroll; wire `HorizontalScrollingOffset`/`FirstDisplayedScrollingColumnIndex` to `hscrollbar.Value`; raise `Scroll` from both scrollbar `ValueChanged` handlers; honour `ScrollBars` in `UpdateScrollBars`; `DisplayedRowCount(true)` counts the partial row.
- **Test:** 100 rows, `ScrollIntoView(0, 50)` → `FirstDisplayedScrollingRowIndex > 0` and `Scroll` raised.
- **Tests today:** `DataGridViewScrollBarChildrenTests` (children only).

### DGV-35 — `HitTest` reports only cells; `DataGridViewHitTestType` is nested in `DataGridView` — Cat A/E — P2 — High
- **Ours:** scans cells only and returns `Nowhere` for headers (`DataGridView.cs:1269-1280`); the enum is `DataGridView.DataGridViewHitTestType` (1283-1299) with no top-level type (grep: only DataGridView.cs references).
- **Upstream:** top-level `System.Windows.Forms.DataGridViewHitTestType` with `ColumnHeader/RowHeader/TopLeftHeader/…`; `HitTest` reports headers and scrollbars (`…/DataGridView.HitTestInfo.cs`).
- **Impact:** Context-menu code `var hit = grid.HitTest(e.X, e.Y); if (hit.Type == DataGridViewHitTestType.ColumnHeader)` does not compile outside a `DataGridView` subclass and, once fixed, never sees a header hit.
- **Fix:** Move the enum to namespace scope (keep a nested alias for source compat); in `HitTest` use `GetColumnAtLocation`/`GetRowAtLocation`/header rect logic to report `ColumnHeader`, `RowHeader`, `TopLeftHeader`, and the scrollbars.
- **Test:** `HitTest(x, 5)` over a header → `Type == ColumnHeader`, `ColumnIndex` correct.
- **Tests today:** none.

### DGV-36 — `DataGridViewCell.FormattedValue` / `GetClipboardContent` bypass formatting; commit ignores `NullValue`/`DataSourceNullValue` — Cat A — P2 — High
- **Ours:** `FormattedValue => FormattedTextOverride ?? value?.ToString()` (`DataGridViewCell.cs:187`) — no `Format`, `NullValue`, or `CellFormatting`; `GetClipboardContent` copies it (`DataGridView.cs:2881`); `ApplyCellFormatting` (2696) is renderer-only; `EndEdit` stores `""` for cleared cells (965).
- **Upstream:** `GetFormattedValue` is the single formatting path used by paint, clipboard and `FormattedValue`; `ParseFormattedValue` maps empty/`NullValue` text to `DataSourceNullValue` (`DBNull`).
- **Impact:** Copy/paste of a `"C2"`-formatted column yields raw decimals; `cell.FormattedValue` in handlers differs from what is drawn; clearing a bound int cell tries to store `""` → conversion failure → silent revert (DGV-10).
- **Fix:** `FormattedValue` → `DataGridView?.ApplyCellFormatting(OwningRow, RowIndex, ColumnIndex, out _) ?? value?.ToString()`; in commit, `text == "" && ValueType != typeof(string)` → `style.DataSourceNullValue`.
- **Test:** `DefaultCellStyle.Format = "C2"`, value `1.5m` → `FormattedValue == "$1.50"` (invariant culture).
- **Tests today:** `DataGridViewHookTests.GetClipboardContent_UsesTheFormattedValue` (asserts the current override-only behaviour).

### DGV-37 — Error/tooltip surface: `cell.ErrorText`, `row.ErrorText`, `ShowCellErrors`, `ShowRowErrors`, `ShowCellToolTips`, `cell.ToolTipText`, `CellToolTipTextNeeded` result — Cat C/D — P2 — High
- **Ours:** all stored (`DataGridViewCell.cs:200-203`, `DataGridViewRow.cs:109`, `DataGridViewParity.cs:419-428`); renderer never draws an error glyph (`ErrorIconBounds` is computed at `DataGridViewParity.Cell.cs:36-45` but unused); `CellToolTipTextNeeded` is raised on mouse-move and its `ToolTipText` discarded (`DataGridView.cs:2366-2372`); `CellErrorTextChanged`/`RowErrorTextChanged` never raised.
- **Upstream:** `PaintErrorIcon` when `ShowCellErrors && ErrorText != ""` (`…/DataGridViewCell.cs:343`, `:3514-3547`); tooltips shown from `ToolTipText`/`CellToolTipTextNeeded`.
- **Impact:** The validation feedback idiom `row.ErrorText = "Quantity required"` shows nothing; hover tooltips never appear.
- **Fix:** Renderer: draw a 12-px red glyph in `ErrorIconBounds` when `ShowCellErrors`; row header glyph when `ShowRowErrors`; hook the grid's `ToolTip` to `cell.ToolTipText`/`column.ToolTipText`/`CellToolTipTextNeeded.ToolTipText`.
- **Test:** `cell.ErrorText = "x"`; render; probe `ErrorIconBounds` centre for non-background colour.
- **Tests today:** `DataGridViewCellTests.ErrorText_Set_GetReturnsExpected` (store only).

### DGV-38 — `VirtualMode`, `CellValueNeeded`, `CellValuePushed`, `NewRowNeeded`, `RowDirtyStateNeeded`, `CancelRowEdit` — Cat B/D — P2 — High
- **Ours:** `VirtualMode` auto-property (`DataGridView.cs:3121`); the events are `add { } remove { }` or never invoked (434-437, 407, 247-249, `DataGridViewParity.cs:167`).
- **Upstream:** `GetValue`/`SetValue` route through `OnCellValueNeeded`/`OnCellValuePushed` when `VirtualMode` (`…/DataGridViewCell.cs:3927`).
- **Impact:** Virtual-mode grids (large result sets) show empty cells. Documented in the compat matrix; listed because the trigger points (`Cell.Value` get/set, `EndEdit`) exist.
- **Fix:** In `DataGridViewCell.Value` getter/setter and `EndEdit`: when `DataGridView.VirtualMode && RowIndex >= 0` raise `CellValueNeeded`/`CellValuePushed` with a real args object; `RowCount` setter already creates rows.
- **Test:** `VirtualMode = true; RowCount = 3; ColumnCount = 1;` handler returns "v" → `grid[0,0].Value == "v"`.
- **Tests today:** none.

### DGV-39 — `EditingControlShowing` args / `EditingControl` (see DGV-06) and `CellStyle` — Cat A — P2 — High
- **Ours:** `new DataGridViewEditingControlShowingEventArgs(editor, new DataGridViewCellStyle())` — an empty style, not the cell's inherited style (`DataGridView.cs:176`).
- **Upstream:** passes the cell's resolved `DataGridViewCellStyle` (`…/DataGridView.Methods.cs:2684-2691`).
- **Impact:** Handlers that read `e.CellStyle.BackColor`/`Font` to style the editor get empties.
- **Fix:** Pass `Rows[rowIndex].Cells[columnIndex].InheritedStyle`.
- **Test:** `DefaultCellStyle.BackColor = Red`; `BeginEdit` → `e.CellStyle.BackColor == Red`.
- **Tests today:** `DataGridViewBeginEditReentrancyTests` (reentrancy only).

### DGV-40 — `Rows.CollectionChanged` / `Columns.CollectionChanged` never raised — Cat D — P2 — High
- **Ours:** declared (`DataGridViewParity.Cell.cs:325`, `DataGridViewFamilyParity.cs:268` under `CS0067`); `OnCollectionChanged` has no callers; only `DataGridViewCellCollection.AddRange` raises its own (`RemainingMemberParity.cs:657`).
- **Upstream:** raised from `Insert/Remove/Clear` on both collections.
- **Impact:** Code that hooks `grid.Columns.CollectionChanged` to persist layout never fires; low traffic.
- **Fix:** Call `OnCollectionChanged(new CollectionChangeEventArgs(Add/Remove/Refresh, item))` from `InsertItem/RemoveItem/ClearItems` alongside the existing `RaiseRowsAdded/RaiseColumnAdded`.
- **Test:** `Columns.Add(...)` → `CollectionChanged` with `Action == Add`.
- **Tests today:** none.

### DGV-41 — Column-header pointer routing / `ColumnHeaderMouseClick` timing — Cat A — P2 — Medium
- **Ours:** `ColumnHeaderMouseClick` raised in `OnMouseDown` (2244) before sorting; `Y` is relative to the content top not the header cell; `ColumnHeaderMouseDoubleClick` never raised (401).
- **Upstream:** raised on mouse-up after `Sort` (`…/DataGridView.Methods.cs:13636-13665`), so handlers observe the new `SortOrder`.
- **Impact:** Handlers reading `grid.SortOrder` in `ColumnHeaderMouseClick` (already `None`, DGV-16) would still see the *old* order after DGV-16 is fixed unless the ordering is corrected.
- **Fix:** Raise after `OnColumnHeaderClick`, from `OnMouseUp`.
- **Test:** simulate header click → in the handler `SortOrder == Ascending`.
- **Tests today:** none.

## Low-priority / Win32-only (P3) — one line each
- `DataGridViewRow.Frozen`, `Row.DividerHeight`, `Column.DividerWidth` — stored-only; frozen rows and dividers are cosmetic layout extras rarely used.
- `EnableHeadersVisualStyles` — Win32 theme toggle; ours always honours `ColumnHeadersDefaultCellStyle` (the *more* useful behaviour), so no portable divergence worth fixing.
- `DataGridView.this[int,int]` returns `null` out of range where upstream throws `ArgumentOutOfRangeException` — fail-soft vs fail-fast, rarely relied upon.
- `InvalidateCell/InvalidateRow/InvalidateColumn/UpdateCellValue/UpdateCellErrorText/UpdateRowHeightInfo` → whole-control `Invalidate` — correct output, perf only.
- `RowUnshared`, `Rows.SharedRow`, `DataGridViewRow.GetState` — row sharing is a WinForms memory optimisation not modelled here; no portable meaning.
- `EditingPanel => null`, `DataGridViewEditingPanel` — the editing panel is a Win32 hosting detail; `EditingControl` (DGV-06) is the portable half.
- `UserSetCursor => Cursor`, `FirstDisplayedScrollingColumnHiddenWidth => 0` — pixel-partial column scrolling is not modelled; harmless.
- `CellContextMenuStripNeeded`/`RowContextMenuStripNeeded`/`cell.ContextMenuStrip` — never consulted on right-click; grid-level `ContextMenuStrip` works, so only per-cell menus are lost.
- `DataGridViewCell.AccessibilityObject`/`DataGridViewRowAccessibleObject` — accessibility tree is not surfaced to any platform layer.
- `HideSelection` — not a WinForms `DataGridView` member at all (it is `ListView`'s); harmless extra.
- Legacy .NET 1.x `Compat/DataGrid*.cs` (1,618 lines): the same shape of gaps as `DataGridView` — `DataGridTableStyle`/`DataGridColumnStyle` are stored-only, `DataGridBoolColumn.TrueValueChanged` etc. are declared-not-raised, `CurrentCell`/`Select`/`IsSelected` hold state without painting it. Present for compile-and-basic-bind only; low priority given its rarity in modern code.

## Systemic patterns
- **`add { } remove { }` events discard the subscriber** even where the framework already has the trigger point in hand (`OnMouseDown/Up/Move/DoubleClick`, `OnKeyUp`, `EndEdit`, `ListChanged`): `CellMouseDown/Up/Move/DoubleClick`, `RowHeaderMouseClick`, `CellContentDoubleClick`, `ColumnHeaderMouseDoubleClick`, `RowStateChanged`, `CellStateChanged`, `ColumnWidthChanged`, `RowHeightChanged`, `ColumnHeadersHeightChanged`, `RowHeadersWidthChanged`, `ColumnDisplayIndexChanged`, `ColumnSortModeChanged`, `DefaultValuesNeeded`, `UserAddedRow`, `NewRowNeeded`, `CellValueNeeded/Pushed`, `SortCompare`. One sweep: back each with a field + `On*` raiser and call it from the obvious site (`Column.Width` setter → `ColumnWidthChanged`, `Row.Height` setter → `RowHeightChanged`, `ColumnHeadersHeight` setter → `ColumnHeadersHeightChanged`, etc.).
- **Fields declared for events that are never invoked**: `_userDeletingRow`, `_userDeletedRow`, `_dataError`, `_rowDirtyStateNeeded`, `CurrentCellDirtyStateChanged`, `Sorted`, `CellLeave`, `CellEnter`, `CollectionChanged` ×2 — the compiler warning was suppressed instead of the event wired.
- **Row/column/cell objects are passive auto-properties**: `Row.Visible`, `Row.Selected`, `Cell.Selected`, `Cell.Value`, `Cell.ErrorText`, `Column.DisplayIndex`, `Column.SortMode`, `HeaderCell.SortGlyphDirection`, `HeaderCell.Value`, `RowTemplate` change nothing because no setter calls back into the owning grid. Give each a `owner?.DataGridView?.On…` hook the way `Column.Width`/`Row.Height` already do.
- **Renderer reads non-WinForms twins instead of the WinForms property**: `column.DefaultCellStyleAlignment` vs `DefaultCellStyle.Alignment`; `column.SortOrder` vs `HeaderCell.SortGlyphDirection`; `column.Sortable` vs `SortMode`; `SelectedRowIndex` vs `row.Selected`/`cell.Selected`; `HeaderText` vs `HeaderCell.Value`; `Theme.*` vs `GridColor`/`BackgroundColor`/`SelectionBackColor`. Make the twins aliases of the WinForms members and read the WinForms ones.
- **"Store then whole-rebind" data binding**: `OnBoundListChanged` ignores `ListChangedType`; every change is a full column+row regeneration (DGV-31/33), which is also why `RowsAdded` never fires for bound rows and `RowsRemoved` fires spuriously.
- **Editing is a single hard-coded `TextBox`**: no per-column editor type, no dirty flag, no `ParseFormattedValue`, no `DataError` — the entire editing surface (`BeginEdit(bool)`, `EditMode`, `EditingControl`, `IsCurrentCellDirty`, `NotifyCurrentCellDirty`, `RefreshEdit`, `CommitEdit`) is stubbed around it.
- **Divergent defaults are codified by existing tests** (`Ctor_Default`, `Height_SetDefault_IsTwentyFive`, `*_ClampsToMinimum`, `NewRowIndex_ReflectsAllowUserToAddRows`, `ClearSelection_ResetsCurrentRowAndCell`, `CellParsing_NotHandled_StoresTheEditedTextAsBefore`, `GetClipboardContent_UsesTheFormattedValue`) — the fixer must update these alongside the code.
