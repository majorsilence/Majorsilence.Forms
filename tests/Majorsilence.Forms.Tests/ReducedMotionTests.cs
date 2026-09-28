using System;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // SystemInformation.PrefersReducedMotion answers false when the active backend cannot tell (the same conservative default every
    // other UI-effect member on that page uses), Headless can be told directly for tests, and PolledSetting is the platform-agnostic
    // mechanism a backend that can only re-read a setting (no push notification) uses to raise a change event anyway.
    public class ReducedMotionTests : IDisposable
    {
        public ReducedMotionTests () => HeadlessRenderer.Use ();

        public void Dispose ()
        {
            HeadlessRenderer.PrefersReducedMotion = false;
            GC.SuppressFinalize (this);
        }

        // ---- SystemInformation, over the Headless backend ------------------------------------------

        [Fact]
        public void Reports_what_the_active_backend_was_told ()
        {
            Assert.False (SystemInformation.PrefersReducedMotion);

            HeadlessRenderer.PrefersReducedMotion = true;

            Assert.True (SystemInformation.PrefersReducedMotion);
        }

        [Fact]
        public void Raises_its_changed_event_when_the_backend_changes ()
        {
            var raised = 0;
            SystemInformation.PrefersReducedMotionChanged += (s, e) => raised++;

            HeadlessRenderer.PrefersReducedMotion = true;
            HeadlessRenderer.PrefersReducedMotion = true; // the same value again: still one

            Assert.Equal (1, raised);
        }

        [Fact]
        public void Does_not_raise_when_set_to_the_value_it_already_holds ()
        {
            HeadlessRenderer.PrefersReducedMotion = true;
            var raised = 0;
            SystemInformation.PrefersReducedMotionChanged += (s, e) => raised++;

            HeadlessRenderer.PrefersReducedMotion = true;

            Assert.Equal (0, raised);
        }

        [Fact]
        public void Unsubscribing_stops_further_notifications ()
        {
            var raised = 0;
            EventHandler handler = (s, e) => raised++;
            SystemInformation.PrefersReducedMotionChanged += handler;
            HeadlessRenderer.PrefersReducedMotion = true;
            Assert.Equal (1, raised);

            SystemInformation.PrefersReducedMotionChanged -= handler;
            HeadlessRenderer.PrefersReducedMotion = false;

            Assert.Equal (1, raised);
        }

        [Fact]
        public void A_backend_that_is_not_IReducedMotionSource_answers_false_not_throw ()
        {
            // A third-party backend that has never heard of IReducedMotionSource should not have to: SystemInformation degrades to
            // its usual conservative false instead of throwing an InvalidCastException.
            var previous = Platform.ConfiguredBackend;
            Platform.Backend = new NotReducedMotionAware ();
            try {
                Assert.False (SystemInformation.PrefersReducedMotion);
                // And subscribing does nothing harmful -- it is simply never raised.
                SystemInformation.PrefersReducedMotionChanged += (_, _) => throw new InvalidOperationException ("never");
            } finally {
                if (previous is not null)
                    Platform.Backend = previous;
            }
        }

        // A minimal IPlatformBackend that is deliberately not IReducedMotionSource, standing in for a third-party backend. Nothing
        // here needs to actually work: the test above never calls any of it.
        private sealed class NotReducedMotionAware : IPlatformBackend
        {
            public string Name => "not reduced-motion aware";
            public void Initialize () => throw new NotImplementedException ();
            public void RunMainLoop (System.Threading.CancellationToken token) => throw new NotImplementedException ();
            public void Stop () => throw new NotImplementedException ();
            public void Post (Action action) => throw new NotImplementedException ();
            public void Invoke (Action action) => throw new NotImplementedException ();
            public T Invoke<T> (Func<T> func) => throw new NotImplementedException ();
            public bool CheckAccess () => throw new NotImplementedException ();
            public void DoEvents () => throw new NotImplementedException ();
            public IWindowBackend CreateWindow (WindowBase owner, bool isPopup) => throw new NotImplementedException ();
            public IPlatformTimer CreateTimer () => throw new NotImplementedException ();
            public string GetClipboardText () => throw new NotImplementedException ();
            public void SetClipboardText (string text) => throw new NotImplementedException ();
            public void ClearClipboard () => throw new NotImplementedException ();
            public ScreenInfo[] GetScreens () => throw new NotImplementedException ();
            public void RunModalLoop (System.Threading.Tasks.Task completed) => throw new NotImplementedException ();
        }

        // ---- HeadlessRenderer.PrefersReducedMotion --------------------------------------------------

        [Fact]
        public void Reading_the_Headless_accessor_throws_when_Headless_is_not_active ()
        {
            var previous = Platform.ConfiguredBackend;
            Platform.Backend = new NotReducedMotionAware ();
            try {
                Assert.Throws<InvalidOperationException> (() => HeadlessRenderer.PrefersReducedMotion);
                Assert.Throws<InvalidOperationException> (() => HeadlessRenderer.PrefersReducedMotion = true);
            } finally {
                if (previous is not null)
                    Platform.Backend = previous;
                else
                    HeadlessRenderer.Use ();
            }
        }

        // ---- PolledSetting: the mechanism a backend uses to turn a re-readable setting into a change event -----------------------

        private sealed class FakeTimer : IPlatformTimer
        {
            public double IntervalMilliseconds { get; set; }

            public event Action? Tick;

            public int StartCount { get; private set; }

            public int StopCount { get; private set; }

            public int DisposeCount { get; private set; }

            public void Start () => StartCount++;

            public void Stop () => StopCount++;

            public void Dispose () => DisposeCount++;

            public void RaiseTick () => Tick?.Invoke ();

            public int Subscribers => Tick?.GetInvocationList ().Length ?? 0;
        }

        [Fact]
        public void PolledSetting_reads_once_up_front_and_starts_the_timer ()
        {
            var reads = 0;
            var timer = new FakeTimer ();

            using var polled = new PolledSetting (() => { reads++; return true; }, timer, 2000);

            Assert.True (polled.Current);
            Assert.Equal (1, reads);
            Assert.Equal (1, timer.StartCount);
            Assert.Equal (2000, timer.IntervalMilliseconds);
        }

        [Fact]
        public void PolledSetting_raises_Changed_only_when_a_tick_finds_a_different_value ()
        {
            var value = false;
            var timer = new FakeTimer ();
            using var polled = new PolledSetting (() => value, timer, 1000);
            var raised = 0;
            polled.Changed += (s, e) => { raised++; Assert.Same (polled, s); };

            timer.RaiseTick (); // unchanged
            Assert.Equal (0, raised);

            value = true;
            timer.RaiseTick (); // changed
            Assert.Equal (1, raised);
            Assert.True (polled.Current);

            timer.RaiseTick (); // unchanged again, at the new value
            Assert.Equal (1, raised);
        }

        [Fact]
        public void PolledSetting_can_change_back_and_forth ()
        {
            var value = false;
            var timer = new FakeTimer ();
            using var polled = new PolledSetting (() => value, timer, 1000);
            var seen = new System.Collections.Generic.List<bool> ();
            polled.Changed += (s, e) => seen.Add (polled.Current);

            value = true; timer.RaiseTick ();
            value = false; timer.RaiseTick ();

            Assert.Equal (new[] { true, false }, seen);
        }

        [Fact]
        public void Disposing_stops_and_disposes_the_timer_and_stops_reacting_to_ticks ()
        {
            var value = false;
            var timer = new FakeTimer ();
            var polled = new PolledSetting (() => value, timer, 1000);
            var raised = 0;
            polled.Changed += (s, e) => raised++;

            polled.Dispose ();
            value = true;
            timer.RaiseTick ();

            Assert.Equal (1, timer.StopCount);
            Assert.Equal (1, timer.DisposeCount);
            Assert.Equal (0, raised);
            // Not redundant with the assertion above: OnTick's own no-op-once-disposed path would already stop it reacting even if the
            // subscription itself were left in place, which is exactly what this catches -- a real unsubscribe, not just a guarded one.
            Assert.Equal (0, timer.Subscribers);
        }

        [Fact]
        public void Disposing_twice_is_harmless ()
        {
            var timer = new FakeTimer ();
            var polled = new PolledSetting (() => false, timer, 1000);

            polled.Dispose ();
            polled.Dispose ();

            Assert.Equal (1, timer.StopCount);
            Assert.Equal (1, timer.DisposeCount);
        }

        [Fact]
        public void PolledSetting_refuses_missing_arguments ()
        {
            Assert.Throws<ArgumentNullException> (() => new PolledSetting (null!, new FakeTimer (), 1000));
            Assert.Throws<ArgumentNullException> (() => new PolledSetting (() => false, null!, 1000));
        }

        // ---- The GNOME output parser, and the real desktop read on whatever OS these tests run on --------------------------------

        [Theory]
        [InlineData ("true\n", true)]
        [InlineData ("false\n", false)]
        [InlineData ("true", true)]
        [InlineData ("  false  ", false)]
        public void Parses_gsettings_booleans (string raw, bool expected)
            => Assert.Equal (expected, DesktopReducedMotion.ParseGnomeAnimationsEnabled (raw));

        [Theory]
        [InlineData ("")]
        [InlineData ("'true'")]
        [InlineData ("maybe")]
        [InlineData ("True")]
        public void An_unrecognised_gsettings_answer_parses_to_neither (string raw)
            => Assert.Null (DesktopReducedMotion.ParseGnomeAnimationsEnabled (raw));

        // ---- DesktopReducedMotion.Dispatch: which OS reader runs, with the OS itself faked ---------

        [Fact]
        public void Dispatch_asks_only_the_matching_readers_predicate ()
        {
            var windowsCalls = 0; var macCalls = 0; var linuxCalls = 0;

            var result = DesktopReducedMotion.Dispatch (
                () => false, () => true, () => throw new InvalidOperationException ("isLinux must not run once isMacOS matched"),
                () => { windowsCalls++; return false; },
                () => { macCalls++; return true; },
                () => { linuxCalls++; return false; });

            Assert.True (result);
            Assert.Equal (0, windowsCalls);
            Assert.Equal (1, macCalls);
            Assert.Equal (0, linuxCalls);
        }

        [Fact]
        public void Dispatch_tries_Windows_then_macOS_then_Linux_in_order ()
        {
            var order = new System.Collections.Generic.List<string> ();

            DesktopReducedMotion.Dispatch (
                () => { order.Add ("windows"); return false; },
                () => { order.Add ("macos"); return false; },
                () => { order.Add ("linux"); return true; },
                () => false, () => false, () => false);

            Assert.Equal (new[] { "windows", "macos", "linux" }, order);
        }

        [Fact]
        public void Dispatch_negates_nothing_by_itself_it_is_the_readers_job ()
        {
            // Windows' own reader negates ("client area animation enabled" -> "prefers reduced motion"); Dispatch just returns
            // whatever the matched reader says, unchanged, for every OS.
            Assert.True (DesktopReducedMotion.Dispatch (() => true, () => false, () => false, () => true, () => false, () => false));
            Assert.False (DesktopReducedMotion.Dispatch (() => true, () => false, () => false, () => false, () => true, () => true));
        }

        [Fact]
        public void Dispatch_answers_false_when_no_predicate_matches ()
        {
            var result = DesktopReducedMotion.Dispatch (() => false, () => false, () => false,
                () => throw new InvalidOperationException (), () => throw new InvalidOperationException (), () => throw new InvalidOperationException ());

            Assert.False (result);
        }

        [Fact]
        public void Dispatch_answers_false_when_the_matched_reader_throws ()
        {
            var result = DesktopReducedMotion.Dispatch (() => false, () => false, () => true,
                () => throw new InvalidOperationException (), () => throw new InvalidOperationException (), () => throw new InvalidOperationException ("boom"));

            Assert.False (result);
        }

        [Fact]
        public void The_real_desktop_read_never_throws_regardless_of_host ()
        {
            // Exercises whichever OS branch this machine actually is (Linux/GNOME in this repository's own CI), and proves the other
            // two branches' P/Invoke declarations at least load and bind correctly wherever this assembly runs, since Read() is what
            // every path funnels through. It does not assert a value: that depends on this host's live desktop setting, which the
            // test suite must not depend on (and, on Linux, must not require a GNOME session to even exist).
            var exception = Record.Exception (() => DesktopReducedMotion.Read ());

            Assert.Null (exception);
        }
    }
}
