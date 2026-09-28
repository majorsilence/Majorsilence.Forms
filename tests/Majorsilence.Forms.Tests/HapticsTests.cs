using System;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// Haptics (register item F13): a reported no-op everywhere but Android and iOS, which have no fake to
// exercise here -- Platform.Backend is swapped for a minimal recording fake instead, the same technique
// ReducedMotionTests.NotReducedMotionAware uses to test the "backend does not implement this" path.
[Collection ("Headless")]
public class HapticsTests
{
    [Fact]
    public void IsSupported_is_false_on_Headless ()
    {
        HeadlessRenderer.Use ();

        Assert.False (Haptics.IsSupported);
    }

    [Fact]
    public void Every_member_is_a_safe_no_op_on_Headless ()
    {
        HeadlessRenderer.Use ();

        Haptics.Tap ();
        Haptics.Impact ();
        Haptics.Vibrate (TimeSpan.FromMilliseconds (200));
        Haptics.Vibrate (TimeSpan.FromMilliseconds (-1));   // clamped, not rejected
    }

    [Fact]
    public void IsSupported_is_true_when_the_backend_implements_IHapticsBackend ()
    {
        var previous = Platform.ConfiguredBackend;
        Platform.Backend = new RecordingHaptics ();
        try {
            Assert.True (Haptics.IsSupported);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Tap_reaches_the_backend ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingHaptics ();
        Platform.Backend = backend;
        try {
            Haptics.Tap ();
            Assert.Equal (1, backend.Taps);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Impact_reaches_the_backend ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingHaptics ();
        Platform.Backend = backend;
        try {
            Haptics.Impact ();
            Assert.Equal (1, backend.Impacts);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Vibrate_reaches_the_backend_with_the_given_duration ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingHaptics ();
        Platform.Backend = backend;
        try {
            Haptics.Vibrate (TimeSpan.FromMilliseconds (250));
            Assert.Equal (TimeSpan.FromMilliseconds (250), backend.LastVibrate);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Vibrate_clamps_a_negative_duration_to_zero_before_reaching_the_backend ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingHaptics ();
        Platform.Backend = backend;
        try {
            Haptics.Vibrate (TimeSpan.FromMilliseconds (-50));
            Assert.Equal (TimeSpan.Zero, backend.LastVibrate);
        } finally {
            RestoreBackend (previous);
        }
    }

    private static void RestoreBackend (IPlatformBackend? previous)
    {
        if (previous is not null)
            Platform.Backend = previous;
        else
            HeadlessRenderer.Use ();
    }

    // A minimal IPlatformBackend + IHapticsBackend fake, standing in for Android/iOS: nothing but the
    // haptics members needs to actually work, the rest matches NotReducedMotionAware's own "never called"
    // contract.
    private sealed class RecordingHaptics : IPlatformBackend, IHapticsBackend
    {
        public int Taps;
        public int Impacts;
        public TimeSpan? LastVibrate;

        public void Tap () => Taps++;
        public void Impact () => Impacts++;
        public void Vibrate (TimeSpan duration) => LastVibrate = duration;

        public string Name => "recording haptics";
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
}
