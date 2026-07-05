// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Majorsilence.Forms;

public partial class Control
{
    // This pattern is ugly, but it saves allocations
    // https://docs.microsoft.com/en-us/dotnet/standard/events/how-to-handle-multiple-events-using-event-properties
    private static readonly object s_autoSizeChangedEvent = new object ();
    private static readonly object s_clickEvent = new object ();
    private static readonly object s_mouseClickEvent = new object ();
    private static readonly object s_mouseDoubleClickEvent = new object ();
    private static readonly object s_contextMenuChangedEvent = new object ();
    private static readonly object s_controlAddedEvent = new object ();
    private static readonly object s_controlRemovedEvent = new object ();
    private static readonly object s_cursorChangedEvent = new object ();
    private static readonly object s_dockChangedEvent = new object ();
    private static readonly object s_doubleClickEvent = new object ();
    private static readonly object s_enabledChangedEvent = new object ();
    private static readonly object s_gotFocusEvent = new object ();
    private static readonly object s_lostFocusEvent = new object ();
    private static readonly object s_invalidatedEvent = new object ();
    private static readonly object s_keyDownEvent = new object ();
    private static readonly object s_keyPressEvent = new object ();
    private static readonly object s_keyUpEvent = new object ();
    private static readonly object s_layoutEvent = new object ();
    private static readonly object s_locationChangedEvent = new object ();
    private static readonly object s_marginChangedEvent = new object ();
    private static readonly object s_mouseDownEvent = new object ();
    private static readonly object s_mouseEnterEvent = new object ();
    private static readonly object s_mouseLeaveEvent = new object ();
    private static readonly object s_mouseMoveEvent = new object ();
    private static readonly object s_mouseUpEvent = new object ();
    private static readonly object s_mouseWheelEvent = new object ();
    private static readonly object s_paddingChangedEvent = new object ();
    private static readonly object s_parentEvent = new object ();
    private static readonly object s_resizeEvent = new object ();
    private static readonly object s_sizeChangedEvent = new object ();
    private static readonly object s_tabIndexChangedEvent = new object ();
    private static readonly object s_tabStopChangedEvent = new object ();
    private static readonly object s_textChangedEvent = new object ();
    private static readonly object s_visibleChangedEvent = new object ();

    /// <summary>
    /// Raised when the AutoSize property is changed.
    /// </summary>
    public event EventHandler? AutoSizeChanged {
        add => Events.AddHandler (s_autoSizeChangedEvent, value);
        remove => Events.RemoveHandler (s_autoSizeChangedEvent, value);
    }

    /// <summary>
    /// Raised when this control is clicked. Matches WinForms: a plain <see cref="EventHandler"/>
    /// (the event args passed are a <see cref="MouseEventArgs"/>, but handlers receive them as
    /// <see cref="EventArgs"/>). Use <see cref="MouseClick"/> for the typed mouse variant.
    /// </summary>
    public event EventHandler? Click {
        add => Events.AddHandler (s_clickEvent, value);
        remove => Events.RemoveHandler (s_clickEvent, value);
    }

    /// <summary>
    /// Raised when the ContextMenu property is changed
    /// </summary>
    public event EventHandler? ContextMenuChanged {
        add => Events.AddHandler (s_contextMenuChangedEvent, value);
        remove => Events.RemoveHandler (s_contextMenuChangedEvent, value);
    }

    /// <summary>
    ///  Raised when a new control is added.
    /// </summary>
    public event EventHandler<EventArgs<Control>>? ControlAdded {
        add => Events.AddHandler (s_controlAddedEvent, value);
        remove => Events.RemoveHandler (s_controlAddedEvent, value);
    }

    /// <summary>
    ///  Raised when a control is removed.
    /// </summary>
    public event EventHandler<EventArgs<Control>>? ControlRemoved {
        add => Events.AddHandler (s_controlRemovedEvent, value);
        remove => Events.RemoveHandler (s_controlRemovedEvent, value);
    }

    /// <summary>
    /// Raised when the Cursor property is changed.
    /// </summary>
    public event EventHandler? CursorChanged {
        add => Events.AddHandler (s_cursorChangedEvent, value);
        remove => Events.RemoveHandler (s_cursorChangedEvent, value);
    }

    /// <summary>
    /// Raised when the Dock property is changed.
    /// </summary>
    public event EventHandler? DockChanged {
        add => Events.AddHandler (s_dockChangedEvent, value);
        remove => Events.RemoveHandler (s_dockChangedEvent, value);
    }

    /// <summary>
    /// Raised when this control is double-clicked. Matches WinForms: a plain <see cref="EventHandler"/>.
    /// Use <see cref="MouseDoubleClick"/> for the typed mouse variant.
    /// </summary>
    public event EventHandler? DoubleClick {
        add => Events.AddHandler (s_doubleClickEvent, value);
        remove => Events.RemoveHandler (s_doubleClickEvent, value);
    }

    /// <summary>
    /// Raised when the Enabled property is changed.
    /// </summary>
    public event EventHandler? EnabledChanged {
        add => Events.AddHandler (s_enabledChangedEvent, value);
        remove => Events.RemoveHandler (s_enabledChangedEvent, value);
    }

    /// <summary>
    /// Raised when the control receives focus.
    /// </summary>
    public event EventHandler? GotFocus {
        add => Events.AddHandler (s_gotFocusEvent, value);
        remove => Events.RemoveHandler (s_gotFocusEvent, value);
    }

    /// <summary>
    /// Raised when the Control is invalidated.
    /// </summary>
    public event EventHandler<EventArgs<Rectangle>>? Invalidated {
        add => Events.AddHandler (s_invalidatedEvent, value);
        remove => Events.RemoveHandler (s_invalidatedEvent, value);
    }

    /// <summary>
    /// Raised when the control loses focus.
    /// </summary>
    public event EventHandler? LostFocus {
        add => Events.AddHandler (s_lostFocusEvent, value);
        remove => Events.RemoveHandler (s_lostFocusEvent, value);
    }

    /// <summary>
    /// Raised when input focus leaves the control (WinForms compatibility alias for <see cref="LostFocus"/>).
    /// </summary>
    public event EventHandler? Leave {
        add => Events.AddHandler (s_lostFocusEvent, value);
        remove => Events.RemoveHandler (s_lostFocusEvent, value);
    }

    /// <summary>
    /// Raised when the user presses down a key.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyDown {
        add => Events.AddHandler (s_keyDownEvent, value);
        remove => Events.RemoveHandler (s_keyDownEvent, value);
    }

    /// <summary>
    /// Raised when the user presses a key.
    /// </summary>
    public event KeyPressEventHandler? KeyPress {
        add => Events.AddHandler (s_keyPressEvent, value);
        remove => Events.RemoveHandler (s_keyPressEvent, value);
    }

    /// <summary>
    /// Raised when the user releases a key.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyUp {
        add => Events.AddHandler (s_keyUpEvent, value);
        remove => Events.RemoveHandler (s_keyUpEvent, value);
    }

    /// <summary>
    /// Raised when the control performs a layout.
    /// </summary>
    public event EventHandler<LayoutEventArgs>? Layout {
        add => Events.AddHandler (s_layoutEvent, value);
        remove => Events.RemoveHandler (s_layoutEvent, value);
    }

    /// <summary>
    /// Raised when the Location property is changed.
    /// </summary>
    public event EventHandler? LocationChanged {
        add => Events.AddHandler (s_locationChangedEvent, value);
        remove => Events.RemoveHandler (s_locationChangedEvent, value);
    }

    /// <summary>
    /// Raised when the Margin property is changed.
    /// </summary>
    public event EventHandler? MarginChanged {
        add => Events.AddHandler (s_marginChangedEvent, value);
        remove => Events.RemoveHandler (s_marginChangedEvent, value);
    }

    /// <summary>
    /// Raised when a mouse button is pressed.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseDown {
        add => Events.AddHandler (s_mouseDownEvent, value);
        remove => Events.RemoveHandler (s_mouseDownEvent, value);
    }

    /// <summary>
    /// Raised when the mouse cursor enters the control.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseEnter {
        add => Events.AddHandler (s_mouseEnterEvent, value);
        remove => Events.RemoveHandler (s_mouseEnterEvent, value);
    }

    /// <summary>
    /// Raised when the mouse cursor leaves the control.
    /// </summary>
    public event EventHandler? MouseLeave {
        add => Events.AddHandler (s_mouseLeaveEvent, value);
        remove => Events.RemoveHandler (s_mouseLeaveEvent, value);
    }

    /// <summary>
    /// Raised when the mouse cursor is moved within the control.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseMove {
        add => Events.AddHandler (s_mouseMoveEvent, value);
        remove => Events.RemoveHandler (s_mouseMoveEvent, value);
    }

    /// <summary>
    /// Raised when a mouse button ir released.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseUp {
        add => Events.AddHandler (s_mouseUpEvent, value);
        remove => Events.RemoveHandler (s_mouseUpEvent, value);
    }

    /// <summary>
    /// Raised when a mouse wheel is rotated.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseWheel {
        add => Events.AddHandler (s_mouseWheelEvent, value);
        remove => Events.RemoveHandler (s_mouseWheelEvent, value);
    }

    /// <summary>
    /// Raised when the Padding property is changed.
    /// </summary>
    public event EventHandler? PaddingChanged {
        add => Events.AddHandler (s_paddingChangedEvent, value);
        remove => Events.RemoveHandler (s_paddingChangedEvent, value);
    }

    /// <summary>
    /// Raised when the Parent property is changed.
    /// </summary>
    public event EventHandler? ParentChanged {
        add => Events.AddHandler (s_parentEvent, value);
        remove => Events.RemoveHandler (s_parentEvent, value);
    }

    /// <summary>
    ///  Raised when the control is resized.
    /// </summary>
    public event EventHandler? Resize {
        add => Events.AddHandler (s_resizeEvent, value);
        remove => Events.RemoveHandler (s_resizeEvent, value);
    }

    /// <summary>
    /// Raised when the Size property is changed.
    /// </summary>
    public event EventHandler? SizeChanged {
        add => Events.AddHandler (s_sizeChangedEvent, value);
        remove => Events.RemoveHandler (s_sizeChangedEvent, value);
    }

    /// <summary>
    /// Raised when the TabIndex property is changed.
    /// </summary>
    public event EventHandler? TabIndexChanged {
        add => Events.AddHandler (s_tabIndexChangedEvent, value);
        remove => Events.RemoveHandler (s_tabIndexChangedEvent, value);
    }

    /// <summary>
    /// Raised when the TabStop property is changed.
    /// </summary>
    public event EventHandler? TabStopChanged {
        add => Events.AddHandler (s_tabStopChangedEvent, value);
        remove => Events.RemoveHandler (s_tabStopChangedEvent, value);
    }

    /// <summary>
    /// Raised when the Text property is changed.
    /// </summary>
    public event EventHandler? TextChanged {
        add => Events.AddHandler (s_textChangedEvent, value);
        remove => Events.RemoveHandler (s_textChangedEvent, value);
    }

    /// <summary>
    /// Raised when the Visisble property is changed.
    /// </summary>
    public event EventHandler? VisibleChanged {
        add => Events.AddHandler (s_visibleChangedEvent, value);
        remove => Events.RemoveHandler (s_visibleChangedEvent, value);
    }

    /// <summary>
    /// Raised when the control receives input focus (WinForms compatibility; maps to GotFocus).
    /// </summary>
    public event EventHandler? Enter {
        add => Events.AddHandler (s_gotFocusEvent, value);
        remove => Events.RemoveHandler (s_gotFocusEvent, value);
    }

    /// <summary>
    /// Raised when the control is being validated (WinForms compat; fires on LostFocus). Stub.
    /// </summary>
    public event EventHandler<System.ComponentModel.CancelEventArgs>? Validating { add { } remove { } }

    /// <summary>
    /// Raised after the control has been validated (WinForms compat; fires on LostFocus). Stub.
    /// </summary>
    public event EventHandler? Validated { add { } remove { } }

    /// <summary>Raised when a drag-and-drop operation enters the control. Stub in Majorsilence.Forms.</summary>
    public event EventHandler<DragEventArgs>? DragEnter { add { } remove { } }

    /// <summary>Raised when the user drags an object over the control. Stub in Majorsilence.Forms.</summary>
    public event EventHandler<DragEventArgs>? DragOver { add { } remove { } }

    /// <summary>Raised when a drag-and-drop operation leaves the control. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? DragLeave { add { } remove { } }

    /// <summary>Raised when a drag-and-drop operation is completed. Stub in Majorsilence.Forms.</summary>
    public event EventHandler<DragEventArgs>? DragDrop { add { } remove { } }

    /// <summary>Raised during a drag-and-drop to provide cursor feedback. Stub in Majorsilence.Forms.</summary>
    public event EventHandler<GiveFeedbackEventArgs>? GiveFeedback { add { } remove { } }

    /// <summary>Raised to determine whether a drag-and-drop should continue. Stub in Majorsilence.Forms.</summary>
    public event EventHandler<QueryContinueDragEventArgs>? QueryContinueDrag { add { } remove { } }

    /// <summary>Raised when the control is painted. WinForms compat — hooks into OnPaint.</summary>
    public event EventHandler<PaintEventArgs>? Paint;

    /// <summary>Raised when the control is moved. Alias for LocationChanged. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? Move { add { } remove { } }

    /// <summary>Raised when the BackColor property changes. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? BackColorChanged { add { } remove { } }

    /// <summary>Raised when the ForeColor property changes. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? ForeColorChanged { add { } remove { } }

    /// <summary>Raised when the Font property changes. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? FontChanged { add { } remove { } }

    /// <summary>Raised when the control's handle is created. Stub in Majorsilence.Forms (always created).</summary>
    public event EventHandler? HandleCreated { add { } remove { } }

    /// <summary>Raised when the control's handle is destroyed. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? HandleDestroyed { add { } remove { } }

    /// <summary>Raised when the mouse is captured or released. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? MouseCaptureChanged { add { } remove { } }

    /// <summary>Raised when the mouse pointer hovers over the control. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? MouseHover { add { } remove { } }

    /// <summary>Raised before a key event to allow the key to be previewed. Stub in Majorsilence.Forms.</summary>
    public event EventHandler<PreviewKeyDownEventArgs>? PreviewKeyDown { add { } remove { } }

    /// <summary>Raised when the control is added to a container control. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? ChangeUICues { add { } remove { } }

    /// <summary>Raised when the control's HelpRequested event fires. Stub in Majorsilence.Forms.</summary>
    public event HelpEventHandler? HelpRequested { add { } remove { } }

    /// <summary>Raised when component is being queried for help. Stub in Majorsilence.Forms.</summary>
    public event QueryAccessibilityHelpEventHandler? QueryAccessibilityHelp { add { } remove { } }

    /// <summary>Raised when the user clicks the control with the mouse (typed mouse variant of <see cref="Click"/>).</summary>
    public event EventHandler<MouseEventArgs>? MouseClick {
        add => Events.AddHandler (s_mouseClickEvent, value);
        remove => Events.RemoveHandler (s_mouseClickEvent, value);
    }

    /// <summary>Raised when the user double-clicks the control with the mouse (typed mouse variant of <see cref="DoubleClick"/>).</summary>
    public event EventHandler<MouseEventArgs>? MouseDoubleClick {
        add => Events.AddHandler (s_mouseDoubleClickEvent, value);
        remove => Events.RemoveHandler (s_mouseDoubleClickEvent, value);
    }

    /// <summary>Raised when the user scrolls the control. Stub in Majorsilence.Forms.</summary>
    public event ScrollEventHandler? Scroll { add { } remove { } }

    /// <summary>Raised when the DPI scaling of the control changes. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? DpiChangedAfterParent { add { } remove { } }

    /// <summary>Raised before the DPI scaling of the control changes. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? DpiChangedBeforeParent { add { } remove { } }

    /// <summary>Raised when the data binding context changes. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? BindingContextChanged { add { } remove { } }

    /// <summary>Raised when the system colors change. Stub in Majorsilence.Forms.</summary>
    public event EventHandler? SystemColorsChanged { add { } remove { } }
}
