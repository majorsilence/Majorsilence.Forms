using System;
using System.Threading;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // Constructing a window creates a native one, and on macOS AppKit refuses to build an NSWindow
    // anywhere but the main thread: NSInternalInconsistencyException, unwound through C++ with no
    // handler, and the process is killed by abort(). Nothing catchable -- the whole process goes.
    //
    // The path into it is ordinary. An exception escaping on a pool, timer or finalizer thread raises
    // AppDomain.UnhandledException on THAT thread, and a handler that reports the failure by showing a
    // form lands straight here. That is how it was found, on the finalizer thread, which is the worst
    // version: the process dies while reporting a fault, so the original exception is never seen.
    [Collection ("Headless")]
    public class WindowCreationThreadTests
    {
        [Fact]
        public void A_window_constructed_off_the_ui_thread_is_still_created_on_it ()
        {
            var previous = Platform.ConfiguredBackend;
            var backend = new HeadlessPlatformBackend ();
            Platform.Backend = backend;

            using var cts = new CancellationTokenSource ();
            var pinned = new ManualResetEventSlim (false);
            var uiThreadId = 0;

            // A pump on another thread, the way an application's Application.Run pins the UI thread for
            // everything created afterwards. It has to be a REAL pump: Invoke blocks until the posted
            // work runs, so without something draining the queue this deadlocks rather than fails.
            var pump = new Thread (() => {
                backend.Initialize ();
                uiThreadId = Environment.CurrentManagedThreadId;
                pinned.Set ();
                backend.RunMainLoop (cts.Token);
            }) { IsBackground = true };

            try {
                pump.Start ();
                Assert.True (pinned.Wait (TimeSpan.FromSeconds (5)), "the pump thread never started");

                Application.RegisterMessageLoop (() => true);

                // The preconditions the fix turns on; asserted so a future change that quietly makes
                // either false cannot leave this passing for the wrong reason.
                Assert.False (backend.CheckAccess (), "this test must not be on the backend's UI thread");
                Assert.True (Application.HasMessageLoop, "a loop must be running or Invoke cannot be used");

                using var form = new Form ();

                var host = (HeadlessWindowHost) form.Backend;
                Assert.Equal (uiThreadId, host.CreatedOnThreadId);
                Assert.NotEqual (Environment.CurrentManagedThreadId, host.CreatedOnThreadId);
            } finally {
                Application.UnregisterMessageLoop ();
                cts.Cancel ();
                pump.Join (TimeSpan.FromSeconds (5));
                if (previous is not null)
                    Platform.Backend = previous;
            }
        }

        // The other half of the guard, and the reason it is not simply "always Invoke": posting only
        // enqueues, and with no loop running nothing would ever drain it, so Invoke would wait forever.
        // Creating on the calling thread is also correct there -- with no loop yet, this thread is the
        // one about to become the UI thread.
        [Fact]
        public void With_no_message_loop_the_window_is_created_on_the_calling_thread ()
        {
            var previous = Platform.ConfiguredBackend;
            var backend = new HeadlessPlatformBackend ();
            Platform.Backend = backend;

            try {
                var pin = new Thread (backend.Initialize);
                pin.Start ();
                pin.Join ();

                Assert.False (backend.CheckAccess ());
                Assert.False (Application.HasMessageLoop);

                using var form = new Form ();

                var host = (HeadlessWindowHost) form.Backend;
                Assert.Equal (Environment.CurrentManagedThreadId, host.CreatedOnThreadId);
            } finally {
                if (previous is not null)
                    Platform.Backend = previous;
            }
        }
    }
}
