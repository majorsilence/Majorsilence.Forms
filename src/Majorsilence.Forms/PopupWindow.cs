namespace Majorsilence.Forms
{
    /// <summary>
    /// Represents a popup window used for things like ComboBoxes and context menus.
    /// </summary>
    public class PopupWindow : WindowBase
    {
        private readonly WindowBase parent_form;

        /// <summary>
        /// Initializes a new instance of the PopupWindow class.
        /// </summary>
        /// <param name="parentForm">
        /// The owning window. Usually a <see cref="Form"/>, but may be any <see cref="WindowBase"/>
        /// (e.g. a <see cref="HostedSurface"/> when Majorsilence.Forms is embedded in another toolkit).
        /// </param>
        public PopupWindow (WindowBase parentForm)
        {
            InitWindow (Majorsilence.Forms.Backends.Platform.Backend.CreateWindow (this, isPopup: true));

            StartPosition = FormStartPosition.Manual;

            parent_form = parentForm;
            parent_form.Deactivated += (o, e) => Hide ();
        }

        /// <inheritdoc/>
        protected override System.Drawing.Size DefaultSize => new System.Drawing.Size (100, 100);

        /// <summary>Gets the default style for all controls of this type.</summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.ControlMidColor;
            });

        /// <summary>Show the PopupWindow at the specified screen coordinates.</summary>
        public void Show (int x, int y)
        {
            Backend.Location = new System.Drawing.Point (x, y);
            Backend.Size = Size;

            Application.ActivePopupWindow = this;

            // Showing the popup deactivates the parent window, whose deactivation handler would
            // otherwise immediately dismiss this popup. Suppress that for the duration of the show.
            //
            // The reset below is deliberately posted to run on a later UI-thread dispatch rather
            // than happening synchronously right after Show() returns: on at least the Avalonia
            // backend, Show() returning does not mean the parent's own deactivation event (fired as
            // a side effect of this popup stealing activation) has been dispatched yet -- that event
            // is often queued and only delivered on a later iteration of the backend's own event
            // loop. Resetting the flag synchronously here left a window where the flag had already
            // gone back to false by the time that deactivation event actually arrived, so
            // WindowBase.OnBackendDeactivated's guard no longer suppressed it and
            // Application.ClosePopups() immediately closed the popup that had just been opened --
            // a real "menu opens then instantly closes" bug found in a real WinForms-migrated app
            // (ReportDesigner.Forms). Posting the reset lets any already-queued deactivation from
            // this same Show() call be dispatched (and correctly suppressed) first.
            Application.SuppressPopupDismiss = true;
            try {
                Show ();
            } finally {
                BeginInvoke (() => Application.SuppressPopupDismiss = false);
            }
        }

        /// <summary>Show the PopupWindow at the specified screen coordinates.</summary>
        public void Show (System.Drawing.Point screenLocation) => Show (screenLocation.X, screenLocation.Y);

        /// <summary>Show the PopupWindow at the specified coordinates relative to the provided Control.</summary>
        public void Show (Control control, int x, int y)
        {
            var pos = control.GetPositionInForm ();

            Show (parent_form.PointToScreen (new System.Drawing.Point (pos.X + x, pos.Y + y)));
        }

        /// <summary>Gets or sets the unscaled size of the window.</summary>
        public new System.Drawing.Size Size { get; set; }

        /// <summary>Gets the ControlStyle properties for this instance of the Control.</summary>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);
    }
}
