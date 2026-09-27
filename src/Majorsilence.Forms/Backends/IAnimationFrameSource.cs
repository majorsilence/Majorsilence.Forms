using System;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional: implemented by a window backend, or by the platform backend itself, that can call back once per display frame, so an
    /// animation is aligned to the frame instead of to a timer. A backend that does not implement it gets a timer at about 60 Hz.
    /// </summary>
    public interface IAnimationFrameSource
    {
        /// <summary>
        /// Calls <paramref name="callback"/> once, at the start of the next display frame, with a timestamp that increases from frame to
        /// frame. Only differences between timestamps mean anything. A request made from inside a callback is served on the frame after.
        /// </summary>
        void RequestAnimationFrame (Action<TimeSpan> callback);
    }
}
