# ToolStrip family, menus, status bar, tooltips, notify icon — findings

## Summary

The strip family is built on a legacy `MenuBase`/`MenuItem` core (`ToolStripItem : MenuItem`, `ToolStrip : ToolBar : MenuBase`), with the WinForms surface layered on top across ~15 partial files. The core (layout, hit-testing, hover, click, drop-down open/close, ContextMenu lifecycle) genuinely works. Three failure patterns dominate everything above it: (1) **split storage** — `ToolStripItem` re-declares `MenuItem` members with `new` (`Enabled`, `Tag`, `Height`, `Checked`, `Alignment`, `MouseEnter`), so the layout/renderer/hit-test path reads the `MenuItem` field while app code writes the `ToolStripItem` one; (2) **appearance knobs nobody reads** — the per-type renderers consume only `Text`/`ImageSK`/`Enabled`/`Hovered`/`HasItems`, so `DisplayStyle`, `Checked`, `ForeColor`/`Font`, `Alignment`, `Spring`, `ShortcutKeyDisplayString`, `ToolTipText` are all inert although the matrix documents only the strip-level group (`Renderer`/`RenderMode`/`LayoutStyle`/`GripStyle`/`Stretch`/`CanOverflow`); (3) **no keyboard pipeline** — `WindowBase.HandleKeyDown` never consults menus, so `ShortcutKeys`, `&Mnemonics`, F10/Alt, arrows and Esc do nothing. Add one coordinate-space bug (`Show(control, point)` takes screen, upstream takes client) and a facade bypass that silences `ItemClicked` on `MenuStrip`/`ContextMenuStrip`.

Counts: **P0: 3**, **P1: 18**, **P2: 17**, P3 list separately. Upstream's legacy `MainMenu`/`MenuItem`/`ContextMenu`/`StatusBar` live in `Controls/Unsupported/` and throw `PlatformNotSupportedException` on .NET, so legacy-only findings are rated P2 at most (this layer is already "more than upstream" there).

The matrix-documented stored-only group is recorded once here and not repeated per finding: `ToolStrip.Renderer` (Initialize hooks run, painting unchanged), `RenderMode`, `LayoutStyle`, `GripStyle`/`GripVisible`/`GripMargin`, `Stretch`, `CanOverflow`, `ToolStripManager.Renderer`/`RenderMode`, `MenuStrip.MdiWindowListItem`, `BindingNavigator.*Item` (`src/Majorsilence.Forms/WinFormsCompat.cs:2252-2345`, `2914-2940`).

## Status (2026-09-03, W5.15 — item storage and appearance)

**Closed:** TSM-01 (P0), TSM-04, TSM-06, TSM-31, and the part of TSM-30 whose triggers this item
touches (`EnabledChanged`, `VisibleChanged`, `AvailableChanged`, `DisplayStyleChanged`). 14 tests in
`ToolStripItemStateTests.cs`, 11 verified to fail with their fix neutralized; 3 are labelled in-test as
guards. The menu-lifecycle half of TSM-30 (`MenuActivate`/`MenuDeactivate`, `ContextMenu.Popup`/
`Collapse`, `MenuItem.Popup`) is left for W5.16 with the rest of the menu facade work.

**An addition to TSM-01, and it matters more than the finding as written.**
`MenuDropDown.OnMouseClick` -- the path a context menu or any sub-menu takes -- gates only on
`clicked_item != null && !clicked_item.HasItems` and **never checked `Enabled`**. The gate the finding
cites (`MenuBase.cs:135`) is on the menu-bar path, which a drop-down never reaches. So deleting the
`new Enabled` shadow fixes a disabled item on a `MenuStrip` and leaves it clickable everywhere disabled
items actually live. Both are fixed; the gate is what the test proves.

**A correction to TSM-31's mechanism.** Strips lay their items out in `OnPaint` (`MenuBase.OnPaint`),
not during a layout pass, so `PerformLayout` on a strip leaves every item with empty bounds and it is
`Invalidate` that re-lays them out. `InvalidateItemLayout` does both -- the layout call is for the
strip's *own* size, which `ToolStripPanelRowLayout.GetPreferredSizeCore` measures from its items.

**Noted while here, not a finding in this file yet:** `MenuBase.GetItemAtLocation` compares the point
it is given against `item.Bounds` with no conversion, while `Bounds` are device pixels and a real
`MouseEventArgs` carries logical ones -- the same defect class as `LST-20` was for `ListView` and
`TreeView`. It is invisible at scale 1, which is why the suite does not show it.

## Findings

### TSM-01 — `ToolStripItem.Enabled` — Cat A — P0 — High
- **Ours:** `public new bool Enabled { get; set; } = true;` on `ToolStripItem` (`src/Majorsilence.Forms/WinFormsCompat.cs:1093`) hides the non-virtual `MenuItem.Enabled` (`src/Majorsilence.Forms/MenuItem.cs:67`, which also folds in `OwnerControl.Enabled` and invalidates). Every consumer holds items as `MenuItem`: click gating `MenuBase.cs:135`, hover gating `MenuBase.cs:224`, disabled colour/greying `Renderers/ToolBarRenderer.cs:35,135`, `Renderers/MenuDropDownRenderer.cs`, `Renderers/MenuRenderer.cs`. All read the base field, which stays `true`.
- **Upstream:** one `Enabled` (`src/System.Windows.Forms/System/Windows/Forms/Controls/ToolStrips/ToolStripItem.cs:801-838`): setter clears selected/pressed, raises `EnabledChanged`, `Invalidate()`; `FireEventInteractive` refuses every mouse event when disabled (`ToolStripItem.cs:2292-2296`).
- **Impact:** `saveToolStripMenuItem.Enabled = false` (the single most common menu operation in a LOB app) leaves the item painted normal, hover-highlighted and **clickable**. Only `toolStrip.Enabled = false` on the whole strip works.
- **Fix:** delete the `new` property; make `MenuItem.Enabled` `virtual` (or route the `new` setter to `((MenuItem)this).Enabled`), raise `OnEnabledChanged` and `OwnerControl?.Invalidate()` in the one setter. Same treatment for `Tag` (`:1096`).
- **Test:** `var i = new ToolStripMenuItem { Enabled = false }; Assert.False (((MenuItem)i).Enabled);` and, on a `ContextMenuStrip` with the item, `menu.GetItemAtLocation(...)` + simulated `OnMouseClick` must not raise `Click`.
- **Tests today:** `ToolStripParityTests.cs` (asserts `CanSelect`, which reads the shadow, so it passes for the wrong reason).

### TSM-02 — `ToolStripMenuItem.ShortcutKeys` (and `MenuItem.Shortcut`) — Cat B — P0 — High
- **Ours:** `public Keys ShortcutKeys { get; set; }` (`WinFormsCompat.cs:1674`) has no consumer. `WindowBase.HandleKeyDown` (`src/Majorsilence.Forms/WindowBase.cs:1341-1383`) handles Accept/Cancel buttons, then `OnKeyDown`, then `adapter.RaiseKeyDown`; `Control.ProcessCmdKey`/`WindowBase.ProcessCmdKey` are `=> false` stubs never called (`Control.Compat.cs:579`, `WindowBase.Compat.cs:34`). `ToolStripManager.IsShortcutDefined` returns `false` always (`TailParity.cs:467`). Legacy `MenuItem.Shortcut` is stored (`AppMenuBindingParity.cs:161`).
- **Upstream:** `ContainerControl.ProcessCmdKey` → `ToolStripManager.ProcessCmdKey` → `ProcessShortcut` walks from the focused control up the parent chain checking each `ContextMenuStrip` and every strip's `Shortcuts` table (`ToolStripManager.cs:716-760`, `Layout/Containers/ContainerControl.cs:1231`); the matching item's `ProcessCmdKey` fires `Click` when `Enabled && ShortcutKeys == keyData && !HasDropDownItems` (`ToolStripMenuItem.cs:1032-1041`). The setter also validates via `IsValidShortcut` (`:357-366`).
- **Impact:** Ctrl+S / Ctrl+N / Ctrl+Z / F5 etc. — declared on essentially every menu item in every migrated app — do nothing. Apps rarely have a second binding.
- **Fix:** in `WindowBase.HandleKeyDown`, after the Accept/Cancel block and before `OnKeyDown`, call a new `ToolStripManager.ProcessShortcut(this, keys)` that walks the form's control tree (and `Control.ContextMenu` of the focused chain) for `ToolStripMenuItem`s whose `ShortcutKeys == keys` and are `Enabled && !HasDropDownItems`, calling `PerformClick()` and returning handled. Also honour `MenuItem.Shortcut` (cast to `Keys`). Make `IsShortcutDefined` consult the same walk.
- **Test:** headless `Form` with a `MenuStrip` → `ToolStripMenuItem { ShortcutKeys = Keys.Control | Keys.S }`; invoke `form.HandleKeyDown(Keys.Control | Keys.S)` (internal) and assert `Click` fired once and the result is `true`; assert a disabled item does not fire.
- **Tests today:** none.

### TSM-03 — `ContextMenu.Show(Control, Point)` / `ContextMenuStrip.Show(Control, Point)` — Cat A — P0 — High
- **Ours:** `ContextMenu.Show(Control parent, Point location)` → `ShowCore` → `MenuDropDown.Show(parent, location)` → `popup.Show(location)` where `PopupWindow.Show(Point screenLocation)` is screen-space (`src/Majorsilence.Forms/ContextMenu.cs:48,86-95`, `MenuDropDown.cs:162-177`, `PopupWindow.cs:58`). The internal right-click path compensates by passing `PointToScreen(e.Location)` (`Control.cs:1768`), which proves the API is screen-space.
- **Upstream:** `ToolStripDropDown.Show(Control control, Point position)` does `_displayLocation = control.PointToScreen(position)` — `position` is in the control's **client** coordinates (`ToolStripDropDown.cs:1813-1826`). `Show(Point)` is the screen-space overload (`:1845`).
- **Impact:** the canonical `contextMenuStrip1.Show(button1, new Point(0, button1.Height))` (drop-down-under-a-button) and `Show(grid, e.Location)` from a `MouseUp` handler open the menu at the top-left of the *screen*. `Show(Control, int, int)` and the `LeftRightAlignment` overload (`OverloadParity.Final.cs:56`) inherit the same bug.
- **Fix:** in `ContextMenu.Show(Control parent, Point location)` (and the `ShowCore` call for it) convert with `parent.PointToScreen(location)` before calling `base.Show`; leave `Show(Point)` as screen-space. Fix `Control.cs:1152,1768` to pass `e.Location` unconverted.
- **Test:** headless form, child at (100,100); `menu.Show(child, new Point(0, 10))`; assert the popup's `Location == child.PointToScreen(new Point(0,10))`.
- **Tests today:** `MenuClickReproTests.cs`, `WindowContextMenuImeParityTests.cs` call `Show(...)` but assert on lifecycle/IME, not location.

### TSM-04 — `ToolStripItem.Visible`/`Available` on a `ToolStrip` — Cat A — P1 — High
- **Ours:** `ToolBar.LayoutItems` lays out `Items.Cast<ILayoutable>()` with no visibility filter (`src/Majorsilence.Forms/ToolBar.cs:77`) and `ToolBarRenderer.Render` paints every item (`Renderers/ToolBarRenderer.cs:13-17`). `Menu`, `MenuDropDown`, `StatusStrip` and their renderers do filter (`Menu.cs:43`, `MenuDropDown.cs:103`, `WinFormsCompat.cs:2989`). `MenuItem.Visible` is a plain auto-property (`MenuItem.cs:364`) — no `PerformLayout`/`Invalidate`; `ToolStripItem.Available` raises `AvailableChanged` only (`ToolStripParity.cs:120-129`). `VisibleChanged` is never raised.
- **Upstream:** `SetVisibleCore` unselects, un-pushes, raises `AvailableChanged` and `VisibleChanged` (`ToolStripItem.cs:3214-3225`); the strip's `DisplayedItems` excludes unavailable items and re-lays out.
- **Impact:** `toolStripButton.Visible = false` (permission-based toolbar trimming) leaves the button laid out and painted on a `ToolStrip`; hit-testing skips it, so it becomes a dead, visible button. On menus the item disappears only after the next unrelated repaint.
- **Fix:** filter `i.Visible` in `ToolBar.LayoutItems` and `ToolBarRenderer.Render`; in `MenuItem.Visible` setter do `OwnerControl?.PerformLayout(); OwnerControl?.Invalidate();` and have `ToolStripItem` raise `VisibleChanged` alongside `AvailableChanged`.
- **Test:** `ToolStrip` with two buttons, hide the first, `PerformLayout()`, assert second button's `Bounds.X` equals the padded left edge and hidden button's bounds are empty.
- **Tests today:** `MenuTests.cs` (hidden item on a `Menu` only).

### TSM-05 — `ToolStripMenuItem.CheckOnClick` / `ToolStripButton.CheckOnClick` — Cat B — P1 — High
- **Ours:** both are bare auto-properties (`WinFormsCompat.cs:1671`, `:1331`); no `OnClick` override on either type toggles `Checked` (grep over `src/` finds no reader).
- **Upstream:** `ToolStripMenuItem.OnClick`: `if (_checkOnClick) Checked = !Checked;` before `base.OnClick` (`ToolStripMenuItem.cs:761-766`); `ToolStripButton.OnClick` identical (`ToolStripButton.cs:225-229`).
- **Impact:** View-menu toggles ("Show Toolbar", "Word Wrap") and toggle toolbar buttons never change state; handlers reading `Checked` see the old value.
- **Fix:** override `OnClick(EventArgs)` in both types: `if (CheckOnClick) Checked = !Checked; base.OnClick(e);`.
- **Test:** `new ToolStripMenuItem { CheckOnClick = true }.PerformClick()` → `Checked == true`, `CheckedChanged` raised once.
- **Tests today:** none.

### TSM-06 — `ToolStripMenuItem.Checked` / `ToolStripButton.Checked` rendering — Cat C — P1 — High
- **Ours:** setters store and raise `CheckedChanged` but neither invalidates (`WinFormsCompat.cs:1660-1667`, `:1317-1326`). `MenuDropDownRenderer.RenderItem` draws background/image/text/arrow only — no check glyph (`Renderers/MenuDropDownRenderer.cs:27-58`); `ToolBarRenderer.RenderItem` picks background from `Hovered || IsDropDownOpened` only — no pressed/checked state (`Renderers/ToolBarRenderer.cs:31-33`).
- **Upstream:** `Checked` setter calls `InvokePaint()` (`ToolStripMenuItem.cs:253-263`); `CheckedImage` is drawn via `OnRenderItemCheck` (`:270-283`); `ToolStripButton` draws checked as pressed background.
- **Impact:** checked menu items show no tick; toggle buttons show no depressed state — the user cannot see the mode they are in.
- **Fix:** invalidate in both setters (`Owner?.Invalidate()`); in `MenuDropDownRenderer.RenderItem` draw `ControlPaint`'s check glyph in the 28px image gutter when `item is ToolStripMenuItem { Checked: true }` (and a bullet for `RadioCheck`); in `ToolBarRenderer.RenderItem` use the highlight background when `item is ToolStripButton { Checked: true }`.
- **Test:** render a `ContextMenuStrip` with a checked item to a bitmap headlessly; assert non-background pixels in the gutter rect of that item and none for an unchecked sibling.
- **Tests today:** none.

### TSM-07 — `ToolStripItem.DisplayStyle` — Cat C — P1 — High
- **Ours:** stored (`WinFormsCompat.cs:1087`); `ToolBarRenderer.RenderItem`/`GetPreferredItemSize` always draw and measure both image and text (`Renderers/ToolBarRenderer.cs:63-66,138-139,175-178`).
- **Upstream:** `DisplayStyle` setter re-lays out (`ToolStripItem.cs:703-716`); the internal layout omits text for `Image`, image for `Text`, both for `None`.
- **Impact:** the designer writes `DisplayStyle = ToolStripItemDisplayStyle.Image` on almost every toolbar button with an icon, and `Text` is still `"toolStripButton1"`/the caption. Migrated toolbars show every button's caption beside its icon, tripling their width.
- **Fix:** in `ToolBarRenderer` treat `text` as empty when `strip_item.DisplayStyle is Image or None` and `image` as null when `Text or None`, in both `RenderItem` and `GetPreferredItemSize`; make the setter `PerformLayout()`.
- **Test:** `ToolStripButton { Text = "Save", Image = 16x16, DisplayStyle = Image }` on a `ToolStrip` — `GetPreferredSize` width equals the image-only width (compare against a `Text = ""` twin).
- **Tests today:** `ToolStripParityTests.cs` (only `ResetDisplayStyle` round-trip).

### TSM-08 — `ToolStrip.ItemClicked`/`ItemAdded`/`ItemRemoved` on `MenuStrip`/`ContextMenuStrip`; `ToolStripDropDownItem.DropDownItemClicked` — Cat D — P1 — High
- **Ours:** the events are wired only in the `ToolStripItemCollection` facade returned by `CreateItemsFacade` (`WinFormsCompat.cs:2221-2248`). `Menu.Items` and `MenuDropDown.Items` re-expose `RootItems` directly (`Menu.cs:38`, `MenuDropDown.cs:70`), so `menuStrip.Items.Add(...)` / `contextMenuStrip.Items.Add(...)` never pass through the callbacks: no `ItemAdded`, no `Renderer.InitializeItem`, no `Click → OnItemClicked` hook. `MenuBase.OnItemClicked` is a no-op virtual (`MenuBase.cs:154`) that `ToolStrip` does not override. `OnDropDownItemClicked` (`WinFormsBaseControls.cs:292`) has no caller.
- **Upstream:** `HandleClick` → `Owner.HandleItemClick` → `OnItemClicked` for every item on every strip type (`ToolStripItem.cs:2412-2445`, `ToolStrip.cs:2453-2463`); `ToolStripDropDownItem` subscribes to its drop-down's `ItemClicked` and re-raises `DropDownItemClicked` (`ToolStripDropDownItem.cs:108,347-348`).
- **Impact:** the very common single-handler pattern `contextMenuStrip1.ItemClicked += (s,e) => switch (e.ClickedItem.Name)` never fires on context menus or menu bars; `fileToolStripMenuItem.DropDownItemClicked` never fires.
- **Fix:** override `MenuBase.OnItemClicked(MouseEventArgs, MenuItem)` in `ToolStrip` to call `OnItemClicked(new ToolStripItemClickedEventArgs(item))` when `item is ToolStripItem` (and remove the per-item `Click +=` hook to avoid double-fire); in `MenuDropDown.OnMouseClick` also walk to `OwnerItem`/`Parent as ToolStripDropDownItem` and call its `OnDropDownItemClicked`. Have `MenuItemCollection.InsertItem/RemoveItem` notify the owning strip for `ItemAdded`/`ItemRemoved`.
- **Test:** `ContextMenuStrip` + item, count `ItemClicked`, simulate `OnMouseClick` at the item's bounds → 1.
- **Tests today:** `ToolStripTests.cs` (plain `ToolStrip` only — the working path).

### TSM-09 — `ToolStripStatusLabel.Spring` / `StatusStrip` item widths — Cat A/C — P1 — High
- **Ours:** `Spring` and `Alignment` are stored (`WinFormsCompat.cs:2072-2075`; `Alignment` is a `new` shadow of `ToolStripItem.Alignment`). `StatusStrip.LayoutItems` gives every AutoSize item a fixed `DefaultItemWidth = 120` (`WinFormsCompat.cs:2983-3006`) rather than its measured text width, and lays out strictly left-to-right.
- **Upstream:** `Spring` triggers layout (`ToolStripStatusLabel.cs:130-142`); `StatusStrip.OnSpringTableLayoutCore` makes spring columns `SizeType.Percent` and others `AutoSize` (`StatusStrip.cs:480-493`).
- **Impact:** the standard status bar ("Ready" spring label, then position/zoom labels pushed to the right) collapses: the spring label is 120px, text longer than 120px is clipped, right-hand items sit immediately after it.
- **Fix:** in `StatusStrip.LayoutItems` measure `item.GetPreferredSize` for AutoSize items, honour explicit `Size.Width`, then distribute remaining width across `Spring` labels; place `Alignment == Right` items from the right edge (also honour `SizeGripBounds`).
- **Test:** 600px `StatusStrip` with `[Spring label, 50px label]`; after `PerformLayout()` assert second label's `Bounds.Right` is at the padded right edge and the spring label fills the remainder.
- **Tests today:** none for `Spring`.

### TSM-10 — `ToolStripItem.Alignment = Right` on `ToolStrip`/`MenuStrip` — Cat C — P1 — High
- **Ours:** stored (`WinFormsCompat.cs:1090`); `ToolBar.LayoutItems`/`Menu.LayoutItems` use `StackLayoutEngine.HorizontalExpand`, which stacks left-to-right with no alignment notion (`Layouts/StackLayoutEngine.cs:30-46`; note the `expand` flag is stored and unused).
- **Upstream:** `ToolStripSplitStackLayout` places `Right`-aligned items from the trailing edge (`ToolStripItem.cs:279-300`).
- **Impact:** "Help"/"Logout"/search box items designed to sit at the right of a menu bar or toolbar appear immediately after the left group.
- **Fix:** in `ToolBar.LayoutItems` and `Menu.LayoutItems` partition by `(item as ToolStripItem)?.Alignment`, lay the left group with the existing engine and the right group backwards from `rect.Right`.
- **Test:** two buttons, second `Alignment = Right`; assert its `Bounds.Right == LogicalClientRectangle.Right`.
- **Tests today:** none.

### TSM-11 — `ToolStripItem.ToolTipText` / `AutoToolTip` / `ToolStrip.ShowItemToolTips` — Cat C — P1 — High
- **Ours:** all stored (`WinFormsCompat.cs:1084`, `:2282`, `ToolStripParity.cs:159`). `ToolTip` supports `Control`s only (`ToolTip.cs:96-110`); nothing shows a tip for a hovered item.
- **Upstream:** `ToolStrip.UpdateToolTip(item)` shows `item.ToolTipText` (or `Text` when `AutoToolTip`) via the strip's internal `ToolTip` whenever `ShowItemToolTips` (`ToolStrip.cs:1600-1665`).
- **Impact:** icon-only toolbars (see TSM-07) rely entirely on tooltips to be discoverable; none appear.
- **Fix:** in `MenuBase.OnHoverChanged`/`SetHover`, when `newItem is ToolStripItem { ToolTipText.Length: > 0 }` (or `AutoToolTip`) and `ShowItemToolTips`, reuse `ToolTip.ShowPopup`-style logic (a `PopupWindow` + `Label`) anchored at the item's bounds; hide on hover-leave/click.
- **Test:** internal hook — after `SetHover(item)` assert the strip's tooltip popup `Visible` and its label text equals `ToolTipText` (headless `PopupWindow` creation is already exercised by `ToolTipTests`).
- **Tests today:** none.

### TSM-12 — `ToolStripMenuItem.ShortcutKeyDisplayString` / `ShowShortcutKeys` rendering — Cat C — P1 — High
- **Ours:** stored (`WinFormsCompat.cs:1677-1680`); `MenuDropDownRenderer.RenderItem` draws text left-aligned and reserves a flat 70px (`Renderers/MenuDropDownRenderer.cs:45-49,100-103`); no shortcut column.
- **Upstream:** `GetShortcutText()` returns `ShortcutKeyDisplayString` or the `Keys`-converted `ShortcutKeys`, measured by `GetShortcutTextSize()` and drawn right-aligned by the renderer (`ToolStripMenuItem.cs:719-745`, `:397-432`).
- **Impact:** menus show no "Ctrl+S" hints; combined with TSM-02 users have no way to learn shortcuts.
- **Fix:** in `MenuDropDownRenderer`, for `ToolStripMenuItem` with `ShowShortcutKeys` compute the shortcut string (`ShortcutKeyDisplayString` else `KeysConverter`-style text of `ShortcutKeys`), add its width to `GetPreferredItemSize`, and draw it `MiddleRight` inside the item bounds minus the arrow gutter.
- **Test:** `GetPreferredItemSize` for an item with `ShortcutKeys = Ctrl+S` is wider than the same item with `ShowShortcutKeys = false`.
- **Tests today:** none.

### TSM-13 — Keyboard menu access: `&Mnemonics` (Alt+F), F10/Alt, arrows, Esc — Cat B — P1 — High
- **Ours:** mnemonics are parsed and underlined only (`Mnemonics.cs`, `Renderers/MenuRenderer.cs:37`). No `OnKeyDown` in `MenuBase`/`Menu`/`MenuDropDown`/`ContextMenu`; `Control.ProcessMnemonic`/`ProcessDialogKey` are `=> false` stubs (`Control.Compat.cs:585,606`); `WindowBase.HandleKeyDown` never consults `Application.ActiveMenu` (`WindowBase.cs:1341-1383`). Esc does not close an open menu.
- **Upstream:** `ToolStripManager.ProcessMenuKey`/`ModalMenuFilter` enter menu mode on Alt/F10 (`ToolStripManager.cs:871`), `ToolStrip.ProcessMnemonic` selects the item whose mnemonic matches and `ToolStripMenuItem.ProcessMnemonic` opens its drop-down (`ToolStripMenuItem.cs:1044-1058`), `ProcessDialogKey`/`ProcessArrowKey` navigate (`ToolStrip.cs:2732-2777,3031`), Esc closes (`ToolStripDropDown.cs:1320,1357`).
- **Impact:** keyboard-only operation of menus is impossible; accessibility and power-user flows break; an accidentally opened menu cannot be dismissed with Esc.
- **Fix:** in `WindowBase.HandleKeyDown`, when `Application.ActiveMenu != null` route Esc → `ClosePopups()`, Left/Right → `SelectedItem = GetNextItem(...)` on the top-level menu, Up/Down/Enter → hover/click within the open `MenuDropDown`; when `keys` has `Alt` and a letter, find the `Menu` item whose `Mnemonic` matches and set `SelectedItem`. `ToolStrip.GetNextItem` already exists (`ToolStripParity.cs:416`).
- **Test:** headless form + `MenuStrip` with "&File"; `HandleKeyDown(Keys.Alt | Keys.F)` → `menu.SelectedItem` is the File item and `IsDropDownOpened`; `HandleKeyDown(Keys.Escape)` → closed.
- **Tests today:** none.

### TSM-14 — `MenuDropDown.OnMouseClick` fires `Click` on disabled items — Cat A — P1 — High
- **Ours:** `if (clicked_item != null && !clicked_item.HasItems) { Application.ClosePopups(); clicked_item.OnClick(e); ... }` — no `Enabled` check (`src/Majorsilence.Forms/MenuDropDown.cs:120-131`). `MenuBase.OnMouseClick` does check (`MenuBase.cs:135`) but `MenuDropDown` overrides it entirely. Compounds TSM-01 (even a correctly-disabled base flag is ignored here).
- **Upstream:** `FireEventInteractive` returns immediately when `!Enabled` (`ToolStripItem.cs:2292-2296`); the drop-down stays open.
- **Impact:** clicking a greyed-out context-menu or drop-down item runs its handler and closes the menu.
- **Fix:** add `&& clicked_item.Enabled` to the condition; do not close popups for a disabled click.
- **Test:** `ContextMenuStrip` with a disabled item; simulate `OnMouseClick` at its bounds; `Click` count stays 0.
- **Tests today:** none (`MenuDropDownClickTests.cs` covers enabled items).

### TSM-15 — `ToolStripSplitButton` button-vs-arrow halves, `OnButtonClick`, `DefaultItem` — Cat A — P1 — High
- **Ours:** `OnClick` calls `base.OnClick` then `ButtonClick?.Invoke` directly (`WinFormsCompat.cs:1576-1582`), bypassing the virtual `OnButtonClick` (`CyotekPortParity.cs:47`); `DefaultItem` is stored (`:1564`). Because the item `HasItems`, `MenuBase.OnMouseClick` also sets `SelectedItem` → `ShowDropDown()` (`MenuBase.cs:136`), so one click both fires `ButtonClick` and opens the menu. `DropDownButtonWidth` is stored.
- **Upstream:** `OnMouseDown/OnMouseUp` split on `DropDownButtonBounds.Contains(e.Location)`: arrow half opens the drop-down only, button half raises `ButtonClick` via `OnButtonClick`, which first fires `DefaultItem`'s `Click` (`ToolStripSplitButton.cs:388-392`, `:419-460`).
- **Impact:** a split "Save ▾" button opens its menu on every save click; overrides of `OnButtonClick` never run; `DefaultItem` never fires.
- **Fix:** in `OnClick(MouseEventArgs)` compute the arrow rect (`Bounds.Right - DropDownButtonWidth`); if the click is inside it, `ShowDropDown()` and return; else `OnButtonClick(EventArgs.Empty)` (which should `DefaultItem?.PerformClick()` then raise `ButtonClick`) and suppress the `SelectedItem` open — e.g. override `ShowDropDown` to no-op unless an `opening_from_arrow` flag is set.
- **Test:** click at `Bounds.Left + 2` → `ButtonClick` 1, `IsDropDownOpened` false; click at `Bounds.Right - 2` → `ButtonClick` 0, `IsDropDownOpened` true.
- **Tests today:** none.

### TSM-16 — `ToolStripItem.ForeColor` / `BackColor` / `Font` — Cat C — P1 — High
- **Ours:** stored (`WinFormsCompat.cs:1175-1181`); all three renderers use `Theme.ForegroundColor`/`Theme.UIFont`/`Theme.FontSize` (`Renderers/ToolBarRenderer.cs:35-36`, `MenuDropDownRenderer.cs:44-45`, `StatusStripRenderer.cs:13,37`). `ForeColorChanged`/`BackColorChanged`/`FontChanged` never raised.
- **Upstream:** setters invalidate and the renderer draws with `item.ForeColor`/`item.Font` (`ToolStripItem.cs:510,864,909`, `ToolStripRenderer.OnRenderItemText`).
- **Impact:** status labels turned red for errors, bold "unsaved" indicators, coloured menu items — all draw in theme colours.
- **Fix:** in each renderer prefer `strip_item.ForeColor` when not `Color.Empty`, `strip_item.Font` when non-null (size via `Font.SizeInPoints`), `BackColor` for the item background; invalidate from the setters and raise the `*Changed` events.
- **Test:** render a `StatusStrip` label with `ForeColor = Red` headlessly; assert a red pixel inside its text bounds.
- **Tests today:** none.

### TSM-17 — `ToolTip.Show(text, control[, x, y][, duration])` — Cat A — P1 — High
- **Ours:** every `Show` overload is `=> SetToolTip(control, text)` (`src/Majorsilence.Forms/ToolTip.cs:188-197`, `IWin32Window` variants `:151-171`). Nothing is displayed until the pointer next enters the control; `x, y` and `duration` are dropped, and the caller's persistent tip text is overwritten.
- **Upstream:** `ShowTooltip` positions and shows the tip immediately (at the point or centred on the control) and auto-hides after `duration` (`ToolTip/ToolTip.cs:1305-1335`).
- **Impact:** validation hints (`toolTip.Show("Required", textBox, 0, textBox.Height, 2000)`) never appear; the control's hover tip is replaced by the hint text.
- **Fix:** route to the existing private `ShowPopup(control, text, at)` with `at = new Point(x, y)` (or the control's centre), do not touch `tips[]`, and start a `Timer` for `duration > 0` that calls `HidePopup()`.
- **Test:** headless form + control; `Show("hi", control, 5, 5)`; assert the popup is `Visible` with label text "hi" and `GetToolTip(control)` unchanged.
- **Tests today:** `ToolTipTests.cs` (SetToolTip/GetToolTip/RemoveAll only).

### TSM-18 — `ToolStripItem.MouseEnter`/`MouseLeave`/`MouseHover`/`MouseDown`/`MouseUp`/`MouseMove`/`DoubleClick` — Cat D — P1 — High
- **Ours:** declared under `#pragma warning disable CS0067` with raisers `OnMouseEnter`/`OnMouseLeave`/`OnMouseDown`/`OnMouseUp` (`ToolStripParity.cs:58-80,298-316`), but `MenuBase.SetHover` (`MenuBase.cs:206-231`) and `MenuBase.OnMouseClick` never call them; `Pressed` therefore never becomes true. `ToolStripLabel` additionally re-declares `MouseEnter`/`MouseLeave` as `add { } remove { }` (`WinFormsCompat.cs:1364-1367`), which discards handlers even after the base is fixed.
- **Upstream:** `ToolStrip.OnMouseMove`/`OnMouseDown`/`OnMouseUp` dispatch `FireEvent(..., MouseEnter/MouseLeave/MouseDown/MouseUp)` to the item under the pointer (`ToolStrip.cs:2442,3329,3362`, `ToolStripItem.cs:2547-2611`).
- **Impact:** status-bar help text on hover (`item.MouseEnter += ...`), drag-initiation from `MouseDown`, and `DoubleClick` on items never fire.
- **Fix:** in `MenuBase.SetHover` call `(old as ToolStripItem)?.OnMouseLeave(...)` / `(item as ToolStripItem)?.OnMouseEnter(...)`; in `Control.RaiseMouseDown/RaiseMouseUp` paths of `MenuBase` call `OnMouseDown/OnMouseUp` on the hit item; delete the `ToolStripLabel` shadows.
- **Test:** `ToolStrip` + button with `SetBounds`; call internal `SetHover(item)`; `MouseEnter` raised once; `SetHover(null)` → `MouseLeave`.
- **Tests today:** none.

### TSM-19 — `NotifyIcon` (all members) — Cat B — P1 — High
- **Ours:** `Visible`/`Icon`/`Text`/`ContextMenuStrip` stored; `Click`/`DoubleClick`/`MouseClick`/`MouseDoubleClick`/`MouseMove`/`BalloonTip*` are `add { } remove { }` (`src/Majorsilence.Forms/NotifyIcon.cs:58-84`); `ShowBalloonTip` validates and returns (`:129-139`). No backend exposes any tray seam (`grep -ri tray Backends/` is empty). The class doc says stub, but **COMPATIBILITY_MATRIX.md:258 lists `NotifyIcon` as "Implemented"**.
- **Upstream:** `Visible` → `UpdateIcon` adds/removes the shell icon (`NotifyIcon.cs:263-276,624`), click messages raise the events (`:445-470`), right-click shows `ContextMenuStrip` (`:604`).
- **Impact:** "minimise to tray" apps (`Resize` → `Hide(); notifyIcon.Visible = true`) become unreachable — the window is hidden and there is no icon to restore it. Silent because everything compiles and returns.
- **Fix:** short-term: correct the matrix row to Partial/stub and document the trap; consider having `Visible = true` while the owning form is hidden log a warning. Long-term: add an `INotifyIconBackend` seam (Avalonia has `TrayIcon`; macOS `NSStatusItem`) with `Show/Hide/SetIcon/SetText`, click callbacks, and use `ContextMenuStrip.Show(Point)` for right-click.
- **Test:** with a fake backend registered, `Visible = true` calls `backend.Show` once; simulated click raises `Click`.
- **Tests today:** `NotifyIconTests.cs` (argument validation only).

### TSM-20 — `ToolStripComboBox.Text` — Cat A — P1 — High
- **Ours:** `ToolStripComboBox` and `ToolStripControlHost` do not override `Text`, so `Text` is `MenuItem.Text` — the *item's* caption (`WinFormsCompat.cs:1731-1830`, `ToolStripHostParity.cs:473-547`); only `ToolStripTextBox` forwards (`WinFormsCompat.cs:1389-1392`). `TextChanged` on the combo item is forwarded to the combo but `Text` is not.
- **Upstream:** `ToolStripControlHost.Text` gets/sets `Control.Text` (`ToolStripControlHost.cs`), so every hosted item reports the hosted control's text.
- **Impact:** `toolStripComboBox1.Text` returns `""` (or whatever caption was set) instead of the typed/selected text; `Text = "x"` sets a caption the renderer hides for hosts and leaves the combo unchanged.
- **Fix:** add `public override string Text { get => Control.Text; set => Control.Text = value; }` to `ToolStripControlHost` (MenuItem.Text is virtual) and drop the `new` in `ToolStripTextBox`.
- **Test:** `var c = new ToolStripComboBox(); c.ComboBox.Text = "abc"; Assert.Equal("abc", c.Text);`
- **Tests today:** `ToolStripControlHostingTests.cs` (hosting/bounds only).

### TSM-21 — `ContextMenu.Closing` reason/cancel, `ToolStripDropDown.Close(reason)`, `AutoClose` — Cat A — P2 — High
- **Ours:** `Close(reason) => Hide()` drops the reason (`TailParity.cs:430`); `Deactivate` always raises `Closing`/`Closed` with `AppFocusChange` (`ContextMenu.cs:122-125`), including after an item click (`MenuDropDown.cs:126` → `ClosePopups`) and after `Close()`; `e.Cancel` is never read; `AutoClose` is stored (`TailParity.cs:406`).
- **Upstream:** `_closeReason` is set by the path that closes (`ItemClicked`, `CloseCalled`, `Keyboard`, `AppClicked`) and `SetVisibleCore` honours `e.Cancel` and pre-sets it from `!AutoClose` (`ToolStripDropDown.cs:958-979,1134,1640-1660`).
- **Impact:** the "keep the menu open when a checkable item is clicked" idiom (`e.Cancel = e.CloseReason == ItemClicked`) does nothing and always sees `AppFocusChange`; `AutoClose = false` menus still close.
- **Fix:** add a `pending_close_reason` field set by `Close(reason)`, the item-click path (`ItemClicked`) and outside-click path (`AppClicked`); in `Deactivate` build the args from it, and if `e.Cancel` (or `!AutoClose` and reason != `CloseCalled`) return without hiding.
- **Test:** `Closing += (s,e) => e.Cancel = true; menu.Close(); Assert.True(menu.Visible)` and reason assertions per path.
- **Tests today:** none (`ApplicationContextTests.cs` matches "Closing" for forms).

### TSM-22 — `ToolStripItem.Size`/`Width`/`Height`/`ContentRectangle`, `ToolStrip.GetItemAt` — Cat A — P2 — High
- **Ours:** `Size` is an independent stored value (`WinFormsCompat.cs:1075`), `Height`/`Width` read it (`:1078`, `ToolStripParity.cs:132`), `ContentRectangle => (0,0,Size)` (`ToolStripParity.cs:147`), while layout writes `MenuItem.Bounds` (`MenuItem.cs:277-280`). `ToolStrip.GetItemAt` hit-tests `new Rectangle(item.Bounds.Location, item.Size)` (`ToolStripParity.cs:403`) — for AutoSize items `Size` is 0×0 so it returns null for everything.
- **Upstream:** `Size`/`Width`/`Height` are views of `_bounds` (`ToolStripItem.cs:952,1807,2009`); `GetItemAt` uses `Bounds` (`ToolStrip.cs:3871`).
- **Impact:** `item.Width`/`item.Height` read 0 after layout; `GetItemAt` never finds an auto-sized item; setting `Size` does not re-layout.
- **Fix:** make `Size` getter return `Bounds.Size` when the explicit size is empty; hit-test with `Bounds`; `PerformLayout` in the setter.
- **Test:** `ToolStripTests`: after `SetBounds(0,0,50,20)`, `item.Width == 50` and `strip.GetItemAt(10,10)` is the item.
- **Tests today:** `ToolStripParityTests.cs`/`MenuTests.cs` (GetItemAt with explicit `Size` only).

### TSM-23 — `ToolStripSeparator` on `ToolStrip`/`MenuStrip` — Cat A — P2 — High
- **Ours:** `ToolBarRenderer.Render` and `MenuRenderer.Render` special-case only `MenuSeparatorItem` (`Renderers/ToolBarRenderer.cs:13-17`, `MenuRenderer.cs:12-20`); a `ToolStripSeparator` (`WinFormsCompat.cs:1719`) is painted as a 6px-wide blank item that hover-highlights (`MenuBase.SetHover` only skips disabled). `MenuDropDownRenderer` does handle it.
- **Upstream:** separators are non-selectable (`CanSelect` false) and drawn as a line by `OnRenderSeparator`.
- **Impact:** toolbar separators vanish (no line) and light up on hover.
- **Fix:** treat `ToolStripSeparator` like `MenuSeparatorItem` in both renderers (vertical line, `GetPreferredSeparatorItemSize`); make `ToolStripSeparator.CanSelect => false` and have `SetHover` skip `!CanSelect`.
- **Test:** `GetPreferredItemSize` for a separator on a `ToolStrip` equals the `MenuSeparatorItem` size; hover does not set `Hovered`.
- **Tests today:** none.

### TSM-24 — `ToolStripMenuItem.CheckState` / `CheckStateChanged` — Cat A/D — P2 — High
- **Ours:** `CheckState` is derived from the bool (`WinFormsCompat.cs:1686-1689`): `Indeterminate` sets `Checked = false` and reads back `Unchecked`; the `Checked` setter raises only `CheckedChanged` (`:1660-1667`), never `OnCheckStateChanged` (`RemainingMemberParity.cs:447-452`). `ToolStripButton.Checked` does raise both (`:1317-1326`).
- **Upstream:** `CheckState` is the storage, `Checked` derives from it, both events fire on any change (`ToolStripMenuItem.cs:253-315`).
- **Impact:** tri-state menu items collapse; `CheckStateChanged` subscribers never run.
- **Fix:** store `CheckState`; `Checked => CheckState != Unchecked`; raise both events from the `CheckState` setter.
- **Test:** `CheckState = Indeterminate` round-trips and raises `CheckStateChanged`.
- **Tests today:** none.

### TSM-25 — `ToolStripItem.IsOnDropDown` — Cat A — P2 — High
- **Ours:** `=> GetCurrentParent() is ToolStripDropDown` (`ToolStripParity.cs:150`). Sub-menus are `MenuDropDown` (created in `MenuItem.ShowDropDown`, `MenuItem.cs:297`), not `ToolStripDropDown`, so items under a `ToolStripMenuItem` report `false`; only `ContextMenuStrip` items report `true`.
- **Upstream:** `IsOnDropDown` is true for any item whose parent is a `ToolStripDropDown` (`ToolStripItem.cs:1226`), which every sub-menu is.
- **Impact:** renderers/handlers branching on `IsOnDropDown` (menu-vs-toolbar styling) take the toolbar branch for menu items.
- **Fix:** `=> Owner is MenuDropDown || OwnerItem is not null`.
- **Test:** item added to `fileMenuItem.DropDownItems`, parented on a `MenuStrip`, reports `IsOnDropDown == true`.
- **Tests today:** none.

### TSM-26 — `ToolStripItem.PerformClick` — Cat A — P2 — High
- **Ours:** `MenuItem.PerformClick() => OnClick(...)` unconditionally (`AppMenuBindingParity.cs:203`).
- **Upstream:** `if (Enabled && Available) FireEvent(Click)` (`ToolStripItem.cs:3017-3023`).
- **Impact:** code that calls `PerformClick` on a possibly-disabled item (keyboard shortcuts, accessibility `DoDefaultAction`) runs the handler anyway. Will matter more once TSM-02 routes shortcuts through it.
- **Fix:** guard with `Enabled && Visible` (after TSM-01 unifies `Enabled`).
- **Test:** disabled item, `PerformClick()`, `Click` count 0.
- **Tests today:** `ToolStripTests.cs`, `AppMenuBindingParityTests.cs` (enabled case).

### TSM-27 — `ToolStripManager.Merge`/`RevertMerge`, `ToolStripItem.MergeAction`/`MergeIndex`, `MenuItem.MergeMenu` — Cat B — P2 — High
- **Ours:** all `Merge`/`RevertMerge` overloads `=> false` (`WinFormsCompat.cs:2923-2938`); `MergeAction`/`MergeIndex` stored twice (`ToolStripParity.cs:193-196`, `new` again on `ToolStripMenuItem` `WinFormsCompat.cs:1700-1703`). Legacy `MenuItem.MergeMenu` does a simplified clone-merge (`AppMenuBindingParity.cs:245-262`).
- **Upstream:** `Merge` walks source items applying `Append/Insert/Replace/Remove/MatchOnly` with `MergeIndex`, recording history for `RevertMerge` (`ToolStripManager.cs:1068-1361`).
- **Impact:** MDI apps that merge a child's `MenuStrip` into the parent's get no child menus; return value `false` is at least honest. Not on the matrix.
- **Fix:** implement `Merge` for `Append`/`Insert`/`Remove`/`Replace`/`MatchOnly` (text-matched recursion) on the `RootItems` collections, keep a per-target history list for `RevertMerge`.
- **Test:** source `[Edit(MatchOnly)→[Paste(Append)]]` merged into target `[Edit→[Copy]]` yields `Edit→[Copy, Paste]`; `RevertMerge` restores.
- **Tests today:** none.

### TSM-28 — `ToolStrip.OverflowButton` null; `ToolStripOverflowButton`/`Overflow` inert — Cat E — P2 — High
- **Ours:** `OverflowButton` is a `ToolStripItem?` auto-property that is never assigned (`ToolStripParity.cs:392`); `ToolStripItem.Overflow`/`Placement`/`IsOnOverflow` are stored/constant (`WinFormsCompat.cs:1166`, `ToolStripParity.cs:153-156`). Items past the right edge are simply clipped.
- **Upstream:** `OverflowButton` is lazily created and never null (`ToolStrip.cs:1344-1356`); items that do not fit move to its drop-down.
- **Impact:** `toolStrip1.OverflowButton.DropDown...` throws `NullReferenceException`; narrow windows lose toolbar buttons entirely. (`CanOverflow` itself is on the matrix; the null and the clipping are not.)
- **Fix:** lazily create a `ToolStripOverflowButton` so the property is non-null; optionally, in `ToolBar.LayoutItems`, move items whose right edge exceeds the client width into its `DropDownItems` when `CanOverflow`.
- **Test:** `Assert.NotNull(new ToolStrip().OverflowButton)`.
- **Tests today:** none.

### TSM-29 — `ToolStrip.TabStop` default — Cat E — P2 — Medium
- **Ours:** no `TabStop` assignment in `MenuBase`/`ToolBar`/`ToolStrip`/`Menu` (grep empty), so the `Control` default (true) applies.
- **Upstream:** `TabStop = false` in the `ToolStrip` constructor (`ToolStrip.cs:127`).
- **Impact:** Tab cycles focus into menu bars/toolbars/status strips between the real input controls.
- **Fix:** set `TabStop = false` in `ToolBar()`/`ToolBar(MenuItem)` constructors.
- **Test:** `Assert.False(new ToolStrip().TabStop)`; `Assert.False(new MenuStrip().TabStop)`.
- **Tests today:** none.

### TSM-30 — Declared-never-raised events with existing triggers — Cat D — P2 — High
- **Ours:** `ToolStripItem.TextChanged`/`EnabledChanged`/`VisibleChanged`/`LocationChanged`/`OwnerChanged`/`DisplayStyleChanged`/`BackColorChanged`/`ForeColorChanged`/`FontChanged` (`ToolStripParity.cs:24-111`, `ExtendedToolkitParity.cs:11-23`); `ToolStrip.LayoutCompleted`/`LayoutStyleChanged`/`RendererChanged` (`ToolStripParity.cs:350-359`) — `Renderer` setter exists at `WinFormsCompat.cs:2296` and does not raise; `MenuStrip.MenuActivate`/`MenuDeactivate` (`RemainingMemberParity.cs:335-347`) — `MenuBase.Activate`/`Deactivate` exist (`MenuBase.cs:34-52`); `ToolStripSplitButton.DefaultItemChanged`; `ContextMenu.Popup`/`Collapse`, `MainMenu.Collapse` (`RemainingMemberParity.cs:170-192`) — `ShowCore`/`Deactivate` exist; `MenuItem.Popup` (`AppMenuBindingParity.cs:282`) — `ShowDropDown` exists.
- **Upstream:** each has a raiser on the obvious path (`ToolStripItem.cs:1868,2800,3006`; `ToolStrip.cs:3100,3262`; `Controls/Menus/MenuStrip.cs:36-40`).
- **Impact:** handlers attached to any of these silently never run; `ContextMenu.Popup` is *the* legacy hook for enabling items before display.
- **Fix:** one sweep: raise from `MenuItem.Text` setter (via a virtual `OnTextChanged`), `Enabled` setter, `Visible` setter, `SetBounds`, `Items` insert (owner change), `Renderer` setter, end of `LayoutItems`, `MenuBase.Activate/Deactivate` (when `this is MenuStrip`), `ContextMenu.ShowCore` (`OnPopup` before `Opening`) and `Deactivate` (`OnCollapse`), `MenuItem.ShowDropDown` (`OnPopup`).
- **Test:** one assertion per event: mutate, assert raised once.
- **Tests today:** none for these events.

### TSM-31 — Setters that store without `PerformLayout`/`Invalidate` — Cat A — P2 — High
- **Ours:** `MenuItem.Image` (`MenuItem.cs:156-163`), `ToolStripItem.Image` (`WinFormsCompat.cs:1104-1129`), `Padding`/`Margin` (`MenuItem.cs:189,242`), `ImageScaling`/`TextImageRelation`/`TextAlign`/`ImageAlign`/`AutoSize`/`Size` (`WinFormsCompat.cs:1075-1170`), `ToolStrip.ImageScalingSize` (`:2279`). Only `Text`, `ImageIndex`/`ImageKey` and `ToolBar.ImageList` re-layout.
- **Upstream:** each calls `InvalidateItemLayout(...)` or `Invalidate()` (`ToolStripItem.cs:1015,1076,1102,1186,1522,1890`).
- **Impact:** runtime changes (swapping a play/pause icon, changing padding) appear only after an unrelated repaint.
- **Fix:** route every one through a private `InvalidateItemLayout()` = `OwnerControl?.PerformLayout(); OwnerControl?.Invalidate();` (already exists as `InvalidateImage`, `WinFormsCompat.cs:1215`).
- **Test:** on a strip with a spy `Invalidate` counter, set `item.Image`; counter increments.
- **Tests today:** `ToolStripItemImageListTests.cs` (ImageIndex path only).

### TSM-32 — Legacy `Form.Menu`, `MenuItem.Shortcut`/`RadioCheck`/`Checked`, `ContextMenu.Popup` — Cat B/C — P2 — High
- **Ours:** `Form.Menu` is `{ get; set; }` (`ControlAndFormParity.cs:535`) — a `MainMenu` assigned there is never added to the form; `MenuItem.Checked`/`RadioCheck`/`DefaultItem`/`OwnerDraw` stored (`MenuItem.cs:340-349`) with no glyph in `MenuDropDownRenderer`; `Shortcut` see TSM-02; `Popup` see TSM-30.
- **Upstream:** .NET's `MainMenu`/`MenuItem`/`ContextMenu` are `[Obsolete]` and throw `PlatformNotSupportedException` (`Controls/Unsupported/MainMenu/MainMenu.cs:23`, `Unsupported/ContextMenu/MenuItem.cs:34`); the .NET Framework semantics are: `Form.Menu` docks the bar, `Checked`/`RadioCheck` draw glyphs, `Shortcut` fires `Click`.
- **Impact:** only .NET-Framework-era apps; they compile here (an improvement on upstream .NET) but the menu bar never appears if assigned via `Form.Menu`.
- **Fix:** `Form.Menu` setter: remove the previous `MainMenu` from the root adapter's `Controls`, add the new one with `Dock = Top`. Reuse TSM-06's glyph drawing for `MenuItem.Checked`/`RadioCheck`.
- **Test:** `form.Menu = new MainMenu()`; assert it is in the form's `Controls` and docked top.
- **Tests today:** `AppMenuBindingParityTests.cs` (clone/merge only).

### TSM-33 — `StatusBar.Panels`/`ShowPanels`/`PanelClick`/`SizingGrip` — Cat C — P2 — High
- **Ours:** `Panels`/`ShowPanels` stored (`StatusBar.cs:15-18`); `StatusBarRenderer` draws only `control.Text` (`Renderers/StatusBarRenderer.cs:10`); `PanelClick`/`DrawItem` never raised (`FinalParity.cs:56-63`); `StatusBarPanel.AutoSize/Width/Alignment/BorderStyle/Icon` stored.
- **Upstream:** .NET marks `StatusBar` unsupported (`Unsupported/StatusBar/StatusBar.cs:29`); .NET Framework drew panels with borders per `Width`/`AutoSize` and raised `PanelClick`.
- **Impact:** legacy apps with `ShowPanels = true` get an empty bar (the `Text` shown is empty when panels are used).
- **Fix:** in `StatusBarRenderer`, when `ShowPanels`, lay panels left-to-right using `Width` (Spring shares remainder, Contents measures text), draw sunken borders and text per `Alignment`; hit-test in `StatusBar.OnMouseClick` to raise `PanelClick`.
- **Test:** headless render with two panels; assert text of each appears within its computed rectangle.
- **Tests today:** none.

### TSM-34 — `ToolTip.InitialDelay`/`AutoPopDelay`/`ReshowDelay`/`AutomaticDelay` — Cat C — P2 — High
- **Ours:** stored with validation (`ToolTip.cs:30-79`); `Control_MouseEnter` shows immediately and nothing ever auto-hides (`ToolTip.cs:203-213`).
- **Upstream:** the tooltip window honours the three delays (`TTM_SETDELAYTIME` via `SetToolInfo`, `ToolTip/ToolTip.cs:815`).
- **Impact:** tips flash on every pointer crossing and stay until leave; `AutoPopDelay = 30000` for long help text has no effect.
- **Fix:** start a `Timer(InitialDelay)` on enter (cancel on leave/down), then `Timer(AutoPopDelay)` to hide.
- **Test:** headless: after `MouseEnter` the popup is not visible until the timer fires (drive with the test `Timer` seam).
- **Tests today:** `ToolTipTests.cs` (delay setters only).

### TSM-35 — `ToolStripDropDownItem.ShowDropDown` event accuracy — Cat A — P2 — Medium
- **Ours:** raises `DropDownOpening` then `MenuItem.ShowDropDown`, then `DropDownOpened` unconditionally (`WinFormsBaseControls.cs:262-272`); `MenuItem.ShowDropDown` is a no-op when `!HasItems || OwnerControl is null` (`MenuItem.cs:296`), so `DropDownOpened` fires with nothing open; `Opening` is consulted only if `HasDropDown` (the drop-down object was touched).
- **Upstream:** `ShowDropDownInternal` raises `OnDropDownShow`, returns early for an empty auto-generated drop-down, and `Opened` comes from the drop-down's own `Opened` (`ToolStripDropDownItem.cs:695-725`).
- **Impact:** `DropDownOpened` handlers run for leaf items and for items whose lazy `DropDownOpening` populated nothing; a lazily-populated menu on a `MenuStrip` whose `DropDownOpening` adds items **does** work (order is correct).
- **Fix:** after `base.ShowDropDown()` raise `OnDropDownOpened` only `if (IsDropDownOpened)`; always run `RaiseOpeningCancelled` through `DropDown` (creating it is cheap).
- **Test:** leaf item `ShowDropDown()` → `DropDownOpened` not raised.
- **Tests today:** none.

### TSM-36 — `ToolStripPanel.Join(strip, row)` row index — Cat A — P2 — High
- **Ours:** `Join` records the strip in `rows[row]` (`TailParity.cs:373-386`), but `OnLayout` arranges from `Controls` ordered menu-first (`ToolStripPanelRowLayout.cs:26-49,110-150`), never reading `rows`; `Rows[i].Bounds` are never set.
- **Upstream:** the row given to `Join` is where the strip lands; `Rows[i].Bounds` are live.
- **Impact:** `Join(toolStrip2, 0)` to put a strip above another is ignored; `PointToRow` always null.
- **Fix:** in `OnLayout`, if `rows` is non-empty iterate rows in order (strips side by side within a row), set each `row.Bounds`; fall back to the current behaviour for strips added via `Controls.Add`.
- **Test:** `Join(a,1); Join(b,0);` → `b.Top < a.Top`.
- **Tests today:** `ToolStripPanelRowLayoutTests.cs` (Controls.Add path).

### TSM-37 — `ToolStrip.ImageScalingSize` — Cat C — P2 — High
- **Ours:** stored, default 16×16 (`WinFormsCompat.cs:2279`); `ToolBarRenderer` uses a hard-coded 20px box (`Renderers/ToolBarRenderer.cs:65-66,175-178`) and `MenuDropDownRenderer` 16px.
- **Upstream:** `SizeToFit` items scale to `Owner.ImageScalingSize` (`ToolStrip.cs:1000-1030`).
- **Impact:** designer-set 24×24 or 32×32 toolbars render at 20px; default 16px icons are upscaled to 20px and blur.
- **Fix:** replace the 20 with `control.ImageScalingSize` in both places.
- **Test:** `GetPreferredItemSize` height tracks `ImageScalingSize.Height`.
- **Tests today:** none.

### TSM-38 — `ToolStripControlHost` Enabled propagation; `ToolStripItem.Tag` shadow; `Renderer` setter cast — Cat A — P2 — High
- **Ours:** `SetBounds` syncs `Control.Bounds`/`Visible` only (`ToolStripHostParity.cs:488-503`); `Enabled = false` on the host leaves the hosted control enabled. `ToolStripItem.Tag` is `new` (`WinFormsCompat.cs:1096`), so `((MenuItem)item).Tag` (e.g. `CloneMenu`, `AppMenuBindingParity.cs:233`) differs from `item.Tag`. `ToolStrip.Renderer` setter does `foreach (ToolStripItem item in Items)` (`WinFormsCompat.cs:2312`) — `InvalidCastException` if the strip holds a `MenuSeparatorItem` or plain `MenuItem` (both legal per `ToolStripItemCollection` docs, `:2003-2010`); `ToolStripAccessibleObject.HitTest` has the same cast (`NestedTypeParity.cs:548`).
- **Upstream:** `ToolStripControlHost.OnEnabledChanged` sets `Control.Enabled`; one `Tag`; `Items` is homogeneous.
- **Fix:** forward `Enabled` to `Control` in the host; delete the `Tag` shadow; use `Items.OfType<ToolStripItem>()`.
- **Test:** `new ToolStrip { Items = { new MenuSeparatorItem() } }.Renderer = new ToolStripProfessionalRenderer()` does not throw.
- **Tests today:** none.

## Low-priority / Win32-only (P3) — one line each
- `NotifyIcon.ShowBalloonTip`/`BalloonTip*` events — shell balloon notifications; no portable equivalent beyond the tray seam in TSM-19.
- `ToolTip.IsBalloon`/`UseAnimation`/`UseFading`/`ToolTipIcon`/`ToolTipTitle`/`StripAmpersands`/`ShowAlways`/`OwnerDraw`/`Draw`/`Popup` — comctl32 tooltip styling; cosmetic.
- `ToolStripDropDown.DropShadowEnabled`/`Opacity`/`AllowTransparency`/`TopLevel` — layered-window attributes.
- `ToolStrip.AllowClickThrough`, `MenuStripClickThrough`/`ToolStripClickThrough` (`Compat/*ClickThrough.cs`) — `WM_MOUSEACTIVATE` message handling; there is no message pump.
- `ToolStripManager.VisualStylesEnabled`, `SaveSettings`/`LoadSettings` — uxtheme and per-user settings store.
- `ToolStripItem.RightToLeftAutoMirrorImage`, `RightToLeft`, `TextDirection` (Vertical90/270) — RTL/vertical text not rendered; niche.
- `ToolStripItem.Anchor`/`Dock`/`BackgroundImage`/`BackgroundImageLayout`/`ImageTransparentColor` — stored; upstream mostly ignores them for strip layout too.
- `ToolStripLabel.IsLink`/`LinkColor`/`VisitedLinkColor`/`ActiveLinkColor`/`LinkBehavior` — not drawn as a link; niche.
- `ToolStripDropDownButton.ShowDropDownArrow`, `ToolStripSplitButton.DropDownButtonWidth` (rendering) — arrow always drawn when `HasItems`; cosmetic.
- `StatusStrip.SizingGrip`/`SizeGripBounds` — grip not drawn; window resize is handled by the OS frame.
- `MenuItem.OwnerDraw`/`DrawItem`/`MeasureItem`, `StatusBar.DrawItem` — legacy owner-draw; upstream .NET unsupported.
- `MenuItem.BarBreak`/`Break`/`MdiList`, `MenuStrip.MdiWindowListItem` — multi-column/MDI window list; documented.
- `ToolStripContainer.ContentPanel` is `Panel` not `ToolStripContentPanel` — type shape only.
- `ToolStripItem.DoDragDrop`/drag events — no OS drag source (matches `Control.DoDragDrop`, documented).

## Systemic patterns
- **`new`-shadowed `MenuItem` members create split storage.** `ToolStripItem.Enabled`, `Tag`, `Height`/`Width`/`Size`, `ToolStripMenuItem.Checked`, `ToolStripStatusLabel.Alignment`, `ToolStripLabel.MouseEnter/MouseLeave` all hide a base member that the layout/renderer/hit-test path reads through a `MenuItem` reference. Sweep: make the `MenuItem` member virtual (or remove the shadow) so there is exactly one field.
- **`Menu`/`MenuDropDown` re-exposing `Items => RootItems` bypasses `ToolStrip`'s facade callbacks**, so anything wired in `CreateItemsFacade` (`ItemAdded`/`ItemRemoved`/`ItemClicked`, `Renderer.InitializeItem`) is dead for `MenuStrip`/`ContextMenuStrip`. Move those hooks to `MenuItemCollection`/`MenuBase` where the single collection lives.
- **Renderers keyed on concrete control type read only `Text`/`ImageSK`/`Enabled`/`Hovered`/`HasItems`.** Every `ToolStripItem`-level appearance knob (`DisplayStyle`, `Checked`, `ForeColor`/`BackColor`/`Font`, `Alignment`, `Spring`, `ShortcutKeyDisplayString`, `ImageScalingSize`, separator type) is unread. One pass over `ToolBarRenderer`/`MenuDropDownRenderer`/`StatusStripRenderer` adding `item as ToolStripItem` branches fixes TSM-06/07/12/16/23/37.
- **No keyboard dispatch to menus.** `WindowBase.HandleKeyDown` is the single entry point and never consults `Application.ActiveMenu` or strip shortcut tables; `ProcessCmdKey`/`ProcessMnemonic`/`ProcessDialogKey` are stubs no one calls. TSM-02 and TSM-13 share one insertion point.
- **Setters store without `PerformLayout`/`Invalidate`; `*Changed` events are declared under `CS0067` although the natural trigger already exists** (setter, `SetHover`, `Activate`/`Deactivate`, `ShowCore`). A mechanical sweep through `MenuItem`/`ToolStripItem` setters closes TSM-30/31 and half of TSM-04/06.
- **Coordinate-space and reason plumbing lost at API boundaries.** `Show(Control, Point)` treats client points as screen; `Close(reason)` → `Hide()` drops the reason and `Deactivate` hard-codes `AppFocusChange`; `e.Cancel` is never read.
- **Matrix drift.** `NotifyIcon` is listed "Implemented" while its own source calls it a stub with never-firing events; `ToolStripMenuItem` row says "Core surface (`Enabled`, `Visible`) present" but both are non-functional on the common paths (TSM-01/04).
