using System;

namespace Majorsilence.Forms
{
    // Gaps found by porting the Krypton Extended Toolkit. Almost all of them are protected virtual
    // methods: a control library subclasses a WinForms control and overrides one of these to intercept
    // behaviour, so a missing one is not a stub that quietly returns nothing -- it is a compile error at
    // the override site (CS0115), and the port stops. Each is wired into the path that already raises the
    // corresponding event, so an override actually runs rather than merely compiling.

    public partial class ToolStripItem
    {
        /// <summary>
        /// Raises the FontChanged event.
        /// </summary>
        /// <remarks>
        /// Menu items that measure their own text override this to invalidate cached metrics when the
        /// font changes underneath them.
        /// </remarks>
        protected virtual void OnFontChanged (EventArgs e) => FontChanged?.Invoke (this, e);

        /// <summary>Raised when this item's font changes.</summary>
        public event EventHandler? FontChanged;
    }

    public abstract partial class ToolStripDropDownItem
    {
        /// <summary>
        /// Raises the DropDownClosed event.
        /// </summary>
        /// <remarks>
        /// WinForms' name for the raiser; <c>OnDropDownHide</c> is the name this layer grew first and now
        /// funnels into this one, so overrides of either see the close.
        /// </remarks>
        protected virtual void OnDropDownClosed (EventArgs e) => RaiseDropDownClosed (e);
    }

    public partial class DataGridViewCell
    {
        /// <summary>
        /// Called when the cell's content -- as opposed to its padding or border -- is clicked.
        /// </summary>
        /// <remarks>
        /// The hook cells with an interactive interior use: a rating cell turns the click into a star
        /// count, a link cell navigates. Raised by <see cref="DataGridView"/> when the hit test lands
        /// inside the content bounds.
        /// </remarks>
        protected virtual void OnContentClick (DataGridViewCellEventArgs e) { }

        /// <summary>Called when the cell is clicked anywhere.</summary>
        protected virtual void OnClick (DataGridViewCellEventArgs e) { }

        // Entry points for DataGridView, which does the hit testing: the overrides above are protected,
        // so the owning grid cannot reach them directly.
        internal void RaiseContentClick (DataGridViewCellEventArgs e) => OnContentClick (e);

        internal void RaiseCellClick (DataGridViewCellEventArgs e) => OnClick (e);
    }

    public partial class ToolStripProgressBar
    {
        /// <summary>Gets the hosted control -- the progress bar itself.</summary>
        /// <remarks>
        /// WinForms derives this item from ToolStripControlHost, whose <c>Control</c> is the hosted
        /// ProgressBar; here the item hosts one directly. Ported code reaches through this to hook the
        /// hosted control's own events, so without it <c>Control</c> in a subclass binds to the
        /// <see cref="Majorsilence.Forms.Control"/> <em>type</em> instead of to the instance.
        /// </remarks>
        public Control Control => ProgressBar;
    }

    public partial class DataGridView
    {
        /// <summary>Gets the grid's vertical scroll bar.</summary>
        /// <remarks>
        /// WinForms exposes both scroll bars to derived grids, which reparent or hide them to implement
        /// their own scrolling (a tree grid parks the bar on an off-screen control while it rebuilds).
        /// Without these, <c>VerticalScrollBar</c> in a subclass binds to the same-named <em>type</em>
        /// instead and the member access fails to compile.
        /// </remarks>
        protected VerticalScrollBar VerticalScrollBar => vscrollbar;

        /// <summary>Gets the grid's horizontal scroll bar.</summary>
        /// <inheritdoc cref="VerticalScrollBar"/>
        protected HorizontalScrollBar HorizontalScrollBar => hscrollbar;
    }

    public partial class DataGridViewRow : IDisposable
    {
        /// <summary>Releases resources held by this row.</summary>
        /// <remarks>WinForms rows are disposable (they inherit DataGridViewBand : DataGridViewElement),
        /// and ported grids put derived rows in <c>using</c> blocks -- which does not compile against a
        /// non-disposable type. Nothing here holds an unmanaged handle, so the work is delegated to the
        /// virtual overload for derived rows that do.</remarks>
        public void Dispose ()
        {
            Dispose (true);
            GC.SuppressFinalize (this);
        }

        /// <summary>Releases resources held by this row.</summary>
        protected virtual void Dispose (bool disposing) { }

        /// <summary>
        /// Creates the collection that holds this row's cells.
        /// </summary>
        /// <remarks>
        /// Overridden by rows that need a specialised collection -- a tree-grid node row returns one that
        /// knows about child nodes. Called once during construction; returning null is not valid.
        /// </remarks>
        protected virtual DataGridViewCellCollection CreateCellsInstance () => new DataGridViewCellCollection (this);
    }

    // A WinForms Form inherits these from Control. Here Form is not a Control -- it composes a root
    // ControlAdapter -- so each one has to be forwarded explicitly for a form to have the same surface,
    // which is what the Control/window parity gate checks for.
    public partial class WindowBase
    {
        /// <inheritdoc cref="Control.InvokePaint"/>
        protected void InvokePaint (Control c, PaintEventArgs e)
        {
            Guard.ThrowIfNull (c);
            adapter.RaiseInvokePaint (c, e);
        }

        /// <inheritdoc cref="Control.InvokePaintBackground"/>
        protected void InvokePaintBackground (Control c, PaintEventArgs e)
        {
            Guard.ThrowIfNull (c);
            adapter.RaiseInvokePaintBackground (c, e);
        }

        /// <inheritdoc cref="Control.OnParentBackgroundImageChanged"/>
        /// <remarks>A top-level window has no parent to sample a background from, so this only exists to
        /// keep the surface identical; overriding it on a Form is harmless but never called.</remarks>
        protected virtual void OnParentBackgroundImageChanged (EventArgs e) { }

        /// <summary>Makes this window top-level or hosted. Mirrors Control.SetTopLevel; a Form already
        /// expresses the same thing through <see cref="Form.TopLevel"/>, which this defers to.</summary>
        protected internal virtual void SetTopLevel (bool value)
        {
            if (this is Form form)
                form.TopLevel = value;
        }

        /// <summary>Gets whether this window is top-level. Mirrors Control.GetTopLevel.</summary>
        protected internal bool GetTopLevel () => this is not Form form || form.TopLevel;
    }

    // WinForms controls are ISynchronizeInvoke, and components that marshal work back to the UI thread
    // (file copiers, background workers, anything holding a SynchronizingObject) are typed against that
    // interface rather than against Control. Implemented explicitly: the public Invoke/BeginInvoke
    // overloads above already exist with this layer's own nullability, and an explicit implementation
    // adapts to the interface's exact signatures without disturbing them or their callers.
    public partial class Control : System.ComponentModel.ISynchronizeInvoke
    {
        bool System.ComponentModel.ISynchronizeInvoke.InvokeRequired => InvokeRequired;

        IAsyncResult System.ComponentModel.ISynchronizeInvoke.BeginInvoke (Delegate method, object?[]? args) =>
            BeginInvoke (method, args ?? []);

        object? System.ComponentModel.ISynchronizeInvoke.EndInvoke (IAsyncResult result) => EndInvoke (result);

        object? System.ComponentModel.ISynchronizeInvoke.Invoke (Delegate method, object?[]? args) =>
            Invoke (method, args ?? []);

        /// <summary>
        /// Called when the background image of this control's <em>parent</em> changes.
        /// </summary>
        /// <remarks>
        /// Controls that fake transparency by sampling what is behind them override this to repaint.
        /// Raised for every child when a container's BackgroundImage is assigned.
        /// </remarks>
        protected virtual void OnParentBackgroundImageChanged (EventArgs e) => Invalidate ();

        // Called by the parent when its own BackgroundImage changes; protected members are not reachable
        // from the container, so the walk over Controls goes through here.
        internal void RaiseParentBackgroundImageChanged () => OnParentBackgroundImageChanged (EventArgs.Empty);

        // Entry points for WindowBase, which forwards its own InvokePaint* to this adapter: the methods
        // above are protected, so a Form -- not being a Control -- cannot reach them directly.
        internal void RaiseInvokePaint (Control c, PaintEventArgs e) => InvokePaint (c, e);

        internal void RaiseInvokePaintBackground (Control c, PaintEventArgs e) => InvokePaintBackground (c, e);
    }
}
