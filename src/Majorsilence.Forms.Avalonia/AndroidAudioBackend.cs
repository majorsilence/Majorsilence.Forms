#if ANDROID
using System.Threading;
using Android.Media;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Android's in-process audio path: a <see cref="MediaPlayer"/> per play, tagged with
    /// <see cref="AudioAttributes"/> so playback routes through the notification volume stream (and
    /// respects Do Not Disturb the way a real notification sound does) rather than the default music
    /// stream a bare <see cref="MediaPlayer"/> would use.
    /// </summary>
    /// <remarks>
    /// Android has no equivalent of five distinct stock alert sounds (unlike Windows, macOS and the
    /// freedesktop sound theme, which <see cref="Media.NativeAudio.SystemSoundCommands"/> maps onto): the
    /// platform has exactly one user-configurable default per <see cref="RingtoneType"/>. All five
    /// <see cref="Media.SystemSounds"/> names play the device's default notification sound -- inventing an
    /// arbitrary split five ways would misrepresent a distinction Android does not actually have.
    /// </remarks>
    internal sealed class AndroidAudioBackend : IAudioBackend
    {
        private static readonly AudioAttributes Attributes = new AudioAttributes.Builder ()!
            .SetUsage (AudioUsageKind.NotificationEvent)!
            .SetContentType (AudioContentType.Sonification)!
            .Build ()!;

        /// <inheritdoc/>
        public Media.IPlayingSound? PlayFile (string path, bool loop)
        {
            MediaPlayer? player = null;
            try {
                player = new MediaPlayer ();
                // AudioAttributes before Prepare: set after, the routing decision it exists to make has
                // already been taken (documented Android ordering, the same reason PlaySystemSound below
                // does not use the MediaPlayer.Create(Context, Uri) convenience method either).
                player.SetAudioAttributes (Attributes);
                player.SetDataSource (path);
                player.Looping = loop;
                player.Prepare (); // synchronous: a local file already on disk needs no network wait
                player.Start ();
                return new PlayingMediaPlayer (player);
            } catch {
                player?.Release ();
                return null; // missing file, unsupported format, no audio hardware: silence, not a crash
            }
        }

        /// <inheritdoc/>
        public Media.IPlayingSound? PlaySystemSound (string name)
        {
            MediaPlayer? player = null;
            try {
                var context = global::Android.App.Application.Context;
                var uri = RingtoneManager.GetDefaultUri (RingtoneType.Notification);
                if (uri is null || context?.ContentResolver is null)
                    return null;

                player = new MediaPlayer ();
                player.SetAudioAttributes (Attributes);
                player.SetDataSource (context, uri);
                player.Prepare ();
                player.Start ();
                return new PlayingMediaPlayer (player);
            } catch {
                player?.Release ();
                return null;
            }
        }

        /// <inheritdoc/>
        public Media.IAudioTrack? PlayTrack (string path, bool loop, float volume, Media.AudioUsage usage)
        {
            MediaPlayer? player = null;
            try {
                player = new MediaPlayer ();
                player.SetAudioAttributes (AttributesFor (usage));
                player.SetDataSource (path);
                player.Looping = loop;
                player.SetVolume (volume, volume);
                player.Prepare ();
                player.Start ();
                return new PlayingTrack (player);
            } catch {
                player?.Release ();
                return null;
            }
        }

        // AudioUsage.Alarm -> USAGE_ALARM is the one that matters most: Android's alarm stream has its own
        // volume, independent of the media stream, so a siren mapped here is still audible with media
        // volume turned all the way down -- the whole reason AudioPlayer (register item F9) exists over
        // SoundPlayer/PlayFile, which are always AudioUsageKind.NotificationEvent (see Attributes above).
        private static AudioAttributes AttributesFor (Media.AudioUsage usage)
        {
            var (usageKind, contentType) = usage switch {
                Media.AudioUsage.Notification => (AudioUsageKind.NotificationEvent, AudioContentType.Sonification),
                Media.AudioUsage.Alarm => (AudioUsageKind.Alarm, AudioContentType.Sonification),
                Media.AudioUsage.Media => (AudioUsageKind.Media, AudioContentType.Music),
                _ => (AudioUsageKind.AssistanceSonification, AudioContentType.Sonification), // Effect, and anything unrecognised
            };

            return new AudioAttributes.Builder ()!.SetUsage (usageKind)!.SetContentType (contentType)!.Build ()!;
        }

        private sealed class PlayingTrack : Media.IAudioTrack
        {
            private readonly MediaPlayer player;
            private int disposed;

            public PlayingTrack (MediaPlayer player)
            {
                this.player = player;
                // Never raised while Looping is true -- MediaPlayer's own completion listener does not
                // fire until playback actually ends, which for a looping track is only Stop()/Dispose().
                player.Completion += (_, _) => Completed?.Invoke (this, System.EventArgs.Empty);
            }

            public event System.EventHandler? Completed;

            public void Dispose ()
            {
                if (Interlocked.Exchange (ref disposed, 1) != 0)
                    return;

                try {
                    if (player.IsPlaying)
                        player.Stop ();
                } catch { }

                player.Release ();
            }
        }

        private sealed class PlayingMediaPlayer : Media.IPlayingSound
        {
            private readonly MediaPlayer player;
            private readonly ManualResetEventSlim done = new (false);
            private int disposed;

            public PlayingMediaPlayer (MediaPlayer player)
            {
                this.player = player;
                player.Completion += (_, _) => done.Set ();
                player.Error += (_, _) => done.Set (); // a stuck stream must not hang PlaySync forever
            }

            public void Wait () => done.Wait ();

            public void Dispose ()
            {
                if (Interlocked.Exchange (ref disposed, 1) != 0)
                    return;

                try {
                    if (player.IsPlaying)
                        player.Stop ();
                } catch { }

                player.Release ();
                done.Set ();
            }
        }
    }
}
#endif
