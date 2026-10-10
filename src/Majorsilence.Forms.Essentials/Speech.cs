using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Majorsilence.Forms.Essentials
{
    /// <summary>
    /// How <see cref="Speech.SpeakAsync"/> reads a line back (register item F15): an early reader's whole reason this exists is
    /// hearing the words alongside seeing them, so the defaults are unhurried, not a screen reader's clipped pace.
    /// </summary>
    public sealed class SpeechOptions
    {
        /// <summary>Pitch, 0.5 (lower) to 2.0 (higher). 1.0 is the voice's own natural pitch.</summary>
        public float Pitch { get; set; } = 1f;

        /// <summary>Speaking rate, 0.5 (slower) to 2.0 (faster). 1.0 is the voice's own natural rate.</summary>
        public float Rate { get; set; } = 1f;

        /// <summary>Volume, 0 (silent) to 1 (full).</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>A BCP-47 language tag ("en-US"), or null for the platform's current default voice.</summary>
        public string? Locale { get; set; }

        /// <summary>
        /// The <see cref="SpeechVoice.Id"/> of an installed voice from <see cref="Speech.GetVoicesAsync"/>, or null for the platform's own
        /// choice. A voice that is no longer installed is ignored, and the line is spoken in the default voice for <see cref="Locale"/>.
        /// </summary>
        public string? Voice { get; set; }
    }

    /// <summary>What a platform reports about the sex of a voice.</summary>
    public enum VoiceGender
    {
        /// <summary>The platform does not say. Android and macOS never do; do not guess from the name.</summary>
        Unknown,

        /// <summary>A male voice.</summary>
        Male,

        /// <summary>A female voice.</summary>
        Female,
    }

    /// <summary>An installed voice, as <see cref="Speech.GetVoicesAsync"/> lists it.</summary>
    /// <param name="Id">What to put in <see cref="SpeechOptions.Voice"/>. Opaque: its shape differs by platform.</param>
    /// <param name="Name">A name to show a person.</param>
    /// <param name="Locale">The voice's language as a BCP-47 tag ("en-GB"), or an empty string when the platform does not say.</param>
    /// <param name="Gender">The voice's sex where the platform reports it (Windows, Linux's espeak, iOS 17 and later), else <see cref="VoiceGender.Unknown"/>.</param>
    /// <param name="RequiresNetwork">Whether the voice needs a connection to speak (some Android voices do), which an app that must speak offline can skip.</param>
    public sealed record SpeechVoice (string Id, string Name, string Locale, VoiceGender Gender, bool RequiresNetwork = false);

    /// <summary>
    /// What a platform actually does the speaking. Internal so <see cref="Speech"/> is the only public surface; a test injects
    /// its own via <see cref="Speech.Backend"/>.
    /// </summary>
    internal interface ISpeechBackend
    {
        /// <summary>Whether this platform can speak at all.</summary>
        bool IsSupported { get; }

        /// <summary>Speaks <paramref name="text"/>, completing when it finishes, is cancelled, or fails.</summary>
        Task SpeakAsync (string text, SpeechOptions options, CancellationToken cancellationToken);

        /// <summary>The installed voices, or none when the platform cannot list them. Never throws.</summary>
        Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync ();
    }

    /// <summary>
    /// Reads text aloud with the platform's own voice (register item F15): optional, for a reader who is still learning to
    /// read, not the app's primary way of telling anyone anything. Every member degrades to doing nothing rather than
    /// throwing when the platform cannot help, the same "never worth a crash" contract <c>Haptics</c> and
    /// <see cref="SecureStorage"/> already use for their own capability gaps. Check <see cref="IsSupported"/> before relying
    /// on a line actually having been read.
    /// </summary>
    public static class Speech
    {
        // Not the Backends.Platform seam every UI-facing capability (Haptics, LocalNotifications, KeepScreenAwake) uses, the
        // same reasoning SecureStorage gives for picking its own backend per target framework directly instead.
        internal static ISpeechBackend Backend { get; set; } = PlatformSpeech.Create ();

        /// <summary>Gets whether this platform can speak at all.</summary>
        public static bool IsSupported => Backend.IsSupported;

        /// <summary>
        /// Lists the installed voices, so a person can pick one (for instance a man's voice) and the app can pass its
        /// <see cref="SpeechVoice.Id"/> in <see cref="SpeechOptions.Voice"/>. Empty when <see cref="IsSupported"/> is false, when the
        /// platform cannot list its voices, or when listing them fails. Android starts its speech engine to answer, so the first call
        /// can take a moment.
        /// </summary>
        public static async Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync ()
        {
            if (!IsSupported)
                return [];

            try {
                return await Backend.GetVoicesAsync ().ConfigureAwait (false);
            } catch {
                return [];
            }
        }

        /// <summary>
        /// Speaks <paramref name="text"/> aloud, completing when it finishes, is cancelled through <paramref name="cancellationToken"/>,
        /// or the platform reports a failure. Starting a new utterance does not implicitly stop one already in progress on
        /// most platforms; call with a token from the same source and cancel it first for "interrupt and say this instead".
        /// Does nothing if <see cref="IsSupported"/> is false.
        /// </summary>
        public static Task SpeakAsync (string text, SpeechOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty (text);
            return Backend.SpeakAsync (text, options ?? new SpeechOptions (), cancellationToken);
        }
    }
}
