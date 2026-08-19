using System;
using System.Collections.Generic;
using System.Linq;
using Majorsilence.Forms.Drawing.Imaging;

namespace Majorsilence.Forms.Drawing
{
    /// <summary>
    /// Animates images that have time-based frames (e.g. animated GIFs). Cross-platform replacement for
    /// System.Drawing.ImageAnimator.
    /// </summary>
    /// <remarks>
    /// Real, as of Phase 4 of docs/gdi-gap-plan.md: <see cref="Image.SelectActiveFrame"/> decodes frames
    /// through <c>SKCodec</c>, so an animated GIF genuinely advances. Frames advance when the caller
    /// calls <see cref="UpdateFrames()"/> — the same pull model System.Drawing uses, where a control's
    /// paint loop drives the animation — rather than from a background timer. Per-frame delays stored in
    /// the file are not honored; each <c>UpdateFrames</c> advances exactly one frame.
    /// </remarks>
    public static class ImageAnimator
    {
        private sealed class AnimationState
        {
            public int FrameIndex;
            public readonly List<EventHandler> Handlers = [];
        }

        private static readonly Dictionary<Image, AnimationState> Animated = [];
        private static readonly object Gate = new ();

        /// <summary>Returns whether the image has more than one frame along the time dimension.</summary>
        public static bool CanAnimate (Image? image)
            => image is not null && image.GetFrameCount (FrameDimension.Time) > 1;

        /// <summary>
        /// Begins animating the image, invoking <paramref name="onFrameChangedHandler"/> after each
        /// advance. Has no effect on a single-frame image.
        /// </summary>
        public static void Animate (Image image, EventHandler onFrameChangedHandler)
        {
            if (!CanAnimate (image))
                return;

            lock (Gate) {
                if (!Animated.TryGetValue (image, out var state))
                    Animated[image] = state = new AnimationState ();
                if (onFrameChangedHandler is not null && !state.Handlers.Contains (onFrameChangedHandler))
                    state.Handlers.Add (onFrameChangedHandler);
            }
        }

        /// <summary>Stops animating the image for the specified handler.</summary>
        public static void StopAnimate (Image image, EventHandler onFrameChangedHandler)
        {
            if (image is null)
                return;

            lock (Gate) {
                if (!Animated.TryGetValue (image, out var state))
                    return;
                state.Handlers.Remove (onFrameChangedHandler);
                if (state.Handlers.Count == 0)
                    Animated.Remove (image);
            }
        }

        /// <summary>Advances every animating image by one frame.</summary>
        public static void UpdateFrames ()
        {
            KeyValuePair<Image, AnimationState>[] snapshot;
            lock (Gate)
                snapshot = [.. Animated];

            foreach (var entry in snapshot)
                Advance (entry.Key, entry.Value);
        }

        /// <summary>Advances the specified image by one frame.</summary>
        public static void UpdateFrames (Image image)
        {
            if (image is null)
                return;

            AnimationState? state;
            lock (Gate)
                Animated.TryGetValue (image, out state);

            if (state is not null)
                Advance (image, state);
        }

        private static void Advance (Image image, AnimationState state)
        {
            var count = image.GetFrameCount (FrameDimension.Time);
            if (count <= 1)
                return;

            state.FrameIndex = (state.FrameIndex + 1) % count;
            image.SelectActiveFrame (FrameDimension.Time, state.FrameIndex);

            // Snapshot the handlers: one of them calling StopAnimate must not mutate the list mid-loop.
            foreach (var handler in state.Handlers.ToArray ())
                handler (image, EventArgs.Empty);
        }
    }
}
