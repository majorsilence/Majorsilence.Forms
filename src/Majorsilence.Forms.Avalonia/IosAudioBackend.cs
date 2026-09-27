#if IOS
using System.Threading;
using AudioToolbox;
using AVFoundation;
using Foundation;

namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// iOS's in-process audio path: <see cref="AVAudioPlayer"/> for a .wav file, <see cref="SystemSound"/>
    /// (AudioToolbox) for the five stock alert names.
    /// </summary>
    /// <remarks>
    /// The audio session category is <see cref="AVAudioSessionCategory.Ambient"/> -- mixes with other
    /// audio and, importantly, respects the silent switch and Do Not Disturb, the same conservative
    /// default <see cref="AndroidAudioBackend"/>'s <c>NotificationEvent</c> usage takes. A category that
    /// overrides the silent switch (<c>Playback</c>) is deliberately not the default here: that is a
    /// judgment call for a specific alert's own <c>Usage</c> (register item F9), not something a generic
    /// "play this cue" API should decide unasked.
    /// </remarks>
    internal sealed class IosAudioBackend : IAudioBackend
    {
        // iOS has no public API enumerating "the five stock alert sounds" the way Android's RingtoneManager
        // or the desktop OSes' own sound themes do. AudioServicesPlaySystemSound's numeric IDs are Apple's
        // own, undocumented but stable bundled system-sound bank (unchanged for over a decade), the same
        // kind of "reach a real OS asset by a known, empirically stable identifier" NativeAudio's own
        // desktop file-path mapping already relies on. Chosen for rough equivalence, the same judgment call
        // NativeAudio.SystemSoundCommands documents for its own five-way split.
        private static uint SystemSoundId (string name) => name switch {
            nameof (Media.SystemSounds.Asterisk) => 1000,     // new-mail.caf (MailReceived): informational
            nameof (Media.SystemSounds.Exclamation) => 1006,  // low_power.caf (LowPower): a warning tone
            nameof (Media.SystemSounds.Hand) => 1073,         // ct-error.caf (AudioToneError): the error tone
            nameof (Media.SystemSounds.Question) => 1001,     // mail-sent.caf (MailSent)
            _ => 1103,                                        // Tink.caf (KeyPressed): Beep, and anything unrecognised
        };

        /// <inheritdoc/>
        public Media.IPlayingSound? PlayFile (string path, bool loop)
        {
            try {
                if (!ActivateSession ())
                    return null;

                using var url = NSUrl.FromFilename (path);
                var player = AVAudioPlayer.FromUrl (url, out var error);
                if (player is null || error is not null)
                    return null;

                player.NumberOfLoops = loop ? -1 : 0;
                if (!player.PrepareToPlay () || !player.Play ()) {
                    player.Dispose ();
                    return null;
                }

                return new PlayingAudioPlayer (player);
            } catch {
                return null; // missing file, unsupported format, no audio hardware: silence, not a crash
            }
        }

        /// <inheritdoc/>
        public Media.IPlayingSound? PlaySystemSound (string name)
        {
            try {
                if (!ActivateSession ())
                    return null;

                var sound = new SystemSound (SystemSoundId (name));
                return new PlayingSystemSound (sound);
            } catch {
                return null;
            }
        }

        private static bool ActivateSession ()
        {
            var session = AVAudioSession.SharedInstance ();
            if (!session.SetCategory (AVAudioSessionCategory.Ambient, out var categoryError) || categoryError is not null)
                return false;

            return session.SetActive (true, out var activeError) && activeError is null;
        }

        private sealed class PlayingAudioPlayer : Media.IPlayingSound
        {
            private readonly AVAudioPlayer player;
            private readonly ManualResetEventSlim done = new (false);
            private int disposed;

            public PlayingAudioPlayer (AVAudioPlayer player)
            {
                this.player = player;
                player.FinishedPlaying += (_, _) => done.Set ();
            }

            public void Wait () => done.Wait ();

            public void Dispose ()
            {
                if (Interlocked.Exchange (ref disposed, 1) != 0)
                    return;

                try { player.Stop (); } catch { }
                player.Dispose ();
                done.Set ();
            }
        }

        // SystemSound has no Stop -- the upstream System.Media.SystemSound contract this stands in for
        // has never had one either (Play is genuinely fire-and-forget, capped at 30 s by AudioToolbox
        // itself), so Dispose here only ever releases the completion registration, never cuts audio short.
        private sealed class PlayingSystemSound : Media.IPlayingSound
        {
            private readonly SystemSound sound;
            private readonly ManualResetEventSlim done = new (false);
            private int disposed;

            public PlayingSystemSound (SystemSound sound)
            {
                this.sound = sound;
                sound.PlaySystemSound (() => done.Set ());
            }

            public void Wait () => done.Wait ();

            public void Dispose ()
            {
                if (Interlocked.Exchange (ref disposed, 1) != 0)
                    return;

                sound.Dispose ();
                done.Set ();
            }
        }
    }
}
#endif
