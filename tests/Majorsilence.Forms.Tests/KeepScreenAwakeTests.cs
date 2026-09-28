using System;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Application.KeepScreenAwake (register item F12): unlike Haptics/LocalNotifications, Headless implements
// IKeepScreenAwakeBackend for real (a simple settable field, the same shape HeadlessPlatformBackend.PrefersReducedMotion
// already uses) rather than answering unsupported -- a bedside/status-display app's own view-model code is
// exactly what a fake here needs to let a test assert against, the same reasoning ReducedMotionTests documents.
[Collection ("Headless")]
public class KeepScreenAwakeTests
{
    [Fact]
    public void Is_false_by_default_on_Headless ()
    {
        HeadlessRenderer.Use ();

        Assert.False (Application.KeepScreenAwake);
    }

    [Fact]
    public void Reflects_the_Headless_backends_value ()
    {
        HeadlessRenderer.Use ();

        Application.KeepScreenAwake = true;
        Assert.True (Application.KeepScreenAwake);

        Application.KeepScreenAwake = false;
        Assert.False (Application.KeepScreenAwake);
    }

    [Fact]
    public void A_backend_that_is_not_IKeepScreenAwakeBackend_answers_false_not_throw ()
    {
        // A third-party backend that has never heard of IKeepScreenAwakeBackend should not have to: this
        // degrades to the usual conservative false instead of throwing an InvalidCastException, the same
        // reasoning ReducedMotionTests.NotReducedMotionAware documents for its own capability.
        var previous = Platform.ConfiguredBackend;
        Platform.Backend = new NotKeepScreenAwakeAware ();
        try {
            Assert.False (Application.KeepScreenAwake);
            // And setting it does nothing harmful.
            Application.KeepScreenAwake = true;
            Assert.False (Application.KeepScreenAwake);
        } finally {
            if (previous is not null)
                Platform.Backend = previous;
            else
                HeadlessRenderer.Use ();
        }
    }

    // A minimal IPlatformBackend that is deliberately not IKeepScreenAwakeBackend, standing in for a
    // third-party backend. Nothing here needs to actually work: the test above never calls any of it.
    private sealed class NotKeepScreenAwakeAware : IPlatformBackend
    {
        public string Name => "not keep-screen-awake aware";
        public void Initialize () => throw new NotImplementedException ();
        public void RunMainLoop (CancellationToken token) => throw new NotImplementedException ();
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
        public void RunModalLoop (Task completed) => throw new NotImplementedException ();
    }

    // ---- DesktopKeepAwake.Dispatch: which OS setter runs, with the OS itself faked -------------------

    [Fact]
    public void Dispatch_asks_only_the_matching_OSs_setter ()
    {
        var windowsCalls = 0; var macCalls = 0; var linuxCalls = 0;

        DesktopKeepAwake.Dispatch (
            () => false, () => true, () => throw new InvalidOperationException ("isLinux must not run once isMacOS matched"),
            () => windowsCalls++,
            () => macCalls++,
            () => linuxCalls++);

        Assert.Equal (0, windowsCalls);
        Assert.Equal (1, macCalls);
        Assert.Equal (0, linuxCalls);
    }

    [Fact]
    public void Dispatch_tries_Windows_then_macOS_then_Linux_in_order ()
    {
        var order = new System.Collections.Generic.List<string> ();

        DesktopKeepAwake.Dispatch (
            () => { order.Add ("windows"); return false; },
            () => { order.Add ("macos"); return false; },
            () => { order.Add ("linux"); return true; },
            () => { }, () => { }, () => { });

        Assert.Equal (new[] { "windows", "macos", "linux" }, order);
    }

    [Fact]
    public void Dispatch_does_nothing_when_no_predicate_matches ()
    {
        var exception = Record.Exception (() => DesktopKeepAwake.Dispatch (() => false, () => false, () => false,
            () => throw new InvalidOperationException (), () => throw new InvalidOperationException (), () => throw new InvalidOperationException ()));

        Assert.Null (exception);
    }

    [Fact]
    public void Dispatch_swallows_the_matched_setters_exception ()
    {
        var exception = Record.Exception (() => DesktopKeepAwake.Dispatch (() => false, () => false, () => true,
            () => throw new InvalidOperationException (), () => throw new InvalidOperationException (), () => throw new InvalidOperationException ("boom")));

        Assert.Null (exception);
    }

    [Fact]
    public void The_real_desktop_set_never_throws_and_toggles_IsEnabled_regardless_of_host ()
    {
        // Exercises whichever OS branch this machine actually is (Linux in this repository's own CI), and
        // proves the other two branches' P/Invoke declarations at least load and bind correctly wherever
        // this assembly runs, since Set() is what every path funnels through -- the same reasoning
        // ReducedMotionTests.The_real_desktop_read_never_throws_regardless_of_host documents for reading.
        // Restores the prior state in finally: this is real, process-wide state (a real Linux run holds a
        // real systemd-inhibit child process while enabled), not something a test may leak past itself.
        // The finally forces a real Set (false) unconditionally, rather than trusting IsEnabled's own
        // tracked value to short-circuit it -- an assertion failing between the enable and disable above
        // would otherwise leave a real child process behind with IsEnabled never having reached true.
        var previous = DesktopKeepAwake.IsEnabled;
        try {
            var enableException = Record.Exception (() => DesktopKeepAwake.Set (true));
            Assert.Null (enableException);
            Assert.True (DesktopKeepAwake.IsEnabled);

            var disableException = Record.Exception (() => DesktopKeepAwake.Set (false));
            Assert.Null (disableException);
            Assert.False (DesktopKeepAwake.IsEnabled);
        } finally {
            DesktopKeepAwake.Set (true);
            DesktopKeepAwake.Set (false);
            if (previous)
                DesktopKeepAwake.Set (true);
        }
    }
}
