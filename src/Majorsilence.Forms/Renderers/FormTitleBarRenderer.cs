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

            // Form text
            e.Canvas.DrawText (control.Text.Trim (), Theme.UIFont, e.LogicalToDeviceUnits (Theme.FontSize), control.ScaledBounds, color, ContentAlignment.MiddleCenter);
        }
    }
}
