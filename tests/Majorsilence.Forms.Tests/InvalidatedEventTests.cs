using System.Drawing;
using System.Threading;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // WinForms parity: Control.Invalidated is InvalidateEventHandler(object, InvalidateEventArgs) exposing
    // e.InvalidRect -- migrated handlers depend on that shape. The fork previously typed it as
    // EventHandler<EventArgs<Rectangle>>.
    public class InvalidatedEventTests
    {
        [Fact]
        public void Invalidated_passes_InvalidRect_via_InvalidateEventArgs ()
        {
            using var control = new Panel ();
            control.CreateControl (); // Invalidate is a no-op before the handle is created.

            Rectangle? got = null;
            control.Invalidated += (s, e) => got = e.InvalidRect;

            var rect = new Rectangle (1, 2, 3, 4);
            control.Invalidate (rect);

            Assert.Equal (rect, got);
        }

        [Fact]
        public void BecomingVisible_invalidates_so_the_revealed_surface_repaints ()
        {
            // Regression: found running ReportDesigner. Switching to a tab showed nothing until an
            // unrelated event forced a paint -- SetVisibleCore laid the newly-revealed subtree out
            // but never invalidated it. WinForms shows the window handle, which paints it; there is
            // no handle here, so it has to invalidate explicitly.
            using var parent = new Panel { Size = new Size (200, 200) };
            var child = new Panel { Size = new Size (100, 100), Visible = false };
            parent.Controls.Add (child);
            parent.CreateControl ();

            var invalidations = 0;
            child.Invalidated += (_, _) => invalidations++;

            child.Visible = true;

            Assert.True (invalidations > 0, "a control made visible must invalidate to repaint");
        }

        [Fact]
        public void Invalidate_repaints_when_no_message_loop_would_ever_drain_a_posted_one ()
        {
            // Regression, found as a red CI gate: a theme change repainted nothing at all
            // (HostedSurfaceTests reported light=0, dark=0 -- the surface never re-rendered).
            //
            // Invalidate marshals to the backend's UI thread, and that thread is pinned by whichever
            // thread constructs the first window in the process (WindowBase's constructor calls
            // Platform.Backend.Initialize). Invalidating from any other thread therefore took the Post
            // branch -- and Post only enqueues. With no Application.Run, nothing drains that queue, so
            // the repaint silently never happened. xUnit hands successive tests different pool threads,
            // which is why this surfaced as an intermittent, platform-specific failure rather than a
            // reproducible one.
            var previous = Platform.ConfiguredBackend;
            var backend = new HeadlessPlatformBackend ();
            Platform.Backend = backend;

            try {
                // Pin the backend's UI thread somewhere this test is not, the way an earlier-created
                // window does for everything that follows it.
                var pin = new Thread (backend.Initialize);
                pin.Start ();
                pin.Join ();

                Assert.False (backend.CheckAccess ());
                Assert.False (Application.HasMessageLoop);

                using var form = new Form ();
                form.Show ();

                var host = (HeadlessWindowHost) form.Backend;
                var before = host.InvalidateCount;

                form.Invalidate ();

                Assert.True (host.InvalidateCount > before,
                    "Invalidate must reach the window backend when no message loop will run a posted one.");
            } finally {
                if (previous is not null)
                    Platform.Backend = previous;
            }
        }
    }
}
