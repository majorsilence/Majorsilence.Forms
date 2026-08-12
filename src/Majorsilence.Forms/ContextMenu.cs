using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a ContextMenu control.
    /// </summary>
    public partial class ContextMenu : MenuDropDown
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
        public event System.ComponentModel.CancelEventHandler? Opening;

        /// <summary>Raised after the context menu has been displayed.</summary>
        public event EventHandler? Opened;

        /// <summary>Raised after the context menu has been closed.</summary>
        public event ToolStripDropDownClosedEventHandler? Closed;

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
        public override void Show (Control parent, Point location) => ShowCore (parent, parent, location);

        /// <summary>
        /// Displays the context menu at the specified screen coordinates, anchored to the window that
        /// triggered it (or, failing that, the active window). Mirrors
        /// System.Windows.Forms.ContextMenuStrip.Show(Point) -- SourceControl stays null, as upstream.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// There is no window available to host the popup; use <see cref="Show(Control, Point)"/>.
        /// </exception>
        public virtual void Show (Point screenLocation) => ShowCore (ResolveAnchor (), null, screenLocation);

        /// <summary>Displays the context menu at the specified control-relative coordinates.</summary>
        public void Show (Control parent, int x, int y) => Show (parent, new Point (x, y));

        /// <summary>Displays the context menu at the specified screen coordinates.</summary>
        public void Show (int x, int y) => Show (new Point (x, y));

        // Single show path for every overload, so Opening -> (unless cancelled) show -> Opened holds no
        // matter which one the caller reached for. 'anchor' is only used to resolve the owning window for
        // the popup; 'source' is what SourceControl reports (null for the parentless overloads, matching
        // WinForms), and it must be assigned before Opening so handlers can read it.
        private void ShowCore (Control anchor, Control? source, Point location)
        {
            SourceControl = source;
            Application.ActiveMenu ??= this;

            var cancelArgs = new System.ComponentModel.CancelEventArgs ();
            Opening?.Invoke (this, cancelArgs);

            if (!cancelArgs.Cancel) {
                base.Show (anchor, location);
                OnOpened (EventArgs.Empty);
            }
        }

        // The popup needs SOME Control belonging to a window to hang off: MenuDropDown.Show resolves the
        // owning WindowBase from it, while the location is already in screen coordinates. Prefer the
        // control that last triggered this menu, else the active window's root ControlAdapter -- which is
        // a Control and reports that window.
        private Control ResolveAnchor ()
        {
            if (SourceControl != null)
                return SourceControl;

            var window = (WindowBase?)Form.ActiveForm ?? Application.MainForm;

            if (window?.adapter is Control adapter)
                return adapter;

            throw new InvalidOperationException ("Cannot show a ContextMenu at a screen location while no window is open; use Show (Control, Point) instead.");
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
    }
}
