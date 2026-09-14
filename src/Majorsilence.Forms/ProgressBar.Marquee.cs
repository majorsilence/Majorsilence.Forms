using System;

namespace Majorsilence.Forms
{
    // SMP-26: the three ProgressBarStyles, of which the renderer drew exactly one shape.
    //
    // ProgressBarRenderer computed a percentage from Value and filled one rectangle, never reading
    // Style. So the standard "indeterminate busy" bar -- Style = Marquee with Value left at 0, the most
    // common non-default configuration in a line-of-business app -- rendered as a permanently EMPTY
    // bar. The app looks hung at precisely the moment it is trying to say it is working.
    //
    // MarqueeAnimationSpeed was stored and doc-commented "Stub", and there was no timer anywhere.
    public partial class ProgressBar
    {
        private Timer? marquee_timer;
        private int marquee_phase;

        /// <summary>
        /// How far along its travel the marquee block currently is, as a fraction from 0 to 1. The
        /// renderer's only input for <see cref="ProgressBarStyle.Marquee"/>, which is independent of
        /// <see cref="Value"/> -- that is what "indeterminate" means.
        /// </summary>
        internal float MarqueePosition => MarqueeSteps == 0 ? 0f : (float)marquee_phase / MarqueeSteps;

        // Travel is quantised so the block advances in visible steps rather than by a sub-pixel amount
        // per tick, and so a test can advance it deterministically.
        internal const int MarqueeSteps = 20;

        /// <summary>Advances the marquee by one step, as its timer does. Wraps at the end of the travel.</summary>
        internal void AdvanceMarquee ()
        {
            marquee_phase = (marquee_phase + 1) % MarqueeSteps;
            Invalidate ();
        }

        // Started and stopped from one place so every route into Marquee (the Style setter, the speed
        // setter, becoming visible) agrees about whether the timer should be running.
        private void UpdateMarqueeTimer ()
        {
            var should_run = Style == ProgressBarStyle.Marquee && MarqueeAnimationSpeed > 0 && Visible && Enabled;

            if (!should_run) {
                if (marquee_timer is not null) {
                    marquee_timer.Stop ();
                    marquee_timer.Dispose ();
                    marquee_timer = null;
                }

                // The phase is only where the block currently is. Resetting it here would restart the
                // animation from the left edge every time the bar is hidden and shown again -- and,
                // less obviously, whenever the control is re-parented.
                return;
            }

            marquee_timer ??= CreateMarqueeTimer ();
            marquee_timer.Interval = MarqueeAnimationSpeed;

            if (!marquee_timer.Enabled)
                marquee_timer.Start ();
        }

        private Timer CreateMarqueeTimer ()
        {
            var timer = new Timer { Interval = MarqueeAnimationSpeed };

            timer.Tick += (_, _) => AdvanceMarquee ();

            return timer;
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged (EventArgs e)
        {
            base.OnVisibleChanged (e);

            // A hidden bar animating in the background is a timer tick and an invalidate per 100ms for
            // nothing -- on a wizard page that is off screen, forever.
            UpdateMarqueeTimer ();
        }

        /// <inheritdoc/>
        protected override void OnEnabledChanged (EventArgs e)
        {
            base.OnEnabledChanged (e);

            UpdateMarqueeTimer ();
        }

        /// <inheritdoc/>
        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                marquee_timer?.Stop ();
                marquee_timer?.Dispose ();
                marquee_timer = null;
            }

            base.Dispose (disposing);
        }
    }
}
