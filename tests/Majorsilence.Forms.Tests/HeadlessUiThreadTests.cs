using System.Threading;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The headless backend names the first thread that initialised it as the UI thread, and every test after that runs on some other thread. A
    // thread-pool thread of a later test could then BE that stale "UI thread", so work marshalled to the UI thread (a PictureBox's background
    // load completion) ran inline on the pool thread, racing the test that expected it queued: W6SweepTests.WaitOnLoad_decides_... failed on
    // macOS with the loaded image where the placeholder was expected. Use () is called at the start of every test, on that test's thread, so
    // it is that thread that is the UI thread.
    [Collection ("Headless")]
    public class HeadlessUiThreadTests
    {
        private static void OnAnotherThread (System.Action action)
        {
            var thread = new Thread (() => action ());
            thread.Start ();
            thread.Join ();
        }

        [Fact]
        public void Use_makes_the_calling_thread_the_UI_thread ()
        {
            var saved = Platform.ConfiguredBackend;

            try {
                // A backend some earlier test's thread initialised: that thread, not this one, is the UI thread.
                var backend = new HeadlessPlatformBackend ();
                OnAnotherThread (backend.Initialize);
                Platform.Backend = backend;
                Assert.False (backend.CheckAccess ());

                HeadlessRenderer.Use ();

                Assert.True (Platform.Backend.CheckAccess ());
                var otherSees = true;
                OnAnotherThread (() => otherSees = Platform.Backend.CheckAccess ());
                Assert.False (otherSees);     // another thread is not the UI thread
            } finally {
                if (saved is not null)
                    Platform.Backend = saved;
            }
        }
    }
}
