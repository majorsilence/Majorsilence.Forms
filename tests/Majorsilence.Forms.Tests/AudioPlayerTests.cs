using System;
using System.IO;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Media;
using Xunit;

namespace Majorsilence.Forms.Tests;

// AudioPlayer is the richer sibling of SoundPlayer, real only where a backend implements
// IAudioBackend.PlayTrack (Android, iOS) -- IsSupported is how a caller checks that ahead of committing to
// a looping-alarm UX, unlike SoundPlayer's silent degrade. HeadlessRenderer.AudioIsSupported is false by
// default (the rest of the suite runs with Headless already active as the ambient backend), so a test that
// wants AudioPlayer to actually play sets it true itself.
[Collection ("Headless")]
public class AudioPlayerTests : IDisposable
{
    public AudioPlayerTests ()
    {
        HeadlessRenderer.Use ();
        HeadlessRenderer.ClearAudioTrackRequests ();
    }

    public void Dispose ()
    {
        HeadlessRenderer.AudioIsSupported = false;
        HeadlessRenderer.ClearAudioTrackRequests ();
        GC.SuppressFinalize (this);
    }

    private static string WriteTempWav ()
    {
        var path = Path.Combine (Path.GetTempPath (), $"audioplayer-test-{Guid.NewGuid ():N}.wav");
        File.WriteAllBytes (path, [1, 2, 3]);
        return path;
    }

    [Fact]
    public void IsSupported_ReflectsWhetherTheBackendImplementsIAudioBackend ()
    {
        // AudioIsSupported (a Headless-only setting on the fake) governs whether PlayTrack answers, but
        // IsSupported itself only asks "is the active backend IAudioBackend at all" -- true the instant
        // HeadlessRenderer.Use () installs a backend that implements it, regardless of AudioIsSupported.
        Assert.True (AudioPlayer.IsSupported);
    }

    [Fact]
    public void Volume_ClampsToZeroToOne ()
    {
        using var player = new AudioPlayer ();

        player.Volume = 1.5f;
        Assert.Equal (1f, player.Volume);

        player.Volume = -0.5f;
        Assert.Equal (0f, player.Volume);

        player.Volume = 0.4f;
        Assert.Equal (0.4f, player.Volume);
    }

    [Fact]
    public void Play_AsksTheBackendWithLoopVolumeAndUsage ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var wav = WriteTempWav ();

        try {
            using var player = new AudioPlayer (wav) { Loop = true, Volume = 0.25f, Usage = AudioUsage.Alarm };
            player.Play ();

            var request = Assert.Single (HeadlessRenderer.AudioTrackRequests);
            Assert.Equal (wav, request.Path);
            Assert.True (request.Loop);
            Assert.Equal (0.25f, request.Volume);
            Assert.Equal (AudioUsage.Alarm, request.Usage);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void Play_DoesNotAskTheBackendWhenUnsupported ()
    {
        // AudioIsSupported left false (the default): AudioPlayer has no NativeAudio-style fallback, so
        // this must degrade to silence exactly as SoundPlayer does when nothing at all can play.
        var wav = WriteTempWav ();

        try {
            using var player = new AudioPlayer (wav);
            player.Play ();

            Assert.Equal (0, HeadlessRenderer.ActiveAudioTrackCount);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void Play_AMissingFileNeverAsksTheBackendEither ()
    {
        HeadlessRenderer.AudioIsSupported = true;

        using var player = new AudioPlayer ("/definitely/not/here.wav");
        player.Play ();

        Assert.Empty (HeadlessRenderer.AudioTrackRequests);
    }

    [Fact]
    public void Play_CalledTwiceOverlapsRatherThanStoppingTheFirst ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var wav = WriteTempWav ();

        try {
            using var player = new AudioPlayer (wav);
            player.Play ();
            player.Play ();

            Assert.Equal (2, HeadlessRenderer.AudioTrackRequests.Count);
            Assert.Equal (2, HeadlessRenderer.ActiveAudioTrackCount);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void Stop_DisposesEveryTrackThisInstanceStarted ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var wav = WriteTempWav ();

        try {
            using var player = new AudioPlayer (wav);
            player.Play ();
            player.Play ();
            Assert.Equal (2, HeadlessRenderer.ActiveAudioTrackCount);

            player.Stop ();

            Assert.Equal (0, HeadlessRenderer.ActiveAudioTrackCount);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void Stop_DoesNotAffectAnotherInstancesTracks ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var wav = WriteTempWav ();

        try {
            using var a = new AudioPlayer (wav);
            using var b = new AudioPlayer (wav);
            a.Play ();
            b.Play ();
            Assert.Equal (2, HeadlessRenderer.ActiveAudioTrackCount);

            a.Stop ();

            Assert.Equal (1, HeadlessRenderer.ActiveAudioTrackCount);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void Completed_RaisedWhenATrackFinishesOnItsOwn ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var wav = WriteTempWav ();

        try {
            using var player = new AudioPlayer (wav);
            var raised = 0;
            player.Completed += (_, _) => raised++;

            player.Play ();
            Assert.True (HeadlessRenderer.CompleteNextAudioTrack ());

            Assert.Equal (1, raised);
            Assert.Equal (0, HeadlessRenderer.ActiveAudioTrackCount);   // the finished track is released too
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void Completed_NotRaisedByStop ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        var wav = WriteTempWav ();

        try {
            using var player = new AudioPlayer (wav);
            var raised = 0;
            player.Completed += (_, _) => raised++;

            player.Play ();
            player.Stop ();

            Assert.Equal (0, raised);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SoundLocation_MaterialisesAStreamOnceAndCleansUpOnDispose ()
    {
        HeadlessRenderer.AudioIsSupported = true;
        byte[] payload = [82, 73, 70, 70, 9, 9];
        string? tempPath;

        using (var player = new AudioPlayer (new MemoryStream (payload))) {
            player.Play ();
            player.Play ();

            var requests = HeadlessRenderer.AudioTrackRequests;
            Assert.Equal (2, requests.Count);
            tempPath = requests[0].Path;

            Assert.Equal (tempPath, requests[1].Path);   // same materialised file both times
            Assert.Equal (payload, File.ReadAllBytes (tempPath));
        }

        Assert.False (File.Exists (tempPath));   // Dispose stops playback and deletes the materialised copy
    }
}
