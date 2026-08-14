using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Majorsilence.Forms.Media;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// The audio path is real now -- <see cref="NativeAudio"/> plays through the OS's own utility -- so
/// these tests assert the commands it builds and the process-lifecycle semantics SoundPlayer layers on
/// top (Stop kills, PlaySync waits, looping respawns, streams materialise once). The launcher seam
/// replaces process creation, so the suite asserts all of it without making a sound.
/// </summary>
public class NativeAudioTests : IDisposable
{
    private sealed class FakeSound : IPlayingSound
    {
        public int Waited;
        public bool Disposed;

        public void Wait () => Interlocked.Increment (ref Waited);
        public void Dispose () => Disposed = true;
    }

    private readonly List<(ProcessStartInfo Info, FakeSound Sound)> launches = [];

    public NativeAudioTests ()
        => NativeAudio.LauncherOverride = info => {
            var sound = new FakeSound ();
            lock (launches)
                launches.Add ((info, sound));
            return sound;
        };

    public void Dispose ()
    {
        NativeAudio.LauncherOverride = null;
        GC.SuppressFinalize (this);
    }

    private (ProcessStartInfo Info, FakeSound Sound) LastLaunch { get { lock (launches) return launches[^1]; } }
    private int LaunchCount { get { lock (launches) return launches.Count; } }

    [Fact]
    public void FileCommands_UseTheCurrentPlatformsOwnUtility ()
    {
        var commands = NativeAudio.FileCommands ("/tmp/x.wav");

        if (OperatingSystem.IsMacOS ()) {
            var cmd = Assert.Single (commands);
            Assert.Equal ("afplay", cmd.FileName);
            Assert.Equal ("/tmp/x.wav", Assert.Single (cmd.ArgumentList));
        } else if (OperatingSystem.IsLinux ()) {
            // paplay first (decodes more than WAV), ALSA's aplay as the fallback.
            Assert.Equal (new[] { "paplay", "aplay" }, commands.Select (c => c.FileName));
            Assert.All (commands, c => Assert.Equal ("/tmp/x.wav", Assert.Single (c.ArgumentList)));
        } else if (OperatingSystem.IsWindows ()) {
            var cmd = Assert.Single (commands);
            Assert.Equal ("powershell", cmd.FileName);
            // PlaySync in the CHILD, not Play: the child living for the duration of playback is what
            // gives Stop (kill) and PlaySync (wait) their meaning.
            Assert.Contains ("PlaySync", cmd.ArgumentList[^1], StringComparison.Ordinal);
        } else {
            Assert.Empty (commands);   // no utility to spawn: silent by design
        }
    }

    [Fact]
    public void SystemSoundCommands_MapAllFiveSoundsDistinctly ()
    {
        string[] names = ["Beep", "Asterisk", "Exclamation", "Hand", "Question"];
        var targets = names
            .Select (n => NativeAudio.SystemSoundCommands (n))
            .Where (c => c.Length > 0)
            .Select (c => string.Join (' ', c[0].ArgumentList))
            .ToList ();

        if (OperatingSystem.IsMacOS () || OperatingSystem.IsLinux () || OperatingSystem.IsWindows ()) {
            Assert.Equal (names.Length, targets.Count);
            Assert.Equal (targets.Count, targets.Distinct (StringComparer.Ordinal).Count ());
        }
    }

    [Fact]
    public void SystemSound_Play_LaunchesTheMappedCommand ()
    {
        SystemSounds.Hand.Play ();

        if (NativeAudio.SystemSoundCommands ("Hand").Length > 0)
            Assert.Equal (1, LaunchCount);
    }

    [Fact]
    public void SoundPlayer_PlaysTheLocationAndStopKillsIt ()
    {
        var wav = Path.Combine (Path.GetTempPath (), $"na-test-{Guid.NewGuid ():N}.wav");
        File.WriteAllBytes (wav, [1, 2, 3]);

        try {
            using var player = new SoundPlayer (wav);
            player.Play ();

            Assert.Equal (1, LaunchCount);
            Assert.Contains (wav, LastLaunch.Info.ArgumentList);
            Assert.False (LastLaunch.Sound.Disposed);

            var first = LastLaunch.Sound;
            player.Stop ();
            Assert.True (first.Disposed);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SoundPlayer_AMissingFilePlaysNothingAndDoesNotThrow ()
    {
        using var player = new SoundPlayer ("/definitely/not/here.wav");
        player.Play ();
        player.PlaySync ();

        Assert.Equal (0, LaunchCount);
    }

    [Fact]
    public void SoundPlayer_PlaySyncWaitsForTheChild ()
    {
        var wav = Path.Combine (Path.GetTempPath (), $"na-test-{Guid.NewGuid ():N}.wav");
        File.WriteAllBytes (wav, [1]);

        try {
            using var player = new SoundPlayer (wav);
            player.PlaySync ();

            Assert.Equal (1, LastLaunch.Sound.Waited);
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SoundPlayer_MaterialisesAStreamOnceAndCleansUpOnDispose ()
    {
        byte[] payload = [82, 73, 70, 70, 9, 9];   // arbitrary bytes; the OS utility is what parses WAV
        string? tempPath;

        using (var player = new SoundPlayer (new MemoryStream (payload))) {
            player.Play ();
            player.Play ();

            Assert.Equal (2, LaunchCount);
            tempPath = LastLaunch.Info.ArgumentList.Last ();

            // Same materialised file both times, holding the stream's bytes.
            lock (launches)
                Assert.Equal (tempPath, launches[0].Info.ArgumentList.Last ());
            Assert.Equal (payload, File.ReadAllBytes (tempPath));
        }

        // Dispose stops playback and deletes the materialised copy.
        Assert.False (File.Exists (tempPath));
    }

    [Fact]
    public void SoundPlayer_LoopRespawnsUntilStopped ()
    {
        var wav = Path.Combine (Path.GetTempPath (), $"na-test-{Guid.NewGuid ():N}.wav");
        File.WriteAllBytes (wav, [1]);

        try {
            using var player = new SoundPlayer (wav);
            player.PlayLooping ();

            // Each fake pass ends immediately, so the loop respawns; seeing more than one launch IS the
            // loop. Bounded wait keeps the test honest on a slow machine.
            var deadline = DateTime.UtcNow.AddSeconds (5);
            while (LaunchCount < 2 && DateTime.UtcNow < deadline)
                Thread.Sleep (10);

            Assert.True (LaunchCount >= 2, $"loop never respawned (launches: {LaunchCount})");

            player.Stop ();
            var settled = LaunchCount;
            Thread.Sleep (100);
            Assert.InRange (LaunchCount, settled, settled + 1);   // at most one in-flight pass after Stop
        } finally {
            File.Delete (wav);
        }
    }

    [Fact]
    public void SoundPlayer_ChangeEventsAreRealNow ()
    {
        using var player = new SoundPlayer ();
        var seen = new List<string> ();

        player.SoundLocationChanged += (_, _) => seen.Add ("location");
        player.StreamChanged += (_, _) => seen.Add ("stream");

        player.SoundLocation = "/tmp/a.wav";
        player.Stream = new MemoryStream ();
        player.SoundLocation = "/tmp/a.wav";   // unchanged: no event

        Assert.Equal (new[] { "location", "stream" }, seen);
    }
}
