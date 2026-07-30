using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a Menu control.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="ToolStrip"/> so that <see cref="MenuStrip"/> exposes the ToolStrip
    /// member surface real WinForms gives it (MenuStrip : ToolStrip upstream). Everything that makes a
    /// Menu a top-docked menu bar -- the horizontal expand layout, hover-opens-the-next-drop-down, and
    /// the MenuRenderer registration -- is unchanged and still lives here.
    /// </remarks>
    public class Menu : ToolStrip
    {
        /// <summary>
        /// Initializes a new instance of the Menu class.
        /// </summary>
        public Menu ()
        {
            Dock = DockStyle.Top;
        }

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (600, 28);

        /// <summary>
        /// Gets the collection of menu items contained by this Menu. Re-exposed past
        /// <see cref="ToolStrip"/>'s ToolStripItemCollection facade: MenuRenderer, LayoutItems and
        /// MenuBase's hit-testing all consume this collection, so it must stay the visible one.
        /// </summary>
        public new MenuItemCollection Items => RootItems;

        /// <inheritdoc/>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle);

        /// <inheritdoc/>
        protected override bool IsTopLevelMenu => true;

        /// <inheritdoc/>
        protected override void LayoutItems ()
        {
            StackLayoutEngine.HorizontalExpand.Layout (ClientRectangle, Items.Where (i => i.Visible).Cast<ILayoutable> ());
        }

        /// <inheritdoc/>
        protected override void OnDeselected (EventArgs e)
        {
            base.OnDeselected (e);

            Deactivate ();
        }

        /// <inheritdoc/>
        protected override void OnHoverChanged (MenuItem? oldItem, MenuItem? newItem)
        {
            if (IsActivated && newItem != null)
                SelectedItem = newItem;
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
