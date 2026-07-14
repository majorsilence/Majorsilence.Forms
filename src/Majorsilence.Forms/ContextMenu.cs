using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ContextMenu control.
    /// </summary>
    public class ContextMenu : MenuDropDown
    {
        /// <summary>
        /// Initializes a new instance of the ContextMenu class.
        /// </summary>
        public ContextMenu () : base ()
        {
        }

        /// <inheritdoc/>
        protected override bool IsTopLevelMenu => true;

        /// <summary>Raised before the context menu is displayed. Can be used to dynamically modify items.</summary>
        public event EventHandler<System.ComponentModel.CancelEventArgs>? Opening;

        /// <summary>Raised after the context menu has been displayed.</summary>
        public event EventHandler? Opened;

        /// <summary>Raised after the context menu has been closed.</summary>
        public event EventHandler<ToolStripDropDownClosedEventArgs>? Closed;

        /// <summary>Raised when the context menu is closing.</summary>
        public event EventHandler<ToolStripDropDownClosingEventArgs>? Closing;

        /// <summary>Raises the Opened event.</summary>
        protected virtual void OnOpened (EventArgs e) => Opened?.Invoke (this, e);

        /// <summary>Raises the Closing event.</summary>
        protected virtual void OnClosing (ToolStripDropDownClosingEventArgs e) => Closing?.Invoke (this, e);

        /// <summary>Raises the Closed event.</summary>
        protected virtual void OnClosed (ToolStripDropDownClosedEventArgs e) => Closed?.Invoke (this, e);

        /// <summary>Gets or sets the control that triggered this context menu.</summary>
        public Control? SourceControl { get; private set; }

        /// <summary>Gets the collection of menu items (WinForms compat alias for Items).</summary>
        public MenuItemCollection MenuItems => Items;

        /// <inheritdoc/>
        public override void Show (Control parent, Point location)
        {
            SourceControl = parent;
            Application.ActiveMenu ??= this;

            var cancelArgs = new System.ComponentModel.CancelEventArgs ();
            Opening?.Invoke (this, cancelArgs);

            if (!cancelArgs.Cancel) {
                base.Show (parent, location);
                OnOpened (EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        internal override void Deactivate ()
        {
            // Fires Closing before the menu hides and Closed after -- matching WinForms ordering. This is
            // the single guaranteed teardown path (focus loss / outside click / ClosePopups).
            OnClosing (new ToolStripDropDownClosingEventArgs (ToolStripDropDownCloseReason.AppFocusChange));
            base.Deactivate ();
            OnClosed (new ToolStripDropDownClosedEventArgs (ToolStripDropDownCloseReason.AppFocusChange));
        }

        /// <summary>Displays the context menu at the specified control-relative coordinates.</summary>
        public void Show (Control parent, int x, int y) => Show (parent, new Point (x, y));

        /// <summary>Displays the context menu at the specified screen coordinates.</summary>
        public void Show (int x, int y) => Show (null!, new Point (x, y));
    }
}
