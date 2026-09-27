using System;

namespace Majorsilence.Forms.Animation
{
    /// <summary>Runs a <see cref="Tween{T}"/> on a control, one step per display frame (<see cref="Control.RequestAnimationFrame"/>).</summary>
    public static class Animator
    {
        /// <summary>
        /// Starts a tween. On each frame <paramref name="apply"/> gets the value for that moment, on the UI thread, and it gets the end value
        /// on the last frame, after which <paramref name="completed"/> runs once. The first frame is time zero, so the start value is applied
        /// then and the movement takes its full <see cref="Tween{T}.Duration"/>.
        /// </summary>
        /// <param name="control">The control whose frames drive it. The animation stops if the control is disposed.</param>
        /// <param name="tween">What to move and how.</param>
        /// <param name="apply">Puts the value on the control (and usually invalidates it).</param>
        /// <param name="completed">Runs once when the end is reached. It does not run if the animation is cancelled.</param>
        /// <returns>A handle that can cancel it.</returns>
        /// <remarks>
        /// It does not read the user's reduced-motion setting; whether to animate is the caller's decision. An exception from
        /// <paramref name="apply"/> or <paramref name="completed"/> ends the animation and is thrown from the frame callback.
        /// </remarks>
        public static AnimationHandle Animate<T> (this Control control, Tween<T> tween, Action<T> apply, Action? completed = null)
        {
            Guard.ThrowIfNull (control);
            Guard.ThrowIfNull (tween);
            Guard.ThrowIfNull (apply);

            var run = new Run<T> (control.RequestAnimationFrame, () => control.IsDisposed, tween, apply, completed);
            control.RequestAnimationFrame (run.OnFrame);
            return run.Handle;
        }

        /// <summary>
        /// Starts a tween on a window, which is not a <see cref="Control"/> in this framework, so it can animate what belongs to the window
        /// (its size, position or opacity). Everything in <see cref="Animate{T}(Control, Tween{T}, Action{T}, Action?)"/> applies.
        /// </summary>
        /// <param name="window">The window whose frames drive it. The animation stops if the window is disposed.</param>
        /// <param name="tween">What to move and how.</param>
        /// <param name="apply">Puts the value on the window.</param>
        /// <param name="completed">Runs once when the end is reached. It does not run if the animation is cancelled.</param>
        /// <returns>A handle that can cancel it.</returns>
        public static AnimationHandle Animate<T> (this WindowBase window, Tween<T> tween, Action<T> apply, Action? completed = null)
        {
            Guard.ThrowIfNull (window);
            Guard.ThrowIfNull (tween);
            Guard.ThrowIfNull (apply);

            var run = new Run<T> (window.RequestAnimationFrame, () => window.IsDisposed, tween, apply, completed);
            window.RequestAnimationFrame (run.OnFrame);
            return run.Handle;
        }

        private sealed class Run<T> (
            Action<Action<TimeSpan>> requestFrame, Func<bool> isDisposed, Tween<T> tween, Action<T> apply, Action? completed)
        {
            private TimeSpan? start;

            public AnimationHandle Handle { get; } = new ();

            public void OnFrame (TimeSpan timestamp)
            {
                if (!Handle.IsRunning)
                    return;

                if (isDisposed ()) {
                    Handle.Cancel ();
                    return;
                }

                start ??= timestamp;
                var elapsed = timestamp - start.Value;

                try {
                    apply (tween.ValueAt (elapsed));

                    // The apply callback may have cancelled it, so ask again before going on.
                    if (!Handle.IsRunning)
                        return;

                    if (tween.IsComplete (elapsed)) {
                        Handle.Finish ();
                        completed?.Invoke ();
                        return;
                    }
                } catch {
                    Handle.Cancel ();
                    throw;
                }

                requestFrame (OnFrame);
            }
        }
    }

    /// <summary>A running animation, which can be cancelled.</summary>
    public sealed class AnimationHandle
    {
        /// <summary>Gets whether the animation is still going: true from the start until it completes or is cancelled.</summary>
        public bool IsRunning { get; private set; } = true;

        /// <summary>Stops the animation where it is. No more values are applied and the completion callback does not run. Safe to call again.</summary>
        public void Cancel () => IsRunning = false;

        internal void Finish () => IsRunning = false;
    }
}
