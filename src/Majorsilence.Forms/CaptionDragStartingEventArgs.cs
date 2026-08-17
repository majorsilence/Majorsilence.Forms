using System;
using System.Drawing;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Carries the start of a caption drag, and lets a handler claim it.
    /// See <see cref="WindowBase.CaptionDragStarting"/>.
    /// </summary>
    public class CaptionDragStartingEventArgs : EventArgs
    {
        /// <summary>Creates the arguments for a caption drag beginning at <paramref name="location"/>.</summary>
        public CaptionDragStartingEventArgs (Point location) => Location = location;

        /// <summary>Where in the caption the drag started, in the caption's own coordinates.</summary>
        public Point Location { get; }

        /// <summary>
        /// Set to true to stop the window moving, leaving the gesture to the application — what a
        /// docking library does to turn a title-bar drag into a re-dock.
        /// </summary>
        public bool Cancel { get; set; }
    }
}
