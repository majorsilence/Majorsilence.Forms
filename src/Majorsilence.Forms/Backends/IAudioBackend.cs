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
    /// <c>null</c> from both members, exactly what "does not implement this interface at all" also means to
    /// a caller. <see cref="Media.SoundPlayer"/> and <see cref="Media.SystemSounds"/> both treat a
    /// <c>null</c> result as "try the next thing" -- <see cref="Media.NativeAudio"/>'s OS-utility path on
    /// desktop, or silence where nothing else can play either.
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
    }
}
