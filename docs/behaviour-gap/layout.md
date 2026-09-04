# Layout and container controls — findings

## Summary
The area splits cleanly in two. The `Layout/` directory is a genuinely faithful, near-line-for-line port of
upstream's layout engines: normalized diffs of `DefaultLayout`, `FlowLayout`, `TableLayout`, `CommonProperties`,
`LayoutUtils` and `LayoutTransaction` against their upstream counterparts come back with almost nothing but
naming, `#if DEBUG` blocks and the opt-in AnchorLayoutV2 path missing — the anchor distance math, the reverse
z-order dock walk, the cached-bounds machinery, the TableLayout style-distribution algorithm and the flow
row-measuring loop are all intact. The damage is almost entirely at the *edges* of that engine: the container
controls that are supposed to feed it, and the `Control` members that are supposed to consume it. The dominant
failure pattern is a container that overrides the wrong hook or nothing at all — `Panel.GetPreferredSize`
replaces the layout engine with a hand-rolled child-bounds scan (and `FlowLayoutPanel`/`TableLayoutPanel`
inherit it), `ButtonBase` never measures its content, `Control.Scale` never calls `ScaleControl`, and
`TableLayoutPanel`'s entire paint region is commented out. The second pattern is the stored-only property:
`SplitContainer` alone has eight (`FixedPanel`, `Panel1MinSize`, `Panel2MinSize`, `IsSplitterFixed`,
`SplitterIncrement`, `BorderStyle`, and three AutoScroll members), `TabControl` has nine in a row. Third is the
shadowing `new`/stored auto-property that silently forks state from its base — `TabPage.Enabled`,
`GroupBox.AutoSize`, `TabPage.UseVisualStyleBackColor`. `SplitContainer` and `Splitter` are effectively
re-implementations rather than ports and diverge on almost every member; `TabControl` fires its four selection
events in a different order from Windows. Counts: 2 P0, 18 P1, 17 P2 (37 findings).

## Findings

> **Status — W5.22, 2026-09-04.** `LAY-01`, `LAY-02`, `LAY-03`, `LAY-04`, `LAY-05`, `LAY-07` and
> `LAY-08` are **CLOSED**. The min sizes are the real clamp, `FixedPanel` drives an `OnLayout`
> redistribution, both splitter events are raised from both classes' drag paths, `Splitter.SplitPosition`
> is the docked sibling's extent, the legacy `Splitter` resizes that sibling on all four dock edges,
> the panels are `SplitterPanel`, and the constructor no longer forces `Dock = Fill`. Covered by
> `tests/Majorsilence.Forms.Tests/SplitContainerBehaviourTests.cs`.
>
> **Two corrections to the text below**, both recorded where they belong:
> * `LAY-05`'s suggested fix says to find the sibling as "the previous sibling by z-order". That is
>   wrong. The dock walk runs in *reverse* z-order, so the control adjacent to the bar is generally not
>   the one before it in `Controls` — inside `SplitContainer` it is two places *after* it. Upstream
>   matches *edges* (`Splitter.FindTarget`), and so does the implementation.
> * `LAY-08` says the ctor should be changed so `DefaultSize` is 150x100. It already was; only the
>   `Dock` and the default `SplitterDistance` needed fixing.
>
> `LAY-06` is still open and was never part of W5.22.

### LAY-01 — `SplitContainer.Panel1MinSize` / `Panel2MinSize` — Cat C — P1 — High
- **Ours:** Plain auto-properties that nothing reads (`src/Majorsilence.Forms/SplitContainer.cs:167,170`). The *working* minimum is held in `Panel1MinimumSize`/`Panel2MinimumSize` (`SplitContainer.cs:93,110`) — names that do not exist in WinForms — which feed `GetMaximumPanel1Size()`/`ResizePanels()` (`SplitContainer.cs:47,120`). So the WinForms-spelled property is inert and the enforced minimum is always the hard-coded 25.
- **Upstream:** `Panel1MinSize`/`Panel2MinSize` are the real clamps: the setter calls `SetInnerMostBorder`/`ApplyPanel1MinSize` and re-clamps `SplitterDistance` (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/SplitContainer.cs:577,610,1272`).
- **Impact:** Designer files emit `splitContainer1.Panel1MinSize = 150;`. Ours accepts it and still lets the splitter be dragged down to 25px, so panels collapse past the point the app expects and child controls clip.
- **Fix:** Make `Panel1MinSize`/`Panel2MinSize` the backing store (delete `Panel1MinimumSize`/`Panel2MinimumSize` or make them forwarding aliases) and re-run `ResizePanels(SplitterDistance)` in the setter.
- **Test:** `sc.Panel1MinSize = 150; sc.SplitterDistance = 10;` then assert `sc.SplitterDistance == 150`.
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitContainerTests.cs` (does not cover MinSize)

### LAY-02 — `SplitContainer.FixedPanel` — Cat C — P1 — High
- **Ours:** `public FixedPanel FixedPanel { get; set; } = FixedPanel.None;` (`src/Majorsilence.Forms/SplitContainer.cs:151`) — never read anywhere in the file. Panel1 is `Dock=Left` with a fixed Width, so on container resize Panel1 always keeps its size, i.e. the control behaves as if `FixedPanel.Panel1` were permanently set.
- **Upstream:** `FixedPanel` drives the whole resize path — `SplitContainer.cs:339` (setter recomputes ratios) and the `OnResize`/`SetSplitterRect` logic at `SplitContainer.cs:1617-1700`, where `FixedPanel.None` preserves the *ratio* (`_ratioWidth`/`_ratioHeight`), `Panel1` pins Panel1's size, `Panel2` pins Panel2's.
- **Impact:** The default (`FixedPanel.None`) should keep the split proportional as the form resizes; ours keeps Panel1 pixel-fixed. Maximising a form that used the default puts all the extra width into Panel2 — a visible layout difference on almost every SplitContainer-based app. Apps that set `FixedPanel.Panel2` get the exact opposite of what they asked for.
- **Fix:** Store the split ratio on resize; in an `OnResize`/`OnLayout` override recompute `SplitterDistance` per `FixedPanel` mirroring `SplitContainer.cs:1617-1700`.
- **Test:** `sc.Size = new Size(200,100); sc.SplitterDistance = 100; sc.Size = new Size(400,100);` assert `SplitterDistance == 200` for `FixedPanel.None` and `== 100` for `FixedPanel.Panel1`.
- **Tests today:** none

### LAY-03 — `SplitContainer.SplitterMoving` / `SplitterMoved` — Cat D — P1 — High
- **Ours:** Both declared on `SplitContainer` (`src/Majorsilence.Forms/SplitContainer.cs:179,182`) and never invoked; `Splitter_Drag` (`SplitContainer.cs:185`) resizes the panel and calls only `Invalidate()`. On `Splitter` itself they are worse — `add { } remove { }` accessors that silently discard the handler (`src/Majorsilence.Forms/Splitter.cs:137,140`), so even `-=`/reflection cannot observe them.
- **Upstream:** `OnSplitterMoving` is raised on every mouse-move during the drag and `OnSplitterMoved` at the end (`src/System.Windows.Forms/System/Windows/Forms/Controls/Splitter/Splitter.cs:419,828,843,858`); `SplitContainer` raises the same pair from `OnMouseMove`/`OnMouseUp` and honours `SplitterCancelEventArgs.Cancel`.
- **Impact:** Apps that persist the splitter position on `SplitterMoved`, or that veto a drag in `SplitterMoving`, do nothing. The `add {} remove {}` form on `Splitter` is the highest-risk shape: it looks wired at compile time and leaks nothing at runtime.
- **Fix:** Raise `SplitterMoving` (checking `.Cancel`) from `Splitter_Drag` before `ResizePanels`, and `SplitterMoved` after the drag finishes; give `Splitter`'s two events real backing fields and raise them from `OnMouseMove`/`OnMouseUp`.
- **Test:** attach a handler, synthesise mouse down/move/up on the splitter, assert both events fired with the new `SplitX`/`SplitY`.
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitterTests.cs` (drag delta only)

### LAY-04 — `Splitter.SplitPosition` — Cat A — P1 — High
- **Ours:** `public int SplitPosition { get => SplitterWidth; set => SplitterWidth = value; }` (`src/Majorsilence.Forms/Splitter.cs:120`) — aliased to the splitter's own **thickness**.
- **Upstream:** `SplitPosition` is the size of the *control the splitter is docked against* (the previous sibling): the setter clamps to `_minSize`/`_maxSize`, rewrites `spd._target.Bounds` per `Dock`, and raises `OnSplitterMoved` (`src/System.Windows.Forms/System/Windows/Forms/Controls/Splitter/Splitter.cs:361-420`).
- **Impact:** `splitter1.SplitPosition = 200;` produces a 200-pixel-thick splitter bar instead of a 200-pixel-wide left panel — a spectacular visual corruption, and the reading direction (`get`) reports the bar width to code restoring a saved layout.
- **Fix:** Implement `SplitPosition` against the previous sibling in `Parent.Controls`, clamped by `MinSize`/`MinExtra`, and raise `SplitterMoved`; keep `SplitterWidth` separate.
- **Test:** two panels + splitter in a parent; set `SplitPosition = 200`; assert the *previous sibling*'s Width is 200 and `splitter.Width` is unchanged.
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitterTests.cs`

### LAY-05 — `Splitter` does not resize its docked sibling — Cat B — P1 — High
- **Ours:** `Splitter` only raises a bespoke `Drag` event carrying a delta (`src/Majorsilence.Forms/Splitter.cs:47,60-71`). Nothing in `Splitter` touches any sibling; only `SplitContainer` subscribes. `MinSize` and `MinExtra` are stored and never consulted (`Splitter.cs:123,130`).
- **Upstream:** The legacy `Splitter` exists precisely to resize the previous docked sibling: `CalcSplitBounds`/`CalcSplitSize`/`ApplySplitPosition` (`Splitter.cs:926-948`) and the `SplitPosition` setter apply the new bounds to `spd._target`, bounded by `MinSize` (target) and `MinExtra` (remaining space).
- **Impact:** A migrated form using the classic "Panel(Dock=Left) + Splitter(Dock=Left) + Panel(Dock=Fill)" idiom shows a draggable-looking bar that never moves anything. Silent: no exception, correct-looking cursor.
- **Fix:** In `OnMouseMove`, locate the sibling along the dock edge and set its Width/Height by the delta, clamped by `MinSize`/`MinExtra`. **Correction (W5.22):** this originally said "the previous sibling by z-order", which is wrong — the dock walk runs in reverse z-order, so the adjacent control is generally not the previous one in `Controls`. Upstream matches facing *edges* (`Splitter.FindTarget`), which is what was implemented.
- **Test:** parent with left panel + splitter; simulate a 40px drag; assert the left panel grew 40px and stayed >= `MinSize`.
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitterTests.cs` (asserts the `Drag` event only)

### LAY-06 — `SplitContainer.IsSplitterFixed` / `SplitterIncrement` — Cat C — P2 — High
- **Ours:** Both plain auto-properties (`src/Majorsilence.Forms/SplitContainer.cs:161,176`). `Splitter_Drag` never checks `IsSplitterFixed`, so a "fixed" splitter still drags; `SplitterIncrement` never quantises the movement and there is no keyboard handling at all (no `OnKeyDown`/`ProcessDialogKey` override on `SplitContainer`).
- **Upstream:** `IsSplitterFixed` gates the mouse-capture path; `SplitterIncrement` quantises both mouse (`SplitContainer.cs:2117-2124`) and arrow-key moves (`SplitContainer.cs:955-968`, `SplitterIncrement` at `:742`).
- **Impact:** Read-only splitters remain user-draggable; keyboard accessibility for the splitter is absent.
- **Fix:** Early-return from `Splitter_Drag` when `IsSplitterFixed`; round the new distance to a multiple of `SplitterIncrement`; add arrow-key handling when the splitter has focus.
- **Test:** `sc.IsSplitterFixed = true;` simulate drag; assert `SplitterDistance` unchanged.
- **Tests today:** none

### LAY-07 — `SplitContainer.Panel1`/`Panel2` are `Panel`, not `SplitterPanel` — Cat E — P1 — High
- **Ours:** `public Panel Panel1 { get; }` / `Panel2` (`src/Majorsilence.Forms/SplitContainer.cs:88,105`), constructed as `new Panel { ... }` (`SplitContainer.cs:27,29`). A real `SplitterPanel : Panel` type exists but is never used by `SplitContainer` (`src/Majorsilence.Forms/MissingTypesParity.cs:363`).
- **Upstream:** `public SplitterPanel Panel1 { get; }` (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/SplitContainer.cs:479,600`), created in the ctor as `SplitterPanel(this)`.
- **Impact:** Migrated code and designer-generated code typed as `SplitterPanel` (`SplitterPanel p = sc.Panel1;`, `foreach (SplitterPanel p in ...)`) fails to compile or throws `InvalidCastException`. `SplitterPanel.Owner` is unreachable.
- **Fix:** Change the property type to `SplitterPanel` and construct `new SplitterPanel(this)`.
- **Test:** `Assert.IsType<SplitterPanel>(new SplitContainer().Panel1)`.
- **Tests today:** none

### LAY-08 — `SplitContainer` constructor forces `Dock = DockStyle.Fill` — Cat E — P1 — High
- **Ours:** `Dock = DockStyle.Fill;` is the first line of the constructor (`src/Majorsilence.Forms/SplitContainer.cs:25`), and the default `SplitterDistance` ends up being whatever `Panel1`'s default `Control` width is rather than a defined value.
- **Upstream:** `SplitContainer` has `Dock = None` (inherited default) and `DefaultSize` 150x100, with `_splitterDistance = 50` (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/SplitContainer.cs:47`).
- **Impact:** A SplitContainer dropped into a form with `Anchor` or explicit `Location`/`Size` (the designer's default) gets those overridden only if the designer also emits `Dock`; when the designer emits `Anchor` instead, the control fills the whole form. Also `sc.SplitterDistance` before any assignment reports the wrong default, so "restore saved distance, else use default" code lands somewhere else than on Windows.
- **Fix:** Drop the `Dock = DockStyle.Fill` from the ctor and initialise `Panel1` to a 50px extent so `SplitterDistance` defaults to 50. **Correction (W5.22):** the `DefaultSize` half of this finding was already correct in our code (`SplitContainer.DefaultSize` was already 150x100); only the `Dock` and the default distance were wrong.
- **Test:** `Assert.Equal(DockStyle.None, new SplitContainer().Dock); Assert.Equal(50, new SplitContainer().SplitterDistance);`
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitContainerTests.cs`

### LAY-09 — `SplitContainer.BorderStyle` / `AutoScroll` / `AutoScrollMargin` / `AutoScrollMinSize` / `AutoScrollPosition` — Cat C — P2 — High
- **Ours:** All five are plain auto-properties in the parity partial (`src/Majorsilence.Forms/TailParity.cs:214,217,220,223,226`) with no reader. `SplitContainerRenderer` (`src/Majorsilence.Forms/Renderers/SplitContainerRenderer.cs`) does not consult `BorderStyle`.
- **Upstream:** `BorderStyle` repaints and changes the client area / `_borderSize` used throughout the splitter math (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/SplitContainer.cs:237`); AutoScroll lives on each `SplitterPanel` and genuinely scrolls.
- **Impact:** `sc.BorderStyle = BorderStyle.Fixed3D` draws nothing and does not shrink the panels; `sc.Panel1.AutoScroll = true` content is clipped instead of scrolled (the `AutoScroll` here shadows nothing useful since it is declared on `SplitContainer` rather than the panels).
- **Fix:** Have the renderer draw the border and subtract it from `PaddedClientRectangle`; delete the four AutoScroll members from `SplitContainer` and let the `SplitterPanel` children inherit `ScrollableControl`'s real ones.
- **Test:** headless render of a `SplitContainer` with `BorderStyle.FixedSingle`; assert edge pixels differ from `BorderStyle.None`.
- **Tests today:** `tests/Majorsilence.Forms.Tests/TailParityTests.cs` (asserts round-trip only)

### LAY-10 — `SplitContainer.Orientation` setter leaves the splitter and Panel2 stale — Cat A — P2 — High
- **Ours:** The setter re-docks `Panel1` and swaps `Panel1.Size`, but sets `splitter.Orientation` (which itself re-docks the splitter) *inside* a `SuspendLayout` and never re-clamps against `Panel2MinimumSize` (`src/Majorsilence.Forms/SplitContainer.cs:71-84`). `Panel1.Size = new Size(Panel1.Height, Panel1.Width)` transposes rather than preserving the split ratio.
- **Upstream:** The `Orientation` setter recomputes the splitter rect, re-derives `SplitterDistance` from the ratio, updates the cursor and calls `UpdateSplitter()` (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/SplitContainer.cs:418`).
- **Impact:** Toggling orientation at runtime (a common "flip layout" menu item) leaves the split at a transposed pixel value that frequently exceeds `Panel2MinSize`, squashing Panel2 to nothing.
- **Fix:** After re-docking, call `ResizePanels(SplitterDistance)` so the clamp runs, and derive the new distance from the old ratio.
- **Test:** 400x100 container, `SplitterDistance = 300`, flip to `Horizontal`; assert `SplitterDistance <= Height - SplitterWidth - Panel2MinSize`.
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitContainerTests.cs`

### LAY-11 — `SplitContainer.SplitterDistance` setter clamps instead of validating — Cat A — P2 — High
- **Ours:** `set => ResizePanels(value)` silently clamps into `[Panel1MinimumSize, GetMaximumPanel1Size()]` (`src/Majorsilence.Forms/SplitContainer.cs:145`, `:120`).
- **Upstream:** The setter throws `ArgumentOutOfRangeException` for `value < 0` and refuses values that violate `Panel1MinSize`/`Panel2MinSize`, raising `SplitterMoved` when it does move (`src/System.Windows.Forms/System/Windows/Forms/Layout/Containers/SplitContainer.cs:634-700`).
- **Impact:** Negative or nonsense values are absorbed rather than surfacing the bug; more importantly `SplitterMoved` is not raised, so listeners never see programmatic moves.
- **Fix:** Validate `value >= 0`, honour the min sizes, and raise `SplitterMoved` after a successful change.
- **Test:** `Assert.Throws<ArgumentOutOfRangeException>(() => sc.SplitterDistance = -1);`
- **Tests today:** `tests/Majorsilence.Forms.Tests/SplitContainerTests.cs`

### LAY-12 — `TabPage.Enabled` — Cat E — P1 — High
- **Ours:** `public new bool Enabled { get; set; } = true;` (`src/Majorsilence.Forms/TabPage.cs:44`) — a `new` auto-property that shadows `Control.Enabled` and stores into its own field. Setting it neither disables the page's children nor greys the tab; `((Control)page).Enabled` still reports `true`. `TabStripItem.Enabled` (`src/Majorsilence.Forms/TabStripItem.cs:29`) exists and is never linked to it.
- **Upstream:** `public new bool Enabled { get => base.Enabled; set => base.Enabled = value; }` — the `new` exists only to change `[Browsable]`, the behaviour is `Control.Enabled` (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabPage.cs:302-306`).
- **Impact:** `tabPage1.Enabled = false;` — a very common way to lock a wizard step — is a complete no-op, and the page's controls stay interactive. Worse, the shadowing means the *base* value diverges: code that reads `Enabled` through a `Control` reference gets the opposite answer to code that reads it through `TabPage`.
- **Fix:** Replace with `public new bool Enabled { get => base.Enabled; set { base.Enabled = value; TabStripItem.Enabled = value; } }`.
- **Test:** `page.Enabled = false; Assert.False(((Control)page).Enabled); Assert.False(child.Enabled /* effective */);`
- **Tests today:** none

### LAY-13 — `TabControl` Selecting/Selected/Deselecting/Deselected order — Cat A — P1 — High
- **Ours:** `TabStrip_SelectedTabChanged` raises `Deselecting`, then `Selecting`, then swaps visibility, then `Deselected`, `SelectedIndexChanged`, `Selected` (`src/Majorsilence.Forms/TabControl.cs:255-296`).
- **Upstream:** two distinct phases. `WmSelChanging` raises `Deselecting` and, if not cancelled, `Deselected` — both **before** the selection changes (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabControl.cs:2022-2029`). Then the selection changes, and `WmSelChange` raises `Selecting`, then `Selected`, then `SelectedIndexChanged` (`TabControl.cs:1970-1977`).
- **Impact:** Two reorderings: (a) `Deselected` fires after `Selecting` and after the page swap, so a handler that saves the outgoing page's state sees `SelectedTab` already pointing at the new page; (b) `Selected` fires *after* `SelectedIndexChanged` instead of before it, so handlers wired to both run in the reverse order to Windows. Both are classic "works on Windows, subtly wrong here" bugs.
- **Fix:** In `TabStrip_SelectedTabChanged`, raise `Deselecting`/`Deselected` up-front, then swap, then `Selecting`/`Selected`/`SelectedIndexChanged` in that order; a cancelled `Deselecting` suppresses `Deselected` too.
- **Test:** record the event names into a list on a tab change; assert `["Deselecting","Deselected","Selecting","Selected","SelectedIndexChanged"]`.
- **Tests today:** `tests/Majorsilence.Forms.Tests/TabControlTests.cs`

### LAY-14 — `TabControl.ImageList` + `TabPage.ImageIndex`/`ImageKey` — Cat C — P1 — High
- **Ours:** `ImageList`, `ImageIndex`, `ImageKey` are all bare auto-properties (`src/Majorsilence.Forms/TabControl.cs:126`, `src/Majorsilence.Forms/TabPage.cs:35,38`). `TabStripItem` — the thing that actually paints a tab header — has no image member at all (`src/Majorsilence.Forms/TabStripItem.cs`, only Text/Padding/Bounds/Enabled/Tag), and `GetPreferredSize` (`TabStripItem.cs:42`) measures text only.
- **Upstream:** `TabPage.ImageIndex`/`ImageKey` update the native TCITEM and the tab is drawn with the image, widening the tab (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabPage.cs:232,260`).
- **Impact:** Every icon-in-tab UI loses its icons silently and the tabs are narrower than on Windows, changing the header layout / wrap points.
- **Fix:** Add `Image`/`ImageIndex` to `TabStripItem`, resolve through `TabControl.ImageList` when the page is added or the index changes, include the image extent in `GetPreferredSize`, and draw it in `TabStripRenderer`.
- **Test:** page with a 16x16 ImageList image; assert `tabControl.GetTabRect(0).Width` exceeds the text-only width and the rendered pixels contain the image colour.
- **Tests today:** none

### LAY-15 — `TabControl.Alignment` / `Multiline` / `Appearance` / `SizeMode` / `ItemSize` / `Padding` / `HotTrack` / `ShowToolTips` / `RightToLeftLayout` — Cat C — P1 (Alignment/ItemSize/SizeMode), P2 (rest) — High
- **Ours:** Nine consecutive plain auto-properties with "Stub in Majorsilence.Forms" comments (`src/Majorsilence.Forms/TabControl.cs:129-158`). None is read by `TabControl`, `TabStrip` or `TabStripRenderer`; the strip is unconditionally `Dock = DockStyle.Top` (`TabControl.cs:23`) and sizes itself from `DefaultSize.Height * RowCount` (`src/Majorsilence.Forms/TabStrip.cs:86-89`).
- **Upstream:** `Alignment` re-creates the handle with `TCS_BOTTOM`/`TCS_VERTICAL` and moves the strip; `ItemSize`/`SizeMode` control tab extents (`TCS_FIXEDWIDTH`/`TCS_RIGHTJUSTIFY`); `Appearance` switches to buttons/flat buttons; `Padding` insets each tab; `ShowToolTips` enables per-page tooltips from `TabPage.ToolTipText`.
- **Impact:** `Alignment = TabAlignment.Bottom` — extremely common — silently keeps the tabs on top. `ItemSize`/`SizeMode = Fixed` designer settings are ignored, so tab widths differ from the mockup. `Appearance = Buttons` renders as ordinary tabs.
- **Fix:** At minimum wire `Alignment` to the strip's `Dock` (Top/Bottom/Left/Right) and `ItemSize`/`SizeMode`/`Padding` into `TabStripItem.GetPreferredSize`; the rest can stay cosmetic.
- **Test:** `tc.Alignment = TabAlignment.Bottom;` assert `tc.GetTabRect(0).Top >= tc.DisplayRectangle.Bottom`.
- **Tests today:** none

### LAY-16 — `TabPage.ToolTipText` + `TabControl.ShowToolTips` — Cat C — P2 — High
- **Ours:** `ToolTipText` is a stored string (`src/Majorsilence.Forms/TabPage.cs:32`) never read; `TabStripItem` has no tooltip plumbing.
- **Upstream:** with `ShowToolTips` on, the tab header shows `TabPage.ToolTipText` (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabPage.cs:447`).
- **Impact:** Tab tooltips never appear. Cosmetic but users notice.
- **Fix:** Forward `ToolTipText` to a `TabStripItem.ToolTipText` and hook the strip's hover into the existing `ToolTip` infrastructure, gated on `TabControl.ShowToolTips`.
- **Test:** hover-simulate over tab 0 with `ShowToolTips = true`; assert the tooltip text.
- **Tests today:** none

### LAY-17 — `TabPage.UseVisualStyleBackColor` — Cat C — P2 — High
- **Ours:** `public new bool UseVisualStyleBackColor { get; set; }` (`src/Majorsilence.Forms/TabPage.cs:47`) — shadowing auto-property, never consulted when painting.
- **Upstream:** the setter calls `Invalidate(true)` and the paint path uses it to pick the themed tab background instead of `BackColor` (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabPage.cs:318-331`, `:605-611`).
- **Impact:** Designer files set `UseVisualStyleBackColor = true` on essentially every TabPage. Ours ignores it, so pages paint with the raw `BackColor` and the page background does not match the tab strip — a visible seam on every migrated tabbed form.
- **Fix:** Make the setter `Invalidate()`; in the page's paint path, pick the theme's tab-body colour when the flag is set.
- **Test:** headless render two pages, one with the flag; assert the client-area pixel differs and matches the strip's body colour.
- **Tests today:** none

### LAY-18 — `TabControl.GetTabRect(index)` fabricates a rectangle out of range — Cat A — P2 — High
- **Ours:** out-of-range indices return `new Rectangle(index * 100, 0, 100, 25)` (`src/Majorsilence.Forms/TabControl.cs:171-174`).
- **Upstream:** `ArgumentOutOfRangeException.ThrowIfNegative(index)` and `ThrowIfGreaterThanOrEqual(index, TabCount)` (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabControl.cs`, `GetTabRect`).
- **Impact:** Hit-testing / owner-draw loops that overrun the tab count get a plausible-looking rectangle instead of an exception, so the bug lands as mis-positioned drawing far from its cause.
- **Fix:** Throw `ArgumentOutOfRangeException` for `index < 0 || index >= TabCount`.
- **Test:** `Assert.Throws<ArgumentOutOfRangeException>(() => tc.GetTabRect(5))` on a 2-tab control.
- **Tests today:** `tests/Majorsilence.Forms.Tests/TabControlTests.cs`

### LAY-19 — `TabControl.SelectedIndex` does not validate — Cat A — P2 — Medium
- **Ours:** `set => tab_strip.SelectedIndex = value;` (`src/Majorsilence.Forms/TabControl.cs:100`); `TabStrip.SelectedIndex` (`src/Majorsilence.Forms/TabStrip.cs:246`) forwards to `Tabs.SelectedIndex` with no range check visible on the TabControl side.
- **Upstream:** `ArgumentOutOfRangeException.ThrowIfLessThan(value, -1)` before anything else (`src/System.Windows.Forms/System/Windows/Forms/Controls/TabControl/TabControl.cs`, `SelectedIndex` setter).
- **Impact:** `SelectedIndex = -5` is absorbed; the difference matters for code that relies on the throw to detect an empty TabControl.
- **Fix:** Add the `< -1` guard in `TabControl.SelectedIndex`.
- **Test:** `Assert.Throws<ArgumentOutOfRangeException>(() => tc.SelectedIndex = -2);`
- **Tests today:** `tests/Majorsilence.Forms.Tests/TabControlTests.cs`

### LAY-20 — `TabControl.HitTest(Point)` tests page bounds, not tab-header bounds — Cat A — P2 — High
- **Ours:** `TabPages.FirstOrDefault(tp => tp.Bounds.Contains(point))` (`src/Majorsilence.Forms/TabControl.cs:250`) — `TabPage.Bounds` is the page's client rectangle (Dock=Fill, i.e. the whole content area below the strip), so every point in the body "hits" whichever page happens to be first, and no point over a tab header hits anything.
- **Upstream:** the equivalent is `TCM_HITTEST` over the tab *headers*.
- **Impact:** Right-click-a-tab context menus (`var page = tc.HitTest(e.Location)`) select the wrong page or none. Common in MDI-style tabbed shells.
- **Fix:** Test `tab_strip.Tabs[i].Bounds` (offset into TabControl coordinates) and return the matching `TabPage`.
- **Test:** `Assert.Same(page1, tc.HitTest(tc.GetTabRect(0).Location + new Size(2,2)))` and `Assert.Null(tc.HitTest(tc.DisplayRectangle.Location))`.
- **Tests today:** none

### LAY-21 — `Control.Scale(SizeF)` / `Control.ScaleControl(SizeF, BoundsSpecified)` — Cat B — P0 — High
- **CLOSED 2026-08-31 (`W5.24`).** `Scale` wraps a `LayoutTransaction` and `ScaleCore` now dispatches
  through `ScaleControl`, which scales `Padding`, `Margin`, `MinimumSize`, `MaximumSize` and calls
  `DefaultLayout.ScaleAnchorInfo`. Min/max are lifted before the bounds are scaled and restored after,
  as upstream does -- a control sitting at its `MinimumSize` cannot otherwise grow, and a test pins
  that ordering.
- **Ours:** `public void Scale(SizeF factor) => ScaleCore(factor.Width, factor.Height);` (`src/Majorsilence.Forms/Control.cs:2207`). `ScaleCore` (`Control.cs:2212-2231`) scales **bounds only** and recurses into children by calling `c.ScaleCore(...)` directly. `ScaleControl(SizeF, BoundsSpecified)` exists (`src/Majorsilence.Forms/Control.Hooks.cs:366`) but a repo-wide grep finds **no caller** — it is dead code. `DefaultLayout.ScaleAnchorInfo` (`src/Majorsilence.Forms/Layout/DockAndAnchorLayout.cs:759`) is likewise never called. There is no `ScaleChildren` property.
- **Upstream:** `Scale(SizeF)` wraps a `LayoutTransaction`, calls `ScaleControl(factor, factor)`, then recurses through `ScaleChildren`/`ChildControls` (`src/System.Windows.Forms/System/Windows/Forms/Control.cs`, `Scale(SizeF factor)`); `ScaleControl` scales `Padding`, `Margin`, `MinimumSize`, `MaximumSize` and the window adornments, then applies the scaled bounds (`Control.cs`, `protected virtual void ScaleControl(SizeF, BoundsSpecified)`), and calls `DefaultLayout.ScaleAnchorInfo` so anchored children keep their edge distances.
- **Impact:** At any non-96 DPI (or `AutoScaleMode.Font` with a different system font), Padding, Margin, MinimumSize and MaximumSize keep their 96-DPI pixel values while bounds grow — FlowLayoutPanel/TableLayoutPanel gaps and control insets end up ~2/3 too small at 150%, `MinimumSize` clamps at the wrong pixel value, and anchored children snap to unscaled distances. Custom controls that override `ScaleControl` (the documented WinForms DPI hook) are never called at all — silently, since the override compiles.
- **Fix:** Rewrite `Scale(SizeF)` to mirror upstream: `LayoutTransaction` + `ScaleControl(factor, BoundsSpecified.All)` + recurse; move padding/margin/min/max scaling and the `DefaultLayout.ScaleAnchorInfo(this, factor)` call into `ScaleControl`; make `ScaleCore` delegate to it.
- **Test:** `p.Padding = new Padding(8); p.MinimumSize = new Size(50,50); p.Scale(new SizeF(2,2));` assert `Padding.All == 16` and `MinimumSize == (100,100)`; and that an overridden `ScaleControl` was invoked.
- **Tests today:** none found for `ScaleControl`

### LAY-22 — `TableLayoutPanel.CellPaint` / `OnCellPaint` / cell-border painting — Cat B — P1 — High
- **Ours:** The entire paint region of `TableLayoutPanel` is commented out — `CellPaint` event, `OnCellPaint`, the `OnLayout` override that invalidates, and `OnPaintBackground` which draws the borders (`src/Majorsilence.Forms/TableLayoutPanel.cs:305-480`, guarded by `// TODO: Custom Cell Paint`). `CellBorderStyle` *is* honoured by the layout engine (`src/Majorsilence.Forms/TableLayoutSettings.cs:66-76` sets `ContainerInfo.CellBorderWidth`, consumed throughout `Layout/TableLayout.cs`), so the gap is reserved but nothing is drawn in it.
- **Upstream:** `CellPaint` at `src/System.Windows.Forms/System/Windows/Forms/Panels/TableLayoutPanel/TableLayoutPanel.cs:308`; `OnLayout` → `Invalidate()` at `:319`; `OnPaintBackground` draws every cell border per `CellBorderStyle` and raises `OnCellPaint` for each cell at `:330-400`.
- **Impact:** `CellBorderStyle = Single/Inset/Outset` reserves the gap but paints nothing, so a grid-looking form migrates as a grid of floating controls with mysterious extra whitespace. `CellPaint` does not exist at all, so designer/user code hooking it fails to compile (and any custom cell backgrounds vanish).
- **Fix:** Un-comment the region and port `OnPaintBackground` to the Skia `PaintEventArgs`; re-add `CellPaint`/`OnCellPaint` and the `OnLayout` → `Invalidate()` override.
- **Test:** headless render a 2x2 TLP with `CellBorderStyle = Single`; assert the pixel at the cell boundary is the border colour, and that a `CellPaint` handler is called 4 times.
- **Tests today:** none

### LAY-23 — `TableLayout.SetElementBounds` ignores RightToLeft — Cat B — P2 — High
- **Ours:** `var isContainerRTL = false;` then `if (containerInfo.Container is Control) { var control = ...; //isContainerRTL = control.RightToLeft == RightToLeft.Yes;  TODO: RTL }` — the assignment is commented out, so `isContainerRTL` is a compile-time constant `false` (`src/Majorsilence.Forms/Layout/TableLayout.cs`, `SetElementBounds`).
- **Upstream:** `if (containerInfo.Container is Control containerAsControl) { isContainerRTL = containerAsControl.RightToLeft == RightToLeft.Yes; }` and the whole column walk mirrors from `displayRectF.Right` (`src/System.Windows.Forms/System/Windows/Forms/Layout/TableLayout.cs`, `SetElementBounds`).
- **Impact:** A `TableLayoutPanel` with `RightToLeft = Yes` lays columns out left-to-right, i.e. mirrored wrongly, for every RTL-localised app.
- **Fix:** Un-comment the assignment; the rest of the mirroring code is already ported.
- **Test:** 2-column TLP with `RightToLeft = Yes`; assert the control in column 0 has the larger `Left`.
- **Tests today:** none

### LAY-24 — `DefaultLayout.LayoutAnchoredControls` skip condition widened — Cat A — P2 — High
- **Ours:** `if ((displayRectangle.Width <= 0) || (displayRectangle.Height <= 0)) return;` — skips the anchored pass for *any* container with a degenerate display rectangle (`src/Majorsilence.Forms/Layout/DockAndAnchorLayout.cs:248`, with a long comment explaining the deliberate widening).
- **Upstream:** `if (CommonProperties.GetAutoSize(container) && ((displayRectangle.Width == 0) || (displayRectangle.Height == 0))) return;` — only AutoSize containers, and only on an exactly-zero dimension (`src/System.Windows.Forms/System/Windows/Forms/Layout/DefaultLayout.cs:349-355`).
- **Impact:** A non-AutoSize container that is currently zero-sized in one dimension (a collapsed splitter panel, a zero-height row in a TLP, a `Panel` sized by a not-yet-run parent layout) leaves its anchored children at stale bounds where upstream would place them (clamped to zero). Usually self-corrects on the next real layout; the residual risk is one stale frame or a child that never gets re-laid-out because nothing else changes.
- **Fix:** Narrow to upstream's condition, or at least keep the widened guard only for `CommonProperties.GetAutoSize(container)`.
- **Test:** `panel.Width = 0; panel.PerformLayout(); panel.Width = 200; panel.PerformLayout();` assert the Right-anchored child ends at 200.
- **Tests today:** none

### LAY-25 — `Panel.GetPreferredSize(Size)` replaces the layout engine — Cat A — P0 — High
- **CLOSED 2026-08-31 (`W5.24`).** The public override is gone; `Panel.GetPreferredSizeCore` delegates to
  `LayoutEngine.GetPreferredSize`, so `FlowLayoutPanel` and `TableLayoutPanel` reach their own ported
  engines and the constraints and cache in `Control.GetPreferredSize` apply. **The suggested test above
  is wrong as written:** a child forced to `(0, 0)` measures 60, not 70, because the engine subtracts the
  container's padding offset from the anchored preferred size (upstream's `DefaultLayout` does this too --
  an anchored child's bounds already begin inside the padding). Place the child at the display-rectangle
  origin `(10, 10)` and 70 is right. There is no `SizeFromClientSize` here either: a panel's `BorderStyle`
  paints inside the client rectangle, so `Padding` is the whole inset.
- **Ours:** `Panel` overrides the **public** `GetPreferredSize(Size proposedSize)` with a hand-rolled scan of `Controls` that unions child `Bounds.Right`/`Bounds.Bottom` plus margins, keyed off `Dock`/`Anchor` (`src/Majorsilence.Forms/Panel.cs:40-59`). It ignores `proposedSize` entirely, ignores `Padding`, never touches `LayoutEngine`, and — because it overrides the public method rather than `GetPreferredSizeCore` — bypasses `Control.GetPreferredSize`'s `ApplySizeConstraints` (MinimumSize/MaximumSize clamping) and the preferred-size cache (`src/Majorsilence.Forms/Control.Layout.cs:112-146`). `FlowLayoutPanel` and `TableLayoutPanel` both derive from `Panel` and do **not** override it, so they inherit this too.
- **Upstream:** `internal override Size GetPreferredSizeCore(Size proposedSize) { Size borderSize = SizeFromClientSize(Size.Empty); Size totalPadding = borderSize + Padding.Size; return LayoutEngine.GetPreferredSize(this, proposedSize - totalPadding) + totalPadding; }` (`src/System.Windows.Forms/System/Windows/Forms/Panels/Panel.cs:146-154`) — it delegates to the container's own layout engine (`FlowLayout.GetPreferredSize` / `TableLayout.GetPreferredSize` / `DefaultLayout.GetPreferredSize`) and adds padding and border.
- **Impact:** P0 for AutoSize containers. An `AutoSize` `TableLayoutPanel` or `FlowLayoutPanel` never asks its own engine what size it needs, so it sizes to the union of where its children *currently* are — which before the first correct layout is the designer's stale positions, and after a content change is the previous size. Concretely: a `FlowLayoutPanel` with `AutoSize = true, WrapContents = true` cannot compute a wrapped height for a proposed width (the argument is discarded), so it reports one row. An `AutoSize` `Panel` ignores its `Padding` (content flush to the edge) and its `MinimumSize`/`MaximumSize` are not applied to `PreferredSize`.
- **Fix:** Delete the `Panel.GetPreferredSize` override and add `internal override Size GetPreferredSizeCore(Size proposedSize)` mirroring upstream — `LayoutEngine.GetPreferredSize(this, proposedSize - totalPadding) + totalPadding`. This automatically fixes FlowLayoutPanel and TableLayoutPanel, since the engines are already correctly ported.
- **Test:** `var flp = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 100 }` with three 60x20 buttons; assert `flp.GetPreferredSize(new Size(100, 0)).Height >= 60` (three wrapped rows). And `new Panel { Padding = new Padding(10) }` with one 50x50 child at (0,0): assert `PreferredSize == (70,70)`.
- **Tests today:** none

### LAY-26 — `GroupBox.AutoSize` — Cat E — P1 — High
- **CLOSED 2026-08-31 (`W5.24`).** `public override bool AutoSize` forwarding to base, plus a
  `GetPreferredSizeCore` that adds the caption band and padding to the engine's answer and widens to the
  caption text when that is the wider of the two.
- **Ours:** `public new bool AutoSize { get; set; }` (`src/Majorsilence.Forms/GroupBox.cs:66`) — a stored-only shadow of `Control.AutoSize`. Setting it never sets the layout state bit that `CommonProperties.GetAutoSize` reads, so `LayoutAutoSizedControls` never resizes the group box, and `((Control)gb).AutoSize` reports a different value than `gb.AutoSize`. `GroupBox.AutoSizeMode` (`src/Majorsilence.Forms/RemainingMemberParity.cs:265`) *does* go through `GetAutoSizeMode`/`SetAutoSizeMode`, so the two halves of the feature read different state. GroupBox also has no `GetPreferredSizeCore` override.
- **Upstream:** `public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }` — the re-declaration is purely to re-expose `[Browsable]` (`src/System.Windows.Forms/System/Windows/Forms/Controls/GroupBox/GroupBox.cs:59-63`), and `GroupBox` overrides `GetPreferredSizeCore` so it can size to its caption + children.
- **Impact:** `groupBox1.AutoSize = true` — common on dynamically-populated option groups — does nothing, and the group box stays at its designer size, clipping content. Silent.
- **Fix:** Replace with `public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }` and add a `GetPreferredSizeCore` that measures caption + `LayoutEngine.GetPreferredSize` + `DisplayRectangle` inset.
- **Test:** `gb.AutoSize = true; Assert.True(((Control)gb).AutoSize);` plus a preferred-size assertion with one child.
- **Tests today:** none

### LAY-27 — `GroupBox.FlatStyle` / `UseCompatibleTextRendering` — Cat C — P2 — High
- **Ours:** Both bare auto-properties with "Stub" comments (`src/Majorsilence.Forms/GroupBox.cs:60,63`); `Renderers/GroupBoxRenderer.cs` never reads either, and neither setter calls `Invalidate()`.
- **Upstream:** `FlatStyle` picks between the themed/3D/flat/popup frame and calls `Invalidate()` (`src/System.Windows.Forms/System/Windows/Forms/Controls/GroupBox/GroupBox.cs:172+`); `FlatStyle.System` also changes `DisplayRectangle`.
- **Impact:** `FlatStyle = Flat` still paints a 3D-ish frame. Cosmetic but affects every "flat" themed migrated form.
- **Fix:** Have `GroupBoxRenderer` branch on `FlatStyle`; make the setter `Invalidate()`.
- **Test:** headless render with `FlatStyle.Flat` vs `Standard`; assert the frame pixels differ.
- **Tests today:** none

### LAY-28 — `Panel.BorderStyle` never drawn and never inset from the client area — Cat C — P1 — High
- **Ours:** `BorderStyle` validates and `Invalidate()`s (`src/Majorsilence.Forms/Panel.cs:71-81`), but `PanelRenderer.Render` is an **empty method body** (`src/Majorsilence.Forms/Renderers/PanelRenderer.cs:9-11`) and nothing subtracts the border from `ClientRectangle`/`DisplayRectangle`.
- **Upstream:** `BorderStyle` is applied through `CreateParams` (`WS_BORDER` for `FixedSingle`, `WS_EX_CLIENTEDGE` for `Fixed3D`) and `UpdateStyles()`; the OS both draws the border and shrinks the client rectangle by 1 / 2 pixels per edge (`src/System.Windows.Forms/System/Windows/Forms/Panels/Panel.cs`, `BorderStyle` + `CreateParams`). `Panel.GetPreferredSizeCore` adds that border back via `SizeFromClientSize(Size.Empty)`.
- **Impact:** `panel1.BorderStyle = FixedSingle` — the standard way to visually group controls without a GroupBox — shows nothing at all. In addition every child is 1-2px larger / offset relative to Windows because the client rectangle is not inset, so `Dock = Fill` children sit where the border should be.
- **Fix:** Draw the border in `PanelRenderer.Render` per `BorderStyle`, and subtract the border thickness in `Panel`'s `DisplayRectangle`/client-rect computation (and add it back in `GetPreferredSizeCore`, see LAY-25).
- **Test:** headless render `new Panel { BorderStyle = BorderStyle.FixedSingle, Size = new Size(50,50) }`; assert the pixel at (0,0) is the border colour and `panel.DisplayRectangle.Width == 48`.
- **Tests today:** none

### LAY-29 — `ScrollableControl.DisplayRectangle` does not carry the scroll offset or the content size — Cat A — P1 — High
- **Ours:** `DisplayRectangle` = `base.DisplayRectangle`, minus the scrollbar extents, deflated by `Padding` (`src/Majorsilence.Forms/ScrollableControl.cs:187-200`). Its origin is always the client origin; scrolling is implemented instead by physically moving children in `ScrollWindow` (`ScrollableControl.cs:335+`).
- **Upstream:** `Rectangle rect = ClientRectangle; if (!_displayRect.IsEmpty) { rect.X = _displayRect.X; rect.Y = _displayRect.Y; if (HScroll) rect.Width = _displayRect.Width; if (VScroll) rect.Height = _displayRect.Height; } return LayoutUtils.DeflateRect(rect, Padding);` (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollableControl.cs`, `DisplayRectangle`) — the origin is the **negative** scroll position and the width/height are the **scrollable content** extent, larger than the client area.
- **Impact:** Two consequences. (a) Anything that reads `DisplayRectangle.Location` to convert between content and client coordinates (custom painting, hit-testing, `ScrollControlIntoView`-style math, third-party controls) gets `(0,0)` instead of `(-scrollX,-scrollY)` and mis-places content by exactly the scroll amount. (b) A `Dock = Fill` child inside an `AutoScroll` container is sized to the *visible* rectangle rather than the content rectangle, so it can never be the thing that makes the panel scroll.
- **Fix:** Maintain a `_displayRect` (content origin + content size) alongside the scrollbars and return it from `DisplayRectangle` as upstream does, letting the layout engine move children rather than `ScrollWindow`.
- **Test:** `panel.AutoScroll = true;` with content taller than the panel; scroll down 50; assert `panel.DisplayRectangle.Y == -50` and `panel.DisplayRectangle.Height == contentHeight`.
- **Tests today:** none

### LAY-30 — `Control.ScrollControlIntoView(Control)` — Cat B — P1 — High
- **Ours:** `public void ScrollControlIntoView (Control? activeControl) { }` — an empty body, and declared on `Control` rather than `ScrollableControl` (`src/Majorsilence.Forms/Control.Compat.cs:456`).
- **Upstream:** `ScrollableControl.ScrollControlIntoView(Control activeControl)` computes the control's rectangle relative to the display rectangle, honours `AutoScrollMargin`, and calls `SetDisplayRectLocation`/`ScrollControlIntoView` up the parent chain (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollableControl.cs`). It is also called internally from `ContainerControl` when focus moves, which is what makes tabbing into an off-screen control scroll it into view.
- **Impact:** Tabbing (or `Focus()`) into a control below the fold of an `AutoScroll` panel leaves it invisible — a common, immediately-noticed defect on long data-entry forms. Explicit `panel.ScrollControlIntoView(txt)` calls also do nothing.
- **Fix:** Implement on `ScrollableControl`: translate the target's bounds into display-rectangle space, add `AutoScrollMargin`, and adjust the scrollbar values; call it from the focus-change path.
- **Test:** `panel.AutoScroll = true;` with a child at y=1000 in a 200px panel; `panel.ScrollControlIntoView(child);` assert `panel.AutoScrollPosition.Y != 0` and the child's client-relative rectangle intersects the panel's client rectangle.
- **Tests today:** none (listed in `tests/Majorsilence.Forms.Tests/NoOpStubBaseline.txt`, but high-impact enough to call out)

### LAY-31 — `ScrollableControl.DockPadding` — Cat C — P2 — High
- **Ours:** `public DockPaddingEdges DockPadding { get; } = new ();` (`src/Majorsilence.Forms/RemainingMemberParity.cs:714`), where `DockPaddingEdges` is a detached bag of four ints with no owner and no consumer (`src/Majorsilence.Forms/MidSizeControlParity.cs:555-574`).
- **Upstream:** `DockPaddingEdges` holds a `ScrollableControl _owner` and every property reads/writes `_owner.Padding`, so `DockPadding.All = 8` is exactly `Padding = new Padding(8)` and inset the `DisplayRectangle` (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollableControl.DockPaddingEdgesConverter.cs:14-90`).
- **Impact:** Designer code migrated from older VS versions emits `this.panel1.DockPadding.All = 5;`. Ours accepts it and the docked children sit flush against the edge.
- **Fix:** Give `DockPaddingEdges` an owner and forward every accessor to `owner.Padding`; make `ScrollableControl.DockPadding` construct it with `this`.
- **Test:** `panel.DockPadding.All = 8; Assert.Equal(new Padding(8), panel.Padding);` and `Assert.Equal(panel.ClientRectangle.Width - 16, panel.DisplayRectangle.Width);`
- **Tests today:** none

### LAY-32 — `ScrollableControl.AutoScrollMargin` — Cat C — P2 — High
- **Ours:** `public Size AutoScrollMargin { get; set; } = Size.Empty;` with the doc comment "the value is stored but not applied to layout" (`src/Majorsilence.Forms/ScrollableControl.cs:71`); `SetAutoScrollMargin(x, y)` just assigns it with no validation and no `PerformLayout` (`ScrollableControl.cs:74`). `CalculateCanvasSize`/`Recalculate` never read it.
- **Upstream:** the setter throws `ArgumentOutOfRangeException` on a negative component and routes through `SetAutoScrollMargin`, which clamps to 0, stores, and calls `LayoutTransaction.DoLayout`; the margin is then added to the scrollable canvas so the last control is not flush against the edge and `ScrollControlIntoView` leaves that much slack (`src/System.Windows.Forms/System/Windows/Forms/Scrolling/ScrollableControl.cs`, `AutoScrollMargin`).
- **Impact:** The bottom/right-most control in an AutoScroll panel is scrollable to exactly its edge instead of leaving the requested margin, and negative values are silently accepted.
- **Fix:** Add the margin to the canvas size in `CalculateCanvasSize`, validate negatives, and `PerformLayout` from the setter.
- **Test:** `panel.AutoScrollMargin = new Size(0, 20);` assert the vertical scrollbar `Maximum` is 20 larger than with an empty margin.
- **Tests today:** none

### LAY-33 — `ScrollableControl.AutoScrollPosition` setter takes `Math.Abs` — Cat A — P2 — High
- **Ours:** `set { var x = Math.Abs(value.X); var y = Math.Abs(value.Y); ... }` (`src/Majorsilence.Forms/ScrollableControl.cs:99-116`), so both `(0, 100)` and `(0, -100)` scroll down by 100. The setter is also a no-op when the corresponding scrollbar is not `Visible`.
- **Upstream:** `set { if (Created) SetDisplayRectLocation(-value.X, -value.Y); SetScrollState(ScrollStateUserHasScrolled, false); }` — the value is **negated**, so `(0, 100)` scrolls down 100 and `(0, -100)` clamps to no scroll (this asymmetry is the well-known reason WinForms code writes `AutoScrollPosition = new Point(-saved.X, -saved.Y)`).
- **Impact:** Any app that round-trips the getter's negative value directly (`p.AutoScrollPosition = savedPosition;`) scrolls in ours where Windows would not, and vice versa for code compensating for the quirk. The divergence is silent and position-dependent.
- **Fix:** Negate rather than `Math.Abs`, and drop the `Visible` guard in favour of clamping against the canvas.
- **Test:** `panel.AutoScrollPosition = new Point(0, -100); Assert.Equal(0, panel.AutoScrollPosition.Y);` (upstream behaviour).
- **Tests today:** none

### LAY-34 — `AutoSize` on `Button` / `CheckBox` / `RadioButton` / `LinkLabel` never measures content — Cat B — P1 — High
- **CLOSED 2026-08-31 (`W5.24`)** for `ButtonBase` and its three subclasses, in one override on the base:
  text through `TextMeasurer` at the effective font (as `Label` does), the image placed per
  `TextImageRelation`, the check/radio glyph column taken from the renderer's own `GlyphSize`/
  `GlyphTextPadding`, plus `Padding` and the border. `LinkLabel` derives from `Label`, which already
  measured. Not covered: a top- or bottom-centred `GlyphAlign` wants its allowance on the vertical axis;
  the glyph is measured as a column beside the text, which is what the default alignment produces.
- **Ours:** `ButtonBase` (`src/Majorsilence.Forms/WinFormsBaseControls.cs:26`) and its subclasses `Button`, `CheckBox`, `RadioButton` have **no** `GetPreferredSizeCore` override anywhere in the repo, so they fall through to `Control.GetPreferredSizeCore` → `CommonProperties.GetSpecifiedBounds(this).Size` (`src/Majorsilence.Forms/Control.Layout.cs:151-154`) — i.e. the size the designer last set. All the `AutoSizeMode` plumbing and `LayoutTransaction.DoLayoutIf(AutoSize, ...)` calls exist (`src/Majorsilence.Forms/Button.cs:50,60-78`, `RadioButton.cs:53,63-80`) and drive a layout that then computes the *current* size. `Label` is the only one that measures (`src/Majorsilence.Forms/Label.cs:74`).
- **Upstream:** `ButtonBase.GetPreferredSizeCore` (`src/System.Windows.Forms/System/Windows/Forms/Controls/Buttons/ButtonBase.cs:1012`), `Button.GetPreferredSizeCore` (`Buttons/Button.cs:84`), `CheckBox.GetPreferredSizeCore` (`Buttons/CheckBox.cs:289`), `RadioButton.GetPreferredSizeCore` (`Buttons/RadioButton.cs:264`) all measure text + image + glyph + padding through the button adapter.
- **Impact:** `button1.AutoSize = true` — the standard way to make a localised button fit its caption — never resizes the button. Combined with `AutoSizeMode.GrowAndShrink` it still does nothing. The same buttons inside an AutoSize `FlowLayoutPanel`/`TableLayoutPanel` then propagate the wrong size up the tree, so whole rows come out the designer's width regardless of the translated text. Silent: `AutoSize` reads back `true`.
- **Fix:** Add `internal override Size GetPreferredSizeCore(Size proposedSize)` on `ButtonBase` measuring text (via the existing `TextMeasurer`, as `Label` does) unioned with the image and glyph, plus `Padding` and the border, clamped by `proposedSize`.
- **Test:** `var b = new Button { AutoSize = true, Text = "A very long caption" }; Assert.True(b.PreferredSize.Width > new Button().Width);`
- **Tests today:** none

### LAY-35 — `DpiHelper.IsScalingRequired` / `IsScalingRequirementMet` are hard-coded `false` — Cat A — P2 — High
- **Ours:** `private static readonly double deviceDpi = LogicalDpi;` — a readonly field initialised to 96 and never assigned from the real device (`src/Majorsilence.Forms/DpiHelper.cs:10`), so `IsScalingRequired => deviceDpi != LogicalDpi` is a compile-time `false` and `IsScalingRequirementMet` with it (`DpiHelper.cs:16,21`). The only consumers of the flag are the two right/bottom anchor fix-ups in `UpdateAnchorInfo` (`src/Majorsilence.Forms/Layout/DockAndAnchorLayout.cs:643,663`), which are therefore dead code. `LogicalToDeviceUnits(value, 0)` likewise returns `value` unchanged.
- **Upstream:** `ScaleHelper.IsScalingRequirementMet` reflects the real system DPI and PerMonitorV2 awareness; the same two branches in `DefaultLayout.UpdateAnchorInfo` (`src/System.Windows.Forms/System/Windows/Forms/Layout/DefaultLayout.cs:333,345`) re-derive `Left`/`Top` from the *old* right/bottom anchor so a right-anchored control that has been pushed past the parent's edge by DPI scaling keeps its width.
- **Impact:** At a non-96 DPI backend scale, right/bottom-anchored controls that overflow the parent are re-anchored from their clipped position and progressively drift/shrink across resizes. The rest of `DpiHelper` silently returns logical units where device units were expected whenever a caller passes `devicePixels == 0`.
- **Fix:** Have `deviceDpi` read the backend's actual scale (the same source `Control.DeviceDpi`/`ScaleFactor` uses at `src/Majorsilence.Forms/Control.cs:2266`) instead of the constant.
- **Test:** at a simulated 144 DPI, assert `DpiHelper.IsScalingRequirementMet` is true and `LogicalToDeviceUnits(10, 0) == 15`.
- **Tests today:** none

### LAY-36 — `LayoutEventArgs.AffectedComponent` and `AffectedControl` are independent fields — Cat A — P2 — High
- **Ours:** two separate auto-properties, each written by only one of the two constructors (`src/Majorsilence.Forms/LayoutEventArgs.cs:38-66`): `LayoutEventArgs(Control?, string?)` sets `AffectedControl` and leaves `AffectedComponent` null; `LayoutEventArgs(IComponent?, string?)` sets `AffectedComponent` and leaves `AffectedControl` null — even when the component *is* a `Control`.
- **Upstream:** one backing `WeakReference<IComponent>`; the `Control` constructor chains to the `IComponent` one, and `AffectedControl => AffectedComponent as Control` (`src/System.Windows.Forms/System/Windows/Forms/Layout/LayoutEventArgs.cs:10-33`).
- **Impact:** Every internal `PerformLayout(control, property)` produces args whose `AffectedComponent` is null, and any `OnLayout` handler (including third-party layout panels ported from WinForms) that reads `e.AffectedComponent` gets nothing. Conversely a `ToolStripItem`-shaped layout raised with the `IComponent` overload yields a null `AffectedControl`. This is exactly the "reads one backing field while a sibling writes another" shape. Ours also holds a strong reference where upstream holds a weak one, so a cached `LayoutEventArgs` (see `Control.Layout.cs:411`, `_cachedLayoutEventArgs`) roots a disposed control.
- **Fix:** Keep a single `IComponent?` field, chain the `Control` constructor to it, and make `AffectedControl => AffectedComponent as Control`; use a `WeakReference<IComponent>` to match upstream's lifetime.
- **Test:** `var e = new LayoutEventArgs(someControl, "Bounds"); Assert.Same(someControl, e.AffectedComponent);`
- **Tests today:** none

### LAY-37 — `TableLayoutSettings` serialization: converter is empty, `ISerializable` and the `LayoutSettings` setter are compiled out — Cat B — P1 — High
- **Ours:** `TableLayoutSettingsTypeConverter` exists twice: the real port is entirely inside `#if DESIGN_TIME` (`src/Majorsilence.Forms/TableLayoutSettingsTypeConverter.cs:7`) and `DESIGN_TIME` is **not defined** in any project file, while the one that actually compiles is an empty `class TableLayoutSettingsTypeConverter : TypeConverter { }` in a *different* namespace, `Majorsilence.Forms.Layout` (`src/Majorsilence.Forms/ConverterParity.cs:533`). Consequently `[TypeConverter(...)]` and `, ISerializable` on `TableLayoutSettings` are also compiled out (`src/Majorsilence.Forms/TableLayoutSettings.cs:15,20`), and `TableLayoutPanel.LayoutSettings` has **no setter at all** (`src/Majorsilence.Forms/TableLayoutPanel.cs:43-58` — the setter body is inside `#if DESIGN_TIME`).
- **Upstream:** `TableLayoutSettings` is `[TypeConverter(typeof(TableLayoutSettingsTypeConverter))]`, `[Serializable]`, `ISerializable`, and the converter round-trips the whole grid (styles + per-control Row/Column/RowSpan/ColumnSpan) to and from an XML string stored in the `.resx`; `TableLayoutPanel.LayoutSettings`'s setter applies a stub produced by that converter.
- **Impact:** Any form with `Localizable = true` containing a `TableLayoutPanel` stores its grid in the `.resx` as `tableLayoutPanel1.LayoutSettings`. On ours the resource cannot be deserialised (the compiled converter's `CanConvertFrom(string)` is `false`, so `ConvertFrom` throws `NotSupportedException`) and even if it could there is nowhere to put it. The panel comes up with no styles and every child in cell (0,0). Also the type is in the wrong namespace for code that names it explicitly.
- **Fix:** Define `DESIGN_TIME` (or delete the guards), remove the stub converter from `ConverterParity.cs`, and re-expose the `LayoutSettings` setter.
- **Test:** round-trip: `TypeDescriptor.GetConverter(typeof(TableLayoutSettings)).ConvertFrom(xml)` returns a stub whose `ColumnStyles.Count` matches, then assign it to `tlp.LayoutSettings` and assert `tlp.GetColumn(child)`.
- **Tests today:** none

## Low-priority / Win32-only (P3) — one line each
- `DefaultLayout.UseAnchorLayoutV2` / `ComputeAnchoredBoundsV2` / `UpdateAnchorInfoV2` — the whole V2 anchor path is absent from our port; upstream gates it behind the `System.Windows.Forms.AnchorLayoutV2` AppContext switch, which is off by default, so V1 (which *is* ported) is the shipping behaviour.
- `TableLayoutSettings.IsStub` branches in the `ColumnCount`/`RowCount`/style getters — ours goes straight to `TableLayout.GetContainerInfo(Owner)` and would NRE on a stub instance, but the only producer of a stub is the compiled-out type converter (see LAY-37), so it is unreachable today.
- `TabControl.RightToLeftLayout` — mirrors the native `TCS_RIGHT`/`WS_EX_LAYOUTRTL` window styles; no portable meaning without the theme engine.
- `Splitter.BorderStyle` — stored-only, but upstream implements it purely through `CreateParams` window styles.
- `TabControl.Appearance = FlatButtons/Buttons` — a comctl32 visual-style variant with no portable equivalent beyond re-skinning the strip renderer.
- `SplitContainer`'s explicit `ISupportInitialize.BeginInit/EndInit` no-ops (`SplitContainer.cs:41-42`) shadow the public `BeginInit`/`EndInit` in `TailParity.cs:235-238`, so a designer's `((ISupportInitialize)sc).EndInit()` hits the no-op rather than the one that calls `PerformLayout()`. Cosmetic today because `PerformLayout` runs anyway on the first show, but the duplication is a trap.

## Systemic patterns
- **The wrong `GetPreferredSize` hook.** Upstream's contract is: override `internal GetPreferredSizeCore(Size)` and let the public `Control.GetPreferredSize` apply `MinimumSize`/`MaximumSize` and the preferred-size cache around it. `Panel` (LAY-25), `PictureBox` (`src/Majorsilence.Forms/PictureBox.cs:226`), `TrackBar` (`src/Majorsilence.Forms/TrackBar.cs:411`) and `ToolStripPanelRow` (`ToolStripPanelRowLayout.cs:177`) all override the **public** method instead, so for those controls `MinimumSize`/`MaximumSize` are silently not applied to `PreferredSize` and the cache is bypassed. One sweep: convert every `public override Size GetPreferredSize` to `internal override Size GetPreferredSizeCore`.
- **Stored-only auto-properties on container controls.** `SplitContainer` (8), `TabControl` (9), `GroupBox` (2), `ScrollableControl.AutoScrollMargin`, `ScrollableControl.DockPadding`. A mechanical sweep — grep for `{ get; set; }` in the container controls and check each name against a consumer — would find them all. Anything with a `// Stub in Majorsilence.Forms` comment in a doc summary is one of these.
- **Shadowing `new`/stored properties that fork state from the base.** `TabPage.Enabled`, `TabPage.UseVisualStyleBackColor`, `GroupBox.AutoSize`. Upstream re-declares these only to change `[Browsable]`/`[EditorBrowsable]` and always forwards to `base`. Rule: a `new` property in this codebase must have body `get => base.X; set => base.X = value;` unless the *type* changes (as with `TabControl.Padding`, which is legitimately `Point`).
- **Behaviour parked behind `#if DESIGN_TIME`, which is never defined.** `TableLayoutSettingsTypeConverter` (whole class), `TableLayoutSettings : ISerializable`, `TableLayoutPanel.LayoutSettings`'s setter, `TableLayoutPanelCellPosition`'s converter, and the designer change-notification in `Control.Layout.cs:190`. Nothing in any `.csproj` or `Directory.Build.props` defines the symbol. Either define it or delete the guards — the current state means "the code is there" reads as "the feature works" when auditing by grep.
- **Paint regions commented out with a `// TODO`.** `TableLayoutPanel`'s `CellPaint`/`OnPaintBackground`/`OnLayout` block (LAY-22), `PanelRenderer.Render` (empty, LAY-28), the RTL assignment in `TableLayout.SetElementBounds` (LAY-23). Each is a layout- or paint-visible feature that the surrounding machinery already supports.
- **Members declared but never called from the framework's own code paths.** `Control.ScaleControl`, `DefaultLayout.ScaleAnchorInfo`, `Control.ScrollControlIntoView`. A "no internal caller" scan over `protected virtual`/`internal static` members would surface this class cheaply — they are the extension points migrated apps override and never see fire.
- **Events declared with `add { } remove { }`.** `Splitter.SplitterMoved`/`SplitterMoving` (`src/Majorsilence.Forms/Splitter.cs:137,140`). This shape is strictly worse than a `#pragma warning disable CS0067` field-like event: the handler is discarded at subscription time and there is no way for a test or for reflection to observe that nothing is wired.
- **Perf, not correctness:** `IArrangedElement.Children` is `IEnumerable<Control>` in our port (`src/Majorsilence.Forms/Control.Layout.cs:155`) where upstream uses an indexed `ArrangedElementCollection`, so every `children.Count()` / `children.ElementAt(i)` in the ported engines re-enumerates — `LayoutAnchoredControls`, `LayoutDockedControls` and `TryCalculatePreferredSize` are all O(n^2) per layout pass.
