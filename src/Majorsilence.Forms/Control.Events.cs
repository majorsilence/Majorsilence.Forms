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
    private static readonly object s_backColorChangedEvent = new object ();
    private static readonly object s_causesValidationChangedEvent = new object ();
    private static readonly object s_clickEvent = new object ();
    private static readonly object s_mouseClickEvent = new object ();
    private static readonly object s_mouseDoubleClickEvent = new object ();
    private static readonly object s_contextMenuChangedEvent = new object ();
    private static readonly object s_contextMenuStripChangedEvent = new object ();
    private static readonly object s_controlAddedEvent = new object ();
    private static readonly object s_controlRemovedEvent = new object ();
    private static readonly object s_cursorChangedEvent = new object ();
    private static readonly object s_dockChangedEvent = new object ();
    private static readonly object s_doubleClickEvent = new object ();
    private static readonly object s_dragDropEvent = new object ();
    private static readonly object s_dragEnterEvent = new object ();
    private static readonly object s_dragLeaveEvent = new object ();
    private static readonly object s_dragOverEvent = new object ();
    private static readonly object s_enabledChangedEvent = new object ();
    private static readonly object s_enterEvent = new object ();
    private static readonly object s_fontChangedEvent = new object ();
    private static readonly object s_foreColorChangedEvent = new object ();
    private static readonly object s_giveFeedbackEvent = new object ();
    private static readonly object s_gotFocusEvent = new object ();
    private static readonly object s_handleCreatedEvent = new object ();
    private static readonly object s_handleDestroyedEvent = new object ();
    private static readonly object s_imeModeChangedEvent = new object ();
    private static readonly object s_leaveEvent = new object ();
    private static readonly object s_longPressEvent = new object ();
    private static readonly object s_lostFocusEvent = new object ();
    private static readonly object s_invalidatedEvent = new object ();
    private static readonly object s_keyDownEvent = new object ();
    private static readonly object s_keyPressEvent = new object ();
    private static readonly object s_keyUpEvent = new object ();
    private static readonly object s_layoutEvent = new object ();
    private static readonly object s_locationChangedEvent = new object ();
    private static readonly object s_marginChangedEvent = new object ();
    private static readonly object s_mouseCaptureChangedEvent = new object ();
    private static readonly object s_mouseDownEvent = new object ();
    private static readonly object s_mouseEnterEvent = new object ();
    private static readonly object s_mouseHoverEvent = new object ();
    private static readonly object s_mouseLeaveEvent = new object ();
    private static readonly object s_mouseMoveEvent = new object ();
    private static readonly object s_mouseUpEvent = new object ();
    private static readonly object s_mouseWheelEvent = new object ();
    private static readonly object s_pinchEvent = new object ();
    private static readonly object s_scrollGestureEvent = new object ();
    private static readonly object s_swipeEvent = new object ();
    private static readonly object s_paddingChangedEvent = new object ();
    private static readonly object s_parentEvent = new object ();
    private static readonly object s_previewKeyDownEvent = new object ();
    private static readonly object s_queryContinueDragEvent = new object ();
    private static readonly object s_resizeEvent = new object ();
    private static readonly object s_rightToLeftChangedEvent = new object ();
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
    public event EventHandler<ControlEventArgs>? ControlAdded {
        add => Events.AddHandler (s_controlAddedEvent, value);
        remove => Events.RemoveHandler (s_controlAddedEvent, value);
    }

    /// <summary>
    ///  Raised when a control is removed.
    /// </summary>
    public event EventHandler<ControlEventArgs>? ControlRemoved {
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
    public event EventHandler<InvalidateEventArgs>? Invalidated {
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
    /// Raised when input focus leaves the control. Has its own event key (it is not an alias of
    /// <see cref="LostFocus"/>); <see cref="OnLostFocus"/> raises it just before LostFocus, matching
    /// the WinForms ordering of Leave -> Validating -> Validated -> LostFocus.
    /// </summary>
    public event EventHandler? Leave {
        add => Events.AddHandler (s_leaveEvent, value);
        remove => Events.RemoveHandler (s_leaveEvent, value);
    }

    /// <summary>
    /// Raised when a touch or pen contact is held in place for the platform's press-and-hold
    /// duration. Does not fire for the mouse. The default handler (see <see cref="Control.OnLongPress"/>)
    /// opens <see cref="ContextMenu"/> if one is set, mirroring the existing right-click behavior.
    /// </summary>
    public event EventHandler<LongPressEventArgs>? LongPress {
        add => Events.AddHandler (s_longPressEvent, value);
        remove => Events.RemoveHandler (s_longPressEvent, value);
    }

    /// <summary>
    /// Raised when the user presses down a key.
    /// </summary>
    public event KeyEventHandler? KeyDown {
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
    public event KeyEventHandler? KeyUp {
        add => Events.AddHandler (s_keyUpEvent, value);
        remove => Events.RemoveHandler (s_keyUpEvent, value);
    }

    /// <summary>
    /// Raised when the control performs a layout.
    /// </summary>
    public event LayoutEventHandler? Layout {
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
    public event MouseEventHandler? MouseDown {
        add => Events.AddHandler (s_mouseDownEvent, value);
        remove => Events.RemoveHandler (s_mouseDownEvent, value);
    }

    /// <summary>
    /// Raised when the mouse cursor enters the control.
    /// </summary>
    public event EventHandler? MouseEnter {
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
    public event MouseEventHandler? MouseMove {
        add => Events.AddHandler (s_mouseMoveEvent, value);
        remove => Events.RemoveHandler (s_mouseMoveEvent, value);
    }

    /// <summary>
    /// Raised when a mouse button ir released.
    /// </summary>
    public event MouseEventHandler? MouseUp {
        add => Events.AddHandler (s_mouseUpEvent, value);
        remove => Events.RemoveHandler (s_mouseUpEvent, value);
    }

    /// <summary>
    /// Raised when a mouse wheel is rotated.
    /// </summary>
    public event MouseEventHandler? MouseWheel {
        add => Events.AddHandler (s_mouseWheelEvent, value);
        remove => Events.RemoveHandler (s_mouseWheelEvent, value);
    }

    /// <summary>
    /// Raised while two touch or pen contacts move relative to each other (pinch-to-zoom and
    /// two-finger rotate). Does not fire for the mouse.
    /// </summary>
    public event EventHandler<PinchGestureEventArgs>? Pinch {
        add => Events.AddHandler (s_pinchEvent, value);
        remove => Events.RemoveHandler (s_pinchEvent, value);
    }

    /// <summary>
    /// Raised repeatedly while a touch or pen drag pans content, and again (with a decaying delta)
    /// during the momentum/flick phase after the contact lifts. Does not fire for the mouse.
    /// <see cref="ScrollableControl"/> already applies this to <see cref="ScrollableControl.AutoScrollPosition"/>;
    /// subscribe here only for custom pan behavior.
    /// </summary>
    public event EventHandler<ScrollGestureEventArgs>? ScrollGesture {
        add => Events.AddHandler (s_scrollGestureEvent, value);
        remove => Events.RemoveHandler (s_scrollGestureEvent, value);
    }

    /// <summary>
    /// Raised for a quick, discrete single-direction touch or pen drag (e.g. carousel/paging
    /// navigation). Does not fire for the mouse. For continuous drag-to-pan with inertia, see
    /// <see cref="ScrollGesture"/> instead.
    /// </summary>
    public event EventHandler<SwipeGestureEventArgs>? Swipe {
        add => Events.AddHandler (s_swipeEvent, value);
        remove => Events.RemoveHandler (s_swipeEvent, value);
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
    /// Raised when the control receives input focus. Has its own event key (it is not an alias of
    /// <see cref="GotFocus"/>); <see cref="OnGotFocus"/> raises it first, matching the WinForms
    /// ordering of Enter -> GotFocus.
    /// </summary>
    public event EventHandler? Enter {
        add => Events.AddHandler (s_enterEvent, value);
        remove => Events.RemoveHandler (s_enterEvent, value);
    }

    /// <summary>
    /// Raised when the <see cref="CausesValidation"/> property changes.
    /// </summary>
    public event EventHandler? CausesValidationChanged {
        add => Events.AddHandler (s_causesValidationChangedEvent, value);
        remove => Events.RemoveHandler (s_causesValidationChangedEvent, value);
    }

    /// <summary>
    /// Raised when the <see cref="ContextMenuStrip"/> property changes. Because ContextMenuStrip is
    /// an alias of <see cref="ContextMenu"/> here, this fires alongside <see cref="ContextMenuChanged"/>.
    /// </summary>
    public event EventHandler? ContextMenuStripChanged {
        add => Events.AddHandler (s_contextMenuStripChangedEvent, value);
        remove => Events.RemoveHandler (s_contextMenuStripChangedEvent, value);
    }

    /// <summary>
    /// Raised when the <see cref="ImeMode"/> property changes.
    /// </summary>
    public event EventHandler? ImeModeChanged {
        add => Events.AddHandler (s_imeModeChangedEvent, value);
        remove => Events.RemoveHandler (s_imeModeChangedEvent, value);
    }

    /// <summary>
    /// Raised when the <see cref="RightToLeft"/> property changes.
    /// </summary>
    public event EventHandler? RightToLeftChanged {
        add => Events.AddHandler (s_rightToLeftChangedEvent, value);
        remove => Events.RemoveHandler (s_rightToLeftChangedEvent, value);
    }

    /// <summary>
    /// Raised when the control is being validated (WinForms compat; fires on LostFocus).
    /// </summary>
    public event System.ComponentModel.CancelEventHandler? Validating;

    /// <summary>
    /// Raised after the control has been validated (WinForms compat; fires on LostFocus when not cancelled).
    /// </summary>
    public event EventHandler? Validated;

    // Drag-and-drop: Majorsilence.Forms has no OS drag source yet (DoDragDrop returns None), so
    // nothing in the framework raises these. They are real, Events-backed events with real
    // OnDragEnter/OnDragOver/... hooks, so a derived control can raise and override them and a
    // future backend can drive them without another API change.

    /// <summary>Raised when a drag-and-drop operation enters the control.</summary>
    public event DragEventHandler? DragEnter {
        add => Events.AddHandler (s_dragEnterEvent, value);
        remove => Events.RemoveHandler (s_dragEnterEvent, value);
    }

    /// <summary>Raised when the user drags an object over the control.</summary>
    public event DragEventHandler? DragOver {
        add => Events.AddHandler (s_dragOverEvent, value);
        remove => Events.RemoveHandler (s_dragOverEvent, value);
    }

    /// <summary>Raised when a drag-and-drop operation leaves the control.</summary>
    public event EventHandler? DragLeave {
        add => Events.AddHandler (s_dragLeaveEvent, value);
        remove => Events.RemoveHandler (s_dragLeaveEvent, value);
    }

    /// <summary>Raised when a drag-and-drop operation is completed.</summary>
    public event DragEventHandler? DragDrop {
        add => Events.AddHandler (s_dragDropEvent, value);
        remove => Events.RemoveHandler (s_dragDropEvent, value);
    }

    /// <summary>Raised during a drag-and-drop to provide cursor feedback.</summary>
    public event EventHandler<GiveFeedbackEventArgs>? GiveFeedback {
        add => Events.AddHandler (s_giveFeedbackEvent, value);
        remove => Events.RemoveHandler (s_giveFeedbackEvent, value);
    }

    /// <summary>Raised to determine whether a drag-and-drop should continue.</summary>
    public event EventHandler<QueryContinueDragEventArgs>? QueryContinueDrag {
        add => Events.AddHandler (s_queryContinueDragEvent, value);
        remove => Events.RemoveHandler (s_queryContinueDragEvent, value);
    }

    /// <summary>Raised when the control is painted. WinForms compat — hooks into OnPaint.</summary>
    public event PaintEventHandler? Paint;

    /// <summary>Raised when the control is moved (fires with LocationChanged).</summary>
    public event EventHandler? Move;

    /// <summary>Raised when the BackColor property changes. Also cascades to children that inherit
    /// their background ambiently (see <see cref="OnParentBackColorChanged"/>).</summary>
    public event EventHandler? BackColorChanged {
        add => Events.AddHandler (s_backColorChangedEvent, value);
        remove => Events.RemoveHandler (s_backColorChangedEvent, value);
    }

    /// <summary>Raised when the ForeColor property changes.</summary>
    public event EventHandler? ForeColorChanged {
        add => Events.AddHandler (s_foreColorChangedEvent, value);
        remove => Events.RemoveHandler (s_foreColorChangedEvent, value);
    }

    /// <summary>Raised when the Font property changes.</summary>
    public event EventHandler? FontChanged {
        add => Events.AddHandler (s_fontChangedEvent, value);
        remove => Events.RemoveHandler (s_fontChangedEvent, value);
    }

    /// <summary>Raised when the control's handle is created. Majorsilence.Forms has no HWND; this
    /// fires from <see cref="CreateControl"/>, the point at which the control becomes live.</summary>
    public event EventHandler? HandleCreated {
        add => Events.AddHandler (s_handleCreatedEvent, value);
        remove => Events.RemoveHandler (s_handleCreatedEvent, value);
    }

    /// <summary>Raised when the control's handle is destroyed (fires when the control is disposed).</summary>
    public event EventHandler? HandleDestroyed {
        add => Events.AddHandler (s_handleDestroyedEvent, value);
        remove => Events.RemoveHandler (s_handleDestroyedEvent, value);
    }

    /// <summary>Raised when the mouse is captured or released (see <see cref="Capture"/>).</summary>
    public event EventHandler? MouseCaptureChanged {
        add => Events.AddHandler (s_mouseCaptureChangedEvent, value);
        remove => Events.RemoveHandler (s_mouseCaptureChangedEvent, value);
    }

    /// <summary>Raised when the mouse pointer hovers over the control. Majorsilence.Forms has no
    /// hover timer, so this fires once per mouse entry, right after <see cref="MouseEnter"/>.</summary>
    public event EventHandler? MouseHover {
        add => Events.AddHandler (s_mouseHoverEvent, value);
        remove => Events.RemoveHandler (s_mouseHoverEvent, value);
    }

    /// <summary>Raised before <see cref="KeyDown"/> so a key can be previewed.</summary>
    public event EventHandler<PreviewKeyDownEventArgs>? PreviewKeyDown {
        add => Events.AddHandler (s_previewKeyDownEvent, value);
        remove => Events.RemoveHandler (s_previewKeyDownEvent, value);
    }

    /// <summary>Raised when the control is added to a container control. Stub in Majorsilence.Forms.</summary>
    public event UICuesEventHandler? ChangeUICues;

    /// <summary>Raises the ChangeUICues event.</summary>
    /// <remarks>
    /// Majorsilence.Forms never changes keyboard/focus cue state on its own, so nothing raises this
    /// internally; it exists because ported control libraries override it and wire handlers to it.
    /// </remarks>
    protected virtual void OnChangeUICues (UICuesEventArgs e) => ChangeUICues?.Invoke (this, e);

    /// <summary>Raised when the control's HelpRequested event fires. Stub in Majorsilence.Forms.</summary>
    public event HelpEventHandler? HelpRequested { add { } remove { } }

    /// <summary>Raised when component is being queried for help. Stub in Majorsilence.Forms.</summary>
    public event QueryAccessibilityHelpEventHandler? QueryAccessibilityHelp { add { } remove { } }

    /// <summary>Raised when the user clicks the control with the mouse (typed mouse variant of <see cref="Click"/>).</summary>
    public event MouseEventHandler? MouseClick {
        add => Events.AddHandler (s_mouseClickEvent, value);
        remove => Events.RemoveHandler (s_mouseClickEvent, value);
    }

    /// <summary>Raised when the user double-clicks the control with the mouse (typed mouse variant of <see cref="DoubleClick"/>).</summary>
    public event MouseEventHandler? MouseDoubleClick {
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
