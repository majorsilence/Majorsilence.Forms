namespace Majorsilence.Forms.Backends
{
    /// <summary>
    /// Optional capability implemented by a platform backend that can play short audio cues in-process
    /// (Android's <c>MediaPlayer</c>, iOS's <c>AVAudioPlayer</c>), rather than <see cref="Media.NativeAudio"/>'s
    /// desktop path of spawning the operating system's own playback utility. Discovered via
    /// <c>Platform.Backend as IAudioBackend</c>, the same optional-capability pattern
    /// <see cref="IWebViewFactory"/> and <see cref="IReducedMotionSource"/> use.
    /// </summary>
    /// <remarks>
    /// A backend may implement this interface everywhere (so callers do not need a second, platform-gated
    /// check) while genuinely playing only on some rows: on a row with no native path of its own it returns
    /// <c>null</c> from every member, exactly what "does not implement this interface at all" also means to
    /// a caller. <see cref="Media.SoundPlayer"/> and <see cref="Media.SystemSounds"/> both treat a
    /// <c>null</c> result as "try the next thing" -- <see cref="Media.NativeAudio"/>'s OS-utility path on
    /// desktop, or silence where nothing else can play either. <see cref="Media.AudioPlayer"/> has no such
    /// fallback (see <see cref="PlayTrack"/>): a row with no native path here plays nothing for it at all,
    /// which is exactly what <see cref="Media.AudioPlayer.IsSupported"/> exists to let a caller check first.
    /// </remarks>
    public interface IAudioBackend
    {
        /// <summary>
        /// Starts playing the .wav file at <paramref name="path"/>, looping natively while
        /// <paramref name="loop"/> is <c>true</c> until the returned handle is disposed. Returns
        /// <c>null</c> if playback could not be started (missing file, unsupported format, no native
        /// playback engine on this row) -- never throws.
        /// </summary>
        Media.IPlayingSound? PlayFile (string path, bool loop);

        /// <summary>
        /// Starts playing the platform's own alert sound closest to <paramref name="name"/> (one of
        /// <see cref="Media.SystemSounds"/>'s five names: <c>Asterisk</c>, <c>Beep</c>, <c>Exclamation</c>,
        /// <c>Hand</c>, <c>Question</c>). Returns <c>null</c> if none is available -- never throws.
        /// </summary>
        Media.IPlayingSound? PlaySystemSound (string name);

        /// <summary>
        /// Starts playing the .wav file at <paramref name="path"/> with the given <paramref name="volume"/>
        /// (0 to 1) and <paramref name="usage"/> (which platform audio stream/session it routes onto),
        /// looping natively while <paramref name="loop"/> is <c>true</c> until the returned handle is
        /// disposed. Unlike <see cref="PlayFile"/>, a caller may start several tracks at once without
        /// stopping earlier ones -- see <see cref="Media.AudioPlayer.Play"/>. Returns <c>null</c> if
        /// playback could not be started, including simply because this row has no native path
        /// (<see cref="Media.AudioPlayer"/> has no OS-utility fallback the way <see cref="PlayFile"/>
        /// does) -- never throws.
        /// </summary>
        Media.IAudioTrack? PlayTrack (string path, bool loop, float volume, Media.AudioUsage usage);
    }
}
