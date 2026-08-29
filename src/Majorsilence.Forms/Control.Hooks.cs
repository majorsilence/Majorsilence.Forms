// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing;

namespace Majorsilence.Forms;

// The protected extensibility surface WinForms custom controls override: the On*Changed
// notifications for the ambient appearance properties, the drag-and-drop and focus hooks, the
// Reset* methods designer serialization calls, and the RTL/scaling helpers.
public partial class Control
{
    #region Ambient appearance notifications

    /// <summary>
    /// Raises the <see cref="BackColorChanged"/> event and cascades to children that inherit their
    /// background ambiently.
    /// </summary>
    protected virtual void OnBackColorChanged (EventArgs e)
    {
        Invalidate ();

        (Events[s_backColorChangedEvent] as EventHandler)?.Invoke (this, e);

        if (Properties.GetObject (s_controlsCollectionProperty) is ControlCollection collection)
            for (var i = 0; i < collection.Count; i++)
                collection[i].OnParentBackColorChanged (e);
    }

    /// <summary>
    /// Called when the parent's BackColor changes. A control that never set its own background
    /// resolves it from the parent (see <see cref="GetEffectiveBackgroundColor"/>), so its own
    /// BackColor changed too.
    /// </summary>
    [EditorBrowsable (EditorBrowsableState.Advanced)]
    protected virtual void OnParentBackColorChanged (EventArgs e)
    {
        if (Style.TryGetBackgroundColor () is null)
            OnBackColorChanged (e);
    }

    /// <summary>
    /// Raises the <see cref="ForeColorChanged"/> event and cascades to children that inherit their
    /// foreground ambiently.
    /// </summary>
    protected virtual void OnForeColorChanged (EventArgs e)
    {
        Invalidate ();

        (Events[s_foreColorChangedEvent] as EventHandler)?.Invoke (this, e);

        if (Properties.GetObject (s_controlsCollectionProperty) is ControlCollection collection)
            for (var i = 0; i < collection.Count; i++)
                collection[i].OnParentForeColorChanged (e);
    }

    /// <summary>
    /// Called when the parent's ForeColor changes.
    /// </summary>
    [EditorBrowsable (EditorBrowsableState.Advanced)]
    protected virtual void OnParentForeColorChanged (EventArgs e)
    {
        if (Style.ForegroundColor is null)
            OnForeColorChanged (e);
    }

    /// <summary>
    /// Raises the <see cref="FontChanged"/> event and cascades to children that inherit their font
    /// ambiently. A font change can resize text, so the control also re-lays-out.
    /// </summary>
    protected virtual void OnFontChanged (EventArgs e)
    {
        Invalidate ();

        (Events[s_fontChangedEvent] as EventHandler)?.Invoke (this, e);

        if (Properties.GetObject (s_controlsCollectionProperty) is ControlCollection collection)
            for (var i = 0; i < collection.Count; i++)
                collection[i].OnParentFontChanged (e);

        PerformLayout (this, nameof (Font));
    }

    /// <summary>
    /// Called when the parent's Font changes. A control that never set its own font resolves it
    /// from the parent (see <see cref="Font"/>), so its own Font changed too.
    /// </summary>
    [EditorBrowsable (EditorBrowsableState.Advanced)]
    protected virtual void OnParentFontChanged (EventArgs e)
    {
        if (_font is null)
            OnFontChanged (e);
    }

    /// <summary>
    /// Raises the <see cref="RightToLeftChanged"/> event and cascades to children that inherit
    /// their reading order.
    /// </summary>
    protected virtual void OnRightToLeftChanged (EventArgs e)
    {
        Invalidate ();

        (Events[s_rightToLeftChangedEvent] as EventHandler)?.Invoke (this, e);

        if (Properties.GetObject (s_controlsCollectionProperty) is ControlCollection collection)
            for (var i = 0; i < collection.Count; i++)
                if (collection[i].right_to_left == RightToLeft.Inherit)
                    collection[i].OnRightToLeftChanged (e);
    }

    #endregion

    #region Handle lifetime

    /// <summary>
    /// Raises the <see cref="HandleCreated"/> event. Majorsilence.Forms has no HWND; this fires from
    /// <see cref="CreateControl"/>, the point at which the control becomes live.
    /// </summary>
    protected virtual void OnHandleCreated (EventArgs e) => (Events[s_handleCreatedEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="HandleDestroyed"/> event (fires when the control is disposed).
    /// </summary>
    protected virtual void OnHandleDestroyed (EventArgs e) => (Events[s_handleDestroyedEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Destroys the handle associated with this control. Called once, from <see cref="Dispose(bool)"/>,
    /// immediately before <see cref="OnHandleDestroyed"/>/<see cref="HandleDestroyed"/> fire -- ported
    /// code commonly overrides it to release window-lifetime resources at that exact point. See
    /// <see cref="Form.DestroyHandle"/> for the matching hook on the Form side (Form doesn't derive from
    /// Control here, so it needed its own copy rather than inheriting this one).
    /// </summary>
    protected virtual void DestroyHandle () => OnHandleDestroyed (EventArgs.Empty);

    #endregion

    #region Focus / validation

    /// <summary>
    /// Raises the <see cref="Enter"/> event. Called by <see cref="OnGotFocus"/>, before GotFocus.
    /// </summary>
    protected virtual void OnEnter (EventArgs e) => (Events[s_enterEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="Leave"/> event. Called by <see cref="OnLostFocus"/>, before LostFocus.
    /// </summary>
    protected virtual void OnLeave (EventArgs e) => (Events[s_leaveEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="CausesValidationChanged"/> event.
    /// </summary>
    protected virtual void OnCausesValidationChanged (EventArgs e) => (Events[s_causesValidationChangedEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="ImeModeChanged"/> event.
    /// </summary>
    protected virtual void OnImeModeChanged (EventArgs e) => (Events[s_imeModeChangedEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="ContextMenuStripChanged"/> event.
    /// </summary>
    protected virtual void OnContextMenuStripChanged (EventArgs e) => (Events[s_contextMenuStripChangedEvent] as EventHandler)?.Invoke (this, e);

    #endregion

    #region Mouse / keyboard

    /// <summary>
    /// Raises the <see cref="MouseClick"/> event. Called by <see cref="OnClick"/>.
    /// </summary>
    protected virtual void OnMouseClick (MouseEventArgs e) => (Events[s_mouseClickEvent] as MouseEventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="MouseDoubleClick"/> event. Called by <see cref="OnDoubleClick(MouseEventArgs)"/>.
    /// </summary>
    protected virtual void OnMouseDoubleClick (MouseEventArgs e) => (Events[s_mouseDoubleClickEvent] as MouseEventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="MouseHover"/> event. Majorsilence.Forms has no hover timer, so this is
    /// called once per mouse entry from <see cref="OnMouseEnter"/>.
    /// </summary>
    protected virtual void OnMouseHover (EventArgs e) => (Events[s_mouseHoverEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="MouseCaptureChanged"/> event. Called when <see cref="Capture"/> changes.
    /// </summary>
    protected virtual void OnMouseCaptureChanged (EventArgs e) => (Events[s_mouseCaptureChangedEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>
    /// Raises the <see cref="PreviewKeyDown"/> event, called just before <see cref="OnKeyDown"/>.
    /// </summary>
    protected virtual void OnPreviewKeyDown (PreviewKeyDownEventArgs e) => (Events[s_previewKeyDownEvent] as PreviewKeyDownEventHandler)?.Invoke (this, e);

    #endregion

    #region Drag and drop

    // Majorsilence.Forms has no OS drag source yet (DoDragDrop returns None), so nothing in the
    // framework calls these. They are wired to real events so a derived control can raise/override
    // them today and a future backend can drive them without an API change.

    /// <summary>Raises the <see cref="DragEnter"/> event.</summary>
    protected virtual void OnDragEnter (DragEventArgs e) => (Events[s_dragEnterEvent] as DragEventHandler)?.Invoke (this, e);

    /// <summary>Raises the <see cref="DragOver"/> event.</summary>
    protected virtual void OnDragOver (DragEventArgs e) => (Events[s_dragOverEvent] as DragEventHandler)?.Invoke (this, e);

    /// <summary>Raises the <see cref="DragDrop"/> event.</summary>
    protected virtual void OnDragDrop (DragEventArgs e) => (Events[s_dragDropEvent] as DragEventHandler)?.Invoke (this, e);

    /// <summary>Raises the <see cref="DragLeave"/> event.</summary>
    protected virtual void OnDragLeave (EventArgs e) => (Events[s_dragLeaveEvent] as EventHandler)?.Invoke (this, e);

    /// <summary>Raises the <see cref="GiveFeedback"/> event.</summary>
    protected virtual void OnGiveFeedback (GiveFeedbackEventArgs e) => (Events[s_giveFeedbackEvent] as GiveFeedbackEventHandler)?.Invoke (this, e);

    /// <summary>Raises the <see cref="QueryContinueDrag"/> event.</summary>
    protected virtual void OnQueryContinueDrag (QueryContinueDragEventArgs e) => (Events[s_queryContinueDragEvent] as QueryContinueDragEventHandler)?.Invoke (this, e);

    #endregion

    #region Printing

    /// <summary>
    /// Paints the control to the supplied surface without going through the normal invalidation
    /// path. WinForms uses this for WM_PRINTCLIENT; here it simply draws the background and then
    /// the control, which is what <see cref="DrawToBitmap"/>-style callers need.
    /// </summary>
    protected virtual void OnPrint (PaintEventArgs e)
    {
        Guard.ThrowIfNull (e);

        OnPaintBackground (e);
        OnPaint (e);
    }

    #endregion

    #region Reset* (designer serialization)

    /// <summary>
    /// Clears any explicitly-set background color so the control resolves it ambiently again
    /// (parent control, then hosting window, then theme).
    /// </summary>
    public virtual void ResetBackColor ()
    {
        if (Style.BackgroundColor is null)
            return;

        Style.BackgroundColor = null;
        OnBackColorChanged (EventArgs.Empty);
    }

    /// <summary>
    /// Clears any explicitly-set foreground color so the control resolves it from its style chain
    /// and the theme again.
    /// </summary>
    public virtual void ResetForeColor ()
    {
        if (Style.ForegroundColor is null)
            return;

        Style.ForegroundColor = null;
        OnForeColorChanged (EventArgs.Empty);
    }

    /// <summary>
    /// Clears any explicitly-set cursor so the control inherits its parent's cursor, or
    /// <see cref="DefaultCursor"/> at the top of the chain.
    /// </summary>
    public virtual void ResetCursor ()
    {
        if (Properties.GetObject (s_cursorProperty) is null)
            return;

        Properties.SetObject (s_cursorProperty, null);
        OnCursorChanged (EventArgs.Empty);
    }

    /// <summary>
    /// Resets <see cref="RightToLeft"/> to <see cref="RightToLeft.Inherit"/>, so the control takes
    /// its reading order from its parent again.
    /// </summary>
    public virtual void ResetRightToLeft () => RightToLeft = RightToLeft.Inherit;

    /// <summary>
    /// Resets <see cref="ImeMode"/> to <see cref="DefaultImeMode"/>.
    /// </summary>
    public void ResetImeMode () => ImeMode = DefaultImeMode;

    #endregion

    #region RTL translation helpers

    /// <summary>
    /// Mirrors a horizontal alignment when the control reads right-to-left.
    /// </summary>
    protected HorizontalAlignment RtlTranslateAlignment (HorizontalAlignment align) => RtlTranslateHorizontal (align);

    /// <summary>
    /// Mirrors a left/right alignment when the control reads right-to-left.
    /// </summary>
    protected LeftRightAlignment RtlTranslateAlignment (LeftRightAlignment align) => RtlTranslateLeftRight (align);

    /// <summary>
    /// Mirrors a content alignment when the control reads right-to-left.
    /// </summary>
    protected ContentAlignment RtlTranslateAlignment (ContentAlignment align) => RtlTranslateContent (align);

    /// <summary>
    /// Mirrors a horizontal alignment when the control reads right-to-left. Center is unchanged.
    /// </summary>
    protected HorizontalAlignment RtlTranslateHorizontal (HorizontalAlignment align)
    {
        if (RightToLeft.Yes != RightToLeft)
            return align;

        return align switch {
            HorizontalAlignment.Left => HorizontalAlignment.Right,
            HorizontalAlignment.Right => HorizontalAlignment.Left,
            _ => align
        };
    }

    /// <summary>
    /// Mirrors a left/right alignment when the control reads right-to-left.
    /// </summary>
    protected LeftRightAlignment RtlTranslateLeftRight (LeftRightAlignment align)
    {
        if (RightToLeft.Yes != RightToLeft)
            return align;

        return align == LeftRightAlignment.Left ? LeftRightAlignment.Right : LeftRightAlignment.Left;
    }

    /// <summary>
    /// Mirrors a content alignment horizontally when the control reads right-to-left. The vertical
    /// component (Top/Middle/Bottom) and the horizontally centered values are unchanged.
    /// </summary>
    protected ContentAlignment RtlTranslateContent (ContentAlignment align)
    {
        if (RightToLeft.Yes != RightToLeft)
            return align;

        return align switch {
            ContentAlignment.TopLeft => ContentAlignment.TopRight,
            ContentAlignment.TopRight => ContentAlignment.TopLeft,
            ContentAlignment.MiddleLeft => ContentAlignment.MiddleRight,
            ContentAlignment.MiddleRight => ContentAlignment.MiddleLeft,
            ContentAlignment.BottomLeft => ContentAlignment.BottomRight,
            ContentAlignment.BottomRight => ContentAlignment.BottomLeft,
            _ => align
        };
    }

    #endregion

    #region Scaling

    /// <summary>
    /// Scales the control's bounds by the given factor. Only the components named by
    /// <paramref name="specified"/> are scaled; the rest keep their current value.
    /// </summary>
    protected virtual void ScaleControl (SizeF factor, BoundsSpecified specified)
    {
        var scaled = GetScaledBounds (Bounds, factor, specified);

        SetBounds (scaled.X, scaled.Y, scaled.Width, scaled.Height, BoundsSpecified.All);
    }

    /// <summary>
    /// Replaces <paramref name="logicalBitmap"/> with a copy scaled from logical (96 DPI) units to
    /// this control's device units. A no-op when the control is running at 96 DPI.
    /// </summary>
    public void ScaleBitmapLogicalToDevice (ref Majorsilence.Forms.Drawing.Bitmap logicalBitmap)
        => logicalBitmap = ScaleBitmapLogicalToDevice (logicalBitmap, DeviceDpi);

    // Split out from the ref-taking protected overload so the scaling itself is testable at a DPI
    // other than the headless backend's 96.
    internal static Majorsilence.Forms.Drawing.Bitmap ScaleBitmapLogicalToDevice (Majorsilence.Forms.Drawing.Bitmap logicalBitmap, int deviceDpi)
    {
        if (logicalBitmap is null)
            return logicalBitmap!;

        var device = new Size (DpiHelper.LogicalToDeviceUnits (logicalBitmap.Width, deviceDpi),
                               DpiHelper.LogicalToDeviceUnits (logicalBitmap.Height, deviceDpi));

        if (device == logicalBitmap.Size)
            return logicalBitmap;

        return new Majorsilence.Forms.Drawing.Bitmap (logicalBitmap, device);
    }

    #endregion
}
