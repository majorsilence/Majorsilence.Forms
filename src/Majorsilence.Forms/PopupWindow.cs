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
            // NOTE: deliberately NOT dismissing on the parent's Deactivated. Opening this popup
            // deactivates the parent as a side effect, so that fires immediately and would close the
            // popup we just opened. Dismissal is handled generically by the posted, activation-
            // cancellable close in WindowBase.OnBackendDeactivated (see
            // Application.ScheduleClosePopupsOnDeactivate), driven by THIS popup's own deactivation.
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

            // Showing the popup deactivates the parent window; that deactivation must NOT dismiss the
            // popup we are opening. This is handled generically now: WindowBase.OnBackendDeactivated
            // POSTS the close and the popup's own activation cancels it
            // (Application.ScheduleClosePopupsOnDeactivate / NotifyWindowActivated), which also works
            // for nested submenus. No show-time timing flag is needed here anymore.
            Show ();
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
