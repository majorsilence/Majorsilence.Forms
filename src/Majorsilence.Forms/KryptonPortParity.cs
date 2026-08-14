using System;
using System.ComponentModel;
using System.Drawing;
using Majorsilence.Forms.Drawing;

namespace Majorsilence.Forms
{
    // Overridable members WinForms declares and this library did not. Every one of them is a virtual (or
    // protected virtual) method in WinForms that a control library overrides to take over behaviour: the
    // set was collected from porting the Krypton Standard Toolkit, whose controls, cells and dialogs
    // override them. Adding them here rather than editing the ported source is what keeps the port a
    // recompile instead of a rewrite.
    //
    // Where there is no cross-platform meaning (a Win32 message hook, a reflected command message), the
    // member exists and is never called -- documented as such per member, the same contract as WndProc.

    /// <summary>Which kinds of strip a tool-strip item may be dropped onto in a designer.</summary>
    [Flags]
    public enum ToolStripItemDesignerAvailability
    {
        /// <summary>The item is not offered by the designer.</summary>
        None = 0,
        /// <summary>A <see cref="ToolStrip"/>.</summary>
        ToolStrip = 0x1,
        /// <summary>A status strip.</summary>
        StatusStrip = 0x2,
        /// <summary>A context menu strip.</summary>
        ContextMenuStrip = 0x4,
        /// <summary>A menu strip.</summary>
        MenuStrip = 0x8,
        /// <summary>Every kind of strip.</summary>
        All = ToolStrip | StatusStrip | ContextMenuStrip | MenuStrip,
    }

    /// <summary>Declares which strips a custom tool-strip item may be added to in a designer.</summary>
    /// <remarks>Design-time metadata: it is read by the designer that populates a strip's item menu, so it
    /// has no runtime effect here — but a custom item carries the attribute and must compile.</remarks>
    [AttributeUsage (AttributeTargets.Class)]
    public sealed class ToolStripItemDesignerAvailabilityAttribute : Attribute
    {
        /// <summary>Initializes the attribute with the strips the item is offered for.</summary>
        public ToolStripItemDesignerAvailabilityAttribute (ToolStripItemDesignerAvailability visibility) =>
            ItemAdditionalBehavior = visibility;

        /// <summary>Gets the strips the item is offered for.</summary>
        public ToolStripItemDesignerAvailability ItemAdditionalBehavior { get; }
    }

    public partial class Control
    {
        /// <summary>
        /// Gives the control first sight of a key message before it is dispatched. Never called: there is
        /// no Win32 message pump here (the documented non-goal shared with <c>WndProc</c>). Present because
        /// a control that pre-processes keys overrides it; return false to let the key travel on.
        /// </summary>
        protected virtual bool ProcessKeyEventArgs (ref Message m) => false;

        /// <summary>Raises the <see cref="SystemColorsChanged"/> event.</summary>
        /// <remarks>Never raised by this layer -- no system-colour change notification is delivered --
        /// but a themed control overrides it to rebuild its palette.</remarks>
        protected virtual void OnSystemColorsChanged (EventArgs e) { }

        /// <summary>
        /// Raises the DoubleClick event, the WinForms-shaped overload that carries no mouse position.
        /// </summary>
        /// <remarks>
        /// WinForms splits this in two: <c>OnDoubleClick(EventArgs)</c> for the click itself and
        /// <c>OnMouseDoubleClick(MouseEventArgs)</c> for the positioned form. This library raised only a
        /// <see cref="MouseEventArgs"/> overload, so a control overriding the WinForms signature compiled
        /// against nothing. The positioned overload calls this one, so an override still runs.
        /// </remarks>
        protected virtual void OnDoubleClick (EventArgs e) { }

        /// <summary>Called when the control's binding context has changed.</summary>
        /// <remarks>Never raised by this layer; a data-bound control overrides it to rebind.</remarks>
        protected virtual void OnBindingContextChanged (EventArgs e) { }

        /// <summary>Raises the <see cref="StyleChanged"/> event.</summary>
        /// <remarks>Never raised: there is no window style to change. Present because a control that
        /// reacts to its own style bits overrides it.</remarks>
        protected virtual void OnStyleChanged (EventArgs e) => StyleChanged?.Invoke (this, e);
    }

    public partial class WindowBase
    {
        /// <summary>Raises the ControlAdded event for a control added to the form.</summary>
        /// <remarks>
        /// A <see cref="Form"/> is not a <see cref="Control"/> here, so the form side needs its own copy
        /// of the notifications Control already had -- a form that watches what is dropped onto it (to
        /// re-parent it into a themed panel, say) overrides this.
        /// </remarks>
        protected virtual void OnControlAdded (ControlEventArgs e) { }

        /// <summary>Raises the ControlRemoved event for a control removed from the form.</summary>
        /// <inheritdoc cref="OnControlAdded"/>
        protected virtual void OnControlRemoved (ControlEventArgs e) { }

        /// <summary>Called when the form's window handle has been created.</summary>
        /// <remarks>Raised when the backend window is shown, which is this library's equivalent of the
        /// handle coming into existence.</remarks>
        protected virtual void OnHandleCreated (EventArgs e) { }

        /// <summary>Called when the form's window handle has been destroyed.</summary>
        /// <inheritdoc cref="OnHandleCreated"/>
        protected virtual void OnHandleDestroyed (EventArgs e) { }

        /// <summary>Called when <see cref="RightToLeft"/> changes.</summary>
        protected virtual void OnRightToLeftChanged (EventArgs e) { }

        /// <summary>Called when the form's window style changes.</summary>
        /// <remarks>Never raised: there is no window style to change.</remarks>
        protected virtual void OnStyleChanged (EventArgs e) { }
    }

    public partial class ToolStrip
    {
        /// <summary>Called when the strip's <c>Renderer</c> changes.</summary>
        /// <remarks>A themed strip overrides this to re-attach to the new renderer's events.</remarks>
        protected virtual void OnRendererChanged (EventArgs e) { }
    }

    public partial class ToolStripControlHost
    {
        /// <summary>
        /// Called so the host can subscribe to the events of the control it hosts.
        /// </summary>
        /// <remarks>
        /// WinForms calls this when the hosted control is attached, and the paired
        /// <see cref="OnUnsubscribeControlEvents"/> when it is detached; a host that forwards a child's
        /// events as its own overrides both.
        /// </remarks>
        protected virtual void OnSubscribeControlEvents (Control? control) { }

        /// <summary>Called so the host can unsubscribe from the events of the control it hosts.</summary>
        /// <inheritdoc cref="OnSubscribeControlEvents"/>
        protected virtual void OnUnsubscribeControlEvents (Control? control) { }
    }

    public partial class CommonDialog
    {
        /// <summary>
        /// Runs the dialog for the given owner window handle -- the WinForms-shaped overload.
        /// </summary>
        /// <remarks>
        /// WinForms declares <c>RunDialog(IntPtr)</c> as the abstract a dialog implements, while this
        /// library declared the <see cref="IWin32Window"/> form. Both now exist: the window-typed overload
        /// delegates here by default, so a dialog implementing either signature works.
        /// </remarks>
        protected virtual bool RunDialog (IntPtr hwndOwner) => false;

        /// <summary>
        /// Hook that would receive the messages of a Win32 common dialog. Never called: these dialogs are
        /// implemented as forms, not as Win32 common dialogs, so there is no procedure to hook. Present
        /// because a dialog that customises the native chrome overrides it.
        /// </summary>
        protected virtual IntPtr HookProc (IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) => IntPtr.Zero;
    }

    public partial class ColorDialog
    {
        /// <summary>
        /// Hook that would receive the messages of the Win32 colour dialog. Never called: this dialog is a
        /// Form, not a Win32 common dialog, so there is no procedure to hook. Present because a themed
        /// colour dialog overrides it to restyle the native chrome.
        /// </summary>
        protected virtual IntPtr HookProc (IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) => IntPtr.Zero;
    }

    public partial class FontDialog
    {
        /// <inheritdoc cref="ColorDialog.HookProc"/>
        protected virtual IntPtr HookProc (IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) => IntPtr.Zero;
    }

    public partial class DataGridView
    {
        /// <summary>Called when the grid is scrolled.</summary>
        protected virtual void OnScroll (ScrollEventArgs e) { }

        /// <summary>Called when a data-binding operation has finished.</summary>
        protected virtual void OnDataBindingComplete (DataGridViewBindingCompleteEventArgs e) { }

        /// <summary>Called when the mouse moves over a cell.</summary>
        protected virtual void OnCellMouseMove (DataGridViewCellMouseEventArgs e) { }

        /// <summary>Called when a mouse button goes down over a cell.</summary>
        protected virtual void OnCellMouseDown (DataGridViewCellMouseEventArgs e) { }

        /// <summary>Called when a mouse button is released over a cell.</summary>
        protected virtual void OnCellMouseUp (DataGridViewCellMouseEventArgs e) { }

        /// <summary>Paints the area behind the rows and columns.</summary>
        /// <remarks>A themed grid overrides this to draw its own background instead of the flat fill.</remarks>
        protected virtual void PaintBackground (Graphics graphics, Rectangle clipBounds, Rectangle gridBounds) { }
    }

    public partial class DataGridViewCell
    {
        /// <summary>Returns the size this cell would like for the given style and row.</summary>
        protected virtual Size GetPreferredSize (Graphics graphics, DataGridViewCellStyle cellStyle, int rowIndex,
                                                Size constraintSize) => new Size (-1, -1);

        /// <summary>Returns the bounds of the cell's content, excluding padding and borders.</summary>
        protected virtual Rectangle GetContentBounds (Graphics graphics, DataGridViewCellStyle cellStyle,
                                                     int rowIndex) => Rectangle.Empty;

        /// <summary>Returns the bounds of the cell's error icon.</summary>
        protected virtual Rectangle GetErrorIconBounds (Graphics graphics, DataGridViewCellStyle cellStyle,
                                                       int rowIndex) => Rectangle.Empty;

        /// <summary>Called when the cell has been added to, or removed from, a grid.</summary>
        /// <inheritdoc cref="DataGridViewColumn.OnDataGridViewChanged"/>
        protected virtual void OnDataGridViewChanged () { }

        /// <summary>Converts the cell's value into the form that is displayed.</summary>
        /// <remarks>A cell that formats its own value (a themed date picker, say) overrides this.</remarks>
        protected virtual object? GetFormattedValue (object? value, int rowIndex,
                                                    ref DataGridViewCellStyle cellStyle,
                                                    TypeConverter? valueTypeConverter,
                                                    TypeConverter? formattedValueTypeConverter,
                                                    DataGridViewDataErrorContexts context) => value;
    }

    public partial class DataGridViewColumn
    {
        /// <summary>Called when the column has been added to, or removed from, a grid.</summary>
        /// <remarks>
        /// WinForms reaches this through <c>DataGridViewElement</c>, which every band and cell derives
        /// from; the types here are independent, so each declares its own. A column that configures its
        /// cells from the grid (a themed column reading the grid's palette) overrides it.
        /// </remarks>
        protected virtual void OnDataGridViewChanged () { }
    }

    public partial class ListBox
    {
        /// <summary>Creates the collection that holds the list's items.</summary>
        /// <remarks>WinForms lets a derived list substitute its own item collection here; a checked list
        /// returns one that tracks check state alongside each item.</remarks>
        protected virtual ObjectCollection CreateItemCollection () => new ObjectCollection (this);

        /// <summary>
        /// Handles a command message reflected from the native list box. Never called: there is no Win32
        /// list box behind this control (the documented <c>WndProc</c> non-goal). Present because a
        /// derived list that intercepts selection notifications overrides it.
        /// </summary>
        protected virtual void WmReflectCommand (ref Message m) { }
    }
}
