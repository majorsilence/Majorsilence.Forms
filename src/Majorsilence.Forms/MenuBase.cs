using System.Drawing;
using Majorsilence.Forms.Renderers;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a base class for all Menu related controls.
    /// </summary>
    public abstract class MenuBase : Control
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
        }

        // Hides the Menu.
        internal virtual void Deactivate ()
        {
            IsActivated = false;
            SelectedItem = null;

            root_item.HideDropDown ();

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
                    SelectedItem = clicked_item;
                    clicked_item.OnClick (e);
                    OnItemClicked (e, clicked_item);
                    Activate ();
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
