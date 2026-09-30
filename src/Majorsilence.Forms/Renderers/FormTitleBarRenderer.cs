namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a FormTitleBar.
    /// </summary>
    public class FormTitleBarRenderer : Renderer<FormTitleBar>
    {
        /// <inheritdoc/>
        protected override void Render (FormTitleBar control, PaintEventArgs e)
        {
            // Skip the title text when the title bar hosts its own content (e.g. a tab strip).
            if (!control.ShowText)
                return;

            // A title bar merged into the native OS title bar blends with the window background, so use
            // the normal foreground color; the accent-colored custom title bar uses the on-accent color.
            var color = control.NativeOverlay ? Theme.ForegroundColor : Theme.ForegroundColorOnAccent;

            var text = control.Text.Trim ();
            var font_size = e.LogicalToDeviceUnits (Theme.FontSize);
            var bounds = control.ScaledBounds;

            // Centred on the whole bar when it fits clear of the icon and the caption buttons; a longer
            // title is laid out in the room between them and ellipsised. Centring it on the whole bar
            // regardless ran a long caption -- a form title carrying a server/database name -- under
            // the minimize button.
            var left = e.LogicalToDeviceUnits (control.TitleLeftInset);
            var right = e.LogicalToDeviceUnits (control.TitleRightInset);
            var inset = System.Math.Max (left, right);
            var width = TextMeasurer.MeasureText (text, Theme.UIFont, font_size).Width;

            if (width > bounds.Width - (2 * inset))
                bounds = new System.Drawing.Rectangle (bounds.X + left, bounds.Y, System.Math.Max (0, bounds.Width - left - right), bounds.Height);

            e.Canvas.DrawText (text, Theme.UIFont, font_size, bounds, color, ContentAlignment.MiddleCenter, maxLines: 1, ellipsis: true);
        }
    }
}
