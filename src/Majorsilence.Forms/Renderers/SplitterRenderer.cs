namespace Majorsilence.Forms.Renderers
{
    /// <summary>
    /// Represents a class that can render a Splitter.
    /// </summary>
    public class SplitterRenderer : Renderer<Splitter>
    {
        /// <inheritdoc/>
        protected override void Render (Splitter control, PaintEventArgs e)
        {
            // The focus cue for a SplitContainer whose splitter has focus is drawn on the bar, as
            // upstream's OnPaint does (Layout/Containers/SplitContainer.cs, DrawFocus) (LAY-06).
            if (control.Parent is SplitContainer { Focused: true })
                e.Canvas.DrawFocusRectangle (control.DeviceClientRectangle);
        }
    }
}
