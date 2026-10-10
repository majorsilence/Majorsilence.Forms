namespace Majorsilence.Forms
{
    /// <summary>
    /// Notices a keyboard inset the form is still holding after the keyboard has gone. Android reports the keyboard opening, but when the window
    /// loses focus to another activity (a file picker, the share sheet) it may never report it closing, so the form stayed short by the
    /// keyboard's height for good. The host asks the input pane for its real state on every layout and clears the inset when it says closed.
    /// </summary>
    internal sealed class InputPaneResync
    {
        private bool occluding;

        /// <summary>The keyboard's occluded height as last reported by an event; zero when it closed.</summary>
        internal void Changed (int occludedHeight) => occluding = occludedHeight > 0;

        /// <summary>True once, when the form is still holding an inset and the input pane says it is closed.</summary>
        internal bool ShouldClear (bool paneClosed)
        {
            if (!occluding || !paneClosed)
                return false;

            occluding = false;
            return true;
        }
    }
}
