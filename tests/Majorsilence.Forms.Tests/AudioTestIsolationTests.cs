using System;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Xunit;

namespace Majorsilence.Forms.Tests;

// #379: MobileAudioTests failed now and then, a different test each time, passing alone. Everything that plays a sound reaches the shared Headless
// backend's process-wide request queue, which those tests assert on, so any test class that plays sounds has to run in the same serialized collection;
// NativeAudioTests had none and ran in parallel with them.
[Collection ("Headless")]
public class AudioTestIsolationTests
{
    private static readonly string[] AudioTestClasses = ["MobileAudioTests", "NativeAudioTests", "AudioPlayerTests", "W6KeysHelpSoundAndAccessibilityTests"];

    [Fact]
    public void Every_class_that_plays_sounds_shares_the_Headless_collection ()
    {
        var assembly = typeof (AudioTestIsolationTests).Assembly;
        var classes = assembly.GetTypes ().Where (t => AudioTestClasses.Contains (t.Name)).ToList ();

        Assert.Equal (AudioTestClasses.Length, classes.Count);

        var outside = classes
            .Where (t => t.GetCustomAttribute<CollectionAttribute> ()?.Name != "Headless")
            .Select (t => t.Name)
            .ToList ();

        Assert.True (outside.Count == 0, $"these play sounds outside the Headless collection: {string.Join (", ", outside)}");
    }

    [Fact]
    public void WaitForQuiet_returns_only_after_a_quiet_window ()
    {
        // The other half of #379. SoundPlayer.PlayLooping respawns on a background task and Stop () only asks it to end, so a test that
        // started a loop can still be writing to the backend's request queue when the next test has already cleared it. The audio tests
        // clear the queue by waiting for a quiet moment rather than once. The contract, checked against the writer's own clock so that a
        // slow or stalled runner cannot make it flaky: when WaitForQuiet returns, nothing has been written for at least the quiet window.
        HeadlessRenderer.Use ();
        var backend = (IAudioBackend) Platform.Backend;
        var writing = true;
        var lastWrite = Stopwatch.GetTimestamp ();
        var writer = new Thread (() => {
            while (Volatile.Read (ref writing)) {
                backend.PlayFile ("late-write.wav", loop: true);
                Interlocked.Exchange (ref lastWrite, Stopwatch.GetTimestamp ());
                Thread.Sleep (2);     // a loop respawning with a real pass between starts, not a hot spin
            }
        });
        writer.Start ();

        try {
            Thread.Sleep (20);        // the loop is well under way
            var stopper = new Thread (() => { Thread.Sleep (150); Volatile.Write (ref writing, false); });
            stopper.Start ();

            AudioTestSupport.WaitForQuiet ();

            var silent = Stopwatch.GetElapsedTime (Interlocked.Read (ref lastWrite));
            Assert.True (silent >= TimeSpan.FromMilliseconds (25), $"only {silent.TotalMilliseconds:0} ms since the last write when WaitForQuiet returned");
            stopper.Join ();
        } finally {
            Volatile.Write (ref writing, false);
            writer.Join ();
            HeadlessRenderer.ClearAudioRequests ();
        }
    }
}
