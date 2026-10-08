using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the push button
    // WinForms draws under the Windows 11 theme, colours measured from it: a near-white rounded face with
    // a light outline and a darker bottom edge, the default button outlined in the accent, a pale blue
    // face under the pointer. The theme's square grey button, filled with the accent on hover and framed
    // two pixels thick when default, read as a different control beside WinForms'. Only where upstream
    // draws the themed button too -- a Standard or System button keeping its visual-style back colour.
    // Apps that have not chosen a font keep the theme's button.
    public partial class Button
    {
        private static readonly SKColor FaceColor = new SKColor (0xFD, 0xFD, 0xFD);
        private static readonly SKColor OutlineColor = new SKColor (0xD0, 0xD0, 0xD0);
        private static readonly SKColor OutlineBottomColor = new SKColor (0xBA, 0xBA, 0xBA);
        private static readonly SKColor AccentOutlineColor = new SKColor (0x00, 0x78, 0xD4);
        private static readonly SKColor AccentBottomColor = new SKColor (0x00, 0x6B, 0xBE);
        private static readonly SKColor HoverFaceColor = new SKColor (0xE0, 0xEE, 0xF9);
        private static readonly SKColor PressedFaceColor = new SKColor (0xCC, 0xE4, 0xF7);
        private static readonly SKColor PressedOutlineColor = new SKColor (0x00, 0x54, 0x99);
        private static readonly SKColor DisabledFaceColor = new SKColor (0xF5, 0xF5, 0xF5);

        /// <summary>Whether this button paints upstream's themed face rather than the theme's.</summary>
        internal bool PaintsUpstreamLook
            => ControlPaint.UsesUpstreamGlyphs
               && UseVisualStyleBackColor
               && FlatStyle is FlatStyle.Standard or FlatStyle.System
               && Style.BackgroundColor is null;

        /// <inheritdoc/>
        protected override void OnPaintBackground (PaintEventArgs e)
        {
            if (!PaintsUpstreamLook) {
                base.OnPaintBackground (e);
                return;
            }

            using var device = e.DeviceSpace ();

            // The rounded corners show what is behind the button.
            e.Canvas.Clear (Parent?.GetEffectiveBackgroundColor () ?? Theme.BackgroundColor);

            var scale = (float) e.Scaling;
            var half = 0.5f * scale;
            var radius = 4f * scale;
            var box = new SKRect (half, half, ScaledWidth - half, ScaledHeight - half);

            var pressed = IsPressed && IsHovering;
            var accent = Enabled && (IsDefault || (Selected && ShowFocusCues));

            var (face, outline, bottom) =
                !Enabled ? (DisabledFaceColor, OutlineColor, OutlineColor)
                : pressed ? (PressedFaceColor, PressedOutlineColor, PressedOutlineColor)
                : IsHovering ? (HoverFaceColor, AccentOutlineColor, AccentBottomColor)
                : accent ? (FaceColor, AccentOutlineColor, AccentBottomColor)
                : (FaceColor, OutlineColor, OutlineBottomColor);

            using var paint = new SKPaint { IsAntialias = true, Color = face };
            e.Canvas.DrawRoundRect (box, radius, radius, paint);

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = scale;
            paint.Color = outline;
            e.Canvas.DrawRoundRect (box, radius, radius, paint);

            // The darker bottom edge, between the rounded corners.
            if (bottom != outline) {
                paint.Color = bottom;
                e.Canvas.DrawLine (box.Left + radius, box.Bottom, box.Right - radius, box.Bottom, paint);
            }

            if (BackgroundImage is not null)
                PaintBackgroundImage (e);
        }
    }
}
