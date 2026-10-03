namespace Majorsilence.Forms
{
    public partial class Form
    {
        /// <summary>The width, in logical pixels, from which a form is no longer <see cref="WindowSizeClass.Compact"/>.</summary>
        public const int MediumWidthBreakpoint = 600;

        /// <summary>The width, in logical pixels, from which a form is <see cref="WindowSizeClass.Expanded"/>.</summary>
        public const int ExpandedWidthBreakpoint = 840;

        private WindowSizeClass? last_size_class;

        /// <summary>
        /// The <see cref="WindowSizeClass"/> for the form's current client width. Use it instead of
        /// comparing <see cref="ClientSize"/> against a pixel breakpoint (or checking the operating system)
        /// when choosing between a single-column and a side-by-side layout. It follows the window, so it
        /// changes on rotation and when a desktop window is resized.
        /// </summary>
        public WindowSizeClass SizeClass => ClassForWidth (ClientSize.Width);

        /// <summary>
        /// Raised after the client area is resized into a different <see cref="SizeClass"/>, not on every
        /// resize, so it is the place to switch layouts. Like <see cref="WindowBase.SizeChanged"/> it is
        /// raised by the render pass that picks up the new size. The first size the form is drawn at only
        /// sets the baseline, so read <see cref="SizeClass"/> once when building the layout.
        /// </summary>
        public event EventHandler? SizeClassChanged;

        /// <summary>Raises the <see cref="SizeClassChanged"/> event.</summary>
        protected virtual void OnSizeClassChanged (EventArgs e) => SizeClassChanged?.Invoke (this, e);

        /// <summary>Maps a client width in logical pixels onto its <see cref="WindowSizeClass"/>.</summary>
        public static WindowSizeClass ClassForWidth (int width) =>
            width >= ExpandedWidthBreakpoint ? WindowSizeClass.Expanded
            : width >= MediumWidthBreakpoint ? WindowSizeClass.Medium
            : WindowSizeClass.Compact;

        /// <inheritdoc/>
        protected override void OnSizeChanged (EventArgs e)
        {
            base.OnSizeChanged (e);

            var current = SizeClass;
            var previous = last_size_class;
            last_size_class = current;

            // The first size we see only establishes the baseline; it is not a change.
            if (previous is not null && previous != current)
                OnSizeClassChanged (EventArgs.Empty);
        }
    }
}
