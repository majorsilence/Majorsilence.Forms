using System;
using System.Drawing;
using Majorsilence.Forms.Telerik;

namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Draws a <see cref="RadToggleSwitch"/> as a switch: a rounded track, accent-filled when on, a
    /// thumb at the end it is switched to, and <see cref="RadToggleSwitch.OnText"/> or
    /// <see cref="RadToggleSwitch.OffText"/> in the half the thumb has left (W6 mechanisms, #176).
    /// The control is a <see cref="CheckBox"/> underneath, which is why it drew as one before.
    /// </summary>
    public class RadToggleSwitchRenderer : Renderer<RadToggleSwitch>
    {
        /// <inheritdoc/>
        protected override void Render (RadToggleSwitch control, PaintEventArgs e)
        {
            var (track, thumb) = control.SwitchGeometry ();

            if (track.Width <= 0 || track.Height <= 0)
                return;

            var on = control.Checked;
            var enabled = control.Enabled;
            var track_color = on && enabled ? Theme.AccentColor : Theme.ControlMidColor;
            var radius = track.Height / 2;

            e.Canvas.FillRoundedRectangle (track.X, track.Y, track.Width, track.Height, track_color, radius, radius);
            e.Canvas.FillRoundedRectangle (thumb.X, thumb.Y, thumb.Width, thumb.Height, Theme.ControlHighColor, thumb.Height / 2, thumb.Height / 2);

            // The caption sits in the half of the track the thumb is not in.
            var text = on ? control.OnText : control.OffText;

            if (string.IsNullOrEmpty (text))
                return;

            var free = on
                ? new Rectangle (track.X, track.Y, Math.Max (0, thumb.Left - track.X), track.Height)
                : new Rectangle (thumb.Right, track.Y, Math.Max (0, track.Right - thumb.Right), track.Height);

            var fore = !enabled ? Theme.ForegroundDisabledColor : on ? Theme.ForegroundColorOnAccent : Theme.ForegroundColor;

            e.Canvas.DrawText (text, control.GetEffectiveFont (), control.LogicalToDeviceUnits (control.GetEffectiveFontSize ()),
                free, fore, ContentAlignment.MiddleCenter, maxLines: 1);
        }
    }
}
