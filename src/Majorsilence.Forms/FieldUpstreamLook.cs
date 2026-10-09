using SkiaSharp;

namespace Majorsilence.Forms
{
    // A ported WinForms app (one that chose its font with Application.SetDefaultFont) gets the text and
    // list boxes WinForms draws under the Windows 11 theme, colours measured from it: a white field
    // (SystemColors.Window) inside a light outline, a text box with a darker line along its bottom edge,
    // and a single-line text box as tall as its font makes it (upstream's AutoSize: Font.Height + 7, 23px
    // for Segoe UI 9pt). The theme's off-white field and grey frame, 20px tall where the designer left
    // it, read as a different control. Fixed3D (the default) borders only; apps that have not chosen a
    // font keep the theme's fields.
    internal static class FieldUpstreamLook
    {
        internal static readonly SKColor Window = SKColors.White;
        internal static readonly SKColor Control = new SKColor (0xF0, 0xF0, 0xF0);
        internal static readonly SKColor TextBoxOutline = new SKColor (0xEC, 0xEC, 0xEC);
        internal static readonly SKColor TextBoxBottom = new SKColor (0x83, 0x83, 0x83);
        internal static readonly SKColor ListBoxOutline = new SKColor (0x82, 0x87, 0x90);

        internal static bool Applies => ControlPaint.UsesUpstreamGlyphs;

        // Fills the field and draws its 1px outline; bottom, when given, is the colour of the bottom edge.
        internal static void Paint (PaintEventArgs e, int width, int height, SKColor face, SKColor outline, SKColor? bottom)
        {
            var scale = (float) e.Scaling;

            e.Canvas.Clear (face);

            using var paint = new SKPaint { Color = outline };
            e.Canvas.DrawRect (new SKRect (0, 0, width, scale), paint);
            e.Canvas.DrawRect (new SKRect (0, 0, scale, height), paint);
            e.Canvas.DrawRect (new SKRect (width - scale, 0, width, height), paint);

            paint.Color = bottom ?? outline;
            e.Canvas.DrawRect (new SKRect (0, height - scale, width, height), paint);
        }
    }

    public partial class TextBox
    {
        /// <inheritdoc/>
        internal override SKColor? UpstreamDefaultBackColor
            => FieldUpstreamLook.Applies && Enabled && !ReadOnly ? FieldUpstreamLook.Window : null;

        private bool PaintsUpstreamField => FieldUpstreamLook.Applies && BorderStyle == BorderStyle.Fixed3D && BackgroundImage is null;

        // Upstream's AutoSize height for a single-line box: the font's height plus the border and its
        // spacing (SystemInformation border size x 4 + 3), or the font's height alone without a border.
        private int UpstreamHeight => Font.Height + (BorderStyle == BorderStyle.None ? 0 : 7);

        private bool SizesToUpstreamHeight => FieldUpstreamLook.Applies && !Multiline;

        private void PaintUpstreamField (PaintEventArgs e)
        {
            using var device = e.DeviceSpace ();

            // Read-only and disabled boxes show SystemColors.Control, as upstream's do.
            var face = !Enabled || ReadOnly && Style.BackgroundColor is null ? FieldUpstreamLook.Control : GetEffectiveBackgroundColor ();
            FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.TextBoxOutline, FieldUpstreamLook.TextBoxBottom);
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore (int x, int y, int width, int height, BoundsSpecified specified)
        {
            if (SizesToUpstreamHeight)
                height = UpstreamHeight;

            base.SetBoundsCore (x, y, width, height, specified);
        }
    }

    public partial class ListBox
    {
        /// <inheritdoc/>
        internal override SKColor? UpstreamDefaultBackColor
            => FieldUpstreamLook.Applies && Enabled ? FieldUpstreamLook.Window : null;

        private bool PaintsUpstreamField => FieldUpstreamLook.Applies && BorderStyle == BorderStyle.Fixed3D && BackgroundImage is null;

        private void PaintUpstreamField (PaintEventArgs e)
        {
            using var device = e.DeviceSpace ();

            var face = Enabled ? GetEffectiveBackgroundColor () : FieldUpstreamLook.Control;
            FieldUpstreamLook.Paint (e, ScaledWidth, ScaledHeight, face, FieldUpstreamLook.ListBoxOutline, null);
        }
    }
}
