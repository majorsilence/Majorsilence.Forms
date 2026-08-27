# List-style controls — findings

## Summary

The list family splits cleanly in two. `ListBox`/`ComboBox` have a real selection model, scrolling and data binding, but their *event plumbing* has holes: every selection path except the `SelectedIndex` setter (SelectedItem=, SetSelected, ClearSelected, multi-select mouse/keyboard) bypasses `OnSelectedIndexChanged`, and `ComboBox` has no editable text region at all despite `DropDown` being the default style. `CheckedListBox` is a `ListBox` with check-state bookkeeping and nothing else — no checkbox is drawn and no click toggles one. `ListView` and `TreeView` are the weaker half: `ListView` renders every item as a 70px large-icon tile regardless of `View`, never scrolls, and drops handlers for `ColumnClick`/`ItemActivate`/`ItemCheck` (`add { } remove { }`); `TreeView` reports a synthetic root node as `SelectedNode` when nothing is selected, has a public `GetNodeAt` built on a fake layout that disagrees with the one the mouse uses, never raises `NodeMouseClick` for the right button, and ignores `CheckBoxes`, `ImageList`/`ImageIndex`, `NodeFont`/`ForeColor`, `Sorted`, `Indent` and `ItemHeight`. The dominant patterns are (1) selection mutated through the item collection's internal setters instead of the control's event-raising path, (2) `add { } remove { }` events on the exact hooks LOB apps subscribe to, (3) renderers that read their own private knobs (`ShowDropdownGlyph`, `INDENT_SIZE`, `GetPreferredSize`) while the WinForms-named property (`ShowPlusMinus`, `Indent`, `ItemHeight`) is a stored-only sibling, and (4) hit-testing that compares logical mouse coordinates with device-pixel bounds everywhere except `ListBox`, which was already fixed for exactly this.

Counts: **P0: 5 · P1: 23 · P2: 13 · P3: listed in one line each.**

## Findings

### LST-01 — `ListView.View` (Details/List/SmallIcon/Tile) — Cat B — P0 — High
- **Ours:** `View` is an auto-property (`src/Majorsilence.Forms/ListView.cs:131`). `LayoutItems` always lays items out as 70×70 tiles (`ListView.cs:44-62`) and `ListViewRenderer.RenderItem` always paints a large icon with centred text beneath (`src/Majorsilence.Forms/Renderers/ListViewRenderer.cs:20-45`). Nothing reads `Columns`, `ColumnHeader.Width`/`TextAlign`, `SubItems[i>0]`, `FullRowSelect`, `GridLines`, `HeaderStyle` or `CheckBoxes` at paint time (grep of `Renderers/` for `View.Details|Columns|GridLines|FullRowSelect` finds only DataGridView). `ColumnHeader.Width = -1/-2` (autosize) is stored verbatim (`ListView.cs:344`).
- **Upstream:** `View` selects the native LVS_* style; Details draws a header and one row per item with subitems in column cells (`src/System.Windows.Forms/System/Windows/Forms/Controls/ListView/ListView.cs:1803`); `ColumnHeader.Width` of -1/-2 autosizes to content/header (`ColumnHeader.cs:384-408`, via `SetColumnWidth`).
- **Impact:** Every `View = View.Details` list — the most common ListView shape in LOB apps — renders as a grid of icon tiles showing only column 0 text; all subitem data is invisible; no header.
- **Fix:** In `ListViewRenderer.Render`, branch on `control.View`: for Details draw a header row from `Columns` (honouring `Width`, `-1`/`-2` via `AutoResizeColumn`, `TextAlign`), then one `ItemHeight` row per item drawing `SubItems[i].Text` at column offsets; `List`/`SmallIcon` as single-line rows. Move `LayoutItems` out of `OnPaint` into a view-aware layout so `item.Bounds` matches. Set `ListViewSubItem.Bounds` while laying out.
- **Test:** Headless render of a 2-column Details list; assert pixels at the second column's x-offset are non-background and `Items[0].SubItems[1].Bounds.X >= Columns[0].Width`.
- **Tests today:** `ListViewParityTests.cs` (only `ImageList_follows_the_view_mode`), `ListViewTests.cs` — none render Details.

### LST-02 — `CheckedListBox` (checkbox glyph, click toggling, `CheckOnClick`) — Cat B — P0 — High
- **Ours:** File header says "visual only stub" (`src/Majorsilence.Forms/CheckedListBox.cs:9`). `CheckOnClick` is stored (`CheckedListBox.cs:38`); the class has no mouse/key override; `RenderManager` routes it to `ListBoxRenderer` (`src/Majorsilence.Forms/Renderers/RenderManager.cs:19`), which draws no glyph (`Renderers/ListBoxRenderer.cs:32-53`). Only `SetItemChecked`/`SetItemCheckState` change state, and they do raise `ItemCheck` first with a cancellable `NewValue` (`CheckedListBox.cs:59-79`).
- **Upstream:** `LbnSelChange` toggles on the second click of a selected item, or every click when `CheckOnClick` (`ListBoxes/CheckedListBox.cs:400-430`); space via `OnKeyPress` (`:777-781`); `OnDrawItem` paints the box (`:452+`).
- **Impact:** A migrated permissions/filter/options dialog shows a plain list; the user cannot tick anything; `CheckedItems` is always empty unless code pre-checks.
- **Fix:** Add a `CheckedListBoxRenderer` (or a `CheckedListBox` branch in `ListBoxRenderer.RenderItem`) that draws `ControlPaint.DrawCheckBox` at the row's left using `GetItemCheckState (index)`; override `OnMouseButtonLogic`/`OnKeyUp` to toggle via `SetItemCheckState` when `CheckOnClick` or the index was already selected, or the click lands in the glyph rectangle, or Space is pressed.
- **Test:** Headless render with one checked item; assert glyph pixels at `GetItemRectangle (0).Left + 4`. Simulate `OnMouseDown` on row 1 with `CheckOnClick = true`; assert `GetItemChecked (1)` and one `ItemCheck` raise.
- **Tests today:** `CheckedListBoxTests.cs` (state round-trips and `ItemCheck` from `SetItemCheckState` only).

### LST-03 — `ListBox.SelectedItem` / `ComboBox.SelectedItem` setter — Cat D — P0 — High
- **Ours:** `ListBox.SelectedItem` set forwards to `Items.SelectedItem` (`src/Majorsilence.Forms/ListBox.cs:553-556`), whose setter assigns the collection's *internal* `SelectedIndex` (`src/Majorsilence.Forms/ListBoxItemCollection.cs:193-211` → `:181-189` → `AddSelectedIndex :65-76`). That path never calls `ListBox.OnSelectedIndexChanged` nor `source_tracker.OnSelectionChanged`. `ComboBox.SelectedItem` forwards to the popup list's `SelectedItem` (`src/Majorsilence.Forms/ComboBox.cs:364-367`) so the combo's `SelectedIndexChanged`/`SelectedItemChanged`/`SelectedValueChanged` also stay silent.
- **Upstream:** `ListBox.SelectedItem` setter assigns `SelectedIndex = index` and therefore raises (`ListBoxes/ListBox.cs:935-960`); `ComboBox.SelectedItem` does the same (`ComboBox/ComboBox.cs:912-935`).
- **Impact:** `cboCustomer.SelectedItem = customer;` no longer runs the `SelectedIndexChanged` handler that loads the detail panel; a bound `BindingSource` position is not moved. This is the single most common way LOB code selects programmatically.
- **Fix:** In `ListBoxItemCollection.SelectedItem` set, resolve the index and assign `owner.SelectedIndex = index` (the public, event-raising setter) instead of the internal one; do the same for `value is null` (`owner.SelectedIndex = -1`).
- **Test:** Subscribe to `SelectedIndexChanged` and `SelectedValueChanged`; set `SelectedItem = Items[1]`; assert one raise each and `SelectedIndex == 1`.
- **Tests today:** `ListBoxTests.cs:245-290`, `ComboBoxTests.cs:220-260` assert state only, never the event.

### LST-04 — `ListBox.SetSelected` / `ClearSelected` / multi-select mouse & keyboard — Cat D — P0 — High
- **Ours:** `ClearSelected` clears `Items.SelectedIndexes` and invalidates (`ListBox.cs:686-690`); `SetSelected` calls `AddSelectedIndex`/`RemoveSelectedIndex` (`:693-698`); `MultiSimple`/Ctrl-click toggles go through `Items.ToggleSelectedIndex` (`:416-427`), Space (`:277-281`) and Shift+arrow (`:299,306,315,324,343`) through `AddSelectedIndex`/`RemoveSelectedIndex`; `SelectionMode = None` clears via the internal setter (`:580-583`). None of these call `OnSelectedIndexChanged`. `SetSelected` also swallows out-of-range and `SelectionMode.None` silently (`:695`).
- **Upstream:** `ClearSelected` raises when anything was selected (`ListBoxes/ListBox.cs:1320-1341`); `SetSelected` always raises and throws for out-of-range/`None` (`:2140-2160`); every native selection change arrives as LBN_SELCHANGE → `OnSelectedIndexChanged`.
- **Impact:** In a multi-select list the "N items selected" label, the enabled state of Delete/Move buttons, and any `SelectedItems`-driven detail view never update from user clicks.
- **Fix:** Route `SetSelected`, `ClearSelected`, `ToggleSelectedIndex`, the multi-select keyboard branches and the `SelectionMode` setter through one private `ChangeSelection (Action mutate)` that snapshots `SelectedIndexes`, applies the mutation, and calls `OnSelectedIndexChanged` + `source_tracker.OnSelectionChanged` when the set changed. Throw `ArgumentOutOfRangeException`/`InvalidOperationException` in `SetSelected` as upstream.
- **Test:** `SelectionMode = MultiSimple`; count `SelectedIndexChanged`; `SetSelected (0, true)`, `SetSelected (2, true)`, `ClearSelected ()` → 3 raises. Simulate two `OnMouseDown`s at rows 0 and 2 → 2 raises.
- **Tests today:** `ListBoxTests.cs:335-376` (state only).

### LST-05 — `TreeView.SelectedNode` (null semantics) — Cat A — P0 — High
- **Ours:** The constructor sets `selected_item = root_item`, the hidden synthetic root (`src/Majorsilence.Forms/TreeView.cs:32-36`). `SelectedNode` get returns `SelectedItem` unfiltered (`TreeView.cs:318-320`), so with nothing selected it returns a non-null node with `Text == ""` whose `Nodes` are the tree's own top-level nodes. `SelectedNode = null` is ignored (`:320`); `SelectedItem` set also returns early on null (`:755-760`). After `Nodes.Clear ()` or `node.Remove ()` the stale node is still returned.
- **Upstream:** Returns null when no node is selected; assigning null clears the caret (`TreeView/TreeView.cs:1084-1128`).
- **Impact:** `if (tree.SelectedNode == null) return;` guards never fire; code reads `SelectedNode.Tag` (null) or `SelectedNode.Text` ("") and proceeds; `tree.SelectedNode = null` to clear a selection does nothing; `SelectedNode.Nodes.Add (...)` adds a top-level node.
- **Fix:** `SelectedNode` get: `selected_item == root_item || selected_item.TreeView != this ? null : selected_item`. Set: allow null by assigning `selected_item = root_item` and raising `AfterSelect` with a null node as upstream does (TVN_SELCHANGED with `hItem == 0` skips `OnAfterSelect`; mirror that — no raise for null). Reset to `root_item` in `TreeViewItemCollection.ClearItems`/`RemoveItem` when the removed subtree contains `selected_item`.
- **Test:** `new TreeView ().SelectedNode` is null; add nodes, select one, set `SelectedNode = null`, assert null; remove the selected node, assert null.
- **Tests today:** `TreeViewTests.cs` — no null-selection assertion.

### LST-06 — `ComboBox.SelectedIndex = -1` (SelectedIndexChanged not raised) — Cat A — P1 — High
- **Ours:** The popup list raises, but `ListBox_SelectedIndexChanged` guards the combo's raise with `if (popup_listbox.SelectedIndex > -1)` (`ComboBox.cs:246`), so clearing the selection raises nothing on the combo and does not move the bound source.
- **Upstream:** Any change, including to -1, runs `OnSelectedItemChanged`/`OnSelectedIndexChanged` (`ComboBox/ComboBox.cs:870-901`).
- **Impact:** A "Clear filter" button that sets `SelectedIndex = -1` leaves dependent controls showing the old choice.
- **Fix:** Drop the `> -1` guard; keep only the `DroppedDown`-close and `userDriven` logic inside an `if (index > -1)`.
- **Test:** Select 2, subscribe, set -1; assert one `SelectedIndexChanged` raise and `Text == ""`.
- **Tests today:** `ComboBoxTests.cs:387-397` asserts *no* raise when -1 is set on an already-empty combo (correct) — nothing for the transition case.

### LST-07 — `ComboBox` editable region (`DropDownStyle` DropDown/Simple, `SelectionStart/Length/SelectedText`, `Select`, `SelectAll`, `MaxLength`, `AutoComplete*`) — Cat B — P1 — High
- **Ours:** No `OnKeyPress`/text input anywhere in `ComboBox.cs`; `SelectAll` is an empty stub (`src/Majorsilence.Forms/ComboBox.SelectAll.cs:6`); `SelectionStart`/`SelectionLength` are stored ints (`ComboBox.cs:425-428`), `SelectedText` set is `{ }` (`:433`), `Select` stores (`:443`), `MaxLength`/`AutoCompleteMode`/`AutoCompleteSource`/`AutoCompleteCustomSource` stored (`:437,452-458`). `DropDownStyle` only raises its own changed event and invalidates (`:92-102`); the renderer paints the selected item's text only (`Renderers/ComboBoxRenderer.cs:26-28`). `Simple` (always-visible list) is not laid out.
- **Upstream:** `DropDown` (the default) and `Simple` host a child edit control; typing raises `TextUpdate`/`TextChanged`, Enter commits via `FindStringIgnoreCase` (`ComboBox/ComboBox.cs:1047-1082, 2456-2480, 951-990`).
- **Impact:** Any combo used for free-text entry with suggestions (search boxes, "Other..." fields, units) cannot be typed into; `DropDownStyle = DropDownList` vs `DropDown` look and behave identically.
- **Fix:** Host an internal `TextBox` over `GetTextArea` when `DropDownStyle != DropDownList`; forward `SelectionStart/Length/SelectedText/Select/SelectAll/MaxLength` to it; on its `TextChanged` raise `OnTextUpdate` then `OnTextChanged`; on Enter run the upstream `Text` setter logic. For `Simple`, dock the popup list below the text area inside the control.
- **Test:** `DropDownStyle = DropDown`; feed `OnKeyPress ('a')`; assert `Text == "a"` and `TextUpdate` raised once; `SelectAll ()` then `SelectionLength == 1`.
- **Tests today:** none (`ComboBox.SelectAll/0` is in `NoOpStubBaseline.txt`).

### LST-08 — `ComboBox.Text` setter/getter — Cat A — P1 — High
- **Ours:** Getter returns `GetItemText (SelectedItem)` whenever `SelectedIndex >= 0`, else `base.Text` (`ComboBox.cs:522-527`). Setter: exact match → `SelectedIndex = idx`, else `base.Text = value` (`:528-534`). So `Text = "custom"` on a combo with a selection stores the string but reads back the selected item's text; `Text = null` does not clear the selection (upstream does).
- **Upstream:** Setter sets `base.Text`, then `null` → `SelectedIndex = -1`, otherwise a case-insensitive match selects and a miss leaves the index alone but keeps the new text; getter returns `base.Text` unless a `DisplayMember` binding is in play (`ComboBox/ComboBox.cs:1083-1130`).
- **Impact:** Restoring a saved free-text value into a `DropDown` combo silently shows the previously selected item; `Text = null` (the documented "clear" idiom) leaves the selection.
- **Fix:** Mirror upstream: always `base.Text = value ?? ""`; `null` → `SelectedIndex = -1`; on a miss keep the text and do not touch the index; make the getter return `base.Text`, and have the selection path (LST-09) write `base.Text` so the two stay in step.
- **Test:** Select 1; `Text = "zzz"`; assert `Text == "zzz"` and `SelectedIndex` unchanged. `Text = null` → `SelectedIndex == -1`.
- **Tests today:** `ComboBoxTests.cs` has no `Text` cases.

### LST-09 — `ComboBox.TextChanged` on selection change — Cat D — P1 — High
- **Ours:** Nothing writes `base.Text` when the selection changes (`ComboBox.cs:356-359, 240-262`), and `Control.Text` is the only thing that raises `OnTextChanged` (`src/Majorsilence.Forms/Control.cs:2609-2624`). The computed getter hides the fact that `TextChanged` never fires for a combo.
- **Upstream:** `SelectedIndex` set → `UpdateText ()` → `Text = s` → `OnTextChanged` (`ComboBox/ComboBox.cs:870-901, 3396-3420`); CBN_SELCHANGE does the same (`:3557-3560`).
- **Impact:** Validation and dirty-tracking wired to `TextChanged` (very common on combos in data-entry forms, and what `Binding` on `Text` listens to) never runs.
- **Fix:** In `ListBox_SelectedIndexChanged`, set `base.Text = SelectedIndex >= 0 ? GetItemText (SelectedItem) : ""` before raising `OnSelectedIndexChanged`.
- **Test:** Subscribe `TextChanged`; `SelectedIndex = 2`; assert one raise and `Text == Items[2].ToString ()`.
- **Tests today:** none.

### LST-10 — `ListBox.Sorted` — Cat C — P1 — High
- **Ours:** Auto-property (`ListBox.cs:644`); no sort on set, no sorted insert in `ListBoxItemCollection.Add` (`ListBoxItemCollection.cs:38-42`). `ComboBox.Sorted` does sort on set (`ComboBox.cs:383-419`) but its `Items.Add` still appends unsorted afterwards.
- **Upstream:** Setting `Sorted = true` sorts (`ListBoxes/ListBox.cs:1029-1045`, `Sort :2171`); `ObjectCollection.AddInternal` binary-searches the insert position while sorted (`ListBox.ObjectCollection.cs:93-121`).
- **Impact:** Designer-set `Sorted = true` lists (countries, users, categories) appear in insertion order; items added after `Sorted = true` on a combo land at the bottom.
- **Fix:** In `ListBoxItemCollection.InsertItem`, when `owner.Sorted` compute the insert index by `string.Compare (owner.GetItemText (a), owner.GetItemText (b), CurrentCulture)` and insert there (adjust `SelectedIndexes` as the Add path already does); in `ListBox.Sorted` set, re-add items in order when turning on, preserving selection like `ComboBox.SortItems`.
- **Test:** `Sorted = true`; add "b","a","c"; assert `Items` is a,b,c; then `Sorted = true` on a populated unsorted list sorts it.
- **Tests today:** `ComboBoxTests.cs:340-363` (combo only, set-time only).

### LST-11 — `TreeView.Sorted` / `TreeViewNodeSorter` — Cat C — P1 — High
- **Ours:** `Sorted` stored (`src/Majorsilence.Forms/MidSizeControlParity.Two.cs:126`), `TreeViewNodeSorter` stored (`TreeView.cs:333`), `Sort ()` empty (`:336`, in `NoOpStubBaseline.txt`). Listed anyway because the *property* is the silent half: designer serialises `Sorted = true`.
- **Upstream:** `Sorted = true` refreshes nodes in sorted order and sorted inserts follow (`TreeView/TreeView.cs:1226-1240`, `Sort :2385-2389`).
- **Impact:** Folder/category trees appear in load order.
- **Fix:** In `TreeViewItemCollection.InsertItem`, when `owner.TreeView?.Sorted == true` or a `TreeViewNodeSorter` is set, insert at the comparer's position; implement `Sort ()` as a recursive stable sort using `TreeViewNodeSorter ?? Comparer<TreeNode>(Text, CurrentCulture)`.
- **Test:** `Sorted = true`; `Nodes.Add ("b"); Nodes.Add ("a")`; assert `Nodes[0].Text == "a"`.
- **Tests today:** none.

### LST-12 — `ListView.Sort` / `ListViewItemSorter` / `Sorting` — Cat B — P1 — High
- **Ours:** `Sort ()` is `Invalidate ()` (`ListView.cs:290`) — not empty, so the no-op scanner misses it; `ListViewItemSorter` and `Sorting` are stored (`:287, :155`).
- **Upstream:** Setting `ListViewItemSorter` sorts immediately (`ListView/ListView.cs:1253-1270`); `Sort ()` applies the comparer (`:5571-5585`).
- **Impact:** The canonical `ColumnClick → ListViewItemSorter = new Comparer(col); Sort ()` pattern does nothing (and `ColumnClick` never fires — LST-18).
- **Fix:** `Sort ()`: if `ListViewItemSorter != null` stable-sort `Items` in place via the comparer (reuse the `Rows.RemoveAt/Insert` technique in `OverloadParity.cs:150-166`); else if `Sorting != None` sort by `Text`. Call `Sort ()` from the `ListViewItemSorter` and `Sorting` setters.
- **Test:** Add "b","a"; `ListViewItemSorter = Comparer<ListViewItem>(Text)`; assert `Items[0].Text == "a"`.
- **Tests today:** `ListViewTests.cs:44` (default null only).

### LST-13 — `ListBox`/`ComboBox` `DrawMode` / `DrawItem` / `MeasureItem` — Cat B — P1 — High
- **Ours:** `DrawMode` stored (`ListBox.cs:647`, `ComboBox.cs:538`); `MeasureItem` is `add { } remove { }` (`ListBox.cs:742`, `ComboBox.cs:553`) so handlers are dropped; `DrawItem` is real but `ListBoxRenderer` never calls `OnDrawItem` (`Renderers/ListBoxRenderer.cs:32-53`). `GetItemHeight` always returns `ItemHeight` (`MidSizeControlParity.Three.cs:243-247`).
- **Upstream:** WM_DRAWITEM/WM_MEASUREITEM reflect to `OnDrawItem`/`OnMeasureItem` when `DrawMode != Normal` (`ListBoxes/ListBox.cs:2348-2400`, `ComboBox/ComboBox.cs:3574-3608`).
- **Impact:** Owner-drawn lists (coloured rows, icons+text, font-name pickers) paint as plain text; variable-height items collapse to one height.
- **Fix:** In `ListBoxRenderer.RenderItem`, when `control.DrawMode != Normal` build a `DrawItemEventArgs` (Graphics over `e.Canvas`, bounds, index, state) and call an `internal RaiseDrawItem`; return without default painting. For `OwnerDrawVariable`, call `OnMeasureItem` per index when computing row heights and store per-item heights for `GetItemRectangle`/`GetItemHeight`. Make `MeasureItem` a real event.
- **Test:** `DrawMode = OwnerDrawFixed`, subscribe `DrawItem`, headless render; assert handler called once per visible item with the row rectangle.
- **Tests today:** none.

### LST-14 — `ListBox.FindString` / `FindStringExact` ignore `DisplayMember` — Cat A — P1 — High
- **Ours:** Both compare against `Items[current]?.ToString ()` (`ListBox.cs:96`, `:726`). `ComboBox.FindString*` correctly use `GetItemText` (`ComboBox.cs:509-519, 556-567`), so the two controls disagree.
- **Upstream:** `FindStringInternal` compares `GetItemText (items[index])` (`ListControl/ListControl.cs:499-503`).
- **Impact:** Type-ahead (`OnKeyUp` letter search, `ListBox.cs:381-390`) and `FindStringExact` on a bound list of `Customer` objects never match, because they compare against `Customer.ToString ()`.
- **Fix:** Replace both `ToString ()` calls with `GetItemText (Items[current])`.
- **Test:** Bind a list of objects with `DisplayMember = "Name"`; `FindStringExact ("Bob") == 1`.
- **Tests today:** `ListBoxTests.cs` — plain-string cases only.

### LST-15 — `ListBox.IndexFromPoint` / `GetItemRectangle` coordinate space & scroll — Cat A — P1 — High
- **Ours:** `IndexFromPoint (x, y)` returns `y / ItemHeight` in logical units, ignoring `top_index` (scroll), the client-rect origin and the scrollbar (`MidSizeControlParity.Three.cs:250-260`). The library's own `GetIndexAtLocation` does it right (converts to device and starts at `top_index`, `ListBox.cs:130-143`). `GetItemRectangle` returns *device* pixels (`ListBox.cs:149-160`, comment at `:132-135`) while `MouseEventArgs` are logical (`tests/Majorsilence.Forms.Tests/ListBoxHitTestTests.cs:8-17`).
- **Upstream:** `IndexFromPoint` uses LB_ITEMFROMPOINT, which accounts for scroll (`ListBoxes/ListBox.cs:1500-1520`); `GetItemRectangle` and mouse coordinates share one client-pixel space (`:1440-1450`).
- **Impact:** Right-click context menus and drag-drop code (`IndexFromPoint (e.Location)` in `MouseDown`) pick the wrong row as soon as the list is scrolled; `GetItemRectangle (i).Contains (e.Location)` fails at scale ≠ 1.
- **Fix:** `IndexFromPoint (x, y) => GetIndexAtLocation (new Point (x, y))`. Return `GetItemRectangle` in logical units (divide by scale) and have the renderer call an internal device-space variant.
- **Test:** 20 items, `TopIndex = 10`; `IndexFromPoint (5, ItemHeight / 2) == 10`.
- **Tests today:** `ListBoxHitTestTests.cs` covers `GetIndexAtLocation` only.

### LST-16 — `CheckedListBox.SelectedItem` / `SelectedItems` return the wrapper — Cat A — P1 — High
- **Ours:** Items are stored as `CheckedListBoxItem` wrappers (`CheckedListBox.cs:16-32, 164-169`). The public `Items` indexer unwraps (`:175-178`), but `ListBox.SelectedItem`/`SelectedItems` read `base.Items` directly (`ListBox.cs:553-564` → `ListBoxItemCollection.cs:193-213`) and return the wrapper; `SelectedItem = originalValue` does `IndexOf` over wrappers, finds nothing, and is silently ignored (`ListBoxItemCollection.cs:206-209`).
- **Upstream:** `SelectedItem` is the object the caller added (`ListBoxes/ListBox.cs:935-960`; `CheckedListBox` does not override it).
- **Impact:** `(Role)clb.SelectedItem` throws `InvalidCastException`; `clb.SelectedItem = role` never selects.
- **Fix:** Override `SelectedItem` (and the `SelectedItems` view) in `CheckedListBox` to unwrap `CheckedListBoxItem.Value`, and map the setter through `Items.IndexOf (value)` (the unwrapping indexer) to `SelectedIndex`.
- **Test:** `Items.Add (obj)`; `SelectedIndex = 0`; `Assert.Same (obj, SelectedItem)`; `SelectedItem = obj` on a fresh list selects it.
- **Tests today:** `CheckedListBoxTests.cs` — none for `SelectedItem`.

### LST-17 — `ListViewItem.Selected` / `ListView.MultiSelect` / `FocusedItem` — Cat D — P1 — High
- **Ours:** `Selected` is an auto-property (`src/Majorsilence.Forms/ListViewItem.cs:116`). Only `ListView.SelectedItem` set raises `ItemSelectionChanged`/`SelectedIndexChanged` (`ListView.cs:98-128`); `SelectedItems.Clear ()`, `SelectedIndices.Add`, and `item.Selected = true` (`ListViewParity.cs:299-351`) are silent. `MultiSelect` is stored (`ListView.cs:140`) — mouse always single-selects (`:65-72`) and `Selected = true` on several items with `MultiSelect = false` all stick. `FocusedItem` is never set by a click (`:272`).
- **Upstream:** `Selected` set goes through `SetItemState`, which produces LVN_ITEMCHANGED → `OnItemSelectionChanged` + `OnSelectedIndexChanged` (`ListView/ListViewItem.cs:681-712`, `ListView.cs:6610-6690`); `MultiSelect` toggles LVS_SINGLESEL (`:1279-1295`).
- **Impact:** `listView.Items[i].Selected = true` (the standard way to select programmatically) updates no dependent UI; Ctrl/Shift-click multi-select is impossible; `FocusedItem` is always null.
- **Fix:** Give `ListViewItem.Selected` a backing field whose setter calls `Parent?.OnItemSelectedChanged (this, value)`; centralise raising there (deselect others when `!MultiSelect`), and have `SelectedItem` set / `OnMouseClick` use it. Set `FocusedItem` in `OnMouseClick`. Honour Ctrl/Shift in `OnMouseClick` when `MultiSelect`.
- **Test:** Subscribe `SelectedIndexChanged`; `Items[1].Selected = true` → 1 raise, `SelectedItems.Count == 1`; with `MultiSelect = false` select two → `SelectedItems.Count == 1`.
- **Tests today:** `ListViewTests.cs:223-263`, `ListViewParityTests.cs:32-58` assert collection contents only.

### LST-18 — `ListView` never-raised, handler-dropping events (`ColumnClick`, `ItemActivate`, `ItemCheck`, `ItemChecked`, `BeforeLabelEdit`, `AfterLabelEdit`, `ItemDrag`) — Cat D — P1 — High
- **Ours:** All declared `add { } remove { }` (`ListView.cs:180, 202, 205, 208, 211, 214, 218`). `OnDoubleClick` raises only the library's `ItemDoubleClicked` (`:75-83`). `ListViewItem.Checked` is an auto-property (`ListViewItem.cs:119`); `CheckBoxes` stored (`ListView.cs:143`), nothing draws a box.
- **Upstream:** LVN_COLUMNCLICK → `OnColumnClick` (`ListView/ListView.cs:6503`), LVN_ITEMACTIVATE → `OnItemActivate` (`:6555-6557`), state change → `OnItemCheck` (before, cancellable) / `OnItemChecked` (`:4789-4796, 6610-6630`); `ListViewItem.Checked` set routes through the state image (`ListViewItem.cs:324-350`).
- **Impact:** Column-click sorting, double-click-to-open (`ItemActivate`), and check-box lists (`ItemChecked` → enable "Apply") are all dead; worse, `+=` compiles and silently discards the delegate.
- **Fix:** Make each a real event with `On*` raisers. Raise `ItemActivate` from `OnDoubleClick` (and Enter) alongside `ItemDoubleClicked`; raise `ColumnClick` from a header hit-test in `OnMouseClick` once LST-01 draws a header; give `ListViewItem.Checked` a setter that calls `Parent?.RaiseItemCheck` (cancellable, `NewValue` honoured) then `ItemChecked`; draw and hit-test a checkbox when `CheckBoxes`.
- **Test:** Subscribe `ItemActivate`; call `OnDoubleClick` over `Items[0].Bounds` → 1 raise. Subscribe `ItemCheck` and set `e.NewValue = Unchecked`; `Items[0].Checked = true` → stays false.
- **Tests today:** `ListViewTests.cs:280-305` (`Checked` state only).

### LST-19 — `ListView` vertical scrolling (`EnsureVisible`, `TopItem`, `Scrollable`) — Cat B — P1 — High
- **Ours:** No scrollbar is created (`ListView.cs:17-21`, contrast `ListBox.cs:40-51`); `EnsureVisible (index) => Invalidate ()` (`:252`); `TopItem` get is `Items[0]`, set is `EnsureVisible` (`ListViewParity.cs:74-77`); `Scrollable` stored (`:234`). `LayoutItems` wraps tiles downward past the client bottom (`:44-62`), and there is no `OnMouseWheel`.
- **Upstream:** `EnsureVisible` scrolls (`ListView/ListView.cs:3189`); `TopItem` reflects/sets the scroll position (`:1710`).
- **Impact:** Any list taller than the control is truncated with no way to reach the rest; `EnsureVisible (Items.Count - 1)` after appending a log line does nothing.
- **Fix:** Add an implicit `VerticalScrollBar` as `ListBox`/`TreeView` do, compute `top_index` from it, offset layout by it, implement `EnsureVisible`/`TopItem` against it, forward `OnMouseWheel`.
- **Test:** 100 items in a 100px-tall Details list; `EnsureVisible (99)`; assert `TopItem != Items[0]` and `Items[99].Bounds.Bottom <= ClientRectangle.Bottom`.
- **Tests today:** none.

### LST-20 — `ListView` / `TreeView` mouse hit-testing mixes logical and device coordinates — Cat A — P1 — Medium
- **Ours:** `ListView.OnMouseClick`/`OnDoubleClick`/`GetItemAt`/`HitTest` test `e.Location` (logical, per `ListBoxHitTestTests.cs:8-17` and `ListBox.cs:132-135`) against `item.Bounds`, which `LayoutItems` builds from `PaddedClientRectangle` and `LogicalToDeviceUnits (70)` (device) (`ListView.cs:44-62, 69, 79, 255`; `ListViewParity.cs:169-184`). `TreeView.GetItemAtLocation` does the same against bounds laid out over `ClientRectangle` with `GetPreferredSize` in device units (`TreeView.cs:173-181, 423-448`; `TreeViewItem.cs:174-180`). `ListBox` alone converts (`ListBox.cs:136`).
- **Upstream:** one coordinate space (client pixels) for both.
- **Impact:** At scale 2 (Retina) a click on the third row of a TreeView/ListView selects the item at half that offset; `GetItemAt (e.X, e.Y)` returns the wrong item. Rated Medium because it needs a scaled runtime check; the ListBox fix history says this was real there.
- **Fix:** Convert once at the top of `ListView.OnMouseClick/OnDoubleClick/GetItemAt/HitTest` and `TreeView.GetItemAtLocation` via `LogicalToDeviceUnits`, exactly as `ListBox.GetIndexAtLocation` does; or lay out in logical units and let renderers scale.
- **Test:** Reuse the `ListBoxHitTestTests` pattern: headless render a TreeView at scale 2, click `RowCentre (2)` in logical units, assert `SelectedNode == Nodes[2]`.
- **Tests today:** none for ListView/TreeView.

### LST-21 — `TreeView.GetNodeAt` / `HitTest` use a fake layout — Cat A — P1 — High
- **Ours:** `GetNodeAt` walks `GetAllItems ()`, a `Stack`-based DFS that yields nodes in *reverse* sibling order (`TreeView.cs:296-306`), and `GetItemBounds` returns `new Rectangle (0, index * ItemHeight, Width, ItemHeight)` using the stored `ItemHeight` (20, `:249`) rather than the real `ScaledItemHeight` (`:750`), ignoring `top_index` and the client origin (`:308-315`). The library's own `GetItemAtLocation` uses the real `_layoutItems` bounds (`:173-181`). `HitTest` (`MidSizeControlParity.Two.cs:158-177`) is built on `GetNodeAt`.
- **Upstream:** `GetNodeAt` is TVM_HITTEST against the actual layout (`TreeView/TreeView.cs:1671`).
- **Impact:** `tree.GetNodeAt (e.X, e.Y)` in `MouseDown`/`MouseUp` (the canonical right-click-select and drag-drop pattern) returns the wrong node — different from the one the tree itself selected on the same click.
- **Fix:** `GetNodeAt (pt) => GetItemAtLocation (pt)` (after LST-20's conversion), running `LayoutItems ()` first if `_layoutItems` is empty. Delete `GetAllItems`/`GetItemBounds`.
- **Test:** Three root nodes, headless render; assert `GetNodeAt (RowCentre (1)) == Nodes[1]` and equals `GetItemAtLocation` for the same point.
- **Tests today:** none.

### LST-22 — `TreeView.NodeMouseClick` not raised for the right button — Cat A — P1 — High
- **Ours:** `OnMouseClick` returns before the raise when `e.Button == Right` (`TreeView.cs:474-483`); the raise only happens on the left-button label path (`:495-498`).
- **Upstream:** WM_LBUTTONUP and WM_RBUTTONUP both raise `OnNodeMouseClick` with `_downButton` (`TreeView/TreeView.cs:3290-3311`).
- **Impact:** The standard `NodeMouseClick += (s, e) => { if (e.Button == Right) { tree.SelectedNode = e.Node; menu.Show (...); } }` never runs.
- **Fix:** Raise `NodeMouseClick` for any button when the click is on a node (not the glyph) before the `ContextMenu` shortcut; keep right-click from changing selection (upstream does not).
- **Test:** Simulate `OnMouseClick` with `MouseButtons.Right` over `Nodes[0]`; assert one `NodeMouseClick` with `e.Button == Right`.
- **Tests today:** `TreeNodeCompatTests.cs:51` (args ctor only).

### LST-23 — `TreeView` selection/expand/collapse event set (`BeforeSelect`, `BeforeCollapse`, `AfterExpand`/`AfterCollapse` on programmatic paths, `TreeViewAction`) — Cat D — P1 — High
- **Ours:** `BeforeSelect`, `BeforeCollapse`, `AfterCheck`, `BeforeCheck`, `BeforeLabelEdit`/`AfterLabelEdit`, `NodeMouseHover`, `ItemDrag` are `add { } remove { }` (`TreeView.cs:210-237`). `AfterExpand`/`AfterCollapse` are raised only from `OnMouseClick`/`OnDoubleClick` (`:494, 535, 501-511`); `TreeNode.Expand ()`, `Collapse ()`, `Toggle ()`, `ExpandAll ()`, `CollapseAll ()` and keyboard Left/Right (`:690-703`) raise nothing after the fact (`TreeViewItem.cs:68-75, 108-123`). `BeforeExpand` is raised from `Expand ()` (`:110`) but `BeforeCollapse` never. `AfterSelect` always reports `TreeViewAction.ByMouse` (`TreeView.cs:599`), including keyboard and programmatic.
- **Upstream:** `Expand ()` clears TVIS_EXPANDEDONCE precisely so TVN_ITEMEXPANDING/ED fire again (`TreeView/TreeNode.cs:1667-1680, 2020-2033`); `DoCollapse` raises `OnBeforeCollapse`/`OnAfterCollapse` manually (`:1465-1476`); `TvnSelecting`/`TvnSelected` raise `OnBeforeSelect`/`OnAfterSelect` with `ByKeyboard`/`ByMouse`/`Unknown` (`TreeView.cs:2490-2533`).
- **Impact:** Lazy-loading trees that populate children in `BeforeExpand` work only for mouse clicks — `ExpandAll ()`/`node.Expand ()` from code leaves them empty (`BeforeExpand` does fire there, but `AfterExpand` cleanup doesn't); "unsaved changes" prompts in `BeforeSelect` never run; `e.Action` checks misfire.
- **Fix:** Move raising into `TreeNode.Expand/Collapse`: `OnBeforeExpand` → set → `OnAfterExpand`; `OnBeforeCollapse` (cancellable) → set → `OnAfterCollapse`. Make `BeforeSelect` real and raise it (cancellable) from `SelectedItem` set with the action passed in from the caller (`ByMouse` in `OnMouseClick`, `ByKeyboard` in `OnKeyDown`, `Unknown` otherwise); pass the same action to `AfterSelect`.
- **Test:** Subscribe `AfterExpand`; `Nodes[0].Expand ()` → 1 raise. Subscribe `BeforeSelect` with `e.Cancel = true`; `SelectedNode = Nodes[1]` → selection unchanged.
- **Tests today:** none on these events.

### LST-24 — `TreeView.CheckBoxes` / `TreeNode.Checked` / `BeforeCheck` / `AfterCheck` — Cat C — P1 — High
- **Ours:** `CheckBoxes` stored (`TreeView.cs:240`); `TreeNode.Checked` auto (`TreeViewItem.cs:319`); renderer never draws a box (`Renderers/TreeViewRenderer.cs:43-92`); `OnKeyDown` has `// TODO: If checkboxes, space toggles checkbox` (`TreeView.cs:726`); `BeforeCheck`/`AfterCheck` dropped (`:216-219`).
- **Upstream:** `Checked` set → `TreeViewBeforeCheck` (cancellable) → `TreeViewAfterCheck` (`TreeView/TreeNode.cs:323-360`); Space toggles (`TreeView.cs:2241-2265`); TVS_CHECKBOXES draws the state image (`:273`).
- **Impact:** Permission/feature trees show no boxes and cannot be ticked; cascading-check code in `AfterCheck` never runs.
- **Fix:** Draw `ControlPaint.DrawCheckBox` between glyph and image when `CheckBoxes`; hit-test it in `OnMouseClick` and toggle on Space; give `Checked` a setter that raises `BeforeCheck` (honour `Cancel`) then `AfterCheck` through the owning tree.
- **Test:** `CheckBoxes = true`; subscribe `AfterCheck`; `Nodes[0].Checked = true` → 1 raise; headless render shows glyph pixels left of the text.
- **Tests today:** none.

### LST-25 — `TreeView.ImageList` / `TreeNode.ImageIndex` / `SelectedImageIndex` / `ImageKey` — Cat C — P1 — High
- **Ours:** `TreeView.ImageList`, `ImageIndex`, `SelectedImageIndex` stored (`TreeView.cs:267-273`), `ImageKey`/`SelectedImageKey` stored (`MidSizeControlParity.Two.cs:114-117`); `TreeNode.ImageIndex/ImageKey/SelectedImageIndex/SelectedImageKey/StateImageIndex` all marked "Stub" (`TreeViewItem.cs:421-434`). The renderer draws only `item.ImageSK`, which is set solely by `TreeNode.Image` (`Renderers/TreeViewRenderer.cs:80-84`; `TreeViewItem.cs:212-226`). Grep of `TreeView.cs`/`TreeViewItem.cs`/`TreeViewRenderer.cs` finds no read of `ImageList`/`ImageIndex`. `COMPATIBILITY_MATRIX.md:227` claims "index-based `ImageIndex` works" — it does not.
- **Upstream:** `TVIF_IMAGE`/`TVIF_SELECTEDIMAGE` from the tree's `ImageList` (`TreeView/TreeView.cs:612`, `TreeNode.cs:505, 888`).
- **Impact:** Every explorer-style tree built the WinForms way (`ImageList` + `ImageIndex`) shows no icons.
- **Fix:** In `TreeViewRenderer.RenderItem`, resolve the bitmap as: node `Image` → else `tree.ImageList` by (`IsSelected ? SelectedImageIndex/Key : ImageIndex/Key`, falling back to the tree's defaults) → `StateImageList[StateImageIndex]`. Correct the matrix row.
- **Test:** `ImageList` with one 16px red bitmap; `Nodes.Add ("a").ImageIndex = 0`; headless render; assert a red pixel in `GetImageBounds`.
- **Tests today:** `TreeViewTests.cs:56-119` (property defaults only).

### LST-26 — `TreeNode.NodeFont` / `ForeColor` / `BackColor`; `TreeView.ItemHeight` / `Indent` / `ShowPlusMinus` / `ShowLines` / `ShowRootLines` / `LineColor` — Cat C — P1 — High
- **Ours:** Renderer uses `Theme.UIFont`, `Theme.FontSize`, `Theme.ForegroundColor`, `INDENT_SIZE = 18`, `control.ShowDropdownGlyph`, and row height from `GetPreferredSize` (`Renderers/TreeViewRenderer.cs:13, 56-57, 73, 91, 144`; `TreeViewItem.cs:174-180`; `TreeView.cs:750`). `NodeFont`/`ForeColor`/`BackColor` (`TreeViewItem.cs:321-325, 418`), `ItemHeight` (`TreeView.cs:249`), `Indent` (`:282`), `ShowPlusMinus`/`ShowLines`/`ShowRootLines` (`:258-264`), `LineColor` (`MidSizeControlParity.Two.cs:111`) are never read at paint. `ShowPlusMinus` and `ShowDropdownGlyph` are sibling knobs for one thing; `VisibleCount` (parity, `MidSizeControlParity.Two.cs:147-152`) divides by `ItemHeight` while the private `VisibleItemCount` divides by `ScaledItemHeight` (`TreeView.cs:864`), so the public answer is wrong.
- **Upstream:** all consumed by the native tree (`TreeView/TreeView.cs:731, 768, 1179`; `TreeNode.cs:177, 404, 729`).
- **Impact:** Bold "unread"/red "error" nodes, taller rows for touch, wider indents, hidden plus-minus — all silently ignored; `VisibleCount` disagrees with what is drawn.
- **Fix:** Renderer: use `item.NodeFont ?? control.Font`, `item.ForeColor`/`BackColor` when not `Empty`, `control.Indent` for `GetIndentStart`, `control.ShowPlusMinus` (make `ShowDropdownGlyph` an alias), draw dotted lines when `ShowLines` (root lines when `ShowRootLines`) in `LineColor`; make `ItemHeight` drive `ScaledItemHeight` when set (>0) and have `VisibleCount` use it.
- **Test:** `Nodes[0].ForeColor = Color.Red`; headless render; assert red text pixels in `GetTextBounds`. `ItemHeight = 40` → `Nodes[1].Bounds.Top - Nodes[0].Bounds.Top == LogicalToDeviceUnits (40)`.
- **Tests today:** none.

### LST-27 — `ListControl.Format` event / `FormattingEnabled` / `FormatString` / `FormatInfo` — Cat B — P1 — High
- **Ours:** `Format`, `FormattingEnabledChanged`, `FormatInfoChanged`, `FormatStringChanged` declared; the last three under `#pragma warning disable CS0067` (`src/Majorsilence.Forms/WinFormsBaseControls.cs:72-85`). `OnFormat` exists (`:157`) but no caller anywhere in `src/` (grep). `ListBox.GetItemText`/`ComboBox.GetItemText` call `DataSourceBinding.DisplayText` directly (`ListBox.cs:229-233`, `ComboBox.cs:502-506`) ignoring `FormattingEnabled`/`FormatString`/`FormatInfo`, which are stored (`WinFormsBaseControls.cs:127-133`, `ListBox.cs:671`, `ComboBox.cs:144`).
- **Upstream:** `GetItemText` raises `OnFormat` when `FormattingEnabled` and otherwise runs `Formatter.FormatObject` with `FormatString`/`FormatInfo` (`ListControl/ListControl.cs:515-560`).
- **Impact:** `FormattingEnabled = true; FormatString = "C2"` (designer-emitted for numeric/date lists) displays raw `ToString ()`; `Format += (s, e) => e.Value = ...` (the standard "show FirstName + LastName" trick) is never called.
- **Fix:** Implement `GetItemText` once in `ListControl`: filter by `DisplayMember`, and when `FormattingEnabled` raise `OnFormat` (return `e.Value` if changed to string) then `string.Format (FormatInfo, "{0:" + FormatString + "}", value)`. Raise the three `*Changed` events from their setters.
- **Test:** `FormattingEnabled = true; Format += (s, e) => e.Value = "X"`; `GetItemText (1) == "X"`. `FormatString = "0.0"`; item `1.25` → `"1.3"` (per `Formatter`/`ToString ("0.0")`).
- **Tests today:** none.

### LST-28 — `ListBox`/`ComboBox` `DataSource` / `DisplayMember` / `ValueMember` overrides skip the `*Changed` events; `DataSource = null` keeps items — Cat D — P2 — High
- **Ours:** The overrides write private fields and refresh, never calling `base` or `OnDataSourceChanged`/`DisplayMemberChanged`/`ValueMemberChanged` (`ListBox.cs:195-222`, `ComboBox.cs:147-173`); the base-class events (`WinFormsBaseControls.cs:64-70`) therefore never fire for these controls. `RefreshDataSource` returns early on a null source, leaving the old items (`ListBox.cs:236-241`, `ComboBox.cs:176-181`).
- **Upstream:** `OnDataSourceChanged` with null clears items and selection (`ListBoxes/ListBox.cs:1914-1926`); setters raise their events (`ListControl/ListControl.cs:48-110, 299`).
- **Impact:** `DataSource = null` to reset a filter leaves stale rows; code that hooks `DataSourceChanged` to re-select never runs.
- **Fix:** Have the overrides call `base.DataSource = value` (which raises) and then refresh; in `RefreshDataSource` treat null as `Items.Clear (); SelectedIndex = -1`.
- **Test:** Bind 3 items, `DataSource = null` → `Items.Count == 0`, `DataSourceChanged` raised once.
- **Tests today:** `ListControlDataSourceTrackingTests.cs`, `DataTableBindingTests.cs` (positive paths only).

### LST-29 — `ListControl.SelectedValue` semantics — Cat A — P2 — High
- **Ours:** With no `DataSource`, get returns `SelectedItem` and set assigns `SelectedItem` (`ListBox.cs:607-641`, `ComboBox.cs:464-492`); a value not found leaves the selection unchanged; empty `ValueMember` compares whole items.
- **Upstream:** Without a data manager get returns null and set is a no-op; empty `ValueMember` throws `InvalidOperationException`; not-found sets `SelectedIndex = -1` (`ListControl/ListControl.cs:354-385`).
- **Impact:** `SelectedValue = missingId` leaves the previous customer selected instead of clearing; code relying on `SelectedValue == null` to mean "unbound" gets the item.
- **Fix:** On a miss set `SelectedIndex = -1`; keep the lenient no-DataSource fallback (documented divergence) but say so in the matrix.
- **Test:** Bind ids 1..3, `SelectedValue = 2` then `SelectedValue = 99` → `SelectedIndex == -1`.
- **Tests today:** `ListControlDataSourceTrackingTests.cs` (found path).

### LST-30 — `ListBox.SelectedIndex` / `SelectedIndices` order in multi-select — Cat A — P2 — High
- **Ours:** `SelectedIndex` is `SelectedIndexes[0]`, the *first chronologically* selected index, and `SelectedIndices`/`SelectedItems` are in click order (`ListBoxItemCollection.cs:181-182, 191, 213`; `ListBox.cs:704`).
- **Upstream:** `SelectedIndex` is the lowest selected index and `SelectedIndices` ascend (`ListBoxes/ListBox.cs:824-845`, LB_GETSELITEMS).
- **Impact:** "Move selected up" and range logic that assumes ascending order misbehave after Ctrl-clicking bottom-to-top.
- **Fix:** Keep `SelectedIndexes` sorted (insert with `BinarySearch`) or sort on read in `SelectedIndex`/`SelectedIndices`/`SelectedItems`.
- **Test:** MultiSimple; `SetSelected (2, true); SetSelected (0, true)`; `SelectedIndex == 0`, `SelectedIndices` is [0, 2].
- **Tests today:** none on order.

### LST-31 — Event order `SelectedValueChanged` vs `SelectedIndexChanged` (and `SelectedItemChanged`) — Cat A — P2 — High
- **Ours:** `ListBox.OnSelectedIndexChanged` raises `SelectedIndexChanged` then `SelectedValueChanged` (`ListBox.cs:494-500`); `ComboBox` raises `SelectedIndexChanged`, `SelectedItemChanged`, `SelectedValueChanged` (`ComboBox.cs:334-342`).
- **Upstream:** `ListControl.OnSelectedIndexChanged` raises `SelectedValueChanged` first (`ListControl/ListControl.cs:639-641`), then the derived class raises `SelectedIndexChanged` (`ListBoxes/ListBox.cs:1865-1905`); `ComboBox` raises `SelectedItemChanged` *before* `OnSelectedIndexChanged` (`ComboBox/ComboBox.cs:898-900`). So upstream: Item → Value → Index.
- **Impact:** Handlers that set state in one and read it in the other see it in reverse.
- **Fix:** Raise in upstream order: `OnSelectedItemChanged` (combo), `OnSelectedValueChanged`, then `SelectedIndexChanged`.
- **Test:** Record raise order into a list; assert `["Item","Value","Index"]`.
- **Tests today:** none.

### LST-32 — `ComboBox.SelectionChangeCommitted` for keyboard changes with the list closed — Cat A — P2 — High
- **Ours:** `userDriven = DroppedDown` (`ComboBox.cs:250-260`), so Up/Down on a focused, closed combo (`OnKeyUp → popup_listbox.RaiseKeyUp`, `:313-315`) raises `SelectedIndexChanged` but never `SelectionChangeCommitted`.
- **Upstream:** CBN_SELENDOK is sent for keyboard selection too → `OnSelectionChangeCommitted` (`ComboBox/ComboBox.cs:3561-3563`, comment block `:3476-3519`).
- **Impact:** Apps that (correctly) use `SelectionChangeCommitted` to distinguish user changes from programmatic ones miss keyboard-driven changes.
- **Fix:** Set a `user_driven` flag in `OnKeyUp` around `RaiseKeyUp` and treat it like `DroppedDown`.
- **Test:** Focus, `OnKeyUp (Keys.Down)` with list closed → `SelectionChangeCommitted` raised once.
- **Tests today:** none.

### LST-33 — `ComboBox.DropDownHeight` / `MaxDropDownItems` / `DropDownWidth` / `ItemHeight` not consumed — Cat C — P2 — High
- **Ours:** Popup is created once with `Size = new Size (Width, 102)` and reused (`ComboBox.cs:215-221`); `DropDownWidth` (`:375`), `MaxDropDownItems` (`:378`), `DropDownHeight` (`:446`) are stored; `ComboBox.ItemHeight` is a separate stored 15 (`:449`) that `GetItemHeight` returns (`:129-133`) while the popup list uses its own font-derived `ItemHeight`.
- **Upstream:** `DropDownHeight`/`MaxDropDownItems` size the list, `DropDownWidth` its width (`ComboBox/ComboBox.cs:434-470, 650-668`); `ItemHeight` is the real row height (`:580`).
- **Impact:** `MaxDropDownItems = 20` still shows ~5 rows; a combo resized after first open keeps the old popup width; `GetItemHeight` lies.
- **Fix:** Size the popup on every open: width `DropDownWidth > 0 ? DropDownWidth : Width`, height `DropDownHeight` if explicitly set else `Math.Min (Items.Count, MaxDropDownItems) * popup_listbox.ScaledItemHeight + border`; alias `ItemHeight` to `popup_listbox.ItemHeight`.
- **Test:** 20 items, `MaxDropDownItems = 10`, open; assert `popup.Height ≈ 10 * ItemHeight`.
- **Tests today:** none.

### LST-34 — `ListBox.TopIndex` bypasses the scrollbar; `ScrollAlwaysVisible` vs `ScrollbarAlwaysVisible` — Cat A — P2 — High
- **Ours:** `TopIndex` set writes `top_index` directly (`ListBox.cs:677-683`) without moving `vscrollbar.Value`, so the next wheel/thumb event snaps back; `FirstVisibleIndex` does it correctly (`:114-125`). `ScrollAlwaysVisible` is a stored auto-property (`:662`) while the working knob is the library-named `ScrollbarAlwaysVisible` (`:513-521`).
- **Upstream:** `TopIndex` is LB_SETTOPINDEX (`ListBoxes/ListBox.cs:1113-1127`); `ScrollAlwaysVisible` is the only property (`:784`).
- **Impact:** `TopIndex = n` visually scrolls but the thumb is wrong and the first scroll jumps; `ScrollAlwaysVisible = true` shows nothing.
- **Fix:** `TopIndex` set → `FirstVisibleIndex = value`; make `ScrollAlwaysVisible` the backing property and `ScrollbarAlwaysVisible` an alias.
- **Test:** 50 items; `TopIndex = 20`; assert `vscrollbar.Value == 20` (via `FirstVisibleIndex`) after a `RaiseMouseWheel (0)`.
- **Tests today:** none.

### LST-35 — `ListBox` keyboard on `KeyUp`; Shift-click range missing in `MultiExtended` — Cat A/B — P2 — High
- **Ours:** All navigation is in `OnKeyUp` (`ListBox.cs:254-394`), so holding an arrow key does not auto-repeat and the visible selection lags the key. Shift-click is a `// TODO` (`:421`).
- **Upstream:** WM_KEYDOWN drives the native list; Shift-click selects a range.
- **Impact:** Scrolling through long lists by holding Down does nothing until release; range selection by mouse impossible.
- **Fix:** Move the body to `OnKeyDown` (as `TreeView` does, `TreeView.cs:603`); on Shift-click select `[anchor..index]`.
- **Test:** Simulate `OnKeyDown (Keys.Down)` → `SelectedIndex` advances.
- **Tests today:** none for keys.

### LST-36 — `ListView.FindItemWithText (string)` — Cat A — P2 — High
- **Ours:** The one-argument overload is an exact `OrdinalIgnoreCase` match on `Text` only (`ListView.cs:258-259`), while the library's own 3/4-arg overloads do the upstream prefix+subitems search (`OverloadParity.cs:90-124`).
- **Upstream:** `FindItemWithText (text)` = prefix search including subitems from index 0 (`ListView/ListView.cs:3200-3202`).
- **Impact:** Type-to-find code (`FindItemWithText (typed)`) returns null for partial input.
- **Fix:** `FindItemWithText (text) => Items.Count == 0 ? null : FindItemWithText (text, true, 0, true)`.
- **Test:** Items "banana","cherry"; `FindItemWithText ("ban")` returns banana. (Current `ListViewTests.cs:571-579` asserts exact-only and would need updating.)
- **Tests today:** `ListViewTests.cs:571-579` (locks in the wrong behaviour).

### LST-37 — Defaults: `ListView.HideSelection` / `TreeView.HideSelection` false; `ListBox`/`ComboBox` `DefaultCursor` Hand — Cat E — P2 — High
- **Ours:** `ListView.HideSelection` (`ListView.cs:222`) and `TreeView.HideSelection` (`TreeView.cs:246`) default false and are stored-only; `ListBox.DefaultCursor => Cursors.Hand` (`ListBox.cs:55`), `ComboBox` too (`ComboBox.cs:48`). `ListBox.HideSelection` (`:653`) does not exist upstream at all.
- **Upstream:** `HideSelection` `[DefaultValue(true)]` on both (`ListView/ListView.cs:1031`, `TreeView/TreeView.cs:481`); cursor is the default arrow.
- **Impact:** Cosmetic but universal: every list/combo shows a hand cursor; unfocused trees/lists keep a strong highlight.
- **Fix:** Default `HideSelection = true` and have the renderers use a grey highlight when `!Focused && HideSelection`; drop the `DefaultCursor` overrides.
- **Test:** `new TreeView ().HideSelection` is true; render unfocused with a selection → highlight colour is `ControlLight`, not `HighlightLow`.
- **Tests today:** none.

### LST-38 — `TreeNode.FullPath` ignores `TreeView.PathSeparator`; detached behaviour — Cat A — P2 — High
- **Ours:** Hard-codes `"\\"` (`TreeViewItem.cs:351-358`); `PathSeparator` is stored (`TreeView.cs:255`); a node in a detached subtree returns just `Text`.
- **Upstream:** `GetFullPath (path, tv.PathSeparator)`; detached throws `InvalidOperationException` (`TreeView/TreeNode.cs:454-469`).
- **Impact:** Trees using `/` as separator (file-path trees) produce `a\b` paths that fail later string splits.
- **Fix:** Use `TreeView?.PathSeparator ?? "\\"` and walk to the root.
- **Test:** `PathSeparator = "/"`; child `FullPath == "Parent/Child"`.
- **Tests today:** `TreeViewTests.cs:304, 556-564` (default separator only).

### LST-39 — `CheckedListBox.Items[i] = value` is a no-op — Cat B — P2 — High
- **Ours:** `CheckedObjectCollection` indexer setter is `set { }` (`CheckedListBox.cs:175-178`).
- **Upstream:** `ObjectCollection` indexer replaces the item (`ListBox.ObjectCollection.cs:167-180`).
- **Impact:** Renaming an entry in place silently does nothing.
- **Fix:** `set => _inner[index] = new CheckedListBoxItem (value, GetWrapper (index).Checked)` preserving check state.
- **Test:** `Items[0] = "new"`; `Items[0] == "new"`; check state preserved.
- **Tests today:** none.

### LST-40 — `ComboBox` mouse wheel does not change selection — Cat B — P2 — Medium
- **Ours:** No `OnMouseWheel` on `ComboBox` (`ComboBox.cs`); the base scrolls nothing.
- **Upstream:** The native control changes the selection on wheel when focused (handled by comctl; no managed override — `grep MouseWheel ComboBox.cs` is empty).
- **Impact:** Users used to wheeling through a focused combo see nothing; low but noticeable.
- **Fix:** Override `OnMouseWheel`: when focused and not dropped down, `SelectedIndex ±= 1` clamped, mark user-driven.
- **Test:** Focus, `OnMouseWheel (delta -120)` → `SelectedIndex` +1.
- **Tests today:** none.

### LST-41 — `ListBox.Text` not overridden — Cat A — P2 — High
- **Ours:** Inherits `Control.Text` (no override in `ListBox.cs`).
- **Upstream:** Getter returns the selected item's display text; setter selects the matching item (`ListBoxes/ListBox.cs:1055-1090`).
- **Impact:** `lbl.Text = listBox.Text` shows "" instead of the selection; `listBox.Text = "Apple"` does not select.
- **Fix:** Mirror upstream using `GetItemText`/`FindStringExact`.
- **Test:** Select 1; `Text == Items[1].ToString ()`; `Text = Items[2].ToString ()` → `SelectedIndex == 2`.
- **Tests today:** none.

### LST-42 — `TreeView.TopNode` / `TreeNode.IsVisible` ignore scrolling — Cat A — P2 — High
- **Ours:** `TopNode` get is `Items.FirstOrDefault ()` and set is `{ }` (`TreeView.cs:327-330`); `IsVisible` is "all ancestors expanded" (`TailParity.cs:69-78`).
- **Upstream:** `TopNode` is TVGN_FIRSTVISIBLE and settable (`TreeView/TreeView.cs:1297`); `IsVisible` is "has an on-screen rectangle" (`TreeNode.cs:609-625`).
- **Impact:** Save/restore scroll position via `TopNode` does nothing; `IsVisible` true for off-screen nodes.
- **Fix:** `TopNode` get → `GetVisibleItems (skipOffscreen: true).FirstOrDefault ()`; set → `EnsureItemVisible` then set `top_index` to its index; `IsVisible` → ancestors expanded **and** index within `[top_index, top_index + VisibleItemCount)`.
- **Test:** 50 nodes, `TopNode = Nodes[30]` → `TopNode == Nodes[30]`, `Nodes[0].IsVisible == false`.
- **Tests today:** none.

### LST-43 — `ListViewItem.Group` typed `object`; groups never rendered — Cat E/C — P2 — High
- **Ours:** `Group` is `object?` (`ListViewItem.cs:131`); assigning it does not add the item to `ListViewGroup.Items` (a plain `List<ListViewItem>`, `ListView.cs:459`); `ShowGroups` stored (`:158`); renderer ignores groups.
- **Upstream:** `ListViewGroup? Group` with two-way membership (`ListView/ListViewItem.cs:438`).
- **Impact:** `item.Group.Header` fails to compile (loud), but `new ListViewItem (...) { Group = g }` compiles and shows no grouping (silent).
- **Fix:** Type as `ListViewGroup?`; setter maintains `group.Items`; render group headers in Details/Tile when `ShowGroups`.
- **Test:** `item.Group = g` → `g.Items.Contains (item)`.
- **Tests today:** `ListViewGroupTests.cs` (collection only).

## Low-priority / Win32-only (P3) — one line each
- `ListBox.UseTabStops` / `UseCustomTabOffsets` / `CustomTabOffsets` — tab expansion in native LB text; stored (`ListBox.cs:659`, `MidSizeControlParity.Three.cs:230-234`).
- `ListBox.MultiColumn` / `ColumnWidth` / `HorizontalScrollbar` / `HorizontalExtent` / `IntegralHeight` — stored (`ListBox.cs:650-674`); niche layouts, portable in principle but rarely used in LOB code.
- `ListBox.ItemHeight` honoured in `DrawMode.Normal` (`ListBox.cs:165-180`) where upstream ignores it unless `OwnerDrawFixed` (`ListBoxes/ListBox.cs:573`); designer never serialises it in Normal mode.
- `ListBox.PreferredHeight` omits the border height upstream adds (`MidSizeControlParity.Three.cs:238` vs `ListBoxes/ListBox.cs:706`).
- `ListBox.Items` writable while `DataSource` is set (upstream throws `DataSourceLocksItems`, `ListBoxes/ListBox.cs:1273-1279`) — deliberate leniency, no silent damage.
- `CheckedListBox.SelectionMode` accepts Multi* (upstream throws, `ListBoxes/CheckedListBox.cs:188-204`); `ThreeDCheckBoxes`, `UseCompatibleTextRendering` — theme-engine only.
- `ComboBox.FlatStyle`, `IntegralHeight` — visual theme knobs; `ComboBox.SelectAll/0` already in baseline.
- `ListBox.WmReflectCommand` (`KryptonPortParity.cs:266`) — Win32 message hook, no portable meaning.
- `ListView.LabelWrap` / `AutoArrange` / `Alignment` / `ArrangeIcons` / `BackgroundImageTiled` / `HotTracking` / `TileSize` / `InsertionMark` / `AllowColumnReorder` — icon-view layout details, stored (`ListView.cs:158-170`, `ListViewParity.cs:31-57`).
- `ListView.VirtualMode` / `VirtualListSize` / `RetrieveVirtualItem` / `CacheVirtualItems` — documented as unsupported in `COMPATIBILITY_MATRIX.md:226`; events are `add { } remove { }` (`ListView.cs:246-249`).
- `ListView.OwnerDraw` / `DrawItem` / `DrawSubItem` / `DrawColumnHeader` — real events, never raised by the renderer; documented in matrix.
- `ListView.LabelEdit` / `ListViewItem.BeginEdit`, `TreeView.LabelEdit` / `TreeNode.BeginEdit` / `EndEdit` / `IsEditing` — in `NoOpStubBaseline.txt`; in-place editing absent.
- `TreeNode.Handle` / `FromHandle` — HTREEITEM only.
- `TreeView.RightToLeftLayout`, `ListView.RightToLeftLayout` — mirroring; stored.
- `TreeView.ShowNodeToolTips` / `TreeNode.ToolTipText`, `ListView.ShowItemToolTips` / `ListViewItem.ToolTipText` — stored; tooltip infrastructure question, not list-specific.
- `TreeView` keyboard `*` (expand all), `+`/`-` — missing; minor.
- `ColumnHeader.AutoResize` — in baseline; `ListView.AutoResizeColumn` exists and works.

## Systemic patterns
- **Selection mutated below the event layer.** `ListBoxItemCollection` exposes internal `SelectedIndex`/`AddSelectedIndex`/`RemoveSelectedIndex`/`ToggleSelectedIndex` and `ListBox`, `ComboBox`, `CheckedListBox` all reach them directly; only the public `ListBox.SelectedIndex` setter raises. Sweep: make every mutation go through one raising method on the control (LST-03, 04, 06). Same shape in `ListView`: `ListViewItem.Selected`/`Checked` are auto-properties, so every path except `ListView.SelectedItem` is silent (LST-17, 18).
- **`add { } remove { }` on the hooks apps actually use.** `ColumnClick`, `ItemActivate`, `ItemCheck(ed)`, `BeforeSelect`, `BeforeCollapse`, `Before/AfterCheck`, `MeasureItem`, label-edit events. They pass the name scanner and discard the delegate. Sweep: grep `add { } remove { }` in these files and convert each to a real event with an `On*` raiser wired to the trigger that already exists (double-click, expand/collapse, checked setter).
- **Renderer reads a private knob while the WinForms-named sibling is stored-only.** `ShowDropdownGlyph`/`ShowPlusMinus`, `INDENT_SIZE`/`Indent`, `GetPreferredSize`/`ItemHeight`, `ScrollbarAlwaysVisible`/`ScrollAlwaysVisible`, `ComboBox.ItemHeight`/`popup_listbox.ItemHeight`, `TreeNode.Image`/`ImageIndex`+`ImageList`. Sweep: make the WinForms name the backing property and alias the library name.
- **Hit-testing in mixed coordinate spaces.** `MouseEventArgs` are logical; `Bounds` computed from `ClientRectangle` + `LogicalToDeviceUnits` are device. `ListBox.GetIndexAtLocation` was fixed and documented; `ListView`, `TreeView.GetItemAtLocation`, and the public `IndexFromPoint`/`GetNodeAt`/`GetItemAt`/`HitTest` were not (LST-15, 20, 21). Sweep: one conversion helper at every mouse entry point, or lay out in logical units.
- **Two implementations of one query.** `TreeView.GetNodeAt` (fake DFS/`ItemHeight`) vs `GetItemAtLocation` (real layout); `ListBox.IndexFromPoint` vs `GetIndexAtLocation`; `ListView.FindItemWithText (string)` vs its own 4-arg overload; `TreeView.VisibleCount` vs `VisibleItemCount`. Sweep: forward the WinForms-named member to the library's working one.
- **"Sorted" stored on three controls, implemented on none of the insert paths** (`ListBox`, `TreeView`, `ListView.Sort`); `ComboBox` sorts once on set then appends.
- **Overrides that drop the base's event.** `ListBox`/`ComboBox` `DataSource`/`DisplayMember`/`ValueMember` overrides never call `base`, so `ListControl`'s `*Changed` events are dead for the only two controls that derive from it (LST-28); `GetItemText` overrides skip the `Format`/`FormattingEnabled` path (LST-27).
- **Wrong defaults that flip a boolean's meaning.** `HideSelection` false vs true on `ListView`/`TreeView`; `DefaultCursor` Hand on lists/combos.
- **Matrix drift.** `COMPATIBILITY_MATRIX.md:227` says TreeView "index-based `ImageIndex` works"; nothing reads it (LST-25). `:225` says `PreferredHeight`/`Sort()` are missing on `ListBox`; `PreferredHeight` now exists, `Sort` still does not.
