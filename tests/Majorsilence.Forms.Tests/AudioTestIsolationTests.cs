using System;
using System.Linq;
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
    public void A_late_write_from_an_earlier_test_is_waited_out_not_counted ()
    {
        // The other half of #379. SoundPlayer.PlayLooping respawns on a background task and Stop () only asks it to end, so a test that
        // started a loop can still be writing to the backend's request queue when the next test has already cleared it. The audio tests
        // clear the queue by waiting for a quiet moment rather than once.
        HeadlessRenderer.Use ();
        var backend = (IAudioBackend) Platform.Backend;
        var writing = true;
        var writer = new Thread (() => {
            while (Volatile.Read (ref writing)) {
                backend.PlayFile ("late-write.wav", loop: true);
                Thread.Sleep (5);     // a loop respawning with a real pass between starts, not a hot spin
            }
        });
        writer.Start ();

        try {
            // The "next test" begins while that loop is still going, and the loop is told to stop a moment later, as Stop () would.
            var stopper = new Thread (() => { Thread.Sleep (150); Volatile.Write (ref writing, false); });
            stopper.Start ();

            Thread.Sleep (20);      // the loop is well under way
            AudioTestSupport.WaitForQuiet ();
            Thread.Sleep (50);      // the test body runs, then asserts

            Assert.Empty (HeadlessRenderer.AudioRequests);
            stopper.Join ();
        } finally {
            Volatile.Write (ref writing, false);
            writer.Join ();
            HeadlessRenderer.ClearAudioRequests ();
        }
    }
}
