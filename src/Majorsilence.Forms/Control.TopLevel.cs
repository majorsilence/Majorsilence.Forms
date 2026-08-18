using System;

namespace Majorsilence.Forms
{
    public partial class Control
    {
        // The window hosting this control while it is a visible top-level control. Created on first
        // show, reused across hide/show, closed on dispose.
        private PopupWindow? top_level_host;

        /// <summary>
        /// Makes this control a top-level one -- a control with no parent that is shown in its own
        /// window -- or returns it to being an ordinary child control.
        /// </summary>
        /// <remarks>
        /// This is how WinForms shows a Control as a floating window: <c>SetTopLevel (true)</c> followed
        /// by <c>Visible = true</c> is the pattern ToolStripDropDown uses for itself, and it is the
        /// pattern Krypton's whole popup layer (context menus, tooltips, the ribbon's application menu,
        /// collapsed-group popups) relies on -- there via <c>PI.ShowWindow(Handle, ...)</c>, which is the
        /// same operation phrased as a user32 call. Windows shows the control's own HWND; here there are
        /// no HWNDs, so a visible top-level control is hosted in a <see cref="PopupWindow"/> instead. Its
        /// <see cref="Bounds"/> at the moment it becomes visible are treated as SCREEN coordinates,
        /// matching what Location means for a WinForms top-level control.
        /// </remarks>
        protected internal virtual void SetTopLevel (bool value)
        {
            if (GetTopLevel () == value)
                return;

            // WinForms throws the same way: a parented control cannot become top-level. The host's
            // internal adapter does not count -- that parent is this feature's own plumbing.
            if (value && Parent is not null && !IsHostAdapter (Parent))
                throw new InvalidOperationException ("A top level control cannot have a parent.");

            SetState (States.TopLevel, value);

            if (!value)
                TearDownTopLevelHost ();
            else if (GetState (States.Visible))
                // The STATE flag, not the Visible getter: controls default to visible, so the common
                // sequence `SetTopLevel(true); Visible = true;` makes the assignment a no-op transition
                // -- this call here is what actually shows the window. The getter also reads false for
                // any parentless control, which a not-yet-hosted top-level control always is.
                UpdateTopLevelHost (true);
        }

        /// <summary>Gets whether this control is a top-level one. See <see cref="SetTopLevel"/>.</summary>
        protected internal bool GetTopLevel () => GetState (States.TopLevel);

        // True when the given parent is the root adapter of this control's own host window.
        private bool IsHostAdapter (Control parent) =>
            top_level_host is { } host && ReferenceEquals (parent, host.Controls.Owner);

        // Called from SetVisibleCore whenever visibility changes. Shows or hides the host window so the
        // control's Visible and its on-screen presence stay one thing.
        private void UpdateTopLevelHost (bool visible)
        {
            if (!GetTopLevel ())
                return;

            if (!visible) {
                top_level_host?.Hide ();
                return;
            }

            // Bounds were set by the caller before showing (Krypton does SetBounds(screenRect) first),
            // and for a top-level control they are screen coordinates. Capture before reparenting into
            // the host moves the control to the host's origin.
            var screen = Bounds;

            if (top_level_host is null) {
                // Any open window serves as the owner; the popup positions itself in screen coordinates
                // and does not read the owner's geometry. Without one there is nothing to host into --
                // WinForms would still show an HWND here, but a popup with no application window behind
                // it has no backend to attach to, so it stays invisible rather than throwing.
                if ((Form.ActiveForm ?? Application.MainForm) is not { } owner)
                    return;

                top_level_host = new PopupWindow (owner);

                // Dismissal: the generic popup machinery hides the HOST (Application.ClosePopups on
                // deactivate). Reflect that back into this control's Visible so both agree, and so code
                // watching VisibleChanged sees the dismissal.
                top_level_host.VisibleChanged += (_, _) => {
                    if (top_level_host is { Visible: false } && Visible)
                        SetVisibleCore (false);
                };

                top_level_host.Controls.Add (this);
            }

            Location = System.Drawing.Point.Empty;
            top_level_host.Size = screen.Size;

            // The control goes live now: Krypton asserts IsHandleCreated immediately after showing.
            CreateControl ();

            top_level_host.Show (screen.X, screen.Y);
        }

        // Closes and forgets the host. Used when the control stops being top-level and when it is
        // disposed -- Krypton dismisses a popup by disposing it, and the window must go with it.
        private void TearDownTopLevelHost ()
        {
            if (top_level_host is not { } host)
                return;

            top_level_host = null;
            host.Controls.Remove (this);
            host.Close ();
        }
    }
}
