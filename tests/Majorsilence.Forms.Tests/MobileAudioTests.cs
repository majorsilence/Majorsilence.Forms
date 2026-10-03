using System;
using System.IO;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Media;
using Xunit;

namespace Majorsilence.Forms.Tests;

// SoundPlayer and SystemSounds try a backend that implements IAudioBackend (Android's MediaPlayer, iOS's
// AVAudioPlayer -- neither runnable from this Linux test host) before falling back to NativeAudio's
// OS-utility path. HeadlessRenderer.AudioIsSupported flips the Headless backend's own IAudioBackend
// between "answers" and "does not" (false by default -- see the remarks on it), so both branches of that
// routing are provable here without a device.
[Collection ("Headless")]
public class MobileAudioTests : IDisposable
{
    public MobileAudioTests ()
    {
        HeadlessRenderer.Use ();
        HeadlessRenderer.ClearAudioRequests ();
    }

    public void Dispose ()
    {
        HeadlessRenderer.AudioIsSupported = false;
        HeadlessRenderer.ClearAudioRequests ();
        NativeAudio.LauncherOverride = null;
        GC.SuppressFinalize (this);
    }

    private static string WriteTempWav ()
    {
        var path = Path.Combine (Path.GetTempPath (), $"mobile-audio-test-{Guid.NewGuid ():N}.wav");
        File.WriteAllBytes (path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public void SoundPlayer_Play_PrefersTheBackendWhenItAnswers ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var launched = 0;
        NativeAudio.LauncherOverride = _ => { launched++; return null; };

        var wav = WriteTempWav ();
        try {
            using var player = new SoundPlayer (wav);
            player.Play ();

            var request = Assert.Single (HeadlessRenderer.AudioRequests);
            Assert.Equal (wav, request.Value);
            Assert.False (request.Loop);
            Assert.False (request.IsSystemSound);
            Assert.Equal (0, launched);   // NativeAudio never asked: the backend already answered
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SoundPlayer_Play_FallsBackToNativeAudioWhenTheBackendCannot ()
    {
        // AudioIsSupported left false (the default): the same as a backend that never implemented
        // IAudioBackend at all -- SoundPlayer must still reach NativeAudio, exactly as it did before F8.
        var launched = 0;
        NativeAudio.LauncherOverride = _ => { launched++; return null; };

        var wav = WriteTempWav ();
        try {
            using var player = new SoundPlayer (wav);
            player.Play ();

            Assert.Single (HeadlessRenderer.AudioRequests);   // the backend WAS asked first ...
            Assert.Equal (1, launched);                        // ... and, having declined, NativeAudio was tried too
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SoundPlayer_PlayLooping_AsksTheBackendToLoopNativelyInOneCall ()
    {
        HeadlessRenderer.AudioIsSupported = true;

        var wav = WriteTempWav ();
        try {
            using var player = new SoundPlayer (wav);
            player.PlayLooping ();

            var request = Assert.Single (HeadlessRenderer.AudioRequests);
            Assert.True (request.Loop);

            // One call, not a respawn loop: a respawn loop's fake pass returns from Wait() instantly (no
            // real playback to wait for), so if one had been spawned it would have spun many times over in
            // this window. Deliberately BEFORE Stop(): stopping first would cancel a just-spawned respawn
            // task before its first iteration ever ran, hiding exactly the bug this test exists to catch.
            System.Threading.Thread.Sleep (100);
            Assert.Single (HeadlessRenderer.AudioRequests);

            player.Stop ();
        } finally {
            File.Delete (wav);
        }
    }

    private sealed class InstantlyDoneSound : IPlayingSound
    {
        public void Wait () { }   // completes immediately, so a respawn loop's while keeps iterating
        public void Dispose () { }
    }

    [Fact]
    public void SoundPlayer_PlayLooping_RespawnsThroughNativeAudioWhenTheBackendCannotLoop ()
    {
        var launched = 0;
        NativeAudio.LauncherOverride = _ => { launched++; return new InstantlyDoneSound (); };

        var wav = WriteTempWav ();
        try {
            using var player = new SoundPlayer (wav);
            player.PlayLooping ();

            var deadline = DateTime.UtcNow.AddSeconds (5);
            while (launched < 2 && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep (10);

            Assert.True (launched >= 2, $"loop never respawned through NativeAudio (launches: {launched})");
            player.Stop ();
        } finally {
            File.Delete (wav);
        }
    }

    // The respawn loop runs on its own task, so a pass could be mid-start when Stop() cancelled it and launch just after Stop returned: a
    // sound nothing was left to stop, and (here) an audio request landing in whichever test ran next, which is how
    // SoundPlayer_Play_FallsBackToNativeAudioWhenTheBackendCannot came to see two requests on macOS. Stop must mean no further pass starts.
    [Fact]
    public void SoundPlayer_Stop_PreventsAnyFurtherLoopPassFromStarting ()
    {
        var launched = 0;
        NativeAudio.LauncherOverride = _ => { System.Threading.Interlocked.Increment (ref launched); return new InstantlyDoneSound (); };

        var wav = WriteTempWav ();
        try {
            for (var i = 0; i < 300; i++) {
                using var player = new SoundPlayer (wav);
                var before = System.Threading.Volatile.Read (ref launched);
                player.PlayLooping ();

                // Let the loop get going, so Stop lands while a pass is being started rather than before the task has run.
                var deadline = DateTime.UtcNow.AddSeconds (5);
                while (System.Threading.Volatile.Read (ref launched) < before + 3 && DateTime.UtcNow < deadline)
                    System.Threading.Thread.SpinWait (50);

                player.Stop ();

                var atStop = System.Threading.Volatile.Read (ref launched);
                System.Threading.Thread.Sleep (1);   // time for a pass that slipped past Stop to show itself
                Assert.Equal (atStop, System.Threading.Volatile.Read (ref launched));
            }
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SystemSound_Play_PrefersTheBackendWhenItAnswers ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var launched = 0;
        NativeAudio.LauncherOverride = _ => { launched++; return null; };

        SystemSounds.Hand.Play ();

        var request = Assert.Single (HeadlessRenderer.AudioRequests);
        Assert.Equal ("Hand", request.Value);
        Assert.True (request.IsSystemSound);
        Assert.Equal (0, launched);
    }

    [Fact]
    public void SystemSound_Play_FallsBackToNativeAudioWhenTheBackendCannot ()
    {
        var launched = 0;
        NativeAudio.LauncherOverride = _ => { launched++; return null; };

        SystemSounds.Hand.Play ();

        Assert.Single (HeadlessRenderer.AudioRequests);
        if (NativeAudio.SystemSoundCommands ("Hand").Length > 0)
            Assert.Equal (1, launched);
    }

    [Fact]
    public void SoundPlayer_AMissingFileNeverAsksTheBackendEither ()
    {
        HeadlessRenderer.AudioIsSupported = true;

        using var player = new SoundPlayer ("/definitely/not/here.wav");
        player.Play ();

        Assert.Empty (HeadlessRenderer.AudioRequests);
    }
}
