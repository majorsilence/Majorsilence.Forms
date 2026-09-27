using System;
using System.Collections.Generic;
using System.IO;

namespace Majorsilence.Forms.Media
{
    /// <summary>
    /// Why a sound is playing, so a backend can route it onto the right platform audio stream.
    /// </summary>
    public enum AudioUsage
    {
        /// <summary>A short UI sound effect (a tap, a swipe) -- Android's <c>USAGE_ASSISTANCE_SONIFICATION</c>, no distinct iOS session.</summary>
        Effect,

        /// <summary>An incoming-message-style cue -- Android's <c>USAGE_NOTIFICATION</c>, an <c>Ambient</c> iOS session (respects the silent switch).</summary>
        Notification,

        /// <summary>
        /// A siren that must be heard: Android's <c>USAGE_ALARM</c> (its own volume stream, audible with media
        /// volume down) and an iOS <c>Playback</c> session (overrides the silent switch). The one usage this
        /// API deliberately makes it easy to be loud with -- match it to what actually needs that, not to
        /// every cue an app plays.
        /// </summary>
        Alarm,

        /// <summary>Ordinary media playback -- Android's <c>USAGE_MEDIA</c>, an iOS <c>Playback</c> session.</summary>
        Media,
    }

    /// <summary>A single playing instance started by <see cref="AudioPlayer"/>: disposing stops it early.</summary>
    /// <remarks>Public rather than internal because <see cref="Backends.IAudioBackend"/> -- implemented by backend assemblies outside this one -- returns it directly.</remarks>
    public interface IAudioTrack : IDisposable
    {
        /// <summary>Raised once, on the UI thread, when playback finishes on its own -- never raised for a <see cref="IDisposable.Dispose"/>-driven stop.</summary>
        event EventHandler? Completed;
    }

    /// <summary>
    /// Plays a .wav file or stream with a volume, a usage (which platform audio stream/session it plays
    /// through) and native looping, and lets more than one instance -- or repeated <see cref="Play"/>
    /// calls on the same instance -- overlap, the way rapid UI sound effects need to.
    /// </summary>
    /// <remarks>
    /// Not upstream (there is no <c>System.Media.AudioPlayer</c>): <see cref="SoundPlayer"/> stands in for
    /// GDI+'s API and stays limited to it deliberately, so this is the richer sibling an alert app reaches
    /// for instead -- a looping siren on the platform's alarm stream (audible with media volume turned all
    /// the way down), one that <see cref="Stop"/> can silence, is exactly the case <see cref="SoundPlayer"/>
    /// cannot serve. Real on Android (<c>MediaPlayer</c>, tagged with <see cref="AudioUsage"/>-mapped
    /// <c>AudioAttributes</c>) and iOS (<c>AVAudioPlayer</c> on a category chosen from
    /// <see cref="AudioUsage"/>); <see cref="IsSupported"/> is <c>false</c> everywhere else -- desktop
    /// already has <see cref="SoundPlayer"/> for the simple case, and none of live volume, a real platform
    /// audio-stream routing, or a genuine completion event map cleanly onto spawning a short-lived OS
    /// utility process the way <see cref="NativeAudio"/> does.
    /// </remarks>
    public class AudioPlayer : IDisposable
    {
        private readonly List<IAudioTrack> active = new ();
        private string sound_location = string.Empty;
        private System.IO.Stream? stream;
        private string? temp_file;
        private float volume = 1f;

        /// <summary>Initializes an empty player.</summary>
        public AudioPlayer () { }

        /// <summary>Initializes a player for the given .wav path.</summary>
        public AudioPlayer (string soundLocation) => SoundLocation = soundLocation ?? string.Empty;

        /// <summary>Initializes a player for the given .wav stream.</summary>
        public AudioPlayer (System.IO.Stream? stream) => Stream = stream;

        /// <summary>Gets or sets the path of the .wav to play.</summary>
        /// <remarks>Mirrors <see cref="SoundPlayer.SoundLocation"/>: wins over <see cref="Stream"/> when both are set.</remarks>
        public string SoundLocation {
            get => sound_location;
            set {
                value ??= string.Empty;
                if (sound_location == value)
                    return;

                sound_location = value;
                temp_file = DeleteTempFile (temp_file);
            }
        }

        /// <summary>Gets or sets the .wav stream to play.</summary>
        public System.IO.Stream? Stream {
            get => stream;
            set {
                if (ReferenceEquals (stream, value))
                    return;

                stream = value;
                temp_file = DeleteTempFile (temp_file);   // the materialised copy no longer matches
            }
        }

        /// <summary>Gets or sets the playback volume, from 0 (silent) to 1 (full). Values outside that range are clamped, never rejected.</summary>
        /// <remarks>Read when <see cref="Play"/> starts a track -- changing it does not retroactively affect a track already playing.</remarks>
        public float Volume {
            get => volume;
            set => volume = value < 0f ? 0f : value > 1f ? 1f : value;
        }

        /// <summary>Gets or sets whether a track loops until <see cref="Stop"/>. Read when <see cref="Play"/> starts a track.</summary>
        public bool Loop { get; set; }

        /// <summary>Gets or sets which platform audio stream/session a track plays through. Read when <see cref="Play"/> starts a track.</summary>
        public AudioUsage Usage { get; set; } = AudioUsage.Effect;

        /// <summary>
        /// Gets whether the active backend can actually play anything -- true on Android and iOS, false
        /// everywhere else. Cheap to call ahead of committing to a looping-alarm UX, unlike
        /// <see cref="SoundPlayer"/>'s silent degrade-and-say-nothing contract.
        /// </summary>
        public static bool IsSupported => Backends.Platform.Backend is Backends.IAudioBackend;

        /// <summary>
        /// Starts a new track playing. Does not stop a track already playing from an earlier <see cref="Play"/>
        /// call on this same instance -- calling it repeatedly lets short cues overlap, matching how rapid UI
        /// sound effects are actually used; call <see cref="Stop"/> first for "restart" behaviour instead.
        /// Fire-and-forget and never throws: a missing file, an unsupported backend or a platform failure
        /// degrades to silence.
        /// </summary>
        public void Play ()
        {
            var path = ResolvePath ();
            if (path is null)
                return;

            if (Backends.Platform.Backend is not Backends.IAudioBackend audio)
                return;

            var track = audio.PlayTrack (path, Loop, Volume, Usage);
            if (track is null)
                return;

            lock (active)
                active.Add (track);

            track.Completed += (_, _) => {
                lock (active)
                    active.Remove (track);
                // A track that finished on its own still holds a native player (Android's MediaPlayer,
                // iOS's AVAudioPlayer) that must be released; Stop() disposes a still-playing one for the
                // same reason, this is that same cleanup for the "it finished before Stop was ever called" case.
                track.Dispose ();
                Completed?.Invoke (this, EventArgs.Empty);
            };
        }

        /// <summary>Stops every track this instance has started that is still playing.</summary>
        public void Stop ()
        {
            IAudioTrack[] tracks;
            lock (active) {
                tracks = active.ToArray ();
                active.Clear ();
            }

            foreach (var track in tracks)
                track.Dispose ();
        }

        /// <summary>Raised when a track this instance started finishes on its own. Not raised for a <see cref="Stop"/>-driven stop.</summary>
        public event EventHandler? Completed;

        // SoundLocation wins when both are set, matching SoundPlayer's own "whichever identifies a sound" contract.
        private string? ResolvePath ()
        {
            if (sound_location.Length > 0)
                return File.Exists (sound_location) ? sound_location : null;

            if (stream is null)
                return null;

            if (temp_file is null) {
                try {
                    var path = Path.Combine (Path.GetTempPath (), $"majorsilence-audioplayer-{Guid.NewGuid ():N}.wav");

                    using (var file = File.Create (path)) {
                        if (stream.CanSeek)
                            stream.Position = 0;
                        stream.CopyTo (file);
                    }

                    temp_file = path;
                } catch {
                    return null;   // unreadable stream or unwritable temp dir: silence, not an exception
                }
            }

            return temp_file;
        }

        private static string? DeleteTempFile (string? path)
        {
            if (path is not null) {
                try { File.Delete (path); } catch { }
            }

            return null;
        }

        /// <summary>Stops every playing track and releases the materialised stream copy, if any.</summary>
        public void Dispose ()
        {
            Stop ();
            temp_file = DeleteTempFile (temp_file);
            GC.SuppressFinalize (this);
        }
    }
}
