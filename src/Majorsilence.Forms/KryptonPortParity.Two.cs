using System;
using Majorsilence.Forms.Drawing;
using Majorsilence.Forms.VisualStyles;

namespace Majorsilence.Forms
{
    // The second wave of members collected from porting the Krypton Standard Toolkit. Where the first wave
    // (KryptonPortParity.cs) was almost entirely overridables, this one is dominated by a single cause: a
    // Form here is not a Control, so every ordinary Control member a form-derived class reaches for has to
    // be declared on the window side as well. A themed form asks for its DPI, makes a Graphics to measure
    // with, hit-tests its own children and sets its bounds -- all of which Control already had.
    //
    // The rest are members that were missing outright, or present but shaped so that a WinForms caller
    // could not reach them.

    public partial class WindowBase
    {
        /// <summary>Gets the DPI the window is currently being displayed at.</summary>
        /// <remarks>
        /// Reads the backend's scaling, exactly as <see cref="Control.DeviceDpi"/> does -- a form and the
        /// controls on it must never disagree about the scale they are drawing at, so both derive it from
        /// the same place rather than caching a value of their own.
        /// </remarks>
        public int DeviceDpi => (int)(Scaling * 96);

        /// <summary>Returns the child control at the given client coordinates, or null.</summary>
        /// <remarks>WinForms z-order: index 0 is topmost, so the first match wins. Matches
        /// <see cref="Control.GetChildAtPoint(System.Drawing.Point)"/> so a form and a panel answer the
        /// same question the same way.</remarks>
        public Control? GetChildAtPoint (System.Drawing.Point pt)
            => Controls.GetAllControls ().FirstOrDefault (c => c.Visible && c.Bounds.Contains (pt));

        /// <summary>Creates a <see cref="Graphics"/> for measuring against the window's surface.</summary>
        /// <remarks>
        /// Use for measurement; for painting use the <c>PaintEventArgs</c> canvas. The common caller is a
        /// form sizing itself to text it has not drawn yet -- a message box working out how wide it needs
        /// to be -- which it cannot do from inside a paint handler because the size decides the layout.
        /// </remarks>
        public Graphics CreateGraphics () => new Graphics ((Control?)null);

        /// <summary>Sets the window's bounds.</summary>
        public void SetBounds (int x, int y, int width, int height)
            => SetBoundsCore (x, y, width, height, BoundsSpecified.All);

        /// <summary>Sets the components of the window's bounds that <paramref name="specified"/> selects.</summary>
        public void SetBounds (int x, int y, int width, int height, BoundsSpecified specified)
            => SetBoundsCore (x, y, width, height, specified);

        /// <summary>Gets or sets whether the window repaints its whole surface when resized.</summary>
        /// <remarks>
        /// A form that paints its own chrome sets this so that a resize does not leave the old border
        /// drawn along the edge that moved. Honoured, not stored: <c>OnSizeChanged</c> invalidates the
        /// whole surface when it is set, which is what the flag means.
        /// </remarks>
        protected bool ResizeRedraw { get; set; }

        /// <summary>Recreates the window's underlying handle.</summary>
        /// <remarks>
        /// A no-op. WinForms code calls this to make the OS re-read window style bits it can only apply at
        /// creation time; there are no style bits and no handle to recreate here, and a control's
        /// appearance follows its properties directly, so there is nothing to force.
        /// </remarks>
        protected void RecreateHandle () { }

        /// <summary>Returns the control that owns the given window handle, or null.</summary>
        /// <remarks>Always null, for the reason given on <see cref="Control.FromHandle(IntPtr)"/>: there
        /// are no HWNDs here to look up. Declared on the window side too because form code calls it
        /// unqualified.</remarks>
        public static Control? FromHandle (IntPtr handle) => null;

        /// <summary>Raises the <see cref="HelpRequested"/> event.</summary>
        /// <remarks>Never raised by this layer -- there is no F1/help-cursor plumbing -- but a form that
        /// shows its own help overrides it, and message-box code calls it directly to service a Help
        /// button, which is a path that does work.</remarks>
        protected virtual void OnHelpRequested (HelpEventArgs hevent) => HelpRequested?.Invoke (this, hevent);

        /// <summary>Raised when the user asks for help on the window.</summary>
        /// <inheritdoc cref="OnHelpRequested"/>
        public event HelpEventHandler? HelpRequested;

        /// <summary>Raised when the window's handle has been destroyed.</summary>
        /// <remarks>
        /// Raised from <see cref="OnHandleDestroyed"/>, which the backend calls when the window closes.
        /// Code that tracks the set of live forms subscribes to this rather than to <c>Closed</c>: it is
        /// the last notification a form sends, so it is the safe point to drop a reference to it.
        /// </remarks>
        public event EventHandler? HandleDestroyed;
    }

    public partial class Control
    {
        /// <summary>Recreates the control's underlying handle.</summary>
        /// <inheritdoc cref="WindowBase.RecreateHandle"/>
        protected void RecreateHandle () { }

        /// <summary>Raises the <see cref="HelpRequested"/> event.</summary>
        /// <inheritdoc cref="WindowBase.OnHelpRequested"/>
        protected virtual void OnHelpRequested (HelpEventArgs hevent) => HelpRequested?.Invoke (this, hevent);
    }

    public partial class PropertyGrid
    {
        /// <summary>Gets or sets the renderer used for the grid's own tool strip.</summary>
        /// <remarks>
        /// The grid carries a small strip of category/alphabetical buttons, and this is how a themed grid
        /// restyles it. Stored and returned: the strip is drawn by this library's own renderer, so the
        /// value is not consulted -- but it must round-trip, because a themed grid reads back what it set.
        /// </remarks>
        protected ToolStripRenderer? ToolStripRenderer { get; set; }
    }

    public partial class UpDownBase
    {
        /// <summary>Gets or sets whether the control is in the middle of updating its own text.</summary>
        /// <remarks>
        /// WinForms uses this as a re-entrancy guard: while it pushes a new value into the edit box, the
        /// resulting TextChanged must not be mistaken for the user typing. A derived up-down sets it around
        /// its own text updates for the same reason, so it has to be settable from a subclass.
        /// </remarks>
        protected bool ChangingText { get; set; }
    }
}
