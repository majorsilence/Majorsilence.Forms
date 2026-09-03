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


        /// <summary>Displays the context menu at a point in <paramref name="parent"/>'s CLIENT coordinates.</summary>
        /// <remarks>
        /// The point is client-relative, as <c>ToolStripDropDown.Show (Control, Point)</c> is upstream:
        /// it does <c>_displayLocation = control.PointToScreen (position)</c>. This overload used to
        /// pass the point straight through as screen coordinates, so the canonical
        /// <c>contextMenuStrip1.Show (button1, new Point (0, button1.Height))</c> -- a menu under a
        /// button -- and <c>Show (grid, e.Location)</c> from a mouse handler both opened the menu at
        /// the top-left of the SCREEN (finding <c>TSM-03</c>, P0). <see cref="Show(Point)"/> is the
        /// screen-space overload and is unchanged.
        /// </remarks>
        public override void Show (Control parent, Point location)
        {
            Guard.ThrowIfNull (parent);

            ShowCore (parent, parent, parent.PointToScreen (location));
        }

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
        /// <summary>Raises the <see cref="Opening"/> event; cancelling it keeps the menu closed.</summary>
        /// <remarks>
        /// The one cancellable point in the lifecycle, and the reason a derived menu overrides it: a
        /// drop-down that fills itself lazily builds its items here and cancels when there is nothing to
        /// show. On the real open path, so an override genuinely suppresses the open.
        /// </remarks>
        protected virtual void OnOpening (System.ComponentModel.CancelEventArgs e) => Opening?.Invoke (this, e);

        /// <summary>Runs the Opening sequence and reports whether a handler cancelled the open.</summary>
        internal bool RaiseOpeningCancelled ()
        {
            var args = new System.ComponentModel.CancelEventArgs ();
            OnOpening (args);
            return args.Cancel;
        }

        private void ShowCore (Control anchor, Control? source, Point location)
        {
            SourceControl = source;
            Application.ActiveMenu ??= this;

            // Popup first, then Opening: the legacy event is documented as the place to enable and
            // disable items before the menu is measured, so it has to run before anything can cancel
            // (TSM-30). Neither was raised from here before.
            OnPopup (EventArgs.Empty);

            var cancelArgs = new System.ComponentModel.CancelEventArgs ();
            OnOpening (cancelArgs);

            if (!cancelArgs.Cancel) {
                base.Show (anchor, location);
                shown = true;
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
        /// <remarks>
        /// Collapse is raised here rather than from <c>MenuBase.OnDeactivated</c>, because a context
        /// menu is never "activated" in MenuBase's sense: that state comes from interacting with a menu
        /// BAR, while a context menu is shown outright. <c>shown</c> is the honest signal for "this menu
        /// is on screen", so Collapse happens once per dismissal and never for a menu that was not
        /// showing (finding <c>TSM-30</c>).
        /// </remarks>
        internal override void Deactivate ()
        {
            var was_shown = shown;

            shown = false;

            // Fires Closing before the menu hides and Closed after -- matching WinForms ordering. This is
            // the single guaranteed teardown path (focus loss / outside click / ClosePopups).
            OnClosing (new ToolStripDropDownClosingEventArgs (ToolStripDropDownCloseReason.AppFocusChange));
            base.Deactivate ();
            OnClosed (new ToolStripDropDownClosedEventArgs (ToolStripDropDownCloseReason.AppFocusChange));

            // After Closed, which is the modern pair; Collapse is the legacy name for the same moment.
            if (was_shown)
                OnCollapse (EventArgs.Empty);
        }

        private bool shown;
    }
}
