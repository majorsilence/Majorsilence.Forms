# Control base class — findings

## Summary
`Control` is a faithful port of the upstream *layout* half (SetBounds/UpdateBounds/PerformLayout/GetNextControl/
ControlCollection are near line-for-line copies and behave correctly), but the *focus, validation, handle-lifetime
and input-routing* half is a hand-written replacement that diverges in ways the name-level scanner cannot see.
Three root causes dominate: (1) **two backing stores for one concept** — `ControlStyles.Selectable` vs
`ControlBehaviors.Selectable`, `UseWaitCursor` auto-property vs `States.UseWaitCursor`, `AutoScrollMargin` property vs
`auto_scroll_margin` field, `GroupBox.AutoSize` vs `Control.AutoSize`; (2) **`IsFocusManagingContainerControl` is
hard-wired to `false`**, which makes `GetContainerControl()` return null everywhere and therefore disables focus
recovery, container-level validation and `ActiveControl`; (3) **a control is "created" the moment it is parented to
any window, shown or not**, so HandleCreated/Load run during `InitializeComponent`. Counts: 12 × P1, 19 × P2, plus a
P3 list. No P0: nothing here crashes on sight, but CTL-01/02/03/04/05 change behaviour every LOB form depends on.

## Findings

### CTL-01 — `Control.OnLostFocus` / focus-change event order and validation semantics — Cat A — P1 — High
- **Ours:** `Select()` sets `Selected`, raises the NEW control's `OnGotFocus` (Enter → GotFocus) and only then
  `adapter.SelectedControl = this`, whose setter calls the OLD control's `Deselect()` → `OnLostFocus`, which raises
  Leave → LostFocus → Validating → Validated unconditionally (`src/Majorsilence.Forms/Control.cs:1227-1254`,
  `:2282-2296`, `src/Majorsilence.Forms/ControlAdapter.cs:76-89`). Observed order A→B: **B.Enter, B.GotFocus, A.Leave,
  A.LostFocus, A.Validating, A.Validated**. `e.Cancel` in Validating is read only to skip Validated; focus has already
  moved. The entering control's `CausesValidation` and any `AutoValidate` mode are never consulted.
- **Upstream:** validation is driven by the container, not by focus loss: `ContainerControl.UpdateFocusedControl`
  raises A.Leave, then `EnterValidation(B)` which returns early when `!enterControl.CausesValidation` or
  `AutoValidate == Disable`, then `ValidateThroughAncestor` runs A.Validating/A.Validated and on Cancel with
  `EnablePreventFocusChange` calls `SetActiveControl(_unvalidatedControl)` to put focus back
  (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/ContainerControl.cs:1534-1670`, `:1727-1760`,
  `:1862-1935`). LostFocus comes from WM_KILLFOCUS (`Control.cs:11477`) and GotFocus from WM_SETFOCUS after
  `ActiveControl` was set (`Control.cs:12054-12083`). Order: A.Leave, A.Validating, A.Validated, B.Enter, A.LostFocus,
  B.GotFocus.
- **Impact:** the canonical "`e.Cancel = true` keeps the user in the invalid TextBox" pattern does nothing; a Cancel
  button with `CausesValidation = false` still triggers validation of the field being left; handlers that read
  `ActiveControl`/`Focused` inside Validating see the wrong control focused.
- **Fix:** move the Validating/Validated pair out of `OnLostFocus` into `ControlAdapter.SelectedControl`'s setter:
  compute `old`, run `old.OnLeave`, then if `value.CausesValidation` run `old.OnValidating`; on Cancel abort the
  switch (leave `selected_control = old`, re-`Select` it) and return; otherwise `old.OnValidated`, `old.OnLostFocus`
  (LostFocus only), then `value.OnEnter`/`OnGotFocus`.
- **Test:** two TextBoxes on a Form, A.Validating sets Cancel; `B.Focus()`; assert `A.Focused && !B.Focused` and the
  recorded order is `[A.Leave, A.Validating]` only. Second test: B.CausesValidation=false → Validating count 0.
- **Tests today:** ControlExtensibilityHookTests (Enter-before-GotFocus only), WindowValidationParityTests (Form.Validate
  only). None for order across two controls or Cancel.

### CTL-02 — `Control.GetContainerControl` / `IsFocusManagingContainerControl` — Cat A — P1 — High
- **Ours:** `IsFocusManagingContainerControl` is `return false;// (...)` (`src/Majorsilence.Forms/Control.Layout.cs:250-254`),
  so `GetContainerControl()` (`Control.cs:669-681`) walks to the root and returns **null for every control**, and
  `WindowBase.GetContainerControl()` forwards to the adapter (`WindowBase.cs:1866`) → also null. Consequences inside
  the framework: `SelectNextIfFocused` (`Control.cs:2359-2364`) never moves focus when the focused control is hidden or
  disabled (it stays `Selected`, `adapter.SelectedControl` still points at it); `GetNextControl` treats a UserControl
  like a Panel.
- **Upstream:** `IsFocusManagingContainerControl(ctl)` = `ControlStyles.ContainerControl` set && `ctl is
  IContainerControl` (`Control.cs:5341-5344`); `GetContainerControl` returns the nearest ContainerControl/Form
  (`Control.cs:5328`); `SelectNextIfFocused` uses it (`Control.cs:10027-10039`).
- **Impact:** `(GetContainerControl() as ContainerControl).Validate()` / `.ActiveControl` — a very common idiom in
  custom controls and third-party libraries — NullReferences; hiding/disabling the focused control leaves keyboard
  input routed to an invisible control until the user clicks.
- **Fix:** make `IsFocusManagingContainerControl` return `ctl.GetStyle(ControlStyles.ContainerControl) && ctl is
  IContainerControl`; set that style in `ContainerControl`, `UserControl`, `ControlAdapter` constructors; have
  `ControlAdapter` implement `IContainerControl` (it already owns `SelectedControl`) and return `FindForm()` from
  `WindowBase.GetContainerControl`.
- **Test:** `form.Controls.Add(tb); tb.Focus(); tb.Visible = false;` → assert `!tb.Focused` and the next TabStop is
  focused. Assert `tb.GetContainerControl()` is the form/adapter, not null.
- **Tests today:** none (grep `GetContainerControl` in tests = 0).

### CTL-03 — `Control.SetStyle(ControlStyles.Selectable)` vs `CanSelect` — Cat A — P1 — High
- **Ours:** `SetStyle`/`GetStyle` write `control_styles` (`src/Majorsilence.Forms/Control.Compat.cs:13,114-124`), but
  `CanSelect` reads `behaviors.HasFlag(ControlBehaviors.Selectable)` (`Control.cs:159-174`), a separate enum only
  settable via the non-WinForms `SetControlBehavior`. `RaiseMouseDown` calls `Select()` for every enabled control hit
  (`Control.cs:1893-1915`).
- **Upstream:** `CanSelect` → `CanSelectCore` → `GetStyle(ControlStyles.Selectable)` && visible/enabled chain;
  focus-on-click is gated on the same style (`Control.cs:11510-11515`).
- **Impact:** every ported custom control that does `SetStyle(ControlStyles.Selectable, false)` in its constructor
  (the standard way to make a label-like/panel-like control non-focusable) still takes focus on click, so clicking it
  fires Leave/Validating on the TextBox the user was editing and shows a focus rectangle. The reverse (`SetStyle(
  Selectable, true)` on a Panel subclass) also silently does nothing.
- **Fix:** delete `ControlBehaviors.Selectable`; make `CanSelect` read `GetStyle(ControlStyles.Selectable)` and have
  the existing `SetControlBehavior(Selectable, x)` callers (Panel, GroupBox, ControlAdapter, UserControl, …) call
  `SetStyle(ControlStyles.Selectable, x)` instead.
- **Test:** `class C : Control { C() { SetStyle(ControlStyles.Selectable, false); } }` → `Assert.False(new C().CanSelect)`;
  and `c.Focus()` returns false.
- **Tests today:** none.

### CTL-04 — `Control` Click/DoubleClick sequence and `MouseEventArgs.Clicks` — Cat A — P1 — High
- **Ours:** `WindowBase.HandlePointerReleased` builds `Clicks` only for the release, raises `RaiseDoubleClick` when
  `Clicks > 1`, then **always** `RaiseClick` and `RaiseMouseUp` (`src/Majorsilence.Forms/WindowBase.cs:1079-1098`,
  `:189-202`). `HandlePointerPressed` hard-codes `Clicks = 1` (`WindowBase.cs:1076`). Order on a double-click:
  MouseDown(1), Click, MouseUp, MouseDown(**1**), DoubleClick, MouseDoubleClick, **Click, MouseClick**, MouseUp.
- **Upstream:** WM_LBUTTONDBLCLK sets `States.DoubleClickFired` and delivers MouseDown with `clicks = 2`
  (`Control.cs:12474`, `:11487`); `WmMouseUp` raises either Click+MouseClick **or** DoubleClick+MouseDoubleClick, never
  both (`Control.cs:11688-11705`).
- **Impact:** a control with both Click and DoubleClick handlers (ListBox/TreeView/DataGridView item activation is
  the classic case) runs the Click action a second time on every double-click; `if (e.Clicks == 2)` in MouseDown —
  the recommended way to detect a double-click at press time — never matches.
- **Fix:** in `HandlePointerPressed` compute clicks with the same `BuildMouseClickArgs` timing and pass 2 on the second
  press; remember it in a `double_click_pending` flag; in `HandlePointerReleased` raise DoubleClick path when the flag
  is set and skip `RaiseClick`, mirroring `WmMouseUp`.
- **Test:** drive two `HandlePointerPressed/Released` pairs within `DOUBLE_CLICK_TIME` on a headless form; assert
  Click count 1, DoubleClick count 1, and the second MouseDown's `e.Clicks == 2`.
- **Tests today:** ControlExtensibilityHookTests (DoubleClick routes to OnMouseDoubleClick — hook-level only).

### CTL-05 — `Control.CreateControl` / `HandleCreated` / `UserControl.Load` timing — Cat A — P1 — High
- **Ours:** `ControlCollection.Insert` calls `item.CreateControl()` whenever `item.Visible`
  (`src/Majorsilence.Forms/ControlCollection.cs:477-481`); `ControlAdapter.Visible => ParentForm != null` is always
  true (`ControlAdapter.cs:69-72`), so any control added to a Form — shown or not — is Created immediately, raising
  `OnHandleCreated`, `OnCreateControl` and `UserControl.Load` (`Control.cs:375-393`, `UserControl.cs:52-56`) in the
  middle of `InitializeComponent`. `CreateControl` also recurses into **hidden** children (`Control.cs:388-389`) and
  `OnVisibleChanged` calls `CreateControl()` even when the transition is to invisible (`Control.cs:1613-1614`).
- **Upstream:** `ControlCollection.Add` creates only if the OWNER is already Created
  (`Control.ControlCollection.cs:106-112`); `CreateControl(false)` skips `!Visible` controls (`Control.cs:4649-4656`),
  which is what gives WinForms its lazy Load for controls on hidden TabPages.
- **Impact:** OnHandleCreated/Load handlers run before the form constructor has finished (fields still null, form
  `ClientSize` not yet applied, `FindForm().SomeService` null); UserControls on non-selected TabPages load their data
  eagerly; `IsHandleCreated` is true inside constructors, defeating the standard "am I initialised yet" guard the
  layer's own doc comment for `IsHandleCreated` says it wanted to fix.
- **Fix:** in `Insert`, gate `item.CreateControl()` on `Owner.Created`; in `CreateControl` skip children with
  `!GetState(States.Visible)`; in `OnVisibleChanged` only create when `Visible && Parent?.Created == true`; have
  `WindowBase` create the adapter (and therefore the tree) at first Show, and create late-added children when
  `Owner.Created`.
- **Test:** `var f = new Form(); var uc = new CountingUserControl(); f.Controls.Add(uc); Assert.Equal(0, uc.LoadCount);
  f.Show(); Assert.Equal(1, ...)`; plus a hidden child: `child.Visible=false; parent.Controls.Add(child)` → not Created.
- **Tests today:** UserControlLoadTests **pins the divergent behaviour** (`Load_fires_when_the_control_goes_live`
  asserts LoadCount==1 right after `Controls.Add` on an unshown Form); FormHandleCreatedTests;
  ControlExtensibilityHookTests.CreateControl_raises_OnHandleCreated_once.

### CTL-06 — `Control.UseWaitCursor` — Cat A — P1 — High
- **Ours:** `public bool UseWaitCursor { get; set; }` auto-property (`src/Majorsilence.Forms/Control.Compat.cs:304`),
  while `Cursor`'s getter checks `GetState(States.UseWaitCursor)` (`Control.cs:419-423`), a flag nothing ever sets.
  `WindowBase.UseWaitCursor` forwards to the adapter's copy (`WindowBase.cs:1873-1876`), so `Form.UseWaitCursor` is dead
  too.
- **Upstream:** setter stores the state bit and cascades to every child (`Control.cs:3438-3456`); `Cursor` returns
  `Cursors.WaitCursor` while set (`Control.cs:1551`).
- **Impact:** `this.UseWaitCursor = true` around a long operation — one of the most common WinForms idioms — shows no
  wait cursor anywhere.
- **Fix:** replace the auto-property with `get => GetState(States.UseWaitCursor); set { SetState(...); foreach child
  child.UseWaitCursor = value; FindForm()?.SetCursor(Cursor); }`.
- **Test:** `panel.UseWaitCursor = true; Assert.Same(Cursors.Wait, panel.Cursor); Assert.Same(Cursors.Wait, child.Cursor)`.
- **Tests today:** WindowStateGeometryParityTests (round-trip of the stored value only).

### CTL-07 — `UserControl.ActiveControl` / `ContainerControl.ActiveControl` — Cat C — P1 — High
- **Ours:** `public Control? ActiveControl { get; set; }` on both (`src/Majorsilence.Forms/UserControl.cs:33,67`):
  the setter focuses nothing, the getter never reflects the focused child.
- **Upstream:** setter → `SetActiveControl` → `ActivateControl` → focus moves and Enter/Leave/validation run
  (`ContainerControl.cs:283-287`, `:1449-1495`); getter is maintained by `UpdateFocusedControl`.
- **Impact:** `ActiveControl = txtName` (the standard way to set initial focus in a UserControl) does nothing;
  `if (ActiveControl is TextBox t) t.SelectAll()` never matches.
- **Fix:** getter: walk `Controls` for the descendant whose `Focused` is true (or cache from the adapter's
  `SelectedControlChanged`); setter: `value?.Select()`; keep null-assignment meaning "deselect".
- **Test:** `uc.Controls.Add(tb); form.Controls.Add(uc); uc.ActiveControl = tb; Assert.True(tb.Focused);
  Assert.Same(tb, uc.ActiveControl)`.
- **Tests today:** UserControlTests.ActiveControl_Set_GetReturnsExpected (round-trip only, no focus assertion).

### CTL-08 — `UserControl.ValidateChildren()` / `ContainerControl.ValidateChildren()` — Cat B — P1 — High
- **Ours:** parameterless overload `=> true` (`src/Majorsilence.Forms/UserControl.cs:36,79`) while the
  `ValidationConstraints` overload genuinely walks children (`OverloadParity.More.cs:17-40`, `OverloadParity.Final.cs:32`).
- **Upstream:** `ValidateChildren() => ValidateChildren(ValidationConstraints.Selectable)`
  (`ContainerControl.cs:1844`), recursing through nested containers via `PerformContainerValidation`.
- **Impact:** `if (!ValidateChildren()) return;` in an OK handler always proceeds; the Validating handlers that would
  have flagged empty fields never run. Also the parity overload only checks direct children, not descendants.
- **Fix:** `ValidateChildren() => ValidateChildren(ValidationConstraints.Selectable)`; make `ValidateChildrenCore`
  recurse into children that have children.
- **Test:** UserControl with a TextBox whose Validating cancels → `Assert.False(uc.ValidateChildren())`; nested one
  level deeper → still false.
- **Tests today:** UserControlTests.ValidateChildren_InvokeWithoutChildren_ReturnsTrue (asserts the stub); OverloadParityTests.

### CTL-09 — `Control.Validate()` — Cat A — P2 — High
- **Ours:** defined on `Control`, runs **this control's own** Validating/Validated (`Control.Compat.cs:395-412`);
  `ContainerControl`/`UserControl` inherit it unchanged; `WindowBase.Validate` validates the adapter (`WindowBase.cs:817`).
- **Upstream:** `Validate()` exists only on `ContainerControl` and validates the *last unvalidated (active) child* and
  its ancestors, not the container (`ContainerControl.cs:1776-1800`).
- **Impact:** `userControl.Validate()` / `form.Validate()` before saving reports true even when the focused TextBox's
  Validating would cancel.
- **Fix:** on `ContainerControl`/`UserControl`/`WindowBase` override to validate the currently focused descendant
  (from CTL-07's ActiveControl), walking up to but excluding the container.
- **Test:** focused child cancels → `Assert.False(container.Validate())`.
- **Tests today:** WindowValidationParityTests (Form's own Validating only).

### CTL-10 — `Control.ClientRectangle` / `ClientSize` / `PaddedClientRectangle` units — Cat A — P1 — Medium
- **Ours:** `ClientRectangle` is built from `GetScaledBounds(Bounds, ScaleFactor)` (device pixels) while `Bounds`,
  `Size`, `DisplayRectangle`, `Location` and all mouse coordinates are logical (`src/Majorsilence.Forms/Control.cs:289-302`,
  `:1651-1660`, `WindowBase.cs:1063-1064` uses `DeviceToLogical`). The `ClientSize` setter computes `border = Width -
  ClientRectangle.Width` (logical minus device) (`Control.cs:313-321`), which is negative at any scale > 1.
- **Upstream:** `ClientSize`/`ClientRectangle` are in the same coordinate space as `Size`/`Bounds`
  (`Control.cs:1268-1272`, `:10231-10236`).
- **Impact:** on a 2× display `ClientSize.Width == 2 * Width`; `ClientRectangle.Contains(e.Location)` in custom
  controls accepts points far outside; `ClientSize = new Size(300,200)` produces a `Size` of `(300 - 100, ...)`
  instead of the requested client size; `AutoScrollPosition`/`DisplayRectangle` math that mixes the two is off by the
  scale factor.
- **Fix:** either make `ClientRectangle` logical (and give the renderer a separate `ScaledClientRectangle`), or at
  minimum compute the `ClientSize` setter's border in one unit (`DeviceToLogicalUnits(ClientRectangle.Size)`).
  Grep every `ClientRectangle` consumer in `Renderers/` before switching.
- **Test:** headless window with `Scaling = 2`: `c.Width = 100; Assert.Equal(100, c.ClientSize.Width); c.ClientSize =
  new Size(50, 50); Assert.Equal(50, c.Width)`.
- **Tests today:** ControlTests.ClientSize/ClientRectangle, ScrollableControlTests — all at scale 1, where the bug is invisible.

### CTL-11 — `ScrollableControl.AutoScrollMargin` — Cat C — P1 — High
- **Ours:** `public Size AutoScrollMargin { get; set; }` (`src/Majorsilence.Forms/ScrollableControl.cs:71`) but
  `Recalculate` adds the **private field** `auto_scroll_margin`, which nothing ever assigns
  (`ScrollableControl.cs:17`, `:245-246`). `SetAutoScrollMargin` writes the property (`:74`). Doc comment admits "stub".
- **Upstream:** the margin feeds `_scrollMargin`, which extends the scroll extent (`Scrolling/ScrollableControl.cs:94-106`,
  `:331-370`).
- **Impact:** the designer-emitted `AutoScrollMargin = new Size(0, 10)` (used so the last row of controls is not flush
  against the bottom when scrolled) is ignored; content is clipped by exactly the margin.
- **Fix:** delete the field; have `Recalculate` read `AutoScrollMargin` and the setter call `PerformLayout`.
- **Test:** Panel 100×100, AutoScroll, child at Bottom=100, `AutoScrollMargin = new Size(0, 20)` → vertical scrollbar
  visible with `Maximum >= 20`.
- **Tests today:** PanelTests / FormControlParityTests / TailParityTests (default-value and round-trip only).

### CTL-12 — `ScrollableControl.Recalculate` copy/paste faults — Cat A — P1 — High
- **Ours:** in the "vertical bar not needed" branch `scroll_position.X = 0;` should be `.Y`
  (`src/Majorsilence.Forms/ScrollableControl.cs:304-309`, compare `:292-297`): every layout pass with a visible
  horizontal bar and no vertical bar zeroes the horizontal position bookkeeping while the children stay shifted, so the
  next `HandleScroll` applies `hscrollbar.Value - 0` and double-scrolls them, and `AutoScrollPosition.X` reads 0.
  Also `hscrollbar.SetBounds(0, ..., sizegrip_visible ? Bounds.Width - bar_size : Bounds.Height, bar_size)` gives the
  horizontal bar a width equal to the control's **Height** when there is no size grip (`:315`).
- **Upstream:** `SetDisplayRectLocation`/`SyncScrollbars` keep one `_displayRect`; bar rectangles come from the
  non-client area (`Scrolling/ScrollableControl.cs:115-133`, `:188-210`).
- **Impact:** a wide-only AutoScroll panel (horizontal timeline, wide grid host) drifts on every scroll and shows a
  truncated or over-long scrollbar.
- **Fix:** `scroll_position.Y = 0` at `:308`; `Bounds.Width` at `:315`.
- **Test:** Panel 200×100 AutoScroll with child at Right=400; scroll `HorizontalScroll.Value = 50`, force
  `PerformLayout()`, assert `AutoScrollPosition.X == -50` and child.Left == -50; assert hscrollbar.Width == 200.
- **Tests today:** ScrollableControlTests (client/display rectangles only), GestureTests (AutoScrollPosition via gesture).

### CTL-13 — `ScrollableControl.DisplayRectangle` — Cat A — P2 — Medium
- **Ours:** client rect minus visible scrollbars minus Padding, origin always (0,0)
  (`src/Majorsilence.Forms/ScrollableControl.cs:187-200`).
- **Upstream:** when scrolled the origin is the negative scroll offset and, when a bar is shown, Width/Height are the
  **virtual** extent; `IArrangedElement.DisplayRectangle` additionally widens to `AutoScrollMinSize`
  (`Scrolling/ScrollableControl.cs:188-230`).
- **Impact:** a `Dock = Fill` or right-anchored child inside an AutoScroll container with `AutoScrollMinSize` is laid
  out to the visible width instead of the virtual one, so it never scrolls horizontally; custom paint code that offsets
  by `DisplayRectangle.Location` (the documented alternative to `AutoScrollPosition`) draws unscrolled.
- **Fix:** offset by `-scroll_position` and, when `hscrollbar.Visible`/`vscrollbar.Visible`, use `canvas_size`
  (max with AutoScrollMinSize) for the corresponding dimension; expose the widened version to the layout engine.
- **Test:** Panel 100×100, `AutoScrollMinSize = (300, 0)`, child Dock=Fill → child.Width == 300 after layout.
- **Tests today:** ScrollableControlTests.DisplayRectangle (padding only).

### CTL-14 — `Control.Visible` with no parent, and `VisibleChanged` on Add/Remove — Cat A — P2 — High
- **Ours:** getter returns `parent?.Visible ?? false` (`src/Majorsilence.Forms/Control.cs:2673-2680`), so a brand-new or
  just-removed control reports `Visible == false`. Because of that, `AssignParent` sees false→true on Add and raises
  `OnVisibleChanged`, then `Insert`'s `finally` raises it **again** (`ControlCollection.cs:477-481`, `Control.cs:104-111`);
  on Remove the true→false transition raises `VisibleChanged` and cascades `OnParentVisibleChanged` to all descendants.
- **Upstream:** `Visible` = `DesiredVisibility && (ParentInternal is null || ParentInternal.Visible)` — an unparented
  control is visible (`Control.cs:3523-3533`); `AssignParent` therefore raises nothing on plain Add/Remove
  (`Control.cs:4262-4272`).
- **Impact:** code that checks `if (ctl.Visible)` before adding (or on a control being moved between containers) is
  wrong; VisibleChanged handlers (lazy loading, layout toggles) run twice per Add and once per Remove; `Controls.Clear()`
  in a rebuild storms VisibleChanged on every descendant.
- **Fix:** `return parent is null || parent.Visible;` (matching the state-flag semantics used by `SetTopLevel`, which
  already special-cases this); drop the explicit `item.OnVisibleChanged` in `Insert`.
- **Test:** `Assert.True(new Button().Visible)`; count VisibleChanged across `form.Controls.Add(b)` (expect 0 or 1, not
  2) and `form.Controls.Remove(b)` (expect 0).
- **Tests today:** ControlTests.OnParentVisibleChanged_* (cascade shape only, not counts).

### CTL-15 — `Control.Parent` setter — Cat A — P2 — High
- **Ours:** `value.Controls.Add(this); OnParentChanged(EventArgs.Empty);` (`src/Majorsilence.Forms/Control.cs:1683-1702`)
  — but `Add → Insert → AssignParent` already raised `OnParentChanged` (`Control.cs:96`), so it fires twice.
- **Upstream:** `ParentInternal` setter only calls `value.Controls.Add(this)` (`Control.cs:2701-2725`).
- **Impact:** ParentChanged handlers (binding-context refresh, ambient-font recalculation, DockPanelSuite's
  reparent bookkeeping) execute twice per `ctl.Parent = x`.
- **Fix:** delete the trailing `OnParentChanged` call in the setter.
- **Test:** count ParentChanged across `c.Parent = panel` → 1.
- **Tests today:** none (grep `ParentChanged` in tests = 0).

### CTL-16 — `Control.Dispose(bool)` / `Disposing` — Cat A — P2 — High
- **Ours:** never sets `States.Disposing` (`src/Majorsilence.Forms/Control.cs:2697-2740`), so `Disposing` is always
  false and `GetAnyDisposingInHierarchy()` never short-circuits `AssignParent`; disposing a parent therefore runs
  `parent.Controls.Remove(this)` for each child → `OnParentChanged`, `OnVisibleChanged`, `OnParentVisibleChanged` cascade,
  `PerformLayout` on the half-torn-down parent, per child.
- **Upstream:** `Dispose` sets `Disposing` first (`Control.cs:4770`, `:4808`) and `AssignParent` returns after
  `OnParentChanged` when anything in the chain is disposing (`Control.cs:4256-4259`).
- **Impact:** user VisibleChanged/Layout handlers run against disposed siblings during `form.Dispose()`; the standard
  guard `if (Disposing || IsDisposed) return;` cannot detect the teardown.
- **Fix:** `SetState(States.Disposing, true)` at the top of `Dispose(bool)` and `States.Disposed` at the end;
  `IsDisposed` can then read the state flag instead of `_isDisposed`.
- **Test:** parent with child; hook child.VisibleChanged; `parent.Dispose()` → count 0; `Assert.True(parent.Disposing)`
  inside a child's Disposed handler.
- **Tests today:** ControlDisposeUnparentsTests (unparenting only).

### CTL-17 — `GroupBox.AutoSize` — Cat E — P2 — High
- **Ours:** `public new bool AutoSize { get; set; }` hides `Control.AutoSize` (`src/Majorsilence.Forms/GroupBox.cs:66`):
  `gb.AutoSize = true` stores a bool the layout engine never reads, and `((Control)gb).AutoSize` reads a different
  value.
- **Upstream:** `public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }`
  (`Controls/GroupBox/GroupBox.cs:59-63`) — a real auto-size that grows to fit children.
- **Impact:** designer-set `AutoSize = true` GroupBoxes stay at their design size; anything enumerating
  `Controls.OfType<Control>().Where(c => c.AutoSize)` disagrees with the typed property.
- **Fix:** replace with `public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }`.
- **Test:** `gb.AutoSize = true; Assert.True(((Control)gb).AutoSize)`; add a child at Bottom=300 → gb.Height grows.
- **Tests today:** GroupBoxTests.AutoSize_Set_GetReturnsExpected (round-trip only).

### CTL-18 — `Panel.GetPreferredSize` — Cat A — P2 — Medium
- **Ours:** overrides the public method (`src/Majorsilence.Forms/Panel.cs:41-60`), bypassing `Control.GetPreferredSize`'s
  `ApplySizeConstraints` (MinimumSize/MaximumSize) and ignoring `Padding`; `UserControl`/`ContainerControl` inherit it.
- **Upstream:** `Panel` overrides `GetPreferredSizeCore` only, and `DefaultLayout.GetPreferredSize` includes the
  container `Padding` and anchor margins (`Panels/Panel.cs:28-45`, `Layout/DefaultLayout.cs:669-690`, `:1110-1140`).
- **Impact:** an AutoSize Panel/UserControl with `Padding` clips its children by the padding and ignores `MinimumSize`.
- **Fix:** rename the override to `internal override Size GetPreferredSizeCore` and add `Padding.Horizontal/Vertical`.
- **Test:** AutoSize Panel, `Padding = 10`, `MinimumSize = (300, 0)`, child at Right=50 → PreferredSize.Width == 300;
  without MinimumSize → 60.
- **Tests today:** none for Panel preferred size.

### CTL-19 — `Control.BeginInvoke(Delegate)` / `EndInvoke` — Cat A — P2 — High
- **Ours:** returns `new System.Threading.Tasks.Task(() => { })`, a task that is never started
  (`src/Majorsilence.Forms/Control.Compat.cs:236-253`); `EndInvoke => null` (`:256`).
- **Upstream:** returns a `ThreadMethodEntry` whose `IsCompleted`/`AsyncWaitHandle` complete when the callback runs;
  `EndInvoke` pumps or waits and returns the result/rethrows (`Control.cs:4368`, `:5039-5064`).
- **Impact:** `ar.AsyncWaitHandle.WaitOne()` and `while (!ar.IsCompleted)` hang forever; `EndInvoke(ar)` returns null
  instead of the value or the exception.
- **Fix:** back it with a `TaskCompletionSource<object?>` completed inside the posted action; `EndInvoke` returns
  `((Task<object?>)ar).GetAwaiter().GetResult()`, and when called on the UI thread runs pending posts first.
- **Test:** `var ar = c.BeginInvoke(new Func<int>(() => 42)); Application.DoEvents(); Assert.True(ar.IsCompleted);
  Assert.Equal(42, c.EndInvoke(ar))`.
- **Tests today:** none.

### CTL-20 — `Control.Handle` / `CreateHandle` / `RecreateHandle` — Cat B — P2 — High
- **Ours:** `Handle => IntPtr.Zero`, `CreateHandle() { }`, `RecreateHandle() { }` (`src/Majorsilence.Forms/Control.Compat.cs:320-323`,
  `KryptonPortParity.Two.cs:81`); `IWin32Window.Handle` also Zero (`Control.cs:16`).
- **Upstream:** reading `Handle` creates the handle (`Control.cs:2201-2214`) — the idiom `var _ = Handle;` /
  `CreateHandle()` exists precisely to make `IsHandleCreated` true and fire `HandleCreated` early; `RecreateHandle`
  re-raises HandleDestroyed/HandleCreated (`Control.cs:9216`).
- **Impact:** code that forces the handle then checks `IsHandleCreated` (or waits for HandleCreated) before
  `BeginInvoke` skips its work; controls that rebuild native state in `OnHandleCreated` after `RecreateHandle()` never
  rebuild. `MessageBox.Show(this)` owner matching by handle cannot work (all controls share Zero).
- **Fix:** `Handle` getter and `CreateHandle()` → `CreateControl()` if not Created and return a stable non-zero token
  (`GetHashCode() | 1`, as `WindowBase.Handle` already does at `WindowBase.cs:669`); `RecreateHandle` →
  `OnHandleDestroyed` + `OnHandleCreated`.
- **Test:** `var c = new Control(); _ = c.Handle; Assert.True(c.IsHandleCreated)`; HandleCreated count 1.
- **Tests today:** KryptonPortParityTests/TailParityTests (Handle exists), none for creation side-effect.

### CTL-21 — `Control.Refresh` / `Update` — Cat A — P2 — High
- **Ours:** both `=> Invalidate()` (`src/Majorsilence.Forms/Control.Compat.cs:31`, `:450`); nothing paints until the
  backend's next frame.
- **Upstream:** `Update()` → `UpdateWindow` paints synchronously; `Refresh()` = `Invalidate(true) + Update()`
  (`Control.cs:9432-9436`, `:10840-10846`).
- **Impact:** the classic progress idiom `label.Text = ...; label.Refresh();` inside a blocking loop shows nothing
  until the loop ends; `DrawToBitmap` after `Update()` sees stale pixels.
- **Fix:** `Update()` → if Created and dirty, render this control's back buffer now (`RaisePaintBackground/RaisePaint`
  into `GetBackBuffer()`) and ask the window to present (`FindWindow()?.Present()` or equivalent flush).
- **Test:** hook `Paint`, set Text, call `Refresh()` → Paint count incremented synchronously (no `DoEvents`).
- **Tests today:** none.

### CTL-22 — `Control.Cursor` setter — Cat A — P2 — High
- **Ours:** stores and raises `CursorChanged`; the cursor is only pushed to the window in `OnMouseEnter`
  (`src/Majorsilence.Forms/Control.cs:419-438`, `:1310`).
- **Upstream:** setter sends WM_SETCURSOR immediately when the pointer is inside the control or it has capture
  (`Control.cs:1570-1585`).
- **Impact:** `Cursor = Cursors.WaitCursor` while the mouse is already over the control (the normal case: user just
  clicked it) shows nothing until the pointer leaves and re-enters; same for `Cursors.Hand` toggled from MouseMove.
- **Fix:** in the setter and in `OverrideCursor`, if `IsHovering`/`current_mouse_in` chain includes this or
  `Capture`, call `FindForm()?.SetCursor(Cursor)` (and the backend push at `WindowBase.cs:567`).
- **Test:** headless form, move pointer into control, set `Cursor = Cursors.Wait`, assert `form.current_cursor` is Wait.
- **Tests today:** CursorTests / ControlExtensibilityHookTests (ResetCursor) — none for live application.

### CTL-23 — `Control.Region` / `RegionChanged` — Cat C/D — P2 — High
- **Ours:** `Region { get; set; }` auto-property (`src/Majorsilence.Forms/Control.Compat.cs:621`); `OnRegionChanged`
  exists (`ControlAndFormParity.cs:401`) but is never called; painting and hit-testing ignore it.
- **Upstream:** setter applies `SetWindowRgn` (clips painting and hit-testing) and raises `OnRegionChanged`
  (`Control.cs:2801-2840`).
- **Impact:** rounded/elliptical custom buttons built with `GraphicsPath` + `Region` render as full rectangles and
  accept clicks in the corners; RegionChanged handlers never run.
- **Fix:** store, raise `OnRegionChanged`, `Invalidate()`; in `PaintChildren` clip the canvas to the region before
  `DrawBitmap`; in `FindVisibleChildAt` test `Region.IsVisible(local)` when set.
- **Test:** control 100×100 with a 50×50 Region; render parent; pixel at (90,90) is parent colour; `GetChildAtPoint(90,90)`
  is null; RegionChanged count 1.
- **Tests today:** WindowRegionTests / TransparentBackgroundPaintTests (Form region; not Control).

### CTL-24 — `Control.HScroll` / `VScroll` — Cat A — P2 — High
- **Ours:** public settable auto-properties on `Control` (`src/Majorsilence.Forms/Control.Compat.cs:609-612`);
  `ScrollableControl` never updates them.
- **Upstream:** protected on `ScrollableControl`, reflecting whether each bar is currently shown
  (`Scrolling/ScrollableControl.cs:232-256`).
- **Impact:** derived scrollable controls that branch on `if (VScroll)` (to reserve bar width in custom paint or
  in `ScrollControlIntoView`-style code) always take the false branch.
- **Fix:** move to `ScrollableControl` as `protected bool HScroll => hscrollbar.Visible;` (setter toggles visibility).
- **Test:** AutoScroll panel with overflow → `Assert.True(VScroll)` via a test subclass.
- **Tests today:** none.

### CTL-25 — `Control.TopLevelControl` — Cat A — P2 — High
- **Ours:** walks to the parentless root and returns it — the internal `ControlAdapter`
  (`src/Majorsilence.Forms/Control.Compat.cs:511-517`).
- **Upstream:** returns the first control with `GetTopLevel()` — the Form (`Control.cs:3276-3290`).
- **Impact:** `TopLevelControl as Form` is null; `TopLevelControl.Text`/`.Bounds` give the adapter's, not the window's.
- **Fix:** if the root is a `ControlAdapter` return `adapter.ParentForm as Form` (or a `Control`-shaped proxy the
  Form already exposes); otherwise the first ancestor with `States.TopLevel`.
- **Test:** `form.Controls.Add(c); Assert.Same(form, c.TopLevelControl)` (or `FindForm()` equivalence).
- **Tests today:** TopLevelControlTests (covers `SetTopLevel` hosting, not this property).

### CTL-26 — `Control.RaiseClick` right-click with a context menu; focus on any button — Cat A — P2 — Medium
- **Ours:** with `ContextMenu != null` a right-button release shows the menu and returns before `OnClick`/`OnMouseClick`
  (`src/Majorsilence.Forms/Control.cs:1767-1770`); `RaiseMouseDown` calls `Select()` for every button (`:1911`).
- **Upstream:** the menu is opened from WM_CONTEXTMENU independently; Click/MouseClick still fire for the right button
  (`Control.cs:11675-11700`); only a left press focuses (`Control.cs:11510-11513`).
- **Impact:** `MouseClick` handlers that check `e.Button == Right` to do their own thing never run once a
  ContextMenuStrip is assigned; right-clicking a Label steals focus from the TextBox being edited.
- **Fix:** raise `OnClick`/`OnMouseClick` first and then show the menu; gate `Select()` on `e.Button == Left`.
- **Test:** control with ContextMenuStrip; right-click → MouseClick count 1. Right-click a non-focused control →
  previously focused control still `Focused`.
- **Tests today:** MenuClickReproTests (menu opens), none for the swallowed click.

### CTL-27 — `UserControl` click on empty area moves focus to the container — Cat A — P2 — Medium
- **Ours:** `UserControl` sets `Selectable` and inherits `RaiseMouseDown → Select()` (`src/Majorsilence.Forms/UserControl.cs:17`,
  `Control.cs:1911`), so clicking its background deselects the child that had focus.
- **Upstream:** `UserControl.OnMouseDown` only calls `Focus()` when `!FocusInside()` (`UserControl.cs:292-299`).
- **Impact:** clicking blank space inside a UserControl fires Leave/Validating on its own TextBox.
- **Fix:** override `RaiseMouseDown`/`OnMouseDown` in UserControl (and ContainerControl): skip `Select()` when
  `ContainsFocus`.
- **Test:** UserControl with focused child; `RaiseMouseDown` at an empty point → child still Focused.
- **Tests today:** none.

### CTL-28 — `Control.ProcessCmdKey` / `ProcessDialogKey` / `IsInputKey` / `ProcessMnemonic` / `ProcessDialogChar` — Cat B — P1 — High
- **Ours:** all declared `=> false` and **never called** by the input pipeline: `RaiseKeyDown`/`RaiseKeyPress` go
  straight from the adapter to `OnPreviewKeyDown`/`OnKeyDown` (`src/Majorsilence.Forms/Control.cs:1817-1875`,
  `Control.Compat.cs:645-700`; grep for callers = 0).
- **Upstream:** `PreProcessMessage` calls `ProcessCmdKey` (walking parents), then `IsInputKey`/`ProcessDialogKey`,
  `IsInputChar`/`ProcessDialogChar`/`ProcessMnemonic` before the key events are raised (`Control.cs` PreProcessMessage
  and `ProcessKeyPreview` chain).
- **Impact:** the single most common keyboard override in ported code — `protected override bool ProcessCmdKey(ref
  Message msg, Keys keyData)` for Ctrl+S / Escape / Enter-as-Tab in grids and editors — never runs on a Control;
  `IsInputKey(Keys.Tab)` returning true cannot keep Tab inside a custom editor; Alt-mnemonics on custom buttons don't work.
- **Fix:** in `ControlAdapter.RaiseKeyDown` before the Tab handling: walk from `SelectedControl` up through parents
  calling `ProcessCmdKey(ref msg, e.KeyData)` (synthesize a `Message` with WM_KEYDOWN); if false and `!IsInputKey`,
  call `ProcessDialogKey`; only then dispatch. Mirror for `ProcessDialogChar`/`ProcessMnemonic` in `RaiseKeyPress`.
- **Test:** control overriding `ProcessCmdKey` to record and return true for `Keys.F5`; send F5 via the window's key
  handler → recorded, and `KeyDown` not raised.
- **Tests today:** none.

### CTL-29 — `Control.BindingContextChanged` / `OnBindingContextChanged` — Cat D — P2 — High
- **Ours:** event is `add { } remove { }` (`src/Majorsilence.Forms/Control.Events.cs:591`); `OnBindingContextChanged` is
  an empty virtual nothing calls (`KryptonPortParity.cs:76`); `BindingContext` setter stores only
  (`Control.Compat.cs:495-498`).
- **Upstream:** raised from the `BindingContext` setter, from `CreateControl` and from `AssignParent` when the
  control has no local context (`Control.cs:4626-4641`, `:4289-4297`).
- **Impact:** data-bound controls (and `ListControl`/`DataGridView` ports) that (re)bind in `OnBindingContextChanged`
  never bind; handlers attached to the event are silently dropped.
- **Fix:** make it an `Events`-backed event; raise from the setter, from `CreateControl` when `binding_context is
  null && Parent is not null`, and cascade to children without a local context in `AssignParent`.
- **Test:** attach handler, `c.BindingContext = new BindingContext()` → count 1; `panel.Controls.Add(c)` on a created
  panel → count 1.
- **Tests today:** none (WindowDataBindingParityTests covers Form-level binding).

### CTL-30 — `Control.ScrollControlIntoView` — Cat B — P2 — High
- **Ours:** empty (`src/Majorsilence.Forms/Control.Compat.cs:456`; listed in NoOpStubBaseline). Flagged here despite
  the baseline because nothing else provides the behaviour: `ScrollableControl` has no override and focus changes never
  scroll.
- **Upstream:** `ScrollableControl.ScrollControlIntoView` moves the display rect so the control is visible and is
  called by `ContainerControl` on every focus change (`Scrolling/ScrollableControl.cs:815-833`).
- **Impact:** tabbing through a tall AutoScroll form leaves the focused TextBox off-screen; explicit calls in code
  (`panel.ScrollControlIntoView(selectedRow)`) do nothing.
- **Fix:** implement on `ScrollableControl` (compute child bounds relative to this, set `AutoScrollPosition`), and call
  it from `ControlAdapter.SelectedControl`'s setter for each `ScrollableControl` ancestor with AutoScroll.
- **Test:** AutoScroll Panel 100 tall, child at Top=300; `panel.ScrollControlIntoView(child)` → `AutoScrollPosition.Y <= -200`.
- **Tests today:** none.

### CTL-31 — `Control.CanFocus` / `Focused` while hidden or disabled — Cat A — P2 — High
- **Ours:** because CTL-02's `SelectNextIfFocused` is dead, `Visible = false`/`Enabled = false` on the focused
  control leaves `Selected` true, so `Focused`, `ContainsFocus` and `adapter.SelectedControl` still report it and
  `RaiseKeyDown`/`RaiseKeyPress` keep routing keys there (`src/Majorsilence.Forms/Control.cs:1817-1875`,
  `:2359-2364`, `:2445-2462`).
- **Upstream:** focus moves to the next TabStop (`Control.cs:1852-1855`, `:10027-10039`); `Focused` is false for a
  hidden window.
- **Impact:** disabling the Save button that has focus swallows keyboard input; `ContainsFocus` returns true for a
  hidden panel.
- **Fix:** falls out of CTL-02; additionally make `Focused => Selected && Visible && Enabled`.
- **Test:** `btn.Focus(); btn.Enabled = false; Assert.False(btn.Focused); Assert.True(nextTabStop.Focused)`.
- **Tests today:** none.

## Low-priority / Win32-only (P3) — one line each
- `Control.SetStyle(ControlStyles.Opaque)` — background is always painted first; upstream skips `OnPaintBackground`. Cosmetic only when a control paints partially (`Control.cs:8360`).
- `Control.SetStyle(ControlStyles.SupportsTransparentBackColor)` — upstream `BackColor` setter throws for alpha < 255 without it (`Control.cs:904`); ours accepts. Lenient, not harmful.
- `ControlStyles.UserPaint / AllPaintingInWmPaint / DoubleBuffer / OptimizedDoubleBuffer / UserMouse / CacheText / EnableNotifyMessage / FixedWidth / FixedHeight` — HWND paint/message semantics; every control here is already double-buffered. `ResizeRedraw`, `StandardClick`, `StandardDoubleClick` ARE consumed (Control.cs:1547, 1774, 1795).
- `Control.DoubleBuffered` — always true here; harmless.
- `Control.UpdateStyles()` — re-reads `CreateParams` upstream; no window style to re-apply.
- `Control.WndProc` / `DefWndProc` / `OnNotifyMessage` / `PreProcessMessage` / `CreateParams` — documented Win32 non-goal.
- `Control.AllowDrop` — stored-only; no OS drag source exists (`DoDragDrop` returns None), documented in COMPATIBILITY_MATRIX.
- `Control.IsMirrored` — always false; RTL mirroring is a Win32 layout flag.
- `Control.GetChildAtPoint` — also returns implicit scrollbars/size grip; upstream only real children. Minor.
- `Control.MouseHover` — fires immediately on enter instead of after `MouseHoverTime`; documented in the event's own remarks.
- `Control.Scroll` event on `Control` is `add { } remove { }` and `ScrollableControl` re-declares it with `EventHandler<ScrollEventArgs>` instead of `ScrollEventHandler` — a base-typed subscription is dropped; `new ScrollEventHandler(...)` fails to compile (loud, not silent).
- `Control.QueryAccessibilityHelp`, `SystemColorsChanged`, `StyleChanged`, `ChangeUICues` — never raised; OS notifications with no portable source.
- `UserControl.DefaultSize` — inherits Panel's 200×100 (upstream 150×150); designers always set Size explicitly.
- `UserControl : Panel` / `ContainerControl : Panel` — `uc is Panel` is true here (false upstream); only affects type tests.
- `ContainerControl.AutoScaleMode` / `AutoScaleDimensions` — `PerformAutoScale` exists but nothing calls it on load; font-based auto-scaling is a GDI metric concept and the layer scales by DPI instead.
- `Control.AccessibleName/Description/Role`, `AccessibilityNotifyClients` — stored; UIA plumbing is separate.
- `Control.DrawToBitmap` — empty (in baseline); `OnPrint` exists and could implement it in ~5 lines.

## Systemic patterns
- **Two backing stores for one concept.** `ControlStyles.Selectable` vs `ControlBehaviors.Selectable` (CTL-03),
  `UseWaitCursor` property vs `States.UseWaitCursor` (CTL-06), `AutoScrollMargin` property vs `auto_scroll_margin`
  field (CTL-11), `GroupBox.AutoSize` vs `Control.AutoSize` (CTL-17), `HScroll/VScroll` vs scrollbar visibility
  (CTL-24). Sweep: grep every `{ get; set; }` on Control/ScrollableControl/UserControl and check whether a sibling
  field/state flag with the same name exists.
- **`IsFocusManagingContainerControl => false` disables an entire subsystem.** GetContainerControl, SelectNextIfFocused,
  ContainerControl.Select, ActiveControl, Validate, ScrollControlIntoView-on-focus all hang off it (CTL-02, 07, 08,
  09, 30, 31). Fixing it and giving `ControlAdapter` an `IContainerControl` implementation unblocks all of them.
- **Validation is attached to the wrong event.** Running Validating inside `OnLostFocus` (CTL-01) cannot honour Cancel,
  CausesValidation or AutoValidate because by then focus has moved; it must live in the focus-switch choke point
  (`ControlAdapter.SelectedControl`).
- **"Created" means "parented", not "shown".** `ControlAdapter.Visible` is unconditionally true, so Add ⇒ CreateControl
  ⇒ HandleCreated/Load during InitializeComponent, and hidden children are created too (CTL-05). A test currently pins
  this.
- **Unparented `Visible` is false.** Drives double VisibleChanged on Add, spurious VisibleChanged on Remove and a
  storm during Dispose (CTL-14, CTL-16); also why `SetTopLevel` had to read the raw state flag.
- **Notification raised twice / from two places.** Parent setter + AssignParent (CTL-15); Insert's explicit
  OnVisibleChanged + AssignParent (CTL-14). Sweep: any `On*Changed(EventArgs.Empty)` call that follows a call which
  itself raises the same notification.
- **Mouse pipeline built on release-only click counting.** `Clicks` is never 2 on press and Click is not suppressed on
  the second release (CTL-04); context-menu and focus rules are keyed on "any button" (CTL-26/27).
- **Async/handle stubs that return "done" without doing.** `BeginInvoke(Delegate)`'s unstarted Task (CTL-19), `Handle`/
  `CreateHandle`/`RecreateHandle` (CTL-20), `Refresh`/`Update` (CTL-21): each returns something plausible so callers
  proceed on a false premise.
- **Logical vs device split at `ClientRectangle`.** Bounds/mouse/DisplayRectangle are logical, ClientRectangle/
  ClientSize/PaddedClientRectangle are device (CTL-10); the `ClientSize` setter subtracts across the two units. Every
  test runs at scale 1, so none of this is covered.
