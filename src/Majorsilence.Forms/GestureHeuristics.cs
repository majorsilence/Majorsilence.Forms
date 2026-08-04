using System;

namespace Majorsilence.Forms
{
    /// <summary>
    /// Backend-neutral decisions a gesture wiring has to make when the platform does not make them.
    /// </summary>
    /// <remarks>
    /// <para>Avalonia has dedicated recognizers that answer these questions itself, so its wiring does
    /// not need this. WinUI's manipulation model does not: it reports one undifferentiated stream of
    /// scale, rotation and translation, and has no swipe gesture at all. The rules for turning that
    /// into <see cref="Control.Pinch"/>, <see cref="Control.ScrollGesture"/> and
    /// <see cref="Control.Swipe"/> live here rather than inline in the Uno wiring for one reason: a
    /// heuristic buried in an event-handler lambda inside a backend assembly cannot be unit-tested,
    /// and this one cannot be verified by running it either -- no sandbox has multi-touch hardware.</para>
    /// <para><b>Velocity is in pixels per second here</b>, deliberately and explicitly. Avalonia's
    /// <c>SwipeGestureEventArgs.Velocity</c> is documented as pixels per second; WinUI's
    /// <c>ManipulationVelocities.Linear</c> is documented as DIP per <i>millisecond</i>. Handing those
    /// two straight to the same neutral event would make <see cref="SwipeGestureEventArgs.VelocityX"/> mean
    /// something different depending on which backend an app happened to be running on.</para>
    /// </remarks>
    internal static class GestureHeuristics
    {
        /// <summary>The speed a drag must reach, in pixels per second, to count as a swipe.</summary>
        /// <remarks>A chosen number, not a platform default -- WinUI has no swipe gesture to read one
        /// from. Roughly the speed of a deliberate flick: a slow drag is a pan, and reporting it as a
        /// swipe as well would fire navigation on an ordinary scroll.</remarks>
        internal const double MinSwipeVelocity = 500d;

        /// <summary>Milliseconds per second, for converting a per-millisecond platform velocity.</summary>
        internal const double MillisecondsPerSecond = 1000d;

        // How far a frame's scale and rotation may drift from "unchanged" and still be treated as a
        // pure pan. Two contacts dragged across the screen never hold a mathematically exact scale of
        // 1.0, so testing against exact equality would classify every two-finger pan as a pinch --
        // and, because the two are mutually exclusive below, two-finger panning would never scroll.
        private const double ScaleTolerance = 0.005d;
        private const double RotationTolerance = 0.5d;

        /// <summary>
        /// Returns whether a manipulation frame is a deliberate pinch or rotate, rather than a pan.
        /// </summary>
        /// <param name="scaleDelta">This frame's incremental scale factor, where 1.0 is unchanged.</param>
        /// <param name="rotationDelta">This frame's incremental rotation, in degrees.</param>
        internal static bool IsPinchFrame (double scaleDelta, double rotationDelta)
            => Math.Abs (scaleDelta - 1d) > ScaleTolerance || Math.Abs (rotationDelta) > RotationTolerance;

        /// <summary>
        /// Returns whether a finished manipulation was fast enough to be a swipe, and in which direction.
        /// </summary>
        /// <param name="velocityX">Horizontal velocity, in pixels per second.</param>
        /// <param name="velocityY">Vertical velocity, in pixels per second.</param>
        /// <param name="direction">The direction of travel, when the result is true.</param>
        internal static bool TryClassifySwipe (double velocityX, double velocityY, out SwipeDirection direction)
        {
            direction = default;

            // Compared squared, so a diagonal flick is measured by its actual speed rather than by
            // whichever axis happens to be larger.
            if ((velocityX * velocityX) + (velocityY * velocityY) < MinSwipeVelocity * MinSwipeVelocity)
                return false;

            direction = Math.Abs (velocityX) >= Math.Abs (velocityY)
                ? velocityX >= 0 ? SwipeDirection.Right : SwipeDirection.Left
                : velocityY >= 0 ? SwipeDirection.Down : SwipeDirection.Up;

            return true;
        }
    }
}
