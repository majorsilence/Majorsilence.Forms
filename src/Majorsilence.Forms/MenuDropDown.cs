using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a MenuDropDown control.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="ToolStrip"/> so that <see cref="ContextMenu"/> and
    /// <see cref="ContextMenuStrip"/> expose the ToolStrip member surface real WinForms gives them
    /// (ContextMenuStrip : ToolStripDropDownMenu : ToolStripDropDown : ToolStrip upstream). The popup
    /// show/hide mechanism and the vertical drop-down layout below are unchanged.
    /// </remarks>
    public class MenuDropDown : ToolStrip
    {
        private WindowBase? parent_form;
        private PopupWindow? popup;
        private int width = 400;
        private int height = 400;

        /// <summary>
        /// Initializes a new instance of the MenuDropDown class.
        /// </summary>
        public MenuDropDown () : base ()
        {
            Dock = DockStyle.Fill;

            foreach (var item in Items)
                item.ParentControl = this;
        }

        /// <summary>
        /// Initializes a new instance of the MenuDropDown class with the provided root MenuItem.
        /// </summary>
        public MenuDropDown (MenuItem root) : base (root)
        {
            // NOTE: base (root) reaches MenuBase (MenuItem) through ToolStrip/ToolBar's protected
            // root-forwarding constructors -- this is the ctor MenuItem.ShowDropDown uses for submenus.
            Dock = DockStyle.Fill;

            foreach (var item in Items)
                item.ParentControl = this;
        }

        /// <inheritdoc/>
        internal override void Deactivate ()
        {
            base.Deactivate ();

            Hide ();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Restores Control's zero default: <see cref="ToolBar"/> (now an ancestor via ToolStrip)
        /// defaults to a 600x34 docked bar, which a drop-down is not. The popup's real size comes from
        /// <see cref="LayoutItems"/>.
        /// </remarks>
        protected override Size DefaultSize => Size.Empty;

        /// <summary>
        /// Gets the collection of menu items in this drop down. Re-exposed past
        /// <see cref="ToolStrip"/>'s ToolStripItemCollection facade: MenuDropDownRenderer, LayoutItems
        /// and MenuBase's hit-testing all consume this collection.
        /// </summary>
        public new MenuItemCollection Items => RootItems;

        /// <inheritdoc/>
        /// <remarks>
        /// A plain drop down is never the top level menu -- it hangs off one. This restores MenuBase's
        /// default, which <see cref="ToolBar"/> (now an ancestor via ToolStrip) flips to true;
        /// <see cref="ContextMenu"/> deliberately sets it back to true because it IS the root.
        /// </remarks>
        protected override bool IsTopLevelMenu => false;

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlMidColor;
                style.Border.Width = 1;
            });

        /// <inheritdoc/>
        public override Form? FindForm ()
        {
            if (base.FindForm () is Form f)
                return f;

            return parent_form as Form;
        }

        /// <summary>
        /// Hides the drop down.
        /// </summary>
        public new void Hide ()
        {
            popup?.Hide ();
        }

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            var visible_items = Items.Where (i => i.Visible).ToList ();

            if (visible_items.Count == 0)
                return;

            var sizes = visible_items.Select (i => i.GetPreferredSize (Size.Empty));

            width = sizes.Select (s => s.Width).Max ();
            height = sizes.Select (s => s.Height).Sum () + 2;

            var client_rect = new Rectangle (1, 1, width - 2, height - 2);

            StackLayoutEngine.VerticalExpand.Layout (client_rect, visible_items.Cast<ILayoutable> ());
        }

        /// <inheritdoc/>
        protected override void OnMouseClick (MouseEventArgs e)
        {
            var clicked_item = GetItemAtLocation (e.Location);

            if (clicked_item != null && !clicked_item.HasItems) {

                // One physical release can be delivered to both this popup and the menu bar on X11
                // (see MenuBase.TryBeginLeafClick); only the first delivery raises the click.
                if (!TryBeginLeafClick (clicked_item))
                    return;

                try {
                    Application.ClosePopups ();

                    clicked_item.OnClick (e);
                    OnItemClicked (e, clicked_item);
                } finally {
                    EndLeafClick ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnHoverChanged (MenuItem? oldItem, MenuItem? newItem)
        {
            if (newItem != null) {
                oldItem?.HideDropDown ();

                Items.FirstOrDefault (i => i.IsDropDownOpened)?.HideDropDown ();
            }

            newItem?.ShowDropDown ();
        }

        /// <summary>
        /// Shows the drop down at the specified location.
        /// </summary>
        public virtual void Show (Control parent, Point location)
        {
            if (popup == null) {
                if (parent.FindWindow () is not WindowBase parent_window)
                    throw new InvalidOperationException ("Control 'parent' must belong to a window.");

                this.parent_form = parent_window;
                popup = new PopupWindow (parent_window);
                popup.Controls.Add (this);
            }

            LayoutItems ();

            // width/height come from the items' preferred sizes, which are already logical (each is run
            // through DeviceToLogicalUnits by MenuItem.GetPreferredSize). Window Size is logical too --
            // ComboBox and ToolTip set their popups the same way. This line used to divide by Scaling,
            // which at scale 1 was identity but at scale 2 handed the popup half the size its items
            // were laid into: the click hit-test (which runs in logical space) then landed off the
            // bottom-right item and the menu did nothing.
            popup.Size = new Size (width, height);

            Invalidate ();
            popup.Show (location);
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override bool Visible {
            get => popup?.Visible ?? false;
            set => popup?.Show ();
        }
    }
}
