using System;

namespace Majorsilence.Forms
{
    public partial class Control
    {
        /// <summary>
        /// Calls <paramref name="callback"/> once, at the start of the next display frame, with a timestamp that increases from frame to
        /// frame. To keep animating, request the next frame from inside the callback.
        /// </summary>
        /// <param name="callback">Receives the frame's timestamp. Only differences between timestamps mean anything.</param>
        /// <remarks>
        /// Call it on the UI thread. On the Avalonia backend it is the top level's frame request, so it is aligned to the display; a
        /// backend without one gets a timer at about 60 Hz; the Headless backend steps by hand
        /// (<c>HeadlessRenderer.AnimationClock</c>), so an animation test is deterministic. A request made inside a callback is served on
        /// the following frame, not the same one.
        /// </remarks>
        public void RequestAnimationFrame (Action<TimeSpan> callback) => AnimationFrames.Request (FindWindow (), callback);
    }

    public partial class WindowBase
    {
        /// <inheritdoc cref="Control.RequestAnimationFrame"/>
        public void RequestAnimationFrame (Action<TimeSpan> callback) => AnimationFrames.Request (this, callback);
    }
}
