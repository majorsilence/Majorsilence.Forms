using System;

namespace Majorsilence.Drawing
{
    /// <summary>
    /// Animates images that have time-based frames (e.g. animated GIFs). Cross-platform replacement for
    /// System.Drawing.ImageAnimator. Majorsilence.Drawing renders a single frame, so this is a documented
    /// no-op: <see cref="CanAnimate"/> always reports false and the animate hooks do nothing. Static
    /// images render correctly regardless.
    /// </summary>
    public static class ImageAnimator
    {
        /// <summary>Returns false — multi-frame animation is not supported.</summary>
        public static bool CanAnimate (Image? image) => false;

        /// <summary>No-op: there are no animated frames to advance.</summary>
        public static void Animate (Image image, EventHandler onFrameChangedHandler) { }

        /// <summary>No-op: nothing was registered to stop.</summary>
        public static void StopAnimate (Image image, EventHandler onFrameChangedHandler) { }

        /// <summary>No-op: the current (single) frame is always up to date.</summary>
        public static void UpdateFrames () { }

        /// <summary>No-op: the current (single) frame is always up to date.</summary>
        public static void UpdateFrames (Image image) { }
    }
}
