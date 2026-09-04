using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a base class for all Menu related controls.
    /// </summary>
    // ScrollableControl rather than Control, matching WinForms' `ToolStrip : ScrollableControl`: every
    // strip in this hierarchy is one there, and code that passes a ToolStrip where a ScrollableControl is
    // expected relies on it. A pure insertion -- ScrollableControl derives from Control, so nothing that
    // worked against the old base stops working.
    public abstract class MenuBase : ScrollableControl
    {
        private readonly MenuItem root_item;

        /// <summary>
        /// Initializes a new instance of the MenuBase class.
        /// </summary>
        protected MenuBase ()
        {
            root_item = new MenuRootItem (this);
        }

        /// <summary>
        /// Initializes a new instance of the MenuBase class with the provided root MenuItem.
        /// </summary>
        protected MenuBase (MenuItem root)
        {
            root_item = root;
        }

        // Shows the Menu.
        private void Activate ()
        {
            IsActivated = true;

            if (IsTopLevelMenu)
                Application.ActiveMenu = this;

            OnActivated ();
        }

        /// <summary>Called when the menu becomes the active one.</summary>
        /// <remarks>
        /// A hook rather than an event: the WinForms-named events live on the derived strips
        /// (<c>MenuStrip.MenuActivate</c>, <c>ContextMenu.Popup</c>), and this is the moment they
        /// describe. They were declared and never raised even though this trigger existed
        /// (finding <c>TSM-30</c>) -- <c>ContextMenu.Popup</c> in particular is *the* legacy hook for
        /// enabling and disabling items just before the menu appears.
        /// </remarks>
        protected virtual void OnActivated () { }

        /// <inheritdoc cref="OnActivated"/>
        protected virtual void OnDeactivated () { }

        // Guards against one physical click raising a menu leaf item's Click twice. On X11 a single
        // button release can be delivered to BOTH the menu bar's window and the drop-down popup's
        // window -- two separate MajorsilenceFormsWindowHost instances -- and each routes it through
        // its OnMouseClick to the same leaf item. Found running the migrated ReportDesigner: one click
        // on File > "New Report from Database" opened its modal dialog twice, stacked at the same
        // spot, looking like a hang (dotnet-stack showed two nested modal loops, one entered from
        // MenuBase.OnMouseClick and one from MenuDropDown.OnMouseClick).
        //
        // Two cases, both covered:
        //  - the handler is modal (the reported one): the second delivery is dispatched by the modal
        //    loop while the first handler is still on the stack, so _leafClickDepth > 0 stops it;
        //  - the handler returns quickly: the second delivery lands right after, so the <50ms gap
        //    since the last leaf click stops it. 50ms is far below a deliberate second click (a fast
        //    mouse double-click is ~150ms) and far above the sub-millisecond duplicate-delivery gap.
        [ThreadStatic] private static int _leafClickDepth;
        [ThreadStatic] private static MenuItem? _lastLeafClickItem;
        [ThreadStatic] private static long _lastLeafClickTicks;

        // Returns false if raising this item's click now would be the duplicate delivery of one
        // already handled, otherwise records a leaf click as started. A caller that gets true MUST
        // pair it with EndLeafClick in a finally.
        private protected static bool TryBeginLeafClick (MenuItem item)
        {
            var now = DateTime.UtcNow.Ticks;

            // The modal case: the second delivery is dispatched by the first handler's own modal
            // loop, so its click is still on the stack.
            if (_leafClickDepth > 0 && ReferenceEquals (_lastLeafClickItem, item))
                return false;

            // The fast-return case: the second delivery lands immediately after the first handler
            // returns. 50ms is well under a deliberate second click (a fast mouse double-click is
            // ~150ms) and far over the sub-millisecond gap between the two deliveries of one release.
            if (ReferenceEquals (_lastLeafClickItem, item) &&
                _lastLeafClickTicks != 0 &&
                now - _lastLeafClickTicks < TimeSpan.FromMilliseconds (50).Ticks)
                return false;

            _leafClickDepth++;
            _lastLeafClickItem = item;
            _lastLeafClickTicks = now;
            return true;
        }

        private protected static void EndLeafClick ()
        {
            if (_leafClickDepth > 0)
                _leafClickDepth--;

            // A modal handler can sit here for seconds; the duplicate delivery is only queued behind
            // it now, so measure the 50ms gap from when the handler returned, not when it started.
            _lastLeafClickTicks = DateTime.UtcNow.Ticks;
        }

        // Hides the Menu.
        internal virtual void Deactivate ()
        {
            var was_activated = IsActivated;

            IsActivated = false;
            SelectedItem = null;

            root_item.HideDropDown ();

            // Only when it had actually been open: Deactivate runs on several paths that can find the
            // menu already closed, and a Collapse for a menu that never appeared is noise.
            if (was_activated)
                OnDeactivated ();

            if (IsTopLevelMenu)
                Application.ActiveMenu = null;
        }

        /// <summary>
        /// Get the MenuItem at the specified location.
        /// </summary>
        public MenuItem? GetItemAtLocation (Point location) => Items.FirstOrDefault (item => item.Visible && item.Bounds.Contains (location));

        /// <summary>
        /// Get the top level Menu control, if any.
        /// </summary>
        public MenuBase? GetTopLevelMenu ()
        {
            if (IsTopLevelMenu)
                return this;

            return root_item.GetTopMenu ();
        }

        /// <summary>
        /// Gets a value indicating if the Menu is currently visible.
        /// </summary>
        public bool IsActivated { get; private set; }

        /// <summary>
        /// Gets a value indicating the Menu should close on click.
        /// </summary>
        protected virtual bool IsReleaseOnClick => true;

        /// <summary>
        /// Gets a value indicating this is a top level menu.
        /// </summary>
        protected virtual bool IsTopLevelMenu => false;

        /// <summary>
        /// Gets the collection of menu items contained by this Menu.
        /// </summary>
        public virtual MenuItemCollection Items => root_item.Items;

        // The real, single item collection -- the one LayoutItems, the renderers and MenuBase's mouse
        // hit-testing all consume. ToolStrip hides Items with a ToolStripItemCollection facade (see
        // ToolStrip's constructor), so Menu/MenuDropDown re-expose this to keep `menu.Items` and
        // `contextMenu.Items` typed as MenuItemCollection the way they always were.
        internal MenuItemCollection RootItems => root_item.Items;

        // The item this menu was built around. For a bar that is a synthetic MenuRootItem with no
        // parent; for a drop-down it is the item that owns it, which is what DropDownOwnerItem reports.
        internal MenuItem RootMenuItem => root_item;

        /// <summary>Whether this is a horizontal menu BAR, as opposed to a drop-down or a toolbar.</summary>
        /// <remarks><see cref="IsTopLevelMenu"/> is true for a ToolBar too, which has no menu mode to
        /// enter; menu-mode entry (F10, bare Alt) needs the narrower question.</remarks>
        internal bool IsTopLevelMenuBar => IsTopLevelMenu && !IsVertical && this is Menu;

        /// <summary>Moves the selection to <paramref name="item"/> as a keyboard action.</summary>
        /// <remarks>The setter is internal to this assembly and this is the one place keyboard
        /// navigation needs, so it is named for what it means rather than exposing the setter further.</remarks>
        internal void SelectItemFromKeyboard (MenuItem? item) => SelectedItem = item;

        /// <summary>
        /// The client area in LOGICAL units, for laying items out.
        /// </summary>
        /// <remarks>
        /// <see cref="Control.ClientRectangle"/> is device-scaled while item Bounds are logical, so
        /// laying out directly into it stored device-sized geometry in logical fields: at scaling 2 a
        /// 28px menu bar produced 56px-tall items that then reported as spilling out of the bar they
        /// were laid into. Identity at scaling 1. (The wider mismatch between ClientRectangle's units
        /// and Bounds' is tracked in BACKLOG.md -- 81 call sites, so not something to flip in passing.)
        /// </remarks>
        protected Rectangle LogicalClientRectangle {
            get {
                var r = ClientRectangle;
                return new Rectangle (
                    DeviceToLogicalUnits (r.X), DeviceToLogicalUnits (r.Y),
                    DeviceToLogicalUnits (r.Width), DeviceToLogicalUnits (r.Height));
            }
        }

        /// <summary>
        /// Lays out the child menu items.
        /// </summary>
        protected abstract void LayoutItems ();

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            base.OnMouseClick (e);

            var clicked_item = GetItemAtLocation (e.Location);

            // Clicking the currently dropped down item releases the menu
            if (IsActivated && IsReleaseOnClick && clicked_item == SelectedItem) {
                Deactivate ();
                return;
            }

            // If we clicked an item, raise the Click events
            if (clicked_item != null) {
                if (clicked_item.Enabled) {
                    // A leaf item's click can reach here twice for one physical release (see
                    // TryBeginLeafClick); an item that opens a submenu is idempotent and not gated.
                    var leaf = !clicked_item.HasItems;

                    if (leaf && !TryBeginLeafClick (clicked_item))
                        return;

                    try {
                        SelectedItem = clicked_item;
                        clicked_item.OnClick (e);
                        OnItemClicked (e, clicked_item);
                        Activate ();
                    } finally {
                        if (leaf)
                            EndLeafClick ();
                    }
                }
            } else {
                Deactivate ();
            }
        }

        /// <summary>
        /// Raises the HoverChanged event.
        /// </summary>
        protected virtual void OnHoverChanged (MenuItem? oldItem, MenuItem? newItem) { }

        /// <summary>
        /// Raises the ItemClicked event.
        /// </summary>
        protected virtual void OnItemClicked (MouseEventArgs e, MenuItem item) { }

        /// <inheritdoc/>
        protected override void OnMouseLeave (EventArgs e)
        {
            base.OnMouseLeave (e);

            SetHover (null);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove (MouseEventArgs e)
        {
            base.OnMouseMove (e);

            SetHover (GetItemAtLocation (e.Location));
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            LayoutItems ();

            RenderManager.Render (this, e);
        }

        /// <summary>
        /// Gets or sets the currently selected menu item.
        /// </summary>
        public MenuItem? SelectedItem {
            get => Items.FirstOrDefault (tp => tp.Selected);
            internal set {
                var old = SelectedItem;

                // Nothing is changing
                if (old == value)
                    return;

                if (old != null)
                    old.Selected = false;

                if (value != null) {
                    value.Selected = true;
                    Activate ();
                }

                Invalidate ();
            }
        }

        /// <summary>
        /// Routes a key pressed while this menu is the active one. Returns whether it was consumed.
        /// </summary>
        /// <remarks>
        /// The keyboard half of menu mode (finding <c>TSM-13</c>): with a menu open, the keys belong to
        /// the menu rather than to whatever holds focus, which is what upstream's ModalMenuFilter
        /// arranges. Nothing here existed -- there was no <c>OnKeyDown</c> in this class or any of its
        /// derivatives -- so an accidentally opened menu could not even be dismissed with Escape, and
        /// keyboard-only operation was impossible.
        /// <para>
        /// Mnemonics and accelerators (<c>Alt+F</c> reaching an item, <c>ShortcutKeys</c>) are a
        /// different mechanism and were done in W1.3; this is navigation once the menu is on screen.
        /// </para>
        /// </remarks>
        internal bool HandleNavigationKey (Keys keys)
        {
            // The deepest open drop-down owns the keys: with File > Recent open, Up/Down move within
            // Recent, not within File.
            var open = DeepestOpenDropDown ();

            switch (keys & Keys.KeyCode) {
                case Keys.Escape:
                    // One level at a time, as upstream does: Escape out of a submenu returns to its
                    // parent menu rather than dismissing the lot.
                    if (open.IsNestedDropDown) {
                        open.DropDownOwnerItem?.HideDropDown ();
                        return true;
                    }

                    Deactivate ();
                    return true;

                case Keys.Down:
                    if (!ReferenceEquals (open, this))
                        return open.MoveSelection (open.SelectedItem, 1);

                    // On a bar, Down opens the selected item instead of moving along it.
                    if (IsTopLevelMenu && !IsVertical) {
                        SelectedItem ??= FirstSelectable ();
                        SelectedItem?.ShowDropDown ();
                        (SelectedItem?.OpenDropDown)?.MoveSelection (null, 1);
                        return true;
                    }

                    return MoveSelection (SelectedItem, 1);

                case Keys.Up:
                    if (!ReferenceEquals (open, this))
                        return open.MoveSelection (open.SelectedItem, -1);

                    return !IsVertical && IsTopLevelMenu ? false : MoveSelection (SelectedItem, -1);

                case Keys.Right:
                case Keys.Left:
                    var forward = (keys & Keys.KeyCode) == Keys.Right;

                    // Inside a drop-down, Right opens a submenu of the current item and Left closes
                    // back to the parent; on a bar, both walk along it.
                    if (!ReferenceEquals (open, this)) {
                        // Right opens a submenu of the current item...
                        if (forward && open.SelectedItem is { } candidate && candidate.HasItems) {
                            candidate.ShowDropDown ();
                            candidate.OpenDropDown?.MoveSelection (null, 1);
                            return true;
                        }

                        // ...and Left closes back to the parent -- but ONLY from a nested one. From a
                        // menu hanging off the BAR, Left means "the menu to the left", so it has to
                        // fall through to the walk below. Asking DropDownOwnerItem alone cannot tell the
                        // two apart: every drop-down has an owning item, including a top-level one,
                        // whose parent is the bar's own synthetic root.
                        if (!forward && open.IsNestedDropDown) {
                            open.DropDownOwnerItem?.HideDropDown ();
                            return true;
                        }
                    }

                    if (!IsTopLevelMenu)
                        return false;

                    // Just move the selection. Because selection and "open" are one state here (see
                    // MoveSelection's note), the SelectedItem setter closes the menu being left and
                    // opens the one being entered by itself -- which is exactly the behaviour that
                    // makes Left/Right feel like moving between menus. Closing the old one by hand
                    // first and re-opening the new one afterwards ALSO worked for one step and then
                    // left the selection null on the next, because the second ShowDropDown on an
                    // already-open item re-shows its popup, and a popup re-show deactivates the menu
                    // through Application.ScheduleClosePopupsOnDeactivate.
                    return MoveSelection (SelectedItem, forward ? 1 : -1);

                case Keys.Enter:
                    var target = ReferenceEquals (open, this) ? SelectedItem : open.SelectedItem;

                    if (target is null || !target.Enabled)
                        return false;

                    if (target.HasItems) {
                        target.ShowDropDown ();
                        target.OpenDropDown?.MoveSelection (null, 1);
                        return true;
                    }

                    Application.ClosePopups ();

                    // PerformClick, so a keyboard activation is the same operation as a mouse one --
                    // including CheckOnClick toggling and the ItemClicked relay.
                    target.PerformClick ();
                    return true;
            }

            return false;
        }

        // The innermost menu with something open below it, or this one when nothing is.
        private MenuBase DeepestOpenDropDown ()
        {
            var menu = this;

            while (menu.SelectedItem?.OpenDropDown is { } child)
                menu = child;

            return menu;
        }

        /// <summary>
        /// Moves the selection one selectable item along, wrapping at the ends.
        /// </summary>
        /// <remarks>
        /// In this framework selecting a menu item OPENS it: <c>MenuItem.Selected</c>'s setter calls
        /// <c>ShowDropDown</c>/<c>HideDropDown</c> directly, which is how click-to-open works. Upstream
        /// separates "highlighted on the bar" from "dropped down", so its F10 highlights without
        /// opening; here the two are one state, and moving the selection to a menu opens it. Splitting
        /// them would change every mouse path into a menu, so this navigation lives with the coupling.
        /// </remarks>
        private bool MoveSelection (MenuItem? from, int step)
        {
            var selectable = Items.Where (i => i.Visible && i.Enabled && i is not MenuSeparatorItem).ToList ();

            if (selectable.Count == 0)
                return false;

            var current = from is null ? -1 : selectable.IndexOf (from);
            var next = current < 0 ? (step > 0 ? 0 : selectable.Count - 1)
                                   : ((current + step) % selectable.Count + selectable.Count) % selectable.Count;

            SelectedItem = selectable[next];

            return true;
        }

        private MenuItem? FirstSelectable ()
            => Items.FirstOrDefault (i => i.Visible && i.Enabled && i is not MenuSeparatorItem);

        /// <summary>Whether items stack vertically, as in a drop-down, rather than along a bar.</summary>
        protected virtual bool IsVertical => false;

        /// <summary>The item this menu drops down from, when it is a drop-down.</summary>
        /// <remarks>Not called <c>OwnerMenuItem</c>: <see cref="ToolStripDropDown"/> already has a
        /// private member of that name meaning the same thing, and a base virtual with a colliding
        /// name is a CS0114 rather than an override.</remarks>
        internal virtual MenuItem? DropDownOwnerItem => null;

        /// <summary>Whether this drop-down hangs off an item that is itself inside another drop-down.</summary>
        /// <remarks>The question Left and Escape need: from a nested menu they close back to the parent,
        /// while from a menu hanging off the bar Left walks the bar and Escape leaves menu mode.</remarks>
        internal bool IsNestedDropDown => DropDownOwnerItem?.OwnerControl is MenuDropDown;

        // Sets the specified item (or none) as the active hover.
        private void SetHover (MenuItem? item)
        {
            var old = Items.FirstOrDefault (tp => tp.Hovered);

            if (item is null || item != old) {
                // Clear any existing hovers
                if (old != null) {
                    old.Hovered = false;
                    Invalidate (old.Bounds);
                }

                if (item == null) {
                    OnHoverChanged (old, item);
                    return;
                }
            }

            if (item.Hovered || !item.Enabled)
                return;

            item.Hovered = true;

            Invalidate (item.Bounds);
            OnHoverChanged (old, item);
        }
    }
}
