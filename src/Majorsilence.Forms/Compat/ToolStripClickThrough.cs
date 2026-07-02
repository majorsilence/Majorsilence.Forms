namespace Majorsilence.Forms
{
    /// <summary>
    ///  A <see cref="ToolStrip"/> that activates and focuses itself on the first click, matching legacy WinForms click-through behavior.
    /// </summary>
    public class ToolStripClickThrough : ToolStrip
    {
        /// <summary>
        ///  Processes Windows messages, focusing the control on mouse activation before passing the message to the base implementation.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            ClickThroughHelper.HandleMouseActivate(this, ref m);
            base.WndProc(ref m);
        }
    }
}
